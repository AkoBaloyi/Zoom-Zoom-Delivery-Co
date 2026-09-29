using UnityEngine;
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
    /// BUILT IN CODE, LIKE THE HUD
    /// Same pattern as OrderHUD: Canvas, Image panel and UI.Text created at runtime from a handful
    /// of serialised numbers, so the layout is tunable in the inspector and there is no prefab to
    /// keep in step with the scene. Colours are the HUD's so a delivered count reads in the same
    /// green as a carried order and a late count in the same red as an expiring one.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShiftResults : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Found automatically if left empty.")]
        [SerializeField] private ShiftTimer shiftTimer;

        [Tooltip("Found automatically if left empty.")]
        [SerializeField] private OrderManager orderManager;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(620f, 400f);
        [SerializeField] private int titleFontSize = 48;
        [SerializeField] private int fontSize = 30;
        [SerializeField] private int buttonFontSize = 26;

        [Header("Colours (match OrderHUD so the same fact has the same colour everywhere)")]
        [SerializeField] private Color panelColour = new Color(0f, 0f, 0f, 0.82f);
        [SerializeField] private Color textColour = Color.white;
        [SerializeField] private Color deliveredColour = new Color(0.15f, 1f, 0.3f, 1f);
        [SerializeField] private Color lateColour = new Color(1f, 0.05f, 0.05f, 1f);
        [SerializeField] private Color buttonColour = new Color(0.16f, 0.16f, 0.16f, 1f);

        public int Delivered { get; private set; }
        public int Late { get; private set; }
        public float Value { get; private set; }
        public bool IsShowing { get; private set; }

        private GameObject _panel;
        private Text _deliveredLine;
        private Text _lateLine;
        private Text _valueLine;

        private void Awake()
        {
            if (shiftTimer == null) shiftTimer = FindAnyObjectByType<ShiftTimer>();
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();

            if (shiftTimer == null || orderManager == null)
            {
                Debug.LogError(
                    "[ShiftResults] Needs both a ShiftTimer and an OrderManager in the scene. " +
                    "Without the timer there is no end to react to; without the manager there is " +
                    "nothing to count. Switching off.", this);
                enabled = false;
                return;
            }

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

            if (order.State == OrderState.Delivered)
            {
                Delivered++;
                Value += value;
            }
            else
            {
                Late++;
            }
        }

        private void HandleShiftEnded()
        {
            IsShowing = true;

            _deliveredLine.text = $"Delivered   {Delivered}";
            _lateLine.text = $"Late        {Late}";
            _valueLine.text = $"Value       {Value:0}";

            _panel.SetActive(true);

            Debug.Log($"[ShiftResults] Shift over: {Delivered} delivered, {Late} late, " +
                      $"{Value:0} value.");
        }

        /// <summary>Reloads the current scene. Also wired to the Restart button.</summary>
        public void Restart()
        {
            // The pause menu sets timeScale to 0 and a reload does not reset it, so a restart from a
            // paused shift would otherwise load a frozen scene.
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("ShiftResults Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD and the pause menu. A result the player cannot see under another panel
            // is the silent ending this exists to remove.
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("Panel");
            _panel.transform.SetParent(canvasGO.transform, false);

            Image panelImage = _panel.AddComponent<Image>();
            panelImage.color = panelColour;

            RectTransform panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = panelSize;

            float y = -28f;

            Text title = CreateLine(panelRect, "Title", ref y, titleFontSize + 16f, titleFontSize,
                textColour, TextAnchor.UpperCenter);
            title.text = "SHIFT OVER";
            title.fontStyle = FontStyle.Bold;

            y -= 18f;
            float lineHeight = fontSize + 12f;

            _deliveredLine = CreateLine(panelRect, "Delivered", ref y, lineHeight, fontSize,
                deliveredColour, TextAnchor.UpperCenter);
            _lateLine = CreateLine(panelRect, "Late", ref y, lineHeight, fontSize,
                lateColour, TextAnchor.UpperCenter);
            _valueLine = CreateLine(panelRect, "Value", ref y, lineHeight, fontSize,
                textColour, TextAnchor.UpperCenter);

            // Buttons sit on the panel's bottom edge, side by side.
            var buttonSize = new Vector2(220f, 64f);
            CreateButton(panelRect, "Restart", "RESTART", new Vector2(-130f, 48f), buttonSize, Restart);
            CreateButton(panelRect, "Quit", "QUIT", new Vector2(130f, 48f), buttonSize, Quit);
        }

        private Text CreateLine(RectTransform parent, string name, ref float y, float advance,
            int size, Color colour, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, advance);

            y -= advance;
            return text;
        }

        private void CreateButton(RectTransform parent, string name, string label, Vector2 offset,
            Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = buttonColour;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(go.transform, false);

            Text text = labelGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = buttonFontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = textColour;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;

            RectTransform labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
    }
}
