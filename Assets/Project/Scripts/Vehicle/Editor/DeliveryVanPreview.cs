using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ZoomZoom.Vehicle.Editor
{
    /// <summary>
    /// Lines the car up beside the two delivery vans in a scratch scene, and renders review
    /// pictures of them for the group.
    ///
    /// Nothing here opens or changes a gameplay scene. The preview is a new scene that is never
    /// saved, every vehicle in it is a copy of Car.prefab with only its Body Style changed, and the
    /// pictures go to docs/handouts/van-preview, which git ignores.
    ///
    /// Tools > Zoom Zoom > Delivery Vans > Open Preview Scene
    /// Tools > Zoom Zoom > Delivery Vans > Render Review Pictures
    ///
    /// From the command line, with the editor closed:
    ///   Unity.exe -batchmode -projectPath . -executeMethod
    ///     ZoomZoom.Vehicle.Editor.DeliveryVanPreview.RenderFromCommandLine -quit
    /// </summary>
    public static class DeliveryVanPreview
    {
        private const string CarPrefabPath = "Assets/Project/Prefabs/Car.prefab";
        private const string OutputFolder = "docs/handouts/van-preview";
        private const int Width = 1600;
        private const int Height = 900;
        private const int Supersample = 2;
        private const float LineSpacing = 4.2f;

        private static readonly string[] Labels = { "Car", "Transit van", "Bakery van" };

        private static readonly CarVisualBuilder.BodyStyle[] Styles =
        {
            CarVisualBuilder.BodyStyle.Car,
            CarVisualBuilder.BodyStyle.TransitVan,
            CarVisualBuilder.BodyStyle.BakeryVan,
        };

        private sealed class Stage
        {
            public GameObject[] vehicles;
            public GameObject roadAhead;
            public float rideHeight;
            public CrazyTaxiSettings chase;
        }

        // ==================================================================
        // ENTRY POINTS
        // ==================================================================

        [MenuItem("Tools/Zoom Zoom/Delivery Vans/Open Preview Scene", priority = 70)]
        public static void OpenPreviewScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Stage stage = BuildStage();
            if (stage == null) return;

            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.LookAt(new Vector3(0f, 0.8f, 0f), Quaternion.Euler(16f, 215f, 0f), 7f);
            }

            Selection.activeGameObject = stage.vehicles[1];
            Debug.Log("[DeliveryVanPreview] Car, Transit van and bakery van lined up in a new " +
                      "scene. It is not saved: close it without saving and nothing has changed.");
        }

        [MenuItem("Tools/Zoom Zoom/Delivery Vans/Render Review Pictures", priority = 71)]
        public static void RenderFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string folder = Render(Path.GetFullPath(OutputFolder));
            if (folder != null) EditorUtility.RevealInFinder(folder);
        }

        /// <summary>For -executeMethod. Optional: -vanPreviewOut &lt;folder&gt;.</summary>
        public static void RenderFromCommandLine()
        {
            string folder = null;

            try
            {
                folder = Render(Path.GetFullPath(ArgumentAfter("-vanPreviewOut") ?? OutputFolder));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            EditorApplication.Exit(folder != null ? 0 : 1);
        }

        // ==================================================================
        // PICTURES
        // ==================================================================

        private static string Render(string folder)
        {
            Stage stage = BuildStage();
            if (stage == null) return null;

            Directory.CreateDirectory(folder);
            Camera camera = MakeCamera();
            var written = new List<string>();

            void Shot(string file, Vector3 position, Vector3 target, float fov)
            {
                camera.transform.SetPositionAndRotation(position,
                    Quaternion.LookRotation(target - position, Vector3.up));
                camera.fieldOfView = fov;

                string path = Path.Combine(folder, file);
                Capture(camera, path);
                written.Add(path);
            }

            // The three side by side.
            LineUp(stage);
            camera.transform.SetPositionAndRotation(new Vector3(9.5f, 3.4f, 11.5f),
                Quaternion.LookRotation(new Vector3(-9.5f, -2.7f, -11.5f), Vector3.up));
            WarmUp(camera);
            Shot("01-lineup-front.png", new Vector3(9.5f, 3.4f, 11.5f), new Vector3(0f, 0.7f, 0f), 34f);
            Shot("02-lineup-back.png", new Vector3(-9.5f, 3.6f, -11.5f), new Vector3(0f, 0.7f, 0f), 34f);

            // Each van on its own, from four sides.
            Vector3 middle = new Vector3(0f, 0.85f, 0f);
            int number = 3;
            for (int i = 1; i < stage.vehicles.Length; i++)
            {
                Isolate(stage, i);
                string name = Slug(Labels[i]);

                Shot($"{number++:00}-{name}-front.png", new Vector3(4.6f, 2.0f, 6.4f), middle, 32f);
                Shot($"{number++:00}-{name}-back.png", new Vector3(-4.8f, 2.3f, -6.6f), middle, 32f);
                Shot($"{number++:00}-{name}-left.png", new Vector3(-8.5f, 1.1f, 0f), middle, 32f);
                Shot($"{number++:00}-{name}-right.png", new Vector3(8.5f, 1.1f, 0f), middle, 32f);
            }

            // What the player sees: the game's own chase camera numbers, at a standstill, on a road
            // with cones every 7 m and a car-sized block 15 m ahead.
            stage.roadAhead.SetActive(true);
            for (int i = 0; i < stage.vehicles.Length; i++)
            {
                Isolate(stage, i);
                string path = Path.Combine(folder, $"{number++:00}-chase-camera-{Slug(Labels[i])}.png");
                ChaseShot(camera, stage, stage.vehicles[i], path);
                written.Add(path);
            }
            stage.roadAhead.SetActive(false);

            // The hit box, which the vans do not change, outlined on all three from the side.
            var outlines = new List<GameObject>();
            Material outline = Unlit("Hit box outline", new Color(1f, 0.1f, 0.75f));
            for (int i = 0; i < stage.vehicles.Length; i++)
            {
                stage.vehicles[i].SetActive(true);
                stage.vehicles[i].transform.position = new Vector3(0f, stage.rideHeight, (i - 1) * 5.4f);
                outlines.Add(OutlineHitBox(stage.vehicles[i], outline));
            }

            Shot($"{number:00}-hit-box.png", new Vector3(14f, 1.2f, 0f), new Vector3(0f, 0.9f, 0f), 40f);

            foreach (GameObject o in outlines)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            LineUp(stage);

            Debug.Log($"[DeliveryVanPreview] Wrote {written.Count} pictures to {folder}:\n" +
                      string.Join("\n", written));
            return folder;
        }

        private static void ChaseShot(Camera camera, Stage stage, GameObject vehicle, string path)
        {
            CrazyTaxiSettings s = stage.chase;
            Vector3 origin = vehicle.transform.position;
            Vector3 forward = vehicle.transform.forward;

            // Same sums as ChaseCamera at a standstill: no speed pull-back, no dash zoom.
            Vector3 position = origin - forward * s.cameraDistance + Vector3.up * s.cameraHeight;
            Vector3 aim = origin + Vector3.up * s.cameraAimHeight + forward * s.cameraLookAhead;
            float fov = s.useCameraFovCurve
                ? Mathf.Clamp(s.cameraFovBySpeed.Evaluate(0f), 20f, 120f)
                : s.cameraFov;

            camera.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(aim - position, Vector3.up));
            camera.fieldOfView = fov;
            camera.nearClipPlane = Mathf.Max(0.01f, s.cameraNearClip);

            Capture(camera, path);

            camera.nearClipPlane = 0.1f;
        }

        private static void Capture(Camera camera, string path)
        {
            int w = Width * Supersample;
            int h = Height * Supersample;

            var big = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                filterMode = FilterMode.Bilinear,
            };
            var small = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                filterMode = FilterMode.Bilinear,
            };
            big.Create();
            small.Create();

            RenderTexture previous = RenderTexture.active;
            RenderInto(camera, big);

            // Halving with bilinear filtering averages each 2 x 2 block, which is the anti-aliasing.
            Graphics.Blit(big, small);

            RenderTexture.active = small;
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(path, pixels.EncodeToPNG());

            Object.DestroyImmediate(pixels);
            big.Release();
            small.Release();
            Object.DestroyImmediate(big);
            Object.DestroyImmediate(small);
        }

        private static void RenderInto(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
                return;
            }

            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
        }

        /// <summary>
        /// Two throwaway frames before the first picture. The first frame after a scene is built has
        /// no shadows and wrong colours, because the pipeline sets its lighting up while drawing it.
        /// </summary>
        private static void WarmUp(Camera camera)
        {
            var scratch = new RenderTexture(256, 144, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            scratch.Create();

            for (int i = 0; i < 2; i++) RenderInto(camera, scratch);

            scratch.Release();
            Object.DestroyImmediate(scratch);
        }

        // ==================================================================
        // STAGE
        // ==================================================================

        private static Stage BuildStage()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[DeliveryVanPreview] {CarPrefabPath} not found.");
                return null;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetUpLighting();
            MakeGround();

            var stage = new Stage { vehicles = new GameObject[Styles.Length] };

            for (int i = 0; i < Styles.Length; i++)
            {
                GameObject vehicle = Object.Instantiate(prefab);
                vehicle.name = Labels[i];

                var builder = vehicle.GetComponent<CarVisualBuilder>();
                var controller = vehicle.GetComponent<VehicleController>();
                if (builder == null || controller == null || controller.Tuning == null)
                {
                    Debug.LogError("[DeliveryVanPreview] Car.prefab needs a VehicleController with a " +
                                   "tuning profile and a CarVisualBuilder.");
                    return null;
                }

                // Wheels touching the ground with the suspension at its rest length, which is where
                // the model puts them when the physics is not running.
                stage.rideHeight = controller.Tuning.suspensionRestLength - controller.Tuning.wheelHeightOffset;
                stage.chase = controller.Taxi;

                builder.SetBodyStyle(Styles[i]);
                builder.Build();
                stage.vehicles[i] = vehicle;
            }

            LineUp(stage);
            stage.roadAhead = MakeRoadAhead();
            stage.roadAhead.SetActive(false);
            return stage;
        }

        private static void LineUp(Stage stage)
        {
            for (int i = 0; i < stage.vehicles.Length; i++)
            {
                stage.vehicles[i].SetActive(true);
                stage.vehicles[i].transform.SetPositionAndRotation(
                    new Vector3((i - 1) * LineSpacing, stage.rideHeight, 0f), Quaternion.identity);
            }
        }

        private static void Isolate(Stage stage, int index)
        {
            for (int i = 0; i < stage.vehicles.Length; i++)
            {
                stage.vehicles[i].SetActive(i == index);
            }

            stage.vehicles[index].transform.SetPositionAndRotation(
                new Vector3(0f, stage.rideHeight, 0f), Quaternion.identity);
        }

        private static void SetUpLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(40f, -140f, 0f);

            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.85f, 0.9f, 1f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(30f, 35f, 0f);

            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            // Flat ambient, set on the probe directly so the pictures do not depend on lighting
            // having been generated for a scene that is never saved.
            var ambient = new Color(0.50f, 0.52f, 0.56f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = probe;
        }

        private static void MakeGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                Lit("Asphalt", new Color(0.34f, 0.35f, 0.37f), 0.15f);
        }

        /// <summary>Lane lines, cones every 7 m, and a car-sized block 15 m ahead in the same lane.</summary>
        private static GameObject MakeRoadAhead()
        {
            var root = new GameObject("Road ahead");
            Material paint = Lit("Road paint", new Color(0.92f, 0.92f, 0.88f), 0.2f);
            Material cone = Lit("Cone", new Color(1f, 0.45f, 0.08f), 0.3f);
            Material traffic = Lit("Car ahead", new Color(0.30f, 0.50f, 0.78f), 0.4f);

            for (float z = 3f; z < 120f; z += 6f)
            {
                Prop(root, PrimitiveType.Cube, "Centre line", new Vector3(1.9f, 0.005f, z),
                    new Vector3(0.12f, 0.01f, 3f), paint);
            }

            Prop(root, PrimitiveType.Cube, "Edge line", new Vector3(-1.9f, 0.005f, 60f),
                new Vector3(0.12f, 0.01f, 120f), paint);

            foreach (float z in new[] { 7f, 14f, 21f, 28f, 35f })
            {
                Prop(root, PrimitiveType.Cylinder, "Cone", new Vector3(-2.6f, 0.35f, z),
                    new Vector3(0.35f, 0.35f, 0.35f), cone);
                Prop(root, PrimitiveType.Cylinder, "Cone", new Vector3(2.6f, 0.35f, z),
                    new Vector3(0.35f, 0.35f, 0.35f), cone);
            }

            Prop(root, PrimitiveType.Cube, "Car ahead", new Vector3(0f, 0.7f, 15f),
                new Vector3(1.8f, 1.4f, 4.2f), traffic);

            return root;
        }

        /// <summary>The vehicle's BoxCollider drawn as twelve thin bright edges.</summary>
        private static GameObject OutlineHitBox(GameObject vehicle, Material material)
        {
            BoxCollider box = vehicle.GetComponentInChildren<BoxCollider>();
            if (box == null) return null;

            var root = new GameObject("Hit box outline");
            root.transform.SetParent(box.transform, false);

            Vector3 c = box.center;
            Vector3 h = box.size * 0.5f;
            const float t = 0.03f;

            for (int i = 0; i < 4; i++)
            {
                float a = (i & 1) == 0 ? -1f : 1f;
                float b = (i & 2) == 0 ? -1f : 1f;

                Prop(root, PrimitiveType.Cube, "Edge x", c + new Vector3(0f, a * h.y, b * h.z),
                    new Vector3(box.size.x + t, t, t), material);
                Prop(root, PrimitiveType.Cube, "Edge y", c + new Vector3(a * h.x, 0f, b * h.z),
                    new Vector3(t, box.size.y + t, t), material);
                Prop(root, PrimitiveType.Cube, "Edge z", c + new Vector3(a * h.x, b * h.y, 0f),
                    new Vector3(t, t, box.size.z + t), material);
            }

            return root;
        }

        private static Camera MakeCamera()
        {
            var camera = new GameObject("Review camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.80f, 0.87f, 0.94f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 800f;
            return camera;
        }

        // ==================================================================
        // HELPERS
        // ==================================================================

        private static void Prop(GameObject parent, PrimitiveType type, string name, Vector3 localPosition,
            Vector3 size, Material material)
        {
            GameObject prop = GameObject.CreatePrimitive(type);
            prop.name = name;
            Object.DestroyImmediate(prop.GetComponent<Collider>());
            prop.transform.SetParent(parent.transform, false);
            prop.transform.localPosition = localPosition;
            prop.transform.localScale = size;
            prop.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material Lit(string name, Color colour, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = colour };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        private static Material Unlit(string name, Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            return new Material(shader) { name = name, color = colour };
        }

        private static string Slug(string label) => label.ToLowerInvariant().Replace(' ', '-');

        private static string ArgumentAfter(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }

            return null;
        }
    }
}
