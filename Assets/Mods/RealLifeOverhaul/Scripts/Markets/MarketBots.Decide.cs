using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the bots' rules: how a bot decides at a close what to trade at the next session.</summary>
    public static partial class MarketBots
    {
        /// <summary>Scratch the decisions work in, kept by the market so nothing is allocated at a close.</summary>
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

        /// <summary>A company's score for a strategy at today's close: above nothing to be held, larger better; NaN when the rule says no.</summary>
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
                        // Eps is in hundredths of a cent, the dividend in cents.
                        return s.DpsCents * 4L * 100L <= eps ? s.DpsCents * 4.0 / s.Last : double.NaN;
                    }
            }
        }

        /// <summary>Whether a holding is sold by its strategy's rule at today's close; stops and limits are the session's.</summary>
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
                    return false;   // momentum and income turn over at the month's turn (Decide)
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

        /// <summary>True on the first market day of a month, when the monthly strategies turn their holdings over.</summary>
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
        /// What a bot will do at the next session's first step, decided at a close or a hire while shut: holdings to sell by its
        /// rule, then the best companies into the room it has, each for its size of its worth. After capital is added it first
        /// buys more of what it keeps, once. Prices are reckoned now; the step's price decides.
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
            // Sales: out of its scope, by the rule, or at a month's turn out of the best twice over.
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
            bool topUp = b.TopUp != 0;
            if (!rebalance && !topUp)
            {
                return;
            }
            long money = Math.Max(0L, b.Cash) + freed * 99 / 100;
            long target = worth * b.Size / 100;
            long tradeLeast = Math.Max(1L, (long)least * TradeLeastAtStart / Math.Max(1, MarketCatalogue.CommissionLeastAtStart));
            if (topUp)
            {
                money = TopUpHeld(state, b, day, year, least, target, tradeLeast, money);
                b.TopUp = b.TopUp >= TopUpAgain ? TopUpDecidedAgain : TopUpDecided;
            }
            if (!rebalance)
            {
                return;   // a month's strategy buys new holdings at the month's turn; till then capital added goes into what it holds
            }
            // Purchases into the room left.
            for (int i = 0; i < w.Ids.Count && kept < slots; i++)
            {
                if (b.Holding(w.Ids[i]) != null)
                {
                    continue;
                }
                Stock s = state.Stock(w.Ids[i]);
                Quote q = MarketTrading.QuoteOf(state, s, MarketCatalogue.PhaseOpen + 1);
                long spend = Math.Min(target, money);
                // A little is held back for the commission and the price's move.
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

        // After capital is added: each holding it keeps bought up to its size of the larger worth, out of its money; returns
        // the money left.
        private static long TopUpHeld(MarketState state, Bot b, int day, int year, int least, long target, long tradeLeast, long money)
        {
            int count = b.Holdings.Count;
            for (int i = 0; i < count && money > 0; i++)
            {
                BotHolding h = b.Holdings[i];
                Stock s = state.Stock(h.StockId);
                if (s == null || Selling(b, h.StockId) || !Allowed(state, b, s, day))
                {
                    continue;
                }
                long gap = target - MarketTrading.Value(h.Shares, MarketTrading.QuoteOf(state, s, MarketCatalogue.PhaseClose).Bid, false);
                if (gap <= 0)
                {
                    continue;
                }
                Quote q = MarketTrading.QuoteOf(state, s, MarketCatalogue.PhaseOpen + 1);
                long qty = Math.Min(Math.Min(Room(state, s), OwnRoom(state, s, true, MarketTrading.NextTradingDay(day), year)), (long)(Math.Min(gap, money) * 100.0 / (q.Ask * 1.01) - 0.5));
                long value = MarketTrading.Value(Math.Max(0, qty), q.Ask, true);
                if (qty <= 0 || value < tradeLeast)
                {
                    continue;
                }
                b.Pending.Add(new BotOrder { StockId = s.Id, Buy = true, Qty = qty });
                money -= value + MarketTrading.Commission(value, least);
            }
            return money;
        }

        // True when a bot has decided to sell a company.
        private static bool Selling(Bot b, int stockId)
        {
            for (int i = 0; i < b.Pending.Count; i++)
            {
                if (!b.Pending[i].Buy && b.Pending[i].StockId == stockId)
                {
                    return true;
                }
            }
            return false;
        }

        // Where a company stands among today's best; int.MaxValue when not among them.
        private static int Rank(Work w, int stockId)
        {
            int at = w.Ids.IndexOf(stockId);
            return at < 0 ? int.MaxValue : at;
        }
    }
}
