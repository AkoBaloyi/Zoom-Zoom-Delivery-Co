# Jean's feedback: vehicle-owner response

30 October 2026. Review and proposed implementation plan; no gameplay changes made. Based on the supplied feedback image and current project source, not a new rendered playtest. The version Jean played is not identified, so reproduce the observations in the intended submission build before claiming they are resolved.

## What the feedback asks of Ako

Jean finds basic movement, quick turn and the S+R stop responsive. His criticism is about access, legibility and usefulness: the gear sequences discourage repeated use, dash is insufficiently distinct from ordinary acceleration, and the controls screen does not clearly distinguish holding from pressing. Preserve that positive handling baseline while testing changes to input and communication.

| Feedback | Vehicle-owner contribution | Coordination required |
| --- | --- | --- |
| Drift and stop inputs are cumbersome | Add direct technique actions with ergonomic defaults; retain existing effects initially. | UI owner must update controls/tutorial displays. |
| Dash is insufficiently distinct | Dedicated successful-activation cue, modest camera response, clear mechanical eligibility and measured benefit. | UI/audio assets if another owner supplies them. |
| Automatic gears lack a readable effect | Communicate existing thrust interruption/RPM recovery; keep gears automatic and secondary. | Agree HUD emphasis with UI owner. |
| HOLD versus PRESS is unclear | Supply precise action wording and implement actual press/hold behaviour consistently. | UI owner controls layout; vehicle owner supplies binding names, conditions and success events. |
| Routes feel like point A to B | Provide comparative driving routes and measured clearance/speed evidence. | Level designer owns the city and objective placement; navigation owner owns GPS. |
| Orders arrive too quickly and markers flicker | Provide a quiet vehicle practice context and avoid adding competing visual noise. | Order/UI owners decide difficulty ramp, urgency cues, ingredients and GPS options. |

## Findings in the current source

1. `VehicleInput.cs` maps W/S/A/D, F/R technique gestures, Space quick turn and Left Ctrl as an alternate ordinary brake. The saved `VehicleControls.inputactions` asset and fallback controls both need review when changing bindings. Updating only one can leave different scenes using different controls.
2. `TaxiTrickRecognizer.cs` recognises closely timed gear/pedal sequences. Merely binding Left Shift to GearDrive will not implement Jean's suggestion: after W has been held longer than the input window, another Drive press does not reliably produce a dash.
3. `VehicleController.Taxi.cs` rejects ordinary Dash above `dashMaximumEntrySpeed` unless drifting. The Balanced settings use 8 m/s (28.8 km/h), while normal cruise is 28 m/s (100.8 km/h). An unexplained rejected input can be mistaken for a broken or weak move.
4. Configured dash thrust is 60 m/s², duration 0.7 seconds and ceiling 38 m/s (136.8 km/h). These are settings, not a guarantee the car reaches that speed during every burst. Normal acceleration is 24 m/s² before gear multipliers, taper and surface effects.
5. `CrazyTaxiFeedback.cs` subscribes to `TrickPerformed`, but confirms a trick with a text label. The status display already shows gear, RPM and a shift highlight.
6. `ChaseCamera.cs` applies speed FOV and acceleration pullback; it does not have a dedicated dash envelope. Its speed fraction clamps at ordinary cruise speed, so the normal speed FOV stops increasing beyond 28 m/s. The current 65–74° range is therefore not a distinct dash cue.
7. `TaxiEngineAudio.cs` handles engine/road loops and a gear-engagement cue. It does not subscribe to trick activation for a dedicated dash sound. `TaxiGearHaptics.cs` already provides shift rumble.
8. `TaxiAutomaticGearbox.cs` genuinely changes thrust; the gear effect is not purely audio. The design question is whether that difference needs to be more prominent to support a player decision.
9. `CrazyTaxiMenuControls.cs` patches menu text by finding a label containing `Brake/Reverse`. That is fragile if the UI owner changes the wording. Its current text describes F+W, R-then-F and S+R without a consistently explicit PRESS/HOLD convention.
10. The root `Game Build` executable has an October 14 timestamp. This does not establish which build Jean played, and timestamps alone do not prove its code version. Record the retest build explicitly.

## Priority 1: simplify access without rebuilding the movement

Recommended keyboard prototype, subject to physical keyboard testing:

| Action | Proposed teaching text | Implementation intent |
| --- | --- | --- |
| Normal drive | HOLD W to accelerate; HOLD A/D to steer. | Preserve ordinary handling. |
| Normal braking/reverse | HOLD S to brake; keep holding after stopping to reverse. | Preserve pedal assistance. |
| Dash | HOLD W, then PRESS Left Shift once to dash. | Explicit Dash action, not a fake gear sequence. One press requests one burst. |
| Drift | HOLD A or D, then PRESS Left Ctrl once to begin a drift. | Explicit Drift action using the existing timed drift effect; do not silently turn it into hold-to-drift. |
| Crazy Stop | HOLD S, then PRESS Left Shift once for a hard stop. | Same modifier as dash; brake context has priority. Preserve ordinary S alone. |
| Quick turn | HOLD A or D, then PRESS Space once to turn around. | Keep the existing successful action. |

Left Ctrl is currently bound to ordinary brake, so remove that alternate brake binding if adopting the proposed drift input. Otherwise pressing Drift would brake as well. Test simultaneous left/right steering with modifiers on ordinary keyboards. Allow rebinding if feasible; don't assume a preferred layout fits every player.

For a gamepad prototype, keep RT/LT and left stick. Use right bumper for Dash while accelerating or Stop while braking, a face button such as X/Square for Drift, and preserve LB/L1 for Quick Turn. These are proposed mappings, not existing ones. Retire or put conflicting old D/R gestures in a separate legacy preset. A preset is useful for comparison but is not equivalent to full remapping.

### Required input rules

- Accept direct technique intents in the controller using the same grounded/upright and speed checks as the existing behaviour. Do not simulate releases of the player's actual pedals to manufacture an old input sequence.
- Trigger only on a new press. Holding Shift must not repeatedly relaunch Dash or Stop.
- Resolve ambiguous modifier contexts consistently: braking beats acceleration. W+S must not produce a dash from the free-rev state. No relevant pedal means no Dash/Stop.
- No steering means no Drift or Quick Turn; give a brief useful hint in the training context rather than pretending an activation succeeded.
- Clear pending technique requests on pause, focus loss, reset and input disable as the existing controller does. Do not let a request pressed in a menu fire on resume.
- Keep input recognition separate from technique effects and presentation. A rejected request must not play the successful dash sound or animation.

### Decide what the new Dash means

The current low-speed launch/drift-exit rule does not match the broad expectation created by “press Shift while holding W”. Two honest designs exist:

**Recommended final prototype:** allow a direct Dash during forward ordinary driving up to cruise speed, plus the existing drift exit. Retain the present 38 m/s ceiling, 60 m/s² thrust and 0.7-second duration for the first comparison; do not raise all speed numbers at once. Add a clearly displayed rearm/cooldown rule so repeated presses cannot maintain indefinite boost. A two-second cooldown from activation is an initial test candidate, not a validated balance value. Reject retriggering during an active dash, and ensure old limiter-cut/legacy inputs cannot bypass the direct-mode cooldown or cause duplicate activations.

**Lower-scope alternative:** preserve the existing launch-only behaviour and explicitly teach “Dash from low speed or out of a drift”. Show why a high-speed attempt is unavailable. This is less code change, but less consistent with Jean's proposed universal-looking shortcut.

Test input simplification with existing effects first, then test the rolling-dash eligibility change separately. This preserves the ability to tell whether improvement came from easier input, clearer feedback or new power. Do not weaken normal driving merely to make Dash look stronger; Jean specifically liked basic movement.

## Priority 2: make an accepted Dash recognisable

Use the existing `TrickPerformed` event so all cues begin when the controller actually accepts the action.

1. Add a short, distinct dash onset sound above the ordinary engine loop. Keep it separate from the gear-engagement sound. It should communicate the action even when the player is watching the road rather than the HUD.
2. Add a small, transient dash-specific FOV envelope rather than increasing the general speed response. A +3° to +5° maximum additive offset is a starting experiment, not a fixed recommendation. Ease in and out, and keep the horizon stable. Avoid additional camera-distance retreat initially: earlier feedback already described the car feeling remote/light when the view moved back.
3. Use a few peripheral speed streaks only if sound and FOV are insufficient. Keep the central road and destination visible, avoid flashing, and provide a reduced-motion/off option. More effects are not automatically better, especially given Jean's complaint about visual flicker elsewhere.
4. Show ready/active/recovering states if a cooldown is added. A tiny icon can communicate availability; it must not require reading an RPM counter to decide whether Dash works.
5. Ensure camera collision clearance accounts for the largest possible dash FOV. The existing near-plane calculation considers normal speed FOV, so adding a new wider envelope without adjusting that calculation could reintroduce wall-edge clipping.
6. Clear effects on reset, disable and scene changes. Audio-off and reduced-motion modes should still leave a readable non-motion confirmation.

The mechanical check is distance gained and time saved from equal starts over a defined straight, followed by whether the player can identify successful versus rejected activation in the game view. Visual drama is not evidence that the move helps a route.

Microsoft's input guidance supports remappable actions and accurate updated button prompts; its motion guidance supports reducing/disabling distracting camera effects. These are design references, not a claim that this project meets certification requirements:
- https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/107
- https://learn.microsoft.com/en-us/xbox/accessibility/accessibility-feature-tags

## Priority 3: keep automatic gears secondary

Do not introduce manual shifting or larger power interruptions to answer this feedback. The current gearbox already changes thrust, drops RPM and has an engagement sound/rumble. The player has no gear-selection decision to make during ordinary driving, so it should support the feel of acceleration rather than compete with order decisions.

First make the existing response coherent: one brief gear-number transition, a readable RPM drop and recovery, and the existing sound/rumble at the same engagement moment. Keep gear feedback quieter than Dash and recovery feedback. If it remains unimportant to players, reduce the HUD emphasis rather than adding a driving mechanic solely to justify the gears.

## Priority 4: teach three successes in a quiet space

Provide a small vehicle practice route: straight for Dash, bend for Drift, turnaround for Quick Turn, and a safe braking marker for Stop. Teach one action at a time, with the exact PRESS/HOLD wording and immediate recognition. The lesson ends on successful action, not merely after displaying a paragraph.

Do not imply stopping is required to deliver: current zones are drive-through. Teach Stop as a way to control an approach or avoid overshooting. The lab scene currently appears disabled in build settings; it cannot be assumed to be available from the submitted game's menu without integration work.

The vehicle owner can supply the mechanics, action events, wording and practice setup. A tutorial with paused/reduced orders requires agreement with the order owner; a menu entry and final layout require UI integration. This is a concrete handover, not a reason to take over the order difficulty system.

## Priority 5: give the level designer evidence for route choices

Jean's route criticism cannot be solved by increasing car speed alone. Supply a two-route comparison: one broad, forgiving route and one shorter, more demanding route with a useful drift exit. Both should be viable with ordinary controls; techniques may improve outcomes without becoming mandatory for beginners.

Measure each route with ordinary driving, deliberate techniques and typical errors. Report time, collisions, correction time and whether exits are visible soon enough. Use the existing 5.827 m whole-car swept diameter and 12.736 m stopping distance as saved baseline evidence, with additional allowance for reaction and error. The saved broad 90° bend already works at ordinary cruise speed, so that geometry alone does not establish a need to drift.

The level/navigation owner should compare destination-only guidance and optional route guidance. Removing all guidance would ignore earlier feedback about being lost. The desired change is room to choose a route while still knowing the destination, not making players guess where work is located. This checkout's code review did not establish the GPS implementation Jean saw, so do not name a supposed route-line script as the cause without checking his build.

## Suggested implementation sequence and proof

| Order | Change | Evidence before moving on |
| --- | --- | --- |
| 1 | Record build/scene/tuning; reproduce existing controls. | Same test route and initial conditions; basic movement and quick turn retained as reference. |
| 2 | Direct actions and PRESS/HOLD prompts, existing effects. | W held for several seconds then a valid request works; no conflicting brake on drift; one press/one activation; pause/reset clears requests; keyboard and pad bindings agree with prompts. |
| 3 | Distinct successful Dash cues. | Player distinguishes actual activation from ordinary acceleration and rejected input; camera remains clear near walls at widest FOV. |
| 4 | Rolling Dash and cooldown experiment, if chosen. | Measured benefit and recovery; no indefinite boost or legacy-input bypass; normal route handling still works. |
| 5 | Practice route and actual city retest. | Player performs actions without coaching, then chooses them voluntarily where useful. Record failures and reasons, not only successes. |
| 6 | Shared route/UI/order handover. | Named owners and decisions for route choices, tutorial entry, guidance options and initial order pressure. |

Add attempted/accepted/rejected counts per technique, rejection reasons and route timings to the review instrumentation. Existing legacy boost/handbrake/flip fields cannot establish use of the current moves. An overall trick count is not enough to tell whether Dash is obvious but Drift remains unused.

Use automated checks for input precedence, acceptance gates, cooldowns, state clearing and camera clearance. Use a short human comparison for comfort, recall and voluntary use. Neither replaces the other. Mark the findings as a small formative retest rather than a statistical result.

## Design explanation for the GDD

Treat the gear-flick sequence as a research reference, not an obligation to reproduce cabinet-era ergonomics on a keyboard. The intended mastery is judging a line, timing the manoeuvre and choosing an exit while orders age. A simpler input can preserve that mastery while removing an obstacle to accessing it. If the move is still never chosen after input and feedback improve, investigate its utility in the routes rather than making its visual effects progressively louder.

## Files involved when implementation is authorised

- `VehicleInput.cs`, `VehicleControls.inputactions`, `TaxiTrickRecognizer.cs`, `VehicleController.Taxi.cs`: direct actions, input precedence and shared eligibility.
- `CrazyTaxiSettings.cs` and the active taxi settings in each tuning asset: explicit dash policy, cooldown and effect parameters; do not tune the inactive legacy boost/camera fields.
- `CrazyTaxiFeedback.cs`, `TaxiEngineAudio.cs`, `ChaseCamera.cs`: success cues, availability, distinct sound and camera envelope/clearance.
- `CrazyTaxiMenuControls.cs` plus the actual UI control-card wiring: correct instructions; replace brittle text matching with a reliable UI reference when integrating.
- `PlaytestLogger.cs`, `Editor/CrazyTaxiValidation.cs` and its measurement helpers: current-action instrumentation and focused regression checks.

All recommendations here are proposals. No game code, scene, tuning asset, build setting or teammate-owned system was changed by this review.
