using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// On-screen numbers and scene-view vectors for what the car is actually doing.
    ///
    /// WHY THIS EXISTS
    /// Tuning a car by feel alone means guessing. "The rear did not step out" has at least four
    /// possible causes: the steering never asked for enough, the grip budget was too big, the
    /// longitudinal demand was too small to eat into it, or the surface multiplier was carrying the
    /// car. Those look identical from the driving seat and are trivially distinguishable here.
    ///
    /// The line that matters most is GRIP. It shows what the steering is asking for against what the
    /// tyres can actually give. When demand exceeds available grip the car is sliding, and by how much
    /// tells you whether it is a hint of rotation or a spin.
    ///
    /// Reads only. Nothing here can change how the car drives.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleTelemetry : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The car being measured. Found on this object if left empty.")]
        [SerializeField] private VehicleController car;

        [Header("Display")]
        [Tooltip("Show the readout. Toggled at runtime with the key below.")]
        [SerializeField] private bool show = true;

        // F10 rather than F9, because VehicleMeasurement already uses F9 for its own developer
        // overlay. One key toggling two overlays meant you could never see one without the other.
        [Tooltip("Key that shows and hides the readout. F1 to F9 belong to VehicleMeasurement, so " +
                 "pick something outside that range.")]
        [SerializeField] private Key toggleKey = Key.F10;

        [Tooltip("Draw velocity and acceleration arrows in the scene view. Green is forward velocity, " +
                 "red is sideways velocity, blue is acceleration.")]
        [SerializeField] private bool drawVectors = true;

        // ---------------- state ----------------

        private Vector3 _previousVelocity;
        private float _acceleration;
        private GUIStyle _style;
        private GUIStyle _shadow;

        private void Awake()
        {
            if (car == null) car = GetComponent<VehicleController>();
            if (car == null) enabled = false;
        }

        private void Update()
        {
            // Device read rather than UnityEngine.Input: active input handling is the Input System
            // package, and the legacy class throws on every call under that setting.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) show = !show;
        }

        private void FixedUpdate()
        {
            if (car?.Body == null) return;

            // Measured rather than read from the tuning, so this reports what the car DID, not what it
            // was asked to do. Those two disagreeing is exactly the kind of bug worth catching.
            Vector3 velocity = car.Body.linearVelocity;
            _acceleration = (velocity - _previousVelocity).magnitude / Mathf.Max(0.0001f, Time.fixedDeltaTime);
            _previousVelocity = velocity;
        }

        private void OnGUI()
        {
            if (!show || car == null) return;

            BuildStyles();

            VehicleTuning tuning = car.Tuning;
            float demand = DemandedLateralAcceleration();
            float available = car.AvailableLateralGrip;
            bool overGrip = demand > available && car.IsGrounded;

            string text =
                $"SPEED       {car.Speed:0.0} m/s   ({car.Speed * 3.6f:0} km/h)\n" +
                $"  forward   {car.ForwardSpeed:0.0} m/s\n" +
                $"  lateral   {car.LateralSpeed:0.0} m/s\n" +
                $"  accel     {_acceleration:0.0} m/s2\n" +
                "\n" +
                $"THROTTLE    {Bar(car.Drive.throttle)}  {car.Drive.throttle:+0.00;-0.00; 0.00}\n" +
                $"STEER       {Bar(car.Drive.steer)}  {car.Drive.steer:+0.00;-0.00; 0.00}\n" +
                $"BRAKE       {Bar(car.Drive.brake)}  {car.Drive.brake:0.00}\n" +
                $"HANDBRAKE   {(car.Drive.handbrake ? "ON" : "off")}\n" +
                $"BOOST       {(car.IsBoosting ? "ON " : "off")} {car.BoostRemaining:0}%" +
                $"{(car.IsSupersonic ? "   SUPERSONIC" : "")}\n" +
                "\n" +
                $"GRIP        asking {demand:0.0} / have {available:0.0} m/s2   " +
                $"{(overGrip ? ">>> SLIDING <<<" : "within grip")}\n" +
                $"  long dmd  {car.LongitudinalDemand:0.0} m/s2 " +
                $"{(tuning != null && tuning.useFrictionCircle ? "(eating lateral grip)" : "(circle OFF)")}\n" +
                $"SLIP ANGLE  {car.SlipAngle:0.0} deg   {(car.IsDrifting ? "DRIFTING" : "")}\n" +
                "\n" +
                $"YAW RATE    {car.YawRate:+0.00;-0.00; 0.00} rad/s\n" +
                $"ANG VEL     {(car.Body != null ? car.Body.angularVelocity.magnitude : 0f):0.00} rad/s\n" +
                $"TURN RADIUS {TurnRadiusText()}\n" +
                "\n" +
                $"GROUNDED    {car.WheelsOnGround}/4 wheels\n" +
                $"SURFACE     {car.Surface.kind}  grip x{car.Surface.gripMultiplier:0.00}\n" +
                $"COM         {(tuning != null ? tuning.centreOfMassOffset.ToString("0.00") : "-")}\n" +
                $"SUSPENSION  {CompressionText()}\n" +
                "\n" +
                $"PHYSICS     {1f / Mathf.Max(0.0001f, Time.fixedDeltaTime):0} Hz\n" +
                $"[{toggleKey}] hide";

            var area = new Rect(12f, 12f, 430f, 640f);

            // Drawn twice, offset by a pixel, so the text stays readable against a pale road as well
            // as a dark one. Cheaper and more reliable than a background box.
            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text, _shadow);
            GUI.Label(area, text, _style);
        }

        /// <summary>
        /// What the steering is currently asking the tyres for, m/s^2. A corner of curvature c at speed
        /// v needs v*v*c of lateral acceleration, which is the same arithmetic the controller uses.
        /// </summary>
        private float DemandedLateralAcceleration()
        {
            float v = Mathf.Abs(car.ForwardSpeed);
            return v * v * Mathf.Abs(car.SteerCurvature);
        }

        private string TurnRadiusText()
        {
            float curvature = Mathf.Abs(car.SteerCurvature);
            if (curvature < 0.0005f) return "straight";
            return $"{1f / curvature:0.0} m";
        }

        private string CompressionText()
        {
            var parts = new string[VehicleController.WheelCount];
            for (int i = 0; i < VehicleController.WheelCount; i++)
            {
                parts[i] = $"{car.GetWheelVisualState(i).compression01:0.00}";
            }
            return string.Join("  ", parts);
        }

        /// <summary>Ten-cell bar for a -1..1 or 0..1 input, so a glance shows how much is being asked.</summary>
        private static string Bar(float value)
        {
            const int cells = 10;
            int filled = Mathf.RoundToInt(Mathf.Clamp01(Mathf.Abs(value)) * cells);
            return "[" + new string('|', filled) + new string('.', cells - filled) + "]";
        }

        private void BuildStyles()
        {
            if (_style != null) return;

            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };

            // Monospace keeps the numbers in fixed columns, so a value changing does not shift the
            // whole block sideways and make it unreadable while driving.
            _style.font = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Courier New", "Menlo", "monospace" }, 14);
            _style.normal.textColor = Color.white;

            _shadow = new GUIStyle(_style);
            _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }

        private void OnDrawGizmos()
        {
            if (!drawVectors || car?.Body == null || !Application.isPlaying) return;

            Vector3 origin = car.transform.position + Vector3.up * 0.5f;

            // Green: where the car is going along its nose.
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, origin + car.GroundForward * car.ForwardSpeed * 0.2f);

            // Red: how much of the motion is sideways. A long red line is a drift.
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, origin + car.GroundRight * car.LateralSpeed * 0.2f);

            // Blue: acceleration, so throttle and brake are visible as well as inferred.
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(origin, origin + car.Body.linearVelocity.normalized * _acceleration * 0.1f);

            // Yellow ring: the corner the steering is currently describing.
            float curvature = Mathf.Abs(car.SteerCurvature);
            if (curvature > 0.0005f)
            {
                float radius = 1f / curvature;
                Vector3 centre = car.transform.position
                                 + car.GroundRight * Mathf.Sign(car.SteerCurvature) * radius;

                Gizmos.color = Color.yellow;
                Vector3 previous = centre + car.GroundForward * radius;
                for (int i = 1; i <= 48; i++)
                {
                    float angle = (i / 48f) * Mathf.PI * 2f;
                    Vector3 point = centre
                                    + (car.GroundForward * Mathf.Cos(angle) + car.GroundRight * Mathf.Sin(angle))
                                    * radius;
                    Gizmos.DrawLine(previous, point);
                    previous = point;
                }
            }
        }
    }
}
