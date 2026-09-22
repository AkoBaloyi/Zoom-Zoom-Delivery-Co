using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenuManager : MonoBehaviour
{
    public GameObject mainPanel;
    public GameObject settingsPanel;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
  public void PlayGame()
    {
        SceneManager.LoadScene(1);
    }

    public void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void Back()
    {
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
