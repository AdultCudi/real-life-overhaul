using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>How a trading bot picks what to hold. Saved as a number: never renumber, add at the end.</summary>
    public enum BotStrategy { Trend = 0, Reversion = 1, Momentum = 2, Income = 3 }

    /// <summary>Where a hired bot stands. Saved as a number.</summary>
    public enum BotStatus
    {
        Running = 0,     // trading by its rules
        Stopped = 1,     // it hit the most it may lose, or was sold up: it holds nothing and trades nothing until started again
        SellingUp = 2    // asked to sell everything: it does at the market's next step, then stops
    }

    /// <summary>Which companies a hired bot trades. Saved as a number: never renumber, add at the end.</summary>
    public enum BotScope
    {
        Market = 0,      // its strategy's whole market, as its firm sets it (never a player's own company)
        Picked = 1,      // only the companies the player picked for it (Bot.Picks), their own listed company among them if they like
        Held = 2         // only the companies the player holds shares of themselves, their own listed company among them
    }

    /// <summary>How often a bot pays the player their share of its profit. Saved as a number.</summary>
    public enum BotPayEvery { Daily = 0, Weekly = 1, Monthly = 2 }

    /// <summary>One firm that runs a bot for hire: a proper name (not translated), its strategy and its terms. Never changed in play.</summary>
    public sealed class BotFirm
    {
        public readonly int Id;                 // saved: never reuse or renumber
        public readonly string Name;
        public readonly BotStrategy Strategy;
        public readonly int FeeBp;              // a month, of what the bot is worth at the month's end (MarketBots.FeeLeastAtStart at least)
        public readonly int ShareBp;            // of the profit it makes over its best month's end so far (the high-water mark)
        public readonly int Size, Stop, Loss;   // its settings to begin with: per cent of its worth in each holding, per cent under cost, per cent of its capital
        public readonly bool Small;             // it trades small companies too
        public readonly string Key;             // its texts: Key + "_about" (a part of a key, not one)

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

    /// <summary>A bot's holding in one company. What it cost is money paid, commission included.</summary>
    public sealed class BotHolding
    {
        public int StockId;
        public long Shares;
        public long Cost;
        public int StopCents;        // sold at the market's next price once the bid reaches this (0: no stop)
        public int OpenedDay;
        public long Gain;            // gains and losses taken on it so far (a holding sold off whole is a win if this is more than nothing)
        public bool Merged;          // (writing only) the shares were also written into the player's holding of the save's line: see MarketStore
    }

    /// <summary>An order a bot decided on at a close, for the market's first step of the next session.</summary>
    public struct BotOrder
    {
        public int StockId;
        public bool Buy;
        public long Qty;
    }

    /// <summary>
    /// A trading bot the player has hired. It trades the player's own cash: what it buys is paid from the player's money
    /// and what it sells is paid into it, each a transaction of the game's as the player's own trades are, up to the
    /// capital it was given (<see cref="Cash"/> is what of that it has not got in shares).
    /// </summary>
    public sealed class Bot
    {
        public int Firm;
        public BotStatus Status;
        public long Capital;         // what it was given
        public long Base;            // what the most it may lose is reckoned from: the capital, or its worth when it was started again
        public long Cash;            // of its money, what is not in shares (under nothing only after a fee its shares could not cover)
        public int Size = 20, Stop, Loss = 35;   // per cent: of its worth in each holding; under what a holding cost to sell it; of Base it may lose
        public int HiredDay;
        public int LastDay;          // the last close it worked at
        public long Peak;            // its highest worth at a close, for the drawdown
        public int DrawdownBp;       // the deepest fall from a peak so far
        public long HighWater;       // its worth after the profit share last taken: the share is only ever of profit above it
        public int Trades, Closed, Wins;   // fills; holdings sold off whole, and how many of them for more than they cost
        public long Realised, Fees, Commission, Dividends;
        public int Month;            // the month (MarketCatalogue.MonthOf) its statement counts
        public long MonthWorth;      // its worth at the start of that month
        public int MonthTrades, MonthDays;   // fills in it, and market days it was hired in it
        public int ShortDay;         // the last day it could not buy because the player's cash was short (told once a day)
        public readonly List<BotHolding> Holdings = new List<BotHolding>(10);
        public readonly List<BotOrder> Pending = new List<BotOrder>(10);

        // The shares it trades (MarketBots.InScope).
        public BotScope Scope;
        public readonly List<int> Picks = new List<int>(MarketBots.PicksMost);   // the companies picked for it (BotScope.Picked)

        // Payouts (MarketBots.Payout): a share of its profit taken off what it trades with, the rest kept to trade.
        public int PayPct;                          // per cent of new profit (MarketBots.PayPcts); 0: it keeps all of it
        public BotPayEvery PayEvery = BotPayEvery.Monthly;
        public long PayMark;                        // its profit so far (MarketBots.Profit) when it was last reckoned: only profit above it is new
        public long PayOwed;                        // reckoned but not yet paid: its money was in shares
        public long PaidOut, MonthPaid;             // paid to the player in all, and in the month its statement counts

        // The player's own company (MarketBots.OwnRoom): what it traded of it today and this game year, and when it last bought and sold it.
        public int OwnDay;
        public long OwnQty;
        public int OwnYear, OwnBoughtBp, OwnSoldBp;
        public int OwnBuyDay, OwnSellDay;

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

    /// <summary>
    /// Trading bots for hire: four firms and their strategies, their terms and every figure of their rules, how a bot
    /// decides, how its trades are booked, its worth. Game-free: the offline checks compile this file, the Guide prints
    /// from it. The market plays them (MarketEngine.Bots.cs) on its own clock.
    ///
    /// Why a bot cannot be played:
    ///   it decides at a close on what every player could see by then (the closes, the dividends, the results given
    ///   out) and trades at the market's first step of the next session, at the price of that moment: the offer for a
    ///   purchase, the bid for a sale, moved against it by its size as any order is, with the commission. So it never
    ///   knows tomorrow's prices; it pays the spread, the move and the commission on every trade, its trades move the
    ///   price as the player's do (counted with theirs, so splitting nothing saves anything), and a holding of its counts
    ///   towards the most of a company the player may hold. A stop is sold at the next price once the bid reaches it,
    ///   which after a gap is under the stop. The market is a fair bet at every moment, so no rule of a bot's beats it
    ///   but by luck: over a run of bad days it loses, and with large holdings and a wide limit it can lose most of what
    ///   it was given. Its fee and its share of the profit are taken on top. Nothing changes a bot while the market is
    ///   behind the clock or held after a load.
    ///
    /// What it trades (<see cref="BotScope"/>): its strategy's whole market, the companies the player picked for it, or
    /// those they hold. The player's own listed company may be among them, traded by the rules its founder deals by and
    /// within a share of its day's volume (<see cref="OwnRoom"/>), so that no bot of theirs moves its price on its own;
    /// another player's company never is. Its payouts (<see cref="Payout"/>): a share of the profit it makes, paid daily,
    /// weekly or monthly, a loss carried forward until it is made good.
    /// </summary>
    public static class MarketBots
    {
        public const int FirmsMost = 4;
        public const int CapitalLeastAtStart = 2500;   // the least a bot is given, at the start's prices (Prices.At)
        public const int FeeLeastAtStart = 5;          // the least monthly fee, at the start's prices
        public const int TradeLeastAtStart = 100;      // a bot makes no purchase worth less than this, at the start's prices
        public const int ListedDaysLeast = 20;         // it buys nothing listed for fewer days than this
        public const int PicksMost = 12;               // companies picked for one bot at most (the Bots tab's picker stays short)
        public const int OwnVolumePct = 10;            // the player's own company: every bot of theirs together trades at most this share of its ordinary day's volume in a day

        public static readonly int[] PayPcts = { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };   // per cent of new profit paid out (0: none)

        public static readonly int[] Sizes = { 10, 20, 25, 50, 100 };   // per cent of its worth in each holding (so 10, 5, 4, 2 or 1 holdings)
        public static readonly int[] Stops = { 0, 5, 10, 15, 25 };      // per cent under what a holding cost (0: none)
        public static readonly int[] Losses = { 20, 35, 50, 75, 90 };   // per cent of what it was given

        // ---- the strategies' rules, on daily closes
        public const int TrendFast = 20, TrendSlow = 50;                  // trend: in while the close is over the 20-day average and that over the 50-day; out under the 50-day
        public const int RsiDays = 5, RsiIn = 30, RsiOut = 60, ReversionFilter = 50, ReversionDaysMost = 10;   // reversion: in at a 5-day RSI under 30 over the 50-day average; out over 60 or after 10 market days
        public const int MomentumDays = 60, MomentumSkip = 5;            // momentum: the gain over 60 market days to a week ago, held for a month
        public const int IncomeQuarters = 4;                             // income: four quarters of results with a profit, the dividend no more than it
        public const int CloseHistoryMost = MarketCatalogue.DailyKept;

        private const string P = "reallife";   // (apart, so that a key made from parts is not taken for a whole one)

        public static readonly BotFirm[] All =
        {
            new BotFirm(1, "Meridian Trend Partners", BotStrategy.Trend, 20, 2000, 20, 10, 35, false, P + "_mkt_bot_trend"),
            new BotFirm(2, "Quayside Reversion Fund", BotStrategy.Reversion, 15, 2000, 20, 10, 35, false, P + "_mkt_bot_reversion"),
            new BotFirm(3, "Larkspur Momentum", BotStrategy.Momentum, 20, 2500, 25, 15, 50, true, P + "_mkt_bot_momentum"),
            new BotFirm(4, "Harbourline Income", BotStrategy.Income, 10, 1000, 10, 0, 35, false, P + "_mkt_bot_income")
        };

        public static BotFirm Firm(int id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        public static Bot Hired(MarketState state, int firm)
        {
            for (int i = 0; i < state.Bots.Count; i++)
            {
                if (state.Bots[i].Firm == firm)
                {
                    return state.Bots[i];
                }
            }
            return null;
        }

        /// <summary>A setting held to one of its choices: the nearest at or under it, else the first.</summary>
        public static int Choice(int[] choices, int value)
        {
            int best = choices[0];
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i] <= value)
                {
                    best = choices[i];
                }
            }
            return best;
        }

        public static int IndexOf(int[] choices, int value)
        {
            int at = Array.IndexOf(choices, value);
            return at < 0 ? 0 : at;
        }

        /// <summary>How many holdings a bot keeps at most with so much of its worth in each.</summary>
        public static int Slots(Bot b)
        {
            return Math.Max(1, 100 / Math.Max(1, b.Size));
        }

        /// <summary>Shares every bot of the player's holds of a company.</summary>
        public static long Held(MarketState state, int stockId)
        {
            long held = 0;
            for (int i = 0; i < state.Bots.Count; i++)
            {
                BotHolding h = state.Bots[i].Holding(stockId);
                held += h != null ? h.Shares : 0;
            }
            return held;
        }

        /// <summary>What a bot is worth: its money and its holdings at the bid.</summary>
        public static long Worth(MarketState state, Bot b, int phase)
        {
            long worth = b.Cash;
            for (int i = 0; i < b.Holdings.Count; i++)
            {
                BotHolding h = b.Holdings[i];
                Stock s = state.Stock(h.StockId);
                if (s != null && s.Listed)
                {
                    worth += MarketTrading.Value(h.Shares, MarketTrading.QuoteOf(state, s, phase).Bid, false);
                }
            }
            return worth;
        }

        /// <summary>Its worth under which it sells everything and stops.</summary>
        public static long Floor(Bot b)
        {
            return b.Base * (100 - b.Loss) / 100;
        }

        /// <summary>A holding's stop for what it cost a share, at a bot's setting (0: none).</summary>
        public static int StopFor(Bot b, long shares, long cost)
        {
            if (b.Stop <= 0 || shares <= 0)
            {
                return 0;
            }
            long each = cost * 100 / shares;   // cents a share
            return (int)Math.Max(1, Math.Min(int.MaxValue, each * (100 - b.Stop) / 100));
        }

        /// <summary>The month's fee at a worth, before it is shared out over the days hired: a share of the worth, the least at least.</summary>
        public static long Fee(BotFirm f, long worth, int least)
        {
            long share = (Math.Max(0, worth) * f.FeeBp + 9999L) / 10000L;
            return Math.Max(least, share);
        }

        /// <summary>Market days in a month of the game's (MarketCatalogue.MonthOf).</summary>
        public static int MarketDaysIn(int month, int year)
        {
            int from = MarketHistory.MonthStart(month, year), to = MarketHistory.MonthStart(month + 1, year);
            int count = 0;
            for (int d = from; d < to; d++)
            {
                count += MarketCatalogue.TradingDay(d) ? 1 : 0;
            }
            return Math.Max(1, count);
        }

        /// <summary>A company a bot may buy: listed long enough, not the player's or another player's, no bid on it, not a penny share, of a size it trades.</summary>
        public static bool Buyable(MarketState state, BotFirm f, Stock s, int day)
        {
            return s.Listed && s.Own == null && s.BidCents <= 0 && day - s.ListedDay >= ListedDaysLeast && (f.Small || s.Size != MarketSize.Small)
                   && s.Last * 1000L >= 2L * MarketCatalogue.PennyCents * Math.Max(100, state.Env.PriceLevel);
        }

        // ---------------------------------------------------------------- the shares a bot trades

        /// <summary>True for a listed company of a player's whose founder is the player of this machine (MarketEngine.Mine).</summary>
        public static bool Mine(MarketState state, Stock s)
        {
            return s != null && s.Own != null && state.Me != 0UL && s.Own.Owner == state.Me && !state.Following;
        }

        /// <summary>
        /// Whether a company is among those a bot trades, by its scope: its strategy's market (no player's company), the
        /// companies picked for it, or those the player holds themselves (as its founder too). A holding outside it is sold
        /// at the next session.
        /// </summary>
        public static bool InScope(MarketState state, Bot b, Stock s)
        {
            if (s == null || (s.Own != null && !Mine(state, s)))
            {
                return false;   // (another player's company is theirs: never traded by a bot)
            }
            switch (b.Scope)
            {
                case BotScope.Picked:
                    return b.Picks.Contains(s.Id);
                case BotScope.Held:
                    return s.Own != null ? s.Own.Founder > 0 : state.Held(s.Id) > 0;
                default:
                    return s.Own == null;
            }
        }

        /// <summary>
        /// A company a bot may buy now. Its strategy's market is what its firm buys (<see cref="Buyable"/>); a company the
        /// player chose, by a pick or by holding it, is bought whatever its size, but never one listed too short a time,
        /// under a bid or a penny share.
        /// </summary>
        public static bool Allowed(MarketState state, Bot b, Stock s, int day)
        {
            BotFirm f = b.Def;
            if (f == null || s == null)
            {
                return false;
            }
            if (b.Scope == BotScope.Market)
            {
                return Buyable(state, f, s, day);
            }
            return InScope(state, b, s) && s.Listed && s.BidCents <= 0 && day - s.ListedDay >= ListedDaysLeast
                   && s.Last * 1000L >= 2L * MarketCatalogue.PennyCents * Math.Max(100, state.Env.PriceLevel);
        }

        /// <summary>A company picked for a bot, or taken off its picks. False when nothing changed (or it already has the most picks).</summary>
        public static bool Pick(Bot b, int stockId, bool on)
        {
            int at = b.Picks.IndexOf(stockId);
            if (on == (at >= 0) || stockId <= 0)
            {
                return false;
            }
            if (!on)
            {
                b.Picks.RemoveAt(at);
                return true;
            }
            if (b.Picks.Count >= PicksMost)
            {
                return false;
            }
            b.Picks.Add(stockId);
            return true;
        }

        /// <summary>The days within which a bot that bought the player's own company may not sell it, nor buy it back after a sale: the founder's short-swing time.</summary>
        public static int OwnSwingDays(int year)
        {
            return Math.Max(1, Math.Max(8, year) / ListingCatalogue.ShortSwingYearShare);
        }

        /// <summary>
        /// The most shares of the player's own company their bots may buy or sell now, so that a bot never pushes its
        /// price on its own and keeps every rule its founder deals by: nothing in the closed time before its results;
        /// every bot together no more than <see cref="OwnVolumePct"/> of its ordinary day's volume in a day; never a sale
        /// within the short-swing time of a purchase, nor a purchase within it of a sale; the founder's own deals and the
        /// bots' together no more of the company in a game year than the founder may deal in; and the founder's shares
        /// and the bots' together no more than the most the founder may hold. Any other company: no limit of this kind
        /// (long.MaxValue). Another player's company: none at all. Reads the company and the player's own bots only, so
        /// every machine of a Going Public session that plays the company comes to the same price.
        /// </summary>
        public static long OwnRoom(MarketState state, Stock s, bool buying, int day, int year)
        {
            if (s == null || s.Own == null)
            {
                return long.MaxValue;
            }
            if (!Mine(state, s) || !s.Listed || ListingRules.Closed(s, day, year))
            {
                return 0;
            }
            OwnListing o = s.Own;
            int y = MarketCatalogue.YearOf(day, Math.Max(8, year)), window = OwnSwingDays(year);
            long today = 0, held = 0;
            int bp = ListingRules.YearUsedBp(o, day, year, buying ? o.BoughtBp : o.SoldBp);
            for (int i = 0; i < state.Bots.Count; i++)
            {
                Bot b = state.Bots[i];
                today += b.OwnDay == day ? b.OwnQty : 0;
                bp += b.OwnYear == y ? (buying ? b.OwnBoughtBp : b.OwnSoldBp) : 0;
                int against = buying ? b.OwnSellDay : b.OwnBuyDay;
                if (against > 0 && day - against < window)
                {
                    return 0;
                }
                BotHolding h = b.Holding(s.Id);
                held += h != null ? h.Shares : 0;
            }
            long room = Math.Max(0L, (long)Math.Floor(Math.Max(0.0, s.Adv) * OwnVolumePct / 100.0) - today);
            room = Math.Min(room, s.Shares * Math.Max(0, ListingCatalogue.FounderDealMostBpYear - bp) / 10000L);
            if (buying)
            {
                room = Math.Min(room, s.Shares * ListingCatalogue.FounderMostBp / 10000L - o.Founder - held);
            }
            return Math.Max(0L, room);
        }

        /// <summary>A bot's trade in the player's own company counted against the day's, the year's and the short-swing limits (<see cref="OwnRoom"/>).</summary>
        public static void OwnCount(Bot b, Stock s, bool buying, long qty, int day, int year)
        {
            if (b.OwnDay != day)
            {
                b.OwnDay = day;
                b.OwnQty = 0;
            }
            b.OwnQty += qty;
            int y = MarketCatalogue.YearOf(day, Math.Max(8, year));
            if (b.OwnYear != y)
            {
                b.OwnYear = y;
                b.OwnBoughtBp = b.OwnSoldBp = 0;
            }
            int bp = ListingRules.Bp(qty, s.Shares);
            if (buying)
            {
                b.OwnBoughtBp += bp;
                b.OwnBuyDay = day;
            }
            else
            {
                b.OwnSoldBp += bp;
                b.OwnSellDay = day;
            }
        }

        // ---------------------------------------------------------------- payouts

        /// <summary>What a bot has made so far and kept: its gains and losses on what it sold, its dividends after tax, less its fees and its share.</summary>
        public static long Profit(Bot b)
        {
            return b.Realised + b.Dividends - b.Fees;
        }

        /// <summary>The losses a bot must make good before it pays anything again.</summary>
        public static long LossesToMakeGood(Bot b)
        {
            return Math.Max(0L, b.PayMark - Profit(b));
        }

        /// <summary>True when a payout falls due at this market day's close: every day, at a week's last market day, or at a month's.</summary>
        public static bool PayDue(BotPayEvery every, int day, int year)
        {
            int next = MarketTrading.NextTradingDay(day);
            switch (every)
            {
                case BotPayEvery.Daily:
                    return true;
                case BotPayEvery.Weekly:
                    return MarketCatalogue.WeekOf(next) != MarketCatalogue.WeekOf(day);
                default:
                    return MarketCatalogue.MonthOf(next, year) != MarketCatalogue.MonthOf(day, year);
            }
        }

        /// <summary>
        /// A bot's payout at its time. The profit it made since it was last reckoned is new only above the best it had
        /// reached (<see cref="Bot.PayMark"/>): a loss is carried forward, and nothing is paid until it is made good. Its
        /// share of the new profit is owed to the player and paid out of the bot's money, as far as its money not in shares
        /// goes (the rest waits for the next time). The bot trades the player's own cash, so the money is already in their
        /// account: a payout takes it off what the bot may trade with, and the figures its limits and its share of profit
        /// are reckoned from come down with it, so a payout is never counted as a loss. What was paid.
        /// </summary>
        public static long Payout(Bot b)
        {
            long profit = Profit(b);
            if (profit > b.PayMark)
            {
                b.PayOwed += (profit - b.PayMark) * Math.Max(0, Math.Min(100, b.PayPct)) / 100;
                b.PayMark = profit;
            }
            long pay = Math.Min(b.PayOwed, Math.Max(0L, b.Cash));
            if (pay <= 0)
            {
                return 0;
            }
            b.Cash -= pay;
            b.PayOwed -= pay;
            b.PaidOut += pay;
            b.MonthPaid += pay;
            b.Base = Math.Max(0L, b.Base - pay);
            b.Peak = Math.Max(0L, b.Peak - pay);
            b.HighWater -= pay;
            b.MonthWorth -= pay;
            return pay;
        }

        // ---------------------------------------------------------------- booking a trade

        /// <summary>
        /// A bot's shares bought or sold at a price, with the player's money moved as their own trades move it and the
        /// bot's holding changed. False, and nothing changed, when the bot's money or the player's cash does not cover a
        /// purchase, or the bot does not hold what it would sell. With <paramref name="move"/> false the lasting move of
        /// the price is given in <paramref name="kept"/> and not put on the company (a Going Public session's market puts it on its tape).
        /// </summary>
        public static bool Fill(MarketState state, Bot b, Stock s, bool buying, long qty, int cents, int least, IMarketWallet wallet, int phase, bool move, out double kept, out bool shortOfCash)
        {
            kept = 0.0;
            shortOfCash = false;
            if (qty <= 0 || cents <= 0)
            {
                return false;
            }
            long value = MarketTrading.Value(qty, cents, buying);
            long fee = MarketTrading.Commission(value, least);
            string name = s.Name + " (" + (b.Def != null ? b.Def.Name : string.Empty) + ")";
            BotHolding h = b.Holding(s.Id);
            if (buying)
            {
                if (b.Cash < value + fee)
                {
                    return false;
                }
                if (wallet.Cash < value + fee)
                {
                    shortOfCash = true;
                    return false;
                }
                if (!wallet.Take(value, MarketMoney.Bought, name))
                {
                    shortOfCash = true;
                    return false;
                }
                if (!wallet.Take(fee, MarketMoney.Commission, name))
                {
                    wallet.Force(fee, MarketMoney.Commission, name);
                }
                b.Cash -= value + fee;
                if (h == null)
                {
                    h = new BotHolding { StockId = s.Id, OpenedDay = state.Day };
                    b.Holdings.Add(h);
                }
                h.Shares += qty;
                h.Cost += value + fee;
                h.StopCents = StopFor(b, h.Shares, h.Cost);
            }
            else
            {
                if (h == null || h.Shares < qty)
                {
                    return false;
                }
                fee = Math.Min(fee, value);
                wallet.Give(value, MarketMoney.Sold, name);
                if (!wallet.Take(fee, MarketMoney.Commission, name))
                {
                    wallet.Force(fee, MarketMoney.Commission, name);
                }
                b.Cash += value - fee;
                long part = h.Cost * qty / h.Shares;
                long gain = value - fee - part;
                b.Realised += gain;
                h.Gain += gain;
                h.Cost -= part;
                h.Shares -= qty;
                if (h.Shares <= 0)
                {
                    b.Holdings.Remove(h);
                    b.Closed++;
                    b.Wins += h.Gain > 0 ? 1 : 0;
                }
            }
            b.Commission += fee;
            b.Trades++;
            b.MonthTrades++;
            // The market moves as it does for the player's own trade, and their recent trading in it (theirs and their bots') grows.
            double share = Math.Sqrt(qty / Math.Max(1.0, s.Adv));
            kept = (buying ? 1.0 : -1.0) * MarketCatalogue.ImpactPermanent * MarketCatalogue.ImpactTemporary * MarketTrading.DayVol(state, s) * share;
            if (move)
            {
                s.Mis += kept;
                s.Sent += kept;
                s.DayVolume += qty;
            }
            s.Impact += qty;
            MarketTrading.AddTrade(state, new Trade { Day = state.Day, Phase = phase, StockId = s.Id, Kind = buying ? TradeKind.Bought : TradeKind.Sold, Qty = qty, PriceCents = cents,
                Value = buying ? -value : value, Fee = fee, Bot = b.Firm });
            return true;
        }

        /// <summary>The most shares of a company a bot may buy now: an order's most, and what is left of the most the player may hold, theirs and their bots' together.</summary>
        public static long Room(MarketState state, Stock s)
        {
            long held = Math.Abs(state.Held(s.Id)) + Held(state, s.Id);
            return Math.Max(0L, Math.Min(MarketTrading.MostPerOrder(s), MarketTrading.MostHeld(s) - held));
        }

        /// <summary>A split: every bot's shares multiplied, its stop and what it waits to trade with them (a sale's stop rounded down: never easier to reach).</summary>
        public static void Split(MarketState state, Stock s, int ratio)
        {
            for (int i = 0; i < state.Bots.Count; i++)
            {
                Bot b = state.Bots[i];
                BotHolding h = b.Holding(s.Id);
                if (h != null)
                {
                    h.Shares *= ratio;
                    h.StopCents = h.StopCents > 0 ? Math.Max(1, h.StopCents / ratio) : 0;
                }
                for (int j = 0; j < b.Pending.Count; j++)
                {
                    BotOrder o = b.Pending[j];
                    if (o.StockId == s.Id)
                    {
                        o.Qty *= ratio;
                        b.Pending[j] = o;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- deciding, at a close

        /// <summary>Scratch the decisions work in, kept by the market (nothing is allocated at a close).</summary>
        public sealed class Work
        {
            public readonly double[] Closes = new double[CloseHistoryMost];
            public readonly double[] A = new double[CloseHistoryMost];
            public readonly double[] B = new double[CloseHistoryMost];
            public readonly List<int> Ids = new List<int>(160);
            public readonly List<double> Scores = new List<double>(160);
        }

        // A company's daily closes, oldest first; how many.
        private static int Closes(Stock s, double[] into)
        {
            int n = Math.Min(s.Daily.Count, into.Length);
            int from = s.Daily.Count - n;
            for (int i = 0; i < n; i++)
            {
                into[i] = s.Daily[from + i].C;
            }
            return n;
        }

        /// <summary>
        /// A company's score for a strategy at today's close, from its closes and what it has given out: more than nothing
        /// to be held (the larger the better), NaN when it has too little history or the rule says no.
        /// </summary>
        public static double Score(BotStrategy strategy, Stock s, Work w)
        {
            int n = Closes(s, w.Closes);
            double c = n > 0 ? w.Closes[n - 1] : 0.0;
            switch (strategy)
            {
                case BotStrategy.Trend:
                    {
                        if (n < TrendSlow)
                        {
                            return double.NaN;
                        }
                        MarketIndicators.Sma(w.Closes, n, TrendFast, w.A);
                        MarketIndicators.Sma(w.Closes, n, TrendSlow, w.B);
                        double fast = w.A[n - 1], slow = w.B[n - 1];
                        return c > fast && fast > slow ? c / slow - 1.0 : double.NaN;
                    }
                case BotStrategy.Reversion:
                    {
                        if (n < Math.Max(ReversionFilter, RsiDays + 1))
                        {
                            return double.NaN;
                        }
                        MarketIndicators.Rsi(w.Closes, n, RsiDays, w.A);
                        MarketIndicators.Sma(w.Closes, n, ReversionFilter, w.B);
                        double rsi = w.A[n - 1];
                        return rsi < RsiIn && c > w.B[n - 1] ? RsiIn - rsi : double.NaN;
                    }
                case BotStrategy.Momentum:
                    {
                        if (n < MomentumDays + MomentumSkip + 1)
                        {
                            return double.NaN;
                        }
                        double then = w.Closes[n - 1 - MomentumSkip - MomentumDays], recent = w.Closes[n - 1 - MomentumSkip];
                        double gain = then > 0.0 ? recent / then - 1.0 : double.NaN;
                        return gain > 0.0 ? gain : double.NaN;
                    }
                default:
                    {
                        // Income: the dividend over a year on today's price, from a company that earned it.
                        if (s.Results.Count < IncomeQuarters || s.DpsCents <= 0 || s.Last <= 0)
                        {
                            return double.NaN;
                        }
                        long eps = 0;
                        for (int i = s.Results.Count - IncomeQuarters; i < s.Results.Count; i++)
                        {
                            if (s.Results[i].ProfitK <= 0)
                            {
                                return double.NaN;
                            }
                            eps += s.Results[i].Eps;
                        }
                        // (Eps in hundredths of a cent; the dividend in cents)
                        return s.DpsCents * 4L * 100L <= eps ? s.DpsCents * 4.0 / s.Last : double.NaN;
                    }
            }
        }

        /// <summary>Whether a holding is sold by its strategy's rule at today's close (its stop and the bot's limit are the market's, during the session).</summary>
        public static bool Exit(BotStrategy strategy, Stock s, BotHolding h, int day, Work w)
        {
            if (!s.Listed)
            {
                return false;
            }
            int n = Closes(s, w.Closes);
            if (n == 0)
            {
                return false;
            }
            double c = w.Closes[n - 1];
            switch (strategy)
            {
                case BotStrategy.Trend:
                    if (n < TrendSlow)
                    {
                        return false;
                    }
                    MarketIndicators.Sma(w.Closes, n, TrendSlow, w.B);
                    return c < w.B[n - 1];
                case BotStrategy.Reversion:
                    if (n < RsiDays + 1)
                    {
                        return false;
                    }
                    MarketIndicators.Rsi(w.Closes, n, RsiDays, w.A);
                    return w.A[n - 1] > RsiOut || MarketDaysBetween(h.OpenedDay, day) >= ReversionDaysMost;
                default:
                    return false;   // momentum and income: at the month's turn (Decide)
            }
        }

        private static int MarketDaysBetween(int from, int to)
        {
            int count = 0;
            for (int d = from + 1; d <= to; d++)
            {
                count += MarketCatalogue.TradingDay(d) ? 1 : 0;
            }
            return count;
        }

        /// <summary>True on the first market day of a month (a month's strategies turn their holdings over then).</summary>
        public static bool MonthTurns(int day, int year)
        {
            int before = day - 1;
            while (before > 0 && !MarketCatalogue.TradingDay(before))
            {
                before--;
            }
            return before <= 0 || MarketCatalogue.MonthOf(before, year) != MarketCatalogue.MonthOf(day, year);
        }

        /// <summary>
        /// What a bot will do at the next session's first step, decided at a close (or when it is hired while the market is
        /// shut) on what is known by then: its holdings to sell by its rule, then companies to buy into the holdings it
        /// has room for, best first, each for so much of its worth as it was told, out of the money it has and what its
        /// sales bring. Prices are reckoned at the bid and the offer of now; the market's price at the step decides.
        /// </summary>
        public static void Decide(MarketState state, Bot b, int day, int year, int least, Work w)
        {
            b.Pending.Clear();
            BotFirm f = b.Def;
            if (f == null || b.Status != BotStatus.Running)
            {
                return;
            }
            int phase = MarketCatalogue.PhaseClose;
            long worth = Worth(state, b, phase);
            bool turn = f.Strategy == BotStrategy.Momentum || f.Strategy == BotStrategy.Income;
            bool rebalance = !turn || MonthTurns(MarketTrading.NextTradingDay(day), year) || b.Holdings.Count == 0;
            int slots = Slots(b);
            // The best now, by the strategy's score.
            w.Ids.Clear();
            w.Scores.Clear();
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                Stock s = state.Stocks[i];
                if (!Allowed(state, b, s, day))
                {
                    continue;
                }
                double score = Score(f.Strategy, s, w);
                if (double.IsNaN(score) || score <= 0.0)
                {
                    continue;
                }
                int at = w.Scores.Count;
                while (at > 0 && (w.Scores[at - 1] < score || (w.Scores[at - 1] == score && w.Ids[at - 1] > s.Id)))
                {
                    at--;
                }
                w.Scores.Insert(at, score);
                w.Ids.Insert(at, s.Id);
            }
            // Sales: out of its scope (a pick taken off, a holding of the player's sold), by the rule, or (at a month's turn) out of the best twice over.
            long freed = 0;
            int kept = 0;
            for (int i = 0; i < b.Holdings.Count; i++)
            {
                BotHolding h = b.Holdings[i];
                Stock s = state.Stock(h.StockId);
                if (s == null || !s.Listed)
                {
                    continue;
                }
                bool sell = !InScope(state, b, s) || (turn ? rebalance && Rank(w, h.StockId) >= 2 * slots : Exit(f.Strategy, s, h, day, w));
                if (sell)
                {
                    b.Pending.Add(new BotOrder { StockId = h.StockId, Buy = false, Qty = h.Shares });
                    freed += MarketTrading.Value(h.Shares, MarketTrading.QuoteOf(state, s, phase).Bid, false);
                }
                else
                {
                    kept++;
                }
            }
            if (!rebalance)
            {
                return;
            }
            // Purchases into the room left.
            long money = Math.Max(0L, b.Cash) + freed * 99 / 100;
            long target = worth * b.Size / 100;
            long tradeLeast = Math.Max(1L, (long)least * TradeLeastAtStart / Math.Max(1, MarketCatalogue.CommissionLeastAtStart));
            for (int i = 0; i < w.Ids.Count && kept < slots; i++)
            {
                if (b.Holding(w.Ids[i]) != null)
                {
                    continue;
                }
                Stock s = state.Stock(w.Ids[i]);
                Quote q = MarketTrading.QuoteOf(state, s, MarketCatalogue.PhaseOpen + 1);
                long spend = Math.Min(target, money);
                // (a little held back for the commission and the move of the price)
                long qty = Math.Min(Math.Min(Room(state, s), OwnRoom(state, s, true, MarketTrading.NextTradingDay(day), year)), (long)(spend * 100.0 / (q.Ask * 1.01) - 0.5));
                long value = MarketTrading.Value(Math.Max(0, qty), q.Ask, true);
                if (qty <= 0 || value < tradeLeast)
                {
                    continue;
                }
                b.Pending.Add(new BotOrder { StockId = s.Id, Buy = true, Qty = qty });
                money -= value + MarketTrading.Commission(value, least);
                kept++;
            }
        }

        // Where a company stands among today's best (int.MaxValue: not among them).
        private static int Rank(Work w, int stockId)
        {
            int at = w.Ids.IndexOf(stockId);
            return at < 0 ? int.MaxValue : at;
        }
    }

    /// <summary>
    /// The dividends paid to the player since they were last told, for one notification: the one dividend with its
    /// company and (a bot's) its firm, or how many and how much in all when several land at once. Kept by the director,
    /// filled as each is paid and emptied when it is told, so nothing is allocated.
    /// </summary>
    public struct DividendTally
    {
        public int Count;
        public long Total;
        public int Stock, Firm;      // the first one's company and firm (0: the player's own shares)

        public void Add(int stock, int firm, long money)
        {
            if (money <= 0)
            {
                return;
            }
            if (Count == 0)
            {
                Stock = stock;
                Firm = firm;
            }
            Count++;
            Total += money;
        }

        public void Clear()
        {
            Count = 0;
            Total = 0;
            Stock = Firm = 0;
        }
    }
}
