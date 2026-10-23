using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>The obituary as words in the language of the moment, ready for the City Ledger's page. Game-free.</summary>
    public sealed class ObituaryPage
    {
        public int Day;
        public string Header = string.Empty;
        public string EarLeftLabel = string.Empty, EarLeft = string.Empty, EarRightLabel = string.Empty, EarRight = string.Empty;
        public string DateMiddle = string.Empty, DateRight = string.Empty;
        public string Kicker = string.Empty, Headline = string.Empty, Stand = string.Empty, Byline = string.Empty, Body = string.Empty;
        public readonly List<string> Labels = new List<string>(3);
        public readonly List<List<string>> Texts = new List<List<string>>(3);
        public readonly List<string> BoxLabels = new List<string>(2);
        public readonly List<List<string>> BoxLines = new List<List<string>>(2);
        public string NoticeHead = string.Empty, Notice = string.Empty;

        /// <summary>The whole page as one text, for the message shown if the paper's page cannot be.</summary>
        public string Plain()
        {
            var t = new System.Text.StringBuilder(2048);
            t.Append(Headline).Append("\n\n");
            if (Stand.Length > 0)
            {
                t.Append(Stand).Append("\n\n");
            }
            t.Append(Body).Append("\n\n");
            for (int i = 0; i < Labels.Count && i < Texts.Count; i++)
            {
                if (Texts[i].Count == 0)
                {
                    continue;
                }
                t.Append(Labels[i]).Append('\n').Append(string.Join(" ", Texts[i])).Append("\n\n");
            }
            for (int i = 0; i < BoxLabels.Count && i < BoxLines.Count; i++)
            {
                t.Append(BoxLabels[i]).Append('\n').Append(string.Join("\n", BoxLines[i])).Append("\n\n");
            }
            t.Append(NoticeHead).Append('\n').Append(Notice);
            return t.ToString();
        }
    }

    /// <summary>
    /// What Permadeath asks of the rest of the mod, set by Wiring/PermadeathLinks. Each left unset is simply without: a
    /// story with only what the game knows, a message in place of the paper.
    /// </summary>
    public static class PermadeathBridges
    {
        /// <summary>The people, the record and the city's knowledge of the player, written into a story that has the game's figures.</summary>
        public static Action<LifeStory> Story;

        /// <summary>The businesses the player runs now: their addresses (as keys) and their names.</summary>
        public static Action<List<string>, List<string>> Businesses;

        /// <summary>The obituary on the City Ledger's page, the action told once the page goes down. False while it cannot be shown yet.</summary>
        public static Func<ObituaryPage, Action, bool> ShowObituary;

        /// <summary>For the test button: a flu begun so many hours ago, untreated. False if none could be given.</summary>
        public static Func<float, bool> Flu;

        public static void Reset()
        {
            Story = null;
            Businesses = null;
            ShowObituary = null;
            Flu = null;
        }
    }
}
