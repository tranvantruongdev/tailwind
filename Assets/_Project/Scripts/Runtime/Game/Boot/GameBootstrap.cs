using Cysharp.Threading.Tasks;
using Template.Core.Save;
using Template.Core.Time;
using Template.Game.Debugging;
using Template.Game.Flow;
using Template.Infra;
using Template.Infra.Audio;
using Template.Infra.Save;
using Template.Infra.Settings;
using UnityEngine;

namespace Template.Game.Boot
{
    /// <summary>
    /// Lives in the Boot scene (first in Build Settings): creates services, loads the save,
    /// applies settings, then hands over to the Title scene.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Start() => Run().Forget();

        private async UniTaskVoid Run()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            var save = new SaveService(new FileSaveStore(), new SaveCodec(SaveSchema.CreateMigrator()), Debug.LogWarning);
            save.Load();

            var audio = AudioService.Create();
            var settings = new SettingsService(save, audio);

            Services.Register(save);
            Services.Register(audio);
            Services.Register(settings);
            Services.Register<IClock>(new SystemClock());

            settings.Apply();
            AppLifecycle.Create();
            DebugOverlay.CreateIfDevelopment();

            var flow = GameFlow.Create();
            Services.Register(flow);
            await flow.GoToAsync(AppState.Title);
        }
    }
}
