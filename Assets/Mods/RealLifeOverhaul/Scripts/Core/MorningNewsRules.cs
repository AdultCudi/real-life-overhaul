using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>One notification of the morning summary: which of the held notices it carries.</summary>
    public struct NewsPart
    {
        public int From;     // the first held notice in it
        public int Count;    // how many, in the order they came; 0 in the head: the morning line alone
        public bool Head;    // it begins with the morning line
        public bool Alone;   // one notice too long for the summary's words around it: shown as it came, under its own key
    }

    /// <summary>
    /// The city's news gathered into the morning line: what is held, for how long after the wake, and how it is packed.
    /// The game's sleep skips time with the game paused, so the mod's parts on game time catch up every missed hour in
    /// the minute the player wakes; held until each has looked again, that news goes out with the morning line. Each
    /// notice keeps its text whole and in order; a greedy fill in order gives the fewest notifications. Game-free; one
    /// pass over a dozen notices at most, nothing allocated.
    /// </summary>
    public static class MorningNewsRules
    {
        public const int Most = 240;              // characters in one notification: the game's toast shows about that much (NoticeCheck)
        public const int MostHeld = 12;           // notices held at once: more are shown as they come, so even a long sleep holds a short list
        public const float CatchUpGame = 1.5f;    // seconds of game time after the wake: every part on game time has looked again
        public const float CatchUpReal = 6f;      // seconds of real time at most: a game paused at once does not keep the line waiting
        public const int Gap = 1;                 // characters between two notices in one notification: a space
        public const int WarningWeight = 2;       // MorningNews.Weight: news at least this strong colours the morning line's notification

        /// <summary>Whether the moment after the wake is over, by game time (Time.time) and real time (Time.unscaledTime), in seconds.</summary>
        public static bool CaughtUp(float gameNow, float gameWoke, float realNow, float realWoke) =>
            gameNow - gameWoke >= CatchUpGame || realNow - realWoke >= CatchUpReal;

        /// <summary>The characters a template adds around its placeholders, each placeholder counted once if present.</summary>
        public static int Extra(string template, string first, string second)
        {
            if (template == null)
            {
                return 0;
            }
            int extra = template.Length;
            if (!string.IsNullOrEmpty(first) && template.Contains(first))
            {
                extra -= first.Length;
            }
            if (!string.IsNullOrEmpty(second) && template.Contains(second))
            {
                extra -= second.Length;
            }
            return extra < 0 ? 0 : extra;
        }

        /// <summary>The held notice a new one replaces: the first with the same duplicate id; -1 for none or no id.</summary>
        public static int Same(IList<string> ids, int count, string id)
        {
            if (string.IsNullOrEmpty(id) || ids == null)
            {
                return -1;
            }
            for (int i = 0; i < count && i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The strongest weight of a part's notices, starting from <paramref name="start"/>.</summary>
        public static int Strongest(IList<int> weights, int from, int count, int start)
        {
            int best = start;
            for (int i = from; i < from + count && i < weights.Count; i++)
            {
                if (weights[i] > best)
                {
                    best = weights[i];
                }
            }
            return best;
        }

        /// <summary>The morning line's weight: the sleep's, unless its news holds a stronger warning or error.</summary>
        public static int HeadWeight(int morning, int news) => news >= WarningWeight && news > morning ? news : morning;

        /// <summary>
        /// Packs the held notices by length: the head (the morning line of <paramref name="morning"/> characters plus
        /// <paramref name="headExtra"/>), then as few more as fit, each with <paramref name="moreExtra"/> around its news.
        /// </summary>
        public static void Pack(int morning, int headExtra, int moreExtra, IList<int> lengths, int count, List<NewsPart> parts)
        {
            parts.Clear();
            if (count > lengths.Count)
            {
                count = lengths.Count;
            }
            int i = 0;
            int used = morning + headExtra;
            int taken = 0;
            while (i < count && used + (taken > 0 ? Gap : 0) + lengths[i] <= Most)
            {
                used += (taken > 0 ? Gap : 0) + lengths[i];
                taken++;
                i++;
            }
            parts.Add(new NewsPart { From = 0, Count = taken, Head = true });
            while (i < count)
            {
                int from = i;
                used = moreExtra;
                taken = 0;
                while (i < count && used + (taken > 0 ? Gap : 0) + lengths[i] <= Most)
                {
                    used += (taken > 0 ? Gap : 0) + lengths[i];
                    taken++;
                    i++;
                }
                if (taken == 0)
                {
                    parts.Add(new NewsPart { From = i, Count = 1, Alone = true });   // too long to share one: as it came, and the next is looked at
                    i++;
                }
                else
                {
                    parts.Add(new NewsPart { From = from, Count = taken });
                }
            }
        }

        /// <summary>The characters a packed part has when it is shown.</summary>
        public static int Length(NewsPart part, int morning, int headExtra, int moreExtra, IList<int> lengths)
        {
            if (part.Head && part.Count == 0)
            {
                return morning;
            }
            int sum = 0;
            for (int i = part.From; i < part.From + part.Count && i < lengths.Count; i++)
            {
                sum += lengths[i] + (i > part.From ? Gap : 0);
            }
            if (part.Alone)
            {
                return sum;
            }
            return part.Head ? morning + headExtra + sum : moreExtra + sum;
        }
    }
}
