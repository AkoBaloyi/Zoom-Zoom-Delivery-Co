#!/usr/bin/env python3
"""Dump the serialized fields of MonoBehaviour components in a Unity scene or prefab.

Read-only. Nothing here writes to the project.

WHY THIS EXISTS
A tuning asset is only half the story: the value that actually runs is whatever the scene
serialized onto the component, and the only honest way to quote an order time limit or a cargo
capacity is to read it out of that scene's YAML with a line number attached. Hand-grepping a
10 000-line scene for `capacity:` finds the key but loses which GameObject owns it, and loses the
line number once the file is re-saved. This resolves the script GUID to a class name, the
m_GameObject fileID to the owning object's name, and prints every field with its absolute line
number in the file, so a claim can be cited as `path:line`.

A scene can also be read straight out of a git revision, which matters when the reference tree is
`origin/main` and the working tree is a feature branch. Bytes come from `git show` unchanged;
they are never written to disk and never re-encoded by a shell pipeline.

USAGE
    python docs/tooling/scene-tools/component_fields.py <scene> [NameOrGuid ...]
    python docs/tooling/scene-tools/component_fields.py --rev origin/main <repo-relative scene> ...

    # every order-system component in the extracted reference scene
    python docs/tooling/scene-tools/component_fields.py .agents/.../ref/Hard.unity

    # just two of them, by class name
    python docs/tooling/scene-tools/component_fields.py --rev origin/main \
        "Assets/Project/Scenes/Possible game scene.unity" OrderManager ShiftTimer

With no filter, every MonoBehaviour whose script GUID resolves to a script under
Assets/Project/Scripts is printed. Filters match either a class name (OrderManager) or a raw GUID.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
SCRIPTS_ROOT = REPO_ROOT / "Assets" / "Project" / "Scripts"

DOC_START = re.compile(r"^--- !u!(\d+) &(-?\d+)")
GUID_LINE = re.compile(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]+)")
GAMEOBJECT_LINE = re.compile(r"^  m_GameObject:\s*\{fileID:\s*(-?\d+)\}")
NAME_LINE = re.compile(r"^  m_Name:\s*(.*)$")
FIELD_LINE = re.compile(r"^  ([A-Za-z_][A-Za-z0-9_]*):(.*)$")

# Keys every MonoBehaviour carries; not design data, so they only clutter the output.
BOILERPLATE = {
    "m_ObjectHideFlags",
    "m_CorrespondingSourceObject",
    "m_PrefabInstance",
    "m_PrefabAsset",
    "m_GameObject",
    "m_Enabled",
    "m_EditorHideFlags",
    "m_Script",
    "m_Name",
    "m_EditorClassIdentifier",
}


def guid_to_script_name() -> dict[str, str]:
    """Map every script GUID under Assets/Project/Scripts to its file stem."""
    table: dict[str, str] = {}
    if not SCRIPTS_ROOT.is_dir():
        return table
    for meta in SCRIPTS_ROOT.rglob("*.cs.meta"):
        text = meta.read_text(encoding="utf-8", errors="replace")
        match = re.search(r"^guid:\s*([0-9a-f]+)", text, re.MULTILINE)
        if match:
            table[match.group(1)] = meta.name[: -len(".cs.meta")]
    return table


def read_scene(scene: str, rev: str | None) -> list[str]:
    """Scene text as lines. From a git revision when rev is given, else from the working tree."""
    if rev:
        result = subprocess.run(
            ["git", "show", f"{rev}:{scene}"],
            cwd=REPO_ROOT,
            capture_output=True,
            check=False,
        )
        if result.returncode != 0:
            sys.exit(f"git show {rev}:{scene} failed: {result.stderr.decode(errors='replace').strip()}")
        raw = result.stdout
    else:
        path = Path(scene)
        if not path.is_absolute():
            path = REPO_ROOT / scene
        if not path.is_file():
            sys.exit(f"not a file: {path}")
        raw = path.read_bytes()
    # Unity writes UTF-8; some hand-edited files carry a BOM.
    return raw.decode("utf-8-sig", errors="replace").splitlines()


class Document:
    """One `--- !u!class &fileID` block of a Unity YAML file."""

    def __init__(self, class_id: str, file_id: str, start_line: int) -> None:
        self.class_id = class_id
        self.file_id = file_id
        self.start_line = start_line  # 1-based
        self.guid: str | None = None
        self.game_object: str | None = None
        self.name: str | None = None
        self.fields: list[tuple[int, str, str]] = []  # (line, key, value)


def parse(lines: list[str]) -> list[Document]:
    docs: list[Document] = []
    current: Document | None = None

    for index, line in enumerate(lines, start=1):
        start = DOC_START.match(line)
        if start:
            current = Document(start.group(1), start.group(2), index)
            docs.append(current)
            continue
        if current is None:
            continue

        guid = GUID_LINE.search(line)
        if guid:
            current.guid = guid.group(1)

        owner = GAMEOBJECT_LINE.match(line)
        if owner:
            current.game_object = owner.group(1)

        named = NAME_LINE.match(line)
        if named:
            current.name = named.group(1).strip()

        field = FIELD_LINE.match(line)
        if field and field.group(1) not in BOILERPLATE:
            current.fields.append((index, field.group(1), field.group(2).strip()))

    return docs


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("scene", help="scene or prefab path (repo-relative when --rev is used)")
    parser.add_argument("filters", nargs="*", help="class names or GUIDs to show; default is all project scripts")
    parser.add_argument("--rev", default=None, help="read the file from this git revision instead of the working tree")
    args = parser.parse_args()

    names = guid_to_script_name()
    lines = read_scene(args.scene, args.rev)
    docs = parse(lines)

    # fileID -> GameObject name, so a component can say which object it sits on.
    objects = {doc.file_id: (doc.name or "(unnamed)") for doc in docs if doc.class_id == "1"}

    wanted_guids = {f for f in args.filters if re.fullmatch(r"[0-9a-f]{32}", f)}
    wanted_names = {f for f in args.filters if f not in wanted_guids}

    source = f"{args.rev}:{args.scene}" if args.rev else args.scene
    print(f"# {source}  ({len(lines)} lines, {len(docs)} documents)")

    shown = 0
    for doc in docs:
        if doc.class_id != "114" or not doc.guid:
            continue
        script = names.get(doc.guid)
        if args.filters:
            if doc.guid not in wanted_guids and (script is None or script not in wanted_names):
                continue
        elif script is None:
            continue  # package/engine script, not this project's design data

        shown += 1
        owner = objects.get(doc.game_object or "", "(owner not in this file)")
        label = script or f"guid {doc.guid}"
        print(f"\n{label}  on GameObject '{owner}'  (document at line {doc.start_line}, guid {doc.guid})")
        for line_no, key, value in doc.fields:
            print(f"  {args.scene}:{line_no}  {key}: {value}")

    if shown == 0:
        print("\n(no matching MonoBehaviour found)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
