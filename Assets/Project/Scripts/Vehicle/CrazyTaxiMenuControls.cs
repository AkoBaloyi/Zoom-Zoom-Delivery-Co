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
                label.text = "W / UP    ACCELERATE\nS / DOWN    BRAKE, THEN REVERSE AT REST\nA / D    STEER\n" +
                    "F / R    DRIVE / REVERSE TRICKS (B / A)\nC    LOOK BEHIND\n" +
                    "STEER + SPACE / LB    QUICK TURN\nBACKSPACE    RESET IF STUCK (-5s)\n\nDASH    F + W together\nDRIFT    R then F while steering\nSTOP    S + R together\nAUTO RESET    Stuck/upside down for 3s (-5s)";
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 36f;
            }
        }
    }
}
