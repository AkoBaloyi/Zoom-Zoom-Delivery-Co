using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace ZoomZoom.Vehicle.EditorTools
{
    /// <summary>
    /// Turns everything the car and the lab used to conjure up at runtime into saved assets.
    ///
    /// WHY THIS MATTERS MORE THAN IT SOUNDS
    /// `new Material(shader)` in a builder has three costs. A fresh instance is leaked on every Play,
    /// because a material created in code is owned by nothing and never unloaded. Nobody can art-pass
    /// the game without editing C#, which shuts Zubuhle out of his own job. And the scene is empty in
    /// the editor until Play is pressed, so there is nothing to light, nothing to frame a camera
    /// against, and nothing to review in a pull request.
    ///
    /// The builders keep their runtime fallback on purpose. A teammate who pulls this branch and has not
    /// run the baker still gets a working scene rather than a magenta one. Baking is an improvement, not
    /// a prerequisite.
    ///
    /// Tools > Zoom Zoom > Bake Lab Assets      writes the .mat files and assigns them
    /// Tools > Zoom Zoom > Bake Lab Into Scene  turns the generated geometry into saved scene objects
    /// Tools > Zoom Zoom > Save Car As Prefab   writes the car out as a reusable prefab
    /// </summary>
    public static class VehicleAssetBaker
    {
        private const string MaterialFolder = "Assets/_Project/Materials";
        private const string PrefabFolder = "Assets/_Project/Prefabs";

        // ==================================================================
        // MATERIALS
        // ==================================================================

        [MenuItem("Tools/Zoom Zoom/Bake Lab Assets", priority = 40)]
        public static void BakeMaterials()
        {
            EnsureFolder(MaterialFolder);

            var written = new List<string>();

            // ---- lab ----
            Material floor = OpaqueMaterial("Lab_Floor", VehicleLabBuilder.FloorColour, 0.05f, written);
            Material minor = OpaqueMaterial("Lab_MarkerMinor", VehicleLabBuilder.MinorMarkerColour, 0.05f, written);
            Material major = OpaqueMaterial("Lab_MarkerMajor", VehicleLabBuilder.MajorMarkerColour, 0.05f, written);
            Material wall = OpaqueMaterial("Lab_Wall", VehicleLabBuilder.WallColour, 0.05f, written);
            Material cone = OpaqueMaterial("Lab_Cone", VehicleLabBuilder.ConeColour, 0.05f, written);
            Material start = OpaqueMaterial("Lab_StartLine", VehicleLabBuilder.StartLineColour, 0.05f, written);

            // ---- effects ----
            Material skid = TransparentMaterial("FX_SkidMark", written);
            Material dust = TransparentMaterial("FX_TyreParticles", written);

            // ---- wire the lab ----
            VehicleLabBuilder lab = FindLab();
            SurfaceKind[] kinds = lab != null && lab.PatchKinds != null
                ? lab.PatchKinds
                : new[] { SurfaceKind.Wet, SurfaceKind.Dirt, SurfaceKind.Grass, SurfaceKind.Ice, SurfaceKind.Metal };

            var surfaceMaterials = new Material[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                surfaceMaterials[i] = OpaqueMaterial(
                    $"Surface_{kinds[i]}", VehicleLabBuilder.SurfaceColour(kinds[i]), 0.05f, written);
            }

            if (lab != null)
            {
                var so = new SerializedObject(lab);
                so.FindProperty("floorMaterialAsset").objectReferenceValue = floor;
                so.FindProperty("minorMarkerMaterialAsset").objectReferenceValue = minor;
                so.FindProperty("majorMarkerMaterialAsset").objectReferenceValue = major;
                so.FindProperty("wallMaterialAsset").objectReferenceValue = wall;
                so.FindProperty("coneMaterialAsset").objectReferenceValue = cone;
                so.FindProperty("startLineMaterialAsset").objectReferenceValue = start;

                SerializedProperty array = so.FindProperty("surfaceMaterialAssets");
                array.arraySize = surfaceMaterials.Length;
                for (int i = 0; i < surfaceMaterials.Length; i++)
                {
                    array.GetArrayElementAtIndex(i).objectReferenceValue = surfaceMaterials[i];
                }

                so.ApplyModifiedProperties();
            }

            // ---- wire the car ----
            CarVisualBuilder car = FindCarVisualBuilder();
            if (car != null)
            {
                VehicleTuning tuning = car.GetComponent<VehicleController>()?.Tuning;

                Material body = OpaqueMaterial("Car_Body", tuning?.bodyColour ?? Color.yellow, 0.35f, written);
                Material trim = OpaqueMaterial("Car_Trim", tuning?.trimColour ?? Color.black, 0.2f, written);
                Material glass = OpaqueMaterial("Car_Glass", tuning?.glassColour ?? Color.grey, 0.85f, written);
                Material tyre = OpaqueMaterial("Car_Tyre", tuning?.tyreColour ?? Color.black, 0.15f, written);
                Material rim = OpaqueMaterial("Car_Rim", tuning?.rimColour ?? Color.white, 0.6f, written);
                Material head = OpaqueMaterial("Car_Headlight", CarVisualBuilder.HeadlightColour, 0.8f, written);
                Material brake = OpaqueMaterial(
                    "Car_BrakeLight", tuning?.brakeLightOffColour ?? Color.red, 0.5f, written);

                var so = new SerializedObject(car);
                so.FindProperty("bodyMaterialAsset").objectReferenceValue = body;
                so.FindProperty("trimMaterialAsset").objectReferenceValue = trim;
                so.FindProperty("glassMaterialAsset").objectReferenceValue = glass;
                so.FindProperty("tyreMaterialAsset").objectReferenceValue = tyre;
                so.FindProperty("rimMaterialAsset").objectReferenceValue = rim;
                so.FindProperty("headlightMaterialAsset").objectReferenceValue = head;
                so.FindProperty("brakeLightMaterialAsset").objectReferenceValue = brake;
                so.ApplyModifiedProperties();
            }

            // ---- wire the effects ----
            TyreEffects effects = FindTyreEffects();
            if (effects != null)
            {
                var so = new SerializedObject(effects);
                so.FindProperty("particleMaterialAsset").objectReferenceValue = dust;
                so.ApplyModifiedProperties();
            }

            foreach (SkidMarks marks in Object.FindObjectsByType<SkidMarks>())
            {
                var so = new SerializedObject(marks);
                so.FindProperty("markMaterialAsset").objectReferenceValue = skid;
                so.ApplyModifiedProperties();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (lab != null || car != null) EditorSceneManager.MarkAllScenesDirty();

            Debug.Log(
                $"[Baker] {written.Count} material assets written to {MaterialFolder} and assigned:\n  " +
                string.Join("\n  ", written) +
                "\n\nThese are ordinary assets now. Recolour them in the inspector and the change sticks, " +
                "no code edit and no rebuild. Save the scene to keep the assignments.");
        }

        // ==================================================================
        // LAB GEOMETRY
        // ==================================================================

        [MenuItem("Tools/Zoom Zoom/Bake Lab Into Scene", priority = 41)]
        public static void BakeLabGeometry()
        {
            VehicleLabBuilder lab = FindLab();

            if (lab == null)
            {
                EditorUtility.DisplayDialog(
                    "No lab found",
                    "Open the VehicleLab scene first. Tools > Zoom Zoom > Open VehicleLab Scene.",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Bake the lab into the scene?",
                    "The floor, markers, wall, cones and surface strips become ordinary saved objects, " +
                    "and Build On Awake is switched off so they are not thrown away and regenerated " +
                    "every time you press Play.\n\n" +
                    "Run Bake Lab Assets first if you have not, or the baked geometry will reference " +
                    "materials that only existed at runtime.\n\n" +
                    "Re-baking replaces whatever is there now.",
                    "Bake it", "Cancel"))
            {
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(lab.gameObject, "Bake lab into scene");

            // Build() clears and regenerates, so this is safe to run repeatedly.
            lab.Build();

            // The generated objects are real scene objects already. The only thing standing between
            // them and being saved is that Awake would wipe them, so that is what gets turned off.
            var so = new SerializedObject(lab);
            so.FindProperty("buildOnAwake").boolValue = false;
            so.ApplyModifiedProperties();

            int count = lab.GetComponentsInChildren<Transform>(true).Length - 1;

            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log(
                $"[Baker] Lab baked: {count} objects are now saved scene content, and Build On Awake is " +
                "off. Save the scene. The lab is visible in the editor without pressing Play, which " +
                "means it can be lit, framed and reviewed like any other level geometry.");
        }

        // ==================================================================
        // CAR PREFAB
        // ==================================================================

        [MenuItem("Tools/Zoom Zoom/Save Car As Prefab", priority = 42)]
        public static void SaveCarPrefab()
        {
            VehicleController car = Object.FindAnyObjectByType<VehicleController>();

            if (car == null)
            {
                EditorUtility.DisplayDialog(
                    "No car found",
                    "Open a scene containing the car first.",
                    "OK");
                return;
            }

            EnsureFolder(PrefabFolder);

            string path = $"{PrefabFolder}/Car.prefab";

            if (System.IO.File.Exists(path) && !EditorUtility.DisplayDialog(
                    "Replace the car prefab?",
                    $"{path} already exists and will be overwritten.",
                    "Replace it", "Cancel"))
            {
                return;
            }

            // The visual model is generated under the car at runtime, so anything baked into the prefab
            // now would be duplicated by the builder on the next Play. Stripping it keeps the prefab to
            // what it should be: the components and their tuning, not a snapshot of generated geometry.
            Transform generated = car.transform.Find("Generated (do not edit by hand)");
            bool hadGenerated = generated != null;

            if (hadGenerated) Object.DestroyImmediate(generated.gameObject);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(car.gameObject, path, out bool success);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (success)
            {
                Debug.Log(
                    $"[Baker] Car saved to {path}. Drop it into any scene and it arrives with its " +
                    "controller, input, visuals, tyre effects and telemetry already wired." +
                    (hadGenerated
                        ? " The generated visual model was stripped first so the prefab holds components " +
                          "rather than a stale snapshot of primitives."
                        : ""));
            }
            else
            {
                Debug.LogError($"[Baker] Could not save the prefab to {path}.");
            }
        }

        // ==================================================================
        // HELPERS
        // ==================================================================

        /// <summary>
        /// Opaque lit material. Written with the same shader fallback chain the runtime builders use, so
        /// a baked asset and a generated one look identical rather than subtly different.
        /// </summary>
        private static Material OpaqueMaterial(string name, Color colour, float smoothness, List<string> written)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError($"[Baker] No lit shader available, cannot write {name}.");
                return null;
            }

            var material = new Material(shader);
            SetColour(material, colour);

            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

            return Save(material, name, written);
        }

        /// <summary>Vertex-coloured, alpha-blended, unlit. For skid marks and tyre particles.</summary>
        private static Material TransparentMaterial(string name, List<string> written)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Sprites/Default")
                            ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");

            if (shader == null)
            {
                Debug.LogError($"[Baker] No particle shader available, cannot write {name}.");
                return null;
            }

            var material = new Material(shader);
            SetColour(material, Color.white);

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);

            material.renderQueue = 3000;

            return Save(material, name, written);
        }

        /// <summary>URP calls it _BaseColor, the built-in pipeline calls it _Color. Set whichever exists.</summary>
        private static void SetColour(Material material, Color colour)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        }

        /// <summary>
        /// Creates the material if it is missing, and otherwise leaves the existing asset completely
        /// alone, returning it so it can still be assigned.
        ///
        /// WHY AN EXISTING ASSET IS NEVER TOUCHED
        /// The first version of this overwrote the existing material with a freshly built one. That was
        /// wrong twice over.
        ///
        /// It was not idempotent. A newly constructed Material has none of the metadata Unity fills in
        /// when a material is imported, so copying its properties over an existing asset stripped
        /// `stringTagMap: RenderType: Opaque` and the disabled MOTIONVECTORS pass. Running the baker a
        /// second time quietly degraded every material it had made the first time.
        ///
        /// More importantly it defeated the entire purpose of having assets. The reason for baking these
        /// out was so Zubuhle could recolour the game without editing C#. An overwriting baker means his
        /// work is destroyed by whoever next runs the tool, which is worse than not having the assets at
        /// all because the loss is silent.
        ///
        /// So the rule is: the baker owns creation, the artist owns the asset thereafter. To reset one
        /// deliberately, delete it and re-run.
        /// </summary>
        private static Material Save(Material material, string name, List<string> written)
        {
            if (material == null) return null;

            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                // The freshly built one was only ever a template and is now redundant.
                Object.DestroyImmediate(material);

                written.Add($"{name}.mat (already existed, left untouched)");
                return existing;
            }

            material.name = name;
            AssetDatabase.CreateAsset(material, path);

            written.Add($"{name}.mat (created)");
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static VehicleLabBuilder FindLab() =>
            Object.FindAnyObjectByType<VehicleLabBuilder>();

        private static CarVisualBuilder FindCarVisualBuilder() =>
            Object.FindAnyObjectByType<CarVisualBuilder>();

        private static TyreEffects FindTyreEffects() =>
            Object.FindAnyObjectByType<TyreEffects>();
    }
}
