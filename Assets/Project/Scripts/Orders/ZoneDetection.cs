using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Turns "the vehicle is near an order's current target" into an actual CargoSystem.TryCollect
    /// / TryDeliver call, or, for a multi-leg order mid-route, an Order.AdvanceTransferLeg call.
    /// Uses plain distance checks against each Order's own points, no trigger colliders required
    /// anywhere in the scene. Attach to the same GameObject as the vehicle controller.
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
        [Tooltip("Metres. Radius for an intermediate transfer point on a multi-leg order. " +
                 "Separate from dropOffRadius since a transfer stop and the final drop-off don't " +
                 "have to feel the same size once real geometry exists for either.")]
        [SerializeField] private float transferRadius = 6f;

        /// <summary>Read by OrderBeacon so its ground disc always matches the actual trigger
        /// radius exactly, rather than being a second, independent number that can drift out of
        /// sync with this one.</summary>
        public float PickupRadius => pickupRadius;
        public float DropOffRadius => dropOffRadius;
        public float TransferRadius => transferRadius;

        [Tooltip("How often to run the distance checks, in seconds. 0 means every frame. Was 0.1, " +
                 "which let a fast-moving car clip through a small radius in less time than that " +
                 "and miss the check entirely, so this defaults to every frame now, the cost of a " +
                 "few distance comparisons a frame is not worth trading away for that.")]
        [SerializeField] private float checkInterval = 0f;

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
            CheckCarried();
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
        /// Checks every currently carried order's current target, not just one, since Cargo can
        /// now hold up to three at once. Iterated backwards because TryDeliver removes from
        /// CarriedOrders, and removing while iterating forwards would skip the next item.
        ///
        /// For a multi-leg order, reaching a transfer point (not the final leg) advances the
        /// order's leg counter and nothing else, the vehicle just keeps carrying it and the
        /// beacon/HUD/arrow move on to the next target on their own next refresh, since they all
        /// read Order.CurrentTarget rather than a fixed drop-off point. Only reaching the target
        /// while IsOnFinalCarryLeg counts as an actual delivery.
        /// </summary>
        private void CheckCarried()
        {
            IReadOnlyList<Order> carried = cargoSystem.CarriedOrders;

            for (int i = carried.Count - 1; i >= 0; i--)
            {
                Order order = carried[i];

                Vector3 target = order.CurrentTarget;
                float radius = order.IsOnFinalCarryLeg ? dropOffRadius : transferRadius;

                float distance = Vector3.Distance(transform.position, target);
                if (distance > radius) continue;

                if (order.IsOnFinalCarryLeg)
                {
                    bool delivered = cargoSystem.TryDeliver(orderManager, order);

                    if (delivered && logActivity)
                        Debug.Log($"[ZoneDetection] Drove into drop-off zone for order {order.Id}.");
                }
                else
                {
                    int legJustCompleted = order.CurrentCarryLegNumber;
                    order.AdvanceTransferLeg();

                    if (logActivity)
                        Debug.Log($"[ZoneDetection] Order {order.Id} reached transfer point " +
                                  $"{legJustCompleted}/{order.TotalCarryLegs - 1}, now heading to " +
                                  (order.IsOnFinalCarryLeg ? "the final drop-off." : "its next transfer point."));
                }
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
                    bool final = order.IsOnFinalCarryLeg;
                    Gizmos.color = final
                        ? new Color(0.95f, 0.55f, 0.15f, 0.6f)
                        : new Color(1f, 0.85f, 0.1f, 0.6f);
                    Gizmos.DrawWireSphere(order.CurrentTarget, final ? dropOffRadius : transferRadius);
                }
            }
        }
    }
}