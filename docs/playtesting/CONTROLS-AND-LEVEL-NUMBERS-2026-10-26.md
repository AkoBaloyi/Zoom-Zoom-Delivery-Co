# Controls, recovery and level-design measurements — 26 October 2026

Team test branch: `codex/crazy-taxi-handling-playtest`.

## Driving and tricks

W / RT always requests forward driving. S / LT brakes, then reverses after a near-stop and
0.18-second dwell. W while reversing brakes before returning forward. F/R (gamepad B/A) only
send technique gestures and never switch the direction of either pedal. Automatic engine
ratios remain part of the acceleration/audio model; players never need to select them.

| Technique | Input | Condition |
| --- | --- | --- |
| Crazy Dash | F + W together, either event order | At or below 8 m/s (28.8 km/h), or exiting a drift; 0.3-second input window |
| Crazy Drift | R then F while steering | Forward speed at least 5 m/s; ordered flick within 0.3 seconds |
| Crazy Stop | S + R together, either event order | Forward speed at least 5 m/s |
| Quick turn | Hold A/D and press Space, or steer + LB | Grounded and upright; neutral steering does nothing |

Quick turn follows current steering and retains 90% of entry momentum instead of capping it
at 6 m/s before a further reduction. Collision losses are never restored by the turn. Normal
braking still works during it. The Balanced centre of mass is 0.55 m below the vehicle origin; grounded
anti-roll torque damps roll while keeping suspension, ramps and airborne motion available.

## Road reset and camera

The car records clear supported tarmac/concrete poses while driving, with a supported spawn
as the initial fallback. An upside-down car or a car trying to accelerate without making
0.75 m of progress for three seconds returns to the last safe point. Parked cars and deliberate
braking do not trigger recovery. The point must still have support and clearance for the actual
body before teleporting; an occupied point is not used. No penalty is charged until a reset
actually succeeds.

A successful reset clears motion, input and trick state, snaps the camera, displays a recovery
message and deducts five seconds from the active shift timer. The penalty can finish the shift.
Backspace / View requests manual recovery when stranded, with the same penalty. The delay and
cost are editable in the Taxi settings.

The camera resolves an overlapping aim pivot before casting to the desired view, reserves
clearance for the near plane, and moves its follow spring to the collision-safe position. A
second cast prevents corner depenetration from pushing it through another wall. It checks solid
colliders in the authored camera collision mask and ignores the player's own colliders/triggers.

## Lab method

Use **Tools > Zoom Zoom > Validate Crazy Taxi Controller** in Unity 6000.5.4f1. The test uses
the production Car prefab's collider size and centre, the production controller and its normal
120 Hz physics step, on a flat dry tarmac floor. It does not save shared scenes.

The turning-circle sweep holds 1, 2, 3, 4, 5 and 8 m/s with proportional pedal input, applies
full steering, and averages actual speed/yaw-rate radius after settling. The swept diameter
also includes the outside corners of the whole vehicle body; use that for physical clearance.

The corner sweep tests entry speeds in 1 km/h increments up to the normal cruise cap, using
full lock followed by straightening. The road is 12 m wide, with a 6 m inner radius and an
18 m outer radius. All four corners of the production collider must stay inside the approach,
quarter-circle and exit corridor. A pass also requires an 85–100 degree final heading, at
least 80% of entry speed retained, and less than 10 degrees of roll. No dash, drift or brake is
used. This measures that road and driving line; tighter streets, obstacles, slopes and other
surfaces need their own tests. It is not a universal fastest corner speed.

The top-speed curve tapers into the cap, so a time to mathematically exact top speed is not a
useful measurement. The report gives the time to 99% of the cap and the separate 0–100 km/h
time. A stop means speed below 0.1 m/s (0.36 km/h). Times exclude human reaction time.

## Measured numbers for level layout

| Measurement | Result |
| --- | --- |
| Normal top speed | **100.80 km/h (28 m/s)** |
| 0 to 99% of top speed | **2.842 s** |
| 0 to 100 km/h | **2.950 s** |
| Top speed to stopped | **0.917 s** |
| Stopping distance | **12.736 m** |
| Tightest whole-car turning circle | **5.827 m diameter** |
| Centre-path turning circle, for comparison | 2.222 m diameter |
| Fastest tested 90-degree corner, defined above | **100.80 km/h entry, 91.405 km/h exit** |

The corner sweep reached the normal cruise cap, so it establishes that this broad corner works
at normal top speed. It does not find the absolute limit for boosted driving or for a narrower
street. The turning diameter includes the actual **1.8 m × 4.2 m** car body. Leave additional
space for player error and scenery rather than using the centre-path circle as road clearance.

At 50 and 96 km/h, quick turns finished at approximately 44.8 and 86.2 km/h respectively in
both directions. Throughout the flat-floor tests, all four wheels remained supported and peak
body tilt stayed below 0.1 degrees. This is flat-floor evidence, not a claim about kerbs or ramps.

## Verification

All **114 controller checks passed** in Unity 6000.5.4f1. The actual game-scene Play Mode check
also passed with **zero runtime errors**. Real keyboard events produced Dash, Drift, Dash,
Stop and a steering-directed Quick Turn. Pressing the R trick selector left W driving forward;
the controller pedal test confirmed brake-to-reverse and returning forward without F/R.

The checks also cover simultaneous trick inputs in both event orders, neutral-steer rejection,
both quick-turn directions at 50/96 km/h, continuous wheel support/tilt during the manoeuvre,
three-second wall/overturned recovery, single time deductions and shift expiry, parking without
recovery, camera travel around an inside corner, an aim pivot overlapping a wall, saved tuning
profiles and analog trigger travel. Shared scene/prefab files were not rewritten.

Evidence: [controller checks](evidence/controls-and-level-numbers-2026-10-26/unity-validation.txt),
[game scene](evidence/controls-and-level-numbers-2026-10-26/game-smoke.txt),
[level measurements](evidence/controls-and-level-numbers-2026-10-26/level-design-numbers.csv),
and [corner speed sweep](evidence/controls-and-level-numbers-2026-10-26/corner-speed-sweep.csv).

These were headless physics and functional checks. Physical controller feel, readability in a
rendered game view, and how generous the cornering should feel still need the team's playtest.
