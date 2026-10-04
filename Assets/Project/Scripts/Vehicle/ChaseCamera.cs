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
    ///
    /// WHY THE CAMERA PULLS IN AT WALLS
    /// Everything above was built and measured on an open floor. The district is not open: streets
    /// are about 20 m kerb to kerb and the camera sits up to 6.5 m behind the car, so the first
    /// time the car turns a corner the camera swings wide into the building on the inside of the
    /// turn and the player is looking at the back of a wall. So every frame a sphere is swept from
    /// a point just above the car out to where the camera wants to be, and if it touches anything
    /// solid the camera is placed on the near side of it instead.
    ///
    /// Two asymmetries are deliberate. Pulling in is instant and easing back out is slow, because
    /// one frame of seeing through a wall reads as a bug while a slow return reads as nothing at
    /// all. And the clamp is applied AFTER the follow smoothing, never to the smoothing target,
    /// so a wall cannot lag through the easing and the follow state is untouched when the wall
    /// ends. The car's own colliders are ignored the same way the wheel raycasts ignore them.
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

        [Tooltip("What counts as a wall for the camera. Defaults to everything except Ignore Raycast. " +
                 "The car is excluded by identity, not by layer, so it can share a layer with the " +
                 "buildings, which it does.")]
        [SerializeField] private LayerMask collisionLayers = Physics.DefaultRaycastLayers;

        private Camera _camera;

        // Smoothing state.
        private Vector3 _position;
        private Vector3 _positionVelocity;
        private Quaternion _rotation = Quaternion.identity;
        private Vector3 _smoothedHeading = Vector3.forward;

        // Wall avoidance. Fraction of the pivot-to-camera distance actually in use: 1 is clear,
        // smaller means a wall has pulled the camera in. Kept separately from _position so the
        // follow smoothing never learns about walls.
        private float _wallPullIn = 1f;
        private Vector3 _renderPosition;
        private readonly RaycastHit[] _wallHits = new RaycastHit[8];

        // Manual look-around.
        private float _yawOffset;
        private float _pitchOffset;
        private float _timeSinceSwivel;

        // Degrees currently added to the field of view by boost. Eased, never snapped.
        private float _fovKick;

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
            _wallPullIn = 1f;
            _fovKick = 0f;

            _position = ComputeDesiredPosition(_smoothedHeading);
            // dt of zero: any wall in the way pulls in immediately, nothing eases.
            _renderPosition = KeepOutOfWalls(_position, 0f);
            _rotation = ComputeDesiredRotation(_renderPosition, _smoothedHeading);

            transform.SetPositionAndRotation(_renderPosition, _rotation);
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
            UpdateFovKick(dt);

            // 1. Which way is "forwards" for the camera this frame.
            Vector3 desiredHeading = ComputeHeading();
            _smoothedHeading = SmoothDirection(_smoothedHeading, desiredHeading,
                Tuning.cameraRotationSmoothTime, dt);

            // 2. Where the camera wants to be, then ease towards it.
            Vector3 desiredPosition = ComputeDesiredPosition(_smoothedHeading);
            _position = Vector3.SmoothDamp(_position, desiredPosition, ref _positionVelocity,
                Tuning.cameraFollowSmoothTime, Mathf.Infinity, dt);

            // 3. If a wall is between the car and that point, stop short of it. Applied to the
            //    smoothed result, not the target, so the wall never lags through the easing.
            _renderPosition = KeepOutOfWalls(_position, dt);

            // 4. Aim at the look-ahead point from where the camera actually is.
            Quaternion desiredRotation = ComputeDesiredRotation(_renderPosition, _smoothedHeading);
            _rotation = SmoothRotation(_rotation, desiredRotation,
                Tuning.cameraRotationSmoothTime, dt);

            transform.SetPositionAndRotation(_renderPosition, _rotation);
            ApplyLens();
        }

        // ==================================================================
        // WALL AVOIDANCE
        // ==================================================================

        /// <summary>
        /// Sweeps a sphere from just above the car to <paramref name="wanted"/> and returns the
        /// nearest point along that line the camera can sit without a wall between it and the car.
        ///
        /// The sweep starts at the look-at height rather than the car's centre so it clears the
        /// car's own roof, and it ends where the camera wants to be, so the check covers exactly
        /// the line of sight the player needs. A hit distance is where the sphere's CENTRE stopped,
        /// which already leaves one radius of clearance from the wall, and the radius is bigger
        /// than the near clip plane, so the wall cannot cut into the frame.
        /// </summary>
        private Vector3 KeepOutOfWalls(Vector3 wanted, float dt)
        {
            float radius = Tuning.cameraCollisionRadius;
            if (radius <= 0f)
            {
                _wallPullIn = 1f;
                return wanted;
            }

            Vector3 pivot = target.transform.position + Vector3.up * Tuning.cameraLookAtHeight;
            Vector3 offset = wanted - pivot;
            float fullDistance = offset.magnitude;
            if (fullDistance < 0.001f) return wanted;
            Vector3 direction = offset / fullDistance;

            float clear = 1f;
            int count = Physics.SphereCastNonAlloc(pivot, radius, direction, _wallHits,
                fullDistance, collisionLayers, QueryTriggerInteraction.Ignore);

            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _wallHits[i];
                if (IsPartOfCar(hit.collider)) continue;
                if (hit.distance < nearest) nearest = hit.distance;
            }

            if (!float.IsPositiveInfinity(nearest))
                clear = Mathf.Clamp01(nearest / fullDistance);

            // In: instant. Out: eased. See the class comment for why they differ.
            if (clear < _wallPullIn)
                _wallPullIn = clear;
            else
                _wallPullIn = Mathf.Lerp(_wallPullIn, clear,
                    SmoothingFactor(Tuning.cameraCollisionRecoverTime, dt));

            return pivot + direction * (fullDistance * _wallPullIn);
        }

        /// <summary>
        /// The sweep starts inside the car, so without this the camera would treat the car's roof
        /// as a wall and pull in to nothing. Checked by identity rather than layer because the car
        /// and the buildings share the Default layer.
        /// </summary>
        private bool IsPartOfCar(Collider c)
        {
            if (c == null) return true;
            if (target.Body != null && c.attachedRigidbody == target.Body) return true;
            return c.transform.IsChildOf(target.transform);
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

            // Second stage. speed01 saturates at topSpeed, so without this the camera was frozen
            // across the whole boost range and the one place the player could feel the second
            // speed stage, the camera did nothing. This ramp starts where the first one stops.
            distance += Tuning.cameraBoostExtraDistance * BoostStage01();

            // Height uses WORLD up, not the car's up. The car rolls in corners; if the camera
            // followed that roll the horizon would tip and the shot becomes unreadable. There is no
            // wall or ceiling driving in this game, so world up is the right simplification.
            return target.transform.position - heading * distance + Vector3.up * height;
        }

        private Quaternion ComputeDesiredRotation(Vector3 cameraPosition, Vector3 heading)
        {
            // Look-ahead grows across the boost range so the fairness rule holds at the boost
            // ceiling too, not only at throttle top speed. See cameraBoostExtraLookAhead.
            float lookAhead = Tuning.cameraLookAhead + Tuning.cameraBoostExtraLookAhead * BoostStage01();

            Vector3 lookAtPoint = target.transform.position
                                  + heading * lookAhead
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
            _camera.fieldOfView = Tuning.cameraFieldOfView + _fovKick;
        }

        /// <summary>
        /// 0 at or below throttle-only top speed, 1 at the boost ceiling. The handling curves scale
        /// against the full range; the camera deliberately does not, so the throttle range keeps
        /// the exact framing that was measured and the boost range gets its own ramp on top.
        /// </summary>
        private float BoostStage01()
        {
            float top = Tuning.topSpeed;
            float ceiling = Tuning.BoostOrTopSpeed();
            if (ceiling - top < 0.01f) return 0f;
            return Mathf.Clamp01((target.Speed - top) / (ceiling - top));
        }

        /// <summary>
        /// FOV widens while boost is being spent and narrows again when it is not. Tied to the
        /// button rather than to speed: the player gets the kick the instant they press, which is
        /// the feedback that says the press did something, and it leaves the instant they let go,
        /// so releasing is felt as well. Speed-tied FOV would arrive late and linger.
        /// </summary>
        private void UpdateFovKick(float dt)
        {
            float wanted = target.IsBoosting ? Tuning.cameraBoostFovKick : 0f;
            _fovKick = Mathf.Lerp(_fovKick, wanted, SmoothingFactor(Tuning.cameraBoostFovSmoothTime, dt));
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

            // How far the car needs to see. Two posts: stopping distance from throttle-only top
            // speed, and from the boost ceiling. The look-ahead has to beat the SECOND one, or a
            // boosting player is being asked to react to a corner the camera never showed them.
            // The first post used to be the only one and it passed, which hid the second failing.
            float stopping = Tuning.PredictedStoppingDistanceFromTopSpeed();
            float stoppingBoosted = Tuning.PredictedStoppingDistanceFromHighestSpeed();
            if (float.IsInfinity(stopping)) return;

            Vector3 stopPoint = carPos + heading * stopping;
            Gizmos.color = Tuning.cameraLookAhead >= stopping ? Color.green : Color.red;
            Gizmos.DrawLine(stopPoint + Vector3.up * 0.1f, stopPoint + Vector3.up * 2.5f);

            if (stoppingBoosted > stopping + 0.01f)
            {
                // Judged against the look-ahead the camera will have AT the boost ceiling, since
                // that is the look-ahead a player at that speed is actually given.
                float lookAheadAtCeiling = Tuning.cameraLookAhead + Tuning.cameraBoostExtraLookAhead;
                Vector3 boostStopPoint = carPos + heading * stoppingBoosted;
                Gizmos.color = lookAheadAtCeiling >= stoppingBoosted ? Color.green : Color.red;
                Gizmos.DrawLine(boostStopPoint + Vector3.up * 0.1f, boostStopPoint + Vector3.up * 3.5f);
            }

            // Wall avoidance, only meaningful while running. Yellow while a wall is holding the
            // camera in, with the sweep sphere drawn where the camera actually is.
            if (!Application.isPlaying || Tuning.cameraCollisionRadius <= 0f) return;

            Vector3 pivot = carPos + Vector3.up * Tuning.cameraLookAtHeight;
            bool heldIn = _wallPullIn < 0.999f;
            Gizmos.color = heldIn ? Color.yellow : new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawLine(pivot, _renderPosition);
            Gizmos.DrawWireSphere(_renderPosition, Tuning.cameraCollisionRadius);
            if (heldIn) Gizmos.DrawLine(_renderPosition, _position);
        }
    }
}
