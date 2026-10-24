# UI and tutorial brief — what the playtests say we need

**For:** Zubuhle (UI / level) · **From:** Ako (vehicle, camera, telemetry) · **Date:** 14 October 2026
**Status:** evidence summary plus a prioritised build list. Not a spec — the visual design is yours.

---

## 1. The one finding three sessions agree on

Players are not crashing. They are not arriving.

| Source | What it showed |
| --- | --- |
| Family session, 3 testers (unstructured) | Two of three could not tell where to go. **None** had trouble with the tickets, timers or values |
| Session S01, 1 participant (recorded) | Asked for a tutorial, a minimap or directional indicator, and a route line to the drop-off after pickup |
| Telemetry, 7 logged shifts | Wall hits: 7, 1, 0, 0, 0, 1, 2. Two shifts delivered **zero** orders while 10 and 12 expired |

The split is clean: **order information is landing, spatial information is not.** The same testers
who could read the HUD could not read the city. Two shifts with zero deliveries and double-digit
expiries are not a difficulty problem, they are a navigation problem.

One important caveat before anyone builds from this: neither session followed
`docs/playtesting/QUESTIONS.md`, no 1-to-5 score has been collected from anybody, and the protocol
minimum of five testers is unmet. Treat the above as a strong direction, not a measured result.

### What this means for your scope

S01 rated the car's controls **easy**, and the participant could identify their own mistakes when
they lost control. So the tutorial does **not** need to teach steering, throttle or braking at any
length. Spend that time on the objective, the destination, and the three tools nobody uses. This
should make the job smaller, not bigger.

---

## 2. Build list, in evidence order

### P0 — Off-screen direction indicator
The game already has world beacons and screen-space destination markers labelling order ID and
distance, so this is not "add navigation from nothing". The gap is **direction when the target is
off-screen**. A distance number tells a lost player how lost they are, not which way to turn.

Minimum: a screen-edge arrow or compass bearing to the active target. S01 specifically asked for
on-screen text showing the direction of the next destination, so even a text bearing would test the
hypothesis cheaply before you commit to art.

### P0 — Per-order countdown, and which one is expiring first
Three orders run at once (`maxActiveOrders` 3, one spawning every 12 s). Nothing on screen ranks
them by remaining time, so a player holding cargo cannot make the routing decision the design is
built around.

`OrderTicketStrip.cs` already exists and is live in `Hard`, `Medium` and `Easy`. It was **not
attached** in `Possible game scene`, which is the scene every logged shift was played on — so the
testers never saw it. Before building anything new, check what it already renders in `Hard`.

Two things to know when you wire it up:
- The real order deadline in the shipped build is **45 seconds**, not the 50 in
  `OrderTuning_Balanced.asset`. A merge broke that asset's YAML and the 50 never loads. The longest
  route in the level needs about 48 s, so one pair is currently unwinnable. Design the countdown
  against 45 until that's fixed.
- `OrderTicketStrip` has no `ShiftEnded` subscription, no `IsShiftActive` check and an
  unconditional `Update`, so it keeps running after the shift ends. That's Kyuri's to fix, but it
  will affect how your strip behaves on the results screen.

### P0 — Escape to pause, and canvas sort order
Pause is currently mouse-click only. Gameplay markers draw over the pause panel and the boost gauge
overlaps the PAUSE button. This is P0 not because it is a big feature but because it **blocks
testing everything else** — a tester who cannot pause cannot stop to read anything you build.

### P1 — Tutorial / onboarding
No instructions exist in the build. What it must cover, derived from what testers actually failed
at rather than from a feature list:

1. **What the objective is.** Drive into a green pickup disc to collect, then into the matching
   drop-off disc before that order's clock runs out. Zones are 6 m radius and you only have to
   *pass through* one — there is no need to stop, which no tester has been told.
2. **How to find the destination.** Whatever you build for P0, teach it here.
3. **What the timer means**, and that three orders run at once with independent clocks.
4. **The four undocumented actions** (see §3). Boost especially: it appears nowhere in the build.

Keep driving instruction to a single line. The evidence says the car is already legible.

### P1 — Finish the Controls screen
It currently reads, from `MainMenu.unity`:

```
W = Acceleration/Forward      D = Steer Right
A = Steer Left                Shift  = Jump
S = Brake/Reverse             SpaceBar = Hand Brake
```

Six bindings against the ten `VehicleInput` defines. Missing: **Left Ctrl, boost, flip, camera
look.**

`Left Ctrl` is the one worth adding first. Its own code comment says it "brakes and never reverses.
For stopping ON a drop-off point" — which is exactly the manoeuvre two family testers complained
about when they asked for a better reverse. They were holding `S`, which becomes reverse once you
pass zero.

(Correction to an earlier QA note that may have reached you: that note said the screen showed "only
W/A/S/D". It shows six bindings including jump and handbrake. The handbrake *is* documented.)

### P2 — Requested, but not yet decided
- **Traffic and pedestrians.** Asked for independently by two sessions; the city reads as empty with
  only the player's car. The current plan rules out traffic, so this needs a recorded decision
  rather than silence. Not a UI job — flagged so it isn't lost.
- **Day/night cycle.** Requested once. Worth knowing that a global `WeatherState` hook already
  exists on the vehicle side and `Easy`, `Medium` and `Hard` each carry a `Weather` root, so an
  environment-state system has somewhere to plug in.
- **Route line to the drop-off**, Crazy Taxi style. Requested once. Strictly better than an arrow,
  and strictly more work. Build the arrow first and see whether it closes the gap.

---

## 3. Constraints worth having before you start

- **There is no audio in the project at all.** No audio assets, no `AudioSource` in any gameplay
  scene. Every cue you design has to work silently, so anything you would normally reinforce with a
  sound needs a visual carrying the full load.
- **`Easy.unity` is in no build settings**, so the Easy button logs an error and does nothing. Don't
  test onboarding there until that's fixed.
- **Scene geometry differs a lot between levels.** `Hard`'s streets are 8.18 m wide against the
  greybox's 25.5 m corridors, and 6 of 12 delivery anchors in `Hard` are not on a road tile at all.
  A guidance system that works in the greybox may read very differently in `Hard`.
- **The boost gauge currently always reads empty.** Boost is earned only by drifting and air time,
  and across all seven logged shifts no player has earned a single unit. If you are laying out HUD
  space, know that this element has never shown a non-zero value in play.

---

## 4. Suggested order of work

1. Escape pause + canvas sorting — unblocks all testing
2. Check what `OrderTicketStrip` already does in `Hard` before building anything
3. Off-screen direction indicator, simplest version that can be tested
4. Per-order countdown with clear expiry ranking
5. Controls screen: add Left Ctrl, boost, flip, camera look
6. Tutorial overlay, using §2's four points

Items 3 and 4 are the two that the playtest evidence actually supports. Everything else is either
unblocking work or filling a documented gap.

---

## 5. How we'll know it worked

The telemetry already records `delivered`, `late` and `wall_hits` per shift, so the test is
specific: if navigation is the problem, **deliveries should rise and late orders fall while wall
hits stay flat.** If wall hits rise too, we've traded one problem for another.

`docs/tooling/playtest_row.py` turns a shift log into a sheet row with the measured columns filled,
so recording a session afterwards is one command. The question script is
`docs/playtesting/QUESTIONS.md` and it should be read as written — five testers, at least two who
answer "never" or "sometimes" on driving games, all on the same build.
