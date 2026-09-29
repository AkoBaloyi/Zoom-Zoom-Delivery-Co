using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonsUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject pausePanel;

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

    // Assign to Main Menu / Restart Button On Click ()
    //
    // Reloads the scene that is running rather than a scene called "MainMenu". No scene by that
    // name exists in the project or in Build Settings, so the Restart button was throwing a
    // "Scene couldn't be loaded" error in every build. The method keeps its name because the
    // button's On Click entry in the scene references it by name, and renaming it would silently
    // unhook the button. Once a real main menu scene exists, point this at it.
    public void GoToMainMenu()
    {
        Time.timeScale = 1f; // Always reset time scale before scene change
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Assign to Exit Button On Click ()
    public void ExitGame()
    {
        Debug.Log("Exiting Game...");
        Application.Quit();
    }
}