using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomZoom.Vehicle.EditorTools
{
    /// <summary>
    /// Rebuilds the VehicleLab scene from scratch.
    ///
    /// WHY THIS EXISTS
    /// Two reasons, and both of them are practical rather than clever.
    ///
    ///  1. The lab is a measuring instrument. If somebody drags the car sideways, deletes the wall
    ///     anchor or unticks a reference while poking about, every reading afterwards is quietly
    ///     wrong. One menu click puts it back exactly as it was.
    ///
    ///  2. It is the layout written down in a form that cannot go stale. A scene file is not
    ///     readable and cannot be reviewed in a pull request. This can.
    ///
    /// Tools > Zoom Zoom > Rebuild VehicleLab Scene
    /// </summary>
    public static class VehicleLabSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/VehicleLab.unity";
        // The profiles live inside the Vehicle script folder rather than a folder of their own,
        // because the team repo layout fixes the eight folders under Assets/_Project and adding a
        // ninth would break the structure everyone else is working against.
        private const string TuningPath =
            "Assets/_Project/Scripts/Vehicle/Tuning/Tuning_Balanced.asset";
        private const string ControlsPath = "Assets/_Project/Scripts/Vehicle/VehicleControls.inputactions";
        // No material paths any more. The car model and its materials are built by CarVisualBuilder
        // from the colours in the tuning profile, so swapping profile changes the car's paint too.

        [MenuItem("Tools/Zoom Zoom/Open VehicleLab Scene", priority = 0)]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// Adds the visual components to a car that already exists in whatever scene is open, and
        /// clears out any old placeholder geometry.
        ///
        /// Separate from the full rebuild because a rebuild throws the scene away. This is the safe
        /// option: it changes the car and nothing else, so it can be run on a scene somebody has
        /// already put work into. It is also what Kyuri and Zubuhle will want when they drop the car
        /// into their own scenes.
        /// </summary>
        [MenuItem("Tools/Zoom Zoom/Add Car Visuals To Open Scene", priority = 10)]
        public static void AddCarVisualsToOpenScene()
        {
            var cars = Object.FindObjectsByType<VehicleController>();

            if (cars.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "No car found",
                    "There is no VehicleController in the open scene, so there is nothing to add " +
                    "visuals to.",
                    "OK");
                return;
            }

            int changed = 0;

            foreach (VehicleController car in cars)
            {
                GameObject go = car.gameObject;

                VehicleVisuals visuals = go.GetComponent<VehicleVisuals>();
                if (visuals == null)
                {
                    visuals = Undo.AddComponent<VehicleVisuals>(go);

                    var so = new SerializedObject(visuals);
                    so.FindProperty("car").objectReferenceValue = car;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed++;
                }

                if (go.GetComponent<CarVisualBuilder>() == null)
                {
                    Undo.AddComponent<CarVisualBuilder>(go);
                    changed++;
                }

                // Old greybox stand-ins. Named exactly, so nothing else gets caught by accident.
                foreach (string placeholder in new[] { "Body Visual", "Nose Marker" })
                {
                    Transform found = go.transform.Find(placeholder);
                    if (found == null) continue;

                    Undo.DestroyObjectImmediate(found.gameObject);
                    changed++;
                }

                EditorUtility.SetDirty(go);
            }

            if (changed == 0)
            {
                Debug.Log("[VehicleLab] Car visuals were already set up. Nothing to change.");
                return;
            }

            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log(
                $"[VehicleLab] Car visuals added, {changed} change(s). Save the scene, then press " +
                "Play. To see the model without playing, use the Build Model Now item in the " +
                "CarVisualBuilder component's context menu.");
        }

        [MenuItem("Tools/Zoom Zoom/Rebuild VehicleLab Scene", priority = 20)]
        public static void RebuildScene()
        {
            bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;

            if (exists && !EditorUtility.DisplayDialog(
                    "Rebuild VehicleLab?",
                    $"This replaces {ScenePath} with a freshly built copy.\n\n" +
                    "Anything you have added to that scene by hand will be lost. The tuning " +
                    "profiles and the scripts are not touched.",
                    "Rebuild it", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            VehicleTuning tuning = AssetDatabase.LoadAssetAtPath<VehicleTuning>(TuningPath);
            if (tuning == null)
            {
                Debug.LogWarning(
                    $"[VehicleLab] Could not find a tuning profile at {TuningPath}. The car will be " +
                    "built without one and you will need to assign it by hand.");
            }

            InputActionAsset controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);

            CreateLight();
            VehicleLabBuilder lab = CreateLab();
            VehicleController car = CreateCar(tuning, controls, lab.StartPoint);
            ChaseCamera chaseCamera = CreateCamera(car);
            CreateMeasurement(car, chaseCamera, lab);

            EditorSceneManager.MarkSceneDirty(scene);

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogError("[VehicleLab] Could not save the scene.");
                return;
            }

            RegisterInBuildSettings();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[VehicleLab] Rebuilt {ScenePath}. Press Play, then F1 to F6 to take readings. " +
                "The floor, markers, wall and cones are generated when the scene starts, which is " +
                "why the scene looks empty until you hit Play.");
        }

        // ==================================================================
        // PIECES
        // ==================================================================

        private static void CreateLight()
        {
            var go = new GameObject("Directional Light");
            go.transform.SetPositionAndRotation(
                new Vector3(0f, 20f, 0f),
                Quaternion.Euler(50f, -30f, 0f));

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.98f, 0.94f);
        }

        private static VehicleLabBuilder CreateLab()
        {
            var go = new GameObject("VehicleLab");
            VehicleLabBuilder lab = go.AddComponent<VehicleLabBuilder>();

            // The three reference points are real objects in the scene rather than generated ones,
            // because the measurement script has to be able to point at them from the inspector.
            Transform start = MakeChild(go.transform, "StartPoint", new Vector3(0f, 0.8f, 0f));
            Transform turn = MakeChild(go.transform, "TurnTestPoint", new Vector3(-70f, 0.8f, 80f));
            Transform wall = MakeChild(go.transform, "WallAnchor", new Vector3(0f, 0f, 200f));

            var so = new SerializedObject(lab);
            so.FindProperty("startPoint").objectReferenceValue = start;
            so.FindProperty("turnTestPoint").objectReferenceValue = turn;
            so.FindProperty("wallAnchor").objectReferenceValue = wall;
            so.ApplyModifiedPropertiesWithoutUndo();

            return lab;
        }

        private static VehicleController CreateCar(VehicleTuning tuning, InputActionAsset controls,
            Transform startPoint)
        {
            var go = new GameObject("Car") { tag = "Player" };
            go.transform.position = startPoint != null ? startPoint.position : new Vector3(0f, 0.8f, 0f);

            // The physics shape. Deliberately a plain box and deliberately not the visual mesh: the
            // car's collision behaviour should be something we chose, not something that fell out of
            // whatever model gets dropped in later.
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(1.8f, 1f, 4.2f);

            // Raised so the body sits above the ground at rest. The suspension holds the car up, and
            // if the collider were touching the floor as well the two would fight each other.
            box.center = new Vector3(0f, 0.2f, 0f);

            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = tuning != null ? tuning.mass : 900f;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.angularDamping = 2.5f;
            body.linearDamping = 0f;
            body.centerOfMass = tuning != null
                ? tuning.centreOfMassOffset
                : new Vector3(0f, -0.35f, 0f);

            VehicleController controller = go.AddComponent<VehicleController>();
            VehicleJumpFlip jumpFlip = go.AddComponent<VehicleJumpFlip>();
            VehicleInput input = go.AddComponent<VehicleInput>();
            VehicleVisuals visuals = go.AddComponent<VehicleVisuals>();
            go.AddComponent<CarVisualBuilder>();

            // Marks and tyre particles. Read-only like VehicleVisuals, so it cannot affect any
            // measurement, and it finds or creates the shared skid mark mesh itself at startup.
            go.AddComponent<TyreEffects>();

            // The grip readout. F9 hides it. This is the thing that turns "it did not feel right" into
            // "the steering asked for 14 and the rear only had 8", which is a fixable statement.
            go.AddComponent<VehicleTelemetry>();

            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("tuning").objectReferenceValue = tuning;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            var inputSo = new SerializedObject(input);
            inputSo.FindProperty("controlsAsset").objectReferenceValue = controls;
            inputSo.FindProperty("controller").objectReferenceValue = controller;
            inputSo.FindProperty("jumpFlip").objectReferenceValue = jumpFlip;
            inputSo.ApplyModifiedPropertiesWithoutUndo();

            if (controls == null)
            {
                Debug.LogWarning(
                    $"[VehicleLab] Could not find {ControlsPath}, so the input asset reference is " +
                    "empty. The car will still drive, using the same bindings built in code.");
            }

            var visualsSo = new SerializedObject(visuals);
            visualsSo.FindProperty("car").objectReferenceValue = controller;
            visualsSo.ApplyModifiedPropertiesWithoutUndo();

            // No visual geometry is created here on purpose. CarVisualBuilder makes the model when
            // the scene starts and hands the wheel transforms to VehicleVisuals itself, the same way
            // VehicleLabBuilder makes the lab. That keeps the shape of the car in readable code
            // instead of in a scene file nobody can review.

            return controller;
        }

        private static ChaseCamera CreateCamera(VehicleController car)
        {
            var go = new GameObject("Chase Camera") { tag = "MainCamera" };
            go.transform.SetPositionAndRotation(
                new Vector3(0f, 3.7f, -7f),
                Quaternion.Euler(9f, 0f, 0f));

            Camera camera = go.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;

            go.AddComponent<AudioListener>();

            ChaseCamera chase = go.AddComponent<ChaseCamera>();

            var so = new SerializedObject(chase);
            so.FindProperty("target").objectReferenceValue = car;
            so.FindProperty("input").objectReferenceValue = car.GetComponent<VehicleInput>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return chase;
        }

        private static void CreateMeasurement(VehicleController car, ChaseCamera chaseCamera,
            VehicleLabBuilder lab)
        {
            var go = new GameObject("Measurement");
            VehicleMeasurement measurement = go.AddComponent<VehicleMeasurement>();

            var so = new SerializedObject(measurement);
            so.FindProperty("car").objectReferenceValue = car;
            so.FindProperty("jumpFlip").objectReferenceValue = car.GetComponent<VehicleJumpFlip>();
            so.FindProperty("playerInput").objectReferenceValue = car.GetComponent<VehicleInput>();
            so.FindProperty("chaseCamera").objectReferenceValue = chaseCamera;
            so.FindProperty("startPoint").objectReferenceValue = lab.StartPoint;
            so.FindProperty("turnTestPoint").objectReferenceValue = lab.TurnTestPoint;
            so.FindProperty("wall").objectReferenceValue = lab.WallAnchor;

            // On in the lab, because reading the numbers while driving is the entire point of the
            // scene. It defaults to off everywhere else.
            so.FindProperty("showDeveloperOverlay").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ==================================================================
        // HELPERS
        // ==================================================================

        private static Transform MakeChild(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        /// <summary>
        /// Adds the lab to the build settings list but leaves it switched OFF. It shows up in the
        /// Build Settings window so it is easy to find, and it does not get shipped in a build.
        /// </summary>
        private static void RegisterInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            foreach (EditorBuildSettingsScene existing in scenes)
            {
                if (existing.path == ScenePath) return;
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, false));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
