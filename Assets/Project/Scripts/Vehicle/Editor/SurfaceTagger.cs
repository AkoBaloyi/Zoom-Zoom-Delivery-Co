using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ZoomZoom.Vehicle.EditorTools
{
    /// <summary>
    /// Labels the city's ground so the car knows what it is driving on.
    ///
    /// THE RULE
    /// Everything under the object named "Roads" is tarmac. Every other static collider the car can
    /// land on is grass, unless it already carries a tag, which wins: the commercial bases were tagged
    /// Concrete by hand and stay Concrete. That is the whole rule, and it replaced a name-pattern
    /// table because the table kept being wrong: it tagged Roads_Parent, whose MeshCollider has no
    /// mesh and cannot be hit, and it left the Floor the car actually drives on untagged. Tagging by
    /// parentage instead of by name means a road piece is tarmac because it is a road, not because
    /// someone spelt its name the way the regex expected.
    ///
    /// ROAD COLLIDERS
    /// The rule only works if the wheels can touch the roads. In Zubuhle's city, New Cityc, the road
    /// pieces under Roads are drawn but had no colliders, so the car drove on the single ground mesh
    /// underneath them, which is lawn from one side of the city to the other. Road and grass were the
    /// same collider, every wheel read the untagged default of tarmac, and the dash worked on the
    /// grass.
    ///
    /// So each drawn road piece gets a child, "Road surface (collider)", holding a MeshCollider made
    /// from the piece's own mesh, squashed flat 2 cm above the drawn carriageway. Flat on purpose:
    /// the road pieces sit on top of the lawn, so their drawn kerbs stand 16 to 28 cm above it, and
    /// the van's body clears the ground by about 28 cm. Solid kerbs were tried first and the van got
    /// stuck climbing from the lawn back onto a curved piece. A flat surface keeps the driving as it
    /// was, a step of a few centimetres between lawn and road, and only adds what the wheels read.
    /// Running the tagger again skips pieces that already have one.
    ///
    /// WHAT THE CAR ACTUALLY HITS
    /// The wheel rays return a collider, and the car walks UP from that collider with
    /// GetComponentInParent looking for a SurfaceType. So one SurfaceType on "Roads" covers every
    /// road piece beneath it, and grass only has to be put on colliders that are NOT beneath "Roads".
    ///
    /// REACHABLE GROUND ONLY
    /// A collider is only tagged grass if somewhere on it, it is the highest ground straight down
    /// from the sky, which is where a wheel would touch it. The road kit's spare tiles parked 48 m
    /// under the city fail that test and are left alone. Tagging them would have meant a tile copied
    /// out of that kit and into the city brought a Grass tag with it.
    ///
    /// THE SAFETY CHECK
    /// "Everything else is grass" is only safe when "Roads" exists and its children are what the car
    /// drives on. With no drivable Roads parent, "everything else" is the ground under the roads too,
    /// and the whole city would become grass with no dash anywhere. So the tagger refuses to run in
    /// that case, saying exactly what is missing.
    ///
    /// EXCLUDED FROM GRASS
    /// Props with a Rigidbody (knockables), triggers, inactive objects, anything too tall or thin to
    /// stand on (lamps, trees, walls), the car itself, and anything under the VehicleLab scaffolding.
    ///
    /// VERIFY
    /// After tagging, every tagged collider is checked at a spot where it is the top surface, using
    /// the same lookup the wheels use. A tag the car cannot read there is reported as a failure.
    ///
    /// Tools > Zoom Zoom > Tag City Surfaces
    ///
    /// From the command line, with the editor closed:
    ///   Unity.exe -batchmode -projectPath . -executeMethod
    ///     ZoomZoom.Vehicle.EditorTools.SurfaceTagger.TagFromCommandLine
    ///     -tagScene Assets/Project/Scenes/Medium.unity [-dryRun] [-tagReport file.txt]
    /// </summary>
    public static class SurfaceTagger
    {
        private static readonly Regex RoadsParentName = new Regex(@"^Roads$", RegexOptions.IgnoreCase);

        // Objects that are never ground, whatever they collide with.
        private static readonly Regex NeverGround = new Regex(
            @"^(Car|Chase Camera|Measurement|VehicleLab|Generated .*|Skid marks|OrderBeacon|Delivery Points|EventSystem)$",
            RegexOptions.IgnoreCase);

        // A collider is only GROUND if the car could stand on it. Reading Medium's scene: of the
        // 3,043 static colliders outside Roads, 616 are streetlamps, 136 traffic lights, 400-odd
        // trees, plus mailboxes, bus stops and fences. None of those is something a wheel ray lands
        // on, and stamping Grass on every lamp post would make the report say "3,043 grass" when the
        // truth is a couple of hundred slabs. So: wide and flat, not tall and thin.
        private const float MinimumFootprintMetres = 3f;   // each horizontal side at least this
        private const float MaximumAspect = 1.0f;          // height no greater than the smaller side

        // Colliders taller than this are domes and sky shells, not something to drop rays from above.
        private const float TallestSensibleCollider = 200f;

        // The road surface sits at most a few centimetres above the old ground. A prop still stuck
        // after rising this far is not resting on the road, and is put back and reported instead.
        private const float MaximumPropLift = 0.4f;

        /// <summary>Name of the flat collider child the tagger gives each road piece.</summary>
        public const string RoadSurfaceName = "Road surface (collider)";

        // How much of the piece's height the flat collider keeps. A 25 cm kerb becomes 0.25 mm.
        private const float FlattenScale = 0.001f;

        // How far the flat surface sits above the drawn carriageway, in metres. Measured in Medium:
        // on about 1% of the road the lawn mesh comes up through the drawn tarmac by 1 to 1.5 cm,
        // so with no margin the lawn wins in those patches and a wheel on the road reads grass.
        // 2 cm covers all of it, and lifting further to 5 cm changes nothing. Too small to see.
        private const float RoadLift = 0.02f;

        // The share of the drawn road the wheels must read as road for the run to pass.
        private const float MinimumRoadShare = 99.5f;

        private static readonly Dictionary<Collider, bool> GroundCache = new Dictionary<Collider, bool>();

        [MenuItem("Tools/Zoom Zoom/Tag City Surfaces", priority = 62)]
        public static void Tag()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[SurfaceTagger] No saved scene is open.");
                return;
            }

            Run(scene, true, out _);
        }

        /// <summary>For -executeMethod. Needs -tagScene; optional -dryRun and -tagReport.</summary>
        public static void TagFromCommandLine()
        {
            bool ok = false;
            string report = null;

            try
            {
                string path = ArgumentAfter("-tagScene");
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogError("[SurfaceTagger] Pass -tagScene followed by the scene's path.");
                }
                else
                {
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    ok = Run(scene, !HasArgument("-dryRun"), out report);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            string reportPath = ArgumentAfter("-tagReport");
            if (!string.IsNullOrEmpty(reportPath) && report != null) File.WriteAllText(reportPath, report);

            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Tags the scene, saving it when <paramref name="save"/> is set. Returns true when it ran
        /// and every check passed. A dry run leaves the changes in memory only.
        /// </summary>
        public static bool Run(Scene scene, bool save, out string report)
        {
            GroundCache.Clear();
            GameObject[] roots = scene.GetRootGameObjects();

            // ---- 1. Find "Roads". -----------------------------------------------------------------
            var roadsRoots = new List<Transform>();
            foreach (GameObject root in roots)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (RoadsParentName.IsMatch(t.name)) roadsRoots.Add(t);
            var roadsSet = new HashSet<Transform>(roadsRoots);

            Undo.SetCurrentGroupName("Tag City Surfaces");
            int group = Undo.GetCurrentGroup();

            // ---- 2. Give the drawn road pieces a flat surface the wheels can touch. ---------------
            int collidersAdded = 0;
            var unmeasured = new List<string>();
            foreach (Transform roads in roadsRoots)
            {
                if (!roads.gameObject.activeInHierarchy) continue;

                foreach (MeshFilter filter in roads.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                    if (filter.transform.Find(RoadSurfaceName) != null) continue;

                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled) continue;

                    if (!CarriagewayHeight(filter, out float height))
                    {
                        unmeasured.Add(filter.name);
                        continue;
                    }

                    var surface = new GameObject(RoadSurfaceName) { layer = filter.gameObject.layer };
                    Undo.RegisterCreatedObjectUndo(surface, "road surface collider");
                    surface.transform.SetParent(filter.transform, false);
                    // The lift is in metres in the world, so it is divided by the piece's own
                    // vertical scale to land the same distance up on every piece.
                    float scaleY = Mathf.Max(0.0001f, Mathf.Abs(filter.transform.lossyScale.y));
                    surface.transform.localPosition = new Vector3(0f, height + RoadLift / scaleY, 0f);
                    surface.transform.localRotation = Quaternion.identity;
                    surface.transform.localScale = new Vector3(1f, FlattenScale, 1f);
                    surface.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    collidersAdded++;
                }
            }

            Physics.SyncTransforms();

            // ---- 3. Refuse unless the car can drive on something under Roads. ---------------------
            int drivableRoads = 0;
            foreach (Transform roads in roadsRoots)
                foreach (Collider c in roads.GetComponentsInChildren<Collider>(false))
                    if (IsHittableGround(c) && IsStandable(c)) drivableRoads++;

            if (drivableRoads == 0)
            {
                Undo.RevertAllDownToGroup(group);

                string why = roadsRoots.Count == 0
                    ? "there is no object named \"Roads\" in this scene"
                    : "no active \"Roads\" parent has road pieces the car could drive on";
                report =
                    $"[SurfaceTagger] REFUSED to tag '{scene.name}': {why}.\n" +
                    "The rule is 'Roads is tarmac, everything else is grass'. Without drivable roads under a " +
                    "Roads parent, 'everything else' includes the ground under the roads, and the whole city " +
                    "would become grass with no dash anywhere. Nothing was changed.\n" +
                    "Fix: put the road pieces under an active parent named \"Roads\", then run again. The " +
                    "tagger gives them colliders itself.";
                Debug.LogError(report);
                return false;
            }

            // ---- 4. Tarmac. -----------------------------------------------------------------------
            foreach (Transform roads in roadsRoots) SetKind(roads, SurfaceKind.Tarmac);

            // ---- 5. Grass on reachable ground that has no tag yet. --------------------------------
            int mask = GroundMask(scene);
            float sky = SkyHeight(roots);
            var grass = new List<Collider>();
            var kept = new Dictionary<SurfaceKind, int>();
            int underRoads = 0, inactive = 0, props = 0, never = 0, notFlat = 0, covered = 0;

            foreach (GameObject root in roots)
            {
                foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                {
                    Transform t = c.transform;
                    if (IsUnderAny(t, roadsSet)) { underRoads++; continue; }
                    if (!c.gameObject.activeInHierarchy) { inactive++; continue; }
                    if (!IsHittableGround(c))
                    {
                        if (c.attachedRigidbody != null) props++;
                        continue;
                    }
                    if (IsNeverGround(t)) { never++; continue; }
                    if (!IsStandable(c)) { notFlat++; continue; }

                    // A tag already in place wins over the default.
                    SurfaceType existing = c.GetComponentInParent<SurfaceType>();
                    if (existing != null)
                    {
                        kept.TryGetValue(existing.kind, out int n);
                        kept[existing.kind] = n + 1;
                        continue;
                    }

                    if (!TopSpot(c, mask, sky, out _)) { covered++; continue; }

                    // Grass goes on the collider OWNER. A parent tag would also catch any road
                    // piece that happens to sit under it, so we tag at the leaf and never above.
                    SetKind(t, SurfaceKind.Grass);
                    grass.Add(c);
                }
            }

            // Weather switch, present and Dry.
            bool addedWeather = false;
            if (Object.FindAnyObjectByType<WeatherState>() == null)
            {
                var go = new GameObject("Weather");
                Undo.RegisterCreatedObjectUndo(go, "add weather state");
                go.AddComponent<WeatherState>();
                addedWeather = true;
            }

            // ---- 6. Knockable props the new kerbs would start inside. -----------------------------
            // A Rigidbody that starts inside a collider is shoved out on the first physics step, so a
            // road blocker standing on the old ground inside the new kerb would jump on Play. Lift it
            // onto the pavement instead. Sideways overlaps are only reported, never moved.
            var lifted = new List<string>();
            var stillInside = new List<string>();
            foreach (Rigidbody body in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (body.gameObject.scene != scene || body.GetComponent<VehicleController>() != null) continue;

                Vector3 push = RoadOverlap(body, roadsSet);
                if (push.magnitude <= 0.01f) continue;

                // Straight up only, a step at a time, until it is clear or has risen higher than any
                // kerb. A prop straddling a kerb edge gets pushed sideways by the solver, so the
                // vertical part of one push is not always enough on its own.
                Undo.RecordObject(body.transform, "lift prop out of road");
                Vector3 start = body.transform.position;
                for (int step = 0; step < 12 && push.magnitude > 0.01f; step++)
                {
                    float rise = body.transform.position.y - start.y;
                    if (rise > MaximumPropLift) break;

                    body.transform.position += Vector3.up * (Mathf.Max(push.magnitude, 0.02f) + 0.005f);
                    Physics.SyncTransforms();
                    push = RoadOverlap(body, roadsSet);
                }

                if (push.magnitude > 0.01f)
                {
                    body.transform.position = start;
                    Physics.SyncTransforms();
                    stillInside.Add(body.name);
                }
                else
                {
                    lifted.Add($"{body.name} by {(body.transform.position.y - start.y) * 100f:0} cm");
                }
            }

            Undo.CollapseUndoOperations(group);
            bool saved = false;
            if (save)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                saved = EditorSceneManager.SaveScene(scene);
            }

            // ---- 7. Verify, at a spot where each collider is the top surface. ---------------------
            Physics.SyncTransforms();
            GroundCache.Clear();
            int roadChecked = 0, roadOk = 0, roadCovered = 0, grassOk = 0;
            var failed = new List<string>();

            foreach (Transform roads in roadsRoots)
            {
                if (!roads.gameObject.activeInHierarchy) continue;

                foreach (Collider c in roads.GetComponentsInChildren<Collider>(false))
                {
                    if (!IsHittableGround(c)) continue;
                    roadChecked++;
                    if (!TopSpot(c, mask, sky, out _)) { roadCovered++; continue; }

                    SurfaceKind read = KindRead(c);
                    if (read == SurfaceKind.Tarmac) roadOk++;
                    else failed.Add($"{PathOf(c.transform)}: road reads {read}");
                }
            }

            foreach (Collider c in grass)
            {
                SurfaceKind read = KindRead(c);
                if (read == SurfaceKind.Grass) grassOk++;
                else failed.Add($"{PathOf(c.transform)}: tagged Grass, reads {read}");
            }

            // How much of the drawn road the wheels would actually read as road. A piece can pass
            // the check above and still have lawn poking up through part of it.
            int footprint = 0, footprintRoad = 0;
            foreach (Transform roads in roadsRoots)
            {
                if (!roads.gameObject.activeInHierarchy) continue;

                foreach (Collider c in roads.GetComponentsInChildren<Collider>(false))
                {
                    if (!IsHittableGround(c)) continue;
                    Bounds b = c.bounds;
                    const int steps = 8;
                    for (int i = 0; i < steps; i++)
                    {
                        for (int k = 0; k < steps; k++)
                        {
                            float x = Mathf.Lerp(b.min.x, b.max.x, (i + 0.5f) / steps);
                            float z = Mathf.Lerp(b.min.z, b.max.z, (k + 0.5f) / steps);
                            if (!c.Raycast(new Ray(new Vector3(x, b.max.y + 1f, z), Vector3.down), out _, b.size.y + 2f)) continue;

                            footprint++;
                            if (HighestGround(new Vector3(x, sky, z), mask, sky - b.min.y + 2f, out RaycastHit top) &&
                                IsUnderAny(top.collider.transform, roadsSet))
                                footprintRoad++;
                        }
                    }
                }
            }

            float roadShare = footprint > 0 ? 100f * footprintRoad / footprint : 0f;
            if (footprint > 0 && roadShare < MinimumRoadShare)
                failed.Add($"only {roadShare:0.0}% of the drawn road reads as road");

            string spawn = "no car in the scene";
            VehicleController car = FindCar(scene);
            if (car != null)
            {
                Vector3 at = car.transform.position;
                if (!HighestGround(at + Vector3.up * 3f, mask, 10f, out RaycastHit hit))
                {
                    spawn = "nothing under it";
                }
                else
                {
                    SurfaceKind kind = KindRead(hit.collider);
                    spawn = $"{kind} ({hit.collider.name})";
                    if (kind != SurfaceKind.Tarmac)
                        spawn += NearestRoad(at, mask, sky, out float metres)
                            ? $", the nearest road is {metres:0} m away"
                            : ", with no road within 80 m";
                }
            }

            // ---- 8. Report. -----------------------------------------------------------------------
            var sb = new StringBuilder($"[SurfaceTagger] {scene.name}{(save ? "" : " (dry run, not saved)")}\n");
            sb.Append($"   ROADS   {collidersAdded} road pieces given a flat surface collider; {drivableRoads} drivable road colliders under " +
                      $"{roadsRoots.Count} \"Roads\" parent(s), tagged Tarmac\n");
            if (unmeasured.Count > 0)
                failed.Add($"{unmeasured.Count} road pieces had no surface to measure: {string.Join(", ", unmeasured.GetRange(0, Mathf.Min(6, unmeasured.Count)))}");
            sb.Append($"   GRASS   {grass.Count} colliders tagged\n");
            sb.Append($"   KEPT    existing tags: {(kept.Count == 0 ? "none" : string.Join(", ", Describe(kept)))}\n");
            sb.Append($"   skipped {underRoads} under Roads, {inactive} inactive, {notFlat} too tall or narrow to stand on, " +
                      $"{covered} never the top surface, {props} Rigidbody props, {never} non-ground\n");
            sb.Append($"   VERIFY  roads {roadOk}/{roadChecked - roadCovered} read Tarmac ({roadCovered} always covered), " +
                      $"{roadShare:0.0}% of the drawn road reads as road; grass {grassOk}/{grass.Count} read Grass\n");
            sb.Append($"   SPAWN   the car starts on {spawn}\n");
            sb.Append(lifted.Count == 0
                ? "   PROPS   no knockable prop needed lifting out of a road piece\n"
                : $"   PROPS   lifted {lifted.Count} knockable props onto the pavement: {string.Join(", ", lifted)}\n");
            if (stillInside.Count > 0)
                failed.Add($"{stillInside.Count} props still start inside a road piece: {string.Join(", ", stillInside)}");
            sb.Append(addedWeather ? "   Weather object added, Dry.\n" : "   WeatherState already present.\n");
            sb.Append(!save ? "   Dry run: nothing saved." : saved ? "   Scene saved." : "   SCENE NOT SAVED: press Ctrl+S or the change is lost.");

            if (failed.Count > 0)
            {
                sb.Append($"\n{failed.Count} tags the car does NOT read:");
                foreach (string f in failed.GetRange(0, Mathf.Min(8, failed.Count))) sb.Append("\n   " + f);
            }

            report = sb.ToString();
            if (failed.Count > 0) Debug.LogWarning(report);
            else Debug.Log(report);

            return failed.Count == 0 && (!save || saved);
        }

        // ==================================================================
        // GROUND TESTS
        // ==================================================================

        private static void SetKind(Transform t, SurfaceKind kind)
        {
            SurfaceType s = t.GetComponent<SurfaceType>();
            if (s == null) s = Undo.AddComponent<SurfaceType>(t.gameObject);
            else Undo.RecordObject(s, "retag surface");
            s.kind = kind;
            s.ApplyDefaultsForKind();
        }

        /// <summary>A collider the car's wheel ray can hit at all.</summary>
        private static bool IsHittableGround(Collider c)
        {
            if (c == null || !c.enabled || c.isTrigger) return false;
            if (c.attachedRigidbody != null) return false;             // props, the car
            if (c is MeshCollider mc && mc.sharedMesh == null) return false; // Roads_Parent's empty collider
            return true;
        }

        private static bool IsStandable(Collider c)
        {
            Bounds b = c.bounds;
            float w = b.size.x, h = b.size.y, d = b.size.z;
            if (w < MinimumFootprintMetres || d < MinimumFootprintMetres) return false; // a post
            if (h > Mathf.Min(w, d) * MaximumAspect) return false;                       // a wall or a tree
            return true;
        }

        /// <summary>Ground for the "highest surface" test: something a wheel could be standing on.</summary>
        private static bool IsGround(Collider c)
        {
            if (GroundCache.TryGetValue(c, out bool ground)) return ground;
            ground = c.gameObject.activeInHierarchy && IsHittableGround(c) && !IsNeverGround(c.transform) && IsStandable(c);
            GroundCache[c] = ground;
            return ground;
        }

        /// <summary>
        /// Finds a spot where this collider is the highest ground straight down from the sky, which
        /// is where a wheel would touch it. Lamps, trees and walls are looked through, because they
        /// are not ground.
        /// </summary>
        private static bool TopSpot(Collider c, int mask, float sky, out Vector3 point)
        {
            Bounds b = c.bounds;
            int nx = Mathf.Clamp(Mathf.CeilToInt(b.size.x / 2f), 2, 16);
            int nz = Mathf.Clamp(Mathf.CeilToInt(b.size.z / 2f), 2, 16);
            float length = sky - b.min.y + 1f;

            for (int i = 0; i < nx; i++)
            {
                for (int k = 0; k < nz; k++)
                {
                    var from = new Vector3(
                        Mathf.Lerp(b.min.x, b.max.x, (i + 0.5f) / nx), sky,
                        Mathf.Lerp(b.min.z, b.max.z, (k + 0.5f) / nz));

                    if (HighestGround(from, mask, length, out RaycastHit hit) && hit.collider == c)
                    {
                        point = hit.point;
                        return true;
                    }
                }
            }

            point = default;
            return false;
        }

        private static bool HighestGround(Vector3 from, int mask, float length, out RaycastHit best)
        {
            best = default;
            bool found = false;
            foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, length, mask, QueryTriggerInteraction.Ignore))
            {
                if (!IsGround(h.collider)) continue;
                if (!found || h.distance < best.distance)
                {
                    best = h;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// The height of the carriageway in the piece's own coordinates: the lowest tenth of its top
        /// surface, so the raised kerbs and islands do not count. Measured with a temporary collider
        /// on the drawn mesh, which is removed again before anything is saved.
        /// </summary>
        private static bool CarriagewayHeight(MeshFilter filter, out float localHeight)
        {
            localHeight = 0f;

            // Flattening along local Y is only right when local Y is the piece's up: its thinnest
            // side, and pointing roughly upwards. A model imported lying on its side fails this and
            // is reported instead of being squashed the wrong way.
            Bounds meshBounds = filter.sharedMesh.bounds;
            if (meshBounds.size.y > Mathf.Min(meshBounds.size.x, meshBounds.size.z)) return false;
            if (Vector3.Dot(filter.transform.up, Vector3.up) < 0.5f) return false;

            var probe = filter.gameObject.AddComponent<MeshCollider>();
            try
            {
                probe.sharedMesh = filter.sharedMesh;
                Physics.SyncTransforms();

                Bounds b = probe.bounds;
                var heights = new List<float>();
                const int steps = 12;
                for (int i = 0; i < steps; i++)
                {
                    for (int k = 0; k < steps; k++)
                    {
                        var from = new Vector3(
                            Mathf.Lerp(b.min.x, b.max.x, (i + 0.5f) / steps), b.max.y + 1f,
                            Mathf.Lerp(b.min.z, b.max.z, (k + 0.5f) / steps));
                        if (probe.Raycast(new Ray(from, Vector3.down), out RaycastHit hit, b.size.y + 2f))
                            heights.Add(filter.transform.InverseTransformPoint(hit.point).y);
                    }
                }

                if (heights.Count == 0) return false;
                heights.Sort();
                localHeight = heights[heights.Count / 10];
                return true;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>The surface the car reads after its wheel ray hits this collider.</summary>
        private static SurfaceKind KindRead(Collider c)
        {
            SurfaceType s = c.GetComponentInParent<SurfaceType>();
            return s != null ? s.kind : SurfaceKind.Tarmac;
        }

        /// <summary>
        /// The deepest push that would move this body's colliders out of a road piece, or zero when
        /// none of them overlaps one.
        /// </summary>
        private static Vector3 RoadOverlap(Rigidbody body, HashSet<Transform> roadsSet)
        {
            Vector3 deepest = Vector3.zero;
            foreach (Collider own in body.GetComponentsInChildren<Collider>())
            {
                if (own.isTrigger || !own.enabled) continue;
                Bounds b = own.bounds;

                foreach (Collider other in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0,
                             QueryTriggerInteraction.Ignore))
                {
                    if (!IsUnderAny(other.transform, roadsSet)) continue;
                    if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                            other, other.transform.position, other.transform.rotation,
                            out Vector3 direction, out float distance) && distance > deepest.magnitude)
                        deepest = direction * distance;
                }
            }

            return deepest;
        }

        /// <summary>How far it is from this point to the nearest spot where the car would read tarmac.</summary>
        private static bool NearestRoad(Vector3 from, int mask, float sky, out float metres)
        {
            for (float r = 1f; r <= 80f; r += 1f)
            {
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI * 2f / 32f;
                    var probe = new Vector3(from.x + Mathf.Cos(a) * r, sky, from.z + Mathf.Sin(a) * r);
                    if (HighestGround(probe, mask, sky - from.y + 20f, out RaycastHit hit) &&
                        KindRead(hit.collider) == SurfaceKind.Tarmac &&
                        hit.collider.GetComponentInParent<SurfaceType>() != null)
                    {
                        metres = r;
                        return true;
                    }
                }
            }

            metres = 0f;
            return false;
        }

        // ==================================================================
        // SCENE HELPERS
        // ==================================================================

        private static VehicleController FindCar(Scene scene)
        {
            foreach (VehicleController car in Object.FindObjectsByType<VehicleController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (car.gameObject.scene == scene) return car;
            return null;
        }

        private static int GroundMask(Scene scene)
        {
            VehicleController car = FindCar(scene);
            return car != null && car.Tuning != null ? car.Tuning.groundMask.value : ~0;
        }

        /// <summary>A height above everything in the level, for dropping rays from.</summary>
        private static float SkyHeight(GameObject[] roots)
        {
            float top = 10f;
            foreach (GameObject root in roots)
            {
                if (!root.activeInHierarchy) continue;
                foreach (Collider c in root.GetComponentsInChildren<Collider>(false))
                {
                    if (!IsHittableGround(c)) continue;
                    Bounds b = c.bounds;
                    if (b.size.y > TallestSensibleCollider) continue;
                    top = Mathf.Max(top, b.max.y);
                }
            }

            return top + 5f;
        }

        private static bool IsUnderAny(Transform t, HashSet<Transform> roots)
        {
            for (Transform p = t; p != null; p = p.parent)
                if (roots.Contains(p)) return true;
            return false;
        }

        private static bool IsNeverGround(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
                if (NeverGround.IsMatch(p.name)) return true;
            return false;
        }

        private static IEnumerable<string> Describe(Dictionary<SurfaceKind, int> counts)
        {
            foreach (KeyValuePair<SurfaceKind, int> kv in counts) yield return $"{kv.Value} {kv.Key}";
        }

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform p = t; p != null; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string ArgumentAfter(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static bool HasArgument(string flag)
        {
            foreach (string a in Environment.GetCommandLineArgs())
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
