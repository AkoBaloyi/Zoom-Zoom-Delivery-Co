using System.Collections;
using UnityEngine;
using ZoomZoom.Orders;

// Beat 5 (Collect and cook): one ingredient, one bay, one delivery, slowly, with nothing else
// competing for attention.
//
// Confirmed by reading ZoneDetection.cs and CargoSystem.cs: every step the player actually does
// - collecting the ingredient, dropping it at the bay, picking the cooked food back up, handing
// it over - already happens automatically off plain distance checks. There is no interact
// button anywhere in this game. So this script does not reinvent any of that; it spawns exactly
// one scripted order, switches off the normal random spawn loop so nothing else shows up to
// confuse the lesson, and puts a message at the top of the screen at each checkpoint.
//
// Guidance between the checkpoints is the real game's DestinationMarker: milk, then the kitchen,
// then the customer, with the arrow hidden while the food cooks because there is nothing to
// drive to. OrderTutorialSequencer makes sure one is in the scene. Nothing here duplicates it.
//
// This does NOT use DrivePromptUI. That checklist belongs to the car beat; the order beats read
// better as one line at a time across the top.
//
// Put this on an empty GameObject in the tutorial scene, SWITCHED OFF - the sequencer switches
// it on when beat 2 finishes. Needs an OrderManager in the scene (found automatically).
public class TutorialOrderVerifier : MonoBehaviour
{
    [Header("Wiring (found automatically if left empty)")]
    public OrderManager orderManager;
    [Tooltip("The end-of-tutorial panel, with your button through to the game scene. This is " +
             "deliberately NOT the car beat's TutorialChoiceUI - that one continues the " +
             "tutorial, this one ends it. Build it by hand and drag it in here.")]
    public TutorialFinishPanel finishPanel;

    [Header("The one order to spawn")]
    [Tooltip("Pick a FoodType with exactly one ingredient for this beat - \"one ingredient, one " +
             "bay, one delivery\" is the whole point.")]
    public FoodType tutorialFood;

    [Header("Messages (all optional - each falls back to a top banner if left empty)")]
    [Tooltip("Shown once, the moment the ingredient is collected.")]
    public TutorialMessage ingredientCollectedMessage;
    [TextArea]
    [SerializeField] private string ingredientCollectedText =
        "Well done, now drop it off at the kitchen.";

    [Tooltip("Shown once, the first moment the order starts cooking.")]
    public TutorialMessage cookingMessage;
    [TextArea]
    [SerializeField] private string cookingMessageText = "Wait for it to cook.";

    [Tooltip("Shown once, the moment the food finishes cooking and is waiting in the bay.")]
    public TutorialMessage readyMessage;
    [TextArea]
    [SerializeField] private string readyMessageText =
        "Pick up the finished item and drop it off.";

    [Tooltip("What the shared choice panel says once this order is delivered.")]
    [TextArea]
    [SerializeField] private string finishedMessage =
        "Nice work, that's the whole loop. You're ready to start your shift.";

    [Tooltip("Seconds between the delivery and the finish panel opening, so the player sees the " +
             "delivery land first. The payout popup lasts 1.8 s and the money change 2 s.")]
    [Min(0f)] [SerializeField] private float finishPanelDelay = 2.5f;

    private Order _order;
    private bool _finished;
    private bool _shownIngredientMessage;
    private bool _shownCookingMessage;
    private bool _shownReadyMessage;

    private void Awake()
    {
        if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
    }

    private void Start()
    {
        if (orderManager == null)
        {
            Debug.LogError("[TutorialOrderVerifier] No OrderManager found in the scene.", this);
            return;
        }

        // Only this one scripted order exists for the whole beat.
        orderManager.AutoSpawnEnabled = false;
        orderManager.OrderResolved += HandleOrderResolved;

        SpawnTutorialOrder();
    }

    private void OnDestroy()
    {
        if (orderManager != null)
        {
            orderManager.OrderResolved -= HandleOrderResolved;
            orderManager.AutoSpawnEnabled = true; // leave the next scene's real spawning alone
        }
        if (_order != null) _order.StateChanged -= HandleOrderStateChanged;
    }

    private void SpawnTutorialOrder()
    {
        if (_order != null) _order.StateChanged -= HandleOrderStateChanged;

        _order = orderManager.SpawnOrder(tutorialFood);
        if (_order == null)
        {
            Debug.LogError("[TutorialOrderVerifier] Could not spawn the tutorial order - check " +
                            "there's an Ingredient Source (or a pickup point), a Processing Bay " +
                            "and a drop off point in the scene.", this);
            return;
        }

        _shownIngredientMessage = false;
        _shownCookingMessage = false;
        _shownReadyMessage = false;
        _order.StateChanged += HandleOrderStateChanged;
    }

    private void HandleOrderStateChanged(Order order, OrderState previousState)
    {
        if (order != _order || _finished) return;

        // Collecting the ingredient doesn't change State (it stays Active until the bay), so
        // this is read from AllIngredientsCollected rather than a state transition.
        if (!_shownIngredientMessage && order.State == OrderState.Active && order.AllIngredientsCollected)
        {
            _shownIngredientMessage = true;
            ShowMessage(ingredientCollectedMessage, ingredientCollectedText);
        }

        if (!_shownCookingMessage && order.State == OrderState.Cooking)
        {
            _shownCookingMessage = true;
            ShowMessage(cookingMessage, cookingMessageText);
        }

        if (!_shownReadyMessage && order.State == OrderState.Ready)
        {
            _shownReadyMessage = true;
            ShowMessage(readyMessage, readyMessageText);
        }
    }

    private void HandleOrderResolved(Order order, float value)
    {
        if (order != _order || _finished) return;

        if (order.State == OrderState.Delivered)
        {
            _finished = true;
            if (finishPanel != null)
            {
                StartCoroutine(ShowFinishPanelAfterDelay());
            }
            else
            {
                Debug.LogError("[TutorialOrderVerifier] The order was delivered but Finish Panel " +
                               "is not assigned, so nothing can open. Drag your " +
                               "TutorialFinishPanel onto that field.", this);
            }
            return;
        }

        // Late (patience ran out) or Burned: this beat is meant to be pressure-free, so rather
        // than fail the player out of the tutorial over a slip, hand them a fresh copy of the
        // same order and let them finish the loop properly.
        Debug.Log($"[TutorialOrderVerifier] Tutorial order ended as {order.State} - spawning " +
                  "another one so the player can finish the beat.");
        SpawnTutorialOrder();
    }

    // The finish panel pauses the game, so opening it the instant the food is handed over
    // freezes the delivery before the player sees it. Game time, so a pause during the wait
    // holds the panel back too.
    private IEnumerator ShowFinishPanelAfterDelay()
    {
        if (finishPanelDelay > 0f) yield return new WaitForSeconds(finishPanelDelay);
        if (finishPanel != null) finishPanel.Show(finishedMessage);
    }

    /// <summary>Uses the wired panel if there is one, and the scene's TutorialMessage (or a
    /// banner built on the spot) if there isn't.</summary>
    private static void ShowMessage(TutorialMessage target, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (target != null) target.Show(text);
        else TutorialMessage.ShowAtTop(text);
    }
}
