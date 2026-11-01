# Five-speed gearbox: sustained pull, audible shifts and engagement feedback

24 October 2026. Current design; supersedes the forward gearbox in the earlier speed-feel record.

## Recorded problem

The player's new `controller_20261024_175014_952.csv` contains 321 samples over 33.096 seconds,
with maximum speed 100.798 km/h. Its tuning sidecar confirms the previous four-speed configuration.
In the first observed acceleration sequence, second appears at 230.313 s, third at 230.726 s and
fourth at 231.345 s. Second therefore occupies only about 0.413 seconds; third about 0.619 seconds
at this recording resolution. The earlier controlled run reached fourth in 1.358 seconds.

The criticism of short gears is supported by these observations. Adding a fifth label to that
same acceleration rush would compress the experience further. The change therefore gives the
transmission a longer power delivery cycle, a timed RPM transition, a real thrust interruption
and audible/tactile engagement, alongside five forward ratios.

## Design identity and deliberate difference from Crazy Taxi

Sega's [Crazy Taxi manual, English page 3](https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/71230/manuals/DC_CT_PC_MG_EFIGS_FINAL_lr.pdf?t=1733765150)
documents D/R selection and deliberate Dash, Drift, Back Dash and Back Drift sequences. It does
not establish this project's numbered five-speed gearbox. The five forward stages are our modern
adaptation, operating underneath the existing direction selector and advanced driving vocabulary.

The relevant lesson is contrast: normal driving should have readable pull, while a learned Dash
produces a decisive burst. The prior tune made ordinary full throttle rush through the gearbox.
This version gives ordinary pulls time to build and keeps the dedicated Dash/Back Dash/Limiter
thrust unchanged. Drift, quick spin, brake-to-reverse, corner grip and the speed-camera cues remain.
No boost tank, jump button or aerial control is introduced.

## Current five-speed tune

Normal cruise remains 28 m/s (100.8 km/h). Reverse remains 12 m/s. The base 24 m/s² forward value
is multiplied by the current gear before pedal, surface, speed taper and shift envelope factors.

| Gear | Normal full-throttle upshift threshold | Thrust multiplier | Untapered full-throttle demand |
| --- | --- | --- | --- |
| 1 | 28.2 km/h | .58 | 13.92 m/s² |
| 2 | 49.4 km/h | .40 | 9.60 m/s² |
| 3 | 70.6 km/h | .36 | 8.64 m/s² |
| 4 | 88.7 km/h | .32 | 7.68 m/s² |
| 5 | Cruise; RPM range extends to 112.9 km/h | .30 | 7.20 m/s² |

The fifth RPM range does not increase the travel ceiling. Ratios are an authored arcade speed-band
and thrust model, not a simulation of clutch plates, engine inertia and wheel-driven shaft ratios.
All bands, thrust values, RPM response and timing remain exposed in the active Taxi settings.

Each shift lasts **0.22 seconds**. Its first 35% unloads toward **12% thrust**; the remaining phase
smoothly restores thrust to 100%. RPM falls over the shift rather than snapping to the next value.
The next automatic change must wait for **0.65 seconds of loaded pull after engagement**; this
hold excludes shift duration. Downshift hysteresis still prevents hunting. Ordinary shifts cannot
reduce the dedicated thrust of Crazy Dash, Back Dash or Limiter Cut.

At approximately 85% of the shift, a separate engagement sound and a short rumble pulse confirm
drive returning. The HUD changes colour during the shift; VehicleLab distinguishes UNLOAD,
ENGAGE and PULL. Rumble targets the gamepad actively supplying player input, not any connected pad.
It stops on pause, lost focus, disabled player input/component, airborne state or teleport.
It uses ordinary controller motors; adaptive-trigger resistance is not required.

| Feedback parameter | Default | Authoring purpose |
| --- | --- | --- |
| Progressive shift | Enabled | Smooth clutch-style recovery; disable for a flat interruption |
| Shift unload fraction | .35 | Fraction of the shift before torque recovery begins |
| Shift feedback volume | .18 | Independent engagement sound level |
| Gear shift clip | Procedural fallback | Replaceable authored engagement AudioClip |
| Shift rumble low / high | .22 / .12 | Separate motor strengths |
| Shift rumble duration | .09 s | Engagement pulse length |

Engine/road loop overrides, separate levels, RPM load response and camera speed cues remain
independent. The procedural sounds are project-owned placeholders; they are not a recording of
Sega's engine or a claim of production engine-audio fidelity.

## Controlled before/after

Identical validation floor, Rigidbody, 120 Hz physics, full throttle, no steering and no technique.
The recorded four-speed motor tune was explicitly reconstructed, including its flat shift
retention and original hold timing semantics. Results are not the player's street-route times.

| Metric | Recorded four-speed tune | Five-speed tune |
| --- | --- | --- |
| 0–40 km/h | .408 s | 1.042 s |
| 0–60 km/h | .667 s | 1.775 s |
| 0–80 km/h | .883 s | 2.567 s |
| 0–100 km/h | 1.792 s | 5.358 s |
| First upshift | .442 s | .575 s |
| Second upshift | .900 s | 1.450 s |
| Third upshift | 1.358 s | 2.325 s |
| Fourth upshift | Not applicable | 3.200 s |
| Authored cruise cap | 100.8 km/h | 100.8 km/h |

The longer ordinary acceleration is intentional: each power stage can be heard and felt, rather
than five numbers flashing during a sub-two-second launch. Crazy Dash retains 60 m/s² dedicated
thrust and its existing 38 m/s ceiling. The car's fast-travel presentation still comes from road
flow, bounded FOV, sound and corner execution; increasing maximum speed is not the solution here.

## Measurement and next playtest

The subsequent brake-held free-rev update separates accelerator engine demand from wheel demand.
Both pedals keep brake priority while full gas revs to 9,000 RPM with an audible limiter; no wheel
thrust or automatic upshifts are granted. Normal road-gear RPM mapping remains at its authored
6,500 reference. This adaptation adds engine expression without changing Dash or road speed.
Revision `20261024d` appends engine demand/free-rev/limiter state (29 columns). The `c` format below
describes the preceding five-speed recording and remains valid historical evidence.

Five-speed recordings use revision `20261024c`, preserve earlier CSV columns and append
`shift_progress`, `shift_thrust_envelope` and `gear_shift_count` (26 columns total). Shift count
resets with the car/input state. Keep the accompanying tuning JSON with the CSV.
The original new player recording and sidecar are preserved unchanged in the project; copied
evidence identifies the before configuration separately from virtual-device test recordings.

Validation reports and raw before/after measurements are in the
[five-speed evidence folder](../playtesting/evidence/crazy-taxi-five-speed-2026-10-24/).
Automated checks can verify all five gears, pull intervals, RPM falls, thrust recovery and cue
dispatch. They cannot establish physical rumble strength, sound quality or perceived speed.

For the next real-controller check: run a normal full-throttle straight, lift/reapply in third,
brake into a corner and accelerate out, then repeat a launch with Crazy Dash. Listen for
pull → unload → RPM fall → engagement → renewed pull; feel whether each pulse matches engagement.
Judge the normal-versus-Dash contrast and whether fifth settles into convincing cruise. Save
both CSV and tuning JSON so the next decision can target a specific stage rather than more RPM numbers.

## Validation outcome

All 75 Unity assertions passed. Source compilation passed with zero errors/warnings (90 runtime,
8 editor scripts). Game-scene and VehicleLab Play Mode regressions passed with zero runtime errors;
the final lab's normal five-speed pull dispatched four engagement sounds and four pulses to the
virtual native DualSense. Its 255-row recording was parsed for all five gears, 26 consistent columns,
12% unloading, intermediate progressive engagement values, the revision tag and unchanged cap.
The physical controller's pulse strength and audible quality still require the player's judgement.
