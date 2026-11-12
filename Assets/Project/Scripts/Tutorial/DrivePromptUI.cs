using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Handles beat one's prompts: an icon plus a short label while the player
// hasn't yet done the action, and a brief confirmation line once they have,
// in the same style as the reference screenshots (icon-led prompt, short
// positive confirmation, then it gets out of the way).
public class DrivePromptUI : MonoBehaviour
{
    public GameObject promptRoot;
    public Image iconImage;
    public TextMeshProUGUI promptText;

    public GameObject confirmRoot;
    public TextMeshProUGUI confirmText;

    public Sprite accelerateIcon; // [assign your actual key/button sprite]
    public Sprite steerIcon;
    public Sprite brakeIcon;

    public void ShowAccelerate()
    {
        SetPrompt(accelerateIcon, "Accelerate");
    }

    public void ConfirmAccelerate()
    {
        ShowConfirm("Nice, you're moving");
        SetPrompt(steerIcon, "Steer");
    }

    public void ConfirmSteer()
    {
        ShowConfirm("Good turn");
        SetPrompt(brakeIcon, "Brake");
    }

    public void ConfirmBrake()
    {
        ShowConfirm("Good stop");
        HideAll();
    }

    private void SetPrompt(Sprite icon, string label)
    {
        promptRoot.SetActive(true);
        iconImage.sprite = icon;
        promptText.text = label;
    }

    private void ShowConfirm(string message)
    {
        confirmRoot.SetActive(true);
        confirmText.text = message;
        CancelInvoke(nameof(HideConfirm));
        Invoke(nameof(HideConfirm), 1f);
    }

    private void HideConfirm() => confirmRoot.SetActive(false);

    public void HideAll()
    {
        promptRoot.SetActive(false);
        confirmRoot.SetActive(false);
    }
}
