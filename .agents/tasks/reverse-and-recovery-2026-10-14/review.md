# Reverse camera view, frame-rate-independent mouse look, overturned-car recovery and five telemetry columns

Four commits on `fix/reverse-and-recovery` touch five files under `Assets/Project/Scripts/Vehicle/` and nothing else. The camera gains a hysteretic, eased 180° swing driven by `ForwardSpeed` plus an antiparallel guard on `ComputeHeading`'s velocity blend; mouse look is re-derived in degrees-per-pixel with no `Time.deltaTime` while the stick path keeps its `dt`; the existing Flip action gains a second meaning when the car is overturned and at rest, backed by a deterministic pose write that deliberately avoids `Teleport`'s boost refill; and `PlaytestLogger` stops reconstructing its two duration fields and appends five reverse/recovery columns. Every new knob is a `[SerializeField]` on a MonoBehaviour, so the three frozen tuning profiles and every scene stay untouched. I re-ran all three verification commands myself rather than trusting the FEAT findings, and they reproduce the recorded output byte for byte.

**Watch for:** nothing blocking. Three non-blocking observations, all **confirmed** by reading the code: a world-down raycast now fires every physics step even when the self-right is disabled and the car is upright (`VehicleJumpFlip.UpdateOverturnedState`); an unidentifiable look device is treated as a mouse, so stick values would be read as pixels in that fallback (as the plan specified); and the righting rotation uses world up rather than the probe's surface normal, so slope and overhead clearance remain play-test questions.

**Verdict**: APPROVED

## High-level view

The reverse view is a latch, not a sign test. `UpdateReverseView` accumulates separate hold timers either side of a 1 m/s dead band (`-1.5` enter, `-0.5` exit), `MoveTowards` makes a full swing take exactly `reverseViewTransitionTime`, and `SmoothStep` supplies the ease. The swing is a single `Quaternion.AngleAxis(180° × eased, Vector3.up)` applied *after* the heading smoothing, so `_smoothedHeading` stays in nose space and `SmoothDirection`'s own `Vector3.Slerp` can never see the 180° step this task exists to avoid. The only other `Vector3.Slerp` in the file, the velocity blend in `ComputeHeading`, is now skipped when `Dot(nose, velocityDirection) <= -0.95f`. There is no antiparallel Slerp left on the reverse path. The behavioural consequence of the hysteresis is that accelerating out of a reverse leaves the camera partly swung for up to `reverseViewReturnDelay + reverseViewTransitionTime` ≈ 0.8 s; that is the deliberate trade for not whipping round in a three-point turn, and it is one of the play-test items.

Mouse look is genuinely frame-rate independent. `ReadSwivelInput` divides the merged look vector by the `0.06` binding scale to recover pixels and multiplies by `mouseLookDegreesPerPixel` with no `dt` anywhere; the stick branch is the original arithmetic unchanged, still `look * cameraSwivelSpeed * dt` behind its original `sqrMagnitude > 0.0001f` gate. Both branches share the original idle/recentre tail, and a sub-deadzone mouse frame correctly falls through to it, which is what lets the camera auto-recentre for a resting hand for the first time.

The self-right cannot fire airborne or out of a drift, and the reason is arithmetic rather than a check that happens to pass. `IsDrifting` requires `Speed >= driftMinimumSpeed` (4) and the gate caps `Speed` at 2, so the two are mutually exclusive before `!IsDrifting` is even consulted. `Speed` is the full 3D `linearVelocity.magnitude`, so a falling car reads fast; staying inside 2 m/s, under 1.5 rad/s of spin and within the 2 m world-down probe for a continuous 0.6 s is not something a trajectory under gravity does. The dispatch spends a queued flip on recovery only when `CanSelfRightNow`, and `TrySelfRight` clears that flag on success so the 3 s backstop cannot double-fire in the same step. Recovery calls `SetPoseAndClearMotion`, never `Teleport`, so the boost tank is not refilled and `boost_earned` keeps describing driving.

Both logger duration fields are now measured rather than reconstructed, on all three exit paths. `_shiftLengthSeconds` is captured in `HandleShiftStarted` while the `ShiftTimer` is certainly alive, `_playSeconds` is summed in `FixedUpdate`, and `WriteRow` sets `_rowWritten` and clears `_shiftRunning` on entry, so whichever of `HandleShiftEnded`, `OnDestroy` or the new `OnApplicationQuit` arrives first writes exactly one row. `_shiftStartTime` and `ShiftLengthSeconds()` are gone, which is what keeps the zero-warning gate clean. The header is 20 columns with the first 15 byte-for-byte identical to `origin/main` and the five new ones appended — I verified that programmatically against `git show origin/main:` rather than by eye.

The hard constraints hold. `git diff origin/main...HEAD --name-only` lists only the five expected source files; the diff restricted to `Assets/Project/Scripts/Vehicle/Tuning`, `Assets/Project/Scenes`, `Assets/Project/Scripts/Orders`, `Assets/Project/Scripts/UI`, `SurfaceType.cs`, `docs/tooling`, `*.csproj` and `PlaytestLogs` is empty; `git log --name-only` confirms no csproj or log path was ever committed; and `git status --porcelain` is clean.

<details>
<summary>Issues (3)</summary>

No blocking problems were found. The three items below are non-blocking observations; none of them contradicts the plan, and none needs to be fixed before this branch is usable.

1. **Unconditional ground raycast** — `Assets/Project/Scripts/Vehicle/VehicleJumpFlip.cs`, `UpdateOverturnedState`: the five gates are assigned to separate `bool` locals, so `_controller.ProbeGroundBelow(selfRightGroundProbe, out _groundHit)` casts a ray every physics step even when `selfRightEnabled` is false or the car is plainly upright. Evaluating `overturned && slow && notSpinning && notDrifting` first and probing only then would remove one `RaycastNonAlloc` per step with no behaviour change. Settled by a profiler reading in the play test if the physics step budget is tight.
2. **Unknown look device is read as a mouse** — `Assets/Project/Scripts/Vehicle/ChaseCamera.cs`, `ReadSwivelInput`: `usingMouse` is true when `input == null` or `CameraLookIsMouse` is still at its `true` default, so in that fallback a stick value of 1.0 would be divided by `MouseBindingDeltaScale` and read as 16.7 px (≈0.83° per frame, frame-rate dependent in the wrong direction). This is exactly the fallback plan.md item 3 asked for, and `VehicleInput` flips the flag as soon as a stick actuates a non-null `activeControl`, so it should never be observed. Settled by the gamepad check already listed in FEAT-001's play-test items: plug in a pad and confirm the right stick feel is unchanged.
3. **Righting rotation ignores the surface normal** — `Assets/Project/Scripts/Vehicle/VehicleJumpFlip.cs`, `TrySelfRight`: the car is placed at `_groundHit.point + Vector3.up * selfRightRideHeight` with `Quaternion.LookRotation(flat, Vector3.up)`, so on a steep slope the car arrives level rather than parallel to the ground, and nothing checks clearance *above* the car. Already flagged in FEAT-002's findings as unverifiable without Unity. Settled by self-righting on a ramp, a kerb, a raised road in `Hard`/`Medium` and under an overpass, and confirming the car neither intersects geometry nor drops noticeably.

</details>

<details>
<summary>Details</summary>

### Verification re-run, not taken on trust

From the worktree root, with the gitignored `Assembly-CSharp*.csproj` and `PlaytestLogs/shift_log.csv` prep already in place from FEAT-001:

```
python docs/tooling/compile_check.py
  RUNTIME  342 references, 83 scripts (49 excluding imported TextMesh Pro samples)
    errors 0, warnings 0
  EDITOR   360 references (18 editor-only), 5 scripts
    errors 0, warnings 0
  Compiles clean.

python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks -q --no-header
  34 passed in 21.36s

python -B docs/tooling/playtest_row.py --log "PlaytestLogs/shift_log.csv" --list
  [0] 2026-10-12 13:33:39  scene=Possible game scene  play_s=300.0  shift_s=300  delivered=9 late=7
  [1] 2026-10-14 11:57:50  scene=Possible game scene  play_s=0.0  shift_s=11  delivered=2 late=12
  [2] 2026-10-14 12:00:25  scene=Possible game scene  play_s=0.0  shift_s=185  delivered=1 late=4

git status --porcelain
  (empty)
```

Identical to what the FEAT files record, including the `18 editor-only` reference count and the two historical `play_s=0.0` rows that are evidence of the defect and were correctly left alone. The commit subjects are 65, 66, 55 and 67 characters, inside the 72-char rule.

### Field names and defaults against the plan

Checked name by name and default by default, not by sampling. `ChaseCamera`: `reverseViewEnterSpeed -1.5f`, `reverseViewExitSpeed -0.5f`, `reverseViewHoldTime 0.25f`, `reverseViewTransitionTime 0.45f`, `reverseViewReturnDelay 0.35f`, `mouseLookDegreesPerPixel 0.05f`, `mouseLookDeadzonePixels 1f`, plus `private const float MouseBindingDeltaScale = 0.06f`. `VehicleJumpFlip`: `selfRightEnabled true`, `selfRightUprightThreshold 0.5f`, `selfRightHoldTime 0.6f`, `selfRightMaxSpeed 2f`, `selfRightMaxAngularSpeed 1.5f`, `selfRightGroundProbe 2f`, `selfRightRideHeight 0.8f`, `selfRightCooldown 2f`, `selfRightAutoAfterSeconds 3f`. `PlaytestLogger`: `reverseSpeedThreshold 0.5f`, `crawlSpeedThreshold 2f`, `brakeInputThreshold 0.05f`. All are `[Tooltip]` + `[SerializeField] private` on the MonoBehaviour, which is what makes the no-scene-edit claim hold.

### The CSV header, checked programmatically

Both `Header` constants were extracted from source (`git show origin/main:` for the old one) and split on commas: 15 columns before, 20 after, the 20-column prefix equal to the old string, and the appended tail `reverse_seconds | reverse_distance_m | seconds_below_2mps | brake_seconds | self_right_count`. The `string.Join` in `WriteRow` emits the five new values in the same order with formats `"0.0"`, `"0.0"`, `"0.0"`, `"0.0"` and integer. The log already on disk still carries the 15-column header, which is why `WarnIfHeaderDrifted` exists; it reads the first line with BOM detection, warns once and does not rewrite, move or delete the file. The `--list` output above is the proof that appending left every historical column at the same index.

### `Teleport` extraction, and the physics path

`SetPoseAndClearMotion` holds the old first half verbatim and `Teleport` is now that call followed by `Drive = default`, `SteerAmount = 0f`, `_steerVelocity = 0f` and the unchanged boost block. The one reordering is `_stuckTimer = 0f` moving earlier inside the same call; nothing between the two points reads it, so the measurement harness cannot observe the difference. No line of `ApplyDriveAndBrake`, `ApplySteering`, `ApplyLateralGrip*`, `ApplyAxleGrip`, `ApplySuspension`, `ApplyBoost`, `EarnBoost` or `ApplyStuckRecovery` is modified, and `rightingTorque` and its three gates are still the tuning asset's `8 / 0.35 / 1.2 / 1.5`.

### What this review cannot settle

Everything that needs Unity: whether 0.45 s reads as a swing rather than a lurch, whether 1 m/s of dead band survives a real three-point turn, whether 0.05 °/px feels sluggish to a tester used to the 60 fps behaviour, whether the car can come to rest inverted at all, whether `rightingTorque: 8` rights it inside the 3 s backstop window, and whether 0.8 m of ride height clears a raised road. The FEAT findings list these with concrete steps; they remain open and are not grounds for withholding the verdict, since all fourteen new numbers are serialized and retune without a recompile.

</details>

<details>
<summary>Files changed</summary>

- `Assets/Project/Scripts/Vehicle/ChaseCamera.cs` — reverse-view state machine, 180° swing about world up, antiparallel guard on the velocity blend, two-branch look input, gizmo and `SnapToTarget` reset.
- `Assets/Project/Scripts/Vehicle/VehicleInput.cs` — one sticky `CameraLookIsMouse` property so the camera can scale the two devices differently.
- `Assets/Project/Scripts/Vehicle/VehicleController.cs` — additive `ProbeGroundBelow` (world down) and the pose half of `Teleport` split into `SetPoseAndClearMotion`.
- `Assets/Project/Scripts/Vehicle/VehicleJumpFlip.cs` — nine self-right fields, the sustained-overturned detector, `TrySelfRight`, the Flip-key dispatch and the 3 s backstop.
- `Assets/Project/Scripts/Vehicle/PlaytestLogger.cs` — captured shift length, accumulated play clock, `OnApplicationQuit`, five appended columns and the header-drift warning.

Full diff: `git diff origin/main...HEAD` in `.worktrees/reverse-recovery`.

</details>
