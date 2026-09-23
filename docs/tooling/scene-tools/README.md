# Scene tools

Four scripts for reading and checking Unity scene wiring without opening the editor. Three are
read-only. They exist because scene changes and tuning numbers have to be justified from evidence,
and the figures they produce are cited in the design documents.

Run them from the repository root with Python 3.11 or newer.

| Script | What it does |
| --- | --- |
| `scene_points.py` | Parses a scene's YAML and reports world-space position and scale for every transform, resolving the hierarchy. Takes a scene path and an optional name filter. |
| `validate_scene.py` | Structural check on a scene: duplicate anchors, dangling local `fileID` references, transforms that are neither parented nor registered in `SceneRoots`, and non-reciprocal parent/child links. Also fails on CRLF, which `.gitattributes` forbids for Unity YAML. |
| `order_distances.py` | Works out the driving distance of every pickup and drop-off pair in a coordinate set and reports how many are completable inside a given order time limit. |
| `place_delivery_points.py` | Writes a `Delivery Points` hierarchy into a scene and wires it into `OrderManager`. **The coordinates in it are for the superseded greybox and will not work as they stand.** Kept because the YAML it emits is correct and it refuses to run twice; replace the two coordinate tables before using it. |

## How to place delivery points on real roads

`OrderManager` has empty `pickupPoints` and `dropOffPoints`. It currently generates points inside
the `Floor` collider instead, which keeps them in the level but does not put them on roads. Placing
real points is the outstanding integration job. This is the method, which worked on the previous
greybox and should be repeated on the current one.

Run `scene_points.py` against the game scene and filter for `Plane`. Those are the road tiles. A
tile pivot is on a road centreline if the pavement objects nearest it sit either side at roughly
equal distance: on the previous greybox the tile at x = -95.37 had kerbs at -99.31 and -91.62, which
put the centreline within four centimetres of the pivot and made the carriageway about eight metres
wide. Check each candidate is inside the `Floor` box collider, which spans x -252 to 252 and z -40
to 400 with its top face at y = 0, and that it is well clear of every building pivot. Place the
points at y = 0; `OrderMarkers` lifts its ground ring by its own `ringGroundOffset`, so the marker
clears the road surface without the point being raised.

Coordinates are tied to the greybox transform of the day. The district was re-exported on 21
September and moved from (-95.37, 0, 159.57) at scale 8.46 to (-114.67, -0.02, 130.02) at scale
8.98, and the road tiles dropped from fifteen to six. Any coordinate set from before that date is
void. Re-derive after every re-export.

## Why the order time limit is 75 s

`Order` starts its countdown at spawn rather than at pickup, so the drive to the pickup is inside
the same budget. With points generated inside the `Floor` bounds and the generator's five metre edge
margin, the usable area is 494 by 430 units. The worst case is a pickup in one corner with its
drop-off in the opposite one, about 655 m, plus up to 466 m to reach that pickup from the start
point, so roughly 1,121 m in total. Measured top speed is 32 m/s but cannot be held through
junctions on the measured sideways grip of 13 m/s^2, so 20 m/s is the working average, which puts
the worst case at about 56 s.

`orderTimeLimit` in `OrderTuning_Balanced.asset` was 45 s, which only worked while points sat on a
60 m ring around the origin. It is now 75 s, which covers the worst case with roughly a quarter
spare. The reasoning is also in the asset's own `whyThisSetup` field so it travels with the data,
and it needs re-deriving once real delivery points exist.
