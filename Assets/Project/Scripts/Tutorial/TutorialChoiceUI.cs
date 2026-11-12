using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Shown once CarFeatureVerifier decides the player has genuinely tried everything the car can
// do. Two buttons: Continue (move on to the next tutorial step) and Main Menu. Build the panel
// by hand in the Canvas, leave it switched off in the scene, drag it onto panelRoot, and wire
// your two buttons' OnClick to Continue() and GoToMainMenu() below.
public class TutorialChoiceUI : MonoBehaviour
{
    public GameObject panelRoot;
    [Tooltip("Optional. A line like \"You've tried everything the car can do.\"")]
    public TextMeshProUGUI messageText;

    [Header("Scenes")]
    [Tooltip("Scene to load for Main Menu. Must match the scene's actual name.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Show()
    {
        if (panelRoot == null)
        {
            Debug.LogError("[TutorialChoiceUI] panelRoot is not assigned in the Inspector.", this);
            return;
        }

        if (messageText != null) messageText.text = "You've tried everything the car can do.";
        panelRoot.SetActive(true);

        // Pause so the choice isn't made under a still-moving car.
        Time.timeScale = 0f;
    }

    /// <summary>Wire this to the Continue button's OnClick.</summary>
    public void Continue()
    {
        Time.timeScale = 1f;
        if (panelRoot != null) panelRoot.SetActive(false);

        // [hook: start the next tutorial beat, or SceneManager.LoadScene the next scene]
    }

    /// <summary>Wire this to the Main Menu button's OnClick.</summary>
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        if (string.IsNullOrEmpty(mainMenuSceneName)) return;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
