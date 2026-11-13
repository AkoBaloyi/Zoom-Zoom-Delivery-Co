using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// The panel at the very end of beat 5, once the tutorial order has been delivered. Separate
// from TutorialChoiceUI on purpose: that one is the car beat's "continue the tutorial" panel,
// this one is "the tutorial is over, go play the game".
//
// Build the panel by hand in the Canvas, exactly like you did for TutorialChoiceUI:
//   1. Make your panel in the Canvas with however many buttons you want on it.
//   2. Leave it SWITCHED OFF in the scene.
//   3. Drag the panel GameObject onto panelRoot below, and its TMP text onto messageText.
//   4. Wire your buttons' OnClick:
//        Play button      -> TutorialFinishPanel.StartGame()
//        Main Menu button -> TutorialFinishPanel.GoToMainMenu()
//   5. Drag this component onto TutorialOrderVerifier's Finish Panel field.
public class TutorialFinishPanel : MonoBehaviour
{
    [Header("Your panel (built by hand, starts switched off)")]
    public GameObject panelRoot;
    [Tooltip("Optional. A line like \"Nice work, that's the whole loop.\"")]
    public TextMeshProUGUI messageText;

    [Header("Scenes")]
    [Tooltip("Exact scene name, no .unity. It must also be in File > Build Profiles > Scene List.")]
    [SerializeField] private string gameSceneName = "Medium";
    [Tooltip("Exact scene name, no .unity. Leave empty if your panel has no Main Menu button.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Tooltip("Freezes the game while the panel is up, the same way TutorialChoiceUI does.")]
    [SerializeField] private bool pauseWhileOpen = true;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>Called by TutorialOrderVerifier once the tutorial order is delivered.</summary>
    public void Show(string message)
    {
        if (panelRoot == null)
        {
            Debug.LogError("[TutorialFinishPanel] panelRoot is not assigned in the Inspector, so " +
                           "there is no panel to show. Drag your finished-tutorial panel onto it.",
                           this);
            return;
        }

        if (messageText != null) messageText.text = message;
        panelRoot.SetActive(true);

        // Never freeze the game behind a panel that is not actually on screen: if a parent is
        // switched off, this names it in the Console and leaves time running.
        if (!TutorialPanels.VerifyVisible(panelRoot, this, "TutorialFinishPanel")) return;

        if (pauseWhileOpen) Time.timeScale = 0f;
    }

    public void Hide()
    {
        if (pauseWhileOpen) Time.timeScale = 1f;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>Wire this to your Play button's OnClick.</summary>
    public void StartGame() => Load(gameSceneName);

    /// <summary>Wire this to your Main Menu button's OnClick.</summary>
    public void GoToMainMenu() => Load(mainMenuSceneName);

    private void Load(string sceneName)
    {
        // Always put time back before leaving, or the next scene starts frozen.
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[TutorialFinishPanel] No scene name set for that button.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[TutorialFinishPanel] Cannot load scene \"{sceneName}\". Check the " +
                           "name on this component and that the scene is in " +
                           "File > Build Profiles > Scene List.", this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
