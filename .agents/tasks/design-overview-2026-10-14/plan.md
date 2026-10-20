# Implementation Plan — Design Overview investigation (2026-10-14)

Deliverable of the whole workflow: `docs/DESIGN-OVERVIEW.md`, an accurate account of what
*Zoom Zoom Delivery Co* currently **is**, answering the five question groups. Four investigators
(INV-A..D) work the groups in parallel; the consolidator writes the document.

---

## 0. Ground rules every step inherits

### 0.1 Read-only, and no editor

No step may change game code, a scene, a prefab, an asset, a tuning value, `ProjectSettings/`, or
git state. No `git checkout`, `switch`, `stash`, `commit`, `push`, `branch`, `merge`, `restore`.
Do not launch Unity: the developer has the project open and a second instance cannot attach.

Writes are permitted **only** under
`.agents/tasks/design-overview-2026-10-14/` and to the one output file `docs/DESIGN-OVERVIEW.md`.
`.agents/` is **not** covered by `.gitignore` (checked: the file ignores `.kiro/`, `planning/`,
`notes/`, `*.plan.md`, but not `.agents/`), so never run `git add -A` / `git add .` in this task.

### 0.2 Which tree is authoritative — decided, do not re-decide

**`origin/main` is the reference tree.** The local checkout is `feature/vehicle-camera` at
`618f2be`, which is 1 commit ahead of the merge base and **7 behind `origin/main`**
(`git rev-list --left-right --count HEAD...origin/main` → `1  7`). The Easy/Medium/Hard restructure
landed on `origin/main` and is **not present locally**: the local Scenes folder still holds
`Kyuri's Scene.unity` and has no `Easy/Medium/Hard`. `git diff --name-status HEAD origin/main`
shows `Kyuri's Scene.unity` renamed to `Hard.unity` (R082), with `Medium.unity`, `Easy.unity` and
`Border.prefab` added.

Rationale: the question is what the shared game *is*, and the shared build is `origin/main`. Using
the local feature branch would describe a scene set the team no longer has.

Consequence: exactly these paths differ and **must be read from `origin/main`**, never from the
working tree:

```
Assets/Project/Scenes/Hard.unity            (was Kyuri's Scene.unity)
Assets/Project/Scenes/Medium.unity
Assets/Project/Scenes/Easy.unity
Assets/Project/Scenes/Border.prefab
Assets/Project/Scenes/MainMenu.unity                 (M)
Assets/Project/Scenes/Possible game scene.unity      (M)
Assets/Project/Scripts/Orders/ButtonsUI.cs           (M)
Assets/Project/Scripts/Orders/OrderHUD.cs            (M)
Assets/Project/Scripts/Orders/OrderTicketStrip.cs    (M)
Assets/Project/Scripts/Orders/ShiftResults.cs        (M)
Assets/Project/Scripts/Orders/ResultsPanel.cs        (new on origin/main)
Assets/Project/Scripts/UI/DifficultySceneLoader.cs   (new on origin/main)
Assets/Project/Scripts/UI/MainMenuManager.cs         (replaces "Main Menu Manager.cs")
ProjectSettings/EditorBuildSettings.asset            (M)
ProjectSettings/ProjectSettings.asset                (M)
docs/playtesting/playtest_sessions.csv               (M)
docs/playtesting/README.md                           (M)
```

Everything else — the whole of `Assets/Project/Scripts/Vehicle/`, all three
`Tuning_*.asset`, `OrderTuning_Balanced.asset`, `Order.cs`, `OrderManager.cs`, `ShiftTimer.cs`,
`ZoneDetection.cs`, `CargoSystem.cs`, `CargoCondition.cs`, `PlayerProgression.cs`, all of
`docs/tooling/` — is byte-identical between `HEAD` and `origin/main` and is read in place.

When a document statement depends on this split, say which tree it came from.

### 0.3 Evidence labels — mandatory on every number

A recent incident nearly committed fabricated playtest data. The standard here is absolute.

| Label | Means | Must carry |
| --- | --- | --- |
| `READ` | Literal value sitting in a file | `path:line` (or `path` + YAML key for scenes) |
| `MEASURED` | Value recorded by an instrument run — the F1–F9 harness, `PlaytestLogger`, or a tooling script | source file + row/filter, or the exact command and its output |
| `DERIVED` | Arithmetic on READ/MEASURED values, nothing else | the inputs, each labelled, and the operation |
| `INFERRED` | Read of code control flow, no runtime evidence | the code path, and the words "not observed at runtime" |
| `UNKNOWN` | Not establishable without running the game or opening the editor | **the single check that would settle it** |

Hard rules:

- No number without a label. No label without a citation.
- Never estimate. "About 2 metres" is `UNKNOWN` unless a file says so.
- A design *intention* is `READ` only when a file states it (e.g. `whyThisSetup` in
  `OrderTuning_Balanced.asset`, the `///` rationale blocks in `VehicleController.cs`). An
  intention nobody wrote down is `UNKNOWN`, not a guess.
- Three family-member testers mentioned in the brief are **not data**: unstructured, no
  questionnaire, no telemetry. They may appear only as "a direction this investigation followed",
  never as a finding or a number.
- `docs/QA-PLAYTEST-REPORT-2026-10-14.md` and `docs/playtesting/QA-AI-20261014-01.md` predate the
  Easy/Medium/Hard restructure. They are untrusted. Where a QA claim disagrees with code, **the
  code wins and the document must say the QA report is wrong, naming both sources.**

### 0.4 Where findings go

Each investigator appends to their own file so parallel steps never collide:

```
.agents/tasks/design-overview-2026-10-14/findings/group-1-vehicle.md
.agents/tasks/design-overview-2026-10-14/findings/group-2-map.md
.agents/tasks/design-overview-2026-10-14/findings/group-3-orders.md
.agents/tasks/design-overview-2026-10-14/findings/group-4-winloss.md
.agents/tasks/design-overview-2026-10-14/findings/group-5-friction.md
.agents/tasks/design-overview-2026-10-14/findings/contradictions.md   (append-only, shared)
```

Every contradiction resolved goes in `contradictions.md` as: claim A + cite, claim B + cite, the
discriminating check run, the verdict, the label of the verdict.

### 0.5 The Hard.unity coordinate trap — read this before touching any distance

`Hard.unity` parents its city under a root transform `City` at **pos (-6.39, 0.40, 139.24), uniform
scale 2.0439076** (verified:
`python docs/tooling/scene-tools/scene_points.py <ref>/Hard.unity City`). Any local coordinate read
straight out of the YAML is wrong by ~2x plus an offset.

Mitigation that is already built: `scene_points.py` resolves the hierarchy and prints **world
space**. Use it. Never hand-read `m_LocalPosition` for a child of `City` and call it a world
coordinate. The same applies to `CityProps` (scale **-871.65**, negative) and `Roads_Parent`
(scale 703.40).

---

## Plan

- [ ] 1. **Build the reference tree and the task scaffold.** (prerequisite for every other item)
      Extract the 17 `origin/main`-only paths from §0.2 into a scratch reference directory using
      the helper already written for this task, which copies blob bytes unchanged — PowerShell
      redirection re-encodes git's stdout and corrupts Unity YAML. Create the `findings/` directory
      and an empty `contradictions.md`. Four scenes and `ResultsPanel.cs` are already extracted;
      do the rest.
      Files: `.agents/tasks/design-overview-2026-10-14/tools/extract_ref.py` (exists),
      `.agents/tasks/design-overview-2026-10-14/ref/*`,
      `.agents/tasks/design-overview-2026-10-14/findings/`
      Verify: `python docs/tooling/scene-tools/validate_scene.py ".agents/tasks/design-overview-2026-10-14/ref/Hard.unity"`
      runs and reports on the extracted copy (proving the YAML survived extraction intact), and
      `git status --short -- Assets ProjectSettings` prints nothing.

- [ ] 2. **Record the read-only baseline.** Capture, verbatim into
      `findings/contradictions.md` under a "baseline" heading, the output of the two commands that
      later steps re-run to prove nothing was changed. `compile_check.py` passes today: it reports
      `RUNTIME 342 references, 80 scripts … errors 0, warnings 0` / `EDITOR 360 references …
      errors 0, warnings 0` / `Compiles clean.`
      Files: `findings/contradictions.md`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, exit 0; and
      `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → passes (install with
      `pip install --user pytest hypothesis` if missing, per `docs/tooling/repo-checks/README.md`).

- [ ] 3. **Catalogue the scene and build inventory, and settle what ships.** Resolve three
      contradictions here because every later group depends on knowing which scene is the game.
      (a) README says `Possible game scene` is "the first scene in Build Settings"; `origin/main`'s
      `EditorBuildSettings.asset` lists `MainMenu.unity` at index 0 and `Possible game scene` at
      index 1 — **discriminating check**: the order of `m_Scenes` entries in that file; README loses.
      (b) `Easy.unity` exists as an asset but is **absent from `m_Scenes`** entirely, while
      `Medium.unity` (index 4) and `Hard.unity` (index 5) are present and `enabled: 1` — confirm by
      listing every `path:` in the file, then grep `DifficultySceneLoader.cs` for how it names
      scenes (`LoadScene` by name or by index) and state whether picking Easy in the menu can work
      at all.
      (c) Name the tested build: the only scene appearing in `PlaytestLogs/shift_log.csv` and
      `Game Build/PlaytestLogs/shift_log.csv` is `Possible game scene` (all 5 rows), so the
      telemetry describes that scene and **not** Hard/Medium/Easy. Say so explicitly.
      Files: `<ref>/EditorBuildSettings.asset`, `<ref>/DifficultySceneLoader.cs`,
      `<ref>/MainMenuManager.cs`, `<ref>/MainMenu.unity`, `README.md`,
      `PlaytestLogs/shift_log.csv`, `Game Build/PlaytestLogs/shift_log.csv` (both UTF-8 BOM —
      decode `utf-8-sig`)
      Verify: write `findings/group-2-map.md` §scene-inventory; re-run
      `python docs/tooling/scene-tools/scene_points.py` against each of the four extracted scenes
      and confirm each resolves without error, and that the scene list in the findings file matches
      `git ls-tree -r --name-only origin/main -- Assets/Project/Scenes` exactly.

### Group 1 — Core vehicle physics & handling (INV-A)

- [ ] 4. **Establish the handling model and the global physics frame.** Answer "arcade or
      sim-cade" from the mechanism, not from feel. `VehicleController.cs` is an *arcade car on a
      Rigidbody held up by four raycasts, not WheelColliders* — its own header block says so and
      explains why (`VehicleController.cs:77-98`); confirm there is no `WheelCollider` anywhere and
      record the force application sites (`AddForce(Vector3.down * tuning.gravity,
      ForceMode.Acceleration)` at ~`:407`, suspension `AddForceAtPosition … ForceMode.Acceleration`
      at ~`:718`, drive `AddForce(GroundForward * totalAccel, ForceMode.Acceleration)` at ~`:823`).
      Note the consequence the code itself flags: **acceleration-mode forces ignore mass, so
      changing `mass` in the tuning asset does not change acceleration** (`:98`) — this is the
      single most important "how heavy does it feel" answer and it is `READ`, not inferred.
      Capture the global frame: `Fixed Timestep: 0.02`, `Maximum Allowed Timestep: 0.33333334`
      (`ProjectSettings/TimeManager.asset`); `m_Gravity y: -9.81`, `m_DefaultSolverIterations: 6`,
      `m_DefaultSolverVelocityIterations: 1`, `m_BounceThreshold: 2`,
      `m_DefaultMaxAngularSpeed: 50`, `m_FrictionType: 0`
      (`ProjectSettings/DynamicsManager.asset`). Flag the override: the car sets its own
      `gravity: 20` in tuning (more than 2x project gravity), `_rb.maxAngularVelocity =
      tuning.maxAngularSpeed`, `_rb.maxLinearVelocity = highestIntendedSpeed * 2.5f`,
      `_rb.interpolation = Interpolate` (`:334-354`).
      Files: `Assets/Project/Scripts/Vehicle/VehicleController.cs`,
      `Assets/Project/Scripts/Vehicle/VehicleInput.cs`,
      `Assets/Project/Scripts/Vehicle/VehicleTuning.cs`,
      `ProjectSettings/TimeManager.asset`, `ProjectSettings/DynamicsManager.asset`
      Verify: `findings/group-1-vehicle.md` §mechanism exists with every number carrying
      `path:line`; `python docs/tooling/compile_check.py` still reports `Compiles clean.`

- [ ] 5. **Document grip vs drift, surface profiles, and weather.** Read the lateral-grip model and
      the handbrake's rear-only implementation (`VehicleController.cs:1024` onward explains why a
      rear-only handbrake cannot be one force at the centre of mass). Then read
      `SurfaceType.cs` for the per-surface profile set and which surfaces exist, and `WeatherState.cs`
      for whether weather is wired to anything or is a dormant type — **discriminating check**: does
      any scene contain a component referencing `WeatherState`, and does `VehicleController` read it
      per-frame? If no scene references it, label weather `READ: type exists, not wired` rather
      than describing it as a feature. Cross-check with the 84 surfaces tagged in the scene
      (commit `c03c220` "tag 84 city surfaces") and `Assets/Project/Scripts/Vehicle/Editor/SurfaceTagger.cs`.
      Files: `Assets/Project/Scripts/Vehicle/SurfaceType.cs`,
      `Assets/Project/Scripts/Vehicle/WeatherState.cs`,
      `Assets/Project/Scripts/Vehicle/VehicleController.cs`,
      `Assets/Project/Scripts/Vehicle/Editor/SurfaceTagger.cs`, `<ref>/Hard.unity`,
      `Assets/Project/Scenes/Possible game scene.unity`
      Verify: `findings/group-1-vehicle.md` §grip lists every surface profile name with its source
      line, and weather carries an explicit wired/not-wired verdict with the check that produced it.

- [ ] 6. **Document boost, jump and flip as they actually behave.** Read `VehicleJumpFlip.cs` end to
      end (jump impulse, flip directionality, ground/air gating, cooldowns) and the boost path in
      `VehicleController.cs` (`AddForce(GroundForward * tuning.boostAcceleration,
      ForceMode.Acceleration)` ~`:878`) plus `BoostGauge.cs` for the earn/spend rates and the gauge
      feedback. Resolve the README claim against code: README states the tank starts full, does not
      refill passively, pays in at **20/s from a handbrake slide and 10/s from air time**, that a
      full tank is **3 s** of boost, and that boost takes the car **32 → 40 m/s**.
      **Discriminating check**: find each of those four numbers as a field in `VehicleTuning.cs` /
      the three `Tuning_*.asset` files; any that is not there is a README claim, not a game fact,
      and the top-speed figures must be reconciled with item 7's CSV reading before being quoted.
      Also note the discoverability fact the logs support: `boost_activations` is `0` in **all 5**
      rows of both `shift_log.csv` files — boost exists and was never used in any logged shift
      (`MEASURED`).
      Files: `Assets/Project/Scripts/Vehicle/VehicleJumpFlip.cs`,
      `Assets/Project/Scripts/Vehicle/BoostGauge.cs`,
      `Assets/Project/Scripts/Vehicle/VehicleController.cs`,
      `Assets/Project/Scripts/Vehicle/VehicleTuning.cs`,
      `Assets/Project/Scripts/Vehicle/Tuning/Tuning_Balanced.asset`,
      `PlaytestLogs/shift_log.csv`, `Game Build/PlaytestLogs/shift_log.csv`, `README.md`
      Verify: `findings/group-1-vehicle.md` §boost-jump-flip; every README number is marked either
      confirmed-in-asset (with `path:line`) or README-only.

- [ ] 7. **Read the three tuning profiles side by side and extract the measured figures.** Tabulate
      `Tuning_Balanced` / `Tuning_Grippy` / `Tuning_Loose` field-by-field and report only the fields
      that **differ** (confirmed differing so far: `mass` 900/900/**950**, `centreOfMassOffset`
      y -0.35/-0.4/-0.3 with Loose also z **-0.15**, `steerInputSmoothTime` 0.07/0.05/0.09,
      `steerVisualExaggeration` 1.6/1.3/**2.0**; identical so far: `gravity` 20,
      `cameraSwivelSpeed` 120, `steerOverdrive*`). State which profile the shipped scene actually
      assigns — **discriminating check**: the tuning asset GUID referenced by the
      `VehicleController` component in `<ref>/Possible game scene.unity` and in `<ref>/Hard.unity`,
      matched against the GUID in each `Tuning_*.asset.meta`.
      Then mine `VehicleLab_Measurements/vehicle_measurements.csv` (641 lines incl. header; columns
      `timestamp,profile,test,reading,value,unit,notes`). **The file is append-only history across
      multiple capture dates, so group by `profile` and take the latest `timestamp` per
      `(profile, test, reading)`** — the first rows are a 2026-09-09 capture reading
      `Balanced / Top speed = 25.0 m/s` and `Sideways grip = 12.0 m/s^2`, which contradicts the
      32 m/s and 13 m/s^2 quoted in `README.md`, `docs/tooling/scene-tools/delivery_points.py`
      (`TOP_SPEED = 32.0`, `SIDEWAYS_GRIP = 13.0`) and `OrderTuning_Balanced.asset`'s
      `whyThisSetup`. **Discriminating check**: latest timestamp wins; report the current measured
      value as `MEASURED` with its timestamp, and record every place still quoting the stale figure
      as a documentation defect. Ignore `vehicle_measurements_ako_local.csv` — `.gitignore` marks
      `*_local.csv` as one person's unmerged capture, not group data.
      Files: `Assets/Project/Scripts/Vehicle/Tuning/Tuning_{Balanced,Grippy,Loose}.asset` (+`.meta`),
      `VehicleLab_Measurements/vehicle_measurements.csv`,
      `<ref>/Possible game scene.unity`, `<ref>/Hard.unity`,
      `Assets/Project/Scripts/Vehicle/VehicleMeasurement.cs` (for what each reading means)
      Verify: `findings/group-1-vehicle.md` §profiles carries a latest-per-profile table whose row
      count equals the distinct `(profile, test, reading)` count in the CSV, and
      `findings/contradictions.md` has the 32-vs-25 m/s entry with its verdict.

### Group 2 — Map & environment (INV-B)

- [ ] 8. **Measure the city: extent, layout, street widths, verticality.** For each of
      `<ref>/Hard.unity`, `<ref>/Medium.unity`, `<ref>/Easy.unity` and
      `Assets/Project/Scenes/Possible game scene.unity`, run `scene_points.py` and derive the
      world-space bounding box of the drivable area and the `Floor` collider dimensions. **Apply
      §0.5**: `Hard.unity` parents everything under `City` at (-6.39, 0.40, 139.24) scale 2.0439076,
      so use `scene_points.py`'s resolved world-space output, never raw `m_LocalPosition`. Recover
      street centrelines and widths with `find_roads.py`, which clusters `SideWalk` kerb objects —
      per `docs/tooling/scene-tools/README.md` it found 7 north-south and 7 east-west centrelines
      (49 junctions) from 54 pavement objects in the old greybox; re-run it per scene and report
      what it finds **now**, noting if it finds nothing because the restructured scenes no longer
      use `SideWalk` objects. Verticality: report whether any drivable surface sits above `y=0.4`
      (the `Floor` top face); if the only non-zero `y` values are props and lights, say the city is
      flat, labelled `READ`.
      Files: `docs/tooling/scene-tools/scene_points.py`, `docs/tooling/scene-tools/find_roads.py`,
      `docs/tooling/scene-tools/fbx_mesh_bounds.py`, the four scenes, `<ref>/Border.prefab`
      Verify: `findings/group-2-map.md` §extent, one sub-section per scene, each with the exact
      command line that produced its numbers; re-running any one command reproduces the quoted
      figures.

- [ ] 9. **Inventory colliders and knockable props per scene.** Count `KnockableProp` instances per
      scene and report their collider shape and mass source (`KnockableProp.cs`,
      `Editor/KnockablePropBaker.cs`; commit `d80d61e` records "mass is a feel number" and
      `752943e` that box size is taken from real vertices, not Unity's reported bounds). Confirm the
      road surface is a **single merged `Roads_Parent` MeshCollider** rather than per-tile colliders
      — `Roads_Parent` resolves to world pos (295.58, 0.40, -158.13) scale 703.40 in `Hard.unity`;
      report the collider component type on it and whether it is convex, and state the consequence
      for surface tagging and for raycast-based suspension (one collider = one surface tag for the
      whole road network, if `SurfaceTagger` works per collider — cross-check with INV-A item 5).
      Files: `Assets/Project/Scripts/Vehicle/KnockableProp.cs`,
      `Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs`,
      `Assets/Project/Scripts/Vehicle/Editor/SurfaceTagger.cs`, the four scenes
      Verify: `findings/group-2-map.md` §props-and-colliders gives a per-scene prop count that
      matches a repeatable count of `KnockableProp` script-GUID occurrences in that scene's YAML,
      and names the `Roads_Parent` collider type with its YAML key.

- [ ] 10. **Cost pickup → drop-off travel time per scene, with a scene-derived point set.** Do not
      trust the hardcoded table: `docs/tooling/scene-tools/delivery_points.py` holds the point set
      for the **old** greybox and it matches only `Possible game scene` (verified: Pickup 01 is
      (-220.11, 0, 7.03) in both). In the restructured scenes the same twelve objects have been
      hand-moved — Pickup 01 is (0, 0, 137.00) in `Hard.unity`, (-96.30, 0, -22.00) in
      `Medium.unity`, (-245.90, 0, -22.00) in `Easy.unity` (all world-space via `scene_points.py`).
      So: extract each scene's actual `Pickup *` / `DropOff *` world positions, plus the player
      spawn position for the approach leg, write them to a per-scene point table under the task
      directory, and cost all 36 pairs with `order_distances.py`'s grid metric (streets run on the
      axes, so Manhattan distance is the honest cost; the script also prints straight-line).
      Report, per scene: shortest / mean / longest job including the approach leg, and how many of
      36 pairs clear that scene's own order time limit (from Group 3 item 12). Baseline for the old
      set at 50 s is 36/36, shortest 260 m / 13 s, mean 557 m / 28 s, longest 954 m / 48 s at the
      20 m/s working average — quote those as `Possible game scene` only.
      **Flag the working average honestly**: `WORKING_AVERAGE = 20.0` is an assumption written into
      the script, so every "seconds" figure is `DERIVED` from an assumed speed, not `MEASURED`. If
      item 7 lands on 25 m/s measured top speed rather than 32, note that 20 m/s is then 80% of top
      speed and the costing is optimistic; do not silently re-base it.
      Files: `docs/tooling/scene-tools/order_distances.py`,
      `docs/tooling/scene-tools/delivery_points.py`, `docs/tooling/scene-tools/scene_points.py`,
      the four scenes, `.agents/tasks/design-overview-2026-10-14/findings/points-<scene>.md`
      Verify: `python docs/tooling/scene-tools/order_distances.py 50` reproduces the
      `Possible game scene` baseline above verbatim; each other scene's table is accompanied by the
      command and the extracted point list that produced it.

### Group 3 — Order system & economy (INV-C)

- [ ] 11. **Resolve the Value formula.** The code path is
       `OrderManager.ResolveDelivery` → `tuning.DeliveryValueFor(order.DesignedDistance) *
       Mathf.Clamp01(conditionMultiplier)`, where
       `DeliveryValueFor(d) = baseDeliveryValue + max(0,d) * valuePerMetre`
       (`OrderTuning.cs:50`) and `DesignedDistance` is the **straight-line pickup→dropoff distance
       fixed at spawn**, never distance driven (`Order.cs`, field comment).
       The contradiction: `OrderTuning_Balanced.asset` sets `baseDeliveryValue: 10` but **contains
       no `valuePerMetre` key at all**, while `OrderTuning.cs:41` declares
       `public float valuePerMetre = 0.1f`. Unity constructs the ScriptableObject (running field
       initialisers) then overlays the YAML, so a missing key keeps `0.1` — meaning the shipped
       value is probably *not* flat 10. The logs are the discriminator and they fit neither naive
       reading: `delivered=9, value=61`; `delivered=4, value=158`; `delivered=6, value=216`
       (`Game Build/PlaytestLogs/shift_log.csv`, `PlaytestLogs/shift_log.csv`). Flat 10 would give
       90/40/60. 10 + 0.1·d would give **more**, not 61 for nine deliveries.
       **Discriminating check, in this order**: (a) confirm which `OrderTuning` asset the scene's
       `OrderManager` actually references, by GUID, in `<ref>/Possible game scene.unity` — it may
       not be `OrderTuning_Balanced`; (b) read how `PlaytestLogger` accumulates `value` (is it the
       sum of `OrderResolved` values, rounded, or something else?); (c) read `CargoCondition.cs`
       for the multiplier range actually passed in, since a condition multiplier below 1 is the only
       term that can push 9 deliveries under 90. Report the formula as `READ` and the per-shift
       totals as `MEASURED`, and if (a)–(c) still cannot reproduce 61/158/216 arithmetically, say
       so and mark the reconciliation `UNKNOWN` with the one check that would settle it (a logged
       shift with per-order values). **Do not invent a formula that fits.**
       Files: `Assets/Project/Scripts/Orders/OrderTuning.cs`,
       `Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset`,
       `Assets/Project/Scripts/Orders/Order.cs`,
       `Assets/Project/Scripts/Orders/OrderManager.cs`,
       `Assets/Project/Scripts/Orders/CargoCondition.cs`,
       `Assets/Project/Scripts/Vehicle/PlaytestLogger.cs`,
       `<ref>/Possible game scene.unity`, both `shift_log.csv` files
       Verify: `findings/group-3-orders.md` §value shows the formula with line cites and an explicit
       arithmetic reconciliation attempt against all three logged totals, each either closed or
       marked `UNKNOWN` + check.

- [ ] 12. **Settle the late penalty and the timer three-way contradiction.** Late penalty:
       `latePenaltyPerSecond` exists in exactly two places — `OrderTuning.cs:44` and
       `OrderTuning_Balanced.asset:35` (value `1`) — and **nothing reads it**. The late path is
       `OrderManager.TickAllOrders` → `Resolve(order, value: 0f, wasLate: true)`, i.e. a late order
       **awards nothing and deducts nothing**. Verify the no-reader claim by searching every `.cs`
       for the identifier and report it as a dead tuning field.
       Timers: `OrderTuning.cs:34` defaults `orderTimeLimit = 45f`;
       `OrderTuning_Balanced.asset` sets `50`; `order_distances.py`'s `main()` defaults to **75.0**
       while its own README and the asset's `whyThisSetup` both talk about 50. **Discriminating
       check**: the runtime value is whatever the asset the scene references says, because the asset
       overrides the C# default — so resolve per scene via the `OrderManager` tuning GUID (item 11a),
       and report 45 as "C# default, reachable only if an asset omits the key", 75 as "a tooling
       script default, never a game value". Also resolve the shift length against the same rule:
       `ShiftTimer.shiftDurationSeconds = 300f` in code, but the serialized value in each scene's
       `ShiftTimer` component is what runs — read it per scene.
       Files: `Assets/Project/Scripts/Orders/OrderTuning.cs`,
       `Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset`,
       `Assets/Project/Scripts/Orders/OrderManager.cs`,
       `Assets/Project/Scripts/Orders/ShiftTimer.cs`,
       `docs/tooling/scene-tools/order_distances.py`, all four scenes
       Verify: `findings/contradictions.md` carries the 45/50/75 entry and the late-penalty entry,
       each with a verdict; `findings/group-3-orders.md` §timers has a per-scene serialized value.

- [ ] 13. **Document spawn cadence, caps, zone radii, cargo, and the determinism claim.** Read:
       `spawnInterval: 12`, `maxActiveOrders: 3` (`OrderTuning_Balanced.asset`); spawn gating in
       `OrderManager.TrySpawn` (`if (_activeOrders.Count >= tuning.maxActiveOrders) return;` before
       the interval decrements, so the interval does not accrue while capped); the fallback point
       generation ladder (real `Transform[]` arrays → `floorBounds` collider scatter with
       `floorEdgeMargin = 5` → a 60 m origin ring at `testPointRadius = 60`,
       `testPointCount = 6`) and which branch each scene takes; zone radii
       `pickupRadius = 6f`, `dropOffRadius = 6f` and `checkInterval = 0f` (every frame) in
       `ZoneDetection.cs`, plus the fact that `OrderBeacon` reads `PickupRadius` so the visual disc
       cannot drift from the trigger; cargo capacity from `CargoSystem.cs` (`HasCapacity`,
       `CarriedOrders`) and the per-scene serialized slot count — commit `e1f1708` says
       "Kyuri's Scene carries two cargo slots", so check whether `Hard.unity` has 2 and
       `Possible game scene` has a different number.
       Determinism: `OrderManager` uses `UnityEngine.Random.Range` in both
       `GenerateBoundedTestPoints` and `PickIndexAvoidingCollisions` (8 attempts, 0.5 m
       same-spot threshold, then a warning and a possibly-colliding pick). There is **no**
       `System.Random` anywhere in the project (searched) and **no seed is set**, so order sequence
       is not reproducible between runs. If any pitch or design document claims deterministic or
       repeatable order sequences, cite it and contradict it.
       Also: `CargoCondition.cs` penalties — what degrades condition, by how much, and the
       multiplier floor; and `PlayerProgression.TrySpendMoney` (`PlayerProgression.cs:14, :127`) has
       **no caller** outside its own file — verify by searching all `.cs` and report the upgrade
       economy as unreachable code.
       Files: `Assets/Project/Scripts/Orders/OrderManager.cs`,
       `Assets/Project/Scripts/Orders/ZoneDetection.cs`,
       `Assets/Project/Scripts/Orders/CargoSystem.cs`,
       `Assets/Project/Scripts/Orders/CargoCondition.cs`,
       `Assets/Project/Scripts/Orders/OrderBeacon.cs`,
       `Assets/Project/Scripts/Orders/PlayerProgression.cs`,
       `Assets/Project/Scripts/Orders/ProgressionTuning.cs`,
       `Assets/Project/Scripts/Orders/ProgressionTuning.asset`, all four scenes
       Verify: `findings/group-3-orders.md` §spawning-and-cargo, with a per-scene statement of which
       point-resolution branch runs and the cargo slot count read from that scene's YAML.

### Group 4 — Win/loss & progression (INV-D)

- [ ] 14. **Define completed vs failed shift, and document the EndShift defect.** The shift is a
       single 300 s countdown (`ShiftTimer.cs`, `shiftDurationSeconds = 300f`, `// 5 minutes, per
       Scope`), `startAutomatically = true`, and at zero `EndShift()` runs. There is **no fail
       state**: `EndShift` sets `IsShiftActive = false`, sets `orderManager.enabled = false`, logs,
       and fires `ShiftEnded`. Nothing checks a quota. Confirm no score threshold, no life count,
       no lose screen exists anywhere.
       The defect, stated precisely: `orderManager.enabled = false` stops `OrderManager.Update`, so
       spawning and ageing stop — but `ZoneDetection.Update` is on the **car**, keeps running, and
       calls `cargoSystem.TryDeliver(orderManager, order)` → `OrderManager.ResolveDelivery(...)`
       **directly**, which does not check `enabled` or `IsShiftActive`. A disabled MonoBehaviour's
       public methods still execute. So a player can keep delivering carried cargo after the clock
       hits zero, incrementing `TotalResolved` and firing `OrderResolved`. **Discriminating check**:
       show that `ResolveDelivery` has no `enabled`/`IsShiftActive` guard (read the method), and
       that `ZoneDetection` is a component on the vehicle object, not on the OrderManager object
       (read both scenes' YAML). Then establish whether `ShiftResults` and `OrderHUD` diverge as a
       result — read `<ref>/ShiftResults.cs` and `<ref>/OrderHUD.cs` and determine which counter
       each displays and whether either subscribes to `ShiftEnded` to freeze its total.
       Results screen: identify the actual source of the end-of-shift screen between
       `<ref>/ShiftResults.cs` and `<ref>/ResultsPanel.cs` (new on `origin/main`) — **discriminating
       check**: which of the two is present as a component in `<ref>/Hard.unity` and
       `<ref>/Possible game scene.unity`, by script GUID.
       Progression: `PlayerProgression.cs` exists and is referenced by `OrderHUD.cs` in 22 places,
       but nothing calls `TrySpendMoney` (item 13) — state whether a progression component is
       actually in any scene, by GUID, and if not, say there is no progression in the shipped game.
       Files: `Assets/Project/Scripts/Orders/ShiftTimer.cs`,
       `Assets/Project/Scripts/Orders/ZoneDetection.cs`,
       `Assets/Project/Scripts/Orders/OrderManager.cs`,
       `Assets/Project/Scripts/Orders/CargoSystem.cs`,
       `<ref>/ShiftResults.cs`, `<ref>/ResultsPanel.cs`, `<ref>/OrderHUD.cs`,
       `<ref>/OrderTicketStrip.cs`, `<ref>/ButtonsUI.cs`,
       `<ref>/Hard.unity`, `<ref>/Possible game scene.unity`
       Verify: `findings/group-4-winloss.md` states the defect as a code path with every hop cited
       `path:line`, labelled `INFERRED (code path, not observed at runtime)` unless a log row
       demonstrates it; results-screen owner named by GUID match.

### Group 5 — Known technical & design friction (INV-D, after item 14)

Excluded as already known: UI canvas sorting, and the incomplete Controls screen. Do not re-report
them except where a *new* consequence is found.

- [ ] 15. **Spawn reachability and the negative-scale prop hazard.** (a) Check every scene's twelve
       delivery points against that scene's drivable surface: a point whose `y=0` sits off the
       `Floor`/`Roads_Parent` extent, or inside a building footprint, is unreachable. Use
       `validate_scene.py` for structural faults and the item 10 point tables plus the item 8
       extents for containment. Report per scene, as a count plus the named points.
       (b) The negative-scale issue: `CityProps` in `Hard.unity` resolves to uniform scale
       **-871.65** (verified). Unity's physics does not support negative scale on a collider
       hierarchy — it mirrors the mesh but the collider and inertia tensor misbehave, which is the
       mechanism behind props being knocked into delivery zones or through the road. State the
       geometry as `READ`, state the collision consequence as `INFERRED` naming the mechanism, and
       mark "props actually pass through the road" as `UNKNOWN` with the one check that settles it
       (a play-mode observation, which this investigation cannot run). Note that commits `7f357c7`
       ("pull each prop out of the city's broken transform chain") and `457d1f3`/`c03c220` already
       treat this chain as broken — cite them.
       Files: `docs/tooling/scene-tools/validate_scene.py`, all four scenes,
       `Assets/Project/Scripts/Vehicle/KnockableProp.cs`,
       `Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs`, `git log` for the three commits
       Verify: `python docs/tooling/scene-tools/validate_scene.py` run against each of the four
       scenes, output pasted into `findings/group-5-friction.md`; every unreachable point named.

- [ ] 16. **Camera look sensitivity, the logger early-exit, serialization drift, audio.**
       (a) Camera: `ChaseCamera.ReadSwivelInput` takes `input.CameraLook` and applies
       `_yawOffset += look.x * Tuning.cameraSwivelSpeed * dt` with a `look.sqrMagnitude > 0.0001f`
       gate — i.e. **no deadzone worth the name and no input smoothing**, and
       `cameraSwivelSpeed: 120` in all three profiles. Mouse delta is already a per-frame
       displacement, so multiplying it by `dt` makes the response frame-rate dependent as well.
       Record this as the mechanism most consistent with the "camera shifts on tiny mouse movement"
       complaint, labelled `INFERRED` from the code, with the `CameraLook` binding and any
       processors on it read from `VehicleControls.inputactions` as `READ`. Check whether the mouse
       binding has a scale/normalise processor; if not, say so.
       (b) `PlaytestLogger`: rows 4 and 5 of `Game Build/PlaytestLogs/shift_log.csv` have
       `shift_seconds=0` with `play_seconds=268.7` and `221.4` — the early-exit path. Read
       `PlaytestLogger.cs` around `:132-134` (`if (_shiftRunning && !_rowWritten) WriteRow();`) and
       `:302` (`Mathf.Round(Time.time - _shiftStartTime + shiftTimer.TimeRemaining)`) and explain
       which branch yields 0, and that **2 of 5 logged shifts are therefore partial records** —
       directly relevant because those two rows are the ones with 0 delivered.
       (c) Serialization drift — stale fields that no longer exist in code but persist in files:
       `enableMultiLegOrders`, `multiLegChance`, `minTransferLegs`, `maxTransferLegs`,
       `extraTimePerTransferLeg` and the mis-indented `whyThisSetup` in
       `OrderTuning_Balanced.asset` (none of the multi-leg fields appear in `OrderTuning.cs`);
       `headerFontSize` and `orderLineSlots` serialized in `Possible game scene.unity:2075-2076`
       and `VehicleLab.unity:9710-9711` with no matching C# field; `hardBrakeDecelThreshold`
       serialized in `Kyuri's Scene.unity:1043` (→ `Hard.unity`) — this one **does** exist in
       `CargoCondition.cs`, so verify each case before calling it stale. Report what Unity does
       with each (ignored on load, lost on next save) and that the `whyThisSetup` indentation means
       the design rationale may not be deserialising into the asset at all.
       (d) Audio: there is **no `AudioSource` reference anywhere** in the project's scripts, scenes
       or assets (searched). The game is silent. Report as `READ` (absence established by an
       exhaustive search of `Assets/Project`), and name it as friction since engine note and
       collision feedback are load-bearing for a driving game.
       Files: `Assets/Project/Scripts/Vehicle/ChaseCamera.cs`,
       `Assets/Project/Scripts/Vehicle/VehicleInput.cs`,
       `Assets/Project/Scripts/Vehicle/VehicleControls.inputactions`,
       `Assets/Project/Scripts/Vehicle/Tuning/Tuning_*.asset`,
       `Assets/Project/Scripts/Vehicle/PlaytestLogger.cs`,
       `Assets/Project/Scripts/Orders/OrderTuning.cs`,
       `Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset`,
       `Assets/Project/Scripts/Orders/CargoCondition.cs`,
       `Assets/Project/Scenes/Possible game scene.unity`, `Assets/Project/Scenes/VehicleLab.unity`,
       `<ref>/Hard.unity`, both `shift_log.csv` files
       Verify: `findings/group-5-friction.md` has one sub-section per (a)–(d); the stale-field list
       distinguishes confirmed-stale from still-present-in-code, each with its `.cs` search result.

### Consolidation

- [ ] 17. **Assemble the evidence ledger and close every contradiction.** Merge the five findings
       files into one citation ledger: every claim destined for the document, its label, its
       citation, and (for contradictions) the discriminating check and verdict. Known contradictions
       that must each carry a verdict before the document is written: 32-vs-25 m/s top speed;
       13-vs-12 m/s² sideways grip; 45/50/75 s order limit; flat-10 vs 61/158/216 value; README's
       "first scene in Build Settings"; Easy absent from build settings; `delivery_points.py` stale
       for three of four scenes; late penalty read by nothing; `TrySpendMoney` with no caller;
       QA reports vs code. Also check the curated `<ref>/playtest_sessions.csv` machine-log rows
       against `shift_log.csv` column by column — the log's `delivered,late,value` for
       `LOG-20261012-133339` is `9,7,61`, so confirm the curated row has not shifted those into
       `play_minutes,delivered,late`; if the tracked CSV is correct, say so, because an external
       draft of this spreadsheet does have them shifted.
       Files: `.agents/tasks/design-overview-2026-10-14/findings/ledger.md`,
       `findings/contradictions.md`, `<ref>/playtest_sessions.csv`, both `shift_log.csv` files
       Verify: `ledger.md` has zero rows labelled `TODO` or lacking a citation; every contradiction
       listed above appears with a verdict.

- [ ] 18. **Write `docs/DESIGN-OVERVIEW.md`.** One section per question group, in the order the
       questions were asked, each answering the sub-questions directly and leading with the answer.
       Required structure: a short "How to read this" box restating the label table from §0.3 and
       the `origin/main` authority rule from §0.2; the five sections; an "Open questions" section
       listing every `UNKNOWN` with its single settling check; a "Where the documentation is wrong"
       section naming each stale claim and its file; and a closing evidence index mapping every
       number to `path:line`.
       Hard constraints: no number without a label and a citation; no estimate; the three family
       testers appear only as investigation directions; QA reports are cited only as superseded.
       If `.agents/tasks/turning-geometry-2026-10-14/report.md` exists, **cite it** for the
       turning-circle vs corner-width vs zone-radius geometry instead of redoing it; it does not
       exist as of this plan (`.agents/` held no `tasks/` directory), so if it is still absent,
       state the geometry question as `UNKNOWN` with the check that would settle it and do not block.
       Files: `docs/DESIGN-OVERVIEW.md`
       Verify: `git status --short -- Assets ProjectSettings` prints nothing (no game file touched),
       and `git status --short -- docs` shows `docs/DESIGN-OVERVIEW.md` as the only added doc.

- [ ] 19. **Audit the citations mechanically, then re-prove the read-only baseline.** Write a small
       checker under the task's tools directory that parses `docs/DESIGN-OVERVIEW.md`, extracts every
       `path:line` citation, and fails if a cited path does not exist in the working tree or in
       `origin/main`, if a cited line number exceeds that file's line count, or if a line containing
       a bare number carries no label from the §0.3 set. This is the verification that the
       anti-fabrication standard was met; a grep is not.
       Files: `.agents/tasks/design-overview-2026-10-14/tools/audit_citations.py`
       Verify: `python .agents/tasks/design-overview-2026-10-14/tools/audit_citations.py docs/DESIGN-OVERVIEW.md`
       exits 0 with zero findings; `python docs/tooling/compile_check.py` reports `Compiles clean.`;
       `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` passes; and
       `git status --short` shows no modification to any file under `Assets/` or `ProjectSettings/`.

---

## Gaps in the brief, and the assumptions taken

1. **The brief described `origin/main`'s scene set as if it were local. It is not.** The working
   tree is `feature/vehicle-camera`, which still has `Kyuri's Scene.unity` and no
   Easy/Medium/Hard. Assumption taken: `origin/main` is authoritative (§0.2), read via extracted
   blobs because switching branches is forbidden and would disturb the developer's open editor.
2. **The `City` root trap is real but belongs to `Hard.unity` only.** In the local
   `Kyuri's Scene.unity` the `City` root is at the origin with scale 1; the
   (-6.3905, 0.4, 139.24) / 2.0439076 transform exists in `origin/main`'s `Hard.unity`. Both facts
   are now verified and item 8 is written against the `origin/main` one.
3. **`.agents/.../turning-geometry-2026-10-14/report.md` does not exist.** Per instruction, item 18
   does not block on it and treats the geometry question as `UNKNOWN` with a settling check.
4. **`VehicleLab.unity` is in build settings but disabled** (`enabled: 0`), which contradicts the
   README's "second scene in Build Settings" framing only mildly; item 3 records it rather than
   making a separate finding of it.
5. **`.agents/` is not gitignored.** Every step must avoid `git add -A`. Flagged in §0.1 rather than
   changing `.gitignore`, which would be a repo change this task is not allowed to make.
