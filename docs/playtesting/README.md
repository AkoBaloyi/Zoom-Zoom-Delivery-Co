# Playtesting

One row per tester per session in `playtest_sessions.csv`. The question script is `QUESTIONS.md`
and must be read as written. This folder is submission material: the rubric's Design Process and
Reflection criteria both ask for playtest evidence, and a sheet with real rows is that evidence.

## Why a CSV and not a shared sheet

The same reasons as `VehicleLab_Measurements/vehicle_measurements.csv`. It lives beside the build it
describes, it diffs, and three people can add rows on three branches and the merge is a union. The
`.gitattributes` rule covers `docs/playtesting/*.csv`. Export to a spreadsheet for charts; the CSV
is the record.

## Two kinds of column, kept apart

**Measured** columns are what the tester did. They come from the session log the game writes, never
from memory: `delivered`, `late`, `value`, `boost_activations`, `boost_seconds`,
`handbrake_seconds`, `wall_hits`, `flips`, `time_above_supersonic_s`, `longest_recovery_s`.

**Asked** columns are what the tester said. `q_` prefix. Scored ones are 1 to 5; open ones are
their words.

The point of keeping them apart is that they disagree, and the disagreement is the finding. A
tester who scores "in control at speed" a 5 and has eleven wall hits is telling you something
neither number says alone.

## Columns

| Column | Type | Source |
| --- | --- | --- |
| `session_id` | text | `S01`, `S02`... one per sitting, shared by every tester in that sitting |
| `date` | ISO date | |
| `build` | commit hash or tag | `git rev-parse --short HEAD` on the build that was played. A row without this cannot be tied to a version and is worthless |
| `variant` | one of the names in `QUESTIONS.md` | Which single factor was changed for this session. `baseline` if none. Two rows with different variants are not comparable with each other; rows sharing a variant are |
| `tester_id` | text | `T01`... never a name. Same tester, same id across sessions |
| `tester_racing_experience` | `never` / `sometimes` / `often` | asked before play |
| `play_minutes` | number | wall clock, including restarts |
| `delivered` | int | session log |
| `late` | int | session log |
| `value` | number | session log |
| `boost_activations` | int | session log, presses that spent fuel |
| `boost_seconds` | number | session log |
| `boost_earned` | number | session log, boost units earned from drifting and air time over the shift |
| `handbrake_seconds` | number | session log |
| `wall_hits` | int | session log, impacts above the wall-recovery test's impact threshold |
| `flips` | int | session log |
| `time_above_supersonic_s` | number | session log |
| `longest_recovery_s` | number | session log, longest gap from an impact to regaining the useful-speed threshold |
| `q_*` scored | 1 to 5 | asked after play |
| `q_*` open | text | asked after play, their words |
| `observer_notes` | text | timestamped, what you saw, what they said |

Free text containing commas must be in double quotes. Quotes inside quotes are doubled.

## Minimum that counts as a playtest

Five testers, at least two who answered `never` or `sometimes`, all on the same `build`, all asked
the same questions in the same order. Fewer than that is a demo, not a test, and should not be
written up as one.

## Running a session

1. Build the player, or note the editor commit. Write the hash down before anyone plays.
2. Start the session log before the tester touches the controls.
3. Say only what `QUESTIONS.md` says to say.
4. One shift, 300 s. A second shift if they ask for one, and note that they asked.
5. Ask the questions, in order, as written.
6. Copy the session log numbers into the row. Add the row. Commit it with the build hash in the
   message.

## The session log

`PlaytestLogger` (on the Car in the game scene) writes one row per shift to
`<project>/PlaytestLogs/shift_log.csv`, outside `Assets` so Unity never imports it. The row has
the same measured columns as this sheet, in the same order, plus the scene name and the shift
length, so it can be pasted straight into `playtest_sessions.csv` with the asked columns filled
in by hand afterwards. It also prints the row to the console at shift end so an observer can
read it off the screen.

It subscribes to the order manager's events and reads the vehicle's public state, the same way
the shift results screen does. It never changes anything. If it is missing from a scene the
measured cells stay blank, and blank is correct; do not estimate them.

The thresholds match the measurement harness so the two agree: a wall hit is a drop of more than
3 m/s in one physics step from above 10 m/s, and recovery ends when the car is grounded, upright
and above 8 m/s again.
