using System;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Task 20, XP and money reward calculation. Turns resolved orders into XP (and the levels
    /// that XP builds toward) and into money the player can later spend.
    ///
    /// WHAT THIS DELIBERATELY DOES NOT DO
    /// It does not unlock cargo slots. In the project plan the two slot and three slot cargo
    /// upgrades (Tasks 19 and 25) are bought with money on the upgrade screen (Task 26, Zubuhle),
    /// at prices set later under "Post MVP XP thresholds, money payouts, prices and upgrade
    /// balance". So this class earns and holds the money, TrySpendMoney is what the upgrade
    /// screen calls to pay for an upgrade, and CargoSystem.TryUnlockCapacity is what it calls to
    /// apply it. Level is kept as the rank that Task 25's rank persistence will save.
    ///
    /// WHY A SEPARATE COMPONENT
    /// OrderManager runs the order queue and CargoSystem runs the slot. XP and money are a third
    /// concern that reads OrderManager.OrderResolved, the same event OrderHUD and ShiftResults
    /// read, so progression can never disagree with what the player saw happen on screen.
    /// Remove this component and the order loop still works exactly as before.
    ///
    /// WHERE THE NUMBERS COME FROM
    /// Money for a delivery is the value OrderManager already calculated: a flat base plus an
    /// amount per metre of the order's designed distance (the plan's distance rule), scaled by
    /// cargo condition if that is switched on. XP is calculated from that same value, so a
    /// longer or cleaner delivery is worth more of both.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerProgression : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private ProgressionTuning tuning;

        [Header("Wiring")]
        [Tooltip("Found automatically if left empty.")]
        [SerializeField] private OrderManager orderManager;

        [Header("Debug")]
        [SerializeField] private bool logActivity = true;

        /// <summary>1 based rank. Starts at 1, the same convention Order.Id uses.</summary>
        public int Level { get; private set; } = 1;

        /// <summary>XP earned since the current level started, i.e. progress toward the next
        /// level, not a lifetime total.</summary>
        public float XpIntoLevel { get; private set; }

        /// <summary>How much XP Level needs before the next level up.</summary>
        public float XpToNextLevel => tuning != null ? tuning.XpRequiredForLevel(Level) : 1f;

        /// <summary>0 to 1, for progress bars.</summary>
        public float LevelProgress01 => Mathf.Clamp01(XpIntoLevel / Mathf.Max(0.01f, XpToNextLevel));

        public float LifetimeXp { get; private set; }

        /// <summary>Money currently held, available to spend on the upgrade screen.</summary>
        public float Money { get; private set; }

        /// <summary>Total money earned, never reduced by spending. For results screens.</summary>
        public float LifetimeMoney { get; private set; }

        /// <summary>Fires every time XP is gained, carrying the amount and the order it came
        /// from. ShiftResults uses this to tally XP earned during a shift.</summary>
        public event Action<float, Order> XpGained;

        /// <summary>Fires every time money is earned from an order, carrying the amount and the
        /// order. Not fired for spending.</summary>
        public event Action<float, Order> MoneyGained;

        /// <summary>Fires whenever Money changes for any reason (earned or spent), carrying the
        /// new balance. The upgrade screen can use this to refresh what is affordable.</summary>
        public event Action<float> MoneyChanged;

        /// <summary>Fires after Level has already been updated, carrying the new level.</summary>
        public event Action<int> LevelUp;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();

            if (tuning == null)
            {
                Debug.LogError(
                    "[PlayerProgression] No ProgressionTuning assigned. Drag a tuning asset onto " +
                    "the Tuning field, XP cannot be calculated without it.", this);
                enabled = false;
                return;
            }

            if (orderManager == null)
            {
                Debug.LogError(
                    "[PlayerProgression] No OrderManager found. Nothing will ever award XP or " +
                    "money, since OrderResolved is the only source of either.", this);
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
            if (order.State == OrderState.Delivered)
            {
                AddMoney(value, order);
                AddXp(tuning.baseXpPerDelivery + value * tuning.xpPerValuePoint, order);
            }
            else
            {
                // Late. Value arrives as 0 from OrderManager for a Late order, so there is no
                // money, and the consolation XP is estimated off baseXpPerDelivery alone.
                float xp = tuning.baseXpPerDelivery * tuning.lateConsolationXpFraction;
                if (xp > 0f) AddXp(xp, order);
            }
        }

        /// <summary>Called by the upgrade screen (Task 26) to pay for an upgrade. Returns false
        /// and spends nothing if the player cannot afford it.</summary>
        public bool TrySpendMoney(float amount)
        {
            if (amount < 0f || Money < amount) return false;

            Money -= amount;
            MoneyChanged?.Invoke(Money);

            if (logActivity)
                Debug.Log($"[PlayerProgression] Spent {amount:0}. {Money:0} left.", this);

            return true;
        }

        private void AddMoney(float amount, Order source)
        {
            if (amount <= 0f) return;

            Money += amount;
            LifetimeMoney += amount;
            MoneyGained?.Invoke(amount, source);
            MoneyChanged?.Invoke(Money);

            if (logActivity)
                Debug.Log($"[PlayerProgression] +{amount:0} money (order {source.Id}). Balance {Money:0}.");
        }

        private void AddXp(float amount, Order source)
        {
            if (amount <= 0f) return;

            LifetimeXp += amount;
            XpIntoLevel += amount;
            XpGained?.Invoke(amount, source);

            if (logActivity)
                Debug.Log($"[PlayerProgression] +{amount:0.#} XP (order {source.Id}). " +
                          $"{XpIntoLevel:0.#}/{XpToNextLevel:0.#} into level {Level}.");

            // A loop, not an if: one large delivery could clear more than one level's worth of
            // XP at once, and each level should fire its own LevelUp rather than be skipped.
            while (XpIntoLevel >= XpToNextLevel)
            {
                XpIntoLevel -= XpToNextLevel;
                Level++;
                LevelUp?.Invoke(Level);

                if (logActivity)
                    Debug.Log($"[PlayerProgression] Level up! Now level {Level}.", this);
            }
        }
    }
}
