using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZoomZoom.Vehicle.Editor
{
    /// <summary>
    /// Attaches the two components that let the player SEE and HEAR the taxi controller.
    ///
    /// WHY THIS TOOL EXISTS
    /// For two weeks the game shipped a car that performed Crazy Taxi tricks silently. The
    /// recogniser, gearbox and pedal logic are plain classes owned by VehicleController through its
    /// partial files, so they were live in every scene the moment the controller was. But the two
    /// things that TELL the player a trick landed are MonoBehaviours - TaxiEngineAudio for the
    /// engine note and overdrive confirmation, CrazyTaxiFeedback for the on-screen trick and gear
    /// readout - and neither had been added to a Car anywhere. Every playtester reported the game
    /// "felt like the old one". It did not. It was the new one with the feedback unplugged.
    ///
    /// Both components are zero-configuration: each finds VehicleController on its own GameObject in
    /// Awake, the audio synthesises its own clips, and the HUD builds its own Canvas at runtime. So
    /// installing them is only ever "put them on the car", and this tool does that for the open
    /// scene, saves it, and reports exactly what it did so the result can be checked rather than
    /// trusted.
    ///
    /// Tools > Zoom Zoom > Install Taxi Feedback
    /// </summary>
    public static class TaxiFeedbackInstaller
    {
        [MenuItem("Tools/Zoom Zoom/Install Taxi Feedback", priority = 63)]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[TaxiFeedbackInstaller] No saved scene is open. Open one of the " +
                               "gameplay scenes first.");
                return;
            }

            Undo.SetCurrentGroupName("Install Taxi Feedback");
            int group = Undo.GetCurrentGroup();

            var cars = new List<VehicleController>();
            foreach (GameObject root in scene.GetRootGameObjects())
                cars.AddRange(root.GetComponentsInChildren<VehicleController>(true));

            if (cars.Count == 0)
            {
                Debug.LogError($"[TaxiFeedbackInstaller] No VehicleController in '{scene.name}'. " +
                               "Nothing to install onto.");
                return;
            }

            var report = new StringBuilder();
            int addedAudio = 0, addedHud = 0, alreadyAudio = 0, alreadyHud = 0;

            foreach (VehicleController car in cars)
            {
                GameObject go = car.gameObject;

                // One of each, never two. DisallowMultipleComponent on both classes would refuse a
                // duplicate anyway, but checking first keeps the report honest about what was done.
                if (go.GetComponent<TaxiEngineAudio>() == null)
                {
                    Undo.AddComponent<TaxiEngineAudio>(go);
                    addedAudio++;
                }
                else alreadyAudio++;

                if (go.GetComponent<CrazyTaxiFeedback>() == null)
                {
                    Undo.AddComponent<CrazyTaxiFeedback>(go);
                    addedHud++;
                }
                else alreadyHud++;

                report.Append("  ").Append(go.name)
                      .Append("  audio=").Append(go.GetComponent<TaxiEngineAudio>() != null ? "yes" : "NO")
                      .Append("  hud=").Append(go.GetComponent<CrazyTaxiFeedback>() != null ? "yes" : "NO")
                      .Append('\n');
            }

            Undo.CollapseUndoOperations(group);

            // Save, rather than leave it to the user. The prop baker learned this the hard way: an
            // unsaved bake looked done in the console and was silently undone the moment Play was
            // entered, because Play restores the saved scene.
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);

            bool changed = addedAudio + addedHud > 0;
            string headline =
                $"[TaxiFeedbackInstaller] {scene.name}: {cars.Count} car(s). " +
                $"Added TaxiEngineAudio x{addedAudio}, CrazyTaxiFeedback x{addedHud}; " +
                $"already present {alreadyAudio}/{alreadyHud}. " +
                (saved ? "Scene saved." : "SCENE NOT SAVED - save it by hand or the change is lost on Play.") +
                "\n" + report +
                (changed
                    ? "Press Play and drive: you should hear the engine, and see the gear and trick readout."
                    : "Nothing to do - both components were already on every car.");

            if (!saved) Debug.LogError(headline);
            else Debug.Log(headline);
        }

        /// <summary>
        /// Reports which gameplay scenes still lack the feedback, without opening or changing any
        /// of them. Reads the scene files as text so it is safe to run with any scene open.
        /// </summary>
        [MenuItem("Tools/Zoom Zoom/Check Taxi Feedback In All Scenes", priority = 64)]
        public static void CheckAll()
        {
            string audioGuid = GuidOf<TaxiEngineAudio>();
            string hudGuid = GuidOf<CrazyTaxiFeedback>();
            string carGuid = GuidOf<VehicleController>();

            var sb = new StringBuilder("[TaxiFeedbackInstaller] Feedback status per scene:\n");
            int missing = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Project/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string text = System.IO.File.ReadAllText(path);

                int cars = Count(text, carGuid);
                if (cars == 0) continue; // not a gameplay scene

                int audio = Count(text, audioGuid);
                int hud = Count(text, hudGuid);
                bool ok = audio >= cars && hud >= cars;
                if (!ok) missing++;

                sb.Append(ok ? "  ok       " : "  MISSING  ")
                  .Append(System.IO.Path.GetFileNameWithoutExtension(path))
                  .Append($"  cars={cars} audio={audio} hud={hud}\n");
            }

            sb.Append(missing == 0
                ? "Every gameplay scene has both components on every car."
                : $"{missing} scene(s) still need Tools > Zoom Zoom > Install Taxi Feedback run with that scene open.");

            if (missing > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        private static string GuidOf<T>() where T : MonoBehaviour
        {
            MonoScript script = FindScript(typeof(T));
            return script != null
                ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script))
                : string.Empty;
        }

        private static MonoScript FindScript(System.Type type)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:MonoScript {type.Name}"))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (ms != null && ms.GetClass() == type) return ms;
            }
            return null;
        }

        private static int Count(string text, string needle)
        {
            if (string.IsNullOrEmpty(needle)) return 0;
            int n = 0, i = 0;
            while ((i = text.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
            return n;
        }
    }
}
