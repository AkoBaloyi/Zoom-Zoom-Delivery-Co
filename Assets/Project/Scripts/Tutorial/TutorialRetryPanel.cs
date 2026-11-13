using System;
using UnityEngine;
using TMPro;

// Shown when the tutorial order runs out of patience or burns in the bay, so the player is told
// what went wrong and asked to try again rather than watching their order silently disappear and
// a new beacon appear somewhere else.
//
// WHY THIS EXISTS
// The tutorial order is a real order on the real OrderManager, so it ages and burns exactly like
// any other. TutorialOrderVerifier already refused to fail the player out over a slip: it quietly
// spawned a fresh copy. That is the right call, but doing it silently is worse than doing nothing,
// because the one thing a tutorial must never do is make the player feel they missed something
// they were not shown. This panel turns that silent restart into a stated one.
//
// Build the panel by hand in the Canvas, exactly like TutorialFinishPanel:
//   1. Make your panel in the Canvas with one button on it.
//   2. Leave it SWITCHED OFF in the scene.
//   3. Drag the panel GameObject onto panelRoot below, and its TMP text onto messageText.
//   4. Wire the button's OnClick to TutorialRetryPanel.Retry().
//   5. Drag this component onto TutorialOrderVerifier's Retry Panel field.
public class TutorialRetryPanel : MonoBehaviour
{
    [Header("Your panel (built by hand, starts switched off)")]
    public GameObject panelRoot;
    [Tooltip("Optional. Says what went wrong, set by TutorialOrderVerifier when it opens this.")]
    public TextMeshProUGUI messageText;

    [Tooltip("Freezes the game while the panel is up, the same way the other tutorial panels do.")]
    [SerializeField] private bool pauseWhileOpen = true;

    /// <summary>Fires when the player presses the button, after the panel is hidden and time is
    /// resumed. TutorialOrderVerifier listens for this and spawns a fresh order.</summary>
    public event Action RetryPressed;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>Called by TutorialOrderVerifier when the tutorial order is lost. Returns false if
    /// the panel could not actually be shown, so the caller can recover instead of leaving the
    /// player staring at a frozen game.</summary>
    public bool Show(string message)
    {
        if (panelRoot == null)
        {
            Debug.LogError("[TutorialRetryPanel] panelRoot is not assigned in the Inspector, so " +
                           "there is no panel to show. Drag your retry panel onto it.", this);
            return false;
        }

        if (messageText != null) messageText.text = message;
        panelRoot.SetActive(true);

        // Never freeze the game behind a panel that is not actually on screen.
        if (!TutorialPanels.VerifyVisible(panelRoot, this, "TutorialRetryPanel")) return false;

        if (pauseWhileOpen) Time.timeScale = 0f;
        return true;
    }

    /// <summary>Wire this to your Try Again button's OnClick.</summary>
    public void Retry()
    {
        Hide();
        RetryPressed?.Invoke();
    }

    public void Hide()
    {
        if (pauseWhileOpen) Time.timeScale = 1f;
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
