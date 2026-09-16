using System.Collections.Generic;
using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// Builds a stand-in car model out of Unity primitives and wires it up to VehicleVisuals.
    ///
    /// WHY THIS IS PRIMITIVES AND NOT A MODEL
    /// There is no car model in the project yet. Rather than leave a grey box, this assembles one out
    /// of boxes and cylinders. That is not a compromise for its own sake: the Rocket League silhouette
    /// is genuinely close to this already. Those cars are a low wide wedge with a canopy, a rear wing
    /// and four big exposed wheels, and the exposed wheels are most of why the movement reads so
    /// clearly. You can see the suspension working, and you can see the wheels lock when the car
    /// starts sliding. A smooth enclosed car body hides all of that.
    ///
    /// The proportions here are deliberately arcade rather than realistic:
    ///     wide track, so it looks planted and so the wheels sit outside the bodywork
    ///     low body, so the wheels look big relative to the car
    ///     short overhangs, so the car reads as agile
    ///     one bright body colour against one dark trim colour, so the shape is legible at speed
    ///
    /// WHEN THE REAL MODEL ARRIVES
    /// Untick Build On Awake, delete the generated child, drop the model in as a child of the car,
    /// then point the four pivot and spin transforms on VehicleVisuals at its wheels. Nothing in the
    /// physics changes and none of the measurements need retaking, because none of this touches the
    /// simulation. See the notes on VehicleVisuals.
    ///
    /// Built at runtime, the same way VehicleLabBuilder builds the lab, so the layout lives in
    /// readable code rather than in thousands of lines of unreviewable scene file. Use the Build
    /// Model Now item in the component's context menu to see it in the editor without pressing Play.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    [DisallowMultipleComponent]
    public class CarVisualBuilder : MonoBehaviour
    {
        private const string GeneratedName = "Model (generated)";

        [Header("Build")]
        [Tooltip("Build the model when the scene starts. Untick this once a real car model is in place.")]
        [SerializeField] private bool buildOnStart = true;

        [Header("Body proportions (metres)")]
        [Tooltip("Width of the main body. Kept narrower than the wheel track on purpose so the wheels " +
                 "stand clear of it, which is what makes the car read as an arcade car rather than a box.")]
        [SerializeField] private float bodyWidth = 1.30f;

        [Tooltip("Length of the main body.")]
        [SerializeField] private float bodyLength = 4.00f;

        [Tooltip("Height of the main body slab. Low, because low is what makes the wheels look big.")]
        [SerializeField] private float bodyHeight = 0.42f;

        [Tooltip("Height of the body's centre above the car origin. The car origin sits at the middle " +
                 "of the physics box, not on the ground.")]
        [SerializeField] private float bodyCentreHeight = 0f;

        [Header("Detail")]
        [Tooltip("Add the canopy, windscreen, wing, lights, skirts and bumpers. Untick for a plain " +
                 "slab, which is occasionally clearer when checking the suspension travel.")]
        [SerializeField] private bool buildDetail = true;

        [Tooltip("Rear wing height above the body top. Set to 0 for no wing.")]
        [SerializeField] private float wingHeight = 0.30f;

        // Collected while building, handed to VehicleVisuals at the end.
        // ------------------------------------------------------------------
        // SAVED MATERIALS
        // Empty means generate from the tuning colours, which keeps an unbaked scene working.
        // Run Tools > Zoom Zoom > Bake Lab Assets to create and assign real .mat files.
        // ------------------------------------------------------------------
        [Header("Materials (leave empty to generate at runtime)")]
        [SerializeField] private Material bodyMaterialAsset;
        [SerializeField] private Material trimMaterialAsset;
        [SerializeField] private Material glassMaterialAsset;
        [SerializeField] private Material tyreMaterialAsset;
        [SerializeField] private Material rimMaterialAsset;
        [SerializeField] private Material headlightMaterialAsset;
        [SerializeField] private Material brakeLightMaterialAsset;

        /// <summary>Headlight colour. Public so the asset baker writes the same value.</summary>
        public static readonly Color HeadlightColour = new Color(0.95f, 0.95f, 0.8f);

        private readonly List<Renderer> _brakeLightRenderers = new List<Renderer>();

        /// <summary>Saved asset if present, otherwise a generated throwaway.</summary>
        private Material Resolve(Material asset, string name, Color colour, float smoothness)
        {
            return asset != null ? asset : MakeMaterial(name, colour, smoothness);
        }

        private VehicleController _car;
        private Transform _generated;

        private VehicleTuning Tuning => _car != null ? _car.Tuning : null;

        private void Awake()
        {
            _car = GetComponent<VehicleController>();
        }

        /// <summary>
        /// Start rather than Awake, because building hands the wheel references over to
        /// VehicleVisuals and that component needs its own Awake to have run first. Start is
        /// guaranteed to be after every Awake, and still before the first frame is drawn, so there
        /// is no moment where the car is visible without its model.
        /// </summary>
        private void Start()
        {
            if (buildOnStart) Build();
        }

        // ==================================================================
        // BUILD
        // ==================================================================

        [ContextMenu("Build model now")]
        public void Build()
        {
            if (_car == null) _car = GetComponent<VehicleController>();

            if (Tuning == null)
            {
                Debug.LogWarning(
                    "[CarVisualBuilder] No tuning profile on the car, so the wheel size is unknown " +
                    "and the model cannot be built. Assign one first.", this);
                return;
            }

            Clear();

            VehicleTuning tuning = Tuning;

            _generated = new GameObject(GeneratedName).transform;
            _generated.SetParent(transform, false);
            _generated.localPosition = Vector3.zero;
            _generated.localRotation = Quaternion.identity;

            // Saved assets when assigned, generated from the tuning colours otherwise. Once the baker
            // has run, these are ordinary .mat files: Zubuhle can recolour the car without touching C#,
            // and the brake light material is a real asset the emission can be tuned on.
            Material body = Resolve(bodyMaterialAsset, "Car body", tuning.bodyColour, 0.35f);
            Material trim = Resolve(trimMaterialAsset, "Car trim", tuning.trimColour, 0.2f);
            Material glass = Resolve(glassMaterialAsset, "Car glass", tuning.glassColour, 0.85f);
            Material tyre = Resolve(tyreMaterialAsset, "Car tyre", tuning.tyreColour, 0.15f);
            Material rim = Resolve(rimMaterialAsset, "Car rim", tuning.rimColour, 0.6f);
            Material headlight = Resolve(headlightMaterialAsset, "Car headlight", HeadlightColour, 0.8f);
            Material brakeLight = Resolve(
                brakeLightMaterialAsset, "Car brake light", tuning.brakeLightOffColour, 0.5f);

            _brakeLightRenderers.Clear();

            BuildBody(body, trim, glass, headlight, brakeLight);
            WheelVisual[] wheels = BuildWheels(tuning, tyre, rim);

            // Hand the references over rather than making somebody drag eight transforms into the
            // inspector, which is eight chances to put a rear wheel in a front slot.
            VehicleVisuals visuals = GetComponent<VehicleVisuals>();
            if (visuals != null)
            {
                visuals.Bind(wheels, _brakeLightRenderers.ToArray());
            }
            else
            {
                Debug.LogWarning(
                    "[CarVisualBuilder] Built the model but there is no VehicleVisuals component on " +
                    "the car, so the wheels will not turn. Add one.", this);
            }

            Debug.Log(
                $"[CarVisualBuilder] Model built. Body {bodyWidth:0.00} x {bodyLength:0.00} m, " +
                $"wheels {tuning.wheelRadius * 2f:0.00} m across, " +
                $"track {(tuning.wheelSideOffset * 2f) + tuning.wheelWidth:0.00} m.");
        }

        [ContextMenu("Clear model")]
        public void Clear()
        {
            Transform existing = transform.Find(GeneratedName);
            while (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);

                existing = transform.Find(GeneratedName);
            }

            _generated = null;
        }

        // ==================================================================
        // BODY
        // ==================================================================

        private void BuildBody(Material body, Material trim, Material glass,
            Material headlight, Material brakeLight)
        {
            float halfLength = bodyLength * 0.5f;
            float bodyTop = bodyCentreHeight + bodyHeight * 0.5f;
            float bodyBottom = bodyCentreHeight - bodyHeight * 0.5f;
            float halfWidth = bodyWidth * 0.5f;

            // The main slab. Everything else is measured off this.
            Box("Chassis", new Vector3(0f, bodyCentreHeight, 0f),
                new Vector3(bodyWidth, bodyHeight, bodyLength), body);

            if (!buildDetail) return;

            // Nose. Tilted down at the front so the car has a wedge profile instead of reading as a
            // brick, and so there is an obvious front end when the car is spinning.
            Box("Nose", new Vector3(0f, bodyCentreHeight - 0.02f, halfLength - 0.25f),
                new Vector3(bodyWidth - 0.15f, bodyHeight * 0.62f, 0.9f), body,
                Quaternion.Euler(-7f, 0f, 0f));

            // Canopy, set back towards the rear like a mid engined car. Narrower than the body so
            // there is a shoulder line, which is most of what makes a shape look designed.
            Box("Canopy", new Vector3(0f, bodyTop + 0.19f, -0.10f),
                new Vector3(bodyWidth - 0.28f, 0.38f, 1.75f), body);

            // Windscreen, raked back.
            Box("Windscreen", new Vector3(0f, bodyTop + 0.15f, 0.92f),
                new Vector3(bodyWidth - 0.32f, 0.40f, 0.10f), glass,
                Quaternion.Euler(-38f, 0f, 0f));

            // Side windows.
            Box("Window left", new Vector3(-(halfWidth - 0.13f), bodyTop + 0.20f, -0.10f),
                new Vector3(0.06f, 0.24f, 1.5f), glass);
            Box("Window right", new Vector3(halfWidth - 0.13f, bodyTop + 0.20f, -0.10f),
                new Vector3(0.06f, 0.24f, 1.5f), glass);

            // Rear window.
            Box("Rear window", new Vector3(0f, bodyTop + 0.16f, -0.97f),
                new Vector3(bodyWidth - 0.32f, 0.34f, 0.10f), glass,
                Quaternion.Euler(30f, 0f, 0f));

            // Side skirts, filling the gap between front and rear wheels so the car does not look
            // like it is floating.
            Box("Skirt left", new Vector3(-(halfWidth + 0.03f), bodyBottom - 0.05f, 0f),
                new Vector3(0.10f, 0.16f, bodyLength * 0.55f), trim);
            Box("Skirt right", new Vector3(halfWidth + 0.03f, bodyBottom - 0.05f, 0f),
                new Vector3(0.10f, 0.16f, bodyLength * 0.55f), trim);

            // Front splitter and rear diffuser. Dark, low and wide: they visually anchor the car to
            // the road, which is worth more than it sounds when the car is airborne a lot.
            Box("Front splitter", new Vector3(0f, bodyBottom - 0.04f, halfLength + 0.02f),
                new Vector3(bodyWidth + 0.06f, 0.10f, 0.34f), trim);
            Box("Rear diffuser", new Vector3(0f, bodyBottom - 0.04f, -(halfLength + 0.02f)),
                new Vector3(bodyWidth + 0.06f, 0.10f, 0.34f), trim);

            // Rear wing on two struts. Pure Rocket League, and it doubles as an unmistakable
            // "this end is the back" marker while the car is tumbling.
            if (wingHeight > 0.01f)
            {
                float wingZ = -(halfLength - 0.1f);
                float wingY = bodyTop + wingHeight;

                Box("Wing blade", new Vector3(0f, wingY, wingZ),
                    new Vector3(bodyWidth + 0.12f, 0.07f, 0.34f), trim);
                Box("Wing strut left", new Vector3(-(halfWidth - 0.22f), wingY - wingHeight * 0.5f, wingZ),
                    new Vector3(0.07f, wingHeight, 0.22f), trim);
                Box("Wing strut right", new Vector3(halfWidth - 0.22f, wingY - wingHeight * 0.5f, wingZ),
                    new Vector3(0.07f, wingHeight, 0.22f), trim);
            }

            // Headlights.
            Box("Headlight left", new Vector3(-(halfWidth - 0.28f), bodyCentreHeight + 0.02f, halfLength + 0.13f),
                new Vector3(0.34f, 0.13f, 0.08f), headlight);
            Box("Headlight right", new Vector3(halfWidth - 0.28f, bodyCentreHeight + 0.02f, halfLength + 0.13f),
                new Vector3(0.34f, 0.13f, 0.08f), headlight);

            // Brake lights. Collected so VehicleVisuals can light them up.
            GameObject brakeLeft = Box("Brake light left",
                new Vector3(-(halfWidth - 0.26f), bodyCentreHeight + 0.06f, -(halfLength + 0.03f)),
                new Vector3(0.36f, 0.13f, 0.08f), brakeLight);
            GameObject brakeRight = Box("Brake light right",
                new Vector3(halfWidth - 0.26f, bodyCentreHeight + 0.06f, -(halfLength + 0.03f)),
                new Vector3(0.36f, 0.13f, 0.08f), brakeLight);

            _brakeLightRenderers.Add(brakeLeft.GetComponent<Renderer>());
            _brakeLightRenderers.Add(brakeRight.GetComponent<Renderer>());
        }

        // ==================================================================
        // WHEELS
        // ==================================================================

        /// <summary>
        /// Four wheels, each as a pivot with a spinner inside it. The wheel positions come straight
        /// from the tuning asset, the same numbers the suspension raycasts use, so the model can never
        /// disagree with the physics about where the wheels are.
        /// </summary>
        private WheelVisual[] BuildWheels(VehicleTuning tuning, Material tyre, Material rim)
        {
            var result = new WheelVisual[VehicleController.WheelCount];

            for (int i = 0; i < VehicleController.WheelCount; i++)
            {
                bool isFront = i < 2;
                bool isLeft = (i % 2) == 0;

                string name = $"Wheel {(isFront ? "front" : "rear")} {(isLeft ? "left" : "right")}";

                var pivot = new GameObject(name).transform;
                pivot.SetParent(_generated, false);

                // Sits at the wheel centre, which is one radius above the contact patch.
                Vector3 origin = new Vector3(
                    isLeft ? -tuning.wheelSideOffset : tuning.wheelSideOffset,
                    tuning.wheelHeightOffset,
                    isFront ? tuning.wheelForwardOffset : -tuning.wheelForwardOffset);

                pivot.localPosition = new Vector3(
                    origin.x,
                    origin.y - (tuning.suspensionRestLength - tuning.wheelRadius),
                    origin.z);

                var spin = new GameObject("Spin").transform;
                spin.SetParent(pivot, false);

                BuildWheelModel(spin, tuning, tyre, rim);

                result[i] = new WheelVisual { pivot = pivot, spin = spin, steers = isFront };
            }

            return result;
        }

        /// <summary>
        /// The wheel itself. Unity's cylinder is 2 units tall and 1 across, standing on its end, so
        /// it gets laid over onto its side and scaled to the tyre size.
        ///
        /// The hub bar matters more than it looks. A plain cylinder spinning about its own axis is
        /// visually identical at every angle, so without something off-axis to catch the eye the
        /// wheels appear completely stationary no matter how fast they are turning.
        /// </summary>
        private void BuildWheelModel(Transform spin, VehicleTuning tuning, Material tyre, Material rim)
        {
            float diameter = tuning.wheelRadius * 2f;

            // Lay the cylinder on its side so its axis runs left to right along the spinner's X,
            // which is the axle the spinner rotates about.
            Quaternion onItsSide = Quaternion.Euler(0f, 0f, 90f);

            GameObject tyreObject = Cylinder("Tyre", Vector3.zero,
                new Vector3(diameter, tuning.wheelWidth * 0.5f, diameter), tyre, onItsSide);
            tyreObject.transform.SetParent(spin, false);

            // Hub, very slightly wider than the tyre so it reads as a separate part.
            GameObject hub = Cylinder("Hub", Vector3.zero,
                new Vector3(diameter * 0.55f, tuning.wheelWidth * 0.54f, diameter * 0.55f),
                rim, onItsSide);
            hub.transform.SetParent(spin, false);

            // The bar across the hub. This is the part that makes rotation visible.
            GameObject bar = Box("Hub bar", Vector3.zero,
                new Vector3(tuning.wheelWidth * 1.06f, diameter * 0.84f, diameter * 0.13f), rim);
            bar.transform.SetParent(spin, false);
        }

        // ==================================================================
        // PRIMITIVE HELPERS
        // ==================================================================

        private GameObject Box(string name, Vector3 localPosition, Vector3 size, Material material)
        {
            return Box(name, localPosition, size, material, Quaternion.identity);
        }

        private GameObject Box(string name, Vector3 localPosition, Vector3 size, Material material,
            Quaternion localRotation)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;

            // No colliders on any of this. The car has exactly one BoxCollider on its root, so there
            // is one place to look when collisions behave oddly, and the visual detail can be
            // reshaped freely without changing how the car hits things.
            StripCollider(box);

            box.transform.SetParent(_generated, false);
            box.transform.localPosition = localPosition;
            box.transform.localRotation = localRotation;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            return box;
        }

        private GameObject Cylinder(string name, Vector3 localPosition, Vector3 size,
            Material material, Quaternion localRotation)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            StripCollider(cylinder);

            cylinder.transform.SetParent(_generated, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localRotation = localRotation;
            cylinder.transform.localScale = size;
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = material;

            return cylinder;
        }

        private static void StripCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider == null) return;

            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        /// <summary>
        /// Flat coloured material for whichever render pipeline is active. Made in code so the model
        /// carries no material assets around with it, and so the colours can live in the tuning
        /// profile with every other number.
        /// </summary>
        private static Material MakeMaterial(string name, Color colour, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var material = new Material(shader) { name = name };
            material.color = colour;

            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);

            return material;
        }
    }
}
