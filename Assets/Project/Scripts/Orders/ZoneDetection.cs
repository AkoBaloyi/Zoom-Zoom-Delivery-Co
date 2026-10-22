using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Turns "the car is near something" into the matching CargoSystem call, using plain distance
    /// checks, so no trigger colliders are needed anywhere in the scene. Every frame it checks:
    ///   Ingredient Sources: collects any ingredient an active order still needs.
    ///   Processing Bays: takes out ready food, then puts in the next order's ingredients.
    ///   Drop offs: delivers any cooked meal whose customer is here.
    /// Attach to the same GameObject as the vehicle controller.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZoneDetection : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private CargoSystem cargoSystem;

        [Header("Zone size (metres)")]
        [Tooltip("Ingredient sources and pickup points.")]
        [SerializeField] private float pickupRadius = 6f;
        [Tooltip("Processing bays.")]
        [SerializeField] private float bayRadius = 7f;
        [Tooltip("Customer drop offs.")]
        [SerializeField] private float dropOffRadius = 6f;

        /// <summary>Read by the beacons and bays so their ground discs always match the real
        /// zone sizes, rather than being separate numbers that can drift out of sync.</summary>
        public float PickupRadius => pickupRadius;
        public float BayRadius => bayRadius;
        public float DropOffRadius => dropOffRadius;

        [Tooltip("How often to run the distance checks, in seconds. 0 means every frame, which " +
                 "stops a fast car clipping through a small radius between checks.")]
        [SerializeField] private float checkInterval = 0f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;

        private float _timeUntilNextCheck;
        private readonly List<IngredientSource> _nearbySources = new List<IngredientSource>();

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (cargoSystem == null) cargoSystem = FindAnyObjectByType<CargoSystem>();

            if (orderManager == null || cargoSystem == null)
            {
                Debug.LogError(
                    "[ZoneDetection] Needs both an OrderManager and a CargoSystem in the scene. " +
                    "Deliveries cannot happen without both.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            _timeUntilNextCheck -= Time.deltaTime;
            if (_timeUntilNextCheck > 0f) return;
            _timeUntilNextCheck = checkInterval;

            CheckIngredients();
            CheckBays();
            CheckDeliveries();
        }

        private bool InRange(Vector3 point, float radius)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f; // a marker on a kerb or a ramp still counts
            return offset.sqrMagnitude <= radius * radius;
        }

        /// <summary>Oldest order first, so when two orders need the same ingredient the one
        /// closer to running out of patience gets it.</summary>
        private void CheckIngredients()
        {
            _nearbySources.Clear();
            foreach (IngredientSource source in IngredientSource.All)
                if (InRange(source.Position, pickupRadius)) _nearbySources.Add(source);

            IReadOnlyList<Order> orders = orderManager.ActiveOrders;
            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];
                if (order.State != OrderState.Active) continue;

                foreach (IngredientStop stop in order.Stops)
                {
                    if (stop.Collected) continue;
                    if (!InRange(stop.Point, pickupRadius) && !NearbySourceSupplies(stop.Ingredient)) continue;

                    cargoSystem.TryCollectIngredient(order, stop);
                    if (!cargoSystem.HasIngredientRoom) return;
                }
            }
        }

        private bool NearbySourceSupplies(IngredientType ingredient)
        {
            if (ingredient == null) return false;
            foreach (IngredientSource source in _nearbySources)
                if (source.SuppliesIngredient(ingredient)) return true;
            return false;
        }

        private void CheckBays()
        {
            foreach (ProcessingBay bay in ProcessingBay.All)
            {
                if (!InRange(bay.Position, bayRadius)) continue;

                // Take out first, so one stop can collect the burger and put the soup in.
                cargoSystem.TryCollectFromBay(bay);
                cargoSystem.TryLoadBay(bay);
            }
        }

        /// <summary>Iterated backwards because TryDeliver removes from CarriedOrders.</summary>
        private void CheckDeliveries()
        {
            IReadOnlyList<Order> carried = cargoSystem.CarriedOrders;

            for (int i = carried.Count - 1; i >= 0; i--)
            {
                Order order = carried[i];
                if (order.State != OrderState.Carried) continue;
                if (!InRange(order.DropOffPoint, dropOffRadius)) continue;

                cargoSystem.TryDeliver(orderManager, order);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.color = new Color(0.20f, 0.60f, 1.00f, 0.6f);
            foreach (IngredientSource s in IngredientSource.All) Gizmos.DrawWireSphere(s.Position, pickupRadius);

            Gizmos.color = new Color(0.98f, 0.55f, 0.12f, 0.6f);
            foreach (ProcessingBay b in ProcessingBay.All) Gizmos.DrawWireSphere(b.Position, bayRadius);

            if (orderManager == null) return;
            Gizmos.color = new Color(0.20f, 0.85f, 0.35f, 0.6f);
            foreach (Order order in orderManager.ActiveOrders)
                if (order.State == OrderState.Carried) Gizmos.DrawWireSphere(order.DropOffPoint, dropOffRadius);
        }
    }
}