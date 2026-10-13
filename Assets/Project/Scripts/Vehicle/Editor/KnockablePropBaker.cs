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
    /// scale PhysX objects to. The box is fitted to the mesh's own vertices in the prop's local
    /// space, so it matches the model however the import rotated it; see FitBoxToMesh. The stale
    /// MeshCollider is disabled and kept so the bake stays reversible, and the original parent is
    /// recorded on the marker so Undo Knockable Props can put the object back.
    ///
    /// EVERY PROP IS ROTATED, AND THAT USED TO BE IGNORED
    /// These are Blender exports, so each one carries the Z-up to Y-up import rotation: the axis
    /// pointing at the sky is the prop's LOCAL Z. Two places read height off local Y instead, with
    /// the same sign of error: the box fitter wrote a measured world height into local Y, and the
    /// seater read the height back out of local Y. Measured props therefore landed at the right
    /// height with a collider on the wrong axes, and the three hand-boxed RoadBlocker04s, whose box
    /// was not measured, were buried half a metre into the road. Both now go through
    /// WorldVerticalHalfExtent, which projects the box's axes onto world up.
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
            /// <summary>Optional. Name of one scene object whose BoxCollider was fitted by hand;
            /// every member of this family copies that box in world metres instead of using the
            /// measured one. For props whose geometry fools the measurement, a human eye wins.</summary>
            public string ReferenceObject;
            /// <summary>Resolved at bake time from ReferenceObject. Not set in the table.</summary>
            public BoxCollider CopyBoxFrom;
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
            // Blockers copy the box Ako fitted by hand on RoadBlocker04.002 on 13 October, which
            // looks right on the model where the measured one was a slab the size of the mesh's
            // stray geometry. One object tuned by eye, applied to all of them.
            new Family { Label = "road blocker",    Name = new Regex(@"^RoadBlocker",    RegexOptions.IgnoreCase), MassKg = 25f,  ReferenceObject = "RoadBlocker04.002" },
        };

        /// <summary>Props below this world height are not on the map and are skipped. The road
        /// surface is at y = 0; the unplaced groups sit at y = -83.6.</summary>
        private const float MinimumWorldY = -5f;

        /// <summary>Gap left between a prop's box bottom and the ground under it, for families
        /// with no hand-fitted reference. A hand-fitted family uses the reference's own gap
        /// instead; see ReferenceClearance.</summary>
        private const float DefaultClearance = 0.03f;

        /// <summary>Reference objects already reported for a rotation mismatch this bake, so the
        /// warning fires once per reference instead of once per prop. Cleared by Bake().</summary>
        private static readonly HashSet<string> rotationWarned = new HashSet<string>();

        [MenuItem("Tools/Zoom Zoom/Make Props Knockable", priority = 60)]
        public static void Bake()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var counts = new Dictionary<string, int>();
            int underground = 0, already = 0, noMesh = 0, refitted = 0;
            rotationWarned.Clear();

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
            var unseated = new List<string>();

            // Resolve each family's hand-fitted reference once, before anything is re-fitted.
            // The reference object itself is excluded from the bake so the copy source is never
            // overwritten by its own copy.
            //
            // A referenced family also inherits the REFERENCE'S OWN ground gap instead of the
            // generic 3 cm. RoadBlocker04.002 was fitted and placed by eye and called perfect; its
            // box bottom sits at y = 0.3140 against a road top of 0.40, i.e. 8.6 cm of initial
            // overlap, which PhysX pushes out in a frame or two. Reproducing that gap puts the
            // other blockers at exactly the y the hand-placed one has, which is the only
            // definition of "right" available for a box nobody measured.
            var references = new Dictionary<string, BoxCollider>();
            var clearances = new Dictionary<string, float>();
            foreach (Family f in Families)
                if (!string.IsNullOrEmpty(f.ReferenceObject) && !references.ContainsKey(f.Label))
                {
                    BoxCollider r = FindReferenceBox(f.ReferenceObject);
                    references[f.Label] = r;
                    clearances[f.Label] = ReferenceClearance(f, r);
                }

            // Shared self-check for both paths below: a freshly baked prop and a re-fitted one are
            // judged the same way, so the log says what is in the scene now either way.
            //
            // Earlier versions reported success on bakes that could not work, so this one measures
            // its own output. Under 10 cm is a box that cannot have been fitted to anything real
            // and is treated as a failure. Over 6 m is reported but NOT treated as a failure: it
            // turned out to be true. The picnic tables in this import measure 9 m square and sit
            // 12 m apart in the park, which is how someone spaces 9 m objects, not 2 m ones. The
            // whole city is oversized relative to the car (its author said so: "buildings are too
            // big, need resizing"). A collider that matches an oversized mesh is correct; the mesh
            // is the thing to fix.
            void Record(Transform t, Family f, BoxCollider box)
            {
                Vector3 ws = WorldExtents(t, box);
                float longest = Mathf.Max(ws.x, ws.y, ws.z);
                if (longest < 0.1f)
                    suspect.Add($"{t.name} box {ws.x:0.00}x{ws.y:0.00}x{ws.z:0.00} m");
                else if (longest > 6f)
                    oversized.Add($"{t.name} {ws.x:0.0}x{ws.y:0.0}x{ws.z:0.0} m");
                if (sample.ContainsKey(f.Label)) return;
                var mk = t.GetComponent<KnockableProp>();
                sample[f.Label] = $"{t.name}: box {ws.x:0.0}x{ws.y:0.0}x{ws.z:0.0} m, {mk.bakedMassKg:0} kg, " +
                                  $"box bottom at y={WorldBoxBottom(t, box):0.00}" +
                                  (f.CopyBoxFrom != null ? $", box copied from {f.CopyBoxFrom.name}" : "") +
                                  (mk.strayVertexFraction > 0.05f ? $", ignored {mk.strayVertexFraction:P0} stray" : "");
            }

            for (int i = 0; i < targets.Count; i++)
            {
                (Transform t, Family f) = targets[i];
                references.TryGetValue(f.Label, out f.CopyBoxFrom);
                if (f.CopyBoxFrom != null && f.CopyBoxFrom.transform == t) { already++; continue; }
                float clearance = clearances.TryGetValue(f.Label, out float c) ? c : DefaultClearance;

                // Already knockable: re-fit in place rather than skip. The seating fix and the
                // reference boxes have to reach props from earlier bakes, and asking the user to
                // undo and redo is the kind of step that gets forgotten.
                var existing = t.GetComponent<KnockableProp>();
                if (existing != null)
                {
                    var oldBox = t.GetComponent<BoxCollider>();
                    if (oldBox != null)
                    {
                        // Re-fit the box, not just the seating. Boxes from earlier bakes have the
                        // measured WORLD height written into their local Y, which is the wrong axis
                        // for every prop in this import; see FitBoxToMesh. Seating a wrong box
                        // would move a prop that is currently in the right place.
                        var filter = t.GetComponent<MeshFilter>();
                        if (f.CopyBoxFrom != null) ApplyReferenceBox(t.gameObject, oldBox, f.CopyBoxFrom);
                        else if (filter != null && filter.sharedMesh != null)
                        {
                            Undo.RecordObject(oldBox, "refit box");
                            FitBoxToMesh(oldBox, filter.sharedMesh, out float stray);
                            Undo.RecordObject(existing, "refit stray fraction");
                            existing.strayVertexFraction = stray;
                        }
                        string why = SeatOnRoad(t, oldBox, clearance);
                        if (why != null) unseated.Add(why);
                        var rb = t.GetComponent<Rigidbody>();
                        if (rb != null) { Undo.RecordObject(rb, "refit mass"); rb.mass = f.MassKg; rb.inertiaTensor = CompactInertia(f.MassKg, 2f); }
                        Undo.RecordObject(existing, "refit marker"); existing.bakedMassKg = f.MassKg;
                        counts.TryGetValue(f.Label, out int re); counts[f.Label] = re + 1;
                        refitted++;
                        Record(t, f, oldBox);
                    }
                    else already++;
                    continue;
                }

                if (t.position.y < MinimumWorldY) { underground++; continue; }
                if (!MakeKnockable(t, f, clearance, unseated)) { noMesh++; continue; }

                counts.TryGetValue(f.Label, out int n);
                counts[f.Label] = n + 1;
                Record(t, f, t.GetComponent<BoxCollider>());
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            var sb = new System.Text.StringBuilder("[KnockablePropBaker] ");
            int total = 0;
            foreach (var kv in counts) { sb.Append($"{kv.Value} {kv.Key}, "); total += kv.Value; }
            sb.Append($"{total} props made knockable in '{scene.name}'.");
            if (refitted > 0) sb.Append($" {refitted} of those were already knockable and were re-seated on the road in place.");
            if (underground > 0) sb.Append($" {underground} skipped: below the map (y < {MinimumWorldY}).");
            if (already > 0) sb.Append($" {already} left alone (hand-fitted reference, or no box to refit).");
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

            // Anything the seater refused is named, with the reason, rather than left to be found
            // by eye in a 950-object scene. A prop listed here was NOT moved: it is still wherever
            // it was before the bake, which is the safe outcome. If the road's hit normals ever
            // point the wrong way, or a prop stands under an awning, every case shows up here.
            if (unseated.Count > 0)
            {
                var us = new System.Text.StringBuilder(
                    $"[KnockablePropBaker] {unseated.Count} props were NOT re-seated and were left exactly where " +
                    "they were. Each line says why:");
                foreach (string s in unseated) us.Append("\n   " + s);
                Debug.LogWarning(us.ToString());
            }
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

        private static bool MakeKnockable(Transform t, Family family, float clearance, List<string> unseated)
        {
            GameObject go = t.gameObject;
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;

            // Remember where it came from before anything moves.
            Transform originalParent = t.parent;
            Vector3 originalLocalScale = t.localScale;

            // Nothing drawn, nothing to knock over: skip it rather than give a body to an
            // invisible object.
            if (go.GetComponent<Renderer>() == null) return false;

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

            // 3. Box fitted to the mesh in the prop's own local space. See FitBoxToMesh.
            var box = Undo.AddComponent<BoxCollider>(go);
            FitBoxToMesh(box, filter.sharedMesh, out float rejectedFraction);

            // 3b. Blockers take the hand-fitted box instead of the measured one. See CopyBoxFrom.
            if (family.CopyBoxFrom != null) ApplyReferenceBox(go, box, family.CopyBoxFrom);

            // 3c. Sit the box ON the road. This was the actual reason nothing moved. The props'
            //     bounds reach well below the visible object (tables to -0.59, toilets to -1.61)
            //     while the road collider under them sits at +0.40, so every body began a metre or
            //     more INSIDE a static collider. PhysX treats that as a penetration to resolve, not
            //     a contact to push, and it pins the body in place; the car's shove never reaches
            //     it. The first version lifted by 2 cm against y = 0, which was neither the road's
            //     height nor the box's bottom. Now the road is found the way the car finds it, with
            //     a raycast from above, and the box bottom is placed a hair above the hit.
            string why = SeatOnRoad(t, box, clearance);
            if (why != null) unseated.Add(why);

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

        /// <summary>
        /// Half the box's extent along WORLD up, for a box that is rotated, which every prop in
        /// this import is.
        ///
        /// WHY NOT box.size.y * scale.y
        /// That is the box's height along its OWN y axis, and the two are only the same thing when
        /// the prop is unrotated. Every prop here is a Blender export carrying the Z-up to Y-up
        /// import rotation, so the axis that points at the sky is local Z, not local Y. Reading the
        /// height off local Y is what buried RoadBlocker04.001 and .003 at y = -0.0899 against the
        /// 0.400 of the hand-placed .002: it used 0.483 m of half-extent where 1.087 m was needed.
        ///
        /// THE ARITHMETIC, WORKED THROUGH ON THAT BLOCKER
        /// Rotation {x:-0.5000001, y:0.5, z:0.49999994, w:0.5}, uniform scale 230.0039. The y row
        /// of localToWorldMatrix comes out (m10, m11, m12) = (0, 0, 230.0039), i.e. local Z is
        /// world up and the other two local axes are horizontal, contributing nothing to height:
        ///     local X -> world -Z,  scaled extent 3.969 m   (the 4 m long barrier panel)
        ///     local Y -> world -X,  scaled extent 0.967 m   (its thickness)
        ///     local Z -> world +Y,  scaled extent 2.175 m   (its height)
        /// box.size = (0.017257674, 0.004203821, 0.009454458), so e = size * 0.5 and
        ///     halfHeight = |0*e.x| + |0*e.y| + |230.0039*e.z| = 1.087281 m.
        /// The old code returned 230.0039 * 0.004203821 / 2 = 0.483448 m, which is 0.603833 m
        /// short. That shortfall, less the 0.116001 m by which the generic 3 cm clearance differs
        /// from the reference's own -0.086001 m gap, is the 0.4899 m those two blockers ended up
        /// below the hand-placed .002. Note that 1.985 m, which looks like a plausible answer, is
        /// half the LOCAL X extent; this rotation sends local X to world -Z, so it is horizontal.
        ///
        /// THE TRAP: e must be the RAW box.size * 0.5. The matrix columns already carry the
        /// transform's scale, so pre-scaling e by lossyScale applies 230x twice and returns
        /// 250.08 m for this blocker. Equally, do not reach for box.bounds here: that reads
        /// PhysX's cached pose, which is stale the moment the baker writes transform.position and
        /// would need a Physics.SyncTransforms() to be trusted in edit mode. This form is pure
        /// transform maths and needs no sync.
        /// </summary>
        private static float WorldVerticalHalfExtent(Transform t, BoxCollider box)
        {
            Matrix4x4 m = t.localToWorldMatrix;
            Vector3 e = box.size * 0.5f;
            return Mathf.Abs(m.m10 * e.x) + Mathf.Abs(m.m11 * e.y) + Mathf.Abs(m.m12 * e.z);
        }

        /// <summary>World height of the bottom face of a rotated box. The centre goes through
        /// TransformPoint, which already handles rotation; only the half-extent needed fixing.</summary>
        private static float WorldBoxBottom(Transform t, BoxCollider box)
            => t.TransformPoint(box.center).y - WorldVerticalHalfExtent(t, box);

        /// <summary>Size of the rotated box's world-aligned bounding box, for the log. Same
        /// projection as WorldVerticalHalfExtent, once per world axis, doubled. For the blocker
        /// above this reads 0.97 x 2.17 x 3.97 m, which is what a player sees; the old
        /// Vector3.Scale(box.size, lossyScale) reported 3.97 x 0.97 x 2.17, the same three numbers
        /// on the wrong axes.</summary>
        private static Vector3 WorldExtents(Transform t, BoxCollider box)
        {
            Matrix4x4 m = t.localToWorldMatrix;
            Vector3 e = box.size * 0.5f;
            return new Vector3(
                (Mathf.Abs(m.m00 * e.x) + Mathf.Abs(m.m01 * e.y) + Mathf.Abs(m.m02 * e.z)) * 2f,
                (Mathf.Abs(m.m10 * e.x) + Mathf.Abs(m.m11 * e.y) + Mathf.Abs(m.m12 * e.z)) * 2f,
                (Mathf.Abs(m.m20 * e.x) + Mathf.Abs(m.m21 * e.y) + Mathf.Abs(m.m22 * e.z)) * 2f);
        }

        /// <summary>
        /// Highest static surface under the prop, or false if there is none.
        ///
        /// The ray starts just above the prop's OWN box top, not 50 m overhead. That is the fix for
        /// trashcontainer01.006, which the old version parked at y = 6.76 while its eight siblings
        /// sat at 0.525: the ray came down from 50 m up, hit the awning the bin stands under, and
        /// the baker dutifully seated the bin on the awning. A ray that starts below the awning
        /// cannot hit it. Hits are also required to face up within 45 degrees, so a wall or a kerb
        /// face the ray clips on the way down is not mistaken for ground.
        ///
        /// No Physics.SyncTransforms() is needed around this, even though the baker moves props
        /// between calls: everything the ray is allowed to hit is static geometry that never moves
        /// during a bake, and every collider that does move is filtered out below for having a
        /// Rigidbody or a KnockableProp.
        /// </summary>
        private static bool TryFindGroundBelow(Transform t, BoxCollider box, out float groundY)
        {
            groundY = 0f;
            Vector3 worldCentre = t.TransformPoint(box.center);
            float half = WorldVerticalHalfExtent(t, box);
            var from = new Vector3(worldCentre.x, worldCentre.y + half + 0.25f, worldCentre.z);

            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, half * 2f + 100f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            bool found = false;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(t)) continue;                         // itself
                if (h.collider.GetComponentInParent<KnockableProp>() != null) continue;  // another prop
                if (h.collider.attachedRigidbody != null) continue;                      // anything dynamic
                if (Vector3.Dot(h.normal, Vector3.up) < 0.707f) continue;                // over 45 deg off up: not a floor
                if (h.distance < best) { best = h.distance; groundY = h.point.y; found = true; }
            }
            return found;
        }

        /// <summary>
        /// Lowers or raises the prop so the bottom face of its box sits <paramref name="clearance"/>
        /// above the static ground under it. Returns null when the prop is seated or already within
        /// a millimetre of it, or a one-line reason naming the prop when the move was refused.
        ///
        /// A refused move leaves the prop exactly where it was: a prop in the wrong place is a
        /// visible, reportable problem, where a prop teleported onto a roof looks like the tool
        /// worked. Upward moves are capped at max(1 m, the box's own height); downward moves are
        /// not capped, since that is the path that repairs an already-floating prop. The cap cannot
        /// be a flat 1 m: a first bake legitimately lifts a table 1.02 m and a toilet 2.04 m,
        /// because their import bounds reach to -0.59 and -1.61 against a road at +0.40. It still
        /// rejects the 6.24 m lift that put trashcontainer01.006 on an awning.
        /// </summary>
        private static string SeatOnRoad(Transform t, BoxCollider box, float clearance)
        {
            float boxBottom = WorldBoxBottom(t, box);
            if (!TryFindGroundBelow(t, box, out float groundY))
                return $"{t.name}: no static ground under it; left at y={t.position.y:0.000}";

            float lift = (groundY + clearance) - boxBottom;
            if (Mathf.Abs(lift) < 0.001f) return null;

            float maxLift = Mathf.Max(1f, WorldVerticalHalfExtent(t, box) * 2f);
            if (lift > maxLift)
                return $"{t.name}: ground found at y={groundY:0.00} would lift it {lift:0.00} m, " +
                       $"more than its own {maxLift:0.00} m of height; left at y={t.position.y:0.000}";

            Undo.RecordObject(t, "seat prop on road");
            t.position += Vector3.up * lift;
            return null;
        }

        /// <summary>
        /// The ground gap to use for a family with a hand-fitted reference: the reference's own.
        /// Its box bottom minus the ground under it, so every member of the family ends up sitting
        /// the way the hand-placed one does. Falls back to DefaultClearance, with a warning, if the
        /// reference is missing or has nothing under it.
        /// </summary>
        private static float ReferenceClearance(Family family, BoxCollider reference)
        {
            if (reference == null) return DefaultClearance;
            Transform rt = reference.transform;
            if (!TryFindGroundBelow(rt, reference, out float groundY))
            {
                Debug.LogWarning($"[KnockablePropBaker] No ground found under reference '{rt.name}'; " +
                                 $"the {family.Label} family falls back to a {DefaultClearance:0.##} m gap.");
                return DefaultClearance;
            }
            return WorldBoxBottom(rt, reference) - groundY;
        }

        /// <summary>
        /// Gives this prop the same box, in world metres, as a hand-fitted reference object. The
        /// reference's box is converted to world size and world offset from its pivot, then
        /// re-expressed in this prop's local scale, so a 230x object and a 0.55x object end up with
        /// an identical box on screen. Centre offset is scaled by the ratio of the two objects'
        /// scales along each axis so the box sits at the same place on the model.
        ///
        /// This is axis-for-axis in LOCAL space, so it is only exact when reference and target
        /// share rotation as well as scale shape. They do here: every RoadBlocker04.* carries
        /// {-0.5000001, 0.5, 0.49999994, 0.5} at 230.0039 uniform. A target rotated differently
        /// gets an approximate box, and is warned about once.
        /// </summary>
        private static void ApplyReferenceBox(GameObject go, BoxCollider box, BoxCollider reference)
        {
            Transform rt = reference.transform;
            Transform t = go.transform;

            float off = Quaternion.Angle(rt.rotation, t.rotation);
            if (off > 1f && rotationWarned.Add(reference.name))
                Debug.LogWarning($"[KnockablePropBaker] '{t.name}' sits {off:0.#} degrees away from reference " +
                                 $"'{reference.name}'. The copied box is only exact when the two share rotation, " +
                                 "so boxes in this family are approximate. Fit one by hand at the new angle.");

            Vector3 worldSize = Vector3.Scale(reference.size, Abs(rt.lossyScale));
            Vector3 worldOffset = Vector3.Scale(reference.center, rt.lossyScale);
            Vector3 s = Abs(t.lossyScale);
            box.size = new Vector3(worldSize.x / Mathf.Max(s.x, 1e-4f), worldSize.y / Mathf.Max(s.y, 1e-4f), worldSize.z / Mathf.Max(s.z, 1e-4f));
            box.center = new Vector3(worldOffset.x / Mathf.Max(s.x, 1e-4f), worldOffset.y / Mathf.Max(s.y, 1e-4f), worldOffset.z / Mathf.Max(s.z, 1e-4f));
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        /// <summary>Finds the scene object whose box every member of a family should copy. Named
        /// in the family table; returns null, with a warning, if it is not in the scene.</summary>
        private static BoxCollider FindReferenceBox(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return null;
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform x in root.GetComponentsInChildren<Transform>(true))
                    if (x.name == objectName)
                    {
                        var b = x.GetComponent<BoxCollider>();
                        if (b != null) return b;
                    }
            Debug.LogWarning($"[KnockablePropBaker] Reference object '{objectName}' not found or has no BoxCollider; " +
                             "its family will use measured boxes instead.");
            return null;
        }

        /// <summary>Inertia tensor of a solid cube of the given mass and side, so an oversized prop
        /// rotates like a compact one. Solid cube: I = m * s^2 / 6 about each axis.</summary>
        private static Vector3 CompactInertia(float mass, float sideMetres)
        {
            float i = mass * sideMetres * sideMetres / 6f;
            return new Vector3(i, i, i);
        }

        /// <summary>
        /// Fits the box to the mesh, in the prop's own local space, ignoring stray vertices.
        ///
        /// WHY LOCAL AND NOT WORLD
        /// BoxCollider.center and size ARE local, and the earlier version measured a world
        /// axis-aligned box and then divided its size by the transform's scale component by
        /// component. That silently assumed local axes line up with world axes. They never do in
        /// this import: the Z-up to Y-up import rotation means local Z points at the sky, so the
        /// measured world HEIGHT landed on local Y, i.e. on a horizontal axis. picnictable01.001
        /// ended up with a 9.21 x 9.21 x 1.98 m collider: a 9 m slab standing on edge where the
        /// table is 9.21 x 1.98 x 9.21. Measuring the vertices in local space needs no division at
        /// all and is exact under any rotation. The world dimensions it produces are the same
        /// numbers as before (percentile selection per axis is invariant to an axis permutation and
        /// to positive scaling); only the axis each one lands on changes, which is the whole fix.
        ///
        /// The box CENTRE does not change, which is why re-baking does not move the props that are
        /// already seated correctly. The old code took InverseTransformPoint of a world centre it
        /// had measured through the pre-normalise matrix, and that round trip is the identity
        /// exactly when the original world scale was positive on every axis. It was, for all 33
        /// props in the scene: the negative prop scale and the negative group scale cancel, e.g. a
        /// park table at -0.29310867 under park_props at -426.46298 under sehirlogo_01 at
        /// 2.0439076 comes to +255.48845 on every axis. Only the size was ever wrong.
        ///
        /// WHY PERCENTILES AND NOT mesh.bounds
        /// This FBX is a Blender scene export: every prop is its own unique mesh, Unity discarded
        /// self-intersecting polygons on import, and the bounds it reports are several times the
        /// visible object (a picnic table came back 9 m square). That is the signature of stray
        /// vertices parked far from the drawn geometry, which the renderer skips but the bounds
        /// include. So per axis the coordinates are sorted and the box is taken from the 2nd to the
        /// 98th percentile, then widened by 4% so a legitimately thin extreme (a table's outermost
        /// leg) is not clipped. The fraction discarded is returned and recorded on the marker, so
        /// the correction is visible rather than silent.
        /// </summary>
        private static void FitBoxToMesh(BoxCollider box, Mesh mesh, out float rejectedFraction)
        {
            Bounds local = RobustLocalBounds(mesh, out rejectedFraction);
            box.center = local.center;
            box.size = local.size;
        }

        /// <summary>Local-space bounds of the vertices that actually form the object, ignoring
        /// strays. See FitBoxToMesh for why this is local and why it is percentile-based.</summary>
        private static Bounds RobustLocalBounds(Mesh mesh, out float rejectedFraction)
        {
            Vector3[] verts = mesh.vertices;
            rejectedFraction = 0f;
            if (verts.Length == 0) return new Bounds(Vector3.zero, Vector3.one);

            var xs = new float[verts.Length];
            var ys = new float[verts.Length];
            var zs = new float[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                xs[i] = v.x; ys[i] = v.y; zs[i] = v.z;
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
