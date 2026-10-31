#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ZoomZoom.Orders.EditorTools
{
    /// <summary>
    /// Puts each ingredient's icon image into that ingredient's Icon field.
    ///
    /// HOW TO USE
    /// 1. Put the Icon_ PNG files in Assets/Project/Art/Ingredients.
    /// 2. In the top menu bar, click Zoom Zoom > Assign Ingredient Icons.
    ///
    /// It matches by name: Icon_Meat.png goes to the ingredient called Meat, Icon_Rice.png to
    /// Rice, and so on. You can also do the same thing by hand: select an ingredient asset and
    /// drag its image onto the Icon field.
    /// </summary>
    public static class AssignIngredientIcons
    {
        private const string IconFolder = "Assets/Project/Art/Ingredients";

        [MenuItem("Zoom Zoom/Assign Ingredient Icons")]
        public static void Assign()
        {
            int assigned = 0;

            // Find every Ingredient asset in the project.
            string[] ingredientIds = AssetDatabase.FindAssets("t:IngredientType");

            foreach (string id in ingredientIds)
            {
                string ingredientPath = AssetDatabase.GUIDToAssetPath(id);
                IngredientType ingredient = AssetDatabase.LoadAssetAtPath<IngredientType>(ingredientPath);
                if (ingredient == null) continue;

                // For example "Vegetables" looks for Assets/Project/Art/Ingredients/Icon_Vegetables.png
                string imagePath = IconFolder + "/Icon_" + ingredient.displayName.Replace(" ", "") + ".png";

                TextureImporter importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning("[AssignIngredientIcons] No image for " + ingredient.displayName +
                                     ". Expected it at " + imagePath);
                    continue;
                }

                // UI images and sprite renderers can only use an image imported as a Sprite.
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spritePixelsPerUnit = 256; // the icons are 256 pixels, so 1 unit wide
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }

                ingredient.icon = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
                EditorUtility.SetDirty(ingredient);
                assigned++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AssignIngredientIcons] Gave " + assigned + " ingredient(s) their icon.");
        }
    }
}
#endif
