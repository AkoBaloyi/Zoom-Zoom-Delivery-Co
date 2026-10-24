# Claim audit of `docs/DESIGN-OVERVIEW.md` — fourth gate pass

A verification gate, not a re-investigation. The document's standard is its own: every number traces
to a file, every claim carries a `READ` / `MEASURED` / `DERIVED` / `INFERRED` / `UNKNOWN` label, and
anything unverifiable is `UNKNOWN` with the check that would settle it. This pass re-checked the five
findings the previous review raised, then sampled the claims the fourth revision introduced. All five
prior findings are closed, verified line by line rather than taken on the revision's word, and four
remain — all of them sourcing or disclosure form, none of them a wrong number. Nothing in this pass
changes what the document asserts about the game.

**Watch for:** the per-scene table's city-scale row quotes Easy's `8.97659` with no line, and §6 item
28 repeats the same figure with no file, in a table whose own preamble promises a cited line for
every value (**confirmed** — the value is at `<ref>/Easy.unity:15888`). §5.3's "absent from the
build's Controls screen" is the one load-bearing sentence in the document with no file behind it, and
the file exists (**confirmed** — `origin/main:Assets/Project/Scenes/MainMenu.unity:1023`, which also
shows the panel lists two more bindings than the QA premise the sentence inherits). And §7's "only
five places" `<notes>` disclosure is still one short: the MainMenu and VehicleLab columns of the
per-scene table come from `notes-vehicle.md:315` and sit outside the enumeration (**confirmed**).

**Verdict**: NEEDS_CHANGES (gate: `CHANGES_REQUESTED`)

## High-level view

The five prior findings are closed on evidence re-read for this pass, not on the revision's word: the
seven `OrderResolved` sites with the `OrderTicketStrip` freeze row behind them, twelve per-scene
camera lines, `m_GameObject` fields in place of block headers in all four scenes, and lines on the
uniqueness sweep, the brake-intent quote and the six-and-six point arrays.

What is left is one citation gap that repeats a number in two places, one factual claim about the
shipped build's UI with no source, and one disclosure sentence that over-claims its own coverage. All
three are the same shape as the gaps this document has closed four times now: the figure is right,
the trace is missing or the self-description of the trace is wider than the trace.

Two smaller slips sit inside paragraphs labelled "all `READ`": a `///` attribution that belongs to a
plain `//` banner, and a line range that stops one line short of the binding it is cited for.

All four required sections are present and intact: "How to read this" with the label table and the
four rules, the per-scene configuration table, "Unknown / needs a running game" with a per-item check
on all 28 entries, and "Contradictions found" with C1–C24. All five mandated contradictions — value
formula, 45/50/75 timer, EndShift divergence, `System.Random` determinism, camera sensitivity — name
both sources, the discriminating check and the runtime winner. No quantitative sentence reads as a
confident estimate; every hedge sampled is arithmetic on cited values or attributed to the
turning-geometry report.

<details>
<summary>Issues (4)</summary>

1. **Easy's greybox scale is uncited** — the per-scene table's "City container / uniform scale" row
   gives Hard a line (`:187643`) and leaves Medium's `1.23330` and Easy's `8.97659` without one, and
   §6 item 28 repeats the `8.97659` with no file. Add `<ref>/Easy.unity:15888` (object named at
   `:16084`) and `<ref>/Medium.unity:177909` (object named at `:165599`).
2. **The Controls-screen claim has no source** — §5.3's "the dedicated non-reversing brake … is
   absent from the build's Controls screen" carries no file. Cite
   `origin/main:Assets/Project/Scenes/MainMenu.unity:1023` and label it, and fix the inherited
   premise while there: that panel lists six bindings (W, A, S, D, `Shift = Jump`,
   `SpaceBar = Hand Brake`), not the W/A/S/D-only screen the QA brief describes, so the omissions are
   Left Ctrl, Left Alt and Q. If the claim is meant to be about the 08:31 build rather than
   `origin/main`, say which tree was read.
3. **§7's `<notes>` disclosure is one place short** — it names five figures as "the only figures in
   this document whose inputs live solely in the task notes", but the per-scene table's MainMenu and
   VehicleLab columns also do (`<notes>/notes-vehicle.md:315` carries VehicleLab's 5 `SurfaceType`
   tags). Either cite that line on those two columns and extend §7, or narrow §7's sentence to
   `<notes>`-cited figures.
4. **Two citation-form slips** — §1.1's "every one of them inside a `///` comment block" does not
   hold for the sixth hit, `VehicleTuning.cs:87`, which is a plain `//` banner; and the six-hit count
   reproduces only on a case-insensitive search, because `VehicleController.cs:79` reads
   `WHEELCOLLIDERS`. §1.5 cites the bindings to `VehicleInput.cs:218-234` while the flip's gamepad
   binding is at `:235`.

</details>

<details>
<summary>Details</summary>

## F1 — The city-scale row quotes two figures and cites one

§Per-scene opens with "Every value is `READ` from the cited line of that scene's `origin/main` blob
unless marked otherwise". The "City container / uniform scale" row gives Hard
`City`, **2.0439076** (`:187643`), then `'City '` (trailing space), 1.23330 for Medium and
`Group 15 Greybox real one 1`, 8.97659 for Easy with no line on either (**confirmed**).

Both values are right. Medium's is `m_LocalScale: {x: 1.2333, y: 1.2333, z: 1.2333}` at
`<ref>/Medium.unity:177909`, on the transform whose `m_GameObject` at `:177905` resolves to the
object named `'City '` at `:165599` — which §5.4 cites in passing for a different purpose, so a
reader can recover it from another section. Easy's is
`m_LocalScale: {x: 8.976594, y: 8.976594, z: 8.976594}` at `<ref>/Easy.unity:15888`, on the transform
whose `m_GameObject` is `2491368242438309547`, named `Group 15 Greybox real one 1` at `:16084`. That
line appears nowhere in the document, and §6 item 28 quotes the figure a second time — "the 54 kerb
meshes are the same ones the README's figures came from, at the same import scale 8.97659" — also
with no file. A number used twice with no trace either time is the one case the table's preamble
exists to prevent.

## F2 — The Controls-screen claim is the one load-bearing sentence with no file

§5.3: "the dedicated non-reversing brake — `VehicleInput` states its intent as 'Left Ctrl brakes and
never reverses. For stopping ON a drop-off point' (`READ`, `VehicleInput.cs:29`, inside the `///`
header) — **is absent from the build's Controls screen**. A tester who only knows about `S` reverses
every time they brake to a standstill."

The quote is sourced; the clause that carries the finding is not. §5's preamble excludes "the
incomplete Controls screen … as already known", which covers mentioning it, but here it is the
premise the paragraph's conclusion rests on, and it is stated as fact about the shipped build with
nothing a reader can open (**confirmed**).

The evidence exists and is one line:

```
origin/main:Assets/Project/Scenes/MainMenu.unity:1023
  m_text: "W = Acceleration/Forward\nA = Steer Left       \nS = Brake/Reverse\nD
    = Steer Right  \nShift  = Jump\nSpaceBar = Hand Brake  "
```

Two consequences for the fix. First, the claim is true: Left Ctrl is not on that panel, and nor are
Left Alt (boost), `Q` (flip) or the mouse — which is also the documentary evidence behind §1.5's
"nobody has used any of it" and §5.2's camera complaint, currently resting on the logs alone.
Second, the panel is **not** the W/A/S/D-only screen the QA brief describes: it lists six bindings,
including the handbrake and jump. The document inherits the narrower premise without saying so. If
the sentence is meant to describe the 2026-10-14 08:31 build rather than `origin/main`, that is a
different artefact — `level0` in the build data, reachable by the same unpack §7 already documents —
and the tree should be named, the way the rest of the document names it.

## F3 — §7's disclosure still claims more coverage than it has

§7: "the `<notes>` citations point at raw tool output that was **not** reproduced for this document.
There are five such places, each flagged at the point of use … Those are the only figures in this
document whose inputs live solely in the task notes."

The per-scene table's last caveat paragraph contradicts that: "the MainMenu and VehicleLab columns
come from the investigation notes and were not re-verified here". Those columns carry figures —
VehicleLab's 5 `SurfaceType` tags, its `WeatherState` 0, MainMenu's zeroes — whose only record is
`<notes>/notes-vehicle.md:315`, the per-scene surface table, with the prose restatement at `:703`
(**confirmed**). They are flagged at the point of use but carry no notes line, so they are both
outside §7's enumeration and outside the `<notes>` citation convention the enumeration describes.

This is the same defect the prior pass raised against §5.2, in the one place the fix did not reach.
Either treatment closes it: cite `<notes>/notes-vehicle.md:315` on the two columns and add them to
§7's list as a sixth place, or narrow §7's sentence so it claims only what it enumerates.

## F4 — Two slips inside paragraphs labelled "all `READ`"

§1.1: "no `WheelCollider` anywhere in the project (`MEASURED` — 6 hits for the type name across
`Assets`, every one of them inside a `///` comment block explaining why it is not used, at
`VehicleController.cs:77,79,80,82`, `SurfaceType.cs:45` and `VehicleTuning.cs:87`, `READ` at those
lines)."

The absence is right and all six lines exist, but two details do not hold (**confirmed**).
`VehicleTuning.cs:87` is `// WHEELS AND SUSPENSION (raycasts, not WheelColliders)` — a plain `//`
section banner inside the class body, not a `///` block, and not an explanation of why raycasts were
chosen. And the count of six reproduces only case-insensitively: `git grep -n "WheelCollider"
origin/main -- "Assets/"` returns five lines, because `VehicleController.cs:79` reads
`/// WHY RAYCASTS AND NOT WHEELCOLLIDERS`. A document that quotes its search terms elsewhere should
say the sweep was case-insensitive, or the next reader re-running it finds a different number.

§1.5: "Bindings (`READ`, `VehicleInput.cs:218-234`): handbrake `Space` / gamepad LB, boost `Left Alt`
/ RB, jump `Left Shift` / A, flip `Q` + WASD / X + stick." The flip's gamepad binding —
`flip.AddBinding("<Gamepad>/buttonWest")`, the "X" in that sentence — is at `:235`, one line past the
cited range (**confirmed**).

## Closed from the prior pass, so the next pass need not reopen them

**Prior F1, §4.3's subscriber list.** `git grep -n "OrderResolved +=" origin/main --
"Assets/**/*.cs"` returns exactly the seven sites the document now names, at the lines it names:
`CargoSystem.cs:85`, `OrderHUD.cs:68`, `OrderTicketStrip.cs:108`, `PlayerProgression.cs:101`,
`ResultsPanel.cs:48`, `ShiftResults.cs:112`, `PlaytestLogger.cs:113`, labelled `MEASURED` with the
command. Every line in the new `OrderTicketStrip` freeze row verifies — stamp `:133-135`, banner
`:138`, unconditional `Update` → `SyncTickets` `:155-170` reading `ActiveOrders` with no shift test,
`Time.unscaledDeltaTime` `:158-159`, `RefreshTicket` `:192`, `TimeRemaining` `:207-209` — and the
`ShiftEnded`/`IsShiftActive`/`shiftTimer` sweep over that file still returns nothing. Per-scene
attachment reproduces by GUID census: `CargoSystem` and `OrderHUD` 1 in all four,
`OrderTicketStrip` and `PlayerProgression` 0/1/1/1, `ShiftResults` 1/1/1/1, `ResultsPanel` 0/0/0/0,
`PlaytestLogger` 1/0/0/0, with neither prefab carrying any of them but `ResultsPanel` — so five live
in Hard, Medium and Easy and four in `Possible game scene`, as stated.

**Prior F2, §5.2's camera chain.** All twelve added lines land: `controlsAsset` with
`guid: 1a2b3c4d5e6f708192a3b4c5d6e7f810` at `PGS:594`, `Hard:1158`, `Medium:39024`, `Easy:3130`,
against that GUID in `VehicleControls.inputactions.meta`; `tuningOverride: {fileID: 0}` at
`PGS:2316`, `Hard:3455`, `Medium:81838`, `Easy:8073`; `input: {fileID: 592668306}` at `PGS:2317`,
`Hard:3456`, `Medium:81839`, `Easy:8074`. The identity resolves too — `--- !u!114 &592668306` at
`PGS:582`, `m_EditorClassIdentifier: …Vehicle.VehicleInput` at `:593`, `m_GameObject` the `Car`'s
`592668305` at `:588`.

**Prior F3, the label residues.** §3.1 step 1 reads `MEASURED`, and the sweep is exact: four hits,
one call — `OrderManager.cs:285` declaration, `:290` log string, `CargoCondition.cs:13` comment,
`CargoSystem.cs:148` call. §5.3's brake intent is at `VehicleInput.cs:29`. §3.4's arrays are at
`PGS:1665`/`:1672`, `Hard:2253`/`:2260`, `Medium:54288`/`:54295`, `Easy:5520`/`:5527`.

**Prior F4, the block-header citations.** §4.2 hop 2 cites the fields now: `ZoneDetection`'s
`m_GameObject` is `{fileID: 592668305}` at `Hard:1339`, `Medium:39205`, `Easy:3311`, `PGS:775`, that
object is `m_Name: Car` at `Hard:1140`, `Medium:39006`, `Easy:3112`, `PGS:576`, and `OrderManager`'s
is `{fileID: 816895043}` at `Hard:2246`, `Medium:54281`, `Easy:5513`, `PGS:1658`, named at
`Hard:2234`, `Medium:54269`, `Easy:5501`, `PGS:1646`. C3's stated check was narrowed to match and
has now actually been run in all four scenes.

## Sampled and exact

Recorded so the next pass can skip it. Every citation below landed where the document says.

`Canvas (2).prefab` on `origin/main`: root `m_Name: Canvas (2)` at `:368` with `m_IsActive: 0` at
`:373`, the `ResultsPanel` MonoBehaviour block opening at `:509` and its
`m_EditorClassIdentifier: …Orders.UI.ResultsPanel` at `:520`, `shiftTimer`/`orderManager`
`{fileID: 0}` at `:526-527`. Easy's instance modifications at `:22448` onward include
`propertyPath: orderManager` → `objectReference: {fileID: 816895044}` at `:22453-22456`, and
`&816895044` is at `Easy:5507` with `m_GameObject: {fileID: 816895043}` at `:5513`, so the explicit
wiring claim holds. The `m_SourcePrefab` GUID is at `Easy:22541`.

§1.1's force budget, re-read on `origin/main`: gravity `:407`, suspension `:718`, drive `:823`,
boost `:878`, steering torque `:1001`, axle grip `:1121-1124`, righting torque `:1163`,
`maxLinearVelocity` `:350`, `Time.fixedDeltaTime = step` `:365`; `VehicleJumpFlip.cs:208` jump,
`:287` and `:294` the flip's velocity and angular-velocity writes.

§5.10's key census: `Tuning_Balanced.asset` holds 118 two-space keys and `VehicleTuning.cs` 108
public field declarations, so the "108 against 108, zero mismatches" reproduces.
`handbrakeGripMultiplier` is serialized 0.25 / 0.35 / 0.18 in the three assets with exactly one `.cs`
occurrence, its declaration at `VehicleTuning.cs:302`. §4.4's dead economy: `TrySpendMoney` at
`PlayerProgression.cs:127` with its only other mention the comment at `:14`; `TryUnlockCapacity` at
`CargoSystem.cs:170`, mentioned in two comments and called nowhere.

§5.8's silence: zero hits for `AudioSource|AudioClip|AudioMixer|PlayOneShot` anywhere under
`Assets/` on `origin/main`, with one `AudioListener` in each of the six scenes.

§2.2's speed bumps: 16 objects named `SpeedBump (n)` in Hard and 17 in Medium, each under a
`SpeedBumps` container that the counts correctly exclude.

## Not re-checked, by scope

The build-binary unpack, the expiry-cadence model, the commit archaeology behind the 50 and the 75,
the lab CSV readings and shift-log rows (all verified exact in the prior pass), the turning-geometry
report's two figures, §5.9's drift run, the resolved world-y bucket run behind §2.2 and §2.3, and the
speed bumps' 0.228 m pivot height. No scene tooling was re-run; the censuses here were read-only
regex passes over the `<ref>` blobs.

</details>

<details>
<summary>Files examined</summary>

- `docs/DESIGN-OVERVIEW.md` — the document under audit, read end to end
- `.agents/tasks/design-overview-2026-10-14/` — `review.md` (prior pass), `notes-map.md` §6,
  `notes-vehicle.md` (targeted reads); `ref/` blobs for `Hard`, `Medium`, `Easy`,
  `Possible game scene`, `Border.prefab`
- `origin/main` — `git grep` for `OrderResolved +=`, `ShiftEnded +=`, `ResolveDelivery`,
  `WheelCollider`, `TrySpendMoney`, `TryUnlockCapacity`, `handbrakeGripMultiplier` and the audio
  types; `git show` of `OrderTicketStrip.cs`, `VehicleInput.cs`, `VehicleController.cs`,
  `VehicleJumpFlip.cs`, `VehicleTuning.cs`, `Tuning_Balanced.asset`, `Canvas (2).prefab`,
  `MainMenu.unity`

Nothing in the repository was modified, no editor was opened, and no tooling script was run.

</details>
