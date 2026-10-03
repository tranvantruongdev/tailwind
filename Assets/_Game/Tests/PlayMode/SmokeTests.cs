using System;
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
        private static string ShotFolder => Path.Combine(Application.dataPath, "..", "Logs", "screenshots");

        [UnityTest]
        public IEnumerator Boots_flies_crashes_and_shows_results_without_errors()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Title", 20f);
            yield return new WaitForSeconds(0.6f);
            Capture("1-title");

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
            yield return new WaitForSecondsRealtime(1.2f);
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

        /// <summary>Renders the main camera plus overlay UI into a 540×960 PNG.</summary>
        private static void Capture(string name)
        {
            var camera = Camera.main;
            if (camera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return; // -nographics: nothing to render
            }

            Directory.CreateDirectory(ShotFolder);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = new RenderMode[canvases.Length];
            var orders = new int[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                orders[i] = canvases[i].sortingOrder;
                if (canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    // In camera space the canvas sorts against sprites, so lift it above them all,
                    // keeping the canvases' order among themselves, as an overlay would draw.
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 1f;
                    canvases[i].sortingOrder = 10000 + orders[i];
                }
            }

            Canvas.ForceUpdateCanvases();
            var texture = new RenderTexture(540, 960, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(540, 960, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            image.Apply();
            camera.targetTexture = previous;
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(ShotFolder, name + ".png"), image.EncodeToPNG());

            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = modes[i];
                canvases[i].sortingOrder = orders[i];
            }

            UnityEngine.Object.Destroy(image);
            texture.Release();
        }
    }
}
