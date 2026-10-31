using System;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// What the districts' markets ask of the rest of the mod (LifeDirector.Market): the living population's pull for a use in one of the band's districts,
    /// -100 to 100, or -1000 for none counted (the market then works from Manhattan's fixed ratios as before). Filled in by Wiring/PopulationLinks; asked
    /// once a game day for each district and use.
    /// </summary>
    public static class MarketBridges
    {
        /// <summary>A district's pull for a use (homes, shops, offices, warehouses: 0 to 3) by its id; -1000 for none.</summary>
        public static Func<string, int, int> PopulationPull;

        public static void Reset()
        {
            PopulationPull = null;
        }
    }
}
