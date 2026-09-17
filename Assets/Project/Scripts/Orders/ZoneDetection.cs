using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Turns "the vehicle is near an order's pickup or drop-off point" into an actual
    /// CargoSystem.TryCollect / TryDeliver call. Uses plain distance checks against each active
    /// Order's own PickupPoint/DropOffPoint, no trigger colliders required anywhere in the scene.
    /// Attach to the same GameObject as the vehicle controller.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZoneDetection : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private CargoSystem cargoSystem;

        [Header("Zone size")]
        [Tooltip("Metres. Tune to match whatever visual marker ends up on the ground.")]
        [SerializeField] private float pickupRadius = 6f;
        [SerializeField] private float dropOffRadius = 6f;

        [Tooltip("How often to run the distance checks, in seconds. 0 means every frame.")]
        [SerializeField] private float checkInterval = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool logActivity = true;
        [SerializeField] private bool drawGizmos = true;

        private float _timeUntilNextCheck;

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

            CheckPickups();
            CheckDropOff();
        }

        private void CheckPickups()
        {
            if (!cargoSystem.HasCapacity) return;

            foreach (Order order in orderManager.ActiveOrders)
            {
                if (order.State != OrderState.Active) continue;

                float distance = Vector3.Distance(transform.position, order.PickupPoint);
                if (distance > pickupRadius) continue;

                bool collected = cargoSystem.TryCollect(order);

                if (collected && logActivity)
                    Debug.Log($"[ZoneDetection] Drove into pickup zone for order {order.Id}.");

                if (collected) return;
            }
        }

        /// <summary>
        /// Checks every currently carried order's drop-off point, not just one, since Cargo can now
        /// hold up to three at once. Iterated backwards because TryDeliver removes from
        /// CarriedOrders, and removing while iterating forwards would skip the next item.
        /// </summary>
        private void CheckDropOff()
        {
            IReadOnlyList<Order> carried = cargoSystem.CarriedOrders;

            for (int i = carried.Count - 1; i >= 0; i--)
            {
                Order order = carried[i];

                float distance = Vector3.Distance(transform.position, order.DropOffPoint);
                if (distance > dropOffRadius) continue;

                bool delivered = cargoSystem.TryDeliver(orderManager, order);

                if (delivered && logActivity)
                    Debug.Log($"[ZoneDetection] Drove into drop-off zone for order {order.Id}.");
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || orderManager == null) return;

            foreach (Order order in orderManager.ActiveOrders)
            {
                if (order.State == OrderState.Active)
                {
                    Gizmos.color = new Color(0.2f, 0.85f, 0.35f, 0.6f);
                    Gizmos.DrawWireSphere(order.PickupPoint, pickupRadius);
                }
                else if (order.State == OrderState.Carried)
                {
                    Gizmos.color = new Color(0.95f, 0.55f, 0.15f, 0.6f);
                    Gizmos.DrawWireSphere(order.DropOffPoint, dropOffRadius);
                }
            }
        }
    }
}