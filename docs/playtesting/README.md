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
| `tester_id` | text | `T01`... never a name. Same tester, same id across sessions |
| `tester_racing_experience` | `never` / `sometimes` / `often` | asked before play |
| `play_minutes` | number | wall clock, including restarts |
| `delivered` | int | session log |
| `late` | int | session log |
| `value` | number | session log |
| `boost_activations` | int | session log, presses that spent fuel |
| `boost_seconds` | number | session log |
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

The measured columns depend on the game writing a log. That logger does not exist yet; until it
does, leave measured cells blank rather than estimating them. It will subscribe to the order
manager's events and read the vehicle's public state, the same way the shift results screen
does, and write one row per shift to `playtest_logs/`. Writing it is on the vehicle owner's list;
it reads the order system but does not change it.
