using System;
using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// What a flip actually did. Handed to the measurement script so the design rule
    /// "a flip must not beat driving" can be checked with numbers instead of opinions.
    /// </summary>
    public struct FlipReport
    {
        public Vector3 startPosition;
        public Vector3 direction;
        public float speedBefore;
        public float speedAfter;
        public float speedGain;
        public float requestedForce;
        public bool speedWasCapped;
    }

    /// <summary>
    /// The recovery move: a grounded jump and a grounded directional flip.
    ///
    /// WHAT THIS IS FOR
    /// The car is meant to be demanding. That means players will end up facing a wall, wedged
    /// against a kerb, or pointing the wrong way after misjudging a corner. Without a recovery
    /// move, the only option is a slow three point turn, which is not fun and is not a skill.
    /// The flip is a get-out-of-trouble button.
    ///
    /// WHAT THIS IS NOT FOR
    /// It must never be the fast way through a normal corner. If flipping beats driving, nobody
    /// learns to drive, and all the work in the steering and grip model is wasted. Three separate
    /// rules enforce that, and all three are exposed so the claim can be tested rather than trusted:
    ///
    ///   1. GROUNDED ONLY (flipRequiresGrounded)
    ///      No air dashing, and it commits the player: for the length of the flip they have no
    ///      steering and no grip, because the wheels are off the ground.
    ///
    ///   2. COOLDOWN (flipCooldown)
    ///      Flip-spamming has a hard rate limit. Raise this until repeatedly flipping down a
    ///      straight is clearly slower than just holding the throttle.
    ///
    ///   3. SPEED GOVERNOR (flipMaxSpeedGain, flipNoSpeedGainAboveSpeed)
    ///      The flip may REDIRECT the car freely, which is the part that gets you out of trouble.
    ///      It may only ADD speed up to a hard cap, and above flipNoSpeedGainAboveSpeed it adds
    ///      none at all. So the faster you are already going, the less a flip gives you, which is
    ///      the opposite of what a corner shortcut would need.
    ///
    ///   4. THE SELF-RIGHT IS NOT A FOURTH WAY TO MOVE
    ///      The same key also rights an overturned car, and that path is held to the same rule. It
    ///      redirects nothing: the car keeps the heading it already had, flattened onto the ground.
    ///      It grants no speed: linear and angular velocity are ZEROED, so taking it always costs
    ///      you every metre per second you had. It is gated on an overturned, slow, not-spinning,
    ///      not-drifting state with ground under the car, held for selfRightHoldTime, and it has its
    ///      own cooldown. Drifting needs Speed at or above driftMinimumSpeed (4) and the gate caps
    ///      speed at 2, so it is unreachable out of a drift; mid air there is no ground within the
    ///      probe. There is no corner it shortens and no air dash it enables.
    ///
    /// Press F4 in the lab to measure the flip and get a PASS/FAIL on rule 3 printed to the console.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    [DisallowMultipleComponent]
    public class VehicleJumpFlip : MonoBehaviour
    {
        [Header("Debug")]
        [Tooltip("Log to the console every time a jump or flip is refused, and why. Useful when a " +
                 "move 'does not work' and you need to know whether it was the cooldown or the " +
                 "grounded rule that stopped it.")]
        [SerializeField] private bool logRefusals = false;

        // These nine live on the component rather than on VehicleTuning deliberately. Every number
        // the car DRIVES with belongs on the shared tuning profiles so the three of them can be
        // compared like for like; the self-right is a softlock escape, not handling, and the three
        // profiles are frozen for this change. Serialized means every threshold can still be tuned
        // in the inspector during a play test without a recompile.
        [Header("Self right")]
        [Tooltip("Lets the flip key recover an overturned car. Off restores the old behaviour " +
                 "exactly, where the only route back is the automatic righting torque in " +
                 "VehicleController.")]
        [SerializeField] private bool selfRightEnabled = true;

        [Tooltip("Dot of the car's up against world up, below which the car counts as overturned. " +
                 "0.5 is the project's own 'upright enough to drive' line " +
                 "(VehicleController.IsUpright), and it deliberately catches the wedged-on-its-side " +
                 "case that the tuning asset's 0.35 auto-right gate misses.")]
        [SerializeField] private float selfRightUprightThreshold = 0.5f;

        [Tooltip("Seconds the overturned, slow, resting state must hold before a flip press " +
                 "becomes a self-right. Stops a press landing as a recovery mid-tumble.")]
        [SerializeField] private float selfRightHoldTime = 0.6f;

        [Tooltip("Metres per second above which the car is still going somewhere and is not stuck. " +
                 "Below driftMinimumSpeed (4), so this can never fire out of a drift.")]
        [SerializeField] private float selfRightMaxSpeed = 2f;

        [Tooltip("Radians per second of spin above which the car is still rolling over and is left " +
                 "alone to finish.")]
        [SerializeField] private float selfRightMaxAngularSpeed = 1.5f;

        [Tooltip("Metres of world-down probe for ground under the car. An airborne car has nothing " +
                 "within this, which is what keeps the move off the table in mid air.")]
        [SerializeField] private float selfRightGroundProbe = 2f;

        [Tooltip("Metres above the ground the car is placed at when it is righted. 0.8 is the spawn " +
                 "height every scene uses with the floor at y = 0, which leaves the documented 0.5 m " +
                 "of air under the body collider and lets the suspension settle it.")]
        [SerializeField] private float selfRightRideHeight = 0.8f;

        [Tooltip("Seconds before another self-right is allowed, so it cannot be spammed.")]
        [SerializeField] private float selfRightCooldown = 2f;

        [Tooltip("Seconds of the same overturned, resting state after which the car rights itself " +
                 "with no input at all. Twice the tuning asset's 1.5 s righting-torque delay, so the " +
                 "gentle torque gets its chance first and this is only the backstop. 0 disables it " +
                 "and leaves the flip key as the only route back.")]
        [SerializeField] private float selfRightAutoAfterSeconds = 3f;

        /// <summary>Fires the moment a flip actually starts, with everything it did.</summary>
        public event Action<FlipReport> FlipStarted;

        /// <summary>Fires the moment an overturned car is righted. Mirrors FlipStarted.</summary>
        public event Action SelfRighted;

        // ---------------- readable state ----------------

        /// <summary>True for flipDuration seconds after a flip begins.</summary>
        public bool IsFlipping => _flipTimer > 0f;

        /// <summary>Seconds until the next flip is allowed. 0 = ready.</summary>
        public float FlipCooldownRemaining { get; private set; }

        /// <summary>Seconds until the next jump is allowed. 0 = ready.</summary>
        public float JumpCooldownRemaining { get; private set; }

        /// <summary>Everything the most recent flip did.</summary>
        public FlipReport LastFlip { get; private set; }

        /// <summary>How many flips have happened since the game started. Handy sanity check.</summary>
        public int FlipCount { get; private set; }

        /// <summary>
        /// How many self-rights have happened since the game started. Logged per shift: a session
        /// full of these is a session where the player kept ending up on the roof, which is a map or
        /// a handling problem rather than a recovery problem.
        /// </summary>
        public int SelfRightCount { get; private set; }

        /// <summary>Seconds until another self-right is allowed. 0 = ready.</summary>
        public float SelfRightCooldownRemaining { get; private set; }

        /// <summary>
        /// True when a flip press would be spent on righting the car instead of flipping it: the
        /// overturned, slow, not-spinning, not-drifting, ground-below state has held for
        /// selfRightHoldTime and the cooldown is clear.
        /// </summary>
        public bool CanSelfRightNow { get; private set; }

        public bool CanJumpNow =>
            JumpCooldownRemaining <= 0f && _controller != null && _controller.IsGrounded;

        public bool CanFlipNow =>
            FlipCooldownRemaining <= 0f
            && !IsFlipping
            && _controller != null
            && (!_controller.Tuning.flipRequiresGrounded || _controller.IsGrounded);

        // ---------------- internals ----------------

        private VehicleController _controller;
        private Rigidbody _rb;
        private VehicleTuning Tuning => _controller != null ? _controller.Tuning : null;

        // Buffered presses. Input happens on drawn frames, physics happens on its own clock, so a
        // press is remembered for a moment rather than acted on immediately.
        private bool _jumpQueued;
        private float _jumpQueuedAge;
        private bool _flipQueued;
        private float _flipQueuedAge;
        private Vector2 _flipQueuedDirection;

        private bool _jumpHeld;
        private float _jumpHoldRemaining;
        private Vector3 _jumpUpDirection = Vector3.up;

        private float _flipTimer;

        // How long the car has been overturned AND resting. One timer, reset the instant any gate
        // stops being true, so a car still tumbling never accumulates towards a recovery.
        private float _overturnedFor;
        private RaycastHit _groundHit;

        private void Awake()
        {
            _controller = GetComponent<VehicleController>();
            _rb = GetComponent<Rigidbody>();
        }

        // ==================================================================
        // ASKED FOR BY WHOEVER IS DRIVING
        // ==================================================================

        /// <summary>Queue a jump. Safe to call from Update.</summary>
        public void RequestJump()
        {
            _jumpQueued = true;
            _jumpQueuedAge = 0f;
        }

        /// <summary>
        /// Queue a flip. Direction is in stick space: y is forward/back, x is left/right, relative
        /// to the car. Zero means straight forward.
        /// </summary>
        public void RequestFlip(Vector2 direction)
        {
            _flipQueued = true;
            _flipQueuedAge = 0f;
            _flipQueuedDirection = direction;
        }

        /// <summary>Tell the jump whether the button is still down, for the variable height.</summary>
        public void SetJumpHeld(bool held)
        {
            _jumpHeld = held;
        }

        // ==================================================================
        // PHYSICS
        // ==================================================================

        private void FixedUpdate()
        {
            if (Tuning == null) return;

            float dt = Time.fixedDeltaTime;

            FlipCooldownRemaining = Mathf.Max(0f, FlipCooldownRemaining - dt);
            JumpCooldownRemaining = Mathf.Max(0f, JumpCooldownRemaining - dt);
            _flipTimer = Mathf.Max(0f, _flipTimer - dt);

            AgeQueuedInput(dt);
            UpdateOverturnedState(dt);

            // One key, two meanings, decided by the car's state rather than by a second binding.
            // A press is spent on recovery only when the car is genuinely stuck upside down; in
            // every other case it reaches TryFlip exactly as it always did. Reusing Flip is also
            // what a tester already tries when the car is on its roof, and adding an input action
            // would break the README Controls table, which is contract-tested against
            // VehicleControls.inputactions.
            if (_flipQueued && CanSelfRightNow && TrySelfRight()) _flipQueued = false;
            else if (_flipQueued && TryFlip()) _flipQueued = false;

            // The backstop. A player who does not know the key exists, or who has given up pressing
            // it, still gets their car back rather than sitting out the rest of the shift.
            if (selfRightAutoAfterSeconds > 0f
                && _overturnedFor >= selfRightAutoAfterSeconds
                && CanSelfRightNow)
            {
                TrySelfRight();
            }

            if (_jumpQueued && TryJump()) _jumpQueued = false;

            ApplyJumpHold(dt);
        }

        /// <summary>Buffered presses expire, so a press does not fire ten seconds later.</summary>
        private void AgeQueuedInput(float dt)
        {
            float buffer = Tuning.inputBufferTime;

            if (_jumpQueued)
            {
                _jumpQueuedAge += dt;
                if (_jumpQueuedAge > buffer) _jumpQueued = false;
            }

            if (_flipQueued)
            {
                _flipQueuedAge += dt;
                if (_flipQueuedAge > buffer) _flipQueued = false;
            }
        }

        // ==================================================================
        // JUMP
        // ==================================================================

        private bool TryJump()
        {
            if (JumpCooldownRemaining > 0f)
            {
                Refuse($"jump on cooldown ({JumpCooldownRemaining:0.00}s left)");
                return false;
            }

            if (!_controller.IsGrounded)
            {
                Refuse("jump needs wheels on the ground");
                return false;
            }

            // Jump away from the surface we are standing on, not straight up in world space. On a
            // ramp that means the car leaves along the ramp, which is what the player expects.
            _jumpUpDirection = _controller.GroundNormal;

            // VelocityChange, not Impulse: an instant change in m/s means the same thing whatever
            // the mass is set to, so the jump height stays put when the mass is tuned.
            _rb.AddForce(_jumpUpDirection * Tuning.jumpSpeed, ForceMode.VelocityChange);

            _jumpHoldRemaining = Tuning.jumpHoldTime;
            JumpCooldownRemaining = Tuning.jumpCooldown;
            return true;
        }

        /// <summary>
        /// Extra push while the button is held, for a short window. Gives a small hop and a full
        /// hop from one button, so the player has fine control over clearing an obstacle.
        /// </summary>
        private void ApplyJumpHold(float dt)
        {
            if (_jumpHoldRemaining <= 0f) return;

            if (!_jumpHeld)
            {
                _jumpHoldRemaining = 0f;
                return;
            }

            _rb.AddForce(_jumpUpDirection * Tuning.jumpHoldAcceleration, ForceMode.Acceleration);
            _jumpHoldRemaining -= dt;
        }

        // ==================================================================
        // FLIP
        // ==================================================================

        private bool TryFlip()
        {
            if (IsFlipping)
            {
                Refuse("already mid flip");
                return false;
            }

            if (FlipCooldownRemaining > 0f)
            {
                Refuse($"flip on cooldown ({FlipCooldownRemaining:0.00}s left)");
                return false;
            }

            if (Tuning.flipRequiresGrounded && !_controller.IsGrounded)
            {
                Refuse("flip needs wheels on the ground");
                return false;
            }

            Vector3 direction = ResolveFlipDirection(_flipQueuedDirection);

            Vector3 velocityBefore = _rb.linearVelocity;
            float speedBefore = velocityBefore.magnitude;

            Vector3 pushed = velocityBefore + direction * Tuning.flipForce;

            // ---- the speed governor ----
            // Redirecting is free. Gaining speed is not. The allowance shrinks to nothing as the
            // car approaches flipNoSpeedGainAboveSpeed, so there is no sudden cliff where the flip
            // stops helping, and no speed at which flipping is a shortcut.
            float headroom = Tuning.flipNoSpeedGainAboveSpeed - speedBefore;
            float allowedGain = Mathf.Clamp(headroom, 0f, Tuning.flipMaxSpeedGain);
            float maxSpeedAfter = speedBefore + allowedGain;

            bool capped = false;
            if (pushed.magnitude > maxSpeedAfter)
            {
                // Keep the new DIRECTION, throw away the extra speed. That is the whole design:
                // the flip moves you, it does not make you faster.
                pushed = pushed.sqrMagnitude > 0.0001f
                    ? pushed.normalized * maxSpeedAfter
                    : Vector3.zero;
                capped = true;
            }

            // The hop goes on after the cap, because upward speed is not corner-cutting speed and
            // should not be eaten by the governor. It is only there to unstick the car.
            Vector3 hop = _controller.GroundNormal * Tuning.flipHopSpeed;

            _rb.linearVelocity = pushed + hop;

            // Spin so the move reads on screen: the car tips over in the direction it flipped.
            // Cross(up, direction) is the axis that rotates the roof towards the flip direction.
            Vector3 spinAxis = Vector3.Cross(_controller.GroundNormal, direction);
            if (spinAxis.sqrMagnitude > 0.0001f)
            {
                _rb.angularVelocity += spinAxis.normalized * Tuning.flipSpin;
            }

            _flipTimer = Tuning.flipDuration;
            FlipCooldownRemaining = Tuning.flipCooldown;
            FlipCount++;

            LastFlip = new FlipReport
            {
                startPosition = _rb.position,
                direction = direction,
                speedBefore = speedBefore,
                speedAfter = pushed.magnitude,
                speedGain = pushed.magnitude - speedBefore,
                requestedForce = Tuning.flipForce,
                speedWasCapped = capped
            };

            FlipStarted?.Invoke(LastFlip);
            return true;
        }

        /// <summary>
        /// Turns stick input into a direction on the ground, relative to the car. Using the car's
        /// own forward and right, never world axes, so "flip left" means the car's left no matter
        /// which way it happens to be facing.
        /// </summary>
        private Vector3 ResolveFlipDirection(Vector2 stick)
        {
            if (stick.magnitude < Tuning.inputDeadzone)
            {
                // No direction held: flip forwards. Predictable default beats doing nothing.
                return _controller.GroundForward;
            }

            stick = stick.normalized;
            Vector3 dir = _controller.GroundForward * stick.y + _controller.GroundRight * stick.x;

            return dir.sqrMagnitude > 0.0001f ? dir.normalized : _controller.GroundForward;
        }

        // ==================================================================
        // SELF RIGHT
        // ==================================================================

        /// <summary>
        /// Watches for the one state the flip cannot help with: the car overturned and come to rest.
        ///
        /// IsGrounded cannot be used here. It comes from suspension rays cast along the car's own
        /// down, so a car on its roof is casting at the sky and reads as airborne, which is exactly
        /// why the flip refuses in the state it exists for. ProbeGroundBelow asks the world-down
        /// question instead.
        ///
        /// All five conditions have to hold together, and the timer resets the moment any of them
        /// stops holding, so this describes a car that is STUCK rather than one that is mid-tumble,
        /// mid-air or mid-drift.
        /// </summary>
        private void UpdateOverturnedState(float dt)
        {
            SelfRightCooldownRemaining = Mathf.Max(0f, SelfRightCooldownRemaining - dt);

            bool overturned = _controller.Uprightness < selfRightUprightThreshold;
            bool slow = _controller.Speed < selfRightMaxSpeed;
            bool notSpinning = _rb.angularVelocity.magnitude < selfRightMaxAngularSpeed;
            bool notDrifting = !_controller.IsDrifting;
            bool nearGround = _controller.ProbeGroundBelow(selfRightGroundProbe, out _groundHit);

            if (overturned && slow && notSpinning && notDrifting && nearGround) _overturnedFor += dt;
            else _overturnedFor = 0f;

            CanSelfRightNow = selfRightEnabled
                              && _overturnedFor >= selfRightHoldTime
                              && SelfRightCooldownRemaining <= 0f
                              && !IsFlipping;
        }

        /// <summary>
        /// Puts an overturned car back on its wheels, facing the way it was already facing.
        ///
        /// Deterministic on purpose. The automatic righting torque in VehicleController is a nudge
        /// that may or may not roll a 900 kg box off its roof depending on what it is wedged
        /// against; this is the guaranteed route back, so the player is never watching the rest of
        /// the shift from upside down. It costs them everything they had: the heading is kept but
        /// speed and spin are thrown away, which is why it can never be worth taking on purpose.
        /// </summary>
        private bool TrySelfRight()
        {
            if (!selfRightEnabled)
            {
                Refuse("self right is switched off");
                return false;
            }

            if (IsFlipping)
            {
                Refuse("self right during a flip");
                return false;
            }

            if (SelfRightCooldownRemaining > 0f)
            {
                Refuse($"self right on cooldown ({SelfRightCooldownRemaining:0.00}s left)");
                return false;
            }

            if (_controller.Uprightness >= selfRightUprightThreshold)
            {
                Refuse("self right needs the car overturned");
                return false;
            }

            if (_controller.Speed >= selfRightMaxSpeed)
            {
                Refuse($"self right needs the car stopped ({_controller.Speed:0.0} m/s)");
                return false;
            }

            if (_rb.angularVelocity.magnitude >= selfRightMaxAngularSpeed)
            {
                Refuse("self right needs the car to stop rolling first");
                return false;
            }

            if (_controller.IsDrifting)
            {
                Refuse("self right while drifting");
                return false;
            }

            if (_overturnedFor < selfRightHoldTime)
            {
                Refuse($"self right needs {selfRightHoldTime:0.00}s of resting upside down " +
                       $"({_overturnedFor:0.00}s so far)");
                return false;
            }

            if (!_controller.ProbeGroundBelow(selfRightGroundProbe, out _groundHit))
            {
                Refuse("self right found no ground below the car");
                return false;
            }

            // Keep the heading, flatten it. Same two fallbacks the controller's own ground basis
            // uses, so a nose pointing straight up or straight down cannot collapse the rotation
            // to zero and produce NaNs.
            Vector3 flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;

            Quaternion upright = Quaternion.LookRotation(flat.normalized, Vector3.up);
            Vector3 place = _groundHit.point + Vector3.up * selfRightRideHeight;

            // SetPoseAndClearMotion, never Teleport: Teleport refills the boost tank, which would
            // make rolling the car the cheapest way to buy boost and would corrupt boost_earned in
            // the shift log.
            _controller.SetPoseAndClearMotion(place, upright);

            _overturnedFor = 0f;
            SelfRightCooldownRemaining = selfRightCooldown;
            SelfRightCount++;
            CanSelfRightNow = false;

            SelfRighted?.Invoke();
            return true;
        }

        private void Refuse(string reason)
        {
            if (logRefusals) Debug.Log($"[VehicleJumpFlip] Refused: {reason}.", this);
        }
    }
}
