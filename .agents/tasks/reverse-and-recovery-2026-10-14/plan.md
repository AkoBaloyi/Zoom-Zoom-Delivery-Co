# Implementation Plan — reverse view, look sensitivity, overturned recovery, telemetry

Worktree: `c:\Users\akoba\DIGI2001\Project\GameDesign\Zoom Zoom Delivery Co\.worktrees\reverse-recovery`
Branch: `fix/reverse-and-recovery` at `f31f8d9` (= `origin/main`), clean.
Step agents run with cwd = the main workspace, so **every path below is absolute** and every command
is to be run from the worktree root.

---

## 0. What exploration changed about the brief

Four findings from reading the real code. None of them cancels a task; two change how a task must be
built, and one corrects a premise.

**0.1 The worktree cannot run `compile_check.py` as checked out.** `*.csproj` is gitignored, so
`Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` are absent from the worktree and
`compile_check.py` exits with "Assembly-CSharp.csproj not found". Copying them in is not enough:
65 runtime and 77 editor `<HintPath>` entries are relative paths into `Library\PackageCache` and
`Library\ScriptAssemblies`, `compile_check` resolves a relative hint path against the *current* repo
root, and the worktree has no `Library`. Item 1 below is a one-time prep step that fixes both; it was
run during planning and the baseline is confirmed green (`Compiles clean.`, 0/0 both passes).
`PlaytestLogs/` is gitignored too, so the `playtest_row.py` check needs the log copied in as well.
Both destinations are gitignored — `git status --porcelain` was empty after the prep, verified.

**0.2 Task 3's premise is partly wrong, and the task still stands.** `VehicleController` already has
an anti-softlock path that the earlier investigation (`notes-friction.md` §7.1) missed:
`ApplyStuckRecovery(dt)` is called every physics step from `FixedUpdate` (step 8) and applies
`rightingTorque` about `Cross(transform.up, Vector3.up)` once `Uprightness < stuckUprightThreshold`
and `Speed < stuckSpeedThreshold` have held for `stuckTimeBeforeRighting`. All three profiles ship it
**on**: `autoRightWhenStuck: 1`, `stuckUprightThreshold: 0.35`, `stuckSpeedThreshold: 1.2`,
`stuckTimeBeforeRighting: 1.5`, `rightingTorque: 8` (rad/s², `ForceMode.Acceleration`, against
`maxAngularSpeed: 7`). So "there is no reset" is wrong as stated.
What is **still true and confirmed by reading the code**:
- Flip and Jump are both refused when inverted. `CanFlipNow` requires `IsGrounded`
  (`VehicleJumpFlip.cs:88`, enforced again in `TryFlip`), `flipRequiresGrounded: 1` on all three
  profiles, and `IsGrounded` comes from suspension rays cast along `-transform.up`
  (`VehicleController.ReadGround`), which point at the **sky** when the car is on its roof. The
  player's own recovery input therefore does nothing in exactly the state it is for. That is what a
  tester feels, and it is the core of Task 3.
- The automatic path has a coverage gap: at `Uprightness` between 0.35 and 0.5 the car is tilted
  ~60–70° (wedged on its side against a kerb), has no wheels on the ground, and is **above** the
  0.35 gate, so the auto-right never fires and the flip is still refused. 0.5 is the project's own
  "upright enough to drive" line (`IsUpright => Uprightness > 0.5`).
- There is no player agency and no feedback: 1.5 s of nothing reads as "stuck", and
  `flips` is `0` in all 7 logged rows, so neither the flip nor the auto-right has ever been
  observed working in a session.
- **Not verifiable from here:** whether `rightingTorque: 8` actually rolls a 900 kg box off its roof,
  and whether the car can come to rest inverted at all. Flagged in item 10; the plan does not assume
  either way, which is why the recovery it adds is deterministic rather than another torque.

**0.3 The self-right must not change a tuning value, so it gets its own serialized fields.** Every
threshold the existing auto-right uses lives in `Tuning_*.asset`, which is frozen by the hard
constraints. All new fields in this plan are `[SerializeField]` fields on the **MonoBehaviours**
(`ChaseCamera`, `VehicleJumpFlip`, `PlaytestLogger`), never on `VehicleTuning`. Scene files carry no
entries for fields that do not exist yet, so Unity deserialises them to their C# defaults: **no scene
edit is needed and none is permitted.**

**0.4 A new input action is off the table, so the Flip key gains a second meaning.**
`docs/tooling/repo-checks/test_documents.py::test_readme_controls_have_required_actions_and_bindings`
reads the `Vehicle` map out of `VehicleControls.inputactions` and asserts the README Controls table
covers it — "a control added to the asset and left out of the table fails this test". Adding a
`SelfRight` action would mean editing the shared input asset, the code-built fallback in
`VehicleInput.BuildFallbackControls`, and the README table. Decision: **reuse the existing `Flip`
action.** When the car is overturned and resting, a flip press is reinterpreted as a self-right.
That is also what a tester already tries, and it makes the literal reported defect ("flip input does
not recover it") false.

---

## The ordered plan

- [ ] **1. Prepare the worktree so the three verification commands can run.**
      Copy the two Unity-generated csproj files in with their relative `Library\…` hint paths made
      absolute into the main workspace, and copy the real shift log in. Both targets are gitignored.
      The helper already exists and was used to confirm the baseline during planning.
      Files (all gitignored, none committed):
      `…\.worktrees\reverse-recovery\Assembly-CSharp.csproj`,
      `…\.worktrees\reverse-recovery\Assembly-CSharp-Editor.csproj`,
      `…\.worktrees\reverse-recovery\PlaytestLogs\shift_log.csv`
      Commands:
      `python "c:\Users\akoba\DIGI2001\Project\GameDesign\Zoom Zoom Delivery Co\.agents\tasks\reverse-and-recovery-2026-10-14\prepare_compile_refs.py"`
      then copy `…\Zoom Zoom Delivery Co\PlaytestLogs\shift_log.csv` to
      `…\.worktrees\reverse-recovery\PlaytestLogs\shift_log.csv`.
      Verify: `python docs/tooling/compile_check.py` ends with `Compiles clean.` and prints
      `errors 0, warnings 0` for RUNTIME (342 references, 83 scripts) and EDITOR (360 references,
      18 editor-only, 5 scripts); `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks -q --no-header`
      prints `34 passed`; `python -B docs/tooling/playtest_row.py --log "PlaytestLogs/shift_log.csv" --list`
      lists 3 rows, two of them `play_s=0.0` against `shift_s=11` and `shift_s=185`;
      `git status --porcelain` prints nothing. **If any of these four is not already true, stop and
      report — the baseline is broken, not your change.**

- [ ] **2. Add the four reverse-view fields and the state machine to `ChaseCamera`, and give the
      heading an antiparallel guard (Task 1).**
      New `[Header("Reverse view")]` block after the existing `collisionLayers` field, following the
      file's `[Tooltip(...)] [SerializeField] private` convention; new private state next to
      `_yawOffset`; one new method `UpdateReverseView(float dt)` and one `ApplyReverseView(Vector3)`;
      `ComputeHeading` gains the dot-product guard; `LateUpdate` and `SnapToTarget` are rewired to
      use the view heading. No existing tuning value is read differently and none is written.
      Files: `…\.worktrees\reverse-recovery\Assets\Project\Scripts\Vehicle\ChaseCamera.cs`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, 0 errors 0 warnings both
      passes. Then item 5's play test.

      **Fields and defaults**
      | field | default | tooltip must say |
      | --- | --- | --- |
      | `reverseViewEnterSpeed` | `-1.5f` | Forward speed (m/s, negative) at and below which the camera starts swinging round to show where the car is reversing. −1.5 matches the velocity gate `ComputeHeading` already uses. |
      | `reverseViewExitSpeed` | `-0.5f` | Forward speed at and above which the camera starts coming back. The gap between this and the enter speed is the dead zone: inside it nothing changes, which is what stops the camera whipping round every time the player brakes to a standstill. −0.5 matches the controller's own reverse gate in `ApplyDriveAndBrake`. |
      | `reverseViewHoldTime` | `0.25f` | Seconds the car must stay below the enter speed before the swing begins. A brief backward roll on a slope, or overrunning the brake, is ignored. |
      | `reverseViewTransitionTime` | `0.45f` | Seconds for the full 180° swing, eased in and out. Never a snap. The existing precedent in this file is `cameraSwivelRecentreDelay` at 1.2 s for letting go of the mouse; a reverse swing has to be quicker than that or the player reverses blind while waiting for it. |
      | `reverseViewReturnDelay` | `0.35f` | Seconds above the exit speed before the camera starts coming back, so shuffling back and forth in a three point turn does not swing the camera twice per shuffle. |

      **Math.** Let `fs = target.ForwardSpeed`, and keep `_reverseTarget` (0 or 1), `_reverseBlend`
      (0…1), `_reverseHold`, `_forwardHold`.
      ```
      if      (fs <= reverseViewEnterSpeed) { _forwardHold = 0; _reverseHold += dt;
                                              if (_reverseHold >= reverseViewHoldTime)    _reverseTarget = 1; }
      else if (fs >= reverseViewExitSpeed)  { _reverseHold = 0; _forwardHold += dt;
                                              if (_forwardHold >= reverseViewReturnDelay) _reverseTarget = 0; }
      else                                  { /* dead zone: both timers held, target unchanged */ }

      _reverseBlend = Mathf.MoveTowards(_reverseBlend, _reverseTarget,
                                        dt / Mathf.Max(0.0001f, reverseViewTransitionTime));
      float eased  = Mathf.SmoothStep(0f, 1f, _reverseBlend);
      ```
      `MoveTowards` makes the swing take exactly `reverseViewTransitionTime` seconds; `SmoothStep`
      supplies the ease at both ends. Then
      ```
      Vector3 ApplyReverseView(Vector3 heading) =>
          eased <= 0.0001f ? heading
                           : Quaternion.AngleAxis(180f * eased, Vector3.up) * heading;
      ```
      **Why a rotation and not a sign flip.** `ComputeDesiredPosition` places the camera at
      `carPos − heading*distance`, and `ComputeDesiredRotation` aims at
      `carPos + heading*lookAhead` (28 m on Balanced, 34 m on Loose). Negating only the look-ahead
      puts the aim point 21.5 m *behind the camera*: the car leaves the frame entirely. Lerping the
      two signs through zero instead walks the camera through the car. One 180° rotation about world
      up moves the camera round the side of the car to the nose and carries the aim point to 28 m
      behind the tail — which **is** "the look-at target behind the car" — keeps the car framed the
      whole way, is continuous, and has an explicitly named axis, so it cannot be ill-conditioned.
      At `eased = 1` the player reversing at up to 8 m/s (`reverseTopSpeed`) is looking along the
      direction of travel instead of 28 m the wrong way.
      **Order of operations in `LateUpdate`** — the rotation is applied *after* the existing heading
      smoothing, never inside `ComputeHeading`:
      ```
      Vector3 desiredHeading = ComputeHeading();                       // nose space, swivel applied
      _smoothedHeading = SmoothDirection(_smoothedHeading, desiredHeading, Tuning.cameraRotationSmoothTime, dt);
      UpdateReverseView(dt);
      Vector3 viewHeading = ApplyReverseView(_smoothedHeading);
      ```
      and `viewHeading` replaces `_smoothedHeading` in the `ComputeDesiredPosition` /
      `KeepOutOfWalls` / `ComputeDesiredRotation` calls below it. `_smoothedHeading` therefore stays
      in nose space, so `SmoothDirection`'s own `Vector3.Slerp` never sees a large step and can never
      become the degenerate case this task is about.
      **The antiparallel guard in `ComputeHeading`** (defect (b)): keep the existing
      `velocity.magnitude < 1.5f` gate, and add before the blend
      ```
      Vector3 velocityDirection = velocity.normalized;
      // Slerp has no defined rotation axis between opposite vectors, and once the car is
      // travelling backwards the velocity direction tells the framing nothing the reverse
      // swing is not already saying. -0.95 is about 162 degrees.
      const float AntiparallelDot = -0.95f;
      if (Vector3.Dot(nose, velocityDirection) <= AntiparallelDot) return ApplySwivel(nose);
      ```
      then the existing `Vector3.Slerp(nose, velocityDirection, Tuning.cameraVelocityInfluence)`.
      Drifting is untouched: `driftSlipAngleThreshold` is 12°, so a normal slide is nowhere near 162°.
      **`SnapToTarget` must reset the new state** (`_reverseBlend`, `_reverseTarget`, `_reverseHold`,
      `_forwardHold` all to 0) next to the existing `_yawOffset = 0f`, and then build its pose from
      `ApplyReverseView(_smoothedHeading)`. `VehicleMeasurement.ResetCar` calls `SnapToTarget` before
      every reading (`VehicleMeasurement.cs:1016-1017`), so without this a reading taken after a
      reversing run would start with the camera half swung round.
      **Gizmo:** in `DrawSightlineGizmos`, when `Application.isPlaying`, use the reverse-rotated
      heading so the drawn sightline matches where the camera is actually looking.

- [ ] **3. Make mouse look frame-rate independent and expose its sensitivity (Task 2).**
      Depends on item 4 for the device test, so write item 4 first or write both in one pass; they
      are in two files and must compile together.
      Files: `…\.worktrees\reverse-recovery\Assets\Project\Scripts\Vehicle\ChaseCamera.cs`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, 0/0. Then report the
      before/after table below in the FEAT `findings`, recomputed from the shipped values rather
      than copied from here.

      **The defect, confirmed in code.** `ReadSwivelInput` does
      `_yawOffset += look.x * Tuning.cameraSwivelSpeed * dt;` and
      `_pitchOffset = Mathf.Clamp(_pitchOffset - look.y * Tuning.cameraSwivelSpeed * dt, -35f, 55f);`.
      `cameraSwivelSpeed` is `120` on all three profiles and `CameraLook` reaches here already scaled
      by `ScaleVector2(x=0.06,y=0.06)` on the `<Mouse>/delta` binding. So the code **does** multiply a
      per-frame displacement by `Time.deltaTime`, which is the wrong way round: degrees per pixel is
      `0.06 × 120 × dt`, i.e. the same hand movement turns the camera 2.4× further at 60 fps than at
      144. That is the real defect named in the brief and it is present as described.

      | | 30 fps | 60 fps | 144 fps | 400 px sweep at 60 fps |
      | --- | --- | --- | --- | --- |
      | before | 0.24 °/px | **0.12 °/px** | 0.05 °/px | 48° |
      | after | 0.05 °/px | **0.05 °/px** | 0.05 °/px | 20° |

      **Fields and defaults**
      | field | default | tooltip must say |
      | --- | --- | --- |
      | `mouseLookDegreesPerPixel` | `0.05f` | Degrees of camera yaw per pixel of mouse movement, at any frame rate. Mouse delta is a displacement, not a rate, so it is deliberately NOT multiplied by Time.deltaTime; the right stick still is, because a stick IS a rate. 0.05 is the top of the 0.03–0.05 band this kind of auxiliary look lands in, and it is also exactly what the old code gave at 144 fps, so the fastest machine feels no change and the 60 fps case stops being twice as fast as intended. |
      | `mouseLookDeadzonePixels` | `1f` | Mouse movement smaller than this is ignored, in pixels. The old gate was 0.0001 on the squared processed vector, i.e. 0.167 px, which a resting hand clears six times over — and any movement also restarts the auto-recentre delay, so a resting hand used to hold the camera permanently off centre. Above about 2 px this starts to feel sticky. |

      **The arithmetic.** One pixel arrives as 0.06, so
      ```
      private const float MouseBindingDeltaScale = 0.06f;  // ScaleVector2 on <Mouse>/delta:
                                                           // VehicleControls.inputactions and the
                                                           // identical fallback in VehicleInput.BuildFallbackControls
      Vector2 pixels  = look / MouseBindingDeltaScale;     // back to pixels
      float yawDeg    = pixels.x * mouseLookDegreesPerPixel;   // no dt
      float pitchDeg  = pixels.y * mouseLookDegreesPerPixel;   // no dt
      ```
      and the stick branch keeps `look.x * Tuning.cameraSwivelSpeed * dt` exactly as it is today, so
      no gamepad behaviour changes and no tuning value moves. Both branches then feed the existing
      `_yawOffset += yawDeg;` / `_pitchOffset = Mathf.Clamp(_pitchOffset - pitchDeg, -35f, 55f);`.
      Gate per device: mouse against `pixels.magnitude < mouseLookDeadzonePixels`, stick against the
      existing `look.sqrMagnitude > 0.0001f`. Factor the idle tail (`_timeSinceSwivel += dt` and the
      recentre block) into one local path so both branches share it unchanged.
      If `input` is null or the device cannot be identified, treat the input as mouse — keyboard and
      mouse is what the playtests ran on, and the stick path is the one with the safe `dt`.

- [ ] **4. Let `VehicleInput` say which device last moved the camera.**
      Needed by item 3. One auto-property and three lines in `Update`, next to the existing
      `CameraLook` assignment; `using UnityEngine.InputSystem;` is already present.
      Files: `…\.worktrees\reverse-recovery\Assets\Project\Scripts\Vehicle\VehicleInput.cs`
      ```
      /// <summary>True when the latest CameraLook value came from a mouse rather than a stick.
      /// The camera has to know, because a mouse delta is a displacement and a stick is a rate:
      /// one must not be scaled by frame time and the other must. Sticky by design — activeControl
      /// is null when the action is at rest, and the last device the player used is the right
      /// answer while nothing is moving.</summary>
      public bool CameraLookIsMouse { get; private set; } = true;
      ```
      set after `CameraLook = look;` with
      `if (_cameraLook != null && look.sqrMagnitude > 0f) { var device = _cameraLook.activeControl?.device; if (device != null) CameraLookIsMouse = device is Mouse; }`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, 0/0. The `Mouse` type comes
      from the already-referenced `Unity.InputSystem` assembly, so no reference changes.

- [ ] **5. Flag what items 2–4 cannot verify, and say so in the FEAT findings.**
      No file changes. Camera feel is not measurable statically: whether 0.45 s reads as a swing
      rather than a lurch, whether the 1 m/s dead band is wide enough in a three point turn, and
      whether 0.05 °/px feels right at the tester's frame rate are all **play-test only**. Record in
      `findings`: open `Assets/Project/Scenes/Possible game scene.unity` in Unity `6000.5.4f1`, hold
      `S` past −1.5 m/s and confirm the camera swings round once, eased, and does not swing when
      braking to a stop with `Left Ctrl`; shuffle forward/back in a three point turn and confirm it
      swings at most once; sweep the mouse and confirm the sensitivity is identical with vsync on and
      off. All five numbers are serialized, so tuning them needs no recompile.

- [ ] **6. Give `VehicleController` an additive ground probe and split the pose half out of
      `Teleport` (Task 3, support).**
      Two additive changes and one behaviour-preserving extraction. **No line in the physics path is
      modified**: no change to `ApplyDriveAndBrake`, `ApplySteering`, `ApplyLateralGrip*`,
      `ApplySuspension`, `ApplyBoost` or `ApplyStuckRecovery`.
      Files: `…\.worktrees\reverse-recovery\Assets\Project\Scripts\Vehicle\VehicleController.cs`
      - `public bool ProbeGroundBelow(float distance, out RaycastHit hit)` — one `TryRaycast` from
        `transform.position` along **world** `Vector3.down` (not `-transform.up`, which is the whole
        reason an inverted car reads as airborne), reusing the existing own-collider filter and
        `tuning.groundMask`. Read-only; applies no force.
      - `public void SetPoseAndClearMotion(Vector3 position, Quaternion rotation)` — the first half
        of today's `Teleport`: the interpolation off/on trick that clears the position history, the
        `_rb.position` / `_rb.rotation` / `transform.SetPositionAndRotation` write, zeroing linear and
        angular velocity, and `_stuckTimer = 0f` so the auto-right timer and the new recovery do not
        fight each other.
      - `Teleport` becomes `SetPoseAndClearMotion(position, rotation)` followed by the parts that are
        only right for a measurement: `Drive = default`, `SteerAmount = 0f`, `_steerVelocity = 0f`,
        and the boost resets. **The recovery must not call `Teleport`**: `Teleport` refills
        `BoostRemaining` to `boostCapacity` and zeroes `TotalBoostEarned`, which would make rolling
        the car the cheapest way to buy boost and would corrupt `boost_earned` in the shift log.
        Say that in the XML doc comment.
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, 0/0, and
      `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks -q --no-header` → `34 passed`.

- [ ] **7. Add the self-right state machine to `VehicleJumpFlip` (Task 3).**
      Depends on item 6. New `[Header("Self right")]` serialized block, new readable state, one new
      method `TrySelfRight()`, one detector `UpdateOverturnedState(float dt)`, and a dispatch change
      in `FixedUpdate` so a queued flip is spent on recovery when the car is overturned and resting.
      Files: `…\.worktrees\reverse-recovery\Assets\Project\Scripts\Vehicle\VehicleJumpFlip.cs`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, 0/0;
      `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks -q --no-header` → `34 passed`.
      Then item 9's play test.

      **Fields and defaults**
      | field | default | tooltip must say |
      | --- | --- | --- |
      | `selfRightEnabled` | `true` | Lets the flip key recover an overturned car. Off restores the old behaviour exactly, where the only route back is the automatic righting torque in VehicleController. |
      | `selfRightUprightThreshold` | `0.5f` | Dot of the car's up against world up, below which the car counts as overturned. 0.5 is the project's own "upright enough to drive" line (`VehicleController.IsUpright`), and it deliberately catches the wedged-on-its-side case that the tuning asset's 0.35 auto-right gate misses. |
      | `selfRightHoldTime` | `0.6f` | Seconds the overturned, slow, resting state must hold before a flip press becomes a self-right. Stops a press landing as a recovery mid-tumble. |
      | `selfRightMaxSpeed` | `2f` | Metres per second above which the car is still going somewhere and is not stuck. Below `driftMinimumSpeed` (4), so this can never fire out of a drift. |
      | `selfRightMaxAngularSpeed` | `1.5f` | Radians per second of spin above which the car is still rolling over and is left alone to finish. |
      | `selfRightGroundProbe` | `2f` | Metres of world-down probe for ground under the car. An airborne car has nothing within this, which is what keeps the move off the table in mid air. |
      | `selfRightRideHeight` | `0.8f` | Metres above the ground the car is placed at when it is righted. 0.8 is the spawn height every scene uses with the floor at y = 0, which leaves the documented 0.5 m of air under the body collider and lets the suspension settle it. |
      | `selfRightCooldown` | `2f` | Seconds before another self-right is allowed, so it cannot be spammed. |
      | `selfRightAutoAfterSeconds` | `3f` | Seconds of the same overturned, resting state after which the car rights itself with no input at all. Twice the tuning asset's 1.5 s righting-torque delay, so the gentle torque gets its chance first and this is only the backstop. 0 disables it and leaves the flip key as the only route back. |

      **The state machine.** One timer, one cooldown, four gates. Run `UpdateOverturnedState` from
      `FixedUpdate` before the queued-input dispatch.
      ```
      bool Overturned   = _controller.Uprightness < selfRightUprightThreshold;
      bool Slow         = _controller.Speed < selfRightMaxSpeed;
      bool NotSpinning  = _rb.angularVelocity.magnitude < selfRightMaxAngularSpeed;
      bool NotDrifting  = !_controller.IsDrifting;
      bool NearGround   = _controller.ProbeGroundBelow(selfRightGroundProbe, out _groundHit);

      if (Overturned && Slow && NotSpinning && NotDrifting && NearGround) _overturnedFor += dt;
      else                                                               _overturnedFor = 0f;

      SelfRightCooldownRemaining = Mathf.Max(0f, SelfRightCooldownRemaining - dt);

      CanSelfRightNow = selfRightEnabled
                        && _overturnedFor >= selfRightHoldTime
                        && SelfRightCooldownRemaining <= 0f
                        && !IsFlipping;
      ```
      Dispatch, replacing `if (_flipQueued && TryFlip()) _flipQueued = false;`:
      ```
      if (_flipQueued && CanSelfRightNow && TrySelfRight()) _flipQueued = false;
      else if (_flipQueued && TryFlip())                    _flipQueued = false;
      if (selfRightAutoAfterSeconds > 0f && _overturnedFor >= selfRightAutoAfterSeconds
          && CanSelfRightNow) TrySelfRight();
      ```
      `TrySelfRight()`:
      ```
      Vector3 flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
      if (flat.sqrMagnitude < 0.0001f) flat = Vector3.ProjectOnPlane(transform.up, Vector3.up);   // nose straight up or down
      if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;                                    // same fallback ReadGround uses
      Quaternion upright = Quaternion.LookRotation(flat.normalized, Vector3.up);
      Vector3    place   = _groundHit.point + Vector3.up * selfRightRideHeight;
      _controller.SetPoseAndClearMotion(place, upright);     // never Teleport: see item 6
      _overturnedFor = 0f;
      SelfRightCooldownRemaining = selfRightCooldown;
      SelfRightCount++;
      SelfRighted?.Invoke();                                 // optional, mirrors FlipStarted
      return true;
      ```
      Heading is preserved (the car faces where it was facing, flattened), velocity and spin are
      cleared, and the pose write reuses `Teleport`'s interpolation trick so the car does not smear
      across the screen.
      **Public surface, mirroring the file's existing style:**
      `public int SelfRightCount { get; private set; }`,
      `public float SelfRightCooldownRemaining { get; private set; }`,
      `public bool CanSelfRightNow { get; private set; }`, and a `Refuse("…")` line for each gate so
      the existing `logRefusals` toggle explains a refusal the same way it does for jump and flip.
      **Why it cannot fire when it must not:** airborne needs ground within 2 m *and* speed under
      2 m/s *and* spin under 1.5 rad/s held for 0.6 s; drifting needs `Speed ≥ driftMinimumSpeed` (4),
      which fails the 2 m/s gate before `!IsDrifting` is even consulted; mid-air spam is barred by the
      0.6 s hold, the 2 s cooldown, and velocity being zeroed rather than granted.
      **Do not disturb the lab:** `VehicleMeasurement` drives flips at `:553` and `:645` with the car
      reset upright and settled, so `Uprightness ≈ 1` and the self-right gate is false there — the F4
      flip measurement still reaches `TryFlip`. Confirm by reading those two call sites before
      committing.

- [ ] **8. Leave `ApplyStuckRecovery` and every tuning value exactly as they are.**
      No file changes. Stated as a plan item because it is the easiest constraint to break: the
      obvious "fix" for a car that will not roll off its roof is to raise `rightingTorque` from 8, and
      that value lives in all three `Tuning_*.asset` files. **If the play test shows the automatic
      torque cannot right the car, stop and report it; do not change the number.** The new self-right
      is the deterministic route back and does not depend on the torque working.

- [ ] **9. Flag what item 7 cannot verify.**
      No file changes. Record in `findings`: whether the car can come to rest inverted at all (its
      collider is a long flat 1.8 × 1.0 × 4.2 m box that tends to fall back on its wheels, and `flips`
      is 0 in all 7 logged rows, so this state has never been observed); whether `rightingTorque: 8`
      rolls it over before the 3 s auto self-right; whether 0.8 m of ride height is enough clearance
      in `Hard`/`Medium`, where the road sits ~0.4 m above the floor plate. Instruct the play test:
      flip the car onto its roof against a kerb, press `Q`, confirm it rights after ~0.6 s facing the
      way it was; repeat without pressing anything and confirm it rights by itself within ~3 s; then
      drive normally for 60 s with `logRefusals` on and confirm no self-right fires while jumping,
      flipping or drifting (`SelfRightCount` stays 0 and `self_right_count` is 0 in the row).

- [ ] **10. Fix both duration fields in `PlaytestLogger` on every exit path (Task 4, data integrity).**
      Do this before the new columns, so the two changes are separable if one has to be reverted.
      Files: `…\.worktrees\reverse-recovery\Assets\Project\Scripts\Vehicle\PlaytestLogger.cs`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, **0 warnings** (see the
      `_shiftStartTime` note below — this is the step most likely to introduce one), and
      `python -B docs/tooling/playtest_row.py --log "PlaytestLogs/shift_log.csv" --list` → the same
      3 rows as the baseline, unchanged.
      - **`shift_seconds` = 0.** `WriteRow` computes `shiftTimer != null ? ShiftLengthSeconds() : 0f`
        and `ShiftLengthSeconds()` reads `shiftTimer.TimeRemaining`. On the `OnDestroy` path the
        `ShiftTimer` component may already be destroyed, Unity reports a destroyed component as null,
        and the field writes 0 — which is what rows 4 and 5 of `Game Build/PlaytestLogs/shift_log.csv`
        show. Fix: **capture the length once at shift start, when the timer is certainly alive.**
        `ShiftTimer.StartShift()` sets `TimeRemaining = shiftDurationSeconds` on the line before it
        fires `ShiftStarted`, so in `HandleShiftStarted` the value is exactly the configured length.
        Add `private float _shiftLengthSeconds;`, set it there, and write it at the end.
        `ShiftTimer.cs` is in `Assets/Project/Scripts/Orders/` and is **off limits**, so a public
        getter for `shiftDurationSeconds` is not available — note that in the comment.
      - **`play_seconds` = 0.0.** `playSeconds` is `Time.time - _shiftStartTime`, and
        `_shiftStartTime` is reset by `HandleShiftStarted`, which `Awake` also calls directly when it
        finds a shift already running. A logger that starts mid-session therefore stamps
        `_shiftStartTime` with the current `Time.time` and immediately writes `play_seconds ≈ 0`
        against a clock that has 11 s or 185 s left — exactly the two rows in
        `PlaytestLogs/shift_log.csv`. Fix: **accumulate instead of subtracting.** Add
        `private float _playSeconds;`, zero it in `HandleShiftStarted`, add `_playSeconds += dt;` in
        `FixedUpdate` beside `_handbrakeSeconds`, and write that. It cannot be poisoned by a stale
        start stamp or a destroyed reference.
      - **Delete `_shiftStartTime` and `ShiftLengthSeconds()` entirely.** A private field that is
        assigned and never read is CS0414, `compile_check` only suppresses CS0649 and CS0169, and the
        gate is 0 warnings. `_recoveryStart` and its `Time.time` arithmetic stay as they are.
      - **Record a mid-shift attach.** In `Awake`'s catch-up branch, note
        `_attachedMidShift = Time.frameCount > 1;` and `Debug.LogWarning` once that `play_seconds` and
        `shift_seconds` describe only the part of the shift this logger saw. Honest rather than silently
        short.
      - **Cover the remaining exit path.** Add `private void OnApplicationQuit() { if (_shiftRunning && !_rowWritten) WriteRow(); }`.
        `WriteRow` already sets `_rowWritten` and `_shiftRunning = false`, so it stays idempotent and
        `OnDestroy` will not double-write.
      - **Correct the comment above the `OnDestroy` write**, which currently claims an early quit is
        "flagged by a shorter play_seconds than shift_seconds". After this fix that is finally true —
        say so, and say that it is what makes an abandoned run distinguishable from a completed one.
        `playtest_row.py` already warns on `shift_seconds == 0` and on `play < shift * 0.9`; after this
        change the first branch means "the logger never saw a start" and the second is the real
        truncation flag. Do not touch `playtest_row.py`.

- [ ] **11. Append the five new columns to `PlaytestLogger` (Task 4).**
      Depends on item 7 for `SelfRightCount` and on item 10 for the duration fields.
      Files: `…\.worktrees\reverse-recovery\Assets\Project\Scripts\Vehicle\PlaytestLogger.cs`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, 0/0;
      `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks -q --no-header` → `34 passed`;
      `python -B docs/tooling/playtest_row.py --log "PlaytestLogs/shift_log.csv" --list` → the same 3
      rows, still parsed correctly, proving the old header and the appended order still line up.

      **The exact new header, 20 columns, the first 15 byte-for-byte as they are today:**
      ```
      timestamp,scene,shift_seconds,play_seconds,delivered,late,value,boost_activations,
      boost_seconds,boost_earned,handbrake_seconds,wall_hits,flips,time_above_supersonic_s,
      longest_recovery_s,reverse_seconds,reverse_distance_m,seconds_below_2mps,brake_seconds,
      self_right_count
      ```
      (one line in the source, split here only to fit). Appended, never inserted: seven historical
      rows across `PlaytestLogs/shift_log.csv` and `Game Build/PlaytestLogs/shift_log.csv` were
      written in the current order, and `playtest_row.py` reads the log by header name, so appending
      keeps every existing column mapping valid.
      **Accumulators, all in `FixedUpdate` beside `_handbrakeSeconds`, all reset in
      `HandleShiftStarted`:**
      | column | field | rule | format |
      | --- | --- | --- | --- |
      | `reverse_seconds` | `_reverseSeconds` | `if (car.ForwardSpeed < -reverseSpeedThreshold) _reverseSeconds += dt;` | `"0.0"` |
      | `reverse_distance_m` | `_reverseDistance` | same gate, `+= Mathf.Abs(car.ForwardSpeed) * dt` | `"0.0"` |
      | `seconds_below_2mps` | `_secondsBelowCrawl` | `if (car.Speed < crawlSpeedThreshold) _secondsBelowCrawl += dt;` | `"0.0"` |
      | `brake_seconds` | `_brakeSeconds` | `if (car.Drive.brake > brakeInputThreshold) _brakeSeconds += dt;` | `"0.0"` |
      | `self_right_count` | `_selfRightsAtStart` | `jumpFlip != null ? jumpFlip.SelfRightCount - _selfRightsAtStart : 0`, exactly how `flips` is already done | integer |
      **Three new thresholds, serialized under the existing `[Header("Thresholds, matched to VehicleMeasurement")]`:**
      | field | default | tooltip must say |
      | --- | --- | --- |
      | `reverseSpeedThreshold` | `0.5f` | Forward speed below minus this counts as reversing. 0.5 m/s is the controller's own reverse gate in `ApplyDriveAndBrake`, so the column counts the same thing the car does. |
      | `crawlSpeedThreshold` | `2f` | Speed below which the car counts as crawling. The column is named `seconds_below_2mps`, so changing this makes the name a lie — change the name too. |
      | `brakeInputThreshold` | `0.05f` | Brake input above which the brake counts as held. This is the dedicated Brake action (Left Ctrl / gamepad B), not `S`: the open question is whether players ever find the brake that does not reverse, and only this action answers it. |
      **Header drift guard.** The header is only written when the file is new or empty, so the local
      log already on disk keeps its 15-column header while new rows carry 20 fields. Appending at the
      end means every existing column still parses (verified: `--list` reads the current file
      correctly). Add a one-time `Debug.LogWarning` when the file's first line is non-empty and does
      not equal `Header`, naming the file and saying to archive it so a new one is started with the
      full header. Read that first line inside the existing `try` block with a `StreamReader` that
      detects the byte-order mark, and compare trimmed. **Do not rewrite, move or delete the existing
      log.**
      **Console summary:** extend the existing `alsoLogToConsole` block with the five new values, in
      the file's existing phrasing style.
      **Out of scope, deliberately:** `docs/playtesting/playtest_sessions.csv` and
      `playtest_row.py`'s `MEASURED` map are not changed. Adding sheet columns is a separate decision
      with its own doc tests, and the brief's only requirement for that script is that it keeps
      working.

- [ ] **12. Run the full verification set and record the evidence.**
      No file changes. From the worktree root, in this order, and paste the real output into the FEAT
      `findings` rather than describing it:
      ```
      python docs/tooling/compile_check.py
      python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks -q --no-header
      python -B docs/tooling/playtest_row.py --log "PlaytestLogs/shift_log.csv" --list
      git status --porcelain
      ```
      Expected: `Compiles clean.` with `errors 0, warnings 0` on both the RUNTIME and EDITOR passes
      (the editor pass reporting `18 editor-only` references is correct and must not be "fixed");
      `34 passed`; three log rows listed; and `git status --porcelain` showing only the five source
      files from items 2, 4, 6, 7, 10 and 11 as modified — no csproj, no `PlaytestLogs/`, no scene, no
      `Tuning_*.asset`, nothing under `Assets/Project/Scripts/Orders/` or `Assets/Project/Scripts/UI/`.

---

## Hard constraints, restated as a checklist for the implementer

1. No change to any value in `Tuning_Balanced.asset`, `Tuning_Grippy.asset` or `Tuning_Loose.asset`,
   and no change to grip, steering, throttle, brake, mass, reverse acceleration or reverse top speed.
   Camera, recovery and telemetry only. **If a fix seems to need a tuning value, stop and report it.**
2. Nothing under `Assets/Project/Scripts/Orders/` or `Assets/Project/Scripts/UI/` is touched. That
   includes `ShiftTimer.cs`, which is why item 10 captures the shift length instead of adding a getter.
3. No `.unity` scene is edited. Unity has scenes open. Every new field is a MonoBehaviour
   `[SerializeField]` whose C# default applies because no scene carries a value for it, so no scene
   change is needed. If something appears to need one, report it as a required manual step.
4. `SurfaceKind` in `SurfaceType.cs` is not reordered and nothing is inserted into it. `Concrete` was
   appended deliberately. Nothing in this plan touches that file.
5. No hand-placed prop or building size changes. Nothing in this plan touches a scene or a mesh.
6. Never set `GIT_LFS_SKIP_SMUDGE=1`.
7. Commit locally on `fix/reverse-and-recovery`; never push. Small commits, subject ≤ 72 chars, body
   saying why (`docs/GIT-RULES.md` rules 2, 3 and 6).
8. `Assembly-CSharp*.csproj` and `PlaytestLogs/` are gitignored and must never appear in a commit —
   `git diff --cached --name-only` before each commit, per `docs/GIT-RULES.md` rule 4.

## What no amount of reading can settle

Flagged rather than assumed, because nothing here opens Unity:
- Whether the reverse swing, its 0.45 s ease and its 1 m/s dead band feel right (item 5).
- Whether 0.05 °/px is the right sensitivity at the tester's frame rate (item 5). The frame-rate
  dependence itself is settled: it is in the code and the arithmetic is in item 3.
- Whether the car can rest inverted at all, and whether `rightingTorque: 8` rights it (items 8, 9).
- Whether 0.8 m of ride height clears the road surface in `Hard` and `Medium`, where the city sits
  ~0.4 m above the floor plate (item 9).
- Whether the two `play_seconds = 0.0` rows came from a mid-session component add or another route to
  the same reset. The *mechanism* is proven from the code; the trigger is not, and item 10 fixes the
  field for every trigger rather than for one guess.
