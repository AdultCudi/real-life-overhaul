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

    /// <summary>What a day did, in cents, as booked at the headquarters and told to the player. Its profit is kept in the bank's capital.</summary>
    public sealed class BankDay
    {
        public long Interest, Reserve, Dividends, Recovered, Gains;    // in: takings
        public long Savers, Staff, Lost, Court, FundLost, Fees, Fine;  // out
        public long Systems;                    // out, at the end game's scale: its systems, compliance and deposit insurance
        public long Withdrawn, Deposited, Lent, Repaid, Defaulted, Cured, WrittenOff;
        public long Bought, Sold, FireSold;
        public bool RunStarted, RunEnded, Breached, Cured2, Fined, Watched, Closed;
        public ClosedWhy Why;
        public long WoundUp;                    // what winding it up left for the player

        public long Income => Interest + Reserve + Dividends + Recovered + Gains;
        public long Costs => Savers + Staff + Lost + Court + FundLost + Fees + Fine + Systems;
        public long Profit => Income - Costs;

        public void Clear()
        {
            Interest = Reserve = Dividends = Recovered = Gains = 0;
            Savers = Staff = Lost = Court = FundLost = Fees = Fine = Systems = 0;
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
