using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonsUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject pausePanel;

    [Header("Scenes")]
    [Tooltip("Exact name of the main menu scene file, without .unity. It must be in File > Build Profiles > Scene List.")]
    public string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        // Ensure the pause panel is closed when starting
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    // Assign to Pause Button On Click ()
    public void PauseGame()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
            Time.timeScale = 0f; // Freeze game
        }
    }

    // Assign to Close / Resume Button On Click ()
    public void ResumeGame()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
            Time.timeScale = 1f; // Resume game
        }
    }

    // Assign to Main Menu Button On Click ()
    // Loads the main menu scene named in the Inspector.
    public void GoToMainMenu()
    {
        Time.timeScale = 1f; // Always reset time scale before scene change

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError($"[ButtonsUI] Cannot load scene \"{mainMenuSceneName}\". Check the name " +
                           "on this component and that the scene is in File > Build Profiles > Scene List.", this);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Assign to Restart Button On Click ()
    // Reloads the scene that is currently running.
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Assign to Exit Button On Click ()
    public void ExitGame()
    {
        Debug.Log("[ButtonsUI] Exiting game.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Quit does nothing in the editor
#else
        Application.Quit();
#endif
    }
}