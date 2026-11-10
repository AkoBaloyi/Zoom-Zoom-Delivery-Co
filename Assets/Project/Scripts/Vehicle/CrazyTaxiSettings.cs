using System;
using UnityEngine;

namespace ZoomZoom.Vehicle
{
    // Project-scale reconstruction values, not reverse-engineered Sega constants.
    [Serializable]
    public sealed class CrazyTaxiSettings
    {
        [Header("Pedal travel and steering input")]
        [Range(0f, 0.4f)] public float acceleratorDeadzone = 0.04f;
        [Range(0f, 0.4f)] public float brakeDeadzone = 0.04f;
        [Range(0f, 0.4f)] public float steeringDeadzone = 0.12f;
        public AnimationCurve acceleratorTravel = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve brakeTravel = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve steeringTravel = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Min(0f)] public float acceleratorRiseTime = 0.04f;
        [Min(0f)] public float acceleratorReleaseTime = 0.02f;
        [Min(0f)] public float brakeResponseTime = 0.02f;
        [Range(0f, 0.3f)] public float pedalFloorHysteresis = 0.1f;
        [Range(0.01f, 0.5f)] public float brakeTrickThreshold = 0.08f;
        [Header("Modern pedal direction assistance")]
        public bool brakeToReverse = true;
        [Min(0f)] public float reverseEngageSpeed = 0.35f;
        [Min(0f)] public float reverseEngageDelay = 0.18f;
        [Range(0.001f, 0.5f)] public float directionPedalThreshold = 0.04f;
        [Header("Arcade driving (metres / seconds)")]
        [Min(1f)] public float cruiseSpeed = 28f;
        [Min(1f)] public float reverseSpeed = 12f;
        [Min(1f)] public float acceleration = 24f;
        [Min(1f)] public float reverseAcceleration = 14f;
        [Min(1f)] public float braking = 28f;
        [Min(0f)] public float coasting = 2.5f;
        [Min(0f)] public float overspeedDeceleration = 8f;
        [Min(1f)] public float cornerGrip = 65f;
        [Min(0.1f)] public float steeringResponse = 18f;
        [Min(1f)] public float yawAcceleration = 20f;
        [Min(0.1f)] public float steeringSpeedForFullAuthority = 3f;
        [Min(0f)] public float chassisStability = 18f;
        [Min(0f)] public float antiRollStrength = 65f;
        [Min(0f)] public float antiRollDamping = 10f;
        [Min(0.1f)] public float normalAccelerationTaper = 6f;
        public bool useCustomAccelerationCurve = false;
        public AnimationCurve accelerationMultiplierBySpeed = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public bool useCustomSteeringCurve = false;
        public AnimationCurve steeringDegreesBySpeed = AnimationCurve.Linear(0f, 160f, 1f, 105f);
        public AnimationCurve gripMultiplierBySpeed = AnimationCurve.Linear(0f, 1f, 2f, 1f);
        [Min(1f)] public float lowSpeedTurnDegrees = 160f;
        [Min(1f)] public float highSpeedTurnDegrees = 105f;
        [Header("Modern automatic forward gears (D/R still selects direction)")]
        public bool automaticGears = true;
        [Tooltip("Upshift speeds as fractions of cruise speed. Last entry sets top-gear RPM range.")]
        public float[] forwardGearSpeedFractions = { 0.28f, 0.49f, 0.70f, 0.88f, 1.12f };
        [Tooltip("Actual drive acceleration multiplier per forward gear. Missing entries use 1.")]
        public float[] forwardGearThrust = { 0.78f, 0.68f, 0.60f, 0.54f, 0.50f };
        [Range(0.05f, 0.8f)] public float downshiftHysteresis = 0.22f;
        [Min(0f)] public float gearShiftTime = 0.16f;
        [Range(0f, 1f)] public float shiftThrustRetention = 0.38f;
        [Tooltip("Minimum loaded pull AFTER the shift has completed, not including shift time.")]
        [Min(0f)] public float gearMinimumHoldTime = 0.70f;
        [Range(0.05f, 0.9f)] public float shiftUnloadFraction = 0.35f;
        public bool progressiveShift = true;
        [Min(0f)] public float shiftFeedbackVolume = 0.18f;
        public AudioClip gearShiftClip;
        public bool shiftRumbleEnabled = true;
        [Range(0f, 1f)] public float shiftRumbleLow = 0.22f;
        [Range(0f, 1f)] public float shiftRumbleHigh = 0.12f;
        [Min(0.01f)] public float shiftRumbleDuration = 0.09f;
        [Min(100f)] public float engineIdleRpm = 1000f;
        [Tooltip("Road-coupled automatic gear RPM reference. Brake-held revs use Engine Free Rev Limit.")]
        [Min(1000f)] public float engineRedlineRpm = 6500f;
        [Min(1f)] public float engineRpmResponse = 16000f;
        [Min(0f)] public float engineLoadRpm = 550f;
        [Header("Brake-held free revving")]
        [Min(1000f)] public float engineFreeRevLimit = 9000f;
        [Min(1f)] public float engineFreeRevRiseRate = 12000f;
        [Min(1f)] public float engineFreeRevFallRate = 6500f;
        [Range(0.2f, 3f)] public float engineFreeRevPitch = 2.8f;
        [Min(1f)] public float engineLimiterFrequency = 12f;
        [Range(0f, 1f)] public float engineLimiterRetention = 0.25f;
        public bool engineAudio = true;
        public AudioClip engineLoop;
        public AudioClip roadLoop;
        [Range(0f, 1f)] public float engineVolume = 0.12f;
        [Range(0.1f, 2f)] public float engineIdlePitch = 0.55f;
        [Range(0.2f, 3f)] public float engineRedlinePitch = 2.2f;
        [Range(0f, 1f)] public float roadAudioVolume = 0.09f;
        [Header("Input rhythm")]
        [Range(0.1f, 0.6f)] public float inputWindow = 0.3f;
        [Range(0f, 0.05f)] public float minimumGap = 0.008f;
        [Range(0.5f, 1f)] public float pedalFloor = 0.9f;
        [Header("Crazy manoeuvres")]
        [Header("Direct technique controls")]
        public bool directTechniqueControls = true;
        [Min(0.7f)] public float directDashCooldown = 2f;
        [Range(0f, 8f)] public float dashFovAddition = 4f;
        [Range(0f, 1f)] public float dashCueVolume = 0.35f;
        public AudioClip dashCueClip;
        [Min(1f)] public float dashSpeed = 38f;
        [Min(1f)] public float dashAcceleration = 60f;
        [Min(0f)] public float dashMaximumEntrySpeed = 8f;
        [Min(0.1f)] public float dashDuration = 0.7f;
        [Min(0f)] public float limiterSpeedStep = 6f;
        [Min(1f)] public float limiterMaximumSpeed = 56f;
        [Min(1f)] public float backDashSpeed = 20f;
        [Min(1f)] public float stopDeceleration = 85f;
        [Min(1f)] public float driftMinimumSpeed = 5f;
        [Min(0.1f)] public float driftDuration = 1.2f;
        [Min(1f)] public float driftTurnDegrees = 150f;
        [Min(0f)] public float driftGrip = 5f;
        [Min(0f)] public float driftDrag = 1.8f;
        [Min(1f)] public float driftExitGrip = 45f;
        [Min(0f)] public float driftExitGripTime = 0.35f;
        [Range(0.05f, 1f)] public float trickSteerThreshold = 0.25f;
        [Range(0f, 1f)] public float driftReleasedSteer = 0.25f;
        [Min(0f)] public float dashChainGraceTime = 0.35f;
        [Header("Modern quick turnaround")]
        public bool quickSpinEnabled = true;
        [Min(45f)] public float quickSpinTurnDegrees = 360f;
        [Min(0.1f)] public float quickSpinYawAcceleration = 30f;
        [Min(0.1f)] public float quickSpinDuration = 0.9f;
        [Min(0f)] public float quickSpinCooldown = 0.65f;
        [Min(0f)] public float quickSpinTravelSpeed = 6f;
        [Range(0f, 1f)] public float quickSpinMomentumRetention = 0.90f;
        [Min(0f)] public float quickSpinRedirectAcceleration = 180f;
        [Min(0.1f)] public float quickSpinTargetGain = 7f;
        [Range(1f, 15f)] public float quickSpinCompletionAngle = 4f;
        [Header("Ground and recovery")]
        [Range(0f, 1f)] public float drivableUprightness = 0.5f;
        [Range(-1f, 0.5f)] public float recoveryUprightness = 0.25f;
        [Min(0f)] public float recoveryMaximumSpeed = 1.5f;
        [Min(0f)] public float automaticRecoveryDelay = 3f;
        [Min(0f)] public float recoveryTimeCost = 5f;
        [Min(0.1f)] public float recoveryLift = 0.8f;
        public Vector3 recoveryClearanceHalfSize = new Vector3(1f, 0.5f, 1.8f);
        [Tooltip("Allow deliberate driving techniques to cause hard-brake cargo damage. Collisions remain independently damaging.")]
        public bool techniquesCauseHardBrakeDamage = false;
        [Min(0f)] public float techniqueCargoGraceTime = 0.1f;
        [Header("Automatic chase camera")]
        [Min(1f)] public float cameraDistance = 5.6f;
        [Min(0.1f)] public float cameraHeight = 2.1f;
        [Min(0f)] public float cameraLookAhead = 7f;
        [Min(0f)] public float cameraAimHeight = 1.15f;
        [Min(0f)] public float cameraVelocityMinimumSpeed = 2f;
        [Range(0f, 180f)] public float cameraMaximumDriftLead = 80f;
        [Min(0.01f)] public float cameraHeadingTime = 0.18f;
        [Min(0.01f)] public float cameraFollowTime = 0.07f;
        [Range(30f, 100f)] public float cameraFov = 65f;
        public bool speedCameraCues = true;
        [Range(0f, 20f)] public float cameraSpeedFovAddition = 9f;
        [Min(0.01f)] public float cameraFovResponseTime = 0.25f;
        [Min(0f)] public float cameraSpeedDistanceAddition = 0.6f;
        [Min(0f)] public float cameraAccelerationPullback = 0.3f;
        [Min(0.01f)] public float cameraAccelerationResponseTime = 0.18f;
        [Range(0f, 0.8f)] public float cameraDriftVelocityBlend = 0.55f;
        [Min(0.1f)] public float cameraCollisionRadius = 0.35f;
        [Min(0.01f)] public float cameraWallReturnTime = 0.3f;
        [Min(0f)] public float cameraWallPadding = 0.05f;
        [Range(1, 8)] public int cameraOverlapPasses = 4;
        [Min(0.01f)] public float cameraNearClip = 0.1f;
        public bool cameraFollowReverse = false;
        [Min(0.1f)] public float cameraReverseSpeedThreshold = 1f;
        public bool useCameraFovCurve = false;
        public AnimationCurve cameraFovBySpeed = AnimationCurve.Linear(0f, 65f, 2f, 70f);

        public float MapPedal(float raw, float deadzone, AnimationCurve travel)
        {
            float value = Mathf.Clamp01((Mathf.Clamp01(raw) - deadzone) / Mathf.Max(0.001f, 1f - deadzone));
            return Mathf.Clamp01(travel != null && travel.length > 0 ? travel.Evaluate(value) : value);
        }

        public void Sanitize()
        {
            cruiseSpeed = Mathf.Max(0.1f, cruiseSpeed);
            reverseSpeed = Mathf.Max(0.1f, reverseSpeed);
            acceleratorDeadzone = Mathf.Clamp(acceleratorDeadzone, 0f, 0.95f);
            brakeDeadzone = Mathf.Clamp(brakeDeadzone, 0f, 0.95f);
            steeringDeadzone = Mathf.Clamp(steeringDeadzone, 0f, 0.95f);
            inputWindow = Mathf.Max(0.01f, inputWindow);
            minimumGap = Mathf.Clamp(minimumGap, 0f, inputWindow);
            pedalFloor = Mathf.Clamp(pedalFloor, 0.1f, 1f);
            pedalFloorHysteresis = Mathf.Clamp(pedalFloorHysteresis, 0f, pedalFloor - 0.01f);
            dashDuration = Mathf.Max(0.01f, dashDuration);
            driftDuration = Mathf.Max(0.01f, driftDuration);
            quickSpinDuration = Mathf.Max(0.01f, quickSpinDuration);
            cameraNearClip = Mathf.Max(0.01f, cameraNearClip);
            cameraCollisionRadius = Mathf.Max(cameraNearClip * 1.2f, cameraCollisionRadius);
            cameraHeadingTime = Mathf.Max(0.001f, cameraHeadingTime);
            cameraFollowTime = Mathf.Max(0.001f, cameraFollowTime);
            cameraWallReturnTime = Mathf.Max(0.001f, cameraWallReturnTime);
            if (acceleratorTravel == null || acceleratorTravel.length == 0) acceleratorTravel = AnimationCurve.Linear(0, 0, 1, 1);
            if (brakeTravel == null || brakeTravel.length == 0) brakeTravel = AnimationCurve.Linear(0, 0, 1, 1);
            if (steeringTravel == null || steeringTravel.length == 0) steeringTravel = AnimationCurve.Linear(0, 0, 1, 1);
            if (accelerationMultiplierBySpeed == null || accelerationMultiplierBySpeed.length == 0) accelerationMultiplierBySpeed = AnimationCurve.Linear(0, 1, 1, 0);
            if (steeringDegreesBySpeed == null || steeringDegreesBySpeed.length == 0) steeringDegreesBySpeed = AnimationCurve.Linear(0, 160, 1, 105);
            if (gripMultiplierBySpeed == null || gripMultiplierBySpeed.length == 0) gripMultiplierBySpeed = AnimationCurve.Linear(0, 1, 2, 1);
            if (cameraFovBySpeed == null || cameraFovBySpeed.length == 0) cameraFovBySpeed = AnimationCurve.Linear(0, 65, 2, 70);
        }
    }
}
