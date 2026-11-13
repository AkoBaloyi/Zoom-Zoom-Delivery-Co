using System;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Counts how hard the player drives. Every collision above a speed threshold is one "hit",
    /// shown live on the HUD and charged at the end of the shift on the results panel.
    ///
    /// WHY THIS AND NOT CargoCondition
    /// CargoCondition already detects impacts, but it only ever does so while an order is in the
    /// car, it degrades a per-order 0-100 condition rather than counting events, and the payout
    /// path never reads it back, so it has no effect on money at all. It is also off by default,
    /// as a Milestone 2 exploration that was never committed to. This is the simpler, committed
    /// rule the group actually wants: a hit is a hit whether the car is loaded or empty, the
    /// player can see the tally climbing while they drive, and the bill arrives once at the end
    /// where it is easy to read. The two can coexist; they measure different things.
    ///
    /// The threshold and cooldown match CargoCondition's so the two agree on what an impact is:
    /// a nudge at walking pace is not a crash, and one heavy hit spread across a few physics
    /// frames is charged once, not three times.
    ///
    /// Attach to the vehicle root, the same GameObject as the Rigidbody, CargoSystem and
    /// ZoneDetection.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class CrashCounter : MonoBehaviour
    {
        [Header("What counts as a hit")]
        [Tooltip("Relative impact speed, m/s, before a collision is counted. 6 m/s is about " +
                 "22 km/h, so kerbs and gentle nudges are ignored. Matches CargoCondition.")]
        [SerializeField] private float impactThreshold = 6f;

        [Tooltip("Seconds before another hit can be counted, so one heavy impact spread over a " +
                 "few physics frames is charged once rather than several times.")]
        [SerializeField] private float cooldownSeconds = 0.5f;

        [Header("Cost")]
        [Tooltip("Rand taken off the shift total for each hit, charged once at the end rather " +
                 "than during the drive.")]
        [SerializeField] private float randPerHit = 5f;

        [Header("Debug")]
        [SerializeField] private bool logActivity = true;

        /// <summary>Hits so far this shift.</summary>
        public int Hits { get; private set; }

        public float RandPerHit => randPerHit;

        /// <summary>What the hits will cost, as a positive number. ShiftResults subtracts it.</summary>
        public float Penalty => Hits * randPerHit;

        /// <summary>Fires with the new total every time a hit lands, so the HUD can update
        /// without polling.</summary>
        public event Action<int> HitsChanged;

        private float _cooldownRemaining;

        private void Update()
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_cooldownRemaining > 0f) return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < impactThreshold) return;

            _cooldownRemaining = cooldownSeconds;
            Hits++;
            HitsChanged?.Invoke(Hits);

            if (logActivity)
                Debug.Log($"[CrashCounter] Hit {Hits} at {impactSpeed:0.0} m/s, " +
                          $"running cost R{Penalty:0}.", this);
        }

        /// <summary>Starts the tally over. Nothing calls this yet; a reload rebuilds the scene
        /// anyway, so it is here for a future "next shift without reloading".</summary>
        public void ResetCount()
        {
            Hits = 0;
            HitsChanged?.Invoke(0);
        }
    }
}
