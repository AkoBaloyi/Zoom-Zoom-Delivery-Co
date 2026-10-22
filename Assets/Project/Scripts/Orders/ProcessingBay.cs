using System;
using System.Collections.Generic;
using UnityEngine;
using ZoomZoom.Orders.UI;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// The Overcooked part of the loop: a bay where the player drops an order's ingredients and
    /// the food cooks for an exact, fixed time while the player drives off to do something else.
    /// Once cooked, it waits for its food's Burn Seconds. Collect it in time and it goes into the
    /// car for delivery. Leave it too long and it burns, the order fails, and the burn penalty
    /// comes off the player's money.
    ///
    /// One bay holds one order at a time. Driving into a bay first takes out any ready food (if
    /// the car has room), then puts in the next order whose ingredients are all in the car, so
    /// "collect the burger, swap in the soup" is a single stop.
    ///
    /// Put one on an empty GameObject where the car should stop. It draws itself from primitives
    /// (a ground disc, a stove, a progress bar and a label), so it works in the greybox with no
    /// art. Turn off Build Visual once there is a real model.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProcessingBay : MonoBehaviour
    {
        private static readonly List<ProcessingBay> _all = new List<ProcessingBay>();

        /// <summary>Every enabled bay in the scene.</summary>
        public static IReadOnlyList<ProcessingBay> All => _all;

        [Tooltip("Shown on the order card, for example \"Bay 1\". Leave empty to use this " +
                 "GameObject's name.")]
        [SerializeField] private string bayName = "";

        [Header("Wiring")]
        [SerializeField] private OrderManager orderManager;
        [Tooltip("Its Bay Radius sets the size of the ground disc, so the disc matches the real zone.")]
        [SerializeField] private ZoneDetection zoneDetection;

        [Header("Look")]
        [SerializeField] private bool buildVisual = true;
        [SerializeField] private float stoveHeight = 2.5f;
        [SerializeField] private float stoveRadius = 1.4f;
        [SerializeField] private float barWidth = 5f;
        [SerializeField] private float barHeight = 0.6f;
        [SerializeField] private float barY = 6f;
        [SerializeField] private float labelY = 7.2f;
        [SerializeField] private float labelCharacterSize = 0.2f;
        [Tooltip("Seconds before burning at which the bay starts flashing red.")]
        [SerializeField] private float warnBelowSeconds = 4f;

        [Header("Debug")]
        [SerializeField] private bool logActivity = true;

        public string BayName => string.IsNullOrWhiteSpace(bayName) ? gameObject.name : bayName.Trim();
        public Vector3 Position => transform.position;

        /// <summary>The order in this bay, or null when it is empty.</summary>
        public Order Order { get; private set; }

        public bool IsEmpty => Order == null;
        public bool IsReady => Order != null && Order.State == OrderState.Ready;

        public event Action<ProcessingBay, Order> CookingStarted;
        public event Action<ProcessingBay, Order> FoodReady;
        public event Action<ProcessingBay, Order> FoodBurned;
        public event Action<ProcessingBay, Order> FoodTakenOut;

        /// <summary>Fires for any of the above, so UI can listen to every bay with one handler.</summary>
        public static event Action<ProcessingBay, Order, string> AnyBayEvent;

        // Visual
        private Transform _disc, _stove, _barRoot, _barFill;
        private MeshRenderer _discRenderer, _stoveRenderer, _barFillRenderer, _barBgRenderer;
        // Made in BuildVisual, not here: Unity does not allow these to be created in a field
        // initializer.
        private MaterialPropertyBlock _discBlock, _stoveBlock, _fillBlock, _bgBlock;
        private TextMesh _label;
        private Camera _camera;

        /// <summary>The nearest bay to a point, or null if there are none.</summary>
        public static ProcessingBay Nearest(Vector3 point)
        {
            ProcessingBay best = null;
            float bestDistance = float.MaxValue;
            foreach (ProcessingBay b in _all)
            {
                float d = (b.Position - point).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = b; }
            }
            return best;
        }

        private void OnEnable()
        {
            if (!_all.Contains(this)) _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (zoneDetection == null) zoneDetection = FindAnyObjectByType<ZoneDetection>();

            if (orderManager == null)
                Debug.LogError($"[ProcessingBay] '{BayName}' found no OrderManager, so burned food " +
                               "cannot fail its order.", this);
        }

        private void Start()
        {
            if (buildVisual) BuildVisual();
        }

        /// <summary>Puts an order's ingredients in and starts cooking. Fails if the bay is busy
        /// or the order does not have every ingredient yet.</summary>
        public bool TryStartCooking(Order order)
        {
            if (Order != null || order == null) return false;
            if (!order.StartCooking(Position, BayName)) return false;

            Order = order;
            if (logActivity)
                Debug.Log($"[ProcessingBay] {BayName}: order {order.Id} ({order.FoodName}) cooking " +
                          $"for {order.CookSeconds:0.#} s.", this);

            CookingStarted?.Invoke(this, order);
            AnyBayEvent?.Invoke(this, order, "cooking");

            // A zero second cook is ready immediately.
            if (order.State == OrderState.Ready) RaiseReady(order);
            return true;
        }

        /// <summary>Takes the cooked food out and puts it in the car. CargoSystem calls this
        /// only when the car has room.</summary>
        public Order TakeOut()
        {
            if (!IsReady) return null;

            Order order = Order;
            if (!order.MarkCarried()) return null;

            Order = null;
            if (logActivity)
                Debug.Log($"[ProcessingBay] {BayName}: order {order.Id} taken out with " +
                          $"{order.BayTimeRemaining:0.0} s before burning.", this);

            FoodTakenOut?.Invoke(this, order);
            AnyBayEvent?.Invoke(this, order, "taken");
            return order;
        }

        private void Update()
        {
            // The shift timer switches the OrderManager off at time up. Freeze the bay with it,
            // so nothing burns (and costs money) after the shift has ended.
            bool shiftRunning = orderManager == null || orderManager.enabled;

            if (Order != null && shiftRunning)
            {
                // The order was cancelled (patience ran out) while it sat here.
                if (Order.IsTerminal)
                {
                    Order = null;
                }
                else
                {
                    bool wasCooking = Order.State == OrderState.Cooking;
                    bool burned = Order.TickBay(Time.deltaTime);

                    if (wasCooking && Order.State == OrderState.Ready) RaiseReady(Order);

                    if (burned)
                    {
                        Order burnt = Order;
                        Order = null;

                        if (logActivity)
                            Debug.Log($"[ProcessingBay] {BayName}: order {burnt.Id} ({burnt.FoodName}) burned.", this);

                        if (orderManager != null) orderManager.ResolveBurned(burnt);
                        FoodBurned?.Invoke(this, burnt);
                        AnyBayEvent?.Invoke(this, burnt, "burned");
                    }
                }
            }

            if (buildVisual && _disc != null) RefreshVisual();
        }

        private void RaiseReady(Order order)
        {
            if (logActivity)
                Debug.Log($"[ProcessingBay] {BayName}: order {order.Id} ({order.FoodName}) ready, " +
                          $"burns in {order.BurnSeconds:0} s.", this);

            FoodReady?.Invoke(this, order);
            AnyBayEvent?.Invoke(this, order, "ready");
        }

        // ---- visual -------------------------------------------------------------------------

        private void RefreshVisual()
        {
            if (_camera == null) _camera = Camera.main;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            float fastPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);

            Color zone;
            Color bar;
            float fill;
            string label;
            bool showBar = true;

            if (Order == null)
            {
                zone = OrderPalette.Cook;
                bar = OrderPalette.Cook;
                fill = 0f;
                label = $"{BayName.ToUpper()}\nEMPTY";
                showBar = false;
            }
            else if (Order.State == OrderState.Cooking)
            {
                zone = OrderPalette.Cook;
                bar = OrderPalette.Cook;
                fill = Order.BayFraction;
                label = $"{BayName.ToUpper()}\n#{Order.Id} {Order.FoodName.ToUpper()}  {Mathf.CeilToInt(Order.BayTimeRemaining)}s";
            }
            else
            {
                bool warning = Order.BayTimeRemaining <= warnBelowSeconds;
                zone = warning ? Color.Lerp(OrderPalette.Deliver, OrderPalette.Urgent, fastPulse) : OrderPalette.Deliver;
                bar = OrderPalette.TimerBar(Order.BayFraction);
                fill = Order.BayFraction;
                label = warning
                    ? $"{BayName.ToUpper()}\nBURNING!  #{Order.Id}  {Mathf.CeilToInt(Order.BayTimeRemaining)}s"
                    : $"{BayName.ToUpper()}\nREADY!  #{Order.Id}  {Mathf.CeilToInt(Order.BayTimeRemaining)}s";
            }

            float radius = zoneDetection != null ? zoneDetection.BayRadius : 6f;
            _disc.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

            Color disc = zone;
            disc.a = Order == null ? 0.3f : IsReady ? Mathf.Lerp(0.45f, 0.8f, pulse) : 0.55f;
            OrderWorldMaterial.SetColour(_discBlock, disc);
            _discRenderer.SetPropertyBlock(_discBlock);

            float stoveScale = IsReady ? 1f + 0.12f * pulse : 1f;
            _stove.localScale = new Vector3(stoveRadius * 2f * stoveScale, stoveHeight * 0.5f, stoveRadius * 2f * stoveScale);
            OrderWorldMaterial.SetColour(_stoveBlock, Color.Lerp(zone, Color.black, Order == null ? 0.55f : 0.15f));
            _stoveRenderer.SetPropertyBlock(_stoveBlock);

            _barRoot.gameObject.SetActive(showBar);
            if (showBar)
            {
                fill = Mathf.Clamp01(fill);
                _barFill.localScale = new Vector3(Mathf.Max(0.001f, barWidth * fill), barHeight * 0.8f, 0.06f);
                _barFill.localPosition = new Vector3(-barWidth * 0.5f + barWidth * fill * 0.5f, 0f, -0.04f);
                OrderWorldMaterial.SetColour(_fillBlock, bar);
                _barFillRenderer.SetPropertyBlock(_fillBlock);
                OrderWorldMaterial.SetColour(_bgBlock, new Color(0.08f, 0.05f, 0.04f, 0.9f));
                _barBgRenderer.SetPropertyBlock(_bgBlock);
            }

            _label.text = label;
            _label.color = zone;

            if (_camera != null)
            {
                Quaternion face = _camera.transform.rotation;
                _barRoot.rotation = face;
                _label.transform.rotation = face;
            }
        }

        private void BuildVisual()
        {
            Material material = OrderWorldMaterial.Get();

            _discBlock = new MaterialPropertyBlock();
            _stoveBlock = new MaterialPropertyBlock();
            _fillBlock = new MaterialPropertyBlock();
            _bgBlock = new MaterialPropertyBlock();

            var root = new GameObject("Bay visual");
            root.transform.SetParent(transform, false);

            _disc = MakePrimitive(PrimitiveType.Cylinder, "Zone disc", root.transform, material, out _discRenderer);
            _disc.localPosition = new Vector3(0f, 0.06f, 0f);

            _stove = MakePrimitive(PrimitiveType.Cylinder, "Stove", root.transform, material, out _stoveRenderer);
            _stove.localPosition = new Vector3(0f, stoveHeight * 0.5f, 0f);

            var barRoot = new GameObject("Bar");
            barRoot.transform.SetParent(root.transform, false);
            barRoot.transform.localPosition = new Vector3(0f, barY, 0f);
            _barRoot = barRoot.transform;

            Transform bg = MakePrimitive(PrimitiveType.Cube, "Bar background", _barRoot, material, out _barBgRenderer);
            bg.localScale = new Vector3(barWidth + 0.3f, barHeight, 0.05f);

            _barFill = MakePrimitive(PrimitiveType.Cube, "Bar fill", _barRoot, material, out _barFillRenderer);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(root.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, labelY, 0f);
            _label = labelGO.AddComponent<TextMesh>();
            _label.fontSize = 64;
            _label.characterSize = labelCharacterSize;
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontStyle = FontStyle.Bold;
            MeshRenderer labelRenderer = labelGO.GetComponent<MeshRenderer>();
            labelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            labelRenderer.receiveShadows = false;
        }

        private static Transform MakePrimitive(PrimitiveType type, string name, Transform parent,
            Material material, out MeshRenderer renderer)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.98f, 0.55f, 0.12f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 6f);
        }
    }
}