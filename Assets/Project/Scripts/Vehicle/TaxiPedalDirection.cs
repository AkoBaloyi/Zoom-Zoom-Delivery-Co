using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>Brake-to-reverse convenience; explicit D/R gestures remain separate technique inputs.</summary>
    public sealed class TaxiPedalDirection
    {
        public TaxiGear Direction { get; private set; } = TaxiGear.Drive;
        private float _standstillTime;
        public void Reset() { Direction = TaxiGear.Drive; _standstillTime = 0f; }

        public DriveInput Resolve(CrazyTaxiSettings s, float gas, float brake, float forwardSpeed,
            float planarSpeed, bool grounded, float dt, out bool changed)
        {
            changed = false;
            gas = Mathf.Clamp01(gas); brake = Mathf.Clamp01(brake);
            if (!s.brakeToReverse)
                return new DriveInput { throttle = gas * (Direction == TaxiGear.Reverse ? -1f : 1f), brake = brake };
            float threshold = Mathf.Clamp(s.directionPedalThreshold, 0.001f, 0.5f);
            bool gasHeld = gas > threshold, brakeHeld = brake > threshold;
            if (gasHeld && brakeHeld) { _standstillTime = 0f; return new DriveInput { brake = brake }; }
            if (!gasHeld && !brakeHeld) { _standstillTime = 0f; return default; }

            TaxiGear wanted = brakeHeld ? TaxiGear.Reverse : TaxiGear.Drive;
            float pedal = brakeHeld ? brake : gas;
            bool movingAgainst = wanted == TaxiGear.Drive ? forwardSpeed < -s.reverseEngageSpeed : forwardSpeed > s.reverseEngageSpeed;
            if (wanted != Direction)
            {
                bool still = grounded && planarSpeed <= Mathf.Max(0.01f, s.reverseEngageSpeed);
                _standstillTime = still ? _standstillTime + Mathf.Max(0f, dt) : 0f;
                if (_standstillTime < Mathf.Max(0f, s.reverseEngageDelay) || !still)
                    return new DriveInput { brake = pedal };
                Direction = wanted;
                _standstillTime = 0f;
                changed = true;
            }
            else _standstillTime = 0f;
            if (movingAgainst) return new DriveInput { brake = pedal };
            return new DriveInput { throttle = pedal * (Direction == TaxiGear.Reverse ? -1f : 1f) };
        }
    }
}
