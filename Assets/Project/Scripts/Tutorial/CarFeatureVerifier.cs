using System.Collections.Generic;
using UnityEngine;
using ZoomZoom.Vehicle;

// Finishes the drive tutorial once the player has genuinely tried every part of the car:
// accelerate, steer, brake, and every trick the recognizer knows about (dash, back dash, drift,
// back drift, stop, limiter cut, quick spin). No fixed finish line, no trigger collider; it's
// done when the player has actually done everything, however long that takes.
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
            if (choiceUI != null) choiceUI.Show();
        }
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
        if (!_hasAccelerated) remaining.Add("accelerate (W)");
        if (!_hasSteered) remaining.Add("steer (A/D)");
        if (!_hasBraked) remaining.Add("brake (S)");
        foreach (TaxiTrick trick in _requiredTricks)
            if (!_tricksDone.Contains(trick)) remaining.Add(TrickLabel(trick));

        promptUI.ShowChecklist(remaining);
    }

    // The combos below match Ako's UI pack for the four tricks it documents explicitly
    // (Shift+gas dash, Shift+brake stop, Ctrl+steer drift, Space+steer quick turn). Back dash,
    // back drift and limiter cut aren't pinned down in anything I've read from this project, so
    // those three just name the trick rather than guess a combo. Check them against your actual
    // input script and fix the strings below if they're wrong.
    private static string TrickLabel(TaxiTrick trick)
    {
        switch (trick)
        {
            case TaxiTrick.Dash: return "dash (Shift + gas)";
            case TaxiTrick.Stop: return "crazy stop (Shift + brake)";
            case TaxiTrick.Drift: return "drift (Ctrl + steer)";
            case TaxiTrick.QuickSpin: return "quick turn (Space + steer)";
            case TaxiTrick.BackDash: return "back dash";
            case TaxiTrick.BackDrift: return "back drift";
            case TaxiTrick.LimiterCut: return "limiter cut";
            default: return trick.ToString();
        }
    }
}
