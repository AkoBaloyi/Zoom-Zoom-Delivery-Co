using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Every tunable number for the order queue lives here so the group can change it in the
    /// Inspector after a playtest without touching code: the menu, order arrival, patience, the
    /// early bonus and late deduction, and the penalties for a cancelled or burned order. Each
    /// difficulty scene's OrderManager uses its own copy.
    /// </summary>
    [CreateAssetMenu(
        fileName = "OrderTuning",
        menuName = "Zoom Zoom/Order Tuning",
        order = 1)]
    public class OrderTuning : ScriptableObject
    {
        [Header("Profile identity")]
        public string profileName = "Unnamed";

        [Header("Menu")]
        [Tooltip("The foods that can be ordered. Make them with Create > Zoom Zoom > Food Type, " +
                 "or run Zoom Zoom > Create Default Menu. Leave empty and every order is a plain " +
                 "delivery worth Base Delivery Value.")]
        public FoodType[] menu;

        [Header("Order arrival")]
        [Tooltip("Seconds between one order arriving and the next. The first order arrives as " +
                 "soon as the shift starts.")]
        public float spawnInterval = 12f;

        [Tooltip("Most orders that can be active at once.")]
        public int maxActiveOrders = 3;

        [Header("Patience")]
        [Tooltip("Seconds each order waits before it is cancelled. This is the bar on each order " +
                 "card, separate from the shift clock. It keeps running while the food cooks.")]
        public float orderTimeLimit = 75f;

        [Header("Cooking (used only for orders with no food set)")]
        [Tooltip("Seconds a plain delivery cooks in a Processing Bay. Foods use their own Cook " +
                 "Seconds.")]
        public float defaultCookSeconds = 8f;
        [Tooltip("Seconds a plain delivery waits in the bay before it burns. Foods use their own " +
                 "Burn Seconds.")]
        public float defaultBurnSeconds = 12f;

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
        public float cancelPenalty = 50f;

        [Tooltip("Rand lost when food is left in a Processing Bay too long and burns. The order " +
                 "fails as well.")]
        public float burnPenalty = 50f;

        /// <summary>Base pay for an order before the time bonus: the food's base pay (or Base
        /// Delivery Value with no food) plus the optional distance extra.</summary>
        public float BasePayFor(FoodType food, float designedDistance)
        {
            float basePay = food != null ? food.basePay : baseDeliveryValue;
            return basePay + Mathf.Max(0f, designedDistance) * valuePerMetre;
        }

        /// <summary>Kept for older callers. Same as BasePayFor with no food.</summary>
        public float DeliveryValueFor(float designedDistance) => BasePayFor(null, designedDistance);

        public float CookSecondsFor(FoodType food) => food != null ? food.cookSeconds : defaultCookSeconds;
        public float BurnSecondsFor(FoodType food) => food != null ? food.burnSeconds : defaultBurnSeconds;

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
            spawnInterval = Mathf.Max(0.5f, spawnInterval);
            maxActiveOrders = Mathf.Max(1, maxActiveOrders);
            orderTimeLimit = Mathf.Max(1f, orderTimeLimit);
            defaultCookSeconds = Mathf.Max(0f, defaultCookSeconds);
            defaultBurnSeconds = Mathf.Max(1f, defaultBurnSeconds);
            baseDeliveryValue = Mathf.Max(0f, baseDeliveryValue);
            valuePerMetre = Mathf.Max(0f, valuePerMetre);
            cancelPenalty = Mathf.Max(0f, cancelPenalty);
            burnPenalty = Mathf.Max(0f, burnPenalty);
            lateBelowPatienceLeft = Mathf.Min(lateBelowPatienceLeft, earlyAbovePatienceLeft);

            if (string.IsNullOrWhiteSpace(profileName))
                profileName = name;
        }
    }
}