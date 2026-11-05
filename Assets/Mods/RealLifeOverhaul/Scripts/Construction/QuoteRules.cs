using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// What a builder's quote means to the client who hires it: the risk it is late or poor (from how busy the builder is, its standing and how far its quoted days
    /// ask more of its crews than the city's pace gives, never a new figure of state) and the sentences that set one quote against another in money, days and the
    /// rent lost while a building waits. Game-free.
    /// </summary>
    public static class QuoteRules
    {
        public const int Low = 0, Medium = 1, High = 2;

        /// <summary>
        /// The risk of a quote: the spread of luck its crews' pace has (the less standing, the wider, from the catalogue's luck range), twenty points for each of the
        /// works it already has on its hands, and a share of how far its quoted pace asks more than the city's. Under 70 is low, under 120 medium, else high.
        /// </summary>
        public static int Risk(int busy, int reputation, int quotedPaceThousandths)
        {
            int least = BuildCatalogue.HireLuckLeast + BuildCatalogue.Clamp(reputation, 0, 100) * (BuildCatalogue.HireLuckMost - BuildCatalogue.HireLuckLeast) / 200;
            int spread = (BuildCatalogue.HireLuckMost - least) / 2;
            int push = Math.Max(0, quotedPaceThousandths - 1000) / 5;
            int score = spread + Math.Max(0, busy) * 20 + push;
            return score < 70 ? Low : score < 120 ? Medium : High;
        }

        /// <summary>The rent a building loses in the days a quote takes over another's, a week's rent over seven: 0 for none.</summary>
        public static long RentLost(long weeklyRent, int days) => weeklyRent > 0L && days > 0 ? weeklyRent * days / 7L : 0L;
    }
}
