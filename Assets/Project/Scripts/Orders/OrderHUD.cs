using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// The main HUD, built in code with nothing to wire:
    ///
    ///   Top centre   the shift timer, big and easy to read. In the last 30 seconds it turns red
    ///                and gently pulses.
    ///   Top right    the wallet, showing your money. When you are paid or lose money, the number
    ///                counts up or down and a green "+R336" or red "-R50" floats beside it.
    ///                Under it, how many orders you have delivered and failed, and what is in
    ///                the car (ingredients and cooked food).
    ///
    /// The order list itself is on the phone (OrderTicketStrip).
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderHUD : MonoBehaviour
    {
        [Header("Wiring (found automatically if left empty)")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private CargoSystem cargoSystem;
        [SerializeField] private ShiftTimer shiftTimer;
        [SerializeField] private PlayerProgression wallet;
        [Tooltip("Found automatically if left empty. Leave the vehicle without one and the " +
                 "damage readout simply does not appear.")]
        [SerializeField] private CrashCounter crashCounter;

        [Header("Behaviour")]
        [Tooltip("Seconds left in the shift when the timer turns red.")]
        [SerializeField] private float timerWarningSeconds = 30f;

        // Colours
        private static readonly Color PanelColour = new Color(0.08f, 0.08f, 0.10f, 0.85f);
        private static readonly Color Faded = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Gain = new Color(0.35f, 0.95f, 0.45f, 1f);
        private static readonly Color Loss = new Color(1f, 0.35f, 0.30f, 1f);

        // The pieces of the HUD
        private RectTransform _timerPanel;
        private Text _timerText;
        private Text _moneyText;
        private Text _moneyChangeText;
        private Text _deliveredText;
        private Text _failedText;
        private Text _ingredientsText;
        private Text _foodText;
        private Text _damageText;
        private Font _font;

        // Counters
        private int _delivered;
        private int _failed;

        // The wallet number counts smoothly toward the real balance.
        private float _shownMoney;
        private float _lastBalance;
        private float _moneyChangeTimer;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (cargoSystem == null) cargoSystem = FindAnyObjectByType<CargoSystem>();
            if (shiftTimer == null) shiftTimer = FindAnyObjectByType<ShiftTimer>();
            if (wallet == null) wallet = FindAnyObjectByType<PlayerProgression>();
            if (crashCounter == null) crashCounter = FindAnyObjectByType<CrashCounter>();

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildHud();

            if (orderManager != null) orderManager.OrderResolved += HandleOrderResolved;
            if (wallet != null) wallet.MoneyChanged += HandleMoneyChanged;
        }

        private void OnDestroy()
        {
            if (orderManager != null) orderManager.OrderResolved -= HandleOrderResolved;
            if (wallet != null) wallet.MoneyChanged -= HandleMoneyChanged;
        }

        // ---- events ---------------------------------------------------------------------------

        private void HandleOrderResolved(Order order, float value)
        {
            if (order.State == OrderState.Delivered) _delivered++;
            else _failed++;
        }

        private void HandleMoneyChanged(float newBalance)
        {
            float change = newBalance - _lastBalance;
            _lastBalance = newBalance;
            if (Mathf.Abs(change) < 0.5f) return;

            // Show the change beside the wallet, green for money in, red for money out.
            _moneyChangeText.text = change > 0f ? $"+R{change:0}" : $"-R{-change:0}";
            _moneyChangeText.color = change > 0f ? Gain : Loss;
            _moneyChangeTimer = 2f;
        }

        // ---- every frame ----------------------------------------------------------------------

        private void Update()
        {
            UpdateTimer();
            UpdateWallet();
            UpdateCounts();
        }

        private void UpdateTimer()
        {
            if (shiftTimer == null) return;

            float left = Mathf.Max(0f, shiftTimer.TimeRemaining);
            int seconds = Mathf.CeilToInt(left);
            _timerText.text = $"{seconds / 60}:{seconds % 60:00}";

            // Red and pulsing in the last stretch of the shift.
            bool warning = left <= timerWarningSeconds && left > 0f;
            _timerText.color = warning ? Loss : Color.white;
            float pulse = warning ? 1f + 0.06f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            _timerPanel.localScale = Vector3.one * pulse;
        }

        private void UpdateWallet()
        {
            float target = wallet != null ? wallet.Money : 0f;

            // Count toward the real balance over about half a second.
            _shownMoney = Mathf.Lerp(_shownMoney, target, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            if (Mathf.Abs(target - _shownMoney) < 1f) _shownMoney = target;

            _moneyText.text = _shownMoney < 0f ? $"-R{-_shownMoney:N0}" : $"R{_shownMoney:N0}";
            _moneyText.color = target < 0f ? Loss : OrderPalette.Money;

            // Fade the "+R336" out over its last half second.
            if (_moneyChangeTimer > 0f)
            {
                _moneyChangeTimer -= Time.unscaledDeltaTime;
                Color c = _moneyChangeText.color;
                c.a = Mathf.Clamp01(_moneyChangeTimer / 0.5f);
                _moneyChangeText.color = c;
            }
        }

        private void UpdateCounts()
        {
            _deliveredText.text = $"Delivered  {_delivered}";
            _failedText.text = $"Failed  {_failed}";
            _failedText.color = _failed > 0 ? Loss : Faded;

            if (cargoSystem == null) return;

            int held = cargoSystem.IngredientsHeld;
            _ingredientsText.text = $"Ingredients  {held}/{cargoSystem.IngredientCapacity}";
            _ingredientsText.color = held >= cargoSystem.IngredientCapacity ? OrderPalette.Warning : Color.white;

            _foodText.text = $"Food  {cargoSystem.SlotsUsed}/{cargoSystem.Capacity}";
            _foodText.color = cargoSystem.SlotsUsed >= cargoSystem.Capacity ? OrderPalette.Warning : Color.white;

            // Running damage bill. The money is not taken until the shift ends, so this is a
            // warning rather than a live deduction: the wallet above stays honest about what has
            // actually been earned.
            if (_damageText != null && crashCounter != null)
            {
                int hits = crashCounter.Hits;
                _damageText.text = hits == 0
                    ? "Damage  none"
                    : $"Damage  {hits} hit{(hits == 1 ? "" : "s")}  -R{crashCounter.Penalty:0}";
                _damageText.color = hits == 0 ? Faded : Loss;
            }
        }

        // ---- building -------------------------------------------------------------------------

        private void BuildHud()
        {
            var canvasObject = new GameObject("HUD Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasObject.transform;

            // Timer, top centre.
            _timerPanel = MakePanel(root, "Timer", new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(200f, 86f));
            Text timerLabel = MakeText(_timerPanel, "Label", new Vector2(0f, -8f), new Vector2(200f, 20f), 16, Faded, TextAnchor.MiddleCenter);
            timerLabel.text = "SHIFT";
            _timerText = MakeText(_timerPanel, "Time", new Vector2(0f, -28f), new Vector2(200f, 50f), 46, Color.white, TextAnchor.MiddleCenter);

            // Wallet, top right.
            RectTransform walletPanel = MakePanel(root, "Wallet", new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(300f, 78f));
            Text walletLabel = MakeText(walletPanel, "Label", new Vector2(18f, -8f), new Vector2(200f, 20f), 16, Faded, TextAnchor.MiddleLeft);
            walletLabel.text = "WALLET";
            _moneyText = MakeText(walletPanel, "Money", new Vector2(18f, -28f), new Vector2(264f, 44f), 40, OrderPalette.Money, TextAnchor.MiddleLeft);

            // "+R336" floats just to the left of the wallet.
            _moneyChangeText = MakeText(walletPanel, "Change", new Vector2(-170f, -26f), new Vector2(160f, 40f), 32, Gain, TextAnchor.MiddleRight);
            _moneyChangeText.text = "";

            // Delivered and failed.
            RectTransform scorePanel = MakePanel(root, "Score", new Vector2(1f, 1f), new Vector2(-24f, -106f), new Vector2(300f, 50f));
            _deliveredText = MakeText(scorePanel, "Delivered", new Vector2(18f, -6f), new Vector2(160f, 38f), 24, Color.white, TextAnchor.MiddleLeft);
            _failedText = MakeText(scorePanel, "Failed", new Vector2(170f, -6f), new Vector2(112f, 38f), 20, Faded, TextAnchor.MiddleRight);

            // What is in the car.
            RectTransform cargoPanel = MakePanel(root, "Cargo", new Vector2(1f, 1f), new Vector2(-24f, -164f), new Vector2(300f, 50f));
            _ingredientsText = MakeText(cargoPanel, "Ingredients", new Vector2(18f, -6f), new Vector2(170f, 38f), 20, Color.white, TextAnchor.MiddleLeft);
            _foodText = MakeText(cargoPanel, "Food", new Vector2(190f, -6f), new Vector2(92f, 38f), 20, Color.white, TextAnchor.MiddleRight);

            // Damage, directly under the cargo counts, since it is the same kind of fact: what
            // the car is carrying and what it has cost so far.
            if (crashCounter != null)
            {
                RectTransform damagePanel = MakePanel(root, "Damage", new Vector2(1f, 1f), new Vector2(-24f, -220f), new Vector2(300f, 50f));
                _damageText = MakeText(damagePanel, "Damage", new Vector2(18f, -6f), new Vector2(264f, 38f), 20, Faded, TextAnchor.MiddleLeft);
                _damageText.text = "Damage  none";
            }
        }

        /// <summary>A dark rounded panel. anchor is the screen corner or edge it sticks to.</summary>
        private RectTransform MakePanel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);

            RectTransform rect = panelObject.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = panelObject.AddComponent<Image>();
            image.raycastTarget = false;
            OrderTicketStrip.ApplyRounded(image, 16f);
            image.color = PanelColour;
            return rect;
        }

        /// <summary>Text placed from the panel's top left corner.</summary>
        private Text MakeText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, Color colour, TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = textObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = colour;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
