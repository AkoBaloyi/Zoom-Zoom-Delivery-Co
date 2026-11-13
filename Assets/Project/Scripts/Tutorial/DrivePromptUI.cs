using System.Collections.Generic;
using UnityEngine;
using TMPro;

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
