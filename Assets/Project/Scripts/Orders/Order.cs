using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Where a single order currently sits in its lifecycle.
    ///   Active   the player is collecting its ingredients (and, once all are in the car,
    ///            taking them to a Processing Bay)
    ///   Cooking  in a Processing Bay, cooking for an exact time
    ///   Ready    cooked and waiting in the bay; burns if left too long
    ///   Carried  the cooked food is in the car, heading to the customer
    ///   Delivered, Late (patience ran out, shown as cancelled), Burned
    /// </summary>
    public enum OrderState
    {
        Available,
        Active,
        Cooking,
        Ready,
        Carried,
        Delivered,
        Late,
        Burned
    }

    /// <summary>The three steps of an order, for the order card and the map markers.</summary>
    public enum OrderStep
    {
        Gather,
        Cook,
        Deliver
    }

    /// <summary>How an order ended, which decides its time bonus or penalty.</summary>
    public enum DeliveryTiming
    {
        Early,
        OnTime,
        Late,
        Failed,
        Burned
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

        /// <summary>The fixed cost of a cancelled or burned order. Zero or negative.</summary>
        public float Penalty;

        public float Total => BasePay + TimeAdjustment + ConditionLoss + Penalty;
    }

    /// <summary>One ingredient an order needs, and where the order suggests getting it.</summary>
    public class IngredientStop
    {
        /// <summary>Null for a plain delivery with no recipe, collected from a pickup point.</summary>
        public readonly IngredientType Ingredient;
        public readonly string Name;
        public readonly Vector3 Point;
        public readonly string PlaceName;
        public bool Collected { get; internal set; }

        public string ChipLabel => Ingredient != null ? Ingredient.ChipLabel
            : (Name.Length <= 5 ? Name.ToUpper() : Name.Substring(0, 4).ToUpper());

        public IngredientStop(IngredientType ingredient, string name, Vector3 point, string placeName)
        {
            Ingredient = ingredient;
            Name = string.IsNullOrEmpty(name) ? "Food" : name;
            Point = point;
            PlaceName = string.IsNullOrEmpty(placeName) ? "Pickup" : placeName;
        }
    }

    /// <summary>
    /// A single delivery order. Plain data plus the transition rules for that data. It does not
    /// know about the vehicle, the cargo hold, the bays or the UI. Those systems read this object
    /// and only change it through the methods below, so two systems can never disagree about
    /// what state an order is in.
    /// </summary>
    public class Order
    {
        public readonly int Id;

        /// <summary>Which of the order colours this order uses. The card, its beacons, its arrow
        /// and its GPS line all use this one colour, so the player can match them at a glance.</summary>
        public readonly int ColourIndex;

        public readonly Vector3 DropOffPoint;
        public readonly string DropOffName;

        /// <summary>The food being delivered. Null means a plain delivery with no food.</summary>
        public readonly FoodType Food;

        /// <summary>The ingredients to collect, in the order they appear on the card.</summary>
        public IReadOnlyList<IngredientStop> Stops => _stops;

        /// <summary>False when the scene has no Processing Bay, in which case the order goes
        /// straight into the car once every ingredient is collected (the old pickup loop).</summary>
        public readonly bool NeedsCooking;
        public readonly float CookSeconds;
        public readonly float BurnSeconds;

        /// <summary>Straight line distance from the first ingredient to the drop off, fixed at
        /// spawn time. Only used by the optional pay per metre.</summary>
        public readonly float DesignedDistance;

        public OrderState State { get; private set; }

        /// <summary>Seconds left on the order's patience bar. Counts down until the order ends,
        /// including while the food cooks.</summary>
        public float TimeRemaining { get; private set; }

        /// <summary>The seconds the patience bar started with.</summary>
        public float TimeLimit { get; }

        /// <summary>0 to 1, how much of the patience bar is left.</summary>
        public float PatienceFraction => TimeLimit > 0.01f ? Mathf.Clamp01(TimeRemaining / TimeLimit) : 0f;

        /// <summary>Seconds left cooking (while Cooking) or before it burns (while Ready).</summary>
        public float BayTimeRemaining { get; private set; }

        /// <summary>0 to 1. While Cooking it fills up; while Ready it drains toward burning.</summary>
        public float BayFraction => State == OrderState.Cooking
            ? (CookSeconds > 0.01f ? 1f - Mathf.Clamp01(BayTimeRemaining / CookSeconds) : 1f)
            : State == OrderState.Ready
                ? (BurnSeconds > 0.01f ? Mathf.Clamp01(BayTimeRemaining / BurnSeconds) : 0f)
                : 0f;

        /// <summary>Where this order should be cooked. Suggested at spawn, then the bay it was
        /// actually put in.</summary>
        public Vector3 BayPoint { get; private set; }
        public string BayName { get; private set; }
        public bool HasBay { get; private set; }

        /// <summary>Seconds until this order is lost: its patience, or the burn timer if its food
        /// is sitting ready in a bay and that runs out first. The lowest one is the most urgent.</summary>
        public float SecondsUntilLost => State == OrderState.Ready
            ? Mathf.Min(TimeRemaining, BayTimeRemaining)
            : TimeRemaining;

        /// <summary>Set by OrderManager when the order resolves.</summary>
        public OrderPayout Payout { get; private set; }

        public string FoodName => Food != null ? Food.displayName : "Delivery";

        public bool IsTerminal =>
            State == OrderState.Delivered || State == OrderState.Late || State == OrderState.Burned;

        public int IngredientsCollected
        {
            get
            {
                int n = 0;
                foreach (IngredientStop s in _stops) if (s.Collected) n++;
                return n;
            }
        }

        public bool AllIngredientsCollected => IngredientsCollected == _stops.Count;

        /// <summary>Ingredients that are physically in the car right now: collected, but the
        /// order has not gone into a bay yet.</summary>
        public int IngredientsInCar => State == OrderState.Active ? IngredientsCollected : 0;

        public OrderStep CurrentStep
        {
            get
            {
                switch (State)
                {
                    case OrderState.Available:
                    case OrderState.Active:
                        return AllIngredientsCollected && NeedsCooking ? OrderStep.Cook : OrderStep.Gather;
                    case OrderState.Cooking:
                    case OrderState.Ready:
                        return OrderStep.Cook;
                    default:
                        return OrderStep.Deliver;
                }
            }
        }

        /// <summary>False while the food is cooking: there is nothing to drive to until it is
        /// ready, so the map markers hide it.</summary>
        public bool HasWorldTarget => !IsTerminal && State != OrderState.Cooking;

        /// <summary>The first ingredient still to collect, or null.</summary>
        public IngredientStop NextStop
        {
            get
            {
                foreach (IngredientStop s in _stops) if (!s.Collected) return s;
                return null;
            }
        }

        /// <summary>Where the car should go for this order. While gathering, the nearest
        /// ingredient still needed from the given position.</summary>
        public Vector3 TargetFrom(Vector3 from)
        {
            switch (CurrentStep)
            {
                case OrderStep.Gather:
                    {
                        IngredientStop best = null;
                        float bestDistance = float.MaxValue;
                        foreach (IngredientStop s in _stops)
                        {
                            if (s.Collected) continue;
                            float d = (s.Point - from).sqrMagnitude;
                            if (d < bestDistance) { bestDistance = d; best = s; }
                        }
                        return best != null ? best.Point : DropOffPoint;
                    }
                case OrderStep.Cook:
                    return BayPoint;
                default:
                    return DropOffPoint;
            }
        }

        /// <summary>Kept for older callers. The first ingredient while gathering.</summary>
        public Vector3 CurrentTarget => TargetFrom(_stops.Count > 0 ? _stops[0].Point : DropOffPoint);

        public event Action<Order, OrderState> StateChanged;

        private readonly List<IngredientStop> _stops;

        public Order(int id, IList<IngredientStop> stops, Vector3 dropOffPoint, string dropOffName,
            float timeLimit, FoodType food, bool needsCooking, float cookSeconds, float burnSeconds,
            int colourIndex = 0)
        {
            Id = id;
            ColourIndex = colourIndex;
            _stops = new List<IngredientStop>(stops);
            DropOffPoint = dropOffPoint;
            DropOffName = string.IsNullOrEmpty(dropOffName) ? "Drop off" : dropOffName;
            Food = food;
            NeedsCooking = needsCooking;
            CookSeconds = Mathf.Max(0f, cookSeconds);
            BurnSeconds = Mathf.Max(1f, burnSeconds);
            DesignedDistance = _stops.Count > 0 ? Vector3.Distance(_stops[0].Point, dropOffPoint) : 0f;
            TimeLimit = timeLimit;
            TimeRemaining = timeLimit;
            State = OrderState.Available;
        }

        public void Activate() => SetState(OrderState.Active);

        /// <summary>Records the bay this order is expected to cook in, for the map markers.</summary>
        public void SuggestBay(Vector3 point, string name)
        {
            if (State == OrderState.Cooking || State == OrderState.Ready) return;
            BayPoint = point;
            BayName = name;
            HasBay = true;
        }

        /// <summary>Marks one ingredient as collected. Returns false if it already was, or the
        /// order is no longer gathering.</summary>
        public bool CollectIngredient(IngredientStop stop)
        {
            if (State != OrderState.Active || stop == null || stop.Collected || !_stops.Contains(stop))
                return false;

            stop.Collected = true;
            StateChanged?.Invoke(this, State); // same state, but the card needs to refresh
            return true;
        }

        /// <summary>All ingredients go into a bay and the cooking clock starts.</summary>
        public bool StartCooking(Vector3 bayPoint, string bayName)
        {
            if (State != OrderState.Active || !AllIngredientsCollected) return false;

            BayPoint = bayPoint;
            BayName = bayName;
            HasBay = true;
            BayTimeRemaining = CookSeconds;
            SetState(OrderState.Cooking);
            if (CookSeconds <= 0f) FinishCooking();
            return true;
        }

        /// <summary>Called by the bay every frame. Returns true the moment the food burns.</summary>
        public bool TickBay(float deltaTime)
        {
            if (State == OrderState.Cooking)
            {
                BayTimeRemaining -= deltaTime;
                if (BayTimeRemaining <= 0f) FinishCooking();
                return false;
            }

            if (State == OrderState.Ready)
            {
                BayTimeRemaining -= deltaTime;
                if (BayTimeRemaining > 0f) return false;
                BayTimeRemaining = 0f;
                return true;
            }

            return false;
        }

        private void FinishCooking()
        {
            BayTimeRemaining = BurnSeconds;
            SetState(OrderState.Ready);
        }

        /// <summary>The cooked food goes into the car. Also used without a bay, straight from
        /// gathering, when the scene has no Processing Bay.</summary>
        public bool MarkCarried()
        {
            bool fromBay = State == OrderState.Ready;
            bool noBay = State == OrderState.Active && AllIngredientsCollected && !NeedsCooking;
            if (!fromBay && !noBay) return false;

            SetState(OrderState.Carried);
            return true;
        }

        public void MarkDelivered() => SetState(OrderState.Delivered);
        public void MarkLate() => SetState(OrderState.Late);
        public void MarkBurned() => SetState(OrderState.Burned);

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