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
        // Engine demand survives brake priority even when wheel throttle is zero.
        public float engineThrottle;
        public bool freeRev;

        /// <summary>-1 full left, 0 straight, +1 full right.</summary>
        public float steer;

        /// <summary>0 to 1. The real brake, separate from reverse.</summary>
        public float brake;

        /// <summary>Retired input retained for historical harness compatibility. Ignored by the taxi.</summary>
        public bool handbrake;

        /// <summary>Retired input retained for historical harness compatibility. Ignored by the taxi.</summary>
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
    /// Crazy Taxi style Rigidbody vehicle with four suspension rays. Ground/suspension/visual
    /// state lives here; the driving and manoeuvre simulation is in VehicleController.Taxi.cs.
    /// Drive/Reverse gear gestures are buffered by VehicleInput and consumed once per physics step.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public partial class VehicleController : MonoBehaviour
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

        /// <summary>
        /// Sideways grip budget at the front axle, m/s^2, after the balance split. Zero until a
        /// grounded physics step has run the per-axle path.
        /// </summary>
        public float FrontAxleGrip { get; private set; }

        /// <summary>
        /// Sideways grip budget at the rear axle, m/s^2, after the balance split, the friction circle
        /// and the handbrake. This is the number the handbrake attacks, so watching it drop when the
        /// handbrake goes on is the direct confirmation that the drift tool is doing something.
        /// </summary>
        public float RearAxleGrip { get; private set; }

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
        /// True while the car is earning boost this step: drifting, or airborne outside a flip.
        /// The gauge lights up on this, and that light is the whole point of earning boost: the
        /// player has to SEE the slide pay out, or it is just a slower way round the corner.
        /// </summary>
        public bool IsEarningBoost { get; private set; }

        /// <summary>Boost earned in the most recent physics step. 0 when not earning.</summary>
        public float BoostEarnedThisStep { get; private set; }

        /// <summary>Boost earned since the last reset, in boost units. Lets the measurement
        /// harness and the playtest logger report how much of the fast state was paid for.</summary>
        public float TotalBoostEarned { get; private set; }

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
        public bool IsUpright => Uprightness > (tuning != null ? Taxi.drivableUprightness : 0.5f);

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





        private readonly SurfaceProfile[] _wheelSurfaces = new SurfaceProfile[4];

        // GetComponent is not free and this lookup happens four times per physics step, which at
        // 120 Hz is 480 calls a second for an answer that almost never changes. Caching by collider
        // turns that into a dictionary hit. Nulls are cached too, so an unlabelled floor is only
        // ever asked about once.
        private readonly System.Collections.Generic.Dictionary<Collider, SurfaceType> _surfaceCache =
            new System.Collections.Generic.Dictionary<Collider, SurfaceType>(64);

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
            InitialiseTaxi();
            ApplyTuningToBody();
            RebuildWheelPositions();

            // Start with a full tank so the fast state is reachable from the first second of play,
            // rather than making the player wait to find out what the car can do.
            BoostRemaining = 0f;
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
            float highestIntendedSpeed = Mathf.Max(Mathf.Max(Taxi.cruiseSpeed, Taxi.reverseSpeed),
                Mathf.Max(Mathf.Max(Taxi.dashSpeed, Taxi.backDashSpeed), Taxi.limiterMaximumSpeed));
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

            StepTaxi(dt);


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
            var blended = new SurfaceProfile
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

            // Weather is applied last and once, to the blend, never per wheel. Dry leaves the
            // blend untouched, so with no WeatherState in the scene this line is a no-op and
            // every lab measurement is still describing the car that ships.
            Surface = SurfaceType.ApplyWeather(blended, WeatherState.Current);
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
            ResetTaxi();
            SteerAmount = 0f;



            // A measured run has to start from a known boost state, or one reading gets a full tank
            // and the next gets whatever was left over.
            BoostRemaining = 0f;

            IsBoosting = false;
            IsSupersonic = false;
            IsEarningBoost = false;
            BoostEarnedThisStep = 0f;
            TotalBoostEarned = 0f;
        }

        /// <summary>
        /// Sets the boost tank directly, clamped to capacity. For the measurement harness only.
        /// Teleport refills the tank so every run starts from the same state, which is right for
        /// the spend test and wrong for the earn test: a full tank swallows everything a slide
        /// earns, and the reading would be zero however well the earning worked.
        /// </summary>
        public void SetBoostRemaining(float amount)
        {
            BoostRemaining = 0f; // Retired boost API retained for historical log/harness compatibility.
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
