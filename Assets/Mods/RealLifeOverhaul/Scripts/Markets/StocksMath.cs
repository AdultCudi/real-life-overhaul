using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>How the market table is ordered. Not saved.</summary>
    public enum MarketSort : byte
    {
        Movers = 0,
        Gainers = 1,
        Losers = 2,
        Active = 3
    }

    /// <summary>
    /// Figures the desk reads off the market it already has: a price against earnings and the dividend, the day's
    /// breadth, a holding marked to the candles, a hired bot's chip. No new store. Game-free.
    /// </summary>
    public static class StocksMath
    {
        /// <summary>Price over a year's earnings, times 100, or 0 when the company did not earn. Eps is a quarter, in hundredths of a cent.</summary>
        public static int PeTimes100(int priceCents, int epsHundredths)
        {
            long annual = (long)epsHundredths * 4 / 100;
            if (annual <= 0 || priceCents <= 0)
            {
                return 0;
            }
            return (int)(priceCents * 100L / annual);
        }

        /// <summary>The dividend yield in basis points, four quarters of the last dividend against the price.</summary>
        public static int YieldBp(int priceCents, int dpsCents)
        {
            if (priceCents <= 0 || dpsCents <= 0)
            {
                return 0;
            }
            return (int)((long)dpsCents * 4 * 10000L / priceCents);
        }

        /// <summary>The day's move in basis points, from the previous close.</summary>
        public static int ChangeBp(int prev, int last)
        {
            if (prev <= 0)
            {
                return 0;
            }
            return (int)((last - prev) * 10000L / prev);
        }

        /// <summary>Running, waiting, paused, out of budget, stopped, selling up. The last three of the code are derived.</summary>
        public static int BotChip(Bot bot)
        {
            if (bot == null)
            {
                return 4;
            }
            if (bot.Status == BotStatus.Stopped)
            {
                return 4;
            }
            if (bot.Status == BotStatus.SellingUp)
            {
                return 5;
            }
            if (bot.Cash <= 0)
            {
                return 3;
            }
            if (bot.Loss > 0 && bot.DrawdownBp >= bot.Loss * 100)
            {
                return 2;   // paused at the loss it was given
            }
            if (bot.Pending.Count == 0)
            {
                return 1;
            }
            return 0;
        }

        public static void Today(MarketState state, out int up, out int down, out long turnover)
        {
            up = 0;
            down = 0;
            turnover = 0;
            if (state == null)
            {
                return;
            }
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                Stock s = state.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                int move = s.Last - s.PrevClose;
                if (move > 0)
                {
                    up++;
                }
                else if (move < 0)
                {
                    down++;
                }
                turnover += s.DayVolume;
            }
        }

        /// <summary>The market value of one size, or of every listed company when <paramref name="size"/> is under 0. The capped index stays MarketIndex.Total.</summary>
        public static double ValueOf(List<Stock> stocks, int size)
        {
            double sum = 0.0;
            for (int i = 0; stocks != null && i < stocks.Count; i++)
            {
                Stock s = stocks[i];
                if (!s.Listed || (size >= 0 && (int)s.Size != size))
                {
                    continue;
                }
                sum += MarketIndex.Value(s);
            }
            return sum;
        }

        /// <summary>Company indexes into <paramref name="into"/>, the order the desk asked for, a sector and a size left out when under 0.</summary>
        public static int Order(List<Stock> stocks, MarketSort sort, int sector, int size, int[] into)
        {
            int n = 0;
            if (stocks == null || into == null)
            {
                return 0;
            }
            for (int i = 0; i < stocks.Count && n < into.Length; i++)
            {
                Stock s = stocks[i];
                if (!s.Listed || (sector >= 0 && (int)s.Sector != sector) || (size >= 0 && (int)s.Size != size))
                {
                    continue;
                }
                int at = n;
                while (at > 0 && Before(stocks[into[at - 1]], s, sort))
                {
                    into[at] = into[at - 1];
                    at--;
                }
                into[at] = i;
                n++;
            }
            return n;
        }

        // True when the one already placed should stand after the newcomer.
        private static bool Before(Stock placed, Stock next, MarketSort sort)
        {
            int pa = ChangeBp(placed.PrevClose, placed.Last);
            int na = ChangeBp(next.PrevClose, next.Last);
            switch (sort)
            {
                case MarketSort.Gainers: return pa < na;
                case MarketSort.Losers: return pa > na;
                case MarketSort.Active: return placed.DayVolume < next.DayVolume;
                default: return Math.Abs(pa) < Math.Abs(na);
            }
        }

        /// <summary>The last few closes, oldest first, for a spark. Returns how many.</summary>
        public static int Spark(List<Candle> candles, int count, float[] into)
        {
            if (candles == null || into == null || count <= 0)
            {
                return 0;
            }
            int n = Math.Min(count, Math.Min(candles.Count, into.Length));
            int from = candles.Count - n;
            for (int i = 0; i < n; i++)
            {
                into[i] = candles[from + i].C;
            }
            return n;
        }

        /// <summary>
        /// A holding marked to each candle: start from the shares, walk the candles, and apply a trade on its day.
        /// <paramref name="trades"/> are day, signed shares, unused price. Nothing is saved.
        /// </summary>
        public static int Mark(int[] days, int[] closes, int candles, int[] tradeDay, long[] tradeShares, int trades, long shares, long[] into)
        {
            if (days == null || closes == null || into == null)
            {
                return 0;
            }
            int n = Math.Min(candles, Math.Min(days.Length, Math.Min(closes.Length, into.Length)));
            long held = shares;
            for (int i = 0; i < n; i++)
            {
                for (int t = 0; t < trades; t++)
                {
                    if (tradeDay[t] == days[i])
                    {
                        held += tradeShares[t];
                    }
                }
                into[i] = held * closes[i] / 100L;
            }
            return n;
        }
    }
}
