using System.Collections.Generic;
using UnityEngine;
using ZoomZoom.Vehicle;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Milestone 2 exploration: makes driving itself the "handling" step Overcooked has and our
    /// current loop doesn't. While carrying an order, hard braking and hard collisions degrade
    /// that order's condition from 100 down toward 0; CargoSystem reads the condition back at the
    /// moment of delivery and scales the payout by it. A trashed delivery still delivers, it's
    /// just worth less, which keeps the existing six-state order machine completely unchanged,
    /// this only ever affects the number CargoSystem hands to OrderManager.ResolveDelivery.
    ///
    /// OFF BY DEFAULT. With enableCondition false this component does nothing at all and every
    /// delivery pays full value, i.e. exactly Milestone 1 behaviour. That's deliberate: this
    /// exists to be demoed as an optional toggle, not a change already committed to, since
    /// whether this is the right fix for "it's just driving, not handling" is an open design
    /// question, not something this file should quietly decide on its own.
    ///
    /// WHAT COUNTS AS A HARD BRAKE
    /// The threshold is derived from the car's own VehicleTuning.brakeDeceleration, not a
    /// hardcoded number. The first version used a fixed 18 m/s², but the car's ordinary full
    /// brake is 22 m/s², so every normal stop at a drop-off registered as a hard brake and cost
    /// 12 condition. A hardcoded threshold would also silently go wrong again the next time
    /// anyone retunes the car. Hard brake now means braking meaningfully harder than the car's
    /// own normal braking can produce (hardBrakeMultiplier above 1), which in practice means a
    /// sudden stop from an impact or a surface change, not the player pressing the brake pedal.
    ///
    /// Attach to the same GameObject as CargoSystem, ZoneDetection, VehicleController and the
    /// vehicle's Rigidbody (i.e. the vehicle root), the same placement convention ZoneDetection
    /// already documents.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class CargoCondition : MonoBehaviour
    {
        [Header("Toggle (Milestone 2 exploration)")]
        [Tooltip("Off by default so the vehicle behaves exactly like Milestone 1 until this is " +
                 "deliberately turned on. When off, every delivery pays out at full value " +
                 "regardless of how it was driven, and no damage is ever applied.")]
        [SerializeField] private bool enableCondition = false;

        [Header("Wiring")]
        [Tooltip("Found automatically on this GameObject if left empty.")]
        [SerializeField] private CargoSystem cargoSystem;
        [Tooltip("Found automatically on this GameObject if left empty. The vehicle's own Rigidbody.")]
        [SerializeField] private Rigidbody vehicleBody;
        [Tooltip("Found automatically on this GameObject if left empty. Its Tuning.brakeDeceleration " +
                 "is what the hard-brake threshold is measured against.")]
        [SerializeField] private VehicleController vehicle;

        [Header("Hard braking")]
        [Tooltip("Hard brake threshold as a multiple of the car's own brakeDeceleration. Must be " +
                 "above 1, otherwise an ordinary full brake counts as a hard brake. 1.25 with the " +
                 "current brakeDeceleration of 22 gives a threshold of 27.5 m/s².")]
        [SerializeField] private float hardBrakeMultiplier = 1.25f;
        [Tooltip("Used only if no VehicleController is found on this GameObject. Should still be " +
                 "set above the car's normal braking (22 at the time of writing).")]
        [SerializeField] private float fallbackHardBrakeDecelThreshold = 27f;
        [Tooltip("Condition points lost per hard-brake event, applied once per event, not per frame.")]
        [SerializeField] private float hardBrakeDamage = 12f;
        [Tooltip("Minimum seconds between one hard-brake penalty and the next, so a single heavy " +
                 "stop across a couple of frames doesn't get charged twice. A collision also " +
                 "starts this cooldown, so one impact is never charged as both a collision and " +
                 "a hard brake.")]
        [SerializeField] private float hardBrakeCooldown = 0.5f;

        [Header("Collisions")]
        [Tooltip("Relative impact speed, m/s, before a collision counts as hard enough to damage cargo.")]
        [SerializeField] private float collisionImpactThreshold = 6f;
        [Tooltip("Condition points lost per m/s of impact speed above the threshold.")]
        [SerializeField] private float collisionDamagePerMps = 4f;

        [Header("Debug")]
        [SerializeField] private bool logActivity = true;

        // Keyed by Order.Id rather than by Order itself, purely so nothing here needs to
        // implement or rely on Order's equality/hashing beyond what int already gives for free.
        private readonly Dictionary<int, float> _condition = new Dictionary<int, float>();

        private float _previousForwardSpeed;
        private float _hardBrakeCooldownRemaining;

        /// <summary>The deceleration, in m/s², above which a speed drop counts as a hard brake.
        /// Read from the car's own tuning every time, so retuning the car moves this with it.</summary>
        public float HardBrakeDecelThreshold
        {
            get
            {
                if (vehicle != null && vehicle.Tuning != null)
                    return vehicle.Tuning.brakeDeceleration * Mathf.Max(1.01f, hardBrakeMultiplier);

                return fallbackHardBrakeDecelThreshold;
            }
        }

        private void Awake()
        {
            if (cargoSystem == null) cargoSystem = GetComponent<CargoSystem>();
            if (vehicleBody == null) vehicleBody = GetComponent<Rigidbody>();
            if (vehicle == null) vehicle = GetComponent<VehicleController>();

            if (cargoSystem == null)
            {
                Debug.LogError(
                    "[CargoCondition] No CargoSystem on this GameObject. Attach both to the same " +
                    "vehicle root. Switching off.", this);
                enabled = false;
                return;
            }

            if (vehicle == null)
            {
                Debug.LogWarning(
                    "[CargoCondition] No VehicleController on this GameObject, so the hard-brake " +
                    $"threshold falls back to {fallbackHardBrakeDecelThreshold} m/s² instead of " +
                    "following the car's brakeDeceleration. Attach this to the vehicle root.", this);
            }

            cargoSystem.OrderCarried += HandleOrderCarried;
            cargoSystem.OrderReleased += HandleOrderReleased;
        }

        private void OnDestroy()
        {
            if (cargoSystem != null)
            {
                cargoSystem.OrderCarried -= HandleOrderCarried;
                cargoSystem.OrderReleased -= HandleOrderReleased;
            }
        }

        private void HandleOrderCarried(Order order)
        {
            _condition[order.Id] = 100f;
        }

        private void HandleOrderReleased(Order order)
        {
            _condition.Remove(order.Id);
        }

        private void FixedUpdate()
        {
            if (vehicleBody == null) return;

            float currentForwardSpeed = ForwardSpeed();

            if (!enableCondition || _condition.Count == 0)
            {
                _previousForwardSpeed = currentForwardSpeed;
                return;
            }

            if (_hardBrakeCooldownRemaining > 0f) _hardBrakeCooldownRemaining -= Time.fixedDeltaTime;

            float decel = (_previousForwardSpeed - currentForwardSpeed) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            _previousForwardSpeed = currentForwardSpeed;

            float threshold = HardBrakeDecelThreshold;

            if (decel > threshold && _hardBrakeCooldownRemaining <= 0f)
            {
                _hardBrakeCooldownRemaining = hardBrakeCooldown;
                ApplyDamage(hardBrakeDamage,
                    $"hard brake ({decel:0.0} m/s² against a {threshold:0.0} threshold)");
            }
        }

        private float ForwardSpeed()
        {
            // linearVelocity, not the deprecated velocity property, matches the Rigidbody API in
            // the Unity 6 line this project targets.
            Vector3 velocity = vehicleBody.linearVelocity;
            return Vector3.Dot(velocity, transform.forward);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!enableCondition || _condition.Count == 0) return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < collisionImpactThreshold) return;

            _hardBrakeCooldownRemaining = hardBrakeCooldown;

            float damage = (impactSpeed - collisionImpactThreshold) * collisionDamagePerMps;
            ApplyDamage(damage, $"collision ({impactSpeed:0.0} m/s)");
        }

        private void ApplyDamage(float amount, string reason)
        {
            if (amount <= 0f) return;

            foreach (int id in new List<int>(_condition.Keys))
            {
                _condition[id] = Mathf.Max(0f, _condition[id] - amount);
            }

            if (logActivity)
                Debug.Log($"[CargoCondition] {amount:0} damage from {reason} applied to " +
                          $"{_condition.Count} carried order(s).");
        }

        public float GetConditionMultiplier(Order order)
        {
            if (!enableCondition || order == null) return 1f;
            return _condition.TryGetValue(order.Id, out float value) ? value / 100f : 1f;
        }

        public float GetConditionPercent(Order order)
        {
            if (!enableCondition || order == null) return 100f;
            return _condition.TryGetValue(order.Id, out float value) ? value : 100f;
        }

        public bool ConditionEnabled => enableCondition;
    }
}