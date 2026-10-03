using UnityEngine;

namespace Tailwind.Audio
{
    /// <summary>Tailwind's sound effects, synthesised at startup (no audio files needed yet).</summary>
    public sealed class SfxBank
    {
        private const int Rate = 44100;

        public AudioClip Flap { get; } = Sweep("flap", 520f, 760f, 0.07f, 0.35f);
        public AudioClip Whoosh { get; } = Noise("whoosh", 0.35f, 0.25f, true);
        public AudioClip ComboUp { get; } = Sweep("combo", 660f, 990f, 0.09f, 0.3f);
        public AudioClip Chime { get; } = TwoTone("chime", 880f, 1320f, 0.22f, 0.35f);
        public AudioClip NearMiss { get; } = Sweep("near", 1200f, 1600f, 0.06f, 0.3f);
        public AudioClip Story { get; } = TwoTone("story", 660f, 990f, 0.4f, 0.3f);
        public AudioClip Crash { get; } = Noise("crash", 0.4f, 0.5f, false);

        private static AudioClip Sweep(string name, float fromHz, float toHz, float seconds, float volume)
        {
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += 2.0 * Mathf.PI * Mathf.Lerp(fromHz, toHz, t) / Rate;
                data[i] = Mathf.Sin((float)phase) * (1f - t) * volume;
            }

            return Clip(name, data);
        }

        private static AudioClip TwoTone(string name, float firstHz, float secondHz, float seconds, float volume)
        {
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float hz = t < 0.4f ? firstHz : secondHz;
                float envelope = t < 0.4f ? 1f - t : (1f - t) * 1.2f;
                data[i] = Mathf.Sin(2f * Mathf.PI * hz * i / Rate) * envelope * volume;
            }

            return Clip(name, data);
        }

        private static AudioClip Noise(string name, float seconds, float volume, bool swell)
        {
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            var random = new System.Random(7);
            float smoothed = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                smoothed = Mathf.Lerp(smoothed, white, swell ? 0.08f : 0.35f); // crude low-pass
                float envelope = swell ? Mathf.Sin(t * Mathf.PI) : (1f - t) * (1f - t);
                data[i] = smoothed * envelope * volume;
            }

            return Clip(name, data);
        }

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
