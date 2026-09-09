using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Owns the live set of orders: spawns new ones up to the active cap, ages every non-terminal
    /// order every frame, and resolves a delivery when zone detection tells it one happened.
    ///
    /// WHAT THIS DOES NOT DO
    /// It does not decide whether a pickup or drop-off is allowed, that is zone detection's job,
    /// since only zone detection knows where the vehicle actually is. It does not touch the cargo
    /// slot either. This script only ever spawns Orders, ages them, and reacts once told an order
    /// was collected, carried, or delivered. Keeping those decisions out of here is what lets
    /// zone detection and the cargo system be built and changed independently, the same reasoning
    /// VehicleController uses to keep steering and grip as separate steps.
    ///
    /// TESTING BEFORE THE GREYBOX ROUTE EXISTS
    /// Like VehicleLabBuilder generates a test track instead of waiting for real level art, this
    /// manager can generate its own ring of placeholder pickup/drop-off points if none are wired
    /// up yet. That means the order queue can be built, tested, and even shown working before
    /// Zubuhle's greybox route exists, and switching to the real route later is just a matter of
    /// dragging real transforms into the two arrays below.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderManager : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Every number this system uses. Swap the asset to test a different pressure profile.")]
        [SerializeField] private OrderTuning tuning;

        [Header("Wiring")]
        [Tooltip("Real pickup points. Leave empty while the greybox route does not exist yet, " +
                 "generated test points will be used instead.")]
        [SerializeField] private Transform[] pickupPoints;

        [Tooltip("Real drop-off points. Leave empty while the greybox route does not exist yet.")]
        [SerializeField] private Transform[] dropOffPoints;

        [Header("Test points (used only if the arrays above are empty)")]
        [Tooltip("Generate a ring of placeholder pickup/drop-off points automatically, so this " +
                 "system can be tested before real level geometry exists.")]
        [SerializeField] private bool generateTestPointsIfEmpty = true;

        [Tooltip("How many placeholder points to generate around the origin.")]
        [SerializeField] private int testPointCount = 6;

        [Tooltip("Radius of the generated ring, metres. Loosely matches the handling sandbox scale.")]
        [SerializeField] private float testPointRadius = 60f;

        [Header("Debug")]
        [Tooltip("Log every spawn, delivery, and late order to the console.")]
        [SerializeField] private bool logActivity = true;

        // ---------------- events ----------------

        /// <summary>Fired the instant a new order is created and activated.</summary>
        public event Action<Order> OrderSpawned;

        /// <summary>Fired when an order leaves the active set, either delivered or late. The float
        /// is the value awarded, 0 for a late order.</summary>
        public event Action<Order, float> OrderResolved;

        // ---------------- readable state ----------------

        /// <summary>Every order that is not yet Delivered or Late. Read-only: nothing outside this
        /// class is allowed to add or remove from the live list directly.</summary>
        public IReadOnlyList<Order> ActiveOrders => _activeOrders;

        /// <summary>How many orders this session has resolved, delivered or late, combined.
        /// Useful as a quick playtest sanity check without reading the console.</summary>
        public int TotalResolved { get; private set; }

        // ---------------- internals ----------------

        private readonly List<Order> _activeOrders = new List<Order>();
        private Vector3[] _pickupPositions;
        private Vector3[] _dropOffPositions;
        private float _timeUntilNextSpawn;
        private int _nextOrderId;

        private void Awake()
        {
            if (tuning == null)
            {
                Debug.LogError(
                    "[OrderManager] No OrderTuning assigned. Drag a tuning asset onto the Tuning " +
                    "field, the queue cannot spawn anything without it.", this);
                enabled = false;
                return;
            }

            ResolvePickupAndDropOffPoints();
            _timeUntilNextSpawn = 0f; // spawn the first order immediately, do not make the player wait
        }

        private void Update()
        {
            if (tuning == null) return;

            float dt = Time.deltaTime;

            TickAllOrders(dt);
            TrySpawn(dt);
        }

        // ==================================================================
        // POINTS
        // ==================================================================

        /// <summary>
        /// Uses the real transforms if any were assigned, otherwise builds a ring of placeholder
        /// points so the system is testable on its own. Logged clearly either way, so nobody
        /// mistakes a placeholder ring for the real route later.
        /// </summary>
        private void ResolvePickupAndDropOffPoints()
        {
            bool haveRealPoints = pickupPoints != null && pickupPoints.Length > 0
                                  && dropOffPoints != null && dropOffPoints.Length > 0;

            if (haveRealPoints)
            {
                _pickupPositions = new Vector3[pickupPoints.Length];
                for (int i = 0; i < pickupPoints.Length; i++)
                    _pickupPositions[i] = pickupPoints[i].position;

                _dropOffPositions = new Vector3[dropOffPoints.Length];
                for (int i = 0; i < dropOffPoints.Length; i++)
                    _dropOffPositions[i] = dropOffPoints[i].position;

                Debug.Log($"[OrderManager] Using {pickupPoints.Length} real pickup points and " +
                          $"{dropOffPoints.Length} real drop-off points.");
                return;
            }

            if (!generateTestPointsIfEmpty)
            {
                Debug.LogError(
                    "[OrderManager] No pickup/drop-off points assigned and test point generation " +
                    "is off. No orders will spawn.", this);
                _pickupPositions = Array.Empty<Vector3>();
                _dropOffPositions = Array.Empty<Vector3>();
                return;
            }

            // A ring rather than a random scatter, for the same reason VehicleLabBuilder generates
            // an exact layout instead of hand placing things: a fixed, repeatable arrangement means
            // two playtests can be compared against each other.
            int count = Mathf.Max(2, testPointCount);
            _pickupPositions = new Vector3[count];
            _dropOffPositions = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 point = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * testPointRadius;

                // Drop-off sits opposite its same-index pickup around the ring, so no pair is
                // trivially close together.
                float dropAngle = angle + Mathf.PI;
                Vector3 dropPoint = new Vector3(Mathf.Cos(dropAngle), 0f, Mathf.Sin(dropAngle)) * testPointRadius;

                _pickupPositions[i] = point;
                _dropOffPositions[i] = dropPoint;
            }

            Debug.Log($"[OrderManager] No real points assigned. Generated {count} placeholder " +
                      $"pickup/drop-off pairs on a {testPointRadius:0} m ring. Replace with real " +
                      "greybox points once the route exists.");
        }

        // ==================================================================
        // SPAWNING
        // ==================================================================

        private void TrySpawn(float dt)
        {
            if (_activeOrders.Count >= tuning.maxActiveOrders) return;
            if (_pickupPositions == null || _pickupPositions.Length == 0) return;

            _timeUntilNextSpawn -= dt;
            if (_timeUntilNextSpawn > 0f) return;

            SpawnOrder();
            _timeUntilNextSpawn = tuning.spawnInterval;
        }

        private void SpawnOrder()
        {
            int pickupIndex = UnityEngine.Random.Range(0, _pickupPositions.Length);
            int dropIndex = UnityEngine.Random.Range(0, _dropOffPositions.Length);

            Order order = new Order(
                id: _nextOrderId++,
                pickupPoint: _pickupPositions[pickupIndex],
                dropOffPoint: _dropOffPositions[dropIndex],
                timeLimit: tuning.orderTimeLimit);

            order.Activate();
            _activeOrders.Add(order);

            if (logActivity)
            {
                Debug.Log($"[OrderManager] Order {order.Id} spawned: {order.DesignedDistance:0.0} m, " +
                          $"{tuning.orderTimeLimit:0} s to deliver. " +
                          $"{_activeOrders.Count}/{tuning.maxActiveOrders} active.");
            }

            OrderSpawned?.Invoke(order);
        }

        // ==================================================================
        // AGEING AND RESOLUTION
        // ==================================================================

        private void TickAllOrders(float dt)
        {
            // Iterate backwards so resolved orders can be removed from _activeOrders inside the
            // loop without skipping the item that shifts into the removed slot.
            for (int i = _activeOrders.Count - 1; i >= 0; i--)
            {
                Order order = _activeOrders[i];
                bool wentLate = order.Tick(dt);

                if (wentLate) Resolve(order, value: 0f, wasLate: true);
            }
        }

        /// <summary>
        /// Call this from zone detection once the vehicle has driven into an order's drop-off
        /// zone while that order is Carried. This is the only entry point that can turn an order
        /// into a Delivered one, everything else about scoring flows from this single call.
        /// </summary>
        public void ResolveDelivery(Order order)
        {
            if (order == null || order.State != OrderState.Carried)
            {
                Debug.LogWarning(
                    $"[OrderManager] ResolveDelivery called on an order that was not Carried " +
                    $"(state was {order?.State}). Ignored.", this);
                return;
            }

            order.MarkDelivered();

            // Flat value for the MVP, per Scope: "the MVP only needs a clear delivery value".
            // Scaling this by DesignedDistance and converting it into XP/money is Milestone 2 work
            // (Task 20), deliberately not built yet so that task is not quietly half-done here.
            float value = tuning.baseDeliveryValue;

            Resolve(order, value, wasLate: false);
        }

        private void Resolve(Order order, float value, bool wasLate)
        {
            _activeOrders.Remove(order);
            TotalResolved++;

            if (logActivity)
            {
                string outcome = wasLate ? "LATE, no value" : $"DELIVERED, +{value:0} value";
                Debug.Log($"[OrderManager] Order {order.Id} resolved: {outcome}. " +
                          $"{_activeOrders.Count}/{tuning.maxActiveOrders} active.");
            }

            OrderResolved?.Invoke(order, value);
        }

        // ==================================================================
        // EDITOR PREVIEW
        // ==================================================================

        private void OnDrawGizmosSelected()
        {
            if (_pickupPositions == null) return;

            Gizmos.color = new Color(0.2f, 0.85f, 0.35f, 0.9f);
            foreach (Vector3 p in _pickupPositions) Gizmos.DrawWireSphere(p, 1.5f);

            Gizmos.color = new Color(0.95f, 0.55f, 0.15f, 0.9f);
            if (_dropOffPositions != null)
                foreach (Vector3 p in _dropOffPositions) Gizmos.DrawWireSphere(p, 1.5f);
        }
    }
}
