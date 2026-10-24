using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomZoom.Vehicle.Editor
{
    /// <summary>Real Unity physics, input rhythm and scene wiring checks. Does not save scenes.</summary>
    public static class CrazyTaxiValidation
    {
        private static readonly List<string> Passed = new List<string>();
        private static VehicleController _car;
        private static bool _continueWithSmoke;
        private const float Dt = 1f / 120f;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Passed.Add(message);
            Debug.Log("[Taxi validation] PASS " + message);
        }
        private static void Call(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

        [MenuItem("Tools/Zoom Zoom/Validate Crazy Taxi Controller")]
        public static void Run()
        {
            SimulationMode previous = Physics.simulationMode;
            float previousStep = Time.fixedDeltaTime;
            SceneSetup[] scenes = EditorSceneManager.GetSceneManagerSetup();
            if (!Application.isBatchMode)
            {
                foreach (SceneSetup setup in scenes)
                    if (string.IsNullOrEmpty(setup.path) || EditorSceneManager.GetSceneByPath(setup.path).isDirty)
                    {
                        Debug.LogError("Save your open scenes before running physics validation, or run the checks in an isolated project.");
                        return;
                    }
            }
            bool success = false;
            try
            {
                Passed.Clear();
                TestRhythm();
                // Transient scene; saved scene setup is restored afterwards. No scene is saved here.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                Physics.simulationMode = SimulationMode.Script;
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Taxi test floor";
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(1000f, 1f, 1000f);
                GameObject car = new GameObject("Taxi test car", typeof(Rigidbody), typeof(BoxCollider));
                car.GetComponent<BoxCollider>().size = new Vector3(1.8f, 0.6f, 3f);
                _car = car.AddComponent<VehicleController>();
                var tuning = ScriptableObject.CreateInstance<VehicleTuning>();
                tuning.name = "Crazy Taxi test";
                tuning.taxi = new CrazyTaxiSettings();
                var serialized = new SerializedObject(_car);
                serialized.FindProperty("tuning").objectReferenceValue = tuning;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Call(_car, "Awake");
                TestPhysics();
                TestCamera();
                TestInputAsset();
                TestPedalTravel();
                UnityEngine.Object.DestroyImmediate(tuning);
                EditorSceneManager.CloseScene(scene, true);
                success = true;
            }
            catch (Exception error) { Debug.LogException(error); Passed.Add("FAIL: " + error); }
            finally
            {
                Physics.simulationMode = previous;
                Time.fixedDeltaTime = previousStep;
                bool canRestore = scenes.Length > 0;
                foreach (SceneSetup setup in scenes) canRestore &= !string.IsNullOrEmpty(setup.path);
                if (canRestore) EditorSceneManager.RestoreSceneManagerSetup(scenes);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string reportDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../VehicleLab_Measurements"));
                Directory.CreateDirectory(reportDirectory);
                File.WriteAllLines(Path.Combine(reportDirectory, "crazy_taxi_validation.txt"), Passed);
                if (Application.isBatchMode)
                {
                    if (success && _continueWithSmoke) EditorApplication.delayCall += CrazyTaxiPlaySmoke.Run;
                    else EditorApplication.Exit(success ? 0 : 1);
                }
            }
        }
        public static void RunAndSmoke()
        {
            _continueWithSmoke = true;
            Run();
        }
        private static void TestRhythm()
        {
            var r = new TaxiTrickRecognizer(0.3, 0.008);
            TaxiTrick Push(TaxiCommand command, double time, bool steer = false, bool fast = false, bool grounded = true) =>
                r.Push(command, time, grounded, true, steer, fast);
            Push(TaxiCommand.Drive, 1);
            Check(Push(TaxiCommand.GasDown, 1.06) == TaxiTrick.Dash, "Drive re-selection from idle recognises dash");
            Check(Push(TaxiCommand.GasDown, 1.07) == TaxiTrick.None, "Held gas cannot repeat a dash");
            Push(TaxiCommand.GasUp, 1.1);
            Push(TaxiCommand.Reverse, 1.13, fast: true);
            Push(TaxiCommand.Drive, 1.18, fast: true);
            Check(Push(TaxiCommand.GasDown, 1.24, fast: true) == TaxiTrick.LimiterCut, "Limiter cut chains a live dash");
            r.Reset();
            Push(TaxiCommand.Drive, 2);
            Check(Push(TaxiCommand.GasDown, 2) == TaxiTrick.None, "Simultaneous gear and gas do not dash");
            r.Reset();
            Push(TaxiCommand.Drive, 3);
            Check(Push(TaxiCommand.GasDown, 3.5) == TaxiTrick.None, "Expired gear input does not dash");
            r.Reset();
            Push(TaxiCommand.GasDown, 4);
            Push(TaxiCommand.Drive, 4.1);
            Check(Push(TaxiCommand.GasDown, 4.16) == TaxiTrick.None, "Gear with gas already held cannot dash");
            r.Reset();
            Push(TaxiCommand.Reverse, 5);
            Check(Push(TaxiCommand.Drive, 5.04, steer: true) == TaxiTrick.Drift, "Ordered reverse-drive with steering recognises drift");
            Check(Push(TaxiCommand.Reverse, 5.1, steer: true) == TaxiTrick.BackDrift, "Drive-reverse with steering recognises back drift");
            r.Reset();
            Push(TaxiCommand.Reverse, 6);
            Check(Push(TaxiCommand.BrakeDown, 6.04) == TaxiTrick.Stop, "Reverse plus brake recognises stop");
            r.Reset();
            Push(TaxiCommand.Drive, 7);
            Push(TaxiCommand.GasDown, 7.04);
            Check(Push(TaxiCommand.Reverse, 7.1, fast: true) == TaxiTrick.BackDash, "Reverse after dash recognises back dash");
            r.Reset();
            Push(TaxiCommand.Drive, 8, grounded: false);
            Check(Push(TaxiCommand.GasDown, 8.04, grounded: false) == TaxiTrick.None, "Airborne input cannot create a dash");
            r.Reset();
            Push(TaxiCommand.BrakeDown, 9);
            Push(TaxiCommand.Drive, 9.03);
            Check(Push(TaxiCommand.GasDown, 9.08) == TaxiTrick.None, "Held brake prevents dash");
            r.Reset();
            Push(TaxiCommand.Reverse, 10);
            Push(TaxiCommand.Drive, 10.05);
            Check(Push(TaxiCommand.Steer, 10.1, steer: true) == TaxiTrick.Drift,
                "Steering immediately after the gear flick can start a drift");
            Check(Push(TaxiCommand.QuickSpin, 11) == TaxiTrick.QuickSpin && r.Gear == TaxiGear.Drive,
                "Modern quick spin deliberately selects Drive");
        }
        private static void ResetCar()
        {
            _car.Teleport(new Vector3(0f, 0.55f, 0f), Quaternion.identity);
            Tick(0.4f);
        }
        private static void Tick(float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / Dt); i++)
            {
                Physics.SyncTransforms();
                Call(_car, "FixedUpdate");
                Physics.Simulate(Dt);
            }
        }
        private static void TestPhysics()
        {
            ResetCar();
            _car.Drive = new DriveInput { throttle = 1f };
            Tick(4f);
            Check(_car.Speed > 26f && _car.Speed < 29f, "Normal acceleration reaches cruise speed without overshoot");
            _car.Drive = new DriveInput { brake = 1f };
            Tick(2f);
            Check(_car.Speed < 0.2f && _car.Body.linearVelocity.z > -0.1f, "Held brake stops without reversing");
            ResetCar();
            _car.Drive = new DriveInput { throttle = -1f };
            Tick(3f);
            Check(_car.Body.linearVelocity.z < -10f && _car.Speed < 13f, "Reverse pedal uses reverse speed ceiling");
            ResetCar();
            _car.Drive = new DriveInput { throttle = 1f };
            _car.QueueTaxiCommand(TaxiCommand.Drive, 20);
            _car.QueueTaxiCommand(TaxiCommand.GasDown, 20.06);
            Tick(0.6f);
            Check(_car.Speed > 30f && _car.ActiveTrick == TaxiTrick.Dash, "Crazy Dash accelerates beyond cruise speed");
            _car.QueueTaxiCommand(TaxiCommand.GasUp, 20.65);
            _car.QueueTaxiCommand(TaxiCommand.Reverse, 20.69);
            _car.QueueTaxiCommand(TaxiCommand.Drive, 20.73);
            _car.QueueTaxiCommand(TaxiCommand.GasDown, 20.79);
            Tick(0.25f);
            Check(_car.ActiveTrick == TaxiTrick.LimiterCut && _car.Speed > 38f, "Limiter cut adds speed above dash ceiling");
            ResetCar();
            _car.Body.linearVelocity = Vector3.forward * 20f;
            _car.Drive = new DriveInput { steer = 1f };
            _car.QueueTaxiCommand(TaxiCommand.Reverse, 30);
            _car.QueueTaxiCommand(TaxiCommand.Drive, 30.06);
            Tick(0.4f);
            Check(_car.IsTaxiDrifting && Quaternion.Angle(_car.transform.rotation, Quaternion.identity) > 15f && _car.Speed > 12f,
                "Drift rotates the chassis while retaining travel momentum");
            _car.Drive = new DriveInput { throttle = 1f };
            _car.QueueTaxiCommand(TaxiCommand.Drive, 30.5);
            _car.QueueTaxiCommand(TaxiCommand.GasDown, 30.56);
            Tick(Dt);
            Check(_car.ActiveTrick == TaxiTrick.Dash, "Dash cancels drift into an exit launch");
            ResetCar();
            _car.Body.linearVelocity = Vector3.forward * 28f;
            _car.Drive = new DriveInput { brake = 1f };
            _car.QueueTaxiCommand(TaxiCommand.Reverse, 40);
            _car.QueueTaxiCommand(TaxiCommand.BrakeDown, 40.05);
            Tick(0.4f);
            Check(_car.Speed < 0.5f, "Crazy Stop arrests forward travel quickly");
            ResetCar();
            _car.Drive = new DriveInput { throttle = 1f };
            _car.QueueTaxiCommand(TaxiCommand.Drive, 50);
            _car.QueueTaxiCommand(TaxiCommand.GasDown, 50.06);
            Tick(0.55f);
            _car.Drive = new DriveInput { throttle = -1f };
            _car.QueueTaxiCommand(TaxiCommand.Reverse, 50.6);
            Tick(0.1f);
            Check(_car.ActiveTrick == TaxiTrick.BackDash && _car.Body.linearVelocity.z < -15f,
                "Back Dash creates a hasty retreat rather than merely braking forward motion");
            ResetCar();
            _car.Body.linearVelocity = Vector3.forward * 20f;
            _car.Drive = new DriveInput { steer = -1f, throttle = -1f };
            _car.QueueTaxiCommand(TaxiCommand.Drive, 60);
            _car.QueueTaxiCommand(TaxiCommand.Reverse, 60.05);
            Tick(0.4f);
            Check(_car.ActiveTrick == TaxiTrick.BackDrift && _car.SelectedGear == TaxiGear.Reverse &&
                Quaternion.Angle(_car.transform.rotation, Quaternion.identity) > 15f,
                "Back Drift rotates while leaving Reverse selected");
            ResetCar();
            _car.Body.linearVelocity = Vector3.forward * 12f;
            _car.Drive = new DriveInput { steer = -1f };
            _car.QueueTaxiCommand(TaxiCommand.QuickSpin, 70);
            Tick(0.88f);
            float spinAngle = Vector3.Angle(_car.transform.forward, Vector3.back);
            Check(spinAngle < 15f && _car.SelectedGear == TaxiGear.Drive,
                "Quick spin turns approximately 180 degrees and leaves forward driving selected");
            Check(_car.QuickSpinCooldownRemaining > 0f && !_car.CanQuickSpinNow,
                "Quick spin cooldown prevents repeated instant turnarounds");
            ResetCar();
            _car.Teleport(new Vector3(0, 20, 0), Quaternion.identity);
            _car.Drive = new DriveInput { throttle = 1f, steer = 1f, boost = true };
            Tick(0.2f);
            Check(Mathf.Abs(_car.Body.linearVelocity.x) < 0.01f && Mathf.Abs(_car.Body.linearVelocity.z) < 0.01f &&
                _car.Body.angularVelocity.magnitude < 0.01f && !_car.IsBoosting,
                "Airborne car is ballistic: no rocket thrust or air steering");
            ResetCar();
        }
        private static void TestCamera()
        {
            var go = new GameObject("Taxi test camera", typeof(Camera));
            var camera = go.AddComponent<ChaseCamera>();
            var so = new SerializedObject(camera);
            so.FindProperty("target").objectReferenceValue = _car;
            so.ApplyModifiedPropertiesWithoutUndo();
            Call(camera, "Awake");
            camera.SnapToTarget();
            Check(Vector3.Dot(camera.transform.forward, _car.transform.forward) > 0.8f, "Default camera faces the road ahead");
            _car.Body.linearVelocity = Vector3.back * 8f;
            camera.RenderCamera(Dt);
            Check(Vector3.Dot(camera.transform.forward, _car.transform.forward) > 0.8f, "Reversing keeps stable forward-facing framing");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 3, -3);
            wall.transform.localScale = new Vector3(20, 10, 0.5f);
            Physics.SyncTransforms();
            camera.SnapToTarget();
            Check(camera.transform.position.z > -2.5f, "Camera pulls in before an obstructing wall");
            UnityEngine.Object.DestroyImmediate(wall);
            Vector3[] positions = new Vector3[3];
            int[] rates = { 30, 60, 144 };
            for (int j = 0; j < rates.Length; j++)
            {
                _car.Teleport(new Vector3(0, 0.55f, 0), Quaternion.identity);
                camera.SnapToTarget();
                _car.transform.rotation = Quaternion.Euler(0, 90, 0);
                for (int frame = 0; frame < rates[j]; frame++) camera.RenderCamera(1f / rates[j]);
                positions[j] = camera.transform.position;
            }
            Check(Vector3.Distance(positions[0], positions[2]) < 0.15f,
                "Camera follow agrees at 30 and 144 frames per second");
            _car.Teleport(new Vector3(50, 0.55f, 50), Quaternion.Euler(0, 35, 0));
            camera.SnapToTarget();
            Check(Vector3.Distance(camera.transform.position, _car.transform.position) < 8f,
                "Teleport snaps the camera instead of dragging it across the map");
        }
        private static void TestInputAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/Project/Scripts/Vehicle/VehicleControls.inputactions");
            var map = asset.FindActionMap("Vehicle", true);
            var fallback = VehicleInput.BuildFallbackControls();
            Check(map.actions.Count == 8 && map.FindAction("Boost") == null && map.FindAction("Jump") == null &&
                map.FindAction("Flip") == null, "Input asset has eight taxi actions and no Rocket League actions");
            Check(map.bindings.Count == fallback.FindActionMap("Vehicle").bindings.Count,
                "Fallback input layout has the same binding count as the asset");
            UnityEngine.Object.DestroyImmediate(fallback);
            foreach (var path in new[] { "Assets/Project/Scenes/Possible game scene.unity", "Assets/Project/Scenes/VehicleLab.unity" })
            {
                string scene = File.ReadAllText(path);
                Check(scene.Contains("1a2b3c4d5e6f708192a3b4c5d6e7f803"), path + " retains its vehicle controller reference");
            }
        }
        private static void TestPedalTravel()
        {
            var settings = new CrazyTaxiSettings();
            Check(settings.MapPedal(0.02f, 0.04f, settings.acceleratorTravel) == 0f,
                "Trigger deadzone prevents unrequested creep");
            float quarter = settings.MapPedal(0.25f, 0.04f, settings.acceleratorTravel);
            float half = settings.MapPedal(0.5f, 0.04f, settings.acceleratorTravel);
            float full = settings.MapPedal(1f, 0.04f, settings.acceleratorTravel);
            Check(quarter > 0f && quarter < half && half < full && Mathf.Abs(full - 1f) < 0.001f,
                "Quarter, half and full trigger travel produce distinct proportional outputs");
            var soft = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 0.2f), new Keyframe(1, 1));
            Check(settings.MapPedal(0.5f, 0f, soft) < settings.MapPedal(0.5f, 0f, settings.acceleratorTravel),
                "Authored pedal curve changes partial trigger response independently of full travel");
            settings.brakeTravel = soft;
            Check(Mathf.Abs(settings.MapPedal(1, 0.04f, settings.brakeTravel) - 1f) < 0.001f,
                "Custom brake curve retains full braking at full trigger travel");
        }
    }
}
