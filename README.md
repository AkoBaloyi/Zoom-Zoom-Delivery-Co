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

This is also the first scene in Build Settings, so a built player opens it directly.

`VehicleLab`, the second scene in Build Settings, is kept deliberately. It is the instrumented
test track used to measure the handling, not the game. Open it from the same Scenes folder if you
want to re-run the measurement tests, which are editor-only and described under Display Toggles
below.

## Controls

The `Vehicle` action map in `Assets/Project/Scripts/Vehicle/VehicleControls.inputactions` is the
source of truth. Both a keyboard and a gamepad drive the same actions.

| Game control | Input binding | Player action |
| --- | --- | --- |
| Throttle and reverse | `W` and `S`, or `Up Arrow` and `Down Arrow`, or the right and left gamepad triggers | `Throttle` |
| Steer left and right | `A` and `D`, or `Left Arrow` and `Right Arrow`, or the left stick | `Steer` |
| Brake without reversing | `Left Ctrl`, or gamepad B | `Brake` |
| Handbrake, cuts rear grip only | `Space`, or gamepad LB | `Handbrake` |
| Boost | `Left Alt`, or gamepad RB | `Boost` |
| Jump | `Left Shift`, or gamepad A | `Jump` |
| Flip, directional | `Q` held with `W` `A` `S` `D` or the arrow keys, or gamepad X with the left stick | `Flip` and `FlipDirection` |
| Look around | `Mouse` movement, or the right stick | `CameraLook` |

Code spans in the binding column are keyboard keys and the mouse; gamepad buttons are named in
plain text because a gamepad `A` and a keyboard `A` are not the same input.

There is no pick-up or drop-off key. Driving into a pickup ring collects the order and driving
into its drop-off ring delivers it, provided the cargo slot allows it. `ZoneDetection` handles
this automatically, so arriving *is* the action.

## Display Toggles

Two keys change what the order system draws. Both work in a build.

| Key | What it shows |
| --- | --- |
| `F11` | Order ground rings, which mark where to stop |
| `F12` | Order compass arrow |

The vehicle telemetry readout and the measurement harness that produced
`VehicleLab_Measurements/vehicle_measurements.csv` are lab instruments and are now editor-only.
They were on `F10` and `F1` to `F9`; in a build those keys do nothing, because a measurement
routine repositions the car and drives it into a wall, which belongs in the test scene and not in
something a marker is playing. Open the project in the editor to use them.

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
