# Crazy Taxi identity, project differences and speed-feel tuning

> Historical four-speed pass. The subsequent [five-speed gearbox record](CRAZY-TAXI-FIVE-SPEED-GEARBOX.md)
> supersedes forward-gear counts, thrust, shift timing and acceleration results below. Pedal direction
> and camera design remain in effect.

24 October 2026 — current design after the player's recorded VehicleLab drive.

## Identity that must remain readable

The foundation is an accessible arcade vehicle with a deeper vocabulary of deliberate driving
techniques. Steering places the car decisively; a recognised drift retains travel momentum while
the chassis turns; a timed dash launches it into the next section; repeated techniques reward
practice. The automatic chase camera keeps the road and corner exit readable. Delivery efficiency
comes from route choice and execution rather than airborne control or a consumable boost tank.

Sega's [Crazy Taxi manual, English page 3](https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/71230/manuals/DC_CT_PC_MG_EFIGS_FINAL_lr.pdf?t=1733765150)
documents separate accelerator, brake and D/R inputs, plus Dash, Drift, Back Dash and Back Drift.
Those documented gestures anchor the advanced control layer. The manual does not specify the
physics or camera constants used in this project, nor establish our numbered forward-gear model.
The earlier [reconstruction record](CRAZY-TAXI-IMPLEMENTATION-2026-10-24.md) preserves the wider
research trail. Its brake-only pedal rule and initial camera defaults are now superseded here.

## What differs, and why

| Area | Reference foundation | Zoom Zoom behaviour | Reason and identity safeguard |
| --- | --- | --- | --- |
| Basic direction control | Separate brake and D/R selector in Sega's manual | S/Down/L2 brakes forward travel, then reverses after a near-stop and 0.18 s dwell; W/Up/R2 returns forward by the same rule | Makes ordinary corrections familiar and reduces button-management friction during deliveries. Explicit D/R remains for learned techniques. |
| Automatic direction events | Original techniques use deliberate selector inputs | Automatic changes cannot count as D/R gesture taps | Prevents convenience controls from granting Dash, Drift or Limiter Cut accidentally. |
| Forward gearbox | Manual documents D/R rather than our four numbered ratios | Tunable D1–D4, load-sensitive arcade RPM, shift thrust changes and minimum gear hold | Adds readable pull/rev/shift feedback without requiring manual clutch or shift controls. It remains subordinate to Dash and drift. |
| Turnaround | Reference emphasises steering and advanced gear gestures | Dedicated, bounded 180-degree quick spin; reverse remains available | User-requested decisive turnaround. Momentum cost and cooldown stop it replacing momentum-preserving drift. |
| Camera presentation | Automatic chase reference; no verified Sega numeric specification | Lower authored view, smooth 65–74 degree FOV, bounded speed distance and acceleration pullback | Gives nearby road movement more screen presence and conveys load without increasing travel speed. Stable horizon and exit framing remain. |
| Engine/road feedback | Vehicle movement is the reference, rather than an exact recovered sound model | Layered placeholder engine and speed-driven air/road sound; authored clips supported | Conveys sustained travel even at steady RPM, and distinguishes power from coasting. Separate levels keep it controllable. |
| Analog tuning | Separate accelerator and brake remain the basis | Independent deadzones, curves, response times and full-pressure technique threshold | Gives modern trigger precision without making gentle pedal use accidentally trigger techniques. |
| Recovery and cargo | Delivery game has different objectives from passenger transport | Safe upright recovery; intentional techniques protected from hard-brake cargo damage by default; collisions still damage cargo | Avoids punishing the skill system during delivery play while retaining collision consequences. |
| Measurement | Project requirement rather than a historical claim | Raw/action/motor meters, timestamped CSV, tuning sidecar | Keeps tuning decisions observable and reproducible. |

These are authored adaptations, not assertions that Sega used the same equations or lacked a
particular feature. Reverse exists in the original reference. The user's initial inability to
reverse motivated our usability work, not a historical claim about Crazy Taxi.

## What the player's recording tells us

Source: `VehicleLab_Measurements/controller_20261024_172706_067.csv` (preserved unchanged).
The file has 235 samples spanning 24.155 seconds. Hardware R2, accelerator action and applied
throttle all reach 1.0; recorded maximum speed is 100.781 km/h. Fourth gear appears in 206 samples
(87.7%). The recording therefore supports full-throttle delivery and reaching the existing cap;
it does not show a missing maximum trigger value causing the slow sensation.

The old code provides additional evidence: fixed 65-degree FOV, a 3.1 m high / 6.5 m distant view,
and one simple engine tone driven mostly by road-speed RPM. Three early shifts are followed by
a long fourth-gear plateau. Together these are plausible explanations for muted speed sensation,
not a measurement of perception. The CSV cannot establish sound quality, frame pacing, scenery
scale, corner feel or whether the player was looking at the debug panel. Those remain playtest questions.

## Before/after tuning record

| Factor | Recorded build / previous tune | Current speed-feel tune | Intended result |
| --- | --- | --- | --- |
| Normal cruise ceiling | 28 m/s = 100.8 km/h | Unchanged | Compare sensation without moving the goalposts |
| Reverse / Dash / Limiter maximum | 12 / 38 / 56 m/s | Unchanged | Preserve manoeuvre balance and existing ceilings |
| Base forward acceleration | 24 m/s² | Unchanged | Avoid another blanket acceleration increase |
| Forward shift fractions of cruise | .28 / .52 / .78 / 1.15 | .42 / .70 / .96 / 1.12 | Spread audible changes across a wider usable speed range |
| Forward gear thrust multipliers | 1.2 / 1.05 / .95 / .85 | 1.15 / 1.05 / 1 / .95 | Reduce early gear interruptions and sustain later pull |
| Shift interruption | .12 s at 20% thrust | .09 s at 55% thrust | Retain a detectable change without repeatedly killing momentum |
| Minimum interval between shifts | None | .45 s | Avoid several changes following a speed spike or Dash |
| RPM load component | None | Up to 550 RPM, tapering with road revs | Make pedal load audible beyond a speed-to-RPM counter |
| Sound | Simple periodic engine tone | Richer engine harmonics/combustion texture plus independent speed-driven road air | Convey acceleration and sustained travel; replaceable clips remain supported |
| Camera base distance / height / look ahead | 6.5 / 3.1 / 5 m | 5.6 / 2.1 / 7 m | Bring foreground flow closer while keeping useful road visibility |
| Camera FOV | Fixed 65 degrees | Smoothstep 65–74 degrees with .25 s response | Build peripheral motion gradually without sudden wide-angle kicks |
| Extra chase distance | None | Up to .6 m at cruise; up to .3 m on acceleration, eased | Add a bounded sense of pull while retaining collision avoidance |
| Brake at standstill | Brake only | Proportional reverse after dwell | Remove selector friction from basic manoeuvring |

The 120 km/h suggestion would mean 33.33 m/s and remains a separate future balancing decision.
This pass follows the request to improve fast-car sensation **without increasing top speed**.
The normal ceiling is not the technique ceiling: Dash and Limiter Cut already exceed it, unchanged.

## Controlled comparison and its limits

Unity's real Rigidbody simulation was run at 120 physics steps per second on the validation
floor, with full throttle, no steering and no recognised techniques. The previous gear parameters
were reconstructed explicitly; both configurations retained the same 24 m/s² base acceleration
and 28 m/s cruise ceiling. Results are threshold samples, rounded to milliseconds:

| Metric | Previous gear tune | Current tune |
| --- | --- | --- |
| 0–40 km/h | 0.508 s | 0.408 s |
| 0–60 km/h | 0.833 s | 0.667 s |
| 0–80 km/h | 1.150 s | 0.883 s |
| 0–100 km/h | 2.125 s | 1.792 s |
| First / second / third upshift | .283 / .650 / 1.067 s | .442 / .900 / 1.358 s |
| Maximum in the 8-second run | 100.800 km/h | 100.800 km/h |

These compare gearbox interruptions and thrust distribution under identical conditions. They
are not the player's city-drive results and do not prove improved perceived speed. Real surfaces,
contacts, steering, pedal smoothing and terrain affect the route recording. Camera and sound
improvements need an interactive judgement at the same cap.

New CSVs include direction, shift state, signed forward speed, sampled forward acceleration,
actual camera FOV and steering, plus a `20261024b` revision marker. A neighbouring `.tuning.json`
captures the active car's Taxi settings. Old CSVs remain intact. Camera tuning overrides, if
used, must be recorded separately; the CSV still logs the rendered camera FOV.

Repeat the same straight and corner route. Test full-throttle launch, lift/reapply at cruise,
brake-to-reverse, forward return, a drift followed by Dash, and the quick spin. Judge whether road
flow, load change and corner exit are clearer, and whether reverse engagement is predictable.
If it still feels slow, examine scenery scale/density and frame pacing before raising the cap.

Raw comparison and automated reports are saved with the
[speed-feel evidence](../playtesting/evidence/crazy-taxi-speed-feel-2026-10-24/).
