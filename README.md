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

`Assets/Project/Scenes/VehicleLab.unity` is a second scene, kept deliberately. It is the
instrumented test track used to measure the handling, not the game. Open it if you want to run
the measurement tests described under Controls below.

## Controls

The `Vehicle` action map in `Assets/Project/Scripts/Vehicle/VehicleControls.inputactions` is the
source of truth. Both a keyboard and a gamepad drive the same actions.

| Game control | Keyboard | Gamepad | Action |
| --- | --- | --- | --- |
| Throttle and reverse | `W` / `S` | Right / left trigger | `Throttle` |
| Steer | `A` / `D` | Left stick X | `Steer` |
| Brake without reversing | `Left Ctrl` | `B` | `Brake` |
| Handbrake, cuts rear grip only | `Space` | `LB` | `Handbrake` |
| Boost | `Left Alt` | `RB` | `Boost` |
| Jump | `Left Shift` | `A` | `Jump` |
| Flip, directional | `Q` + `WASD` | `X` + left stick | `Flip` / `FlipDirection` |
| Camera | Mouse movement | Right stick | `CameraLook` |

There is no pick-up or drop-off key. Driving into a pickup ring collects the order and driving
into its drop-off ring delivers it, provided the cargo slot allows it. `ZoneDetection` handles
this automatically, so arriving *is* the action.

### Developer overlays

Not part of the game, but useful to a marker who wants to see the systems working.

| Key | What it shows |
| --- | --- |
| `F10` | Vehicle telemetry: speed, grip budget, front/rear balance, slip angle, surface |
| `F11` | Order ground rings and the fallback order list |
| `F12` | Order compass arrow |
| `F1`–`F6` | Measurement tests, `VehicleLab` scene only. `F6` runs all of them |

## Ownership

### System Ownership

| System | Owner |
| --- | --- |
| Vehicle, camera, and shared-build integration | Ako |
| Orders, timers, cargo, and zone detection | Kyuri |
| Greybox level and UI | Zubuhle |

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
