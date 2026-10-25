using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Decides which order the player is working on right now, so every piece of UI can agree on
    /// what to make loudest. This is the "always know what to deal with first" rule: however many
    /// orders are on screen, one of them is clearly the priority.
    ///
    /// AUTOMATIC BY DEFAULT
    /// With no input, the selected order is the one closest to being lost: the lowest patience,
    /// or the burn timer if its food is ready in a bay and that runs out first. Orders that are
    /// cooking are skipped, because there is nothing to drive to until they are ready.
    ///
    /// THE PLAYER CAN OVERRIDE IT
    /// Tab (or D-pad right) cycles through the orders, oldest first. A chosen order stays
    /// selected until it ends, then selection goes back to automatic. G (or D-pad up) turns the
    /// GPS route line on and off.
    ///
    /// The selected order gets the highlighted card, the full size arrow and the GPS line.
    /// Nothing here changes an order. Added automatically next to the OrderManager if no scene
    /// object has one.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderFocus : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Found automatically if left empty.")]
        [SerializeField] private OrderManager orderManager;

        [Header("Controls")]
        [SerializeField] private Key cycleKey = Key.Tab;
        [SerializeField] private Key gpsKey = Key.G;
        [Tooltip("D-pad right cycles orders, D-pad up toggles the GPS line.")]
        [SerializeField] private bool useGamepad = true;

        [Header("GPS")]
        [Tooltip("Whether the GPS route line starts switched on.")]
        [SerializeField] private bool gpsOnAtStart = true;

        [Header("Debug")]
        [SerializeField] private bool logActivity = false;

        /// <summary>The order the UI should make loudest. Null when there are no orders.</summary>
        public Order Selected { get; private set; }

        /// <summary>The order closest to being lost, whatever is selected.</summary>
        public Order MostUrgent { get; private set; }

        /// <summary>True while the player has picked an order with the cycle button.</summary>
        public bool IsManual => _manualId >= 0;

        public bool GpsOn { get; private set; }

        public event Action<Order> SelectionChanged;
        public event Action<bool> GpsToggled;

        private int _manualId = -1;

        /// <summary>The scene's OrderFocus, added next to the OrderManager if there is none.</summary>
        public static OrderFocus Ensure(OrderManager manager)
        {
            OrderFocus focus = FindAnyObjectByType<OrderFocus>();
            if (focus == null && manager != null) focus = manager.gameObject.AddComponent<OrderFocus>();
            return focus;
        }

        private void Awake()
        {
            if (orderManager == null) orderManager = GetComponent<OrderManager>();
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            GpsOn = gpsOnAtStart;

            if (orderManager == null)
            {
                Debug.LogError("[OrderFocus] No OrderManager in the scene. Switching off.", this);
                enabled = false;
                return;
            }

            // The GPS line reads the selection, so make sure there is one.
            if (FindAnyObjectByType<GpsRoute>() == null) gameObject.AddComponent<GpsRoute>();
        }

        private void Update()
        {
            ReadInput();
            Recalculate();
        }

        private void ReadInput()
        {
            bool cycle = false, toggle = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                cycle |= keyboard[cycleKey].wasPressedThisFrame;
                toggle |= keyboard[gpsKey].wasPressedThisFrame;
            }

            Gamepad pad = Gamepad.current;
            if (useGamepad && pad != null)
            {
                cycle |= pad.dpad.right.wasPressedThisFrame;
                toggle |= pad.dpad.up.wasPressedThisFrame;
            }

            if (cycle) Cycle();
            if (toggle) SetGps(!GpsOn);
        }

        /// <summary>Selects the next live order after the current one, oldest first, wrapping.</summary>
        public void Cycle()
        {
            IReadOnlyList<Order> orders = orderManager.ActiveOrders;
            int currentId = Selected != null ? Selected.Id : -1;
            int first = -1, next = -1;
            bool seenCurrent = false;

            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].IsTerminal) continue;
                if (first < 0) first = orders[i].Id;
                if (seenCurrent) { next = orders[i].Id; break; }
                if (orders[i].Id == currentId) seenCurrent = true;
            }

            _manualId = next >= 0 ? next : first;
            Recalculate();

            if (logActivity && Selected != null)
                Debug.Log($"[OrderFocus] Player selected order {Selected.Id} ({Selected.FoodName}).");
        }

        public void SetGps(bool on)
        {
            if (GpsOn == on) return;
            GpsOn = on;
            GpsToggled?.Invoke(on);
            if (logActivity) Debug.Log($"[OrderFocus] GPS {(on ? "on" : "off")}.");
        }

        private void Recalculate()
        {
            IReadOnlyList<Order> orders = orderManager.ActiveOrders;

            Order manual = null, urgentActionable = null, urgentAny = null;
            for (int i = 0; i < orders.Count; i++)
            {
                Order o = orders[i];
                if (o.IsTerminal) continue;

                if (o.Id == _manualId) manual = o;
                if (urgentAny == null || o.SecondsUntilLost < urgentAny.SecondsUntilLost) urgentAny = o;
                if (o.HasWorldTarget &&
                    (urgentActionable == null || o.SecondsUntilLost < urgentActionable.SecondsUntilLost))
                    urgentActionable = o;
            }

            // The chosen order ended: back to automatic.
            if (manual == null) _manualId = -1;

            MostUrgent = urgentAny;
            Order selected = manual ?? urgentActionable ?? urgentAny;

            if (selected != Selected)
            {
                Selected = selected;
                SelectionChanged?.Invoke(selected);
            }
        }
    }
}
