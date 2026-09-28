using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Every tunable number for the order queue lives in this one asset, for the same reasons
    /// VehicleTuning exists for the car: several complete setups can be saved side by side and
    /// swapped without touching the scene, and edits made while the game is running are kept
    /// after play mode stops, which is what makes tuning against a live playtest actually useful.
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

        [Header("Reward")]
        public float baseDeliveryValue = 10f;

        [Range(0f, 1f)]
        public float latePenaltyPerSecond = 1f;

        [Header("Multi-leg orders (Milestone 2 exploration)")]
        [Tooltip("Off by default, so the queue spawns exactly the Milestone 1 direct pickup-to-" +
                 "drop-off order until this is deliberately turned on. This is the single switch " +
                 "for demoing the M1 loop versus the proposed M2 loop from the same build.")]
        public bool enableMultiLegOrders = false;

        [Range(0f, 1f)]
        [Tooltip("Chance an individual spawned order gets transfer points at all, once multi-leg " +
                 "orders are enabled. Keeping some orders direct even with the feature on means " +
                 "not every delivery demands the same amount of driving, which matters for the " +
                 "\"which order first\" prioritisation the hypothesis relies on.")]
        public float multiLegChance = 0.5f;

        [Tooltip("Minimum number of transfer points a multi-leg order can get.")]
        public int minTransferLegs = 1;

        [Tooltip("Maximum number of transfer points a multi-leg order can get.")]
        public int maxTransferLegs = 2;

        [Tooltip("Extra seconds added to orderTimeLimit per transfer point, so a multi-leg order " +
                 "is not simply a direct order made harder by the same clock. Without this, " +
                 "turning multi-leg orders on would silently make every order tighter, which is a " +
                 "balance change, not the structural one this feature is meant to test.")]
        public float extraTimePerTransferLeg = 15f;

        private void OnValidate()
        {
            spawnInterval = Mathf.Max(0.5f, spawnInterval);
            maxActiveOrders = Mathf.Max(1, maxActiveOrders);
            orderTimeLimit = Mathf.Max(1f, orderTimeLimit);
            baseDeliveryValue = Mathf.Max(0f, baseDeliveryValue);

            minTransferLegs = Mathf.Max(0, minTransferLegs);
            maxTransferLegs = Mathf.Max(minTransferLegs, maxTransferLegs);
            extraTimePerTransferLeg = Mathf.Max(0f, extraTimePerTransferLeg);

            if (string.IsNullOrWhiteSpace(profileName))
                profileName = name;
        }
    }
}