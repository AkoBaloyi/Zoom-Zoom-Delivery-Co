using UnityEngine;
using UnityEngine.Events;

public enum TutorialBeat
{
    Drive,
    Arrive,
    Clock,
    GearAndTrick,
    CollectAndCook,
    Overlap,
    Risk,
    Done
}

public class BeatManager : MonoBehaviour
{
    public TutorialBeat currentBeat = TutorialBeat.Drive;

    [System.Serializable]
    public class BeatEvent : UnityEvent<TutorialBeat> { }

    public BeatEvent onBeatStart;
    public BeatEvent onBeatComplete;

    private void Start()
    {
        // Fire the first beat's start event so its prompts show immediately.
        onBeatStart.Invoke(currentBeat);
    }

    public void CompleteBeat(TutorialBeat beat)
    {
        if (beat != currentBeat) return; // ignore out of order triggers

        onBeatComplete.Invoke(currentBeat);
        currentBeat++;
        onBeatStart.Invoke(currentBeat);

        if (currentBeat == TutorialBeat.Done)
        {
            // [hook: unlock main scene / load city scene]
        }
    }
}
