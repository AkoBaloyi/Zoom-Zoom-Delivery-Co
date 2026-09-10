using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// Builds the VehicleLab test environment: a long flat floor, distance markers every 5 units,
    /// one solid wall, and four cones set out as a corner.
    ///
    /// WHY THIS IS GENERATED IN CODE
    /// The lab is a measuring instrument, not a level. Seventy-odd distance markers placed by hand
    /// would be seventy chances to nudge one and quietly ruin a reading. Generated from numbers, the
    /// layout is exact, it is identical every time the scene is opened, and the whole thing is
    /// described in about thirty readable lines rather than three thousand lines of scene file.
    ///
    /// Nothing here is meant to look nice. It is a ruler.
    ///
    /// PHYSICS NOTES THAT MATTER FOR THE READINGS
    ///  - The markers and cones have their colliders removed. A marker you can clip is a marker that
    ///    can corrupt a measurement, and the whole point of them is to be a neutral reference.
    ///  - The markers sit well outside the driving lane for the same reason.
    ///  - The wall is the ONE thing in here that is solid, because hitting it is a test.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleLabBuilder : MonoBehaviour
    {
        private const string GeneratedContainerName = "Generated (do not edit by hand)";

        [Header("Reference points")]
        [Tooltip("Where the car starts and where every test puts it back to.")]
        [SerializeField] private Transform startPoint;

        [Tooltip("Open space for the turning circle test.")]
        [SerializeField] private Transform turnTestPoint;

        [Tooltip("The wall. This transform gets the wall's mesh and collider built onto it.")]
        [SerializeField] private Transform wallAnchor;

        [Header("Floor")]
        [Tooltip("Length of the floor along Z, metres. Needs to be long enough to reach top speed, " +
                 "hit the wall, and still have room to stop.")]
        [SerializeField] private float groundLength = 600f;

        [Tooltip("Width of the floor along X, metres. Wide, because a turning circle at high speed " +
                 "needs a lot of room: radius = speed squared over grip.")]
        [SerializeField] private float groundWidth = 520f;

        [Tooltip("How far back from the start line the floor extends, metres.")]
        [SerializeField] private float groundBehindStart = 40f;

        [Header("Distance markers")]
        [Tooltip("Spacing of the small markers, metres. 5 is the unit the level design is quoted in.")]
        [SerializeField] private float markerSpacing = 5f;

        [Tooltip("Every Nth marker is taller and gets a number. 5 x 5 m = a label every 25 m.")]
        [SerializeField] private int majorMarkerEvery = 5;

        [Tooltip("How far to either side of the centre line the markers stand, metres. Outside the " +
                 "driving lane on purpose.")]
        [SerializeField] private float markerSideOffset = 12f;

        [Header("Wall")]
        [Tooltip("Distance from the start line to the wall, metres.")]
        [SerializeField] private float wallDistance = 200f;

        [Tooltip("Wall size: width, height, thickness in metres.")]
        [SerializeField] private Vector3 wallSize = new Vector3(30f, 6f, 2f);

        [Header("Cone corner")]
        [Tooltip("Distance from the start line to where the cone corner begins, metres.")]
        [SerializeField] private float coneCornerDistance = 130f;

        [Tooltip("Radius of the 90 degree corner the four cones trace out, metres. Compare this " +
                 "against the turning circle reading from F2 to see what speed it can be taken at: " +
                 "a corner of radius R can be held at speeds up to the square root of R times grip.")]
        [SerializeField] private float coneCornerRadius = 12f;

        [Header("Turn test area")]
        [Tooltip("Where the turning circle test starts. Kept off to one side so a wide circle at high " +
                 "test speed still fits on the floor.")]
        [SerializeField] private Vector3 turnTestPosition = new Vector3(-70f, 0.8f, 80f);

        [Header("Surface patches")]
        [Tooltip("Lay out the surface test strips. Each is a wide patch of one surface type with its " +
                 "own grip, running parallel to the main tarmac lane so the same corner can be tried " +
                 "on each one and the readings compared directly.")]
        [SerializeField] private bool buildSurfacePatches = true;

        [Tooltip("Width of one surface strip, metres. Wide enough that all four wheels are on the " +
                 "same surface, which matters because the car averages grip across its wheels and a " +
                 "narrow strip would only ever give a half-and-half reading.")]
        [SerializeField] private float patchWidth = 30f;

        [Tooltip("Length of a surface strip, metres. Long enough to reach a useful speed, slide, and " +
                 "still pull up before running out of it.")]
        [SerializeField] private float patchLength = 260f;

        [Tooltip("Distance from the start line to where the strips begin, metres. Placed past the " +
                 "cone corner so the tarmac tests are not driven across.")]
        [SerializeField] private float patchStartDistance = 40f;

        [Tooltip("Gap between neighbouring strips, metres. A visible gap of plain tarmac between " +
                 "strips means a run onto grass starts from a known grip rather than from whatever " +
                 "the previous strip was.")]
        [SerializeField] private float patchGap = 10f;

        [Tooltip("Which surfaces get a strip, in left to right order across the floor. The main lane " +
                 "down the middle stays tarmac, so tarmac does not need to be in this list.")]
        [SerializeField]
        private SurfaceKind[] patchKinds =
        {
            SurfaceKind.Wet,
            SurfaceKind.Dirt,
            SurfaceKind.Grass,
            SurfaceKind.Ice,
            SurfaceKind.Metal
        };

        // ------------------------------------------------------------------
        // SAVED MATERIALS
        //
        // These used to be made with `new Material(...)` every time the scene started, which had three
        // costs worth being rid of: a fresh instance leaked on every run, nobody could art-pass the lab
        // without editing C#, and the scene looked empty until Play was pressed.
        //
        // Assigned, they are ordinary assets anyone can select and edit. Left empty, the builder falls
        // back to making them in code so an unbaked scene still works rather than rendering magenta.
        // Run Tools > Zoom Zoom > Bake Lab Assets to create and assign them.
        // ------------------------------------------------------------------
        [Header("Materials (leave empty to generate at runtime)")]
        [SerializeField] private Material floorMaterialAsset;
        [SerializeField] private Material minorMarkerMaterialAsset;
        [SerializeField] private Material majorMarkerMaterialAsset;
        [SerializeField] private Material wallMaterialAsset;
        [SerializeField] private Material coneMaterialAsset;
        [SerializeField] private Material startLineMaterialAsset;

        [Tooltip("One per entry in Patch Kinds, in the same order. Left empty or short, the missing " +
                 "ones are generated at runtime from the surface's colour.")]
        [SerializeField] private Material[] surfaceMaterialAssets = new Material[0];

        [Header("Build")]
        [Tooltip("Build the lab when the scene starts.\n\n" +
                 "Turn this OFF once the lab has been baked into the scene with " +
                 "Tools > Zoom Zoom > Bake Lab Into Scene, otherwise the saved geometry is thrown away " +
                 "and regenerated on every Play, which defeats the point of baking it.")]
        [SerializeField] private bool buildOnAwake = true;

        // ---------------- what other scripts can read ----------------

        public Transform StartPoint => startPoint;
        public Transform TurnTestPoint => turnTestPoint;
        public Transform WallAnchor => wallAnchor;

        /// <summary>
        /// The fastest a car with this much grip could hold the cone corner, m/s. Useful sanity
        /// check: a corner of radius R needs v*v/R of grip, so v = sqrt(R * grip).
        /// </summary>
        public float ConeCornerSpeedLimit(float lateralGripAcceleration)
        {
            return Mathf.Sqrt(Mathf.Max(0f, coneCornerRadius * lateralGripAcceleration));
        }

        private Transform _generated;

        private void Awake()
        {
            if (buildOnAwake) Build();
        }

        // ==================================================================
        // BUILD
        // ==================================================================

        [ContextMenu("Build lab now")]
        public void Build()
        {
            Clear();

            _generated = new GameObject(GeneratedContainerName).transform;
            _generated.SetParent(transform, false);

            PlaceReferencePoints();

            // Saved asset if one is assigned, generated only as a fallback. The colours stay here as
            // the defaults the baker writes into the assets, so there is still one place that says
            // what the lab is supposed to look like.
            Material floorMaterial = Resolve(floorMaterialAsset, FloorColour);
            Material minorMarkerMaterial = Resolve(minorMarkerMaterialAsset, MinorMarkerColour);
            Material majorMarkerMaterial = Resolve(majorMarkerMaterialAsset, MajorMarkerColour);
            Material wallMaterial = Resolve(wallMaterialAsset, WallColour);
            Material coneMaterial = Resolve(coneMaterialAsset, ConeColour);
            Material startMaterial = Resolve(startLineMaterialAsset, StartLineColour);

            BuildFloor(floorMaterial);
            BuildStartLine(startMaterial);
            BuildDistanceMarkers(minorMarkerMaterial, majorMarkerMaterial);
            BuildWall(wallMaterial);
            BuildConeCorner(coneMaterial);
            if (buildSurfacePatches) BuildSurfacePatches();

            if (buildSurfacePatches && patchKinds != null && patchKinds.Length > 0)
            {
                Debug.Log(
                    $"[VehicleLab] {patchKinds.Length} surface strips laid out to the right of the " +
                    $"centre lane, each {patchWidth:0} x {patchLength:0} m starting at " +
                    $"{patchStartDistance:0} m. Drive the same corner on each and compare: grip is " +
                    "the tuning value times the strip's multiplier, printed on its label post.");
            }

            Debug.Log(
                $"[VehicleLab] Lab built. Floor {groundWidth:0} x {groundLength:0} m, markers every " +
                $"{markerSpacing:0} m, wall at {wallDistance:0} m, cone corner radius " +
                $"{coneCornerRadius:0} m starting at {coneCornerDistance:0} m.");
        }

        [ContextMenu("Clear lab")]
        public void Clear()
        {
            Transform existing = transform.Find(GeneratedContainerName);
            while (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);

                existing = transform.Find(GeneratedContainerName);
            }

            _generated = null;
        }

        /// <summary>
        /// Moves the reference points to match the layout numbers, so changing a number here cannot
        /// leave a test aiming at the wrong place.
        /// </summary>
        private void PlaceReferencePoints()
        {
            if (startPoint != null)
            {
                startPoint.SetPositionAndRotation(
                    new Vector3(0f, 0.8f, 0f),
                    Quaternion.LookRotation(Vector3.forward, Vector3.up));
            }

            if (turnTestPoint != null)
            {
                turnTestPoint.SetPositionAndRotation(
                    turnTestPosition,
                    Quaternion.LookRotation(Vector3.forward, Vector3.up));
            }

            if (wallAnchor != null)
            {
                wallAnchor.SetPositionAndRotation(
                    new Vector3(0f, 0f, wallDistance),
                    Quaternion.identity);
            }
        }

        private void BuildFloor(Material material)
        {
            // A thick box rather than a Plane: a Plane has a paper-thin collider that a car at
            // 25 m/s can punch through, and a floor that occasionally lets the car fall out of the
            // world is not much use as a measuring instrument.
            float centreZ = (groundLength * 0.5f) - groundBehindStart;

            GameObject floor = MakeBox(
                "Floor",
                new Vector3(0f, -1f, centreZ),
                new Vector3(groundWidth, 2f, groundLength),
                material,
                keepCollider: true);

            floor.transform.SetParent(_generated, true);
        }

        private void BuildStartLine(Material material)
        {
            GameObject line = MakeBox(
                "Start line",
                new Vector3(0f, 0.01f, 0f),
                new Vector3(markerSideOffset * 2f, 0.02f, 0.4f),
                material,
                keepCollider: false);

            line.transform.SetParent(_generated, true);
        }

        /// <summary>
        /// Posts up both sides of the floor every markerSpacing metres, with a taller post and a
        /// printed number every majorMarkerEvery of them. This is how a reading like "stopped in
        /// 14.3 m" gets checked by eye rather than taken on trust.
        /// </summary>
        private void BuildDistanceMarkers(Material minor, Material major)
        {
            if (markerSpacing <= 0.01f) return;

            var markers = new GameObject("Distance markers").transform;
            markers.SetParent(_generated, false);

            float end = groundLength - groundBehindStart - markerSpacing;
            int index = 0;

            for (float z = 0f; z <= end; z += markerSpacing, index++)
            {
                bool isMajor = majorMarkerEvery > 0 && index % majorMarkerEvery == 0;

                Vector3 size = isMajor
                    ? new Vector3(0.3f, 2.2f, 0.3f)
                    : new Vector3(0.15f, 0.7f, 0.15f);

                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject post = MakeBox(
                        $"{z:0} m {(side < 0 ? "left" : "right")}",
                        new Vector3(markerSideOffset * side, size.y * 0.5f, z),
                        size,
                        isMajor ? major : minor,
                        keepCollider: false);

                    post.transform.SetParent(markers, true);
                }

                if (isMajor) MakeDistanceLabel(markers, z);
            }
        }

        private void BuildWall(Material material)
        {
            GameObject wall = MakeBox(
                "Wall",
                new Vector3(0f, wallSize.y * 0.5f, wallDistance),
                wallSize,
                material,
                keepCollider: true);

            // Parent to the anchor if there is one, so the measurement script's wall reference and
            // the actual lump of geometry can never drift apart.
            wall.transform.SetParent(wallAnchor != null ? wallAnchor : _generated, true);
        }

        /// <summary>
        /// Four cones tracing a 90 degree corner of a known radius, so the measured turning circle
        /// can be compared against a corner of the kind the city will actually have.
        ///
        /// The arc runs from heading straight down the floor to heading across it. Cones sit at
        /// 180, 150, 120 and 90 degrees around a circle whose centre is one radius to the right.
        /// </summary>
        private void BuildConeCorner(Material material)
        {
            var corner = new GameObject($"Cone corner (radius {coneCornerRadius:0} m)").transform;
            corner.SetParent(_generated, false);

            Vector3 centre = new Vector3(coneCornerRadius, 0f, coneCornerDistance);
            float[] anglesDegrees = { 180f, 150f, 120f, 90f };

            for (int i = 0; i < anglesDegrees.Length; i++)
            {
                float radians = anglesDegrees[i] * Mathf.Deg2Rad;
                Vector3 position = centre + new Vector3(
                    Mathf.Cos(radians) * coneCornerRadius,
                    0.5f,
                    Mathf.Sin(radians) * coneCornerRadius);

                GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cone.name = $"Cone {i + 1}";
                cone.transform.SetPositionAndRotation(position, Quaternion.identity);

                // Unity's cylinder is 2 units tall at scale 1, so 0.5 gives a 1 m cone.
                cone.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                cone.GetComponent<MeshRenderer>().sharedMaterial = material;

                StripCollider(cone);
                cone.transform.SetParent(corner, true);
            }
        }

        // ==================================================================
        // SURFACE PATCHES
        // ==================================================================

        /// <summary>
        /// Lays one strip per surface kind, side by side and all starting from the same line.
        ///
        /// WHY STRIPS RATHER THAN A PATCHWORK
        /// The point of the lab is to be able to compare. Parallel strips of the same length starting
        /// at the same distance mean the same test can be driven on ice and on dirt and the two
        /// readings differ only by the surface. A scattered patchwork would look more like a level but
        /// would make every reading depend on where exactly the car happened to be.
        ///
        /// WHY EACH STRIP IS WIDER THAN THE CAR
        /// The car averages grip across whichever wheels are touching ground. A strip narrower than
        /// the track width could never give a clean reading, because two wheels would always be on
        /// tarmac. patchWidth is checked against that at build time.
        ///
        /// The strips sit slightly proud of the main floor and keep their colliders, so the wheel
        /// raycasts hit the strip rather than the tarmac underneath it.
        /// </summary>
        private void BuildSurfacePatches()
        {
            if (patchKinds == null || patchKinds.Length == 0) return;

            var patches = new GameObject("Surface patches").transform;
            patches.SetParent(_generated, false);

            // Laid out to the RIGHT of the centre lane, so the turning circle area off to the left at
            // x -70 stays clear tarmac and the F2 reading is unaffected by any of this.
            float x = markerSideOffset + patchGap + (patchWidth * 0.5f);

            for (int i = 0; i < patchKinds.Length; i++)
            {
                SurfaceKind kind = patchKinds[i];
                SurfaceProfile profile = SurfaceType.DefaultsFor(kind);

                var strip = new GameObject($"{kind} (grip x{profile.gripMultiplier:0.00})");
                strip.transform.SetParent(patches, false);

                // Proud of the floor by 2 cm. The floor is a 2 m thick box centred 1 m below zero, so
                // its top face is at y 0; putting the strip's top face just above that means the wheel
                // ray hits the strip first without the car visibly climbing a step.
                // Saved asset for this strip if the baker has made one, otherwise generated.
                Material stripMaterial = Resolve(
                    i < surfaceMaterialAssets.Length ? surfaceMaterialAssets[i] : null,
                    ColourFor(kind));

                GameObject slab = MakeBox(
                    "Surface",
                    new Vector3(x, -0.09f, patchStartDistance + (patchLength * 0.5f)),
                    new Vector3(patchWidth, 0.2f, patchLength),
                    stripMaterial,
                    keepCollider: true);

                slab.transform.SetParent(strip.transform, true);

                // On the PARENT, not the slab. The controller resolves surfaces with
                // GetComponentInParent, so labelling the parent covers this slab and anything else
                // added to the strip later, such as a ramp or a kerb.
                SurfaceType.Attach(strip, kind);

                // A post at the near end so the driver can tell which strip is which from the seat.
                GameObject label = MakeBox(
                    "Marker post",
                    new Vector3(x, 1.4f, patchStartDistance - 1.5f),
                    new Vector3(0.35f, 2.8f, 0.35f),
                    stripMaterial,
                    keepCollider: false);

                label.transform.SetParent(strip.transform, true);

                MakeSurfaceLabel(strip.transform, kind, profile, x);

                x += patchWidth + patchGap;
            }

            float rightEdge = x - patchGap - (patchWidth * 0.5f);
            float halfFloor = groundWidth * 0.5f;

            // A strip hanging off the edge of the floor would drop the car into nothing, and finding
            // that out by driving off is a poor use of anyone's afternoon.
            if (rightEdge > halfFloor)
            {
                Debug.LogWarning(
                    $"[VehicleLab] The surface strips reach x {rightEdge:0} m but the floor only goes " +
                    $"to {halfFloor:0} m. Widen groundWidth, narrow patchWidth, or drop a surface " +
                    "from the list, or the outermost strip will hang over the edge.", this);
            }

            // The track width check: a strip narrower than the car cannot give a clean reading.
            float trackWidth = 1.6f;
            if (patchWidth < trackWidth * 2f)
            {
                Debug.LogWarning(
                    $"[VehicleLab] patchWidth is {patchWidth:0.0} m, which is tight against a car " +
                    $"about {trackWidth:0.0} m across. Readings will be contaminated by wheels " +
                    "hanging onto the tarmac either side. 10 m or more is comfortable.", this);
            }
        }

        /// <summary>
        /// A readable colour per surface. These are flat greybox colours chosen to be told apart at
        /// speed rather than to look like the real material.
        /// </summary>
        private static Color ColourFor(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Wet: return new Color(0.18f, 0.28f, 0.42f);
                case SurfaceKind.Dirt: return new Color(0.42f, 0.31f, 0.19f);
                case SurfaceKind.Grass: return new Color(0.24f, 0.42f, 0.18f);
                case SurfaceKind.Ice: return new Color(0.74f, 0.85f, 0.92f);
                case SurfaceKind.Metal: return new Color(0.52f, 0.54f, 0.58f);
                default: return new Color(0.32f, 0.34f, 0.36f);
            }
        }

        /// <summary>
        /// Prints the surface name and its grip multiplier beside the strip, facing back towards the
        /// start line. The number is on the floor for the same reason the distance markers are: a
        /// reading you can check by eye beats one you have to take on trust.
        /// </summary>
        private void MakeSurfaceLabel(Transform parent, SurfaceKind kind, SurfaceProfile profile, float x)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;

            var label = new GameObject($"Label {kind}");
            label.transform.SetPositionAndRotation(
                new Vector3(x, 3.4f, patchStartDistance - 1.5f),
                Quaternion.LookRotation(Vector3.back, Vector3.up));

            TextMesh text = label.AddComponent<TextMesh>();
            text.text = $"{kind}\nx{profile.gripMultiplier:0.00}";
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.5f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;

            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.transform.SetParent(parent, true);
        }

        // ==================================================================
        // PRIMITIVE HELPERS
        // ==================================================================

        private static GameObject MakeBox(string name, Vector3 position, Vector3 size,
            Material material, bool keepCollider)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetPositionAndRotation(position, Quaternion.identity);
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            if (!keepCollider) StripCollider(box);

            return box;
        }

        private static void StripCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider == null) return;

            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        /// <summary>
        /// A printed distance number beside the major markers. Uses the legacy TextMesh rather than
        /// TextMeshPro so the lab has no package dependency of its own.
        /// </summary>
        private void MakeDistanceLabel(Transform parent, float distance)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;

            var label = new GameObject($"Label {distance:0} m");
            label.transform.SetPositionAndRotation(
                new Vector3(markerSideOffset + 1.2f, 2.6f, distance),
                // Faces back down the floor, so it can be read from the driver's seat.
                Quaternion.LookRotation(Vector3.back, Vector3.up));

            TextMesh text = label.AddComponent<TextMesh>();
            text.text = $"{distance:0}";
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.35f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;

            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.transform.SetParent(parent, true);
        }

        /// <summary>
        /// Flat coloured material for the current render pipeline. Made in code because the lab does
        /// not need material assets cluttering up the project, and because nothing in here is
        /// supposed to look like anything.
        /// </summary>
        // The lab's palette, in one place. The baker reads these when it writes the material assets, so
        // the colours and the assets cannot drift apart silently.
        public static readonly Color FloorColour = new Color(0.32f, 0.34f, 0.36f);
        public static readonly Color MinorMarkerColour = new Color(0.85f, 0.85f, 0.85f);
        public static readonly Color MajorMarkerColour = new Color(0.15f, 0.55f, 0.95f);
        public static readonly Color WallColour = new Color(0.75f, 0.2f, 0.2f);
        public static readonly Color ConeColour = new Color(1f, 0.45f, 0.05f);
        public static readonly Color StartLineColour = new Color(0.2f, 0.85f, 0.35f);

        /// <summary>Colour for a surface strip. Public so the asset baker writes the same values.</summary>
        public static Color SurfaceColour(SurfaceKind kind) => ColourFor(kind);

        /// <summary>Which surfaces the lab is currently set up to lay out.</summary>
        public SurfaceKind[] PatchKinds => patchKinds;

        /// <summary>
        /// Use the saved asset when there is one, otherwise make a throwaway. Keeping the fallback means
        /// a scene that has never been baked still renders correctly instead of turning magenta, which
        /// matters because a teammate pulling this branch has not run the baker yet.
        /// </summary>
        private static Material Resolve(Material asset, Color fallbackColour)
        {
            return asset != null ? asset : MakeMaterial(fallbackColour);
        }

        private static Material MakeMaterial(Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material material = shader != null
                ? new Material(shader)
                : new Material(Shader.Find("Sprites/Default"));

            material.name = $"Lab {colour}";
            material.color = colour;

            // Matte, so the shapes read clearly and nothing is lost in a highlight.
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.05f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

            return material;
        }

        // ==================================================================
        // EDITOR PREVIEW
        // The layout is visible in the scene view before pressing play, without any of the
        // generated objects being saved into the scene file.
        // ==================================================================

        private void OnDrawGizmos()
        {
            float centreZ = (groundLength * 0.5f) - groundBehindStart;

            Gizmos.color = new Color(0.4f, 0.4f, 0.45f, 0.5f);
            Gizmos.DrawWireCube(
                new Vector3(0f, 0f, centreZ),
                new Vector3(groundWidth, 0.01f, groundLength));

            // The wall.
            Gizmos.color = new Color(0.9f, 0.25f, 0.25f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(0f, wallSize.y * 0.5f, wallDistance), wallSize);

            // The cone corner arc.
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
            Vector3 centre = new Vector3(coneCornerRadius, 0f, coneCornerDistance);
            Vector3 previous = Vector3.zero;

            for (int i = 0; i <= 20; i++)
            {
                float angle = Mathf.Lerp(180f, 90f, i / 20f) * Mathf.Deg2Rad;
                Vector3 point = centre + new Vector3(
                    Mathf.Cos(angle) * coneCornerRadius, 0.2f,
                    Mathf.Sin(angle) * coneCornerRadius);

                if (i > 0) Gizmos.DrawLine(previous, point);
                previous = point;
            }

            // Start line and turn test area.
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Gizmos.DrawLine(new Vector3(-markerSideOffset, 0.2f, 0f),
                new Vector3(markerSideOffset, 0.2f, 0f));

            Gizmos.color = new Color(0.9f, 0.9f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(turnTestPosition, 1.5f);
        }
    }
}
