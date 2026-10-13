using System;

namespace RealLifeOverhaul.Districts
{
    /// <summary>
    /// What the district view asks of the rest of the mod, one reading per district each. Every one is optional: one that
    /// nobody filled in gives no figure, and the view shows the district grey on that layer and leaves the line off its
    /// card. Wiring/DistrictLinks.cs fills them in once per city load. The view never calls another feature directly, and
    /// reads only when it refreshes: when the map is opened on a day it has not read yet, or a new day comes while the
    /// map is open.
    /// </summary>
    public static class DistrictBridges
    {
        /// <summary>A district's income level, 60 to 140 with 100 the city's middle; live tells whether it is the city's economy's (false: from the game's class shares). False if there is none.</summary>
        public delegate bool IncomeReader(string district, out int level, out bool live);

        /// <summary>What the district thinks of the player as an owner (0 to 100, 50 to start) and how well they are known there (0 to 100). False while Standing is off.</summary>
        public delegate bool StandingReader(string district, out int opinion, out int known);

        /// <summary>The player's power and water accounts at places in the district: how many there are, how many are supplied, and how many are cut off.</summary>
        public delegate bool SupplyReader(string district, out int accounts, out int supplied, out int cut);

        /// <summary>
        /// The internet in a district on a day: the providers that reach it (the player's own company counted where its
        /// network is open), how many of them have an outage that day, and the player's own network there: 0 none, 1 being
        /// dug (ready on readyDay), 2 open with share thousandths of the district's connections.
        /// </summary>
        public delegate bool InternetReader(string district, int day, out int providers, out int down, out int fibre, out int readyDay, out int share);

        /// <summary>
        /// The council in a district: building sites at work there now, its electorate, and the result of the last election
        /// counted there (the winner's name and per cent; null while there is none). False while civic life is off.
        /// </summary>
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
