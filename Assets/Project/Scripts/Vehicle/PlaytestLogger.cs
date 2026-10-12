using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZoomZoom.Orders;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// Writes one row of measured playtest data per shift, so the measured half of
    /// docs/playtesting/playtest_sessions.csv comes from the game and not from memory.
    ///
    /// WHY THIS EXISTS
    /// The playtest sheet keeps two kinds of column apart: what the tester DID (delivered, wall
    /// hits, boost seconds) and what the tester SAID (the q_ columns). The point of the split is
    /// that they disagree, and the disagreement is the finding: a tester who scores "in control
    /// at speed" a 5 and has eleven wall hits is telling you something neither number says alone.
    /// That only works if the measured side is actually measured. An observer counting crashes
    /// by eye while also running the question script gets both wrong.
    ///
    /// WHAT IT READS AND WHAT IT NEVER TOUCHES
    /// Public state on VehicleController and VehicleJumpFlip, and OrderManager.OrderResolved, the
    /// same event OrderHUD and ShiftResults read. It subscribes and reads; it never calls a method
    /// that changes anything, so it can be removed from a scene without consequence. The order
    /// system is not this component's to change.
    ///
    /// WHAT COUNTS AS A WALL HIT
    /// The same rule the measurement harness uses: a drop of more than impactSpeedDrop in a single
    /// physics step, from above a minimum speed. Two systems with two definitions of "crash" would
    /// put two different numbers in the design document for the same event. Recovery ends the same
    /// way the harness ends it too: grounded, upright, and back above usefulSpeed.
    ///
    /// WHERE THE FILE GOES
    /// PlaytestLogs/shift_log.csv beside the Assets folder, never inside it, so Unity does not
    /// import it as an asset and the editor does not reimport on every shift. In a built player
    /// Application.dataPath is the _Data folder, so it lands beside the executable instead.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaytestLogger : MonoBehaviour
    {
        [Header("Wiring (all found automatically if left empty)")]
        [SerializeField] private VehicleController car;
        [SerializeField] private VehicleJumpFlip jumpFlip;
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private ShiftTimer shiftTimer;

        [Header("Thresholds, matched to VehicleMeasurement")]
        [Tooltip("A drop in speed larger than this in one physics step counts as hitting something. " +
                 "3 m/s is the harness's value; at 120 Hz even a gentle wall is far bigger.")]
        [SerializeField] private float impactSpeedDrop = 3f;

        [Tooltip("Only count an impact if the car was going at least this fast. Stops a parking " +
                 "nudge against a kerb reading as a crash.")]
        [SerializeField] private float impactMinimumSpeed = 10f;

        [Tooltip("Forward speed at which the car counts as driving usefully again after an impact. " +
                 "8 m/s is the harness's value.")]
        [SerializeField] private float usefulSpeed = 8f;

        [Header("Output")]
        [Tooltip("Folder beside Assets. Kept outside Assets so Unity never imports the log.")]
        [SerializeField] private string folderName = "PlaytestLogs";
        [SerializeField] private string fileName = "shift_log.csv";
        [SerializeField] private bool alsoLogToConsole = true;

        // Tally, reset at shift start.
        private int _delivered;
        private int _late;
        private float _value;
        private int _boostActivations;
        private float _boostSeconds;
        private float _boostEarnedAtStart;
        private float _handbrakeSeconds;
        private int _wallHits;
        private int _flipsAtStart;
        private float _supersonicSeconds;
        private float _longestRecovery;
        private float _shiftStartTime;

        // Per-step state.
        private float _previousSpeed;
        private bool _wasBoosting;
        private bool _recovering;
        private float _recoveryStart;
        private bool _shiftRunning;
        private bool _rowWritten;

        private const string Header =
            "timestamp,scene,shift_seconds,play_seconds,delivered,late,value,boost_activations," +
            "boost_seconds,boost_earned,handbrake_seconds,wall_hits,flips,time_above_supersonic_s," +
            "longest_recovery_s";

        private void Awake()
        {
            if (car == null) car = GetComponent<VehicleController>();
            if (car == null) car = FindAnyObjectByType<VehicleController>();
            if (jumpFlip == null && car != null) jumpFlip = car.GetComponent<VehicleJumpFlip>();
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (shiftTimer == null) shiftTimer = FindAnyObjectByType<ShiftTimer>();

            if (car == null || orderManager == null || shiftTimer == null)
            {
                Debug.LogWarning(
                    "[PlaytestLogger] Needs a VehicleController, an OrderManager and a ShiftTimer in " +
                    "the scene. One is missing, so no shift log will be written. The measured " +
                    "columns in the playtest sheet stay blank for this session.", this);
                enabled = false;
                return;
            }

            orderManager.OrderResolved += HandleOrderResolved;
            shiftTimer.ShiftStarted += HandleShiftStarted;
            shiftTimer.ShiftEnded += HandleShiftEnded;

            // ShiftTimer starts the shift in its own Awake when startAutomatically is on, which may
            // have run before this Awake subscribed. Catch that case rather than miss the shift.
            if (shiftTimer.IsShiftActive) HandleShiftStarted();
        }

        private void OnDestroy()
        {
            if (orderManager != null) orderManager.OrderResolved -= HandleOrderResolved;
            if (shiftTimer != null)
            {
                shiftTimer.ShiftStarted -= HandleShiftStarted;
                shiftTimer.ShiftEnded -= HandleShiftEnded;
            }

            // A shift cut short by stopping Play mode still gets its row, flagged by a shorter
            // play_seconds than shift_seconds. Losing the data because the tester quit early is
            // worse than a row that says they quit early.
            if (_shiftRunning && !_rowWritten) WriteRow();
        }

        private void HandleShiftStarted()
        {
            _delivered = 0;
            _late = 0;
            _value = 0f;
            _boostActivations = 0;
            _boostSeconds = 0f;
            _boostEarnedAtStart = car.TotalBoostEarned;
            _handbrakeSeconds = 0f;
            _wallHits = 0;
            _flipsAtStart = jumpFlip != null ? jumpFlip.FlipCount : 0;
            _supersonicSeconds = 0f;
            _longestRecovery = 0f;
            _shiftStartTime = Time.time;

            _previousSpeed = car.Speed;
            _wasBoosting = false;
            _recovering = false;
            _shiftRunning = true;
            _rowWritten = false;
        }

        private void HandleShiftEnded()
        {
            if (!_shiftRunning) return;
            WriteRow();
        }

        private void HandleOrderResolved(Order order, float value)
        {
            if (!_shiftRunning) return;

            if (order.State == OrderState.Delivered)
            {
                _delivered++;
                _value += value;
            }
            else
            {
                _late++;
            }
        }

        private void FixedUpdate()
        {
            if (!_shiftRunning) return;

            float dt = Time.fixedDeltaTime;
            float speed = car.Speed;

            // Boost: count presses that actually spent fuel, and the seconds spent.
            if (car.IsBoosting)
            {
                _boostSeconds += dt;
                if (!_wasBoosting) _boostActivations++;
            }
            _wasBoosting = car.IsBoosting;

            if (car.Drive.handbrake) _handbrakeSeconds += dt;
            if (car.IsSupersonic) _supersonicSeconds += dt;

            // Wall hit: the harness's rule. A drop this big in one step can only be a collision.
            bool hit = _previousSpeed - speed > impactSpeedDrop && _previousSpeed >= impactMinimumSpeed;
            if (hit && !_recovering)
            {
                _wallHits++;
                _recovering = true;
                _recoveryStart = Time.time;
            }
            else if (hit && _recovering)
            {
                // A second impact during recovery is still a hit, but the recovery clock keeps
                // running from the first one: the player is still not back in the game.
                _wallHits++;
            }

            if (_recovering)
            {
                bool drivingUsefully = car.IsGrounded && car.IsUpright && car.ForwardSpeed >= usefulSpeed;
                if (drivingUsefully)
                {
                    float recovery = Time.time - _recoveryStart;
                    if (recovery > _longestRecovery) _longestRecovery = recovery;
                    _recovering = false;
                }
            }

            _previousSpeed = speed;
        }

        // ==================================================================
        // OUTPUT
        // ==================================================================

        private void WriteRow()
        {
            _rowWritten = true;
            _shiftRunning = false;

            // A recovery still in progress at the whistle counts for as long as it lasted.
            if (_recovering)
            {
                float recovery = Time.time - _recoveryStart;
                if (recovery > _longestRecovery) _longestRecovery = recovery;
            }

            float playSeconds = Time.time - _shiftStartTime;
            float shiftSeconds = shiftTimer != null ? ShiftLengthSeconds() : 0f;
            int flips = jumpFlip != null ? jumpFlip.FlipCount - _flipsAtStart : 0;
            float boostEarned = car.TotalBoostEarned - _boostEarnedAtStart;

            var inv = CultureInfo.InvariantCulture;
            string row = string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", inv),
                Csv(SceneManager.GetActiveScene().name),
                shiftSeconds.ToString("0", inv),
                playSeconds.ToString("0.0", inv),
                _delivered.ToString(inv),
                _late.ToString(inv),
                _value.ToString("0", inv),
                _boostActivations.ToString(inv),
                _boostSeconds.ToString("0.0", inv),
                boostEarned.ToString("0", inv),
                _handbrakeSeconds.ToString("0.0", inv),
                _wallHits.ToString(inv),
                flips.ToString(inv),
                _supersonicSeconds.ToString("0.0", inv),
                _longestRecovery.ToString("0.0", inv));

            string path = OutputPath();
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                bool needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
                using (var writer = new StreamWriter(path, append: true, Encoding.UTF8))
                {
                    if (needsHeader) writer.WriteLine(Header);
                    writer.WriteLine(row);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlaytestLogger] Could not write {path}: {e.Message}. The row is in " +
                               "the console below; copy it by hand.", this);
            }

            if (alsoLogToConsole)
            {
                Debug.Log(
                    "[PlaytestLogger] SHIFT ROW (paste into docs/playtesting/playtest_sessions.csv, " +
                    "measured columns):\n" +
                    $"delivered {_delivered}, late {_late}, value {_value:0}, " +
                    $"boost {_boostActivations}x / {_boostSeconds:0.0} s / earned {boostEarned:0}, " +
                    $"handbrake {_handbrakeSeconds:0.0} s, wall hits {_wallHits}, flips {flips}, " +
                    $"supersonic {_supersonicSeconds:0.0} s, longest recovery {_longestRecovery:0.0} s\n" +
                    $"written to {path}");
            }
        }

        private float ShiftLengthSeconds()
        {
            // ShiftTimer does not expose its configured length, only TimeRemaining. At shift end
            // that is 0, so the length is what elapsed. For an early quit, play_seconds is shorter
            // and the difference is the signal.
            return Mathf.Round(Time.time - _shiftStartTime + shiftTimer.TimeRemaining);
        }

        private string OutputPath()
        {
            // dataPath is <project>/Assets in the editor and <game>_Data in a player. One level up
            // from either is a sensible, writable place that Unity does not import from.
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(root, folderName, fileName);
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
