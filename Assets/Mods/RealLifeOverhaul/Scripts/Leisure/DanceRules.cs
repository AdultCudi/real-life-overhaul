namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// The dances the player knows: the game's nine (Dancing.DanceType, in the game's own order) as a saved set of bits. A
    /// game begun with the set starts with the first four; the dance class at the school (Careers/Study) teaches the next
    /// one a class sat. A save from before the set keeps every dance, and NPC dancers keep every dance. Game-free; checked
    /// offline (EconomyCheck).
    /// </summary>
    public static class DanceRules
    {
        public const int Count = 9;                  // the game's dances
        public const int All = (1 << Count) - 1;
        public const int Starter = 0xF;              // the first four of the game's list: Boogie, Sway, Swing and The Macarena

        /// <summary>The set a save starts with when it holds none: the first four in a new game, every dance in an older save.</summary>
        public static int FirstSet(bool newGame) => newGame ? Starter : All;

        /// <summary>A saved set made sound: the first four always, nothing past the game's nine.</summary>
        public static int Sound(int set) => (set & All) | Starter;

        /// <summary>True if the dance (its index in the game's list) is in the set.</summary>
        public static bool Known(int set, int dance) => dance >= 0 && dance < Count && (set & (1 << dance)) != 0;

        /// <summary>How many of the game's dances the set holds.</summary>
        public static int KnownCount(int set)
        {
            int n = 0;
            for (int i = 0; i < Count; i++)
            {
                n += Known(set, i) ? 1 : 0;
            }
            return n;
        }

        /// <summary>The first dance not known yet, in the game's order; -1 when every dance is known.</summary>
        public static int NextLocked(int set)
        {
            for (int i = 0; i < Count; i++)
            {
                if (!Known(set, i))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The set with the next dance learned, and which (-1 when every dance was known already).</summary>
        public static int Learn(int set, out int learned)
        {
            learned = NextLocked(set);
            return learned >= 0 ? set | (1 << learned) : set;
        }

        /// <summary>A known dance by a roll (any whole number): the same roll, the same dance; the first dance for an empty set.</summary>
        public static int Pick(int set, int roll)
        {
            int n = KnownCount(set);
            if (n == 0)
            {
                return 0;
            }
            int k = ((roll % n) + n) % n;
            for (int i = 0; i < Count; i++)
            {
                if (Known(set, i) && k-- == 0)
                {
                    return i;
                }
            }
            return 0;
        }

        /// <summary>A known dance other than <paramref name="now"/> by a roll; <paramref name="now"/> itself when it is the only one.</summary>
        public static int Other(int set, int now, int roll)
        {
            int without = Known(set, now) ? set & ~(1 << now) : set;
            return KnownCount(without) == 0 ? now : Pick(without, roll);
        }
    }
}
