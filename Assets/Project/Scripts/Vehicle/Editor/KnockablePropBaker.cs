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
    /// The city came in as flat geometry, not prefabs: the scene has about 950 props as plain
    /// GameObjects named things like trashcontainer01.004. Hand-editing even the 46 in this first
    /// pass is an afternoon of clicking; doing it again after the next re-export is another. The
    /// tool makes the decision once, by name, and the scene can be re-baked in a second.
    ///
    /// WHY A BOX COLLIDER AND NOT THE MESH COLLIDER THAT IS ALREADY THERE
    /// Two reasons found by reading the scene file rather than assuming.
    ///   1. The bins and tables carry a NEGATIVE scale (-0.29). PhysX cannot build a convex hull from
    ///      a mirrored mesh, and a Rigidbody on a non-convex MeshCollider logs an error every frame
    ///      and falls through the floor. A BoxCollider does not care which way the mesh is wound.
    ///   2. The cones have no collider at all. They are _LOD1 decoration; the car drives through
    ///      them today. A box fitted to the rendered bounds gives them a body for the first time.
    /// The box is fitted in the prop's local space from the mesh bounds, so it is right whatever
    /// the scale sign, and the old MeshCollider is disabled rather than destroyed so this is
    /// reversible with Undo and the original data is still there.
    ///
    /// MASS
    /// The car is 900 kg. Masses here are in that unit so the contact looks right: a bin at 40 kg
    /// goes flying, a toilet at 150 kg lurches and tips, a cone at 4 kg barely registers. Nothing
    /// here should ever stop the car, which is the whole point; walls and buildings stay static
    /// and still do.
    ///
    /// SLEEP
    /// Every body starts asleep. 54 bodies at rest cost nothing; 54 awake bodies all settling on
    /// the first frame cost a hitch. Unity sleeps them again a few seconds after they stop.
    ///
    /// THE TOOL SAVES THE SCENE
    /// Pressing Play snapshots the saved scene and leaving Play restores it. The first version only
    /// marked the scene dirty, so a bake followed by a test drive was silently undone by the test
    /// drive, with the console still reporting success. Now it writes the file.
    ///
    /// The KnockableProp marker it adds lives in the runtime folder, not here: a component
    /// serialised into a scene has to exist in the player build.
    ///
    /// Tools > Zoom Zoom > Make Props Knockable      bakes the selected families in the open scene
    /// Tools > Zoom Zoom > Undo Knockable Props      strips what the baker added, re-enables the old colliders
    /// </summary>
    public static class KnockablePropBaker
    {
        /// <summary>One line per prop family: a name pattern, a mass, and whether the box should be
        /// shrunk slightly so neighbours do not start overlapped and explode apart on frame one.</summary>
        private struct Family
        {
            public string Label;
            public Regex Name;
            public float MassKg;
            public float Shrink;
        }

        // The first playtest set, chosen on 12 October: four families, 46 objects. Lamps, fences,
        // traffic lights, bus stops and mailboxes are deliberately NOT here yet. If these four feel
        // right, extend the list; if the car gets caught on tumbling furniture, cut it.
        private static readonly Family[] Families =
        {
            new Family { Label = "trash container", Name = new Regex(@"^trashcontainer", RegexOptions.IgnoreCase), MassKg = 40f,  Shrink = 0.95f },
            new Family { Label = "picnic table",    Name = new Regex(@"^picnictable",    RegexOptions.IgnoreCase), MassKg = 60f,  Shrink = 0.95f },
            new Family { Label = "mobile toilet",   Name = new Regex(@"^mobile_toilet",  RegexOptions.IgnoreCase), MassKg = 150f, Shrink = 0.97f },
            new Family { Label = "traffic cone",    Name = new Regex(@"^cone_type",      RegexOptions.IgnoreCase), MassKg = 4f,   Shrink = 1.0f },
        };

        [MenuItem("Tools/Zoom Zoom/Make Props Knockable", priority = 60)]
        public static void Bake()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var counts = new Dictionary<string, int>();
            int skipped = 0;

            Undo.SetCurrentGroupName("Make Props Knockable");
            int group = Undo.GetCurrentGroup();

            foreach (GameObject go in scene.GetRootGameObjects())
            {
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    Family? match = Match(t.name);
                    if (match == null) continue;

                    if (t.GetComponent<KnockableProp>() != null) { skipped++; continue; }
                    if (!MakeKnockable(t.gameObject, match.Value)) { skipped++; continue; }

                    counts.TryGetValue(match.Value.Label, out int n);
                    counts[match.Value.Label] = n + 1;
                }
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);

            // Save, do not just mark dirty. The first version left this to the user and the
            // result was a bake that vanished: pressing Play snapshots the SAVED scene, and leaving
            // Play restores that snapshot, so an unsaved bake was undone by the first test drive
            // while the console still said it was done. A tool whose work can be lost by the very
            // next thing anyone does is not finished until it has written the file.
            bool saved = EditorSceneManager.SaveScene(scene);

            var sb = new System.Text.StringBuilder("[KnockablePropBaker] ");
            int total = 0;
            foreach (var kv in counts) { sb.Append($"{kv.Value} {kv.Key}, "); total += kv.Value; }
            sb.Append($"{total} props made knockable in '{scene.name}'");
            if (skipped > 0) sb.Append($", {skipped} skipped (already done or no renderer)");
            sb.Append(saved
                ? ". Scene saved; it will survive Play. Ctrl+Z then save again to revert."
                : ". SCENE NOT SAVED: Unity refused the save. Press Ctrl+S before pressing Play or this is lost.");
            if (saved) Debug.Log(sb.ToString()); else Debug.LogWarning(sb.ToString());
        }

        [MenuItem("Tools/Zoom Zoom/Undo Knockable Props", priority = 61)]
        public static void Strip()
        {
            var scene = EditorSceneManager.GetActiveScene();
            int n = 0;

            Undo.SetCurrentGroupName("Undo Knockable Props");
            int group = Undo.GetCurrentGroup();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (KnockableProp marker in root.GetComponentsInChildren<KnockableProp>(true))
                {
                    GameObject go = marker.gameObject;
                    foreach (var rb in go.GetComponents<Rigidbody>()) Undo.DestroyObjectImmediate(rb);
                    foreach (var box in go.GetComponents<BoxCollider>()) Undo.DestroyObjectImmediate(box);
                    foreach (var mesh in go.GetComponents<MeshCollider>())
                    {
                        Undo.RecordObject(mesh, "re-enable mesh collider");
                        mesh.enabled = true;
                    }
                    Undo.DestroyObjectImmediate(marker);
                    n++;
                }
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

        private static bool MakeKnockable(GameObject go, Family family)
        {
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;

            // The old collider stays on the object, switched off. Destroying it would make the
            // bake irreversible once the Undo history is gone.
            foreach (var mesh in go.GetComponents<MeshCollider>())
            {
                Undo.RecordObject(mesh, "disable mesh collider");
                mesh.enabled = false;
            }

            // Box from the mesh's own bounds, in local space, so a mirrored scale does not matter:
            // the box is expressed before scale is applied and Unity scales it with the transform.
            Bounds b = filter.sharedMesh.bounds;
            var box = Undo.AddComponent<BoxCollider>(go);
            box.center = b.center;
            box.size = b.size * family.Shrink;

            var rb = Undo.AddComponent<Rigidbody>(go);
            rb.mass = family.MassKg;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.8f;      // settles tumbling furniture rather than letting it spin
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.sleepThreshold = 0.05f;
            rb.Sleep();

            var marker = Undo.AddComponent<KnockableProp>(go);
            marker.family = family.Label;
            marker.bakedMassKg = family.MassKg;

            return true;
        }
    }
}
