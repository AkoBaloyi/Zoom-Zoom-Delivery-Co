using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// One item on the menu, such as a Burger or Soup. Make one asset per food with
    /// Create > Zoom Zoom > Food Type, then list them in the Menu of the shift settings
    /// (Order Tuning) asset. Every number here is tuned in the Inspector, not in code.
    ///
    /// RECIPE
    /// Ingredients are what the player has to collect from around the map. Once all of them are
    /// in the car, the player drops them at a Processing Bay, where the food cooks for exactly
    /// Cook Seconds. It then sits ready for Burn Seconds, and burns if nobody collects it.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Food_",
        menuName = "Zoom Zoom/Food Type",
        order = 3)]
    public class FoodType : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Shown on the order card and in popups, for example \"Burger\".")]
        public string displayName = "Burger";

        [Tooltip("Optional. Shown on the order card next to the name.")]
        public Sprite icon;

        [Tooltip("Colour of the order card's header for this food.")]
        public Color cardColour = new Color(0.93f, 0.36f, 0.27f, 1f);

        [Header("Recipe")]
        [Tooltip("What the player has to collect for this food. Each one needs an Ingredient " +
                 "Source in the scene that supplies it. One to three works best on the order card.")]
        public IngredientType[] ingredients;

        [Tooltip("Exact seconds this food cooks in a Processing Bay.")]
        [Min(0f)] public float cookSeconds = 8f;

        [Tooltip("Seconds the cooked food waits in the bay before it burns and the order fails.")]
        [Min(1f)] public float burnSeconds = 12f;

        [Header("Pay")]
        [Tooltip("Rand paid for delivering this food on time, before the early bonus or late " +
                 "deduction.")]
        public float basePay = 250f;

        [Header("Spawning")]
        [Tooltip("How often this food is ordered compared with the others. 2 is twice as often as 1.")]
        [Min(0f)] public float spawnWeight = 1f;

        private void OnValidate()
        {
            basePay = Mathf.Max(0f, basePay);
            cookSeconds = Mathf.Max(0f, cookSeconds);
            burnSeconds = Mathf.Max(1f, burnSeconds);
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
    }
}