using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ZoomZoom.Orders.UI;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// A sign fixed to a building: a board in the shop's colour with its icon, its name, and what
    /// it sells, for example a lilac DAIRY sign with a milk carton and "Milk, Cheese, Eggs".
    /// It does not float or turn to face the camera; it stays on the wall like a real shop sign.
    /// It can also go over a cooking bay.
    ///
    /// HOW TO USE
    /// 1. Right click the building in the Hierarchy and choose Create Empty, so the sign is a
    ///    child of the building and moves with it.
    /// 2. Add Component > Shop Sign, then drag the shop (the object with Ingredient Source) into
    ///    Shop. For a bay, drag the bay into Bay instead.
    /// 3. Move it onto the wall above the door, and turn it so the blue arrow points out toward
    ///    the road. The yellow box in the Scene view shows exactly where the sign will be.
    /// 4. On the shop itself, untick Show Label to remove the old floating text. The beacon
    ///    still marks the shop whenever an order needs it.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShopSign : MonoBehaviour
    {
        [Tooltip("The shop this sign is for.")]
        [SerializeField] private IngredientSource shop;

        [Tooltip("Or a cooking bay, for a sign over a bay. Leave Shop empty if you use this.")]
        [SerializeField] private ProcessingBay bay;

        [Header("Size (metres)")]
        [SerializeField] private float width = 6f;
        [SerializeField] private float height = 1.8f;

        [Tooltip("Show what the shop sells under its name.")]
        [SerializeField] private bool showWhatItSells = true;

        private const float Depth = 0.15f;

        private void Start()
        {
            // Work out what the sign says and its colour.
            string title;
            string subtitle = "";
            Color colour;
            Sprite icon = null;

            if (shop != null)
            {
                title = WithoutNumber(shop.PlaceName).ToUpper();
                colour = OrderPalette.Pickup;

                var names = new List<string>();
                foreach (IngredientType item in shop.Supplies)
                {
                    if (item == null) continue;
                    names.Add(item.displayName);
                    if (icon == null)
                    {
                        icon = IngredientIcons.Icon(item);
                        colour = IngredientIcons.Colour(item);
                    }
                }
                if (showWhatItSells) subtitle = string.Join(", ", names);
            }
            else if (bay != null)
            {
                title = bay.BayName.ToUpper();
                subtitle = showWhatItSells ? "Cooking bay" : "";
                colour = OrderPalette.Cook;
            }
            else
            {
                Debug.LogWarning("[ShopSign] No Shop or Bay assigned, so this sign has nothing to show.", this);
                return;
            }

            BuildSign(title, subtitle, colour, icon);
        }

        private void BuildSign(string title, string subtitle, Color colour, Sprite icon)
        {
            // The board: a dark frame with the coloured panel just in front of it.
            MakeBox("Frame", new Vector3(width + 0.2f, height + 0.2f, Depth), Vector3.zero, Color.Lerp(colour, Color.black, 0.55f));
            MakeBox("Panel", new Vector3(width, height, Depth), new Vector3(0f, 0f, 0.02f), colour);

            // Everything printed on the front goes on this "face", turned to look out along the
            // blue arrow, so the text reads the right way round from the road.
            var face = new GameObject("Face");
            face.transform.SetParent(transform, false);
            face.transform.localPosition = new Vector3(0f, 0f, Depth * 0.5f + 0.04f);
            face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            float left = -width * 0.5f;
            float textStart = left + 0.3f;

            // Icon on a white circle at the left end.
            if (icon != null)
            {
                float circle = height * 0.78f;

                SpriteRenderer disc = MakeSprite(face.transform, "Icon circle", IngredientIcons.Disc(), Color.white, 0);
                disc.transform.localPosition = new Vector3(left + 0.2f + circle * 0.5f, 0f, 0f);
                disc.transform.localScale = Vector3.one * circle / Mathf.Max(0.01f, disc.sprite.bounds.size.x);

                SpriteRenderer picture = MakeSprite(face.transform, "Icon", icon, colour, 1);
                picture.transform.localPosition = new Vector3(left + 0.2f + circle * 0.5f, 0f, -0.01f);
                picture.transform.localScale = Vector3.one * (circle * 0.72f) / Mathf.Max(0.01f, icon.bounds.size.x);

                textStart = left + 0.4f + circle;
            }

            float textWidth = width * 0.5f - 0.2f - textStart;
            bool twoLines = !string.IsNullOrEmpty(subtitle);

            MakeText(face.transform, "Name", title, new Vector3(textStart, twoLines ? height * 0.16f : 0f, 0f),
                height * (twoLines ? 0.36f : 0.45f), textWidth);

            if (twoLines)
                MakeText(face.transform, "Sells", subtitle, new Vector3(textStart, -height * 0.24f, 0f),
                    height * 0.2f, textWidth);
        }

        // ---- small helpers ---------------------------------------------------------------------

        /// <summary>A flat coloured box with no collider.</summary>
        private void MakeBox(string name, Vector3 size, Vector3 position, Color colour)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(transform, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;

            MeshRenderer boxRenderer = box.GetComponent<MeshRenderer>();
            boxRenderer.sharedMaterial = OrderWorldMaterial.Get();
            var block = new MaterialPropertyBlock();
            OrderWorldMaterial.SetColour(block, colour);
            boxRenderer.SetPropertyBlock(block);
        }

        private static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Color colour, int order)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.SetParent(parent, false);
            SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = colour;
            spriteRenderer.sortingOrder = order;
            return spriteRenderer;
        }

        /// <summary>White bold text, left aligned, that shrinks itself to fit its box.
        /// TextMeshPro rather than the older TextMesh, because TextMesh draws on top of
        /// everything and the sign would show through other buildings.</summary>
        private static void MakeText(Transform parent, string name, string words, Vector3 position, float lineHeight, float maxWidth)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.text = words;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            // The box the text must fit in: maxWidth wide, one line tall, starting at position.
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(maxWidth, lineHeight);
            text.rectTransform.localPosition = position;

            text.enableAutoSizing = true;
            text.fontSizeMin = 0.1f;
            text.fontSizeMax = 100f;
        }

        /// <summary>"Butcher 2" becomes "Butcher", so the second shop's sign looks the same.</summary>
        private static string WithoutNumber(string name)
        {
            int space = name.LastIndexOf(' ');
            if (space > 0 && int.TryParse(name.Substring(space + 1), out _)) return name.Substring(0, space);
            return name;
        }

        /// <summary>Shows where the sign will be, and which way it faces, before pressing Play.</summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, height, Depth));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 1.5f);
        }
    }
}
