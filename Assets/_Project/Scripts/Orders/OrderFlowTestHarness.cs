using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// TEMPORARY. Stands in for zone detection so the order state machine and the cargo system can
    /// be proven end to end, Available through Delivered or Late, before the vehicle can actually
    /// drive into a real pickup or drop-off zone.
    ///
    /// WHY THIS EXISTS
    /// Task 7 (order states) and Task 8 (cargo) are both built, but nothing has ever called
    /// CargoSystem.TryCollect or TryDeliver, because the thing that's meant to call them, zone
    /// detection, doesn't exist yet. Without this, there is no way to actually watch an order reach
    /// Collected or Carried, only to trust that the code would probably work.
    ///
    /// WHAT REPLACES THIS LATER
    /// Once zone detection exists, it calls exactly the same two CargoSystem methods this does,
    /// TryCollect when the vehicle enters a pickup trigger, TryDeliver when it enters the matching
    /// drop-off trigger, just driven by real collisions instead of a keyboard press. When that
    /// happens, delete this component. It is a lab tool, the same category as VehicleLabBuilder and
    /// VehicleMeasurement, not a game system, and it should not ship.
    ///
    /// KEYS
    ///   C   Try to collect the oldest Active order (simulates driving into its pickup zone)
    ///   V   Try to deliver whatever is currently carried (simulates reaching its drop-off zone)
    ///   L   Log the current state of every active order
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderFlowTestHarness : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private CargoSystem cargoSystem;

        private void Awake()
        {
            // FindAnyObjectByType, not FindFirstObjectByType: "first" is defined by instance ID
            // ordering, which Unity has deprecated because it will not survive the move to EntityId.
            // A scene should only ever hold one of each of these, so any match is the right match.
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (cargoSystem == null) cargoSystem = FindAnyObjectByType<CargoSystem>();

            if (orderManager == null || cargoSystem == null)
            {
                Debug.LogError(
                    "[OrderFlowTestHarness] Needs both an OrderManager and a CargoSystem in the " +
                    "scene. Nothing will happen until both exist.", this);
                enabled = false;
                return;
            }

            // A Late order that was being carried has to free the slot even though nobody
            // delivered it. Real zone detection will not need this wiring, a slot only ever empties
            // through an explicit TryDeliver call there, but until it exists this is the only
            // thing keeping a Late-while-carried order from leaving the slot stuck full forever.
            orderManager.OrderResolved += (order, value) => cargoSystem.ClearIfResolved(order);
        }

        private void Start()
        {
            Debug.Log("[OrderFlowTestHarness] TEMPORARY tool, stands in for zone detection. " +
                      "C = collect oldest active order, V = deliver what's carried, L = log states. " +
                      "Delete this component once real zone detection exists.");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.cKey.wasPressedThisFrame) TryCollectOldest();
            if (keyboard.vKey.wasPressedThisFrame) TryDeliverCarried();
            if (keyboard.lKey.wasPressedThisFrame) LogAllStates();
        }

        private void TryCollectOldest()
        {
            Order target = null;

            foreach (Order order in orderManager.ActiveOrders)
            {
                if (order.State != OrderState.Active) continue;
                target = order;
                break; // ActiveOrders is filled in spawn order, so the first match is the oldest.
            }

            if (target == null)
            {
                Debug.Log("[OrderFlowTestHarness] No Active order available to collect right now.");
                return;
            }

            bool ok = cargoSystem.TryCollect(target);
            Debug.Log(ok
                ? $"[OrderFlowTestHarness] Collected order {target.Id}. State is now {target.State}."
                : $"[OrderFlowTestHarness] Could not collect order {target.Id}.");
        }

        private void TryDeliverCarried()
        {
            Order carried = cargoSystem.CurrentOrder;

            if (carried == null)
            {
                Debug.Log("[OrderFlowTestHarness] Nothing is currently carried, nothing to deliver.");
                return;
            }

            bool ok = cargoSystem.TryDeliver(orderManager, carried);
            Debug.Log(ok
                ? $"[OrderFlowTestHarness] Delivered order {carried.Id}. State is now {carried.State}."
                : $"[OrderFlowTestHarness] Could not deliver order {carried.Id}.");
        }

        private void LogAllStates()
        {
            Debug.Log("[OrderFlowTestHarness] ---- Active orders ----");
            foreach (Order order in orderManager.ActiveOrders)
            {
                Debug.Log($"  Order {order.Id}: {order.State}, {order.TimeRemaining:0.0}s remaining");
            }
            Debug.Log($"[OrderFlowTestHarness] Cargo slot: " +
                      (cargoSystem.HasCapacity ? "empty" : $"carrying order {cargoSystem.CurrentOrder.Id}"));
        }
    }
}
