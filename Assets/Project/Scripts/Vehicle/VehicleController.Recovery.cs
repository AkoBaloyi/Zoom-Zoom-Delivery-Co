using UnityEngine;
using ZoomZoom.Orders;

namespace ZoomZoom.Vehicle
{
    public partial class VehicleController
    {
        private bool _hasRecoveryPoint;
        private Vector3 _recoveryPoint;
        private Quaternion _recoveryRotation;
        private float _safeSampleRemaining;
        private Vector3 _stuckOrigin;
        private bool _monitoringStuck;

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
            if (support.collider == null || support.normal.y < 0.85f) return;
            SurfaceType surface = support.collider.GetComponentInParent<SurfaceType>();
            if (surface != null && surface.kind != SurfaceKind.Tarmac && surface.kind != SurfaceKind.Concrete) return;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 position = support.point + Vector3.up * (Taxi.recoveryClearanceHalfSize.y + 0.15f);
            if (!RecoveryClear(position, rotation, true)) return;
            _recoveryPoint = position;
            _recoveryRotation = rotation;
            _hasRecoveryPoint = true;
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

        private void StepRoadRecovery(float dt)
        {
            _safeSampleRemaining -= dt;
            // Save only supported, upright, clear tarmac/concrete poses. Standing against a wall
            // cannot overwrite the last safe point; an initial parked spawn can seed it once.
            bool road = Surface.kind == SurfaceKind.Tarmac || Surface.kind == SurfaceKind.Concrete;
            if (road && WheelsOnGround == 4 && Uprightness > 0.95f && GroundNormal.y > 0.85f &&
                (_hasRecoveryPoint ? Speed > 2f : true) && _safeSampleRemaining <= 0f)
            {
                Quaternion rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(GroundForward, Vector3.up), Vector3.up);
                Vector3 position = _rb.position + Vector3.up * 0.15f;
                if (RecoveryClear(position, rotation, true))
                {
                    _recoveryPoint = position;
                    _recoveryRotation = rotation;
                    _hasRecoveryPoint = true;
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
            // Manual reset still requires being stranded; ordinary parked cars never reset.
            bool recover = stranded && ((_taxiRecoveryRequested && Speed < Taxi.recoveryMaximumSpeed) ||
                _rightingDelay >= Taxi.automaticRecoveryDelay);
            _taxiRecoveryRequested = false;
            if (!recover || !_hasRecoveryPoint) return;
            bool supported = false;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit ground in Physics.RaycastAll(_recoveryPoint + Vector3.up, Vector3.down, 3f,
                tuning.groundMask, QueryTriggerInteraction.Ignore))
            {
                if (IsOwnCollider(ground.collider) || ground.distance >= nearest) continue;
                nearest = ground.distance;
                supported = ground.normal.y >= 0.85f;
            }
            if (!supported) return;
            if (!RecoveryClear(_recoveryPoint, _recoveryRotation)) return;
            Vector3 destination = _recoveryPoint;
            Quaternion heading = _recoveryRotation;
            SelfRightCount++;
            Teleport(destination, heading);
            foreach (ShiftTimer timer in FindObjectsByType<ShiftTimer>(FindObjectsSortMode.None))
                if (timer.gameObject.scene == gameObject.scene) timer.ApplyTimePenalty(Taxi.recoveryTimeCost);
            Recovered?.Invoke(Taxi.recoveryTimeCost);
        }
    }
}
