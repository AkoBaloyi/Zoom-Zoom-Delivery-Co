using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Owns the live set of orders: spawns new ones up to the active cap, ages every non-terminal
    /// order's patience bar every frame, and resolves a delivery when zone detection tells it one
    /// happened. It also works out what each order pays: the food's base pay, an early bonus or
    /// late deduction from the patience bar, and a fixed penalty when an order is cancelled.
    /// All of those numbers come from the Order Tuning asset.
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

        /// <summary>Fires when an order ends, with what it paid in Rand. Negative for a
        /// cancelled order, because of the penalty.</summary>
        public event Action<Order, float> OrderResolved;

        /// <summary>Same moment as OrderResolved, with the full breakdown (base, time bonus,
        /// damage, penalty) for popups and the end of shift summary.</summary>
        public event Action<Order, OrderPayout> OrderPaid;

        public IReadOnlyList<Order> ActiveOrders => _activeOrders;
        public int TotalResolved { get; private set; }

        /// <summary>The tuning asset in use, so other systems read the same numbers instead of
        /// each needing their own reference to it.</summary>
        public OrderTuning Tuning => tuning;

        private readonly List<Order> _activeOrders = new List<Order>();
        private Vector3[] _pickupPositions;
        private Vector3[] _dropOffPositions;
        private string[] _pickupNames;
        private string[] _dropOffNames;
        private float _timeUntilNextSpawn;
        private int _nextOrderId = 1; // orders read as #1, #2... not #0, #1, matching how a person actually counts

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

                // The scene object's name is the place name on the order card, so name the
                // pickup and drop off objects what the player should read, e.g. "Noodle Bar".
                _pickupNames = new string[pickupPoints.Length];
                for (int i = 0; i < pickupPoints.Length; i++) _pickupNames[i] = pickupPoints[i].name;

                _dropOffNames = new string[dropOffPoints.Length];
                for (int i = 0; i < dropOffPoints.Length; i++) _dropOffNames[i] = dropOffPoints[i].name;

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
            int pickupIndex = PickIndexAvoidingCollisions(_pickupPositions, o => o.PickupPoint);
            int dropIndex = PickIndexAvoidingCollisions(_dropOffPositions, o => o.DropOffPoint);

            Vector3 pickupPos = _pickupPositions[pickupIndex];
            Vector3 dropPos = _dropOffPositions[dropIndex];

            Order order = new Order(
                id: _nextOrderId++,
                pickupPoint: pickupPos,
                dropOffPoint: dropPos,
                timeLimit: tuning.orderTimeLimit,
                food: tuning.PickFood(),
                pickupName: NameAt(_pickupNames, pickupIndex, "Restaurant"),
                dropOffName: NameAt(_dropOffNames, dropIndex, "House"));

            order.Activate();
            _activeOrders.Add(order);

            if (logActivity)
            {
                Debug.Log($"[OrderManager] Order {order.Id} spawned: {order.FoodName} from " +
                          $"{order.PickupName} to {order.DropOffName}, {tuning.orderTimeLimit:0} s patience, " +
                          $"base pay R{tuning.BasePayFor(order.Food, order.DesignedDistance):0}. " +
                          $"{_activeOrders.Count}/{tuning.maxActiveOrders} active.");
            }

            OrderSpawned?.Invoke(order);
        }

        private static string NameAt(string[] names, int index, string fallbackPrefix)
        {
            if (names != null && index >= 0 && index < names.Length && !string.IsNullOrEmpty(names[index]))
                return names[index];
            return $"{fallbackPrefix} {index + 1}";
        }

        /// <summary>
        /// Picks a random index into pool, but avoids landing on a point already in use by another
        /// currently active order's own pickup or drop-off, so two orders can't spawn stacked on
        /// top of each other. With a small point pool and several orders active at once, a purely
        /// random pick collides often enough to be a regular occurrence, not a rare edge case, this
        /// is what actually stops that.
        /// </summary>
        private int PickIndexAvoidingCollisions(Vector3[] pool, Func<Order, Vector3> selector)
        {
            const int maxAttempts = 8;
            const float sameSpotThreshold = 0.5f;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int candidate = UnityEngine.Random.Range(0, pool.Length);
                Vector3 candidatePos = pool[candidate];

                bool collides = false;
                foreach (Order existing in _activeOrders)
                {
                    if (Vector3.Distance(selector(existing), candidatePos) < sameSpotThreshold)
                    {
                        collides = true;
                        break;
                    }
                }

                if (!collides) return candidate;
            }

            // Every attempt collided. This almost always means there are fewer distinct points
            // than the number of orders that can be active at once, worth surfacing rather than
            // silently spawning on top of an existing order.
            Debug.LogWarning(
                "[OrderManager] Could not find a pickup/drop-off point not already in use by " +
                "another active order after several attempts. testPointCount (or the number of " +
                "real points assigned) may be too small relative to maxActiveOrders.", this);
            return UnityEngine.Random.Range(0, pool.Length);
        }

        private void TickAllOrders(float dt)
        {
            for (int i = _activeOrders.Count - 1; i >= 0; i--)
            {
                Order order = _activeOrders[i];
                bool wentLate = order.Tick(dt);
                if (!wentLate) continue;

                // Patience ran out: the order is cancelled and costs a fixed penalty.
                var payout = new OrderPayout
                {
                    Timing = DeliveryTiming.Failed,
                    Penalty = -tuning.cancelPenalty
                };
                Resolve(order, payout);
            }
        }

        /// <summary>Call from zone detection once the vehicle has driven into a Carried order's
        /// drop off zone. The only entry point that can turn an order into Delivered.</summary>
        /// <param name="conditionMultiplier">0 to 1 from CargoCondition when it is switched on,
        /// otherwise 1 (full pay).</param>
        public void ResolveDelivery(Order order, float conditionMultiplier = 1f)
        {
            if (order == null || order.State != OrderState.Carried)
            {
                Debug.LogWarning(
                    $"[OrderManager] ResolveDelivery called on an order that was not Carried " +
                    $"(state was {order?.State}). Ignored.", this);
                return;
            }

            float patienceLeft = order.PatienceFraction;
            order.MarkDelivered();

            var payout = new OrderPayout { BasePay = tuning.BasePayFor(order.Food, order.DesignedDistance) };

            // Time bonus from the patience bar: early pays extra, late pays less.
            if (patienceLeft > tuning.earlyAbovePatienceLeft)
            {
                payout.Timing = DeliveryTiming.Early;
                payout.TimeAdjustment = payout.BasePay * tuning.earlyBonus;
            }
            else if (patienceLeft < tuning.lateBelowPatienceLeft)
            {
                payout.Timing = DeliveryTiming.Late;
                payout.TimeAdjustment = -payout.BasePay * tuning.lateDeduction;
            }
            else
            {
                payout.Timing = DeliveryTiming.OnTime;
            }

            // Damaged cargo, only when CargoCondition is switched on.
            float beforeCondition = payout.BasePay + payout.TimeAdjustment;
            payout.ConditionLoss = -beforeCondition * (1f - Mathf.Clamp01(conditionMultiplier));

            Resolve(order, payout);
        }

        private void Resolve(Order order, OrderPayout payout)
        {
            _activeOrders.Remove(order);
            TotalResolved++;
            order.SetPayout(payout);

            if (logActivity)
            {
                Debug.Log($"[OrderManager] Order {order.Id} ({order.FoodName}) {payout.Timing}: " +
                          $"base R{payout.BasePay:0}, time {payout.TimeAdjustment:+0;-0;0}, " +
                          $"damage {payout.ConditionLoss:0}, penalty {payout.Penalty:0}, " +
                          $"total R{payout.Total:0}. {_activeOrders.Count}/{tuning.maxActiveOrders} active.");
            }

            OrderResolved?.Invoke(order, payout.Total);
            OrderPaid?.Invoke(order, payout);
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