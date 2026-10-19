# Scene tools

Scripts for reading and changing scene wiring without opening the Unity editor. Everything here is
read-only except `place_delivery_points.py`. They exist because scene changes and tuning numbers
have to be justified from evidence, and the figures they produce are cited in the design documents
and in the tuning assets themselves.

Run them from the repository root with Python 3.11 or newer.

| Script | What it does |
| --- | --- |
| `scene_points.py` | Parses a scene's YAML and reports world-space position and scale for every transform, resolving the hierarchy. Takes a scene path and an optional name filter. |
| `find_roads.py` | Recovers the street grid from the pavement, lists every junction, and scores each one by its clearance from the nearest building. This is what the delivery point coordinates came from. |
| `delivery_points.py` | The chosen point set and the constraints it satisfies. One source of truth, imported by the two scripts below. |
| `place_delivery_points.py` | Writes the `Delivery Points` hierarchy into a scene and wires it into `OrderManager`. Refuses to run twice and refuses to run if a `fileID` would collide. |
| `order_distances.py` | Costs all 36 pickup and drop-off pairs and reports how many are completable inside a given order time limit. |
| `validate_scene.py` | Structural check on a scene: duplicate anchors, dangling local `fileID` references, transforms that are neither parented nor registered in `SceneRoots`, and non-reciprocal parent/child links. Also fails on CRLF, which `.gitattributes` forbids for Unity YAML. |
| `fbx_mesh_bounds.py` | Reads local-space mesh bounds out of a binary FBX. Exists because a `MeshCollider` keeps its size in the model asset rather than in the scene, so without this there is no way to say how wide a street is without opening Unity. `--json` writes a map the script below loads. |
| `corner_geometry.py` | Sizes the level against the car. Reports every collider with a world footprint where one can be derived, the free corridor at each delivery anchor, the widest-bottleneck route in from the car's start, and the turning radius the tuning asset allows at a range of speeds. |

## How the corner widths were found

`find_roads.py` measures kerb pivot to kerb pivot, which recovers the street grid but not the width
the car can use: the greybox kerbs are 0.24 m tall and the car's body collider starts at 0.50 m, so
it drives straight over them. The only things it can hit are the blocks that stand above the
suspension's reach.

So `corner_geometry.py` works from colliders instead. Box colliders state their own size. Mesh
colliders do not, so it loads `fbx_mesh_bounds.py` output and calibrates the model-to-world
convention rather than assuming it: of six candidate axis-order and unit-factor combinations it keeps
whichever puts the bottom of a typical object closest to the ground, on the grounds that a city rests
on the floor. In the greybox that winner is off by 0.09 m and the runner-up by 3.35 m, and the same
calibration run against `Easy.unity` independently picks the same convention. As a third check, the
box colliders it reports in the city scene match the figures `KnockablePropBaker` logs from inside
Unity to within a few centimetres.

From the footprints it builds an occupancy grid clipped to the drivable slab, takes the distance to
the nearest obstacle at every free cell, and runs a max-min Dijkstra from the car's start. That last
step answers the question a turning circle actually has to pass: not "how wide is this street" but
"on the roomiest route to this anchor, how wide is the tightest point".

For the tiled city scenes there are no usable mesh bounds for 1295 separate pack models, so
`--road=<regex>` measures the carriageway instead: a modular road abuts, so the spacing between
adjacent tile centres IS the tile size. That gives 8.18 m for `Hard.unity` and 13.81 m for
`Medium.unity`, each confirmed a second way.

The figures these produce, and what they mean for the reverse-gear playtest finding, are written up
in `.agents/tasks/turning-geometry-2026-10-14/report.md`.

## How the delivery points were found

`OrderManager` takes `Transform` arrays for pickups and drop-offs. While they were empty it fell
back to generating points, first on a 60 m ring around the origin and later scattered inside the
`Floor` collider. Both kept orders somewhere in the level; neither put them on a road.

The road tiles are too sparse to work from, since the district only has five distinct ones. The
pavement is not. A street runs between two facing kerbs, so `find_roads.py` clusters the 54
`SideWalk` objects on each axis, pairs adjacent kerb lines into centrelines, and treats the
crossings as junctions. That recovered seven north-south streets and seven east-west ones, so 49
junctions, from geometry rather than from anybody's eye.

Streets come in close pairs, roughly 20 m apart, so the chosen set takes one street from each pair
rather than putting two points on parallel carriageways. Each candidate then had to sit at least
18 m from every building pivot and at least 8 m inside the drivable floor. Twelve were picked for
spread across the district; the tightest building clearance in the set is 43 m.

Points sit at y = 0, which is the top face of the `Floor` collider. `OrderBeacon` lifts its ground
disc by its own `groundDiscYOffset`, so nothing needs raising to avoid z-fighting.

Coordinates are tied to the greybox transform of the day. The district was re-exported on 21
September 2026 and moved from (-95.37, 0, 159.57) at scale 8.46 to (-114.67, -0.02, 130.02) at
scale 8.98, which voided the previous set, and the `Floor` changed from 504 by 440 to 504 by 555 in
the same window. Re-run `find_roads.py` and replace the tables in `delivery_points.py` after every
re-export.

## What the order time limit costs

`Order` starts its countdown at spawn rather than at pickup, so the drive to the pickup is inside
the same budget. Across the 36 pairs the shortest job is 260 m of grid driving including the
approach, the longest is 954 m, and the mean is 557 m. Measured top speed is 32 m/s but cannot be
held through junctions on the measured sideways grip of 13 m/s^2, so jobs are costed at a 20 m/s
working average: 13 s for the shortest, 28 s for the mean, 48 s for the longest.

`orderTimeLimit` in `OrderTuning_Balanced.asset` is 50 s, set for the Milestone 1 build. Against the
real point set that clears all 36 pairs at the working average, with about 2 s spare on the longest
(North East Corner to South West Yard, 48 s), so a clean run of the worst job makes it and a wrong
turn on it does not. The previous 45 s left that one pair about 3 s short. If real laps come in
slower than 20 m/s, moving one drop-off costs less than loosening the timer for every job.

`order_distances.py` takes the limit as an argument, so
`python docs/tooling/scene-tools/order_distances.py 50` re-checks the whole set in one command after
any change to the points or the limit.
