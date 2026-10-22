using System;

namespace RealLifeOverhaul.Districts
{
    /// <summary>
    /// What the district view asks of the rest of the mod, one reading per district, filled by Wiring/DistrictLinks.
    /// One left unset gives no figure: the district shows grey on that layer. Read once a day at most.
    /// </summary>
    public static class DistrictBridges
    {
        /// <summary>A district's income level, 60 to 140 with 100 the middle; live if the city economy's, else from class shares.</summary>
        public delegate bool IncomeReader(string district, out int level, out bool live);

        /// <summary>The district's opinion of the player as an owner (0 to 100, 50 to start) and how known they are. False while Standing is off.</summary>
        public delegate bool StandingReader(string district, out int opinion, out int known);

        /// <summary>The player's power and water accounts in the district: how many, how many supplied, how many cut off.</summary>
        public delegate bool SupplyReader(string district, out int accounts, out int supplied, out int cut);

        /// <summary>
        /// The internet in a district on a day: the providers that reach it, how many are down, and the player's own
        /// network: 0 none, 1 being dug (ready on readyDay), 2 open with share thousandths of the connections.
        /// </summary>
        public delegate bool InternetReader(string district, int day, out int providers, out int down, out int fibre, out int readyDay, out int share);

        /// <summary>The council in a district: sites at work, its electorate, the last result (winner null for none). False while civic life is off.</summary>
        public delegate bool CouncilReader(string district, out int sites, out int electorate, out string winner, out int percent);

        public static IncomeReader Income;

        /// <summary>The city's economy's change to a district's footfall, per cent of customers; null while it is off.</summary>
        public static Func<string, int?> Footfall;

        /// <summary>What a district's customers bear against the game's price, thousandths; null while the city's economy is off.</summary>
        public static Func<string, int?> Bears;

        public static StandingReader Standing;
        public static SupplyReader Supply;
        public static InternetReader Internet;
        public static CouncilReader Council;

        public static void Reset()
        {
            Income = null;
            Footfall = null;
            Bears = null;
            Standing = null;
            Supply = null;
            Internet = null;
            Council = null;
        }
    }
}
