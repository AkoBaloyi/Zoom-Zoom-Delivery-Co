using System;
using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// One wheel's two transforms.
    ///
    /// WHY IT TAKES TWO AND NOT ONE
    /// A wheel does two rotations at once: it steers about its vertical axis and it rolls about its
    /// axle. Put both on one transform and they fight, because the roll keeps redefining which way
    /// "vertical" is, and after a few seconds of driving the wheel is lying on its side. Splitting
    /// them means each transform has exactly one job:
    ///
    ///     pivot    position from the suspension, and the steering angle
    ///       spin   the rolling angle, and nothing else
    ///         (the actual wheel model hangs under here)
    ///
    /// Marked Serializable because this one genuinely is meant to be filled in in the inspector,
    /// unlike DriveInput over in VehicleController which is deliberately NOT serialized.
    /// </summary>
    [Serializable]
    public class WheelVisual
    {
        [Tooltip("Gets moved up and down by the suspension, and turned left and right by the steering. " +
                 "Should sit at the wheel's centre with no rotation of its own.")]
        public Transform pivot;

        [Tooltip("Child of the pivot. Only ever rotates about its local X, which must point along " +
                 "the axle. The wheel model goes under this.")]
        public Transform spin;

        [Tooltip("Tick for the front wheels. Only these turn with the steering.")]
        public bool steers;
    }

    /// <summary>
    /// Drives everything on the car that moves but does not affect the physics: the wheels, and the
    /// brake lights.
    ///
    /// WHY THIS IS A SEPARATE COMPONENT
    /// Nothing in here can change how the car drives. That is the point. It only reads. So the whole
    /// thing can be switched off, or pointed at a completely different model, and the measurements
    /// taken in the lab stay valid. When a real car model arrives, the swap is: drop the model in,
    /// point these four pivot and spin transforms at its wheels, done. No physics is touched.
    ///
    /// WHAT MAKES WHEELS LOOK RIGHT, IN ORDER OF HOW MUCH IT MATTERS
    ///  1. They stay on the ground. The wheels follow the same raycast the suspension used, so on a
    ///     bump they ride up into the arch and the body stays level. A car whose wheels are welded
    ///     to the body reads as a sliding box no matter how good the handling underneath is.
    ///  2. They spin at the right rate. A wheel rolling without slipping turns at speed / radius
    ///     radians per second. Guess this and the car looks like it is skating on ice.
    ///  3. They lock when you slide. Holding the handbrake stops them dead, which is the clearest
    ///     possible signal that the car has stopped gripping.
    ///  4. They keep turning in mid air, slowly winding down, because nothing is driving them.
    ///
    /// Runs in Update, not FixedUpdate: this is drawing, and it should happen once per drawn frame
    /// at whatever rate the screen is running, not once per physics step.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleVisuals : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The car this is drawing. Found on the same object if left empty.")]
        [SerializeField] private VehicleController car;

        [Tooltip("Four wheels, in this order: front left, front right, rear left, rear right. " +
                 "The order matters, it has to match the controller's raycasts.")]
        [SerializeField] private WheelVisual[] wheels = new WheelVisual[0];

        [Tooltip("Renderers that light up under braking. Their material gets an instance of its own " +
                 "at startup, so the shared asset is never modified.")]
        [SerializeField] private Renderer[] brakeLights = new Renderer[0];

        [Header("Debug")]
        [Tooltip("Turn off to freeze the wheels where they are, which is occasionally useful for " +
                 "checking the rest position and the wheel radius by eye.")]
        [SerializeField] private bool animate = true;

        /// <summary>Current rolling angle of each wheel in degrees. Kept so it carries over frames.</summary>
        private float[] _spinAngles;

        private Material[] _brakeLightMaterials;
        private static readonly int EmissionColourId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");

        private VehicleTuning Tuning => car != null ? car.Tuning : null;

        private void Awake()
        {
            if (car == null) car = GetComponent<VehicleController>();
            if (car == null) car = GetComponentInParent<VehicleController>();

            if (car == null)
            {
                Debug.LogError(
                    "[VehicleVisuals] No VehicleController found. The wheels will not move.", this);
                enabled = false;
                return;
            }

            _spinAngles = new float[Mathf.Max(1, wheels.Length)];
            PrepareBrakeLights();
        }

        /// <summary>
        /// Called by CarVisualBuilder after it has made the model, so the references are set up
        /// without anyone having to drag eight transforms into the inspector by hand.
        /// </summary>
        public void Bind(WheelVisual[] newWheels, Renderer[] newBrakeLights)
        {
            wheels = newWheels ?? new WheelVisual[0];
            brakeLights = newBrakeLights ?? new Renderer[0];
            _spinAngles = new float[Mathf.Max(1, wheels.Length)];
            PrepareBrakeLights();
        }

        private void PrepareBrakeLights()
        {
            _brakeLightMaterials = new Material[brakeLights.Length];

            for (int i = 0; i < brakeLights.Length; i++)
            {
                if (brakeLights[i] == null) continue;

                // Touching .material rather than .sharedMaterial deliberately: it hands back a copy
                // belonging to this renderer, so brightening it cannot leak into any other object
                // using the same material, and cannot dirty an asset on disk.
                Material instance = brakeLights[i].material;

                // URP only evaluates emission when this keyword is on, and a material made in code
                // does not have it by default. Setting it here means the brake lights work whether
                // the material came from an asset or was built at runtime.
                instance.EnableKeyword("_EMISSION");
                instance.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

                _brakeLightMaterials[i] = instance;
            }
        }

        private void Update()
        {
            if (!animate || car == null || Tuning == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateWheels(dt);
            UpdateBrakeLights();
        }

        // ==================================================================
        // WHEELS
        // ==================================================================

        private void UpdateWheels(float dt)
        {
            VehicleTuning tuning = Tuning;

            float steerAngle = ComputeVisualSteerAngle(tuning);
            float spinDegreesThisFrame = ComputeSpinDegrees(tuning, dt);

            int count = Mathf.Min(wheels.Length, VehicleController.WheelCount);

            for (int i = 0; i < count; i++)
            {
                WheelVisual wheel = wheels[i];
                if (wheel == null) continue;

                WheelVisualState state = car.GetWheelVisualState(i);

                if (wheel.pivot != null)
                {
                    // The wheel centre sits one radius above the contact patch, and the contact patch
                    // is suspensionLength below where the ray started. So as the suspension squashes,
                    // suspensionLength shrinks and the wheel rises towards the body. This is the one
                    // line that makes the car look sprung rather than rigid.
                    Vector3 origin = car.GetWheelLocalRayOrigin(i);
                    float drop = state.suspensionLength - tuning.wheelRadius;

                    wheel.pivot.localPosition = new Vector3(origin.x, origin.y - drop, origin.z);

                    wheel.pivot.localRotation = wheel.steers
                        ? Quaternion.Euler(0f, steerAngle, 0f)
                        : Quaternion.identity;
                }

                if (wheel.spin == null) continue;

                // Wheels on the ground are driven by the road. Wheels in the air are not driven by
                // anything, so they just wind down.
                float delta = state.grounded
                    ? spinDegreesThisFrame
                    : spinDegreesThisFrame * Mathf.Max(0f, 1f - tuning.airborneWheelSpinDecay * dt);

                _spinAngles[i] = Mathf.Repeat(_spinAngles[i] + delta, 360f);
                wheel.spin.localRotation = Quaternion.Euler(_spinAngles[i], 0f, 0f);
            }
        }

        /// <summary>
        /// How far the wheels have rolled this frame, in degrees.
        ///
        /// A wheel rolling without slipping covers its own circumference in one turn, so the angular
        /// rate is simply speed divided by radius. That is why the radius has to be right: too small
        /// and the wheels spin like a cartoon, too large and the car skates.
        /// </summary>
        private float ComputeSpinDegrees(VehicleTuning tuning, float dt)
        {
            if (tuning.lockWheelsOnHandbrake && car.Drive.handbrake)
            {
                // Locked solid. Not slowed down, stopped, because that is what a handbrake does and
                // it is the strongest visual cue we have that the car is now sliding rather than
                // gripping.
                return 0f;
            }

            float radius = Mathf.Max(0.05f, tuning.wheelRadius);
            float radiansPerSecond = car.ForwardSpeed / radius;

            return radiansPerSecond * Mathf.Rad2Deg * dt;
        }

        /// <summary>
        /// The angle to turn the front wheels to, in degrees.
        ///
        /// Started from the real number rather than an invented one. The car corners on a curvature,
        /// which is one over the turn radius, and the front wheel angle that produces a given
        /// curvature is atan(wheelbase * curvature). So at 5 m/s the wheels turn a long way and at
        /// 25 m/s they barely move, which is exactly what the physics is doing.
        ///
        /// The catch is that "barely moves" is about three degrees, and on screen that reads as the
        /// player's input being ignored. So there is a multiplier on top. It only ever changes what
        /// the wheels look like, never how the car turns, and it is in the tuning asset so the size
        /// of the lie is written down rather than buried in here.
        /// </summary>
        private float ComputeVisualSteerAngle(VehicleTuning tuning)
        {
            float wheelbase = Mathf.Max(0.1f, tuning.wheelForwardOffset * 2f);
            float trueAngle = Mathf.Atan(wheelbase * car.SteerCurvature) * Mathf.Rad2Deg;

            float exaggerated = trueAngle * tuning.steerVisualExaggeration;

            return Mathf.Clamp(exaggerated, -tuning.maxVisualSteerAngle, tuning.maxVisualSteerAngle);
        }

        // ==================================================================
        // BRAKE LIGHTS
        // ==================================================================

        private void UpdateBrakeLights()
        {
            if (_brakeLightMaterials == null) return;

            VehicleTuning tuning = Tuning;

            // On for the brake, and also for the handbrake, because from behind those look the same
            // and both mean "this car is slowing down, do not drive into it".
            bool braking = car.Drive.brake > 0.05f || car.Drive.handbrake;

            // Reverse counts too: pressing back while still rolling forwards is braking, and it is
            // the most common way a player actually slows down.
            if (car.Drive.throttle < -0.05f && car.ForwardSpeed > 0.5f) braking = true;

            Color emission = braking ? tuning.brakeLightOnColour : Color.black;
            Color baseColour = braking ? tuning.brakeLightOnColour : tuning.brakeLightOffColour;

            for (int i = 0; i < _brakeLightMaterials.Length; i++)
            {
                Material material = _brakeLightMaterials[i];
                if (material == null) continue;

                if (material.HasProperty(EmissionColourId)) material.SetColor(EmissionColourId, emission);
                if (material.HasProperty(BaseColourId)) material.SetColor(BaseColourId, baseColour);
            }
        }

        // ==================================================================
        // EDITOR
        // ==================================================================

        private void OnDrawGizmosSelected()
        {
            if (car == null || Tuning == null) return;

            // Where each wheel should be sitting, so a wrong radius or a wrong offset is obvious
            // in the scene view without having to press Play.
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);

            for (int i = 0; i < VehicleController.WheelCount; i++)
            {
                Vector3 origin = car.GetWheelLocalRayOrigin(i);
                float drop = Tuning.suspensionRestLength - Tuning.wheelRadius;
                Vector3 centre = car.transform.TransformPoint(
                    new Vector3(origin.x, origin.y - drop, origin.z));

                Gizmos.DrawWireSphere(centre, Tuning.wheelRadius);
            }
        }
    }
}
