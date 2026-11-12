using UnityEngine;

// Attach to a trigger collider placed at the point in the scene where a beat
// should be considered finished (a destination, a corner, a bay). Set
// requiredBeat to the beat this trigger closes out, and drag the scene's
// BeatManager into the manager field.
public class BeatTrigger : MonoBehaviour
{
    public TutorialBeat requiredBeat;
    public BeatManager manager;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        manager.CompleteBeat(requiredBeat);
    }
}
