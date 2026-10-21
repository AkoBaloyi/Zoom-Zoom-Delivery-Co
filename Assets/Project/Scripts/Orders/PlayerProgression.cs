using System;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// The player's money. Adds what each order pays and subtracts the penalty for a cancelled
    /// order, read from OrderManager.OrderResolved so it always matches what happened on screen.
    /// The HUD shows this balance, and the upgrade screen (Task 26) can spend it with
    /// TrySpendMoney. There is no XP or level system.
    ///
    /// The class keeps its old name so the component already in your scenes keeps working.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerProgression : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Found automatically if left empty.")]
        [SerializeField] private OrderManager orderManager;

        [Header("Debug")]
        [SerializeField] private bool logActivity = true;

        /// <summary>Current balance in Rand. Can go below zero after penalties.</summary>
        public float Money { get; private set; }

        /// <summary>Total earned from deliveries, never reduced by penalties or spending.</summary>
        public float LifetimeMoney { get; private set; }

        /// <summary>Fires every time money is earned from an order, with the amount and the order.</summary>
        public event Action<float, Order> MoneyGained;

        /// <summary>Fires whenever Money changes for any reason, with the new balance.</summary>
        public event Action<float> MoneyChanged;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();

            if (orderManager == null)
            {
                Debug.LogError("[PlayerProgression] No OrderManager found, so no money will ever be " +
                               "earned. Switching off.", this);
                enabled = false;
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
            // Positive for a delivery, negative for a cancelled order (the penalty).
            if (Mathf.Approximately(value, 0f)) return;

            Money += value;
            if (value > 0f)
            {
                LifetimeMoney += value;
                MoneyGained?.Invoke(value, order);
            }
            MoneyChanged?.Invoke(Money);

            if (logActivity)
                Debug.Log($"[PlayerProgression] {value:+0;-0} Rand (order {order.Id}). Balance R{Money:0}.");
        }

        /// <summary>Called by the upgrade screen (Task 26) to pay for an upgrade. Returns false
        /// and spends nothing if the player cannot afford it.</summary>
        public bool TrySpendMoney(float amount)
        {
            if (amount < 0f || Money < amount) return false;

            Money -= amount;
            MoneyChanged?.Invoke(Money);

            if (logActivity)
                Debug.Log($"[PlayerProgression] Spent R{amount:0}. R{Money:0} left.", this);

            return true;
        }
    }
}