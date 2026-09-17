using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomZoom.Vehicle
{
    /// <summary>
    /// Turns Unity Input System actions into a simple set of numbers the car can read.
    ///
    /// Why this is its own script:
    ///  - The controller should not care whether the player is on a keyboard or a pad. It just
    ///    wants "throttle 0.6, steer -1". That means we can swap input, or let the measurement
    ///    script drive the car instead, without touching the physics at all.
    ///  - To let the measurement harness drive, we just DISABLE this component. Nothing else changes.
    ///
    /// Bindings live in VehicleControls.inputactions. Both devices drive the SAME actions, so
    /// there is only one set of behaviour to tune and to explain:
    ///
    ///   Action        Keyboard              Controller
    ///   ------------  --------------------  ------------------
    ///   Throttle      W / S                 RT / LT
    ///   Steer         A / D                 Left stick X
    ///   Brake         Left Ctrl             B (east)
    ///   Handbrake     Space                 LB
    ///   Boost         Left Alt              RB
    ///   Jump          Left Shift            A (south)
    ///
    /// Three separate ways to slow down, on purpose:
    ///   S           brakes, then reverses once stopped. The everyday one.
    ///   Left Ctrl   brakes and never reverses. For stopping ON a drop-off point.
    ///   Space       handbrake. Locks the REAR only, so the car rotates rather than just slowing.
    ///   Flip          Q                     X (west)
    ///   FlipDirection WASD                  Left stick
    ///   CameraLook    Mouse move            Right stick
    ///
    /// Input is read in Update (once per drawn frame, which is when the player's press exists)
    /// and consumed by the physics in FixedUpdate. Button presses are handed over as one-shot
    /// requests so a press can never be silently dropped or acted on twice.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleInput : MonoBehaviour
    {
        private const string MapName = "Vehicle";

        [Header("Wiring")]
        [Tooltip("The VehicleControls.inputactions asset. Drag it in if this is empty.")]
        [SerializeField] private InputActionAsset controlsAsset;

        [Tooltip("The car this input drives.")]
        [SerializeField] private VehicleController controller;

        [Tooltip("The jump/flip component on the same car. Optional: leave empty to disable both moves.")]
        [SerializeField] private VehicleJumpFlip jumpFlip;

        [Header("Feel")]
        [Tooltip("Invert the vertical camera look, for people who prefer it.")]
        [SerializeField] private bool invertCameraLookY = false;

        private InputActionMap _map;
        private bool _ownsControlsAsset;
        private InputAction _throttle;
        private InputAction _steer;
        private InputAction _brake;
        private InputAction _handbrake;
        private InputAction _boost;
        private InputAction _jump;
        private InputAction _flip;
        private InputAction _flipDirection;
        private InputAction _cameraLook;

        /// <summary>Right stick / mouse movement this frame. The camera reads this itself.</summary>
        public Vector2 CameraLook { get; private set; }

        /// <summary>True when everything is wired up and the actions were found.</summary>
        public bool IsReady => _map != null;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<VehicleController>();
            if (jumpFlip == null) jumpFlip = GetComponent<VehicleJumpFlip>();

            if (controlsAsset == null)
            {
                Debug.LogWarning(
                    "[VehicleInput] No input actions asset assigned, so the same bindings are being " +
                    "built in code instead. The car will drive normally. To edit the bindings in the " +
                    "inspector, drag Assets/_Project/Scripts/Vehicle/VehicleControls.inputactions " +
                    "onto the 'Controls Asset' field, or use Tools > Zoom Zoom > Rebuild VehicleLab " +
                    "Scene which wires it up for you.", this);

                controlsAsset = BuildFallbackControls();
                _ownsControlsAsset = true;
            }
            else
            {
                Debug.Log(
                    $"[VehicleInput] Using bindings from '{controlsAsset.name}'.", this);
            }

            ResolveActions();
        }

        /// <summary>
        /// Looks up the action map and every action from the asset as it exists RIGHT NOW.
        ///
        /// WHY THIS IS NOT DONE ONCE IN Awake
        /// It used to be, and that caused this:
        ///
        ///   Map must be contained in state
        ///   Map index on InputActionMap is out of range
        ///
        /// Those are Debug.Asserts inside the Input System's InputActionState. EnableAllActions checks
        /// that the map it was handed is present in the state it belongs to, then calls EnableControls
        /// which asserts the same thing again, which is why a single Enable() call prints each message
        /// twice and it looks like two components are at fault when there is only one.
        ///
        /// They fire when the InputActionMap object is orphaned: its state exists but no longer lists
        /// this map, and its index is stale. That happens whenever the .inputactions asset is
        /// re-imported while the scene is loaded, which is exactly what editing the bindings by hand
        /// does. Unity rebuilds the asset's maps as new objects and the reference cached in Awake keeps
        /// pointing at the old ones.
        ///
        /// Awake does not run again when a component is merely disabled and re-enabled, and this
        /// component is disabled deliberately, by VehicleMeasurement, so the harness can drive. So the
        /// stale reference survived and OnEnable kept handing it to Enable().
        ///
        /// Re-resolving is a handful of dictionary lookups once per enable, not per frame, so the cost
        /// is irrelevant next to being correct.
        /// </summary>
        private void ResolveActions()
        {
            if (controlsAsset == null) return;

            _map = controlsAsset.FindActionMap(MapName, throwIfNotFound: false);
            if (_map == null)
            {
                Debug.LogError(
                    $"[VehicleInput] The assigned asset has no '{MapName}' action map.", this);
                return;
            }

            _throttle = Find("Throttle");
            _steer = Find("Steer");
            _brake = Find("Brake");
            _handbrake = Find("Handbrake");
            _boost = Find("Boost");
            _jump = Find("Jump");
            _flip = Find("Flip");
            _flipDirection = Find("FlipDirection");
            _cameraLook = Find("CameraLook");
        }

        /// <summary>
        /// True when the cached map must not be handed to Enable or Disable.
        ///
        /// The identity comparison at the end is the one that matters, and it is not obvious. Comparing
        /// _map.asset against controlsAsset is not enough: when Unity re-imports an .inputactions file
        /// it reloads the ScriptableObject IN PLACE, so the asset reference survives and the old map
        /// still names it as its owner, while the asset's own array has been rebuilt with new map
        /// objects. The old map therefore looks perfectly valid by every cheap test and is still
        /// orphaned from the live state.
        ///
        /// So the real question is not "does this map claim to belong to the asset" but "is this the map
        /// the asset hands out right now", and only reference equality answers that.
        /// </summary>
        private bool MapIsStale
        {
            get
            {
                if (_map == null || controlsAsset == null) return true;
                if (_map.asset != controlsAsset) return true;

                return !ReferenceEquals(_map, controlsAsset.FindActionMap(MapName, throwIfNotFound: false));
            }
        }

        /// <summary>
        /// The same bindings as VehicleControls.inputactions, built in code.
        ///
        /// WHY THIS DUPLICATION EXISTS, AND WHEN IT IS USED
        /// The .inputactions asset is the source of truth for the game. It is what gets edited, and
        /// it is what a rebinding screen would read later. But a scene with an unassigned asset
        /// reference is a car that silently does not move, which is a horrible thing to hand to
        /// somebody. So if the field is empty, the lab builds the identical set in code and says so
        /// in the console. If the two ever disagree, the asset is the one that is right.
        /// </summary>
        private InputActionAsset BuildFallbackControls()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "VehicleControls (built in code)";

            InputActionMap map = asset.AddActionMap(MapName);

            // Throttle: forward and reverse on one axis.
            InputAction throttle = map.AddAction("Throttle", InputActionType.Value);
            throttle.AddCompositeBinding("1DAxis")
                .With("Positive", "<Keyboard>/w")
                .With("Negative", "<Keyboard>/s");
            throttle.AddCompositeBinding("1DAxis")
                .With("Positive", "<Gamepad>/rightTrigger")
                .With("Negative", "<Gamepad>/leftTrigger");

            InputAction steer = map.AddAction("Steer", InputActionType.Value);
            steer.AddCompositeBinding("1DAxis")
                .With("Positive", "<Keyboard>/d")
                .With("Negative", "<Keyboard>/a");
            steer.AddBinding("<Gamepad>/leftStick/x");

            // Footbrake on Left Ctrl, not Space. S already brakes before it reverses, so this is the
            // secondary control: stop without ever rolling backwards, which is what you want when
            // pulling up at a drop-off point.
            InputAction brake = map.AddAction("Brake", InputActionType.Value);
            brake.AddBinding("<Keyboard>/leftCtrl");
            brake.AddBinding("<Gamepad>/buttonEast");

            // Handbrake on Space. It was on Left Ctrl, which is the wrong way round: the handbrake is
            // a thing you stab mid-corner and hold, and Space is the key every driving game puts it
            // on. LB matches the same convention on a pad.
            InputAction handbrake = map.AddAction("Handbrake", InputActionType.Value);
            handbrake.AddBinding("<Keyboard>/space");
            handbrake.AddBinding("<Gamepad>/leftShoulder");

            // Boost. Left Shift is already the jump and the right trigger is already the throttle, so
            // this takes Left Alt, which is reachable without leaving WASD, and the right shoulder on a
            // pad. Held rather than tapped, because the boost is a resource being spent.
            InputAction boost = map.AddAction("Boost", InputActionType.Value);
            boost.AddBinding("<Keyboard>/leftAlt");
            boost.AddBinding("<Gamepad>/rightShoulder");

            InputAction jump = map.AddAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/leftShift");
            jump.AddBinding("<Gamepad>/buttonSouth");

            InputAction flip = map.AddAction("Flip", InputActionType.Button);
            flip.AddBinding("<Keyboard>/q");
            flip.AddBinding("<Gamepad>/buttonWest");

            // Which way to flip. Same stick as steering, so the flip goes where you were already
            // pointing the controls.
            InputAction flipDirection = map.AddAction("FlipDirection", InputActionType.Value);
            flipDirection.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            flipDirection.AddBinding("<Gamepad>/leftStick");

            InputAction cameraLook = map.AddAction("CameraLook", InputActionType.Value);
            cameraLook.AddBinding("<Mouse>/delta", processors: "ScaleVector2(x=0.06,y=0.06)");
            cameraLook.AddBinding("<Gamepad>/rightStick");

            return asset;
        }

        private InputAction Find(string actionName)
        {
            InputAction action = _map.FindAction(actionName, throwIfNotFound: false);
            if (action == null)
            {
                Debug.LogError(
                    $"[VehicleInput] Action '{actionName}' is missing from the '{MapName}' map.", this);
            }
            return action;
        }

        private void OnEnable()
        {
            // Re-resolved unconditionally rather than only when a staleness test says so. Enabling is
            // rare, a handful of name lookups costs nothing measurable, and any test for "is this
            // reference still good" is a guess about Unity's asset reload behaviour that would silently
            // stop being true. Fetching the current map is not a guess.
            ResolveActions();

            _map?.Enable();
        }

        private void OnDisable()
        {
            // Only disable a map that is still attached to its asset. DisableAllActions carries the
            // same assertions as the enable path, so disabling an orphaned map spams the console on the
            // way out just as enabling one did on the way in. An orphaned map is already not driving
            // anything, so there is nothing to switch off.
            if (!MapIsStale) _map.Disable();

            // Let go of everything. Without this, a key held at the moment this component is
            // switched off would stay "held" forever as far as the car is concerned.
            if (controller != null) controller.Drive = default;
            if (jumpFlip != null) jumpFlip.SetJumpHeld(false);
            CameraLook = Vector2.zero;
        }

        private void Update()
        {
            if (_map == null || controller == null) return;

            controller.Drive = new DriveInput
            {
                throttle = Read(_throttle),
                steer = Read(_steer),
                brake = Read(_brake),
                handbrake = Read(_handbrake) > 0.5f,
                boost = Read(_boost) > 0.5f
            };

            if (jumpFlip != null)
            {
                // Held state drives the variable jump height.
                jumpFlip.SetJumpHeld(_jump != null && _jump.IsPressed());

                // Presses are queued, not applied here, because the physics step that will
                // act on them has not happened yet.
                if (_jump != null && _jump.WasPressedThisFrame())
                    jumpFlip.RequestJump();

                if (_flip != null && _flip.WasPressedThisFrame())
                {
                    Vector2 dir = _flipDirection != null
                        ? _flipDirection.ReadValue<Vector2>()
                        : Vector2.zero;
                    jumpFlip.RequestFlip(dir);
                }
            }

            Vector2 look = _cameraLook != null ? _cameraLook.ReadValue<Vector2>() : Vector2.zero;
            if (invertCameraLookY) look.y = -look.y;
            CameraLook = look;
        }

        private static float Read(InputAction action)
        {
            return action != null ? action.ReadValue<float>() : 0f;
        }

        private void OnDestroy()
        {
            // Only clean up the throwaway asset we made ourselves. Never destroy a real project asset.
            if (_ownsControlsAsset && controlsAsset != null)
            {
                Destroy(controlsAsset);
            }
        }
    }
}
