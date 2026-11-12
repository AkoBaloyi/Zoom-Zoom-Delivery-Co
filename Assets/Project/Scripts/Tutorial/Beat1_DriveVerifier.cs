using UnityEngine;

// Watches the car's actual physical movement (not just key presses) to
// confirm the player has genuinely accelerated, steered and braked, then
// completes beat one. Put this on an empty GameObject in the tutorial scene.
public class Beat1_DriveVerifier : MonoBehaviour
{
    public BeatManager beatManager;
    public DrivePromptUI promptUI;
    public Rigidbody carRigidbody; // [swap for your car's actual rigidbody or controller reference]

    public float minSpeedForAccelerate = 1.5f;
    public float minSteerAngleDelta = 5f;
    public float minBrakeDeceleration = 2f;

    private bool hasAccelerated = false;
    private bool hasSteered = false;
    private bool hasBraked = false;

    private float lastSpeed = 0f;
    private float lastHeading = 0f;

    private void Start()
    {
        lastHeading = carRigidbody.transform.eulerAngles.y;
    }

    private void Update()
    {
        if (beatManager.currentBeat != TutorialBeat.Drive) return;

        float speed = carRigidbody.linearVelocity.magnitude;
        float heading = carRigidbody.transform.eulerAngles.y;
        float headingDelta = Mathf.Abs(Mathf.DeltaAngle(lastHeading, heading));

        // Accelerate: car has actually built up real speed
        if (!hasAccelerated && speed >= minSpeedForAccelerate)
        {
            hasAccelerated = true;
            promptUI.ConfirmAccelerate();
        }

        // Steer: heading has changed meaningfully while moving
        if (hasAccelerated && !hasSteered && speed > 0.5f && headingDelta >= minSteerAngleDelta)
        {
            hasSteered = true;
            promptUI.ConfirmSteer();
        }

        // Brake: speed has dropped sharply frame to frame after having been fast
        float deceleration = (lastSpeed - speed) / Time.deltaTime;
        if (hasSteered && !hasBraked && lastSpeed > minSpeedForAccelerate && deceleration >= minBrakeDeceleration)
        {
            hasBraked = true;
            promptUI.ConfirmBrake();
        }

        lastSpeed = speed;
        lastHeading = heading;

        if (hasAccelerated && hasSteered && hasBraked)
        {
            beatManager.CompleteBeat(TutorialBeat.Drive);
        }
    }
}