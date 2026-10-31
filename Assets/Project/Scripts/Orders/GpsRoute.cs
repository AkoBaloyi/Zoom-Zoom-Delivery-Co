using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZoomZoom.Orders.UI;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// A GPS line on the ground showing the road route from the car to wherever the selected
    /// order (see OrderFocus) needs it next, drawn in that order's colour. Shown when the
    /// Guidance setting is GPS or Both (main menu Settings, or G / D-pad up in game). Tab, or
    /// D-pad right, changes which order it follows.
    ///
    /// THE ROUTE FOLLOWS THE ROADS, NOT THE QUICKEST LINE
    /// The route is found with Unity's NavMesh, baked on the roads only. So the line always
    /// sticks to the road, even when cutting across a field would be quicker. That is the
    /// decision this gives the player: follow the safe road route, or read the map and risk the
    /// grass for a shortcut.
    ///
    /// SETUP (once per scene)
    ///   1. Window > Package Manager > Unity Registry, install "AI Navigation".
    ///   2. Select the parent object of your road meshes and Add Component > NavMesh Surface.
    ///      Set Collect Objects to "Current Object Hierarchy", so only the roads are included.
    ///   3. Click Bake. A blue overlay should cover the roads in the Scene view.
    /// With no NavMesh baked, the line falls back to a straight line and says so once in the
    /// Console, so the game still plays.
    ///
    /// Put this on any object in the scene, for example next to the OrderManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class GpsRoute : MonoBehaviour
    {
        [Header("Wiring (found automatically if left empty)")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private OrderFocus focus;
        [Tooltip("The car. Found automatically: the object with ZoneDetection.")]
        [SerializeField] private Transform car;

        [Header("Route")]
        [Tooltip("Seconds between route updates. The route also updates when the selected order " +
                 "or its next stop changes.")]
        [SerializeField] private float recalculateEvery = 0.4f;
        [Tooltip("How far, in metres, the car or a target may be from the road and still be " +
                 "snapped onto it to start or end the route.")]
        [SerializeField] private float snapToRoadDistance = 40f;
        [Tooltip("Draw a straight line when no NavMesh has been baked, so there is always a line.")]
        [SerializeField] private bool straightLineWithoutNavMesh = true;

        [Header("Look")]
        [SerializeField] private float width = 1.6f;
        [Tooltip("Height above the road, so the line does not flicker into it.")]
        [SerializeField] private float heightAboveGround = 0.35f;
        [Range(0f, 1f)]
        [SerializeField] private float opacity = 0.85f;
        [Tooltip("Seconds left below which the line flashes red.")]
        [SerializeField] private float urgentBelowSeconds = 8f;

        private LineRenderer _line;
        private NavMeshPath _path;
        private readonly List<Vector3> _points = new List<Vector3>();
        private readonly List<Vector3> _scratch = new List<Vector3>();
        private float _timer;
        private Order _routeOrder;
        private Vector3 _routeTarget;
        private bool _warnedNoNavMesh;
        private bool _hasRoadRoute;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (car == null)
            {
                ZoneDetection z = FindAnyObjectByType<ZoneDetection>();
                if (z != null) car = z.transform;
            }

            if (orderManager == null || car == null)
            {
                Debug.LogError("[GpsRoute] Needs an OrderManager and a car (ZoneDetection) in the scene. " +
                               "Switching off.", this);
                enabled = false;
                return;
            }

            _path = new NavMeshPath();
            BuildLine();
        }

        private void Start()
        {
            if (focus == null) focus = OrderFocus.Ensure(orderManager);
        }

        private void Update()
        {
            Order order = focus != null ? focus.Selected : null;
            bool show = focus != null && focus.GpsOn && order != null && !order.IsTerminal;

            if (!show)
            {
                if (_line.enabled) _line.enabled = false;
                _routeOrder = null;
                return;
            }

            Vector3 target = order.TargetFrom(car.position);

            _timer -= Time.deltaTime;
            bool changed = order != _routeOrder || (target - _routeTarget).sqrMagnitude > 1f;
            if (changed || _timer <= 0f)
            {
                _timer = recalculateEvery;
                bool sameJob = order == _routeOrder && (target - _routeTarget).sqrMagnitude <= 1f;
                _routeOrder = order;
                _routeTarget = target;
                Rebuild(car.position, target, sameJob);
            }

            Color colour = OrderPalette.OrderColourWithUrgency(order, urgentBelowSeconds, Time.time);
            colour.a = opacity;
            _line.startColor = colour;
            _line.endColor = colour;
            _line.enabled = _points.Count >= 2;
        }

        private void Rebuild(Vector3 from, Vector3 to, bool sameJob)
        {
            _scratch.Clear();

            if (TryRoadRoute(from, to, _scratch))
            {
                // Start at the car and end at the target itself, even if they are off the road.
                _scratch.Insert(0, from);
                _scratch.Add(to);
                _hasRoadRoute = true;
            }
            else if (sameJob && _hasRoadRoute)
            {
                // One failed update (the car mid jump, say) keeps the last good route rather than
                // flicking to a straight line and back.
                return;
            }
            else if (straightLineWithoutNavMesh)
            {
                _scratch.Add(from);
                _scratch.Add(to);
                _hasRoadRoute = false;
            }

            _points.Clear();
            _points.AddRange(_scratch);

            for (int i = 0; i < _points.Count; i++) _points[i] += Vector3.up * heightAboveGround;

            _line.positionCount = _points.Count;
            _line.SetPositions(_points.ToArray());
        }

        private bool TryRoadRoute(Vector3 from, Vector3 to, List<Vector3> into)
        {
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, snapToRoadDistance, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out NavMeshHit end, snapToRoadDistance, NavMesh.AllAreas))
            {
                WarnNoNavMesh();
                return false;
            }

            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path) ||
                _path.status == NavMeshPathStatus.PathInvalid || _path.corners.Length < 2)
            {
                WarnNoNavMesh();
                return false;
            }

            into.AddRange(_path.corners);
            return true;
        }

        private void WarnNoNavMesh()
        {
            if (_warnedNoNavMesh) return;
            _warnedNoNavMesh = true;
            Debug.LogWarning(
                "[GpsRoute] No road NavMesh near the car or the target, so the GPS shows a straight " +
                "line. Install AI Navigation, add a NavMesh Surface to the roads' parent object " +
                "(Collect Objects: Current Object Hierarchy) and click Bake.", this);
        }

        private void BuildLine()
        {
            var go = new GameObject("GPS Route");
            go.transform.SetParent(transform, false);

            // Lying flat: TransformZ alignment faces the line along this object's Z axis, and
            // rotating 90 degrees on X points that axis straight down.
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.alignment = LineAlignment.TransformZ;
            _line.widthMultiplier = width;
            _line.numCornerVertices = 4;
            _line.numCapVertices = 4;
            _line.sharedMaterial = OrderWorldMaterial.Get();
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.positionCount = 0;
            _line.enabled = false;
        }
    }
}
