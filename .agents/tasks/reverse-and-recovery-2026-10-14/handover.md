# Handover: reverse view, mouse look, overturned-car recovery, reverse telemetry

Branch: `fix/reverse-and-recovery` — **local only, never pushed, not merged.**
Worktree: `.worktrees\reverse-recovery`
Base: `origin/main` at `f31f8d9`. Four commits ahead, nothing behind.

Per `docs/GIT-RULES.md` rule 6 Ako is the merge owner, and rule 9 requires a 60-second play test in Unity 6000.5.4f1 before anything reaches `main`. No agent can run Unity, so the merge and the play test below are yours.

```
a670951 feat(telemetry): log reverse, crawling, brake use and self rights
93a1788 fix(telemetry): measure shift and play seconds instead of guessing
555736c feat(vehicle): let the flip key right an overturned car
3a23877 fix(camera): add reverse view and frame-rate-independent mouse look
```

Five files, all under `Assets/Project/Scripts/Vehicle/`, 651 insertions / 33 deletions. No scene, no `Tuning_*.asset`, no `Orders/`, no `UI/`, no `SurfaceType.cs`, no `docs/tooling`, no csproj, no shift log.

## What changed, per file

**`ChaseCamera.cs`** (+211) — A reverse view that is a latch rather than a sign test. `UpdateReverseView(dt)` holds separate timers either side of a 1 m/s dead band, and `ApplyReverseView` rotates the heading by `180° × SmoothStep(blend)` about world up. The rotation is applied *after* the heading smoothing, so `_smoothedHeading` stays in nose space and `SmoothDirection`'s own `Vector3.Slerp` never sees a 180° step. `ComputeHeading`'s velocity blend is now skipped when `Dot(nose, velocityDirection) <= -0.95f`, which is the antiparallel guard. `ReadSwivelInput` is two branches sharing the original idle/recentre tail: the mouse branch recovers pixels by dividing out the `0.06` binding scale and multiplies by `mouseLookDegreesPerPixel` with **no `Time.deltaTime`**; the stick branch is the original `look * cameraSwivelSpeed * dt` arithmetic, untouched. `SnapToTarget` resets all four pieces of reverse state (`VehicleMeasurement.ResetCar` calls it before every reading). The playing-mode sightline gizmo follows the rotated heading.

**`VehicleInput.cs`** (+22) — One sticky property, `public bool CameraLookIsMouse { get; private set; } = true;`, set in `Update` from `_cameraLook.activeControl?.device is Mouse` only while the look vector is non-zero and the device is non-null. Sticky because `activeControl` is null at rest. Defaults to mouse when the device is unknown, which is the one fallback to confirm with a pad plugged in.

**`VehicleController.cs`** (+52) — Two additive members and one behaviour-preserving extraction. `public bool ProbeGroundBelow(float distance, out RaycastHit hit)` casts along **world** `Vector3.down` (the suspension rays run along `-transform.up`, which points at the sky on an inverted car), reusing the existing own-collider filter and `tuning.groundMask`; read-only, no force. `public void SetPoseAndClearMotion(Vector3, Quaternion)` is the first half of the old `Teleport` verbatim, and `Teleport` is now that call plus its measurement-specific tail (`Drive = default`, steer resets, the unchanged boost block). Recovery calls the pose half only, deliberately: `Teleport` refills `BoostRemaining`, so calling it would make rolling the car the cheapest way to buy boost and would corrupt `boost_earned`. No line of `ApplyDriveAndBrake`, `ApplySteering`, `ApplyLateralGrip*`, `ApplyAxleGrip`, `ApplySuspension`, `ApplyBoost`, `EarnBoost` or `ApplyStuckRecovery` changed.

**`VehicleJumpFlip.cs`** (+227) — Nine serialized self-right fields, a sustained-overturned detector (`UpdateOverturnedState`), `TrySelfRight()`, the Flip-key dispatch and a 3 s no-input backstop. New readable surface: `SelfRightCount`, `SelfRightCooldownRemaining`, `CanSelfRightNow`, `event Action SelfRighted`. **No new input action** — the existing Flip binding (Q) carries the second meaning, so `VehicleControls.inputactions` and the README Controls table stay unchanged and their contract test still passes. Airborne and mid-drift self-rights are unreachable by arithmetic, not by a check that happens to pass: drifting needs `Speed >= driftMinimumSpeed` (4) and the gate caps `Speed` at 2.

**`PlaytestLogger.cs`** (+172) — `_shiftStartTime` and `ShiftLengthSeconds()` are gone. The shift length is captured once in `HandleShiftStarted` while the `ShiftTimer` is certainly alive (it was being read in `OnDestroy`, where a destroyed component reads as null and wrote `shift_seconds=0`), and the play clock is accumulated in `FixedUpdate` (it was `Time.time - _shiftStartTime` against a stamp `HandleShiftStarted` re-takes, which wrote `play_seconds=0.0`). `OnApplicationQuit` was added; `WriteRow` sets `_rowWritten` and clears `_shiftRunning` on entry, so exactly one row is written whichever of the three exit paths fires first. A logger that attaches mid-shift now warns once that its row covers only the part it saw. Five columns appended, plus a once-only header-drift warning that reads the first line with BOM detection and **does not** rewrite, move or delete the existing log.

## The shift-log header, exactly

Twenty columns. The first fifteen are byte-for-byte what `origin/main` writes (verified programmatically against `git show origin/main:`), and the five new ones are appended, never inserted, because seven historical rows across the two logs were written in the old order and `docs/tooling/playtest_row.py` reads by header name.

```
timestamp,scene,shift_seconds,play_seconds,delivered,late,value,boost_activations,boost_seconds,boost_earned,handbrake_seconds,wall_hits,flips,time_above_supersonic_s,longest_recovery_s,reverse_seconds,reverse_distance_m,seconds_below_2mps,brake_seconds,self_right_count
```

`PlaytestLogs/shift_log.csv` on disk still begins with the old fifteen-column header, so the first new row will trigger the drift warning. Archive or rename that file when you want a clean twenty-column log; everything already in it keeps parsing either way.

## Every new serialized field, its default, and where to tune it

All of them are `[Tooltip]` + `[SerializeField] private` on a MonoBehaviour, not on `VehicleTuning`. That is what keeps the three tuning profiles and every scene untouched: a scene saved before these fields existed carries no entry for them, so Unity falls back to the C# initialisers. Retuning any of them needs no recompile and no code change.

**ChaseCamera component** (on the chase camera in the scene — Inspector, "Reverse view" and "Mouse look" headers):

| Field | Default | Note |
|---|---|---|
| `reverseViewEnterSpeed` | `-1.5` | matches `ComputeHeading`'s own velocity gate |
| `reverseViewExitSpeed` | `-0.5` | matches the controller's reverse gate |
| `reverseViewHoldTime` | `0.25` s | raise this first if the camera twitches at a stop |
| `reverseViewTransitionTime` | `0.45` s | a full swing takes exactly this long |
| `reverseViewReturnDelay` | `0.35` s | stops a double swing in a three-point turn |
| `mouseLookDegreesPerPixel` | `0.05` | raise this if it feels sluggish; do **not** reintroduce `dt` |
| `mouseLookDeadzonePixels` | `1` px | what finally lets the camera auto-recentre for a resting hand |

`MouseBindingDeltaScale = 0.06f` is a `private const`, not a field: it mirrors the `ScaleVector2` processor on `<Mouse>/delta` in `VehicleControls.inputactions` and in `VehicleInput.BuildFallbackControls`. If that processor is ever changed, this constant has to change with it.

**VehicleJumpFlip component** (Inspector, "Self right" header):

| Field | Default | Note |
|---|---|---|
| `selfRightEnabled` | `true` | |
| `selfRightUprightThreshold` | `0.5` | `VehicleController.IsUpright`'s own line |
| `selfRightHoldTime` | `0.6` s | how long the resting-overturned state must hold |
| `selfRightMaxSpeed` | `2` m/s | below `driftMinimumSpeed` 4, which is why a drift cannot self-right |
| `selfRightMaxAngularSpeed` | `1.5` rad/s | |
| `selfRightGroundProbe` | `2` m | world-down ray length |
| `selfRightRideHeight` | `0.8` m | the spawn height every scene uses; **check this in Hard and Medium** |
| `selfRightCooldown` | `2` s | |
| `selfRightAutoAfterSeconds` | `3` s | no-input backstop, twice the tuning asset's 1.5 s righting-torque delay |

**PlaytestLogger component** (Inspector, under "Thresholds, matched to VehicleMeasurement"):

| Field | Default | Note |
|---|---|---|
| `reverseSpeedThreshold` | `0.5` | the controller's own reverse gate, so the column counts what the car counts |
| `crawlSpeedThreshold` | `2` | the column name `seconds_below_2mps` hard-codes 2; change one, change the other |
| `brakeInputThreshold` | `0.05` | the dedicated Brake action (Left Ctrl / pad B), not S |

Untouched and still as shipped on all three profiles: `rightingTorque` 8, `stuckUprightThreshold` 0.35, `stuckSpeedThreshold` 1.2, `stuckTimeBeforeRighting` 1.5, `autoRightWhenStuck` on. The automatic torque and the new self-right are layered, not competing: the torque gets 1.5 s, a Flip press can take over from 0.6 s, the backstop fires at 3 s, and `SetPoseAndClearMotion` zeroes `_stuckTimer` so the torque's countdown restarts after a recovery.

## Manual steps you must perform in Unity (6000.5.4f1)

Nothing below can be settled from code or tooling. Open `Assets/Project/Scenes/Possible game scene.unity`, and turn `logRefusals` on for the recovery items.

**1. Camera feel on the reverse swing.** Hold S past −1.5 m/s and confirm the view swings round **once**, eased rather than lurching, and that 0.45 s reads as a swing and not a whip. Brake to a dead stop with Left Ctrl and confirm it does *not* swing — that is the dead band plus the 0.25 s hold; if it twitches, raise `reverseViewHoldTime` before touching the speeds. Shuffle forward and back through a three-point turn and confirm at most one swing per shuffle, not two. Known trade: accelerating out of a reverse leaves the camera partly swung for up to `reverseViewReturnDelay + reverseViewTransitionTime` ≈ 0.8 s. Then reverse fast (up to `reverseTopSpeed` 8 m/s) down a street and watch for the camera clipping buildings as it comes round the car's flank — `KeepOutOfWalls` runs on the rotated heading, but that path has never been seen against real geometry. Finally run the F5 road-ahead measurement in VehicleLab after a reversing run, which confirms the `SnapToTarget` reset.

**2. Mouse sensitivity.** Sweep the mouse with vsync on, with vsync off, and with a 30 fps cap if you can set one, and confirm the camera turns the same amount for the same hand movement. It now does `0.05 °/px` at every frame rate; before it was 0.24 at 30 fps, 0.12 at 60 and 0.050 at 144, so a 400 px sweep used to give 96 / 48 / 20° and now gives 20° everywhere. That means it is **2.4× slower than the 60 fps feel you have been playing with** — if it feels sluggish, raise `mouseLookDegreesPerPixel`; do not put `Time.deltaTime` back. Confirm a resting hand now lets the camera auto-recentre after `cameraSwivelRecentreDelay` 1.2 s, and that the 1 px dead zone does not feel sticky starting a slow pan. Plug in a gamepad and confirm the right stick is unchanged; that also confirms `CameraLookIsMouse` is flipping, and it is the only way to exercise the unknown-device fallback, which reads a stick value as pixels.

**3. Overturned-car recovery.** On the roof, press Q and confirm the car rights after about 0.6 s facing the way it was facing. Repeat and press nothing: confirm it rights within about 3 s. Then check for false positives — drive hard for 60 s, jumping, flipping and drifting, and confirm `self_right_count` stays 0 with no refusal/self-right line in the console; the case to watch is a heavy landing that briefly reads slow, overturned and near ground, which the 0.6 s hold should absorb. Also spend the boost tank, self-right, and confirm `BoostRemaining` does **not** jump back to capacity (the BoostGauge shows it) and that `boost_earned` in the next row is not inflated. Run F4 in VehicleLab afterwards and confirm the flip measurement still gives its usual PASS/FAIL.

**4. Can the car rest inverted at all?** The body collider is a flat 1.8 × 1.0 × 4.2 m box that tends to fall back onto its wheels, and `flips` is 0 in all seven logged rows, so nobody has ever been observed in this state. Flip it onto its roof against a kerb or wall on purpose and see whether it stays. If it cannot be made to stay, the self-right is insurance rather than a fix — leave the defaults alone. Also press nothing first and see whether `rightingTorque` 8 rolls a 900 kg box over inside its own 1.5 s window, in which case the 3 s backstop never fires in practice.

**5. The 0.8 m ride height in Hard and Medium.** `selfRightRideHeight` 0.8 is the spawn height every scene uses with the floor plate at y = 0, but in Hard and Medium the city sits roughly 0.4 m above that plate. Self-right on a raised road, on a kerb, on a ramp and under an overpass, and confirm the car neither spawns intersecting geometry nor drops noticeably. The probe uses `_groundHit.point` so it should follow the raised surface; what nothing checks is clearance **above** the car in a tunnel, and the righting rotation uses world up rather than the probe's surface normal, so on a steep slope the car arrives level rather than parallel to the ground.

## Verification evidence

Re-run from the worktree root just now, after the final commit. The gitignored prep (`Assembly-CSharp*.csproj` with absolute hint paths, and a copy of `PlaytestLogs/shift_log.csv`) was already in place and was never staged.

```
python docs/tooling/compile_check.py
  RUNTIME  342 references, 83 scripts (49 excluding imported TextMesh Pro samples)
    errors 0, warnings 0
  EDITOR   360 references (18 editor-only), 5 scripts
    errors 0, warnings 0
  Compiles clean.

python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks -q --no-header
  .................................. [100%]
  34 passed in 20.19s

python -B docs/tooling/playtest_row.py --log "PlaytestLogs/shift_log.csv" --list
  [0] 2026-10-12 13:33:39  scene=Possible game scene  play_s=300.0  shift_s=300  delivered=9 late=7
  [1] 2026-10-14 11:57:50  scene=Possible game scene  play_s=0.0  shift_s=11  delivered=2 late=12
  [2] 2026-10-14 12:00:25  scene=Possible game scene  play_s=0.0  shift_s=185  delivered=1 late=4

git status --porcelain
  (empty)

git log --oneline origin/main..HEAD
  a670951 93a1788 555736c 3a23877   (four commits, listed above)

git status -sb
  ## fix/reverse-and-recovery...origin/main [ahead 4]
```

`git branch -r` lists no `origin/fix/reverse-and-recovery`, and `git ls-remote --heads origin` has no match for "reverse": nothing was pushed. `git diff origin/main...HEAD --name-only` lists only the five source files above. The two `play_s=0.0` rows are the historical defect this branch fixes for future rows; they are evidence of sessions that happened and were deliberately left alone.

No new column can be exercised outside Unity — the logger only writes at shift end — so the row's correctness rests on code reading plus the fact that the fifteen-column file on disk still parses identically.

## What the implementation refused to do, on a hard constraint

- **`rightingTorque` was not raised.** If the play test shows the existing 8 rad/s² cannot roll the car off its roof, that is to be reported, not tuned: the value lives in all three frozen `Tuning_*.asset` files, and the deterministic self-right exists precisely so the torque does not have to be touched.
- **`ShiftTimer.cs` got no getter.** It is a teammate's file under `Orders/` and was off limits, so it was read only. The consequence is real: a logger that attaches mid-shift cannot recover the *configured* shift length, which is why that case warns instead of guessing.
- **No new input action.** The README Controls table is contract-tested against `VehicleControls.inputactions`, so recovery reuses Flip (Q) — which is also the key a tester already tries.
- **No scene was edited and none is needed.** Every new knob is a serialized MonoBehaviour field.
- `SurfaceKind`, prop and building sizes, grip, steering, throttle, brake, mass, reverse acceleration and reverse top speed are all untouched. `docs/tooling/playtest_row.py` is untouched; its existing `shift_seconds == 0` and `play < shift * 0.9` warnings become meaningful again on their own. No existing shift log was rewritten, moved or deleted. `GIT_LFS_SKIP_SMUDGE` was never set.

## Non-blocking observations from review

Three, all confirmed by reading the code, none worth fixing before you use the branch. `UpdateOverturnedState` fires a world-down raycast every physics step even when the self-right is disabled and the car is plainly upright — reorder the gates if the physics budget is tight. An unidentifiable look device is treated as a mouse, so a stick value would be read as pixels in that fallback (as specified); the gamepad check above settles it. And the righting rotation ignores the surface normal, covered by step 5.
