using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>Where a height variance stands: not asked, before the council, allowed or refused.</summary>
    public enum VarianceState
    {
        None = 0,
        Pending = 1,
        Granted = 2,
        Refused = 3
    }

    /// <summary>
    /// The height variance hearing: a builder asks City Hall's council for floors above what the building code allows on a plot. The council hears it the way it hears a
    /// change of zoning (CouncilZoning's members, at their sitting) with a suit worked out here from how far over the plans go, their quality, the builder's standing and
    /// licence, the building code in force and what the district asks for. The player's company and the city's builders ask by this one rule. Game-free and deterministic.
    /// </summary>
    public static class VarianceRules
    {
        public const int OverMost = 4;           // floors over the code that may be asked for
        public const int SuitBase = 600;         // thousandths: the planners' view of a variance one floor over for a middling builder
        public const int PerFloor = 130;         // thousandths less for each floor asked over the first
        public const int FeePermille = 2000;     // of the zoning fee: what the hearing costs the player's company

        /// <summary>The floors a building is over the code's limit; 0 when it is within it.</summary>
        public static int Over(int storeys, int limit) => Math.Max(0, storeys - limit);

        /// <summary>True when a variance may be asked for these floors: over the limit, and by no more than the council hears.</summary>
        public static bool Askable(int over) => over >= 1 && over <= OverMost;

        /// <summary>
        /// The planners' view of a variance in thousandths, 50 to 950: less for each floor over the first; more for good plans, a respected builder, a higher licence
        /// (0 to 3), a code that favours building (-1 to 1) and a use the district asks for (demand 0 to 100, or -1 where it is not known).
        /// </summary>
        public static int Suit(int over, int quality, int standing, int level, int permits, int demand)
        {
            int suit = SuitBase - Math.Max(0, over - 1) * PerFloor + (BuildBounds(quality) - 60) * 3 + (BuildBounds(standing) - 50) * 2 + BuildCatalogue.Clamp(level, 0, LevelRules.Levels - 1) * 40
                       + BuildCatalogue.Clamp(permits, -1, 1) * 60 + (demand >= 70 ? 60 : demand >= 0 && demand < 40 ? -60 : 0);
            return BuildCatalogue.Clamp(suit, 50, 950);
        }

        private static int BuildBounds(int value) => BuildCatalogue.Clamp(value, 0, 100);

        /// <summary>The planners' own vote where no council answers: the same suit as the odds, drawn from the save's seed and the matter.</summary>
        public static bool Planners(int seed, string matter, int suit)
        {
            return BuildRandom.Of(seed, BuildRandom.Hash(matter), 0x5A17).Range(0, 1000) < BuildCatalogue.Clamp(suit, 0, 1000);
        }

        /// <summary>The matter a hearing is about, for the council's draws: the lot and the floors asked.</summary>
        public static string Matter(string lot, int floors) => "var:" + lot + ":" + floors;

        /// <summary>What the hearing costs a company, from the zoning fee.</summary>
        public static long Fee(long zoneFee) => Math.Max(0L, zoneFee) * FeePermille / 1000L;
    }
}
