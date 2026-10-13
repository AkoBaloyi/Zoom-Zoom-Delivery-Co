using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZoomZoom.Vehicle.EditorTools
{
    /// <summary>
    /// Labels the city's ground so the car knows what it is driving on.
    ///
    /// WHAT THE CAR ACTUALLY HITS
    /// The wheel rays return a collider, and ResolveSurface walks UP from that collider with
    /// GetComponentInParent looking for a SurfaceType. So the label has to sit on, or above, the
    /// object that OWNS the collider the ray lands on, and that is not always the object whose
    /// name you would tag. Reading the scene file settled it for this city:
    ///   - 581 of 582 road_base pieces have NO collider. The car drives on Roads_Parent, which
    ///     carries one merged MeshCollider for every road in the city. One SurfaceType on
    ///     Roads_Parent covers all of them; tagging the 582 children would do nothing.
    ///   - commercial_base and parkbase pieces each carry their own MeshCollider, so each one is
    ///     tagged individually.
    /// The tagger does not assume any of this. It tags by the rule above, then VERIFIES: it casts
    /// the car's own ground ray down onto every object it touched and reports which SurfaceKind
    /// came back. If the number verified is lower than the number tagged, the log says so and
    /// names the first few, because a tag the ray cannot see is not a tag.
    ///
    /// WEATHER
    /// Also drops a WeatherState into the scene if there is none, set to Dry, so the switch exists
    /// and changes nothing until someone flips it.
    ///
    /// Tools > Zoom Zoom > Tag City Surfaces
    /// </summary>
    public static class SurfaceTagger
    {
        private struct Rule
        {
            public Regex Name;
            public SurfaceKind Kind;
            public string Label;
        }

        private static readonly Rule[] Rules =
        {
            new Rule { Name = new Regex(@"^Roads_Parent$"),              Kind = SurfaceKind.Tarmac,   Label = "roads (merged collider on Roads_Parent)" },
            new Rule { Name = new Regex(@"^road_base(\.\d+)?$"),         Kind = SurfaceKind.Tarmac,   Label = "road_base pieces with their own collider" },
            new Rule { Name = new Regex(@"^commercial_base(\.\d+)?$"),   Kind = SurfaceKind.Concrete, Label = "commercial bases" },
            new Rule { Name = new Regex(@"^parkbase\d*(_sample)?(\.\d+)?$"), Kind = SurfaceKind.Grass, Label = "park bases" },
        };

        [MenuItem("Tools/Zoom Zoom/Tag City Surfaces", priority = 62)]
        public static void Tag()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Tag City Surfaces");
            int group = Undo.GetCurrentGroup();

            var tagged = new List<(Transform t, Rule r)>();
            var skippedNoCollider = new Dictionary<string, int>();

            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    Rule? rule = Match(t.name);
                    if (rule == null) continue;

                    // Only objects that OWN an enabled collider can be hit, so only they get a tag.
                    // Children without colliders inherit the parent's tag through
                    // GetComponentInParent anyway, and tagging them would be a false count.
                    bool hasCollider = false;
                    foreach (Collider c in t.GetComponents<Collider>())
                        if (c.enabled && !c.isTrigger) { hasCollider = true; break; }
                    if (!hasCollider)
                    {
                        skippedNoCollider.TryGetValue(rule.Value.Label, out int n);
                        skippedNoCollider[rule.Value.Label] = n + 1;
                        continue;
                    }

                    SurfaceType s = t.GetComponent<SurfaceType>();
                    if (s == null) s = Undo.AddComponent<SurfaceType>(t.gameObject);
                    else Undo.RecordObject(s, "retag surface");
                    s.kind = rule.Value.Kind;
                    s.ApplyDefaultsForKind();
                    tagged.Add((t, rule.Value));
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

            // VERIFY: fire the car's ray at each tagged object and see what comes back.
            Physics.SyncTransforms();
            var verified = new Dictionary<string, int>();
            var failed = new List<string>();
            foreach ((Transform t, Rule r) in tagged)
            {
                Bounds b = WorldBounds(t);
                Vector3 from = new Vector3(b.center.x, b.max.y + 5f, b.center.z);
                SurfaceKind? seen = null;
                foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.attachedRigidbody != null) continue; // ignore props
                    if (!h.collider.transform.IsChildOf(t) && h.collider.transform != t) continue;
                    var s = h.collider.GetComponentInParent<SurfaceType>();
                    seen = s != null ? s.kind : SurfaceKind.Tarmac;
                    break;
                }
                if (seen == r.Kind) { verified.TryGetValue(r.Label, out int n); verified[r.Label] = n + 1; }
                else failed.Add($"{t.name}: tagged {r.Kind}, ray saw {(seen.HasValue ? seen.Value.ToString() : "nothing")}");
            }

            var sb = new System.Text.StringBuilder("[SurfaceTagger] ");
            var perLabel = new Dictionary<string, int>();
            foreach ((Transform t, Rule r) in tagged) { perLabel.TryGetValue(r.Label, out int n); perLabel[r.Label] = n + 1; }
            foreach (var kv in perLabel)
            {
                verified.TryGetValue(kv.Key, out int v);
                sb.Append($"\n   {kv.Value,4} tagged, {v,4} verified by the car's own ray   {kv.Key}");
            }
            foreach (var kv in skippedNoCollider)
                sb.Append($"\n   {kv.Value,4} skipped (no collider of their own, covered by a parent)   {kv.Key}");
            sb.Append(addedWeather ? "\n   Weather object added, set to Dry (changes nothing until flipped)."
                                   : "\n   WeatherState already present, left alone.");
            sb.Append(saved ? "\n   Scene saved." : "\n   SCENE NOT SAVED: press Ctrl+S.");

            if (failed.Count > 0)
            {
                sb.Append($"\n{failed.Count} tags the car's ray does NOT see:");
                foreach (string f in failed.GetRange(0, Mathf.Min(8, failed.Count))) sb.Append("\n   " + f);
                Debug.LogWarning(sb.ToString());
            }
            else Debug.Log(sb.ToString());
        }

        private static Rule? Match(string name)
        {
            foreach (Rule r in Rules) if (r.Name.IsMatch(name)) return r;
            return null;
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
