using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What a candle of a chart stands for.</summary>
    public enum Timeframe { Intraday = 0, Day = 1, Week = 2, Month = 3, Year = 4, Count = 5 }

    /// <summary>
    /// A company's or an index's price history, kept small: a candle a step for today and the session before, a day for the
    /// last <see cref="MarketCatalogue.DailyKept"/> market days, and a month before that.
    /// </summary>
    public static class MarketHistory
    {
        /// <summary>A move from one price or level to another in bp; 0 with none before. Every day's move the app shows is this.</summary>
        public static int MoveBp(long now, long before)
        {
            return before > 0 ? (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (now - before) * 10000L / before)) : 0;
        }

        public static int DayMoveBp(Stock s)
        {
            return MoveBp(s.Last, s.PrevClose);
        }

        public static int IndexDayMoveBp(MarketState state)
        {
            return MoveBp(state.IndexLast, state.IndexPrevClose);
        }

        public static int SectorDayMoveBp(MarketState state, int sector)
        {
            return MoveBp(state.SectorLast[sector], state.SectorPrevClose[sector]);
        }

        /// <summary>
        /// The close a day's move is measured from: in a session yesterday's, between sessions the one before; with too short a
        /// history, <paramref name="was"/>, the listing price.
        /// </summary>
        public static int DayBase(List<Candle> daily, bool session, int was)
        {
            int at = daily.Count - (session ? 1 : 2);
            return at >= 0 && daily[at].C > 0 ? daily[at].C : was;
        }

        /// <summary>Out of a sector's daily closes.</summary>
        public static int DayBase(List<int> closes, bool session, int was)
        {
            int at = closes.Count - (session ? 1 : 2);
            return at >= 0 && closes[at] > 0 ? closes[at] : was;
        }

        /// <summary>A finished day's candle goes on; the oldest beyond the daily window is folded into its month.</summary>
        public static void AddDay(List<Candle> daily, List<Candle> monthly, in Candle day, int year)
        {
            if (day.Empty)
            {
                return;
            }
            daily.Add(day);
            while (daily.Count > MarketCatalogue.DailyKept)
            {
                Fold(monthly, daily[0], year);
                daily.RemoveAt(0);
            }
            while (monthly.Count > MarketCatalogue.MonthlyKept)
            {
                monthly.RemoveAt(0);
            }
        }

        /// <summary>Into the last month's candle if it is the same month, else a new one.</summary>
        public static void Fold(List<Candle> monthly, in Candle day, int year)
        {
            int month = MarketCatalogue.MonthOf(day.Day, year);
            int last = monthly.Count - 1;
            if (last >= 0 && MarketCatalogue.MonthOf(monthly[last].Day, year) == month)
            {
                monthly[last] = monthly[last].Then(day);
                return;
            }
            Candle start = day;
            start.Day = MonthStart(month, year);
            monthly.Add(start);
        }

        /// <summary>The first game day of a month as <see cref="MarketCatalogue.MonthOf"/> counts them.</summary>
        public static int MonthStart(int month, int year)
        {
            year = Math.Max(8, year);
            int y = MarketCatalogue.FloorDiv(month, 12), m = month - y * 12;
            return y * year + (m * year + 11) / 12 + 1;
        }

        public static int YearStart(int y, int year)
        {
            return y * Math.Max(8, year) + 1;
        }

        public static int PeriodOf(int day, Timeframe frame, int year)
        {
            switch (frame)
            {
                case Timeframe.Week: return MarketCatalogue.WeekOf(day);
                case Timeframe.Month: return MarketCatalogue.MonthOf(day, year);
                case Timeframe.Year: return MarketCatalogue.YearOf(day, year);
                default: return day;
            }
        }

        private static int PeriodStart(int period, Timeframe frame, int year)
        {
            switch (frame)
            {
                case Timeframe.Week: return period * 7 + 1;
                case Timeframe.Month: return MonthStart(period, year);
                case Timeframe.Year: return YearStart(period, year);
                default: return period;
            }
        }

        /// <summary>The candles of a timeframe, oldest first, today's last; a week only from days. Nothing allocated if the list is large enough.</summary>
        public static void Build(List<Candle> daily, List<Candle> monthly, in Candle today, Timeframe frame, int year, List<Candle> into)
        {
            into.Clear();
            if (frame == Timeframe.Day || frame == Timeframe.Intraday)
            {
                for (int i = 0; i < daily.Count; i++)
                {
                    into.Add(daily[i]);
                }
                if (!today.Empty)
                {
                    into.Add(today);
                }
                return;
            }
            if (frame != Timeframe.Week)
            {
                for (int i = 0; i < monthly.Count; i++)
                {
                    Merge(into, monthly[i], frame, year);
                }
            }
            for (int i = 0; i < daily.Count; i++)
            {
                Merge(into, daily[i], frame, year);
            }
            if (!today.Empty)
            {
                Merge(into, today, frame, year);
            }
        }

        private static void Merge(List<Candle> into, in Candle c, Timeframe frame, int year)
        {
            int period = PeriodOf(c.Day, frame, year);
            int last = into.Count - 1;
            if (last >= 0 && PeriodOf(into[last].Day, frame, year) == period)
            {
                into[last] = into[last].Then(c);
                return;
            }
            Candle start = c;
            start.Day = PeriodStart(period, frame, year);
            into.Add(start);
        }

        /// <summary>Steps folded into bars of so many steps each (2: ten-minute bars), oldest first.</summary>
        public static void Bars(Candle[] bars, int count, int perBar, List<Candle> into)
        {
            into.Clear();
            perBar = Math.Max(1, perBar);
            for (int i = 0; i < count; i++)
            {
                if (i % perBar == 0 || into.Count == 0)
                {
                    into.Add(bars[i]);
                }
                else
                {
                    into[into.Count - 1] = into[into.Count - 1].Then(bars[i]);
                }
            }
        }

        public static void Range(List<Candle> candles, int from, int count, out int low, out int high)
        {
            low = int.MaxValue;
            high = 0;
            for (int i = Math.Max(0, from); i < from + count && i < candles.Count; i++)
            {
                if (candles[i].Empty)
                {
                    continue;
                }
                low = Math.Min(low, candles[i].L);
                high = Math.Max(high, candles[i].H);
            }
            if (high == 0)
            {
                low = 0;
            }
        }

        /// <summary>Every price divided by the ratio, to the nearest cent and at least one; the volume multiplied.</summary>
        public static void Split(List<Candle> candles, int ratio)
        {
            for (int i = 0; i < candles.Count; i++)
            {
                candles[i] = Split(candles[i], ratio);
            }
        }

        public static Candle Split(in Candle c, int ratio)
        {
            if (c.Empty || ratio <= 1)
            {
                return c;
            }
            return new Candle(c.Day, Div(c.O, ratio), Div(c.H, ratio), Div(c.L, ratio), Div(c.C, ratio), c.V * ratio);
        }

        private static int Div(int cents, int ratio)
        {
            return Math.Max(1, (int)Math.Round(cents / (double)ratio, MidpointRounding.AwayFromZero));
        }
    }
}
