"""Measure what the car can actually hit, and how much room it has at each delivery anchor.

Written for the turning-circle investigation: three testers asked for a better reverse and
said the turns were too wide, and nobody had ever checked the turning circle against the
width of the corners the level makes the player take.

Read-only. Reuses scene_points for the YAML parse and the hierarchy walk, so the world
coordinates here are the same ones every other script in this folder reports.

WHAT IT MEASURES
    1. Every collider in the scene, by type, with a world-space footprint where one can be
       derived from the YAML alone. A BoxCollider's m_Size times the world scale is exact.
       A MeshCollider's bounds live in the mesh asset, not the scene, so those are listed
       and flagged rather than guessed at.
    2. The delivery anchors, in world space.
    3. A free-space grid built from the box footprints, and from it:
         - the clearance radius at each anchor (largest circle of free ground centred on it)
         - the widest-bottleneck route from the car's start to each anchor, which is the
           honest answer to "how tight is the tightest corner on the way in"
    4. The turning radius the tuning asset allows at a range of speeds, so the two can be
       read against each other on the same page.

Usage:
    python docs/tooling/scene-tools/corner_geometry.py "<scene>.unity" [--grid 1.0]

The route search is a max-min Dijkstra on the distance-to-obstacle field: it finds the path
whose narrowest point is as wide as possible. That is the right question for a car, because a
route is only drivable if its tightest point is drivable.
"""
import heapq
import json
import math
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from scene_points import flat, fileid, parse, quat_mul, quat_mul_vec, vec  # noqa: E402

COLLIDER_CLASSES = {
    64: "MeshCollider",
    65: "BoxCollider",
    135: "SphereCollider",
    136: "CapsuleCollider",
    154: "TerrainCollider",
}

ANCHOR = re.compile(r"^(Pickup|DropOff)\b", re.I)

# Half the car, from the tuning asset: wheelSideOffset 0.8 and wheelForwardOffset 1.4.
CAR_HALF_WIDTH = 0.8
CAR_HALF_LENGTH = 1.4


def load(path):
    """Everything downstream needs: names, world transforms, colliders, components."""
    docs, cls_of = parse(path)
    fields = {fid: flat(body) for fid, body in docs.items()}

    names = {fid: f.get("m_Name", "") for fid, f in fields.items() if cls_of[fid] == 1}

    tr = {}
    for fid, f in fields.items():
        if cls_of[fid] not in (4, 224):
            continue
        tr[fid] = {
            "go": fileid(f.get("m_GameObject")),
            "father": fileid(f.get("m_Father")),
            "pos": vec(f.get("m_LocalPosition")) or (0.0, 0.0, 0.0),
            "rot": vec(f.get("m_LocalRotation"), ("x", "y", "z", "w")) or (0, 0, 0, 1),
            "scl": vec(f.get("m_LocalScale")) or (1.0, 1.0, 1.0),
        }

    tr_of_go = {t["go"]: fid for fid, t in tr.items()}

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

    return docs, cls_of, fields, names, tr, tr_of_go, world


def box_corners(centre, size, pos, rot, scl):
    """World-space corners of a BoxCollider. Honest about rotation, unlike an AABB."""
    out = []
    for sx in (-0.5, 0.5):
        for sy in (-0.5, 0.5):
            for sz in (-0.5, 0.5):
                local = (
                    (centre[0] + sx * size[0]) * scl[0],
                    (centre[1] + sy * size[1]) * scl[1],
                    (centre[2] + sz * size[2]) * scl[2],
                )
                r = quat_mul_vec(rot, local)
                out.append((pos[0] + r[0], pos[1] + r[1], pos[2] + r[2]))
    return out


def collect_colliders(cls_of, fields, names, tr, tr_of_go, world, mesh_bounds=None,
                      axis=(0, 2, 1), unit=0.01):
    """Every collider, with a world footprint wherever one can be derived.

    BoxColliders state their own size, so those are exact. MeshColliders need the mesh,
    which is why mesh_bounds exists: pass the map fbx_mesh_bounds.py writes and the same
    footprint maths runs on them. axis and unit convert from the model file's convention
    to Unity's; calibrate() picks them rather than anybody assuming.
    """
    boxes, others = [], []
    mesh_bounds = mesh_bounds or {}
    for fid, f in fields.items():
        cls = cls_of[fid]
        if cls not in COLLIDER_CLASSES:
            continue
        go = fileid(f.get("m_GameObject"))
        tfm = tr_of_go.get(go)
        if tfm is None:
            continue
        wp, wr, ws = world(tfm)
        name = names.get(go, "?")

        size = centre = None
        source = ""
        if cls == 65:
            size = vec(f.get("m_Size")) or (1.0, 1.0, 1.0)
            centre = vec(f.get("m_Center")) or (0.0, 0.0, 0.0)
            source = "box"
        elif cls == 64 and name in mesh_bounds:
            b = mesh_bounds[name]
            lo = [b["min"][a] * unit for a in axis]
            hi = [b["max"][a] * unit for a in axis]
            size = tuple(hi[i] - lo[i] for i in range(3))
            centre = tuple((hi[i] + lo[i]) * 0.5 for i in range(3))
            source = "mesh"

        if size is None:
            others.append({
                "name": name, "kind": COLLIDER_CLASSES[cls], "pos": wp, "scale": ws,
            })
            continue

        corners = box_corners(centre, size, wp, wr, ws)
        boxes.append({
            "name": name,
            "source": source,
            "pos": wp,
            "world_size": (abs(size[0] * ws[0]), abs(size[1] * ws[1]), abs(size[2] * ws[2])),
            "xmin": min(c[0] for c in corners), "xmax": max(c[0] for c in corners),
            "ymin": min(c[1] for c in corners), "ymax": max(c[1] for c in corners),
            "zmin": min(c[2] for c in corners), "zmax": max(c[2] for c in corners),
        })
    return boxes, others


def calibrate(cls_of, fields, names, tr, tr_of_go, world, mesh_bounds):
    """Work out the axis order and unit factor instead of assuming them.

    A model file can be Z-up or Y-up and can be authored in metres, centimetres or
    millimetres, and getting it wrong scales every distance in the report. The test is
    physical rather than documentary: a city rests ON the ground. So try each convention
    and keep whichever puts the bottom of the most objects closest to the floor and
    produces a footprint that fits inside the drivable slab.
    """
    permutations = {
        "xyz (model already Y-up)": (0, 1, 2),
        "xzy (model is Z-up)": (0, 2, 1),
    }
    results = []
    for label, axis in permutations.items():
        for unit in (1.0, 0.01, 0.001):
            boxes, _ = collect_colliders(cls_of, fields, names, tr, tr_of_go, world,
                                         mesh_bounds, axis, unit)
            derived = [b for b in boxes if b["source"] == "mesh"]
            if not derived:
                continue
            sills = sorted(abs(b["ymin"]) for b in derived)
            median_sill = sills[len(sills) // 2]
            span = max(max(b["xmax"] for b in derived) - min(b["xmin"] for b in derived),
                       max(b["zmax"] for b in derived) - min(b["zmin"] for b in derived))
            results.append((median_sill, span, label, axis, unit, len(derived)))
    results.sort()
    return results


def box_corners_note():
    return ("A rotated box is measured from its eight transformed corners, so the figure is "
            "the real axis-aligned footprint rather than the unrotated size.")


def anchors(names, tr, tr_of_go, world):
    out = []
    for go, name in names.items():
        if not ANCHOR.match(name or ""):
            continue
        tfm = tr_of_go.get(go)
        if tfm is None:
            continue
        wp, _, _ = world(tfm)
        out.append((name, wp))
    out.sort()
    return out


def named(names, tr, tr_of_go, world, pattern):
    rx = re.compile(pattern, re.I)
    out = []
    for go, name in names.items():
        if not rx.search(name or ""):
            continue
        tfm = tr_of_go.get(go)
        if tfm is None:
            continue
        wp, _, ws = world(tfm)
        out.append((name, wp, ws))
    out.sort()
    return out


# ----------------------------------------------------------------------
# FREE SPACE
# ----------------------------------------------------------------------

def classify(b, sill, drivable_area):
    """Decide what a footprint is: ground, kerb, obstacle, or backdrop.

    The sill is the height the suspension can climb. Below it the car rides over the
    thing, so counting it as solid would invent corners the car never has to take.

    Backdrop is the category that matters for honesty. One object in the greybox covers
    most of the map once its mesh bounds are applied, which is either a boundary wall or a
    mesh whose bounds do not belong to the transform the scene file pairs them with.
    Either way it is not a corner, so it is excluded AND named, rather than quietly
    dropped or quietly left in to swallow the whole measurement.
    """
    if b["name"] == "Car":
        return "car"
    area = (b["xmax"] - b["xmin"]) * (b["zmax"] - b["zmin"])
    if area > 0.1 * drivable_area:
        return "backdrop"
    if b["ymax"] <= sill:
        return "kerb or ground"
    return "obstacle"


def build_grid(boxes, bounds, step, sill=0.9):
    """Occupancy grid over the XZ plane. True = blocked."""
    x0, x1, z0, z1 = bounds
    drivable_area = (x1 - x0) * (z1 - z0)
    nx = int(math.ceil((x1 - x0) / step)) + 1
    nz = int(math.ceil((z1 - z0) / step)) + 1
    blocked = [[False] * nz for _ in range(nx)]

    used, skipped = [], []
    for b in boxes:
        kind = classify(b, sill, drivable_area)
        if kind != "obstacle":
            skipped.append((kind, b))
            continue
        used.append(b)
        i0 = max(0, int(math.floor((b["xmin"] - x0) / step)))
        i1 = min(nx - 1, int(math.ceil((b["xmax"] - x0) / step)))
        j0 = max(0, int(math.floor((b["zmin"] - z0) / step)))
        j1 = min(nz - 1, int(math.ceil((b["zmax"] - z0) / step)))
        for i in range(i0, i1 + 1):
            row = blocked[i]
            for j in range(j0, j1 + 1):
                row[j] = True
    return blocked, nx, nz, used, skipped


def distance_field(blocked, nx, nz, step):
    """Metres from each free cell to the nearest blocked cell or the grid edge.

    Multi-source BFS on an 8-connected grid, which is within a few per cent of the true
    Euclidean distance and far cheaper than the exact transform. Good enough: the figures
    it feeds are quoted to the nearest tenth of a metre.
    """
    INF = float("inf")
    dist = [[INF] * nz for _ in range(nx)]
    heap = []
    for i in range(nx):
        for j in range(nz):
            if blocked[i][j]:
                dist[i][j] = 0.0
                heap.append((0.0, i, j))
            elif i in (0, nx - 1) or j in (0, nz - 1):
                dist[i][j] = 0.0
                heap.append((0.0, i, j))
    heapq.heapify(heap)
    diag = step * math.sqrt(2.0)
    steps = ((1, 0, step), (-1, 0, step), (0, 1, step), (0, -1, step),
             (1, 1, diag), (1, -1, diag), (-1, 1, diag), (-1, -1, diag))
    while heap:
        d, i, j = heapq.heappop(heap)
        if d > dist[i][j]:
            continue
        for di, dj, w in steps:
            ni, nj = i + di, j + dj
            if ni < 0 or nj < 0 or ni >= nx or nj >= nz:
                continue
            nd = d + w
            if nd < dist[ni][nj]:
                dist[ni][nj] = nd
                heapq.heappush(heap, (nd, ni, nj))
    return dist


def widest_route(dist, nx, nz, start, step):
    """For every cell, the widest bottleneck of the best route to it from start.

    Max-min Dijkstra: the value of a route is the smallest clearance anywhere along it, and
    we maximise that. The answer per cell is therefore "the tightest point on the roomiest
    way in", which is exactly the number a turning circle has to fit through.
    """
    best = [[-1.0] * nz for _ in range(nx)]
    si, sj = start
    if dist[si][sj] <= 0.0:
        return best
    best[si][sj] = dist[si][sj]
    heap = [(-best[si][sj], si, sj)]
    steps = ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1))
    while heap:
        negv, i, j = heapq.heappop(heap)
        v = -negv
        if v < best[i][j]:
            continue
        for di, dj in steps:
            ni, nj = i + di, j + dj
            if ni < 0 or nj < 0 or ni >= nx or nj >= nz:
                continue
            d = dist[ni][nj]
            if d <= 0.0:
                continue
            nv = min(v, d)
            if nv > best[ni][nj]:
                best[ni][nj] = nv
                heapq.heappush(heap, (-nv, ni, nj))
    return best


# ----------------------------------------------------------------------
# WHAT THE CAR CAN DO, FROM THE TUNING ASSET
# ----------------------------------------------------------------------

def steering_table(grip=13.0, od_low=0.6, od_top=1.15, ref=40.0, max_curv=0.35):
    """VehicleTuning.MaxCurvatureAt and GripLimitedRadiusAt, in Python.

    Same formulae, same order of operations, so the table can be checked against the
    lab reading in VehicleLab_Measurements/vehicle_measurements.csv.
    """
    rows = []
    for v in (1, 2, 3, 4, 5, 6, 8, 9.84, 10, 12, 14, 15, 16, 18, 20, 25, 30, 32, 40):
        if v < 0.5:
            steer_r = 1.0 / max_curv
        else:
            od = od_low + (od_top - od_low) * min(1.0, v / ref)
            curv = min((grip * od) / (v * v), max_curv)
            steer_r = 1.0 / curv
        grip_r = (v * v) / grip
        rows.append((v, steer_r, grip_r, max(steer_r, grip_r)))
    return rows


def tile_pitch(rows, axis, tolerance=0.75):
    """The spacing between adjacent tile centres along one axis.

    A modular road is laid on a grid, so the gap between neighbouring tile centres IS the
    tile size. That turns "how wide is this street" into a measurement that needs no mesh
    data at all, which matters because the city scenes are MeshColliders end to end.
    """
    values = sorted(set(round(r[1][axis], 2) for r in rows))
    lanes = []
    for v in values:
        if lanes and v - lanes[-1][-1] <= tolerance:
            lanes[-1].append(v)
        else:
            lanes.append([v])
    centres = [sum(c) / len(c) for c in lanes]
    gaps = [round(b - a, 2) for a, b in zip(centres, centres[1:])]
    return centres, gaps


def report_tiles(names, tr, tr_of_go, world, pattern):
    rows = named(names, tr, tr_of_go, world, pattern)
    if not rows:
        return
    print(f"\nTILE GRID for /{pattern}/: {len(rows)} objects")
    for axis, label in ((0, "x"), (2, "z")):
        centres, gaps = tile_pitch(rows, axis)
        counted = {}
        for g in gaps:
            counted[g] = counted.get(g, 0) + 1
        common = sorted(counted.items(), key=lambda kv: -kv[1])[:8]
        print(f"   {label}: {len(centres)} distinct lines, "
              f"most common spacings {', '.join(f'{g:.2f} m x{n}' for g, n in common)}")
    sizes = {}
    for name, p, s in rows:
        key = (round(s[0], 3), round(s[1], 3), round(s[2], 3))
        sizes[key] = sizes.get(key, 0) + 1
    print("   world scales in use (a tile's world size is its mesh size times this):")
    for key, n in sorted(sizes.items(), key=lambda kv: -kv[1])[:6]:
        print(f"      {key}  x{n}")


def road_mask(rows, bounds, step, pitch):
    """Mark the cells covered by road tiles as drivable. Everything else is off-road.

    A modular road IS the street: each tile is a square of side `pitch` centred on its
    transform, so laying them down recovers the carriageway without touching a mesh. The
    distance field over that mask then gives the half-width of the street at every point,
    which is the number a turning radius has to fit inside.
    """
    x0, x1, z0, z1 = bounds
    nx = int(math.ceil((x1 - x0) / step)) + 1
    nz = int(math.ceil((z1 - z0) / step)) + 1
    free = [[False] * nz for _ in range(nx)]
    half = pitch * 0.5
    for _, p, _ in rows:
        i0 = max(0, int(math.floor((p[0] - half - x0) / step)))
        i1 = min(nx - 1, int(math.ceil((p[0] + half - x0) / step)))
        j0 = max(0, int(math.floor((p[2] - half - z0) / step)))
        j1 = min(nz - 1, int(math.ceil((p[2] + half - z0) / step)))
        for i in range(i0, i1 + 1):
            row = free[i]
            for j in range(j0, j1 + 1):
                row[j] = True
    return free, nx, nz


def report_road_network(names, tr, tr_of_go, world, patterns, pts, start, step, pitch):
    rows = []
    for pattern in patterns:
        rows.extend(named(names, tr, tr_of_go, world, pattern))
    if not rows:
        print("\nNo road tiles matched, so no carriageway measurement is possible.")
        return

    xs = [p[0] for _, p, _ in rows]
    zs = [p[2] for _, p, _ in rows]
    pad = pitch + 4.0
    bounds = (min(xs) - pad, max(xs) + pad, min(zs) - pad, max(zs) + pad)

    free, nx, nz = road_mask(rows, bounds, step, pitch)
    blocked = [[not free[i][j] for j in range(nz)] for i in range(nx)]
    covered = sum(1 for i in range(nx) for j in range(nz) if free[i][j])
    print(f"\nCARRIAGEWAY from {len(rows)} road tiles at a {pitch:.2f} m module, "
          f"grid {nx} x {nz} at {step:.1f} m")
    print(f"   road covers {covered} cells, about {covered * step * step:.0f} m2, "
          f"inside x[{bounds[0]:.0f},{bounds[1]:.0f}] z[{bounds[2]:.0f},{bounds[3]:.0f}]")

    dist = distance_field(blocked, nx, nz, step)

    def cell(p):
        return (max(0, min(nx - 1, int(round((p[0] - bounds[0]) / step)))),
                max(0, min(nz - 1, int(round((p[2] - bounds[2]) / step)))))

    # The car starts off the carriageway in these scenes, so the route search begins at the
    # widest road cell near the start rather than at the start itself.
    si, sj = cell(start)
    bestcell, bestval = None, -1.0
    for i in range(max(0, si - 60), min(nx, si + 61)):
        for j in range(max(0, sj - 60), min(nz, sj + 61)):
            if dist[i][j] > bestval:
                bestval, bestcell = dist[i][j], (i, j)
    print(f"   route search starts from the widest road cell within 60 m of the car "
          f"({bestval:.2f} m half-width)")

    best = widest_route(dist, nx, nz, bestcell, step)

    widths = sorted((dist[i][j], i, j) for i in range(nx) for j in range(nz)
                    if dist[i][j] > 0)
    if widths:
        print(f"   road half-width across the network: min {widths[0][0]:.2f} m, "
              f"median {widths[len(widths) // 2][0]:.2f} m, max {widths[-1][0]:.2f} m")

    print(f"\n   {'anchor':34s} {'on road':>8s} {'half-width':>11s} {'street w':>9s} "
          f"{'bottleneck':>11s} {'route w':>8s}")
    for name, p in pts:
        i, j = cell(p)
        on = "yes" if free[i][j] else "no"
        d = dist[i][j]
        bn = best[i][j]
        bn_s = f"{bn:11.2f}" if bn >= 0 else "unreachable"
        rw_s = f"{bn * 2:8.2f}" if bn >= 0 else "       -"
        print(f"   {name:34s} {on:>8s} {d:11.2f} {d * 2:9.2f} {bn_s} {rw_s}")


def main(scene, step=1.0, bounds_json=None, sill=0.9, tiles=None, roads=None, pitch=8.18):
    docs, cls_of, fields, names, tr, tr_of_go, world = load(scene)

    print(f"scene: {scene}")
    print(f"documents: {len(docs)}  transforms: {len(tr)}  gameobjects: {len(names)}")

    roots = [fid for fid, t in tr.items() if t["father"] == 0 or t["father"] not in tr]
    print(f"\nROOT OBJECTS ({len(roots)}), with the scale every child inherits:")
    for fid in sorted(roots, key=lambda f: names.get(tr[f]["go"], "")):
        t = tr[fid]
        wp, _, ws = world(fid)
        kids = sum(1 for o in tr.values() if o["father"] == fid)
        print(f"   {names.get(t['go'], '?'):36s} pos=({wp[0]:9.3f},{wp[1]:7.3f},{wp[2]:9.3f}) "
              f"scale=({ws[0]:8.5f},{ws[1]:8.5f},{ws[2]:8.5f})  direct children {kids}")

    mesh_bounds = {}
    axis, unit = (0, 2, 1), 0.01
    if bounds_json:
        mesh_bounds = json.loads(Path(bounds_json).read_text(encoding="utf-8"))
        print(f"\nmesh bounds loaded for {len(mesh_bounds)} meshes from {bounds_json}")
        cal = calibrate(cls_of, fields, names, tr, tr_of_go, world, mesh_bounds)
        print("CALIBRATION, scored by how close the bottom of a typical object sits to "
              "the floor (a city rests on the ground):")
        for median_sill, span, label, ax, un, n in cal[:6]:
            print(f"   median |bottom| {median_sill:10.2f} m  widest span {span:10.1f} m  "
                  f"{label}, unit {un}, {n} meshes resolved")
        if cal:
            axis, unit = cal[0][3], cal[0][4]
            print(f"   using {cal[0][2]} with unit {unit}")

    boxes, others = collect_colliders(cls_of, fields, names, tr, tr_of_go, world,
                                      mesh_bounds, axis, unit)
    derived = sum(1 for b in boxes if b["source"] == "mesh")
    print(f"\nCOLLIDERS: {len(boxes) - derived} BoxCollider, {derived} MeshCollider "
          f"resolved from mesh bounds, {len(others)} unresolved")
    kinds = {}
    for o in others:
        kinds[o["kind"]] = kinds.get(o["kind"], 0) + 1
    for k, n in sorted(kinds.items()):
        print(f"   {k:16s} {n}")
    if others:
        print("   (non-box bounds live in the mesh asset, not the scene YAML, so they are "
              "counted but not measured)")
        families = {}
        for o in others:
            key = re.sub(r"[\s._]*\d+$", "", o["name"]) or o["name"]
            families[key] = families.get(key, 0) + 1
        print("   collider owners by name family:")
        for key, n in sorted(families.items(), key=lambda kv: -kv[1])[:30]:
            print(f"      {n:6d}  {key}")
        if len(families) > 30:
            print(f"      ... and {len(families) - 30} more families")

    if boxes:
        print("\nBOX COLLIDER FOOTPRINTS, world metres:")
        for b in sorted(boxes, key=lambda d: -d["world_size"][0] * d["world_size"][2])[:40]:
            print(f"   {b['name']:34s} {b['world_size'][0]:8.2f} x {b['world_size'][1]:7.2f} x "
                  f"{b['world_size'][2]:8.2f}   x[{b['xmin']:9.2f},{b['xmax']:9.2f}] "
                  f"y[{b['ymin']:7.2f},{b['ymax']:7.2f}] z[{b['zmin']:9.2f},{b['zmax']:9.2f}]")
        if len(boxes) > 40:
            print(f"   ... and {len(boxes) - 40} more")

    pts = anchors(names, tr, tr_of_go, world)
    print(f"\nDELIVERY ANCHORS ({len(pts)}):")
    for name, p in pts:
        print(f"   {name:34s} ({p[0]:9.2f}, {p[1]:6.2f}, {p[2]:9.2f})")

    car = named(names, tr, tr_of_go, world, r"^Car$")
    start = car[0][1] if car else (0.0, 0.0, 0.0)
    if car:
        print(f"\nCar start: ({start[0]:.2f}, {start[1]:.2f}, {start[2]:.2f})")

    for pattern in (tiles or []):
        report_tiles(names, tr, tr_of_go, world, pattern)

    if roads:
        report_road_network(names, tr, tr_of_go, world, roads, pts, start, step, pitch)

    print("\nTURNING RADIUS THE TUNING ALLOWS (Balanced: grip 13, overdrive 0.6 to 1.15 "
          "scaled on 40 m/s, curvature cap 0.35):")
    print(f"   {'speed':>6s} {'steering r':>11s} {'grip r':>9s} {'actual r':>9s} "
          f"{'circle':>8s}")
    for v, sr, gr, ar in steering_table():
        print(f"   {v:6.2f} {sr:11.2f} {gr:9.2f} {ar:9.2f} {ar * 2:8.2f}")

    if not boxes:
        print("\nNo box colliders, so no free-space measurement is possible from the YAML "
              "alone. Nothing further to report for this scene.")
        return

    # The analysis region is the drivable slab, not the whole scene. The car can only be
    # where there is ground under the wheel rays, so measuring free space beyond the floor
    # would report corridors the car cannot use.
    floor = next((b for b in boxes if b["name"] == "Floor"), None)
    if floor is not None:
        bounds = (floor["xmin"], floor["xmax"], floor["zmin"], floor["zmax"])
        print(f"\nDrivable slab from the Floor collider: x[{bounds[0]:.1f},{bounds[1]:.1f}] "
              f"z[{bounds[2]:.1f},{bounds[3]:.1f}], "
              f"{bounds[1] - bounds[0]:.0f} x {bounds[3] - bounds[2]:.0f} m")
    else:
        all_pts = [p for _, p in pts] + [start]
        pad = 40.0
        bounds = (
            min(min(b["xmin"] for b in boxes), min(p[0] for p in all_pts)) - pad,
            max(max(b["xmax"] for b in boxes), max(p[0] for p in all_pts)) + pad,
            min(min(b["zmin"] for b in boxes), min(p[2] for p in all_pts)) - pad,
            max(max(b["zmax"] for b in boxes), max(p[2] for p in all_pts)) + pad,
        )
        print(f"\nNo Floor collider; analysing the bounding box of everything found: "
              f"x[{bounds[0]:.1f},{bounds[1]:.1f}] z[{bounds[2]:.1f},{bounds[3]:.1f}]")

    blocked, nx, nz, used, skipped = build_grid(boxes, bounds, step, sill)
    by_kind = {}
    for kind, b in skipped:
        by_kind.setdefault(kind, []).append(b["name"])
    for kind, lst in sorted(by_kind.items()):
        print(f"   excluded as {kind}: {len(lst)}"
              + (f"  -> {', '.join(sorted(lst)[:8])}" if kind in ("backdrop", "car") else ""))
    solid = sum(1 for i in range(nx) for j in range(nz) if blocked[i][j])
    print(f"\nFREE SPACE GRID {nx} x {nz} at {step:.1f} m, bounds x[{bounds[0]:.0f},"
          f"{bounds[1]:.0f}] z[{bounds[2]:.0f},{bounds[3]:.0f}]")
    print(f"   {len(used)} of {len(boxes)} footprints stand above the car's {sill:.1f} m "
          f"sill and are treated as obstacles")
    print(f"   {solid} of {nx * nz} cells blocked ({100.0 * solid / (nx * nz):.1f}%)")

    dist = distance_field(blocked, nx, nz, step)

    def cell(p):
        return (max(0, min(nx - 1, int(round((p[0] - bounds[0]) / step)))),
                max(0, min(nz - 1, int(round((p[2] - bounds[2]) / step)))))

    si, sj = cell(start)
    best = widest_route(dist, nx, nz, (si, sj), step)

    print("\nPER-ANCHOR CLEARANCE AND APPROACH BOTTLENECK")
    print("   clearance      largest free circle centred on the anchor, radius in metres")
    print("   corridor       twice that, i.e. the free width across the anchor")
    print("   bottleneck     tightest half-width on the roomiest route in from the car start")
    print(f"\n   {'anchor':34s} {'clearance':>10s} {'corridor':>9s} {'bottleneck':>11s} "
          f"{'route width':>12s}")
    for name, p in pts:
        i, j = cell(p)
        clear = dist[i][j]
        bn = best[i][j]
        bn_s = f"{bn:11.2f}" if bn >= 0 else "  unreachable"
        wid_s = f"{bn * 2:12.2f}" if bn >= 0 else "            -"
        print(f"   {name:34s} {clear:10.2f} {clear * 2:9.2f} {bn_s} {wid_s}")

    print(f"\n   car half-width {CAR_HALF_WIDTH:.1f} m, half-length {CAR_HALF_LENGTH:.1f} m, "
          f"so a corridor under {2 * CAR_HALF_WIDTH:.1f} m is impassable at any speed")


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    grid, bounds_file, sill_arg = 1.0, None, 0.9
    tile_patterns, road_patterns, road_pitch = [], [], 8.18
    for a in sys.argv[1:]:
        if a.startswith("--grid="):
            grid = float(a.split("=", 1)[1])
        elif a.startswith("--bounds="):
            bounds_file = a.split("=", 1)[1]
        elif a.startswith("--sill="):
            sill_arg = float(a.split("=", 1)[1])
        elif a.startswith("--tiles="):
            tile_patterns.append(a.split("=", 1)[1])
        elif a.startswith("--road="):
            road_patterns.append(a.split("=", 1)[1])
        elif a.startswith("--pitch="):
            road_pitch = float(a.split("=", 1)[1])
    main(args[0] if args else "Assets/Project/Scenes/Possible game scene.unity",
         grid, bounds_file, sill_arg, tile_patterns, road_patterns, road_pitch)
