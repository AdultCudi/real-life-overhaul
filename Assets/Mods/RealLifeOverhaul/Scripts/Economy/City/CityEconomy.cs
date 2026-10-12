using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The city's economy as the rest of the mod may read it. Read-only, game-free, and safe at any time: while the
    /// city's economy is off, not started yet or stopped by a fault, every member answers as a city with no economy of
    /// the mod's would (a steady state, the banks' own rates, prices at 100, every district at 100) and
    /// <see cref="MonthTurned"/> is not raised.
    ///
    ///     CityEconomy.Running                 true while the model is live
    ///     CityEconomy.State                   the cycle: Boom, Steady, Slowdown or Recession
    ///     CityEconomy.BasePoints              the central bank's rate in points on top of the banks' own (-2 to +6, in quarters)
    ///     CityEconomy.PriceIndex              the price index (100 at the start)
    ///     CityEconomy.InflationPercent        inflation in per cent a year (-1 to +12)
    ///     CityEconomy.DistrictIncome(name)    a district's income level, 60 to 140, by the game's own name for the neighbourhood
    ///     CityEconomy.MonthTurned             raised once when a month of the model turns, after the new figures are in place
    ///
    /// Nothing here may be written by a reader; the director alone sets <see cref="Live"/>.
    /// </summary>
    public static class CityEconomy
    {
        /// <summary>The state in use, or null. Set by the city's economy director only.</summary>
        internal static CityState Live { get; set; }

        /// <summary>The strength in use (the option's step); the catalogue's default while nothing is live.</summary>
        internal static int Level { get; set; } = MacroCatalogue.LevelDefault;

        public static bool Running => Live != null;

        public static CycleState State => Live != null ? Live.Cycle : CycleState.Steady;

        public static float BasePoints => Live != null ? CityModel.BasePoints(Live) : 0f;

        public static float PriceIndex => Live != null ? Live.Index / 1000f : MacroCatalogue.IndexStart / 1000f;

        /// <summary>What the game's own costs are adjusted by, in tenths of a point (the price level of every price of the mod's: Core/Prices).</summary>
        public static int CostFactor => Live != null ? CityModel.Costs(Live) : PriceRules.Start;

        public static float InflationPercent => Live != null ? Live.Inflation / 100f : MacroCatalogue.InflationTarget / 100f;

        /// <summary>The month of the state the cycle is in, from 1.</summary>
        public static int MonthOfState => Live != null ? Live.MonthsIn : 1;

        /// <summary>A district's income level by the game's own name for the neighbourhood ("ba:neighborhood_..."); 100 for one that is not known or while the economy is off.</summary>
        public static float DistrictIncome(string name)
        {
            CityDistrict d = Live != null && name != null ? Live.District(name) : null;
            return d != null ? d.Income / 10f : MacroCatalogue.IncomeMiddle / 10f;
        }

        /// <summary>A month of the model has turned. Raised on the game's day event, once, after the month's figures are in place. A handler must not throw; one that does is not allowed to stop the others.</summary>
        public static event Action MonthTurned;

        internal static void RaiseMonthTurned(Action<Exception> fault)
        {
            Action handlers = MonthTurned;
            if (handlers == null)
            {
                return;
            }
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    fault?.Invoke(e);
                }
            }
        }
    }
}
