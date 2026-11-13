using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class TutorialFinishPanel : MonoBehaviour
{
    public GameObject panelRoot;
    
    public TextMeshProUGUI messageText;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "Medium";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [SerializeField] private bool pauseWhileOpen = true;

    // Set by Show just before it switches the panel on. If something else hides the panel
    // before this component's own Awake has run (in the tutorial scene, TutFinish also points
    // at EndPanel), Unity only runs that Awake when Show switches the panel on. Without this
    // check the panel would hide itself again at once and leave the game paused with nothing
    // on screen.
    private bool _showRequested;

    private void Awake()
    {
        if (panelRoot != null && !_showRequested) panelRoot.SetActive(false);
    }

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
        _showRequested = true;
        panelRoot.SetActive(true);

        if (pauseWhileOpen) Time.timeScale = 0f;
    }

    public void Hide()
    {
        _showRequested = false;
        if (pauseWhileOpen) Time.timeScale = 1f;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

 
    public void StartGame() => Load(gameSceneName);

 
    public void GoToMainMenu() => Load(mainMenuSceneName);

    private void Load(string sceneName)
    {
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
