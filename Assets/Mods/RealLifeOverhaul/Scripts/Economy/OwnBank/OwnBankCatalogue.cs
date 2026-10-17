namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Every figure of the player's own bank. Money in cents at the start's prices unless said; rates in hundredths of a per
    /// cent a year; shares and factors in thousandths.
    /// </summary>
    public static class OwnBankCatalogue
    {
        public const long LicenceCents = 300000000L;       // the city's banking licence, once: $3,000,000
        public const long CapitalLeastCents = 500000000L;  // the least capital paid in before the first saver: $5,000,000
        public const long CapitalStepCents = 10000000L;    // capital is put in and taken out in steps of $100,000

        public const int AgainCapital = 500;      // thousandths more capital for a new licence for each bank closed before
        public const int AgainIntegrity = 55;     // with Standing on, the integrity a new licence needs: a little over a newcomer's 50

        // The regulator. Ratios are capital against risk-weighted assets, thousandths.
        public const int MinRatio = 100;        // the law's 10%
        public const int WatchRatio = 125;      // watched under this, and the city hears of it
        public const int LendRatio = 130;       // the bank lends and buys no further than this, clear of the watch line
        public const int ReserveShare = 80;     // reserves kept against the deposits, thousandths
        public const int CureMonths = 1;        // a breach is to be put right within this many game months...
        public const int StrikesClose = 2;      // ...each deadline missed is fined; this many close the bank
        public const int FineShare = 100;       // a fine: thousandths of the capital the bank is short of...
        public const long FineLeastCents = 5000000L;   // ...and never under $50,000

        // Risk weights, thousandths; reserves at the central bank weigh nothing.
        public static readonly int[] LoanWeight = { 1500, 1000, 750, 1000, 1000 };   // by segment
        public const int ArrearsWeight = 1500;
        public const int FundWeight = 2500;

        // A closed bank is wound up: what its assets fetch, and the regulator's charge on what is left, thousandths.
        public const int WindPerforming = 850, WindArrears = 250, WindFund = 970, WindFee = 100;

        public const int Savers = 2;
        public const int People = 0, Firms = 1;
        public static readonly string[] SaverKeys = { "reallife_ob_saver_people", "reallife_ob_saver_firms" };

        public static readonly long[] SavingsCents = { 40000000000L, 25000000000L };   // the city's savings: $400,000,000 and $250,000,000

        public const int CityRateStart = 300;         // the bank rate before the central bank moves it, as the paper and the Stocks app print it
        public const int RivalSaverMargin = 150;      // the rival banks pay savers the bank rate less this...
        public const int RivalSaverLeast = 25;        // ...and never under a quarter of a point
        public const int SaverOffsetLeast = -200, SaverOffsetMost = 400, SaverOffsetStep = 25;   // the player's rate against theirs

        // Where the city saves: each rival holds HoldRivals, the bank its OwnSaverHold grown with the city's trust, times e
        // to the SaverPull for each point it pays over the rivals. A day moves a balance SaverFollow of a month's way there.
        public const int HoldRivals = 1000;
        public static readonly int[] OwnSaverHold = { 70, 90 };          // people trust a name more slowly; firms chase the rate
        public static readonly int[] SaverPull = { 300, 450 };
        public static readonly int[] SaverFollow = { 150, 250 };
        public static readonly int[] RunShare = { 30, 60 };              // a day's withdrawals in a run, thousandths of the balance
        public const int AnswerMost = 100;      // the rivals pay savers more, and charge borrowers less, by up to a point...
        public const int AnswerPer = 500;       // ...this many hundredths of a point for each whole share of the city the bank has

        public const int Segments = 5;
        public const int Poor = 0, Fair = 1, Good = 2, Shops = 3, Companies = 4;
        public static readonly string[] SegmentKeys =
        {
            "reallife_ob_seg_poor", "reallife_ob_seg_fair", "reallife_ob_seg_good", "reallife_ob_seg_shops", "reallife_ob_seg_companies"
        };
        public static bool IsBusiness(int segment) => segment >= Shops;

        public static readonly long[] DemandCents = { 3000000000L, 12000000000L, 20000000000L, 15000000000L, 25000000000L };   // what the city borrows from all its lenders

        public static readonly int[] TypicalScore = { 0, 620, 740, 660, 780 };   // the rivals price a segment at their rate for this rating
        public const int PoorPremium = 1200;     // no bank lends to the poorly rated: their yardstick is the fair price and this
        public const int HouseRateDefault = 12;  // whole per cent: the game's largest bank ships at 12%
        public const int LoanOffsetLeast = -300, LoanOffsetMost = 900, LoanOffsetStep = 25;

        public static readonly int[] OwnLoanHold = { 120, 70, 70, 80, 60 };   // the private lender's customers come readily to a bank
        public static readonly int[] LoanPull = { 250, 400, 450, 350, 500 };   // a point dearer: e to minus this many thousandths of the weight
        public const int LendFollow = 250;       // a month's way to the book wanted, thousandths
        public static readonly int[] DemandCycle = { 1100, 1000, 900, 800 };    // what the city borrows in a boom, steady, a slowdown and a recession
        public static readonly int[] TermYears = { 1, 2, 3, 3, 4 };            // repaid evenly over this many game years

        // Defaults a year in a steady city, thousandths of the loans, and what moves them.
        public static readonly int[] DefaultBase = { 160, 50, 15, 45, 15 };
        public static readonly int[] PeopleCycle = { 850, 1000, 1200, 1500 };   // by CycleState
        public const int InflationPerPoint = 60;   // more of people's defaults for each point of inflation over the target...
        public const int DeflationPerPoint = 30;   // ...and for each point under it
        public const int AdversePerPoint = 150;    // each point dearer than the rivals: the borrowers a dear lender gets are riskier
        public const int MonthNoiseLeast = 700, MonthNoiseMost = 1300;   // each month's luck by segment, from the bank's seed
        public const int FactorLeast = 400, FactorMost = 3000;

        // The council's levers on defaults (Civic), at +1 and at -1, thousandths.
        public static readonly int[] TaxOnFirms = { 120, -80 };
        public static readonly int[] GrantsOnFirms = { -100, 60 };      // grants, or a licence fee in their place
        public static readonly int[] WagesOnFirms = { 60, -60 };        // a wage floor, or a wage subsidy
        public static readonly int[] WagesOnPeople = { -80, 30 };
        public static readonly int[] RentOnFirms = { 40, -30 };         // landlords set their rents, or rent control
        public static readonly int[] RentOnPeople = { 80, -60 };

        // A month's part of the arrears is put right and the rest written off; of that, a part comes back (a firm's loan has security).
        public static readonly int[] CureShare = { 200, 250, 300, 250, 300 };
        public static readonly int[] RecoverShare = { 100, 150, 200, 350, 450 };
        public const int RecoverMonths = 2;
        public const int CourtExtra = 150;       // with the courts on, each claim brings this much more back...
        public const int CourtMonths = 2;        // ...over this many more months...
        public const int CourtCost = 50;         // ...for this share of what is claimed, paid when it is written off

        public const long StaffDayCents = 150000L;   // the banking hall and its back office, a day: $1,500
        public const int StaffOnSavings = 3;         // thousandths of the deposits a year
        public const int StaffOnLoans = 8;           // thousandths of the loans a year
        public const int ReserveSpread = 50;         // reserves at the central bank earn the bank rate less this

        public const int TrustMonths = 9;            // the city trusts a new bank fully this many months after it opens
        public const int StandingNeutral = 700;      // the city's trust at an integrity of 50...
        public const int StandingPerPoint = 6;       // ...and each point either way
        public const int CushionWatch = 850, CushionBreach = 500;
        public static readonly int[] CycleTrust = { 1000, 1000, 950, 900 };
        public const int ScareMost = 1000;
        public const int ScareFade = 400;            // the scare a month takes away, thousandths
        public const int ScareBreach = 300, ScareFine = 250, ScareRun = 200;
        public const int RunStart = 450, RunEnd = 550;   // a run begins when confidence falls under the first and ends over the second

        public const int Strategies = 4;             // none, cautious, balanced, bold
        public static readonly string[] StrategyKeys = { "reallife_ob_fund_none", "reallife_ob_fund_cautious", "reallife_ob_fund_balanced", "reallife_ob_fund_bold" };
        public const int FundLimitMost = 250, FundLimitStep = 50;    // the most of the deposits it may hold, thousandths
        public static readonly int[] FundCompanies = { 0, 12, 10, 8 };   // companies held, an even part each
        public static readonly int[] FundDistress = { 0, 100, 200, 300 }; // the most distress a company held may show, thousandths
        public const int FundCommissionBp = 10;      // a trade's commission
        public const int FundSpreadBp = 20;          // bought over the last price and sold under it by this much
        public const int FundFireSale = 30;          // thousandths under the last price when it must sell to pay its savers
        public const int FundDrift = 200;            // thousandths off its part before a holding is traded back

        public const int HistoryMonths = 12;
        public const int SaleYears = 3;              // a buyer pays its capital and this many years of the last year's profit

        public const string TakingsKey = "reallife_transaction_ownbank_takings";       // interest, dividends, recoveries and gains: takings at the headquarters
        public const string SaversKey = "reallife_transaction_ownbank_savers";         // a row among its goods
        public const string LossesKey = "reallife_transaction_ownbank_losses";         // loans written off, court costs, fund losses: a row among its goods
        public const string WagesKey = "reallife_transaction_ownbank_wages";           // salaries
        public const string FineKey = "reallife_transaction_ownbank_fine";             // not deducted
        public const string LicenceKey = "reallife_transaction_ownbank_licence";       // capital, not deducted
        public const string CapitalInKey = "reallife_transaction_ownbank_investment_capital";
        public const string CapitalOutKey = "reallife_transaction_ownbank_investment_dividend";
        public const string KeptKey = "reallife_transaction_ownbank_investment_kept";   // the day's profit kept in the bank's capital
        public const string CoveredKey = "reallife_transaction_ownbank_investment_covered";     // the day's loss met from it
        public const string SaleKey = "reallife_transaction_ownbank_investment_sale";   // the bank sold, or what its winding up left
        public const string SaversRow = "reallife_books_ownbank_savers";
        public const string LossesRow = "reallife_books_ownbank_losses";
        public const string SystemsKey = "reallife_transaction_ownbank_systems";       // at the end game's scale: its systems, compliance and deposit insurance

        // At the end game's scale (Core/EndScale), mixed in by its share: crews by kind in place of a share of what it holds.
        public const long LicenceCentsScale = 6000000000L;        // $60,000,000, with the core banking systems and the branches
        public const long CapitalLeastCentsScale = 15000000000L;  // $150,000,000
        public const int PoolScale = 5000;                        // the city's savings and borrowing at its real size, thousandths
        public const int Tellers = 0, Managers = 1, Officers = 2, Compliance = 3, CrewKinds = 4;
        public static readonly int[] CrewWage = { 16000, 42000, 36000, 45000 };   // cents a day each
        public const long TellerCents = 1200000000L;              // people's savings one teller looks after ($12,000,000: 2,000 savers)
        public const long ManagerCents = 720000000L;              // businesses' savings one relationship manager looks after ($7,200,000: 120 businesses)
        public const long OfficerCents = 800000000L;              // loans one loan officer looks after ($8,000,000)
        public const int ComplianceLeast = 12;                    // compliance and risk officers at the least...
        public const long ComplianceCents = 15000000000L;         // ...and one more for each $150,000,000 it holds
        public const long SystemsDayCents = 4000000L;             // core banking, cards, payments and data, a day ($40,000)
        public const int InsuranceBp = 8;                         // deposit insurance a year, of the deposits
        public const int CrewPace = 40;                           // thousandths of the need hired a day while short
        public const int CostYearDays = 60;                       // the crews' wages and the systems are for a day of a 60-day year: a longer year pays them alike by the year
        public const int IncomeAtScale = 1000;                    // thousandths of what its loans earn at scale

        /// <summary>True for a transaction of the bank's that is takings at its headquarters.</summary>
        public static bool IsTakings(string type) => type == TakingsKey;

        /// <summary>The row among its headquarters' goods a cost of the bank's is entered in; null for none.</summary>
        public static string CostRow(string type) => type == SaversKey ? SaversRow : type == LossesKey ? LossesRow : null;
    }
}
