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
    /// CURRENT CAPACITY: 1, matching Task 8 and the pre-alpha milestone. It was briefly set to 3
    /// directly, skipping past Tasks 19 and 25 (scheduled after Milestone 1 and Milestone 2
    /// respectively) and past the XP/money gating Scope describes for those upgrades. Reverted:
    /// representing capacity that hasn't been earned through any system that exists yet
    /// misrepresents where the build actually is, not just the design intent.
    /// </summary>
    [DisallowMultipleComponent]
    public class CargoSystem : MonoBehaviour
    {
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

            _carriedOrders.Remove(expectedOrder);
            orderManager.ResolveDelivery(expectedOrder);

            if (logActivity)
                Debug.Log($"[CargoSystem] Order {expectedOrder.Id} delivered. " +
                          $"{_carriedOrders.Count}/{capacity} slots used.");

            return true;
        }

        public void ClearIfResolved(Order order)
        {
            _carriedOrders.Remove(order);
        }

        private void Refuse(string reason)
        {
            if (logActivity) Debug.Log($"[CargoSystem] Refused: {reason}.", this);
        }
    }
}