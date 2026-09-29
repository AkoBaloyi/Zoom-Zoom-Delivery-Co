"""Recover the road grid from the greybox pavement, then propose delivery points.

The road tiles alone are too sparse to work from: the current district has five distinct
ones. The pavement is not. A road runs between two parallel kerbs, so clustering the
SideWalk objects on each axis recovers the centreline of every street, including the ones
with no tile under them.

Usage:
    python docs/tooling/scene-tools/find_roads.py "Assets/Project/Scenes/<scene>.unity"

Reads the scene through scene_points, so it needs no Unity and no manual coordinates.
"""
import math
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).parent
ROW = re.compile(
    r"^(mesh|    ) (.+?)\s+pos=\(\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(-?[\d.]+)\)"
)

# Two kerbs closer together than this are the two sides of one street.
MAX_STREET_WIDTH = 30.0
# Kerbs within this of each other on the long axis count as the same kerb line.
KERB_TOLERANCE = 6.0
# Keep a candidate this far from any building pivot.
MIN_BUILDING_CLEARANCE = 18.0
# Keep a candidate this far inside the drivable floor.
FLOOR_MARGIN = 8.0


def read_scene(scene):
    out = subprocess.run(
        [sys.executable, str(HERE / "scene_points.py"), scene],
        capture_output=True, text=True, check=True,
    ).stdout
    rows = []
    for line in out.splitlines():
        m = ROW.match(line)
        if m:
            rows.append((m.group(2).strip(),
                         float(m.group(3)), float(m.group(4)), float(m.group(5))))
    return rows


def cluster(values, tolerance):
    """One-dimensional single-link clustering, returns cluster centres and sizes."""
    out = []
    for v in sorted(values):
        if out and v - out[-1][-1] <= tolerance:
            out[-1].append(v)
        else:
            out.append([v])
    return [(sum(c) / len(c), len(c)) for c in out]


def centrelines(kerbs, tolerance, max_width):
    """Pair adjacent kerb lines into street centrelines."""
    lines = cluster(kerbs, tolerance)
    out = []
    for (a, na), (b, nb) in zip(lines, lines[1:]):
        width = b - a
        if width <= max_width:
            out.append((round((a + b) / 2, 2), round(width, 1), na + nb))
    return out


def main(scene):
    rows = read_scene(scene)
    by_name = {}
    for name, x, y, z in rows:
        by_name.setdefault(name, (x, y, z))

    floor = by_name.get("Floor")
    car = by_name.get("Car")
    if floor is None:
        sys.exit("No Floor object found; cannot tell what is drivable.")

    # Floor is a unit cube scaled, so its footprint is scale/2 either side of centre.
    fx, fy, fz = floor
    scale = next(
        (line for line in subprocess.run(
            [sys.executable, str(HERE / "scene_points.py"), scene, "^Floor$"],
            capture_output=True, text=True, check=True).stdout.splitlines()),
        "",
    )
    sm = re.search(r"scale=\(\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(-?[\d.]+)\)", scale)
    sx, _, sz = (float(sm.group(1)), float(sm.group(2)), float(sm.group(3)))
    bounds = (fx - sx / 2 + FLOOR_MARGIN, fx + sx / 2 - FLOOR_MARGIN,
              fz - sz / 2 + FLOOR_MARGIN, fz + sz / 2 - FLOOR_MARGIN)
    print(f"Floor {sx:.0f} x {sz:.0f} centred ({fx:.0f}, {fz:.0f}); "
          f"usable x {bounds[0]:.0f} to {bounds[1]:.0f}, z {bounds[2]:.0f} to {bounds[3]:.0f}")
    if car:
        print(f"Car starts at ({car[0]:.1f}, {car[2]:.1f})")

    walks = [(n, x, z) for n, (x, y, z) in by_name.items() if n.startswith("SideWalk")]
    buildings = [(x, z) for n, (x, y, z) in by_name.items()
                 if n.startswith(("Cube", "Building"))]
    print(f"{len(walks)} pavement objects, {len(buildings)} building pivots\n")

    # A pavement object lining an east-west street varies in x and shares a z with its row.
    ns = centrelines([x for _, x, _ in walks], KERB_TOLERANCE, MAX_STREET_WIDTH)
    ew = centrelines([z for _, _, z in walks], KERB_TOLERANCE, MAX_STREET_WIDTH)

    print("North-south streets, centreline x (width, kerb objects):")
    for c, w, n in ns:
        print(f"   x = {c:8.2f}   width {w:5.1f} m   from {n} kerbs")
    print("East-west streets, centreline z (width, kerb objects):")
    for c, w, n in ew:
        print(f"   z = {c:8.2f}   width {w:5.1f} m   from {n} kerbs")

    print("\nJunctions, scored by clearance from the nearest building pivot:")
    candidates = []
    for cx, wx, _ in ns:
        for cz, wz, _ in ew:
            if not (bounds[0] <= cx <= bounds[1] and bounds[2] <= cz <= bounds[3]):
                continue
            clear = min((math.dist((cx, cz), b) for b in buildings), default=999.0)
            candidates.append((clear, cx, cz, min(wx, wz)))
    candidates.sort(reverse=True)
    for clear, cx, cz, w in candidates:
        mark = "ok  " if clear >= MIN_BUILDING_CLEARANCE else "TIGHT"
        print(f"   {mark} ({cx:8.2f}, {cz:7.2f})  clearance {clear:6.1f} m  "
              f"narrowest street {w:4.1f} m")

    usable = [c for c in candidates if c[0] >= MIN_BUILDING_CLEARANCE]
    print(f"\n{len(usable)} of {len(candidates)} junctions clear {MIN_BUILDING_CLEARANCE:.0f} m")
    if car:
        print("Distance from the start point to each usable junction:")
        for clear, cx, cz, _ in sorted(
            usable, key=lambda c: abs(c[1] - car[0]) + abs(c[2] - car[2])
        ):
            grid = abs(cx - car[0]) + abs(cz - car[2])
            print(f"   ({cx:8.2f}, {cz:7.2f})  {grid:6.0f} m of grid driving")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1
         else "Assets/Project/Scenes/Possible game scene.unity")
