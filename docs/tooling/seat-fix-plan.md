# KnockablePropBaker seating fix: implementation plan

Scope: `Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs` plus one new standalone
maths check under `docs/tooling/`. Nothing else in the project changes.

All paths below are relative to the worktree root:

```
c:\Users\akoba\DIGI2001\Project\GameDesign\Zoom Zoom Delivery Co\.worktrees\seat-fix
```

Step agents start in the main workspace, so every command must `cd` into the worktree first.
A relative path run from the main workspace edits the wrong copy of the project.

## Hard constraints

- No prop's size or scale changes. Transform scale, meshes and the family mass table are untouched.
- Mass stays decoupled from size. No size-derived or cube-law mass.
- `Assets/Project/Scenes/Kyuri's Scene.unity` is NOT edited. The user has it open, and the three
  blockers plus `trashcontainer01.006` were already corrected by hand in commit `c03c220`
  (confirmed by reading the scene: all three blockers at `y = 0.4`, `trashcontainer01.006` at
  `0.5251243`, matching its siblings). The re-bake is a user action in the editor.
- Do not reorder or insert into `SurfaceKind` in `Assets/Project/Scripts/Vehicle/SurfaceType.cs`.
- Do not touch anything under `Assets/Project/Scripts/Orders/`.
- Never set `GIT_LFS_SKIP_SMUDGE=1`.
- Do not add a row to `docs/BUILD-LOG.md`. `test_documents.py` asserts that file contains zero real
  rows; a row breaks the harness.
- New text files are written with LF endings. `.gitattributes` sets `eol=lf` on `.cs`.

## Correction to the brief, read this first

The brief's worked example is wrong about which local axis points up, and the plan uses the
corrected arithmetic. Do not re-derive it; the numbers below come from the scene file and were
checked twice (quaternion-to-matrix, then the quaternion sandwich `v' = q v q*`).

For `RoadBlocker04.*` — rotation `{x:-0.5000001, y:0.5, z:0.49999994, w:0.5}`, uniform scale
`230.0039` — the y-row of `localToWorldMatrix` is `(m10, m11, m12) = (0, 0, 230.0039)`:

| local axis | maps to world | scaled box extent |
| --- | --- | --- |
| X | −Z | 3.969 m |
| Y | −X | 0.967 m |
| Z | **+Y (up)** | **2.175 m** |

So the true world vertical half-extent is `230.0039 * 0.009454458 / 2 = 1.087281 m`, not the
1.985 m in the brief. 1.985 m is half the **local X** extent, and this rotation sends local X to
world −Z. The reference box is a 4 m long, 2.17 m tall, 0.97 m thick barrier panel, which is what a
road blocker in a city modelled 2–5x oversized should be.

Every prop in the bake is a Blender export carrying the Z-up→Y-up import rotation, so **local Z is
world up for all four families** (verified for the blocker, both picnic table variants, the trash
containers and the mobile toilets).

### The actual root cause, and why one fix is not enough

`SeatOnRoad` computes `boxBottom = TransformPoint(box.center).y - |box.size.y * lossyScale.y| / 2`.
That is only right when the box's local Y extent happens to equal its world height. And it does,
for every box the baker *measures*, because `MakeKnockable` (lines 333–341) writes a **world** AABB
straight into **local** axes:

```csharp
box.size = new Vector3(world.size.x / s.x, world.size.y / s.y, world.size.z / s.z);
```

Two errors of the same sign: the fitter puts the measured world height into local Y, and the seater
reads the height back out of local Y. The bottom it gets is correct; the collider it built is not.
`picnictable01.001` currently has a collider that is 9.21 × 9.21 × 1.98 m in world space — a 9 m
tall slab standing on edge where the table is 9.21 × 1.98 × 9.21.

The three blockers broke because their box is *not* measured: `ApplyReferenceBox` copies the
hand-fitted box off `RoadBlocker04.002`, whose local Y carries the 0.967 m thickness, not the
2.175 m height. The seater then used 0.4834 m where 1.0873 m was needed and buried them
(−0.0899 against the hand-fitted 0.400).

Consequence for the plan: **fixing `SeatOnRoad` alone would move all 30 correct props**, because
their stored boxes are mis-oriented — `picnictable01.*` would rise 3.6 m, the toilets would sink
0.64 m. The seating fix and the box-fit fix must land together. With both applied, the
rotation-aware bottom of a measured prop is *numerically identical* to what the old code computed
(both equal `measured world centre.y − measured world height / 2`), so the 30 props do not move at
all, whatever road height sits under each one.

### Design decisions

1. **Half-extent by matrix projection, not `box.bounds`.** `halfHeight = |m10·e.x| + |m11·e.y| +
   |m12·e.z|` with `e = box.size * 0.5f` and `m = transform.localToWorldMatrix`. `box.bounds` reads
   PhysX's cached pose, which goes stale the moment the baker writes `transform.position` and needs
   `Physics.SyncTransforms()` to be trusted in edit mode; the matrix form is pure transform maths
   and needs no sync. **Pitfall:** `e` must be the raw `box.size * 0.5f`. The matrix columns already
   carry the scale, so pre-scaling `e` double-applies it and returns 250.08 m for the blocker.
2. **Measure the mesh in local space, not by inverting a world AABB.** `BoxCollider.center/size`
   live in local space, so taking the robust percentile bounds directly off `mesh.vertices` gives a
   box that is exact under any rotation and deletes the divide-by-scale step. Inverting a world AABB
   needs a per-axis system that is singular at 45°. The measured world dimensions are unchanged —
   percentile selection per axis is invariant to an axis permutation and to positive scaling — only
   the axis each dimension lands on changes.
3. **Hand-fitted families keep the hand-fitted seating.** For a family with a `ReferenceObject`, the
   reference's own box-bottom-to-ground offset becomes the clearance for the whole family instead of
   the generic 0.03 m. `RoadBlocker04.002` sits with its box bottom at `0.313999`, 0.086 m below the
   road top at `0.40`; reproducing that offset puts `.001` and `.003` at exactly `y = 0.400`. The
   user fitted and placed that object by eye and called it perfect, so it is the ground truth, and
   the 8.6 cm of initial overlap is a frame or two of PhysX depenetration, not the metre-deep
   penetration that pinned the earlier bakes.
4. **Upward-lift cap is `max(1 m, box world height)`, not a flat 1 m.** A flat 1 m rejects the
   legitimate first bake: the file's own comment records import bounds reaching −0.59 m for tables
   and −1.61 m for toilets against a road at +0.40, so first-bake lifts are ~1.02 m and ~2.04 m.
   The cap still rejects the 6.24 m overhang lift that floated `trashcontainer01.006`.
5. **The maths proof goes in `docs/tooling/seat_math_check.py`, not in `repo-checks`.** The brief
   allowed either. A standalone script keeps `repo-checks` reporting exactly the 34 the verification
   spec names, and `repo-checks/README.md`'s module map scopes that harness to version-control and
   document contracts — geometry is not in it, and its `conftest.py` only provides Git plumbing
   fixtures. `compile_check.py` is the precedent for a hand-run check living directly under
   `docs/tooling/`.

## Verification commands

Discovered and run against the current worktree, all three green before any edit:

```powershell
cd "c:\Users\akoba\DIGI2001\Project\GameDesign\Zoom Zoom Delivery Co\.worktrees\seat-fix"
python docs/tooling/compile_check.py
python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks -q --no-header
python docs/tooling/seat_math_check.py   # added by item 6
```

`compile_check.py` must end with exactly `Compiles clean.` and print:

```
RUNTIME  342 references, 80 scripts (46 excluding imported TextMesh Pro samples)
  errors 0, warnings 0
EDITOR   360 references (18 editor-only), 5 scripts
  errors 0, warnings 0
```

The pytest run takes about 85 s and must report `34 passed`. Do not "fix" the editor pass's
reference handling; the duplicate-reference filtering in `compile_check.py` is deliberate.

# Implementation Plan

- [ ] 1. Make the two verification commands runnable inside the worktree. `compile_check.py` needs
      `Assembly-CSharp.csproj`, `Assembly-CSharp-Editor.csproj` and a `Library/` for the relative
      `HintPath` entries; the worktree has none of them. Copy the two csproj files from the main
      workspace and point `Library` at the main workspace's copy with a directory junction (no admin
      needed). All three paths are gitignored (`.gitignore:51` for `*.csproj`, `:14` for
      `/[Ll]ibrary/`), so none of this can reach a commit. This is already in place from the
      planning pass; re-create it only if a command reports a missing csproj.
      Files: none tracked. Creates `Assembly-CSharp.csproj`, `Assembly-CSharp-Editor.csproj`,
      `Library` (junction) in the worktree root.
      ```powershell
      $m = "c:\Users\akoba\DIGI2001\Project\GameDesign\Zoom Zoom Delivery Co"
      $w = "$m\.worktrees\seat-fix"
      Copy-Item "$m\Assembly-CSharp.csproj" "$w\" -Force
      Copy-Item "$m\Assembly-CSharp-Editor.csproj" "$w\" -Force
      if (-not (Test-Path "$w\Library")) { cmd /c mklink /J "$w\Library" "$m\Library" }
      ```
      Verify: `cd "<worktree>"; python docs/tooling/compile_check.py` ends with `Compiles clean.`,
      0 errors and 0 warnings on both passes, 80 runtime and 5 editor scripts. Then
      `git status --short` shows only the files you meant to change.

- [ ] 2. Add the three world-space box helpers to `KnockablePropBaker.cs` and route the log through
      them. `WorldVerticalHalfExtent(Transform, BoxCollider)` projects the box's scaled local axes
      onto world up (`|m10·e.x| + |m11·e.y| + |m12·e.z|`, `e = box.size * 0.5f`);
      `WorldBoxBottom(Transform, BoxCollider)` returns `TransformPoint(box.center).y` minus that;
      `WorldExtents(Transform, BoxCollider)` returns the full rotation-aware world AABB size
      (`Σ|m[r][j]·e_j|·2` per row) for the diagnostics. Put the derivation in a comment with the
      blocker's numbers worked through — y-row `(0, 0, 230.0039)`, local Z is up, half-extent
      `1.087281 m`, and the `e`-pre-scaled result `250.08 m` named as the trap. Then replace
      `Vector3 ws = Vector3.Scale(box.size, t.lossyScale)` (line 177) with `WorldExtents(...)` and
      `float bottom = t.TransformPoint(box.center).y - ws.y * 0.5f` (line 186) with
      `WorldBoxBottom(...)`, so the log reports what the player sees. The `< 0.1 m` and `> 6 m`
      self-check thresholds stay as they are; for these axis-permutation rotations the longest world
      side is unchanged, so the oversized list stays at the same 9 picnic tables.
      Files: `Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, 0/0 both passes.

- [ ] 3. Fit the collider in local space so it matches the mesh under rotation, and re-fit boxes that
      earlier bakes got wrong. Rename `RobustWorldBounds(Mesh, Transform, out float)` to
      `RobustLocalBounds(Mesh, out float)` and drop the matrix: run the same 2nd-to-98th percentile
      selection and 4% pad over raw `mesh.vertices`, returning local-space bounds (empty-mesh
      fallback becomes `new Bounds(Vector3.zero, Vector3.one)`). In `MakeKnockable`, set
      `box.center = local.center` and `box.size = local.size` directly, deleting the
      `world.size / Mathf.Max(s, 1e-4f)` block at lines 335–341 and its now-stale comment; keep the
      `MeshFilter`/`Renderer` null guards and `strayVertexFraction`. Then extract that fit into
      `FitBoxToMesh(Transform, BoxCollider, Mesh, out float)` and call it from the already-knockable
      branch (line 148–152) for families with no `CopyBoxFrom`, guarded by `Undo.RecordObject(oldBox,
      "refit box")` and a `MeshFilter`/`sharedMesh` null check, so a re-bake corrects the 30
      mis-oriented boxes already in the scene rather than re-seating a wrong box. This item must land
      with item 4 in the same commit: either change alone moves props that are currently right.
      Files: `Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, 0/0 both passes.

- [ ] 4. Rewrite `SeatOnRoad` (line 391) to use the true world bottom and to reject bogus ray hits.
      Split the ray out as `TryFindGroundBelow(Transform t, BoxCollider box, out float groundY)` so
      item 5 can reuse it: start the ray at the prop's own world box top plus `0.25f` instead of
      50 m overhead and cast `boxWorldHeight + 100f` downward — a ray that starts below the awning
      cannot hit it, which is the direct fix for `trashcontainer01.006` landing at `y = 6.7634945`
      while its eight siblings sat at `0.5251`. Keep the existing hit filters (`IsChildOf(t)`,
      `GetComponentInParent<KnockableProp>()`, `attachedRigidbody`, `~0`,
      `QueryTriggerInteraction.Ignore`) and add a normal test: skip any hit more than 45° off world
      up, `Vector3.Dot(h.normal, Vector3.up) < 0.707f` (cos 45°). `SeatOnRoad` then becomes
      `static string SeatOnRoad(Transform t, BoxCollider box, float clearance)` returning `null` on
      success or no-op and a one-line reason naming the prop otherwise; it computes the bottom with
      `WorldBoxBottom` and refuses the move when the lift is upward and larger than
      `Mathf.Max(1f, boxWorldHeight)`, leaving the prop exactly where it is. Downward moves stay
      uncapped: that is the repair path for an already-floating prop. Thread the reasons out through
      a `List<string> unseated` passed into `MakeKnockable(Transform, Family, List<string>)` and
      collected in the already-knockable branch too, then emit one `Debug.LogWarning` at the end of
      `Bake()` listing every prop that was not seated and why (no ground found, hit rejected, or lift
      too large).
      Files: `Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, 0/0 both passes. The
      designed failure mode is visible, not silent: if the road's hit normals ever point the wrong
      way, every prop is reported by name and nothing moves.

- [ ] 5. Seat the hand-fitted families to the reference's own ground offset. After the family
      references are resolved (lines 130–133), compute one clearance per referenced family with
      `TryFindGroundBelow` from item 4: `WorldBoxBottom(reference) - groundY`, falling back to
      `0.03f` with a warning if the reference is missing or no ground is found. Store it beside the
      resolved `BoxCollider` (a second `Dictionary<string, float>` keyed by `Family.Label` is enough)
      and pass it to `SeatOnRoad` for members of that family, `0.03f` for everyone else. For
      `RoadBlocker04` this is `0.313999 - 0.40 = -0.086001 m`, which puts `.001`
      and `.003` back at exactly `y = 0.400` — identical box, rotation, scale and road height as
      `.002`, so the result is exact rather than close. `.002` itself is still excluded from the bake
      and never moves. Also note in a comment that `ApplyReferenceBox` is only exact when reference
      and target share rotation and scale (they do: all `RoadBlocker04.*` are
      `{-0.5000001, 0.5, 0.49999994, 0.5}` at `230.0039`), and warn once if a target's rotation
      differs from the reference's by more than a degree.
      Files: `Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs`
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.`, 0/0 both passes.

- [ ] 6. Add `docs/tooling/seat_math_check.py`, a dependency-free script in the house style of
      `validate_scene.py` (module docstring, `ok`/`FAIL` lines, `main()` returning an exit code,
      `sys.exit(main())`). It reimplements the C# arithmetic — quaternion to matrix, y-row
      `(m10, m11, m12)`, `halfHeight = Σ|m1j · e_j|` — over the real transform values read out of
      `Kyuri's Scene.unity`, hard-coded in the script with a comment saying where they came from, and
      asserts:
      (a) local Z is the world-up axis for all four families' rotations;
      (b) the blocker reference dims `(0.017257674, 0.004203821, 0.009454458)` at scale `230.0039`
      give `1.087281 m` under `(-0.5, 0.5, 0.5, 0.5)` and `0.483448 m` under identity, with a
      comment recording that the brief's 1.985 m is half the local X extent, which this rotation
      sends to world −Z;
      (c) the double-scale trap is pinned to a number: pre-scaling `e` by the lossy scale returns
      `250.08 m` for the same blocker, and the correct helper must not equal it;
      (d) the blocker's world AABB is `(0.967, 2.175, 3.969)` and, at `y = 0.4`, its world centre is
      `1.401280` and its true bottom `0.313999`, so the reference gap is `-0.086001`, and a sibling
      seated with that gap on a road at `0.40` lands at `0.400000` within `1e-6`;
      (e) the no-move invariant for the four measured samples: the corrected box (measured world
      extents re-expressed on the axes they actually occupy) has the same world extents as the box
      in the scene today and a world bottom equal to the old `center.y - size.y*scale.y/2` value
      within `1e-4 m` — `picnictable01.001` `0.430013` (half `0.987530`, y stays `0.5031612`),
      `picnictable02.001` `0.430013` (half `2.680662`, y stays `0.6285775`),
      `trashcontainer01.001` `0.430055` (half `1.283542`, y stays `0.5251251`),
      `mobile_toilet.002` `0.427963` (half `1.952369`, y stays `0.5725833`);
      (f) the first-bake lift figures that justify the `max(1 m, height)` cap: `1.02 m` for a table
      whose bounds reach `-0.59`, `2.04 m` for a toilet reaching `-1.61`, both allowed by
      `max(1 m, height)` and both rejected by a flat 1 m, against the `6.2384 m` overhang lift
      (`6.7634945 - 0.5251`) that floated `trashcontainer01.006`, which both caps reject.
      Files: `docs/tooling/seat_math_check.py` (new, LF endings)
      Verify: `python docs/tooling/seat_math_check.py` exits 0 with every line `ok` and no `FAIL`.
      Then `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks -q --no-header` still
      reports `34 passed` — the new file is outside the harness's fixed path lists, so the count must
      not change.

- [ ] 7. Run the three verification commands in sequence as the final gate, then commit on
      `fix/seat-on-road-rotation` only. One commit, `git add` the three files by name (never `-A`),
      subject at most 72 characters and a body saying why — e.g. subject
      `Seat props by the box's true world height, not its local Y`, body covering the two
      same-signed errors, why the box fit had to change with the seater, and the reference-gap rule
      for the blockers. Do not push. Confirm `git diff --cached --name-only` lists exactly
      `Assets/Project/Scripts/Vehicle/Editor/KnockablePropBaker.cs`,
      `docs/tooling/seat_math_check.py` and `docs/tooling/seat-fix-plan.md`, with no `Library/`,
      no `.csproj` and no `.unity` entry. `git status` also shows an untracked `.hypothesis/` — a
      pre-existing pytest cache from an earlier harness run. Leave it untracked and unstaged.
      Files: no further edits.
      Verify: `python docs/tooling/compile_check.py` → `Compiles clean.` 0/0;
      `python -B -m pytest -p no:cacheprovider docs/tooling/repo-checks -q --no-header` → `34 passed`;
      `python docs/tooling/seat_math_check.py` → exit 0;
      `git diff --cached --name-only | Select-String '^Library/'` → no output (GIT-RULES rule 4).

## Hand-off: what the user runs in Unity

Unity cannot run headlessly here, so the re-bake stays a user action and the scene is left alone.
With the project open on this branch, `Tools > Zoom Zoom > Make Props Knockable` in
`Kyuri's Scene` should report 33 props across four families, 29 skipped below the map, and leave
these local y values:

| Prop | Expected y |
| --- | --- |
| `RoadBlocker04.001` / `.002` / `.003` | `0.400` exactly |
| `picnictable01.001`–`.006` | `~0.5032`, unchanged |
| `trashcontainer01.*` (9) | `~0.5251`, unchanged |
| `mobile_toilet.002`–`.013` | `~0.5725`–`0.5747`, unchanged |
| `picnictable02.001`–`.003` | `~0.6286`, unchanged |

No prop should move by more than ~0.001 m, and no prop should report a rejected seating. The
console's per-family sample line now prints world dimensions in world axis order, so the blocker
reads `0.97 x 2.17 x 3.97 m` where it used to read `3.97 x 0.97 x 2.17`; the nine picnic tables are
still listed as over 6 m on a side, which remains true and remains a warning rather than an error.
The colliders on the 30 measured props change orientation on this bake — a picnic table goes from a
9.2 m tall slab to a 9.2 × 1.98 × 9.2 m table — which is the point: the box now matches the mesh.
No size, scale or mass changes.
