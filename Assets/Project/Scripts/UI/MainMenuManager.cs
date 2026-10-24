using UnityEngine;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// Switches between the start screen's panels. Wire each button's OnClick to one method:
    ///   Play button          -> OpenDifficultyPanel
    ///   Controls button      -> OpenControlsPanel
    ///   Exit button          -> ExitGame
    ///   Back button (any)    -> BackToMainMenu
    /// Only one panel is visible at a time, and the main menu panel is shown when the scene starts.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("The panel with Play, Controls and Exit.")]
        [SerializeField] private GameObject mainMenuPanel;
        [Tooltip("The panel with Easy, Medium and Hard.")]
        [SerializeField] private GameObject difficultyPanel;
        [Tooltip("The panel that shows the controls, with a Back button.")]
        [SerializeField] private GameObject controlsPanel;

        private void Awake()
        {
            // Coming back from a paused or finished shift: time must run and the mouse must work.
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ShowOnly(mainMenuPanel);
        }

        public void OpenDifficultyPanel() => ShowOnly(difficultyPanel);

        public void OpenControlsPanel() => ShowOnly(controlsPanel);

        public void BackToMainMenu() => ShowOnly(mainMenuPanel);

        public void ExitGame()
        {
            Debug.Log("[MainMenuManager] Exit pressed.");

#if UNITY_EDITOR
            // Application.Quit does nothing in the editor, so stop play mode instead.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowOnly(GameObject panel)
        {
            if (panel == null)
            {
                Debug.LogWarning("[MainMenuManager] A panel field is empty. Drag the panel into " +
                                 "this component in the Inspector.", this);
                return;
            }

            if (mainMenuPanel != null) mainMenuPanel.SetActive(panel == mainMenuPanel);
            if (difficultyPanel != null) difficultyPanel.SetActive(panel == difficultyPanel);
            if (controlsPanel != null) controlsPanel.SetActive(panel == controlsPanel);
        }
    }
}
