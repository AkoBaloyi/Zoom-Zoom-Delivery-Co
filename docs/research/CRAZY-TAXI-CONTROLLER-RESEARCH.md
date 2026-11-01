# Crazy Taxi's vehicle controller, and what pivoting to it would cost

> Superseded for implementation by [the 24 October research and implementation record](CRAZY-TAXI-IMPLEMENTATION-2026-10-24.md). The Crazy Stop description and implementation recommendations below are historical.

**Module:** DIGA3002A · **Group:** 15 · **Project:** Zoom Zoom Delivery Co.
**Author:** Ako Baloyi (vehicle, camera, telemetry) · **Date:** 14 October 2026
**Prompted by:** lecturer feedback recommending a pivot of the vehicle control model toward Crazy Taxi.

**How to read this.** Three kinds of claim, marked throughout:
**[RESEARCH]** external, with an inline source link. **[PROJECT]** read from a file in this repo,
with the path. **[ANALYSIS]** my own reasoning over the other two — argument, not fact.

---

## 1. Executive summary

Crazy Taxi's controller is not primarily a *handling* model. It is a small set of discrete,
learnable input tricks sitting on top of deliberately forgiving physics, paid for by an immediate
scoring system. Copying the handling would miss the point; copying the input grammar and the
payout structure would not.

Three findings reframe the pivot.

**The techniques need an input we do not have.** All four named Crazy Taxi moves are built on
tapping a discrete Drive/Reverse gear while the accelerator is managed separately. Our `Throttle`
is a single axis carrying forward *and* reverse — `W`/`S` on keyboard, right and left trigger on a
pad **[PROJECT: `VehicleInput.cs`, `BuildFallbackControls`]**. So Crazy Taxi's skill ceiling is not
mistuned here, it is unexpressible. The pivot begins at the input map.

**Our reward loop is the real deficit, and the arithmetic shows why it never fires.** Boost holds
100 units, drains at 33.3 per second — a 3.0-second tank — and is earned only at 20 per second
while drifting or 10 per second airborne, with recharge at zero
**[PROJECT: `Tuning_Balanced.asset`]**. Filling the tank therefore takes **five seconds of
continuous drift to buy three seconds of boost**, where drift means holding more than 12° of slip
above 4 m/s **[ANALYSIS]**. Across all seven logged shifts, boost earned, boost used and handbrake
seconds are all zero **[PROJECT: `docs/playtesting/playtest_sessions.csv`]**. Crazy Taxi pays
instead in the primary currency, immediately, with a multiplier, for an input that takes a quarter
of a second.

**The collision incentive is neutral where Crazy Taxi is positive — and that gap is the opportunity.**
I expected to find our cargo system punishing contact and fighting a Crazy Taxi reward model. It is
gentler than that. `CargoCondition` is enabled in all four scenes, but the newer ones already
softened it: hard-brake damage 12 → 6 and the collision threshold 6 → 10 m/s
**[PROJECT: `Hard.unity`, `Medium.unity`, `Easy.unity` vs `Possible game scene.unity`]**. Contact
below 10 m/s is already free. So the problem is not that we punish near-misses; it is that we do
nothing at all for them, while Crazy Taxi pays for them and multiplies the payment **[ANALYSIS]**.

**Recommendation.** Do not re-tune grip. Do three things in order: split the throttle axis so a
gear input exists; add one immediate, visible, chaining payout for skilful driving; and decide
whether traffic is in scope, because the payout needs something to be near. If the group will not
commit to traffic, the pivot loses its centre and a cheaper variant is in §6.

---

## 2. What Crazy Taxi's controller actually is

### 2.1 Inputs

The arcade cabinet ran on Sega's NAOMI board with analog steering, a separate analog accelerator
and brake, and a physical Drive/Reverse gear lever. A hardware thread documenting the NAOMI's
analog channel assignment records Crazy Taxi using one channel for the steering axis and two more
for accelerate and brake — [Naomi analog controls discussion](https://www.arcade-projects.com/threads/resource-on-naomi-analog-controls.1508/)
**[RESEARCH]**. On console the lever became two buttons, which is why guides describe rolling a
thumb across them.

The important structural point: **accelerator, brake and gear are three independent inputs.** The
gear is discrete and can be changed instantly at any speed.

### 2.2 The four techniques

| Technique | Input | Effect |
| --- | --- | --- |
| **Crazy Dash** | Release accelerator → shift to Drive → press accelerator | Burst above normal top speed |
| **Crazy Drift** | Release gas → tap Reverse then Drive, while steering | Slides through a tight corner |
| **Crazy Stop** | Release everything mid-drift; add brake to stop sooner | Near-instant stop |
| **Limit Cut** | Release both pedals → Reverse + Drive + accelerator | Speed beyond the normal cap |

**Crazy Dash** is release-accelerator, shift-to-Drive, press-accelerator — [IGN's strategy guide](https://guidesarchive.ign.com/guides/16200/crazymoves.html)
**[RESEARCH]**. The three steps must be *sequential with a tiny gap*, not simultaneous, and a
correct input is audible as an engine overdrive note — [Steam community guide to the four key moves](https://steamcommunity.com/sharedfiles/filedetails/?id=3383283795)
**[RESEARCH]**. Performed from a standstill it removes the usual wheelspin, behaving as though
traction were complete, and briefly exceeds normal floored speed — [crazytaxi.net skills page](https://www.crazytaxi.net/skills.html)
**[RESEARCH]**.

**Crazy Drift** is release-gas, then tap Reverse and Drive while steering the way you want to go —
[Crazy Taxi 2 advanced techniques](https://rappy-world.com/guide/crazy_taxi_2_advanced_techniques_guide)
**[RESEARCH]**. On a pad it is a thumb roll across the two gear buttons while turning —
[IGN crazy moves](https://guidesarchive.ign.com/guides/12006/crazymoves.html) **[RESEARCH]**.

**Crazy Stop** is letting go of everything during a drift, and braking as well stops you faster —
[cheatcodes.com Dreamcast hints](http://www.cheatcodes.com/guide/hint-tips-information-crazy-taxi-dreamcast-41702/)
**[RESEARCH]**.

**Limit Cut** releases both pedals then presses Reverse, Drive and accelerator together for speed
past the normal ceiling, and is chained with Crazy Dash to maximise jump distance —
[IGN Crazy Box guide](https://guidesarchive.ign.com/guides/16200/crazybox.html) **[RESEARCH]**.

### 2.3 What that structure buys

**[ANALYSIS]** Four observations about the design, not the feel:

1. **Every technique is an input pattern, not a held state.** None is a resource you accumulate.
   The cost is dexterity and timing; the payoff is immediate. This is why the skill ceiling is high
   while the floor stays at "hold accelerator, steer".
2. **They are discoverable by accident and masterable only on purpose.** A player fumbling the gear
   while turning will produce a Crazy Drift and see it happen. A retrospective records players
   spending extended periods learning the dash — [the Guardian on Crazy Taxi at 25](https://www.theguardian.com/games/2025/feb/24/taxis-sega-pop-punk-classic-crazy-taxi-at-25)
   **[RESEARCH]**.
3. **The audio is part of the interface.** The overdrive note is how you know the input landed. Our
   project has no audio of any kind **[PROJECT: established in `docs/DESIGN-OVERVIEW.md`]**, so a
   technique layer here would need a visual confirmation instead.
4. **Speed caps are soft.** Two of the four techniques exist specifically to exceed the normal top
   speed. The cap is a baseline for unskilled play, not a limit.

---

## 3. Why it feels the way it does

Sega's own account of the game's origin has the creator stuck in traffic, noticing the empty
opposite carriageway, and imagining how much drivers would want to cross over and accelerate away
if it were allowed — [Sega: the moment it all began](https://www.sega.com/news/crazy-taxi-world-tour-the-moment-it-all-began)
**[RESEARCH]**. That is a design brief about *transgression*, not about driving.

Producer Kenji Kanno, interviewed in 2000, described arcade design as producing a large emotional
swing in a short span, and said a game should make you want to play it the moment you see it —
[IGN interview with Kenji Kanno](https://www.ign.com/articles/2000/02/05/the-crazy-interview-with-sojs-kenji-kanno)
**[RESEARCH]**. More recently he framed his motivation as wanting to see people smile because of
his games — [3DMGame interview](https://en.3dmgame.com/original/3375) **[RESEARCH]**.

**[ANALYSIS]** The forgiveness follows from the brief. If the fantasy is driving in ways that would
be illegal, the game cannot spend its time penalising you for it. There is no damage model and no
fail-by-crash; contact deflects you and costs time, which is the only currency that matters. The
difficulty lives entirely in the clock and in the technique layer.

This matters for us because it is the opposite of a simulation instinct. **A Crazy Taxi pivot is a
decision to stop modelling a van and start modelling a fantasy.** That is a design choice the group
should make explicitly, because it has consequences for the delivery-courier framing.

---

## 4. The reward loop, which is the real lesson

Fare on drop-off is built from distance travelled, time remaining, and tips earned through skilful
driving — [crazytaxi.net general rules](http://crazytaxi.net/rules.html) **[RESEARCH]**. A "Tip
Rate" multiplies the base fare and rises with how fast and how aggressively you drive —
[rappy-world fare guide](https://rappy-world.com/guide/crazy_taxi_fare_guide) **[RESEARCH]**.

The central mechanism is **Crazy Through**: passing close to a car without touching it pays a small
tip; doing it against oncoming traffic pays more; and doing several consecutively multiplies the
value for each additional thrill delivered to the passenger — [GamesRadar score-attack retrospective](https://www.gamesradar.com/retrobots-favorite-score-attack-games/7/)
**[RESEARCH]**. Jump and drift bonuses sit alongside it. Stunts generally earn extra tips —
[Wikipedia on Crazy Taxi](https://en.wikipedia.org/wiki/Crazy_Taxi) **[RESEARCH]**.

### 4.1 Side by side with ours

**[ANALYSIS]** Both games set out to reward driving well. The architectures are opposites.

| | Crazy Taxi | Zoom Zoom Delivery Co. |
| --- | --- | --- |
| What is rewarded | Proximity to traffic, jumps, drifts | Drift slip angle, air time |
| Payout currency | The score itself (tips) | Boost, a spendable resource |
| Payout timing | Immediate | Deferred until the player spends it |
| Chaining | Multiplies per consecutive near-miss | None |
| Cost to trigger | A fraction of a second | 5 s of sustained >12° slip |
| Times it has fired in play | The core of the scoring loop | **Zero, across seven logged shifts** |

The exchange rate is the part I had not calculated before. Our tank is 100 units draining at 33.3
per second, so a full boost lasts 3.0 seconds; it fills at 20 per second while drifting
**[PROJECT: `Tuning_Balanced.asset`]**. **Five seconds of continuous difficult driving to buy three
seconds of reward**, with nothing shown to the player while they earn it, and `boostRechargeRate`
at zero so there is no passive fallback **[ANALYSIS]**.

Then the deferred currency has nowhere to go. `TrySpendMoney` exists in `PlayerProgression.cs:127`
and has no caller anywhere in the project **[PROJECT]**. Its own comment describes it as what an
upgrade screen would call, so it is a deliberate hook awaiting a screen rather than an oversight —
but as shipped, money accumulates and cannot be spent, and no `PlayerProgression` is attached to
any gameplay scene **[PROJECT]**.

**[ANALYSIS] The lesson is about payout timing and legibility, not grip.** Crazy Taxi's reward is
the score, it arrives instantly, it is visible on screen as it happens, and performing the trick
again immediately is worth more than performing it twice separately. Ours is invisible while being
earned, expensive to earn, and spends into a system that does not exist yet. Any of those three
would be enough to kill it on its own.

---

## 5. Gap analysis against our controller

Our model, read from source: a direct-force Rigidbody car at 900 kg on a 1/120 s timestep, with
raycast suspension (`springStrength` 110, `springDamper` 14, `suspensionRestLength` 0.6,
`wheelRadius` 0.36) and a grip budget rather than a tyre slip curve **[PROJECT]**.

| Dimension | Crazy Taxi | Ours **[PROJECT: `Tuning_Balanced.asset`]** | Gap |
| --- | --- | --- | --- |
| Accelerator / reverse | Separate pedals plus discrete gear | One `Throttle` axis, W/S or RT/LT | **Structural** |
| Dedicated brake | Analog pedal | `Brake` on Left Ctrl / B — exists already | None |
| Technique layer | Four named moves | None | **Structural** |
| Top speed | Soft; two techniques exceed it | `topSpeed` 32, `boostTopSpeed` 40 m/s | Soft already, via boost |
| Reverse | A technique input, tapped | `reverseTopSpeed` 8, `reverseAcceleration` 9 | Works; not a technique |
| Drift entry | Gear tap | `handbrakeGripMultiplier` 0.25, rear 0.15, slip >12° above 4 m/s | Different grammar |
| Collision model | No damage; deflect and lose time | Damage above 10 m/s at 4/m·s⁻¹, 6 per hard brake | Close, already soft |
| Arrival | Requires a full stop in the zone — [rules](http://crazytaxi.net/rules.html) | **No speed or stop test at all**; 6 m radius, distance only | We are *more* arcade |
| Reward payout | Immediate, chaining, in score | Deferred resource, no chaining | **Structural** |
| Audio confirmation | Engine overdrive note | No audio anywhere | **Missing channel** |

Three structural gaps, one missing channel, and several places we are already closer than expected.
Notably `ZoneDetection` applies no speed gate whatsoever — driving through a drop-off at 32 m/s
delivers the order **[PROJECT: `.agents/tasks/turning-geometry-2026-10-14/report.md`]**, which is
*more* permissive than Crazy Taxi, where the passenger needs you to stop. That divergence is worth
a deliberate decision rather than leaving it as an accident.

---

## 6. What a pivot would require

Ordered by value per unit of work. Costs are my estimates **[ANALYSIS]**.

### 6.1 Split the throttle axis so a gear exists — prerequisite for everything else
**Change.** Replace the single `Throttle` 1D axis with `Accelerator` (positive only) plus a gear
input. Keyboard: `W` accelerate, `S` brake, and two gear keys. Pad: RT accelerate, LT brake,
buttons for D and R. `Brake` already exists as its own action on Left Ctrl / B, so the pedal side
is half-built.
**Files.** `VehicleInput.cs` (including `BuildFallbackControls`), `VehicleControls.inputactions`,
and the `ApplyDriveAndBrake` branch in `VehicleController.cs` that currently infers intent from
`ForwardSpeed > 0.5f`.
**Breaks.** Every existing binding and the Controls screen, which already omits four of ten actions.
Also `VehicleMeasurement`, which drives the car by writing `DriveInput` directly — the F1–F6
harness and all 787 measurement rows assume the current struct.
**Retest.** Full F1–F6 sweep. This is the change most likely to invalidate existing measurements,
so capture a baseline first.

### 6.2 Add one immediate chaining payout
**Change.** A near-miss detector plus a visible, chaining, instantly-paid bonus. Start with one
trigger, not four.
**Files.** New component in `Assets/Project/Scripts/Vehicle/`; HUD work is Zubuhle's and the
scoring hook is Kyuri's.
**Breaks.** Nothing, if it pays into score rather than into boost. **[ANALYSIS]** I would strongly
prefer adding this alongside boost rather than converting boost, so the two can be compared on the
same build — exactly how `Tuning_Grippy` was kept comparable to Balanced by holding top speed and
physics rate identical.
**Retest.** One session with the question script. This is the change the hypothesis actually turns on.

### 6.3 Decide traffic, explicitly
**[ANALYSIS]** Crazy Through needs something to be near. Without moving traffic the central reward
has nothing to trigger on, and near-misses against static props are a much weaker read. Two
independent sessions already asked for traffic and pedestrians; the plan currently rules traffic
out. **The pivot converts a rejected nice-to-have into a dependency.** This is a group decision and
a scope decision, not a vehicle task, and it should be recorded either way.

### 6.4 Add the technique layer
**Change.** Implement dash, drift and limit-cut equivalents as input patterns on the new gear.
**Files.** `VehicleController.cs`, `VehicleInput.cs`.
**Breaks.** Potentially the `boostTopSpeed` 40 ceiling, if a limit-cut equivalent should exceed it.
**Retest.** Everything. Sega never published Crazy Taxi's internal constants, so there are no
reference values to copy — the magnitudes would have to be found with our own harness
**[ANALYSIS]**.

### 6.5 Visual confirmation for technique inputs
**[ANALYSIS]** Crazy Taxi confirms a successful dash with an engine note. We have no audio, so a
successful technique needs a visual tell or it will be unlearnable. Cheap, and it gates whether
6.4 is worth doing at all.

### 6.6 The cheaper variant, if traffic is refused
**[ANALYSIS]** Keep the current handling. Do 6.1 and 6.2 only, and have the chaining bonus pay for
near-misses against the 33 knockable props and the buildings rather than traffic. Weaker, but it
tests the payout hypothesis for a fraction of the work, and the hypothesis is the thing under
question.

---

## 7. What not to copy

**[ANALYSIS]**

**Do not make the car more forgiving.** My own note on `Tuning_Grippy` records why it was rejected
as the default: the car does not slide, so the handbrake has nothing to do and cornering has no
texture, and new players do well immediately then stop improving
**[PROJECT: `Tuning_Grippy.asset`, `whyThisSetup`]**. Crazy Taxi is forgiving *and* deep, but its
depth is in the technique layer, not in the grip. Copy the forgiveness without the techniques and
you get exactly the plateau I already wrote down.

**Do not adopt the full-stop arrival.** Crazy Taxi requires stopping in the zone; we require
nothing. Stopping adds a deceleration skill but costs flow, and our late-delivery rate is already
high for navigation reasons rather than driving ones. Change this only as a deliberate difficulty
decision.

**Do not remove the cargo condition system.** It is already soft — nothing below 10 m/s in the new
scenes — and it is the only mechanic in the game that makes *how* you drive matter to the delivery
rather than just how fast. It is also Kyuri's. If the near-miss bonus lands, condition becomes the
risk against the reward, which is a better loop than either alone.

**Do not chase Crazy Taxi's numbers.** They are not public. Everything in §2 comes from player and
press documentation, not engineering notes. We have a measurement harness and 787 rows of
baseline; our own values are better evidence than a guess at Sega's.

---

## 8. Open questions

| Question | What would settle it |
| --- | --- |
| Is traffic in scope? | A group decision, recorded. Everything in §6.2 depends on it |
| Should the payout replace boost or sit beside it? | One session per variant on the same build, same question script |
| Does a gear input survive on keyboard? | Prototype 6.1 and try it. A thumb roll is easy on a pad; two keys plus WASD may not be |
| What magnitude should a dash give? | Our own harness. No public Crazy Taxi value exists |
| Does the lecturer mean the input grammar or the feel? | **Ask.** This document answers "the controller", but "pivot to Crazy Taxi" could mean the reward loop, the tone, or the city design, and those are different projects |
| Would a near-miss bonus survive our collision thresholds? | Arithmetic first, then a session. Below 10 m/s contact is free, so near-miss and light-contact rewards would overlap |

**[ANALYSIS]** The last one is the one I would resolve first, and it is a conversation rather than a
task. "Pivot the character controller to Crazy Taxi" has at least three readings, and §6 costs
differ by an order of magnitude between them.

---

## 9. Sources

All external material is linked inline at the point of use. Content was rephrased for compliance
with licensing restrictions; no source is quoted beyond a short phrase, and the analysis and
conclusions are my own.

- [Sega — Crazy Taxi: World Tour, the moment it all began](https://www.sega.com/news/crazy-taxi-world-tour-the-moment-it-all-began) — origin of the design brief
- [IGN — interview with Kenji Kanno, 2000](https://www.ign.com/articles/2000/02/05/the-crazy-interview-with-sojs-kenji-kanno) — arcade design philosophy
- [3DMGame — Kanno interview](https://en.3dmgame.com/original/3375) — stated motivation
- [IGN strategy guide, crazy moves](https://guidesarchive.ign.com/guides/16200/crazymoves.html) and [the second guide](https://guidesarchive.ign.com/guides/12006/crazymoves.html) — dash and drift inputs
- [IGN Crazy Box guide](https://guidesarchive.ign.com/guides/16200/crazybox.html) — limit cut
- [Steam community guide to the four key moves](https://steamcommunity.com/sharedfiles/filedetails/?id=3383283795) — dash timing and the audio tell
- [crazytaxi.net skills page](https://www.crazytaxi.net/skills.html) — dash traction behaviour
- [crazytaxi.net general rules](http://crazytaxi.net/rules.html) — fare composition and the full-stop arrival
- [rappy-world — Crazy Taxi fare guide](https://rappy-world.com/guide/crazy_taxi_fare_guide) — tip rate as a multiplier
- [rappy-world — Crazy Taxi 2 advanced techniques](https://rappy-world.com/guide/crazy_taxi_2_advanced_techniques_guide) — drift input
- [cheatcodes.com — Dreamcast hints](http://www.cheatcodes.com/guide/hint-tips-information-crazy-taxi-dreamcast-41702/) — crazy stop
- [GamesRadar — score-attack retrospective](https://www.gamesradar.com/retrobots-favorite-score-attack-games/7/) — Crazy Through and its multiplier
- [the Guardian — Crazy Taxi at 25](https://www.theguardian.com/games/2025/feb/24/taxis-sega-pop-punk-classic-crazy-taxi-at-25) — mastery time, Dreamcast frame rate
- [arcade-projects — NAOMI analog controls](https://www.arcade-projects.com/threads/resource-on-naomi-analog-controls.1508/) — cabinet input channels
- [Wikipedia — Crazy Taxi](https://en.wikipedia.org/wiki/Crazy_Taxi) — stunt tips, general structure

Project values were read from `Assets/Project/Scripts/Vehicle/Tuning/Tuning_Balanced.asset`,
`Tuning_Grippy.asset`, `Assets/Project/Scripts/Vehicle/VehicleInput.cs`,
`Assets/Project/Scripts/Orders/CargoCondition.cs`, `Assets/Project/Scripts/Orders/PlayerProgression.cs`,
the four gameplay scenes on `origin/main`, `docs/playtesting/playtest_sessions.csv`, and
`docs/DESIGN-OVERVIEW.md`.
