# Customising the controller and camera

Current controls and measurements are in the [26 October report](playtesting/CONTROLS-AND-LEVEL-NUMBERS-2026-10-26.md):
W/S choose travel direction; F/R are trick gestures; quick turn requires steering and retains
90% momentum; stuck recovery waits three seconds and costs five seconds. Anti-roll is controlled
by `antiRollStrength` and `antiRollDamping`. Recovery cost is `recoveryTimeCost`.

The 25 October team-test branch uses tighter normal steering, higher corner grip and stronger
five-speed acceleration. See the [current values and measured comparison](playtesting/CRAZY-TAXI-HANDLING-2026-10-25.md).
Earlier dated comparisons below describe their recorded builds.

Open the car's `VehicleTuning` asset and expand **Taxi**. All active driving and camera settings
are grouped there; chassis, suspension and visual settings follow below. The existing Balanced,
Grippy and Loose assets retain their identities for scene compatibility, but all now start with
the same taxi configuration. Duplicate an asset to make a new experimental profile, then assign
it to the car's **Tuning** field. Create a blank one with **Assets > Create > Zoom Zoom > Vehicle Tuning**.

These are custom project scripts, not an external vehicle package. Runtime tuning edits to an
asset persist after stopping Play Mode. Record a baseline or duplicate the asset before experimenting.
Camera overrides are optional; normally the camera reads the same tuning asset as the car.

## Pedals and steering input

### VehicleLab controller meter

Open **VehicleLab**, press Play, and click inside the Game view. The top-right panel appears
automatically; no scene rebuild or F-key test is required. With a native DualSense the driving
hints use R2/L2, Circle (Drive), Cross (Reverse), and L1 (quick turn).

- **Hardware R2/L2** shows normalized device travel before Input System processors, plus the
  highest value reached since reset. These are Unity device readings, not electrical sensor data.
- **Gas/Brake action** shows the bindings' current values. **Sources** identifies which device
  or keyboard control is supplying each action. A selected monitoring device does not restrict
  driving bindings; with several pads, use **Next controller** to inspect each one.
- **Applied gas/brake** shows the car's actual input after deadzones, curves and response times.
  Motor pedal shows FORWARD/REVERSE and its magnitude. The brake action can become negative
  motor pedal at rest. Motor thrust separately shows the acceleration requested from physics, including surface,
  forward gear and shift effects. It is not measured net chassis acceleration.
- **Reset peaks** clears the travel maxima. **Record CSV** samples raw travel, actions, applied
  pedals, speed, forward gear, RPM, thrust, focus and enabled state at approximately 10 Hz.
  New recordings also include signed forward speed, sampled forward acceleration, selected
  direction, trick, shift state, camera FOV, steering and a feel revision tag. Five-speed recordings use `20261024c` and append shift progress,
  actual shift thrust envelope and shift count, making 26 columns. Shift count resets on car/input reset. Each recording
  has a `.tuning.json` sidecar containing the active car Taxi settings. Measured forward
  acceleration is a signed-speed difference over the sample interval, including heading changes;
  it is not an isolated engine-force measurement. Camera overrides are not part of that car snapshot.
  Stop recording or exit Play Mode to close the file. Editor files appear in the project's
  `VehicleLab_Measurements/controller_<timestamp>.csv`; builds use their persistent-data folder.

If no gamepad is detected, inspect Unity's Input Debugger (**Window > Analysis > Input Debugger**).
If hardware values move but actions do not, inspect the displayed sources/bindings. If actions
move but the car receives zero, check the panel's pause/focus/input status: automatic F-key tests
temporarily disable player input. If neither hardware meter moves, click the Game view and
check the device connection. A virtual XInput pad appears under its reported layout rather
than as native DualSense. The meter does not require adaptive-trigger resistance support.

### Automatic forward gears and engine feedback

Under **Taxi > Modern automatic forward gears**, the five default speed fractions are
`0.28, 0.49, 0.70, 0.88, 1.12` of cruise speed. The first four are upshift thresholds; the last
sets top-gear RPM range. Ratios use positive forward speed, so partial trigger travel changes
acceleration without changing the gear thresholds. Reverse bypasses forward-gear thrust.

| Setting | Default | Purpose |
| --- | --- | --- |
| Acceleration | 24 m/s² | Base forward thrust before gear/surface/pedal factors |
| Forward gear thrust | .58 / .40 / .36 / .32 / .30 | Actual acceleration multiplier for each gear |
| Gear shift time | 0.22 s | Duration of reduced thrust during each automatic change |
| Shift thrust retention | 0.12 | Unloaded thrust floor; progressively restored during engagement |
| Minimum gear hold | 0.65 s | Loaded pull after the preceding shift completes; excludes shift time |
| Progressive shift / unload fraction | On / .35 | First 35% unloads; the rest smoothly re-engages thrust |
| Shift feedback volume / gear shift clip | .18 / procedural fallback | Independent engagement sound and optional authored AudioClip |
| Shift rumble enabled / low / high | On / .22 / .12 | Engagement pulse on the pad actively supplying input |
| Shift rumble duration | .09 s | Pulse length; pause, focus loss, disable and teleport stop it |
| Engine load RPM | 550 | Maximum pedal/load rev offset, reduced as road revs rise |
| Road audio volume | 0.09 | Speed-driven air/road layer independent of engine RPM |
| Downshift hysteresis | 0.22 | Downshift below 78% of the previous upshift threshold |
| Engine idle / redline RPM | 1000 / 6500 | Display and engine sound range |
| Engine RPM response | 16000 RPM/s | Rev rise/fall speed |
| Engine volume | 0.12 | Procedural placeholder loop level |
| Engine idle / redline pitch | 0.55 / 2.2 | Loop pitch range |
| Automatic gears / engine audio | enabled | Independent switches for physics ratios and sound |

Add/remove speed-band entries to change gear count. Missing thrust entries use 1×. Keep band
values ascending; the runtime enforces a minimal gap for invalid edits. Shift hysteresis is
clamped at runtime. No automatic shift occurs while airborne. Reset/teleport clears shift state.
Normal shifts do not reduce dedicated Crazy Dash, Back Dash or Limiter Cut thrust.
At full throttle, the current controlled run shifts at about .575 / 1.450 / 2.325 / 3.200 seconds
and reaches 100 km/h in 5.358 seconds. This deliberately replaces the previous 1.792-second rush
through four gears. Ordinary pull has time to develop; a successful Crazy Dash keeps its burst.
The engine RPM drop spans the shift, rather than jumping to a new speed-derived number in one step.
The engagement sound and rumble pulse start around 85% of the shift, alongside restored drive.
Rumble uses standard motors, not adaptive-trigger resistance; unsupported devices can ignore it.
The RPM model is a controllable arcade approximation; audio is synthesized placeholder content.
**Engine loop** and **Road loop** accept authored AudioClips. A missing clip uses the built-in
procedural layer. Engine audio and road volume are independent; set road volume to zero to mute it.

### Brake-to-reverse direction assistance

**Brake to reverse** defaults on. S/Down/L2 remains a brake until planar speed is at most
**Reverse engage speed** (0.35 m/s), then becomes reverse after **Reverse engage delay**
(0.18 seconds) of continuous pressure. W/Up/R2 first brakes reverse travel and returns to Drive
at rest. **Direction pedal threshold** (0.04) rejects tiny residual mapped input. Both pedals
brake with no thrust. Sliding sideways and airborne states cannot engage an automatic change.
Explicit F/R or Circle/Cross still supplies intentional D/R for the original technique vocabulary.
An automatic direction change clears gesture arming rather than pretending the player tapped D/R.

### Input settings

#### Brake-held free revs

Hold S/Down/L2 and W/Up/R2 together. Brakes stay applied and wheel throttle is zero; the engine
revs independently. Full gas reaches **Engine free rev limit = 9,000 RPM**, with rise/fall rates
of **12,000 / 6,500 RPM/s**. Half mapped gas targets 5,000 RPM from a 1,000 RPM idle. The normal
**Engine redline RPM** field remains the road-coupled automatic-gear reference (6,500), not the
brake-held ceiling. Above that reference, sound rises toward **Engine free rev pitch = 2.8**.
The limiter cuts/reapplies engine sound at **12 Hz** with **.25 retention** during each cut.
All these values are exposed in Taxi settings. This is free revving, not a powered burnout.

VehicleLab/HUD shows FREE REV and LIMITER. Revision `20261024d` recordings append `engine_demand`,
`free_rev` and `rev_limiter` to the earlier 26-column format (29 columns total), alongside the
tuning sidecar. Check for engine demand 1, RPM near 9000, applied wheel gas 0 and drive acceleration 0.

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
uses the bounded speed-cue mapping described below and automatic forward-facing framing.

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

## Speed presentation without changing the cap

The normal speed cap remains **28 m/s = 100.8 km/h**. The existing Dash/Limiter ceilings are
unchanged. The 120 km/h suggestion is a future balance choice (33.33 m/s), not part of this pass.

| Camera setting | Default | Effect |
| --- | --- | --- |
| Distance / height / look ahead | 5.6 / 2.1 / 7 m | Lower foreground framing with visible exit road |
| Speed camera cues | On | Enables FOV, speed distance and acceleration pullback |
| Camera speed FOV addition | 9 degrees | Smoothstep mapping from 65 at rest to 74 at cruise |
| Camera FOV response time | 0.25 s | Smooth transition; no instantaneous speed kick |
| Camera speed distance addition | 0.6 m | Bounded chase extension at cruise |
| Camera acceleration pullback | 0.3 m | Bounded pullback at base acceleration |
| Camera acceleration response time | 0.18 s | Filters surge to avoid a twitch on pedal changes |

The custom FOV curve overrides the default FOV mapping when enabled. Turning speed cues off
removes their additions; camera height, base distance and authored FOV remain independent.
Collision avoidance still applies after these cues, so walls can pull the camera closer.

For a useful next playtest, repeat the same straight and corner route at the same cap. Record a
full-throttle launch, release/reapply at cruising speed, brake to rest and hold to reverse,
then use R2 to return forward. Compare the CSV and tuning sidecar with the saved before recording.
Judge road flow, turn readability, engine load changes and sense of speed separately from RPM numbers.
The [identity and speed-feel record](research/CRAZY-TAXI-IDENTITY-AND-SPEED-FEEL.md) contains the
before/after values and explains which differences from Crazy Taxi are deliberate.

See the [five-speed gearbox record](research/CRAZY-TAXI-FIVE-SPEED-GEARBOX.md) for the latest
recorded-drive diagnosis, current tuning and the Crazy Taxi identity rationale.
