using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The builder's licence by floors: a higher level lets a builder put up taller buildings, earned by finished builds and standing together (neither
    /// alone), and never lost once earned. The player's company and the city's eight builders stand on the same ladder: a rival's level follows from the
    /// builds it has done and its standing, so it bids only on buildings its level allows. Game-free, deterministic, nothing allocated.
    /// </summary>
    public static class LevelRules
    {
        public const int Levels = 4;

        /// <summary>The floors each level is licensed to.</summary>
        public static readonly int[] Floors = { 6, 8, 10, 12 };

        /// <summary>The builds a level asks for, and the standing it asks for with them.</summary>
        public static readonly int[] BuildsFrom = { 0, 2, 5, 9 };

        public static readonly int[] StandingFrom = { 0, 30, 45, 60 };

        /// <summary>The level that builds and standing earn: the highest whose both asks are met.</summary>
        public static int For(int builds, int standing)
        {
            int level = 0;
            for (int i = 1; i < Levels; i++)
            {
                if (builds >= BuildsFrom[i] && standing >= StandingFrom[i])
                {
                    level = i;
                }
            }
            return level;
        }

        /// <summary>The floors a level is licensed to; a level out of range is the nearest.</summary>
        public static int FloorsOf(int level) => Floors[Math.Max(0, Math.Min(Levels - 1, level))];

        /// <summary>True when a building of these floors is within the level's licence.</summary>
        public static bool Allows(int level, int storeys) => storeys <= FloorsOf(level);

        /// <summary>What the next level asks for, or false at the top: its builds and its standing.</summary>
        public static bool Next(int level, out int builds, out int standing, out int floors)
        {
            builds = standing = floors = 0;
            if (level >= Levels - 1)
            {
                return false;
            }
            builds = BuildsFrom[level + 1];
            standing = StandingFrom[level + 1];
            floors = Floors[level + 1];
            return true;
        }

        /// <summary>
        /// The level the city's builder has, by the same rule as the player's: the builds it has done and its standing, with the builds it had behind it before the
        /// save began (an eighth of its standing), so that a well thought of builder is licensed for tall buildings from the first day.
        /// </summary>
        public static int Rival(int built, int reputation) => For(built + Math.Max(0, reputation) / 8, reputation);
    }
}
