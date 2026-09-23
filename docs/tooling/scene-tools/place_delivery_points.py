"""Insert real pickup and drop-off point GameObjects into the game scene and wire them
into OrderManager, replacing the 60 m placeholder ring.

Positions are the world positions of the greybox road tiles, cross-checked against the
kerb (SideWalk) positions either side of each centreline. Run scene_points.py to re-derive.

Idempotent: refuses to run twice.
"""
import math
import re
import sys

SCENE = "Assets/Project/Scenes/Possible game scene.unity"
ORDER_MANAGER_FILEID = "816895044"
BASE = 700100001

# name, x, z, source road tile in the greybox
PICKUPS = [
    ("Pickup 01 West Main",   -95.37, 159.57, "Plane"),
    ("Pickup 02 Centre Cross", -1.61, 159.82, "Plane.006"),
    ("Pickup 03 East Mid",     98.95, 265.78, "Plane.007"),
    ("Pickup 04 North West", -205.03, 371.76, "Plane.013"),
    ("Pickup 05 North East",  208.96, 372.00, "Plane.010"),
    ("Pickup 06 South Gate",   98.98,  53.61, "Plane.009 (1)"),
]

DROPOFFS = [
    ("DropOff 01 West Mid",     -95.26, 265.93, "Plane.008"),
    ("DropOff 02 North Centre",  -1.81, 370.69, "Plane.011"),
    ("DropOff 03 North East",   108.00, 364.70, "Plane.003"),
    ("DropOff 04 West Gate",   -205.20, 159.82, "Plane.005"),
    ("DropOff 05 East Gate",    209.34, 158.31, "Plane.009"),
    ("DropOff 06 North Inner",  -95.90, 364.70, "Plane.004"),
]

PARENT_NAME = "Delivery Points"

GAMEOBJECT = """--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tfm}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""

TRANSFORM = """--- !u!4 &{tfm}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: -0, y: -0, z: -0, w: 1}}
  m_LocalPosition: {{x: {x}, y: {y}, z: {z}}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{children}
  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""

# Building pivots read out of the same scene, used only as a sanity check.
BUILDINGS = [
    (-115.40, 184.93), (-78.09, 232.17), (64.32, 200.02), (73.46, 300.49),
    (-76.12, 179.78), (-111.18, 115.05), (-159.83, 186.14), (-74.73, 139.16),
    (-69.08, 103.77), (-146.57, 247.77), (15.43, 235.66), (29.48, 115.30),
    (133.99, 186.68), (200.23, 237.72), (154.71, 80.88), (-30.43, 292.57),
    (-125.01, 287.64), (-167.96, 292.57), (-167.96, 342.66), (-65.00, 347.99),
    (30.80, 342.66), (132.25, 287.90), (144.06, 350.05), (186.16, 315.65),
    (-30.43, 223.43), (-172.70, 114.51), (27.28, 388.20),
]

# Floor box: centre (0, -1, 180), scale (504, 2, 440)
FLOOR = (-252.0, 252.0, -40.0, 400.0)


def main():
    with open(SCENE, "r", encoding="utf-8") as fh:
        text = fh.read()

    if PARENT_NAME in text:
        sys.exit(f"'{PARENT_NAME}' already present in the scene. Nothing to do.")

    ok = True
    for name, x, z, src in PICKUPS + DROPOFFS:
        if not (FLOOR[0] <= x <= FLOOR[1] and FLOOR[2] <= z <= FLOOR[3]):
            print(f"  FAIL  {name} is outside the Floor collider")
            ok = False
        near = min(math.dist((x, z), b) for b in BUILDINGS)
        flag = "ok  " if near >= 15.0 else "TIGHT"
        print(f"  {flag}  {name:26s} ({x:8.2f}, {z:7.2f})  from {src:14s} "
              f"nearest building pivot {near:6.1f} m")
    if not ok:
        sys.exit("Aborted: a point falls off the drivable floor.")

    ids = {}
    nxt = BASE
    parent_go, parent_tfm = nxt, nxt + 1
    nxt += 2
    for name, _, _, _ in PICKUPS + DROPOFFS:
        ids[name] = (nxt, nxt + 1)
        nxt += 2

    docs = []
    child_refs = "".join(
        f"\n  - {{fileID: {ids[n][1]}}}" for n, _, _, _ in PICKUPS + DROPOFFS
    )
    docs.append(GAMEOBJECT.format(go=parent_go, tfm=parent_tfm, name=PARENT_NAME))
    docs.append(TRANSFORM.format(
        tfm=parent_tfm, go=parent_go, x=0, y=0, z=0,
        children=child_refs, father=0))

    for name, x, z, _ in PICKUPS + DROPOFFS:
        go, tfm = ids[name]
        docs.append(GAMEOBJECT.format(go=go, tfm=tfm, name=name))
        docs.append(TRANSFORM.format(
            tfm=tfm, go=go, x=x, y=0, z=z, children=" []", father=parent_tfm))

    new_docs = "".join(docs)

    # 1. insert the new documents immediately before SceneRoots
    marker = "--- !u!1660057539 &"
    at = text.index(marker)
    text = text[:at] + new_docs + text[at:]

    # 2. register the parent as a scene root
    text = text.replace(
        "  m_Roots:\n",
        f"  m_Roots:\n  - {{fileID: {parent_tfm}}}\n",
        1,
    )

    # 3. wire the arrays on OrderManager
    block_start = text.index(f"--- !u!114 &{ORDER_MANAGER_FILEID}")
    block_end = text.index("--- !u!", block_start + 10)
    block = text[block_start:block_end]

    if "pickupPoints: []" not in block or "dropOffPoints: []" not in block:
        sys.exit("OrderManager arrays are not both empty. Inspect before rerunning.")

    pickup_yaml = "pickupPoints:\n" + "".join(
        f"  - {{fileID: {ids[n][1]}}}\n" for n, _, _, _ in PICKUPS
    )
    drop_yaml = "dropOffPoints:\n" + "".join(
        f"  - {{fileID: {ids[n][1]}}}\n" for n, _, _, _ in DROPOFFS
    )

    block = block.replace("pickupPoints: []\n", pickup_yaml, 1)
    block = block.replace("dropOffPoints: []\n", drop_yaml, 1)
    text = text[:block_start] + block + text[block_end:]

    with open(SCENE, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)

    print(f"\nWrote {len(PICKUPS)} pickup and {len(DROPOFFS)} drop-off transforms under "
          f"'{PARENT_NAME}' (fileID {parent_tfm}) and wired them into OrderManager.")


if __name__ == "__main__":
    main()
