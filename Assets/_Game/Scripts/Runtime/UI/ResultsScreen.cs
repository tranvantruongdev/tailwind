using System;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Template.Feel;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Tailwind.UI
{
    /// <summary>Everything the results card shows about one run.</summary>
    public struct RunSummary
    {
        public int distance;
        public int letters;
        public int letterPoints;
        public int nearMisses;
        public int nearMissPoints;
        public int bestCombo;
        public int score;
        public int best;
        public bool newBest;
        public string storyLine;
    }

    /// <summary>
    /// The results card: stat rows appear one by one, the score counts up, then a "New best!" ribbon and paper
    /// confetti on a record. Tapping the card skips straight to the end. Retry is the main, thumb-reachable action.
    /// </summary>
    public sealed class ResultsScreen : UIScreen, IPointerClickHandler
    {
        private const float RowStep = 0.16f;
        private const float CountSeconds = 0.7f;

        private const int TipCount = 5;

        /// <summary>One rule of the game, in the player's language.</summary>
        private static string Tip(int index)
        {
            switch (index % TipCount)
            {
                case 0: return Loc.T("Ride the pale wind streams to build a combo.");
                case 1: return Loc.T("Letters are worth 10 × your combo.");
                case 2: return Loc.T("The combo fades a second after you leave the wind.");
                case 3: return Loc.T("Skim past a chimney for a near-miss bonus.");
                default: return Loc.T("Inside the wind, flaps are gentler. Tap a little more.");
            }
        }

        private readonly TextMeshProUGUI[] _rowValues = new TextMeshProUGUI[4];
        private readonly RectTransform[] _rows = new RectTransform[4];
        private TextMeshProUGUI _total;
        private TextMeshProUGUI _best;
        private RectTransform _ribbon;
        private RectTransform _storyStrip;
        private TextMeshProUGUI _story;
        private Sequence _reveal;
        private RunSummary _summary;
        private bool _revealing;

        public event Action RetryPressed;
        public event Action HomePressed;

        /// <summary>A stat row appeared (index 0..3, then 4 for the score); for a rising tick sound.</summary>
        public event Action<int> RowShown;

        /// <summary>The "New best!" ribbon appeared; for a sting and a haptic.</summary>
        public event Action NewBest;

        public static ResultsScreen Create(Transform parent)
        {
            var theme = UiTheme.Current;
            var overlay = UiFactory.CreateOverlay(parent);
            overlay.name = "Results";
            var screen = overlay.gameObject.AddComponent<ResultsScreen>();
            var card = UiFactory.CreateCard(overlay.rectTransform, Vector2.zero, new Vector2(900, 1360));

            UiFactory.CreateText(card, Loc.T("Delivered"), 92, new Vector2(0, 525), new Vector2(760, 130), TextAlignmentOptions.Center, UiFont.Display)
                .color = theme.ink;

            string[] labels = { "Distance", "Letters", "Near misses", "Best combo" }; // row names, kept in English
            string[] shown = { Loc.T("Distance"), Loc.T("Letters"), Loc.T("Near misses"), Loc.T("Best combo") };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = 385f - i * 100f;
                var row = UiFactory.CreateRect("Row " + labels[i], card);
                row.anchoredPosition = new Vector2(0f, y);
                row.sizeDelta = new Vector2(760f, 90f);
                UiFactory.CreateText(row, shown[i], 46, new Vector2(-190f, 0f), new Vector2(380f, 90f), TextAlignmentOptions.MidlineLeft)
                    .color = theme.muted;
                var value = UiFactory.CreateText(row, "", 54, new Vector2(190f, 0f), new Vector2(380f, 90f), TextAlignmentOptions.MidlineRight,
                    UiFont.Display);
                value.color = theme.ink;
                if (i < labels.Length - 1)
                {
                    UiFactory.CreateRounded(card, new Vector2(0f, y - 50f), new Vector2(760f, 3f), theme.paperEdge, 2).name = "Divider";
                }

                screen._rows[i] = row;
                screen._rowValues[i] = value;
            }

            UiFactory.CreateText(card, Loc.T("SCORE"), 38, new Vector2(0, -35), new Vector2(400, 60)).color = theme.muted;
            screen._total = UiFactory.CreateText(card, "0", 150, new Vector2(0, -130), new Vector2(760, 170), TextAlignmentOptions.Center, UiFont.Display);
            screen._total.color = theme.ink;
            screen._best = UiFactory.CreateText(card, "", 40, new Vector2(0, -225), new Vector2(700, 60));
            screen._best.color = theme.muted;

            screen._storyStrip = UiFactory.CreateRounded(card, new Vector2(0, -345), new Vector2(820, 120), theme.paperEdge, 24).rectTransform;
            screen._story = UiFactory.CreateText(screen._storyStrip, "", 46, Vector2.zero, new Vector2(760, 110), TextAlignmentOptions.Center, UiFont.Story);
            screen._story.color = theme.ink;

            UiFactory.CreateIconButton(card, theme.iconHome, new Vector2(-330, -545), 124, () => screen.HomePressed?.Invoke(),
                ButtonStyle.Secondary, "Home");
            UiFactory.CreateButton(card, Loc.T("Retry"), new Vector2(80, -545), new Vector2(600, 150), () => screen.RetryPressed?.Invoke(),
                ButtonStyle.Primary, theme.iconRetry);

            // The ribbon overlaps the card's top edge, tilted a little like a sticker.
            screen._ribbon = UiFactory.CreateRect("New best", card);
            screen._ribbon.anchoredPosition = new Vector2(0, 690);
            screen._ribbon.sizeDelta = new Vector2(500, 110);
            screen._ribbon.localRotation = Quaternion.Euler(0f, 0f, -3f);
            UiFactory.AddShadow(screen._ribbon, Vector2.zero, new Vector2(500, 100), 50, 0.3f, 8f);
            UiFactory.CreateRounded(screen._ribbon, Vector2.zero, new Vector2(500, 100), theme.highlight, 50);
            UiFactory.CreateText(screen._ribbon, Loc.T("New best!"), 64, new Vector2(0, 3), new Vector2(480, 100), TextAlignmentOptions.Center, UiFont.Display);

            overlay.gameObject.SetActive(false);
            return screen;
        }

        public void Present(RunSummary summary)
        {
            _summary = summary;
            string muted = ColorUtility.ToHtmlStringRGBA(UiTheme.Current.muted);
            _rowValues[0].text = UiFactory.Tabular(summary.distance.ToString()) + "<size=70%> m</size>";
            _rowValues[1].text = $"{summary.letters}<color=#{muted}><size=75%>  +{summary.letterPoints}</size></color>";
            _rowValues[2].text = $"{summary.nearMisses}<color=#{muted}><size=75%>  +{summary.nearMissPoints}</size></color>";
            _rowValues[3].text = "×" + summary.bestCombo;
            _best.text = summary.newBest ? Loc.T("Your best yet") : Loc.F("Best {0}", summary.best);
            // A new story beat if one unlocked; otherwise a tip that teaches one rule of the game.
            _story.text = !string.IsNullOrEmpty(summary.storyLine)
                ? Loc.T(summary.storyLine)
                : $"<color=#{muted}>{Loc.T("Tip:")}</color> {Tip(summary.score + summary.distance)}";
            _ribbon.gameObject.SetActive(false);
            _total.text = UiFactory.Tabular("0");
            foreach (var row in _rows)
            {
                SetRowVisible(row, 0f);
            }

            ShowAsync().Forget();
            Reveal();
        }

        /// <summary>Hides at once (a new run is starting), stopping any count-up.</summary>
        public void HideImmediate()
        {
            _reveal.Stop();
            _revealing = false;
            gameObject.SetActive(false);
        }

        public override async UniTask HideAsync()
        {
            _reveal.Stop();
            _revealing = false;
            await base.HideAsync();
        }

        // Leaving the scene mid-reveal must not run the remaining callbacks on destroyed objects.
        private void OnDestroy() => _reveal.Stop();

        /// <summary>Tapping the card while it is still counting jumps to the final numbers.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_revealing)
            {
                return;
            }

            _reveal.Complete();
            if (_revealing)
            {
                Finish();
            }
        }

        private void Reveal()
        {
            _reveal.Stop();
            _revealing = true;
            bool calm = JuiceFx.ReduceMotion;
            var sequence = Sequence.Create(useUnscaledTime: true).ChainDelay(calm ? 0.05f : 0.2f);
            for (int i = 0; i < _rows.Length; i++)
            {
                int index = i;
                sequence = sequence
                    .ChainCallback(() => RowShown?.Invoke(index))
                    .Chain(Tween.Custom(_rows[i], 0f, 1f, calm ? 0.05f : RowStep, SetRowVisible, Ease.OutCubic, useUnscaledTime: true));
            }

            int score = _summary.score;
            _reveal = sequence
                .ChainCallback(() => RowShown?.Invoke(_rows.Length))
                .Chain(Tween.Custom(_total, 0f, score, calm ? 0.05f : CountSeconds,
                    (t, v) => t.text = UiFactory.Tabular(Mathf.RoundToInt(v).ToString()), Ease.OutCubic, useUnscaledTime: true))
                .ChainCallback(Finish);
        }

        private void Finish()
        {
            if (!_revealing)
            {
                return;
            }

            _revealing = false;
            _total.text = UiFactory.Tabular(_summary.score.ToString());
            foreach (var row in _rows)
            {
                SetRowVisible(row, 1f);
            }

            if (!_summary.newBest)
            {
                return;
            }

            _ribbon.gameObject.SetActive(true);
            JuiceFx.Punch(_ribbon, 0.25f, 0.4f);
            NewBest?.Invoke();
            Confetti.Burst((RectTransform)transform, UiTheme.Current);
        }

        private static void SetRowVisible(RectTransform row, float t)
        {
            var group = row.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = row.gameObject.AddComponent<CanvasGroup>();
            }

            group.alpha = t;
            row.anchoredPosition = new Vector2((1f - t) * -24f, row.anchoredPosition.y);
        }
    }

    /// <summary>Paper confetti for a new best: small rotated scraps that drift down and fade. Skipped with reduce motion.</summary>
    public static class Confetti
    {
        public static void Burst(RectTransform parent, UiTheme theme, int count = 40)
        {
            if (JuiceFx.ReduceMotion || parent == null)
            {
                return;
            }

            var colors = new[] { theme.accent, theme.highlight, theme.paper, Color.white };
            var random = new System.Random(Environment.TickCount);
            float halfWidth = parent.rect.width * 0.5f;
            float top = parent.rect.height * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var size = new Vector2(14f + (float)random.NextDouble() * 12f, 22f + (float)random.NextDouble() * 16f);
                var piece = UiFactory.CreateRounded(parent, new Vector2(Range(random, -halfWidth, halfWidth), top + 40f), size,
                    colors[i % colors.Length], 4);
                piece.name = "Confetti";
                var rect = piece.rectTransform;
                float spinStart = Range(random, 0f, 360f);
                rect.localEulerAngles = new Vector3(0f, 0f, spinStart);
                float seconds = Range(random, 1.6f, 2.6f);
                var start = rect.anchoredPosition;
                var end = new Vector2(start.x + Range(random, -160f, 160f), -top * Range(random, 0.2f, 0.9f));
                Sequence.Create(useUnscaledTime: true)
                    .ChainDelay(Range(random, 0f, 0.35f))
                    .Chain(Tween.Custom(rect, start, end, seconds, (r, p) => r.anchoredPosition = p, Ease.OutQuad, useUnscaledTime: true))
                    .Group(Tween.LocalEulerAngles(rect, new Vector3(0f, 0f, spinStart), new Vector3(0f, 0f, spinStart + Range(random, -540f, 540f)),
                        seconds, Ease.OutQuad, useUnscaledTime: true))
                    .Group(Tween.Custom(piece, 1f, 0f, seconds, (img, a) =>
                    {
                        var c = img.color;
                        c.a = a;
                        img.color = c;
                    }, Ease.InCubic, useUnscaledTime: true))
                    .OnComplete(piece.gameObject, go => UnityEngine.Object.Destroy(go), warnIfTargetDestroyed: false);
            }
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
