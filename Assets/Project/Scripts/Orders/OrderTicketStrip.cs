using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// Overcooked style order cards along the top left of the screen, oldest on the left.
    ///
    /// Each card shows the order number, the food and the seconds left, PICK and DROP step
    /// badges, where to pick up and where to deliver, what the order pays right now (early, on
    /// time or late), and its own patience bar, separate from the shift clock, with marks where
    /// early ends and late begins. The card shakes when the bar is nearly empty. When an order
    /// ends, a stamp lands on its card (EARLY, ON TIME, LATE or CANCELLED), a short message says
    /// what it paid or cost, and the card slides away.
    ///
    /// Built in code, nothing to wire. All pay numbers come from the Order Tuning asset.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderTicketStrip : MonoBehaviour
    {
        [Header("Wiring (all found automatically if left empty)")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private CargoSystem cargoSystem;
        [SerializeField] private CargoCondition cargoCondition;

        [Header("Art (optional)")]
        [Tooltip("Sprites for cards, badges, stamps and bars. Leave empty for flat colours.")]
        [SerializeField] private HudSkin skin;

        [Header("Layout")]
        [Tooltip("Offset from the screen's top left corner, in reference pixels (1920x1080).")]
        [SerializeField] private Vector2 stripOrigin = new Vector2(24f, -24f);
        [Tooltip("Size of one order card before Ticket Scale.")]
        [SerializeField] private Vector2 cardSize = new Vector2(240f, 156f);
        [SerializeField] private float ticketGap = 10f;
        [Tooltip("Scales every card, its text and the gaps. 1 is the original size.")]
        [SerializeField] private float ticketScale = 1.05f;

        [Header("Behaviour")]
        [Tooltip("Seconds left below which a card's bar shakes.")]
        [SerializeField] private float urgentBelowSeconds = 8f;
        [Tooltip("Seconds a stamp stays on a card before it slides away.")]
        [SerializeField] private float resolvedLingerSeconds = 1.1f;
        [Tooltip("Seconds each message stays on screen.")]
        [SerializeField] private float popupSeconds = 1.8f;

        private static readonly string[] StepLabels = { "PICK", "DROP" };
        private static readonly OrderStep[] Steps = { OrderStep.Pickup, OrderStep.Deliver };

        private class Card
        {
            public Order Order;
            public RectTransform Root;
            public Image Paper;
            public Text Title;
            public Text TimeText;
            public Image FoodIcon;
            public RectTransform[] BadgeRects;
            public Image[] Badges;
            public Image[] BadgeIcons;
            public Text[] BadgeLabels;
            public Text FromText;
            public Text ToText;
            public Text PayText;
            public RectTransform BarFill;
            public Image BarFillImage;
            public Text Stamp;
            public Image StampImage;
            public RectTransform StampRect;

            public Vector2 Position;
            public bool Resolving;
            public float ResolveTimer;
            public float StampTimer;
            public float StampDuration;
        }

        private readonly List<Card> _cards = new List<Card>();
        private readonly Dictionary<Order, Card> _byOrder = new Dictionary<Order, Card>();
        private readonly Queue<KeyValuePair<string, Color>> _popupQueue = new Queue<KeyValuePair<string, Color>>();

        private RectTransform _stripRoot;
        private Text _popup;
        private float _popupTimer;
        private Font _font;

        private float BarWidth => cardSize.x - 20f;

        // ---- setup ----------------------------------------------------------------------------

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (cargoSystem == null) cargoSystem = FindAnyObjectByType<CargoSystem>();
            if (cargoCondition == null) cargoCondition = FindAnyObjectByType<CargoCondition>();

            if (orderManager == null)
            {
                Debug.LogError("[OrderTicketStrip] No OrderManager in the scene. Switching off.", this);
                enabled = false;
                return;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            HudSkin.LogStatus(skin, "OrderTicketStrip", this);
            BuildCanvas();

            orderManager.OrderPaid += HandleOrderPaid;
            if (cargoSystem != null) cargoSystem.CapacityChanged += HandleCapacityChanged;
        }

        private void OnDestroy()
        {
            if (orderManager != null) orderManager.OrderPaid -= HandleOrderPaid;
            if (cargoSystem != null) cargoSystem.CapacityChanged -= HandleCapacityChanged;
        }

        // ---- events ---------------------------------------------------------------------------

        private void HandleOrderPaid(Order order, OrderPayout payout)
        {
            if (_byOrder.TryGetValue(order, out Card card))
            {
                card.Resolving = true;
                card.ResolveTimer = resolvedLingerSeconds;

                switch (payout.Timing)
                {
                    case DeliveryTiming.Early:
                        ShowStamp(card, "EARLY!", OrderPalette.Deliver, SkinSprite(s => s.stampDelivered));
                        break;
                    case DeliveryTiming.OnTime:
                        ShowStamp(card, "ON TIME", OrderPalette.Deliver, SkinSprite(s => s.stampDelivered));
                        break;
                    case DeliveryTiming.Late:
                        ShowStamp(card, "LATE", new Color(1f, 0.55f, 0.1f, 1f), SkinSprite(s => s.stampLate));
                        break;
                    default:
                        ShowStamp(card, "CANCELLED", OrderPalette.Urgent, null);
                        break;
                }
            }

            switch (payout.Timing)
            {
                case DeliveryTiming.Early:
                    PushPopup($"Order #{order.Id} early, +R{payout.Total:0}", OrderPalette.Deliver);
                    break;
                case DeliveryTiming.OnTime:
                    PushPopup($"Order #{order.Id} on time, +R{payout.Total:0}", OrderPalette.Paper);
                    break;
                case DeliveryTiming.Late:
                    PushPopup($"Order #{order.Id} late, +R{payout.Total:0}", new Color(1f, 0.6f, 0.15f, 1f));
                    break;
                default:
                    PushPopup($"Order #{order.Id} cancelled, minus R{-payout.Penalty:0}", OrderPalette.Urgent);
                    break;
            }
        }

        private void HandleCapacityChanged(int capacity)
        {
            PushPopup($"Cargo upgraded: {capacity} slots", OrderPalette.Money);
        }

        // ---- per frame ------------------------------------------------------------------------

        private void Update()
        {
            SyncCards();
            UpdateCards(Time.unscaledDeltaTime);
            UpdatePopup(Time.unscaledDeltaTime);
        }

        private void SyncCards()
        {
            IReadOnlyList<Order> active = orderManager.ActiveOrders;
            for (int i = 0; i < active.Count; i++)
            {
                if (!_byOrder.ContainsKey(active[i])) BuildCard(active[i]);
            }
        }

        private void UpdateCards(float dt)
        {
            float follow = 1f - Mathf.Exp(-12f * dt);

            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                Card c = _cards[i];

                if (c.Resolving)
                {
                    c.ResolveTimer -= dt;
                    if (c.ResolveTimer <= -0.5f)
                    {
                        _byOrder.Remove(c.Order);
                        _cards.RemoveAt(i);
                        Destroy(c.Root.gameObject);
                        continue;
                    }
                }

                RefreshCard(c);
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                Card c = _cards[i];
                var target = new Vector2(i * (cardSize.x + ticketGap), 0f);
                if (c.Resolving && c.ResolveTimer <= 0f) target.y = 240f; // slide up and away

                c.Position = Vector2.Lerp(c.Position, target, follow);

                Vector2 shake = Vector2.zero;
                if (!c.Resolving && !c.Order.IsTerminal && c.Order.TimeRemaining < urgentBelowSeconds)
                {
                    float strength = 1f - Mathf.Clamp01(c.Order.TimeRemaining / urgentBelowSeconds);
                    shake.x = Mathf.Sin(Time.unscaledTime * 45f) * 2.5f * strength;
                }

                c.Root.anchoredPosition = c.Position + shake;

                if (c.StampTimer > 0f)
                {
                    c.StampTimer -= dt;
                    float age = c.StampDuration - c.StampTimer;
                    c.StampRect.localScale = Vector3.one * Mathf.Lerp(1.7f, 1f, Mathf.Clamp01(age / 0.15f));

                    float fade = Mathf.Clamp01(c.StampTimer / 0.4f);
                    Color sc = c.Stamp.color;
                    sc.a = fade;
                    c.Stamp.color = sc;
                    c.StampImage.color = new Color(1f, 1f, 1f, fade);
                }
            }
        }

        private void RefreshCard(Card c)
        {
            Order order = c.Order;
            OrderTuning tuning = orderManager.Tuning;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);

            c.TimeText.text = order.IsTerminal ? "" : $"{Mathf.CeilToInt(order.TimeRemaining)}s";

            float urgency = order.IsTerminal
                ? 0f
                : 1f - Mathf.Clamp01(order.TimeRemaining / Mathf.Max(0.01f, urgentBelowSeconds));
            Color paperBase = SkinSprite(s => s.ticketPaper) != null ? Color.white : OrderPalette.Paper;
            c.Paper.color = Color.Lerp(paperBase, new Color(1f, 0.78f, 0.72f, 1f), urgency * pulse);

            // Step badges
            int current = order.CurrentStep == OrderStep.Pickup ? 0 : 1;
            bool finished = order.State == OrderState.Delivered;
            bool icons = SkinSprite(s => s.iconPickup) != null && SkinSprite(s => s.iconDropOff) != null;

            for (int i = 0; i < Steps.Length; i++)
            {
                bool done = finished || i < current;
                bool isCurrent = !finished && i == current && !order.IsTerminal;
                Color stepColour = OrderPalette.ForStep(Steps[i]);
                Sprite stepIcon = icons ? (Steps[i] == OrderStep.Pickup ? skin.iconPickup : skin.iconDropOff) : null;

                if (done)
                {
                    c.Badges[i].color = Color.Lerp(stepColour, OrderPalette.Ink, 0.25f);
                    c.BadgeRects[i].localScale = Vector3.one;
                    Sprite doneIcon = SkinSprite(s => s.iconDone);
                    if (doneIcon != null) SetBadgeIcon(c, i, doneIcon, 1f);
                    else if (icons) SetBadgeIcon(c, i, stepIcon, 1f);
                    else SetBadgeText(c, i, "OK", Color.white);
                }
                else if (isCurrent)
                {
                    c.Badges[i].color = stepColour;
                    c.BadgeRects[i].localScale = Vector3.one * (1f + 0.08f * pulse);
                    if (icons) SetBadgeIcon(c, i, stepIcon, 1f);
                    else SetBadgeText(c, i, StepLabels[i], OrderPalette.Ink);
                }
                else
                {
                    c.Badges[i].color = OrderPalette.BadgeOff;
                    c.BadgeRects[i].localScale = Vector3.one;
                    if (icons) SetBadgeIcon(c, i, stepIcon, 0.45f);
                    else SetBadgeText(c, i, StepLabels[i], new Color(OrderPalette.Ink.r, OrderPalette.Ink.g, OrderPalette.Ink.b, 0.55f));
                }
            }

            // Where to go: the line for the current step is bold.
            c.FromText.fontStyle = current == 0 && !order.IsTerminal ? FontStyle.Bold : FontStyle.Normal;
            c.ToText.fontStyle = current == 1 && !order.IsTerminal ? FontStyle.Bold : FontStyle.Normal;

            // What it pays right now
            if (order.IsTerminal && order.Payout != null)
            {
                float total = order.Payout.Total;
                c.PayText.text = total >= 0f ? $"Paid R{total:0}" : $"Lost R{-total:0}";
                c.PayText.color = total >= 0f ? new Color(0.1f, 0.5f, 0.2f, 1f) : OrderPalette.Urgent;
            }
            else if (tuning != null)
            {
                PreviewPay(order, tuning, out string label, out float pay, out Color colour);
                c.PayText.text = $"{label}: R{pay:0}";
                c.PayText.color = colour;
            }

            // Patience bar, coloured by the pay bracket it is currently in.
            float fraction = order.PatienceFraction;
            float barHeight = c.BarFill.sizeDelta.y;
            float width = BarWidth * fraction;
            bool pill = SkinSprite(s => s.barFill) != null;
            c.BarFillImage.enabled = !order.IsTerminal && (!pill || width >= barHeight);
            c.BarFill.sizeDelta = new Vector2(Mathf.Max(pill ? barHeight : 0f, width), barHeight);
            c.BarFillImage.color = tuning == null ? OrderPalette.TimerBar(fraction)
                : fraction > tuning.earlyAbovePatienceLeft ? OrderPalette.Deliver
                : fraction < tuning.lateBelowPatienceLeft ? OrderPalette.Urgent
                : OrderPalette.Warning;
        }

        /// <summary>What delivering this order right now would pay, and which bracket it is in.</summary>
        private void PreviewPay(Order order, OrderTuning tuning, out string label, out float pay, out Color colour)
        {
            float basePay = tuning.BasePayFor(order.Food, order.DesignedDistance);
            float fraction = order.PatienceFraction;

            if (fraction > tuning.earlyAbovePatienceLeft)
            {
                label = "Early";
                pay = basePay * (1f + tuning.earlyBonus);
                colour = new Color(0.1f, 0.5f, 0.2f, 1f);
            }
            else if (fraction < tuning.lateBelowPatienceLeft)
            {
                label = "Late";
                pay = basePay * (1f - tuning.lateDeduction);
                colour = OrderPalette.Urgent;
            }
            else
            {
                label = "On time";
                pay = basePay;
                colour = OrderPalette.Ink;
            }

            if (cargoCondition != null) pay *= cargoCondition.GetConditionMultiplier(order);
        }

        // ---- popups ---------------------------------------------------------------------------

        private void PushPopup(string text, Color colour)
        {
            _popupQueue.Enqueue(new KeyValuePair<string, Color>(text, colour));
        }

        private void UpdatePopup(float dt)
        {
            if (_popupTimer > 0f)
            {
                _popupTimer -= dt;
                float fadeIn = Mathf.Clamp01((popupSeconds - _popupTimer) / 0.12f);
                float fadeOut = Mathf.Clamp01(_popupTimer / 0.4f);

                Color c = _popup.color;
                c.a = Mathf.Min(fadeIn, fadeOut);
                _popup.color = c;
                _popup.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, fadeIn);

                // Shorten the wait when more popups are queued, so a burst never falls far behind.
                if (_popupQueue.Count > 1 && _popupTimer > 0.8f) _popupTimer = 0.8f;

                if (_popupTimer <= 0f) _popup.text = string.Empty;
                return;
            }

            if (_popupQueue.Count == 0) return;

            KeyValuePair<string, Color> next = _popupQueue.Dequeue();
            _popup.text = next.Key;
            _popup.color = new Color(next.Value.r, next.Value.g, next.Value.b, 0f);
            _popupTimer = popupSeconds;
        }

        // ---- building -------------------------------------------------------------------------

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("OrderCards Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var stripGO = new GameObject("Cards");
            stripGO.transform.SetParent(canvasGO.transform, false);
            _stripRoot = stripGO.AddComponent<RectTransform>();
            _stripRoot.anchorMin = _stripRoot.anchorMax = _stripRoot.pivot = new Vector2(0f, 1f);
            _stripRoot.anchoredPosition = stripOrigin;
            _stripRoot.sizeDelta = new Vector2(100f, cardSize.y);
            _stripRoot.localScale = Vector3.one * Mathf.Max(0.1f, ticketScale);

            var popupGO = new GameObject("Popup");
            popupGO.transform.SetParent(canvasGO.transform, false);
            _popup = popupGO.AddComponent<Text>();
            _popup.font = _font;
            _popup.fontSize = 42;
            _popup.fontStyle = FontStyle.Bold;
            _popup.alignment = TextAnchor.MiddleCenter;
            _popup.horizontalOverflow = HorizontalWrapMode.Overflow;
            _popup.verticalOverflow = VerticalWrapMode.Overflow;
            _popup.raycastTarget = false;

            RectTransform popupRect = _popup.rectTransform;
            popupRect.anchorMin = popupRect.anchorMax = new Vector2(0.5f, 1f);
            popupRect.pivot = new Vector2(0.5f, 1f);
            popupRect.anchoredPosition = new Vector2(0f, -240f);
            popupRect.sizeDelta = new Vector2(1200f, 70f);
            AddOutline(popupGO, 3f);
        }

        private void BuildCard(Order order)
        {
            var c = new Card { Order = order };
            float w = cardSize.x, h = cardSize.y;

            var rootGO = new GameObject($"Order card {order.Id}");
            rootGO.transform.SetParent(_stripRoot, false);
            c.Root = rootGO.AddComponent<RectTransform>();
            c.Root.anchorMin = c.Root.anchorMax = c.Root.pivot = new Vector2(0f, 1f);
            c.Root.sizeDelta = new Vector2(w, h);

            c.Paper = rootGO.AddComponent<Image>();
            c.Paper.color = OrderPalette.Paper;
            c.Paper.raycastTarget = false;
            if (skin != null) skin.ApplyFrame(c.Paper, skin.ticketPaper, h);
            var shadow = rootGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(4f, -4f);

            // Header: food colour, icon, "#1 FRIED RICE", seconds left
            RectTransform header = MakeRect(c.Root, "Header", Vector2.zero, new Vector2(w, 34f));
            Image headerImage = header.gameObject.AddComponent<Image>();
            headerImage.color = order.Food != null ? order.Food.cardColour : OrderPalette.TicketHeader(order.Id);
            headerImage.raycastTarget = false;

            float titleX = 8f;
            if (order.Food != null && order.Food.icon != null)
            {
                RectTransform iconRect = MakeRect(header, "Food icon", new Vector2(6f, -3f), new Vector2(28f, 28f));
                c.FoodIcon = iconRect.gameObject.AddComponent<Image>();
                c.FoodIcon.sprite = order.Food.icon;
                c.FoodIcon.preserveAspect = true;
                c.FoodIcon.raycastTarget = false;
                titleX = 38f;
            }

            c.Title = MakeText(header, "Title", new Vector2(titleX, -2f), new Vector2(w - titleX - 52f, 30f),
                17, Color.white, TextAnchor.MiddleLeft, true);
            c.Title.text = $"#{order.Id} {order.FoodName.ToUpper()}";
            AddOutline(c.Title.gameObject, 2f);

            c.TimeText = MakeText(header, "Time", new Vector2(w - 52f, -2f), new Vector2(44f, 30f),
                18, Color.white, TextAnchor.MiddleRight, true);
            AddOutline(c.TimeText.gameObject, 2f);

            // Step badges on the left
            bool badgeArt = SkinSprite(s => s.badgeBackground) != null;
            Vector2 badgeSize = new Vector2(40f, 40f);
            c.BadgeRects = new RectTransform[Steps.Length];
            c.Badges = new Image[Steps.Length];
            c.BadgeIcons = new Image[Steps.Length];
            c.BadgeLabels = new Text[Steps.Length];
            for (int i = 0; i < Steps.Length; i++)
            {
                RectTransform badge = MakeRect(c.Root, $"Badge {i}", Vector2.zero, badgeSize);
                badge.pivot = new Vector2(0.5f, 0.5f);
                badge.anchoredPosition = new Vector2(8f + badgeSize.x * 0.5f + i * (badgeSize.x + 6f), -42f - badgeSize.y * 0.5f);
                c.BadgeRects[i] = badge;

                c.Badges[i] = badge.gameObject.AddComponent<Image>();
                c.Badges[i].raycastTarget = false;
                if (badgeArt) HudSkin.ApplyIcon(c.Badges[i], skin.badgeBackground);

                RectTransform iconRect = MakeRect(badge, "Icon", Vector2.zero, badgeSize * 0.7f);
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                c.BadgeIcons[i] = iconRect.gameObject.AddComponent<Image>();
                c.BadgeIcons[i].preserveAspect = true;
                c.BadgeIcons[i].raycastTarget = false;
                c.BadgeIcons[i].enabled = false;

                c.BadgeLabels[i] = MakeText(badge, "Label", Vector2.zero, badgeSize, 13,
                    OrderPalette.Ink, TextAnchor.MiddleCenter, true);
                Stretch(c.BadgeLabels[i].rectTransform);
            }

            // From and To, right of the badges
            float textX = 8f + 2f * badgeSize.x + 6f + 8f;
            float textW = w - textX - 6f;
            c.FromText = MakeText(c.Root, "From", new Vector2(textX, -42f), new Vector2(textW, 20f),
                14, OrderPalette.Ink, TextAnchor.MiddleLeft, false);
            c.FromText.text = $"From: {Shorten(order.PickupName, 18)}";
            c.ToText = MakeText(c.Root, "To", new Vector2(textX, -62f), new Vector2(textW, 20f),
                14, OrderPalette.Ink, TextAnchor.MiddleLeft, false);
            c.ToText.text = $"To: {Shorten(order.DropOffName, 20)}";

            // What it pays right now
            c.PayText = MakeText(c.Root, "Pay", new Vector2(8f, -88f), new Vector2(w - 16f, 22f),
                16, OrderPalette.Ink, TextAnchor.MiddleCenter, true);

            // Patience bar with early and late marks
            float barH = SkinSprite(s => s.barFill) != null ? 18f : 16f;
            float barY = -(h - barH - 12f);
            RectTransform barBg = MakeRect(c.Root, "Patience background", new Vector2(10f, barY), new Vector2(BarWidth, barH));
            Image barBgImage = barBg.gameObject.AddComponent<Image>();
            barBgImage.color = new Color(0.2f, 0.12f, 0.08f, 0.85f);
            barBgImage.raycastTarget = false;
            if (skin != null && HudSkin.ApplyPill(barBgImage, skin.barBackground, barH)) barBgImage.color = Color.white;

            c.BarFill = MakeRect(c.Root, "Patience fill", new Vector2(10f, barY), new Vector2(BarWidth, barH));
            c.BarFillImage = c.BarFill.gameObject.AddComponent<Image>();
            c.BarFillImage.raycastTarget = false;
            if (skin != null) HudSkin.ApplyPill(c.BarFillImage, skin.barFill, barH);

            OrderTuning tuning = orderManager.Tuning;
            if (tuning != null)
            {
                AddBarMark(c.Root, "Early mark", 10f + BarWidth * tuning.earlyAbovePatienceLeft, barY, barH);
                AddBarMark(c.Root, "Late mark", 10f + BarWidth * tuning.lateBelowPatienceLeft, barY, barH);
            }

            // Stamps, last so they draw on top
            c.StampRect = MakeRect(c.Root, "Stamp", Vector2.zero, new Vector2(w, 60f));
            c.StampRect.pivot = new Vector2(0.5f, 0.5f);
            c.StampRect.anchoredPosition = new Vector2(w * 0.5f, -h * 0.5f);
            c.StampRect.localRotation = Quaternion.Euler(0f, 0f, 12f);
            c.Stamp = c.StampRect.gameObject.AddComponent<Text>();
            c.Stamp.font = _font;
            c.Stamp.fontSize = 36;
            c.Stamp.fontStyle = FontStyle.Bold;
            c.Stamp.alignment = TextAnchor.MiddleCenter;
            c.Stamp.horizontalOverflow = HorizontalWrapMode.Overflow;
            c.Stamp.verticalOverflow = VerticalWrapMode.Overflow;
            c.Stamp.raycastTarget = false;
            AddOutline(c.Stamp.gameObject, 3f);

            RectTransform stampImageRect = MakeRect(c.Root, "Stamp image", Vector2.zero, new Vector2(w - 8f, h * 0.6f));
            stampImageRect.pivot = new Vector2(0.5f, 0.5f);
            stampImageRect.anchoredPosition = new Vector2(w * 0.5f, -h * 0.5f);
            c.StampImage = stampImageRect.gameObject.AddComponent<Image>();
            c.StampImage.preserveAspect = true;
            c.StampImage.raycastTarget = false;
            c.StampImage.enabled = false;

            // Drops in from above its slot
            c.Position = new Vector2(_cards.Count * (w + ticketGap), 240f);
            c.Root.anchoredPosition = c.Position;

            _cards.Add(c);
            _byOrder[order] = c;
            RefreshCard(c);
        }

        private void AddBarMark(RectTransform parent, string name, float x, float barY, float barH)
        {
            RectTransform mark = MakeRect(parent, name, new Vector2(x - 1f, barY + 3f), new Vector2(2f, barH + 6f));
            Image image = mark.gameObject.AddComponent<Image>();
            image.color = OrderPalette.Ink;
            image.raycastTarget = false;
        }

        private void ShowStamp(Card c, string text, Color colour, Sprite sprite)
        {
            bool useSprite = sprite != null;

            c.StampImage.enabled = useSprite;
            c.StampImage.sprite = sprite;
            c.StampImage.color = Color.white;
            c.Stamp.text = useSprite ? string.Empty : text;
            c.Stamp.color = colour;

            // The update loop pops and fades whatever StampRect points at.
            c.StampRect = useSprite ? c.StampImage.rectTransform : c.Stamp.rectTransform;
            c.StampDuration = resolvedLingerSeconds + 0.6f;
            c.StampTimer = c.StampDuration;
        }

        // ---- helpers --------------------------------------------------------------------------

        private Sprite SkinSprite(System.Func<HudSkin, Sprite> pick) => skin != null ? pick(skin) : null;

        private static void SetBadgeIcon(Card c, int i, Sprite icon, float alpha)
        {
            c.BadgeIcons[i].enabled = true;
            c.BadgeIcons[i].sprite = icon;
            c.BadgeIcons[i].color = new Color(1f, 1f, 1f, alpha);
            c.BadgeLabels[i].text = string.Empty;
        }

        private static void SetBadgeText(Card c, int i, string text, Color colour)
        {
            c.BadgeIcons[i].enabled = false;
            c.BadgeLabels[i].text = text;
            c.BadgeLabels[i].color = colour;
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
            return text.Substring(0, max - 1) + ".";
        }

        private RectTransform MakeRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Text MakeText(Transform parent, string name, Vector2 position, Vector2 size,
            int fontSize, Color colour, TextAnchor alignment, bool bold)
        {
            RectTransform rect = MakeRect(parent, name, position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = colour;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddOutline(GameObject go, float distance)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(distance, -distance);
        }
    }
}