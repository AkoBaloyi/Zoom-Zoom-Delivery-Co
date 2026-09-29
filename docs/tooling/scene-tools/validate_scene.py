"""Structural checks on a Unity scene YAML: duplicate anchors, dangling fileID references,
orphaned transforms, and line endings.
"""
import re
import sys
from collections import Counter

DOC = re.compile(r"^--- !u!(\d+) &(\d+)")
REF = re.compile(r"fileID: (\d+)")


def main(path):
    raw = open(path, "rb").read()
    if b"\r\n" in raw:
        print("FAIL  file contains CRLF; .gitattributes mandates eol=lf")
        return 1
    text = raw.decode("utf-8")

    anchors = []
    cls_of = {}
    for line in text.splitlines():
        m = DOC.match(line)
        if m:
            anchors.append(int(m.group(2)))
            cls_of[int(m.group(2))] = int(m.group(1))

    dupes = [a for a, n in Counter(anchors).items() if n > 1]
    print(f"documents: {len(anchors)}  distinct anchors: {len(set(anchors))}")
    if dupes:
        print(f"FAIL  duplicate anchors: {dupes}")
        return 1
    print("ok    no duplicate anchors")

    known = set(anchors) | {0}
    dangling = sorted({int(r) for r in REF.findall(text)} - known)
    # asset references carry a guid on the same line, those are external and fine
    truly = []
    for line in text.splitlines():
        for r in REF.findall(line):
            if int(r) in dangling and "guid:" not in line:
                truly.append((int(r), line.strip()))
    if truly:
        print(f"FAIL  {len(truly)} local fileID references point at nothing:")
        for fid, line in truly[:10]:
            print(f"        {fid}  in  {line}")
        return 1
    print("ok    every local fileID reference resolves")

    # every Transform must have a father that exists, or be listed in SceneRoots
    roots = set()
    in_roots = False
    for line in text.splitlines():
        if line.startswith("  m_Roots:"):
            in_roots = True
            continue
        if in_roots:
            m = re.match(r"  - \{fileID: (\d+)\}", line)
            if m:
                roots.add(int(m.group(1)))
            else:
                in_roots = False

    bodies = {}
    cur = None
    for line in text.splitlines():
        m = DOC.match(line)
        if m:
            cur = int(m.group(2))
            bodies[cur] = []
        elif cur is not None:
            bodies[cur].append(line)

    orphans = []
    for fid, cls in cls_of.items():
        if cls != 4:
            continue
        body = "\n".join(bodies[fid])
        fm = re.search(r"m_Father: \{fileID: (\d+)\}", body)
        father = int(fm.group(1)) if fm else 0
        if father == 0 and fid not in roots:
            orphans.append(fid)
    if orphans:
        print(f"FAIL  root transforms missing from SceneRoots: {orphans}")
        return 1
    print(f"ok    all {sum(1 for c in cls_of.values() if c == 4)} transforms parented or "
          f"registered as roots ({len(roots)} roots)")

    # children lists must be reciprocal with m_Father
    bad = []
    for fid, cls in cls_of.items():
        if cls != 4:
            continue
        body = "\n".join(bodies[fid])
        for child in re.findall(r"  - \{fileID: (\d+)\}", body.split("m_Father")[0]):
            cbody = "\n".join(bodies.get(int(child), []))
            fm = re.search(r"m_Father: \{fileID: (\d+)\}", cbody)
            if not fm or int(fm.group(1)) != fid:
                bad.append((fid, child))
    if bad:
        print(f"FAIL  {len(bad)} parent/child links are not reciprocal: {bad[:10]}")
        return 1
    print("ok    parent and child links agree")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
