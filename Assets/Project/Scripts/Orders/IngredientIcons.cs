using UnityEngine;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// The colour for each ingredient type, and each ingredient's icon image. Used by the order
    /// chips, the shop signs, the beacons and the arrows, so an ingredient looks the same
    /// everywhere.
    ///
    /// The colour tells you which shop sells it:
    ///   Meat     red brown   Butcher
    ///   Bakery   gold        Bakery
    ///   Produce  green       Market
    ///   Seafood  blue        Fish Market
    ///   Dairy    lilac       Dairy
    ///
    /// The icon tells you exactly what it is. Icons are the Icon_ images in
    /// Assets/Project/Art/Ingredients, put into each ingredient's Icon field by
    /// Zoom Zoom > Assign Ingredient Icons.
    /// </summary>
    public static class IngredientIcons
    {
        private static Sprite _disc;

        public static Color Colour(IngredientCategory category)
        {
            switch (category)
            {
                case IngredientCategory.Meat: return new Color(0.72f, 0.30f, 0.26f, 1f);
                case IngredientCategory.Bakery: return new Color(0.82f, 0.60f, 0.28f, 1f);
                case IngredientCategory.Seafood: return new Color(0.22f, 0.55f, 0.85f, 1f);
                case IngredientCategory.Dairy: return new Color(0.60f, 0.50f, 0.85f, 1f);
                default: return new Color(0.30f, 0.68f, 0.28f, 1f); // Produce
            }
        }

        public static Color Colour(IngredientType ingredient) =>
            Colour(ingredient != null ? ingredient.Category : IngredientCategory.Produce);

        /// <summary>The kind of shop that sells this type, for "Get Meat at any Butcher".</summary>
        public static string ShopName(IngredientCategory category)
        {
            switch (category)
            {
                case IngredientCategory.Meat: return "Butcher";
                case IngredientCategory.Bakery: return "Bakery";
                case IngredientCategory.Seafood: return "Fish Market";
                case IngredientCategory.Dairy: return "Dairy";
                default: return "Market";
            }
        }

        public static string ShopName(IngredientType ingredient) =>
            ShopName(ingredient != null ? ingredient.Category : IngredientCategory.Produce);

        /// <summary>The ingredient's icon image. Null if it has none yet, in which case the order
        /// chip shows the ingredient's short name instead.</summary>
        public static Sprite Icon(IngredientType ingredient) =>
            ingredient != null ? ingredient.icon : null;

        /// <summary>A plain white circle, drawn once, for the coloured disc behind an icon on a
        /// shop sign or beacon.</summary>
        public static Sprite Disc()
        {
            if (_disc != null) return _disc;

            const int size = 64;
            float radius = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius, dy = y + 0.5f - radius;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();

            _disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _disc;
        }
    }
}
