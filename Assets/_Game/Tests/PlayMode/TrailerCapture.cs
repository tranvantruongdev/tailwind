using System.Collections;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Tailwind.Core;
using Template.Game.Flow;
using Template.Infra;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tailwind.PlayModeTests
{
    /// <summary>
    /// Records the frames for the README GIF: title, an autopilot flight, a crash and the results.
    /// Explicit, so normal test runs skip it. Run it with graphics:
    ///   Tools/run-unity-tests.ps1 -TestPlatform PlayMode -Graphics -TestFilter Tailwind.PlayModeTests.TrailerCapture
    /// then Tools/make-gif.ps1 turns Logs/frames into docs/tailwind.gif.
    /// </summary>
    [Explicit("Records GIF frames; run on demand")]
    public class TrailerCapture
    {
        private const int Fps = 30;
        private int _frame;
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = CameraShot.Folder("frames");
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }

            Directory.CreateDirectory(_folder);
            _frame = 0;
            Time.captureFramerate = Fps; // game time advances 1/30 s per frame, however long a frame takes to save
        }

        [TearDown]
        public void TearDown() => Time.captureFramerate = 0;

        [UnityTest]
        public IEnumerator Records_trailer_frames()
        {
            // No scene (and so no camera) yet, so check the GPU only.
            Assume.That(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "needs graphics (run without -nographics)");

            SceneManager.LoadScene("Boot");
            while (SceneManager.GetActiveScene().name != "Title")
            {
                yield return null;
            }

            yield return Record(1.6f);

            Services.Get<GameFlow>().GoToAsync(AppState.Game).Forget();
            while (SceneManager.GetActiveScene().name != "Game")
            {
                yield return null;
            }

            yield return Record(0.6f);

            var controller = Object.FindAnyObjectByType<RunController>();
            var runField = typeof(RunController).GetField("_run", BindingFlags.NonPublic | BindingFlags.Instance);
            var tap = typeof(RunController).GetMethod("OnTap", BindingFlags.NonPublic | BindingFlags.Instance);
            var tuning = TailwindTuning.Default();
            tap.Invoke(controller, null);

            for (int i = 0; i < Fps * 13; i++)
            {
                var run = (GliderRun)runField.GetValue(controller);
                if (run.Crashed)
                {
                    break;
                }

                if (AutopilotBot.ShouldFlap(run, tuning))
                {
                    tap.Invoke(controller, null);
                }

                yield return Shot();
            }

            // Let go: the glider drops, crashes, and the results come up.
            yield return Record(2.6f);
        }

        private IEnumerator Record(float seconds)
        {
            int frames = Mathf.RoundToInt(seconds * Fps);
            for (int i = 0; i < frames; i++)
            {
                yield return Shot();
            }
        }

        private IEnumerator Shot()
        {
            yield return null; // CameraShot renders the camera itself, so any point in the frame works
            CameraShot.Save(Path.Combine(_folder, $"frame_{_frame++:D4}.tga"));
        }
    }
}
