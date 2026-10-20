# Findings — Group 3 (order system & economy) and Group 4 (win/loss & progression)

Investigation of 2026-10-14. Read-only: no game code, scene, asset or tuning value was changed, no
editor was opened, no git state was touched. The only files written are this one and the helper
`docs/tooling/scene-tools/component_fields.py`.

## How to read this

| Label | Means |
| --- | --- |
| `READ` | Literal value sitting in a file, cited `path:line` |
| `MEASURED` | Value recorded by an instrument or extracted from a shipped binary, with the command that produced it |
| `DERIVED` | Arithmetic on `READ`/`MEASURED` values, inputs and operation both shown |
| `INFERRED` | Read of code control flow, no runtime evidence |
| `UNKNOWN` | Not establishable without running the game, plus the single check that would settle it |

**Trees.** `origin/main` is the reference tree. `Hard.unity`, `Medium.unity`, `Easy.unity` are read
from the extracted copies in `ref/`; `Possible game scene.unity`, `ShiftResults.cs`, `OrderHUD.cs`,
`ResultsPanel.cs` are read from `origin/main` via `git show`. Everything in
`Assets/Project/Scripts/Orders/` except those four UI files is byte-identical between `HEAD` and
`origin/main` (verified for `OrderTuning_Balanced.asset`: working tree == `HEAD` blob ==
`origin/main` blob) and is read in place.

**Scope note.** The order scripts are a teammate's code. Nothing here is a change request; it is a
description of what the files currently say and what the shipped build actually ran.

**Reproduction.** Every scene number below comes from:

```
python docs/tooling/scene-tools/component_fields.py <scene> OrderManager ShiftTimer ZoneDetection \
    CargoSystem CargoCondition PlayerProgression ShiftResults ResultsPanel
python docs/tooling/scene-tools/component_fields.py --rev origin/main \
    "Assets/Project/Scenes/Possible game scene.unity" OrderManager ShiftTimer ...
```

---

# Group 3 — Order system & economy

## 3.1 The Value formula, and the three logged totals

**The formula is not flat 10. The prior QA assertion of a flat 10 is wrong, and the shift logs
refute it by themselves.**

The code path, all `READ`:

1. `CargoSystem.TryDeliver` reads the cargo condition and calls
   `orderManager.ResolveDelivery(expectedOrder, conditionMultiplier)`
   (`Assets/Project/Scripts/Orders/CargoSystem.cs:142`, `CargoSystem.cs:148`). This is the **only**
   caller of `ResolveDelivery` in the project (searched every `.cs`; the only other occurrences are
   the declaration and two doc comments).
2. `OrderManager.ResolveDelivery` computes
   `float value = tuning.DeliveryValueFor(order.DesignedDistance) * Mathf.Clamp01(conditionMultiplier);`
   (`OrderManager.cs:298`).
3. `OrderTuning.DeliveryValueFor(d)` returns `baseDeliveryValue + Mathf.Max(0f, d) * valuePerMetre`
   (`OrderTuning.cs:48-50`).
4. `DesignedDistance` is the straight-line pickup→drop-off distance fixed in the constructor,
   `Vector3.Distance(pickupPoint, dropOffPoint)` (`Order.cs:43`, `Order.cs:74`) — never distance
   driven.

So: **value = (baseDeliveryValue + straightLineMetres × valuePerMetre) × clamp01(cargoCondition)**.

### The numbers actually in play — MEASURED from the shipped build

`OrderTuning_Balanced.asset` sets `baseDeliveryValue: 10` (`:34`) and **contains no `valuePerMetre`
key at all** (verified: the identifier appears in exactly four places in the project, all in
`OrderTuning.cs:41`, `:50`, `:59` and a comment in `ProgressionTuning.cs:9`; it appears in no
`.asset`). `OrderTuning.cs:41` declares `public float valuePerMetre = 0.1f`.

Rather than reason about what Unity does with a missing key, I read the values out of the build the
testers played. The built asset's serialized fields sit in declaration order after the
`profileName` string in `Game Build/Zoom Zoom Delivery Co_Data/sharedassets1.assets` at offset
113 113 568:

| field | value in the shipped build |
| --- | --- |
| `profileName` | `Balanced` |
| `spawnInterval` | 12.0 |
| `maxActiveOrders` | 3 |
| **`orderTimeLimit`** | **45.0** |
| `baseDeliveryValue` | 10.0 |
| **`valuePerMetre`** | **0.1** |
| `latePenaltyPerSecond` | 1.0 |

`MEASURED`. Build timestamp 2026-10-14 08:31:46; the four standalone log rows are 09:09–09:38 the
same morning, so this binary is the one that produced them. See §3.3 for why `orderTimeLimit` reads
45 and not the 50 in the YAML — it is the same defect, and it is the only field whose C# default
differs from the asset value.

**So the shipped formula is `(10 + 0.1 × straightLineMetres) × condition`.**

### Reconciling 61, 158 and 216

Logged totals (`MEASURED`, decoded `utf-8-sig`):

| source | date | delivered | late | value | wall_hits |
| --- | --- | --- | --- | --- | --- |
| `PlaytestLogs/shift_log.csv` row 1 (editor run) | 2026-10-12 | 9 | 7 | 61 | 7 |
| `Game Build/PlaytestLogs/shift_log.csv` row 1 | 2026-10-14 09:09 | 4 | 4 | 158 | 1 |
| `Game Build/.../shift_log.csv` row 2 | 2026-10-14 09:17 | 6 | 10 | 216 | 0 |
| `Game Build/.../shift_log.csv` rows 3–4 | 09:29, 09:38 | 0 | 12 / 10 | 0 | 0 |

`PlaytestLogger` accumulates `_value += value` on `OrderResolved` for delivered orders only
(`PlaytestLogger.cs:167-172`) and writes it with `ToString("0")` (`PlaytestLogger.cs:256`), i.e. the
plain sum of the per-order values above, rounded to an integer. It stops counting when the row is
written (`_shiftRunning = false`), so these totals exclude any post-whistle delivery.

Straight-line distances for the point set in `Possible game scene` (`MEASURED` via
`python docs/tooling/scene-tools/order_distances.py 50`, `straight` column): minimum 94 m, maximum
386 m, mean of the 36 pairs 216.7 m.

**Flat 10 is impossible.** With `valuePerMetre = 0` every delivery would be worth
`10 × c ≤ 10`, capping 4 deliveries at 40 and 6 at 60. The logs show 158 and 216. `DERIVED` from
`OrderManager.cs:298` + `OrderTuning.cs:50` + the two log rows.

**A lower bound on `valuePerMetre` from the logs alone.** Since `c_i ≤ 1` and `d_i ≤ 386`:

- 4 deliveries, 158: `158 ≤ 4 × (10 + 386v)` ⇒ **v ≥ 0.076**
- 6 deliveries, 216: `216 ≤ 6 × (10 + 386v)` ⇒ **v ≥ 0.067**

`DERIVED`. Both bounds are consistent with the `MEASURED` 0.1 and both exclude 0. The logs and the
build binary agree independently.

**Is 0.1 sufficient?** Yes, with room: at `v = 0.1` the per-delivery ceiling is
`10 + 0.1 × 386 = 48.6`, so 4 deliveries can reach 194 (158 observed, needs mean designed distance
≥ 295 m at full condition — 12 of the 36 pairs are ≥ 295 m) and 6 can reach 292 (216 observed, needs
mean ≥ 260 m). Both are above the 216.7 m pool mean, so both shifts drew longer-than-average jobs,
which is unremarkable with 4 and 6 samples. `DERIVED`.

**The 61 row is the cargo-condition row.** Nine deliveries at 61 total is 6.78 each — *below*
`baseDeliveryValue` 10, which is only possible with `conditionMultiplier < 1`. Rigorously, since
each term is `(10 + 0.1 d_i) c_i ≥ 10 c_i`:

> `61 ≥ 10 × Σc_i` ⇒ `Σc_i ≤ 6.1` ⇒ **mean condition ≤ 0.678**

and using the 216.7 m pool mean, the expected full-condition total would be `9 × 31.67 = 285`,
implying a **mean condition of about 0.21**. `DERIVED`. That is consistent with
`enableCondition: 1` in that scene (§3.7) and with the 7 logged wall hits plus any sub-threshold
collisions the logger does not count (the logger's wall-hit rule and CargoCondition's collision
rule are different thresholds — see §3.7).

**Verdict: the formula is closed, the three totals are explained, nothing is irreconcilable.** The
one piece not closable from files is the exact per-order breakdown: `UNKNOWN`. Discriminating check:
`OrderManager` already logs it — `"Order {id} spawned: {distance} m, {limit} s to deliver, worth
{value}"` (`OrderManager.cs:219-222`) and `"Order {id} resolved: DELIVERED, +{value} value"`
(`OrderManager.cs:310-312`). Capture one shift's `Player.log` and the per-order values are there in
full. No such log is kept in the repo (searched for `*.log`, `output_log.txt` — none).

## 3.2 Late orders: no deduction, no award

**A late order awards nothing and deducts nothing. `latePenaltyPerSecond` is dead.**

- `Order.Tick` decrements `TimeRemaining` and calls `MarkLate()` at zero (`Order.cs:101-110`).
- `OrderManager.TickAllOrders` reacts with `Resolve(order, value: 0f, wasLate: true)`
  (`OrderManager.cs:274`) — the value is a literal `0f`.
- `Resolve` removes the order, increments `TotalResolved`, and fires `OrderResolved(order, 0f)`
  (`OrderManager.cs:303-315`). Nothing subtracts from any running total.
- `latePenaltyPerSecond` exists in exactly two places: `OrderTuning.cs:44` (declaration) and
  `OrderTuning_Balanced.asset:35` (value `1`). **It is read nowhere.** Searched every `.cs` in
  `Assets/` for the identifier: one hit, the declaration. `READ`.

Runtime consequence: a missed order costs the player the time they spent on it and the money they
would have earned, nothing more. The displayed money only ever goes up. On the consumer side,
`PlayerProgression.HandleOrderResolved` pays `baseXpPerDelivery × lateConsolationXpFraction` for a
late order (`PlayerProgression.cs:120`), and `lateConsolationXpFraction` is `0` in
`ProgressionTuning.asset:17`, so a late order earns zero XP as well (`READ`).

`OnValidate` clamps five fields but never `latePenaltyPerSecond` (`OrderTuning.cs:55-58`), which is
consistent with it being forgotten rather than deliberately inert.

## 3.3 The timer contradiction: 45 / 50 / 75, settled

**The shipped build ran 45 s. The asset says 50 s. The 50 s is unreachable because a merge broke
the asset's YAML.**

Four sources, each `READ`:

| source | value | status |
| --- | --- | --- |
| `OrderTuning.cs:31` | `orderTimeLimit = 45f` | C# field initialiser |
| `OrderTuning_Balanced.asset:33` | `orderTimeLimit: 50` | serialized asset value |
| `docs/tooling/scene-tools/order_distances.py:25`, `:68` | `75.0` | a tooling script's CLI default, never a game value |
| `Game Build/.../sharedassets1.assets` @113113600+8 | `45.0` | what the build actually holds (`MEASURED`) |

**Per scene.** Every one of the four scenes' `OrderManager` references the same tuning asset, GUID
`d45a48eeca7d4364896ca1448c017ed2` = `OrderTuning_Balanced.asset` (`.meta` GUID match):

| scene | `OrderManager.tuning` | serialized order limit source |
| --- | --- | --- |
| `origin/main:Possible game scene.unity:1664` | `OrderTuning_Balanced` | the one asset |
| `ref/Hard.unity:2252` | `OrderTuning_Balanced` | the one asset |
| `ref/Medium.unity:54287` | `OrderTuning_Balanced` | the one asset |
| `ref/Easy.unity:5519` | `OrderTuning_Balanced` | the one asset |

There is no per-scene override: the time limit is a single global number. `READ`.

### Why the build reads 45

`OrderTuning_Balanced.asset` is **not valid YAML**. `whyThisSetup` sits at indentation 0
(`OrderTuning_Balanced.asset:16`) while every other field sits at indentation 2, so it closes the
`MonoBehaviour:` block mapping and then the file re-indents. A standards-compliant parser fails:

```
python -c "import yaml; yaml.safe_load(open('Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset',
          encoding='utf-8-sig').read().split('--- !u!114 &11400000',1)[1])"
→ ParserError: while parsing a block mapping ... expected <block end>, but found
  '<block mapping start>' in "<unicode string>", line 29, column 3:  spawnInterval: 12
```

`MEASURED`. The error lands on the first key after the de-indented block — i.e. **everything from
`spawnInterval` onward is downstream of a structural break**, and `profileName` (line 15) is the
only design field upstream of it.

The build's bytes show exactly that fingerprint: `profileName` loaded as `Balanced`, and
`orderTimeLimit` — the single field whose C# default (45) differs from its asset value (50) — reads
**45**. Every other field after the break happens to have an identical default
(`spawnInterval` 12, `maxActiveOrders` 3, `baseDeliveryValue` 10, `latePenaltyPerSecond` 1), so no
other field can distinguish "asset loaded" from "asset ignored". `INFERRED` mechanism on a
`MEASURED` outcome.

`whyThisSetup` is also **not a field in `OrderTuning.cs` at all** (the class declares
`profileName`, `spawnInterval`, `maxActiveOrders`, `orderTimeLimit`, `baseDeliveryValue`,
`valuePerMetre`, `latePenaltyPerSecond` and nothing else). The rationale block is a comment that
only looks like data, and the one that broke the file.

### Did the 45→75 editor change survive the merges? No.

`orderTimeLimit` through history (`MEASURED`, `git show <rev>:<path>` per commit):

| commit | date | `orderTimeLimit` |
| --- | --- | --- |
| `e9ce21c` | 2026-09-17 | 45 |
| `1c52e42` | 2026-09-23 | **75** |
| `4a5b336` | 2026-09-23 | 45 |
| `c655ac7` | 2026-09-23 | 50 |
| `b008e86` | 2026-09-29 | 50 |
| `dafac15` | 2026-09-29 | 50 |
| `HEAD` / `origin/main` | — | 50 (and unreachable) |

So 75 was briefly a real asset value at `1c52e42` and was reverted the same day. It survives only as
`order_distances.py`'s CLI default. The number that did *not* survive is the 50: it is still in the
file, but the merge commit **`03e0752` "Merge branch 'feature/orders-cargo' into
feature/vehicle-camera" (2026-09-29 21:15)** is where `whyThisSetup` lost its two spaces of
indentation — in `dafac15` and every earlier commit it is indented 2, and from `03e0752` onward it
is indented 0. `MEASURED` by walking `git rev-list HEAD` and comparing the blob hash of the asset at
each commit against `git rev-parse HEAD:<path>` (`72fdec99`); the blob changes between `03e0752` and
its parent `b008e86`. The 50 s decision was made, committed, documented — and then silently
disabled by a merge resolution.

### Independent confirmation from the telemetry

The two logged shifts with zero deliveries are a clean clock test: with nothing delivered, the
expiry schedule is fully determined by `spawnInterval`, `maxActiveOrders` and `orderTimeLimit`.
Stepping `OrderManager.Update`'s exact order of operations (tick, then spawn with the cap checked
*before* the interval decrements — `OrderManager.cs:190-197`) at 1/60 s:

| limit | expired by 221.4 s | expired by 268.7 s |
| --- | --- | --- |
| 45 s | **10** | **12** |
| 50 s | 9 | 12 |
| logged (`late`) | **10** | **12** |

`DERIVED` from `READ` tuning values via the model above. The 221.4 s row discriminates, and it picks
45 s. Two independent lines of evidence — the build's serialized bytes and the logged late cadence —
agree.

### Tight or forgiving?

At 45 s, **tight at the top end and the margin the team reasoned about is gone.**

- The costing in `whyThisSetup` and in `order_distances.py` says 36/36 pairs clear a 50 s limit at a
  20 m/s working average, with the longest job (`Pickup 03 North East Corner` →
  `DropOff 06 South West Yard`, 954 m including the approach leg) at 48 s — about 2 s of slack.
  `MEASURED` (`python docs/tooling/scene-tools/order_distances.py 50`).
- The same tool at 45 s reports **35/36**. The longest job does not fit. `MEASURED` (printed in the
  tool's own sensitivity table: `at 45 s: 35/36`).
- Every "seconds" figure there is `DERIVED` from `WORKING_AVERAGE = 20.0` written into
  `order_distances.py`, not from a driven lap. It is an assumption, and it is optimistic: the
  turning-geometry report measures the stopping distance test at 31.37 m/s entry and 18.09 m
  (`.agents/tasks/turning-geometry-2026-10-14/report.md:62`), and holding 20 m/s through junctions
  is exactly what the lateral grip budget does not allow.
- The clock starts at **spawn, not at pickup** (`Order` constructor sets `TimeRemaining = timeLimit`
  with state `Available`, `Order.cs:76-77`; `OrderManager.SpawnOrder` activates immediately,
  `OrderManager.cs:214`), so the drive *to* the pickup is inside the same budget. With a 147–428 m
  approach (`order_distances.py` output), a long approach plus a long job is the failure case.
- And in the tested build the player could not see any of it: `OrderTicketStrip` — the per-order
  countdown UI — is **absent from `Possible game scene`** in both trees (GUID
  `d62a920a72c9db04fa4abaf122673c94`, 0 occurrences) and **present in `Hard`, `Medium` and `Easy`**
  (1 occurrence each). `READ`. A 45 s deadline with no visible countdown is not a tight design, it
  is an invisible one.

## 3.4 Spawn cadence, caps, zone radii, cargo capacity

All `READ`.

| thing | value | source |
| --- | --- | --- |
| spawn interval | 12 s | `OrderTuning_Balanced.asset:31`; build binary 12.0 |
| active-order cap | 3 | `OrderTuning_Balanced.asset:32`; build binary 3 |
| pickup radius | 6 m | all four scenes (`Hard:1347`, `Medium:39213`, `Easy:3319`, `PGS:783`) |
| drop-off radius | 6 m | all four scenes (`Hard:1348`, `Medium:39214`, `Easy:3320`, `PGS:784`) |
| zone check interval | **0.1 s**, not the `0f` the code default implies | all four scenes (`Hard:1349`, `Medium:39215`, `Easy:3321`, `PGS:785`) vs `ZoneDetection.cs:31` |
| cargo capacity | **1** in `Possible game scene` (`PGS:1959`), **2** in `Hard` (`:2619`), `Medium` (`:62221`), `Easy` (`:6507`) | vs `CargoSystem.cs:42` default 1 |

Beliefs confirmed: 12 s, cap 3, 6 m radii, 1 slot in the shipping scene, 2 slots in the former
Kyuri's Scene (now `Hard.unity`) — and the same 2 in `Medium` and `Easy`, which were not in the
brief.

One belief corrected: `checkInterval` is **0.1 s in every scene**, not every frame. The script's own
tooltip says `0` "stops a fast car clipping through a small radius between checks"
(`ZoneDetection.cs:29-30`), and the scenes override it. The turning-geometry report costs the
consequence: at 32 m/s the car covers 3.2 m between checks, so the guaranteed-detection radius drops
from 6 m to 5.78 m (`.agents/tasks/turning-geometry-2026-10-14/report.md:489-495`). That report also
measures the 6 m radius in all five scenes (`report.md:63`, `:482`) and concludes the zone is
comfortably enterable and stoppable-in (`report.md:497-500`) — cited rather than redone.

**The cap suppresses the interval rather than queueing it.** `TrySpawn` returns on the cap check
*before* touching `_timeUntilNextSpawn` (`OrderManager.cs:190`), so the 12 s does not accrue while
three orders are live. A slot freeing does not produce an instant spawn; it produces one 12 s later.
`READ`. Measured effect, from the model in §3.3: with nothing delivered the system settles into
bursts of three — spawns at 0, 12, 24, then 57, 69, 81, then 114, 126, 138 (at 45 s) — not a steady
drip.

**Point resolution branch, per scene.** `ResolvePickupAndDropOffPoints` prefers real `Transform[]`
arrays, then a `floorBounds` collider scatter, then a 60 m origin ring (`OrderManager.cs:84-137`).
All four scenes take the **first** branch: six `pickupPoints` and six `dropOffPoints` are wired in
each (`Hard:2253-2266`, `Medium:54288-54301`, `Easy:5520-5533`, `PGS:1665-1678`). `floorBounds` is
assigned in three of them (`Hard:2267`, `Medium:54302`, `PGS:1679`) and `{fileID: 0}` in `Easy`
(`:5534`), but it is never reached, so `floorEdgeMargin: 5`, `testPointCount: 6` and
`testPointRadius: 60` are all inert in all four. `READ`/`INFERRED` (code path, not observed at
runtime).

## 3.5 Determinism: the brief's premise is wrong in the detail, right in the conclusion

**There is no `System.Random` anywhere in the project.** Searched every `.cs` under `Assets/`: zero
matches. The randomness is `UnityEngine.Random.Range`, at **six** call sites, not four:

- `OrderManager.cs:176` (×2) and `OrderManager.cs:179` (×2) — `GenerateBoundedTestPoints`, the
  `floorBounds` scatter branch, which **no scene reaches** (§3.4).
- `OrderManager.cs:242` — `PickIndexAvoidingCollisions`, the candidate pick, 8 attempts against a
  0.5 m same-spot threshold.
- `OrderManager.cs:265` — the give-up pick after all 8 attempts collide, which also logs a warning.

No seed is set anywhere. `READ`.

**The precise consequence, which matters:** the spawn *cadence* is fully deterministic — 12 s
interval, cap 3, 45 s expiry, no randomness in the timing at all (§3.3's model reproduces the logged
late counts exactly). What differs between runs is **which** of the 6 pickups and 6 drop-offs each
order uses, drawn independently and uniformly, hence uniformly over the 36 pairs. Since value is
`10 + 0.1 × distance` and the pair distances span 94–386 m, two runs with identical play produce
different scores. `INFERRED` (code path) + `DERIVED` for the distance spread.

**The "same schedule every run" claim.** I could not find it in this repository. Searched every
tracked `.md` (`README.md`, `docs/*.md`, `docs/playtesting/*.md`, `docs/tooling/**/README.md`) for
`schedule`, `seed`, `same orders`, `order sequence`, `every session`, `deterministic`, `repeatab*`,
`predetermined`, `fixed schedule`, `4.2` — no document claims a repeatable order sequence. The
nearest in-repo claim is `ShiftTimer.cs:9-10`, "so every session is the same length and comparable
across playtests", which is about shift **length** and is true. `UNKNOWN` whether the pitch deck
makes the stronger claim: it is not in the repo. Discriminating check: if the pitch's §4.2 does say
the order schedule repeats, it is wrong about locations and right about timing, and the fix is one
`UnityEngine.Random.InitState(seed)` call before the first spawn.

## 3.6 Can money be spent? No.

**`PlayerProgression.TrySpendMoney` has no caller.** `READ`:

- Declared at `PlayerProgression.cs:127`. The only other occurrence of the identifier in any `.cs`
  is the class comment at `PlayerProgression.cs:14`.
- `CargoSystem.TryUnlockCapacity` — the companion that would apply a purchased upgrade — is declared
  at `CargoSystem.cs:170` and likewise called from nowhere.
- Neither is wired through a UnityEvent either. The complete set of `m_MethodName` values across all
  five scenes is `ExitGame, GoToMainMenu, PauseGame, ResumeGame` (gameplay scenes) and
  `BackToMainMenu, ExitGame, LoadEasy, LoadHard, LoadMedium, OpenControlsPanel, OpenDifficultyPanel`
  (`MainMenu`). No inspector hook reaches either method.

So money accumulates and is displayed, and there is nothing to buy. `ProgressionTuning.asset` holds
a full level curve (`baseXpPerDelivery: 5`, `xpPerValuePoint: 0.5`, `lateConsolationXpFraction: 0`,
`xpToNextLevel: 20/35/55/80/110` — `ProgressionTuning.asset:15-23`) and levels do fire, but a level
grants nothing: `LevelUp` is consumed only by `ShiftResults` as a "LEVEL UP!" tag
(`origin/main:ShiftResults.cs:156-160`, `:176-180`) and by `OrderHUD` as a number
(`origin/main:OrderHUD.cs:182-183`). There is no persistence of any kind — no `PlayerPrefs`, no
`JsonUtility`, no save file in `Assets/Project` (the only `File.Write*` in the project is
`VehicleMeasurement.cs:1156`, `:1173`, writing the vehicle-lab CSV). Level and money reset to 1 and
0 on every scene load. `READ`.

## 3.7 CargoCondition: on in every scene, and it does not know a prop from a wall

**The brief's "off by default" is the code default, not the shipped state. `enableCondition` is `1`
in all four scenes.** `READ`: `CargoCondition.cs:42` declares `= false`; the scenes serialize
`enableCondition: 1` at `Hard:1364`, `Medium:39230`, `Easy:3336`, `origin/main PGS:800` (and
`HEAD PGS:805`). The class comment's "OFF BY DEFAULT … exists to be demoed as an optional toggle,
not a change already committed to" (`CargoCondition.cs:15-19`) no longer describes the game.

Penalty magnitudes differ between the shipping scene and the three difficulty scenes — `READ`:

| field | `Possible game scene` | `Hard` / `Medium` / `Easy` | C# default |
| --- | --- | --- | --- |
| `enableCondition` | 1 (`:800`) | 1 (`:1364` / `:39230` / `:3336`) | `false` (`:42`) |
| `hardBrakeMultiplier` | 1.25 (`:804`) | 1.25 (`:1368` / `:39234` / `:3340`) | 1.25 (`:57`) |
| `fallbackHardBrakeDecelThreshold` | 27 (`:805`) | 27 (`:1369` / …) | 27 (`:60`) |
| `hardBrakeDamage` | **12** (`:806`) | **6** (`:1370` / …) | 12 (`:62`) |
| `hardBrakeCooldown` | 0.5 (`:807`) | 0.5 (`:1371` / …) | 0.5 (`:67`) |
| `collisionImpactThreshold` | **6 m/s** (`:808`) | **10 m/s** (`:1372` / …) | 6 (`:71`) |
| `collisionDamagePerMps` | **4** (`:809`) | **1.5** (`:1373` / …) | 4 (`:73`) |

The three difficulty scenes are roughly **a quarter** as punishing per impact as the scene the
testers played: at a 20 m/s hit, `Possible game scene` costs `(20 − 6) × 4 = 56` condition and
`Hard` costs `(20 − 10) × 1.5 = 15`. `DERIVED` from `CargoCondition.cs:192`.

**Hard-brake threshold.** `HardBrakeDecelThreshold` returns
`vehicle.Tuning.brakeDeceleration × Mathf.Max(1.01f, hardBrakeMultiplier)`
(`CargoCondition.cs:87-92`), falling back to the serialized 27 only if no `VehicleController` is
found. The scenes leave `vehicle: {fileID: 0}`, but `Awake` does
`GetComponent<VehicleController>()` (`CargoCondition.cs:102`) and `VehicleController` **is** on the
same `Car` object (`Hard:1175`, `HEAD PGS:615`), referencing `Tuning_Balanced`
(GUID `1a2b3c4d5e6f708192a3b4c5d6e7f811`) whose `brakeDeceleration` is `22`
(`Tuning_Balanced.asset:105`). So the threshold is **22 × 1.25 = 27.5 m/s²**. `DERIVED`. The
brief's 1.25 multiplier is confirmed. For context, the turning-geometry report puts full braking
plus coast drag at 27 m/s² (`report.md:62`), so ordinary braking sits just under the threshold by
0.5 m/s² — the margin the class comment was aiming for, and a thin one.

**Damage mechanics**, all `READ`:

- Condition starts at 100 the moment an order is carried (`CargoCondition.cs:136`).
- A hard brake costs a flat `hardBrakeDamage`, once per `hardBrakeCooldown` (`:165-168`).
- A collision costs `(impactSpeed − collisionImpactThreshold) × collisionDamagePerMps` where
  `impactSpeed` is `collision.relativeVelocity.magnitude` (`:184`, `:192`), and also starts the
  hard-brake cooldown so one hit is not charged twice (`:190`).
- Damage applies to **every** carried order, not the one that was hit (`:206-209`) — so with the
  2-slot scenes, one wall costs both parcels.
- The multiplier is `condition / 100`, floored at 0 (`:208`, `:223`). A trashed delivery still
  delivers; it pays nothing.

**`KnockableProp` is not consulted — confirmed.** `CargoCondition.cs` has no reference to
`KnockableProp` and no layer, tag, mass or `Rigidbody` test in `OnCollisionEnter`
(`CargoCondition.cs:180-194`): the only inputs are `collision.relativeVelocity.magnitude` and the
threshold. So clipping a knockable road blocker at speed costs exactly what hitting a building
costs. In `Possible game scene` a 15 m/s brush against a prop costs `(15 − 6) × 4 = 36` condition,
i.e. 36% of that delivery's pay. `DERIVED`. Whether a tester actually hit a prop rather than a wall
is `UNKNOWN` — the logger records `wall_hits` without a collider identity. Discriminating check:
`CargoCondition` already logs `"{amount} damage from collision ({speed} m/s)"`
(`CargoCondition.cs:211-213`); add nothing, just capture `Player.log` and cross-reference against
`KnockableProp`'s own log line, or log `collision.collider.name`.

**Logged wall hits undercount condition damage.** `PlaytestLogger` counts a hit as a speed drop
greater than `impactSpeedDrop: 3` from at least `impactMinimumSpeed: 10` m/s
(`PlaytestLogger.cs:199`, scene values `HEAD PGS:14046-14047`). `CargoCondition` charges any
impact at or above 6 m/s relative velocity. An 8 m/s shunt from a 9 m/s approach damages cargo and
logs no wall hit. `DERIVED` — and this is why the 61-value row's implied mean condition of ~0.21 is
reachable with only 7 logged hits.

---

# Group 4 — Win/loss & progression

## 4.1 A completed shift is the only outcome. There is no fail state.

`READ`:

- The shift is one countdown: `shiftDurationSeconds = 300f` with the comment
  `// 5 minutes, per Scope` (`ShiftTimer.cs:19`), `startAutomatically = true` (`:21`).
- Serialized per scene, the value is **300 in all four**: `Hard:2301`, `Medium:54336`, `Easy:5568`,
  `origin/main PGS:1713`. `startAutomatically: 1` in all four. No scene overrides the length, so
  Easy, Medium and Hard differ in geometry and cargo slots but not in shift length.
- `Update` decrements and calls `EndShift()` at zero (`ShiftTimer.cs:57-67`). `EndShift` sets
  `IsShiftActive = false`, sets `orderManager.enabled = false`, logs, and fires `ShiftEnded`
  (`:83-95`).

Nothing checks a quota, a score threshold, a life count or a damage total. Searched every `.cs` in
`Assets/Project` for `game ?over`, `GameOver`, `failState`, `FailShift`, `\blose\b`, `quota`,
`minimumDelivered`, `targetDeliveries`: the only hits are two prose comments in
`VehicleController.cs` about losing the back end. **You cannot fail a shift. You can only finish one
with a low number on it.** `READ`.

Running out of road is not a fail state either: nothing resets or respawns the car on a flip, and
the shift clock keeps running.

## 4.2 The EndShift defect, characterised precisely

**What the brief describes is real, and it is narrower and stranger than "late resolutions are
ignored".** The chain, every hop `READ`, the behaviour `INFERRED` (code path, not observed at
runtime):

1. `ShiftTimer.EndShift` sets `orderManager.enabled = false` (`ShiftTimer.cs:87`). That stops
   `OrderManager.Update` only — so spawning (`TrySpawn`) and ageing (`TickAllOrders`) stop, and
   orders still Active or Carried freeze with time on their clocks, exactly as the class comment
   promises (`ShiftTimer.cs:8-10`).
2. `ZoneDetection` is a component on a **different GameObject**: `Car`
   (`Hard:1333`, `Medium:39199`, `Easy:3305`, `origin/main PGS:769`), while `OrderManager` is on
   `OrderManager` (`Hard:2240`, `PGS:1652`). Disabling one does not disable the other.
   `ZoneDetection.Update` keeps running, and it has no `IsShiftActive` check — it checks only
   distance (`ZoneDetection.cs:53-101`).
3. `ZoneDetection.CheckDeliveries` calls `cargoSystem.TryDeliver(orderManager, order)`
   (`ZoneDetection.cs:98`), which calls `orderManager.ResolveDelivery(expectedOrder,
   conditionMultiplier)` directly (`CargoSystem.cs:148`).
4. `ResolveDelivery` guards on one thing only: `order.State != OrderState.Carried`
   (`OrderManager.cs:287`). **There is no `enabled` guard and no `IsShiftActive` guard.** A disabled
   MonoBehaviour's public methods execute normally. The order is marked Delivered, the value is
   computed, `TotalResolved++`, and `OrderResolved` fires (`:295-315`).

So: **after the whistle, the player can still drive a carried parcel into its drop-off and have it
resolve.** Pickups cannot happen (`CheckPickups` requires `order.State == OrderState.Active` and the
frozen Active orders qualify — so in fact pickups *can* still happen too: a frozen Active order
within 6 m will be collected, `ZoneDetection.cs:65`, `:69`, `:74`). Nothing stops driving either: no subscriber
to `ShiftEnded` disables `VehicleInput` or `VehicleController`, and the complete subscriber list is
`ResultsPanel`, `ShiftResults` and `PlaytestLogger`.

### What actually diverges, and what the player sees

| counter | freezes at the whistle? | source |
| --- | --- | --- |
| `ShiftResults.Delivered` / `.Late` / `.Value` / `.XpEarned` | **yes** — `if (IsShowing) return;` in `HandleOrderResolved` and `HandleXpGained` | `origin/main:ShiftResults.cs:137`, `:152`, `:158`, `:164` |
| `PlaytestLogger` row (`delivered`, `late`, `value`) | **yes** — `WriteRow()` sets `_shiftRunning = false`, and every handler early-returns on it | `PlaytestLogger.cs:167`, `:233-234` |
| `OrderManager.TotalResolved` | **no** — incremented unconditionally | `OrderManager.cs:306` |
| `OrderHUD`'s `Delivered N  Late M  $X` line | **no** — `OrderHUD` subscribes to `OrderResolved` and **never subscribes to `ShiftEnded`** | `origin/main:OrderHUD.cs:68`, `:76-87`, `:168` |
| `PlayerProgression.Money` / `Level` | **no** — no freeze rule | `PlayerProgression.cs:109-123`, `:144-145`, `:167-171` |

So the divergence is not "ShiftResults ignores late resolutions". It is: **`ShiftResults` freezes
everything; `OrderHUD` and `PlayerProgression` freeze nothing.**

And the two are on screen at the same time. `ShiftResults` builds its canvas at
`sortingOrder = 50` (`origin/main:ShiftResults.cs:259`) with a 620 × 400 panel anchored dead centre
(`PGS:1578`), while `OrderHUD`'s canvas sets **no** `sortingOrder` at all
(`origin/main:OrderHUD.cs:213`, default 0) and sits in the top-right corner at
`panelPosition {x: -8, y: -8}`, 380 × 210 (`Hard:2914-2915`). They do not overlap. Critically,
**`ShiftResults` never sets `Time.timeScale = 0`** — the only `timeScale` writes in it are the
`= 1f` resets in `Restart` and `GoToMainMenu` (`:202`, `:220`).

The player-visible result: the TIME UP panel appears centre-screen with frozen numbers, the world
keeps moving behind it, the car is still drivable, and the HUD keeps ticking in the corner. Deliver
one more parcel and **the corner of the screen and the middle of the screen show different delivered
counts, different late counts and different money, simultaneously**, with no indication which is the
score. `INFERRED` from the code path; not observed at runtime.

`ResultsPanel.cs` — the other results implementation, new on `origin/main` — *does* freeze the world
(`Time.timeScale = 0f`, `origin/main:ResultsPanel.cs:83`) and *does* freeze its tally
(`if (_shiftOver) return;`, `:61`). It is in **no scene** (§4.4), so that fix is not in the game.

Discriminating check for the whole of §4.2: let a shift expire while carrying a parcel, drive into
the drop-off, and compare the HUD's delivered count with the panel's. One run settles it.

## 4.3 Objectives besides delivering, and progression

**No other objective exists.** Nothing in the shift is scored except resolved orders. `OrderResolved`
is the single source of delivered, late, value, money and XP — declared at `OrderManager.cs:45` and
subscribed by `OrderHUD` (`origin/main:OrderHUD.cs:68`), `ShiftResults`
(`origin/main:ShiftResults.cs:112`), `CargoSystem` (`CargoSystem.cs:85`, to free a slot),
`PlayerProgression` (`PlayerProgression.cs:101`) and `PlaytestLogger` (`PlaytestLogger.cs:113`).
Boost, flips, handbrake time and supersonic time are measured by `PlaytestLogger` and shown by
`BoostGauge`, and feed no score. `READ`.

**Progression is in the game — in three scenes, and it goes nowhere.** Correcting the brief's
premise: a `PlayerProgression` component **is** attached in the restructured scenes.
`READ`, by GUID `cdd06e60aa8338a4bb39e087cbb6958d`:

| scene | `PlayerProgression` attached? |
| --- | --- |
| `Hard.unity` | yes, on `OrderManager` (`:2304`, tuning `ProgressionTuning` GUID `b7cde7f5…`) |
| `Medium.unity` | yes (`:54339`) |
| `Easy.unity` | yes (`:5571`) |
| `Possible game scene.unity` (`HEAD` and `origin/main`) | **no** — 0 occurrences of the GUID |

So the QA report's "no `PlayerProgression` component is attached to the game scene" was true of the
scene it tested, and is false of the three scenes on `origin/main`. Both sources named; code wins.

What progression does there: XP accrues as `baseXpPerDelivery + value × xpPerValuePoint`
(`PlayerProgression.cs:114`), levels fire in a loop so one big delivery can clear several
(`:167-171`), `OrderHUD` draws a level and XP bar (`origin/main:OrderHUD.cs:182-188`), and
`ShiftResults` shows XP earned and a LEVEL UP tag (`origin/main:ShiftResults.cs:174-181`). What it
does **not** do: nothing can be bought (§3.6),
capacity never rises in play (`TryUnlockCapacity` uncalled), and nothing is saved (no `PlayerPrefs`,
no serialisation) — so level and money reset on every scene load, including the Restart button.
`READ`.

Cargo capacity is therefore a per-scene difficulty constant, not a progression reward: 1 slot in
`Possible game scene`, 2 in each of Easy, Medium and Hard (§3.4).

## 4.4 The results screen: `ShiftResults`, not `ResultsPanel`

**`ShiftResults` is the results screen in every scene. `ResultsPanel` is in none of them.** `READ`,
by GUID:

| script | GUID | `Hard` | `Medium` | `Easy` | `PGS` (`origin/main`) | `PGS` (`HEAD`) |
| --- | --- | --- | --- | --- | --- | --- |
| `ShiftResults` | `8f3e2a1c9b7d4e6fa05c1d2e3f4a5b6c` | 1 (`:2171`) | 1 (`:46262`) | 1 (`:4534`) | 1 (`:1562`) | 1 (`:13983`) |
| `ResultsPanel` | `afcd8a025ccf16d4d85203c80cf90775` | 0 | 0 | 0 | 0 | 0 |

In every case `ShiftResults` sits on the `OrderHUD` GameObject.

What it shows, and where each line comes from (`origin/main:ShiftResults.cs`):

| line | text | source |
| --- | --- | --- |
| title | `TIME UP!` | hardcoded (`:285`) |
| delivered | `Delivered   {n}` | `OrderResolved` where `order.State == Delivered` (`:141`, `:170`) |
| late | `Late        {n}` | the `else` branch of the same handler (`:146`, `:171`) |
| value | `Value       {n}` | sum of the per-order values from `ResolveDelivery` (`:142`, `:172`) |
| XP | `XP earned {n}   (Level {L})` + `   LEVEL UP!` | `PlayerProgression.XpGained` / `.LevelUp`; the whole line is hidden if no `PlayerProgression` is in the scene (`:174-185`) |

So in `Possible game scene` the panel has four lines and the XP line is switched off at runtime
(`_xpLine.gameObject.SetActive(false)`, `:184`); in Easy/Medium/Hard it has five. `INFERRED` from
`:174-185` plus the §4.3 GUID table.

Buttons: `RESTART` (reloads the active scene by build index, `:198-203`), `MAIN MENU` (loads the
scene named by `mainMenuSceneName`, serialized as `MainMenu` in all four scenes — `Hard:2186`,
`PGS:1577` — guarded by `Application.CanStreamedLevelBeLoaded`, `:218-231`), and `QUIT`
(`Application.Quit`, no-op in the editor, `:234-244`). The canvas, panel and buttons are built in
code at runtime from the serialized layout numbers; there is no results prefab (`:250` onward). An
`EventSystem` is added if the scene lacks one, or the buttons would be unclickable (`:208-214`).

Two gaps worth naming, both `READ`: the panel never stops the game (§4.2), and `MainMenu` is in the
build settings of both trees, so the Main Menu button works — but `Easy.unity` is **not** in
`m_Scenes` in either tree, while `Medium` and `Hard` are only on `origin/main`:

```
HEAD        : MainMenu(1), Possible game scene(1), VehicleLab(0), SampleScene(0)
origin/main : MainMenu(1), Possible game scene(1), VehicleLab(0), SampleScene(0), Medium(1), Hard(1)
```

`MEASURED` from `ProjectSettings/EditorBuildSettings.asset` at each revision. The shipped build
contains exactly two scenes — `Assets/Project/Scenes/MainMenu.unity` and
`Assets/Project/Scenes/Possible game scene.unity`, the only two scene paths in
`Game Build/Zoom Zoom Delivery Co_Data/globalgamemanagers`, with `level0` and `level1` the only
level files present. Unity 6000.5.4f1. So the tested build predates the Easy/Medium/Hard
restructure entirely, and all five logged shifts name `Possible game scene`.

---

# Contradictions

Each entry: the two sources, the discriminating check, the verdict, and the label of the verdict.

### C-1 Order value: flat 10 vs the logged totals
- **A:** prior QA pass — delivery value is a flat 10.
- **B:** `OrderTuning.cs:48-50` + `OrderManager.cs:298` — value is
  `(baseDeliveryValue + metres × valuePerMetre) × condition`.
- **Check:** flat 10 caps 4 deliveries at 40 and 6 at 60; the logs show 158 and 216.
- **Verdict: B. A is wrong.** The build's own bytes read `valuePerMetre = 0.1`, and the logs
  independently force `valuePerMetre ≥ 0.076`. `MEASURED` + `DERIVED`.

### C-2 `valuePerMetre`: declared in code, absent from the asset
- **A:** `OrderTuning.cs:41` — `= 0.1f`.
- **B:** `OrderTuning_Balanced.asset` — no such key.
- **Check:** read the field out of the shipped build's serialized asset data.
- **Verdict: 0.1 is what runs.** `MEASURED` (`sharedassets1.assets` @113113600+16 = 0.1).

### C-3 Order time limit: 45 vs 50 vs 75
- **A:** `OrderTuning.cs:31` — C# default 45.
- **B:** `OrderTuning_Balanced.asset:33` — 50, with a 400-word rationale at `:16`.
- **C:** `docs/tooling/scene-tools/order_distances.py:25`, `:68` — 75.
- **Check:** (i) read the value out of the build binary; (ii) model the expiry cadence against the
  two zero-delivery log rows.
- **Verdict: the build ran 45.** (i) `sharedassets1.assets` @113113600+8 = 45.0, `MEASURED`.
  (ii) the model predicts 10 expiries by 221.4 s at a 45 s limit and 9 at 50 s; the log says 10,
  `DERIVED`. The asset's 50 is unreachable because `whyThisSetup` lost its indentation in merge
  `03e0752`, breaking the YAML mapping (PyYAML `ParserError` at the first key after it). 75 was a
  real asset value only at commit `1c52e42`, reverted the same day, and is otherwise a tooling
  default. The QA report observed the 45 and recorded the cause as "unverified"
  (`docs/QA-PLAYTEST-REPORT-2026-10-14.md:183`); this closes it.

### C-4 Late penalty: a tuned field nothing reads
- **A:** `OrderTuning.cs:44` + `OrderTuning_Balanced.asset:35` — `latePenaltyPerSecond: 1`.
- **B:** `OrderManager.cs:274` — the late path passes a literal `value: 0f`.
- **Check:** search every `.cs` for the identifier.
- **Verdict: B. The field is dead.** One hit, the declaration. A late order neither pays nor
  deducts. `READ`.

### C-5 `CargoCondition` "off by default" vs every scene turning it on
- **A:** `CargoCondition.cs:15-19`, `:42` — off by default, "a change not already committed to".
- **B:** all four scenes serialize `enableCondition: 1`.
- **Check:** read the serialized field per scene.
- **Verdict: B. It is on in the shipped game,** and it is the term that explains the 61-value shift.
  `READ`.

### C-6 Penalty magnitudes: shipping scene vs difficulty scenes
- **A:** `Possible game scene` — `hardBrakeDamage 12`, `collisionImpactThreshold 6`,
  `collisionDamagePerMps 4`.
- **B:** `Hard`/`Medium`/`Easy` — `6`, `10`, `1.5`.
- **Check:** read all four scenes.
- **Verdict: both are real; the tested scene is roughly 4× harsher per impact.** Any balance read
  off the 2026-10-12/14 logs does not transfer to the current scenes. `READ` + `DERIVED`.

### C-7 `checkInterval`: code default 0 vs scenes 0.1
- **A:** `ZoneDetection.cs:31` — `0f`, with a tooltip explaining why 0 matters for a fast car.
- **B:** all four scenes — `0.1`.
- **Check:** read the serialized field per scene.
- **Verdict: B, 0.1 s runs,** costing ~0.22 m of guaranteed detection radius at 32 m/s
  (`.agents/tasks/turning-geometry-2026-10-14/report.md:489-495`). `READ`.

### C-8 `System.Random` in four places vs `UnityEngine.Random` in six
- **A:** brief — unseeded `System.Random`, four sites.
- **B:** project — zero `System.Random`; six `UnityEngine.Random.Range` calls, four of them in a
  branch no scene reaches.
- **Check:** regex search of every `.cs`.
- **Verdict: B.** The conclusion survives: unseeded, so order *locations* differ every run. But the
  *cadence* is fully deterministic, which is why the §3.3 model reproduces the logged late counts.
  `READ` + `DERIVED`.

### C-9 "Same schedule every run"
- **A:** reported pitch §4.2 — the same schedule every run.
- **B:** `OrderManager.cs:242`, `:265` — unseeded random point pick.
- **Check:** searched every tracked `.md` for the claim.
- **Verdict: the claim is not in this repository,** so it cannot be cited or contradicted from here.
  `UNKNOWN`. Timing repeats, locations do not; if the pitch says both repeat it is half wrong, and
  one `Random.InitState` call would make it fully right.

### C-10 No `PlayerProgression` in any gameplay scene
- **A:** `docs/QA-PLAYTEST-REPORT-2026-10-14.md:204` and the brief — no component attached.
- **B:** `Hard:2304`, `Medium:54339`, `Easy:5571` — attached, with `ProgressionTuning` assigned.
- **Check:** count the script GUID per scene.
- **Verdict: B for the restructured scenes, A for `Possible game scene`.** The QA report predates
  the restructure and is correct only about the scene it tested. `READ`.

### C-11 Which script owns the results screen
- **A:** `ResultsPanel.cs`, new on `origin/main`, freezes the world and the tally.
- **B:** `ShiftResults.cs`, which freezes the tally but not the world.
- **Check:** script-GUID occurrence count per scene.
- **Verdict: B in all five scenes; A is in none.** `READ`.

### C-12 `TrySpendMoney` / `TryUnlockCapacity` callers
- **A:** `PlayerProgression.cs:14` and `CargoSystem.cs:18-21` — "the upgrade screen calls it".
- **B:** no caller in any `.cs` and no `m_MethodName` in any scene.
- **Check:** identifier search plus a `m_MethodName` inventory of all five scenes.
- **Verdict: B. The upgrade economy is unreachable code.** `READ`.

### C-13 Stale serialized fields (verified case by case)
- `enableMultiLegOrders`, `multiLegChance`, `minTransferLegs`, `maxTransferLegs`,
  `extraTimePerTransferLeg` (`OrderTuning_Balanced.asset:36-40`) — **stale**, none exists in
  `OrderTuning.cs`.
- `whyThisSetup` (`:16`) — **stale and harmful**: not a field in `OrderTuning.cs`, and its
  indentation is what breaks the asset (C-3).
- `transferRadius` (`HEAD PGS:789`) — **stale**, not in `ZoneDetection.cs`. Already gone from
  `origin/main`'s copy of the scene.
- `hardBrakeDecelThreshold: 18` (`HEAD PGS:808`) — **stale**: `CargoCondition.cs` declares
  `hardBrakeMultiplier` and `fallbackHardBrakeDecelThreshold`, and only a read-only *property*
  named `HardBrakeDecelThreshold` (`:88`), which is not serialized. Already replaced in
  `origin/main`'s copy. Note the 18 would have been *below* the car's ordinary 22 m/s² brake — the
  exact bug the class comment says was fixed (`CargoCondition.cs:21-33`); the fix landed in code
  and the old scene value lingered until the scene was re-saved.
- **Verdict:** `READ`. Unity ignores a serialized key with no matching field and drops it on the
  next save; the field keeps its C# initialiser value. The only one with a behavioural consequence
  is `whyThisSetup`.

---

# Open questions (`UNKNOWN`, each with the one check that settles it)

1. **Per-order value breakdown for the three logged shifts.** Capture one shift's `Player.log`:
   `OrderManager.cs:205-215` and `:308-311` already print distance, limit and value per order.
2. **Does Unity's YAML reader reject `OrderTuning_Balanced.asset` outright, or parse it partially?**
   The build bytes prove the *outcome* (45, and `profileName` loaded); the mechanism is inferred.
   Open the asset in the Inspector: if `Order Time Limit` reads 45 the parse is failing; if it reads
   50 then something else produced the 45 and the build provenance needs chasing instead.
3. **Whether any tester's condition loss came from a `KnockableProp` rather than a wall.**
   `CargoCondition.cs:212-214` already logs each damage event; add `collision.collider.name` to it,
   or cross-reference `KnockableProp`'s own log, and read one `Player.log`.
4. **Whether the pitch deck claims a repeatable order schedule.** Not in the repo; check the deck.
5. **What a post-whistle delivery looks like on screen.** Let the clock expire while carrying a
   parcel, drive into the drop-off, and compare the HUD corner against the TIME UP panel (§4.2).
6. **Why two of five logged rows have `shift_seconds = 0`.** `PlaytestLogger.cs:302` computes
   `Mathf.Round(Time.time - _shiftStartTime + shiftTimer.TimeRemaining)`, which cannot be 0 after
   221 s of play; `:244` falls back to `0f` only when `shiftTimer` is null. The likely mechanism is
   the `ShiftTimer` being destroyed before the logger during application shutdown, making the
   reference a destroyed-object null, but that is Group 5's territory. Either way those two rows are
   partial records and they are exactly the two with 0 delivered. Check: quit a build mid-shift and
   read the written row.

---

# Where the documentation is wrong

| claim | where | what is true |
| --- | --- | --- |
| delivery value is a flat 10 | prior QA pass | `(10 + 0.1 × straight-line metres) × condition`; the build holds `valuePerMetre = 0.1` |
| the order limit is 50 s | `OrderTuning_Balanced.asset:16` (`whyThisSetup`), `docs/tooling/scene-tools/README.md` costings | the build ran 45 s; the 50 is in the file but unreachable |
| the order limit is 75 s | `order_distances.py:25`, `:68` (CLI default) | never a game value since `1c52e42` was reverted on 2026-09-23 |
| cause of the 45-vs-50 mismatch unverified | `docs/QA-PLAYTEST-REPORT-2026-10-14.md:183` | merge `03e0752` de-indented `whyThisSetup` and broke the asset's YAML |
| no `PlayerProgression` in the game scene | `docs/QA-PLAYTEST-REPORT-2026-10-14.md:204` | true of `Possible game scene`, false of Easy/Medium/Hard |
| cargo condition is off by default | `CargoCondition.cs:15-19` | `enableCondition: 1` in all four scenes |
| zone checks run every frame | `ZoneDetection.cs:29-31` tooltip | `checkInterval: 0.1` in all four scenes |
| the upgrade screen calls `TrySpendMoney` / `TryUnlockCapacity` | `PlayerProgression.cs:14`, `CargoSystem.cs:18-21` | no caller exists in code or in any scene |
| "every session is the same length and comparable across playtests" | `ShiftTimer.cs:9-10` | true for length (300 s everywhere); order locations and therefore scores are not comparable run to run |

---

# Reproduction appendix

```powershell
# serialized component values, any scene, any revision
python docs/tooling/scene-tools/component_fields.py ".agents/tasks/design-overview-2026-10-14/ref/Hard.unity" OrderManager ShiftTimer ZoneDetection CargoSystem CargoCondition PlayerProgression ShiftResults ResultsPanel
python docs/tooling/scene-tools/component_fields.py --rev origin/main "Assets/Project/Scenes/Possible game scene.unity" OrderManager ShiftTimer ZoneDetection CargoSystem CargoCondition PlayerProgression ShiftResults ResultsPanel

# route costing for the tested point set
python docs/tooling/scene-tools/order_distances.py 50
python docs/tooling/scene-tools/order_distances.py 45
```

```python
# the shipped build's own tuning values (MEASURED, §3.1 / §3.3)
import struct
data = open('Game Build/Zoom Zoom Delivery Co_Data/sharedassets1.assets', 'rb').read()
i = data.find(b'OrderTuning_Balanced')
j = data.find(b'Balanced', i + 20) + 8            # past the profileName string
print(struct.unpack('<f i f f f f', data[j:j+24]))
# -> (12.0, 3, 45.0, 10.0, 0.10000000149011612, 1.0)
#     spawnInterval, maxActiveOrders, orderTimeLimit, baseDeliveryValue, valuePerMetre, latePenaltyPerSecond
```

```python
# the asset does not parse (MEASURED, §3.3)
import yaml
src = open('Assets/Project/Scripts/Orders/OrderTuning_Balanced.asset', encoding='utf-8-sig').read()
yaml.safe_load(src.split('--- !u!114 &11400000', 1)[1])   # ParserError at "spawnInterval: 12"
```

```python
# expiry cadence model (DERIVED, §3.3) — mirrors OrderManager.Update at 1/60 s
DT = 1/60
def run(limit, cap=3, interval=12.0, horizon=300.0):
    active, next_spawn, expiries, t = [], 0.0, [], 0.0
    while t < horizon:
        for k in range(len(active) - 1, -1, -1):
            active[k] -= DT
            if active[k] <= 0:
                expiries.append(t); active.pop(k)
        if len(active) < cap:                      # cap checked before the interval decrements
            next_spawn -= DT
            if next_spawn <= 0:
                active.append(limit); next_spawn = interval
        t += DT
    return expiries
for limit in (45.0, 50.0):
    e = run(limit)
    print(limit, [sum(1 for x in e if x <= m) for m in (221.4, 268.7, 300.0)])
# -> 45.0 [10, 12, 15]   50.0 [9, 12, 13]   logged late: 10 @221.4 s, 12 @268.7 s
```

```powershell
# shift logs (both UTF-8 BOM; decode utf-8-sig or the first column reads wrong)
python -c "import csv,io; print(list(csv.DictReader(io.StringIO(open('Game Build/PlaytestLogs/shift_log.csv','rb').read().decode('utf-8-sig')))))"
```
