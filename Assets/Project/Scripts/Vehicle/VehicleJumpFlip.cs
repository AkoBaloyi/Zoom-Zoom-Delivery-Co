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

        /// <summary>Fires the moment a flip actually starts, with everything it did.</summary>
        public event Action<FlipReport> FlipStarted;

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

            if (_flipQueued && TryFlip()) _flipQueued = false;
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

        private void Refuse(string reason)
        {
            if (logRefusals) Debug.Log($"[VehicleJumpFlip] Refused: {reason}.", this);
        }
    }
}
