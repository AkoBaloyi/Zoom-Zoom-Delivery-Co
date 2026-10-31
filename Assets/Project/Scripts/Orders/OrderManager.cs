using System;
using System.Collections.Generic;
using UnityEngine;
using ZoomZoom.Orders.UI;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Owns the live set of orders: spawns new ones up to the active cap, ages every order's
    /// patience bar every frame, and resolves an order when it is delivered, cancelled or burned.
    ///
    /// Each new order picks a food from the menu, then an Ingredient Source in the scene for each
    /// of that food's ingredients, a Processing Bay to cook in, and a customer drop off. What it
    /// pays is the food's base pay, an early bonus or late deduction from the patience bar, and a
    /// fixed penalty when it is cancelled or burned. All of those numbers come from the Order
    /// Tuning asset and the Food Type assets.
    ///
    /// FALLBACKS, so a half built scene still plays
    ///   An ingredient with no source in the scene is collected from a Pickup Point instead.
    ///   A food with no ingredients (or no food at all) is one pickup from a Pickup Point.
    ///   With no Processing Bay in the scene, orders skip cooking and go straight into the car.
    ///   With no Pickup or Drop Off Points assigned, test points are generated on the floor.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderManager : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private OrderTuning tuning;

        [Header("Wiring")]
        [Tooltip("Customer houses. The scene object's name is shown on the order card.")]
        [SerializeField] private Transform[] dropOffPoints;

        [Tooltip("Optional. Only used for ingredients no Ingredient Source supplies, and for " +
                 "foods with no recipe. Can be left empty once the sources are placed.")]
        [SerializeField] private Transform[] pickupPoints;

        [Header("Test points (used only for whichever list above is empty)")]
        [Tooltip("Constrain generated test points to this collider's world bounds, e.g. the " +
                 "Floor's Box Collider. If assigned, this takes priority over the ring below.")]
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

        /// <summary>Fires when the difficulty ramp changes how many orders can be open at once,
        /// with the new limit.</summary>
        public event Action<int> MaxOrdersChanged;

        /// <summary>How many orders can be open at once right now, from the difficulty ramp.</summary>
        public int CurrentMaxOrders => tuning != null ? tuning.MaxOrdersAt(_shiftSeconds) : OrderTuning.MaxOrders;

        /// <summary>Fires when an order ends, with what it paid in Rand. Negative for a
        /// cancelled or burned order, because of the penalty.</summary>
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
        private readonly HashSet<IngredientType> _warnedNoSource = new HashSet<IngredientType>();
        private Vector3[] _pickupPositions = Array.Empty<Vector3>();
        private Vector3[] _dropOffPositions = Array.Empty<Vector3>();
        private string[] _pickupNames;
        private string[] _dropOffNames;
        private float _timeUntilNextSpawn;
        private float _shiftSeconds;
        private int _currentMax = -1;
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

            ResolvePoints();
            _timeUntilNextSpawn = tuning.firstOrderDelay;
        }

        private void Start()
        {
            // Start, not Awake: sources and bays register themselves in OnEnable.
            if (!logActivity) return;
            Debug.Log($"[OrderManager] {IngredientSource.All.Count} ingredient source(s) and " +
                      $"{ProcessingBay.All.Count} processing bay(s) in the scene." +
                      (ProcessingBay.All.Count == 0 ? " With no bay, orders skip cooking." : ""));
        }

        private void Update()
        {
            if (tuning == null) return;

            float dt = Time.deltaTime;
            TickAllOrders(dt);
            TrySpawn(dt);
        }

        // ---- points ---------------------------------------------------------------------------

        private void ResolvePoints()
        {
            if (dropOffPoints != null && dropOffPoints.Length > 0)
                ReadTransforms(dropOffPoints, out _dropOffPositions, out _dropOffNames);

            if (pickupPoints != null && pickupPoints.Length > 0)
                ReadTransforms(pickupPoints, out _pickupPositions, out _pickupNames);

            bool needDrops = _dropOffPositions.Length == 0;
            bool needPickups = _pickupPositions.Length == 0;

            if ((needDrops || needPickups) && (floorBounds != null || generateTestPointsIfEmpty))
            {
                if (needDrops) _dropOffPositions = GenerateTestPoints(Mathf.PI);
                if (needPickups) _pickupPositions = GenerateTestPoints(0f);

                Debug.Log($"[OrderManager] Generated test points for: " +
                          $"{(needDrops ? "drop offs " : "")}{(needPickups ? "pickups" : "")}. " +
                          "Assign real points in the Inspector when the level has them.");
            }

            if (_dropOffPositions.Length == 0)
            {
                Debug.LogError(
                    "[OrderManager] No drop off points assigned and test point generation is off. " +
                    "No orders will spawn.", this);
            }
        }

        private static void ReadTransforms(Transform[] source, out Vector3[] positions, out string[] names)
        {
            var p = new List<Vector3>();
            var n = new List<string>();
            foreach (Transform t in source)
            {
                if (t == null) continue;
                p.Add(t.position);
                // The scene object's name is the place name on the order card, so name the drop
                // off objects what the player should read, e.g. "Mrs Naidoo's House".
                n.Add(t.name);
            }
            positions = p.ToArray();
            names = n.ToArray();
        }

        /// <summary>Points inside floorBounds if it is assigned, otherwise on a ring around the
        /// origin. angleOffset lets drop offs sit opposite pickups on the ring.</summary>
        private Vector3[] GenerateTestPoints(float angleOffset)
        {
            int count = Mathf.Max(2, testPointCount);
            var points = new Vector3[count];

            if (floorBounds != null)
            {
                Bounds b = floorBounds.bounds;
                float minX = b.min.x + floorEdgeMargin, maxX = b.max.x - floorEdgeMargin;
                float minZ = b.min.z + floorEdgeMargin, maxZ = b.max.z - floorEdgeMargin;

                if (minX < maxX && minZ < maxZ)
                {
                    float groundY = b.max.y + 0.1f;
                    for (int i = 0; i < count; i++)
                        points[i] = new Vector3(UnityEngine.Random.Range(minX, maxX), groundY,
                            UnityEngine.Random.Range(minZ, maxZ));
                    return points;
                }

                Debug.LogError("[OrderManager] floorBounds is too small for floorEdgeMargin. " +
                               "Using the ring instead.", this);
            }

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + angleOffset;
                points[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * testPointRadius;
            }
            return points;
        }

        // ---- spawning -------------------------------------------------------------------------

        private void TrySpawn(float dt)
        {
            _shiftSeconds += dt;

            int max = CurrentMaxOrders;
            if (max != _currentMax)
            {
                bool rising = _currentMax >= 0 && max > _currentMax;
                _currentMax = max;
                MaxOrdersChanged?.Invoke(max);

                // The ramp just allowed another order: bring it in soon rather than waiting out
                // the rest of a long spawn interval.
                if (rising) _timeUntilNextSpawn = Mathf.Min(_timeUntilNextSpawn, 2f);

                if (logActivity)
                    Debug.Log($"[OrderManager] Up to {max} order(s) at once from " +
                              $"{_shiftSeconds / 60f:0.#} minutes into the shift.");
            }

            if (_activeOrders.Count >= max) return;
            if (_dropOffPositions.Length == 0) return;

            _timeUntilNextSpawn -= dt;
            if (_timeUntilNextSpawn > 0f) return;

            SpawnOrder();
            _timeUntilNextSpawn = tuning.spawnInterval;
        }

        private void SpawnOrder()
        {
            FoodType food = tuning.PickFood();
            List<IngredientStop> stops = BuildStops(food);
            if (stops.Count == 0)
            {
                Debug.LogWarning("[OrderManager] Could not build an order: no ingredient sources " +
                                 "and no pickup points. Skipping.", this);
                return;
            }

            int dropIndex = PickIndexAvoidingCollisions(_dropOffPositions, o => o.DropOffPoint);
            bool needsCooking = ProcessingBay.All.Count > 0;

            var order = new Order(
                id: _nextOrderId++,
                stops: stops,
                dropOffPoint: _dropOffPositions[dropIndex],
                dropOffName: NameAt(_dropOffNames, dropIndex, "House"),
                timeLimit: tuning.orderTimeLimit,
                food: food,
                needsCooking: needsCooking,
                cookSeconds: tuning.CookSecondsFor(food),
                burnSeconds: tuning.BurnSecondsFor(food),
                colourIndex: FreeColourIndex());

            if (needsCooking)
            {
                ProcessingBay bay = ProcessingBay.Nearest(stops[stops.Count - 1].Point);
                if (bay != null) order.SuggestBay(bay.Position, bay.BayName);
            }

            order.Activate();
            _activeOrders.Add(order);

            if (logActivity)
            {
                var parts = new List<string>();
                foreach (IngredientStop s in stops) parts.Add($"{s.Name} ({s.PlaceName})");
                Debug.Log($"[OrderManager] Order {order.Id} spawned: {order.FoodName} needs " +
                          $"{string.Join(", ", parts)}" +
                          (needsCooking ? $", cooks {order.CookSeconds:0.#} s" : "") +
                          $", to {order.DropOffName}. {tuning.orderTimeLimit:0} s patience, base pay " +
                          $"R{tuning.BasePayFor(order.Food, order.DesignedDistance):0}. " +
                          $"{_activeOrders.Count}/{CurrentMaxOrders} active.");
            }

            OrderSpawned?.Invoke(order);
        }

        /// <summary>One stop per ingredient, at a random source that supplies it.</summary>
        private List<IngredientStop> BuildStops(FoodType food)
        {
            var stops = new List<IngredientStop>();

            if (food != null && food.ingredients != null)
            {
                foreach (IngredientType ingredient in food.ingredients)
                {
                    if (ingredient == null) continue;

                    var sources = new List<IngredientSource>();
                    foreach (IngredientSource s in IngredientSource.All)
                        if (s.SuppliesIngredient(ingredient)) sources.Add(s);

                    if (sources.Count > 0)
                    {
                        IngredientSource pick = sources[UnityEngine.Random.Range(0, sources.Count)];
                        stops.Add(new IngredientStop(ingredient, ingredient.displayName, pick.Position, pick.PlaceName));
                        continue;
                    }

                    if (_warnedNoSource.Add(ingredient))
                        Debug.LogWarning($"[OrderManager] No Ingredient Source supplies " +
                                         $"'{ingredient.displayName}', so it is collected from a " +
                                         "pickup point instead. Add it to a source's Supplies list.", this);

                    if (_pickupPositions.Length == 0) continue;
                    int index = UnityEngine.Random.Range(0, _pickupPositions.Length);
                    stops.Add(new IngredientStop(ingredient, ingredient.displayName,
                        _pickupPositions[index], NameAt(_pickupNames, index, "Shop")));
                }
            }

            // No recipe: a single pickup of the whole food, the original loop.
            if (stops.Count == 0 && _pickupPositions.Length > 0)
            {
                int index = PickIndexAvoidingCollisions(_pickupPositions,
                    o => o.Stops.Count > 0 ? o.Stops[0].Point : o.DropOffPoint);
                string name = food != null ? food.displayName : "Food";
                stops.Add(new IngredientStop(null, name, _pickupPositions[index],
                    NameAt(_pickupNames, index, "Restaurant")));
            }

            return stops;
        }

        /// <summary>The first order colour no active order is using, so no two live orders ever
        /// share a colour.</summary>
        private int FreeColourIndex()
        {
            for (int i = 0; i < OrderPalette.OrderColourCount; i++)
            {
                bool used = false;
                foreach (Order o in _activeOrders)
                    if (o.ColourIndex == i) { used = true; break; }
                if (!used) return i;
            }
            return (_nextOrderId - 1) % OrderPalette.OrderColourCount;
        }

        private static string NameAt(string[] names, int index, string fallbackPrefix)
        {
            if (names != null && index >= 0 && index < names.Length && !string.IsNullOrEmpty(names[index]))
                return names[index];
            return $"{fallbackPrefix} {index + 1}";
        }

        /// <summary>
        /// Picks a random index into pool, but avoids landing on a point already in use by another
        /// active order, so two orders can't share a drop off. With a small pool and several
        /// orders active at once, a purely random pick collides often.
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

            Debug.LogWarning(
                "[OrderManager] Every point tried was already in use by another active order. " +
                "There may be fewer drop off points than Max Active Orders.", this);
            return UnityEngine.Random.Range(0, pool.Length);
        }

        // ---- resolving ------------------------------------------------------------------------

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

        /// <summary>Called by a Processing Bay when food was left in it too long.</summary>
        public void ResolveBurned(Order order)
        {
            if (order == null || order.IsTerminal || !_activeOrders.Contains(order)) return;

            order.MarkBurned();
            var payout = new OrderPayout
            {
                Timing = DeliveryTiming.Burned,
                Penalty = -tuning.burnPenalty
            };
            Resolve(order, payout);
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
                          $"total R{payout.Total:0}. {_activeOrders.Count}/{CurrentMaxOrders} active.");
            }

            OrderResolved?.Invoke(order, payout.Total);
            OrderPaid?.Invoke(order, payout);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 0.35f, 0.9f);
            foreach (Vector3 p in _pickupPositions) Gizmos.DrawWireSphere(p, 1.5f);

            Gizmos.color = new Color(0.95f, 0.55f, 0.15f, 0.9f);
            foreach (Vector3 p in _dropOffPositions) Gizmos.DrawWireSphere(p, 1.5f);
        }
    }
}
