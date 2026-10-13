using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZoomZoom.Vehicle.EditorTools
{
    /// <summary>
    /// Turns chosen street props in the open scene into things the car can knock over.
    ///
    /// WHY A TOOL AND NOT THE INSPECTOR
    /// The city came in as flat geometry, not prefabs: about 950 props as plain GameObjects named
    /// things like trashcontainer01.004. Hand-editing even the few dozen in this first pass is an
    /// afternoon of clicking; doing it again after the next re-export is another. The tool makes
    /// the decision once, by name, and the scene can be re-baked in a second.
    ///
    /// WHY EACH PROP IS PULLED OUT OF ITS PARENT
    /// This is the part the first two versions got wrong, and the reason the props did not move.
    /// The city FBX was authored in centimetres. Its group nodes compensate with scales like 267
    /// and -426, and the props under them have local scales like 0.147 or -0.293, so a bin's
    /// WORLD scale is (255, 141, 255): huge, non-uniform, and reached through a negative parent.
    /// PhysX cannot run a Rigidbody in that hierarchy. Unity's own manual says never to scale the
    /// parent of a rigidbody, BoxCollider refuses negative scale and forces it positive with
    /// "unexpected collision geometry", and non-uniform parent scale shears any rotated child. The
    /// bodies existed; they could not behave.
    ///
    /// So each prop is reparented to the scene root with its world position and rotation kept and
    /// its scale made positive on every axis (magnitudes preserved, so the mesh still renders at
    /// its real size). Non-uniform scale on the body's own transform is fine; it is the PARENT
    /// scale PhysX objects to. The box is fitted to the renderer's measured WORLD bounds, taken
    /// before anything moves, so it is the size the player sees whatever the import did. The stale
    /// MeshCollider is disabled and kept so the bake stays reversible, and the original parent is
    /// recorded on the marker so Undo Knockable Props can put the object back.
    ///
    /// WHY SOME MATCHES ARE SKIPPED
    /// The scene has whole groups of props 84 m below the map, never placed. Every cone_type in
    /// the file is one of them; the cone-shaped things actually on the road are RoadBlocker04.
    /// Giving a body to an object under the floor does nothing a player can see and wastes a
    /// physics slot, so anything below MinimumWorldY is left alone and counted as skipped.
    ///
    /// MASS AND SIZE ARE SEPARATE DECISIONS
    /// The city is modelled two to five times larger than the car and is not being resized. So the
    /// collider is fitted to the mesh at whatever size it is, and the mass is a fixed feel number
    /// per family chosen so the 900 kg car always wins: bins 60 kg, tables 120, toilets 180,
    /// blockers 25. The inertia tensor is capped to that of a compact object so a 9 m table turns
    /// like a 2 m one instead of swinging round as a 9 m lever. Walls and buildings stay static.
    ///
    /// THE TOOL SAVES THE SCENE
    /// Pressing Play snapshots the saved scene and leaving Play restores it. A bake that is only
    /// marked dirty is undone by the first test drive. This writes the file.
    ///
    /// Tools > Zoom Zoom > Make Props Knockable      bakes the selected families in the open scene
    /// Tools > Zoom Zoom > Undo Knockable Props      strips what the baker added and reparents back
    /// </summary>
    public static class KnockablePropBaker
    {
        private struct Family
        {
            public string Label;
            public Regex Name;
            /// <summary>Mass in kilograms, fixed, whatever size the mesh is. This is a FEEL number
            /// against the car's 900 kg, not a physics derivation: it answers "does the car move
            /// it, and how much does it resist", and nothing else.</summary>
            public float MassKg;
        }

        // The playtest set. Lamps, fences, traffic lights, bus stops and mailboxes are deliberately
        // NOT here yet: if this lot feels right the list grows, if the car gets caught on tumbling
        // furniture it shrinks. One line per family.
        //
        // WHY MASS IS NOT DERIVED FROM SIZE
        // An earlier version scaled mass by the cube of the measured size, on the argument that a
        // 9 m table should weigh what a 9 m table weighs. It produced a 4 tonne table the car
        // bounced off, which is physically right and the wrong game: the request was "things move
        // when the car hits them", the city is built two to five times larger than the car and is
        // not going to be rebuilt this week, and nobody playing a delivery game checks whether the
        // furniture obeys the cube law. So mass is a plain feel number per family, chosen so the
        // car always wins and heavier families resist more. The size the mesh happens to be only
        // decides the collider; it never decides whether the thing moves.
        private static readonly Family[] Families =
        {
            new Family { Label = "trash container", Name = new Regex(@"^trashcontainer", RegexOptions.IgnoreCase), MassKg = 60f  },
            new Family { Label = "picnic table",    Name = new Regex(@"^picnictable",    RegexOptions.IgnoreCase), MassKg = 120f },
            new Family { Label = "mobile toilet",   Name = new Regex(@"^mobile_toilet",  RegexOptions.IgnoreCase), MassKg = 180f },
            new Family { Label = "traffic cone",    Name = new Regex(@"^cone_type",      RegexOptions.IgnoreCase), MassKg = 5f   },
            new Family { Label = "road blocker",    Name = new Regex(@"^RoadBlocker",    RegexOptions.IgnoreCase), MassKg = 25f  },
        };

        /// <summary>Props below this world height are not on the map and are skipped. The road
        /// surface is at y = 0; the unplaced groups sit at y = -83.6.</summary>
        private const float MinimumWorldY = -5f;

        [MenuItem("Tools/Zoom Zoom/Make Props Knockable", priority = 60)]
        public static void Bake()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var counts = new Dictionary<string, int>();
            int underground = 0, already = 0, noMesh = 0;

            Undo.SetCurrentGroupName("Make Props Knockable");
            int group = Undo.GetCurrentGroup();

            // Collect first, then mutate. Reparenting while iterating GetComponentsInChildren of
            // the roots would move objects out from under the enumeration.
            var targets = new List<(Transform t, Family f)>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    Family? f = Match(t.name);
                    if (f != null) targets.Add((t, f.Value));
                }

            var sample = new Dictionary<string, string>();
            var suspect = new List<string>();
            var oversized = new List<string>();

            foreach ((Transform t, Family f) in targets)
            {
                if (t.GetComponent<KnockableProp>() != null) { already++; continue; }
                if (t.position.y < MinimumWorldY) { underground++; continue; }
                if (!MakeKnockable(t, f)) { noMesh++; continue; }

                counts.TryGetValue(f.Label, out int n);
                counts[f.Label] = n + 1;

                // Self-check. Earlier versions reported success on bakes that could not work, so
                // this one measures its own output and says what it made. Under 10 cm is a box that
                // cannot have been fitted to anything real and is treated as a failure. Over 6 m is
                // reported but NOT treated as a failure: it turned out to be true. The picnic tables
                // in this import measure 9 m square and sit 12 m apart in the park, which is how
                // someone spaces 9 m objects, not 2 m ones. The whole city is oversized relative to
                // the car (its author said so: "buildings are too big, need resizing"). A collider
                // that matches an oversized mesh is correct; the mesh is the thing to fix.
                var box = t.GetComponent<BoxCollider>();
                Vector3 ws = Vector3.Scale(box.size, t.lossyScale);
                float longest = Mathf.Max(ws.x, ws.y, ws.z);
                if (longest < 0.1f)
                    suspect.Add($"{t.name} box {ws.x:0.00}x{ws.y:0.00}x{ws.z:0.00} m");
                else if (longest > 6f)
                    oversized.Add($"{t.name} {ws.x:0.0}x{ws.y:0.0}x{ws.z:0.0} m");
                if (!sample.ContainsKey(f.Label))
                {
                    var mk = t.GetComponent<KnockableProp>();
                    sample[f.Label] = $"{t.name}: box {ws.x:0.0}x{ws.y:0.0}x{ws.z:0.0} m, {mk.bakedMassKg:0} kg" +
                                      (mk.strayVertexFraction > 0.05f ? $", ignored {mk.strayVertexFraction:P0} stray" : "");
                }
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            var sb = new System.Text.StringBuilder("[KnockablePropBaker] ");
            int total = 0;
            foreach (var kv in counts) { sb.Append($"{kv.Value} {kv.Key}, "); total += kv.Value; }
            sb.Append($"{total} props made knockable in '{scene.name}'.");
            if (underground > 0) sb.Append($" {underground} skipped: below the map (y < {MinimumWorldY}).");
            if (already > 0) sb.Append($" {already} already knockable.");
            if (noMesh > 0) sb.Append($" {noMesh} skipped: no mesh or renderer.");
            sb.Append(saved
                ? " Scene saved; it will survive Play."
                : " SCENE NOT SAVED: Unity refused the save. Press Ctrl+S before Play or this is lost.");
            sb.Append("\nOne of each, world size, so the result can be judged without pressing Play:");
            foreach (var kv in sample) sb.Append($"\n   {kv.Key,-16} {kv.Value}");

            if (oversized.Count > 0)
            {
                sb.Append($"\n{oversized.Count} props are over 6 m on a side. They are knockable all the same: mass is a " +
                          "fixed feel number per family, so the car moves them regardless of size. Listed because a " +
                          "9 m picnic table that tumbles like a light object will look like one, and that is a " +
                          "known trade: the city is modelled larger than the car and is not being resized for this.");
                foreach (string s in oversized) sb.Append("\n   " + s);
            }

            if (suspect.Count > 0)
            {
                sb.Append($"\n{suspect.Count} BOX SIZES ARE BROKEN (under 10 cm). These will not behave; " +
                          "run Undo Knockable Props and report the sizes:");
                foreach (string s in suspect) sb.Append("\n   " + s);
                Debug.LogError(sb.ToString());
            }
            else if (!saved) Debug.LogWarning(sb.ToString());
            else if (oversized.Count > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/Zoom Zoom/Undo Knockable Props", priority = 61)]
        public static void Strip()
        {
            var scene = EditorSceneManager.GetActiveScene();
            int n = 0;

            Undo.SetCurrentGroupName("Undo Knockable Props");
            int group = Undo.GetCurrentGroup();

            var markers = new List<KnockableProp>();
            foreach (GameObject root in scene.GetRootGameObjects())
                markers.AddRange(root.GetComponentsInChildren<KnockableProp>(true));

            foreach (KnockableProp marker in markers)
            {
                GameObject go = marker.gameObject;
                foreach (var rb in go.GetComponents<Rigidbody>()) Undo.DestroyObjectImmediate(rb);
                foreach (var box in go.GetComponents<BoxCollider>()) Undo.DestroyObjectImmediate(box);
                foreach (var mesh in go.GetComponents<MeshCollider>())
                {
                    Undo.RecordObject(mesh, "re-enable mesh collider");
                    mesh.enabled = true;
                }

                // Back under the parent it came from, keeping world placement. Scale returns to
                // whatever the parent chain implies, which is the import's original.
                if (marker.originalParent != null)
                {
                    Undo.SetTransformParent(go.transform, marker.originalParent, "reparent prop");
                    Undo.RecordObject(go.transform, "restore scale");
                    go.transform.localScale = marker.originalLocalScale;
                }

                Undo.DestroyObjectImmediate(marker);
                n++;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[KnockablePropBaker] Stripped {n} props back to static in '{scene.name}'" +
                      (saved ? ", scene saved." : ". SCENE NOT SAVED, press Ctrl+S."));
        }

        private static Family? Match(string name)
        {
            foreach (Family f in Families)
                if (f.Name.IsMatch(name)) return f;
            return null;
        }

        private static bool MakeKnockable(Transform t, Family family)
        {
            GameObject go = t.gameObject;
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;

            // Remember where it came from before anything moves.
            Transform originalParent = t.parent;
            Vector3 originalLocalScale = t.localScale;

            // Measure the prop BEFORE touching the transform, and do not trust either
            // mesh.bounds or renderer.bounds to do it. This FBX is a Blender scene export: every
            // prop is its own unique mesh, Unity discarded self-intersecting polygons from it on
            // import, and the bounds it reports are several times the visible object (a picnic
            // table came back 9 m square). That is the known symptom of stray vertices far from the
            // drawn geometry, which the renderer skips but the bounds include. So the size is taken
            // from the vertex cloud itself with the outliers thrown away. The amount discarded is
            // recorded on the marker, so the correction is visible rather than silent.
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return false;
            Bounds world = RobustWorldBounds(filter.sharedMesh, t, out float rejectedFraction);

            // 1. Out of the broken hierarchy. World position and rotation are kept; scale is reset
            //    to exactly 1 so the Rigidbody lives in a clean, positive, uniform transform.
            Vector3 worldPos = t.position;
            Quaternion worldRot = t.rotation;
            Vector3 lossy = t.lossyScale;

            Undo.SetTransformParent(t, null, "unparent prop");
            Undo.RecordObject(t, "normalise prop transform");
            t.SetPositionAndRotation(worldPos, worldRot);
            // Keep the magnitude per axis so the MESH still renders at its real size, but make
            // every axis positive. Non-uniform scale on the body's OWN transform is fine; it is
            // non-uniform scale on a PARENT that PhysX cannot take.
            t.localScale = new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));

            // 2. Old collider off, not destroyed: the bake stays reversible.
            foreach (var mesh in go.GetComponents<MeshCollider>())
            {
                Undo.RecordObject(mesh, "disable mesh collider");
                mesh.enabled = false;
            }

            // 3. Box fitted to the measured world bounds, expressed in the prop's new local space.
            //    BoxCollider.size is multiplied by the transform's scale, so divide it back out.
            //    Renderer bounds are axis-aligned in world space, so for a prop rotated off the
            //    grid this box is a little loose rather than tight. Fine for furniture on a street
            //    grid, where nearly everything is at 0 or 90 degrees; not fine for a tilted sign,
            //    which is one more reason lamps and signs are not in the list yet.
            var box = Undo.AddComponent<BoxCollider>(go);
            Vector3 s = t.localScale;
            box.center = t.InverseTransformPoint(world.center);
            box.size = new Vector3(
                world.size.x / Mathf.Max(s.x, 1e-4f),
                world.size.y / Mathf.Max(s.y, 1e-4f),
                world.size.z / Mathf.Max(s.z, 1e-4f));

            // Props from this import sit with their pivot at ground level and their bounds
            // overlapping the road collider by a few centimetres. A body that starts inside
            // another collider gets shoved out on frame one and may launch. Lift it clear.
            float sink = (world.min.y) - 0.0f;
            if (sink < 0.02f)
            {
                Undo.RecordObject(t, "lift prop clear of road");
                t.position += Vector3.up * (0.02f - sink);
            }

            // 4. The body. Starts asleep so 50 bodies at rest cost nothing on frame one.
            //    Mass is the family's feel number, not a function of size; see Families.
            float mass = family.MassKg;

            var rb = Undo.AddComponent<Rigidbody>(go);
            rb.mass = mass;
            rb.linearDamping = 0.2f;
            // Stronger than a normal prop because these are large: a 9 m table hit on one corner
            // is a 9 m lever and will pivot for a long time without this. Settles the swing.
            rb.angularDamping = 1.5f;
            // Inertia as if the object were compact. PhysX derives inertia from the collider's
            // extent, and a 9 m box at 120 kg has enormous rotational inertia: it swings slowly
            // and heavily and can roll back through the car. Capping the tensor to what a 2 m box
            // of the same mass would have makes a big prop turn like a small one, which is what
            // "knockable" means in an arcade game.
            rb.inertiaTensor = CompactInertia(mass, 2f);
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.sleepThreshold = 0.05f;
            rb.Sleep();

            var marker = Undo.AddComponent<KnockableProp>(go);
            marker.family = family.Label;
            marker.bakedMassKg = mass;
            marker.originalParent = originalParent;
            marker.originalLocalScale = originalLocalScale;
            marker.strayVertexFraction = rejectedFraction;

            return true;
        }

        /// <summary>Inertia tensor of a solid cube of the given mass and side, so an oversized prop
        /// rotates like a compact one. Solid cube: I = m * s^2 / 6 about each axis.</summary>
        private static Vector3 CompactInertia(float mass, float sideMetres)
        {
            float i = mass * sideMetres * sideMetres / 6f;
            return new Vector3(i, i, i);
        }

        /// <summary>
        /// World-space bounds of the vertices that actually form the object, ignoring strays.
        ///
        /// Per axis, the vertex coordinates are sorted and the box is taken from the 2nd to the
        /// 98th percentile, then widened by 4% so a legitimately thin extreme (a table's
        /// outermost leg) is not clipped. A handful of vertices parked far from the mesh, which is
        /// what inflates Unity's own bounds, land outside those percentiles and are ignored. On a
        /// clean mesh the result is within a few percent of the true bounds; on this import it is
        /// the difference between a 9 m box and a 1.8 m one.
        ///
        /// Transformed with the object's CURRENT world matrix, before the baker normalises it, so
        /// it measures what is on screen right now.
        /// </summary>
        private static Bounds RobustWorldBounds(Mesh mesh, Transform t, out float rejectedFraction)
        {
            Vector3[] verts = mesh.vertices;
            rejectedFraction = 0f;
            if (verts.Length == 0) return new Bounds(t.position, Vector3.one);

            Matrix4x4 m = t.localToWorldMatrix;
            var xs = new float[verts.Length];
            var ys = new float[verts.Length];
            var zs = new float[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 w = m.MultiplyPoint3x4(verts[i]);
                xs[i] = w.x; ys[i] = w.y; zs[i] = w.z;
            }
            System.Array.Sort(xs); System.Array.Sort(ys); System.Array.Sort(zs);

            int lo = Mathf.FloorToInt(verts.Length * 0.02f);
            int hi = Mathf.Max(lo, Mathf.CeilToInt(verts.Length * 0.98f) - 1);
            var min = new Vector3(xs[lo], ys[lo], zs[lo]);
            var max = new Vector3(xs[hi], ys[hi], zs[hi]);
            Vector3 pad = (max - min) * 0.04f;
            min -= pad; max += pad;

            // How much of the raw extent was thrown away, as a fraction, so the log can show it.
            Vector3 rawSize = new Vector3(xs[xs.Length - 1] - xs[0], ys[ys.Length - 1] - ys[0], zs[zs.Length - 1] - zs[0]);
            Vector3 keptSize = max - min;
            float rawMax = Mathf.Max(rawSize.x, rawSize.y, rawSize.z);
            if (rawMax > 1e-4f) rejectedFraction = 1f - Mathf.Max(keptSize.x, keptSize.y, keptSize.z) / rawMax;

            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }
    }
}
