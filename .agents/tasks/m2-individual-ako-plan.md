# Authoring plan — Milestone 2 individual document (Ako Baloyi, DIGA3002A, Group 15)

Planning pass only. This plan was written read-only: no game code, scene, prefab, asset or tuning
value was touched, Unity was not opened, and no commit, branch or merge was made. The author steps
that execute this plan inherit all of that.

**Deliverables.**

| File | Contents | Rubric criteria |
| --- | --- | --- |
| `docs/M2-INDIVIDUAL-AKO.md` (File A) | Written prose, diagrams, verification index | 1, 2, 4 |
| `docs/M2-REFLECTION-SCAFFOLD.md` (File B) | Working material for the author to write from. **Not** submission prose | 3 |

**Workflow decision: the remaining steps are NOT restructured.** This is one coherent authoring
job — two files in one directory, one voice, one evidence base, heavy cross-referencing between
File A §3 and File B. Splitting it into FEAT units would multiply hand-off cost without buying
parallelism (execution is sequential either way). The existing `author-loop` + `finalize` tail
consumes this plan, and its stop contract is unchanged: the loop stops when
`.agents/tasks/m2-review.json` carries top-level `"verdict": "APPROVED"`.

---

## 0. Design decisions made in this pass

No design document was supplied for the *document*, so these were decided here. Each is one or two
sentences of rationale; the author steps implement them and do not re-decide them.

1. **Two files, exactly as the brief specifies, and no third.** File A is the submission; File B is
   working material. Mixing them would put generated first-person reflection next to assessed prose,
   which is the failure mode the 14 October fabricated-data incident already demonstrated.
2. **File A front matter order: metadata block → §0 Executive summary → §0.1 Rubric map → §0.2
   Scope → §1–§4 (the four criteria) → §5 Verification index.** The executive summary is the first
   prose section, before any rubric section, as the author requested; the rubric map sits
   immediately after it; the scope statement follows because it is the thing a marker needs before
   reading a claim of ownership.
3. **Criterion sections are numbered §1–§4 in rubric order**, so "section number" in the rubric map
   is unambiguous and a marker can map text to criterion without a key.
4. **Three Mermaid figures, all inline in File A, no image files.** Two are mandatory per the brief
   (core loop, system integration). The third (feedback-channel map) carries criterion 2's
   communication-design half, which is worth marks and reads thin without a diagram. Mermaid rather
   than images because it diffs, needs no asset, and cannot go stale against a binary.
5. **Screenshots are placeholders only, never claims.** The author cannot open Unity (the project is
   open in the editor). Every screenshot appears as a fenced placeholder naming what to capture and
   where it goes; none is referenced as if it exists.
6. **Per-claim labels inline, not in a trailing table.** `READ`/`MEASURED`/`DERIVED`/`INFERRED`/
   `UNKNOWN` with `path:line`, a CSV row, or an exact command, carried in the sentence that makes the
   claim. A trailing provenance table decouples the number from its source, which is how the 22.1 m
   turning-circle figure survived being misread for weeks.
7. **No new tests are added to `docs/tooling/repo-checks`.** The suite's 34-test count is itself
   cited as evidence in File A; adding a test would invalidate a cited figure. Verification uses the
   project's existing commands.
8. **File B is written in third-person reportage plus direct questions.** Every bullet is a fact
   about an artefact ("The commit body of `1efc886` records…"); every gap is a question addressed to
   the author ("What did you expect the fourth bake to do?"). No sentence may be a first-person
   claim about what Ako thought, felt or intended.

---

## 1. Evidence map, per rubric criterion

Paths are repository-relative. `<ref>/` means
`.agents/tasks/design-overview-2026-10-14/ref/` — the `origin/main` scene blobs extracted
read-only. Scene reads for Easy/Medium/Hard **must** come from there or from `git show origin/main:…`,
never from the working tree (the restructure is not local; see §4 row 1).

### Criterion 1 — Macro-level design documentation (15)

| Evidence | What it supports |
| --- | --- |
| `.agents/tasks/design-overview-2026-10-14/notes-vehicle.md` §1–§5 | Handling model as built: arcade by construction, force budget, grip-as-budget, boost/jump/flip numbers |
| same, §3 "Turn radius the car is allowed, by speed" and the 29.1 m/s crossover | The "demanding but predictable" line as a designed quantity |
| `.agents/tasks/turning-geometry-2026-10-14/report.md` §3.1, §3.4, §4.2–§4.6 | Speed-dependent cornering; the friction-circle mechanism; why one fix cannot serve four scenes |
| `notes-map.md` §6 "Pickup → drop-off travel time", §4.5 | Travel-time envelope across the shift; what the car stands on |
| `notes-orders.md` §3.1–§3.4, §4.1–§4.2 | The order loop Ako's work plugs into: value formula, 45 s live limit, no fail state |
| `docs/playtesting/M2-PLAYTEST-WRITEUP.md` §4.1, §4.7 | Macro consequence: the reward half of the loop is inert; the bottleneck is navigation |
| `README.md` "Controls", "Display Toggles", "Ownership" | The player-facing contract as committed |
| `VehicleLab_Measurements/vehicle_measurements.csv`, session `2026-10-07 13:56:xx` | Every measured figure quoted in §1 |

### Criterion 2 — Updated feature and communication design documentation (10)

| Evidence | What it supports |
| --- | --- |
| `docs/M1-GEMINI-PROMPT-PACK.md:571` (Micro-level/Feature) and `:573` (Communication Design) | The M1 baseline: exactly what M1's two documentation criteria covered. This is the "expand on" anchor |
| `docs/M1-INDIVIDUAL-GDD-AGENT-PROMPT.md` ("OrderManager has no real pickup or drop-off points…", "338 rows") | M1-era state, for the delta table. Both files are **git-ignored working material** — cite by path, do not assume a marker can open them |
| `Assets/Project/Scripts/Vehicle/SurfaceType.cs:10-21`, `:113-131`, `:236-258`, `:272-314` | SurfaceKind enum, the untagged-default path, Concrete, `ApplyWeather` |
| `Assets/Project/Scripts/Vehicle/WeatherState.cs:34` | One global weather enum, `Dry` when absent |
| `Assets/Project/Scripts/Vehicle/Editor/SurfaceTagger.cs` (whole file, esp. the rule table and the VERIFY block) | The tagging tool, its rules, and its self-check |
| `Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs:17-101` (reparenting, mass-as-feel), `SeatOnRoad`, `RobustLocalBounds` (2nd–98th percentile, +4 %) | The prop baker as a feature, with its reasoning in its own header |
| `Assets/Project/Scripts/Vehicle/KnockableProp.cs` | What the marker component stores and why it is in the runtime assembly |
| `Assets/Project/Scripts/Vehicle/BoostGauge.cs:85-128`, `:144` | Communication design: the four gauge strings, the sorting order |
| `Assets/Project/Scripts/Vehicle/ChaseCamera.cs` `ComputeHeading`, `ComputeDesiredRotation`, `ReadSwivelInput` | Camera as a communication channel, and its reverse gap |
| `Assets/Project/Scripts/Vehicle/PlaytestLogger.cs` (`Header`, `FixedUpdate`, `WriteRow`, `ShiftLengthSeconds`) | Telemetry as a designed instrument, and its one corrupt field |
| `Assets/Project/Scripts/Vehicle/VehicleMeasurement.cs` (F1–F6, `turnTestSpeed`, `brakeTestSpeedFraction`) | The measurement harness |
| `Assets/Project/Scripts/Orders/ShiftResults.cs` (lines cited in `notes-orders.md` §4.4) | End-of-shift results component: four lines in `Possible game scene`, five elsewhere |
| `docs/playtesting/README.md`, `QUESTIONS.md` | The playtest instrument as communication design (two column kinds, the five-tester bar) |
| `.gitattributes:206-219`, `.gitignore` local-additions block, `.mailmap` | Shared-build integration as a documented feature |

### Criterion 3 — Reflection (5) — all of this goes to **File B**, not File A

| Evidence | What it supports |
| --- | --- |
| Commit chain `a4b6649 → e1f1708 → 7f357c7 → 752943e → 9e562f7 → d80d61e → 457d1f3 → c03c220` (all 2026-10-13) | The five prop attempts, in order, each with its own stated cause |
| Commit body of `1efc886` ("reported a clean bake while nothing in the scene had moved on four earlier attempts") | The "do not trust a tool's success message" lesson, in the author's own commit record |
| Commit body of `c03c220` (the three hand-corrected props, the 0.483 m half-extent, "Not verified: whether the car now actually knocks props over") | Honest scoping of what was and was not proven |
| `notes-map.md` §4.1, §4.2, §4.6 | The surface-tagging error, measured |
| `turning-geometry-2026-10-14/report.md` §3.1 ("22.1 m is correct and is being read wrongly") | The misread turning circle |
| `git show 03e0752` (merge, 2026-09-29, Kyuri) + `notes-friction.md` §1.1–§1.2 | The merge that de-indented `whyThisSetup` and stranded `orderTimeLimit: 50` |
| `.gitignore` local-additions comments (`Assets/_Project/`, `Game Build/`, `/.worktrees/`) | The 304 MB FBX / 96.95 GB `.git/lfs/tmp` incident and its two recurrences |
| `docs/playtesting/README.md` "Why `session_type` exists"; `playtest_sessions.csv` column 2 | The fabricated-data refusal and the structural fix |
| `M2-PLAYTEST-WRITEUP.md` §4.1, §4.6, §4.7, §5 | The hypothesis evidence and its limits |
| `git shortlog -sn --all` → Ako 65, Zubuhle Nel 57, Kyuri Reddy 41; `.mailmap` | Contribution record, and a stale in-file figure to correct |

### Criterion 4 — Contribution and expression of intent (10)

| Evidence | What it supports |
| --- | --- |
| `README.md` "System Ownership" / "Branch Ownership" tables | Ownership, as committed and test-enforced (`docs/tooling/repo-checks/test_documents.py::test_readme_ownership_tables_contract`) |
| `git log --follow` on `PlaytestLogger.cs` (`ca9a463`), `BoostGauge.cs` (`a3a1351`, `e0bcf89`), `ChaseCamera.cs` (`311cdc8`), `ShiftResults.cs` (`7788db4`, later `7022448` by Kyuri), `SurfaceType.cs` (`3c52f6b`), the editor tools (`457d1f3`) | Per-file authorship for each claimed system |
| `M2-PLAYTEST-WRITEUP.md` §4.1 + `vehicle_measurements.csv` Boost rows | The hypothesis argument: the reward mechanism is present, correct and inert |
| `turning-geometry-2026-10-14/report.md` §3.4 and §3.5 | Two mechanisms with non-obvious causes, ready for a live question |
| `docs/GIT-RULES.md` rules 1–11 | Merge discipline Ako owns and is answerable for |
| `notes-vehicle.md` §8 (11 contradictions), `notes-friction.md` Contradictions 1–8 | Preparedness: the known-wrong list, already written down |

---

## 2. File A — section-by-section specification

Target length 2,800–4,000 words of prose excluding tables and fenced blocks. Plain academic register,
UK English, no superlatives.

**Metadata block** (not a section, 5 lines): module DIGA3002A · Group 15 · Author Ako Baloyi ·
Area: vehicle physics and tuning, chase camera, telemetry, surface traction and weather, knockable
props, boost/jump/flip, boost gauge, end-of-shift results, shared-build integration · Tree described:
`feature/vehicle-camera` @ `618f2be`, with `origin/main` named wherever the two differ.

### §0 Executive summary (150–250 words, three bold labels, in this order)

- **What changed** since Milestone 1, in Ako's area only. Source the delta from the commit record
  after the 23 September M1 deadline: chase-camera wall avoidance (`311cdc8`, 29 Sep), end-of-shift
  results (`7788db4`, 29 Sep), earned boost plus the gauge (`a3a1351`, 4 Oct; earn-test fix
  `e0bcf89`, 7 Oct), `PlaytestLogger` and the session sheet (`ca9a463`, 12 Oct), knockable props and
  the baker (five commits, 13 Oct), surface tagging plus `WeatherState` and `ApplyWeather`
  (`457d1f3`/`c03c220`, 13 Oct), and the ignore-rule work that stopped the LFS disk incident
  recurring (`618f2be`, 14 Oct).
- **What the player can do now** that they could not before — phrased as capability, not components:
  slide and jump to bank boost and spend it for a 40.1 m/s push; drive a city whose surfaces can
  differ under the wheels; knock 33 street props out of the way in `Hard`; take a corner without the
  camera clipping into a building; finish a shift and read what the shift was worth. State in this
  section, not lower down, that the boost economy **has never been exercised in logged play** —
  `boost_activations`, `boost_seconds`, `boost_earned` and `handbrake_seconds` are 0 in all seven
  rows of `docs/playtesting/playtest_sessions.csv`.
- **What is still broken.** Name all five plainly, no softening: (a) the Tarmac tag sits on a
  `MeshCollider` with `m_Mesh: {fileID: 0}`, so the drivable area resolves to the untagged default
  rather than to a tagged surface; (b) the chase camera has no reverse case — it aims 28 m in front
  of the car while reversing; (c) `PlaytestLogger` writes `shift_seconds` as 0 on every observed exit
  path, and the two S01 rows also carry `play_seconds` 0.0; (d) `Easy.unity` is in no
  `EditorBuildSettings.asset` in either tree; (e) there is no audio anywhere in the project.

This section must stand alone for a marker who reads nothing else.

### §0.1 Rubric map

Exactly **one** table, four rows, three columns: Criterion (with points) · Where it is answered ·
Primary evidence.

| Criterion | Where it is answered | Primary evidence |
| --- | --- | --- |
| 1. Macro-level design documentation (15) | §1 Macro-level design: the shift, the loop, and what the vehicle work changed | `notes-vehicle.md` §1–§5; `turning-geometry` report §3–§4; `M2-PLAYTEST-WRITEUP.md` §4.1/§4.7 |
| 2. Updated feature and communication design documentation (10) | §2 Updated feature and communication design documentation | `M1-GEMINI-PROMPT-PACK.md:571,573` as the M1 baseline; `SurfaceType.cs`, `SurfaceTagger.cs`, `KnockablePropBaker.cs`, `BoostGauge.cs`, `PlaytestLogger.cs` |
| 3. Reflection documentation (5) | `docs/M2-REFLECTION-SCAFFOLD.md` — written by the author, not here; §3 is a one-paragraph pointer | Commit chain of 13 Oct 2026; `.gitignore` incident record; `playtest_sessions.csv` `session_type` |
| 4. Contribution and expression of intent (10) | §4 Contribution, integration and intent | `git shortlog -sn --all`; per-file `git log --follow`; `README.md` ownership tables; `GIT-RULES.md` |

### §0.2 Scope: the systems this document claims

Short — 120 words maximum. Name Ako's systems exactly: vehicle physics and tuning; the chase camera;
telemetry (`PlaytestLogger` runtime logger, `VehicleMeasurement` in-editor harness); surface traction
and weather; knockable street props and their editor baker; boost, jump and flip; the boost gauge;
the end-of-shift results component; shared-build integration (git rules, Git LFS, merge discipline,
the Python verification tooling). Then name what is **not** claimed, in one sentence each: orders,
shift timers, cargo, the order HUD and ticket strip, the results *panel*, the difficulty scenes and
the main menu are Kyuri Reddy's; the greybox level and UI art are Zubuhle Nel's. Add the one honest
caveat: `ShiftResults.cs` was authored by Ako (`7788db4`) and later extended by Kyuri (`7022448`,
level progression), so that file is shared.

### §1 Macro-level design: the shift, the loop, and what the vehicle work changed

Cover, in this order:

1. **The unit of play is the 300-second shift** (`ShiftTimer.shiftDurationSeconds: 300`, serialized
   300 in all four gameplay scenes). Orders arrive on a 12 s cadence up to three live, each with a
   45 s clock that starts at spawn and not at pickup — so the drive to the pickup is inside the
   budget. State the 45 s as the live value and say why the asset's 50 never reaches the runtime
   (`notes-friction.md` §1.1–§1.2).
2. **Figure 1 — core gameplay loop, with Ako's contributions marked.** Mermaid `flowchart`. Nodes:
   shift starts → order spawns → drive to pickup → enter 6 m pickup zone → carry → drive to drop-off
   → enter 6 m drop-off zone → value awarded → (parallel) order expires → late. Overlay the
   Ako-owned arcs as distinctly classed nodes: *steer/throttle/brake under a grip budget*, *handbrake
   slide → boost earned*, *air time → boost earned*, *boost spent → 40 m/s*, *crash → flip/auto-right
   → back to driving*, *camera keeps the road visible*, *every loop writes one telemetry row*,
   *shift end → results*. Use `classDef ako` and apply it; quote every label so parentheses and
   commas cannot break the parse.
3. **What the vehicle work changed about the loop, across the whole playtime.** Three claims, each
   with its number: (a) the loop's pace is set by a 20 m/s working average that the order budget is
   costed on, while the car's measured top speed is 32 m/s and its cornering is grip-limited only
   above 29.1 m/s — so the limiter across a shift is corner speed and arrival, not top speed;
   (b) recovery is designed as a player action, not a rescue — flip measured at 4.4439 m over
   0.4583 s against 4.6895 m of simply driving, i.e. `Flip is not a shortcut = 1 (PASS)`; wall
   recovery total 1.7583 s from 19.9 m/s; (c) progression inside a shift is skill, not unlocks:
   delivery value is `(10 + metres × 0.1) × condition` and nothing is spent, so the only thing that
   compounds over five minutes is the player's knowledge of the city and the car.
4. **Figure 2 — system-integration view.** Mermaid `flowchart LR` with three subgraphs: *Vehicle,
   camera, telemetry (Ako)*, *Orders, cargo, shift (Kyuri)*, *Level and UI art (Zubuhle)*. Show the
   real dependency directions read from code, not an idealised bus: `ZoneDetection` lives on the Car
   but belongs to the order system and reads the car's transform; `PlaytestLogger` subscribes to the
   order manager's events and reads the vehicle's public state and changes nothing; `CargoCondition`
   reads `VehicleTuning.brakeDeceleration`; `ShiftResults` listens to `OrderResolved`; `BoostGauge`
   reads the controller. Mark the boundary line explicitly — the vehicle never calls into orders.
5. **Micro-to-macro integration, briefly** (the rubric asks for "briefly"): one paragraph showing one
   micro decision propagating to the macro experience. Use the friction circle: coasting already
   spends 5 of a 16 m/s² longitudinal reference, so brake input at or above half travel strips the
   rear axle to 20 % of its cornering share, effective lateral grip falls 12.78 → 8.63 m/s², the
   achievable corner widens 11 % at 10 m/s and 32 % at 20 m/s, and because the loss is rear-only it
   swings the tail rather than pushing the nose wide. Then the macro consequence: braking into a
   drop-off is the manoeuvre most likely to cost a delivery, which is a shift-level outcome produced
   by a single tuning field. Label the mechanism `READ`/`DERIVED` and state plainly that whether
   players hit it in play is `UNKNOWN` — no telemetry distinguishes a braking slide.
6. **Screenshot placeholders** (clearly marked, not claims): boost gauge in its four states; the
   chase camera at a building corner with wall avoidance active; the results screen at shift end.

### §2 Updated feature and communication design documentation

1. **M1 → M2 delta table** — the expansion the criterion asks for. Rows, each with both sources:

   | Area | At M1 (23 Sep 2026) | Now | Source pair |
   | --- | --- | --- | --- |
   | Measurement sheet | 338 rows | 640 data rows (641 lines incl. header), all `Balanced` | `M1-GEMINI-PROMPT-PACK.md:571` / line count of `VehicleLab_Measurements/vehicle_measurements.csv` |
   | Delivery points | none assigned; `OrderManager` fell back to a six-pair ring | 12 hand-placed anchors wired in all four gameplay scenes | `M1-INDIVIDUAL-GDD-AGENT-PROMPT.md` / `Possible game scene.unity:1596-1609`, `notes-friction.md` §2.2 |
   | Boost | in the tuning asset, no earn path shown to the player | earned only from drift (20/s) and air (10/s), `boostRechargeRate` 0, gauge states authored | `a3a1351`, `e0bcf89` / `notes-vehicle.md` §5 |
   | Camera | cast no rays, could clip into geometry | wall avoidance with `cameraCollisionRadius` 0.4 and `cameraCollisionRecoverTime` 0.35 | `311cdc8` / `Tuning_Balanced.asset` |
   | Surfaces | enum and profiles existed, nothing tagged | 84 tags in `Hard.unity`; `WeatherState` + `ApplyWeather` added | `3c52f6b` / `457d1f3`, `c03c220`, `notes-map.md` §4.6 |
   | Props | none | 33 knockables in `Hard` only, four families, editor baker + undo | `a4b6649`…`c03c220` / `notes-map.md` §5.1 |
   | Telemetry of play | none | one row per shift to `PlaytestLogs/shift_log.csv`, 11 measured columns | `ca9a463` / `docs/playtesting/README.md` |
   | Editor tooling | 4 team-authored editor tools | same 4, two of them new this milestone | `Assets/Project/Scripts/Vehicle/Editor/` |

2. **Feature documentation proper**, one subsection each, technical angle then design angle:
   earned boost; surface traction and weather; knockable props and the baker; the chase camera;
   telemetry (both instruments); the end-of-shift results component. For each, state the decision and
   its reason in one or two sentences — the reasons are the marks here, not the field lists.
   Mandatory reasons to carry (each already written down in code or commit, cite it):
   boost earned rather than regenerating or a pickup; mass as a feel number after the cube-law model
   produced a 4-tonne table; `SurfaceKind.Concrete` **appended** so 640 serialized measurement rows
   keep their meaning; weather as one global enum applied after the four-wheel blend rather than a
   per-surface wetness flag across ~600 objects; collider boxes fitted from the 2nd–98th vertex
   percentile +4 % after `mesh.bounds` and `renderer.bounds` were rejected; the 9 m picnic table ruled
   a warning rather than an error because the tables really are 9 m; props reparented to the scene
   root with the original parent and local scale recorded so the bake is reversible.
3. **Communication design.** What the car tells the player without text, and what text it does use:
   the gauge's four strings and its `sortingOrder = 10`; skid marks and surface-aware tyre particles
   driven from the blended profile; wheel and suspension visuals driven from real steering curvature;
   camera look-ahead (28 m) checked against measured stopping distance (18.09 m from 31.37 m/s) and
   the camera-road-ahead fairness reading (42.7612 m visible, −8.579 m behind, both PASS); the
   results component's four lines in `Possible game scene` and five where a `PlayerProgression`
   exists. Then the honest counter-side: there is **no audio at all** — no audio file under `Assets`,
   no `AudioSource` anywhere, an `AudioListener` on every gameplay camera with nothing to hear — so
   impacts and speed have no sound channel, and the `IsDrifting` and `KnockableProp` hooks that were
   designed for it are unused.
4. **Figure 3 — feedback-channel map.** Mermaid `flowchart LR`: car state on the left
   (`Speed`, `SlipAngle`, `IsDrifting`, `WheelsOnGround`, `Boost`, blended `Surface`, impact) → channel
   in the middle (boost gauge, skid marks, tyre particles, wheel/suspension visuals, camera distance
   and look-ahead, results lines, telemetry CSV) → who receives it on the right (player / designer).
   Mark the missing channel (audio) as a dashed node so the gap is visible rather than argued.
5. **The playtest instrument as communication design** — two column kinds kept apart because they
   disagree and the disagreement is the finding; `session_type` in column two so machine, AI and human
   rows cannot be confused by a row count; the five-tester bar stated as unmet.
6. **Screenshot placeholders**: the 84 surface tags selected in the Hard hierarchy; a knocked-over
   prop mid-tumble; the `VehicleMeasurement` F6 console output.

### §3 Reflection documentation (5 marks) — pointer only

One short paragraph, no reflection prose. State: the reflection is the assessed personal voice and is
written by the author; the working material, with dates, commit SHAs, file paths and figures, is in
`docs/M2-REFLECTION-SCAFFOLD.md`. Name the five failure threads the scaffold carries so a marker can
see the reflection is evidenced rather than vague. Do not summarise what the reflection will conclude.

### §4 Contribution, integration and intent

1. **Ownership, with per-file evidence.** A table: system · the file(s) · the commit that introduced
   it · how it is verified. Use the real SHAs from §1 of this plan. Include the `README.md` ownership
   tables and note that `test_documents.py::test_readme_ownership_tables_contract` asserts them, so
   the ownership claim is machine-checked rather than asserted.
2. **Contribution figures, honestly scoped.** `git shortlog -sn --all` → Ako Baloyi 65, Zubuhle Nel
   57, Kyuri Reddy 41. Say the scope (`--all`, every ref) and say that `.mailmap`'s own comment
   quotes an older, narrower split (37/21/7) that no longer matches; the `.mailmap` exists so three
   people do not report as four, and it was chosen over rewriting history because a rewrite changes
   every hash on every branch.
3. **Integration into the whole.** The boundary from Figure 2, plus the two negotiated decisions:
   the penalty/reward split with the order-system owner (Kyuri owns hard-braking and collision
   penalties through `CargoCondition`; Ako owns the sliding and air-time reward through boost; a
   single shared "driving quality" number was rejected because two people would own one number), and
   the constraint accepted from the level artist (prop and building sizes are hand-placed and were not
   touched, so mass carried the feel instead of scale).
4. **The hypothesis — the heart of this criterion.** Structure it as three findings:
   - *The reward half has never fired.* Across all seven logged rows `boost_activations`,
     `boost_seconds`, `boost_earned` and `handbrake_seconds` are 0. The code is proven: a forced tank
     measures 3.0083 s and a 40.1278 m/s peak, and a 2.5 s handbrake slide at 10 m/s counted as
     drifting for 1.875 s and paid 37.5 units — exactly 1.875 × 20. So the mechanism intended to
     carry "rewards good driving" is present, correct and inert in play. Say the chain: no handbrake →
     no drift → no earnings → nothing to spend. Say which two readings the telemetry cannot separate
     (undiscoverable vs too stingy) and which two unanswered questions were written to separate them
     (`q_used_boost_when`, `q_what_made_you_drive_well`).
   - *The predictability half has one data point, positive.* In session S01 the participant rated the
     controls easy and could identify their own mistakes when losing control — the substance of
     `q_knew_why_lost_control`. Not captured as a score; one participant is one participant.
   - *Three independent sessions converge on navigation, not driving.* Two of three family testers
     could not tell where to go while none struggled with tickets, timers or values; S01 asked for a
     minimap, a directional indicator and a post-pickup route line; wall hits across the seven logged
     runs are 7, 1, 0, 0, 0, 1, 2. Players are not crashing, they are not arriving — which reframes a
     44–83 % late rate from a difficulty problem to a navigation problem.
5. **Preparedness for the marking session.** A short list of questions the author should expect, with
   where the answer lives — including two mechanisms with non-obvious causes (the rear-only friction
   circle; the camera's missing reverse case and its degenerate `Vector3.Slerp` on antiparallel
   vectors) and at least two questions about teammates' systems (the 45 s limit and the broken YAML;
   why `ZoneDetection` lives on the Car). Keep it to a dozen lines; it is preparation, not a script.

### §5 Verification index

Three parts, in this order.

1. **Label legend** — the five labels and what each must carry, matching the convention used inline
   throughout the document.
2. **Claim index** — a navigable table so a marker can pick any figure and find its source without
   reading the document: the figure, the section it appears in, its label, its source. Cover at
   minimum: 300 s shift; 45 s order limit; 12 s spawn / 3 active; 6 m zones; 900 kg; 32 m/s;
   40.1278 m/s; 3.0083 s; 20 and 10 units/s; 13 m/s² grip; 0.58 balance; 18.0948 m from 31.3653 m/s;
   22.0967 m first-lap sweep vs 20.3 m steady state vs 5.71 m below 5 m/s; 29.1 m/s crossover;
   84 tags (2/22/60); 7 of 1295 null-mesh MeshColliders; 33 props (12/9/9/3, cones 0); 640
   measurement rows; 7 sheet rows of which 2 `human`; wall hits 7/1/0/0/0/1/2.
3. **Commands run, with results** — verbatim, from the author step's own run:
   - `python docs/tooling/compile_check.py` → `RUNTIME 342 references, 80 scripts (46 excluding
     imported TextMesh Pro samples) / errors 0, warnings 0`; `EDITOR 360 references (18 editor-only),
     5 scripts / errors 0, warnings 0`; `Compiles clean.` (exit 0). Add the one-line caveat that the
     5 editor scripts include `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`, a Unity template
     leftover, so only four are team-authored.
   - `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → `34 passed`.
   - `git status --porcelain`, pasted verbatim, with the sentence that nothing under `Assets/` is
     modified and that `ProjectSettings/ProjectSettings.asset` was **already** modified before this
     document was written (it is the author's open editor session, not this work).

---

## 3. File B — scaffold specification (`docs/M2-REFLECTION-SCAFFOLD.md`)

**Opening line, first sentence of the file:** a plain statement that this is working material for Ako
to write the reflection from, and is not text to submit.

**Then a four-line "how to use this" block:** every bullet below is a fact about an artefact with its
source; the questions are for the author to answer in their own words; nothing here is written in the
author's voice, and nothing here should be pasted into the submission unchanged.

Sections, matching the rubric's own sub-prompts:

### B1 Personal systems — how the work was actually organised

Facts to carry: branch ownership and the one-branch rule (`GIT-RULES.md` rule 2, `README.md` branch
table); the labelled-evidence convention used across the investigation notes; the habit visible in
commit bodies of recording what was verified *and* what was not (quote `c03c220`'s "Not verified:
whether the car now actually knocks props over"); the four read-only Python tool families under
`docs/tooling/`; the 34-test harness and the `-B -p no:cacheprovider` run rule that keeps caches off
disk; the measurement harness as a standing instrument (640 rows, newest session 2026-10-07 13:56).
Open questions: which of these habits existed before this project and which were built during it?
Which one cost time without paying for itself?

### B2 Successes

Facts: earned boost measured end to end (3.0083 s tank, 40.1278 m/s peak, 37.5 units from a 1.875 s
drift); the camera wall-avoidance work and the wall-hit sequence 7 → 1 → 0 across the three runs with
substantial driving — **with** the caveat, stated here, that the three runs had different operators,
different durations and no controlled variant, so this is suggestive and not a result; the flip
proven not to be a shortcut by measurement; 33 props baked and re-seated; 84 surfaces tagged; the
serialization-safe `Concrete` append; the `.gitignore` reasoning that ended a recurring disk-filling
failure; the playtest instrument and write-up. Open questions: which of these would the author
present first in the marking session, and why that one?

### B3 Failures and lessons

Five threads, each as facts with sources, no interpretation:

1. **The props took five attempts.** The commit chain in order with each stated cause: bake not saved
   so entering Play restored the saved scene (`e1f1708`); the city's transform chain (`7f357c7`);
   wrong bounds source (`752943e`); the 9 m table ruled real rather than wrong (`9e562f7`); mass
   derived from size producing a 4-tonne table (`d80d61e`); then seating on the road (`457d1f3`,
   `c03c220`). Record the lesson the author already wrote in `1efc886`: the tool reported a clean bake
   while nothing had moved on four earlier attempts. Record the three hand-corrections in `c03c220`
   with their numbers (trashcontainer01.006 y 6.763 → 0.525; RoadBlocker04.001/.003 y −0.090 → 0.400,
   low by exactly the 0.483 m local-Y half-extent). Record that the rotation-aware fix
   (`395497b`, `1efc886`) exists **only** on `fix/seat-on-road-rotation` and is not on
   `feature/vehicle-camera` or `origin/main`. Open questions: what did the author expect each bake to
   do, and at which attempt did the diagnosis change from "the tool is wrong" to "the scene is wrong"?
2. **The surface tagging is wrong on the roads, and it is the author's error.** Facts: `Roads_Parent`
   carries `SurfaceType kind: 0` on a `MeshCollider` with `m_Mesh: {fileID: 0}` — one of 7 null-mesh
   MeshColliders among Hard's 1295; 633 road tiles under `Roads_Parent` own no collider at all; the
   second Tarmac tag is on a palette `road_base` object 48 m below the map; the `Floor` BoxCollider
   carries no tag; the 60 Concrete and 22 Grass tags are on objects that own real colliders and do
   resolve. The premise is recorded in the author's own commit message (`c03c220`: "the merged road
   collider the car actually drives on") and in `SurfaceTagger.cs`'s header. Open questions: the
   tagger has a verify pass that casts the car's own ray at every tagged object and warns when a tag
   the ray cannot see is found — what did that warning say on the run, and what happened to it?
3. **"22.1 m turning circle" was read wrongly for weeks.** It is the widest sweep of the first lap of
   a U-turn at 9.8399 m/s, inflated by the entry spiral; steady state is 20.3 m, and below 5 m/s the
   curvature cap binds and the circle is 5.71 m. Arrivals are low-speed manoeuvres. Open questions:
   what decisions were deferred or avoided while the 22.1 m figure was the working number?
4. **A merge broke a tuning asset.** `03e0752` (2026-09-29, Kyuri) de-indented `whyThisSetup` to
   column 1, which closes the mapping and strands everything below it — including
   `orderTimeLimit: 50`. The build runs the C# default of 45 s; the longest costed route needs 48 s.
   Open questions: what would have caught this, and is that check worth its cost?
5. **An untracked 304 MB FBX nearly destroyed the working environment.**
   `Assets/_Project/Models/Group18_Greybox.fbx`, 23 September 2026: the editor's source-control
   integration polls `git status`, then pipes untracked files through `git hash-object` with the LFS
   filters live; `.git/lfs/tmp` reached 96.95 GB across 831 files in under an hour and filled a 464 GB
   disk. Same shape twice more: a 486 MB player build across 197 files, and a git worktree. All three
   now ignored with the reasoning recorded in `.gitignore`. Open questions: what made the cause
   findable, and what would the author now check first on a new machine?
6. **Fabricated playtest data was proposed and refused.** Five machine and AI-QA telemetry rows were
   relabelled as human participants with invented tester identities, experience levels and verbatim
   quotes. It was rejected. The structural fix: a `session_type` column in position two so machine, AI
   and human rows cannot be confused by a row count; the underlying observations were real and already
   evidenced in the QA documents. Open questions: for the author only.

### B4 The hypothesis

Carry the three findings from File A §4.4 as bare facts with their sources, plus the instrument: the
decomposition in `docs/playtesting/QUESTIONS.md` under "What each question is for" maps every question
to one word of the design question. Carry the limits plainly: zero scored 1-to-5 answers exist from
anyone; the protocol minimum of five testers with at least two inexperienced, on one build and one
variant, is unmet; `tester_racing_experience` was never asked; `variant` is effectively `baseline` on
every row, so `boost-regen` — the one comparison that would directly address the inert-boost finding —
is untested. Open questions: what did the author expect the first unguided player to do with the
handbrake? Does the inert reward half change the hypothesis, the mechanic, or the way it is taught?

### B5 Group workflow, collaboration, communication and role fulfilment

Facts: `GIT-RULES.md`'s eleven rules, with Ako as merge owner (rule 6), tag owner (rule 7) and
pre-merge checker (rule 9); branch ownership per `README.md`; `git shortlog -sn --all` → 65 / 57 / 41,
and `.mailmap`'s older 37 / 21 / 7 figure; `.gitattributes` `merge=union` on
`VehicleLab_Measurements/*.csv` and `docs/playtesting/*.csv` so three people can append rows without
conflict (`git check-attr merge` confirms both); Unity Smart Merge registered for `.unity`/`.prefab`
(rule 11); the penalty/reward split negotiated with Kyuri; prop and building sizes left untouched
because the level artist hand-placed them; props and surfaces baked into the scene that was then
renamed `Hard.unity` on `origin/main`; the 14 October session where one teammate was absent (sick) and
another stepped out, recorded in the S01 `observer_notes`. Also carry the two unfinished seams:
`git tag -l` has no checkpoint tags despite rule 7, and `docs/BUILD-LOG.md` holds no real rows.
Open questions: which rule was hardest to hold to, and what did the author do when a teammate's merge
broke something they owned?

### B6 Evidence appendix

A dated timeline, one line per entry, SHA + date + subject, from 2026-09-10 (`3c52f6b`, surfaces) to
2026-10-14 (`618f2be`, ignore rules), plus the non-commit events (the 23 Sep LFS incident; the 14 Oct
S01 session; the 14 Oct fabricated-data refusal). This is what makes the reflection checkable.

### B7 Before you paste anything into the submission

A short checklist: every figure re-checked against the cited file; no sentence kept that the author
would not say out loud in the marking session; no claim that the surface model is working on roads;
no claim that earned boost rewards good driving in practice; neither the family session nor S01
described as a controlled playtest.

---

## 4. Stale, contradictory and corrected sources — flag list for the author step

| Source | Status | What the author must do |
| --- | --- | --- |
| Working tree vs `origin/main` | The Easy/Medium/Hard restructure is on `origin/main` only; local is `feature/vehicle-camera` @ `618f2be`. `Possible game scene.unity` differs: 136 GameObjects locally vs 40 on `origin/main` | Read Easy/Medium/Hard from `<ref>/` or `git show origin/main:…`. Name the tree whenever a scene claim depends on it |
| `docs/QA-PLAYTEST-REPORT-2026-10-14.md` | **Stale**: predates the restructure; its scene-specific findings describe the pre-restructure build | Cite only for functional defects confirmed live (QA-003, QA-006, QA-007, QA-008, QA-009) and say the scene-specific parts are stale |
| `docs/playtesting/QA-AI-20261014-01.md` | **Stale** for the same reason; also AI-operated, not a playtest | Cite as functional QA evidence only, never as player-experience evidence |
| `docs/playtesting/README.md` | Says "As of 14 October 2026 the sheet holds **zero** `human` rows"; the sheet now holds 2 | Quote the sheet, not the README, for the human-row count; note the README line is behind |
| `.mailmap` comment "Ako 37, Zubuhle 21, Kyuri 7" | Older and narrower than `git shortlog -sn --all` (65/57/41) | Quote the command and its scope; mention the in-file figure as superseded |
| `notes-friction.md` §3.1 and `turning-geometry` §4.3 vs `notes-map.md` §4.1–§4.2 | §3.1 describes `Roads_Parent`'s MeshCollider without the null mesh; §4.3 says Hard has no `Roads_Parent` at all. Both are wrong on this point | Follow `notes-map.md` §4.1/§4.2. Settled independently in this pass: `Roads_Parent` exists, holds `SurfaceType kind: 0` and a `MeshCollider` with `m_Mesh: {fileID: 0}`, and 633 road tiles are parented to it with no collider of their own |
| `KnockableProp.cs:26-29` / commit `7f357c7` ("PhysX cannot run a Rigidbody under negative non-uniform parent scale") | **Mechanism overstated**: `notes-friction.md` §3.1 measured 1311 of 1312 colliders under `City` resolving to a *positive* world scale — the negatives cancel | Write the reparenting as correct for stated and measured reasons (hand-tunability at +255 world scale, a reversible undo path, BoxCollider refusing negative scale) and say the negative-scale mechanism recorded in the tooltip is not supported by the later measurement |
| "5 kg cones" among the prop masses | The family is declared in code with **zero instances**; every `cone_type` is in the below-map palette | Write the four families that exist with their counts (12 toilets @180, 9 tables @120, 9 bins @60, 3 blockers @25) and the cone family as declared-but-unused |
| "bins −1.15 m" sink figure | **Unverified.** The table (−0.59) and toilet (−1.61) figures are in `docs/tooling/seat-fix-plan.md` and `seat_math_check.py:281-289` on branch `fix/seat-on-road-rotation`; no source for −1.15 was found | Drop the bin figure, or cite one if found. Do not write it unsourced |
| "five editor tools" (M1 prose) | `compile_check.py` reports 5 editor scripts, but one is `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`, a Unity template leftover | Write four team-authored editor tools and name them |
| `Tuning_Balanced.asset` `whyThisSetup` | Three internal errors: stopping distance 23.3 m (real 18.09 m measured, because the car also coasts), "rear gets 84 % of the front's grip" (72.4 % by the arithmetic), and grip quoted as 12 in two comment blocks where the asset serializes 13 | If any of these is quoted, quote the measurement and name the asset prose as wrong |
| `OrderTuning_Balanced.asset:33` `orderTimeLimit: 50` | Unreachable — the YAML above it is broken | Write 45 s as the live value, with the mechanism |
| `vehicle_measurements.csv` `Profile` rows (F7) | Not measurements — F7 echoes tuning fields into the CSV | Never call `Profile / Top speed = 32` a measured figure; the nearest driven evidence is 31.3653 m/s in 3.75 s |
| `docs/M1-GEMINI-PROMPT-PACK.md`, `docs/M1-INDIVIDUAL-GDD-AGENT-PROMPT.md` | **Git-ignored** working material; the submitted M1 Individual GDD itself is not in the repository | Use them for the M1 baseline and say what they are. Assume the author holds the submitted M1 PDF; do not quote figures from a document this pass has not read |
| `docs/DESIGN-OVERVIEW.md` | Did not exist at planning time (checked) | If it has appeared, read it and prefer it over the notes files on any disagreement. Do not edit it, and do not block on it |

---

## 5. Load-bearing claims and the exact source to spot-check

Spot-check each before writing it. Do not copy a figure from this plan without opening its source.

| Claim | Spot-check against |
| --- | --- |
| Boost earned, never regenerated: 20 /s drift, 10 /s air, 0 on a flip, `boostRechargeRate` 0 | `Tuning_Balanced.asset`; `VehicleController.cs:910-932`, recharge gate `:885`; `notes-vehicle.md` §5 |
| Tank 3.0083 s, peak 40.1278 m/s, 37.5 units from a 1.875 s drift | `VehicleLab_Measurements/vehicle_measurements.csv`, `Boost` rows, session `2026-10-07 13:56:45` |
| Earn condition is `IsDrifting` (grounded, ≥ 4 m/s, slip ≥ 12°), not the handbrake | `VehicleController.cs:677-683`, `:910-920`; `notes-vehicle.md` §8 item 10 (the README's wording is looser than the code) |
| Boost never used in play: `boost_activations`/`boost_seconds`/`boost_earned`/`handbrake_seconds` all 0 across 7 rows | `docs/playtesting/playtest_sessions.csv` (all rows); `M2-PLAYTEST-WRITEUP.md` §4.1 |
| `SurfaceKind` = 7 kinds, `Concrete` appended last; untagged colliders return `SurfaceProfile.Default`, which is numerically Tarmac | `SurfaceType.cs:10-21`, `:113-131` (`Default` block), `:236-258` |
| Blend over grounded wheels, then one `ApplyWeather` pass; `Dry` returns the profile unchanged | `VehicleController.cs:566-621` (weather at `:621`); `SurfaceType.cs:313-314` |
| 84 tags in `Hard`: 2 Tarmac, 22 Grass, 60 Concrete | `notes-map.md` §4.6; re-countable from `<ref>/Hard.unity` |
| The Tarmac tag is inert: `Roads_Parent`'s MeshCollider has `m_Mesh: {fileID: 0}` (7 of 1295 null-mesh); 633 tiles under it own no collider; `Floor` carries no tag | `notes-map.md` §4.1, §4.2, §4.6; `<ref>/Hard.unity` GameObject `&8744834134912007418` and `Floor` `&1551314015` |
| 33 props in `Hard` only; 12/9/9/3 by family; box from the 2nd–98th vertex percentile +4 %; inertia capped so a 9 m table turns like a 2 m one | `notes-map.md` §5.1–§5.2; `KnockablePropBaker.cs` `RobustLocalBounds` and `CompactInertia` |
| Camera wall avoidance: `cameraCollisionRadius` 0.4, `cameraCollisionRecoverTime` 0.35; previously cast no rays | `Tuning_Balanced.asset`; commit `311cdc8`; `ChaseCamera.cs` |
| No reverse case: look-at 28 m ahead, degenerate `Vector3.Slerp` on antiparallel vectors | `turning-geometry` report §3.5 and Option 1; `ChaseCamera.cs` `ComputeHeading`/`ComputeDesiredRotation` |
| Friction circle: saturates at half brake travel, rear grip 10.92 → 2.18, effective lateral 12.78 → 8.63, corner +11 % at 10 m/s and +32 % at 20 m/s | `turning-geometry` report §3.4; `notes-vehicle.md` §3 |
| Turning circle: 22.0967 m first-lap sweep at 9.8399 m/s; 20.3 m steady state; 5.71 m below 5 m/s | `vehicle_measurements.csv` `Turning circle` rows; `turning-geometry` report §3.1 |
| Stopping 18.0948 m from 31.3653 m/s, explained by brake 22 + coast 5 = 27 m/s² | `vehicle_measurements.csv` `Stopping distance`; `VehicleController.cs:771`, `:776` |
| `PlaytestLogger` writes `shift_seconds` 0 on the exit path; the S01 rows also show `play_seconds` 0.0 | `notes-friction.md` §5 (`PlaytestLogger.cs:244`, `:302`, `:134`); `playtest_sessions.csv` S01 rows |
| 45 s live order limit; the asset's 50 is stranded by broken YAML | `notes-friction.md` §1.1–§1.2; `OrderTuning.cs:31`; `OrderTuning_Balanced.asset:16`, `:33`; `git show 03e0752` |
| `Easy.unity` in no build settings; `Medium`/`Hard` only on `origin/main` | `git show origin/main:ProjectSettings/EditorBuildSettings.asset`; local `ProjectSettings/EditorBuildSettings.asset`; `notes-orders.md` §4.4 |
| No audio anywhere; `AudioListener` on every gameplay camera | `notes-friction.md` §6 |
| Integration tooling: compile check 80 runtime / 5 editor, 0 errors 0 warnings; 34 repo-checks; `merge=union` on both CSV families | run the commands in §6; `.gitattributes:206-219`; `git check-attr merge -- <csv>` |
| Contribution: 65 / 57 / 41 via `git shortlog -sn --all` | run it; `.mailmap` |
| `ShiftResults` is the results screen in every scene; `ResultsPanel` is in none | `notes-orders.md` §4.4 (GUID table) |

---

## 6. Hard constraints — the do-not-write list

The author step must not drift into any of these. Each is a claim that would be false.

1. **Do not write that surface traction works on the roads.** The drivable area resolves to the
   untagged default. Write the failure, and write the honest size of it: because
   `SurfaceProfile.Default` is numerically identical to Tarmac in dry conditions, the live consequence
   is an **inert tag**, not changed grip. The 22 Grass tags on park bases are the only surface
   variation the car could plausibly reach.
2. **Do not write that earned boost rewards good driving in practice.** It has never fired in logged
   play. Write: present, correct, inert — and say so in §0 as well as §4.
3. **Do not describe the three family testers or session S01 as a controlled playtest.** Neither
   followed `QUESTIONS.md`; no 1-to-5 score has been collected from anyone; the five-tester minimum is
   unmet and the document must say so.
4. **Do not claim what the car stands on in `Hard` as settled.** It is `UNKNOWN` — `Floor` at
   y = 0.00, road visuals at y ≈ 0.40 with no collider, and a negative-scaled mesh at y ≈ 0.398 are
   three candidates. The settling check is one downward raycast from the spawn in play mode. Say that.
5. **Do not propose or make any change to hand-placed prop or building sizes.**
6. **Do not present the 7 → 1 → 0 wall-hit trend as a result.** Different operators, different
   durations, no controlled variant.
7. **Do not quote the `Profile` CSV rows as measurements** (F7 echoes the asset).
8. **Do not claim a screenshot exists.** Placeholders only, clearly marked, naming what to capture.
9. **Do not claim checkpoint tags or build-log rows exist.** `git tag -l` is empty and
   `docs/BUILD-LOG.md` has no real rows; both are rule-7 and rule-9 gaps worth naming.
10. **Do not write a first-person sentence in File B**, and do not invent a feeling, opinion or
    intention for the author anywhere in either file.
11. **Do not touch game content.** No file under `Assets/`, `ProjectSettings/`, `Packages/` is to be
    created, edited or deleted. Do not open Unity. Do not commit, push, branch or merge. Do not edit
    `docs/DESIGN-OVERVIEW.md` if it appears.
12. **Do not change `docs/tooling/repo-checks`** — the 34-test count is cited evidence.
13. **Do not add links to `README.md`.** `test_documents.py::test_readme_document_links_resolve_and_are_tracked`
    asserts the Links section contains exactly three targets.

---

## 7. Verification commands and the pre-existing baseline

Every item below verifies with these. They are the project's real commands, discovered in
`docs/tooling/repo-checks/README.md` and the commit bodies that use them.

```powershell
python docs/tooling/compile_check.py
python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks
git status --porcelain
git status --porcelain -- Assets ProjectSettings
```

Baseline captured in this planning pass, read-only, so the author step can prove it changed nothing:

```
 M .gitignore
 M ProjectSettings/ProjectSettings.asset
 M docs/playtesting/README.md
 M docs/playtesting/playtest_sessions.csv
 M docs/tooling/scene-tools/README.md
?? .agents/
?? docs/QA-PLAYTEST-REPORT-2026-10-14.md
?? docs/playtesting/M2-PLAYTEST-WRITEUP.md
?? docs/playtesting/QA-AI-20261014-01.md
?? docs/playtesting/evidence/
?? docs/tooling/playtest_row.py
?? docs/tooling/scene-tools/component_fields.py
?? docs/tooling/scene-tools/corner_geometry.py
?? docs/tooling/scene-tools/fbx_mesh_bounds.py
?? docs/tooling/scene-tools/scene_physics.py
?? docs/tooling/scene-tools/serialization_drift.py
```

`git status --porcelain -- Assets ProjectSettings` returns exactly `M ProjectSettings/ProjectSettings.asset`
and nothing else. That modification **pre-dates this work** (the author has the editor open). After
every item below, the only permitted change to this listing is the appearance of
`?? docs/M2-INDIVIDUAL-AKO.md` and `?? docs/M2-REFLECTION-SCAFFOLD.md`. Anything else is a defect in
the item that introduced it.

Compile check and repo-checks are expected to be unchanged throughout: this is a documentation task
and must not move either result.

---

# Implementation Plan

- [ ] 1. Establish the clean baseline and spot-check the load-bearing numbers before writing any
      prose. Run the four commands in §7, record the outputs for §5 of File A, then open each source
      in the §5 spot-check table and confirm the figure. Correct this plan's figure if a source
      disagrees; the source wins.
      Files: none created or modified.
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, exit 0;
      `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → `34 passed`;
      `git status --porcelain -- Assets ProjectSettings` → only `M ProjectSettings/ProjectSettings.asset`.

- [ ] 2. Create `docs/M2-INDIVIDUAL-AKO.md` with the metadata block and the complete heading skeleton
      from §2 of this plan — §0, §0.1, §0.2, §1, §2, §3, §4, §5 — plus the §0.1 rubric map table
      filled in and the §0.2 scope statement written. Leave the criterion sections as headings only.
      Files: `docs/M2-INDIVIDUAL-AKO.md`
      Verify: `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → `34 passed`;
      `git status --porcelain` matches the §7 baseline plus `?? docs/M2-INDIVIDUAL-AKO.md` only.

- [ ] 3. Write §0 Executive summary — 150–250 words, three bold labels in the order given in §2
      (what changed / what the player can do now / what is still broken), each figure labelled and
      cited inline. The inert boost economy belongs in the second part; all five defects belong in
      the third, unsoftened. Depends on item 1's spot-checks for every number.
      Files: `docs/M2-INDIVIDUAL-AKO.md`
      Verify: `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → `34 passed`;
      word count of §0 between 150 and 250; `git status --porcelain -- Assets ProjectSettings`
      unchanged from baseline.

- [ ] 4. Write §1 Macro-level design, including Figure 1 (core loop with Ako's contributions classed
      and marked) and Figure 2 (system-integration view with three owner subgraphs and the real
      dependency directions). Both as Mermaid fenced blocks with quoted node labels. Add the three
      §1 screenshot placeholders, clearly marked as not captured.
      Files: `docs/M2-INDIVIDUAL-AKO.md`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`;
      `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → `34 passed`;
      both Mermaid blocks render in the editor preview without a parse error; no figure in §1 lacks a
      label and a citation.

- [ ] 5. Write §2 Updated feature and communication design documentation: the M1 → M2 delta table,
      the six feature subsections with their decision reasons, the communication-design subsection
      including the no-audio counter-side, Figure 3 (feedback-channel map with audio dashed), the
      playtest instrument subsection, and the three §2 screenshot placeholders. Depends on item 4 for
      the integration boundary it refers back to.
      Files: `docs/M2-INDIVIDUAL-AKO.md`
      Verify: `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → `34 passed`;
      Figure 3 renders; every delta-table row cites both an M1 source and a current source.

- [ ] 6. Write §3 (one-paragraph pointer to File B, no reflection prose) and §4 Contribution,
      integration and intent — ownership table with real SHAs, contribution figures with their
      command and scope, the integration and negotiated-decision paragraphs, the three hypothesis
      findings, and the marking-session preparation list.
      Files: `docs/M2-INDIVIDUAL-AKO.md`
      Verify: `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → `34 passed`;
      every SHA in the ownership table resolves — `git show --no-patch --pretty=format:"%h %ad %an %s" <sha>`
      for each; `git shortlog -sn --all` reproduces the quoted 65 / 57 / 41.

- [ ] 7. Write §5 Verification index: the label legend, the navigable claim index covering at minimum
      the figures listed in §2 of this plan, and the commands-run block with verbatim outputs
      including `git status --porcelain` and the note that `ProjectSettings.asset` was already
      modified. Run the commands fresh for this item rather than pasting item 1's output.
      Files: `docs/M2-INDIVIDUAL-AKO.md`
      Verify: `python docs/tooling/compile_check.py` and
      `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` both pass and their pasted
      outputs match the run; every figure named in §2 of this plan appears in the claim index with a
      label and a source.

- [ ] 8. Create `docs/M2-REFLECTION-SCAFFOLD.md` per §3 of this plan: the opening not-for-submission
      sentence, the how-to-use block, B1–B7. Facts with dates, SHAs, paths and figures; open questions
      addressed to the author; no first-person sentence anywhere.
      Files: `docs/M2-REFLECTION-SCAFFOLD.md`
      Verify: `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → `34 passed`;
      `git status --porcelain` matches the §7 baseline plus the two new `docs/` files only; every SHA
      cited in B1–B6 resolves via `git show --no-patch <sha>`.

- [ ] 9. Cross-file consistency and constraint sweep. Reconcile File A §3 against File B's actual
      section list; confirm every figure appearing in both files agrees; walk the §6 do-not-write list
      item by item against both files; confirm File B contains no first-person sentence and no
      invented feeling, opinion or intention; confirm no screenshot is claimed to exist; confirm the
      stale sources in §4 are described as stale wherever they are cited.
      Files: `docs/M2-INDIVIDUAL-AKO.md`, `docs/M2-REFLECTION-SCAFFOLD.md`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, exit 0;
      `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → `34 passed`;
      `git status --porcelain` equals the §7 baseline plus exactly
      `?? docs/M2-INDIVIDUAL-AKO.md` and `?? docs/M2-REFLECTION-SCAFFOLD.md`;
      `git status --porcelain -- Assets ProjectSettings` still returns only
      `M ProjectSettings/ProjectSettings.asset`. Record the three outputs in File A §5 if item 7's
      pasted block has drifted.

- [ ] 10. Final read-through for register and length: academic prose, UK English, no superlatives, no
      filler; File A 2,800–4,000 words of prose excluding tables and fenced blocks; §0 still 150–250
      words; §0.2 still under 120 words. Fix anything that reads as marketing. Make no change that
      introduces an unlabelled or uncited figure.
      Files: `docs/M2-INDIVIDUAL-AKO.md`, `docs/M2-REFLECTION-SCAFFOLD.md`
      Verify: `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks` → `34 passed`;
      `git status --porcelain` unchanged from item 9; word counts within the stated ranges.

**Gaps and assumptions recorded rather than stalled on.** (a) The submitted M1 Individual GDD is not
in the repository; the M1 baseline is taken from the two git-ignored prompt files, and the document
says so. (b) `docs/DESIGN-OVERVIEW.md` did not exist at planning time; if it appears, it outranks the
notes files and must not be edited. (c) The "bins −1.15 m" sink figure has no locatable source and is
dropped. (d) What the car rests on in `Hard`, whether a prop can be launched through the road, and
whether the car can come to rest inverted are all `UNKNOWN` without play mode; each is written as a
question with its settling check rather than as a finding.
