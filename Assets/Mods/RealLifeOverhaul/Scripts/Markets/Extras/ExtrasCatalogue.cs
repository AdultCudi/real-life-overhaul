namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Every figure of the stock market's extras: seeded and reverse splits and the founder's own, the index fund, block
    /// trades, a bank of the player's on the market and what the rival tycoons do with all of them. Game-free; read by the
    /// Guide and the offline checks. Money is whole units unless a name says cents; shares of something are bp.
    /// </summary>
    public static class ExtrasCatalogue
    {
        // ---- splits
        public const int SplitLevelLeastCents = 18000;   // a board's own level: each company splits over a price between this...
        public const int SplitLevelStepCents = 1000;     // ...in steps of this...
        public const int SplitLevelSteps = 12;           // ...up to the market's old level (MarketCatalogue.SplitAboveCents), by its id
        public const int ReverseAfterDays = 10;          // market days running under a unit before a sound company puts its shares together
        public const double ReverseDistressMost = 0.5;   // a company nearer failing than this does not: the penny rule takes it
        public const int ReverseTargetCents = 1000;      // at the start's prices: the price a consolidation aims over
        public static readonly int[] ReverseRatios = { 2, 5, 10, 20, 50 };   // old shares for one new: the smallest that reaches the target
        public static readonly int[] FounderForward = { 2, 3, 5, 10 };       // the founder's choices: new shares for one old...
        public static readonly int[] FounderReverse = { 2, 5, 10 };          // ...and old shares for one new
        public const int FounderSplitLeastCents = 500;   // at the start's prices: no split takes a share under this
        public const int FounderReverseMostCents = 15000;   // no consolidation takes a share over this: under every board's level, so it is not split again at once
        public const int SplitsKept = 48;                // splits remembered for the charts' notes
        public const int RatioMost = 50;                 // the largest ratio either way a save's line may hold

        // ---- the index fund
        public const int FundId = 9001;                  // no company has this id: the fund's number where one is needed
        public const string FundName = "City Index Fund";   // a name of its own, as a company's: never translated
        public const string FundTicker = "CIF";
        public const int FundUnitDivisor = 10;           // a unit is the index's points over this, in money: 100 a unit at 1,000 points
        public const int FundFeeBp = 30;                 // a year, taken out of the fund's worth a market day at a time
        public const int FundHoldingBp = 200;            // the most of the whole market's float one holder's units may stand for
        public const int FundDuesMost = 24;              // payment days kept at once (a company pays within a few days of its record)

        // ---- block trades
        public const int BlockListedDays = 20;           // market days a company has traded before a block is dealt in it
        public const int DiscloseStepBp = 500;           // a holding is told to the market at each 5% it passes, up or down
        public const int DisclosuresKept = 64;

        // ---- a bank of the player's own on the market
        public const string BankPlace = "#bank";         // the place of the bank's stock: no business's place is written like it
        public const int BankAgeYears = 1;               // game years the bank has run before it may list
        public const int BankQuartersLeast = 4;          // complete quarters of its own books
        public const int BankProfitQuartersLeast = 3;    // ...of the last four in profit
        public const double BankBookMost = 3.0;          // its worth is never counted over this many times its own capital
        public static readonly int[] BankFloatSteps = { 1000, 1500, 2000, 2500, 3000, 4000 };   // bp of the bank offered
        public const int BankFloatDefault = 2000;
        public const int BankDaysKept = 40;              // days of the bank's own takings and profit kept for the market's mornings
        public const int BankQuartersKept = 12;

        // ---- the rival tycoons
        public const int RivalEveryDays = 30;            // a tycoon looks at the market once in this many days, on a day of their own
        public const int RivalFundBp = 150;              // of their worth, what a tycoon keeps in the fund at most
        public const int RivalFundStepBp = 50;           // ...bought this much of their worth at a time
        public const int RivalBlockBp = 100;             // of their worth, a block of another company
        public const int RivalStakesMost = 4;            // companies a tycoon holds blocks of at once
        public const int RivalStakeMostBp = 500;         // of one company, a tycoon's blocks together: under the first disclosure but for a rival's holding
        public const int RivalArmChance = 250;           // per mille a look: a tycoon whose holding is listed puts a bank or a lab of its own on the market
        public const int RivalArmWorthBp = 800;          // of their worth, what the arm is worth when it lists
        public const int RivalArmsMost = 1;              // arms a tycoon lists in all
        public const int RivalSellChance = 200;          // per mille a look: a tycoon sells a block they hold
        public static readonly string[] ArmWords = { "Bank", "Labs" };   // the arm's name after the holding's first word: never translated

        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
