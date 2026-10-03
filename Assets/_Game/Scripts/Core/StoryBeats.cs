using System.Collections.Generic;

namespace Tailwind.Core
{
    public struct StoryBeat
    {
        public int distance;
        public string text;
    }

    /// <summary>One-line story moments unlocked by distance, shown on the results and title screens.</summary>
    public static class StoryBeats
    {
        public static readonly IReadOnlyList<StoryBeat> All = new[]
        {
            new StoryBeat { distance = 100, text = "First letter delivered. Someone smiled." },
            new StoryBeat { distance = 300, text = "The old baker got a letter from his son." },
            new StoryBeat { distance = 600, text = "Lantern festival tonight; the sky is busy." },
            new StoryBeat { distance = 1000, text = "Every letter in town, delivered. Mây rests on the moon's edge." },
        };

        public const string Prologue = "Gióng Town at dusk. Mây, a paper glider, has letters to deliver.";

        public static int Count => All.Count;

        /// <summary>The beat for the best distance so far, or the prologue before the first one.</summary>
        public static string TextFor(int storyIndex) => storyIndex >= 0 && storyIndex < All.Count ? All[storyIndex].text : Prologue;

        /// <summary>Highest beat index reached at a distance, or -1.</summary>
        public static int IndexForDistance(int distance)
        {
            int index = -1;
            for (int i = 0; i < All.Count; i++)
            {
                if (distance >= All[i].distance)
                {
                    index = i;
                }
            }

            return index;
        }
    }
}
