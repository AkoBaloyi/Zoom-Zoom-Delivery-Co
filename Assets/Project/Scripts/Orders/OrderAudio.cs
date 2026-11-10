using UnityEngine;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Sound cues for the order system, so the player hears what is happening without having to
    /// look at the phone:
    ///   a new order        two note notification chime
    ///   ingredient         short pop
    ///   into the bay       soft knock
    ///   food ready         bell
    ///   delivered          bright cash sound
    ///   cancelled, burned  low falling buzz
    ///   car full, bay busy two quiet blips
    ///   running out        a soft tick, once a second, then twice a second in the last few
    ///                      seconds, rising in pitch: for whichever order is closest to being
    ///                      lost, including food about to burn
    ///
    /// The ticking replaces the flashing markers playtesters found hard on the eyes: urgency is
    /// heard, and shown only by a slow pulse.
    ///
    /// All sounds are made in code (OrderSounds), so there are no audio files to import. Added
    /// automatically next to the OrderManager, so there is nothing to set up. Turn the volume
    /// down, or switch off single cues, in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrderAudio : MonoBehaviour
    {
        [Header("Wiring (found automatically if left empty)")]
        [SerializeField] private OrderManager orderManager;
        [SerializeField] private CargoSystem cargoSystem;
        [SerializeField] private OrderFocus focus;

        [Header("Volume")]
        [Range(0f, 1f)] [SerializeField] private float volume = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float tickVolume = 0.5f;

        [Header("Cues")]
        [SerializeField] private bool playNewOrder = true;
        [SerializeField] private bool playIngredient = true;
        [SerializeField] private bool playBay = true;
        [SerializeField] private bool playResults = true;
        [SerializeField] private bool playRefused = true;
        [SerializeField] private bool playUrgentTicks = true;

        [Header("Urgent ticking")]
        [Tooltip("Seconds left below which the most urgent order starts ticking.")]
        [SerializeField] private float urgentBelowSeconds = 8f;
        [Tooltip("Seconds left below which the ticking doubles in speed.")]
        [SerializeField] private float hurryBelowSeconds = 3f;

        private AudioSource _source;
        private AudioSource _tickSource; // its own source, because pitch applies to everything a source plays
        private AudioClip _newOrder, _ingredient, _loaded, _ready, _delivered, _failed, _refused, _tick;
        private float _tickTimer;
        private float _lastRefused = -10f;

        private void Awake()
        {
            if (orderManager == null) orderManager = FindAnyObjectByType<OrderManager>();
            if (cargoSystem == null) cargoSystem = FindAnyObjectByType<CargoSystem>();

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // heard the same wherever the car is

            _tickSource = gameObject.AddComponent<AudioSource>();
            _tickSource.playOnAwake = false;
            _tickSource.spatialBlend = 0f;

            _newOrder = Clip("Order new", OrderSounds.NewOrder());
            _ingredient = Clip("Order ingredient", OrderSounds.Ingredient());
            _loaded = Clip("Order loaded", OrderSounds.Loaded());
            _ready = Clip("Order ready", OrderSounds.Ready());
            _delivered = Clip("Order delivered", OrderSounds.Delivered());
            _failed = Clip("Order failed", OrderSounds.Failed());
            _refused = Clip("Order refused", OrderSounds.Refused());
            _tick = Clip("Order tick", OrderSounds.Tick());
        }

        private void Start()
        {
            if (focus == null) focus = OrderFocus.Ensure(orderManager);

            if (orderManager != null)
            {
                orderManager.OrderSpawned += HandleSpawned;
                orderManager.OrderPaid += HandlePaid;
            }
            if (cargoSystem != null)
            {
                cargoSystem.IngredientCollected += HandleIngredient;
                cargoSystem.IngredientsLoaded += HandleLoaded;
                cargoSystem.Refused += HandleRefused;
            }
            ProcessingBay.AnyBayEvent += HandleBay;
        }

        private void OnDestroy()
        {
            if (orderManager != null)
            {
                orderManager.OrderSpawned -= HandleSpawned;
                orderManager.OrderPaid -= HandlePaid;
            }
            if (cargoSystem != null)
            {
                cargoSystem.IngredientCollected -= HandleIngredient;
                cargoSystem.IngredientsLoaded -= HandleLoaded;
                cargoSystem.Refused -= HandleRefused;
            }
            ProcessingBay.AnyBayEvent -= HandleBay;
        }

        private void Update()
        {
            if (!playUrgentTicks || focus == null || orderManager == null || !orderManager.enabled) return;

            Order urgent = focus.MostUrgent;
            if (urgent == null || urgent.IsTerminal || urgent.SecondsUntilLost >= urgentBelowSeconds)
            {
                _tickTimer = 0f;
                return;
            }

            // Time.deltaTime, so the ticking stops while the game is paused.
            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;

            float left = urgent.SecondsUntilLost;
            _tickTimer = left < hurryBelowSeconds ? 0.5f : 1f;

            float urgency = 1f - Mathf.Clamp01(left / Mathf.Max(0.01f, urgentBelowSeconds));
            _tickSource.pitch = Mathf.Lerp(1f, 1.35f, urgency);
            _tickSource.PlayOneShot(_tick, volume * tickVolume);
        }

        // ---- events ---------------------------------------------------------------------------

        private void HandleSpawned(Order order)
        {
            if (playNewOrder) Play(_newOrder);
        }

        private void HandlePaid(Order order, OrderPayout payout)
        {
            if (!playResults) return;
            bool failed = payout.Timing == DeliveryTiming.Failed || payout.Timing == DeliveryTiming.Burned;
            Play(failed ? _failed : _delivered);
        }

        private void HandleIngredient(Order order, IngredientStop stop)
        {
            if (playIngredient) Play(_ingredient);
        }

        private void HandleLoaded(Order order, ProcessingBay bay)
        {
            if (playBay) Play(_loaded);
        }

        private void HandleBay(ProcessingBay bay, Order order, string what)
        {
            if (playBay && what == "ready") Play(_ready);
        }

        private void HandleRefused(string reason)
        {
            // The car sitting in a full shop asks every frame, so only blip now and then.
            if (!playRefused || Time.unscaledTime - _lastRefused < 2f) return;
            _lastRefused = Time.unscaledTime;
            Play(_refused);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) _source.PlayOneShot(clip, volume);
        }

        private static AudioClip Clip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, OrderSounds.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
