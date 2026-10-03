using System;
using PrimeTween;
using Tailwind.Art;
using Template.Feel;
using Template.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Tailwind.UI
{
    /// <summary>In-run HUD plus the pause and results panels, built in code.</summary>
    public sealed class TailwindHud : MonoBehaviour
    {
        private RectTransform _safe;
        private Text _distance;
        private Image _comboChip;
        private Text _comboText;
        private Text _hint;
        private Text _story;
        private CanvasGroup _storyGroup;
        private GameObject _pausePanel;
        private GameObject _resultsPanel;
        private Text _resultsTitle;
        private Text _resultsBody;
        private Text _resultsStory;
        private int _shownCombo = 1;

        public event Action PausePressed;
        public event Action ResumePressed;
        public event Action RetryPressed;
        public event Action HomePressed;

        public RectTransform SafeArea => _safe;

        public static TailwindHud Create()
        {
            UiFactory.EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<TailwindHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            _safe = UiFactory.CreateSafeArea(transform);

            _distance = UiFactory.CreateText(_safe, "0 m", 88, new Vector2(0, 800), new Vector2(700, 140));
            _distance.fontStyle = FontStyle.Bold;

            var chip = UiFactory.CreateRect("Combo", _safe);
            chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0.5f);
            chip.anchoredPosition = new Vector2(0, 680);
            chip.sizeDelta = new Vector2(220, 84);
            _comboChip = chip.gameObject.AddComponent<Image>();
            _comboChip.color = Palette.Accent;
            _comboText = UiFactory.CreateText(chip, "×2", 54, Vector2.zero, chip.sizeDelta);
            _comboText.color = Palette.Ink;
            _comboText.fontStyle = FontStyle.Bold;
            chip.gameObject.SetActive(false);

            UiFactory.CreateButton(_safe, "II", new Vector2(-440, 820), new Vector2(120, 120), () => PausePressed?.Invoke());

            _hint = UiFactory.CreateText(_safe, "Tap to fly", 64, new Vector2(0, -560), new Vector2(900, 120));
            Tween.Scale(_hint.transform, 1.08f, 0.6f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);

            _story = UiFactory.CreateText(_safe, "", 44, new Vector2(0, 560), new Vector2(980, 160));
            _story.fontStyle = FontStyle.Italic;
            _storyGroup = _story.gameObject.AddComponent<CanvasGroup>();
            _storyGroup.alpha = 0f;

            BuildPausePanel();
            BuildResultsPanel();
        }

        private void BuildPausePanel()
        {
            var panel = UiFactory.CreatePanel(_safe, new Color(0f, 0f, 0f, 0.7f));
            panel.raycastTarget = true;
            _pausePanel = panel.gameObject;
            UiFactory.CreateText(panel.rectTransform, "Paused", 96, new Vector2(0, 360), new Vector2(800, 160));
            UiFactory.CreateButton(panel.rectTransform, "Resume", new Vector2(0, 80), new Vector2(520, 150), () => ResumePressed?.Invoke());
            UiFactory.CreateButton(panel.rectTransform, "Restart", new Vector2(0, -110), new Vector2(520, 130), () => RetryPressed?.Invoke());
            UiFactory.CreateButton(panel.rectTransform, "Home", new Vector2(0, -280), new Vector2(520, 130), () => HomePressed?.Invoke());
            _pausePanel.SetActive(false);
        }

        private void BuildResultsPanel()
        {
            var panel = UiFactory.CreatePanel(_safe, new Color(0.08f, 0.05f, 0.14f, 0.88f));
            panel.raycastTarget = true;
            _resultsPanel = panel.gameObject;
            _resultsTitle = UiFactory.CreateText(panel.rectTransform, "", 92, new Vector2(0, 520), new Vector2(950, 160));
            _resultsTitle.fontStyle = FontStyle.Bold;
            _resultsBody = UiFactory.CreateText(panel.rectTransform, "", 56, new Vector2(0, 260), new Vector2(950, 320));
            _resultsStory = UiFactory.CreateText(panel.rectTransform, "", 42, new Vector2(0, 40), new Vector2(950, 160));
            _resultsStory.fontStyle = FontStyle.Italic;
            _resultsStory.color = Palette.Accent;
            UiFactory.CreateButton(panel.rectTransform, "Retry", new Vector2(0, -260), new Vector2(560, 170), () => RetryPressed?.Invoke());
            UiFactory.CreateButton(panel.rectTransform, "Home", new Vector2(0, -460), new Vector2(560, 130), () => HomePressed?.Invoke());
            _resultsPanel.SetActive(false);
        }

        public void ShowReady()
        {
            _hint.gameObject.SetActive(true);
            _pausePanel.SetActive(false);
            _resultsPanel.SetActive(false);
            SetDistance(0);
            SetCombo(1);
        }

        public void HideHint() => _hint.gameObject.SetActive(false);

        public void SetDistance(int metres) => _distance.text = $"{metres} m";

        public void SetCombo(int combo)
        {
            if (combo == _shownCombo)
            {
                return;
            }

            _shownCombo = combo;
            _comboChip.gameObject.SetActive(combo > 1);
            if (combo > 1)
            {
                _comboText.text = $"×{combo}";
                JuiceFx.Punch(_comboChip.transform, 0.25f, 0.25f);
            }
        }

        public void ShowStory(string text)
        {
            _story.text = text;
            Sequence.Create(useUnscaledTime: true)
                .Chain(Tween.Custom(_storyGroup, 0f, 1f, 0.4f, (g, a) => g.alpha = a, useUnscaledTime: true))
                .ChainDelay(2.6f)
                .Chain(Tween.Custom(_storyGroup, 1f, 0f, 0.6f, (g, a) => g.alpha = a, useUnscaledTime: true));
        }

        /// <summary>"+20" style text at a world position.</summary>
        public void FloatAt(Camera camera, Vector3 worldPosition, string text, Color color)
        {
            Vector2 screen = camera.WorldToScreenPoint(worldPosition);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_safe, screen, null, out var local))
            {
                // FloatingText positions relative to the rect centre.
                JuiceFx.FloatingText(_safe, text, local - _safe.rect.center, color, 56);
            }
        }

        public void ShowPause(bool visible) => _pausePanel.SetActive(visible);

        public void ShowResults(int distance, int delivery, int score, bool newBest, int best, string storyLine)
        {
            _resultsTitle.text = newBest ? "New best!" : "Delivered";
            _resultsBody.text = $"{distance} m flown\n{delivery} delivery points\nScore {score}{(newBest ? string.Empty : $"   ·   Best {best}")}";
            _resultsStory.text = storyLine ?? string.Empty;
            _resultsPanel.SetActive(true);
            JuiceFx.Punch(_resultsTitle.transform, 0.25f, 0.35f);
        }
    }
}
