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
    ///
    /// WHY THE CAMERA SWINGS ROUND WHEN THE CAR REVERSES
    /// Everything above assumes the car is going forwards. Reversing used to aim the shot at a point
    /// up to 34 m in FRONT of a car travelling backwards at up to 8 m/s, so the player was reversing
    /// blind down the one axis they could not see, which is why recovering from a mistake felt worse
    /// than the mistake. So sustained backward motion swings the view round to the nose and carries
    /// the look-ahead point behind the tail. It is a timed, eased, hysteretic swing rather than a
    /// sign test, because forward speed crosses zero constantly and a camera that whips round every
    /// time the player brakes would be worse than the problem it fixes.
    ///
    /// WHY MOUSE AND STICK ARE SCALED DIFFERENTLY
    /// They arrive merged into one Vector2, but a mouse delta is a displacement and a stick reading
    /// is a rate. Only one of them may be multiplied by frame time. Scaling both by it, which is
    /// what this file used to do, made the mouse sensitivity depend on the frame rate.
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

        [Header("Reverse view")]
        [Tooltip("Forward speed (m/s, negative) at and below which the camera starts swinging round " +
                 "to show where the car is reversing. -1.5 matches the velocity gate ComputeHeading " +
                 "already uses, so the two agree about when backwards motion is real rather than noise.")]
        [SerializeField] private float reverseViewEnterSpeed = -1.5f;

        [Tooltip("Forward speed at and above which the camera starts coming back. The gap between " +
                 "this and the enter speed is the dead zone: inside it nothing changes, which is what " +
                 "stops the camera whipping round every time the player brakes to a standstill. " +
                 "-0.5 matches the controller's own reverse gate in ApplyDriveAndBrake.")]
        [SerializeField] private float reverseViewExitSpeed = -0.5f;

        [Tooltip("Seconds the car must stay below the enter speed before the swing begins. A brief " +
                 "backward roll on a slope, or overrunning the brake, is ignored.")]
        [SerializeField] private float reverseViewHoldTime = 0.25f;

        [Tooltip("Seconds for the full 180 degree swing, eased in and out. Never a snap. The existing " +
                 "precedent in this file is cameraSwivelRecentreDelay at 1.2 s for letting go of the " +
                 "mouse; a reverse swing has to be quicker than that or the player reverses blind " +
                 "while they wait for it.")]
        [SerializeField] private float reverseViewTransitionTime = 0.45f;

        [Tooltip("Seconds above the exit speed before the camera starts coming back, so shuffling " +
                 "back and forth in a three point turn does not swing the camera twice per shuffle.")]
        [SerializeField] private float reverseViewReturnDelay = 0.35f;

        [Header("Mouse look")]
        [Tooltip("Degrees of camera yaw per pixel of mouse movement, at any frame rate. Mouse delta " +
                 "is a displacement, not a rate, so it is deliberately NOT multiplied by " +
                 "Time.deltaTime; the right stick still is, because a stick IS a rate. 0.05 is the " +
                 "top of the 0.03-0.05 band this kind of auxiliary look lands in, and it is also " +
                 "exactly what the old code gave at 144 fps, so the fastest machine feels no change " +
                 "and the 60 fps case stops being twice as fast as intended.")]
        [SerializeField] private float mouseLookDegreesPerPixel = 0.05f;

        [Tooltip("Mouse movement smaller than this is ignored, in pixels. The old gate was 0.0001 on " +
                 "the squared processed vector, i.e. 0.167 px, which a resting hand clears six times " +
                 "over - and any movement also restarts the auto-recentre delay, so a resting hand " +
                 "used to hold the camera permanently off centre. Above about 2 px this starts to " +
                 "feel sticky.")]
        [SerializeField] private float mouseLookDeadzonePixels = 1f;

        // The ScaleVector2(x=0.06,y=0.06) processor on the <Mouse>/delta binding, in
        // VehicleControls.inputactions and duplicated in VehicleInput.BuildFallbackControls. One
        // pixel of mouse movement therefore arrives here as 0.06, and dividing by this gets the
        // pixels back so the sensitivity dial above can be stated in the unit a player understands.
        private const float MouseBindingDeltaScale = 0.06f;

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

        // Reverse view. _reverseTarget is where the swing is headed (0 behind the car, 1 round at
        // the nose), _reverseBlend is where it has actually got to, and the two timers are the
        // hold periods that stop a momentary speed sign change from starting or ending a swing.
        private float _reverseBlend;
        private float _reverseTarget;
        private float _reverseHold;
        private float _forwardHold;

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
        ///
        /// The reverse swing is reset here as well, and it has to be: VehicleMeasurement.ResetCar
        /// calls this before every reading, so without the reset a reading taken after a reversing
        /// run would start with the camera half swung round and the road-ahead number would be
        /// measured down the wrong axis.
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
            _reverseBlend = 0f;
            _reverseTarget = 0f;
            _reverseHold = 0f;
            _forwardHold = 0f;

            Vector3 viewHeading = ApplyReverseView(_smoothedHeading);

            _position = ComputeDesiredPosition(viewHeading);
            // dt of zero: any wall in the way pulls in immediately, nothing eases.
            _renderPosition = KeepOutOfWalls(_position, 0f);
            _rotation = ComputeDesiredRotation(_renderPosition, viewHeading);

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

            // 2. Reverse. The swing is applied AFTER the smoothing above and never inside
            //    ComputeHeading, so _smoothedHeading stays in nose space: its own Vector3.Slerp
            //    therefore never sees a 180 degree step and can never become the degenerate case.
            UpdateReverseView(dt);
            Vector3 viewHeading = ApplyReverseView(_smoothedHeading);

            // 3. Where the camera wants to be, then ease towards it.
            Vector3 desiredPosition = ComputeDesiredPosition(viewHeading);
            _position = Vector3.SmoothDamp(_position, desiredPosition, ref _positionVelocity,
                Tuning.cameraFollowSmoothTime, Mathf.Infinity, dt);

            // 4. If a wall is between the car and that point, stop short of it. Applied to the
            //    smoothed result, not the target, so the wall never lags through the easing.
            _renderPosition = KeepOutOfWalls(_position, dt);

            // 5. Aim at the look-ahead point from where the camera actually is.
            Quaternion desiredRotation = ComputeDesiredRotation(_renderPosition, viewHeading);
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

            Vector3 velocityDirection = velocity.normalized;

            // Vector3.Slerp has no defined rotation axis between two opposite vectors, and once the
            // car is travelling backwards nose and velocity ARE opposite, so the blend below is
            // ill-conditioned exactly when reversing. It also has nothing useful left to say: the
            // reverse swing is already telling the framing where the car is going. -0.95 is about
            // 162 degrees, and driftSlipAngleThreshold is 12 degrees, so no normal slide is anywhere
            // near this and drifting is untouched.
            const float AntiparallelDot = -0.95f;
            if (Vector3.Dot(nose, velocityDirection) <= AntiparallelDot) return ApplySwivel(nose);

            Vector3 blended = Vector3.Slerp(nose, velocityDirection,
                Tuning.cameraVelocityInfluence);

            return ApplySwivel(blended.sqrMagnitude > 0.0001f ? blended.normalized : nose);
        }

        // ==================================================================
        // REVERSE VIEW
        // ==================================================================

        /// <summary>
        /// Decides whether the camera should be looking behind the car, and moves the blend towards
        /// that decision.
        ///
        /// WHY THIS NEEDS A STATE MACHINE AND NOT JUST A SIGN TEST
        /// Forward speed crosses zero every time the player brakes hard, bumps a kerb or rolls back
        /// on a slope. A bare `ForwardSpeed &lt; 0` test would swing the camera round on all of those,
        /// which is far worse than never swinging at all. So there are two different speeds with a
        /// dead zone between them, and a hold time on each side: the swing only starts after the car
        /// has genuinely been going backwards for a moment, and only ends after it has genuinely
        /// stopped doing so. Inside the dead zone nothing changes at all, which is what makes a
        /// three point turn swing the camera once rather than twice per shuffle.
        /// </summary>
        private void UpdateReverseView(float dt)
        {
            float forwardSpeed = target.ForwardSpeed;

            if (forwardSpeed <= reverseViewEnterSpeed)
            {
                _forwardHold = 0f;
                _reverseHold += dt;
                if (_reverseHold >= reverseViewHoldTime) _reverseTarget = 1f;
            }
            else if (forwardSpeed >= reverseViewExitSpeed)
            {
                _reverseHold = 0f;
                _forwardHold += dt;
                if (_forwardHold >= reverseViewReturnDelay) _reverseTarget = 0f;
            }
            // else: the dead zone between the two speeds. Both timers are held where they are and
            // the target is left alone, so braking to a standstill decides nothing either way.

            // MoveTowards rather than an exponential ease, so a full swing takes exactly
            // reverseViewTransitionTime seconds and that number means what it says on the tooltip.
            // The ease at both ends comes from the SmoothStep in ApplyReverseView.
            _reverseBlend = Mathf.MoveTowards(_reverseBlend, _reverseTarget,
                dt / Mathf.Max(0.0001f, reverseViewTransitionTime));
        }

        /// <summary>
        /// Rotates the heading round towards the back of the car by however far the reverse swing
        /// has got.
        ///
        /// WHY ONE ROTATION ABOUT WORLD UP
        /// ComputeDesiredPosition puts the camera at carPos - heading * distance and
        /// ComputeDesiredRotation aims at carPos + heading * lookAhead, which is 28 m out on
        /// Balanced and 34 m on Loose. Negating just the look-ahead would put the aim point about
        /// 21 m BEHIND the camera and the car would leave the frame entirely. Lerping the two signs
        /// through zero instead walks the camera straight through the car. A single 180 degree
        /// rotation about world up carries the camera round the side of the car to the nose and the
        /// aim point round to 28 m behind the tail, keeps the car framed the whole way, is
        /// continuous, and has an explicitly named axis so it can never be ill-conditioned the way
        /// a Slerp between opposite vectors is.
        /// </summary>
        private Vector3 ApplyReverseView(Vector3 heading)
        {
            float eased = Mathf.SmoothStep(0f, 1f, _reverseBlend);
            if (eased <= 0.0001f) return heading;
            return Quaternion.AngleAxis(180f * eased, Vector3.up) * heading;
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

        /// <summary>
        /// Reads the look input and turns it into a yaw and pitch offset.
        ///
        /// TWO DEVICES, TWO UNITS, TWO BRANCHES
        /// A mouse delta is a DISPLACEMENT: it already says how far the hand moved during this
        /// frame, so multiplying it by frame time is simply wrong and makes the same hand movement
        /// turn the camera 2.4x further at 60 fps than at 144. A stick is a RATE: it says how fast
        /// to turn, so it must be multiplied by frame time. Both arrive merged into one Vector2, so
        /// the device is asked for and the two are scaled differently on purpose. If the device
        /// cannot be identified the mouse path is taken, because keyboard and mouse is what the
        /// playtests ran on.
        /// </summary>
        private void ReadSwivelInput(float dt)
        {
            Vector2 look = input != null ? input.CameraLook : Vector2.zero;
            bool usingMouse = input == null || input.CameraLookIsMouse;

            if (usingMouse)
            {
                // Back to pixels, so the sensitivity dial is stated in a unit a player understands.
                Vector2 pixels = look / MouseBindingDeltaScale;
                if (pixels.magnitude >= mouseLookDeadzonePixels)
                {
                    _yawOffset += pixels.x * mouseLookDegreesPerPixel;
                    _pitchOffset = Mathf.Clamp(
                        _pitchOffset - pixels.y * mouseLookDegreesPerPixel, -35f, 55f);
                    _timeSinceSwivel = 0f;
                    return;
                }
            }
            else if (look.sqrMagnitude > 0.0001f)
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
            // While running, the reverse swing is part of where the camera is actually looking, so
            // the gizmo has to include it or the drawn sightline disagrees with the shot on screen.
            Vector3 heading = Application.isPlaying && _smoothedHeading.sqrMagnitude > 0.0001f
                ? ApplyReverseView(_smoothedHeading)
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
