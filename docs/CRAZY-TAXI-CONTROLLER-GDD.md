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

Acceleration, braking and gear selection are separate actions. Drive is selected at the start of
play. Holding the brake brings the vehicle to rest without automatically reversing it. Reverse
remains available for backing away from an obstruction or making a precise correction.

The main turnaround move is a quick 180-degree spin. This follows the desired experience of
turning the vehicle around decisively rather than relying on extended reversing. The spin is a
deliberate modern adaptation: it has a direct input, predictable rotation, adjustable momentum
retention and a cooldown. It leaves Drive selected so the player can continue forward immediately.
Steering chooses the spin direction; without steering, the default is a rightward turn.

| Action | Keyboard | Controller |
| --- | --- | --- |
| Accelerate | W / Up | Right trigger |
| Brake | S / Down / Left Ctrl | Left trigger |
| Steer | A–D / Left–Right | Left stick |
| Quick turnaround | Space | Left bumper |
| Select Drive | F | B |
| Select Reverse | R | A |
| Look behind | Hold C | Hold right-stick click |
| Right an overturned stationary vehicle | Backspace | View / Select |

## Analog trigger travel

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
An optional speed-based FOV curve is available as a modern presentation choice.

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
