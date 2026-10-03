using System.Collections.Generic;
using Tailwind.Art;
using Tailwind.Core;
using UnityEngine;

namespace Tailwind.View
{
    /// <summary>
    /// Draws the part of the course near the camera: chimneys with hanging lantern lines,
    /// wind streams with drifting streaks, and bobbing letters. Views are pooled and recycled.
    /// </summary>
    public sealed class CourseView : MonoBehaviour
    {
        private const float Margin = 3f;

        private TailwindTuning _tuning;
        private readonly Dictionary<int, ChimneyVisual> _chimneys = new Dictionary<int, ChimneyVisual>();
        private readonly Dictionary<int, StreamVisual> _streams = new Dictionary<int, StreamVisual>();
        private readonly Dictionary<int, SpriteRenderer> _letters = new Dictionary<int, SpriteRenderer>();
        private readonly Stack<ChimneyVisual> _freeChimneys = new Stack<ChimneyVisual>();
        private readonly Stack<StreamVisual> _freeStreams = new Stack<StreamVisual>();
        private readonly Stack<SpriteRenderer> _freeLetters = new Stack<SpriteRenderer>();
        private readonly List<int> _toRemove = new List<int>();

        private sealed class ChimneyVisual
        {
            public GameObject Root;
            public SpriteRenderer Stack;
            public SpriteRenderer Cap;
            public SpriteRenderer Rope;
            public SpriteRenderer[] Lanterns;
        }

        private sealed class StreamVisual
        {
            public GameObject Root;
            public SpriteRenderer Band;
            public SpriteRenderer[] Streaks;
            public float Length;
        }

        public static CourseView Create(Transform parent, TailwindTuning tuning)
        {
            var go = new GameObject("Course");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CourseView>();
            view._tuning = tuning;
            return view;
        }

        /// <summary>Recycles everything, e.g. when a new run starts.</summary>
        public void Clear()
        {
            foreach (var c in _chimneys.Values) Release(c);
            foreach (var s in _streams.Values) Release(s);
            foreach (var l in _letters.Values) Release(l);
            _chimneys.Clear();
            _streams.Clear();
            _letters.Clear();
        }

        public void Sync(GliderRun run, float cameraLeft, float cameraRight, float time)
        {
            float left = cameraLeft - Margin;
            float right = cameraRight + Margin;
            var course = run.Course;

            // Chimneys
            for (int i = 0; i < course.Chimneys.Count; i++)
            {
                var c = course.Chimneys[i];
                if (c.Right < left || c.x > right)
                {
                    continue;
                }

                if (!_chimneys.ContainsKey(i))
                {
                    _chimneys[i] = Place(c);
                }
            }

            Recycle(_chimneys, i => course.Chimneys[i].Right < left, Release);

            // Streams
            for (int i = 0; i < course.Streams.Count; i++)
            {
                var s = course.Streams[i];
                if (s.xEnd < left || s.xStart > right)
                {
                    continue;
                }

                if (!_streams.TryGetValue(i, out var visual))
                {
                    visual = Place(s);
                    _streams[i] = visual;
                }

                AnimateStreaks(visual, time);
            }

            Recycle(_streams, i => course.Streams[i].xEnd < left, Release);

            // Letters
            for (int i = 0; i < course.Letters.Count; i++)
            {
                var l = course.Letters[i];
                if (l.x < left || l.x > right)
                {
                    continue;
                }

                if (!_letters.TryGetValue(i, out var letter))
                {
                    letter = _freeLetters.Count > 0 ? _freeLetters.Pop() : ProceduralSprites.Spawn("Letter", transform, ProceduralSprites.Envelope, Color.white, 12);
                    letter.gameObject.SetActive(true);
                    _letters[i] = letter;
                }

                bool collected = run.IsLetterCollected(i);
                letter.enabled = !collected;
                float bob = Mathf.Sin(time * 3f + i) * 0.08f;
                letter.transform.position = new Vector3(l.x, l.y + bob, 0f);
                letter.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 2f + i) * 8f);
            }

            Recycle(_letters, i => course.Letters[i].x < left, Release);
        }

        private ChimneyVisual Place(Chimney c)
        {
            var v = _freeChimneys.Count > 0 ? _freeChimneys.Pop() : CreateChimney();
            v.Root.SetActive(true);
            float cx = c.x + c.width * 0.5f;

            // Brick stack from the ground to the gap bottom, with a darker cap.
            float stackHeight = c.GapBottom - _tuning.groundY + 0.5f;
            v.Stack.transform.position = new Vector3(cx, _tuning.groundY - 0.5f + stackHeight * 0.5f, 0f);
            v.Stack.transform.localScale = new Vector3(c.width, stackHeight, 1f);
            v.Cap.transform.position = new Vector3(cx, c.GapBottom - 0.1f, 0f);
            v.Cap.transform.localScale = new Vector3(c.width + 0.2f, 0.2f, 1f);

            // Rope from the sky down to the gap top, with lanterns at the bottom.
            float ropeHeight = _tuning.ceilingY + 1f - c.GapTop;
            v.Rope.transform.position = new Vector3(cx, c.GapTop + ropeHeight * 0.5f, 0f);
            v.Rope.transform.localScale = new Vector3(c.width, ropeHeight, 1f);
            for (int i = 0; i < v.Lanterns.Length; i++)
            {
                float lx = c.x + (i + 0.5f) * c.width / v.Lanterns.Length;
                v.Lanterns[i].transform.position = new Vector3(lx, c.GapTop + 0.12f, 0f);
            }

            return v;
        }

        private StreamVisual Place(WindStream s)
        {
            var v = _freeStreams.Count > 0 ? _freeStreams.Pop() : CreateStream();
            v.Root.SetActive(true);
            v.Length = s.xEnd - s.xStart;
            v.Root.transform.position = new Vector3(s.xStart, s.yCenter, 0f);
            v.Band.transform.localPosition = new Vector3(v.Length * 0.5f, 0f, 0f);
            v.Band.transform.localScale = new Vector3(v.Length, s.height, 1f);
            return v;
        }

        private static void AnimateStreaks(StreamVisual v, float time)
        {
            for (int i = 0; i < v.Streaks.Length; i++)
            {
                // Streaks drift backwards through the band so the wind reads as moving air.
                float phase = Mathf.Repeat(-time * 1.5f + i * (v.Length / v.Streaks.Length), v.Length);
                float lane = ((i % 3) - 1) * 0.3f;
                v.Streaks[i].transform.localPosition = new Vector3(phase, lane, 0f);
            }
        }

        private ChimneyVisual CreateChimney()
        {
            var root = new GameObject("Chimney");
            root.transform.SetParent(transform, false);
            var lanterns = new SpriteRenderer[3];
            for (int i = 0; i < lanterns.Length; i++)
            {
                lanterns[i] = ProceduralSprites.Spawn("Lantern", root.transform, ProceduralSprites.Circle, Palette.Lantern, 11);
                lanterns[i].transform.localScale = Vector3.one * 0.22f;
            }

            return new ChimneyVisual
            {
                Root = root,
                Stack = ProceduralSprites.Spawn("Stack", root.transform, ProceduralSprites.Square, Palette.Brick, 8),
                Cap = ProceduralSprites.Spawn("Cap", root.transform, ProceduralSprites.Square, Palette.BrickCap, 9),
                Rope = ProceduralSprites.Spawn("Rope", root.transform, ProceduralSprites.Square, Palette.LanternRope, 8),
                Lanterns = lanterns,
            };
        }

        private StreamVisual CreateStream()
        {
            var root = new GameObject("WindStream");
            root.transform.SetParent(transform, false);
            var streaks = new SpriteRenderer[6];
            for (int i = 0; i < streaks.Length; i++)
            {
                streaks[i] = ProceduralSprites.Spawn("Streak", root.transform, ProceduralSprites.Square, Palette.StreamStreak, 6);
                streaks[i].transform.localScale = new Vector3(0.6f, 0.03f, 1f);
            }

            return new StreamVisual
            {
                Root = root,
                Band = ProceduralSprites.Spawn("Band", root.transform, ProceduralSprites.SoftBand, Palette.Stream, 5),
                Streaks = streaks,
            };
        }

        private void Release(ChimneyVisual v)
        {
            v.Root.SetActive(false);
            _freeChimneys.Push(v);
        }

        private void Release(StreamVisual v)
        {
            v.Root.SetActive(false);
            _freeStreams.Push(v);
        }

        private void Release(SpriteRenderer letter)
        {
            letter.gameObject.SetActive(false);
            _freeLetters.Push(letter);
        }

        private void Recycle<T>(Dictionary<int, T> map, System.Func<int, bool> isBehind, System.Action<T> release)
        {
            _toRemove.Clear();
            foreach (var pair in map)
            {
                if (isBehind(pair.Key))
                {
                    _toRemove.Add(pair.Key);
                }
            }

            foreach (int key in _toRemove)
            {
                release(map[key]);
                map.Remove(key);
            }
        }
    }
}
