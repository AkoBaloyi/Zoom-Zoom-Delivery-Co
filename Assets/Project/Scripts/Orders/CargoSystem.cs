using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// The cargo hold: accepts orders up to Capacity, refusing a pickup once full. Internally this
    /// is a list rather than one nullable order, since Capacity will genuinely change as the group
    /// reaches Tasks 19 and 25 (two-slot, then three-slot), and a list scales to that without
    /// needing to be rewritten, unlike a single field would.
    ///
    /// STARTING CAPACITY: 1, matching Task 8 and the pre-alpha milestone. It was briefly set to 3
    /// directly in the Inspector, skipping past Tasks 19 and 25 (scheduled after Milestone 1 and
    /// Milestone 2 respectively) and past the XP/money gating Scope describes for those upgrades.
    /// Reverted at the time, since representing capacity that hasn't been earned through any
    /// system that exists yet misrepresents where the build actually is, not just the design
    /// intent. TryUnlockCapacity below is the hook for Task 19 and Task 25: the upgrade screen
    /// (Task 26) calls it when the player buys the two slot or three slot upgrade, so capacity
    /// only ever rises through the purchase the plan describes, not by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public class CargoSystem : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Found automatically if left empty. Used only to subscribe to OrderResolved so " +
                 "a Late-while-carried order always frees its slot, without needing any other " +
                 "script to wire that up.")]
        [SerializeField] private OrderManager orderManager;

        [Tooltip("Optional. Found automatically if present on this GameObject. When it is " +
                 "switched on, a delivery's pay is scaled by the cargo's condition.")]
        [SerializeField] private CargoCondition cargoCondition;

        [Header("Capacity")]
        [Tooltip("How many orders can be carried at once. MVP/pre-alpha scope is 1, per Task 8. " +
                 "Do not raise this until the task it actually belongs to is reached: Task 19 " +
                 "(two-slot, scheduled after Milestone 1) or Task 25 (three-slot, scheduled after " +
                 "Milestone 2), since raising it earlier represents progression the player hasn't " +
                 "earned through any system that exists yet.")]
        [SerializeField] private int capacity = 1;

        [Header("Debug")]
        [SerializeField] private bool logActivity = true;

        private readonly List<Order> _carriedOrders = new List<Order>();

        public IReadOnlyList<Order> CarriedOrders => _carriedOrders;
        public bool HasCapacity => _carriedOrders.Count < capacity;
        public int Capacity => capacity;
        public int SlotsUsed => _carriedOrders.Count;

        /// <summary>Fires the moment an order is actually accepted into the cargo slot (after
        /// TryCollect succeeds). CargoCondition subscribes to this to start tracking that order's
        /// condition from 100.</summary>
        public event Action<Order> OrderCarried;

        /// <summary>Fires the moment an order leaves the cargo slot for any reason: delivered,
        /// gone Late while still carried, or otherwise cleared. CargoCondition subscribes to this
        /// to stop tracking an order that no longer occupies a slot.</summary>
        public event Action<Order> OrderReleased;

        /// <summary>Fires whenever capacity actually changes, so UI (OrderHUD's slot count) can
        /// react immediately rather than only noticing next time it happens to poll Capacity.</summary>
        public event Action<int> CapacityChanged;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (cargoCondition == null) cargoCondition = GetComponent<CargoCondition>();

            if (orderManager == null)
            {
                Debug.LogError(
                    "[CargoSystem] No OrderManager found. A Late order that was still carried " +
                    "will NOT free its slot, since there is nothing to read the resolution from.",
                    this);
                return;
            }

            // This used to be the job of a temporary test harness. Moving it here means the
            // slot is guaranteed to free correctly regardless of what else exists in the scene,
            // rather than depending on some other script remembering to wire it up.
            orderManager.OrderResolved += HandleOrderResolved;
        }

        private void OnDestroy()
        {
            if (orderManager != null) orderManager.OrderResolved -= HandleOrderResolved;
        }

        private void HandleOrderResolved(Order order, float value)
        {
            ClearIfResolved(order);
        }

        public bool TryCollect(Order order)
        {
            if (order == null) return false;

            if (!HasCapacity)
            {
                Refuse($"order {order.Id}: cargo full ({_carriedOrders.Count}/{capacity})");
                return false;
            }

            if (order.State != OrderState.Active)
            {
                Refuse($"order {order.Id}: not Active (state was {order.State})");
                return false;
            }

            order.MarkCollected();
            order.MarkCarried();
            _carriedOrders.Add(order);
            OrderCarried?.Invoke(order);

            if (logActivity)
                Debug.Log($"[CargoSystem] Order {order.Id} collected and now carried. " +
                          $"{_carriedOrders.Count}/{capacity} slots used.");

            return true;
        }

        public bool TryDeliver(OrderManager orderManager, Order expectedOrder)
        {
            if (orderManager == null || expectedOrder == null) return false;

            if (!_carriedOrders.Contains(expectedOrder))
            {
                Refuse($"drop-off does not match anything carried (zone wants order {expectedOrder.Id})");
                return false;
            }

            if (expectedOrder.State != OrderState.Carried)
            {
                Refuse($"order {expectedOrder.Id}: not Carried (state was {expectedOrder.State})");
                return false;
            }

            float conditionMultiplier = cargoCondition != null
                ? cargoCondition.GetConditionMultiplier(expectedOrder)
                : 1f;

            _carriedOrders.Remove(expectedOrder);
            OrderReleased?.Invoke(expectedOrder);
            orderManager.ResolveDelivery(expectedOrder, conditionMultiplier);

            if (logActivity)
                Debug.Log($"[CargoSystem] Order {expectedOrder.Id} delivered. " +
                          $"{_carriedOrders.Count}/{capacity} slots used.");

            return true;
        }

        public void ClearIfResolved(Order order)
        {
            if (_carriedOrders.Remove(order)) OrderReleased?.Invoke(order);
        }

        /// <summary>
        /// The one legitimate way capacity is ever allowed to rise above what's set in the
        /// Inspector, called by the upgrade screen (Task 26) when the player buys the Task 19 or
        /// Task 25 cargo upgrade. Refuses to lower capacity (that would be a downgrade
        /// bug, not an unlock) and refuses to re-apply the same value, so it's safe to call every
        /// time a level-up happens without checking "did we already unlock this" elsewhere.
        /// </summary>
        public bool TryUnlockCapacity(int newCapacity)
        {
            if (newCapacity <= capacity) return false;

            int previous = capacity;
            capacity = newCapacity;
            CapacityChanged?.Invoke(capacity);

            if (logActivity)
                Debug.Log($"[CargoSystem] Capacity unlocked: {previous} -> {capacity}.", this);

            return true;
        }

        private void Refuse(string reason)
        {
            if (logActivity) Debug.Log($"[CargoSystem] Refused: {reason}.", this);
        }
    }
}