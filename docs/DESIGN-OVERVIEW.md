# Zoom Zoom Delivery Co — design overview

What the game currently **is**, as of 14 October 2026. Written for the engineer who owns the
vehicle, camera and telemetry code. It answers five question groups: vehicle physics and handling,
map and environment, order system and economy, win/loss and progression, and known friction.

This is a description, not a change request. Nothing here proposes a tuning value.

---

## How to read this

Every number carries a label. The labels are not decoration: they say how much weight the number
can take.

| Label | Means | Carries |
| --- | --- | --- |
| `READ` | A literal value sitting in a file | `path:line`, or `path` + the YAML key |
| `MEASURED` | Recorded by an instrument run — the F1–F9 vehicle lab harness, `PlaytestLogger`, or a tooling script | the source file and row, or the exact command |
| `DERIVED` | Arithmetic on `READ`/`MEASURED` values, nothing else | the inputs, each labelled, and the operation |
| `INFERRED` | A reading of code control flow with no runtime evidence | the code path, plus the words "not observed at runtime" |
| `UNKNOWN` | Not establishable without running the game or opening the editor | the single check that would settle it |

Four rules this document holds to:

- **Nothing is estimated.** "About 2 metres" is `UNKNOWN` unless a file says so. Where the only
  available figure rests on an authored assumption rather than a measurement, the assumption is
  named at the point of use.
- **A design intention is `READ` only when a file states it.** The `///` rationale blocks in
  `VehicleController.cs` and the `whyThisSetup` strings in the tuning assets are quotable. An
  intention nobody wrote down is `UNKNOWN`, not a guess.
- **Code beats documents.** Where a prior report, a README or an asset comment disagrees with what
  the code does at runtime, the code wins and the disagreement is recorded in
  [Contradictions found](#contradictions-found) with both sources named.
- **An absence found by searching is `MEASURED`, not `READ`.** "There is no `X` anywhere" is the
  result of a regex or GUID sweep, which is a tooling run, so it carries `MEASURED` plus the search
  terms or the command. `READ` stays reserved for a literal value sitting in a file. This matters
  because a search can only be as good as its reach, and one limit of reach applies throughout:
  **a class-id or GUID census of a scene file does not see inside a `PrefabInstance`**, so every
  per-scene count below — components as well as colliders — understates a scene by whatever its
  prefab instances hold. That limit caught an earlier draft of this document out on the
  `ResultsPanel` row (§Per-scene, §4.2, C6).

### Which tree these numbers come from

`origin/main` is authoritative, because the shared game is `origin/main`. The local checkout is
`feature/vehicle-camera`, which is 1 commit ahead of the merge base and 7 behind `origin/main`, and
which still carries `Kyuri's Scene.unity` with no Easy/Medium/Hard. Reading the working tree would
describe a scene set the team no longer has.

Where a path is written `<ref>/…` it means
`.agents/tasks/design-overview-2026-10-14/ref/…`, a set of byte-identical blobs extracted from
`origin/main`. Everything under `Assets/Project/Scripts/Vehicle/`, the three `Tuning_*.asset`
files, `OrderTuning_Balanced.asset`, and the order scripts other than the four UI files are
byte-identical between `HEAD` and `origin/main` and are cited in place.

Where a path is written `<notes>/…` it means `.agents/tasks/design-overview-2026-10-14/…`, the four
evidence notes behind this document. A `<notes>` citation appears only where the raw inputs to a
`DERIVED` figure were produced by one of the untracked tools listed in §7 and are recorded nowhere
else. That is a weaker trace than a repository path, and it is flagged as such at each point of use
rather than left implicit.

### The telemetry is local, untracked, and smaller than it looks

Both shift logs are gitignored — `**/PlaytestLogs/` (`.gitignore:226`) and `/[Gg]ame [Bb]uild/`
(`.gitignore:223`), `READ`. Neither file is in the repository, so nobody else has these rows and
they can change under you. As read on 14 October 2026 there are **seven** logged shifts, not the
five an earlier pass counted: four in `Game Build/PlaytestLogs/shift_log.csv` and three in
`PlaytestLogs/shift_log.csv` (`MEASURED`, both files decoded `utf-8-sig`).

All seven name `Possible game scene`. **No logged shift comes from Easy, Medium or Hard**, and none
could have: `PlaytestLogger` is attached in `Possible game scene` only — 0 in the other three and 0
in their prefabs (`MEASURED`, §Per-scene and §4.3). The
shipped build contains exactly two scenes, `MainMenu.unity` and `Possible game scene.unity` — the
only two scene paths in `Game Build/Zoom Zoom Delivery Co_Data/globalgamemanagers`, with `level0`
and `level1` the only level files present (`MEASURED`). So the tested build predates the
Easy/Medium/Hard restructure entirely, and any balance conclusion drawn from these rows describes a
scene whose collision penalties are roughly four times harsher than the three current ones
(§3.5).

---

## Per-scene configuration

The scenes differ. A single set of numbers would be wrong for most of them, so here they are side
by side. Every value is `READ` from the cited line of that scene's `origin/main` blob unless marked
otherwise.

| | Possible game scene | Hard | Medium | Easy | MainMenu | VehicleLab |
| --- | --- | --- | --- | --- | --- | --- |
| In build settings | yes, index 1, `enabled: 1` | yes, `enabled: 1` | yes, `enabled: 1` | **absent entirely** | yes, index 0 | yes, `enabled: 0` |
| Shift length (s) | 300 (`:1713`) | 300 (`:2301`) | 300 (`:54336`) | 300 (`:5568`) | n/a | n/a |
| `startAutomatically` | 1 (`:1714`) | 1 (`:2302`) | 1 (`:54337`) | 1 (`:5569`) | n/a | n/a |
| Cargo slots | **1** (`:1959`) | 2 (`:2619`) | 2 (`:62221`) | 2 (`:6507`) | n/a | n/a |
| Pickup / drop-off radius (m) | 6 / 6 (`:783-784`) | 6 / 6 (`:1347-1348`) | 6 / 6 (`:39213-39214`) | 6 / 6 (`:3319-3320`) | n/a | n/a |
| Zone `checkInterval` (s) | 0.1 (`:785`) | 0.1 (`:1349`) | 0.1 (`:39215`) | 0.1 (`:3321`) | n/a | n/a |
| `enableCondition` | 1 (`:800`) | 1 (`:1364`) | 1 (`:39230`) | 1 (`:3336`) | n/a | n/a |
| `hardBrakeDamage` | **12** (`:806`) | 6 (`:1370`) | 6 (`:39236`) | 6 (`:3342`) | n/a | n/a |
| `collisionImpactThreshold` (m/s) | **6** (`:808`) | 10 (`:1372`) | 10 (`:39238`) | 10 (`:3344`) | n/a | n/a |
| `collisionDamagePerMps` | **4** (`:809`) | 1.5 (`:1373`) | 1.5 (`:39239`) | 1.5 (`:3345`) | n/a | n/a |
| `OrderTicketStrip` (per-order countdown UI) | **0** | 1 | 1 | 1 | — | — |
| `PlayerProgression` | **0** | 1 | 1 | 1 | — | — |
| `ShiftResults` | 1 | 1 | 1 | 1 | — | — |
| `PlaytestLogger` (writes `shift_log.csv`) | **1** | **0** | **0** | **0** | — | — |
| `ResultsPanel` | **0** | 1, in a `Canvas (2)` instance overridden to `m_IsActive: 0` (`:4223-4225`) | 1, same override (`:105846-105848`) | 1, no override — inherits the prefab's `m_IsActive: 0` | — | — |
| `SurfaceType` tags | 0 | **84** | 0 | 0 | 0 | 5 |
| `WeatherState` | 0 (→ `Dry`) | 1, `weather: 0` = Dry (`:1740`) | 1, Dry (`:45211`) | 1, Dry (`:3877`) | 0 | 0 |
| `KnockableProp` | 0 | **33** | 0 | 0 | — | — |
| `Floor` BoxCollider | 504.00 × 554.75 m, top face y = 0.00 | 579.40 × 601.85 m, top y = 0.00 (`:4453-4454`, `:4527-4528`) | 579.40 × 601.85 m, top y = 0.00 | **none at all** | n/a | generated |
| City container / uniform scale | `Group 15 Greybox real one 1` prefab instance | `City`, **2.0439076** (`:187643`) | `'City '` (trailing space), **1.2333** (`:177909`; object named at `:165599`) | `Group 15 Greybox real one 1`, **8.976594** (`:15888`) | n/a | n/a |
| Vehicle tuning asset | `Tuning_Balanced` | `Tuning_Balanced` | `Tuning_Balanced` | `Tuning_Balanced` | n/a | `Tuning_Balanced` |
| Order tuning asset | `OrderTuning_Balanced` | same | same | same | n/a | n/a |

Component counts are `MEASURED` by script-GUID occurrence over each blob: `OrderTicketStrip`
`d62a920a72c9db04fa4abaf122673c94`, `PlayerProgression` `cdd06e60aa8338a4bb39e087cbb6958d`,
`ShiftResults` `8f3e2a1c9b7d4e6fa05c1d2e3f4a5b6c`, `ResultsPanel`
`afcd8a025ccf16d4d85203c80cf90775`, `PlaytestLogger` `9c2d4e6f8a1b4c3d9e5f7a8b0c1d2e3f`,
`KnockableProp` `5d8c1f2a3b4e4c6d9a7b8e0f1c2d3e4f`,
`SurfaceType` `95c5eae1aeacb5341adbe50a1c9f4a67`, `WeatherState`
`7a3e5c1d9f2b4a6e8c0d1e2f3a4b5c6d`. §4.3 adds three more for the `OrderResolved` subscriber census:
`CargoSystem` `250347cebb58b514babc1e4b307d187f`, `OrderHUD` `051cd210914a68e42b68cbbfd39ed9f3`,
and `PlaytestLogger` above — each 1 in all four gameplay scenes except `PlaytestLogger`, which is
`Possible game scene` only.

**The census is blind to prefab contents, and that is not only a collider problem.** A class-id or
GUID sweep of a scene file cannot see inside a `PrefabInstance` (C6), so each scene's prefab
instances have to be resolved by hand and added:

| Scene | Prefab instances | What they add |
| --- | --- | --- |
| `Hard` | 2 — `Border.prefab` (`:2587`), `Canvas (2).prefab` (`:4311`) | 89 GameObjects + 84 MeshColliders; 12 GameObjects + 1 `ResultsPanel` |
| `Medium` | 2 — `Canvas (2).prefab` (`:105934`), `Border.prefab` (`:169961`) | the same two sets |
| `Easy` | 1 — `Canvas (2).prefab` (`:22541`) | 12 GameObjects + 1 `ResultsPanel`; Border's objects are inline here, not instanced |
| `Possible game scene` | 1 — `Group 15 Greybox real one 1.prefab` (`:3415`) | 96 GameObjects + 90 MeshColliders, 0 Rigidbodies |

`MEASURED` — each `m_SourcePrefab` GUID resolved against `origin/main`, then the prefab blob
censused by class id: `Border.prefab` GUID `70f442799ea311641904614d76bbbf24` at
`origin/main:Assets/Project/Scenes/Border.prefab`, 89 / 84; `Canvas (2).prefab` GUID
`16e1a52f888ac16458f58d73070092a7`, 12 GameObjects, 0 MeshColliders. Neither prefab contains the
`OrderTicketStrip`, `PlayerProgression`, `ShiftResults` or `PlaytestLogger` GUID (`MEASURED`, 0
occurrences of each in both), and nor does `Possible game scene`'s
`Group 15 Greybox real one 1.prefab` (`MEASURED`, same four GUIDs, 0 each), so those four rows stand
as counted. The **`ResultsPanel` row does not**: the scene-level
census returns 0 in all four scenes, and in three of them the component is in the game anyway,
inside `Canvas (2).prefab` — see §4.2.

Two further caveats. The four gameplay-scene columns were re-read directly from the `<ref>`
blobs for this document; the MainMenu and VehicleLab columns come from the investigation notes and
were not re-verified here. `Floor` dimensions are `DERIVED` from the BoxCollider's `m_Size` and
`m_Center` times the resolved world scale, not read as a literal. Hard's inputs, all `READ`: the box
is a unit cube at the object's origin (`m_Size: {x: 1, y: 1, z: 1}` at `<ref>/Hard.unity:4527`,
`m_Center` all zero at `:4528`), the Transform sits at local y = −1 (`:4453`) with scale
(579.4047, 2, 601.85046) (`:4454`), and both ancestors are identity — `Generated (do not edit by
hand)` at `:771-772` and the scene root above it at `:3142-3143` — so local resolves to world
unchanged. The box therefore spans y[−2.00, 0.00] and **the top face is exactly y = 0.00**. Medium's
`Floor` is the same object with the same numbers (`<ref>/Medium.unity:106527-106528` under the same
identity container at `:33641-33642`), which is why the two columns match. `Possible game scene`'s is
the same construction at a smaller size — local y = −1 with scale (504, 2, 554.752) at
`<ref>/Possible game scene.unity:2874-2875` — so its top face is y = 0.00 too.

Build settings `READ` from `origin/main:ProjectSettings/EditorBuildSettings.asset`, which lists
exactly six paths: `MainMenu.unity` (1), `Possible game scene.unity` (1), `VehicleLab.unity` (0),
`Assets/Scenes/SampleScene.unity` (0), `Medium.unity` (1), `Hard.unity` (1).

**Three things in that table deserve to be read twice.**

**`Easy.unity` is not in build settings at all, it has no `Floor` object, and the menu button for it
cannot work.** The difficulty buttons are wired to `DifficultySceneLoader`, not `MainMenuManager` —
`MainMenu.unity`'s `m_MethodName` set is `BackToMainMenu, ExitGame, LoadEasy, LoadHard, LoadMedium,
OpenControlsPanel, OpenDifficultyPanel` (`READ`, verified on `origin/main`), and
`DifficultySceneLoader` loads **by name** from serialized strings `easySceneName = "Easy"`,
`mediumSceneName = "Medium"`, `hardSceneName = "Hard"` (`READ`). `Load` guards with
`if (!Application.CanStreamedLevelBeLoaded(sceneName))`, logs an error naming the scene, and
returns. `INFERRED` from that path, not observed at runtime: pressing Easy logs
`Cannot load scene "Easy"` and does nothing, while Medium and Hard work because they are in
`m_Scenes` with `enabled: 1`. It fails safely rather than crashing — but it fails silently to the
player, because the only feedback is a console error.

`Possible game scene` is the only scene with one cargo slot, the only scene without a per-order
countdown strip, the only scene without progression, and the harshest scene for collision damage.
It is also the only scene anyone has telemetry for.

Order timing and value are **global**, not per-scene: all four scenes point their `OrderManager` at
the same `OrderTuning_Balanced.asset`, GUID `d45a48eeca7d4364896ca1448c017ed2`
(`PGS:1664`, `Hard:2252`, `Medium:54287`, `Easy:5519`, `READ`). There is no per-scene override of
spawn interval, active cap, order time limit or delivery value. Easy, Medium and Hard differ in
geometry, cargo slots and collision punishment — not in the clock.

---

## 1. Core vehicle physics and handling

### 1.1 Arcade, by construction, and the code says so

Strictly arcade. There is no sim-cade element anywhere in the mechanism.

The car is a single `Rigidbody` held up by four downward raycasts, driven entirely by
`ForceMode.Acceleration` forces and torques. There is **no `WheelCollider` anywhere in the
project** (`MEASURED` — 6 hits for the type name across `Assets`, every one of them inside a `///`
comment block explaining why it is not used, at `VehicleController.cs:77,79,80,82`,
`SurfaceType.cs:45` and `VehicleTuning.cs:87`, `READ` at those lines). The
header states the design in its own words: everything is applied as forces and torques, never by
moving the transform (`VehicleController.cs:94-99`, `READ`).

PhysX contact friction is deliberately not part of handling. The car's body never rubs the floor,
so a `PhysicMaterial` on the road changes nothing (`SurfaceType.cs:40-55`, `READ`).

The force budget per physics step, in fixed order — read the ground, hold the car up, gravity,
drive/brake, steer, grip, limits (`VehicleController.cs:94-96`):

| What | Call | Line |
| --- | --- | --- |
| suspension spring + damper, at each wheel | `AddForceAtPosition(up * accel, origin, Acceleration)` | `:718` |
| gravity, its own, not the project's | `AddForce(Vector3.down * tuning.gravity, Acceleration)` | `:407` |
| drive / reverse / brake / coast, one signed number | `AddForce(GroundForward * totalAccel, Acceleration)` | `:823` |
| boost, a separate push, grounded or airborne | `AddForce(GroundForward * boostAcceleration, Acceleration)` | `:878` |
| steering, as a commanded yaw rate about the ground normal | `AddTorque(GroundNormal * yawAccel, Acceleration)` | `:1001` |
| lateral grip, per axle, half each — the shipped path | `AddForceAtPosition(GroundRight * …, axlePos, Acceleration)` | `:1121-1124` |
| anti-softlock righting torque | `AddTorque(axis * rightingTorque, Acceleration)` | `:1163` |
| jump, instant velocity change | `AddForce(up * jumpSpeed, VelocityChange)` | `VehicleJumpFlip.cs:208` |
| flip, direct velocity and angular-velocity writes | `_rb.linearVelocity = pushed + hop` | `VehicleJumpFlip.cs:287,294` |

All `READ`. Per-wheel slip is computed and published for skid marks and tyre effects
(`VehicleController.cs:487-507`) but does not feed the physics: cornering forces are computed per
**axle** (`ApplyAxleGrip`, `:1100-1127`). So four-wheel raycast suspension, a two-axle tyre model,
no friction curve, no load sensitivity. `INFERRED` from that control flow, not observed at runtime:
one wheel dropping into a gutter changes suspension and the surface blend, but not the grip split.

### 1.2 How heavy the car feels — and why mass is not the answer

**Changing `mass` does not change the handling.** Every handling force is
`ForceMode.Acceleration`, which ignores mass. The header says so outright: acceleration mode
ignores mass, so changing the mass in the tuning asset does not secretly change the handling
(`VehicleController.cs:96-98`, repeated at the field, `VehicleTuning.cs:64-68`), `READ`. `mass`
only changes how hard the car shoves other rigidbodies.

The weight is in the suspension and the centre of mass:

| Quantity | Value | Label | Source |
| --- | --- | --- | --- |
| mass | 900 kg | `READ` | `Tuning_Balanced.asset:33`; also on the Rigidbody, `Assets/Project/Prefabs/Car.prefab:72` |
| linear damping | **0** | `READ` | `Tuning_Balanced.asset:37`; the tooltip asks for 0 so the car's own coast and top-speed numbers are the only things slowing it (`VehicleTuning.cs:84`) |
| `angularDamping` | 2.5 | `READ` | `Tuning_Balanced.asset:36` |
| `centreOfMassOffset` | (0, −0.35, 0) m local | `READ` | `Tuning_Balanced.asset:34` |
| gravity | **20 m/s² down** | `READ` | `Tuning_Balanced.asset:32`; applied `VehicleController.cs:407` |
| `useGravity` | false — project gravity switched off | `READ` | `VehicleController.cs:339` |
| downforce, anti-roll bar | **do not exist** | `MEASURED` | 0 matches for `downforce`, `downForce` or `antiRoll` in any `.cs` under `Assets` |
| max angular speed | 7 rad/s | `READ` | `Tuning_Balanced.asset:35`; set `VehicleController.cs:343` |
| max linear velocity | 100 m/s | `DERIVED` | `max(topSpeed 32, boostTopSpeed 40) × 2.5`, `VehicleController.cs:347-350` |
| `suspensionRestLength` | 0.60 m | `READ` | `Tuning_Balanced.asset:43` |
| `springStrength` / `springDamper` | 110 / 14, per wheel | `READ` | `Tuning_Balanced.asset:45` and `:46` |
| static ride height | 0.555 m | `DERIVED` | each wheel carries gravity/4 = 5 m/s²; 5 ÷ 110 = 0.045 m compression; 0.60 − 0.045 |
| suspension damping ratio | ζ ≈ 0.67, underdamped | `DERIVED` | ω = √110 = 10.49 rad/s; critical 2ω = 20.98; 14 ÷ 20.98 |

Both suspension derivations depend on the spring being in acceleration units rather than newtons,
which is `READ` at `VehicleController.cs:713-714` — `accel = tuning.springStrength × compression −
tuning.springDamper × upwardSpeed`, applied with `ForceMode.Acceleration` at `:718`. Read as a
spring rate in N/m the same two numbers would mean something else entirely, and both figures above
would be wrong.

Load transfer is real and unscripted: the spring force is applied at the wheel position, so the
body squats and leans from the same forces that hold it up, and the code says that is why
(`VehicleController.cs:696-719`, `READ`).

**The physics rate is 120 Hz, and the car sets it.** `ProjectSettings/TimeManager.asset` says
`Fixed Timestep: 0.02` (50 Hz), but `Tuning_Balanced.asset:31` carries `physicsHz: 120` with
`overrideFixedTimestep: 1`, and `ApplyTuningToBody` writes `Time.fixedDeltaTime = 1f / physicsHz`
(`VehicleController.cs:365`) from `Awake` and again from every `FixedUpdate` (`:320`, `:394`). All
`READ`. `INFERRED`, not observed at runtime: a scene with no `VehicleController` in it — MainMenu —
runs at the project's 50 Hz, and `Time.fixedDeltaTime` is a global that stays at 1/120 for the rest
of the session once set.

### 1.3 Grip versus drift

**Lateral grip is a budget in m/s², not a friction coefficient.** Each step the car works out how
much sideways acceleration would cancel the slide outright and spends up to the budget on it. If a
corner asks for more, the car runs wide by exactly the difference, every time
(`VehicleController.cs:1007-1020`, `READ`).

Steering commands a **curvature**, not an angle, so turn rate falls out as `curvature × speed`
(`VehicleController.cs:968-1002`). With `steerFromGripBudget: 1` the available curvature is itself
derived from the grip budget (`VehicleTuning.cs:719-741`, `READ`):

```
curvature = min( (lateralGripAcceleration × overdrive(v)) / v² , maximumCurvature )
overdrive(v) = lerp(0.6, 1.15, clamp01(v / max(topSpeed, boostTopSpeed)))
```

| Field | Balanced | Label | Source |
| --- | --- | --- | --- |
| `lateralGripAcceleration` | **13 m/s²** | `READ` | `Tuning_Balanced.asset:169` (C# default is 12, `VehicleTuning.cs:297`) |
| `gripBalance` | 0.58, front-biased → the rear lets go first | `READ` | `Tuning_Balanced.asset:241` |
| `perAxleGrip` | true — the two-force model ships | `READ` | `Tuning_Balanced.asset:240` |
| `useFrictionCircle` / strength / `longitudinalGripReference` | true / 0.8 / 16 m/s² | `READ` | `Tuning_Balanced.asset:233`, `:234`, `:235` |
| `maximumCurvature` | 0.35 (1/m) → 2.86 m tightest radius | `READ` / `DERIVED` | `Tuning_Balanced.asset:239`; radius = 1/0.35 |
| `driftYawAuthority` | 0.55 — steering keeps 55 % authority mid-drift so counter-steer works | `READ` | `Tuning_Balanced.asset:243`; applied `VehicleController.cs:991-993` |
| drift test | grounded **and** ≥ 4 m/s **and** slip angle ≥ 12° | `READ` | `VehicleController.cs:681-683`; `Tuning_Balanced.asset:244-245` |
| `handbrakeRearGripMultiplier` | 0.15 | `READ` | `Tuning_Balanced.asset:242` |
| `handbrakeDeceleration` | 8 m/s² | `READ` | `Tuning_Balanced.asset:232` |

So: it grips, until you touch a pedal. `DERIVED` from the above, with the friction circle applied
to the **rear axle only** because the rear is the driven axle — that asymmetry is power oversteer
(`VehicleController.cs:1078-1082`, `READ`):

| What the driver is doing | long. demand | front grip | rear grip | effective lateral |
| --- | --- | --- | --- | --- |
| coasting (`coastDeceleration` 5) | 5 | 15.08 | 10.48 | **12.78** |
| quarter brake | 10.5 | 15.08 | 8.78 | 11.93 |
| **half brake or more** | ≥ 16 | 15.08 | **2.18** | **8.63** |
| full throttle at 10 m/s | 18.95 | 15.08 | 2.18 | 8.63 |
| full throttle at 25 m/s | 7.54 | 15.08 | 9.89 | 12.48 |
| handbrake held, no throttle | 13 | 15.08 | **1.09** | — |

`DERIVED`. The demand column and the grip columns come from different formulas on different inputs,
so both are given.

The **grip columns** come from `lateralGripAcceleration` 13 (`Tuning_Balanced.asset:169`),
`gripBalance` 0.58 (`:241`), `frictionCircleStrength` 0.8 (`:234`) and `longitudinalGripReference`
16 (`:235`), through `LateralGripAfterLongitudinal` (`VehicleTuning.cs:755-766`):
`grip × lerp(1, √(1 − (demand/16)²), 0.8)`.

The **longitudinal-demand column** comes from `VehicleTuning.AccelerationAt`
(`VehicleTuning.cs:690-704`), which with `accelerationFromFormula: 1` evaluates
`launchAcceleration × (1 − (v / topSpeed)^accelerationFalloffPower)` — `launchAcceleration` 22
(`Tuning_Balanced.asset:220`), `topSpeed` 32 (`:50`), `accelerationFalloffPower` 1.7 (`:221`). So
`22 × (1 − (10/32)^1.7) = 18.95` at 10 m/s and `22 × (1 − (25/32)^1.7) = 7.54` at 25 m/s, and the
same expression crosses the 16 m/s² reference at 14.90 m/s. The brake and coast rows take their
demand from `brakeDeceleration` 22 (`:105`), `coastDeceleration` 5 (`:106`) and
`handbrakeDeceleration` 8 (`:232`) instead, summed into `stoppingAccel` as §1.7 describes.

**The consequence worth carrying into a design conversation:** coasting already spends 5 of the
16 m/s² longitudinal reference, so any brake at or above half travel, and full throttle anywhere
below about 14.9 m/s, saturates the circle and cuts the rear axle to 20 % of its share. Because the
loss is rear-only, braking into a corner does not push the nose wide — it swings the tail, and past
12° of slip `driftYawAuthority` halves the steering's ability to catch it. That is a legible path
from "I braked for the drop-off" to "I ended up facing the wrong way", and reverse is the recovery.
`INFERRED` from the code path; the parameters and arithmetic are above, the feel is not statically
measurable.

The handbrake does three things, not one (all `READ`): cuts rear grip directly by ×0.15
(`VehicleController.cs:1087`); adds 8 m/s² of drag into the same `stoppingAccel` as the footbrake
(`:788-790`); and floors that drag into `LongitudinalDemand` with `Mathf.Max(|totalAccel|,
handbrakeDrag)` so throttle-plus-handbrake cannot report near-zero tyre work (`:802-819`).

### 1.4 Turning geometry, and what it has to fit

Full workings are in `.agents/tasks/turning-geometry-2026-10-14/report.md`; cited here rather than
redone.

The achievable radius is the wider of the steering curvature and the grip limit. Two independent
derivations — one in that report §3.1, one from `VehicleTuning.MaxCurvatureAt` — agree, and the one
point where either can be checked against a lab reading agrees to 0.1 m:

| speed | steering radius | grip radius | achievable | circle |
| --- | --- | --- | --- | --- |
| ≤ 4 m/s | 2.86 (cap binds) | 0.08–1.23 | **2.86 m** | **5.71 m** |
| 9.84 m/s | 10.13 | 7.45 | 10.13 m | 20.26 m |
| 15 m/s | 21.47 | 17.31 | 21.47 m | 42.93 m |
| 20 m/s | 35.16 | 30.77 | 35.16 m | 70.33 m |
| 32 m/s | 75.74 | **78.77** | 78.77 m | 157.54 m |
| 40 m/s | 107.02 | **123.08** | 123.08 m | 246.15 m |

`DERIVED`. Crossover at 29.1 m/s: below it the steering curve is the limit and the car grips, above
it full lock asks for more grip than the tyres have.

**The "22.1 m turning circle" figure is a U-turn at 9.84 m/s and is being read wrongly.** The lab
records `Turning circle width 22.0967 m` at `Test speed held 9.8399 m/s`, radius 11.0484 m,
`Limiting factor 0` = steering not grip (`MEASURED`,
`VehicleLab_Measurements/vehicle_measurements.csv`, latest reading 2026-10-07 13:56:32). The
steady-state circle at that speed is 20.26 m; the extra 1.8 m is the entry transient, because
`MeasureTurningCircle` records the widest span of the **first** lap and the car spirals in. So
22.1 m is the figure for "room to U-turn at 10 m/s" and 11.05 m is the figure for a junction. Below
5 m/s the `maximumCurvature` cap binds and the circle is 5.71 m — a quarter of the headline number.

**It has been measured at exactly one speed, ever.** `turnTestSpeed` is 10 in every scene that has
the harness, and all 15 turning-circle rows in the CSV are three identical runs at 9.8399 m/s
(`MEASURED`). The table above is arithmetic from the shipped asset, not a measurement.

Against the level geometry, per the same report (§4.2–4.6), `MEASURED` for the corridor widths and
`DERIVED` for the speeds through a stated right-angle model (`R ≤ 1.1716 × (W/2 − 0.8)`, car
treated as a point with 0.8 m clearance, so the speeds are optimistic by roughly the extra 0.25 m a
2.8 m wheelbase sweeps):

| Scene | tightest corridor on a route to an anchor | fastest 90° turn there |
| --- | --- | --- |
| `Possible game scene` | 25.5 m | 11.8 m/s |
| `Medium` | 13.82 m module | 8.1 m/s |
| `Hard` | 8.18 m module, 5.0 m locally | **5.8 m/s, or impossible** |
| `Easy` | 8.0 m at `Pickup 03 North East Corner` | **5.8 m/s** |

Two readings follow. **In the scene that was actually played there is no corner the turning circle
cannot take** — the tightest route bottleneck is 25.5 m against a 20.9 m circle at 10 m/s, and the
minimum 5.71 m circle needs only a 6.48 m corridor. **In Hard and at one Easy anchor the geometry
genuinely caps cornering at 5.8 m/s**, against the 20 m/s working average the order timer is costed
on — a three-to-fourfold mismatch. On Hard's 5 m stretches the required radius is 1.99 m, below the
2.86 m the curvature cap allows, so a right-angle turn there is impossible at any speed and the car
must use the slab beside the road. A fix sized for one scene is wrong for the others.

On the zone side, there is nothing to overshoot: `ZoneDetection` has **no speed gate, no slow-down
requirement and no dwell time** in its 124 lines (`ZoneDetection.cs`, `READ`; 124 in both `HEAD`
and `origin/main`, where the file is byte-identical). Driving through a
zone at 32 m/s delivers exactly as parking in it does. The 6 m radius is comfortably enterable and
stoppable-in — from 20 m/s the car stops in 7.41 m against a 12 m chord (`DERIVED` at 27 m/s²).

### 1.5 Boost, jump and flip

**Boost is earned, never regenerated.** All `READ` from `Tuning_Balanced.asset` at the cited lines:

| Field | Value | Line |
| --- | --- | --- |
| `boostCapacity` | 100 units | `:225` |
| `boostConsumptionRate` | 33.3 units/s while held | `:226` |
| `boostRechargeRate` | **0** on all three profiles | `:227` |
| `boostEarnedFromDrift` | 20 units/s | `:229` |
| `boostEarnedFromAir` | 10 units/s | `:230` |
| `boostAcceleration` | 16 m/s², on top of drive, grounded or airborne | `:224` |
| `boostTopSpeed` | 40 m/s | `:223` |
| `supersonicThreshold` | 38 m/s | `:231` |

The recharge branch is gated on `> 0f` (`VehicleController.cs:885`), so it never runs; the fields
are kept as a documented A/B switch (`VehicleTuning.cs:205-210`), `READ`. The tank starts full in
`Awake` (`:322-325`). `DERIVED`: a full tank is **3.00 s** of boost (100 ÷ 33.3, measured at
3.0083 s), 5.0 s of drifting fills an empty tank, 10.0 s of air time does, and supersonic is
unreachable on throttle alone because 38 > `topSpeed` 32.

**What counts as "drifting" for earning is the controller's own `IsDrifting`, which reads nothing
about the handbrake.** The test is grounded, ≥ 4 m/s, slip angle ≥ 12°
(`VehicleController.cs:677-683`); `EarnBoost` (`:910-932`) never consults `Drive.handbrake`, `READ`.
The handbrake is one way to reach that state, not the state itself — a throttle-induced slide pays
identically. Air earning needs **all four** wheels off the ground (`WheelsOnGround == 0`, `:917`)
and pays **nothing during a flip**, because "a recovery move that refuels the car is a loop, not a
recovery" (`:905-908`, `:919-920`), `READ`. Boost is excluded from `LongitudinalDemand`, so it never
spends grip — which makes it the tool for *holding* a slide rather than starting one (`:808-813`).

Jump is grounded-only and launches along the **ground normal**, not world up, so a ramp launches
along the ramp (`VehicleJumpFlip.cs:196-204`, `READ`). `jumpSpeed` 6.5 m/s as a `VelocityChange` so
mass cannot change the height (`Tuning_Balanced.asset:171`); hold 0.18 s (`:172`) at 18 m/s²
(`:173`) against gravity 20 (`:32`), cooldown 0.35 s (`:174`); a 0.12 s input buffer (`:175`) means
a press just before touchdown fires on landing. `DERIVED`: height from the impulse alone is
**1.06 m** (`PredictedJumpHeight()` = 6.5² ÷ (2 × 20), `VehicleTuning.cs:816-820`).

Flip is the recovery move, grounded-only (`flipRequiresGrounded` true at
`Tuning_Balanced.asset:176`, enforced `VehicleJumpFlip.cs:251-255`), 9 m/s of instant velocity
change (`flipForce`, `:177`) plus a 2.5 m/s hop along the ground normal (`flipHopSpeed`, `:178`) and
2.5 rad/s of spin (`flipSpin`, `:179`), 0.45 s duration (`flipDuration`, `:180`), **1.6 s cooldown**
(`flipCooldown`, `:181`), direction stick-relative against the car's own
`GroundForward`/`GroundRight` (`VehicleJumpFlip.cs:321-337`). It is speed-governed:
`allowedGain = clamp(12 − speedBefore, 0, 3)` from `flipNoSpeedGainAboveSpeed: 12` (`:183`) and
`flipMaxSpeedGain: 3` (`:182`), and over the cap **the direction is kept and the extra speed thrown
away** (`:268-282`), `READ`.
The lab confirms it is not a shortcut: 4.4439 m of displacement in 0.4583 s against 4.6895 m if the
player had simply driven, flagged `Flip is not a shortcut = 1 (PASS)`, speed 10.2316 → 12.0 m/s, a
gain of 1.7684 m/s against the 3 m/s cap (`MEASURED`, 2026-10-07 13:56:41).

Bindings (`READ`, `VehicleInput.cs:218-234`): handbrake `Space` / gamepad LB, boost `Left Alt` / RB,
jump `Left Shift` / A, flip `Q` + WASD / X + stick.

**Nobody has used any of it.** `boost_activations` is **0 in all seven logged rows** of both shift
logs, as are `boost_seconds`, `boost_earned`, `handbrake_seconds` and `flips` (`MEASURED`). Boost
exists, is reachable, has a labelled on-screen gauge (`BoostGauge.cs:99-128` writes
`BOOST +DRIFT`, `BOOST +AIR`, `BOOST SUPERSONIC`, `BOOST EMPTY drift or jump to earn`), and has
never been activated in a logged shift.

### 1.6 Surfaces and weather

`SurfaceType` is a component on a collider, found through the wheel raycast's own hit and cached per
collider, nulls included, so an unlabelled floor is queried once (`VehicleController.cs:541-556`,
`READ`). Lookup is `GetComponentInParent`, so labelling a parent covers its children. An
**unlabelled collider returns plain tarmac on purpose**, so untagged ground drives exactly as the
car did before surfaces existed (`SurfaceType.cs:113-131`, `READ`).

`SurfaceKind` is `Tarmac, Wet, Dirt, Grass, Ice, Metal, Concrete` (`SurfaceType.cs:10-21`). All
seven profiles as written, `READ`:

| kind | grip | drive | brake | rolling resistance | line |
| --- | --- | --- | --- | --- | --- |
| Tarmac (default) | 1.00 | 1.00 | 1.00 | 0 | `:119-128` |
| Wet | 0.60 | 0.85 | 0.65 | 0 | `:161-174` |
| Dirt | 0.55 | 0.75 | 0.70 | 2.5 | `:176-189` |
| Grass | 0.45 | 0.60 | 0.60 | 5.0 | `:191-204` |
| Ice | 0.18 | 0.45 | 0.25 | 0 | `:206-219` |
| Metal | 0.80 | 0.95 | 0.85 | 0 | `:221-234` |
| Concrete | 0.92 | 0.97 | 0.93 | 0 | `:236-258` |

`gripMultiplier` scales `lateralGripAcceleration`, `driveMultiplier` scales forward acceleration,
`brakeMultiplier` scales brake **and** handbrake, and `rollingResistance` is an absolute extra m/s²
added to `stoppingAccel` even under power (`VehicleController.cs:750,776,790,797`). Values are
averaged over the **grounded wheels only**, so a boundary is a transition the player drives through
rather than a snap (`:558-621`); airborne, the blend holds the last surface.

**Weather is wired but inert.** `WeatherKind` is `Dry, Rain, Snow, Ice`; `ApplyWeather` runs once on
the blend as its last step (`VehicleController.cs:621`) and its `Dry` branch is `return ground;`
with no mutation (`SurfaceType.cs:313-314`), `READ`. `WeatherState.Current` returns `Dry` when no
instance exists (`WeatherState.cs:34`). Hard, Medium and Easy each carry one `WeatherState` with
`weather: 0` = Dry; `Possible game scene` has none. Rain/Snow/Ice are reachable only by changing one
inspector value, and nothing in the game changes it.

**Only three of the seven profiles are tagged anywhere, and the tags are almost all on things that
are not ground.** Hard's 84 `SurfaceType` components are 2 × Tarmac, 22 × Grass and 60 × Concrete
(`MEASURED`), with serialized multipliers matching `DefaultsFor` so nobody hand-edited them. But
the Grass tags are on park bases and the Concrete tags on building base meshes, and **neither
Tarmac tag is on a collider the car can touch**: one is `Roads_Parent`, whose MeshCollider
references no mesh (`<ref>/Hard.unity:199870`, `m_Mesh: {fileID: 0}`, `READ` — verified directly),
and the other is a palette tile 48 m below the map. The `Floor` box carries no `SurfaceType` at all,
so it falls through to the Tarmac default. Medium, Easy and `Possible game scene` have zero tags.
`INFERRED`, not observed at runtime: the whole drivable area behaves as untagged tarmac, and Wet,
Dirt, Ice and Metal are unreachable in play. §6 names the one check that settles it.

### 1.7 What has actually been measured

`VehicleLab_Measurements/vehicle_measurements.csv` is tracked, append-only history: 640 data rows,
**all `Balanced`**, 43 distinct `(profile, test, reading)` triples, newest session 2026-10-07
13:56 (`MEASURED`). `Tuning_Grippy` and `Tuning_Loose` are referenced by **no scene or prefab**
(`MEASURED`, GUID search over every `.unity`/`.prefab`) and have **zero** recorded readings. Every lab
number describes the shipped car.

One caveat that changes how three figures should be quoted: the `Profile` test does not drive the
car. It echoes tuning fields straight into the CSV (`VehicleMeasurement.cs:981-987`, `READ`). So
`Profile / Top speed = 32` is a `READ` of the asset that happens to live in a CSV. The driven tests
are Stopping distance, Turning circle, Wall recovery, Flip distance, Boost and Camera road ahead.

| Reading | Value | Label |
| --- | --- | --- |
| Braking distance from 31.3653 m/s | **18.0948 m** in 1.1583 s | `MEASURED` |
| Turning circle width at 9.8399 m/s | 22.0967 m, radius 11.0484 m, steering-limited | `MEASURED` |
| Peak speed on boost | 40.1278 m/s | `MEASURED` |
| Full tank duration | 3.0083 s | `MEASURED` |
| Time to cross throttle top speed on boost | 1.1083 s; to the ceiling 1.6 s | `MEASURED` |
| Distance on one tank from standstill | 94.0301 m | `MEASURED` |
| Boost earned by a 2.5 s slide at 10 m/s | 37.5 units over 1.875 s counted as drifting | `MEASURED` |
| Wall recovery from 19.9 m/s | keeps 8.4018 m/s through impact; 1.7583 s total | `MEASURED` |
| Camera road ahead at 31.2167 m/s | furthest visible ground 42.7612 m, nearest −8.579 m, both checks PASS | `MEASURED` |

The 37.5 units over 1.875 s is exactly 1.875 × 20, so the 20 units/s drift rate is confirmed by
measurement and not only by the asset (`DERIVED` cross-check).

**Why the measured stop beats the textbook figure.** The harness notes "textbook value 22.36 m,
difference −19.1 %". The textbook figure divides by `brakeDeceleration` alone
(`VehicleMeasurement.cs:347`), but the brake phase drives `throttle = 0, brake = 1` (`:341`), and
with zero throttle the controller also adds `coastDeceleration` into the same `stoppingAccel`
(`VehicleController.cs:771` then `:776`). Real deceleration is **22 + 5 = 27 m/s²**, giving
31.3653² ÷ 54 = 18.22 m against 18.0948 m measured, within 0.7 % (`DERIVED`). Every "textbook"
stopping number in the project is about 23 % long for the same reason, including
`PredictedStoppingDistanceFromTopSpeed()` (`VehicleTuning.cs:795-800`). Independent corroboration
that 27 is the right number: `CargoCondition.cs:60` defaults its hard-brake threshold to exactly
`27f`.

**There is no top-speed test.** F1 accelerates only to `brakeTestSpeedFraction` = 0.98 of the asset
value (`VehicleMeasurement.cs:71`, used `:314`), which is where the 31.3653 m/s entry speed comes
from. Whether the car reaches 32.0 m/s, and in what distance, is `UNKNOWN` — see §6.

---

## 2. Map and environment

### 2.1 Layout, per scene

There is no single city. Four scenes carry a drivable map and the twelve delivery anchors, and they
share almost nothing but the anchors' names.

| | `Possible game scene` | `Hard` | `Medium` | `Easy` |
| --- | --- | --- | --- | --- |
| GameObjects in the scene file | 40 | 1986 | 1982 | 219 |
| MeshColliders in the scene file | 0 | 1295 | 397 | 168 |
| Art kit | greybox, in a prefab instance | `road_*` city kit, laid on a module | Toon City Pack two-lane pieces | the same greybox, re-placed |
| Street module | kerb spacing 8.8–25.5 m, kerbs 0.24 m tall | **8.18 m** single, 16.35 m double | **13.82 m** two-lane | no road geometry at all |
| Layout character | greybox blocks | tight regular grid | irregular, hand-placed | 54 kerbs, 24 cubes, 4 buildings, no roads |

`MEASURED` for the counts (document-header census per class id) and for the module figures. The two
count rows are **scene-file figures only** and understate every scene by its prefab instances — add
101 GameObjects and 84 MeshColliders to Hard and to Medium (89/84 from `Border.prefab` plus 12/0
from `Canvas (2).prefab`), 12 GameObjects to Easy, and 96 GameObjects and 90 MeshColliders to
`Possible game scene` (§Per-scene).

**Hard is a laid grid.** Tile-centre spacing is dominated by one module on both axes, and every one
of the 650 `^road_` tiles has the same world scale, 204.391 (`MEASURED`). Two independent methods
agree on the width: tile-centre clustering gives 8.18 m, and mesh bounds × transform scale gives
`4.0000 FBX units × 0.01 × 204.391 = 8.176 m` — where the 0.01 import factor is itself stated in
the repo (`KnockablePropBaker.cs:20`, "authored in centimetres", `READ`). **Weak trace on the mesh
side:** the `road_base` bound of 4.0000 FBX units, the `road_double_base` bound of 8.0000 and the
0.1000 slab thickness used below are recorded only at `<notes>/notes-map.md:185` and `:189`, from an
untracked `fbx_mesh_bounds.py` run (§7), and the FBX is not in the local tree — the same weakness
the footbridge figure in §2.2 carries. The tile-centre clustering supports the 8.18 m on its own;
the mesh figure is agreement, not the foundation. The only wide pieces are 16 `road_double_*` and
`road_connector_*` items against 582 single `road_base` tiles (`MEASURED`). There is no open
highway.

**Medium is hand-placed.** 63 distinct x lines and 67 z lines from 246 road tiles with no dominant
spacing, against Hard where one spacing accounts for a large plurality of lines (`MEASURED`).
`INFERRED`: Medium's roads were placed piece by piece. Its 13.82 m module in the table above is
`DERIVED` as `11.2048 FBX units × 1.2333` world scale, and the 13.82 m also appears in the measured
tile-spacing histogram, which is the cross-check (`MEASURED`). Same weak trace as Hard's: the
11.2048 is recorded only at `<notes>/notes-map.md:206`.

**Easy has no road geometry.** Its complete inventory is 84 `wired-fence`, 54 kerbs, 24 `Cube`,
4 `Building One`, a door cluster, the twelve anchors and the system objects. Zero objects named
`road*` (`MEASURED`).

### 2.2 Verticality — the city is flat

`DERIVED` from the pivot distributions. Hard's 650 `^road_` pivots sit at four heights, and the
buckets account for all 650 (`MEASURED` — world y resolved through the full parent chain,
position/rotation/scale composed, from `<ref>/Hard.unity`):

| world y | count | what it is |
| --- | --- | --- |
| 0.40 | 615 | the placed grid |
| 0.60 | 1 | one tile 20 cm high, the only outlier on the map |
| −47.90 | 17 | one import-palette copy per tile type, parented to `City` |
| −83.64 | 17 | a second palette, the `.001`-suffixed names, parented to `Roads_Parent` |

So **34 of the 650 are unplaced palette copies below the map**, two complete sets of the same 17
tile types, and 616 are on the map within 20 cm of each other. (An earlier pass put the underground
count at 51 by double-counting one palette; the re-derivation above sums to the verified 650.) All
246 of Medium's road pivots sit within 3 cm of each other over a 352 × 504 m district
(`MEASURED`). Nothing drivable steps.

Three vertical features exist and **none is confirmed drivable**:

- **16 speed bumps in Hard, 17 in Medium**, each a MeshCollider at world y = 0.228 outside the
  `City` hierarchy entirely, placed in pairs across streets (`MEASURED`). These are the only
  road-surface features that are definitely not cosmetic. Bump height is `UNKNOWN` — §6.
- **Two footbridges in Hard**, `Footbridge01` and `Footbridge`, each owning one on-map MeshCollider
  at pivot y = 0.39 (`MEASURED`). `DERIVED` at a 16.9 × 15.9 m footprint and **11.49 m tall**:
  `Footbridge01`'s mesh measures 8.2560 × 7.7916 × 5.6214 FBX units, and 5.6214 × 0.01 × 204.391 =
  11.49. Weak trace — those three input numbers are recorded only at `<notes>/notes-map.md:282-283`,
  from an untracked `fbx_mesh_bounds.py` run (§7); the FBX itself is not in the local tree.
  Whether a car can get onto one is `UNKNOWN`; no ramp was found.
- **One stone pile 7.52 m up in Hard** (`MEASURED`). `INFERRED`: a misplaced prop, not a feature.

No ramps. No bridges a car can use. No elevation change on any confirmed drivable surface in any
scene.

### 2.3 What the car can hit, and what holds it up

This is where the scene files stop being able to answer, and it matters more than any other open
question in this document.

**The car has plenty to crash into.** Hard carries 1221 colliders on the map across 127 name
families — 307 `streetlamp01`, 91 `fence03_sample`, 67 `TrafficLight02`, 62 `tree03`, 59
`commercial_base`, 41 `apartment_base`, 35 `Bus_stop`, down a long tail — plus 109 below the map
(`MEASURED`). Easy has 169 on the map and none below.

**But no placed road tile in Hard owns a collider, and the one merged collider that stands in for
them references no mesh.** Across 582 `road_base` objects the collider count is two, and both sit on
a single object at world y = −47.902, parented directly to `City` rather than to `Roads_Parent` —
the FBX's unplaced palette, the same thing `KnockablePropBaker.cs:40` calls whole groups of props
below the map, never placed (`MEASURED` + `READ`). The same pattern holds for every other `road_*`
family. Re-derived with the §2.2 census, every MeshCollider owned by a `road_*` object in the scene
is accounted for: the 615 placed tiles at y = 0.40 and the one at 0.60 carry **zero** between them,
the `Roads_Parent` palette at −83.64 carries zero, and all **34** sit on the 17 `City` palette
objects at −47.90, two apiece (`MEASURED`, same run as §2.2).

`Roads_Parent` itself holds one enabled, non-convex `MeshCollider` whose `m_Mesh` is `{fileID: 0}` —
a null reference — verified directly at `<ref>/Hard.unity:199849` for the component and `:199870`
for the field (`READ`). It also carries the `SurfaceType` Tarmac tag (`:199880-199886`). A
MeshCollider with no mesh has no collision geometry (`INFERRED` from the serialized value, not
observed at runtime).

Medium is the opposite: 221 colliders across 246 road-named objects. Medium's roads are physically
present (`MEASURED`).

So three candidate drivable surfaces exist in Hard at three heights, and the files do not say which
one the suspension rays hit:

| Candidate | Collider | Height |
| --- | --- | --- |
| `Floor` | BoxCollider 579.40 × 601.85 m | top face **y = 0.00** (`DERIVED` from `<ref>/Hard.unity:4453-4454` and `:4527-4528`, §Per-scene) |
| Placed road tiles | **none** | visuals at pivot y = 0.40, slab 0.204 m thick |
| `Bastrop-blocks-combos.001` | non-convex MeshCollider, uniform scale **−65155.355** | pivot y = 0.398 (`MEASURED`) |

The repo disagrees with itself about this, in one file: `KnockablePropBaker.cs:40` says the road
surface is at y = 0, and `:346` says the road collider under them sits at +0.40 (`READ`, both). The
0.40 is the `City` root's own `m_LocalPosition.y` (`<ref>/Hard.unity:187642`, on the root object
named at `:212927` with `m_Father: {fileID: 0}` at `:187722`, so local equals world), which is where
the road *visuals* sit; the `Floor` collider's top face is 0.40 m below that (`DERIVED`). If the car
rests on the `Floor`, its wheels are inside the road slab. The 0.204 m slab thickness is `DERIVED`
as `0.1000 FBX units × 0.01 × 204.391`, and carries the same weak trace as the module width — the
0.1000 is recorded only at `<notes>/notes-map.md:185`, with the arithmetic at `:197` (§2.1).

**Easy is the worst case:** no `Floor` box, no road geometry, so its only candidate drivable
collider is that same negative-scaled non-convex mesh, at pivot y = 0.000 — `READ` at
`<ref>/Easy.unity:15120`, and it is a scene root (`m_Father: {fileID: 0}` at `:15124`), so the local
value is the world value. Whether the car has any ground at all in Easy is `UNKNOWN`.

### 2.4 Dynamic obstacles, ramps, shortcuts

**Knockable props exist in Hard only** — 33 of them, and zero in Medium, Easy and
`Possible game scene` (`MEASURED`). By family and baked mass: 12 mobile toilets at 180 kg, 9 picnic
tables at 120 kg, 9 trash containers at 60 kg, 3 road blockers at 25 kg. The 5 kg `cone_type`
family exists in the baker table (`KnockablePropBaker.cs:92`) and has **zero instances**.

They are real obstacles, not cosmetic. Measured world box footprints: picnic tables **9.21 × 9.21 m
and 1.98 m tall**, trash containers 2.74–3.68 × 2.97–3.69 × 2.57 m, mobile toilets 2.49 × 2.61 ×
3.90 m, road blockers 3.97 × 2.17 × 0.97 m (`MEASURED`) — against a car box of 1.80 × 4.20 × 1.00 m,
which is `READ` rather than measured because it is a serialized literal, `m_Size: {x: 1.8, y: 1,
z: 4.2}` at `<ref>/Hard.unity:1235`. A picnic table is 2.19 car-lengths across (`DERIVED`:
9.21 ÷ 4.20). Mass is
deliberately a feel number per family, chosen so the car always wins and heavier families resist
more, after an earlier
size-derived version produced "a 4 tonne table the car bounced off" (`KnockablePropBaker.cs:72-87`,
`READ`).

Each baked prop was reparented to the scene root with positive scale, had its imported MeshCollider
**disabled rather than destroyed**, received a BoxCollider fitted to the mesh's robust world bounds
(2nd–98th percentile, +4 % pad), and a Rigidbody with `linearDamping 0.2`, `angularDamping 1.5`, a
compacted inertia tensor so a 9 m table rotates like a 2 m one, `Discrete` collision detection and
`Sleep()` on creation (`READ`, `KnockablePropBaker.cs`).

**There is no shortcut, ramp or alley object in any scene** (`MEASURED`, full name histograms). What
exists instead is an unintended one: the `Floor` box in Hard and Medium is 579.40 × 601.85 m and
flat, and the road network sits inside it — Hard's road pivots span 515.07 × 523.24 m and Medium's
351.76 × 503.66 m (`MEASURED`). `DERIVED`: the floor extends past the roads on every side, so the
roads impose no constraint the car has to respect. The carriageway analysis agrees from the other
direction — of Hard's twelve anchors only
six sit on the carriageway at all, and only four of those are reachable from the car's start by an
all-road route; in Medium ten of twelve are on the carriageway but the route search reaches none of
them by road alone, so Medium's road mask is not contiguous from the spawn (`MEASURED`). Either way
**the player must leave the road to finish a job, so cutting across open ground is not a shortcut,
it is the route.**

The boundary is a fence ring: `Border.prefab`
(`origin/main:Assets/Project/Scenes/Border.prefab`, GUID `70f442799ea311641904614d76bbbf24`), 89
GameObjects carrying 84 MeshColliders (`MEASURED`), instanced once in Hard (`:2587`) and Medium
(`:169961`), present as plain objects in Easy, absent from `Possible game scene`. Because prefab
instance contents are not in the scene file, Hard's and Medium's true collider counts are 84 higher
than §2.1's table.

### 2.5 Pickup to drop-off travel time

**State the assumption first, because every "seconds" figure depends on it.** The costing uses
`WORKING_AVERAGE = 20.0` m/s, which is a constant written into
`docs/tooling/scene-tools/delivery_points.py:49` (`READ`), not a recorded lap. The order tuning
asset admits as much in its own rationale: the 20 m/s average is an estimate from measured grip
rather than a recorded lap (`OrderTuning_Balanced.asset:27-28`, `READ`). So distances below are
`MEASURED` and seconds are `DERIVED` from an authored assumption. Given §1.4 — Hard's geometry caps
cornering at 5.8 m/s on an 8.18 m street — 20 m/s is optimistic there by a wide margin.

Distances are grid (Manhattan) metric including the approach leg from the car's start, because the
countdown begins at **spawn, not at pickup**:

| | `Hard` | `Medium` | `Easy` | `Possible game scene` |
| --- | --- | --- | --- | --- |
| shortest | 150 m / 7.5 s | 195 m / 9.8 s | 266 m / 13.3 s | 260 m / 13.0 s |
| median | 557 m / 27.8 s | 533 m / 26.7 s | 542 m / 27.1 s | 541 m / 27.1 s |
| mean | 520 m / 26 s | 514 m / 26 s | 570 m / 29 s | 557 m / 28 s |
| longest | 918 m / 45.9 s | 893 m / 44.7 s | 852 m / 42.6 s | **954 m / 47.7 s** |
| pairs clearing 45 s | 35/36 | 36/36 | 36/36 | **35/36** |
| pairs clearing 50 s | 36/36 | 36/36 | 36/36 | 36/36 |

`MEASURED` distances, `DERIVED` seconds. Reproduced by
`python docs/tooling/scene-tools/order_distances.py 50` for `Possible game scene` and by a
per-scene point extraction for the others.

**The shape matters more than the extremes.** All four scenes put the bulk of their 36 pairs in two
clumps — a short cluster under 20 s and a long cluster from 30 to 40 s — with a thin middle. Hard is
the most bimodal: 15 of 36 under 20 s, 14 over 30 s, only 2 between 20 and 25 s (`MEASURED`). Since
order locations are drawn uniformly and independently (§3.6), a player's experience of the timer
depends heavily on which clump they draw.

**The anchor names are now misleading.** All twelve anchors were hand-moved per scene rather than
re-derived, and the names still describe the old greybox layout. Hard's `Pickup 01 South West Gate`
is at x = 0, the middle of the map; Hard's `Pickup 06 North Centre` is at z = −111.50, the southern
edge (`MEASURED`). `docs/tooling/scene-tools/delivery_points.py` calls itself "one source of truth"
and matches exactly one of the four scenes.

---

## 3. Order system and economy

### 3.1 How Value is calculated

**It is not a flat 10.** The code path. Every hop below is `READ` except the uniqueness claim in
step 1, which is `MEASURED` because a uniqueness claim is a sweep and not a value in a file:

1. `CargoSystem.TryDeliver` reads the cargo condition and calls
   `orderManager.ResolveDelivery(expectedOrder, conditionMultiplier)` (`CargoSystem.cs:142,148`,
   `READ`). That is the only call site in the project (`MEASURED`):
   `git grep -n "ResolveDelivery" origin/main -- "Assets/**/*.cs"` returns four hits and exactly one
   call — the declaration (`OrderManager.cs:285`), a log string inside it (`:290`), a comment
   mention (`CargoCondition.cs:13`) and `CargoSystem.cs:148`.
2. `OrderManager.ResolveDelivery` computes
   `tuning.DeliveryValueFor(order.DesignedDistance) * Mathf.Clamp01(conditionMultiplier)`
   (`OrderManager.cs:298`, verified directly).
3. `OrderTuning.DeliveryValueFor(d)` returns `baseDeliveryValue + Mathf.Max(0f, d) * valuePerMetre`
   (`OrderTuning.cs:48-50`, verified directly).
4. `DesignedDistance` is the straight-line pickup→drop-off distance fixed in the constructor,
   `Vector3.Distance(pickupPoint, dropOffPoint)` (`Order.cs:43,74`) — **never distance driven**, so
   driving in circles earns nothing (the field tooltip says exactly that, `OrderTuning.cs:37-40`).

So **value = (baseDeliveryValue + straight-line metres × valuePerMetre) × clamp01(cargoCondition)**.

The asset is the complication. `OrderTuning_Balanced.asset` sets `baseDeliveryValue: 10` (`:34`) and
**contains no `valuePerMetre` key at all** — verified directly: `:34` is `baseDeliveryValue: 10` and
`:35` is `latePenaltyPerSecond: 1`, with nothing between them. The identifier appears in exactly
four places in the project, all in `OrderTuning.cs` and one `ProgressionTuning.cs` comment, and in
no `.asset` (`MEASURED`, 4 hits). `OrderTuning.cs:41` declares `public float valuePerMetre = 0.1f`.

Rather than reason about what Unity does with a missing key, the value was read out of the build the
testers played. The built asset's serialized fields sit in declaration order after the `profileName`
string in `Game Build/Zoom Zoom Delivery Co_Data/sharedassets1.assets` at offset 113 113 568:
`spawnInterval 12.0`, `maxActiveOrders 3`, `orderTimeLimit 45.0`, `baseDeliveryValue 10.0`,
**`valuePerMetre 0.1`**, `latePenaltyPerSecond 1.0` (`MEASURED`; the unpack is in §7). The build is
dated 2026-10-14 08:31:46 for the executable and 08:31:47 for `sharedassets1.assets`
(`MEASURED`, file timestamps, verified directly), and the four standalone log rows are 09:09–09:38
the same morning, so that binary produced them.

**The shipped formula is `(10 + 0.1 × straight-line metres) × condition`.**

The logged totals confirm it independently. Flat 10 is arithmetically impossible: with
`valuePerMetre = 0` every delivery is worth at most 10, capping 4 deliveries at 40 and 6 at 60, and
the logs show **158 for 4 deliveries and 216 for 6** (`MEASURED`). Straight-line distances for the
tested point set run 94 m to 386 m with a 36-pair mean of 216.7 m (`MEASURED`), so since every
condition multiplier is ≤ 1:

- 4 deliveries at 158: `158 ≤ 4 × (10 + 386v)` ⇒ **v ≥ 0.076**
- 6 deliveries at 216: `216 ≤ 6 × (10 + 386v)` ⇒ **v ≥ 0.067**

`DERIVED`. Both bounds exclude zero and both are consistent with the `MEASURED` 0.1.

**The 61-value row is the cargo-condition row.** Nine deliveries totalling 61 is 6.78 each, below
`baseDeliveryValue` 10, which is only possible with `conditionMultiplier < 1`. Rigorously, since
each term is `(10 + 0.1 d_i) c_i ≥ 10 c_i`:

> `61 ≥ 10 × Σc_i` ⇒ `Σc_i ≤ 6.1` ⇒ **mean condition ≤ 0.678**

`DERIVED`, and that is as far as the files go. A tighter figure would need the nine actual designed
distances, which are not recorded, so the exact mean condition is `UNKNOWN` — §6.

`PlaytestLogger` accumulates `_value += value` on `OrderResolved` for delivered orders only
(`PlaytestLogger.cs:167-172`) and writes it with `ToString("0")` (`:256`), so the logged total is
the plain rounded sum of per-order values and excludes anything delivered after the row is written
(`READ`).

### 3.2 Late orders deduct nothing

**A late order awards nothing and deducts nothing.** `Order.Tick` decrements `TimeRemaining` and
calls `MarkLate()` at zero (`Order.cs:101-110`); `OrderManager.TickAllOrders` reacts with
`Resolve(order, value: 0f, wasLate: true)` — a literal `0f`, verified directly at
`OrderManager.cs:274`; `Resolve` removes the order, increments `TotalResolved` and fires
`OrderResolved(order, 0f)` (`:303-315`). Nothing subtracts from any running total. The displayed
money only ever goes up.

`latePenaltyPerSecond` exists in exactly two places — `OrderTuning.cs:44` (declaration, verified)
and `OrderTuning_Balanced.asset:35` (value `1`, verified) — and **is read nowhere**: 1 hit for the
identifier across every `.cs` under `Assets/`, its own declaration (`MEASURED`). `OnValidate` clamps
five fields and never this one (`OrderTuning.cs:53-59`, verified), which reads as forgotten rather
than deliberately inert.

On the XP side, `PlayerProgression.HandleOrderResolved` pays
`baseXpPerDelivery × lateConsolationXpFraction` for a late order (`PlayerProgression.cs:120`), and
`lateConsolationXpFraction` is `0` in `ProgressionTuning.asset:17` (`READ`). A late order earns zero
XP as well.

So a missed order costs the player the time spent on it and the money they would have earned,
nothing more.

### 3.3 The order timer: the build ran 45 s, the asset says 50 s, and the 50 is unreachable

Four sources:

| Source | Value | Status |
| --- | --- | --- |
| `OrderTuning.cs:31` | `orderTimeLimit = 45f` | C# field initialiser (verified) |
| `OrderTuning_Balanced.asset:33` | `orderTimeLimit: 50` | serialized asset value (verified) |
| `docs/tooling/scene-tools/order_distances.py:25,68` | `75.0` | a tooling script's CLI default, never a game value |
| `Game Build/…/sharedassets1.assets` @113113600+8 | **45.0** | what the build actually held (`MEASURED`) |

**The asset is not valid YAML, and that is why.** `whyThisSetup:` sits at column 1 in
`OrderTuning_Balanced.asset:16` while every other key in the document is indented two spaces —
verified directly: `:15` reads `  profileName: Balanced` and `:31` reads `  spawnInterval: 12`,
both indented, and `:16` is flush left. At indent 0 that key closes the `MonoBehaviour:` block
mapping; the quoted scalar runs to `:30`; and `:31` resumes two-space indentation belonging to no
open mapping. A conformant parser rejects the file at exactly that point:

```
python -c "import yaml; yaml.safe_load(open('Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset',
          encoding='utf-8-sig').read().split('--- !u!114 &11400000',1)[1])"
→ ParserError: expected <block end>, but found '<block mapping start>' … spawnInterval: 12
```

`MEASURED`. The same test on the sibling `Tuning_Balanced.asset` parses cleanly and yields **118
two-space keys** — 10 of them Unity boilerplate (`m_*` and `serializedVersion`) and 108 tuning
fields (`MEASURED`, verified directly; the 108 is the figure §5.10 matches against the class). So
the test is sound and the fault is specific to this one asset. Everything from `:31` down is
downstream of the break — and `orderTimeLimit` is the **only** field there whose C# default differs
from its asset value, so it is the only one that can discriminate "asset loaded" from "asset
ignored". The build's bytes show exactly that fingerprint: `profileName` loaded as `Balanced` from
above the break, `orderTimeLimit` reads 45 from below it.

`whyThisSetup` is not a field in `OrderTuning.cs` at all — the class declares `profileName`,
`spawnInterval`, `maxActiveOrders`, `orderTimeLimit`, `baseDeliveryValue`, `valuePerMetre`,
`latePenaltyPerSecond` and nothing else (`READ`). The rationale block is a comment that looks like
data, and it is the thing that broke the keys below it.

**Independent confirmation from the telemetry.** Two logged shifts have `delivered = 0`, which makes
the expiry schedule fully determined by `spawnInterval`, `maxActiveOrders` and `orderTimeLimit`.
Stepping `OrderManager.Update`'s exact order of operations — tick, then spawn with the cap checked
*before* the interval decrements (`OrderManager.cs:190-197`) — gives:

| limit | expired by 221.4 s | expired by 268.7 s |
| --- | --- | --- |
| 45 s | **10** | **12** |
| 50 s | 9 | 12 |
| logged `late` | **10** | **12** |

`DERIVED` from `READ` tuning values. The 221.4 s row discriminates and picks 45 s. Re-run at 30, 50,
60, 90, 120 and 144 fps the counts are identical, so the match is not a frame-rate coincidence. Two
independent lines of evidence — the build's serialized bytes and the logged late cadence — agree.

**Where 75 came from.** It was briefly a real asset value at commit `1c52e42` (2026-09-23) and was
reverted the same day (`MEASURED` by walking the asset's blob through history). It survives only as
a CLI default in a tooling script. The 50 was decided, committed and documented at `c655ac7`, then
silently disabled by merge **`03e0752`** (2026-09-29 21:15), which is where `whyThisSetup` lost its
two spaces of indentation (`MEASURED`: indented 2 in `dafac15` and every earlier commit, indent 0
from `03e0752` onward).

**Tight or forgiving? At 45 s, tight at the top end, and invisible.** The asset's own costing says
36/36 pairs clear 50 s with about 2 s of slack on the longest job; at 45 s the same tool reports
35/36 and the longest job does not fit (`MEASURED`, §2.5). The clock starts at spawn, not pickup, so
the drive *to* the pickup is inside the same budget, and a long approach plus a long job is the
failure case. And in the tested build the player could not see any of it: `OrderTicketStrip`, the
per-order countdown UI, is **absent from `Possible game scene`** and present in Hard, Medium and
Easy (`MEASURED`, §Per-scene). A 45 s deadline with no visible countdown is not a tight design, it
is an undisplayed one.

### 3.4 Spawn cadence, caps, zones, cargo

| Thing | Value | Source |
| --- | --- | --- |
| spawn interval | 12 s | `OrderTuning_Balanced.asset:31`; build binary 12.0 |
| active-order cap | 3 | `OrderTuning_Balanced.asset:32`; build binary 3 |
| pickup / drop-off radius | 6 m / 6 m, every scene | verified per scene, §Per-scene |
| zone `checkInterval` | **0.1 s**, every scene | verified per scene, against `ZoneDetection.cs:31` default `0f` |
| cargo slots | 1 in `Possible game scene`, 2 in Hard/Medium/Easy | verified per scene |

All `READ`. `OrderBeacon` reads `PickupRadius` for its ground disc
(`ZoneDetection.cs:24-27`), so the visual disc cannot drift from the trigger.

**The cap suppresses the interval rather than queueing it.** `TrySpawn` returns on the cap check
*before* touching `_timeUntilNextSpawn` (`OrderManager.cs:190`, `READ`), so the 12 s does not accrue
while three orders are live. A slot freeing does not produce an instant spawn; it produces one 12 s
later. `DERIVED` from the §3.3 model: with nothing delivered the system settles into bursts of three
— spawns at 0, 12, 24, then 57, 69, 81, then 114, 126, 138 — not a steady drip.

**`checkInterval` overrides the code's own stated rationale.** The tooltip says 0 "means every
frame, which stops a fast car clipping through a small radius between checks"
(`ZoneDetection.cs:29-30`, verified), and every scene serializes 0.1. `DERIVED`: at 32 m/s the car
covers 3.2 m between checks, so the radius within which detection is *guaranteed* rather than merely
likely drops from 6 m to 5.78 m; at the 40 m/s boost ceiling it is 5.66 m
(`.agents/tasks/turning-geometry-2026-10-14/report.md:495-498`). Small, but it would read to a
player as "I drove over it and nothing happened".

**All four scenes take the real-points branch.** `ResolvePickupAndDropOffPoints` prefers real
`Transform[]` arrays, then a `floorBounds` collider scatter, then a 60 m origin ring
(`OrderManager.cs:84-137`); six `pickupPoints` and six `dropOffPoints` are wired in each scene,
verified per scene at the array keys — `pickupPoints` / `dropOffPoints` at `PGS:1665`/`:1672`,
`Hard:2253`/`:2260`, `Medium:54288`/`:54295`, `Easy:5520`/`:5527`, each array six entries
(`READ`). So `floorEdgeMargin: 5`, `testPointCount: 6` and `testPointRadius: 60` are inert
everywhere, and the `floorBounds` scatter is dead code in play.

### 3.5 Cargo condition is on in every scene

**The code default is off; the shipped state is on.** `CargoCondition.cs:42` declares
`= false` and the class header says in capitals "OFF BY DEFAULT … exists to be demoed as an optional
toggle, not a change already committed to" (`:15-19`). Every scene serializes `enableCondition: 1`
— verified at `PGS:800`, `Hard:1364`, `Medium:39230`, `Easy:3336`. Delivery payouts are scaled by
condition, not paid at full value.

Mechanics, all `READ`: condition starts at 100 the moment an order is carried (`:136`); a hard brake
costs a flat `hardBrakeDamage` once per `hardBrakeCooldown` (`:165-168`); a collision costs
`(impactSpeed − collisionImpactThreshold) × collisionDamagePerMps` where `impactSpeed` is
`collision.relativeVelocity.magnitude` (`:184,192`); damage applies to **every** carried order, not
the one that was hit (`:206-209`), so in the two-slot scenes one wall costs both parcels; and the
multiplier is `condition / 100` floored at 0 (`:208,223`), so a trashed delivery still delivers and
pays nothing.

**The hard-brake threshold has two branches, and the scene files serialize the state that takes the
losing one.** `HardBrakeDecelThreshold` is a getter with a null check (`CargoCondition.cs:87-96`,
`READ`):

```csharp
if (vehicle != null && vehicle.Tuning != null)                                      // :91
    return vehicle.Tuning.brakeDeceleration * Mathf.Max(1.01f, hardBrakeMultiplier); // :92

return fallbackHardBrakeDecelThreshold;                                             // :94
```

| Branch | Inputs | Threshold |
| --- | --- | --- |
| `vehicle` resolved (`:92`) | `brakeDeceleration` 22 (`Tuning_Balanced.asset:105`) × `max(1.01, hardBrakeMultiplier 1.25)` — the multiplier serialized per scene at `PGS:804`, `Hard:1368`, `Medium:39234`, `Easy:3340` | **27.5 m/s²** |
| `vehicle` null (`:94`) | `fallbackHardBrakeDecelThreshold` 27, serialized at `PGS:805`, `Hard:1369`, `Medium:39235`, `Easy:3341` | 27 m/s² |

All inputs `READ`; both thresholds `DERIVED` from them. **Every scene serializes
`vehicle: {fileID: 0}`** — a null reference — at `PGS:803`, `Hard:1367`, `Medium:39233`, `Easy:3339`
(`READ`), so the serialized state alone reads 27, not 27.5.

**27.5 is nevertheless what runs, through one hop that is a code-path inference rather than a value
in a file.** `Awake` fills the reference in: `if (vehicle == null) vehicle =
GetComponent<VehicleController>();` (`CargoCondition.cs:102`, `READ`), and the two components share
a GameObject in all four scenes — `m_GameObject: {fileID: 592668305}` for the `CargoCondition` at
`PGS:794`, `Hard:1358`, `Medium:39224`, `Easy:3330` and for the `VehicleController` at `PGS:617`,
`Hard:1181`, `Medium:39047`, `Easy:3153` (`READ`, all eight lines re-read for this revision, so
Medium and Easy are confirmed rather than assumed). `INFERRED` from that path, not observed at
runtime: the `GetComponent` succeeds, so the `:92` branch is live and the threshold is 27.5 m/s².
Two things in the file back that reading without needing the game to run, both `READ`: the
`hardBrakeMultiplier` tooltip states the result outright — 1.25 against the current
`brakeDeceleration` of 22 "gives a threshold of 27.5 m/s²" (`:55-56`) — and the fallback's own
tooltip says it is "Used only if no VehicleController is found on this GameObject" (`:58-59`). The
null branch is also loud if it ever does run: `Awake` logs a warning naming the fallback value
(`:113-119`, `READ`), so a console with no such warning would settle it.

Why the distinction is worth the space: ordinary full braking plus coast drag is 27 m/s² (§1.7). On
the live branch normal braking sits **0.5 m/s² under the threshold** — the margin the class comment
was aiming for, and a thin one. On the serialized fallback the two numbers are **equal**, and the
only thing keeping the penalty off a normal stop is the strict `>` at `:164`. A reader who opens the
scene YAML and stops there gets the second reading.

**The tested scene is roughly four times harsher per impact than the three current ones.**
`DERIVED` from `CargoCondition.cs:192` and the per-scene values: a 20 m/s hit costs
`(20 − 6) × 4 = 56` condition in `Possible game scene` and `(20 − 10) × 1.5 = 15` in Hard. Any
balance read off the October logs does not transfer.

**It does not know a prop from a wall.** `CargoCondition` has no reference to `KnockableProp` and no
layer, tag, mass or Rigidbody test in `OnCollisionEnter` (`:180-194`) — the only inputs are relative
velocity magnitude and the threshold (`READ`). Clipping a knockable road blocker costs exactly what
hitting a building costs: in `Possible game scene` a 15 m/s brush against a prop costs
`(15 − 6) × 4 = 36` condition, 36 % of that delivery's pay (`DERIVED`).

**And the logged wall-hit count undercounts condition damage.** `PlaytestLogger` counts a hit as a
speed drop greater than `impactSpeedDrop: 3` from at least `impactMinimumSpeed: 10` m/s
(`PlaytestLogger.cs:199`), while `CargoCondition` charges any impact at or above 6 m/s relative
velocity. An 8 m/s shunt from a 9 m/s approach damages cargo and logs no wall hit (`DERIVED`) —
which is how the 61-value row reaches a mean condition at or below 0.678 with only 7 logged hits.

### 3.6 Determinism: the cadence repeats, the locations do not

**There is no `System.Random` anywhere in the project** — 0 matches across every `.cs` under
`Assets/` (`MEASURED`). The randomness is `UnityEngine.Random.Range` at six call sites: four in
`GenerateBoundedTestPoints` (`OrderManager.cs:176,179`), which **no scene reaches** (§3.4); one in
`PickIndexAvoidingCollisions` (`:242`), 8 attempts against a 0.5 m same-spot threshold; and one in
the give-up pick after all 8 collide (`:265`), which also logs a warning. **No seed is set
anywhere.**

The precise consequence: the spawn **cadence** is fully deterministic — 12 s interval, cap 3, 45 s
expiry, no randomness in the timing at all, which is why the §3.3 model reproduces the logged late
counts exactly. What differs between runs is **which** of the 6 pickups and 6 drop-offs each order
uses, drawn independently and uniformly, hence uniformly over the 36 pairs. Since value is
`10 + 0.1 × distance` and pair distances span 94–386 m, two runs with identical play produce
different scores (`INFERRED` from the code path, `DERIVED` for the spread). A per-session
delivered/late/value figure is not comparable to another session's at the resolution a playtest
sheet implies.

`ShiftTimer.cs:9-10` claims "every session is the same length and comparable across playtests"
(`READ`). True for length — 300 s in every scene — and not true for score.

---

## 4. Win/loss conditions and progression

### 4.1 A completed shift is the only outcome — there is no fail state

The shift is one countdown: `shiftDurationSeconds = 300f` with the comment `// 5 minutes, per
Scope` (`ShiftTimer.cs:19`, verified), `startAutomatically = true` (`:21`, verified), serialized at
300 and `1` in all four scenes (§Per-scene). `Update` decrements and calls `EndShift()` at zero
(`:57-67`); `EndShift` sets `IsShiftActive = false`, sets `orderManager.enabled = false`, logs and
fires `ShiftEnded` (`:83-95`). All `READ`.

**Nothing checks a quota, a score threshold, a life count or a damage total.** `MEASURED` — a
search of every `.cs` under `Assets/Project` for `game ?over`, `GameOver`, `failState`, `FailShift`,
`\blose\b`, `quota`, `minimumDelivered` and `targetDeliveries` returns only two prose comments in
`VehicleController.cs` about losing the back end. **You cannot fail a shift. You can only
finish one with a low number on it.**

Running out of road is not a fail state either: nothing resets or respawns the car, and the clock
keeps running (§5.3).

### 4.2 The EndShift / ZoneDetection divergence

What happens after the whistle is narrower and stranger than "late resolutions are ignored". Every
hop `READ`, the behaviour `INFERRED` from the code path and not observed at runtime:

1. `ShiftTimer.EndShift` sets `orderManager.enabled = false` (`:87`). That stops
   `OrderManager.Update` only, so spawning and ageing stop and orders still Active or Carried freeze
   with time on their clocks — exactly as the class comment promises (`ShiftTimer.cs:8-10`).
2. `ZoneDetection` is a component on a **different GameObject**, the `Car`: its `m_GameObject` is
   `{fileID: 592668305}` at `Hard:1339`, `Medium:39205`, `Easy:3311` and `PGS:775`, and that object
   is `m_Name: Car` at `Hard:1140`, `Medium:39006`, `Easy:3112` and `PGS:576`. `OrderManager`'s
   `m_GameObject` is `{fileID: 816895043}` at `Hard:2246`, `Medium:54281`, `Easy:5513` and
   `PGS:1658`, which is `m_Name: OrderManager` at `Hard:2234`, `Medium:54269`, `Easy:5501` and
   `PGS:1646`. (The `--- !u!114 &…` block headers six lines above each `m_GameObject` are not the
   evidence; the field is.) Disabling one does not disable the other. `ZoneDetection.Update`
   keeps running and has no `IsShiftActive` check — it checks only distance (`:53-101`).
3. `ZoneDetection.CheckDeliveries` calls `cargoSystem.TryDeliver(orderManager, order)` (`:98`),
   which calls `orderManager.ResolveDelivery(…)` directly (`CargoSystem.cs:148`).
4. `ResolveDelivery` guards on one thing only: `order.State != OrderState.Carried`
   (`OrderManager.cs:287`, verified directly — there is no `enabled` guard and no `IsShiftActive`
   guard). A disabled MonoBehaviour's public methods execute normally.

So after the whistle the player can still drive a carried parcel into its drop-off and have it
resolve — and pickups still work too, because `CheckPickups` requires `State == Active` and the
frozen Active orders qualify (`ZoneDetection.cs:65,69,74`). Nothing stops driving either: no
subscriber to `ShiftEnded` disables `VehicleInput` or `VehicleController`. The complete subscriber
list is `ResultsPanel`, `ShiftResults` and `PlaytestLogger` — `MEASURED` rather than `READ`, because
it is a sweep and not a value in a file: `git grep -n "ShiftEnded" origin/main -- "Assets/**/*.cs"`
returns the event's declaration and raise (`ShiftTimer.cs:39`, `:95`) and exactly three `+=` sites,
`ResultsPanel.cs:49`, `ShiftResults.cs:113` and `PlaytestLogger.cs:115`.

**What actually diverges is which counters freeze:**

| Counter | Freezes at the whistle? | Source |
| --- | --- | --- |
| `ShiftResults.Delivered` / `.Late` / `.Value` / `.XpEarned` | **yes** — `if (IsShowing) return;` | `origin/main:ShiftResults.cs:137,152,158,164` |
| `PlaytestLogger` row — `Possible game scene` only, §4.3 | **yes** — `WriteRow()` sets `_shiftRunning = false` | `PlaytestLogger.cs:167,233-234` |
| `OrderManager.TotalResolved` | **no** — incremented unconditionally | `OrderManager.cs:306` |
| `OrderHUD`'s `Delivered N  Late M  $X` line | **no** — never subscribes to `ShiftEnded` | `origin/main:OrderHUD.cs:68,76-87,168` |
| `PlayerProgression.Money` / `Level` | **no** — no freeze rule | `PlayerProgression.cs:109-123,144-145,167-171` |
| `OrderTicketStrip`'s tickets, stamps and banners — Hard, Medium and Easy only | **no**, and it holds no tally to freeze | `origin/main:OrderTicketStrip.cs:108,133-138,155-170,192` |

Every cited line is `READ`; the last row's "Hard, Medium and Easy only" is the per-scene component
census, `MEASURED` (§Per-scene).

The ticket strip is the fourth surface that does not stop at the whistle, and it is a
different kind from the other three: it keeps no delivered/late/value count of its own, so it cannot
diverge from `ShiftResults` numerically. What it does instead is **stamp** a post-whistle resolution
— `HandleOrderResolved` sets the stamp text to `"DELIVERED!"` or `"LATE!"` (`:133-135`) and pushes
`$"Order #{order.Id} delivered!  +${value:0}"` as a banner (`:138`) — and keep rendering: `Update`
runs unconditionally into `SyncTickets`, which reads `orderManager.ActiveOrders` with no
`IsShiftActive` test (`:155-170`), and `UpdateTickets` refreshes every ticket from
`t.Order.TimeRemaining` each frame (`:192,207-209`). It has no `ShiftEnded` subscription, no
`IsShiftActive` check and no `shiftTimer` reference at all:
`git grep -n -E "ShiftEnded|IsShiftActive|shiftTimer" origin/main
-- "Assets/Project/Scripts/Orders/OrderTicketStrip.cs"` returns nothing (`MEASURED`). It also ticks
on `Time.unscaledDeltaTime` (`:158-159`, `READ`), so it would keep animating even behind a
`timeScale = 0` results screen.

So it is not "ShiftResults ignores late resolutions" — it is **`ShiftResults` freezes
everything and every other order-facing surface freezes nothing**, and they are on screen at the
same time. `ShiftResults` never sets `Time.timeScale = 0`; its only `timeScale` writes are the
`= 1f` resets in `Restart` and `GoToMainMenu` (`:202,220`), `READ`.

`INFERRED` player-visible result: the TIME UP panel appears centre-screen with frozen numbers, the
world keeps moving behind it, the car is still drivable, and the HUD keeps ticking in the corner.
Deliver one more parcel and the corner of the screen and the middle of the screen show different
delivered counts, different late counts and different money at the same time, with no indication
which is the score — and in Hard, Medium and Easy the ticket strip also stamps that delivery
`DELIVERED!` and banners its payout while the results panel says the shift is over.

**The fix for this is already in three scenes, switched off.** `ResultsPanel.cs` — the other results
implementation, new on `origin/main` at `Assets/Project/Scripts/Orders/ResultsPanel.cs` — **does**
freeze the world (`Time.timeScale = 0f`, `:83`) and **does** freeze its tally
(`if (_shiftOver) return;`, `:60`), `READ`. It ships in Hard, Medium and Easy, inside a canvas
nobody turned on:

| Hop | Evidence | Label |
| --- | --- | --- |
| Hard, Medium and Easy each instance `Canvas (2).prefab` | `m_SourcePrefab` GUID `16e1a52f888ac16458f58d73070092a7` at `Hard:4311`, `Medium:105934`, `Easy:22541` | `READ` |
| that prefab carries one enabled `ResultsPanel` | `origin/main:Assets/Project/Prefabs/Canvas (2).prefab:509-520` — `m_Enabled: 1`, script GUID `afcd8a025ccf16d4d85203c80cf90775`, on the `Buttons` child | `READ` |
| the prefab's root object is inactive | `Canvas (2)` at `:368`, `m_IsActive: 0` at `:373` | `READ` |
| Hard and Medium override it to 0 again | `Hard:4223-4225`, `Medium:105846-105848` — `propertyPath: m_IsActive`, `value: 0` | `READ` |
| Easy carries no such override, so it inherits the prefab's 0 | `Easy:22448-22536` — the instance's only modifications are `m_Name`, `orderManager` and the root `RectTransform` | `READ` |

So the component is in the build and never executes: `Awake` does not run on a GameObject under an
inactive root, so it never subscribes to `ShiftEnded` and never writes `Time.timeScale`. Enabling it
is one checkbox, which is a different sentence to put in front of a designer than "the fix is not in
the game". Two details that matter if anyone ticks it. Its `shiftTimer` and `orderManager` fields
are `{fileID: 0}` in the prefab (`:526-527`), but `Awake` resolves both with
`FindAnyObjectByType` when they are null (`ResultsPanel.cs:36-37`), so the empty wiring is not a
blocker — and Easy has already wired `orderManager` explicitly to its scene `OrderManager`
(`Easy:22453-22456` → `fileID: 816895044`, `Easy:5507-5518`), `READ`. Second, nothing removes
`ShiftResults`, so enabling the panel would put **two** results screens in the scene at once, one
freezing the world and one not.

An earlier pass reported this component as absent from every scene on the strength of a GUID census
returning 0 in all four scene files. That census cannot see inside a `PrefabInstance`, which is the
caveat recorded in C6 — it was applied to the collider counts and not to the component counts.

### 4.3 Objectives besides delivering

**None.** Nothing in the shift is scored except resolved orders. `OrderResolved` is the single
source of delivered, late, value, money and XP, declared at `OrderManager.cs:45` (`READ`).

The subscriber list is `MEASURED` rather than `READ`, because an enumeration produced by a `+=`
sweep is a tooling run and not a value in a file:
`git grep -n "OrderResolved +=" origin/main -- "Assets/**/*.cs"` returns **seven** sites —
`CargoSystem.cs:85` (to free a cargo slot), `OrderHUD.cs:68`, `OrderTicketStrip.cs:108`,
`PlayerProgression.cs:101`, `ResultsPanel.cs:48`, `ShiftResults.cs:112` and
`PlaytestLogger.cs:113`.

How many of the seven actually attach is per scene, and no scene gets all of them. The census
(`MEASURED`, script-GUID occurrence over each `<ref>` blob with that scene's prefab instances
resolved and checked as well, §Per-scene) gives **five live in Hard, Medium and Easy** —
`CargoSystem`, `OrderHUD`, `OrderTicketStrip`, `PlayerProgression`, `ShiftResults` — and **four in
`Possible game scene`** — `CargoSystem`, `OrderHUD`, `PlaytestLogger`, `ShiftResults`.

The two that go missing do so for different reasons. `ResultsPanel` is present in Hard, Medium and
Easy and attaches in none of them: it subscribes inside `Awake` (`:48-49`, `READ`), which never runs
under its `m_IsActive: 0` root (§4.2). `PlaytestLogger` is the opposite — 1 in
`Possible game scene`, **0 in Hard, Medium and Easy**, and not inside any of their prefabs either
(`MEASURED`, GUID `9c2d4e6f8a1b4c3d9e5f7a8b0c1d2e3f`: 0 in all three scene blobs, 0 in
`Border.prefab` and `Canvas (2).prefab`). `Possible game scene` is therefore the only scene that
writes a `shift_log.csv` row: a logged playtest of Hard, Medium or Easy needs the component added
first.

Of the ones that do attach, `OrderTicketStrip` is the subscriber with no shift-end awareness — see
§4.2's freeze table.

Boost, flips, handbrake time and supersonic time are measured by
`PlaytestLogger` where it is attached, and shown by `BoostGauge`. Neither feeds a score.

### 4.4 Progression exists in three scenes and goes nowhere

A `PlayerProgression` component is attached in Hard, Medium and Easy and **not** in
`Possible game scene` (`MEASURED`, §Per-scene). What it does there: XP accrues as
`baseXpPerDelivery + value × xpPerValuePoint` (`PlayerProgression.cs:114`), levels fire in a loop so
one big delivery can clear several (`:167-171`), `OrderHUD` draws a level and XP bar
(`origin/main:OrderHUD.cs:182-188`), and `ShiftResults` shows XP earned and a LEVEL UP tag
(`origin/main:ShiftResults.cs:174-181`). `ProgressionTuning.asset:15-23` holds a full curve:
`baseXpPerDelivery: 5`, `xpPerValuePoint: 0.5`, `lateConsolationXpFraction: 0`,
`xpToNextLevel: 20/35/55/80/110`. All `READ`.

**Nothing can be bought.** `PlayerProgression.TrySpendMoney` is declared at `:127` and the only
other occurrence of the identifier in any `.cs` is the class comment at `:14`. Its companion
`CargoSystem.TryUnlockCapacity` (`:170`) is likewise called from nowhere. Neither is wired through a
UnityEvent: the complete set of `m_MethodName` values across all five scenes is `ExitGame`,
`GoToMainMenu`, `PauseGame`, `ResumeGame` in the gameplay scenes and `BackToMainMenu`, `ExitGame`,
`LoadEasy`, `LoadHard`, `LoadMedium`, `OpenControlsPanel`, `OpenDifficultyPanel` in MainMenu
(`MEASURED`, identifier and `m_MethodName` sweeps). **The upgrade economy is unreachable code.**

**And nothing is saved.** No `PlayerPrefs`, no `JsonUtility`, no save file; the only `File.Write*`
in the project is `VehicleMeasurement.cs:1156,1173` writing the vehicle-lab CSV (`MEASURED` for the
three absences, `READ` at the two cited lines). Level and money reset to 1 and 0 on every scene
load, including the Restart button.

So cargo capacity is a per-scene difficulty constant, not a progression reward: 1 slot in
`Possible game scene`, 2 in each of Easy, Medium and Hard.

### 4.5 The results screen is `ShiftResults`

`ShiftResults` is the results screen in every scene and sits on the `OrderHUD` GameObject.
`ResultsPanel` is the only other one, and it never runs: it ships in Hard, Medium and Easy inside a
`Canvas (2)` prefab instance whose root is `m_IsActive: 0`, and is absent from `Possible game scene`
altogether (§4.2). It shows, all `READ` from
`origin/main:ShiftResults.cs`: the hardcoded title `TIME UP!` (`:285`), `Delivered {n}` (`:141,170`),
`Late {n}` (`:146,171`), `Value {n}` (`:142,172`), and `XP earned {n} (Level {L})` with an optional
`LEVEL UP!` tag — the whole XP line hidden at runtime if no `PlayerProgression` is in the scene
(`:174-185`). So `Possible game scene` shows four lines and Easy/Medium/Hard show five.

Buttons: `RESTART` reloads the active scene by build index (`:198-203`), `MAIN MENU` loads the scene
named by `mainMenuSceneName`, serialized as `MainMenu` in all four scenes, guarded by
`Application.CanStreamedLevelBeLoaded` (`:218-231`), and `QUIT` calls `Application.Quit`, a no-op in
the editor (`:234-244`). The canvas, panel and buttons are built in code at runtime from serialized
layout numbers; there is no results prefab (`:250` onward). An `EventSystem` is added if the scene
lacks one, or the buttons would be unclickable (`:208-214`).

---

## 5. Known technical and design friction

UI canvas sorting and the incomplete Controls screen are excluded as already known, except where a
new consequence turned up.

### 5.1 The order tuning asset silently discards half of itself

The highest-impact defect in the project, and it is not cosmetic: a YAML indentation fault makes the
order time limit 45 s instead of the 50 s the file says, and puts the whole back half of the asset
behind a structural break. Full workings in §3.3. The 400-word design rationale that argues for 50 s
over 45 s is itself the thing that broke it, and it is not a field the class declares, so it would
deserialize nowhere even if correctly indented.

Side effect worth naming: the rationale text documents a game that is not being played. It says
50 s clears all 36 pairs with about 2 s spare on the longest, and that 45 s "left that one pair
about 3 s short" (`OrderTuning_Balanced.asset:24-27`, `READ`). The asset describes its own current
failure mode.

### 5.2 Camera look sensitivity

This is the most likely cause of a "camera shifts when moving the mouse even a little" report, and
the number is findable. The chain, all `READ`:

1. `VehicleControls.inputactions:503` — `CameraLook` binds `<Mouse>/delta` with
   `"processors": "ScaleVector2(x=0.06,y=0.06)"` (verified directly). The gamepad binding has no
   processor.
2. `VehicleInput.controlsAsset` points at that asset, so the asset's binding is what runs, not the
   code-built fallback in `BuildFallbackControls` — which happens to carry the same 0.06 scale. The
   field serializes `guid: 1a2b3c4d5e6f708192a3b4c5d6e7f810` at `PGS:594`, `Hard:1158`,
   `Medium:39024` and `Easy:3130`, and that GUID is `VehicleControls.inputactions`'s own, from
   `origin/main:Assets/Project/Scripts/Vehicle/VehicleControls.inputactions.meta`.
3. `ChaseCamera.cs:358-360` (verified directly) —
   `_yawOffset += look.x * Tuning.cameraSwivelSpeed * dt;` and a pitch line clamped to [−35°, +55°].
4. `cameraSwivelSpeed: 120` in all three profiles (`Tuning_Balanced.asset:211`, verified), and
   `ChaseCamera.tuningOverride` is `{fileID: 0}` at `PGS:2316`, `Hard:3455`, `Medium:81838` and
   `Easy:8073`, so the camera reads the car's profile.
5. `ChaseCamera.input` is `{fileID: 592668306}` at `PGS:2317`, `Hard:3456`, `Medium:81839` and
   `Easy:8074`, and `&592668306` is the `VehicleInput` component — script GUID
   `1a2b3c4d5e6f708192a3b4c5d6e7f802` at `PGS:591`, `Hard:1155`, `Medium:39021`, `Easy:3127` —
   whose `m_GameObject` is the `Car`'s `{fileID: 592668305}` at `PGS:588`, `Hard:1152`,
   `Medium:39018` and `Easy:3124`. **The swivel path is wired and live in all four scenes.**

`DERIVED` — degrees of yaw per pixel of mouse movement is `0.06 × 120 × dt`:

| Frame rate | deg/pixel | 400 px sweep |
| --- | --- | --- |
| 30 fps | 0.24 | 96° |
| 60 fps | **0.12** | 48° |
| 144 fps | 0.05 | 20° |

Three separate faults, in order of how much they explain the complaint:

1. **Unit collision.** `cameraSwivelSpeed` is documented as "Degrees per second the player can
   swivel the camera by hand (right stick / mouse)" (`VehicleTuning.cs:628`, `READ`). That is the
   right unit for a stick, which saturates at 1.0. Mouse delta is an unbounded pixel count:
   `DERIVED`, a 50 px flick in one frame reads as `look.x = 50 × 0.06 = 3.0`, three times full stick
   deflection, so the camera is commanded at `3.0 × 120 = 360°/s`. One number cannot serve both
   devices.
2. **Frame-rate dependence.** Mouse delta is already a per-frame displacement, so multiplying by
   `dt` makes the same physical movement produce **4.8× more rotation at 30 fps than at 144 fps**
   (`DERIVED`: 0.24 ÷ 0.05 from the table above). A stick is a rate and needs the `dt`; a mouse is a
   delta and does not.
3. **No deadzone worth the name.** The gate is `look.sqrMagnitude > 0.0001f`
   (`ChaseCamera.cs:356`, verified), i.e. a delta above `0.01 / 0.06 = 0.167` pixels (`DERIVED`). A
   single pixel of jitter is six times over the threshold. `_timeSinceSwivel` resets on any input
   over that gate (`:361`) and auto-recentre only runs after `cameraSwivelRecentreDelay: 1.2` s of
   silence (`:367-374`), so a hand resting on a mouse keeps the camera permanently off-centre. Yaw
   is unclamped; only pitch is clamped.

One thing that is **not** wrong: the camera does not snap. The swivel offset feeds the *desired*
heading, eased with `cameraRotationSmoothTime: 0.14` (`ChaseCamera.cs:157-158,171-172`), `READ`. The
complaint is about magnitude, not abruptness.

**There is no sensitivity setting, and nowhere to put one.** `MEASURED` — an exhaustive search of
`Assets/Project/Scripts` for `sensitiv`, `Settings`, `Options` and `PlayerPrefs` finds no
sensitivity field outside the tuning ScriptableObject. On `origin/main`
`MainMenuManager.cs`'s whole public surface is `OpenDifficultyPanel`, `OpenControlsPanel`,
`BackToMainMenu` and `ExitGame` — no settings panel at all. The only exposed look preference
anywhere is `invertCameraLookY` on `VehicleInput` (`:56`, serialized `0`). No target sensitivity is
recorded in any file, so this document names none.

### 5.3 Reverse has no camera, and an overturned car cannot be recovered

**Reverse itself is fine.** `reverseAcceleration` 9 m/s² (`Tuning_Balanced.asset:103`) and
`reverseTopSpeed` 8 m/s (`:102`) against `launchAcceleration` 22 (`:220`) and `topSpeed` 32 (`:50`),
all `READ`, so reverse is 41 % of forward acceleration and 25 % of forward top speed, reaching its
ceiling in 0.89 s over 3.56 m (`DERIVED`). Steering in
reverse is handled properly: `targetYawRate = curvature * ForwardSpeed`, and `ForwardSpeed` goes
negative, so the turn direction flips the way a real car does.

**What is missing is the view.** `ChaseCamera` never tests the sign of `ForwardSpeed`;
`ComputeDesiredRotation` aims the camera at a point `cameraLookAhead` metres in **front** of the
car, which on Balanced is 28 m (`Tuning_Balanced.asset:203`), and `ComputeHeading` runs
`Vector3.Slerp(nose, velocity.normalized, 0.25)` on two antiparallel vectors once reverse speed
passes the 1.5 m/s gate — a degenerate call with no defined rotation axis (`READ`, per
`.agents/tasks/turning-geometry-2026-10-14/report.md` §3.5). Reversing is done blind.
**"Make reverse better" is a missing feature, not a tuning number.**

Compounding it: `VehicleInput` puts forward and reverse on one `Throttle` axis, so `S` brakes then
reverses, and the dedicated non-reversing brake — `VehicleInput` states its intent as "Left Ctrl
brakes and never reverses. For stopping ON a drop-off point" (`READ`, `VehicleInput.cs:29`, inside
the `///` header) — is absent from the Controls screen. A tester who only knows about `S` reverses
every time they brake to a standstill.

The Controls screen's full contents are `READ` from the single `m_text` on its TextMeshPro label
(`<ref>/MainMenu.unity:1023`, one string wrapped across two physical lines):

```
W = Acceleration/Forward      D = Steer Right
A = Steer Left                Shift  = Jump
S = Brake/Reverse             SpaceBar = Hand Brake
```

**This corrects a premise inherited from the QA pass**, which reported the screen as showing "only
W/A/S/D" (`docs/QA-PLAYTEST-REPORT-2026-10-14.md`, QA-006; `docs/playtesting/QA-AI-20261014-01.md`,
with `docs/playtesting/evidence/QA-AI-20261014-01/01-controls.jpg`). It lists six bindings, not
four, and jump and handbrake are among them. What is genuinely absent is **Left Ctrl, boost, flip
and camera look** — four of the ten actions `VehicleInput` defines.

The distinction matters for §3.3's finding that no logged run records any handbrake use. The
handbrake key *is* documented, so "the player could not find it" is not available as an
explanation; "the player had no reason to use it" is. Boost, by contrast, has no on-screen
documentation anywhere in the build, so for boost the discoverability explanation does survive.
Separating those two is what `q_what_made_you_drive_well` and `q_used_boost_when` exist to do, and
neither has been answered by any participant (`docs/playtesting/playtest_sessions.csv`, both
`human` rows, all `q_` columns empty, `MEASURED`).

**And there is no reset.** `VehicleController.Teleport` exists (`:1188`) and the only call site in
the project is `VehicleMeasurement.cs:1016`, inside the measurement harness (`MEASURED` by searching
every `.cs` under `Assets/Project/Scripts` for `.Teleport(`). No respawn, no out-of-bounds check, no
kill plane — `Respawn`, `ResetCar`, `ResetPosition`, `OutOfBounds` and `killPlane` return nothing
outside the harness (`MEASURED`, same search set).

Composing that with the flip gate gives the sharpest finding in this group. Flip requires grounded
(`VehicleJumpFlip.cs:88,251`, `flipRequiresGrounded: 1`); `IsGrounded` is true only if a suspension
ray hit something (`VehicleController.cs:512`); and the rays are cast along `-transform.up`
(`:441-442`, used `:467`). `INFERRED`, not observed at runtime: **on its roof the car's
`transform.up` points at the sky, so the rays point at the sky, so nothing is hit, so Flip is
refused exactly when the player needs it.** Jump is refused for the same reason (`:196`). With no
reset, an overturned car is stuck until the 300 s clock runs out.

Caveat on how often this bites: `flips` is 0 in all seven logged rows, so no logged shift recorded a
flip and this state has not been observed (`MEASURED`). Whether the car can come to rest inverted at
all is `UNKNOWN`: its collider is `m_Size: {x: 1.8, y: 1, z: 4.2}` with `m_Center: {x: 0, y: 0.2,
z: 0}` (`READ`, `<ref>/Hard.unity:1235-1236`, identical at `Medium:39101-39102` and
`Easy:3207-3208`), a long flat box, and whether such a box settles on its roof rather than rolling
back onto its wheels is not derivable from the geometry.

Related: `Easy` has no floor plate and no road geometry, so falling out of `Easy` ends the shift in
practice (`INFERRED`, from §2.3 plus the absence of a reset).

### 5.4 The one genuinely negative-scaled collider

A single object carries an **enabled, non-convex MeshCollider under a negative world scale**, in all
three new scenes and nowhere else — `Bastrop-blocks-combos.001` in Hard (`<ref>/Hard.unity:90500`,
its MeshCollider at `:183555` with `m_Enabled: 1` at `:183572` and `m_Convex: 0` at `:183574`,
`READ`) and `Bastrop-blocks-combos.001 (1)` in the other two (`Medium:170226`, `Easy:18823`):

| Scene | local scale, `READ` | parent, `READ` | world scale |
| --- | --- | --- | --- |
| `Hard` | (−31877.838 ×3) (`<ref>/Hard.unity:183550`) | `City`, **+2.0439076** (`:187643`) | (−65155.36 ×3) — negative, uniform (`DERIVED`) |
| `Medium` | (−60305.746, −59762.26, −53445.06) (`<ref>/Medium.unity:181471`) | `'City '`, **+1.2333** (`:177909`, object named at `:165599`) | (−74375.08, −73704.80, −65913.79) — negative **and** non-uniform (`DERIVED`) |
| `Easy` | (−69773.65, −72439.586, −53445.06) (`<ref>/Easy.unity:15121`) | none — `m_Father: {fileID: 0}` (`:15124`) | identical to local (`READ`) |

**No parent cancels the sign, but not for the reason an earlier draft gave.** Medium's object is a
child of the `'City '` container, not a scene root as this document previously stated; that parent's
scale is positive and uniform, so it scales the object without flipping it back. Hard's parent is
positive too. Only Easy's object is genuinely parentless. `INFERRED` from the
geometry, not observed at runtime: negative scale mirrors the mesh, which reverses triangle winding,
which flips the collision normal PhysX computes per face; a non-convex triangle-mesh collider is
one-sided, so a flipped surface is solid from the wrong side — the car can pass into it where it
should be blocked and be blocked where it should be open. This is the mechanism the repo itself names
for the city chain (`KnockableProp.cs:27-29`), applied to the one collider where the sign does not
cancel. Medium's and Easy's are negative *and* non-uniform, the doubly-unsupported case.

`corner_geometry.py` already flags this object in its `classify()`: one object in the greybox covers
most of the map once its mesh bounds are applied, which is either a boundary wall or a mesh whose
bounds do not belong to the transform the scene file pairs them with (`READ`).

**But the Rigidbody hazard the repo warns about is closed.** Every Rigidbody in every scene resolves
to a positive-scale ancestor chain — 34 in Hard (Car + 33 props), 1 each in the others, zero under a
bad chain (`MEASURED`). The negatives in the city hierarchy **cancel**: `CityProps` is at local
−426.4630 under `City` at +2.0439, giving world −871.65, but its children carry negative local
scales too and the product comes out positive. Of 1312 colliders under `City` in Hard, 1311 resolve
to positive world scale (`MEASURED`). The reparenting work from `7f357c7`, `457d1f3` and `c03c220`
is in place and holds; the stated mechanism in `KnockableProp.cs:26-29` overstates what was wrong.
The three props sampled in detail also carry a **disabled** imported MeshCollider alongside the
baker's enabled BoxCollider (`m_Enabled: 0`, `m_Convex: 0`, `READ`), which is the right outcome — a
non-convex mesh collider on a non-kinematic Rigidbody is the one combination PhysX refuses outright.

### 5.5 A prop through the road needs a speed nothing in the game reaches; it cannot block a zone

**Through the road, but the speed it would take is not reachable.** Every prop Rigidbody in Hard has
`m_CollisionDetection: 0` (Discrete) and `m_Interpolate: 0` (`READ`), against the Car's `2`
(ContinuousDynamic, set in code at `VehicleController.cs:358` with a comment naming exactly this
hazard for the car itself). `DERIVED`: a Discrete body can pass a static collider in one step once
it travels further than its own thickness in that step.

**The step is 1/120 s, not the project's 0.02.** `ProjectSettings/TimeManager.asset:6` says
`Fixed Timestep: 0.02`, but any scene with a `VehicleController` in it has that overwritten with
`1/120` from `Awake` and from every `FixedUpdate` (§1.2, C10), and Hard has one. The thresholds at
the step that actually runs:

| Prop | thin axis | threshold at 1/120 s | at the project's 0.02 s, for comparison |
| --- | --- | --- | --- |
| `RoadBlocker04` | 0.967 m | **116 m/s** | 48.3 m/s |
| `trashcontainer01` | 2.57 m | **308 m/s** | 128.5 m/s |

`DERIVED` — thickness × 120. The thin axes are `DERIVED` from the baked `BoxCollider` `m_Size`
against world scale: `RoadBlocker04.002` is `m_Size (0.017258, 0.004204, 0.009454)` at uniform scale
230.00 → 3.97 × 0.967 × 2.17 m, and `trashcontainer01.006` is `m_Size (0.010709, 0.018157,
0.012937)` at (255.49, 141.38, 255.49) → 2.74 × 2.57 × 3.31 m. Weak trace on the serialized sizes —
they are recorded at `<notes>/notes-friction.md:367-371` rather than quoted by line from the scene.

**Both thresholds are above the car's own ceiling.** `maxLinearVelocity` is 100 m/s
(`VehicleController.cs:350`, `READ`), and **no cap is set on the props** — `MEASURED`, an absence
established by two sweeps: `maxLinearVelocity|m_MaxLinearVelocity` returns 0 lines across
`<ref>/Hard.unity`, so none of the 33 prop Rigidbodies serializes one, and the identifier's only
occurrence in any `.cs` under `Assets/Project/Scripts` is the car's own line above. So a prop is not
barred from 116 m/s in principle — but it would have to leave a 40 m/s collision travelling nearly three
times as fast as the thing that hit it. Nothing in the files says what impulse the car imparts, so
whether a prop can reach that speed at all is `UNKNOWN`. On the numbers above this is a low-priority
possibility, not the realistic candidate an earlier pass made it: that framing came from dividing by
the 0.02 s step this project overrides, which understated both thresholds by 2.4×.

**Into a delivery zone.** Of all 33 × 12 prop-to-anchor distances in Hard the closest is 15.86 m
(`RoadBlocker04.001` → `DropOff 06 South West Yard`), with **zero props within 12 m** of any anchor
(`MEASURED`; same weak trace as the box sizes above — the run is recorded at
`<notes>/notes-friction.md:385-386`, not reproduced for this document). And a prop knocked into a
zone cannot *block* a delivery regardless, because detection is a pure distance test on the car's
transform
(`ZoneDetection.cs:72,95`), not a trigger volume and not a raycast (`INFERRED`). A prop can obstruct
the approach physically; it cannot gate the check.

### 5.6 Hard has one self-delivering pickup/drop-off pair

`MEASURED` — the closest pickup/drop-off pair per scene: `Possible game scene` 93.68 m, `Easy`
83.80 m, `Medium` 44.32 m, **`Hard` 9.14 m** (`Pickup 01 South West Gate` →
`DropOff 04 Centre North`). Weak trace: all four figures come from a 36-pair sweep of resolved world
positions recorded at `<notes>/notes-friction.md:233-236` and not reproduced for this document. The
conclusion does not depend on the exact metres — two 6 m spheres (`READ` per scene, §Per-scene)
intersect at any separation under 12 m, and Hard is the only scene with such a pair.

`INFERRED` from the code path, not observed at runtime: `ZoneDetection.Update` calls `CheckPickups()`
then `CheckDeliveries()` in the same tick (`:59-60`), and `CheckDeliveries` iterates
`cargoSystem.CarriedOrders`, which by then contains the order collected one statement earlier. A car
standing in the overlap collects and delivers in a single tick.
`OrderManager.PickIndexAvoidingCollisions` guards only against a new pickup landing on another
*active order's* pickup and a new drop-off on another active order's drop-off (`:202-203`, 0.5 m
threshold at `:238`); it never compares the pickup it just chose against the drop-off it is about to
choose for the same order. Both indices are uniform over six, so this pair comes up about **1 spawn
in 36** in Hard (`DERIVED`), paying `10 + 9.14 × 0.1 = 10.91` before condition scaling.

### 5.7 The telemetry's time columns are unreliable in two different ways

Both `shift_log.csv` files, as read on 14 October 2026 (`MEASURED`):

| File | timestamp | shift_s | play_s | delivered | late | value |
| --- | --- | --- | --- | --- | --- | --- |
| `Game Build/…` | 2026-10-14 09:09:12 | 300 | 163.5 | 4 | 4 | 158 |
| `Game Build/…` | 2026-10-14 09:17:49 | 300 | 300.0 | 6 | 10 | 216 |
| `Game Build/…` | 2026-10-14 09:29:20 | **0** | 268.7 | 0 | 12 | 0 |
| `Game Build/…` | 2026-10-14 09:38:11 | **0** | 221.4 | 0 | 10 | 0 |
| `PlaytestLogs/…` | 2026-10-12 13:33:39 | 300 | 300.0 | 9 | 7 | 61 |
| `PlaytestLogs/…` | 2026-10-14 11:57:50 | 11 | **0.0** | 2 | 12 | 59 |
| `PlaytestLogs/…` | 2026-10-14 12:00:25 | 185 | **0.0** | 1 | 4 | 33 |

**Failure mode one: `shift_seconds = 0` while `play_seconds` is 221–269.** `WriteRow` computes
`shiftSeconds = shiftTimer != null ? ShiftLengthSeconds() : 0f` (`PlaytestLogger.cs:244`, verified)
and `ShiftLengthSeconds` returns
`Mathf.Round(Time.time - _shiftStartTime + shiftTimer.TimeRemaining)` (`:302`, verified).
`TimeRemaining` is clamped to 0 as it crosses (`ShiftTimer.cs:61-67`), so that expression is ≥ 269
for any reachable value and can never be 0. `DERIVED` by elimination: the only path is the **false
arm of the ternary**, i.e. `shiftTimer != null` evaluating false, which happens when the
`ShiftTimer`'s native object has already been destroyed — Unity reports a destroyed component as
null. `INFERRED`: the row is written from `OnDestroy` (`:134`), where ordering across objects is not
guaranteed, so on quit the `ShiftTimer` can go first.

**Failure mode two: `play_seconds = 0.0` with non-zero `shift_seconds` and real deliveries.** Two
editor rows show this, and it is the inverse pattern. `playSeconds` is `Time.time - _shiftStartTime`
(`:243`), so a 0.0 means those two terms were equal at write time while `shift_seconds` of 11 and
185 means `TimeRemaining` was 11 s and 185 s. The mechanism is `UNKNOWN` — §6.

**The consequence for anyone reading these rows.** The comment above the write call describes the
opposite of what happens: "A shift cut short by stopping Play mode still gets its row, flagged by a
shorter play_seconds than shift_seconds" (`:131-133`, `READ`). On the first failure mode the flag is
inverted. An analyst filtering on `play < shift` to find truncated sessions will miss precisely the
two rows with 0 delivered — the rows most likely to be read as evidence about onboarding. What is
**not** damaged: `delivered`, `late`, `value` and `wall_hits` are accumulated in the logger's own
managed fields and are unaffected, which is what makes the §3.3 cadence check safe.

A smaller related defect: `ShiftLengthSeconds` exists only because `ShiftTimer` does not expose its
configured length (`:297-303`), yet `shiftDurationSeconds` is serialized in plain sight in every
scene at 300. A public getter would make the reconstruction, and this bug, unnecessary.

### 5.8 The game is silent

`MEASURED`, absence established by exhaustive search: **no audio file of any kind under `Assets/`**
(`*.wav, *.mp3, *.ogg, *.aiff, *.aif, *.mod, *.xm, *.flac` → 0 files), and **no `AudioSource`,
`AudioClip`, `AudioMixer` or `PlayOneShot` anywhere** in any `.cs`, `.unity`, `.prefab` or `.asset`
in the project, including the three `origin/main` scenes. Every gameplay scene does carry an
`AudioListener` on its camera, and `VehicleLabSceneBuilder.cs:290` adds one deliberately. A listener
with nothing to hear.

Named as friction rather than a missing feature because the hooks were designed in:
`VehicleController` exposes `IsDrifting` with a comment that it exists so "audio, particles and any
drift scoring" read one definition (`:243-246`), and `KnockableProp`'s header lists "a sound" as one
of the things its marker component is for (`:12`), both `READ`. For a driving game the engine note
is the main speed cue and the impact sound is the main collision cue; with neither, the only
feedback that the car hit something is the visual jolt.

### 5.9 Serialization drift

`MEASURED` — `python docs/tooling/scene-tools/serialization_drift.py` over the scenes, the order
scripts, the vehicle tuning assets and the `<ref>` blobs: 15 files checked, 59 stale keys. They sort
into three piles, and the sorting matters because most of the suspected drift lives in the working
tree only and is already fixed on `origin/main`.

**Genuinely stale on `origin/main`:**

| Key | Where | What runs instead |
| --- | --- | --- |
| `orderTimeLimit: 50` | `OrderTuning_Balanced.asset:33` | 45 s — §3.3. Not renamed; unreachable because the YAML above it is broken |
| `enableMultiLegOrders`, `multiLegChance`, `minTransferLegs`, `maxTransferLegs`, `extraTimePerTransferLeg` | `OrderTuning_Balanced.asset:36-40` | nothing. Multi-leg orders do not exist in code; the asset describes a removed feature |
| `whyThisSetup` | `OrderTuning_Balanced.asset:16` | nothing, and it breaks the keys below it |
| `patchKinds` | every scene, both trees | nothing; absent from `VehicleLabBuilder.cs` |
| `headerFontSize`, `orderLineSlots`, four colour keys, `warningThreshold` | `VehicleLab.unity:9710-9716` | nothing; absent from `OrderHUD.cs` in both trees. Low-stakes — `VehicleLab` is `enabled: 0` in build settings |

**Already cleaned on `origin/main`, working-tree only:** `hardBrakeDecelThreshold: 18`,
`transferRadius: 6`, and the `OrderHUD` colour and font block in `Possible game scene.unity`. Note
that the stale `18` would have been *below* the car's ordinary 22 m/s² brake — the exact bug
`CargoCondition.cs:21-33` says was fixed; the fix landed in code and the old scene value lingered
until the scene was re-saved. Also note `urgentBelowSeconds: 8` on the HUD block became
`urgentShiftSeconds: 30` on `origin/main`, so the HUD turns the clock urgent at 30 s, not 8.

**Not stale at all:** `ticketScale`, `mainMenuSceneName`, `playerProgression` and `xpColour` in the
three new scenes are declared on `origin/main` (`ShiftResults.cs:58,62,75`,
`OrderTicketStrip.cs:41`, `ButtonsUI.cs:11`). They flag only when the new scenes are checked against
the local branch's older copies of those scripts. **Anyone re-running the drift checker must match
the scene's tree to the script's tree.**

What Unity does with a stale key: drops it on load, keeps the C# initialiser value, and the key
survives in the file until that object is next re-serialized (`READ`, demonstrated inside this repo
— the three new scenes and `origin/main`'s `Possible game scene` were saved after the
`CargoCondition` rename and carry the current names; the working tree's older copy does not).

### 5.10 Dormant vehicle data, and one live field with no reader

**The three vehicle tuning assets are structurally clean:** 108 serialized tuning keys against 108
public fields in `VehicleTuning.cs`, zero mismatches in either direction, in all three
(`MEASURED` by set comparison; verified directly for `Tuning_Balanced` — 118 two-space keys less 10
Unity boilerplate). Nothing serialized is unread and nothing declared is missing. **This corrects a
premise this investigation was given:** `hardBrakeDecelThreshold` is not in any vehicle tuning
asset; it was a scene key on a `CargoCondition` component, and it is already gone on `origin/main`
(§5.9). What is dormant in the vehicle assets is dormant on purpose, with one exception:

- **`handbrakeGripMultiplier` — a live field with no reader.** Declared at `VehicleTuning.cs:302`
  and serialized in all three assets (0.25 / 0.35 / 0.18), and the only match for the identifier
  anywhere in `Assets/**/*.cs` is its own declaration (`MEASURED`, 1 hit). The handbrake's real
  grip dial is
  `handbrakeRearGripMultiplier` (`VehicleController.cs:1087`). Changing this one in the inspector
  does nothing. It reads as the pre-`perAxleGrip` four-wheel version of the same idea.
- `accelerationBySpeed` and `maxCurvatureBySpeed` — five and six authored keyframes per asset, read
  only when `accelerationFromFormula` / `steerFromGripBudget` are false, and both are `1` in all
  three (`READ`). Kept deliberately as A/B switches, but anyone opening the asset in the inspector
  sees authored curves that are not driving the car. `maxCurvatureBySpeed`'s own docstring describes
  a 12 m/s² grip budget that no longer ships (`VehicleTuning.cs:841-851`).
- `boostRechargeRate` / `boostRechargeDelay` — intentionally dead, documented as a switch
  (`VehicleTuning.cs:205-210`). Worth distinguishing from the first item: this one is a design
  choice, that one is a leftover.
- `isChosenSetup` — documentation only, nothing reads it (`VehicleTuning.cs:40-41`).

### 5.11 Beacon ground discs may be under the road

`OrderBeacon` places its ground disc `groundDiscYOffset = 0.05` m above the order point
(`OrderBeacon.cs:48,160`, `READ`), and every order point is at y = 0.00 (`MEASURED`, all twelve in
all four scenes). In Hard and Medium the city sits 0.40 m above the floor plate, which puts the disc
about 0.35 m **below** the road surface (`DERIVED`). The 14 m beam above it
(`OrderBeacon.cs:43`) would still be visible. `INFERRED`; settle by looking at one beacon in the
editor.

---

## Contradictions found

Each entry names both sources, the check that discriminated between them, and which one the code
uses at runtime. The two prior QA documents —
`docs/QA-PLAYTEST-REPORT-2026-10-14.md` and `docs/playtesting/QA-AI-20261014-01.md` — predate the
Easy/Medium/Hard restructure. **Where they disagree with code, the code wins, and that is said
explicitly.**

### The five that change what you would build

**C1 — Order value: flat 10 versus the logged 61 / 158 / 216, and the `valuePerMetre` fallback.**
*A:* a prior QA pass — delivery value is a flat 10. *B:* `OrderTuning.cs:48-50` +
`OrderManager.cs:298` — value is `(baseDeliveryValue + metres × valuePerMetre) × condition`. The
complicating fact is that `OrderTuning_Balanced.asset` has **no `valuePerMetre` key**, so the
question is whether the C# initialiser `0.1f` survives. *Check:* read the field out of the shipped
build's serialized asset data, then bound it independently from the logs. *Verdict: B, and
`valuePerMetre = 0.1` is what ran.* The build bytes read 0.1 (`MEASURED`), and flat 10 is
arithmetically impossible against 158 for four deliveries and 216 for six — the logs alone force
`v ≥ 0.076` (`DERIVED`). **A is wrong.** The 61-value row is the cargo-condition row: nine
deliveries at 61 total forces a mean condition ≤ 0.678 (`DERIVED`).

**C2 — Order time limit: 45 versus 50 versus 75, and which the build used.**
*A:* `OrderTuning.cs:31` — C# default 45. *B:* `OrderTuning_Balanced.asset:33` — 50, with a 400-word
rationale at `:16` arguing specifically for 50 over 45. *C:*
`docs/tooling/scene-tools/order_distances.py:25,68` — 75. *Check:* read the value out of the build
binary, then model the expiry cadence against the two zero-delivery log rows at six frame rates.
*Verdict: the build ran 45, in every scene, because the limit is global.* The build bytes read 45.0
(`MEASURED`); the cadence model predicts 10 expiries by 221.4 s at 45 s and 9 at 50 s, and the log
says 10 (`DERIVED`). The asset's 50 is unreachable because `whyThisSetup` lost its indentation in
merge `03e0752`, breaking the YAML mapping. 75 was a real asset value only at `1c52e42`, reverted
the same day, and is otherwise a tooling default — **never a game value**. The QA report observed
the 45 and recorded the cause as unverified (`docs/QA-PLAYTEST-REPORT-2026-10-14.md:183`); this
closes it, and on this point the QA report happens to be right.

**C3 — EndShift versus ZoneDetection: which counters freeze.**
*A:* `ShiftTimer.cs:8-10` + `:87` — disabling `OrderManager` freezes the shift. *B:*
`ZoneDetection` is on the `Car`, not on `OrderManager`, and `ResolveDelivery` has no `enabled` and
no `IsShiftActive` guard (`OrderManager.cs:287`, verified). *Check:* confirm the two components sit
on different GameObjects by reading each one's `m_GameObject` field — not its block header — in all
four gameplay scenes, then read `ResolveDelivery` for a guard. Done for both components in all four:
`ZoneDetection` on `{fileID: 592668305}` = `Car`, `OrderManager` on `{fileID: 816895043}` =
`OrderManager`, lines in §4.2. *Verdict: B.
Deliveries and pickups still resolve after the whistle.* And the divergence is not the one that was
suspected: `ShiftResults` freezes everything, while `OrderHUD`, `PlayerProgression` and (in Hard,
Medium and Easy) `OrderTicketStrip` freeze nothing, so the corner and the centre of the screen can
show different scores at once and a post-whistle delivery still gets a `DELIVERED!` stamp.
`ResultsPanel.cs` — which does freeze both the world and its tally — **is in Hard, Medium and Easy,
inside a `Canvas (2)` prefab instance whose root is `m_IsActive: 0`**, so the fix ships disabled
rather than missing (§4.2 for the five-hop trace). An earlier pass called it absent on a GUID census
that returned 0 in every scene file; the census cannot see into a `PrefabInstance`, which is C6's
own caveat applied to colliders but not to components. `INFERRED` from the code path, not observed
at runtime.

**C4 — Unseeded `System.Random` versus the deterministic-schedule claim.**
*A:* the premise handed to this investigation — unseeded `System.Random`, four sites — and a
reported pitch claim that the order schedule is the same every run. *B:* the project has **zero**
`System.Random` (`MEASURED`, searched every `.cs`); the randomness is `UnityEngine.Random.Range` at six
sites, four of them in a branch no scene reaches. *Check:* regex search of every `.cs`, plus a search
of every tracked `.md` for the schedule claim. *Verdict: B on the mechanism, and the conclusion
survives in a more precise form.* No seed is set, so order **locations** differ every run and
therefore so do scores; the **cadence** is fully deterministic, which is why the C2 model reproduces
the logged late counts exactly. The stronger pitch claim **is not in this repository** — nothing in
any tracked `.md` claims a repeatable order sequence (`MEASURED`, not `READ`, because it is an
absence found by searching:
`git grep -l -i -E "seed|deterministic|repeatab|same orders|order sequence|fixed schedule"
origin/main -- "*.md"` returns exactly one file, `docs/tooling/repo-checks/README.md:37`, and its
hit is "seeded scratch repositories" in a description of the test harness, nothing to do with
orders). So the claim can be neither cited nor contradicted from here: `UNKNOWN`. If the deck does say it, it is right about timing and wrong about
locations, and one `UnityEngine.Random.InitState(seed)` call before the first spawn would make it
fully right.

**C5 — Camera sensitivity: a missing scale processor versus a unit mismatch.**
*A:* the working hypothesis — the mouse binding has no scale processor. *B:*
`VehicleControls.inputactions:503` carries `ScaleVector2(x=0.06,y=0.06)` (verified directly).
*Check:* read the binding. *Verdict: B. The processor exists.* The problem is downstream: mouse
delta is multiplied by `cameraSwivelSpeed` (120 deg/**s**) and by `dt`, which is correct for a stick
and wrong for a delta — so the response is both over-scaled and frame-rate dependent, and the
0.167-pixel gate is not a deadzone (§5.2). Three faults, none of them a missing processor.

### The rest

**C6 — `Possible game scene` on `origin/main`: city deleted, or extracted to a prefab?**
*A:* a class-id census — 0 MeshColliders, 3 MeshRenderers, blob shrunk 414 KB → 101 KB, therefore
the district was removed and the scene is a bare plane. *B:* the scene holds one
`!u!1001 PrefabInstance`. *Check:* resolve its `m_SourcePrefab` GUID. *Verdict: A is wrong, and I
verified B directly.* The blob has exactly one PrefabInstance (line 3415) whose GUID
`1c3c5ea4155a8a849ba37da00eb32075` resolves to
`origin/main:Assets/Project/Prefabs/Group 15 Greybox real one 1.prefab` — 96 GameObjects, 90
MeshColliders, 0 Rigidbodies, 0 negative world scales. **Nothing was lost; the greybox moved.** This
matters beyond the one scene: **a class-id or GUID census of a scene file does not see prefab
contents**, so every per-scene count in this document — components as well as colliders —
understates Hard, Medium, Easy and `Possible game scene` by whatever their prefab instances
contain. The resolved inventory is in §Per-scene, and the one row where the omission changed a
conclusion rather than a count is `ResultsPanel`: 0 by census in all four scenes, actually present
in three (§4.2).

**C7 — Does `Roads_Parent` exist in `Hard.unity`, and does it have geometry?**
*A:* `.agents/tasks/turning-geometry-2026-10-14/report.md` §4.3 — "There is no `Roads_Parent` object
in this scene". *B:* two other passes report it, with a null mesh. *Check:* read the GameObject and
every component document. *Verdict: A is wrong on existence; B is right, and the null mesh is the
headline.* Verified directly: `m_Name: Roads_Parent` at `<ref>/Hard.unity:199843`, with a
MeshCollider at `:199849` — `m_Enabled: 1`, `m_Convex: 0`, **`m_Mesh: {fileID: 0}`** at `:199870` —
and a `SurfaceType` Tarmac tag at `:199880-199886`. A third pass described a hole in that mesh as a
hole in the world; there is no mesh to have a hole in. Combined with the fact that **zero placed
road tiles own a collider** (`MEASURED`), what holds the car up in Hard is genuinely open — §6.

**C8 — Street widths "8.8 m to 25.5 m".**
*A:* `find_roads.py` on the working-tree `Possible game scene`, which reproduces exactly seven
north-south and seven east-west centrelines at 8.8–25.5 m kerb spacing. *B:* the same tool on the
`origin/main` blobs, which finds **0 pavement objects** in Hard, Medium and `Possible game scene`
and refuses to run on Easy for lack of a `Floor`. *Check:* run it against all four `<ref>` scenes;
then C6. *Verdict: both are right about different trees, and neither figure is the constraint.* The
kerbs are only visible to the tool where the greybox is inline, which on `origin/main` it is not
(C6). And the kerbs are **0.24 m tall strips** — `SideWalk001.005` spans y −0.05 to 0.19 (`MEASURED`) —
against a car body collider at y 0.50–1.50 (`DERIVED` from `m_LocalPosition.y: 0.8` at
`<ref>/Hard.unity:1246` plus `m_Center.y: 0.2` and `m_Size.y: 1` at `:1235-1236`, §6 item 8) and
wheels that are raycasts with 0.9 m of reach, so **the car drives over them**. The real constraint in that scene is the 28 building blocks that
stand above the sill, giving a 25.5 m tightest route corridor. The `README.md` in
`docs/tooling/scene-tools/` still presents the kerb grid as current; it describes a tree state that
`origin/main` does not have.

**C9 — Grip 12 versus 13.** `VehicleTuning.cs:297` declares `lateralGripAcceleration = 12f`;
`Tuning_Balanced.asset:169` serializes `13`. Unity's YAML overlay wins, so **runtime uses 13**. The
stale 12 is still quoted in two comment blocks that no longer match the shipped car —
`SurfaceType.cs:150-152` and `VehicleTuning.cs:841-851`. Documentation defect; code is right.

**C10 — Physics rate, gravity, max angular speed: project settings versus the car.**
`TimeManager.asset` says 50 Hz, `DynamicsManager.asset` says gravity −9.81 and
`m_DefaultMaxAngularSpeed: 50`. The car overrides all three — 120 Hz
(`VehicleController.cs:365`), gravity 20 with `useGravity = false` (`:339,407`), max angular 7
(`:343`). **Runtime uses the car's values**, in any scene containing a `VehicleController`.

**C11 — "Measured top speed is 32 m/s".** *A:* `OrderTuning_Balanced.asset:21` and the CSV's
`Profile / Top speed = 32.0`. *B:* the `Profile` test does not drive the car; it echoes tuning fields
(`VehicleMeasurement.cs:981-987`), and there is **no top-speed test** in the harness. *Check:* read
what F7 does, and look for a driven top-speed reading. *Verdict: 32 is a `READ` of the asset, not a
measurement.* The only driven evidence is an entry speed of **31.3653 m/s** reached in 3.75 s, which
the harness itself annotates as 98 % of the 32.0 target because `brakeTestSpeedFraction` is 0.98
(`VehicleMeasurement.cs:71`). The true asymptote is `UNKNOWN` — §6. A related stale pair survives in
history: the nine oldest CSV rows (2026-09-09) echo top speed 25.0 and sideways grip 12.0, and all
103 later `Profile` rows echo 32.0 and 13.0 (`MEASURED`). **Latest wins: 32 / 13**, and
`VehicleTuning.cs:132` still carries a tooltip reading "25 m/s is about 90 km/h" next to a default
of `32f`.

**C12 — The asset's own stopping-distance claim.** `Tuning_Balanced.asset`'s `whyThisSetup` says
stopping distance is 23.3 m from 32 m/s. Code plus measurement give **18.09 m from 31.37 m/s**,
because the asset text uses the brake-only formula and the car also coasts (§1.7). **The measurement
is right; the asset prose is about 23 % pessimistic.** This one errs safe — the camera shows more
road than needed, not less. The same brake-only error is in
`VehicleTuning.PredictedStoppingDistanceFromTopSpeed()` (`:795-800`).

**C13 — "The rear is given 84 per cent of the grip the front has"** (`Tuning_Balanced.asset`
`whyThisSetup`). The arithmetic at `VehicleController.cs:1075-1076` gives front 15.08 and rear
10.92 m/s², so the rear is **72.4 %** of the front (`DERIVED`). 84 % is the rear's share of the
single-force budget (0.42 × 2). The split is real and front-biased; the sentence states it against
the wrong denominator. Two sibling cases: `Tuning_Grippy`'s prose says "Grip 18" against a
serialized `17`, and `Tuning_Loose`'s says "Grip 8" against a serialized `10`. Nothing reads the
prose, so these are documentation defects only.

**C14 — README's boost description versus the earn condition.** `README.md` says a handbrake slide
pays into the tank at 20 a second. The code's earn test is `IsDrifting` — grounded, ≥ 4 m/s, slip
≥ 12° — and reads nothing about the handbrake (`VehicleController.cs:910-920`). **Code wins on the
condition**; the handbrake is one way to reach that state and a throttle-induced slide pays
identically. The rates, the tank size, the 3 s duration and the 32 → 40 m/s figures in the README are
all confirmed against the asset.

**C15 — `CargoCondition` "off by default" versus every scene turning it on.**
`CargoCondition.cs:15-19,42` says off by default, "exactly Milestone 1 behaviour". All four scenes
serialize `enableCondition: 1` (verified). **It is on in the shipped game**, and it is the term that
explains the 61-value shift. The C# default is irrelevant to what ships.

**C16 — `ZoneDetection.checkInterval`: code default 0 versus scenes 0.1.** The tooltip says 0 "stops
a fast car clipping through a small radius between checks" (`:29-31`); all four scenes serialize
`0.1` (verified). **The scenes override the code's own stated rationale**, costing about 0.22 m of
guaranteed detection radius at 32 m/s.

**C17 — Late penalty: a tuned field nothing reads.** `OrderTuning.cs:44` and
`OrderTuning_Balanced.asset:35` both carry `latePenaltyPerSecond: 1`; `OrderManager.cs:274` passes a
literal `value: 0f` on the late path. One hit for the identifier across every `.cs` — its own
declaration. **The field is dead.**

**C18 — "No `PlayerProgression` component is attached to the game scene."**
*A:* `docs/QA-PLAYTEST-REPORT-2026-10-14.md:204`. *B:* Hard, Medium and Easy each have one.
*Check:* count the script GUID per scene. *Verdict: A is true of `Possible game scene` and false of
the three scenes on `origin/main`.* **The QA report predates the restructure and is correct only
about the scene it tested.** Code wins. The economy is still unreachable (§4.4), so the QA report's
conclusion survives even where its premise does not.

**C19 — "The upgrade screen calls `TrySpendMoney` / `TryUnlockCapacity`."** `PlayerProgression.cs:14`
and `CargoSystem.cs:18-21` say so. No caller exists in any `.cs` and no `m_MethodName` in any scene
reaches either. **The upgrade economy is unreachable code.**

**C20 — Knockable prop masses "60 / 120 / 180 / 5 / 25 kg".** Five families exist in the baker table
(`KnockablePropBaker.cs:89-96`), but the 5 kg `cone_type` family has **zero** baked instances; the
33 props in Hard are 12 × 180, 9 × 120, 9 × 60, 3 × 25 (`MEASURED`). **The table is right, the scene
inventory is not.** The cone-shaped things on the road are `RoadBlocker04` at 25 kg.

**C21 — "Floor top face y = 0.4."** The `Floor` BoxCollider is a unit box at the object's origin on
a Transform at local y = −1 with y-scale 2, so it spans y[−2.00, 0.00] — top face **y = 0.00** — in
Hard, Medium and `Possible game scene`, and Easy has no Floor at all. Lines for all three, and the
identity ancestor chain the derivation needs, are in §Per-scene. The 0.4 is the `City` root's `m_LocalPosition`, which is where the road
*visuals* sit. **They differ by 0.40 m**, and `KnockablePropBaker.cs` disagrees with itself about it
— `:40` says the road surface is at y = 0, `:346` says the road collider sits at +0.40. Unresolved in
the files; same question as §6 item 1.

**C22 — "PhysX cannot run a Rigidbody under negative non-uniform parent scale" as the reason props
were reparented.** `KnockableProp.cs:27-29` and commit `7f357c7` state that mechanism. *Check:*
print the full local-scale chain for every collider under `City` and multiply it out. *Verdict: the
negatives cancel* — 1311 of 1312 colliders under `City` resolve to positive world scale, and the
props were never under a net-negative chain (`MEASURED`). The reparenting is still sound for other
reasons (hand-tunability, the undo path), but the stated mechanism overstates what was wrong. The one
genuinely negative collider is `Bastrop-blocks-combos.001`, which the reparenting did not touch
(§5.4).

**C23 — `PlaytestLogger`'s truncation flag.** The comment says an early quit is "flagged by a shorter
play_seconds than shift_seconds" (`:131-133`); two rows have `play_seconds` **longer** than
`shift_seconds` and two more have `play_seconds = 0.0`. **The comment describes the ternary arm that
did not run** (§5.7). `DERIVED`.

**C24 — "Five logged shifts", and "no logged player has reached supersonic."** Both were true of an
earlier read of the gitignored logs and are not true now. There are **seven** rows across the two
files, and `PlaytestLogs/shift_log.csv` row 2 records **`time_above_supersonic_s = 2.5`**
(`MEASURED`). That row also has `boost_activations = 0` and `boost_seconds = 0.0`, which is
anomalous: `supersonicThreshold` is 38 m/s, `topSpeed` is 32, the acceleration formula makes 32 an
asymptote rather than a ceiling to exceed, and the flip governor adds at most 3 m/s and only below
12 m/s. So a car that never boosted should not have spent 2.5 s above 38 m/s. **How that row was
produced is `UNKNOWN`** — §6. What survives unchanged: `boost_activations` is 0 in all seven rows, so
boost has still never been activated in a logged shift.

---

## 6. Unknown / needs a running game

Everything this investigation could not settle without play, each with the single check that would
settle it. Nothing in this list is estimated elsewhere in the document.

### Physics and collision

1. **Which collider holds the car up, in each scene.** Three candidates in Hard at three heights:
   the `Floor` box with its top face at y = 0.00, the road visuals at y ≈ 0.40 that own no collider,
   and `Bastrop-blocks-combos.001` at pivot y ≈ 0.398 under a negative non-convex scale. **Check:**
   one `Physics.Raycast` straight down from the car's spawn, or a play-mode drop test, logging
   `hit.collider.name` and `hit.point.y`. This single check also settles C21, the 0.40 m visual
   offset, and item 2 below.
2. **Whether any `SurfaceType` tag is ever read.** Neither Tarmac tag in Hard is on a collider the
   car can touch (one has a null mesh, the other is 48 m underground), and the `Floor` box carries no
   tag. **Check:** the same raycast, plus logging the blended surface each step on a drive across a
   park base.
3. **Whether `Easy` has any drivable ground at all.** No `Floor`, no road geometry, one
   triple-negative-scaled non-convex MeshCollider. **Check:** the same raycast, run in Easy.
4. **Whether negative scale actually breaks the `Bastrop` collider.** The repo asserts PhysX cannot
   run a Rigidbody under such a chain and that BoxCollider forces negative scale positive with
   "unexpected collision geometry" (`KnockableProp.cs:17-33`); it says nothing about a static
   non-convex MeshCollider. **Check:** drive onto it in Easy in play mode, or compare the
   MeshCollider gizmo against the visible blocks in the editor.
5. **What speed a struck prop actually leaves at** — low priority, and the reason it is low is
   worth keeping. Tunnelling needs 116 m/s for the thinnest prop at the 1/120 s step the car
   installs, against a car capped at 100 m/s and a 40 m/s boost ceiling (§5.5). Props carry no cap
   of their own and the impulse is not readable from the files, so the possibility is open and
   remote rather than likely. **Check:** ram a `RoadBlocker04` at boost speed with the prop's
   `linearVelocity.magnitude` logged; if it peaks anywhere near 100 m/s the question becomes real,
   and if it peaks at car-like speeds this item closes.
6. **Whether the car can come to rest inverted**, which is what makes the no-reset/no-flip trap in
   §5.3 reachable. **Check:** flip it onto its roof against a kerb in play mode and try to recover.
7. **Whether any delivery point sits inside a building footprint.** All twelve in all four scenes
   sit at y = 0.00 inside the `Floor` footprint where one exists, but transforms cannot see building
   meshes. **Check:** a downward raycast from each point in play mode, or `fbx_mesh_bounds.py`
   against the building meshes.
8. **Whether the car is clear of the road surface at spawn in Hard and Medium.** By transform the
   collider's bottom face is 0.5 m above the `Floor` top face, which leaves 0.1 m once the city's
   0.40 m offset is applied. `DERIVED` from four `READ` values: the `Car` root at
   `m_LocalPosition.y: 0.8` (`<ref>/Hard.unity:1246`, `Medium:39112`, both `m_Father: {fileID: 0}`
   at `Hard:1250` and `Medium:39116`, so local equals world), the BoxCollider's
   `m_Center.y: 0.2` and `m_Size.y: 1` (`Hard:1235-1236`) giving `0.8 + 0.2 − 0.5 = 0.50 m`, the
   `Floor` top face at y = 0.00 (`:4453-4454` with `:4527-4528`), and the `City` root's 0.40 m lift
   (`:187642`) giving
   `0.50 − 0.40 = 0.10 m`. **Check:** `fbx_mesh_bounds.py` on the road mesh for the true top face,
   or the item 1 raycast.
9. **Speed bump height.** 16 in Hard, 17 in Medium, at world y = 0.228. The mesh
   `Assets/Aside/Art/FastMesh/Meshs/Road/SpeedBump.fbx` exists on `origin/main` but not in the local
   working tree. **Check:** fetch it through LFS and run `fbx_mesh_bounds.py` on it. Do **not** set
   `GIT_LFS_SKIP_SMUDGE=1`.
10. **Whether the footbridges are drivable.** 11.49 m tall, pivot y 0.39, no ramp found.
    **Check:** play-mode, or `fbx_mesh_bounds.py --json` plus a gradient comparison against the
    suspension travel.
11. **Whether the road slab's top face is at 0.400 or 0.502.** The slab is 0.204 m thick
    (`DERIVED` from a 0.1000 FBX bound recorded only at `<notes>/notes-map.md:185`, weak trace,
    §2.1) with its pivot at world y 0.40 (`<ref>/Hard.unity:187642`); whether the pivot is centred
    or on the top face is not in the printed table. **Check:** `fbx_mesh_bounds.py --json` and read
    `min`/`max` for `road_base`.

### Vehicle and camera

12. **Throttle-only top speed as a driven number.** There is no top-speed test; F1 stops at 0.98 of
    the asset value. **Check:** hold full throttle until `ForwardSpeed` stops rising and log the
    asymptote, or re-run F6 with `brakeTestSpeedFraction` at 1.0 and read `Entry speed`.
13. **Turning circle at any speed other than 9.84 m/s.** `turnTestSpeed` is 10 everywhere and all 15
    CSV rows are that one speed. The radius table in §1.4 is arithmetic, and the car becomes
    grip-limited above 29.1 m/s where no reading exists. **Check:** run F2 at 20 and 30 m/s and
    compare `Limiting factor`.
14. **Any handling figure at all for `Tuning_Grippy` or `Tuning_Loose`.** Zero rows in the CSV; both
    are referenced by no scene. **Check:** assign the asset to the lab car and press F6.
15. **Whether the lab figures describe the scenes anyone plays.** The readings come from
    `VehicleLab.unity` (5 tagged surfaces, no `WeatherState`). **Check:** run F1/F2 in each shipped
    scene, on road and on grass, and compare.
16. **Jump height with the hold held full.** The impulse-only figure is 1.06 m (`DERIVED` from
    `PredictedJumpHeight()`). Adding 0.18 s of hold at a net −2 m/s² gives a larger number, but it
    ignores any suspension contribution at launch, so no figure for it is quoted here.
    **Check:** log peak `transform.position.y` on a held jump from flat ground.
17. **Whether the rear really does step out under braking in play.** The parameters, the code path
    and the arithmetic are in §1.3; the feel is not statically measurable. **Check:** a logged drive
    with brake input and `SlipAngle` recorded together.
18. **Which row of the sensitivity table the tester felt.** The response is frame-rate dependent by a
    factor of about 5 across 30–144 fps. **Check:** a frame-rate reading from that machine and build.
19. **Whether the beacon ground discs are visible in Hard and Medium**, given the 0.40 m city offset
    against the 0.05 m disc lift. **Check:** look at one beacon in the editor.

### Orders, economy and telemetry

20. **Per-order value breakdown for the logged shifts**, which would close the condition arithmetic
    exactly rather than bounding it at ≤ 0.678. `OrderManager` already logs distance, limit and value
    per order (`:205-215`, `:308-311`). **Check:** capture one shift's `Player.log`. No such log is
    kept in the repo (searched for `*.log` and `output_log.txt` — none).
21. **The exact mean cargo condition on the 61-value shift.** Only the bound is derivable; a point
    figure needs the nine actual designed distances, which are not recorded. **Check:** as item 20.
22. **Whether Unity's own YAML reader rejects `OrderTuning_Balanced.asset` outright or parses it
    partially.** The build bytes prove the outcome (45, with `profileName` loaded); the mechanism is
    inferred, and only one field discriminates. **Check:** open the asset in the Inspector — if
    `Order Time Limit` reads 45 the parse is failing. Or add a second key after `whyThisSetup` whose
    value differs from its C# default and read it back.
23. **Whether any tester's condition loss came from a `KnockableProp` rather than a wall.**
    `CargoCondition.cs:211-213` already logs each damage event. **Check:** add
    `collision.collider.name` to that line, or cross-reference `KnockableProp`'s own log, and read
    one `Player.log`.
24. **What a post-whistle delivery looks like on screen** (C3). **Check:** let the clock expire while
    carrying a parcel, drive into the drop-off, and compare the HUD corner against the TIME UP panel.
25. **How a logged row reached `time_above_supersonic_s = 2.5` with `boost_activations = 0`**
    (C24). **Check:** reproduce with `ForwardSpeed` logged alongside the supersonic timer, and read
    `PlaytestLogger`'s supersonic accumulation against `VehicleController.IsSupersonic`.
26. **Why two rows have `play_seconds = 0.0` with real deliveries** (§5.7, failure mode two).
    **Check:** quit a build mid-shift and read the written row; compare `_shiftStartTime` against
    `Time.time` at write.
27. **Whether the pitch deck claims a repeatable order schedule** (C4). Not in the repo.
    **Check:** read the deck's §4.2.
28. **Easy's street widths.** `find_roads.py` cannot run on it for lack of a `Floor`. **Check:**
    give the tool explicit bounds, or restore a `Floor`, then re-run; the 54 kerb meshes are the same
    ones the README's figures came from, at the same import scale 8.97659.

---

## 6a. Claims demoted while writing this

Forty-seven claims have been struck, demoted or re-sourced across four passes because their numbers
could not be pointed at a file. Recorded so a later pass does not reinstate them.

**First pass — twelve claims that arrived from the investigation notes.**

| # | Claim as it arrived | What happened | Why |
| --- | --- | --- | --- |
| 1 | `Possible game scene` on `origin/main` is a bare 504 × 555 m plane; the city was deleted | **struck** | 1 `PrefabInstance` at `<ref>/…:3415` resolves to `Group 15 Greybox real one 1.prefab` on `origin/main`. Verified directly. C6 |
| 2 | `Hard.unity` has no `Roads_Parent` object | **struck** | `m_Name: Roads_Parent` at `<ref>/Hard.unity:199843`. Verified directly. C7 |
| 3 | A hole in the merged road mesh is a hole in the world | **struck** | `m_Mesh: {fileID: 0}` at `<ref>/Hard.unity:199870` — there is no mesh to have a hole in. C7 |
| 4 | Mean cargo condition ≈ 0.21 on the 61-value shift | **demoted to `UNKNOWN`** | the 0.21 substitutes the 36-pair pool mean for nine unrecorded designed distances. Only the bound ≤ 0.678 is derivable. §6 item 21 |
| 5 | Target camera sensitivity 0.03–0.05 deg/px | **struck** | its own source calls it a recommendation, not a measurement; no file states a target. §5.2 |
| 6 | Jump height ≈ 2.1 m with the hold held full | **demoted to `UNKNOWN`** | ignores any suspension contribution at launch. Only the 1.06 m impulse-only figure is derivable. §6 item 16 |
| 7 | Five logged shifts | **re-sourced to seven** | both gitignored logs re-read on 14 Oct: 4 + 3 rows. C24 |
| 8 | No logged player has reached supersonic | **struck** | `PlaytestLogs/shift_log.csv` row 2 records `time_above_supersonic_s = 2.5`. C24 |
| 9 | Hard's road grid has 84 distinct x lines and 72 z lines | **demoted** | a second run reports 66 and 65. Only the 8.18 m module reproduces across both, so only that is quoted. §2.1 |
| 10 | Measured top speed is 32 m/s | **demoted to `READ`** | the `Profile` test echoes the asset rather than driving the car; the only driven figure is 31.3653 m/s and no top-speed test exists. C11 |
| 11 | Street widths 8.8–25.5 m are not reproduced by any method | **re-sourced** | they reproduce on the working-tree scene and are invisible on `origin/main` because of demotion 1. Both are right about different trees, and 0.24 m kerbs are not the constraint either way. C8 |
| 12 | `mass: 900` at `Tuning_Balanced.asset:35` | **re-sourced to `:33`** | `:35` is `maxAngularSpeed: 7`. Verified directly |

**Second pass — nine claims this document itself made and could not support.** Two changed a
conclusion, the rest were citation form or arithmetic on the wrong input.

| # | Claim as this document made it | What happened | Why |
| --- | --- | --- | --- |
| 13 | `ResultsPanel` is in no scene — 0 occurrences of its GUID in all four, `MEASURED` | **struck**, and the conclusion re-sourced | false, and the label was unearnable: a GUID census cannot see inside a `PrefabInstance`, and Hard, Medium and Easy each instance `Canvas (2).prefab`, which carries an enabled `ResultsPanel`. "Not in the game" became "ships disabled behind `m_IsActive: 0`". §4.2, C3, C6, §Per-scene |
| 14 | Prop tunnelling threshold 48.3 m/s for `RoadBlocker04`, 128.5 m/s for a trash container | **re-sourced to 116 and 308 m/s** | both divided by `TimeManager.asset:6`'s 0.02 s, the step §1.2 and C10 establish the car overwrites with 1/120. At the step that runs, both land above the car's own 100 m/s cap, so §5.5's "realistic candidate" framing went with it and §6 item 5 dropped in priority |
| 15 | Hard's 650 road pivots: 615 at 0.40, 1 at 0.60, remaining 51 underground | **re-derived to 615 / 1 / 17 / 17** | the buckets summed to 667 against a verified 650. One palette had been counted twice; the real figure is 34 underground, in two 17-tile sets at −47.90 and −83.64. §2.2, §2.3 |
| 16 | The §1.3 longitudinal-demand column, 18.95 at 10 m/s and 7.54 at 25 m/s | **re-sourced** | correct, but sourced to the grip helper only. The demand comes from `VehicleTuning.AccelerationAt` (`:690-704`) on `launchAcceleration`, `topSpeed` and `accelerationFalloffPower`, none of which the section named. §1.3 |
| 17 | "spring stiffness / damper 110 / 14, `Tuning_Balanced.asset`" | **re-sourced** | neither name is a key in that file. They are `springStrength` (`:45`) and `springDamper` (`:46`), and the derivations built on them also needed `VehicleController.cs:713-714` for the acceleration units. Same treatment for `angularDamping` (`:36`), `centreOfMassOffset` (`:34`), `perAxleGrip` (`:240`), `driftYawAuthority` (`:243`) and the uncited `hardBrakeMultiplier` 1.25 (`PGS:804`, `Hard:1368`) |
| 18 | `useFrictionCircle` / strength / reference at `Tuning_Balanced.asset:234-235` | **re-sourced to `:233-235`** | `useFrictionCircle: 1` is at `:233`; the cited range covered only the other two |
| 19 | `ZoneDetection` "its 120 lines" | **re-sourced to 124** | the file is 124 lines in both trees. The absence claim it supported was right; the count traced to nothing |
| 20 | Footbridges 11.49 m tall from "FBX bounds × 0.01 × 204.391" | **re-sourced to `<notes>/notes-map.md:282-283`** | "FBX bounds" is not something a reader can open, and the three input numbers are recorded only in the task notes from an untracked tool run. Now cited as the weak trace it is |
| 21 | `ResultsPanel.cs:61` for `if (_shiftOver) return;` | **re-sourced to `:60`** | off by one; found on re-reading rather than reported |

**Third pass — twenty claims, almost all sourcing and labelling.** Sixteen answer review findings
F1–F5; four were found in this pass's own re-read, and one of those four (item 40) corrected a
statement of fact about the scene hierarchy rather than a citation. Every other figure in the group
was re-derived and came back correct.

| # | Claim as this document made it | What happened | Why |
| --- | --- | --- | --- |
| 22 | The hard-brake threshold is 27.5 m/s², `DERIVED` from `brakeDeceleration` 22 × `hardBrakeMultiplier` 1.25 | **re-sourced, and the inference hop named** | the same serialized block carries `vehicle: {fileID: 0}` and `fallbackHardBrakeDecelThreshold: 27` (`Hard:1367`, `:1369` and siblings), which is the branch the getter returns on null (`CargoCondition.cs:94`) — so the cited evidence read 27, where the margin against ordinary braking is zero rather than 0.5. 27.5 wins only through `GetComponent<VehicleController>()` in `Awake` (`:102`) on a shared GameObject, now cited for all four scenes and labelled `INFERRED`. §3.5 |
| 23 | Hard's street module: `4.0000 FBX units × 0.01 × 204.391 = 8.176 m` | **re-sourced, weak trace flagged** | the import factor was cited and the bound was not; 4.0000 and the `road_double_base` 8.0000 exist only at `<notes>/notes-map.md:185`, `:189`, and the FBX is not in the local tree. The tile-centre clustering now carries the 8.18 m on its own. §2.1 |
| 24 | Medium's street module is 13.82 m | **re-sourced, weak trace flagged** | the 11.2048 FBX input is recorded only at `<notes>/notes-map.md:206`. §2.1 |
| 25 | The road slab is 0.204 m thick | **re-sourced, weak trace flagged** | same table, same run: the 0.1000 thin-axis bound is at `<notes>/notes-map.md:185` with the arithmetic at `:197`. §2.3, §6 item 11 |
| 26 | `reverseAcceleration` 9 and `reverseTopSpeed` 8, `READ`, no path | **lines added** | `Tuning_Balanced.asset:103` and `:102`; the forward figures it is compared against are `:220` and `:50`. §5.3 |
| 27 | Flip: 9 m/s, 2.5 m/s hop, 2.5 rad/s, 0.45 s — only the 1.6 s cooldown cited | **lines added** | `flipRequiresGrounded` `:176`, `flipForce` `:177`, `flipHopSpeed` `:178`, `flipSpin` `:179`, `flipDuration` `:180`. §1.5 |
| 28 | The flip governor's `clamp(12 − speedBefore, 0, 3)`, cited to code only | **lines added** | `flipMaxSpeedGain: 3` (`:182`), `flipNoSpeedGainAboveSpeed: 12` (`:183`). §1.5 |
| 29 | The jump hold block cited as `Tuning_Balanced.asset:167-180` | **re-sourced to `:172-175`** | found in this pass's own re-read: the old range opened on `steerInputSmoothTime` and ran through the whole flip block. The four keys are `jumpHoldTime` `:172`, `jumpHoldAcceleration` `:173`, `jumpCooldown` `:174`, `inputBufferTime` `:175`, with gravity at `:32`. §1.5 |
| 30 | Car collider `m_Size` / `m_Center`, `READ`, no path | **path added, and confirmed per scene** | `<ref>/Hard.unity:1235-1236`, identical at `Medium:39101-39102` and `Easy:3207-3208`. §5.3 |
| 31 | "a car box of 1.80 × 4.20 × 1.00 m" labelled `MEASURED` | **relabelled `READ`** | it is a serialized literal, so `MEASURED` was the wrong label by this document's own taxonomy. The prop footprints beside it are genuinely measured. §2.4 |
| 32 | Spawn clearance "0.5 m above the `Floor` top face … about 0.1 m under the collider", `DERIVED`, inputs unnamed | **inputs named** | `Car` `m_LocalPosition.y: 0.8` (`Hard:1246`, `Medium:39112`, both roots), the collider at `:1235-1236`, the `Floor` top face per item 41, the `City` lift at `:187642`. §6 item 8 |
| 33 | "a car body collider at y 0.50–1.50" | **relabelled `DERIVED` with its inputs** | same two inputs as item 32; it had read as a bare figure inside a `MEASURED` sentence about kerbs. C8 |
| 34 | "the closest is 15.86 m, with zero props within 12 m", `MEASURED`, no command or row | **cited, weak trace flagged** | `<notes>/notes-friction.md:385-386`; §7's untracked-tool disclosure named §2, §3.4 and §5.9 only, so this sat outside it. §5.5 |
| 35 | Closest pickup/drop-off pairs 93.68 / 83.80 / 44.32 / 9.14 m, `MEASURED`, no command or row | **cited, weak trace flagged, conclusion re-grounded** | `<notes>/notes-friction.md:233-236`. The finding does not need the exact metres: any separation under 12 m makes two 6 m zones intersect, and the radii are `READ` per scene. §5.6 |
| 36 | The tracked-`.md` sweep for a repeatable-schedule claim, labelled `READ` | **relabelled `MEASURED`, with the one hit disclosed** | an absence found by searching is `MEASURED` by this document's own rule. The sweep returns exactly one file, `docs/tooling/repo-checks/README.md:37`, whose hit is "seeded scratch repositories" in a test-harness description. C4 |
| 37 | "the complete subscriber list is `ResultsPanel`, `ShiftResults` and `PlaytestLogger`", under a `READ` paragraph | **relabelled `MEASURED` with the command** | a sweep, not a value: three `+=` sites at `ResultsPanel.cs:49`, `ShiftResults.cs:113`, `PlaytestLogger.cs:115`. §4.2 |
| 38 | "no cap is set on the props (`READ`)" | **relabelled `MEASURED` with the command** | an absence across 33 serialized Rigidbodies. §5.5 |
| 39 | `City`'s scale and 0.40 m lift both cited to `<ref>/Hard.unity:187633` | **re-sourced to `:187643` and `:187642`** | found in this pass's own re-read: `:187633` is the `Transform` block header, not either value. The object is confirmed as the root `City` by `m_GameObject` at `:187639` against `m_Name: City` at `:212927`, with `m_Father: {fileID: 0}` at `:187722`. §Per-scene, §2.3 |
| 40 | "`Bastrop-blocks-combos.001` … negative world scale (`READ`)" with Medium's copy at the **scene root**, and "in Medium and Easy there is no parent transform that could cancel the sign" | **corrected and re-labelled** | found in this pass's own re-read while sourcing the table. Medium's object is a child of the `'City '` container (`m_Father: {fileID: 6859320381740429298}` at `<ref>/Medium.unity:181474`, that transform at `:177899-177909` with a positive uniform 1.2333, the object named `'City '` at `:165599`), so only Easy's is parentless. The world-scale figures are right but `DERIVED` in Hard and Medium, not `READ`: the serialized locals are −31877.838 (`Hard:183550`) and (−60305.746, −59762.26, −53445.06) (`Medium:181471`). Easy's is `READ` at `:15121` because it has no parent. The conclusion is unchanged — a positive parent scales without flipping the sign back. Easy's pivot y = 0.000 in §2.3 was labelled `MEASURED` and is a serialized literal (`:15120`), now `READ`. §5.4, §2.3 |
| 41 | The `Floor`'s top face at y = 0.00 cited to `<ref>/Hard.unity:4444`, `READ` | **re-sourced to `:4453-4454` + `:4527-4528`, relabelled `DERIVED`** | found in this pass's own re-read, same shape as item 39: `:4444` is the `Transform` block header. The figure resolves from four literals — a unit box at the origin (`:4527-4528`), local y = −1 with y-scale 2 (`:4453-4454`) — and needs the ancestor chain to be identity to reach world, which it is (`:771-772`, `:3142-3143`). Those last two were never checked before, so "top face y = 0.00" was a `READ` label on a three-hop derivation. §Per-scene, §2.3, §6 item 8 |

**Fourth pass — six claims, answering review findings F1–F5.** One was wrong on the facts; the rest
were sourcing and labelling in sections the earlier sweeps did not reach. Every figure re-derived in
this pass came back correct.

| # | Claim as this document made it | What happened | Why |
| --- | --- | --- | --- |
| 42 | `OrderResolved` is "subscribed by `OrderHUD`, `ShiftResults`, `CargoSystem`, `PlayerProgression` and `PlaytestLogger` (`READ`)" | **re-sourced to seven sites, relabelled `MEASURED`, and one consequence added** | the list was two short and the label unearnable. `git grep -n "OrderResolved +=" origin/main -- "Assets/**/*.cs"` returns seven: the five named plus `ResultsPanel.cs:48` and `OrderTicketStrip.cs:108`. `ResultsPanel` never attaches (it subscribes in `Awake`, `:48-49`, under an `m_IsActive: 0` root), but `OrderTicketStrip` is live in Hard, Medium and Easy and has no `ShiftEnded` subscription, no `IsShiftActive` check and no `shiftTimer` reference at all, so §4.2's freeze table was missing a surface that keeps running after the whistle. Censusing the other six to state how many attach per scene turned up a fact the document had nowhere: `PlaytestLogger` is in `Possible game scene` only — 0 in the three new scenes and 0 in their prefabs — so it now has a §Per-scene row, a scope note on §4.2's freeze row, and a clause in the telemetry preamble. §4.3, §4.2, §Per-scene, C3 |
| 43 | §5.2's camera chain, "all `READ`", with the `controlsAsset` GUID, the `tuningOverride: {fileID: 0}` and the per-scene `ChaseCamera.input` wiring carrying no path | **per-scene lines added** | three of the five hops cited nothing a reader could open, and the traces existed only in the task notes against the working-tree scene — outside §7's `<notes>` disclosure. Now read from the `<ref>` blobs: `controlsAsset` at `PGS:594`, `Hard:1158`, `Medium:39024`, `Easy:3130` (against the GUID in `VehicleControls.inputactions.meta`), `tuningOverride` at `PGS:2316`, `Hard:3455`, `Medium:81838`, `Easy:8073`, `input` at `PGS:2317`, `Hard:3456`, `Medium:81839`, `Easy:8074` resolving to the `VehicleInput` on the `Car`. The 0.06, the 120, the `dt` multiply and the 0.0001 gate were already sourced and are unchanged. §5.2 |
| 44 | "This is the only caller of `ResolveDelivery` in the project", inside a list introduced "all `READ`" | **relabelled `MEASURED` with the sweep** | a uniqueness claim is an absence claim, which this document's fourth rule makes `MEASURED`. The sweep returns four hits and one call: `OrderManager.cs:285` (declaration), `:290` (a log string), `CargoCondition.cs:13` (a comment) and `CargoSystem.cs:148`. §3.1 |
| 45 | The dedicated brake's stated intent quoted `READ` with no line | **line added** | `VehicleInput.cs:29`, inside the `///` header, which this document's second rule makes quotable. §5.3 |
| 46 | "six `pickupPoints` and six `dropOffPoints` are wired in each scene (`READ`)" with no per-scene line | **per-scene lines added** | every other row of that section's table says "verified per scene" and this one did not. The arrays are six and six in all four, at `PGS:1665`/`:1672`, `Hard:2253`/`:2260`, `Medium:54288`/`:54295`, `Easy:5520`/`:5527`. §3.4 |
| 47 | `ZoneDetection` and `OrderManager` co-location cited to `Hard:1333`, `Medium:39199`, `Easy:3305`, `PGS:769` and `Hard:2240`, `PGS:1652` | **re-sourced to the `m_GameObject` lines, completed for all four scenes, and C3's stated check narrowed to match** | same shape as items 39 and 41: those lines are `--- !u!114 &…` block headers, not the field that carries the claim. The field sits six lines below each — `ZoneDetection` at `Hard:1339`, `Medium:39205`, `Easy:3311`, `PGS:775`, `OrderManager` at `Hard:2246`, `Medium:54281`, `Easy:5513`, `PGS:1658` — and both identities now resolve by name in every scene (`m_Name: Car` at `Hard:1140`, `Medium:39006`, `Easy:3112`, `PGS:576`; `m_Name: OrderManager` at `Hard:2234`, `Medium:54269`, `Easy:5501`, `PGS:1646`). C3 claimed "every scene's YAML" while citing two scenes for `OrderManager`; the check has now actually been run in all four. §4.2, C3 |

One finding from an earlier review could not be closed here, and it is not a claim in this document:
the same 0.02 s timestep error sits in `<notes>/notes-friction.md:365-369`. Every pass has been
scoped to `docs/DESIGN-OVERVIEW.md` only, so the notes still carry it. Anyone re-deriving from the
notes should take §5.5's table over notes-friction's figures.

---

## 7. Reproduction

`<ref>` = `.agents/tasks/design-overview-2026-10-14/ref/`.

**Half of these commands will not run for you yet.** Only seven files under
`docs/tooling/scene-tools/` are tracked on `origin/main` — `README.md`, `delivery_points.py`,
`find_roads.py`, `order_distances.py`, `place_delivery_points.py`, `scene_points.py` and
`validate_scene.py` (`MEASURED`, `git ls-tree -r --name-only origin/main -- docs/tooling/scene-tools`).
The five scripts that produced most of the per-scene geometry in §2 — `corner_geometry.py`,
`scene_physics.py`, `component_fields.py`, `serialization_drift.py` and `fbx_mesh_bounds.py` — are
**untracked local files**. They are read-only analysis tools and committing them would make every
figure in §2, §3.4 and §5.9 independently checkable; until then those figures rest on this document
and the task notes alone. Same caveat as the shift logs: the evidence is real, and it is not shared.

Two exceptions worth knowing about. The §2.2 and §2.3 road-pivot figures no longer depend on any of
those five: the snippet below resolves the transform chain itself and needs nothing but Python and
the scene file, which is why those buckets can be checked by anyone. And the `<notes>` citations
point at raw tool output that was **not** reproduced for this document. There are five such places,
each flagged at the point of use: the street-module and slab-thickness mesh bounds (§2.1, §2.3, §6
item 11 → `<notes>/notes-map.md:185`, `:189`, `:197`, `:206`), the footbridge mesh bounds (§2.2 →
`:282-283`), the baked prop box sizes and the prop-to-anchor distances (§5.5 →
`<notes>/notes-friction.md:367-371`, `:385-386`), and the per-scene closest pickup/drop-off pairs
(§5.6 → `:233-236`). Those are the only figures in this document whose inputs live solely in the
task notes.

```powershell
# per-scene serialized component values, any scene, any revision
python docs/tooling/scene-tools/component_fields.py "<ref>/Hard.unity" OrderManager ShiftTimer ZoneDetection CargoSystem CargoCondition PlayerProgression ShiftResults ResultsPanel

# what the component census cannot see: resolve each scene's prefab instances, then census the
# prefab itself. This is the check that found ResultsPanel (§Per-scene, §4.2)
Select-String -Path "<ref>/Hard.unity" -Pattern "m_SourcePrefab:"
git show "origin/main:Assets/Project/Prefabs/Canvas (2).prefab" | Select-String "m_IsActive|afcd8a025ccf16d4d85203c80cf90775|m_Enabled"
Select-String -Path "<ref>/Easy.unity" -Pattern "16e1a52f888ac16458f58d73070092a7" -SimpleMatch

# route costing for the tested point set
python docs/tooling/scene-tools/order_distances.py 50
python docs/tooling/scene-tools/order_distances.py 45

# scene geometry: extents, colliders, corridor widths, job costing
python docs/tooling/scene-tools/scene_points.py "<ref>/Hard.unity" "^(City|Floor|Roads_Parent|Car)$"
python docs/tooling/scene-tools/corner_geometry.py "<ref>/Hard.unity" --grid=1.0 --road=^road_ --pitch=8.18
python docs/tooling/scene-tools/scene_physics.py "<ref>/Hard.unity" detail "^(Roads_Parent|Floor|CityProps|City)$"
python docs/tooling/scene-tools/scene_physics.py "<ref>/<scene>.unity" jobs 50
python docs/tooling/scene-tools/fbx_mesh_bounds.py "Assets/Aside/Art/Blender/sehirlogo_01.fbx"

# structural check and stale serialized keys
python docs/tooling/scene-tools/validate_scene.py "<ref>/Hard.unity"
python docs/tooling/scene-tools/serialization_drift.py "Assets/Project/Scenes" "Assets/Project/Scripts/Orders" "Assets/Project/Scripts/Vehicle/Tuning" "<ref>"

# the shift logs (UTF-8 BOM; decode utf-8-sig or the first column reads wrong)
python -c "import io; print(io.open('Game Build/PlaytestLogs/shift_log.csv',encoding='utf-8-sig').read())"
```

```python
# the shipped build's own order tuning values (§3.1, §3.3)
import struct
data = open('Game Build/Zoom Zoom Delivery Co_Data/sharedassets1.assets', 'rb').read()
i = data.find(b'OrderTuning_Balanced')
j = data.find(b'Balanced', i + 20) + 8            # past the profileName string
print(struct.unpack('<f i f f f f', data[j:j+24]))
# -> (12.0, 3, 45.0, 10.0, 0.10000000149011612, 1.0)
#     spawnInterval, maxActiveOrders, orderTimeLimit, baseDeliveryValue, valuePerMetre, latePenaltyPerSecond
```

```python
# the order tuning asset does not parse (§3.3)
import yaml
src = open('Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset', encoding='utf-8-sig').read()
yaml.safe_load(src.split('--- !u!114 &11400000', 1)[1])   # ParserError at "spawnInterval: 12"
```

```python
# road pivot heights and their colliders (§2.2, §2.3) — no tooling dependency, resolves the
# full parent chain so the buckets sum to the object count
import re, collections
txt = open(".agents/tasks/design-overview-2026-10-14/ref/Hard.unity", encoding="utf-8").read()
gos, trs, go2tr, mc = {}, {}, {}, collections.Counter()
def vec(s, keys=("x", "y", "z")):
    return {k: float(re.search(k + r":\s*(-?[\d.eE+-]+)", s).group(1)) for k in keys}
for b in re.split(r"(?m)^--- ", txt):
    m = re.match(r"!u!(\d+) &(\d+)", b)
    if not m: continue
    cid, fid = m.group(1), int(m.group(2))
    if cid == "1":
        gos[fid] = re.search(r"(?m)^  m_Name:\s*(.*)$", b).group(1).strip()
    elif cid in ("4", "64"):
        go = int(re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", b).group(1))
        if cid == "64":
            mc[go] += 1; continue
        trs[fid] = {
            "father": int(re.search(r"m_Father:\s*\{fileID:\s*(\d+)\}", b).group(1)),
            "pos":   vec(re.search(r"m_LocalPosition:\s*\{([^}]*)\}", b).group(1)),
            "scale": vec(re.search(r"m_LocalScale:\s*\{([^}]*)\}", b).group(1)),
            "rot":   vec(re.search(r"m_LocalRotation:\s*\{([^}]*)\}", b).group(1), ("x","y","z","w")),
        }
        go2tr[go] = fid
cross = lambda a, b: [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
def qrot(q, v):
    u, w = [q["x"], q["y"], q["z"]], q["w"]
    t = cross(u, [cross(u, v)[i] + w*v[i] for i in range(3)])
    return [v[i] + 2.0*t[i] for i in range(3)]
def world_y(tr):
    p, f = [trs[tr]["pos"][k] for k in "xyz"], trs[tr]["father"]
    while f in trs:
        a = trs[f]
        p = qrot(a["rot"], [p[i]*a["scale"]["xyz"[i]] for i in range(3)])
        p = [p[i] + a["pos"]["xyz"[i]] for i in range(3)]
        f = a["father"]
    return p[1]
objs, colls = collections.Counter(), collections.Counter()
for fid, nm in gos.items():
    if not nm.startswith("road_"): continue
    y = round(world_y(go2tr[fid]), 2)
    objs[y] += 1; colls[y] += mc.get(fid, 0)
print(sorted(objs.items()), sum(objs.values()), sorted(colls.items()))
# -> 0.4:615  0.6:1  -47.9:17  -83.64:17   total 650;  colliders only at -47.9, 34 of them
```

```python
# expiry cadence model (§3.3) — mirrors OrderManager.Update at 1/60 s
DT = 1/60
def run(limit, cap=3, interval=12.0, horizon=300.0):
    active, next_spawn, expiries, t = [], 0.0, [], 0.0
    while t < horizon:
        for k in range(len(active) - 1, -1, -1):
            active[k] -= DT
            if active[k] <= 0:
                expiries.append(t); active.pop(k)
        if len(active) < cap:                      # cap checked before the interval decrements
            next_spawn -= DT
            if next_spawn <= 0:
                active.append(limit); next_spawn = interval
        t += DT
    return expiries
for limit in (45.0, 50.0):
    e = run(limit)
    print(limit, [sum(1 for x in e if x <= m) for m in (221.4, 268.7, 300.0)])
# -> 45.0 [10, 12, 15]   50.0 [9, 12, 13]   logged late: 10 @221.4 s, 12 @268.7 s
```

### Related documents

- `.agents/tasks/turning-geometry-2026-10-14/report.md` — turning circle against corner geometry
  against zone radius, per scene. Cited throughout §1.4 and §5.3 rather than redone. Note C7: its
  §4.3 claim that `Hard.unity` has no `Roads_Parent` is wrong.
- `docs/QA-PLAYTEST-REPORT-2026-10-14.md` and `docs/playtesting/QA-AI-20261014-01.md` — **superseded**
  on the points where they disagree with code; see C2, C18. Both predate the Easy/Medium/Hard
  restructure and describe `Possible game scene` as it was. Both are tracked on `origin/main` and
  merely absent from this branch's working tree, so the line citations above resolve there.
- `docs/tooling/scene-tools/README.md` — its street-grid tables describe a tree state that
  `origin/main` does not have; see C8.

### A note on the three unstructured testers

The three family-member sessions mentioned in the brief are not data: unstructured, no
questionnaire, no telemetry, no per-tester mapping to any logged row. They appear in this document
only as directions the investigation followed. The camera report sent §5.2; the reverse/overshoot
report sent §1.4 and §5.3, where the chain "reversing implies overshooting implies wide turns" breaks
at the first link, because `ZoneDetection` gives no reason to stop. The two testers who could not
tell where to go are better supported by the logs: **four of the seven recorded shifts delivered two
or fewer orders, and two delivered none** (`MEASURED`). Wall hits were 7, 1, 0, 0, 0, 1, 2 across
those seven rows — so if wide turns were putting players into buildings, this is where it would show,
and it does not.
