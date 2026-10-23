namespace RealLifeOverhaul.Utilities
{
    /// <summary>One internet provider: who it is, where it reaches, how reliable it is and its plans.</summary>
    public sealed class Provider
    {
        public string Id;
        public string NameKey;
        public string KindKey;           // cable, fibre, wireless, copper
        public string[] Districts;       // the game's neighbourhoods it reaches
        public int OutagePerMille;       // the chance in a thousand of an outage on a day
        public int[] Mbps;               // its plans, slowest first
        public int[] MonthCents;         // what each costs a month on a contract, at the start's prices (a home's)
    }

    /// <summary>
    /// The city's rival internet providers at the start's prices, with plans, reach and reliability, billed with power and
    /// water. Checked offline (EconomyCheck).
    /// </summary>
    public static class InternetCatalogue
    {
        private static readonly string[] City =
        {
            "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity",
            "ba:neighborhood_lowermanhattan", "ba:neighborhood_midtown", "ba:neighborhood_murrayhill"
        };

        private static readonly string[] Everywhere =
        {
            "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity",
            "ba:neighborhood_lowermanhattan", "ba:neighborhood_midtown", "ba:neighborhood_murrayhill", "ba:neighborhood_thehamptons"
        };

        public static readonly Provider[] Providers =
        {
            new Provider
            {
                Id = "kestrel", NameKey = "reallife_net_provider_kestrel", KindKey = "reallife_net_kind_cable", Districts = City, OutagePerMille = 30,
                Mbps = new[] { 100, 300, 1000 }, MonthCents = new[] { 4500, 6500, 8500 }
            },
            new Provider
            {
                Id = "brightvein", NameKey = "reallife_net_provider_brightvein", KindKey = "reallife_net_kind_fibre", OutagePerMille = 5,
                Districts = new[] { "ba:neighborhood_lowermanhattan", "ba:neighborhood_midtown", "ba:neighborhood_murrayhill" },
                Mbps = new[] { 300, 1000, 2000 }, MonthCents = new[] { 6000, 8000, 11000 }
            },
            new Provider
            {
                Id = "aerowave", NameKey = "reallife_net_provider_aerowave", KindKey = "reallife_net_kind_wireless", Districts = Everywhere, OutagePerMille = 60,
                Mbps = new[] { 50, 150 }, MonthCents = new[] { 3500, 5000 }
            },
            new Provider
            {
                Id = "copperhill", NameKey = "reallife_net_provider_copperhill", KindKey = "reallife_net_kind_copper", Districts = Everywhere, OutagePerMille = 40,
                Mbps = new[] { 25, 75 }, MonthCents = new[] { 2500, 3800 }
            }
        };

        /// <summary>The player's own company as a provider, reaching where its network is open (BroadbandRules.Apply).</summary>
        public static readonly Provider Own = new Provider
        {
            Id = "own", NameKey = BroadbandCatalogue.OwnNameKey, KindKey = "reallife_net_kind_fibre", Districts = new string[0],
            OutagePerMille = BroadbandCatalogue.OutageBase, Mbps = BroadbandCatalogue.Mbps, MonthCents = new int[BroadbandCatalogue.Mbps.Length]
        };

        /// <summary>The company's place among the providers: after the rivals.</summary>
        public static int OwnIndex => Providers.Length;

        public const int BusinessShare = 1800;   // thousandths of a home's price that a business plan costs (repairs first)
        public const int MonthlyShare = 1200;        // ...and month by month, without a contract
        public const int ContractMonths = 12;        // a contract runs this many of the game's months
        public const int ExitShare = 500;            // thousandths of each month left that leaving a contract early costs
        public static int InstallFee => Prices.At(60);   // without a contract; free on one
        public const int InstallDays = 2;            // an engineer installs it this many days after it is bought
        public const int GraceDays = 7;   // days a save meeting the rule for the first time lets its businesses that need a connection open without one

        // an outage: so many hours, from an hour of the day (a business plan is repaired in half the time)
        public const int OutageLeastHours = 2, OutageMostHours = 8;
        public const int OutageFirstHour = 6, OutageLastHour = 20;

        // a provider's month, thousandths of its list: a walk of a few per cent a month, a promotion now and then
        public const int PromoLeast = 800, PromoMost = 1100, PromoStep = 40, PromoChancePerMille = 120, PromoCut = 150;

        /// <summary>The kinds of business that cannot open without a connection: web design, software, and the headquarters.</summary>
        public static readonly string[] Needs =
        {
            "ba:businesstype_graphicdesigner", "ba:businesstype_webdevelopmentagency", "ba:businesstype_headquarters",
            "reallife:businesstype_softwarestudio", "reallife:businesstype_gamestudio", "reallife:businesstype_itconsultancy", "reallife:businesstype_cybersecurity",
            "reallife:businesstype_ailab"
        };
    }
}
