# Self-contained Gemini editing pack — Game Design Document

Use these prompts in order in Gemini inside the open GDD. Each numbered .txt file is a COMPLETE prompt: copy its entire contents, including the replacement text. No source-file upload is needed. Only the open Google Doc is required.

Apply and inspect each edit before sending the next one. Eight bounded edits make placement and missing changes easier to check than one instruction to rewrite a 46-page document. Every step repeats the essential context so it can be understood without access to this chat. Earlier steps establish the document structure for later consistency checks.

The instructions refer to the headings in the updated 46-page PDF, using page numbers only as orientation. All factual tables and complete prose are embedded below, not referred to as inaccessible attachments. Material from the earlier documentation pack has been incorporated automatically into the appropriate prompts.

## Order

1. Research and game identity — `01-research-and-game-identity.txt`
2. Goals and subsystem integration — `02-goals-and-subsystem-integration.txt`
3. Controller and techniques — `03-controller-and-techniques.txt`
4. Vehicle measurements — `04-vehicle-measurements.txt`
5. Camera and engine — `05-camera-and-engine.txt`
6. Damage and recovery — `06-damage-and-recovery.txt`
7. Core loop and evaluation — `07-core-loop-and-evaluation.txt`
8. Production and final consistency — `08-production-and-final-consistency.txt`

## Prompt 1: Research and game identity

You are editing the open Google Doc for Zoom Zoom Delivery Co., specifically the GAME DESIGN DOCUMENT, not the project plan or individual reflection. All source text and facts required for this step are included below. You do not need access to local files, GitHub, Unity, earlier chat messages or attachments. Do not ask me to upload those sources or tell me to retrieve facts already supplied here.

Apply only the changes specified in this prompt. The location clues refer to a 46-page PDF exported from this document; use exact headings and nearby text to locate sections, because page numbers change after editing. Search the BODY, not just its table of contents. If a named target cannot be found, do not guess or replace unrelated material: identify the missing target in your response. Complete other unambiguous edits. If you cannot edit the document directly, say so and return the exact formatted replacement for the named target; do not claim the document changed.

Current baseline: the active car uses Crazy Taxi-inspired ground techniques. Rocket League jump/flip/boost-tank descriptions are historical. The player drives through pickup/drop-off zones; stopping is not mandatory for every delivery. Stunt tips, processing bays and R50 repair charges are proposals, not verified implemented features. Current collision cargo damage and successful stranded recovery are implemented and must remain distinct from those proposals. Do not invent tests, screenshots, lecturer quotations, team approvals or book quotations. Use South African English.

The material between BEGIN REPLACEMENT and END REPLACEMENT is finished GDD prose, not instructions to be summarised. Insert it substantially verbatim: only adapt heading numbering, native table formatting and transitions necessary for placement. Keep the analysis, limitations, examples, values and source links. Convert Markdown tables into Google Docs tables. Do not insert this instruction block, placement notes or completion checklist into the GDD. Do not create an “editorial preamble” or a list of changes inside the submitted body. After editing, report the actual headings changed and any unresolved target, outside the document.

EDIT 1: Research and game identity

EXACT PLACEMENT AND ACTIONS
1. Locate “2. Game Research”, followed by “2.1. Synopsis”, “2.2. Source Games and Subsystems we took” and “2.3. Category and Subcategory Analysis” (original PDF pp. 8–12).
2. Replace the BODY of Section 2, including those subsections, up to but NOT including “3. Interrogation, Hypothesis and Design Goals”, with the replacement below.
3. If preserving the superseded research is necessary, first move it intact to an end appendix headed “Historical research: Rocket League controller prototype”. Clearly label it as superseded. Do not leave both versions in the current-design chapter.
4. Remove the misplaced “1.3. Source Game Three: Crazy Taxi Analysis” paragraph from the contents/front matter (original p. 2): its function is now fulfilled by Section 2. Do not delete the genuine “1.3. Design and testing terms” glossary.
5. The former source text may say “later submission” or “earlier document”; change these only to “earlier design proposal” or “earlier controller iteration” as appropriate. No new evidence is implied.

BEGIN REPLACEMENT

## 2. Game Research

### 2.1. Synopsis

Zoom Zoom Delivery Co. is a single-player arcade delivery game in which the player chooses between timed orders and drives the routes needed to complete them. The current design combines Overcooked-inspired concurrent task pressure with Crazy Taxi-inspired ground-based driving techniques. Rocket League informed an earlier controller iteration and is retained here as development history rather than a second simultaneous description of the current car.

The car makes delivery planning consequential. A clean corner can preserve the chance to collect another order; a collision can make the original route no longer worthwhile. Position, heading, cargo and remaining time jointly determine the next useful action. The intended experience is a courier becoming more resourceful under pressure, rather than a driver struggling against arbitrary control failure.

### 2.2. Source systems: current and historical

| Source | Relationship to the current design | Limit of the comparison |
| --- | --- | --- |
| Crazy Taxi (Sega, 1999) | Current vehicle reference: acceleration techniques, momentum-preserving drift, rapid stopping and automatic chase framing. | Authored reconstruction; no claim of identical physics constants, original code or a completed stunt-tip economy. |
| Overcooked (Ghost Town Games, 2016) | Automatic concurrent orders, deadlines and pressure to sequence work while other tasks continue ageing. | Cargo occupancy is not autonomous cooking. Processing bays remain proposed wherever not implemented. |
| Rocket League (Psyonix, 2015) | Historical vehicle reference explored through speed, sliding and jump/flip recovery. | The current taxi controller retires the active jump, aerial flip and refillable boost tank. |

### 2.3. Category and design conventions

The current game is an arcade delivery-driving game with time-management decisions. The driving layer asks how to execute a route; the order layer asks which route remains worth executing. This description is an analytical position for this project, not a claim that “momentum recovery” is an established genre or that expert play in the reference games is inherently imprecise.

Arcade handling permits exaggerated response when it serves a readable action. It does not require random instability. Likewise, fixed task timings can make a rule learnable without guaranteeing that every generated job is reachable or fair. That depends on route geometry, information, vehicle behaviour and the work already competing for attention.

### 2.4. Operations and the meaning of the splice

Bogost describes works as configurations of discrete, interlocking units of meaning. His comparative approach supports examining how an operation changes when placed in another configuration, rather than comparing only the surface appearance of complete games. [Bogost, *Unit Operations*](https://bogost.com/books/unit-operations/).

Applied to this project, the useful question is what an operation does before and after the splice. “Drive quickly” is too broad. Launching, carrying, waiting, turning and recovering each establish a particular relationship between the player and the remaining work. The following is our analysis of that relationship, rather than terminology or conclusions quoted from Bogost.

| Operation | Role in the reference/design lineage | Meaning in this delivery game |
| --- | --- | --- |
| A task's timer continues while attention is elsewhere | The Overcooked reference supplies concurrent work that does not wait for the player. | Time spent completing one journey reduces the opportunity to complete other journeys. |
| Commit a limited resource | The station/capacity comparison concerns occupying something that another task could use. | Loading an order occupies cargo space and commits the car to destinations, but does not reproduce a cooking station that processes work while the player leaves. |
| Dash | The Crazy Taxi driving reference supplies a deliberate acceleration technique. | A correctly aligned launch can create a delivery opportunity; a badly aligned launch can spend the same opportunity on recovery. |
| Drift | The driving reference supplies rotation while retaining travel momentum. | A junction becomes a choice of approach and exit, with consequences for the next destination. |
| Recover | The earlier controller explored correction after losing a line. | Recovery includes restoring movement and deciding whether the original delivery plan is still worthwhile. |

This reveals a limit in the Overcooked comparison. A cargo slot can be occupied, but occupation alone is not parallel processing. If the courier has to remain with the car for every task, the system primarily asks for route sequencing. A processing bay would change that structure by allowing one operation to continue while the courier travels elsewhere. That is a meaningful proposed extension, and it must remain labelled as proposed until implemented.

The same distinction applies to Crazy Taxi. Reconstructing driving techniques does not automatically reconstruct the incentive to use them. Our current movement advantage can reward a dash by saving time. The proposed stunt-tip economy would add an explicit financial reward for particular performances. Those produce different decisions, so they must not be described as an already unified system.

The design claim is therefore specific: the current car and timed orders can connect execution to route opportunity. Whether that connection produces rich scheduling or sustained appetite for risk remains an evaluation question. A recognizable vehicle and an order-card interface are insufficient evidence on their own.

### 2.5. The controller pivot

The shift from the Rocket League-inspired prototype to the Crazy Taxi-inspired controller changes where we expect expertise to appear. The earlier document puts weight on instability, jump and flip recovery. The current controller puts greater weight on ordinary steering, ground-based techniques and repeatable input sequences. Keeping both descriptions in the present tense hides that design change.

The relevant loss is also worth recording. Removing active jump and aerial flip reduces the player's vocabulary for moving vertically and correcting mid-air mistakes. The replacement gains a clearer relationship between road geometry and deliberate ground movement. A bend, a missed junction and a departure can now be addressed through drift, quick turn and dash. This is a narrower action space with a clearer relationship to the delivery task, although it still needs routes that make those differences matter.

Calling the original inspiration “slightly out of control” is not enough to justify a controller. An expert's apparent chaos can depend on highly precise inputs. Our question is what a beginner can understand and what an experienced player can improve. The current design seeks low entry difficulty in basic movement and greater mastery in the timing, placement and combination of techniques.

### 2.6. Crazy Taxi: reconstruction and adaptation

Crazy Taxi is the current arcade driving reference. The important borrowed structure is the combination of accessible continuous steering with discrete, timed driving techniques. In a published interview, creator Kenji Kanno describes the original intention as driving action rather than vehicle simulation, including deliberate dash and drift behaviour and a late rewrite of its driving code. This supports treating the vehicle's response as authored game design rather than realism that appears automatically from a physics solver. [Kanno interview, 3DM, 23 September 2026](https://en.3dmgame.com/original/4658).

**Retained/reconstructed:** the roles of dash, drift and rapid stopping; input sequences that reward practice; momentum as something the player can redirect; and an automatic chase camera that keeps driving readable. “Cloned” here means recreating these observable relationships, not copying exact constants or proprietary code.

**Changed:** ordinary brake-to-reverse assistance, modern keyboard/controller mappings, automatic forward ratios, the direct quick-turn action, manual look-behind and safe-position recovery. These changes make the driving vocabulary available on the project's controls and limit time lost to being stranded. Passenger fares are recontextualised into food-delivery work and overlapping order pressure. The later submission proposes cooking bays and a stunt-versus-repair economy; these remain design intentions wherever they are not present in the inspected implementation.

**Omitted or retired:** the earlier Rocket League boost tank, active jump and aerial flip. A ramp-induced jump is still possible through motion and geometry. A direct quick-turn button is a project adaptation and should not be presented as an original Crazy Taxi input.

### Why ordinary Unity physics does not automatically reproduce the original

Unity's built-in 3D physics uses Nvidia PhysX. It supplies general-purpose simulation, not Crazy Taxi's particular input grammar, speed rules, steering response or camera. Those rules must be authored and tuned explicitly. The project therefore retains Rigidbody collisions and suspension while implementing its own arcade driving and trick states. [Unity, Built-in 3D physics](https://docs.unity3d.com/6000.0/Documentation/Manual/PhysicsOverview.html).

It would be inaccurate to say that Crazy Taxi existed “before physics engines” or that Unity cannot reproduce its feel. The defensible claim is that default physics does not reproduce a bespoke arcade controller without custom rules and comparative testing. The original handling constants and source code have not been verified here, so exact equivalence cannot be claimed.

END REPLACEMENT

CHECK THIS STEP
Section 2 must identify Crazy Taxi as current and Rocket League as historical; explain the operations and limits of the comparison; contain full analysis, not just a source-game list. Leave Section 3 for the next step.


---

## Prompt 2: Goals and subsystem integration

You are editing the open Google Doc for Zoom Zoom Delivery Co., specifically the GAME DESIGN DOCUMENT, not the project plan or individual reflection. All source text and facts required for this step are included below. You do not need access to local files, GitHub, Unity, earlier chat messages or attachments. Do not ask me to upload those sources or tell me to retrieve facts already supplied here.

Apply only the changes specified in this prompt. The location clues refer to a 46-page PDF exported from this document; use exact headings and nearby text to locate sections, because page numbers change after editing. Search the BODY, not just its table of contents. If a named target cannot be found, do not guess or replace unrelated material: identify the missing target in your response. Complete other unambiguous edits. If you cannot edit the document directly, say so and return the exact formatted replacement for the named target; do not claim the document changed.

Current baseline: the active car uses Crazy Taxi-inspired ground techniques. Rocket League jump/flip/boost-tank descriptions are historical. The player drives through pickup/drop-off zones; stopping is not mandatory for every delivery. Stunt tips, processing bays and R50 repair charges are proposals, not verified implemented features. Current collision cargo damage and successful stranded recovery are implemented and must remain distinct from those proposals. Do not invent tests, screenshots, lecturer quotations, team approvals or book quotations. Use South African English.

The material between BEGIN REPLACEMENT and END REPLACEMENT is finished GDD prose, not instructions to be summarised. Insert it substantially verbatim: only adapt heading numbering, native table formatting and transitions necessary for placement. Keep the analysis, limitations, examples, values and source links. Convert Markdown tables into Google Docs tables. Do not insert this instruction block, placement notes or completion checklist into the GDD. Do not create an “editorial preamble” or a list of changes inside the submitted body. After editing, report the actual headings changed and any unresolved target, outside the document.

EDIT 2: Goals and subsystem integration

EXACT PLACEMENT AND ACTIONS
1. Locate “3. Interrogation, Hypothesis and Design Goals” (original pp. 12–15). Replace it through its last prototype goal with the new Section 3 below.
2. Replace “4. Integration of Subsystems”, including “4.1. Where integration is now complete”, up to but NOT including “5. Character Controller Design Rationale” with the new Section 4 below.
3. Do not copy the old claims that the car is deliberately unreliable, that fixed task timings mean only the player can be to blame, or that capacity equal to the queue eliminates all choice.
4. Preserve any original dated playtest evidence in its own later playtest section. Do not turn the new example into a recorded observation. Do not change the separate project-plan document or claim its hypothesis was updated.

BEGIN REPLACEMENT

## 3. Interrogation, Hypothesis and Design Goals

### 3.1. Design question

How can timed delivery commitments and learnable arcade driving make players revise their routes, recover from mistakes and improve through deliberate practice?

### 3.2. Working hypothesis

If orders continue ageing while the player uses a reliable car with learnable movement techniques, then driving performance can alter which deliveries remain feasible, prompting route revision and active recovery. This should give repeated shifts value through improved judgement and execution. The claim is supported only if players make meaningful choices after setbacks and can improve those outcomes; more crashes or more button presses alone do not establish success.

The proposed processing-bay and stunt-tip/repair economy would extend this hypothesis. They must be evaluated as additional systems rather than used as evidence for behaviour the current build has not demonstrated.

### 3.3. Controller design and experience goals

The player in Zoom Zoom Delivery Co. is trying to keep a working plan alive while driving changes the conditions on which that plan was based. An order can be reachable when it appears and cease to be sensible after a missed corner. Conversely, a good exit from a junction can make an additional pickup worth attempting. The car therefore does more than carry the player between objectives. It changes which objectives remain possible.

The intended character is a capable courier working close to the limit of what they can manage. The player should want to take one more order because a better line, a well-timed dash or a quicker recovery might make it achievable. Pressure comes from that possibility. If success is guaranteed, there is little to judge; if failure is inevitable, there is little reason to improve. The design needs an interval in which the player's growing ability changes the answer.

This gives the car a personality through action. Ordinary driving is forgiving enough to establish confidence. Advanced moves let the player express decisiveness and control. The order queue gives those movements consequences: a fast turn is valuable because of what it makes reachable next. The desired satisfaction is not simply “the vehicle moves quickly”. It is “I saw a way to save that delivery, and I executed it”.

**Controller design goal:** provide reliable basic driving and distinct, learnable techniques that let the player improve routes, approaches and recovery. Each advanced action should solve a recognisable driving problem and retain a reason not to use it in some situations.

**Controller experience goal:** make the player feel increasingly resourceful under delivery pressure. Success should feel attributable to a decision and its execution; failure should remain understandable and leave a meaningful next action.

These goals do not require deliberately unreliable controls. The controller's response should be consistent. Uncertainty comes from the player's judgement and execution within a changing set of commitments, not from a vehicle that arbitrarily refuses the intended action.

### 3.4. Goals and evidence

| Goal | Evidence to seek | What would challenge the goal |
| --- | --- | --- |
| Understandable basic driving | A beginner can depart, steer, brake and change direction after onboarding, and explain the controls. | Repeated confusion caused by inconsistent inputs rather than a difficult route. |
| Useful advanced movement | Deliberate technique choices improve time, placement or control in suitable situations. | Techniques are unused or add input complexity without changing outcomes. |
| Planning affected by execution | The player changes a route or priority after a setback or successful manoeuvre. | The only consequence is waiting, with no viable alternative. |
| Readable failure | The player can identify the relevant input, obstacle or decision, and the recorded scene supports their account. | Information appears too late or the task was infeasible before the player's error. |
| Recovery with a next action | After correction/reset the player can orient and choose a worthwhile task. | The vehicle moves again but no useful decision remains. |
| Motivation to improve | Players voluntarily replay and describe something they intend to do differently. | Repetition produces resignation rather than learning or interest. |

## 4. Integration of Subsystems

The integration occurs through the car's position and movement within a queue of ageing commitments. Driving into a pickup zone can occupy cargo space; driving into a drop-off resolves a carried order. The player therefore acts on the task system through vehicle movement, without a separate confirmation action. Arrival and execution meet at the same event.

Time spent driving or recovering reduces the remaining opportunity for all active orders. Cargo damage links qualifying collisions to the value of carried work. Route geometry and camera readability determine whether the player can anticipate and execute those journeys. This creates mutual dependencies: a controller cannot be judged only on an empty straight road, and order timers cannot be balanced without considering actual routes.

Capacity below the active-order limit adds pressure to choose what to carry. It is not a universal proof that planning exists, and increasing capacity to match the queue would not remove all decisions about deadlines, route order or reward. A cargo slot also does not process a task independently while the courier leaves. Those distinctions keep the Overcooked comparison precise.

The active controller uses generous ordinary cornering and explicit drift techniques. The earlier friction-circle and rear-axle account belongs to the historical controller; it should not be used to prove current driving trade-offs. Current trade-offs must be demonstrated through measured routes and observed choices.

### 4.1. A delivery situation

Consider an illustrative situation, not a recorded playtest. The courier is carrying two orders. One destination is close but requires turning back; the other lies along the current road. A third order is waiting near the route the courier could take after the second delivery.

There is no useful decision based on urgency alone. The player must compare remaining time with travel and service requirements. Turning back may save the closest order but place the car badly for both remaining jobs. Continuing may protect the current momentum and free cargo space near the third pickup, while sacrificing the opportunity to save the first. The car's position and heading are part of the planning state.

A drift and clean exit may reduce the cost of one route. A collision may change which route is feasible. The resulting reprioritisation is the interaction we need to observe: did the mistake change a decision, or did it merely make the player wait before continuing exactly as before?

This also explains why increasing punishment is not the same as deepening the game. If a collision makes every remaining order impossible, it removes alternatives. A smaller setback that leaves two competing possibilities may produce a richer decision. The useful amount of resistance is enough to make a plan vulnerable while preserving something worth attempting.

END REPLACEMENT

CHECK THIS STEP
Both goals must be explicitly labelled. The hypothesis must be testable and the route scenario must remain illustrative. Do not claim it has been validated.


---

## Prompt 3: Controller and techniques

You are editing the open Google Doc for Zoom Zoom Delivery Co., specifically the GAME DESIGN DOCUMENT, not the project plan or individual reflection. All source text and facts required for this step are included below. You do not need access to local files, GitHub, Unity, earlier chat messages or attachments. Do not ask me to upload those sources or tell me to retrieve facts already supplied here.

Apply only the changes specified in this prompt. The location clues refer to a 46-page PDF exported from this document; use exact headings and nearby text to locate sections, because page numbers change after editing. Search the BODY, not just its table of contents. If a named target cannot be found, do not guess or replace unrelated material: identify the missing target in your response. Complete other unambiguous edits. If you cannot edit the document directly, say so and return the exact formatted replacement for the named target; do not claim the document changed.

Current baseline: the active car uses Crazy Taxi-inspired ground techniques. Rocket League jump/flip/boost-tank descriptions are historical. The player drives through pickup/drop-off zones; stopping is not mandatory for every delivery. Stunt tips, processing bays and R50 repair charges are proposals, not verified implemented features. Current collision cargo damage and successful stranded recovery are implemented and must remain distinct from those proposals. Do not invent tests, screenshots, lecturer quotations, team approvals or book quotations. Use South African English.

The material between BEGIN REPLACEMENT and END REPLACEMENT is finished GDD prose, not instructions to be summarised. Insert it substantially verbatim: only adapt heading numbering, native table formatting and transitions necessary for placement. Keep the analysis, limitations, examples, values and source links. Convert Markdown tables into Google Docs tables. Do not insert this instruction block, placement notes or completion checklist into the GDD. Do not create an “editorial preamble” or a list of changes inside the submitted body. After editing, report the actual headings changed and any unresolved target, outside the document.

EDIT 3: Controller and techniques

EXACT PLACEMENT AND ACTIONS
1. Locate “5. Character Controller Design Rationale” (original pp. 17–18).
2. Replace the text after that heading and BEFORE “5.1. Difficulty Levels” with the content below. This removes the old current-tense paragraphs “Rear-axle handbrake”, “Grounded jump and short directional air flip” and “Boost with recharge”. It also removes duplicated no-manual-acceptance and cargo-capacity rationale from this controller subsection; those principles are handled in Section 4.
3. Preserve “5.1. Difficulty Levels” and “5.2. Collision Response” at this step. Their limited consistency corrections are handled in the final prompt.
4. Use nested headings under Section 5 without renumbering or deleting the two preserved sections. The temporary 5.0.x labels can be replaced with unnumbered nested headings if that better fits the document style.

BEGIN REPLACEMENT

## 5. Character Controller Design Rationale

The car is the player's character: it determines how the player can act on the city and how confidently they can commit to a delivery route. The current controller combines accessible ordinary movement with techniques that change the timing and geometry of a journey. The following descriptions explain their intended use and the conditions under which each could fail to justify its place.

### 5.0.1. Basic controls


W/Up or the right trigger requests forward driving. S/Down or the left trigger brakes forward motion, then requests reverse after the car reaches a near-stop and the 0.18-second direction-change delay completes. Steering uses A/D, the arrow keys or the left stick. Pressing the forward pedal while reversing brakes before changing direction. Both pedals together give braking priority while allowing the engine to rev.

F and R, or gamepad B and A respectively, are technique gestures. They do not force the ordinary forward pedal to become a reverse pedal. Five automatic forward ratios provide acceleration and engine feedback without requiring manual gear changes. Analogue triggers allow partial acceleration and braking; keyboard inputs request the full input amount.

### 5.0.2. Actions and their design purpose

Anthropy and Clark's framework draws attention to verbs, objects, rules, context and the player's dialogue with a game. [*A Game Design Vocabulary*, publisher description and contents](https://www.informit.com/store/game-design-vocabulary-exploring-the-foundational-principles-9780133155228). Our application is to judge a car action by the situation it changes. A verb earns its place when the level and task state give the player a reason to choose it.

### Crazy Dash: committing to an exit

Crazy Dash turns a short input sequence into a strong launch. Its design role is to reward preparation as well as timing. Before accelerating, the player must establish a useful heading and enough space to use the burst. A successful dash after a turn should convert that preparation into progress toward the next task.

Its cost need not be a fuel meter. Speed reduces the time available to correct the line, read a new obstruction or reconsider the route. If dashing is always safe and always fastest, it becomes a repeated input tax rather than an interesting choice. The level must therefore include approaches where waiting to align is better than launching immediately. Testing should compare useful distance gained and subsequent correction, not only the number of dashes performed.

### Crazy Drift: separating facing from travelling

During a drift the chassis can rotate while travel momentum persists. The player is managing two things that ordinary driving largely aligns: where the car points and where it is going. That separation gives the move its expressive role. A corner can be prepared before the car has finished moving through it.

The important moment is the exit. A drift that looks dramatic but leaves the car aimed across the next road has not necessarily helped the delivery. A dash out of a controlled drift can reconnect heading and travel into a purposeful departure. This makes the combination meaningful: the first action prepares a direction; the second commits to it.

Drift should be compared with an ordinary steer-and-brake line on the same route. If ordinary steering completes every relevant corner faster and more safely, the problem may be the geometry or the lack of a useful drift outcome, rather than an insufficiently visible tutorial. Conversely, forcing every corner to require a trick would undermine the accessible basic-driving layer.

### Crazy Stop: precision at the end of speed

Crazy Stop offers stronger deceleration so the player can control where a fast approach ends. It creates a possible rhythm of acceleration, commitment and precise interruption. However, our current delivery zones activate on entry: stopping is not a universal requirement for completing a delivery.

That rule limits the justification for the technique. It can still help before a sharp turn, obstacle or awkward next departure, but the GDD must not claim that every drop-off demands a precise stop. If playtesting shows the move has little useful context, the team should choose deliberately between designing approaches that need it, introducing a low-speed service requirement, or retaining it as a situational move. A low-speed requirement would be a new order-interaction rule and would need its own test.

### Quick turn: choosing what to do after an error

Quick turn provides a direct turnaround rather than an extended reverse manoeuvre. It allows a missed junction to lead to another decision while the player still has agency. It is not the same operation as emergency recovery: the player chooses and performs the turn on the road, while a stranded reset relocates the car to a safe pose.

The current quick turn retains about 90% of entry momentum before other losses. That generosity has an important consequence: it could become a normal routing tool, not merely a rescue move. This is not automatically a defect. The question is whether it produces useful route choices or overwhelms drift, reverse and ordinary turning. Compare all four ways of changing direction where they can serve the same objective. A cooldown alone does not establish that the alternatives remain worthwhile.



### 5.0.3. Technique input and effect table

The table describes this project's current controls. Gamepad letters use the Xbox-style mapping. Recognised techniques require appropriate ground contact and an upright car. Repeatedly holding a button is not repeated trick activation.

| Technique | Input and conditions | Effect | What it rewards |
| --- | --- | --- | --- |
| Crazy Dash | Release the pedals, then press F and W within the 0.30 s window; simultaneous input in either event order is accepted. Gamepad: B and right trigger. Ordinary launch requires speed at or below 8 m/s; a dash can also exit a drift. | A short, strong forward acceleration burst, using a higher speed ceiling than normal driving. A drift-exit dash redirects travel toward the new heading. | Timing a launch and aligning the car before committing to speed; recovering travel time after a pickup, stop or corner. |
| Crazy Drift | At forward speed of at least 5 m/s, press R then F within 0.30 s while steering. Gamepad: A then B while steering. | Enters a low-grip slide with stronger yaw, allowing the chassis to rotate while travel momentum continues. A dash can end the slide with an exit launch. | Choosing an entry line, managing momentum and timing the exit. It is a deliberate manoeuvre rather than random rear-end loss. |
| Crazy Stop | At forward speed of at least 5 m/s, combine S and R; release the accelerator for a clear execution. Gamepad: left trigger and A. | Applies stronger deceleration along the direction of travel, with a configured 85 m/s² stop deceleration. This setting is not a measured stopping time. | Judging a late braking point and making a controlled approach to a destination. |
| Quick turn | Hold A or D and press Space; gamepad: steer and press left bumper. Requires ground contact, an upright car and an available cooldown. No steering means no turn. | Performs a controlled approximately 180-degree turnaround in the selected direction, retaining about 90% of entry momentum before other losses. Collision losses are not restored. | Recognising a wrong route or overshoot and committing to recovery promptly. It provides an accessible turnaround without replacing the drift's cornering role. |

The implemented rewards here are movement advantages: time saved, positioning and recovery. The final submission proposes stunt tips and chained monetary rewards, but this inspection did not establish an implemented stunt-tip system. Do not describe each trick as awarding money until that connection is built and tested. Technique-related deceleration is protected from the ordinary hard-braking cargo penalty by default; real collisions remain damaging.

END REPLACEMENT

CHECK THIS STEP
There must be full rationale AND a four-row technique table, with actual keyboard/controller inputs, entry conditions, effects and rewards. No monetary stunt rewards may be presented as already implemented.


---

## Prompt 4: Vehicle measurements

You are editing the open Google Doc for Zoom Zoom Delivery Co., specifically the GAME DESIGN DOCUMENT, not the project plan or individual reflection. All source text and facts required for this step are included below. You do not need access to local files, GitHub, Unity, earlier chat messages or attachments. Do not ask me to upload those sources or tell me to retrieve facts already supplied here.

Apply only the changes specified in this prompt. The location clues refer to a 46-page PDF exported from this document; use exact headings and nearby text to locate sections, because page numbers change after editing. Search the BODY, not just its table of contents. If a named target cannot be found, do not guess or replace unrelated material: identify the missing target in your response. Complete other unambiguous edits. If you cannot edit the document directly, say so and return the exact formatted replacement for the named target; do not claim the document changed.

Current baseline: the active car uses Crazy Taxi-inspired ground techniques. Rocket League jump/flip/boost-tank descriptions are historical. The player drives through pickup/drop-off zones; stopping is not mandatory for every delivery. Stunt tips, processing bays and R50 repair charges are proposals, not verified implemented features. Current collision cargo damage and successful stranded recovery are implemented and must remain distinct from those proposals. Do not invent tests, screenshots, lecturer quotations, team approvals or book quotations. Use South African English.

The material between BEGIN REPLACEMENT and END REPLACEMENT is finished GDD prose, not instructions to be summarised. Insert it substantially verbatim: only adapt heading numbering, native table formatting and transitions necessary for placement. Keep the analysis, limitations, examples, values and source links. Convert Markdown tables into Google Docs tables. Do not insert this instruction block, placement notes or completion checklist into the GDD. Do not create an “editorial preamble” or a list of changes inside the submitted body. After editing, report the actual headings changed and any unresolved target, outside the document.

EDIT 4: Vehicle measurements

EXACT PLACEMENT AND ACTIONS
1. Find the current “5. Character Controller Design Rationale”. Insert the complete subsection below immediately AFTER its technique input/effect table and supporting paragraph, and BEFORE “5.1. Difficulty Levels”.
2. If this exact new subsection already exists, replace it rather than creating a duplicate.
3. All data and conditions are included below. Do not request CSV files and do not say the numbers were measured by you. These are saved 26 October 2026 measurements provided by the project review.
4. Keep the methods with the table. Do not reduce the corner result to “maximum corner speed” without the geometry and pass criteria.

BEGIN REPLACEMENT

### Vehicle lab: measurements and design implications

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

#### What these results mean for the design

The saved 26 October lab results give an ordinary top speed of 100.8 km/h, 2.842 seconds to 99% of that ceiling, 0.917 seconds for ordinary braking to the test's stopped threshold, and 12.736 m stopping distance. The tightest measured whole-car swept turning diameter is 5.827 m. These results describe a rapid, manoeuvrable vehicle; they do not support keeping every earlier claim about a difficult, traction-limited controller.

The most revealing result is the tested 90-degree bend: on a 12 m-wide road with 6 m inner and 18 m outer radius, the car enters at 100.8 km/h and exits at 91.405 km/h without a trick. On that geometry, cruise-speed turning does not require the player to spend speed before entering. A design that expects every right-angle corner to force a brake-versus-speed decision is contradicted by this result.

This is useful evidence rather than an embarrassment. It tells us where to look next. The challenge may need to come from tighter approaches, adjacent decisions, exit alignment or the consequences of boosted speed. It also means a drift demonstration must compare useful route outcomes rather than assume that a special move is inherently superior.

The whole-car turning diameter gives a physical clearance baseline, not a recommended street width. A route also needs room for approach error, steering correction, props and an intelligible camera view. Likewise, stopping distance excludes noticing and deciding. The practical distance needed to make an informed stop includes distance travelled while the player recognises the situation.

The following measurements are saved evidence from 26 October 2026; a new successful measurement session was not performed during this documentation review.

END REPLACEMENT

CHECK THIS STEP
Confirm five required measurements, 99% definition, 12.736 m stopping distance, whole-car versus centre-path distinction, exact corner geometry and limits, and the finding that this wide corner works without a trick. No fresh-test claim.


---

## Prompt 5: Camera and engine

You are editing the open Google Doc for Zoom Zoom Delivery Co., specifically the GAME DESIGN DOCUMENT, not the project plan or individual reflection. All source text and facts required for this step are included below. You do not need access to local files, GitHub, Unity, earlier chat messages or attachments. Do not ask me to upload those sources or tell me to retrieve facts already supplied here.

Apply only the changes specified in this prompt. The location clues refer to a 46-page PDF exported from this document; use exact headings and nearby text to locate sections, because page numbers change after editing. Search the BODY, not just its table of contents. If a named target cannot be found, do not guess or replace unrelated material: identify the missing target in your response. Complete other unambiguous edits. If you cannot edit the document directly, say so and return the exact formatted replacement for the named target; do not claim the document changed.

Current baseline: the active car uses Crazy Taxi-inspired ground techniques. Rocket League jump/flip/boost-tank descriptions are historical. The player drives through pickup/drop-off zones; stopping is not mandatory for every delivery. Stunt tips, processing bays and R50 repair charges are proposals, not verified implemented features. Current collision cargo damage and successful stranded recovery are implemented and must remain distinct from those proposals. Do not invent tests, screenshots, lecturer quotations, team approvals or book quotations. Use South African English.

The material between BEGIN REPLACEMENT and END REPLACEMENT is finished GDD prose, not instructions to be summarised. Insert it substantially verbatim: only adapt heading numbering, native table formatting and transitions necessary for placement. Keep the analysis, limitations, examples, values and source links. Convert Markdown tables into Google Docs tables. Do not insert this instruction block, placement notes or completion checklist into the GDD. Do not create an “editorial preamble” or a list of changes inside the submitted body. After editing, report the actual headings changed and any unresolved target, outside the document.

EDIT 5: Camera and engine

EXACT PLACEMENT AND ACTIONS
1. Locate the subsection called “6.4. Vehicle and Camera” (original p. 21), following the empty “6.3. Source Game Three, Crazy Taxi” heading. Its old body starts “[HISTORICAL NOTE...]” and “We will start with a simple placeholder vehicle”.
2. Replace that subsection's body through the paragraph before “6.5. Orders and Timing” with the two supplied sections below. Keep its parent heading “Vehicle and Camera”; use the supplied headings beneath it.
3. If the old body has historical value, move it to “Historical controller and camera prototype” at the end; do not leave it as the only detailed current vehicle explanation.
4. The 7 m aim offset is not a visibility limit. Delete any sentence within this target that equates aim distance with visible road length, or guarantees that every corner is visible before braking is necessary. Do not invent screenshot evidence.

BEGIN REPLACEMENT

### Engine and vehicle implementation

The project uses Unity 6000.5.4f1. The controller combines a Rigidbody and body collider with four downward suspension rays, custom movement rules and a 120 Hz physics step. The suspension rays detect support and surface information. The custom controller supplies the acceleration, braking, steering, grip and technique behaviour; Unity resolves physical contact with the environment. This division makes handling parameters directly adjustable while retaining collision and ramp interaction.

Input events are captured with timestamps and consumed by the controller, allowing short technique sequences to survive between physics updates. Pending sequences are cleared on events such as teleporting and disabling input, so a stored button sequence cannot unexpectedly activate after recovery. The car's lowered centre of mass and grounded anti-roll assistance support readable road movement. Ramps can launch the vehicle physically, but there is no active jump button, directional aerial flip or airborne propulsion in the current taxi controller.

This is a reconstruction of arcade driving behaviour, not a claim to reproduce Sega's original source code. Legacy jump, boost and camera fields still exist in serialized data for compatibility; their presence does not mean those older mechanics remain active. Current taxi settings take precedence.

### Camera design: information before commitment

The camera determines which decisions the player can make in time. Before a dash, the player needs to judge the departure space. During a drift, they need to read both their movement and the exit. After a collision, they need to understand the car's new orientation and a possible way back into the route. A camera can remain technically outside a wall while still failing all three tasks.

The current chase camera follows above and behind the car, keeps a level horizon and eases heading changes. During a forward drift it partly frames travel direction, helping the player read the path while the chassis rotates. Speed widens the field of view within a bounded range. These are communication choices: the view should convey motion without making the car feel remote or making the road difficult to judge.

Wall avoidance uses overlap resolution and sphere sweeps with near-plane clearance. Obstructions pull the view inward and the camera returns outward smoothly when clear. This addresses physical clipping, but a tightly obstructed camera can still become too close to read an exit. Dense corners therefore need a separate readability test in the rendered level.

The current 7 m look-ahead value is an offset of the point toward which the camera aims. It is not a claim that only 7 m of road is visible. Visible and readable distance depends on camera placement, field of view, elevation and occlusion. Comparing that number directly with stopping distance cannot establish an execution bottleneck or justify reverting to the old 28 m value.

A stronger test records when the relevant turn or obstacle first becomes recognisable and how much time remains before the player reaches it. Repeat it during ordinary driving, a dash approach and a drift exit. If the player has already committed before the exit becomes readable, the issue may require a landmark, changed geometry or different framing, rather than blaming their reaction time.

#### Current camera behaviour and settings

The camera follows behind and above the car using smoothed position and heading changes. Its horizon uses world-up, so chassis roll does not tip the entire view. Current taxi settings place it 5.6 m behind and 2.1 m above the car, with an aim height of 1.15 m and 7 m look-ahead. The field of view eases from 65 to 74 degrees as ordinary speed increases. Speed can add 0.6 m follow distance, and acceleration up to another 0.3 m, giving bounded speed feedback rather than an uncontrolled retreat.

To avoid wall clipping, the camera checks for overlap around its aim pivot and sweeps a sphere toward the intended viewing position. Clearance includes the camera's near-plane corners, rather than checking only a single line. An obstruction pulls the camera inward immediately; its normal distance returns smoothly after the obstruction clears. Another sweep checks the adjusted position, and the follow spring resumes from the safe point. The checks ignore the car's own colliders and triggers, and depend on correctly configured solid colliders and camera layers.

Look-ahead lets the player read the road ahead. During a forward drift, the camera partially follows travel direction as well as the car's nose, helping keep the exit road visible as the chassis rotates. This helps corner readability; it does not let the player see through a building or guarantee visibility around every blind bend. The normal reverse view keeps its forward chase orientation; holding C or right-stick click provides look-behind. Recovery snaps the camera to the restored vehicle rather than dragging the view across the map.

The design goal is to communicate speed, direction and available road without making the car feel detached from the player. Actual city corners still need rendered playtesting for visibility and comfort; the saved automated collision checks cannot establish those experiential outcomes.

END REPLACEMENT

CHECK THIS STEP
Preserve camera distances, FOV, drift framing, overlap/sphere sweep explanation, near-plane protection, look-behind, and actual readability test. No claim that the camera sees through buildings or that Unity cannot support bespoke arcade physics.


---

## Prompt 6: Damage and recovery

You are editing the open Google Doc for Zoom Zoom Delivery Co., specifically the GAME DESIGN DOCUMENT, not the project plan or individual reflection. All source text and facts required for this step are included below. You do not need access to local files, GitHub, Unity, earlier chat messages or attachments. Do not ask me to upload those sources or tell me to retrieve facts already supplied here.

Apply only the changes specified in this prompt. The location clues refer to a 46-page PDF exported from this document; use exact headings and nearby text to locate sections, because page numbers change after editing. Search the BODY, not just its table of contents. If a named target cannot be found, do not guess or replace unrelated material: identify the missing target in your response. Complete other unambiguous edits. If you cannot edit the document directly, say so and return the exact formatted replacement for the named target; do not claim the document changed.

Current baseline: the active car uses Crazy Taxi-inspired ground techniques. Rocket League jump/flip/boost-tank descriptions are historical. The player drives through pickup/drop-off zones; stopping is not mandatory for every delivery. Stunt tips, processing bays and R50 repair charges are proposals, not verified implemented features. Current collision cargo damage and successful stranded recovery are implemented and must remain distinct from those proposals. Do not invent tests, screenshots, lecturer quotations, team approvals or book quotations. Use South African English.

The material between BEGIN REPLACEMENT and END REPLACEMENT is finished GDD prose, not instructions to be summarised. Insert it substantially verbatim: only adapt heading numbering, native table formatting and transitions necessary for placement. Keep the analysis, limitations, examples, values and source links. Convert Markdown tables into Google Docs tables. Do not insert this instruction block, placement notes or completion checklist into the GDD. Do not create an “editorial preamble” or a list of changes inside the submitted body. After editing, report the actual headings changed and any unresolved target, outside the document.

EDIT 6: Damage and recovery

EXACT PLACEMENT AND ACTIONS
1. Locate “5.3. Crash Damage and Recovery Tables” (original p. 20).
2. Replace its body, including the existing damage table and the paragraph beginning “Following game design theories”, up to the paragraph starting “Constraint to watch: displaced objects must not become a faster route.” Preserve that following constraint paragraph.
3. If that ending paragraph is absent, replace only this subsection up to the next same-level or higher-level heading. Do not delete the entire Core Gameplay Loop.
4. Delete the unsupported R20 charge, 2.5-second steering stun, fixed 15%/35% impact tiers and five-second mandatory crash delay. Replace them with all three tables and the analysis below. They represent different systems, not alternative names for one punishment.
5. The equation uses relative impact speed, not the speedometer and not the logger's definition of a wall hit.

BEGIN REPLACEMENT

### 5.3. Crash Damage and Recovery

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

An earlier final-submission design proposal specifies R50 per hard crash but leaves the trigger as “8–10 m/s”. The following is a concrete **proposed clarification**, choosing the upper end to align with the existing cargo threshold. It is not an implemented or approved rule.

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

#### Design implications: preserving a next move

A collision can already cost velocity, route position and time from several active orders. Cargo damage adds a loss to the value being carried. A proposed repair fee would add another charge. These costs interact; they should not be balanced as isolated systems.

In the inspected Easy, Medium and Hard scenes, a collision removes 1.5 cargo-condition points per m/s of relative impact speed above 10 m/s, down to zero condition. All carried orders are affected. This is a continuous cargo-value rule, not the new PDF's 15%/35% damage bands. The logger's speed-drop definition of a wall hit is another measurement entirely and is not the collision damage formula.

The proposed R50 repair fee would change behaviour beyond cargo damage. Cargo damage matters only when carrying something. A repair fee could make an empty journey financially risky as well. A flat fee would also introduce a threshold: two impacts just above the threshold would cost twice as much as one much faster impact. That may be acceptable, but it is a design consequence that needs a reason and feedback the player can read.

If stunt tips are introduced, their relationship to repair costs becomes central. A risk-reward system needs attainable upside, identifiable downside and some capacity to choose the exposure. Without the upside, additional penalties may simply teach careful driving. If reckless driving always returns more profit even after crashes, caution becomes irrelevant. Compare net outcomes and voluntary choices, not just how many penalties fired.

Stranded recovery serves a different purpose. The implemented safe-position reset prevents an overturned or blocked car from consuming the remainder of the shift. A successful reset subtracts five seconds from the active shift; it does not freeze steering for five seconds. The player resumes with less time available. That preserves a cost while restoring the ability to act.

There are therefore three recoveries to evaluate: restoring vehicle control, restoring orientation in the city, and restoring a viable plan. Getting upright is necessary but insufficient if the player cannot find the next useful destination. A satisfying recovery ends with an intentional next action, not just a moving Rigidbody.

END REPLACEMENT

CHECK THIS STEP
Current formula and examples must remain exact. R50 above 10 m/s is explicitly a proposed clarification, not approval or implementation. Recovery costs shift time after successful relocation; it does not impose a steering stun. Stacking repairs and cargo damage remains unresolved.


---

## Prompt 7: Core loop and evaluation

You are editing the open Google Doc for Zoom Zoom Delivery Co., specifically the GAME DESIGN DOCUMENT, not the project plan or individual reflection. All source text and facts required for this step are included below. You do not need access to local files, GitHub, Unity, earlier chat messages or attachments. Do not ask me to upload those sources or tell me to retrieve facts already supplied here.

Apply only the changes specified in this prompt. The location clues refer to a 46-page PDF exported from this document; use exact headings and nearby text to locate sections, because page numbers change after editing. Search the BODY, not just its table of contents. If a named target cannot be found, do not guess or replace unrelated material: identify the missing target in your response. Complete other unambiguous edits. If you cannot edit the document directly, say so and return the exact formatted replacement for the named target; do not claim the document changed.

Current baseline: the active car uses Crazy Taxi-inspired ground techniques. Rocket League jump/flip/boost-tank descriptions are historical. The player drives through pickup/drop-off zones; stopping is not mandatory for every delivery. Stunt tips, processing bays and R50 repair charges are proposals, not verified implemented features. Current collision cargo damage and successful stranded recovery are implemented and must remain distinct from those proposals. Do not invent tests, screenshots, lecturer quotations, team approvals or book quotations. Use South African English.

The material between BEGIN REPLACEMENT and END REPLACEMENT is finished GDD prose, not instructions to be summarised. Insert it substantially verbatim: only adapt heading numbering, native table formatting and transitions necessary for placement. Keep the analysis, limitations, examples, values and source links. Convert Markdown tables into Google Docs tables. Do not insert this instruction block, placement notes or completion checklist into the GDD. Do not create an “editorial preamble” or a list of changes inside the submitted body. After editing, report the actual headings changed and any unresolved target, outside the document.

EDIT 7: Core loop and evaluation

EXACT PLACEMENT AND ACTIONS
Apply the following separate, bounded changes. The replacement contains labelled components for each destination; do not paste the entire block into one location.
1. Replace “6.1. Source Game One, Rocket League” BODY with “Source controller: Crazy Taxi, with a historical Rocket League lineage” and its two paragraphs. Rename that subsection accordingly.
2. Replace “6.2. Source Game Two, Overcooked” BODY with “Overcooked: concurrent work and the limits of the adaptation” and its paragraph. Remove the now-duplicate EMPTY heading “6.3. Source Game Three, Crazy Taxi”; do not remove a nonempty section without checking whether its contents are already preserved by Prompt 1 or the source-controller text.
3. Replace “6.9. Combined Core Loop” (original p. 23), up to but not including “Systems and Mechanics”, with the supplied Combined core loop paragraphs. They preserve the useful position/time/earnings argument.
4. In the “Systems and Mechanics” table (original pp. 24–26), replace ONLY the Vehicle physics and Chase camera rows using the supplied rows. Add Recovery and Collision cargo damage rows if absent; otherwise update their existing rows. Preserve all other rows and images.
5. Replace “6.10. System Interaction” BODY, up to “7. Playtesting Documentation”, with the System interaction paragraph.
6. In “7.1. What We Will Record During Testing”, replace the outdated goals/evidence table with the supplied Current controller evaluation table and its final paragraph. Keep original recorded results and participant quotations in later subsections. Add a sentence immediately before those historical results identifying that their boost/jump/flip metrics describe the earlier controller and do not validate the current taxi techniques.
7. Do not set a new shift length, order spawn interval or XP threshold. This prompt does not supply verified replacement values for those other-team systems. Report any conflicting values externally for reconciliation.

BEGIN REPLACEMENT

### Source controller: Crazy Taxi, with a historical Rocket League lineage

The current vehicle loop is to read a route, establish a line, execute ordinary driving or a deliberate technique, judge the resulting position and choose the next action. Crazy Taxi supplies the current reference for accessible arcade motion with timed dash/drift techniques. The project reconstructs observable action relationships rather than Sega's source code or exact tuning values. Automatic forward ratios, brake-to-reverse assistance, a direct quick turn and safe-position recovery are project adaptations.

Rocket League informed the earlier prototype's jump, flip, boost and momentum-recovery experiments. Those details belong to the iteration record. The current controller does not retain active jump, aerial flip or the refillable boost tank.

### Overcooked: concurrent work and the limits of the adaptation

The order reference concerns work that continues ageing while the player attends elsewhere. This forces comparison of commitments rather than allowing each journey to be considered in isolation. Cargo occupancy adds a capacity constraint, but a car carrying an order is not a cooking station processing it independently. Proposed processing bays would create that additional scheduling structure and must be documented as a separate extension until implemented.

### Combined core loop

An order appears and its deadline starts. The player compares the live work, their position and heading, available cargo space and the route needed to complete a job. Entering a valid pickup zone loads the order if capacity permits. Driving toward its destination changes the time left on every active order. Ordinary controls and techniques affect how efficiently the route is executed; qualifying collisions can reduce carried cargo condition. Entering the matching drop-off resolves the delivery according to the order's reward rules. The player then reassesses the remaining opportunities from their new position.

Position carries between deliveries: finishing a job leaves the courier somewhere that may favour another route. Time carries: work that was not served continues ageing. Earnings carry: losses affect the result of a shift that is still ongoing. What matters is whether the player can use that changing state to make another worthwhile decision. A crash that changes which job to attempt is more consequential to planning than one that merely adds a delay to an otherwise fixed sequence.

The inspected delivery-resolution code calculates base pay, then applies an early bonus or late deduction based on patience remaining, and applies cargo condition to that adjusted value. Exact reward amounts depend on tuning. This is not evidence of a separate stunt-tip system. Proposed repairs, stunt chains and processing bays must not be described as already producing this loop.

### System interaction

Vehicle movement and district geometry determine the time and positioning needed to reach a destination. The order queue determines deadlines and opportunities. Cargo capacity affects which commitments can be carried together, and cargo condition links collision consequences to delivery value. The camera and navigation feedback determine whether those choices are perceptible in time. The shift timer also interacts with stranded recovery: a successful reset removes five seconds from the active shift, so it is not an isolated system coupled only at session end.

### Current vehicle and camera implementation rows

| System | Current supported description | Limits or work still requiring evaluation |
| --- | --- | --- |
| Vehicle controller | Custom arcade controller on a Rigidbody with four suspension rays; ordinary driving plus Crazy Dash, Crazy Drift, Crazy Stop and quick turn; automatic forward ratios; current normal cruise ceiling 28 m/s. | Retired jump/flip/boost fields are not active features. Technique usefulness needs route comparisons and player observation. |
| Camera | Automatic chase view, stable horizon, smoothed follow/heading, 7 m aim offset, drift travel-direction framing, bounded speed FOV, collision sweeps and manual look-behind. | Aim offset is not visible road distance. Corner readability and comfort require rendered playtesting. |
| Recovery | Safe-road-pose reset for stranded/overturned conditions; successful reset clears motion and deducts five seconds from the active shift. | Check orientation and next viable route after recovery, not just restored motion. |
| Collision cargo damage | Easy/Medium/Hard: max(0, relative impact speed in m/s − 10) × 1.5 condition points per carried order, limited by remaining condition. | Proposed repair fees and stunt-tip rewards are separate unimplemented/unchecked extensions. |

### Current controller evaluation

| Claim to evaluate | Comparison or observation | Finding that would challenge it |
| --- | --- | --- |
| Advanced moves create useful mastery. | Compare a route using ordinary controls with the same route using a learned technique; record completion, mistakes and the player's reason for choosing it. | More input complexity without a useful change in time, position or control. |
| Quick turn supports recovery without erasing alternatives. | Compare quick turn, reverse, drift and normal turning at missed junctions. | One move is consistently preferable across every relevant context. |
| Driving and order planning affect one another. | Observe the intended next job before and after an actual driving setback. | The player repeats the same action with added delay, with no viable alternative or meaningful revision. |
| The camera supports informed commitment. | Record first recognition of turns and obstacles under different approaches. | The relevant information appears only after avoiding failure becomes impractical. |
| Damage makes cargo matter. | Compare empty and loaded journeys and ask why the player changed their driving. | No behavioural distinction, or players become so cautious that the techniques cease to be worthwhile. |
| Recovery preserves engagement. | Observe the next intentional action and willingness to continue after a reset. | Reset restores the car but leaves the player lost, resigned or unable to save anything. |

Use repeated route setups for controller comparisons, while separating beginner and experienced-player observations. Pair recorded behaviour with explanations in the player's own words. A high number of crashes alone does not prove entertaining difficulty, and a low number does not prove the absence of challenge. The question is whether the player can perceive, choose and improve.

END REPLACEMENT

CHECK THIS STEP
Changes must land in their separate named destinations. Retain historical test results as history. Do not imply newly executed tests or alter unrelated order/level implementation rows.


---

## Prompt 8: Production and final consistency

You are editing the open Google Doc for Zoom Zoom Delivery Co., specifically the GAME DESIGN DOCUMENT, not the project plan or individual reflection. All source text and facts required for this step are included below. You do not need access to local files, GitHub, Unity, earlier chat messages or attachments. Do not ask me to upload those sources or tell me to retrieve facts already supplied here.

Apply only the changes specified in this prompt. The location clues refer to a 46-page PDF exported from this document; use exact headings and nearby text to locate sections, because page numbers change after editing. Search the BODY, not just its table of contents. If a named target cannot be found, do not guess or replace unrelated material: identify the missing target in your response. Complete other unambiguous edits. If you cannot edit the document directly, say so and return the exact formatted replacement for the named target; do not claim the document changed.

Current baseline: the active car uses Crazy Taxi-inspired ground techniques. Rocket League jump/flip/boost-tank descriptions are historical. The player drives through pickup/drop-off zones; stopping is not mandatory for every delivery. Stunt tips, processing bays and R50 repair charges are proposals, not verified implemented features. Current collision cargo damage and successful stranded recovery are implemented and must remain distinct from those proposals. Do not invent tests, screenshots, lecturer quotations, team approvals or book quotations. Use South African English.

The material between BEGIN REPLACEMENT and END REPLACEMENT is finished GDD prose, not instructions to be summarised. Insert it substantially verbatim: only adapt heading numbering, native table formatting and transitions necessary for placement. Keep the analysis, limitations, examples, values and source links. Convert Markdown tables into Google Docs tables. Do not insert this instruction block, placement notes or completion checklist into the GDD. Do not create an “editorial preamble” or a list of changes inside the submitted body. After editing, report the actual headings changed and any unresolved target, outside the document.

EDIT 8: Production and final consistency

EXACT PLACEMENT AND ACTIONS
1. Under “Design Process thus far”, insert “Vehicle and camera: production responsibility and iteration evidence” immediately BEFORE the existing subsection “Vehicle” (original pp. 35–36). Rename the existing Vehicle subsection “Historical vehicle iteration: Rocket League-inspired prototype”. Preserve its genuine development history and images, but remove any unverified claim that specific test outcomes have been demonstrated in the new controller.
2. Do not replace the group “Process and Collaboration” section or manufacture an individual reflection. The new production discussion belongs to the design record.
3. Put the Source and evidence note before the bibliography. Add the supplied source entries without deleting existing game references or asset credits. Do not invent missing course-edition details.
4. Remove the accidental “Appendix: Editorial Preamble & Evidence Index” introductory paragraph from the front matter and remove the final section “Inserted Headings & Contradictions Requiring Human Decisions”. Those are editing artefacts, not design content. Keep any genuinely useful evidence in a properly labelled appendix.
5. In the vehicle glossary, mark Friction circle, Grip budget, old 13-grip/0.58–0.42 grip-balance figures, rear-handbrake, boost-tank, flip and supersonic references as HISTORICAL where applicable, or move them to the historical appendix. Add these definitions: Crazy Dash—timed acceleration technique; Crazy Drift—deliberate rotation while retaining travel momentum; Crazy Stop—strong technique deceleration; Quick turn—steering-directed grounded turnaround; Stranded reset—safe-pose relocation with a five-second shift deduction after success; Camera look-ahead—offset of the camera's aim point, not measured visible road length.
6. In “5.1. Difficulty Levels”, replace the absolute claim that equal urgency creates no choice with: “Equal urgency does not eliminate choice: destination location, value, approach direction and expected travel time can still distinguish orders. The test should examine whether layout changes create different decisions rather than assuming that more density necessarily produces more meaningful reprioritisation.” Preserve the actual scene descriptions, but mark any claim that denser scenes definitely produce more interesting decisions as a hypothesis requiring testing.
7. In “5.2. Collision Response”, retain the explanation of physical props but soften any claim that object displacement proves fairness to: “Object displacement provides a visible consequence of contact. Whether players can correctly read the cause and severity remains a playtest question.”
8. Search CURRENT-DESIGN sections only for retired jump/flip/boost-tank claims, 32 m/s normal top speed, 28 m camera visibility, mouse swivel, rear-grip percentages, the R20 fee, steering stuns and compulsory crash delays. Correct them to the facts established in this prompt series. Preserve explicitly historical records with dates/status labels.
9. Search for claims that cargo capacity equal to active orders eliminates all choice, that fixed timers guarantee fairness, or that every failed delivery can only be blamed on driving. Correct to the narrower arguments supplied earlier. If the earlier prompts have not been run, do not invent missing replacements: report that those sections still need their numbered edit.
10. Regenerate the table of contents and normalise heading numbering after the edits. Do not type guessed page numbers. If you cannot update the contents field, report that limitation outside the document.
11. Leave unresolved shift duration, active-order timing, progression status, economy stacking and final-build verification as explicit questions in your RESPONSE, not fabricated answers in the GDD.

BEGIN REPLACEMENT

### Vehicle and camera: production responsibility and iteration evidence

The custom Unity controller combines body collisions and four-ray suspension with authored arcade acceleration, steering and technique states. This division lets the project retain interaction with roads and props while deciding what a dash, drift or stop should do. Unity is capable of such custom behaviour; ordinary physics settings simply do not supply the intended action grammar by themselves.

The production value of that arrangement is iteration. A parameter change can be isolated, measured and compared before asking the level designer to adapt streets around it. But a controller change also invalidates dependent evidence. Retiring the aerial flip means old flip tests no longer demonstrate the current recovery design. Changing steering means old road-clearance assumptions need checking. A measurement is useful only when its controller version and conditions are known.

Chandler's *The Game Production Handbook* organises production around planning, implementation, validation and learning from the project. [Third-edition overview and contents](https://www.oreilly.com/library/view/the-game-production/9781449688097/). Applied here, the contribution is not proved by the quantity of code or repository rules. It is shown through a traceable decision: what problem was identified, what changed, what evidence followed and what that evidence required from the rest of the team.

For the vehicle lead, the handover therefore includes the active control rules, tuning version, lab method, measured clearances, camera constraints and remaining uncertainties. Level and order designers need those facts to make compatible decisions. Document only handovers and approvals that actually occurred; attach genuine records rather than writing a persuasive retrospective that the evidence cannot support.

### Source and evidence note

The course connections use Bogost's account of configurative units, Anthropy and Clark's vocabulary of actions and context, and Chandler's production-cycle framing. The specific analysis of this vehicle is the team's application of those ideas, not a quotation from the books. Formal book references should use the editions assigned by the course; no unverified page numbers or quotations are supplied here.

Vehicle measurement values are attributed to the saved 26 October 2026 lab record. A fresh successful run was not obtained during the documentation review. The implementation description reflects the inspected current project; proposed economy extensions must be verified separately before being described as built.

### Sources to add to the bibliography

- Bogost, Ian. Unit Operations: An Approach to Videogame Criticism. Author overview: https://bogost.com/books/unit-operations/ . Add the publication details of the course edition once confirmed.
- Anthropy, Anna, and Naomi Clark. A Game Design Vocabulary: Exploring the Foundational Principles Behind Good Game Design. Addison-Wesley Professional, 2014. Publisher description and contents: https://www.informit.com/store/game-design-vocabulary-exploring-the-foundational-principles-9780133155228 .
- Chandler, Heather Maxwell. The Game Production Handbook. Edition to match the course copy. Third-edition overview and contents consulted: https://www.oreilly.com/library/view/the-game-production/9781449688097/ . The author's surname is Chandler, not Gardner.
- Sega. Crazy Taxi. 1999. Video game; current controller reference, not a claim of source-code access.
- 3DM. “TGS Interview with the Producer of Crazy Taxi: High-Speed Tour: Fun is Our Top Priority!” 23 September 2026. https://en.3dmgame.com/original/4658 . Used for the creator's account of authored driving action, not original physics constants.
- Unity Technologies. Built-in 3D physics. https://docs.unity3d.com/6000.0/Documentation/Manual/PhysicsOverview.html . Used for the PhysX integration statement.

END REPLACEMENT

CHECK THIS STEP
Verify the doc contains the two controller goals, all four named techniques with controls, all five lab measures with conditions, full design analysis and separate implemented/proposed damage. Report unresolved facts outside the body. Do not claim to have read the complete course books or run the game.
