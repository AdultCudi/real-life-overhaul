using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Where the player's bank stands. Saved as a number: never renumber.</summary>
    public enum OwnBankState
    {
        None = 0,
        Running = 1,
        Sold = 2,
        Closed = 3      // closed by the regulator and wound up
    }

    /// <summary>What the regulator makes of the bank. Saved as a number.</summary>
    public enum Regulator
    {
        Clear = 0,
        Watch = 1,      // under the watch ratio: the city hears of it
        Breach = 2      // under the law's ratio: a deadline to put it right
    }

    /// <summary>Why the regulator closed a bank. Saved as a number.</summary>
    public enum ClosedWhy
    {
        None = 0,
        Strikes = 1,    // a breach left standing past its second deadline
        Insolvent = 2,  // no capital left
        Collapse = 3,   // a ratio under half the law's
        Run = 4         // savers it could not pay
    }

    /// <summary>One company the bank's fund holds.</summary>
    public sealed class FundHolding
    {
        public int StockId;
        public string Ticker = string.Empty, Name = string.Empty;
        public long Qty;
        public long CostCents;       // what the shares held cost in all, commission apart
        public int LastCents;        // the last price seen
        public int DpsCents;         // the last quarterly dividend, a share
    }

    /// <summary>The player's bank: its balance sheet in cents, its choices, the regulator's view, its fund and its months.</summary>
    public sealed class OwnBank
    {
        public OwnBankState State;
        public string Name = string.Empty;
        public int Seed;
        public int FoundedDay, EndDay;          // EndDay: the day it was sold or closed
        public long EndCents;                   // what the player had for it then
        public ClosedWhy Why;
        public int LastDay;                     // the last day run
        public bool Test;                       // founded by a test tool: it pays nothing out
        public int Closures;                    // the player's banks the regulator has closed: each raises the next one's capital
        public int WatchTold = int.MinValue;    // the day the regulator's watch was last told, once a month at most; int.MinValue: never
        public bool Still;                      // written while the feature stood down: the days until it is on again are not run

        public long Capital, Reserves;
        public readonly long[] Deposits = new long[OwnBankCatalogue.Savers];
        public readonly long[] Loans = new long[OwnBankCatalogue.Segments];      // performing
        public readonly long[] Arrears = new long[OwnBankCatalogue.Segments];    // paying nothing
        public readonly long[] Recovering = new long[OwnBankCatalogue.Segments]; // written off, still to come back (off the books)
        public readonly int[] Recover = new int[OwnBankCatalogue.Segments];      // days: each day brings back this part of what is recovering

        public readonly int[] SaverOffset = new int[OwnBankCatalogue.Savers];   // hundredths of a point over the rivals' rate
        public readonly int[] LoanOffset = new int[OwnBankCatalogue.Segments];  // hundredths of a point over the rivals' rate
        public readonly bool[] Lending = new bool[OwnBankCatalogue.Segments];
        public int Strategy;                    // an index of OwnBankCatalogue.StrategyKeys
        public int FundLimit;                   // thousandths of the deposits
        public bool Rebalance;                  // a choice changed: the fund is traded to it on the next day

        public int Scare;                       // thousandths, fading
        public bool Run;
        public int RunFrom;
        public Regulator Stage;
        public int Deadline, Strikes;
        public long FinedCents;                 // in all

        public readonly List<FundHolding> Holdings = new List<FundHolding>(12);

        public int MonthFrom;                   // the first day of the month being added up
        public int Month;                       // months since it was founded: each month's luck is drawn from it
        public long MonthIncome, MonthCosts, LastIncome, LastCosts;
        public long MonthLost, LastLost, LostAll, RecoveredAll;   // written off, and come back, in all
        public readonly long[] History = new long[OwnBankCatalogue.HistoryMonths];   // each month's profit, the oldest first
        public int HistoryCount;

        public int ScaleFrom = EndScaleRules.Unset;   // the day the end game's scale began to come in
        public readonly int[] Crew = new int[OwnBankCatalogue.CrewKinds];
        public long FoundedLeast;               // the least capital when it was founded, cents at the start's prices (0: the least before the scale)
        public long Public;                     // of the capital, what an offer of its shares on the stock market raised: kept while the stock trades, never paid out (OwnBankRules.Keep)

        public long CounterFees;                // what the branches' counters charged since the last day was run: into the books with the next day
        public int CounterHour = int.MinValue;  // the last hour the branches were worked (day x 24 + hour); int.MinValue: never
        public readonly List<BankBranch> Branches = new List<BankBranch>(4);   // each branch's figures (Economy/OwnBank/Branch)

        // Its level and the owner's pay (Economy/OwnBank/Tiers).
        public int Tier;                        // BankTiers: a community bank when founded; BankTiers.Unknown for a line written before levels, until fitted
        public int TierFrom;                    // the day its record at its level counts from: founded, moved up, or last in breach
        public int TierTold = BankTiers.Unknown;   // the level the player was last told the bank is ready to move up to
        public int PayMode;                     // BankTiers.PayShareMode or PayFixedMode
        public int PayShare;                    // thousandths of each day's profit paid to the owner
        public long PayFixed;                   // cents a day paid to the owner whatever the day made, while the bank can spare it
        public long PaidMonth, PaidLast, PaidAll;   // what the owner was paid this month, last month and in all
        public long PayWanted, PayGiven;        // the last day run: the pay asked for and what was paid
        public int PayHeld;                     // why less was paid that day: a BankTiers PayHeld value

        // Its automatic dividends (Economy/OwnBank/Dividends).
        public int DivEvery;                    // BankDividends: Off, Weekly, Monthly or Yearly
        public int DivShare;                    // thousandths of the profit kept over the time paid out
        public int DivFrom;                     // the first day of the time being added up
        public long DivKept;                    // the profit kept since then, after the owner's pay (a loss takes it down)
        public int DivDay;                      // the day the last one fell due; 0: none yet
        public long DivWanted, DivPaid;         // the last one: asked for and paid
        public int DivWhy;                      // why it was less: a BankDividends value
        public long DivAll;                     // paid out in all

        // Its risk (Economy/OwnBank/Risk). Zero everywhere while the option is off, so a bank that never met it reads as before.
        public int Risk;                        // BankRisk level in force: set from the city each day (BankCity.Risk), so the rules that see only the bank read it
        public long Interbank;                  // borrowed from other banks: a liability beside the deposits
        public int InterbankDue;                // the day it falls due; 0: none borrowed
        public int InterbankBp;                 // the rate it bears, hundredths of a per cent a year
        public long SoldFace, SoldLoss;         // loans sold to other banks in all, at their face, and what the sales cost
        public long SaleLossPend, FundingPend;  // what a sale of loans cost and the fee for money borrowed, taken from the capital at once: into the books with the next day
        public int SignsTold = int.MinValue;    // the day the warning signs were last told, once a month at most; int.MinValue: never
        public int ShockTold = int.MinValue;    // the month a coming shock was last told; int.MinValue: never

        // Its credit cards (Economy/OwnBank/Cards).
        public bool CardsOn;                    // issuing: off, no new card is issued and the book runs down
        public int CardAuto = CardTerms.AutoDefault, CardDecline = CardTerms.DeclineDefault;   // credit scores: approved at once from the first, declined under the second
        public int CardRate;                    // hundredths of a point over the rivals' card rate
        public int CardFee;                     // an index of CardTerms.FeeSteps
        public readonly bool[] CardIssue = new bool[CardTerms.Kinds];
        public readonly int[] CardLimit = new int[CardTerms.Kinds];            // an index of CardTerms.Limits[kind]
        public readonly int[] CardHolders = new int[CardTerms.Kinds];
        public readonly long[] CardBal = new long[CardTerms.Kinds];            // carried, paying
        public readonly long[] CardArr = new long[CardTerms.Kinds];            // in arrears
        public readonly int[] CardQueue = new int[CardTerms.Kinds];            // applicants between the two lines, waiting for the owner
        public readonly int[] CardRisk = new int[CardTerms.Kinds];             // thousandths: the book's defaults against its kind's, by the scores approved
        public readonly long[] CardFig = new long[CardTerms.Figures], CardLast = new long[CardTerms.Figures];   // this month's figures and last month's
        public long CardLostAll;                // written off and lost to fraud in all
        public bool OwnCard;                    // the owner holds the bank's top card (the credit file's own-bank card)

        // Its Black card tier (Economy/OwnBank/Cards/BlackTerms).
        public bool BlackOn;                    // offered to the rich clients who meet its lines
        public int BlackWealth = BlackTerms.WealthDefault;   // an index of BlackTerms.WealthLines: the least a client holds
        public int BlackScore = BlackTerms.ScoreDefault;     // the least credit score
        public int BlackFee = BlackTerms.FeeDefault;         // an index of BlackTerms.FeeSteps
        public int BlackReward = BlackTerms.RewardDefault;   // an index of BlackTerms.RewardBp
        public int BlackHolders;
        public long BlackBal, BlackArr;         // carried, paying; in arrears
        public int BlackRisk = 1000;            // thousandths: its defaults against the best kind's, by the scores approved
        public readonly long[] BlackFig = new long[CardTerms.Figures], BlackLast = new long[CardTerms.Figures];   // this month's figures and last month's
        public long OwnOwed;                    // what the owner owes on it, the credit file's figure in cents
        public long OwnLostAll;                 // of it, written off in all
        public long OwnLimit;                   // its limit, cents
        public int OwnRateBp;                   // the rate it was opened at, hundredths of a per cent a year
        public long OwnDrawn, OwnPaid, OwnInterest, OwnFees, OwnLost;   // what the owner's card did since the last day was run: into the books with the next day

        // Its trading desk (Economy/OwnBank/Desk).
        public bool DeskOn;                     // the desk is open: brokerage for the clients
        public bool DeskDirty;                  // a choice changed: the own book is traded to it on the next day
        public int DeskMandate;                 // BankDesk: none, index, income or growth
        public int DeskShare = BankDesk.ShareDefault;   // thousandths of the capital the own book may hold
        public int DeskLimit = BankDesk.LimitDefault;   // thousandths: the loss from its peak that cuts the book
        public long DeskPeak;                   // the highest value the book has had since it last was cut or began
        public int DeskCutDay;                  // the day the limit last cut the book; 0: never
        public int DeskCuts;                    // times it has cut
        public long DeskBrokerage, DeskDerivs, DeskCommissionAll;   // this month's commissions on shares and on options and futures, and in all
        public long DeskLastBrokerage, DeskLastDerivs;
        public readonly List<FundHolding> Book = new List<FundHolding>(10);   // the own book's holdings

        public OwnBank()
        {
            for (int j = 0; j < CardTerms.Kinds; j++)
            {
                CardIssue[j] = j != OwnBankCatalogue.Poor;   // as a bank lends to all but the poorly rated
                CardLimit[j] = CardTerms.LimitDefault;
                CardRisk[j] = 1000;
            }
        }

        public BankBranch Branch(string place)
        {
            for (int i = 0; i < Branches.Count; i++)
            {
                if (Branches[i].Place == place)
                {
                    return Branches[i];
                }
            }
            return null;
        }

        public bool Running => State == OwnBankState.Running;
        public bool Exists => State != OwnBankState.None;

        public long Savings
        {
            get
            {
                long sum = 0;
                for (int k = 0; k < Deposits.Length; k++)
                {
                    sum += Deposits[k];
                }
                return sum;
            }
        }

        public long Lent
        {
            get
            {
                long sum = 0;
                for (int j = 0; j < Loans.Length; j++)
                {
                    sum += Loans[j];
                }
                return sum;
            }
        }

        public long InArrears
        {
            get
            {
                long sum = 0;
                for (int j = 0; j < Arrears.Length; j++)
                {
                    sum += Arrears[j];
                }
                return sum;
            }
        }

        /// <summary>What the fund cost: its book value.</summary>
        public long FundCost
        {
            get
            {
                long sum = 0;
                for (int i = 0; i < Holdings.Count; i++)
                {
                    sum += Holdings[i].CostCents;
                }
                return sum;
            }
        }

        /// <summary>What the fund is worth at the last prices seen.</summary>
        public long FundValue
        {
            get
            {
                long sum = 0;
                for (int i = 0; i < Holdings.Count; i++)
                {
                    sum += Holdings[i].Qty * Holdings[i].LastCents;
                }
                return sum;
            }
        }

        public FundHolding Find(int stockId)
        {
            for (int i = 0; i < Holdings.Count; i++)
            {
                if (Holdings[i].StockId == stockId)
                {
                    return Holdings[i];
                }
            }
            return null;
        }

        /// <summary>What the own book cost, and what it is worth at the last prices seen.</summary>
        public long BookCost
        {
            get
            {
                long sum = 0;
                for (int i = 0; i < Book.Count; i++)
                {
                    sum += Book[i].CostCents;
                }
                return sum;
            }
        }

        public long BookValue
        {
            get
            {
                long sum = 0;
                for (int i = 0; i < Book.Count; i++)
                {
                    sum += Book[i].Qty * Book[i].LastCents;
                }
                return sum;
            }
        }

        /// <summary>The cards' balances that pay, the owner's included.</summary>
        public long CardBook
        {
            get
            {
                long sum = OwnOwed + BlackBal;
                for (int j = 0; j < CardBal.Length; j++)
                {
                    sum += CardBal[j];
                }
                return sum;
            }
        }

        public long CardArrears
        {
            get
            {
                long sum = BlackArr;
                for (int j = 0; j < CardArr.Length; j++)
                {
                    sum += CardArr[j];
                }
                return sum;
            }
        }

        public int CardsHeld
        {
            get
            {
                int sum = BlackHolders;
                for (int j = 0; j < CardHolders.Length; j++)
                {
                    sum += CardHolders[j];
                }
                return sum;
            }
        }

        /// <summary>What the new businesses hold as assets: the own book at cost, the cards' balances and their arrears.</summary>
        public long OtherAssets => BookCost + CardBook + CardArrears;

        public FundHolding FindBook(int stockId)
        {
            for (int i = 0; i < Book.Count; i++)
            {
                if (Book[i].StockId == stockId)
                {
                    return Book[i];
                }
            }
            return null;
        }
    }

    /// <summary>The city as the bank's day reads it (OwnBankGame.City): game-free, so a day runs offline as in the game.</summary>
    public struct BankCity
    {
        public int YearDays, MonthDays;
        public int Level;               // the price level (Core/Prices), tenths of a point
        public int CityRateBp;          // the bank rate: CityRateStart and the central bank's points
        public int BasePointsQuarter;   // the central bank's points, in quarters
        public int HouseRate;           // the largest rival bank's own rate, whole per cent
        public int Multiplier;          // the game's interest multiplier, thousandths
        public CycleState Cycle;
        public int InflationBp;         // a year
        public int Trouble;             // business pressure's chance in this economy, thousandths (CityModel.Trouble)
        public int Integrity;           // the player's integrity as an owner, 0 to 100
        public int Tax, Grants, Wages, Rent;   // the council's levers: -1, 0 or 1
        public bool Courts;             // the justice system takes a lender's claims
        public int Scale;               // the end game's share (Core/EndScale): 0 as before, 1000 at scale
        public int Reach;               // thousandths more of the city's people the bank's branches bring it (Economy/OwnBank/Branch): 0 with none
        public int[] People;            // thousandths of its first day's figure the city's people now save and borrow, by pool (the savers, then the segments); null with none
        public int Risk;                // BankRisk: how hard the bank is held to the economy (the option); Off is the bank as before
        public int Activity;            // the stock market's activity, thousandths of an ordinary day's; 0 while the market is not running (Economy/OwnBank/Desk)
        public bool Derivs;             // the exchange has options and futures for the desk's clients

        /// <summary>A quiet city at the start's prices: the bank rate as it begins, a steady cycle, the council as it came.</summary>
        public static BankCity Quiet(int yearDays)
        {
            return new BankCity
            {
                YearDays = yearDays, MonthDays = MacroCatalogue.MonthDays(yearDays), Level = PriceRules.Start, CityRateBp = OwnBankCatalogue.CityRateStart,
                HouseRate = OwnBankCatalogue.HouseRateDefault, Multiplier = 1000, Cycle = CycleState.Steady, InflationBp = MacroCatalogue.InflationTarget,
                Trouble = 1000, Integrity = 50
            };
        }
    }

    /// <summary>
    /// What a day did, in cents, as booked at the headquarters and told to the player. Its profit is kept in the bank's
    /// capital, and the owner's pay (<see cref="Pay"/>) is taken out of it.
    /// </summary>
    public sealed class BankDay
    {
        public long Interest, Reserve, Dividends, Recovered, Gains;    // in: takings
        public long Savers, Staff, Lost, Court, FundLost, Fees, Fine;  // out
        public long Systems;                    // out, at the end game's scale: its systems, compliance and deposit insurance
        public long Counter;                    // in: what the branches' counters charged their customers (OwnBankRules.Visit)
        public long Brokerage, Derivs;          // in: the desk's commission on clients' trades in shares, and in options and futures (Economy/OwnBank/Desk)
        public long CardInterest, CardInterchange, CardFees;   // in: what the cards earn (Economy/OwnBank/Cards)
        public long CardLost, CardFraud, CardCosts;            // out: card balances written off, spend lost to fraud, rewards and processing
        public long Funding, SaleLoss, DeskCosts;              // out: interest on money borrowed from other banks, what loans sold to other banks cost, the desk's running costs
        public bool Rolled, RiskTold, DeskCut;                 // a loan from other banks was rolled over; new warning signs; the desk's risk limit cut the book
        public long OwnerCardPaid;              // the owner's card: cash it paid the bank today, for the page
        public int ShockNew = -1;               // the kind of borrower a coming shock is first told for today; -1 none
        public Waterfall Waterfall;             // how the assets were shared out when the bank was closed today
        public long Pay;                        // the owner's pay out of the capital (OwnBankRules.OwnerPay): neither takings nor a cost
        public long PaidOut;                    // an automatic dividend out of the capital (OwnBankRules.Dividends): neither takings nor a cost
        public bool DividendDue;                // an automatic dividend fell due today, paid or not
        public long Withdrawn, Deposited, Lent, Repaid, Defaulted, Cured, WrittenOff;
        public long Bought, Sold, FireSold;
        public bool RunStarted, RunEnded, Breached, Cured2, Fined, Watched, Closed;
        public ClosedWhy Why;
        public long WoundUp;                    // what winding it up left for the player

        public long Income => Interest + Reserve + Dividends + Recovered + Gains + Counter + Brokerage + Derivs + CardInterest + CardInterchange + CardFees;
        public long Costs => Savers + Staff + Lost + Court + FundLost + Fees + Fine + Systems + CardLost + CardFraud + CardCosts + Funding + SaleLoss + DeskCosts;
        public long Profit => Income - Costs;

        public void Clear()
        {
            Interest = Reserve = Dividends = Recovered = Gains = 0;
            Savers = Staff = Lost = Court = FundLost = Fees = Fine = Systems = 0;
            Counter = Pay = PaidOut = 0;
            Brokerage = Derivs = CardInterest = CardInterchange = CardFees = CardLost = CardFraud = CardCosts = Funding = SaleLoss = DeskCosts = OwnerCardPaid = 0;
            Rolled = RiskTold = DeskCut = false;
            ShockNew = -1;
            Waterfall = default(Waterfall);
            DividendDue = false;
            Withdrawn = Deposited = Lent = Repaid = Defaulted = Cured = WrittenOff = 0;
            Bought = Sold = FireSold = 0;
            RunStarted = RunEnded = Breached = Cured2 = Fined = Watched = Closed = false;
            Why = ClosedWhy.None;
            WoundUp = 0;
        }
    }

    /// <summary>One listed company as the bank's fund sees it on a day (Wiring/OwnBankLinks, from the Stocks app's market).</summary>
    public struct FundQuote
    {
        public int StockId;
        public string Ticker, Name;
        public int LastCents;
        public int DpsCents;
        public long Shares;
        public int Size;                // MarketSize: 0 large, 1 mid, 2 small
        public int Distress;            // thousandths
        public bool Listed, UnderBid;
        public int GoneCents;           // gone from the market: what each share was paid out at
    }
}
