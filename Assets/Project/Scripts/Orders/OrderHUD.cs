using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// The player-facing stats panel, top-right: shift clock, cargo hold, score and level/XP.
    /// The list of active orders is not here any more, it moved to OrderTicketStrip, which shows
    /// each order as an Overcooked style ticket, as the project plan's Concept section describes. This panel is deliberately small so the tickets
    /// are what the eye goes to. Real Canvas + UI.Text, builds itself in code.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderHUD : MonoBehaviour
    {
        [Header("Wiring (found automatically if left empty)")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private CargoSystem cargoSystem;
        [SerializeField] private ShiftTimer shiftTimer;
        [Tooltip("Optional. When its own toggle is on, the cargo line shows condition.")]
        [SerializeField] private CargoCondition cargoCondition;
        [Tooltip("Optional. When assigned, a level/XP line and bar are added.")]
        [SerializeField] private PlayerProgression playerProgression;

        [Header("Art")]
        [SerializeField] private HudSkin skin;

        [Header("Layout")]
        [SerializeField] private Vector2 panelPosition = new Vector2(-24f, -24f); // offset from the top-right corner
        [SerializeField] private Vector2 panelSize = new Vector2(380f, 178f);
        [SerializeField] private int fontSize = 24;
        [SerializeField] private float urgentShiftSeconds = 30f;

        private Text _timerLine;
        private Text _cargoLine;
        private Text _scoreLine;
        private Text _xpLine;
        private RectTransform _xpBarFill;
        private float _xpBarWidth;
        private readonly List<Image> _cargoSlots = new List<Image>();
        private float _pad = 14f;

        private int _deliveredCount;
        private int _lateCount;
        private float _totalValueEarned;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (cargoSystem == null) cargoSystem = FindAnyObjectByType<CargoSystem>();
            if (shiftTimer == null) shiftTimer = FindAnyObjectByType<ShiftTimer>();
            if (cargoCondition == null) cargoCondition = FindAnyObjectByType<CargoCondition>();
            if (playerProgression == null) playerProgression = FindAnyObjectByType<PlayerProgression>();

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

            RefreshTimerLine();
            RefreshCargoLine();
            RefreshScoreLine();
            RefreshXpLine();
        }

        private void RefreshCargoLine()
        {
            if (_cargoLine == null) return;

            if (cargoSystem == null)
            {
                _cargoLine.text = "Cargo: (no cargo system found)";
                return;
            }

            RefreshCargoSlots();
            bool slotArt = _cargoSlots.Count > 0;

            if (cargoSystem.SlotsUsed == 0)
            {
                _cargoLine.text = slotArt ? "Cargo" : $"Cargo: empty (0/{cargoSystem.Capacity})";
                _cargoLine.color = OrderPalette.Neutral;
                return;
            }

            var ids = new List<string>(cargoSystem.SlotsUsed);
            bool showCondition = cargoCondition != null && cargoCondition.ConditionEnabled;

            foreach (Order o in cargoSystem.CarriedOrders)
            {
                ids.Add(showCondition
                    ? $"#{o.Id} @ {cargoCondition.GetConditionPercent(o):0}%"
                    : $"#{o.Id}");
            }

            _cargoLine.text = slotArt
                ? $"Cargo  {string.Join(", ", ids)}"
                : $"Cargo {cargoSystem.SlotsUsed}/{cargoSystem.Capacity}: {string.Join(", ", ids)}";

            if (showCondition)
            {
                float worst = 100f;
                foreach (Order o in cargoSystem.CarriedOrders)
                    worst = Mathf.Min(worst, cargoCondition.GetConditionPercent(o));

                _cargoLine.color = Color.Lerp(OrderPalette.Urgent, OrderPalette.Deliver, worst / 100f);
            }
            else
            {
                _cargoLine.color = OrderPalette.Deliver;
            }
        }

        /// <summary>One slot icon per unit of capacity, filled for each carried order. Only
        /// exists when the skin has both cargo slot sprites.</summary>
        private void RefreshCargoSlots()
        {
            for (int i = 0; i < _cargoSlots.Count; i++)
            {
                bool exists = i < cargoSystem.Capacity;
                _cargoSlots[i].enabled = exists;
                if (!exists) continue;

                _cargoSlots[i].sprite = i < cargoSystem.SlotsUsed ? skin.cargoSlotFilled : skin.cargoSlotEmpty;
            }
        }

        private void RefreshScoreLine()
        {
            if (_scoreLine == null) return;

            // Money comes from PlayerProgression (Task 20) when present, since that is the
            // balance the upgrade screen will spend from. Without it, the shift's earned value.
            float money = playerProgression != null ? playerProgression.Money : _totalValueEarned;
            _scoreLine.text = $"Delivered {_deliveredCount}   Late {_lateCount}   ${money:0}";
        }

        private void RefreshXpLine()
        {
            if (_xpLine == null) return;

            if (playerProgression == null)
            {
                _xpLine.text = string.Empty;
                if (_xpBarFill != null) _xpBarFill.gameObject.SetActive(false);
                return;
            }

            _xpLine.text = $"Level {playerProgression.Level}   " +
                           $"XP {playerProgression.XpIntoLevel:0}/{playerProgression.XpToNextLevel:0}";

            if (_xpBarFill != null)
            {
                float barHeight = _xpBarFill.sizeDelta.y;
                float width = _xpBarWidth * playerProgression.LevelProgress01;
                bool pill = skin != null && skin.barFill != null;

                // A sliced pill cannot be narrower than its height without its ends overlapping.
                _xpBarFill.gameObject.SetActive(!pill || width >= barHeight);
                _xpBarFill.sizeDelta = new Vector2(Mathf.Max(pill ? barHeight : 0f, width), barHeight);
            }
        }

        private void RefreshTimerLine()
        {
            if (_timerLine == null || shiftTimer == null) return;

            _timerLine.text = $"Shift  {shiftTimer.FormattedTimeRemaining}";
            _timerLine.color = shiftTimer.TimeRemaining <= urgentShiftSeconds
                ? OrderPalette.Urgent
                : OrderPalette.Neutral;
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
            panelImage.color = OrderPalette.PanelDark;
            panelImage.raycastTarget = false;

            // The wood frame art is thicker than the flat panel's margin, so with art the
            // contents move in and the panel grows by the same amount to keep the same room inside.
            bool framed = skin != null && skin.statsPanel != null;
            _pad = framed ? 32f : 14f;
            Vector2 size = panelSize + Vector2.one * (2f * (_pad - 14f));

            if (framed && skin.ApplyFrame(panelImage, skin.statsPanel, size.y)) panelImage.color = Color.white;

            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = panelPosition;
            panelRect.sizeDelta = size;

            float y = 2f - _pad;
            const float lineHeight = 32f;

            _timerLine = CreateLine(panelRect, "Timer", ref y, lineHeight, fontSize + 6, OrderPalette.Neutral);
            _timerLine.fontStyle = FontStyle.Bold;
            _cargoLine = CreateLine(panelRect, "Cargo", ref y, lineHeight, fontSize - 2, OrderPalette.Neutral);
            _scoreLine = CreateLine(panelRect, "Score", ref y, lineHeight, fontSize - 4, OrderPalette.Neutral);
            _xpLine = CreateLine(panelRect, "XP", ref y, lineHeight, fontSize - 4, OrderPalette.Xp);

            BuildCargoSlots(panelRect, _cargoLine.rectTransform.anchoredPosition.y);

            // XP star sits at the left end of the XP bar, the bar starts after it.
            float barHeight = skin != null && skin.barFill != null ? 16f : 12f;
            float barX = _pad;
            if (skin != null && skin.xpIcon != null)
            {
                var starGO = new GameObject("XP star");
                starGO.transform.SetParent(panelRect, false);
                Image star = starGO.AddComponent<Image>();
                star.raycastTarget = false;
                HudSkin.ApplyIcon(star, skin.xpIcon);
                RectTransform starRect = star.rectTransform;
                starRect.anchorMin = starRect.anchorMax = new Vector2(0f, 1f);
                starRect.pivot = new Vector2(0f, 0.5f);
                starRect.anchoredPosition = new Vector2(_pad - 2f, y - 2f - barHeight * 0.5f);
                starRect.sizeDelta = new Vector2(28f, 28f);
                barX += 30f;
            }

            _xpBarWidth = size.x - _pad - barX;

            var barBgGO = new GameObject("XP bar background");
            barBgGO.transform.SetParent(panelRect, false);
            Image barBg = barBgGO.AddComponent<Image>();
            barBg.color = new Color(0f, 0f, 0f, 0.6f);
            barBg.raycastTarget = false;
            RectTransform barBgRect = barBg.rectTransform;
            barBgRect.anchorMin = new Vector2(0f, 1f);
            barBgRect.anchorMax = new Vector2(0f, 1f);
            barBgRect.pivot = new Vector2(0f, 1f);
            barBgRect.anchoredPosition = new Vector2(barX, y - 2f);
            barBgRect.sizeDelta = new Vector2(_xpBarWidth, barHeight);
            if (skin != null && HudSkin.ApplyPill(barBg, skin.barBackground, barHeight)) barBg.color = Color.white;

            var barFillGO = new GameObject("XP bar fill");
            barFillGO.transform.SetParent(panelRect, false);
            Image barFill = barFillGO.AddComponent<Image>();
            barFill.color = OrderPalette.Xp;
            barFill.raycastTarget = false;
            _xpBarFill = barFill.rectTransform;
            _xpBarFill.anchorMin = new Vector2(0f, 1f);
            _xpBarFill.anchorMax = new Vector2(0f, 1f);
            _xpBarFill.pivot = new Vector2(0f, 1f);
            _xpBarFill.anchoredPosition = new Vector2(barX, y - 2f);
            _xpBarFill.sizeDelta = new Vector2(0f, barHeight);
            if (skin != null) HudSkin.ApplyPill(barFill, skin.barFill, barHeight);
        }

        /// <summary>Three slot icons right aligned on the cargo line (the most capacity the
        /// plan's upgrades reach). Unused ones are hidden by RefreshCargoSlots.</summary>
        private void BuildCargoSlots(RectTransform panelRect, float lineY)
        {
            if (skin == null || skin.cargoSlotEmpty == null || skin.cargoSlotFilled == null) return;

            const int maxSlots = 3;
            const float slotSize = 30f, gap = 4f;
            float right = panelRect.sizeDelta.x - _pad;

            for (int i = 0; i < maxSlots; i++)
            {
                var go = new GameObject($"Cargo slot {i}");
                go.transform.SetParent(panelRect, false);
                Image slot = go.AddComponent<Image>();
                slot.raycastTarget = false;
                HudSkin.ApplyIcon(slot, skin.cargoSlotEmpty);

                RectTransform rect = slot.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(right - (i + 1) * slotSize - i * gap, lineY + 1f);
                rect.sizeDelta = new Vector2(slotSize, slotSize);

                _cargoSlots.Add(slot);
            }
        }

        private Text CreateLine(RectTransform parent, string name, ref float y,
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
            text.raycastTarget = false;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(_pad, y);
            rect.sizeDelta = new Vector2(parent.sizeDelta.x - 2f * _pad, advance);

            y -= advance;
            return text;
        }
    }
}
