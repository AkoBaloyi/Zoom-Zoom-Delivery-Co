using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// EVERY tunable number for the car and the chase camera lives in this one asset.
    ///
    /// Why a ScriptableObject instead of inspector fields on the scripts:
    ///  1. We can save several complete setups side by side (Balanced / Grippy / Loose)
    ///     and swap between them without touching the scene, so a test is repeatable.
    ///  2. Edits made to a ScriptableObject while the game is running are KEPT when you
    ///     stop playing. Edits to component fields are thrown away. That makes tuning
    ///     while driving actually useful.
    ///  3. There is one place to look when a number is wrong, which is the whole point.
    ///
    /// Units: metres, seconds, kilograms, radians (except where a field says degrees).
    /// Speeds are metres per second. 25 m/s is about 90 km/h.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VehicleTuning",
        menuName = "Zoom Zoom/Vehicle Tuning",
        order = 0)]
    public class VehicleTuning : ScriptableObject
    {
        // ------------------------------------------------------------------
        // WHICH SETUP IS THIS, AND WHY
        // Filled in by hand. This is the paper trail for the design decision.
        // ------------------------------------------------------------------
        [Header("Profile identity")]
        [Tooltip("Short name for this setup. Gets written into every CSV row so readings " +
                 "can never be confused between profiles.")]
        public string profileName = "Unnamed";

        [Tooltip("Plain language: what this setup is trying to do, what it feels like, " +
                 "and why we kept it or rejected it.")]
        [TextArea(4, 12)]
        public string whyThisSetup = "";

        [Tooltip("Tick this on the ONE profile we are shipping with. Purely documentation, " +
                 "nothing reads it at runtime.")]
        public bool isChosenSetup = false;

        // ------------------------------------------------------------------
        // SIMULATION
        // ------------------------------------------------------------------
        [Header("Simulation")]
        [Tooltip("Force the physics step rate when the car spawns. A car that is stepped at a " +
                 "fixed, high rate gives the same result every run, which is the whole basis of " +
                 "'never feels random'. 120 Hz is what Rocket League uses.")]
        public bool overrideFixedTimestep = true;

        [Tooltip("Physics steps per second. 120 = a 0.00833 s step.")]
        [Range(50f, 240f)]
        public float physicsHz = 120f;

        [Tooltip("Downward acceleration applied to the car, m/s^2. We do NOT use Unity's project " +
                 "gravity, because gravity is a handling value: heavier gravity = snappier landings " +
                 "and shorter hops. Real gravity is 9.81, arcade cars usually want more.")]
        public float gravity = 20f;

        // ------------------------------------------------------------------
        // CHASSIS
        // ------------------------------------------------------------------
        [Header("Chassis")]
        [Tooltip("Rigidbody mass, kg. Almost all of our forces are applied as accelerations, " +
                 "so changing mass does NOT secretly change the handling. It only changes how the " +
                 "car pushes other physics objects.")]
        public float mass = 900f;

        [Tooltip("Centre of mass offset from the car origin, in local space. Pushing it DOWN makes " +
                 "the car much less likely to roll over. Pushing it BACK makes the nose lift.")]
        public Vector3 centreOfMassOffset = new Vector3(0f, -0.35f, 0f);

        [Tooltip("Hard cap on how fast the car can spin, rad/s. Stops collisions from turning into " +
                 "a helicopter.")]
        public float maxAngularSpeed = 7f;

        [Tooltip("How quickly unwanted spin bleeds away. Higher = the car settles down faster after " +
                 "a knock.")]
        public float angularDamping = 2.5f;

        [Tooltip("Air resistance on straight line speed. Keep at 0: our own coast and top speed " +
                 "numbers should be the only things slowing the car, otherwise the maths stops matching.")]
        public float linearDamping = 0f;

        // ------------------------------------------------------------------
        // WHEELS AND SUSPENSION (raycasts, not WheelColliders)
        // ------------------------------------------------------------------
        [Header("Wheels and suspension")]
        [Tooltip("Distance from car centre to front/rear wheel rays, metres. Half the wheelbase.")]
        public float wheelForwardOffset = 1.4f;

        [Tooltip("Distance from car centre to left/right wheel rays, metres. Half the track width.")]
        public float wheelSideOffset = 0.8f;

        [Tooltip("Height of the wheel ray origins in local space. 0 = level with the car origin.")]
        public float wheelHeightOffset = 0f;

        [Tooltip("Wheel radius, metres. Used to place the wheel model on the ground and to work out " +
                 "how fast it should be spinning: a wheel rolling without slipping turns at " +
                 "speed / radius radians per second. Get this wrong and the wheels visibly skate.")]
        public float wheelRadius = 0.36f;

        [Tooltip("Wheel width, metres. Purely how the model looks, no effect on the physics.")]
        public float wheelWidth = 0.28f;

        [Tooltip("How far below the ray origin the wheel sits when the suspension is fully extended, " +
                 "metres. This is effectively the ride height.")]
        public float suspensionRestLength = 0.6f;

        [Tooltip("Extra ray length past the rest length, metres. This is the gap in which the car " +
                 "still counts as grounded while the suspension is stretched, e.g. over a crest.")]
        public float suspensionExtraRayLength = 0.3f;

        [Tooltip("Spring stiffness, in m/s^2 of push per metre of compression, PER WHEEL. " +
                 "Four wheels have to hold up 'gravity', so each wheel needs about gravity/4 at rest. " +
                 "Too low = the car bottoms out, too high = it pogos.")]
        public float springStrength = 110f;

        [Tooltip("Suspension damper, in m/s^2 per m/s of compression speed. This is what stops the " +
                 "bouncing. Too low = pogo stick, too high = the car feels welded to the road.")]
        public float springDamper = 14f;

        [Tooltip("Which layers count as drivable ground for the wheel rays.")]
        public LayerMask groundMask = ~0;

        // ------------------------------------------------------------------
        // DRIVE
        // ------------------------------------------------------------------
        [Header("Drive")]
        [Tooltip("Top speed on full throttle, m/s. 25 m/s is about 90 km/h.")]
        public float topSpeed = 25f;

        [Tooltip("Forward acceleration in m/s^2 (Y) at a given forward speed in m/s (X).\n\n" +
                 "This is a CURVE, not one number, on purpose: a single acceleration value means the " +
                 "car pulls just as hard at 24 m/s as at 0, which feels wrong and makes top speed a " +
                 "hard clamp. Curving it down to zero at top speed means the car runs out of pull " +
                 "naturally, and the player can feel how much is left.")]
        public AnimationCurve accelerationBySpeed = DefaultAccelerationCurve();

        [Tooltip("Top speed in reverse, m/s. Deliberately much slower than forwards.")]
        public float reverseTopSpeed = 8f;

        [Tooltip("Acceleration in reverse, m/s^2.")]
        public float reverseAcceleration = 9f;

        [Tooltip("Throttle/steer values smaller than this count as 'not pressed'. Stops controller " +
                 "stick drift from creeping the car forward.")]
        [Range(0f, 0.4f)]
        public float inputDeadzone = 0.08f;

        // ------------------------------------------------------------------
        // SLOWING DOWN
        // ------------------------------------------------------------------
        [Header("Braking and coasting")]
        [Tooltip("Braking deceleration, m/s^2. This is the number that sets stopping distance, so it " +
                 "is the number the level layout depends on. Stopping distance = topSpeed^2 / (2 * this).")]
        public float brakeDeceleration = 22f;

        [Tooltip("Deceleration with no throttle and no brake, m/s^2. Not zero on purpose: lifting off " +
                 "should slow you down, so the player has a middle option between full throttle and " +
                 "full brake. This is a big part of feeling in control.")]
        public float coastDeceleration = 5f;

        // ------------------------------------------------------------------
        // STEERING
        // ------------------------------------------------------------------
        [Header("Steering")]
        [Tooltip("Maximum turn curvature (Y, in 1/metres) at a given speed (X, in m/s).\n\n" +
                 "Curvature is 1 / turn radius. Curvature 0.1 means a 10 m radius circle.\n\n" +
                 "We steer by curvature rather than by 'degrees per second' because curvature is what " +
                 "the player actually reads off the road: at a given speed, full lock always draws the " +
                 "same size circle. Full lock at high speed gives a WIDE circle, which is what stops the " +
                 "car spinning out. Half stick gives half the curvature, every time.")]
        public AnimationCurve maxCurvatureBySpeed = DefaultCurvatureCurve();

        [Tooltip("Seconds for the steering input to ramp from 0 to full. Small amount of smoothing so a " +
                 "keyboard tap is not an instant snap, but small enough that it never feels laggy.")]
        [Range(0f, 0.4f)]
        public float steerInputSmoothTime = 0.07f;

        [Tooltip("How hard the car is allowed to change its turn rate, rad/s^2. This is the difference " +
                 "between a car that snaps into a corner and one that leans into it. It never changes " +
                 "WHERE the car ends up, only how quickly it gets to the turn rate it asked for.")]
        public float maxYawAcceleration = 16f;

        // ------------------------------------------------------------------
        // GRIP: the single most important handling value
        // ------------------------------------------------------------------
        [Header("Sideways grip")]
        [Tooltip("How hard the tyres can fight sideways sliding, in m/s^2. THIS is the slide-or-bite dial.\n\n" +
                 "A corner of radius R at speed V needs V*V/R of sideways grip. If this number is bigger " +
                 "than that, the car holds the line. If it is smaller, the car runs wide, and it runs wide " +
                 "by a predictable amount rather than randomly letting go.\n\n" +
                 "Turn it DOWN for a slidey, drifty delivery van. Turn it UP for a car that bites.")]
        public float lateralGripAcceleration = 12f;

        [Tooltip("Grip multiplier while the handbrake is held. 0.25 means the car keeps a quarter of its " +
                 "sideways grip, so it slides on purpose. This is the same dial as above, on a button.")]
        [Range(0f, 1f)]
        public float handbrakeGripMultiplier = 0.25f;

        // ------------------------------------------------------------------
        // JUMP
        // ------------------------------------------------------------------
        [Header("Jump (grounded only)")]
        [Tooltip("Instant upward speed added on jump, m/s. Jump height = this^2 / (2 * gravity).")]
        public float jumpSpeed = 6.5f;

        [Tooltip("Seconds the player can keep holding jump for extra height. Gives a short hop and a " +
                 "full hop from the same button.")]
        public float jumpHoldTime = 0.18f;

        [Tooltip("Extra upward acceleration while jump is held, m/s^2.")]
        public float jumpHoldAcceleration = 18f;

        [Tooltip("Seconds before the car can jump again.")]
        public float jumpCooldown = 0.35f;

        [Tooltip("How long a jump or flip press is remembered, seconds. If the player presses just " +
                 "before the wheels touch down, the move still fires on landing. Without this, moves " +
                 "get silently eaten and the controls feel unreliable even though nothing is wrong.")]
        [Range(0f, 0.4f)]
        public float inputBufferTime = 0.12f;

        // ------------------------------------------------------------------
        // FLIP: the recovery move
        // ------------------------------------------------------------------
        [Header("Flip (recovery move, grounded only)")]
        [Tooltip("DESIGN RULE, exposed so it can be demonstrated: the flip only works with wheels on " +
                 "the ground. Untick it during testing to show what it would feel like as an air dash, " +
                 "then tick it back on.")]
        public bool flipRequiresGrounded = true;

        [Tooltip("How hard the push is, in m/s of instant velocity change. This is the 'force' dial. " +
                 "It is a velocity change rather than a Newton force so that it means the same thing " +
                 "no matter what the mass is set to.")]
        public float flipForce = 9f;

        [Tooltip("Small upward speed added with the flip, m/s. Just enough to clear a kerb or unstick " +
                 "the car, not enough to be a jump.")]
        public float flipHopSpeed = 2.5f;

        [Tooltip("Spin added during the flip, rad/s, so the move reads clearly on screen. Keep it low: " +
                 "at high values the car will not land on its wheels and the recovery move becomes the " +
                 "thing you need to recover from.")]
        public float flipSpin = 2.5f;

        [Tooltip("Seconds the flip counts as 'in progress'. Used for measurement and to stop the flip " +
                 "being re-triggered mid-move.")]
        public float flipDuration = 0.45f;

        [Tooltip("Seconds before the car can flip again. THIS is the main dial that stops flip-spamming " +
                 "being faster than driving. Raise it until flipping through a corner loses to driving it.")]
        public float flipCooldown = 1.6f;

        [Tooltip("Hard cap on how much overall SPEED a flip may add, m/s. The flip is allowed to move the " +
                 "car sideways out of trouble, but it is not allowed to be a free speed boost.")]
        public float flipMaxSpeedGain = 3f;

        [Tooltip("Above this speed (m/s) the flip adds NO speed at all. It only redirects the car. " +
                 "This is the rule that makes flipping a corner slower than driving it: the faster you " +
                 "are already going, the less the flip gives you.")]
        public float flipNoSpeedGainAboveSpeed = 12f;

        // ------------------------------------------------------------------
        // ANTI-SOFTLOCK
        // ------------------------------------------------------------------
        [Header("Stuck recovery")]
        [Tooltip("If the car ends up on its roof and stationary, roll it back over. The flip is the " +
                 "player's recovery move, but the flip needs wheels on the ground, so without this the " +
                 "player could be stuck forever. Being stuck forever is never a fair difficulty.")]
        public bool autoRightWhenStuck = true;

        [Tooltip("Counts as 'not upright' when the car's up direction dotted with world up drops below " +
                 "this. 1 = perfectly upright, 0 = on its side, -1 = fully upside down.")]
        [Range(-1f, 1f)]
        public float stuckUprightThreshold = 0.35f;

        [Tooltip("Counts as 'not moving' below this speed, m/s.")]
        public float stuckSpeedThreshold = 1.2f;

        [Tooltip("Seconds of being upside down and stationary before we roll the car back over.")]
        public float stuckTimeBeforeRighting = 1.5f;

        [Tooltip("How hard the righting rotation is, rad/s^2.")]
        public float rightingTorque = 8f;

        // ------------------------------------------------------------------
        // LOOK
        // These change what the player sees, never what the car does. They are in here with
        // everything else so that a profile swap changes the whole car, look included.
        // ------------------------------------------------------------------
        [Header("Look: wheels")]
        [Tooltip("Most the front wheels will visibly turn, degrees. A real car at speed barely turns " +
                 "its wheels, which looks wrong on screen, so this is a cap on an exaggeration " +
                 "rather than a physical value.")]
        [Range(5f, 60f)]
        public float maxVisualSteerAngle = 32f;

        [Tooltip("Multiplier on the true steering geometry.\n\n" +
                 "1 = the wheels show exactly the angle the physics is actually using, worked out " +
                 "from the turn curvature. Honest, but at 25 m/s that is only about 3 degrees and " +
                 "reads as dead straight. Above 1 the wheels lie a little so the player can see what " +
                 "they asked for. The car still corners on the real number either way.")]
        [Range(1f, 4f)]
        public float steerVisualExaggeration = 1.6f;

        [Tooltip("Stop the wheels turning while the handbrake is held. Locked wheels are the clearest " +
                 "signal there is that the car has stopped gripping and started sliding.")]
        public bool lockWheelsOnHandbrake = true;

        [Tooltip("How quickly a spinning wheel slows down when the car leaves the ground, per second. " +
                 "Wheels carry on turning in the air, they just are not being driven any more.")]
        [Range(0f, 5f)]
        public float airborneWheelSpinDecay = 0.6f;

        [Header("Look: colours")]
        [Tooltip("Main body colour.")]
        public Color bodyColour = new Color(0.93f, 0.72f, 0.09f, 1f);

        [Tooltip("Nose, skirts, wing and bumpers. A second darker colour is what stops the car " +
                 "reading as one undifferentiated lump.")]
        public Color trimColour = new Color(0.13f, 0.14f, 0.17f, 1f);

        [Tooltip("Windscreen and canopy glass.")]
        public Color glassColour = new Color(0.25f, 0.34f, 0.42f, 1f);

        [Tooltip("Tyres.")]
        public Color tyreColour = new Color(0.09f, 0.09f, 0.1f, 1f);

        [Tooltip("Rims and the hub bar. Keep this bright: the hub bar is the only thing that makes " +
                 "the wheel rotation visible at all, and a spinning plain cylinder looks stationary.")]
        public Color rimColour = new Color(0.82f, 0.84f, 0.87f, 1f);

        [Tooltip("Brake light colour when the brake is off.")]
        public Color brakeLightOffColour = new Color(0.22f, 0.03f, 0.03f, 1f);

        [Tooltip("Brake light colour when the brake is on. This is an emission colour, so values " +
                 "above 1 are allowed and will bloom.")]
        [ColorUsage(true, true)]
        public Color brakeLightOnColour = new Color(3f, 0.15f, 0.1f, 1f);

        // ------------------------------------------------------------------
        // CAMERA
        // ------------------------------------------------------------------
        [Header("Chase camera")]
        [Tooltip("How far behind the car the camera sits, metres.")]
        public float cameraDistance = 7f;

        [Tooltip("How far above the car the camera sits, metres. Higher shows more road but flattens " +
                 "the sense of speed.")]
        public float cameraHeight = 2.9f;

        [Tooltip("Seconds for the camera to catch up to where it should be. This is the 'stiffness' dial. " +
                 "0 = welded to the car (harsh, no sense of weight). Large = the camera trails badly and " +
                 "the player cannot see the corner. 0.10 to 0.15 is usually the sweet spot.")]
        [Range(0f, 0.6f)]
        public float cameraFollowSmoothTime = 0.12f;

        [Tooltip("How far in FRONT of the car the camera aims, metres. This is what decides whether the " +
                 "player can see the corner in time. It must be at least the car's stopping distance, or " +
                 "the player is being asked to react to something they could not see.")]
        public float cameraLookAhead = 16f;

        [Tooltip("Height of the aim point above the car, metres. Raise it to push the road further down " +
                 "the screen and show more of what is coming.")]
        public float cameraLookAtHeight = 1.4f;

        [Tooltip("Extra downward tilt in degrees, on top of aiming at the look-ahead point.")]
        public float cameraExtraPitch = 4f;

        [Tooltip("Camera field of view in degrees. Wider shows more road and exaggerates speed, but " +
                 "distorts at the edges.")]
        [Range(30f, 110f)]
        public float cameraFieldOfView = 62f;

        [Tooltip("Extra distance added at top speed, metres. Pulling back as the car speeds up shows " +
                 "more road exactly when the stopping distance is longest.")]
        public float cameraExtraDistanceAtTopSpeed = 2.5f;

        [Tooltip("Extra height added at top speed, metres.")]
        public float cameraExtraHeightAtTopSpeed = 0.8f;

        [Tooltip("How much the camera looks along where the car is MOVING instead of where it is POINTING. " +
                 "0 = always behind the nose (steady, but during a slide you cannot see where you are " +
                 "going). 1 = always behind the velocity. A little bit helps during slides.")]
        [Range(0f, 1f)]
        public float cameraVelocityInfluence = 0.25f;

        [Tooltip("Seconds for the camera to swing round to a new heading. Separate from position " +
                 "smoothing so the camera can follow position tightly while still rotating gently.")]
        [Range(0f, 0.8f)]
        public float cameraRotationSmoothTime = 0.14f;

        [Tooltip("Degrees per second the player can swivel the camera by hand (right stick / mouse).")]
        public float cameraSwivelSpeed = 120f;

        [Tooltip("Seconds of no swivel input before the camera drifts back behind the car. 0 = never " +
                 "auto-recentre.")]
        public float cameraSwivelRecentreDelay = 1.2f;

        // ==================================================================
        // HELPERS
        // These let the measurement script compare what the car ACTUALLY did
        // against what these numbers say it SHOULD do. If the two disagree,
        // something is wrong in the controller, not in the driving.
        // ==================================================================

        /// <summary>Forward acceleration the drive should produce at this speed, m/s^2.</summary>
        public float AccelerationAt(float forwardSpeed)
        {
            EnsureCurves();
            return Mathf.Max(0f, accelerationBySpeed.Evaluate(Mathf.Abs(forwardSpeed)));
        }

        /// <summary>Tightest curvature (1/metres) available at this speed.</summary>
        public float MaxCurvatureAt(float speed)
        {
            EnsureCurves();
            return Mathf.Max(0f, maxCurvatureBySpeed.Evaluate(Mathf.Abs(speed)));
        }

        /// <summary>Tightest turn radius the steering curve allows at this speed, metres.</summary>
        public float SteeringRadiusAt(float speed)
        {
            float c = MaxCurvatureAt(speed);
            return c <= 0.0001f ? float.PositiveInfinity : 1f / c;
        }

        /// <summary>
        /// Tightest radius the GRIP allows at this speed, metres. A corner needs V*V/R of
        /// sideways grip, so the tightest possible radius is V*V / grip.
        /// </summary>
        public float GripLimitedRadiusAt(float speed)
        {
            if (lateralGripAcceleration <= 0.0001f) return float.PositiveInfinity;
            return (speed * speed) / lateralGripAcceleration;
        }

        /// <summary>
        /// The radius the car should actually manage at this speed: whichever of the two
        /// limits above is wider. If grip is the wider one, the car understeers at full lock.
        /// </summary>
        public float PredictedTurnRadiusAt(float speed)
        {
            return Mathf.Max(SteeringRadiusAt(speed), GripLimitedRadiusAt(speed));
        }

        /// <summary>Textbook stopping distance from top speed, metres: v^2 / (2a).</summary>
        public float PredictedStoppingDistanceFromTopSpeed()
        {
            if (brakeDeceleration <= 0.0001f) return float.PositiveInfinity;
            return (topSpeed * topSpeed) / (2f * brakeDeceleration);
        }

        /// <summary>Jump height from the jump impulse alone (ignoring the hold bonus), metres.</summary>
        public float PredictedJumpHeight()
        {
            if (gravity <= 0.0001f) return float.PositiveInfinity;
            return (jumpSpeed * jumpSpeed) / (2f * gravity);
        }

        // ==================================================================
        // DEFAULTS
        // ==================================================================

        /// <summary>
        /// Acceleration falls off with speed and reaches zero at top speed, so the car runs
        /// out of pull instead of hitting an invisible wall.
        /// </summary>
        public static AnimationCurve DefaultAccelerationCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 18f),
                new Keyframe(8f, 14f),
                new Keyframe(16f, 8f),
                new Keyframe(22f, 3.5f),
                new Keyframe(25f, 0f));
        }

        /// <summary>
        /// Max curvature (1/m) by speed. These values sit just inside the default grip budget
        /// of 12 m/s^2, which means full lock is right on the edge of gripping at every speed.
        /// That is the "demanding but predictable" target: the limit is always the same limit.
        ///
        ///   0 m/s  -> 0.350  (2.9 m radius, parking manoeuvres)
        ///   5 m/s  -> 0.285  (3.5 m radius, needs  7.1 m/s^2)
        ///  10 m/s  -> 0.111  (9.0 m radius, needs 11.1 m/s^2)
        ///  15 m/s  -> 0.050  ( 20 m radius, needs 11.3 m/s^2)
        ///  20 m/s  -> 0.028  ( 36 m radius, needs 11.1 m/s^2)
        ///  25 m/s  -> 0.018  ( 55 m radius, needs 11.4 m/s^2)
        /// </summary>
        public static AnimationCurve DefaultCurvatureCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(5f, 0.285f),
                new Keyframe(10f, 0.111f),
                new Keyframe(15f, 0.05f),
                new Keyframe(20f, 0.028f),
                new Keyframe(25f, 0.018f));
        }

        /// <summary>Rebuilds any curve that has been left empty, so a blank profile still drives.</summary>
        public void EnsureCurves()
        {
            if (accelerationBySpeed == null || accelerationBySpeed.length == 0)
                accelerationBySpeed = DefaultAccelerationCurve();

            if (maxCurvatureBySpeed == null || maxCurvatureBySpeed.length == 0)
                maxCurvatureBySpeed = DefaultCurvatureCurve();
        }

        private void OnValidate()
        {
            EnsureCurves();

            // Keep the numbers physically sensible so a typo cannot produce nonsense.
            physicsHz = Mathf.Clamp(physicsHz, 30f, 240f);
            mass = Mathf.Max(1f, mass);
            topSpeed = Mathf.Max(0.1f, topSpeed);
            reverseTopSpeed = Mathf.Max(0f, reverseTopSpeed);
            brakeDeceleration = Mathf.Max(0f, brakeDeceleration);
            coastDeceleration = Mathf.Max(0f, coastDeceleration);
            lateralGripAcceleration = Mathf.Max(0f, lateralGripAcceleration);
            suspensionRestLength = Mathf.Max(0.01f, suspensionRestLength);
            suspensionExtraRayLength = Mathf.Max(0f, suspensionExtraRayLength);

            // A wheel bigger than the suspension travel would be buried in the body, and a zero
            // radius would divide by zero when working out the spin rate.
            wheelRadius = Mathf.Clamp(wheelRadius, 0.05f, suspensionRestLength);
            wheelWidth = Mathf.Max(0.02f, wheelWidth);
            flipDuration = Mathf.Max(0.05f, flipDuration);
            cameraDistance = Mathf.Max(0.5f, cameraDistance);

            if (string.IsNullOrWhiteSpace(profileName))
                profileName = name;
        }

        private void Reset()
        {
            accelerationBySpeed = DefaultAccelerationCurve();
            maxCurvatureBySpeed = DefaultCurvatureCurve();
        }
    }
}
