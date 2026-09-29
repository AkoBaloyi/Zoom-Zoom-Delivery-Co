using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZoomZoom.Orders.UI;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Puts the order system on screen.
    ///
    /// WHY THIS IS A SEPARATE FILE AND NOT AN EDIT TO OrderManager
    /// The order logic already works. Orders spawn, time down and resolve, and the console says so.
    /// The problem was that none of it was visible: the only visual in the whole system was an
    /// OnDrawGizmosSelected, which draws nothing unless you happen to have the object selected in the
    /// hierarchy and nothing at all in a build. A working system nobody can see reads as a broken one.
    ///
    /// So this reads OrderManager and draws what it finds. It never calls a method that changes an
    /// order, which means it cannot break the delivery rules and can be deleted without consequence.
    /// It is also a new file rather than a change to an existing one, so it cannot cause a merge
    /// conflict with whoever owns the order system.
    ///
    /// This is a development view, not the finished HUD. The real order UI belongs to whoever owns the
    /// UI, and this should be switched off once that exists.
    ///
    /// EDIT: corner text was 14pt, effectively unreadable at a glance while driving. Bumped to a
    /// serialized field defaulting to 22pt so it can be tuned further without another code change.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderMarkers : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToOrderSystem()
        {
            OrderManager manager = Object.FindAnyObjectByType<OrderManager>();
            if (manager == null) return;

            if (manager.GetComponent<OrderMarkers>() != null) return;

            // This component has two halves, and each one retires independently the moment
            // something better is in the scene. The decision is made by looking at the scene, not
            // by a setting somebody has to remember to flip, so removing the real UI restores the
            // fallback automatically.
            //
            // The order list retires to OrderHUD, which draws the same information through a real
            // Canvas. Two lists of the same orders in two corners is clutter.
            //
            // The ground rings retire to OrderBeacon, which is strictly better at the same job: it
            // reads its disc radius from ZoneDetection every frame, so the footprint cannot drift
            // out of step with the real trigger, where the ring below duplicates the 6 m as a second
            // number to keep in sync by hand. It also numbers each beacon and stands a taller beam.
            bool listCovered = Object.FindAnyObjectByType<OrderHUD>() != null;
            bool ringsCovered = Object.FindAnyObjectByType<OrderBeacon>() != null;

            if (listCovered && ringsCovered)
            {
                Debug.Log(
                    "[OrderMarkers] Not attaching. OrderHUD owns the order list and OrderBeacon owns " +
                    "the ground markers, so there is nothing left for this to draw. Delete either of " +
                    "those and this comes back on its own.");
                return;
            }

            OrderMarkers markers = manager.gameObject.AddComponent<OrderMarkers>();
            if (listCovered) markers.orderList = false;
            if (ringsCovered) markers.worldMarkers = false;

            Debug.Log(
                "[OrderMarkers] Attached as a fallback. " +
                (ringsCovered
                    ? "OrderBeacon is present, so the ground rings are off and it owns them. "
                    : "No OrderBeacon found, so rings are painted on the ground at the pickup and " +
                      "drop-off points and F11 hides them. ") +
                (listCovered
                    ? "OrderHUD is present, so the built-in order list is off and the HUD owns it."
                    : "No OrderHUD found, so the built-in order list is being drawn."));
        }

        [Header("Wiring")]
        [SerializeField] private OrderManager orderManager;

        [Header("Display")]
        [SerializeField] private bool show = true;
        [SerializeField] private Key toggleKey = Key.F11;
        [SerializeField] private bool worldMarkers = true;
        [SerializeField] private bool orderList = true;

        [Header("Order list text")]
        [Tooltip("Font size of the order list in the corner. Was 14, unreadable while driving.")]
        [SerializeField] private int orderListFontSize = 22;

        [Tooltip("Width of the order list panel, metres... pixels. Widened to fit the larger font " +
                 "without wrapping.")]
        [SerializeField] private float orderListWidth = 400f;

        [Header("Ground rings")]
        [SerializeField] private float ringRadius = 6f;
        [SerializeField] private float ringThickness = 0.9f;
        [SerializeField] private float ringGroundOffset = 0.06f;
        [Range(12, 128)]
        [SerializeField] private int ringSegments = 64;
        [SerializeField] private float pillarHeight = 9f;
        [SerializeField] private Color pickupColour = new Color(0.2f, 0.75f, 1f, 0.55f);
        [SerializeField] private Color dropOffColour = new Color(0.3f, 1f, 0.4f, 0.7f);
        [SerializeField] private Color urgentColour = new Color(1f, 0.25f, 0.15f, 0.85f);
        [SerializeField] private float urgentBelowSeconds = 8f;

        private readonly List<GameObject> _beaconPool = new List<GameObject>();
        private Material _beaconMaterial;
        private Mesh _ringMesh;
        private Transform _beaconRoot;
        private Transform _player;
        private GUIStyle _style;
        private GUIStyle _shadow;

        private void Awake()
        {
            if (orderManager == null) orderManager = GetComponent<OrderManager>();
            if (orderManager == null) orderManager = Object.FindAnyObjectByType<OrderManager>();

            if (orderManager == null)
            {
                Debug.LogWarning(
                    "[OrderMarkers] No OrderManager in the scene, so there is nothing to draw. " +
                    "Switching off rather than checking every frame.", this);
                enabled = false;
                return;
            }

            var root = new GameObject("Order beacons");
            _beaconRoot = root.transform;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _player = player.transform;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) show = !show;

            if (!worldMarkers) return;

            if (!show)
            {
                HideFrom(0);
                return;
            }

            int used = 0;
            IReadOnlyList<Order> orders = orderManager.ActiveOrders;

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];
                if (order.IsTerminal) continue;

                bool carrying = order.State == OrderState.Carried || order.State == OrderState.Collected;
                Vector3 target = carrying ? order.DropOffPoint : order.PickupPoint;
                Color baseColour = carrying ? dropOffColour : pickupColour;

                float urgency = 1f - Mathf.Clamp01(order.TimeRemaining / Mathf.Max(0.01f, urgentBelowSeconds));
                Color colour = Color.Lerp(baseColour, urgentColour, urgency);

                PlaceBeacon(used, target, colour);
                used++;
            }

            HideFrom(used);
        }

        private void PlaceBeacon(int index, Vector3 groundPosition, Color colour)
        {
            while (_beaconPool.Count <= index) _beaconPool.Add(CreateBeacon());

            GameObject beacon = _beaconPool[index];
            if (!beacon.activeSelf) beacon.SetActive(true);

            beacon.transform.position = groundPosition + Vector3.up * ringGroundOffset;
            beacon.transform.localScale = Vector3.one * ringRadius;

            var renderer = beacon.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", colour);
                block.SetColor("_Color", colour);
                renderer.SetPropertyBlock(block);
            }
        }

        private GameObject CreateBeacon()
        {
            var beacon = new GameObject("Order ring");

            if (_ringMesh == null) _ringMesh = BuildRingMesh();
            beacon.AddComponent<MeshFilter>().sharedMesh = _ringMesh;

            if (_beaconMaterial == null) _beaconMaterial = CreateBeaconMaterial();

            MeshRenderer renderer = beacon.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _beaconMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            beacon.transform.SetParent(_beaconRoot, true);
            return beacon;
        }

        private Mesh BuildRingMesh()
        {
            int segments = Mathf.Clamp(ringSegments, 12, 128);

            float half = Mathf.Max(0.01f, ringThickness * 0.5f) / Mathf.Max(0.01f, ringRadius);
            float inner = Mathf.Max(0.02f, 1f - half);
            float outer = 1f + half;

            var vertices = new List<Vector3>((segments + 1) * 2 + 8);
            var colours = new List<Color>((segments + 1) * 2 + 8);
            var uvs = new List<Vector2>((segments + 1) * 2 + 8);
            var triangles = new List<int>(segments * 6 + 12);

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);

                vertices.Add(new Vector3(cos * inner, 0f, sin * inner));
                vertices.Add(new Vector3(cos * outer, 0f, sin * outer));

                colours.Add(Color.white);
                colours.Add(Color.white);

                uvs.Add(new Vector2(t, 0f));
                uvs.Add(new Vector2(t, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = i * 2 + 1, c = i * 2 + 2, d = i * 2 + 3;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }

            if (pillarHeight > 0.01f)
            {
                float h = pillarHeight / Mathf.Max(0.01f, ringRadius);
                float w = Mathf.Min(0.35f, half * 2.5f);

                AddPillarQuad(vertices, colours, uvs, triangles, new Vector3(1f, 0f, 0f), w, h);
                AddPillarQuad(vertices, colours, uvs, triangles, new Vector3(0f, 0f, 1f), w, h);
            }

            var mesh = new Mesh { name = "Order ring" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddPillarQuad(List<Vector3> vertices, List<Color> colours, List<Vector2> uvs,
            List<int> triangles, Vector3 axis, float halfWidth, float height)
        {
            int start = vertices.Count;

            Vector3 side = axis * halfWidth;

            vertices.Add(-side);
            vertices.Add(side);
            vertices.Add(-side + Vector3.up * height);
            vertices.Add(side + Vector3.up * height);

            colours.Add(Color.white);
            colours.Add(Color.white);
            colours.Add(new Color(1f, 1f, 1f, 0f));
            colours.Add(new Color(1f, 1f, 1f, 0f));

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));

            triangles.Add(start + 0); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private static Material CreateBeaconMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogError("[OrderMarkers] Found no usable shader, so the rings cannot be drawn.");
                return null;
            }

            var material = new Material(shader) { name = "Order ring" };

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);

            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha"))
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha"))
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);

            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);

            return material;
        }

        private void HideFrom(int index)
        {
            for (int i = index; i < _beaconPool.Count; i++)
            {
                if (_beaconPool[i].activeSelf) _beaconPool[i].SetActive(false);
            }
        }

        private void OnGUI()
        {
            if (!show || !orderList || orderManager == null) return;

            BuildStyles();

            IReadOnlyList<Order> orders = orderManager.ActiveOrders;

            var text = new System.Text.StringBuilder();
            text.AppendLine($"ORDERS  {orders.Count} live   {orderManager.TotalResolved} resolved");
            text.AppendLine();

            if (orders.Count == 0)
            {
                text.AppendLine("waiting for the first spawn");
            }

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];
                Vector3 from = _player != null ? _player.position : transform.position;

                float distance = Vector3.Distance(
                    from,
                    order.State == OrderState.Carried ? order.DropOffPoint : order.PickupPoint);

                text.AppendLine(
                    $"#{order.Id,-3} {order.State,-9} {order.TimeRemaining,5:0.0}s  {distance,5:0} m");
            }

            var area = new Rect(Screen.width - orderListWidth - 12f, 12f, orderListWidth, 340f);

            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text.ToString(), _shadow);
            GUI.Label(area, text.ToString(), _style);
        }

        private void BuildStyles()
        {
            // Rebuild whenever the tuned size no longer matches, so changing orderListFontSize in
            // the Inspector at runtime actually takes effect instead of needing a replay.
            if (_style != null && _style.fontSize == orderListFontSize) return;

            _style = new GUIStyle(GUI.skin.label) { fontSize = orderListFontSize, alignment = TextAnchor.UpperLeft };
            _style.font = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Courier New", "monospace" }, orderListFontSize);
            _style.normal.textColor = Color.white;

            _shadow = new GUIStyle(_style);
            _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }

        private void OnDestroy()
        {
            if (_beaconRoot != null) Destroy(_beaconRoot.gameObject);
            if (_beaconMaterial != null) Destroy(_beaconMaterial);
            if (_ringMesh != null) Destroy(_ringMesh);
        }
    }
}