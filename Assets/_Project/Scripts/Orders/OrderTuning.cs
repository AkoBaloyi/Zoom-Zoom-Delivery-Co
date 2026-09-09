using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Every tunable number for the order queue lives in this one asset, for the same reasons
    /// VehicleTuning exists for the car: several complete setups can be saved side by side and
    /// swapped without touching the scene, and edits made while the game is running are kept
    /// after play mode stops, which is what makes tuning against a live playtest actually useful.
    ///
    /// Runtime order DATA (which orders exist right now, what state each is in) does not live
    /// here. This asset only holds the numbers that decide how orders are generated and scored,
    /// the same separation VehicleTuning draws between "the car's numbers" and "what the car is
    /// currently doing".
    /// </summary>
    [CreateAssetMenu(
        fileName = "OrderTuning",
        menuName = "Zoom Zoom/Order Tuning",
        order = 1)]
    public class OrderTuning : ScriptableObject
    {
        [Header("Profile identity")]
        [Tooltip("Short name for this setup, written into any exported test data.")]
        public string profileName = "Unnamed";

        [Tooltip("Plain language: what this setup is trying to do and why it was kept or rejected.")]
        [TextArea(4, 12)]
        public string whyThisSetup = "";

        [Header("Spawning")]
        [Tooltip("Seconds between one order spawning and the next becoming eligible to spawn. " +
                 "This is the main pressure dial: lower it and the player has less breathing room " +
                 "between decisions.")]
        public float spawnInterval = 12f;

        [Tooltip("Maximum number of orders that can be Active at once. Once this many are active, " +
                 "spawning pauses until one is resolved (Delivered or Late), so the queue cannot " +
                 "grow to a point where reading it is the actual challenge instead of driving.")]
        public int maxActiveOrders = 3;

        [Header("Timing per order")]
        [Tooltip("Seconds an order stays Active before it becomes Late, if not Delivered first.")]
        public float orderTimeLimit = 45f;

        [Header("Reward")]
        [Tooltip("Base value paid for a Delivered order, before any distance or lateness adjustment.")]
        public float baseDeliveryValue = 10f;

        [Tooltip("Fraction of an order's value lost per second it sits Late before being cleared. " +
                 "0 means a Late order is worth nothing at all, which is the current MVP behaviour.")]
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
