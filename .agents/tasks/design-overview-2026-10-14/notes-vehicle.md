# Group 1 — Core vehicle physics & handling

Investigation notes for `docs/DESIGN-OVERVIEW.md`. Read-only: no game code, scene, asset, tuning
value or git state was touched. Unity was not opened.

**Labels.** `READ` = literal value in a file, cited `path:line`. `MEASURED` = recorded by an
instrument run, cited by CSV row/filter. `DERIVED` = arithmetic on READ/MEASURED inputs, inputs
named. `INFERRED` = code control flow, not observed at runtime. `UNKNOWN` = not establishable
without running the game, plus the one check that would settle it. No number appears without a
label and a citation. Nothing is estimated.

**Which tree.** Every file in this section is byte-identical between `HEAD`
(`feature/vehicle-camera` @ `618f2be`) and `origin/main`, so the tree split in the plan §0.2 does
not apply here. Verified:
`git diff --name-status HEAD origin/main -- "Assets/Project/Scripts/Vehicle" "ProjectSettings/TimeManager.asset" "ProjectSettings/DynamicsManager.asset" "VehicleLab_Measurements" "Assets/Project/Prefabs/Car.prefab"`
→ empty output. `git status --short` for the same paths → clean. Scene reads below that concern the
Easy/Medium/Hard restructure use the extracted `origin/main` copies in
`.agents/tasks/design-overview-2026-10-14/ref/`.

**Build sanity.** `python docs/tooling/compile_check.py` → `RUNTIME 342 references, 80 scripts …
errors 0, warnings 0` / `EDITOR 360 references … errors 0, warnings 0` / `Compiles clean.`
(exit 0), unchanged from the plan's baseline.

---

## 1. Handling model: what the mechanism actually is

**Arcade, by construction, and the code says so in its own header.** It is a single `Rigidbody`
held up by four downward raycasts, driven entirely by `ForceMode.Acceleration` forces and torques.
There is no `WheelCollider` anywhere in the project.

| Fact | Label | Source |
| --- | --- | --- |
| "Arcade car on a Rigidbody, held up by four raycasts instead of WheelColliders" | READ | `Assets/Project/Scripts/Vehicle/VehicleController.cs:75-98` |
| No `WheelCollider` type used anywhere; the string appears only in explanatory comments | READ | matches only at `VehicleController.cs:77,79,80,82`, `SurfaceType.cs:45`, `VehicleTuning.cs:87`, all inside `///` blocks |
| `[RequireComponent(typeof(Rigidbody))]`, `[DisallowMultipleComponent]` | READ | `VehicleController.cs:100-101` |
| PhysX contact friction is deliberately *not* part of handling — the car never rubs the floor, so a `PhysicMaterial` on the road changes nothing | READ | `SurfaceType.cs:40-55` |

### The whole force budget, every physics step

Order of work is fixed: read ground → hold car up → gravity → drive/brake → steer → grip → limits
(`VehicleController.cs:94-96`, `:392-428`).

| # | What | Call | Line |
| --- | --- | --- | --- |
| 1 | suspension spring + damper, **at each wheel** | `AddForceAtPosition(up * accel, origin, ForceMode.Acceleration)` | `:718` |
| 2 | gravity (own, not the project's) | `AddForce(Vector3.down * tuning.gravity, ForceMode.Acceleration)` | `:407` |
| 3 | drive / reverse / brake / coast, one signed number along `GroundForward` | `AddForce(GroundForward * totalAccel, ForceMode.Acceleration)` | `:823` |
| 4 | boost, separate push, grounded or not | `AddForce(GroundForward * tuning.boostAcceleration, ForceMode.Acceleration)` | `:878` |
| 5 | steering as a commanded **yaw rate** about the ground normal | `AddTorque(GroundNormal * yawAccel, ForceMode.Acceleration)` | `:1001` |
| 6 | lateral grip, single force at the centre of mass (only when `perAxleGrip` is off and handbrake is up) | `AddForce(GroundRight * …, ForceMode.Acceleration)` | `:1052-1053` |
| 7 | lateral grip, **per axle**, half each (the shipped path) | `AddForceAtPosition(GroundRight * …, axlePosition, ForceMode.Acceleration)` | `:1121-1124` |
| 8 | anti-softlock righting torque | `AddTorque(axis * tuning.rightingTorque, ForceMode.Acceleration)` | `:1163` |
| — | jump: instant velocity change | `AddForce(up * jumpSpeed, ForceMode.VelocityChange)` | `VehicleJumpFlip.cs:208` |
| — | jump hold | `AddForce(up * jumpHoldAcceleration, ForceMode.Acceleration)` | `VehicleJumpFlip.cs:229` |
| — | flip: direct velocity and angular-velocity writes | `_rb.linearVelocity = pushed + hop` / `_rb.angularVelocity += …` | `VehicleJumpFlip.cs:287, :294` |

**Per-wheel slip is measured but does not feed the physics.** Each wheel reports
`lateralSlipSpeed` from `GetPointVelocity(contactPoint)` projected on the wheel's right vector
(`VehicleController.cs:487-507`), and that is published for skid marks and tyre effects only. The
forces that decide cornering are computed per **axle**, not per wheel
(`ApplyAxleGrip`, `:1100-1127`). So: four-wheel raycast suspension, two-axle tyre model, no
friction curve, no load sensitivity. `INFERRED` from that control flow, not observed at runtime:
a single wheel dropping into a gutter changes suspension and the surface blend but does not change
the grip split.

**Load transfer is real and unscripted.** The spring force is applied at the wheel position, so
the body squats and leans from the same forces that hold it up — the code states this is why it is
done that way (`VehicleController.cs:696-719`, force at `:718`).

**Mass does not affect handling.** Every handling force is `ForceMode.Acceleration`, which ignores
mass. The header says so explicitly: "Acceleration mode ignores mass, so changing the mass in the
tuning asset does not secretly change the handling" (`VehicleController.cs:96-98`), repeated at the
field (`VehicleTuning.cs:64-68`). `mass` therefore only changes how the car shoves other
rigidbodies. This is the honest answer to "how heavy does the car feel": the weight is in the
suspension and the centre of mass, not in the kilograms.

---

## 2. Mass, body, suspension and the global physics frame

Values are the **shipped** profile, `Tuning_Balanced.asset`, which is the asset every scene
references (proof in §6).

| Quantity | Value | Label | Source |
| --- | --- | --- | --- |
| mass | 900 kg | READ | `Tuning_Balanced.asset:35` (`mass: 900`); C# default `VehicleTuning.cs:68`; also serialized on the Rigidbody, `Assets/Project/Prefabs/Car.prefab:72`, `Possible game scene.unity:637` |
| linear damping (drag) | **0** | READ | `Tuning_Balanced.asset` `linearDamping: 0`; `VehicleTuning.cs:84` — tooltip: keep at 0 so the car's own coast/top-speed numbers are the only things slowing it |
| angular damping | 2.5 | READ | `Tuning_Balanced.asset` `angularDamping: 2.5`; `VehicleTuning.cs:80` |
| centre-of-mass offset | (0, −0.35, 0) m, local | READ | `Tuning_Balanced.asset` `centreOfMassOffset: {x: 0, y: -0.35, z: 0}`; `VehicleTuning.cs:72` |
| gravity | 20 m/s² down | READ | `Tuning_Balanced.asset` `gravity: 20`; `VehicleTuning.cs:59`; applied `VehicleController.cs:407` |
| `useGravity` | false — project gravity is switched off | READ | `VehicleController.cs:339` |
| **downforce** | **does not exist** | READ | no match for `downforce`/`downForce` in any `.cs` under `Assets`. There is also no anti-roll bar (`antiRoll` → no match) |
| max angular speed | 7 rad/s | READ | `Tuning_Balanced.asset` `maxAngularSpeed: 7`; set `VehicleController.cs:343` |
| max linear velocity | 100 m/s | DERIVED | `max(topSpeed 32, boostTopSpeed 40) × 2.5` per `VehicleController.cs:347-350` |
| interpolation | `Interpolate` | READ | `VehicleController.cs:354` |
| collision detection | `ContinuousDynamic` | READ | `VehicleController.cs:358` |

### Suspension

| Quantity | Value | Label | Source |
| --- | --- | --- | --- |
| rest length (ride height) | 0.60 m | READ | `Tuning_Balanced.asset` `suspensionRestLength: 0.6`; `VehicleTuning.cs:109` |
| extra ray beyond rest | 0.30 m → total ray 0.90 m | READ | `suspensionExtraRayLength: 0.3`; ray built `VehicleController.cs:443` |
| spring stiffness | 110 m/s² of push per metre of compression, **per wheel** | READ | `springStrength: 110`; `VehicleTuning.cs:118`; used `VehicleController.cs:713` (spring can only push: `:708`) |
| spring damper | 14 m/s² per m/s of compression speed | READ | `springDamper: 14`; `VehicleTuning.cs:122`; used `VehicleController.cs:714` |
| half-wheelbase / half-track / wheel radius | 1.4 m / 0.8 m / 0.36 m | READ | `wheelForwardOffset: 1.4`, `wheelSideOffset: 0.8`, `wheelRadius: 0.36` |
| static compression at rest | 0.045 m per wheel | DERIVED | each wheel carries gravity/4 = 5 m/s²; 5 ÷ 110 (`springStrength`) |
| static ride height | 0.555 m | DERIVED | 0.60 − 0.045 |
| suspension damping ratio | ζ ≈ 0.67 (underdamped, settles without pogo) | DERIVED | ω = √110 = 10.49 rad/s; critical = 2ω = 20.98; actual 14 → 14/20.98 |

A wheel hanging *below* rest length still counts as grounded, and the spring produces no force
there — "the car should not lose drive every time it crests a small bump. Predictable beats picky"
(`VehicleController.cs:471-474`, force gate `:708`).

### Physics timestep — the 120 Hz belief is right at runtime and wrong in the project file

| Source | Value | Label |
| --- | --- | --- |
| `ProjectSettings/TimeManager.asset` → `Fixed Timestep: 0.02` | **50 Hz** | READ |
| `ProjectSettings/TimeManager.asset` → `Maximum Allowed Timestep: 0.33333334` | — | READ |
| `Tuning_Balanced.asset` `overrideFixedTimestep: 1`, `physicsHz: 120` | **120 Hz** | READ |
| `Time.fixedDeltaTime = 1f / physicsHz` written from the car's own `ApplyTuningToBody` | 0.0083333 s | READ `VehicleController.cs:360-372` (write at `:365`) |

**Verdict: the runtime rate is 120 Hz, and it is the car that sets it**, not the project settings.
`ApplyTuningToBody` is called from `Awake` and again from every `FixedUpdate`
(`VehicleController.cs:320`, `:394`), so the override lands on the car's first step. Two
consequences, both `INFERRED` from Unity's semantics rather than observed: a scene with no
`VehicleController` in it (e.g. `MainMenu`) runs at the project's 50 Hz, and `Time.fixedDeltaTime`
is a global that stays at 1/120 for the rest of the session once set.

### Project-wide physics frame (for contrast with the overrides above)

All READ from `ProjectSettings/DynamicsManager.asset`: `m_Gravity {0, -9.81, 0}` (overridden by
the car's 20), `m_DefaultSolverIterations: 6`, `m_DefaultSolverVelocityIterations: 1`,
`m_BounceThreshold: 2`, `m_DefaultMaxAngularSpeed: 50` (overridden by the car's 7),
`m_FrictionType: 0`, `m_DefaultContactOffset: 0.01`, `m_SleepThreshold: 0.005`,
`m_EnableEnhancedDeterminism: 0`, `m_SolverType: 0`.

---

## 3. Grip vs drift: the real numbers

### The model

Lateral grip is a **budget in m/s²**, not a friction coefficient. Every step the car works out how
much sideways acceleration would cancel the slide outright and spends up to the budget on it; if a
corner asks for more, the car runs wide by exactly the difference, every time
(`VehicleController.cs:1007-1020`).

Steering commands a **curvature** (1/radius), not an angle. Turn rate falls out as
`curvature × speed` (`VehicleController.cs:968-1002`). With `steerFromGripBudget: 1` the available
curvature is itself derived from the grip budget:

```
curvature = (lateralGripAcceleration × overdrive) / v²        clamped to maximumCurvature
overdrive = lerp(steerOverdriveAtLowSpeed, steerOverdriveAtTopSpeed, clamp01(v / max(topSpeed, boostTopSpeed)))
```
READ: `VehicleTuning.cs:719-741`.

| Field | Balanced value | Label | Source |
| --- | --- | --- | --- |
| `lateralGripAcceleration` | **13 m/s²** | READ | `Tuning_Balanced.asset` (`lateralGripAcceleration: 13`); C# default is 12 at `VehicleTuning.cs:297` |
| `gripBalance` | 0.58 (front-biased → rear lets go first) | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:397` |
| `perAxleGrip` | true — the two-force model ships | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:390` |
| `useFrictionCircle` / `frictionCircleStrength` / `longitudinalGripReference` | true / 0.8 / 16 m/s² | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:337,344,349` |
| `steerFromGripBudget` | true — the authored curvature curve is bypassed | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:365` |
| `steerOverdriveAtLowSpeed` / `AtTopSpeed` | 0.6 / 1.15 | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:371,377` |
| `maximumCurvature` | 0.35 (1/m) → 2.86 m tightest radius | READ / DERIVED | `Tuning_Balanced.asset`; radius = 1/0.35 |
| `maxYawAcceleration` | 16 rad/s² | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:286` |
| `driftYawAuthority` | 0.55 — steering keeps 55 % authority mid-drift so counter-steer works | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:413`; applied `VehicleController.cs:991-993` |
| drift test | grounded **and** speed ≥ 4 m/s **and** slip angle ≥ 12° | READ | `VehicleController.cs:681-683`; thresholds `driftMinimumSpeed: 4`, `driftSlipAngleThreshold: 12` |

### Axle grip, worked through (Balanced, tarmac, surface multiplier 1.0)

Front and rear budgets are `total × share × 2`, each axle contributing half of what it spends, so
at `gripBalance 0.5` the pair reproduces the old single force exactly
(`VehicleController.cs:1064-1098`, half-contribution at `:1121-1126`). The friction circle is
applied to the **rear only**, because the rear is the driven axle — that asymmetry *is* power
oversteer (`VehicleController.cs:1078-1082`).

| State | front grip | rear grip | front : rear | Label |
| --- | --- | --- | --- | --- |
| base split, before the circle | 15.08 | 10.92 | 1.38 : 1 | DERIVED from 13 × 0.58 × 2 and 13 × 0.42 × 2 |
| coasting (`LongitudinalDemand` = `coastDeceleration` 5) | 15.08 | 10.48 | 1.44 : 1 | DERIVED via `LateralGripAfterLongitudinal` (`VehicleTuning.cs:755-766`) with used01 = 5/16 |
| full throttle from low speed (demand 22, clamped to the 16 reference) | 15.08 | **2.18** | 6.9 : 1 | DERIVED, used01 = 1 → `lerp(1, 0, 0.8)` = 0.2 |
| handbrake held, no throttle (demand = coast 5 + handbrake 8 = 13) | 15.08 | **1.09** | 13.8 : 1 | DERIVED: circle gives 7.28, then × `handbrakeRearGripMultiplier` 0.15 |

### What the handbrake actually does — three effects, not one

1. **Cuts rear grip directly**: `rearGrip *= handbrakeRearGripMultiplier` = **0.15**
   (`VehicleController.cs:1087`; value in `Tuning_Balanced.asset`).
2. **Drags**: adds `handbrakeDeceleration` = **8 m/s²** × surface brake multiplier to the same
   `stoppingAccel` as the footbrake (`VehicleController.cs:788-790`). The tooltip explains the
   intent: a handbrake locks the rear only, so roughly a third of four-wheel braking power, and
   without the drag it was "a grip switch … the tyres icing over" (`VehicleTuning.cs:254-264`).
3. **Eats rear grip a second time, through the circle**: the drag is floored into
   `LongitudinalDemand` with `Mathf.Max(|totalAccel|, handbrakeDrag)` so that throttle-plus-handbrake
   — the standard drift entry — cannot report near-zero tyre work (`VehicleController.cs:802-819`).

Also: with `perAxleGrip` already on, pulling the handbrake does **not** switch models; the model
switch at `VehicleController.cs:1033` only matters if `perAxleGrip` were off
(`INFERRED` from that branch).

### Turn radius the car is allowed, by speed (Balanced)

All DERIVED from `MaxCurvatureAt`/`GripLimitedRadiusAt` (`VehicleTuning.cs:719-786`) with grip 13,
overdrive 0.6→1.15 over 0→40 m/s:

| speed | overdrive | steering radius | grip-limited radius | limit | predicted radius |
| --- | --- | --- | --- | --- | --- |
| 5 m/s | 0.669 | 2.88 m | 1.92 m | steering | 2.88 m |
| 9.84 m/s | 0.735 | 10.13 m | 7.45 m | steering | 10.13 m |
| 15 m/s | 0.806 | 21.5 m | 17.3 m | steering | 21.5 m |
| 20 m/s | 0.875 | 35.2 m | 30.8 m | steering | 35.2 m |
| 25 m/s | 0.944 | 50.9 m | 48.1 m | steering | 50.9 m |
| 32 m/s | 1.040 | 75.7 m | **78.8 m** | **grip** | 78.8 m |
| 40 m/s | 1.150 | 107.0 m | **123.1 m** | **grip** | 123.1 m |

Crossover at **29.1 m/s** (DERIVED: overdrive = 1 when speed01 = (1−0.6)/0.55 = 0.727, × 40).
Above that, full lock asks for more grip than the tyres have and the car understeers or steps out.
Below it the car grips. That is the designed "demanding but predictable" line, and the 9.84 m/s row
matches the harness's own prediction exactly (its note records "grip could hold 7.4 m but the
steering curve only asks for 10.1 m").

### Drive, brake, coast

| Field | Balanced | Label | Source |
| --- | --- | --- | --- |
| `topSpeed` | 32 m/s (115 km/h) | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:132` |
| acceleration source | formula, not the authored curve (`accelerationFromFormula: 1`) | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:140`; `AccelerationAt` `:690-706` |
| `launchAcceleration` / `accelerationFalloffPower` | 22 m/s² / 1.7 | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:144,150` |
| `brakeDeceleration` | 22 m/s² | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:247` |
| `coastDeceleration` | 5 m/s² | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:252`; added whenever throttle is 0, `VehicleController.cs:771` |
| `reverseTopSpeed` / `reverseAcceleration` | 8 m/s / 9 m/s² | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:160,163` |
| `inputDeadzone` | 0.08 | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:168` |
| effective braking with throttle released | **27 m/s²** (brake 22 + coast 5) | DERIVED | `VehicleController.cs:771` and `:776` both add into one `stoppingAccel` |
| reverse behaviour | throttle against motion brakes first, reverses after; braking is capped so it can never drag the car backwards | READ | `VehicleController.cs:734-766` |

---

## 4. Surfaces and weather: every profile's real numbers

`SurfaceType` is a component on a collider, found through the wheel raycast's own hit and cached
per collider — nulls cached too, so an unlabelled floor is queried once
(`VehicleController.cs:541-556`, cache declared `:298-299`). Lookup is
`GetComponentInParent`, so labelling a parent covers its children. An **unlabelled** collider
returns plain tarmac on purpose, so untagged ground drives exactly as the car did before surfaces
existed (`SurfaceType.cs:113-131`).

`SurfaceKind` = `Tarmac, Wet, Dirt, Grass, Ice, Metal, Concrete` (`SurfaceType.cs:10-21`;
`Concrete` is appended last with a comment warning never to insert, because scenes store these as
integers).

### `SurfaceType.DefaultsFor` — all seven, as written

| kind | grip | drive | brake | rolling resistance | mark strength | particles | spray when rolling | line |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Tarmac (`Default`) | 1.00 | 1.00 | 1.00 | 0 | 1.00 | 1.0 | no | `SurfaceType.cs:119-128` |
| Wet | 0.60 | 0.85 | 0.65 | 0 | 0.50 | 1.4 | yes | `:161-174` |
| Dirt | 0.55 | 0.75 | 0.70 | 2.5 | 0.85 | 2.2 | yes | `:176-189` |
| Grass | 0.45 | 0.60 | 0.60 | 5.0 | 0.35 | 1.8 | yes | `:191-204` |
| Ice | 0.18 | 0.45 | 0.25 | 0 | 0.15 | 0.6 | no | `:206-219` |
| Metal | 0.80 | 0.95 | 0.85 | 0 | 0.40 | 1.2 | no | `:221-234` |
| Concrete | 0.92 | 0.97 | 0.93 | 0 | 0.80 | 0.9 | no | `:236-258` |

All READ. `gripMultiplier` multiplies `lateralGripAcceleration`, `driveMultiplier` multiplies
forward acceleration, `brakeMultiplier` multiplies brake **and** handbrake deceleration, and
`rollingResistance` is an absolute extra m/s² added to `stoppingAccel` even under power
(`VehicleController.cs:750`, `:776`, `:790`, `:797`).

Concrete at 0.92 is a deliberate step *down* from tarmac, so cutting across a forecourt loosens the
car slightly rather than rewarding leaving the road (READ, `SurfaceType.cs:237-242`).

### Blending across four wheels

Grip, drive, brake, rolling resistance, mark strength, particle amount and both colours are
**averaged over the grounded wheels only** (`VehicleController.cs:566-621`). The discrete fields
(`kind`, `sprayWhenRolling`) cannot be averaged, so the wheel with the highest `particleAmount`
wins. Averaging is explicitly chosen over "whichever surface most wheels are on" so a boundary is
a transition the player can drive through rather than a snap (docstring `:558-565`). Airborne, the
blend is held at the last surface so a landing still reads as landing on that ground (`:601`).

### `ApplyWeather`, applied once to the blend, never per wheel

`WeatherKind` = `Dry, Rain, Snow, Ice` (`SurfaceType.cs:29-34`). Applied at
`VehicleController.cs:621` as the last step of the blend, once, to the blend — never per wheel.

| weather | grip | drive | brake | rolling resistance | other | line |
| --- | --- | --- | --- | --- | --- | --- |
| **Dry** | ×1 | ×1 | ×1 | +0 | returns the profile completely unchanged | `SurfaceType.cs:313-314` |
| Rain | ×0.65, or ×0.85 on Grass/Dirt | ×0.88, or ×0.90 loose | ×0.70, or ×0.90 loose | +0 | mark ×0.5, particles ×1.3, spray forced on | `:272-286` |
| Snow | `min(grip, 1) × 0.45` | ×0.60 | ×0.50 | **+2.0** | mark/particles overwritten, spray on | `:288-301` |
| Ice | `min(grip, 0.18)` | `min(drive, 0.45)` | `min(brake, 0.25)` | +0 | mark ×0.2, particles ×0.6, spray off | `:303-312` |

**Dry pass-through: CONFIRMED.** `ApplyWeather`'s `default:` branch is `return ground;` with no
mutation (`SurfaceType.cs:313-314`), and the controller comments that with no `WeatherState` in the
scene the line is a no-op so every lab measurement still describes the shipped car
(`VehicleController.cs:617-621`).

### Is weather wired? Yes, present, and set to Dry everywhere

Discriminating check: count the `WeatherState` script GUID `7a3e5c1d9f2b4a6e8c0d1e2f3a4b5c6d` per
scene and read the serialized `weather:` value. `WeatherState.Current` is a static that returns
`Dry` when no instance exists (`WeatherState.cs:34`), and the car reads it every physics step
through the blend.

| scene | `WeatherState` | serialized value | `SurfaceType` tags |
| --- | --- | --- | --- |
| `<ref>/Hard.unity` | 1 | `weather: 0` = **Dry** (line 1740) | **84** |
| `<ref>/Medium.unity` | 1 | `weather: 0` = Dry (line 45211) | **0** |
| `<ref>/Easy.unity` | 1 | `weather: 0` = Dry (line 3877) | **0** |
| `Possible game scene.unity` (the logged build) | 0 | n/a → `Dry` by default | **0** |
| `Kyuri's Scene.unity` (local, = `Hard` on `origin/main`) | 1 | `weather: 0` = Dry (line 1414) | 84 |
| `VehicleLab.unity` | 0 | n/a → Dry | 5 |
| `MainMenu.unity` | 0 | n/a | 0 |

All READ. Verdict: **weather is wired but inert.** Rain/Snow/Ice are reachable only by changing one
inspector value; nothing in the game changes it, and no scene ships anything but Dry.

### Which surfaces exist in the city

`Hard.unity`'s 84 tags, by `kind` integer, with their serialized multipliers:

| kind | count | serialized grip / drive / brake / roll | matches `DefaultsFor`? |
| --- | --- | --- | --- |
| 0 = Tarmac | 2 | 1 / 1 / 1 / 0 | yes |
| 3 = Grass | 22 | 0.45 / 0.6 / 0.6 / 5 | yes |
| 6 = Concrete | 60 | 0.92 / 0.97 / 0.93 / 0 | yes |

MEASURED by a repeatable count over the scene YAML (84 total, matching commit `c03c220` "tag 84
city surfaces"); the per-instance values are READ. **Wet, Dirt, Ice and Metal are tagged nowhere**,
so four of the seven profiles are currently unreachable in play. Medium, Easy and
`Possible game scene` have no tags at all, which means every surface in them is the untagged
tarmac default — including anything the player drives off the road onto.

---

## 5. Boost, jump and flip

### Boost — earned, not regenerating. Confirmed.

| Field | Balanced | Label | Source |
| --- | --- | --- | --- |
| `boostEnabled` | true | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:186` |
| `boostCapacity` (tank) | **100 boost-units** | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:199` |
| `boostConsumptionRate` | **33.3 units/s** while held | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:202`; spent `VehicleController.cs:881` |
| `boostRechargeRate` | **0** — no free refill, on all three profiles | READ | all of `Tuning_{Balanced,Grippy,Loose}.asset`; `VehicleTuning.cs:210`; the recharge branch is gated on `> 0f` at `VehicleController.cs:885` so it never runs |
| `boostRechargeDelay` | 1 s (only gates the dead recharge path) | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:214` |
| `boostEarnedFromDrift` | **20 units/s** | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:223`; applied `VehicleController.cs:915` |
| `boostEarnedFromAir` | **10 units/s** | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:234`; applied `VehicleController.cs:917-920` |
| earn rate during a flip | **0** | READ | `VehicleController.cs:919-920` — "a recovery move that refuels the car is a loop, not a recovery" (`:905-908`) |
| `boostAcceleration` | 16 m/s², on top of drive, grounded **or airborne** | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:195`; applied `VehicleController.cs:874-878` |
| `boostTopSpeed` | 40 m/s | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:191` |
| `supersonicThreshold` | 38 m/s | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:239`; set `VehicleController.cs:901` |
| tank starts full | yes, in `Awake` | READ | `VehicleController.cs:322-325` |
| full tank duration | **3.00 s** | DERIVED | 100 ÷ 33.3 |
| seconds of drifting to fill an empty tank | 5.0 s | DERIVED | 100 ÷ 20 |
| seconds of air time to fill an empty tank | 10.0 s | DERIVED | 100 ÷ 10 |
| supersonic reachable on throttle alone? | no — 38 > `topSpeed` 32 | DERIVED | the two READ values |

**The exact condition that counts as "drifting" for earning** is the controller's own `IsDrifting`,
no second opinion: `IsGrounded && Speed >= driftMinimumSpeed (4 m/s) && SlipAngle >=
driftSlipAngleThreshold (12°)`, where `SlipAngle` is `|atan2(LateralSpeed, |ForwardSpeed|)|` in
degrees, forced to 0 below 0.5 m/s so reversing cannot read as a 180° slide
(`VehicleController.cs:677-683`). The handbrake is **a way to cause** that state, not the state
itself — nothing in the earn path reads `Drive.handbrake` (`EarnBoost`, `:910-932`).

Air earning requires **all four** wheels off the ground (`WheelsOnGround == 0`,
`VehicleController.cs:917`) and no flip in progress. The field tooltip notes that a short kerb hop
often pays nothing because the rays still reach ground at both ends of the arc
(`VehicleTuning.cs:226-234`).

Earning runs while boosting (both are evaluated every step, `VehicleController.cs:899`), and
earning into a full tank still lights the gauge while adding nothing to `TotalBoostEarned`
(`:921-931`). Boost is deliberately excluded from `LongitudinalDemand`, so it never spends grip —
which makes it the tool for *holding* a slide rather than starting one
(`VehicleController.cs:808-813`).

Player-facing feedback exists: `BoostGauge` colours the bar and writes `BOOST +DRIFT`, `BOOST +AIR`,
`BOOST SUPERSONIC` or `BOOST EMPTY drift or jump to earn` (READ,
`Assets/Project/Scripts/Vehicle/BoostGauge.cs:99-128`), on a canvas with `sortingOrder = 10`
(`BoostGauge.cs:144`). The whole gauge hides itself when `boostEnabled` is false (`:85-88`).

### Jump — grounded only

| Field | Balanced | Label | Source |
| --- | --- | --- | --- |
| `jumpSpeed` | 6.5 m/s, applied as `VelocityChange` so mass cannot change the height | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:430`; `VehicleJumpFlip.cs:208` |
| `jumpHoldTime` | 0.18 s | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:434`; set `VehicleJumpFlip.cs:210` |
| `jumpHoldAcceleration` | 18 m/s² (against gravity 20, so it nearly cancels it) | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:437`; `VehicleJumpFlip.cs:229` |
| `jumpCooldown` | 0.35 s | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:440`; set `VehicleJumpFlip.cs:211` |
| ground check | `!_controller.IsGrounded` → refused | READ | `VehicleJumpFlip.cs:196-199` |
| jump direction | the **ground normal**, not world up, so a ramp launches along the ramp | READ | `VehicleJumpFlip.cs:204` |
| `inputBufferTime` | 0.12 s — a press just before touchdown still fires on landing | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:446`; aged `VehicleJumpFlip.cs:167-180` |
| height from the impulse alone | 1.06 m | DERIVED | `PredictedJumpHeight()` = 6.5² / (2 × 20), `VehicleTuning.cs:816-820` |
| height with the hold held full | ≈ 2.1 m | DERIVED | 0.18 s at net −2 m/s² then free flight; ignores any suspension contribution at launch, so treat as an upper bound |

### Flip — the recovery move, grounded only, speed-governed

| Field | Balanced | Label | Source |
| --- | --- | --- | --- |
| `flipRequiresGrounded` | **true** — no air dash | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:455`; enforced `VehicleJumpFlip.cs:251-255` |
| `flipForce` | 9 m/s of instant velocity change (not a Newton force, so mass-independent) | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:460`; `VehicleJumpFlip.cs:262` |
| `flipHopSpeed` | 2.5 m/s along the ground normal, added **after** the speed cap | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:464`; `VehicleJumpFlip.cs:285-287` |
| `flipSpin` | 2.5 rad/s about `cross(groundNormal, direction)` | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:469`; `VehicleJumpFlip.cs:291-294` |
| `flipDuration` | 0.45 s counted as "flipping" (also blocks re-trigger and boost earning) | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:473`; `VehicleJumpFlip.cs:297`, `:67` |
| `flipCooldown` | **1.6 s** | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:477`; `VehicleJumpFlip.cs:298` |
| `flipMaxSpeedGain` | 3 m/s hard cap | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:481` |
| `flipNoSpeedGainAboveSpeed` | 12 m/s — above this a flip adds no speed at all | READ | `Tuning_Balanced.asset`; `VehicleTuning.cs:486` |
| governor | `allowedGain = clamp(12 − speedBefore, 0, 3)`; over the cap the **direction is kept and the extra speed thrown away** | READ | `VehicleJumpFlip.cs:268-282` |
| direction | stick-relative, resolved against the car's own `GroundForward`/`GroundRight`; no stick → flip forwards | READ | `VehicleJumpFlip.cs:321-337` |

Bindings (READ, `VehicleInput.cs:218-234`): handbrake `Space` / gamepad LB, boost `Left Alt` /
RB, jump `Left Shift` / A, flip `Q` + WASD / X + stick.

---

## 6. The three tuning profiles, side by side

**Which one is live: `Tuning_Balanced`, in every scene.** Discriminating check — the tuning GUID on
each `VehicleController`, matched against each `.asset.meta`:

| asset | GUID (from `.meta`) | referenced by |
| --- | --- | --- |
| `Tuning_Balanced` | `1a2b3c4d5e6f708192a3b4c5d6e7f811` | `Car.prefab:103`, `Possible game scene.unity:627`, `Kyuri's Scene.unity:861`, `VehicleLab.unity:4438`, `Aside/ScenesZA/GreyBox.unity:11270`, and on `origin/main` `<ref>/Hard.unity:1187`, `<ref>/Medium.unity:39053`, `<ref>/Easy.unity:3159`, `<ref>/Possible game scene.unity:623` |
| `Tuning_Grippy` | `1a2b3c4d5e6f708192a3b4c5d6e7f812` | **nothing** — zero matches in any `.unity` or `.prefab` |
| `Tuning_Loose` | `1a2b3c4d5e6f708192a3b4c5d6e7f813` | **nothing** |

All READ. `Tuning_Balanced` also carries `isChosenSetup: 1`; the other two carry `0`
(documentation only, nothing reads it — `VehicleTuning.cs:40-41`).

### Every field that differs between the three

Identical across all three (so not listed below): `physicsHz` 120, `overrideFixedTimestep` 1,
`gravity` 20, `maxAngularSpeed` 7, `linearDamping` 0, `wheelForwardOffset` 1.4, `wheelSideOffset`
0.8, `wheelHeightOffset` 0, `suspensionRestLength` 0.6, `suspensionExtraRayLength` 0.3, `groundMask`
all layers, `topSpeed` 32, `reverseTopSpeed` 8, `inputDeadzone` 0.08, `accelerationFromFormula` 1,
`launchAcceleration` 22, `accelerationFalloffPower` 1.7, the whole boost block (`boostEnabled` 1,
`boostTopSpeed` 40, `boostAcceleration` 16, `boostCapacity` 100, `boostConsumptionRate` 33.3,
`boostRechargeRate` 0, `boostRechargeDelay` 1, `boostEarnedFromDrift` 20, `boostEarnedFromAir` 10,
`supersonicThreshold` 38), `useFrictionCircle` 1, `frictionCircleStrength` 0.8,
`longitudinalGripReference` 16, `steerFromGripBudget` 1, `steerOverdriveAtLowSpeed` 0.6,
`steerOverdriveAtTopSpeed` 1.15, `maximumCurvature` 0.35, `perAxleGrip` 1, `gripBalance` 0.58,
`driftYawAuthority` 0.55, `driftSlipAngleThreshold` 12, `driftMinimumSpeed` 4, `jumpSpeed` 6.5,
`jumpHoldAcceleration` 18, `jumpCooldown` 0.35, `inputBufferTime` 0.12, `flipRequiresGrounded` 1,
the whole stuck-recovery block (`autoRightWhenStuck` 1, 0.35 / 1.2 / 1.5 / 8),
`lockWheelsOnHandbrake` 1, `cameraSwivelSpeed` 120, `cameraSwivelRecentreDelay` 1.2, the four
`cameraBoost*` fields (1.5 / 9 / 8 / 0.12), `cameraCollisionRadius` 0.4 and
`cameraCollisionRecoverTime` 0.35.

| field | Balanced (live) | Grippy | Loose |
| --- | --- | --- | --- |
| `profileName` | Balanced | Grippy | Loose |
| `isChosenSetup` | **1** | 0 | 0 |
| `mass` | 900 | 900 | **950** |
| `centreOfMassOffset` | (0, −0.35, 0) | (0, **−0.4**, 0) | (0, **−0.3**, **−0.15**) |
| `angularDamping` | 2.5 | 3 | 2 |
| `wheelRadius` / `wheelWidth` | 0.36 / 0.28 | 0.34 / 0.32 | 0.38 / 0.26 |
| `springStrength` / `springDamper` | 110 / 14 | **130 / 17** | **95 / 12** |
| derived damping ratio ζ | 0.67 | 0.75 | 0.62 |
| **`lateralGripAcceleration`** | **13** | **17** | **10** |
| `handbrakeRearGripMultiplier` | 0.15 | 0.22 | **0.10** |
| `handbrakeGripMultiplier` (dead, see §8) | 0.25 | 0.35 | 0.18 |
| `handbrakeDeceleration` | 8 | 9.5 | 6 |
| `brakeDeceleration` | 22 | **26** | **18** |
| `coastDeceleration` | 5 | 5.5 | 4 |
| derived effective braking (brake + coast) | 27 | 31.5 | 22 |
| derived stopping distance from 32 m/s | 19.0 m | 16.3 m | 23.3 m |
| `reverseAcceleration` | 9 | 10 | 8 |
| `maxYawAcceleration` | 16 | **22** | **13** |
| `steerInputSmoothTime` | 0.07 | 0.05 | 0.09 |
| `maxVisualSteerAngle` | 32 | 28 | 38 |
| `steerVisualExaggeration` | 1.6 | 1.3 | **2.0** |
| `airborneWheelSpinDecay` | 0.6 | 0.6 | 0.4 |
| `jumpHoldTime` | 0.18 | 0.18 | 0.2 |
| `flipForce` / `flipHopSpeed` / `flipSpin` | 9 / 2.5 / 2.5 | 8 / 2.5 / 2.2 | 10 / 2.8 / 2.8 |
| `flipDuration` / `flipCooldown` | 0.45 / 1.6 | 0.45 / 1.6 | 0.5 / 1.5 |
| `flipMaxSpeedGain` / `flipNoSpeedGainAboveSpeed` | 3 / 12 | 2.5 / 11 | 3 / 12 |
| `accelerationBySpeed` curve peak (dormant) | 18 | 20 | 16 |
| `maxCurvatureBySpeed` curve (dormant) | 0.35→0.018 | **0.40→0.028** | 0.35→0.018 (same as Balanced) |
| `cameraDistance` / `cameraHeight` | 4 / 1.6 | 4.2 / 1.7 | 3.8 / 1.5 |
| `cameraFollowSmoothTime` | 0.12 | 0.09 | 0.15 |
| `cameraLookAhead` | 28 | 28 | **34** |
| `cameraLookAtHeight` / `cameraExtraPitch` | 1.4 / 4 | 1.3 / 4 | 1.5 / 5 |
| `cameraFieldOfView` | 100 | 98 | 102 |
| `cameraExtraDistanceAtTopSpeed` / `…HeightAtTopSpeed` | 2.5 / 0.8 | 2 / 0.6 | 3 / 1 |
| `cameraVelocityInfluence` | 0.25 | 0.15 | 0.4 |
| `cameraRotationSmoothTime` | 0.14 | 0.1 | 0.18 |
| body / trim / glass / tyre / rim colours | yellow set | blue set | red set |

All profile values READ from `Assets/Project/Scripts/Vehicle/Tuning/Tuning_{Balanced,Grippy,Loose}.asset`;
ζ and stopping distances DERIVED as shown earlier.

### Grippy and Loose are completely unmeasured — confirmed

`VehicleLab_Measurements/vehicle_measurements.csv`: 640 data rows + header (641 lines), columns
`timestamp,profile,test,reading,value,unit,notes`. Grouping by `profile` gives **`Balanced` 640,
nothing else**. MEASURED by `Import-Csv … | Group-Object profile`.

So every number in the lab describes the shipped car, and the two rejected profiles have **no**
recorded stopping distance, turning circle, boost, flip or wall-recovery figure. Their
`whyThisSetup` claims are design prose, not readings — and two of them disagree with their own
asset (§7).

---

## 7. The measured figures, verified against the claims

Method: the CSV is append-only history across many capture sessions, so group by
`(profile, test, reading)` and take the **latest timestamp**. That yields **43** distinct
readings, all `Balanced`, the newest session being `2026-10-07 13:56:xx`
(MEASURED, `Import-Csv … | Group-Object {profile|test|reading} | … Sort-Object timestamp | Select -Last 1`).

**One caveat that changes how three of these should be quoted:** the `Profile` test (F7) does not
drive the car. It echoes tuning fields straight into the CSV —
`Report("Profile", "Top speed", Tuning.topSpeed, …)` at `VehicleMeasurement.cs:981-987`. So
`Profile / Top speed = 32` is a READ of the asset that happens to live in a CSV, not a MEASURED
result. The driven tests are Stopping distance, Turning circle, Wall recovery, Flip distance, Boost
and Camera road ahead.

| Claim to verify | Verdict | Value | Label | Source |
| --- | --- | --- | --- | --- |
| top speed 32 m/s | **not measured as such** | 32.0 is the asset value echoed by F7 (latest `2026-10-07 13:56:19`). The nearest driven evidence: entry speed **31.3653 m/s** reached in **3.75 s** on full throttle, recorded as "98 % of the 32.0 m/s top speed" | READ (echo) + MEASURED (31.37 m/s) | `Tuning_Balanced.asset`; CSV `Profile/Top speed`; CSV `Stopping distance/Entry speed` + `Time to reach test speed`, `2026-10-07 13:56:24` |
| stopping distance 18.1 m from 31.4 m/s | **confirmed** | **18.0948 m** from **31.3653 m/s** in 1.1583 s | MEASURED | CSV `Stopping distance`, `2026-10-07 13:56:24` |
| turning circle ≈ 22.1 m | **confirmed** | width **22.0967 m**, radius **11.0484 m**, at **9.8399 m/s**, lap 7.05 s, limiting factor **0 = steering** (not grip) | MEASURED | CSV `Turning circle`, `2026-10-07 13:56:32` |
| boost peak 40.1 m/s | **confirmed** | **40.1278 m/s** (ceiling is 40; the push stops at the ceiling and momentum carries the last 0.13) | MEASURED | CSV `Boost/Peak speed on boost`, `2026-10-07 13:56:45` |
| full-boost duration 3.01 s | **confirmed** | **3.0083 s**, against 100 ÷ 33.3 = 3.00 s | MEASURED + DERIVED | CSV `Boost/Full tank duration`, `2026-10-07 13:56:45` |

Other readings from the same session worth carrying into the document, all MEASURED:

- **Boost**: time to cross throttle top speed **1.1083 s**, to the ceiling **1.6 s**, distance on
  one tank **94.0301 m** from standstill, stopping distance from peak **36.5963 m**.
- **Boost earning**: a 2.5 s handbrake slide at 10 m/s counted as drifting for **1.875 s** and paid
  **37.5** units. That is exactly 1.875 × 20, so the 20 units/s drift rate is confirmed by
  measurement and not only by the asset (DERIVED cross-check).
- **Flip**: displacement **4.4439 m** over **0.4583 s** against **4.6895 m** if the player had
  simply driven — `Flip is not a shortcut = 1 (PASS)`. Speed went 10.2316 → 12.0 m/s, a gain of
  **1.7684 m/s** against the 3 m/s cap, with the note "speed governor cut the gain, direction kept".
- **Wall recovery**: 19.9 m/s into a wall keeps **8.4018 m/s** through the impact; back-off 0.975 s,
  turn-and-rebuild 0.7833 s, **total 1.7583 s**.
- **Camera road ahead**: at 31.2167 m/s the furthest visible ground is **42.7612 m** and the nearest
  is **−8.579 m** (behind the car); both fairness checks PASS.

### Why the measured stop beats the textbook figure — resolved, not hand-waved

The harness's own note reads "textbook value 22.36 m, difference −19.1 %". The textbook figure uses
`brakeDeceleration` alone (`predicted = v² / (2 × brakeDeceleration)`,
`VehicleMeasurement.cs:347`). But the brake phase drives `throttle = 0, brake = 1`
(`VehicleMeasurement.cs:341`), and with zero throttle the controller **also** adds
`coastDeceleration` into the same `stoppingAccel` (`VehicleController.cs:771` then `:776`). Real
deceleration is therefore 22 + 5 = **27 m/s²**, giving 31.3653² / 54 = **18.22 m** — within 0.7 % of
the 18.0948 m measured (DERIVED). Every "textbook" stopping number in the project is ~23 % long for
the same reason, including `PredictedStoppingDistanceFromTopSpeed()` (`VehicleTuning.cs:795-800`)
and the boost figure of 36.5963 m, whose true value from 40.13 m/s is **29.8 m** (DERIVED).

Independent corroboration that 27 m/s² is the right number: `CargoCondition.cs:60` defaults its
hard-brake threshold to exactly `27f`.

---

## 8. Contradictions

Each entry names both sources and says which one the runtime uses.

1. **Physics rate: 50 Hz vs 120 Hz.** `ProjectSettings/TimeManager.asset` → `Fixed Timestep: 0.02`
   (50 Hz). `Tuning_Balanced.asset` → `overrideFixedTimestep: 1`, `physicsHz: 120`, written into
   `Time.fixedDeltaTime` at `VehicleController.cs:365`.
   **Runtime uses 120 Hz** once a `VehicleController` has run a step; scenes with no car (MainMenu)
   run the project's 50 Hz. Label: READ both sides.

2. **Gravity: 9.81 vs 20.** `DynamicsManager.asset` → `m_Gravity y: -9.81`. The car sets
   `useGravity = false` (`VehicleController.cs:339`) and applies `tuning.gravity` = 20
   (`:407`). **Runtime uses 20 m/s²** — more than double real gravity, on purpose
   (`VehicleTuning.cs:57-59`).

3. **Max angular speed: 50 vs 7.** `DynamicsManager.asset` → `m_DefaultMaxAngularSpeed: 50`;
   `VehicleController.cs:343` overwrites it with `tuning.maxAngularSpeed` = 7. **Runtime uses 7.**

4. **Grip 12 vs 13.** `VehicleTuning.cs:297` declares `lateralGripAcceleration = 12f`;
   `Tuning_Balanced.asset` serializes `13`. Unity's YAML overlay wins, so **runtime uses 13**. The
   same 12 is still quoted in two comment blocks that no longer match the shipped car:
   `SurfaceType.cs:150-152` ("chosen against the default tuning grip of 12 m/s^2") and
   `VehicleTuning.cs:841-851` (the curvature-curve table "sit just inside the default grip budget
   of 12"). Documentation defect, code is right.

5. **Top speed 25 vs 32 in the measurement history.** The nine oldest CSV rows
   (`2026-09-09 14:25:15` → `20:52:29`) echo `Top speed 25.0` and `Sideways grip 12.0`; all 103
   later `Profile` rows (`2026-09-10 13:27:46` → `2026-10-07 13:56:19`) echo `32.0` and `13.0`.
   **Latest wins: 32 / 13**, which agrees with the asset and with
   `docs/tooling/scene-tools/delivery_points.py:46-47` (`TOP_SPEED = 32.0`, `SIDEWAYS_GRIP = 13.0`).
   The stale pair survives only in a stale tooltip: `VehicleTuning.cs:132` still reads "Top speed on
   full throttle, m/s. 25 m/s is about 90 km/h" next to a default of `32f`.

6. **Balanced's own stopping-distance claim.** `Tuning_Balanced.asset` `whyThisSetup`: "Stopping
   distance is 23.3 m from 32 m/s". Code + measurement give **18.09 m from 31.37 m/s** and 19.0 m
   from a true 32 m/s, because the asset text uses the brake-only formula and the car also coasts.
   **The measurement is right; the asset prose is 23 % pessimistic.** (This one errs safe — the
   camera shows more road than needed, not less.)

7. **Grippy's prose vs Grippy's numbers.** `whyThisSetup` says "Grip 18"; the asset serializes
   `lateralGripAcceleration: 17`. Nothing reads the prose, so this is a documentation defect only —
   but it is in the same file as the number it contradicts.

8. **Loose's prose vs Loose's numbers.** `whyThisSetup` says "Grip 8" (asset: `10`) and "the 17.4 m
   stopping distance" (DERIVED from the asset: 23.3 m with brake 18 + coast 4 from 32 m/s; 17.4 m is
   what 25 m/s and brake-only would have given, so the sentence predates the top-speed change).

9. **"The rear is given 84 per cent of the grip the front has"** (`Tuning_Balanced.asset`
   `whyThisSetup`). The arithmetic at `VehicleController.cs:1075-1076` gives front 15.08 and rear
   10.92 m/s², so rear is **72.4 %** of front. 84 % is the rear's share of the *single-force
   budget* (0.42 × 2). The split is real and front-biased; the sentence states it against the wrong
   denominator.

10. **README's boost description vs the code's earn condition.** README: "a handbrake slide pays
    into it at 20 a second". Code: the earn test is `IsDrifting`
    (grounded, ≥ 4 m/s, slip ≥ 12°) and reads nothing about the handbrake
    (`VehicleController.cs:910-920`). The handbrake is one way to reach that state; a fast
    throttle-induced slide pays identically. Rates, tank, 3 s and 32 → 40 in the README are all
    confirmed against the asset. Code wins on the condition.

11. **Stale serialized key in `Possible game scene`** — see §9, item 1. It is a scene/script
    mismatch, so the runtime silently uses the C# initialisers.

---

## 9. Stale serialized fields and dormant data

**The three vehicle tuning assets are clean.** Checked by extracting every top-level YAML key from
each asset and every `public` field declared in `VehicleTuning.cs`: **108 keys, 108 fields, zero
mismatches in either direction, in all three assets.** Nothing serialized is unread by the class,
and nothing declared is missing from an asset. (MEASURED by the key-vs-field set comparison.)

That corrects the premise this investigation was given: `hardBrakeDecelThreshold` is **not** in any
vehicle asset. What is actually stale:

1. **`hardBrakeDecelThreshold: 18` in `Assets/Project/Scenes/Possible game scene.unity:808`**, on
   the `ZoomZoom.Orders.CargoCondition` component. The current script declares
   `fallbackHardBrakeDecelThreshold = 27f` (`CargoCondition.cs:60`) and `hardBrakeMultiplier`, and
   that scene has neither key. `Kyuri's Scene.unity:1042-1043` and `<ref>/Hard.unity:1368-1369`
   both carry the current pair (`hardBrakeMultiplier: 1.25`, `fallbackHardBrakeDecelThreshold: 27`).
   Consequence, INFERRED from Unity's deserialisation order: in `Possible game scene` the old key is
   dropped and the C# initialisers run, so that scene uses threshold 27 and multiplier 1.25 while
   the YAML still says 18. **This matters because `Possible game scene` is the only scene in the
   telemetry.** It belongs to Group 3/5's area; recorded here because it was named in this brief.

2. **`handbrakeGripMultiplier` — a live field with no reader.** Declared at
   `VehicleTuning.cs:302` and serialized in all three assets (0.25 / 0.35 / 0.18), and the only
   match for the identifier anywhere in `Assets/**/*.cs` is its own declaration. The handbrake's
   real grip dial is `handbrakeRearGripMultiplier` (`VehicleController.cs:1087`). Changing
   `handbrakeGripMultiplier` in the inspector does nothing. It reads as the pre-`perAxleGrip`
   four-wheel version of the same idea, left behind when the rear-only model landed.

3. **`accelerationBySpeed` — serialized, authored, dormant.** Five keyframes in each asset, read
   only when `accelerationFromFormula` is false (`VehicleTuning.cs:690-706`), and it is `1` in all
   three. The shipped acceleration comes from
   `launchAcceleration × (1 − speed01^accelerationFalloffPower)`.

4. **`maxCurvatureBySpeed` — serialized, authored, dormant.** Six keyframes in each asset, read only
   when `steerFromGripBudget` is false (`VehicleTuning.cs:719-741`), and it is `1` in all three.
   Its own docstring (`VehicleTuning.cs:841-851`) describes a 12 m/s² grip budget that no longer
   ships. Both curves are kept deliberately as A/B switches, but anyone reading the asset in the
   inspector sees authored curves that are not driving the car.

5. **`boostRechargeRate` / `boostRechargeDelay` — intentionally dead, not stale.** `0` on every
   profile, and the recharge branch is gated on `> 0f` (`VehicleController.cs:885`). Kept so a
   playtest can compare "earned" against "regenerating" by changing one field
   (`VehicleTuning.cs:205-210`). Worth distinguishing from item 2: this one is documented as a
   switch, that one is a leftover.

6. **`isChosenSetup` — documentation only**, nothing reads it at runtime (`VehicleTuning.cs:40-41`).

---

## 10. UNKNOWN, with the single check that would settle each

1. **Throttle-only top speed as a driven number.** There is no top-speed test in the harness — F1
   accelerates only to `brakeTestSpeedFraction` of the asset value (`= 0.98f`,
   `VehicleMeasurement.cs:71`, used `:314`, giving the 31.37 m/s entry speed). Whether the car
   actually reaches 32.0 m/s, and in what distance, is unmeasured. *Check:* add a reading that holds
   full throttle until `ForwardSpeed` stops rising and log the asymptote; or re-run F6 with
   `brakeTestSpeedFraction` at 1.0 and read `Entry speed`.

2. **Turning circle at any speed other than 9.84 m/s.** The F2 reading is a single speed
   (`turnTestSpeed = 10f`, `VehicleMeasurement.cs:79`). The radius table in §3 above is DERIVED
   arithmetic, not measured, and the car becomes grip-limited above 29.1 m/s where no reading
   exists. *Check:* run F2 at 20 and 30 m/s and compare `Limiting factor`.

3. **Turning circle vs corner width vs delivery-zone radius.** `.agents/tasks/turning-geometry-2026-10-14/report.md`
   **does not exist** (checked: the only task directory is `design-overview-2026-10-14`), so there is
   nothing to cite for the geometry half of this question. The vehicle half is settled: 22.0967 m
   measured circle width at 9.84 m/s, steering-limited. *Check:*
   `python docs/tooling/scene-tools/corner_geometry.py "<scene>.unity"` — the script exists and is
   read-only, and its own header says it was written for exactly this question (clearance radius per
   anchor and widest-bottleneck route vs the tuning radius at a range of speeds). It is currently
   **untracked** (`git status` → `?? docs/tooling/scene-tools/corner_geometry.py`), so its output is
   not yet group evidence. The comparison is Group 2's to run.

4. **Any handling figure for Grippy or Loose.** Zero rows in the CSV. *Check:* assign the asset to
   the lab car and press F6.

5. **Whether the measured figures describe the scenes anyone plays.** The lab readings come from
   `VehicleLab.unity` (5 tagged surfaces, no `WeatherState`). `Hard.unity` has 84 tagged surfaces
   including 22 Grass patches at 0.45 grip and 5 m/s² rolling resistance; Medium, Easy and
   `Possible game scene` have none. *Check:* run F1/F2 in each shipped scene, on road and on grass,
   and compare against the lab numbers.

6. **Effect of the 2.0439× `City` root scale on anything vehicle-side.** Not examined here; the
   suspension ray length (0.9 m) and wheel offsets are in car-local space and unaffected, but
   whether kerb heights and ramp angles in the scaled city are within the suspension's travel is a
   scene question. *Check:* Group 2's `scene_points.py` world-space geometry against
   `suspensionRestLength + suspensionExtraRayLength`.

7. **Supersonic in practice.** `IsSupersonic` needs 38 m/s, which only boost can reach, and
   `boost_activations` is 0 in every logged shift (Group 5's reading of `shift_log.csv`). So no
   logged player has ever seen the state. *Check:* a logged shift with a non-zero
   `boost_activations`.
