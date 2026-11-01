using System;

namespace ZoomZoom.Vehicle
{
    public enum TaxiGear { Drive, Reverse }
    public enum TaxiCommand { Drive, Reverse, GasDown, GasUp, BrakeDown, BrakeUp, Steer, QuickSpin, AutoDrive, AutoReverse }
    public enum TaxiTrick { None, Dash, Drift, Stop, LimiterCut, BackDash, BackDrift, QuickSpin }

    /// <summary>Consumes ordered input events, including taps between two physics steps.</summary>
    public sealed class TaxiTrickRecognizer
    {
        private double _window;
        private double _minimumGap;
        private double _driveAt, _reverseAt;
        private double _gasAt;
        private bool _gas, _brake, _dashArmed, _cutArmed;
        private double _lastTime;
        private double _driftAt;
        private TaxiTrick _pendingDrift;
        public TaxiGear Gear { get; private set; }

        public TaxiTrickRecognizer(double window, double minimumGap)
        {
            _window = window;
            _minimumGap = minimumGap;
            Reset();
        }
        public void Configure(double window, double minimumGap)
        {
            _window = Math.Max(0.01, window);
            _minimumGap = Math.Max(0, Math.Min(minimumGap, _window));
        }

        public void Reset()
        {
            _driveAt = _reverseAt = double.NegativeInfinity;
            _gasAt = double.NegativeInfinity;
            _lastTime = double.NegativeInfinity;
            _driftAt = double.NegativeInfinity;
            _pendingDrift = TaxiTrick.None;
            _gas = _brake = _dashArmed = _cutArmed = false;
            Gear = TaxiGear.Drive;
        }

        private bool Recent(double now, double before) =>
            now - before >= _minimumGap && now - before <= _window;
        private bool Together(double now, double before) => now >= before && now - before <= _window;

        public TaxiTrick Push(TaxiCommand command, double time, bool grounded,
            bool movingForward, bool steering, bool dashStillFast)
        {
            if (time < _lastTime) return TaxiTrick.None;
            _lastTime = time;
            TaxiTrick result = TaxiTrick.None;
            switch (command)
            {
                case TaxiCommand.AutoDrive:
                case TaxiCommand.AutoReverse:
                    Gear = command == TaxiCommand.AutoDrive ? TaxiGear.Drive : TaxiGear.Reverse;
                    _driveAt = _reverseAt = _driftAt = double.NegativeInfinity;
                    _dashArmed = _cutArmed = false;
                    _pendingDrift = TaxiTrick.None;
                    break;
                case TaxiCommand.QuickSpin:
                    Gear = TaxiGear.Drive;
                    _driveAt = _reverseAt = double.NegativeInfinity;
                    _dashArmed = _cutArmed = false;
                    result = TaxiTrick.QuickSpin;
                    break;
                case TaxiCommand.GasUp:
                    _gas = false;
                    break;
                case TaxiCommand.BrakeUp:
                    _brake = false;
                    break;
                case TaxiCommand.BrakeDown:
                    _brake = true;
                    _dashArmed = false;
                    if (Gear == TaxiGear.Reverse && movingForward && Together(time, _reverseAt))
                        result = TaxiTrick.Stop;
                    break;
                case TaxiCommand.Reverse:
                    Gear = TaxiGear.Reverse;
                    _reverseAt = time;
                    _dashArmed = false;
                    if (movingForward && Recent(time, _driveAt))
                    {
                        _pendingDrift = TaxiTrick.BackDrift;
                        _driftAt = time;
                        if (steering) result = _pendingDrift;
                    }
                    if (result == TaxiTrick.None && _brake && movingForward) result = TaxiTrick.Stop;
                    else if (result == TaxiTrick.None && dashStillFast && _gas) result = TaxiTrick.BackDash;
                    break;
                case TaxiCommand.Drive:
                    Gear = TaxiGear.Drive;
                    _driveAt = time;
                    _dashArmed = !_gas && !_brake;
                    _cutArmed = _dashArmed && dashStillFast && Recent(time, _reverseAt);
                    if (movingForward && Recent(time, _reverseAt))
                    {
                        _pendingDrift = TaxiTrick.Drift;
                        _driftAt = time;
                        if (steering) result = _pendingDrift;
                    }
                    if (result == TaxiTrick.None && _gas && !_brake && Together(time, _gasAt))
                    {
                        result = dashStillFast && Recent(time, _reverseAt) ? TaxiTrick.LimiterCut : TaxiTrick.Dash;
                        _gasAt = double.NegativeInfinity;
                        _dashArmed = _cutArmed = false;
                    }
                    break;
                case TaxiCommand.Steer:
                    if (movingForward && steering && Recent(time, _driftAt)) result = _pendingDrift;
                    break;
                case TaxiCommand.GasDown:
                    if (_gas) break;
                    _gas = true;
                    _gasAt = time;
                    if (_dashArmed && !_brake && Gear == TaxiGear.Drive && Together(time, _driveAt))
                    {
                        result = _cutArmed ? TaxiTrick.LimiterCut : TaxiTrick.Dash;
                        _gasAt = double.NegativeInfinity;
                    }
                    _dashArmed = _cutArmed = false; // a pattern is spent once, never replayed
                    break;
            }
            if (result != TaxiTrick.None) _pendingDrift = TaxiTrick.None;
            return grounded ? result : TaxiTrick.None;
        }
    }
}
