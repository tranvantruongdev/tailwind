namespace Tailwind.Core
{
    /// <summary>
    /// A simple pilot used by tests to prove every generated course is passable, and later to
    /// tune difficulty. It flies like a good player: it picks a target height (the next gap, or a
    /// wind stream running between gaps) and taps at the bottom of a band centred on that target,
    /// so each hop peaks just above it.
    /// </summary>
    public static class AutopilotBot
    {
        public static bool ShouldFlap(GliderRun run, TailwindTuning t)
        {
            if (run.VelocityY > 0f)
            {
                return false;
            }

            // Height gained by one flap: v² / 2g, with weaker flaps and lower gravity inside streams.
            float v = t.flapVelocity * (run.InStream ? t.streamFlapScale : 1f);
            float g = -t.gravity * (run.InStream ? t.streamGravityScale : 1f);
            float hopHeight = v * v / (2f * g);

            return run.Y <= TargetY(run, t) - hopHeight * 0.5f;
        }

        public static float TargetY(GliderRun run, TailwindTuning t)
        {
            var chimneys = run.Course.Chimneys;
            for (int i = 0; i < chimneys.Count; i++)
            {
                var c = chimneys[i];
                if (c.Right < run.X - 0.3f)
                {
                    continue;
                }

                // Over (or just before) a chimney, lean toward the next gap's height while staying
                // in this gap's safe band, so a big climb or drop afterwards starts early.
                bool overChimney = run.X >= c.x - 1f;
                if (overChimney && i + 1 < chimneys.Count)
                {
                    var next = chimneys[i + 1];
                    float margin = t.gliderRadius + 0.7f;
                    float low = c.GapBottom + margin;
                    float high = c.GapTop - margin;
                    if (low < high)
                    {
                        return next.gapCenter < low ? low : next.gapCenter > high ? high : next.gapCenter;
                    }
                }

                // Ride a stream if one runs here and its height is safe for the coming gap.
                var streams = run.Course.Streams;
                for (int s = 0; s < streams.Count; s++)
                {
                    var stream = streams[s];
                    bool here = run.X >= stream.xStart && run.X <= stream.xEnd;
                    bool safe = stream.yCenter > c.GapBottom + 0.5f && stream.yCenter < c.GapTop - 0.5f;
                    if (here && safe)
                    {
                        return stream.yCenter;
                    }
                }

                return c.gapCenter;
            }

            return 5.5f;
        }
    }
}
