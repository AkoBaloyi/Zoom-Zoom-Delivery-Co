using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Every tunable number for player progression (XP and levels) lives in this one asset,
    /// matching OrderTuning and VehicleTuning: several setups saved side by side, edited live
    /// during a playtest without touching the scene. Money per delivery is set in OrderTuning
    /// (baseDeliveryValue and valuePerMetre), since it is the delivery value itself.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProgressionTuning",
        menuName = "Zoom Zoom/Progression Tuning",
        order = 2)]
    public class ProgressionTuning : ScriptableObject
    {
        [Header("XP per resolved order")]
        [Tooltip("Flat XP for every successful delivery, on top of the value based amount below. " +
                 "Keeps a short delivery worth something.")]
        public float baseXpPerDelivery = 5f;

        [Tooltip("Extra XP per point of delivery value. Value already reflects designed distance " +
                 "and cargo condition, so XP inherits both for free.")]
        public float xpPerValuePoint = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Fraction of baseXpPerDelivery paid as XP even when an order goes Late. 0 means " +
                 "a late order earns nothing, matching how it already pays no money.")]
        public float lateConsolationXpFraction = 0f;

        [Header("Level curve (rank)")]
        [Tooltip("XP required to advance out of each level: index 0 is level 1 to 2, index 1 is " +
                 "level 2 to 3, and so on. Past the end of the array the last value repeats, so " +
                 "the curve never runs out, it just stops getting harder.")]
        public float[] xpToNextLevel = { 20f, 35f, 55f, 80f, 110f };

        /// <summary>XP required to advance out of the given level (1 based). Clamps to the last
        /// curve entry once level exceeds the array, rather than throwing.</summary>
        public float XpRequiredForLevel(int level)
        {
            if (xpToNextLevel == null || xpToNextLevel.Length == 0) return 999999f;
            int index = Mathf.Clamp(level - 1, 0, xpToNextLevel.Length - 1);
            return Mathf.Max(1f, xpToNextLevel[index]);
        }

        private void OnValidate()
        {
            baseXpPerDelivery = Mathf.Max(0f, baseXpPerDelivery);
            xpPerValuePoint = Mathf.Max(0f, xpPerValuePoint);
        }
    }
}
