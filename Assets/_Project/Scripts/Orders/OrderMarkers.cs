using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderMarkers : MonoBehaviour
    {
        /// <summary>
        /// Attaches this component to the order system automatically when a scene loads.
        ///
        /// WHY IT SELF-ATTACHES INSTEAD OF BEING PLACED IN THE SCENE
        /// A component nobody adds does nothing, and that is a failure with no symptom: the file
        /// compiles, the scene loads, no error is logged, and the feature is simply absent. That is
        /// exactly what happened here the first time round.
        ///
        /// Adding it to the scene by hand would fix it once, but the scene is shared, so the edit has to
        /// survive whoever next rebuilds or re-bakes it. Attaching at load means the marker follows the
        /// order system wherever it exists, in the editor and in a build, without a scene change anyone
        /// has to remember to keep.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToOrderSystem()
        {
            OrderManager manager = Object.FindAnyObjectByType<OrderManager>();
            if (manager == null) return;

            if (manager.GetComponent<OrderMarkers>() != null) return;

            manager.gameObject.AddComponent<OrderMarkers>();

            Debug.Log(
                "[OrderMarkers] Attached to the order system. F11 hides the beacons and the order list. " +
                "Cargo pickup and delivery are still driven by OrderFlowTestHarness until real zone " +
                "detection exists: C collects the oldest active order, V delivers what is carried, " +
                "L logs every order's state.");
        }

        [Header("Wiring")]
        [Tooltip("The order system being watched. Found on this object, or anywhere in the scene, if " +
                 "left empty.")]
        [SerializeField] private OrderManager orderManager;

        [Header("Display")]
        [Tooltip("Show the world markers and the order list. Toggled at runtime with the key below.")]
        [SerializeField] private bool show = true;

        // F11 rather than F8, because VehicleMeasurement in this scene already owns F1 to F9 and F8
        // there resets the car to the start line. Sharing the key meant hiding the beacons also
        // teleported the player, which reads as the markers breaking the car.
        [Tooltip("Key that shows and hides the order markers. F1 to F9 are taken by the vehicle lab " +
                 "tools, so pick something outside that range.")]
        [SerializeField] private Key toggleKey = Key.F11;

        [Tooltip("Draw a beacon at each pickup and drop-off point.")]
        [SerializeField] private bool worldMarkers = true;

        [Tooltip("List the live orders in the top right corner.")]
        [SerializeField] private bool orderList = true;

        [Header("Ground rings")]
        // WHY A RING ON THE FLOOR AND NOT A TOWER IN THE AIR
        // The first version stood a 12 m cube at each point, and it did not work. The lab already has
        // marker posts every 5 m and label posts on every surface strip, so a tall coloured box reads
        // as more lab furniture. Worse, a tower tells you roughly where to go but never where to STOP,
        // and stopping accurately is the whole interaction at a delivery point.
        //
        // A ring painted on the ground is what Crazy Taxi uses, and it answers both questions at once:
        // the centre is the target and the edge is the tolerance. It also gives real zone detection an
        // obvious home later, because the ring IS the trigger volume rather than a decoration next to
        // one.
        [Tooltip("Radius of the ring painted on the ground, metres. This doubles as the arrival " +
                 "tolerance the player reads, so it should match whatever zone detection ends up using.")]
        [SerializeField] private float ringRadius = 6f;

        [Tooltip("How thick the painted line is, metres.")]
        [SerializeField] private float ringThickness = 0.9f;

        [Tooltip("How far above the ground the ring sits, metres. Small, but not zero: at zero the ring " +
                 "and the road are in the same plane and fight over which gets drawn, which flickers.")]
        [SerializeField] private float ringGroundOffset = 0.06f;

        [Tooltip("How many segments the ring is built from. 64 looks round at every size a car can " +
                 "drive up to and costs nothing.")]
        [Range(12, 128)]
        [SerializeField] private int ringSegments = 64;

        [Tooltip("Height of the soft column of light above the ring, metres. Zero switches it off. " +
                 "The ring answers where to stop; this answers where to look from far away.")]
        [SerializeField] private float pillarHeight = 9f;

        [Tooltip("Colour of a pickup the player has not collected yet.")]
        [SerializeField] private Color pickupColour = new Color(0.2f, 0.75f, 1f, 0.55f);

        [Tooltip("Colour of the drop-off for cargo already aboard. This is the one the player is " +
                 "actually driving towards, so it is the one that should stand out.")]
        [SerializeField] private Color dropOffColour = new Color(0.3f, 1f, 0.4f, 0.7f);

        [Tooltip("Colour a beacon shifts towards as its order runs out of time.")]
        [SerializeField] private Color urgentColour = new Color(1f, 0.25f, 0.15f, 0.85f);

        [Tooltip("Seconds remaining at which a beacon is fully urgent.")]
        [SerializeField] private float urgentBelowSeconds = 8f;

        // ---------------- state ----------------

        // Pooled, because orders come and go constantly and creating a beacon per order per frame
        // would churn the garbage collector for no reason.
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

            // By tag, so this never needs a reference to the vehicle system.
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _player = player.transform;
        }

        private void Update()
        {
            // Read straight off the keyboard device, not UnityEngine.Input. This project has active
            // input handling set to the Input System package, and the old class throws
            // InvalidOperationException on every call under that setting, which meant this Update
            // aborted before drawing a single beacon and filled the console instead.
            //
            // The null check matters: Keyboard.current is null when no keyboard is present, which is
            // normal on a gamepad-only or mobile session, not an error.
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

                // One beacon per order, at whichever end the player needs next. Showing both at once
                // doubles the clutter and hides the one piece of information that matters: where to go.
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

        /// <summary>
        /// Positions a pooled beacon, growing the pool only when a frame genuinely needs more than it
        /// has ever needed before.
        /// </summary>
        private void PlaceBeacon(int index, Vector3 groundPosition, Color colour)
        {
            while (_beaconPool.Count <= index) _beaconPool.Add(CreateBeacon());

            GameObject beacon = _beaconPool[index];
            if (!beacon.activeSelf) beacon.SetActive(true);

            // Flat on the ground, unrotated. The mesh is already built in the XZ plane at unit radius,
            // so placing it is a position and a uniform scale and nothing else.
            beacon.transform.position = groundPosition + Vector3.up * ringGroundOffset;
            beacon.transform.localScale = Vector3.one * ringRadius;

            // A per-renderer property block rather than a material per beacon, so all of them share one
            // material and one draw setup no matter how many orders are live.
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

            // No collider. A marker the car can hit would change the driving, and this is a view of
            // the game rather than part of it. Built from an empty GameObject rather than
            // CreatePrimitive for the same reason: a primitive arrives with a collider to remove.
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

        /// <summary>
        /// A flat ring in the XZ plane at unit radius, plus an optional soft column above it.
        ///
        /// WHY THIS IS GENERATED AND NOT A TEXTURE
        /// The tyre smoke came from a public domain art pack, because a soft grey puff is a picture and
        /// drawing one by hand is wasted effort. A ring is not a picture, it is four numbers, and a
        /// generated one is crisp at any radius while a 512 pixel PNG stretched over 12 metres of road
        /// is a blurry smear. Different problems, different answers.
        ///
        /// The column is built from two crossed quads rather than a cylinder. Seen from a car it reads
        /// the same, it is 8 vertices instead of hundreds, and it cannot be mistaken for solid geometry
        /// the way a shaded cylinder can.
        /// </summary>
        private Mesh BuildRingMesh()
        {
            int segments = Mathf.Clamp(ringSegments, 12, 128);

            // Thickness is expressed in metres but the mesh is unit radius and scaled at placement, so
            // it has to be divided through by the radius to survive that scaling.
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

                // White vertex colour: the per-order tint arrives through the property block, so one
                // mesh and one material serve every ring on screen whatever colour it needs to be.
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

        /// <summary>
        /// One upright quad through the centre, fading out with height so the column reads as light
        /// rather than as a wall. The fade is in the vertex alpha, which is why the material has to be
        /// vertex coloured.
        /// </summary>
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

        /// <summary>
        /// Vertex-coloured, alpha-blended, unlit, double sided.
        ///
        /// WHY THE BLEND STATE IS SET BY HAND
        /// The previous version set _Surface to 1 and expected a transparent material. On URP that does
        /// nothing. _Surface and _Blend are inputs to URP's material EDITOR, which reads them and then
        /// writes the state that actually decides the pixel: _SrcBlend, _DstBlend, _ZWrite and the
        /// _SURFACE_TYPE_TRANSPARENT keyword. No editor code runs for a material built with
        /// new Material(), so the markers were rendering fully OPAQUE despite their alpha, which is
        /// exactly the "beacon hides the corner behind it" problem the old comment claimed to have
        /// solved. The same bug was in the skid marks and the tyre particles.
        ///
        /// WHY THIS IS DUPLICATED FROM SkidMarks RATHER THAN SHARED
        /// SkidMarks has an identical helper, and calling it would make the order system reference the
        /// vehicle system. That boundary is deliberate and stated all over both: the orders know nothing
        /// about the car, which is what lets either be worked on or replaced without the other. Fifteen
        /// lines of URP setup is a cheaper price than that coupling.
        /// </summary>
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

            // Editor-facing hints, so the material still reads correctly if anyone opens it.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);

            // The state that actually applies. Straight alpha blending.
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

            // Double sided, because the pillar quads are flat and would vanish from one side otherwise.
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
                // Distance from the CAR, not from this component. Found by tag rather than by type so
                // the order system keeps knowing nothing about the vehicle system.
                Vector3 from = _player != null ? _player.position : transform.position;

                float distance = Vector3.Distance(
                    from,
                    order.State == OrderState.Carried ? order.DropOffPoint : order.PickupPoint);

                text.AppendLine(
                    $"#{order.Id,-3} {order.State,-9} {order.TimeRemaining,5:0.0}s  {distance,5:0} m");
            }

            var area = new Rect(Screen.width - 330f, 12f, 318f, 300f);

            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text.ToString(), _shadow);
            GUI.Label(area, text.ToString(), _style);
        }

        private void BuildStyles()
        {
            if (_style != null) return;

            _style = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperLeft };
            _style.font = Font.CreateDynamicFontFromOSFont(
                new[] { "Consolas", "Courier New", "monospace" }, 14);
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
