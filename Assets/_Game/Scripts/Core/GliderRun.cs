using System;
using System.Collections.Generic;

namespace Tailwind.Core
{
    public enum RunEventType
    {
        Flap,
        EnteredStream,
        LeftStream,
        ComboChanged,
        LetterCollected,
        NearMiss,
        StoryUnlocked,
        Crashed,
    }

    /// <summary>Something the view should react to (sound, particles, haptics, text).</summary>
    public struct RunEvent
    {
        public RunEventType type;
        public float x;
        public float y;

        /// <summary>Combo for ComboChanged, points for LetterCollected/NearMiss, story index for StoryUnlocked.</summary>
        public int value;

        public override string ToString() => $"{type}({value}) at {x:0.00},{y:0.00}";
    }

    /// <summary>
    /// One run of Tailwind, simulated in pure C#. The view calls <see cref="Flap"/> on input and
    /// <see cref="Step"/> with a fixed timestep, then plays the returned events. Because nothing here
    /// touches Unity, runs are deterministic and a bot can play thousands of them in a test.
    /// </summary>
    public sealed class GliderRun
    {
        private const float GenerateAhead = 30f;

        private readonly TailwindTuning _t;
        private readonly HashSet<int> _collectedLetters = new HashSet<int>();
        private bool _flapQueued;
        private float _streamTime;
        private float _graceLeft;
        private int _overlapChimney = -1;
        private float _overlapClearance = float.MaxValue;
        private int _nextChimney;
        private int _storyIndex = -1;

        public GliderRun(TailwindTuning tuning, Course course)
        {
            _t = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Course = course ?? throw new ArgumentNullException(nameof(course));
            Y = (_t.groundY + _t.ceilingY) * 0.5f;
            Combo = 1;
            Speed = Difficulty.SpeedAt(_t, 0f);
            Course.EnsureGeneratedUntil(GenerateAhead);
        }

        public GliderRun(TailwindTuning tuning, ulong seed)
            : this(tuning, new Course(tuning, seed))
        {
        }

        public Course Course { get; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public float VelocityY { get; private set; }
        public float Speed { get; private set; }
        public bool InStream { get; private set; }
        public int Combo { get; private set; }
        public int DeliveryPoints { get; private set; }
        public int LettersCollected => _collectedLetters.Count;
        public int NearMisses { get; private set; }
        public bool Crashed { get; private set; }
        public float ElapsedSeconds { get; private set; }

        public int Distance => (int)X;
        public int Score => Distance + DeliveryPoints;

        /// <summary>Highest story beat reached this run, or -1.</summary>
        public int StoryIndex => _storyIndex;

        public bool IsLetterCollected(int index) => _collectedLetters.Contains(index);

        public void Flap()
        {
            if (!Crashed)
            {
                _flapQueued = true;
            }
        }

        public void Step(float dt, List<RunEvent> events)
        {
            if (Crashed || dt <= 0f)
            {
                return;
            }

            ElapsedSeconds += dt;
            Course.EnsureGeneratedUntil(X + GenerateAhead);

            bool inStream = FindStream(X, Y);
            if (_flapQueued)
            {
                _flapQueued = false;
                VelocityY = _t.flapVelocity * (inStream ? _t.streamFlapScale : 1f);
                Emit(events, RunEventType.Flap, 0);
            }

            float gravity = _t.gravity * (inStream ? _t.streamGravityScale : 1f);
            VelocityY = Math.Max(VelocityY + gravity * dt, -_t.maxFallSpeed);
            Speed = Difficulty.SpeedAt(_t, X) * (inStream ? _t.streamSpeedScale : 1f);

            X += Speed * dt;
            Y += VelocityY * dt;
            if (Y + _t.gliderRadius > _t.ceilingY)
            {
                Y = _t.ceilingY - _t.gliderRadius;
                VelocityY = Math.Min(0f, VelocityY);
            }

            UpdateStreamAndCombo(FindStream(X, Y), dt, events);
            CollectLetters(events);
            UpdateStory(events);

            if (CheckCrash())
            {
                Crashed = true;
                Emit(events, RunEventType.Crashed, Score);
                return;
            }

            UpdateNearMiss(events);
        }

        private bool FindStream(float x, float y)
        {
            var streams = Course.Streams;
            for (int i = 0; i < streams.Count; i++)
            {
                if (streams[i].Contains(x, y))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateStreamAndCombo(bool nowInStream, float dt, List<RunEvent> events)
        {
            if (nowInStream && !InStream)
            {
                // Stream time carries over brief exits; it only resets when the combo lapses.
                Emit(events, RunEventType.EnteredStream, Combo);
            }
            else if (!nowInStream && InStream)
            {
                Emit(events, RunEventType.LeftStream, Combo);
                _graceLeft = _t.comboGraceSeconds;
            }

            InStream = nowInStream;

            if (InStream)
            {
                _streamTime += dt;
                while (_streamTime >= _t.comboStepSeconds)
                {
                    _streamTime -= _t.comboStepSeconds;
                    if (Combo < _t.maxCombo)
                    {
                        Combo++;
                        Emit(events, RunEventType.ComboChanged, Combo);
                    }
                }
            }
            else if (Combo > 1)
            {
                _graceLeft -= dt;
                if (_graceLeft <= 0f)
                {
                    Combo = 1;
                    _streamTime = 0f;
                    Emit(events, RunEventType.ComboChanged, Combo);
                }
            }
        }

        private void CollectLetters(List<RunEvent> events)
        {
            var letters = Course.Letters;
            float reach = _t.gliderRadius + _t.letterPickupRadius;
            for (int i = 0; i < letters.Count; i++)
            {
                var letter = letters[i];
                if (letter.x < X - reach)
                {
                    continue;
                }

                if (letter.x > X + reach)
                {
                    break; // letters are generated in increasing x order
                }

                float dx = letter.x - X;
                float dy = letter.y - Y;
                if (dx * dx + dy * dy <= reach * reach && _collectedLetters.Add(i))
                {
                    int points = _t.letterPoints * Combo;
                    DeliveryPoints += points;
                    Emit(events, RunEventType.LetterCollected, points);
                }
            }
        }

        private void UpdateStory(List<RunEvent> events)
        {
            while (_storyIndex + 1 < StoryBeats.Count && X >= StoryBeats.All[_storyIndex + 1].distance)
            {
                _storyIndex++;
                Emit(events, RunEventType.StoryUnlocked, _storyIndex);
            }
        }

        private bool CheckCrash()
        {
            float r = _t.gliderRadius;
            if (Y - r <= _t.groundY)
            {
                return true;
            }

            var chimneys = Course.Chimneys;
            while (_nextChimney < chimneys.Count && chimneys[_nextChimney].Right < X - r)
            {
                _nextChimney++;
            }

            for (int i = _nextChimney; i < chimneys.Count; i++)
            {
                var c = chimneys[i];
                if (c.x > X + r)
                {
                    break;
                }

                // Chimney below the gap, lantern line above it.
                if (CircleHitsRect(X, Y, r, c.x, _t.groundY, c.Right, c.GapBottom) ||
                    CircleHitsRect(X, Y, r, c.x, c.GapTop, c.Right, _t.ceilingY))
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateNearMiss(List<RunEvent> events)
        {
            float r = _t.gliderRadius;
            var chimneys = Course.Chimneys;

            if (_overlapChimney >= 0 && chimneys[_overlapChimney].Right < X - r)
            {
                if (_overlapClearance <= _t.nearMissDistance)
                {
                    NearMisses++;
                    DeliveryPoints += _t.nearMissPoints;
                    Emit(events, RunEventType.NearMiss, _t.nearMissPoints);
                }

                _overlapChimney = -1;
                _overlapClearance = float.MaxValue;
            }

            for (int i = _nextChimney; i < chimneys.Count; i++)
            {
                var c = chimneys[i];
                if (c.x > X + r)
                {
                    break;
                }

                if (c.Right >= X - r)
                {
                    if (_overlapChimney != i)
                    {
                        _overlapChimney = i;
                        _overlapClearance = float.MaxValue;
                    }

                    float clearance = Math.Min(Y - r - c.GapBottom, c.GapTop - (Y + r));
                    _overlapClearance = Math.Min(_overlapClearance, clearance);
                    break;
                }
            }
        }

        private static bool CircleHitsRect(float cx, float cy, float r, float left, float bottom, float right, float top)
        {
            if (top <= bottom)
            {
                return false;
            }

            float nearestX = Math.Max(left, Math.Min(cx, right));
            float nearestY = Math.Max(bottom, Math.Min(cy, top));
            float dx = cx - nearestX;
            float dy = cy - nearestY;
            return dx * dx + dy * dy < r * r;
        }

        private void Emit(List<RunEvent> events, RunEventType type, int value)
        {
            events?.Add(new RunEvent { type = type, x = X, y = Y, value = value });
        }
    }
}
