# Crazy Taxi controller validation — 24 October 2026

The replacement is implemented in the current project checkout. The existing separate
`feature/accelerator-and-gear` checkout was read as background and was not modified.

## Results

| Check | Result |
| --- | --- |
| Fresh compilation of all runtime scripts | 0 errors, 0 warnings |
| Fresh compilation of all editor scripts | 0 errors, 0 warnings |
| Unity 6000.5.4f1 technique/physics/camera checks | 40 passed |
| Actual game scene, 12-second automated Play Mode test | Passed |
| Runtime errors in the Play Mode test | 0 |
| Real input-event moves in the game scene | Dash, Back Dash, Dash, Stop, Dash, Quick Spin |
| Maximum displacement during the smoke test | 46.48 m |
| Accelerator output at quarter / half / full RT travel | 0.219 / 0.479 / 1.000 |
| Brake output at quarter / half / full LT travel | 0.219 / 0.479 / 1.000 |
| README action table | All eight current actions documented |
| Saved tuning profiles | All 80 scalar taxi settings explicitly present in all three profiles |
| Local documentation links and patch whitespace | Passed |

The trigger outputs reflect the configured 0.04 deadzone followed by a linear travel curve.
They are requested pedal amounts, not acceleration in m/s² or measured physical trigger force.

Raw evidence:

- [Engine checks](evidence/crazy-taxi-controller-2026-10-24/crazy_taxi_validation.txt)
- [Actual scene and analog input report](evidence/crazy-taxi-controller-2026-10-24/crazy_taxi_playmode.txt)

## Method and limits

A running Unity session held the original project's lock. Validation therefore used an isolated
temporary project containing the latest project scripts, tuning/input assets and the actual game
scene's dependencies, with installed packages referenced from the existing cache. The user's
editor was not closed and no shared scene was saved.

The checks used real Unity physics. The Play Mode test queued virtual keyboard and gamepad events
through the Input System, including partial trigger travel. It simulated Game-view focus because
the batch editor has no focused game window. This focus override exists only in the batch test.

The engine checks separately cover invalid/valid technique timing, cruise/reverse speed, normal
braking, dash/limiter speed, drift momentum and cancellation, stopping, back manoeuvres, quick-spin
heading/cooldown, ballistic flight, camera walls/reverse/frame-rate/teleport behaviour, and pedal
curve authoring. Historical boost/flip CSV measurements were not rewritten; new lab runs use a
separate taxi measurement filename.

These were headless engine checks. Rendered visual quality, a physical controller's hardware feel,
and the preferred handling balance still require playtesting. No exact recreation of Sega's
undocumented physics or camera constants is claimed.

The repository's separate Python documentation suite could not run: the bundled Python lacks
pytest/hypothesis, and dependency installation failed because package-host DNS was unavailable.
The action-table, serialized-setting and local-link checks above were run independently using
Python's standard library; they do not substitute for claiming the unavailable suite passed.

## Reproduction

From a clean saved editor scene, use **Tools > Zoom Zoom > Validate Crazy Taxi Controller**.
The engine report is written to `VehicleLab_Measurements/crazy_taxi_validation.txt`.

For an isolated batch project with the required assets/packages, invoke
`ZoomZoom.Vehicle.Editor.CrazyTaxiValidation.RunAndSmoke` with Unity's `-executeMethod` option.
It runs the engine checks and then the actual game scene smoke test, writes both reports under
`VehicleLab_Measurements`, and exits nonzero on a failed check.

## GitHub branch integration

Before publishing to `feature/vehicle-camera`, the implementation was integrated with
`origin/main` at `4b01f51`, which contains the existing remote feature branch. Incoming scenes,
UI, orders, assets and telemetry were preserved. The completed taxi controller replaced the
older vehicle/input implementation at the conflicts. Recovery counts now bridge to the newer
telemetry. Unrelated edits in the original working folder were not included.

The merged tree compiles with 97 runtime scripts and 10 editor scripts, with zero errors and
zero warnings. All 40 engine checks pass. The actual merged game scene passes its input smoke
test with six recognised moves, 48.75 m maximum displacement, correct analog pedal travel and
zero runtime errors. See [merged engine evidence](evidence/crazy-taxi-controller-2026-10-24/merged-validation.txt)
and [merged Play Mode evidence](evidence/crazy-taxi-controller-2026-10-24/merged-playmode.txt).
