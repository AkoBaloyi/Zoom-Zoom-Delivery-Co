"""Check the order time limit against the delivery points that are actually in the scene.

Order starts its countdown at spawn, not at pickup, so the drive to the pickup is inside
the same budget and has to be costed with the job.

Usage:
    python docs/tooling/scene-tools/order_distances.py [time_limit_seconds]
"""
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from delivery_points import (  # noqa: E402
    DROPOFFS, PICKUPS, START, TOP_SPEED, WORKING_AVERAGE,
)


def grid(a, b):
    """Streets run north-south and east-west, so grid distance is the honest cost."""
    return abs(a[0] - b[0]) + abs(a[1] - b[1])


def main(limit=75.0):
    rows = []
    for pn, px, pz, _ in PICKUPS:
        approach = grid(START, (px, pz))
        for dn, dx, dz, _ in DROPOFFS:
            job = grid((px, pz), (dx, dz))
            straight = math.dist((px, pz), (dx, dz))
            rows.append((approach + job, pn, dn, straight, job, approach))

    rows.sort()
    print(f"{'pair':52s} {'straight':>9s} {'job':>6s} {'+approach':>10s} "
          f"{'s@32':>6s} {'s@20':>6s}")
    for total, pn, dn, straight, job, _ in rows:
        flag = "" if total / WORKING_AVERAGE <= limit else "  OVER"
        print(f"{pn + ' -> ' + dn:52s} {straight:9.0f} {job:6.0f} {total:10.0f} "
              f"{total / TOP_SPEED:6.1f} {total / WORKING_AVERAGE:6.1f}{flag}")

    worst, best = rows[-1], rows[0]
    mean = sum(r[0] for r in rows) / len(rows)
    solvable = sum(1 for r in rows if r[0] / WORKING_AVERAGE <= limit)
    print()
    print(f"approach to the nearest pickup:  "
          f"{min(grid(START, (p[1], p[2])) for p in PICKUPS):.0f} m")
    print(f"approach to the furthest pickup: "
          f"{max(grid(START, (p[1], p[2])) for p in PICKUPS):.0f} m")
    print(f"shortest job: {best[1]} to {best[2]}, {best[0]:.0f} m, "
          f"{best[0] / WORKING_AVERAGE:.0f} s at {WORKING_AVERAGE:.0f} m/s")
    print(f"longest job:  {worst[1]} to {worst[2]}, {worst[0]:.0f} m, "
          f"{worst[0] / WORKING_AVERAGE:.0f} s at {WORKING_AVERAGE:.0f} m/s")
    print(f"mean job:     {mean:.0f} m, {mean / WORKING_AVERAGE:.0f} s at "
          f"{WORKING_AVERAGE:.0f} m/s, {mean / TOP_SPEED:.0f} s at top speed")
    print()
    print(f"At a {limit:.0f} s limit, {solvable} of {len(rows)} pairs are completable "
          f"at the {WORKING_AVERAGE:.0f} m/s working average.")
    if solvable < len(rows):
        need = max(r[0] for r in rows) / WORKING_AVERAGE
        print(f"The worst pair needs {need:.0f} s. Raise the limit or move a point.")
    for candidate in (45, 60, 75, 90, 105):
        n = sum(1 for r in rows if r[0] / WORKING_AVERAGE <= candidate)
        print(f"  at {candidate:3d} s: {n:2d}/{len(rows)}")


if __name__ == "__main__":
    main(float(sys.argv[1]) if len(sys.argv) > 1 else 75.0)
