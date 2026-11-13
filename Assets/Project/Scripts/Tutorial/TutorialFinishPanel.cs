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

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
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
        panelRoot.SetActive(true);

        if (pauseWhileOpen) Time.timeScale = 0f;
    }

    public void Hide()
    {
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
