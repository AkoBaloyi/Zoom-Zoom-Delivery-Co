using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// Bright, in-world beacons at every place an active order needs the car right now: each
    /// ingredient source still needed, the bay once every ingredient is in the car or the food
    /// is ready, and the customer once the food is in the car. Nothing is shown while food is
    /// cooking, because the bay shows its own countdown.
    ///
    /// Every beacon is drawn in its order's own colour, the same colour as the order's
    /// notification on the phone and its arrow on screen, and labelled with the order number,
    /// what to do there and the seconds left, so the player can tell which beacon belongs to
    /// which order and when it expires. Beacons flash red when their order is about to be lost.
    /// The selected order's beacons are solid; the others are faded.
    ///
    /// When two orders need the same place they share one beacon, with one label line per order
    /// in that order's colour.
    ///
    /// Beacons grow the further they are from the car, so a far target is still easy to spot,
    /// and shrink back up close. The ground disc never scales, because it shows the real zone.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderBeacon : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private OrderManager orderManager;

        [Tooltip("Found automatically if left empty. The ground disc's radius is read from this " +
                 "every frame, so the visual footprint always matches the real zone exactly.")]
        [SerializeField] private ZoneDetection zoneDetection;

        [Tooltip("Used only if no ZoneDetection is found in the scene.")]
        [SerializeField] private float fallbackGroundDiscRadius = 6f;

        [Header("Pool")]
        [Tooltip("Beacons made at start. More are made automatically if needed.")]
        [SerializeField] private int poolSize = 8;

        [Header("Beam")]
        [SerializeField] private float beamHeight = 14f;
        [SerializeField] private float beamRadius = 0.55f;

        [Header("Ground disc")]
        [Tooltip("Height of the flat disc off the ground, purely to avoid z-fighting with the floor.")]
        [SerializeField] private float groundDiscYOffset = 0.05f;

        [Header("Number label")]
        [SerializeField] private int numberFontSize = 64;
        [SerializeField] private float numberCharacterSize = 0.3f;
        [SerializeField] private float numberHeightAboveBeam = 1.5f;

        [Header("Priority")]
        [Tooltip("Found automatically. The selected order's beacons are solid, the rest faded.")]
        [SerializeField] private OrderFocus focus;
        [Tooltip("Opacity of beacons for orders that are not selected.")]
        [SerializeField] private float unselectedAlpha = 0.5f;
        [Tooltip("Seconds of order time left below which a beacon flashes red.")]
        [SerializeField] private float urgentBelowSeconds = 8f;

        [Header("Size by distance (further is bigger)")]
        [Tooltip("Distance is measured from this. Found automatically: the car (the object with " +
                 "ZoneDetection), or the camera if there is no car.")]
        [SerializeField] private Transform distanceFrom;
        [Tooltip("Metres. At or closer than this, a beacon is at Near Scale.")]
        [SerializeField] private float nearDistance = 30f;
        [Tooltip("Metres. At or further than this, a beacon is at Far Scale.")]
        [SerializeField] private float farDistance = 300f;
        [SerializeField] private float nearScale = 1f;
        [SerializeField] private float farScale = 3f;

        [Header("Pulse")]
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseScaleAmount = 0.18f;

        private struct BeaconSlot
        {
            public GameObject Root;
            public Transform Beam;
            public MeshRenderer BeamRenderer;
            public MaterialPropertyBlock Block;
            public Transform GroundDisc;
            public MeshRenderer GroundDiscRenderer;
            public MaterialPropertyBlock DiscBlock;
            public TextMesh Number;
        }

        /// <summary>One place to mark this frame, possibly shared by several orders.</summary>
        private class Target
        {
            public Vector3 Position;
            public string Label;
            public Color Colour;
            public float Radius;
            public bool Selected;
        }

        private readonly List<BeaconSlot> _slots = new List<BeaconSlot>();
        private readonly List<Target> _targets = new List<Target>();
        private readonly List<Target> _targetPool = new List<Target>();
        private Transform _poolRoot;
        private Camera _camera;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (zoneDetection == null) zoneDetection = FindAnyObjectByType<ZoneDetection>();
            _camera = Camera.main;
            if (distanceFrom == null)
            {
                if (zoneDetection != null) distanceFrom = zoneDetection.transform;
                else if (_camera != null) distanceFrom = _camera.transform;
            }

            if (orderManager == null)
            {
                Debug.LogError("[OrderBeacon] No OrderManager found in the scene. Switching off.", this);
                enabled = false;
                return;
            }

            if (zoneDetection == null)
            {
                Debug.LogWarning(
                    $"[OrderBeacon] No ZoneDetection found, using the fallback radius of " +
                    $"{fallbackGroundDiscRadius}m for every ground disc.", this);
            }

            var root = new GameObject("Order Beacons");
            root.transform.SetParent(transform, false);
            _poolRoot = root.transform;

            for (int i = 0; i < Mathf.Max(1, poolSize); i++) _slots.Add(BuildSlot(i));
        }

        private void Start()
        {
            if (focus == null) focus = OrderFocus.Ensure(orderManager);
        }

        private void Update()
        {
            if (_camera == null) _camera = Camera.main;

            CollectTargets();

            for (int i = 0; i < _targets.Count; i++)
            {
                if (i >= _slots.Count) _slots.Add(BuildSlot(i));

                Target t = _targets[i];
                float sizeScale = nearScale;
                if (distanceFrom != null)
                {
                    float distance = Vector3.Distance(distanceFrom.position, t.Position);
                    float f = Mathf.InverseLerp(nearDistance, Mathf.Max(nearDistance + 0.01f, farDistance), distance);
                    sizeScale = Mathf.Lerp(nearScale, farScale, f);
                }

                Color colour = t.Colour;
                if (!t.Selected) colour.a = unselectedAlpha;
                PlaceBeacon(_slots[i], t.Position, colour, t.Label, t.Radius, sizeScale * (t.Selected ? 1.15f : 1f));
            }

            for (int i = _targets.Count; i < _slots.Count; i++)
            {
                if (_slots[i].Root.activeSelf) _slots[i].Root.SetActive(false);
            }
        }

        private void CollectTargets()
        {
            _targets.Clear();

            float pickupR = zoneDetection != null ? zoneDetection.PickupRadius : fallbackGroundDiscRadius;
            float bayR = zoneDetection != null ? zoneDetection.BayRadius : fallbackGroundDiscRadius;
            float dropR = zoneDetection != null ? zoneDetection.DropOffRadius : fallbackGroundDiscRadius;

            IReadOnlyList<Order> active = orderManager.ActiveOrders;
            Order selected = focus != null ? focus.Selected : null;

            for (int i = 0; i < active.Count; i++)
            {
                Order order = active[i];
                if (!order.HasWorldTarget) continue;

                Color colour = OrderPalette.OrderColourWithUrgency(order, urgentBelowSeconds, Time.time);
                bool isSelected = order == selected;
                string seconds = $"{Mathf.CeilToInt(order.SecondsUntilLost)}s";

                switch (order.CurrentStep)
                {
                    case OrderStep.Gather:
                        foreach (IngredientStop stop in order.Stops)
                        {
                            if (stop.Collected) continue;
                            AddTarget(stop.Point, $"#{order.Id} {stop.Name.ToUpper()} {seconds}", colour, pickupR, isSelected);
                        }
                        break;

                    case OrderStep.Cook:
                        if (!order.HasBay) break;
                        bool ready = order.State == OrderState.Ready;
                        AddTarget(order.BayPoint, $"#{order.Id} {(ready ? "READY" : "COOK")} {seconds}", colour, bayR, isSelected);
                        break;

                    default:
                        AddTarget(order.DropOffPoint, $"#{order.Id} DROP {seconds}", colour, dropR, isSelected);
                        break;
                }
            }
        }

        /// <summary>Adds a place to mark, merging with one already at the same spot. The first
        /// (oldest) order keeps its colour, since it is the most urgent.</summary>
        private void AddTarget(Vector3 position, string label, Color colour, float radius, bool selected)
        {
            // Each line of a shared label is tinted in its own order's colour.
            string line = $"<color=#{OrderPalette.Hex(colour)}>{label}</color>";

            foreach (Target existing in _targets)
            {
                if ((existing.Position - position).sqrMagnitude < 1f)
                {
                    existing.Label += "\n" + line;
                    existing.Radius = Mathf.Max(existing.Radius, radius);
                    if (selected)
                    {
                        // The selected order's colour wins the beam.
                        existing.Selected = true;
                        existing.Colour = colour;
                    }
                    return;
                }
            }

            if (_targetPool.Count <= _targets.Count) _targetPool.Add(new Target());
            Target t = _targetPool[_targets.Count];
            t.Position = position;
            t.Label = line;
            t.Colour = colour;
            t.Radius = radius;
            t.Selected = selected;
            _targets.Add(t);
        }

        private void PlaceBeacon(BeaconSlot slot, Vector3 groundPosition, Color colour, string label,
            float discRadius, float sizeScale)
        {
            if (!slot.Root.activeSelf) slot.Root.SetActive(true);

            slot.Root.transform.position = groundPosition;

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScaleAmount;
            float height = beamHeight * sizeScale;
            float radius = beamRadius * sizeScale;
            float halfHeight = height * 0.5f * pulse;

            slot.Beam.localPosition = new Vector3(0f, halfHeight, 0f);
            slot.Beam.localScale = new Vector3(radius * pulse, halfHeight, radius * pulse);

            OrderWorldMaterial.SetColour(slot.Block, colour);
            slot.BeamRenderer.SetPropertyBlock(slot.Block);

            // The disc IS the real zone radius, so its size comes straight from ZoneDetection.
            float discPulse = 1f + Mathf.Sin(Time.time * pulseSpeed + 1.5f) * (pulseScaleAmount * 0.5f);
            float discDiameter = discRadius * 2f * discPulse;
            slot.GroundDisc.localPosition = new Vector3(0f, groundDiscYOffset, 0f);
            slot.GroundDisc.localScale = new Vector3(discDiameter, 0.02f, discDiameter);

            Color discColour = colour;
            discColour.a = 0.6f;
            OrderWorldMaterial.SetColour(slot.DiscBlock, discColour);
            slot.GroundDiscRenderer.SetPropertyBlock(slot.DiscBlock);

            slot.Number.text = label;
            slot.Number.color = new Color(1f, 1f, 1f, Mathf.Max(0.75f, colour.a));
            slot.Number.characterSize = numberCharacterSize * sizeScale;

            Vector3 numberPos = groundPosition + Vector3.up * (height + numberHeightAboveBeam * sizeScale);
            slot.Number.transform.position = numberPos;

            if (_camera != null)
            {
                Vector3 toCamera = _camera.transform.position - numberPos;
                if (toCamera.sqrMagnitude > 0.001f)
                    slot.Number.transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
            }
        }

        private BeaconSlot BuildSlot(int index)
        {
            Material material = OrderWorldMaterial.Get();

            var slotRoot = new GameObject($"Beacon {index}");
            slotRoot.transform.SetParent(_poolRoot, false);
            slotRoot.SetActive(false);

            GameObject beamGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beamGO.name = "Beam";
            Destroy(beamGO.GetComponent<Collider>());
            beamGO.transform.SetParent(slotRoot.transform, false);

            MeshRenderer beamRenderer = beamGO.GetComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = material;
            beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;

            GameObject discGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            discGO.name = "Ground Disc";
            Destroy(discGO.GetComponent<Collider>());
            discGO.transform.SetParent(slotRoot.transform, false);

            MeshRenderer discRenderer = discGO.GetComponent<MeshRenderer>();
            discRenderer.sharedMaterial = material;
            discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            discRenderer.receiveShadows = false;

            var numberGO = new GameObject("Number");
            numberGO.transform.SetParent(slotRoot.transform, false);
            TextMesh number = numberGO.AddComponent<TextMesh>();
            number.fontSize = numberFontSize;
            number.characterSize = numberCharacterSize;
            number.anchor = TextAnchor.LowerCenter;
            number.alignment = TextAlignment.Center;
            number.fontStyle = FontStyle.Bold;
            number.richText = true;

            MeshRenderer numberRenderer = numberGO.GetComponent<MeshRenderer>();
            numberRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            numberRenderer.receiveShadows = false;

            return new BeaconSlot
            {
                Root = slotRoot,
                Beam = beamGO.transform,
                BeamRenderer = beamRenderer,
                Block = new MaterialPropertyBlock(),
                GroundDisc = discGO.transform,
                GroundDiscRenderer = discRenderer,
                DiscBlock = new MaterialPropertyBlock(),
                Number = number
            };
        }
    }
}