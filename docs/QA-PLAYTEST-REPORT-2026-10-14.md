# Zoom Zoom Delivery Co — QA and Playtest Analysis

**Assessment date:** 2026-10-14  
**Source reviewed:** working tree originally inspected at `c03c220`. Pre-existing changes were preserved.  
**Executable tested:** `Game Build/Zoom Zoom Delivery Co.exe`; exact assembly fingerprint and fresh-session evidence are in [QA-AI-20261014-01](playtesting/QA-AI-20261014-01.md). The executable's source commit is unverified.  
**Test classification:** static review plus a Windows standalone smoke/playtest, project-provided compile and scene validation, vehicle-lab data, and historical session evidence. Findings identify whether they were confirmed live, confirmed statically, or remain a risk.

## Issue summary

| ID | Finding | Severity | Priority | Confidence |
| --- | --- | --- | --- | --- |
| QA-001 | Serialized tuning/UI data contains fields no longer represented by current scripts | Major | P1 | Confirmed statically |
| QA-002 | Orders can still resolve after the shift ends, while the result screen freezes its totals | Major | P1 | Confirmed statically |
| QA-003 | The shipped game scene does not attach the ticket-strip UI, leaving no persistent live-order timer/list | Major | P1 | Confirmed live and statically |
| QA-004 | Game has no audio assets or scene `AudioSource` components | Major | P1 | Confirmed statically |
| QA-005 | README launch instructions contradict the actual build scene order | Minor | P2 | Confirmed statically |
| QA-006 | Pause is mouse-button-only; Escape does not pause the running build | Minor | P2 | Confirmed live and statically |
| QA-007 | Pause UI is visually obscured by live destination markers and the boost HUD | Minor | P2 | Confirmed live |
| QA-008 | Controls screen omits boost, jump, flip, dedicated brake, camera and gamepad bindings | Major | P1 | Confirmed live and statically |
| QA-009 | Build order deadlines differ from the reviewed tuning asset | Major | P1 | Confirmed discrepancy; cause unverified |

## 1. Executive summary

### What the game is

*Zoom Zoom Delivery Co* is a single-player arcade courier game. The player drives a car around a compact greybox city, automatically picks up orders by entering a pickup zone, and automatically delivers them by entering the matching drop-off zone. The intended pressure comes from a five-minute shift, automatic order spawns, a three-order active cap, a one-slot cargo hold, and per-order delivery clocks.

The intended desktop player flow is **Main Menu → Play → Possible game scene**. `MainMenu.unity` and `Possible game scene.unity` are the two enabled Build Settings scenes. Controls target keyboard/mouse and gamepad; the project is best treated as a Windows/PC prototype. Android and mobile-render-pipeline settings exist, but the game has no touch driving UI or documented mobile control scheme.

### Core loop

1. Begin a 300-second shift from the menu.
2. Drive to a spawned pickup beacon; entering its 6 m zone collects it if the single cargo slot is free.
3. Follow the destination marker/beacon to the drop-off; entering its 6 m zone resolves the delivery before its timer expires.
4. Drive quickly but retain control: throttle, brake, handbrake drift, jump/flip recovery, and earned boost are all supported.
5. Repeat while new orders spawn every 12 seconds, up to three live orders.
6. Read delivered/late/value results and restart or quit at shift end.

### Overall quality assessment

**Playable prototype with clear core systems; not release-ready.** The Windows standalone launches successfully, starts a five-minute delivery shift, spawns/ages orders, exposes pause/restart/results flow, and did not emit a runtime script exception during the observed session. The game has a coherent mechanical foundation: explicit input actions, vehicle tuning, camera collision handling, real delivery anchors, markers, a results screen, a pause menu, telemetry, and a dedicated vehicle-lab measurement scene. The earlier compile check covered 80 runtime and 5 editor scripts and passed, and both enabled scenes passed structural YAML validation. Sustained driving and delivery-route completion were not verified by this tester.

The highest risk is not basic compilation; it is player-facing clarity and configuration integrity. The active scene does not attach the more informative ticket-strip UI, audio is wholly absent, and old serialized data no longer maps cleanly to current scripts. Those issues make it difficult to judge the intended “automatic pressure plus satisfying recovery” design question with confidence.

### Address first

1. Repair the serialized-data/script contract and re-save the affected scene/assets.
2. Make the end-of-shift state authoritative: stop player/order interaction or prevent all post-shift resolutions.
3. Restore a persistent, readable order queue/timer UI, fix HUD/overlay layering, and add a concise onboarding overlay.
4. Add the minimum audio feedback set: engine, skid, impact/landing, order spawn/pickup/delivery/late.

## 2. Test coverage

| Area | Coverage and result |
| --- | --- |
| Project structure and documentation | Inspected README, build log, playtest protocol/questions, credits, scene tooling, package manifest, project settings, input configuration, playtest log, and vehicle measurements. |
| Scenes | Inspected `MainMenu.unity`, `Possible game scene.unity`, `VehicleLab.unity`, and the non-build `Kyuri's Scene.unity`. Build Settings enables Main Menu and Possible game scene; VehicleLab is disabled. The two enabled scenes passed `validate_scene.py` with no duplicate anchors, dangling local references, orphan transforms, or broken hierarchy links. |
| Runtime code | Reviewed vehicle, order/cargo, zone, shift, HUD/marker, results, menu/pause, logging, weather, and audio-related implementation. Project-provided compile check passed: 80 runtime scripts and 5 editor scripts, 0 errors/warnings. |
| Input and UI | Reviewed `VehicleControls.inputactions`, UI input actions, EventSystems, menu button bindings, and game-scene button bindings. Keyboard/mouse and gamepad driving controls are wired; no pause action is defined. |
| Windows standalone playtest | Launched `Game Build/Zoom Zoom Delivery Co.exe` (Unity 6000.5.4f1, Direct3D 12 on an NVIDIA RTX 4050). Observed a clean 05:00 shift restart, automatic order spawning/late resolution, destination markers, HUD, mouse pause/resume, restart, and the end-of-shift results panel. Escape did not open pause. The observed completed shift displayed 6 delivered, 10 late, and 216 value; it is a system/result-screen check, not a balance result attributable to a controlled solo route run. |
| Level and timing | Six pickups and six drop-offs are wired to `OrderManager`. The repository's modeled route calculation reported 36/36 pairs within an assumed 50 s budget at 20 m/s, with 260–954 m approach-plus-delivery routes. This is a model, not a measured route traversal. The executable logs 45 s deadlines, so that calculation does not validate the supplied build's balance (QA-009). |
| Fresh session | `QA-AI-20261014-01` covers Main Menu → Controls → Main Menu → Play, order expiry, Escape, pause/resume and UI evidence. It has its own screenshots and build fingerprint; the tester is explicitly identified as AI-operated QA. |
| Vehicle evidence | Reviewed historical VehicleLab CSV. Its latest recorded run (2026-10-07) measured 32 m/s top speed, 18.1 m stopping distance from 31.4 m/s, a 40.1 m/s boost peak, 3.0 s full boost duration, and a passing camera-road-ahead check. This is historic lab evidence, not a new run. |
| Prior play evidence | One raw 300 s shift log exists (2026-10-12): 9 delivered, 7 late, 61 value, 7 wall hits, 0 boost activations, 0 handbrake seconds, and 0 flips. It lacks tester identity/build context and `playtest_sessions.csv` contains only its header, so it cannot support player-experience conclusions. |

### Not tested or not independently verified in play

The supplied Windows build was used for the live test. The automation available for this assessment sends discrete key presses rather than continuous analogue/key holds, so a representative sustained drive through a selected pickup and matching drop-off could not be independently completed without compromising the test. Pickup/delivery events visible in `Player.log` are therefore logged as runtime evidence, not represented as the tester's own route completion.

Main Menu → Controls → Main Menu → Play has now been verified in the fresh recorded session. Unverified or only partially verified: full keyboard driving feel under sustained steering/throttle, boost/handbrake/jump/flip feel, collision/recovery fairness, camera orbit, gamepad behavior, frame rate/memory, resolution/ultrawide layout, audio listening, and Android/mobile behavior. Brief vehicle-key pulses were inconclusive; they do not establish an input defect or a successful mechanic test.

## 3. Detailed bug reports

### QA-001 — Serialized configuration has drifted from the active scripts

- **Severity / priority:** Major / P1
- **Area:** order, cargo-condition, HUD, and tuning serialization
- **Reproduction (static):**
  1. Open `Assets/Project/Scripts/Orders/OrderTuning.cs` and compare its serialized fields with `OrderTuning_Balanced.asset`.
  2. Open `Possible game scene.unity` and compare the `CargoCondition` and `OrderHUD` component YAML with their current C# fields.
  3. Load and re-save these assets/scenes in the current editor version.
- **Expected:** every intended balance/UI setting is represented by a current serialized field, shown in the Inspector, and preserved on re-save.
- **Actual:** the order tuning asset retains fields such as `whyThisSetup`, `enableMultiLegOrders`, and transfer-leg settings that are absent from `OrderTuning.cs`; conversely, the current `valuePerMetre` field is not serialized in the asset. The game scene retains obsolete CargoCondition fields (`hardBrakeDecelThreshold`) and obsolete OrderHUD fields (`headerFontSize`, `orderLineSlots`, colour fields) that current scripts do not declare. Unity will ignore obsolete fields and use current defaults where a replacement is absent.
- **Evidence:** direct script-versus-YAML comparison. Current `CargoCondition` computes its braking threshold from `VehicleTuning.brakeDeceleration × hardBrakeMultiplier`, rather than reading the scene’s old `hardBrakeDecelThreshold: 18`. Current `OrderHUD` no longer declares the serialized ticket/list fields present in the scene.
- **Likely cause:** a code refactor renamed/removed serialized fields without a migration path (`FormerlySerializedAs`, migration editor script, or deliberate reauthoring).
- **Recommended fix:** decide the intended production settings, add migration attributes/scripts where continuity matters, remove obsolete serialized data by reauthoring the components/assets, and add a CI/editor validation that flags unknown serialized fields. Re-run vehicle and order-balance tests after the migration.

### QA-002 — Shift end does not prevent post-shift deliveries

- **Severity / priority:** Major / P1
- **Area:** shift state, zone detection, scoring/results
- **Reproduction (code-path):**
  1. Start a shift carrying an order.
  2. Let `ShiftTimer` reach 00:00.
  3. Drive into the carried order’s drop-off zone while the result panel is displayed.
- **Expected:** after the shift ends, either gameplay is frozen or no order/cargo/score system can resolve a delivery; all player-facing counters remain consistent.
- **Actual:** `ShiftTimer.EndShift()` only disables the `OrderManager` component. `ZoneDetection` continues updating, calls `CargoSystem.TryDeliver`, which calls the public `OrderManager.ResolveDelivery` method even when that component is disabled. `ShiftResults` intentionally ignores later resolutions, but `OrderHUD` continues to count them and cargo is cleared. This creates divergent state after the visible end-of-shift result.
- **Evidence:** `ShiftTimer.cs`, `ZoneDetection.cs`, `CargoSystem.cs`, `OrderManager.cs`, and the explicit comment in `ShiftResults.cs` describing this case.
- **Likely cause:** end-of-shift behavior was split across systems without a shared “shift finished” guard.
- **Recommended fix:** add a single `CanResolveOrders`/game-state gate checked by zone detection and cargo delivery, then disable vehicle input or set an explicit results state at shift end. Add a play-mode test: expire a shift while carrying cargo, enter the drop-off, and assert delivered/late/value/cargo/HUD/results cannot diverge.

### QA-003 — Live-order priority information is absent from the shipped game scene

- **Severity / priority:** Major / P1
- **Area:** onboarding, order HUD, routing decisions
- **Reproduction (live/static):**
  1. Inspect components attached to `Possible game scene.unity`.
  2. Confirm `OrderTicketStrip.cs` exists in the project but has no attached instance in the scene.
  3. Inspect current `OrderHUD.cs`: it renders shift, cargo, delivered/late/value, and optional XP; it does not render the live order list/timers that its class comment says moved to the ticket strip.
- **Expected:** while up to three orders run concurrently, the player can persistently read each order’s identity, pickup/delivery state, remaining time, and urgency to make informed routing choices.
- **Actual:** the scene has world beacons and screen-space destination markers, but no ticket strip. Markers label order ID and distance; they do not provide a persistent comparative list of remaining times. In the standalone, concurrent marker arrows accumulated while the top-right HUD only showed shift/cargo/delivered/late/value; no order-card countdown or route priority information appeared. The README says an order list is present, which further obscures the gap.
- **Evidence:** scene attachment inventory, `OrderHUD.cs`, the absence of `OrderTicketStrip` from `Possible game scene.unity`, and live Windows-build observation with three simultaneous marker arrows.
- **Likely cause:** the new HUD was authored but not added/wired in the game scene after the older HUD list was removed.
- **Recommended fix:** attach and visually validate `OrderTicketStrip`, using the supplied `HudSkin`, and test at 1280×720, 1920×1080, ultrawide, and 3 active orders. If it is intentionally out of scope, restore a compact list with countdowns to `OrderHUD` and correct the README.

### QA-004 — The game has no implemented audio feedback

- **Severity / priority:** Major / P1
- **Area:** audio, game feel, accessibility
- **Reproduction (static):** search `Assets` for common audio formats and the enabled scenes for `AudioSource`/`AudioClip` references.
- **Expected:** core driving and delivery events have audible feedback, including at minimum engine state, sliding/collision/landing, order arrival, pickup, delivery, late failure, and UI selection.
- **Actual:** no `.wav`, `.mp3`, `.ogg`, `.aif`, or `.aiff` assets were found, and neither enabled scene contains an `AudioSource` or `m_AudioClip` reference. The project’s own asset-source document explicitly notes that the car is silent.
- **Evidence:** asset/scene search; `docs/Free Asset Sources.md` also documents the omission.
- **Likely cause:** audio is planned but has not entered implementation.
- **Recommended fix:** implement a small audio manager/mix hierarchy and begin with the four sounds the project documentation already prioritizes: engine loop, tyre skid, impact, and landing; add delivery UI sounds at the same time. Provide independent master/SFX/music sliders and captions/visual reinforcement for critical events.

### QA-005 — Run instructions contradict Build Settings

- **Severity / priority:** Minor / P2
- **Area:** documentation, build/run workflow
- **Reproduction:** compare README “How to Open” text with `ProjectSettings/EditorBuildSettings.asset`.
- **Expected:** the instructions state the same initial scene and player flow that a build uses.
- **Actual:** README says `Possible game scene.unity` is the first build scene and that a built player opens it directly. Build Settings actually puts `MainMenu.unity` at index 0 and `Possible game scene.unity` at index 1. The menu’s `PlayGame()` hardcodes scene index 1, so the current menu flow works but is brittle if scene order changes.
- **Evidence:** README, Build Settings, and `MainMenuManager.cs`.
- **Recommended fix:** update README to document Main Menu → Play, and load the gameplay scene by a named constant/addressable or verified build index rather than a bare `1`.

### QA-006 — Pause is not available through a dedicated keyboard/gamepad action

- **Severity / priority:** Minor / P2
- **Area:** controls, accessibility
- **Reproduction:**
  1. Start a Windows-build shift and press Escape.
  2. Observe that the shift continues and no pause overlay opens.
  3. Inspect `VehicleControls.inputactions`, `ButtonsUI.cs`, and the game-scene UI button bindings.
- **Expected:** pause is available through a standard keyboard and gamepad input and is discoverable.
- **Actual:** `ButtonsUI` exposes only button-click methods; the game scene wires a mouse-clickable PAUSE button. The vehicle input map has no Pause action, and `PauseMenu.cs` is empty. In the running build, Escape left gameplay active; only the lower-left pointer-clickable PAUSE control opened the menu. A player using only a controller or keyboard has no defined pause path.
- **Evidence:** live Windows-build test, input action map, and scene `OnClick` wiring.
- **Recommended fix:** add a Pause action (Escape/Menu/Start), show it in the controls screen, set a first-selected UI element for controller navigation, and test pause/resume/restart from both input families.

### QA-007 — Pause UI is not visually isolated from active gameplay HUD

- **Severity / priority:** Minor / P2
- **Area:** pause UI, HUD composition, readability
- **Reproduction:**
  1. Start a shift and wait until multiple destination markers are active.
  2. Click the lower-left PAUSE button.
  3. Inspect the pause dialog and lower-left HUD.
- **Expected:** the pause state presents an uncluttered, visually authoritative dialog; transient delivery markers should be hidden or placed behind an opaque overlay, and the pause button should not compete with the boost display.
- **Actual:** live blue/pink destination markers and distance labels render over the central pause dialog, crossing the RESTART label. The lower-left boost bar also overlaps the PAUSE control, making its label only partly readable.
- **Evidence:** live Windows-build pause test at 1536×864. The pause action correctly froze the clock at 04:07 for a ten-second observation, so this is a presentation/layering failure rather than a failed pause implementation.
- **Likely cause:** runtime marker and boost canvases are sorted above the scene-authored pause canvas, with both the BoostGauge and PAUSE button anchored to the same lower-left area.
- **Recommended fix:** assign explicit canvas sorting orders; hide or disable marker/HUD canvases while paused; reserve non-overlapping safe areas for pause and boost UI; then verify at 720p, 1080p, and ultrawide.

### QA-008 — The Controls screen omits implemented mechanics

- **Severity / priority:** Major / P1
- **Area:** onboarding, input documentation
- **Reproduction:** launch the Windows build and select Controls.
- **Expected:** the screen explains implemented player actions and relevant keyboard/gamepad bindings, especially the boost and recovery tools central to the design.
- **Actual:** only W acceleration, A/D steering, S brake/reverse and Space handbrake are listed. Left Alt boost, Left Shift jump, Q/direction flip, Left Ctrl dedicated brake, camera controls and gamepad bindings are omitted.
- **Evidence:** fresh-session screenshot `docs/playtesting/evidence/QA-AI-20261014-01/01-controls.jpg`, compared with `Assets/Project/Scripts/Vehicle/VehicleControls.inputactions`.
- **Likely cause:** manually authored instruction text has not kept up with the input configuration.
- **Recommended fix:** update the screen and add a short boost/recovery introduction. Prefer binding labels generated from the input asset; validate discoverability with first-time players.

### QA-009 — The supplied build and source tuning disagree on deadlines

- **Severity / priority:** Major / P1
- **Area:** build provenance, balance configuration
- **Reproduction:** start a fresh shift; inspect spawned-order entries in the player log; compare their time limits with `OrderTuning_Balanced.asset` in the reviewed source.
- **Expected:** the tested build's configuration is identifiable and matches the balance assumptions used in its test report.
- **Actual:** the executable logs `45 s to deliver`; the source asset specifies `orderTimeLimit: 50`. The previous route-model claim used 50 seconds and cannot certify this build.
- **Evidence:** runtime log for QA-AI-20261014-01; `Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset:33`; the active source scene references that asset's GUID.
- **Likely cause:** unverified. Possibilities include an older/different built configuration or asset-import/default-value behavior. A stale build is not proven solely by the mismatch.
- **Recommended fix:** inspect the effective Inspector value, reconcile the intended deadline, rebuild, and embed a build identifier and effective tuning values. Repeat route timing on that identified build.

## 4. Gameplay and design feedback

### Controls and game feel

The mechanical palette includes separate throttle/reverse and brake, rear-only handbrake behavior, boost earned through drifting/airtime, jump, directional flip, camera swivel, wall-aware chase camera, and automatic recovery. Vehicle-key pulses in the fresh session were inconclusive, and sustained steering/throttle could not be assessed with the available discrete-input automation. Historical lab data recorded 32 m/s top speed, an 18.1 m stop from 31.4 m/s, and 42.8 m of road ahead against 22.1 m stopping distance in its camera test. Those figures are historical measurements, not current handling impressions.

The game needs real novice and experienced-player sessions before claiming that this is predictable in practice. The only raw shift log recorded no boost, handbrake, or flip use. That could mean the mechanics are unclear, were unnecessary in that route, or simply were not attempted; it is a priority observation to investigate, not proof of a defect.

### Onboarding, objectives, and UI/UX

The objective is easy to summarize but not fully taught in the game scene: enter a pickup area, then the matching delivery area before time expires. The current palette uses blue for pickup and green for delivery, with urgency blending toward red. The live build shows numbered beacons and off-screen arrows, but no explanatory legend or per-order clock. The menu's Controls screen omits several mechanics (QA-008), and the missing ticket strip removes the clearest way to compare deadlines. Keep the 6 m discs and bright beacons, but pair them with a short first-shift explanation and a readable priority queue.

The results panel is a good closure mechanism, but QA-002 must be fixed so it is an authoritative end state. The pause UI should not depend on the mouse.

### Difficulty, pacing, reward, and progression

The earlier route model assumed 50-second deadlines and a constant 20 m/s average. The tested build reports 45 seconds (QA-009), so its actual approach, braking, turning and recovery allowances need fresh route measurements. The expiry scenario establishes that uncollected orders count as late; it cannot establish fairness or a human difficulty curve.

The one saved shift resolved 16 orders in five minutes (9 delivered, 7 late). That 44% late rate and 7 wall hits are worth investigating in a controlled cohort, but the single unversioned row cannot establish balance. The current game scene uses one cargo slot; without the live ticket queue, players cannot make the trade-offs that make that limitation interesting. Progression assets/classes exist but no `PlayerProgression` component is attached to the game scene, matching the documentation that persistent currency/XP progression is not yet in the build.

### Level, visual, audio, and accessibility feedback

Six named pickup and six drop-off anchors are intentionally spread around the city. The structural and distance checks are strong evidence of thoughtful route setup, but not of actual navigation readability, collision fairness, or visual obstruction. Run route tests from car spawn, each pickup, each drop-off, and after a crash/recovery.

Visual feedback is strongest in the functional markers and camera, but the presentation is still greybox/prototype-level. No audio means speed, skids, impacts, pickup, delivery, late status, and UI actions lose an important feedback channel. Add visual alternatives for all audio signals, readable colour contrast, remappable controls, pause support, camera-look inversion/sensitivity settings, and reduced-motion/camera options.

### Performance, stability, and platform risks

No standalone performance or stability profiling was possible. The active PC quality level uses URP and 120 Hz vehicle physics; it needs profiling on target-spec hardware with three active orders, marker pools, skids, collisions, and the full city. Avoid treating the open-editor memory footprint as player performance data. Mobile render assets and Android settings are present, but the absence of touch controls, device QA, build evidence, and audio/UI scaling tests means mobile is not ready to claim as a target.

## 5. Prioritized action plan

### Quick wins

1. Fix QA-002: make shift end stop all delivery resolution and player driving, then automate the regression case.
2. Fix QA-003 and QA-007: attach/wire the ticket strip (or restore a timed order list), isolate pause from gameplay HUD, and add a first-shift controls/objective panel.
3. Correct README/build instructions and replace the hard-coded menu scene index.
4. Add Pause input for Escape and controller Menu/Start, including controller UI focus.
5. Add minimum SFX and a master/SFX volume control; ship visual feedback alongside each key sound.

### Larger production work

1. Complete a serialization migration pass (QA-001), reauthor affected assets/scenes, and add validation for stale fields.
2. Expand the Windows-build playtest into a structured matrix: keyboard and gamepad; new and experienced driving players; 720p/1080p/ultrawide; normal, collision/recovery, late-order, boost, handbrake, flip, pause, restart, results, and menu exit paths.
3. Recruit the documented minimum five testers across the same version/variant, capture their logs, and populate `docs/playtesting/playtest_sessions.csv` with build identifiers and questionnaire responses.
4. Profile CPU/GPU/memory and frame pacing in a player build; then decide whether mobile is in scope and build a real mobile-control/accessibility plan before supporting it.

## Verification record

- Project version: Unity `6000.5.4f1`.
- Compilation: project-provided `compile_check.py` passed runtime (80 scripts) and editor (5 scripts), 0 errors and 0 warnings.
- Scene structure: `MainMenu.unity` and `Possible game scene.unity` passed the repository scene validator.
- Delivery timing: the repository model reports 36/36 pairs at an assumed 50 seconds and 20 m/s; this is not proof of completion in the tested 45-second build.
- Standalone validation: `Game Build/Zoom Zoom Delivery Co.exe` launched successfully. Live coverage confirmed shift start/restart, automatic order pressure, marker/HUD display, mouse pause/resume, Escape non-response, and end-of-shift results. No runtime C# error was present in the observed `Player.log` startup/session output.
