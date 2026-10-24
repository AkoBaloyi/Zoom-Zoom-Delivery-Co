using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// One item on the menu, such as Fried Rice or Soup. Make one asset per food with
    /// Create > Zoom Zoom > Food Type, then list them in the Menu of the shift settings
    /// (Order Tuning) asset. Every number here is tuned in the Inspector, not in code.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Food_",
        menuName = "Zoom Zoom/Food Type",
        order = 3)]
    public class FoodType : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Shown on the order card and in popups, for example \"Fried Rice\".")]
        public string displayName = "Fried Rice";

        [Tooltip("Optional. Shown on the order card next to the name.")]
        public Sprite icon;

        [Tooltip("Colour of the order card's header for this food.")]
        public Color cardColour = new Color(0.93f, 0.36f, 0.27f, 1f);

        [Header("Pay")]
        [Tooltip("Rand paid for delivering this food on time and undamaged, before the early " +
                 "bonus or late deduction.")]
        public float basePay = 250f;

        [Header("Spawning")]
        [Tooltip("How often this food is ordered compared with the others. 2 is twice as often as 1.")]
        [Min(0f)] public float spawnWeight = 1f;

        private void OnValidate()
        {
            basePay = Mathf.Max(0f, basePay);
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
    }
}
