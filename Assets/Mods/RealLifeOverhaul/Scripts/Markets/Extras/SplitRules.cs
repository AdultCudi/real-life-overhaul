using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Why a founder's split cannot be asked for. Each has a sentence (ExtrasText).</summary>
    public enum SplitWhy
    {
        None = 0,
        Off,         // the extras are switched off, or the market is not this machine's to change
        NotMine,     // no listed company of the player's own
        Firm,        // a firm with investors: its shares are lines of its cap table too
        Board,       // the founder does not chair the board
        Pending,     // a split is announced already
        Bid,         // a bid for the company stands
        Ratio,       // not one of the ratios a board may choose
        TooLow,      // the shares would trade under the least
        TooHigh,     // the consolidated shares would trade over the most
        Count
    }

    /// <summary>What a consolidation left over for the player: whole money paid for parts of a share, and what was closed.</summary>
    public struct ReverseResult
    {
        public long Cash;            // whole money owed to the player for parts of a share (under nothing: owed by them on a short)
        public long FounderCash;     // the same for the founder's own shares of their company
        public long BotCash;         // ...and for their bots' holdings, already added to each bot's money
        public int Cancelled;        // open orders too small to be one share
    }

    /// <summary>
    /// Splits beyond the market's old one: each board's own level by its company's id, shares put together where a price
    /// has fallen under a unit, and the founder's own choice. A consolidation (so many old shares for one new) changes every
    /// count and price in the same moment; a part of a share cannot be held, so it is paid for at the price before, to the
    /// cent (cents under a whole unit of money wait in <see cref="ExtrasState.OddCents"/>). Game-free; run once at an open.
    /// </summary>
    public static class SplitRules
    {
        /// <summary>The price over which a company's board splits its shares: its own, from its id, at or under the market's old level.</summary>
        public static int LevelOf(int stockId)
        {
            int step = (int)(MarketRandom.Mix32((uint)stockId * 2654435761u + 0x51ed27u) % (uint)(ExtrasCatalogue.SplitLevelSteps + 1));
            return Math.Min(MarketCatalogue.SplitAboveCents, ExtrasCatalogue.SplitLevelLeastCents + step * ExtrasCatalogue.SplitLevelStepCents);
        }

        /// <summary>
        /// True when a city company's board would split now by its own level; over the market's old level the old rule splits it. A player's company
        /// splits by the old rule or when its founder asks: its board is theirs.
        /// </summary>
        public static bool WantsSplit(Stock s)
        {
            return s != null && s.Listed && s.Own == null && s.SplitDay <= 0 && s.BidCents <= 0 && s.Last > LevelOf(s.Id) && s.Last <= MarketCatalogue.SplitAboveCents;
        }

        /// <summary>True when a sound company whose shares have closed under a unit for long enough puts them together.</summary>
        public static bool WantsReverse(Stock s)
        {
            return s != null && s.Listed && s.Own == null && s.SplitDay <= 0 && s.BidCents <= 0 && s.LowDays >= ExtrasCatalogue.ReverseAfterDays
                   && s.Distress < ExtrasCatalogue.ReverseDistressMost;
        }

        /// <summary>How many old shares make one new: the smallest ratio that takes the price to the target at today's prices, else the largest.</summary>
        public static int ReverseRatio(int lastCents, int priceLevel)
        {
            long target = (long)ExtrasCatalogue.ReverseTargetCents * Math.Max(100, priceLevel) / 1000L;
            for (int i = 0; i < ExtrasCatalogue.ReverseRatios.Length; i++)
            {
                if ((long)Math.Max(1, lastCents) * ExtrasCatalogue.ReverseRatios[i] >= target)
                {
                    return ExtrasCatalogue.ReverseRatios[i];
                }
            }
            return ExtrasCatalogue.ReverseRatios[ExtrasCatalogue.ReverseRatios.Length - 1];
        }

        /// <summary>A ratio as the tape and the save keep it: over nothing so many new shares for one, under nothing so many old for one new.</summary>
        public static bool Known(int ratio)
        {
            return (ratio >= 2 && ratio <= 10) || (ratio <= -2 && ratio >= -ExtrasCatalogue.RatioMost);
        }

        /// <summary>Why the founder may not ask their board for this split now; the market makes the same test at its moment on every machine.</summary>
        public static SplitWhy FounderWhy(Stock s, int ratio, int priceLevel)
        {
            if (s == null || !s.Listed || s.Own == null)
            {
                return SplitWhy.NotMine;
            }
            if (s.Own.Firm)
            {
                return SplitWhy.Firm;
            }
            if (!s.Own.Board)
            {
                return SplitWhy.Board;
            }
            if (s.SplitDay > 0)
            {
                return SplitWhy.Pending;
            }
            if (s.BidCents > 0)
            {
                return SplitWhy.Bid;
            }
            bool forward = Array.IndexOf(ExtrasCatalogue.FounderForward, ratio) >= 0, reverse = ratio < 0 && Array.IndexOf(ExtrasCatalogue.FounderReverse, -ratio) >= 0;
            if (!forward && !reverse)
            {
                return SplitWhy.Ratio;
            }
            long level = Math.Max(100, priceLevel);
            if (forward && (long)s.Last * 1000L < (long)ExtrasCatalogue.FounderSplitLeastCents * level * ratio)
            {
                return SplitWhy.TooLow;
            }
            if (reverse && (long)s.Last * -ratio > ExtrasCatalogue.FounderReverseMostCents)
            {
                return SplitWhy.TooHigh;
            }
            return SplitWhy.None;
        }

        /// <summary>
        /// True while a split must wait a morning: a dividend is declared and has not left the price, and a dividend a share
        /// in whole cents cannot be divided without somebody gaining or losing by the rounding. It waits for the record day.
        /// </summary>
        public static bool Waits(Stock s)
        {
            return s.DeclaredCents > 0 && !s.ExDone && s.RecordDay > 0;
        }

        /// <summary>The player's drawn lines and price alerts on a company follow its split: every price by new over old.</summary>
        public static void Charts(ChartBook book, int stockId, int fresh, int old)
        {
            if (book == null || fresh == old || fresh < 1 || old < 1)
            {
                return;
            }
            bool changed = false;
            for (int i = 0; i < book.Drawings.Count; i++)
            {
                ChartDrawing d = book.Drawings[i];
                if (d.StockId == stockId)
                {
                    d.FromCents = Scaled(d.FromCents, old, fresh);
                    d.ToCents = Scaled(d.ToCents, old, fresh);
                    book.Drawings[i] = d;
                    changed = true;
                }
            }
            for (int i = 0; i < book.Alerts.Count; i++)
            {
                PriceAlert a = book.Alerts[i];
                if (a.StockId == stockId)
                {
                    a.Cents = Scaled(a.Cents, old, fresh);
                    book.Alerts[i] = a;
                    changed = true;
                }
            }
            if (changed)
            {
                book.Touch();
            }
        }

        // A price by a over b, to the nearest cent and at least one; nothing stays nothing.
        private static int Scaled(int cents, int a, int b)
        {
            if (cents <= 0)
            {
                return cents;
            }
            double v = Math.Round(cents * (double)a / b, MidpointRounding.AwayFromZero);
            return v >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)v);
        }

        private static int Times(int cents, int n)
        {
            long v = (long)cents * n;
            return v > int.MaxValue ? int.MaxValue : v < int.MinValue ? int.MinValue : (int)v;
        }

        /// <summary>
        /// The rules written on a company (the player's and the rivals') follow its split: the shares each counts as its own and trades at a time, and a
        /// grid's price level. A rule's shares are the player's holding's, which the split has changed already; this is the rule's own count of them.
        /// </summary>
        public static void Rules(MarketState state, int stockId, int fresh, int old)
        {
            for (int i = 0; i < state.Rules.Count; i++)
            {
                RuleBot r = state.Rules[i];
                if (r.StockId != stockId)
                {
                    continue;
                }
                r.Shares = r.Shares * fresh / old;
                r.PerTrade = r.PerTrade > 0 ? Math.Max(1L, r.PerTrade * fresh / old) : 0L;
                if (r.Kind == RuleKind.Grid)
                {
                    r.Level = Scaled(r.Level, old, fresh);
                }
            }
        }

        /// <summary>The rivals' blocks of a company follow its split.</summary>
        public static void Rivals(ExtrasState x, int stockId, int fresh, int old)
        {
            for (int t = 0; t < x.Rivals.Length; t++)
            {
                List<RivalStake> stakes = x.Rivals[t].Stakes;
                for (int i = stakes.Count - 1; i >= 0; i--)
                {
                    if (stakes[i].Stock != stockId)
                    {
                        continue;
                    }
                    RivalStake k = stakes[i];
                    k.Shares = k.Shares * fresh / old;
                    if (k.Shares <= 0)
                    {
                        stakes.RemoveAt(i);
                    }
                    else
                    {
                        stakes[i] = k;
                    }
                }
            }
        }

        // Whole money out of cents owed to the player, the cents under a unit kept for the next time; cents owed by them are met from what is kept first and rounded up.
        private static long Whole(ExtrasState x, long cents)
        {
            if (cents >= 0)
            {
                long all = x.OddCents + cents;
                x.OddCents = all % 100L;
                return all / 100L;
            }
            long owed = -cents - x.OddCents;
            if (owed <= 0)
            {
                x.OddCents = -owed;
                return 0L;
            }
            long money = (owed + 99L) / 100L;
            x.OddCents = money * 100L - owed;
            return -money;
        }

        /// <summary>
        /// A consolidation takes effect: <paramref name="n"/> old shares are one new at n times the price. Every count is divided and
        /// every price multiplied in this one moment; a part of a share is paid for at the price before (<paramref name="result"/>,
        /// for the engine to book). An open order's price is exact; one left with no whole share is cancelled by the engine.
        /// <paramref name="mine"/>: the company is this machine's player's own, so the founder's part of a share is theirs to be paid.
        /// </summary>
        public static void Reverse(MarketState state, Stock s, int n, bool mine, out ReverseResult result)
        {
            result = default(ReverseResult);
            if (n <= 1)
            {
                return;
            }
            ExtrasState x = state.Extras;
            double before = s.Mid * 100.0;   // the price a part of a share is paid at, in cents and not yet rounded: the sum is rounded once
            double ln = Math.Log(n);
            s.Shares = Math.Max(1L, s.Shares / n);
            s.LogFair += ln;
            s.Adv /= n;
            s.Impact /= n;
            s.PrevClose = Times(s.PrevClose, n);
            s.Open = Times(s.Open, n);
            s.High = Times(s.High, n);
            s.Low = Times(s.Low, n);
            s.Last = s.MidCents;
            s.DayVolume /= n;
            s.DpsCents = Times(s.DpsCents, n);
            s.DeclaredCents = Times(s.DeclaredCents, n);
            if (s.BidCents > 0)
            {
                s.BidCents = Times(s.BidCents, n);
            }
            if (s.Own != null)
            {
                OwnListing own = s.Own;
                long part = own.Founder % n;
                own.Founder /= n;
                if (part > 0 && !own.Test && mine)   // another player's company: their own machine pays them
                {
                    result.FounderCash = Whole(x, PartCents(part, before));
                }
                own.Rps *= n;
                own.ProposedCents = Times(own.ProposedCents, n);
                own.ListedCents = Times(own.ListedCents, n);
                own.YearCents = Times(own.YearCents, n);
                own.YearDividendCents = Times(own.YearDividendCents, n);
                own.Float /= n;
                own.VestBase /= n;
                for (int i = own.Deals.Count - 1; i >= 0; i--)
                {
                    OwnDeal d = own.Deals[i];
                    d.Shares /= n;   // towards nothing: a deal of a part of a share is matched no more
                    d.Cents = Times(d.Cents, n);
                    if (d.Shares == 0)
                    {
                        own.Deals.RemoveAt(i);
                    }
                    else
                    {
                        own.Deals[i] = d;
                    }
                }
                s.Shares = Math.Max(s.Shares, own.Founder + Math.Max(0L, own.Float));
            }
            for (int i = 0; i < s.BarCount; i++)
            {
                s.Bars[i] = Up(s.Bars[i], n);
            }
            for (int i = 0; i < s.LastBarCount; i++)
            {
                s.LastBars[i] = Up(s.LastBars[i], n);
            }
            for (int i = 0; i < s.Daily.Count; i++)
            {
                s.Daily[i] = Up(s.Daily[i], n);
            }
            for (int i = 0; i < s.Monthly.Count; i++)
            {
                s.Monthly[i] = Up(s.Monthly[i], n);
            }
            for (int i = 0; i < s.Results.Count; i++)
            {
                QuarterResult r = s.Results[i];
                r.Eps = Times(r.Eps, n);
                r.Consensus = Times(r.Consensus, n);
                r.DpsCents = Times(r.DpsCents, n);
                s.Results[i] = r;
            }
            Position p = state.Position(s.Id);
            if (p != null && p.Shares != 0)
            {
                long held = Math.Abs(p.Shares), part = held % n, whole = held / n;
                bool isLong = p.Shares > 0;
                if (part > 0)
                {
                    long costPart = p.Cost * part / held;   // towards nothing, as a sale's
                    long money = Whole(x, isLong ? PartCents(part, before) : -PartCents(part, before));
                    result.Cash = money;
                    long gain = isLong ? money - costPart : costPart + money;
                    p.Realised += gain;
                    state.Account.Realised += gain;
                    p.Cost -= costPart;
                }
                p.Shares = isLong ? whole : -whole;
                if (whole == 0)
                {
                    state.Positions.Remove(p);
                }
            }
            for (int i = 0; i < state.Orders.Count; i++)
            {
                Order o = state.Orders[i];
                if (o.StockId != s.Id || !o.IsOpen)
                {
                    continue;
                }
                o.Qty /= n;
                o.Filled = Math.Min(o.Qty, o.Filled / n);
                if (o.LimitCents > 0)
                {
                    o.LimitCents = Times(o.LimitCents, n);
                }
                if (o.StopCents > 0)
                {
                    o.StopCents = Times(o.StopCents, n);
                }
                if (o.Left <= 0)
                {
                    result.Cancelled++;   // the engine cancels it and settles a worked order's money
                }
            }
            for (int b = 0; b < state.Bots.Count; b++)
            {
                Bot bot = state.Bots[b];
                BotHolding h = bot.Holding(s.Id);
                if (h != null && h.Shares > 0)
                {
                    long part = h.Shares % n;
                    if (part > 0)
                    {
                        long costPart = h.Cost * part / h.Shares;
                        long money = Whole(x, PartCents(part, before));
                        bot.Cash += money;
                        bot.Realised += money - costPart;
                        h.Gain += money - costPart;
                        h.Cost -= costPart;
                        result.BotCash += money;
                    }
                    h.Shares /= n;
                    h.StopCents = h.StopCents > 0 ? Times(h.StopCents, n) : 0;
                    if (h.Shares == 0)
                    {
                        bot.Holdings.Remove(h);
                    }
                }
                for (int j = bot.Pending.Count - 1; j >= 0; j--)
                {
                    BotOrder o = bot.Pending[j];
                    if (o.StockId != s.Id)
                    {
                        continue;
                    }
                    o.Qty /= n;
                    if (o.Qty <= 0)
                    {
                        bot.Pending.RemoveAt(j);
                    }
                    else
                    {
                        bot.Pending[j] = o;
                    }
                }
            }
            for (int i = 0; i < state.Derivs.Contracts.Count; i++)
            {
                DerivContract c = state.Derivs.Contracts[i];
                if (c.StockId != s.Id || !c.IsOpen)
                {
                    continue;
                }
                // Lots are round hundreds, which every ratio divides; one that is not is rounded down, never worth more than before.
                c.Qty = Math.Max(1L, c.Qty / n);
                c.Price *= n;
                c.Premium *= n;
            }
            Charts(state.Charts, s.Id, 1, n);
            Rules(state, s.Id, 1, n);
            Rivals(x, s.Id, 1, n);
            x.NoteSplit(s.Id, state.Day, 1, n);
            s.SplitDay = 0;
            s.SplitRatio = 0;
        }

        /// <summary>What a split the market's own rule made (so many new shares for one) still has to change outside it: the player's chart, the rivals' blocks, the note.</summary>
        public static void AfterForward(MarketState state, Stock s, int ratio)
        {
            if (ratio <= 1)
            {
                return;
            }
            Charts(state.Charts, s.Id, ratio, 1);
            Rules(state, s.Id, ratio, 1);
            Rivals(state.Extras, s.Id, ratio, 1);
            state.Extras.NoteSplit(s.Id, state.Day, ratio, 1);
        }

        private static long PartCents(long part, double cents)
        {
            return Math.Max(0L, (long)Math.Round(part * cents, MidpointRounding.AwayFromZero));
        }

        private static Candle Up(in Candle c, int n)
        {
            return c.Empty ? c : new Candle(c.Day, Times(c.O, n), Times(c.H, n), Times(c.L, n), Times(c.C, n), c.V / n);
        }
    }
}
