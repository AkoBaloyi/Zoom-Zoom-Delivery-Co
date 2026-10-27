# Playtesting and Telemetry — Milestone 2

**Module:** DIGA3002A · **Group:** 15 · **Project:** Zoom Zoom Delivery Co.
**Author:** Ako Baloyi (vehicle, camera, telemetry) · **Period covered:** 7–14 October 2026
**Instrument:** `docs/playtesting/playtest_sessions.csv` (33 columns) · **Script:** `docs/playtesting/QUESTIONS.md`

---

## 1. Method

Playtesting for this project separates two kinds of column, and the separation is the point.

**Measured** columns are what the player did. They are written automatically by `PlaytestLogger`,
a component on the Car that subscribes to the order manager's events and reads the vehicle's
public state. It writes one row per shift to `PlaytestLogs/shift_log.csv`, outside `Assets` so
Unity never imports it, and it changes nothing it observes. Its thresholds are deliberately
identical to those in the vehicle measurement harness, so the two data sets agree on terms: a
wall hit is a loss of more than 3 m/s in a single physics step from above 10 m/s, and a recovery
ends when the car is grounded, upright and above 8 m/s again.

**Asked** columns are what the player said. They carry a `q_` prefix: five statements scored 1–5,
then eight open questions recorded in the tester's own words. `QUESTIONS.md` fixes the wording and
the order, because changing either between testers makes the answers incomparable, and
incomparable answers are not evidence.

The two are kept apart because they disagree, and the disagreement is the finding. A tester who
scores "I felt in control at high speed" a 5 while the log shows eleven wall hits is reporting
something that neither number says on its own.

### Threshold for a valid playtest

The protocol sets its own bar: **five testers, at least two of whom answer "never" or "sometimes"
to the driving-experience question, all on the same build hash, all asked the same questions in
the same order.** Anything below that is a demo, not a test, and is not written up as one. One
factor changes per session, named in the `variant` column; `baseline` means nothing was changed.

### Design question under test

> Does automatic order pressure, combined with demanding but predictable driving, make recovery
> satisfying?

Each question maps to one word of that sentence. *Predictable* is tested by
`q_car_predictable` and `q_knew_why_lost_control`. *Demanding* is tested by
`q_in_control_at_speed` read against the measured `wall_hits` and `time_above_supersonic_s`.
*Recovery* is tested by `q_regained_control_was_mine` and
`q_recovery_satisfying_or_frustrating` against the measured `longest_recovery_s`. *Satisfying* is
tested by `q_wanted_to_continue` and `q_best_moment`.

---

## 2. Status of data collection

**One user test with a real participant has been run, plus one unstructured family session. Neither
followed the question script, so the protocol minimum is still unmet.** A `session_type` column
sits in column two so the composition of the sheet cannot be misread from a row count.

| `session_type` | Rows | What it means |
| --- | --- | --- |
| `machine-log` | 1 | Telemetry imported from a shift log with no recorded observer or participant |
| `mixed-ai-user` | 2 | Build verification with both AI and developer interaction; driving cannot be attributed to either |
| `ai-qa` | 2 | AI-operated functional QA of UI, pause and order expiry; no sustained driving route |
| `human` | **2** | Session S01: one participant, two logged shifts, recorded, run in the editor |

The five machine and AI rows have an empty `build` column, so none can be tied to a commit. Under
the protocol's own rule — "a row without this cannot be tied to a version and is worthless" — they
are retained as engineering evidence only.

Session S01 is the first row in this project that can be tied to a commit. It was run in the Unity
editor from a working tree at `618f2be` with no modifications anywhere under `Assets/`, so the
content played is exactly that commit's content and the `build` column is accurate rather than
nominal.

What S01 is **not**: no 1-to-5 scored question was administered, so all thirteen `q_` columns are
blank and the five scored statements in `QUESTIONS.md` remain unanswered by anyone. Feedback was
gathered as open conversation and recorded by the observer in paraphrase, not quoted. The earlier
family session of three testers was explicitly unstructured, with no telemetry and no
questionnaire; its findings appear below as directions to investigate, not as data.

The protocol minimum — five testers, at least two inexperienced, same build, same variant, same
questions in the same order — is therefore still not met. One participant is one participant.

---

## 3. Telemetry recorded

All five runs were played on `Possible game scene`. None was played on `Hard`, `Medium` or `Easy`,
so none of them exercised the surface-traction model or the knockable street props.

| Timestamp | Type | shift s | play s | Delivered | Late | Value | Boost acts | Boost earned | Handbrake s | Wall hits | Flips | Longest recovery s |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-10-12 13:33:39 | `machine-log` | 300 | 300.0 | 9 | 7 | 61 | 0 | 0 | 0.0 | 7 | 0 | 3.1 |
| 2026-10-14 09:09:12 | `mixed-ai-user` | 300 | 163.5 | 4 | 4 | 158 | 0 | 0 | 0.0 | 1 | 0 | 1.4 |
| 2026-10-14 09:17:49 | `mixed-ai-user` | 300 | 300.0 | 6 | 10 | 216 | 0 | 0 | 0.0 | 0 | 0 | 0.0 |
| 2026-10-14 09:29:20 | `ai-qa` | 0 | 268.7 | 0 | 12 | 0 | 0 | 0 | 0.0 | 0 | 0 | 0.0 |
| 2026-10-14 09:38:11 | `ai-qa` | 0 | 221.4 | 0 | 10 | 0 | 0 | 0 | 0.0 | 0 | 0 | 0.0 |

---

## 4. Findings

### 4.1 The earned-boost system has never been exercised — the most significant result

Across all five records, `boost_activations`, `boost_seconds` and `boost_earned` are **zero**.
Not a single unit of boost was earned, let alone spent.

This is not a defect in the boost code. The vehicle measurement harness, driving the same build
under a scripted input, records a full tank lasting 3.01 s and a peak speed of 40.1 m/s when
boost is forced. The mechanic works when it is triggered.

The cause is upstream, and the log shows it directly: `handbrake_seconds` is **0.0 in every
run**. Boost in this build is earned, not regenerated — `boostRechargeRate` is 0, and the only
sources are drifting at 20 units/second and air time at 10 units/second. A player who never
pulls the handbrake never drifts, therefore never earns, therefore has nothing to spend. The
chain is: no handbrake → no drift → no earnings → no boost. `flips` is 0 in all five runs, so the
air-time route produced nothing either.

The design consequence is sharper than a bug report. The pitch commits to boost as the reward
that makes players want to drive *well* rather than merely fast. Under unguided play that reward
has never once been collected, which means the mechanism intended to carry the reward half of the
design question is currently inert. Two readings are possible and the telemetry cannot separate
them: either the earn condition is undiscoverable, or it is too stingy to trigger incidentally.
`q_used_boost_when` and `q_what_made_you_drive_well` exist specifically to separate those two,
and both are unanswered.

### 4.2 Wall hits fell as camera work landed, on weak evidence

Wall hits run 7 → 1 → 0 across the three runs with substantial driving, following the chase
camera's wall-avoidance work (the camera previously cast no rays at all and could clip into
geometry). `longest_recovery_s` falls alongside it, 3.1 → 1.4 → 0.0.

This is suggestive and no more. The three runs had different operators, different durations
(300.0 s, 163.5 s, 300.0 s) and no controlled variant, so improvement in driver familiarity is
not separable from improvement in the camera. It is a hypothesis for the next session, not a
result.

### 4.3 Late deliveries outnumber on-time deliveries

In the only two runs that both completed a full 300-second shift and delivered anything:
9 delivered against 7 late, and 6 delivered against 10 late. The second is a minority of orders
arriving on time.

This is consistent with a known tightness in the configuration rather than with random failure.
A route check across the configured delivery anchors found all 36 pickup/drop-off pairs
completable within the order budget at the project's 20 m/s working average, but the longest
route (954 m) leaves roughly two seconds of margin. That is a deliberately narrow window, and it
is a reasonable thing to test — but it is likely to read as punishing until a player knows the
city, the markers and the brakes. Note also that the two `ai-qa` records accumulated 12 and 10
late orders with zero deliveries: those were expiry checks, and they confirm the expiry path
fires, not that the game is too hard.

### 4.4 Logger defect: `shift_seconds` written as 0 on an early-exit path

Two records carry `shift_seconds: 0` alongside 268.7 s and 221.4 s of logged play. The configured
shift length is 300 s in both cases. The shift length is being written as zero when the session
ends through an exit path rather than through shift expiry, which makes those rows' totals
non-comparable with properly terminated rows and would corrupt any aggregate computed across the
sheet. This needs fixing in `PlaytestLogger` before a real session is recorded, otherwise an
abandoned run is indistinguishable from a complete one in the data.

### 4.5 Functional defects found by the AI QA pass

Recorded in `docs/playtesting/QA-AI-20261014-01.md` with screenshots in
`docs/playtesting/evidence/QA-AI-20261014-01/`:

- Escape does not pause. Pause is reachable only by mouse click, so a keyboard or controller
  player has no pause path at all.
- Gameplay markers draw over the pause panel, and the boost gauge overlaps the PAUSE button.
- The Controls screen omits actions that are actually implemented.
- The build logged 45-second order deadlines while the reviewed source asset specifies 50. This
  is serialized configuration drifting from the current scripts and should be reconciled before
  any balance conclusion is drawn from a session.

### 4.6 Session S01: the first participant, and the first positive result

One participant, run in the editor from `618f2be`, recorded, two logged shifts. The participant
played two versions: one described as a car-control test intended as a tutorial baseline, the other
as the main game. They preferred the second clearly — "much more structured, much more easy to do" —
citing clearer roads and visible surroundings making orientation easier.

**Caveat on that comparison:** the versions were played in sequence, so a preference for the second
cannot be separated from having already learned the controls during the first. It is a direction,
not a measured result. There is also a discrepancy to resolve: the logger recorded scene
`Possible game scene` for *both* shifts, so the two-version account and the telemetry do not agree
and the scene attribution on both rows is unconfirmed.

**The result that matters:** the participant rated the controls easy and **could identify their own
mistakes when losing control.** That is the substance of `q_knew_why_lost_control` — "When I lost
control, I could tell why" — and it is the first evidence in either direction on the *predictable*
leg of the design question. It was not captured as a score, so it does not populate the column, but
it is the first time anyone has reported the handling as legible rather than arbitrary.

Read against §4.1 it also sharpens the boost problem. A participant who finds the car predictable
and still never touches the handbrake is not being defeated by the handling; they simply have no
reason to drift. That points at discoverability over generosity.

Requests recorded: a tutorial or onboarding flow, since no instructions exist; a minimap or
on-screen directional indicator for the next destination; a route line to the drop-off after pickup,
Crazy Taxi style; more traffic and pedestrians, the city reading as empty with only the player's
car; and a day/night cycle.

### 4.7 Three sessions now converge on the same problem, and it is not the driving

The strongest finding in this document is not from any single session but from the agreement between
three independent ones:

| Source | Evidence |
| --- | --- |
| Family session, three testers | Two of three could not tell where to go. None had trouble with tickets, timers or values |
| Session S01, one participant | Requested a minimap, a directional indicator, and a post-pickup route line |
| Telemetry, five runs | Two runs delivered **zero** orders with 10 and 12 late; S01's runs delivered 2 and 1 against 12 and 4 late |

Order information is landing. Spatial information is not. The split is clean and falsifiable: the
same testers who could read the HUD could not read the city. Wall hits across all seven logged runs
are 7, 1, 0, 0, 0, 1, 2 — so players are not crashing, they are not arriving.

This reframes the late-delivery rate in §4.3. A 44% to 83% late rate was read as a difficulty or
timer-budget problem. Three sessions now suggest it is primarily a navigation problem, and the
timer budget is a compounding factor rather than the cause.

**Traffic and pedestrians have now been requested independently by two sessions.** The current plan
rules out traffic. That makes it a decision that needs recording with a stated reason rather than an
omission, and it belongs in the keep-change-cut record the pitch commits to in §4.4.

---

## 5. Limitations

Stated plainly, because the value of this record depends on it:

1. **One participant, and no scored questions from anyone.** The protocol minimum is five testers
   with at least two inexperienced, all on one build and one variant, all asked the same questions
   in the same order. One participant does not support a claim about enjoyment, perceived fairness,
   recovery satisfaction or difficulty balance. The single predictability observation in §4.6 is
   one person's paraphrased comment, not a score, and `tester_racing_experience` was not asked.
2. **Only two of seven rows carry a build hash.** Session S01 carries `618f2be`, verified against a
   clean `Assets/` tree. The five machine and AI rows carry none, so they cannot be reproduced or
   strictly compared with anything.
3. **Mixed attribution on two rows.** The two `mixed-ai-user` records involved both AI and
   developer interaction; driving outcomes cannot be attributed to a single actor.
4. **Single scene.** All runs used `Possible game scene`. The surface-traction model (84 tagged
   surfaces: tarmac roads, concrete commercial bases, grass park bases) and the 33 knockable
   street props exist only in `Hard`, so neither has appeared in any logged play.
5. **No variant comparison.** Every row is effectively `baseline`. The variants defined in
   `QUESTIONS.md` — `boost-off`, `boost-regen`, `flip-off`, `grippy`, `loose`, `camera-far` — are
   untested, including the `boost-regen` comparison that would directly address finding 4.1.

---

## 6. What the data does support

Three claims are safe:

- The delivery loop runs end to end in a standalone Windows build and produces a results screen
  with delivered, late and value totals.
- The order expiry path fires reliably; late orders accumulate as designed.
- Under unguided play, the handbrake and boost are not discovered or used.

That third one is a finding about the design, not a gap in the data, and it is the single most
actionable thing in this document.

---

## 7. Next session

The minimum valid playtest is five testers at roughly eight minutes each: one 300-second shift
plus the question script. Before running it:

1. Fix the `shift_seconds` zero-write (§4.4), or abandoned runs will pollute the sheet.
2. Add an Escape/Start pause binding and fix the HUD layering (§4.5), both of which will otherwise
   dominate tester comments and crowd out the handling feedback the session is for.
3. Reconcile the 45 s/50 s order deadline drift (§4.5) so the balance being tested is the balance
   that was designed.
4. Decide the scene. `Hard` is the only one carrying the surface model and the props; if the
   session is meant to test those, it must run there.
5. Record the build hash before anyone plays.

Then, per tester: `python docs/tooling/playtest_row.py --session S01 --tester T01 --experience
sometimes --build <hash> --play-minutes <wall clock> --append`. That fills the eleven measured
columns from the shift log and leaves exactly the thirteen asked columns blank, which are then
typed from the tester's answers. The script refuses to write a `human` row without a session id,
tester id and build hash.

Recruit so that at least two testers answer "never" or "sometimes". The protocol requires it
because experienced driving-game players read handling differently, and a sample of only
confident drivers cannot detect the discoverability problem that finding 4.1 points at.
