using UnityEngine;

namespace ZoomZoom.Vehicle
{
    /// <summary>Layered engine and road loops. Authored clips can replace the procedural placeholders.</summary>
    [DisallowMultipleComponent]
    public sealed class TaxiEngineAudio : MonoBehaviour
    {
        private VehicleController _car;
        private AudioSource _engine, _road, _shift, _dash;
        private AudioClip _engineClip, _roadClip, _shiftClip, _dashClip;
        private bool _shiftPending;
        private float _shiftWait;
        public int ShiftCueCount { get; private set; }
        public int DashCueCount { get; private set; }
        private void Awake()
        {
            _car = GetComponent<VehicleController>();
            if (!Application.isPlaying || _car == null) return;
            _engineClip = CreateLoop(false);
            _roadClip = CreateLoop(true);
            _engine = CreateSource(_engineClip);
            _road = CreateSource(_roadClip);
            _shiftClip = CreateShiftClip();
            _shift = CreateSource(_shiftClip);
            _shift.loop = false;
            _shift.volume = 1f;
            _dashClip = CreateDashClip();
            _dash = CreateSource(_dashClip);
            _dash.loop = false;
            _dash.volume = 1f;
        }
        private void OnEnable()
        {
            if (_car != null) { _car.GearShifted += ConfirmShift; _car.TrickPerformed += ConfirmDash; _car.Repositioned += StopAudio; }
        }
        private void ConfirmDash(TaxiTrick trick)
        {
            if (_dash == null || !_car.Taxi.engineAudio || Time.timeScale <= 0f ||
                (trick != TaxiTrick.Dash && trick != TaxiTrick.LimiterCut)) return;
            _dash.PlayOneShot(_car.Taxi.dashCueClip != null ? _car.Taxi.dashCueClip : _dashClip, _car.Taxi.dashCueVolume);
            DashCueCount++;
        }
        private static AudioClip CreateDashClip()
        {
            const int rate = 24000;
            var samples = new float[8400];
            var random = new System.Random(101);
            float phase = 0f, noise = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(180f, 650f, t) / rate;
                noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2f - 1f, 0.3f);
                samples[i] = Mathf.Sin(Mathf.PI * t) * (1f - t) * (0.45f * Mathf.Sin(phase) + 0.45f * noise);
            }
            var clip = AudioClip.Create("Taxi dash launch", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
        private void ConfirmShift(int from, int to)
        {
            if (_shift == null || !_car.Taxi.engineAudio || Time.timeScale <= 0f) return;
            _shift.pitch = to > from ? 0.9f : 1.15f;
            _shiftWait = Mathf.Max(0f, _car.Taxi.gearShiftTime) * 0.85f;
            _shiftPending = true;
        }
        private static AudioClip CreateShiftClip()
        {
            const int rate = 24000;
            float[] samples = new float[2400];
            var random = new System.Random(1618);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 5f)) * Mathf.Exp(-7f * t);
                samples[i] = envelope * (0.65f * Mathf.Sin(2f * Mathf.PI * 95f * i / rate) +
                    0.25f * ((float)random.NextDouble() * 2f - 1f));
            }
            var clip = AudioClip.Create("Taxi gear engagement", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
        private AudioSource CreateSource(AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip; source.loop = true; source.playOnAwake = false;
            source.spatialBlend = 0f; source.volume = 0f;
            return source;
        }
        private static AudioClip CreateLoop(bool road)
        {
            const int rate = 24000;
            var samples = new float[rate];
            var random = new System.Random(road ? 2718 : 3141);
            float filtered = 0f;
            for (int i = 0; i < rate; i++)
            {
                float phase = 2f * Mathf.PI * 70f * i / rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                filtered = Mathf.Lerp(filtered, noise, road ? 0.12f : 0.35f);
                float pulse = Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 2f) +
                    0.28f * Mathf.Sin(phase * 3f) + 0.18f * Mathf.Sin(phase * 5f);
                float combustion = filtered * (0.45f + 0.55f * Mathf.Max(0f, Mathf.Sin(phase)));
                samples[i] = road ? filtered * 0.75f : Mathf.Clamp(pulse * 0.45f + combustion * 0.35f, -0.95f, 0.95f);
            }
            // Bridge the noise seam; engine harmonics already contain a whole number of cycles.
            for (int i = 0; i < 120; i++)
                samples[rate - 120 + i] = Mathf.Lerp(samples[rate - 120 + i], samples[i], i / 120f);
            var clip = AudioClip.Create(road ? "Taxi road air (procedural)" : "Taxi engine (procedural)", rate, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        private void Update()
        {
            if (_engine == null) return;
            var s = _car.Taxi;
            if (Time.timeScale <= 0f) { StopAudio(); return; }
            if (_car.IsFreeRevving) { _shiftPending = false; _shift.Stop(); }
            if (!s.engineAudio) { _dash.Stop(); _shiftPending = false; _shift.Stop(); }
            if (_shiftPending)
            {
                _shiftWait -= Time.deltaTime;
                if (_shiftWait <= 0f)
                {
                    _shiftPending = false;
                    _shift.PlayOneShot(s.gearShiftClip != null ? s.gearShiftClip : _shiftClip,
                        Mathf.Clamp01(s.shiftFeedbackVolume));
                    ShiftCueCount++;
                }
            }
            float revs = Mathf.InverseLerp(s.engineIdleRpm, s.engineRedlineRpm, _car.EngineRpm);
            float load = _car.EngineDemand;
            float speed = Mathf.Clamp01(_car.Speed / Mathf.Max(0.1f, s.cruiseSpeed));
            float pitch = _car.EngineRpm <= s.engineRedlineRpm
                ? Mathf.Lerp(s.engineIdlePitch, s.engineRedlinePitch, revs)
                : Mathf.Lerp(s.engineRedlinePitch, s.engineFreeRevPitch,
                    Mathf.InverseLerp(s.engineRedlineRpm, Mathf.Max(s.engineRedlineRpm + 1f, s.engineFreeRevLimit), _car.EngineRpm));
            Play(_engine, s.engineLoop != null ? s.engineLoop : _engineClip, s.engineAudio,
                pitch, s.engineVolume * Mathf.Lerp(0.3f, 1f, load) *
                Mathf.Lerp(0.2f, 1f, _car.GearThrustEnvelope) * _car.RevLimiterGain);
            Play(_road, s.roadLoop != null ? s.roadLoop : _roadClip, s.roadAudioVolume > 0f,
                Mathf.Lerp(0.7f, 1.4f, speed), s.roadAudioVolume * speed * speed);
        }
        private static void Play(AudioSource source, AudioClip clip, bool enabled, float pitch, float volume)
        {
            if (!enabled) { source.Stop(); source.volume = 0f; return; }
            if (source.clip != clip) { source.Stop(); source.clip = clip; }
            source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            source.volume = Mathf.MoveTowards(source.volume, Mathf.Clamp01(volume), Time.deltaTime * 4f);
            if (!source.isPlaying) source.Play();
        }
        private void StopAudio()
        { _shiftPending = false; if (_engine != null) _engine.Stop(); if (_road != null) _road.Stop(); if (_shift != null) _shift.Stop(); if (_dash != null) _dash.Stop(); }
        private void OnDisable()
        {
            StopAudio();
            if (_car != null) { _car.GearShifted -= ConfirmShift; _car.TrickPerformed -= ConfirmDash; _car.Repositioned -= StopAudio; }
        }
        private void OnDestroy()
        {
            if (_engine != null) Destroy(_engine);
            if (_road != null) Destroy(_road);
            if (_shift != null) Destroy(_shift);
            if (_dash != null) Destroy(_dash);
            if (_engineClip != null) Destroy(_engineClip);
            if (_roadClip != null) Destroy(_roadClip);
            if (_shiftClip != null) Destroy(_shiftClip);
            if (_dashClip != null) Destroy(_dashClip);
        }
    }
}
