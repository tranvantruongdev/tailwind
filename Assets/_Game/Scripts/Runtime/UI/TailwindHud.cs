using System;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Tailwind.Art;
using Template.Feel;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tailwind.UI
{
    /// <summary>
    /// In-run HUD pinned to the safe area's edges: distance (top centre), combo pill with a draining grace bar,
    /// letters chip (top right), pause (top left), the start hint, and story notes. Owns the pause and results
    /// screens. Built in code from <see cref="UiTheme"/>.
    /// </summary>
    public sealed class TailwindHud : MonoBehaviour
    {
        private const float ComboBarWidth = 150f;

        private RectTransform _safe;
        private TextMeshProUGUI _distance;
        private RectTransform _combo;
        private TextMeshProUGUI _comboText;
        private RectTransform _comboBar;
        private RectTransform _letters;
        private TextMeshProUGUI _lettersText;
        private RectTransform _hint;
        private Tween _hintBreath;
        private RectTransform _story;
        private TextMeshProUGUI _storyText;
        private Sequence _storySequence;
        private PauseScreen _pause;
        private ResultsScreen _results;
        private int _shownDistance = -1;
        private int _shownCombo = 1;
        private int _shownLetters = -1;

        public event Action PausePressed;
        public event Action ResumePressed;
        public event Action RetryPressed;
        public event Action HomePressed;

        /// <summary>The results card revealed a row (0..3) or the score (4).</summary>
        public event Action<int> ResultsRowShown;

        /// <summary>The results card showed the "New best!" ribbon.</summary>
        public event Action NewBestShown;

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
            var theme = UiTheme.Current;
            _safe = UiFactory.CreateSafeArea(transform);

            var pause = UiFactory.CreateIconButton(_safe, theme.iconPause, Vector2.zero, 104, () => PausePressed?.Invoke(), ButtonStyle.Glass, "II");
            UiFactory.Place(pause, new Vector2(0f, 1f), new Vector2(92f, -92f));

            _distance = UiFactory.CreateText(_safe, "", 112, Vector2.zero, new Vector2(640, 150), TextAlignmentOptions.Center, UiFont.Display);
            UiFactory.Place(_distance, new Vector2(0.5f, 1f), new Vector2(0f, -96f));
            if (theme.displayShadow != null)
            {
                _distance.fontSharedMaterial = theme.displayShadow;
            }

            BuildCombo(theme);
            BuildLetters(theme);
            BuildHint(theme);
            BuildStory(theme);

            _pause = PauseScreen.Create(transform);
            _pause.ResumePressed += () => ResumePressed?.Invoke();
            _pause.RestartPressed += () => RetryPressed?.Invoke();
            _pause.HomePressed += () => HomePressed?.Invoke();

            _results = ResultsScreen.Create(transform);
            _results.RetryPressed += () => RetryPressed?.Invoke();
            _results.HomePressed += () => HomePressed?.Invoke();
            _results.RowShown += i => ResultsRowShown?.Invoke(i);
            _results.NewBest += () => NewBestShown?.Invoke();
        }

        private void BuildCombo(UiTheme theme)
        {
            _combo = UiFactory.CreateRect("Combo", _safe);
            _combo.sizeDelta = new Vector2(190f, 110f);
            UiFactory.Place(_combo, new Vector2(0.5f, 1f), new Vector2(0f, -232f));
            UiFactory.CreateRounded(_combo, new Vector2(0f, 10f), new Vector2(190f, 84f), theme.highlight, 42).name = "Pill";
            _comboText = UiFactory.CreateText(_combo, "×2", 60, new Vector2(0f, 13f), new Vector2(190f, 84f), TextAlignmentOptions.Center, UiFont.Display);

            // The bar drains over the grace period after leaving a wind stream: when it empties, the combo is gone.
            UiFactory.CreateRounded(_combo, new Vector2(0f, -46f), new Vector2(ComboBarWidth, 12f), new Color(0f, 0f, 0f, 0.3f), 6).name = "Bar track";
            var bar = UiFactory.CreateRounded(_combo, new Vector2(-ComboBarWidth * 0.5f, -46f), new Vector2(ComboBarWidth, 12f), Color.white, 6);
            bar.name = "Bar";
            _comboBar = bar.rectTransform;
            _comboBar.pivot = new Vector2(0f, 0.5f);
            _combo.gameObject.SetActive(false);
        }

        private void BuildLetters(UiTheme theme)
        {
            _letters = UiFactory.CreateRect("Letters", _safe);
            _letters.sizeDelta = new Vector2(200f, 92f);
            UiFactory.Place(_letters, new Vector2(1f, 1f), new Vector2(-124f, -92f));
            UiFactory.CreateRounded(_letters, Vector2.zero, new Vector2(200f, 84f), new Color(0f, 0f, 0f, 0.32f), 42).name = "Chip";
            UiFactory.CreateImage(_letters, ProceduralSprites.Envelope, new Vector2(-46f, 0f), new Vector2(66f, 48f), Palette.Letter).name = "Envelope";
            _lettersText = UiFactory.CreateText(_letters, "0", 56, new Vector2(40f, 3f), new Vector2(96f, 84f), TextAlignmentOptions.MidlineLeft,
                UiFont.Display);
            _lettersText.color = theme.textOnDark;
        }

        private void BuildHint(UiTheme theme)
        {
            // Not a button: a tap anywhere flaps, so every graphic here ignores raycasts.
            _hint = UiFactory.CreateRect("Hint", _safe);
            _hint.sizeDelta = new Vector2(560f, 140f);
            UiFactory.Place(_hint, new Vector2(0.5f, 0f), new Vector2(0f, 340f));
            UiFactory.AddShadow(_hint, Vector2.zero, new Vector2(560f, 128f), 64, 0.3f, 10f);
            UiFactory.CreateRounded(_hint, Vector2.zero, new Vector2(560f, 128f), theme.accent, 64);
            UiFactory.CreateText(_hint, Loc.T("Tap to fly"), 62, new Vector2(0f, 3f), new Vector2(520f, 128f), TextAlignmentOptions.Center, UiFont.Display)
                .color = theme.ink;
        }

        private void BuildStory(UiTheme theme)
        {
            _story = UiFactory.CreateRect("Story", _safe);
            _story.sizeDelta = new Vector2(920f, 170f);
            UiFactory.Place(_story, new Vector2(0.5f, 1f), new Vector2(0f, 260f));
            UiFactory.AddShadow(_story, Vector2.zero, _story.sizeDelta, 32, 0.3f, 10f);
            UiFactory.CreateRounded(_story, Vector2.zero, _story.sizeDelta, theme.paper, 32);
            _storyText = UiFactory.CreateText(_story, "", 50, Vector2.zero, new Vector2(860f, 150f), TextAlignmentOptions.Center, UiFont.Story);
            _storyText.color = theme.ink;
            _story.gameObject.SetActive(false);
        }

        public void ShowReady()
        {
            _hint.gameObject.SetActive(true);
            _hint.localScale = Vector3.one;
            _hintBreath.Stop();
            if (!JuiceFx.ReduceMotion)
            {
                _hintBreath = Tween.Scale(_hint, 1.05f, 1f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
            }

            _pause.gameObject.SetActive(false);
            _results.HideImmediate();
            _storySequence.Stop();
            _story.gameObject.SetActive(false);
            SetDistance(0);
            SetCombo(1, 0f);
            SetLetters(0);
        }

        public void HideHint()
        {
            _hintBreath.Stop();
            _hint.gameObject.SetActive(false);
        }

        public void SetDistance(int metres)
        {
            if (metres == _shownDistance)
            {
                return;
            }

            _shownDistance = metres;
            _distance.text = UiFactory.Tabular(metres.ToString()) + "<size=55%><alpha=#CC> m</size>";
        }

        public void SetCombo(int combo, float hold)
        {
            if (combo != _shownCombo)
            {
                _shownCombo = combo;
                _combo.gameObject.SetActive(combo > 1);
                if (combo > 1)
                {
                    _comboText.text = "×" + combo;
                    JuiceFx.Punch(_combo, 0.22f, 0.25f);
                }
            }

            if (combo > 1)
            {
                _comboBar.sizeDelta = new Vector2(Mathf.Max(12f, ComboBarWidth * Mathf.Clamp01(hold)), _comboBar.sizeDelta.y);
            }
        }

        public void SetLetters(int count)
        {
            if (count == _shownLetters)
            {
                return;
            }

            bool pickup = count > _shownLetters && _shownLetters >= 0;
            _shownLetters = count;
            _lettersText.text = count.ToString();
            if (pickup)
            {
                JuiceFx.Punch(_letters, 0.2f, 0.25f);
            }
        }

        /// <summary>A story beat as a paper note that slides down from the top edge, holds, and slides back.</summary>
        public void ShowStory(string text)
        {
            _storySequence.Stop();
            _storyText.text = Loc.T(text);
            _story.gameObject.SetActive(true);
            var hidden = new Vector2(0f, 260f);
            var shown = new Vector2(0f, -400f);
            if (JuiceFx.ReduceMotion)
            {
                _story.anchoredPosition = shown;
                _storySequence = Sequence.Create(useUnscaledTime: true)
                    .ChainDelay(3.2f)
                    .ChainCallback(() => _story.gameObject.SetActive(false));
                return;
            }

            _story.anchoredPosition = hidden;
            _storySequence = Sequence.Create(useUnscaledTime: true)
                .Chain(Tween.Custom(_story, hidden, shown, 0.45f, (r, p) => r.anchoredPosition = p, Ease.OutBack, useUnscaledTime: true))
                .ChainDelay(2.8f)
                .Chain(Tween.Custom(_story, shown, hidden, 0.35f, (r, p) => r.anchoredPosition = p, Ease.InCubic, useUnscaledTime: true))
                .ChainCallback(() => _story.gameObject.SetActive(false));
        }

        /// <summary>"+20" style text at a world position.</summary>
        public void FloatAt(Camera camera, Vector3 worldPosition, string text, Color color)
        {
            Vector2 screen = camera.WorldToScreenPoint(worldPosition);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_safe, screen, null, out var local))
            {
                // FloatingText positions relative to the rect centre.
                JuiceFx.FloatingText(_safe, text, local - _safe.rect.center, color, 60);
            }
        }

        public void ShowPause(bool visible)
        {
            if (visible == _pause.gameObject.activeSelf)
            {
                return;
            }

            if (visible)
            {
                _pause.ShowAsync().Forget();
            }
            else
            {
                _pause.HideAsync().Forget();
            }
        }

        private void OnDestroy()
        {
            _storySequence.Stop();
            _hintBreath.Stop();
        }

        public void ShowResults(RunSummary summary)
        {
            _hintBreath.Stop();
            _combo.gameObject.SetActive(false);
            _results.Present(summary);
        }
    }
}
