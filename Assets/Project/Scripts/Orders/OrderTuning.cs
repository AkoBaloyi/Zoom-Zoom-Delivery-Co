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

        [TextArea(4, 12)]
        public string whyThisSetup = "";

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

        private void OnValidate()
        {
            spawnInterval = Mathf.Max(0.5f, spawnInterval);
            maxActiveOrders = Mathf.Max(1, maxActiveOrders);
            orderTimeLimit = Mathf.Max(1f, orderTimeLimit);
            baseDeliveryValue = Mathf.Max(0f, baseDeliveryValue);

            if (string.IsNullOrWhiteSpace(profileName))
                profileName = name;
        }
    }
}