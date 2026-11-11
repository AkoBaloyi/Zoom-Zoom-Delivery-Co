# The car as a delivery decision

Design review and replacement prose for Ako's GDD contribution, 29 October 2026.

## Editorial review — not submission prose

The updated 46-page GDD needs a coherent current-design argument, not more sections added around the old one. The strongest existing passage is the discussion of what carries between deliveries: position, time and earnings. That passage explains relationships and consequences. Use that standard throughout the controller chapter.

The earlier documentation pack supplied implementation evidence but did not develop that argument sufficiently. The updated PDF then omitted much of the evidence and introduced new unsupported mechanics. Its problems are both editorial and substantive:

| Location in the updated PDF | Problem | Required correction |
| --- | --- | --- |
| pp. 2–3 | Editorial instructions appear inside the front matter; Crazy Taxi analysis is inserted into the contents area. | Move editorial notes outside the submission and regenerate the contents after the rewrite. |
| pp. 8–18 | Synopsis, hypothesis, integration and controller rationale still describe Rocket League handling as current. | State the current Crazy Taxi direction consistently. Preserve Rocket League as a dated earlier iteration. |
| p. 17 | The controller chapter justifies the retired handbrake/jump/flip design. | Replace with the current driving vocabulary and the reasoning below. |
| p. 20 | The new crash table invents an R20 charge, 2.5-second steering stun, stepped 15%/35% damage and a mandatory crash delay. | Remove these claims. Current cargo damage is a continuous formula; successful stranded recovery subtracts five seconds from the shift. A repair fee remains proposed. |
| p. 21 | “Source Game Three, Crazy Taxi” is empty. | Explain the pivot and the function of the borrowed operations. Avoid presenting three equally current source systems when one controller replaces another. |
| pp. 16, 24, 46 | Camera aim distance is treated as metres of visible road. | A 7 m aim offset is not a 7 m visibility limit. Measure actual readable road and decision time in the rendered scene. |
| pp. 24–27 | Current implementation and testing tables still list retired controller features. | Separate historical evidence, current implementation, current measurements and proposed tests. |
| Entire PDF | No occurrences of Crazy Dash, Crazy Drift or Crazy Stop; no 2.842 s acceleration or 5.827 m turning-circle result. | Insert the verified technique and lab tables from the companion documentation file. |
| p. 46 | A list of editing instructions and unresolved issues is presented as finished content. | Keep it as an external review checklist, not a concluding GDD section. |

Several existing arguments also overclaim. Cargo capacity equal to active orders does not eliminate every decision: route order, deadlines, reward and travel distance can still compete. Equal urgency does not make two destinations equivalent. Fixed timers do not guarantee fairness, and a player's failure is not automatically their fault. A camera, route layout, unclear rule or impossible deadline can invalidate an otherwise reasonable action. The documentation should test these possibilities rather than rule them out by assertion.

The text below is replacement design prose. It distinguishes the current implementation from design interpretation and future proposals. It does not fabricate a development meeting, a playtest finding or an approved team decision.

---

## 1. Design position: being quick enough to change the plan

The player in Zoom Zoom Delivery Co. is trying to keep a working plan alive while driving changes the conditions on which that plan was based. An order can be reachable when it appears and cease to be sensible after a missed corner. Conversely, a good exit from a junction can make an additional pickup worth attempting. The car therefore does more than carry the player between objectives. It changes which objectives remain possible.

The intended character is a capable courier working close to the limit of what they can manage. The player should want to take one more order because a better line, a well-timed dash or a quicker recovery might make it achievable. Pressure comes from that possibility. If success is guaranteed, there is little to judge; if failure is inevitable, there is little reason to improve. The design needs an interval in which the player's growing ability changes the answer.

This gives the car a personality through action. Ordinary driving is forgiving enough to establish confidence. Advanced moves let the player express decisiveness and control. The order queue gives those movements consequences: a fast turn is valuable because of what it makes reachable next. The desired satisfaction is not simply “the vehicle moves quickly”. It is “I saw a way to save that delivery, and I executed it”.

**Controller design goal:** provide reliable basic driving and distinct, learnable techniques that let the player improve routes, approaches and recovery. Each advanced action should solve a recognisable driving problem and retain a reason not to use it in some situations.

**Controller experience goal:** make the player feel increasingly resourceful under delivery pressure. Success should feel attributable to a decision and its execution; failure should remain understandable and leave a meaningful next action.

These goals do not require deliberately unreliable controls. The controller's response should be consistent. Uncertainty comes from the player's judgement and execution within a changing set of commitments, not from a vehicle that arbitrarily refuses the intended action.

## 2. Reading the splice through Unit Operations

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

## 3. Why the controller pivot matters

The shift from the Rocket League-inspired prototype to the Crazy Taxi-inspired controller changes where we expect expertise to appear. The earlier document puts weight on instability, jump and flip recovery. The current controller puts greater weight on ordinary steering, ground-based techniques and repeatable input sequences. Keeping both descriptions in the present tense hides that design change.

The relevant loss is also worth recording. Removing active jump and aerial flip reduces the player's vocabulary for moving vertically and correcting mid-air mistakes. The replacement gains a clearer relationship between road geometry and deliberate ground movement. A bend, a missed junction and a departure can now be addressed through drift, quick turn and dash. This is a narrower action space with a clearer relationship to the delivery task, although it still needs routes that make those differences matter.

Calling the original inspiration “slightly out of control” is not enough to justify a controller. An expert's apparent chaos can depend on highly precise inputs. Our question is what a beginner can understand and what an experienced player can improve. The current design seeks low entry difficulty in basic movement and greater mastery in the timing, placement and combination of techniques.

## 4. The car's vocabulary: choices, not a collection of buttons

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

Insert the verified input/condition/effect table from `FINAL-CONTROLLER-DOCUMENTATION.md` after these explanations. The rationale explains why the actions exist; the table makes their rules unambiguous.

## 5. A delivery situation: how the parts become a game

Consider an illustrative situation, not a recorded playtest. The courier is carrying two orders. One destination is close but requires turning back; the other lies along the current road. A third order is waiting near the route the courier could take after the second delivery.

There is no useful decision based on urgency alone. The player must compare remaining time with travel and service requirements. Turning back may save the closest order but place the car badly for both remaining jobs. Continuing may protect the current momentum and free cargo space near the third pickup, while sacrificing the opportunity to save the first. The car's position and heading are part of the planning state.

A drift and clean exit may reduce the cost of one route. A collision may change which route is feasible. The resulting reprioritisation is the interaction we need to observe: did the mistake change a decision, or did it merely make the player wait before continuing exactly as before?

This also explains why increasing punishment is not the same as deepening the game. If a collision makes every remaining order impossible, it removes alternatives. A smaller setback that leaves two competing possibilities may produce a richer decision. The useful amount of resistance is enough to make a plan vulnerable while preserving something worth attempting.

## 6. Measurements as evidence about the action space

The saved 26 October lab results give an ordinary top speed of 100.8 km/h, 2.842 seconds to 99% of that ceiling, 0.917 seconds for ordinary braking to the test's stopped threshold, and 12.736 m stopping distance. The tightest measured whole-car swept turning diameter is 5.827 m. These results describe a rapid, manoeuvrable vehicle; they do not support keeping every earlier claim about a difficult, traction-limited controller.

The most revealing result is the tested 90-degree bend: on a 12 m-wide road with 6 m inner and 18 m outer radius, the car enters at 100.8 km/h and exits at 91.405 km/h without a trick. On that geometry, cruise-speed turning does not require the player to spend speed before entering. A design that expects every right-angle corner to force a brake-versus-speed decision is contradicted by this result.

This is useful evidence rather than an embarrassment. It tells us where to look next. The challenge may need to come from tighter approaches, adjacent decisions, exit alignment or the consequences of boosted speed. It also means a drift demonstration must compare useful route outcomes rather than assume that a special move is inherently superior.

The whole-car turning diameter gives a physical clearance baseline, not a recommended street width. A route also needs room for approach error, steering correction, props and an intelligible camera view. Likewise, stopping distance excludes noticing and deciding. The practical distance needed to make an informed stop includes distance travelled while the player recognises the situation.

Keep the full lab table and method with these interpretations. Label the numbers as saved evidence from the earlier run; this review did not perform a new successful measurement session.

## 7. Camera: the information that makes commitment fair

The camera determines which decisions the player can make in time. Before a dash, the player needs to judge the departure space. During a drift, they need to read both their movement and the exit. After a collision, they need to understand the car's new orientation and a possible way back into the route. A camera can remain technically outside a wall while still failing all three tasks.

The current chase camera follows above and behind the car, keeps a level horizon and eases heading changes. During a forward drift it partly frames travel direction, helping the player read the path while the chassis rotates. Speed widens the field of view within a bounded range. These are communication choices: the view should convey motion without making the car feel remote or making the road difficult to judge.

Wall avoidance uses overlap resolution and sphere sweeps with near-plane clearance. Obstructions pull the view inward and the camera returns outward smoothly when clear. This addresses physical clipping, but a tightly obstructed camera can still become too close to read an exit. Dense corners therefore need a separate readability test in the rendered level.

The current 7 m look-ahead value is an offset of the point toward which the camera aims. It is not a claim that only 7 m of road is visible. Visible and readable distance depends on camera placement, field of view, elevation and occlusion. Comparing that number directly with stopping distance cannot establish an execution bottleneck or justify reverting to the old 28 m value.

A stronger test records when the relevant turn or obstacle first becomes recognisable and how much time remains before the player reaches it. Repeat it during ordinary driving, a dash approach and a drift exit. If the player has already committed before the exit becomes readable, the issue may require a landmark, changed geometry or different framing, rather than blaming their reaction time.

## 8. Collision and recovery: preserving a next move

A collision can already cost velocity, route position and time from several active orders. Cargo damage adds a loss to the value being carried. A proposed repair fee would add another charge. These costs interact; they should not be balanced as isolated systems.

In the inspected Easy, Medium and Hard scenes, a collision removes 1.5 cargo-condition points per m/s of relative impact speed above 10 m/s, down to zero condition. All carried orders are affected. This is a continuous cargo-value rule, not the new PDF's 15%/35% damage bands. The logger's speed-drop definition of a wall hit is another measurement entirely and is not the collision damage formula.

The proposed R50 repair fee would change behaviour beyond cargo damage. Cargo damage matters only when carrying something. A repair fee could make an empty journey financially risky as well. A flat fee would also introduce a threshold: two impacts just above the threshold would cost twice as much as one much faster impact. That may be acceptable, but it is a design consequence that needs a reason and feedback the player can read.

If stunt tips are introduced, their relationship to repair costs becomes central. A risk-reward system needs attainable upside, identifiable downside and some capacity to choose the exposure. Without the upside, additional penalties may simply teach careful driving. If reckless driving always returns more profit even after crashes, caution becomes irrelevant. Compare net outcomes and voluntary choices, not just how many penalties fired.

Stranded recovery serves a different purpose. The implemented safe-position reset prevents an overturned or blocked car from consuming the remainder of the shift. A successful reset subtracts five seconds from the active shift; it does not freeze steering for five seconds. The player resumes with less time available. That preserves a cost while restoring the ability to act.

There are therefore three recoveries to evaluate: restoring vehicle control, restoring orientation in the city, and restoring a viable plan. Getting upright is necessary but insufficient if the player cannot find the next useful destination. A satisfying recovery ends with an intentional next action, not just a moving Rigidbody.

## 9. Engine choices and production responsibility

The custom Unity controller combines body collisions and four-ray suspension with authored arcade acceleration, steering and technique states. This division lets the project retain interaction with roads and props while deciding what a dash, drift or stop should do. Unity is capable of such custom behaviour; ordinary physics settings simply do not supply the intended action grammar by themselves.

The production value of that arrangement is iteration. A parameter change can be isolated, measured and compared before asking the level designer to adapt streets around it. But a controller change also invalidates dependent evidence. Retiring the aerial flip means old flip tests no longer demonstrate the current recovery design. Changing steering means old road-clearance assumptions need checking. A measurement is useful only when its controller version and conditions are known.

Chandler's *The Game Production Handbook* organises production around planning, implementation, validation and learning from the project. [Third-edition overview and contents](https://www.oreilly.com/library/view/the-game-production/9781449688097/). Applied here, the contribution is not proved by the quantity of code or repository rules. It is shown through a traceable decision: what problem was identified, what changed, what evidence followed and what that evidence required from the rest of the team.

For the vehicle lead, the handover therefore includes the active control rules, tuning version, lab method, measured clearances, camera constraints and remaining uncertainties. Level and order designers need those facts to make compatible decisions. Document only handovers and approvals that actually occurred; attach genuine records rather than writing a persuasive retrospective that the evidence cannot support.

## 10. What would support or challenge this design?

| Claim to evaluate | Comparison or observation | Finding that would challenge it |
| --- | --- | --- |
| Advanced moves create useful mastery. | Compare a route using ordinary controls with the same route using a learned technique; record completion, mistakes and the player's reason for choosing it. | More input complexity without a useful change in time, position or control. |
| Quick turn supports recovery without erasing alternatives. | Compare quick turn, reverse, drift and normal turning at missed junctions. | One move is consistently preferable across every relevant context. |
| Driving and order planning affect one another. | Observe the intended next job before and after an actual driving setback. | The player repeats the same action with added delay, with no viable alternative or meaningful revision. |
| The camera supports informed commitment. | Record first recognition of turns and obstacles under different approaches. | The relevant information appears only after avoiding failure becomes impractical. |
| Damage makes cargo matter. | Compare empty and loaded journeys and ask why the player changed their driving. | No behavioural distinction, or players become so cautious that the techniques cease to be worthwhile. |
| Recovery preserves engagement. | Observe the next intentional action and willingness to continue after a reset. | Reset restores the car but leaves the player lost, resigned or unable to save anything. |

Use repeated route setups for controller comparisons, while separating beginner and experienced-player observations. Pair recorded behaviour with explanations in the player's own words. A high number of crashes alone does not prove entertaining difficulty, and a low number does not prove the absence of challenge. The question is whether the player can perceive, choose and improve.

## Sources and scope of the theoretical discussion

The theoretical framing above uses the linked author/publisher descriptions and contents. The detailed vehicle argument is an application to this project, not a summary of entire books. No chapter quotations or edition-specific page citations have been invented. For formal assessment, add the actual course editions and relevant page references after consulting those texts. The production author's name is **Heather Maxwell Chandler**.

Implementation and measurement claims were checked against the project files identified in `FINAL-CONTROLLER-DOCUMENTATION.md`, with an additional read of `VehicleController.Taxi.cs`, `ChaseCamera.cs` and `OrderManager.cs` during this review. The reviewed submission is `Game design doc LP Final Submission.pdf`, 46 pages. The original PDF is unchanged.
