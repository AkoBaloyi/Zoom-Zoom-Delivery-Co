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

`orderTimeLimit` in `OrderTuning_Balanced.asset` is 45 s, which is what it was set to while points
were still generated on a 60 m ring around the origin. Against the real point set that clears 35 of
the 36 pairs; the longest, North East Corner to South West Yard, needs about 48 s and is expected to
run late unless it is driven above the working average. The group chose to keep 45 s rather than
raise it. If that pair proves annoying in play, moving one drop-off costs less than loosening the
timer for every job.

`order_distances.py` takes the limit as an argument, so
`python docs/tooling/scene-tools/order_distances.py 45` re-checks the whole set in one command after
any change to the points or the limit.
