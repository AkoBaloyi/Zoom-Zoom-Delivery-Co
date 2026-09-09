using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// The MVP's one cargo slot. This is the thing that actually turns an Active order into a
    /// Collected, then Carried, order, and later into a Delivered one, by accepting or refusing it
    /// into the slot.
    ///
    /// WHY THIS SITS BETWEEN ZONE DETECTION AND THE ORDER
    /// Zone detection will know WHEN the vehicle is at a pickup or drop-off point. It should never
    /// have to know whether a slot is free, that decision belongs here, in one place, for the same
    /// reason VehicleController is the only thing that decides whether the car is grounded. Two
    /// systems independently deciding "is there room" is how they end up disagreeing.
    ///
    /// WHY COLLECTED AND CARRIED HAPPEN TOGETHER HERE
    /// Order.cs already treats Collected as a same-tick pass-through into Carried, since nothing in
    /// this design distinguishes "physically touched" from "now in the vehicle" as separate moments.
    /// This system is what actually makes that call, by accepting the order into the slot the
    /// instant it decides there's room.
    ///
    /// FUTURE, NOT YET BUILT
    /// The MVP is one slot. Tasks 19 and 25 (two-slot, then three-slot) will change slotCount and
    /// generalise the single Order field below into a small array, without needing to change how
    /// TryCollect or TryDeliver are called from outside, that's the whole point of keeping the
    /// public methods slot-count agnostic already.
    /// </summary>
    [DisallowMultipleComponent]
    public class CargoSystem : MonoBehaviour
    {
        [Header("Debug")]
        [Tooltip("Log every accepted pickup, refused pickup, and delivery to the console.")]
        [SerializeField] private bool logActivity = true;

        /// <summary>The order currently occupying the slot, or null if the slot is empty. Public so
        /// UI can show what's being carried without this system needing to know UI exists.</summary>
        public Order CurrentOrder { get; private set; }

        /// <summary>True while the one slot is empty. Zone detection checks this before even
        /// attempting a pickup, though TryCollect refuses safely either way.</summary>
        public bool HasCapacity => CurrentOrder == null;

        /// <summary>
        /// Called by zone detection when the vehicle enters an order's pickup zone. Only succeeds if
        /// the slot is free AND the order is still Active (not already collected by an earlier call,
        /// not Late). Both checks matter: without the second one, a zone that fires twice in one
        /// frame could collect an order that already went Late a moment earlier.
        /// </summary>
        public bool TryCollect(Order order)
        {
            if (order == null) return false;

            if (!HasCapacity)
            {
                Refuse($"order {order.Id}: cargo slot already holds order {CurrentOrder.Id}");
                return false;
            }

            if (order.State != OrderState.Active)
            {
                Refuse($"order {order.Id}: not Active (state was {order.State})");
                return false;
            }

            order.MarkCollected();
            order.MarkCarried();
            CurrentOrder = order;

            if (logActivity)
                Debug.Log($"[CargoSystem] Order {order.Id} collected and now carried.");

            return true;
        }

        /// <summary>
        /// Called by zone detection when the vehicle enters a drop-off zone. expectedOrder is the
        /// order that particular zone belongs to, checked against what's actually in the slot so a
        /// player can never deliver the wrong parcel to the wrong door. Clears the slot and resolves
        /// the order through the OrderManager in the same call, so the two can never drift out of
        /// sync, a slot that thinks it's full after its order has already resolved is exactly the
        /// kind of state bug this method exists to prevent.
        /// </summary>
        public bool TryDeliver(OrderManager orderManager, Order expectedOrder)
        {
            if (orderManager == null || expectedOrder == null) return false;

            if (CurrentOrder != expectedOrder)
            {
                Refuse($"drop-off does not match what's carried " +
                       $"(carrying {(CurrentOrder != null ? CurrentOrder.Id.ToString() : "nothing")}, " +
                       $"zone wants order {expectedOrder.Id})");
                return false;
            }

            Order delivered = CurrentOrder;
            CurrentOrder = null;
            orderManager.ResolveDelivery(delivered);

            if (logActivity)
                Debug.Log($"[CargoSystem] Order {delivered.Id} delivered, slot now free.");

            return true;
        }

        /// <summary>
        /// If the order currently in the slot goes Late while still carried (the player never
        /// reached the drop-off in time), the slot has to be freed even though nobody delivered
        /// anything. OrderManager.OrderResolved fires for this case too; wire this up to that event
        /// so a Late order can never leave the slot permanently stuck full.
        /// </summary>
        public void ClearIfResolved(Order order)
        {
            if (CurrentOrder == order) CurrentOrder = null;
        }

        private void Refuse(string reason)
        {
            if (logActivity) Debug.Log($"[CargoSystem] Refused: {reason}.", this);
        }
    }
}
