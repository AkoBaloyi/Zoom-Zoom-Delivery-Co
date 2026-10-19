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

        [Tooltip("Forward speed below minus this counts as reversing. 0.5 m/s is the controller's own " +
                 "reverse gate in ApplyDriveAndBrake, so the column counts the same thing the car does.")]
        [SerializeField] private float reverseSpeedThreshold = 0.5f;

        [Tooltip("Speed below which the car counts as crawling. The column is named seconds_below_2mps, " +
                 "so changing this makes the name a lie - change the name too.")]
        [SerializeField] private float crawlSpeedThreshold = 2f;

        [Tooltip("Brake input above which the brake counts as held. This is the dedicated Brake action " +
                 "(Left Ctrl / gamepad B), not S: the open question is whether players ever find the " +
                 "brake that does not reverse, and only this action answers it.")]
        [SerializeField] private float brakeInputThreshold = 0.05f;

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

        // Reverse and recovery. Nothing in this logger recorded reversing before, so the whole reverse
        // investigation had to be arithmetic off the tuning numbers instead of measurement. These five
        // close that: how long the car spends backwards and how far it gets, how long it spends going
        // nowhere, whether the dedicated brake is ever used, and how often a tester ends up needing the
        // self-right.
        private float _reverseSeconds;
        private float _reverseDistance;
        private float _secondsBelowCrawl;
        private float _brakeSeconds;
        private int _selfRightsAtStart;

        // Captured once at shift start instead of reconstructed at the end. WriteRow can run from
        // OnDestroy with the ShiftTimer component already destroyed, Unity reports a destroyed
        // component as null, and the old code then wrote shift_seconds 0 - which is exactly what two
        // of the logged rows show. ShiftTimer.StartShift sets TimeRemaining = shiftDurationSeconds on
        // the line before it fires ShiftStarted, so the value read in HandleShiftStarted IS the
        // configured length. ShiftTimer lives in Assets/Project/Scripts/Orders/ and belongs to a
        // teammate, so it is not given a getter for that length; capturing it here costs nothing.
        private float _shiftLengthSeconds;

        // Accumulated in FixedUpdate rather than computed as Time.time minus a start stamp. The stamp
        // was re-taken by every HandleShiftStarted call, including the catch-up call in Awake, so a
        // logger that attached mid-session wrote play_seconds 0.0 against a clock that still had
        // minutes on it. A sum of physics steps cannot be poisoned by a stale stamp.
        private float _playSeconds;

        // Per-step state.
        private float _previousSpeed;
        private bool _wasBoosting;
        private bool _recovering;
        private float _recoveryStart;
        private bool _shiftRunning;
        private bool _rowWritten;
        private bool _attachedMidShift;
        private bool _headerDriftWarned;

        // Twenty columns. The first fifteen are byte-for-byte the ones every logged row already uses
        // and the five reverse/recovery ones are APPENDED, never inserted: seven historical rows across
        // the two shift logs were written in that order, and docs/tooling/playtest_row.py reads the log
        // by header name, so appending is what keeps all of them readable.
        private const string Header =
            "timestamp,scene,shift_seconds,play_seconds,delivered,late,value,boost_activations," +
            "boost_seconds,boost_earned,handbrake_seconds,wall_hits,flips,time_above_supersonic_s," +
            "longest_recovery_s,reverse_seconds,reverse_distance_m,seconds_below_2mps,brake_seconds," +
            "self_right_count";

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
            if (shiftTimer.IsShiftActive)
            {
                // The ordinary case is both Awakes running on the first frame, where nothing was
                // missed. A later frame means this logger attached part-way through a shift that was
                // already under way, and then both duration fields describe only the part of the
                // shift it saw. The configured length cannot be recovered from here - ShiftTimer
                // exposes TimeRemaining and nothing else, and it is off limits - so the row says what
                // it measured and the console says why it is short. Honest rather than silently wrong.
                _attachedMidShift = Time.frameCount > 1;
                HandleShiftStarted();

                if (_attachedMidShift)
                {
                    Debug.LogWarning(
                        "[PlaytestLogger] Attached part-way through a shift that was already running. " +
                        "play_seconds and shift_seconds in this row cover only the part of the shift " +
                        "this logger saw, not the configured shift length.", this);
                }
            }
        }

        private void OnDestroy()
        {
            if (orderManager != null) orderManager.OrderResolved -= HandleOrderResolved;
            if (shiftTimer != null)
            {
                shiftTimer.ShiftStarted -= HandleShiftStarted;
                shiftTimer.ShiftEnded -= HandleShiftEnded;
            }

            // A shift cut short by stopping Play mode still gets its row. It is flagged by a
            // play_seconds shorter than shift_seconds, which is what makes an abandoned run
            // distinguishable from a completed one: a full shift writes the two within rounding of
            // each other, a quit at halfway writes half. That claim was false while play_seconds came
            // from a start stamp that could be re-taken, and it is true now that the clock is
            // accumulated and the length is captured at the start. Losing the data because the tester
            // quit early is worse than a row that says they quit early.
            if (_shiftRunning && !_rowWritten) WriteRow();
        }

        private void OnApplicationQuit()
        {
            // The third exit path. A built player closed with the window button quits without
            // necessarily running OnDestroy on this component first, so the row would be lost. WriteRow
            // sets _rowWritten and clears _shiftRunning, so whichever of the three paths arrives first
            // writes the row and the other two do nothing.
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
            _reverseSeconds = 0f;
            _reverseDistance = 0f;
            _secondsBelowCrawl = 0f;
            _brakeSeconds = 0f;
            _selfRightsAtStart = jumpFlip != null ? jumpFlip.SelfRightCount : 0;
            _shiftLengthSeconds = shiftTimer != null ? shiftTimer.TimeRemaining : 0f;
            _playSeconds = 0f;

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

            // The play clock, summed one step at a time so it only ever counts seconds this logger
            // was actually running for.
            _playSeconds += dt;

            if (car.Drive.handbrake) _handbrakeSeconds += dt;
            if (car.IsSupersonic) _supersonicSeconds += dt;

            // Reversing, by the controller's own definition of it. ApplyDriveAndBrake treats forward
            // speed below -0.5 m/s as rolling backwards, so the column counts what the car counts and a
            // nudge back off a kerb does not read as a three-point turn. The distance is the integral of
            // the speed rather than a position delta: it answers "how far backwards did they have to
            // go", which a straight-line start-to-end measurement would not.
            if (car.ForwardSpeed < -reverseSpeedThreshold)
            {
                _reverseSeconds += dt;
                _reverseDistance += Mathf.Abs(car.ForwardSpeed) * dt;
            }

            // Time spent going nowhere. This is the real cost of a bad recovery, and no column measured
            // it: a tester who spent ten seconds shuffling off a wall and one who drove the same route
            // cleanly wrote rows that looked alike apart from the delivery count.
            if (speed < crawlSpeedThreshold) _secondsBelowCrawl += dt;

            // The dedicated Brake action only, not S. S is reverse throttle, which brakes first and then
            // reverses, so a tester can stop the car for a whole shift without ever touching the brake.
            // Near-zero brake seconds beside real reverse seconds is what says they never found it.
            if (car.Drive.brake > brakeInputThreshold) _brakeSeconds += dt;

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

            int flips = jumpFlip != null ? jumpFlip.FlipCount - _flipsAtStart : 0;
            int selfRights = jumpFlip != null ? jumpFlip.SelfRightCount - _selfRightsAtStart : 0;
            float boostEarned = car.TotalBoostEarned - _boostEarnedAtStart;

            var inv = CultureInfo.InvariantCulture;
            string row = string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", inv),
                Csv(SceneManager.GetActiveScene().name),
                _shiftLengthSeconds.ToString("0", inv),
                _playSeconds.ToString("0.0", inv),
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
                _longestRecovery.ToString("0.0", inv),
                _reverseSeconds.ToString("0.0", inv),
                _reverseDistance.ToString("0.0", inv),
                _secondsBelowCrawl.ToString("0.0", inv),
                _brakeSeconds.ToString("0.0", inv),
                selfRights.ToString(inv));

            string path = OutputPath();
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                bool needsHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
                if (!needsHeader) WarnIfHeaderDrifted(path);
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
                    $"supersonic {_supersonicSeconds:0.0} s, longest recovery {_longestRecovery:0.0} s, " +
                    $"reverse {_reverseSeconds:0.0} s / {_reverseDistance:0.0} m, " +
                    $"below {crawlSpeedThreshold:0} m/s for {_secondsBelowCrawl:0.0} s, " +
                    $"brake {_brakeSeconds:0.0} s, self rights {selfRights}\n" +
                    $"written to {path}");
            }
        }

        private void WarnIfHeaderDrifted(string path)
        {
            if (_headerDriftWarned) return;

            // The header is only written when the file is new or empty, so a log started before these
            // columns existed keeps its fifteen-column header while new rows carry twenty fields.
            // Appending is what makes that survivable: every old column is still at the same index, so
            // docs/tooling/playtest_row.py, which reads by header name, still parses every historical
            // row correctly - the old rows simply have nothing to say about reverse. So warn once and
            // leave the file alone. A shift log is evidence of a session that happened, and rewriting,
            // moving or deleting it to tidy the header would destroy data to fix a cosmetic problem.
            string firstLine;
            using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                // The log is written with Encoding.UTF8, which emits a byte-order mark, so the reader
                // has to detect it or the first column name would arrive with invisible bytes attached.
                firstLine = reader.ReadLine();
            }

            if (string.IsNullOrEmpty(firstLine) || firstLine.Trim() == Header) return;

            _headerDriftWarned = true;
            Debug.LogWarning(
                $"[PlaytestLogger] {path} begins with an older header, so rows appended to it carry " +
                "more fields than its header names. Everything already in the file still reads " +
                "correctly; only the new reverse and recovery columns go unnamed. Archive or rename " +
                "that file and a fresh log will be started with the full header.", this);
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
