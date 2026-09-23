"""Work out what order time limit the real district needs.

The 45 s limit in OrderTuning_Balanced was set against the placeholder 60 m ring, where the
worst case was a 120 m hop. With real points the worst case is much longer, and the timer runs
from spawn, not from pickup, so the drive TO the pickup is inside the budget too.
"""
import math

SPAWN = (0.0, 0.0)  # Car transform in the game scene

PICKUPS = {
    "P1 West Main": (-95.37, 159.57),
    "P2 Centre Cross": (-1.61, 159.82),
    "P3 East Mid": (98.95, 265.78),
    "P4 North West": (-205.03, 371.76),
    "P5 North East": (208.96, 372.00),
    "P6 South Gate": (98.98, 53.61),
}
DROPOFFS = {
    "D1 West Mid": (-95.26, 265.93),
    "D2 North Centre": (-1.81, 370.69),
    "D3 North East": (108.00, 364.70),
    "D4 West Gate": (-205.20, 159.82),
    "D5 East Gate": (209.34, 158.31),
    "D6 North Inner": (-95.90, 364.70),
}

TOP_SPEED = 32.0     # m/s, measured, Balanced profile
AVG_SPEED = 20.0     # m/s, conservative average on a junction-heavy grid


def manhattan(a, b):
    """Roads run north-south and east-west, so grid distance is the honest lower bound."""
    return abs(a[0] - b[0]) + abs(a[1] - b[1])


def report():
    print(f"{'pair':34s} {'straight':>9s} {'grid':>8s} {'+approach':>10s} "
          f"{'s @32':>7s} {'s @20':>7s}")
    rows = []
    for pn, p in PICKUPS.items():
        approach = manhattan(SPAWN, p)
        for dn, d in DROPOFFS.items():
            straight = math.dist(p, d)
            grid = manhattan(p, d)
            total = approach + grid
            rows.append((total, pn, dn, straight, grid, total))

    rows.sort()
    for _, pn, dn, straight, grid, total in rows:
        print(f"{pn + ' -> ' + dn:34s} {straight:9.0f} {grid:8.0f} {total:10.0f} "
              f"{total / TOP_SPEED:7.1f} {total / AVG_SPEED:7.1f}")

    worst = rows[-1]
    best = rows[0]
    print()
    print(f"approach to nearest pickup: {min(manhattan(SPAWN, p) for p in PICKUPS.values()):.0f} m")
    print(f"approach to furthest pickup: {max(manhattan(SPAWN, p) for p in PICKUPS.values()):.0f} m")
    print(f"shortest job: {best[1]} -> {best[2]}, {best[5]:.0f} m grid incl. approach, "
          f"{best[5] / AVG_SPEED:.0f} s at {AVG_SPEED:.0f} m/s")
    print(f"longest job:  {worst[1]} -> {worst[2]}, {worst[5]:.0f} m grid incl. approach, "
          f"{worst[5] / AVG_SPEED:.0f} s at {AVG_SPEED:.0f} m/s")
    print()
    mean = sum(r[0] for r in rows) / len(rows)
    print(f"mean job length incl. approach: {mean:.0f} m -> {mean / AVG_SPEED:.0f} s at "
          f"{AVG_SPEED:.0f} m/s, {mean / TOP_SPEED:.0f} s at top speed")
    print(f"orders solvable inside 45 s at {AVG_SPEED:.0f} m/s: "
          f"{sum(1 for r in rows if r[0] / AVG_SPEED <= 45)}/{len(rows)}")
    for limit in (60, 75, 90, 105, 120):
        n = sum(1 for r in rows if r[0] / AVG_SPEED <= limit)
        print(f"  at {limit:3d} s: {n:2d}/{len(rows)} solvable")


if __name__ == "__main__":
    report()
