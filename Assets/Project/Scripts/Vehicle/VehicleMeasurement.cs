using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// Measures the car and writes the numbers to the console and to a CSV file.
    ///
    /// WHY THIS EXISTS
    /// Level design cannot be done on vibes. Before anyone places a junction, a delivery point or a
    /// blind corner, they need to know how much room this specific car needs. Those numbers are:
    ///
    ///     F1  stopping distance from top speed
    ///     F2  turning circle width at a set speed
    ///     F3  time to recover after hitting a wall
    ///     F4  how far a flip moves the car (and whether it beats driving)
    ///     F5  how much road the camera actually shows ahead
    ///     F6  run all five, one after the other
    ///     F7  print the current tuning profile and the reasoning behind it
    ///     F8  put the car back on the start line
    ///     F9  toggle the on-screen readout
    ///
    /// HOW THE TESTS DRIVE
    /// Each test switches the player's input component off and writes into exactly the same
    /// DriveInput struct a human would. The car has no idea it is being tested, so the readings are
    /// the real behaviour and not a special case. Every run starts with a teleport that zeroes
    /// velocity and spin, so no reading can be polluted by the run before it.
    ///
    /// WHERE THE NUMBERS GO
    /// Every reading is appended to a CSV with the tuning profile name on the row. That is what
    /// makes "compare two setups on the same test" possible: run F6 on Balanced, swap the profile,
    /// run F6 again, then sort the CSV by profile.
    ///
    /// NOTE ON TEST SETTINGS
    /// The test parameters below live on this component and deliberately NOT in VehicleTuning. If
    /// they lived in the tuning profile, swapping profiles would also swap the test, and the two
    /// sets of readings would not be comparable. The test has to stay still for the comparison to
    /// mean anything.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleMeasurement : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private VehicleController car;
        [SerializeField] private VehicleJumpFlip jumpFlip;
        [SerializeField] private VehicleInput playerInput;
        [SerializeField] private ChaseCamera chaseCamera;

        [Header("Lab reference points (optional)")]
        [Tooltip("Where the car is put back to before each test. Falls back to wherever the car " +
                 "started when the scene loaded.")]
        [SerializeField] private Transform startPoint;

        [Tooltip("Open space for the turning circle test, so the car does not clip the wall or the " +
                 "cones mid-lap. Falls back to the start point.")]
        [SerializeField] private Transform turnTestPoint;

        [Tooltip("The wall for the crash recovery test. If this is empty, F3 is skipped.")]
        [SerializeField] private Transform wall;

        [Header("Test 1: stopping distance")]
        [Tooltip("Fraction of top speed that counts as 'at top speed' before we hit the brake. " +
                 "Never quite 1.0, because the acceleration curve reaches zero AT top speed so the " +
                 "last fraction takes forever.")]
        [Range(0.8f, 1f)]
        [SerializeField] private float brakeTestSpeedFraction = 0.98f;

        [Tooltip("Speed below which the car counts as stopped, m/s.")]
        [SerializeField] private float stoppedSpeed = 0.25f;

        [Header("Test 2: turning circle")]
        [Tooltip("Speed to hold while measuring the turning circle, m/s. This is the 'set speed' the " +
                 "reading is quoted at, so write it down next to the result.")]
        [SerializeField] private float turnTestSpeed = 10f;

        [Tooltip("Which way to turn. 1 = right, -1 = left. Should give the same answer either way, " +
                 "and if it does not, something is asymmetric and that is a bug.")]
        [SerializeField] private float turnTestSteer = 1f;

        [Header("Test 3: wall recovery")]
        [Tooltip("Speed to hit the wall at, m/s.")]
        [SerializeField] private float crashTestSpeed = 20f;

        [Tooltip("How far back from the wall to start the run up, metres. Needs to be long enough " +
                 "to reach the crash speed.")]
        [SerializeField] private float crashRunUp = 80f;

        [Tooltip("Forward speed the car has to reach again before it counts as 'driving usefully', " +
                 "m/s. This is a judgement call: set it to the speed at which the player is back in " +
                 "the game rather than merely moving.")]
        [SerializeField] private float usefulSpeed = 8f;

        [Tooltip("A single sharp drop in speed larger than this, in one physics step, means we hit " +
                 "something. At 120 Hz even a gentle wall impact is far bigger than this.")]
        [SerializeField] private float impactSpeedDrop = 3f;

        [Tooltip("How far to reverse away from the wall before turning round, metres.")]
        [SerializeField] private float recoveryBackOffDistance = 7f;

        [Tooltip("Give up on reversing after this long, seconds.")]
        [SerializeField] private float recoveryMaxReverseTime = 2f;

        [Tooltip("Use the flip during the recovery. Run the test both ways: if the flip does not " +
                 "measurably shorten the recovery, it is not doing the job it exists for.")]
        [SerializeField] private bool useFlipInRecovery = true;

        [Header("Test 4: flip distance")]
        [Tooltip("Speed to be doing when the flip fires, m/s. Set this to the same speed as the " +
                 "turning circle test so the two readings can be compared directly.")]
        [SerializeField] private float flipTestSpeed = 10f;

        [Tooltip("Flip direction in stick terms. (0,1) forward, (1,0) right, (0,-1) backwards.")]
        [SerializeField] private Vector2 flipTestDirection = new Vector2(0f, 1f);

        [Header("Test 5: camera sightline")]
        [Tooltip("How many samples to take up the middle of the screen when working out how far " +
                 "ahead the ground is visible. More is slightly more precise, 200 is plenty.")]
        [Range(20, 400)]
        [SerializeField] private int cameraSampleCount = 200;

        [Header("General")]
        [Tooltip("Abandon any single test stage after this long, seconds. Stops a broken setup from " +
                 "hanging the editor.")]
        [SerializeField] private float stageTimeout = 30f;

        [Tooltip("CSV file name. Written next to the project folder in the editor.")]
        [SerializeField] private string csvFileName = "vehicle_measurements.csv";

        [Tooltip("Developer readout in the corner of the screen. This is a debug overlay for tuning, " +
                 "NOT game UI. The real HUD is a separate job and lives somewhere else entirely.")]
        [SerializeField] private bool showDeveloperOverlay = false;

        // ---------------- internals ----------------

        private const string CsvHeader = "timestamp,profile,test,reading,value,unit,notes";

        private string _csvPath;
        private bool _csvReady;
        private bool _running;
        private string _currentTest = "none";
        private string _lastResultSummary = "no readings yet";

        private Vector3 _fallbackStartPosition;
        private Quaternion _fallbackStartRotation;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];
        private readonly List<Vector3> _pathSamples = new List<Vector3>(4096);

        // Results kept so later tests can refer to earlier ones (the camera test compares against
        // the real measured stopping distance if one has been taken this session).
        private float _measuredStoppingDistance = -1f;

        private VehicleTuning Tuning => car != null ? car.Tuning : null;

        private void Awake()
        {
            if (car == null)
            {
                Debug.LogError("[VehicleLab] No car assigned to VehicleMeasurement. " +
                               "Nothing can be measured.", this);
                enabled = false;
                return;
            }

            if (jumpFlip == null) jumpFlip = car.GetComponent<VehicleJumpFlip>();
            if (playerInput == null) playerInput = car.GetComponent<VehicleInput>();

            _fallbackStartPosition = car.transform.position;
            _fallbackStartRotation = car.transform.rotation;

            PrepareCsv();
        }

        private void Start()
        {
            Debug.Log(
                "[VehicleLab] Ready. F1 stopping distance | F2 turning circle | F3 wall recovery | " +
                "F4 flip distance | F5 camera road ahead | F6 run all | F7 profile summary | " +
                "F8 reset car | F9 overlay");

            LogProfileSummary();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Dev shortcuts are read straight off the keyboard device rather than through the action
            // map on purpose: they are lab tools, not game controls, and they should not appear in
            // the bindings the player sees.
            if (keyboard.f9Key.wasPressedThisFrame)
            {
                showDeveloperOverlay = !showDeveloperOverlay;
            }

            if (keyboard.f8Key.wasPressedThisFrame && !_running)
            {
                ResetCar(StartPose());
                Debug.Log("[VehicleLab] Car reset to the start line.");
            }

            if (keyboard.f7Key.wasPressedThisFrame)
            {
                LogProfileSummary();
            }

            // Nothing can be measured without a tuning profile, because every reading is quoted
            // against the numbers in it.
            if (_running || Tuning == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) StartCoroutine(RunSingle(MeasureStoppingDistance()));
            else if (keyboard.f2Key.wasPressedThisFrame) StartCoroutine(RunSingle(MeasureTurningCircle()));
            else if (keyboard.f3Key.wasPressedThisFrame) StartCoroutine(RunSingle(MeasureWallRecovery()));
            else if (keyboard.f4Key.wasPressedThisFrame) StartCoroutine(RunSingle(MeasureFlipDistance()));
            else if (keyboard.f5Key.wasPressedThisFrame) MeasureCameraRoadAhead();
            else if (keyboard.f6Key.wasPressedThisFrame) StartCoroutine(RunAll());
        }

        // ==================================================================
        // TEST RUNNERS
        // ==================================================================

        private IEnumerator RunSingle(IEnumerator test)
        {
            _running = true;
            SetPlayerInputEnabled(false);

            // try/finally so a test that throws or gets interrupted can never leave the car with
            // the player's controls switched off.
            try
            {
                yield return test;
            }
            finally
            {
                SetPlayerInputEnabled(true);
                _running = false;
                _currentTest = "none";
                if (car != null) car.Drive = default;
            }
        }

        private IEnumerator RunAll()
        {
            _running = true;
            SetPlayerInputEnabled(false);

            try
            {
                Debug.Log("[VehicleLab] ===== FULL RUN START =====");
                LogProfileSummary();

                yield return MeasureStoppingDistance();
                yield return Settle(0.5f);

                yield return MeasureTurningCircle();
                yield return Settle(0.5f);

                yield return MeasureWallRecovery();
                yield return Settle(0.5f);

                yield return MeasureFlipDistance();
                yield return Settle(0.5f);

                // Camera reading is taken at speed, because the camera pulls back as the car speeds
                // up. Measuring it parked would flatter it.
                yield return HoldSpeed(Tuning.topSpeed * brakeTestSpeedFraction, 6f);
                MeasureCameraRoadAhead();

                Debug.Log("[VehicleLab] ===== FULL RUN COMPLETE =====");
                Debug.Log($"[VehicleLab] CSV: {_csvPath}");
            }
            finally
            {
                SetPlayerInputEnabled(true);
                _running = false;
                _currentTest = "none";
                if (car != null) car.Drive = default;
            }
        }

        // ==================================================================
        // TEST 1: STOPPING DISTANCE FROM TOP SPEED
        // ==================================================================

        private IEnumerator MeasureStoppingDistance()
        {
            _currentTest = "Stopping distance";
            ResetCar(StartPose());
            yield return Settle(0.4f);

            float targetSpeed = Tuning.topSpeed * brakeTestSpeedFraction;

            // Phase 1: get up to speed.
            float accelerationTime = 0f;
            float elapsed = 0f;
            while (car.ForwardSpeed < targetSpeed && elapsed < stageTimeout)
            {
                car.Drive = new DriveInput { throttle = 1f };
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                accelerationTime = elapsed;
            }

            if (elapsed >= stageTimeout)
            {
                Debug.LogWarning(
                    $"[VehicleLab] Stopping distance: never reached {targetSpeed:0.0} m/s " +
                    $"(got to {car.ForwardSpeed:0.0}). Check the acceleration curve and the run up length.");
            }

            float entrySpeed = car.ForwardSpeed;
            Vector3 brakePoint = car.transform.position;

            // Phase 2: full brake, no throttle.
            float brakeTime = 0f;
            while (car.Speed > stoppedSpeed && brakeTime < stageTimeout)
            {
                car.Drive = new DriveInput { throttle = 0f, brake = 1f };
                yield return new WaitForFixedUpdate();
                brakeTime += Time.fixedDeltaTime;
            }

            float distance = FlatDistance(brakePoint, car.transform.position);
            float predicted = (entrySpeed * entrySpeed) / (2f * Tuning.brakeDeceleration);
            float error = predicted > 0.01f ? (distance - predicted) / predicted * 100f : 0f;

            _measuredStoppingDistance = distance;

            Report("Stopping distance", "Time to reach test speed", accelerationTime, "s",
                $"0 to {entrySpeed:0.0} m/s on full throttle");
            Report("Stopping distance", "Entry speed", entrySpeed, "m/s",
                $"{brakeTestSpeedFraction:P0} of the {Tuning.topSpeed:0.0} m/s top speed");
            Report("Stopping distance", "Braking distance", distance, "m",
                $"full brake from {entrySpeed:0.0} m/s, textbook value {predicted:0.00} m, " +
                $"difference {error:+0.0;-0.0}%");
            Report("Stopping distance", "Braking time", brakeTime, "s", "");

            _lastResultSummary =
                $"Stopping distance {distance:0.0} m from {entrySpeed:0.0} m/s";

            Debug.Log(
                $"[VehicleLab] LEVEL DESIGN NUMBER: leave at least {distance:0.0} m of clear " +
                $"sight before anything the player has to stop for. " +
                $"That is {distance / 5f:0.0} of the 5 m markers.");
        }

        // ==================================================================
        // TEST 2: MINIMUM TURNING CIRCLE WIDTH
        // ==================================================================

        private IEnumerator MeasureTurningCircle()
        {
            _currentTest = "Turning circle";

            CarPose pose = turnTestPoint != null
                ? new CarPose(turnTestPoint.position, turnTestPoint.rotation)
                : StartPose();

            ResetCar(pose);
            yield return Settle(0.4f);

            // Phase 1: reach the test speed in a straight line.
            float elapsed = 0f;
            while (car.ForwardSpeed < turnTestSpeed && elapsed < stageTimeout)
            {
                car.Drive = new DriveInput { throttle = 1f };
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            // Phase 2: full lock, holding the speed steady, until the car has come all the way round.
            _pathSamples.Clear();
            _pathSamples.Add(car.transform.position);

            float steer = Mathf.Sign(turnTestSteer);
            float turnedDegrees = 0f;
            Vector3 previousHeading = Flatten(car.transform.forward).normalized;
            float lapTime = 0f;
            float speedSum = 0f;
            int speedSamples = 0;

            // Signed, not absolute. Adding up the absolute change per step would let every tiny
            // wobble count towards the lap, so a noisy car would report a full circle early and the
            // measured width would come out too small.
            while (Mathf.Abs(turnedDegrees) < 360f && lapTime < stageTimeout)
            {
                car.Drive = new DriveInput
                {
                    // Simple proportional hold on the speed, so the reading is genuinely "at
                    // turnTestSpeed" rather than at whatever the car drifted to.
                    throttle = Mathf.Clamp((turnTestSpeed - car.Speed) * 0.8f, -1f, 1f),
                    steer = steer
                };

                yield return new WaitForFixedUpdate();
                lapTime += Time.fixedDeltaTime;

                Vector3 heading = Flatten(car.transform.forward).normalized;
                if (heading.sqrMagnitude > 0.0001f && previousHeading.sqrMagnitude > 0.0001f)
                {
                    turnedDegrees += Vector3.SignedAngle(previousHeading, heading, Vector3.up);
                    previousHeading = heading;
                }

                speedSum += car.Speed;
                speedSamples++;

                // Only record when the car has actually moved, which keeps the list small enough
                // for the widest-span search below.
                Vector3 position = car.transform.position;
                if (FlatDistance(position, _pathSamples[_pathSamples.Count - 1]) > 0.1f)
                {
                    _pathSamples.Add(position);
                }
            }

            float averageSpeed = speedSamples > 0 ? speedSum / speedSamples : 0f;
            float width = WidestSpan(_pathSamples);
            float radius = width * 0.5f;

            float predictedRadius = Tuning.PredictedTurnRadiusAt(averageSpeed);
            float steeringRadius = Tuning.SteeringRadiusAt(averageSpeed);
            float gripRadius = Tuning.GripLimitedRadiusAt(averageSpeed);
            bool gripLimited = gripRadius > steeringRadius;

            Report("Turning circle", "Test speed held", averageSpeed, "m/s",
                $"asked for {turnTestSpeed:0.0} m/s");
            Report("Turning circle", "Turning circle width", width, "m",
                $"widest span of one full lap at {averageSpeed:0.0} m/s, " +
                $"textbook value {predictedRadius * 2f:0.0} m");
            Report("Turning circle", "Turning radius", radius, "m", "");
            Report("Turning circle", "Time for a full lap", lapTime, "s", "");
            Report("Turning circle", "Limiting factor", gripLimited ? 1 : 0, "0=steering 1=grip",
                gripLimited
                    ? $"grip runs out first: steering would allow {steeringRadius:0.0} m but grip " +
                      $"needs {gripRadius:0.0} m, so the car understeers at full lock"
                    : $"steering lock runs out first: grip could hold {gripRadius:0.0} m but the " +
                      $"steering curve only asks for {steeringRadius:0.0} m, so the car is not sliding");

            _lastResultSummary = $"Turning circle {width:0.0} m wide at {averageSpeed:0.0} m/s";

            Debug.Log(
                $"[VehicleLab] LEVEL DESIGN NUMBER: at {averageSpeed:0.0} m/s this car needs a " +
                $"{width:0.0} m wide space to turn round. Any junction tighter than that has to be " +
                "taken slower.");
        }

        // ==================================================================
        // TEST 3: RECOVERY AFTER HITTING THE WALL
        // ==================================================================

        private IEnumerator MeasureWallRecovery()
        {
            _currentTest = "Wall recovery";

            if (wall == null)
            {
                Debug.LogWarning("[VehicleLab] Wall recovery skipped: no wall assigned.");
                yield break;
            }

            // Line the car up square to the wall, far enough back to reach the crash speed.
            Vector3 wallPosition = wall.position;
            Vector3 startPosition = StartPose().position;

            Vector3 approach = Flatten(wallPosition - startPosition);
            if (approach.sqrMagnitude < 0.01f) approach = Vector3.forward;
            approach.Normalize();

            Vector3 runUpStart = wallPosition - approach * crashRunUp;
            runUpStart.y = startPosition.y;

            ResetCar(new CarPose(runUpStart, Quaternion.LookRotation(approach, Vector3.up)));
            yield return Settle(0.4f);

            // Phase 1: build up to the crash speed and hold it, so the impact happens at the speed
            // the reading is quoted at rather than at whatever the run up happened to reach.
            float elapsed = 0f;
            float previousSpeed = 0f;
            bool hit = false;
            float impactSpeed = 0f;

            while (elapsed < stageTimeout)
            {
                car.Drive = new DriveInput
                {
                    throttle = Mathf.Clamp((crashTestSpeed - car.Speed) * 0.8f, -1f, 1f)
                };

                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float speed = car.Speed;

                // A drop this big in a single physics step can only be a collision.
                if (previousSpeed - speed > impactSpeedDrop && previousSpeed > crashTestSpeed * 0.5f)
                {
                    hit = true;
                    impactSpeed = previousSpeed;
                    break;
                }

                previousSpeed = speed;
            }

            if (!hit)
            {
                Debug.LogWarning(
                    "[VehicleLab] Wall recovery: never detected an impact. Check the wall position " +
                    "and that the run up is long enough.");
                yield break;
            }

            Vector3 impactPosition = car.transform.position;
            float recoveryTime = 0f;

            // Phase 2: back off. Full reverse with the wheel turned, which is what a player does.
            float reverseTime = 0f;
            bool flipUsed = false;

            while (reverseTime < recoveryMaxReverseTime
                   && FlatDistance(car.transform.position, impactPosition) < recoveryBackOffDistance)
            {
                car.Drive = new DriveInput { throttle = -1f, steer = 1f };

                if (useFlipInRecovery && jumpFlip != null && jumpFlip.CanFlipNow)
                {
                    // Flip backwards, away from the wall. This is exactly the situation the flip
                    // exists for, so the test uses it the way a player would.
                    jumpFlip.RequestFlip(new Vector2(0f, -1f));
                    flipUsed = true;
                }

                yield return new WaitForFixedUpdate();
                reverseTime += Time.fixedDeltaTime;
                recoveryTime += Time.fixedDeltaTime;
            }

            // Phase 3: turn round and get back up to a useful speed.
            float driveAwayTime = 0f;
            while (driveAwayTime < stageTimeout)
            {
                car.Drive = new DriveInput { throttle = 1f, steer = 1f };
                yield return new WaitForFixedUpdate();

                driveAwayTime += Time.fixedDeltaTime;
                recoveryTime += Time.fixedDeltaTime;

                bool drivingUsefully = car.IsGrounded
                                       && car.IsUpright
                                       && car.ForwardSpeed >= usefulSpeed;

                if (drivingUsefully) break;
            }

            Report("Wall recovery", "Impact speed", impactSpeed, "m/s", "");
            Report("Wall recovery", "Speed kept through impact", car.Speed, "m/s",
                "measured one physics step after contact");
            Report("Wall recovery", "Back off time", reverseTime, "s",
                $"full reverse until {recoveryBackOffDistance:0.0} m clear of the impact point" +
                (flipUsed ? ", flip used" : ", no flip used"));
            Report("Wall recovery", "Turn and rebuild speed time", driveAwayTime, "s",
                $"until forward speed reached {usefulSpeed:0.0} m/s");
            Report("Wall recovery", "Total recovery time", recoveryTime, "s",
                $"impact to driving usefully again, flip {(useFlipInRecovery ? "enabled" : "disabled")}");

            _lastResultSummary = $"Wall recovery {recoveryTime:0.00} s from {impactSpeed:0.0} m/s";

            Debug.Log(
                $"[VehicleLab] DESIGN NUMBER: a wall hit at {impactSpeed:0.0} m/s costs the player " +
                $"about {recoveryTime:0.0} s. Compare that against the delivery timer before " +
                "deciding whether a crash should be survivable or run-ending.");
        }

        // ==================================================================
        // TEST 4: HOW FAR THE FLIP MOVES THE CAR
        // ==================================================================

        private IEnumerator MeasureFlipDistance()
        {
            _currentTest = "Flip distance";

            if (jumpFlip == null)
            {
                Debug.LogWarning("[VehicleLab] Flip test skipped: no VehicleJumpFlip on the car.");
                yield break;
            }

            ResetCar(StartPose());
            yield return Settle(0.4f);

            // Get up to the test speed first. A flip from a standstill is not the interesting case,
            // the interesting case is a flip at the speed a player takes a corner at.
            if (flipTestSpeed > 0.1f)
            {
                float spinUp = 0f;
                while (car.ForwardSpeed < flipTestSpeed && spinUp < stageTimeout)
                {
                    car.Drive = new DriveInput { throttle = 1f };
                    yield return new WaitForFixedUpdate();
                    spinUp += Time.fixedDeltaTime;
                }
            }

            // Wait until the flip is genuinely available, so we are not measuring a refused flip.
            float waited = 0f;
            while (!jumpFlip.CanFlipNow && waited < stageTimeout)
            {
                car.Drive = new DriveInput
                {
                    throttle = Mathf.Clamp((flipTestSpeed - car.Speed) * 0.8f, -1f, 1f)
                };

                yield return new WaitForFixedUpdate();
                waited += Time.fixedDeltaTime;
            }

            Vector3 flipStart = car.transform.position;
            float speedBefore = car.Speed;
            int flipsBefore = jumpFlip.FlipCount;

            jumpFlip.RequestFlip(flipTestDirection);

            // Let it actually start.
            float startWait = 0f;
            while (jumpFlip.FlipCount == flipsBefore && startWait < 1f)
            {
                yield return new WaitForFixedUpdate();
                startWait += Time.fixedDeltaTime;
            }

            if (jumpFlip.FlipCount == flipsBefore)
            {
                Debug.LogWarning(
                    "[VehicleLab] Flip test: the flip never fired. Switch on 'Log Refusals' on the " +
                    "VehicleJumpFlip component to see why.");
                yield break;
            }

            FlipReport report = jumpFlip.LastFlip;

            // Measure until the move is over AND the car is back on its wheels, because a flip that
            // leaves the car in the air is not finished doing things to the car's position.
            float flightTime = 0f;
            float peakSpeed = speedBefore;
            while ((jumpFlip.IsFlipping || !car.IsGrounded) && flightTime < stageTimeout)
            {
                // No throttle during the flip. We are measuring the flip, not the engine.
                car.Drive = default;
                yield return new WaitForFixedUpdate();
                flightTime += Time.fixedDeltaTime;
                peakSpeed = Mathf.Max(peakSpeed, car.Speed);
            }

            float displacement = FlatDistance(flipStart, car.transform.position);

            // THE DESIGN RULE, CHECKED.
            // Over the same stretch of time, how far would the car have gone by simply driving at
            // the speed it already had? If the flip covers less ground, it cannot be a shortcut.
            float distanceIfDriving = speedBefore * flightTime;
            bool flipIsSlower = displacement <= distanceIfDriving;

            Report("Flip distance", "Speed before flip", speedBefore, "m/s", "");
            Report("Flip distance", "Speed straight after flip", report.speedAfter, "m/s",
                report.speedWasCapped
                    ? "speed governor cut the gain, direction kept"
                    : "under the gain cap, no capping needed");
            Report("Flip distance", "Speed gained", report.speedGain, "m/s",
                $"cap is {Tuning.flipMaxSpeedGain:0.0} m/s, and 0 above " +
                $"{Tuning.flipNoSpeedGainAboveSpeed:0.0} m/s");
            Report("Flip distance", "Peak speed during flip", peakSpeed, "m/s", "");
            Report("Flip distance", "Flip displacement", displacement, "m",
                $"flip force {report.requestedForce:0.0} m/s, cooldown {Tuning.flipCooldown:0.00} s");
            Report("Flip distance", "Flip duration", flightTime, "s",
                "from the flip firing to being back on the ground");
            Report("Flip distance", "Distance if driving instead", distanceIfDriving, "m",
                $"{speedBefore:0.0} m/s held for {flightTime:0.00} s");
            Report("Flip distance", "Flip is not a shortcut", flipIsSlower ? 1 : 0, "1=pass 0=fail",
                flipIsSlower
                    ? "PASS: the flip covers no more ground than driving would have"
                    : "FAIL: the flip out-runs driving. Raise flipCooldown, lower flipForce, or " +
                      "lower flipNoSpeedGainAboveSpeed");

            _lastResultSummary =
                $"Flip moved {displacement:0.0} m in {flightTime:0.00} s " +
                $"({(flipIsSlower ? "PASS" : "FAIL")})";

            if (flipIsSlower)
            {
                Debug.Log(
                    $"[VehicleLab] DESIGN RULE PASS: flip covered {displacement:0.0} m where driving " +
                    $"would have covered {distanceIfDriving:0.0} m. The flip is a recovery move, " +
                    "not a corner shortcut.");
            }
            else
            {
                Debug.LogWarning(
                    $"[VehicleLab] DESIGN RULE FAIL: flip covered {displacement:0.0} m but driving " +
                    $"would only have covered {distanceIfDriving:0.0} m. Players will flip through " +
                    "corners. Raise flipCooldown or lower flipForce.");
            }
        }

        // ==================================================================
        // TEST 5: HOW MUCH ROAD THE CAMERA SHOWS
        // ==================================================================

        /// <summary>
        /// Samples straight up the middle of the screen and asks, at each height, where that pixel
        /// lands on the ground. The furthest one is how far ahead the player can actually see.
        /// Then it compares that against the distance the car needs to stop.
        /// </summary>
        private void MeasureCameraRoadAhead()
        {
            _currentTest = "Camera road ahead";

            if (chaseCamera == null || chaseCamera.Camera == null)
            {
                Debug.LogWarning("[VehicleLab] Camera test skipped: no ChaseCamera assigned.");
                return;
            }

            Camera cam = chaseCamera.Camera;
            Vector3 carPosition = car.transform.position;
            Vector3 heading = Flatten(car.GroundForward);
            if (heading.sqrMagnitude < 0.0001f) heading = Flatten(car.transform.forward);
            heading.Normalize();

            float nearest = float.MaxValue;
            float furthest = float.MinValue;
            float horizonViewportY = 1f;
            int hits = 0;

            for (int i = 0; i < cameraSampleCount; i++)
            {
                float viewportY = i / (float)(cameraSampleCount - 1);
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, viewportY, 0f));

                if (!TryRaycast(ray.origin, ray.direction, 2000f, out RaycastHit hit))
                {
                    // First miss going up the screen is where the ground runs out and we are
                    // looking at sky. Everything above this shows no road at all.
                    if (hits > 0 && viewportY < horizonViewportY) horizonViewportY = viewportY;
                    continue;
                }

                float ahead = Vector3.Dot(hit.point - carPosition, heading);
                nearest = Mathf.Min(nearest, ahead);
                furthest = Mathf.Max(furthest, ahead);
                hits++;
            }

            if (hits == 0)
            {
                Debug.LogWarning("[VehicleLab] Camera test: the camera can see no ground at all.");
                return;
            }

            float currentSpeed = car.Speed;
            float stoppingFromNow = (currentSpeed * currentSpeed) / (2f * Tuning.brakeDeceleration);
            float stoppingFromTop = Tuning.PredictedStoppingDistanceFromTopSpeed();
            float reference = _measuredStoppingDistance > 0f ? _measuredStoppingDistance : stoppingFromTop;

            bool okForNow = furthest >= stoppingFromNow;
            bool okForTopSpeed = furthest >= reference;

            Report("Camera road ahead", "Speed when measured", currentSpeed, "m/s", "");
            Report("Camera road ahead", "Nearest visible ground", nearest, "m",
                "measured along the car's forward direction, negative means behind the car");
            Report("Camera road ahead", "Furthest visible ground", furthest, "m",
                $"road visible up the middle of the screen, horizon at {horizonViewportY:P0} screen height");
            Report("Camera road ahead", "Stopping distance at this speed", stoppingFromNow, "m", "");
            Report("Camera road ahead", "Stopping distance from top speed", reference, "m",
                _measuredStoppingDistance > 0f ? "measured this session" : "calculated from tuning");
            Report("Camera road ahead", "Enough road for this speed", okForNow ? 1 : 0, "1=pass 0=fail",
                okForNow ? "PASS" : "FAIL: the player cannot see far enough to stop");
            Report("Camera road ahead", "Enough road for top speed", okForTopSpeed ? 1 : 0, "1=pass 0=fail",
                okForTopSpeed
                    ? "PASS"
                    : $"FAIL: raise cameraHeight, cameraExtraPitch or cameraDistance until the " +
                      $"furthest visible ground clears {reference:0.0} m");

            _lastResultSummary =
                $"Camera shows {furthest:0.0} m ahead, needs {reference:0.0} m " +
                $"({(okForTopSpeed ? "PASS" : "FAIL")})";

            if (!okForTopSpeed)
            {
                Debug.LogWarning(
                    $"[VehicleLab] FAIRNESS FAIL: the camera shows {furthest:0.0} m of road but the " +
                    $"car needs {reference:0.0} m to stop. At top speed the player is being asked " +
                    "to react to things they cannot see yet.");
            }
        }

        // ==================================================================
        // PROFILE SUMMARY: which setup, and why
        // ==================================================================

        /// <summary>
        /// Prints the profile that is loaded, the headline numbers, and the written reasoning from
        /// the asset. This is what gets shown when someone asks "which setup did you pick and why".
        /// </summary>
        private void LogProfileSummary()
        {
            if (Tuning == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("[VehicleLab] ---- TUNING PROFILE ----");
            sb.AppendLine($"  Profile          : {Tuning.profileName}" +
                          (Tuning.isChosenSetup ? "   <-- marked as the chosen setup" : ""));
            sb.AppendLine($"  Asset            : {Tuning.name}");
            sb.AppendLine($"  Top speed        : {Tuning.topSpeed:0.0} m/s " +
                          $"({Tuning.topSpeed * 3.6f:0} km/h)");
            sb.AppendLine($"  Brake            : {Tuning.brakeDeceleration:0.0} m/s^2 " +
                          $"-> {Tuning.PredictedStoppingDistanceFromTopSpeed():0.0} m to stop");
            sb.AppendLine($"  Sideways grip    : {Tuning.lateralGripAcceleration:0.0} m/s^2");
            sb.AppendLine($"  Tightest corner  : {Tuning.PredictedTurnRadiusAt(turnTestSpeed):0.0} m " +
                          $"radius at {turnTestSpeed:0.0} m/s");
            sb.AppendLine($"  Flip             : force {Tuning.flipForce:0.0} m/s, " +
                          $"cooldown {Tuning.flipCooldown:0.00} s, " +
                          $"gain cap {Tuning.flipMaxSpeedGain:0.0} m/s");
            sb.AppendLine($"  Camera           : {Tuning.cameraDistance:0.0} m back, " +
                          $"{Tuning.cameraHeight:0.0} m up, " +
                          $"{Tuning.cameraLookAhead:0.0} m look-ahead, " +
                          $"{Tuning.cameraFollowSmoothTime:0.00} s smoothing");
            sb.AppendLine($"  Physics step     : {Tuning.physicsHz:0} Hz");
            sb.AppendLine($"  Why this setup   : " +
                          (string.IsNullOrWhiteSpace(Tuning.whyThisSetup)
                              ? "(nothing written yet, fill this in on the asset)"
                              : Tuning.whyThisSetup.Replace("\n", "\n                     ")));

            Debug.Log(sb.ToString());

            Report("Profile", "Top speed", Tuning.topSpeed, "m/s", Tuning.profileName);
            Report("Profile", "Brake deceleration", Tuning.brakeDeceleration, "m/s^2", Tuning.profileName);
            Report("Profile", "Sideways grip", Tuning.lateralGripAcceleration, "m/s^2", Tuning.profileName);
            Report("Profile", "Camera look ahead", Tuning.cameraLookAhead, "m", Tuning.profileName);
            Report("Profile", "Chosen setup", Tuning.isChosenSetup ? 1 : 0, "1=yes 0=no",
                Tuning.whyThisSetup);
        }

        // ==================================================================
        // PLUMBING
        // ==================================================================

        /// <summary>A place and a facing to drop the car at. Named CarPose so it is never confused
        /// with UnityEngine.Pose.</summary>
        private struct CarPose
        {
            public Vector3 position;
            public Quaternion rotation;

            public CarPose(Vector3 position, Quaternion rotation)
            {
                this.position = position;
                this.rotation = rotation;
            }
        }

        private CarPose StartPose()
        {
            return startPoint != null
                ? new CarPose(startPoint.position, startPoint.rotation)
                : new CarPose(_fallbackStartPosition, _fallbackStartRotation);
        }

        private void ResetCar(CarPose pose)
        {
            car.Teleport(pose.position, pose.rotation);
            if (chaseCamera != null) chaseCamera.SnapToTarget();
        }

        /// <summary>Sit still for a moment so the suspension settles before a reading starts.</summary>
        private IEnumerator Settle(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                car.Drive = default;
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
            }
        }

        /// <summary>Hold a speed for a while. Used to take the camera reading at speed.</summary>
        private IEnumerator HoldSpeed(float speed, float seconds)
        {
            ResetCar(StartPose());

            float t = 0f;
            while (t < seconds + stageTimeout)
            {
                car.Drive = new DriveInput
                {
                    throttle = Mathf.Clamp((speed - car.Speed) * 0.8f, -1f, 1f)
                };

                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;

                if (car.Speed >= speed * 0.98f && t > seconds) break;
            }
        }

        private void SetPlayerInputEnabled(bool value)
        {
            if (playerInput != null) playerInput.enabled = value;
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// The widest gap between any two points on the recorded path. For one full lap of a circle
        /// that is the diameter, which is exactly the width of space the car needs to turn round in.
        /// </summary>
        private static float WidestSpan(List<Vector3> points)
        {
            if (points.Count < 2) return 0f;

            // Comparing every point against every other point is fine for a normal lap, but a wide
            // circle at a high test speed can record thousands of points and this would then be
            // millions of comparisons in a single frame. Thinning the list keeps the answer to well
            // within a few centimetres and keeps the editor responsive.
            const int maxPoints = 1200;
            int stride = Mathf.Max(1, points.Count / maxPoints);

            float widest = 0f;
            for (int i = 0; i < points.Count; i += stride)
            {
                for (int j = i + stride; j < points.Count; j += stride)
                {
                    float d = FlatDistance(points[i], points[j]);
                    if (d > widest) widest = d;
                }
            }
            return widest;
        }

        /// <summary>Raycast that ignores the car itself, the same way the wheels do.</summary>
        private bool TryRaycast(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
        {
            best = default;
            bool found = false;
            float bestDistance = float.MaxValue;

            int count = Physics.RaycastNonAlloc(origin, direction, _hitBuffer, distance,
                Tuning != null ? Tuning.groundMask.value : ~0, QueryTriggerInteraction.Ignore);

            Rigidbody carBody = car != null ? car.Body : null;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (carBody != null && hit.collider.attachedRigidbody == carBody) continue;
                if (hit.distance >= bestDistance) continue;

                bestDistance = hit.distance;
                best = hit;
                found = true;
            }

            return found;
        }

        // ==================================================================
        // OUTPUT
        // ==================================================================

        /// <summary>
        /// One labelled reading, to the console and to the CSV. Every reading goes through here, so
        /// the console and the file can never disagree with each other.
        /// </summary>
        private void Report(string test, string reading, float value, string unit, string notes)
        {
            string profile = Tuning != null ? Tuning.profileName : "no profile";

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[VehicleLab] {0,-18} | {1,-32} = {2,10:0.000} {3,-12} {4}",
                test, reading, value, unit, string.IsNullOrEmpty(notes) ? "" : "| " + notes));

            AppendCsvRow(profile, test, reading, value, unit, notes);
        }

        private void PrepareCsv()
        {
            try
            {
                // In the editor this lands next to the project folder, which is the easiest place
                // to find it and drop it into a spreadsheet.
                string directory = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", "VehicleLab_Measurements"));

                Directory.CreateDirectory(directory);
                _csvPath = Path.Combine(directory, csvFileName);

                if (!File.Exists(_csvPath))
                {
                    File.WriteAllText(_csvPath, CsvHeader + "\n");
                }

                _csvReady = true;
                Debug.Log($"[VehicleLab] Readings are being appended to: {_csvPath}");
            }
            catch (System.Exception e)
            {
                // Catching broadly on purpose: writing next to the project can fail for a whole
                // family of reasons (read only folder, no permission, path too long) and none of
                // them should stop the lab from running. Fall back to the per-user data folder.
                try
                {
                    string directory = Path.Combine(Application.persistentDataPath, "VehicleLab");
                    Directory.CreateDirectory(directory);
                    _csvPath = Path.Combine(directory, csvFileName);

                    if (!File.Exists(_csvPath)) File.WriteAllText(_csvPath, CsvHeader + "\n");

                    _csvReady = true;
                    Debug.Log($"[VehicleLab] Readings are being appended to: {_csvPath}");
                }
                catch (System.Exception inner)
                {
                    _csvReady = false;
                    Debug.LogWarning(
                        $"[VehicleLab] Could not open a CSV file ({e.Message} / {inner.Message}). " +
                        "Readings will still appear in the console.");
                }
            }
        }

        private void AppendCsvRow(string profile, string test, string reading, float value,
            string unit, string notes)
        {
            if (!_csvReady) return;

            try
            {
                // InvariantCulture matters: on a machine set to a locale that uses a comma for the
                // decimal point, "14,31" would split into two columns and quietly ruin the file.
                string row = string.Join(",",
                    Csv(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    Csv(profile),
                    Csv(test),
                    Csv(reading),
                    value.ToString("0.0000", CultureInfo.InvariantCulture),
                    Csv(unit),
                    Csv(notes));

                File.AppendAllText(_csvPath, row + "\n");
            }
            catch (System.Exception e)
            {
                // A reading that reached the console is still a reading. Never let a file problem
                // throw in the middle of a test.
                Debug.LogWarning($"[VehicleLab] Could not write to the CSV: {e.Message}");
            }
        }

        /// <summary>Quotes a field so commas, quotes and newlines inside it cannot break the file.</summary>
        private static string Csv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "\"\"";
            return "\"" + field.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        // ==================================================================
        // DEVELOPER OVERLAY
        // Debug text for tuning. This is not the game HUD, and it is off by default.
        // ==================================================================

        private GUIStyle _overlayStyle;

        private void OnGUI()
        {
            if (!showDeveloperOverlay || car == null || Tuning == null) return;

            if (_overlayStyle == null)
            {
                _overlayStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(10, 10, 8, 8),
                    richText = false
                };
            }

            var sb = new StringBuilder();
            sb.AppendLine($"PROFILE  {Tuning.profileName}");
            sb.AppendLine($"speed    {car.Speed,6:0.0} m/s   ({car.Speed * 3.6f,5:0} km/h)");
            sb.AppendLine($"forward  {car.ForwardSpeed,6:0.0} m/s");
            sb.AppendLine($"sideways {car.LateralSpeed,6:0.0} m/s   <- this is the slide");
            sb.AppendLine($"steer    {car.SteerAmount,6:0.00}");
            sb.AppendLine($"yaw rate {car.YawRate,6:0.00} rad/s");
            sb.AppendLine($"wheels   {car.WheelsOnGround} of 4 on the ground");
            sb.AppendLine($"upright  {car.Uprightness,6:0.00}");

            if (jumpFlip != null)
            {
                sb.AppendLine($"flip     {(jumpFlip.CanFlipNow ? "ready" : $"cooling {jumpFlip.FlipCooldownRemaining:0.00}s")}" +
                              $"   count {jumpFlip.FlipCount}");
            }

            sb.AppendLine();
            sb.AppendLine($"test     {_currentTest}");
            sb.AppendLine($"last     {_lastResultSummary}");
            sb.AppendLine();
            sb.AppendLine("F1 stop  F2 turn  F3 wall  F4 flip  F5 camera");
            sb.AppendLine("F6 all   F7 profile  F8 reset  F9 hide");

            var area = new Rect(10f, 10f, 470f, 320f);
            GUI.Box(area, GUIContent.none);
            GUI.Label(area, sb.ToString(), _overlayStyle);
        }
    }
}
