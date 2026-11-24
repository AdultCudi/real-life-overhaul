using System;

namespace RealLifeOverhaul.Ui
{
    // What the Your bank screen reads from the bank the player owns: its books in cents, its choices and the regulator's view. Game-free:
    // OwnBankBuild writes the page from it, the live screen (OwnBankReader over the real OwnBank) and the preview's samples both fill it.
    // A name that is a text of the game is its key (a saver's, a segment's, a level's): the page asks the words for it.

    /// <summary>A kind of saver of the bank: people, or firms.</summary>
    public sealed class OwnSaverFact
    {
        public string Name = string.Empty;
        public long Deposits;
        public long Pool;               // the city's savings of this kind
        public int OwnBp, RivalBp;      // the bank's rate and the rival banks', in hundredths of a per cent
        public int Offset;              // the bank's rate over the rivals', hundredths of a point
        public long Wanted;             // what savers of this kind would put in at that rate
    }

    /// <summary>A segment of the bank's borrowers.</summary>
    public sealed class OwnSegmentFact
    {
        public string Name = string.Empty;
        public long Lent, Arrears, Recovering;
        public int OwnBp, RivalBp, Offset;
        public int DefaultPerMille, DefaultUsual;
        public bool Lends;
        public long Wanted;
        public long SaleMost;           // the most of this kind's loans one sale to other banks may take (the bank risk)
    }

    /// <summary>One kind of borrower's part of the book, against the share one kind may be (the bank risk).</summary>
    public sealed class OwnConcFact
    {
        public string Name = string.Empty;
        public int Share;               // thousandths of the book
        public int Excess;              // thousandths over the limit; 0 under it
    }

    /// <summary>One kind of client's cards: the owner's rules for it, what it holds and how the lines treat its applicants.</summary>
    public sealed class OwnCardKindFact
    {
        public string Tier = string.Empty;
        public string Name = string.Empty;
        public bool Issue;
        public int LimitStep;
        public long LimitCents;
        public int Holders, Queue, Wanted;
        public long Balance, Arrears;
        public int DefaultPerMille;     // a card's defaults a year now
        public int AutoShare, QueueShare;   // thousandths of applicants approved at once and queued; the rest are declined
    }

    /// <summary>A company the fund holds.</summary>
    public sealed class OwnHoldingFact
    {
        public string Name = string.Empty;
        public long Qty, Cost, Value, Year;
    }

    /// <summary>A branch of the bank with this month's figures and last month's.</summary>
    public sealed class OwnBranchFact
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public long Visitors, Served, Lost, Opened, Loans, Fees, CostDay;
        public long PaidIn, TakenOut, Lent, Refused;
        public long LastVisitors, LastServed, LastLost, LastOpened, LastLoans, LastLent, LastRefused, LastFees, LastPaidIn, LastTakenOut;
        public int Week;                // seen to this week
        public bool Open;
        public int Tellers, StaffAll, ServePerTeller;
    }

    /// <summary>What the Your bank screen shows for the bank, whatever its state.</summary>
    public sealed class OwnBankFacts
    {
        public bool Ready;              // the feature runs and a save is loaded
        public int SaverLeast, SaverMost, SaverStep, LoanLeast, LoanMost, LoanStep;   // the rate offsets the bank may set (hundredths of a point), from OwnBankCatalogue
        public int LoanFloorBp;         // no loan rate goes under this (CreditCatalogue.RateFloor), hundredths of a per cent
        public int FundLimitMost, FundLimitStep, FundWeight;   // thousandths: the fund's limit, its step, and the weight the regulator counts a share at
        public long CapitalStep;        // cents: capital goes in and comes out by this step
        public int State;               // 0 none yet, 1 running, 2 sold, 3 closed
        public int Day;
        public string Name = string.Empty;
        public bool Test;

        // Not running: founding it, or founding it again
        public long Licence, FoundCapital;
        public bool HasHq;
        public string HqName = string.Empty;
        public int LawRatio;            // thousandths
        public int RivalSaverBp, RivalLoanBp;
        public string CommunityHolds = string.Empty;   // what a community bank may hold, as a sentence's figures: branches, loans, deposits
        public long CommBranches, CommLoans, CommDeposits;
        public int EndDay;
        public long EndCents;
        public string Why;              // why the regulator closed it, as words
        public string AgainWait;        // why it cannot be founded again yet (a sentence's key and figures), or null
        public int AgainDay, AgainIntegrity, Integrity;
        public bool StandingOn;
        public long NextCapital;        // the capital a new licence asks after closures
        public int Closures;

        // Running
        public string Level = string.Empty;
        public int Tier;
        public bool Top;
        public int Stage;               // 0 clear, 1 watch, 2 breach
        public string StageWords = string.Empty;
        public int StageDay;
        public bool Strikes;
        public bool Run;
        public long Reserves, Lent, Arrears, Fund, FundCost, Deposits, Capital, Risk, ReserveNeed, LostAll, RecoveredAll;
        public long CitySavings;        // the city's savings in all, for the bank's share
        public int Ratio, WatchRatio;
        public int Trust, Confidence;
        public long MonthIncome, MonthCosts, MonthLost, LastIncome, LastCosts;
        public bool HasLast;
        public long YearProfit, SaleValue, MostOut, PerDayNow, PerDayLast;
        public bool Sellable, HasDaily, HasHistory;
        public string Crews;            // the end game's crews line, or null
        public int DaysRun;

        // Level
        public long DepositsMost = -1, LoansMost = -1;   // -1: no limit at this level
        public int BranchesNow, BranchesMost;
        public bool Over;
        public string NextLevel;        // the next level's name; null at the top
        public int RecordMonths, HeldMonths;
        public long CapitalLeast;
        public bool RegulatorOk;
        public long MoveFee;
        public string MoveWhy;          // why it cannot move up now, or null
        public long AfterLoans, AfterDeposits;
        public int AfterBranches;
        public bool NextTop;

        // Pay and dividends
        public int PayMode;             // 0 a share, 1 a fixed sum
        public int PayShare;            // thousandths
        public long PayFixed, PayWanted, PayGiven, PaidMonth, PaidLast, PaidAll;
        public string PayHeld;          // the sentence of what held the last day's pay back, or null
        public int DivEvery;            // 0 off, 1 weekly, 2 monthly, 3 yearly
        public string[] DivEveryNames = new string[0];
        public int DivShare;
        public int DivNextDay;
        public long DivKept, DivPaid, DivWanted, DivAll;
        public int DivDay;
        public bool DivShort;
        public string DivWhy;

        public long DivExpected, DivExpectedTax;   // what the next automatic dividend is expected to come to, and what would be withheld from it
        public int DivTaxBp;            // the share withheld, hundredths of a per cent (0 while the market is not running)
        public bool DivFromOwn;

        // Risk (Economy/OwnBank/Risk)
        public int RiskLevel;           // BankRisk: 0 off, 1 standard, 2 hard
        public string[] RiskNames = new string[0];
        public bool RiskOn;
        public int WarnRatio;           // the ratio under which the bank lends no further, its warning line
        public bool Warned, WatchHeld;
        public int Signs;               // BankSign flags
        public string SignWords = string.Empty;
        public int CoverTenths;         // tenths of a day of a run the cash would last
        public long RunNeed;            // what a day of a run takes
        public int ShockNow = -1, ShockAhead = -1;   // the kind of borrower in trouble this month and the one coming next month
        public string ShockNowName = string.Empty, ShockAheadName = string.Empty;
        public int ConcLimit;           // thousandths of the book one kind may be
        public OwnConcFact[] Conc = new OwnConcFact[0];
        public long Borrowed, BorrowRoom;
        public int BorrowBp, BorrowedBp, BorrowDue;   // today's price, the rate it bears and the day it falls due
        public long BorrowFee;          // the arrangement fee of the most that may be borrowed (a share of it for less)
        public int SaleDiscount, SaleMostShare;       // thousandths of the face of loans sold to other banks, and the share of a kind one sale may take
        public long SoldFace, SoldLoss;
        public int LendScale;           // thousandths of the new lending a watched bank may do
        public long ToClear;            // the capital that would clear the warning line
        public int RunHold;             // what paying people well holds back of a run's outflow, thousandths
        public long WfAssets, WfSavers, WfSaversLoss, WfBanks, WfBanksLoss, WfFee, WfOwner;   // how the assets would be shared out if the regulator took the bank now
        public int ReserveShare;        // reserves kept against the deposits, thousandths
        public int SaverFlight;         // the speed savers leave at against the speed they come, thousandths

        // Cards (Economy/OwnBank/Cards)
        public bool CardsOn;
        public int CardAuto, CardDecline, CardRate, CardFee;
        public int CardAutoLeast = 520, CardAutoMost = 820, CardDeclineLeast = 420, CardLineStep = 20,
            CardRateLeast = -500, CardRateMost = 800, CardRateStep = 50;   // the catalogue's, so a fact filled by hand has steps
        public long[] CardFees = new long[0];        // the yearly fees to choose from, cents at today's prices
        public long[][] CardLimits = new long[0][];  // the limits to choose from, by kind, cents at today's prices
        public int CardRateNow, CardRivalBp;
        public OwnCardKindFact[] CardKinds = new OwnCardKindFact[0];
        public long CardBook, CardArrearsAll, CardLostAll;
        public int CardsHeld, CardsQueued;
        public long[] CardFig = new long[0], CardLast = new long[0];
        public bool OwnCard;
        public long OwnOwed, OwnLimit, OwnLostAll, OwnMost, OwnLeast, OwnStep;
        public int OwnRateBp;
        public string OwnName = string.Empty;

        // The Black card tier (Economy/OwnBank/Cards/BlackTerms)
        public bool BlackOn;
        public int BlackWealth, BlackScore, BlackFee, BlackReward;      // the owner's lines: indexes of the steps below, and the least credit score
        public int BlackScoreLeast = 700, BlackScoreMost = 840, BlackScoreStep = 20;
        public long[] BlackWealths = new long[0];      // the least wealth to choose from, cents at the start's prices
        public long[] BlackFees = new long[0];         // the yearly fees to choose from, cents at today's prices
        public int[] BlackRewards = new int[0];        // the rewards to choose from, hundredths of a per cent of the spend
        public int BlackHolders, BlackWanted, BlackRich, BlackScoreShare, BlackDefaultPerMille, BlackRateBp;
        public long BlackBook, BlackArrears;
        public long[] BlackFig = new long[0], BlackLast = new long[0];

        // Desk (Economy/OwnBank/Desk)
        public bool DeskOn;
        public int DeskMandate, DeskShare, DeskLimit;
        public string[] MandateNames = new string[0];
        public int DeskShareMost = 250, DeskShareStep = 50, DeskLimitLeast = 50, DeskLimitMost = 250, DeskLimitStep = 25, DeskCompanies, DeskCutKeep = 500;
        public long BookValue, BookCost, BookRoom, DeskPeak, Custody;
        public int DeskDrawdown, DeskCuts, DeskCutDay, Activity;
        public bool DeskBuying, Derivs;
        public long DeskBrokerage, DeskDerivs, DeskLastBrokerage, DeskLastDerivs, DeskAll;
        public OwnHoldingFact[] Book = new OwnHoldingFact[0];

        // Books by tab
        public OwnSaverFact[] Savers = new OwnSaverFact[0];
        public OwnSegmentFact[] Segments = new OwnSegmentFact[0];
        public bool Courts;
        public int Strategy, FundLimit;
        public long FundRoom, FundDividends;
        public string[] StrategyNames = new string[0];
        public int StrategyCompanies;
        public OwnHoldingFact[] Holdings = new OwnHoldingFact[0];
        public float[] FundSeries = new float[0];
        public int[] FundDays = new int[0];
        public OwnBranchFact[] Branches = new OwnBranchFact[0];
        public bool BranchOn = true;
        public int Reach, ReachFirms;
        public long BranchDeposits, BranchLoans, BranchMargin, BranchFees, BranchCosts;
        public bool BranchBrings;
        public int WeekServed;
        public int Hue;
    }
}
