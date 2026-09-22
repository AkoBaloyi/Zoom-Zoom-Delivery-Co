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
    public void GoToMainMenu()
    {
        Time.timeScale = 1f; // Always reset time scale before scene change
        SceneManager.LoadScene("MainMenu"); // Make sure "MainMenu" matches your scene name in Build Settings
    }

    // Assign to Exit Button On Click ()
    public void ExitGame()
    {
        Debug.Log("Exiting Game...");
        Application.Quit();
    }
}