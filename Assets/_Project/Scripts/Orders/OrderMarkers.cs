using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Puts the order system on screen.
    ///
    /// WHY THIS IS A SEPARATE FILE AND NOT AN EDIT TO OrderManager
    /// The order logic already works. Orders spawn, time down and resolve, and the console says so.
    /// The problem was that none of it was visible: the only visual in the whole system was an
    /// OnDrawGizmosSelected, which draws nothing unless you happen to have the object selected in the
    /// hierarchy and nothing at all in a build. A working system nobody can see reads as a broken one.
    ///
    /// So this reads OrderManager and draws what it finds. It never calls a method that changes an
    /// order, which means it cannot break the delivery rules and can be deleted without consequence.
    /// It is also a new file rather than a change to an existing one, so it cannot cause a merge
    /// conflict with whoever owns the order system.
    ///
    /// This is a development view, not the finished HUD. The real order UI belongs to whoever owns the
    /// UI, and this should be switched off once that exists.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderMarkers : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The order system being watched. Found on this object, or anywhere in the scene, if " +
                 "left empty.")]
        [SerializeField] private OrderManager orderManager;

        [Header("Display")]
        [Tooltip("Show the world markers and the order list. Toggled at runtime with the key below.")]
        [SerializeField] private bool show = true;

        [Tooltip("Key that shows and hides the order markers.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;

        [Tooltip("Draw a beacon at each pickup and drop-off point.")]
        [SerializeField] private bool worldMarkers = true;

        [Tooltip("List the live orders in the top right corner.")]
        [SerializeField] private bool orderList = true;

        [Header("Beacons")]
        [Tooltip("How tall a beacon stands, metres. Tall enough to be seen over the lab's marker posts.")]
        [SerializeField] private float beaconHeight = 12f;

        [Tooltip("How wide a beacon is, metres.")]
        [SerializeField] private float beaconWidth = 1.6f;

        [Tooltip("Colour of a pickup the player has not collected yet.")]
        [SerializeField] private Color pickupColour = new Color(0.2f, 0.75f, 1f, 0.55f);

        [Tooltip("Colour of the drop-off for cargo already aboard. This is the one the player is " +
                 "actually driving towards, so it is the one that should stand out.")]
        [SerializeField] private Color dropOffColour = new Color(0.3f, 1f, 0.4f, 0.7f);

        [Tooltip("Colour a beacon shifts towards as its order runs out of time.")]
        [SerializeField] private Color urgentColour = new Color(1f, 0.25f, 0.15f, 0.85f);

        [Tooltip("Seconds remaining at which a beacon is fully urgent.")]
        [SerializeField] private float urgentBelowSeconds = 8f;

        // ---------------- state ----------------

        // Pooled, because orders come and go constantly and creating a beacon per order per frame
        // would churn the garbage collector for no reason.
        private readonly List<GameObject> _beaconPool = new List<GameObject>();
        private Material _beaconMaterial;
        private Transform _beaconRoot;
        private GUIStyle _style;
        private GUIStyle _shadow;

        private void Awake()
        {
            if (orderManager == null) orderManager = GetComponent<OrderManager>();
            if (orderManager == null) orderManager = Object.FindAnyObjectByType<OrderManager>();

            if (orderManager == null)
            {
                Debug.LogWarning(
                    "[OrderMarkers] No OrderManager in the scene, so there is nothing to draw. " +
                    "Switching off rather than checking every frame.", this);
                enabled = false;
                return;
            }

            var root = new GameObject("Order beacons");
            _beaconRoot = root.transform;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) show = !show;

            if (!worldMarkers) return;

            if (!show)
            {
                HideFrom(0);
                return;
            }

            int used = 0;
            IReadOnlyList<Order> orders = orderManager.ActiveOrders;

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];
                if (order.IsTerminal) continue;

                // One beacon per order, at whichever end the player needs next. Showing both at once
                // doubles the clutter and hides the one piece of information that matters: where to go.
                bool carrying = order.State == OrderState.Carried || order.State == OrderState.Collected;
                Vector3 target = carrying ? order.DropOffPoint : order.PickupPoint;
                Color baseColour = carrying ? dropOffColour : pickupColour;

                float urgency = 1f - Mathf.Clamp01(order.TimeRemaining / Mathf.Max(0.01f, urgentBelowSeconds));
                Color colour = Color.Lerp(baseColour, urgentColour, urgency);

                PlaceBeacon(used, target, colour);
                used++;
            }

            HideFrom(used);
        }

        /// <summary>
        /// Positions a pooled beacon, growing the pool only when a frame genuinely needs more than it
        /// has ever needed before.
        /// </summary>
        private void PlaceBeacon(int index, Vector3 groundPosition, Color colour)
        {
            while (_beaconPool.Count <= index) _beaconPool.Add(CreateBeacon());

            GameObject beacon = _beaconPool[index];
            if (!beacon.activeSelf) beacon.SetActive(true);

            beacon.transform.position = groundPosition + Vector3.up * (beaconHeight * 0.5f);
            beacon.transform.localScale = new Vector3(beaconWidth, beaconHeight, beaconWidth);

            // A per-renderer property block rather than a material per beacon, so all of them share one
            // material and one draw setup no matter how many orders are live.
            var renderer = beacon.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", colour);
                block.SetColor("_Color", colour);
                renderer.SetPropertyBlock(block);
            }
        }

        private GameObject CreateBeacon()
        {
            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beacon.name = "Order beacon";

            // No collider: a beacon the car can crash into would change the driving, and this is meant
            // to be a view of the game rather than part of it.
            Collider collider = beacon.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            if (_beaconMaterial == null) _beaconMaterial = CreateBeaconMaterial();
            beacon.GetComponent<MeshRenderer>().sharedMaterial = _beaconMaterial;

            var renderer = beacon.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            beacon.transform.SetParent(_beaconRoot, true);
            return beacon;
        }

        private static Material CreateBeaconMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Sprites/Default");

            var material = new Material(shader) { name = "Order beacon" };

            // Transparent, so a beacon standing in front of the road does not hide the corner behind it.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

            material.renderQueue = 3000;
            return material;
        }

        private void HideFrom(int index)
        {
            for (int i = index; i < _beaconPool.Count; i++)
            {
                if (_beaconPool[i].activeSelf) _beaconPool[i].SetActive(false);
            }
        }

        private void OnGUI()
        {
            if (!show || !orderList || orderManager == null) return;

            BuildStyles();

            IReadOnlyList<Order> orders = orderManager.ActiveOrders;

            var text = new System.Text.StringBuilder();
            text.AppendLine($"ORDERS  {orders.Count} live   {orderManager.TotalResolved} resolved");
            text.AppendLine();

            if (orders.Count == 0)
            {
                text.AppendLine("waiting for the first spawn");
            }

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];
                float distance = Vector3.Distance(
                    transform.position,
                    order.State == OrderState.Carried ? order.DropOffPoint : order.PickupPoint);

                text.AppendLine(
                    $"#{order.Id,-3} {order.State,-9} {order.TimeRemaining,5:0.0}s  {distance,5:0} m");
            }

            var area = new Rect(Screen.width - 330f, 12f, 318f, 300f);

            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text.ToString(), _shadow);
            GUI.Label(area, text.ToString(), _style);
        }

        private void BuildStyles()
        {
            if (_style != null) return;

            _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperLeft };
            _style.font = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Courier New", "monospace" }, 14);
            _style.normal.textColor = Color.white;

            _shadow = new GUIStyle(_style);
            _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }

        private void OnDestroy()
        {
            if (_beaconRoot != null) Destroy(_beaconRoot.gameObject);
            if (_beaconMaterial != null) Destroy(_beaconMaterial);
        }
    }
}
