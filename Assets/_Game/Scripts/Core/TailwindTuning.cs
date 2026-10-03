using System;

namespace Tailwind.Core
{
    /// <summary>
    /// Every gameplay number in one place. World units: 1 unit = 1 metre of distance.
    /// The play area runs from the ground (y = GroundY) to the sky ceiling (y = CeilingY).
    /// </summary>
    [Serializable]
    public sealed class TailwindTuning
    {
        // Glider
        public float gravity = -18f;
        public float flapVelocity = 6.5f;
        public float maxFallSpeed = 12f;
        public float gliderRadius = 0.25f;

        // Play area
        public float groundY = 1f;
        public float ceilingY = 10f;
        public float startRunway = 6f;

        // Speed ramps with distance
        public float baseSpeed = 3f;
        public float speedGainPer10m = 0.05f;
        public float maxSpeed = 5f;

        // Chimney gaps shrink with distance
        public float chimneyWidth = 1f;
        public float gapStart = 3.2f;
        public float gapEnd = 2.6f;
        public float gapShrinkDistance = 600f;
        public float minGapMargin = 0.6f;

        /// <summary>
        /// Fairness rule at chunk junctions: at most this much height change per metre of open air
        /// between consecutive chimneys. The generator inserts extra space when a junction is steeper.
        /// </summary>
        public float maxTransitionSlope = 0.4f;

        /// <summary>
        /// When the safe heights of two consecutive chimneys overlap by less than one flap's lift,
        /// there must be at least this much open air between them (about a second to drop and recover).
        /// </summary>
        public float minTransitionOpen = 4f;

        /// <summary>Height one flap gains from rest outside a stream: v² / 2g (≈1.17 m with defaults).</summary>
        public float HopHeight => flapVelocity * flapVelocity / (2f * -gravity);

        // Wind streams (the drafting twist)
        public float streamHeight = 1.2f;
        public float streamGravityScale = 0.6f;
        public float streamSpeedScale = 1.25f;

        /// <summary>Flaps are weaker inside a stream, so a tap keeps you riding it instead of popping out the top.</summary>
        public float streamFlapScale = 0.6f;
        public float comboStepSeconds = 0.5f;
        public int maxCombo = 5;
        public float comboGraceSeconds = 1f;

        // Scoring
        public int letterPoints = 10;
        public float letterPickupRadius = 0.35f;
        public int nearMissPoints = 5;
        public float nearMissDistance = 0.25f;

        public static TailwindTuning Default() => new TailwindTuning();
    }

    /// <summary>How the course gets harder with distance. Pure functions, unit-tested.</summary>
    public static class Difficulty
    {
        public static float SpeedAt(TailwindTuning t, float distance)
        {
            float speed = t.baseSpeed + Math.Max(0f, distance) / 10f * t.speedGainPer10m;
            return Math.Min(speed, t.maxSpeed);
        }

        public static float GapAt(TailwindTuning t, float distance)
        {
            float progress = Clamp01(distance / t.gapShrinkDistance);
            return t.gapStart + (t.gapEnd - t.gapStart) * progress;
        }

        /// <summary>Relative weights of chunk tiers 0 (easy), 1 (medium), 2 (hard) at a distance.</summary>
        public static double[] TierWeightsAt(float distance)
        {
            if (distance < 100f)
            {
                return new[] { 1.0, 0.0, 0.0 };
            }

            if (distance < 300f)
            {
                return new[] { 0.3, 0.7, 0.0 };
            }

            if (distance < 600f)
            {
                return new[] { 0.0, 0.6, 0.4 };
            }

            return new[] { 0.0, 0.3, 0.7 };
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
