namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Every figure of the player's own bank, late in a game and very costly: the city's banking licence (run from the
    /// player's headquarters) and the capital the regulator wants paid in before a single saver is taken; then the
    /// city's savings won from the rival banks (the game's own, BankTerms.Banks) by the rate paid on them, against how far
    /// the city trusts the bank; loans to the city's people and businesses, priced by risk against what the rival banks
    /// (and, for those no bank lends to, the private lender) charge; a fund that puts a share of the deposits into the
    /// Stocks app's companies within rules; and a regulator that checks the capital ratio every day, fines a breach left
    /// standing and closes a bank that cannot be saved. Money in cents of the start's prices unless said (charged at
    /// today's, Core/Prices); rates in hundredths of a per cent a year; shares and factors in thousandths. Game-free,
    /// checked by EconomyCheck (OwnBankChecks).
    /// </summary>
    public static class OwnBankCatalogue
    {
        // ---------------------------------------------------------------- the licence and the capital

        public const long LicenceCents = 300000000L;       // the city's banking licence, once: $3,000,000
        public const long CapitalLeastCents = 500000000L;  // the capital the regulator wants paid in at the least: $5,000,000
        public const long CapitalStepCents = 10000000L;    // what is put in or taken out is a whole number of these: $100,000

        // ---------------------------------------------------------------- the regulator

        public const int MinRatio = 100;        // capital against risk-weighted assets, thousandths: 10% is the law
        public const int WatchRatio = 125;      // under 12.5% the regulator watches the bank (and the city hears of it)
        public const int LendRatio = 130;       // the bank lends and buys no further than would bring it under 13%, clear of the watch line
        public const int ReserveShare = 80;     // reserves kept against the deposits, thousandths: no new loan out of these
        public const int CureMonths = 1;        // a breach is to be put right within this many of the game's months...
        public const int StrikesClose = 2;      // ...each deadline missed is fined; the second one closes the bank
        public const int FineShare = 100;       // a fine is a tenth of the capital the bank is short of...
        public const long FineLeastCents = 5000000L;   // ...and never under $50,000

        // The risk weights the regulator counts each asset at, thousandths (reserves at the central bank count nothing).
        public static readonly int[] LoanWeight = { 1500, 1000, 750, 1000, 1000 };   // by Segments
        public const int ArrearsWeight = 1500;
        public const int FundWeight = 2500;

        // A bank that is closed is wound up: what its assets fetch, thousandths, and the regulator's charge on what is left.
        public const int WindPerforming = 850, WindArrears = 250, WindFund = 970, WindFee = 100;

        // ---------------------------------------------------------------- the savers

        public const int Savers = 2;            // the city's people and its businesses
        public const int People = 0, Firms = 1;
        public static readonly string[] SaverKeys = { "reallife_ob_saver_people", "reallife_ob_saver_firms" };

        // The city's savings at the start's prices, all with the rival banks before the player's opens.
        public static readonly long[] SavingsCents = { 40000000000L, 25000000000L };   // $400,000,000 and $250,000,000

        public const int CityRateStart = 300;   // the bank rate before the central bank moves it, as the paper and the Stocks app print it (Wiring/LedgerLinks)
        public const int RivalSaverMargin = 150;      // the rival banks pay savers the bank rate less this...
        public const int RivalSaverLeast = 25;        // ...never under a quarter of a point
        public const int SaverOffsetLeast = -200, SaverOffsetMost = 400, SaverOffsetStep = 25;   // what the player pays against them

        // How the city chooses where to save. Each rival holds HoldRivals, the bank its OwnHold grown with the city's trust;
        // the bank's by its rate against the rivals', e to the Pull thousandths for each point over. Each day a balance
        // moves Follow thousandths of a month's way to its weight's share of the city's savings.
        public const int HoldRivals = 1000;
        public static readonly int[] OwnSaverHold = { 70, 90 };          // people trust a name more slowly; firms chase the rate
        public static readonly int[] SaverPull = { 300, 450 };
        public static readonly int[] SaverFollow = { 150, 250 };
        public static readonly int[] RunShare = { 30, 60 };              // a day's withdrawals in a run, thousandths of the balance
        public const int AnswerMost = 100;      // the rivals raise what they pay savers (and lower what they charge) by up to a point...
        public const int AnswerPer = 500;       // ...this many hundredths of a point for each whole share of the city the bank has taken (500: half a point at 10%)

        // ---------------------------------------------------------------- the borrowers

        public const int Segments = 5;
        public const int Poor = 0, Fair = 1, Good = 2, Shops = 3, Companies = 4;
        public static readonly string[] SegmentKeys =
        {
            "reallife_ob_seg_poor", "reallife_ob_seg_fair", "reallife_ob_seg_good", "reallife_ob_seg_shops", "reallife_ob_seg_companies"
        };
        public static bool IsBusiness(int segment) => segment >= Shops;

        // What the city would borrow at the start's prices, from all its banks together (the poor: from the private lender).
        public static readonly long[] DemandCents = { 3000000000L, 12000000000L, 20000000000L, 15000000000L, 25000000000L };

        // The rival banks' price for each: their own rate for a typical rating of the kind (LendingTerms, as the city's banks
        // price the player), a business loan a point under it (ProductTerms.BusinessRate). The poor: no bank of the city lends
        // under the threshold, so the yardstick is the fair price and PoorPremium more, a long way under the private lender's.
        public static readonly int[] TypicalScore = { 0, 620, 740, 660, 780 };
        public const int PoorPremium = 1200;
        public const int HouseRateDefault = 12;  // a bank's own rate in a city that has none to read, whole per cent (the game's largest bank ships at 12%)
        public const int LoanOffsetLeast = -300, LoanOffsetMost = 900, LoanOffsetStep = 25;

        public static readonly int[] OwnLoanHold = { 120, 70, 70, 80, 60 };   // the private lender's customers come readily to a bank
        public static readonly int[] LoanPull = { 250, 400, 450, 350, 500 };   // a point dearer: e to minus this much thousandths of the weight
        public const int LendFollow = 250;       // a month's way to the book wanted, thousandths
        public static readonly int[] DemandCycle = { 1100, 1000, 900, 800 };    // what the city borrows in a boom, steady, a slowdown and a recession
        public static readonly int[] TermYears = { 1, 2, 3, 3, 4 };            // repaid evenly over this many of the game's years

        // Defaults a year in a steady city at the start's prices with the council's policies as they came, thousandths of
        // the loans, and what moves them.
        public static readonly int[] DefaultBase = { 160, 50, 15, 45, 15 };
        public static readonly int[] PeopleCycle = { 850, 1000, 1200, 1500 };   // Boom, Steady, Slowdown, Recession (MacroCatalogue's CycleState)
        public const int InflationPerPoint = 60;   // a point of inflation over the target, thousandths more of people's defaults...
        public const int DeflationPerPoint = 30;   // ...and under it (incomes fall behind the debts)
        public const int AdversePerPoint = 150;    // each point dearer than the rivals brings borrowers this much likelier to default
        public const int MonthNoiseLeast = 700, MonthNoiseMost = 1300;   // each month's luck, segment by segment, from the bank's seed
        public const int FactorLeast = 400, FactorMost = 3000;

        // The council's policies on defaults (Civic: Lever.BusinessTax, Grants, Wages, Rent; +1 and -1), thousandths.
        public static readonly int[] TaxOnFirms = { 120, -80 };
        public static readonly int[] GrantsOnFirms = { -100, 60 };      // grants, or a licence fee in their place
        public static readonly int[] WagesOnFirms = { 60, -60 };        // a wage floor, or a wage subsidy
        public static readonly int[] WagesOnPeople = { -80, 30 };
        public static readonly int[] RentOnFirms = { 40, -30 };         // landlords set their rents, or rent control
        public static readonly int[] RentOnPeople = { 80, -60 };

        // Arrears: a loan that defaults pays nothing; over a month a part of it is put right and the rest is written off,
        // and of what is written off a part comes back over the months after (a firm's loan has security, a person's little).
        public static readonly int[] CureShare = { 200, 250, 300, 250, 300 };
        public static readonly int[] RecoverShare = { 100, 150, 200, 350, 450 };
        public const int RecoverMonths = 2;
        public const int CourtExtra = 150;       // with the courts (Justice on), each claim brings this much more back...
        public const int CourtMonths = 2;        // ...over this many more months...
        public const int CourtCost = 50;         // ...for this share of what is claimed, paid when it is written off

        // ---------------------------------------------------------------- what the bank costs to run

        public const long StaffDayCents = 150000L;   // the banking hall and its back office, a day: $1,500
        public const int StaffOnSavings = 3;         // thousandths of the deposits a year
        public const int StaffOnLoans = 8;           // thousandths of the loans a year
        public const int ReserveSpread = 50;         // reserves at the central bank earn the bank rate less this

        // ---------------------------------------------------------------- trust

        public const int TrustMonths = 9;            // the city trusts a new bank fully this many months after it opens
        public const int StandingNeutral = 700;      // what the city's trust is with the player's integrity at 50 (Standing)...
        public const int StandingPerPoint = 6;       // ...and each point of integrity either way
        public const int CushionWatch = 850, CushionBreach = 500;
        public static readonly int[] CycleTrust = { 1000, 1000, 950, 900 };
        public const int ScareMost = 1000;
        public const int ScareFade = 400;            // a month takes this much of the city's scare away, thousandths
        public const int ScareBreach = 300, ScareFine = 250, ScareRun = 200;
        public const int RunStart = 450, RunEnd = 550;   // a run begins when the city's confidence falls under the first and ends over the second

        // ---------------------------------------------------------------- the fund

        public const int Strategies = 4;             // none, cautious, balanced, bold
        public static readonly string[] StrategyKeys = { "reallife_ob_fund_none", "reallife_ob_fund_cautious", "reallife_ob_fund_balanced", "reallife_ob_fund_bold" };
        public const int FundLimitMost = 250, FundLimitStep = 50;    // the most of the deposits it may hold, thousandths
        public static readonly int[] FundCompanies = { 0, 12, 10, 8 };   // how many companies it holds, each an even part
        public static readonly int[] FundDistress = { 0, 100, 200, 300 }; // the most distress a company may show, thousandths
        public const int FundCommissionBp = 10;      // a trade's commission, hundredths of a per cent
        public const int FundSpreadBp = 20;          // bought over the last price and sold under it by this much
        public const int FundFireSale = 30;          // thousandths under the last price, when the bank must sell to pay its savers
        public const int FundDrift = 200;            // a holding this far off its part is traded back to it, thousandths

        // ---------------------------------------------------------------- selling it

        public const int HistoryMonths = 12;
        public const int SaleYears = 3;              // a buyer pays its capital and three years of the last year's profit

        // ---------------------------------------------------------------- the money, as the game's transactions name it

        public const string TakingsKey = "reallife_transaction_ownbank_takings";       // interest earned, dividends, recoveries, gains: takings at the headquarters
        public const string SaversKey = "reallife_transaction_ownbank_savers";         // what the savers are paid: a row among its goods
        public const string LossesKey = "reallife_transaction_ownbank_losses";         // loans written off and court costs, fund losses: a row among its goods
        public const string WagesKey = "reallife_transaction_ownbank_wages";           // its staff: salaries
        public const string FineKey = "reallife_transaction_ownbank_fine";             // the regulator's fine: not deducted
        public const string LicenceKey = "reallife_transaction_ownbank_licence";       // the licence: capital, not deducted
        public const string CapitalInKey = "reallife_transaction_ownbank_investment_capital";   // capital put in
        public const string CapitalOutKey = "reallife_transaction_ownbank_investment_dividend"; // capital taken out as a dividend
        public const string KeptKey = "reallife_transaction_ownbank_investment_kept";   // the day's profit kept in the bank's capital
        public const string CoveredKey = "reallife_transaction_ownbank_investment_covered";     // the day's loss met from it
        public const string SaleKey = "reallife_transaction_ownbank_investment_sale";   // the bank sold, or what its winding up left
        public const string SaversRow = "reallife_books_ownbank_savers";               // the headquarters' statement: rows among its goods
        public const string LossesRow = "reallife_books_ownbank_losses";

        /// <summary>True for a transaction of the bank's that is takings at its headquarters (the statement counts it so).</summary>
        public static bool IsTakings(string type) => type == TakingsKey;

        /// <summary>The row among its headquarters' goods a cost of the bank's is entered in; null for none.</summary>
        public static string CostRow(string type) => type == SaversKey ? SaversRow : type == LossesKey ? LossesRow : null;
    }
}
