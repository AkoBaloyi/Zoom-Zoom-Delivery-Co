# Crazy Taxi pivot — the short version

> Superseded for implementation by [the 24 October research and implementation record](CRAZY-TAXI-IMPLEMENTATION-2026-10-24.md). The Crazy Stop description and implementation recommendations below are historical.

Ako Baloyi · 14 October 2026 · Group 15, DIGA3002A

This is the readable summary. The full research, with every source and every number traced to a
file, is in `CRAZY-TAXI-CONTROLLER-RESEARCH.md`. Read that one if you need to defend a claim.

---

## The finding, in one paragraph

Crazy Taxi is not a better-handling game than ours. It is a game that pays you instantly for
driving well, where ours makes you work for five seconds to earn three seconds of a reward you
cannot see while you are earning it, and then spends it into a shop that does not exist. The
lecturer is right that something is wrong, and it is not the grip values.

---

## The number that explains it

Our boost tank holds 100 units. It drains at 33.3 units a second, so a full boost lasts three
seconds. It fills at 20 units a second, and only while the car is drifting, which means holding
more than twelve degrees of slip at over four metres a second.

So: five seconds of hard, deliberate sliding buys three seconds of boost.

Nothing on screen tells the player they are earning anything while it happens. There is no slow
passive refill as a fallback, because the recharge rate is set to zero. And the money that boost
feeds into cannot be spent, because the function that would spend it has never been connected to
anything.

Across all seven shifts we have logged, boost earned, boost used and handbrake seconds are zero.
Not low. Zero. Nobody has ever triggered the reward.

---

## What Crazy Taxi does instead

You get paid for passing close to a car without hitting it. Closer is worth more. Doing it against
oncoming traffic is worth more again. Doing several in a row multiplies the payment each time.

The payment is the score itself, not a resource. It arrives immediately. It appears on screen as it
happens. And it costs a fraction of a second of input rather than five seconds of difficult
driving.

Crazy Taxi also has four named tricks — a dash, a drift, a stop and a technique for exceeding the
top speed. All four work the same way: you flick the gear lever between Drive and Reverse while
managing the accelerator separately. They are input patterns, not resources. A beginner never
touches them and the game still works; an expert chains them and goes twice as fast.

---

## The one thing blocking it

Every Crazy Taxi trick needs a separate gear control. Ours does not exist. Our accelerator and
reverse share a single control — W and S on the keyboard, the two triggers on a pad — so `S` brakes
and then becomes reverse once you stop.

That means Crazy Taxi's whole skill ceiling is not badly tuned in our game. It is impossible to
perform. The pivot starts by splitting that control, not by changing how the car grips.

Some good news: we already have a separate brake on Left Ctrl, so half the pedal arrangement is
built. The missing piece is only the gear.

---

## The trap in the scope

The near-miss reward needs something to miss. Without moving traffic there is nothing to drive
close to, and near-misses against walls read much weaker.

Two of our playtest sessions already asked for traffic and pedestrians. Our plan currently rules
traffic out. So this pivot quietly turns something we rejected into something we depend on. That is
a group decision and a scope decision, and it should be made out loud.

If the group will not commit to traffic, there is a cheaper version: pay for near-misses against
the 33 knockable street props we already have. Weaker, but it tests whether the reward idea works
for a fraction of the effort.

---

## Two things I got wrong earlier, now corrected

I told you our cargo system punishes collisions and would fight a Crazy Taxi reward model. It is
far gentler than I said. Kyuri already softened it in the newer levels: the damage from hard
braking halved, and the speed at which a collision starts to count rose from six to ten metres a
second. Anything under ten is free. So we do not punish near-misses. We simply do nothing for them,
which is a much easier problem to fix.

I also called the unspendable money an oversight. It is not. The function is a deliberate hook
waiting for an upgrade screen that has not been built. Unfinished, not broken.

---

## One place we are already ahead

Crazy Taxi makes you come to a full stop to drop a passenger off. Our drop-off zones have no speed
check at all — you can drive through at full speed and the delivery completes. That is more
permissive than Crazy Taxi. It is worth deciding on purpose whether we keep it, rather than leaving
it as something that happens to be true.

---

## What I would do, in order

1. **Ask the lecturer which Crazy Taxi he means.** "Pivot the controller" could mean the input
   tricks, the reward loop, or the whole tone and city design. Those are three different projects
   and the cheapest is ten times cheaper than the dearest. This is the first thing to settle.
2. **Split the throttle into accelerator and gear.** Nothing else in the pivot is possible first.
   Expect to re-run the measurement harness afterwards, because the test rig drives the car through
   the same control it is changing.
3. **Add one immediate, visible, chaining reward.** One trigger, not four. Pay it in score, not in
   boost, and leave boost in place so the two can be compared on the same build.
4. **Decide traffic, and write the decision down either way.**
5. **Only then** consider the dash and drift tricks, and only if there is a visual confirmation for
   them. Crazy Taxi tells you a trick landed with an engine sound. We have no audio at all, so
   without a visual tell the tricks cannot be learned.

---

## What not to copy

Do not make the car more forgiving. I already tested that and wrote down why it failed: our Grippy
setup does not slide, so the handbrake has nothing to do, cornering loses its texture, and new
players do well immediately and then stop improving. Crazy Taxi is forgiving *and* deep, but its
depth is in the tricks, not the grip. Copy the forgiveness without the tricks and we get the
plateau I already identified.

Do not chase Crazy Taxi's actual numbers. Sega never published them. Everything known about the
game's feel comes from players and press, not engineering notes. We have a measurement harness and
787 rows of our own data, which is better evidence than a guess at theirs.
