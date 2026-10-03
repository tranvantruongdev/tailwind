using Cysharp.Threading.Tasks;
using PrimeTween;
using Tailwind.Art;
using Tailwind.Core;
using Tailwind.View;
using Template.Core.Save;
using Template.Core.Settings;
using Template.Game.Flow;
using Template.Infra;
using Template.Infra.Settings;
using Template.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tailwind
{
    /// <summary>
    /// Title screen. It doubles as the tutorial: "Tap to fly" starts a run. Shows the latest story
    /// beat and best results, and opens the shared settings popup.
    /// </summary>
    public sealed class TailwindTitleController : MonoBehaviour
    {
        private ScreenStack _stack;
        private SettingsPanelView _settingsView;
        private SettingsPresenter _settingsPresenter;
        private Transform _glider;
        private bool _leaving;

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

            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas("Title UI");
            _stack = canvas.gameObject.AddComponent<ScreenStack>();
            _stack.RootBackPressed += Application.Quit;
            var safe = UiFactory.CreateSafeArea(canvas.transform);

            var save = Services.Get<SaveService>().Data;
            var title = UiFactory.CreateText(safe, "Tailwind", 150, new Vector2(0, 700), new Vector2(1000, 220));
            title.fontStyle = FontStyle.Bold;
            UiFactory.CreateText(safe, StoryBeats.TextFor(save.storyIndex), 42, new Vector2(0, 540), new Vector2(940, 140)).fontStyle = FontStyle.Italic;

            if (save.totalRuns > 0)
            {
                UiFactory.CreateText(safe, $"Best {save.bestScore}   ·   {save.bestDistance} m", 48, new Vector2(0, -330), new Vector2(900, 90));
            }

            var hint = UiFactory.CreateText(safe, "Tap to fly", 72, new Vector2(0, -470), new Vector2(900, 120));
            hint.color = Palette.Accent;
            Tween.Scale(hint.transform, 1.08f, 0.6f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);

            UiFactory.CreateButton(safe, "Settings", new Vector2(0, -760), new Vector2(420, 120), () => OpenSettings().Forget());

            _settingsView = SettingsPanelView.Create(safe);
            _settingsView.CloseRequested += () => CloseSettings().Forget();
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
                _leaving = true;
                Services.Get<GameFlow>().GoToAsync(AppState.Game).Forget();
            }
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
            Services.Get<SettingsService>().Commit();
            await _stack.PopAsync();
        }

        private void OnDestroy()
        {
            _settingsPresenter?.Dispose();
            if (_glider != null)
            {
                Tween.StopAll(_glider);
            }
        }
    }
}
