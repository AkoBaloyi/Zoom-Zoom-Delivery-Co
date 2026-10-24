# Customising the controller and camera

Open the car's `VehicleTuning` asset and expand **Taxi**. All active driving and camera settings
are grouped there; chassis, suspension and visual settings follow below. The existing Balanced,
Grippy and Loose assets retain their identities for scene compatibility, but all now start with
the same taxi configuration. Duplicate an asset to make a new experimental profile, then assign
it to the car's **Tuning** field. Create a blank one with **Assets > Create > Zoom Zoom > Vehicle Tuning**.

These are custom project scripts, not an external vehicle package. Runtime tuning edits to an
asset persist after stopping Play Mode. Record a baseline or duplicate the asset before experimenting.
Camera overrides are optional; normally the camera reads the same tuning asset as the car.

## Pedals and steering input

| Parameter | Controls |
| --- | --- |
| Accelerator / brake deadzone | Trigger travel ignored near rest |
| Accelerator / brake travel curves | How remaining physical trigger travel maps to requested pedal amount |
| Accelerator rise / release time | How quickly requested acceleration rises and falls |
| Brake response time | How quickly the braking request follows trigger movement |
| Steering deadzone / travel curve | Centre tolerance and steering sensitivity across stick travel |
| Steering response | Maximum steering-command change per second |
| Pedal floor / hysteresis | Full-press recognition and the release threshold for a new technique |
| Brake trick threshold | Pedal amount that counts as braking in technique patterns |
| Input window / minimum gap | Maximum spacing and minimum deliberate spacing between technique events |

Pedal curves use **X = trigger travel after deadzone removal, Y = requested pedal amount**.
Both axes run from 0 to 1. Keep the first key at (0, 0) and the last at (1, 1) for a pedal that
rests at zero and reaches full output. A lower middle section gives finer low-speed control;
a higher middle section makes early travel more aggressive. Accelerator and brake curves are independent.
Response times of zero follow the curve immediately. Keyboard pedals still request 0 or 1, with
the authored response times providing their transition.

## Normal driving

Cruise and reverse speeds are independent. Acceleration and reverse acceleration control their
respective pull. Braking, coasting and overspeed deceleration are separate: the last controls how
dash speed falls back toward normal cruising once the technique ends.

Normal corner grip is independent of pedal use. Low/high-speed turn rates, steering authority
speed, yaw acceleration and chassis stability control placement and weight. For finer authoring,
enable the custom acceleration or steering curve. Their X axis is speed divided by the relevant
speed ceiling: 1 means normal top speed; steering keys above 1 can shape dash/limiter handling.
The acceleration curve Y axis is a multiplier; steering Y is degrees/second. Grip has a separate
speed multiplier curve. Existing surface and weather definitions multiply the resulting drive,
brake and grip forces.

## Techniques and quick turnaround

Dash speed, acceleration, duration, chaining grace time, limiter increment and maximum speed are
independent. Back Dash has a separate reverse ceiling. A ceiling ends additional propulsion;
overspeed deceleration governs the return rather than an abrupt velocity clamp.

Drift minimum speed, duration, yaw rate, grip, drag, released-steer behaviour, exit grip and exit
grip duration can all be changed. More drift grip tightens the line; less retains more lateral
travel. More drag loses speed faster. Entry steering threshold changes how deliberate a turn must be.

Quick Spin has its own enable switch, maximum turn rate, yaw acceleration, duration, cooldown,
travel-speed limit, momentum retention, velocity-redirection acceleration, target gain and
completion tolerance. It does not share a hidden handbrake setting. Raising momentum retention
or travel speed makes a wider turnaround; lowering them produces a tighter spin. A shorter
duration needs enough yaw authority to finish the turn. The cooldown begins at activation and
includes the authored spin duration plus its additional cooldown.

## Grounding, recovery, cargo and camera

Drivable uprightness, recovery uprightness/speed thresholds, automatic recovery delay, lift and
clearance volume are exposed. The existing suspension asset controls wheel positions, spring,
damper, ray length, mass, gravity and simulation rate. Recovery must find clear space before
repositioning. Technique-related hard-brake cargo damage is a separate switch; collision damage
uses the existing cargo component's settings.

Camera distance, height, aim height, look-ahead, heading/position smoothing, FOV, drift velocity
blend, lead-angle limit, velocity threshold, reverse framing, near clip, collision radius,
padding, return time and overlap resolution passes are exposed. FOV can optionally follow a
custom speed curve. That curve uses **X = speed / cruise speed, Y = FOV in degrees**. The default
uses a fixed FOV and automatic forward-facing framing, preserving the arcade presentation.

Control bindings remain editable in `VehicleControls.inputactions`. The new quick-turn action is
`QuickSpin`. Existing script GUIDs stay intact, so the game and lab scenes use the replacement
without rebuilding the scenes. Old boost/flip fields are compatibility data, hidden by the tuning
inspector, and do not control the taxi.

## Verification

Run **Tools > Zoom Zoom > Validate Crazy Taxi Controller** from a clean saved editor scene, or
run it in the isolated validation project. It tests input rhythms, normal driving, tricks,
quick-turn heading/cooldown, analog pedal curves and camera behaviour. The report is written to
`VehicleLab_Measurements/crazy_taxi_validation.txt` in that project.

After changing a profile, check the actual streets with both keyboard and controller. Automated
checks catch regressions; they do not establish which authored feel is best for the game.
