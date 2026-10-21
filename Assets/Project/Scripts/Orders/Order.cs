using System;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Where a single order currently sits in its lifecycle. Matches the six states named in the
    /// project plan's Task 7: available, active, collected, carried, delivered, late. Late is
    /// shown to the player as "cancelled": the patience bar ran out and the order failed.
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

    /// <summary>The two steps of an order, for the order card's step badges.</summary>
    public enum OrderStep
    {
        Pickup,
        Deliver
    }

    /// <summary>How an order ended, which decides its time bonus or penalty.</summary>
    public enum DeliveryTiming
    {
        Early,
        OnTime,
        Late,
        Failed
    }

    /// <summary>
    /// Everything that went into what an order paid, so the popups and the end of shift summary
    /// can show bonuses and penalties separately rather than one number.
    /// </summary>
    public class OrderPayout
    {
        public DeliveryTiming Timing;

        /// <summary>The food's base pay. 0 for a failed order.</summary>
        public float BasePay;

        /// <summary>Early bonus (positive) or late deduction (negative).</summary>
        public float TimeAdjustment;

        /// <summary>Pay lost to damaged cargo, when cargo condition is switched on. Zero or
        /// negative.</summary>
        public float ConditionLoss;

        /// <summary>The fixed cost of a cancelled order. Zero or negative.</summary>
        public float Penalty;

        public float Total => BasePay + TimeAdjustment + ConditionLoss + Penalty;
    }

    /// <summary>
    /// A single delivery order. Plain data plus the transition rules for that data. It does not
    /// know about the vehicle, the cargo slot, or the UI. Those systems read this object and
    /// react to it, and only change it through the methods below, so two systems can never
    /// disagree about what state an order is in.
    /// </summary>
    public class Order
    {
        public readonly int Id;
        public readonly Vector3 PickupPoint;
        public readonly Vector3 DropOffPoint;

        /// <summary>The food being delivered. Null means a plain delivery with no food.</summary>
        public readonly FoodType Food;

        /// <summary>Place names for the order card, taken from the pickup and drop off objects'
        /// names in the scene.</summary>
        public readonly string PickupName;
        public readonly string DropOffName;

        /// <summary>Straight line distance from pickup to drop off, fixed at spawn time.</summary>
        public readonly float DesignedDistance;

        public OrderState State { get; private set; }

        /// <summary>Seconds left on the order's patience bar. Counts down until Delivered or
        /// Late.</summary>
        public float TimeRemaining { get; private set; }

        /// <summary>The seconds the patience bar started with.</summary>
        public float TimeLimit { get; }

        /// <summary>0 to 1, how much of the patience bar is left.</summary>
        public float PatienceFraction => TimeLimit > 0.01f ? Mathf.Clamp01(TimeRemaining / TimeLimit) : 0f;

        /// <summary>Set by OrderManager when the order resolves.</summary>
        public OrderPayout Payout { get; private set; }

        public string FoodName => Food != null ? Food.displayName : "Delivery";

        public bool IsTerminal => State == OrderState.Delivered || State == OrderState.Late;

        /// <summary>Pickup until the cargo is in the hold, Deliver after that.</summary>
        public OrderStep CurrentStep =>
            State == OrderState.Available || State == OrderState.Active ? OrderStep.Pickup : OrderStep.Deliver;

        /// <summary>Where the vehicle should currently be heading.</summary>
        public Vector3 CurrentTarget => CurrentStep == OrderStep.Pickup ? PickupPoint : DropOffPoint;

        public event Action<Order, OrderState> StateChanged;

        public Order(int id, Vector3 pickupPoint, Vector3 dropOffPoint, float timeLimit,
            FoodType food = null, string pickupName = null, string dropOffName = null)
        {
            Id = id;
            PickupPoint = pickupPoint;
            DropOffPoint = dropOffPoint;
            Food = food;
            PickupName = string.IsNullOrEmpty(pickupName) ? "Pickup" : pickupName;
            DropOffName = string.IsNullOrEmpty(dropOffName) ? "Drop off" : dropOffName;
            DesignedDistance = Vector3.Distance(pickupPoint, dropOffPoint);
            TimeLimit = timeLimit;
            TimeRemaining = timeLimit;
            State = OrderState.Available;
        }

        public void Activate() => SetState(OrderState.Active);
        public void MarkCollected() => SetState(OrderState.Collected);
        public void MarkCarried() => SetState(OrderState.Carried);
        public void MarkDelivered() => SetState(OrderState.Delivered);
        public void MarkLate() => SetState(OrderState.Late);

        public void SetPayout(OrderPayout payout) => Payout = payout;

        /// <summary>Advances the patience countdown. Returns true the moment this call causes
        /// the order to go Late (cancelled).</summary>
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