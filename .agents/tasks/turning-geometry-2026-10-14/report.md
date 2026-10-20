# Turning circle against corner geometry against zone radius

Read-only investigation, 14 October 2026. Nothing in the project was changed. Two new read-only
scripts were added under `docs/tooling/scene-tools/`; no scene, prefab, script, asset or tuning
value was touched, and no prop or building size is proposed for change anywhere in this report.

---

## 1. Summary answer

**The testers are right that something makes them reverse, and wrong about the cause. The turning
circle is not the problem in the level they played, and stopping inside the zone is not required by
the code at all. Three other things are real, and two of them are not geometry.**

In order of how much evidence sits behind them:

1. **The delivery zone has no stop requirement.** `ZoneDetection` fires on 3D distance alone, every
   0.1 s, with no speed gate anywhere in the file. Driving *through* a zone at 32 m/s delivers the
   order exactly as parking in the middle of it does. So "overshoot the zone, reverse back into it"
   cannot be the mechanism: there is nothing to overshoot. This is measured from code, not inferred.

2. **The chase camera has no reverse case.** `ChaseCamera.ComputeHeading` blends the nose with the
   velocity direction, and `ComputeDesiredRotation` aims the camera at a point `cameraLookAhead`
   metres in **front** of the car. On Balanced that is 28 m. Reversing therefore puts the player's
   view 28 m the wrong way, and the heading blend is mathematically ill-conditioned because nose and
   velocity are antiparallel (`Vector3.Slerp` between opposite vectors has no defined axis). Reverse
   in this game is not badly tuned. It is undisplayed. "Make reverse better" is a missing feature,
   not a tuning number.

3. **The car cannot hold its tightest corner with a foot on either pedal.** The friction circle
   saturates at `longitudinalGripReference` 16 m/s². Coasting already spends 5 of that
   (`coastDeceleration`), so **any brake input at or above half travel, and full throttle anywhere
   below 14.9 m/s, uses the whole longitudinal budget** and cuts the rear axle's cornering grip to
   20% of its share. Effective lateral grip falls from 12.8 to 8.6 m/s², which widens the achievable corner
   by 11% at 10 m/s and 32% at 20 m/s, and because the loss is rear-only the car *rotates* rather
   than pushing wide. Braking into a drop-off makes the back step out. That is a legible, calculable
   path to "the turns are too wide" and then to reversing, and it has nothing to do with the
   turning circle.

4. **"22.1 m" is a U-turn at 9.8 m/s, not a corner figure, and the geometry it has to fit is fine in
   the scene that was played and bad in the scene that is replacing it.** In
   `Possible game scene` the tightest route bottleneck to any delivery anchor is a **25.5 m** free
   corridor, which the car can corner at up to 11.8 m/s. In `Hard.unity` the road module is
   **8.18 m**, which caps a right-angle turn at **5.8 m/s**, and in `Easy.unity` one anchor
   (`Pickup 03 North East Corner`) has only **8.0 m** of free width — tighter than the 12 m zone
   that is supposed to sit on it. A fix sized for the greybox is wrong for Hard by a factor of
   three.

The teammate's chain of reasoning — reversing implies overshooting implies wide turns — breaks at
the first link, because the code gives no reason to stop. The two testers who could not tell where
to go are the better lead, and the shift log agrees with them: two of the four recorded sessions
delivered **zero** orders and ran 10 and 12 late.

---

## 2. What is measured, what is read, what is not knowable from here

| Claim | Status |
| --- | --- |
| Turning radius and circle at any speed, all three profiles | **Computed** from `VehicleTuning.MaxCurvatureAt` and the three `.asset` files. Cross-checks to the lab reading at 9.84 m/s to within 0.1 m. |
| Turning circle 22.1 m at 9.84 m/s | **Read** from `vehicle_measurements.csv`. Correct. One speed only. |
| Stopping distance 18.09 m from 31.37 m/s | **Read** from the CSV. Correct, and fully explained by brake 22 + coast 5 = 27 m/s². |
| Zone radius 6 m | **Measured** in all five scenes. Correct everywhere. |
| Car mass 900 kg | **Read** from `Tuning_Balanced.asset`. Correct (Loose is 950). |
| Street widths 8.8–25.5 m | **Re-derived** and reproduced exactly by `find_roads.py`. But see §4.2: those kerbs are 0.24 m tall and the car drives over them, so they are not the constraint. |
| Free corridor width at every delivery anchor in `Possible game scene` and `Easy` | **Measured** from collider geometry plus real mesh bounds. |
| Road module width in `Hard` (8.18 m) and `Medium` (13.81 m) | **Measured** two independent ways each. |
| Building footprints in `Hard` and `Medium` | **Not measured.** 1295 and 397 MeshColliders drawn from many separate asset-pack models. Only the carriageway was measured there. |
| Whether players actually reversed, how often, for how long | **Unknowable from here.** `PlaytestLogger` has no reverse column. See §7. |
| Whether the rear really does step out under braking in play | **Unknowable without running the game.** The mechanism is in the code and the numbers are above; the feel is not measurable statically. |
| Grippy and Loose handling | **Never measured.** All 787 CSV rows across both files are `Balanced`. |

---

## 3. Evidence: the car

### 3.1 The turning circle is strongly speed-dependent already

`VehicleTuning.steerFromGripBudget` is `1` on all three profiles, so the authored
`maxCurvatureBySpeed` curve is dead code at runtime. `VehicleTuning.MaxCurvatureAt` computes:

```
curvature = min( (lateralGripAcceleration * overdrive(v)) / v² ,  maximumCurvature )
overdrive(v) = lerp(steerOverdriveAtLowSpeed, steerOverdriveAtTopSpeed, v / BoostOrTopSpeed())
```

On Balanced: grip 13, overdrive 0.6 → 1.15 scaled on 40 m/s, `maximumCurvature` 0.35. The achievable
radius is the wider of that and the grip-limited `v²/grip`:

| speed (m/s) | steering radius | grip radius | achievable radius | circle |
| --- | --- | --- | --- | --- |
| 1–4 | 2.86 (cap) | 0.08–1.23 | **2.86** | **5.71** |
| 5 | 2.88 | 1.92 | 2.88 | 5.75 |
| 6 | 4.06 | 2.77 | 4.06 | 8.11 |
| 8 | 6.93 | 4.92 | 6.93 | 13.87 |
| **9.84** | **10.13** | **7.45** | **10.13** | **20.26** |
| 10 | 10.43 | 7.69 | 10.43 | 20.86 |
| 12 | 14.48 | 11.08 | 14.48 | 28.96 |
| 15 | 21.47 | 17.31 | 21.47 | 42.93 |
| 20 | 35.16 | 30.77 | 35.16 | 70.33 |
| 25 | 50.94 | 48.08 | 50.94 | 101.88 |
| 32 | 75.74 | 78.77 | 78.77 | 157.54 |
| 40 | 107.02 | 123.08 | 123.08 | 246.15 |

Three things follow.

**The 22.1 m figure is correct and is being read wrongly.** The CSV records
`Turning circle width 22.0967 m` at a held speed of `9.8399 m/s`, with
`Turning radius 11.0484 m` and `Limiting factor 0` (steering, not grip). The formula predicts
10.13 m radius, i.e. a 20.26 m circle, and the CSV's own note says exactly that: *textbook value
20.3 m*. The extra 1.8 m is the entry transient — `MeasureTurningCircle` records the widest span of
the **first** full lap, and `maxYawAcceleration` 16 rad/s² plus `steerInputSmoothTime` 0.07 s mean
the car spirals in rather than snapping onto the circle. So the steady-state circle at 10 m/s is
20.3 m and the measured first-lap sweep is 22.1 m. Both numbers are right; the 22.1 m is the one to
quote for "room needed to U-turn at 10 m/s", and 11.05 m is the one to quote for a junction.

**The car turns far tighter below 10 m/s than the headline figure suggests.** At 5 m/s and below the
`maximumCurvature` cap binds and the circle is 5.71 m — a quarter of the quoted 22.1 m. An arrival
manoeuvre is a low-speed manoeuvre, so the quoted figure has been scaring the design away from
corners the car can actually take.

**It was measured at one speed, ever.** `turnTestSpeed` is `10` in `VehicleLab.unity`,
`Possible game scene.unity` and `Kyuri's Scene.unity`. The CSV holds 15 turning-circle rows, which
are three identical runs of five readings each, all at 9.8399 m/s, all Balanced. There is no
low-speed and no cruising-speed reading. The table above is arithmetic from the shipped asset, not
a measurement, and the one point where the two can be compared agrees to 0.1 m.

### 3.2 The three profiles differ only in grip, and two are unmeasured

Identical across all three: `steerFromGripBudget 1`, `steerOverdriveAtLowSpeed 0.6`,
`steerOverdriveAtTopSpeed 1.15`, `maximumCurvature 0.35`, `topSpeed 32`, `boostTopSpeed 40`,
`reverseTopSpeed 8`, `gripBalance 0.58`, `useFrictionCircle 1`, `frictionCircleStrength 0.8`,
`longitudinalGripReference 16`.

| | grip | radius at 10 m/s | circle at 10 m/s | brake | reverse accel | camera look-ahead |
| --- | --- | --- | --- | --- | --- | --- |
| Balanced (chosen) | 13 | 10.43 m | 20.9 m | 22 | 9 | 28 m |
| Grippy | 17 | 7.98 m | 16.0 m | 26 | 10 | 28 m |
| Loose | 10 | 13.56 m | 27.1 m | 18 | 8 | 34 m |

Grippy already turns 24% tighter than Balanced and has never been driven in the lab. If a tighter
turning circle is wanted, a profile that provides it exists and is untested.

### 3.3 Stopping distance, including the low-speed numbers nobody recorded

`MeasureStoppingDistance` drives `throttle = 0, brake = 1`, and in `ApplyDriveAndBrake` zero throttle
adds `coastDeceleration` on top of `brakeDeceleration`. The real deceleration is **27 m/s²**, not 22.

Recorded: `Braking distance 18.0948 m` from `Entry speed 31.3653 m/s`, labelled
*textbook value 22.36 m, difference −19.1%*. That −19.1% is not the car beating physics; it is
`VehicleTuning.PredictedStoppingDistanceFromTopSpeed()` dividing by `brakeDeceleration` alone and
ignoring coast. At 27 m/s² the prediction is 18.22 m against 18.09 m measured — a difference of
0.13 m, which is less than one physics step at that speed. **The recorded number is right and its
own error bar is wrong.** Worth a one-line fix in the helper some day; it is not a handling problem.

Computed from 27 m/s², with the brake-only figure for comparison:

| speed | stop distance (brake + coast, 27) | brake only (22) |
| --- | --- | --- |
| 6 m/s | 0.67 m | 0.82 m |
| 8 | 1.19 | 1.45 |
| 10 | 1.85 | 2.27 |
| 12 | 2.67 | 3.27 |
| 15 | 4.17 | 5.11 |
| 20 | 7.41 | 9.09 |
| 25 | 11.57 | 14.20 |
| 31.37 | **18.22 (18.09 measured)** | 22.37 |
| 32 | 18.96 | 23.27 |
| 40 (boost ceiling) | 29.63 | 36.36 |

### 3.4 The friction circle is the mechanism that actually widens the turns

`ApplyLateralGripPerAxle` splits the budget (`gripBalance` 0.58) and applies the friction circle to
the **rear axle only**, on purpose — the comment says so, because the rear is the driven axle:

```
frontGrip = 13 * 0.58 * 2 = 15.08        (never reduced)
rearGrip  = 13 * 0.42 * 2 = 10.92        (passed through LateralGripAfterLongitudinal)
AvailableLateralGrip = (frontGrip + rearGrip) / 2
```

`LongitudinalDemand` is the net accel along the car, floored at the handbrake drag. The circle
reference is 16 m/s², and coasting alone already spends 5:

| what the driver is doing | demand | rear grip | effective lateral grip |
| --- | --- | --- | --- |
| coasting | 5 | 10.48 | **12.78** |
| 25% brake | 10.5 | 8.78 | 11.93 |
| **half brake or more** | **≥16** | **2.18** | **8.63** |
| full brake | 27 | 2.18 | 8.63 |
| full throttle at 10 m/s | 18.95 | 2.18 | 8.63 |
| full throttle at 15 m/s | 15.93 | 2.99 | 9.03 |
| full throttle at 25 m/s | 7.54 | 9.89 | 12.48 |

Holding the tightest available corner at 10 m/s needs `v²/R = 100/10.43 = 9.59 m/s²` of lateral
grip. Off the pedals the car has 12.78 and holds the line. On the brake or on full throttle it has
8.63 and cannot: the grip-limited radius becomes 11.59 m instead of 10.43 m, **11% wider**. Taking
the braking case, where the grip stays at 8.63 across the whole speed range, the gap grows to
**21% at 15 m/s** (26.07 m against 21.47 m) and **32% at 20 m/s** (46.35 m against 35.16 m). The
throttle case fades out instead: full throttle stops saturating the circle above 14.9 m/s and is
costing almost nothing by 25 m/s.

Two consequences, and the second is the one that produces reversing:

- The corner the steering promises is only available with both feet off. This is a deliberate design
  choice, documented at length in `VehicleTuning`'s Combined Slip section, and it is working.
- Because the loss is entirely at the rear, braking into a turn does not push the nose wide, it
  swings the tail. Past 12° of slip (`driftSlipAngleThreshold`) above 4 m/s the car is flagged
  `IsDrifting` and `driftYawAuthority` drops steering authority to 55%, so the steering stops being
  able to pull it straight. From the driving seat that is "I braked for the drop-off and slid past
  it sideways", and the recovery from facing the wrong way is reverse.

**I cannot confirm this happens in play without running the game.** The parameters, the code path and
the arithmetic are all above; whether players hit it depends on whether they brake into arrivals,
which nothing in the telemetry records.

### 3.5 Reverse: what the code actually does

`ApplyDriveAndBrake`, throttle < 0:

```
if (ForwardSpeed > 0.5f)                               -> brake at brakeDeceleration * -throttle
else if (|ForwardSpeed| < reverseTopSpeed)             -> drive at reverseAcceleration * throttle
```

Balanced: `reverseAcceleration` 9 m/s², `reverseTopSpeed` 8 m/s, against `launchAcceleration` 22 and
`topSpeed` 32. So reverse is 41% of forward acceleration and 25% of forward top speed. It reaches its
ceiling in 0.89 s over 3.56 m, which is not slow for the job.

Steering in reverse is handled, and handled well: `ApplySteering` computes
`targetYawRate = curvature * ForwardSpeed`, and `ForwardSpeed` goes negative, so the turn direction
flips the way a real car does. At 8 m/s reverse the radius is 6.93 m; near standstill it is the
2.86 m cap. There is no bug here.

What is missing is everything around it:

- **No camera support.** `ChaseCamera` never tests the sign of `ForwardSpeed`. The camera stays
  behind the nose and aims 28 m ahead of it, and `ComputeHeading` runs `Vector3.Slerp(nose,
  velocity.normalized, 0.25)` on two antiparallel vectors once reverse speed passes the 1.5 m/s
  gate — a degenerate call with no defined rotation axis. Reversing is done blind.
- **No dedicated reverse control.** `VehicleInput` puts forward and reverse on one `Throttle` axis;
  `S` brakes then reverses. That is a reasonable arcade choice, but combined with the camera it means
  the player's only way to look where they are reversing is the mouse swivel — which is the other
  thing the testers complained about.
- **The non-reversing brake is undocumented.** `VehicleInput` states the design intent plainly:
  *"Left Ctrl brakes and never reverses. For stopping ON a drop-off point."* The build's Controls
  screen does not mention it — that is `QA-008` in `docs/QA-PLAYTEST-REPORT-2026-10-14.md`, logged
  the same morning as the test sessions. A tester who wants to stop on a point and only knows about
  `S` will reverse every time they brake to a standstill, because `S` held past zero *is* reverse.

So: **"make reverse better" is a missing feature, not a tuning change.** The numbers are fine. The
camera and the control documentation are not.

---

## 4. Evidence: the level, per scene

### 4.1 Which scene the testers played

`Game Build/PlaytestLogs/shift_log.csv` records four sessions on 2026-10-14 between 09:09 and 09:38,
every one of them with `scene = Possible game scene`. The executable is dated 08:31 the same
morning. The working tree is on `feature/vehicle-camera`, where that scene has the greybox district
under a root called `Group 15 Greybox real one`; `git status` shows the scene file unmodified, so
the built scene is the committed one. **That is the geometry analysed below.**

Worth knowing before anyone rebuilds: on `origin/main` the *same* scene file has 40 transforms, no
greybox and no buildings at all — just the `Floor` slab, the car and the twelve anchors. A build
from main would be a completely open plain, and nothing measured here would describe it.

`Hard`, `Medium` and `Easy` exist only on `origin/main`; they are not in this working tree. They
were read out of git for this analysis.

The shift log is also the only quantitative thing the sessions produced, and it is worth reading:

| time | play s | delivered | late | wall hits | handbrake s | boost | flips | longest recovery |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 09:09 | 163.5 | 4 | 4 | 1 | 0.0 | 0 | 0 | 1.4 s |
| 09:17 | 300.0 | 6 | 10 | 0 | 0.0 | 0 | 0 | 0.0 s |
| 09:29 | 268.7 | **0** | 12 | 0 | 0.0 | 0 | 0 | 0.0 s |
| 09:38 | 221.4 | **0** | 10 | 0 | 0.0 | 0 | 0 | 0.0 s |

Near-zero wall hits across all four sessions. If wide turns were putting players into buildings, this
is where it would show, and it does not. Two sessions delivered nothing at all. That is a
find-the-place problem, not a drive-the-car problem. (Three testers, four rows; the session was
unstructured and there is no per-tester mapping, so these rows cannot be attributed to individuals
and should not be presented as a controlled result.)

### 4.2 `Possible game scene`: the kerbs are not walls, and the corners are not tight

Collider inventory: **2 BoxColliders** (`Floor`, 504 × 555 m with its top face at y = 0; and the
car's own 1.8 × 1.0 × 4.2 m box) and **90 MeshColliders**, all referencing one model,
`Assets/Aside/Art/Blender/Group 18 Greybox draft 1.fbx`.

Mesh bounds are not in the scene file, so `fbx_mesh_bounds.py` reads them from the FBX and
`corner_geometry.py` calibrates the model-to-world convention rather than assuming it: of six
candidate axis-order and unit combinations, `xyz` with a 0.01 unit factor puts the bottom of a
typical object **0.09 m** from the floor, and the next best candidate is off by 3.35 m. The same
calibration run against `Easy.unity` independently lands on the same convention at 0.07 m. As a
third check, the box colliders this pipeline reports in `Hard.unity` match the developer's own
`KnockablePropBaker` log from Unity: mobile toilet 2.49 × 3.90 × 2.61 against the log's
"2.5x3.9x2.6", trash container 2.74 × 2.57 × 3.31 against "2.7x2.6x3.3", picnic table
9.21 × 1.98 × 9.21 against "9.2x2.0x9.2".

**The street widths are real but irrelevant.** `find_roads.py` reproduces the previously cited figure
exactly — seven north-south and seven east-west centrelines, kerb-to-kerb spacing from **8.8 m** to
**25.5 m**. But that is pivot-to-pivot between `SideWalk` objects, and those objects are
**0.24 m tall strips** (e.g. `SideWalk001.005` spans y −0.05 to 0.19). The car's body collider sits
at y 0.50–1.50 and its wheels are raycasts with 0.9 m of reach. **A 0.19 m kerb cannot be hit; the
car drives over it.** The only things in this scene the car can collide with are the 28 building
blocks that stand above the sill, 8 to 85 m tall, covering 9.3% of the 504 × 555 m slab.

So the honest corner measurement is free space between buildings. For each anchor: the clearance
radius is the largest circle of free ground centred on it; the bottleneck is the tightest half-width
along the roomiest route in from the car's start, found by max-min Dijkstra on the distance field.

| anchor | world (x, z) | clearance | free corridor | route bottleneck | fastest 90° turn |
| --- | --- | --- | --- | --- | --- |
| **DropOff 04 Centre North** | −6.79, 252.71 | **12.73 m** | **25.5 m** | **12.73 m** | **11.8 m/s** |
| DropOff 03 West Midpoint | −220.11, 231.92 | 17.00 | 34.0 | 17.00 | 14.0 m/s |
| DropOff 05 East Midpoint | 83.65, 231.92 | 36.31 | 72.6 | 24.00 | 17.2 m/s |
| Pickup 04 West Quarter | −104.06, 252.71 | 30.90 | 61.8 | 24.00 | 17.2 m/s |
| DropOff 02 North West Corner | −125.04, 344.79 | 24.90 | 49.8 | 24.90 | 17.6 m/s |
| Pickup 03 North East Corner | 83.65, 344.79 | 40.66 | 81.3 | 29.00 | 19.3 m/s |
| Pickup 06 North Centre | −24.02, 344.79 | 31.56 | 63.1 | 29.00 | 19.3 m/s |
| Pickup 01 South West Gate | −220.11, 7.03 | 32.00 | 64.0 | 32.00 | 20.5 m/s |
| Pickup 02 Centre Cross | −6.79, 140.33 | 49.49 | 99.0 | 35.97 | 22.0 m/s |
| DropOff 06 South West Yard | −104.06, 7.03 | 38.80 | 77.6 | 38.80 | 23.0 m/s |
| Pickup 05 East Approach | 83.65, 120.14 | 38.94 | 77.9 | 38.94 | 23.1 m/s |
| DropOff 01 South East Gate | 83.65, 7.03 | 47.67 | 95.3 | 47.67 | 26.2 m/s |

The "fastest 90° turn" column comes from the geometry of a right-angle turn between two corridors of
width W, taken as wide as the corridor allows. With `a = W/2 − 0.8` (half-width less the car's
`wheelSideOffset`), the arc clears the inside corner of the junction only if

```
R ≤ (4 − 2√2) · a  ≈  1.1716 · (W/2 − 0.8)
```

Inverting that against the radius table in §3.1 gives the speed. The model treats the car as a point
with 0.8 m of clearance; a real 2.8 m wheelbase sweeps a band about 0.25 m wider than the body in a
tight turn, so these speeds are optimistic by roughly that much of corridor width. It also assumes
the driver uses the whole corridor, which a player aiming at a beacon will not.

**The verdict for the scene the testers played: there is no corner in it that the turning circle
cannot take.** The single tightest point on the best route to any anchor is a 25.5 m corridor at
`DropOff 04 Centre North`, and the car's circle is 20.9 m across at 10 m/s. The next tightest is
34 m at `DropOff 03 West Midpoint`, good for 14.0 m/s, and the remaining ten anchors are clear at
17.2 m/s or better. The minimum circle the car can draw, 5.71 m, needs only a 6.48 m corridor to
turn a right angle, and this scene's narrowest route offers four times that.

The order-route model is also consistent: `order_distances.py` and `delivery_points.py` record the
36 pairs costed at a 20 m/s working average, longest route 954 m at 48 s. Nothing in the corner
geometry of this scene contradicts a 20 m/s average. (Note `QA-009`: the built executable logged a
45 s order limit while `OrderTuning_Balanced.asset` says 50, so the build the testers played was
3 s short on the worst pair. That is a build-provenance issue, already filed, and it affects the
"ran out of time" complaint rather than the turning one.)

### 4.3 `Hard.unity`: the carriageway is 8.18 m and that does cap cornering at 5.8 m/s

The `City` root is at **(−6.3905, 0.4, 139.24)** with uniform scale **2.0439076**, exactly as the
brief states — verified, and applied to every coordinate below. 1986 transforms, 35 BoxColliders (the
baked knockable props), 1295 MeshColliders.

There is no `Roads_Parent` object in this scene. There *is* a `Floor` BoxCollider,
579.4 × 601.85 m with its top at y = 0, under the whole city, and the road tiles sit on top of it at
y ≈ 0.4. So the car is not confined to the road; it can drop off the 0.4 m edge onto the slab and
drive between the buildings. The road measurement below is therefore the width of the **carriageway
the player is presumably meant to follow**, not a hard wall.

**Measured road module: 8.18 m.** 582 `road_base` tiles, all at uniform world scale 204.391
(= local 100 × City 2.0439). Clustering their world positions gives 66 distinct lines in x and 65 in
z, with adjacent-line spacing of **8.18 m (×34) and 8.17 m (×26)** in x and **8.18 m (×29) and
8.17 m (×23)** in z. A tiled road abuts, so the pitch is the tile size.

Rasterising all 650 `road_*` tiles (base, junction, turn, intersection, end) as 8.18 m squares at
0.5 m resolution gives 47 737 m² of carriageway. The widest point anywhere on the network is 9.21 m
half-width, i.e. **18.4 m**, which is the junctions. Most of the network is one module wide.

| anchor | world (x, z) | on a road tile? | free width at the anchor | tightest point on the road route in |
| --- | --- | --- | --- | --- |
| DropOff 04 Centre North | 2.10, 145.90 | yes | 9.00 m | 9.00 m |
| DropOff 05 East Midpoint | 83.65, 234.56 | yes | 8.00 m | 8.00 m |
| Pickup 01 South West Gate | 0.00, 137.00 | yes | 5.00 m | 5.00 m |
| Pickup 04 West Quarter | −103.00, 252.71 | yes | 8.00 m | not reachable by road |
| DropOff 06 South West Yard | −22.70, 11.50 | yes | 4.00 m | not reachable by road |
| DropOff 01 South East Gate | 83.65, 5.23 | **no** | — | — |
| DropOff 02 North West Corner | −93.60, 270.40 | **no** | — | — |
| DropOff 03 West Midpoint | −245.40, 139.80 | **no** | — | — |
| Pickup 02 Centre Cross | 121.30, 150.60 | **no** | — | — |
| Pickup 03 North East Corner | 254.10, 151.00 | **no** | — | — |
| Pickup 05 East Approach | 118.00, 120.14 | **no** | — | — |
| Pickup 06 North Centre | −3.40, −111.50 | **no** | — | — |

Two separate problems, both worse than the turning circle:

**Cornering speeds.** On an 8.18 m street `R ≤ 1.1716 × (4.09 − 0.8) = 3.85 m`, which the Balanced
car can only produce below **5.84 m/s**. At the widest junction (18.4 m) it is **9.68 m/s**. On the
5 m stretches `R_max` is 1.99 m, which is **below the 2.86 m the curvature cap allows**, so a
right-angle turn there is impossible at *any* speed — the car has to use the slab beside the road.
Against the 20 m/s working average the order timer is costed on, this is a three-to-four-fold
mismatch.

**Half the delivery anchors are not on the road.** Six of twelve do not sit on a road tile at all,
and several still carry greybox coordinates — `Pickup 04 West Quarter` at z = 252.71 and
`Pickup 05 East Approach` at z = 120.14 are the exact values `delivery_points.py` derived for the
old greybox pavement. `delivery_points.py` says in its own docstring that the set must be re-derived
after any re-export, and the city has been re-exported since. For a player following the road, those
beacons are in the middle of a block.

**What is not measured here:** building footprints. The 1295 MeshColliders come from many separate
asset-pack models, and I did not extract bounds for all of them. So I cannot give a building-to-
building clearance figure for Hard the way I can for the greybox, and the per-anchor clearance from
obstacles in Hard is unknown.

### 4.4 `Medium.unity`: a different pack, 13.81 m two-lane roads

Root container is `'City '` — note the trailing space in the name — at (−1, 0, 85) with scale
**1.2333**, so neither the container name nor the scale matches Hard's. 1982 transforms,
2 BoxColliders, 397 MeshColliders. No `road_base` objects at all: this scene uses Toon City Pack
pieces (26 `two-lane-linear-road-piece`, 7 `two-lane-curved-road-piece`, 4 `two-lane-T-junction`,
2 `two-lane-road-roundabout`) plus 1236 `wooden-fence` objects.

**Measured road width: 13.81 m.** Two independent derivations agree.
`two-lane-linear-road-piece.fbx` has a mesh 11.2048 units square; at the scene's world scale of
1.233 that is 13.81 m. The most common tile spacing measured from the scene positions is **13.82 m**.

The curved piece is 22.402 units square → 27.62 m, so its centreline turn radius is about 13.8 m.
Two different numbers follow, depending on how the player takes the corner. Cutting a right angle
across a 13.81 m carriageway allows R ≤ 7.15 m, which caps the turn at **8.1 m/s**. Following the
curved piece's own 13.8 m arc works up to **11.7 m/s**, where the car's radius reaches 13.8 m. Both
are better than Hard and worse than the greybox.

### 4.5 `Easy.unity`: mostly roomy, with one anchor tighter than its own zone

Same greybox model as `Possible game scene`, moved: root `Group 15 Greybox real one 1` at
(−98, 0, 145.073), scale 8.97659. No `Floor` BoxCollider — the ground is a single mesh,
`Bastrop-blocks-combos.001 (1)`, carrying negative scales on all three axes, so there is no slab to
bound the analysis and no flat-floor fallback. There is a `Border` root with 4 children, and a
`Weather` root (as in Medium and Hard; `Possible game scene` has none).

| anchor | clearance | free corridor | route bottleneck | fastest 90° turn |
| --- | --- | --- | --- | --- |
| **Pickup 03 North East Corner** | **4.00 m** | **8.0 m** | **4.00 m** | **5.8 m/s** |
| DropOff 02 North West Corner | 12.00 | 24.0 | 12.00 | 11.4 m/s |
| Pickup 04 West Quarter | 22.73 | 45.5 | 22.73 | 16.6 m/s |
| DropOff 04 Centre North | 28.56 | 57.1 | 24.00 | 17.2 m/s |
| DropOff 05 East Midpoint | 31.24 | 62.5 | 24.00 | 17.2 m/s |
| DropOff 06 South West Yard | 28.63 | 57.3 | 28.63 | 19.1 m/s |
| Pickup 06 North Centre | 30.00 | 60.0 | 30.00 | 19.7 m/s |
| DropOff 01 South East Gate | 35.00 | 70.0 | 35.00 | 21.6 m/s |
| Pickup 02 Centre Cross | 46.97 | 93.9 | 36.14 | 22.1 m/s |
| DropOff 03 West Midpoint | 40.00 | 80.0 | 40.00 | 23.5 m/s |
| Pickup 01 South West Gate | 40.00 | 80.0 | 40.00 | 23.5 m/s |
| Pickup 05 East Approach | 42.97 | 85.9 | 41.56 | 24.0 m/s |

`Pickup 03 North East Corner` is the worst single delivery anchor found anywhere in the project. Its
free clearance is 4.00 m and the pickup trigger is a 6 m sphere, so **a third of the zone's radius is
inside geometry**. The approach is an 8.0 m corridor, which caps the turn at 5.8 m/s, and the
scene is called Easy.

### 4.6 The scenes differ enough that one fix cannot serve them

| | container | scale | ground | road / corridor at the worst anchor | tightest 90° turn |
| --- | --- | --- | --- | --- | --- |
| `Possible game scene` | `Group 15 Greybox real one` | 8.97659 | `Floor` box, 504 × 555 | 25.5 m | 11.8 m/s |
| `Easy` | `Group 15 Greybox real one 1` | 8.97659 | single mesh, no slab | 8.0 m | 5.8 m/s |
| `Medium` | `'City '` (trailing space) | 1.2333 | `Floor` box, 579 × 602 | 13.81 m | 8.1 m/s |
| `Hard` | `City` | 2.0439076 | `Floor` box, 579 × 602 | 8.18 m (5.0 m locally) | 5.8 m/s, or impossible |

Three different containers, three different scales, three different road systems, and twelve
delivery anchors that have been hand-moved per scene rather than re-derived. **A change sized against
the greybox's 25.5 m corridors would do nothing for Hard's 8.18 m streets, and a turning circle
tightened enough for Hard would make the greybox trivially easy.** Whatever is decided, it has to be
decided per scene, or the geometry has to be unified first.

---

## 5. Evidence: the zone

`ZoneDetection` is 120 lines and there is no speed test in any of them.

```
float distance = Vector3.Distance(transform.position, order.PickupPoint);
if (distance > pickupRadius) continue;
```

- `pickupRadius` and `dropOffRadius` are **6 m in all five scenes** — verified individually in
  `Possible game scene`, `Kyuri's Scene`, `Easy`, `Medium` and `Hard`. `OrderBeacon` reads the same
  value every frame for its ground disc, so what the player sees is the real trigger.
- **No stop requirement, no slow-down requirement, no dwell time.** Arriving at any speed in any
  direction delivers.
- `Vector3.Distance` is 3D. The car's transform sits 0.8 m above the ground and the anchors are at
  y = 0, which shrinks the effective horizontal radius to 5.95 m. In `Hard`, where the road surface
  is 0.4 m above the slab the anchors sit on, it is about 5.88 m. Negligible.
- `checkInterval` is **0.1 s** in every scene, not the `0f` the script's own default implies. At
  32 m/s the car moves 3.2 m between checks, so the radius within which detection is *guaranteed*
  rather than merely likely is 5.78 m; at the 40 m/s boost ceiling it is 5.66 m. A pass that clips
  the edge of a zone at speed can be missed. Small, but it is a real 0.3 m of lost radius and it
  would read to a player as "I drove over it and nothing happened" — which is a reason to turn round
  and come back.

**Can the car enter a 6 m zone and stop inside it?** Yes, comfortably, and it does not need to. A
dead-centre pass gives a 12 m chord. From 20 m/s the car stops in 7.41 m, from 25 m/s in 11.57 m. It
only fails above about 25 m/s, and above 25 m/s on arrival there is nothing to fail at, because the
order has already been delivered by the time the brake matters.

---

## 6. Testing the working hypothesis

The brief asked me to treat "the testers are right" as the hypothesis and try to disprove it. Taking
the chain one link at a time:

| link | verdict |
| --- | --- |
| "Players reversed a lot" | **Unverified and unverifiable from here.** No reverse telemetry exists. Three unstructured sessions, two comments. |
| "Reversing means overshooting" | **Disproved for deliveries.** `ZoneDetection` has no stop requirement, so there is no overshoot to recover from. Reversing after a *missed turn* or a *wrong destination* is still possible, and the shift log's two zero-delivery sessions point that way. |
| "Overshooting means the turns are too wide" | **Disproved in the scene they played.** Tightest route bottleneck 25.5 m against a 20.9 m circle at 10 m/s; near-zero wall hits in all four sessions. **Confirmed for `Hard` and for one anchor in `Easy`**, where the geometry genuinely caps cornering at 5.8 m/s. |
| "Wide turns are the cause" | **Partly, by a different mechanism.** The friction circle removes 34% of lateral grip the moment the brake passes half travel, all of it at the rear. Braking into a corner swings the tail and `driftYawAuthority` then halves the steering's ability to catch it. That is a real "the turns are too wide" and a real route to needing reverse. Not measurable statically. |
| "Make reverse better" | **Confirmed, and it is not tuning.** Reverse has no camera, and the dedicated non-reversing brake is absent from the Controls screen (`QA-008`). |

So the teammate was right to treat the three comments as fewer than three findings, and picked the
wrong one to merge them into. The defensible merge is: **two testers could not tell where to go, and
the car gives no view and no documented tool for the manoeuvre you make when you get it wrong.** The
"turns are too wide" comment is a third, separate finding that is true of the scenes being built now
and false of the scene that was played.

---

## 7. Options, with numbers

Ordered by evidence behind them, not by effort.

### Option 1 — Give the camera a reverse case
**What:** when `ForwardSpeed` is below some threshold (−1.5 m/s matches the existing velocity gate),
either flip the look-at point behind the car or swing the camera round. Also guard
`ComputeHeading`'s `Vector3.Slerp` against antiparallel inputs.
**Numbers:** `cameraLookAhead` is 28 m (34 m on Loose) and currently always points forward.
`reverseTopSpeed` is 8 m/s, so a reversing player covers 8 m/s while looking 28 m the other way.
**Evidence:** two of three testers asked for better reverse; `ChaseCamera` has no reverse branch at
all; the Slerp is degenerate by inspection.
**Cost:** one method in `ChaseCamera.cs`. Changes no handling value and invalidates no measurement in
the CSV.
**What it breaks:** a camera that whips round at every brake-to-stop would be worse than the current
one. Needs a dead zone and an ease, and `cameraSwivelRecentreDelay` (1.2 s) is the existing precedent
for the timing.
**Retest:** a drive-round with deliberate reversing, plus whatever camera work comes out of the
separate mouse-sensitivity finding, since both live in the same file.

### Option 2 — Document the controls that already exist
**What:** put Left Ctrl, boost, jump, flip and camera look on the Controls screen.
**Numbers:** `VehicleInput.cs` lists eight actions; the build's Controls screen lists four.
**Evidence:** `QA-008`, filed 14 October from the same build. `VehicleInput`'s own comment says
Left Ctrl exists "for stopping ON a drop-off point" — the exact manoeuvre under discussion.
**Cost:** text. No code, no tuning, no scene.
**What it breaks:** nothing.
**Retest:** ask a fresh player to stop on a drop-off point without reversing and see whether they
find the key.

### Option 3 — Raise `maximumCurvature` from 0.35 to 0.5
**What:** the one change that tightens the turning circle without touching anything already measured.
**Numbers:** minimum radius goes from 2.86 m to 2.00 m, minimum circle from 5.71 m to 4.00 m, and the
narrowest corridor in which a right angle is possible goes from **6.48 m to 5.01 m**. That is
exactly the width of Hard's narrowest road stretches, so it converts them from impossible to
marginal rather than to comfortable — an intermediate 0.4 cap gives 5.87 m if marginal is not good
enough. The cap only binds below about 5 m/s, so **every reading in `vehicle_measurements.csv` stays
valid**: the 9.84 m/s turning-circle run computes a curvature of 0.0987, nowhere near either cap.
**Evidence:** the geometry tables in §4.3 and §4.5; the cap is what limits the car below 5 m/s, and
the complaints are about low-speed manoeuvring.
**Cost:** one field, three profiles. Surgical.
**What it breaks:** a sharper parking lock can read as twitchy and can make the car feel less like a
van. The `VehicleTuning` tooltip's own framing ("about a three point turn") is a design statement, so
this is a feel decision, not just a number.
**Retest:** F2 at 2, 4 and 6 m/s, which has never been run. That gap should be closed regardless of
whether this option is taken.

### Option 4 — Soften the friction circle, or raise its reference
**What:** either lower `frictionCircleStrength` from 0.8, or raise `longitudinalGripReference` from
16 towards the 27 m/s² that brake-plus-coast actually produces.
**Numbers:** today, coasting spends 5 of a 16 m/s² budget before the player touches anything, so
brake input above ≈0.5 saturates the circle and rear grip drops from 10.92 to 2.18 m/s². Effective
lateral grip falls 12.78 → 8.63 m/s², widening the achievable corner by 11% at 10 m/s, 21% at 15 and
32% at 20. Raising the reference to 27 would make full braking spend exactly the whole budget instead
of blowing through it at half travel.
**Evidence:** the code path and arithmetic in §3.4. **Not** evidence from play — no telemetry
distinguishes a braking slide from any other.
**Cost:** one or two fields. But this is the mechanic `VehicleTuning` spends two comment blocks
defending as the thing that makes throttle a handling input, so it is a design change, not a fix.
**What it breaks:** power oversteer and the handbrake drift both run through this path. Weakening it
makes the car blander and would invalidate the "demanding but predictable" framing in
`Tuning_Balanced.asset`'s `whyThisSetup`.
**Retest:** everything. F1 to F6 on all three profiles, plus a feel pass on drifting.

### Option 5 — Re-derive the delivery anchors for `Hard`, `Medium` and `Easy`
**What:** run the existing tooling against the actual road network of each scene instead of
hand-moving greybox coordinates.
**Numbers:** 6 of 12 anchors in `Hard` are not on a road tile; `Pickup 04` and `Pickup 05` still
carry the old greybox z values 252.71 and 120.14. In `Easy`, `Pickup 03 North East Corner` has 4.00 m
of clearance for a 6 m zone.
**Evidence:** §4.3 and §4.5, measured. `delivery_points.py`'s own docstring already requires this
after a re-export.
**Cost:** `find_roads.py` assumes a greybox pavement and will not work on tiled city roads as
written; it would need a road-tile mode. `place_delivery_points.py` already writes anchors and
refuses to run twice, so the writing half exists.
**What it breaks:** moving anchors changes every route length, so `order_distances.py` has to be
re-run and `orderTimeLimit` re-checked. The 36-pair, 954 m, 48 s model is specific to the greybox
set.
**Retest:** route distances and the order timer, per scene.

### Option 6 — Raise the zone radius
**What:** 6 m → 8 m.
**Numbers:** a 6 m zone gives a 12 m chord dead-centre and a guaranteed-detection radius of 5.78 m at
32 m/s. 8 m would give 16 m and 7.84 m. `OrderBeacon` reads the radius live so the disc follows
automatically with no second number to keep in sync.
**Evidence:** weak for the turning problem, since nothing requires stopping. Stronger for `Easy`'s
`Pickup 03`, where the zone is already bigger than the free space — though there the fix is to move
the anchor, not to grow the zone further into a building.
**Cost:** one field per scene.
**What it breaks:** a bigger zone makes arrival less precise and devalues the driving. It also makes
the `Easy` overlap worse. **Not recommended** ahead of options 1, 2 and 5.

### Option 7 — Widen the corners in `Hard`
**What:** lay a second row of road tiles on the junctions the routes have to use.
**Numbers:** going from one 8.18 m module to two (16.36 m) raises the corner speed cap from 5.84 m/s
to 9.02 m/s on Balanced, or 10.46 m/s on Grippy.
**Evidence:** §4.3, measured.
**Cost:** level work in a scene that is not in this working tree, on an asset pack whose tiles are
modular (so this is tile placement, not resizing). **No prop or building is resized by this** — it
adds road where there is currently none.
**What it breaks:** block layouts and anything parked at the kerb. Interacts with option 5, since
re-derived anchors would want the wider junctions anyway.
**Retest:** a drive of each route; `order_distances.py` if any anchor moves.

### Option 8 — Measure Grippy, and measure the turning circle at more than one speed
**What:** run F6 on all three profiles, and F2 at a spread of speeds.
**Numbers:** all 787 rows across both CSVs are Balanced. Grippy's radius at 10 m/s is 7.98 m against
Balanced's 10.43 — 24% tighter, free, already authored, never driven.
**Evidence:** the CSV itself.
**Cost:** `turnTestSpeed` is a serialized field on the `Measurement` object, so this needs an editor
session and a few minutes. No code change.
**What it breaks:** nothing. It is the cheapest way to replace the arithmetic in §3.1 with
measurement, and it would settle option 3 and option 8's own question at the same time.
**Retest:** n/a — this *is* the test.

### Also worth logging, cheaply: the telemetry gap
`PlaytestLogger` records delivered, late, value, boost, handbrake seconds, wall hits, flips,
supersonic seconds and longest recovery. It does not record **reverse seconds, reverse distance,
time below 2 m/s, brake seconds, or time spent with steering at full lock**. Every one of those is a
one-line addition next to `_handbrakeSeconds`, and the first two would have turned this entire
investigation from arithmetic into measurement. If one thing is done before the next playtest, it
should be that.

---

## 8. What I could not determine

Stated plainly, as asked:

1. **Whether players actually reversed, and how much.** There is no reverse telemetry. The two
   comments are a direction to investigate and nothing more; the session was unstructured, with no
   questionnaire and no per-tester data, and the four shift rows cannot be attributed to the three
   testers.
2. **Building footprints in `Hard` and `Medium`.** 1295 and 397 MeshColliders across many asset-pack
   models. Only the carriageway was measured there, so I cannot give obstacle clearance per anchor in
   those two scenes the way I can for the greybox.
3. **Whether the braking-oversteer mechanism in §3.4 is what players felt.** The code path and the
   numbers are certain; the behaviour in play is not measurable without running the game, which this
   investigation is not permitted to do.
4. **Grippy and Loose handling.** Never measured. The figures given for them are arithmetic from
   their assets.
5. **The effect of weather.** `Easy`, `Medium` and `Hard` each have a `Weather` root;
   `Possible game scene` has none, and `SurfaceType.ApplyWeather` is a no-op when the surface blend
   is dry. Every figure in this report is a dry-road figure. Wet or icy surfaces multiply
   `gripMultiplier`, which moves the grip-limited radius and the stopping distance together, and none
   of that has been measured.
6. **`Cube.022` in the greybox.** Its mesh bounds, applied to its transform, produce a
   592 × 86 × 779 m box covering most of the map — either a boundary backdrop or a mesh whose bounds
   do not belong to the transform the scene pairs them with. It is excluded from the free-space grid
   and named in the tool's output rather than dropped silently. If it is a real collider standing in
   the playfield, the greybox clearance figures in §4.2 are wrong; nothing else I measured suggests
   it is.

---

## 9. Reproducing every number here

```
# street grid and the 8.8-25.5 m kerb spacing
python docs/tooling/scene-tools/find_roads.py "Assets/Project/Scenes/Possible game scene.unity"

# mesh bounds for the greybox, needed because its obstacles are MeshColliders
python docs/tooling/scene-tools/fbx_mesh_bounds.py \
    "Assets/Aside/Art/Blender/Group 18 Greybox draft 1.fbx" --json=greybox_bounds.json

# colliders, calibration, per-anchor clearance and route bottlenecks
python docs/tooling/scene-tools/corner_geometry.py \
    "Assets/Project/Scenes/Possible game scene.unity" --bounds=greybox_bounds.json

# the city scenes live on origin/main, not in this worktree
git show "origin/main:Assets/Project/Scenes/Hard.unity" > Hard.unity
python docs/tooling/scene-tools/corner_geometry.py Hard.unity --tiles=^road_base
python docs/tooling/scene-tools/corner_geometry.py Hard.unity --road=^road_ --grid=0.5
```

Files and symbols cited:

- `Assets/Project/Scripts/Vehicle/VehicleTuning.cs` — `MaxCurvatureAt`, `AccelerationAt`,
  `LateralGripAfterLongitudinal`, `SteeringRadiusAt`, `GripLimitedRadiusAt`,
  `PredictedTurnRadiusAt`, `PredictedStoppingDistanceFromTopSpeed`
- `Assets/Project/Scripts/Vehicle/VehicleController.cs` — `ApplyDriveAndBrake` (reverse branch),
  `ApplySteering`, `SmoothSteerInput`, `ApplyLateralGripPerAxle`, `ApplyAxleGrip`
- `Assets/Project/Scripts/Vehicle/VehicleMeasurement.cs` — `MeasureStoppingDistance`,
  `MeasureTurningCircle`, `turnTestSpeed`
- `Assets/Project/Scripts/Vehicle/ChaseCamera.cs` — `ComputeHeading`, `ComputeDesiredRotation`,
  `ReadSwivelInput`
- `Assets/Project/Scripts/Vehicle/VehicleInput.cs` — `Throttle` composite, `Brake` binding, the
  control-summary comment block
- `Assets/Project/Scripts/Vehicle/PlaytestLogger.cs` — `Header`, `FixedUpdate`, `WriteRow`
- `Assets/Project/Scripts/Orders/ZoneDetection.cs` — `pickupRadius`, `dropOffRadius`,
  `checkInterval`, `CheckPickups`, `CheckDeliveries`
- `Assets/Project/Scripts/Orders/OrderBeacon.cs` — `fallbackGroundDiscRadius`, `PlaceBeacon`
- `Assets/Project/Scripts/Vehicle/Tuning/Tuning_{Balanced,Grippy,Loose}.asset`
- `VehicleLab_Measurements/vehicle_measurements.csv` (640 rows) and
  `vehicle_measurements_ako_local.csv` (147 rows)
- `Game Build/PlaytestLogs/shift_log.csv`, `PlaytestLogs/shift_log.csv`
- `docs/tooling/scene-tools/{README.md,delivery_points.py,find_roads.py,order_distances.py}`
- `docs/QA-PLAYTEST-REPORT-2026-10-14.md` — QA-008, QA-009
- `docs/M1-GEMINI-PROMPT-PACK.md` lines 530 and 669, which already record "corner geometry has not
  yet been sized against the measured turning circle" as an open gap. This report closes it.
