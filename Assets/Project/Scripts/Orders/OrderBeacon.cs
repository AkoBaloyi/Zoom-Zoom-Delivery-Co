using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Orders.UI
{
    /// <summary>
    /// A bright, in-world beacon at each active order's current target, pickup while Active,
    /// drop-off once Carried. Built to actually stand out from across the map: a saturated,
    /// pulsing beam, and the order's own number floating above it in large, camera-facing text.
    ///
    /// WHY THIS EXISTS ALONGSIDE DestinationMarker
    /// DestinationMarker is the screen-space arrow, useful for aiming while driving. This is the
    /// in-world object, useful for spotting a target from a distance before it's even worth
    /// checking the HUD. They answer different questions, which way to turn versus where exactly
    /// to stop, so both stay.
    ///
    /// WHY THIS REPLACES OrderMarkers' GROUND RINGS
    /// OrderMarkers' rings were a flat, low-alpha, unlabelled ring, functional but not something
    /// that grabs attention, and gave no way to tell two active orders apart beyond their colour.
    /// This is meant to be the version that actually ships: brighter, animated, and legible from
    /// range because it names the order. Once this is confirmed working in the scene, turn
    /// worldMarkers off on OrderMarkers (a serialized bool there already, no code change needed)
    /// so the two don't draw on top of each other.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderBeacon : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private OrderManager orderManager;

        [Tooltip("Found automatically if left empty. The ground disc's radius is read from this " +
                 "every frame, so the visual footprint always matches the real pickup/drop-off " +
                 "trigger exactly, no separate number to keep in sync by hand.")]
        [SerializeField] private ZoneDetection zoneDetection;

        [Tooltip("Used only if no ZoneDetection is found in the scene.")]
        [SerializeField] private float fallbackGroundDiscRadius = 6f;

        [Header("Pool")]
        [SerializeField] private int poolSize = 6;

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

        [Header("Colours, bright and saturated on purpose")]
        [SerializeField] private Color pickupColour = new Color(0.15f, 0.55f, 1f, 1f);
        [SerializeField] private Color dropOffColour = new Color(0.1f, 1f, 0.25f, 1f);
        [SerializeField] private Color urgentColour = new Color(1f, 0.05f, 0.05f, 1f);
        [SerializeField] private float urgentBelowSeconds = 8f;

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

        private readonly List<BeaconSlot> _slots = new List<BeaconSlot>();
        private static Material _beamMaterial;
        private Camera _camera;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (zoneDetection == null) zoneDetection = FindAnyObjectByType<ZoneDetection>();
            _camera = Camera.main;

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
                    $"{fallbackGroundDiscRadius}m. The ground disc may not match the real pickup " +
                    "zone if one exists somewhere this couldn't find it.", this);
            }

            BuildPool();
        }

        private void Update()
        {
            if (_camera == null) _camera = Camera.main;

            IReadOnlyList<Order> active = orderManager.ActiveOrders;
            int used = 0;

            for (int i = 0; i < active.Count && used < _slots.Count; i++)
            {
                Order order = active[i];
                if (order.IsTerminal) continue;

                bool goingToDropOff = order.State == OrderState.Carried
                                     || order.State == OrderState.Collected;

                Vector3 target = goingToDropOff ? order.DropOffPoint : order.PickupPoint;
                Color baseColour = goingToDropOff ? dropOffColour : pickupColour;

                float urgency = 1f - Mathf.Clamp01(order.TimeRemaining / Mathf.Max(0.01f, urgentBelowSeconds));
                Color colour = Color.Lerp(baseColour, urgentColour, urgency);

                float discRadius = zoneDetection != null
                    ? (goingToDropOff ? zoneDetection.DropOffRadius : zoneDetection.PickupRadius)
                    : fallbackGroundDiscRadius;

                PlaceBeacon(_slots[used], target, colour, order.Id, discRadius);
                used++;
            }

            for (int i = used; i < _slots.Count; i++)
            {
                if (_slots[i].Root.activeSelf) _slots[i].Root.SetActive(false);
            }
        }

        private void PlaceBeacon(BeaconSlot slot, Vector3 groundPosition, Color colour, int orderId,
            float discRadius)
        {
            if (!slot.Root.activeSelf) slot.Root.SetActive(true);

            slot.Root.transform.position = groundPosition;

            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScaleAmount;
            float halfHeight = beamHeight * 0.5f * pulse;

            slot.Beam.localPosition = new Vector3(0f, halfHeight, 0f);
            slot.Beam.localScale = new Vector3(beamRadius * pulse, halfHeight, beamRadius * pulse);

            slot.Block.SetColor("_BaseColor", colour);
            slot.Block.SetColor("_Color", colour);
            slot.BeamRenderer.SetPropertyBlock(slot.Block);

            // The disc IS the real pickup/drop-off radius, not a decorative guess, so its size
            // comes straight from ZoneDetection every frame rather than being tuned separately.
            float discPulse = 1f + Mathf.Sin(Time.time * pulseSpeed + 1.5f) * (pulseScaleAmount * 0.5f);
            float discDiameter = discRadius * 2f * discPulse;
            slot.GroundDisc.localPosition = new Vector3(0f, groundDiscYOffset, 0f);
            slot.GroundDisc.localScale = new Vector3(discDiameter, 0.02f, discDiameter);

            Color discColour = colour;
            discColour.a = 0.6f; // was 0.35, too faint to register as the actual interactive boundary
            slot.DiscBlock.SetColor("_BaseColor", discColour);
            slot.DiscBlock.SetColor("_Color", discColour);
            slot.GroundDiscRenderer.SetPropertyBlock(slot.DiscBlock);

            slot.Number.text = $"#{orderId}";
            slot.Number.color = colour;

            Vector3 numberPos = groundPosition + Vector3.up * (beamHeight + numberHeightAboveBeam);
            slot.Number.transform.position = numberPos;

            if (_camera != null)
            {
                Vector3 toCamera = _camera.transform.position - numberPos;
                if (toCamera.sqrMagnitude > 0.001f)
                    slot.Number.transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
            }
        }

        private void BuildPool()
        {
            if (_beamMaterial == null) _beamMaterial = CreateBeamMaterial();

            var root = new GameObject("Order Beacons");
            root.transform.SetParent(transform, false);

            for (int i = 0; i < Mathf.Max(1, poolSize); i++)
            {
                var slotRoot = new GameObject($"Beacon {i}");
                slotRoot.transform.SetParent(root.transform, false);
                slotRoot.SetActive(false);

                GameObject beamGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beamGO.name = "Beam";
                Destroy(beamGO.GetComponent<Collider>());
                beamGO.transform.SetParent(slotRoot.transform, false);

                MeshRenderer beamRenderer = beamGO.GetComponent<MeshRenderer>();
                beamRenderer.sharedMaterial = _beamMaterial;
                beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beamRenderer.receiveShadows = false;

                GameObject discGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                discGO.name = "Ground Disc";
                Destroy(discGO.GetComponent<Collider>());
                discGO.transform.SetParent(slotRoot.transform, false);

                MeshRenderer discRenderer = discGO.GetComponent<MeshRenderer>();
                discRenderer.sharedMaterial = _beamMaterial;
                discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                discRenderer.receiveShadows = false;

                var numberGO = new GameObject("Number");
                numberGO.transform.SetParent(slotRoot.transform, false);
                TextMesh number = numberGO.AddComponent<TextMesh>();
                number.fontSize = numberFontSize;
                number.characterSize = numberCharacterSize;
                number.anchor = TextAnchor.MiddleCenter;
                number.alignment = TextAlignment.Center;
                number.fontStyle = FontStyle.Bold;

                MeshRenderer numberRenderer = numberGO.GetComponent<MeshRenderer>();
                numberRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                numberRenderer.receiveShadows = false;

                _slots.Add(new BeaconSlot
                {
                    Root = slotRoot,
                    Beam = beamGO.transform,
                    BeamRenderer = beamRenderer,
                    Block = new MaterialPropertyBlock(),
                    GroundDisc = discGO.transform,
                    GroundDiscRenderer = discRenderer,
                    DiscBlock = new MaterialPropertyBlock(),
                    Number = number
                });
            }
        }

        private static Material CreateBeamMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogError("[OrderBeacon] Found no usable shader, beams cannot be drawn.");
                return null;
            }

            var material = new Material(shader) { name = "Order beacon beam" };

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);

            return material;
        }
    }
}