using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Vehicle
{
    /// <summary>Persistent gear/speed readout and an immediate confirmation for each successful move.</summary>
    [DisallowMultipleComponent]
    public sealed class CrazyTaxiFeedback : MonoBehaviour
    {
        private VehicleController _car;
        private Text _status, _move, _hint;
        private float _showUntil;
        private GameObject _canvas;
        private void Awake()
        {
            _car = GetComponent<VehicleController>();
            if (_car == null) { enabled = false; return; }
            _canvas = new GameObject("Driving HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _canvas.transform.SetParent(transform, false);
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.GetComponent<Canvas>().sortingOrder = 9;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _status = Label("Gear and speed", new Vector2(30, 75), 32, Color.white);
            _status.rectTransform.sizeDelta = new Vector2(1250, 45);
            _status.fontSize = 28;
            _move = Label("Manoeuvre", new Vector2(30, 125), 34, new Color(1f, 0.82f, 0.18f));
            _hint = Label("Controls", new Vector2(30, 25), 18, new Color(0.85f, 0.9f, 1f));
        }
        private Text Label(string name, Vector2 position, int size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(_canvas.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(850, 45);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = colour;
            text.raycastTarget = false;
            go.GetComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }
        private void OnEnable()
        {
            if (_car == null) return;
            _car.TrickPerformed += Confirm;
            _car.Recovered += ShowRecovery;
        }
        private void OnDisable()
        {
            if (_car != null) _car.TrickPerformed -= Confirm;
            if (_car != null) _car.Recovered -= ShowRecovery;
            if (_canvas != null) _canvas.SetActive(false);
        }
        private void Confirm(TaxiTrick trick)
        {
            string name = trick == TaxiTrick.LimiterCut ? "LIMITER CUT" :
                trick == TaxiTrick.QuickSpin ? "QUICK TURN" :
                trick == TaxiTrick.BackDash ? "CRAZY BACK DASH" :
                trick == TaxiTrick.BackDrift ? "CRAZY BACK DRIFT" : "CRAZY " + trick.ToString().ToUpperInvariant();
            _move.text = name;
            _showUntil = Time.time + 1.1f;
        }
        private void ShowRecovery(float cost)
        {
            _move.text = $"BACK ON ROAD   -{cost:0.#}s";
            _showUntil = Time.time + 2f;
        }
        private void LateUpdate()
        {
            if (_car == null) return;
            _canvas.SetActive(true);
            _status.text = (_car.SelectedGear == TaxiGear.Drive ? "D" + _car.ForwardGear : "R") + "    " +
                (_car.Speed * 3.6f).ToString("0") + " km/h    " + _car.EngineRpm.ToString("0") + " RPM" +
                (_car.IsAtRevLimiter ? "  LIMITER" : _car.IsFreeRevving ? "  FREE REV" : _car.IsGearShifting ? "  SHIFT" : "");
            if (_car.Taxi.directTechniqueControls)
                _status.text += _car.ActiveTrick == TaxiTrick.Dash ? "    DASH ACTIVE" :
                    _car.DashCooldownRemaining > 0f ? $"    DASH {_car.DashCooldownRemaining:0.0}s" : "    DASH READY";
            _status.color = _car.IsGearShifting ? new Color(1f, 0.82f, 0.18f) : Color.white;
            _move.enabled = Time.time < _showUntil;
            bool pad = UnityEngine.InputSystem.Gamepad.current != null &&
                (UnityEngine.InputSystem.Keyboard.current == null ||
                UnityEngine.InputSystem.Gamepad.current.lastUpdateTime > UnityEngine.InputSystem.Keyboard.current.lastUpdateTime);
            bool sony = pad && UnityEngine.InputSystem.Gamepad.current is UnityEngine.InputSystem.DualShock.DualShockGamepad;
            string brakeHint = _car.Taxi.brakeToReverse ? "BRAKE / REVERSE" : "BRAKE";
            if (_car.Taxi.directTechniqueControls)
            {
                _hint.text = sony ? "HOLD R2/L2 + PRESS R1: DASH/STOP   STEER + PRESS SQUARE: DRIFT   STEER + L1: TURN" :
                    pad ? "HOLD RT/LT + PRESS RB: DASH/STOP   STEER + PRESS X: DRIFT   STEER + LB: TURN" :
                    "HOLD W/S + PRESS SHIFT: DASH/STOP   STEER + PRESS CTRL: DRIFT   STEER + SPACE: TURN";
                var vehicleInput = _car.GetComponent<VehicleInput>();
                _hint.text += "\n" + (pad ? "D-PAD UP" : "F8") + "  MOTION EFFECTS: " +
                    (vehicleInput == null || vehicleInput.MotionEffectsEnabled ? "ON" : "OFF");
                _hint.rectTransform.sizeDelta = new Vector2(1250, 55);
                return;
            }
            _hint.text = sony ? "R2  GAS     L2  " + brakeHint + "     STEER + L1  TURN     CIRCLE / CROSS  TRICKS" :
                pad ? "RT  GAS     LT  " + brakeHint + "     STEER + LB  TURN     B / A  TRICKS" :
                "W  GAS     S  " + brakeHint + "     A/D + SPACE  TURN     F / R  TRICKS";
        }
        private void OnDestroy() { if (_canvas != null) Destroy(_canvas); }
    }
}
