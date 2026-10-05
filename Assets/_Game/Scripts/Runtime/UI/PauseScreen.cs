using System;
using Cysharp.Threading.Tasks;
using Template.Infra;
using Template.Infra.Settings;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tailwind.UI
{
    /// <summary>
    /// Pause card: Resume (main action), Restart, Home, plus quick sound and vibration switches so players
    /// don't need the settings screen mid-run. Shows and hides with the <see cref="UIScreen"/> animation.
    /// </summary>
    public sealed class PauseScreen : UIScreen
    {
        private Image _soundIcon;
        private Image _vibrationIcon;
        private float _mutedMusic = 0.8f;
        private float _mutedSfx = 1f;

        public event Action ResumePressed;
        public event Action RestartPressed;
        public event Action HomePressed;

        public static PauseScreen Create(Transform parent)
        {
            var theme = UiTheme.Current;
            var overlay = UiFactory.CreateOverlay(parent);
            overlay.name = "Pause";
            var screen = overlay.gameObject.AddComponent<PauseScreen>();
            var card = UiFactory.CreateCard(overlay.rectTransform, Vector2.zero, new Vector2(860, 1060));

            UiFactory.CreateText(card, Loc.T("Paused"), 96, new Vector2(0, 400), new Vector2(700, 140), TextAlignmentOptions.Center, UiFont.Display)
                .color = theme.ink;
            UiFactory.CreateButton(card, Loc.T("Resume"), new Vector2(0, 205), new Vector2(700, 160), () => screen.ResumePressed?.Invoke(),
                ButtonStyle.Primary, theme.iconPlay);
            UiFactory.CreateButton(card, Loc.T("Restart"), new Vector2(0, 20), new Vector2(700, 130), () => screen.RestartPressed?.Invoke(),
                ButtonStyle.Secondary, theme.iconRetry);
            UiFactory.CreateButton(card, Loc.T("Home"), new Vector2(0, -140), new Vector2(700, 130), () => screen.HomePressed?.Invoke(),
                ButtonStyle.Secondary, theme.iconHome);

            var sound = UiFactory.CreateIconButton(card, theme.iconSoundOn, new Vector2(-90, -360), 112, screen.ToggleSound, ButtonStyle.Secondary, "S");
            var vibration = UiFactory.CreateIconButton(card, theme.iconVibration, new Vector2(90, -360), 112, screen.ToggleVibration, ButtonStyle.Secondary, "V");
            screen._soundIcon = FindIcon(sound);
            screen._vibrationIcon = FindIcon(vibration);

            overlay.gameObject.SetActive(false);
            return screen;
        }

        public override async UniTask ShowAsync()
        {
            Refresh();
            await base.ShowAsync();
        }

        private void ToggleSound()
        {
            var settings = Services.Get<SettingsService>();
            var s = settings.Current;
            if (s.musicVolume > 0.001f || s.sfxVolume > 0.001f)
            {
                _mutedMusic = s.musicVolume;
                _mutedSfx = s.sfxVolume;
                s.musicVolume = 0f;
                s.sfxVolume = 0f;
            }
            else
            {
                s.musicVolume = _mutedMusic > 0.001f ? _mutedMusic : 0.8f;
                s.sfxVolume = _mutedSfx > 0.001f ? _mutedSfx : 1f;
            }

            settings.Apply();
            settings.Commit();
            Refresh();
        }

        private void ToggleVibration()
        {
            var settings = Services.Get<SettingsService>();
            settings.Current.haptics = !settings.Current.haptics;
            settings.Apply();
            settings.Commit();
            Refresh();
        }

        private void Refresh()
        {
            if (!Services.TryGet<SettingsService>(out var settings))
            {
                return;
            }

            var theme = UiTheme.Current;
            var s = settings.Current;
            bool soundOn = s.musicVolume > 0.001f || s.sfxVolume > 0.001f;
            if (_soundIcon != null && theme.iconSoundOff != null)
            {
                _soundIcon.sprite = soundOn ? theme.iconSoundOn : theme.iconSoundOff;
            }

            if (_vibrationIcon != null)
            {
                var c = _vibrationIcon.color;
                c.a = s.haptics ? 1f : 0.3f;
                _vibrationIcon.color = c;
            }
        }

        private static Image FindIcon(Button button)
        {
            var icon = button.transform.Find("Icon");
            return icon != null ? icon.GetComponent<Image>() : null;
        }
    }
}
