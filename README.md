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
4. In the Project window, open `Assets/Scenes/SampleScene.unity`.
5. Enter Play mode.

## Controls

The `Player` action map in `Assets/InputSystem_Actions.inputactions` is the source of truth.

| Game control | Input binding | Player action |
| --- | --- | --- |
| Drive forward / backward | `W` / `S` or Up Arrow / Down Arrow | `Move` |
| Steer left / right | `A` / `D` or Left Arrow / Right Arrow | `Move` |
| Handbrake | `Space` | `Jump` |
| Pick up / drop off order | `E` | `Interact` |
| Camera | Mouse movement | `Look` |

## Ownership

### System Ownership

| System | Owner |
| --- | --- |
| Vehicle and camera | Ako |
| Orders, timers, and cargo | Kyuri |
| Greybox level and UI | Zubuhle |

### Branch Ownership

| Branch | Owner |
| --- | --- |
| `feature/vehicle-camera` | Ako |
| `feature/orders-cargo` | Kyuri |
| `feature/greybox-ui` | Zubuhle |

## Where Things Live

Team-authored work follows the system folders under `Assets/_Project`. `Assets/_Project/ThirdParty` is the only location for any asset obtained from outside the team.
