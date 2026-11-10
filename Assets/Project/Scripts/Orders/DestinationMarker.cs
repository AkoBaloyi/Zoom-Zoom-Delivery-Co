using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// The on screen arrows: one per order, pointing at where that order needs the car next
    /// (the nearest ingredient still needed, then the bay, then the customer). Hidden while the
    /// food cooks, because there is nothing to drive to yet.
    ///
    /// WHAT AN ARROW TELLS YOU AT A GLANCE
    ///   Whose it is: it is drawn in the order's own colour, the same as its notification on the
    ///   phone and its beacon in the world, and labelled with the order number.
    ///   What to get: while gathering, the ingredient type's icon sits above the label, matching
    ///   the chip on the order card and the sign on the shop.
    ///   When it expires: the label shows the seconds left, and near the end the arrow swells and
    ///   pulses faster (it never flashes; OrderAudio adds a ticking sound).
    ///   How far: closer targets get bigger arrows and further ones smaller, like the markers in
    ///   a racing game, on a scale that stays readable out to several hundred metres. The exact
    ///   distance is written underneath.
    ///   What matters most: the selected order (see OrderFocus) gets a full size, solid arrow
    ///   drawn on top; the others are smaller and faded.
    ///
    /// When the target is on screen the arrow sits over it. When it is not, the arrow sits on
    /// the edge of the play area like a compass: ahead is the top, behind is the bottom. The
    /// play area leaves out the phone on the left and the stats panel at the top.
    /// </summary>
    [DisallowMultipleComponent]
    public class DestinationMarker : MonoBehaviour
    {
        [Header("Wiring (found automatically if left empty)")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private OrderFocus focus;
        [Tooltip("Distance is measured from this. Found automatically: the car (the object with " +
                 "ZoneDetection), or the camera if there is no car.")]
        [SerializeField] private Transform distanceFrom;

        [Header("Markers")]
        [SerializeField] private float markerSize = 96f;
        [SerializeField] private float edgeMargin = 60f;
        [SerializeField] private int distanceFontSize = 22;
        [SerializeField] private int labelFontSize = 26;

        [Header("Keep clear (reference pixels, 1920x1080)")]
        [Tooltip("Width kept clear on the left for the order phone.")]
        [SerializeField] private float reservedLeft = 440f;
        [Tooltip("Height kept clear at the top for the stats panel.")]
        [SerializeField] private float reservedTop = 190f;

        [Header("Size by distance (closer is bigger)")]
        [Tooltip("Metres. At or closer than this, an arrow is at Close Scale.")]
        [SerializeField] private float closeDistance = 15f;
        [Tooltip("Metres. At or further than this, an arrow is at Farthest Scale.")]
        [SerializeField] private float farthestDistance = 600f;
        [SerializeField] private float closeScale = 1.5f;
        [SerializeField] private float farthestScale = 0.45f;

        [Header("Priority")]
        [Tooltip("Size of arrows for orders that are not selected, relative to the selected one.")]
        [SerializeField] private float unselectedScale = 0.75f;
        [Tooltip("Opacity of arrows for orders that are not selected.")]
        [SerializeField] private float unselectedAlpha = 0.6f;
        [Tooltip("Seconds left below which an arrow starts to pulse.")]
        [SerializeField] private float urgentBelowSeconds = 8f;

        [Header("Pulse")]
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseScaleAmount = 0.12f;

        private struct MarkerSlot
        {
            public RectTransform Root;
            public Image Arrow;
            public Text Distance;
            public Text Label;
            public Image Icon;
        }

        private readonly List<MarkerSlot> _slots = new List<MarkerSlot>();
        private Canvas _canvas;
        private RectTransform _canvasRoot;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (distanceFrom == null)
            {
                ZoneDetection car = FindAnyObjectByType<ZoneDetection>();
                if (car != null) distanceFrom = car.transform;
                else if (targetCamera != null) distanceFrom = targetCamera.transform;
            }

            if (orderManager == null || targetCamera == null)
            {
                Debug.LogError(
                    "[DestinationMarker] Needs an OrderManager and a Camera (Camera.main) in the " +
                    "scene. Switching off.", this);
                enabled = false;
                return;
            }

            BuildCanvas();
        }

        private void Start()
        {
            if (focus == null) focus = OrderFocus.Ensure(orderManager);
        }

        private void Update()
        {
            if (orderManager == null || targetCamera == null) return;

            // The Guidance setting can turn the arrows off (GPS route only).
            if (!GameSettings.ShowArrows)
            {
                foreach (MarkerSlot s in _slots) s.Root.gameObject.SetActive(false);
                return;
            }

            IReadOnlyList<Order> active = orderManager.ActiveOrders;
            Order selected = focus != null ? focus.Selected : null;
            int used = 0;
            int selectedSlot = -1;

            for (int i = 0; i < active.Count; i++)
            {
                Order order = active[i];
                if (!order.HasWorldTarget) continue; // cooking: nothing to drive to yet
                if (order.CurrentStep == OrderStep.Cook && !order.HasBay) continue;

                if (used >= _slots.Count) _slots.Add(BuildSlot(used));

                Vector3 target = order.TargetFrom(DistanceOrigin);
                float distance = Vector3.Distance(DistanceOrigin, target);
                bool isSelected = order == selected;

                Color colour = OrderPalette.OrderColour(order);
                if (!isSelected) colour.a = unselectedAlpha;

                // Near the end the arrow swells and pulses faster. It never flashes.
                float scale = ScaleForDistance(distance) * (isSelected ? 1f : unselectedScale)
                              * OrderPalette.UrgencyPulse(order, urgentBelowSeconds, Time.time);
                string label = $"#{order.Id} {StepWord(order, out IngredientType ingredient)}  {Mathf.CeilToInt(order.SecondsUntilLost)}s";

                PlaceMarker(_slots[used], target, colour, label, distance, scale);
                SetIcon(_slots[used], ingredient, colour.a);
                if (isSelected) selectedSlot = used;
                used++;
            }

            for (int i = used; i < _slots.Count; i++)
                _slots[i].Root.gameObject.SetActive(false);

            // The selected arrow is drawn on top of the others.
            if (selectedSlot >= 0) _slots[selectedSlot].Root.SetAsLastSibling();
        }

        private Vector3 DistanceOrigin =>
            distanceFrom != null ? distanceFrom.position : targetCamera.transform.position;

        /// <summary>The word after the order number: the ingredient to get, COOK, READY or DROP.</summary>
        private string StepWord(Order order, out IngredientType ingredient)
        {
            ingredient = null;
            switch (order.CurrentStep)
            {
                case OrderStep.Gather:
                    IngredientStop next = order.NearestStop(DistanceOrigin, out _);
                    if (next == null) return "";
                    ingredient = next.Ingredient;
                    return next.Name.ToUpper();
                case OrderStep.Cook:
                    return order.State == OrderState.Ready ? "READY" : "COOK";
                default:
                    return "DROP";
            }
        }

        /// <summary>Shows the ingredient's type icon above the label while gathering.</summary>
        private static void SetIcon(MarkerSlot slot, IngredientType ingredient, float alpha)
        {
            Sprite sprite = IngredientIcons.Icon(ingredient);
            bool show = sprite != null;
            slot.Icon.gameObject.SetActive(show);
            if (!show) return;

            slot.Icon.sprite = sprite;
            Color c = IngredientIcons.Colour(ingredient);
            slot.Icon.color = new Color(c.r, c.g, c.b, Mathf.Max(0.7f, alpha));
        }

        /// <summary>
        /// Close Scale up close, Farthest Scale far away. The blend follows the logarithm of the
        /// distance, so each doubling of distance shrinks the arrow by about the same step:
        /// 30 m, 60 m, 150 m, 300 m and 600 m all look different, instead of everything past
        /// 200 m looking the same size.
        /// </summary>
        private float ScaleForDistance(float distance)
        {
            float near = Mathf.Max(1f, closeDistance);
            float far = Mathf.Max(near + 1f, farthestDistance);
            float d = Mathf.Clamp(distance, near, far);
            float t = Mathf.Log(d / near) / Mathf.Log(far / near);
            return Mathf.Lerp(closeScale, farthestScale, t);
        }

        private void PlaceMarker(MarkerSlot slot, Vector3 target, Color colour, string label,
            float distance, float sizeScale)
        {
            slot.Root.gameObject.SetActive(true);
            slot.Root.localScale = Vector3.one * sizeScale;

            Vector3 toTarget = target - targetCamera.transform.position;
            Vector3 localDir = targetCamera.transform.InverseTransformDirection(toTarget);
            bool inFront = localDir.z > 0.01f;
            Vector3 screenPoint = inFront ? targetCamera.WorldToScreenPoint(target) : Vector3.zero;

            float scaleFactor = _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;

            // The area arrows may use: inside the screen, clear of the phone and the stats panel.
            float margin = edgeMargin * Mathf.Max(1f, sizeScale) * scaleFactor;
            float minX = reservedLeft * scaleFactor + margin * 0.5f;
            float maxX = Screen.width - margin;
            float minY = margin;
            float maxY = Screen.height - reservedTop * scaleFactor - margin * 0.5f;
            if (maxX <= minX) maxX = minX + 1f;
            if (maxY <= minY) maxY = minY + 1f;

            bool insideArea = inFront
                              && screenPoint.x >= minX && screenPoint.x <= maxX
                              && screenPoint.y >= minY && screenPoint.y <= maxY;

            Vector2 finalScreenPos;
            float rotationDegrees = 0f;
            bool rotated = false;

            if (insideArea)
            {
                // Lift the arrow so its tip, pointing down, touches the target.
                finalScreenPos = new Vector2(screenPoint.x, screenPoint.y + markerSize * 0.5f * sizeScale * scaleFactor);
            }
            else
            {
                // A flat compass bearing: ahead is the top of the area, behind is the bottom, left
                // and right are the sides. Using the camera's own up axis instead sends anything
                // behind the car to the top edge, because a chase camera tilts down.
                Vector3 flatForward = targetCamera.transform.forward;
                flatForward.y = 0f;
                Vector3 flatRight = targetCamera.transform.right;
                flatRight.y = 0f;

                Vector2 dir = new Vector2(
                    Vector3.Dot(toTarget, flatRight.normalized),
                    Vector3.Dot(toTarget, flatForward.normalized));
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
                dir.Normalize();

                Vector2 centre = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
                float halfW = (maxX - minX) * 0.5f;
                float halfH = (maxY - minY) * 0.5f;
                float scaleX = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
                float scaleY = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
                finalScreenPos = centre + dir * Mathf.Min(scaleX, scaleY);

                rotationDegrees = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                rotated = true;
            }

            slot.Root.anchoredPosition = finalScreenPos / scaleFactor;

            // On screen the arrow points down at the target; off screen it points the way to turn.
            slot.Arrow.rectTransform.localRotation = rotated
                ? Quaternion.Euler(0f, 0f, rotationDegrees)
                : Quaternion.Euler(0f, 0f, 180f);

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScaleAmount;
            slot.Arrow.rectTransform.localScale = Vector3.one * pulse;

            slot.Arrow.color = colour;
            slot.Distance.color = colour;
            slot.Distance.text = $"{distance:0}m";
            slot.Label.text = label;
            slot.Label.color = colour;
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("DestinationMarker Canvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRoot = canvasGO.GetComponent<RectTransform>();

            for (int i = 0; i < OrderTuning.MaxOrders; i++) _slots.Add(BuildSlot(i));
        }

        private MarkerSlot BuildSlot(int index)
        {
            Sprite arrowSprite = GetOrCreateArrowSprite();

            var rootGO = new GameObject($"Marker {index}");
            rootGO.transform.SetParent(_canvasRoot, false);

            RectTransform root = rootGO.AddComponent<RectTransform>();
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(markerSize, markerSize);

            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(rootGO.transform, false);
            Image arrow = arrowGO.AddComponent<Image>();
            arrow.sprite = arrowSprite;
            arrow.raycastTarget = false;
            RectTransform arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = arrowRect.anchorMax = arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(markerSize, markerSize);
            arrowRect.anchoredPosition = Vector2.zero;

            Text distance = MakeLabel(rootGO.transform, "Distance", distanceFontSize, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -markerSize * 0.5f - 4f));

            Text label = MakeLabel(rootGO.transform, "Order", labelFontSize, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, markerSize * 0.5f + 4f));

            // Ingredient icon above the label, in the ingredient type's colour.
            var iconGO = new GameObject("Ingredient icon");
            iconGO.transform.SetParent(rootGO.transform, false);
            Image icon = iconGO.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            iconRect.anchoredPosition = new Vector2(0f, markerSize * 0.5f + 4f + labelFontSize + 8f);
            var iconShadow = iconGO.AddComponent<Shadow>();
            iconShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            iconShadow.effectDistance = new Vector2(2f, -2f);
            iconGO.SetActive(false);

            rootGO.SetActive(false);
            return new MarkerSlot { Root = root, Arrow = arrow, Distance = distance, Label = label, Icon = icon };
        }

        private static Text MakeLabel(Transform parent, string name, int size, TextAnchor alignment,
            Vector2 pivot, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(220f, 32f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        /// <summary>
        /// A small upward pointing triangle, generated in code rather than imported as art: it is a
        /// handful of numbers, not a picture, so it is crisp at any size and needs no asset.
        /// </summary>
        private static Sprite _cachedArrowSprite;

        private static Sprite GetOrCreateArrowSprite()
        {
            if (_cachedArrowSprite != null) return _cachedArrowSprite;

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
                float t = y / (float)(size - 1); // 0 at bottom, 1 at tip
                float halfWidthAtRow = (1f - t) * (size * 0.5f);

                for (int x = 0; x < size; x++)
                {
                    bool inside = Mathf.Abs(x - centre) <= halfWidthAtRow;
                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            _cachedArrowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _cachedArrowSprite;
        }
    }
}
