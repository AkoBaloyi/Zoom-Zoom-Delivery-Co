# Vehicle control and camera design

## Design identity

Zoom Zoom Delivery Co uses a custom arcade driving system whose main reference is the original
Crazy Taxi. Its identity comes from fast, readable driving, decisive changes of direction,
momentum-preserving slides, and short input sequences that reward practice. Basic driving should
be easy to understand. Greater speed and more efficient routes come from learning when to dash,
drift, stop and turn around.

The system modernises that reference for keyboard and contemporary controllers. It does not use
a boost meter, jump, directional flip or aerial steering. Advanced driving is expressed through
the vehicle's ground movement and timed inputs.

## Core controls

### Modern forward gearbox and launch response

Drive now contains five automatic forward gears. This is a modern project adaptation, separate
from the original Crazy Taxi D/R selector and its trick sequences. Ordinary pedal use selects direction at a standstill; explicit Drive/Reverse buttons remain
for technique inputs. Ordinary acceleration shifts through D1–D5 without extra buttons. Reverse remains
available, and the quick spin remains the main turnaround move.

Forward gears change actual motor thrust. The initial configuration shifts at approximately
28, 49, 71 and 89 km/h. Each shift unloads toward 12% thrust, drops RPM, then progressively
re-engages over 0.22 seconds. A minimum 0.65-second loaded pull AFTER engagement and downshift
hysteresis prevent rushed changes and hunting near a shift point. Each gear's speed
band and thrust multiplier, shift duration, RPM response, sound pitch and volume can be authored
independently. This is an arcade gearbox using speed bands, rather than a clutch or wheel-based
mechanical simulation. Crazy Dash, Back Dash and Limiter Cut retain their dedicated thrust so a
normal automatic shift cannot interrupt a successful technique.

Base forward acceleration is 24 m/s² before per-gear multiplication. The five gear multipliers
are 0.58 / 0.40 / 0.36 / 0.32 / 0.30: first-gear motor demand is therefore 13.92 m/s² before surface
and pedal factors. Normal acceleration builds through sustained pulls; Crazy Dash retains its
separate 60 m/s² burst. The contrast between normal driving and learned techniques is deliberate. The normal
speed ceiling remains 28 m/s (101 km/h), preserving the reward for advanced acceleration tricks.
The HUD shows D1–D5, speed, RPM and shift state. Layered procedural placeholder engine and road-air loops provide rev changes and sustained travel
feedback. Load also affects revs; coasting and powered acceleration sound different at the same
road speed. Each loop can be replaced with an authored clip or muted independently of physics. A separate
engagement cue and a short adjustable rumble pulse confirm the returning drive after each shift.
Rumble targets the pad supplying player input and stops on pause, focus loss, disable or teleport.

VehicleLab automatically displays live controller meters. Hardware trigger travel, pedal actions
and applied motor inputs are separate readings. This supports testing quarter/half/full travel,
detecting focus or disabled-input problems, and recording comparable tuning sessions as CSV.

### Pedals and direction selection

W/Up/R2 drives forward. S/Down/L2 brakes while travelling forward; holding it after a near-stop
engages proportional reverse after a short 0.18-second dwell. W/Up/R2 brakes reverse travel
before returning to Drive by the same rule. Standstill means planar speed at or below 0.35 m/s,
not simply a zero forward-speed reading during a slide. Airborne movement cannot engage the
assistance. Pressing both pedals gives brake priority and never powers both directions.

Explicit D/R remains available for Crazy Taxi sequences. Selecting R and using the accelerator
retains powered reverse/Back Dash behaviour; selecting D retains powered forward techniques.
Automatic direction changes are distinct non-gesture events: they cannot manufacture a Dash,
Drift or Limiter Cut. The assistance can be disabled to restore separate brake and D/R control.
This creates an accessible basic driving layer while preserving deliberate advanced inputs.

The main turnaround move is a quick 180-degree spin. This follows the desired experience of
turning the vehicle around decisively rather than relying on extended reversing. The spin is a
deliberate modern adaptation: it has a direct input, predictable rotation, adjustable momentum
retention and a cooldown. It leaves Drive selected so the player can continue forward immediately.
Steering chooses the spin direction; without steering, the default is a rightward turn.

| Action | Keyboard | Controller |
| --- | --- | --- |
| Accelerate | W / Up | Right trigger |
| Brake, then reverse at rest | S / Down / Left Ctrl | Left trigger (L2) |
| Steer | A–D / Left–Right | Left stick |
| Quick turnaround | Space | Left bumper |
| Select Drive | F | B |
| Select Reverse | R | A |
| Look behind | Hold C | Hold right-stick click |
| Right an overturned stationary vehicle | Backspace | View / Select |

## Analog trigger travel

### Brake-held engine revving

Holding brake and accelerator together applies the brake and disengages wheel drive, while the
engine independently revs from a 1,000 RPM idle toward 9,000 RPM at full accelerator. Partial accelerator produces
proportional free revs. At the cap an audible limiter pulses; releasing gas lets revs fall.
Automatic gear shifts do not advance during free revving, and the revs do not charge a boost
or create an automatic Dash on brake release. Engine demand is separate from wheel throttle,
so brake priority cannot erase the accelerator's effect on sound and RPM. Normal five-speed
shift tuning and travel ceilings remain unchanged. This is a deliberate project adaptation.

Controller pedals use the full range of trigger travel. A light press produces light acceleration
or braking; a full press requests the full available amount. This supports careful approaches,
small corrections and controlled departures as well as fast driving.

Accelerator and brake have independent deadzones, response curves and response times. Steering
has its own deadzone, response curve and smoothing. The threshold used to recognise a deliberately
floored accelerator is separate from the curve used for ordinary movement. Consequently, tuning
dash recognition does not remove fine control from the lower part of trigger travel.

## Driving techniques

Crazy Dash provides a strong forward launch when the player releases both pedals, selects Drive,
then presses the accelerator. Crazy Drift uses a quick Reverse-to-Drive sequence while turning;
the vehicle rotates while retaining travel momentum. A dash during a drift launches the car along
its new heading. Crazy Stop combines Reverse and braking for a much faster stop. Limiter Cut
chains a new correctly timed Reverse–Drive–accelerator sequence into an existing fast dash.
Back Dash and Back Drift provide advanced reverse manoeuvres.

These moves require deliberate inputs. Holding a button does not repeatedly trigger a trick, and
an expired sequence does not activate later. Ground contact is required. Leaving a ramp creates
a ballistic jump caused by the level geometry; the player cannot apply aerial propulsion.

Quick turnaround and gear-flick drift have different purposes. The turnaround gives a predictable
change of heading at the cost of speed and a cooldown. A drift preserves more momentum and can
flow into a dash, rewarding more skilful cornering. This distinction gives beginners a recovery
tool while retaining an advanced driving vocabulary.

## Camera and feedback

The camera follows automatically from behind and above the vehicle. Its horizon remains level
when the chassis leans or crosses uneven ground. Heading changes are eased, and drift framing
partly follows the travel direction to help the player read the turn's exit. Obstructions pull
the camera inward immediately; the normal distance returns smoothly once the view clears.

The default reverse presentation retains the original forward-facing chase orientation. Players
can hold a look-behind control; the designer can instead enable automatic reverse framing. Camera
distance, height, aim, smoothing, drift lead, collision clearance and field of view are adjustable.
Default presentation now sits 2.1 m above the chassis and 5.6 m behind it, with a 7 m look-ahead.
Speed smoothly widens the FOV from 65 to 74 degrees, adds up to 0.6 m chase distance, and forward
acceleration adds up to 0.3 m of eased pullback. These are bounded, authored modern cues rather
than claimed Sega camera constants. The horizon stays stable; no random camera shake is used.
The entire cue layer can be disabled; a custom FOV curve can replace the default speed mapping.

A persistent gear and speed display makes the vehicle's state visible. Successful techniques
produce an immediate named confirmation. The aim is to make both the input vocabulary and the
resulting movement learnable through play.

## Fairness and recovery

Ordinary cornering is forgiving and predictable. Advanced moves should create useful decisions,
not unpredictable loss of control. A stationary overturned vehicle can be righted manually and
automatically recovers after a configurable delay if there is safe clearance.

Cargo damage follows actual loss of travel speed rather than apparent forward-speed changes
caused solely by chassis rotation. Deliberate techniques are protected from hard-brake damage by
default, so the game does not secretly punish its own turnaround and stopping moves. Collision
damage remains active. The designer can enable hard-brake damage during techniques when testing
a more demanding cargo model.

## Authoring and balancing

The controller is built from project-owned movement, input recognition and camera logic on top of
Unity's Rigidbody collision simulation. Its behaviour is not locked inside a third-party vehicle
controller. Saved tuning profiles expose acceleration, braking, speed limits, turn response, grip,
drift behaviour, input timing, spin behaviour, recovery, pedal curves and camera presentation.

Designers can edit individual values and curves, duplicate profiles for experiments, and change
the active profile without rewriting the movement code. Surface and weather multipliers remain
part of the same system. The initial values are reconstruction choices at this project's scale,
not claimed copies of Sega's internal constants.

Playtesting should assess whether the Crazy Taxi identity remains immediately readable, whether
partial trigger presses improve precision, whether the quick spin is useful without replacing
drifting, and whether the camera shows enough of the road during fast turns.

## Reference and adaptation record

Sega's [original-game manual](https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/71230/manuals/DC_CT_PC_MG_EFIGS_FINAL_lr.pdf?t=1733765150)
establishes independent pedals, D/R gears and the core dash/drift/backward manoeuvres. Original
Crazy Taxi does include Reverse; the preference to emphasise spinning is a design response to
the player's experience, not a claim that the original lacked reverse.

The direct quick-turn input, analog response authoring, optional reverse camera, recovery controls
and tuning interface are this project's modern adaptations. Detailed evidence and implementation
choices are recorded in [the research record](research/CRAZY-TAXI-IMPLEMENTATION-2026-10-24.md).
The current [identity and differences record](research/CRAZY-TAXI-IDENTITY-AND-SPEED-FEEL.md)
explains each adaptation, its purpose, and the evidence from the player recording and controlled comparison.

The [five-speed gearbox record](research/CRAZY-TAXI-FIVE-SPEED-GEARBOX.md) supersedes the
previous four-speed tune and records the new pull durations, sound/pulse controls and evidence.
