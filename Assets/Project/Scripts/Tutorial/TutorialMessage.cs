using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A short line of text across the top of the screen for a few seconds, then it hides itself.
// No button, no choice, the player just keeps driving while they read it.
//
// This version cannot silently do nothing. The old one needed panelRoot and messageText wired
// in the Inspector and logged an error if they weren't, which meant a missed field anywhere in
// the chain showed the player no message at all. Now:
//   - If panelRoot/messageText are wired, it uses your panel, exactly as before.
//   - If they are not, it builds its own top banner the first time it is asked to show
//     something, so a message always appears.
//   - TutorialMessage.ShowAtTop("...") is a static shortcut that finds whichever TutorialMessage
//     is already in the scene (even one on a switched-off object) or makes one, so a caller
//     needs no Inspector reference at all.
public class TutorialMessage : MonoBehaviour
{
    [Header("Your panel (optional - one is built automatically if these are empty)")]
    public GameObject panelRoot;
    public TextMeshProUGUI messageText;

    [Tooltip("How long the message stays up if Show is called with no duration of its own.")]
    [SerializeField] private float defaultDuration = 5f;

    [Header("Auto-built banner (only used when panelRoot is left empty)")]
    [Tooltip("Gap between the top of the screen and the banner, in 1920x1080 reference pixels.")]
    [SerializeField] private float topOffset = 40f;
    [SerializeField] private Vector2 bannerSize = new Vector2(1200f, 96f);
    [SerializeField] private int bannerFontSize = 40;
    [SerializeField] private Color bannerBackground = new Color(0f, 0f, 0f, 0.72f);

    // Timed on unscaled time: TutorialChoiceUI sets Time.timeScale to 0 while its panel is up,
    // and a message shown around that moment would otherwise hang on screen forever.
    private float _hideAt = -1f;

    // Only used by the auto-built banner, when TMP has no default font to fall back on.
    private Text _legacyText;
    private bool _built;

    private static TutorialMessage _shared;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (_shared == null) _shared = this;
    }

    /// <summary>Shows a message at the top of the screen with no Inspector wiring needed at all.
    /// Uses whichever TutorialMessage is already in the scene if there is one, so a panel you
    /// built by hand is still the one that shows, and only builds a banner as a last resort.</summary>
    public static void ShowAtTop(string message, float duration = -1f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        TutorialMessage target = _shared;

        if (target == null)
            target = FindAnyObjectByType<TutorialMessage>(FindObjectsInactive.Include);

        if (target == null)
        {
            var go = new GameObject("Tutorial Message (auto)");
            target = go.AddComponent<TutorialMessage>();
        }

        _shared = target;

        // A message on a switched-off object would never tick its own hide timer.
        if (!target.gameObject.activeSelf) target.gameObject.SetActive(true);

        target.Show(message, duration);
    }

    /// <summary>Shows message for duration seconds (or defaultDuration if duration is left at
    /// -1), then hides on its own. Calling Show again while one is already up just replaces
    /// the text and restarts the timer.</summary>
    public void Show(string message, float duration = -1f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        if (panelRoot == null || (messageText == null && _legacyText == null)) BuildBanner();

        if (messageText != null) messageText.text = message;
        else if (_legacyText != null) _legacyText.text = message;

        if (panelRoot != null) panelRoot.SetActive(true);

        _hideAt = Time.unscaledTime + (duration > 0f ? duration : defaultDuration);
    }

    public void Hide()
    {
        _hideAt = -1f;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (_hideAt < 0f) return;
        if (Time.unscaledTime >= _hideAt) Hide();
    }

    // ---- the fallback banner ------------------------------------------------------------------

    /// <summary>Builds a top-of-screen banner on its own Canvas. Only runs when the Inspector
    /// fields were left empty, and only once.</summary>
    private void BuildBanner()
    {
        if (_built) return;
        _built = true;

        var canvasGO = new GameObject("Tutorial Message Canvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // above the order HUD and the arrows

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panelGO = new GameObject("Banner");
        panelGO.transform.SetParent(canvasGO.transform, false);

        RectTransform rect = panelGO.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = bannerSize;
        rect.anchoredPosition = new Vector2(0f, -topOffset);

        Image background = panelGO.AddComponent<Image>();
        background.color = bannerBackground;
        background.raycastTarget = false;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);

        if (!TryBuildTmpText(textGO)) BuildLegacyText(textGO);

        panelRoot = panelGO;
        panelRoot.SetActive(false);
    }

    /// <summary>TMP is what the rest of the project's UI uses, so it is tried first. If the
    /// project has no default TMP font asset, building one at runtime throws rather than just
    /// drawing nothing, hence the guard and the legacy fallback underneath.</summary>
    private bool TryBuildTmpText(GameObject textGO)
    {
        try
        {
            if (TMP_Settings.defaultFontAsset == null) return false;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = bannerFontSize;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            StretchToPanel(tmp.rectTransform);
            messageText = tmp;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void BuildLegacyText(GameObject textGO)
    {
        var text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = bannerFontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;

        StretchToPanel(text.rectTransform);
        _legacyText = text;
    }

    private static void StretchToPanel(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(24f, 10f);
        rect.offsetMax = new Vector2(-24f, -10f);
    }
}
