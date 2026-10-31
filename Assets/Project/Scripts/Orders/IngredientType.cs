using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// The five ingredient types. Each has its own icon and colour, used on the order chips, the
    /// shop signs and the map beacons, so the player can match what an order needs to where it
    /// is sold at a glance. They line up with the five shops: Butcher, Bakery, Market, Fish
    /// Market and Dairy.
    ///
    /// Auto works the type out from the ingredient's name (Meat and Chicken are Meat, Bun, Bread
    /// and Dough are Bakery, and so on), so existing ingredient assets need no changes.
    /// </summary>
    public enum IngredientCategory
    {
        Auto,
        Meat,
        Bakery,
        Produce,
        Seafood,
        Dairy
    }

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

        [Tooltip("Decides the icon and colour. Auto works it out from the name.")]
        public IngredientCategory category = IngredientCategory.Auto;

        [Tooltip("Up to about 5 letters. Only shown where an icon cannot be. Leave empty to use " +
                 "the start of the name.")]
        public string shortLabel = "";

        [Tooltip("The ingredient's icon image (Icon_<Name>.png in Assets/Project/Art/Ingredients). " +
                 "Zoom Zoom > Assign Ingredient Icons fills this in, or drag the image here.")]
        public Sprite icon;

        /// <summary>The ingredient's type, with Auto resolved from its name.</summary>
        public IngredientCategory Category =>
            category != IngredientCategory.Auto ? category : GuessCategory(displayName);

        /// <summary>The text on this ingredient's chip when no icon can be shown.</summary>
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

        /// <summary>Works out the type from a name like "Chicken" or "Cheese". Anything not
        /// recognised counts as Produce, the Market's type.</summary>
        public static IngredientCategory GuessCategory(string ingredientName)
        {
            string n = (ingredientName ?? "").ToLowerInvariant();

            if (Has(n, "meat", "chicken", "beef", "pork", "lamb", "boerewors", "sausage", "mince", "bacon"))
                return IngredientCategory.Meat;
            if (Has(n, "bun", "bread", "dough", "roll", "flour", "pastry", "tortilla"))
                return IngredientCategory.Bakery;
            if (Has(n, "fish", "prawn", "shrimp", "salmon", "tuna", "seafood", "calamari"))
                return IngredientCategory.Seafood;
            if (Has(n, "milk", "cheese", "egg", "butter", "cream", "yoghurt", "yogurt", "ice cream"))
                return IngredientCategory.Dairy;
            return IngredientCategory.Produce;
        }

        private static bool Has(string text, params string[] words)
        {
            foreach (string w in words)
                if (text.Contains(w)) return true;
            return false;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
    }
}
