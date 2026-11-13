using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Shows the player exactly what is still left to try on the car. CarFeatureVerifier calls
// ShowChecklist every time something changes, so this is always an accurate "what's left" list
// rather than a fixed three-step script that goes quiet once it's past step one.
//
// Needs just two things wired in the Inspector: the panel GameObject (promptRoot) and a
// TextMeshProUGUI to write the checklist into (promptText). No icons, no per-step sprites.
public class DrivePromptUI : MonoBehaviour
{
    public GameObject promptRoot;
    public TextMeshProUGUI promptText;

    private void Awake()
    {
        HideAll();
    }

    /// <summary>remaining is the list of things still to do, in plain words with the keyboard key
    /// and the controller button (eg "steer: A/D | left stick").
    /// An empty list hides the panel, since there is nothing left to tell the player.</summary>
    public void ShowChecklist(List<string> remaining)
    {
        if (!CheckWiring()) return;

        if (remaining == null || remaining.Count == 0)
        {
            HideAll();
            return;
        }

        promptRoot.SetActive(true);
        promptText.text = "Still to try (keyboard | controller):\n" + string.Join("\n", remaining);
    }

    public void HideAll()
    {
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    private bool CheckWiring()
    {
        if (promptRoot != null && promptText != null) return true;

        if (promptRoot == null) Debug.LogError("[DrivePromptUI] promptRoot is not assigned in the Inspector.", this);
        if (promptText == null) Debug.LogError("[DrivePromptUI] promptText is not assigned in the Inspector.", this);
        return false;
    }
}
