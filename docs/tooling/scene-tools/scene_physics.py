"""Count which objects actually own colliders, and dump a component's serialized fields.

WHY THIS EXISTS
corner_geometry.py measures the footprints it can derive from the YAML and honestly names
the rest, but it stops at the top thirty name families and it never says how many objects
in a family carry no collider at all. That is the question a city scene keeps raising: 582
objects are named road_base, so how many of them does the suspension raycast actually hit?
A collider belongs to one GameObject, so a merged mesh is one collider for a whole street
network, and the difference between 582 colliders and 1 decides whether a surface tag can
vary street by street or applies to the entire road at once. Counting owners is the only way
to tell, and nothing else here counted them.

The second mode exists for the same reason: a baked prop records its mass and family on a
marker component, and reading those out of the YAML is the only way to check what the baker
produced without opening the editor.

Read-only. Reuses scene_points for the YAML parse and the hierarchy walk, so the world
coordinates match every other script in this folder.

Usage:
    python docs/tooling/scene-tools/scene_physics.py "<scene>.unity" owners [NAMEREGEX]
    python docs/tooling/scene-tools/scene_physics.py "<scene>.unity" detail NAMEREGEX [--with-colliders]
    python docs/tooling/scene-tools/scene_physics.py "<scene>.unity" script GUID
    python docs/tooling/scene-tools/scene_physics.py "<scene>.unity" extent NAMEREGEX
    python docs/tooling/scene-tools/scene_physics.py "<scene>.unity" reach [Y_CUTOFF]
    python docs/tooling/scene-tools/scene_physics.py "<scene>.unity" jobs [TIME_LIMIT_S]
"""
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

# Keys Unity writes on every MonoBehaviour. Anything else is the script's own field.
BOILERPLATE = {
    "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
    "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script",
    "m_Name", "m_EditorClassIdentifier", "serializedVersion",
}

# trashcontainer01.004 and tree03 are the same family; the suffix is an import index.
FAMILY = re.compile(r"[\s._]*\d+$")


def load(path):
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

    return cls_of, fields, names, tr, tr_of_go, world


def family_of(name):
    return FAMILY.sub("", name or "") or (name or "?")


def path_of(fid, tr, names):
    """Parent chain, root first. Shows which container's scale an object inherits."""
    out = []
    seen = set()
    while fid in tr and fid not in seen:
        seen.add(fid)
        out.append(names.get(tr[fid]["go"], "?"))
        fid = tr[fid]["father"]
    return "/".join(reversed(out))


def colliders_by_go(cls_of, fields):
    out = {}
    for fid, f in fields.items():
        if cls_of[fid] not in COLLIDER_CLASSES:
            continue
        out.setdefault(fileid(f.get("m_GameObject")), []).append({
            "kind": COLLIDER_CLASSES[cls_of[fid]],
            "enabled": f.get("m_Enabled", "?"),
            "convex": f.get("m_Convex", "-"),
            "trigger": f.get("m_IsTrigger", "-"),
            "mesh": f.get("m_Mesh", ""),
            "size": f.get("m_Size", ""),
            "fid": fid,
        })
    return out


def mode_owners(cls_of, fields, names, tr, tr_of_go, world, pattern):
    """Per name family: how many objects exist, how many own a collider, of what kind."""
    rx = re.compile(pattern, re.I) if pattern else None
    cols = colliders_by_go(cls_of, fields)

    stats = {}
    for go, name in names.items():
        if rx and not rx.search(name or ""):
            continue
        fam = family_of(name)
        s = stats.setdefault(fam, {"objects": 0, "owners": 0, "kinds": {},
                                   "disabled": 0, "convex": 0})
        s["objects"] += 1
        for c in cols.get(go, []):
            s["owners"] += 1
            s["kinds"][c["kind"]] = s["kinds"].get(c["kind"], 0) + 1
            if c["enabled"] == "0":
                s["disabled"] += 1
            if c["convex"] == "1":
                s["convex"] += 1

    total_obj = sum(s["objects"] for s in stats.values())
    total_own = sum(s["owners"] for s in stats.values())
    print(f"{len(stats)} name families, {total_obj} GameObjects, "
          f"{total_own} colliders on them")
    print(f"\n{'family':34s} {'objects':>8s} {'colliders':>10s} {'disabled':>9s} "
          f"{'convex':>7s}  kinds")
    for fam, s in sorted(stats.items(), key=lambda kv: (-kv[1]["objects"], kv[0])):
        kinds = ", ".join(f"{k} x{n}" for k, n in sorted(s["kinds"].items())) or "none"
        print(f"{fam[:34]:34s} {s['objects']:8d} {s['owners']:10d} {s['disabled']:9d} "
              f"{s['convex']:7d}  {kinds}")


def mode_detail(cls_of, fields, names, tr, tr_of_go, world, pattern, with_colliders=False):
    rx = re.compile(pattern, re.I)
    cols = colliders_by_go(cls_of, fields)
    rows = [(n, go) for go, n in names.items() if rx.search(n or "")]
    if with_colliders:
        rows = [(n, go) for n, go in rows if cols.get(go)]
    rows.sort()
    print(f"{len(rows)} objects match /{pattern}/"
          + (" and own at least one collider" if with_colliders else ""))
    for name, go in rows:
        tfm = tr_of_go.get(go)
        if tfm is None:
            print(f"   {name:30s} (no transform)")
            continue
        wp, _, ws = world(tfm)
        own = cols.get(go, [])
        print(f"   {name:30s} pos=({wp[0]:9.2f},{wp[1]:8.3f},{wp[2]:9.2f}) "
              f"scale=({ws[0]:9.3f},{ws[1]:9.3f},{ws[2]:9.3f})")
        print(f"      path {path_of(tfm, tr, names)}")
        if not own:
            print("      colliders: none")
        for c in own:
            extra = []
            if c["convex"] != "-":
                extra.append(f"convex={c['convex']}")
            if c["size"]:
                extra.append(f"m_Size={c['size']}")
            if c["mesh"]:
                extra.append(f"mesh={c['mesh']}")
            print(f"      {c['kind']:14s} enabled={c['enabled']} "
                  f"trigger={c['trigger']} {' '.join(extra)}")


def mode_script(cls_of, fields, names, tr, tr_of_go, world, guid):
    rows = []
    for fid, f in fields.items():
        if cls_of[fid] != 114 or guid not in (f.get("m_Script") or ""):
            continue
        go = fileid(f.get("m_GameObject"))
        own = {k: v for k, v in f.items() if k not in BOILERPLATE}
        rows.append((names.get(go, "?"), f.get("m_Enabled", "?"), own, go))
    rows.sort()
    print(f"{len(rows)} MonoBehaviour instances of script guid {guid}")
    for name, enabled, own, go in rows:
        tfm = tr_of_go.get(go)
        wp = world(tfm)[0] if tfm is not None else (0, 0, 0)
        print(f"   {name:30s} enabled={enabled} "
              f"pos=({wp[0]:9.2f},{wp[1]:7.3f},{wp[2]:9.2f})")
        for k, v in own.items():
            print(f"      {k}: {v}")
    if rows:
        tally = {}
        for _, _, own, _ in rows:
            key = tuple(f"{k}={own.get(k)}" for k in sorted(own) if k != "originalParent")
            tally[key] = tally.get(key, 0) + 1
        print("\nidentical field sets, by count:")
        for key, n in sorted(tally.items(), key=lambda kv: -kv[1]):
            print(f"   x{n:4d}  {', '.join(key)}")


def mode_jobs(cls_of, fields, names, tr, tr_of_go, world, limit):
    """Cost all 36 pickup-to-drop-off pairs from the points in THIS scene.

    order_distances.py does the same arithmetic against the hardcoded table in
    delivery_points.py, which was recovered from one scene and does not match the others:
    the twelve objects were hand-moved when the difficulty scenes were split out. This mode
    reads the anchors and the car's start out of the scene file instead, so the figures
    belong to the scene being asked about. The speed constants are imported from
    delivery_points so there is still one source of truth for the assumption, and the
    assumption is exactly that: WORKING_AVERAGE is a number somebody chose, not a lap time.
    """
    from delivery_points import TOP_SPEED, WORKING_AVERAGE  # noqa: PLC0415

    cap = float(limit) if limit else 50.0
    anchor = re.compile(r"^(Pickup|DropOff)\b", re.I)
    pick, drop, start = [], [], None
    for go, name in names.items():
        tfm = tr_of_go.get(go)
        if tfm is None:
            continue
        p = world(tfm)[0]
        if name == "Car":
            start = (p[0], p[2])
        elif anchor.match(name or ""):
            (pick if name.lower().startswith("pickup") else drop).append(
                (name, p[0], p[2]))
    pick.sort()
    drop.sort()
    if start is None:
        print("no object named Car, so the approach leg cannot be costed")
        return
    print(f"car start ({start[0]:.2f}, {start[1]:.2f}); "
          f"{len(pick)} pickups, {len(drop)} drop-offs")
    print(f"speeds imported from delivery_points.py: TOP_SPEED {TOP_SPEED} m/s, "
          f"WORKING_AVERAGE {WORKING_AVERAGE} m/s (an assumption, not a measurement)")

    def gridd(a, b):
        return abs(a[0] - b[0]) + abs(a[1] - b[1])

    rows = []
    for pn, px, pz in pick:
        approach = gridd(start, (px, pz))
        for dn, dx, dz in drop:
            job = gridd((px, pz), (dx, dz))
            rows.append((approach + job, pn, dn, job, approach,
                         math.dist((px, pz), (dx, dz))))
    rows.sort()
    print(f"\n{'pair':52s} {'straight':>9s} {'job':>6s} {'+approach':>10s} "
          f"{'s@' + str(int(TOP_SPEED)):>6s} {'s@' + str(int(WORKING_AVERAGE)):>6s}")
    for total, pn, dn, job, _, straight in rows:
        flag = "" if total / WORKING_AVERAGE <= cap else "  OVER"
        print(f"{pn + ' -> ' + dn:52s} {straight:9.0f} {job:6.0f} {total:10.0f} "
              f"{total / TOP_SPEED:6.1f} {total / WORKING_AVERAGE:6.1f}{flag}")

    metres = [r[0] for r in rows]
    mean = sum(metres) / len(metres)
    ok = sum(1 for m in metres if m / WORKING_AVERAGE <= cap)
    print()
    print(f"shortest job: {rows[0][1]} to {rows[0][2]}, {metres[0]:.0f} m, "
          f"{metres[0] / WORKING_AVERAGE:.0f} s at {WORKING_AVERAGE:.0f} m/s")
    print(f"longest job:  {rows[-1][1]} to {rows[-1][2]}, {metres[-1]:.0f} m, "
          f"{metres[-1] / WORKING_AVERAGE:.0f} s at {WORKING_AVERAGE:.0f} m/s")
    print(f"mean job:     {mean:.0f} m, {mean / WORKING_AVERAGE:.0f} s at "
          f"{WORKING_AVERAGE:.0f} m/s, {mean / TOP_SPEED:.0f} s at top speed")
    print(f"median job:   {metres[len(metres) // 2]:.0f} m, "
          f"{metres[len(metres) // 2] / WORKING_AVERAGE:.0f} s")
    print("\ndistribution of the 36 pairs, metres including the approach leg:")
    for q in (0, 10, 25, 50, 75, 90, 100):
        i = min(len(metres) - 1, int(round(q / 100.0 * (len(metres) - 1))))
        print(f"   p{q:<3d} {metres[i]:7.0f} m   {metres[i] / WORKING_AVERAGE:6.1f} s")
    print("\nseconds at the working average, by band:")
    bands = [(0, 15), (15, 20), (20, 25), (25, 30), (30, 40), (40, 50), (50, 999)]
    for lo, hi in bands:
        n = sum(1 for m in metres if lo <= m / WORKING_AVERAGE < hi)
        print(f"   {lo:3d} to {hi:3d} s: {n:2d}  {'#' * n}")
    print(f"\nAt a {cap:.0f} s limit, {ok} of {len(rows)} pairs are completable at the "
          f"{WORKING_AVERAGE:.0f} m/s working average.")
    for candidate in (45, 50, 60, 75, 90, 105):
        n = sum(1 for m in metres if m / WORKING_AVERAGE <= candidate)
        print(f"  at {candidate:3d} s: {n:2d}/{len(rows)}")


def mode_reach(cls_of, fields, names, tr, tr_of_go, world, threshold):
    """Split every collider by whether its owner stands on the map or under it.

    A city FBX brings in a palette of unplaced copies, and this import parks whole groups
    tens of metres below the ground. A collider down there is real to PhysX and invisible
    to the player, so counting colliders without splitting them overstates by a wide
    margin what the car can actually hit.
    """
    cut = float(threshold) if threshold else -5.0
    cols = colliders_by_go(cls_of, fields)
    above, below = {}, {}
    for go, lst in cols.items():
        tfm = tr_of_go.get(go)
        if tfm is None:
            continue
        y = world(tfm)[0][1]
        bucket = above if y >= cut else below
        fam = family_of(names.get(go, "?"))
        rec = bucket.setdefault(fam, {"colliders": 0, "ymin": y, "ymax": y})
        rec["colliders"] += len(lst)
        rec["ymin"] = min(rec["ymin"], y)
        rec["ymax"] = max(rec["ymax"], y)

    for label, bucket in (("ON the map", above), ("BELOW the map", below)):
        total = sum(r["colliders"] for r in bucket.values())
        print(f"\n{label} (owner pivot y {'>=' if bucket is above else '<'} {cut:g}): "
              f"{total} colliders across {len(bucket)} name families")
        print(f"   {'family':34s} {'colliders':>10s} {'y min':>9s} {'y max':>9s}")
        for fam, r in sorted(bucket.items(), key=lambda kv: -kv[1]["colliders"]):
            print(f"   {fam[:34]:34s} {r['colliders']:10d} {r['ymin']:9.2f} "
                  f"{r['ymax']:9.2f}")


def mode_extent(cls_of, fields, names, tr, tr_of_go, world, pattern):
    """World AABB of the matching pivots, plus the y values they sit at."""
    rx = re.compile(pattern, re.I)
    pts = []
    for go, name in names.items():
        if not rx.search(name or ""):
            continue
        tfm = tr_of_go.get(go)
        if tfm is None:
            continue
        pts.append(world(tfm)[0])
    if not pts:
        print(f"no objects match /{pattern}/")
        return
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    print(f"/{pattern}/: {len(pts)} pivots")
    for i, ax in enumerate("xyz"):
        print(f"   {ax}: {lo[i]:10.2f} to {hi[i]:10.2f}   span {hi[i] - lo[i]:9.2f} m")
    ys = {}
    for p in pts:
        ys[round(p[1], 2)] = ys.get(round(p[1], 2), 0) + 1
    print("   distinct y values (world), by count:")
    for y, n in sorted(ys.items(), key=lambda kv: -kv[1])[:20]:
        print(f"      y={y:9.2f}  x{n}")
    if len(ys) > 20:
        print(f"      ... and {len(ys) - 20} more distinct y values")


def main(argv):
    flags = [a for a in argv if a.startswith("--")]
    argv = [a for a in argv if not a.startswith("--")]
    scene = argv[0]
    mode = argv[1] if len(argv) > 1 else "owners"
    arg = argv[2] if len(argv) > 2 else None
    cls_of, fields, names, tr, tr_of_go, world = load(scene)
    print(f"scene: {scene}\nmode: {mode} {arg or ''} {' '.join(flags)}\n")
    if mode == "owners":
        mode_owners(cls_of, fields, names, tr, tr_of_go, world, arg)
    elif mode == "detail":
        mode_detail(cls_of, fields, names, tr, tr_of_go, world, arg,
                    "--with-colliders" in flags)
    elif mode == "script":
        mode_script(cls_of, fields, names, tr, tr_of_go, world, arg)
    elif mode == "extent":
        mode_extent(cls_of, fields, names, tr, tr_of_go, world, arg)
    elif mode == "reach":
        mode_reach(cls_of, fields, names, tr, tr_of_go, world, arg)
    elif mode == "jobs":
        mode_jobs(cls_of, fields, names, tr, tr_of_go, world, arg)
    else:
        sys.exit(f"unknown mode {mode!r}; expected owners, detail, script, extent, "
                 "reach or jobs")


if __name__ == "__main__":
    main(sys.argv[1:])
