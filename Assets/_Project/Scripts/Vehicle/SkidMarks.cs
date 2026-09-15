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
        [SerializeField] private float markWidth = 0.24f;

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

            GetComponent<MeshFilter>().sharedMesh = _mesh;

            MeshRenderer renderer = GetComponent<MeshRenderer>();

            // Saved asset first, then whatever is already on the renderer, then a generated fallback.
            if (markMaterialAsset != null) renderer.sharedMaterial = markMaterialAsset;
            else if (renderer.sharedMaterial == null) renderer.sharedMaterial = CreateMarkMaterial();

            // Marks lie on the floor and cannot meaningfully cast or receive shadows, and turning
            // both off avoids a shadow pass over a large always-visible mesh.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Vertex-coloured, alpha-blended, unlit. Vertex colour is the important part: it is how one
        /// mesh carries black rubber on tarmac and pale dust on dirt at the same time. The fallback
        /// chain covers a project where the URP particle shader is not present.
        /// </summary>
        private static Material CreateMarkMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");

            Material material = new Material(shader) { name = "Skid mark" };

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);   // transparent
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);       // alpha blend
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);         // double sided

            material.renderQueue = 3000;
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
            float opacity = Mathf.Clamp01(slip / Mathf.Max(0.01f, slipForFullOpacity))
                            * surface.markStrength;

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

            _uvs[v + 0] = new Vector2(0f, 0f);
            _uvs[v + 1] = new Vector2(1f, 0f);
            _uvs[v + 2] = new Vector2(0f, 1f);
            _uvs[v + 3] = new Vector2(1f, 1f);

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

            // Recalculating bounds would undo the large fixed bounds set in Awake, so it is not done.
            _dirty = false;
        }

        /// <summary>Wipes every mark. Called when a measured run restarts so one run cannot inherit
        /// the trails of the last one.</summary>
        public void Clear()
        {
            if (_marks == null) return;

            _writeIndex = 0;
            _markCount = 0;

            for (int i = 0; i < MaxWheels; i++) _lastMarkPerWheel[i] = -1;

            // Collapsing every triangle is cheaper than clearing and rebuilding the mesh, and it
            // keeps the buffers at their allocated size so nothing is reallocated.
            for (int i = 0; i < _triangles.Length; i++) _triangles[i] = 0;
            for (int i = 0; i < _vertices.Length; i++) _vertices[i] = Vector3.zero;

            _dirty = true;
        }
    }
}
