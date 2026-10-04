using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Tailwind.Art;
using Tailwind.Audio;
using Tailwind.Core;
using Tailwind.UI;
using Tailwind.View;
using Template.Core.Flow;
using Template.Core.Save;
using Template.Feel;
using Template.Game.Flow;
using Template.Infra;
using Template.Infra.Audio;
using Template.Infra.Device;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Tailwind
{
    /// <summary>
    /// The Game scene: owns one <see cref="GliderRun"/>, steps it at a fixed rate, turns input into
    /// flaps, and turns run events into sound, haptics, particles and HUD updates.
    /// </summary>
    public sealed class RunController : MonoBehaviour
    {
        private const float StepSeconds = 1f / 120f;
        private const float CameraLead = 1.8f;

        private enum Phase
        {
            Ready,
            Flying,
            Paused,
            Crashed,
            Results,
        }

        private readonly TailwindTuning _tuning = TailwindTuning.Default();
        private readonly List<RunEvent> _events = new List<RunEvent>();
        private StateMachine<Phase> _phase;
        private GliderRun _run;
        private Camera _camera;
        private GliderView _glider;
        private CourseView _course;
        private TailwindHud _hud;
        private SfxBank _sfx;
        private AudioService _audio;
        private float _accumulator;
        private int _startStoryIndex;

        private void Start()
        {
            if (!BootGuard.EnsureBooted())
            {
                return;
            }

            _phase = new StateMachine<Phase>(Phase.Ready)
                .Allow(Phase.Ready, Phase.Flying, Phase.Paused)
                .Allow(Phase.Flying, Phase.Paused, Phase.Crashed)
                .Allow(Phase.Paused, Phase.Flying, Phase.Ready)
                .Allow(Phase.Crashed, Phase.Results)
                .Allow(Phase.Results, Phase.Ready);

            _audio = Services.Get<AudioService>();
            _sfx = new SfxBank();

            _camera = Camera.main;
            _camera.orthographic = true;
            _camera.orthographicSize = 5.6f;
            ParallaxBackground.Create(_camera, _tuning.groundY);

            var world = new GameObject("World").transform;
            _course = CourseView.Create(world, _tuning);
            _glider = GliderView.Create(world);

            _hud = TailwindHud.Create();
            _hud.PausePressed += Pause;
            _hud.ResumePressed += Resume;
            _hud.RetryPressed += NewRun;
            _hud.HomePressed += GoHome;
            _hud.ResultsRowShown += row => _audio.PlaySfx(_sfx.ComboUp, 0.35f, 0.9f + row * 0.12f);
            _hud.NewBestShown += () =>
            {
                _audio.PlaySfx(_sfx.Story);
                Haptics.Medium();
            };
            AppLifecycle.BackPressed += OnBack;
            AppLifecycle.PauseChanged += OnAppPause;

            NewRun();
        }

        private void OnDestroy()
        {
            AppLifecycle.BackPressed -= OnBack;
            AppLifecycle.PauseChanged -= OnAppPause;
            Time.timeScale = 1f;
        }

        private void NewRun()
        {
            Time.timeScale = 1f;
            if (_phase.Current != Phase.Ready)
            {
                _phase.Go(Phase.Ready);
            }

            ulong seed = (ulong)DateTime.UtcNow.Ticks;
            _run = new GliderRun(_tuning, seed);
            _accumulator = 0f;
            _startStoryIndex = Services.Get<SaveService>().Data.storyIndex;

            _course.Clear();
            _glider.Reset(new Vector2(_run.X, _run.Y));
            _hud.ShowReady();
            PlaceCamera();
        }

        private void Update()
        {
            if (_run == null)
            {
                return;
            }

            if (TapThisFrame())
            {
                OnTap();
            }

            if (_phase.Current == Phase.Flying)
            {
                _accumulator += Time.deltaTime;
                while (_accumulator >= StepSeconds && !_run.Crashed)
                {
                    _accumulator -= StepSeconds;
                    _events.Clear();
                    _run.Step(StepSeconds, _events);
                    foreach (var e in _events)
                    {
                        Handle(e);
                    }
                }

                _hud.SetDistance(_run.Distance);
                _hud.SetCombo(_run.Combo, _run.ComboHold);
                _hud.SetLetters(_run.LettersCollected);
            }

            if (_phase.Current != Phase.Crashed && _phase.Current != Phase.Results)
            {
                _glider.Follow(new Vector2(_run.X, _run.Y), _run.VelocityY, _run.InStream, Time.deltaTime);
            }

            PlaceCamera();
            float left = _camera.transform.position.x - _camera.orthographicSize * _camera.aspect;
            float right = _camera.transform.position.x + _camera.orthographicSize * _camera.aspect;
            _course.Sync(_run, left, right, Time.time);
        }

        private void PlaceCamera()
        {
            _camera.transform.position = new Vector3(_run.X + CameraLead, (_tuning.groundY + _tuning.ceilingY) * 0.5f, -10f);
        }

        private bool TapThisFrame()
        {
            bool keyboard = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            bool pointer = Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
            if (pointer && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                pointer = false; // tapping a HUD button isn't a flap
            }

            return keyboard || pointer;
        }

        private void OnTap()
        {
            switch (_phase.Current)
            {
                case Phase.Ready:
                    _phase.Go(Phase.Flying);
                    _hud.HideHint();
                    _run.Flap();
                    break;
                case Phase.Flying:
                    _run.Flap();
                    break;
            }
        }

        private void Handle(RunEvent e)
        {
            switch (e.type)
            {
                case RunEventType.Flap:
                    _glider.OnFlap();
                    _audio.PlaySfx(_sfx.Flap, 0.7f, UnityEngine.Random.Range(0.95f, 1.08f));
                    break;
                case RunEventType.EnteredStream:
                    _audio.PlaySfx(_sfx.Whoosh, 0.9f);
                    break;
                case RunEventType.ComboChanged:
                    if (e.value > 1)
                    {
                        _audio.PlaySfx(_sfx.ComboUp, 0.6f, 1f + (e.value - 2) * 0.12f);
                    }

                    break;
                case RunEventType.LetterCollected:
                    _audio.PlaySfx(_sfx.Chime);
                    Haptics.Light();
                    _hud.FloatAt(_camera, new Vector3(e.x, e.y + 0.6f, 0f), $"+{e.value}", Palette.Accent);
                    break;
                case RunEventType.NearMiss:
                    _audio.PlaySfx(_sfx.NearMiss, 0.8f);
                    Haptics.Light();
                    JuiceFx.HitStop(0.04f).Forget();
                    _hud.FloatAt(_camera, new Vector3(e.x, e.y + 0.6f, 0f), $"Close! +{e.value}", Color.white);
                    break;
                case RunEventType.StoryUnlocked:
                    _audio.PlaySfx(_sfx.Story);
                    _hud.ShowStory(StoryBeats.TextFor(e.value));
                    break;
                case RunEventType.Crashed:
                    OnCrashed().Forget();
                    break;
            }
        }

        private async UniTaskVoid OnCrashed()
        {
            _phase.Go(Phase.Crashed);
            _audio.PlaySfx(_sfx.Crash);
            Haptics.Heavy();
            JuiceFx.Shake(_camera.transform, 0.25f, 0.3f);
            _glider.OnCrash();

            // A moment of slow motion, then the results.
            Time.timeScale = JuiceFx.ReduceMotion ? 1f : 0.35f;
            await UniTask.Delay(TimeSpan.FromSeconds(0.6f), ignoreTimeScale: true, cancellationToken: this.GetCancellationTokenOnDestroy());
            Time.timeScale = 1f;
            ShowResults();
        }

        private void ShowResults()
        {
            var save = Services.Get<SaveService>();
            var data = save.Data;
            bool newBest = _run.Score > data.bestScore;
            data.bestScore = Math.Max(data.bestScore, _run.Score);
            data.bestDistance = Math.Max(data.bestDistance, _run.Distance);
            data.totalRuns++;

            string storyLine = null;
            if (_run.StoryIndex > data.storyIndex)
            {
                data.storyIndex = _run.StoryIndex;
                storyLine = StoryBeats.TextFor(_run.StoryIndex);
            }
            else if (_run.StoryIndex > _startStoryIndex)
            {
                storyLine = StoryBeats.TextFor(_run.StoryIndex);
            }

            save.MarkDirty();
            save.Save();

            _phase.Go(Phase.Results);
            int nearMissPoints = _run.NearMisses * _tuning.nearMissPoints;
            _hud.ShowResults(new RunSummary
            {
                distance = _run.Distance,
                letters = _run.LettersCollected,
                letterPoints = _run.DeliveryPoints - nearMissPoints,
                nearMisses = _run.NearMisses,
                nearMissPoints = nearMissPoints,
                bestCombo = _run.BestCombo,
                score = _run.Score,
                best = data.bestScore,
                newBest = newBest,
                storyLine = storyLine,
            });
        }

        private void Pause()
        {
            if (_phase.TryGo(Phase.Paused))
            {
                Time.timeScale = 0f;
                _hud.ShowPause(true);
            }
        }

        private void Resume()
        {
            if (_phase.Current != Phase.Paused)
            {
                return;
            }

            Time.timeScale = 1f;
            _hud.ShowPause(false);
            _phase.Go(Phase.Flying);
        }

        private void OnBack()
        {
            switch (_phase.Current)
            {
                case Phase.Flying:
                case Phase.Ready:
                    Pause();
                    break;
                case Phase.Paused:
                    Resume();
                    break;
                case Phase.Results:
                    GoHome();
                    break;
            }
        }

        private void OnAppPause(bool paused)
        {
            if (paused && _phase.Current == Phase.Flying)
            {
                Pause();
            }
        }

        private void GoHome()
        {
            Time.timeScale = 1f;
            Services.Get<GameFlow>().GoToAsync(AppState.Title).Forget();
        }
    }
}
