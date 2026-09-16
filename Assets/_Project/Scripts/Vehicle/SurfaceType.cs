using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// The kinds of ground the car can be driving on. Deliberately a short list: every extra
    /// surface is another thing the player has to learn to read at a glance, and a delivery route
    /// that needs six different traction rules memorised stops being readable.
    /// </summary>
    public enum SurfaceKind
    {
        Tarmac,
        Wet,
        Dirt,
        Grass,
        Ice,
        Metal
    }

    /// <summary>
    /// Marks a collider as a particular kind of ground, and carries everything the car needs to
    /// know about driving on it.
    ///
    /// WHY A COMPONENT AND NOT A PHYSIC MATERIAL
    /// Unity's PhysicMaterial friction is used by the PhysX solver when two colliders rub together.
    /// This car never rubs against the floor: it is held up by four raycasts and pushed around by
    /// accelerations we work out ourselves, so PhysX friction is not part of the handling at all.
    /// Unity's own documentation makes the same point about WheelColliders, which compute friction
    /// separately and ignore PhysicMaterial settings entirely. Setting a Dynamic Friction value on
    /// the floor would therefore change precisely nothing about how this car drives, which is worse
    /// than having no setting at all because it looks like it should work.
    ///
    /// So the surface carries its own numbers, and they are multipliers on the tuning values rather
    /// than replacements for them. That keeps one source of truth for "how grippy is this car" in
    /// VehicleTuning, and makes each surface a readable statement about how it differs from tarmac:
    /// ice is 0.18 of normal grip, and that reads immediately.
    ///
    /// HOW IT IS FOUND
    /// The wheel raycast already returns the collider it hit, so the lookup is free. Because it
    /// happens four times per physics step at 120 Hz, the result is cached per collider rather than
    /// calling GetComponent 480 times a second.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurfaceType : MonoBehaviour
    {
        [Tooltip("Which surface this collider is. Setting this in the inspector and pressing the " +
                 "Apply Defaults button below fills in sensible numbers for that kind.")]
        public SurfaceKind kind = SurfaceKind.Tarmac;

        [Header("Handling")]
        [Tooltip("Multiplies lateralGripAcceleration from the tuning profile. 1 = the car's normal " +
                 "sideways grip, which is what tarmac means. Below 1 the car slides sooner, and it " +
                 "slides by a calculable amount: grip becomes tuning value times this.")]
        [Range(0.05f, 2f)]
        public float gripMultiplier = 1f;

        [Tooltip("Multiplies forward acceleration. Loose ground spins the wheels rather than pushing " +
                 "the car, so dirt and grass pull away noticeably more slowly than tarmac.")]
        [Range(0.1f, 2f)]
        public float driveMultiplier = 1f;

        [Tooltip("Multiplies braking deceleration. This is the number that decides how far past the " +
                 "drop-off point the player slides when they brake late on a wet road.")]
        [Range(0.1f, 2f)]
        public float brakeMultiplier = 1f;

        [Tooltip("Extra deceleration on top of coasting, m/s^2. Long grass and deep dirt drag the " +
                 "car down even with the throttle held, which is what makes cutting a corner across " +
                 "the verge a real decision rather than a free shortcut.")]
        [Range(0f, 20f)]
        public float rollingResistance = 0f;

        [Header("Look")]
        [Tooltip("Colour of the mark the tyres leave. Black rubber on tarmac, pale dust on dirt, " +
                 "nothing much on ice.")]
        public Color markColour = new Color(0.06f, 0.06f, 0.07f, 0.75f);

        [Tooltip("How strongly this surface takes a tyre mark at all, 0 to 1. Tarmac holds rubber, " +
                 "grass barely shows anything, and ice takes nothing.")]
        [Range(0f, 1f)]
        public float markStrength = 1f;

        [Tooltip("Colour of the particles thrown up. Grey smoke off tarmac, brown dust off dirt, " +
                 "green clippings off grass, white spray off snow or ice.")]
        public Color particleColour = new Color(0.62f, 0.62f, 0.64f, 0.5f);

        [Tooltip("How much this surface throws up when the tyres slip, 0 to 1. Dirt and grass throw " +
                 "far more than tarmac does.")]
        [Range(0f, 3f)]
        public float particleAmount = 1f;

        [Tooltip("Loose surfaces spray material even when the car is simply driving fast in a " +
                 "straight line. Tarmac does not, so this is off for hard ground.")]
        public bool sprayWhenRolling = false;

        /// <summary>
        /// The numbers used when a collider has no SurfaceType on it at all. Plain tarmac, so an
        /// unlabelled floor behaves exactly as the car did before surfaces existed. That matters:
        /// every reading taken in the lab before this system was added stays valid.
        /// </summary>
        public static readonly SurfaceProfile Default = new SurfaceProfile
        {
            kind = SurfaceKind.Tarmac,
            gripMultiplier = 1f,
            driveMultiplier = 1f,
            brakeMultiplier = 1f,
            rollingResistance = 0f,
            markColour = new Color(0.06f, 0.06f, 0.07f, 0.75f),
            markStrength = 1f,
            particleColour = new Color(0.62f, 0.62f, 0.64f, 0.5f),
            particleAmount = 1f,
            sprayWhenRolling = false
        };

        /// <summary>Snapshot of this component's values, so the car reads a struct and not a reference.</summary>
        public SurfaceProfile Profile => new SurfaceProfile
        {
            kind = kind,
            gripMultiplier = gripMultiplier,
            driveMultiplier = driveMultiplier,
            brakeMultiplier = brakeMultiplier,
            rollingResistance = rollingResistance,
            markColour = markColour,
            markStrength = markStrength,
            particleColour = particleColour,
            particleAmount = particleAmount,
            sprayWhenRolling = sprayWhenRolling
        };

        /// <summary>
        /// Sensible starting numbers for each surface kind.
        ///
        /// The grip figures are the interesting ones, and they are chosen against the default tuning
        /// grip of 12 m/s^2 so that each surface says something specific about what the car can do.
        /// A corner of radius R at speed V needs V*V/R of sideways grip, so on ice at 0.18 the car
        /// has 2.16 m/s^2 and a 20 m corner can only be held at about 6.6 m/s. That is the sort of
        /// thing the player learns by driving it once, which is the whole point of making the
        /// surfaces differ by a lot rather than by a little.
        /// </summary>
        public static SurfaceProfile DefaultsFor(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Wet:
                    return new SurfaceProfile
                    {
                        kind = kind,
                        gripMultiplier = 0.6f,
                        driveMultiplier = 0.85f,
                        brakeMultiplier = 0.65f,
                        rollingResistance = 0f,
                        markColour = new Color(0.1f, 0.11f, 0.14f, 0.45f),
                        markStrength = 0.5f,
                        particleColour = new Color(0.75f, 0.8f, 0.85f, 0.4f),
                        particleAmount = 1.4f,
                        sprayWhenRolling = true
                    };

                case SurfaceKind.Dirt:
                    return new SurfaceProfile
                    {
                        kind = kind,
                        gripMultiplier = 0.55f,
                        driveMultiplier = 0.75f,
                        brakeMultiplier = 0.7f,
                        rollingResistance = 2.5f,
                        markColour = new Color(0.35f, 0.26f, 0.17f, 0.6f),
                        markStrength = 0.85f,
                        particleColour = new Color(0.55f, 0.42f, 0.28f, 0.55f),
                        particleAmount = 2.2f,
                        sprayWhenRolling = true
                    };

                case SurfaceKind.Grass:
                    return new SurfaceProfile
                    {
                        kind = kind,
                        gripMultiplier = 0.45f,
                        driveMultiplier = 0.6f,
                        brakeMultiplier = 0.6f,
                        rollingResistance = 5f,
                        markColour = new Color(0.16f, 0.22f, 0.11f, 0.5f),
                        markStrength = 0.35f,
                        particleColour = new Color(0.3f, 0.45f, 0.16f, 0.5f),
                        particleAmount = 1.8f,
                        sprayWhenRolling = true
                    };

                case SurfaceKind.Ice:
                    return new SurfaceProfile
                    {
                        kind = kind,
                        gripMultiplier = 0.18f,
                        driveMultiplier = 0.45f,
                        brakeMultiplier = 0.25f,
                        rollingResistance = 0f,
                        markColour = new Color(0.8f, 0.88f, 0.95f, 0.25f),
                        markStrength = 0.15f,
                        particleColour = new Color(0.9f, 0.95f, 1f, 0.35f),
                        particleAmount = 0.6f,
                        sprayWhenRolling = false
                    };

                case SurfaceKind.Metal:
                    return new SurfaceProfile
                    {
                        kind = kind,
                        gripMultiplier = 0.8f,
                        driveMultiplier = 0.95f,
                        brakeMultiplier = 0.85f,
                        rollingResistance = 0f,
                        markColour = new Color(0.12f, 0.12f, 0.13f, 0.4f),
                        markStrength = 0.4f,
                        particleColour = new Color(0.95f, 0.8f, 0.45f, 0.6f),
                        particleAmount = 1.2f,
                        sprayWhenRolling = false
                    };

                default:
                    return Default;
            }
        }

        /// <summary>Overwrites this component's numbers with the defaults for its kind.</summary>
        [ContextMenu("Apply defaults for this kind")]
        public void ApplyDefaultsForKind()
        {
            SurfaceProfile p = DefaultsFor(kind);
            gripMultiplier = p.gripMultiplier;
            driveMultiplier = p.driveMultiplier;
            brakeMultiplier = p.brakeMultiplier;
            rollingResistance = p.rollingResistance;
            markColour = p.markColour;
            markStrength = p.markStrength;
            particleColour = p.particleColour;
            particleAmount = p.particleAmount;
            sprayWhenRolling = p.sprayWhenRolling;
        }

        /// <summary>Convenience for the lab builder, which creates its surfaces in code.</summary>
        public static SurfaceType Attach(GameObject target, SurfaceKind kind)
        {
            SurfaceType surface = target.GetComponent<SurfaceType>();
            if (surface == null) surface = target.AddComponent<SurfaceType>();

            surface.kind = kind;
            surface.ApplyDefaultsForKind();
            return surface;
        }
    }

    /// <summary>
    /// A plain value copy of a surface's numbers.
    ///
    /// A struct rather than a reference to the component on purpose: the car blends the readings
    /// from four wheels that may be on four different surfaces, and blending needs values, not
    /// pointers to things that might be destroyed or edited mid-step.
    /// </summary>
    public struct SurfaceProfile
    {
        public SurfaceKind kind;
        public float gripMultiplier;
        public float driveMultiplier;
        public float brakeMultiplier;
        public float rollingResistance;
        public Color markColour;
        public float markStrength;
        public Color particleColour;
        public float particleAmount;
        public bool sprayWhenRolling;

        /// <summary>
        /// Weighted average of two surfaces, for a car straddling a boundary. Without this, a car
        /// with two wheels on tarmac and two on ice would snap between full grip and no grip as the
        /// average flipped, which reads as the physics being unreliable rather than as a transition.
        /// </summary>
        public static SurfaceProfile Lerp(SurfaceProfile a, SurfaceProfile b, float t)
        {
            return new SurfaceProfile
            {
                // The discrete fields cannot be averaged, so they take whichever side dominates.
                kind = t < 0.5f ? a.kind : b.kind,
                sprayWhenRolling = t < 0.5f ? a.sprayWhenRolling : b.sprayWhenRolling,

                gripMultiplier = Mathf.Lerp(a.gripMultiplier, b.gripMultiplier, t),
                driveMultiplier = Mathf.Lerp(a.driveMultiplier, b.driveMultiplier, t),
                brakeMultiplier = Mathf.Lerp(a.brakeMultiplier, b.brakeMultiplier, t),
                rollingResistance = Mathf.Lerp(a.rollingResistance, b.rollingResistance, t),
                markColour = Color.Lerp(a.markColour, b.markColour, t),
                markStrength = Mathf.Lerp(a.markStrength, b.markStrength, t),
                particleColour = Color.Lerp(a.particleColour, b.particleColour, t),
                particleAmount = Mathf.Lerp(a.particleAmount, b.particleAmount, t)
            };
        }
    }
}
