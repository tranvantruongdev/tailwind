using System.Linq;
using NUnit.Framework;

namespace Tailwind.Core.Tests
{
    public class CourseAndDifficultyTests
    {
        private static readonly TailwindTuning T = TailwindTuning.Default();

        [Test]
        public void Speed_ramps_with_distance_and_caps()
        {
            Assert.AreEqual(3f, Difficulty.SpeedAt(T, 0f), 1e-5);
            Assert.AreEqual(3.5f, Difficulty.SpeedAt(T, 100f), 1e-5);
            Assert.AreEqual(5f, Difficulty.SpeedAt(T, 2000f), 1e-5);
        }

        [Test]
        public void Gap_shrinks_from_start_to_end()
        {
            Assert.AreEqual(3.2f, Difficulty.GapAt(T, 0f), 1e-5);
            Assert.AreEqual(2.9f, Difficulty.GapAt(T, 300f), 1e-5);
            Assert.AreEqual(2.6f, Difficulty.GapAt(T, 600f), 1e-5);
            Assert.AreEqual(2.6f, Difficulty.GapAt(T, 5000f), 1e-5);
        }

        [Test]
        public void Tier_weights_move_from_easy_to_hard()
        {
            CollectionAssert.AreEqual(new[] { 1.0, 0.0, 0.0 }, Difficulty.TierWeightsAt(10f));
            Assert.Greater(Difficulty.TierWeightsAt(700f)[2], Difficulty.TierWeightsAt(400f)[2]);
        }

        [Test]
        public void Same_seed_builds_the_same_course()
        {
            var a = new Course(T, 777);
            var b = new Course(T, 777);
            a.EnsureGeneratedUntil(800f);
            b.EnsureGeneratedUntil(800f);

            CollectionAssert.AreEqual(a.PlacedChunkIds, b.PlacedChunkIds);
            Assert.AreEqual(a.Chimneys.Count, b.Chimneys.Count);
            for (int i = 0; i < a.Chimneys.Count; i++)
            {
                Assert.AreEqual(a.Chimneys[i].x, b.Chimneys[i].x);
                Assert.AreEqual(a.Chimneys[i].gapCenter, b.Chimneys[i].gapCenter);
            }
        }

        [Test]
        public void Different_seeds_build_different_courses()
        {
            var a = new Course(T, 1);
            var b = new Course(T, 2);
            a.EnsureGeneratedUntil(800f);
            b.EnsureGeneratedUntil(800f);
            CollectionAssert.AreNotEqual(a.PlacedChunkIds, b.PlacedChunkIds);
        }

        [Test]
        public void Course_starts_with_the_intro_chunk_after_the_runway()
        {
            var course = new Course(T, 42);
            course.EnsureGeneratedUntil(50f);
            Assert.AreEqual(ChunkLibrary.IntroChunkId, course.PlacedChunkIds[0]);
            Assert.GreaterOrEqual(course.Chimneys[0].x, T.startRunway);
        }

        [Test]
        public void Generated_layout_is_always_well_formed()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var course = new Course(T, seed);
                course.EnsureGeneratedUntil(1200f);

                for (int i = 0; i < course.Chimneys.Count; i++)
                {
                    var c = course.Chimneys[i];
                    Assert.GreaterOrEqual(c.gapHeight, T.gapEnd - 1e-4, $"seed {seed}: gap too small");
                    Assert.GreaterOrEqual(c.GapBottom, T.groundY + T.minGapMargin - 1e-4, $"seed {seed}: gap too low");
                    Assert.LessOrEqual(c.GapTop, T.ceilingY - T.minGapMargin + 1e-4, $"seed {seed}: gap too high");
                    if (i > 0)
                    {
                        Assert.Greater(c.x, course.Chimneys[i - 1].Right, $"seed {seed}: chimneys overlap");
                    }
                }

                for (int i = 1; i < course.PlacedChunkIds.Count; i++)
                {
                    Assert.AreNotEqual(course.PlacedChunkIds[i - 1], course.PlacedChunkIds[i], $"seed {seed}: same chunk twice in a row");
                }

                var letterXs = course.Letters.Select(l => l.x).ToList();
                CollectionAssert.IsOrdered(letterXs, $"seed {seed}: letters out of order");
            }
        }

        [Test]
        public void Every_chunk_obeys_the_fairness_slope()
        {
            foreach (var chunk in ChunkLibrary.Default)
            {
                for (int i = 1; i < chunk.chimneys.Length; i++)
                {
                    var a = chunk.chimneys[i - 1];
                    var b = chunk.chimneys[i];
                    float open = b.x - (a.x + T.chimneyWidth);
                    float change = System.Math.Abs(b.gapCenter - a.gapCenter);
                    Assert.LessOrEqual(change, T.maxTransitionSlope * open + 1e-3f,
                        $"{chunk.id}: chimneys {i - 1}→{i} change {change:0.00} m over {open:0.00} m of open air");
                }

                Assert.Greater(chunk.length, chunk.chimneys.Length == 0 ? 0f : chunk.chimneys[chunk.chimneys.Length - 1].x + T.chimneyWidth,
                    $"{chunk.id}: last chimney sticks out of the chunk");
            }
        }

        [Test]
        public void Junctions_between_chunks_obey_the_fairness_slope()
        {
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var course = new Course(T, seed);
                course.EnsureGeneratedUntil(1200f);
                for (int i = 1; i < course.Chimneys.Count; i++)
                {
                    var a = course.Chimneys[i - 1];
                    var b = course.Chimneys[i];
                    float open = b.x - a.Right;
                    float change = System.Math.Abs(b.gapCenter - a.gapCenter);
                    Assert.LessOrEqual(change, T.maxTransitionSlope * open + 1e-3f,
                        $"seed {seed}: chimney at {b.x:0.0} changes {change:0.00} m over {open:0.00} m");

                    // Close chimneys must share at least one flap's lift of safe height; otherwise there must be
                    // room to drop and recover. (The generator adds a 0.1 m margin on top at chunk junctions.)
                    float overlap = Course.SafeOverlap(T, a.GapBottom, a.GapTop, b.GapBottom, b.GapTop);
                    Assert.IsTrue(overlap >= T.HopHeight - 1e-3f || open >= T.minTransitionOpen - 1e-3f,
                        $"seed {seed}: chimney at {b.x:0.0} shares only {overlap:0.00} m of safe height with {open:0.00} m to adjust");
                }
            }
        }

        [Test]
        public void Hard_chunks_only_appear_later()
        {
            var course = new Course(T, 9);
            course.EnsureGeneratedUntil(100f);
            foreach (string id in course.PlacedChunkIds)
            {
                Assert.AreEqual(0, ChunkLibrary.Find(ChunkLibrary.Default, id).tier, $"{id} is too hard for the first 100 m");
            }
        }
    }
}
