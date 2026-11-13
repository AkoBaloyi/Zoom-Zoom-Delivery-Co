using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZoomZoom.Vehicle;

// Finishes the drive tutorial once the player has genuinely tried every part of the car:
// accelerate, steer, brake, and every trick the car's current controls can perform. With the
// direct technique buttons (the shipped setting) that is dash, stop, drift and quick spin; back
// dash, back drift and limiter cut have no button then, so asking for them meant the tutorial
// could never finish. No fixed finish line, no trigger collider; it's done when the player has
// actually done everything, however long that takes.
//
// Tricks are read from VehicleController.TrickPerformed, the real event the car fires every
// time a trick lands (see BeginTaxiTrick in VehicleController.Taxi.cs), so this never has to
// guess at key combos or re-detect something the car already tells you about directly. The
// required list is built from the TaxiTrick enum itself, so a new trick added to the recognizer
// is automatically required here too, nothing to update by hand.
//
// Put this on an empty GameObject in the tutorial scene.
public class CarFeatureVerifier : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("The car's VehicleController component.")]
    public VehicleController vehicle;
    public DrivePromptUI promptUI;
    public TutorialChoiceUI choiceUI;

    [Header("Basic driving thresholds")]
    public float minForwardSpeed = 1.5f;
    public float minSteerAmount = 0.3f;
    public float minBrakeInput = 0.2f;

    [Header("Finish")]
    [Tooltip("Seconds between the last checklist item and the choice panel opening. The panel " +
             "pauses the game, and a trick is ticked off the moment it starts, so without this " +
             "wait the last trick freezes before the player sees it. The longest trick (drift) " +
             "lasts 1.2 s.")]
    [Min(0f)] public float choicePanelDelay = 2f;

    private bool _hasAccelerated;
    private bool _hasSteered;
    private bool _hasBraked;
    private readonly HashSet<TaxiTrick> _tricksDone = new HashSet<TaxiTrick>();
    private readonly List<TaxiTrick> _requiredTricks = new List<TaxiTrick>();
    private bool _finished;

    private void Awake()
    {
        foreach (TaxiTrick trick in System.Enum.GetValues(typeof(TaxiTrick)))
        {
            if (trick == TaxiTrick.None) continue;
            if (vehicle != null && !vehicle.CanPerform(trick)) continue; // no button for it
            _requiredTricks.Add(trick);
        }
    }

    private void OnEnable()
    {
        if (vehicle != null) vehicle.TrickPerformed += HandleTrickPerformed;
    }

    private void OnDisable()
    {
        if (vehicle != null) vehicle.TrickPerformed -= HandleTrickPerformed;
    }

    private void Start()
    {
        UpdatePrompt(); // show the full checklist immediately, don't wait for the first input
    }

    private void HandleTrickPerformed(TaxiTrick trick)
    {
        if (_finished || trick == TaxiTrick.None) return;
        if (_tricksDone.Add(trick)) UpdatePrompt();
    }

    private void Update()
    {
        if (_finished) return;

        if (vehicle == null)
        {
            Debug.LogError("[CarFeatureVerifier] No VehicleController assigned.", this);
            return;
        }

        bool changed = false;

        if (!_hasAccelerated && vehicle.ForwardSpeed >= minForwardSpeed)
        {
            _hasAccelerated = true;
            changed = true;
        }

        if (!_hasSteered && Mathf.Abs(vehicle.SteerAmount) >= minSteerAmount)
        {
            _hasSteered = true;
            changed = true;
        }

        if (!_hasBraked && vehicle.Drive.brake >= minBrakeInput)
        {
            _hasBraked = true;
            changed = true;
        }

        if (changed) UpdatePrompt();

        if (IsEverythingDone())
        {
            _finished = true;
            if (promptUI != null) promptUI.HideAll();
            StartCoroutine(ShowChoiceAfterDelay());
        }
    }

    // Game time, not real time: if the player pauses during the wait, the panel waits too
    // instead of opening behind the pause menu.
    private IEnumerator ShowChoiceAfterDelay()
    {
        if (choicePanelDelay > 0f) yield return new WaitForSeconds(choicePanelDelay);
        if (choiceUI != null) choiceUI.Show();
    }

    private bool IsEverythingDone()
    {
        if (!_hasAccelerated || !_hasSteered || !_hasBraked) return false;
        foreach (TaxiTrick trick in _requiredTricks)
            if (!_tricksDone.Contains(trick)) return false;
        return true;
    }

    private void UpdatePrompt()
    {
        if (promptUI == null) return;

        var remaining = new List<string>();
        if (!_hasAccelerated) remaining.Add("accelerate: W | RT (R2)");
        if (!_hasSteered) remaining.Add("steer: A/D | left stick");
        if (!_hasBraked) remaining.Add("brake: S | LT (L2)");
        foreach (TaxiTrick trick in _requiredTricks)
            if (!_tricksDone.Contains(trick)) remaining.Add(TrickLabel(trick));

        promptUI.ShowChecklist(remaining);
    }

    // Each line is "keyboard | controller", with the Xbox button first and the PlayStation one in
    // brackets. The keys are the car's bindings in Vehicle/VehicleControls.inputactions and match
    // the main menu's controls card, so change all three together if a binding moves. Back dash,
    // back drift and limiter cut only exist when the direct technique buttons are switched off
    // (they come from gear-tap gestures), so they never appear in the shipped tutorial and just
    // name the trick.
    private static string TrickLabel(TaxiTrick trick)
    {
        switch (trick)
        {
            case TaxiTrick.Dash: return "dash: Shift + W | RB (R1) + RT (R2)";
            case TaxiTrick.Stop: return "crazy stop: Shift + S | RB (R1) + LT (L2)";
            case TaxiTrick.Drift: return "drift: Ctrl + A/D | X (Square) + left stick";
            case TaxiTrick.QuickSpin: return "quick turn: Space + A/D | LB (L1) + left stick";
            case TaxiTrick.BackDash: return "back dash";
            case TaxiTrick.BackDrift: return "back drift";
            case TaxiTrick.LimiterCut: return "limiter cut";
            default: return trick.ToString();
        }
    }
}
