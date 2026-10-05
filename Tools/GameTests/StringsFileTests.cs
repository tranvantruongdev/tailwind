using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Tailwind.Core.Tests
{
    /// <summary>
    /// The shipped strings.csv is valid, and everything the player reads has a Vietnamese and a Japanese row: each
    /// Loc.T/Loc.F literal in the game's code, every story beat, and the template's Settings labels. dotnet only (it
    /// reads files from the repo).
    /// </summary>
    public class StringsFileTests
    {
        private static string Root()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Assets", "_Game", "Resources", "strings.csv")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Assets/_Game/Resources/strings.csv not found above the test folder");
            return dir.FullName;
        }

        [Test]
        public void Every_text_on_screen_has_vietnamese_and_japanese()
        {
            string root = Root();
            var strings = Strings.Parse(File.ReadAllText(Path.Combine(root, "Assets", "_Game", "Resources", "strings.csv")));
            CollectionAssert.IsEmpty(strings.Validate());

            var texts = new HashSet<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Assets", "_Game", "Scripts", "Runtime"), "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"Loc\.[TF]\(""((?:[^""\\]|\\.)*)"""))
                {
                    texts.Add(Regex.Unescape(m.Groups[1].Value));
                }
            }

            string settings = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Runtime", "UI", "SettingsPanelView.cs"));
            foreach (Match m in Regex.Matches(settings, @"Create(?:Text|Slider|Toggle|Button)\(card, ""([^""]+)"""))
            {
                texts.Add(m.Groups[1].Value);
            }

            texts.UnionWith(StoryBeats.All.Select(b => b.text));
            texts.Add(StoryBeats.Prologue);

            Assert.Greater(texts.Count, 25, "the scan found the game's texts");
            foreach (var language in new[] { "vi", "ja" })
            {
                CollectionAssert.IsEmpty(texts.Where(t => !strings.Has(t, language)).OrderBy(t => t).ToList(), $"strings.csv has no {language} for these");
            }
        }
    }
}
