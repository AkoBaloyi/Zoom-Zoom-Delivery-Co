using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ZoomZoom.Vehicle.Editor
{
    /// <summary>Batch-only smoke test of the actual game scene in an isolated project copy.</summary>
    [InitializeOnLoad]
    public static class CrazyTaxiPlaySmoke
    {
        private const string ActiveKey = "CrazyTaxiSmoke.Active";
        private static double _started;
        private static Keyboard _keyboard;
        private static Gamepad _gamepad;
        private static VehicleController _car;
        private static Vector3 _startPosition;
        private static float _maximumDistance;
        private static int _stage, _frames;
        private static int _analogStage;
        private static int _pedalStage, _tricksBeforeAuto;
        private static bool _reversePedalWorked, _forwardPedalWorked, _bothPedalsStopped;
        private static bool _brakeRevWorked;
        private static bool _selectorKeepsForward;
        private static bool _fiveSpeedStarted;
        private static int _startingShiftCues, _startingPulses;
        private static readonly float[] GasSamples = new float[3];
        private static readonly float[] BrakeSamples = new float[3];
        private static readonly List<string> Moves = new List<string>();
        private static readonly double[] AnalogTimes = { 0.2, 0.35, 0.4, 0.55, 0.6, 0.75, 0.8, 10.2, 10.35, 10.4, 10.55, 10.6, 10.75, 10.8 };
        private static readonly List<string> Errors = new List<string>();
        private static readonly double[] Times = { 1, 2, 2.1, 2.2, 3, 3.1, 4, 4.1, 4.2, 5, 6, 7, 7.1, 7.2, 8, 8.3, 8.6, 9, 10 };
        private static readonly Key[][] Keys = {
            new[] { Key.W }, new[] { Key.W, Key.LeftShift }, new[] { Key.W }, new[] { Key.W },
            new[] { Key.W, Key.D, Key.LeftCtrl }, new[] { Key.W, Key.D },
            new[] { Key.W, Key.LeftShift }, new[] { Key.W }, new[] { Key.W }, new[] { Key.S, Key.LeftShift },
            new[] { Key.W, Key.R }, new Key[0], new[] { Key.W }, new[] { Key.W, Key.LeftShift },
            new[] { Key.W, Key.C }, new[] { Key.W, Key.A, Key.Space }, new[] { Key.W }, new[] { Key.W }, new Key[0]
        };
        static CrazyTaxiPlaySmoke()
        {
            EditorApplication.playModeStateChanged += StateChanged;
            if (SessionState.GetBool(ActiveKey, false)) Application.logMessageReceived += Log;
            if (SessionState.GetBool(ActiveKey, false) && EditorApplication.isPlaying) Begin();
        }
        public static void Run()
        {
            SessionState.SetBool("CrazyTaxiSmoke.Lab", false);
            StartScene("Assets/Project/Scenes/Possible game scene.unity");
        }
        public static void RunLab()
        {
            SessionState.SetBool("CrazyTaxiSmoke.Lab", true);
            StartScene("Assets/Project/Scenes/VehicleLab.unity");
        }
        // Unsaved, isolated lab: validates live input/audio without opening a team scene.
        public static void RunTransient()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch-only transient test.");
            SessionState.SetBool("CrazyTaxiSmoke.Lab", true);
            SessionState.SetBool("CrazyTaxiSmoke.Transient", true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "VehicleLab";
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.transform.localScale = new Vector3(2000, 1, 2000);
            var car = new GameObject("Direct controls test car", typeof(Rigidbody), typeof(BoxCollider));
            car.transform.position = new Vector3(0, 0.55f, 0);
            var originalBox = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/Car.prefab").GetComponent<BoxCollider>();
            car.GetComponent<BoxCollider>().size = originalBox.size;
            car.GetComponent<BoxCollider>().center = originalBox.center;
            var controller = car.AddComponent<VehicleController>();
            var settings = new SerializedObject(controller);
            settings.FindProperty("tuning").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VehicleTuning>(
                "Assets/Project/Scripts/Vehicle/Tuning/Tuning_Balanced.asset");
            settings.ApplyModifiedPropertiesWithoutUndo();
            car.AddComponent<VehicleInput>();
            car.AddComponent<VehicleLabInputMeter>();
            var view = new GameObject("Test camera", typeof(Camera), typeof(AudioListener)).AddComponent<ChaseCamera>();
            var cameraSettings = new SerializedObject(view);
            cameraSettings.FindProperty("target").objectReferenceValue = controller;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString("CrazyTaxiSmoke.Errors", "");
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
            EditorApplication.isPlaying = true;
        }
        private static void StartScene(string scene)
        {
            SessionState.SetBool("CrazyTaxiSmoke.Transient", false);
            if (!Application.isBatchMode) throw new InvalidOperationException("Run this smoke test in an isolated batch-mode project copy.");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString("CrazyTaxiSmoke.Errors", "");
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) Begin();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../VehicleLab_Measurements"));
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "crazy_taxi_playmode.txt"), SessionState.GetString("CrazyTaxiSmoke.Report", "No report"));
                bool passed = SessionState.GetBool("CrazyTaxiSmoke.Passed", false);
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }
        private static void Begin()
        {
            // A headless editor has no focused Game view. Simulate that focus only in this test.
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
            _started = EditorApplication.timeSinceStartup;
            _stage = _frames = 0;
            _analogStage = 0;
            _pedalStage = 0;
            _reversePedalWorked = _forwardPedalWorked = _bothPedalsStopped = false;
            _brakeRevWorked = false;
            _selectorKeepsForward = false;
            _fiveSpeedStarted = false;
            Moves.Clear();
            _maximumDistance = 0f;
            Errors.Clear();
            string startupErrors = SessionState.GetString("CrazyTaxiSmoke.Errors", "");
            if (!string.IsNullOrEmpty(startupErrors)) Errors.Add(startupErrors);
        }
        private static void Log(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                Errors.Add(condition);
                SessionState.SetString("CrazyTaxiSmoke.Errors", SessionState.GetString("CrazyTaxiSmoke.Errors", "") + condition + "\n");
            }
        }
        private static void Update()
        {
            if (!EditorApplication.isPlaying) return;
            double elapsed = EditorApplication.timeSinceStartup - _started;
            if (_car == null)
            {
                _car = UnityEngine.Object.FindAnyObjectByType<VehicleController>();
                if (_car != null)
                {
                    _startPosition = _car.transform.position;
                    _keyboard = InputSystem.AddDevice<Keyboard>("TaxiSmokeKeyboard");
                    _gamepad = InputSystem.AddDevice<UnityEngine.InputSystem.DualShock.DualSenseGamepadHID>("TaxiSmokeDualSense");
                    _car.TrickPerformed += RecordMove;
                    var meter = _car.GetComponent<VehicleLabInputMeter>();
                    if (SessionState.GetBool("CrazyTaxiSmoke.Lab", false) && meter != null)
                        typeof(VehicleLabInputMeter).GetMethod("StartRecording", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(meter, null);
                }
            }
            _frames++;
            if (_car != null)
            {
                _car.gameObject.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
                TestAnalogTravel(elapsed);
                TestAutomaticPedals(elapsed);
                if (elapsed > 3.02 && elapsed < 3.1)
                    _selectorKeepsForward |= _car.Drive.throttle > 0f && _car.ForwardSpeed > 0f;
                if (SessionState.GetBool("CrazyTaxiSmoke.Lab", false) && elapsed >= 18 && !_fiveSpeedStarted)
                {
                    _car.Teleport(_startPosition, Quaternion.identity);
                    _startingShiftCues = _car.GetComponent<TaxiEngineAudio>().ShiftCueCount;
                    _startingPulses = _car.GetComponent<TaxiGearHaptics>().PulseCount;
                    QueuePedals(1f, 0f);
                    _fiveSpeedStarted = true;
                }
                _maximumDistance = Mathf.Max(_maximumDistance, Vector3.Distance(_startPosition, _car.transform.position));
                // Use real Input System events, not writes to the controller's command struct.
                if (_stage < Times.Length && elapsed >= Times[_stage])
                {
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Keys[_stage]), InputState.currentTime);
                    _stage++;
                }
            }
            if (elapsed < (SessionState.GetBool("CrazyTaxiSmoke.Lab", false) ? 26 : 18)) return;
            bool inputReady = _car != null && _car.GetComponent<VehicleInput>() != null && _car.GetComponent<VehicleInput>().IsReady;
            bool feedbackReady = _car != null && _car.GetComponent<CrazyTaxiFeedback>() != null;
            bool cameraReady = UnityEngine.Object.FindAnyObjectByType<ChaseCamera>() != null;
            bool retired = _car != null && (_car.GetComponent<VehicleJumpFlip>() == null || !_car.GetComponent<VehicleJumpFlip>().enabled) && !_car.IsBoosting;
            int tricks = _car != null ? _car.TrickCount : 0;
            bool analog = GasSamples[0] > 0.1f && GasSamples[0] < GasSamples[1] && GasSamples[1] < GasSamples[2] && GasSamples[2] > 0.95f &&
                BrakeSamples[0] > 0.1f && BrakeSamples[0] < BrakeSamples[1] && BrakeSamples[1] < BrakeSamples[2] && BrakeSamples[2] > 0.95f;
            bool lab = SessionState.GetBool("CrazyTaxiSmoke.Lab", false);
            bool meterReady = !lab || (_car != null && _car.GetComponent<VehicleLabInputMeter>() != null);
            bool automaticPedals = _reversePedalWorked && _forwardPedalWorked && _bothPedalsStopped && _brakeRevWorked && tricks == _tricksBeforeAuto;
            int shiftCues = _car != null ? _car.GetComponent<TaxiEngineAudio>().ShiftCueCount - _startingShiftCues : 0;
            int pulses = _car != null ? _car.GetComponent<TaxiGearHaptics>().PulseCount - _startingPulses : 0;
            bool fiveSpeedFeedback = !lab || (_car.ForwardGear == 5 && shiftCues >= 4 && pulses >= 4);
            bool allCoreMoves = Moves.Contains("Dash") && Moves.Contains("Drift") && Moves.Contains("Stop") && Moves.Contains("QuickSpin");
            int dashCues = _car != null ? _car.GetComponent<TaxiEngineAudio>().DashCueCount : 0;
            bool passed = Errors.Count == 0 && inputReady && feedbackReady && cameraReady && retired && analog && allCoreMoves && _selectorKeepsForward && _maximumDistance > 2f && meterReady && automaticPedals && fiveSpeedFeedback && dashCues > 0;
            string testScene = SessionState.GetBool("CrazyTaxiSmoke.Transient", false) ? "Transient vehicle lab" : $"Actual {(lab ? "VehicleLab" : "game")} scene";
            string report = $"{testScene} Play Mode: {(passed ? "PASS" : "FAIL")}\n" +
                $"Lab meter ready: {meterReady}\n" +
                $"Frames: {_frames}\nInput ready: {inputReady}\nDriving feedback: {feedbackReady}\nCamera: {cameraReady}\n" +
                $"Retired mechanics inactive: {retired}\nRecognised moves: {tricks}\nMaximum displacement: {_maximumDistance:0.00} m\n" +
                $"Moves: {string.Join(", ", Moves)}\nAnalog trigger travel: {analog}\n" +
                $"Dash / Drift / Stop / steered Quick Turn via real input events: {allCoreMoves}\nR trick selector keeps W driving forward: {_selectorKeepsForward}\n" +
                $"Automatic L2 reverse: {_reversePedalWorked}\nR2 returns to forward: {_forwardPedalWorked}\nBoth pedals brake: {_bothPedalsStopped}\nAutomatic direction changes create no tricks: {tricks == _tricksBeforeAuto}\n" +
                $"Dash sound cues dispatched: {dashCues}\nFive-speed feedback: {fiveSpeedFeedback}\nEngagement sound cues dispatched: {shiftCues}\nVirtual pad engagement pulses dispatched: {pulses}\n" +
                $"Brake + accelerator reaches 9000 RPM without wheel thrust: {_brakeRevWorked}\n" +
                $"Accelerator outputs at quarter/half/full travel: {GasSamples[0]:0.000}, {GasSamples[1]:0.000}, {GasSamples[2]:0.000}\n" +
                $"Brake outputs at quarter/half/full travel: {BrakeSamples[0]:0.000}, {BrakeSamples[1]:0.000}, {BrakeSamples[2]:0.000}\n" +
                $"Runtime errors: {Errors.Count}\n" + string.Join("\n", Errors);
            SessionState.SetString("CrazyTaxiSmoke.Report", report);
            SessionState.SetBool("CrazyTaxiSmoke.Passed", passed);
            Debug.Log(report);
            Application.logMessageReceived -= Log;
            EditorApplication.update -= Update;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            if (_gamepad != null) InputSystem.RemoveDevice(_gamepad);
            if (_car != null) _car.TrickPerformed -= RecordMove;
            EditorApplication.isPlaying = false;
        }
        private static void RecordMove(TaxiTrick move) => Moves.Add(move.ToString());
        private static void TestAutomaticPedals(double elapsed)
        {
            if (elapsed < 12) return;
            if (_pedalStage == 0)
            {
                _car.Teleport(_startPosition, Quaternion.identity);
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                QueuePedals(0f, 0.5f);
                _tricksBeforeAuto = _car.TrickCount;
                _pedalStage = 1;
            }
            if (_pedalStage == 1 && elapsed > 13)
                _reversePedalWorked |= _car.Drive.throttle < -0.4f && _car.ForwardSpeed < -0.5f;
            if (_pedalStage == 1 && elapsed > 14) { QueuePedals(0.5f, 0f); _pedalStage = 2; }
            if (_pedalStage == 2 && elapsed > 15)
                _forwardPedalWorked |= _car.Drive.throttle > 0.4f && _car.ForwardSpeed > 0.5f;
            if (_pedalStage == 2 && elapsed > 16) { QueuePedals(1f, 1f); _pedalStage = 3; }
            if (_pedalStage == 3 && elapsed > 17)
            {
                _bothPedalsStopped |= _car.Drive.throttle == 0f && _car.Drive.brake > 0.4f && _car.Speed < 0.5f;
                _brakeRevWorked |= _car.IsAtRevLimiter && _car.EngineRpm >= 8950f && _car.EngineRpm <= 9000.01f &&
                    _car.AppliedDriveAcceleration == 0f && _car.Speed < 0.5f;
            }
        }
        private static void QueuePedals(float gas, float brake)
        {
            InputSystem.QueueStateEvent(_gamepad, new UnityEngine.InputSystem.DualShock.LowLevel.DualSenseHIDInputReport
            {
                leftStickX = 128, leftStickY = 128, rightStickX = 128, rightStickY = 128, buttons0 = 8,
                rightTrigger = (byte)Mathf.RoundToInt(gas * 255f), leftTrigger = (byte)Mathf.RoundToInt(brake * 255f)
            }, InputState.currentTime);
        }
        private static void TestAnalogTravel(double elapsed)
        {
            if (_gamepad == null || _analogStage >= AnalogTimes.Length || elapsed < AnalogTimes[_analogStage]) return;
            VehicleInput input = _car.GetComponent<VehicleInput>();
            int stage = _analogStage;
            if (stage == 1 || stage == 3 || stage == 5)
            {
                float expected = stage == 1 ? 64f / 255f : stage == 3 ? 128f / 255f : 1f;
                expected = _car.Taxi.MapPedal(expected, _car.Taxi.acceleratorDeadzone, _car.Taxi.acceleratorTravel);
                if (Mathf.Abs(input.AcceleratorAmount - expected) > 0.01f) return;
                GasSamples[(stage - 1) / 2] = input.AcceleratorAmount;
            }
            else if (stage == 8 || stage == 10 || stage == 12)
            {
                float expected = stage == 8 ? 64f / 255f : stage == 10 ? 128f / 255f : 1f;
                expected = _car.Taxi.MapPedal(expected, _car.Taxi.brakeDeadzone, _car.Taxi.brakeTravel);
                if (Mathf.Abs(input.BrakeAmount - expected) > 0.01f) return;
                BrakeSamples[(stage - 8) / 2] = input.BrakeAmount;
            }
            else
            {
                float value = stage == 0 || stage == 7 ? 0.25f : stage == 2 || stage == 9 ? 0.5f : stage == 4 || stage == 11 ? 1f : 0f;
                InputSystem.QueueStateEvent(_gamepad, new UnityEngine.InputSystem.DualShock.LowLevel.DualSenseHIDInputReport
                {
                    leftStickX = 128, leftStickY = 128, rightStickX = 128, rightStickY = 128,
                    buttons0 = 8,
                    rightTrigger = (byte)Mathf.RoundToInt((stage < 7 ? value : 0f) * 255f),
                    leftTrigger = (byte)Mathf.RoundToInt((stage >= 7 ? value : 0f) * 255f)
                }, InputState.currentTime);
            }
            _analogStage++;
        }
    }
}
