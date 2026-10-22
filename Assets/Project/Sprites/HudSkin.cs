using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// All the HUD art in one asset, shared by OrderTicketStrip and OrderHUD, so the sprites are
    /// assigned once instead of on every component. Every field is optional: anything left empty
    /// falls back to the flat colour the code already draws, so the HUD works with no art, some
    /// art, or all of it.
    ///
    /// SLICING IS DONE IN CODE
    /// Pills (timer bars) and frames (stats panel, ticket paper) have to stretch without
    /// squashing their rounded ends. Normally that means setting borders by hand in Unity's
    /// Sprite Editor. Instead, ApplyPill and ApplyFrame build a bordered copy of the sprite at
    /// runtime and scale it to the target height, so the PNGs can be dropped in as they are.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HudSkin",
        menuName = "Zoom Zoom/HUD Skin",
        order = 3)]
    public class HudSkin : ScriptableObject
    {
        [Header("Order tickets")]
        [Tooltip("Optional. Ticket paper. Leave empty to use the flat cream colour.")]
        public Sprite ticketPaper;
        [Tooltip("White rounded square. Tinted blue, green or grey in code.")]
        public Sprite badgeBackground;
        public Sprite iconPickup;
        public Sprite iconDropOff;
        [Tooltip("Shown on a badge once that step is done.")]
        public Sprite iconDone;
        public Sprite stampDelivered;
        public Sprite stampLate;

        [Header("Bars (order patience bar)")]
        public Sprite barBackground;
        [Tooltip("White pill. Tinted green, yellow or red in code.")]
        public Sprite barFill;

        [Header("Stats panel")]
        public Sprite statsPanel;
        public Sprite cargoSlotEmpty;
        public Sprite cargoSlotFilled;

        [Header("Frame slicing")]
        [Range(0.05f, 0.45f)]
        [Tooltip("How much of the panel and ticket art, as a fraction of its height, is frame " +
                 "that must not stretch. Raise it if the wood edge of the panel looks squashed.")]
        public float frameBorderFraction = 0.16f;

        /// <summary>Logs, once at start, whether a skin is assigned and which of its slots are
        /// empty, so "the art is not showing" can be answered from the Console.</summary>
        public static void LogStatus(HudSkin skin, string owner, Object context)
        {
            if (skin == null)
            {
                Debug.Log($"[{owner}] No HUD Skin assigned, drawing flat colours. Drag a HUD Skin " +
                          "asset into the Skin field (in Edit mode, not while playing).", context);
                return;
            }

            var missing = new List<string>();
            if (skin.badgeBackground == null) missing.Add("Badge Background");
            if (skin.iconPickup == null) missing.Add("Icon Pickup");
            if (skin.iconDropOff == null) missing.Add("Icon Drop Off");
            if (skin.iconDone == null) missing.Add("Icon Done");
            if (skin.stampDelivered == null) missing.Add("Stamp Delivered");
            if (skin.stampLate == null) missing.Add("Stamp Late");
            if (skin.barBackground == null) missing.Add("Bar Background");
            if (skin.barFill == null) missing.Add("Bar Fill");
            if (skin.statsPanel == null) missing.Add("Stats Panel");
            if (skin.cargoSlotEmpty == null) missing.Add("Cargo Slot Empty");
            if (skin.cargoSlotFilled == null) missing.Add("Cargo Slot Filled");

            Debug.Log(missing.Count == 0
                ? $"[{owner}] Using HUD Skin '{skin.name}', all sprites assigned."
                : $"[{owner}] Using HUD Skin '{skin.name}'. Empty slots (flat colour used): " +
                  string.Join(", ", missing), context);
        }

        private static readonly Dictionary<(Sprite, Vector4), Sprite> SlicedCache =
            new Dictionary<(Sprite, Vector4), Sprite>();

        /// <summary>Puts a horizontal pill sprite on an Image so it stretches to any width at the
        /// given height with round ends intact. Returns false (and changes nothing) if sprite is null.</summary>
        public static bool ApplyPill(Image image, Sprite sprite, float targetHeight)
        {
            if (image == null || sprite == null) return false;

            float h = sprite.rect.height;
            var border = new Vector4(h * 0.5f, h * 0.5f - 1f, h * 0.5f, h * 0.5f - 1f);
            ApplySliced(image, sprite, border, h / Mathf.Max(1f, targetHeight));
            return true;
        }

        /// <summary>Puts a framed panel sprite on an Image so its frame keeps its thickness at
        /// any size. Returns false (and changes nothing) if sprite is null.</summary>
        public bool ApplyFrame(Image image, Sprite sprite, float targetHeight)
        {
            if (image == null || sprite == null) return false;

            float b = sprite.rect.height * frameBorderFraction;
            ApplySliced(image, sprite, new Vector4(b, b, b, b), sprite.rect.height / Mathf.Max(1f, targetHeight));
            return true;
        }

        /// <summary>Plain icon: whole sprite, aspect kept. Returns false if sprite is null.</summary>
        public static bool ApplyIcon(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return false;

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            return true;
        }

        private static void ApplySliced(Image image, Sprite sprite, Vector4 border, float pixelsPerUnitMultiplier)
        {
            var key = (sprite, border);
            if (!SlicedCache.TryGetValue(key, out Sprite sliced) || sliced == null)
            {
                sliced = Sprite.Create(sprite.texture, sprite.rect, new Vector2(0.5f, 0.5f),
                    sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
                sliced.name = sprite.name + " (sliced)";
                SlicedCache[key] = sliced;
            }

            image.sprite = sliced;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        }
    }
}