# Teaching the player to drive our game

A tutorial plan for Zoom Zoom Delivery Co.

Ako Baloyi · for Kyuri and Zah · three days

---

## Why I am writing this

Every playtest we have run has told us the same thing, and we have mostly heard it as a complaint
about the level. Two of the three family testers could not tell where to go. Our one recorded
participant asked for a minimap, a direction indicator and a route line. Two of our logged shifts
delivered nothing at all while ten and twelve orders expired.

I want to be precise about what that data says, because it is easy to read it as a navigation
problem and stop there. Our wall hits across all seven logged shifts are 7, 1, 0, 0, 0, 1 and 2.
Players are not crashing into our city. They are not arriving in it. And across every one of those
shifts, nobody earned a single unit of boost and nobody touched the handbrake once.

So the problem is not that our systems are broken. I have measured them and they work. The problem
is that we have never taught anybody how to play, and a system nobody can find is indistinguishable
from a system that does not exist.

There is also something I only confirmed this week, and it changes the order of this plan. Our
Crazy Taxi tricks are live. The dash, the drift, the stop and the limiter cut all fire, and the
recogniser passes fifteen named tests. But the two components that tell the player a trick landed
have never been attached to the car in any scene. So for the last two weeks we have shipped a car
that performs tricks silently. The player does them by accident and learns nothing.

That is the first thing I want us to fix, and it is the reason this tutorial can work at all.

---

## The idea this plan rests on

Raph Koster's argument in *A Theory of Fun for Game Design* is that fun is what we feel while we are
mastering a pattern, and that games are, underneath everything else, machines for teaching patterns.
The consequence he draws is the part I keep coming back to: a game stops being fun when the pattern
is fully mastered, and it is never fun in the first place if the pattern cannot be read. He calls
that second state noise.

Our game is currently noise. Not too hard, not too easy. Unreadable.

Anthropy and Clark put the same problem in more practical language in *A Game Design Vocabulary*.
They talk about games in terms of verbs, the things a player actually does, and they are firm that
a player learns a verb by performing it in a situation designed to make it the obvious thing to do.
Not by reading about it. A control list teaches nobody anything, which is roughly what our controls
screen is: six bindings out of ten actions, with no boost, no flip and no gear on it.

And Bogost's framing in *Unit Operations* is useful for sequencing. Our game is not one experience,
it is a set of discrete units that combine: a car, a destination, a clock, an ingredient, a cooking
bay, a tip, a repair bill. Each one is meaningful on its own. The game only exists in the
combination. A tutorial, then, should introduce units one at a time and only then let them meet.

So the design rule for everything below is one rule, and I would like us to hold it firmly:

> **One new thing per beat. The player performs it rather than reads it. They do not meet the next
> thing until they have performed this one successfully.**

---

## The seven beats

I am describing these as beats rather than levels because none of them should take more than about
forty seconds. The whole tutorial should run under five minutes, and a confident player should be
able to drive straight through it without noticing they were taught anything.

**Beat one. Drive.**
An empty street. No clock, no order, no destination. Nothing on screen except the car. The player
accelerates, steers, brakes, and discovers the gear. That is the whole beat.

I want this to feel almost insultingly simple, because our one real participant rated the controls
easy and could identify their own mistakes when they lost control. The handling is the one thing we
have positive evidence for. We should not spend tutorial time defending it. We should spend about
twenty seconds proving it and move on.

**Beat two. Arrive.**
One destination appears. The player drives to it. This is the beat that fixes our actual measured
failure, so it carries the most weight of any beat here.

The important design decision is that the destination should begin off-screen. If it is visible the
beat teaches nothing, because the player just looks at it and drives. The thing we need them to
learn is how to read our direction cue when they cannot see where they are going, and the only way
to teach that is to make them need it.

**Beat three. The clock.**
Same route the player has just driven. Now it has a timer on it. No new geography and no new verb,
only pressure applied to a pattern they already hold.

This is deliberate and it is straight out of Koster. Introducing pressure and novelty together is
how you produce panic rather than learning. They already know the route. Now they learn what the
clock feels like.

**Beat four. One trick, two ways in.**
Here we teach the dash, and only the dash. A long straight with a wide opening at the end of it, so
that performing the trick is rewarded with something unmistakable and failing it costs nothing.

I want to be specific about *how* we teach it, because I only confirmed this week that our car has
two routes to every trick. There is the authentic one, where you flick the gear between Drive and
Reverse in rhythm with the pedals, the way the original arcade game demanded. And there is a
one-button layer: hold the accelerator and tap the technique button, and you dash. Hold the brake
and tap it, and you stop. The same button reads what your feet are doing and does the right thing.

The beat should teach the one-button version. It is learnable in a single attempt, which is what a
tutorial beat needs to be, and it means the player leaves knowing a trick exists and what it sounds
like when it lands. The gear rhythm stays in the game as the thing a curious player finds on their
own. That is not a compromise. It is the shape Koster describes when he talks about a game being
easy to learn and hard to master: a floor the player can stand on in one go, and a ceiling they can
spend the rest of the game reaching for.

This beat is also the reason the feedback work has to come first. A dash with no engine note and no
on-screen confirmation is not a mechanic, it is a coincidence. We have eight tricks in the
recogniser. The player should leave the tutorial knowing one of them. The other seven are theirs to
find, which is exactly the "grokking" Koster says is the actual pleasure of play.

**Beat five. Collect and cook.**
One ingredient, one bay, one delivery. The player picks something up, puts it on a bay, waits,
collects it, delivers it. Slowly, with nothing competing for their attention.

They need to see the cook bar fill and the burn warning appear while standing right next to it,
because the next beat depends on them trusting that bar when they can no longer see it.

**Beat six. Overlap.**
Two orders. This is the beat where our game actually becomes our game.

Everything before it could be any delivery game. This is the one that teaches the thing we
identified as missing from our Milestone 2 build: that you start a task, leave it running, do
something else, and come back before it fails. If a player does not learn this, they will play our
game as a sequence of errands, which is precisely what our testers did.

I would like us to design this beat so that waiting at the bay is visibly the wrong choice. Not
punished, just obviously wasteful, so the player works it out themselves rather than being told.

I have checked that this is actually possible, because a beat that teaches you to leave the bay is
cruel if there is nowhere you can get back from in time. Food cooks for eight seconds and burns
twelve seconds after that, so the player has twenty seconds. From our one bay, the Bakery is a
fifty-metre run and the Fish Market is seventy-seven. Both are round-trippable inside the window
even if you crawl every corner. So the beat works, and it should use one of those two.

The other three sources are not free. The Dairy and the Market need a decent average speed, and
the Butcher is a genuine burn at anything short of a clean cruise. That is not a problem to fix, it
is the risk economy we wrote into the hypothesis showing up in the geometry: which ingredient you
fetch while the food cooks is a real decision, and a good driver has more options than a careful
one. Kyuri, it does mean a Butcher-plus-cooking order is a hard-tier order and should come late in
a shift rather than early.

One constraint that shapes everything: the bay and the ingredient sources exist in the Medium scene
only. The other scenes have none. So the tutorial is a Medium tutorial unless we place bays
elsewhere, and I would rather we did not spend three days doing that.

**Beat seven. Risk.**
The tip and the repair bill, introduced together, because neither means anything alone. A near miss
pays. A heavy crash costs. The player should see both numbers appear within about ten seconds of
each other so the trade-off is legible as a trade-off rather than as two unrelated events.

---

## What I want us to avoid

**A text wall before the game starts.** If the player reads instead of drives, we have failed
before they have touched the car.

**Teaching everything.** We have eleven input actions, eight tricks, seven surface types, weather,
a quick-turn, a recovery reset and a look-behind. The tutorial should teach six verbs at most.
Everything else is discovery, and discovery is the reward for having learned the six.

**Blocking progress.** No beat should refuse to let the player continue. If they fail a beat, the
beat should quietly repeat. The moment we gate a final-year prototype behind a tutorial step, we
have built a chore.

**Explaining in words what we could show in space.** This is Zah's strongest lever and I would
rather spend level geometry than UI text wherever we have the choice.

---

## Three days, and who does what

I have laid this out as three working days with a soft fourth for integration, following the
dependency-first approach in Maxwell-Gardner's *Game Production Handbook*. The ordering matters more
than the hours: beat four cannot be built before the feedback exists, and beat six cannot be built
before beat five works.

### Day one — make the game legible

**Me.** Attach the engine audio and trick feedback to the car in every scene. I have built a menu
item that does it in one click per scene and saves the result, so this is ten minutes rather than
an afternoon, and it is still the single highest-value ten minutes available to any of us, because
it turns eight silent mechanics into eight audible ones. Then finish the in-game controls screen.
Our README now lists every one of the eleven bound actions; the screen in the build lists six, and
until this week the README itself had one key wrong, which means a tester reading it would press
Ctrl to brake and drift into a wall instead.

**Kyuri.** The direction cue. Whatever form it takes, the minimum is that a player who cannot see
their destination can tell which way to turn. Our existing markers show distance, which tells a lost
player how lost they are rather than which way to go. If the cheapest version is on-screen text
giving a bearing, I would rather we shipped that on day one and improved it on day three than spent
day one on a minimap.

**Zah.** The tutorial street. One short stretch of road, visually distinct from the city, wide enough
that beat one is impossible to fail and with one clear corner for beat two. This does not need to be
pretty on day one, it needs to exist so Kyuri and I have somewhere to put things.

### Day two — build the beats

**Me.** Beats one and four. The driving beat and the dash beat, including the long straight and the
reward at the end of it. I will also add a tutorial-completion row to the session logger so we can
measure whether the tutorial worked, rather than asking people whether they liked it.

**Kyuri.** Beats two, three and five. Destination, clock, and the collect-cook-deliver loop. These
are all order-system beats and they are yours end to end.

**Zah.** The beat framing. Whatever tells the player a beat has started and ended: a title, a tone, a
gate opening. Also the cook and burn bar, because that bar is the thing beat six depends on being
trusted, and trust is a visual-design problem more than a code problem.

### Day three — the beat that matters, and a real test

**Morning, together.** Beat six. I want all three of us on this one because it is the beat where our
hypothesis either appears or does not. Kyuri owns the orders, Zah owns the space that makes waiting
look wasteful, and I have already done the measurement that tells us the overlap is possible: the
Bakery and Fish Market round trips both fit the twenty-second window with room to spare. So we
build the beat around one of those two and we do not have to argue about whether it can work.

**Afternoon.** Beat seven, and then we test. Five testers, the question script we already have in the
playtesting folder, same build for everyone, at least two of whom do not play driving games. We have
two human rows in that sheet after a month. We can have seven by the end of day three.

### Day four, if we have it

Integration, the things the test breaks, and the tutorial's own pacing. I expect at least two beats
to be in the wrong order and I would rather discover that from five testers than argue about it
between ourselves.

---

## How we will know it worked

I do not want to judge this on whether people say they enjoyed it. We already have a sheet built for
this and it records what people did rather than what they remember.

If the tutorial works, the next five sessions should show deliveries up and late orders down **while
wall hits stay flat**. That combination is the whole claim: that our problem was comprehension and
not difficulty. If wall hits rise too, we have taught people to hurry without teaching them to drive,
and we will have traded one failure for another.

And one number I will be watching more than any other. Across seven logged shifts, boost earned and
handbrake seconds are both zero. If a single tester finishes a shift having deliberately performed a
trick, this tutorial has done something that two weeks of tuning could not.

---

## One last thing

I am conscious that we have written a lot of documents about this game and shipped relatively few
changes to it, and that the gap between the two is the honest story of our production. So I would
rather this document was wrong in places and acted on this week than correct and discussed next week.

If any of you think a beat is in the wrong place, or that your day is unrealistic, say so now rather
than on day three. The sequence is the part I care about. The hours are negotiable.
