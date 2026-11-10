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
    /// A beacon at a shop also shows the ingredient's type icon on a disc in the type colour,
    /// the same icon and colour as the chip on the order card. Every shop that sells a needed
    /// ingredient lights up, not just one, so the player chooses which to drive to.
    ///
    /// Every beacon is drawn in its order's own colour, the same colour as the order's
    /// notification on the phone and its arrow on screen, and labelled with the order number,
    /// what to do there and the seconds left, so the player can tell which beacon belongs to
    /// which order and when it expires. Beacons swell and pulse faster (but never flash) when
    /// their order is about to be lost.
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
            public Transform IconRoot;
            public SpriteRenderer IconDisc;
            public SpriteRenderer Icon;
        }

        /// <summary>One place to mark this frame, possibly shared by several orders.</summary>
        private class Target
        {
            public Vector3 Position;
            public string Label;
            public Color Colour;
            public float Radius;
            public bool Selected;
            public Sprite Icon;
            public Color IconColour;
            public float Pulse;
        }

        private readonly List<BeaconSlot> _slots = new List<BeaconSlot>();
        private readonly List<Target> _targets = new List<Target>();
        private readonly List<Target> _targetPool = new List<Target>();
        private readonly List<Vector3> _places = new List<Vector3>();
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
                PlaceBeacon(_slots[i], t, colour, sizeScale * (t.Selected ? 1.15f : 1f));
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

                Color colour = OrderPalette.OrderColour(order);
                bool isSelected = order == selected;
                float pulse = OrderPalette.UrgencyPulse(order, urgentBelowSeconds, Time.time);
                string seconds = $"{Mathf.CeilToInt(order.SecondsUntilLost)}s";

                switch (order.CurrentStep)
                {
                    case OrderStep.Gather:
                        // Every shop that sells a needed ingredient lights up, so the player can
                        // pick which one to drive to.
                        foreach (IngredientStop stop in order.Stops)
                        {
                            if (stop.Collected) continue;
                            Sprite icon = stop.Ingredient != null ? IngredientIcons.Icon(stop.Ingredient) : null;
                            Color iconColour = stop.Ingredient != null ? IngredientIcons.Colour(stop.Ingredient) : colour;

                            Order.PlacesFor(stop, _places);
                            foreach (Vector3 place in _places)
                                AddTarget(place, $"#{order.Id} {stop.Name.ToUpper()} {seconds}", colour, pickupR,
                                    isSelected, pulse, icon, iconColour);
                        }
                        break;

                    case OrderStep.Cook:
                        if (!order.HasBay) break;
                        bool ready = order.State == OrderState.Ready;
                        AddTarget(order.BayPoint, $"#{order.Id} {(ready ? "READY" : "COOK")} {seconds}", colour, bayR,
                            isSelected, pulse, null, colour);
                        break;

                    default:
                        AddTarget(order.DropOffPoint, $"#{order.Id} DROP {seconds}", colour, dropR,
                            isSelected, pulse, null, colour);
                        break;
                }
            }
        }

        /// <summary>Adds a place to mark, merging with one already at the same spot. The first
        /// (oldest) order keeps its colour, since it is the most urgent.</summary>
        private void AddTarget(Vector3 position, string label, Color colour, float radius, bool selected,
            float pulse, Sprite icon, Color iconColour)
        {
            // Each line of a shared label is tinted in its own order's colour.
            string line = $"<color=#{OrderPalette.Hex(colour)}>{label}</color>";

            foreach (Target existing in _targets)
            {
                if ((existing.Position - position).sqrMagnitude < 1f)
                {
                    existing.Label += "\n" + line;
                    existing.Radius = Mathf.Max(existing.Radius, radius);
                    existing.Pulse = Mathf.Max(existing.Pulse, pulse);
                    if (existing.Icon == null && icon != null)
                    {
                        existing.Icon = icon;
                        existing.IconColour = iconColour;
                    }
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
            t.Pulse = pulse;
            t.Icon = icon;
            t.IconColour = iconColour;
            _targets.Add(t);
        }

        private void PlaceBeacon(BeaconSlot slot, Target t, Color colour, float sizeScale)
        {
            if (!slot.Root.activeSelf) slot.Root.SetActive(true);

            Vector3 groundPosition = t.Position;
            slot.Root.transform.position = groundPosition;

            // A gentle breathing pulse, plus a bigger, quicker swell when the order is nearly
            // out of time. Size changes only: the colour never flashes.
            float pulse = (1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScaleAmount) * Mathf.Max(1f, t.Pulse);
            float height = beamHeight * sizeScale;
            float radius = beamRadius * sizeScale;
            float halfHeight = height * 0.5f * pulse;

            slot.Beam.localPosition = new Vector3(0f, halfHeight, 0f);
            slot.Beam.localScale = new Vector3(radius * pulse, halfHeight, radius * pulse);

            OrderWorldMaterial.SetColour(slot.Block, colour);
            slot.BeamRenderer.SetPropertyBlock(slot.Block);

            // The disc IS the real zone radius, so its size comes straight from ZoneDetection.
            float discPulse = 1f + Mathf.Sin(Time.time * pulseSpeed + 1.5f) * (pulseScaleAmount * 0.5f);
            float discDiameter = t.Radius * 2f * discPulse;
            slot.GroundDisc.localPosition = new Vector3(0f, groundDiscYOffset, 0f);
            slot.GroundDisc.localScale = new Vector3(discDiameter, 0.02f, discDiameter);

            Color discColour = colour;
            discColour.a = 0.6f;
            OrderWorldMaterial.SetColour(slot.DiscBlock, discColour);
            slot.GroundDiscRenderer.SetPropertyBlock(slot.DiscBlock);

            // Ingredient icon on a disc in its type colour, on top of the beam, so the beacon
            // matches the chip on the order card. Then the label above it.
            Vector3 top = groundPosition + Vector3.up * (height * pulse + numberHeightAboveBeam * sizeScale);
            bool hasIcon = t.Icon != null;
            slot.IconRoot.gameObject.SetActive(hasIcon);
            if (hasIcon)
            {
                float iconSize = 2.4f * sizeScale;
                slot.IconRoot.position = top + Vector3.up * (iconSize * 0.5f);
                slot.IconRoot.localScale = Vector3.one * iconSize;
                slot.Icon.sprite = t.Icon;
                // Sized from the image itself, so it fits the disc however the PNG was imported.
                slot.Icon.transform.localScale = Vector3.one * (0.72f / Mathf.Max(0.01f, t.Icon.bounds.size.x));
                slot.IconDisc.color = new Color(t.IconColour.r, t.IconColour.g, t.IconColour.b, Mathf.Max(0.8f, colour.a));
                slot.Icon.color = new Color(1f, 1f, 1f, Mathf.Max(0.8f, colour.a));
                if (_camera != null) slot.IconRoot.rotation = _camera.transform.rotation;
                top += Vector3.up * (iconSize + 0.3f * sizeScale);
            }

            slot.Number.text = t.Label;
            slot.Number.color = new Color(1f, 1f, 1f, Mathf.Max(0.75f, colour.a));
            slot.Number.characterSize = numberCharacterSize * sizeScale;
            slot.Number.transform.position = top;

            if (_camera != null)
            {
                Vector3 toCamera = _camera.transform.position - top;
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

            // Ingredient icon: a coloured disc with the white icon in front of it.
            var iconRoot = new GameObject("Ingredient icon");
            iconRoot.transform.SetParent(slotRoot.transform, false);

            var discGO2 = new GameObject("Disc");
            discGO2.transform.SetParent(iconRoot.transform, false);
            SpriteRenderer iconDisc = discGO2.AddComponent<SpriteRenderer>();
            iconDisc.sprite = IngredientIcons.Disc();
            iconDisc.sortingOrder = 0;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(iconRoot.transform, false);
            iconGO.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            iconGO.transform.localScale = Vector3.one * 0.72f;
            SpriteRenderer icon = iconGO.AddComponent<SpriteRenderer>();
            icon.sortingOrder = 1;

            iconRoot.SetActive(false);

            return new BeaconSlot
            {
                IconRoot = iconRoot.transform,
                IconDisc = iconDisc,
                Icon = icon,
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
