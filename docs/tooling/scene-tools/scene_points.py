"""Read a Unity scene YAML and report world-space transforms.

Used to find real ground positions in the greybox district without opening Unity.
Handles the subset of YAML Unity emits: one document per object, anchored by fileID.
"""
import math
import re
import sys
from collections import defaultdict

DOC = re.compile(r"^--- !u!(\d+) &(\d+)")
KV = re.compile(r"^(\s*)([\w_]+):\s*(.*)$")
VEC = re.compile(r"\{([^}]*)\}")


def parse(path):
    docs = {}
    cls_of = {}
    cur = None
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        for line in fh:
            m = DOC.match(line)
            if m:
                cls, fid = int(m.group(1)), int(m.group(2))
                cur = []
                docs[fid] = cur
                cls_of[fid] = cls
                continue
            if cur is not None:
                cur.append(line.rstrip("\n"))
    return docs, cls_of


def flat(body):
    """Collapse a doc body into key -> raw value, keeping the first occurrence."""
    out = {}
    for line in body:
        m = KV.match(line)
        if not m:
            continue
        key, val = m.group(2), m.group(3).strip()
        if key not in out:
            out[key] = val
    return out


def vec(raw, keys=("x", "y", "z")):
    m = VEC.search(raw or "")
    if not m:
        return None
    parts = {}
    for chunk in m.group(1).split(","):
        if ":" not in chunk:
            continue
        k, v = chunk.split(":", 1)
        try:
            parts[k.strip()] = float(v.strip())
        except ValueError:
            return None
    return tuple(parts.get(k, 0.0) for k in keys)


def fileid(raw):
    m = re.search(r"fileID:\s*(-?\d+)", raw or "")
    return int(m.group(1)) if m else 0


def quat_mul_vec(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    # t = 2 * cross(q.xyz, v)
    tx = 2.0 * (y * vz - z * vy)
    ty = 2.0 * (z * vx - x * vz)
    tz = 2.0 * (x * vy - y * vx)
    return (
        vx + w * tx + (y * tz - z * ty),
        vy + w * ty + (z * tx - x * tz),
        vz + w * tz + (x * ty - y * tx),
    )


def quat_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    )


def main(path):
    docs, cls_of = parse(path)
    fields = {fid: flat(body) for fid, body in docs.items()}

    names = {fid: f.get("m_Name", "") for fid, f in fields.items() if cls_of[fid] == 1}

    tr = {}
    for fid, f in fields.items():
        if cls_of[fid] not in (4, 224):  # Transform, RectTransform
            continue
        tr[fid] = {
            "go": fileid(f.get("m_GameObject")),
            "father": fileid(f.get("m_Father")),
            "pos": vec(f.get("m_LocalPosition")) or (0.0, 0.0, 0.0),
            "rot": vec(f.get("m_LocalRotation"), ("x", "y", "z", "w")) or (0, 0, 0, 1),
            "scl": vec(f.get("m_LocalScale")) or (1.0, 1.0, 1.0),
        }

    cache = {}

    def world(fid):
        if fid in cache:
            return cache[fid]
        t = tr[fid]
        father = t["father"]
        if father == 0 or father not in tr:
            res = (t["pos"], t["rot"], t["scl"])
        else:
            fp, fr, fs = world(father)
            scaled = tuple(t["pos"][i] * fs[i] for i in range(3))
            rotated = quat_mul_vec(fr, scaled)
            res = (
                tuple(fp[i] + rotated[i] for i in range(3)),
                quat_mul(fr, t["rot"]),
                tuple(fs[i] * t["scl"][i] for i in range(3)),
            )
        cache[fid] = res
        return res

    # which transforms carry a renderer, i.e. are actual geometry
    has_mesh = set()
    for fid, f in fields.items():
        if cls_of[fid] in (23, 33):  # MeshRenderer, MeshFilter
            has_mesh.add(fileid(f.get("m_GameObject")))

    rows = []
    for fid, t in tr.items():
        go = t["go"]
        name = names.get(go, "?")
        wp, wr, ws = world(fid)
        rows.append((name, wp, ws, go in has_mesh, fid, go))

    want = sys.argv[2] if len(sys.argv) > 2 else None
    rows.sort(key=lambda r: r[0])
    for name, wp, ws, mesh, fid, go in rows:
        if want and not re.search(want, name, re.I):
            continue
        tag = "mesh" if mesh else "    "
        print(
            f"{tag} {name:34s} pos=({wp[0]:9.2f},{wp[1]:8.2f},{wp[2]:9.2f}) "
            f"scale=({ws[0]:7.2f},{ws[1]:6.2f},{ws[2]:7.2f}) tfm={fid} go={go}"
        )


if __name__ == "__main__":
    main(sys.argv[1])
