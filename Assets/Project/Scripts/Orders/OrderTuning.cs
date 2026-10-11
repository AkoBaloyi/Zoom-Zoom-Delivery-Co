using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Every tunable number for the order queue lives in this one asset, for the same reasons
    /// VehicleTuning exists for the car: several complete setups can be saved side by side and
    /// swapped without touching the scene, and edits made while the game is running are kept
    /// after play mode stops, which is what makes tuning against a live playtest useful.
    /// Task 18 (spawn interval, active order limit, late penalty) is tuned here, and Ako's
    /// comparison build (Task 21) swaps between saved copies of this asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "OrderTuning",
        menuName = "Zoom Zoom/Order Tuning",
        order = 1)]
    public class OrderTuning : ScriptableObject
    {
        [Header("Profile identity")]
        public string profileName = "Unnamed";

        [Header("Spawning")]
        [Tooltip("Seconds between one order spawning and the next becoming eligible to spawn.")]
        public float spawnInterval = 12f;

        [Tooltip("Maximum number of orders that can be Active at once.")]
        public int maxActiveOrders = 3;

        [Header("Timing per order")]
        [Tooltip("Seconds an order stays Active before it becomes Late, if not Delivered first.")]
        public float orderTimeLimit = 45f;

        [Header("Reward (Task 20)")]
        [Tooltip("Flat money paid for any successful delivery, before distance is added.")]
        public float baseDeliveryValue = 10f;

        [Tooltip("Extra money per metre of the order's DESIGNED pickup to drop off distance, never " +
                 "the distance actually driven, so driving in circles earns nothing. This is the " +
                 "distance based reward rule from the project plan's Scope section. 0 makes every " +
                 "delivery worth the same flat amount.")]
        public float valuePerMetre = 0.1f;

        [Range(0f, 1f)]
        public float latePenaltyPerSecond = 1f;

        /// <summary>Money for delivering an order of the given designed distance, before any
        /// cargo condition scaling.</summary>
        public float DeliveryValueFor(float designedDistance)
        {
            return baseDeliveryValue + Mathf.Max(0f, designedDistance) * valuePerMetre;
        }

        private void OnValidate()
        {
            spawnInterval = Mathf.Max(0.5f, spawnInterval);
            maxActiveOrders = Mathf.Max(1, maxActiveOrders);
            orderTimeLimit = Mathf.Max(1f, orderTimeLimit);
            baseDeliveryValue = Mathf.Max(0f, baseDeliveryValue);
            valuePerMetre = Mathf.Max(0f, valuePerMetre);

            if (string.IsNullOrWhiteSpace(profileName))
                profileName = name;
        }
    }
}