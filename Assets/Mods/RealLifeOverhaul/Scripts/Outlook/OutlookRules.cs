using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// What the Economy app works out of the readings, game-free (EconomyCheck's OutlookChecks): a figure's change over some days
    /// and how it reads, the one-word verdict on the economy, and the districts in the order of their pull on customers. All
    /// whole numbers, the same on every machine for the same readings; a few comparisons, run when the app is written.
    /// </summary>
    public static class OutlookRules
    {
        // ------------------------------------------------------------------ changes

        /// <summary>
        /// A figure's change over <paramref name="days"/>: from its reading that many days before the newest of the window
        /// (or the one day before or after that, since a day may have none) to <paramref name="now"/>. A per cent in basis
        /// points, or the figure's own points. Unknown without a reading to compare with, or from nothing.
        /// </summary>
        public static OutlookDelta Over(int[] window, int count, int days, int now, OutlookChange kind)
        {
            if (window == null || count <= 1 || count > window.Length || days <= 0 || now == OutlookCatalogue.NoValue)
            {
                return OutlookDelta.None;
            }
            int at = count - 1 - days;
            for (int k = 0; k < 3; k++)
            {
                int i = at + (k == 0 ? 0 : k == 1 ? -1 : 1);
                if (i >= 0 && i < count - 1 && window[i] != OutlookCatalogue.NoValue)
                {
                    return Between(window[i], now, kind);
                }
            }
            return OutlookDelta.None;
        }

        /// <summary>The change from one reading to another.</summary>
        public static OutlookDelta Between(int then, int now, OutlookChange kind)
        {
            if (then == OutlookCatalogue.NoValue || now == OutlookCatalogue.NoValue)
            {
                return OutlookDelta.None;
            }
            if (kind == OutlookChange.Points)
            {
                return OutlookDelta.Of((int)Math.Max(int.MinValue + 1L, Math.Min(int.MaxValue, (long)now - then)));
            }
            if (then == 0)
            {
                return OutlookDelta.None;
            }
            long bp = ((long)now - then) * 10000L / Math.Abs((long)then);
            return OutlookDelta.Of((int)Math.Max(-1000000L, Math.Min(1000000L, bp)));
        }

        /// <summary>How a change reads: nothing moved (flat), good or bad news for the player, or neither (<paramref name="good"/> is 0).</summary>
        public static OutlookTone OutcomeOf(OutlookDelta change, int good)
        {
            if (!change.Known || change.Value == 0)
            {
                return OutlookTone.Flat;
            }
            if (good == 0)
            {
                return OutlookTone.Neutral;
            }
            return (change.Value > 0) == (good > 0) ? OutlookTone.Good : OutlookTone.Bad;
        }

        /// <summary>
        /// Fills each tile's short and long change and each district's, from the book's history and today's readings. The stocks'
        /// tile is the market's own history, set by whoever reads the market; it is left as it is here.
        /// </summary>
        public static void Trends(OutlookBook book, OutlookReadings r, int[] scratch)
        {
            if (book == null || r == null || scratch == null || scratch.Length < OutlookCatalogue.HistoryDays)
            {
                return;
            }
            int day = r.Day;
            int count = OutlookCatalogue.HistoryDays;
            for (int t = 0; t < OutlookCatalogue.Tiles.Length; t++)
            {
                OutlookTile tile = OutlookCatalogue.Tiles[t];
                if (tile.Series == null)
                {
                    continue;
                }
                book.Window(tile.Series, day, count, scratch);
                int now = Now(r, tile.Series);
                r.Short[t] = Over(scratch, count, OutlookCatalogue.ShortDays, now, tile.Kind);
                r.Long[t] = Over(scratch, count, OutlookCatalogue.LongDays, now, tile.Kind);
            }
            // What the model remembers of itself stands in for the first month of a book: prices rose by the month's change, a
            // district's income was a month ago what it kept.
            if (!r.Long[OutlookCatalogue.TilePrices].Known && r.CityOn)
            {
                r.Long[OutlookCatalogue.TilePrices] = OutlookDelta.Of(r.MonthChange);   // hundredths of a per cent are basis points
            }
            for (int i = 0; i < r.Districts.Count; i++)
            {
                OutlookDistrict d = r.Districts[i];
                book.Window(OutlookCatalogue.OfDistrictIncome(d.Id), day, count, scratch);
                d.IncomeLong = Over(scratch, count, OutlookCatalogue.LongDays, d.Income, OutlookChange.Points);
                if (!d.IncomeLong.Known && d.Before != d.Income)
                {
                    d.IncomeLong = OutlookDelta.Of(d.Income - d.Before);
                }
                d.WorthLong = WorthChange(book, r, d, day, OutlookCatalogue.LongDays);
            }
        }

        // A tile's figure today, in the units its history is kept in.
        private static int Now(OutlookReadings r, string series)
        {
            switch (series)
            {
                case OutlookCatalogue.Prices: return r.Prices;
                case OutlookCatalogue.Inflation: return r.Inflation;
                case OutlookCatalogue.Rate: return r.Rate;
                case OutlookCatalogue.Unemployment: return r.JobsOn ? r.Unemployment : OutlookCatalogue.NoValue;
                case OutlookCatalogue.Income: return r.Income;
                case OutlookCatalogue.Property: return r.Property;
                case OutlookCatalogue.Reserve: return r.CouncilOn ? ThousandsOf(r.Reserve) : OutlookCatalogue.NoValue;
                default: return OutlookCatalogue.NoValue;
            }
        }

        /// <summary>Money in thousands, held inside a whole number.</summary>
        public static int ThousandsOf(long money)
        {
            long k = money / 1000L;
            return (int)Math.Max(int.MinValue + 1L, Math.Min(int.MaxValue, k));
        }

        // ------------------------------------------------------------------ worth

        /// <summary>
        /// What a property is worth against the game's figure, in thousandths, for a state of the economy: the city's formula
        /// (<see cref="CityModel.Property(CycleState, int, int, int, int)"/>) with the district's market level on it where the mod
        /// keeps one (0: none).
        /// </summary>
        public static int Worth(int cycle, int index, int income, int start, int strength, int marketLevel)
        {
            int cycleState = Math.Max(0, Math.Min(MacroCatalogue.StateCount - 1, cycle));
            long worth = CityModel.Property((CycleState)cycleState, index, income, start, strength);
            if (marketLevel > 0)
            {
                worth = worth * marketLevel / 1000L;
            }
            return (int)Math.Max(0L, Math.Min(int.MaxValue, worth));
        }

        // A district's property over some days, from the book's cycle, prices, income and level of that day against today's.
        private static OutlookDelta WorthChange(OutlookBook book, OutlookReadings r, OutlookDistrict d, int day, int days)
        {
            int then = day - days;
            int cycle, index, income;
            if (!book.Get(OutlookCatalogue.Cycle, then, out cycle) || !book.Get(OutlookCatalogue.Prices, then, out index)
                || !book.Get(OutlookCatalogue.OfDistrictIncome(d.Id), then, out income))
            {
                return OutlookDelta.None;
            }
            int level;
            if (!book.Get(OutlookCatalogue.OfDistrictLevel(d.Id), then, out level))
            {
                level = 0;
            }
            if ((level == 0) != (d.Level == 0))
            {
                return OutlookDelta.None;   // the district had no market level then (or has none now): the two are not the same measure
            }
            int before = Worth(cycle, index, income, d.Start, r.Level, level);
            return Between(before, d.Property, OutlookChange.Percent);
        }

        // ------------------------------------------------------------------ the verdict

        /// <summary>
        /// The economy in one word. A recession and a slowdown are the city's own states; a boom or a steady economy with prices
        /// rising 1.5 points over the target is overheating; a boom is growing; a steady economy is steady, unless the districts'
        /// incomes and the shares have fallen over the long window (slowing) or risen (growing).
        /// </summary>
        public static OutlookVerdict Judge(int cycle, int inflation, OutlookDelta incomeLong, OutlookDelta stocksLong)
        {
            if (cycle >= (int)CycleState.Recession)
            {
                return OutlookVerdict.Recession;
            }
            if (cycle == (int)CycleState.Slowdown)
            {
                return OutlookVerdict.Slowing;
            }
            if (inflation >= MacroCatalogue.InflationTarget + OutlookCatalogue.HotGap)
            {
                return OutlookVerdict.Overheating;
            }
            if (cycle == (int)CycleState.Boom)
            {
                return OutlookVerdict.Growing;
            }
            bool falling = (incomeLong.Known && incomeLong.Value <= OutlookCatalogue.IncomeFalls) || (stocksLong.Known && stocksLong.Value <= OutlookCatalogue.StocksFall);
            bool rising = (incomeLong.Known && incomeLong.Value >= OutlookCatalogue.IncomeRises) || (stocksLong.Known && stocksLong.Value >= OutlookCatalogue.StocksRise);
            return falling && !rising ? OutlookVerdict.Slowing : rising && !falling ? OutlookVerdict.Growing : OutlookVerdict.Steady;
        }

        public static readonly string[] VerdictKeys =
        {
            "reallife_econapp_verdict_growing", "reallife_econapp_verdict_steady", "reallife_econapp_verdict_slowing",
            "reallife_econapp_verdict_overheating", "reallife_econapp_verdict_recession"
        };

        public static readonly string[] VerdictLineKeys =
        {
            "reallife_econapp_verdict_line_growing", "reallife_econapp_verdict_line_steady", "reallife_econapp_verdict_line_slowing",
            "reallife_econapp_verdict_line_overheating", "reallife_econapp_verdict_line_recession"
        };

        /// <summary>How a verdict reads: growing is good, steady is neutral, the others are bad news.</summary>
        public static OutlookTone OutcomeOf(OutlookVerdict verdict)
        {
            return verdict == OutlookVerdict.Growing ? OutlookTone.Good : verdict == OutlookVerdict.Steady ? OutlookTone.Neutral : OutlookTone.Bad;
        }

        // ------------------------------------------------------------------ the districts

        /// <summary>
        /// Gives each district its pull on customers, 100 the mean of the city's, and its rank: the game's weight of its
        /// customers with what its income adds or takes and the cycle's change to footfall on it. The same weights the game
        /// draws its customers by, so the ranking says where a shop would be busiest.
        /// </summary>
        public static void Rank(OutlookReadings r)
        {
            int n = r.Districts.Count;
            if (n == 0)
            {
                return;
            }
            long total = 0;
            for (int i = 0; i < n; i++)
            {
                total += Raw(r, r.Districts[i]);
            }
            long mean = Math.Max(1L, total / n);
            for (int i = 0; i < n; i++)
            {
                r.Districts[i].Pull = (int)Math.Max(1L, Math.Min(9999L, Raw(r, r.Districts[i]) * 100L / mean));
            }
            for (int i = 0; i < n; i++)
            {
                int rank = 1;
                OutlookDistrict a = r.Districts[i];
                for (int j = 0; j < n; j++)
                {
                    rank += j != i && Before(r.Districts[j], a) ? 1 : 0;
                }
                a.Rank = rank;
            }
        }

        private static long Raw(OutlookReadings r, OutlookDistrict d)
        {
            long feet = Math.Max(100L, 1000L + (r.Footfall + d.Footfall) * 10L);
            return Math.Max(1L, d.Weight) * feet;
        }

        // True if a stands before b: more pull, then more income, then the id.
        private static bool Before(OutlookDistrict a, OutlookDistrict b)
        {
            if (a.Pull != b.Pull)
            {
                return a.Pull > b.Pull;
            }
            if (a.Income != b.Income)
            {
                return a.Income > b.Income;
            }
            return string.CompareOrdinal(a.Id, b.Id) < 0;
        }

        /// <summary>The districts' indices in rank order, in <paramref name="order"/> (at least as long as the list); returns how many.</summary>
        public static int Order(OutlookReadings r, int[] order)
        {
            int n = Math.Min(r.Districts.Count, order.Length);
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }
            for (int i = 1; i < n; i++)
            {
                int held = order[i];
                int j = i - 1;
                while (j >= 0 && r.Districts[order[j]].Rank > r.Districts[held].Rank)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = held;
            }
            return n;
        }

        /// <summary>The mean of the districts' incomes, tenths; the middle where there are none.</summary>
        public static int MeanIncome(OutlookReadings r)
        {
            int n = r.Districts.Count;
            if (n == 0)
            {
                return MacroCatalogue.IncomeMiddle;
            }
            long sum = 0;
            for (int i = 0; i < n; i++)
            {
                sum += r.Districts[i].Income;
            }
            return (int)(sum / n);
        }

        /// <summary>The mean of the districts' property worth, thousandths; 1000 where there are none.</summary>
        public static int MeanWorth(OutlookReadings r)
        {
            int n = r.Districts.Count;
            if (n == 0)
            {
                return 1000;
            }
            long sum = 0;
            for (int i = 0; i < n; i++)
            {
                sum += r.Districts[i].Property;
            }
            return (int)(sum / n);
        }
    }
}
