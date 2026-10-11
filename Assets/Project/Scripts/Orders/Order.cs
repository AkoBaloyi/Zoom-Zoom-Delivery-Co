using System;
using System.Collections.Generic;
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
    ///
    /// MULTI-LEG ORDERS (Milestone 2 exploration)
    /// An order can carry zero or more transfer points between its pickup and its final
    /// drop-off. With zero transfer points this behaves exactly like the Milestone 1 order: one
    /// leg to the pickup, one leg to the drop-off. With one or more, the Carried state now spans
    /// several legs in sequence, pickup to transfer 1, transfer 1 to transfer 2 (if any), the
    /// last transfer to the final drop-off, all still inside the same Carried state. The state
    /// machine itself is unchanged; a leg counter sits alongside it rather than adding new
    /// states, since "which leg" is not a state a player needs to see reflected in the six-state
    /// machine, only in the UI's target and label.
    /// </summary>
    public class Order
    {
        public readonly int Id;
        public readonly Vector3 PickupPoint;
        public readonly Vector3 DropOffPoint;

        /// <summary>Intermediate stops between PickupPoint and DropOffPoint. Empty for a direct,
        /// Milestone-1-style order. Never null, always a plain list even when empty, so callers
        /// don't need a separate null check before reading Count.</summary>
        public readonly IReadOnlyList<Vector3> TransferPoints;

        /// <summary>Straight-line distance from pickup to drop-off, fixed at spawn time. Reward is
        /// calculated against this, never against the distance actually driven, so driving in
        /// circles cannot generate free value. For a multi-leg order this is still pickup to
        /// final drop-off directly, not the sum of legs, for the same reason.</summary>
        public readonly float DesignedDistance;

        public OrderState State { get; private set; }

        /// <summary>Seconds remaining before this order becomes Late. Counts down only while
        /// Active, Collected, or Carried; does nothing once Delivered or Late.</summary>
        public float TimeRemaining { get; private set; }

        /// <summary>How many transfer points into TransferPoints the vehicle has already passed
        /// while carrying this order. 0 means "heading to TransferPoints[0], or straight to
        /// DropOffPoint if there are none."</summary>
        public int TransferLegsCompleted { get; private set; }

        /// <summary>True once there are no more transfer points left, i.e. CurrentTarget is
        /// DropOffPoint itself and reaching it should resolve the order as Delivered rather than
        /// just advancing to the next leg.</summary>
        public bool IsOnFinalCarryLeg => TransferLegsCompleted >= TransferPoints.Count;

        /// <summary>1-based leg number for display, out of TotalCarryLegs. Leg 1 is
        /// pickup-to-first-transfer (or pickup-to-dropoff if there are no transfer points).</summary>
        public int CurrentCarryLegNumber => TransferLegsCompleted + 1;

        /// <summary>Total number of legs once carried: one more than the number of transfer
        /// points, since the final leg into DropOffPoint always exists.</summary>
        public int TotalCarryLegs => TransferPoints.Count + 1;

        /// <summary>Where the vehicle should currently be heading. PickupPoint while Available or
        /// Active; the next transfer point, or DropOffPoint once there are none left, once
        /// Collected or Carried; DropOffPoint (frozen) once resolved.</summary>
        public Vector3 CurrentTarget
        {
            get
            {
                if (State == OrderState.Available || State == OrderState.Active) return PickupPoint;
                if (IsOnFinalCarryLeg) return DropOffPoint;
                return TransferPoints[TransferLegsCompleted];
            }
        }

        /// <summary>Fires whenever State changes, carrying the order itself and the previous
        /// state. UI and scoring subscribe to this rather than polling every frame.</summary>
        public event Action<Order, OrderState> StateChanged;

        /// <summary>Fires whenever a transfer leg completes while still Carried, i.e. every
        /// advance that does NOT resolve the order. UI uses this to know when to refresh which
        /// leg it's displaying without waiting for the (rarer) StateChanged event.</summary>
        public event Action<Order> LegAdvanced;

        public Order(int id, Vector3 pickupPoint, Vector3 dropOffPoint, float timeLimit,
            IReadOnlyList<Vector3> transferPoints = null)
        {
            Id = id;
            PickupPoint = pickupPoint;
            DropOffPoint = dropOffPoint;
            TransferPoints = transferPoints ?? Array.Empty<Vector3>();
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

        /// <summary>Called by zone detection when the vehicle enters a Carried order's current
        /// transfer point and at least one more leg remains after it. Does not change State,
        /// carrying stays carrying, only which point counts as CurrentTarget moves on.</summary>
        public void AdvanceTransferLeg()
        {
            if (State != OrderState.Carried) return;
            if (IsOnFinalCarryLeg) return; // nothing left to advance into; the final leg ends in MarkDelivered instead

            TransferLegsCompleted++;
            LegAdvanced?.Invoke(this);
        }

        /// <summary>Called by zone detection when the vehicle enters this order's drop-off zone
        /// while the order is Carried, on its final leg, and TimeRemaining is still above zero.</summary>
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