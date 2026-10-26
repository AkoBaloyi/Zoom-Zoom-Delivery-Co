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
    public static partial class CrazyTaxiValidation
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
                TestAutomaticGears();
                TestFreeRev();
                TestPedalDirection();
                // Transient scene; saved scene setup is restored afterwards. No scene is saved here.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene.name = "VehicleLab";
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                Physics.simulationMode = SimulationMode.Script;
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Taxi test floor";
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(1000f, 1f, 1000f);
                GameObject car = new GameObject("Taxi test car", typeof(Rigidbody), typeof(BoxCollider));
                var productionBox = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/Car.prefab").GetComponent<BoxCollider>();
                car.GetComponent<BoxCollider>().size = productionBox.size;
                car.GetComponent<BoxCollider>().center = productionBox.center;
                _car = car.AddComponent<VehicleController>();
                var tuning = ScriptableObject.CreateInstance<VehicleTuning>();
                tuning.name = "Crazy Taxi test";
                tuning.taxi = new CrazyTaxiSettings();
                var serialized = new SerializedObject(_car);
                serialized.FindProperty("tuning").objectReferenceValue = tuning;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Call(_car, "Awake");
                Check(_car.GetComponent<VehicleLabInputMeter>() != null, "VehicleLab automatically exposes the controller meter");
                TestPhysics();
                TestRoadRecovery();
                TestSharperHandling();
                RecordFeelComparison();
                MeasureLevelDesign();
                TestCamera();
                TestInputAsset();
                TestPedalTravel();
                TestDualSenseTravel();
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
        private static void TestDualSenseTravel()
        {
            var pad = InputSystem.AddDevice<UnityEngine.InputSystem.DualShock.DualSenseGamepadHID>();
            var controls = VehicleInput.BuildFallbackControls();
            var map = controls.FindActionMap("Vehicle", true);
            try
            {
                map.Enable();
                foreach (float value in new[] { 0f, 0.25f, 0.5f, 1f })
                {
                    InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.DualShock.LowLevel.DualSenseHIDInputReport
                    {
                        leftStickX = 128, leftStickY = 128, rightStickX = 128, rightStickY = 128,
                        buttons0 = 8,
                        rightTrigger = (byte)Mathf.RoundToInt(value * 255f),
                        leftTrigger = (byte)Mathf.RoundToInt(value * 255f)
                    });
                    InputSystem.Update();
                    Check(Mathf.Abs(pad.rightTrigger.ReadUnprocessedValue() - value) < 0.01f &&
                        Mathf.Abs(pad.leftTrigger.ReadUnprocessedValue() - value) < 0.01f,
                        $"Virtual native DualSense reads R2/L2 travel {value:0.00} (actual {pad.rightTrigger.ReadUnprocessedValue():0.000})");
                }
            }
            finally
            {
                map.Disable();
                UnityEngine.Object.DestroyImmediate(controls);
                InputSystem.RemoveDevice(pad);
            }
        }
        private static void TestAutomaticGears()
        {
            var settings = new CrazyTaxiSettings();
            var box = new TaxiAutomaticGearbox();
            float first = box.Step(settings, 0f, 1f, false, true, Dt);
            float shifting = box.Step(settings, 13f, 1f, false, true, Dt);
            Check(box.Gear == 2 && box.ShiftRemaining > 0f && shifting < first * 0.65f,
                "Automatic upshift changes forward ratio and briefly reduces actual thrust");
            box.Step(settings, 26f, 1f, false, true, Dt);
            Check(box.Gear == 2, "Minimum gear hold prevents racing through several ratios on a speed spike");
            box.Step(settings, 11.4f, 1f, false, true, 0.5f);
            Check(box.Gear == 2, "Downshift hysteresis prevents hunting around the upshift speed");
            box.Step(settings, 6f, 1f, false, true, 0.5f);
            Check(box.Gear == 1, "Slowing below hysteresis band downshifts");
            float reverse = box.Step(settings, -10f, 1f, true, true, 0.3f);
            Check(box.Gear == 1 && reverse == 1f, "Reverse remains independent of forward ratios");
            box.Reset();
            box.Step(settings, 25f, 1f, false, false, 1f);
            Check(box.Gear == 1, "Airborne speed cannot trigger an automatic shift");
            settings.automaticGears = false;
            Check(box.Step(settings, 20f, 1f, false, true, Dt) == 1f, "Automatic gearbox can be disabled entirely");
            settings.automaticGears = true;
            settings.forwardGearSpeedFractions = new float[0];
            Check(box.Step(settings, 20f, 1f, false, true, Dt) == settings.forwardGearThrust[0],
                "Empty gear bands safely retain a single forward gear");
            box.Reset();
            box.Step(settings, 0f, 0f, false, true, 1f);
            float idle = box.Rpm;
            box.Step(settings, 0f, 1f, false, true, 1f);
            Check(box.Rpm > idle + 400f, "Engine load affects revs independently of road speed");
            settings = new CrazyTaxiSettings(); box.Reset();
            box.Step(settings, 7.7f, 1f, false, true, 1f);
            float beforeShift = box.Rpm;
            float unloaded = box.Step(settings, 8f, 1f, false, true, Dt);
            box.Step(settings, 8f, 1f, false, true, 0.16f);
            Check(box.Rpm < beforeShift - 1200f && box.ThrustEnvelope(settings) > settings.shiftThrustRetention,
                "Upshift produces a clear RPM fall while torque progressively re-engages");
            float engaged = box.Step(settings, 8f, 1f, false, true, 0.1f);
            Check(engaged > unloaded * 2f && box.Gear == 2,
                "Engagement restores real thrust while retaining the selected ratio");
            box.Step(settings, 27f, 1f, false, true, 0.4f);
            Check(box.Gear == 2, "Loaded gear hold starts after clutch engagement, not at shift initiation");
        }
        private static void TestFreeRev()
        {
            var s = new CrazyTaxiSettings(); var box = new TaxiAutomaticGearbox();
            float thrust = 1f;
            for (int i = 0; i < 120; i++) thrust = box.Step(s, 0f, 1f, false, true, Dt, true);
            Check(Mathf.Abs(box.Rpm - 9000f) < 0.01f && box.IsAtRevLimiter && thrust == 0f && box.ShiftCount == 0,
                "Brake-held full engine demand reaches 9000 RPM without thrust or automatic shifts");
            bool cut = false, restored = false, withinCeiling = true;
            for (int i = 0; i < 30; i++)
            {
                box.Step(s, 0f, 1f, false, true, Dt, true);
                cut |= box.RevLimiterGain < 0.5f; restored |= box.RevLimiterGain > 0.99f;
                withinCeiling &= box.Rpm <= 9000.01f;
            }
            Check(cut && restored, "Rev limiter alternates engine audio cut and recovery");
            Check(withinCeiling, "Free-rev limiter never exceeds its authored ceiling");
            box.Step(s, 0f, 0.5f, false, true, 2f, true);
            Check(Mathf.Abs(box.Rpm - 5000f) < 1f && !box.IsAtRevLimiter,
                "Half engine demand produces proportional free revs rather than a full-rev switch");
            box.Step(s, 0f, 0f, false, true, 2f);
            Check(box.Rpm == s.engineIdleRpm && !box.IsFreeRevving,
                "Releasing engine demand returns revs to idle and clears free-rev state");
        }
        private static void TestPedalDirection()
        {
            var s = new CrazyTaxiSettings();
            var pedals = new TaxiPedalDirection();
            DriveInput input = default;
            for (int i = 0; i < 100; i++) input = pedals.Resolve(s, 0f, 1f, 10f, 10f, true, Dt, out _);
            Check(input.brake == 1f && input.throttle == 0f && pedals.Direction == TaxiGear.Drive,
                "Held reverse pedal brakes forward travel without changing direction at speed");
            input = pedals.Resolve(s, 0f, 0.5f, 0f, 0f, true, 0.1f, out _);
            Check(input.brake == 0.5f && input.throttle == 0f, "Standstill dwell leaves time for a clean stop");
            input = pedals.Resolve(s, 0f, 0.5f, 0f, 0f, true, 0.1f, out bool changed);
            Check(changed && input.throttle == -0.5f && input.brake == 0f,
                "Holding half brake at rest engages proportional reverse");
            input = pedals.Resolve(s, 0.6f, 0f, -5f, 5f, true, 1f, out changed);
            Check(!changed && input.brake == 0.6f && input.throttle == 0f,
                "Forward pedal brakes reverse travel before engaging Drive");
            input = pedals.Resolve(s, 0.6f, 0f, 0f, 0f, true, 0.2f, out changed);
            Check(changed && input.throttle == 0.6f && pedals.Direction == TaxiGear.Drive,
                "Forward pedal returns to Drive at rest");
            input = pedals.Resolve(s, 1f, 1f, 0f, 0f, true, 1f, out changed);
            Check(!changed && input.throttle == 0f && input.brake == 1f,
                "Both pedals brake without selecting reverse or fighting the motor");
            pedals.Reset();
            input = pedals.Resolve(s, 0f, 1f, 0f, 4f, true, 1f, out changed);
            Check(!changed && input.throttle == 0f, "Sideways travel cannot be mistaken for a standstill");
            input = pedals.Resolve(s, 0f, 1f, 0f, 0f, false, 1f, out changed);
            Check(!changed && input.throttle == 0f, "Airborne car cannot engage automatic reverse");
            input = pedals.Resolve(s, 0.7f, 0f, 10f, 10f, true, Dt, out _);
            Check(input.throttle == 0.7f, "Gas always drives forward regardless of trick selector history");
            pedals.Reset(); s.brakeToReverse = false;
            input = pedals.Resolve(s, 0f, 1f, 0f, 0f, true, 1f, out changed);
            Check(!changed && input.brake == 1f && input.throttle == 0f, "Direction assistance can be disabled for original pedal behaviour");
            var tricks = new TaxiTrickRecognizer(0.3, 0.008);
            Check(tricks.Push(TaxiCommand.AutoReverse, 1, true, false, false, true) == TaxiTrick.None &&
                tricks.Push(TaxiCommand.AutoDrive, 1.1, true, false, true, true) == TaxiTrick.None &&
                tricks.Push(TaxiCommand.GasDown, 1.15, true, false, true, true) == TaxiTrick.None,
                "Automatic direction changes cannot synthesize Dash, Drift or Limiter Cut gestures");
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
            Check(Push(TaxiCommand.GasDown, 2) == TaxiTrick.Dash, "Simultaneous Drive and gas recognise dash");
            r.Reset();
            Push(TaxiCommand.GasDown, 2.1);
            Check(Push(TaxiCommand.Drive, 2.1) == TaxiTrick.Dash, "Simultaneous gas and Drive work in either event order");
            r.Reset();
            Push(TaxiCommand.Drive, 3);
            Check(Push(TaxiCommand.GasDown, 3.5) == TaxiTrick.None, "Expired gear input does not dash");
            r.Reset();
            Push(TaxiCommand.GasDown, 4);
            Check(Push(TaxiCommand.Drive, 4.5) == TaxiTrick.None, "Drive with gas held beyond the input window cannot dash");
            Check(Push(TaxiCommand.GasDown, 4.56) == TaxiTrick.None, "Held gas cannot replay a dash");
            r.Reset();
            Push(TaxiCommand.Reverse, 5);
            Check(Push(TaxiCommand.Drive, 5.04, steer: true) == TaxiTrick.Drift, "Ordered reverse-drive with steering recognises drift");
            Check(Push(TaxiCommand.Reverse, 5.1, steer: true) == TaxiTrick.BackDrift, "Drive-reverse with steering recognises back drift");
            r.Reset();
            Push(TaxiCommand.Reverse, 6);
            Check(Push(TaxiCommand.BrakeDown, 6.04) == TaxiTrick.Stop, "Reverse plus brake recognises stop");
            r.Reset();
            Push(TaxiCommand.Reverse, 6.1);
            Check(Push(TaxiCommand.BrakeDown, 6.1) == TaxiTrick.Stop, "Simultaneous Reverse and brake recognise stop");
            r.Reset();
            Push(TaxiCommand.BrakeDown, 6.2);
            Check(Push(TaxiCommand.Reverse, 6.2) == TaxiTrick.Stop, "Simultaneous brake and Reverse work in either event order");
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
            _car.Drive = new DriveInput { engineThrottle = 1f, freeRev = true, brake = 1f };
            _car.QueueTaxiCommand(TaxiCommand.Drive, 0);
            _car.QueueTaxiCommand(TaxiCommand.GasDown, 0.06);
            Tick(1.2f);
            Check(_car.Speed < 0.2f && _car.EngineRpm > 8950f && _car.AppliedDriveAcceleration == 0f && _car.TrickCount == 0,
                "Stationary brake-and-gas revs to 9000 without moving the Rigidbody or triggering tricks");
            ResetCar();
            _car.Drive = new DriveInput { throttle = 1f };
            Tick(0.5f);
            Check(_car.Speed > 6f, "Five-speed normal launch retains over 21 km/h within half a second");
            Tick(7.5f);
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
            foreach (float kmh in new[] { 50f, 96f })
            {
                foreach (float steer in new[] { -1f, 1f })
                {
                    ResetCar();
                    _car.Body.linearVelocity = Vector3.forward * (kmh / 3.6f);
                    _car.Drive = new DriveInput { steer = steer };
                    _car.QueueTaxiCommand(TaxiCommand.QuickSpin, 71);
                    float peakRoll = 0f;
                    int leastSupport = 4;
                    float initialYaw = 0f;
                    for (int frame = 0; frame < 106; frame++)
                    {
                        Tick(Dt);
                        peakRoll = Mathf.Max(peakRoll, Vector3.Angle(_car.transform.up, Vector3.up));
                        leastSupport = Mathf.Min(leastSupport, _car.WheelsOnGround);
                        if (frame == 24) initialYaw = Vector3.SignedAngle(Vector3.forward, _car.transform.forward, Vector3.up);
                    }
                    Check(_car.Speed > kmh / 3.6f * 0.8f,
                        $"Quick turn from {kmh} km/h retains at least 80% speed (steer {steer}, actual {_car.Speed * 3.6f:0.0} km/h)");
                    Check(peakRoll < 10f && leastSupport >= 3 && initialYaw * steer > 15f,
                        $"Quick turn follows steering and stays flat throughout at {kmh} km/h (steer {steer}; peak tilt {peakRoll:0.0} degrees; minimum {leastSupport} wheels)");
                }
            }
            ResetCar();
            _car.Body.linearVelocity = Vector3.forward * 20f;
            _car.Drive = default;
            int beforeSpin = _car.TrickCount;
            _car.QueueTaxiCommand(TaxiCommand.QuickSpin, 72);
            Tick(0.2f);
            Check(_car.TrickCount == beforeSpin && _car.ActiveTrick == TaxiTrick.None,
                "Quick turn with neutral steering does nothing");
            ResetCar();
            _car.Teleport(new Vector3(0, 20, 0), Quaternion.identity);
            _car.Drive = new DriveInput { throttle = 1f, steer = 1f, boost = true };
            Tick(0.2f);
            Check(Mathf.Abs(_car.Body.linearVelocity.x) < 0.01f && Mathf.Abs(_car.Body.linearVelocity.z) < 0.01f &&
                _car.Body.angularVelocity.magnitude < 0.01f && !_car.IsBoosting,
                "Airborne car is ballistic: no rocket thrust or air steering");
            ResetCar();
        }

        private static void TestSharperHandling()
        {
            CrazyTaxiSettings original = _car.Tuning.taxi;
            var rows = new List<string> { "configuration,speed_mps,heading_degrees,travel_radius_m,exit_speed_mps" };
            try
            {
                foreach (float speed in new[] { 12f, 20f, 28f })
                {
                    float previousRadius = 0f;
                    foreach (bool before in new[] { true, false })
                    {
                        var settings = new CrazyTaxiSettings();
                        if (before)
                        {
                            settings.cornerGrip = 38f;
                            settings.steeringResponse = 14f;
                            settings.yawAcceleration = 12f;
                            settings.lowSpeedTurnDegrees = 125f;
                            settings.highSpeedTurnDegrees = 58f;
                        }
                        _car.Tuning.taxi = settings;
                        ResetCar();
                        Vector3 start = _car.transform.position;
                        _car.Body.linearVelocity = Vector3.forward * speed;
                        _car.Drive = new DriveInput { steer = 1f };
                        Tick(0.8f);
                        float heading = Vector3.Angle(Vector3.forward, _car.transform.forward);
                        Vector3 travel = Vector3.ProjectOnPlane(_car.transform.position - start, Vector3.up);
                        // A circle through the start (forward tangent) and end measures the travel path,
                        // so rotating the chassis without redirecting momentum cannot pass this check.
                        float radius = travel.sqrMagnitude / Mathf.Max(0.01f, 2f * Mathf.Abs(travel.x));
                        rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "{0},{1:F1},{2:F2},{3:F2},{4:F2}", before ? "previous" : "sharp_turns",
                            speed, heading, radius, _car.Speed));
                        if (before) previousRadius = radius;
                        else
                        {
                            Check(radius < previousRadius * 0.85f, $"Travel radius at {speed} m/s is at least 15% tighter ({radius:0.00} m vs {previousRadius:0.00} m)");
                            Check(_car.Speed > speed * 0.70f && _car.IsUpright,
                                $"Sharper corner at {speed} m/s retains momentum and stays upright");
                        }
                    }
                }
                foreach (string profile in new[] { "Balanced", "Grippy", "Loose" })
                {
                    var asset = AssetDatabase.LoadAssetAtPath<VehicleTuning>($"Assets/Project/Scripts/Vehicle/Tuning/Tuning_{profile}.asset");
                    Check(asset != null && asset.taxi.highSpeedTurnDegrees == original.highSpeedTurnDegrees &&
                        asset.taxi.cornerGrip == original.cornerGrip && asset.taxi.forwardGearThrust[0] == original.forwardGearThrust[0],
                        $"{profile} serialized profile uses the new steering, grip and launch settings");
                }
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../VehicleLab_Measurements"));
                Directory.CreateDirectory(directory);
                File.WriteAllLines(Path.Combine(directory, "crazy_taxi_handling_comparison.csv"), rows);
            }
            finally { _car.Tuning.taxi = original; ResetCar(); }
        }

        private static void RecordFeelComparison()
        {
            CrazyTaxiSettings original = _car.Tuning.taxi;
            var rows = new List<string> { "configuration,cruise_kmh,time_to_40_s,time_to_60_s,time_to_80_s,time_to_100_s,shift_1_s,shift_2_s,shift_3_s,shift_4_s,max_kmh" };
            try
            {
                foreach (bool before in new[] { true, false })
                {
                    var s = new CrazyTaxiSettings();
                    if (before)
                    {
                        s.forwardGearThrust = new[] { 0.58f, 0.40f, 0.36f, 0.32f, 0.30f };
                        s.gearShiftTime = 0.22f; s.shiftThrustRetention = 0.12f;
                        s.gearMinimumHoldTime = 0.65f;
                    }
                    _car.Tuning.taxi = s;
                    ResetCar();
                    _car.Drive = new DriveInput { throttle = 1f };
                    float[] targets = { 40f, 60f, 80f, 100f };
                    float[] reached = { -1f, -1f, -1f, -1f };
                    float[] shifts = { -1f, -1f, -1f, -1f };
                    int previousGear = 1, shift = 0;
                    float maximum = 0f;
                    for (int frame = 1; frame <= 8 * 120; frame++)
                    {
                        Tick(Dt);
                        float kmh = _car.Speed * 3.6f;
                        maximum = Mathf.Max(maximum, kmh);
                        for (int i = 0; i < targets.Length; i++)
                            if (reached[i] < 0f && kmh >= targets[i]) reached[i] = frame * Dt;
                        if (_car.ForwardGear != previousGear && shift < shifts.Length) shifts[shift++] = frame * Dt;
                        previousGear = _car.ForwardGear;
                    }
                    Check(maximum <= s.cruiseSpeed * 3.6f + 0.5f && reached[3] > 0f,
                        (before ? "Before" : "After") + " controlled run reaches 100 km/h without raising the cruise cap");
                    rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0},{1:F2},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3},{8:F3},{9:F3},{10:F3}",
                        before ? "previous_five_speed" : "stronger_five_speed", s.cruiseSpeed * 3.6f,
                        reached[0], reached[1], reached[2], reached[3], shifts[0], shifts[1], shifts[2], shifts[3], maximum));
                    if (!before)
                    {
                        Check(_car.ForwardGear == 5 && shift == 4, "Normal acceleration reaches all five gears with exactly four upshifts");
                        Check(shifts[3] > 3f && shifts[3] - shifts[2] >= 0.85f,
                            "Fifth arrives after sustained gear pulls rather than a rapid counter sequence");
                    }
                }
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../VehicleLab_Measurements"));
                Directory.CreateDirectory(directory);
                File.WriteAllLines(Path.Combine(directory, "crazy_taxi_speed_feel_comparison.csv"), rows);
            }
            finally { _car.Tuning.taxi = original; ResetCar(); }
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
            var sideWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sideWall.transform.position = new Vector3(-3f, 3f, 0f);
            sideWall.transform.localScale = new Vector3(0.5f, 10f, 20f);
            Physics.SyncTransforms();
            bool clearCorner = true;
            for (int frame = 0; frame < 180; frame++)
            {
                _car.transform.rotation = Quaternion.Euler(0f, frame * 0.5f, 0f);
                camera.RenderCamera(1f / 60f);
                foreach (GameObject obstacle in new[] { wall, sideWall })
                    clearCorner &= Vector3.Distance(camera.transform.position,
                        obstacle.GetComponent<Collider>().ClosestPoint(camera.transform.position)) >= _car.Taxi.cameraCollisionRadius - 0.02f;
            }
            Check(clearCorner, "Camera stays outside both walls throughout an inside-corner turn");
            UnityEngine.Object.DestroyImmediate(sideWall);
            _car.transform.rotation = Quaternion.identity;
            wall.transform.position = new Vector3(0f, 3f, -0.1f);
            Physics.SyncTransforms();
            camera.SnapToTarget();
            for (int frame = 0; frame < 60; frame++) camera.RenderCamera(1f / 60f);
            Check(camera.transform.position.z > 0.45f,
                $"Camera stays on the visible side when its aim pivot overlaps a close building (camera {camera.transform.position}; car {_car.transform.position})");
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
            _car.Body.linearVelocity = Vector3.zero;
            camera.SnapToTarget();
            float restFov = camera.Camera.fieldOfView;
            _car.Body.linearVelocity = _car.transform.forward * _car.Taxi.cruiseSpeed;
            for (int frame = 0; frame < 120; frame++) camera.RenderCamera(Dt);
            Check(camera.Camera.fieldOfView > restFov + 7f && camera.Camera.fieldOfView <= restFov + 9.1f,
                "Speed camera smoothly widens FOV within the authored limit at unchanged cruise speed");
            _car.Taxi.speedCameraCues = false;
            camera.SnapToTarget();
            Check(Mathf.Abs(camera.Camera.fieldOfView - _car.Taxi.cameraFov) < 0.01f,
                "Speed camera cues can be disabled for a stable authored view");
            _car.Taxi.speedCameraCues = true;
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
