using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// Drives everything the tyres throw out: the marks on the ground and the stuff in the air.
    ///
    /// WHY THIS IS A SEPARATE COMPONENT, LIKE VehicleVisuals
    /// It only reads. Nothing here can change how the car drives, so it can be switched off entirely
    /// and every reading taken in the lab stays valid. That separation is worth keeping strictly:
    /// effects are the part most likely to be rewritten when the art arrives, and none of that work
    /// should be able to disturb tuned physics.
    ///
    /// WHAT IT READS
    /// Per wheel, the controller already publishes the contact point, the ground normal at that
    /// point, the sideways slide at that point, and which surface is underneath. That is the complete
    /// set needed, so this component computes almost nothing itself and instead decides what each
    /// reading should look like.
    ///
    /// TWO SEPARATE REASONS TO EMIT, WHICH IS THE PART WORTH GETTING RIGHT
    ///  1. SLIP. The tyre is sliding across the ground, so it smokes and it leaves a mark. Happens on
    ///     every surface, and how much depends on how hard the slide is.
    ///  2. ROLLING SPRAY. The tyre is gripping perfectly but the ground is loose, so it throws dust
    ///     or clippings just from driving over it. Happens only on surfaces flagged sprayWhenRolling,
    ///     and it is what stops dirt and grass feeling like differently coloured tarmac.
    ///
    /// Collapsing those two into one "emit when fast" rule is the usual mistake. It gives tarmac
    /// permanent smoke at speed, and gives dirt nothing at all when the car drives straight over it.
    ///
    /// Runs in Update rather than FixedUpdate because it is drawing, not simulating.
    /// </summary>
    [DisallowMultipleComponent]
    public class TyreEffects : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The car being read. Found on this object if left empty.")]
        [SerializeField] private VehicleController car;

        [Tooltip("Where tyre marks are drawn. One of these is shared by every car in the scene, so " +
                 "leave it empty and it will find the one in the scene, or create one.")]
        [SerializeField] private SkidMarks skidMarks;

        [Tooltip("One particle system per wheel, in the controller's order: front left, front right, " +
                 "rear left, rear right. Leave empty and they are built in code at startup.")]
        [SerializeField] private ParticleSystem[] wheelParticles = new ParticleSystem[0];

        [Header("Behaviour")]
        [Tooltip("Draw marks on the ground.")]
        [SerializeField] private bool drawMarks = true;

        [Tooltip("Throw particles from the tyres.")]
        [SerializeField] private bool emitParticles = true;

        [Tooltip("Sideways slide, m/s, at which particle output is at its fullest. Matches the skid " +
                 "mark opacity ramp so the smoke and the mark tell the player the same story.")]
        [SerializeField] private float slipForFullEmission = 6f;

        [Tooltip("Slide below this, m/s, throws nothing from a hard surface. Keep it above the " +
                 "wobble the car has when driving dead straight, or tarmac smokes constantly.")]
        [SerializeField] private float minimumSlipToEmit = 1.2f;

        [Tooltip("Particles per second at full slip, per wheel. The surface's own particleAmount " +
                 "multiplies this, so dirt at 2.2 throws more than twice what tarmac at 1.0 does.")]
        [SerializeField] private float particlesPerSecondAtFullSlip = 45f;

        [Tooltip("Speed, m/s, at which a loose surface sprays its most from simply being driven over. " +
                 "Only applies to surfaces flagged sprayWhenRolling.")]
        [SerializeField] private float speedForFullRollingSpray = 18f;

        [Tooltip("Particles per second per wheel from rolling over loose ground at that speed. Kept " +
                 "well below the slip rate: driving through dirt should look busy, not like a bonfire.")]
        [SerializeField] private float rollingSprayParticlesPerSecond = 14f;

        // ---------------- state ----------------

        private ParticleSystem.EmissionModule[] _emission;
        private ParticleSystem.MainModule[] _main;
        private bool _built;

        private void Awake()
        {
            if (car == null) car = GetComponent<VehicleController>();

            if (car == null)
            {
                Debug.LogError(
                    "[TyreEffects] No VehicleController found. Tyre effects need a car to read, so " +
                    "this component has switched itself off rather than throwing every frame.", this);
                enabled = false;
                return;
            }

            EnsureSkidMarks();
            EnsureParticles();
            _built = true;
        }

        /// <summary>
        /// Marks are shared: every car in the scene draws into one mesh, because the mesh is a fixed
        /// budget of quads and giving each car its own would multiply that budget by the number of
        /// cars for no visual gain.
        /// </summary>
        private void EnsureSkidMarks()
        {
            if (!drawMarks || skidMarks != null) return;

#if UNITY_2023_1_OR_NEWER
            skidMarks = Object.FindFirstObjectByType<SkidMarks>();
#else
            skidMarks = Object.FindObjectOfType<SkidMarks>();
#endif

            if (skidMarks != null) return;

            // Deliberately NOT a child of the car. The marks stay on the road after the car has
            // driven away, so parenting them to a moving object would drag them along with it.
            var holder = new GameObject("Skid marks");
            skidMarks = holder.AddComponent<SkidMarks>();
        }

        /// <summary>
        /// Builds one particle system per wheel if none were wired up by hand.
        ///
        /// Made in code for the same reason the lab is: the emitters are defined by about a dozen
        /// numbers, and a dozen readable numbers in a file are easier to review and harder to break
        /// than four prefabs that have to be kept in step by hand.
        /// </summary>
        private void EnsureParticles()
        {
            if (!emitParticles) return;

            // Qualified by type, not by instance: WheelCount is a const, and C# rejects reaching a
            // const through an object reference.
            int wheelCount = VehicleController.WheelCount;

            if (wheelParticles == null || wheelParticles.Length < wheelCount)
            {
                var rebuilt = new ParticleSystem[wheelCount];

                for (int i = 0; i < wheelCount; i++)
                {
                    if (wheelParticles != null && i < wheelParticles.Length && wheelParticles[i] != null)
                    {
                        rebuilt[i] = wheelParticles[i];
                        continue;
                    }

                    rebuilt[i] = CreateWheelParticleSystem(i);
                }

                wheelParticles = rebuilt;
            }

            _emission = new ParticleSystem.EmissionModule[wheelCount];
            _main = new ParticleSystem.MainModule[wheelCount];

            for (int i = 0; i < wheelCount; i++)
            {
                if (wheelParticles[i] == null) continue;

                _emission[i] = wheelParticles[i].emission;
                _main[i] = wheelParticles[i].main;

                // Emission is driven entirely from script, so it starts at zero rather than at
                // whatever the default is. Without this every wheel puffs once on spawn.
                ParticleSystem.EmissionModule emission = _emission[i];
                emission.rateOverTime = 0f;
            }
        }

        private ParticleSystem CreateWheelParticleSystem(int index)
        {
            var holder = new GameObject($"Tyre particles {index}");

            // Parented to the car so it follows, but positioned each frame at the contact patch.
            holder.transform.SetParent(transform, false);

            ParticleSystem particles = holder.AddComponent<ParticleSystem>();

            // Has to be stopped before the modules are configured, or Unity emits with the defaults
            // for a frame first.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startColor = new Color(0.6f, 0.6f, 0.62f, 0.45f);
            main.gravityModifier = -0.06f;  // drifts up, like dust and smoke do
            main.maxParticles = 220;

            // World space, so a puff stays where it was thrown instead of being dragged along by the
            // car. This is the single most important setting here: in local space the smoke follows
            // the car around and instantly reads as fake.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            // Spreading out and fading as it disperses is what separates smoke from a cloud of balls.
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 1.6f));

            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);

            // Air resistance, so a puff slows rather than sailing away at its launch speed.
            ParticleSystem.LimitVelocityOverLifetimeModule limit = particles.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.35f;
            limit.limit = new ParticleSystem.MinMaxCurve(2.5f);

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateParticleMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Sorted so a puff behind another puff does not pop in front of it.
            renderer.sortMode = ParticleSystemSortMode.Distance;

            particles.Play();
            return particles;
        }

        /// <summary>
        /// Additive-free, alpha-blended, vertex-coloured particle material. Vertex colour matters for
        /// the same reason it does on the marks: it is how one shared material shows grey smoke off
        /// tarmac and brown dust off dirt without needing a material per surface.
        /// </summary>
        private static Material CreateParticleMaterial()
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");

            Material material = new Material(shader) { name = "Tyre particles" };

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

            material.renderQueue = 3000;
            return material;
        }

        // ==================================================================
        // PER FRAME
        // ==================================================================

        private void Update()
        {
            if (!_built || car == null) return;

            int wheelCount = VehicleController.WheelCount;
            Vector3 travel = car.Body != null ? car.Body.linearVelocity : Vector3.zero;
            float speed = travel.magnitude;

            for (int i = 0; i < wheelCount; i++)
            {
                WheelVisualState wheel = car.GetWheelVisualState(i);
                SurfaceProfile surface = car.SurfaceUnderWheel(i);

                if (!wheel.grounded)
                {
                    SetEmission(i, 0f);

                    // An airborne wheel must break its ribbon, or the mark stretches from take-off
                    // straight to landing in one long quad across the gap.
                    if (drawMarks && skidMarks != null)
                    {
                        skidMarks.ReportWheel(i, wheel.contactPoint, wheel.contactNormal,
                            Vector3.zero, 0f, surface);
                    }
                    continue;
                }

                float slip = Mathf.Abs(wheel.lateralSlipSpeed);

                // Direction of travel flattened onto the ground this wheel is touching, which is what
                // the mark needs to know to work out its own width direction.
                Vector3 forward = Vector3.ProjectOnPlane(travel, wheel.contactNormal);
                if (forward.sqrMagnitude < 0.0001f) forward = car.GroundForward;

                if (drawMarks && skidMarks != null)
                {
                    skidMarks.ReportWheel(i, wheel.contactPoint, wheel.contactNormal,
                        forward.normalized, slip, surface);
                }

                if (!emitParticles) continue;

                MoveEmitter(i, wheel);
                SetParticleColour(i, surface.particleColour);
                SetEmission(i, EmissionRateFor(slip, speed, surface));
            }
        }

        /// <summary>
        /// How much this wheel should be throwing, from the two independent causes.
        ///
        /// The larger of the two wins rather than the sum: a car sliding sideways through dirt is
        /// already at full output from the slide, and adding the rolling spray on top would double it
        /// for no visible benefit beyond a solid wall of particles.
        /// </summary>
        private float EmissionRateFor(float slip, float speed, SurfaceProfile surface)
        {
            float fromSlip = 0f;
            if (slip >= minimumSlipToEmit)
            {
                float slip01 = Mathf.Clamp01(slip / Mathf.Max(0.01f, slipForFullEmission));
                fromSlip = slip01 * particlesPerSecondAtFullSlip * surface.particleAmount;
            }

            float fromRolling = 0f;
            if (surface.sprayWhenRolling)
            {
                float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, speedForFullRollingSpray));
                fromRolling = speed01 * rollingSprayParticlesPerSecond * surface.particleAmount;
            }

            return Mathf.Max(fromSlip, fromRolling);
        }

        /// <summary>
        /// Puts the emitter at the contact patch and aims it away from the ground, so particles are
        /// thrown off the surface rather than through it.
        /// </summary>
        private void MoveEmitter(int index, WheelVisualState wheel)
        {
            if (wheelParticles == null || index >= wheelParticles.Length) return;

            ParticleSystem particles = wheelParticles[index];
            if (particles == null) return;

            Transform emitter = particles.transform;
            emitter.position = wheel.contactPoint + wheel.contactNormal * 0.06f;
            emitter.rotation = Quaternion.LookRotation(wheel.contactNormal);
        }

        private void SetEmission(int index, float rate)
        {
            if (_emission == null || index >= _emission.Length) return;

            ParticleSystem.EmissionModule emission = _emission[index];
            emission.rateOverTime = rate;
        }

        private void SetParticleColour(int index, Color colour)
        {
            if (_main == null || index >= _main.Length) return;

            ParticleSystem.MainModule main = _main[index];
            main.startColor = colour;
        }

        /// <summary>Clears the marks. Called between measured runs so trails cannot carry over.</summary>
        public void ClearMarks()
        {
            if (skidMarks != null) skidMarks.Clear();
        }
    }
}
