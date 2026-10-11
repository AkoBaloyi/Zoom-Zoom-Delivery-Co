using System;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Where a single order currently sits in its lifecycle. Matches the six states named in the
    /// project plan's Task 7: available, active, collected, carried, delivered, late.
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

    /// <summary>The two steps of an order, for the ticket strip's step badges.</summary>
    public enum OrderStep
    {
        Pickup,
        Deliver
    }

    /// <summary>
    /// A single delivery order. This is plain data plus the transition rules for that data. It
    /// does not know about the vehicle, the cargo slot, or the UI. Those systems read this object
    /// and react to it, and only ever change its state through the methods below, so two systems
    /// can never disagree about what state an order is in.
    /// </summary>
    public class Order
    {
        public readonly int Id;
        public readonly Vector3 PickupPoint;
        public readonly Vector3 DropOffPoint;

        /// <summary>Straight line distance from pickup to drop off, fixed at spawn time. Reward is
        /// calculated against this, never against the distance actually driven, so driving in
        /// circles cannot generate free reward. This is the distance rule from the project plan's
        /// Scope section.</summary>
        public readonly float DesignedDistance;

        public OrderState State { get; private set; }

        /// <summary>Seconds remaining before this order becomes Late. Counts down only while
        /// Active, Collected, or Carried; does nothing once Delivered or Late.</summary>
        public float TimeRemaining { get; private set; }

        /// <summary>The seconds the order started with, so the ticket can draw a draining bar.</summary>
        public float TimeLimit { get; }

        /// <summary>True once the order can no longer change state.</summary>
        public bool IsTerminal => State == OrderState.Delivered || State == OrderState.Late;

        /// <summary>Pickup until the cargo is in the hold, Deliver after that.</summary>
        public OrderStep CurrentStep =>
            State == OrderState.Available || State == OrderState.Active ? OrderStep.Pickup : OrderStep.Deliver;

        /// <summary>Where the vehicle should currently be heading: the pickup while Available or
        /// Active, the drop off once Collected or Carried (and frozen there once resolved).</summary>
        public Vector3 CurrentTarget => CurrentStep == OrderStep.Pickup ? PickupPoint : DropOffPoint;

        /// <summary>Fires whenever State changes, carrying the order itself and the previous
        /// state. UI and scoring subscribe to this rather than polling every frame.</summary>
        public event Action<Order, OrderState> StateChanged;

        public Order(int id, Vector3 pickupPoint, Vector3 dropOffPoint, float timeLimit)
        {
            Id = id;
            PickupPoint = pickupPoint;
            DropOffPoint = dropOffPoint;
            DesignedDistance = Vector3.Distance(pickupPoint, dropOffPoint);
            TimeLimit = timeLimit;
            TimeRemaining = timeLimit;
            State = OrderState.Available;
        }

        /// <summary>Available orders become Active immediately, the same tick they are generated.
        /// Kept as a distinct state, rather than skipped, so a future rule (for example a brief
        /// "incoming" flash before the timer starts) has somewhere to attach without a redesign.</summary>
        public void Activate() => SetState(OrderState.Active);

        /// <summary>Called when the vehicle enters this order's pickup zone with a free cargo
        /// slot. Zone detection decides whether this can happen; this method only records it.</summary>
        public void MarkCollected() => SetState(OrderState.Collected);

        /// <summary>Called once the Cargo System has actually accepted the order into its slot.</summary>
        public void MarkCarried() => SetState(OrderState.Carried);

        /// <summary>Called when the vehicle enters this order's drop off zone while the order is
        /// Carried and TimeRemaining is still above zero.</summary>
        public void MarkDelivered() => SetState(OrderState.Delivered);

        /// <summary>Called when TimeRemaining reaches zero in any non terminal state.</summary>
        public void MarkLate() => SetState(OrderState.Late);

        /// <summary>Advances the countdown. Returns true the moment this call causes the order to
        /// go Late, so the order manager can react without polling TimeRemaining itself.</summary>
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