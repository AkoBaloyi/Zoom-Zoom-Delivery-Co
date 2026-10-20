# Reflection scaffold — working material, not submission text

This file is working material for Ako Baloyi to write the Milestone 2 reflection from, and nothing
in it is text to submit.

**How to use this.** Every bullet below is a fact about an artefact, with the file, commit or figure
it came from, so a claim in the finished reflection can be checked rather than taken on trust. Every
**Q** is a question for the author to answer in their own words, because only the author can answer
it. Nothing here is written in the author's voice, and nothing here contains a judgement, feeling or
intention attributed to the author. Nothing here should be pasted into the submission unchanged.

Sections follow the rubric's own sub-prompts for criterion 3: personal systems (B1); successes (B2);
failures and lessons (B3); the hypothesis (B4); group workflow, collaboration, communication and
role fulfilment (B5). B6 is a dated timeline for checking the finished text against. B7 is a
pre-submission checklist.

**Authority.** `docs/DESIGN-OVERVIEW.md` is the project-level reference and outranks the
investigation notes cited below wherever they could disagree. Its §6a lists twelve claims it struck
or demoted; check any figure against it before using it.

---

## B1. Personal systems — how the work was organised

**Facts.**

- Branch discipline is written down and assigned: `docs/GIT-RULES.md` rule 2 states that Ako owns
  `feature/vehicle-camera`, Kyuri owns `feature/orders-cargo` and Zubuhle owns
  `feature/greybox-ui`, and requires `git branch --show-current` before the first commit of a
  session. The same table appears in `README.md` under "Branch Ownership".
- All work described in this milestone sits on `feature/vehicle-camera`; the tree described in
  `docs/M2-INDIVIDUAL-AKO.md` is that branch at `618f2be` (14 October 2026).
- A labelled-evidence convention is used across every investigation note in
  `.agents/tasks/design-overview-2026-10-14/`: `READ`, `MEASURED`, `DERIVED`, `INFERRED`, `UNKNOWN`,
  each carrying a `path:line`, a CSV filter or an exact command. The convention is restated at the
  head of `notes-vehicle.md`, `notes-map.md`, `notes-orders.md` and `notes-friction.md`.
- Commit bodies record what was verified *and* what was not. `c03c220` (13 October) ends:
  "Verified: 80 runtime + 5 editor scripts compile, 0 errors 0 warnings; scene structurally valid
  (2419 transforms, 51 roots, no duplicate anchors, all references resolve); 34/34 repo checks" and
  then "Not verified: whether the car now actually knocks props over. That needs a Unity play test."
- Four read-only Python tool families exist under `docs/tooling/`: `compile_check.py`,
  `playtest_row.py`, `scene-tools/` and `repo-checks/`.
- The verification suite is 34 tests, run as
  `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks`. The `-B` and
  `-p no:cacheprovider` flags keep bytecode and cache directories off disk; `.hypothesis/` and
  `__pycache__/` are also ignored, with the reasoning recorded in `.gitignore`.
- The measurement harness is a standing instrument rather than a one-off: 640 data rows in
  `VehicleLab_Measurements/vehicle_measurements.csv`, every row `Balanced`, newest capture session
  `2026-10-07 13:56`. The tracked file is union-merged so three people can capture at once
  (`.gitattributes`); per-machine working captures are excluded by the
  `VehicleLab_Measurements/*_local.csv` rule.
- The four team-authored editor tools are `KnockablePropBaker`, `SurfaceTagger`, `VehicleAssetBaker`
  and `VehicleLabSceneBuilder`, all under `Assets/Project/Scripts/Vehicle/Editor/`. The compile check
  counts five editor scripts because `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs` is a Unity
  template leftover.

**Q.** Which of these habits existed before this project, and which were built during it?
**Q.** Which one cost time without paying for itself?
**Q.** The "verified / not verified" split in commit bodies appears from 13 October onward. What
prompted it, and was it a deliberate rule or a reaction to something?

---

## B2. Successes

**Facts.**

- Earned boost is measured end to end: a full tank lasts 3.0083 s, peak speed on boost is
  40.1278 m/s, and a 2.5 s handbrake slide at 10 m/s counted as drifting for 1.875 s and paid 37.5
  units — exactly 1.875 × 20, which confirms the 20 units/s rate by measurement rather than only
  from the asset (`vehicle_measurements.csv`, `Boost` rows, `2026-10-07 13:56:45` and `13:56:50`).
- The flip was proven by measurement not to be a shortcut: 4.4439 m of displacement over 0.4583 s
  against 4.6895 m of simply driving, logged as `Flip is not a shortcut = 1 (PASS)`.
- Chase-camera wall avoidance landed at `311cdc8` (29 September). Before it the camera cast no rays
  and could clip into geometry. Wall hits run 7 → 1 → 0 across the three logged runs with
  substantial driving. **Caveat, to be carried wherever this is used:** those three runs had
  different operators, different durations (300.0 s, 163.5 s, 300.0 s) and no controlled variant, so
  driver familiarity is not separable from the camera change. It is suggestive and is not a result.
- 33 props were baked and re-seated in `Hard.unity`, across four families (12 mobile toilets at
  180 kg, 9 picnic tables at 120 kg, 9 trash containers at 60 kg, 3 road blockers at 25 kg).
- 84 `SurfaceType` tags were placed in `Hard.unity`: 2 Tarmac, 22 Grass, 60 Concrete. The 60
  Concrete and 22 Grass tags are on objects that own real colliders and do resolve. (The Tarmac case
  is B3 thread 2.)
- `SurfaceKind.Concrete` was appended rather than inserted, so the 640 serialized measurement rows
  and every scene's integer surface value keep their meaning (`SurfaceType.cs:10-21`, with the
  comment that states the rule).
- `.gitignore` gained reasoned entries that ended a recurring disk-filling failure: `Assets/_Project/`,
  `Game Build/`, `/.worktrees/`, `**/PlaytestLogs/`, `Assets/_Project.meta` (commit `618f2be`,
  14 October). Each carries its mechanism in a comment rather than just a path.
- The playtest instrument exists and is documented: `docs/playtesting/README.md`, `QUESTIONS.md`,
  a 33-column sheet, `docs/tooling/playtest_row.py`, and the write-up
  `docs/playtesting/M2-PLAYTEST-WRITEUP.md`.
- The repo-level verification holds throughout: `python docs/tooling/compile_check.py` reports 0
  errors and 0 warnings across 80 runtime and 5 editor scripts, and the 34-test suite passes.

**Q.** Which of these would you present first in the marking session, and why that one rather than
the measured boost numbers?
**Q.** Which of them was hardest to get right, as opposed to longest to do?

---

## B3. Failures and lessons

Six threads, each as facts with sources. No interpretation is supplied; the interpretation is the
reflection.

### Thread 1 — The knockable props took five attempts

Commit chain, all 13 October 2026, in order, each with the cause stated in its own subject or body:

| SHA | Subject | Stated cause addressed |
| --- | --- | --- |
| `a4b6649` | Knockable street props: a baker for the first four families, and an editor compile pass | first attempt |
| `e1f1708` | The prop baker saves the scene it changes, and Kyuri's Scene carries two cargo slots | a bake only marked dirty was undone by entering Play, which restores the saved scene |
| `7f357c7` | Knockable props, third attempt: pull each prop out of the city's broken transform chain | the city's transform chain |
| `752943e` | Knockable props: size the box from the vertices that exist, not the bounds Unity reports | wrong bounds source |
| `9e562f7` | Knockable props: the 9 m tables are real, so the tool now says so instead of calling them wrong | the tool was treating a correct measurement as an error |
| `d80d61e` | Knockable props: mass is a feel number, so everything moves at the size it already is | mass derived from size produced a 4-tonne table the car bounced off |
| `457d1f3` | Seat props on the road, copy the hand-fitted blocker box, and tag the city's surfaces | collider bottoms below the road |
| `c03c220` | Scene: tag 84 city surfaces, re-seat knockable props | the same, in the scene |

- The cause found last was the simplest: collider bottoms sat below the road surface, so PhysX pinned
  the bodies. Recorded sink figures are picnic tables at −0.59 and mobile toilets at −1.61 against a
  road at +0.40 (`docs/tooling/seat-fix-plan.md` and `seat_math_check.py:281-289`, on branch
  `fix/seat-on-road-rotation`).
- The lesson is already written in a commit body. `1efc886`: "This tool reported a clean bake while
  nothing in the scene had moved on four earlier attempts, which is the failure mode worth being
  loud about." That commit makes the summary line carry the count of refused seatings and logs a
  bake with refusals as a warning.
- Three props were hand-corrected in `c03c220`, with the arithmetic recorded:
  `trashcontainer01.006` y 6.763 → 0.525 (the downward ray hit an overhang and left it floating
  6.4 m up; 0.525 matches its eight siblings); `RoadBlocker04.001` and `.003` y −0.090 → 0.400. Both
  blockers were low by exactly 0.483 m, the local-Y half-extent of the reference box, because
  `SeatOnRoad` subtracts the box half-height in local space and ignores the 120-degree axis-swap
  rotation those blockers carry. The same commit states the baker still has that bug, so re-running
  it undoes the three fixes.
- The rotation-aware fix exists **only** on `fix/seat-on-road-rotation` (`395497b`, then `1efc886`).
  `git branch -a --contains` returns that branch alone for both: neither is on
  `feature/vehicle-camera` or `origin/main`.
- The reparenting rationale recorded in `KnockableProp.cs:26-29` and commit `7f357c7` states that
  PhysX cannot run a Rigidbody under negative non-uniform parent scale. A later measurement found
  1311 of 1312 colliders under `City` resolving to a *positive* world scale, because the negative
  local scales cancel against the negative group scales (`notes-friction.md` §3.1). The reparenting
  is still correct for other stated and measured reasons; the mechanism in the tooltip is not
  supported by that measurement.

**Q.** What was the expected outcome of each bake, before it was run?
**Q.** At which attempt did the diagnosis change from "the tool is wrong" to "the scene is wrong",
and what caused that change?
**Q.** The rotation-aware fix is finished and sitting on a branch nobody merged. What was the
decision there?

### Thread 2 — The surface tagging is wrong on the roads

**Facts.**

- `Roads_Parent` in `Hard.unity` carries `SurfaceType kind: 0` (Tarmac) on a `MeshCollider` whose
  `m_Mesh` is `{fileID: 0}` — a null reference, so no collision geometry. It is one of 7 null-mesh
  MeshColliders among Hard's 1295 (`notes-map.md` §4.1, §4.6).
- 633 road tiles under `Roads_Parent` own no collider of their own.
- The second Tarmac tag is on a palette `road_base` object at world y = −47.90, 48 m below the map.
- The `Floor` BoxCollider — the only large drivable collider in the scene — carries no `SurfaceType`
  at all.
- The 60 Concrete tags (on `commercial_base` meshes) and 22 Grass tags (on park bases) are on
  objects with real colliders and do resolve.
- The premise behind the error is recorded in the author's own commit message. `c03c220`:
  "Roads_Parent -> Tarmac (the merged road collider the car actually drives on; the 582 road_base
  children carry no collider of their own bar one)."
- `SurfaceTagger.cs:101` contains a verify pass: "VERIFY: fire the car's ray at each tagged object
  and see what comes back", using `Physics.RaycastAll` downward over 400 m, with a
  `Debug.LogWarning` path at `:140` when the check does not come back clean.
- Live consequence, stated for accuracy: `SurfaceProfile.Default` is numerically identical to Tarmac
  in dry conditions (`SurfaceType.cs:113-131`), so the result is an inert tag rather than changed
  grip. The fix is a single Tarmac tag on `Floor`.
- What the car actually rests on in `Hard` remains `UNKNOWN`; the settling check is one downward
  `Physics.Raycast` from the spawn reporting `hit.collider.name` and `hit.point.y`.

**Q.** The tagger's verify pass warns when a tag the car's ray cannot see is found. What did that
warning say on the run that produced `c03c220`, and what happened to it?
**Q.** What would have made the `Floor` collider the obvious thing to tag?

### Thread 3 — "22.1 m turning circle" was read wrongly for weeks

**Facts.**

- The CSV records `Turning circle width 22.0967 m` at a held speed of 9.8399 m/s, with
  `Turning radius 11.0484 m` and `Limiting factor 0`, meaning steering-limited and not grip-limited.
- That figure is the widest span of the **first** lap of a U-turn. `maxYawAcceleration` 16 rad/s²
  and `steerInputSmoothTime` 0.07 s mean the car spirals in rather than snapping onto the circle, so
  the entry transient adds roughly 1.8 m. The CSV's own note records the textbook value as 20.3 m.
- The steady-state circle at 10 m/s is 20.3 m. Below 5 m/s the `maximumCurvature` 0.35 cap binds and
  the circle is 5.71 m — about a quarter of the quoted figure.
- `turning-geometry-2026-10-14/report.md` §3.1 states the position directly: "The 22.1 m figure is
  correct and is being read wrongly", and adds that arrivals are low-speed manoeuvres, so the quoted
  figure "has been scaring the design away from corners the car can actually take."

**Q.** What decisions were deferred, avoided or argued for while 22.1 m was the working number?
**Q.** Where did the figure get repeated — in conversation, in a document, in a design decision — and
does any of that need correcting now?

### Thread 4 — A merge broke a tuning asset

**Facts.**

- `03e0752`, 29 September 2026, author Kyuri: "Merge branch 'feature/orders-cargo' into
  feature/vehicle-camera". That merge de-indented `whyThisSetup` in `OrderTuning_Balanced.asset` to
  column 1, which closes the YAML mapping and strands every key below it.
- The stranded keys include `orderTimeLimit: 50` (`OrderTuning_Balanced.asset:33`). The C# field
  initialiser is `45f` (`OrderTuning.cs:31`), and the shipped build holds 45.0 (read from
  `sharedassets1.assets` at offset 113113600+8).
- `orderTimeLimit` is the only field after the break whose C# default differs from its asset value,
  which is why nothing else visibly changed.
- `whyThisSetup` is not a field in `OrderTuning.cs` at all. It is a rationale comment written as if
  it were data.
- Route costing: the longest configured pickup-to-drop-off pair in the logged scene is 954 m, which
  needs roughly 48 s at the project's 20 m/s working average. 35 of 36 pairs clear 45 s; all 36
  clear 50 s.
- Consequence: one route was unwinnable in the shipped build while the asset read as though it was
  not.
- The asset belongs to the order system, which is Kyuri's. The merge discipline is `GIT-RULES.md`
  rules 6 and 9, and Ako is named as merge owner and pre-merge checker in both.

**Q.** What check would have caught a YAML indentation fault in a `.asset` file, and is that check
worth what it costs to run on every merge?
**Q.** The pre-merge check in rule 9 is "play for at least 60 seconds with zero console errors". A
broken order limit produces no console error. Does the rule need changing, and who should decide
that?

### Thread 5 — An untracked 304 MB FBX nearly destroyed the working environment

**Facts**, all recorded in `.gitignore` comments rather than reconstructed here.

- `Assets/_Project/Models/Group18_Greybox.fbx`, 304 MB, 23 September 2026. `Assets/_Project/` is the
  husk left behind when the source folder was renamed to `Assets/Project`.
- Mechanism: `.gitattributes` routes `*.fbx` through `filter=lfs`. The editor's source-control
  integration polls `git status --untracked-files=normal`, then calls
  `git hash-object --stdin-paths` on what it finds, and that call runs with the LFS filters live, so
  every poll pushed the whole 304 MB through a temp file.
- Result: `.git/lfs/tmp` reached **96.95 GB across 831 files in under an hour** and filled a 464 GB
  disk, which stopped Unity from being able to build at all.
- Same shape twice more, both now ignored with the reasoning recorded: a Windows player build at
  `Game Build/`, 486 MB across 197 files including `UnityPlayer.dll` and the whole `_Data` folder;
  and a git worktree under `/.worktrees/`, which is a complete second checkout and would have fed
  the whole `Assets` tree to the same loop on every poll.
- The `.gitignore` entry for `Assets/_Project/` also records that the folder should be deleted once
  someone confirms the 304 MB export is not the only copy, and that
  `Assets/Aside/Art/Blender` holds the same greybox at 192 MB.

**Q.** What made the cause findable, as opposed to the symptom?
**Q.** What would you now check first on a new machine, before opening the editor?

### Thread 6 — Fabricated playtest data was proposed and refused

**Facts.**

- Five machine and AI-QA telemetry rows were relabelled as human participants, with invented tester
  identities, experience levels and verbatim quotes. The proposal was rejected.
- The structural fix is a `session_type` column in **position two** of
  `docs/playtesting/playtest_sessions.csv`, taking the values `human`, `machine-log`, `ai-qa` and
  `mixed-ai-user`.
- The reasoning is written into `docs/playtesting/README.md` under "Why `session_type` exists": the
  honest caveats previously lived in `observer_notes`, the last of thirty-three columns and therefore
  off-screen in any spreadsheet, and the README states that five rows in a sheet whose stated minimum
  is five testers invites exactly the wrong conclusion. The column turns the real-tester count into
  a filter rather than a careful read.
- The underlying observations were real and were already evidenced in
  `docs/QA-PLAYTEST-REPORT-2026-10-14.md` and `docs/playtesting/QA-AI-20261014-01.md`. Nothing was
  lost by refusing; only the attribution changed.
- Current composition of the sheet: 7 rows — 1 `machine-log`, 2 `mixed-ai-user`, 2 `ai-qa`,
  2 `human`. `docs/playtesting/README.md:74` still states the sheet holds zero `human` rows, which
  is behind the data.

**Q.** At what point was the relabelling caught, and what caught it — the README's five-tester
minimum, a read of the sheet itself, or something else?

*Note: this thread is included because the artefact trail exists, not because the reflection must use
it.*

---

## B4. The hypothesis

> Does automatic order pressure, combined with demanding but predictable driving, make recovery
> satisfying?

**The three findings, as facts** (the full argument is `docs/M2-INDIVIDUAL-AKO.md` §4.4 and
`M2-PLAYTEST-WRITEUP.md` §4.1, §4.6, §4.7):

1. **The reward half has never fired.** `boost_activations`, `boost_seconds`, `boost_earned` and
   `handbrake_seconds` are 0 in all seven rows of `playtest_sessions.csv`. `flips` is 0 in all seven
   too. The code is proven under the harness: 3.0083 s tank, 40.1278 m/s peak, 37.5 units from a
   1.875 s drift. Present, correct, inert.
2. **The predictability half has one data point, positive.** In session S01 the participant rated the
   controls easy and could identify their own mistakes when losing control — the substance of
   `q_knew_why_lost_control`. Not captured as a score.
3. **Three independent sessions converge on navigation.** Two of three family testers could not tell
   where to go while none struggled with tickets, timers or values; S01 requested a minimap, a
   directional indicator and a post-pickup route line; wall hits across the seven logged runs are 7,
   1, 0, 0, 0, 1, 2.

**One reading that needs accounting for, not hiding.** One S01 row carries
`time_above_supersonic_s` 2.5 while its boost columns are 0. `IsSupersonic` is
`Speed >= supersonicThreshold` where `Speed` is the rigidbody's 3D linear-velocity magnitude rather
than forward speed (`VehicleController.cs:146`, `:901`), and the logger accumulates on that flag
alone (`PlaytestLogger.cs:196`), so at gravity 20 m/s² a fall passes 38 m/s in under two seconds.
The column can therefore register without boost. Nothing in the log separates vertical from forward
speed; the settling check is logging `ForwardSpeed` alongside `Speed`.

**The instrument.** `docs/playtesting/QUESTIONS.md` under "What each question is for" decomposes the
design question word by word: *predictable* → `q_car_predictable` and `q_knew_why_lost_control`;
*demanding* → `q_in_control_at_speed` read against measured `wall_hits` and
`time_above_supersonic_s`; *recovery* → `q_regained_control_was_mine` and
`q_recovery_satisfying_or_frustrating` against measured `longest_recovery_s`; *satisfying* →
`q_wanted_to_continue` and `q_best_moment`.

**The limits, stated plainly.**

- Zero scored 1-to-5 answers exist from anyone. All thirteen `q_` columns are blank on both `human`
  rows.
- The protocol minimum — five testers, at least two answering "never" or "sometimes", one build, one
  variant, the same questions in the same order — is unmet.
- `tester_racing_experience` was never asked of anyone.
- `variant` is effectively `baseline` on every row, so `boost-regen` — the one comparison that would
  directly address finding 1 — is untested.
- Neither the three-person family session nor S01 followed `QUESTIONS.md`. Neither is a controlled
  playtest.
- All logged runs were played on `Possible game scene`, so the surface-traction model and the 33
  knockable props have never appeared in logged play at all.

**Q.** What was the expectation about what an unguided first-time player would do with the handbrake?
**Q.** Does the inert reward half change the hypothesis, change the mechanic, or change the way the
mechanic is taught? These are three different answers and the reflection should pick one.
**Q.** Finding 3 says the bottleneck is navigation, which is not in this area of contribution. What
does that mean for how the next version of this area is prioritised?

---

## B5. Group workflow, collaboration, communication and role fulfilment

**Facts.**

- `docs/GIT-RULES.md` holds eleven numbered rules. Ako is named as merge owner (rule 6, with Kyuri
  as fallback after 24 hours of no reply), tag owner (rule 7) and pre-merge checker (rule 9).
- Branch ownership per `README.md`: `feature/vehicle-camera` Ako, `feature/orders-cargo` Kyuri,
  `feature/greybox-ui` Zubuhle. The ownership tables are asserted by
  `docs/tooling/repo-checks/test_documents.py::test_readme_ownership_tables_contract`, so they
  cannot drift silently.
- `git shortlog -sn --all` → Ako Baloyi 65, Zubuhle Nel 57, Kyuri Reddy 41. Scope is `--all`, every
  ref. `.mailmap`'s own comment quotes an older and narrower split of 37 / 21 / 7.
- `.mailmap` exists because Ako's commits are split across two author names ("AkoBaloyi" and
  "Ako Baloyi") from one email address, so `git shortlog` reported four contributors for a team of
  three. Its comment records why a mailmap was chosen over rewriting history: a rewrite would have
  changed every commit hash on every branch for all three contributors, which the comment calls a far
  worse trade.
- `.gitattributes` sets `merge=union` on `VehicleLab_Measurements/*.csv` and
  `docs/playtesting/*.csv`, with the reasoning that both files are append-only and conflicted on
  four separate merges in a row. `git check-attr merge` confirms `union` on both. The same comment
  warns against copying the rule to a file where two people edit the same line.
- Unity Smart Merge (`unityyamlmerge`) is required for `.unity` and `.prefab` conflicts, with a
  30-minute escalation to the system owner (rule 11).
- The penalty/reward split is visible in the code and in the ownership tables: hard-braking and
  collision penalties live in Kyuri's `CargoCondition`, which reads
  `vehicle.Tuning.brakeDeceleration` each time it tests a stop (`CargoCondition.cs:91-92`), while the
  sliding and air-time reward lives in Ako's boost earn path (`VehicleController.cs:910-920`). How
  that division was reached is **not recorded anywhere in the repository**: no file, commit message
  or commit diff on any ref mentions a negotiation over it or an alternative single shared score
  (searched with `git log --all --grep`, `git log --all -S`, and a text search of `docs/`,
  `README.md` and `Assets/Project/Scripts/`).
- A constraint was accepted from the level artist: prop and building sizes are hand-placed and were
  not changed, so mass carried the feel instead of scale (`KnockablePropBaker.cs:72-87`).
- The props and surfaces were baked into the scene then named `Kyuri's Scene.unity`, which
  `origin/main` renamed to `Hard.unity`. The local and remote copies carry the same 33 props and 84
  tags.
- Session S01, 14 October 2026: `observer_notes` records that one teammate was absent through
  illness and another stepped out during the session.
- One file is genuinely shared: `ShiftResults.cs`, authored at `7788db4` (Ako, 29 September) and
  extended at `7022448` (Kyuri, 14 October, level progression).

**Two unfinished seams in work Ako owns.**

- `git tag -l` returns nothing, despite rule 7 assigning checkpoint tagging to Ako and specifying
  the exact commands.
- `docs/BUILD-LOG.md` contains its header and a single example row dated 2025-01-15. No real build
  has been logged, including the 14 October standalone build whose timestamp is recorded elsewhere as
  `2026-10-14 08:31:46`.

**Q.** Which of the eleven rules was hardest to hold to, and what made it hard?
**Q.** On the penalty/reward split, which the repository records only as an outcome: what was
proposed, by whom, and what was the stated reason for settling on two owned halves rather than one
shared number? If the only record is recollection, say so in the reflection rather than citing a
file.
**Q.** What was done when a teammate's merge broke something in this area (thread 4), and what was
communicated to whom?
**Q.** Rule 7 and the build log are both unmet. Was that a decision, a drift, or a consequence of
something else?

---

## B6. Evidence appendix — dated timeline

Commits are `git show --no-patch` verified. Non-commit events are marked.

| Date | SHA / event | Subject |
| --- | --- | --- |
| 2026-09-07 | `b4a9ae4` | Add Unity project and current gameplay work |
| 2026-09-09 | `0dc80d1` | Add code-driven car visuals and update vehicle tuning |
| 2026-09-10 | `3c52f6b` | Add surface types with per-surface grip, drive and braking |
| 2026-09-10 | `25d9d61` | Add skid marks and surface-aware tyre particles |
| 2026-09-10 | `bbb86c3` | Make throttle and steering fight for grip |
| 2026-09-14 | `8f4ce85` | Make the handbrake lock the rear axle, and put it on Space |
| 2026-09-17 | `e9ce21c` (Kyuri) | Implement order, cargo, zone detection, shift timer, and order UI |
| 2026-09-23 | `1c52e42` | Bind the arrow keys, and keep the lab instruments out of player builds |
| 2026-09-23 | **event** | 304 MB FBX / 96.95 GB `.git/lfs/tmp` / 464 GB disk filled (thread 5) |
| 2026-09-23 | **event** | Milestone 1 deadline |
| 2026-09-29 | `311cdc8` | Keep the chase camera out of the buildings |
| 2026-09-29 | `7788db4` | Close the loop: a results screen when the shift ends, and a Restart that works |
| 2026-09-29 | `03e0752` (Kyuri) | Merge `feature/orders-cargo` into `feature/vehicle-camera` — broke the order tuning YAML (thread 4) |
| 2026-10-04 | `a3a1351` | Boost is earned, and the car finally shows it |
| 2026-10-07 | `e0bcf89` | Fix the boost earn test, which could only ever report zero |
| 2026-10-07 | **event** | Measurement session `13:56` — the 43 readings quoted throughout |
| 2026-10-12 | `ca9a463` | Make the playtest measurable: a session logger, and the sheet it feeds |
| 2026-10-12 | **event** | First logged shift, 13:33:39 |
| 2026-10-13 | `a4b6649` | Knockable street props: a baker for the first four families |
| 2026-10-13 | `e1f1708` | The prop baker saves the scene it changes |
| 2026-10-13 | `7f357c7` | Knockable props, third attempt: pull each prop out of the city's broken transform chain |
| 2026-10-13 | `752943e` | Size the box from the vertices that exist |
| 2026-10-13 | `9e562f7` | The 9 m tables are real, so the tool now says so |
| 2026-10-13 | `d80d61e` | Mass is a feel number |
| 2026-10-13 | `457d1f3` | Seat props on the road, copy the hand-fitted blocker box, tag the city's surfaces |
| 2026-10-13 | `c03c220` | Scene: tag 84 city surfaces, re-seat knockable props |
| 2026-10-13 | `395497b`, `1efc886` | Rotation-aware seating fix — on `fix/seat-on-road-rotation` only |
| 2026-10-14 | **event** | Standalone build, 08:31:46; four logged shifts 09:09–09:38 |
| 2026-10-14 | **event** | Session S01: one participant, two logged shifts, recorded, run in the editor at `618f2be` |
| 2026-10-14 | **event** | Fabricated-data proposal refused; `session_type` added (thread 6) |
| 2026-10-14 | `618f2be` | Ignore the six paths that kept showing up untracked |
| 2026-10-14 | `7022448` (Kyuri) | Level progression — extends `ShiftResults.cs` |

---

## B7. Before pasting anything into the submission

- [ ] Every figure re-checked against the file, CSV row or command cited here, not against this
      scaffold.
- [ ] No sentence kept that could not be said out loud in the marking session.
- [ ] No claim that the surface-traction model is working on the roads (B3 thread 2).
- [ ] No claim that earned boost rewards good driving in practice (B4 finding 1).
- [ ] Neither the three-person family session nor S01 described as a controlled playtest, and the
      unmet five-tester minimum stated.
- [ ] The 7 → 1 → 0 wall-hit trend carried with its caveat, never as a result.
- [ ] `Profile` rows from `vehicle_measurements.csv` never quoted as measurements — F7 echoes the
      tuning asset.
- [ ] Scene claims name their tree: `feature/vehicle-camera` @ `618f2be` or `origin/main`.
- [ ] Scene-specific findings from `QA-PLAYTEST-REPORT-2026-10-14.md` and `QA-AI-20261014-01.md`
      described as stale, since both predate the Easy/Medium/Hard restructure.
- [ ] No screenshot referred to as if it exists unless it has actually been captured.
- [ ] No decision called negotiated or agreed unless a file, commit or report records it; where the
      only record is recollection, said as recollection (B5).
