using System;
using Template.Infra;
using Template.Infra.Audio;
using UnityEngine;

namespace Tailwind.Audio
{
    /// <summary>
    /// Tailwind's one calm lo-fi loop, synthesised in code: 76 BPM, eight bars of Fmaj7–Em7–Dm7–Cmaj7 on soft keys,
    /// a sine bass, a gentle kick, swung hats and a little vinyl crackle. Notes that ring past the end wrap to the
    /// start, so it repeats without a seam. Title and runs share it, so it never restarts between them.
    /// </summary>
    public static class MusicLoop
    {
        public const int Rate = 22050;
        public const float Bpm = 76f;
        public const int Bars = 8;

        private static AudioClip _clip;

        public static AudioClip Clip
        {
            get
            {
                if (_clip == null)
                {
                    var samples = Samples();
                    _clip = AudioClip.Create("dusk-loop", samples.Length, 1, Rate, false);
                    _clip.SetData(samples, 0);
                }

                return _clip;
            }
        }

        /// <summary>Starts the loop (crossfading in), or keeps it going if it's already playing.</summary>
        public static void Play()
        {
            if (Services.TryGet<AudioService>(out var audio))
            {
                audio.PlayMusic(Clip, 1.2f);
            }
        }

        public static float[] Samples()
        {
            // Root, then the chord's 3rd, 5th and 7th as semitones above it.
            (int root, int[] tones)[] chords =
            {
                (53, new[] { 4, 7, 11 }), // Fmaj7
                (52, new[] { 3, 7, 10 }), // Em7
                (50, new[] { 3, 7, 10 }), // Dm7
                (48, new[] { 4, 7, 11 }), // Cmaj7
            };
            float beat = 60f / Bpm;
            var buffer = new float[Mathf.RoundToInt(Bars * 4 * beat * Rate)];
            var random = new System.Random(5);
            for (int bar = 0; bar < Bars; bar++)
            {
                var (root, tones) = chords[bar % chords.Length];
                float t0 = bar * 4 * beat;
                foreach (float strike in new[] { 0f, 2.5f })
                {
                    Keys(buffer, t0 + strike * beat, root + 12, 0.035f);
                    foreach (int tone in tones)
                    {
                        Keys(buffer, t0 + strike * beat + 0.012f, root + 12 + tone, 0.03f); // a slight strum
                    }
                }

                Note(buffer, t0, Hz(root - 12), 1.8f * beat, 0.12f, 0.02f, 0.3f);
                Note(buffer, t0 + 2 * beat, Hz(root - 5), 1.4f * beat, 0.1f, 0.02f, 0.3f);
                Kick(buffer, t0);
                Kick(buffer, t0 + 2 * beat);
                for (int i = 0; i < 8; i++)
                {
                    float swing = i % 2 == 1 ? 0.08f * beat : 0f;
                    Hat(buffer, t0 + i * 0.5f * beat + swing, i % 2 == 1 ? 0.018f : 0.01f, random);
                }
            }

            Crackle(buffer, random);
            return Normalized(buffer, 0.6f);
        }

        private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        /// <summary>A soft electric-piano-ish note: a sine with a quieter octave, struck and left to fade.</summary>
        private static void Keys(float[] buffer, float at, int midi, float amp)
        {
            Note(buffer, at, Hz(midi), 1.6f, amp, 0.01f, 0.9f, 1.2f);
            Note(buffer, at, Hz(midi + 12), 0.8f, amp * 0.25f, 0.01f, 0.5f, 2.5f);
        }

        private static void Note(float[] buffer, float at, float hz, float seconds, float amp, float attack, float release, float decay = 0f)
        {
            int start = Mathf.RoundToInt(at * Rate);
            int n = Mathf.RoundToInt((seconds + release) * Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float envelope = t < attack ? t / attack : t < seconds ? 1f : Mathf.Max(0f, 1f - (t - seconds) / release);
                if (decay > 0f)
                {
                    envelope *= Mathf.Exp(-t * decay);
                }

                buffer[(start + i) % buffer.Length] += Mathf.Sin(2f * Mathf.PI * hz * t) * envelope * amp;
            }
        }

        private static void Kick(float[] buffer, float at)
        {
            int start = Mathf.RoundToInt(at * Rate);
            int n = Mathf.RoundToInt(0.16f * Rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += 2.0 * Math.PI * Mathf.Lerp(100f, 45f, t) / Rate;
                buffer[(start + i) % buffer.Length] += (float)Math.Sin(phase) * (1f - t) * (1f - t) * 0.16f;
            }
        }

        private static void Hat(float[] buffer, float at, float amp, System.Random random)
        {
            int start = Mathf.RoundToInt(at * Rate);
            int n = Mathf.RoundToInt(0.035f * Rate);
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                buffer[(start + i) % buffer.Length] += (white - last) * (1f - i / (float)n) * amp; // crude high-pass
                last = white;
            }
        }

        /// <summary>Sparse, very quiet clicks, like dust on a record.</summary>
        private static void Crackle(float[] buffer, System.Random random)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (random.NextDouble() < 0.0004)
                {
                    buffer[i] += (float)(random.NextDouble() * 2.0 - 1.0) * 0.02f;
                }
            }
        }

        private static float[] Normalized(float[] buffer, float peak)
        {
            float max = 0f;
            foreach (float s in buffer)
            {
                max = Mathf.Max(max, Mathf.Abs(s));
            }

            for (int i = 0; i < buffer.Length && max > 0f; i++)
            {
                buffer[i] *= peak / max;
            }

            return buffer;
        }
    }
}
