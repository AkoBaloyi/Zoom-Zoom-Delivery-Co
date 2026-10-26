using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Vehicle
{
    public partial class VehicleController
    {
        private struct TimedCommand
        {
            public TaxiCommand command;
            public double time;
        }
        private readonly Queue<TimedCommand> _taxiCommands = new Queue<TimedCommand>();
        private TaxiTrickRecognizer _tricks;
        private float _trickRemaining, _dashWindow, _dashCeiling, _driftSign, _exitGripRemaining;
        private float _rightingDelay;
        private bool _taxiRecoveryRequested;
        private float _spinCooldownRemaining, _spinSpeed, _spinSign;
        private Vector3 _spinTargetForward;
        private float _cargoProtectionRemaining;
        public CrazyTaxiSettings Taxi => tuning.taxi ?? (tuning.taxi = new CrazyTaxiSettings());
        public TaxiTrick ActiveTrick { get; private set; }
        public TaxiGear SelectedGear { get; private set; }
        public int TrickCount { get; private set; }
        public int SelfRightCount { get; private set; }
        public bool IsTaxiDrifting => ActiveTrick == TaxiTrick.Drift || ActiveTrick == TaxiTrick.BackDrift;
        public float TrickRemaining => _trickRemaining;
        public float QuickSpinCooldownRemaining => _spinCooldownRemaining;
        public bool IsTechniqueCargoProtected => ActiveTrick != TaxiTrick.None || _cargoProtectionRemaining > 0f;
        public bool CanQuickSpinNow => tuning != null && Taxi.quickSpinEnabled && IsGrounded && IsUpright && _spinCooldownRemaining <= 0f;
        public event Action<TaxiTrick> TrickPerformed;
        public event Action Repositioned;
        public event Action<int, int> GearShifted;
        public event Action<float> Recovered;
        private readonly TaxiAutomaticGearbox _gearbox = new TaxiAutomaticGearbox();
        public int ForwardGear => _gearbox.Gear;
        public float EngineRpm => _gearbox.Rpm;
        public float EngineDemand => Mathf.Clamp01(Mathf.Max(Mathf.Abs(Drive.throttle), Drive.engineThrottle));
        public bool IsFreeRevving => _gearbox.IsFreeRevving;
        public bool IsAtRevLimiter => _gearbox.IsAtRevLimiter;
        public float RevLimiterGain => _gearbox.RevLimiterGain;
        public bool IsGearShifting => _gearbox.ShiftRemaining > 0f;
        public float GearShiftProgress => _gearbox.ShiftProgress;
        public float GearThrustEnvelope => _gearbox.ThrustEnvelope(Taxi);
        public int ForwardShiftCount => _gearbox.ShiftCount;
        public float AppliedDriveAcceleration { get; private set; }

        private void InitialiseTaxi()
        {
            Taxi.Sanitize();
            _tricks = new TaxiTrickRecognizer(Taxi.inputWindow, Taxi.minimumGap);
            var oldMoves = GetComponent<VehicleJumpFlip>();
            if (oldMoves != null) oldMoves.enabled = false;
            if (GetComponent<CrazyTaxiFeedback>() == null) gameObject.AddComponent<CrazyTaxiFeedback>();
            if (GetComponent<TaxiEngineAudio>() == null) gameObject.AddComponent<TaxiEngineAudio>();
            if (GetComponent<TaxiGearHaptics>() == null) gameObject.AddComponent<TaxiGearHaptics>();
            if (gameObject.scene.name == "VehicleLab" && GetComponent<VehicleLabInputMeter>() == null)
                gameObject.AddComponent<VehicleLabInputMeter>();
            ResetTaxi();
        }

        public void QueueTaxiCommand(TaxiCommand command, double eventTime)
        {
            // A paused menu must not build up a launch sequence for the next unpaused frame.
            if (Time.timeScale <= 0f || !isActiveAndEnabled) return;
            if (_taxiCommands.Count >= 128) ClearTaxiInput();
            _taxiCommands.Enqueue(new TimedCommand { command = command, time = eventTime });
        }

        public void ClearTaxiInput()
        {
            _taxiCommands.Clear();
            _gearbox.Reset(Taxi.engineIdleRpm);
            AppliedDriveAcceleration = 0f;
            _tricks?.Reset();
            SelectedGear = TaxiGear.Drive;
            Drive = default;
            ActiveTrick = TaxiTrick.None;
            _trickRemaining = _dashWindow = _exitGripRemaining = 0f;
            _cargoProtectionRemaining = 0f;
        }

        public void RequestTaxiRecovery() => _taxiRecoveryRequested = true;

        private void ResetTaxi()
        {
            ClearTaxiInput();
            _dashCeiling = Taxi.dashSpeed;
            _rightingDelay = 0f;
            _taxiRecoveryRequested = false;
            _spinCooldownRemaining = 0f;
            _hasRecoveryPoint = false;
            _safeSampleRemaining = 0f;
            _monitoringStuck = false;
            SeedRecoveryPoint();
            Repositioned?.Invoke();
        }

        private void BeginTaxiTrick(TaxiTrick trick)
        {
            ActiveTrick = trick;
            _trickRemaining = Taxi.dashDuration;
            if (trick == TaxiTrick.Drift || trick == TaxiTrick.BackDrift)
            {
                _trickRemaining = Taxi.driftDuration;
                _driftSign = Mathf.Sign(Drive.steer);
            }
            else if (trick == TaxiTrick.Dash || trick == TaxiTrick.LimiterCut)
            {
                bool exitingDrift = Mathf.Abs(LateralSpeed) > 2f;
                _dashCeiling = trick == TaxiTrick.LimiterCut
                    ? Mathf.Min(Taxi.limiterMaximumSpeed, Mathf.Max(Taxi.dashSpeed, _dashCeiling) + Taxi.limiterSpeedStep)
                    : Taxi.dashSpeed;
                _dashWindow = Taxi.dashDuration + Taxi.dashChainGraceTime;
                _exitGripRemaining = exitingDrift ? Taxi.driftExitGripTime : 0f;
            }
            else if (trick == TaxiTrick.BackDash)
            {
                Vector3 normalVelocity = Vector3.Project(_rb.linearVelocity, GroundNormal);
                float retreatSpeed = Mathf.Min(Taxi.backDashSpeed, Mathf.Max(6f, Mathf.Abs(ForwardSpeed)));
                _rb.linearVelocity = normalVelocity - GroundForward * retreatSpeed;
                UpdateLocalVelocity();
            }
            else if (trick == TaxiTrick.QuickSpin)
            {
                _spinSign = Mathf.Sign(Drive.steer);
                _spinTargetForward = Quaternion.AngleAxis(180f * _spinSign, GroundNormal) * GroundForward;
                _spinSpeed = Vector3.ProjectOnPlane(_rb.linearVelocity, GroundNormal).magnitude
                    * Taxi.quickSpinMomentumRetention;
                _trickRemaining = Taxi.quickSpinDuration;
                _spinCooldownRemaining = Taxi.quickSpinDuration + Taxi.quickSpinCooldown;
            }
            TrickCount++;
            TrickPerformed?.Invoke(trick);
        }

        private void StepTaxi(float dt)
        {
            if (_tricks == null) _tricks = new TaxiTrickRecognizer(Taxi.inputWindow, Taxi.minimumGap);
            _tricks.Configure(Taxi.inputWindow, Taxi.minimumGap);
            _trickRemaining = Mathf.Max(0f, _trickRemaining - dt);
            _dashWindow = Mathf.Max(0f, _dashWindow - dt);
            _exitGripRemaining = Mathf.Max(0f, _exitGripRemaining - dt);
            _spinCooldownRemaining = Mathf.Max(0f, _spinCooldownRemaining - dt);
            _cargoProtectionRemaining = Mathf.Max(0f, _cargoProtectionRemaining - dt);
            if (_trickRemaining <= 0f) ActiveTrick = TaxiTrick.None;
            // Opposite-direction W/S can be a logical brake without both physical pedals being
            // pressed. Only the input adapter's two-pedal flag requests free revving.
            bool freeRev = Drive.freeRev;

            while (_taxiCommands.Count > 0)
            {
                TimedCommand input = _taxiCommands.Dequeue();
                if (input.command == TaxiCommand.QuickSpin && (!CanQuickSpinNow ||
                    Mathf.Abs(Drive.steer) < Taxi.trickSteerThreshold)) continue;
                TaxiTrick trick = _tricks.Push(input.command, input.time, IsGrounded && IsUpright,
                    ForwardSpeed >= Taxi.driftMinimumSpeed, Mathf.Abs(Drive.steer) >= Taxi.trickSteerThreshold,
                    _dashWindow > 0f && ForwardSpeed > Taxi.cruiseSpeed * 0.8f);
                if (trick == TaxiTrick.Dash && Mathf.Abs(ForwardSpeed) > Taxi.dashMaximumEntrySpeed && !IsTaxiDrifting)
                    trick = TaxiTrick.None;
                if (trick != TaxiTrick.None && (!freeRev || trick == TaxiTrick.Stop || trick == TaxiTrick.QuickSpin))
                    BeginTaxiTrick(trick);
            }
            if (ActiveTrick != TaxiTrick.None) _cargoProtectionRemaining = Taxi.techniqueCargoGraceTime;
            SelectedGear = Drive.throttle < 0f || ForwardSpeed < -Taxi.reverseEngageSpeed ? TaxiGear.Reverse : TaxiGear.Drive;

            BoostRemaining = 0f;
            IsBoosting = IsSupersonic = IsEarningBoost = false;
            BoostEarnedThisStep = TotalBoostEarned = 0f;
            float steerTarget = Mathf.Clamp(Drive.steer, -1f, 1f);
            SteerAmount = Mathf.MoveTowards(SteerAmount, steerTarget, Taxi.steeringResponse * dt);
            IsDrifting = IsGrounded && (IsTaxiDrifting || (Speed >= Taxi.driftMinimumSpeed && SlipAngle >= 12f));
            LongitudinalDemand = AvailableLateralGrip = 0f;

            RecoverTaxi(dt);
            AppliedDriveAcceleration = 0f;
            int previousGear = _gearbox.Gear;
            int previousShiftCount = _gearbox.ShiftCount;
            float gearThrust = _gearbox.Step(Taxi, ForwardSpeed, EngineDemand,
                Drive.throttle < 0f || ForwardSpeed < -Taxi.reverseEngageSpeed, IsGrounded, dt, freeRev);
            if (_gearbox.ShiftCount != previousShiftCount) GearShifted?.Invoke(previousGear, _gearbox.Gear);
            if (!IsGrounded || !IsUpright) return;
            if (ActiveTrick == TaxiTrick.QuickSpin)
            {
                ApplyQuickSpin(dt);
                return;
            }

            // Signed throttle remains an adapter for the existing measurement harness. Human input
            // gets its sign exclusively from the standstill pedal-direction helper.
            float gas = Mathf.Clamp(Drive.throttle, -1f, 1f);
            float pedal = Mathf.Abs(gas);
            float direction = Mathf.Sign(gas);
            float brake = Mathf.Clamp01(Drive.brake);
            Vector3 planar = Vector3.ProjectOnPlane(_rb.linearVelocity, GroundNormal);
            bool drift = IsTaxiDrifting;

            float slowing = brake * Taxi.braking * Surface.brakeMultiplier;
            if (pedal <= 0f && !drift) slowing += Taxi.coasting;
            slowing += Mathf.Max(0f, Surface.rollingResistance);
            if (drift) slowing += Taxi.driftDrag;
            if (ActiveTrick == TaxiTrick.Stop) slowing = Taxi.stopDeceleration;
            float planarSpeed = planar.magnitude;
            float normalCeiling = ForwardSpeed >= 0f ? Taxi.cruiseSpeed : Taxi.reverseSpeed;
            if (ActiveTrick == TaxiTrick.None && planarSpeed > normalCeiling)
                slowing += Mathf.Min(Taxi.overspeedDeceleration, (planarSpeed - normalCeiling) / dt);
            if (planarSpeed > 0.001f)
                _rb.AddForce(-planar / planarSpeed * Mathf.Min(slowing, planarSpeed / dt), ForceMode.Acceleration);

            float speedLimit = direction >= 0f ? Taxi.cruiseSpeed : Taxi.reverseSpeed;
            float acceleration = direction >= 0f ? Taxi.acceleration : Taxi.reverseAcceleration;
            if (direction >= 0f) acceleration *= gearThrust;
            if (ActiveTrick == TaxiTrick.Dash || ActiveTrick == TaxiTrick.LimiterCut)
            {
                speedLimit = _dashCeiling;
                acceleration = Taxi.dashAcceleration;
            }
            else if (ActiveTrick == TaxiTrick.BackDash)
            {
                speedLimit = Taxi.backDashSpeed;
                acceleration = Taxi.dashAcceleration;
                direction = -1f;
            }

            float push = 0f;
            if (pedal > 0f && brake < 0.05f && !freeRev && ActiveTrick != TaxiTrick.Stop && !drift)
            {
                float along = ForwardSpeed * direction;
                float fade = ActiveTrick == TaxiTrick.Dash || ActiveTrick == TaxiTrick.LimiterCut
                    || ActiveTrick == TaxiTrick.BackDash ? 1f : Mathf.Clamp01((speedLimit - along) / Mathf.Max(0.01f, Taxi.normalAccelerationTaper));
                if (Taxi.useCustomAccelerationCurve && ActiveTrick == TaxiTrick.None)
                    fade = Mathf.Max(0f, Taxi.accelerationMultiplierBySpeed.Evaluate(Mathf.Max(0f, along) / Mathf.Max(0.1f, speedLimit)));
                push = Mathf.Min(acceleration * pedal * fade * Surface.driveMultiplier,
                    Mathf.Max(0f, speedLimit - along) / dt);
                _rb.AddForce(GroundForward * (push * direction), ForceMode.Acceleration);
                AppliedDriveAcceleration = push * direction;
            }

            // A ceiling ends thrust; it never deletes existing speed. Landing/collisions stay physical.
            float speed01 = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / Taxi.cruiseSpeed);
            float turnDegrees = Taxi.useCustomSteeringCurve
                ? Mathf.Max(0f, Taxi.steeringDegreesBySpeed.Evaluate(Mathf.Abs(ForwardSpeed) / Mathf.Max(0.1f, Taxi.cruiseSpeed)))
                : Mathf.Lerp(Taxi.lowSpeedTurnDegrees, Taxi.highSpeedTurnDegrees, speed01);
            float yaw = turnDegrees
                * Mathf.Deg2Rad * SteerAmount * Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / Mathf.Max(0.1f, Taxi.steeringSpeedForFullAuthority))
                * (ForwardSpeed < 0f ? -1f : 1f);
            if (drift)
            {
                float turn = Mathf.Abs(SteerAmount) > 0.1f ? SteerAmount : _driftSign * Taxi.driftReleasedSteer;
                yaw = Taxi.driftTurnDegrees * Mathf.Deg2Rad * turn * Mathf.Clamp01(planarSpeed / 5f);
            }
            SteerCurvature = Mathf.Abs(ForwardSpeed) > 0.1f ? yaw / ForwardSpeed : 0f;
            _rb.AddTorque(GroundNormal * Mathf.Clamp((yaw - YawRate) / dt,
                -Taxi.yawAcceleration, Taxi.yawAcceleration), ForceMode.Acceleration);

            float grip = drift ? Taxi.driftGrip : _exitGripRemaining > 0f ? Taxi.driftExitGrip : Taxi.cornerGrip;
            grip *= Surface.gripMultiplier;
            grip *= Mathf.Max(0f, Taxi.gripMultiplierBySpeed.Evaluate(Mathf.Abs(ForwardSpeed) / Mathf.Max(0.1f, Taxi.cruiseSpeed)));
            AvailableLateralGrip = FrontAxleGrip = RearAxleGrip = grip;
            _rb.AddForce(GroundRight * Mathf.Clamp(-LateralSpeed / dt, -grip, grip), ForceMode.Acceleration);
            LongitudinalDemand = Mathf.Max(push, slowing);

            // Stabilise roll/pitch gently while allowing suspension and ramp launches to work.
            Vector3 correction = Vector3.Cross(transform.up, GroundNormal);
            _rb.AddTorque(correction * Taxi.chassisStability, ForceMode.Acceleration);
            ApplyAntiRoll();
            if (ActiveTrick == TaxiTrick.Stop && planarSpeed < 0.3f)
            {
                _rb.linearVelocity -= planar;
                ActiveTrick = TaxiTrick.None;
                _trickRemaining = 0f;
            }
        }

        private void RecoverTaxi(float dt)
        {
            StepRoadRecovery(dt);
        }

        private void ApplyQuickSpin(float dt)
        {
            float angle = Vector3.SignedAngle(GroundForward, _spinTargetForward, GroundNormal);
            if (Mathf.Abs(angle) > 170f) angle = Mathf.Abs(angle) * _spinSign;
            float rate = Mathf.Clamp(angle * Taxi.quickSpinTargetGain,
                -Taxi.quickSpinTurnDegrees, Taxi.quickSpinTurnDegrees) * Mathf.Deg2Rad;
            _rb.AddTorque(GroundNormal * Mathf.Clamp((rate - YawRate) / dt,
                -Taxi.quickSpinYawAcceleration, Taxi.quickSpinYawAcceleration), ForceMode.Acceleration);
            Vector3 planar = Vector3.ProjectOnPlane(_rb.linearVelocity, GroundNormal);
            // Collisions can remove momentum; the turn must never put that lost speed back.
            _spinSpeed = Mathf.Min(_spinSpeed, planar.magnitude);
            _spinSpeed = Mathf.MoveTowards(_spinSpeed, 0f, Taxi.braking * Mathf.Clamp01(Drive.brake) * dt);
            Vector3 desired = GroundForward * _spinSpeed;
            Vector3 acceleration = Vector3.ClampMagnitude((desired - planar) / dt, Taxi.quickSpinRedirectAcceleration);
            _rb.AddForce(acceleration, ForceMode.Acceleration);
            _rb.AddTorque(Vector3.Cross(transform.up, GroundNormal) * Taxi.chassisStability, ForceMode.Acceleration);
            ApplyAntiRoll();
            SteerAmount = _spinSign;
            AvailableLateralGrip = FrontAxleGrip = RearAxleGrip = Taxi.quickSpinRedirectAcceleration;
            LongitudinalDemand = acceleration.magnitude;
            if (Mathf.Abs(angle) < Taxi.quickSpinCompletionAngle)
            {
                ActiveTrick = TaxiTrick.None;
                _trickRemaining = 0f;
            }
        }

        private void ApplyAntiRoll()
        {
            if (!IsGrounded || !IsUpright) return;
            float error = Vector3.Dot(Vector3.Cross(transform.up, GroundNormal), GroundForward);
            float rate = Vector3.Dot(_rb.angularVelocity, GroundForward);
            _rb.AddTorque(GroundForward * (error * Taxi.antiRollStrength - rate * Taxi.antiRollDamping),
                ForceMode.Acceleration);
        }
    }
}
