using System;
using System.Collections.Generic;
using Template.Core.Random;

namespace Tailwind.Core
{
    /// <summary>A chimney below the gap and a hanging lantern line above it.</summary>
    [Serializable]
    public struct Chimney
    {
        public float x;
        public float width;
        public float gapCenter;
        public float gapHeight;

        public float GapBottom => gapCenter - gapHeight * 0.5f;
        public float GapTop => gapCenter + gapHeight * 0.5f;
        public float Right => x + width;
    }

    [Serializable]
    public struct WindStream
    {
        public float xStart;
        public float xEnd;
        public float yCenter;
        public float height;

        public bool Contains(float x, float y) => x >= xStart && x <= xEnd && Math.Abs(y - yCenter) <= height * 0.5f;
    }

    [Serializable]
    public struct Letter
    {
        public float x;
        public float y;
    }

    /// <summary>
    /// The level: chimneys, wind streams and letters, generated ahead of the glider from hand-made
    /// chunks picked by distance tier. Same seed, same course, so runs can be replayed and simulated.
    /// </summary>
    public sealed class Course
    {
        private readonly TailwindTuning _tuning;
        private readonly SeededRandom _random;
        private readonly IReadOnlyList<ChunkDef> _chunks;
        private readonly List<Chimney> _chimneys = new List<Chimney>();
        private readonly List<WindStream> _streams = new List<WindStream>();
        private readonly List<Letter> _letters = new List<Letter>();
        private readonly List<string> _placedChunkIds = new List<string>();
        private float _generatedUntil;
        private string _lastChunkId;

        /// <summary>Procedural course from the chunk library.</summary>
        public Course(TailwindTuning tuning, ulong seed, IReadOnlyList<ChunkDef> chunks = null)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _random = new SeededRandom(seed);
            _chunks = chunks ?? ChunkLibrary.Default;
            _generatedUntil = tuning.startRunway;
            Procedural = true;
        }

        /// <summary>Fixed course for tests and hand-made challenges.</summary>
        public Course(TailwindTuning tuning, IEnumerable<Chimney> chimneys, IEnumerable<WindStream> streams = null, IEnumerable<Letter> letters = null)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _chimneys.AddRange(chimneys);
            _chimneys.Sort((a, b) => a.x.CompareTo(b.x));
            if (streams != null)
            {
                _streams.AddRange(streams);
                _streams.Sort((a, b) => a.xStart.CompareTo(b.xStart));
            }

            if (letters != null)
            {
                _letters.AddRange(letters);
                _letters.Sort((a, b) => a.x.CompareTo(b.x)); // pickup scan relies on x order
            }

            _generatedUntil = float.MaxValue;
            Procedural = false;
        }

        public bool Procedural { get; }
        public IReadOnlyList<Chimney> Chimneys => _chimneys;
        public IReadOnlyList<WindStream> Streams => _streams;
        public IReadOnlyList<Letter> Letters => _letters;
        public IReadOnlyList<string> PlacedChunkIds => _placedChunkIds;
        public float GeneratedUntil => _generatedUntil;

        /// <summary>Appends chunks until the course reaches at least <paramref name="x"/>.</summary>
        public void EnsureGeneratedUntil(float x)
        {
            if (!Procedural)
            {
                return;
            }

            while (_generatedUntil < x)
            {
                var chunk = PickChunk(_generatedUntil);
                float origin = _generatedUntil + JunctionSpacing(chunk, _generatedUntil);
                Place(chunk, origin);
                _generatedUntil = origin + chunk.length;
            }
        }

        private ChunkDef PickChunk(float atDistance)
        {
            if (_placedChunkIds.Count == 0)
            {
                return ChunkLibrary.Find(_chunks, ChunkLibrary.IntroChunkId) ?? _chunks[0];
            }

            var tierWeights = Difficulty.TierWeightsAt(atDistance);
            var candidates = ChunkLibrary.OfTier(_chunks, _random.WeightedIndex(tierWeights));
            if (candidates.Count == 0)
            {
                candidates = new List<ChunkDef>(_chunks);
            }

            // Never repeat the previous chunk unless it's the only option.
            if (candidates.Count > 1)
            {
                candidates.RemoveAll(c => c.id == _lastChunkId);
            }

            return _random.Pick(candidates);
        }

        /// <summary>
        /// Extra space needed before <paramref name="chunk"/> so the climb or drop from the previous
        /// chimney to the chunk's first chimney stays within <see cref="TailwindTuning.maxTransitionSlope"/>.
        /// </summary>
        private float JunctionSpacing(ChunkDef chunk, float originX)
        {
            if (_chimneys.Count == 0 || chunk.chimneys.Length == 0)
            {
                return 0f;
            }

            var previous = _chimneys[_chimneys.Count - 1];
            var first = chunk.chimneys[0];
            float firstX = originX + first.x;
            float gap = Difficulty.GapAt(_tuning, firstX) * first.gapScale;
            float center = ClampCenter(first.gapCenter, gap);

            float open = firstX - previous.Right;
            float required = Math.Abs(center - previous.gapCenter) / _tuning.maxTransitionSlope;
            if (SafeOverlap(_tuning, previous.GapBottom, previous.GapTop, center - gap * 0.5f, center + gap * 0.5f) < _tuning.HopHeight + 0.1f)
            {
                required = Math.Max(required, _tuning.minTransitionOpen);
            }

            return Math.Max(0f, required - open);
        }

        /// <summary>How much of the glider's safe height range two gaps share (negative when they don't overlap).</summary>
        public static float SafeOverlap(TailwindTuning t, float bottomA, float topA, float bottomB, float topB)
        {
            float low = Math.Max(bottomA, bottomB) + t.gliderRadius;
            float high = Math.Min(topA, topB) - t.gliderRadius;
            return high - low;
        }

        private float ClampCenter(float center, float gap)
        {
            float minCenter = _tuning.groundY + _tuning.minGapMargin + gap * 0.5f;
            float maxCenter = _tuning.ceilingY - _tuning.minGapMargin - gap * 0.5f;
            return Math.Max(minCenter, Math.Min(maxCenter, center));
        }

        private void Place(ChunkDef chunk, float originX)
        {
            _placedChunkIds.Add(chunk.id);
            _lastChunkId = chunk.id;

            foreach (var c in chunk.chimneys)
            {
                float x = originX + c.x;
                float gap = Difficulty.GapAt(_tuning, x) * c.gapScale;
                _chimneys.Add(new Chimney
                {
                    x = x,
                    width = _tuning.chimneyWidth,
                    gapHeight = gap,
                    gapCenter = ClampCenter(c.gapCenter, gap),
                });
            }

            foreach (var s in chunk.streams)
            {
                _streams.Add(new WindStream
                {
                    xStart = originX + s.xStart,
                    xEnd = originX + s.xEnd,
                    yCenter = s.yCenter,
                    height = _tuning.streamHeight,
                });
            }

            foreach (var l in chunk.letters)
            {
                _letters.Add(new Letter { x = originX + l.x, y = l.y });
            }
        }
    }
}
