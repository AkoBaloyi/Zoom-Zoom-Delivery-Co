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
        private void OnEnable() { if (_car != null) _car.TrickPerformed += Confirm; }
        private void OnDisable()
        {
            if (_car != null) _car.TrickPerformed -= Confirm;
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
        private void LateUpdate()
        {
            if (_car == null) return;
            _canvas.SetActive(true);
            _status.text = (_car.SelectedGear == TaxiGear.Drive ? "D" : "R") + "    " +
                (_car.Speed * 3.6f).ToString("0") + " km/h";
            _move.enabled = Time.time < _showUntil;
            bool pad = UnityEngine.InputSystem.Gamepad.current != null &&
                (UnityEngine.InputSystem.Keyboard.current == null ||
                UnityEngine.InputSystem.Gamepad.current.lastUpdateTime > UnityEngine.InputSystem.Keyboard.current.lastUpdateTime);
            _hint.text = pad ? "RT  GAS     LT  BRAKE     LB  QUICK TURN     B  DRIVE     A  REVERSE" :
                "W  GAS     S  BRAKE     SPACE  QUICK TURN     F  DRIVE     R  REVERSE";
        }
        private void OnDestroy() { if (_canvas != null) Destroy(_canvas); }
    }
}
