using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// Draws tyre marks as ONE procedural mesh with a fixed-size ring buffer of quads.
    ///
    /// WHY ONE MESH AND NOT A TRAIL RENDERER PER WHEEL
    /// Four TrailRenderers is the obvious answer and it is the wrong one here. A TrailRenderer
    /// billboards towards the camera, so a mark on the ground twists as the camera swings and stops
    /// looking like it is painted on the road. It also has no notion of the surface normal, so marks
    /// on a ramp float. And it cannot vary width or colour per segment, which is exactly what is
    /// needed when the car crosses from tarmac onto dirt mid-slide.
    ///
    /// A single mesh solves all three: every quad is laid flat against the ground normal recorded at
    /// the moment it was placed, carries its own vertex colour, and belongs to one draw call no
    /// matter how many wheels are marking.
    ///
    /// WHY A RING BUFFER
    /// Marks have to be bounded or a long session eats memory until the frame rate dies. A fixed
    /// array with a wrapping write index means the oldest mark is overwritten by the newest, memory
    /// is allocated exactly once at startup, and the per-frame cost never grows. Nothing here
    /// allocates after Awake, so this does not feed the garbage collector during play.
    ///
    /// The general approach follows the well-trodden Unity skidmark technique, closest public
    /// reference being Nition/UnitySkidmarks. This is an independent implementation written against
    /// this car's per-wheel contact data rather than a port of that code.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class SkidMarks : MonoBehaviour
    {
        /// <summary>
        /// One placed mark. Stores the two edges of the tyre rather than a centre point, because the
        /// next mark has to stitch onto exactly this edge for the ribbon to be continuous.
        /// </summary>
        private struct Mark
        {
            public Vector3 leftEdge;
            public Vector3 rightEdge;
            public Vector3 normal;
            public Color colour;
            public int previous;   // index of the mark this one connects back to, -1 to start fresh
        }

        [Header("Material (leave empty to generate at runtime)")]
        [Tooltip("Must be a vertex-coloured transparent material: the mesh carries the surface colour " +
                 "in its vertex colours, which is how one mesh shows rubber on tarmac and dust on dirt.")]
        [SerializeField] private Material markMaterialAsset;

        [Header("Budget")]
        [Tooltip("How many mark segments exist before the oldest is recycled. Each one is a quad, so " +
                 "1024 segments is 4096 vertices, which is nothing for a GPU. Raise it for longer " +
                 "trails, lower it if the marks are eating memory on a weak machine.")]
        [SerializeField] private int maxMarks = 1024;

        [Tooltip("Metres the wheel must travel before another segment is laid. Too small wastes the " +
                 "budget on overlapping quads at low speed, too large makes the trail visibly faceted " +
                 "through a tight corner.")]
        [SerializeField] private float minimumSegmentDistance = 0.12f;

        [Header("Look")]
        [Tooltip("Width of a mark, metres. Should be a touch narrower than the tyre so the mark reads " +
                 "as coming from under the wheel rather than from beside it.")]
        [SerializeField] private float markWidth = 0.3f;

        [Tooltip("Slice of the mark texture sampled across the width of the tyre, as a pair of U " +
                 "coordinates. FX_SkidStreak is a soft vertical streak, so the middle of it is the " +
                 "part that reads as a tyre mark. Widen this to take in more of the soft halo.")]
        [SerializeField] private Vector2 markTextureURange = new Vector2(0.42f, 0.58f);

        [Tooltip("Height up the mark texture that every segment samples. Held constant along the " +
                 "length of the mark on purpose, so consecutive segments tile with no visible seam.")]
        [Range(0f, 1f)]
        [SerializeField] private float markTextureV = 0.5f;

        [Tooltip("Opacity of the very faintest mark, before the slide has built up. Without a floor " +
                 "here a light scuff computes an alpha near zero and simply cannot be seen, which " +
                 "reads as the marks being broken rather than as a gentle corner.")]
        [Range(0f, 1f)]
        [SerializeField] private float minimumOpacity = 0.45f;

        [Tooltip("How far the mark is lifted off the ground, metres. Without a small lift the mark " +
                 "and the road occupy the same plane and fight over which one is drawn, which flickers.")]
        [SerializeField] private float groundOffset = 0.02f;

        [Tooltip("Sideways slide, in m/s, at which a mark reaches full opacity. Below this the mark " +
                 "fades in, so a gentle corner leaves a faint scuff and a full slide leaves a black " +
                 "stripe. This is what makes the marks readable as a record of what the car did.")]
        [SerializeField] private float slipForFullOpacity = 6f;

        [Tooltip("Slide below this, in m/s, leaves no mark at all. Stops the car painting a permanent " +
                 "line everywhere it drives.")]
        [SerializeField] private float minimumSlipToMark = 1.2f;

        // ---------------- state ----------------

        private Mark[] _marks;
        private int _writeIndex;
        private int _markCount;

        // Geometry buffers, sized once and reused forever.
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Color[] _colours;
        private Vector2[] _uvs;
        private int[] _triangles;

        private Mesh _mesh;
        private bool _dirty;

        // The last mark laid by each wheel, so a segment knows what to stitch onto. -1 means the
        // wheel is not currently marking, so the next mark starts a new ribbon instead of stretching
        // a quad across the gap where the car was airborne.
        private const int MaxWheels = 8;
        private readonly int[] _lastMarkPerWheel = new int[MaxWheels];
        private readonly Vector3[] _lastPositionPerWheel = new Vector3[MaxWheels];

        private void Awake()
        {
            maxMarks = Mathf.Max(16, maxMarks);

            _marks = new Mark[maxMarks];
            _vertices = new Vector3[maxMarks * 4];
            _normals = new Vector3[maxMarks * 4];
            _colours = new Color[maxMarks * 4];
            _uvs = new Vector2[maxMarks * 4];
            _triangles = new int[maxMarks * 6];

            for (int i = 0; i < MaxWheels; i++) _lastMarkPerWheel[i] = -1;

            _mesh = new Mesh { name = "Skid marks" };

            // Without this a trail longer than 65535 vertices silently breaks. 1024 quads is well
            // under that, but the budget is a serialised field and somebody will raise it.
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            // The mesh is written in world space, so Unity's automatic bounds would be recalculated
            // constantly and the marks would pop in and out as the car drove. A large fixed bounds
            // costs one always-visible draw call and removes the problem.
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            // Fetched rather than assumed. RequireComponent covers the inspector's Add Component
            // flow, but this object is also built in code by TyreEffects, and if the attribute ever
            // fails to fire there the next line is a null dereference inside Awake, which aborts the
            // component silently and takes the marks with it.
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();

            // Saved asset first, then whatever is already on the renderer, then a generated fallback.
            if (markMaterialAsset != null) renderer.sharedMaterial = markMaterialAsset;
            else if (renderer.sharedMaterial == null)
                renderer.sharedMaterial = CreateVertexColouredTransparent("Skid mark");

            // Marks lie on the floor and cannot meaningfully cast or receive shadows, and turning
            // both off avoids a shadow pass over a large always-visible mesh.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _renderer = renderer;

            // Says out loud what it ended up with. "No skid marks" has several possible causes that
            // look identical on screen: no material, an opaque material, a missing shader, or marks
            // never being offered at all. This line separates the first three from the last one
            // without anyone having to attach a debugger.
            Material inUse = renderer.sharedMaterial;

            // Whether a texture is bound is called out explicitly, because an untextured particle or
            // decal material does not fail, it draws a plain quad. That is what made the tyre smoke
            // look like flying squares, and it is invisible as a cause unless something says so.
            bool hasTexture = inUse != null
                              && inUse.HasProperty("_BaseMap")
                              && inUse.GetTexture("_BaseMap") != null;

            Debug.Log(
                $"[SkidMarks] Ready. Budget {maxMarks} segments. " +
                $"Material '{(inUse != null ? inUse.name : "NONE")}' " +
                $"shader '{(inUse != null && inUse.shader != null ? inUse.shader.name : "NONE")}' " +
                $"queue {(inUse != null ? inUse.renderQueue : -1)} " +
                $"texture {(hasTexture ? inUse.GetTexture("_BaseMap").name : "NONE, marks will be hard-edged quads")} " +
                $"{(markMaterialAsset != null ? "(saved asset)" : "(generated at runtime)")}. " +
                $"Marks need at least {minimumSlipToMark:0.0} m/s of sideways slide, " +
                $"and the telemetry overlay counts them on the MARKS line.", this);
        }

        /// <summary>How many segments have been laid since the last Clear. Diagnostics only.</summary>
        public int SegmentsLaid { get; private set; }

        /// <summary>
        /// How many segments actually produced drawable triangles, as opposed to the degenerate first
        /// segment of each slide. If this is zero while SegmentsLaid is not, the marks are being
        /// recorded but every ribbon is being broken before it can connect, which points at the slip
        /// threshold or the grounded check rather than at the material.
        /// </summary>
        public int SegmentsDrawn { get; private set; }

        /// <summary>Whether the mesh is currently attached to a renderer that could draw it.</summary>
        public bool IsRenderable =>
            _renderer != null && _renderer.enabled && _renderer.sharedMaterial != null;

        private MeshRenderer _renderer;

        /// <summary>
        /// Vertex-coloured, alpha-blended, unlit. Vertex colour is the important part: it is how one
        /// mesh carries black rubber on tarmac and pale dust on dirt at the same time.
        ///
        /// WHY THIS SETS THE BLEND STATE BY HAND
        /// The obvious way to make a URP material transparent is SetFloat("_Surface", 1), and that
        /// does nothing. _Surface, _Blend and _Mode are inputs to URP's material EDITOR, which reads
        /// them and then writes the real state: _SrcBlend, _DstBlend, _ZWrite, the
        /// _SURFACE_TYPE_TRANSPARENT keyword and the render queue. Nothing runs that editor code for a
        /// material built with new Material(), so a runtime material that only sets _Surface keeps the
        /// shader's defaults of One/Zero and throws the alpha away.
        ///
        /// That is what was wrong with the marks. The mesh was correct and the colours were correct,
        /// and every quad was drawn with its alpha ignored.
        ///
        /// The saved FX_SkidMark.mat asset is still the better source, because it was written by that
        /// editor code and is therefore right by construction. This is the fallback for a car dropped
        /// into a scene that has no such asset.
        /// </summary>
        public static Material CreateVertexColouredTransparent(string materialName)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");

            if (shader == null)
            {
                Debug.LogError(
                    "[SkidMarks] Found no usable shader for tyre marks. Marks will not be visible. " +
                    "This normally means the URP particle shaders were stripped from the build.");
                return null;
            }

            var material = new Material(shader) { name = materialName };

            // The editor-facing hints, kept so the material reads correctly if anyone opens it.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 0f);

            // The state that actually decides how the pixel lands. Straight alpha blending:
            // result = source * sourceAlpha + destination * (1 - sourceAlpha).
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            // Newer URP splits the alpha channel's blend out separately. Absent on older versions,
            // hence the guard rather than an unconditional set.
            if (material.HasProperty("_SrcBlendAlpha"))
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha"))
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            // Marks lie flat on the road and overlap each other constantly. Writing depth would make
            // them occlude one another and flicker where two trails cross.
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);

            // The keyword is what selects the transparent variant of the shader. Without it the
            // opaque variant is compiled and used no matter what the blend floats say.
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");

            // Queue and tag put the marks in the transparent pass, after the road they sit on.
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // No shadow or depth contribution from a decal lying on the floor.
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);

            // White base colour and no texture, so the vertex colour is the only thing tinting the
            // mark. A tint here would multiply against every surface's mark colour.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            return material;
        }

        // ==================================================================
        // WHAT THE CAR CALLS
        // ==================================================================

        /// <summary>
        /// Offer a mark for one wheel. Decides for itself whether the slide is hard enough and the
        /// wheel has travelled far enough to be worth a segment, so the caller can simply hand over
        /// the wheel state every step without tracking any of that.
        /// </summary>
        /// <param name="wheelIndex">Which wheel, so its ribbon stays separate from the others.</param>
        /// <param name="position">Contact patch in world space.</param>
        /// <param name="normal">Ground normal at the contact patch.</param>
        /// <param name="forward">Direction of travel, used to work out which way is across the tyre.</param>
        /// <param name="slipSpeed">Sideways slide at this wheel, m/s. Absolute value is used.</param>
        /// <param name="surface">Surface under this wheel, for colour and how well it takes a mark.</param>
        public void ReportWheel(int wheelIndex, Vector3 position, Vector3 normal, Vector3 forward,
            float slipSpeed, SurfaceProfile surface)
        {
            if (_marks == null || wheelIndex < 0 || wheelIndex >= MaxWheels) return;

            float slip = Mathf.Abs(slipSpeed);

            // Not sliding, or this surface does not hold a mark. Break the ribbon so the next mark
            // does not stretch a quad from here to wherever the car starts sliding again.
            if (slip < minimumSlipToMark || surface.markStrength <= 0.01f)
            {
                _lastMarkPerWheel[wheelIndex] = -1;
                return;
            }

            // Far enough since the last segment? Comparing squared distances avoids a square root
            // in a method called four times per frame.
            int previous = _lastMarkPerWheel[wheelIndex];
            if (previous >= 0)
            {
                float moved = (position - _lastPositionPerWheel[wheelIndex]).sqrMagnitude;
                if (moved < minimumSegmentDistance * minimumSegmentDistance) return;
            }

            // Across the tyre: perpendicular to travel, flat against the ground. Falls back to the
            // world axis if the car is barely moving, so the mark never collapses to zero width.
            Vector3 across = Vector3.Cross(normal, forward);
            if (across.sqrMagnitude < 0.0001f) across = Vector3.Cross(normal, Vector3.forward);
            if (across.sqrMagnitude < 0.0001f) return;
            across = across.normalized * (markWidth * 0.5f);

            Vector3 lifted = position + normal * groundOffset;

            // Harder slide, darker mark. Multiplied by how well the surface takes a mark at all, so
            // ice records almost nothing and tarmac records everything.
            //
            // Ramped from minimumOpacity rather than from zero. The old version multiplied straight
            // by slip/slipForFullOpacity, so a mark laid at the 1.2 m/s threshold came out at 0.2 of
            // the surface colour's alpha, which on a grey floor is invisible. A mark worth recording
            // at all is worth being able to see.
            float slip01 = Mathf.Clamp01(slip / Mathf.Max(0.01f, slipForFullOpacity));
            float opacity = Mathf.Lerp(minimumOpacity, 1f, slip01) * surface.markStrength;

            Color colour = surface.markColour;
            colour.a *= opacity;

            int index = _writeIndex;

            _marks[index] = new Mark
            {
                leftEdge = lifted - across,
                rightEdge = lifted + across,
                normal = normal,
                colour = colour,

                // Only stitch to the previous mark if it has not already been recycled out from under
                // us. Without this check a wrapping buffer can join the newest mark to an ancient one
                // and draw a quad clean across the map.
                previous = previous >= 0 && IsStillLive(previous) ? previous : -1
            };

            WriteGeometry(index);

            _lastMarkPerWheel[wheelIndex] = index;
            _lastPositionPerWheel[wheelIndex] = position;

            SegmentsLaid++;
            if (_marks[index].previous >= 0) SegmentsDrawn++;

            _writeIndex = (_writeIndex + 1) % maxMarks;
            _markCount = Mathf.Min(_markCount + 1, maxMarks);
            _dirty = true;
        }

        /// <summary>
        /// Has this slot survived, or has the ring buffer already wrapped past it? A mark is dead once
        /// the write head has come all the way round to it again.
        /// </summary>
        private bool IsStillLive(int index)
        {
            if (_markCount < maxMarks) return index < _markCount;

            // Full buffer: the oldest live slot is the one about to be overwritten.
            return index != _writeIndex;
        }

        /// <summary>
        /// Fills the four vertices and six indices for one mark.
        ///
        /// A mark with no predecessor writes a degenerate quad, which draws nothing. That is
        /// deliberate: the first segment of a slide has nothing to stretch back to, and inventing a
        /// start point would put a stray triangle wherever the car happened to be.
        /// </summary>
        private void WriteGeometry(int index)
        {
            int v = index * 4;
            int t = index * 6;

            Mark mark = _marks[index];

            Vector3 previousLeft = mark.leftEdge;
            Vector3 previousRight = mark.rightEdge;
            Color previousColour = mark.colour;

            if (mark.previous >= 0)
            {
                Mark before = _marks[mark.previous];
                previousLeft = before.leftEdge;
                previousRight = before.rightEdge;

                // Colour comes from the earlier mark at the earlier end, so a mark that crosses onto
                // dirt blends from rubber to dust across the quad instead of switching abruptly.
                previousColour = before.colour;
            }

            _vertices[v + 0] = previousLeft;
            _vertices[v + 1] = previousRight;
            _vertices[v + 2] = mark.leftEdge;
            _vertices[v + 3] = mark.rightEdge;

            _normals[v + 0] = mark.normal;
            _normals[v + 1] = mark.normal;
            _normals[v + 2] = mark.normal;
            _normals[v + 3] = mark.normal;

            _colours[v + 0] = previousColour;
            _colours[v + 1] = previousColour;
            _colours[v + 2] = mark.colour;
            _colours[v + 3] = mark.colour;

            // U runs across the tyre, V runs along the mark. V is deliberately the SAME at both ends
            // of the quad, so every segment samples one horizontal line of the texture and the
            // ribbon tiles with no seam no matter how the segments are spaced.
            //
            // The old version used the full 0..1 square. With no texture assigned that looked like a
            // hard-edged strip, and with a texture assigned it stretched the whole image over every
            // single segment, which beads the trail instead of softening its edges.
            _uvs[v + 0] = new Vector2(markTextureURange.x, markTextureV);
            _uvs[v + 1] = new Vector2(markTextureURange.y, markTextureV);
            _uvs[v + 2] = new Vector2(markTextureURange.x, markTextureV);
            _uvs[v + 3] = new Vector2(markTextureURange.y, markTextureV);

            if (mark.previous >= 0)
            {
                _triangles[t + 0] = v + 0;
                _triangles[t + 1] = v + 2;
                _triangles[t + 2] = v + 1;
                _triangles[t + 3] = v + 2;
                _triangles[t + 4] = v + 3;
                _triangles[t + 5] = v + 1;
            }
            else
            {
                // Degenerate on purpose: valid indices, zero area, nothing drawn.
                for (int i = 0; i < 6; i++) _triangles[t + i] = v;
            }
        }

        /// <summary>
        /// Pushes the buffers to the mesh once per frame, and only when something actually changed.
        /// Uploading in LateUpdate rather than on every ReportWheel call means one upload per frame
        /// instead of four.
        /// </summary>
        private void LateUpdate()
        {
            if (!_dirty || _mesh == null) return;

            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.colors = _colours;
            _mesh.uv = _uvs;
            _mesh.triangles = _triangles;

            // Restated, not skipped. Assigning vertices or triangles makes Unity recompute the bounds
            // itself, so the large fixed volume set in Awake is gone by this point every frame. The
            // recomputed box spans the world origin, because most of the buffer is still zeroed, out
            // to the newest mark, which happens to contain everything and hides the problem. Setting
            // it back keeps culling independent of how much of the buffer has been used.
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            _dirty = false;
        }

        /// <summary>Wipes every mark. Called when a measured run restarts so one run cannot inherit
        /// the trails of the last one.</summary>
        public void Clear()
        {
            if (_marks == null) return;

            _writeIndex = 0;
            _markCount = 0;
            SegmentsLaid = 0;
            SegmentsDrawn = 0;

            for (int i = 0; i < MaxWheels; i++) _lastMarkPerWheel[i] = -1;

            // Collapsing every triangle is cheaper than clearing and rebuilding the mesh, and it
            // keeps the buffers at their allocated size so nothing is reallocated.
            for (int i = 0; i < _triangles.Length; i++) _triangles[i] = 0;
            for (int i = 0; i < _vertices.Length; i++) _vertices[i] = Vector3.zero;

            _dirty = true;
        }
    }
}
