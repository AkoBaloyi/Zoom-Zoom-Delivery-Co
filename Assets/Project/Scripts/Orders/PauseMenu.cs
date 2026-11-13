using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// Esc, P, or gamepad Start pauses the game: freezes time and shows whatever panel you build
    /// in the scene. This script does NOT build any UI itself, it only toggles it and runs the
    /// button logic. Build the panel by hand in the Canvas, leave it switched off in the scene,
    /// then drag it onto panelRoot below and wire your buttons' OnClick to the public methods:
    ///
    ///   On-screen Pause button -> Pause()          (or TogglePause() if the same button closes it)
    ///   Resume button          -> Resume()
    ///   Restart Shift          -> RestartShift()
    ///   Controls button        -> ToggleControls()  (needs a separate controls panel/object)
    ///   End Shift button       -> EndShiftEarly()   (goes straight to the results screen)
    ///   Quit to Menu button    -> QuitToMenu()
    ///
    /// Do NOT wire a button to SetPaused: it takes a bool, so the Inspector shows a checkbox that
    /// is easy to leave on the wrong setting, and a Pause button wired to it with the box unticked
    /// does nothing at all. Pause(), Resume() and TogglePause() exist so that cannot happen.
    ///
    /// Put this on an empty GameObject anywhere in the gameplay scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenu : MonoBehaviour
    {
        [Header("Your panel (built by hand in the scene, starts switched off)")]
        [Tooltip("The whole pause panel, dim background included. Switched on/off by this script.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("Optional. A separate panel/object for the controls list, toggled by ToggleControls().")]
        [SerializeField] private GameObject controlsPanel;

        [Tooltip("Optional. Your on-screen Pause button, hidden while the game is paused so it " +
                 "cannot sit on top of the pause panel. Leave empty if the panel covers it anyway.")]
        [SerializeField] private GameObject pauseButton;

        [Header("Scenes")]
        [Tooltip("Scene to load for Quit to Menu. Must match the scene's actual name.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        public bool IsPaused => _paused;

        private ShiftTimer _shiftTimer;
        private bool _paused;

        private void Awake()
        {
            _shiftTimer = FindAnyObjectByType<ShiftTimer>();

            if (panelRoot != null) panelRoot.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
        }

        private void Update()
        {
            bool pressed = Keyboard.current != null &&
                (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame);
            if (!pressed && Gamepad.current != null) pressed = Gamepad.current.startButton.wasPressedThisFrame;

            if (pressed) TogglePause();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (panelRoot != null) panelRoot.SetActive(paused);
            if (!paused && controlsPanel != null) controlsPanel.SetActive(false);

            // The on-screen Pause button has nothing to do while the panel is up, and leaving it
            // sitting over a dimmed panel reads as a bug.
            if (pauseButton != null) pauseButton.SetActive(!paused);
        }

        /// <summary>Wire this to your on-screen Pause button's OnClick. Safe to call when already
        /// paused: it simply stays paused.</summary>
        public void Pause() => SetPaused(true);

        /// <summary>Wire this instead if the same button should both open and close the panel.</summary>
        public void TogglePause() => SetPaused(!_paused);

        /// <summary>Wire this to the Resume button's OnClick.</summary>
        public void Resume() => SetPaused(false);

        /// <summary>Wire this to the Restart Shift button's OnClick.</summary>
        public void RestartShift()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Wire this to an End Shift button's OnClick. Ends the shift right now, exactly
        /// as if the clock had run out, so ShiftResults shows the real results screen.</summary>
        public void EndShiftEarly()
        {
            SetPaused(false);
            if (_shiftTimer != null) _shiftTimer.EndShiftEarly();
        }

        /// <summary>Wire this to the Quit to Menu button's OnClick.</summary>
        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            if (string.IsNullOrEmpty(mainMenuSceneName)) return;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        /// <summary>Wire this to the Controls button's OnClick.</summary>
        public void ToggleControls()
        {
            if (controlsPanel == null) return;
            controlsPanel.SetActive(!controlsPanel.activeSelf);
        }
    }
}