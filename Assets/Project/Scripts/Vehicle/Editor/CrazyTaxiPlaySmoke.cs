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
        private static readonly float[] GasSamples = new float[3];
        private static readonly float[] BrakeSamples = new float[3];
        private static readonly List<string> Moves = new List<string>();
        private static readonly double[] AnalogTimes = { 0.2, 0.35, 0.4, 0.55, 0.6, 0.75, 0.8, 10.2, 10.35, 10.4, 10.55, 10.6, 10.75, 10.8 };
        private static readonly List<string> Errors = new List<string>();
        private static readonly double[] Times = { 1, 2, 2.1, 2.2, 3, 3.1, 4, 4.1, 4.2, 5, 6, 7, 7.1, 7.2, 8, 8.3, 8.6, 9, 10 };
        private static readonly Key[][] Keys = {
            new[] { Key.W }, new Key[0], new[] { Key.F }, new[] { Key.W },
            new[] { Key.W, Key.D, Key.R }, new[] { Key.W, Key.D, Key.F },
            new Key[0], new[] { Key.F }, new[] { Key.W }, new[] { Key.S, Key.R },
            new[] { Key.W, Key.R }, new Key[0], new[] { Key.F }, new[] { Key.W },
            new[] { Key.W, Key.C }, new[] { Key.W, Key.Space }, new[] { Key.W }, new[] { Key.W }, new Key[0]
        };
        static CrazyTaxiPlaySmoke()
        {
            EditorApplication.playModeStateChanged += StateChanged;
            if (SessionState.GetBool(ActiveKey, false)) Application.logMessageReceived += Log;
            if (SessionState.GetBool(ActiveKey, false) && EditorApplication.isPlaying) Begin();
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run this smoke test in an isolated batch-mode project copy.");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString("CrazyTaxiSmoke.Errors", "");
            Application.logMessageReceived -= Log;
            Application.logMessageReceived += Log;
            EditorSceneManager.OpenScene("Assets/Project/Scenes/Possible game scene.unity", OpenSceneMode.Single);
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
                    _gamepad = InputSystem.AddDevice<Gamepad>("TaxiSmokeGamepad");
                    _car.TrickPerformed += RecordMove;
                }
            }
            _frames++;
            if (_car != null)
            {
                _car.gameObject.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
                TestAnalogTravel(elapsed);
                _maximumDistance = Mathf.Max(_maximumDistance, Vector3.Distance(_startPosition, _car.transform.position));
                // Use real Input System events, not writes to the controller's command struct.
                if (_stage < Times.Length && elapsed >= Times[_stage])
                {
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Keys[_stage]), InputState.currentTime);
                    _stage++;
                }
            }
            if (elapsed < 12) return;
            bool inputReady = _car != null && _car.GetComponent<VehicleInput>() != null && _car.GetComponent<VehicleInput>().IsReady;
            bool feedbackReady = _car != null && _car.GetComponent<CrazyTaxiFeedback>() != null;
            bool cameraReady = UnityEngine.Object.FindAnyObjectByType<ChaseCamera>() != null;
            bool retired = _car != null && (_car.GetComponent<VehicleJumpFlip>() == null || !_car.GetComponent<VehicleJumpFlip>().enabled) && !_car.IsBoosting;
            int tricks = _car != null ? _car.TrickCount : 0;
            bool analog = GasSamples[0] > 0.1f && GasSamples[0] < GasSamples[1] && GasSamples[1] < GasSamples[2] && GasSamples[2] > 0.95f &&
                BrakeSamples[0] > 0.1f && BrakeSamples[0] < BrakeSamples[1] && BrakeSamples[1] < BrakeSamples[2] && BrakeSamples[2] > 0.95f;
            bool passed = Errors.Count == 0 && inputReady && feedbackReady && cameraReady && retired && analog && tricks >= 2 && _maximumDistance > 2f;
            string report = $"Actual game scene Play Mode: {(passed ? "PASS" : "FAIL")}\n" +
                $"Frames: {_frames}\nInput ready: {inputReady}\nDriving feedback: {feedbackReady}\nCamera: {cameraReady}\n" +
                $"Retired mechanics inactive: {retired}\nRecognised moves: {tricks}\nMaximum displacement: {_maximumDistance:0.00} m\n" +
                $"Moves: {string.Join(", ", Moves)}\nAnalog trigger travel: {analog}\n" +
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
        private static void TestAnalogTravel(double elapsed)
        {
            if (_gamepad == null || _analogStage >= AnalogTimes.Length || elapsed < AnalogTimes[_analogStage]) return;
            VehicleInput input = _car.GetComponent<VehicleInput>();
            int stage = _analogStage++;
            if (stage == 1 || stage == 3 || stage == 5)
                GasSamples[(stage - 1) / 2] = input.AcceleratorAmount;
            else if (stage == 8 || stage == 10 || stage == 12)
                BrakeSamples[(stage - 8) / 2] = input.BrakeAmount;
            else
            {
                float value = stage == 0 || stage == 7 ? 0.25f : stage == 2 || stage == 9 ? 0.5f : stage == 4 || stage == 11 ? 1f : 0f;
                InputSystem.QueueStateEvent(_gamepad, new GamepadState
                {
                    rightTrigger = stage < 7 ? value : 0f,
                    leftTrigger = stage >= 7 ? value : 0f
                }, InputState.currentTime);
            }
        }
    }
}
