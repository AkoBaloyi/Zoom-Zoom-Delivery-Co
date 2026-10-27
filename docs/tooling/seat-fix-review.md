# Rotation-aware box seating in KnockablePropBaker

The baker had two same-signed errors around the same wrong assumption: the box fitter wrote a
measured *world* AABB straight into *local* box axes, and the seater read the prop's height back
off local Y. Every prop in this import is a Blender export carrying the Z-up to Y-up rotation, so
local Z is the axis pointing at the sky and local Y is horizontal. The fix routes both the fitter
and the seater through rotation-aware helpers (`WorldVerticalHalfExtent`, `WorldBoxBottom`,
`WorldExtents`), fits the collider from `mesh.vertices` in local space so no divide-by-scale step
is needed, and hardens the seating raycast against the overhang hit that parked a bin on an awning
at y = 6.76. Both halves had to land together: fixing the seater alone would have moved all 30
props that are currently in the right place.

Watch for: the empty-mesh fallback in `RobustLocalBounds` now yields a box `scale`× too large
(230 m for a blocker, where the old code produced 1 m) — a regression on an edge path, *confirmed*;
props the seater refuses are reported in a second warning that the headline success line never
mentions, so a total seating failure still reads as a clean bake, *confirmed*; the three blockers
are deliberately seated 8.6 cm *inside* the road to reproduce the hand-placed one, which PhysX will
pop out on wake, *likely*. The brief's own worked example is wrong — the blocker's vertical
half-extent is 1.0873 m, not 1.985 m — and the code is right; see below before any later pass
"corrects" it.

**Verdict**: APPROVED

## Follow-up: issues 1, 2 and 3 are closed

A second commit on this branch closed the top three issues below; the rest of the list still
stands as written.

- **1, empty-mesh fallback** — closed by making the fallback the physical size it used to be
  rather than by refusing the prop: `RobustLocalBounds` now divides `EmptyMeshFallbackMetres`
  (1 m, world) by the transform's scale, so it lands at 1 m in world space instead of 230 m.
  Refusing an empty mesh would have been a behaviour change on a path no prop in the scene
  reaches, and prop sizes on the live path are untouched. Pinned by section 7 of
  `seat_math_check.py`.
- **2, refusals never reach the headline** — the headline now carries
  `"N NOT SEATED: left exactly where they were, each one named below."` and the severity ladder
  logs a bake with refusals as a warning, so it can no longer read as clean at info level. Over
  6 m stays a warning, under 10 cm stays an error.
- **3, undo on the reference-box re-fit** — the `ApplyReferenceBox` branch now calls
  `Undo.RecordObject(oldBox, "refit box")` the way its sibling does.

The per-run verdict JSON this document came with was dropped; it was machine output. The plan and
this review stay, because they carry the root cause and the trade-offs a teammate needs.

## High-level view

The root cause is one assumption in two places, which is why the fitter and the seater had to
change in the same edit. `WorldVerticalHalfExtent` projects the scaled local axes onto world up
with `e` kept as the raw `box.size * 0.5`, so scale is applied once. The no-move invariant for the
30 already-correct props rests on the import's negative prop scale cancelling its negative group
scale; I checked every stored `originalLocalScale` in the scene and all 33 multiply out positive,
so the claim holds for the whole set and not just the five props the proof samples.

Overhang rejection is carried almost entirely by the new ray start, not by the lift cap. The ray
begins 0.25 m above the prop's own box top, which bounds any achievable lift at roughly the prop's
own height, so `max(1 m, box height)` only ever bites inside a 25 cm window. The cap scaling with
the prop also means the tallest family could still be lifted 5.36 m if the ray ever reached that
far. The flip side of the tight ray is that a prop sunk deeper than its own box height can no
longer be repaired at all — it gets reported instead, which is the safe direction but a capability
the 50 m ray had.

Clearance is now per-family: families with a hand-fitted reference inherit the reference's own
box-bottom-to-ground gap rather than the generic 3 cm. For `RoadBlocker04` that gap is −0.086 m,
which puts `.001` and `.003` at exactly the hand-placed `.002`'s y = 0.400 and is the only available
definition of right for a box nobody measured. It also means those three bodies start overlapping
the road.

Failure reporting is much better than it was — every refused seating is named with its reason — but
it lands in a separate warning below a headline that still reads as success, and the box re-fit
added to the already-knockable path is recorded for undo only on the measured branch, not on the
reference-copy branch beside it.

The arithmetic is proved out of band in `docs/tooling/seat_math_check.py`, which re-derives the
quaternion-to-matrix maths over transform values quoted from the scene. It passes. Unity cannot run
headlessly here, so nothing in the diff is verified in-editor; the hand-off table in the plan is
the only end-to-end check, and it is a user action.

All five hard constraints hold: no transform scale or mesh is touched, the family mass table is
byte-identical (60/120/180/5/25 kg with `CompactInertia(mass, 2f)`), and `git diff HEAD
--name-only` lists exactly one file, so `Kyuri's Scene.unity`, `SurfaceType.cs` and
`Assets/Project/Scripts/Orders/` are all untouched. The colliders on 30 props do change
orientation, which is the fix, not a size change.

<details>
<summary>Issues (14)</summary>

None of these block the fix. Ordered by what I would fix first.

1. **Empty-mesh fallback is `scale`× too large** — `RobustLocalBounds` returns a local size of 1 for
   a zero-vertex mesh, which is a 230 m box on a blocker where the old code produced 1 m. Treat an
   empty mesh as not knockable (`FitBoxToMesh` reports failure, `MakeKnockable` counts it as
   `noMesh`). *confirmed*
2. **Refusals never reach the headline** — the summary line and its severity ladder ignore
   `unseated`, so a bake where every prop was refused still opens with "33 props made knockable…
   Scene saved" at info level. Append the count and promote the headline to a warning when
   `unseated.Count > 0`. *confirmed*
3. **Undo not recorded on the reference-box re-fit** — the measured branch calls
   `Undo.RecordObject(oldBox, "refit box")`, the `ApplyReferenceBox` branch beside it records
   nothing, so Ctrl+Z after a re-bake leaves blockers' copied boxes in place. Add the matching
   `Undo.RecordObject`. *confirmed*
4. **Blockers start 8.6 cm inside the road** — the reference-derived clearance is −0.086 m and the
   bodies are baked asleep, so the overlap is resolved as a pop at up to 10 m/s on first contact.
   Watch it in playtest; `rb.maxDepenetrationVelocity ≈ 1` keeps the exact y match and softens it.
   *likely*
5. **Lift cap is near-inert and proportional** — the ray start already bounds any lift at about the
   box height, and `max(1 m, box height)` permits 5.36 m for `picnictable02`. The overhang
   protection is the ray start; do not weaken it on the assumption the cap covers it. *confirmed*
6. **Deeply sunk props can no longer be recovered** — a prop more than its own height plus 25 cm
   below the surface gets no ray hit and is reported rather than repaired. Accept, or add an
   explicit deep-recovery path. *likely*
7. **Downward moves are uncapped and unreported** — a prop floating 50 m up is silently dropped,
   while a 1.1 m lift is refused and logged. Consider reporting large downward moves too.
   *confirmed*
8. **Re-fit overwrites hand-tuned colliders** — the already-knockable path now re-fits every
   non-reference box from the mesh, discarding by-eye collider edits without a log line. Only the
   named reference is protected. *confirmed*
9. **Brief's 1.985 m figure is wrong** — the blocker's vertical half-extent is 1.0873 m; 1.985 m is
   half the local X extent, which this rotation sends to world −Z. Correct the brief so a later
   pass does not "fix" the right number into the wrong one. *confirmed*
10. **Proof script quotes pre-fix axes for the lift caps** — it prints "max(1 m, 9.21 m)" for a
    table whose post-fix height is 1.98 m, and 3.31 m for a bin that is 2.57 m. Conclusions survive;
    derive the caps from the corrected box. *confirmed*
11. **Headline wording understates the re-fit** — "were re-seated on the road in place" no longer
    describes the work, since the boxes were re-fitted too. *confirmed*
12. **Rotation-mismatch warning names one prop** — keyed by `reference.name`, so the first
    off-angle prop is named and the rest are silent, unlike the `unseated` list. *confirmed*
13. **`WorldExtents` reports a world AABB** — fine for the 90°-multiple rotations in these
    families, but it will overstate a tilted prop's size and can trip the `< 0.1 m` or `> 6 m`
    lists when lamps and signs join the list. *confirmed*
14. **Change is uncommitted** — all three files are working-tree only; the plan's final step (one
    commit, three files added by name) has not run. *confirmed*

</details>

<details>
<summary>Details</summary>

## The two same-signed errors, and the no-move invariant

The old fitter did this, after normalising the transform:

```csharp
box.size = new Vector3(world.size.x / s.x, world.size.y / s.y, world.size.z / s.z);
```

`world` is an axis-aligned *world* bounds; `box.size` is *local*. The division cancels scale but not
rotation, so the measured world height landed on local Y — a horizontal axis for every prop here.
`picnictable01.001` carries a 9.21 × 9.21 × 1.98 m collider in world space today: a 9 m slab on
edge where the table is 9.21 × 1.98 × 9.21.

The seater then read the height back off the same wrong axis, which cancelled the error for any box
the baker *measured* — both expressions reduce to `measured world centre.y − measured world
height / 2`. That is why only the three blockers visibly broke: their box is copied from
`RoadBlocker04.002`, whose local Y carries the 0.967 m panel thickness rather than the 2.175 m
height, so the seater used 0.483 m of half-extent where 1.087 m was needed.

Repairing `SeatOnRoad` alone would have moved
every prop whose stored box is mis-oriented: tables up 3.6 m, toilets down 0.64 m. Landing both
changes keeps the rotation-aware bottom numerically equal to what the old code computed, so the 30
correct props stay put. That equality depends on the box *centre* surviving the switch from
"inverse-transform a world centre" to "take the local centre directly", which holds exactly when
the original world scale was positive on every axis. The proof script samples one prop per family;
I checked the whole set instead — the 33 stored `originalLocalScale` values fall into five groups
(12 toilets at +0.9, 3 blockers at +0.421, 9 tables at −0.293, 9 bins at −0.293/−0.162), each under
a group scale of matching sign, so every world scale multiplies out positive.

## The brief's 1.985 m is the wrong axis

The task brief states the vertical half-extent for box dims
`(0.017257674, 0.004203821, 0.009454458)` at uniform scale 230.0039 under `(-0.5, 0.5, 0.5, 0.5)` is
1.985 m. It is 1.0873 m. Deriving the matrix from that quaternion gives

```
       local X   local Y   local Z
world  [  0        -1         0   ]
world  [  0         0         1   ]   <- y row: local Z is world up
world  [ -1         0         0   ]
```

so the y row of `localToWorldMatrix` is `(0, 0, 230.0039)` and
`half = 230.0039 × 0.009454458 / 2 = 1.087281 m`. 1.985 m is `230.0039 × 0.017257674 / 2`, half the
*local X* extent, which this rotation sends to world −Z: it is the 4 m length of the barrier panel,
measured horizontally. The brief's identity-rotation figure of 0.4834 m is right, and is exactly
the local-Y reading the old code used — which is the bug, not the target.

The code, the plan and the proof script all carry the corrected number and all three name the
brief's error explicitly. The risk here is procedural rather than technical: a later pass taking the
brief at face value would "fix" a correct 1.0873 into a wrong 1.985.

## Overhang rejection is the ray start, not the lift cap

`TryFindGroundBelow` starts the ray at `worldCentre.y + half + 0.25f` and requires
`Vector3.Dot(h.normal, Vector3.up) >= 0.707f`, keeping the three existing ignores (own colliders via
`IsChildOf`, `GetComponentInParent<KnockableProp>()`, `attachedRigidbody`). That is what the brief
asked for, and it is the real fix for `trashcontainer01.006`: an awning 6.24 m up is now outside the
ray's reach, because a bin's box is 2.57 m tall and the ray can only see 2.82 m of column.

Which makes the lift cap mostly redundant. The highest surface the ray can find is
`boxTop + 0.25`, so the largest achievable lift is about the box's own height plus 25 cm, while the
cap is `max(1 m, box height)`. The cap therefore only rejects within a 25 cm window above the prop's
height, or for props under 1 m tall (none in these families). The cap also scales with the prop, so
for `picnictable02` it permits a 5.36 m lift; if the ray start is ever loosened again, the cap alone
will not catch a tall prop landing on a roof. The brief's "~1 m" is deliberately
not what shipped: a flat 1 m would reject the legitimate first bake, which lifts a table 1.02 m and
a toilet 2.04 m against import bounds reaching −0.59 and −1.61. The plan documents that trade and
the proof pins both numbers.

The tight ray costs something. A prop sunk more than its own height plus 25 cm into the road can no
longer be seated: the ray starts below the road's top face, and with
`Physics.queriesHitBackfaces` off by default the underside faces are not hit either, so
`TryFindGroundBelow` returns false and the prop is left buried and named in the warning. Safe
direction, reduced capability — the old 50 m ray could dig anything out, at the cost of the awning
bug.

Downward moves stay uncapped and unreported, which is the documented repair path for a floating
prop but is also asymmetric: a prop accidentally sitting 50 m above the city is silently dropped to
the road (the ray reaches `height + 100 m`), while a 1.1 m lift is refused and logged. A silent bake
is not evidence that nothing large moved.

## The blockers start 8.6 cm inside the road

`ReferenceClearance` measures the hand-fitted reference's own box-bottom-to-ground gap and applies
it to the whole family. `RoadBlocker04.002`'s box bottom sits at 0.313998 against a road top of
0.400, so the family clearance is −0.086 m and `.001`/`.003` land at exactly y = 0.400 — same box,
rotation, scale and road height, so the match is exact rather than close, and the reference itself
is still excluded from the bake.

The side effect is three bodies that begin life overlapping a static collider by 8.6 cm. The code
calls this "a frame or two of PhysX depenetration", which is true but understates it: the props are
baked asleep (`rb.Sleep()`), so the overlap is not resolved until something wakes them, and Unity's
default max depenetration velocity is 10 m/s. The first touch from the car will deliver both the
impact impulse and an 8.6 cm pop, which can read as the blocker jumping. Clamping the
reference-derived clearance to `Mathf.Max(0f, gap)` removes the overlap at the cost of the exact y
match; lowering `rb.maxDepenetrationVelocity` to ~1 m/s keeps the match and takes the sting out.
The hand-placed y is the user's stated ground truth, so this is a flag for playtest, not an
objection.

## The empty-mesh fallback got 230× bigger

`RobustLocalBounds` returns `new Bounds(Vector3.zero, Vector3.one)` for a mesh with no vertices,
and `FitBoxToMesh` assigns that straight to `box.size`. The old path divided a 1 m *world* fallback
by the transform's scale, producing a 1 m world box; the new one puts a local size of 1 on a prop
whose scale is 230, which is a 230 m collider. For a table it is 255 m.

It cannot fire for the 33 props in this scene — every one has a real mesh, and `MakeKnockable`
already skips a null `sharedMesh`. But the trigger is a mesh asset with zero vertices, and this
import is on record for dropping geometry ("Unity discarded self-intersecting polygons on import"),
so an empty mesh is not a fantasy. The failure would be loud in the viewport and quiet in the
console: the `> 6 m` list is a warning, not an error, so the bake would still report success with a
255 m dynamic body in the scene. The clean fix is to treat an empty mesh as not knockable — have
`FitBoxToMesh` report failure and let `MakeKnockable` count it as `noMesh` — rather than to
reproduce the old 1 m box, which was never useful either.

## A refused seating never reaches the headline

Naming every refusal with its reason, and leaving the prop where it was, is the right call: a
visibly wrong position beats an invisible teleport. The gap is in how it surfaces. The headline
`StringBuilder` never mentions `unseated`, and the severity ladder is `suspect` → not-saved →
`oversized` → `Debug.Log`, with `unseated` absent from it. If the road's hit normals ever flip and
all 33 props are refused,
the console's first line still reads "33 props made knockable… Scene saved", at info level, with the
refusals in a separate warning underneath. Appending the count to the headline and promoting it to a
warning when `unseated.Count > 0` would close that.

Three smaller diagnostics gaps sit next to it. The headline still says the re-fitted props "were
re-seated on the road in place", which now understates the work: their boxes were re-fitted too,
and that is the change a reader needs to know about. The rotation-mismatch warning in
`ApplyReferenceBox` is keyed by `reference.name` through `rotationWarned`, so it names the first
off-angle prop and goes silent for the rest — deliberate, but it means the `unseated` list names
every offender while this one names one. And `WorldExtents` reports the world AABB of the rotated
box, which equals the box's own dimensions only for 90° multiples; when the family list grows to the
tilted props the file already discusses (lamps, signs), their logged size will be overstated and
could trip the `< 0.1 m` or `> 6 m` lists spuriously.

There is also an undo asymmetry. On the already-knockable path the measured branch guards its
mutation with `Undo.RecordObject(oldBox, "refit box")`, but the branch beside it calls
`ApplyReferenceBox` on an existing box with no `Undo.RecordObject` anywhere in it. The omission is
inherited from the old code, but the new `else if` makes the two neighbours inconsistent: Ctrl+Z
after a re-bake restores a table's box and leaves a blocker's copied box in place.

The already-knockable branch now
re-fits boxes instead of only re-seating them, which is exactly the point — 30 mis-oriented boxes
need correcting — but it also means any collider hand-adjusted since the last bake is overwritten
without a word in the log. Only the named reference is protected. Today that costs nothing, because
`RoadBlocker04.002` is the only hand-fitted box; it is worth knowing before the next round of
by-eye tuning.

## What the proof covers, and one figure it quotes wrong

`docs/tooling/seat_math_check.py` is dependency-free, hard-codes transform values quoted from the
scene file, and re-derives the C# arithmetic line for line. I ran it as a narrow spot-check because
the brief and the code disagreed on a number: every line `ok`, exit 0. It covers local-Z-is-up for
all four families, both half-extent figures, the double-scale trap pinned at 250.08 m, the reference
gap landing siblings at 0.400000 from four different starting heights, the old formula reproducing
the buried −0.08989909 (which is what makes this the bug rather than a coincidence), the no-move
invariant for one prop per family, and the lift-cap bracket.

Its section 6 figures are computed from the *pre-fix* boxes still in the scene, so the caps it
prints are axis-permuted: "max(1 m, 9.21 m)" for a table whose post-fix height is 1.98 m, and a bin
cap of 3.31 m where the post-fix value is 2.57 m. Every conclusion survives (1.02 < 1.98 and
6.24 > 2.57), but the numbers a future reader will quote are wrong, and the whole point of the file
is to be quotable. Deriving the caps from the corrected box would fix it.

The recorded evidence names `compile_check.py` (`Compiles clean.`, 0/0 both passes) and the
`repo-checks` pytest run; it does not mention the math check, which is the one artifact the fix's
central claim rests on, so that run is the gap I closed. Nothing here is verified inside Unity — it
cannot run headlessly in this environment — so the orientation flip on 30 colliders, the blockers
landing at 0.400, and the `trashcontainer01.006` repair are all still predictions. The plan's
hand-off table is the right shape for checking them, and it is a user action in the editor.

## Constraints

Checked rather than assumed. `git diff HEAD --name-only` returns exactly
`Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs`, and
`git status --porcelain` scoped to `Assets/Project/Scenes`,
`Assets/Project/Scripts/Vehicle/SurfaceType.cs` and `Assets/Project/Scripts/Orders` is empty, so the
scene, the `SurfaceKind` enum and the orders code are untouched. The `Families` table is unchanged
in the diff, mass is still `rb.mass = family.MassKg` with `CompactInertia(mass, 2f)`, and there is
no size term anywhere near it. No transform scale is written except the pre-existing
`t.localScale = Abs(lossy)` normalisation on first bake, which preserves magnitudes. The 30
colliders that change orientation are collider geometry, not prop size.

</details>

<details>
<summary>Files changed</summary>

- `Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs` — rotation-aware world box helpers,
  local-space mesh fit, box re-fit on re-bake, overhang-rejecting seater with in-place fallback,
  reference-derived clearance, and the unseated report.
- `docs/tooling/seat_math_check.py` (new, untracked) — standalone proof of the rotated-box
  arithmetic against transform values quoted from the scene.
- `docs/tooling/seat-fix-plan.md` (new, untracked) — implementation plan, including the correction
  to the brief's worked example.

All three are working-tree only; the plan's final step (one commit, three files added by name) has
not run yet. Full diff:
`git diff HEAD -- Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs` in
`.worktrees/seat-fix`.

</details>
