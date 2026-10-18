using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>How a trading bot picks what to hold. Saved as a number: never renumber, add at the end.</summary>
    public enum BotStrategy { Trend = 0, Reversion = 1, Momentum = 2, Income = 3 }

    /// <summary>Where a hired bot stands. Saved as a number.</summary>
    public enum BotStatus
    {
        Running = 0,
        Stopped = 1,     // at its limit or sold up: holds and trades nothing until started again
        SellingUp = 2    // sells everything at the market's next step, then stops
    }

    /// <summary>Which companies a hired bot trades. Saved as a number: never renumber, add at the end.</summary>
    public enum BotScope
    {
        Market = 0,      // its strategy's market, as its firm sets it; never a player's own company
        Picked = 1,      // only the companies picked for it, the player's own among them if they like
        Held = 2         // only the companies the player holds themselves, their own among them
    }

    /// <summary>How often a bot pays the player their share of its profit. Saved as a number.</summary>
    public enum BotPayEvery { Daily = 0, Weekly = 1, Monthly = 2 }

    /// <summary>One firm that runs a bot for hire: a proper name, never translated, its strategy and its terms.</summary>
    public sealed class BotFirm
    {
        public readonly int Id;                 // saved: never reuse or renumber
        public readonly string Name;
        public readonly BotStrategy Strategy;
        public readonly int FeeBp;              // a month, of its worth at the month's end
        public readonly int ShareBp;            // of the profit over its high-water mark
        public readonly int Size, Stop, Loss;   // to begin with: per cent of worth in each holding, under cost, of capital
        public readonly bool Small;             // it trades small companies too
        public readonly string Key;             // its texts: Key + "_about"

        public BotFirm(int id, string name, BotStrategy strategy, int feeBp, int shareBp, int size, int stop, int loss, bool small, string key)
        {
            Id = id;
            Name = name;
            Strategy = strategy;
            FeeBp = feeBp;
            ShareBp = shareBp;
            Size = size;
            Stop = stop;
            Loss = loss;
            Small = small;
            Key = key;
        }
    }

        /// <summary>Its cost is money paid, commission included.</summary>
    public sealed class BotHolding
    {
        public int StockId;
        public long Shares;
        public long Cost;
        public int StopCents;        // sold at the next price once the bid reaches it; 0 none
        public int OpenedDay;
        public long Gain;            // gains and losses taken on it so far; a holding sold whole is a win above nothing
        public bool Merged;          // writing only: the shares also went into the player's holding of the save line
    }

    /// <summary>An order a bot decided on at a close, for the market's first step of the next session.</summary>
    public struct BotOrder
    {
        public int StockId;
        public bool Buy;
        public long Qty;
    }

    /// <summary>A trading bot the player has hired: it trades the player's own cash up to its capital; <see cref="Cash"/> is what is not in shares.</summary>
    public sealed class Bot
    {
        public int Firm;
        public BotStatus Status;
        public long Capital;         // what it was given
        public long Base;            // the capital, or its worth when started again: what its limit is reckoned from
        public long Cash;            // not in shares; negative only after a fee its shares could not cover
        public int Size = 20, Stop, Loss = 35;   // per cent: of worth in each holding; under cost to sell; of Base it may lose
        public int HiredDay;
        public int LastDay;          // the last close it worked at
        public long Peak;            // its highest worth at a close, for the drawdown
        public int DrawdownBp;       // the deepest fall from a peak so far
        public long HighWater;       // its worth after the last profit share: the share is only of profit above it
        public int Trades, Closed, Wins;   // fills; holdings sold whole, and how many for more than they cost
        public long Realised, Fees, Commission, Dividends;
        public int Month;            // MarketCatalogue.MonthOf of the month its statement counts
        public long MonthWorth;      // its worth at the start of that month
        public int MonthTrades, MonthDays;   // fills in it, and market days hired in it
        public int ShortDay;         // the last day the player's cash was short of a purchase: told once a day
        public readonly List<BotHolding> Holdings = new List<BotHolding>(10);
        public readonly List<BotOrder> Pending = new List<BotOrder>(10);

        public BotScope Scope;
        public readonly List<int> Picks = new List<int>(MarketBots.PicksMost);   // BotScope.Picked

        // Payouts (MarketBots.Payout): a share of its profit taken off what it trades with.
        public int PayPct;                          // per cent of new profit; 0 keeps all of it
        public BotPayEvery PayEvery = BotPayEvery.Monthly;
        public long PayMark;                        // its profit when last reckoned: only profit above it is new
        public long PayOwed;                        // reckoned but not paid: its money was in shares
        public long PaidOut, MonthPaid;             // to the player in all, and in its statement's month

        // The player's own company (MarketBots.OwnRoom): what it traded of it today and this year, and its last purchase and sale.
        public int OwnDay;
        public long OwnQty;
        public int OwnYear, OwnBoughtBp, OwnSoldBp;
        public int OwnBuyDay, OwnSellDay;

        // Capital added or taken out while it runs.
        public int TopUp;                           // a MarketBots.TopUp state; 0 none
        public long MonthAdded;                     // money added this month times the market days before it; less for money taken out

        public BotFirm Def => MarketBots.Firm(Firm);

        public BotHolding Holding(int stockId)
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
}
