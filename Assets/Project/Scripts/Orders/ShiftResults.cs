using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// What the player sees when the shift clock reaches zero.
    ///
    /// WHY THIS EXISTS
    /// ShiftTimer ends the shift correctly: it stops spawning, stops ageing, and raises ShiftEnded.
    /// Nothing listened. The HUD clock read 00:00, the frozen orders stayed on screen, the car kept
    /// driving, and the player had no way to tell that anything had happened, let alone how they
    /// had done. A loop that closes silently has not closed. This is the last link: it turns the
    /// event into a result the player can read and a way back to the start.
    ///
    /// WHAT IT COUNTS AND WHEN IT STOPS COUNTING
    /// Delivered, late and value are tallied from OrderManager.OrderResolved, the same event OrderHUD
    /// reads, so the two never disagree. The tally is frozen the moment ShiftEnded fires. That
    /// matters because ZoneDetection keeps running after the shift and OrderManager's methods can
    /// still be called on a disabled component, so a Carried order can still be dropped off after
    /// time. The HUD will count it; this screen will not, because a delivery after the whistle is not
    /// part of the shift.
    ///
    /// THE PANEL ART
    /// Drag the cartoon panel PNG onto Panel Sprite and the whole screen is laid out around it:
    /// the title sits in the orange header band, the stars and the three stat rows on the cream
    /// body, and the buttons across the bottom of it. Everything is positioned in fractions of the
    /// panel rather than fixed pixels, so changing Panel Size keeps the layout on the art.
    /// Panel Size must keep the artwork's own ratio (about 1.76 wide to 1 tall) or the picture
    /// stretches. Leave Panel Sprite empty and it falls back to the old plain dark panel.
    ///
    /// BUILT IN CODE, LIKE THE HUD
    /// Same pattern as OrderHUD: Canvas, Image panel and UI.Text created at runtime from a handful
    /// of serialised numbers, so the layout is tunable in the inspector and there is no prefab to
    /// keep in step with the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShiftResults : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Found automatically if left empty.")]
        [SerializeField] private ShiftTimer shiftTimer;

        [Tooltip("Found automatically if left empty.")]
        [SerializeField] private OrderManager orderManager;

        [Tooltip("Found automatically if left empty. Supplies the hit count and what it costs.")]
        [SerializeField] private CrashCounter crashCounter;

        [Header("Ranking (share of the orders a shift actually produces)")]
        [Tooltip("How many orders a real shift puts in front of the player. 25 is the absolute " +
                 "ceiling the spawner can reach (first at 3s, then one every 12s, throttled by " +
                 "the 1-2-3-4 ramp), but that assumes no travel time at all. Playtesting puts " +
                 "the real figure near 12, so that is what the stars are measured against.\n\n" +
                 "Retune this one number after a playtest and all three star bars move with it.")]
        [SerializeField] private int ordersPerShift = 12;

        [Tooltip("Share of those orders that must be delivered for each star. Percentages " +
                 "rather than counts so the bars follow Orders Per Shift instead of having to " +
                 "be rewritten every time it changes.\n\n" +
                 "At 12 orders these come out as 4, 7 and 10.")]
        [Range(0, 100)] [SerializeField] private int oneStarPercent = 30;
        [Range(0, 100)] [SerializeField] private int twoStarPercent = 60;
        [Range(0, 100)] [SerializeField] private int threeStarPercent = 80;

        [Header("Scenes")]
        [Tooltip("Exact name of the start screen scene. It must be in File > Build Profiles > Scene List.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Panel art")]
        [Tooltip("The cartoon panel PNG. Import it as a Sprite (2D and UI). Leave empty for the " +
                 "old plain dark panel.")]
        [SerializeField] private Sprite panelSprite;

        // These fields are deliberately named differently from the ones this class used before the
        // artwork went in. Unity keeps a serialised value against a field NAME, so a component
        // already sitting in a scene holds on to its old numbers and never sees a new default. The
        // old Button Colour was a near black grey, which with the new dark brown label made the
        // buttons look like they had vanished. Renaming drops the stale value and takes these.

        [Tooltip("Keep this at the artwork's own ratio, about 1.76 wide to 1 tall, or the picture " +
                 "stretches. 1150 x 653 is that ratio; scale both numbers together to resize.")]
        [SerializeField] private Vector2 cardSize = new Vector2(1150f, 653f);

        [Tooltip("Darkens the game behind the panel so it reads as an ending, not an overlay.")]
        [SerializeField] private Color backdropColour = new Color(0f, 0f, 0f, 0.55f);

        [Header("Type")]
        [SerializeField] private int titleSize = 58;
        [SerializeField] private int starsSize = 54;
        [Tooltip("Five rows now share the body, so this is smaller than it was with three.")]
        [SerializeField] private int rowSize = 30;
        [SerializeField] private int buttonTextSize = 30;

        [Header("Colours (tuned for the cream panel, not the old dark one)")]
        [Tooltip("Used only when Panel Sprite is empty.")]
        [SerializeField] private Color fallbackPanelColour = new Color(0f, 0f, 0f, 0.82f);
        [Tooltip("The dark brown of the artwork's outlines. Used for the title and the labels.")]
        [SerializeField] private Color inkColour = new Color(0.26f, 0.16f, 0.09f, 1f);
        [SerializeField] private Color deliveredTextColour = new Color(0.10f, 0.52f, 0.20f, 1f);
        [SerializeField] private Color lateTextColour = new Color(0.78f, 0.16f, 0.12f, 1f);
        [SerializeField] private Color starColour = new Color(1f, 0.72f, 0.15f, 1f);
        [Tooltip("The orange of the artwork's header band.")]
        [SerializeField] private Color buttonFill = new Color(0.98f, 0.72f, 0.14f, 1f);

        public int Delivered { get; private set; }
        public int Late { get; private set; }
        public float Value { get; private set; }
        public bool IsShowing { get; private set; }

        private GameObject _panel;
        private Text _title;
        private Text _starsLine;
        private Text _deliveredValue;
        private Text _lateValue;
        private Text _moneyValue;
        private Text _damageValue;
        private Text _totalValue;

        private void Awake()
        {
            if (shiftTimer == null) shiftTimer = FindAnyObjectByType<ShiftTimer>();
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (crashCounter == null) crashCounter = FindAnyObjectByType<CrashCounter>();

            if (shiftTimer == null || orderManager == null)
            {
                Debug.LogError(
                    "[ShiftResults] Needs both a ShiftTimer and an OrderManager in the scene. " +
                    "Without the timer there is no end to react to; without the manager there is " +
                    "nothing to count. Switching off.", this);
                enabled = false;
                return;
            }

            EnsureEventSystem(); // the buttons cannot be clicked without one
            BuildCanvas();
            _panel.SetActive(false);

            orderManager.OrderResolved += HandleOrderResolved;
            shiftTimer.ShiftEnded += HandleShiftEnded;
        }

        private void OnDestroy()
        {
            if (orderManager != null) orderManager.OrderResolved -= HandleOrderResolved;
            if (shiftTimer != null) shiftTimer.ShiftEnded -= HandleShiftEnded;
        }

        private void HandleOrderResolved(Order order, float value)
        {
            // Frozen once the whistle has gone. See the class comment.
            if (IsShowing) return;

            // Value is negative for a cancelled order (the penalty), so it counts toward money too.
            Value += value;

            if (order.State == OrderState.Delivered) Delivered++;
            else Late++;
        }

        private void HandleShiftEnded()
        {
            IsShowing = true;

            // The buttons need the mouse, even if the driving camera had it locked.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // RANKING: what share of a shift's orders actually got there, not how much money was
            // made. See the note on the threshold fields for why the old Rand bars could not be
            // reached.
            int stars = Delivered >= DeliveriesFor(threeStarPercent) ? 3
                      : Delivered >= DeliveriesFor(twoStarPercent) ? 2
                      : Delivered >= DeliveriesFor(oneStarPercent) ? 1 : 0;
            _starsLine.text = Stars(stars);

            // The damage bill is charged once, here, rather than during the drive, so the wallet
            // on the HUD only ever shows money genuinely earned and the cost of driving badly
            // arrives as one readable number at the end.
            int hits = crashCounter != null ? crashCounter.Hits : 0;
            float damage = crashCounter != null ? crashCounter.Penalty : 0f;
            float total = Value - damage;

            _deliveredValue.text = Delivered.ToString();
            _lateValue.text = Late.ToString();
            _moneyValue.text = Money(Value);
            _damageValue.text = hits == 0 ? "none" : $"{hits} x R{crashCounter.RandPerHit:0}   -R{damage:0}";
            _damageValue.color = hits == 0 ? inkColour : lateTextColour;
            _totalValue.text = Money(total);
            _totalValue.color = total < 0f ? lateTextColour : deliveredTextColour;

            _panel.SetActive(true);

            Debug.Log($"[ShiftResults] Shift over: {Delivered} delivered, {Late} late, " +
                      $"R{Value:0} earned, {hits} hit(s) costing R{damage:0}, total R{total:0}. " +
                      $"{stars} star(s) against bars of {DeliveriesFor(oneStarPercent)}, " +
                      $"{DeliveriesFor(twoStarPercent)} and {DeliveriesFor(threeStarPercent)} " +
                      $"out of {ordersPerShift}.");
        }

        /// <summary>Deliveries needed to hit a given share of a shift's orders. Rounded to the
        /// nearest whole order, and never zero, so a low percentage still has to be earned.</summary>
        private int DeliveriesFor(int percent) =>
            Mathf.Max(1, Mathf.RoundToInt(ordersPerShift * percent / 100f));

        /// <summary>Rand with the minus sign outside the R, so a loss reads "-R120" rather than
        /// "R-120".</summary>
        private static string Money(float amount) =>
            amount < 0f ? $"-R{-amount:0}" : $"R{amount:0}";

        private static string Stars(int count)
        {
            string s = "";
            for (int i = 0; i < 3; i++) s += i < count ? "★ " : "☆ ";
            return s.TrimEnd();
        }

        /// <summary>Reloads the current scene. Also wired to the Restart button.</summary>
        public void Restart()
        {
            // The pause menu sets timeScale to 0 and a reload does not reset it, so a restart from a
            // paused shift would otherwise load a frozen scene.
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Buttons need an EventSystem to receive clicks. Adds one, using the Input
        /// System's UI module, only if the scene does not already have one.</summary>
        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>Loads the start screen. Also wired to the Main Menu button.</summary>
        public void GoToMainMenu()
        {
            Time.timeScale = 1f; // same reason as Restart

            if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                Debug.LogError($"[ShiftResults] Cannot load scene \"{mainMenuSceneName}\". Check the " +
                               "name on this component and that the scene is in File > Build Profiles > " +
                               "Scene List.", this);
                return;
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>Quits the player. In the editor there is nothing to quit, so it says so.</summary>
        public void Quit()
        {
            if (Application.isEditor)
            {
                Debug.Log("[ShiftResults] Quit pressed. In the editor this does nothing; in a build " +
                          "it closes the game.");
                return;
            }

            Application.Quit();
        }

        // ==================================================================
        // LAYOUT
        // ==================================================================
        //
        // Where the artwork's parts sit, measured off the PNG, as fractions of its height from the
        // top: the orange header band runs 0.10 to 0.31, and the cream body 0.33 to 0.90. Anchors
        // below are in Unity's bottom-up form (1 is the top edge), which is why the header lands at
        // 0.69 to 0.90 and the body at 0.10 to 0.67.

        private const float HeaderBottom = 0.69f;
        private const float HeaderTop = 0.90f;
        private const float BodyBottom = 0.10f;
        private const float BodyTop = 0.67f;

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("ShiftResults Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD (9), the order phone (10) and PauseMenu (60). A result the player
            // cannot see under another panel is the silent ending this exists to remove.
            canvas.sortingOrder = 70;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // The backdrop is what gets switched on and off, so the dim and the card appear
            // together and nothing of the result is left showing behind it.
            _panel = new GameObject("Results");
            _panel.transform.SetParent(canvasGO.transform, false);
            RectTransform backdrop = _panel.AddComponent<RectTransform>();
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = Vector2.zero;
            backdrop.offsetMax = Vector2.zero;
            Image dim = _panel.AddComponent<Image>();
            dim.color = backdropColour;

            // The card itself.
            var cardGO = new GameObject("Card");
            cardGO.transform.SetParent(_panel.transform, false);
            RectTransform card = cardGO.AddComponent<RectTransform>();
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = cardSize;

            Image cardImage = cardGO.AddComponent<Image>();
            if (panelSprite != null)
            {
                cardImage.sprite = panelSprite;
                cardImage.color = Color.white;
            }
            else
            {
                cardImage.color = fallbackPanelColour;
            }

            // Title, centred in the orange header band. Starts past x 0.20 so it clears the little
            // parcel and its sparkles in the artwork's top left corner.
            _title = MakeText(card, "Title", new Vector2(0.20f, HeaderBottom), new Vector2(0.94f, HeaderTop),
                titleSize, inkColour, TextAnchor.MiddleCenter, true);
            _title.text = "TIME UP!";

            _starsLine = MakeText(card, "Stars", new Vector2(0.08f, 0.575f), new Vector2(0.92f, BodyTop),
                starsSize, starColour, TextAnchor.MiddleCenter, true);
            AddOutline(_starsLine.gameObject, inkColour, 2f);

            // Five stat rows between the stars and the buttons: label left, number right. Total
            // is last and a size larger, because it is the one number the player came for.
            const float rowBand = 0.062f;
            float rowTop = 0.575f;

            MakeStatRow(card, "Delivered", "Delivered", rowTop - rowBand, rowTop, deliveredTextColour, out _deliveredValue);
            rowTop -= rowBand;
            MakeStatRow(card, "Late", "Late", rowTop - rowBand, rowTop, lateTextColour, out _lateValue);
            rowTop -= rowBand;
            MakeStatRow(card, "Money", "Money", rowTop - rowBand, rowTop, inkColour, out _moneyValue);
            rowTop -= rowBand;
            MakeStatRow(card, "Damage", "Damage", rowTop - rowBand, rowTop, inkColour, out _damageValue);
            rowTop -= rowBand;
            MakeStatRow(card, "Total", "Total", rowTop - rowBand, rowTop, deliveredTextColour, out _totalValue, rowSize + 6);

            // Buttons across the bottom of the cream body.
            const float buttonBottom = BodyBottom + 0.025f;
            const float buttonTop = buttonBottom + 0.130f;
            MakeButton(card, "Restart", "RESTART", 0.090f, 0.345f, buttonBottom, buttonTop, Restart);
            MakeButton(card, "MainMenu", "MAIN MENU", 0.372f, 0.628f, buttonBottom, buttonTop, GoToMainMenu);
            MakeButton(card, "Quit", "QUIT", 0.655f, 0.910f, buttonBottom, buttonTop, Quit);
        }

        /// <summary>A label on the left and its number on the right, both on one band of the body,
        /// which reads far better than one centred line of padded spaces.</summary>
        private void MakeStatRow(RectTransform card, string name, string label, float bottom, float top,
            Color colour, out Text value, int size = 0)
        {
            if (size <= 0) size = rowSize;

            Text labelText = MakeText(card, name + " label", new Vector2(0.12f, bottom), new Vector2(0.55f, top),
                size, inkColour, TextAnchor.MiddleLeft, true);
            labelText.text = label;

            // The value box starts before the label box ends; both are single words on opposite
            // sides, so the overlap is harmless and gives a long value like "3 x R5   -R15" the
            // room it needs.
            value = MakeText(card, name + " value", new Vector2(0.40f, bottom), new Vector2(0.88f, top),
                size, colour, TextAnchor.MiddleRight, true);
            value.text = "0";
        }

        private Text MakeText(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            int size, Color colour, TextAnchor alignment, bool bold)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = colour;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void MakeButton(RectTransform parent, string name, string label,
            float left, float right, float bottom, float top, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = go.AddComponent<Image>();
            image.color = buttonFill;
            // The rounded rectangle OrderTicketStrip generates, so the buttons match the artwork's
            // soft corners without needing a second sprite.
            OrderTicketStrip.ApplyRounded(image, 18f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            Text text = MakeText(rect, "Label", Vector2.zero, Vector2.one,
                buttonTextSize, inkColour, TextAnchor.MiddleCenter, true);
            text.text = label;
        }

        private static void AddOutline(GameObject go, Color colour, float distance)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = colour;
            outline.effectDistance = new Vector2(distance, -distance);
        }
    }
}
