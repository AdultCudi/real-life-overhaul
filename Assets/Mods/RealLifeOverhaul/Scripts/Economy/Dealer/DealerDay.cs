using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The dealership's morning as the director runs it, for the interface conversion's additions: the market's new day, the other dealers' cars put
    /// out and re-priced, and each lot's day closed into its views and reports. Game-free: the director calls it from its hour, and the run checks
    /// call the same method over a hundred days.
    /// </summary>
    public static class DealerDay
    {
        /// <summary>The day's morning if the market has not turned for it yet (once a day, whenever the first hour of it is worked); true when it ran.</summary>
        public static bool Morning(DealerState s, int day, IList<MarketModel> models, IList<string> colours, int level, bool closeYards = true)
        {
            if (s.MarketDay == day)
            {
                return false;
            }
            DealerRules.Morning(s, day, models, colours, level);
            RivalRules.Morning(s, day, models, level);
            for (int y = 0; closeYards && y < s.Yards.Count; y++)
            {
                DealerRules.CloseDay(s, s.Yards[y], day);
            }
            return true;
        }
    }
}
