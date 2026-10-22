using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>Which technique, if any, is currently running.</summary>
    public enum CrazyTechnique
    {
        None = 0,
        Dash = 1,
        Drift = 2,
        Stop = 3,
        LimitCut = 4
    }

    /// <summary>
    /// Crazy Taxi's four gear-flick techniques, rebuilt on this car.
    ///
    /// WHAT IS FAITHFUL AND WHAT IS OURS
    /// The inputs and the behaviour are Crazy Taxi's, documented by players and press and sourced in
    /// docs/research/CRAZY-TAXI-CONTROLLER-RESEARCH.md:
    ///
    ///   Crazy Dash   release accelerator -> select Drive -> press accelerator.
    ///                A burst above normal floored speed. From a standstill it launches as though
    ///                traction were complete instead of spinning the wheels up.
    ///   Crazy Drift  release accelerator -> tap Reverse then Drive, while steering.
    ///                Slides through a tight corner, rotating the car rather than scrubbing speed.
    ///   Crazy Stop   release everything during a drift; brake as well to stop sooner.
    ///   Limit Cut    release both pedals -> Reverse, Drive and accelerator together.
    ///                Speed past the normal ceiling. Chained with the dash in expert play.
    ///
    /// The MAGNITUDES are not Crazy Taxi's, because Sega never published them. Every number lives in
    /// VehicleTuning under "Crazy techniques" with its reasoning attached, and the defaults are
    /// hypotheses sized against values this car already has. They are meant to be driven by
    /// VehicleMeasurement and changed. Claiming otherwise would be inventing a reference.
    ///
    /// WHY THE STEPS MUST BE SEQUENTIAL
    /// Guides are explicit that mashing the inputs simultaneously FAILS and that a minuscule gap
    /// between them is required. That is not an implementation detail, it is the skill: the move has
    /// a rhythm, and a version that fired on a simultaneous press would be a button rather than a
    /// technique. tuning.techniqueMinimumGap enforces the gap and tuning.techniqueInputWindow
    /// enforces the deadline.
    ///
    /// HOW IT REACHES THE PHYSICS
    /// Almost entirely from outside, as force and torque, the same way VehicleController.ApplyBoost
    /// applies its own push and respects its own ceiling. The single exception is the drift's rear
    /// grip, which is a multiplier the controller folds in at the one place it already splits grip
    /// per axle - see VehicleController.TechniqueRearGripMultiplier for why that one needs a hook.
    ///
    /// Nothing here changes a handling value. With tuning.techniquesEnabled off the car drives
    /// exactly as it did, which is what lets the layer be A/B tested on one build.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    public class CrazyTechniques : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The car. Found on this object if left empty.")]
        [SerializeField] private VehicleController controller;

        [Header("Debug")]
        [Tooltip("Log every recognised technique to the console. Noisy, but it is the only way to " +
                 "tell a failed input from an input that was never attempted, and the game has no " +
                 "audio to confirm a successful move with.")]
        [SerializeField] private bool logTechniques = false;

        // Timestamps, all Time.time. Negative infinity means "never happened", which keeps every
        // window comparison below honest on the first frame without a special case.
        private float _acceleratorReleasedAt = float.NegativeInfinity;
        private float _gearToDriveAt = float.NegativeInfinity;
        private float _gearToReverseAt = float.NegativeInfinity;
        private bool _acceleratorWasDown;
        private Gear _lastGear = Gear.Drive;

        private CrazyTechnique _active = CrazyTechnique.None;
        private float _activeUntil;
        private float _nextDashAllowedAt;
        private float _driftSteerSign;

        /// <summary>Which technique is running, or None.</summary>
        public CrazyTechnique Active => _active;

        /// <summary>Seconds left on the running technique, 0 when none.</summary>
        public float TimeRemaining => _active == CrazyTechnique.None
            ? 0f
            : Mathf.Max(0f, _activeUntil - Time.time);

        /// <summary>Lifetime counts, for the HUD and the session log.</summary>
        public int DashCount { get; private set; }
        public int DriftCount { get; private set; }
        public int StopCount { get; private set; }
        public int LimitCutCount { get; private set; }

        /// <summary>
        /// Total techniques performed. The headline number for a HUD: Crazy Taxi confirms a
        /// successful dash with an engine note, and this project has no audio at all, so a
        /// technique that lands has to be visible or it cannot be learned.
        /// </summary>
        public int TotalTechniques => DashCount + DriftCount + StopCount + LimitCutCount;

        private VehicleTuning Tuning => controller != null ? controller.Tuning : null;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<VehicleController>();
        }

        private void OnDisable()
        {
            // Hand the car back exactly as it was found. Without this, disabling the component
            // mid-drift would leave the rear axle permanently loosened.
            _active = CrazyTechnique.None;
            if (controller != null) controller.TechniqueRearGripMultiplier = 1f;
        }

        private void FixedUpdate()
        {
            VehicleTuning t = Tuning;
            if (controller == null || t == null) return;

            if (!t.techniquesEnabled)
            {
                if (_active != CrazyTechnique.None) EndTechnique();
                return;
            }

            TrackInputEdges(t);
            Recognise(t);
            ApplyActive(t);
        }

        /// <summary>
        /// Records when the accelerator was let go and when each gear position was selected.
        ///
        /// The accelerator is read as the magnitude of Drive.throttle rather than from VehicleInput
        /// directly, so the techniques work for anything that drives the car - a rebound input
        /// scheme, a replay, or a future AI driver - rather than only for a human on this frame's
        /// keyboard.
        /// </summary>
        private void TrackInputEdges(VehicleTuning t)
        {
            float now = Time.time;

            bool down = Mathf.Abs(controller.Drive.throttle) > t.inputDeadzone;
            if (_acceleratorWasDown && !down) _acceleratorReleasedAt = now;
            _acceleratorWasDown = down;

            Gear gear = controller.Drive.gear;
            if (gear != _lastGear)
            {
                if (gear == Gear.Drive) _gearToDriveAt = now;
                else _gearToReverseAt = now;
                _lastGear = gear;
            }
        }

        private bool Within(float timestamp, float window, float now)
        {
            float age = now - timestamp;
            return age >= 0f && age <= window;
        }

        private void Recognise(VehicleTuning t)
        {
            float now = Time.time;

            // A running technique is not interrupted by the inputs that would start another, except
            // that a drift can be ended early by a crazy stop and extended by re-tapping.
            if (_active == CrazyTechnique.Drift && t.crazyStopEnabled && IsCrazyStopInput(t))
            {
                Begin(CrazyTechnique.Stop, t.crazyStopDuration);
                StopCount++;
                Log("Crazy Stop");
                return;
            }

            if (_active != CrazyTechnique.None && now < _activeUntil) return;
            if (_active != CrazyTechnique.None) EndTechnique();

            bool reverseThenDrive =
                Within(_gearToDriveAt, t.techniqueInputWindow, now) &&
                _gearToDriveAt - _gearToReverseAt >= t.techniqueMinimumGap &&
                _gearToDriveAt - _gearToReverseAt <= t.techniqueInputWindow;

            bool acceleratorJustPressed =
                _acceleratorWasDown &&
                Within(_gearToDriveAt, t.techniqueInputWindow, now) &&
                now - _gearToDriveAt >= t.techniqueMinimumGap;

            bool liftedBeforeGear =
                _gearToDriveAt - _acceleratorReleasedAt >= t.techniqueMinimumGap &&
                _gearToDriveAt - _acceleratorReleasedAt <= t.techniqueInputWindow;

            bool steering = Mathf.Abs(controller.Drive.steer) > 0.3f;

            // Limit Cut before Dash: it is the same tail - gear reaches Drive, accelerator follows -
            // with a Reverse tap in front of it, so testing the dash first would swallow every
            // limit cut. The reverse tap is the harder input and earns the higher ceiling.
            if (t.limitCutEnabled && acceleratorJustPressed && liftedBeforeGear && reverseThenDrive && !steering)
            {
                Begin(CrazyTechnique.LimitCut, t.limitCutDuration);
                LimitCutCount++;
                Log("Limit Cut");
                return;
            }

            // Crazy Drift fires on the gear arriving in Drive while steering, NOT on a following
            // accelerator press: the move is a mid-corner flick and waiting for the throttle would
            // make it unperformable at the moment it is wanted.
            if (t.crazyDriftEnabled && reverseThenDrive && steering && !_acceleratorWasDown)
            {
                _driftSteerSign = Mathf.Sign(controller.Drive.steer);
                Begin(CrazyTechnique.Drift, t.crazyDriftDuration);
                DriftCount++;
                Log("Crazy Drift");
                return;
            }

            if (t.dashEnabled && now >= _nextDashAllowedAt && acceleratorJustPressed && liftedBeforeGear)
            {
                Begin(CrazyTechnique.Dash, t.dashDuration);
                _nextDashAllowedAt = now + t.dashDuration + t.dashCooldown;
                DashCount++;
                Log("Crazy Dash");
            }
        }

        /// <summary>
        /// A crazy stop is the absence of input during a drift: everything released, with the brake
        /// making it sharper rather than being required. Documented as letting go of all the buttons
        /// mid-drift, and pressing brake as well to stop faster.
        /// </summary>
        private bool IsCrazyStopInput(VehicleTuning t)
        {
            return !_acceleratorWasDown || controller.Drive.brake > 0.5f;
        }

        private void Begin(CrazyTechnique technique, float duration)
        {
            _active = technique;
            _activeUntil = Time.time + Mathf.Max(0.01f, duration);
        }

        private void EndTechnique()
        {
            _active = CrazyTechnique.None;
            controller.TechniqueRearGripMultiplier = 1f;
        }

        private void ApplyActive(VehicleTuning t)
        {
            if (_active == CrazyTechnique.None) return;

            if (Time.time >= _activeUntil)
            {
                EndTechnique();
                return;
            }

            // Surface scaling. Crazy Taxi has one surface and we have seven; letting a dash on grass
            // push as hard as a dash on tarmac would make the traction model we already built
            // irrelevant. Off reproduces the original more literally.
            float surface = t.techniquesRespectSurface ? controller.Surface.driveMultiplier : 1f;

            switch (_active)
            {
                case CrazyTechnique.Dash:
                    PushForward(t.dashAcceleration * surface, t.dashSpeedCeiling, t);
                    ApplyLaunchTraction(t, surface);
                    break;

                case CrazyTechnique.LimitCut:
                    PushForward(t.limitCutAcceleration * surface, t.limitCutSpeedCeiling, t);
                    break;

                case CrazyTechnique.Drift:
                    ApplyDrift(t, surface);
                    break;

                case CrazyTechnique.Stop:
                    ApplyStop(t);
                    break;
            }
        }

        /// <summary>
        /// Adds the technique's push, but only while below its own ceiling.
        ///
        /// Mirrors ApplyBoost deliberately: a push that kept applying at the ceiling would burn the
        /// technique's duration on nothing the player can see, and the ceiling is the whole point of
        /// a move whose purpose is exceeding top speed.
        /// </summary>
        private void PushForward(float acceleration, float ceiling, VehicleTuning t)
        {
            if (!controller.IsGrounded) return;
            if (controller.ForwardSpeed >= ceiling) return;

            controller.Body.AddForce(
                controller.GroundForward * acceleration, ForceMode.Acceleration);
        }

        /// <summary>
        /// Replaces the low-speed part of the acceleration curve during a standing-start dash.
        ///
        /// The curve exists so a stationary car does not simply leap forward - it models the wheels
        /// spinning up. Crazy Taxi's dash from a stop is described as converting that energy as
        /// though traction were complete, so the technique's job here is to pay back exactly the
        /// amount the curve withheld, no more.
        /// </summary>
        private void ApplyLaunchTraction(VehicleTuning t, float surface)
        {
            if (!controller.IsGrounded) return;
            if (t.dashLaunchTraction <= 0f) return;
            if (Mathf.Abs(controller.ForwardSpeed) > t.dashLaunchSpeedThreshold) return;

            float curved = t.AccelerationAt(controller.ForwardSpeed);
            float shortfall = t.launchAcceleration - curved;
            if (shortfall <= 0f) return;

            controller.Body.AddForce(
                controller.GroundForward * (shortfall * t.dashLaunchTraction * surface),
                ForceMode.Acceleration);
        }

        /// <summary>
        /// The gear-flick slide: loosen the rear, rotate the car, and charge a fraction of the
        /// handbrake's drag for it.
        ///
        /// The rotation is applied as torque rather than by raising the steering's commanded yaw
        /// rate, because the steering COMMANDS a rate and would simply haul the car back to it -
        /// the same reason VehicleController backs steering authority off during a normal drift.
        /// Torque adds rotation the tyres then have to argue with, which is what makes the slide
        /// catchable instead of scripted.
        /// </summary>
        private void ApplyDrift(VehicleTuning t, float surface)
        {
            controller.TechniqueRearGripMultiplier = t.crazyDriftRearGripMultiplier;

            if (!controller.IsGrounded) return;

            if (!Mathf.Approximately(t.crazyDriftYawBoost, 0f))
            {
                controller.Body.AddTorque(
                    Vector3.up * (t.crazyDriftYawBoost * _driftSteerSign), ForceMode.Acceleration);
            }

            // A fraction of the handbrake's drag. Not none: a slide that cost nothing would be the
            // strictly best way to take every corner, and the technique would stop being a choice.
            float drag = t.handbrakeDeceleration * t.crazyDriftDragFraction * surface;
            if (drag > 0f && Mathf.Abs(controller.ForwardSpeed) > 0.1f)
            {
                controller.Body.AddForce(
                    controller.GroundForward * (-Mathf.Sign(controller.ForwardSpeed) * drag),
                    ForceMode.Acceleration);
            }
        }

        /// <summary>
        /// The near-instant stop. Capped so it can only ever bring the car to rest, never drag it
        /// backwards - the same guard ApplyDriveAndBrake uses, for the same reason: a brake that
        /// overshoots zero reads as the physics being unreliable.
        /// </summary>
        private void ApplyStop(VehicleTuning t)
        {
            float speed = Mathf.Abs(controller.ForwardSpeed);
            if (speed < 0.1f)
            {
                EndTechnique();
                return;
            }

            float maxStopping = speed / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            float decel = Mathf.Min(t.crazyStopDeceleration, maxStopping);

            controller.Body.AddForce(
                controller.GroundForward * (-Mathf.Sign(controller.ForwardSpeed) * decel),
                ForceMode.Acceleration);
        }

        private void Log(string name)
        {
            if (!logTechniques) return;
            Debug.Log($"[CrazyTechniques] {name} at {controller.ForwardSpeed:0.0} m/s", this);
        }
    }
}
