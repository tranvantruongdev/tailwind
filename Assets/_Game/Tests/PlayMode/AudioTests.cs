using System;
using System.Linq;
using NUnit.Framework;
using Tailwind.Audio;
using UnityEngine;

namespace Tailwind.PlayModeTests
{
    /// <summary>The music loop, measured (not listened to): exactly eight bars, below clipping, no seam where it repeats.</summary>
    public class AudioTests
    {
        [Test]
        public void The_loop_is_eight_bars_and_repeats_without_a_seam()
        {
            var samples = MusicLoop.Samples();
            Assert.AreEqual(Mathf.RoundToInt(MusicLoop.Bars * 4 * 60f / MusicLoop.Bpm * MusicLoop.Rate), samples.Length, "eight bars of 4/4");
            Assert.IsFalse(samples.Any(float.IsNaN));
            Assert.AreEqual(0.6f, samples.Max(Math.Abs), 0.001f, "normalised to 0.6, below clipping");
            float largestStep = 0f;
            for (int i = 1; i < samples.Length; i++)
            {
                largestStep = Mathf.Max(largestStep, Mathf.Abs(samples[i] - samples[i - 1]));
            }

            Assert.LessOrEqual(Mathf.Abs(samples[0] - samples[samples.Length - 1]), largestStep, "the jump from the end back to the start is no bigger than any step inside");
        }
    }
}
