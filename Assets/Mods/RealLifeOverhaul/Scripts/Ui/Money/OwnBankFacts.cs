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
