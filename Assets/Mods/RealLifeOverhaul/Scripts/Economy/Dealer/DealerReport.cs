using System;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>What a report shows of a day.</summary>
    public enum ReportKind { Sold = 0, Revenue = 1, Profit = 2, StockAge = 3, Visitors = 4, Conversion = 5, Arrears = 6, Count = 7 }

    /// <summary>The showrooms' reports, game-free: a day's figures, a chart's series and average, and sums; today as it stands.</summary>
    public static class DealerReport
    {
        /// <summary>A day's figures of a showroom, or of all of them for a null place, into the book given. False if none has figures of that day.</summary>
        public static bool Day(DealerState s, string place, int day, int today, DealerBook into)
        {
            into.Clear(day);
            bool any = false;
            for (int i = 0; i < s.Yards.Count; i++)
            {
                DealerYard y = s.Yards[i];
                if (place != null && y.Place != place)
                {
                    continue;
                }
                if (day == today && y.Today.Day == today)
                {
                    into.Add(y.Today);
                    into.Cars += y.Cars.Count - y.Today.Cars;
                    TodayNow(s, y, today, into);
                    any = true;
                    continue;
                }
                for (int b = 0; b < y.Past.Length; b++)
                {
                    if (y.Past[b].Day == day)
                    {
                        into.Add(y.Past[b]);
                        any = true;
                        break;
                    }
                }
            }
            return any;
        }

        // Today's read-offs as the showroom stands now, in place of its open book's.
        private static void TodayNow(DealerState s, DealerYard y, int today, DealerBook into)
        {
            int age = 0;
            for (int c = 0; c < y.Cars.Count; c++)
            {
                age += Math.Max(0, today - y.Cars[c].Bought);
            }
            into.Age += age - y.Today.Age;
            int arrears = 0, late = 0;
            for (int i = 0; i < s.Plans.Count; i++)
            {
                DealerPlan p = s.Plans[i];
                if (p.Place == y.Place && p.Missed > 0)
                {
                    arrears = (int)Math.Min(int.MaxValue, (long)arrears + p.Owed);
                    late++;
                }
            }
            into.Arrears += arrears - y.Today.Arrears;
            into.Late += late - y.Today.Late;
            into.Rep += y.Rep - y.Today.Rep;
        }

        /// <summary>What a report shows of a day's figures; a conversion is in tenths of a per cent.</summary>
        public static long Value(DealerBook b, ReportKind kind)
        {
            switch (kind)
            {
                case ReportKind.Sold: return b.Sold;
                case ReportKind.Revenue: return b.Revenue;
                case ReportKind.Profit: return b.Margin;
                case ReportKind.StockAge: return b.Cars > 0 ? (b.Age + b.Cars / 2) / b.Cars : 0;
                case ReportKind.Visitors: return b.Visitors;
                case ReportKind.Conversion: return b.Visitors > 0 ? (b.Sold * 1000L + b.Visitors / 2) / b.Visitors : 0;
                case ReportKind.Arrears: return b.Arrears;
                default: return 0;
            }
        }

        public static bool Money(ReportKind kind)
        {
            return kind == ReportKind.Revenue || kind == ReportKind.Profit || kind == ReportKind.Arrears;
        }

        /// <summary>A chart's series of <paramref name="days"/> days up to today, oldest first, with whether each has figures. Returns the first day.</summary>
        public static int Series(DealerState s, string place, ReportKind kind, int today, int days, long[] values, bool[] have, DealerBook scratch)
        {
            int n = Math.Min(days, Math.Min(values.Length, have.Length));
            int first = today - n + 1;
            for (int i = 0; i < n; i++)
            {
                have[i] = Day(s, place, first + i, today, scratch);
                values[i] = have[i] ? Value(scratch, kind) : 0;
            }
            return first;
        }

        /// <summary>The average of the last <see cref="DealerCatalogue.AverageDays"/> days that have figures, for each day; 0 before any.</summary>
        public static void Average(long[] values, bool[] have, int n, long[] into)
        {
            for (int i = 0; i < n; i++)
            {
                long sum = 0;
                int count = 0;
                for (int k = Math.Max(0, i - DealerCatalogue.AverageDays + 1); k <= i; k++)
                {
                    if (have[k])
                    {
                        sum += values[k];
                        count++;
                    }
                }
                into[i] = count > 0 ? (sum + count / 2) / count : 0;
            }
        }

        /// <summary>The figures of the days from and to (both in) of a showroom or of all, summed. Returns the days that had figures.</summary>
        public static int Sum(DealerState s, string place, int from, int to, int today, DealerBook into, DealerBook scratch)
        {
            into.Clear(from);
            int days = 0;
            for (int d = from; d <= to; d++)
            {
                if (Day(s, place, d, today, scratch))
                {
                    into.Add(scratch);
                    days++;
                }
            }
            return days;
        }
    }
}
