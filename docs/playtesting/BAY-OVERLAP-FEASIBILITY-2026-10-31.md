# Can the player leave the bay and get back before the food burns?

Measurement for tutorial beat six, 31 October 2026. Ako Baloyi.

## The question

The tutorial plan's sixth beat teaches the overlapping-task loop: drop food on a bay, drive off to
collect the next ingredient, return before it burns. That beat only works if the round trip to a
useful errand fits inside the cook-plus-burn window. If it does not, the correct play is to stand at
the bay and wait, which is exactly the behaviour the beat is meant to teach players *out of*.

This was flagged as a prerequisite in `docs/TUTORIAL-PLAN.md` and had not been checked.

## What is in the scenes

The cooking loop exists in **one scene only**. `Medium.unity` has one `ProcessingBay` and five
`IngredientSource` objects. `Hard`, `Easy` and `Possible game scene` have none of either, so beat six
can only be built in Medium unless Kyuri places bays elsewhere.

From `OrderTuning_Balanced.asset`: `defaultCookSeconds: 8`, `defaultBurnSeconds: 12`, `burnPenalty: 50`.
So the window is **20 seconds** from dropping food to the moment it burns.

## Positions, read from Medium.unity

| object | x | z | straight-line distance from bay |
| --- | --- | --- | --- |
| Bay 1 | −22.2 | −2.8 | — |
| Bakery | −0.6 | 45.8 | 53.2 m |
| Fish Market | −98.6 | −13.5 | 77.1 m |
| Dairy | −1.7 | −93.0 | 92.5 m |
| Market | 73.3 | 30.9 | 101.3 m |
| Butcher | 85.5 | −67.5 | 125.6 m |

## Round trips against the 20-second window

Three average speeds: 8 m/s is the measured corner cap in Medium, 12 m/s is a realistic mixed
average, 20 m/s is the cruise figure the order timers were costed on.

| source | at 8 m/s | at 12 m/s | at 20 m/s |
| --- | --- | --- | --- |
| Bakery | 13.3 s ok | 8.9 s ok | 5.3 s ok |
| Fish Market | 19.3 s ok | 12.9 s ok | 7.7 s ok |
| Dairy | 23.1 s **burns** | 15.4 s ok | 9.3 s ok |
| Market | 25.3 s **burns** | 16.9 s ok | 10.1 s ok |
| Butcher | 31.4 s **burns** | 20.9 s **burns** | 12.6 s ok |

Straight-line distances, so real routes are longer and every figure above is optimistic.

## What this means

**Beat six is buildable as designed.** Two sources, Bakery and Fish Market, are reachable within the
window at any realistic speed, including crawling round corners at 8 m/s. That is enough for the
tutorial: the beat needs one errand that is obviously achievable so the player learns that leaving
the bay is correct.

The remaining three sources are not free. Dairy and Market need a 12 m/s average; Butcher needs
close to cruise. That gradient is a feature rather than a problem. It means the choice of *which*
ingredient to fetch while food cooks is a real decision with a skill component, which is the risk
economy the hypothesis is built on.

Two things worth Kyuri's attention:

- The straight-line figures flatter the Butcher route badly. At 125 m one way it is likely a
  genuine burn at anything but a clean cruise, so an order needing Butcher plus cooking is a
  hard-tier order and should be scheduled late in a shift, not early.
- Bay 1 sits roughly centrally. A second bay nearer Market and Butcher would open the east side up,
  but it is not needed for the tutorial.

## What this does not settle

Route length, not straight line. A proper check drives each leg with the measurement harness. Until
then treat every "ok" above as "probably ok" and every "burns" as "definitely burns".
