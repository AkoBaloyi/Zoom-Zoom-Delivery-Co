using UnityEngine;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// The one place every order colour is defined, so a step has the same colour on the order
    /// card, the map beacons, the screen arrows and the bays by construction.
    ///   Blue    gather ingredients
    ///   Orange  cook at a Processing Bay
    ///   Green   deliver to the customer (and food ready to collect)
    ///   Red     urgent, burning, cancelled
    ///
    /// The ticket styling (cream paper, brown ink, coloured headers) follows the project plan's
    /// Concept section: orders appear on screen "with a countdown attached exactly like an
    /// Overcooked ticket".
    /// </summary>
    public static class OrderPalette
    {
        // Step colours
        public static readonly Color Pickup = new Color(0.20f, 0.60f, 1.00f, 1f);   // blue
        public static readonly Color Cook = new Color(0.98f, 0.55f, 0.12f, 1f);     // orange
        public static readonly Color Deliver = new Color(0.20f, 0.85f, 0.35f, 1f);  // green
        public static readonly Color Warning = new Color(1.00f, 0.85f, 0.10f, 1f);  // yellow
        public static readonly Color Urgent = new Color(0.95f, 0.15f, 0.12f, 1f);   // red

        // Ticket and panel styling
        public static readonly Color Paper = new Color(0.98f, 0.93f, 0.78f, 1f);
        public static readonly Color Ink = new Color(0.25f, 0.15f, 0.08f, 1f);
        public static readonly Color PanelDark = new Color(0.12f, 0.08f, 0.06f, 0.78f);
        public static readonly Color BadgeOff = new Color(0.80f, 0.74f, 0.60f, 1f);
        public static readonly Color Money = new Color(1.00f, 0.80f, 0.20f, 1f);
        public static readonly Color Neutral = Color.white;

        /// <summary>
        /// One colour per live order, shared by its card, beacons, arrow and GPS line. Four, to
        /// match the four order cap. Chosen to stay clear of the colours that mean something else:
        /// red (urgent), green (ready) and orange (cooking).
        /// </summary>
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

        /// <summary>The order's colour, flashing toward red once it is about to be lost, so the
        /// marker shows both whose it is and how soon it expires.</summary>
        public static Color OrderColourWithUrgency(Order order, float urgentBelowSeconds, float time)
        {
            Color c = OrderColour(order);
            float left = order.SecondsUntilLost;
            if (left >= urgentBelowSeconds) return c;

            float urgency = 1f - Mathf.Clamp01(left / Mathf.Max(0.01f, urgentBelowSeconds));
            float flash = 0.5f + 0.5f * Mathf.Sin(time * Mathf.Lerp(6f, 16f, urgency));
            return Color.Lerp(c, Urgent, flash * Mathf.Lerp(0.4f, 1f, urgency));
        }

        /// <summary>"RRGGBB" for rich text colour tags.</summary>
        public static string Hex(Color c)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
            int g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f);
            int b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        /// <summary>Header colours cycled by order id, for orders with no food set.</summary>
        private static readonly Color[] TicketHeaders =
        {
            new Color(0.93f, 0.36f, 0.27f, 1f),
            new Color(0.25f, 0.62f, 0.90f, 1f),
            new Color(0.38f, 0.74f, 0.35f, 1f),
            new Color(0.95f, 0.70f, 0.18f, 1f),
            new Color(0.70f, 0.42f, 0.85f, 1f),
            new Color(0.95f, 0.48f, 0.65f, 1f),
        };

        public static Color TicketHeader(int orderId)
        {
            int index = ((orderId - 1) % TicketHeaders.Length + TicketHeaders.Length) % TicketHeaders.Length;
            return TicketHeaders[index];
        }

        public static Color ForStep(OrderStep step)
        {
            switch (step)
            {
                case OrderStep.Gather: return Pickup;
                case OrderStep.Cook: return Cook;
                default: return Deliver;
            }
        }

        /// <summary>The colour of wherever this order wants the car to go right now. Food
        /// waiting in a bay is green, because it is ready to collect.</summary>
        public static Color ForOrder(Order order)
        {
            if (order.State == OrderState.Ready) return Deliver;
            return ForStep(order.CurrentStep);
        }

        /// <summary>Blends toward red as the order runs out of time.</summary>
        public static Color WithUrgency(Color baseColour, float timeRemaining, float urgentBelowSeconds)
        {
            float urgency = 1f - Mathf.Clamp01(timeRemaining / Mathf.Max(0.01f, urgentBelowSeconds));
            return Color.Lerp(baseColour, Urgent, urgency);
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