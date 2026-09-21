using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Owns the live set of orders: spawns new ones up to the active cap, ages every non-terminal
    /// order every frame, and resolves a delivery when zone detection tells it one happened.
    /// Never touches the cargo slot or decides whether a pickup is allowed, that stays in
    /// CargoSystem and ZoneDetection so each piece can change independently.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderManager : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private OrderTuning tuning;

        [Header("Wiring")]
        [Tooltip("Real pickup points. Leave empty while the greybox route does not exist yet.")]
        [SerializeField] private Transform[] pickupPoints;

        [Tooltip("Real drop-off points. Leave empty while the greybox route does not exist yet.")]
        [SerializeField] private Transform[] dropOffPoints;

        [Header("Test points (used only if the arrays above are empty)")]
        [Tooltip("Constrain generated test points to this collider's world bounds, e.g. the " +
                 "Floor's Box Collider. If assigned, this takes priority over the ring below, " +
                 "since it keeps orders spawning inside the actual level rather than a shape " +
                 "guessed at before the level existed.")]
        [SerializeField] private Collider floorBounds;

        [Tooltip("Keep generated points this far in from the floor's edge, so an order never " +
                 "spawns inside a wall or right at the boundary.")]
        [SerializeField] private float floorEdgeMargin = 5f;

        [SerializeField] private bool generateTestPointsIfEmpty = true;
        [SerializeField] private int testPointCount = 6;
        [SerializeField] private float testPointRadius = 60f;

        [Header("Debug")]
        [SerializeField] private bool logActivity = true;

        public event Action<Order> OrderSpawned;
        public event Action<Order, float> OrderResolved;

        public IReadOnlyList<Order> ActiveOrders => _activeOrders;
        public int TotalResolved { get; private set; }

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
            _timeUntilNextSpawn = 0f;
        }

        private void Update()
        {
            if (tuning == null) return;

            float dt = Time.deltaTime;
            TickAllOrders(dt);
            TrySpawn(dt);
        }

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

            if (floorBounds != null)
            {
                GenerateBoundedTestPoints();
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

            int count = Mathf.Max(2, testPointCount);
            _pickupPositions = new Vector3[count];
            _dropOffPositions = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 point = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * testPointRadius;

                float dropAngle = angle + Mathf.PI;
                Vector3 dropPoint = new Vector3(Mathf.Cos(dropAngle), 0f, Mathf.Sin(dropAngle)) * testPointRadius;

                _pickupPositions[i] = point;
                _dropOffPositions[i] = dropPoint;
            }

            Debug.Log($"[OrderManager] No real points assigned. Generated {count} placeholder " +
                      $"pickup/drop-off pairs on a {testPointRadius:0} m ring. Replace with real " +
                      "greybox points once the route exists.");
        }

        /// <summary>
        /// Scatters test points inside floorBounds instead of the origin-centred ring, so orders
        /// only ever spawn inside the actual greybox rather than a shape guessed at before it
        /// existed. Reads the collider's real world bounds, so this stays correct if the floor is
        /// moved or resized later, nothing here is a hardcoded coordinate.
        /// </summary>
        private void GenerateBoundedTestPoints()
        {
            Bounds bounds = floorBounds.bounds;

            float minX = bounds.min.x + floorEdgeMargin;
            float maxX = bounds.max.x - floorEdgeMargin;
            float minZ = bounds.min.z + floorEdgeMargin;
            float maxZ = bounds.max.z - floorEdgeMargin;

            if (minX >= maxX || minZ >= maxZ)
            {
                Debug.LogError(
                    "[OrderManager] floorBounds is too small for the current floorEdgeMargin, " +
                    "there is no room left to place a point inside it. Falling back to the ring.",
                    this);
                floorBounds = null;
                ResolvePickupAndDropOffPoints();
                return;
            }

            float groundY = bounds.max.y + 0.1f;
            int count = Mathf.Max(2, testPointCount);

            _pickupPositions = new Vector3[count];
            _dropOffPositions = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                _pickupPositions[i] = new Vector3(
                    UnityEngine.Random.Range(minX, maxX), groundY, UnityEngine.Random.Range(minZ, maxZ));

                _dropOffPositions[i] = new Vector3(
                    UnityEngine.Random.Range(minX, maxX), groundY, UnityEngine.Random.Range(minZ, maxZ));
            }

            Debug.Log($"[OrderManager] No real points assigned. Generated {count} pickup and " +
                      $"{count} drop-off points inside {floorBounds.name}'s bounds " +
                      $"({floorEdgeMargin:0}m edge margin). Replace with real greybox points once " +
                      "specific delivery locations exist.");
        }

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

        private void TickAllOrders(float dt)
        {
            for (int i = _activeOrders.Count - 1; i >= 0; i--)
            {
                Order order = _activeOrders[i];
                bool wentLate = order.Tick(dt);
                if (wentLate) Resolve(order, value: 0f, wasLate: true);
            }
        }

        /// <summary>Call from zone detection once the vehicle has driven into a Carried order's
        /// drop-off zone. The only entry point that can turn an order into Delivered.</summary>
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

            // Flat value for the MVP. Scaling by DesignedDistance into XP/money is Milestone 2 work.
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