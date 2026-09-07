using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// Third person chase camera. Sits behind and slightly above the car and aims at a point some
    /// way in FRONT of it.
    ///
    /// THE JOB OF THIS CAMERA
    /// The car can stop from top speed in about 14 metres. If the camera does not show at least
    /// that much road, the player is being asked to react to something they physically could not
    /// see, and every crash feels unfair no matter how good the handling is. So look-ahead is not
    /// a decoration, it is a fairness requirement, and it is measured (press F5 in the lab).
    ///
    /// WHY THIS IS NOT PARENTED TO THE CAR
    /// Parenting copies the car's rotation exactly. The car rolls in corners and pitches over
    /// bumps, so a parented camera rolls and pitches with it and the horizon lurches about. Here
    /// the camera works out where it WANTS to be from the car's state, then eases towards it. The
    /// car's roll and pitch never reach the camera.
    ///
    /// EVERY VALUE IS LIVE
    /// Nothing is cached. The tuning asset is read every frame, so the four dials, distance,
    /// height, follow smoothing and look-ahead, can be dragged while driving and the change is
    /// immediate. Because they live on a ScriptableObject the change is also KEPT when play mode
    /// stops, which is the only sane way to find these numbers.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class ChaseCamera : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The car to follow.")]
        [SerializeField] private VehicleController target;

        [Tooltip("Where the tuning numbers come from. Leave empty to use the car's own tuning asset, " +
                 "which is normally what you want so a profile swap changes the camera too.")]
        [SerializeField] private VehicleTuning tuningOverride;

        [Tooltip("Optional. Used for the manual look-around stick. Not required.")]
        [SerializeField] private VehicleInput input;

        private Camera _camera;

        // Smoothing state.
        private Vector3 _position;
        private Vector3 _positionVelocity;
        private Quaternion _rotation = Quaternion.identity;
        private Vector3 _smoothedHeading = Vector3.forward;

        // Manual look-around.
        private float _yawOffset;
        private float _pitchOffset;
        private float _timeSinceSwivel;

        /// <summary>The Camera this script drives. The measurement script needs it for the road-ahead reading.</summary>
        public Camera Camera => _camera;

        /// <summary>The car being followed.</summary>
        public VehicleController Target => target;

        private VehicleTuning Tuning
        {
            get
            {
                if (tuningOverride != null) return tuningOverride;
                return target != null ? target.Tuning : null;
            }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            if (target == null)
            {
                Debug.LogError(
                    "[ChaseCamera] No target car assigned. Drag the Car object onto the Target field.",
                    this);
            }
        }

        private void OnEnable()
        {
            SnapToTarget();
        }

        /// <summary>
        /// Jump straight to where the camera should be, with no easing. Used at startup and at the
        /// start of every measurement, so a reading is never taken while the camera is still
        /// sliding in from somewhere else.
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null || Tuning == null) return;

            _smoothedHeading = ComputeHeading();
            _positionVelocity = Vector3.zero;
            _yawOffset = 0f;
            _pitchOffset = 0f;

            _position = ComputeDesiredPosition(_smoothedHeading);
            _rotation = ComputeDesiredRotation(_position, _smoothedHeading);

            transform.SetPositionAndRotation(_position, _rotation);
            ApplyLens();
        }

        /// <summary>
        /// LateUpdate, not Update: the car's interpolated transform for this frame is final by now,
        /// so the camera never chases a position that is one frame stale.
        /// </summary>
        private void LateUpdate()
        {
            if (target == null || Tuning == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            ReadSwivelInput(dt);

            // 1. Which way is "forwards" for the camera this frame.
            Vector3 desiredHeading = ComputeHeading();
            _smoothedHeading = SmoothDirection(_smoothedHeading, desiredHeading,
                Tuning.cameraRotationSmoothTime, dt);

            // 2. Where the camera wants to be, then ease towards it.
            Vector3 desiredPosition = ComputeDesiredPosition(_smoothedHeading);
            _position = Vector3.SmoothDamp(_position, desiredPosition, ref _positionVelocity,
                Tuning.cameraFollowSmoothTime, Mathf.Infinity, dt);

            // 3. Aim at the look-ahead point.
            Quaternion desiredRotation = ComputeDesiredRotation(_position, _smoothedHeading);
            _rotation = SmoothRotation(_rotation, desiredRotation,
                Tuning.cameraRotationSmoothTime, dt);

            transform.SetPositionAndRotation(_position, _rotation);
            ApplyLens();
        }

        // ==================================================================
        // WHERE AND WHAT TO LOOK AT
        // ==================================================================

        /// <summary>
        /// The direction the camera looks along.
        ///
        /// Straight down the car's nose is the steady option, but during a slide the car is pointed
        /// one way and travelling another, and the player needs to see where they are GOING. Mixing
        /// a little of the velocity direction in solves that. Too much and the camera swings about
        /// every time the car twitches, so this is deliberately a small blend by default.
        /// </summary>
        private Vector3 ComputeHeading()
        {
            Vector3 nose = Flatten(target.GroundForward);
            if (nose.sqrMagnitude < 0.0001f) nose = Vector3.forward;

            Vector3 velocity = Flatten(target.Body != null ? target.Body.linearVelocity : Vector3.zero);

            // Below walking pace the velocity direction is noise, so ignore it.
            if (velocity.magnitude < 1.5f || Tuning.cameraVelocityInfluence <= 0f)
                return ApplySwivel(nose);

            Vector3 blended = Vector3.Slerp(nose, velocity.normalized,
                Tuning.cameraVelocityInfluence);

            return ApplySwivel(blended.sqrMagnitude > 0.0001f ? blended.normalized : nose);
        }

        private Vector3 ComputeDesiredPosition(Vector3 heading)
        {
            float speed01 = Tuning.topSpeed > 0.01f
                ? Mathf.Clamp01(target.Speed / Tuning.topSpeed)
                : 0f;

            // Pulling back and lifting as speed rises shows more road exactly when the stopping
            // distance is longest. It also reads as speed without touching the field of view.
            float distance = Tuning.cameraDistance + Tuning.cameraExtraDistanceAtTopSpeed * speed01;
            float height = Tuning.cameraHeight + Tuning.cameraExtraHeightAtTopSpeed * speed01;

            // Height uses WORLD up, not the car's up. The car rolls in corners; if the camera
            // followed that roll the horizon would tip and the shot becomes unreadable. There is no
            // wall or ceiling driving in this game, so world up is the right simplification.
            return target.transform.position - heading * distance + Vector3.up * height;
        }

        private Quaternion ComputeDesiredRotation(Vector3 cameraPosition, Vector3 heading)
        {
            Vector3 lookAtPoint = target.transform.position
                                  + heading * Tuning.cameraLookAhead
                                  + Vector3.up * Tuning.cameraLookAtHeight;

            Vector3 toTarget = lookAtPoint - cameraPosition;
            if (toTarget.sqrMagnitude < 0.0001f) toTarget = heading;

            Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);

            // Extra downward tilt on top of the aim, so more of the road surface is on screen
            // without having to push the look-ahead point unnaturally far out.
            Quaternion tilt = Quaternion.Euler(Tuning.cameraExtraPitch + _pitchOffset, 0f, 0f);

            return look * tilt;
        }

        private void ApplyLens()
        {
            if (_camera == null) return;
            _camera.fieldOfView = Tuning.cameraFieldOfView;
        }

        // ==================================================================
        // MANUAL LOOK AROUND
        // ==================================================================

        private void ReadSwivelInput(float dt)
        {
            Vector2 look = input != null ? input.CameraLook : Vector2.zero;

            if (look.sqrMagnitude > 0.0001f)
            {
                _yawOffset += look.x * Tuning.cameraSwivelSpeed * dt;
                _pitchOffset = Mathf.Clamp(
                    _pitchOffset - look.y * Tuning.cameraSwivelSpeed * dt, -35f, 55f);
                _timeSinceSwivel = 0f;
                return;
            }

            _timeSinceSwivel += dt;

            // Drift back behind the car once the player lets go, so they are never left driving
            // sideways because they forgot they had moved the camera.
            if (Tuning.cameraSwivelRecentreDelay <= 0f) return;
            if (_timeSinceSwivel < Tuning.cameraSwivelRecentreDelay) return;

            float recentre = 1f - Mathf.Exp(-3f * dt);
            _yawOffset = Mathf.Lerp(_yawOffset, 0f, recentre);
            _pitchOffset = Mathf.Lerp(_pitchOffset, 0f, recentre);
        }

        private Vector3 ApplySwivel(Vector3 heading)
        {
            if (Mathf.Abs(_yawOffset) < 0.01f) return heading;
            return Quaternion.AngleAxis(_yawOffset, Vector3.up) * heading;
        }

        // ==================================================================
        // HELPERS
        // ==================================================================

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>
        /// Frame rate independent easing.
        ///
        /// 1 - e^(-dt/smoothTime) covers the same fraction of the remaining gap in the same amount
        /// of TIME whatever the frame rate. A plain Lerp(a, b, 0.1f) does not: it moves twice as
        /// fast at 120 fps as at 60, so the camera would feel different on a different machine and
        /// none of the tuning would transfer. smoothTime is the time constant, so after that many
        /// seconds the camera has closed about 63% of the gap.
        /// </summary>
        private static float SmoothingFactor(float smoothTime, float dt)
        {
            if (smoothTime <= 0.0001f) return 1f;
            return 1f - Mathf.Exp(-dt / smoothTime);
        }

        private static Vector3 SmoothDirection(Vector3 current, Vector3 target, float smoothTime, float dt)
        {
            Vector3 result = Vector3.Slerp(current, target, SmoothingFactor(smoothTime, dt));
            return result.sqrMagnitude > 0.0001f ? result.normalized : target;
        }

        private static Quaternion SmoothRotation(Quaternion current, Quaternion target, float smoothTime, float dt)
        {
            return Quaternion.Slerp(current, target, SmoothingFactor(smoothTime, dt));
        }

        // ==================================================================
        // EDITOR VIEW OF THE FAIRNESS RULE
        // ==================================================================

        private void OnDrawGizmos()
        {
            DrawSightlineGizmos();
        }

        /// <summary>
        /// Draws the look-ahead point against the car's stopping distance, so the two can be
        /// compared by eye in the scene view without running a measurement.
        /// </summary>
        private void DrawSightlineGizmos()
        {
            if (target == null || Tuning == null) return;

            Vector3 carPos = target.transform.position;
            Vector3 heading = Application.isPlaying && _smoothedHeading.sqrMagnitude > 0.0001f
                ? _smoothedHeading
                : Flatten(target.transform.forward).normalized;

            if (heading.sqrMagnitude < 0.0001f) return;

            // Where the camera is aiming.
            Vector3 lookAt = carPos + heading * Tuning.cameraLookAhead
                                    + Vector3.up * Tuning.cameraLookAtHeight;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(carPos + Vector3.up * 0.5f, lookAt);
            Gizmos.DrawWireSphere(lookAt, 0.5f);

            // How far the car needs to see: stopping distance from top speed.
            float stopping = Tuning.PredictedStoppingDistanceFromTopSpeed();
            if (float.IsInfinity(stopping)) return;

            Vector3 stopPoint = carPos + heading * stopping;
            Gizmos.color = Tuning.cameraLookAhead >= stopping ? Color.green : Color.red;
            Gizmos.DrawLine(stopPoint + Vector3.up * 0.1f, stopPoint + Vector3.up * 2.5f);
        }
    }
}
