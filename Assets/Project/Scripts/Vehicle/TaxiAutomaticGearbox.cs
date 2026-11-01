using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>Forward ratios are independent of the D/R selector used by Crazy Taxi tricks.</summary>
    public sealed class TaxiAutomaticGearbox
    {
        public int Gear { get; private set; } = 1;
        public float Rpm { get; private set; } = 1000f;
        public float ShiftRemaining { get; private set; }
        public int ShiftCount { get; private set; }
        private float _holdRemaining;
        private float _shiftDuration, _shiftFromRpm;
        private float _limiterPhase;
        public bool IsFreeRevving { get; private set; }
        public bool IsAtRevLimiter { get; private set; }
        public float RevLimiterGain { get; private set; } = 1f;
        public float ShiftProgress => ShiftRemaining > 0f && _shiftDuration > 0f ? 1f - ShiftRemaining / _shiftDuration : 1f;
        public float ThrustEnvelope(CrazyTaxiSettings s)
        {
            if (ShiftRemaining <= 0f) return 1f;
            if (!s.progressiveShift) return Mathf.Clamp01(s.shiftThrustRetention);
            float engage = Mathf.InverseLerp(Mathf.Clamp(s.shiftUnloadFraction, 0.05f, 0.9f), 1f, ShiftProgress);
            return Mathf.Lerp(Mathf.Clamp01(s.shiftThrustRetention), 1f, engage * engage * (3f - 2f * engage));
        }
        public void Reset(float idleRpm = 1000f)
        {
            Gear = 1; Rpm = Mathf.Max(100f, idleRpm); ShiftRemaining = 0f; ShiftCount = 0; _holdRemaining = _shiftDuration = _limiterPhase = 0f;
            IsFreeRevving = IsAtRevLimiter = false; RevLimiterGain = 1f;
        }

        public float Step(CrazyTaxiSettings s, float speed, float pedal, bool reverse, bool grounded, float dt, bool freeRev = false)
        {
            IsFreeRevving = freeRev;
            IsAtRevLimiter = false; RevLimiterGain = 1f;
            if (freeRev)
            {
                ShiftRemaining = _holdRemaining = 0f;
                if (Mathf.Abs(speed) <= Mathf.Max(0.01f, s.reverseEngageSpeed)) Gear = 1;
                float limit = Mathf.Max(s.engineIdleRpm + 1f, s.engineFreeRevLimit);
                float requested = Mathf.Lerp(s.engineIdleRpm, limit, Mathf.Clamp01(pedal));
                Rpm = Mathf.MoveTowards(Rpm, requested, Mathf.Max(1f,
                    requested > Rpm ? s.engineFreeRevRiseRate : s.engineFreeRevFallRate) * dt);
                Rpm = Mathf.Clamp(Rpm, s.engineIdleRpm, limit);
                IsAtRevLimiter = pedal >= 0.98f && Rpm >= limit - 50f;
                _limiterPhase = Mathf.Repeat(_limiterPhase + dt * Mathf.Max(1f, s.engineLimiterFrequency), 1f);
                RevLimiterGain = IsAtRevLimiter && _limiterPhase < 0.45f ? Mathf.Clamp01(s.engineLimiterRetention) : 1f;
                return 0f; // Revving a disengaged engine never grants wheel thrust.
            }
            ShiftRemaining = Mathf.Max(0f, ShiftRemaining - dt);
            _holdRemaining = Mathf.Max(0f, _holdRemaining - dt);
            int count = s.forwardGearSpeedFractions == null ? 0 : s.forwardGearSpeedFractions.Length;
            if (!s.automaticGears || count == 0) { Gear = 1; ShiftRemaining = 0f; }
            else
            {
                Gear = Mathf.Clamp(Gear, 1, count);
                if (reverse) { Gear = 1; ShiftRemaining = _holdRemaining = 0f; }
                else if (grounded && ShiftRemaining <= 0f && _holdRemaining <= 0f)
                {
                    float fraction = Mathf.Max(0f, speed) / Mathf.Max(0.1f, s.cruiseSpeed);
                    int next = Gear;
                    if (Gear < count && fraction >= Limit(s, Gear - 1)) next++;
                    else if (Gear > 1 && fraction < Limit(s, Gear - 2) * (1f - Mathf.Clamp(s.downshiftHysteresis, 0.05f, 0.8f))) next--;
                    if (next != Gear)
                    {
                        _shiftFromRpm = Rpm;
                        Gear = next; ShiftRemaining = _shiftDuration = Mathf.Max(0f, s.gearShiftTime);
                        _holdRemaining = ShiftRemaining + Mathf.Max(0f, s.gearMinimumHoldTime); ShiftCount++;
                    }
                }
            }
            float ceiling = reverse ? s.reverseSpeed : s.cruiseSpeed * (count > 0 ? Limit(s, Gear - 1) : 1f);
            float revFraction = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.1f, ceiling));
            if (!grounded) revFraction = Mathf.Max(revFraction, pedal);
            float targetRpm = Mathf.Lerp(s.engineIdleRpm, Mathf.Max(s.engineIdleRpm + 1f, s.engineRedlineRpm), revFraction);
            targetRpm += Mathf.Clamp01(pedal) * Mathf.Max(0f, s.engineLoadRpm) * (1f - revFraction * 0.6f);
            targetRpm = Mathf.Min(Mathf.Max(s.engineIdleRpm + 1f, s.engineRedlineRpm), targetRpm);
            if (ShiftRemaining > 0f)
            {
                float revDrop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.85f, ShiftProgress));
                Rpm = Mathf.Lerp(_shiftFromRpm, targetRpm, revDrop);
            }
            else Rpm = Mathf.MoveTowards(Rpm, targetRpm,
                Mathf.Max(1f, Rpm > s.engineRedlineRpm ? s.engineFreeRevFallRate : s.engineRpmResponse) * dt);
            if (reverse || !s.automaticGears) return 1f;
            float torque = s.forwardGearThrust != null && s.forwardGearThrust.Length >= Gear
                ? Mathf.Max(0f, s.forwardGearThrust[Gear - 1]) : 1f;
            return torque * ThrustEnvelope(s);
        }

        private static float Limit(CrazyTaxiSettings s, int index)
        {
            // Enforce ordered bands even while an author edits the array live.
            float limit = 0.05f;
            for (int i = 0; i <= index; i++) limit = Mathf.Max(limit + 0.01f, s.forwardGearSpeedFractions[i]);
            return limit;
        }
    }
}
