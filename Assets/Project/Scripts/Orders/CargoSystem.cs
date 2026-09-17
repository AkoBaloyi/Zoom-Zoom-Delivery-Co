using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// The cargo hold: accepts orders up to Capacity, refusing a pickup once full. This used to be
    /// a single nullable slot; it's now a list, since capacity is 3, not 1, per direct request.
    ///
    /// A NOTE ON SCOPE
    /// The Design Document's Scope section describes the MVP starting at one slot, with two and
    /// three slots arriving later as an XP/money-gated upgrade (Tasks 19 and 25). Setting capacity
    /// to 3 here skips that gating entirely rather than building toward it. That's a fine call to
    /// make deliberately for prototyping and testing the core loop with more pressure, it's a
    /// different thing from having decided the progression system isn't needed. Worth a line in
    /// Changes either way, since it's a real, visible divergence from what's written down.
    /// </summary>
    [DisallowMultipleComponent]
    public class CargoSystem : MonoBehaviour
    {
        [Header("Capacity")]
        [Tooltip("How many orders can be carried at once.")]
        [SerializeField] private int capacity = 3;

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