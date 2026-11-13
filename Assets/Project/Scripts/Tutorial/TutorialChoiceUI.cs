using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Shown once CarFeatureVerifier decides the player has genuinely tried everything the car can
// do. Two buttons: Continue (move on to the next tutorial step) and Main Menu. Build the panel
// by hand in the Canvas, leave it switched off in the scene, drag it onto panelRoot, and wire
// your two buttons' OnClick to Continue() and GoToMainMenu() below.
public class TutorialChoiceUI : MonoBehaviour
{
    [Header("Your panel (built by hand, starts switched off)")]
    public GameObject panelRoot;
    [Tooltip("Optional. A line like \"You've tried everything the car can do.\"")]
    public TextMeshProUGUI messageText;

    [Header("Scenes")]
    [Tooltip("Scene to load for Main Menu. Must match the scene's actual name.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    /// <summary>Fires every time Continue is pressed, after the panel is hidden and time is
    /// resumed. This same panel gets reused for more than one beat finishing (the car beat,
    /// then later the whole order tutorial), so whatever should happen next is decided by
    /// whoever is listening, not hardcoded here.</summary>
    public event System.Action Continued;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>message is what this beat wants the panel to say, so the same panel can read
    /// "You've tried everything the car can do." the first time and "Nice work, you've
    /// delivered your first order." the second, without needing a separate component for
    /// each.</summary>
    public void Show(string message = "You've tried everything the car can do.")
    {
        if (panelRoot == null)
        {
            Debug.LogError("[TutorialChoiceUI] panelRoot is not assigned in the Inspector.", this);
            return;
        }

        if (messageText != null) messageText.text = message;
        panelRoot.SetActive(true);

        // Pause so the choice isn't made under a still-moving car.
        Time.timeScale = 0f;
    }

    /// <summary>Wire this to the Continue button's OnClick.</summary>
    public void Continue()
    {
        Time.timeScale = 1f;
        if (panelRoot != null) panelRoot.SetActive(false);
        Continued?.Invoke();
    }

    /// <summary>Wire this to the Main Menu button's OnClick.</summary>
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        if (string.IsNullOrEmpty(mainMenuSceneName)) return;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
