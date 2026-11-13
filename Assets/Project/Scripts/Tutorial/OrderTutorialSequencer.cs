using UnityEngine;
using ZoomZoom.Orders;
using ZoomZoom.Orders.UI;

// Chains the whole tutorial together, step by step:
//   1. Car tricks (your existing CarFeatureVerifier), untouched.
//   2. Its shared choice panel. Continue switches the car beat off, hides the car checklist,
//      and starts beat 2.
//   3. "Follow the red arrow to the beacon." - shown at the top, the instant beat 2 starts.
//   4. Beat 2 (Arrive): drive to the Transform you place as B2.
//   5. "Well done, now go pick up ingredients." - shown the instant beat 2 finishes, while
//      beat 5 starts underneath it.
//   6. Beat 5 (Collect and cook) takes over, with the real game's arrows guiding the player
//      milk -> kitchen -> drop off, and a top message at each checkpoint.
//   7. The shared choice panel again, this time saying the whole tutorial is over.
//
// Messages can no longer silently fail. Every message goes through TutorialMessage.ShowAtTop
// if its Inspector slot is empty, which finds whatever TutorialMessage is already in the scene
// or builds a top banner itself. A missed Inspector field costs you nothing now.
//
// Arrows are the real game's DestinationMarker, not a tutorial copy of it: one arrow per order
// pointing at the nearest ingredient still needed, then the bay, then the customer, hidden
// while the food cooks. This script makes sure one exists and that the Guidance setting is not
// hiding it, so the tutorial scene behaves like the normal game without any extra setup.
//
// Scene setup:
//   1. Car beat: your existing CarFeatureVerifier, left ACTIVE from the start. Drag its
//      GameObject into carBeatObject below. Do NOT drag a Canvas or a message panel in here:
//      SetActive(false) takes every child with it.
//   2. ONE TutorialChoiceUI panel, used twice. Drag that component into choiceUI below.
//   3. Beat 2 (Arrive): a GameObject with TutorialDestination, hasTimeLimit OFF, its
//      destination set to the Transform you place for B2. Leave it SWITCHED OFF.
//   4. Beat 5 (Collect and cook): a GameObject with TutorialOrderVerifier, also SWITCHED OFF.
//   5. Message panels are optional. Wire them if you want your own, leave them empty if not.
public class OrderTutorialSequencer : MonoBehaviour
{
    [Header("Found automatically if left empty")]
    [Tooltip("Informational only. Auto spawn is switched off on EVERY OrderManager in the " +
             "scene in Awake, not just this one - see KillAllAutoSpawn below.")]
    public OrderManager orderManager;

    [Header("The shared choice panel (used by the car beat AND beat 5)")]
    public TutorialChoiceUI choiceUI;

    [Tooltip("Optional. Your CarFeatureVerifier's GameObject - switched off the moment Continue " +
             "is pressed. Never point this at a Canvas or a message panel: switching a " +
             "GameObject off switches all of its children off too.")]
    public GameObject carBeatObject;

    [Header("Beat 2: Arrive (leave switched off in the scene)")]
    public TutorialDestination arriveBeat;

    [Tooltip("Optional. Left empty, the message still shows - on whichever TutorialMessage is " +
             "in the scene, or on a banner built automatically.")]
    public TutorialMessage arriveStartMessage;
    [TextArea]
    [SerializeField] private string arriveStartText = "Follow the red arrow to the beacon.";

    [Tooltip("Optional, same fallback as above.")]
    public TutorialMessage arriveDoneMessage;
    [TextArea]
    [SerializeField] private string arriveDoneText = "Well done, now go pick up ingredients.";

    [Header("Beat 5: Collect and cook (leave switched off in the scene)")]
    public TutorialOrderVerifier collectBeat;

    [Header("Arrows")]
    [Tooltip("Makes sure the real game's DestinationMarker is in this scene and switched on, so " +
             "the player gets the same arrows they will use for the rest of the game: milk, " +
             "then the kitchen, then the drop off.")]
    [SerializeField] private bool ensureArrows = true;

    [Tooltip("If the saved Guidance setting is GPS only, arrows are hidden game-wide and the " +
             "tutorial would teach nothing. This switches it to Markers and GPS for the run.")]
    [SerializeField] private bool forceArrowsVisible = true;

    [Header("Diagnostics")]
    [Tooltip("Logs a warning, with a clickable stack trace, if any order spawns before beat 5 " +
             "asks for one.")]
    [SerializeField] private bool warnOnUnexpectedOrders = true;

    // True once the car beat's panel has been continued, so a second Continue (after beat 5
    // is delivered, which is the whole tutorial finishing) doesn't try to start beat 2 again.
    private bool _orderTutorialStarted;

    // True once beat 5 is running, after which an order spawning is entirely expected.
    private bool _collectBeatStarted;

    // Every OrderManager in the scene, not just the one this script's field points at.
    private OrderManager[] _managers = new OrderManager[0];

    private void Awake()
    {
        KillAllAutoSpawn();
        EnsureArrows();

        if (arriveBeat != null) arriveBeat.gameObject.SetActive(false);
        if (collectBeat != null) collectBeat.gameObject.SetActive(false);
    }

    /// <summary>Switches off the random spawn loop on EVERY OrderManager in the scene.
    ///
    /// FindAnyObjectByType returns whichever one Unity happens to hand back first, so if a
    /// second OrderManager exists anywhere (a child object, something inside a prefab, a
    /// leftover from duplicating the main scene), switching off only that one leaves the other
    /// spawning random orders with real beacons all through the tutorial.</summary>
    private void KillAllAutoSpawn()
    {
        _managers = FindObjectsByType<OrderManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int live = 0;
        string names = "";

        foreach (OrderManager m in _managers)
        {
            if (m == null) continue;
            m.AutoSpawnEnabled = false;

            if (m.isActiveAndEnabled)
            {
                live++;
                names += (names.Length > 0 ? ", " : "") + m.gameObject.name;
            }
        }

        if (orderManager == null && _managers.Length > 0) orderManager = _managers[0];

        if (_managers.Length == 0)
        {
            Debug.LogError("[OrderTutorialSequencer] No OrderManager in this scene at all. " +
                           "Beat 5 cannot spawn its order without one.", this);
            return;
        }

        Debug.Log($"[OrderTutorialSequencer] Auto spawn switched off on {_managers.Length} " +
                  $"OrderManager(s), {live} of them active: {names}", this);

        if (live > 1)
        {
            Debug.LogWarning(
                $"[OrderTutorialSequencer] {live} active OrderManagers are in this scene " +
                $"({names}). There should be exactly one. Every other script finds its manager " +
                "with FindAnyObjectByType, so with two of them in the scene they do not all " +
                "agree on which one they are talking to. Delete the extra one.", this);
        }
    }

    /// <summary>The arrows the player follows between the ingredient, the bay and the customer
    /// are the real game's DestinationMarker. This only makes sure one is present and visible,
    /// it does not re-implement any of it.</summary>
    private void EnsureArrows()
    {
        if (!ensureArrows) return;

        // DestinationMarker hides every arrow while Guidance is set to GPS only.
        if (forceArrowsVisible && !GameSettings.ShowArrows)
        {
            GameSettings.Guidance = GuidanceMode.Both;
            Debug.Log("[OrderTutorialSequencer] Guidance was set to GPS only, which hides all " +
                      "arrows. Switched to Markers and GPS so the tutorial can show them.", this);
        }

        var marker = FindAnyObjectByType<DestinationMarker>(FindObjectsInactive.Include);

        if (marker != null)
        {
            // Usually it lives on the OrderHUD object, which is easy to leave switched off in a
            // tutorial scene - and then there are no arrows anywhere.
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            marker.enabled = true;
            return;
        }

        var go = new GameObject("Destination Arrows (auto)");
        go.AddComponent<DestinationMarker>();
        Debug.Log("[OrderTutorialSequencer] No DestinationMarker in this scene, so one was " +
                  "added. The player now gets the same arrows as the normal game.", this);
    }

    private void OnEnable()
    {
        if (choiceUI != null) choiceUI.Continued += HandleChoiceContinued;
        if (arriveBeat != null) arriveBeat.Arrived += HandleArriveDone;

        foreach (OrderManager m in _managers)
            if (m != null) m.OrderSpawned += HandleAnyOrderSpawned;
    }

    private void OnDisable()
    {
        if (choiceUI != null) choiceUI.Continued -= HandleChoiceContinued;
        if (arriveBeat != null) arriveBeat.Arrived -= HandleArriveDone;

        foreach (OrderManager m in _managers)
            if (m != null) m.OrderSpawned -= HandleAnyOrderSpawned;
    }

    /// <summary>Fires for every order any manager creates. Before beat 5 starts, no order
    /// should exist, so one appearing means something else in the scene called SpawnOrder.
    /// Click the warning in the Console and the stack trace underneath names the script.</summary>
    private void HandleAnyOrderSpawned(Order order)
    {
        if (!warnOnUnexpectedOrders || _collectBeatStarted) return;

        string food = order != null ? order.FoodName : "unknown food";
        Debug.LogWarning(
            $"[OrderTutorialSequencer] An order for '{food}' spawned before beat 5 asked for " +
            "one. Auto spawn is already off on every OrderManager, so something else called " +
            "SpawnOrder directly. Click this line and read the stack trace below it - the " +
            "script that created this order is named in there.", this);
    }

    private void HandleChoiceContinued()
    {
        if (_orderTutorialStarted) return; // the second Continue is the whole thing finishing
        _orderTutorialStarted = true;

        if (carBeatObject != null) carBeatObject.SetActive(false);

        // The car beat's checklist has nothing left to say, and the order beats use messages at
        // the top of the screen instead.
        var prompt = FindAnyObjectByType<DrivePromptUI>(FindObjectsInactive.Include);
        if (prompt != null) prompt.HideAll();

        if (arriveBeat != null) arriveBeat.gameObject.SetActive(true);

        ShowMessage(arriveStartMessage, arriveStartText);
    }

    private void HandleArriveDone()
    {
        _collectBeatStarted = true;
        ShowMessage(arriveDoneMessage, arriveDoneText);
        if (collectBeat != null) collectBeat.gameObject.SetActive(true);
    }

    /// <summary>Uses the wired panel if there is one, and the scene's TutorialMessage (or a
    /// banner built on the spot) if there isn't. A message never just fails to appear.</summary>
    private static void ShowMessage(TutorialMessage target, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (target != null) target.Show(text);
        else TutorialMessage.ShowAtTop(text);
    }
}
