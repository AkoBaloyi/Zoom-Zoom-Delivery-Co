# LP Final: Gemini controller writing pack

**For:** Ako Baloyi  
**Purpose:** Add evidence-based controller, camera, vehicle-engine, and contribution sections to the open LP Final Submission Google Doc.  
**Repository baseline:** `origin/main@06fe0ff8d53634509cf608e85209c4f223d87123`.

## Read this before using Gemini

No file named **“LP Final Submission”**, or a close variant, was found in the tracked `origin/main` evidence. Neither that submission nor the private Google Doc was inspected. An untracked file with that title was deliberately excluded and not opened. Attach or export the current submission later if you want this pack revised against it.

Gemini must first audit the **open** Google Doc. It must inspect the existing headings, voice, terms, formatting, repeated material, claims, citations, and suitable insertion points before it writes anything. Do not let it infer the document’s state from this pack.

This pack distinguishes:

- **Confirmed current:** supported by `origin/main` source or tracked evidence.
- **Historical:** valid for an older controller or measurement set, not the current controller.
- **Branch-only:** present at commit `6061f54`, not merged into `origin/main`.
- **Scene-attached:** serialized on a current scene or prefab, not merely present as source.
- **Tested:** supported by the named tracked automated or measurement evidence. This does not mean live feel was tested.
- **Derived design estimate:** arithmetic from stated values, not a recorded measurement.
- **Proposed:** wording or behavior awaiting Ako or group approval.
- **Not yet measured:** no qualifying recorded CSV value exists.

Aim for a clear final controller contribution of about 10–15 pages after it is integrated into the submission. Edit one section at a time.

## Source-setup checklist

- [ ] Upload the rendered PDF of this writing pack to Drive as the consolidated guide.
- [ ] Upload the current measurement originals: `docs/playtesting/evidence/controls-and-level-numbers-2026-10-26/level-design-numbers.csv` and `corner-speed-sweep.csv`.
- [ ] Upload `VehicleLab_Measurements/vehicle_measurements.csv` only when writing the clearly labelled historical comparison.
- [ ] Upload `docs/research/CRAZY-TAXI-CONTROLLER-RESEARCH.md` for the research section and follow its visible inline sources.
- [ ] Upload only the implementation files needed for the section: `VehicleMeasurement.cs`, `ChaseCamera.cs`, `VehicleJumpFlip.cs`, `VehicleController.cs`, `VehicleController.Taxi.cs`, `VehicleController.Recovery.cs`, `TaxiTrickRecognizer.cs`, `VehicleInput.cs`, `VehicleControls.inputactions`, the three tuning assets, `CargoCondition.cs`, `OrderManager.cs`, and `OrderTuning_Balanced.asset`.
- [ ] Upload `.agents/tasks/turning-geometry-2026-10-14/report.md` only for the historical derived geometry discussion.
- [ ] Export current files read-only with `git show origin/main:<path>`. Export branch evidence read-only with `git show 6061f54:<path>`. Do not present a branch export as current code.
- [ ] Add any later submission export supplied by Ako, but do not treat this pack as evidence of the private Doc’s current wording.

## Operational guide

Follow this order every time. The sequence is based on Google’s guidance for [writing and editing with Gemini in Docs](https://support.google.com/docs/answer/13951448), [adding files as Gemini sources](https://support.google.com/docs/answer/16813283), [matching a document’s style and format where available](https://support.google.com/docs/answer/15541879), and [refining selected sections instead of replacing a whole document](https://workspace.google.com/blog/product-announcements/reimagining-content-creation).

1. **Upload sources to Drive.** Upload this pack and only the originals needed for the next section. Account, plan, region, desktop, and experimental-feature availability may affect which Gemini controls appear.
2. **Add viewable files as Gemini conversation sources.** Add the files through Sources or `@`. Confirm every file is visible in the Sources list. Too many files may exceed the conversation context, so use section-specific originals.
3. **Open LP Final Submission.** Work in the real Google Doc. Do not create a replacement from this pack.
4. **Run the audit-only prompt.** Gemini must report the current structure and conflicts without drafting or inserting text.
5. **Edit one section at a time.** Run the master prompt, then one section prompt. Ask Gemini to show the proposed insertion and wait for approval.
6. **Highlight each insertion and verify against sources.** Review each factual statement, status label, number, ownership claim, and link before accepting it.
7. **Never regenerate the whole Doc.** Reject any action that rewrites unrelated sections or removes group members’ work.

Content was rephrased for compliance with licensing restrictions.

---

# Audit-only prompt

Copy this first. Do not combine it with a writing prompt.

```text
Audit the open LP Final Submission Google Doc only. Do not draft, rewrite, insert, delete, or format any submission text.

Inspect and report:
1. Its heading structure and the best existing insertion points for controller goals, measurements, techniques, Crazy Taxi research, camera, damage/recovery, engine/implementation, and contribution/intent.
2. Its established voice, tense, person, design vocabulary, naming, table style, citation style, and formatting patterns.
3. Existing statements about the controller, car, camera, physics, damage, recovery, Crazy Taxi, testing, measurements, ownership, and personal intent.
4. Duplicates or contradictions inside the Doc and conflicts with the pinned sources.
5. Claims that lack evidence, confuse current and historical work, call branch-only work available, or attribute Kyuri’s or Zubuhle’s work to Ako.
6. Questions only Ako can answer, including personal intent, reflection, marking-session decisions, the meaning of 0-to-top, technique naming, and proposed repair rules.

Return an audit table with: location, current wording or short paraphrase, issue, source to check, recommended insertion point, and question for Ako. Do not silently resolve conflicts. Do not write in Ako’s voice. End with “AUDIT COMPLETE — NO EDITS MADE.”
```

---

# Master writing prompt

Run this once after the audit. Then run only one of the eight section prompts.

```text
Use the audit and pinned sources to propose one named insertion into the open LP Final Submission. Preserve the document’s established voice, terms, heading levels, table style, and citation style. Do not imitate unsupported personal reflection.

Assignment frame: Long Form Project, “Splice and Subvert Two Games.” The group work discusses systems/game, research, interrogation/hypothesis/design goals/question, and process/reflection. The individual work discusses design documentation and contribution/expression of intent. Treat these as supplied assignment requirements, not facts read from the private Doc.

Use the course ideas without fabricated quotations, page numbers, editions, or citations:
- Apply Bogost’s Unit Operations to the interactions among car, order, processing bay, tip/pay, cargo damage, camera, and navigation units. Explain how local rules combine into the delivery experience rather than treating the controller as an isolated object.
- Apply Maxwell-Gardner’s production-evidence framing to scope, risk, ownership, milestones, iteration, verification, and the difference between implemented, attached, measured, tested, proposed, and unresolved work.
- Apply Anthropy and Clark’s clear design vocabulary: verbs, objects, relationships, rules, feedback, goals, challenges, and intended experience. Use concrete mechanism names instead of vague claims about “feel.”

Evidence rules:
- Use origin/main@06fe0ff8d53634509cf608e85209c4f223d87123 for current repository facts.
- Use 6061f54 only as branch evidence. Never call its CrazyTechniques component player-available: it is branch-only, absent from main, unattached in its commit, and untested there.
- Keep intention, implementation, observation, historical evidence, derived estimates, unresolved work, and proposals separate.
- Use only recorded CSV values as measurements. Show formulas and assumptions only in a separate Derived design estimate table.
- Never invent intent, feelings, reflection, participant numbers, controlled testing, results, citations, quotations, pages, editions, or implementation.
- Family and S01 comments are informal feedback, not controlled testing.
- Headless or automated checks establish mechanics under their stated setup, not player experience.
- Claim only Ako’s work. Describe links to Kyuri’s and Zubuhle’s systems as integration boundaries.
- Quote fewer than 30 consecutive words from any source. Prefer paraphrase and include: “Content was rephrased for compliance with licensing restrictions.”

Writing process:
1. Name the one target section and the audited insertion point.
2. Show the complete proposed insertion first.
3. Mark every status clearly and include visible links beside research claims.
4. List source files used and questions requiring Ako’s answer.
5. Wait for Ako’s approval.
6. After approval, apply only that insertion and highlight it for source verification.
7. Never regenerate the whole Doc or edit another person’s section.
```

---

# Section prompt 1: Controller goals

```text
Write only the controller-goals insertion. Use the evidence appendix and the audited Doc rather than repeating the appendix.

Use two separate headings: “Controller design goal” and “Controller experience goal.” Present both passages as polished candidate wording proposed for Ako’s approval. Do not say that either passage is Ako’s personal intent until he approves it.

Proposed design-goal wording:
“Create a direct-force, raycast-suspension arcade controller whose separate accelerator and Drive/Reverse technique grammar, surface-aware handling, recovery, and camera feedback connect driving risk to order timing, cargo condition, and payout.”

Proposed experience-goal wording:
“Make base driving immediately readable while giving practiced players a performable skill ceiling where faster routes create deliberate risk/reward, mistakes recover quickly, and feedback explains each successful technique and loss.”

Explain briefly that the design goal names rules and mechanisms plus Kyuri order-layer integration. Explain that the experience goal names readable base driving, a performable skill ceiling, deliberate risk/reward, recovery, and legible feedback. Treat order timing, cargo, and payout as Kyuri-owned systems that the car integrates with, not as Ako’s implementation ownership.

Add “Ako to approve or revise” below each candidate. Ask Ako what changed during production and why before writing any intent or reflection. Show the insertion and wait for approval.
```

---

# Section prompt 2: Measurements

```text
Write only the controller-measurements insertion. Use the evidence appendix and pinned CSV files. Do not copy measurements from prose when the CSV is available.

Create the required measured-results table exactly around these five measures:

| Measure | Recorded current result | Exact setup and limit |
| Top speed | 28.000 m/s (100.800 km/h) | Balanced current controller; 12 seconds full throttle; dry flat tarmac; runtime 120 Hz override; production car; no tricks. |
| 0-to-top | not yet measured | Exact cap is approached asymptotically. The recorded proxy is 0-to-99% of 28 m/s in 2.842 s; 0-to-100 km/h is 2.950 s. Keep proxies in this note, not as the missing measurement. |
| Top-speed-to-0 | 0.917 s | 28 m/s to below 0.1 m/s; full brake; dry flat tarmac; no human reaction time. Recorded stopping distance is 12.736 m. |
| Tightest turning circle | 5.827 m whole-car swept diameter | 3.600 km/h target; steady full lock; production car’s outer collider corners included. Centre-path diameter 2.222 m is comparison only. |
| Fastest 90-degree-corner speed | 100.800 km/h entry; 91.405 km/h exit | Fastest tested on a 12 m road, 6 m inner/18 m outer radius; whole collider stayed on road; at least 80% retained; under 10° roll; no tricks. This reached the normal cap and is not a universal maximum. |

Use “measured” only for recorded CSV values. State that geometry changes the corner result. State that CrazyTechniques at 6061f54 was not measured.

Then add a separate “Historical measurements, not the current controller” table:
- About 32.0000 m/s top speed.
- 18.0948 m and 1.1583 s stopping from 31.3653 m/s.
- 22.0967 m first-lap turning sweep at 9.8399 m/s, with recorded radius 11.0484 m. This is not a tightest-circle or 90-degree-corner result.

Then add a separate “Derived design estimates, not measurements” table:
- Historical steady circle at 9.84 m/s: about 20.26 m. Formula: 2 × 10.13 m calculated radius using the historical curvature/grip assumptions.
- Historical low-speed cap: about 5.71 m. Formula: 2 / 0.35 m^-1.
- Explain that “about 20.3 m” and “about 5.71 m” are arithmetic, not CSV observations.

Add the exact rerun method: production Car collider, Balanced current profile, flat dry tarmac, runtime 120 Hz override, no tricks. Open VehicleLab and use F1 stopping, F2 turning, F3 wall recovery, F5 camera, and F6 the current full suite; F4 is retired. `VehicleMeasurement` disables player input while running. Also run Tools > Zoom Zoom > Validate Crazy Taxi Controller. For a new 0-to-top result, Ako must first approve an operational threshold, such as 99%; until then print “not yet measured.”

Cite `level-design-numbers.csv`, `corner-speed-sweep.csv`, `VehicleLab_Measurements/vehicle_measurements.csv`, `VehicleMeasurement.cs`, and the turning-geometry report. Show the insertion and wait for approval.
```

---

# Section prompt 3: Techniques

```text
Write only the driving-techniques insertion. Use the evidence appendix. Do not merge the 6061f54 component with the later origin/main implementation.

Start with this branch-evidence table. Preserve the distinction between input, runtime effect, rewarded action, needed feedback, and status.

| 6061f54 move | Exact input recognized there | Runtime effect there | Rewarded action | Needed feedback | Status and evidence |
| Crazy Dash | Release accelerator; actual gear transition into Drive; press accelerator. Minimum gap 0.02 s, window 0.25 s. Reselecting Drive while already in Drive does not update the timestamp. | +18 m/s² for 0.8 s to 36 m/s; full launch-traction replacement below 3 m/s; 0.05 s cooldown; surface-scaled. | Accurate release/gear/press timing and repeated corner exits. | Named move, visible/audible confirmation, duration or speed cue. | Main: no. Branch-only: yes. Scene-attached: no. Tested in that commit: no. `6061f54:CrazyTechniques.cs`, `VehicleTuning.cs`. |
| Crazy Drift | Accelerator up; Reverse then Drive, each gap within 0.02–0.25 s; steering magnitude above 0.3 when Drive arrives. | Rear grip ×0.2, yaw acceleration 6 rad/s², and 35% of handbrake drag for 1.1 s; surface-scaled. | Timed corner entry and a catchable rotation. Label that reward as design analysis. | Move name, slip/rotation cue, exit/catch cue. | Main: no for this component. Branch-only: yes. Scene-attached: no. Tested there: no. Same sources. |
| Crazy Stop | While branch Crazy Drift is active, accelerator released OR brake above 0.5. | Up to 45 m/s² stopping deceleration for 0.6 s, ending early at rest. Because Drift already requires accelerator up, the next recognition step can trigger Stop immediately and truncate Drift. | Converting a committed slide into a precise stop. Label as analysis. | Distinct stop confirmation and clear unintended-trigger feedback. | Main: no for this component. Branch-only: yes. Scene-attached: no. Tested there: no. Same sources. |
| Limit Cut | Accelerator released; Reverse; Drive; accelerator, within the same timing window and with no steering. | +22 m/s² for 0.55 s to 44 m/s; surface-scaled. | A harder chained speed sequence above the normal ceiling. | Distinct name, speed-ceiling cue, sequence success/failure cue. | Main: no for this component. Branch-only: yes. Scene-attached: no. Tested there: no. Same sources. |

Explain why a player cannot feel a branch-only, unattached component: source existence does not put it on a serialized player object or into the current build.

Then add a clearly separate current-main note. Current availability comes from the later serialized `VehicleController` path using `TaxiTrickRecognizer.cs` and `VehicleController.Taxi.cs`, not from `CrazyTechniques.cs`. Current controls differ: Accelerator W/Up/RT; Brake S/Down/Left Ctrl/LT; Drive gesture F/gamepad East; Reverse gesture R/gamepad South; steering A/D/arrows/left stick. Tracked game smoke observed Dash, Drift, Dash, Stop, and QuickSpin with zero runtime errors. Automated evidence is not a live feel result.

Use this decision gate:
- Current Limiter Cut is a chained Reverse → Drive → accelerator speed technique while a fast dash window is live.
- Current QuickSpin, called Quick Turn in player-facing prose, is separate: hold steering and press Space/LB. It retains 90% momentum, runs for 0.9 s, and has a 0.65 s cooldown.
- Default documentation decision: document Limit Cut/Limiter Cut as the fourth named Crazy Taxi technique and document Quick Turn separately.
- Rename or redesign Limit Cut as Quick Turn only after Ako defines the intended move and approves the change. Do not make that product decision in the document.

Show the insertion and wait for approval.
```

---

# Section prompt 4: Crazy Taxi research

```text
Write only the Crazy Taxi research insertion. Use `docs/research/CRAZY-TAXI-CONTROLLER-RESEARCH.md` and its inline sources. Note that parts of that 14 October document are marked superseded, so use it for external research routes and historical reasoning, not blindly for current project facts.

Organize the insertion under “What was cloned,” “What was adapted,” and “Limits of fidelity.”

What was cloned structurally:
- Separate accelerator, brake, and discrete Drive/Reverse input grammar.
- Named sequential techniques rather than a held upgrade resource.
- Immediate, legible confirmation and reward for successful skill inputs.
- A readable base action of accelerate and steer, with expert sequences layered above it.

What was adapted for Zoom Zoom Delivery Co.:
- Surface and weather multipliers so tarmac, wet, dirt, grass, ice, metal, and concrete can affect drive, grip, brake, resistance, and effects.
- Integration points for Kyuri’s order timing, cargo condition, early/late payout, cancellation, and burn rules. Do not claim a stunt-tip or fixed-repair system exists.
- Keyboard and gamepad bindings rather than an arcade cabinet’s exact controls.
- Telemetry and lab measurement for reproducibility.
- Safe recovery for a compact delivery level.
- A possible repair tradeoff only as a proposal requiring Ako/group approval and Kyuri integration.

Limits of fidelity:
“Sega did not publish the original controller constants or source. The arcade behavior was hand-authored for fixed hardware. This project uses a Unity Rigidbody with direct forces and raycast suspension at a verified runtime timestep. Structural and behavioral fidelity can be evaluated; numeric identity cannot be claimed.”

Never say Crazy Taxi predates physics engines. Do not claim “pound-for-pound exact” numeric reconstruction.

Use visible inline links where the claims appear, including:
- [NAOMI analog controls discussion](https://www.arcade-projects.com/threads/resource-on-naomi-analog-controls.1508/)
- [IGN Crazy Moves guide](https://guidesarchive.ign.com/guides/16200/crazymoves.html)
- [Crazy Taxi skills guide](https://www.crazytaxi.net/skills.html)
- [Crazy Taxi 2 advanced-techniques guide](https://rappy-world.com/guide/crazy_taxi_2_advanced_techniques_guide)
- [IGN Crazy Box guide](https://guidesarchive.ign.com/guides/16200/crazybox.html)
- [Crazy Taxi rules](http://crazytaxi.net/rules.html)
- [Crazy Taxi fare guide](https://rappy-world.com/guide/crazy_taxi_fare_guide)
- [GamesRadar score-attack retrospective](https://www.gamesradar.com/retrobots-favorite-score-attack-games/7/)
- [Sega’s origin account](https://www.sega.com/news/crazy-taxi-world-tour-the-moment-it-all-began)

Paraphrase. Reproduce fewer than 30 consecutive words from any one source. End the research section with exactly: “Content was rephrased for compliance with licensing restrictions.” Show the insertion and wait for approval.
```

---

# Section prompt 5: Camera

```text
Write only the camera insertion. Use three separate headings: “Camera design intention,” “Camera implementation rule,” and “Camera observed/tested result.” Do not collapse intention into a test claim.

Design intention:
- Propose that the automatic chase view should keep the base drive readable, show the exit direction during a slide, communicate speed, avoid walls, and let the player check behind without steering the camera continuously.
- Label this as design intention, not as Ako’s personal statement until approved.

Implementation rule:
- The camera follows the car’s nose/heading with smoothing; during a forward taxi drift, velocity can lead the heading toward the travel direction.
- Speed raises FOV from 65° by up to 9°, adds up to 0.6 m follow distance, and forward acceleration adds up to 0.3 m pullback.
- Wall avoidance sizes a sphere to protect the camera near plane, runs sphere casts, resolves existing overlaps with penetration checks, then casts again. Do not call this a simple raycast.
- Hold C or right-stick press for a 180° LookBehind view.
- `VehicleInput.CameraLook` returns zero. Manual mouse/right-stick orbit is retired. Right-stick movement is not current camera look.
- `cameraFollowReverse` is serialized false in all three profiles, so automatic reverse framing is off. Do not invent a reverse transition or dead-zone. The serialized 1 m/s reverse threshold is dormant while the toggle is false.

List the active nested Taxi camera values: distance 5.6 m; height 2.1 m; look-ahead 7 m; aim height 1.15 m; velocity minimum 2 m/s; maximum drift lead 80°; drift blend 0.55; heading time 0.18 s; follow time 0.07 s; base FOV 65°; collision radius 0.35 m; wall return 0.3 s; wall padding 0.05 m; four overlap passes; near clip 0.1 m; reverse follow off.

Profile comparison:
- Balanced, Grippy, and Loose have the same active nested Taxi camera values.
- Older top-level compatibility fields differ, including 4.0/4.2/3.8 m distances, but `ChaseCamera.Settings` reads the nested Taxi settings. Do not present the legacy differences as current behavior.

Observed/tested result:
- Tracked validation supports automatic speed/heading follow at 30 and 144 fps, wall avoidance, and the current LookBehind path. State the exact evidence name used.
- Do not claim live readability, comfort, corner visibility, or player preference without Ako’s observation.

Cite `ChaseCamera.cs`, `VehicleInput.cs`, `VehicleControls.inputactions`, the three tuning assets, and tracked validation. Show the insertion and wait for approval.
```

---

# Section prompt 6: Damage and recovery

```text
Write only the damage-and-recovery insertion. Use the evidence appendix and current source/scene files. Separate implemented behavior from proposed economy changes.

Define three quantities before the table:
- Physical travel speed: planar Rigidbody movement used to detect hard deceleration.
- Relative impact speed: `collision.relativeVelocity.magnitude`, used for collision damage.
- Above-threshold speed: max(0, relative impact speed − scene threshold), used in the condition-loss formula.

Implemented formula:
condition loss = max(0, relative impact speed − scene threshold) × condition points per m/s.
Delivery money loss = (base pay + early/late time adjustment) × lost-condition fraction.
Condition is clamped between zero and the amount remaining. This is cargo condition, not a fixed repair bill.

Create this crash-speed-versus-cost table. Label every money value a derived example assuming an on-time R250 payout, not a measurement or fixed fee.

| Scene setup | Relative impact | Above threshold | Condition loss | Derived R250 delivery loss |
| Possible game scene: threshold 6 m/s, 4 points per m/s | 8 / 10 / 15 / 20 m/s | 2 / 4 / 9 / 14 m/s | 8 / 16 / 36 / 56 points | R20 / R40 / R90 / R140 |
| Easy and Hard: threshold 10 m/s, 1.5 points per m/s | 12 / 15 / 20 m/s | 2 / 5 / 10 m/s | 3 / 7.5 / 15 points | R7.50 / R18.75 / R37.50 |
| Medium | — | — | not available from scene attachment | No serialized CargoCondition was found. Do not infer a cost. |

Explain that `Possible game scene`, Easy, and Hard attach different settings. The current normal braking value is 28 m/s², so the attached hard-brake threshold is 28 × 1.25 = 35 m/s². Current hard-brake losses are 12 condition points in Possible game scene and 6 in Easy/Hard. Comments that mention 22 or 27.5 are stale. Current technique settings protect technique braking from hard-brake damage. Verify attachment before claiming this in any newly supplied scene.

Include the order consequences: normal base pay is R250; early adjustment is +20% above 50% patience; late adjustment is −10% below 20%; cancellation and burn are each R50. No stunt-tip or fixed repair-fee system is implemented on current main. Any repair bracket or tip economy is Proposed and requires Ako/group approval plus Kyuri integration. Do not invent speed brackets.

Add a recovery table:

| State | Implemented current response | Status limit |
| Stuck while trying to move | After 3 seconds without sufficient progress, reset to the last clear, supported tarmac/concrete pose; a successful reset deducts 5 seconds. Manual Backspace/Select uses the same stranded checks and cost. | Usable only where the merged controller is serialized and the shift timer is present. Automated checks do not prove player comfort. |
| Flipped/overturned | The same recovery path watches uprightness, validates support and clearance, teleports, and snaps the camera. | Do not describe it as a jump or flip move. `VehicleJumpFlip` is a disabled compatibility proxy. |
| Ordinary parked car | No automatic reset merely for parking or holding brake. | The recovery request requires a stranded state. |

Say that Kyuri owns cargo, order payout, cancellations, burn, timers, and any tip integration. Ako owns the vehicle/recovery side and integration evidence. Do not claim completed tip or repair integration. Show the insertion and wait for approval.
```

---

# Section prompt 7: Engine and implementation

```text
Write only the engine-and-implementation insertion. Use the evidence appendix and exact current source paths.

Cover these verified facts:
- Unity 6000.5.4f1 and Universal Render Pipeline 17.5.0.
- A custom direct-force Rigidbody controller using AddForce, AddForceAtPosition, and AddTorque.
- Four raycast suspension contacts. It does not use WheelCollider for the production vehicle controller.
- Project Settings stores a 0.02 s fixed timestep (50 Hz), while the active Balanced tuning enables a runtime override to 120 Hz. `VehicleController.ApplyTuningToBody` sets `Time.fixedDeltaTime = 1 / physicsHz`. State both values and the mechanism.
- Active Balanced Taxi values include 28 m/s cruise speed, 24 m/s² base acceleration before gearbox scaling, 28 m/s² braking, and 65 m/s² corner grip.

Explain architecture reasons without claiming they were Ako’s intent unless he confirms them: direct forces expose arcade acceleration, grip, yaw, recovery, and technique effects; ray contacts keep suspension tunable; a fixed runtime step helps repeat measurements and input sequences; explicit status/evidence labels make production review reproducible.

Document the measurement/verification tools:
- VehicleLab F1 stopping, F2 turning, F3 wall recovery, F4 retired, F5 camera road-ahead, F6 current full suite. F7 profile, F8 reset, and F9 overlay are display/lab controls, not required result rows.
- `VehicleMeasurement` writes CSV and disables player input during a run.
- `docs/tooling/compile_check.py` builds temporary runtime/editor assemblies from Unity-generated references. A planning run on the stale checkout said “Compiles clean” with two runtime obsolete-API warnings, but this is not an exact origin/main compile claim.
- `Tools > Zoom Zoom > Validate Crazy Taxi Controller` has tracked evidence containing 114 successful controller check descriptions and no recorded failure/error lines in that evidence. The game smoke recorded five recognized moves and zero runtime errors. These are automated/headless or scripted checks, not player-feel evidence.
- The scene validator checks line endings, duplicate anchors, dangling local IDs, orphan transforms, and reciprocal hierarchy links. Do not claim a fresh final run if none was performed.
- Exactly 34 repository `test_*` functions are defined. The fresh planning result was 32 passed and 2 failed. One expected exactly three README links although four exist; one expected a QuickSpin row without A/D even though A/D belongs to Steer. Report these as documentation-contract failures. Never say 34 currently pass.

List claims that still require a live play session: readability, comfort, skill ceiling, technique performability for players, risk-taking, recovery satisfaction, camera comfort, and replay motivation. Family or S01 feedback remains informal unless Ako supplies a controlled protocol.

Cite `ProjectVersion.txt`, `Packages/manifest.json`, `TimeManager.asset`, `VehicleController.cs`, `VehicleController.Taxi.cs`, `VehicleMeasurement.cs`, tuning, tracked validation/game smoke, `compile_check.py`, scene validator documentation, and repo checks. Show the insertion and wait for approval.
```

---

# Section prompt 8: Contribution and intent

```text
Write only the contribution-and-intent insertion. Use the evidence appendix and audit. Do not write personal reflection in Ako’s voice.

Create a concise ownership table:

| Person | Owned work |
| Ako | Vehicle/character control; vehicle physics and tuning; camera; telemetry and measurement; surfaces/weather vehicle response; knockable-prop vehicle integration; historical boost/jump/flip work; current self-right/recovery; Crazy Taxi gear and techniques; engine/repository verification. |
| Kyuri | Orders, timers, cargo, tips or payout integration, cancellations, HUD, ticket/results, difficulty scenes, and menus. |
| Zubuhle | Level art and UI art. |

Claim only Ako’s work. If a source shows Ako wiring the car to another system, call it integration rather than ownership of that system.

Add this compact integration map:
Player input → Ako’s VehicleInput/TaxiTrickRecognizer → Ako’s VehicleController/Rigidbody/raycast suspension → Ako’s camera, feedback, telemetry, surfaces, props, and recovery.
Vehicle state/collisions → Kyuri’s CargoCondition/order payout/timer rules → Kyuri’s HUD/results.
World and route presentation → Zubuhle’s level/UI art around the shared vehicle and order interfaces.

Add marking-discussion points as questions, not answers:
- Which controller goal does Ako approve, and what wording would he change?
- Which controller decisions did Ako personally make, and what evidence records each decision?
- What production risk or failure changed his next action?
- Which measured result affected tuning or level discussion?
- Which work was prototyped, retired, replaced, or left unresolved, and why?
- What did Ako learn about separating mechanical verification from player experience?
- Which live play observations can he personally support?
- What does he want markers to understand about his intent and contribution?

Include the time-specific old-feel explanation:
“At the point when the main-line changes were support, input, camera, telemetry, and recovery changes, those changes did not replace the old force loop by themselves. The `6061f54` CrazyTechniques component remained branch-only and unattached, so there was no live technique recognizer from that branch for a player to feel. Later origin/main work installed a different current force loop and `TaxiTrickRecognizer` path. The earlier observation that the game still felt old must therefore be dated; it is not evidence that the current main controller feels the same.”

Separate that repository explanation from any claim about present feel. Ask Ako for current live evidence. Do not call family or S01 feedback controlled testing. Apply Bogost to interacting units, Maxwell-Gardner to ownership/process evidence, and Anthropy/Clark to precise verbs, rules, feedback, goals, and challenges. Use no fabricated quotations, pages, or editions.

Show the insertion and wait for approval.
```

---

# Final fact-check prompt

Run this after each approved insertion and once more before submission. It reviews only; it does not apply corrections.

```text
Fact-check the one highlighted insertion against the pinned source files and the audited open Doc. Do not edit the Doc.

Return a claim table with: claim, classification (supported / unsupported / ambiguous / needs Ako), exact source file or visible external link, current/historical/branch/proposed status, ownership, and smallest correction.

Check all of the following:
- Every measurement comes from the named CSV, includes units/setup, and keeps current, historical, and derived values separate.
- “not yet measured” remains on exact 0-to-top unless a newly approved protocol and CSV replace it.
- 6061f54 remains branch-only, unattached, and not player-available; current main’s separate recognizer is not attributed to that component.
- Limit Cut/Limiter Cut and Quick Turn/QuickSpin remain separate unless Ako approved a defined redesign.
- Scene attachment is verified before implemented source is called usable in a scene.
- Camera claims use active nested Taxi values; manual orbit and automatic reverse framing are not called current defaults.
- Damage distinguishes physical travel speed, relative impact speed, and above-threshold speed. Derived R250 examples are not called measured repair fees.
- No stunt-tip or fixed repair-fee system is called implemented.
- Ako is credited only for Ako-owned work; Kyuri’s and Zubuhle’s boundaries remain visible.
- Automated/headless checks are not called live experience tests. Family and S01 comments are not called controlled tests.
- No personal intent, feelings, reflection, measurement, result, citation, quotation, page, or edition was invented.
- External text is paraphrased, fewer than 30 consecutive words are reproduced from any source, visible links appear beside claims, and the licensing sentence is present.
- The insertion says in substance: Sega did not publish controller constants/source; the behavior was authored for fixed arcade hardware; this project uses Rigidbody/direct-force/raycast suspension at its verified timestep; structural/behavioral fidelity is possible, but numeric identity cannot be claimed. It never says Crazy Taxi predates physics engines.
- The insertion matches the audited voice and formatting and does not duplicate another section.

List unsupported or ambiguous claims first. Propose the smallest correction, but do not apply it. End with either “READY FOR AKO REVIEW” or “NOT READY” and the unresolved items.
```

---

# Compact evidence appendix

## Current baseline and measurements

| Item | Confirmed value/status | Evidence route |
| --- | --- | --- |
| Repository baseline | `origin/main@06fe0ff8d53634509cf608e85209c4f223d87123` | `git log -1 origin/main` |
| Engine | Unity 6000.5.4f1; URP 17.5.0 | `ProjectSettings/ProjectVersion.txt`; `Packages/manifest.json` |
| Physics | Direct-force Rigidbody; four ray contacts; no production WheelCollider path | `VehicleController.cs`; `VehicleController.Taxi.cs` |
| Timestep | Project setting 0.02 s; Balanced runtime override 120 Hz | `TimeManager.asset`; `Tuning_Balanced.asset`; `VehicleController.ApplyTuningToBody` |
| Current top speed | 28.000 m/s = 100.800 km/h | `level-design-numbers.csv` |
| Current exact 0-to-top | **not yet measured**; 99% proxy 2.842 s | Same CSV |
| Current stop | 0.917 s and 12.736 m from 28 m/s to below 0.1 m/s | Same CSV |
| Current tightest circle | 5.827 m whole-car sweep at 3.6 km/h; centre path 2.222 m | Same CSV |
| Current 90° corner | 100.800 km/h entry; 91.405 km/h exit; defined 12 m course | Same CSV; `corner-speed-sweep.csv` |
| Current validation | 114 successful check descriptions in tracked evidence; game smoke five recognized moves, zero runtime errors | `unity-validation.txt`; `game-smoke.txt` |
| Repo tests | 34 defined; fresh planning result 32 passed, 2 documentation-contract failures | `docs/tooling/repo-checks` and planning record |

## Historical and branch boundaries

| Item | Status | Evidence route |
| --- | --- | --- |
| Historical speed | About 32 m/s, older controller/profile | `origin/main:VehicleLab_Measurements/vehicle_measurements.csv` |
| Historical stop | 18.0948 m and 1.1583 s from 31.3653 m/s | Same CSV |
| Historical turning sweep | 22.0967 m at 9.8399 m/s; radius 11.0484 m | Same CSV |
| Derived historical geometry | About 20.26 m steady circle and 5.71 m low-speed cap | `.agents/tasks/turning-geometry-2026-10-14/report.md` |
| Branch component | `CrazyTechniques.cs` exists at 6061f54; absent from main; no scene/prefab attachment in that commit; not tested there | `git show 6061f54:...`; `git diff origin/main...6061f54`; ancestry/scene search |
| Current technique path | Later `TaxiTrickRecognizer` and `VehicleController.Taxi`, reached through current serialized VehicleController | Current source, prefab/scene serialization, tracked validation |

## Camera, cargo, and recovery

| Item | Confirmed current rule | Evidence route |
| --- | --- | --- |
| Camera | Automatic nose/heading follow; drift travel lead; speed FOV/distance cues; sphere-cast plus overlap/penetration wall avoidance | `ChaseCamera.cs`; active nested Taxi tuning |
| Camera input | C/right-stick press LookBehind; manual orbit retired; reverse follow off | `VehicleInput.cs`; `VehicleControls.inputactions`; three tuning assets |
| Cargo collision | max(0, relative impact − threshold) × points per m/s | `CargoCondition.cs` |
| Scene cargo | Possible: 6 m/s, 4 points/m/s, hard brake 12. Easy/Hard: 10 m/s, 1.5, hard brake 6. Medium: no serialized component found | Current scene YAML |
| Order money | R250 base; +20% early; −10% late; R50 cancellation; R50 burn | `OrderTuning_Balanced.asset`; `OrderManager.cs` |
| Repair/tips | No fixed repair fee or stunt-tip system implemented on current main | Current order/vehicle source |
| Recovery | Manual/automatic stranded recovery; 3 s automatic delay; last safe road pose; successful five-second cost | `VehicleController.Recovery.cs`; Balanced tuning |
| Jump/flip | Retired; compatibility proxy disabled | `VehicleJumpFlip.cs` |

## Exact lab rerun

Use the production Car collider, Balanced profile, flat dry tarmac, runtime 120 Hz override, and no tricks. F1 measures stopping, F2 turning, F3 wall recovery, F5 camera road-ahead, and F6 the current full suite; F4 is retired. Run `Tools > Zoom Zoom > Validate Crazy Taxi Controller`. Record the CSV, Unity version, commit, profile, surface, timestep, collider, thresholds, course geometry, and whether techniques were disabled. Define and approve “top” before adding a 0-to-top result.

## Approval gates and unresolved claims

| Gate | Required decision/evidence |
| --- | --- |
| Doc structure/voice | Gemini audit of the open private Doc; this pack did not inspect it |
| Goals | Ako approves or revises both candidate goals |
| 0-to-top | Ako accepts an operational threshold and a new recorded run, or it stays “not yet measured” |
| Technique naming | Keep Limiter Cut and Quick Turn separate unless Ako defines and approves a redesign |
| Repair economy | Ako/group and Kyuri approve any proposed repair brackets or stunt tips |
| Medium cargo | Verify/add scene attachment outside this documentation task before claiming it works there |
| Current feel | Live play observation with stated method; automation cannot establish feel |
| Intent/reflection | Ako supplies his own decisions, reasons, feelings, and lessons |

## Source paths and visible research links

Current repository paths use `git show origin/main:<path>`. Branch-only technique paths use `git show 6061f54:<path>`. Core files are under `Assets/Project/Scripts/Vehicle/`, `Assets/Project/Scripts/Orders/`, `Assets/Project/Scripts/Vehicle/Tuning/`, `ProjectSettings/`, and `docs/playtesting/evidence/controls-and-level-numbers-2026-10-26/`.

Google workflow references: [write/edit with Gemini](https://support.google.com/docs/answer/13951448), [Gemini Sources](https://support.google.com/docs/answer/16813283), [style and format matching](https://support.google.com/docs/answer/15541879), and [section-level content creation](https://workspace.google.com/blog/product-announcements/reimagining-content-creation).

Research routes from `docs/research/CRAZY-TAXI-CONTROLLER-RESEARCH.md`: [NAOMI controls](https://www.arcade-projects.com/threads/resource-on-naomi-analog-controls.1508/), [IGN moves](https://guidesarchive.ign.com/guides/16200/crazymoves.html), [Crazy Taxi skills](https://www.crazytaxi.net/skills.html), [advanced techniques](https://rappy-world.com/guide/crazy_taxi_2_advanced_techniques_guide), [IGN Crazy Box](https://guidesarchive.ign.com/guides/16200/crazybox.html), [general rules](http://crazytaxi.net/rules.html), [fare guide](https://rappy-world.com/guide/crazy_taxi_fare_guide), [score-attack discussion](https://www.gamesradar.com/retrobots-favorite-score-attack-games/7/), [Sega origin account](https://www.sega.com/news/crazy-taxi-world-tour-the-moment-it-all-began), [Kenji Kanno interview](https://www.ign.com/articles/2000/02/05/the-crazy-interview-with-sojs-kenji-kanno), and [Crazy Taxi at 25](https://www.theguardian.com/games/2025/feb/24/taxis-sega-pop-punk-classic-crazy-taxi-at-25).

Content was rephrased for compliance with licensing restrictions.
