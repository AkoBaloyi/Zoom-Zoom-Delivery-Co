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