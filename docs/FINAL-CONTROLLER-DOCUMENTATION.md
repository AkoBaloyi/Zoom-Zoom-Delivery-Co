# Vehicle controller, engine, camera and damage

Prepared for Ako Baloyi's contribution to *Zoom Zoom Delivery Co.* on 29 October 2026.

## Editorial note: where this belongs

The supplied **LP Final Submission.pdf** contains earlier Rocket League-based documentation and, on PDF pages 72–74, a later **“Hypothesis and Design Goals: Overcooked meets Crazy Taxi”** section. The following material belongs with that later design. Keep the older material as dated development history, rather than allowing both versions to describe the current controller.

The exam brief asks for actions and challenges, the relationship between systems and experience, research, design intent, iteration and individual ownership. These sections address the vehicle contribution to those requirements. They do not replace the separately required personal reflection, and do not invent personal experiences or team decisions.

Status labels matter: **implemented** means supported by the inspected project; **recorded measurement** means supported by the saved lab evidence; **proposed** means design work still requiring implementation or evaluation.

## 1. Controller: design goal and experience goal

### Design goal

The car is the player's character. The controller should make ordinary driving immediately understandable while making efficient driving a learnable skill. Acceleration, braking and steering provide reliable basic movement; Crazy Dash, Crazy Drift, Crazy Stop and quick turn provide distinct ways to launch, redirect momentum, stop and recover a route. These actions should let the player improve delivery times through deliberate timing and route execution. Road dimensions and delivery approaches should be designed around measured vehicle behaviour, so difficulty comes from decisions and execution rather than an unexplained mismatch between the car and the level.

### Experience goal

The player should feel like an increasingly capable courier: quick, daring and under pressure, but able to understand and recover from mistakes. A successful drift into a dash should feel earned. Missing a turn should create a recoverable loss of time rather than helplessness. The player should be able to explain what caused a failure and choose a different approach on the next attempt.

These are intentions, not proven player outcomes. Evaluate them by observing whether a new player can drive and stop after the tutorial, whether a returning player deliberately uses the techniques, whether mistakes can be explained without prompting, and whether the player voluntarily attempts another shift. Compare route completion and recovery times before and after learning the tricks. Automated physics checks establish functional behaviour; they do not establish enjoyment.

### Basic control rules

W/Up or the right trigger requests forward driving. S/Down or the left trigger brakes forward motion, then requests reverse after the car reaches a near-stop and the 0.18-second direction-change delay completes. Steering uses A/D, the arrow keys or the left stick. Pressing the forward pedal while reversing brakes before changing direction. Both pedals together give braking priority while allowing the engine to rev.

F and R, or gamepad B and A respectively, are technique gestures. They do not force the ordinary forward pedal to become a reverse pedal. Five automatic forward ratios provide acceleration and engine feedback without requiring manual gear changes. Analogue triggers allow partial acceleration and braking; keyboard inputs request the full input amount.

## 2. Engine and vehicle implementation

The project uses Unity 6000.5.4f1. The controller combines a Rigidbody and body collider with four downward suspension rays, custom movement rules and a 120 Hz physics step. The suspension rays detect support and surface information. The custom controller supplies the acceleration, braking, steering, grip and technique behaviour; Unity resolves physical contact with the environment. This division makes handling parameters directly adjustable while retaining collision and ramp interaction.

Input events are captured with timestamps and consumed by the controller, allowing short technique sequences to survive between physics updates. Pending sequences are cleared on events such as teleporting and disabling input, so a stored button sequence cannot unexpectedly activate after recovery. The car's lowered centre of mass and grounded anti-roll assistance support readable road movement. Ramps can launch the vehicle physically, but there is no active jump button, directional aerial flip or airborne propulsion in the current taxi controller.

This is a reconstruction of arcade driving behaviour, not a claim to reproduce Sega's original source code. Legacy jump, boost and camera fields still exist in serialized data for compatibility; their presence does not mean those older mechanics remain active. Current taxi settings take precedence.

## 3. Vehicle lab measurements

**Evidence status:** the following are recorded results from **26 October 2026**, cross-checked against the saved CSV and test description. A new successful Unity run was not obtained during this documentation pass. They must not be relabelled as new measurements from 29 October or as final-build certification.

The lab uses the production Car prefab's collider dimensions with the production controller and default taxi test settings, on flat, dry tarmac at 120 Hz. It constructs a test car rather than loading and driving an entire gameplay scene. The production collider measures 1.8 m across by 4.2 m long.

| Required measurement | Recorded result | Definition and conditions |
| --- | --- | --- |
| Normal top speed | **100.800 km/h (28 m/s)** | Full throttle for 12 seconds, without tricks. This is the ordinary cruise ceiling, not the maximum boosted speed. |
| Time from 0 to top speed | **2.842 s to 99% of the ceiling** | The acceleration curve approaches the ceiling asymptotically. Use this practical definition rather than claiming a time to mathematically exact top speed. 0–100 km/h was **2.950 s**. |
| Time from top speed to 0 | **0.917 s** | Ordinary full braking; “stopped” means below 0.1 m/s. Excludes human reaction time. Stopping distance was **12.736 m**. |
| Tightest turning circle | **5.827 m whole-car swept diameter** | Lowest result from full-lock tests targeting 1, 2, 3, 4, 5 and 8 m/s. The tightest result occurred at the 1 m/s target. Centre-path diameter was only **2.222 m** and must not be used as physical road clearance. |
| Fastest tested 90-degree corner | **100.800 km/h entry; 91.405 km/h exit** | A 12 m-wide corner with 6 m inner radius and 18 m outer radius. Full lock then straighten; no dash, drift or brake. |

The corner test required all four corners of the car's collider to remain within the approach, bend and exit corridor, an 85–100 degree final heading, at least 80% entry-speed retention and less than 10 degrees of roll. The sweep reached the ordinary speed cap: it demonstrates that this broad corner is negotiable at cruise speed, not the absolute cornering limit. Narrow streets and boosted approaches require separate tests.

For level design, use the whole-car swept diameter and leave additional clearance for scenery and player error. Allow more than the lab's 12.736 m stopping distance when the player must notice a destination and react. A significant design finding is that this wide corner does not force ordinary-speed braking; sharper routes, delivery approaches or technique-dependent shortcuts are needed if cornering is intended to create meaningful decisions.

**Repeatable lab procedure:** run Unity's Tools → Zoom Zoom → Validate Crazy Taxi Controller command with saved scenes. Record the build/revision, tuning profile, physics frequency, surface and collider dimensions. Repeat the straight acceleration and stopping tests, the settled full-lock speed sweep and the defined 90-degree corridor sweep. Archive the resulting CSV files with the actual run date. The existing script uses filenames containing `2026-10-26`, so the filename alone does not prove the date of a later run.

## 4. Driving techniques

The table describes this project's current controls. Gamepad letters use the Xbox-style mapping. Recognised techniques require appropriate ground contact and an upright car. Repeatedly holding a button is not repeated trick activation.

| Technique | Input and conditions | Effect | What it rewards |
| --- | --- | --- | --- |
| Crazy Dash | Release the pedals, then press F and W within the 0.30 s window; simultaneous input in either event order is accepted. Gamepad: B and right trigger. Ordinary launch requires speed at or below 8 m/s; a dash can also exit a drift. | A short, strong forward acceleration burst, using a higher speed ceiling than normal driving. A drift-exit dash redirects travel toward the new heading. | Timing a launch and aligning the car before committing to speed; recovering travel time after a pickup, stop or corner. |
| Crazy Drift | At forward speed of at least 5 m/s, press R then F within 0.30 s while steering. Gamepad: A then B while steering. | Enters a low-grip slide with stronger yaw, allowing the chassis to rotate while travel momentum continues. A dash can end the slide with an exit launch. | Choosing an entry line, managing momentum and timing the exit. It is a deliberate manoeuvre rather than random rear-end loss. |
| Crazy Stop | At forward speed of at least 5 m/s, combine S and R; release the accelerator for a clear execution. Gamepad: left trigger and A. | Applies stronger deceleration along the direction of travel, with a configured 85 m/s² stop deceleration. This setting is not a measured stopping time. | Judging a late braking point and making a controlled approach to a destination. |
| Quick turn | Hold A or D and press Space; gamepad: steer and press left bumper. Requires ground contact, an upright car and an available cooldown. No steering means no turn. | Performs a controlled approximately 180-degree turnaround in the selected direction, retaining about 90% of entry momentum before other losses. Collision losses are not restored. | Recognising a wrong route or overshoot and committing to recovery promptly. It provides an accessible turnaround without replacing the drift's cornering role. |

The implemented rewards here are movement advantages: time saved, positioning and recovery. The final submission proposes stunt tips and chained monetary rewards, but this inspection did not establish an implemented stunt-tip system. Do not describe each trick as awarding money until that connection is built and tested. Technique-related deceleration is protected from the ordinary hard-braking cargo penalty by default; real collisions remain damaging.

## 5. Crazy Taxi research: retained, adapted and omitted

Crazy Taxi is the current arcade driving reference. The important borrowed structure is the combination of accessible continuous steering with discrete, timed driving techniques. In a published interview, creator Kenji Kanno describes the original intention as driving action rather than vehicle simulation, including deliberate dash and drift behaviour and a late rewrite of its driving code. This supports treating the vehicle's response as authored game design rather than realism that appears automatically from a physics solver. [Kanno interview, 3DM, 23 September 2026](https://en.3dmgame.com/original/4658).

**Retained/reconstructed:** the roles of dash, drift and rapid stopping; input sequences that reward practice; momentum as something the player can redirect; and an automatic chase camera that keeps driving readable. “Cloned” here means recreating these observable relationships, not copying exact constants or proprietary code.

**Changed:** ordinary brake-to-reverse assistance, modern keyboard/controller mappings, automatic forward ratios, the direct quick-turn action, manual look-behind and safe-position recovery. These changes make the driving vocabulary available on the project's controls and limit time lost to being stranded. Passenger fares are recontextualised into food-delivery work and overlapping order pressure. The later submission proposes cooking bays and a stunt-versus-repair economy; these remain design intentions wherever they are not present in the inspected implementation.

**Omitted or retired:** the earlier Rocket League boost tank, active jump and aerial flip. A ramp-induced jump is still possible through motion and geometry. A direct quick-turn button is a project adaptation and should not be presented as an original Crazy Taxi input.

### Why ordinary Unity physics does not automatically reproduce the original

Unity's built-in 3D physics uses Nvidia PhysX. It supplies general-purpose simulation, not Crazy Taxi's particular input grammar, speed rules, steering response or camera. Those rules must be authored and tuned explicitly. The project therefore retains Rigidbody collisions and suspension while implementing its own arcade driving and trick states. [Unity, Built-in 3D physics](https://docs.unity3d.com/6000.0/Documentation/Manual/PhysicsOverview.html).

It would be inaccurate to say that Crazy Taxi existed “before physics engines” or that Unity cannot reproduce its feel. The defensible claim is that default physics does not reproduce a bespoke arcade controller without custom rules and comparative testing. The original handling constants and source code have not been verified here, so exact equivalence cannot be claimed.

## 6. Camera and communication design

The camera follows behind and above the car using smoothed position and heading changes. Its horizon uses world-up, so chassis roll does not tip the entire view. Current taxi settings place it 5.6 m behind and 2.1 m above the car, with an aim height of 1.15 m and 7 m look-ahead. The field of view eases from 65 to 74 degrees as ordinary speed increases. Speed can add 0.6 m follow distance, and acceleration up to another 0.3 m, giving bounded speed feedback rather than an uncontrolled retreat.

To avoid wall clipping, the camera checks for overlap around its aim pivot and sweeps a sphere toward the intended viewing position. Clearance includes the camera's near-plane corners, rather than checking only a single line. An obstruction pulls the camera inward immediately; its normal distance returns smoothly after the obstruction clears. Another sweep checks the adjusted position, and the follow spring resumes from the safe point. The checks ignore the car's own colliders and triggers, and depend on correctly configured solid colliders and camera layers.

Look-ahead lets the player read the road ahead. During a forward drift, the camera partially follows travel direction as well as the car's nose, helping keep the exit road visible as the chassis rotates. This helps corner readability; it does not let the player see through a building or guarantee visibility around every blind bend. The normal reverse view keeps its forward chase orientation; holding C or right-stick click provides look-behind. Recovery snaps the camera to the restored vehicle rather than dragging the view across the map.

The design goal is to communicate speed, direction and available road without making the car feel detached from the player. Actual city corners still need rendered playtesting for visibility and comfort; the saved automated collision checks cannot establish those experiential outcomes.

## 7. Crash damage, repair costs and recovery

### Current implemented damage

The inspected Easy, Medium and Hard scenes enable cargo condition damage with a 10 m/s relative-impact threshold and 1.5 condition points lost for each m/s above that threshold. Every carried order receives the damage, and condition cannot fall below zero. The car itself has no inspected health degradation or repair-payment mechanism.

For one impact: **condition loss = max(0, relative impact speed − 10) × 1.5**, capped by the condition remaining. Relative impact speed is not necessarily the speedometer reading: another moving body or vertical movement can affect it.

| Relative crash speed | Vehicle repair cost in inspected code | Cargo condition lost from one impact | Reduction for an otherwise R100 delivery starting at full condition |
| --- | --- | --- | --- |
| 0–10 m/s (0–36 km/h) | None implemented | 0 points | R0 |
| 15 m/s (54 km/h) | None implemented | 7.5 points | R7.50 |
| 20 m/s (72 km/h) | None implemented | 15 points | R15 |
| 28 m/s (100.8 km/h) | None implemented | 27 points | R27 |
| 38 m/s (136.8 km/h) | None implemented | 42 points | R42 |

These are calculated examples from the implemented rule, not measured crashes or fixed repair bills. Delivery value is reduced when resolving the order; no carried cargo means no cargo-condition loss. Separate sufficiently severe deceleration events can also remove six condition points in these three scenes, subject to the hard-brake cooldown and technique protection. The older “Possible game scene” has different thresholds (6 m/s and four points per m/s), so its values must not be mixed into this table.

### Proposed repair rule from the final submission

PDF page 73 proposes R50 per hard crash but leaves the trigger as “8–10 m/s”. The following is a concrete **proposed clarification**, choosing the upper end to align with the existing cargo threshold. It is not an implemented or approved rule.

| Relative crash speed | Proposed repair charge |
| --- | --- |
| At or below 10 m/s (36 km/h) | R0 |
| Above 10 m/s (36 km/h) | R50 per distinct crash, independent of further speed increase |

If implemented, show the charge and cause immediately and total repairs in the shift summary. A sustained scrape should count as one crash, with a new charge only after separation and a new qualifying impact. Define this event rule before implementation. The team must also decide whether repair charges replace or stack with cargo-value loss; silently applying both would increase punishment beyond the documented R50 fee.

### Stuck and flipped rules: implemented

| Situation | Response | Cost |
| --- | --- | --- |
| Trying to drive but making no more than 0.75 m progress for about three seconds | Attempt recovery to the last safe road pose. | Five seconds removed from the active shift only after a successful reset. |
| Overturned for about three seconds | Attempt the same recovery. | Same five-second cost. |
| Manual recovery: Backspace or View/Select | Requires the car to be stranded and moving below 1.5 m/s. | Same cost after success. |
| Safe point is obstructed or unsupported | Do not teleport into it. | No recovery charge until a reset succeeds. |
| Parked upright or deliberately braking | Does not by itself trigger stuck recovery. | None. |

The car records clear, supported road poses and can use a supported spawn as its initial fallback. A successful recovery clears motion, pending inputs and tricks, updates the camera and sends recovery feedback. The time deduction can end a shift. There is no implemented additional R50 fee merely for becoming stuck or flipped.

## 8. Course connection and individual contribution

Bogost's *Unit Operations* provides a useful lens for analysing the recombination: dash, drift, stopping and recovery are not meaningful only as isolated features. In this project they affect how a courier can execute a plan while orders continue ageing. Their changed relationship with the delivery system is the design argument. This is an application of the book's comparative approach, not a quotation or a claim that it supplies these driving rules. [Bogost, Unit Operations](https://bogost.com/books/unit-operations/).

Anthropy and Clark's *A Game Design Vocabulary* supports describing the car through verbs, objects, rules and context. A drift is a player verb; a bend, wall or destination gives that verb a purpose and a constraint. Camera, sound and feedback help the player understand the result. The technique table therefore records the input, outcome and reason to use each action. [Publisher description and contents](https://www.informit.com/store/game-design-vocabulary-exploring-the-foundational-principles-9780133155228).

The production reference is **Heather Maxwell Chandler**, rather than “Heather Maxwell-Gardner”. Applying the production-cycle framing of *The Game Production Handbook*, this contribution should demonstrate the dependency from controller implementation to measurement, level sizing, integration, validation and documentation. Preserve dated evidence and distinguish completed work from the remaining economy proposal. Verify the edition used by the course before adding a year or page-number citation. [The Game Production Handbook, third-edition contents](https://www.oreilly.com/library/view/the-game-production/9781449688097/).

Ako's documented ownership covers the vehicle, camera and handling measurements. The personal reflection should explain the reasoning behind the pivot, what the lab evidence changed for level design, what remains unproven about player experience, and how findings were communicated to teammates. Add actual messages, commits, feedback and dated screenshots as evidence; do not invent meetings, approvals or playtest outcomes.

## Evidence index

- Supplied `LP Final Submission.pdf`: PDF pp. 72–74 establish the later Crazy Taxi direction and proposed economy; pp. 5, 8 and the earlier vehicle sections retain superseded descriptions.
- Supplied `5. 2026 Long Project Final Submission - Exam.pdf`: pp. 2–4 establish the design, research, process, reflection and contribution criteria.
- `VehicleLab_Measurements/level_design_numbers_2026-10-26.csv` and `corner_speed_sweep_2026-10-26.csv`: numerical measurement evidence.
- `docs/playtesting/CONTROLS-AND-LEVEL-NUMBERS-2026-10-26.md` and its evidence folder: method and saved functional checks.
- `Assets/Project/Scripts/Vehicle/VehicleController.cs`, `.Taxi.cs`, `.Recovery.cs`, `TaxiTrickRecognizer.cs`, `CrazyTaxiSettings.cs`, `ChaseCamera.cs`, and `Tuning/Tuning_Balanced.asset`: current vehicle rules and settings.
- `Assets/Project/Scripts/Orders/CargoCondition.cs`, `CargoSystem.cs`, `OrderManager.cs`, and the Easy/Medium/Hard scenes: damage calculation, payout connection and scene overrides.

Before submission, add a labelled controller-input image, a top-down whole-car turning-circle diagram and a chase-camera obstruction screenshot. They should depict the actual build and state their capture date. These are remaining visual evidence tasks, not images already captured for this document.
