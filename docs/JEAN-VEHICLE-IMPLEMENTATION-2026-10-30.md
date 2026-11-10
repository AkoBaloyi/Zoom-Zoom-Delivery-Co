# Vehicle feedback implementation — 30 October 2026

This records the first implementation pass responding to Jean's controller feedback. The earlier action plan is a pre-change review. Changes are based on main at `06fe0ff` and are delivered on `feature/vehicle-camera` for team playtesting. No submission build was produced for this pass.

## Player-facing changes

| Action | Keyboard | Controller |
| --- | --- | --- |
| Accelerate | Hold W | Hold RT / R2 |
| Brake, then reverse | Hold S | Hold LT / L2 |
| Dash | Hold W, press Left Shift | Hold RT / R2, press RB / R1 |
| Drift | Steer, press Left Ctrl | Steer, press X / Square |
| Crazy Stop | Hold S, press Left Shift | Hold LT / L2, press RB / R1 |
| Quick turn | Steer, press Space | Steer, press LB / L1 |
| Toggle camera motion effects | Press F8 | Press D-pad up |

Direct technique controls are enabled in all three tuning presets. Left Ctrl no longer also applies ordinary braking. The saved input asset and fallback bindings agree. Legacy gear-sequence recognition remains available through the tuning setting for comparison; it cannot bypass the direct Dash cooldown.

Dash can activate after sustained acceleration, including at normal cruise, or when leaving a drift. It retains the existing 0.7-second burst and 38 m/s configured ceiling, against 28 m/s ordinary cruise. These are tuning limits, not measured average speeds. Dash has a two-second cooldown starting at activation. Holding its button does not repeat it. Brake input takes priority, and airborne requests are rejected. Drift and Stop require forward movement; Drift also requires steering.

A successful Dash adds a bounded four-degree FOV envelope, a dedicated rising sound, and HUD active/cooldown feedback. The camera motion toggle disables the speed/dash camera effects. The sound is a procedural placeholder with an optional authored clip slot; its dispatch has been tested, its final mix still needs listening in game.

The menu control card and driving hints explain HOLD versus PRESS. The menu update uses the existing text-patching approach, so its actual layout must be checked at the submission resolution. Gear behavior, its existing thrust interruption, RPM display, sound and rumble are retained; this pass does not establish that Jean now understands their effect.

## Verification and remaining playtest

The Unity physics/input regression passed 184 checks, including rolling Dash, cooldown enforcement, pause/input clearing, drift exit, brake priority, airborne rejection, bounded Dash FOV, and saved/fallback input parity. Evidence is archived alongside this document under `playtesting/evidence/jean-controls-2026-10-30`.

The Play Mode check uses an unsaved, isolated vehicle lab and synthetic keyboard/controller input through Unity's Input System. It does not establish that the team city scene, menu layout, physical controller rumble or audible mix are ready for submission.

Final Play Mode result: PASS, exit code 0, zero runtime errors. It recognised Dash, Drift, Stop and Quick Turn, dispatched two Dash audio cues, and passed analog pedal, reverse/forward transition, braking and five-speed feedback checks. The temporary lab includes its own measurement component and audio listener. The editor's unrelated physics-settings serialization migration was removed after testing.

For the next human playtest, drive one fixed route using normal driving, then repeat using Dash and Drift. Record completion time, collisions, missed inputs and whether each activation was understood without watching the speed readout. Check the menu at the target resolution, try motion effects on/off, and listen for Dash against the engine sound. Ask Jean to repeat the same route before calling his feedback resolved.

This pass does not change orders, ingredient communication, GPS, objective placement or shared scenes. Those remain team coordination tasks. No new measured vehicle specification is claimed by this record.
