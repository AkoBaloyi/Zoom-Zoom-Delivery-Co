using System;
using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// The one global countdown a shift runs against, so every session is the same length and
    /// comparable across playtests. At zero, spawning and ageing both stop; orders still Active or
    /// Carried are frozen rather than forced into Delivered or Late, since an unfinished order is
    /// neither outcome.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShiftTimer : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private OrderManager orderManager;

        [Header("Shift Length")]
        [SerializeField] private float shiftDurationSeconds = 300f; // 5 minutes, per Scope

        [SerializeField] private bool startAutomatically = true;

        [Header("Debug")]
        [SerializeField] private bool logActivity = true;

        public float TimeRemaining { get; private set; }
        public bool IsShiftActive { get; private set; }

        public string FormattedTimeRemaining
        {
            get
            {
                int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(TimeRemaining));
                return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
            }
        }

        public event Action ShiftStarted;
        public event Action ShiftEnded;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();

            if (orderManager == null)
            {
                Debug.LogError(
                    "[ShiftTimer] No OrderManager found in the scene. The timer will still run, " +
                    "but cannot stop spawning when the shift ends.", this);
            }

            TimeRemaining = shiftDurationSeconds;

            if (startAutomatically) StartShift();
        }

        private void Update()
        {
            if (!IsShiftActive) return;

            TimeRemaining -= Time.deltaTime;

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                EndShift();
            }
        }

        public void StartShift()
        {
            if (IsShiftActive) return;

            TimeRemaining = shiftDurationSeconds;
            IsShiftActive = true;

            if (logActivity)
                Debug.Log($"[ShiftTimer] Shift started, {shiftDurationSeconds:0} seconds on the clock.");

            ShiftStarted?.Invoke();
        }

        private void EndShift()
        {
            IsShiftActive = false;

            if (orderManager != null) orderManager.enabled = false;

            if (logActivity)
            {
                Debug.Log("[ShiftTimer] Shift ended. Spawning and ageing stopped, remaining orders " +
                          "left in their current state rather than forced to a result.");
            }

            ShiftEnded?.Invoke();
        }
    }
}