using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// The one piece of Task 11 that didn't exist yet: an on-screen marker telling the player
    /// where to actually drive. One arrow per active order, pointing at its pickup point while
    /// Active, switching to its drop-off point once Carried. When the target is on screen, the
    /// arrow sits over it with a distance readout. When it isn't, whether behind the camera or
    /// just off to the side, the arrow clamps to the screen edge and rotates to keep pointing the
    /// right way, the same convention as a race game's off-track waypoint arrow.
    ///
    /// WHY THIS EXISTS SEPARATELY FROM ORDERHUD
    /// OrderHUD is a list, useful for checking status at a glance. This is spatial, useful while
    /// actually driving, and it needs camera projection math OrderHUD has no reason to carry.
    /// Keeping them separate means either can be turned off without losing the other.
    ///
    /// WHY THIS IS DIFFERENT FROM ako'S OrderMarkers
    /// OrderMarkers draws world-space rings on the ground, and is explicitly a development tool
    /// meant to be switched off once real UI exists, by his own comment in that file. This is
    /// that real UI: screen-space, works even when a target is far away or genuinely out of view,
    /// and does not disappear if the world-space ring's draw distance is exceeded.
    /// </summary>
    [DisallowMultipleComponent]
    public class DestinationMarker : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private Camera targetCamera;

        [Header("Markers")]
        [SerializeField] private int markerPoolSize = 6;
        [SerializeField] private float markerSize = 64f;
        [SerializeField] private float edgeMargin = 60f;
        [SerializeField] private int distanceFontSize = 22;

        [Header("Colours")]
        [SerializeField] private Color pickupColour = new Color(0.3f, 0.65f, 1f, 0.95f);
        [SerializeField] private Color dropOffColour = new Color(0.35f, 0.95f, 0.45f, 0.95f);
        [SerializeField] private Color urgentColour = new Color(1f, 0.25f, 0.2f, 0.95f);
        [SerializeField] private float urgentBelowSeconds = 8f;

        private struct MarkerSlot
        {
            public RectTransform Root;
            public Image Arrow;
            public Text Distance;
        }

        private readonly List<MarkerSlot> _slots = new List<MarkerSlot>();
        private RectTransform _canvasRect;
        private Canvas _canvas;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (targetCamera == null) targetCamera = Camera.main;

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

        private void Update()
        {
            if (orderManager == null || targetCamera == null) return;

            IReadOnlyList<Order> active = orderManager.ActiveOrders;
            int used = 0;

            for (int i = 0; i < active.Count && used < _slots.Count; i++)
            {
                Order order = active[i];
                if (order.IsTerminal) continue;

                bool goingToDropOff = order.State == OrderState.Carried
                                     || order.State == OrderState.Collected;

                Vector3 target = goingToDropOff ? order.DropOffPoint : order.PickupPoint;
                Color baseColour = goingToDropOff ? dropOffColour : pickupColour;

                float urgency = 1f - Mathf.Clamp01(order.TimeRemaining / Mathf.Max(0.01f, urgentBelowSeconds));
                Color colour = Color.Lerp(baseColour, urgentColour, urgency);

                PlaceMarker(_slots[used], target, colour, order.TimeRemaining);
                used++;
            }

            for (int i = used; i < _slots.Count; i++)
                _slots[i].Root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Projects target into screen space, then either sits the marker directly over it (on
        /// screen) or clamps it to the screen edge with a rotated arrow (off screen, including
        /// straight behind the camera, which needs its own flip before the normal clamp math works).
        /// </summary>
        private void PlaceMarker(MarkerSlot slot, Vector3 target, Color colour, float timeRemaining)
        {
            slot.Root.gameObject.SetActive(true);

            Vector3 screenPoint = targetCamera.WorldToScreenPoint(target);
            bool behindCamera = screenPoint.z < 0f;

            if (behindCamera)
            {
                // A point behind the camera projects to the correct screen position mirrored
                // through the centre; flipping it here is what stops the arrow from briefly
                // pointing the wrong way as a target passes behind the player.
                screenPoint.x = Screen.width - screenPoint.x;
                screenPoint.y = Screen.height - screenPoint.y;
            }

            float halfW = Screen.width * 0.5f;
            float halfH = Screen.height * 0.5f;

            bool onScreen = !behindCamera
                             && screenPoint.x >= 0f && screenPoint.x <= Screen.width
                             && screenPoint.y >= 0f && screenPoint.y <= Screen.height;

            Vector2 fromCentre = new Vector2(screenPoint.x - halfW, screenPoint.y - halfH);

            Vector2 finalScreenPos;
            float rotationDegrees;
            bool showArrowRotated;

            if (onScreen)
            {
                finalScreenPos = new Vector2(screenPoint.x, screenPoint.y);
                rotationDegrees = 0f;
                showArrowRotated = false;
            }
            else
            {
                Vector2 dir = fromCentre.sqrMagnitude > 0.001f ? fromCentre.normalized : Vector2.up;

                float availableW = halfW - edgeMargin;
                float availableH = halfH - edgeMargin;

                float scaleX = Mathf.Abs(dir.x) > 0.0001f ? availableW / Mathf.Abs(dir.x) : float.MaxValue;
                float scaleY = Mathf.Abs(dir.y) > 0.0001f ? availableH / Mathf.Abs(dir.y) : float.MaxValue;
                float scale = Mathf.Min(scaleX, scaleY);

                Vector2 clamped = dir * scale;
                finalScreenPos = new Vector2(halfW + clamped.x, halfH + clamped.y);

                // Sprite's tip points up (0 degrees) before rotation, atan2 measures from the
                // positive X axis, so subtracting 90 aligns "up" with the actual direction.
                rotationDegrees = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                showArrowRotated = true;
            }

            float scaleFactor = _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;
            slot.Root.anchoredPosition = finalScreenPos / scaleFactor;
            slot.Arrow.rectTransform.localRotation = showArrowRotated
                ? Quaternion.Euler(0f, 0f, rotationDegrees)
                : Quaternion.identity;

            slot.Arrow.color = colour;
            slot.Distance.color = colour;

            float distance = Vector3.Distance(targetCamera.transform.position, target);
            slot.Distance.text = onScreen ? $"{distance:0}m" : $"{distance:0}m \u2192";
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

            canvasGO.AddComponent<GraphicRaycaster>();

            _canvasRect = canvasGO.GetComponent<RectTransform>();

            Sprite arrowSprite = GetOrCreateArrowSprite();

            for (int i = 0; i < Mathf.Max(1, markerPoolSize); i++)
            {
                var rootGO = new GameObject($"Marker {i}");
                rootGO.transform.SetParent(canvasGO.transform, false);

                RectTransform root = rootGO.AddComponent<RectTransform>();
                root.anchorMin = new Vector2(0f, 0f);
                root.anchorMax = new Vector2(0f, 0f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.sizeDelta = new Vector2(markerSize, markerSize);

                var arrowGO = new GameObject("Arrow");
                arrowGO.transform.SetParent(rootGO.transform, false);
                Image arrow = arrowGO.AddComponent<Image>();
                arrow.sprite = arrowSprite;
                RectTransform arrowRect = arrow.rectTransform;
                arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
                arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
                arrowRect.pivot = new Vector2(0.5f, 0.5f);
                arrowRect.sizeDelta = new Vector2(markerSize, markerSize);
                arrowRect.anchoredPosition = Vector2.zero;

                var labelGO = new GameObject("Distance");
                labelGO.transform.SetParent(rootGO.transform, false);
                Text label = labelGO.AddComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = distanceFontSize;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.UpperCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0.5f, 0f);
                labelRect.anchorMax = new Vector2(0.5f, 0f);
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.anchoredPosition = new Vector2(0f, -markerSize * 0.5f - 4f);
                labelRect.sizeDelta = new Vector2(140f, 28f);

                rootGO.SetActive(false);

                _slots.Add(new MarkerSlot { Root = root, Arrow = arrow, Distance = label });
            }
        }

        /// <summary>
        /// A small upward-pointing triangle, generated in code rather than imported as art, same
        /// reasoning as the ring mesh in OrderMarkers: it is a handful of numbers, not a picture,
        /// so it is crisp at any size and needs no asset dependency.
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

            _cachedArrowSprite = Sprite.Create(
                tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _cachedArrowSprite;
        }
    }
}