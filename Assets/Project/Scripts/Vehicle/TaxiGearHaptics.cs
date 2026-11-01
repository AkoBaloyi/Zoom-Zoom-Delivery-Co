using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomZoom.Vehicle
{
    /// <summary>A brief engagement pulse on the pad supplying player input; no adaptive-trigger dependency.</summary>
    [DisallowMultipleComponent]
    public sealed class TaxiGearHaptics : MonoBehaviour
    {
        private VehicleController _car;
        private VehicleInput _input;
        private Gamepad _pad;
        private float _wait, _remaining;
        private bool _pending, _focus = true;
        public int PulseCount { get; private set; }
        public bool IsPulsing { get; private set; }
        private void Awake() { _car = GetComponent<VehicleController>(); _input = GetComponent<VehicleInput>(); }
        private void OnEnable()
        {
            if (_car == null) return;
            _car.GearShifted += Shift;
            _car.Repositioned += Stop;
        }
        private void Shift(int from, int to)
        {
            Stop();
            if (!_focus || !_car.Taxi.shiftRumbleEnabled || _input == null || !_input.isActiveAndEnabled) return;
            _pad = _input.ActiveGamepad;
            if (_pad == null) return;
            _wait = Mathf.Max(0f, _car.Taxi.gearShiftTime) * 0.85f;
            _pending = true;
        }
        private void Update()
        {
            if (_pad == null) return;
            if (!_pad.added || !_focus || Time.timeScale <= 0f || !_car.Taxi.shiftRumbleEnabled ||
                !_car.IsGrounded || _car.IsFreeRevving || _input == null || !_input.isActiveAndEnabled) { Stop(); return; }
            if (_pending)
            {
                _wait -= Time.deltaTime;
                if (_wait > 0f) return;
                _pending = false;
                _remaining = Mathf.Max(0.01f, _car.Taxi.shiftRumbleDuration);
            }
            if (_remaining <= 0f) { Stop(); return; }
            float fade = Mathf.Clamp01(_remaining / Mathf.Max(0.01f, _car.Taxi.shiftRumbleDuration));
            if (!IsPulsing) { IsPulsing = true; PulseCount++; }
            _pad.SetMotorSpeeds(Mathf.Clamp01(_car.Taxi.shiftRumbleLow) * fade,
                Mathf.Clamp01(_car.Taxi.shiftRumbleHigh) * fade);
            _remaining -= Time.deltaTime;
        }
        private void Stop()
        {
            if (_pad != null && _pad.added) _pad.SetMotorSpeeds(0f, 0f);
            _pad = null; _pending = IsPulsing = false; _remaining = _wait = 0f;
        }
        private void OnApplicationFocus(bool focus) { _focus = focus; if (!focus) Stop(); }
        private void OnDisable()
        {
            Stop();
            if (_car == null) return;
            _car.GearShifted -= Shift;
            _car.Repositioned -= Stop;
        }
    }
}
