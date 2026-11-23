namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The leased lines the carriers sell to a data centre: business fibre in steps from 5 to 100 Gbps, at the start's prices (money in cents, scaled by the price
    /// level like every plan). Each figure says why it is what it is. A rack moves 100 Mbps and an AI rack 400 (Tech/Infra/InfraLimits), so a line carries ten
    /// standard racks for each Gbps. Checked offline (EconomyCheck, --leased).
    /// </summary>
    public static class LeasedLineCatalogue
    {
        public const int Steps = 4;

        /// <summary>Mbps a standard rack and an AI rack move (Tech/Infra/InfraLimits.MbpsPerRack and MbpsPerGpuRack, held equal by a check): a line's size over it is the racks it carries.</summary>
        public const int RackMbps = 100, GpuRackMbps = 400;

        /// <summary>The steps' speeds, Mbps: what a hall of 50, 100, 400 or 1000 racks moves, so each step is a size of hall.</summary>
        public static readonly int[] Mbps = { 5000, 10000, 40000, 100000 };

        /// <summary>
        /// A month's price by step, cents: 2000, 1600, 1400 and 1200 dollars for each Gbps, the larger the cheaper as carriers sell it. A standard rack takes 100 Mbps, so the
        /// line costs 200, 160, 140 and 120 dollars a rack a month: at most 5% of what a rack of the old kind takes in a month in the cloud ($4,200), and far less of a new rack's.
        /// </summary>
        public static readonly int[] MonthCents = { 1000000, 1600000, 5600000, 12000000 };

        /// <summary>Thousandths of a month's price the installation costs where fibre already runs: the carrier's engineers, the cross connect and the equipment.</summary>
        public const int InstallShare = 1500;

        /// <summary>Days the installation takes where fibre runs, by step: a bigger line needs more equipment ordered and tested.</summary>
        public static readonly int[] InstallDays = { 7, 10, 14, 21 };

        // How much a district has today decides what the line costs to bring: Brightvein's fibre runs in three districts and Kestrel's cable in three more.
        public const int Fibre = 0, Cable = 1, Dig = 2, Classes = 3;

        /// <summary>Thousandths of the installation fee by what the district has: fibre in the street, only cable (the carrier lights a fibre along it), nothing (a new route is dug).</summary>
        public static readonly int[] FeeShare = { 1000, 1500, 2500 };

        /// <summary>Days added to the installation by what the district has: laying a fibre in an existing duct takes a week, digging a new route two.</summary>
        public static readonly int[] ExtraDays = { 0, 7, 14 };

        // The player's own broadband company may carry its own data centre in a district it has dug (InternetCatalogue.OwnLine). The line is the carriers' sizes and steps, but it
        // runs on the company's own fibre, so the carrier's dig is never needed: the district counts as fibre in the street. Its price is what the company pays for the capacity.

        /// <summary>
        /// Thousandths of a carrier's month price the company's own line costs the data centre: the company buys its upstream capacity from the carriers in bulk, a fifth under
        /// what they sell a hall, and carries the hall on its own fibre at no more than that. Never under three quarters of the carriers' price (held by a check), so the
        /// carriers' prices still mean something and a network is no way round them: a rack costs $128 a month on a 10 Gbps line here, $160 from a carrier, against $4,200 of takings.
        /// </summary>
        public const int OwnPriceShare = 800;

        /// <summary>Thousandths of a carrier's installation fee for a line on the company's own fibre: its own engineers lay it in its own street, with no carrier's margin.</summary>
        public const int OwnInstallShare = 600;
    }
}
