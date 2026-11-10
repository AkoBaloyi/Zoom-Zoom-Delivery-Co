using System;

namespace ZoomZoom.Orders
{
    /// <summary>
    /// Synthesises the order sound effects as raw samples, so no audio files are needed. Each
    /// sound is a few short notes with a quick attack and a smooth fade, kept soft and clear so
    /// they cut through the engine without being harsh. Plain C# with no Unity types; OrderAudio
    /// turns the samples into AudioClips.
    /// </summary>
    public static class OrderSounds
    {
        public const int SampleRate = 44100;

        /// <summary>Two rising notes, like a phone notification: a new order has arrived.</summary>
        public static float[] NewOrder() => Mix(0.5f,
            Note(0.00f, 880f, 0.16f, 0.5f),
            Note(0.10f, 1320f, 0.22f, 0.5f));

        /// <summary>A short upward pop: an ingredient was collected.</summary>
        public static float[] Ingredient() => Mix(0.18f, Sweep(0f, 620f, 1040f, 0.12f, 0.45f));

        /// <summary>A soft low knock: ingredients went into the bay.</summary>
        public static float[] Loaded() => Mix(0.25f,
            Note(0.00f, 196f, 0.18f, 0.5f),
            Note(0.05f, 294f, 0.15f, 0.3f));

        /// <summary>A bell: food is ready to collect from the bay.</summary>
        public static float[] Ready() => Mix(0.9f,
            Bell(0.00f, 1046.5f, 0.8f, 0.45f),
            Bell(0.15f, 1318.5f, 0.7f, 0.35f));

        /// <summary>A bright two note cash sound: an order was delivered.</summary>
        public static float[] Delivered() => Mix(0.5f,
            Note(0.00f, 1568f, 0.12f, 0.3f),
            Note(0.07f, 2093f, 0.35f, 0.3f),
            Note(0.07f, 1046.5f, 0.35f, 0.15f));

        /// <summary>A low falling buzz: an order was cancelled or burned.</summary>
        public static float[] Failed() => Mix(0.5f, Sweep(0f, 330f, 150f, 0.45f, 0.45f, buzzy: true));

        /// <summary>Two quiet low blips: the car is full, or the bay is busy.</summary>
        public static float[] Refused() => Mix(0.25f,
            Note(0.00f, 330f, 0.07f, 0.3f),
            Note(0.11f, 262f, 0.09f, 0.3f));

        /// <summary>A short woodblock tick. Played faster and higher as an order runs out.</summary>
        public static float[] Tick() => Mix(0.06f, Note(0f, 1400f, 0.05f, 0.4f), Click(0f, 0.012f, 0.15f));

        // ---- building blocks ------------------------------------------------------------------

        private struct Layer
        {
            public float Start, Duration, Volume;
            public Func<float, float> Wave; // time since layer start, in seconds, to sample
        }

        /// <summary>A sine note with a 5 ms attack and an exponential fade, plus a quiet octave
        /// for warmth.</summary>
        private static Layer Note(float start, float freq, float duration, float volume) => new Layer
        {
            Start = start, Duration = duration, Volume = volume,
            Wave = t => Envelope(t, duration) *
                        (float)(Math.Sin(2 * Math.PI * freq * t) + 0.25 * Math.Sin(4 * Math.PI * freq * t))
        };

        /// <summary>A bell: inharmonic overtones that fade faster than the fundamental.</summary>
        private static Layer Bell(float start, float freq, float duration, float volume) => new Layer
        {
            Start = start, Duration = duration, Volume = volume,
            Wave = t =>
            {
                double a = Math.Sin(2 * Math.PI * freq * t) * Math.Exp(-t * 5.0);
                double b = Math.Sin(2 * Math.PI * freq * 2.76 * t) * Math.Exp(-t * 9.0) * 0.4;
                double c = Math.Sin(2 * Math.PI * freq * 5.4 * t) * Math.Exp(-t * 14.0) * 0.2;
                return Attack(t) * (float)(a + b + c);
            }
        };

        /// <summary>A pitch glide from one frequency to another.</summary>
        private static Layer Sweep(float start, float from, float to, float duration, float volume, bool buzzy = false) =>
            new Layer
            {
                Start = start, Duration = duration, Volume = volume,
                Wave = t =>
                {
                    // Phase is the integral of a linearly changing frequency.
                    double k = (to - from) / duration;
                    double phase = 2 * Math.PI * (from * t + 0.5 * k * t * t);
                    double s = Math.Sin(phase);
                    if (buzzy) s = 0.7 * s + 0.3 * Math.Sin(3 * phase) + 0.15 * Math.Sin(5 * phase);
                    return Envelope(t, duration) * (float)s;
                }
            };

        /// <summary>A tiny burst of noise for the hard edge of a tick.</summary>
        private static Layer Click(float start, float duration, float volume)
        {
            var random = new Random(7);
            return new Layer
            {
                Start = start, Duration = duration, Volume = volume,
                Wave = t => (float)(random.NextDouble() * 2 - 1) * (1f - t / duration)
            };
        }

        private static float Attack(float t) => t < 0.005f ? t / 0.005f : 1f;

        /// <summary>Quick attack, then a fade that reaches near silence at the end.</summary>
        private static float Envelope(float t, float duration) =>
            Attack(t) * (float)Math.Exp(-5.0 * t / duration);

        private static float[] Mix(float length, params Layer[] layers)
        {
            float total = length;
            foreach (Layer l in layers) total = Math.Max(total, l.Start + l.Duration);

            var samples = new float[(int)(total * SampleRate) + 1];
            foreach (Layer l in layers)
            {
                int first = (int)(l.Start * SampleRate);
                int count = (int)(l.Duration * SampleRate);
                for (int i = 0; i < count && first + i < samples.Length; i++)
                    samples[first + i] += l.Volume * l.Wave(i / (float)SampleRate);
            }

            // Keep every sound safely below clipping.
            float peak = 0f;
            foreach (float s in samples) peak = Math.Max(peak, Math.Abs(s));
            if (peak > 0.9f)
                for (int i = 0; i < samples.Length; i++) samples[i] *= 0.9f / peak;

            return samples;
        }
    }
}
