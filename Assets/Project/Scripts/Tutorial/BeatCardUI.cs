using UnityEngine;
using TMPro;

// The beat title card from Ako's UI sheet 05: shows a short title whenever a
// new beat starts, then hides itself. Wire BeatManager's onBeatStart event to
// ShowBeat in the inspector.
public class BeatCardUI : MonoBehaviour
{
    public GameObject cardRoot;
    public TMPro.TextMeshProUGUI titleText;

    public void ShowBeat(TutorialBeat beat)
    {
        titleText.text = beat.ToString();
        cardRoot.SetActive(true);
        CancelInvoke(nameof(Hide));
        Invoke(nameof(Hide), 1.5f);
    }

    private void Hide() => cardRoot.SetActive(false);
}
