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

        /// <summary>Held boost: the second speed stage, above throttle-only top speed.</summary>
        public bool boost;
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

        /// <summary>
        /// Where this wheel touches the ground, in world space. When the wheel is airborne this is
        /// where the contact patch would be at full extension, which is meaningless for effects, so
        /// check <see cref="grounded"/> first.
        /// </summary>
        public Vector3 contactPoint;

        /// <summary>The ground normal under THIS wheel, not the car average. World up when airborne.</summary>
        public Vector3 contactNormal;

        /// <summary>
        /// How fast this wheel's contact patch is sliding sideways, m/s. Taken at the wheel rather
        /// than at the centre of mass, so a rotating car correctly reports the outside rear wheel
        /// sliding hardest. This is the number skid marks and tyre squeal should be driven from.
        /// </summary>
        public float lateralSlipSpeed;
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

        /// <summary>
        /// The surface under the car right now, averaged across whichever wheels are touching
        /// ground. Read this for skid mark colour, particle colour and any HUD readout. When the car
        /// is airborne it holds the last surface it was on, which is what a landing effect needs.
        /// </summary>
        public SurfaceProfile Surface { get; private set; } = SurfaceType.Default;

        /// <summary>The surface under one wheel, 0 to 3. Front left, front right, rear left, rear right.</summary>
        public SurfaceProfile SurfaceUnderWheel(int index) =>
            index >= 0 && index < 4 ? _wheelSurfaces[index] : SurfaceType.Default;

        /// <summary>
        /// How hard the tyres are working along the car this step, m/s^2. Drive and braking both count,
        /// boost does not. This is what the friction circle spends against the grip budget, and it is
        /// worth exposing because "why did the rear let go there" is answered by this number.
        /// </summary>
        public float LongitudinalDemand { get; private set; }

        /// <summary>Lateral grip actually available after longitudinal demand took its share, m/s^2.</summary>
        public float AvailableLateralGrip { get; private set; }

        /// <summary>Boost left in the tank, in boost-seconds. Drive the HUD gauge from this.</summary>
        public float BoostRemaining { get; private set; }

        /// <summary>Boost left as 0 to 1, for a bar or a dial.</summary>
        public float BoostFraction =>
            tuning != null && tuning.boostCapacity > 0f
                ? Mathf.Clamp01(BoostRemaining / tuning.boostCapacity)
                : 0f;

        /// <summary>True while boost is actually being spent, as opposed to merely held.</summary>
        public bool IsBoosting { get; private set; }

        /// <summary>
        /// True at and above the supersonic threshold. This is the hook for the trail, the speed
        /// lines, the engine howl and any camera shake: a state that switches on and off reads as
        /// fast in a way a gradually rising number never does.
        /// </summary>
        public bool IsSupersonic { get; private set; }

        /// <summary>
        /// Angle in DEGREES between where the car is pointing and where it is actually going.
        /// 0 = tracking true. 90 = travelling straight sideways. This is the number that says
        /// "this is a drift" rather than "this is a corner", and it is what tyre squeal volume,
        /// smoke and skid marks should all be driven from.
        /// </summary>
        public float SlipAngle { get; private set; }

        /// <summary>
        /// True while the car is sliding enough, and fast enough, to count as drifting. Read this
        /// for audio, particles and any drift scoring rather than recomputing the test elsewhere.
        /// </summary>
        public bool IsDrifting { get; private set; }

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
        private float _boostRechargeCountdown;

        private readonly SurfaceProfile[] _wheelSurfaces = new SurfaceProfile[4];

        // GetComponent is not free and this lookup happens four times per physics step, which at
        // 120 Hz is 480 calls a second for an answer that almost never changes. Caching by collider
        // turns that into a dictionary hit. Nulls are cached too, so an unlabelled floor is only
        // ever asked about once.
        private readonly System.Collections.Generic.Dictionary<Collider, SurfaceType> _surfaceCache =
            new System.Collections.Generic.Dictionary<Collider, SurfaceType>(64);
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

            // Start with a full tank so the fast state is reachable from the first second of play,
            // rather than making the player wait to find out what the car can do.
            BoostRemaining = tuning.boostCapacity;
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

            // Headroom above whichever ceiling is higher, so the boost is never silently clamped by
            // a limit derived from the throttle-only top speed.
            float highestIntendedSpeed = tuning.boostEnabled
                ? Mathf.Max(tuning.topSpeed, tuning.boostTopSpeed)
                : tuning.topSpeed;
            _rb.maxLinearVelocity = highestIntendedSpeed * 2.5f;

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

            // 4b. Boost, also grounded or not: a rocket does not need wheels on the floor.
            ApplyBoost(dt);

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

                // A wheel in the air keeps the surface it last touched rather than resetting to
                // tarmac, so a jump off dirt still lands with dirt particles.

                // Hanging at full extension is the default: that is where a wheel sits in mid air.
                float suspensionLength = rest;

                // Per-wheel normal, so a mark laid on a ramp lies flat on the ramp rather than
                // following the car's averaged idea of which way is up.
                Vector3 contactNormal = up;

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

                    contactNormal = hit.normal;
                    _wheelSurfaces[i] = ResolveSurface(hit.collider);
                }

                // Sideways slide measured AT this wheel. GetPointVelocity is what makes the outside
                // rear wheel of a spinning car report more slide than the inside front one, which is
                // the difference between four identical skid marks and four that read as a drift.
                Vector3 wheelRight = Vector3.ProjectOnPlane(transform.right, contactNormal);
                float slip = wheelRight.sqrMagnitude > 0.0001f
                    ? Vector3.Dot(_rb.GetPointVelocity(_wheelContactPoints[i]), wheelRight.normalized)
                    : 0f;

                _wheelVisuals[i] = new WheelVisualState
                {
                    index = i,
                    grounded = _wheelGrounded[i],
                    suspensionLength = suspensionLength,
                    compression01 = Mathf.Clamp01((rest - suspensionLength) / Mathf.Max(0.0001f, rest * 0.7f)),
                    isFront = i < 2,
                    isLeft = (i % 2) == 0,
                    contactPoint = _wheelContactPoints[i],
                    contactNormal = contactNormal,
                    lateralSlipSpeed = _wheelGrounded[i] ? slip : 0f
                };
            }

            WheelsOnGround = groundedCount;
            IsGrounded = groundedCount > 0;

            UpdateSurfaceBlend();
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
        /// What kind of ground a collider is, cached so the answer is looked up once per collider
        /// rather than four times per physics step forever.
        ///
        /// A collider with no SurfaceType returns plain tarmac. That default is deliberate: it means
        /// an unlabelled floor drives exactly as it did before this system existed, so none of the
        /// readings already taken in the lab are invalidated by adding surfaces.
        /// </summary>
        private SurfaceProfile ResolveSurface(Collider collider)
        {
            if (collider == null) return SurfaceType.Default;

            if (!_surfaceCache.TryGetValue(collider, out SurfaceType surface))
            {
                // GetComponentInParent, not GetComponent: the lab builds a patch as a parent with
                // its geometry underneath, and labelling the parent should cover the children.
                surface = collider.GetComponentInParent<SurfaceType>();

                // Cached even when null, so an unlabelled floor is not re-queried every step.
                _surfaceCache[collider] = surface;
            }

            return surface != null ? surface.Profile : SurfaceType.Default;
        }

        /// <summary>
        /// Blends the four wheel surfaces into the one the car is treated as driving on.
        ///
        /// Averaging matters at boundaries. With two wheels on tarmac and two on ice, a "whichever
        /// surface most wheels are on" rule would flip between full grip and almost none as the car
        /// crossed the line, and a car that changes behaviour discontinuously reads as broken rather
        /// than as difficult. Averaging gives a transition the player can feel and drive through.
        /// </summary>
        private void UpdateSurfaceBlend()
        {
            int count = 0;
            float grip = 0f, drive = 0f, brake = 0f, roll = 0f, markStrength = 0f, particles = 0f;
            Color mark = Color.clear, particleColour = Color.clear;
            SurfaceKind dominant = Surface.kind;
            bool spray = false;
            float dominantWeight = -1f;

            for (int i = 0; i < 4; i++)
            {
                if (!_wheelGrounded[i]) continue;

                SurfaceProfile s = _wheelSurfaces[i];
                grip += s.gripMultiplier;
                drive += s.driveMultiplier;
                brake += s.brakeMultiplier;
                roll += s.rollingResistance;
                markStrength += s.markStrength;
                particles += s.particleAmount;
                mark += s.markColour;
                particleColour += s.particleColour;
                count++;

                // The discrete fields cannot be averaged, so the loosest surface under any wheel
                // wins. One wheel on grass should throw grass, because that is what is happening.
                if (s.particleAmount > dominantWeight)
                {
                    dominantWeight = s.particleAmount;
                    dominant = s.kind;
                    spray = s.sprayWhenRolling;
                }
            }

            // Airborne: hold the last surface, so a landing still reads as landing on that ground.
            if (count == 0) return;

            float inverse = 1f / count;
            Surface = new SurfaceProfile
            {
                kind = dominant,
                sprayWhenRolling = spray,
                gripMultiplier = grip * inverse,
                driveMultiplier = drive * inverse,
                brakeMultiplier = brake * inverse,
                rollingResistance = roll * inverse,
                markStrength = markStrength * inverse,
                particleAmount = particles * inverse,
                markColour = mark * inverse,
                particleColour = particleColour * inverse
            };
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

            // Slip angle from the two components we already have. Atan2 against the ABSOLUTE forward
            // speed so that reversing does not read as a 180 degree slide.
            SlipAngle = Speed < 0.5f
                ? 0f
                : Mathf.Abs(Mathf.Atan2(LateralSpeed, Mathf.Abs(ForwardSpeed)) * Mathf.Rad2Deg);

            IsDrifting = IsGrounded
                         && Speed >= tuning.driftMinimumSpeed
                         && SlipAngle >= tuning.driftSlipAngleThreshold;
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
                    // Loose ground spins the wheels instead of pushing the car, so the same throttle
                    // produces less acceleration on dirt than on tarmac.
                    driveAccel += tuning.AccelerationAt(ForwardSpeed) * throttle * Surface.driveMultiplier;
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
                stoppingAccel += tuning.brakeDeceleration * brake * Surface.brakeMultiplier;
            }

            // Deep grass and loose dirt drag the car even under power. This is what makes cutting a
            // corner across the verge a decision with a cost rather than a free shortcut.
            stoppingAccel += Surface.rollingResistance;

            stoppingAccel = Mathf.Min(stoppingAccel, maxStoppingAccel);

            float totalAccel = driveAccel - Mathf.Sign(ForwardSpeed) * stoppingAccel;

            // Do not let the drive push past top speed. The acceleration curve already fades to
            // zero at top speed, this is just a backstop for slopes and shoves.
            if (driveAccel > 0f && ForwardSpeed >= tuning.topSpeed) totalAccel = Mathf.Min(totalAccel, 0f);
            if (driveAccel < 0f && ForwardSpeed <= -tuning.reverseTopSpeed) totalAccel = Mathf.Max(totalAccel, 0f);

            // Recorded for the friction circle. This is how much work the tyres are doing along the
            // car, and it is what the lateral grip calculation then has to share a budget with.
            // Boost is deliberately NOT counted: it is a rocket, not the tyres, so it does not consume
            // grip. That also makes boost the tool for holding a slide rather than causing one.
            LongitudinalDemand = Mathf.Abs(totalAccel);

            if (Mathf.Abs(totalAccel) > 0.0001f)
            {
                _rb.AddForce(GroundForward * totalAccel, ForceMode.Acceleration);
            }
        }

        // ==================================================================
        // 4b. BOOST: the second speed stage
        // ==================================================================

        /// <summary>
        /// Boost is a separate push with its own, higher ceiling.
        ///
        /// It is deliberately NOT just a bigger number on the throttle. Throttle fades out at
        /// topSpeed and boost carries the car from there up to boostTopSpeed, so there are two
        /// distinct states and the player can feel the moment they cross between them. A single
        /// flat top speed has nothing to be compared against, which is exactly why simply raising
        /// topSpeed makes a car feel no faster than it did before.
        ///
        /// Runs whether or not the wheels are on the ground, because a rocket does not care.
        /// </summary>
        private void ApplyBoost(float dt)
        {
            IsBoosting = false;

            if (!tuning.boostEnabled)
            {
                IsSupersonic = false;
                return;
            }

            bool wants = Drive.boost && BoostRemaining > 0f;

            if (wants)
            {
                // Only push while there is headroom left. Without this the boost keeps spending fuel
                // at the ceiling and the tank drains for nothing the player can see.
                if (ForwardSpeed < tuning.boostTopSpeed)
                {
                    _rb.AddForce(GroundForward * tuning.boostAcceleration, ForceMode.Acceleration);
                }

                BoostRemaining = Mathf.Max(0f, BoostRemaining - tuning.boostConsumptionRate * dt);
                _boostRechargeCountdown = tuning.boostRechargeDelay;
                IsBoosting = true;
            }
            else if (tuning.boostRechargeRate > 0f)
            {
                // A delay before refilling, so feathering the button is not a free way to sit at top
                // speed forever. Spending the tank has to be a decision with a cost.
                _boostRechargeCountdown = Mathf.Max(0f, _boostRechargeCountdown - dt);

                if (_boostRechargeCountdown <= 0f)
                {
                    BoostRemaining = Mathf.Min(
                        tuning.boostCapacity,
                        BoostRemaining + tuning.boostRechargeRate * dt);
                }
            }

            IsSupersonic = Speed >= tuning.supersonicThreshold;
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
            float yawAuthority = tuning.maxYawAcceleration;

            // While drifting, hand most of the rotation over to the tyres.
            //
            // Steering here COMMANDS a turn rate. At full authority that command wins instantly, so
            // the moment the rear steps out the steering hauls the car straight back to the turn rate
            // it asked for and the slide dies before the player can hold it. Backing the authority off
            // during a drift is what lets the axle forces decide the rotation, which in turn is what
            // makes counter-steering actually catch the car instead of just requesting a new turn rate.
            //
            // Only applies when perAxleGrip is on. Without axle forces there is nothing else producing
            // yaw, so reducing this on the original model would just make the car unresponsive.
            if (tuning.perAxleGrip && IsDrifting)
            {
                yawAuthority *= tuning.driftYawAuthority;
            }

            float yawAccel = Mathf.Clamp(
                yawError / Mathf.Max(dt, 0.0001f),
                -yawAuthority,
                yawAuthority);

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
            if (tuning.perAxleGrip)
            {
                ApplyLateralGripPerAxle(dt);
                return;
            }

            float grip = tuning.lateralGripAcceleration * Surface.gripMultiplier;

            // The friction circle. Accelerating hard leaves less grip for cornering, which is what
            // makes the throttle a handling input rather than just a speed input.
            grip = tuning.LateralGripAfterLongitudinal(grip, LongitudinalDemand);

            if (Drive.handbrake) grip *= tuning.handbrakeGripMultiplier;
            AvailableLateralGrip = grip;
            if (grip <= 0f) return;

            float accelNeededToStopSliding = Mathf.Abs(LateralSpeed) / Mathf.Max(dt, 0.0001f);
            float accelToApply = Mathf.Min(accelNeededToStopSliding, grip);
            if (accelToApply <= 0.0001f) return;

            _rb.AddForce(GroundRight * (-Mathf.Sign(LateralSpeed) * accelToApply),
                ForceMode.Acceleration);
        }

        /// <summary>
        /// The same grip budget, split front/rear and applied AT THE AXLES.
        ///
        /// This is the whole difference between a skid and a drift. One force at the centre of mass
        /// can only ever slide the car sideways. Two forces at two positions also produce a yaw
        /// moment, so when the rear axle runs out of grip before the front, the back end swings and
        /// the car rotates because it is sliding. That is oversteer, and oversteer is what a drift is.
        ///
        /// The bookkeeping is deliberately arranged so that at gripBalance 0.5 the total sideways
        /// force is exactly what the single-force version produced. Each axle gets a budget of
        /// (total * share * 2) and contributes half of what it spends, so the two halves add back up
        /// to the original number. That means switching this on does not silently move the turning
        /// circle, and the readings from F2 still mean what they meant.
        /// </summary>
        private void ApplyLateralGripPerAxle(float dt)
        {
            float total = tuning.lateralGripAcceleration * Surface.gripMultiplier;
            if (total <= 0f) return;

            float frontGrip = total * tuning.gripBalance * 2f;
            float rearGrip = total * (1f - tuning.gripBalance) * 2f;

            // The friction circle applies to the DRIVEN axle only, which is the rear. That asymmetry is
            // the whole reason power oversteer exists: the rear tyres are being asked to put the power
            // down AND hold the corner, the fronts only have to steer. Applying it to both axles would
            // just make the car understeer under power instead of stepping out.
            rearGrip = tuning.LateralGripAfterLongitudinal(rearGrip, LongitudinalDemand);
            AvailableLateralGrip = (frontGrip + rearGrip) * 0.5f;

            // The handbrake works on the rear only. Locking one end is what turns a handbrake pull
            // into a rotation instead of a four wheel slide.
            if (Drive.handbrake) rearGrip *= tuning.handbrakeRearGripMultiplier;

            ApplyAxleGrip(tuning.wheelForwardOffset, frontGrip, dt);
            ApplyAxleGrip(-tuning.wheelForwardOffset, rearGrip, dt);
        }

        /// <summary>
        /// Spends one axle's grip budget against the sideways motion measured AT THAT AXLE.
        ///
        /// GetPointVelocity is the important part: a rotating car has different velocity at the nose
        /// and at the tail, and that difference is exactly the information a single centre-of-mass
        /// reading throws away.
        /// </summary>
        private void ApplyAxleGrip(float localForwardOffset, float grip, float dt)
        {
            if (grip <= 0f) return;

            Vector3 axlePosition = transform.TransformPoint(
                new Vector3(0f, tuning.wheelHeightOffset, localForwardOffset));

            float lateralAtAxle = Vector3.Dot(_rb.GetPointVelocity(axlePosition), GroundRight);

            float accelNeeded = Mathf.Abs(lateralAtAxle) / Mathf.Max(dt, 0.0001f);
            float accelToApply = Mathf.Min(accelNeeded, grip);
            if (accelToApply <= 0.0001f) return;

            // Half each, so front + rear equals the original single force at neutral balance.
            _rb.AddForceAtPosition(
                GroundRight * (-Mathf.Sign(lateralAtAxle) * accelToApply * 0.5f),
                axlePosition,
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

            // A measured run has to start from a known boost state, or one reading gets a full tank
            // and the next gets whatever was left over.
            if (tuning != null) BoostRemaining = tuning.boostCapacity;
            _boostRechargeCountdown = 0f;
            IsBoosting = false;
            IsSupersonic = false;
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
