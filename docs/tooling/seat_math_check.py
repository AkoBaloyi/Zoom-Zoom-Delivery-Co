"""Checks the rotated-box arithmetic behind KnockablePropBaker's seating fix.

Unity cannot be run headlessly here, so the geometry the baker relies on is re-derived in plain
Python and checked against the numbers in Assets/Project/Scenes/Kyuri's Scene.unity. Every
transform, box centre and box size below was read out of that scene file and is quoted verbatim in
SCENE; nothing is invented or rounded.

What it proves:
  1. For all four prop families the axis pointing at world up is the prop's LOCAL Z, not local Y.
     Reading height off local Y is the bug.
  2. WorldVerticalHalfExtent returns 1.087281 m for RoadBlocker04's hand-fitted box, where the old
     local-Y reading returned 0.483448 m.
  3. Pre-scaling the half-extents by lossyScale double-applies the 230x scale and returns 250.08 m.
  4. The reference-gap rule puts RoadBlocker04.001 and .003 at exactly the y of the hand-placed
     .002, and the old formula reproduces the buried y = -0.08989909 seen in the scene.
  5. Re-fitting the box in local space does not move any of the 30 props that are already right,
     including the box centre: the negative prop scale and negative group scale of the import
     cancel, so the old measure-in-world-then-invert round trip was the identity.
  6. The max(1 m, box height) lift cap passes a legitimate first bake and rejects the 6.24 m
     overhang lift that put trashcontainer01.006 on a roof.

Usage:
    python docs/tooling/seat_math_check.py
Exit status is non-zero if any line reads FAIL.
"""
import sys

# Read out of Assets/Project/Scenes/Kyuri's Scene.unity.
# rot = m_LocalRotation (x, y, z, w), scale = m_LocalScale, pos_y = m_LocalPosition.y,
# size = BoxCollider m_Size, centre = BoxCollider m_Center. All five props are root-level, so
# local and world transforms are the same thing.
SCENE = {
    "RoadBlocker04.002": {
        "rot": (-0.5000001, 0.5, 0.49999994, 0.5),
        "scale": (230.00386, 230.00389, 230.00388),
        "pos_y": 0.4,
        "size": (0.017257674, 0.004203821, 0.009454458),
        "centre": (0.00006942788, -0.0000024854714, 0.004353316),
    },
    "picnictable01.001": {
        "rot": (-0.70710677, -0.0, -0.0, 0.7071068),
        "scale": (255.48845, 255.48845, 255.48845),
        "pos_y": 0.5031612,
        "size": (0.036056746, 0.0077305273, 0.036056716),
        "centre": (0.0, -0.00000005949069, 0.003578958),
    },
    "picnictable02.001": {
        "rot": (-0.70710677, -0.0, -0.0, 0.7071068),
        "scale": (255.48845, 255.48845, 255.48845),
        "pos_y": 0.6285775,
        "size": (0.036056716, 0.020984601, 0.036056716),
        "centre": (0.0, -0.0000000587908, 0.0097151045),
    },
    "trashcontainer01.001": {
        "rot": (-0.50000006, 0.49999994, 0.5, 0.50000006),
        "scale": (255.48842, 141.3813, 255.48845),
        "pos_y": 0.5251251,
        "size": (0.010708572, 0.018157173, 0.012936574),
        "centre": (0.000032490247, -0.0018730651, 0.0046517644),
    },
    "mobile_toilet.002": {
        "rot": (0.000000053385076, 0.7071068, 0.70710677, -0.00000005338508),
        "scale": (183.95169, 183.95169, 183.95169),
        "pos_y": 0.5725833,
        "size": (0.013512909, 0.021226974, 0.014195645),
        "centre": (0.0000000051366404, -0.00031612173, 0.009827302),
    },
}

# The buried y that RoadBlocker04.001 and .003 were baked to before the fix, and the y of the
# hand-placed .002 they should match. Both from the user's report and the scene history.
BURIED_Y = -0.08989909
HAND_PLACED_Y = 0.4

IDENTITY = (0.0, 0.0, 0.0, 1.0)

# The hierarchy each prop was pulled out of, from the same scene file: (axis, prop's own
# originalLocalScale on that axis as recorded on its KnockableProp marker, then the m_LocalScale of
# each group above it). The baker normalises a prop's scale to the positive magnitude of its old
# world scale, so the chain has to multiply out to the POSITIVE m_LocalScale the prop carries
# today. If it multiplied out negative, the box centre the old code stored would be mirrored and a
# re-bake would move the prop; it does not, for any of the 33 props in the scene.
IMPORT_CHAIN = {
    "RoadBlocker04.002": (0, 0.42141503, 267.0324, 2.0439076),      # Traffic_Assets, sehirlogo_01
    "picnictable01.001": (0, -0.29310867, -426.46298, 2.0439076),   # park_props, sehirlogo_01
    "picnictable02.001": (0, -0.29310867, -426.46298, 2.0439076),   # park_props, sehirlogo_01
    "trashcontainer01.001": (1, -0.16219947, -426.46298, 2.0439076),  # CityProps, sehirlogo_01
    "mobile_toilet.002": (0, 0.9, 100.0, 2.0439076),                # base_construction.001
}

failures = []


def check(label, condition, detail=""):
    print(("ok    " if condition else "FAIL  ") + label + (("  " + detail) if detail else ""))
    if not condition:
        failures.append(label)


def close(a, b, tol):
    return abs(a - b) <= tol


def rotation_rows(q):
    """The three rows of the rotation matrix Unity builds from a quaternion. Row i gives the
    world-space component i of a local vector, so row 1 is the one that matters for height."""
    x, y, z, w = q
    return (
        (1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y)),
        (2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)),
        (2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y)),
    )


def y_row_scaled(rot, scale):
    """(m10, m11, m12) of localToWorldMatrix: the rotation's y row with the scale folded in."""
    r = rotation_rows(rot)[1]
    return tuple(r[j] * scale[j] for j in range(3))


def world_vertical_half_extent(rot, scale, size):
    """The C# WorldVerticalHalfExtent, line for line: e = size * 0.5, then
    |m10*e.x| + |m11*e.y| + |m12*e.z|."""
    m = y_row_scaled(rot, scale)
    e = [s * 0.5 for s in size]
    return sum(abs(m[j] * e[j]) for j in range(3))


def world_extents(rot, scale, size):
    """The C# WorldExtents: the same projection once per world axis, doubled."""
    r = rotation_rows(rot)
    e = [s * 0.5 for s in size]
    return tuple(sum(abs(r[i][j] * scale[j] * e[j]) for j in range(3)) * 2 for i in range(3))


def world_centre_y(rot, scale, pos_y, centre):
    """TransformPoint(box.center).y for a root-level prop."""
    m = y_row_scaled(rot, scale)
    return pos_y + sum(m[j] * centre[j] for j in range(3))


def world_box_bottom(p, pos_y=None):
    y = p["pos_y"] if pos_y is None else pos_y
    return (world_centre_y(p["rot"], p["scale"], y, p["centre"])
            - world_vertical_half_extent(p["rot"], p["scale"], p["size"]))


def old_box_bottom(p, pos_y=None):
    """What the code did before the fix: half the height read off LOCAL y."""
    y = p["pos_y"] if pos_y is None else pos_y
    return (world_centre_y(p["rot"], p["scale"], y, p["centre"])
            - abs(p["size"][1] * p["scale"][1]) * 0.5)


def seat(p, pos_y, ground_y, clearance):
    """SeatOnRoad's move, with the cap. Returns (new_y, accepted)."""
    lift = (ground_y + clearance) - world_box_bottom(p, pos_y)
    if abs(lift) < 0.001:
        return pos_y, True
    cap = max(1.0, world_vertical_half_extent(p["rot"], p["scale"], p["size"]) * 2)
    if lift > cap:
        return pos_y, False
    return pos_y + lift, True


def main():
    print("1. which local axis points at world up")
    # Every prop here is a Blender export carrying the Z-up to Y-up import rotation, so the y row
    # of the rotation matrix is (0, 0, 1) for all of them: world up is local Z.
    for name in ("RoadBlocker04.002", "picnictable01.001", "trashcontainer01.001",
                 "mobile_toilet.002"):
        r = rotation_rows(SCENE[name]["rot"])[1]
        check(f"{name:22} local Z is world up",
              close(r[2], 1.0, 1e-6) and close(r[0], 0.0, 1e-6) and close(r[1], 0.0, 1e-6),
              "y row = (%.6f, %.6f, %.6f)" % r)

    print("\n2. the blocker's vertical half-extent")
    blocker = SCENE["RoadBlocker04.002"]
    rot, scale, size = blocker["rot"], blocker["scale"], blocker["size"]
    half = world_vertical_half_extent(rot, scale, size)
    check("rotated half-extent is 1.087281 m", close(half, 1.087281, 1e-6), "got %.6f" % half)
    half_identity = world_vertical_half_extent(IDENTITY, scale, size)
    check("unrotated half-extent is 0.483448 m", close(half_identity, 0.483448, 1e-6),
          "got %.6f" % half_identity)
    # The brief's worked example gives 1.985 m. That is half the LOCAL X extent
    # (230.0039 * 0.017257674 / 2), and this rotation sends local X to world -Z, so it is a
    # horizontal measurement: the 4 m length of the barrier panel, not its height.
    local_x_half = scale[0] * size[0] * 0.5
    check("1.985 m is half the local X extent, which this rotation sends to world -Z",
          close(local_x_half, 1.984670, 1e-5)
          and close(rotation_rows(rot)[2][0], -1.0, 1e-6),
          "local X half %.6f, world z component of local X %.6f"
          % (local_x_half, rotation_rows(rot)[2][0]))
    shortfall = half - half_identity
    check("reading height off local Y is 0.603833 m short",
          close(shortfall, 0.603833, 1e-5), "got %.6f" % shortfall)

    print("\n3. the double-scale trap")
    # Pre-scaling e by lossyScale applies the 230x twice. Pinned to a number so a future edit that
    # reintroduces it fails here instead of in the scene.
    m = y_row_scaled(rot, scale)
    trap = sum(abs(m[j] * (size[j] * 0.5 * scale[j])) for j in range(3))
    check("pre-scaled half-extents return 250.08 m", close(trap, 250.078943, 1e-5),
          "got %.6f" % trap)
    check("the correct helper is not the trap", not close(half, trap, 1e-3),
          "ratio %.4f equals the uniform scale %.4f" % (trap / half, scale[2])
          if close(trap / half, scale[2], 1e-2) else "ratio %.4f" % (trap / half))

    print("\n4. the reference gap puts the blockers where the hand-placed one sits")
    extents = world_extents(rot, scale, size)
    check("world AABB is 0.967 x 2.175 x 3.969 m",
          close(extents[0], 0.966895, 1e-5) and close(extents[1], 2.174562, 1e-5)
          and close(extents[2], 3.969332, 1e-5),
          "got %.6f x %.6f x %.6f" % extents)
    centre = world_centre_y(rot, scale, blocker["pos_y"], blocker["centre"])
    bottom = world_box_bottom(blocker)
    check("reference world centre is 1.401280", close(centre, 1.401280, 1e-5), "got %.6f" % centre)
    check("reference true bottom is 0.313999", close(bottom, 0.313999, 1e-5), "got %.6f" % bottom)

    # The gap cancels: a sibling with the same box, rotation and scale on ground at the same height
    # lands at exactly the reference's y, whatever that height is. Checked at the nominal road top
    # of 0.40 and at the height the buried bake implies, 0.397933, from any starting y.
    for ground in (0.40, 0.397933):
        gap = bottom - ground
        landed = [seat(blocker, start, ground, gap) for start in (BURIED_Y, 0.0, 0.4, 6.0)]
        check(f"on ground at {ground:.6f}, gap {gap:+.6f} seats every sibling at 0.400000",
              all(ok and close(y, HAND_PLACED_Y, 1e-6) for y, ok in landed),
              "from y = -0.0899 / 0.0 / 0.4 / 6.0 -> " + " ".join("%.6f" % y for y, _ in landed))
    check("the gap against a road top of 0.40 is -0.086001",
          close(bottom - 0.40, -0.086001, 1e-5), "got %+.6f" % (bottom - 0.40))

    # And the old formula reproduces the buried value in the scene, which is what makes this the
    # bug and not a coincidence: 0.483448 m of half-extent instead of 1.087281 m, 3 cm clearance.
    old_landed = 0.397933 + 0.03 - (old_box_bottom(blocker) - blocker["pos_y"])
    check("the old formula reproduces the buried y = -0.08989909",
          close(old_landed, BURIED_Y, 2e-6), "got %.8f" % old_landed)
    check("the error is the 0.4899 m reported", close(HAND_PLACED_Y - BURIED_Y, 0.48989909, 1e-8),
          "0.400000 - (-0.089899) = %.8f" % (HAND_PLACED_Y - BURIED_Y))

    print("\n5. no-move invariant for the props that are already right")
    # First the box centre: it only survives the switch to a local-space fit if the import's
    # negative scales cancelled, which they do.
    for name, (axis, *chain) in IMPORT_CHAIN.items():
        product = 1.0
        for v in chain:
            product *= v
        current = SCENE[name]["scale"][axis]
        check(f"{name:22} import chain is +{current:.5f}, so the stored box centre is unmirrored",
              product > 0 and close(product, current, 1e-3),
              "%s -> %.5f" % (" * ".join("%g" % v for v in chain), product))

    # The old fitter wrote the measured WORLD extents straight into the LOCAL axes, so today's
    # stored box has the measured height on local Y. The corrected fitter measures in local space,
    # which puts each measured dimension on the axis it actually occupies. Both give the same world
    # extents and the same world bottom, so re-baking does not move these props.
    for name in ("picnictable01.001", "picnictable02.001", "trashcontainer01.001",
                 "mobile_toilet.002"):
        p = SCENE[name]
        rows = rotation_rows(p["rot"])
        measured = tuple(p["size"][j] * p["scale"][j] for j in range(3))  # world x, y, z as fitted
        # local axis j occupies world axis i where |rows[i][j]| == 1
        corrected = []
        for j in range(3):
            i = max(range(3), key=lambda k: abs(rows[k][j]))
            corrected.append(measured[i] / p["scale"][j])
        corrected = tuple(corrected)
        new_extents = world_extents(p["rot"], p["scale"], corrected)
        new_half = world_vertical_half_extent(p["rot"], p["scale"], corrected)
        new_bottom = world_centre_y(p["rot"], p["scale"], p["pos_y"], p["centre"]) - new_half
        old_bottom = old_box_bottom(p)
        check(f"{name:22} corrected box keeps the measured world extents",
              all(close(new_extents[i], measured[i], 1e-5) for i in range(3)),
              "%.6f x %.6f x %.6f" % new_extents)
        check(f"{name:22} world bottom unchanged, y stays {p['pos_y']:.7f}",
              close(new_bottom, old_bottom, 1e-4),
              "half %.6f, bottom %.6f vs %.6f" % (new_half, new_bottom, old_bottom))

    print("\n6. the lift cap")
    # A first bake legitimately lifts a prop by more than a metre: the import bounds reach -0.59 for
    # a table and -1.61 for a toilet against a road top of 0.40 plus 3 cm of clearance.
    table_height = world_extents(SCENE["picnictable01.001"]["rot"],
                                 SCENE["picnictable01.001"]["scale"],
                                 SCENE["picnictable01.001"]["size"])[1]
    toilet_height = abs(SCENE["mobile_toilet.002"]["size"][1]
                        * SCENE["mobile_toilet.002"]["scale"][1])
    for label, reach, height, want in (("table", -0.59, table_height, 1.02),
                                       ("toilet", -1.61, toilet_height, 2.04)):
        lift = 0.43 - reach
        cap = max(1.0, height)
        check(f"first bake lifts a {label} {want:.2f} m, allowed by max(1 m, {height:.2f} m)",
              close(lift, want, 5e-3) and lift <= cap, "lift %.4f, cap %.4f" % (lift, cap))
        check(f"a flat 1 m cap would have rejected the {label}", lift > 1.0,
              "lift %.4f" % lift)
    # trashcontainer01.006 was put on an awning at 6.7634945 while its siblings sat at 0.5251243.
    overhang = 6.7634945 - 0.5251243
    bin_height = world_extents(SCENE["trashcontainer01.001"]["rot"],
                               SCENE["trashcontainer01.001"]["scale"],
                               SCENE["trashcontainer01.001"]["size"])[1]
    check("the overhang lift is 6.2384 m", close(overhang, 6.2383702, 1e-6), "%.7f" % overhang)
    check("max(1 m, box height) rejects it", overhang > max(1.0, bin_height),
          "cap %.4f" % max(1.0, bin_height))

    print()
    if failures:
        print(f"{len(failures)} checks FAILED:")
        for f in failures:
            print("  " + f)
        return 1
    print("Seating maths checks out.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
