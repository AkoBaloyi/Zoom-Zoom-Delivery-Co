# Zoom Zoom Delivery Co

## Game Description

*Zoom Zoom Delivery Co* is a solo arcade delivery game about keeping a small courier business moving while orders arrive automatically. Players drive through a compact greybox city, collect cargo, and race it to the correct destination before timers expire. The game explores pressure, mistakes, and recovery: predictable handling rewards practice, collisions or missed turns create setbacks, and a clear route back into the delivery loop gives each failure a chance to become a satisfying comeback.

## Design Question

Does automatic order pressure combined with demanding but predictable driving make recovery satisfying?

## Rights

This project is university coursework. Ako, Kyuri, and Zubuhle reserve all rights. No permission is granted to copy, modify, redistribute, or resubmit this work. Any such use requires written permission from all three rights holders: Ako, Kyuri, and Zubuhle.

## How to Open

A marker should open the `main` branch.

1. Add this project folder in Unity Hub.
2. Select Unity Editor `6000.5.4f1` for the project.
3. Open the project and allow Unity to finish importing it.
4. In the Project window, open `Assets/Project/Scenes/Possible game scene.unity`.
5. Enter Play mode.

The build starts at the main menu; Play loads this game scene.

`VehicleLab`, retained as a disabled scene in Build Settings, is kept deliberately. It is the instrumented
test track used to measure the handling, not the game. Open it from the same Scenes folder if you
want to re-run the measurement tests, which are editor-only and described under Display Toggles
below.

## Controls

The `Vehicle` action map in `Assets/Project/Scripts/Vehicle/VehicleControls.inputactions` is the
source of truth. Driving now follows the original Crazy Taxi control structure: independent gas,
brake, and Drive/Reverse gears.

| Game control | Input binding | Player action |
| --- | --- | --- |
| Accelerate in selected gear | `W` or `Up Arrow`, or gamepad RT | `Accelerator` |
| Brake without reversing | `S`, `Down Arrow` or `Left Ctrl`, or gamepad LT | `Brake` |
| Steer | `A` and `D`, or `Left Arrow` and `Right Arrow`, or gamepad left stick | `Steer` |
| Select Drive | `F`, or gamepad B | `GearDrive` |
| Select Reverse | `R`, or gamepad A | `GearReverse` |
| Quick 180-degree turnaround | `Space`, or gamepad LB | `QuickSpin` |
| Hold to look behind | `C`, or gamepad right-stick click | `LookBehind` |
| Right an overturned stationary car | `Backspace`, or gamepad View/Select | `Recover` |

The main turnaround is a quick 180-degree spin on Space / LB. Reverse remains available for precise recovery.
The car starts in Drive. Gear selections persist after releasing the button. Holding the brake
stops the car and keeps it stopped; select Reverse and press gas to back up.

**Driving techniques:** release both pedals, tap Drive, then press gas for Crazy Dash. While moving
forward, tap Reverse then Drive while steering for Crazy Drift. Dash during a drift to launch out
of the turn. Reverse followed by brake performs Crazy Stop. While a dash is still fast, release
gas, tap Reverse then Drive and press gas again for Limiter Cut. Reverse immediately after a dash
with gas held performs Back Dash; Drive then Reverse while turning performs Back Drift.

Gamepad trigger travel is proportional: partial RT gives partial acceleration and partial LT gives
partial braking. Deadzones, travel curves, response times and the separate trick-recognition threshold
are editable in the tuning asset.

The gear/speed readout and named move confirmation are visible at the bottom left. There is no
boost tank, jump, handbrake button, directional flip or aerial steering. Space / LB now performs a
quick turnaround rather than the retired handbrake behaviour. The camera follows
automatically. Looking behind is optional; reversing alone keeps the normal forward view.

There is no pickup/drop-off key. Driving into the appropriate zone collects or delivers the order
when a cargo slot allows it, following the existing delivery rules.

## Display Toggles

Driving feedback and delivery guidance appear automatically. F10 is the existing editor-only
vehicle telemetry overlay. The VehicleLab measurement tools remain editor-only: F1 stopping,
F2 turning, F3 wall recovery, F5 camera, F6 the remaining measurement set, F7 profile, F8 reset,
and F9 the lab overlay. Historical flip and boost measurements are retired.

Run `Tools > Zoom Zoom > Validate Crazy Taxi Controller` for the automated technique, physics and
camera checks. Results go to `VehicleLab_Measurements/crazy_taxi_validation.txt`. Run it in an isolated validation
project when other scenes are being actively edited.

The research, adaptations and tuning values are recorded in
[Crazy Taxi implementation](docs/research/CRAZY-TAXI-IMPLEMENTATION-2026-10-24.md).
The [GDD section](docs/CRAZY-TAXI-CONTROLLER-GDD.md) explains the design; the
[tuning guide](docs/CRAZY-TAXI-TUNING-GUIDE.md) explains the exposed values and curves.

## Ownership

### System Ownership

| System | Owner |
| --- | --- |
| Vehicle and camera | Ako |
| Orders, timers and cargo | Kyuri |
| Greybox level and UI | Zubuhle |

Ako also owns shared-build integration, which is the wiring between these three systems rather
than a system of its own: build settings, the delivery points the order queue draws from, and
whichever scene the README names. Zone detection belongs to the order system but lives on the car,
because it measures distance from the vehicle's own transform.

### Branch Ownership

| Branch | Owner |
| --- | --- |
| `feature/vehicle-camera` | Ako |
| `feature/orders-cargo` | Kyuri |
| `feature/greybox-ui` | Zubuhle |

## Where Things Live

Team-authored work follows the system folders under `Assets/Project`. Any asset obtained from
outside the team is recorded in [Credits](docs/CREDITS.md) with its author, source and licence.

## Links

- [Credits](docs/CREDITS.md)
- [Build log](docs/BUILD-LOG.md)
- [Git rules](docs/GIT-RULES.md)
