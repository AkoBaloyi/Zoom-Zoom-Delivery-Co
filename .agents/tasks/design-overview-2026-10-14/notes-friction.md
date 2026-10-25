# Group 5 — Known technical & design friction

Investigation scope: question group 5, "are there any known bugs with physics, collisions or
spawning that a playtester might realistically encounter?" Deliberately excluded as already known
to the developer: UI canvas sorting, and the incomplete Controls screen. Neither is re-reported
except where a *new* consequence turned up.

Read-only investigation. No game code, scene, asset, tuning value or git state was changed. Unity
was not opened. One new read-only helper script was added
(`docs/tooling/scene-tools/serialization_drift.py`) because the drift finding below has to be
mechanically reproducible, not grep-by-hand.

## How to read the labels

| Label | Means |
| --- | --- |
| `READ` | Literal value sitting in a file, with `path:line` |
| `MEASURED` | Value produced by an instrument run — `PlaytestLogger`, or a tooling script whose exact command is given |
| `DERIVED` | Arithmetic on `READ`/`MEASURED` values, inputs named |
| `INFERRED` | Read of code control flow. Not observed at runtime |
| `UNKNOWN` | Not establishable without running the game or opening the editor. Carries the single check that would settle it |

Trees: `origin/main` is authoritative per plan §0.2. Paths written as `<ref>/…` are `origin/main`
blobs extracted to `.agents/tasks/design-overview-2026-10-14/ref/`.

`MEASURED` — `git diff --name-status HEAD origin/main -- Assets ProjectSettings`: of the paths cited
below, **only two differ between the two trees** — `Assets/Project/Scenes/Possible game scene.unity`
(M) and `Assets/Project/Scripts/Orders/OrderHUD.cs` (M), plus
`Assets/Project/Scripts/UI/Main Menu Manager.cs` (deleted, replaced by `MainMenuManager.cs`). Every
other script, tuning asset, `ProjectSettings/*.asset` and `VehicleLab.unity` cited here is
byte-identical in both trees and is read in place. Where a `Possible game scene.unity` line number
appears without a `<ref>/` prefix it is the **working-tree** copy, and §1.3 and §7.2 say explicitly
what `origin/main`'s copy does instead.

---

## 1. Serialization drift, and the one case that changes the game

This is the biggest finding in the group, and it is not cosmetic. **The order time limit the game
actually runs is 45 s, not the 50 s written in the tuning asset**, and the cause is a YAML
indentation fault that silently discards the back half of that asset.

### 1.1 `OrderTuning_Balanced.asset` is not valid YAML

`READ` — `whyThisSetup:` sits at **column 1** in
`Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset:16`, while every other key in the
document is indented two spaces (`profileName` at `:15`, `spawnInterval` at `:31`). At indent 0 the
key closes the `MonoBehaviour:` block mapping opened at `:4`; the quoted scalar then runs to `:30`;
and `:31` resumes two-space indentation, which no longer belongs to any open mapping.

`MEASURED` — a conformant parser rejects the file at exactly that point:

```
python -c "import io,yaml; L=io.open('Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset',encoding='utf-8').read().split('\n'); yaml.safe_load('\n'.join(L[3:]))"
→ ParserError: while parsing a block mapping … expected <block end>, but found
  '<block mapping start>' in "<unicode string>", line 28, column 3  (= file line 31, spawnInterval)
```

The same test on the sibling `Assets/Project/Scripts/Vehicle/Tuning/Tuning_Balanced.asset` parses
cleanly and yields 118 keys, so the test is sound and the fault is specific to this one asset.

Everything from `:31` down is in the unreachable region: `spawnInterval: 12`, `maxActiveOrders: 3`,
**`orderTimeLimit: 50`**, `baseDeliveryValue: 10`, `latePenaltyPerSecond: 1`, and the five stale
multi-leg keys. Of these, only `orderTimeLimit` differs from its C# initialiser, so it is the only
one that can be used as a discriminator — the rest coincide with the defaults
(`OrderTuning.cs:24` `spawnInterval = 12f`, `:27` `maxActiveOrders = 3`, `:35`
`baseDeliveryValue = 10f`).

### 1.2 The runtime limit is 45 s — discriminating check, closed

`READ` — `OrderTuning.cs:31`: `public float orderTimeLimit = 45f;`. `READ` —
`OrderTuning_Balanced.asset:33`: `orderTimeLimit: 50`.

`READ` — all four scenes point their `OrderManager.tuning` at this one asset, by GUID
`d45a48eeca7d4364896ca1448c017ed2` (= `OrderTuning_Balanced.asset.meta`):
`Possible game scene.unity:1595`, `<ref>/Hard.unity:2252`, `<ref>/Medium.unity:54287`,
`<ref>/Easy.unity:5519`. It is the only `OrderTuning` asset in the project.

The check: two logged shifts have `delivered=0`, i.e. a player who never interacted with the queue.
For such a player the whole late sequence is deterministic from `spawnInterval`, `maxActiveOrders`
and `orderTimeLimit`, because `OrderManager.TrySpawn` decrements the spawn clock only while under
the active cap, and `Order.Tick` is a plain countdown. Simulating that loop against the two rows:

| `orderTimeLimit` | late at 221.4 s | late at 268.7 s |
| --- | --- | --- |
| 45 s (C# default) | **10** | **12** |
| 50 s (asset value) | 9 | 12 |
| measured rows | **10** | **12** |

`MEASURED` — the rows are `Game Build/PlaytestLogs/shift_log.csv:4` (`play_seconds 268.7`,
`late 12`) and `:5` (`play_seconds 221.4`, `late 10`); both carry `delivered 0`.
`DERIVED` — the simulation, from `OrderManager.cs:188-197` (`TrySpawn`), `:268-275`
(`TickAllOrders`), `Order.cs:101-111` (`Tick`), `ShiftTimer.cs:19` (`shiftDurationSeconds = 300f`).
Re-ran at 30, 50, 60, 90, 120 and 144 fps: the counts are identical at every rate, so the match is
not a frame-rate coincidence.

**Verdict: `orderTimeLimit` in play is 45 s.** The asset's 50 never reaches the runtime. Every
document that quotes 50 s — including this asset's own `whyThisSetup` rationale at `:16-30`, which
argues 50 s specifically because 45 s left the longest job about 3 s short — describes a game that
is not the one being played.

Corroboration, not evidence: the QA note in the original brief records a tester hitting a "45s
deadline vs 50s design spec". That document is untrusted per plan §0.3; it agrees with the code, so
the code stands and the QA note happens to be right here.

### 1.3 Stale keys, mechanically enumerated

`MEASURED` — `python docs/tooling/scene-tools/serialization_drift.py "Assets/Project/Scenes"
"Assets/Project/Scripts/Orders" "Assets/Project/Scripts/Vehicle/Tuning"
".agents/tasks/design-overview-2026-10-14/ref"` → 15 files checked, 59 stale keys.

**Sort the 59 into three piles before reading anything into them.** This matters: most of the
suspected drift in the brief turns out to live in the *working tree only* and to be already fixed on
`origin/main`, which is the authoritative tree.

**Pile 1 — genuinely stale on `origin/main`, i.e. a real defect in the shared game:**

| Key | Where | Current C# | What runs instead |
| --- | --- | --- | --- |
| `orderTimeLimit: 50` | `OrderTuning_Balanced.asset:33` (identical in both trees) | present, `= 45f` | 45 s — see §1.2. Not "stale" in the renamed sense; unreachable because the YAML above it is broken |
| `enableMultiLegOrders`, `multiLegChance`, `minTransferLegs`, `maxTransferLegs`, `extraTimePerTransferLeg` | `OrderTuning_Balanced.asset:36-40` | **absent** from `OrderTuning.cs` | nothing. Multi-leg orders do not exist in code; the asset describes a feature that was removed |
| `patchKinds` | every scene, both trees (`Possible game scene.unity:2210` locally, `:2269` on `origin/main`; `<ref>/Hard.unity:3182`, `<ref>/Medium.unity:77859`, `<ref>/Easy.unity:7528`) | absent from `VehicleLabBuilder.cs` | nothing |
| `headerFontSize: 29`, `orderLineSlots: 6`, `normalColour`, `warningColour`, `carriedColour`, `panelColour`, `warningThreshold` | `VehicleLab.unity:9710-9716` — byte-identical in both trees | **absent** from `OrderHUD.cs` in *both* trees (checked `git show origin/main:…/OrderHUD.cs`) | nothing. `VehicleLab` is the measurement scene and is `enabled: 0` in build settings, so this is low-stakes, but it is real |

**Pile 2 — stale in the working tree, already cleaned on `origin/main`.** Three of the five keys the
brief named land here (`hardBrakeDecelThreshold`, `headerFontSize`, `orderLineSlots`); they are the
local branch's problem, not the team's. The other two it named, `whyThisSetup` and
`enableMultiLegOrders`, are in pile 1 and are real:

| Key | Working tree | `origin/main` | What runs on `origin/main` |
| --- | --- | --- | --- |
| `hardBrakeDecelThreshold: 18` | `Possible game scene.unity:808` | **absent**; replaced by `hardBrakeMultiplier: 1.25` (`:804`) and `fallbackHardBrakeDecelThreshold: 27` (`:805`) | `brakeDeceleration × max(1.01, 1.25)` = **27.5 m/s²** (`CargoCondition.cs:87-95`) — and the same number applies in the working tree, because there the key is stale and the multiplier falls back to its `1.25` initialiser |
| `transferRadius: 6` | `Possible game scene.unity:789` | **absent** | nothing; the field does not exist in `ZoneDetection.cs` |
| `headerFontSize`, `orderLineSlots`, `normalColour`, `pickupColour`, `dropOffColour`, `transferColour`, `urgentColour`, `panelColour` on the `OrderHUD` block | `Possible game scene.unity:2075-2082` | **absent** | colour comes from the static `OrderPalette` (`OrderHUD.cs:112,136,200,223,288`) |
| `urgentBelowSeconds: 8` on the `OrderHUD` block | `Possible game scene.unity:2083` | **absent**; the block now reads `urgentShiftSeconds: 30` (`:2142`) | **30 s**, not 8. The HUD turns the clock urgent at 30 s. (The two `urgentBelowSeconds: 8` entries that *do* survive on `origin/main`, at `:325` and `:2118`, belong to `OrderBeacon` and `DestinationMarker`, where that field genuinely exists — `OrderBeacon.cs:57`. Not drift) |

**Pile 3 — not stale at all, an artefact of comparing across branches.** `ticketScale`,
`mainMenuSceneName`, `playerProgression`, `xpColour` in the three new scenes are all declared on
`origin/main` (`ShiftResults.cs:58,62,75`, `OrderTicketStrip.cs:41`, `ButtonsUI.cs:11`). They flag
only because the local feature branch carries older copies of those four scripts. **Anyone
re-running the checker must match the scene's tree to the script's tree.**

What Unity does with a stale key: it is dropped on load, and it survives in the file until that
object is next re-serialized. Proof of both halves, inside this repo: the three new scenes *and*
`origin/main`'s `Possible game scene` were saved after the `CargoCondition` rename and carry the
current names (`<ref>/Hard.unity:1368-1369`, `<ref>/Medium.unity:39234-39235`,
`<ref>/Easy.unity:3340-3341`), while the working tree's older copy still carries the dead ones. A
save clears drift; nothing else does. `READ`.

`whyThisSetup` itself: the field exists (`VehicleTuning.cs:37`, read by `VehicleMeasurement.cs:975`)
and the three `Tuning_*.asset` files all carry it correctly indented at `:16`. Only
`OrderTuning_Balanced.asset` has it at column 0 — and `OrderTuning` has no `whyThisSetup` field at
all, so even correctly indented it would deserialize nowhere. `INFERRED` (code path, not observed at
runtime): the order-tuning rationale is a comment in a file no code reads, written in a position
that also breaks the keys below it.

### 1.4 Values a designer would get wrong by reading the file

These are serialized values that *do* load, but that differ from the C# default a reader would
assume. Listing them because the same trap applies in reverse.

- `READ` — `enableCondition: 1` in **all four** scenes (`Possible game scene.unity:805`,
  `<ref>/Hard.unity:1364`, `<ref>/Medium.unity:39230`, `<ref>/Easy.unity:3336`). The C# default is
  `false` (`CargoCondition.cs:42`) and the class header says in capitals "OFF BY DEFAULT … exactly
  Milestone 1 behaviour" (`CargoCondition.cs:15-19`). Cargo condition is **on** in every shipped
  scene, so delivery payouts are scaled by condition, not paid at full value.
- `READ` — `checkInterval: 0.1` in all four scenes (`Possible game scene.unity:790`,
  `<ref>/Hard.unity:1349`, `<ref>/Medium.unity:39215`, `<ref>/Easy.unity:3321`). The C# default is
  `0f` and its tooltip says 0 "means every frame, which stops a fast car clipping through a small
  radius between checks" (`ZoneDetection.cs:31-33`). The documented protection is switched off in
  every scene. `DERIVED`: at the boost ceiling of 40 m/s (`VehicleTuning.cs:191`) the car covers
  4.0 m per check against a 6 m radius (12 m chord), so a zone is still sampled ~3 times on a
  straight pass — the risk is small, but a delivery can read as up to 100 ms late.
- `READ` — cargo capacity differs by scene: `capacity: 1` in `Possible game scene.unity:1890`
  versus `capacity: 2` in `<ref>/Hard.unity:2619`, `<ref>/Medium.unity:62221`,
  `<ref>/Easy.unity:6507`. The scene all the telemetry came from carries **one** slot; the three
  scenes on `origin/main` carry two. Any conclusion drawn from the logs about "wanted more slots"
  was drawn against the one-slot build.
- `READ` — `hardBrakeDamage: 12` in `Possible game scene.unity:809` versus `6` in the three new
  scenes. Same split.

---

## 2. Spawning: reachability, coincidence, and the car's own spawn

### 2.1 Structural integrity — clean

`MEASURED` — `python docs/tooling/scene-tools/validate_scene.py <scene>` on all seven scenes
(`Possible game scene` in both trees, `<ref>/Hard`, `<ref>/Medium`, `<ref>/Easy`, `MainMenu`,
`VehicleLab`) exits 0 for every one: no duplicate anchors, every local `fileID` resolves, every
transform parented or registered as a root, parent/child links reciprocal. No structural fault
underlies any spawning problem.

One caveat that applies to every scene-file measurement in §2 and §3, established in §7.2: these
counts do not see inside `PrefabInstance` entries. Each new scene has one or two, they resolve to
`Group 15 Greybox real one 1.prefab`, `Border.prefab` and `Canvas (2).prefab`, and all three were
audited directly — none contains a Rigidbody and none contains a negatively scaled collider.

### 2.2 Every delivery point is inside the drivable extent

`READ` — all four gameplay scenes assign six real `Pickup *` and six real `DropOff *` transforms
to `OrderManager` (e.g. `Possible game scene.unity:1596-1609`), so the real-points branch runs
(`OrderManager.cs:86-101`) and the `floorBounds` scatter and the 60 m ring are both dead code in
play. `floorBounds` is assigned anyway in three scenes and `{fileID: 0}` in `<ref>/Easy.unity:5534`.

`MEASURED` — world positions resolved through the full transform hierarchy (so the `City` root's
offset and 2.0439 scale are accounted for, per plan §0.5). All twelve points in all four scenes sit
at **y = 0.00 exactly**, and all twelve are inside that scene's `Floor` footprint where one exists:

| Scene | `Floor` world top face | footprint x | footprint z | points x range | points z range |
| --- | --- | --- | --- | --- | --- |
| `Possible game scene` | y = 0.000 | −252.0 … 252.0 | −154.7 … 400.0 | −220.1 … 83.7 | 7.0 … 344.8 |
| `<ref>/Hard` | y = 0.000 | −291.3 … 288.1 | −154.7 … 447.1 | −245.4 … 254.1 | −111.5 … 270.4 |
| `<ref>/Medium` | y = 0.000 | −291.3 … 288.1 | −154.7 … 447.1 | within | within |
| `<ref>/Easy` | **no `Floor` object at all** | — | — | — | — |

Floor figures `DERIVED` from `READ` values: `Floor` at world y = −1.000 with world scale y = 2.000
and a unit `BoxCollider` (`m_Size: (1,1,1)`, `m_Center: (0,0,0)`) → top face y = −1 + 1 = 0.
`Possible game scene.unity` tfm 1551314016, same fileID in Hard/Medium.

**No unreachable point found, in any scene.** `INFERRED` for "inside a building footprint": the
points are all on y = 0 inside the floor plate, but whether a given point sits under a building
*mesh* cannot be settled from transforms alone. `UNKNOWN` — the settling check is a downward
raycast from each point in play mode, or `fbx_mesh_bounds.py` against the building meshes, neither
of which this investigation can run against a live scene.

### 2.3 Hard has one degenerate pickup/drop-off pair, and it is self-delivering

`MEASURED` — all 36 pickup × drop-off distances per scene, from resolved world positions:

| Scene | closest pair | pairs < 12 m (the two 6 m zones overlap) | longest pair |
| --- | --- | --- | --- |
| `Possible game scene` | 93.68 m | 0 | 386.42 m |
| `<ref>/Easy` | 83.80 m | 0 | 417.64 m |
| `<ref>/Medium` | 44.32 m | 0 | 425.65 m |
| `<ref>/Hard` | **9.14 m** (Pickup 01 South West Gate → DropOff 04 Centre North) | **1** | 499.63 m |

`READ` — `pickupRadius: 6` and `dropOffRadius: 6` in every scene
(`Possible game scene.unity:787-788` and siblings; code defaults match, `ZoneDetection.cs:21-22`).
Two 6 m spheres 9.14 m apart intersect.

`INFERRED` (code path, not observed at runtime) — the consequence. `ZoneDetection.Update` calls
`CheckPickups()` then `CheckDeliveries()` in the same tick (`ZoneDetection.cs:59-60`), and
`CheckDeliveries` iterates `cargoSystem.CarriedOrders`, which by then contains the order collected
one statement earlier. So a car standing in the overlap collects and delivers that order in a single
tick. `OrderManager.PickIndexAvoidingCollisions` guards only against a *new* pickup landing on
another active order's pickup, and a new drop-off landing on another active order's drop-off
(`OrderManager.cs:202-203`, threshold 0.5 m at `:238`) — it never compares the pickup it just chose
against the drop-off it is about to choose for the *same* order. Both indices are uniform over six
(`:242`), so the 01→04 pair comes up about **1 spawn in 36** in `Hard`. `DERIVED`: its
`DesignedDistance` is 9.14 m, so it pays `10 + 9.14 × 0.1 = 10.91` before condition scaling
(`OrderTuning.cs:50`), against a mean job of several hundred metres.

**Pickup and drop-off cannot be exactly coincident in any current scene** (minimum 9.14 m), so the
zero-distance case does not arise — but the overlap case does, in `Hard`, and it is a free delivery.

### 2.4 The car does not spawn clipped

`DERIVED` from `READ` values — the Car's `BoxCollider` is `m_Size: (1.8, 1.0, 4.2)` with
`m_Center: (0, 0.2, 0)` on a transform at world y = 0.800 and unit scale, so its lowest face is at
**y = 0.500**. The `Floor` top face is y = 0.000. Clearance at spawn: **0.5 m of air**, in all four
scenes (`Possible game scene.unity` Car tfm 592668311; identical collider in Hard/Medium/Easy, Car
at (−1.74, 0.80, 0.00) in the three new scenes, (0, 0.80, 0) in `Possible game scene`). With
`suspensionRestLength: 0.6` and `wheelRadius: 0.36` (`Tuning_Balanced.asset:43,41`) that is a
sensible ride height, not a clip.

The qualifier: in `Hard` and `Medium` the city is parented under a root at world y = **0.400**
(`City` tfm 8371924365102370620 → world pos (−6.39, 0.400, 139.24); `Roads_Parent` → (295.58,
0.400, −158.13)), i.e. the roads sit about 0.4 m above the floor plate, leaving roughly 0.1 m under
the car's collider at spawn. `UNKNOWN` — the exact road top face needs the FBX mesh extent, not the
transform; settle it with `docs/tooling/scene-tools/fbx_mesh_bounds.py` against the road mesh, or a
single downward raycast in play mode.

A related consequence of that 0.4 m offset: `OrderBeacon` places its ground disc only
`groundDiscYOffset = 0.05` m above the order point (`OrderBeacon.cs:48,160`), and every order point
is at y = 0. In `Hard`/`Medium` that puts the disc ~0.35 m **below** the road surface. The 14 m
beam above it (`OrderBeacon.cs:43`) would still be visible. `INFERRED`; settle by looking at one
beacon in the editor.

---

## 3. Collisions and the negative-scale transform chain

### 3.1 The Rigidbody hazard is closed — verified, with one correction to the premise

The premise in the brief is that knockable props were pulled out of the city hierarchy because
imported group nodes carry scales like 267 and −426 and PhysX cannot run a Rigidbody under negative
non-uniform parent scale. The reparenting did happen and it worked. The stated mechanism, however,
is not what the geometry shows.

`MEASURED` — every `Rigidbody` in every scene, with its full ancestor chain:

| Scene | Rigidbodies | with negative or non-uniform **ancestor** scale |
| --- | --- | --- |
| `Possible game scene` (both trees) | 1 (the Car, at the scene root) | 0 |
| `<ref>/Hard` | 34 (Car + 33 props) | **0** |
| `<ref>/Medium` | 1 | 0 |
| `<ref>/Easy` | 1 | 0 |

All 33 props in `Hard` sit one level under `City` at uniform +2.0439, with positive local scales
(90, 112.531 or 125). Chain printed for each, e.g. `mobile_toilet.013 < City`. **Nothing in any
scene has a Rigidbody under a bad chain.** The fix from `7f357c7` "Knockable props, third attempt:
pull each prop out of the city's broken transform chain" (2026-10-13), `457d1f3` and `c03c220` is
in place and holds. The three props sampled in detail also carry a **disabled** imported
`MeshCollider` alongside the baker's enabled `BoxCollider` (`m_Enabled: 0`, `m_Convex: 0` on
`trashcontainer01.006` and `picnictable01.005` in `<ref>/Hard.unity`), which is the right outcome: a
non-convex mesh collider on a non-kinematic Rigidbody is the one combination PhysX refuses outright,
and the baker switched them off rather than leaving them to error at runtime. `READ`.

The correction: `MEASURED` — the negative scales in the city hierarchy **cancel**. `CityProps` and
`park_props` each have local scale −426.4630 under `City` at +2.0439, giving world scale −871.65 —
but their children carry negative local scales too, so the product comes out positive:

```
trashcontainer01_openEmpty  local (-0.2931,-0.1622,-0.2931)
  CityProps                 local (-426.4630, -426.4630, -426.4630)
    City                    local (2.0439, 2.0439, 2.0439)
  → world scale (255.49, 141.38, 255.49)     positive
```

`MEASURED` — of 1312 colliders under `City` in `<ref>/Hard`, **1311 resolve to a positive world
scale**. So "props are under negative scale" is true of the group nodes and false of the colliders.
The `KnockableProp.originalParent` tooltip (`KnockableProp.cs:26-29`) states the PhysX rule as the
reason for the reparenting; the measurement does not support negative scale having reached the prop
colliders. The reparenting is still correct — a prop at +255 world scale with a 90–125 local scale
is a nightmare to hand-tune, and the baker's undo path needs the recorded parent — but the hazard
note in the code overstates what was wrong. Flagged for §Contradictions.

### 3.2 The one collider that really is negatively scaled

`READ` — a single object, `Bastrop-blocks-combos.001`, carries an **enabled, non-convex
`MeshCollider` under a negative world scale**, in all three new scenes and nowhere else:

| Scene | world position | world scale | collider |
| --- | --- | --- | --- |
| `<ref>/Hard` | (−6.39, 0.398, 139.16) | (−65155.36, −65155.36, −65155.36) — negative, uniform | `MeshCollider` `m_Enabled: 1`, `m_Convex: 0` |
| `<ref>/Medium` | — | (−74375.08, −73704.80, −65913.79) — negative **and** non-uniform | same |
| `<ref>/Easy` | (−1.51, 0.000, 136.64) | (−69773.65, −72439.59, −53445.06) — negative **and** non-uniform | same |

In `Hard` it is a child of `City`; in `Medium` and `Easy` it is a scene **root**, so there is no
parent transform that could cancel the sign.

`INFERRED` (code path and geometry, not observed at runtime) — the mechanism. A negative scale
mirrors the mesh, which reverses triangle winding, which flips the collision normal PhysX computes
for each face. A non-convex (triangle-mesh) collider is one-sided, so a flipped surface is solid
from the wrong side: the car can pass into it where it should be blocked, and be blocked where it
should be open. This is the mechanism the repo itself names for the city chain
(`KnockableProp.cs:27-29`), applied to the one collider where the sign does not cancel. In `Medium`
and `Easy` the scale is negative *and* non-uniform, which is the doubly-unsupported case.

`UNKNOWN` — whether the car actually falls through it, and whether it is load-bearing drivable
surface or decoration. Single settling check: enter play mode in `Easy` and drive onto the object
(or, without play mode, open the scene and look at whether the MeshCollider gizmo encloses the
visible blocks). `Easy` is the scene where it matters most, because `Easy` has **no `Floor` object
at all** (§2.2) — there is no catch-all plate underneath to land on.

### 3.3 Can a prop be launched through the road, or into a delivery zone?

Two separate questions. Both answered as far as geometry allows.

**Through the road.** `READ` — every prop Rigidbody in `<ref>/Hard` has
`m_CollisionDetection: 0` (Discrete) and `m_Interpolate: 0`, e.g. `RoadBlocker04.002` (mass 25),
`trashcontainer01.006` (mass 60), `picnictable01.005` (mass 120). The Car, by contrast, has
`m_CollisionDetection: 2` (`Possible game scene.unity` Car Rigidbody). `READ` —
`Fixed Timestep: 0.02` (`ProjectSettings/TimeManager.asset:6`). `DERIVED` — a Discrete-mode body
can pass through a static collider in one step once it travels further than its own thickness in
that step. For `RoadBlocker04.002` the baked `BoxCollider` is `m_Size: (0.017258, 0.004204,
0.009454)` on world scale (230.00, 230.00, 230.00) → **3.97 × 0.967 × 2.17 m**, so the thin axis is
0.967 m and the threshold is 0.967 / 0.02 = **48.3 m/s**. For `trashcontainer01.006`
(`m_Size: (0.010709, 0.018157, 0.012937)` on (255.49, 141.38, 255.49)) the box is 2.74 × 2.57 ×
3.31 m, threshold ≈ 128 m/s.

`READ` — the Car's speed is capped: `_rb.maxLinearVelocity = highestIntendedSpeed * 2.5f`
(`VehicleController.cs:350`) = 100 m/s given `boostTopSpeed = 40f` (`VehicleTuning.cs:191`). **No
such cap is set on the props.** `INFERRED` — so the road blocker is the realistic candidate:
0.97 m thick, 25 kg, Discrete, uncapped, hit by a 900 kg car at up to 40 m/s. The heavier
2.5–3.3 m props are effectively safe.

`UNKNOWN` — whether it happens in practice. Single settling check: in play mode, ram a
`RoadBlocker04` at boost speed and watch whether it ends up under the road. The impulse the car
actually imparts is not readable from the files.

**Into a delivery zone.** `READ` — `Hard` is the only scene with knockable props (33 of them; the
other three scenes have one Rigidbody each, the Car). `MEASURED` — all 33 × 12 prop-to-order-point
distances from resolved world positions: the closest is **15.86 m** (`RoadBlocker04.001` →
`DropOff 06 South West Yard`), **0 props within 12 m**, 0 within 6 m. No prop starts in or near a
zone. `INFERRED` — a prop knocked
into a zone cannot *block* a delivery regardless, because detection is a pure distance test on the
car's transform (`ZoneDetection.cs:72,95`), not a trigger volume and not a raycast. A prop sitting
on the point would be driven over or pushed aside; it cannot gate the check. **So: a prop can
obstruct the approach physically, but it cannot block a zone logically.**

### 3.4 The road is one collider

`READ` — `Roads_Parent` in `<ref>/Hard` carries a single `MeshCollider`, `m_Enabled: 1`,
`m_Convex: 0`, at world scale 703.40 uniform positive. `MEASURED` — 1260 of `Hard`'s MeshColliders
are under `City` at uniform positive scale, 18 at non-uniform positive, plus 24 BoxColliders at
uniform and 9 non-uniform. Friction consequence: one collider is one surface tag for the entire
road network, and any hole in that single mesh is a hole in the world with only the `Floor` plate
0.4 m below to catch the car — and in `Easy`, nothing at all.

---

## 4. Camera look sensitivity

This is the most likely cause of the "camera shifts when moving the mouse even a little" report, and
the number is findable. The brief's prior suspicion that the mouse binding has no scale processor is
**wrong** — it has one.

`READ` — the live chain, all four values:

1. `VehicleControls.inputactions:501-505` — `CameraLook` binds `<Mouse>/delta` with
   `"processors": "ScaleVector2(x=0.06,y=0.06)"`. The gamepad binding at `:512-516` has no
   processor.
2. `Possible game scene.unity:598` — `VehicleInput.controlsAsset` is GUID
   `1a2b3c4d5e6f708192a3b4c5d6e7f810` = `VehicleControls.inputactions.meta`. So the asset's binding
   is what runs, **not** the code-built fallback in `VehicleInput.BuildFallbackControls`, which
   happens to carry the same 0.06 scale.
3. `ChaseCamera.cs:354-360` — `_yawOffset += look.x * Tuning.cameraSwivelSpeed * dt;` and
   `_pitchOffset = Mathf.Clamp(_pitchOffset - look.y * Tuning.cameraSwivelSpeed * dt, -35f, 55f);`
4. `cameraSwivelSpeed: 120` in **all three** profiles (`Tuning_Balanced.asset:211`,
   `Tuning_Grippy.asset:206`, `Tuning_Loose.asset:206`), and the scene's `VehicleController` uses
   `Tuning_Balanced` (`Possible game scene.unity:627`, GUID `…f811`). `ChaseCamera.tuningOverride`
   is `{fileID: 0}` (`:2257`), so the camera reads the car's profile.
5. `Possible game scene.unity:2258` — `ChaseCamera.input: {fileID: 592668306}` = the `VehicleInput`
   on the Car. Same in all three new scenes. **The swivel path is wired and live.**

### The numbers

`DERIVED` — degrees of yaw per pixel of mouse movement = `0.06 × 120 × dt`:

| Frame rate | degrees per pixel | 400 px sweep |
| --- | --- | --- |
| 30 fps | 0.24 | 96° |
| 60 fps | **0.12** | 48° |
| 144 fps | 0.05 | 20° |

`DERIVED` — the deadzone. The gate is `look.sqrMagnitude > 0.0001f` (`ChaseCamera.cs:356`), i.e.
magnitude > 0.01, i.e. a mouse delta greater than `0.01 / 0.06 = 0.167` pixels. **There is no
deadzone in any useful sense** — a single pixel of mouse jitter is 6× over the threshold.

Three separate faults, in order of how much they explain the complaint:

1. **Unit collision.** `cameraSwivelSpeed` is documented as "Degrees per second the player can
   swivel the camera by hand (right stick / mouse)" (`VehicleTuning.cs:628`). That is the correct
   unit for a stick, whose input saturates at 1.0. Mouse delta is a pixel count with no ceiling: a
   50 px flick in one frame reads as `look.x = 3.0`, i.e. three times full stick deflection, so the
   camera turns at 360°/s. One number cannot serve both devices.
2. **Frame-rate dependence.** Mouse delta is already a per-frame displacement, so multiplying by
   `dt` makes the same physical mouse movement produce ~5× more rotation at 30 fps than at 144 fps
   (table above). A stick, which is a rate, needs the `dt`; a mouse, which is a delta, does not.
3. **No deadzone, and no recentre while moving.** `_timeSinceSwivel` resets to 0 on any input over
   the 0.167 px gate (`ChaseCamera.cs:361`), and the auto-recentre only runs after
   `cameraSwivelRecentreDelay: 1.2` s of silence (`Tuning_Balanced.asset:212`,
   `ChaseCamera.cs:367-374`). A hand resting on a mouse keeps the camera permanently off-centre.
   Yaw is unclamped; only pitch is clamped, to [−35°, +55°] (`ChaseCamera.cs:359-360`).

One thing that is **not** wrong: the camera does not snap. The swivel offset feeds the *desired*
heading, which is then eased with `cameraRotationSmoothTime: 0.14` (`ChaseCamera.cs:157-158,
171-172`; `Tuning_Balanced.asset:210`). The complaint is about magnitude, not about abruptness.

### Is it configurable, and what to aim for

`READ` — not by the player, on either tree. An exhaustive search of `Assets/Project/Scripts` for
`sensitiv`, `Settings`, `Options` and `PlayerPrefs` finds no sensitivity field anywhere outside the
tuning ScriptableObject. On the working tree the only hit is a `settingsPanel` GameObject with
`OpenSettings`/`CloseSettings` and nothing inside it that the code touches
(`Assets/Project/Scripts/UI/Main Menu Manager.cs:6,14-22`). That file is **deleted on
`origin/main`** and replaced by `Assets/Project/Scripts/UI/MainMenuManager.cs` (66 lines), whose
whole public surface is `OpenDifficultyPanel`, `OpenControlsPanel`, `BackToMainMenu` and `ExitGame`
— **no settings panel at all**. So the shared build has no place to put a sensitivity slider.

The only exposed look preference anywhere is `invertCameraLookY` on `VehicleInput`
(`VehicleInput.cs:56`, serialized `0` at `Possible game scene.unity:601`).

Recommended range, as a recommendation and not a measurement: **0.03–0.05 degrees per pixel** at
the player's actual frame rate, which is roughly a third of today's 60 fps figure and in the region
most driving games land for an auxiliary look-around. Three ways to get there, cheapest first:

- Leave the code alone and drop the processor to `ScaleVector2(x=0.02,y=0.02)` →
  0.04 deg/px at 60 fps. Still frame-rate dependent.
- Better: split the two devices. Drop the `* dt` on the mouse path and multiply by an explicit
  degrees-per-pixel constant, keeping `cameraSwivelSpeed × dt` for the stick. Removes fault 2
  entirely and makes fault 1 impossible to reintroduce.
- Add a real deadzone. The current 0.167 px threshold should be more like 1–2 px, or a
  `StickDeadzone` processor on the gamepad binding and nothing on the mouse (where a deadzone in
  pixels is the right shape).

`UNKNOWN` — what the tester's frame rate was, which decides which row of the table they felt.
Single settling check: a frame-rate reading from the same machine and build.

---

## 5. `PlaytestLogger` writes `shift_seconds=0` on the early-exit path

Confirmed, with the branch identified and the arithmetic closed.

`MEASURED` — `Game Build/PlaytestLogs/shift_log.csv` (UTF-8 BOM; decoded `utf-8-sig`), all four
data rows:

| line | timestamp | scene | shift_seconds | play_seconds | delivered | late | value |
| --- | --- | --- | --- | --- | --- | --- | --- |
| :2 | 2026-10-14 09:09:12 | Possible game scene | 300 | 163.5 | 4 | 4 | 158 |
| :3 | 2026-10-14 09:17:49 | Possible game scene | 300 | 300.0 | 6 | 10 | 216 |
| :4 | 2026-10-14 09:29:20 | Possible game scene | **0** | 268.7 | 0 | 12 | 0 |
| :5 | 2026-10-14 09:38:11 | Possible game scene | **0** | 221.4 | 0 | 10 | 0 |

plus `PlaytestLogs/shift_log.csv:2` — 2026-10-12 13:33:39, 300 / 300.0 / 9 / 7 / 61.

`READ` — the write path. `WriteRow` computes
`float shiftSeconds = shiftTimer != null ? ShiftLengthSeconds() : 0f;`
(`PlaytestLogger.cs:244`), and `ShiftLengthSeconds` returns
`Mathf.Round(Time.time - _shiftStartTime + shiftTimer.TimeRemaining)` (`:302`). `playSeconds` is
`Time.time - _shiftStartTime` (`:243`) — the *same* first term.

`DERIVED` — the branch, by elimination. `ShiftTimer.TimeRemaining` is never negative outside
`Update`: it is clamped to 0 the moment it crosses (`ShiftTimer.cs:61-67`). So
`Mathf.Round(268.7 + TimeRemaining) ≥ 269` for any reachable `TimeRemaining`, and can never be 0.
The only way `shift_seconds` is 0 while `play_seconds` is 268.7 is the **false arm of the ternary at
`:244`** — `shiftTimer != null` evaluating false. Which it does when the `ShiftTimer`'s native
object has already been destroyed: Unity's `Object` equality reports a destroyed component as
null.

`INFERRED` (code path, not observed at runtime) — how that is reached. The row is written from
`OnDestroy`: `if (_shiftRunning && !_rowWritten) WriteRow();` (`PlaytestLogger.cs:134`). On
application quit or on leaving Play mode, `OnDestroy` order across objects is not guaranteed. If the
`ShiftTimer` goes first, the logger's reference is already dead and `shift_seconds` comes out 0.
Row `:2` is the same early-exit (`play 163.5 < shift 300`) with the `ShiftTimer` still alive, which
is exactly the non-determinism you would expect, and is good evidence for this reading rather than
any other.

**Consequences.** Two of five logged shifts are partial records, and the comment above the call
describes the opposite of what happens: "A shift cut short by stopping Play mode still gets its row,
flagged by a shorter play_seconds than shift_seconds" (`PlaytestLogger.cs:131-133`). On this branch
the flag is inverted — `play_seconds` 268.7 is *longer* than `shift_seconds` 0 — so an analyst
filtering on "play < shift" to find truncated sessions will miss precisely these two rows and treat
them as complete 0-delivery shifts. Those are also the only two rows with `delivered = 0`, so they
are the rows most likely to be read as evidence about onboarding.

Worth noting what is **not** damaged: `delivered`, `late`, `value`, `wall_hits` and the rest are
accumulated in the logger's own managed fields and are unaffected by the dead `ShiftTimer`
reference. Only `shift_seconds` is lost. That is what makes §1.2's use of these rows safe.

A smaller related defect, same file: `ShiftLengthSeconds` exists only because `ShiftTimer` does not
expose its configured length (`PlaytestLogger.cs:297-303`), yet
`ShiftTimer.shiftDurationSeconds` is serialized in plain sight in every scene
(`Possible game scene.unity:1644`, `<ref>/Hard.unity:2301`, `<ref>/Medium.unity:54336`,
`<ref>/Easy.unity:5568`, all `300`). A public getter would make the whole reconstruction — and this
bug — unnecessary.

---

## 6. Audio: confirmed absent

`READ` (absence established by exhaustive search):

- **No audio file of any kind under `Assets/`.** `Get-ChildItem -Recurse Assets -Include *.wav,
  *.mp3, *.ogg, *.aiff, *.aif, *.mod, *.xm, *.flac` returns nothing.
- **No `AudioSource` anywhere** in any `.cs`, `.unity`, `.prefab` or `.asset` in the project,
  including the three `origin/main` scenes. A full-text search for `AudioSource`, `AudioClip`,
  `AudioMixer` and `PlayOneShot` across every one of those files returns zero hits.
- Every gameplay scene **does** carry an `AudioListener` on its camera —
  `Possible game scene.unity:2260`, `<ref>/Hard.unity:3461`, `<ref>/Medium.unity:81844`,
  `<ref>/Easy.unity:8079`, `Kyuri's Scene.unity:3042`, `MainMenu.unity:1166`,
  `VehicleLab.unity:12127` — and `VehicleLabSceneBuilder.cs:290` adds one deliberately. A listener
  with nothing to hear.

(The only `AudioListener` hits outside `Assets/Project` are in third-party demo scenes under
`Assets/Aside/` and `Assets/TextMesh Pro/`, which do not ship.)

**The game is silent.** Naming it as friction rather than a missing feature: `VehicleController`
exposes `IsDrifting` with a comment that it exists so "audio, particles and any drift scoring" read
one definition (`VehicleController.cs:243-246`), and `KnockableProp`'s header lists "a sound" as one
of the things its marker component is for (`KnockableProp.cs:12`). The hooks were designed in. For a
driving game, engine note is the main speed cue and the impact sound is the main collision cue; with
neither, the only feedback that the car hit something is the visual jolt, which is also the thing a
tester reports as "I didn't know what happened".

---

## 7. Further friction found, beyond the brief's list

### 7.1 An overturned car cannot be recovered, and there is no reset

The sharpest new finding. A complete code path, every hop cited.

`READ` — `VehicleController.Teleport(Vector3, Quaternion)` exists (`VehicleController.cs:1188`) and
is the only thing in the project that can reposition the car. `MEASURED` — a search of every `.cs`
under `Assets/Project/Scripts` for `.Teleport(` returns exactly one call site:
`VehicleMeasurement.cs:1016`, inside the F1–F9 measurement harness. **No gameplay code calls it.**
There is no respawn, no out-of-bounds check, no kill plane: searches for `Respawn`, `ResetCar`,
`ResetPosition`, `OutOfBounds`, `killPlane` return nothing outside the harness.

`READ` — the Flip move, the only self-righting mechanic, is gated on being grounded:
`CanFlipNow` requires `(!Tuning.flipRequiresGrounded || _controller.IsGrounded)`
(`VehicleJumpFlip.cs:88`), enforced again at `:251`, and `flipRequiresGrounded: 1` in
`Tuning_Balanced.asset:176` (C# default also `true`, `VehicleTuning.cs:455`).

`READ` — `IsGrounded` is true only if a suspension raycast hit something:
`IsGrounded = groundedCount > 0` (`VehicleController.cs:512`), and the rays are cast along
`Vector3 down = -up` where `up = transform.up` (`:441-442`, used at `:467`).

`INFERRED` (code path, not observed at runtime) — compose those: on its roof, the car's
`transform.up` points at the sky, so `down` points at the sky, so no ray hits, so `IsGrounded` is
false, so **Flip is refused exactly when the player needs it**. Jump is refused for the same reason
(`VehicleJumpFlip.cs:196`). With no reset and no respawn, an overturned car is stuck until the 300 s
shift clock runs out.

`MEASURED`, as a caveat on how often this bites: `flips` is `0` in all five logged rows of both
`shift_log.csv` files, so no logged shift recorded a flip and this state has not been observed.
`UNKNOWN` — whether the car can actually come to rest inverted. Its collider is 1.8 × 1.0 × 4.2 m,
a long flat box that tends to fall back onto its wheels. Single settling check: in play mode, flip
the car onto its roof against a kerb and try to recover.

This is also the most direct answer available to a tester saying they could not figure out how to
reset the car: there is nothing to figure out.

### 7.2 The greybox became a prefab on `origin/main` — a trap for anyone auditing scene files

Not a defect. Recorded because it looks exactly like one, and because it changes how every
scene-file audit in this document has to be read.

`MEASURED` — `git cat-file -s` on both blobs: `HEAD:Assets/Project/Scenes/Possible game scene.unity`
is 414,538 bytes; `origin/main:…` is **100,980**, and the extracted reference copy matches that byte
count exactly, so it is not an extraction fault. A YAML class-id census of the two scene files:

| | PrefabInstance | GameObjects | MeshRenderers | MeshColliders | Rigidbodies |
| --- | --- | --- | --- | --- | --- |
| working tree (`HEAD`) | 0 | 136 | 97 | 90 | 1 |
| `origin/main` | **1** | 40 | 3 | **0** | 1 |

On the face of it the city is gone. It is not. `READ` — `origin/main`'s copy holds one
`!u!1001 PrefabInstance` whose `m_SourcePrefab` GUID `1c3c5ea4155a8a849ba37da00eb32075` resolves to
`Assets/Project/Prefabs/Group 15 Greybox real one 1.prefab`, added on `origin/main`. `MEASURED` —
that prefab contains **96 GameObjects, 94 MeshRenderers, 90 MeshColliders, 0 Rigidbodies**, root
local scale 8.977, and of its 90 colliders **0 have negative world scale and 72 are non-uniform** —
the same 72/18 split the working tree's inline copy has. The greybox was extracted to a prefab, not
deleted. The blob shrank because 96 objects moved out of the scene file.

**The trap, and why it is in this document:** a class-id census of a scene file does not see prefab
contents. Every scene audit above had to be checked for this. `MEASURED` — PrefabInstance counts and
what each resolves to:

| Scene | PrefabInstances | resolving to | Rigidbodies inside those prefabs |
| --- | --- | --- | --- |
| `<ref>/Possible game scene` | 1 | `Group 15 Greybox real one 1.prefab` | 0 |
| `<ref>/Hard` | 2 | `Border.prefab`, `Canvas (2).prefab` | 0 |
| `<ref>/Medium` | 2 | `Canvas (2).prefab`, `Border.prefab` | 0 |
| `<ref>/Easy` | 1 | `Canvas (2).prefab` | 0 |
| working tree `Possible game scene`, `Kyuri's Scene`, `MainMenu`, `VehicleLab` | 0 | — | — |

`MEASURED` — `Border.prefab` holds 89 GameObjects, 84 MeshColliders, **0 Rigidbodies**, 0 colliders
at negative world scale (84 non-uniform); `City 1.prefab` 8 GameObjects and no colliders at all;
`SpeedBumps.prefab` 18 GameObjects, 17 MeshColliders, 0 Rigidbodies. **So §3.1's conclusion holds:
no Rigidbody anywhere, in a scene or in a prefab one of these scenes instantiates, sits under a bad
transform chain.** §3.2's single negative-scale collider is inline in all three scenes, not inside a
prefab.

The one caveat that remains: `<ref>/Possible game scene.unity`'s audit rows in §2 and §3 describe
only its 40 in-scene objects. Its 90 greybox colliders live in the prefab and were audited there,
with the result above.

A separate point worth keeping: **every row in both `shift_log.csv` files names
`Possible game scene`** (5 of 5), and the two trees' copies differ in more than the prefab
extraction — the working tree's still carries the stale keys of §1.3 pile 2 and `origin/main`'s does
not. The gameplay values that matter are the same in both (`enableCondition: 1` at
working-tree `:805` / `origin/main` `:800`; `checkInterval: 0.1` at `:790` / `:785`;
`capacity: 1` at `:1890` / `:1959`; `shiftDurationSeconds: 300` at `:1644` / `:1713`;
`hardBrakeDamage: 12` at `:809` / `:806`), so the telemetry remains usable against either — but the
scene is not identical and a diff of the two files is dominated by the prefab move.

### 7.3 Order sequence is not reproducible between runs

`READ` — `OrderManager` picks both indices with `UnityEngine.Random.Range` (`:242`, `:265`) and
`GenerateBoundedTestPoints` does likewise. No seed is set anywhere, and there is no `System.Random`
in the project. `INFERRED` — two playtest sessions on the same build get different order sequences,
so a per-session delivered/late count is not comparable to another session's at the resolution the
playtest sheet implies. Relevant to §1.2 only in that the *passive* case is deterministic regardless
of which points get picked, which is why that check works.

### 7.4 `Easy` has no floor plate

`READ` — `<ref>/Easy.unity` contains no object named `Floor`, `Surface`, `Ground` or `Plane`, and
`OrderManager.floorBounds` is `{fileID: 0}` (`:5534`). The other three scenes all have the 2 m-thick
`Floor` box with its top face at y = 0. `INFERRED` — in `Easy` the car drives only on the greybox
colliders (83 MeshColliders under `Group 15 Greybox real one 1`) and on
`Bastrop-blocks-combos.001`, the one negatively scaled collider from §3.2, with nothing underneath
to catch it. Combined with §7.1 (no reset), falling out of `Easy` ends the shift in practice.

---

## Contradictions

Each with the discriminating check and a verdict.

1. **`orderTimeLimit`: 50 (asset) vs 45 (C# default).** Check: simulate the passive-player late
   sequence against the two `delivered=0` rows, at six frame rates. Verdict: **45 s runs**; the
   asset's 50 is behind a YAML fault. `MEASURED` + `DERIVED`. The asset's own `whyThisSetup`
   rationale, which argues for 50 over 45, documents a game that is not being played.
2. **`whyThisSetup` claims 36/36 pairs clear the limit at 50 s** (`OrderTuning_Balanced.asset:24`).
   Since the limit is 45 s, that clearance claim is void as written, and the same text already
   records that 45 s "left that one pair about 3 s short" (`:26-27`). Verdict: the asset documents
   its own current failure mode. Costing the pairs at 45 s belongs to Group 2/3; flagged here, not
   resolved here.
3. **"PhysX cannot run a Rigidbody under negative non-uniform parent scale" as the reason props were
   reparented** (`KnockableProp.cs:27-29`, commit `7f357c7`) vs the resolved geometry. Check: print
   the full local-scale chain for every collider under `City` and multiply it out. Verdict: the
   negatives **cancel** — 1311 of 1312 colliders under `City` resolve to positive world scale, and
   the props were never under a net-negative chain. The reparenting is still sound for other reasons
   (hand-tunability, the undo path), but the stated mechanism is wrong. The one genuinely negative
   collider is `Bastrop-blocks-combos.001`, which the reparenting did not touch. `MEASURED`.
4. **"The mouse binding has no scale processor"** (working hypothesis in the brief) vs
   `VehicleControls.inputactions:503`. Check: read the binding. Verdict: it **does** —
   `ScaleVector2(x=0.06,y=0.06)`. The sensitivity problem is the `× cameraSwivelSpeed × dt` unit
   mismatch downstream, not a missing processor. `READ`.
5. **`PlaytestLogger`'s own comment** says an early quit is "flagged by a shorter play_seconds than
   shift_seconds" (`:131-133`) vs rows `:4`/`:5`, where `play_seconds` is *longer* than
   `shift_seconds`. Check: eliminate every reachable value of `TimeRemaining` from the `:302`
   expression. Verdict: the comment describes the arm of `:244` that did not run; on the arm that
   did, the flag is inverted. `DERIVED`.
6. **`CargoCondition`'s header** says "OFF BY DEFAULT … exactly Milestone 1 behaviour"
   (`:15-19`) vs `enableCondition: 1` in all four scenes. Check: read the serialized value in each
   scene. Verdict: it is **on** everywhere. The C# default is irrelevant to what ships. `READ`.
7. **`ZoneDetection`'s tooltip** recommends `checkInterval = 0` "which stops a fast car clipping
   through a small radius between checks" (`:31-33`) vs `checkInterval: 0.1` in all four scenes.
   Verdict: the scenes override it; the stated protection is off. `READ`.
8. **Stale-looking keys in the three new scenes** (`ticketScale`, `mainMenuSceneName`,
   `playerProgression`, `xpColour`) vs `origin/main`'s scripts. Check:
   `git show origin/main:<script>` and grep for each identifier. Verdict: **not stale** — all are
   declared on `origin/main` (`ShiftResults.cs:58,62,75`, `OrderTicketStrip.cs:41`,
   `ButtonsUI.cs:11`). They appear stale only when the new scenes are checked against the local
   feature branch's older copies of those four scripts. Anyone re-running the drift checker must
   check scenes against the matching tree.
9. **The brief's stale-field list** (`hardBrakeDecelThreshold`, `headerFontSize`, `orderLineSlots`)
   vs `origin/main`'s `Possible game scene.unity`. Check: `git show origin/main:<scene>` and grep
   for each key. Verdict: **all three are already gone on `origin/main`** — the scene was re-saved
   and now carries `hardBrakeMultiplier: 1.25` / `fallbackHardBrakeDecelThreshold: 27` and
   `urgentShiftSeconds: 30`. They are working-tree-only drift. What survives on `origin/main` is
   `patchKinds` in every scene, the `VehicleLab.unity` OrderHUD block, and
   `OrderTuning_Balanced.asset`'s five multi-leg keys. §1.3 piles 1 and 2. `MEASURED`.
10. **"`origin/main` deleted the greybox from `Possible game scene`"** — an apparent finding from a
    class-id census (0 MeshColliders, 3 MeshRenderers, blob 414 KB → 101 KB) vs the actual contents.
    Check: look for `!u!1001 PrefabInstance` and resolve its `m_SourcePrefab` GUID. Verdict:
    **wrong** — the greybox was extracted to
    `Assets/Project/Prefabs/Group 15 Greybox real one 1.prefab` (96 objects, 90 MeshColliders, 0
    Rigidbodies, 0 negative scales). Nothing was lost. Recorded because a scene-file census silently
    undercounts any scene containing a prefab instance, which is a standing hazard for this kind of
    investigation. `MEASURED`.

---

## Open questions (every `UNKNOWN`, with its one settling check)

| Question | Settling check |
| --- | --- |
| Does any delivery point sit inside a building footprint? | Downward raycast from each point in play mode, or `fbx_mesh_bounds.py` against the building meshes |
| Is the car actually clear of the road surface at spawn in `Hard`/`Medium` (≈0.1 m by transform)? | `fbx_mesh_bounds.py` on the road mesh for the true top face, or one downward raycast from the spawn |
| Does the car fall through `Bastrop-blocks-combos.001`? Is it drivable surface or decoration? | Drive onto it in `Easy` in play mode; or open the scene and compare the MeshCollider gizmo against the visible blocks |
| Can a `RoadBlocker04` be launched through the road? | Ram one at boost speed in play mode and look underneath. Threshold is 48.3 m/s (§3.3) |
| Can the car come to rest inverted, making §7.1 reachable? | Flip it onto its roof against a kerb in play mode and try to recover |
| Which row of the sensitivity table did the tester feel? | A frame-rate reading from that machine and build |
| Are the beacon ground discs visible in `Hard`/`Medium`, given the 0.4 m city offset vs the 0.05 m disc lift? | Look at one beacon in the editor |
| Does Unity's own YAML reader stop at the §1.1 fault, or skip the bad key and continue? The 45 s result says it stops, but only that one key discriminates | Add a second asset key after `whyThisSetup` whose value differs from its C# default, and read it back in the inspector |

---

## Commands that reproduce the measured figures

```
# stale serialized keys (new helper, read-only)
python docs/tooling/scene-tools/serialization_drift.py "Assets/Project/Scenes" \
    "Assets/Project/Scripts/Orders" "Assets/Project/Scripts/Vehicle/Tuning" \
    ".agents/tasks/design-overview-2026-10-14/ref"
# → 15 files checked, 59 stale keys

# the order-tuning asset is not valid YAML
python -c "import io,yaml; L=io.open('Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset',encoding='utf-8').read().split('\n'); yaml.safe_load('\n'.join(L[3:]))"
# → ParserError at file line 31 (spawnInterval)

# structural check, all seven scenes
python docs/tooling/scene-tools/validate_scene.py "Assets/Project/Scenes/Possible game scene.unity"
python docs/tooling/scene-tools/validate_scene.py ".agents/tasks/design-overview-2026-10-14/ref/Hard.unity"
python docs/tooling/scene-tools/validate_scene.py ".agents/tasks/design-overview-2026-10-14/ref/Medium.unity"
python docs/tooling/scene-tools/validate_scene.py ".agents/tasks/design-overview-2026-10-14/ref/Easy.unity"
# → exit 0, all four checks ok, for every scene

# the shift logs (UTF-8 BOM)
python -c "import io; print(io.open('Game Build/PlaytestLogs/shift_log.csv',encoding='utf-8-sig').read())"

# blob sizes behind §7.2
git cat-file -s "HEAD:Assets/Project/Scenes/Possible game scene.unity"         # 414538
git cat-file -s "origin/main:Assets/Project/Scenes/Possible game scene.unity"  # 100980

# which cited paths differ between the two trees (§"Trees")
git diff --name-status HEAD origin/main -- Assets ProjectSettings

# the prefab the shrunken scene points at (§7.2)
git show "origin/main:Assets/Project/Scenes/Possible game scene.unity" | Select-String "m_SourcePrefab"
git show "origin/main:Assets/Project/Prefabs/Group 15 Greybox real one 1.prefab" | Measure-Object -Line

# stale keys as they stand on origin/main, not in the working tree
git show "origin/main:Assets/Project/Scenes/Possible game scene.unity" |
    Select-String "hardBrakeDecelThreshold|transferRadius|headerFontSize|urgentShiftSeconds"
# → only urgentShiftSeconds: 30 matches; the other three are absent
```

The Rigidbody-chain scan, the per-collider world-scale audit, the 36-pair distance matrix and the
passive-player late simulation were run from throwaway scripts under the system temp directory, not
in the repo. Each is a short transform-resolution pass over the scene YAML using the same hierarchy
maths as `docs/tooling/scene-tools/scene_points.py`; the inputs and outputs are quoted in full
above, and §3.1's chain print is the one that needs re-running if anyone doubts the
negative-scale cancellation.

---

## Note on the three family-member testers

Per plan §0.3 they are not data. They appear here only as directions this investigation followed:
the camera report sent §4, and the reverse/overshoot report is turning-circle geometry, which is
outside this group's scope and is the subject of plan item 18's geometry `UNKNOWN`.
