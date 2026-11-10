using UnityEngine;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// Every order colour in one place, so the same thing has the same colour on the phone, the
    /// beacons, the arrows, the bays and the HUD.
    ///   Blue    gather ingredients
    ///   Orange  cooking at a bay
    ///   Green   ready, delivered, money in
    ///   Red     urgent, burned, cancelled, money out
    /// Each live order also has its own colour (blue, yellow, magenta or cyan) shared by its card,
    /// beacons, arrow and GPS line, so the player can tell which marker belongs to which order.
    /// </summary>
    public static class OrderPalette
    {
        // Step colours
        public static readonly Color Pickup = new Color(0.20f, 0.60f, 1.00f, 1f);   // blue
        public static readonly Color Cook = new Color(0.98f, 0.55f, 0.12f, 1f);     // orange
        public static readonly Color Deliver = new Color(0.20f, 0.85f, 0.35f, 1f);  // green
        public static readonly Color Warning = new Color(1.00f, 0.85f, 0.10f, 1f);  // yellow
        public static readonly Color Urgent = new Color(0.95f, 0.15f, 0.12f, 1f);   // red

        // Card and HUD styling
        public static readonly Color Paper = new Color(0.98f, 0.93f, 0.78f, 1f);
        public static readonly Color Ink = new Color(0.25f, 0.15f, 0.08f, 1f);
        public static readonly Color BadgeOff = new Color(0.80f, 0.74f, 0.60f, 1f);
        public static readonly Color Money = new Color(1.00f, 0.80f, 0.20f, 1f);
        public static readonly Color Neutral = Color.white;

        /// <summary>One colour per live order. Four, to match the four order limit, and chosen to
        /// stay clear of red (urgent), green (ready) and orange (cooking).</summary>
        private static readonly Color[] OrderColours =
        {
            new Color(0.20f, 0.55f, 1.00f, 1f), // blue
            new Color(1.00f, 0.84f, 0.10f, 1f), // yellow
            new Color(0.92f, 0.30f, 0.85f, 1f), // magenta
            new Color(0.10f, 0.90f, 0.80f, 1f), // cyan
        };

        public static int OrderColourCount => OrderColours.Length;

        public static Color OrderColour(int index) =>
            OrderColours[((index % OrderColours.Length) + OrderColours.Length) % OrderColours.Length];

        public static Color OrderColour(Order order) => OrderColour(order.ColourIndex);

        /// <summary>
        /// A size multiplier for markers: exactly 1 while the order has time, then a smooth swell
        /// that grows and quickens as it runs out (up to 30% bigger). A slow pulse, never a
        /// flicker; the ticking sound (OrderAudio) does the rest.
        /// </summary>
        public static float UrgencyPulse(Order order, float urgentBelowSeconds, float time)
        {
            float left = order.SecondsUntilLost;
            if (left >= urgentBelowSeconds) return 1f;

            float urgency = 1f - Mathf.Clamp01(left / Mathf.Max(0.01f, urgentBelowSeconds));
            float wave = 0.5f + 0.5f * Mathf.Sin(time * Mathf.Lerp(4f, 12f, urgency));
            return 1f + wave * Mathf.Lerp(0.1f, 0.3f, urgency);
        }

        /// <summary>"RRGGBB" for rich text colour tags.</summary>
        public static string Hex(Color c)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
            int g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f);
            int b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>Green, then yellow, then red, for a draining timer bar.</summary>
        public static Color TimerBar(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            return fraction > 0.5f
                ? Color.Lerp(Warning, Deliver, (fraction - 0.5f) * 2f)
                : Color.Lerp(Urgent, Warning, fraction * 2f);
        }
    }
}
