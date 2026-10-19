# Group 2 — Map & environment (INV-B)

Investigation notes for `docs/DESIGN-OVERVIEW.md`, question group 2. Covers plan items 8, 9 and 10.

Read-only investigation. No game code, scene, prefab, asset, tuning value, `ProjectSettings/` or git
state was changed. Unity was not opened.

---

## How to read this

| Label | Means |
| --- | --- |
| `READ` | Literal value in a file, cited `path:line` or `path` + YAML anchor |
| `MEASURED` | Produced by a tooling run; the exact command is given |
| `DERIVED` | Arithmetic on `READ`/`MEASURED` inputs, inputs and operation stated |
| `INFERRED` | Read of code or data structure, no runtime evidence |
| `UNKNOWN` | Not establishable without running the game or opening the editor; the single settling check is named |

**Authority.** Per plan §0.2, `origin/main` is the reference tree. `<ref>` below means
`.agents/tasks/design-overview-2026-10-14/ref/`, which holds byte-identical blobs extracted from
`origin/main` by `.agents/tasks/design-overview-2026-10-14/tools/extract_ref.py`. Two blobs were
missing from `<ref>` when this item started and were extracted the same way:
`Possible game scene.unity` and `Border.prefab`. Everything under `Assets/Project/Scripts/` and
`docs/tooling/` is identical between `HEAD` and `origin/main` and is read in place.

**One new tool.** `docs/tooling/scene-tools/scene_physics.py` was written for this item because
nothing existing answers "how many objects in this family own a collider" or "cost these pairs using
*this* scene's points". It is read-only and reuses `scene_points.py` for the YAML parse and the
hierarchy walk, so its world coordinates match the rest of the folder. Modes: `owners`, `detail`,
`script`, `extent`, `reach`, `jobs`. Its `jobs` mode was validated against the existing
`order_distances.py` baseline and reproduces it exactly (see §6.1).

**What the tools cannot see, stated up front.**

1. **Mesh extents are not in a scene file.** A `BoxCollider` states `m_Size`, so it can be measured
   from the YAML. A `MeshCollider` points at a mesh in a model asset, so its footprint has to come
   from `fbx_mesh_bounds.py`. Where that was done it is said so; where it was not, the figure is
   `UNKNOWN`, not estimated.
2. **Prefab instance contents are invisible.** `scene_points.py`, `corner_geometry.py` and
   `scene_physics.py` parse only the documents in the scene file. A prefab instance contributes one
   `PrefabInstance` document plus overrides, so the colliders inside it are not counted. This matters
   exactly once, for `Border.prefab` (§5.4), and is handled explicitly there.
3. **Pivots are not geometry.** `extent` reports the bounding box of transform *pivots*. A pivot at
   y = 10 does not mean the surface is at y = 10.

---

## 0. Scene inventory

`MEASURED` — `git ls-tree -r --name-only origin/main -- Assets/Project/Scenes`

Four scenes carry a drivable map and the twelve delivery anchors. All four were examined.

| Scene | Documents | GameObjects | MeshColliders | BoxColliders | Rigidbodies |
| --- | --- | --- | --- | --- | --- |
| `Hard.unity` | 9393 | 1986 | 1295 | 35 | 34 |
| `Medium.unity` | 8262 | 1982 | 397 | 2 | 1 |
| `Easy.unity` | 1019 | 219 | 168 | 1 | 1 |
| `Possible game scene.unity` | 150 | 40 | 0 | 2 | 1 |

`MEASURED` — document counts from `corner_geometry.py` headers; component counts by counting
`--- !u!64 &` / `--- !u!65 &` / `--- !u!54 &` document headers in each `<ref>` blob.

**The scene the telemetry came from no longer contains a city.** On `origin/main`,
`Possible game scene.unity` is 40 GameObjects: a `Floor`, a `Car`, the twelve delivery points, the
order/cargo/HUD systems and a `Buttons` object. Zero MeshColliders. `HEAD`'s copy of the same file
still has 136 GameObjects and 90 MeshColliders (`MEASURED` — `git show HEAD:"Assets/Project/Scenes/Possible game scene.unity"`
piped through a document-header count), and `git diff --stat HEAD origin/main` reports
10,910 changed / 10,925 deleted lines for that path. So the greybox district was **removed** from
that scene between `HEAD` and `origin/main` (`DERIVED` from those two counts plus the diffstat).

Consequence for every later section: the five logged shifts name `Possible game scene`, and on the
authoritative tree that scene is a bare 504 × 555 m plane. Any statement about "the city the testers
drove" is about a tree that is not `origin/main`.

---

## 1. Extent and layout, per scene

### 1.1 Hard.unity

```
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Hard.unity" --grid=2.0 --tiles=^road_
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" extent "^road_"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" detail "^(Roads_Parent|Floor|CityProps|City)$"
```

- **16 root objects.** `MEASURED`.
- **Drivable slab:** the `Floor` object carries the only large `BoxCollider`.
  `m_LocalPosition {x: -1.5703, y: -1, z: 146.1874}`, `m_LocalScale {x: 579.4047, y: 2, z: 601.85046}`
  (`READ` — `origin/main:Assets/Project/Scenes/Hard.unity:4444`, Transform anchor `&1551314016`;
  `m_Name: Floor` at line 4438). Its parent chain is
  `VehicleLab/Generated (do not edit by hand)/Floor` and every link has `m_IsActive: 1` (`READ`).
  World box **x[-291.27, 288.13], y[-2.00, 0.00], z[-154.74, 447.11]**, i.e. **579.40 × 601.85 m with
  its top face at y = 0.00** (`MEASURED`, `corner_geometry.py` BOX COLLIDER FOOTPRINTS).
- **Road network extent:** 650 objects matching `^road_`; pivots span **x[-259.30, 255.76] (515.07 m)**
  and **z[-116.50, 406.74] (523.24 m)** (`MEASURED`). The road mask covers **53,198 m²** inside
  x[-271, 268] z[-129, 419] (`MEASURED`, `--road=^road_ --pitch=8.18 --grid=1.0`).
- **Layout is a tight regular grid.** Tile-centre spacing on both axes is dominated by one module:
  x has 84 distinct lines with most common spacings 8.18 m ×20 and 8.17 m ×16; z has 72 distinct
  lines with 8.18 m ×31 and 8.17 m ×24 (`MEASURED`). Every one of the 650 tiles has the same world
  scale, 204.391 uniform (`MEASURED`). A single module and two dominant spacings is the signature of
  a laid grid, not of hand-placed streets.
- **No open highway.** The only wide pieces are the `road_double_*` family: 2 each of
  `road_double_base`, `_intersection`, `_junction`, `_turn_left`, `_turn_left_long`, `_turn_right`,
  `_turn_right_long`, plus 2 each of the two `road_connector_*` transition pieces (`MEASURED`,
  `scene_physics.py owners`). 16 double-width pieces against 582 single `road_base` tiles.

### 1.2 Medium.unity

```
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Medium.unity" --grid=8.0 --tiles="road"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Medium.unity" extent "road"
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Medium.unity" --grid=1.0 --road="road" --pitch=13.82
```

- **16 root objects**, and the same `Floor` box: **579.40 × 601.85 m, top face y = 0.00**, identical
  world extent to Hard's (`MEASURED`).
- **A completely different art kit.** Collider owners are `two-lane-linear-road-piece`,
  `two-lane-curved-road-piece`, `two-lane-T-junction`, `two-lane-road-roundabout`,
  `two-lane-linear-road-end-piece`, with buildings named `suburban-house-*`, `cb-apartment-*`,
  `industrial-buildings-*` and `infrastructure-building-power-plant` (`MEASURED`). None of Hard's
  names appear. The source asset is
  `Assets/Aside/Art/Toon City Pack/Models/Infrastructure/Roads-Streets-Highways/two-lane-linear-road-piece.fbx`
  (`MEASURED` — resolved by searching `.meta` files for the collider's mesh guid
  `8c338600d4e15465986843f5e7326d74`).
- **Road extent:** 246 road-named objects, pivots spanning **x[-173.67, 178.09] (351.76 m)**,
  **z[-107.48, 396.18] (503.66 m)** (`MEASURED`). Road mask covers **38,751 m²** inside
  x[-191, 196] z[-125, 414] (`MEASURED`).
- **Layout is irregular, not a grid.** 63 distinct x lines and 67 distinct z lines from 246 tiles,
  and no dominant spacing: the x modes are 1.11 ×3, 2.38 ×2, then 10.57, 8.43, 1.28, 3.83, 13.82,
  12.17 each ×1; the z modes are 10.41 ×2, 13.82 ×2, 1.30 ×2, 1.31 ×2, then 9.63, 4.18, 12.18,
  23.69 each ×1 (`MEASURED`). Compare Hard, where one spacing accounts for 36 of 84 x lines.
  `INFERRED`: Medium's roads were placed by hand piece by piece; Hard's were laid on a module.

### 1.3 Easy.unity

```
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Easy.unity" --grid=2.0 --tiles=^road_
python docs/tooling/scene-tools/scene_physics.py "<ref>/Easy.unity" owners "Floor|Ground|Road|Cube|Plane|SideWalk|Generated|VehicleLab|Border"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Easy.unity" reach -5
```

- **17 root objects**, and **no `Floor` object of any kind**. The scene's only `BoxCollider` is the
  `Car`'s (`MEASURED`). A `VehicleLab` root and a `Generated (do not edit by hand)` child are both
  present but carry no collider, so the generated floor was deleted and its container left behind
  (`MEASURED`).
- **This is the old greybox, re-placed.** The city sits under
  `Group 15 Greybox real one 1` at (-98.000, 0.000, 145.073), uniform scale **8.97659**, 83 direct
  children (`MEASURED`). `docs/tooling/scene-tools/README.md` records the greybox re-export of
  21 September 2026 landing at "(-114.67, -0.02, 130.02) at scale 8.98" (`READ`). Same scale,
  different position: the district was moved again after that README was written (`DERIVED`).
- **No road geometry at all.** The complete object inventory is 84 `wired-fence`, 54 kerbs
  (28 `SideWalk002`, 26 `SideWalk001`), 24 `Cube`, 4 `Building One`, 4 `Fence` group nodes, one
  `Bastrop-blocks-combos.001 (1)`, a `Door_Group`/`DoorFrame`/`Door`/`CTRL_Hole`/`Handle_*` cluster,
  the twelve anchors, and the system objects (`MEASURED` — name histogram over
  `scene_points.py` output). Zero objects named `road*`.
- **Extent:** pivots span x[-320.80, 291.45] (612.25 m), z[-154.00, 458.00] (612.00 m) (`MEASURED`),
  but that span is set by the border fence ring, not by the district.

### 1.4 Possible game scene.unity (origin/main)

- **14 roots, no city.** `Floor` `BoxCollider` **504.00 × 2 × 554.75 m**, world
  x[-252.00, 252.00], y[-2.00, 0.00], z[-154.74, 400.01], **top face y = 0.00** (`MEASURED`).
- `find_roads.py` reports **0 pavement objects, 0 building pivots** (`MEASURED`, §2.4).
- The twelve anchors match `delivery_points.py` exactly — `Pickup 01 South West Gate` at
  (-220.11, 0.00, 7.03) in both (`MEASURED` vs `READ` `delivery_points.py:28`).

---

## 2. Street widths and the block module

### 2.1 Two independent measurements agree for Hard

**Method A, tile-centre spacing.** `corner_geometry.py --tiles=^road_` clusters tile pivots per axis
and reports the gap between adjacent lines. Dominant gap **8.18 m** on both axes (`MEASURED`, §1.1).

**Method B, mesh bounds × transform scale.**
`python docs/tooling/scene-tools/fbx_mesh_bounds.py "Assets/Aside/Art/Blender/sehirlogo_01.fbx"`
(the asset resolved from the collider mesh guid `9880d46381ad3d7419bbc4b65517906e`) reports:

| mesh | FBX size x | FBX size y | FBX size z |
| --- | --- | --- | --- |
| `road_base` | 4.0000 | 4.0000 | 0.1000 |
| `road_junction` | 4.0000 | 4.0001 | 0.1000 |
| `road_intersection` | 4.0002 | 4.0001 | 0.1000 |
| `road_end` | 4.0000 | 4.4874 | 0.1000 |
| `road_double_base` | 8.0000 | 8.0000 | 0.1000 |

`MEASURED`. The model is Z-up, so FBX z is Unity y. Every road tile's world scale is 204.391
(`MEASURED`). `KnockablePropBaker.cs:20` states the city FBX "was authored in centimetres"
(`READ`), i.e. an import factor of 0.01.

`DERIVED`: 4.0000 × 0.01 × 204.391 = **8.176 m** for a single tile, and
8.0000 × 0.01 × 204.391 = **16.351 m** for a double. Slab thickness
0.1000 × 0.01 × 204.391 = **0.204 m**.

Method A's 8.18 m and Method B's 8.176 m are the same number reached two ways, which also confirms
the 0.01 import factor rather than assuming it.

**So Hard's streets are 8.18 m wide on a single carriageway and 16.35 m on a double.** `DERIVED`.

### 2.2 Medium

Same two methods. `two-lane-linear-road-piece` mesh is **11.2048 × 0.1650 × 11.2048** FBX units
(`MEASURED`); this kit is Y-up, so the thin axis is the slab. World scale 1.2333 and a metre-unit
import give `DERIVED` **11.2048 × 1.2333 = 13.82 m** square, thickness
0.1650 × 1.2333 = **0.204 m**. The value 13.82 m appears in the measured spacing histogram
(x ×1, z ×2), which is the cross-check (`MEASURED`, §1.2).

**Medium's street module is 13.82 m, a two-lane piece.** `DERIVED`.

### 2.3 Width across the mask, both scenes

`corner_geometry.py --road=... --grid=1.0` builds the carriageway from the tiles and takes a distance
field over it, so the half-width at every road cell is measured rather than assumed.

| Scene | half-width min | median | max | street width (×2) |
| --- | --- | --- | --- | --- |
| Hard (`--pitch=8.18`) | 1.00 m | 3.00 m | 10.00 m | 2.00 / 6.00 / 20.00 m |
| Medium (`--pitch=13.82`) | 1.00 m | 4.00 m | 10.49 m | 2.00 / 8.00 / 20.98 m |

`MEASURED`. Read these as a per-cell distribution over the whole network, not as "a street is 6 m
wide": the minimum is a tile corner, the maximum is the middle of a junction or roundabout. The
module figures in §2.1 and §2.2 are the honest answer to "how wide is a street"; this table is the
honest answer to "how much room is there at the tightest and widest points".

### 2.4 Easy and Possible game scene: not derivable with the existing tool

`python docs/tooling/scene-tools/find_roads.py "<ref>/<scene>.unity"` for each of the four:

| Scene | Result |
| --- | --- |
| `Hard.unity` | `Floor 579 x 602 centred (-2, 146)`; **0 pavement objects**, 1 building pivot; zero centrelines |
| `Medium.unity` | `Floor 579 x 602 centred (-2, 146)`; **0 pavement objects**, 1 building pivot; zero centrelines |
| `Easy.unity` | exits 1: `No Floor object found; cannot tell what is drivable.` |
| `Possible game scene.unity` | `Floor 504 x 555 centred (0, 123)`; **0 pavement objects, 0 building pivots**; zero centrelines |

`MEASURED`. The tool recovers streets by clustering `SideWalk` kerbs. Hard and Medium have a `Floor`
but no `SideWalk` objects; Easy has 54 `SideWalk` objects but no `Floor`. **So the street grid cannot
be recovered in any of the four scenes on `origin/main`.** The README's "seven north-south streets
and seven east-west ones, so 49 junctions, from 54 `SideWalk` objects" describes a scene state that
no longer exists on the authoritative tree (`READ` — `docs/tooling/scene-tools/README.md`, "How the
delivery points were found").

Easy's street widths: `UNKNOWN`. **Settling check** — Easy carries the same 54 greybox kerb meshes at
the same import scale 8.97659 as the scene the README figures came from, so either restore a `Floor`
object to Easy or give `find_roads.py` an explicit bounds argument, then re-run it; the kerb-pair
widths should reproduce.

---

## 3. Verticality

```
python docs/tooling/scene-tools/scene_physics.py "<ref>/<scene>.unity" extent "."
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" extent "^road_"
```

**Hard is flat.** Of 650 `^road_` pivots: **615 at y = 0.40**, 1 at 0.60, 17 at -47.90, 34 at -83.64
(`MEASURED`). The 51 negative ones are unplaced import palette copies (§5.1). Across all 1986
transforms the modal heights are y = 0.40 (×1599), -83.64 (×92), 0.41 (×91), 0.52 (×42), 0.00 (×30),
0.39 (×27) (`MEASURED`). Nothing drivable steps.

**Medium is flat.** All 246 road pivots sit at y = 0.12 (×243), 0.13 (×2), 0.15 (×1) — a 3 cm spread
over a 352 × 504 m district (`MEASURED`).

**Three vertical features exist, none of them confirmed drivable.**

1. **16 speed bumps in Hard, 17 in Medium.** Hard's `SpeedBumps` root holds 16 children, each a
   `MeshCollider` at world y = 0.228, uniform scale 2.090, all sharing mesh
   `{fileID: -7102350196987980496, guid: be9b112dbd8a0d241ac4b804016e1a9a}` (`MEASURED`,
   `scene_physics.py detail "SpeedBump|Bump"`). They are placed in pairs across streets, e.g.
   (-1.78, 0.228, 131.14) and (-1.78, 0.228, 145.54). Medium's `SpeedBumps` root holds 17 children
   (`MEASURED`). Bump height: `UNKNOWN`. **Settling check** — the mesh asset
   `Assets/Aside/Art/FastMesh/Meshs/Road/SpeedBump.fbx` exists on `origin/main` but not in the local
   working tree (`MEASURED` — `git grep -l be9b112dbd8a0d241ac4b804016e1a9a origin/main`, which also
   finds `Assets/Project/Prefabs/SpeedBumps.prefab`); fetch it through LFS and run
   `fbx_mesh_bounds.py` on it.
2. **Footbridges in Hard.** `Footbridge01` and `Footbridge` each own one on-map MeshCollider at pivot
   y = 0.39 (`MEASURED`, `reach`). `Footbridge01`'s mesh is 8.2560 × 7.7916 × 5.6214 FBX units
   (`MEASURED`), which at 0.01 × 204.391 is `DERIVED` **16.9 × 15.9 m footprint, 11.49 m tall**. So
   there is an 11.5 m structure in the city. Whether a car can get onto it: `UNKNOWN`.
   **Settling check** — a play-mode drive at it, or `fbx_mesh_bounds.py --json` plus a ramp-gradient
   check against the suspension travel in `VehicleTuning`.
3. **One stone pile 7 m up in Hard.** The `stonepiles02` family's collider owners span pivot
   y 0.40 to **7.52** (`MEASURED`, `reach`). One object of that family is seven metres above the
   rest. `INFERRED`: a misplaced prop rather than a design feature; not drivable either way.

**Verdict: the city is flat.** `DERIVED` from the pivot distributions above. There are no ramps, no
bridges a car can use, and no elevation change on any confirmed drivable surface in any of the four
scenes.

---

## 4. What the car can actually contact

This is the section that changes the picture most, so the evidence is given in full.

### 4.1 The merged road collider has no mesh

```
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" detail "^(Roads_Parent|Floor|CityProps|City)$"
```

`Roads_Parent` resolves to world (295.58, 0.40, -158.13), uniform scale 703.402, parented
`City/Roads_Parent` (`MEASURED`). Its GameObject `&8744834134912007418` holds three components
(`READ` — `origin/main:Assets/Project/Scenes/Hard.unity:199831-199845`):

- Transform `&7511784478815654055`
- `MeshCollider` `&8744834134912007419` — `m_Enabled: 1`, `m_IsTrigger: 0`, `m_Convex: 0`,
  **`m_Mesh: {fileID: 0}`** (`READ` — `Hard.unity:199849` for the document header, `:199870` for the
  mesh field)
- `MonoBehaviour` `&8744834134912007420` — `SurfaceType`, `kind: 0`, `gripMultiplier: 1`
  (`READ` — `Hard.unity:199872-199886`)

`m_Mesh: {fileID: 0}` is a null reference. **A MeshCollider with no mesh has no collision geometry**
(`INFERRED` from the serialized value; not observed at runtime). It is not the only one: 7 of Hard's
1295 MeshColliders have a null mesh — `Roads_Parent`, `Traffic_Assets`, `Trees`, `Sun` ×2,
`Starlight Sun` ×2 — all of them container nodes (`MEASURED` — null-mesh tally over each `<ref>`
blob). Medium has 3, all on road objects: `long road horizontal`, `long road horizontal (1)`,
`'long road vertical '`. Easy has 1, `Door_Group`. `Possible game scene` has none because it has no
MeshColliders at all.

### 4.2 No placed road tile owns a collider in Hard

```
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" owners "^(road_|Roads|Floor|Plane|SideWalk|Border)"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" detail "^(road_|Plane)" --with-colliders
```

| family | objects | colliders |
| --- | --- | --- |
| `road_base` | 582 | 2 |
| `road_junction` | 20 | 2 |
| `road_turn_left` | 9 | 2 |
| `road_intersection` | 8 | 2 |
| `road_end` | 5 | 2 |
| `road_turn_left_long` | 4 | 2 |
| each of 11 other `road_*` families | 2 | 2 |
| `Plane` | 4 | 8 |
| `Floor` | 1 | 1 (BoxCollider) |
| `Roads_Parent` | 1 | 1 (null-mesh MeshCollider) |

`MEASURED`. The brief's "of 582 `road_base` objects only one carries its own collider" is **correct
as a count** — but the detail run shows what that one object is. Every collider-owning road object:

- sits at world **y = -47.902**, i.e. 48 m below the map;
- is parented directly to `City`, **not** to `Roads_Parent` (path `City/road_base`);
- carries **two** identical MeshColliders, not one.

There are 17 such road objects plus 4 `Plane` objects at y ≈ -54.5 (`MEASURED`). Exactly one object
per tile type: this is the FBX's unplaced palette, the same thing `KnockablePropBaker.cs:40` calls
"whole groups of props 84 m below the map, never placed" (`READ`).

**So zero of the 615 placed road tiles in Hard own a collider, and the one merged collider that was
meant to stand in for them references no mesh.** `DERIVED` from §4.1 and this table.

Medium is the opposite: 221 colliders across 246 road-named objects, with 22 on the
`two-lane-linear-road-piece` family alone (`MEASURED`). Medium's roads are physically present.

### 4.3 Duplicate colliders

1295 MeshCollider documents in Hard resolve to 1270 distinct owner GameObjects, so **25 objects carry
two MeshColliders each** (`MEASURED` — mesh-collider-per-GameObject tally). 21 of those 25 are the
below-map palette objects of §4.2. The `road_base` GameObject's own `m_Component` list has four
entries (Transform, MeshFilter, MeshRenderer, and one collider document anchor), so the duplicate is
a second collider document attached to the same GameObject (`READ` — `m_Component` block of the
`m_Name: road_base` GameObject). Harmless in practice because the objects are off the map, but it is
a scene defect.

### 4.4 Collider inventory split by whether it is on the map

```
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" reach -5
python docs/tooling/scene-tools/scene_physics.py "<ref>/Easy.unity" reach -5
```

Hard: **1221 colliders on the map** across 127 name families, **109 below the map** across 60
families (`MEASURED`). The on-map list is almost entirely street furniture and building shells at
pivot y = 0.39–0.57: `streetlamp01` ×307, `fence03_sample` ×91, `TrafficLight02` ×67, `tree03` ×62,
`commercial_base` ×59, `tree02` ×43, `polelight01` ×42, `apartment_base` ×41, `Bus_stop` ×35,
`tree04` ×35, `mailbox` ×30, `tree01` ×28, `streetlamp02` ×26, down a long tail. Easy: 169 on the
map, **0 below** (`MEASURED`).

So the car has plenty to crash into. What it *stands on* is a separate question, and it is the one
the scene files do not settle.

### 4.5 What holds the car up — the one thing that stays UNKNOWN

Three candidate surfaces exist in Hard, at three different heights:

| Candidate | Collider | Height | Evidence |
| --- | --- | --- | --- |
| `Floor` | BoxCollider, 579.40 × 601.85 m | top face **y = 0.00** | `READ` `Hard.unity:4444` |
| Placed road tiles | **none** | visual slab at pivot y = 0.40, 0.204 m thick | §2.1, §4.2 |
| `Bastrop-blocks-combos.001` | non-convex MeshCollider | pivot **y = 0.398** | `MEASURED` |

`Bastrop-blocks-combos.001` is the interesting one. In Hard it resolves to world
(-6.39, 0.398, 139.16) with uniform scale **-65155.355** — negative on every axis — carrying one
non-convex MeshCollider on mesh `{fileID: 1944168556134712843, guid: 9880d46381ad...}` (`MEASURED`).
That mesh measures 0.8181 × 0.8269 × 0.0766 FBX units over 72 vertices (`MEASURED`), so at
0.01 × 65155.355 its world size is `DERIVED` **≈533 × 539 m footprint, ≈50 m tall**. The same object
exists in Medium at scale (-74375.077, -73704.795, -65913.792), pivot y = 0.037, and in Easy at
(-69773.650, -72439.586, -53445.060), pivot y = 0.000 — same mesh in all three (`MEASURED`).

`corner_geometry.py` already names this hazard in `classify()`: "One object in the greybox covers
most of the map once its mesh bounds are applied, which is either a boundary wall or a mesh whose
bounds do not belong to the transform the scene file pairs them with" (`READ`).

**`UNKNOWN`: which collider the four suspension raycasts hit at the spawn point, in each scene.**
**Settling check** — one `Physics.Raycast` straight down from the car's spawn, or a play-mode drop
test, reporting `hit.collider.name` and `hit.point.y`. Nothing in the scene files can answer it,
because the answer depends on whether PhysX accepts a non-convex MeshCollider under a
triple-negative scale.

Two things that *are* established, and that make this the right question to ask:

- The road visuals sit **0.40 m above the `Floor` collider's top face** (`DERIVED`: City root
  `m_LocalPosition {y: 0.4}` at `Hard.unity:187633` versus Floor top face y = 0.00). If the car rests
  on the `Floor`, its wheels are inside the road slab.
- `KnockablePropBaker.cs` contradicts itself about where the ground is: `:40` says "The road surface
  is at y = 0", `:346` says "the road collider under them sits at +0.40" (`READ`, both). And
  `SeatOnRoad` finds the ground with a downward `Physics.RaycastAll` ignoring dynamic bodies
  (`READ` — `KnockablePropBaker.cs` `SeatOnRoad`), so the props were seated on whatever that
  raycast hit. The 33 baked props in Hard have box bottoms around y = 0.06–1.14 (`MEASURED`, box
  footprint y-mins), which is consistent with a ground surface near 0.0–0.5 and does not separate the
  candidates.

**Easy is the worst case.** It has no `Floor` box and no road geometry, so its *only* candidate
drivable collider is the negative-scaled `Bastrop-blocks-combos.001 (1)` at pivot y = 0.000
(`MEASURED`). `UNKNOWN` whether the car has any ground at all in Easy; same settling check.

Context for that: **`Easy.unity` is not in the build.** `origin/main`'s
`ProjectSettings/EditorBuildSettings.asset` lists exactly six paths —
`MainMenu.unity` (enabled 1), `Possible game scene.unity` (1), `VehicleLab.unity` (0),
`Assets/Scenes/SampleScene.unity` (0), `Medium.unity` (1), `Hard.unity` (1) — and `Easy.unity`
appears nowhere in it (`READ` — `git show origin/main:ProjectSettings/EditorBuildSettings.asset`,
every `path:` line listed). So Easy is an unfinished scene rather than a shipping one, which is the
most likely explanation for the missing floor. Plan item 3b owns the build-settings verdict.

### 4.6 Surface tags land almost entirely on things that are not ground

```
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" script 95c5eae1aeacb5341adbe50a1c9f4a67
```

84 `SurfaceType` components in Hard, 0 in Medium, 0 in Easy, 0 in `Possible game scene`
(`MEASURED` — guid `95c5eae1aeacb5341adbe50a1c9f4a67` from `SurfaceType.cs.meta`). The enum is
`Tarmac, Wet, Dirt, Grass, Ice, Metal, Concrete` = 0..6 (`READ` — `SurfaceType.cs:12-20`).

| kind | profile as serialized | count | owners |
| --- | --- | --- | --- |
| 0 Tarmac | grip 1, drive 1, brake 1 | **2** | `Roads_Parent` (null-mesh collider), `road_base` (palette tile at y = -47.90) |
| 3 Grass | grip 0.45, drive 0.6, brake 0.6, rollingResistance 5 | 22 | `parkbase01_sample` ×12, `parkbase02_sample` ×5, `parkbase03_sample` ×2, `parkbase01/02/03` ×1 each |
| 6 Concrete | grip 0.92, drive 0.97, brake 0.93, rollingResistance 0 | 60 | `commercial_base` ×60 |

`MEASURED`. The serialized numbers match `SurfaceType.DefaultsFor` for Grass
(`READ` — `SurfaceType.cs:191`) and Concrete (`READ` — `SurfaceType.cs:236`), so nobody hand-edited
them.

`DERIVED`, and it matters: **neither Tarmac tag is on a collider the car can touch** — one has no
mesh, the other is 48 m underground. The 60 Concrete tags are on building base meshes. The `Floor`
box carries no `SurfaceType` at all (`MEASURED`), so it falls through to `SurfaceType.Default`,
which is Tarmac at grip 1.0 (`READ` — `SurfaceType.cs:118`). The 22 Grass tags on park bases are the
only surface variation the car could plausibly reach.

So the "84 city surfaces tagged" work is real and correctly valued, but on the evidence here the
whole drivable area behaves as untagged tarmac. `INFERRED`, not observed at runtime; it follows from
§4.1, §4.2 and §4.5 and is settled by the same raycast check.

---

## 5. Dynamic obstacles, ramps and shortcuts

### 5.1 Knockable props: Hard only, and the inventory is not what the brief says

```
python docs/tooling/scene-tools/scene_physics.py "<ref>/<scene>.unity" script 5d8c1f2a3b4e4c6d9a7b8e0f1c2d3e4f
```

`KnockableProp` instances per scene, by script guid `5d8c1f2a3b4e4c6d9a7b8e0f1c2d3e4f`
(from `KnockableProp.cs.meta`):

| Scene | KnockableProp | SurfaceType | Rigidbodies |
| --- | --- | --- | --- |
| `Hard.unity` | **33** | 84 | 34 (33 props + Car) |
| `Medium.unity` | **0** | **0** | 1 (Car) |
| `Easy.unity` | **0** | **0** | 1 (Car) |
| `Possible game scene.unity` | **0** | **0** | 1 (Car) |
| `Border.prefab` | 0 | 0 | 0 |

`MEASURED`. **Confirmed: Easy and Medium have no knockable props and no surface tags at all.** The
local `Kyuri's Scene.unity` on `HEAD` has the same 33 and 84 (`MEASURED`), which is consistent with
the `R082` rename to `Hard.unity` recorded in plan §0.2.

The 33 in Hard, by family and baked mass:

| family | count | mass each | tuning source |
| --- | --- | --- | --- |
| mobile toilet (`mobile_toilet.*`) | 12 | 180 kg | `KnockablePropBaker.cs:91` |
| picnic table (`picnictable01.*` ×6, `picnictable02.*` ×3) | 9 | 120 kg | `KnockablePropBaker.cs:90` |
| trash container (`trashcontainer01.*`) | 9 | 60 kg | `KnockablePropBaker.cs:89` |
| road blocker (`RoadBlocker04.*`) | 3 | 25 kg | `KnockablePropBaker.cs:96` |
| traffic cone (`cone_type*`) | **0** | 5 kg declared | `KnockablePropBaker.cs:92` |

`MEASURED` for the counts and the serialized `bakedMassKg`; `READ` for the family table. The brief
lists five masses including 5 kg cones. The 5 kg family exists in code and has **zero instances**:
`KnockablePropBaker.cs:40` states "Every `cone_type` in the file is one of them", referring to the
groups below the map, and `:101` sets `MinimumWorldY = -5f` with `:162` skipping anything below it
(`READ`). The cone-shaped things actually on the road are `RoadBlocker04` at 25 kg (`READ`,
`:92-96`).

### 5.2 What happens on contact, and whether they are real obstacles

Each baked prop received, per `MakeKnockable` (`READ` — `KnockablePropBaker.cs`):

- reparenting to the scene root with world position and rotation kept and scale made positive on
  every axis, because "PhysX cannot run a Rigidbody in that hierarchy" under the city's negative,
  non-uniform group scales (`READ` — `:17-33`);
- the original `MeshCollider` **disabled, not destroyed**, and a `BoxCollider` fitted to the mesh's
  robust world bounds (2nd–98th percentile of the vertex cloud, +4 % pad) (`READ`);
- `Rigidbody` with the family mass, `linearDamping 0.2` (`:360`), `angularDamping 1.5` (`:363`),
  `inertiaTensor = CompactInertia(mass, 2f)` so a 9 m table rotates like a 2 m one (`:369`),
  `collisionDetectionMode Discrete`, `sleepThreshold 0.05`, and `Sleep()` on creation (`READ`).

Measured world box footprints in Hard (`MEASURED`, `corner_geometry.py` BOX COLLIDER FOOTPRINTS):

- picnic tables **9.21 × 9.21 m** footprint (one pair 8.48–8.73 m), 1.98 m or 5.36 m tall
- trash containers 2.74–3.68 × 2.97–3.69 m, 2.57 m tall
- mobile toilets 2.49 × 2.61 m, 3.90 m tall
- road blockers 3.97 × 2.17 m, 0.97 m tall
- the car for comparison: 1.80 × 4.20 m, 1.00 m tall

`DERIVED`: **these are real obstacles, not cosmetic.** A 9.21 m picnic table is 2.2 car-lengths
across and 1.98 m tall, against a 900 kg car (plan item 7). At 120 kg with a capped inertia tensor it
will move when hit, by design: `KnockablePropBaker.cs:72-87` records that mass is "a plain feel
number per family, chosen so the car always wins and heavier families resist more", after an earlier
version derived mass from size and produced "a 4 tonne table the car bounced off" (`READ`). The baker
itself flags props over 6 m on a side and explains they are knockable anyway (`READ`).

Two caveats, both `READ` from the same file:

- the box is fitted to **axis-aligned** world bounds, so a prop rotated off the grid gets a loose
  box (`:300-306`);
- `strayVertexFraction` is **negative** on 21 of the 33 props (-0.08 on every mobile toilet and
  trash container, -0.024 on several picnic tables) (`MEASURED`). A negative value means the kept
  box came out *larger* than the raw vertex extent, which is what the +4 % pad does on a clean mesh.
  So the correction is behaving, but the field's own tooltip describes it as a fraction discarded,
  `[Range(0f, 1f)]` (`READ` — `KnockableProp.cs`), and a negative value is outside that range.
  Cosmetic inspector issue, no runtime effect.

### 5.3 Speed bumps

16 in Hard, 17 in Medium, 0 in Easy and 0 in `Possible game scene` (`MEASURED`, §3). These are the
only road-surface features that are not cosmetic by the §4.2 argument — they are separate objects
with their own MeshColliders at y = 0.228, outside the `City` hierarchy entirely (path
`SpeedBumps/SpeedBump (n)`) (`MEASURED`). They are not mentioned in the brief.

### 5.4 The boundary

`Border.prefab` is 89 GameObjects carrying **84 MeshColliders**, 4 per `wired-fence (n)` across 21
fence groups, arranged under 4 `Fence` group nodes (`MEASURED` — `scene_physics.py owners` on the
prefab). Prefab-local extent x[-320.80, 291.45], z[-154.00, 458.00], y[0.00, 0.04] (`MEASURED`).

- **Hard**: instanced once, `m_LocalPosition` (-270.19733, 0, 412), named `Border`
  (`READ` — `PrefabInstance &885107737` modifications, `m_SourcePrefab` guid
  `70f442799ea311641904614d76bbbf24`).
- **Medium**: instanced once at (-265.51056, 0.03702, 395.4957) (`READ`, same form).
- **Easy**: **not a prefab instance.** The same hierarchy exists as plain objects under a `Border`
  root at (-265.511, 0.037, 395.496), with 4 `Fence` groups and 84 `wired-fence` objects
  (`MEASURED`). Zero references to the prefab guid in the file (`MEASURED`).
- **Possible game scene**: absent (`MEASURED`).

`DERIVED`: Hard and Medium's true collider counts are 84 higher than the per-scene table in §0,
because prefab instance contents are not in the scene file. Easy's 168 already include its 84.

### 5.5 Shortcuts

There is no object in any of the four scenes named as a shortcut, alley, ramp or jump
(`MEASURED` — full name histograms, §1). What exists instead is an unintended one:

`INFERRED` — **the whole district is open ground.** The `Floor` box in Hard and Medium is
579.40 × 601.85 m and flat, and it underlies the entire road network (road extent 515 × 523 m in
Hard, 352 × 504 m in Medium). The roads impose no constraint the car has to respect. The
carriageway run supports this from the other direction: of Hard's 12 delivery anchors only **6 sit on
the carriageway at all**, and only 4 of those are reachable from the car's start by an all-road route
(`MEASURED` — `--road=^road_ --pitch=8.18 --grid=1.0`). In Medium 10 of 12 are on the carriageway but
the max-min route search reaches **none** of them by road alone, so Medium's road mask is not
contiguous from the spawn (`MEASURED`). Either way the player must leave the road to finish a job, so
cutting across open ground is not a shortcut, it is the route.

This bears directly on the "wide turns" direction in the brief: if half the anchors are off-road,
corner width is not the only geometry the turning circle has to fit. Street widths are §2;
`ZoneDetection` uses `pickupRadius = 6f` and `dropOffRadius = 6f` (`READ` —
`ZoneDetection.cs:21-22`), serialized as 6 and 6 in Hard with `checkInterval: 0.1`
(`MEASURED` — `scene_physics.py script 6c6239d78dc6b374daa7df0491bd32e7`). A 12 m-diameter
trigger on an 8.18 m street spills onto the pavement on both sides (`DERIVED`: 12.00 > 8.18). The
turning-circle side of that comparison belongs to Group 1 and to
`.agents/tasks/turning-geometry-2026-10-14/report.md` if it ever exists.

---

## 6. Pickup → drop-off travel time

### 6.1 The method, and the assumption it rests on

`order_distances.py` costs all 36 pairs on a grid (Manhattan) metric plus the approach leg from the
car's start, because `Order` starts its countdown at spawn and not at pickup (`READ` —
`docs/tooling/scene-tools/README.md`, "What the order time limit costs"). It reads a hardcoded point
table that matches only `Possible game scene` (§1.4), so for the other three scenes the points were
taken from the scene files with `scene_physics.py jobs`, which does the same arithmetic and imports
the same speed constants.

**Validation.** `python docs/tooling/scene-tools/order_distances.py 50` and
`python docs/tooling/scene-tools/scene_physics.py "<ref>/Possible game scene.unity" jobs 50` produce
the same figures: shortest 260 m / 13 s (`Pickup 02 Centre Cross` → `DropOff 04 Centre North`),
mean 557 m / 28 s, longest 954 m / 48 s (`Pickup 03 North East Corner` → `DropOff 06 South West
Yard`), 36/36 at 50 s, 35/36 at 45 s (`MEASURED`, both commands).

**The assumption, stated plainly.** Every "seconds" figure below is `DERIVED` from
`WORKING_AVERAGE = 20.0` m/s, which is a number written into
`docs/tooling/scene-tools/delivery_points.py:49`, not a lap time. `TOP_SPEED = 32.0` m/s and
`SIDEWAYS_GRIP = 13.0` m/s² sit beside it at `:46-47` and are the figures plan item 7 is checking
against `vehicle_measurements.csv`. If the current measured top speed is 25 m/s rather than 32, then
20 m/s is 80 % of top speed rather than 63 %, and the costing below is optimistic. **It has not been
re-based** — doing so silently is exactly what the plan forbids.

The grid metric is also doing less work than it looks like. It is the honest cost when streets run on
the axes, which is true of Hard. It is pessimistic for `Possible game scene`, which on `origin/main`
is an empty plane with no streets at all, and for the off-road legs of §5.5.

### 6.2 Distribution, per scene

All four runs at a 50 s limit. Metres include the approach leg; seconds are at 20 m/s.

| | `Hard` | `Medium` | `Easy` | `Possible game scene` |
| --- | --- | --- | --- | --- |
| car start (x, z) | (-1.74, 0.00) | (11.70, 72.40) | (-1.74, 0.00) | (0.00, 0.00) |
| shortest | **150 m / 7.5 s** | 195 m / 9.8 s | 266 m / 13.3 s | 260 m / 13.0 s |
| p10 | 320 m / 16.0 s | 293 m / 14.7 s | 389 m / 19.4 s | 343 m / 17.2 s |
| p25 | 382 m / 19.1 s | 379 m / 19.0 s | 452 m / 22.6 s | 452 m / 22.6 s |
| **median** | **557 m / 27.8 s** | **533 m / 26.7 s** | **542 m / 27.1 s** | **541 m / 27.1 s** |
| mean | 520 m / 26 s | 514 m / 26 s | 570 m / 29 s | 557 m / 28 s |
| p75 | 623 m / 31.1 s | 639 m / 31.9 s | 701 m / 35.1 s | 660 m / 33.0 s |
| p90 | 788 m / 39.4 s | 746 m / 37.3 s | 799 m / 39.9 s | 790 m / 39.5 s |
| longest | **918 m / 45.9 s** | 893 m / 44.7 s | 852 m / 42.6 s | **954 m / 47.7 s** |
| clear 45 s | 35/36 | **36/36** | **36/36** | 35/36 |
| clear 50 s | 36/36 | 36/36 | 36/36 | 36/36 |

`MEASURED` for distances, `DERIVED` for seconds.

Histogram of the 36 pairs by seconds at 20 m/s (`MEASURED`):

| band | Hard | Medium | Easy | Possible game scene |
| --- | --- | --- | --- | --- |
| 0–15 s | 3 | 5 | 1 | 1 |
| 15–20 s | 12 | 8 | 5 | 6 |
| 20–25 s | 2 | 5 | 8 | 9 |
| 25–30 s | 5 | 6 | 8 | 5 |
| 30–40 s | 11 | 10 | 11 | 12 |
| 40–50 s | 3 | 2 | 3 | 3 |
| over 50 s | 0 | 0 | 0 | 0 |

**The shape is the finding, not the extremes.** All four scenes put the bulk of their jobs in two
clumps: a short cluster under 20 s and a long cluster from 30 to 40 s, with a thin middle. Hard is
the most bimodal (15 of 36 under 20 s, 14 over 30 s, only 2 between 20 and 25 s). So a player's
experience of the timer depends heavily on which clump they draw, and order selection is random per
plan item 13. At a 50 s limit every pair clears in every scene; at 45 s, Hard and
`Possible game scene` each have exactly one pair that does not.

Worst pairs, for the record (`MEASURED`): Hard `Pickup 03 North East Corner` → `DropOff 03 West
Midpoint` 918 m; Medium `Pickup 06 North Centre` → `DropOff 06 South West Yard` 893 m; Easy
`Pickup 01 South West Gate` → `DropOff 05 East Midpoint` 852 m; `Possible game scene` `Pickup 03
North East Corner` → `DropOff 06 South West Yard` 954 m.

### 6.3 The anchors moved, and `delivery_points.py` is stale for three scenes

`MEASURED` — `Pickup 01 South West Gate` world position per scene:

| Scene | position | matches `delivery_points.py:28`? |
| --- | --- | --- |
| `Possible game scene` | (-220.11, 0.00, 7.03) | yes |
| `Hard` | (0.00, 0.00, 137.00) | no |
| `Medium` | (-96.30, 0.00, -22.00) | no |
| `Easy` | (-245.90, 0.00, -22.00) | no |

All twelve differ per scene. The names still describe the old layout, which is now misleading:
Hard's `Pickup 01 South West Gate` is at x = 0, the middle of the map, and Hard's `Pickup 06 North
Centre` is at z = -111.50, the southern edge. `MEASURED`. Cosmetic, but it will mislead anyone
reading a log.

---

## 7. The coordinate trap, per scene

Confirmed and extended. Every scene except `Possible game scene` parents its city under a root with a
non-unit scale, so a child's `m_LocalPosition` read straight out of the YAML is wrong.

| Scene | container | world position | uniform scale | direct children |
| --- | --- | --- | --- | --- |
| `Hard` | `City` | (-6.3905, 0.4, 139.24) | **2.0439076** | 76 |
| `Medium` | `'City '` (trailing space in the name) | (-1.000, 0.000, 85.000) | **1.23330** | 10 |
| `Easy` | `Group 15 Greybox real one 1` | (-98.000, 0.000, 145.073) | **8.97659** | 83 |
| `Possible game scene` | none | — | — | — |

`READ` for Hard — `origin/main:Assets/Project/Scenes/Hard.unity:187633`, Transform anchor
`&8371924365102370620`: `m_LocalPosition {x: -6.3905, y: 0.4, z: 139.24}`,
`m_LocalScale {x: 2.0439076, y: 2.0439076, z: 2.0439076}`. `MEASURED` for the other two
(`corner_geometry.py` ROOT OBJECTS, which prints the scale every child inherits).

Nested containers compound it, and two of them are negative:

| Scene | node | world scale |
| --- | --- | --- |
| `Hard` | `City/CityProps` | **-871.651** uniform |
| `Hard` | `City/Roads_Parent` | 703.402 uniform |
| `Hard` | `City/Bastrop-blocks-combos.001` | **-65155.355** uniform |
| `Medium` | `'City '/Bastrop-blocks-combos.001 (1)` | **(-74375.077, -73704.795, -65913.792)** |
| `Easy` | `Bastrop-blocks-combos.001 (1)` (scene root) | **(-69773.650, -72439.586, -53445.060)** |
| `Easy` | greybox children, e.g. `Cube.004` | (1493.158, 1448.289, 1705.550) |

`MEASURED`. Practical rule, unchanged: use `scene_points.py`, `corner_geometry.py` or
`scene_physics.py`, all of which resolve the hierarchy and print world space. Never hand-read
`m_LocalPosition` for a child of any of these nodes. Note that `Medium`'s container name has a
trailing space, so a regex anchored with `^City$` silently misses it — that is how it was nearly
missed here.

---

## Contradictions

Each entry: the two claims, the discriminating check that was run, the verdict and its label.

**C1. "The car drives on a single merged MeshCollider on parent `Roads_Parent`."**
- A: the investigation brief for this item.
- B: that MeshCollider's `m_Mesh` is `{fileID: 0}`.
- Check: read the `Roads_Parent` GameObject's component list and every component document
  (`origin/main:Assets/Project/Scenes/Hard.unity:199831-199886`).
- **Verdict: A is wrong.** The collider exists and is enabled but references no mesh, so it has no
  geometry. `READ` for the null reference; `INFERRED` (not observed at runtime) for the consequence.

**C2. "Of 582 `road_base` objects only one carries its own collider."**
- A: the brief, implying the one collider is the road the car drives on.
- B: the one `road_base` that owns colliders sits at world y = -47.902, is parented to `City` not to
  `Roads_Parent`, and carries two MeshColliders.
- Check: `scene_physics.py owners` then `detail --with-colliders` on `^(road_|Plane)`.
- **Verdict: the count is right, the reading is wrong.** Zero placed road tiles own a collider.
  `MEASURED`.

**C3. Street widths "8.8 m to 25.5 m".**
- A: the brief.
- B: measured module 8.18 m single / 16.35 m double (Hard) and 13.82 m (Medium), two independent
  methods agreeing in each case; mask half-widths ×2 give 2.00–20.00 m (Hard) and 2.00–20.98 m
  (Medium).
- Check: tile-centre spacing (`--tiles=`), mesh bounds × world scale (`fbx_mesh_bounds.py`), and the
  carriageway distance field (`--road=`).
- **Verdict: 8.8 and 25.5 are not reproduced by any of the three methods.** `MEASURED`. Neither
  figure is dismissed: the brief does not say how they were obtained, and 25.5 is close to the
  20.98 m widest point in Medium. **Settling check** — name the command and scene the 8.8/25.5 pair
  came from, then re-run it; until then the module figures are the citable ones.

**C4. Knockable prop masses "60/120/180/5/25 kg".**
- A: the brief, as five families in the scene.
- B: five families in the baker table (`KnockablePropBaker.cs:89-96`), but the 5 kg `cone_type`
  family has **zero** baked instances; the 33 props are 12×180, 9×120, 9×60, 3×25.
- Check: dump every `KnockableProp` component's `family` and `bakedMassKg` from the scene YAML.
- **Verdict: the table is right, the scene inventory is not.** `MEASURED`.

**C5. "Floor top face y = 0.4."**
- A: plan §0.5 and the brief.
- B: the `Floor` BoxCollider is at local y = -1 with y-scale 2, so it spans y[-2.00, 0.00] — top face
  **y = 0.00** — in Hard, Medium and `Possible game scene`; Easy has no Floor at all. The 0.4 belongs
  to the `City` root's `m_LocalPosition`, which is where the road *visuals* sit.
- Check: read the Floor Transform and BoxCollider (`Hard.unity:4444`) and the City Transform
  (`:187633`).
- **Verdict: 0.4 is the city's height, not the floor's.** They differ by 0.40 m. `READ`.

**C6. `KnockablePropBaker.cs` disagrees with itself about the ground height.**
- A: `:40` — "The road surface is at y = 0".
- B: `:346` — "the road collider under them sits at +0.40".
- Check: both lines read; §4.5 shows both heights have a real collider candidate (`Floor` top at
  0.00, `Bastrop` pivot at 0.398).
- **Verdict: unresolved in the files, and it is the same question as §4.5.** `UNKNOWN`.
  **Settling check** — one downward `Physics.Raycast` from the spawn point reporting
  `hit.collider.name` and `hit.point.y`.

**C7. The scene-tools README's street grid.**
- A: `docs/tooling/scene-tools/README.md` — "seven north-south streets and seven east-west ones, so
  49 junctions, from 54 `SideWalk` objects", and a `Floor` of 504 × 555.
- B: `find_roads.py` finds 0 pavement objects in Hard, Medium and `Possible game scene`, and refuses
  to run on Easy for lack of a `Floor`. The 504 × 555 Floor survives only in
  `Possible game scene`, whose district has been deleted.
- Check: run `find_roads.py` against all four `<ref>` scenes.
- **Verdict: the README describes a scene state that does not exist on `origin/main`.**
  `MEASURED`. Documentation defect; the README's own instruction to "re-run `find_roads.py` and
  replace the tables after every re-export" was not followed through the Easy/Medium/Hard split.

**C8. `delivery_points.py` is the single source of truth for the point set.**
- A: its own docstring — "One source of truth".
- B: it matches 1 of 4 scenes; all twelve points differ in the other three.
- Check: compare each scene's resolved anchor positions against the table (§6.3).
- **Verdict: true for `Possible game scene` only.** `MEASURED`. Consequence:
  `order_distances.py` output is valid for that scene and for no other, which is why
  `scene_physics.py jobs` exists.

**C9. `ZoneDetection.checkInterval`.**
- A: `ZoneDetection.cs:31` defaults it to `0f`, with the tooltip "0 means every frame, which stops a
  fast car clipping through a small radius between checks" (`READ`).
- B: Hard serializes `checkInterval: 0.1` (`MEASURED`).
- Check: dump the scene's `ZoneDetection` component fields by script guid.
- **Verdict: the scene overrides the code's own stated rationale.** `MEASURED`. Group 3 owns the
  consequence; recorded here because the zone radius is being compared against street width in §5.5.

**C10. "Easy.unity and Medium.unity have no knockable props or surface tags."**
- Check: count both script guids in all four `<ref>` blobs.
- **Verdict: confirmed, zero of each in both.** `MEASURED`.

---

## Open questions

Every `UNKNOWN` raised above, with the one check that settles it.

1. **Which collider holds the car up, per scene.** `Floor` top at y = 0.00, the uncollided road
   visuals at y ≈ 0.40, or the negative-scaled `Bastrop-blocks-combos.001` at pivot y ≈ 0.40 (Hard),
   0.04 (Medium), 0.00 (Easy). **Check:** one downward `Physics.Raycast` from the car's spawn, or a
   play-mode drop test, logging `hit.collider.name` and `hit.point.y`. This single check also settles
   C6, the 0.40 m visual offset in §4.5, and whether any `SurfaceType` tag is ever read (§4.6).
2. **Whether Easy has any drivable ground.** Its only candidate is one triple-negative-scaled
   non-convex MeshCollider. **Check:** as above, run in Easy.
3. **Whether negative scale breaks the `Bastrop` and `CityProps` colliders.** `KnockablePropBaker.cs`
   asserts PhysX cannot run a Rigidbody under such a chain and that `BoxCollider` forces negative
   scale positive with "unexpected collision geometry" (`READ` — `:17-33`); it says nothing about a
   static non-convex MeshCollider. **Check:** play-mode observation, or a `Physics.OverlapBox` probe
   inside the slab's derived footprint.
4. **Speed bump height.** **Check:** fetch `Assets/Aside/Art/FastMesh/Meshs/Road/SpeedBump.fbx`
   from `origin/main` through LFS and run `fbx_mesh_bounds.py` on it. (Do not set
   `GIT_LFS_SKIP_SMUDGE=1`.)
5. **Whether the footbridges are drivable.** 11.49 m tall, pivot y 0.39, no ramp found.
   **Check:** play-mode, or `fbx_mesh_bounds.py --json` plus a gradient comparison against the
   suspension travel in `VehicleTuning`.
6. **Easy's street widths.** `find_roads.py` cannot run. **Check:** restore a `Floor` object to Easy
   or give `find_roads.py` explicit bounds, then re-run; the 54 kerb meshes are the same ones the
   README's figures came from, at the same import scale.
7. **Whether the road slab's top face is at 0.400 or 0.502.** The slab is 0.204 m thick with its
   pivot at world y 0.40; whether the pivot is centred or on the top face is not in the printed
   table. **Check:** `fbx_mesh_bounds.py --json` and read `min`/`max` for `road_base`.
8. **Where "8.8 m to 25.5 m" came from.** See C3. **Check:** the command and scene that produced it.

---

## Commands index

`<ref>` = `.agents/tasks/design-overview-2026-10-14/ref/`. Every figure in this file comes from one of
these, or from a `path:line` citation.

```
python docs/tooling/scene-tools/order_distances.py 50
python docs/tooling/scene-tools/find_roads.py "<ref>/Hard.unity"
python docs/tooling/scene-tools/find_roads.py "<ref>/Medium.unity"
python docs/tooling/scene-tools/find_roads.py "<ref>/Easy.unity"
python docs/tooling/scene-tools/find_roads.py "<ref>/Possible game scene.unity"
python docs/tooling/scene-tools/scene_points.py "<ref>/Hard.unity" "^(City|Floor|Roads_Parent|CityProps|Car|Border|Delivery Points)$"
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Hard.unity" --grid=2.0 --tiles=^road_
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Hard.unity" --grid=1.0 --road=^road_ --pitch=8.18
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Medium.unity" --grid=8.0 --tiles="road"
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Medium.unity" --grid=1.0 --road="road" --pitch=13.82
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Easy.unity" --grid=2.0 --tiles=^SideWalk
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Possible game scene.unity" --grid=4.0
python docs/tooling/scene-tools/fbx_mesh_bounds.py "Assets/Aside/Art/Blender/sehirlogo_01.fbx"
python docs/tooling/scene-tools/fbx_mesh_bounds.py "Assets/Aside/Art/Toon City Pack/Models/Infrastructure/Roads-Streets-Highways/two-lane-linear-road-piece.fbx"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" owners "^(road_|Roads|Floor|Plane|SideWalk|Border)"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" detail "^(Roads_Parent|Floor|CityProps|City)$"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" detail "^(road_|Plane)" --with-colliders
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" detail "SpeedBump|Bump"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" extent "^road_"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" reach -5
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" script 5d8c1f2a3b4e4c6d9a7b8e0f1c2d3e4f
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" script 95c5eae1aeacb5341adbe50a1c9f4a67
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" script 6c6239d78dc6b374daa7df0491bd32e7
python docs/tooling/scene-tools/scene_physics.py "<ref>/Medium.unity" owners "road"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Medium.unity" extent "road"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Easy.unity" owners "Floor|Ground|Road|Cube|Plane|SideWalk|Generated|VehicleLab|Border"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Easy.unity" reach -5
python docs/tooling/scene-tools/scene_physics.py "<ref>/Easy.unity" detail "^(Cube|Building One|Start line|StartPoint|Generated)"
python docs/tooling/scene-tools/scene_physics.py "<ref>/Border.prefab" owners
python docs/tooling/scene-tools/scene_physics.py "<ref>/Border.prefab" extent "."
python docs/tooling/scene-tools/scene_physics.py "<ref>/<scene>.unity" extent "."
python docs/tooling/scene-tools/scene_physics.py "<ref>/<scene>.unity" jobs 50
python docs/tooling/scene-tools/scene_physics.py "<ref>/<scene>.unity" detail "Bastrop"
git show "origin/main:ProjectSettings/EditorBuildSettings.asset"
git diff --stat HEAD origin/main -- "Assets/Project/Scenes/Possible game scene.unity"
git grep -l be9b112dbd8a0d241ac4b804016e1a9a origin/main
```

Read-only baseline re-proved after the write: `python docs/tooling/compile_check.py` →
`RUNTIME 342 references, 80 scripts … errors 0, warnings 0` / `EDITOR 360 references … errors 0,
warnings 0` / `Compiles clean.`, exit 0; `python -B -m pytest -p no:cacheprovider
docs/tooling/repo-checks` → `34 passed`; `git status --short -- Assets ProjectSettings` shows only
`M ProjectSettings/ProjectSettings.asset`, which was already modified in the working tree before
this item began and was not touched by it.

Component and null-mesh tallies used `scene_points.parse` directly; the exact one-liners are in the
session log for this item and reduce to: count `--- !u!<class> &` document headers per class, and for
each class-64 document compare `m_Mesh` against the literal `{fileID: 0}`.
