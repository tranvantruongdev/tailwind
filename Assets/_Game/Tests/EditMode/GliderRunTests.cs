using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Template.Core.Save;
using Newtonsoft.Json.Linq;

namespace Tailwind.Core.Tests
{
    public class GliderRunTests
    {
        private const float Dt = 1f / 120f;
        private static readonly TailwindTuning T = TailwindTuning.Default();

        private static GliderRun Run(params Chimney[] chimneys) => new GliderRun(T, new Course(T, chimneys));

        private static Chimney Chimney(float x, float gapCenter, float gap = 3f) =>
            new Chimney { x = x, width = 1f, gapCenter = gapCenter, gapHeight = gap };

        private static List<RunEvent> Advance(GliderRun run, float seconds, bool holdHeight = false)
        {
            var events = new List<RunEvent>();
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps && !run.Crashed; i++)
            {
                if (holdHeight && run.VelocityY < -1f && run.Y < 5.5f)
                {
                    run.Flap();
                }

                run.Step(Dt, events);
            }

            return events;
        }

        [Test]
        public void Falls_to_the_ground_without_input()
        {
            var run = Run();
            var events = Advance(run, 2f);

            Assert.IsTrue(run.Crashed);
            Assert.AreEqual(RunEventType.Crashed, events.Last().type);
            // Free fall from 5.5 to ground+radius (4.25 units) at 18 u/s² takes ~0.69 s.
            Assert.AreEqual(0.69f, run.ElapsedSeconds, 0.03f);
        }

        [Test]
        public void Flap_sets_upward_velocity_and_emits_event()
        {
            var run = Run();
            run.Flap();
            var events = new List<RunEvent>();
            run.Step(Dt, events);

            Assert.AreEqual(RunEventType.Flap, events[0].type);
            Assert.Greater(run.VelocityY, 6f);
        }

        [Test]
        public void Ceiling_stops_the_glider_without_crashing()
        {
            var run = Run();
            for (int i = 0; i < 400; i++)
            {
                run.Flap();
                run.Step(Dt, null);
            }

            Assert.IsFalse(run.Crashed);
            Assert.LessOrEqual(run.Y, T.ceilingY - T.gliderRadius + 1e-4);
        }

        [Test]
        public void Hits_a_chimney_below_the_gap()
        {
            var run = Run(Chimney(2f, 8f)); // gap far above the glider's flight line
            var events = Advance(run, 3f, holdHeight: true);

            Assert.IsTrue(run.Crashed);
            Assert.AreEqual(RunEventType.Crashed, events.Last().type);
            Assert.Less(run.X, 3f);
        }

        [Test]
        public void Passes_through_a_gap_and_scores_distance()
        {
            var run = Run(Chimney(3f, 5.5f, 3.5f), Chimney(9f, 5.5f, 3.5f));
            Advance(run, 4f, holdHeight: true);

            Assert.IsFalse(run.Crashed);
            Assert.Greater(run.X, 10f);
            Assert.AreEqual(run.Distance, run.Score - run.DeliveryPoints);
        }

        /// <summary>Taps near the bottom of a stream centred at 5.5; weaker in-stream flaps keep it inside.</summary>
        private static List<RunEvent> RideStream(GliderRun run, float seconds)
        {
            var events = new List<RunEvent>();
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps && !run.Crashed; i++)
            {
                if (run.Y < 5.3f && run.VelocityY < 0f)
                {
                    run.Flap();
                }

                run.Step(Dt, events);
            }

            return events;
        }

        [Test]
        public void Wind_stream_builds_combo_and_speeds_up()
        {
            var stream = new WindStream { xStart = 0f, xEnd = 100f, yCenter = 5.5f, height = 1.2f };
            var run = new GliderRun(T, new Course(T, new Chimney[0], new[] { stream }));
            var events = RideStream(run, 1.6f);

            Assert.IsTrue(run.InStream, "weaker in-stream flaps should keep the glider inside the band");
            Assert.IsTrue(events.Any(e => e.type == RunEventType.EnteredStream));
            Assert.GreaterOrEqual(run.Combo, 3);
            Assert.Greater(run.Speed, Difficulty.SpeedAt(T, run.X) * 1.2f);
        }

        [Test]
        public void Combo_resets_after_grace_period_outside_streams()
        {
            var stream = new WindStream { xStart = 0f, xEnd = 4f, yCenter = 5.5f, height = 1.2f };
            var run = new GliderRun(T, new Course(T, new Chimney[0], new[] { stream }));
            RideStream(run, 0.9f); // ends at x ≈ 3.4, still inside the stream
            Assert.Greater(run.Combo, 1);

            var events = Advance(run, 2.5f, holdHeight: true);
            Assert.AreEqual(1, run.Combo);
            Assert.IsTrue(events.Any(e => e.type == RunEventType.LeftStream));
            Assert.IsTrue(events.Any(e => e.type == RunEventType.ComboChanged && e.value == 1));
        }

        [Test]
        public void Combo_hold_drains_after_leaving_and_best_combo_is_kept()
        {
            var stream = new WindStream { xStart = 0f, xEnd = 4f, yCenter = 5.5f, height = 1.2f };
            var run = new GliderRun(T, new Course(T, new Chimney[0], new[] { stream }));
            Assert.AreEqual(0f, run.ComboHold, "no combo yet");

            var events = RideStream(run, 0.9f); // still inside, combo > 1
            Assert.Greater(run.Combo, 1);
            Assert.AreEqual(1f, run.ComboHold, "held fully while riding");

            events.AddRange(Advance(run, 0.6f, holdHeight: true)); // left the stream at x = 4, part of the grace used
            Assert.IsFalse(run.InStream);
            Assert.That(run.ComboHold, Is.GreaterThan(0f).And.LessThan(1f));
            int peak = events.Where(e => e.type == RunEventType.ComboChanged).Max(e => e.value);

            Advance(run, 2f, holdHeight: true); // grace over
            Assert.AreEqual(1, run.Combo);
            Assert.AreEqual(0f, run.ComboHold);
            Assert.AreEqual(peak, run.BestCombo, "best combo survives the lapse");
        }

        [Test]
        public void Letters_score_ten_times_combo_once()
        {
            // On the free-fall path: after 0.1 s the glider is at x 0.3, y ≈ 5.41.
            var letters = new[] { new Letter { x = 0.3f, y = 5.42f }, new Letter { x = 0.33f, y = 5.42f } };
            var run = new GliderRun(T, new Course(T, new Chimney[0], null, letters));
            var events = Advance(run, 0.4f);

            var collected = events.Where(e => e.type == RunEventType.LetterCollected).ToList();
            Assert.AreEqual(2, collected.Count);
            Assert.AreEqual(20, run.DeliveryPoints);
            Assert.AreEqual(2, run.LettersCollected);
        }

        [Test]
        public void Near_miss_gives_bonus_points()
        {
            bool ShouldFlap(GliderRun r) => r.Y < 5.35f && r.VelocityY < 0f;
            float overlapFrom = 2f - T.gliderRadius;
            float overlapTo = 3f + T.gliderRadius;

            // 1. Fly the input schedule on an empty course; record the lowest and highest point over x 2..3.
            var probe = Run();
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            while (probe.X < 3.5f)
            {
                if (ShouldFlap(probe))
                {
                    probe.Flap();
                }

                probe.Step(Dt, null);
                if (probe.X >= overlapFrom && probe.X <= overlapTo)
                {
                    minY = Math.Min(minY, probe.Y);
                    maxY = Math.Max(maxY, probe.Y);
                }
            }

            // 2. Same inputs, now with a chimney whose gap bottom leaves 0.1 clearance under that lowest point.
            float gapBottom = minY - T.gliderRadius - 0.1f;
            float gapTop = maxY + T.gliderRadius + 1f;
            var run = Run(new Chimney { x = 2f, width = 1f, gapCenter = (gapBottom + gapTop) * 0.5f, gapHeight = gapTop - gapBottom });
            var events = new List<RunEvent>();
            while (run.X < 4f && !run.Crashed)
            {
                if (ShouldFlap(run))
                {
                    run.Flap();
                }

                run.Step(Dt, events);
            }

            Assert.IsFalse(run.Crashed, string.Join(", ", events));
            Assert.AreEqual(1, run.NearMisses);
            Assert.IsTrue(events.Any(e => e.type == RunEventType.NearMiss && e.value == T.nearMissPoints));
        }

        [Test]
        public void Story_beats_unlock_by_distance()
        {
            var run = Run();
            var events = new List<RunEvent>();
            while (run.X < 320f)
            {
                if (run.Y < 5.5f && run.VelocityY < 0f)
                {
                    run.Flap();
                }

                run.Step(Dt, events);
            }

            var beats = events.Where(e => e.type == RunEventType.StoryUnlocked).Select(e => e.value).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1 }, beats);
            Assert.AreEqual(1, run.StoryIndex);
            Assert.AreEqual(1, StoryBeats.IndexForDistance(320));
            Assert.AreEqual(-1, StoryBeats.IndexForDistance(50));
        }

        [Test]
        public void Same_seed_and_inputs_replay_exactly()
        {
            float Play(ulong seed)
            {
                var run = new GliderRun(T, seed);
                for (int i = 0; i < 3000 && !run.Crashed; i++)
                {
                    if (AutopilotBot.ShouldFlap(run, T))
                    {
                        run.Flap();
                    }

                    run.Step(Dt, null);
                }

                return run.X * 1000f + run.Y + run.DeliveryPoints;
            }

            Assert.AreEqual(Play(5), Play(5));
        }

        [Test]
        public void Bot_can_pass_every_generated_course_to_1000m()
        {
            // Guards the chunk library and junction rules: if this fails, the generator made an unfair spot.
            // (Probed separately: 100 seeds × 1000 m, 0 crashes.)
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var run = new GliderRun(T, seed);
                var events = new List<RunEvent>();
                while (!run.Crashed && run.X < 1000f)
                {
                    if (AutopilotBot.ShouldFlap(run, T))
                    {
                        run.Flap();
                    }

                    events.Clear();
                    run.Step(Dt, events);
                }

                Assert.IsFalse(run.Crashed, $"seed {seed} crashed at {run.X:0.0} m (y {run.Y:0.00}), chunks so far: {string.Join(" ", run.Course.PlacedChunkIds)}");
            }
        }

        [Test]
        public void Save_v2_migrates_to_v3_with_tailwind_fields()
        {
            var data = JObject.Parse("{\"saveVersion\": 2, \"bestScore\": 120, \"totalRuns\": 4}");
            SaveSchema.CreateMigrator().Migrate(data);
            var save = data.ToObject<SaveData>();

            Assert.AreEqual(3, save.saveVersion);
            Assert.AreEqual(120, save.bestScore);
            Assert.AreEqual(0, save.bestDistance);
            Assert.AreEqual(-1, save.storyIndex);
        }
    }
}
