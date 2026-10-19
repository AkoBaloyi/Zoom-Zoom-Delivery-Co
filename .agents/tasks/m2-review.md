# Milestone 2 individual documentation — second review pass

Second pass over `docs/M2-INDIVIDUAL-AKO.md` (File A, rubric criteria 1, 2 and 4) and
`docs/M2-REFLECTION-SCAFFOLD.md` (File B, working material for criterion 3), judged against the
no-fabrication contract in `.agents/tasks/m2-individual-ako-plan.md` §6 and the nine gate conditions
in the review brief. All eight findings from the first pass (`.agents/tasks/m2-review.json`, verdict
`CHANGES_REQUESTED`) are fixed, including the one blocking finding: the unsourced "driving quality"
negotiation is now written in both files as an absence of record, with the searches named, and File B
converts it to a question for the author. I re-ran the first pass's own searches and they still come
back empty, so the new wording is the accurate one.

This pass independently traced roughly sixty figures to source rather than accepting the documents'
labels. Every one reproduced, including the hard ones: the null-mesh Tarmac collider at
`Hard.unity:199870`, the 2/22/60 tag histogram, 33 `KnockableProp` components, 7 null-mesh
MeshColliders among 1295, 633 collider-less road tiles under `Roads_Parent`, `Floor` carrying no
`SurfaceType`, the 126-vs-30 Transform count across trees, 35-of-36 routes clearing 45 s, every
measurement-CSV reading, and all 26 cited commit SHAs. The four gate conditions that exist to stop
the document flattering the work — surface traction broken on roads, earned boost inert in play,
neither session a controlled playtest, QA reports flagged stale — are each met in the document's own
words, in the executive summary as well as the body.

**Watch for:** seven precision defects, none blocking. The most substantive (confirmed) is that the
`git show --numstat e9ce21c` figures in §4.1 reproduce only with `--no-renames`; run as cited, git
reports the files as renames at `0 0`. Also confirmed: §1.1 sources the live 12 s / 3-active cadence
to asset keys that the same paragraph then explains are unreachable; the §0.1 rubric map reads as
though File B is author-written; and `play_seconds` is described as a sheet column when it is a
shift-log field quoted inside `observer_notes`.

**Verdict**: APPROVED

## High-level view

The no-fabrication contract holds. Every figure I pulled resolved to the file, CSV row, scene
document or named report cited for it, and I found no claim contradicted by its source. Where the two
documents disagree with an upstream artefact they say so and name the artefact: the README's
handbrake wording is looser than the code, `docs/playtesting/README.md:74` is behind the sheet,
`.mailmap`'s 37/21/7 is superseded, the `KnockableProp` tooltip's negative-scale mechanism is
unsupported by the later measurement, and `Tuning_Balanced.asset`'s own prose is wrong in three
places.

The four honesty gates are met in the document's own words rather than by implication, and in §0 as
well as the body. §2.3 carries the heading "The tagging is wrong on the roads, and the error is the
author's own" and names both the failure and its fix. §0 and §4.4 both state that the reward half of
the design question has never fired, and Figure 1 classes the earn-and-spend chain separately rather
than burying the caveat in prose. §2.9 states the five-tester bar unmet and neither session a
controlled playtest. §4.4 flags both QA documents stale for their scene-specific findings and marks
the AI-operated one as not player-experience evidence.

File B is working material throughout. Zero first-person matches across the whole file, an opening
sentence that states it is not submission text, every B1–B6 bullet carrying a date, SHA, path or
figure, and open questions in every section including the one the first pass flagged as a statement
wearing a question marker. Nothing in it attributes a feeling, opinion or intention to the author;
the two places where a motive would be natural to assume — the penalty/reward split and the unmerged
rotation fix — are written as recorded outcomes plus a question.

The defects that remain are citation precision in a document that sets itself an unusually high
citation standard, which is why they are worth listing at all. One figure reproduces only with a flag
the document does not name; one citation points at a stranded YAML key; one scene-line citation marks
only the fourth of four origin/main-only reads as origin/main; one rubric-map cell could be read as
claiming the scaffold is the author's own writing; and one telemetry field is described as a sheet
column when it lives in the shift log. The document also runs well past the authoring plan's prose
target, which is a plan-conformance matter rather than a truth matter.

<details>
<summary>Issues (7)</summary>

1. **`e9ce21c` figures need `--no-renames`** — §4.1 (File A:573) states the commit "adds those files
   as pure insertions with zero deletions — 321 lines for `ChaseCamera.cs`, 296 for `SurfaceType.cs`
   (MEASURED, `git show --numstat e9ce21c`)". Run as cited, rename detection reports
   `0  0  Assets/{_Project => Project}/Scripts/Vehicle/ChaseCamera.cs`. The 321/296 figures appear
   only under `--no-renames`, which also shows matching deletions at the old `Assets/_Project/` path.
   Name the flag, or describe the commit as the `Assets/_Project` → `Assets/Project` rename it is —
   which supports the paragraph's conclusion more directly than the insertion reading does.
2. **Live cadence sourced to a stranded key** — §1.1 (File A:73, claim index A:721) cites
   `OrderTuning_Balanced.asset:31-32` for the 12 s spawn interval and 3 active orders, two sentences
   before explaining that the `whyThisSetup` break at column 1 strands every key below it. Those two
   keys are below the break. The values are right because the C# defaults match
   (`OrderTuning.cs:24,27`); cite those for the live figures and keep the asset citation for what the
   asset says.
3. **Rubric map implies File B is author-written** — §0.1 row 3 (File A:45) reads
   "`docs/M2-REFLECTION-SCAFFOLD.md`, written by the author". The scaffold is generated working
   material; §3 states the correct position. Reword the cell to match §3.
4. **`play_seconds` described as a sheet column** — §0 defect (c) (File A:36) and §2.6 (File A:429)
   say the two S01 rows "carry `play_seconds` 0.0". `playtest_sessions.csv` has no `play_seconds` or
   `shift_seconds` column; both are `PlaytestLogs/shift_log.csv` fields quoted inside
   `observer_notes` ("Logged play_seconds=0.0; shift_seconds=11"). Say where the figure lives.
5. **Scene citations under-marked for tree** — §1.1 (File A:69) and the claim index (A:719) cite
   `Hard:2301`, `Medium:54336`, `Easy:5568` and `origin/main Possible game scene:1713`, naming only
   the fourth as `origin/main`. All four are origin/main reads; Hard, Medium and Easy do not exist on
   the described tree at all, where the scene is `Kyuri's Scene.unity`. Mark the first three too.
6. **Prose length past the plan's range** — plan §2 sets 2,800–4,000 words of prose excluding tables
   and fenced blocks and 150–250 words for §0; the file measures roughly 5,830 and 252 on a count
   that excludes fences, table rows and blockquotes. Either trim or record the deviation as a
   decision, since the plan is the stated contract.
7. **§5.3's pasted `git status` has gone stale mid-review** — the block in §5.3 (File A:756-779)
   lists ` M ProjectSettings/ProjectSettings.asset` and explains it as pre-existing editor state.
   That line matched a fresh run at the start of this review and no longer does: the author's open
   editor session has since reverted the file, so
   `git status --porcelain -- Assets ProjectSettings Packages` now returns nothing at all. The
   document's substantive claim is unaffected and in fact strengthened, but the block is presented as
   verbatim current output. Re-paste it at finalize time rather than leaving the stale line.

</details>

<details>
<summary>Details</summary>

### What this pass checked, and how

I judged rather than re-ran: Unity was not opened, no scene or asset was modified, and the author's
`compile_check.py` and pytest runs are left to the finalize step. Everything below is a read-only
spot-check of a specific articulable doubt about a claim, using the sources the documents name.

Scene facts came from `.agents/tasks/design-overview-2026-10-14/ref/`, the extracted `origin/main`
blobs, parsed with a throwaway Python script over the YAML documents rather than by eye, because the
tag and prop counts are the kind of figure that is easy to assert and tedious to verify. Commit facts
came from `git log --all`, `git show --numstat`, `git branch -a --contains` and `git check-attr`. CSV
facts came from `Import-Csv` and from reading the raw header, since the sheet's column positions are
themselves a claim in §2.9.

### The surface-traction gate, verified in the scene file

This is the gate most likely to be softened, so I traced it end to end. `Roads_Parent` is GameObject
`&8744834134912007418` with three components. One is a `MeshCollider` (class 64) whose
`m_Mesh: {fileID: 0}` sits at `Hard.unity:199870` — the exact line the claim index cites. One is a
`SurfaceType` (class 114, script GUID `95c5eae1aeacb5341adbe50a1c9f4a67`) carrying `kind: 0`, Tarmac.
Its Transform has 633 direct children and 633 total descendants, of which **zero** own a collider of
any class. `Floor` is `&1551314015` with four components — Transform, MeshRenderer, BoxCollider,
MeshFilter — and no `SurfaceType` among them. The second Tarmac tag is on a `road_base` object, which
`notes-map.md:349` places at world y = −47.902, 48 m below the map.

Tag histogram over all 84 `SurfaceType` components in `Hard`: `{0: 2, 3: 22, 6: 60}` — 2 Tarmac,
22 Grass, 60 Concrete, matching §2.3 and the claim index exactly. Null-mesh tally: 7 of 1295
MeshColliders, matching `notes-map.md:320-321`, which names all seven as container nodes.

The two qualifications the document attaches hold as well: `Default` is numerically Tarmac when dry
(`SurfaceType.cs:113-131`, grip/drive/brake all 1), so the live consequence is an inert tag rather
than changed grip; and what the car rests on remains UNKNOWN pending one downward raycast.

### The inert-boost gate, verified against the sheet and the harness

All seven rows of `playtest_sessions.csv` carry `boost_activations`, `boost_seconds`, `boost_earned`
and `handbrake_seconds` at 0, and `flips` at 0. Composition is 1 `machine-log`, 2 `mixed-ai-user`,
2 `ai-qa`, 2 `human`, with `session_type` in column 2 of 33 and 13 `q_` columns all blank.

On the harness side, `Boost` rows at `2026-10-07 13:56:45` give full tank 3.0083 s and peak
40.1278 m/s; `13:56:50` gives slide held 2.5 s, time counted as drifting 1.875 s, boost earned 37.5
— exactly 1.875 × 20, so the document's claim to have confirmed the 20 units/s rate by measurement
rather than from the asset stands. `boostConsumptionRate` is 33.3 at `VehicleTuning.cs:202` and in
the asset, giving the 100 ÷ 33.3 = 3.00 derivation. `boostRechargeRate` is 0 in the asset and the
recharge branch is gated `> 0f` at `VehicleController.cs:885`.

The `time_above_supersonic_s` 2.5 anomaly on one S01 row is accounted for rather than hidden:
`Speed` is `_rb.linearVelocity.magnitude` at `VehicleController.cs:146`, `IsSupersonic` tests it
against the threshold at `:901`, `supersonicThreshold` is 38 in the asset, and
`PlaytestLogger.cs:196` accumulates on the flag alone — so a fall can register the column without
boost, and the document labels that reading INFERRED and names the settling check.

### Measurement and tuning figures

Every quoted reading reproduced from `vehicle_measurements.csv` (640 data rows, 641 lines, all
`Balanced`): stopping 18.0948 m from 31.3653 m/s; turning circle width 22.0967 m at 9.8399 m/s with
`Limiting factor 0`; wall recovery 1.7583 s from 19.9 m/s; flip displacement 4.4439 m over 0.4583 s
against 4.6895 m driving with `Flip is not a shortcut = 1`; camera road ahead 42.7612 m forward and
−8.5790 m behind at 31.2167 m/s, both PASS.

Asset values: mass 900 at line 33 with `maxAngularSpeed` at 35 — the claim index's parenthetical
about the line numbers is right; `topSpeed` 32; `lateralGripAcceleration` 13 against a C# default of
12 at `VehicleTuning.cs:297`; `gripBalance` 0.58; `frictionCircleStrength` 0.8;
`longitudinalGripReference` 16; `maximumCurvature` 0.35; `maxYawAcceleration` 16;
`steerInputSmoothTime` 0.07; `driftSlipAngleThreshold` 12; `driftMinimumSpeed` 4;
`driftYawAuthority` 0.55; `cameraLookAhead` 28; `cameraCollisionRadius` 0.4;
`cameraCollisionRecoverTime` 0.35; `flipMaxSpeedGain` 3 at `:481`; `flipNoSpeedGainAboveSpeed` 12 at
`:486`, enforced by the headroom clamp at `VehicleJumpFlip.cs:268-270` and the cap at `:273-281`.

The 29.1 m/s crossover checks out arithmetically: `MaxCurvatureAt` lerps overdrive from
`steerOverdriveAtLowSpeed` 0.6 to `steerOverdriveAtTopSpeed` 1.15 over 0 → 40 m/s
(`VehicleTuning.cs:735`), so overdrive reaches 1 at 0.4/0.55 × 40 = 29.09 m/s. The friction-circle
chain — rear 10.92 → 2.18, effective lateral 12.78 → 8.63, 11 % wider at 10 m/s — is in the
turning-geometry report at §3.4 lines 178-198, and the report's own §3.1 line 107 carries the
sentence the scaffold quotes: "The 22.1 m figure is correct and is being read wrongly."

### The props, the camera gap, and the correction against the project's own README

Prop families in `Hard` come out at 12 mobile toilets at 180 kg, 9 picnic tables at 120 kg, 9 trash
containers at 60 kg and 3 road blockers at 25 kg — 33 total, matching the `KnockableProp` component
count and §2.4 family for family. 16 `cone_type` GameObjects exist in the scene and none carries a
`KnockableProp`, so the "declared in the baker table, zero instances" reading is right; the family is
declared at 5 kg at `KnockablePropBaker.cs:92`.

The camera's reverse gap verifies as a genuine absence rather than a mis-tuning: `ChaseCamera.cs`
contains no reference to `ForwardSpeed` at all, and `ComputeHeading` runs
`Vector3.Slerp(nose, velocity.normalized, …)` at `:267` behind a `velocity.magnitude < 1.5f` gate at
`:264`, which is the degenerate antiparallel call §2.5 and the turning-geometry report describe.

§2.2's correction against this project's own `README.md` holds. `README.md:55` says "a handbrake
slide pays into it at 20 a second", while the earn path at `VehicleController.cs:910-920` keys off
`IsDrifting` — grounded, `driftMinimumSpeed` 4, `driftSlipAngleThreshold` 12 at `:681-683` — and
reads nothing about the handbrake. A throttle-induced slide pays identically, as the document says.

### Order-system figures the document borrows from a teammate's system

The YAML break is real: `whyThisSetup` starts at column 1 on line 16 of
`OrderTuning_Balanced.asset`, and `orderTimeLimit: 50` sits at line 33, below it. I checked every
C# default against every asset value after the break — `spawnInterval` 12f/12, `maxActiveOrders`
3/3, `baseDeliveryValue` 10f/10, `latePenaltyPerSecond` 1f/1, `valuePerMetre` 0.1f absent from the
asset — and `orderTimeLimit` (45f vs 50) is the only one that differs. That is the load-bearing claim
behind "nothing else visibly changed", and it is the same check that produces issue 2: the two keys
§1.1 cites for the live cadence are below the break with it.

Route costing from `notes-map.md` §6.2, `Possible game scene` column: median 541 m / 27.1 s, longest
954 m / 47.7 s, clear 45 s 35/36, clear 50 s 36/36. `WORKING_AVERAGE = 20.0` is at
`delivery_points.py:49` with the comment the document paraphrases, and §1.1 correctly flags that it
is a constant rather than a lap time and has not been re-based.

Value formula: `OrderManager.cs:298` multiplies `DeliveryValueFor` by `Mathf.Clamp01(condition)`;
`OrderTuning.cs:48-50` is base + distance × rate. Zone radii 6 m at `ZoneDetection.cs:21-22`.
`shiftDurationSeconds: 300` at `Hard:2301`, `Medium:54336`, `Easy:5568` and `PGS:1713` — all four
line numbers hit exactly, in the origin/main blobs.

### Telemetry claims, including the two defective columns

`PlaytestLogger.cs:90-93` is a 15-column header beginning `timestamp,scene,shift_seconds` — the first
pass's wording fix is correctly applied. Thresholds match the document's "same terms as the harness"
claim: `impactSpeedDrop` 3f, `impactMinimumSpeed` 10f, `usefulSpeed` 8f, used at `:199` and `:215`.

The `shift_seconds` defect reads as described. `WriteRow` is reached from the destroy path at `:134`,
and `:244` is `shiftTimer != null ? ShiftLengthSeconds() : 0f`. The comment at `:131-133` does claim
the opposite of the observed behaviour — it says an early quit is "flagged by a shorter play_seconds
than shift_seconds" — so §2.6's point about an analyst's filter missing exactly those rows is sound.
Two `ai-qa` rows record `shift_seconds=0` against 268.7 and 221.4 logged play seconds; §2.6's
"observed early-exit path" is the accurate phrasing, and is a correction of the authoring plan's
broader "every observed exit path".

### Sheet-as-instrument claims in §2.9

The first pass's column-split fix is correctly applied and verifies: `playtest_row.py:32-44` holds an
11-entry `MEASURED` map, `docs/playtesting/README.md:79-80` says eleven measured and thirteen asked,
`play_minutes` comes from an operator flag at `:178` and is blank on all seven rows, and the header
is 33 columns. 11 + 13 + 9 identity/context columns = 33.

`README.md:74` does still say the sheet holds zero `human` rows against two in the data, and §2.9
names that line as behind the data with the sheet as authority — which is the handling the plan's
stale-source table asks for.

### Contribution and integration claims

`git shortlog -sn --all` returns 65/57/41 to the name. `git tag -l` is empty. `docs/BUILD-LOG.md`
holds its header, five process rules and one example row dated 2025-01-15. `git check-attr merge`
returns `union` for both CSV families. `GIT-RULES.md` has eleven numbered rules with Ako named at
rules 6, 7 and 9 and the 30-minute escalation at rule 11.
`test_documents.py::test_readme_ownership_tables_contract` exists at line 511, so the ownership claim
really is machine-checked.

All 26 cited SHAs resolve with the subjects both documents give them, including the prop chain in
order on 13 October and `03e0752` as Kyuri's 29 September merge. `git branch -a --contains` returns
`fix/seat-on-road-rotation` alone for both `395497b` and `1efc886`, so File B's statement that the
rotation-aware fix is on no shipping branch is correct.

The `.gitignore` incident figures are in the file's own comments, not reconstructed: 304 MB,
96.95 GB across 831 files in under an hour on 23 September 2026, a 464 GB disk, the 486 MB
`Game Build/` across 197 files, and `/.worktrees/`. `git check-ignore -v` confirms both M1 prompt
files are ignored at `.gitignore:144-145`, so §2.1's caveat that a marker cannot open them from a
clone is right. `M1-GEMINI-PROMPT-PACK.md:571` and `:573` are the two rubric headings the delta table
anchors to, and `:571` contains both "338 rows" and "five editor tools".

### Audio, build settings and tree differences

No `AudioSource`, `AudioClip` or `AudioMixer` reference anywhere under `Assets/Project`, and an
`AudioListener` in each of the four project scenes, matching `notes-friction.md` §6 "Audio: confirmed
absent". `Easy.unity` appears in neither `EditorBuildSettings.asset`; `origin/main` adds Medium and
Hard only. `Possible game scene.unity` has 126 `!u!4` documents locally against 30 on `origin/main`,
with one `PrefabInstance` there — the figures §4.4 quotes, and the reason it declines the "bare plane"
reading that `DESIGN-OVERVIEW.md` §6a struck.

### Register and content-safety gates

A superlative and marketing-tone scan over both files returns one hit, `RobustLocalBounds`, which is
a method name. No sentence in either file proposes changing prop or building sizes; §2.4 and §4.3
both record the hand-placed sizes as a constraint accepted and not touched, with mass carrying the
feel instead. §5.3's claim that no game content moved holds: at the start of this review
`git status --porcelain -- Assets ProjectSettings Packages` returned only
`M ProjectSettings/ProjectSettings.asset`, the pre-existing editor-session change the document names,
and by the end of it that file had reverted and the scoped command returned nothing. The two new
`docs/` files are the only additions attributable to this work. The side effect is issue 7: the
verbatim block in §5.3 no longer matches a fresh run by exactly that one line.

### Structure against gate condition 7

Top-level sections map to the rubric criteria (§1 macro, §2 feature and communication, §3 reflection
pointer, §4 contribution), §0.2 is a 120-word scope statement naming Ako's nine claimed systems and
the three not claimed, three Mermaid blocks are present with Figure 1 classing this author's arcs and
marking the earn-and-spend chain inert and Figure 2 showing the vehicle/camera/telemetry boundary
against the order system with the three crossings marked, §6 is the diagram list, and §7 states
plainly that none of the six screenshots exists. Six inline placeholders all carry
"SCREENSHOT PLACEHOLDER — NOT CAPTURED".

### First-pass findings, re-checked

| # | First-pass finding | Status |
| --- | --- | --- |
| 1 | Unsourced "driving quality" negotiation (blocking) | Fixed. Both files now state no file, commit message or diff records it, name the searches, and B5 asks the author what was proposed and by whom. `git log --all -S"driving quality"` and `--grep=negotiat` both return nothing |
| 2 | `SurfaceType.cs:313-314` mis-cited for Dry | Fixed to `:317-318`; `default:` is at 317 and `return ground;` at 318 |
| 3 | `SurfaceType.cs:237-242` mis-cited for 0.92 | Fixed to `:236-245`; `gripMultiplier = 0.92f` is at 245 |
| 4 | `WeatherState.cs:34` off by one | Fixed to `:34-35`; `Current` is at 35 |
| 5 | Eleven measured columns attributed to the CSV header | Fixed; now cites `README.md:79-80` and `playtest_row.py:32-44`, and names `play_minutes` as the twelfth, blank on all rows |
| 6 | Logger column range mis-stated | Fixed; now "15 columns beginning `timestamp`, `scene`, `shift_seconds`" |
| 7 | Uncited 44–83 % late rate | Fixed; cited to `M2-PLAYTEST-WRITEUP.md:216`, which carries that sentence, and added to the claim index with the recomputed per-row shares, which I verified arithmetically from `delivered` and `late` |
| 8 | File B thread 6 Q was a statement | Fixed; now asks what caught the relabelling, with the note kept separately |

### Minor observations not worth an issue number

`variant` is `baseline` on three rows and empty on four, so "effectively `baseline` on every row"
(File A:519 region, File B:323) is carried by "effectively" rather than by the data; describing it as
three baseline and four blank would be tighter. File B:87 names five of the six paths that
`618f2be`'s own subject counts as six — `/PlaytestLogs/` at `.gitignore:169` is the sixth. Neither
changes a conclusion.

</details>

<details>
<summary>Files reviewed</summary>

| File | What it is | Judgement |
| --- | --- | --- |
| `docs/M2-INDIVIDUAL-AKO.md` | Submission prose for rubric criteria 1, 2 and 4; 7 sections, 3 inline Mermaid figures, claim index, commands-run block | Approved with six non-blocking precision issues |
| `docs/M2-REFLECTION-SCAFFOLD.md` | Working material for criterion 3; B1–B7, six evidenced failure threads, dated timeline, pre-submission checklist | Approved; no first-person voice, no invented interiority, every bullet sourced |
| `.agents/tasks/m2-individual-ako-plan.md` | Authoring contract, including the §6 do-not-write list | Read as the contract; File A correctly departs from it where the plan was wrong (the negotiated split, "every observed exit path") |

Sources opened read-only for spot-checks: `Assets/Project/Scripts/Vehicle/` (`VehicleController.cs`,
`VehicleTuning.cs`, `VehicleJumpFlip.cs`, `SurfaceType.cs`, `WeatherState.cs`, `ChaseCamera.cs`,
`BoostGauge.cs`, `PlaytestLogger.cs`, `KnockableProp.cs`, `Editor/KnockablePropBaker.cs`,
`Editor/SurfaceTagger.cs`, `Tuning/Tuning_Balanced.asset`),
`Assets/Project/Scripts/Orders/` (`OrderManager.cs`, `OrderTuning.cs`,
`OrderTuning_Balanced.asset`, `ShiftTimer.cs`, `ShiftResults.cs`, `ZoneDetection.cs`,
`CargoCondition.cs`), `VehicleLab_Measurements/vehicle_measurements.csv`,
`docs/playtesting/` (`playtest_sessions.csv`, `README.md`, `M2-PLAYTEST-WRITEUP.md`),
`docs/tooling/playtest_row.py`, `docs/tooling/scene-tools/delivery_points.py`,
`docs/tooling/repo-checks/test_documents.py`, `docs/GIT-RULES.md`, `docs/BUILD-LOG.md`,
`docs/DESIGN-OVERVIEW.md`, `README.md`, `.gitignore`, `.gitattributes`,
`ProjectSettings/EditorBuildSettings.asset`,
`.agents/tasks/design-overview-2026-10-14/` (`notes-map.md`, `notes-orders.md`,
`notes-friction.md`, `ref/*.unity`), `.agents/tasks/turning-geometry-2026-10-14/report.md`.
Unity was not opened; no scene, asset or script was modified.

</details>
