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
        private const string CarMaterialPath = "Assets/_Project/Materials/Lab_Car.mat";
        private const string NoseMaterialPath = "Assets/_Project/Materials/Lab_CarNose.mat";

        [MenuItem("Tools/Zoom Zoom/Open VehicleLab Scene", priority = 0)]
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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

            AddVisual(go.transform, "Body Visual", new Vector3(0f, 0.2f, 0f),
                new Vector3(1.8f, 1f, 4.2f), CarMaterialPath);

            // A stripe on the nose. Sounds trivial, but when the car is sliding you need to be able
            // to see at a glance which way it is pointed as opposed to which way it is going.
            AddVisual(go.transform, "Nose Marker", new Vector3(0f, 0.75f, 1.5f),
                new Vector3(0.7f, 0.15f, 1f), NoseMaterialPath);

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

        private static void AddVisual(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, string materialPath)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = name;

            // The visual has no collider. The one BoxCollider on the car root is the whole physics
            // shape, so there is exactly one place to look when collisions behave oddly.
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            visual.transform.localRotation = Quaternion.identity;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null) visual.GetComponent<MeshRenderer>().sharedMaterial = material;
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
