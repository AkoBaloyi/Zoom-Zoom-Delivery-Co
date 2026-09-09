using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// What the car is being asked to do this frame. Filled in by VehicleInput when a human is
    /// driving, or by VehicleMeasurement when a test is driving. The car cannot tell the difference,
    /// which is exactly why the measurements are trustworthy.
    /// </summary>
    public struct DriveInput
    {
        /// <summary>-1 full reverse, 0 coast, +1 full throttle.</summary>
        public float throttle;

        /// <summary>-1 full left, 0 straight, +1 full right.</summary>
        public float steer;

        /// <summary>0 to 1. The real brake, separate from reverse.</summary>
        public float brake;

        /// <summary>Held handbrake: cuts sideways grip on purpose.</summary>
        public bool handbrake;
    }

    /// <summary>
    /// Where one wheel currently is and what it is doing, for anything that needs to draw it.
    ///
    /// The physics does not need this. It is published purely so the wheel models can sit on the
    /// ground properly instead of being welded to the body: the visuals read the same raycast the
    /// suspension already did, rather than doing their own and possibly disagreeing with it.
    /// </summary>
    public struct WheelVisualState
    {
        /// <summary>Which of the four. 0 front left, 1 front right, 2 rear left, 3 rear right.</summary>
        public int index;

        /// <summary>True when this wheel found ground.</summary>
        public bool grounded;

        /// <summary>
        /// How far below the ray origin the wheel's contact patch currently sits, metres. Equal to
        /// the rest length when the wheel is hanging free, smaller when the suspension is squashed.
        /// </summary>
        public float suspensionLength;

        /// <summary>0 = hanging at full extension, 1 = fully squashed. Handy for driving effects.</summary>
        public float compression01;

        /// <summary>Front wheels steer, rear wheels do not.</summary>
        public bool isFront;

        /// <summary>Left side of the car.</summary>
        public bool isLeft;
    }

    /// <summary>
    /// Arcade car on a Rigidbody, held up by four raycasts instead of WheelColliders.
    ///
    /// WHY RAYCASTS AND NOT WHEELCOLLIDERS
    /// WheelColliders simulate a real tyre: slip curves, friction circles, load sensitivity.
    /// That is great if you want a realistic car and terrible if you want a car you can explain.
    /// With WheelColliders, "why did I lose the back end there" has about six possible answers and
    /// most of them are invisible to the player. Here there are exactly two things that decide
    /// whether the car makes a corner:
    ///
    ///     1. the turn curvature the steering allows at this speed  (maxCurvatureBySpeed)
    ///     2. how much sideways acceleration the tyres can produce  (lateralGripAcceleration)
    ///
    /// A corner of radius R at speed V needs V*V/R of sideways grip. If the car has it, the car
    /// makes the corner. If it does not, the car runs wide, and it runs wide by a calculable
    /// amount. Same speed and same steering always gives the same line. That is the whole design
    /// target: demanding, but the player can always work out what happened and do better.
    ///
    /// THE ORDER OF WORK, every physics step:
    ///     read the ground -> hold the car up -> gravity -> drive/brake -> steer -> grip -> limits
    ///
    /// Everything is applied as forces and torques in ForceMode.Acceleration, never by moving the
    /// transform. Acceleration mode ignores mass, so changing the mass in the tuning asset does not
    /// secretly change the handling. Metres per second squared means what it says.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class VehicleController : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("Every number the car uses. Swap this asset to swap the whole feel of the car.")]
        [SerializeField] private VehicleTuning tuning;

        [Header("Debug")]
        [Tooltip("Draw the wheel rays and the grip/steer directions in the scene view.")]
        [SerializeField] private bool drawDebugGizmos = true;

        /// <summary>
        /// Set this every frame by whoever is driving.
        ///
        /// NonSerialized on purpose. This is live input, rewritten every single frame, so saving it
        /// into the scene file would be meaningless: it would store whatever key happened to be down
        /// when the scene was last saved, and show up as noise in every git diff of the scene.
        /// Marking it explicitly also settles Unity's serialization analyzer, which otherwise warns
        /// that it is quietly skipping the field. Better to say we do not want it saved than to
        /// leave the reader wondering whether it was an oversight.
        /// </summary>
        [System.NonSerialized] public DriveInput Drive;

        // ---------------- what the rest of the game can read ----------------

        public VehicleTuning Tuning => tuning;
        public Rigidbody Body => _rb;

        /// <summary>At least one wheel has ground under it.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>How many of the four wheels found ground. 4 = flat, 2 = on two wheels, 0 = airborne.</summary>
        public int WheelsOnGround { get; private set; }

        /// <summary>Average of the ground normals under the wheels. World up when airborne.</summary>
        public Vector3 GroundNormal { get; private set; } = Vector3.up;

        /// <summary>Car forward, flattened onto the ground. This is the direction the car drives in.</summary>
        public Vector3 GroundForward { get; private set; } = Vector3.forward;

        /// <summary>Car right, on the ground plane. This is the direction sideways slide happens in.</summary>
        public Vector3 GroundRight { get; private set; } = Vector3.right;

        /// <summary>Overall speed, m/s.</summary>
        public float Speed => _rb != null ? _rb.linearVelocity.magnitude : 0f;

        /// <summary>Speed along the car's nose, m/s. Negative when reversing.</summary>
        public float ForwardSpeed { get; private set; }

        /// <summary>Speed sideways through the tyres, m/s. This is the slide. 0 = perfectly gripping.</summary>
        public float LateralSpeed { get; private set; }

        /// <summary>Steering after smoothing, -1 to 1. What the car is really using.</summary>
        public float SteerAmount { get; private set; }

        /// <summary>Turn rate about the ground normal, rad/s.</summary>
        public float YawRate { get; private set; }

        /// <summary>
        /// The turn curvature the steering is currently asking for, in 1/metres. This is the real
        /// number the car is cornering on, so the wheel models can be turned to match it instead of
        /// guessing an angle that has nothing to do with the physics.
        /// </summary>
        public float SteerCurvature { get; private set; }

        /// <summary>How upright the car is. 1 = on its wheels, 0 = on its side, -1 = on its roof.</summary>
        public float Uprightness => Vector3.Dot(transform.up, Vector3.up);

        /// <summary>True when the car is the right way up enough to drive.</summary>
        public bool IsUpright => Uprightness > 0.5f;

        /// <summary>Always 4. Named rather than hard coded at every call site.</summary>
        public const int WheelCount = 4;

        /// <summary>
        /// Where wheel i is and what it is doing, for drawing it. 0 front left, 1 front right,
        /// 2 rear left, 3 rear right.
        /// </summary>
        public WheelVisualState GetWheelVisualState(int index)
        {
            return _wheelVisuals[Mathf.Clamp(index, 0, WheelCount - 1)];
        }

        /// <summary>Where wheel i's ray starts, in the car's own space.</summary>
        public Vector3 GetWheelLocalRayOrigin(int index)
        {
            return _wheelLocalPositions[Mathf.Clamp(index, 0, WheelCount - 1)];
        }

        // ---------------- internals ----------------

        private Rigidbody _rb;
        private Collider[] _ownColliders;

        private readonly Vector3[] _wheelLocalPositions = new Vector3[4];
        private readonly bool[] _wheelGrounded = new bool[4];
        private readonly Vector3[] _wheelContactPoints = new Vector3[4];
        private readonly float[] _wheelCompression = new float[4];

        // Published for the visuals. Written once per physics step, read once per drawn frame.
        private readonly WheelVisualState[] _wheelVisuals = new WheelVisualState[4];

        // Reused so the raycasts never allocate.
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

        private float _steerVelocity;      // scratch for the steering smoothing
        private float _stuckTimer;
        private float _appliedFixedTimestep = -1f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _ownColliders = GetComponentsInChildren<Collider>(true);

            if (tuning == null)
            {
                Debug.LogError(
                    "[VehicleController] No VehicleTuning assigned. Drag one of the profiles from " +
                    "Assets/_Project/Scripts/Vehicle/Tuning onto the Tuning field. The car cannot " +
                    "drive without its numbers.", this);
                enabled = false;
                return;
            }

            tuning.EnsureCurves();
            ApplyTuningToBody();
            RebuildWheelPositions();
        }

        /// <summary>
        /// Pushes the tuning numbers into the Rigidbody. Called every physics step so that editing
        /// the tuning asset while the game is running takes effect immediately.
        /// </summary>
        private void ApplyTuningToBody()
        {
            _rb.mass = tuning.mass;
            _rb.centerOfMass = tuning.centreOfMassOffset;

            // We apply our own gravity from the tuning asset instead of using the project setting,
            // because gravity is a handling value and it belongs with the other handling values.
            _rb.useGravity = false;

            _rb.linearDamping = tuning.linearDamping;
            _rb.angularDamping = tuning.angularDamping;
            _rb.maxAngularVelocity = tuning.maxAngularSpeed;
            _rb.maxLinearVelocity = tuning.topSpeed * 2.5f;

            // Interpolation matters: physics runs at a fixed rate that is not the frame rate, so
            // without this the car visibly stutters even though the simulation is perfectly smooth.
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            // The wall test drives at 20 m/s into a 2 m thick wall. Discrete collision detection
            // can step straight through that at a high physics rate, which would look like a bug.
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (tuning.overrideFixedTimestep)
            {
                float step = 1f / Mathf.Max(1f, tuning.physicsHz);
                if (!Mathf.Approximately(step, _appliedFixedTimestep))
                {
                    Time.fixedDeltaTime = step;
                    _appliedFixedTimestep = step;
                    Debug.Log(
                        $"[VehicleController] Physics step set to {tuning.physicsHz:0} Hz " +
                        $"({step:0.00000} s) by tuning profile '{tuning.profileName}'. " +
                        "A fixed, high step rate is what makes runs repeatable.");
                }
            }
        }

        /// <summary>Works out where the four wheel rays start, from the tuning numbers.</summary>
        private void RebuildWheelPositions()
        {
            float x = tuning.wheelSideOffset;
            float y = tuning.wheelHeightOffset;
            float z = tuning.wheelForwardOffset;

            _wheelLocalPositions[0] = new Vector3(-x, y, z);   // front left
            _wheelLocalPositions[1] = new Vector3(x, y, z);    // front right
            _wheelLocalPositions[2] = new Vector3(-x, y, -z);  // rear left
            _wheelLocalPositions[3] = new Vector3(x, y, -z);   // rear right
        }

        private void FixedUpdate()
        {
            if (tuning == null) return;

            float dt = Time.fixedDeltaTime;

            ApplyTuningToBody();
            RebuildWheelPositions();

            // 1. Where is the ground, and are we on it.
            ReadGround();

            // 2. Work out the car's own sense of forward and sideways, on the ground plane.
            UpdateLocalVelocity();

            // 3. Hold the car up.
            ApplySuspension();

            // 4. Gravity, always, grounded or not.
            _rb.AddForce(Vector3.down * tuning.gravity, ForceMode.Acceleration);

            // 5, 6, 7. Driving only works with wheels on the ground. In the air the car is a
            // thrown object, which is what makes committing to a jump or a flip feel like a decision.
            if (IsGrounded)
            {
                ApplyDriveAndBrake(dt);
                ApplySteering(dt);
                ApplyLateralGrip(dt);
            }
            else
            {
                // Still smooth the steering input in the air so the wheels are already turned the
                // right way on landing, instead of snapping a frame later.
                SmoothSteerInput(dt);
            }

            // 8. Never let the player be stuck upside down with no way out.
            ApplyStuckRecovery(dt);
        }

        // ==================================================================
        // 1. GROUND
        // ==================================================================

        /// <summary>
        /// Fires one ray straight down from each wheel, in the CAR's down direction rather than
        /// world down, so the suspension still works on a slope or a ramp.
        /// </summary>
        private void ReadGround()
        {
            Vector3 up = transform.up;
            Vector3 down = -up;
            float rayLength = tuning.suspensionRestLength + tuning.suspensionExtraRayLength;

            int groundedCount = 0;
            Vector3 normalSum = Vector3.zero;

            float rest = tuning.suspensionRestLength;

            for (int i = 0; i < 4; i++)
            {
                Vector3 origin = transform.TransformPoint(_wheelLocalPositions[i]);
                _wheelGrounded[i] = false;
                _wheelCompression[i] = 0f;
                _wheelContactPoints[i] = origin + down * rest;

                // Hanging at full extension is the default: that is where a wheel sits in mid air.
                float suspensionLength = rest;

                if (TryRaycast(origin, down, rayLength, out RaycastHit hit))
                {
                    _wheelGrounded[i] = true;
                    _wheelContactPoints[i] = hit.point;

                    // Positive = suspension squashed. Negative = wheel hanging below rest length,
                    // which still counts as grounded on purpose: the car should not lose drive
                    // every time it crests a small bump. Predictable beats picky.
                    _wheelCompression[i] = rest - hit.distance;

                    // For drawing, the wheel can be squashed up towards the body but never past a
                    // bump stop, and never stretched further than its rest length. Without the
                    // lower clamp a hard landing pulls the wheel model up inside the bodywork.
                    suspensionLength = Mathf.Clamp(hit.distance, rest * 0.3f, rest);

                    normalSum += hit.normal;
                    groundedCount++;
                }

                _wheelVisuals[i] = new WheelVisualState
                {
                    index = i,
                    grounded = _wheelGrounded[i],
                    suspensionLength = suspensionLength,
                    compression01 = Mathf.Clamp01((rest - suspensionLength) / Mathf.Max(0.0001f, rest * 0.7f)),
                    isFront = i < 2,
                    isLeft = (i % 2) == 0
                };
            }

            WheelsOnGround = groundedCount;
            IsGrounded = groundedCount > 0;
            GroundNormal = groundedCount > 0 ? (normalSum / groundedCount).normalized : Vector3.up;

            // The basis the car actually drives in: its nose flattened onto the ground.
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, GroundNormal);
            if (flatForward.sqrMagnitude < 0.0001f)
            {
                // Nose pointing straight at the ground or straight up. Fall back to the roof
                // direction so the basis never collapses to zero and produces NaNs.
                flatForward = Vector3.ProjectOnPlane(transform.up, GroundNormal);
            }

            GroundForward = flatForward.sqrMagnitude > 0.0001f
                ? flatForward.normalized
                : Vector3.forward;

            GroundRight = Vector3.Cross(GroundNormal, GroundForward).normalized;
        }

        /// <summary>
        /// Raycast that ignores the car's own colliders. The ray starts inside the car body, so
        /// without this filter the car could detect itself as ground.
        /// </summary>
        private bool TryRaycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
        {
            best = default;
            bool found = false;
            float bestDistance = float.MaxValue;

            int count = Physics.RaycastNonAlloc(
                origin, direction, _hitBuffer, distance, tuning.groundMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (IsOwnCollider(hit.collider)) continue;
                if (hit.distance >= bestDistance) continue;

                bestDistance = hit.distance;
                best = hit;
                found = true;
            }

            return found;
        }

        private bool IsOwnCollider(Collider c)
        {
            if (c == null) return true;
            if (c.attachedRigidbody == _rb) return true;

            for (int i = 0; i < _ownColliders.Length; i++)
            {
                if (_ownColliders[i] == c) return true;
            }
            return false;
        }

        // ==================================================================
        // 2. LOCAL VELOCITY
        // ==================================================================

        private void UpdateLocalVelocity()
        {
            Vector3 v = _rb.linearVelocity;
            ForwardSpeed = Vector3.Dot(v, GroundForward);
            LateralSpeed = Vector3.Dot(v, GroundRight);
            YawRate = Vector3.Dot(_rb.angularVelocity, GroundNormal);
        }

        // ==================================================================
        // 3. SUSPENSION
        // ==================================================================

        /// <summary>
        /// Spring and damper at each wheel. The spring pushes the car up out of the ground, the
        /// damper is what stops it bouncing like a pogo stick. Applying the force at the wheel
        /// position rather than at the centre is what gives weight transfer for free: the car
        /// leans in corners and squats under braking without any of that being scripted.
        /// </summary>
        private void ApplySuspension()
        {
            Vector3 up = transform.up;

            for (int i = 0; i < 4; i++)
            {
                if (!_wheelGrounded[i]) continue;

                float compression = _wheelCompression[i];

                // A suspension spring can only push. If the wheel is hanging below rest length
                // there is nothing to push against, so no force.
                if (compression <= 0f) continue;

                Vector3 origin = transform.TransformPoint(_wheelLocalPositions[i]);
                float upwardSpeed = Vector3.Dot(_rb.GetPointVelocity(origin), up);

                float accel = tuning.springStrength * compression
                              - tuning.springDamper * upwardSpeed;

                if (accel <= 0f) continue;

                _rb.AddForceAtPosition(up * accel, origin, ForceMode.Acceleration);
            }
        }

        // ==================================================================
        // 4/5. DRIVE, REVERSE, BRAKE, COAST
        // ==================================================================

        private void ApplyDriveAndBrake(float dt)
        {
            float throttle = ApplyDeadzone(Drive.throttle, tuning.inputDeadzone);
            float brake = Mathf.Clamp01(Drive.brake);

            // How much deceleration we are allowed to apply before we would drag the car backwards.
            // Braking should stop the car, never reverse it. Without this cap the car twitches
            // backwards at a standstill, which reads as the physics being unreliable.
            float maxStoppingAccel = Mathf.Abs(ForwardSpeed) / Mathf.Max(dt, 0.0001f);

            float driveAccel = 0f;      // along GroundForward, signed
            float stoppingAccel = 0f;   // always opposes ForwardSpeed

            if (throttle > 0f)
            {
                if (ForwardSpeed < -0.5f)
                {
                    // Rolling backwards, player asks for forwards. That is a brake, not a launch.
                    stoppingAccel += tuning.brakeDeceleration * throttle;
                }
                else
                {
                    driveAccel += tuning.AccelerationAt(ForwardSpeed) * throttle;
                }
            }
            else if (throttle < 0f)
            {
                if (ForwardSpeed > 0.5f)
                {
                    // Rolling forwards, player asks for reverse. Brake first, reverse after,
                    // which is what everyone expects from arcade controls.
                    stoppingAccel += tuning.brakeDeceleration * -throttle;
                }
                else if (Mathf.Abs(ForwardSpeed) < tuning.reverseTopSpeed)
                {
                    driveAccel += tuning.reverseAcceleration * throttle;
                }
            }
            else
            {
                // No throttle at all still slows the car down. Lifting off has to be a real
                // third option next to throttle and brake, otherwise the only way to slow down
                // is the brake and cornering loses all its texture.
                stoppingAccel += tuning.coastDeceleration;
            }

            if (brake > 0f)
            {
                stoppingAccel += tuning.brakeDeceleration * brake;
            }

            stoppingAccel = Mathf.Min(stoppingAccel, maxStoppingAccel);

            float totalAccel = driveAccel - Mathf.Sign(ForwardSpeed) * stoppingAccel;

            // Do not let the drive push past top speed. The acceleration curve already fades to
            // zero at top speed, this is just a backstop for slopes and shoves.
            if (driveAccel > 0f && ForwardSpeed >= tuning.topSpeed) totalAccel = Mathf.Min(totalAccel, 0f);
            if (driveAccel < 0f && ForwardSpeed <= -tuning.reverseTopSpeed) totalAccel = Mathf.Max(totalAccel, 0f);

            if (Mathf.Abs(totalAccel) > 0.0001f)
            {
                _rb.AddForce(GroundForward * totalAccel, ForceMode.Acceleration);
            }
        }

        // ==================================================================
        // 6. STEERING
        // ==================================================================

        private void SmoothSteerInput(float dt)
        {
            float target = ApplyDeadzone(Drive.steer, tuning.inputDeadzone);
            SteerAmount = tuning.steerInputSmoothTime <= 0.0001f
                ? target
                : Mathf.SmoothDamp(SteerAmount, target, ref _steerVelocity,
                    tuning.steerInputSmoothTime, Mathf.Infinity, dt);

            // Kept up to date even in mid air, where there is no steering torque to apply but the
            // front wheels should still visibly be turned the way the player is holding them.
            SteerCurvature = tuning.MaxCurvatureAt(ForwardSpeed) * SteerAmount;
        }

        /// <summary>
        /// Steering by CURVATURE, which is 1 / turn radius.
        ///
        /// The curve says "at this speed, full lock gives you this much curvature". Turn rate then
        /// falls out of the maths: turnRate = curvature * speed. Two things come from this for free:
        ///
        ///   - The faster you go, the wider the tightest possible corner gets. That is what stops
        ///     the car spinning out at speed, and it is a rule the player can feel and predict
        ///     rather than a hidden stability assist quietly saving them.
        ///   - Half stick is half the curvature at any speed. Steering input means the same thing
        ///     at 5 m/s and at 25 m/s.
        ///
        /// Reversing works automatically: speed goes negative, so the turn direction flips, the way
        /// a real car does when you back it up.
        /// </summary>
        private void ApplySteering(float dt)
        {
            SmoothSteerInput(dt);

            float curvature = SteerCurvature;
            float targetYawRate = curvature * ForwardSpeed;

            float yawError = targetYawRate - YawRate;

            // maxYawAcceleration limits how quickly the car takes up the new turn rate. It changes
            // how eager the car feels, not where the car ends up.
            float yawAccel = Mathf.Clamp(
                yawError / Mathf.Max(dt, 0.0001f),
                -tuning.maxYawAcceleration,
                tuning.maxYawAcceleration);

            _rb.AddTorque(GroundNormal * yawAccel, ForceMode.Acceleration);
        }

        // ==================================================================
        // 7. SIDEWAYS GRIP: slide or bite
        // ==================================================================

        /// <summary>
        /// Grip as a budget, in m/s^2.
        ///
        /// Every step we work out how much sideways acceleration it would take to cancel the slide
        /// completely, then spend up to lateralGripAcceleration of it. If the budget covers it, the
        /// car tracks exactly where it is pointed. If the corner asks for more than the budget, the
        /// car runs wide by the difference.
        ///
        /// This is the "slides or bites" dial, and it is honest about it: there is no random chance
        /// of losing grip, no sudden snap. Ask for more than the car has and it understeers, every
        /// single time, by the same amount.
        /// </summary>
        private void ApplyLateralGrip(float dt)
        {
            float grip = tuning.lateralGripAcceleration;
            if (Drive.handbrake) grip *= tuning.handbrakeGripMultiplier;
            if (grip <= 0f) return;

            float accelNeededToStopSliding = Mathf.Abs(LateralSpeed) / Mathf.Max(dt, 0.0001f);
            float accelToApply = Mathf.Min(accelNeededToStopSliding, grip);
            if (accelToApply <= 0.0001f) return;

            _rb.AddForce(GroundRight * (-Mathf.Sign(LateralSpeed) * accelToApply),
                ForceMode.Acceleration);
        }

        // ==================================================================
        // 8. ANTI-SOFTLOCK
        // ==================================================================

        /// <summary>
        /// The flip is the player's recovery move, and the flip needs wheels on the ground. So a car
        /// resting on its roof has no way out. Sitting there unable to play is not difficulty, it is
        /// a dead end, so after a short delay we roll the car back over.
        /// </summary>
        private void ApplyStuckRecovery(float dt)
        {
            if (!tuning.autoRightWhenStuck)
            {
                _stuckTimer = 0f;
                return;
            }

            bool notUpright = Uprightness < tuning.stuckUprightThreshold;
            bool notMoving = Speed < tuning.stuckSpeedThreshold;

            if (!notUpright || !notMoving)
            {
                _stuckTimer = 0f;
                return;
            }

            _stuckTimer += dt;
            if (_stuckTimer < tuning.stuckTimeBeforeRighting) return;

            Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
            if (axis.sqrMagnitude < 0.0001f)
            {
                // Exactly upside down, so there is no single obvious way to roll. Nudge it.
                axis = transform.forward;
            }

            _rb.AddTorque(axis.normalized * tuning.rightingTorque, ForceMode.Acceleration);
        }

        // ==================================================================
        // HELPERS
        // ==================================================================

        /// <summary>
        /// Ignores tiny stick movement, then rescales what is left so the input still reaches a
        /// full 1.0. Without the rescale there is a visible step as the stick leaves the deadzone.
        /// </summary>
        private static float ApplyDeadzone(float value, float deadzone)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude <= deadzone) return 0f;
            if (deadzone >= 0.999f) return Mathf.Sign(value);

            float rescaled = (magnitude - deadzone) / (1f - deadzone);
            return Mathf.Sign(value) * Mathf.Clamp01(rescaled);
        }

        /// <summary>
        /// Drops the car at a position with no leftover speed or spin. Every measurement starts
        /// with this, so no reading can be contaminated by the run before it.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();

            // Turning interpolation off and on again clears the position history. Without this the
            // car visibly smears across the map from where it was to where it now is.
            RigidbodyInterpolation previous = _rb.interpolation;
            _rb.interpolation = RigidbodyInterpolation.None;

            _rb.position = position;
            _rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            _rb.interpolation = previous;

            Drive = default;
            SteerAmount = 0f;
            _steerVelocity = 0f;
            _stuckTimer = 0f;
        }

        private void OnDrawGizmos()
        {
            if (!drawDebugGizmos || tuning == null) return;

            // Works before the game starts too, so the wheel layout can be checked in the editor.
            float x = tuning.wheelSideOffset;
            float y = tuning.wheelHeightOffset;
            float z = tuning.wheelForwardOffset;
            float rest = tuning.suspensionRestLength;
            float extra = tuning.suspensionExtraRayLength;

            Vector3[] locals =
            {
                new Vector3(-x, y, z), new Vector3(x, y, z),
                new Vector3(-x, y, -z), new Vector3(x, y, -z)
            };

            for (int i = 0; i < locals.Length; i++)
            {
                Vector3 origin = transform.TransformPoint(locals[i]);
                bool grounded = Application.isPlaying && _wheelGrounded[i];

                Gizmos.color = grounded ? Color.green : Color.red;
                Gizmos.DrawLine(origin, origin - transform.up * rest);

                Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
                Gizmos.DrawLine(origin - transform.up * rest,
                    origin - transform.up * (rest + extra));

                Gizmos.color = grounded ? Color.green : Color.grey;
                Gizmos.DrawWireSphere(origin - transform.up * rest, 0.08f);
            }

            if (!Application.isPlaying) return;

            Vector3 centre = transform.position;

            // Where the car is pointed.
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(centre, centre + GroundForward * 3f);

            // How much it is sliding sideways.
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(centre, centre + GroundRight * LateralSpeed * 0.5f);

            // Which way is up as far as the ground is concerned.
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(centre, centre + GroundNormal * 2f);
        }
    }
}
