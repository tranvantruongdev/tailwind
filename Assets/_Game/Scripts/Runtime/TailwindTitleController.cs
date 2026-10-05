using Cysharp.Threading.Tasks;
using PrimeTween;
using Tailwind.Art;
using Tailwind.Core;
using Tailwind.View;
using Template.Core.Save;
using Template.Core.Settings;
using Template.Feel;
using Template.Game.Flow;
using Template.Infra;
using Template.Infra.Settings;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Tailwind
{
    /// <summary>
    /// Title screen. It doubles as the tutorial: "Tap to fly" (or a tap anywhere) starts a run. Shows the latest
    /// story beat in handwriting and the best result, and opens the shared settings popup from the gear.
    /// </summary>
    public sealed class TailwindTitleController : MonoBehaviour
    {
        private ScreenStack _stack;
        private SettingsPanelView _settingsView;
        private SettingsPresenter _settingsPresenter;
        private Transform _glider;
        private Transform _envelope;
        private RectTransform _play;
        private bool _leaving;
        private string _builtIn; // the language the labels were built in
        private static bool _languageChecked;

        private void Start()
        {
            if (!BootGuard.EnsureBooted())
            {
                return;
            }

            var camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = 5.6f;
            camera.transform.position = new Vector3(0f, 5.5f, -10f);
            ParallaxBackground.Create(camera, TailwindTuning.Default().groundY);

            // A glider bobbing in the sky behind the title.
            var glider = ProceduralSprites.Spawn("Title glider", null, ProceduralSprites.Glider, Palette.Glider, 20);
            _glider = glider.transform;
            _glider.position = new Vector3(-0.8f, 6.2f, 0f);
            Tween.PositionY(_glider, 6.6f, 1.2f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
            Tween.Rotation(_glider, new Vector3(0f, 0f, 8f), 1.2f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);

            BuildUi();
            Audio.MusicLoop.Play();
        }

        private void BuildUi()
        {
            var theme = UiTheme.Current;
            var saves = Services.Get<SaveService>();
            if (!_languageChecked)
            {
                _languageChecked = true; // once per app run, before any label is built
                if (saves.LoadedFrom == "Fresh")
                {
                    Services.Get<SettingsService>().Current.language = Loc.FromSystem(); // a first launch speaks the device's language
                }
            }

            _builtIn = Loc.Language;
            var save = saves.Data;
            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas("Title UI");
            _stack = canvas.gameObject.AddComponent<ScreenStack>();
            _stack.RootBackPressed += Application.Quit;
            var safe = UiFactory.CreateSafeArea(canvas.transform);

            var logo = UiFactory.CreateText(safe, "Tailwind", 176, Vector2.zero, new Vector2(1000, 230), TextAlignmentOptions.Center, UiFont.Display);
            UiFactory.Place(logo, new Vector2(0.5f, 1f), new Vector2(0f, -330f));
            logo.color = theme.paper;
            if (theme.displayShadow != null)
            {
                logo.fontSharedMaterial = theme.displayShadow;
            }

            // A letter riding along with the logo.
            var envelope = UiFactory.CreateImage(logo.rectTransform, ProceduralSprites.Envelope, new Vector2(345f, 78f), new Vector2(88f, 64f), Palette.Letter);
            _envelope = envelope.transform;
            _envelope.localRotation = Quaternion.Euler(0f, 0f, -12f);
            if (!JuiceFx.ReduceMotion)
            {
                Tween.LocalPositionY(_envelope, 92f, 1.4f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
            }

            var tagline = UiFactory.CreateText(safe, Loc.T(StoryBeats.TextFor(save.storyIndex)), 58, Vector2.zero, new Vector2(900, 170),
                TextAlignmentOptions.Center, UiFont.Story);
            UiFactory.Place(tagline, new Vector2(0.5f, 1f), new Vector2(0f, -510f));
            tagline.color = new Color(1f, 1f, 1f, 0.9f);

            var gear = UiFactory.CreateIconButton(safe, theme.iconSettings, Vector2.zero, 104, () => OpenSettings().Forget(), ButtonStyle.Glass, "Settings");
            UiFactory.Place(gear, new Vector2(1f, 1f), new Vector2(-92f, -92f));

            if (save.totalRuns > 0)
            {
                var chip = UiFactory.CreateRect("Best", safe);
                chip.sizeDelta = new Vector2(540f, 84f);
                UiFactory.Place(chip, new Vector2(0.5f, 0f), new Vector2(0f, 560f));
                UiFactory.CreateRounded(chip, Vector2.zero, chip.sizeDelta, new Color(0f, 0f, 0f, 0.32f), 42);
                UiFactory.CreateImage(chip, theme.iconTrophy, new Vector2(-205f, 0f), new Vector2(52f, 52f), theme.accent).name = "Trophy";
                UiFactory.CreateText(chip, Loc.F("Best {0}  ·  {1} m", save.bestScore, save.bestDistance), 44, new Vector2(30f, 3f), new Vector2(430f, 84f))
                    .color = theme.textOnDark;
            }

            // The breathing lives on a container so it doesn't fight the button's own press animation.
            _play = UiFactory.CreateRect("Play", safe);
            _play.sizeDelta = new Vector2(620f, 160f);
            UiFactory.Place(_play, new Vector2(0.5f, 0f), new Vector2(0f, 360f));
            UiFactory.CreateButton(_play, Loc.T("Tap to fly"), Vector2.zero, new Vector2(620f, 160f), StartGame, ButtonStyle.Primary, theme.iconPlay);

            _settingsView = SettingsPanelView.Create(canvas.transform);
            _settingsView.CloseRequested += () => CloseSettings().Forget();

            Appear(logo.rectTransform, 0f);
            Appear(tagline.rectTransform, 0.08f);
            Appear(_play, 0.16f);
            if (!JuiceFx.ReduceMotion)
            {
                Tween.Scale(_play, 1.04f, 1f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, startDelay: 0.6f);
            }
        }

        /// <summary>Fades in while rising 24 px; skipped with reduce motion.</summary>
        private static void Appear(RectTransform rect, float delay)
        {
            if (JuiceFx.ReduceMotion)
            {
                return;
            }

            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var to = rect.anchoredPosition;
            var from = to + new Vector2(0f, -24f);
            rect.anchoredPosition = from;
            Sequence.Create()
                .ChainDelay(delay)
                .Chain(Tween.Custom(group, 0f, 1f, UiTheme.Current.normal, (g, a) => g.alpha = a))
                .Group(Tween.Custom(rect, from, to, UiTheme.Current.normal, (r, p) => r.anchoredPosition = p, Ease.OutCubic));
        }

        private void Update()
        {
            if (_leaving || _stack == null || _stack.Count > 0)
            {
                return;
            }

            bool keyboard = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            bool pointer = Pointer.current != null && Pointer.current.press.wasPressedThisFrame &&
                           !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            if (keyboard || pointer)
            {
                StartGame();
            }
        }

        private void StartGame()
        {
            if (_leaving || _stack == null || _stack.Count > 0)
            {
                return;
            }

            _leaving = true;
            Services.Get<GameFlow>().GoToAsync(AppState.Game).Forget();
        }

        private async UniTaskVoid OpenSettings()
        {
            var settings = Services.Get<SettingsService>();
            _settingsPresenter = new SettingsPresenter(settings.Current);
            _settingsPresenter.SettingsChanged += _ => settings.Apply();
            _settingsPresenter.Attach(_settingsView);
            await _stack.PushAsync(_settingsView);
        }

        private async UniTaskVoid CloseSettings()
        {
            _settingsPresenter?.Dispose();
            _settingsPresenter = null;
            var settings = Services.Get<SettingsService>();
            settings.Commit();
            await _stack.PopAsync();
            if (settings.Current.language != _builtIn && !_leaving)
            {
                _leaving = true;
                Services.Get<GameFlow>().GoToAsync(AppState.Title).Forget(); // every label is built once: rebuild in the new language
            }
        }

        private void OnDestroy()
        {
            _settingsPresenter?.Dispose();
            if (_glider != null)
            {
                Tween.StopAll(_glider);
            }

            if (_envelope != null)
            {
                Tween.StopAll(_envelope);
            }

            if (_play != null)
            {
                Tween.StopAll(_play);
            }
        }
    }
}
