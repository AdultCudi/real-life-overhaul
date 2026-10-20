namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// When the partner raises things about the player's fortune, game-free: not before some days after moving in (7 to 12,
    /// by their seed), then a few days between one word and the next (5 to 10, drawn afresh each time from the seed and the
    /// day of the last); an ask stays open a week, with a reminder on its fifth and sixth day. Nothing here is read per frame.
    /// </summary>
    public static class PartnerVoiceRules
    {
        public const int SettleLeast = 7, SettleMost = 12;   // days after moving in before the first
        public const int GapLeast = 5, GapMost = 10;         // days between one word and the next
        public const int AskDays = 7;                        // days an ask stays open before it lapses
        public static readonly int[] RemindOn = { 5, 6 };    // days of an open ask on which they remind the player

        /// <summary>The days after moving in before the first word, from the seed: <see cref="SettleLeast"/> to <see cref="SettleMost"/>.</summary>
        public static int Settle(int seed, int movedIn) => SettleLeast + Mix(seed, movedIn) % (SettleMost - SettleLeast + 1);

        /// <summary>The days after a word before the next, from the seed and that word's day: <see cref="GapLeast"/> to <see cref="GapMost"/>.</summary>
        public static int Gap(int seed, int lastDay) => GapLeast + Mix(seed ^ 0x5bd1e995, lastDay) % (GapMost - GapLeast + 1);

        /// <summary>Whether a word may come today: settled in since moving in, and the gap since the last word over.</summary>
        public static bool Free(int day, int movedIn, int lastDay, int seed)
        {
            if (day - movedIn < Settle(seed, movedIn))
            {
                return false;
            }
            return lastDay <= 0 || day - lastDay >= Gap(seed, lastDay);
        }

        /// <summary>The reminder due on a day of an open ask (0 or 1), with so many sent already; -1 for none.</summary>
        public static int Reminder(int since, int sent)
        {
            return sent < RemindOn.Length && since >= RemindOn[sent] ? sent : -1;
        }

        private static int Mix(int seed, int day)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)day * 40503u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h & 0x7fffffff);
            }
        }
    }
}
