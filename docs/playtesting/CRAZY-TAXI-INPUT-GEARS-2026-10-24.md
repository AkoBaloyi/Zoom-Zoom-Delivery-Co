# Controller meter and automatic gearbox validation

The VehicleLab meter and four-speed arcade gearbox were checked in an isolated Unity
6000.5.4f1 project. The user's open project was left running. The lab scene and game scene
were tested from this checkout, with the updated source and tuning assets.

- The source compilation check passed with zero errors and warnings (88 runtime and 8 editor scripts).
- All 53 Unity assertions passed, including forward upshift thrust reduction, downshift
  hysteresis, reverse independence, disabled gears, empty gear arrays, airborne shift refusal,
  lab meter creation, existing tricks/camera, and native DualSense normalized travel.
- Full-throttle normal driving exceeded 10.5 m/s within 0.5 seconds in the physics test and
  reached the existing 28 m/s ceiling without overshoot. The old base acceleration was 18 m/s²;
  the new base is 24 m/s² with separately authored forward-gear multipliers.
- Actual game and VehicleLab Play Mode smoke tests passed using a **virtual native
  DualSenseGamepadHID**, keyboard events, the real action bindings and VehicleInput.
  Quarter/half/full applied gas and brake were approximately 0.220 / 0.481 / 1.000.
  Both scenes reported zero runtime errors and recognized the existing driving techniques.
- VehicleLab created its meter automatically and recorded controller/action/applied-input
  telemetry to CSV. The saved sample was parsed for consistent columns, full trigger travel
  on both pedals, and automatic forward-gear changes.

Evidence is in [the input and gears evidence folder](evidence/crazy-taxi-input-gears-2026-10-24/).
Its CSV uses a virtual device and is test evidence, not a physical controller calibration.
The automated runs used a headless graphics/audio environment: physical USB/Bluetooth
DualSense travel, panel appearance on the user's display, engine sound quality and subjective
driving feel still need an interactive playtest. The meter makes that hardware test observable
and recordable; it does not establish that the user's connection problem is already fixed.

For an interactive check, open VehicleLab, enter Play Mode and focus the Game view. Release
both triggers, reset peaks, then slowly sweep R2 and L2 individually through quarter, half and
full travel. Compare hardware, action and applied bars. Select Drive with Circle and accelerate
along the straight: D1–D4, RPM drops and brief shift thrust reductions should be observable.
Use Record CSV before this sequence and stop it afterwards. Gear speed bands, per-gear thrust,
shift duration/retention, RPM and audio are editable in the active VehicleTuning asset's Taxi section.

The VehicleLab menu's stale `Assets/_Project` paths were also corrected to the current
`Assets/Project` scene, controls and tuning paths. No scene rebuild is required for the meter.
