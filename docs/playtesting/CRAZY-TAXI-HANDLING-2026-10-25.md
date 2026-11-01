# Sharp-turn and acceleration team playtest — 25 October 2026

Branch: `codex/crazy-taxi-handling-playtest`.

This branch includes the latest local vehicle/input/gearbox improvements and the published
`feature/vehicle-camera` branch, including current `main`. It is intended for team testing
before merging the handling changes into the shared game.

## What changed

| Setting | Previous | Team test |
| --- | --- | --- |
| Low-speed steering rate | 125 degrees/s | 160 degrees/s |
| Cruise-speed steering rate | 58 degrees/s | 105 degrees/s |
| Steering input response | 14/s | 18/s |
| Yaw acceleration | 12 rad/s² | 20 rad/s² |
| Normal lateral grip | 38 m/s² | 65 m/s² |
| Gear thrust multipliers, first to fifth | .58 / .40 / .36 / .32 / .30 | .78 / .68 / .60 / .54 / .50 |
| Shift duration | .22 s | .16 s |
| Minimum thrust retained during shifts | 12% | 38% |
| Loaded hold after a shift | .65 s | .70 s |

Normal cruise remains 28 m/s (100.8 km/h). Stronger higher gears reduce the weak pull after
launch, and shorter shifts retain more power while keeping audible and visible gear changes.
Higher grip lets the travel direction follow the tighter steering. All three saved profiles
and newly created settings receive the changes. Custom steering curves have matching endpoints;
custom-curve selection remains an authoring choice.

These are project tuning choices inspired by the requested Crazy Taxi feel, rather than
measured Sega constants. Drift, dash, limiter-cut and quick-spin settings retain their values.

## Team test

Fetch the remote branch and check out `codex/crazy-taxi-handling-playtest`. Use Unity
6000.5.4f1. Start with `Assets/Project/Scenes/VehicleLab.unity`, then drive the same route in
`Assets/Project/Scenes/Possible game scene.unity`.

1. Hold W or R2 from rest. Check whether takeoff is immediate and the car keeps pulling through
   the higher gears without the previous long pauses. Repeat with half R2 travel.
2. Take the same right-angle corner at roughly 40, 70 and 100 km/h. Try both directions and
   partial steering. Record whether the car follows the corner, cuts too far inside, or slides wide.
3. Release the steering after a corner and accelerate away. Check for unwanted fishtailing.
4. Test brake-to-reverse, a gear-flick drift, a dash out of the drift, and quick turnaround.
5. Record device, scene, commit, approximate entry speed and whether turning or acceleration
   feels too strong or too weak. Subjective feel needs human driving feedback.

The automated validator is available at **Tools > Zoom Zoom > Validate Crazy Taxi Controller**.
It checks actual travel radius at 12, 20 and 28 m/s, momentum retention, saved profiles,
acceleration, automatic gears, braking, techniques, input and camera behaviour.

## Automated results

Unity 6000.5.4f1 passed all 90 controller checks on 25 October 2026. On the flat test floor,
full steering for .8 seconds with no throttle gave these travel-path radii:

| Entry speed | Previous radius | New radius | Exit speed with new settings |
| --- | --- | --- | --- |
| 12 m/s (43 km/h) | 8.15 m | 5.47 m | 9.82 m/s |
| 20 m/s (72 km/h) | 16.60 m | 10.40 m | 17.75 m/s |
| 28 m/s (101 km/h) | 29.87 m | 16.67 m | 25.73 m/s |

These are short corner-path measurements, including steering buildup, rather than steady-state
turning circles. They show a 33–44% tighter path while the vehicle stays upright.

| Full-throttle acceleration | Previous | New |
| --- | --- | --- |
| 0–40 km/h | 1.042 s | .692 s |
| 0–60 km/h | 1.775 s | 1.033 s |
| 0–80 km/h | 2.567 s | 1.450 s |
| 0–100 km/h | 5.358 s | 2.942 s |

The comparison uses the preceding five-speed settings and the new settings in the same Unity
physics harness. It includes four upshifts and retains the 100.8 km/h cruise cap. Automated
checks establish behaviour; the team still needs to judge controller feel on the actual route.

The actual game scene also passed its automated Play Mode driving check: six recognised
techniques, proportional trigger inputs, automatic reverse/forward pedal changes, brake-held
free revving and zero runtime errors. This was a headless functional check; it does not assess
rendering or physical gamepad feel.

Evidence: [90 controller checks](evidence/crazy-taxi-handling-2026-10-25/unity-validation.txt),
[turn radii](evidence/crazy-taxi-handling-2026-10-25/turn-radius-comparison.csv),
[acceleration](evidence/crazy-taxi-handling-2026-10-25/acceleration-comparison.csv), and
[game scene check](evidence/crazy-taxi-handling-2026-10-25/game-smoke.txt).
