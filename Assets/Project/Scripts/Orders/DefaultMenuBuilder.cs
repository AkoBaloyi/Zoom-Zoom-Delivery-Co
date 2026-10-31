#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZoomZoom.Orders.EditorTools
{
    /// <summary>
    /// Three Unity menu commands:
    ///
    /// Zoom Zoom > Create Default Menu
    ///   Makes the ingredient assets and one Food Type per menu item in Assets/Project/Data/Menu,
    ///   fills in each food's recipe and cook time, and adds every food to the Menu of every
    ///   Order Tuning asset. Running it again only adds what is missing. A food you have already
    ///   given ingredients to is never overwritten.
    ///
    /// Zoom Zoom > Add Second Shop Of Each Type
    ///   Copies every shop to the opposite side of the bay, so each ingredient is sold in two
    ///   parts of the map and the player has a route to choose.
    ///
    /// Zoom Zoom > Add Default Kitchen To Scene
    ///   Adds the five shops (Butcher, Bakery, Market, Fish Market, Dairy) as Ingredient Sources
    ///   and one Processing Bay to the open scene, around the middle of the Scene view, so the
    ///   loop can be played straight away. Drag them to where they belong on the map afterwards.
    ///
    /// Editor only: wrapped in UNITY_EDITOR so it never breaks a build, wherever it is placed.
    /// </summary>
    public static class DefaultMenuBuilder
    {
        private const string Folder = "Assets/Project/Data/Menu";
        private const string IngredientFolder = "Assets/Project/Data/Menu/Ingredients";

        private struct IngredientDef
        {
            public string Name, Label, Place;
            public Color Colour;
            public IngredientDef(string name, string label, string place, Color colour)
            {
                Name = name; Label = label; Place = place; Colour = colour;
            }
        }

        private struct FoodDef
        {
            public string Name;
            public float BasePay, Weight, Cook, Burn;
            public Color Colour;
            public string[] Recipe;
            public FoodDef(string name, float basePay, float weight, Color colour, float cook, float burn, params string[] recipe)
            {
                Name = name; BasePay = basePay; Weight = weight; Colour = colour; Cook = cook; Burn = burn; Recipe = recipe;
            }
        }

        // Ingredient, card label, the shop that sells it, chip colour once collected.
        private static readonly IngredientDef[] Ingredients =
        {
            new IngredientDef("Meat",       "MEAT",  "Butcher",     new Color(0.75f, 0.22f, 0.20f)),
            new IngredientDef("Chicken",    "CHKN",  "Butcher",     new Color(0.85f, 0.55f, 0.30f)),
            new IngredientDef("Bun",        "BUN",   "Bakery",      new Color(0.80f, 0.60f, 0.25f)),
            new IngredientDef("Bread",      "BREAD", "Bakery",      new Color(0.70f, 0.50f, 0.25f)),
            new IngredientDef("Dough",      "DOUGH", "Bakery",      new Color(0.78f, 0.68f, 0.45f)),
            new IngredientDef("Vegetables", "VEG",   "Market",      new Color(0.30f, 0.65f, 0.25f)),
            new IngredientDef("Rice",       "RICE",  "Market",      new Color(0.55f, 0.55f, 0.50f)),
            new IngredientDef("Noodles",    "NOOD",  "Market",      new Color(0.85f, 0.70f, 0.20f)),
            new IngredientDef("Fish",       "FISH",  "Fish Market", new Color(0.25f, 0.55f, 0.85f)),
            new IngredientDef("Milk",       "MILK",  "Dairy",       new Color(0.45f, 0.65f, 0.90f)),
            new IngredientDef("Cheese",     "CHSE",  "Dairy",       new Color(0.90f, 0.75f, 0.15f)),
            new IngredientDef("Eggs",       "EGGS",  "Dairy",       new Color(0.85f, 0.60f, 0.40f)),
        };

        private static readonly string[] Places = { "Butcher", "Bakery", "Market", "Fish Market", "Dairy" };

        // Name, base pay in Rand, how often it is ordered, card colour, cook seconds, burn seconds, recipe.
        // Cheaper everyday foods turn up more often; pricier ones are rarer.
        private static readonly FoodDef[] Foods =
        {
            new FoodDef("Fried Rice",     250f, 1.2f, new Color(0.93f, 0.36f, 0.27f),  8f, 12f, "Rice", "Vegetables"),
            new FoodDef("Burger",         280f, 1.5f, new Color(0.85f, 0.55f, 0.20f), 10f, 12f, "Meat", "Bun"),
            new FoodDef("Pizza",          320f, 1.3f, new Color(0.90f, 0.25f, 0.20f), 12f, 12f, "Dough", "Cheese"),
            new FoodDef("Bunny Chow",     300f, 1.0f, new Color(0.95f, 0.65f, 0.15f), 12f, 12f, "Bread", "Meat", "Vegetables"),
            new FoodDef("Boerewors Roll", 220f, 1.2f, new Color(0.60f, 0.30f, 0.20f),  6f, 12f, "Meat", "Bun"),
            new FoodDef("Sushi",          380f, 0.7f, new Color(0.25f, 0.62f, 0.90f),  6f, 10f, "Fish", "Rice"),
            new FoodDef("Soup",           260f, 0.8f, new Color(0.38f, 0.74f, 0.35f), 10f, 12f, "Vegetables"),
            new FoodDef("Tacos",          300f, 1.0f, new Color(0.95f, 0.75f, 0.20f),  8f, 12f, "Meat", "Cheese", "Vegetables"),
            new FoodDef("Ramen",          300f, 0.9f, new Color(0.70f, 0.42f, 0.85f), 10f, 12f, "Noodles", "Chicken"),
            new FoodDef("Chicken Wings",  270f, 1.1f, new Color(0.80f, 0.35f, 0.15f), 12f, 12f, "Chicken"),
            new FoodDef("Milkshake",      180f, 1.0f, new Color(0.95f, 0.48f, 0.65f),  4f, 10f, "Milk"),
            new FoodDef("Birthday Cake",  450f, 0.4f, new Color(0.55f, 0.80f, 0.90f), 15f, 15f, "Dough", "Milk", "Eggs"),
        };

        // ---- menu -------------------------------------------------------------------------------

        [MenuItem("Zoom Zoom/Create Default Menu")]
        public static void CreateDefaultMenu()
        {
            EnsureFolder(Folder);
            EnsureFolder(IngredientFolder);

            Dictionary<string, IngredientType> ingredients = LoadOrCreateIngredients(out int ingredientsCreated);

            var foods = new List<FoodType>();
            int created = 0, recipesFilled = 0;

            foreach (FoodDef def in Foods)
            {
                string path = $"{Folder}/Food_{def.Name.Replace(" ", "")}.asset";
                FoodType food = AssetDatabase.LoadAssetAtPath<FoodType>(path);

                if (food == null)
                {
                    food = ScriptableObject.CreateInstance<FoodType>();
                    food.displayName = def.Name;
                    food.basePay = def.BasePay;
                    food.spawnWeight = def.Weight;
                    food.cardColour = new Color(def.Colour.r, def.Colour.g, def.Colour.b, 1f);
                    AssetDatabase.CreateAsset(food, path);
                    created++;
                }

                // Fill the recipe only if this food has none yet, so a tuned recipe is kept.
                if (food.ingredients == null || food.ingredients.Length == 0)
                {
                    var recipe = new List<IngredientType>();
                    foreach (string name in def.Recipe)
                        if (ingredients.TryGetValue(name, out IngredientType ing)) recipe.Add(ing);

                    food.ingredients = recipe.ToArray();
                    food.cookSeconds = def.Cook;
                    food.burnSeconds = def.Burn;
                    EditorUtility.SetDirty(food);
                    recipesFilled++;
                }

                foods.Add(food);
            }

            // Add every food to every Order Tuning asset that does not have it yet.
            int tuningCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:OrderTuning"))
            {
                var tuning = AssetDatabase.LoadAssetAtPath<OrderTuning>(AssetDatabase.GUIDToAssetPath(guid));
                if (tuning == null) continue;

                var menu = new List<FoodType>();
                if (tuning.menu != null)
                    foreach (FoodType f in tuning.menu)
                        if (f != null && !menu.Contains(f)) menu.Add(f);

                foreach (FoodType f in foods)
                    if (!menu.Contains(f)) menu.Add(f);

                tuning.menu = menu.ToArray();
                EditorUtility.SetDirty(tuning);
                tuningCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DefaultMenuBuilder] {ingredientsCreated} new ingredient(s), {created} new " +
                      $"food(s), {recipesFilled} recipe(s) filled in. {foods.Count} foods on the menu " +
                      $"of {tuningCount} Order Tuning asset(s). Next: Zoom Zoom > Add Default " +
                      "Kitchen To Scene, or add Ingredient Sources by hand.");

            if (foods.Count > 0) EditorGUIUtility.PingObject(foods[0]);
        }

        private static Dictionary<string, IngredientType> LoadOrCreateIngredients(out int createdCount)
        {
            createdCount = 0;
            var result = new Dictionary<string, IngredientType>();

            foreach (IngredientDef def in Ingredients)
            {
                string path = $"{IngredientFolder}/Ingredient_{def.Name.Replace(" ", "")}.asset";
                IngredientType ing = AssetDatabase.LoadAssetAtPath<IngredientType>(path);

                if (ing == null)
                {
                    ing = ScriptableObject.CreateInstance<IngredientType>();
                    ing.displayName = def.Name;
                    ing.shortLabel = def.Label;
                    ing.category = CategoryForPlace(def.Place);
                    AssetDatabase.CreateAsset(ing, path);
                    createdCount++;
                }

                result[def.Name] = ing;
            }

            return result;
        }

        // ---- kitchen ----------------------------------------------------------------------------

        [MenuItem("Zoom Zoom/Add Default Kitchen To Scene")]
        public static void AddDefaultKitchen()
        {
            // Make sure the ingredient assets exist first.
            EnsureFolder(Folder);
            EnsureFolder(IngredientFolder);
            Dictionary<string, IngredientType> ingredients = LoadOrCreateIngredients(out int made);
            if (made > 0) AssetDatabase.SaveAssets();

            var existing = new HashSet<string>();
            foreach (IngredientSource s in Object.FindObjectsByType<IngredientSource>())
                existing.Add(s.PlaceName);

            Vector3 centre = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            const float ringRadius = 70f;

            var root = new GameObject("Kitchen");
            root.transform.position = Ground(centre);
            Undo.RegisterCreatedObjectUndo(root, "Add Default Kitchen");

            int added = 0;
            for (int i = 0; i < Places.Length; i++)
            {
                string place = Places[i];
                if (existing.Contains(place)) continue;

                var supplies = new List<IngredientType>();
                foreach (IngredientDef def in Ingredients)
                    if (def.Place == place && ingredients.TryGetValue(def.Name, out IngredientType ing))
                        supplies.Add(ing);

                float angle = i / (float)Places.Length * Mathf.PI * 2f;
                Vector3 position = Ground(centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius);

                var go = new GameObject(place);
                go.transform.SetParent(root.transform, true);
                go.transform.position = position;
                go.AddComponent<IngredientSource>().Configure(place, supplies.ToArray());
                added++;
            }

            bool hasBay = Object.FindObjectsByType<ProcessingBay>().Length > 0;
            if (!hasBay)
            {
                var bay = new GameObject("Bay 1");
                bay.transform.SetParent(root.transform, true);
                bay.transform.position = Ground(centre);
                bay.AddComponent<ProcessingBay>();
            }

            if (added == 0 && hasBay)
            {
                Undo.DestroyObjectImmediate(root);
                Debug.Log("[DefaultMenuBuilder] The scene already has all five shops and a bay. Nothing added.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            Debug.Log($"[DefaultMenuBuilder] Added {added} shop(s)" + (hasBay ? "" : " and Bay 1") +
                      " under 'Kitchen', in a ring around the middle of the Scene view. Drag them " +
                      "to where they belong on the map, then save the scene.");
        }

        // ---- second shops -----------------------------------------------------------------------

        /// <summary>
        /// Copies every shop in the scene to the opposite side of the bay, named "<shop> 2", so
        /// each ingredient can be bought in two places in different parts of the map. Orders then
        /// light up both, and the player decides which one fits their route: the choice the
        /// tutor found missing when every trip was a fixed A to B.
        /// </summary>
        [MenuItem("Zoom Zoom/Add Second Shop Of Each Type")]
        public static void AddSecondShops()
        {
            ProcessingBay[] bays = Object.FindObjectsByType<ProcessingBay>();
            IngredientSource[] sources = Object.FindObjectsByType<IngredientSource>();

            if (sources.Length == 0)
            {
                Debug.LogWarning("[DefaultMenuBuilder] No shops in the scene yet. Run Zoom Zoom > " +
                                 "Add Default Kitchen To Scene first.");
                return;
            }

            // Mirror through the bay (or the middle of the shops if there is no bay).
            Vector3 centre = Vector3.zero;
            if (bays.Length > 0) centre = bays[0].transform.position;
            else
            {
                foreach (IngredientSource src in sources) centre += src.transform.position;
                centre /= sources.Length;
            }

            var existing = new HashSet<string>();
            foreach (IngredientSource src in sources) existing.Add(src.PlaceName);

            int added = 0;
            foreach (IngredientSource src in sources)
            {
                string name = src.PlaceName + " 2";
                if (src.PlaceName.EndsWith(" 2") || existing.Contains(name)) continue;

                Vector3 mirrored = centre + (centre - src.transform.position);
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Add Second Shops");
                go.transform.SetParent(src.transform.parent, true);
                go.transform.position = Ground(mirrored);

                var supplies = new List<IngredientType>(src.Supplies);
                go.AddComponent<IngredientSource>().Configure(name, supplies.ToArray());
                existing.Add(name);
                added++;
            }

            if (added > 0) EditorSceneManager.MarkSceneDirty(sources[0].gameObject.scene);
            Debug.Log($"[DefaultMenuBuilder] Added {added} second shop(s), mirrored to the other side " +
                      "of the bay. Move them onto roads in a different part of the map, then save.");
        }

        private static IngredientCategory CategoryForPlace(string place)
        {
            switch (place)
            {
                case "Butcher": return IngredientCategory.Meat;
                case "Bakery": return IngredientCategory.Bakery;
                case "Fish Market": return IngredientCategory.Seafood;
                case "Dairy": return IngredientCategory.Dairy;
                default: return IngredientCategory.Produce;
            }
        }

        /// <summary>Drops a point onto whatever collider is below it, so markers sit on the road.</summary>
        private static Vector3 Ground(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 500f))
                return hit.point;
            return new Vector3(point.x, 0f, point.z);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
