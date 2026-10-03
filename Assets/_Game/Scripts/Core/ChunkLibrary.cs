using System;
using System.Collections.Generic;

namespace Tailwind.Core
{
    [Serializable]
    public struct ChimneyDef
    {
        public float x;
        public float gapCenter;

        /// <summary>Multiplies the distance-based gap; above 1 for easy openings, never below 1.</summary>
        public float gapScale;
    }

    [Serializable]
    public struct StreamDef
    {
        public float xStart;
        public float xEnd;
        public float yCenter;
    }

    [Serializable]
    public struct LetterDef
    {
        public float x;
        public float y;
    }

    /// <summary>A hand-designed slice of course. Positions are relative to where the chunk is placed.</summary>
    public sealed class ChunkDef
    {
        public string id;
        public int tier;
        public float length;
        public ChimneyDef[] chimneys = new ChimneyDef[0];
        public StreamDef[] streams = new StreamDef[0];
        public LetterDef[] letters = new LetterDef[0];
    }

    /// <summary>
    /// The ten chunks the course is built from, in three difficulty tiers. Hand-made chunks keep
    /// patterns fair; a fully random course produces impossible spots.
    /// </summary>
    public static class ChunkLibrary
    {
        public const string IntroChunkId = "intro_flat";

        private static ChimneyDef C(float x, float gapCenter, float gapScale = 1f) => new ChimneyDef { x = x, gapCenter = gapCenter, gapScale = gapScale };
        private static StreamDef S(float from, float to, float y) => new StreamDef { xStart = from, xEnd = to, yCenter = y };
        private static LetterDef L(float x, float y) => new LetterDef { x = x, y = y };

        public static readonly IReadOnlyList<ChunkDef> Default = new List<ChunkDef>
        {
            // Tier 0: wide gaps, gentle height changes, the first wind stream.
            new ChunkDef { id = IntroChunkId, tier = 0, length = 10f,
                chimneys = new[] { C(3f, 5.5f, 1.4f), C(7.5f, 5.5f, 1.4f) },
                letters = new[] { L(5.5f, 5.5f) } },
            new ChunkDef { id = "steps_gentle", tier = 0, length = 10f,
                chimneys = new[] { C(3f, 4.8f, 1.25f), C(7.5f, 6f, 1.25f) },
                letters = new[] { L(5.4f, 5.4f) } },
            new ChunkDef { id = "first_wind", tier = 0, length = 11f,
                chimneys = new[] { C(2f, 5.5f, 1.3f), C(9f, 5.5f, 1.3f) },
                streams = new[] { S(3.5f, 8f, 5.5f) },
                letters = new[] { L(5f, 5.5f), L(6.8f, 5.5f) } },

            // Tier 1: height swings and streams near the top or bottom.
            // Every chunk obeys the fairness rule: |height change| <= maxTransitionSlope × open air (checked by a test).
            // Close chimneys also keep their safe bands overlapping by more than one flap's lift (~1.17 m),
            // which caps swings at ~0.9 m where gaps are narrowest (2.6 m).
            new ChunkDef { id = "zigzag", tier = 1, length = 11f,
                chimneys = new[] { C(2f, 4.85f), C(5.5f, 5.75f), C(9f, 4.85f) },
                letters = new[] { L(3.8f, 5.3f), L(7.3f, 5.3f) } },
            new ChunkDef { id = "high_wind", tier = 1, length = 11f,
                chimneys = new[] { C(2f, 7f), C(9f, 7f) },
                streams = new[] { S(3.5f, 8f, 7.4f) },
                letters = new[] { L(5f, 7.4f), L(6.6f, 7.4f) } },
            new ChunkDef { id = "low_wind", tier = 1, length = 11f,
                chimneys = new[] { C(2f, 3.6f), C(9f, 3.6f) },
                streams = new[] { S(3.5f, 8f, 3.4f) },
                letters = new[] { L(5f, 3.4f), L(6.6f, 3.4f) } },
            new ChunkDef { id = "stairs_up", tier = 1, length = 11f,
                chimneys = new[] { C(2f, 4.3f), C(5.5f, 5.2f), C(9f, 6.1f) },
                letters = new[] { L(7.3f, 5.65f) } },

            // Tier 2: streams threaded through gaps, bigger dives, dense rows.
            new ChunkDef { id = "threaded_wind", tier = 2, length = 10f,
                chimneys = new[] { C(2f, 5f), C(5f, 5f), C(8f, 5f) },
                streams = new[] { S(1.5f, 9.5f, 5f) },
                letters = new[] { L(3.5f, 5f), L(6.5f, 5f) } },
            new ChunkDef { id = "dive", tier = 2, length = 10.5f,
                chimneys = new[] { C(2f, 7f), C(8f, 5f) },
                letters = new[] { L(5.5f, 6f) } },
            new ChunkDef { id = "lantern_row", tier = 2, length = 12f,
                chimneys = new[] { C(1.5f, 4.9f), C(4.5f, 5.7f), C(7.5f, 4.9f), C(10.5f, 5.7f) },
                letters = new[] { L(6f, 5.3f) } },
        };

        public static ChunkDef Find(IReadOnlyList<ChunkDef> chunks, string id)
        {
            foreach (var chunk in chunks)
            {
                if (chunk.id == id)
                {
                    return chunk;
                }
            }

            return null;
        }

        public static List<ChunkDef> OfTier(IReadOnlyList<ChunkDef> chunks, int tier)
        {
            var result = new List<ChunkDef>();
            foreach (var chunk in chunks)
            {
                if (chunk.tier == tier)
                {
                    result.Add(chunk);
                }
            }

            return result;
        }
    }
}
