"""Write the delivery points into a scene and wire them into OrderManager.

Coordinates come from delivery_points.py, which records how they were derived. This script
only does the YAML. Idempotent: it refuses to run if the hierarchy is already there.

Usage:
    python docs/tooling/scene-tools/place_delivery_points.py [scene]
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from delivery_points import DROPOFFS, PICKUPS  # noqa: E402

DEFAULT_SCENE = "Assets/Project/Scenes/Possible game scene.unity"
PARENT_NAME = "Delivery Points"
BASE = 700100001

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


def main(scene=DEFAULT_SCENE):
    text = Path(scene).read_text(encoding="utf-8")

    if PARENT_NAME in text:
        sys.exit(f"'{PARENT_NAME}' is already in {scene}. Nothing to do.")

    points = [(n, x, z) for n, x, z, _ in PICKUPS + DROPOFFS]

    ids = {}
    nxt = BASE
    parent_go, parent_tfm = nxt, nxt + 1
    nxt += 2
    for name, _, _ in points:
        ids[name] = (nxt, nxt + 1)
        nxt += 2

    taken = set(re.findall(r"^--- !u!\d+ &(\d+)", text, re.M))
    clash = [i for pair in ids.values() for i in pair if str(i) in taken]
    clash += [i for i in (parent_go, parent_tfm) if str(i) in taken]
    if clash:
        sys.exit(f"fileID collision with the scene: {clash}. Move BASE.")

    docs = [
        GAMEOBJECT.format(go=parent_go, tfm=parent_tfm, name=PARENT_NAME),
        TRANSFORM.format(
            tfm=parent_tfm, go=parent_go, x=0, y=0, z=0, father=0,
            children="".join(f"\n  - {{fileID: {ids[n][1]}}}" for n, _, _ in points)),
    ]
    for name, x, z in points:
        go, tfm = ids[name]
        docs.append(GAMEOBJECT.format(go=go, tfm=tfm, name=name))
        docs.append(TRANSFORM.format(
            tfm=tfm, go=go, x=x, y=0, z=z, children=" []", father=parent_tfm))

    at = text.index("--- !u!1660057539 &")
    text = text[:at] + "".join(docs) + text[at:]

    text = text.replace("  m_Roots:\n", f"  m_Roots:\n  - {{fileID: {parent_tfm}}}\n", 1)

    block_start = text.index("ZoomZoom.Orders.OrderManager")
    block_end = text.index("--- !u!", block_start)
    block = text[block_start:block_end]
    if "pickupPoints: []" not in block or "dropOffPoints: []" not in block:
        sys.exit("OrderManager's arrays are not both empty. Inspect before rerunning.")

    block = block.replace(
        "pickupPoints: []\n",
        "pickupPoints:\n" + "".join(
            f"  - {{fileID: {ids[n][1]}}}\n" for n, _, _, _ in PICKUPS), 1)
    block = block.replace(
        "dropOffPoints: []\n",
        "dropOffPoints:\n" + "".join(
            f"  - {{fileID: {ids[n][1]}}}\n" for n, _, _, _ in DROPOFFS), 1)
    text = text[:block_start] + block + text[block_end:]

    Path(scene).write_text(text, encoding="utf-8", newline="\n")
    print(f"Wrote {len(PICKUPS)} pickup and {len(DROPOFFS)} drop-off transforms under "
          f"'{PARENT_NAME}' (fileID {parent_tfm}) and wired them into OrderManager.")
    for name, x, z in points:
        print(f"  {name:30s} ({x:8.2f}, 0.00, {z:7.2f})  transform {ids[name][1]}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SCENE)
