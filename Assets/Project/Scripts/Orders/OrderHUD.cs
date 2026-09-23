using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// The player-facing HUD for orders and cargo: which orders are active and how long they have
    /// left, what the cargo slot is carrying, and a running score. Real Canvas + UI.Text, not a
    /// debug OnGUI overlay, meant to ship. Builds itself in code, add the component and press Play.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderHUD : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private CargoSystem cargoSystem;
        [SerializeField] private ShiftTimer shiftTimer;

        [Header("Layout")]
        [SerializeField] private Vector2 panelPosition = new Vector2(-24f, -24f); // offset from the top-right corner
        [SerializeField] private Vector2 panelSize = new Vector2(420f, 340f);
        [SerializeField] private int fontSize = 24;
        [SerializeField] private int headerFontSize = 20;
        [SerializeField] private int orderLineSlots = 6;

        [Header("Colours (matches DestinationMarker and OrderBeacon exactly, so all three " +
                "channels agree)")]
        [SerializeField] private Color normalColour = Color.white;
        [SerializeField] private Color pickupColour = new Color(0.15f, 0.6f, 1f, 1f);
        [SerializeField] private Color dropOffColour = new Color(0.15f, 1f, 0.3f, 1f);
        [SerializeField] private Color urgentColour = new Color(1f, 0.05f, 0.05f, 1f);
        [SerializeField] private Color panelColour = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private float urgentBelowSeconds = 8f;

        private Text _timerLine;
        private Text _cargoLine;
        private Text _scoreLine;
        private Text _headerLine;
        private readonly List<Text> _orderLines = new List<Text>();

        private int _deliveredCount;
        private int _lateCount;
        private float _totalValueEarned;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (cargoSystem == null) cargoSystem = FindAnyObjectByType<CargoSystem>();
            if (shiftTimer == null) shiftTimer = FindAnyObjectByType<ShiftTimer>();

            if (orderManager == null)
            {
                Debug.LogError(
                    "[OrderHUD] No OrderManager found in the scene. The HUD has nothing to read " +
                    "from and has switched itself off.", this);
                enabled = false;
                return;
            }

            BuildCanvas();
            orderManager.OrderResolved += HandleOrderResolved;
        }

        private void OnDestroy()
        {
            if (orderManager != null) orderManager.OrderResolved -= HandleOrderResolved;
        }

        private void HandleOrderResolved(Order order, float value)
        {
            if (order.State == OrderState.Delivered)
            {
                _deliveredCount++;
                _totalValueEarned += value;
            }
            else
            {
                _lateCount++;
            }
        }

        private void Update()
        {
            if (orderManager == null) return;

            RefreshOrderLines();
            RefreshCargoLine();
            RefreshScoreLine();
            RefreshTimerLine();
        }

        private void RefreshOrderLines()
        {
            IReadOnlyList<Order> active = orderManager.ActiveOrders;

            for (int i = 0; i < _orderLines.Count; i++)
            {
                if (i >= active.Count)
                {
                    _orderLines[i].text = string.Empty;
                    continue;
                }

                Order order = active[i];
                bool goingToDropOff = order.State == OrderState.Carried || order.State == OrderState.Collected;

                _orderLines[i].text = $"Order {order.Id}   {order.State}   {order.TimeRemaining:0.0}s";

                // Exactly the same blend DestinationMarker and OrderBeacon use: base colour by
                // pickup/drop-off, shifted toward red as the timer runs out. A player should
                // never see a different colour for the same order across the three channels.
                Color baseColour = goingToDropOff ? dropOffColour : pickupColour;
                float urgency = 1f - Mathf.Clamp01(order.TimeRemaining / Mathf.Max(0.01f, urgentBelowSeconds));
                _orderLines[i].color = Color.Lerp(baseColour, urgentColour, urgency);
            }

            if (active.Count > _orderLines.Count)
            {
                Debug.LogWarning(
                    $"[OrderHUD] {active.Count} orders active but only {_orderLines.Count} HUD lines " +
                    "exist. Raise orderLineSlots.", this);
            }
        }

        private void RefreshCargoLine()
        {
            if (_cargoLine == null) return;

            if (cargoSystem == null)
            {
                _cargoLine.text = "Cargo: (no cargo system found)";
                return;
            }

            if (cargoSystem.SlotsUsed == 0)
            {
                _cargoLine.text = $"Cargo: empty (0/{cargoSystem.Capacity})";
                _cargoLine.color = normalColour;
                return;
            }

            var ids = new List<string>(cargoSystem.SlotsUsed);
            foreach (Order o in cargoSystem.CarriedOrders) ids.Add(o.Id.ToString());

            _cargoLine.text = $"Cargo: {cargoSystem.SlotsUsed}/{cargoSystem.Capacity} (orders {string.Join(", ", ids)})";
            _cargoLine.color = dropOffColour; // matches the "carried" colour everywhere else
        }

        private void RefreshScoreLine()
        {
            if (_scoreLine == null) return;

            _scoreLine.text =
                $"Delivered {_deliveredCount}   Late {_lateCount}   Value {_totalValueEarned:0}";
        }

        private void RefreshTimerLine()
        {
            if (_timerLine == null || shiftTimer == null) return;

            _timerLine.text = $"Shift  {shiftTimer.FormattedTimeRemaining}";
            _timerLine.color = shiftTimer.TimeRemaining <= 30f ? urgentColour : normalColour;
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("OrderHUD Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);

            Image panelImage = panelGO.AddComponent<Image>();
            panelImage.color = panelColour;

            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            // Anchored to the screen's top-right corner rather than top-left, so it doesn't sit
            // where OrderMarkers' own corner text used to be.
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = panelPosition;
            panelRect.sizeDelta = panelSize;

            float y = -12f;
            const float lineHeight = 30f;

            _timerLine = CreateLine(panelRect, "Timer", ref y, lineHeight, fontSize + 4, normalColour);
            _timerLine.fontStyle = FontStyle.Bold;
            _cargoLine = CreateLine(panelRect, "Cargo", ref y, lineHeight, fontSize, normalColour);
            _scoreLine = CreateLine(panelRect, "Score", ref y, lineHeight, fontSize, normalColour);

            y -= 6f;

            _headerLine = CreateLine(panelRect, "Header", ref y, headerFontSize, headerFontSize,
                normalColour);
            _headerLine.text = "ACTIVE ORDERS";
            _headerLine.fontStyle = FontStyle.Bold;

            for (int i = 0; i < Mathf.Max(1, orderLineSlots); i++)
            {
                Text line = CreateLine(panelRect, $"Order line {i}", ref y, lineHeight, fontSize,
                    normalColour);
                _orderLines.Add(line);
            }
        }

        private static Text CreateLine(RectTransform parent, string name, ref float y,
            float advance, int size, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(14f, y);
            rect.sizeDelta = new Vector2(parent.sizeDelta.x - 28f, advance);

            y -= advance;
            return text;
        }
    }
}