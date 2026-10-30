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
                if (label.gameObject.scene != scene || !(label.text.Contains("Brake/Reverse") || label.text.Contains("DRIVE / REVERSE TRICKS") || label.text.Contains("DIRECT DRIVING CONTROLS"))) continue;
                label.text = "DIRECT DRIVING CONTROLS\n" +
                    "HOLD W / RT (R2)    ACCELERATE\nHOLD S / LT (L2)    BRAKE, THEN REVERSE\nHOLD A/D / LEFT STICK    STEER\n\n" +
                    "DASH: HOLD W/RT + PRESS SHIFT/RB (R1)\n" +
                    "DRIFT: STEER + PRESS CTRL / X (SQUARE)\n" +
                    "STOP: HOLD S/LT + PRESS SHIFT/RB (R1)\n" +
                    "QUICK TURN: STEER + PRESS SPACE/LB (L1)\n\n" +
                    "Dash works up to cruising speed or out of a drift. Ready again after 2s.\n" +
                    "Drift/Stop need forward speed. Tricks require ground contact.\n" +
                    "HOLD C / R3    LOOK BEHIND\nPRESS BACKSPACE / VIEW    RESET IF STUCK (-5s)\n" +
                    "PRESS F8 / D-PAD UP    TOGGLE MOTION EFFECTS";
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 36f;
            }
        }
    }
}
