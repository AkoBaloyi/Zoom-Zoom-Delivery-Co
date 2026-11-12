using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// The two delivery van bodies: a white Transit-style panel van in the company colours, and a
    /// round-roofed bakery van with a loaf on the roof.
    ///
    /// WHAT STAYS THE SAME
    /// Both vans sit on the car's own four wheels, placed from the same tuning numbers the
    /// suspension raycasts use. Nothing in this file touches the BoxCollider, the Rigidbody or the
    /// tuning asset, so a van drives exactly like the car and no measurement needs retaking.
    ///
    /// The cost of that is the roof. The hit box is 1 m tall and a van is not, so everything above
    /// it has no collision: the roof can pass through a low branch, and a van lying on its roof
    /// sinks into the road. Giving the van a taller box would fix that, but a taller box changes
    /// how the car tips and turns, which makes it a handling change for the group to decide.
    ///
    /// HOW BIG
    /// The footprint matches the hit box, 1.8 m by 4.2 m. The height is held to about 1.8 m above
    /// the road, the size of a small van, because the chase camera looks over the roof at the road
    /// 7 m ahead. A full height van roof would cover the road the player is steering for. Even at
    /// 1.8 m the roof hid about 12 m of road in front of the nose from the old 2.1 m camera, which
    /// is why the camera was raised to 2.6 m (Tuning_Balanced), where it hides about 7 m.
    ///
    /// COORDINATES
    /// Car space in metres: x to the right, y up, z forwards. The car's origin is not on the road,
    /// so heights are written as g + h, meaning h metres above the road.
    /// </summary>
    public partial class CarVisualBuilder
    {
        [System.Serializable]
        public class TransitPaint
        {
            [Tooltip("Main paint. White, because the white panel van is the one everybody recognises.")]
            public Color body = new Color(0.94f, 0.94f, 0.92f, 1f);

            [Tooltip("Company colour for the side stripes and the ZZ badges. The car's yellow, so the " +
                     "van and the car read as the same firm.")]
            public Color livery = new Color(0.93f, 0.72f, 0.09f, 1f);
        }

        [System.Serializable]
        public class BakeryPaint
        {
            [Tooltip("Upper body, roof and the stripes on the awning.")]
            public Color body = new Color(0.96f, 0.90f, 0.76f, 1f);

            [Tooltip("Lower half and the nose. The same brown IngredientIcons uses for bakery items.")]
            public Color lower = new Color(0.82f, 0.60f, 0.28f, 1f);

            [Tooltip("Pinstripe, door shut lines, wheel arch lips and the roof sign.")]
            public Color trim = new Color(0.40f, 0.24f, 0.12f, 1f);

            [Tooltip("The loaf on the roof.")]
            public Color loaf = new Color(0.80f, 0.52f, 0.22f, 1f);

            [Tooltip("The coloured stripes on the awning over the serving hatch.")]
            public Color awning = new Color(0.80f, 0.22f, 0.22f, 1f);
        }

        [Header("Van paint (only used when Body Style is a van)")]
        [SerializeField] private TransitPaint transitPaint = new TransitPaint();
        [SerializeField] private BakeryPaint bakeryPaint = new BakeryPaint();

        private static readonly Color IndicatorColour = new Color(1f, 0.55f, 0.08f, 1f);
        private static readonly Color PlateColour = new Color(0.93f, 0.93f, 0.90f, 1f);

        private static string VanLabel(BodyStyle style)
        {
            switch (style)
            {
                case BodyStyle.TransitVan: return "Transit van";
                case BodyStyle.BakeryVan: return "Bakery van";
                default: return "Car";
            }
        }

        // ==================================================================
        // SHARED MEASUREMENTS
        // ==================================================================

        /// <summary>The measurements both vans are built around, all taken from the tuning.</summary>
        private struct VanFrame
        {
            public float halfWidth;         // body side
            public float chassisHalfWidth;  // dark block between the wheels
            public float ground;            // the road, with the suspension at rest
            public float bodyBottom;
            public float wheelCentreY;
            public float wheelRadius;
            public float frontAxle;
            public float rearAxle;
            public float archTop;           // top of the wheel openings
        }

        private static VanFrame MakeVanFrame(VehicleTuning tuning)
        {
            float tyreInner = tuning.wheelSideOffset - tuning.wheelWidth * 0.5f;
            float tyreOuter = tuning.wheelSideOffset + tuning.wheelWidth * 0.5f;

            var frame = new VanFrame
            {
                // 6 cm inside the outer face of the tyres, so the wheels stand just proud of the
                // bodywork the way they do on the car.
                halfWidth = tyreOuter - 0.06f,

                // Narrow enough that a front wheel at full lock does not swing into it.
                chassisHalfWidth = Mathf.Max(0.2f, tyreInner - 0.16f),

                ground = tuning.wheelHeightOffset - tuning.suspensionRestLength,
                wheelRadius = tuning.wheelRadius,
                frontAxle = tuning.wheelForwardOffset,
                rearAxle = -tuning.wheelForwardOffset,
            };

            frame.bodyBottom = frame.ground + 0.26f;
            frame.wheelCentreY = frame.ground + tuning.wheelRadius;

            // 14 cm of clearance over the tyre, so the suspension has room to push the wheels up.
            frame.archTop = frame.wheelCentreY + tuning.wheelRadius + 0.14f;

            return frame;
        }

        /// <summary>
        /// Everything below the wheel arches: full-width panels in front of, between and behind the
        /// wheels, and a dark chassis block through the openings, so the gap behind each tyre reads
        /// as a wheel well instead of a hole through the van.
        /// </summary>
        private void BuildVanUnderbody(VanFrame f, float rear, float front, float archHalf,
            Material panel, Material chassis)
        {
            float hw = f.halfWidth;
            float rearArchBack = f.rearAxle - archHalf;
            float frontArchFront = f.frontAxle + archHalf;

            if (rearArchBack - rear > 0.01f)
                Span("Lower body rear", -hw, hw, f.bodyBottom, f.archTop, rear, rearArchBack, panel);

            Span("Lower body middle", -hw, hw, f.bodyBottom, f.archTop,
                f.rearAxle + archHalf, f.frontAxle - archHalf, panel);

            if (front - frontArchFront > 0.01f)
                Span("Lower body front", -hw, hw, f.bodyBottom, f.archTop, frontArchFront, front, panel);

            Span("Chassis", -f.chassisHalfWidth, f.chassisHalfWidth, f.bodyBottom + 0.04f,
                f.archTop + 0.01f, rear + 0.02f, front - 0.02f, chassis);
        }

        // ==================================================================
        // TRANSIT VAN
        // ==================================================================

        /// <summary>
        /// A white panel van with a short sloping bonnet, a raked windscreen and a flat high roof.
        /// The details that make it read as a Transit rather than a box: the sliding side door and its
        /// rail, the tall tail lights up the rear corners, twin rear doors, the big mirrors and the
        /// dark plastic bumpers and arches. The yellow stripe and ZZ badges are the company livery.
        /// </summary>
        private void BuildTransitVan(VehicleTuning tuning, Material trim, Material glass,
            Material headlight, Material brakeLight, Material chrome)
        {
            VanFrame f = MakeVanFrame(tuning);
            float g = f.ground;
            float hw = f.halfWidth;

            Material paint = Generated("Transit paint", transitPaint.body, 0.45f);
            Material livery = Generated("Transit livery", transitPaint.livery, 0.4f);
            Material amber = Generated("Van indicator", IndicatorColour, 0.6f);
            Material plate = Generated("Van number plate", PlateColour, 0.3f);

            // Stations along the van.
            const float rear = -2.06f;            // rear door face
            const float slidingDoorBack = -0.62f;
            const float bPillar = 0.50f;          // back of the cab
            const float screenTopZ = 0.98f;       // windscreen meets the roof
            const float screenBaseZ = 1.46f;      // windscreen meets the bonnet
            const float nose = 2.02f;             // front panel; the bumper stands proud of it

            float roof = g + 1.80f;
            float screenTop = g + 1.72f;
            float belt = g + 1.30f;               // bottom of the cab glass
            float bonnetFront = g + 1.10f;
            float archHalf = f.wheelRadius + 0.08f;

            // ---- shell ----
            BuildVanUnderbody(f, rear, nose, archHalf, paint, trim);
            Span("Cargo box", -hw, hw, f.archTop, roof, rear, bPillar, paint);
            Span("Cab", -hw, hw, f.archTop, roof, bPillar, screenTopZ, paint);
            Span("Cab lower", -hw, hw, f.archTop, belt, screenTopZ, screenBaseZ, paint);
            Span("Engine bay", -hw, hw, f.archTop, bonnetFront, screenBaseZ, nose, paint);
            SideSlab("Bonnet", 0f, 2f * hw - 0.04f, new Vector2(screenBaseZ, belt),
                new Vector2(nose, bonnetFront), 0.30f, paint);

            // The windscreen is a solid block of glass filling the wedge under it. Its sides are what
            // show through as the front, slanted part of each door window, so the door glass follows
            // the windscreen pillar without needing any shape a box cannot make.
            SideSlab("Windscreen", 0f, 2f * hw - 0.02f, new Vector2(screenTopZ, screenTop),
                new Vector2(screenBaseZ, belt), 0.40f, glass);
            SpanPair("Door window", hw - 0.004f, hw + 0.004f, belt, screenTop, bPillar + 0.06f,
                screenTopZ, glass);

            for (int side = -1; side <= 1; side += 2)
            {
                SideSlab($"Windscreen pillar {SideName(side)}", side * (hw - 0.035f), 0.07f,
                    new Vector2(screenTopZ, screenTop), new Vector2(screenBaseZ, belt), 0.07f, paint,
                    lift: 0.012f, trimEnds: -0.02f);
            }

            for (int i = -1; i <= 1; i++)
            {
                Span($"Roof rib {i + 2}", i * 0.46f - 0.03f, i * 0.46f + 0.03f, roof - 0.01f,
                    roof + 0.018f, rear + 0.12f, bPillar - 0.10f, paint);
            }

            // ---- livery ----
            float stripeLow = g + 0.90f, stripeHigh = g + 1.04f;
            float pinLow = g + 1.07f, pinHigh = g + 1.10f;
            SpanPair("Livery stripe", hw - 0.004f, hw + 0.004f, stripeLow, stripeHigh, rear + 0.01f,
                nose - 0.18f, livery);
            SpanPair("Pinstripe", hw - 0.004f, hw + 0.004f, pinLow, pinHigh, rear + 0.01f,
                nose - 0.18f, trim);
            Span("Livery stripe rear", -hw + 0.14f, hw - 0.14f, stripeLow, stripeHigh, rear - 0.004f,
                rear + 0.004f, livery);
            Span("Pinstripe rear", -hw + 0.14f, hw - 0.14f, pinLow, pinHigh, rear - 0.004f,
                rear + 0.004f, trim);

            ZzBadge("Badge right", new Vector3(hw, g + 1.48f, -1.25f), Vector3.forward, Vector3.right,
                0.44f, livery, trim);
            ZzBadge("Badge left", new Vector3(-hw, g + 1.48f, -1.25f), Vector3.back, Vector3.left,
                0.44f, livery, trim);
            ZzBadge("Badge rear", new Vector3(0f, g + 1.42f, rear), Vector3.right, Vector3.back,
                0.44f, livery, trim);

            // ---- doors ----
            float seamLow = f.bodyBottom + 0.06f;
            float seamTop = screenTop + 0.02f;
            SpanPair("Cab door shut line", hw - 0.004f, hw + 0.007f, seamLow, seamTop,
                bPillar - 0.008f, bPillar + 0.008f, trim);
            SpanPair("Front door shut line", hw - 0.004f, hw + 0.007f, f.archTop + 0.06f, belt - 0.02f,
                screenBaseZ + 0.01f, screenBaseZ + 0.026f, trim);
            SpanPair("Cab door handle", hw, hw + 0.022f, g + 1.18f, g + 1.22f, bPillar + 0.10f,
                bPillar + 0.24f, trim);

            // Sliding door on the left, the kerb side when driving on the left.
            Span("Sliding door shut line back", -hw - 0.007f, -hw + 0.004f, seamLow, seamTop,
                slidingDoorBack - 0.008f, slidingDoorBack + 0.008f, trim);
            Span("Sliding door shut line top", -hw - 0.007f, -hw + 0.004f, seamTop - 0.016f, seamTop,
                slidingDoorBack, bPillar, trim);
            Span("Sliding door rail", -hw - 0.016f, -hw + 0.004f, g + 1.16f, g + 1.195f, rear + 0.06f,
                slidingDoorBack, trim);
            Span("Sliding door handle", -hw - 0.022f, -hw, g + 1.18f, g + 1.22f, bPillar - 0.20f,
                bPillar - 0.08f, trim);

            SpanPair("Sill", hw - 0.01f, hw + 0.012f, f.bodyBottom, f.bodyBottom + 0.10f,
                f.rearAxle + archHalf, f.frontAxle - archHalf, trim);
            TrapezoidArches(f, archHalf, trim);

            // ---- front ----
            Span("Front bumper", -hw - 0.01f, hw + 0.01f, g + 0.24f, g + 0.64f, nose - 0.08f,
                nose + 0.10f, trim);
            Span("Grille", -0.45f, 0.45f, g + 0.70f, g + 1.04f, nose - 0.01f, nose + 0.012f, trim);
            for (int i = 0; i < 2; i++)
            {
                Span($"Grille bar {i + 1}", -0.42f, 0.42f, g + 0.79f + i * 0.12f,
                    g + 0.815f + i * 0.12f, nose + 0.012f, nose + 0.022f, chrome);
            }

            SpanPair("Headlight", 0.50f, hw - 0.04f, g + 0.90f, g + 1.06f, nose - 0.01f, nose + 0.02f,
                headlight);
            SpanPair("Headlight wrap", hw - 0.01f, hw + 0.006f, g + 0.92f, g + 1.04f, nose - 0.16f,
                nose - 0.005f, headlight);
            SpanPair("Front indicator", 0.56f, hw - 0.08f, g + 0.80f, g + 0.86f, nose - 0.01f,
                nose + 0.016f, amber);
            Span("Front plate", -0.26f, 0.26f, g + 0.40f, g + 0.52f, nose + 0.10f, nose + 0.108f, plate);

            SpanPair("Mirror arm", hw, hw + 0.10f, g + 1.38f, g + 1.42f, 1.26f, 1.31f, trim);
            SpanPair("Mirror", hw + 0.08f, hw + 0.14f, g + 1.28f, g + 1.54f, 1.21f, 1.35f, trim);

            // ---- rear ----
            Span("Rear bumper", -hw - 0.005f, hw + 0.005f, g + 0.24f, g + 0.48f, rear - 0.07f,
                rear + 0.12f, trim);
            Span("Rear step", -0.35f, 0.35f, g + 0.24f, g + 0.28f, rear - 0.14f, rear - 0.06f, trim);
            Span("Rear plate", -0.26f, 0.26f, g + 0.30f, g + 0.42f, rear - 0.078f, rear - 0.07f, plate);

            float doorTop = g + 1.70f;
            Span("Rear door shut line", -0.008f, 0.008f, g + 0.50f, doorTop, rear - 0.007f,
                rear + 0.004f, trim);
            Span("Rear door frame top", -hw + 0.15f, hw - 0.15f, doorTop - 0.016f, doorTop,
                rear - 0.007f, rear + 0.004f, trim);
            SpanPair("Rear door frame side", hw - 0.166f, hw - 0.15f, g + 0.50f, doorTop,
                rear - 0.007f, rear + 0.004f, trim);
            Span("Rear door handle", 0.03f, 0.13f, g + 1.18f, g + 1.22f, rear - 0.02f, rear, trim);

            // Tall tail lights up the rear corners, the most recognisable thing about the back of a
            // Transit, and the part the player looks at most from the chase camera.
            AddBrakeLight(Span("Brake light right", hw - 0.13f, hw - 0.01f, g + 0.70f, g + 1.26f,
                rear - 0.04f, rear + 0.01f, brakeLight));
            AddBrakeLight(Span("Brake light left", -hw + 0.01f, -hw + 0.13f, g + 0.70f, g + 1.26f,
                rear - 0.04f, rear + 0.01f, brakeLight));
            AddBrakeLight(Span("High brake light", -0.18f, 0.18f, g + 1.73f, g + 1.77f, rear - 0.012f,
                rear + 0.004f, brakeLight));
            SpanPair("Rear indicator", hw - 0.13f, hw - 0.01f, g + 0.58f, g + 0.68f, rear - 0.03f,
                rear + 0.01f, amber);
        }

        /// <summary>Dark plastic wheel arches: a flat top, two sloped shoulders and two feet.</summary>
        private void TrapezoidArches(VanFrame f, float archHalf, Material material)
        {
            float x0 = f.halfWidth - 0.02f;
            float x1 = f.halfWidth + 0.04f;
            float top = f.archTop + 0.02f;
            float knee = f.archTop - 0.18f;
            const float flat = 0.24f;
            const float thickness = 0.06f;
            const float overlap = thickness * 0.5f;

            foreach (float axle in new[] { f.frontAxle, f.rearAxle })
            {
                string n = axle > 0f ? "front" : "rear";
                float ahead = axle + archHalf + 0.02f;
                float behind = axle - archHalf - 0.02f;

                StrutPair($"Arch {n} top", x0, x1, new Vector2(axle - flat, top),
                    new Vector2(axle + flat, top), thickness, material, overlap);
                StrutPair($"Arch {n} shoulder ahead", x0, x1, new Vector2(axle + flat, top),
                    new Vector2(axle + archHalf, knee), thickness, material, overlap);
                StrutPair($"Arch {n} shoulder behind", x0, x1, new Vector2(axle - flat, top),
                    new Vector2(axle - archHalf, knee), thickness, material, overlap);
                StrutPair($"Arch {n} foot ahead", x0, x1, new Vector2(ahead, knee),
                    new Vector2(ahead, f.bodyBottom + 0.04f), thickness, material, overlap);
                StrutPair($"Arch {n} foot behind", x0, x1, new Vector2(behind, knee),
                    new Vector2(behind, f.bodyBottom + 0.04f), thickness, material, overlap);
            }
        }

        // ==================================================================
        // BAKERY VAN
        // ==================================================================

        /// <summary>
        /// An old-fashioned bread van: cream over bakery brown, a round roof, a round nose with round
        /// headlights and chrome bumpers, ribbed sides, whitewall tyres, a serving hatch under a
        /// striped awning on the kerb side, and a loaf of bread on the roof. The loaf sits towards the
        /// back of the roof so it stays below the chase camera's line of sight.
        /// </summary>
        private void BuildBakeryVan(VehicleTuning tuning, Material trim, Material glass,
            Material headlight, Material brakeLight, Material chrome, out Material whitewall)
        {
            VanFrame f = MakeVanFrame(tuning);
            float g = f.ground;
            float hw = f.halfWidth;

            Material cream = Generated("Bakery cream", bakeryPaint.body, 0.4f);
            Material brown = Generated("Bakery brown", bakeryPaint.lower, 0.35f);
            Material chocolate = Generated("Bakery chocolate", bakeryPaint.trim, 0.3f);
            Material loaf = Generated("Bakery loaf", bakeryPaint.loaf, 0.25f);
            Material awning = Generated("Bakery awning", bakeryPaint.awning, 0.3f);
            Material amber = Generated("Van indicator", IndicatorColour, 0.6f);
            Material plate = Generated("Van number plate", PlateColour, 0.3f);
            whitewall = cream;

            const float rear = -2.02f;
            const float cabBack = 0.80f;
            const float screenTopZ = 1.55f;
            const float screenBaseZ = 1.70f;
            const float noseZ = 1.92f;            // axis of the rounded nose
            const float hatchBack = -1.30f;
            const float hatchFront = 0.10f;

            float split = g + 1.00f;              // brown below, cream above
            float shoulder = g + 1.52f;           // top of the flat sides, where the round roof starts
            float screenBase = g + 1.20f;
            float screenTop = g + 1.50f;
            float archHalf = f.wheelRadius + 0.14f;

            // ---- shell ----
            BuildVanUnderbody(f, rear, f.frontAxle + archHalf + 0.12f, archHalf, brown, trim);
            Span("Lower band", -hw, hw, f.archTop, split, rear, noseZ, brown);
            Span("Body", -hw, hw, split, shoulder, rear, screenTopZ, cream);
            Span("Nose", -hw, hw, split - 0.01f, screenBase, screenTopZ, noseZ, brown);
            Cylinder("Nose curve", new Vector3(0f, g + 1.00f, noseZ), new Vector3(0.40f, hw - 0.02f, 0.40f),
                brown, Quaternion.Euler(0f, 0f, 90f));

            // Round roof: a cylinder laid along the van, squashed to 20 cm of curve, with its lower
            // half buried in the body.
            Cylinder("Round roof", new Vector3(0f, shoulder, (rear + screenTopZ) * 0.5f),
                new Vector3(2f * hw, (screenTopZ - rear) * 0.5f, 0.40f), cream,
                Quaternion.Euler(90f, 0f, 0f));

            // Upright split windscreen, built the same way as the Transit's.
            SideSlab("Windscreen", 0f, 2f * hw - 0.02f, new Vector2(screenTopZ, screenTop),
                new Vector2(screenBaseZ, screenBase), 0.30f, glass);
            SideSlab("Windscreen divider", 0f, 0.06f, new Vector2(screenTopZ, screenTop),
                new Vector2(screenBaseZ, screenBase), 0.05f, cream, lift: 0.01f, trimEnds: -0.01f);
            for (int side = -1; side <= 1; side += 2)
            {
                SideSlab($"Windscreen pillar {SideName(side)}", side * (hw - 0.035f), 0.07f,
                    new Vector2(screenTopZ, screenTop), new Vector2(screenBaseZ, screenBase), 0.06f,
                    cream, lift: 0.01f, trimEnds: -0.02f);
            }

            SpanPair("Door window", hw - 0.004f, hw + 0.004f, screenBase, screenTop - 0.02f,
                cabBack + 0.08f, screenTopZ, glass);

            // ---- sides ----
            SpanPair("Pinstripe", hw - 0.004f, hw + 0.006f, split, split + 0.03f, rear + 0.01f,
                noseZ - 0.02f, chocolate);
            Span("Pinstripe rear", -hw + 0.01f, hw - 0.01f, split, split + 0.03f, rear - 0.006f,
                rear + 0.004f, chocolate);

            SpanPair("Cab door shut line back", hw - 0.004f, hw + 0.007f, f.bodyBottom + 0.06f,
                shoulder - 0.02f, cabBack - 0.008f, cabBack + 0.008f, chocolate);
            SpanPair("Cab door shut line front", hw - 0.004f, hw + 0.007f, f.archTop + 0.04f,
                screenBase, screenTopZ - 0.03f, screenTopZ - 0.014f, chocolate);
            SpanPair("Cab door handle", hw, hw + 0.022f, g + 1.10f, g + 1.14f, cabBack + 0.10f,
                cabBack + 0.24f, chrome);

            // Ribbed panels down the load area, like the old corrugated vans. Left out behind the
            // hatch on the left side.
            for (int i = 0; i < 16; i++)
            {
                float z = rear + 0.18f + 0.16f * i;
                if (z > cabBack - 0.10f) break;

                Span($"Rib right {i + 1}", hw, hw + 0.014f, split + 0.06f, shoulder - 0.06f,
                    z - 0.0175f, z + 0.0175f, cream);

                bool behindHatch = z > hatchBack - 0.08f && z < hatchFront + 0.08f;
                if (!behindHatch)
                {
                    Span($"Rib left {i + 1}", -hw - 0.014f, -hw, split + 0.06f, shoulder - 0.06f,
                        z - 0.0175f, z + 0.0175f, cream);
                }
            }

            BuildServingHatch(hw, g, hatchBack, hatchFront, glass, chocolate, cream, awning);
            RoundArches(f, archHalf, chocolate, brown);

            // ---- front ----
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.56f;
                Disc($"Headlight bezel {SideName(side)}", new Vector3(x, g + 1.02f, 2.08f), 0.27f, 0.07f,
                    Vector3.forward, chrome);
                Disc($"Headlight {SideName(side)}", new Vector3(x, g + 1.02f, 2.085f), 0.22f, 0.08f,
                    Vector3.forward, headlight);
                Disc($"Front indicator {SideName(side)}", new Vector3(x, g + 0.86f, 2.06f), 0.08f, 0.06f,
                    Vector3.forward, amber);
            }

            Disc("Nose badge", new Vector3(0f, g + 1.00f, 2.10f), 0.12f, 0.06f, Vector3.forward, chrome);
            Cylinder("Front bumper", new Vector3(0f, g + 0.56f, 2.07f), new Vector3(0.12f, hw + 0.02f, 0.12f),
                chrome, Quaternion.Euler(0f, 0f, 90f));
            Span("Front plate", -0.22f, 0.22f, g + 0.505f, g + 0.615f, 2.13f, 2.138f, plate);

            SpanPair("Mirror arm", hw, hw + 0.09f, g + 1.26f, g + 1.29f, 1.47f, 1.50f, chocolate);
            for (int side = -1; side <= 1; side += 2)
            {
                Disc($"Mirror {SideName(side)}", new Vector3(side * (hw + 0.10f), g + 1.33f, 1.485f),
                    0.14f, 0.03f, Vector3.forward, chrome);
            }

            // ---- rear ----
            Span("Rear door shut line", -0.008f, 0.008f, f.bodyBottom + 0.10f, shoulder + 0.10f,
                rear - 0.007f, rear + 0.004f, chocolate);

            for (int side = -1; side <= 1; side += 2)
            {
                string s = SideName(side);
                Disc($"Rear window ring {s}", new Vector3(side * 0.40f, g + 1.30f, rear - 0.006f), 0.34f,
                    0.012f, Vector3.back, chrome);
                Disc($"Rear window {s}", new Vector3(side * 0.40f, g + 1.30f, rear - 0.01f), 0.28f, 0.02f,
                    Vector3.back, glass);
                Disc($"Brake light bezel {s}", new Vector3(side * 0.66f, g + 0.72f, rear - 0.015f), 0.25f,
                    0.03f, Vector3.back, chrome);
                AddBrakeLight(Disc($"Brake light {s}", new Vector3(side * 0.66f, g + 0.72f, rear - 0.02f),
                    0.20f, 0.04f, Vector3.back, brakeLight));
                Disc($"Rear indicator {s}", new Vector3(side * 0.66f, g + 0.92f, rear - 0.012f), 0.09f,
                    0.024f, Vector3.back, amber);
            }

            Cylinder("Rear bumper", new Vector3(0f, g + 0.50f, rear - 0.06f), new Vector3(0.12f, hw + 0.02f, 0.12f),
                chrome, Quaternion.Euler(0f, 0f, 90f));
            Span("Rear plate", -0.22f, 0.22f, g + 0.445f, g + 0.555f, rear - 0.128f, rear - 0.12f, plate);

            // ---- the loaf ----
            // Low, and towards the back of the roof. From the chase camera it then sits below the
            // line the roof already hides, so it costs no extra view of the road ahead.
            float roofTop = shoulder + 0.20f;
            const float loafZ = -1.40f;
            for (int i = 0; i < 2; i++)
            {
                float z = loafZ + (i == 0 ? -0.30f : 0.30f);
                Span($"Loaf bracket {i + 1}", -0.20f, 0.20f, roofTop - 0.03f, roofTop + 0.03f,
                    z - 0.04f, z + 0.04f, chocolate);
            }

            // A capsule laid along the van and flattened into a bread shape, with three score marks.
            float loafCentre = roofTop + 0.03f + 0.11f;
            Primitive(PrimitiveType.Capsule, "Loaf", new Vector3(0f, loafCentre, loafZ),
                new Vector3(0.50f, 0.50f, 0.22f), loaf, Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 3; i++)
            {
                Box($"Loaf score {i + 1}", new Vector3(0f, loafCentre + 0.104f, loafZ - 0.25f + 0.25f * i),
                    new Vector3(0.22f, 0.02f, 0.05f), cream, Quaternion.Euler(0f, 35f, 0f));
            }
        }

        /// <summary>
        /// Serving hatch on the left, kerb side, with a counter under it and a short striped awning
        /// over it. The awning only reaches 24 cm out, so it barely passes the hit box.
        /// </summary>
        private void BuildServingHatch(float hw, float g, float back, float front, Material glass,
            Material frame, Material stripeA, Material stripeB)
        {
            float low = g + 1.12f;
            float high = g + 1.44f;
            const float bar = 0.04f;

            Span("Hatch", -hw - 0.004f, -hw + 0.004f, low, high, back, front, glass);
            Span("Hatch frame top", -hw - 0.012f, -hw + 0.004f, high, high + bar, back - bar, front + bar, frame);
            Span("Hatch frame bottom", -hw - 0.012f, -hw + 0.004f, low - bar, low, back - bar, front + bar, frame);
            Span("Hatch frame back", -hw - 0.012f, -hw + 0.004f, low, high, back - bar, back, frame);
            Span("Hatch frame front", -hw - 0.012f, -hw + 0.004f, low, high, front, front + bar, frame);
            Span("Counter", -hw - 0.10f, -hw + 0.01f, low - 0.05f, low - 0.02f, back, front, frame);

            const int stripes = 7;
            const float reach = 0.24f;
            const float drop = 0.08f;
            float hinge = g + 1.51f;
            float stripe = (front - back) / stripes;
            float slope = Mathf.Sqrt(reach * reach + drop * drop);
            float angle = Mathf.Atan2(drop, reach) * Mathf.Rad2Deg;

            for (int i = 0; i < stripes; i++)
            {
                Material m = i % 2 == 0 ? stripeB : stripeA;
                float z = back + stripe * (i + 0.5f);

                Box($"Awning stripe {i + 1}", new Vector3(-hw - reach * 0.5f, hinge - drop * 0.5f, z),
                    new Vector3(slope + 0.02f, 0.014f, stripe), m, Quaternion.Euler(0f, 0f, angle));

                // Scalloped edge hanging off the front of the awning.
                Box($"Awning flap {i + 1}", new Vector3(-hw - reach - 0.004f, hinge - drop - 0.035f, z),
                    new Vector3(0.012f, 0.07f, stripe - 0.02f), m);
            }
        }

        /// <summary>
        /// Round wheel arches: a lip of six short bars around a half circle, and body-coloured fillers
        /// stepped into the top corners of the square opening underneath, so the opening looks round.
        /// Each step starts just outside the inner edge of the lip, so the lip hides the stair edge,
        /// and no step reaches past the opening, so nothing can poke out of the bodywork. They sit
        /// 3 mm inside the panel, so wherever the panel is already solid they are hidden.
        /// </summary>
        private void RoundArches(VanFrame f, float archHalf, Material lip, Material panel)
        {
            float hw = f.halfWidth;
            float radius = archHalf;
            float centreY = f.archTop + 0.04f - radius;
            float lipInner = radius - 0.03f;
            float openingTop = f.archTop - centreY;
            const int segments = 6;

            // Distance from the axle to the inner and outer edge of each step.
            float[] stepInner = { archHalf - 0.06f, archHalf - 0.14f, archHalf - 0.24f };
            float[] stepOuter = { archHalf + 0.01f, archHalf - 0.06f, archHalf - 0.14f };

            foreach (float axle in new[] { f.frontAxle, f.rearAxle })
            {
                string n = axle > 0f ? "front" : "rear";
                var centre = new Vector2(axle, centreY);

                for (int i = 0; i < segments; i++)
                {
                    float a0 = Mathf.PI * i / segments;
                    float a1 = Mathf.PI * (i + 1) / segments;
                    Vector2 p0 = centre + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                    Vector2 p1 = centre + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;

                    StrutPair($"Arch {n} lip {i + 1}", hw - 0.02f, hw + 0.03f, p0, p1, 0.06f, lip, 0.03f);
                }

                for (int step = 0; step < stepInner.Length; step++)
                {
                    float near = stepInner[step];
                    float lowest = Mathf.Sqrt(Mathf.Max(0f, lipInner * lipInner - near * near)) + 0.005f;
                    if (lowest >= openingTop) continue;

                    for (int way = -1; way <= 1; way += 2)
                    {
                        string where = way > 0 ? "ahead" : "behind";
                        SpanPair($"Arch {n} corner {where} {step + 1}", hw - 0.10f, hw - 0.003f,
                            centreY + lowest, f.archTop + 0.01f,
                            axle + way * near, axle + way * stepOuter[step], panel);
                    }
                }
            }
        }

        // ==================================================================
        // LIVERY
        // ==================================================================

        /// <summary>
        /// The company's ZZ monogram on a badge. <paramref name="right"/> is the reader's right across
        /// the panel and <paramref name="outward"/> points out of the panel towards the reader, so the
        /// letters come out the right way round on whichever face the badge goes on.
        /// </summary>
        private void ZzBadge(string name, Vector3 centre, Vector3 right, Vector3 outward, float height,
            Material badge, Material ink)
        {
            Vector3 up = Vector3.up;
            float letterHeight = height * 0.72f;
            float letterWidth = letterHeight * 0.78f;
            float bar = letterHeight * 0.21f;
            float gap = letterWidth * 0.35f;
            float slant = letterWidth * 0.12f;    // italic lean, so it looks quick
            float badgeWidth = 2f * letterWidth + gap + 2f * slant + height * 0.30f;

            Decal(name, centre, right, outward, badgeWidth, height, 0.006f, 0.004f, badge);

            for (int i = 0; i < 2; i++)
            {
                float across = (i == 0 ? -0.5f : 0.5f) * (letterWidth + gap);
                string letter = $"{name} Z{i + 1}";

                Vector3 top = centre + right * (across + slant) + up * (letterHeight * 0.5f - bar * 0.5f);
                Vector3 bottom = centre + right * (across - slant) - up * (letterHeight * 0.5f - bar * 0.5f);

                Decal(letter + " top", top, right, outward, letterWidth, bar, 0.006f, 0.009f, ink);
                Decal(letter + " bottom", bottom, right, outward, letterWidth, bar, 0.006f, 0.009f, ink);

                // The diagonal, from the left end of the bottom bar to the right end of the top bar.
                Vector3 from = bottom + right * (-letterWidth * 0.5f + bar * 0.55f) + up * (bar * 0.5f);
                Vector3 to = top + right * (letterWidth * 0.5f - bar * 0.55f) - up * (bar * 0.5f);
                Vector3 diagonal = to - from;

                Decal(letter + " diagonal", (from + to) * 0.5f, diagonal.normalized, outward,
                    diagonal.magnitude + bar * 0.5f, bar * 1.05f, 0.006f, 0.009f, ink);
            }
        }

        // ==================================================================
        // VAN HELPERS
        // ==================================================================

        private static string SideName(int side) => side < 0 ? "left" : "right";

        private void AddBrakeLight(GameObject lamp)
        {
            _brakeLightRenderers.Add(lamp.GetComponent<Renderer>());
        }

        /// <summary>A box given by its extents rather than its centre and size.</summary>
        private GameObject Span(string name, float x0, float x1, float y0, float y1, float z0, float z1,
            Material material)
        {
            return Box(name,
                new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0)),
                material);
        }

        /// <summary>The same box on both sides. x0 to x1 is measured outwards from the centre line.</summary>
        private void SpanPair(string name, float x0, float x1, float y0, float y1, float z0, float z1,
            Material material)
        {
            Span(name + " right", x0, x1, y0, y1, z0, z1, material);
            Span(name + " left", -x1, -x0, y0, y1, z0, z1, material);
        }

        /// <summary>
        /// A bar seen from the side, centred on the line from a to b, spanning x0 to x1 across the van.
        /// Points are (along the van, height). <paramref name="overlap"/> lengthens both ends so that
        /// bars meeting at an angle leave no notch at the joint.
        /// </summary>
        private GameObject Strut(string name, float x0, float x1, Vector2 a, Vector2 b, float thickness,
            Material material, float overlap = 0f)
        {
            Vector2 d = b - a;
            Vector3 forward = new Vector3(0f, d.y, d.x).normalized;
            Vector3 hint = Mathf.Abs(forward.y) > 0.99f ? Vector3.forward : Vector3.up;
            Vector2 mid = (a + b) * 0.5f;

            return Box(name, new Vector3((x0 + x1) * 0.5f, mid.y, mid.x),
                new Vector3(Mathf.Abs(x1 - x0), thickness, d.magnitude + 2f * overlap), material,
                Quaternion.LookRotation(forward, hint));
        }

        private void StrutPair(string name, float x0, float x1, Vector2 a, Vector2 b, float thickness,
            Material material, float overlap = 0f)
        {
            Strut(name + " right", x0, x1, a, b, thickness, material, overlap);
            Strut(name + " left", -x1, -x0, a, b, thickness, material, overlap);
        }

        /// <summary>
        /// A sloping panel seen from the side, such as a bonnet or a windscreen, with its outer face
        /// on the line from a to b and its thickness going in underneath. Points are (along the van,
        /// height). <paramref name="lift"/> raises it off that line, for parts laid over another one.
        /// </summary>
        private GameObject SideSlab(string name, float x, float width, Vector2 a, Vector2 b,
            float thickness, Material material, float lift = 0f, float trimEnds = 0f)
        {
            Vector2 d = (b - a).normalized;
            float length = (b - a).magnitude - 2f * trimEnds;

            // The perpendicular that faces up, or forwards if the line is vertical.
            var n = new Vector2(-d.y, d.x);
            if (n.y < 0f || (Mathf.Approximately(n.y, 0f) && n.x < 0f)) n = -n;

            Vector2 mid = (a + b) * 0.5f + n * (lift - thickness * 0.5f);
            Quaternion rotation = Quaternion.LookRotation(new Vector3(0f, d.y, d.x), new Vector3(0f, n.y, n.x));

            return Box(name, new Vector3(x, mid.y, mid.x), new Vector3(width, thickness, length), material,
                rotation);
        }

        /// <summary>
        /// A thin flat part laid on a panel: <paramref name="along"/> runs its length, and its outer
        /// face stands <paramref name="proud"/> out from <paramref name="centre"/> along
        /// <paramref name="outward"/>.
        /// </summary>
        private GameObject Decal(string name, Vector3 centre, Vector3 along, Vector3 outward, float length,
            float width, float depth, float proud, Material material)
        {
            Vector3 across = Vector3.Cross(outward, along).normalized;
            return Box(name, centre + outward * (proud - depth * 0.5f), new Vector3(length, width, depth),
                material, Quaternion.LookRotation(outward, across));
        }

        /// <summary>A round flat part: a cylinder of this diameter and thickness, facing along <paramref name="facing"/>.</summary>
        private GameObject Disc(string name, Vector3 centre, float diameter, float thickness, Vector3 facing,
            Material material)
        {
            return Cylinder(name, centre, new Vector3(diameter, thickness * 0.5f, diameter), material,
                Quaternion.FromToRotation(Vector3.up, facing));
        }

        /// <summary>Any Unity primitive, collider stripped, parented to the generated model.</summary>
        private GameObject Primitive(PrimitiveType type, string name, Vector3 localPosition, Vector3 size,
            Material material, Quaternion localRotation)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            StripCollider(shape);

            shape.transform.SetParent(_generated, false);
            shape.transform.localPosition = localPosition;
            shape.transform.localRotation = localRotation;
            shape.transform.localScale = size;
            shape.GetComponent<MeshRenderer>().sharedMaterial = material;

            return shape;
        }
    }
}
