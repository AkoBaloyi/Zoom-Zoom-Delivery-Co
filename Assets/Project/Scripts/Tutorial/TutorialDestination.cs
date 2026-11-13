using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ZoomZoom.Orders;
using ZoomZoom.Orders.UI;

// Beat 2 (Arrive) AND Beat 3 (Clock), per Ako's tutorial plan: one destination appears, starts
// off-screen, and the beat ends the moment the car drives into it. Beat 3 is explicitly "same
// route, now timed" - no new geography, just a countdown added on top - which is why both beats
// are this one component: leave hasTimeLimit off for beat 2, switch it on for beat 3, same
// marker and arrow either way.
//
// No ingredients, no cooking, no new verb - just reading the direction cue and arriving (beat
// 2), then doing it again against a clock (beat 3).
//
// This is NOT a real Order. An Order with zero ingredient stops can never reach Carried -
// CargoSystem.TryCollectIngredient (the only thing that ever calls Order.MarkCarried outside a
// bay) only runs when there's something to collect - so a bare "just go here" order would sit
// stuck forever. Rather than force the real system into a shape it doesn't support, this is a
// standalone marker: one world beacon and one edge-of-screen arrow, built the same way
// (OrderWorldMaterial, the same generated arrow sprite) as the real OrderBeacon/DestinationMarker
// so it looks and reads exactly like the markers the player will rely on for the rest of the
// game, just pointed at one static point instead of the live order list.
//
// Put this on an empty GameObject in the tutorial scene and drag in the destination.
public class TutorialDestination : MonoBehaviour
{
    [Header("Wiring (found automatically if left empty)")]
    [Tooltip("The car. Found automatically: the object with ZoneDetection.")]
    [SerializeField] private Transform car;
    [SerializeField] private Camera targetCamera;

    [Header("Target")]
    [Tooltip("Where the player needs to drive. An empty GameObject placed in the level, off-screen from the start position - see Ako's note: if it's visible, the beat teaches nothing.")]
    [SerializeField] private Transform destination;
    [SerializeField] private float arrivalRadius = 6f;

    [Tooltip("Matches OrderPalette.Deliver, since arriving is the same 'green, you're done here' idea as a delivery.")]
    [SerializeField] private Color markerColour = new Color(0.20f, 0.85f, 0.35f, 1f);

    [Header("On-screen arrow")]
    [SerializeField] private float markerSize = 96f;
    [SerializeField] private float edgeMargin = 60f;

    [Header("Beat 3 only: time limit")]
    [Tooltip("Off for beat 2 (Arrive): no clock, just get there. On for beat 3 (Clock): same " +
             "route, now with a countdown, so the player feels the time pressure a real order " +
             "has before any food or money is involved.")]
    [SerializeField] private bool hasTimeLimit = false;
    [SerializeField] private float timeLimitSeconds = 30f;
    [Tooltip("Optional. A TMP text to show the countdown on, e.g. \"18s\".")]
    [SerializeField] private TextMeshProUGUI timeText;

    /// <summary>Fires once, the moment the car enters arrivalRadius of the destination.</summary>
    public event System.Action Arrived;

    /// <summary>Beat 3 only: fires once if hasTimeLimit is on and the clock runs out before
    /// Arrived does. Wire this to whatever should happen on a miss - most simply, call
    /// ResetTimer() and let them try again.</summary>
    public event System.Action TimedOut;

    private bool _arrived;
    private bool _timedOut;
    private float _timeRemaining;
    private GameObject _worldBeacon;
    private MeshRenderer _beamRenderer;
    private MaterialPropertyBlock _block;
    private RectTransform _arrowRoot;
    private Image _arrowImage;

    private void Awake()
    {
        if (car == null)
        {
            ZoneDetection z = FindAnyObjectByType<ZoneDetection>();
            if (z != null) car = z.transform;
        }
        if (targetCamera == null) targetCamera = Camera.main;

        if (car == null || targetCamera == null || destination == null)
        {
            Debug.LogError("[TutorialDestination] Needs car (or a ZoneDetection in the scene), " +
                            "camera and destination all assigned.", this);
            enabled = false;
            return;
        }

        BuildWorldBeacon();
        BuildArrow();

        _timeRemaining = timeLimitSeconds;
        UpdateTimeText();
    }

    private void Update()
    {
        if (_arrived || _timedOut) return;

        UpdateArrow();

        if (hasTimeLimit)
        {
            _timeRemaining -= Time.deltaTime;
            UpdateTimeText();
            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                _timedOut = true;
                TimedOut?.Invoke();
                return;
            }
        }

        Vector3 offset = destination.position - car.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= arrivalRadius * arrivalRadius)
        {
            _arrived = true;
            _worldBeacon.SetActive(false);
            _arrowRoot.gameObject.SetActive(false);
            Arrived?.Invoke();
        }
    }

    /// <summary>Beat 3 only: starts the countdown over and re-shows the marker, for another
    /// attempt after a TimedOut.</summary>
    public void ResetTimer()
    {
        _timedOut = false;
        _timeRemaining = timeLimitSeconds;
        UpdateTimeText();
        if (!_arrived)
        {
            _worldBeacon.SetActive(true);
            _arrowRoot.gameObject.SetActive(true);
        }
    }

    private void UpdateTimeText()
    {
        if (timeText == null || !hasTimeLimit) return;
        timeText.text = Mathf.CeilToInt(Mathf.Max(0f, _timeRemaining)) + "s";
    }

    // ---- world beacon, same approach as OrderBeacon's beam ----------------------------------

    private void BuildWorldBeacon()
    {
        _worldBeacon = new GameObject("Tutorial Destination Beacon");
        _worldBeacon.transform.SetParent(transform, false);
        _worldBeacon.transform.position = destination.position;

        GameObject beamGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beamGO.name = "Beam";
        Destroy(beamGO.GetComponent<Collider>());
        beamGO.transform.SetParent(_worldBeacon.transform, false);
        beamGO.transform.localPosition = new Vector3(0f, 7f, 0f);
        beamGO.transform.localScale = new Vector3(0.55f, 7f, 0.55f);

        _beamRenderer = beamGO.GetComponent<MeshRenderer>();
        _beamRenderer.sharedMaterial = OrderWorldMaterial.Get();
        _beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _beamRenderer.receiveShadows = false;

        _block = new MaterialPropertyBlock();
        OrderWorldMaterial.SetColour(_block, markerColour);
        _beamRenderer.SetPropertyBlock(_block);
    }

    // ---- on-screen edge arrow, a trimmed one-target version of DestinationMarker's ----------

    private void BuildArrow()
    {
        var canvasGO = new GameObject("Tutorial Destination Canvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(canvasGO.transform, false);
        _arrowRoot = arrowGO.AddComponent<RectTransform>();
        _arrowRoot.anchorMin = _arrowRoot.anchorMax = Vector2.zero;
        _arrowRoot.pivot = new Vector2(0.5f, 0.5f);
        _arrowRoot.sizeDelta = new Vector2(markerSize, markerSize);

        _arrowImage = arrowGO.AddComponent<Image>();
        _arrowImage.sprite = ArrowSprite();
        _arrowImage.color = markerColour;
        _arrowImage.raycastTarget = false;
    }

    private void UpdateArrow()
    {
        Vector3 toTarget = destination.position - targetCamera.transform.position;
        Vector3 localDir = targetCamera.transform.InverseTransformDirection(toTarget);
        bool inFront = localDir.z > 0.01f;
        Vector3 screenPoint = inFront ? targetCamera.WorldToScreenPoint(destination.position) : Vector3.zero;

        float minX = edgeMargin, maxX = Screen.width - edgeMargin;
        float minY = edgeMargin, maxY = Screen.height - edgeMargin;

        bool insideArea = inFront && screenPoint.x >= minX && screenPoint.x <= maxX
                                   && screenPoint.y >= minY && screenPoint.y <= maxY;

        if (insideArea)
        {
            _arrowRoot.anchoredPosition = new Vector2(screenPoint.x, screenPoint.y + markerSize * 0.5f);
            _arrowImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        }
        else
        {
            // Compass bearing against the camera's flattened forward/right, same trick
            // DestinationMarker uses, so the two feel identical to drive by.
            Vector3 flatForward = targetCamera.transform.forward; flatForward.y = 0f;
            Vector3 flatRight = targetCamera.transform.right; flatRight.y = 0f;

            Vector2 dir = new Vector2(
                Vector3.Dot(toTarget, flatRight.normalized),
                Vector3.Dot(toTarget, flatForward.normalized));
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
            dir.Normalize();

            Vector2 centre = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            float halfW = (maxX - minX) * 0.5f, halfH = (maxY - minY) * 0.5f;
            float scaleX = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            float scaleY = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
            _arrowRoot.anchoredPosition = centre + dir * Mathf.Min(scaleX, scaleY);

            float rot = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _arrowImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
        }
    }

    private static Sprite _cachedArrow;

    /// <summary>The exact same generated triangle DestinationMarker uses, so the two arrows
    /// are pixel-identical rather than two different shapes that happen to mean the same thing.</summary>
    private static Sprite ArrowSprite()
    {
        if (_cachedArrow != null) return _cachedArrow;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        int centre = size / 2;

        for (int y = 0; y < size; y++)
        {
            float t = y / (float)(size - 1);
            float halfWidthAtRow = (1f - t) * (size * 0.5f);
            for (int x = 0; x < size; x++)
            {
                bool inside = Mathf.Abs(x - centre) <= halfWidthAtRow;
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        _cachedArrow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _cachedArrow;
    }
}
