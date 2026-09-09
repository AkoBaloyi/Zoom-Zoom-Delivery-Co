using System;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Where a single order currently sits in its lifecycle. Matches the six states named in the
    /// group's task table: available, active, collected, carried, delivered, late.
    /// </summary>
    public enum OrderState
    {
        Available,
        Active,
        Collected,
        Carried,
        Delivered,
        Late
    }

    /// <summary>
    /// A single delivery order. This is plain data plus the transition rules for that data, it
    /// does not know about the vehicle, the cargo slot, or the UI, those systems read this object
    /// and react to it, they never reach in and change its state directly except through the
    /// methods below. Keeping the transitions here, in one place, is what stops two systems from
    /// disagreeing about what state an order is in.
    /// </summary>
    public class Order
    {
        public readonly int Id;
        public readonly Vector3 PickupPoint;
        public readonly Vector3 DropOffPoint;

        /// <summary>Straight-line distance from pickup to drop-off, fixed at spawn time. Reward is
        /// calculated against this, never against the distance actually driven, so driving in
        /// circles cannot generate free value.</summary>
        public readonly float DesignedDistance;

        public OrderState State { get; private set; }

        /// <summary>Seconds remaining before this order becomes Late. Counts down only while
        /// Active, Collected, or Carried; does nothing once Delivered or Late.</summary>
        public float TimeRemaining { get; private set; }

        /// <summary>Fires whenever State changes, carrying the order itself and the previous
        /// state. UI and scoring subscribe to this rather than polling every frame.</summary>
        public event Action<Order, OrderState> StateChanged;

        public Order(int id, Vector3 pickupPoint, Vector3 dropOffPoint, float timeLimit)
        {
            Id = id;
            PickupPoint = pickupPoint;
            DropOffPoint = dropOffPoint;
            DesignedDistance = Vector3.Distance(pickupPoint, dropOffPoint);
            TimeRemaining = timeLimit;
            State = OrderState.Available;
        }

        /// <summary>Available orders become Active immediately, the same tick they're generated.
        /// Kept as a distinct state, rather than skipped, purely so a future rule (e.g. a brief
        /// "incoming" flash before the timer starts) has somewhere to attach without a redesign.</summary>
        public void Activate() => SetState(OrderState.Active);

        /// <summary>Called by zone detection when the vehicle enters this order's pickup zone
        /// with a free cargo slot. Zone detection decides WHETHER this can happen (slot free or
        /// not); this method only records that it did.</summary>
        public void MarkCollected() => SetState(OrderState.Collected);

        /// <summary>Called once the Cargo System has actually accepted the order into its slot.</summary>
        public void MarkCarried() => SetState(OrderState.Carried);

        /// <summary>Called by zone detection when the vehicle enters this order's drop-off zone
        /// while the order is Carried and TimeRemaining is still above zero.</summary>
        public void MarkDelivered() => SetState(OrderState.Delivered);

        /// <summary>Called when TimeRemaining reaches zero in any non-terminal state.</summary>
        public void MarkLate() => SetState(OrderState.Late);

        /// <summary>True once the order can no longer change state.</summary>
        public bool IsTerminal => State == OrderState.Delivered || State == OrderState.Late;

        /// <summary>Advances the countdown. Returns true the moment this call causes the order to
        /// go Late, so the caller (the order manager) knows to react without also having to poll
        /// TimeRemaining itself.</summary>
        public bool Tick(float deltaTime)
        {
            if (IsTerminal) return false;

            TimeRemaining -= deltaTime;
            if (TimeRemaining > 0f) return false;

            TimeRemaining = 0f;
            MarkLate();
            return true;
        }

        private void SetState(OrderState newState)
        {
            if (State == newState) return;

            OrderState previous = State;
            State = newState;
            StateChanged?.Invoke(this, previous);
        }
    }
}
