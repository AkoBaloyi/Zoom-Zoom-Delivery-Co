using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// The car's hold. It carries two kinds of thing, each with its own limit:
    ///   Ingredients, up to Ingredient Capacity, collected from Ingredient Sources and dropped
    ///   off at a Processing Bay. They belong to the order that needed them.
    ///   Cooked meals, up to Capacity, taken out of a bay and delivered to the customer.
    ///
    /// Every pickup, bay visit and delivery goes through this class, which checks the limits and
    /// then tells the Order and the bay what changed. ZoneDetection decides when the car is close
    /// enough to try.
    /// </summary>
    [DisallowMultipleComponent]
    public class CargoSystem : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Found automatically if left empty.")]
        [SerializeField] private OrderManager orderManager;

        [Tooltip("Optional. Found automatically if present on this GameObject. When it is " +
                 "switched on, a delivery's pay is scaled by the cargo's condition.")]
        [SerializeField] private CargoCondition cargoCondition;

        [Header("Capacity")]
        [Tooltip("How many cooked meals the car can carry at once.")]
        [SerializeField] private int capacity = 1;

        [Tooltip("How many ingredients the car can carry at once, across every order. Enough " +
                 "for one or two recipes is what makes the player choose a route.")]
        [SerializeField] private int ingredientCapacity = 4;

        [Header("Debug")]
        [SerializeField] private bool logActivity = true;

        private readonly List<Order> _carriedOrders = new List<Order>();

        /// <summary>Cooked meals in the car.</summary>
        public IReadOnlyList<Order> CarriedOrders => _carriedOrders;
        public bool HasCapacity => _carriedOrders.Count < capacity;
        public int Capacity => capacity;
        public int SlotsUsed => _carriedOrders.Count;

        public int IngredientCapacity => ingredientCapacity;

        /// <summary>Ingredients in the car right now, across every order.</summary>
        public int IngredientsHeld
        {
            get
            {
                if (orderManager == null) return 0;
                int n = 0;
                foreach (Order o in orderManager.ActiveOrders) n += o.IngredientsInCar;
                return n;
            }
        }

        public bool HasIngredientRoom => IngredientsHeld < ingredientCapacity;

        /// <summary>Fires when a cooked meal goes into the car. CargoCondition starts tracking
        /// its condition from 100 here.</summary>
        public event Action<Order> OrderCarried;

        /// <summary>Fires when a cooked meal leaves the car for any reason.</summary>
        public event Action<Order> OrderReleased;

        /// <summary>Fires when an ingredient is collected, with the order and the ingredient.</summary>
        public event Action<Order, IngredientStop> IngredientCollected;

        /// <summary>Fires when an order's ingredients go into a bay.</summary>
        public event Action<Order, ProcessingBay> IngredientsLoaded;

        /// <summary>Fires when something could not be done, with a short reason for the
        /// player, for example "Car full". UI should throttle repeats.</summary>
        public event Action<string> Refused;

        /// <summary>Fires whenever meal capacity changes.</summary>
        public event Action<int> CapacityChanged;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (cargoCondition == null) cargoCondition = GetComponent<CargoCondition>();

            if (orderManager == null)
            {
                Debug.LogError(
                    "[CargoSystem] No OrderManager found. A cancelled order that was still " +
                    "carried will NOT free its slot.", this);
                return;
            }

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

        // ---- ingredients ----------------------------------------------------------------------

        /// <summary>Collects one ingredient for one order, if the car has room.</summary>
        public bool TryCollectIngredient(Order order, IngredientStop stop)
        {
            if (order == null || stop == null || stop.Collected) return false;
            if (order.State != OrderState.Active) return false;

            if (!HasIngredientRoom)
            {
                Refuse($"Car full of ingredients ({ingredientCapacity}). Drop some at a bay.", true);
                return false;
            }

            // Without a bay, the last ingredient turns straight into a meal, which needs a slot.
            bool completesWithoutBay = !order.NeedsCooking && order.IngredientsCollected == order.Stops.Count - 1;
            if (completesWithoutBay && !HasCapacity)
            {
                Refuse($"No room for order #{order.Id}. Deliver what you are carrying first.", true);
                return false;
            }

            if (!order.CollectIngredient(stop)) return false;

            if (logActivity)
                Debug.Log($"[CargoSystem] Collected {stop.Name} for order {order.Id} " +
                          $"({order.IngredientsCollected}/{order.Stops.Count}). " +
                          $"{IngredientsHeld}/{ingredientCapacity} ingredients in the car.");

            IngredientCollected?.Invoke(order, stop);

            if (completesWithoutBay && order.MarkCarried()) AddMeal(order);
            return true;
        }

        // ---- bays -----------------------------------------------------------------------------

        /// <summary>Takes ready food out of the bay into the car, if there is a free meal slot.</summary>
        public bool TryCollectFromBay(ProcessingBay bay)
        {
            if (bay == null || !bay.IsReady) return false;

            if (!HasCapacity)
            {
                Refuse($"{bay.BayName}: no room for the food. Deliver what you are carrying first.", true);
                return false;
            }

            Order order = bay.TakeOut();
            if (order == null) return false;

            AddMeal(order);
            return true;
        }

        /// <summary>Puts the oldest order with every ingredient in the car into an empty bay.</summary>
        public bool TryLoadBay(ProcessingBay bay)
        {
            if (bay == null || orderManager == null) return false;

            Order ready = null;
            foreach (Order o in orderManager.ActiveOrders)
            {
                if (o.State == OrderState.Active && o.NeedsCooking && o.AllIngredientsCollected)
                {
                    ready = o;
                    break; // ActiveOrders is in spawn order, so this is the oldest.
                }
            }

            if (ready == null) return false;

            if (!bay.IsEmpty)
            {
                Refuse($"{bay.BayName} is busy with order #{bay.Order.Id}.", true);
                return false;
            }

            if (!bay.TryStartCooking(ready)) return false;

            if (logActivity)
                Debug.Log($"[CargoSystem] Order {ready.Id} ingredients loaded into {bay.BayName}.");

            IngredientsLoaded?.Invoke(ready, bay);
            return true;
        }

        // ---- delivery -------------------------------------------------------------------------

        public bool TryDeliver(OrderManager manager, Order expectedOrder)
        {
            if (manager == null || expectedOrder == null) return false;
            if (!_carriedOrders.Contains(expectedOrder)) return false;
            if (expectedOrder.State != OrderState.Carried) return false;

            float conditionMultiplier = cargoCondition != null
                ? cargoCondition.GetConditionMultiplier(expectedOrder)
                : 1f;

            _carriedOrders.Remove(expectedOrder);
            OrderReleased?.Invoke(expectedOrder);
            manager.ResolveDelivery(expectedOrder, conditionMultiplier);

            if (logActivity)
                Debug.Log($"[CargoSystem] Order {expectedOrder.Id} delivered. " +
                          $"{_carriedOrders.Count}/{capacity} meal slots used.");

            return true;
        }

        public void ClearIfResolved(Order order)
        {
            if (_carriedOrders.Remove(order)) OrderReleased?.Invoke(order);
        }

        /// <summary>Raises meal capacity, for a later upgrade. Never lowers it.</summary>
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

        // ---- helpers --------------------------------------------------------------------------

        private void AddMeal(Order order)
        {
            _carriedOrders.Add(order);
            OrderCarried?.Invoke(order);

            if (logActivity)
                Debug.Log($"[CargoSystem] Order {order.Id} ({order.FoodName}) in the car. " +
                          $"{_carriedOrders.Count}/{capacity} meal slots used.");
        }

        private void Refuse(string reason, bool tellPlayer)
        {
            if (tellPlayer) Refused?.Invoke(reason);
        }

        private void OnValidate()
        {
            capacity = Mathf.Max(1, capacity);
            ingredientCapacity = Mathf.Max(1, ingredientCapacity);
        }
    }
}