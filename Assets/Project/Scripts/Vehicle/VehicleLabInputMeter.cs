using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomZoom.Vehicle
{
    /// <summary>Lab-only raw hardware, action and motor diagnostics, independent of automatic tests.</summary>
    [DisallowMultipleComponent]
    public sealed class VehicleLabInputMeter : MonoBehaviour
    {
        private VehicleController _car;
        private VehicleInput _input;
        private int _deviceId = -1;
        private float _gasPeak, _brakePeak, _nextSample;
        private StreamWriter _recording;
        private string _message = "Click the Game view before driving. Raw readings require editor input focus too.";
        private bool _focus = true;
        private ChaseCamera _camera;
        private float _lastSpeed, _lastTime, _measuredAcceleration;
        private Gamepad Pad
        {
            get
            {
                foreach (Gamepad pad in Gamepad.all) if (pad.deviceId == _deviceId) return pad;
                return Gamepad.current;
            }
        }
        private void Awake() { _car = GetComponent<VehicleController>(); _input = GetComponent<VehicleInput>(); }
        private void OnApplicationFocus(bool focus) { _focus = focus; }
        private void Update()
        {
            Gamepad pad = Pad;
            if (pad != null && pad.deviceId != _deviceId) { _deviceId = pad.deviceId; ResetPeaks(); }
            float gas = pad != null ? pad.rightTrigger.ReadUnprocessedValue() : 0f;
            float brake = pad != null ? pad.leftTrigger.ReadUnprocessedValue() : 0f;
            _gasPeak = Mathf.Max(_gasPeak, gas); _brakePeak = Mathf.Max(_brakePeak, brake);
            if (_recording == null || Time.unscaledTime < _nextSample) return;
            _nextSample = Time.unscaledTime + 0.1f;
            float elapsed = Time.unscaledTime - _lastTime;
            _measuredAcceleration = _lastTime > 0f && elapsed > 0f ? (_car.ForwardSpeed - _lastSpeed) / elapsed : 0f;
            _lastSpeed = _car.ForwardSpeed; _lastTime = Time.unscaledTime;
            if (_camera == null) _camera = FindAnyObjectByType<ChaseCamera>();
            try
            {
                _recording.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F3},{1},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F3},{9},{10:F0},{11:F3},{12},{13},{14},{15:F3},{16:F3},{17},{18},{19},{20:F2},{21:F3},20261024d,{22:F3},{23:F3},{24},{25:F3},{26},{27}",
                    Time.unscaledTime, pad != null ? pad.deviceId : -1, gas, brake,
                    _input != null ? _input.RawAccelerator : 0f, _input != null ? _input.RawBrake : 0f,
                    _car.Drive.throttle, _car.Drive.brake, _car.Speed * 3.6f, _car.ForwardGear,
                    _car.EngineRpm, _car.AppliedDriveAcceleration, _input != null && _input.isActiveAndEnabled,
                    _focus, Time.timeScale, _car.ForwardSpeed * 3.6f, _measuredAcceleration,
                    _car.SelectedGear, _car.ActiveTrick, _car.IsGearShifting,
                    _camera != null && _camera.Camera != null ? _camera.Camera.fieldOfView : 0f, _car.Drive.steer,
                    _car.GearShiftProgress, _car.GearThrustEnvelope, _car.ForwardShiftCount,
                    _car.EngineDemand, _car.IsFreeRevving, _car.IsAtRevLimiter));
            }
            catch (IOException e) { StopRecording(); _message = e.Message; }
        }
        private void ResetPeaks() { _gasPeak = _brakePeak = 0f; }
        private void OnGUI()
        {
            if (_car == null) return;
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Max(0.3f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUILayout.BeginArea(new Rect(Screen.width / scale - 450f, 12f, 438f, 650f), GUI.skin.box);
            GUILayout.Label("VEHICLE LAB  |  CONTROLLER + ENGINE");
            Gamepad pad = Pad;
            GUILayout.Label(pad == null ? "NO GAMEPAD DETECTED" : $"{pad.displayName}  #{pad.deviceId}\n{pad.layout} / {pad.description.interfaceName}");
            if (Gamepad.all.Count > 1 && GUILayout.Button("Next controller"))
            {
                for (int i = 0; i < Gamepad.all.Count; i++)
                    if (Gamepad.all[i].deviceId == _deviceId) { _deviceId = Gamepad.all[(i + 1) % Gamepad.all.Count].deviceId; break; }
                ResetPeaks();
            }
            Meter("R2 / RT hardware", pad != null ? pad.rightTrigger.ReadUnprocessedValue() : 0f, _gasPeak);
            Meter("L2 / LT hardware", pad != null ? pad.leftTrigger.ReadUnprocessedValue() : 0f, _brakePeak);
            Vector2 stick = pad != null ? pad.leftStick.ReadUnprocessedValue() : Vector2.zero;
            GUILayout.Label($"Left stick  X {stick.x:+0.000;-0.000;0.000}   Y {stick.y:+0.000;-0.000;0.000}");
            Meter("Gas action", _input != null ? _input.RawAccelerator : 0f);
            Meter("Brake action", _input != null ? _input.RawBrake : 0f);
            if (_input != null) GUILayout.Label($"Sources: {_input.AcceleratorSource} / {_input.BrakeSource}");
            Meter(_car.Drive.throttle < 0f ? "Motor pedal REVERSE" : "Motor pedal FORWARD", Mathf.Abs(_car.Drive.throttle));
            Meter("Brake applied", _car.Drive.brake);
            GUILayout.Label($"{_car.Speed * 3.6f:0.0} km/h   {_car.SelectedGear} {_car.ForwardGear}   {_car.EngineRpm:0} RPM" +
                (_car.IsGearShifting ? (_car.GearShiftProgress < _car.Taxi.shiftUnloadFraction ? "  UNLOAD" : "  ENGAGE") : "  PULL") + $"\nMotor thrust {_car.AppliedDriveAcceleration:0.0} m/s²   Surface ×{_car.Surface.driveMultiplier:0.00}");
            GUILayout.Label(_input == null ? "VehicleInput MISSING" : !_input.isActiveAndEnabled ?
                "PLAYER INPUT DISABLED: automatic test may own the car" : !Application.isFocused || !_focus ?
                "INPUT FOCUS LOST: click the Game view" : Time.timeScale <= 0f ? "PAUSED" : "Player input enabled");
            if (pad != null) GUILayout.Label($"East / Circle (D): {pad.buttonEast.isPressed}   South / Cross (R): {pad.buttonSouth.isPressed}\nL1 / LB (turn): {pad.leftShoulder.isPressed}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset peaks")) ResetPeaks();
            if (GUILayout.Button(_recording == null ? "Record CSV" : "Stop recording"))
            { if (_recording == null) StartRecording(); else StopRecording(); }
            GUILayout.EndHorizontal();
            GUILayout.Label(_message, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
        private static void Meter(string label, float value, float peak = -1f)
        {
            GUILayout.Label($"{label}: {value * 100f:0.0}%" + (peak >= 0f ? $"   peak {peak * 100f:0.0}%" : ""));
            Rect rect = GUILayoutUtility.GetRect(1f, 9f, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none);
            Color previous = GUI.color;
            GUI.color = new Color(0.2f, 0.9f, 0.65f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
        private void StartRecording()
        {
            try
            {
#if UNITY_EDITOR
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../VehicleLab_Measurements"));
#else
                string directory = Path.Combine(Application.persistentDataPath, "VehicleLab_Measurements");
#endif
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "controller_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".csv");
                _recording = new StreamWriter(path) { AutoFlush = true };
                _recording.WriteLine("seconds,device_id,hardware_r2,hardware_l2,action_gas,action_brake,applied_gas,applied_brake,kmh,forward_gear,rpm,drive_acceleration,input_enabled,focus,time_scale,forward_kmh,measured_forward_acceleration,direction,trick,shifting,camera_fov,steer,feel_revision,shift_progress,shift_thrust_envelope,gear_shift_count,engine_demand,free_rev,rev_limiter");
                File.WriteAllText(Path.ChangeExtension(path, ".tuning.json"), JsonUtility.ToJson(_car.Taxi, true));
                _lastTime = 0f;
                _message = "Recording at 10 Hz to VehicleLab_Measurements/" + Path.GetFileName(path);
                Debug.Log("[VehicleLab meter] Recording controller travel to " + path);
            }
            catch (Exception e) { StopRecording(); _message = "Recording failed: " + e.Message; }
        }
        private void StopRecording()
        {
            _recording?.Dispose();
            if (_recording != null) _message = "Recording saved in VehicleLab_Measurements.";
            _recording = null;
        }
        private void OnDisable() { StopRecording(); }
    }
}
