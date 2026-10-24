using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZoomZoom.Vehicle
{
    /// <summary>Migrate the existing menu's control card without rewriting a shared scene.</summary>
    internal static class CrazyTaxiMenuControls
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialise()
        {
            SceneManager.sceneLoaded -= UpdateCard;
            SceneManager.sceneLoaded += UpdateCard;
            UpdateCard(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        private static void UpdateCard(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainMenu") return;
            foreach (TMP_Text label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                if (label.gameObject.scene != scene || !label.text.Contains("Brake/Reverse")) continue;
                label.text = "W / UP    ACCELERATE\nS / DOWN    BRAKE\nA / D    STEER\n" +
                    "F    DRIVE  (gamepad B)\nR    REVERSE  (gamepad A)\nC    LOOK BEHIND\n" +
                    "SPACE / LB    QUICK TURN\nBACKSPACE    RIGHT CAR\n\nDASH    Release pedals, F then W\nDRIFT    R then F while steering";
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 36f;
            }
        }
    }
}
