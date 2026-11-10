using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>Automatic arcade chase camera. Stable horizon, delayed yaw and drift framing.</summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private VehicleController target;
        [SerializeField] private VehicleTuning tuningOverride;
        [SerializeField] private VehicleInput input;
        [SerializeField] private LayerMask collisionLayers = Physics.DefaultRaycastLayers;
        private Camera _camera;
        private SphereCollider _collisionProbe;
        private float _heading, _headingVelocity, _wallDistance;
        private float _dashFov;
        private bool MotionCues => Settings.speedCameraCues && (input == null || input.MotionEffectsEnabled);
        private float _fov, _fovVelocity, _surge, _surgeVelocity, _previousForwardSpeed;
        private Vector3 _position, _positionVelocity;
        private readonly RaycastHit[] _hits = new RaycastHit[64];
        private readonly Collider[] _overlaps = new Collider[64];
        private bool _ready;
        private bool _lookingBehind;
        public Camera Camera => _camera;
        public VehicleController Target => target;
        private CrazyTaxiSettings Settings => tuningOverride != null ? tuningOverride.taxi : target.Taxi;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _collisionProbe = GetComponent<SphereCollider>();
            if (_collisionProbe == null) _collisionProbe = gameObject.AddComponent<SphereCollider>();
            _collisionProbe.isTrigger = true;
            _collisionProbe.enabled = false;
            if (target == null) target = FindAnyObjectByType<VehicleController>();
            if (target != null && input == null) input = target.GetComponent<VehicleInput>();
        }
        private void OnEnable()
        {
            if (target == null) target = FindAnyObjectByType<VehicleController>();
            if (target != null) target.Repositioned += SnapToTarget;
            SnapToTarget();
        }
        private void OnDisable()
        {
            if (target != null) target.Repositioned -= SnapToTarget;
        }
        public void SnapToTarget()
        {
            _ready = false;
            if (target == null || target.Tuning == null) return;
            if (_camera == null) _camera = GetComponent<Camera>();
            _heading = NoseYaw();
            _headingVelocity = 0f;
            _positionVelocity = Vector3.zero;
            _wallDistance = float.PositiveInfinity;
            _fov = Settings.cameraFov;
            _dashFov = 0f;
            _fovVelocity = _surge = _surgeVelocity = 0f;
            _previousForwardSpeed = target.ForwardSpeed;
            _position = DesiredPosition(_heading);
            _ready = true;
            RenderCamera(0f);
        }
        private void LateUpdate()
        {
            if (Time.deltaTime > 0f) RenderCamera(Time.deltaTime);
        }

        // Explicit dt also lets the editor validation exercise the same camera path deterministically.
        public void RenderCamera(float dt)
        {
            if (target == null || target.Tuning == null) return;
            if (!_ready) { SnapToTarget(); return; }
            if (input == null) input = target.GetComponent<VehicleInput>();
            CrazyTaxiSettings s = Settings;
            float speed01 = Mathf.Clamp01(target.Speed / Mathf.Max(0.1f, s.cruiseSpeed));
            float acceleration = dt > 0f ? (target.ForwardSpeed - _previousForwardSpeed) / dt : 0f;
            _previousForwardSpeed = target.ForwardSpeed;
            float surgeTarget = MotionCues ? Mathf.Clamp01(acceleration / Mathf.Max(1f, s.acceleration)) * s.cameraAccelerationPullback : 0f;
            _surge = dt > 0f ? Mathf.SmoothDamp(_surge, surgeTarget, ref _surgeVelocity,
                Mathf.Max(0.01f, s.cameraAccelerationResponseTime), Mathf.Infinity, dt) : 0f;
            float wantedYaw = NoseYaw();
            Vector3 velocity = target.Body != null ? target.Body.linearVelocity : Vector3.zero;
            velocity.y = 0f;
            // Never blend antiparallel vectors in reverse. The original keeps its forward view.
            // During a slide the travel direction partly leads the camera, showing the exit road.
            if (target.IsTaxiDrifting && velocity.sqrMagnitude > s.cameraVelocityMinimumSpeed * s.cameraVelocityMinimumSpeed && target.ForwardSpeed > 0f)
            {
                float travelYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                wantedYaw += Mathf.Clamp(Mathf.DeltaAngle(wantedYaw, travelYaw), -s.cameraMaximumDriftLead, s.cameraMaximumDriftLead)
                    * s.cameraDriftVelocityBlend;
            }
            if (dt > 0f)
                _heading = Mathf.SmoothDampAngle(_heading, wantedYaw, ref _headingVelocity,
                    s.cameraHeadingTime, Mathf.Infinity, dt);
            bool behind = input != null && input.LookBehind;
            if (s.cameraFollowReverse && target.ForwardSpeed < -s.cameraReverseSpeedThreshold) behind = !behind;
            float viewYaw = _heading + (behind ? 180f : 0f);
            Vector3 desired = DesiredPosition(viewYaw);
            if (behind != _lookingBehind)
            {
                _position = desired;
                _positionVelocity = Vector3.zero;
                _wallDistance = float.PositiveInfinity;
                _lookingBehind = behind;
            }
            _position = dt > 0f ? Vector3.SmoothDamp(_position, desired, ref _positionVelocity,
                s.cameraFollowTime, Mathf.Infinity, dt) : desired;

            Vector3 pivot = target.transform.position + Vector3.up * s.cameraAimHeight;
            Vector3 candidate = AvoidWalls(pivot, _position, dt);
            // The spring must continue from the collision-safe point, otherwise its hidden
            // position can remain inside a building and push back through it on the next frame.
            if ((candidate - _position).sqrMagnitude > 0.0001f)
            {
                _position = candidate;
                _positionVelocity = Vector3.zero;
            }
            Vector3 heading = Quaternion.Euler(0f, viewYaw, 0f) * Vector3.forward;
            Vector3 aim = pivot + heading * s.cameraLookAhead;
            Vector3 toAim = aim - candidate;
            Quaternion rotation = Quaternion.LookRotation(toAim.sqrMagnitude > 0.001f ? toAim : heading, Vector3.up);
            transform.SetPositionAndRotation(candidate, rotation);
            float wantedFov = s.useCameraFovCurve
                ? Mathf.Clamp(s.cameraFovBySpeed.Evaluate(target.Speed / Mathf.Max(0.1f, s.cruiseSpeed)), 20f, 120f)
                : s.cameraFov + (MotionCues ? s.cameraSpeedFovAddition * speed01 * speed01 * (3f - 2f * speed01) : 0f);
            bool dashing = target.ActiveTrick == TaxiTrick.Dash || target.ActiveTrick == TaxiTrick.LimiterCut;
            float dashTarget = MotionCues && dashing ? s.dashFovAddition : 0f;
            _dashFov = dt > 0f ? Mathf.MoveTowards(_dashFov, dashTarget, dt * (dashing ? 40f : 15f)) : 0f;
            if (!MotionCues) { _dashFov = 0f; wantedFov = s.cameraFov; }
            wantedFov += _dashFov;
            _fov = dt > 0f ? Mathf.SmoothDamp(_fov, wantedFov, ref _fovVelocity,
                Mathf.Max(0.01f, s.cameraFovResponseTime), Mathf.Infinity, dt) : wantedFov;
            _camera.fieldOfView = Mathf.Clamp(_fov, 20f, 120f);
            _camera.nearClipPlane = s.cameraNearClip;
        }
        private float NoseYaw()
        {
            Vector3 forward = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up);
            return forward.sqrMagnitude > 0.001f ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg : _heading;
        }
        private Vector3 DesiredPosition(float yaw)
        {
            CrazyTaxiSettings s = Settings;
            Vector3 heading = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float speed01 = Mathf.Clamp01(target.Speed / Mathf.Max(0.1f, s.cruiseSpeed));
            float distance = s.cameraDistance + (MotionCues ? s.cameraSpeedDistanceAddition * speed01 + _surge : 0f);
            return target.transform.position - heading * distance + Vector3.up * s.cameraHeight;
        }
        private bool IsOwn(Collider collider) => collider == null ||
            collider.transform.IsChildOf(target.transform) || collider.attachedRigidbody == target.Body;

        private Vector3 AvoidWalls(Vector3 pivot, Vector3 wanted, float dt)
        {
            CrazyTaxiSettings s = Settings;
            // Near-plane corners also need clearance, including wide aspect ratios and speed FOV.
            float tangent = Mathf.Tan(Mathf.Max(_camera.fieldOfView, s.cameraFov + s.cameraSpeedFovAddition + s.dashFovAddition) * 0.5f * Mathf.Deg2Rad);
            float radius = Mathf.Max(s.cameraCollisionRadius, s.cameraNearClip *
                Mathf.Sqrt(1f + tangent * tangent * (1f + _camera.aspect * _camera.aspect)));
            // Sphere casts miss a wall that already contains their starting sphere.
            pivot = ResolveOverlap(pivot, radius);
            Vector3 offset = wanted - pivot;
            float distance = offset.magnitude;
            if (distance < 0.001f) return pivot;
            Vector3 direction = offset / distance;
            float clear = distance;
            int count = Physics.SphereCastNonAlloc(pivot, radius, direction, _hits,
                distance, collisionLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!IsOwn(_hits[i].collider)) clear = Mathf.Min(clear, Mathf.Max(0f, _hits[i].distance - s.cameraWallPadding));
            _wallDistance = dt <= 0f || clear < _wallDistance ? clear :
                Mathf.Lerp(_wallDistance, clear, 1f - Mathf.Exp(-dt / s.cameraWallReturnTime));
            Vector3 result = ResolveOverlap(pivot + direction * Mathf.Min(clear, _wallDistance), radius);
            // Depenetration at an inside corner must not push the camera through the far wall.
            offset = result - pivot;
            distance = offset.magnitude;
            if (distance > 0.001f)
            {
                direction = offset / distance;
                count = Physics.SphereCastNonAlloc(pivot, radius, direction, _hits,
                    distance, collisionLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (!IsOwn(_hits[i].collider)) distance = Mathf.Min(distance,
                        Mathf.Max(0f, _hits[i].distance - s.cameraWallPadding));
                result = pivot + direction * distance;
            }
            return result;
        }

        private Vector3 ResolveOverlap(Vector3 result, float radius)
        {
            CrazyTaxiSettings s = Settings;
            // Sphere casts do not report a collider that already contains the sphere's origin.
            // Resolve overlaps too, including walls a camera entered while turning a corner.
            for (int pass = 0; pass < s.cameraOverlapPasses; pass++)
            {
                int count = Physics.OverlapSphereNonAlloc(result, radius, _overlaps,
                    collisionLayers, QueryTriggerInteraction.Ignore);
                bool adjusted = false;
                for (int i = 0; i < count; i++)
                {
                    Collider obstacle = _overlaps[i];
                    if (IsOwn(obstacle)) continue;
                    _collisionProbe.radius = radius;
                    // A disabled probe may have no native shape after a domain reload. Enable
                    // only for this immediate query, then remove it before any physics step.
                    _collisionProbe.enabled = true;
                    bool penetrating = Physics.ComputePenetration(_collisionProbe, result, Quaternion.identity,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation,
                        out Vector3 normal, out float depth);
                    _collisionProbe.enabled = false;
                    if (penetrating)
                    {
                        result += normal * (depth + 0.01f);
                        adjusted = true;
                    }
                }
                if (!adjusted) break;
            }
            return result;
        }
    }
}
