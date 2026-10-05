using Tailwind.Core;
using Template.Infra;
using Template.Infra.Settings;
using Template.UI;
using UnityEngine;

namespace Tailwind
{
    /// <summary>
    /// UI text in the player's language (Settings → Language), from Resources/strings.csv, where the English is the key.
    /// Wrap every player-facing literal: <c>Loc.T("Retry")</c>, <c>Loc.F("Best {0}", score)</c>.
    /// </summary>
    public static class Loc
    {
        private static Strings _strings;

        public static Strings Table
        {
            get
            {
                if (_strings == null)
                {
                    var file = Resources.Load<TextAsset>("strings");
                    _strings = Strings.Parse(file != null ? file.text : "en,vi,ja\n");
                }

                return _strings;
            }
        }

        public static string Language => Services.TryGet<SettingsService>(out var settings) ? settings.Current.language : "en";

        public static string T(string english) => Table.Translate(english, Language);

        public static string F(string english, params object[] args) => string.Format(T(english), args);

        /// <summary>The device language if the game has it, else English: a first launch's choice.</summary>
        public static string FromSystem() =>
            Application.systemLanguage == SystemLanguage.Japanese ? "ja" : Application.systemLanguage == SystemLanguage.Vietnamese ? "vi" : "en";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            UiFactory.Localize = T; // the template's own labels (Settings) in the player's language
            SettingsPanelView.Languages = new[] { ("en", "English"), ("vi", "Tiếng Việt"), ("ja", "日本語") };
        }
    }
}
