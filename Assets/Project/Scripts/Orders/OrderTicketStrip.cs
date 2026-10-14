using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// The order ticket strip. The project plan's Concept section says orders appear on screen
    /// "with a countdown attached exactly like an Overcooked ticket", and this is that: one paper
    /// ticket per active order along the top left of the screen, oldest on the left. Each ticket
    /// shows a coloured header with the order number and time left, PICK and DROP badges that
    /// tick off as the order progresses, one line of plain status text, the order's value, and a
    /// timer bar that drains from green to red and shakes when the order is nearly out of time.
    /// A DELIVERED or LATE stamp lands when it resolves, then the ticket slides away, and new
    /// tickets drop in from above. A banner announces deliveries, late orders and level ups.
    ///
    /// Everything is built in code from Image and Text, so there is no prefab to wire up. Add this
    /// component to any scene object and press Play. Art is optional: assign a HudSkin asset and
    /// any sprite it holds replaces the matching flat colour; anything it leaves empty stays flat.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderTicketStrip : MonoBehaviour
    {
        [Header("Wiring (all found automatically if left empty)")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private CargoSystem cargoSystem;
        [SerializeField] private CargoCondition cargoCondition;
        [SerializeField] private PlayerProgression playerProgression;

        [Header("Art (optional)")]
        [Tooltip("Sprites for tickets, badges, stamps and bars. Leave empty for flat colours.")]
        [SerializeField] private HudSkin skin;

        [Header("Layout")]
        [Tooltip("Offset from the screen's top-left corner, in reference pixels (1920x1080).")]
        [SerializeField] private Vector2 stripOrigin = new Vector2(24f, -24f);
        [SerializeField] private Vector2 ticketSize = new Vector2(210f, 150f);
        [SerializeField] private float ticketGap = 10f;
        [Tooltip("Scales every ticket, including its text and the gaps between tickets. 1 is the " +
                 "original size, 1.05 is 5% bigger.")]
        [SerializeField] private float ticketScale = 1.05f;

        [Header("Behaviour")]
        [SerializeField] private float urgentBelowSeconds = 8f;
        [Tooltip("Seconds a DELIVERED / LATE stamp stays on a ticket before it slides away.")]
        [SerializeField] private float resolvedLingerSeconds = 1.1f;
        [SerializeField] private float bannerSeconds = 2.4f;

        private static readonly string[] StepLabels = { "PICK", "DROP" };
        private static readonly OrderStep[] Steps = { OrderStep.Pickup, OrderStep.Deliver };

        private class Ticket
        {
            public Order Order;
            public RectTransform Root;
            public Image Paper;
            public Image Header;
            public Text Title;
            public Text TimeText;
            public RectTransform[] BadgeRects;
            public Image[] Badges;
            public Image[] BadgeIcons;
            public Text[] BadgeLabels;
            public Text Status;
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

        private readonly List<Ticket> _tickets = new List<Ticket>();
        private readonly Dictionary<Order, Ticket> _byOrder = new Dictionary<Order, Ticket>();
        private readonly Queue<KeyValuePair<string, Color>> _bannerQueue = new Queue<KeyValuePair<string, Color>>();

        private RectTransform _stripRoot;
        private Canvas _canvas;
        private Text _banner;
        private float _bannerTimer;
        private Font _font;

        // --------------------------------------------------------------------------------------

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (cargoSystem == null) cargoSystem = FindAnyObjectByType<CargoSystem>();
            if (cargoCondition == null) cargoCondition = FindAnyObjectByType<CargoCondition>();
            if (playerProgression == null) playerProgression = FindAnyObjectByType<PlayerProgression>();

            if (orderManager == null)
            {
                Debug.LogError("[OrderTicketStrip] No OrderManager in the scene. Switching off.", this);
                enabled = false;
                return;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            HudSkin.LogStatus(skin, "OrderTicketStrip", this);
            BuildCanvas();

            orderManager.OrderResolved += HandleOrderResolved;

            if (playerProgression != null) playerProgression.LevelUp += HandleLevelUp;
            if (cargoSystem != null) cargoSystem.CapacityChanged += HandleCapacityChanged;
        }

        private void OnDestroy()
        {
            if (orderManager != null) orderManager.OrderResolved -= HandleOrderResolved;

            if (playerProgression != null) playerProgression.LevelUp -= HandleLevelUp;
            if (cargoSystem != null) cargoSystem.CapacityChanged -= HandleCapacityChanged;
        }

        // ---- events ---------------------------------------------------------------------------

        private void HandleOrderResolved(Order order, float value)
        {
            if (!_byOrder.TryGetValue(order, out Ticket ticket)) return;

            bool delivered = order.State == OrderState.Delivered;
            ticket.Resolving = true;
            ticket.ResolveTimer = resolvedLingerSeconds;

            Sprite stampSprite = skin == null ? null : (delivered ? skin.stampDelivered : skin.stampLate);
            ShowStamp(ticket, delivered ? "DELIVERED!" : "LATE!",
                delivered ? OrderPalette.Deliver : OrderPalette.Urgent, resolvedLingerSeconds + 0.6f,
                stampSprite);

            if (delivered)
                PushBanner($"Order #{order.Id} delivered!  +${value:0}", OrderPalette.Deliver);
            else
                PushBanner($"Order #{order.Id} ran out of time!", OrderPalette.Urgent);
        }

        private void HandleLevelUp(int level)
        {
            PushBanner($"LEVEL UP!  Level {level}", OrderPalette.Xp);
        }

        private void HandleCapacityChanged(int capacity)
        {
            PushBanner($"Cargo upgraded:  {capacity} slots", OrderPalette.Money);
        }

        // ---- per frame ------------------------------------------------------------------------

        private void Update()
        {
            SyncTickets();
            UpdateTickets(Time.unscaledDeltaTime);
            UpdateBanner(Time.unscaledDeltaTime);
        }

        private void SyncTickets()
        {
            IReadOnlyList<Order> active = orderManager.ActiveOrders;
            for (int i = 0; i < active.Count; i++)
            {
                if (_byOrder.ContainsKey(active[i])) continue;
                BuildTicket(active[i]);
            }
        }

        private void UpdateTickets(float dt)
        {
            float follow = 1f - Mathf.Exp(-12f * dt);

            for (int i = _tickets.Count - 1; i >= 0; i--)
            {
                Ticket t = _tickets[i];

                if (t.Resolving)
                {
                    t.ResolveTimer -= dt;
                    if (t.ResolveTimer <= -0.5f)
                    {
                        _byOrder.Remove(t.Order);
                        _tickets.RemoveAt(i);
                        Destroy(t.Root.gameObject);
                        continue;
                    }
                }

                RefreshTicket(t);
            }

            // Positions are assigned after removals so the survivors close the gap smoothly.
            for (int i = 0; i < _tickets.Count; i++)
            {
                Ticket t = _tickets[i];
                var target = new Vector2(i * (ticketSize.x + ticketGap), 0f);

                if (t.Resolving && t.ResolveTimer <= 0f)
                    target.y = 220f; // slide up and off the top of the screen

                t.Position = Vector2.Lerp(t.Position, target, follow);

                Vector2 shake = Vector2.zero;
                if (!t.Resolving && t.Order.TimeRemaining < urgentBelowSeconds && !t.Order.IsTerminal)
                {
                    float strength = 1f - Mathf.Clamp01(t.Order.TimeRemaining / urgentBelowSeconds);
                    shake.x = Mathf.Sin(Time.unscaledTime * 45f) * 2.5f * strength;
                }

                t.Root.anchoredPosition = t.Position + shake;

                if (t.StampTimer > 0f)
                {
                    t.StampTimer -= dt;
                    float age = t.StampDuration - t.StampTimer;
                    float pop = Mathf.Lerp(1.7f, 1f, Mathf.Clamp01(age / 0.15f));
                    t.StampRect.localScale = Vector3.one * pop;

                    float fade = Mathf.Clamp01(t.StampTimer / 0.4f);

                    Color c = t.Stamp.color;
                    c.a = fade;
                    t.Stamp.color = c;
                    t.StampImage.color = new Color(1f, 1f, 1f, fade);

                    if (t.StampTimer <= 0f)
                    {
                        t.Stamp.text = string.Empty;
                        t.StampImage.enabled = false;
                    }
                }
            }
        }

        private void RefreshTicket(Ticket t)
        {
            Order order = t.Order;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);

            // Header
            t.Title.text = $"ORDER #{order.Id}";
            t.TimeText.text = $"{Mathf.CeilToInt(order.TimeRemaining)}s";

            // Paper tints toward pink as the clock runs out.
            float urgency = order.IsTerminal
                ? 0f
                : 1f - Mathf.Clamp01(order.TimeRemaining / Mathf.Max(0.01f, urgentBelowSeconds));
            Color paperBase = HasArt(s => s.ticketPaper) ? Color.white : OrderPalette.Paper;
            t.Paper.color = Color.Lerp(paperBase, new Color(1f, 0.78f, 0.72f, 1f), urgency * pulse);

            // Badges
            int current = order.CurrentStep == OrderStep.Pickup ? 0 : 1;
            bool finished = order.State == OrderState.Delivered;
            int total = Steps.Length;

            Vector2 badgeSize = BadgeSize;
            const float badgeGap = 8f;
            float rowWidth = total * badgeSize.x + (total - 1) * badgeGap;
            float startX = (ticketSize.x - rowWidth) * 0.5f;
            bool icons = HasArt(s => s.iconPickup) && HasArt(s => s.iconDropOff);

            for (int i = 0; i < t.Badges.Length; i++)
            {
                t.BadgeRects[i].anchoredPosition =
                    new Vector2(startX + i * (badgeSize.x + badgeGap) + badgeSize.x * 0.5f, -64f);

                bool done = finished || i < current;
                bool isCurrent = !finished && i == current && !order.IsTerminal;

                Color stepColour = OrderPalette.ForStep(Steps[i]);
                Sprite stepIcon = icons ? (Steps[i] == OrderStep.Pickup ? skin.iconPickup : skin.iconDropOff) : null;

                if (done)
                {
                    t.Badges[i].color = Color.Lerp(stepColour, OrderPalette.Ink, 0.25f);
                    t.BadgeRects[i].localScale = Vector3.one;

                    if (HasArt(s => s.iconDone)) SetBadgeIcon(t, i, skin.iconDone, 1f);
                    else if (icons) SetBadgeIcon(t, i, stepIcon, 1f);
                    else SetBadgeText(t, i, "OK", Color.white);
                }
                else if (isCurrent)
                {
                    t.Badges[i].color = stepColour;
                    t.BadgeRects[i].localScale = Vector3.one * (1f + 0.08f * pulse);

                    if (icons) SetBadgeIcon(t, i, stepIcon, 1f);
                    else SetBadgeText(t, i, StepLabels[i], OrderPalette.Ink);
                }
                else
                {
                    t.Badges[i].color = OrderPalette.BadgeOff;
                    t.BadgeRects[i].localScale = Vector3.one;

                    if (icons) SetBadgeIcon(t, i, stepIcon, 0.45f);
                    else SetBadgeText(t, i, StepLabels[i],
                        new Color(OrderPalette.Ink.r, OrderPalette.Ink.g, OrderPalette.Ink.b, 0.55f));
                }
            }

            // Status line
            t.Status.text = StatusFor(order, out Color statusColour);
            t.Status.color = statusColour;

            // Timer bar
            float fraction = order.TimeLimit > 0.01f ? Mathf.Clamp01(order.TimeRemaining / order.TimeLimit) : 0f;
            float barHeight = t.BarFill.sizeDelta.y;
            float barWidth = (ticketSize.x - 20f) * fraction;
            // A sliced pill cannot get narrower than its own height without its round ends
            // overlapping, so below that it just disappears.
            bool pill = HasArt(s => s.barFill);
            t.BarFillImage.enabled = !pill || barWidth >= barHeight;
            t.BarFill.sizeDelta = new Vector2(Mathf.Max(pill ? barHeight : 0f, barWidth), barHeight);
            t.BarFillImage.color = OrderPalette.TimerBar(fraction);
        }

        private string StatusFor(Order order, out Color colour)
        {
            colour = OrderPalette.Ink;

            switch (order.State)
            {
                case OrderState.Delivered:
                    colour = new Color(0.1f, 0.5f, 0.2f, 1f);
                    return "Delivered!";

                case OrderState.Late:
                    colour = OrderPalette.Urgent;
                    return "Too late!";

                case OrderState.Collected:
                case OrderState.Carried:
                    if (cargoCondition != null && cargoCondition.ConditionEnabled)
                        return $"Deliver it!  ({cargoCondition.GetConditionPercent(order):0}% fresh)";
                    return "Deliver it!";

                default:
                    if (cargoSystem != null && !cargoSystem.HasCapacity)
                    {
                        colour = new Color(0.7f, 0.45f, 0f, 1f);
                        return "Cargo full: drop one off";
                    }
                    return $"Pick up  (${ValueOf(order):0})";
            }
        }

        private float ValueOf(Order order)
        {
            OrderTuning tuning = orderManager.Tuning;
            return tuning != null ? tuning.DeliveryValueFor(order.DesignedDistance) : 0f;
        }

        // ---- art helpers ----------------------------------------------------------------------

        private bool HasArt(System.Func<HudSkin, Sprite> pick) => skin != null && pick(skin) != null;

        /// <summary>Square badges when badge art is assigned (the art is square), the original
        /// wide flat badges otherwise.</summary>
        private Vector2 BadgeSize => HasArt(s => s.badgeBackground) ? new Vector2(46f, 46f) : new Vector2(58f, 40f);

        private static void SetBadgeIcon(Ticket t, int i, Sprite icon, float alpha)
        {
            t.BadgeIcons[i].enabled = true;
            t.BadgeIcons[i].sprite = icon;
            t.BadgeIcons[i].color = new Color(1f, 1f, 1f, alpha);
            t.BadgeLabels[i].text = string.Empty;
        }

        private static void SetBadgeText(Ticket t, int i, string text, Color colour)
        {
            t.BadgeIcons[i].enabled = false;
            t.BadgeLabels[i].text = text;
            t.BadgeLabels[i].color = colour;
        }

        // ---- banner ---------------------------------------------------------------------------

        private void PushBanner(string text, Color colour)
        {
            _bannerQueue.Enqueue(new KeyValuePair<string, Color>(text, colour));
        }

        private void UpdateBanner(float dt)
        {
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= dt;
                float fadeIn = Mathf.Clamp01((bannerSeconds - _bannerTimer) / 0.12f);
                float fadeOut = Mathf.Clamp01(_bannerTimer / 0.5f);

                Color c = _banner.color;
                c.a = Mathf.Min(fadeIn, fadeOut);
                _banner.color = c;
                _banner.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, fadeIn);

                if (_bannerTimer <= 0f) _banner.text = string.Empty;
                return;
            }

            if (_bannerQueue.Count == 0) return;

            KeyValuePair<string, Color> next = _bannerQueue.Dequeue();
            _banner.text = next.Key;
            _banner.color = new Color(next.Value.r, next.Value.g, next.Value.b, 0f);
            _bannerTimer = bannerSeconds;
        }

        // ---- building -------------------------------------------------------------------------

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("OrderTicketStrip Canvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var stripGO = new GameObject("Strip");
            stripGO.transform.SetParent(canvasGO.transform, false);
            _stripRoot = stripGO.AddComponent<RectTransform>();
            _stripRoot.anchorMin = new Vector2(0f, 1f);
            _stripRoot.anchorMax = new Vector2(0f, 1f);
            _stripRoot.pivot = new Vector2(0f, 1f);
            _stripRoot.anchoredPosition = stripOrigin;
            _stripRoot.sizeDelta = new Vector2(100f, ticketSize.y);
            _stripRoot.localScale = Vector3.one * Mathf.Max(0.1f, ticketScale);

            // Banner sits centred near the top, clear of both the strip and the right-hand HUD.
            var bannerGO = new GameObject("Banner");
            bannerGO.transform.SetParent(canvasGO.transform, false);
            _banner = bannerGO.AddComponent<Text>();
            _banner.font = _font;
            _banner.fontSize = 44;
            _banner.fontStyle = FontStyle.Bold;
            _banner.alignment = TextAnchor.MiddleCenter;
            _banner.horizontalOverflow = HorizontalWrapMode.Overflow;
            _banner.verticalOverflow = VerticalWrapMode.Overflow;
            _banner.raycastTarget = false;

            RectTransform bannerRect = _banner.rectTransform;
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -200f);
            bannerRect.sizeDelta = new Vector2(1200f, 70f);

            var outline = bannerGO.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private void BuildTicket(Order order)
        {
            var t = new Ticket { Order = order };

            float w = ticketSize.x, h = ticketSize.y;
            int index = _tickets.Count;

            var rootGO = new GameObject($"Ticket {order.Id}");
            rootGO.transform.SetParent(_stripRoot, false);
            t.Root = rootGO.AddComponent<RectTransform>();
            t.Root.anchorMin = new Vector2(0f, 1f);
            t.Root.anchorMax = new Vector2(0f, 1f);
            t.Root.pivot = new Vector2(0f, 1f);
            t.Root.sizeDelta = new Vector2(w, h);

            t.Paper = rootGO.AddComponent<Image>();
            t.Paper.color = OrderPalette.Paper;
            t.Paper.raycastTarget = false;
            if (skin != null) skin.ApplyFrame(t.Paper, skin.ticketPaper, h);
            var shadow = rootGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(4f, -4f);

            // Header
            RectTransform headerRect = MakeRect(t.Root, "Header", new Vector2(0f, 0f), new Vector2(w, 34f));
            t.Header = headerRect.gameObject.AddComponent<Image>();
            t.Header.color = OrderPalette.TicketHeader(order.Id);
            t.Header.raycastTarget = false;

            t.Title = MakeText(headerRect, "Title", new Vector2(10f, -2f), new Vector2(w - 70f, 30f),
                22, Color.white, TextAnchor.MiddleLeft, true);
            AddOutline(t.Title.gameObject, 2f);

            t.TimeText = MakeText(headerRect, "Time", new Vector2(w - 70f, -2f), new Vector2(60f, 30f),
                22, Color.white, TextAnchor.MiddleRight, true);
            AddOutline(t.TimeText.gameObject, 2f);

            // Badges
            t.BadgeRects = new RectTransform[Steps.Length];
            t.Badges = new Image[Steps.Length];
            t.BadgeIcons = new Image[Steps.Length];
            t.BadgeLabels = new Text[Steps.Length];
            Vector2 badgeSize = BadgeSize;
            for (int i = 0; i < Steps.Length; i++)
            {
                RectTransform badgeRect = MakeRect(t.Root, $"Badge {i}", new Vector2(0f, -44f), badgeSize);
                badgeRect.pivot = new Vector2(0.5f, 0.5f);
                t.BadgeRects[i] = badgeRect;

                t.Badges[i] = badgeRect.gameObject.AddComponent<Image>();
                t.Badges[i].raycastTarget = false;
                if (skin != null) HudSkin.ApplyIcon(t.Badges[i], skin.badgeBackground);

                RectTransform iconRect = MakeRect(badgeRect, "Icon", Vector2.zero, badgeSize * 0.68f);
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                t.BadgeIcons[i] = iconRect.gameObject.AddComponent<Image>();
                t.BadgeIcons[i].raycastTarget = false;
                t.BadgeIcons[i].preserveAspect = true;
                t.BadgeIcons[i].enabled = false;

                t.BadgeLabels[i] = MakeText(badgeRect, "Label", Vector2.zero, badgeSize,
                    16, OrderPalette.Ink, TextAnchor.MiddleCenter, true);
                // MakeText anchors top-left; this one fills its badge, so stretch it.
                RectTransform labelRect = t.BadgeLabels[i].rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
            }

            // Status line
            t.Status = MakeText(t.Root, "Status", new Vector2(8f, -90f), new Vector2(w - 16f, 30f),
                16, OrderPalette.Ink, TextAnchor.MiddleCenter, true);

            // Timer bar
            float barH = HasArt(s => s.barFill) ? 18f : 14f;
            RectTransform barBg = MakeRect(t.Root, "Bar background", new Vector2(10f, -124f), new Vector2(w - 20f, barH));
            Image barBgImage = barBg.gameObject.AddComponent<Image>();
            barBgImage.color = new Color(0.2f, 0.12f, 0.08f, 0.85f);
            barBgImage.raycastTarget = false;
            if (skin != null && HudSkin.ApplyPill(barBgImage, skin.barBackground, barH)) barBgImage.color = Color.white;

            t.BarFill = MakeRect(t.Root, "Bar fill", new Vector2(10f, -124f), new Vector2(w - 20f, barH));
            t.BarFillImage = t.BarFill.gameObject.AddComponent<Image>();
            t.BarFillImage.raycastTarget = false;
            if (skin != null) HudSkin.ApplyPill(t.BarFillImage, skin.barFill, barH);

            // Stamp, last so it draws on top of everything else
            t.StampRect = MakeRect(t.Root, "Stamp", new Vector2(0f, -52f), new Vector2(w, 60f));
            t.StampRect.pivot = new Vector2(0.5f, 0.5f);
            t.StampRect.anchoredPosition = new Vector2(w * 0.5f, -h * 0.5f);
            t.StampRect.localRotation = Quaternion.Euler(0f, 0f, 12f);
            t.Stamp = t.StampRect.gameObject.AddComponent<Text>();
            t.Stamp.font = _font;
            t.Stamp.fontSize = 40;
            t.Stamp.fontStyle = FontStyle.Bold;
            t.Stamp.alignment = TextAnchor.MiddleCenter;
            t.Stamp.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.Stamp.verticalOverflow = VerticalWrapMode.Overflow;
            t.Stamp.raycastTarget = false;
            AddOutline(t.Stamp.gameObject, 3f);

            // Sprite stamp, used instead of the text one when the skin has stamp art. The art is
            // already tilted, so it is not rotated again.
            RectTransform stampImageRect = MakeRect(t.Root, "Stamp image", Vector2.zero, new Vector2(w - 8f, h * 0.62f));
            stampImageRect.pivot = new Vector2(0.5f, 0.5f);
            stampImageRect.anchoredPosition = new Vector2(w * 0.5f, -h * 0.5f);
            t.StampImage = stampImageRect.gameObject.AddComponent<Image>();
            t.StampImage.preserveAspect = true;
            t.StampImage.raycastTarget = false;
            t.StampImage.enabled = false;

            // Starts above its slot and drops in.
            t.Position = new Vector2(index * (w + ticketGap), 220f);
            t.Root.anchoredPosition = t.Position;

            _tickets.Add(t);
            _byOrder[order] = t;

            RefreshTicket(t);
        }

        private void ShowStamp(Ticket t, string text, Color colour, float seconds, Sprite sprite)
        {
            bool useSprite = sprite != null;

            t.StampImage.enabled = useSprite;
            t.StampImage.sprite = sprite;
            t.StampImage.color = Color.white;
            t.Stamp.text = useSprite ? string.Empty : text;
            t.Stamp.color = colour;

            // The update loop pops and fades whatever StampRect points at, so point it at the
            // stamp that is actually showing.
            t.StampRect = useSprite ? t.StampImage.rectTransform : t.Stamp.rectTransform;
            t.StampDuration = seconds;
            t.StampTimer = seconds;
        }

        private RectTransform MakeRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
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

        private static void AddOutline(GameObject go, float distance)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(distance, -distance);
        }
    }
}