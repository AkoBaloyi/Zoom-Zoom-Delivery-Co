using UnityEngine;
using ZoomZoom.Orders;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// Getting the car out of trouble: the automatic recovery for a car that is overturned or
    /// driving into something without moving, and the Respawn button (Backspace, or the View,
    /// Share or Create button on a pad).
    ///
    /// WHERE THE CAR GOES BACK TO
    /// Every half second on clear road the car remembers where it is, keeping the last eight spots.
    /// A respawn tries the newest first and works back, so one blocked spot (a traffic car stopped
    /// on it, a prop knocked onto it) does not leave the car stuck. If none of them is clear, the
    /// car goes back to where it was last placed: the level's start, or wherever Teleport put it.
    ///
    /// PRESSING AGAIN GOES FURTHER BACK
    /// If the car has not saved a new spot since its last respawn, the next respawn skips the spot
    /// it used and goes to the one before. A spot that keeps getting the car stuck therefore cannot
    /// trap it: pressed enough times, the button always ends at the start.
    ///
    /// THE BUTTON ALWAYS WORKS
    /// The button used to work only when the car already counted as stranded, which meant
    /// overturned, or held against something with the gas down. A car wedged upright with the gas
    /// released did not count, so the button did nothing exactly when players reached for it. Now
    /// a press respawns the car whatever state it is in, once a second at most, and if every road
    /// spot is blocked it goes back to the start rather than doing nothing. Each respawn costs the
    /// same time penalty as before. The automatic recovery still only acts on a car that is
    /// stranded, so a parked car never moves on its own.
    ///
    /// A car that falls more than fallRespawnDepth below its start and every saved spot, through a
    /// gap or off the edge of the level, is put back automatically.
    /// </summary>
    public partial class VehicleController
    {
        private const int SafePoseHistory = 8;
        private const float RespawnCooldown = 1f;
        private const float StuckHintDelay = 1.5f;   // seconds of trying to drive without getting anywhere
        private const float StuckHintHold = 6f;      // how long the prompt stays once the player stops trying
        private const float StuckHintRadius = 3f;    // moving this far from where it was counts as getting somewhere
        private const float SpawnLift = 0.3f;

        private readonly Vector3[] _safePositions = new Vector3[SafePoseHistory];
        private readonly Quaternion[] _safeRotations = new Quaternion[SafePoseHistory];
        private int _safePoseCount, _safePoseNewest = -1;
        private bool _newestSpotUsed;   // respawned to the newest spot and none saved since
        private bool _respawning;       // Teleport is moving the car for a respawn, so keep the spots

        private bool _hasSpawnPose;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        private float _safeSampleRemaining;
        private Vector3 _stuckOrigin;
        private bool _monitoringStuck;
        private float _respawnCooldown;

        private Vector3 _hintAnchor;
        private float _effortWithoutProgress;
        private float _stuckHintRemaining;

        /// <summary>
        /// True once the player has spent a moment trying to drive without the car getting anywhere,
        /// and for a few seconds after they give up. The HUD shows the Respawn prompt while it is.
        /// </summary>
        public bool LooksStuck => _stuckHintRemaining > 0f;

        /// <summary>
        /// Respawns the car at its last clear spot on the road, or at the start if none is clear.
        /// For an on-screen or pause-menu button: point the button's OnClick at the car and pick
        /// VehicleController.Respawn. Pressed while paused, it happens as soon as the game resumes.
        /// </summary>
        public void Respawn() => RequestTaxiRecovery();

        private bool HasSafePose => _safePoseCount > 0;

        /// <summary>Ring-buffer slot of the saved spot this many saves old (0 is the newest).</summary>
        private int SafeSlot(int age) => (_safePoseNewest - age + SafePoseHistory) % SafePoseHistory;

        /// <summary>
        /// Called from ResetTaxi every time the car is placed somewhere. Anything other than a
        /// respawn (the level starting, or a Teleport) makes that place the new start, and the old
        /// spots are forgotten because they belong to wherever the car was before.
        /// </summary>
        private void ResetRecovery()
        {
            _safeSampleRemaining = 0f;
            _monitoringStuck = false;
            _hintAnchor = transform.position;
            _effortWithoutProgress = 0f;
            _stuckHintRemaining = 0f;
            if (_respawning) return;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            _spawnPosition = transform.position;
            _spawnRotation = Quaternion.LookRotation(forward, Vector3.up);
            _hasSpawnPose = true;
            ClearSafePoses();
            SeedRecoveryPoint();
        }

        private void ClearSafePoses()
        {
            _safePoseCount = 0;
            _safePoseNewest = -1;
            _newestSpotUsed = false;
        }

        private void RememberSafePose(Vector3 position, Quaternion rotation)
        {
            _safePoseNewest = (_safePoseNewest + 1) % SafePoseHistory;
            _safePositions[_safePoseNewest] = position;
            _safeRotations[_safePoseNewest] = rotation;
            _safePoseCount = Mathf.Min(_safePoseCount + 1, SafePoseHistory);
            _newestSpotUsed = false;
        }

        private void SeedRecoveryPoint()
        {
            if (_rb == null) return;
            float nearest = float.PositiveInfinity;
            RaycastHit support = default;
            foreach (RaycastHit hit in Physics.RaycastAll(_rb.position + Vector3.up * 2f, Vector3.down, 4f,
                tuning.groundMask, QueryTriggerInteraction.Ignore))
            {
                if (IsOwnCollider(hit.collider) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                support = hit;
            }
            // Any upright, supported ground will do for the seed, grass included. It is where the
            // level put the car, so it is a safe place to come back to. Medium's spawn is on the
            // lawn 14 m from the nearest road, and while this only accepted tarmac and concrete the
            // car had no recovery point at all until it first reached a road. The 0.5 s sampler
            // below still saves road poses only.
            if (support.collider == null || support.normal.y < 0.85f) return;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 position = support.point + Vector3.up * (Taxi.recoveryClearanceHalfSize.y + 0.15f);
            if (!RecoveryClear(position, rotation, true)) return;
            RememberSafePose(position, rotation);
        }

        private bool RecoveryClear(Vector3 position, Quaternion rotation, bool checkpoint = false)
        {
            BoxCollider body = GetComponent<BoxCollider>();
            Vector3 half = Taxi.recoveryClearanceHalfSize;
            if (body != null)
            {
                half = Vector3.Max(half, Vector3.Scale(body.size, transform.lossyScale) * 0.5f);
                position += rotation * Vector3.Scale(body.center, transform.lossyScale);
            }
            // A pose that merely fits flush against a wall is not an escape checkpoint.
            if (checkpoint) half += new Vector3(1f, 0f, 2f);
            foreach (Collider obstacle in Physics.OverlapBox(position, half,
                rotation, tuning.groundMask, QueryTriggerInteraction.Ignore))
                if (!IsOwnCollider(obstacle)) return false;
            return true;
        }

        /// <summary>Flat ground within 2 m below the pose, so the car lands on something.</summary>
        private bool RecoverySupported(Vector3 position)
        {
            bool supported = false;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit ground in Physics.RaycastAll(position + Vector3.up, Vector3.down, 3f,
                tuning.groundMask, QueryTriggerInteraction.Ignore))
            {
                if (IsOwnCollider(ground.collider) || ground.distance >= nearest) continue;
                nearest = ground.distance;
                supported = ground.normal.y >= 0.85f;
            }
            return supported;
        }

        /// <summary>Below this height the car has fallen out of the level.</summary>
        private float FallFloor()
        {
            float lowest = _spawnPosition.y;
            for (int age = 0; age < _safePoseCount; age++)
                lowest = Mathf.Min(lowest, _safePositions[SafeSlot(age)].y);
            return lowest - Taxi.fallRespawnDepth;
        }

        private void StepRoadRecovery(float dt)
        {
            _safeSampleRemaining -= dt;
            _respawnCooldown = Mathf.Max(0f, _respawnCooldown - dt);

            // Save only supported, upright, clear tarmac/concrete poses. Standing against a wall
            // cannot overwrite the last safe point; an initial parked spawn can seed it once.
            // Ground speed, not Speed: a respawned car drops 15 cm onto its springs, and under the
            // car's own gravity (tuning.gravity, 20, about twice Earth's) it lands at about
            // 2.4 m/s. Counting that would save the landing as a new spot and stop a second press
            // from going further back.
            bool road = Surface.kind == SurfaceKind.Tarmac || Surface.kind == SurfaceKind.Concrete;
            Vector3 velocity = _rb.linearVelocity;
            bool driving = velocity.x * velocity.x + velocity.z * velocity.z > 4f;
            if (road && WheelsOnGround == 4 && Uprightness > 0.95f && GroundNormal.y > 0.85f &&
                (HasSafePose ? driving : true) && _safeSampleRemaining <= 0f)
            {
                Quaternion rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(GroundForward, Vector3.up), Vector3.up);
                Vector3 position = _rb.position + Vector3.up * 0.15f;
                if (RecoveryClear(position, rotation, true))
                {
                    RememberSafePose(position, rotation);
                    _safeSampleRemaining = 0.5f;
                }
            }

            bool overturned = Uprightness < Taxi.recoveryUprightness;
            bool tryingToMove = Mathf.Abs(Drive.throttle) > 0.1f && !Drive.freeRev;
            if (!tryingToMove) _monitoringStuck = false;
            if (tryingToMove && (!_monitoringStuck || Vector3.Distance(_rb.position, _stuckOrigin) > 0.75f))
            {
                _stuckOrigin = _rb.position;
                _monitoringStuck = true;
                if (!overturned) _rightingDelay = 0f;
            }
            // Use lack of progress rather than wheel contact or instantaneous speed: a car
            // hung on a wall/prop can have no wheel rays and still need recovery.
            bool blocked = tryingToMove && _monitoringStuck;
            bool stranded = overturned || blocked;
            _rightingDelay = stranded ? _rightingDelay + dt : 0f;
            StepStuckHint(dt, tryingToMove || overturned);

            bool manual = _taxiRecoveryRequested && _respawnCooldown <= 0f;
            _taxiRecoveryRequested = false;
            // The automatic path still only acts on a stranded car; a parked car never moves itself.
            bool automatic = stranded && _rightingDelay >= Taxi.automaticRecoveryDelay;
            bool fell = _hasSpawnPose && _rb.position.y < FallFloor();
            if (!manual && !automatic && !fell) return;

            if (TryRespawn()) return;

            // Nothing clear anywhere. A player who asked, or a car falling out of the level, still
            // goes back to the start, lifted clear, rather than being left where it is.
            if ((manual || fell) && _hasSpawnPose) RespawnAtStart(Taxi.recoveryLift);
        }

        /// <summary>
        /// The Respawn prompt. It shows once the player has spent StuckHintDelay seconds trying to
        /// drive, or lying overturned, without the car getting StuckHintRadius metres from where it
        /// was. This is looser than the automatic recovery on purpose: a car rocking between two
        /// walls, or a player switching between gas and reverse, never lets that one fire, and
        /// those players are the ones who need telling.
        /// </summary>
        private void StepStuckHint(float dt, bool trying)
        {
            if ((_rb.position - _hintAnchor).sqrMagnitude > StuckHintRadius * StuckHintRadius)
            {
                _hintAnchor = _rb.position;
                _effortWithoutProgress = 0f;
                _stuckHintRemaining = 0f;
                return;
            }
            _effortWithoutProgress = Mathf.Clamp(_effortWithoutProgress + (trying ? dt : -dt), 0f, StuckHintDelay);
            _stuckHintRemaining = _effortWithoutProgress >= StuckHintDelay ? StuckHintHold :
                Mathf.Max(0f, _stuckHintRemaining - dt);
        }

        /// <summary>Newest usable road spot first, then older ones, then the start. False if all are blocked.</summary>
        private bool TryRespawn()
        {
            for (int age = _newestSpotUsed ? 1 : 0; age < _safePoseCount; age++)
            {
                int slot = SafeSlot(age);
                Vector3 position = _safePositions[slot];
                Quaternion rotation = _safeRotations[slot];
                if (!RecoverySupported(position) || !RecoveryClear(position, rotation)) continue;
                // The newer spots were blocked or have already failed the car once; forget them.
                _safePoseNewest = slot;
                _safePoseCount -= age;
                RespawnAt(position, rotation);
                _newestSpotUsed = true;
                return true;
            }

            if (!_hasSpawnPose || !RecoveryClear(_spawnPosition + Vector3.up * SpawnLift, _spawnRotation))
                return false;
            RespawnAtStart(0f);
            return true;
        }

        private void RespawnAtStart(float extraLift)
        {
            ClearSafePoses();
            RespawnAt(_spawnPosition + Vector3.up * (SpawnLift + extraLift), _spawnRotation);
        }

        private void RespawnAt(Vector3 position, Quaternion rotation)
        {
            SelfRightCount++;
            _respawning = true;
            try { Teleport(position, rotation); }
            finally { _respawning = false; }
            _respawnCooldown = RespawnCooldown;
            foreach (ShiftTimer timer in FindObjectsByType<ShiftTimer>(FindObjectsInactive.Exclude))
                if (timer.gameObject.scene == gameObject.scene) timer.ApplyTimePenalty(Taxi.recoveryTimeCost);
            Recovered?.Invoke(Taxi.recoveryTimeCost);
        }
    }
}
