namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// How hard the player's bank is held to the city's economy (the option "Bank risk"): Off is the bank as it was before this layer, every
    /// figure unchanged; Standard and Hard add credit risk that follows the cycle, one sector in trouble now and then, a concentration charge,
    /// savers who leave faster than they come, a regulator whose watch has teeth, and the tools to meet a run. Game-free, so the offline
    /// checks and the Guide read the same table. Every table is by level (Off, Standard, Hard); a row of the Off level is the neutral figure.
    /// </summary>
    public static class BankRisk
    {
        public const int Off = 0, Standard = 1, Hard = 2, Count = 3;
        public static readonly string[] NameKeys = { "reallife_ob_risk_0", "reallife_ob_risk_1", "reallife_ob_risk_2" };

        // Credit risk follows the economy's state (CycleState: boom, steady, slowdown, recession), thousandths of the default rate.
        // A firm's defaults already move with business pressure; these add the cycle itself, which the bank carries whatever it lends.
        public static readonly int[][] FirmCycle =
        {
            new[] { 1000, 1000, 1000, 1000 },
            new[] { 900, 1000, 1250, 1700 },   // a recession makes a firm's loan two thirds likelier to fail: the usual swing of a business cycle
            new[] { 850, 1000, 1500, 2300 }
        };
        public static readonly int[][] PeopleStress =
        {
            new[] { 1000, 1000, 1000, 1000 },
            new[] { 1000, 1000, 1100, 1250 },   // people's defaults already swing with the cycle (OwnBankCatalogue.PeopleCycle): only a little more on top
            new[] { 950, 1000, 1250, 1600 }
        };

        // A written-off loan comes back less in a slump: what is recovered, thousandths of the usual.
        public static readonly int[][] RecoveryCycle =
        {
            new[] { 1000, 1000, 1000, 1000 },
            new[] { 1000, 1000, 900, 750 },    // security fetches less when everyone is selling
            new[] { 1000, 1000, 800, 600 }
        };

        // One kind of borrower in trouble for a month, drawn from the bank's seed: its chance a month, thousandths, and what it does to that kind's defaults.
        public static readonly int[] ShockChance = { 0, 80, 150 };       // about one month in twelve on Standard, one in seven on Hard
        public static readonly int[] ShockDefault = { 1000, 1800, 2600 };
        public const int ShockWarnShare = 400;                            // the last two fifths of the month before it: time to look at the book, not to avoid it
        public const int ShockSalt = 7919;                                // keeps a shock's draw apart from the month's luck (OwnBankRules.Noise)

        // Concentration: too much of the book in one kind of borrower.
        public static readonly int[] ConcLimit = { 1000, 500, 450 };     // thousandths of the book one kind may be before it is charged
        public const int ConcFloorShare = 1000;                          // a book smaller than the capital is too young to be concentrated: thousandths of the capital
        public static readonly int[] ConcWeight = { 0, 400, 600 };       // thousandths added to the risk weight of what is over the limit
        public static readonly int[] ConcDefault = { 0, 1500, 2500 };    // more defaults, thousandths for a whole book over the limit: one trade's troubles are shared

        // Liquidity.
        public static readonly int[] ReserveShare = { OwnBankCatalogue.ReserveShare, 100, 120 };   // reserves kept against the deposits, thousandths
        public static readonly int[] LendRatio = { OwnBankCatalogue.LendRatio, 140, 150 };          // the ratio under which it lends and buys no further: its warning line
        public static readonly int[] FlightSpeed = { 1000, 2000, 3000 };  // savers leave this many times faster than they come, thousandths
        public const int FlightConfidence = 650;                           // under this confidence savers begin to leave on their own
        public static readonly int[] FlightShare = { 0, 8, 16 };           // thousandths of a balance a day at no confidence at all
        public const int FlightFirms = 1500;                               // businesses leave first: thousandths of people's flight
        public static readonly int[] RunStart = { OwnBankCatalogue.RunStart, 520, 560 };   // confidence under which a run begins
        public static readonly int[] RunEnd = { OwnBankCatalogue.RunEnd, 620, 650 };       // and over which it ends
        public static readonly int[] RunMult = { 1000, 1000, 1400 };      // a day's withdrawals in a run, thousandths of the usual
        public const int RunRateMost = 600;                                // paying savers well holds back this much of a run's outflow, thousandths...
        public const int RunRatePer = 250;                                 // ...for each point over the rivals' rate
        public const int HaircutLiquid = 150;                              // the fund and the book count for this much less than their value when cash is needed
        public const int CoverWarnDays = 20, CoverRunDays = 10;            // tenths of a day of a run's outflow the cash covers: under these the city loses heart
        public const int CoverMargin = 5;                                  // tenths of a day more than the warning that the bank keeps in cash, so that rounding never tips it under
        public const int CoverWarnTrust = 850, CoverRunTrust = 700;        // thousandths of confidence kept at each
        public const int LossTrust = 850;                                  // a month's loss of this share of the capital costs this much confidence...
        public const int LossShare = 50;                                   // ...(thousandths of the capital)

        // The regulator's ladder: the warning line is LendRatio above; the watch's lines are the catalogue's.
        public static readonly int[] WatchLend = { 1000, 500, 250 };      // thousandths of the new lending a watched bank may do

        // Meeting a run.
        public static readonly int[][] SaleDiscount =                      // loans sold to other banks, thousandths of their face, by the cycle
        {
            new[] { 0, 0, 0, 0 },
            new[] { 40, 60, 100, 160 },
            new[] { 60, 90, 150, 240 }
        };
        public const int SaleMost = 250;                                   // of a kind's performing loans, thousandths, in one sale
        public const int SaleRun = 60;                                     // buyers know a bank in a run is a seller under pressure: this much more off, thousandths
        public static readonly int[] InterbankMost = { 0, 400, 300 };     // borrowed from other banks: thousandths of the capital
        public static readonly int[] InterbankSpread = { 0, 150, 250 };   // over the bank rate, hundredths of a point
        public const int InterbankRun = 300;                               // dearer while a run is on, hundredths of a point...
        public static readonly int[] InterbankCycle = { 0, 0, 100, 250 }; // ...and by the cycle
        public const int InterbankFeeBp = 50;                              // an arrangement fee on what is borrowed, hundredths of a per cent
        public const int InterbankRoll = 400;                              // what a loan not repaid on its day costs more a year, hundredths of a point
        public const int InterbankMonths = 1;                              // game months a loan runs

        // A bank taken by the regulator sells for less in a slump: what its performing loans fetch, thousandths of the usual.
        public static readonly int[][] WindCycle =
        {
            new[] { 1000, 1000, 1000, 1000 },
            new[] { 1000, 1000, 950, 900 },
            new[] { 1000, 950, 880, 800 }
        };

        // The warning signs the page names.
        public const int SignConfidence = 620;                             // a confidence under this is a sign
        public const int SignRatioRoom = 20;                               // a ratio within this of the warning line (thousandths) is a sign

        public static int Level(int risk) => risk < Off ? Off : risk > Hard ? Hard : risk;
    }

    /// <summary>The parts of a risky bank that the page names as warning signs. Bit flags, never saved.</summary>
    public static class BankSign
    {
        public const int Confidence = 1;   // the city's confidence in the bank is falling
        public const int Cover = 2;        // its cash would not last a run
        public const int Ratio = 4;        // its capital ratio is near the line it must hold
        public const int Shock = 8;        // a kind of borrower is about to be in trouble
        public const int Flight = 16;      // savers are leaving of their own accord
        public const int Concentrated = 32;
        public const int All = 63;
    }
}
