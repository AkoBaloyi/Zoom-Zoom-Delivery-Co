using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// The shift settings asset. Every tunable number for a shift lives here so the group can
    /// change it in the Inspector after a playtest without touching code: the menu, order
    /// arrival, patience, pay brackets, the penalty for a failed order, food condition, shift
    /// length and the star thresholds. Each difficulty scene's OrderManager uses its own copy.
    /// </summary>
    [CreateAssetMenu(
        fileName = "OrderTuning",
        menuName = "Zoom Zoom/Order Tuning",
        order = 1)]
    public class OrderTuning : ScriptableObject
    {
        [Header("Profile identity")]
        public string profileName = "Unnamed";

        [Header("Shift")]
        [Tooltip("Seconds in one shift. ShiftTimer uses this. Test 120 to 180.")]
        public float shiftLengthSeconds = 180f;

        [Header("Menu")]
        [Tooltip("The foods that can be ordered. Make them with Create > Zoom Zoom > Food Type. " +
                 "Leave empty and every order is a plain delivery worth Base Delivery Value.")]
        public FoodType[] menu;

        [Header("Order arrival")]
        [Tooltip("Seconds between one order arriving and the next. The first order arrives as " +
                 "soon as the shift starts.")]
        public float spawnInterval = 15f;

        [Tooltip("Most orders that can be active at once.")]
        public int maxActiveOrders = 3;

        [Header("Patience")]
        [Tooltip("Seconds each order waits before it is cancelled. This is the bar on each order " +
                 "card, separate from the shift clock.")]
        public float orderTimeLimit = 45f;

        [Header("Pay")]
        [Tooltip("Rand paid for an order when no food is set on it.")]
        public float baseDeliveryValue = 250f;

        [Tooltip("Optional extra Rand per metre of the order's planned distance. 0 turns it off.")]
        public float valuePerMetre = 0f;

        [Header("Time bonus (share of patience left at delivery)")]
        [Range(0f, 1f)]
        [Tooltip("Delivered with more than this share of the patience bar left counts as early.")]
        public float earlyAbovePatienceLeft = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Extra pay for an early delivery, as a share of base pay. 0.2 is 20% extra.")]
        public float earlyBonus = 0.2f;

        [Range(0f, 1f)]
        [Tooltip("Delivered with less than this share of the patience bar left counts as late.")]
        public float lateBelowPatienceLeft = 0.2f;

        [Range(0f, 1f)]
        [Tooltip("Pay taken off a late delivery, as a share of base pay. 0.1 is 10% less.")]
        public float lateDeduction = 0.1f;

        [Header("Failed orders")]
        [Tooltip("Rand lost when an order's patience bar runs out and it is cancelled.")]
        public float cancelPenalty = 100f;

        [Header("Food condition")]
        [Tooltip("Collisions slower than this (m/s, relative speed) do not count as a hit, so " +
                 "small bumps never damage food.")]
        public float minimumImpactSpeed = 6f;

        [Tooltip("Seconds after a hit before another can count, so one crash is one hit.")]
        public float hitCooldown = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Share of pay still earned when food has lost all its stability. Each lost " +
                 "stage takes an equal share of the rest.")]
        public float minimumConditionPay = 0.25f;

        [Header("Stars (profit at the end of the shift, in Rand)")]
        public float oneStarProfit = 2000f;
        public float twoStarProfit = 3500f;
        public float threeStarProfit = 7000f;

        public int StarsFor(float profit)
        {
            if (profit >= threeStarProfit) return 3;
            if (profit >= twoStarProfit) return 2;
            if (profit >= oneStarProfit) return 1;
            return 0;
        }

        /// <summary>Base pay for an order before time and condition: the food's base pay (or
        /// Base Delivery Value with no food) plus the optional distance extra.</summary>
        public float BasePayFor(FoodType food, float designedDistance)
        {
            float basePay = food != null ? food.basePay : baseDeliveryValue;
            return basePay + Mathf.Max(0f, designedDistance) * valuePerMetre;
        }

        /// <summary>Kept for older callers. Same as BasePayFor with no food.</summary>
        public float DeliveryValueFor(float designedDistance) => BasePayFor(null, designedDistance);

        /// <summary>Picks a food from the menu, weighted by each food's Spawn Weight.</summary>
        public FoodType PickFood()
        {
            if (menu == null || menu.Length == 0) return null;

            float total = 0f;
            foreach (FoodType f in menu)
                if (f != null) total += Mathf.Max(0f, f.spawnWeight);

            if (total <= 0f) return null;

            float roll = Random.value * total;
            foreach (FoodType f in menu)
            {
                if (f == null) continue;
                roll -= Mathf.Max(0f, f.spawnWeight);
                if (roll <= 0f) return f;
            }

            return menu[menu.Length - 1];
        }

        private void OnValidate()
        {
            shiftLengthSeconds = Mathf.Max(10f, shiftLengthSeconds);
            spawnInterval = Mathf.Max(0.5f, spawnInterval);
            maxActiveOrders = Mathf.Max(1, maxActiveOrders);
            orderTimeLimit = Mathf.Max(1f, orderTimeLimit);
            baseDeliveryValue = Mathf.Max(0f, baseDeliveryValue);
            valuePerMetre = Mathf.Max(0f, valuePerMetre);
            cancelPenalty = Mathf.Max(0f, cancelPenalty);
            minimumImpactSpeed = Mathf.Max(0f, minimumImpactSpeed);
            hitCooldown = Mathf.Max(0f, hitCooldown);
            lateBelowPatienceLeft = Mathf.Min(lateBelowPatienceLeft, earlyAbovePatienceLeft);
            twoStarProfit = Mathf.Max(oneStarProfit, twoStarProfit);
            threeStarProfit = Mathf.Max(twoStarProfit, threeStarProfit);

            if (string.IsNullOrWhiteSpace(profileName))
                profileName = name;
        }
    }
}