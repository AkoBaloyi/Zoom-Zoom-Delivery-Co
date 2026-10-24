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
        [Header("Arcade driving (metres / seconds)")]
        [Min(1f)] public float cruiseSpeed = 28f;
        [Min(1f)] public float reverseSpeed = 12f;
        [Min(1f)] public float acceleration = 18f;
        [Min(1f)] public float reverseAcceleration = 14f;
        [Min(1f)] public float braking = 28f;
        [Min(0f)] public float coasting = 2.5f;
        [Min(0f)] public float overspeedDeceleration = 8f;
        [Min(1f)] public float cornerGrip = 38f;
        [Min(0.1f)] public float steeringResponse = 14f;
        [Min(1f)] public float yawAcceleration = 12f;
        [Min(0.1f)] public float steeringSpeedForFullAuthority = 3f;
        [Min(0f)] public float chassisStability = 18f;
        [Min(0.1f)] public float normalAccelerationTaper = 6f;
        public bool useCustomAccelerationCurve = false;
        public AnimationCurve accelerationMultiplierBySpeed = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public bool useCustomSteeringCurve = false;
        public AnimationCurve steeringDegreesBySpeed = AnimationCurve.Linear(0f, 125f, 1f, 58f);
        public AnimationCurve gripMultiplierBySpeed = AnimationCurve.Linear(0f, 1f, 2f, 1f);
        [Min(1f)] public float lowSpeedTurnDegrees = 125f;
        [Min(1f)] public float highSpeedTurnDegrees = 58f;
        [Header("Input rhythm")]
        [Range(0.1f, 0.6f)] public float inputWindow = 0.3f;
        [Range(0f, 0.05f)] public float minimumGap = 0.008f;
        [Range(0.5f, 1f)] public float pedalFloor = 0.9f;
        [Header("Crazy manoeuvres")]
        [Min(1f)] public float dashSpeed = 38f;
        [Min(1f)] public float dashAcceleration = 60f;
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
        [Range(0f, 1f)] public float quickSpinMomentumRetention = 0.65f;
        [Min(0f)] public float quickSpinRedirectAcceleration = 65f;
        [Min(0.1f)] public float quickSpinTargetGain = 7f;
        [Range(1f, 15f)] public float quickSpinCompletionAngle = 4f;
        [Header("Ground and recovery")]
        [Range(0f, 1f)] public float drivableUprightness = 0.5f;
        [Range(-1f, 0.5f)] public float recoveryUprightness = 0.25f;
        [Min(0f)] public float recoveryMaximumSpeed = 1.5f;
        [Min(0f)] public float automaticRecoveryDelay = 2.5f;
        [Min(0.1f)] public float recoveryLift = 0.8f;
        public Vector3 recoveryClearanceHalfSize = new Vector3(1f, 0.5f, 1.8f);
        [Tooltip("Allow deliberate driving techniques to cause hard-brake cargo damage. Collisions remain independently damaging.")]
        public bool techniquesCauseHardBrakeDamage = false;
        [Min(0f)] public float techniqueCargoGraceTime = 0.1f;
        [Header("Automatic chase camera")]
        [Min(1f)] public float cameraDistance = 6.5f;
        [Min(0.1f)] public float cameraHeight = 3.1f;
        [Min(0f)] public float cameraLookAhead = 5f;
        [Min(0f)] public float cameraAimHeight = 1.15f;
        [Min(0f)] public float cameraVelocityMinimumSpeed = 2f;
        [Range(0f, 180f)] public float cameraMaximumDriftLead = 80f;
        [Min(0.01f)] public float cameraHeadingTime = 0.18f;
        [Min(0.01f)] public float cameraFollowTime = 0.07f;
        [Range(30f, 100f)] public float cameraFov = 65f;
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
            if (steeringDegreesBySpeed == null || steeringDegreesBySpeed.length == 0) steeringDegreesBySpeed = AnimationCurve.Linear(0, 125, 1, 58);
            if (gripMultiplierBySpeed == null || gripMultiplierBySpeed.length == 0) gripMultiplierBySpeed = AnimationCurve.Linear(0, 1, 2, 1);
            if (cameraFovBySpeed == null || cameraFovBySpeed.length == 0) cameraFovBySpeed = AnimationCurve.Linear(0, 65, 2, 70);
        }
    }
}
