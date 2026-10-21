using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// One ingredient, such as Meat or Vegetables. Foods list the ingredients they need, and
    /// Ingredient Source objects in the scene (the butcher, the bakery, the market) list the
    /// ingredients they supply. Make one with Create > Zoom Zoom > Ingredient, or let
    /// Zoom Zoom > Create Default Menu make the full set.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Ingredient_",
        menuName = "Zoom Zoom/Ingredient",
        order = 4)]
    public class IngredientType : ScriptableObject
    {
        [Tooltip("Shown in popups and on the map, for example \"Meat\".")]
        public string displayName = "Meat";

        [Tooltip("Up to about 5 letters, shown on the order card. Leave empty to use the start " +
                 "of the name.")]
        public string shortLabel = "";

        [Tooltip("Optional. Shown on the order card instead of the short label.")]
        public Sprite icon;

        [Tooltip("Tint for this ingredient on the order card once it has been collected.")]
        public Color colour = new Color(0.20f, 0.60f, 1.00f, 1f);

        /// <summary>The text on this ingredient's chip on the order card.</summary>
        public string ChipLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(shortLabel)) return shortLabel.Trim().ToUpper();
                string source = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
                source = source.Trim().ToUpper();
                return source.Length <= 5 ? source : source.Substring(0, 4);
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
    }
}
