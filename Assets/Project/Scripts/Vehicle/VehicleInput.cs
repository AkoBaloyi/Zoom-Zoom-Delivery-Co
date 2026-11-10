using UnityEngine;
using UnityEngine.InputSystem;

namespace ZoomZoom.Vehicle
{
    /// <summary>Analog pedals, brake-to-reverse convenience and explicit D/R technique gestures.</summary>
    [DisallowMultipleComponent]
    public class VehicleInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset controlsAsset;
        [SerializeField] private VehicleController controller;
        private InputActionAsset _ownedAsset;
        private InputActionMap _map;
        private InputAction _gas, _brake, _steer, _drive, _reverse, _lookBehind, _recover, _spin, _technique, _drift, _motion;
        private bool _floored, _braking, _suppress, _paused;
        private bool _hasFocus = true;
        private float _gasAmount, _brakeAmount;
        private readonly TaxiPedalDirection _direction = new TaxiPedalDirection();
        public float RawAccelerator { get; private set; }
        public float RawBrake { get; private set; }
        public float AcceleratorAmount => _gasAmount;
        public float BrakeAmount => _brakeAmount;
        public TaxiGear SelectedGear { get; private set; } = TaxiGear.Drive;
        public Vector2 CameraLook => Vector2.zero;
        public bool CameraLookIsMouse => false;
        public bool LookBehind => !_paused && _lookBehind != null && _lookBehind.IsPressed();
        public bool IsReady => _map != null;
        public bool MotionEffectsEnabled { get; private set; } = true;
        public string AcceleratorSource => _gas?.activeControl?.path ?? "idle";
        public string BrakeSource => _brake?.activeControl?.path ?? "idle";
        public Gamepad ActiveGamepad => (_gas?.activeControl?.device as Gamepad)
            ?? (_brake?.activeControl?.device as Gamepad) ?? (_steer?.activeControl?.device as Gamepad);

        private void Awake()
        {
            if (controller == null) controller = GetComponent<VehicleController>();
            _ownedAsset = controlsAsset != null ? Instantiate(controlsAsset) : BuildFallbackControls();
            _map = _ownedAsset.FindActionMap("Vehicle", true);
            if (_map.FindAction("Accelerator") == null || _map.FindAction("GearDrive") == null || _map.FindAction("QuickSpin") == null || _map.FindAction("Technique") == null)
            {
                Destroy(_ownedAsset);
                _ownedAsset = BuildFallbackControls();
                _map = _ownedAsset.FindActionMap("Vehicle", true);
            }
            _gas = _map.FindAction("Accelerator", true);
            _brake = _map.FindAction("Brake", true);
            _steer = _map.FindAction("Steer", true);
            _drive = _map.FindAction("GearDrive", true);
            _reverse = _map.FindAction("GearReverse", true);
            _lookBehind = _map.FindAction("LookBehind", true);
            _recover = _map.FindAction("Recover", true);
            _spin = _map.FindAction("QuickSpin", true);
            _technique = _map.FindAction("Technique", true);
            _drift = _map.FindAction("Drift", true);
            _motion = _map.FindAction("ToggleMotion", true);
            _technique.performed += Technique;
            _drift.performed += DirectDrift;
            _motion.performed += ToggleMotion;
            _gas.performed += GasChanged;
            _gas.canceled += GasChanged;
            _brake.performed += BrakeChanged;
            _brake.canceled += BrakeChanged;
            _drive.performed += SelectDrive;
            _reverse.performed += SelectReverse;
            _steer.performed += SteerChanged;
            _recover.performed += Recover;
            _spin.performed += QuickSpin;
            if (controller != null) controller.Repositioned += ResetInput;
        }

        public static InputActionAsset BuildFallbackControls()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "Crazy Taxi Controls";
            var map = asset.AddActionMap("Vehicle");
            var gas = map.AddAction("Accelerator", InputActionType.Value, expectedControlLayout: "Axis");
            gas.AddBinding("<Keyboard>/w");
            gas.AddBinding("<Keyboard>/upArrow");
            gas.AddBinding("<Gamepad>/rightTrigger");
            var brake = map.AddAction("Brake", InputActionType.Value, expectedControlLayout: "Axis");
            brake.AddBinding("<Keyboard>/s");
            brake.AddBinding("<Keyboard>/downArrow");
            brake.AddBinding("<Gamepad>/leftTrigger");
            var steer = map.AddAction("Steer", InputActionType.Value, expectedControlLayout: "Axis");
            steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            steer.AddBinding("<Gamepad>/leftStick/x");
            map.AddAction("GearDrive", InputActionType.Button).AddBinding("<Keyboard>/f");
            map.FindAction("GearDrive").AddBinding("<Gamepad>/buttonEast");
            map.AddAction("GearReverse", InputActionType.Button).AddBinding("<Keyboard>/r");
            map.FindAction("GearReverse").AddBinding("<Gamepad>/buttonSouth");
            map.AddAction("LookBehind", InputActionType.Button).AddBinding("<Keyboard>/c");
            map.FindAction("LookBehind").AddBinding("<Gamepad>/rightStickPress");
            map.AddAction("Recover", InputActionType.Button).AddBinding("<Keyboard>/backspace");
            map.FindAction("Recover").AddBinding("<Gamepad>/select");
            map.AddAction("QuickSpin", InputActionType.Button).AddBinding("<Keyboard>/space");
            map.FindAction("QuickSpin").AddBinding("<Gamepad>/leftShoulder");
            map.AddAction("Technique", InputActionType.Button).AddBinding("<Keyboard>/leftShift");
            map.FindAction("Technique").AddBinding("<Gamepad>/rightShoulder");
            map.AddAction("Drift", InputActionType.Button).AddBinding("<Keyboard>/leftCtrl");
            map.FindAction("Drift").AddBinding("<Gamepad>/buttonWest");
            map.AddAction("ToggleMotion", InputActionType.Button).AddBinding("<Keyboard>/f8");
            map.FindAction("ToggleMotion").AddBinding("<Gamepad>/dpad/up");
            return asset;
        }

        private void OnEnable() { _suppress = false; _map?.Enable(); }
        private void OnDisable() { _suppress = true; _map?.Disable(); ResetInput(); }
        private bool AcceptInput => !_suppress && _hasFocus && controller != null && Time.timeScale > 0f;

        private void GasChanged(InputAction.CallbackContext context)
        {
            if (!AcceptInput) return;
            float floor = controller.Taxi.pedalFloor;
            float value = context.ReadValue<float>();
            bool down = _floored ? value > floor - controller.Taxi.pedalFloorHysteresis : value >= floor;
            if (down == _floored) return;
            _floored = down;
            controller.QueueTaxiCommand(down ? TaxiCommand.GasDown : TaxiCommand.GasUp, context.time);
        }
        private void BrakeChanged(InputAction.CallbackContext context)
        {
            if (!AcceptInput) return;
            bool down = context.ReadValue<float>() > controller.Taxi.brakeTrickThreshold;
            if (down == _braking) return;
            _braking = down;
            controller.QueueTaxiCommand(down ? TaxiCommand.BrakeDown : TaxiCommand.BrakeUp, context.time);
        }
        private void SelectDrive(InputAction.CallbackContext context)
        {
            if (!AcceptInput || controller.Taxi.directTechniqueControls) return;
            SelectedGear = TaxiGear.Drive;
            controller.QueueTaxiCommand(TaxiCommand.Drive, context.time);
        }
        private void SelectReverse(InputAction.CallbackContext context)
        {
            if (!AcceptInput || controller.Taxi.directTechniqueControls) return;
            SelectedGear = TaxiGear.Reverse;
            controller.QueueTaxiCommand(TaxiCommand.Reverse, context.time);
        }
        private void Recover(InputAction.CallbackContext context)
        {
            if (AcceptInput) controller.RequestTaxiRecovery();
        }
        private void SteerChanged(InputAction.CallbackContext context)
        {
            if (AcceptInput && Mathf.Abs(context.ReadValue<float>()) >= controller.Taxi.trickSteerThreshold)
                controller.QueueTaxiCommand(TaxiCommand.Steer, context.time);
        }
        private void QuickSpin(InputAction.CallbackContext context)
        {
            if (!AcceptInput || !controller.CanQuickSpinNow) return;
            float raw = Mathf.Clamp(_steer.ReadValue<float>(), -1f, 1f);
            float steer = controller.Taxi.MapPedal(Mathf.Abs(raw), controller.Taxi.steeringDeadzone,
                controller.Taxi.steeringTravel) * Mathf.Sign(raw);
            if (Mathf.Abs(steer) < controller.Taxi.trickSteerThreshold) return;
            controller.Drive.steer = steer;
            controller.QueueTaxiCommand(TaxiCommand.QuickSpin, context.time);
        }
        // Read current hardware values here, not last frame's smoothed Drive state.
        // A held pedal plus a fresh technique press works independently of gear rhythm.
        private void Technique(InputAction.CallbackContext context)
        {
            if (!AcceptInput || !controller.Taxi.directTechniqueControls) return;
            if (_brake.ReadValue<float>() > controller.Taxi.brakeTrickThreshold)
                controller.QueueTaxiCommand(TaxiCommand.DirectStop, context.time);
            else if (_gas.ReadValue<float>() >= controller.Taxi.pedalFloor)
                controller.QueueTaxiCommand(TaxiCommand.DirectDash, context.time);
        }
        private void DirectDrift(InputAction.CallbackContext context)
        {
            if (!AcceptInput || !controller.Taxi.directTechniqueControls) return;
            float raw = _steer.ReadValue<float>();
            controller.Drive.steer = controller.Taxi.MapPedal(Mathf.Abs(raw), controller.Taxi.steeringDeadzone,
                controller.Taxi.steeringTravel) * Mathf.Sign(raw);
            controller.QueueTaxiCommand(TaxiCommand.DirectDrift, context.time);
        }
        private void ToggleMotion(InputAction.CallbackContext context)
        {
            if (AcceptInput) MotionEffectsEnabled = !MotionEffectsEnabled;
        }
        private void Update()
        {
            if (controller == null || _map == null) return;
            if (!_hasFocus) return;
            bool paused = Time.timeScale <= 0f;
            if (paused != _paused) ResetInput();
            _paused = paused;
            if (paused) return;
            CrazyTaxiSettings settings = controller.Taxi;
            RawAccelerator = Mathf.Clamp01(_gas.ReadValue<float>());
            RawBrake = Mathf.Clamp01(_brake.ReadValue<float>());
            float gasTarget = settings.MapPedal(RawAccelerator, settings.acceleratorDeadzone, settings.acceleratorTravel);
            float brakeTarget = settings.MapPedal(RawBrake, settings.brakeDeadzone, settings.brakeTravel);
            float gasTime = gasTarget > _gasAmount ? settings.acceleratorRiseTime : settings.acceleratorReleaseTime;
            _gasAmount = SmoothPedal(_gasAmount, gasTarget, gasTime, Time.deltaTime);
            _brakeAmount = SmoothPedal(_brakeAmount, brakeTarget, settings.brakeResponseTime, Time.deltaTime);
            float rawSteer = Mathf.Clamp(_steer.ReadValue<float>(), -1f, 1f);
            Vector3 planar = Vector3.ProjectOnPlane(controller.Body.linearVelocity, controller.GroundNormal);
            DriveInput drive = _direction.Resolve(settings, _gasAmount, _brakeAmount, controller.ForwardSpeed,
                planar.magnitude, controller.IsGrounded, Time.deltaTime, out bool directionChanged);
            // Pedals alone choose travel direction. Trick gestures never change it.
            SelectedGear = _direction.Direction;
            drive.steer = settings.MapPedal(Mathf.Abs(rawSteer), settings.steeringDeadzone, settings.steeringTravel) * Mathf.Sign(rawSteer);
            drive.engineThrottle = _gasAmount;
            drive.freeRev = _gasAmount > settings.directionPedalThreshold && _brakeAmount > settings.directionPedalThreshold;
            controller.Drive = drive;
        }
        private static float SmoothPedal(float current, float target, float responseTime, float dt) =>
            responseTime <= 0f ? target : Mathf.MoveTowards(current, target, dt / responseTime);
        public void ResetInput()
        {
            _floored = _braking = false;
            _gasAmount = _brakeAmount = RawAccelerator = RawBrake = 0f;
            SelectedGear = TaxiGear.Drive;
            _direction.Reset();
            controller?.ClearTaxiInput();
        }
        private void OnApplicationFocus(bool focused)
        {
            _hasFocus = focused;
            if (!focused) ResetInput();
        }
        private void OnDestroy()
        {
            if (controller != null) controller.Repositioned -= ResetInput;
            if (_ownedAsset != null) Destroy(_ownedAsset);
        }
    }
}
