using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// Esc (or gamepad Start) pauses the game: freezes time, dims the screen, and shows
    /// Resume / Restart Shift / Controls / Quit to Menu. Built in code, nothing to wire.
    ///
    /// Sits on a canvas above every other HUD canvas (sortingOrder 60), so markers, arrows and
    /// the trick readout can never draw over it, the bug Ako flagged in his UI pack.
    ///
    /// Put this on an empty GameObject anywhere in the gameplay scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenu : MonoBehaviour
    {
        [Header("Scenes")]
        [Tooltip("Scene to load for Quit to Menu. Must match the scene's actual name.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private static readonly Color Paper = new Color(0.98f, 0.93f, 0.78f, 1f);
        private static readonly Color Ink = new Color(0.25f, 0.15f, 0.08f, 1f);

        private GameObject _dim;
        private RectTransform _controlsPanel;
        private Font _font;
        private bool _paused;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
            _dim.SetActive(false);
        }

        private void Update()
        {
            bool pressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (!pressed && Gamepad.current != null) pressed = Gamepad.current.startButton.wasPressedThisFrame;

            if (pressed) SetPaused(!_paused);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            _dim.SetActive(paused);
            if (!paused) _controlsPanel.gameObject.SetActive(false);
        }

        private void RestartShift()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void QuitToMenu()
        {
            Time.timeScale = 1f;
            if (string.IsNullOrEmpty(mainMenuSceneName)) return;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void ToggleControls() =>
            _controlsPanel.gameObject.SetActive(!_controlsPanel.gameObject.activeSelf);

        // ---- building ---------------------------------------------------------------------

        private void Build()
        {
            var canvasGO = new GameObject("Pause Canvas");
            canvasGO.transform.SetParent(transform, false);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60; // above every HUD canvas (timer/wallet at 9, phone at 10)

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _dim = new GameObject("Dim");
            _dim.transform.SetParent(canvasGO.transform, false);
            RectTransform dimRect = _dim.AddComponent<RectTransform>();
            Stretch(dimRect);
            Image dimImage = _dim.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.6f);

            RectTransform panel = MakeRect(dimRect, "Panel", new Vector2(420f, 460f));
            Image panelImage = panel.gameObject.AddComponent<Image>();
            OrderTicketStrip.ApplyRounded(panelImage, 18f);
            panelImage.color = Paper;

            MakeText(panel, "Title", new Vector2(0f, 190f), new Vector2(420f, 40f), 30, Ink, TextAnchor.MiddleCenter).text = "PAUSED";

            MakeButton(panel, "Resume", new Vector2(0f, 90f), () => SetPaused(false), filled: true);
            MakeButton(panel, "Restart Shift", new Vector2(0f, 30f), RestartShift, filled: false);
            MakeButton(panel, "Controls", new Vector2(0f, -30f), ToggleControls, filled: false);
            MakeButton(panel, "Quit to Menu", new Vector2(0f, -90f), QuitToMenu, filled: false);

            MakeText(panel, "Hint", new Vector2(0f, -150f), new Vector2(420f, 24f), 13,
                new Color(Ink.r, Ink.g, Ink.b, 0.6f), TextAnchor.MiddleCenter).text = "Esc to resume";

            // Controls, hidden until the button is pressed.
            _controlsPanel = MakeRect(dimRect, "Controls", new Vector2(620f, 360f));
            Image controlsImage = _controlsPanel.gameObject.AddComponent<Image>();
            OrderTicketStrip.ApplyRounded(controlsImage, 18f);
            controlsImage.color = new Color(0.08f, 0.08f, 0.10f, 0.95f);

            string controlsText =
                "DRIVE\nW accelerate   S brake, then reverse at rest   A D steer\n\n" +
                "TRICKS - EASY WAY\nShift+gas dash   Shift+brake stop   Ctrl+steer drift   Space+steer quick turn\n\n" +
                "ORDERS\nDrive through the coloured disc to collect or deliver.\n" +
                "Tab / D-pad right changes which order you are following.";
            MakeText(_controlsPanel, "Text", new Vector2(0f, 20f), new Vector2(560f, 260f), 18,
                Color.white, TextAnchor.MiddleCenter).text = controlsText;

            MakeButton(_controlsPanel, "Close", new Vector2(0f, -140f), ToggleControls, filled: true);
            _controlsPanel.gameObject.SetActive(false);
        }

        private Button MakeButton(Transform parent, string label, Vector2 position, System.Action onClick, bool filled)
        {
            RectTransform rect = MakeRect(parent, label, new Vector2(240f, 46f));
            rect.anchoredPosition = position;

            Image image = rect.gameObject.AddComponent<Image>();
            OrderTicketStrip.ApplyRounded(image, 8f);
            image.color = filled ? Ink : Paper;
            if (!filled)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = Ink;
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            Button button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());

            Text text = MakeText(rect, "Label", Vector2.zero, new Vector2(240f, 46f), 20,
                filled ? Paper : Ink, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.text = label;
            return button;
        }

        private RectTransform MakeRect(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        private Text MakeText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, Color colour, TextAnchor alignment)
        {
            RectTransform rect = MakeRect(parent, name, size);
            rect.anchoredPosition = position;

            Text text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = colour;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
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
    }
}
