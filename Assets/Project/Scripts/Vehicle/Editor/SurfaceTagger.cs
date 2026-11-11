using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZoomZoom.Vehicle.EditorTools
{
    /// <summary>
    /// Labels the city's ground so the car knows what it is driving on.
    ///
    /// THE RULE
    /// Everything under the object named "Roads" is tarmac. Every other static collider the car can
    /// land on is grass. That is the whole rule, and it replaced a name-pattern table because the
    /// table kept being wrong: it tagged Roads_Parent, whose MeshCollider has no mesh and cannot be
    /// hit, and it left the Floor the car actually drives on untagged. Tagging by parentage instead
    /// of by name means a road piece is tarmac because it is a road, not because someone spelt its
    /// name the way the regex expected.
    ///
    /// WHAT THE CAR ACTUALLY HITS
    /// The wheel rays return a collider, and the car walks UP from that collider with
    /// GetComponentInParent looking for a SurfaceType. So one SurfaceType on "Roads" covers every
    /// road piece beneath it that owns a collider, and grass only has to be put on colliders that
    /// are NOT beneath "Roads". Children with no collider of their own are skipped: they inherit
    /// from above anyway, and tagging them would be a false count.
    ///
    /// THE SAFETY CHECK
    /// "Everything else is grass" is only safe when "Roads" exists and its children are what the car
    /// drives on. In a scene with no collidable Roads parent, the car drives on a Floor, and the rule
    /// would make that Floor grass - the whole city becomes grass and no dash fires anywhere. So the
    /// tagger counts hittable colliders under Roads first and refuses to run if there are none,
    /// saying exactly what is missing. A tool that quietly turns the city into a lawn is worse than
    /// one that stops and explains.
    ///
    /// EXCLUDED FROM GRASS
    /// Props with a Rigidbody (knockables), triggers, the car itself, and anything under the
    /// VehicleLab scaffolding. Those are not ground.
    ///
    /// VERIFY
    /// After tagging, the car's own ground ray is fired at every tagged object and the kind that
    /// comes back is compared with the kind that was set. A tag the ray cannot see is not a tag.
    ///
    /// Tools > Zoom Zoom > Tag City Surfaces
    /// </summary>
    public static class SurfaceTagger
    {
        private static readonly Regex RoadsParentName = new Regex(@"^Roads$", RegexOptions.IgnoreCase);

        // Objects that are never ground, whatever they collide with.
        private static readonly Regex NeverGround = new Regex(
            @"^(Car|Chase Camera|Measurement|VehicleLab|Generated .*|Skid marks|OrderBeacon|Delivery Points|EventSystem)$",
            RegexOptions.IgnoreCase);

        [MenuItem("Tools/Zoom Zoom/Tag City Surfaces", priority = 62)]
        public static void Tag()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[SurfaceTagger] No saved scene is open.");
                return;
            }

            // ---- 1. Find "Roads" and prove the car can land on something beneath it. -------------
            var roadsRoots = new List<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (RoadsParentName.IsMatch(t.name)) roadsRoots.Add(t);

            int hittableUnderRoads = 0;
            foreach (Transform roads in roadsRoots)
                foreach (Collider c in roads.GetComponentsInChildren<Collider>(true))
                    if (IsHittableGround(c) && IsStandable(c)) hittableUnderRoads++;

            if (roadsRoots.Count == 0 || hittableUnderRoads == 0)
            {
                string why = roadsRoots.Count == 0
                    ? "there is no object named \"Roads\" in this scene"
                    : $"\"Roads\" exists but none of its {roadsRoots[0].GetComponentsInChildren<Collider>(true).Length} colliders can be driven on";
                Debug.LogError(
                    $"[SurfaceTagger] REFUSED to tag '{scene.name}': {why}.\n" +
                    "The rule is 'Roads is tarmac, everything else is grass'. Without a collidable Roads " +
                    "parent, 'everything else' is the ground the car drives on, and the whole city would " +
                    "become grass with no dash anywhere. Nothing was changed.\n" +
                    "Fix: give this scene a parent named \"Roads\" whose children carry the road colliders " +
                    "(Medium has one; Hard has only Roads_Parent, whose collider has no mesh), then run again.");
                return;
            }

            // ---- 2. Tag. --------------------------------------------------------------------------
            Undo.SetCurrentGroupName("Tag City Surfaces");
            int group = Undo.GetCurrentGroup();

            var tagged = new List<(Transform t, SurfaceKind kind)>();
            var roadsSet = new HashSet<Transform>(roadsRoots);

            foreach (Transform roads in roadsRoots)
            {
                SetKind(roads, SurfaceKind.Tarmac);
                tagged.Add((roads, SurfaceKind.Tarmac));
            }

            int grassSkippedUnderRoads = 0, grassSkippedNeverGround = 0, grassSkippedProp = 0, grassSkippedNotFlat = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                {
                    Transform t = c.transform;
                    if (IsUnderAny(t, roadsSet)) { grassSkippedUnderRoads++; continue; }
                    if (!IsHittableGround(c))
                    {
                        if (c.attachedRigidbody != null) grassSkippedProp++;
                        continue;
                    }
                    if (IsNeverGround(t)) { grassSkippedNeverGround++; continue; }
                    if (!IsStandable(c)) { grassSkippedNotFlat++; continue; }

                    // Grass goes on the collider OWNER. A parent tag would also catch any road
                    // piece that happens to sit under it, so we tag at the leaf and never above.
                    SetKind(t, SurfaceKind.Grass);
                    tagged.Add((t, SurfaceKind.Grass));
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

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            // ---- 3. Verify with the car's own ray. ------------------------------------------------
            Physics.SyncTransforms();
            int tarmacVerified = 0, grassVerified = 0, grassTagged = 0;
            var failed = new List<string>();
            foreach ((Transform t, SurfaceKind kind) in tagged)
            {
                if (kind == SurfaceKind.Grass) grassTagged++;
                Bounds b = WorldBounds(t);
                var from = new Vector3(b.center.x, b.max.y + 5f, b.center.z);
                SurfaceKind? seen = null;
                foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.attachedRigidbody != null) continue;
                    if (!h.collider.transform.IsChildOf(t) && h.collider.transform != t) continue;
                    var s = h.collider.GetComponentInParent<SurfaceType>();
                    seen = s != null ? s.kind : SurfaceKind.Tarmac;
                    break;
                }
                if (seen == kind) { if (kind == SurfaceKind.Tarmac) tarmacVerified++; else grassVerified++; }
                else failed.Add($"{t.name}: tagged {kind}, ray saw {(seen.HasValue ? seen.Value.ToString() : "nothing")}");
            }

            // ---- 4. Report. -----------------------------------------------------------------------
            var sb = new StringBuilder($"[SurfaceTagger] {scene.name}\n");
            sb.Append($"   TARMAC  {roadsRoots.Count} \"Roads\" parent(s) tagged, covering {hittableUnderRoads} drivable colliders; {tarmacVerified}/{roadsRoots.Count} verified by the car's ray\n");
            sb.Append($"   GRASS   {grassTagged} colliders tagged; {grassVerified}/{grassTagged} verified by the car's ray\n");
            sb.Append($"   skipped {grassSkippedUnderRoads} under Roads (inherit tarmac), {grassSkippedNotFlat} too tall or narrow to stand on (lamps, trees, fences), {grassSkippedProp} Rigidbody props, {grassSkippedNeverGround} non-ground\n");
            sb.Append(addedWeather ? "   Weather object added, Dry.\n" : "   WeatherState already present.\n");
            sb.Append(saved ? "   Scene saved." : "   SCENE NOT SAVED: press Ctrl+S or the change is lost on Play.");
            if (failed.Count > 0)
            {
                sb.Append($"\n{failed.Count} tags the car's ray does NOT see:");
                foreach (string f in failed.GetRange(0, Mathf.Min(8, failed.Count))) sb.Append("\n   " + f);
                Debug.LogWarning(sb.ToString());
            }
            else Debug.Log(sb.ToString());
        }

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

        // A collider is only GROUND if the car could stand on it. Reading Medium's scene: of the
        // 3,043 static colliders outside Roads, 616 are streetlamps, 136 traffic lights, 400-odd
        // trees, plus mailboxes, bus stops and fences. None of those is something a wheel ray lands
        // on, and stamping Grass on every lamp post would make the report say "3,043 grass" when the
        // truth is a couple of hundred slabs. So: wide and flat, not tall and thin.
        private const float MinimumFootprintMetres = 3f;   // each horizontal side at least this
        private const float MaximumAspect = 1.0f;          // height no greater than the smaller side

        private static bool IsStandable(Collider c)
        {
            Bounds b = c.bounds;
            float w = b.size.x, h = b.size.y, d = b.size.z;
            if (w < MinimumFootprintMetres || d < MinimumFootprintMetres) return false; // a post
            if (h > Mathf.Min(w, d) * MaximumAspect) return false;                       // a wall or a tree
            return true;
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

        private static Bounds WorldBounds(Transform t)
        {
            var cols = t.GetComponentsInChildren<Collider>();
            if (cols.Length == 0) return new Bounds(t.position, Vector3.one);
            Bounds b = cols[0].bounds;
            foreach (Collider c in cols) b.Encapsulate(c.bounds);
            return b;
        }
    }
}
