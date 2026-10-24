"""Read local-space mesh bounds straight out of a binary FBX.

WHY THIS EXISTS
A BoxCollider states its size in the scene file, so scene_points can measure it. A
MeshCollider does not: it points at a mesh, and the mesh lives in the model asset. The
greybox district is 90 MeshColliders on one FBX, so without this there is no way to say how
wide a street is without opening Unity, and the whole point of this folder is not having to.

Read-only. Prints one row per mesh, and `--json` emits a map the scene tools can load.

Usage:
    python docs/tooling/scene-tools/fbx_mesh_bounds.py "<model>.fbx" [--json out.json]

WHAT IT DOES NOT DO
It reports the bounds of the mesh as the FBX stores them, in FBX units. Unity's importer
may rescale on import (Model Importer "Scale Factor" and "Convert Units"). The scale that
matters is checked by comparing against something already known in world space: the greybox
floor is 504 m across and the buildings have to fit on it.
"""
import json
import struct
import sys
import zlib
from pathlib import Path

HEADER = b"Kaydara FBX Binary  \x00"

SCALARS = {"Y": ("<h", 2), "C": ("<?", 1), "I": ("<i", 4), "F": ("<f", 4),
           "D": ("<d", 8), "L": ("<q", 8)}
ARRAYS = {"f": ("f", 4), "d": ("d", 8), "l": ("q", 8), "i": ("i", 4), "b": ("?", 1)}


class Reader:
    def __init__(self, data):
        self.d = data
        self.p = 0

    def u8(self):
        v = self.d[self.p]
        self.p += 1
        return v

    def u32(self):
        v = struct.unpack_from("<I", self.d, self.p)[0]
        self.p += 4
        return v

    def u64(self):
        v = struct.unpack_from("<Q", self.d, self.p)[0]
        self.p += 8
        return v

    def raw(self, n):
        v = self.d[self.p:self.p + n]
        self.p += n
        return v


def read_property(r, want_values):
    """One property. Big arrays are skipped unless the caller actually wants them."""
    code = chr(r.u8())

    if code in SCALARS:
        fmt, size = SCALARS[code]
        v = struct.unpack_from(fmt, r.d, r.p)[0]
        r.p += size
        return v

    if code in ("S", "R"):
        n = r.u32()
        return r.raw(n)

    if code in ARRAYS:
        fmt, size = ARRAYS[code]
        length = r.u32()
        encoding = r.u32()
        comp_len = r.u32()
        payload = r.raw(comp_len)
        if not want_values:
            return None
        if encoding == 1:
            payload = zlib.decompress(payload)
        return struct.unpack_from("<" + fmt * length, payload, 0)

    raise ValueError(f"unknown FBX property code {code!r} at {r.p}")


def walk(r, end, wide, path, out):
    """Depth-first over the record tree, collecting only what the bounds need."""
    null_len = 25 if wide else 13

    while r.p < end:
        if end - r.p < null_len:
            break
        start = r.p
        end_offset = r.u64() if wide else r.u32()
        num_props = r.u64() if wide else r.u32()
        prop_len = r.u64() if wide else r.u32()
        name_len = r.u8()
        name = r.raw(name_len).decode("utf-8", "replace")

        # A record with EndOffset 0 is the null terminator that closes a sibling list. At the
        # top level the file footer sits straight after it, and the footer is not a record, so
        # the only safe move is to stop reading here.
        if end_offset == 0:
            r.p = start + null_len
            return

        here = path + [name]
        prop_start = r.p

        # Geometry: id, name, class. Vertices: the doubles we are here for.
        want = name == "Vertices" and len(path) >= 2 and path[-1] == "Geometry"
        props = []
        for _ in range(num_props):
            props.append(read_property(r, want))
        r.p = prop_start + prop_len

        if name == "Geometry" and len(props) >= 2:
            raw_name = props[1]
            label = raw_name.split(b"\x00")[0].decode("utf-8", "replace") \
                if isinstance(raw_name, bytes) else str(raw_name)
            out["current"] = {"id": props[0], "name": label}
            out["geometries"].append(out["current"])

        if name == "Model" and len(props) >= 2:
            raw_name = props[1]
            label = raw_name.split(b"\x00")[0].decode("utf-8", "replace") \
                if isinstance(raw_name, bytes) else str(raw_name)
            out["models"].append({"id": props[0], "name": label})

        if name == "C" and len(props) >= 3:
            out["connections"].append((props[1], props[2]))

        if want and props and props[0] is not None:
            verts = props[0]
            xs = verts[0::3]
            ys = verts[1::3]
            zs = verts[2::3]
            if xs and out["current"] is not None:
                out["current"]["bounds"] = (
                    min(xs), min(ys), min(zs), max(xs), max(ys), max(zs))
                out["current"]["vertices"] = len(xs)

        if r.p < end_offset:
            walk(r, end_offset, wide, here, out)
        r.p = end_offset


def read_fbx(path):
    data = Path(path).read_bytes()
    if not data.startswith(HEADER):
        raise SystemExit(f"{path} is not a binary FBX")
    version = struct.unpack_from("<I", data, 23)[0]
    wide = version >= 7500

    r = Reader(data)
    r.p = 27
    out = {"geometries": [], "models": [], "connections": [], "current": None}
    walk(r, len(data), wide, [], out)
    out.pop("current", None)
    return version, out


def main(path, json_out=None):
    version, out = read_fbx(path)
    geoms = [g for g in out["geometries"] if "bounds" in g]
    print(f"{path}")
    print(f"FBX version {version}, {len(out['geometries'])} geometries "
          f"({len(geoms)} with vertex data), {len(out['models'])} models, "
          f"{len(out['connections'])} connections")

    # Geometry is connected to the Model that uses it, and the Model name is what the scene
    # file shows. Resolving through the connection table is the only safe mapping: Blender
    # names mesh data and object independently.
    model_by_id = {m["id"]: m["name"] for m in out["models"]}
    model_for_geom = {}
    for child, parent in out["connections"]:
        if parent in model_by_id:
            model_for_geom.setdefault(child, model_by_id[parent])

    rows = []
    for g in geoms:
        x0, y0, z0, x1, y1, z1 = g["bounds"]
        rows.append({
            "geometry": g["name"],
            "model": model_for_geom.get(g["id"], ""),
            "vertices": g["vertices"],
            "size": (x1 - x0, y1 - y0, z1 - z0),
            "min": (x0, y0, z0),
            "max": (x1, y1, z1),
        })
    rows.sort(key=lambda d: -(d["size"][0] * d["size"][2]))

    print(f"\n{'model':30s} {'geometry':30s} {'verts':>8s} "
          f"{'size x':>11s} {'size y':>11s} {'size z':>11s}")
    for d in rows:
        print(f"{d['model'][:30]:30s} {d['geometry'][:30]:30s} {d['vertices']:8d} "
              f"{d['size'][0]:11.4f} {d['size'][1]:11.4f} {d['size'][2]:11.4f}")

    if json_out:
        by_model = {}
        for d in rows:
            key = d["model"] or d["geometry"]
            by_model[key] = {"min": d["min"], "max": d["max"], "size": d["size"]}
        Path(json_out).write_text(json.dumps(by_model, indent=1), encoding="utf-8")
        print(f"\nwrote {json_out} with {len(by_model)} entries")


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    out = None
    for a in sys.argv[1:]:
        if a.startswith("--json="):
            out = a.split("=", 1)[1]
    main(args[0], out)
