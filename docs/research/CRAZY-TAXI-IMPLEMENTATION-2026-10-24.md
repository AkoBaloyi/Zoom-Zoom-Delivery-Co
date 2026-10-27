# Crazy Taxi driving and camera reconstruction

> Initial reconstruction record. The later brake-to-reverse and speed-feel changes supersede
> the pedal-direction and camera defaults below; see [the current identity record](CRAZY-TAXI-IDENTITY-AND-SPEED-FEEL.md).

24 October 2026. Target: the original 1999 arcade / 2000 Dreamcast Crazy Taxi.
This supersedes the implementation recommendations in the 14 October research. It does not target
Crazy Taxi 2, High Roller, mobile spinoffs or World Tour. The player character here is the vehicle.

## Sources and corrections

1. **Sega, Crazy Taxi Dreamcast Collection manual, English pages 3â€“4**
   ([publisher manual distributed with the game](https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/71230/manuals/DC_CT_PC_MG_EFIGS_FINAL_lr.pdf?t=1733765150)).
   Primary evidence for separate gas/brake and D/R controls. It documents Dash, Drift, Back Dash and
   Back Drift. Dash begins with released pedals, then Drive and gas. Drift combines a quick Râ€“D
   sequence with steering. Back Dash selects Reverse after a Dash; Back Drift uses Dâ€“R while turning.
   The manual also describes stopping for passenger pickup/drop-off. Those passenger rules are
   outside this controller-and-camera replacement; the existing delivery system retains its rules.
2. **Crazy Taxi Techniques, The 5 Skills You Need to Succeed**
   ([firsthand cabinet technique account](https://www.crazytaxi.net/skills.html)).
   Practical primary observations distinguish sequential gear/gas timing, braking and non-braking
   drifts, dash-out-of-drift, and repeated Limiter Cuts while the previous dash is still fast.
3. **Crazy Taxi Techniques, Additional Skills**
   ([firsthand advanced technique account](https://www.crazytaxi.net/additionalskills.html)).
   Describes the official Stop as gas release, Reverse, held brake after Dash/Limiter Cut, and
   reports that the normal camera keeps its orientation when reversing. This is observational
   evidence, not Sega's engine specification.
4. **Sega's original arcade service-manual index**
   ([preserved Sega Amusements service site](https://backup.segakore.fr/sauservice/Manuals/cta_html/ctamain.html)).
   Identifies separate accelerator/brake assemblies and original cabinet manuals. The linked full
   original cabinet PDF was unavailable during this research, so no timing/physics claims rely on it.

The previous prototype incorrectly made releasing inputs during Drift the Crazy Stop gesture,
omitted Back Dash/Back Drift, inferred gear events only when gear *changed*, and sampled technique
edges in FixedUpdate. Re-selecting Drive must count as a press. A rapid Râ€“D sequence must survive
between physics steps. Holding gas must never replay a recognised sequence every step.

There is no verified source here for Sega's acceleration curve, steering gains, spring constants,
camera distance, camera FOV, smoothing times or exact input windows. The values below are authored
reconstruction parameters, **not measured original constants**. This implementation must be tuned
through play rather than described as an exact physics clone.

## Controller implemented

The existing Rigidbody, four suspension rays, collision shapes, wheel visual contract and ground
surface sampling remain. The normal drive/steer/grip path has been replaced. Gas and brake no
longer fight for a shared axis; Drive and Reverse are latched independently. Ordinary steering
has generous independent corner grip. A deliberately recognised drift rotates the chassis while
retaining momentum; coasting then lets it settle. Brake reduces velocity in its actual direction
of travel. It cannot drive the vehicle backwards. Opposite gear plus gas is a powered direction
change, not a hidden brake/reverse switch. There is no airborne steering or thrust.

| Move | Gesture | Implemented effect |
| --- | --- | --- |
| Crazy Dash | Both pedals released â†’ select Drive â†’ floor gas | Strong launch above cruising speed |
| Crazy Drift | At forward speed, tap R â†’ D while steering | Lower lateral grip and stronger yaw, preserving slide momentum |
| Crazy Stop | Reverse â†’ brake, or Reverse while brake is already held | Strong deceleration along travel; holds at rest |
| Limiter Cut | During the fast dash window, unfloor gas â†’ R â†’ D â†’ floor gas | Raises the next dash ceiling, with a project safety cap |
| Crazy Back Dash | Select R immediately after a forward dash, with gas held | Strong reverse drive |
| Crazy Back Drift | At forward speed, D â†’ R while steering | Rotating slide ending with Reverse selected |
| Drift cancel | Perform Dash during Drift | Forward exit launch and temporary stronger grip |

The easier R+brake stop is intentionally accepted outside a dash as well. Exact analog unfloor
thresholds, maximum chained speed, and maximum drift duration are project adaptations.
Tricks require ground contact and an upright car. The modern quick-turn action, Space / gamepad LB, provides a controlled 180-degree spin with
configurable speed retention and cooldown. Reverse stays available, but the spin is the main
turnaround. This is an explicitly approved adaptation rather than an original input claim.

Input is captured from Input System callbacks
with event timestamps and consumed once in the controller's physics step. Pause, focus loss,
input disabling and teleport clear pending patterns. Physics and technique application have one
owner; there is no dependency on two MonoBehaviours' FixedUpdate ordering.

The Rocket League boost tank, drift/air refill, jump and directional flip are retired. Existing
serialized script identities and zero-valued telemetry properties are compatibility shims for old
scenes/logs. They cannot activate those mechanics. The tuning inspector shows active taxi settings,
chassis, suspension and visuals, rather than the retired tuning controls.

## Camera implemented

The reconstruction uses an automatic third-person chase view, a stable world-up horizon, eased
heading changes and moderate framing. During a drift it partly frames the travel direction so the
exit remains visible. Reverse preserves the normal forward-facing view; C/right-stick-click is an
explicit convenience look-back, **an adaptation**, not a claimed original feature. There is no
Rocket League free-orbit input, supersonic mode or boost-triggered FOV kick.

Heading interpolation uses yaw angles rather than interpolating opposing forward/reverse vectors.
The camera follows the interpolated vehicle in LateUpdate. Its collision sweep excludes the car
and triggers, pulls inward immediately, and eases outward after an obstruction clears. Overlap
resolution handles starting inside geometry. Teleport/recovery resets follow history.

Camera numeric choices and drift framing are an engineering reconstruction. Original-game sources
establish the camera's presentation and reported reverse behaviour, not this exact follow equation.

## Active starting values

Stored in `VehicleTuning.taxi`, explicitly serialized in all three existing profiles:

| Setting | Starting value |
| --- | --- |
| Cruise / reverse speed | 28 / 12 m/s |
| Normal acceleration / brake | 18 / 28 m/sÂ² |
| Normal corner grip | 38 m/sÂ² |
| Dash ceiling / acceleration | 38 m/s / 60 m/sÂ² |
| Limiter increment / maximum | 6 m/s / 56 m/s |
| Back-dash ceiling | 20 m/s |
| Stop deceleration | 85 m/sÂ² |
| Drift minimum speed / duration | 5 m/s / 1.2 s |
| Drift yaw / lateral grip | 150Â°/s / 5 m/sÂ² |
| Input window / minimum event spacing | 0.30 s / 0.008 s |
| Camera distance / height / FOV | 6.5 m / 3.1 m / 65Â° |
| Heading / position smoothing | 0.18 s / 0.07 s |

## Integration and checks

`VehicleController` owns the new motor and recognizer. `VehicleInput` owns a private action asset
instance. `ChaseCamera` replaces the existing camera class while preserving its serialized script
GUID. Existing cars receive `CrazyTaxiFeedback` automatically; the previous gauge is inert. No
shared scene edit is necessary. The existing game, orders, markers and cargo still reference the
same vehicle component. Cargo braking now measures planar travel speed so rotating a car cannot
create fictitious braking damage. Technique braking protection and its short grace period are
configurable; collision damage remains independent. Existing asset GUIDs remain intact.

`Tools > Zoom Zoom > Validate Crazy Taxi Controller` checks valid/invalid input rhythms, forward
and reverse driving, brake behaviour, dash speed, limiter speed, drift momentum, drift cancel,
stopping, ballistic flight, camera reverse stability, obstruction handling, frame-rate consistency,
teleport framing and input-asset compatibility using Unity physics. It writes its results to
`VehicleLab_Measurements/crazy_taxi_validation.txt`.

The original project was locked by a running Unity editor, so automated engine validation uses a
temporary project copy with the installed package cache. It does not close the user's editor or
save shared scenes. Compilation and automated checks do not establish subjective handling quality;
keyboard/controller playtesting on the actual streets remains necessary for final tuning.

Archived measurement CSV rows describe the previous controller. New behaviour must be measured
again; those historical numbers are not evidence for this reconstruction.

## Modernisation agreed with the designer

Reverse remains available; a direct quick spin is the primary turnaround. Controller trigger
travel is proportional, with independent pedal curves, deadzones and response times. Input
recognition thresholds are separate from ordinary pedal output. The active tuning inspector exposes
normal drive, techniques, spin, suspension, recovery and camera controls, including optional speed
curves and reverse-camera framing. See the [GDD section](../CRAZY-TAXI-CONTROLLER-GDD.md) and
[tuning guide](../CRAZY-TAXI-TUNING-GUIDE.md).
