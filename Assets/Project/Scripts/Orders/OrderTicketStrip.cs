using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// The order list, styled like a food delivery app on a phone: a phone shaped panel down the
    /// left of the screen, one notification per order, oldest at the top and newest at the
    /// bottom. New orders slide in like notifications; finished ones get a stamp and slide away,
    /// and the rest move up. The phone has room for four, which is why there are never more than
    /// four orders at once.
    ///
    /// Each notification shows:
    ///   a coloured tag with the order number, in the order's own colour, which is also the
    ///   colour of its beacons, its arrow and its GPS line;
    ///   the food and the seconds left;
    ///   a chip per ingredient (filled in once collected), a COOK chip (counts down, then READY,
    ///   flashing red before it burns) and a DROP chip;
    ///   what it pays right now, the next thing to do, and its patience bar.
    ///
    /// The selected order (see OrderFocus) glows in its colour, so the player always knows which
    /// order is the priority. Built in code, nothing to wire.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderTicketStrip : MonoBehaviour
    {
        [Header("Wiring (all found automatically if left empty)")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private CargoSystem cargoSystem;
        [SerializeField] private CargoCondition cargoCondition;
        [SerializeField] private OrderFocus focus;

        [Header("Art (optional)")]
        [Tooltip("Only the stamps and the drop off icon are used from this.")]
        [SerializeField] private HudSkin skin;

        [Header("Phone")]
        [Tooltip("Draw the phone around the notifications. Off shows the notifications on their own.")]
        [SerializeField] private bool showPhoneFrame = true;
        [Tooltip("Shown at the top of the phone screen, like the app's name.")]
        [SerializeField] private string appName = "Zoom Zoom Eats";
        [Tooltip("Offset of the phone from the screen's top left corner, in reference pixels (1920x1080).")]
        [SerializeField] private Vector2 phoneOrigin = new Vector2(24f, -24f);
        [Tooltip("Scales the whole phone.")]
        [SerializeField] private float phoneScale = 1f;

        [Header("Notifications")]
        [SerializeField] private Vector2 notificationSize = new Vector2(372f, 104f);
        [SerializeField] private float notificationGap = 8f;

        [Header("Behaviour")]
        [Tooltip("Seconds left below which a notification shakes and its time turns red.")]
        [SerializeField] private float urgentBelowSeconds = 8f;
        [Tooltip("Seconds before burning at which the COOK chip flashes red.")]
        [SerializeField] private float burnWarningSeconds = 4f;
        [Tooltip("Seconds a stamp stays on a notification before it slides away.")]
        [SerializeField] private float resolvedLingerSeconds = 1.1f;
        [Tooltip("Seconds each message stays on screen.")]
        [SerializeField] private float popupSeconds = 1.8f;
        [Tooltip("Show a short message for every ingredient collected.")]
        [SerializeField] private bool popupOnIngredient = true;
        [Tooltip("Seconds before the same warning (for example \"car full\") can show again.")]
        [SerializeField] private float repeatWarningSeconds = 3f;

        private const float Padding = 14f;
        private const float StatusBarHeight = 40f;
        private const float HomeBarHeight = 22f;

        private enum ChipKind { Ingredient, Cook, Drop }

        private class Chip
        {
            public ChipKind Kind;
            public IngredientStop Stop;
            public RectTransform Rect;
            public Image Background;
            public Image Icon;
            public Text Label;
        }

        private class Card
        {
            public Order Order;
            public RectTransform Root;
            public Image Glow;
            public Image Background;
            public Text TimeText;
            public RectTransform GpsTag;
            public List<Chip> Chips = new List<Chip>();
            public Text StepText;
            public Text PayText;
            public RectTransform BarFill;
            public Image BarFillImage;
            public Text Stamp;
            public Image StampImage;
            public RectTransform StampRect;
            public CanvasGroup Group;

            public Vector2 Position;
            public bool Resolving;
            public float ResolveTimer;
            public float StampTimer;
            public float StampDuration;
        }

        private readonly List<Card> _cards = new List<Card>();
        private readonly Dictionary<Order, Card> _byOrder = new Dictionary<Order, Card>();
        private readonly Queue<KeyValuePair<string, Color>> _popupQueue = new Queue<KeyValuePair<string, Color>>();
        private readonly Dictionary<string, float> _lastWarning = new Dictionary<string, float>();

        private RectTransform _listRoot;
        private Text _statusCount;
        private Text _emptyText;
        private Text _popup;
        private float _popupTimer;
        private Font _font;

        private static readonly Color CardColour = new Color(0.97f, 0.96f, 0.93f, 1f);
        private static readonly Color UrgentCard = new Color(1f, 0.80f, 0.76f, 1f);
        private static readonly Color GoodGreen = new Color(0.1f, 0.5f, 0.2f, 1f);
        private static readonly Color MutedInk = new Color(0.25f, 0.15f, 0.08f, 0.6f);

        private float W => notificationSize.x;
        private float H => notificationSize.y;
        private float BarWidth => W - 36f;

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
            BuildCanvas();

            orderManager.OrderPaid += HandleOrderPaid;
            ProcessingBay.AnyBayEvent += HandleBayEvent;
            if (cargoSystem != null)
            {
                cargoSystem.CapacityChanged += HandleCapacityChanged;
                cargoSystem.IngredientCollected += HandleIngredientCollected;
                cargoSystem.Refused += HandleRefused;
            }
        }

        private void Start()
        {
            if (focus == null) focus = OrderFocus.Ensure(orderManager);
            if (focus != null) focus.GpsToggled += HandleGpsToggled;
        }

        private void OnDestroy()
        {
            if (orderManager != null) orderManager.OrderPaid -= HandleOrderPaid;
            ProcessingBay.AnyBayEvent -= HandleBayEvent;
            if (cargoSystem != null)
            {
                cargoSystem.CapacityChanged -= HandleCapacityChanged;
                cargoSystem.IngredientCollected -= HandleIngredientCollected;
                cargoSystem.Refused -= HandleRefused;
            }
            if (focus != null) focus.GpsToggled -= HandleGpsToggled;
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
                    case DeliveryTiming.Burned:
                        ShowStamp(card, "BURNED", OrderPalette.Urgent, null);
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
                case DeliveryTiming.Burned:
                    PushPopup($"{order.FoodName} #{order.Id} burned, minus R{-payout.Penalty:0}", OrderPalette.Urgent);
                    break;
                default:
                    PushPopup($"Order #{order.Id} cancelled, minus R{-payout.Penalty:0}", OrderPalette.Urgent);
                    break;
            }
        }

        private void HandleIngredientCollected(Order order, IngredientStop stop)
        {
            if (!popupOnIngredient) return;

            string text = order.AllIngredientsCollected && order.NeedsCooking
                ? $"Got {stop.Name}. #{order.Id} {order.FoodName} ready to cook!"
                : $"Got {stop.Name} for #{order.Id}";
            PushPopup(text, OrderPalette.OrderColour(order));
        }

        private void HandleBayEvent(ProcessingBay bay, Order order, string what)
        {
            switch (what)
            {
                case "cooking":
                    PushPopup($"#{order.Id} {order.FoodName} cooking, {order.CookSeconds:0}s", OrderPalette.Cook);
                    break;
                case "ready":
                    PushPopup($"#{order.Id} {order.FoodName} ready at {bay.BayName}!", OrderPalette.Deliver);
                    break;
            }
        }

        private void HandleRefused(string reason)
        {
            float now = Time.unscaledTime;
            if (_lastWarning.TryGetValue(reason, out float last) && now - last < repeatWarningSeconds) return;
            _lastWarning[reason] = now;
            PushPopup(reason, OrderPalette.Warning);
        }

        private void HandleCapacityChanged(int capacity)
        {
            PushPopup($"Cargo upgraded: {capacity} slots", OrderPalette.Money);
        }

        private void HandleGpsToggled(bool on)
        {
            PushPopup(on ? "GPS on" : "GPS off", OrderPalette.Neutral);
        }

        // ---- per frame ------------------------------------------------------------------------

        private void Update()
        {
            SyncCards();
            UpdateCards(Time.unscaledDeltaTime);
            UpdateStatusBar();
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

        private void UpdateStatusBar()
        {
            int live = 0;
            foreach (Order o in orderManager.ActiveOrders) if (!o.IsTerminal) live++;

            int max = orderManager.Tuning != null
                ? Mathf.Min(orderManager.Tuning.maxActiveOrders, OrderTuning.MaxOrders)
                : OrderTuning.MaxOrders;

            if (_statusCount != null)
            {
                string gps = focus != null && focus.GpsOn ? "GPS  " : "";
                _statusCount.text = $"{gps}{live}/{max}";
            }

            if (_emptyText != null) _emptyText.enabled = _cards.Count == 0;
        }

        private void UpdateCards(float dt)
        {
            float follow = 1f - Mathf.Exp(-12f * dt);
            Order selected = focus != null ? focus.Selected : null;

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

                RefreshCard(c, c.Order == selected && !c.Resolving);
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                Card c = _cards[i];

                // Oldest at the top, newest at the bottom, like a notification feed.
                var target = new Vector2(0f, -i * (H + notificationGap));
                bool leaving = c.Resolving && c.ResolveTimer <= 0f;
                if (leaving) target.x = -(W + 60f); // slide out to the left

                c.Position = Vector2.Lerp(c.Position, target, follow);
                c.Group.alpha = leaving ? Mathf.Clamp01(1f + c.ResolveTimer * 2f) : 1f;

                Vector2 shake = Vector2.zero;
                if (!c.Resolving && !c.Order.IsTerminal && c.Order.SecondsUntilLost < urgentBelowSeconds)
                {
                    float strength = 1f - Mathf.Clamp01(c.Order.SecondsUntilLost / urgentBelowSeconds);
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

        private void RefreshCard(Card c, bool selected)
        {
            Order order = c.Order;
            OrderTuning tuning = orderManager.Tuning;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            float fastPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);
            Color orderColour = OrderPalette.OrderColour(order);

            // Selection glow, in the order's colour.
            c.Glow.enabled = selected;
            if (selected)
            {
                Color glow = orderColour;
                glow.a = Mathf.Lerp(0.65f, 1f, pulse);
                c.Glow.color = glow;
            }

            c.GpsTag.gameObject.SetActive(selected && focus != null && focus.GpsOn);

            // Time left, red when urgent.
            bool urgent = !order.IsTerminal && order.SecondsUntilLost < urgentBelowSeconds;
            c.TimeText.text = order.IsTerminal ? "" : $"{Mathf.CeilToInt(order.TimeRemaining)}s";
            c.TimeText.color = urgent ? OrderPalette.Urgent : OrderPalette.Ink;

            float urgency = urgent ? 1f - Mathf.Clamp01(order.SecondsUntilLost / Mathf.Max(0.01f, urgentBelowSeconds)) : 0f;
            c.Background.color = Color.Lerp(CardColour, UrgentCard, urgency * pulse);

            foreach (Chip chip in c.Chips) RefreshChip(chip, order, pulse, fastPulse);

            RefreshStepText(c, order);

            // What it pays right now
            if (order.IsTerminal && order.Payout != null)
            {
                float total = order.Payout.Total;
                c.PayText.text = total >= 0f ? $"Paid R{total:0}" : $"Lost R{-total:0}";
                c.PayText.color = total >= 0f ? GoodGreen : OrderPalette.Urgent;
            }
            else if (tuning != null)
            {
                PreviewPay(order, tuning, out string label, out float pay, out Color colour);
                c.PayText.text = $"{label} R{pay:0}";
                c.PayText.color = colour;
            }

            // Patience bar, coloured by the pay bracket it is currently in.
            float fraction = order.PatienceFraction;
            c.BarFillImage.enabled = !order.IsTerminal;
            c.BarFill.sizeDelta = new Vector2(BarWidth * fraction, c.BarFill.sizeDelta.y);
            c.BarFillImage.color = tuning == null ? OrderPalette.TimerBar(fraction)
                : fraction > tuning.earlyAbovePatienceLeft ? OrderPalette.Deliver
                : fraction < tuning.lateBelowPatienceLeft ? OrderPalette.Urgent
                : OrderPalette.Warning;
        }

        private void RefreshChip(Chip chip, Order order, float pulse, float fastPulse)
        {
            bool live = !order.IsTerminal;
            OrderStep step = order.CurrentStep;
            Color off = OrderPalette.BadgeOff;
            float scale = 1f;

            switch (chip.Kind)
            {
                case ChipKind.Ingredient:
                    {
                        Color ingredientColour = chip.Stop.Ingredient != null ? chip.Stop.Ingredient.colour : OrderPalette.Pickup;
                        if (chip.Stop.Collected)
                        {
                            chip.Background.color = ingredientColour;
                            SetChip(chip, chip.Stop.ChipLabel, Color.white, 1f);
                        }
                        else
                        {
                            bool needed = live && step == OrderStep.Gather;
                            chip.Background.color = needed ? Color.Lerp(off, OrderPalette.Pickup, 0.35f * pulse) : off;
                            SetChip(chip, chip.Stop.ChipLabel, needed ? OrderPalette.Ink : MutedInk, 0.5f);
                            if (needed) scale = 1f + 0.06f * pulse;
                        }
                        break;
                    }

                case ChipKind.Cook:
                    {
                        if (order.State == OrderState.Cooking)
                        {
                            chip.Background.color = OrderPalette.Cook;
                            SetChip(chip, $"{Mathf.CeilToInt(order.BayTimeRemaining)}s", Color.white, 1f);
                        }
                        else if (order.State == OrderState.Ready)
                        {
                            bool burning = order.BayTimeRemaining <= burnWarningSeconds;
                            chip.Background.color = burning
                                ? Color.Lerp(OrderPalette.Deliver, OrderPalette.Urgent, fastPulse)
                                : OrderPalette.Deliver;
                            SetChip(chip, burning ? $"{Mathf.CeilToInt(order.BayTimeRemaining)}s" : "READY", Color.white, 1f);
                            scale = 1f + 0.1f * pulse;
                        }
                        else if (order.State == OrderState.Carried || order.State == OrderState.Delivered)
                        {
                            chip.Background.color = Color.Lerp(OrderPalette.Cook, OrderPalette.Ink, 0.25f);
                            SetChip(chip, "COOK", Color.white, 1f);
                        }
                        else if (live && step == OrderStep.Cook)
                        {
                            chip.Background.color = OrderPalette.Cook;
                            SetChip(chip, "COOK", OrderPalette.Ink, 1f);
                            scale = 1f + 0.08f * pulse;
                        }
                        else
                        {
                            chip.Background.color = off;
                            SetChip(chip, "COOK", MutedInk, 0.5f);
                        }
                        break;
                    }

                default: // Drop
                    {
                        if (order.State == OrderState.Delivered)
                        {
                            chip.Background.color = Color.Lerp(OrderPalette.Deliver, OrderPalette.Ink, 0.25f);
                            SetDropChip(chip, true, Color.white, 1f);
                        }
                        else if (live && step == OrderStep.Deliver)
                        {
                            chip.Background.color = OrderPalette.Deliver;
                            SetDropChip(chip, false, OrderPalette.Ink, 1f);
                            scale = 1f + 0.08f * pulse;
                        }
                        else
                        {
                            chip.Background.color = off;
                            SetDropChip(chip, false, MutedInk, 0.45f);
                        }
                        break;
                    }
            }

            chip.Rect.localScale = Vector3.one * scale;
        }

        private void RefreshStepText(Card c, Order order)
        {
            if (order.IsTerminal)
            {
                c.StepText.text = string.Empty;
                return;
            }

            string text;
            Color colour = OrderPalette.Ink;

            switch (order.State)
            {
                case OrderState.Cooking:
                    text = $"Cooking at {order.BayName}";
                    colour = Color.Lerp(OrderPalette.Cook, OrderPalette.Ink, 0.3f);
                    break;

                case OrderState.Ready:
                    bool burning = order.BayTimeRemaining <= burnWarningSeconds;
                    text = burning ? $"Burning! Get it from {order.BayName}" : $"Ready! Collect at {order.BayName}";
                    colour = burning ? OrderPalette.Urgent : GoodGreen;
                    break;

                case OrderState.Carried:
                    text = $"Deliver to {order.DropOffName}";
                    colour = GoodGreen;
                    break;

                default:
                    if (order.CurrentStep == OrderStep.Cook)
                    {
                        text = order.HasBay ? $"Take to {order.BayName}" : "Take to a bay";
                        colour = Color.Lerp(OrderPalette.Cook, OrderPalette.Ink, 0.3f);
                    }
                    else
                    {
                        IngredientStop next = order.NextStop;
                        int left = order.Stops.Count - order.IngredientsCollected;
                        text = next == null ? "" : $"Get {next.Name} at {next.PlaceName}";
                        if (left > 1) text += $" (+{left - 1} more)";
                        colour = Color.Lerp(OrderPalette.Pickup, OrderPalette.Ink, 0.45f);
                    }
                    break;
            }

            c.StepText.text = Shorten(text, 44);
            c.StepText.color = colour;
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
                colour = GoodGreen;
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
                if (_popupQueue.Count > 0 && _popupTimer > 0.8f) _popupTimer = 0.8f;

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
            var canvasGO = new GameObject("Order Phone Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            int slots = OrderTuning.MaxOrders;
            float listHeight = slots * H + (slots - 1) * notificationGap;
            float phoneW = W + Padding * 2f;
            float phoneH = Padding + StatusBarHeight + listHeight + HomeBarHeight + Padding;

            RectTransform phone = MakeRect(canvasGO.transform, "Phone", phoneOrigin, new Vector2(phoneW, phoneH));
            phone.localScale = Vector3.one * Mathf.Max(0.1f, phoneScale);

            if (showPhoneFrame)
            {
                // Body (the bezel), then the screen inset inside it.
                Image body = phone.gameObject.AddComponent<Image>();
                body.raycastTarget = false;
                ApplyRounded(body, 30f);
                body.color = new Color(0.05f, 0.05f, 0.07f, 0.94f);

                RectTransform screen = MakeRect(phone, "Screen", new Vector2(6f, -6f), new Vector2(phoneW - 12f, phoneH - 12f));
                Image screenImage = screen.gameObject.AddComponent<Image>();
                screenImage.raycastTarget = false;
                ApplyRounded(screenImage, 24f);
                screenImage.color = new Color(0.15f, 0.16f, 0.20f, 0.92f);

                // Notch
                RectTransform notch = MakeRect(phone, "Notch", new Vector2(phoneW * 0.5f - 45f, -6f), new Vector2(90f, 16f));
                Image notchImage = notch.gameObject.AddComponent<Image>();
                notchImage.raycastTarget = false;
                ApplyRounded(notchImage, 8f);
                notchImage.color = new Color(0.05f, 0.05f, 0.07f, 1f);

                // Home indicator
                RectTransform home = MakeRect(phone, "Home bar", new Vector2(phoneW * 0.5f - 60f, -(phoneH - Padding - 10f)), new Vector2(120f, 5f));
                Image homeImage = home.gameObject.AddComponent<Image>();
                homeImage.raycastTarget = false;
                ApplyRounded(homeImage, 2.5f);
                homeImage.color = new Color(1f, 1f, 1f, 0.55f);

                // Status bar: app name on the left, order count (and GPS) on the right.
                Text title = MakeText(phone, "App name", new Vector2(Padding + 6f, -Padding - 4f),
                    new Vector2(W * 0.65f, 28f), 18, Color.white, TextAnchor.MiddleLeft, true);
                title.text = appName;

                _statusCount = MakeText(phone, "Order count", new Vector2(Padding + W * 0.55f, -Padding - 4f),
                    new Vector2(W * 0.45f - 6f, 28f), 16, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleRight, true);
            }

            _listRoot = MakeRect(phone, "Notifications", new Vector2(Padding, -(Padding + StatusBarHeight)),
                new Vector2(W, listHeight));
            _listRoot.gameObject.AddComponent<RectMask2D>(); // finished orders slide out of view

            _emptyText = MakeText(_listRoot, "No orders", new Vector2(0f, -listHeight * 0.4f), new Vector2(W, 30f),
                16, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter, false);
            _emptyText.text = "No orders right now";

            var popupGO = new GameObject("Popup");
            popupGO.transform.SetParent(canvasGO.transform, false);
            _popup = popupGO.AddComponent<Text>();
            _popup.font = _font;
            _popup.fontSize = 38;
            _popup.fontStyle = FontStyle.Bold;
            _popup.alignment = TextAnchor.MiddleCenter;
            _popup.horizontalOverflow = HorizontalWrapMode.Overflow;
            _popup.verticalOverflow = VerticalWrapMode.Overflow;
            _popup.raycastTarget = false;

            RectTransform popupRect = _popup.rectTransform;
            popupRect.anchorMin = popupRect.anchorMax = new Vector2(0.5f, 1f);
            popupRect.pivot = new Vector2(0.5f, 1f);
            popupRect.anchoredPosition = new Vector2(120f, -40f);
            popupRect.sizeDelta = new Vector2(1200f, 60f);
            AddOutline(popupGO, 3f);
        }

        private void BuildCard(Order order)
        {
            var c = new Card { Order = order };
            Color orderColour = OrderPalette.OrderColour(order);

            var rootGO = new GameObject($"Order {order.Id}");
            rootGO.transform.SetParent(_listRoot, false);
            c.Root = rootGO.AddComponent<RectTransform>();
            c.Root.anchorMin = c.Root.anchorMax = c.Root.pivot = new Vector2(0f, 1f);
            c.Root.sizeDelta = new Vector2(W, H);
            c.Group = rootGO.AddComponent<CanvasGroup>();
            c.Group.interactable = false;
            c.Group.blocksRaycasts = false;

            // Selection glow, drawn behind the card and slightly larger than it.
            RectTransform glow = MakeRect(c.Root, "Selected glow", new Vector2(-3f, 3f), new Vector2(W + 6f, H + 6f));
            c.Glow = glow.gameObject.AddComponent<Image>();
            c.Glow.raycastTarget = false;
            ApplyRounded(c.Glow, 17f);
            c.Glow.enabled = false;

            RectTransform bg = MakeRect(c.Root, "Card", Vector2.zero, new Vector2(W, H));
            c.Background = bg.gameObject.AddComponent<Image>();
            c.Background.raycastTarget = false;
            ApplyRounded(c.Background, 14f);
            c.Background.color = CardColour;

            // Colour strip down the left edge.
            RectTransform strip = MakeRect(c.Root, "Colour strip", new Vector2(6f, -10f), new Vector2(6f, H - 20f));
            Image stripImage = strip.gameObject.AddComponent<Image>();
            stripImage.raycastTarget = false;
            ApplyRounded(stripImage, 3f);
            stripImage.color = orderColour;

            // Order number tag in the order's colour, then the food.
            RectTransform tag = MakeRect(c.Root, "Order tag", new Vector2(18f, -8f), new Vector2(42f, 24f));
            Image tagImage = tag.gameObject.AddComponent<Image>();
            tagImage.raycastTarget = false;
            ApplyRounded(tagImage, 7f);
            tagImage.color = orderColour;
            Text tagText = MakeText(tag, "Number", Vector2.zero, new Vector2(42f, 24f), 16, OrderPalette.Ink, TextAnchor.MiddleCenter, true);
            Stretch(tagText.rectTransform);
            tagText.text = $"#{order.Id}";

            float titleX = 66f;
            if (order.Food != null && order.Food.icon != null)
            {
                RectTransform iconRect = MakeRect(c.Root, "Food icon", new Vector2(66f, -6f), new Vector2(28f, 28f));
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = order.Food.icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                titleX = 98f;
            }

            Text title = MakeText(c.Root, "Food", new Vector2(titleX, -8f), new Vector2(W - titleX - 120f, 24f),
                17, OrderPalette.Ink, TextAnchor.MiddleLeft, true);
            title.text = Shorten(order.FoodName.ToUpper(), 16);

            c.TimeText = MakeText(c.Root, "Time", new Vector2(W - 62f, -8f), new Vector2(50f, 24f),
                18, OrderPalette.Ink, TextAnchor.MiddleRight, true);

            // "GPS" tag on the selected order while the route line is on.
            c.GpsTag = MakeRect(c.Root, "GPS tag", new Vector2(W - 112f, -10f), new Vector2(44f, 20f));
            Image gpsImage = c.GpsTag.gameObject.AddComponent<Image>();
            gpsImage.raycastTarget = false;
            ApplyRounded(gpsImage, 6f);
            gpsImage.color = OrderPalette.Ink;
            Text gpsText = MakeText(c.GpsTag, "Label", Vector2.zero, new Vector2(44f, 20f), 12, orderColour, TextAnchor.MiddleCenter, true);
            Stretch(gpsText.rectTransform);
            gpsText.text = "GPS";
            c.GpsTag.gameObject.SetActive(false);

            // Chip row: one per ingredient, then COOK (if this scene has bays), then DROP.
            var kinds = new List<KeyValuePair<ChipKind, IngredientStop>>();
            foreach (IngredientStop stop in order.Stops) kinds.Add(new KeyValuePair<ChipKind, IngredientStop>(ChipKind.Ingredient, stop));
            if (order.NeedsCooking) kinds.Add(new KeyValuePair<ChipKind, IngredientStop>(ChipKind.Cook, null));
            kinds.Add(new KeyValuePair<ChipKind, IngredientStop>(ChipKind.Drop, null));

            const float chipGap = 3f, chipH = 26f, chipTop = -38f, chipW = 40f;
            for (int i = 0; i < kinds.Count; i++)
            {
                var chip = new Chip { Kind = kinds[i].Key, Stop = kinds[i].Value };
                chip.Rect = MakeRect(c.Root, $"Chip {i}", Vector2.zero, new Vector2(chipW, chipH));
                chip.Rect.pivot = new Vector2(0.5f, 0.5f);
                chip.Rect.anchoredPosition = new Vector2(18f + chipW * 0.5f + i * (chipW + chipGap), chipTop - chipH * 0.5f);

                chip.Background = chip.Rect.gameObject.AddComponent<Image>();
                chip.Background.raycastTarget = false;
                ApplyRounded(chip.Background, 7f);

                RectTransform iconRect = MakeRect(chip.Rect, "Icon", Vector2.zero, new Vector2(chipH * 0.8f, chipH * 0.8f));
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                chip.Icon = iconRect.gameObject.AddComponent<Image>();
                chip.Icon.preserveAspect = true;
                chip.Icon.raycastTarget = false;
                chip.Icon.enabled = false;

                chip.Label = MakeText(chip.Rect, "Label", Vector2.zero, new Vector2(chipW, chipH), 12,
                    OrderPalette.Ink, TextAnchor.MiddleCenter, true);
                Stretch(chip.Label.rectTransform);
                chip.Label.horizontalOverflow = HorizontalWrapMode.Wrap;
                chip.Label.verticalOverflow = VerticalWrapMode.Truncate; // best fit needs both bounded
                chip.Label.resizeTextForBestFit = true;
                chip.Label.resizeTextMinSize = 8;
                chip.Label.resizeTextMaxSize = 12;

                c.Chips.Add(chip);
            }

            // Pay, to the right of the chips.
            c.PayText = MakeText(c.Root, "Pay", new Vector2(W - 132f, chipTop), new Vector2(120f, chipH),
                15, OrderPalette.Ink, TextAnchor.MiddleRight, true);

            // Next step
            c.StepText = MakeText(c.Root, "Next step", new Vector2(18f, -68f), new Vector2(W - 30f, 20f),
                14, OrderPalette.Ink, TextAnchor.MiddleLeft, true);

            // Patience bar with early and late marks
            const float barH = 7f;
            float barY = -(H - barH - 9f);
            RectTransform barBg = MakeRect(c.Root, "Patience background", new Vector2(18f, barY), new Vector2(BarWidth, barH));
            Image barBgImage = barBg.gameObject.AddComponent<Image>();
            barBgImage.color = new Color(0.2f, 0.12f, 0.08f, 0.25f);
            barBgImage.raycastTarget = false;

            c.BarFill = MakeRect(c.Root, "Patience fill", new Vector2(18f, barY), new Vector2(BarWidth, barH));
            c.BarFillImage = c.BarFill.gameObject.AddComponent<Image>();
            c.BarFillImage.raycastTarget = false;

            OrderTuning tuning = orderManager.Tuning;
            if (tuning != null)
            {
                AddBarMark(c.Root, "Early mark", 18f + BarWidth * tuning.earlyAbovePatienceLeft, barY, barH);
                AddBarMark(c.Root, "Late mark", 18f + BarWidth * tuning.lateBelowPatienceLeft, barY, barH);
            }

            // Stamps, last so they draw on top
            c.StampRect = MakeRect(c.Root, "Stamp", Vector2.zero, new Vector2(W, 50f));
            c.StampRect.pivot = new Vector2(0.5f, 0.5f);
            c.StampRect.anchoredPosition = new Vector2(W * 0.5f, -H * 0.5f);
            c.StampRect.localRotation = Quaternion.Euler(0f, 0f, 8f);
            c.Stamp = c.StampRect.gameObject.AddComponent<Text>();
            c.Stamp.font = _font;
            c.Stamp.fontSize = 34;
            c.Stamp.fontStyle = FontStyle.Bold;
            c.Stamp.alignment = TextAnchor.MiddleCenter;
            c.Stamp.horizontalOverflow = HorizontalWrapMode.Overflow;
            c.Stamp.verticalOverflow = VerticalWrapMode.Overflow;
            c.Stamp.raycastTarget = false;
            AddOutline(c.Stamp.gameObject, 3f);

            RectTransform stampImageRect = MakeRect(c.Root, "Stamp image", Vector2.zero, new Vector2(W * 0.6f, H * 0.8f));
            stampImageRect.pivot = new Vector2(0.5f, 0.5f);
            stampImageRect.anchoredPosition = new Vector2(W * 0.5f, -H * 0.5f);
            c.StampImage = stampImageRect.gameObject.AddComponent<Image>();
            c.StampImage.preserveAspect = true;
            c.StampImage.raycastTarget = false;
            c.StampImage.enabled = false;

            // Slides in from the left, in its slot at the bottom of the feed.
            c.Position = new Vector2(-(W + 60f), -_cards.Count * (H + notificationGap));
            c.Root.anchoredPosition = c.Position;

            _cards.Add(c);
            _byOrder[order] = c;
            RefreshCard(c, false);
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

        // ---- rounded corners ------------------------------------------------------------------

        private const int RoundTexSize = 64;
        private const float RoundTexRadius = 24f;
        private static Sprite _rounded;

        /// <summary>A white rounded rectangle, generated once in code and 9 sliced, so any panel
        /// can have rounded corners of any radius with no art asset.</summary>
        private static Sprite RoundedSprite()
        {
            if (_rounded != null) return _rounded;

            var tex = new Texture2D(RoundTexSize, RoundTexSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[RoundTexSize * RoundTexSize];
            float r = RoundTexRadius;
            for (int y = 0; y < RoundTexSize; y++)
            {
                for (int x = 0; x < RoundTexSize; x++)
                {
                    // Distance from the nearest corner centre, only inside the corner squares.
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, r, RoundTexSize - r);
                    float cy = Mathf.Clamp(py, r, RoundTexSize - r);
                    float dx = px - cx, dy = py - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(r - d + 0.5f);
                    pixels[y * RoundTexSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            _rounded = Sprite.Create(tex, new Rect(0, 0, RoundTexSize, RoundTexSize), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            _rounded.name = "Rounded rect (generated)";
            return _rounded;
        }

        private static void ApplyRounded(Image image, float radius)
        {
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = RoundTexRadius / Mathf.Max(0.5f, radius);
        }

        // ---- helpers --------------------------------------------------------------------------

        private Sprite SkinSprite(System.Func<HudSkin, Sprite> pick) => skin != null ? pick(skin) : null;

        /// <summary>An ingredient chip shows the ingredient's icon if it has one, else its label.</summary>
        private static void SetChip(Chip chip, string label, Color textColour, float iconAlpha)
        {
            Sprite icon = chip.Kind == ChipKind.Ingredient && chip.Stop.Ingredient != null ? chip.Stop.Ingredient.icon : null;
            ApplyChip(chip, icon, label, textColour, iconAlpha);
        }

        /// <summary>The DROP chip uses the skin's drop off icon (or done icon) when there is one.</summary>
        private void SetDropChip(Chip chip, bool done, Color textColour, float iconAlpha)
        {
            Sprite icon = done ? SkinSprite(s => s.iconDone) : null;
            if (icon == null) icon = SkinSprite(s => s.iconDropOff);
            ApplyChip(chip, icon, "DROP", textColour, iconAlpha);
        }

        private static void ApplyChip(Chip chip, Sprite icon, string label, Color textColour, float iconAlpha)
        {
            if (icon != null)
            {
                chip.Icon.enabled = true;
                chip.Icon.sprite = icon;
                chip.Icon.color = new Color(1f, 1f, 1f, iconAlpha);
                chip.Label.text = string.Empty;
            }
            else
            {
                chip.Icon.enabled = false;
                chip.Label.text = label;
                chip.Label.color = textColour;
            }
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