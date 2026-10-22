namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The levels of the player's bank (its tier in the code, its level in the texts), game-free so the offline checks
    /// read the same table: a community bank, founded cheaply in the middle of a game and held to a few branches and a
    /// small book; a regional bank; and a national bank, which is the bank as it was before levels, with every figure of
    /// OwnBankCatalogue and no limit but the regulator's ratio. Money in cents at the start's prices; each figure has its
    /// value before the end game's scale and at it (Core/EndScale), mixed by the share as the bank's other figures are.
    /// Moving up costs the next level's licence less the licence of the level held, so a bank climbed to the top has paid
    /// what a national licence costs. The owner's pay (<see cref="PayShareMode"/>, <see cref="PayFixedMode"/>) is chosen
    /// in the bank's app. EconomyCheck (BankTierChecks) holds the table to its aims: the first level paid back within
    /// <see cref="PaybackMonthsMost"/> months, each move up paid back by what it adds.
    /// </summary>
    public static class BankTiers
    {
        public const int Community = 0, Regional = 1, National = 2, Count = 3;
        public const int Unknown = -1;   // a line written before levels: fitted to the bank's size when it is read (OwnBankRules.FitTier)

        public static readonly string[] NameKeys = { "reallife_ob_tier_community", "reallife_ob_tier_regional", "reallife_ob_tier_national" };

        // The licence of each level, once, and the least capital a bank of that level holds.
        public static readonly long[] LicenceCents = { 15000000L, 80000000L, OwnBankCatalogue.LicenceCents };                  // $150,000, $800,000, $3,000,000
        public static readonly long[] LicenceCentsScale = { 50000000L, 500000000L, OwnBankCatalogue.LicenceCentsScale };       // $500,000, $5,000,000, $60,000,000
        public static readonly long[] CapitalCents = { 30000000L, 70000000L, OwnBankCatalogue.CapitalLeastCents };             // $300,000, $700,000, $5,000,000
        public static readonly long[] CapitalCentsScale = { 100000000L, 400000000L, OwnBankCatalogue.CapitalLeastCentsScale };  // $1,000,000, $4,000,000, $150,000,000

        // What each level may hold: its branches; its loans, performing and in arrears; its deposits. NoLimit: none but the ratio.
        public const long NoLimit = long.MaxValue;
        public static readonly int[] BranchesMost = { 2, 6, BranchCatalogue.BranchesMost };
        public static readonly long[] LoansCents = { 250000000L, 1200000000L, NoLimit };           // $2,500,000, $12,000,000
        public static readonly long[] LoansCentsScale = { 1500000000L, 7000000000L, NoLimit };     // $15,000,000, $70,000,000
        public static readonly long[] DepositsCents = { 400000000L, 2000000000L, NoLimit };        // $4,000,000, $20,000,000
        public static readonly long[] DepositsCentsScale = { 2500000000L, 10000000000L, NoLimit }; // $25,000,000, $100,000,000

        // A move up: game months the bank has held its level without a breach (its record), by the level moved up to.
        public static readonly int[] RecordMonths = { 0, 6, 12 };

        // What each level costs to run: the banking hall a day before the scale (OwnBankCatalogue.StaffDayCents at the
        // top); at scale, its core systems a day and the compliance officers it keeps at the least.
        public static readonly long[] HallDayCents = { 40000L, 80000L, OwnBankCatalogue.StaffDayCents };       // $400, $800, $1,500
        public static readonly long[] SystemsDayCents = { 150000L, 800000L, OwnBankCatalogue.SystemsDayCents }; // $1,500, $8,000, $40,000
        public static readonly int[] ComplianceLeast = { 2, 5, OwnBankCatalogue.ComplianceLeast };

        // The owner's pay: a share of each day's profit, in steps, or a fixed sum a day; the rest stays in the capital.
        public const int PayShareMode = 0, PayFixedMode = 1;
        public const int PayStep = 50;                    // thousandths: the slider moves in steps of 5%
        public const int PayShareMost = 1000;             // thousandths: all of the day's profit
        public const long PayFixedMostCents = 100000000000L;   // the most a fixed pay may be set to: $1,000,000,000 a day

        // The aims EconomyCheck holds the table to.
        public const int PaybackMonthsMost = 12;          // game months a community bank takes at most to pay its licence back from its profit
    }
}
