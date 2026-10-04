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
    /// End-to-end check of the real game: boot, title, a run flown by the autopilot through the actual
    /// RunController, a crash and the results screen. Any error or exception logged fails the test.
    /// Screenshots go to Logs/screenshots (render with graphics, i.e. without -nographics).
    /// </summary>
    public class SmokeTests
    {
        [UnityTest]
        public IEnumerator Boots_flies_crashes_and_shows_results_without_errors()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Title", 20f);
            yield return new WaitForSeconds(0.6f);
            Capture("1-title");

            // Settings popup over the title, then close it again.
            var title = UnityEngine.Object.FindAnyObjectByType<TailwindTitleController>();
            Assert.IsNotNull(title, "TailwindTitleController should exist in the Title scene");
            Call(title, "OpenSettings");
            yield return new WaitForSecondsRealtime(0.5f);
            Capture("7-settings");
            Call(title, "CloseSettings");
            yield return new WaitForSecondsRealtime(0.4f);

            Services.Get<GameFlow>().GoToAsync(AppState.Game).Forget();
            yield return WaitForScene("Game", 20f);
            yield return new WaitForSeconds(0.6f);

            var controller = UnityEngine.Object.FindAnyObjectByType<RunController>();
            Assert.IsNotNull(controller, "RunController should exist in the Game scene");
            var runField = typeof(RunController).GetField("_run", BindingFlags.NonPublic | BindingFlags.Instance);
            var tap = typeof(RunController).GetMethod("OnTap", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(runField);
            Assert.IsNotNull(tap);
            Capture("2-ready");

            var tuning = TailwindTuning.Default();
            tap.Invoke(controller, null); // starts the run
            bool flyingShot = false;
            bool streamShot = false;
            float elapsed = 0f;
            while (elapsed < 25f)
            {
                var run = (GliderRun)runField.GetValue(controller);
                if (run.Crashed || run.X > 120f)
                {
                    break;
                }

                if (AutopilotBot.ShouldFlap(run, tuning))
                {
                    tap.Invoke(controller, null);
                }

                if (!flyingShot && run.X > 35f)
                {
                    Capture("3-flying");
                    flyingShot = true;

                    // Pause mid-flight, look at the pause card, carry on.
                    Call(controller, "Pause");
                    yield return new WaitForSecondsRealtime(0.5f);
                    Capture("8-pause");
                    Call(controller, "Resume"); // straight back to the autopilot loop, so the glider keeps flying
                }

                if (!streamShot && run.InStream && run.X > 20f)
                {
                    Capture("4-wind-stream");
                    streamShot = true;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            var flown = (GliderRun)runField.GetValue(controller);
            Assert.Greater(flown.X, 30f, $"autopilot only reached {flown.X:0.0} m in the real game loop");

            // Stop tapping: the glider falls, crashes, and the results screen appears.
            float wait = 0f;
            while (!flown.Crashed && wait < 5f)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(flown.Crashed, "glider should crash once nobody taps");
            yield return new WaitForSecondsRealtime(2.4f); // let the results reveal finish (rows, count-up, ribbon)
            Capture("5-results");
            Assert.Greater(Services.Get<Template.Core.Save.SaveService>().Data.totalRuns, 0, "the run should be saved");

            // Home again: the title now shows the best result.
            Services.Get<GameFlow>().GoToAsync(AppState.Title).Forget();
            yield return WaitForScene("Title", 20f);
            yield return new WaitForSeconds(1.5f);
            Capture("6-title-after-run");
        }

        private static IEnumerator WaitForScene(string name, float timeout)
        {
            float t = 0f;
            while (SceneManager.GetActiveScene().name != name)
            {
                t += Time.unscaledDeltaTime;
                if (t > timeout)
                {
                    Assert.Fail($"Timed out waiting for scene {name}; active is {SceneManager.GetActiveScene().name}");
                }

                yield return null;
            }
        }

        /// <summary>Saves the screen at three shapes: 9:16 phone, 20:9 tall phone, 4:3 tablet.</summary>
        private static void Capture(string name)
        {
            string root = CameraShot.Folder("screenshots");
            CameraShot.Save(Path.Combine(root, name + ".png"));
            CameraShot.Save(Path.Combine(root, "tall", name + ".png"), 540, 1200);
            CameraShot.Save(Path.Combine(root, "tablet", name + ".png"), 768, 1024);
        }

        /// <summary>Calls a private method (screens and controllers keep their handlers private).</summary>
        private static void Call(object target, string method)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{method} not found");
            info.Invoke(target, null);
        }
    }
}
