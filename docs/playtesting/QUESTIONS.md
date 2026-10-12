# Playtest script

Read these in this order, with this wording, to every tester. Changing the wording between
testers makes the answers incomparable, and incomparable answers are not evidence.

Do not help during play. Do not explain controls beyond what the README says. If the tester asks
how something works, say "what do you think?" and write down what they thought. What a tester
cannot work out unaided is a finding.

## Before play

Ask once, record in `tester_racing_experience`:

> How often do you play driving games? Never, sometimes, or a lot?

Record `never`, `sometimes` or `often`. Testers who play a lot read handling differently from
testers who do not, and the game is not for one group only.

## During play

Say nothing. Watch for these and write them in `observer_notes` with the clock time:

- The first time they crash: what they do in the first three seconds afterwards.
- The first time an order goes late: what they do, and whether they notice.
- Whether they ever press boost, and what was on screen when they did.
- Whether they ever use the handbrake on purpose, as opposed to by accident.
- Anything they say out loud. Quote it.

The counts (`delivered`, `late`, `wall_hits`, `boost_seconds` and so on) come from the session
log the game writes, not from watching. If the log is not running, leave those cells blank
rather than estimating.

## After play

Scored questions are 1 to 5, where 1 is strongly disagree and 5 is strongly agree. Read the
statement, take the number, then ask "why?" and write the answer next to it.

| Column | Read aloud |
| --- | --- |
| `q_car_predictable` | "The car did what I expected it to do." |
| `q_knew_why_lost_control` | "When I lost control, I could tell why." |
| `q_in_control_at_speed` | "I felt in control at high speed." |
| `q_regained_control_was_mine` | "When I got going again after a crash, that was my doing, not luck." |
| `q_wanted_to_continue` | "When the shift ended I wanted to play another one." |

Open questions, written down as said:

| Column | Read aloud |
| --- | --- |
| `q_used_boost_when` | "When did you use boost, and why then?" |
| `q_what_made_you_drive_well` | "What, if anything, made you want to drive well rather than just fast?" |
| `q_lost_time_changed_plan` | "After you lost time, did you change which order you went for, or the way you went? What did you change?" |
| `q_recovery_satisfying_or_frustrating` | "Think of the worst crash. Getting going again afterwards: satisfying or frustrating? Why?" |
| `q_late_order_what_did_you_do` | "An order went late. What did you do about it?" |
| `q_wanted_more_slots` | "Did you ever want to carry more than one order at once? When?" |
| `q_best_moment` | "What was the best moment?" |
| `q_worst_moment` | "What was the worst moment?" |

## What each question is for

The design question is: *does automatic order pressure combined with demanding but predictable
driving make recovery satisfying?* Every question above feeds one word of it.

- *Predictable*: `q_car_predictable`, `q_knew_why_lost_control`. If testers lose control and
  cannot say why, the handling is not predictable, whatever the measurement sheet says.
- *Demanding*: `q_in_control_at_speed` against `wall_hits` and `time_above_supersonic_s`. A tester
  who reports control at speed and never went fast has not tested the claim.
- *Recovery*: `q_regained_control_was_mine`, `q_recovery_satisfying_or_frustrating`,
  `longest_recovery_s`, the observer's crash note. The second of those is the design question
  asked out loud, which the pitch (section 10.3) commits to doing. The first separates "I
  recovered" from "the car recovered me": if testers score it low while `longest_recovery_s` is
  short, the flip or the stuck-recovery is doing the work and the player knows it.
- *Pressure, both directions*: `q_lost_time_changed_plan` is half the design question. The pitch
  says the driving must change the queue; if nobody ever switched target or route after losing
  time, that direction is not happening and the order overlap is too weak. Then
  `q_late_order_what_did_you_do` and `q_wanted_more_slots`, which are the evidence the cargo
  capacity decision needs, and that decision belongs to the order system's owner.
- *Satisfying*: `q_wanted_to_continue`, `q_best_moment`.

## Variants

One factor changes per session and `variant` names it. Everything else, including the build hash,
stays the same across the whole playtest, or the rows cannot be compared. The values:

| `variant` | What is different |
| --- | --- |
| `baseline` | The build as committed. Earned boost on, flip on, Balanced profile. |
| `boost-off` | `boostEnabled` false on the tuning asset. Tests whether boost helps the loop or only looks fast. |
| `boost-regen` | `boostRechargeRate` 12, earn rates 0. Regenerating boost, for comparison with earned. |
| `flip-off` | Flip input unbound or `VehicleJumpFlip` disabled. Tests whether recovery survives without it. |
| `grippy` / `loose` | The other two tuning profiles on the car. |
| `camera-far` | `cameraDistance` +1.5 m and `cameraHeight` +0.5 m. |

Record the variant before the tester sits down, and change nothing else that session. Two testers
on different variants are two data points, not a comparison; a comparison needs the same variant
run by several testers and another variant run by several more.

`q_used_boost_when` and `q_what_made_you_drive_well` are for the boost resource decision. If
nobody can name a reason to drive well other than the clock, boost that is earned by driving
well is the answer; if testers already feel rewarded, it is not needed.

## What is not on this list, and why

Currency and XP progression. There is no currency and no XP in the build: delivery value is a
flat 10 and nothing is spent. A question about whether progression feels too fast or too slow
cannot be answered until the system exists, and asking it now produces opinions about a thing
the tester has not experienced. Add the question when the system ships, not before.
