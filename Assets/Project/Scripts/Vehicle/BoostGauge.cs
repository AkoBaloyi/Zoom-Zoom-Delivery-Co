using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// The boost tank on screen: a bar that drains while boost is spent and lights up while boost
    /// is being earned.
    ///
    /// WHY THE LIGHT MATTERS MORE THAN THE BAR
    /// Boost is earned by drifting and by air time. A reward the player cannot see is not a
    /// reward, it is a hidden rule, and hidden rules teach nothing. So the moment the car starts
    /// earning, the bar changes colour and a short label says what for. The player slides, the
    /// bar lights and climbs, and the connection is made without a tutorial. The empty-tank state
    /// is shown too, so "why won't it boost" has a visible answer.
    ///
    /// BUILT IN CODE, LIKE THE HUD
    /// Canvas, Image and Text created at runtime from serialised numbers, the same pattern
    /// OrderHUD and ShiftResults use, so there is no prefab to keep in step with the scene and the
    /// layout can be tuned in the inspector. Bottom-left corner: the order HUD owns the top and
    /// the right, and the speed of the car is read from the road, not from a number, so the
    /// gauge sits where the eye is not already busy.
    ///
    /// Reads VehicleController only. Never writes to it.
    /// </summary>
    [DisallowMultipleComponent]
    public class BoostGauge : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Found automatically if left empty.")]
        [SerializeField] private VehicleController car;

        [Header("Layout")]
        [SerializeField] private Vector2 barSize = new Vector2(320f, 22f);
        [SerializeField] private Vector2 cornerOffset = new Vector2(28f, 28f);
        [SerializeField] private int labelFontSize = 20;

        [Header("Colours")]
        [SerializeField] private Color backColour = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color fillColour = new Color(1f, 0.62f, 0.1f, 1f);
        [SerializeField] private Color spendingColour = new Color(1f, 0.9f, 0.4f, 1f);
        [SerializeField] private Color earningColour = new Color(0.2f, 0.85f, 1f, 1f);
        [SerializeField] private Color emptyColour = new Color(1f, 0.2f, 0.15f, 1f);
        [SerializeField] private Color labelColour = Color.white;

        [Header("Feel")]
        [Tooltip("Seconds for the fill to catch up with the real tank level. A little lag reads as " +
                 "liquid rather than a number; too much and the player does not trust it.")]
        [Range(0f, 0.3f)]
        public float fillSmoothTime = 0.06f;

        [Tooltip("Seconds the EARNING label stays up after earning stops, so a short slide still " +
                 "reads as having paid rather than flickering.")]
        [Range(0f, 1f)]
        public float earningLabelHold = 0.4f;

        private RectTransform _fillRect;
        private Image _fill;
        private Text _label;
        private float _shownFraction;
        private float _fillVelocity;
        private float _earningLabelTimer;

        private void Awake()
        {
            if (car == null) car = FindAnyObjectByType<VehicleController>();
            if (car == null)
            {
                Debug.LogWarning("[BoostGauge] No VehicleController in the scene, so there is no " +
                                 "tank to show. Switching off.", this);
                enabled = false;
                return;
            }

            Build();
            _shownFraction = car.BoostFraction;
        }

        private void LateUpdate()
        {
            if (car == null || car.Tuning == null) return;

            bool boostOn = car.Tuning.boostEnabled;
            if (_fillRect.parent.gameObject.activeSelf != boostOn)
                _fillRect.parent.gameObject.SetActive(boostOn);
            if (!boostOn) return;

            float dt = Time.deltaTime;

            _shownFraction = Mathf.SmoothDamp(_shownFraction, car.BoostFraction, ref _fillVelocity,
                fillSmoothTime, Mathf.Infinity, dt);
            _fillRect.anchorMax = new Vector2(Mathf.Clamp01(_shownFraction), 1f);

            if (car.IsEarningBoost) _earningLabelTimer = earningLabelHold;
            else _earningLabelTimer = Mathf.Max(0f, _earningLabelTimer - dt);

            bool empty = car.BoostRemaining <= 0.01f;

            if (car.IsEarningBoost)
            {
                _fill.color = earningColour;
                _label.text = car.IsDrifting ? "BOOST  +DRIFT" : "BOOST  +AIR";
                _label.color = earningColour;
            }
            else if (car.IsBoosting)
            {
                _fill.color = spendingColour;
                _label.text = car.IsSupersonic ? "BOOST  SUPERSONIC" : "BOOST";
                _label.color = labelColour;
            }
            else if (empty)
            {
                _fill.color = emptyColour;
                _label.text = "BOOST  EMPTY  drift or jump to earn";
                _label.color = emptyColour;
            }
            else if (_earningLabelTimer > 0f)
            {
                // Earning just stopped: hold the colour so a short slide registers.
                _fill.color = earningColour;
                _label.color = earningColour;
            }
            else
            {
                _fill.color = fillColour;
                _label.text = "BOOST";
                _label.color = labelColour;
            }
        }

        // ==================================================================
        // LAYOUT
        // ==================================================================

        private void Build()
        {
            var canvasGO = new GameObject("BoostGauge Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // above the world, below the results screen (50)

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Bottom-left anchored root for the whole gauge.
            var root = new GameObject("Gauge");
            root.transform.SetParent(canvasGO.transform, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.zero;
            rootRect.pivot = Vector2.zero;
            rootRect.anchoredPosition = cornerOffset;
            rootRect.sizeDelta = new Vector2(barSize.x, barSize.y + labelFontSize + 10f);

            // Back plate.
            var back = new GameObject("Back");
            back.transform.SetParent(root.transform, false);
            Image backImage = back.AddComponent<Image>();
            backImage.color = backColour;
            RectTransform backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = Vector2.zero;
            backRect.anchorMax = new Vector2(1f, 0f);
            backRect.pivot = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = Vector2.zero;
            backRect.sizeDelta = new Vector2(0f, barSize.y);

            // Fill, anchored left-to-right so anchorMax.x is the fraction.
            var fill = new GameObject("Fill");
            fill.transform.SetParent(back.transform, false);
            _fill = fill.AddComponent<Image>();
            _fill.color = fillColour;
            _fillRect = fill.GetComponent<RectTransform>();
            _fillRect.anchorMin = Vector2.zero;
            _fillRect.anchorMax = new Vector2(1f, 1f);
            _fillRect.offsetMin = new Vector2(3f, 3f);
            _fillRect.offsetMax = new Vector2(-3f, -3f);

            // Label above the bar.
            var label = new GameObject("Label");
            label.transform.SetParent(root.transform, false);
            _label = label.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = labelFontSize;
            _label.fontStyle = FontStyle.Bold;
            _label.color = labelColour;
            _label.alignment = TextAnchor.LowerLeft;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.text = "BOOST";
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(2f, barSize.y + 4f);
            labelRect.sizeDelta = new Vector2(0f, labelFontSize + 6f);
        }
    }
}
