using System;
using Template.Core.Save;
using Template.Core.Settings;
using Template.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Template.Game.Sample
{
    /// <summary>
    /// The "V" in MVP: only displays values and forwards input. All logic sits in
    /// <see cref="SettingsPresenter"/> (Core), which is unit-tested without Unity.
    /// </summary>
    public sealed class SettingsPanelView : UIScreen, ISettingsView
    {
        private Slider _music;
        private Slider _sfx;
        private Toggle _haptics;
        private Toggle _reduceMotion;

        public event Action<float> MusicChanged;
        public event Action<float> SfxChanged;
        public event Action<bool> HapticsChanged;
        public event Action<bool> ReduceMotionChanged;
        public event Action CloseRequested;

        public override bool IsModal => true;

        public static SettingsPanelView Create(Transform parent)
        {
            var dim = UiFactory.CreatePanel(parent, new Color(0f, 0f, 0f, 0.8f));
            dim.name = "Settings";
            dim.raycastTarget = true;
            var root = dim.rectTransform;
            var view = dim.gameObject.AddComponent<SettingsPanelView>();

            UiFactory.CreateText(root, "Settings", 80, new Vector2(0, 560), new Vector2(900, 140));
            view._music = UiFactory.CreateSlider(root, "Music", new Vector2(0, 320), v => view.MusicChanged?.Invoke(v));
            view._sfx = UiFactory.CreateSlider(root, "Sound effects", new Vector2(0, 120), v => view.SfxChanged?.Invoke(v));
            view._haptics = UiFactory.CreateToggle(root, "Vibration", new Vector2(0, -60), v => view.HapticsChanged?.Invoke(v));
            view._reduceMotion = UiFactory.CreateToggle(root, "Reduce motion", new Vector2(0, -180), v => view.ReduceMotionChanged?.Invoke(v));
            UiFactory.CreateButton(root, "Close", new Vector2(0, -480), new Vector2(480, 140), () => view.CloseRequested?.Invoke());

            dim.gameObject.SetActive(false);
            return view;
        }

        public void Show(SettingsData settings)
        {
            _music.SetValueWithoutNotify(settings.musicVolume);
            _sfx.SetValueWithoutNotify(settings.sfxVolume);
            _haptics.SetIsOnWithoutNotify(settings.haptics);
            _reduceMotion.SetIsOnWithoutNotify(settings.reduceMotion);
        }

        public override bool HandleBack()
        {
            CloseRequested?.Invoke();
            return true;
        }
    }
}
