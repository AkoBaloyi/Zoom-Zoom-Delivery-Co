using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZoomZoom.Orders.UI;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Points at where the player is supposed to be going.
    ///
    /// WHY THIS EXISTS SEPARATELY FROM OrderMarkers
    /// OrderMarkers paints rings on the ground at the pickup and drop-off points. A ring is perfect
    /// once you can see it and useless before that, and a marker you cannot see tells you nothing at
    /// all. Nothing in the game currently says "turn around", which is why the orders were legible in
    /// the list and invisible in the world.
    ///
    /// So this is the screen-space half: an arrow that is always on screen, whether or not the target
    /// is. World-space markers and screen-space guidance are genuinely different jobs, and keeping them
    /// in separate files means the ground rings can be replaced by real art, or this can be replaced by
    /// the real HUD, without either touching the other.
    ///
    /// WHY AN ARROW, AND WHY IT DOES NOT ROUTE
    /// A study comparing minimap, arrow and compass guidance during movement found arrows performed
    /// best, minimaps middling and compasses worst, because a moving player needs something readable at
    /// a glance rather than something to be interpreted. Crazy Taxi, the closest thing to a reference
    /// for this game, uses one large arrow and no minimap at all, and it deliberately points at the
    /// destination rather than along the road.
    ///
    /// That last part is a design decision and not a shortcut. Being told the direction and having to
    /// work out the route yourself is the skill. An arrow that solved the route would remove the only
    /// thinking the navigation asks for, and this is meant to be easy to understand and hard to master.
    ///
    /// A minimap is still worth adding later, for choosing WHICH order to run rather than how to reach
    /// one. That is a planning question and a minimap is the right tool for it. It is deliberately not
    /// here yet, because how many orders can be live at once is still an open design question and the
    /// answer decides whether a minimap is essential or decoration.
    ///
    /// Reads only. Nothing here can change an order.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderCompass : MonoBehaviour
    {
        /// <summary>
        /// Attaches to the order system at scene load, for the same reason OrderMarkers does: a
        /// component nobody adds does nothing, and that failure has no symptom.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToOrderSystem()
        {
            OrderManager manager = Object.FindAnyObjectByType<OrderManager>();
            if (manager == null) return;
            if (manager.GetComponent<OrderCompass>() != null) return;

            // Stand down if the real HUD is present.
            //
            // This was written as a development view while there was no UI at all, and it says so at
            // the top of the file. DestinationMarker now does the same job through a proper Canvas, so
            // attaching anyway would put two sets of arrows on screen pointing at the same places,
            // which is worse than either alone and reads as a bug rather than as two systems.
            //
            // Checked by type rather than by a serialised toggle so nobody has to remember to switch
            // this off, and so it comes back on its own if the real marker is ever removed.
            if (Object.FindAnyObjectByType<DestinationMarker>() != null)
            {
                Debug.Log(
                    "[OrderCompass] DestinationMarker is in the scene, so the development compass is " +
                    "standing down to avoid drawing a second set of arrows. Delete or disable " +
                    "DestinationMarker if you want this one back.");
                return;
            }

            manager.gameObject.AddComponent<OrderCompass>();

            Debug.Log(
                "[OrderCompass] Attached. The arrow points at the order you are working on. TAB, or " +
                "the D-pad, switches which order that is. F12 hides it.");
        }

        [Header("Wiring")]
        [Tooltip("The order system being read. Found automatically if left empty.")]
        [SerializeField] private OrderManager orderManager;

        [Tooltip("Camera the arrow is drawn against. Uses the main camera if left empty.")]
        [SerializeField] private Camera viewCamera;

        [Header("Display")]
        [SerializeField] private bool show = true;

        [Tooltip("Key that hides the arrow. F1 to F9 belong to the vehicle lab tools, F10 is the " +
                 "telemetry overlay and F11 is the order marker list, so this takes F12.")]
        [SerializeField] private Key toggleKey = Key.F12;

        [Tooltip("Key that switches which order the arrow is following.")]
        [SerializeField] private Key cycleKey = Key.Tab;

        [Tooltip("Show a smaller, dimmer arrow for every other live order as well. Off by default: " +
                 "six arrows at once is clutter, and the point of a focus is to have one thing to do.")]
        [SerializeField] private bool showUnfocusedOrders = false;

        [Header("Look")]
        [Tooltip("Size of the focused arrow in pixels.")]
        [SerializeField] private float arrowSize = 64f;

        [Tooltip("How far in from the screen edge an off-screen arrow sits, pixels. Keeps it clear of " +
                 "the very corner where it is easy to miss.")]
        [SerializeField] private float edgeInset = 74f;

        [Tooltip("Colour when heading to a pickup.")]
        [SerializeField] private Color pickupColour = new Color(0.25f, 0.8f, 1f, 0.95f);

        [Tooltip("Colour when carrying cargo and heading to the drop-off. This is the one the player " +
                 "is actually being paid for, so it is the one that should read as urgent-but-good.")]
        [SerializeField] private Color dropOffColour = new Color(0.35f, 1f, 0.45f, 0.95f);

        [Tooltip("Colour the arrow shifts towards as the order runs out of time.")]
        [SerializeField] private Color urgentColour = new Color(1f, 0.3f, 0.2f, 1f);

        [Tooltip("Seconds remaining at which the arrow is fully urgent.")]
        [SerializeField] private float urgentBelowSeconds = 8f;

        // ---------------- state ----------------

        private Transform _player;
        private Texture2D _arrow;
        private GUIStyle _label;
        private GUIStyle _labelShadow;

        // Which order the arrow follows. Held as an ID rather than a list index, because the active
        // list is reordered as orders resolve and an index would silently start pointing at a
        // different order than the player chose.
        private int _focusedOrderId = -1;

        private void Awake()
        {
            if (orderManager == null) orderManager = GetComponent<OrderManager>();
            if (orderManager == null) orderManager = Object.FindAnyObjectByType<OrderManager>();

            if (orderManager == null)
            {
                Debug.LogWarning("[OrderCompass] No OrderManager in the scene. Switching off.", this);
                enabled = false;
                return;
            }

            // By tag, so the order system still needs no reference to the vehicle system.
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _player = player.transform;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;

            // Read straight off the devices rather than through the action map. These are HUD controls
            // and the map is the VEHICLE map: putting them there would mean the orders system owned
            // bindings in the car's control scheme. When the real HUD exists it should own an action
            // map of its own and this should move into it.
            if (keyboard != null)
            {
                if (keyboard[toggleKey].wasPressedThisFrame) show = !show;
                if (keyboard[cycleKey].wasPressedThisFrame) CycleFocus();
            }

            if (pad != null && pad.dpad.right.wasPressedThisFrame) CycleFocus();
        }

        // ==================================================================
        // FOCUS
        // ==================================================================

        /// <summary>
        /// The order the arrow is following.
        ///
        /// Chosen rather than clicked. A list you click is unusable at 30 metres a second, which is
        /// most of the time in this game, so the focus falls back to the only sensible default: what
        /// you are carrying, because you cannot pick up anything else until it is delivered, and
        /// otherwise whatever is closest to expiring.
        /// </summary>
        private Order ResolveFocused()
        {
            IReadOnlyList<Order> orders = orderManager.ActiveOrders;
            if (orders.Count == 0) return null;

            // Carrying something? That is the job, whatever the player last pressed Tab on.
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].IsTerminal) continue;
                if (orders[i].State == OrderState.Carried) return orders[i];
            }

            // Still following a live choice?
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].IsTerminal) continue;
                if (orders[i].Id == _focusedOrderId) return orders[i];
            }

            // The chosen order is gone, delivered or expired. Fall back to the most urgent rather than
            // to the first in the list, because the first is an arbitrary spawn order and the most
            // urgent is the one about to cost the player something.
            Order best = null;
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].IsTerminal) continue;
                if (best == null || orders[i].TimeRemaining < best.TimeRemaining) best = orders[i];
            }

            if (best != null) _focusedOrderId = best.Id;
            return best;
        }

        private void CycleFocus()
        {
            IReadOnlyList<Order> orders = orderManager.ActiveOrders;

            // Build the live set in list order, then step to the one after the current focus. Wrapping
            // through a filtered copy keeps terminal orders out of the rotation, so Tab never lands on
            // something already delivered.
            int firstLive = -1, next = -1;
            bool seenCurrent = false;

            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].IsTerminal) continue;

                if (firstLive < 0) firstLive = orders[i].Id;

                if (seenCurrent) { next = orders[i].Id; break; }
                if (orders[i].Id == _focusedOrderId) seenCurrent = true;
            }

            _focusedOrderId = next >= 0 ? next : firstLive;
        }

        // ==================================================================
        // DRAWING
        // ==================================================================

        private void OnGUI()
        {
            if (!show || orderManager == null) return;

            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) return;

            BuildResources();

            Order focused = ResolveFocused();

            if (showUnfocusedOrders)
            {
                IReadOnlyList<Order> orders = orderManager.ActiveOrders;
                for (int i = 0; i < orders.Count; i++)
                {
                    if (orders[i].IsTerminal || orders[i] == focused) continue;
                    DrawIndicator(cam, orders[i], 0.55f, false);
                }
            }

            if (focused != null) DrawIndicator(cam, focused, 1f, true);
        }

        /// <summary>
        /// Draws one order's indicator, on screen or clamped to the edge.
        ///
        /// THE CASE THAT MATTERS IS THE TARGET BEING BEHIND THE CAMERA
        /// WorldToScreenPoint is only meaningful in front of the camera. Behind it, the projection
        /// mirrors, and code that trusts the result puts the arrow on the wrong side of the screen and
        /// sends the player further away. So the direction is taken in camera space instead, where
        /// negative z simply means behind and the x and y still give a usable bearing.
        /// </summary>
        private void DrawIndicator(Camera cam, Order order, float strength, bool focused)
        {
            bool carrying = order.State == OrderState.Carried || order.State == OrderState.Collected;
            Vector3 target = carrying ? order.DropOffPoint : order.PickupPoint;

            Vector3 local = cam.transform.InverseTransformPoint(target);
            bool inFront = local.z > 0.01f;

            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Rect safe = new Rect(edgeInset, edgeInset,
                Screen.width - edgeInset * 2f, Screen.height - edgeInset * 2f);

            Vector2 point;
            bool offScreen;

            if (inFront)
            {
                Vector3 sp = cam.WorldToScreenPoint(target);
                point = new Vector2(sp.x, Screen.height - sp.y);   // GUI space has y downwards
                offScreen = !safe.Contains(point);
            }
            else
            {
                // Behind: there is no valid projection, so the bearing comes from camera space. y is
                // negated because a target above the camera should push the arrow UP the screen, and
                // GUI y grows downwards.
                Vector2 bearing = new Vector2(local.x, -local.y);
                if (bearing.sqrMagnitude < 0.0001f) bearing = Vector2.up;

                point = centre + bearing.normalized * (Screen.height * 2f);
                offScreen = true;
            }

            float urgency = 1f - Mathf.Clamp01(order.TimeRemaining / Mathf.Max(0.01f, urgentBelowSeconds));
            Color colour = Color.Lerp(carrying ? dropOffColour : pickupColour, urgentColour, urgency);
            colour.a *= strength;

            float size = arrowSize * (focused ? 1f : 0.6f);
            float distance = _player != null
                ? Vector3.Distance(_player.position, target)
                : Vector3.Distance(cam.transform.position, target);

            if (offScreen)
            {
                Vector2 edge = ClampToRect(centre, point, safe);
                Vector2 direction = (point - centre).sqrMagnitude > 0.0001f
                    ? (point - centre).normalized
                    : Vector2.up;

                // The texture points up, and GUI rotation is clockwise, so this maps a direction to the
                // angle that turns the arrow onto it.
                float angle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;

                DrawRotated(_arrow, edge, size, angle, colour);

                if (focused) DrawLabel(edge + Vector2.up * (size * 0.62f), $"{distance:0} m", colour);
            }
            else
            {
                // On screen: a downward chevron sitting above the target, so it points AT the ring
                // rather than floating beside it.
                Vector2 above = point + Vector2.up * (size * 0.75f);
                DrawRotated(_arrow, above, size * 0.72f, 180f, colour);

                if (focused) DrawLabel(above + Vector2.up * (size * 0.55f), $"{distance:0} m", colour);
            }
        }

        /// <summary>
        /// Where the ray from the screen centre towards a point leaves the safe rectangle.
        ///
        /// Solved per axis rather than by walking the ray, so it costs the same whether the target is
        /// just off screen or a kilometre behind.
        /// </summary>
        private static Vector2 ClampToRect(Vector2 centre, Vector2 towards, Rect rect)
        {
            Vector2 direction = towards - centre;
            if (direction.sqrMagnitude < 0.0001f) return centre;

            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;

            // How far along the ray each axis would hit its boundary. The smaller one is the edge the
            // ray actually crosses first.
            float scaleX = Mathf.Abs(direction.x) > 0.0001f
                ? halfWidth / Mathf.Abs(direction.x)
                : float.MaxValue;

            float scaleY = Mathf.Abs(direction.y) > 0.0001f
                ? halfHeight / Mathf.Abs(direction.y)
                : float.MaxValue;

            return centre + direction * Mathf.Min(scaleX, scaleY);
        }

        private static void DrawRotated(Texture2D texture, Vector2 centre, float size, float angle,
            Color colour)
        {
            if (texture == null) return;

            var rect = new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size);

            Matrix4x4 saved = GUI.matrix;
            Color savedColour = GUI.color;

            GUIUtility.RotateAroundPivot(angle, centre);
            GUI.color = colour;
            GUI.DrawTexture(rect, texture);

            GUI.color = savedColour;
            GUI.matrix = saved;
        }

        private void DrawLabel(Vector2 centre, string text, Color colour)
        {
            var rect = new Rect(centre.x - 60f, centre.y - 26f, 120f, 24f);

            _labelShadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f * colour.a);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _labelShadow);

            _label.normal.textColor = colour;
            GUI.Label(rect, text, _label);
        }

        // ==================================================================
        // RESOURCES
        // ==================================================================

        private void BuildResources()
        {
            if (_arrow == null) _arrow = BuildArrowTexture(64);

            if (_label != null) return;

            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };
            _label.font = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Courier New", "monospace" }, 15);

            _labelShadow = new GUIStyle(_label);
        }

        /// <summary>
        /// A triangle pointing up, with softened edges.
        ///
        /// WHY THIS IS GENERATED RATHER THAN DOWNLOADED
        /// The tyre smoke came from a public domain art pack, because a soft grey puff is a picture and
        /// hand-drawing one is wasted effort. A triangle is not a picture, it is three points, and
        /// carrying a PNG plus its import settings plus its licence note in order to draw one would be
        /// more work and more to go wrong, not less.
        ///
        /// The edge softening matters more than it sounds: the arrow is rotated to arbitrary angles, and
        /// a hard-edged triangle rotated 23 degrees has visibly stepped sides.
        /// </summary>
        private static Texture2D BuildArrowTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Order arrow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            // Normalised, y upwards, which is how Texture2D pixels are addressed.
            Vector2 apex = new Vector2(0.5f, 0.96f);
            Vector2 left = new Vector2(0.06f, 0.13f);
            Vector2 right = new Vector2(0.94f, 0.13f);

            float feather = 1.6f / size;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);

                    // Distance to the inside of all three edges. The smallest one decides the alpha, so
                    // the shape fades out over `feather` at every edge including the sharp tip.
                    float d = Mathf.Min(
                        EdgeDistance(p, apex, left),
                        Mathf.Min(EdgeDistance(p, left, right), EdgeDistance(p, right, apex)));

                    float alpha = Mathf.Clamp01(d / feather);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// Signed distance from a point to the line through a and b, positive on the inside of a
        /// clockwise-wound triangle.
        /// </summary>
        private static float EdgeDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            float length = edge.magnitude;
            if (length < 1e-6f) return 0f;

            // Two dimensional cross product, divided through by the edge length to turn an area into a
            // distance.
            return ((p.x - a.x) * edge.y - (p.y - a.y) * edge.x) / length;
        }

        private void OnDestroy()
        {
            if (_arrow != null) Destroy(_arrow);
        }
    }
}
