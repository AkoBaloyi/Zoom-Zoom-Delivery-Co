"""Finds serialization drift: keys serialized in a scene/prefab/asset that no current C# script
declares, and declared fields the file omits.

WHY THIS EXISTS
A stale key is worse than a missing one. Unity keeps writing a field it no longer knows about
until the next time the object is re-serialized, so a designer reading the scene YAML (or a text
diff in a code review) sees `hardBrakeDecelThreshold: 18` and reasonably concludes the threshold
is 18. The runtime never reads it: the field was renamed, so the C# initialiser wins and the real
value is something else entirely. The same goes the other way: a key absent from the YAML is NOT
zero, it is whatever the C# field initialiser says, which is why "the asset does not mention
valuePerMetre" does not mean valuePerMetre is 0.

Read-only. Opens files, prints findings, writes nothing.

USAGE
    python docs/tooling/scene-tools/serialization_drift.py <file-or-dir> [<file-or-dir> ...]
    python docs/tooling/scene-tools/serialization_drift.py --missing <file>   # also list omitted
                                                                             # fields (C# default)

Only components whose script lives under Assets/Project/Scripts are checked; Unity's own
components and third-party assets are skipped, since their field sets are not ours to know.
"""
import os
import re
import sys

SCRIPTS_ROOT = os.path.join("Assets", "Project", "Scripts")

DOC_START = re.compile(r"^--- !u!(\d+) &(\d+)")
SCRIPT_REF = re.compile(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})")
GUID_IN_META = re.compile(r"^guid: ([0-9a-f]{32})", re.M)

# A field declaration, after attributes have been stripped onto the same logical line.
FIELD = re.compile(
    r"^\s*(?:public|private|protected|internal)"
    r"(?:\s+(?:static|readonly|volatile|new))*"
    r"\s+[\w<>,\[\]\.\?\s]+?"
    r"\s+(\w+)\s*(?:=[^=]|;)"
)
FORMERLY = re.compile(r"FormerlySerializedAs\(\s*\"(\w+)\"")

# Keys Unity writes for every MonoBehaviour, plus the YAML scaffolding.
BUILTIN_KEYS = {
    "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
    "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
    "m_EditorClassIdentifier", "serializedVersion",
}


def declared_fields(cs_path):
    """Field names a MonoBehaviour/ScriptableObject in this file can serialize.

    Inclusive on purpose: every public or [SerializeField] field name, plus any
    [FormerlySerializedAs] alias. Properties, methods and events are excluded because they are
    never serialized and would otherwise mask a genuinely stale key of the same name.
    """
    names = set()
    try:
        text = open(cs_path, encoding="utf-8", errors="replace").read()
    except OSError:
        return names

    pending_attrs = ""
    attr_depth = 0
    for raw in text.split("\n"):
        line = raw.split("//")[0]
        stripped = line.strip()
        if not stripped:
            continue

        # Attributes can span several lines, because Tooltip strings are concatenated across
        # them throughout this project. Track bracket depth rather than guessing from the first
        # character, or [SerializeField] gets separated from its field and the field is missed.
        if attr_depth > 0 or stripped.startswith("["):
            consumed = ""
            for ch in stripped:
                consumed += ch
                if ch == "[":
                    attr_depth += 1
                elif ch == "]":
                    attr_depth -= 1
                    if attr_depth == 0:
                        break
            pending_attrs += consumed
            if attr_depth > 0:
                continue
            line = stripped[len(consumed):]
            stripped = line.strip()
            if not stripped:
                continue

        attrs, pending_attrs = pending_attrs, ""

        if stripped.startswith(("*", "/*", "#")):
            continue
        # A method or property signature has a paren or a brace BEFORE any initialiser. A field
        # initialiser like `= new Color(...)` has one after, and must not be rejected for it.
        head = stripped.split("=")[0]
        if "(" in head or "{" in head or "=>" in stripped:
            continue
        if " event " in stripped or stripped.startswith("event "):
            continue
        if " const " in stripped:
            continue

        m = FIELD.match(line)
        if not m:
            continue
        is_public = stripped.startswith("public")
        has_serializefield = "SerializeField" in attrs
        if not (is_public or has_serializefield):
            continue
        names.add(m.group(1))
        names.update(FORMERLY.findall(attrs))
    return names


def script_index(repo_root):
    """guid -> (relative .cs path, declared field names)."""
    index = {}
    root = os.path.join(repo_root, SCRIPTS_ROOT)
    for folder, _dirs, files in os.walk(root):
        for f in files:
            if not f.endswith(".cs.meta"):
                continue
            meta = os.path.join(folder, f)
            m = GUID_IN_META.search(open(meta, encoding="utf-8", errors="replace").read())
            if not m:
                continue
            cs = meta[: -len(".meta")]
            index[m.group(1)] = (os.path.relpath(cs, repo_root).replace("\\", "/"),
                                 declared_fields(cs))
    return index


def documents(path):
    """Yields (first_line_number, [lines]) per YAML document in a Unity file."""
    lines = open(path, encoding="utf-8", errors="replace").read().split("\n")
    start, body = None, []
    for i, line in enumerate(lines, 1):
        if DOC_START.match(line):
            if start is not None:
                yield start, body
            start, body = i, [line]
        elif start is not None:
            body.append(line)
    if start is not None:
        yield start, body


def check_file(path, index, repo_root, show_missing=False):
    rel = os.path.relpath(path, repo_root).replace("\\", "/")
    findings = 0
    for first_line, body in documents(path):
        text = "\n".join(body)
        m = SCRIPT_REF.search(text)
        if not m or m.group(1) not in index:
            continue
        cs_rel, fields = index[m.group(1)]

        # Top-level keys of this document, i.e. exactly two spaces of indent.
        keys = []
        for offset, line in enumerate(body):
            km = re.match(r"^  (\w+):", line)
            if km:
                keys.append((km.group(1), first_line + offset))

        seen = set()
        for key, line_no in keys:
            if key in BUILTIN_KEYS or key in fields or key in seen:
                continue
            seen.add(key)
            print(f"STALE   {rel}:{line_no}  {key}  "
                  f"(no such serialized field in {cs_rel})")
            findings += 1

        if show_missing:
            present = {k for k, _ in keys}
            for field in sorted(fields - present):
                print(f"OMITTED {rel}:{first_line}  {field}  "
                      f"(absent from the file, so {cs_rel}'s initialiser is what runs)")
    return findings


def main(argv):
    show_missing = "--missing" in argv
    targets = [a for a in argv if not a.startswith("--")]
    if not targets:
        print(__doc__)
        return 2

    repo_root = os.getcwd()
    index = script_index(repo_root)
    print(f"scripts indexed: {len(index)}")

    files = []
    for t in targets:
        if os.path.isdir(t):
            for folder, _dirs, names in os.walk(t):
                for n in names:
                    if n.endswith((".unity", ".prefab", ".asset")):
                        files.append(os.path.join(folder, n))
        else:
            files.append(t)

    total = 0
    for f in sorted(files):
        total += check_file(f, index, repo_root, show_missing)
    print(f"\n{len(files)} file(s) checked, {total} stale key(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
