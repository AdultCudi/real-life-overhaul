using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.DaySeries
{
    /// <summary>
    /// The day series' arithmetic, and the money the screens work out without the game: a change over a window, who borrows
    /// today, a loan's cost, whether a business can secure one, and how a pooled balance is split across named accounts.
    /// The same inputs always give the same answers, so a Going Public session agrees. Game-free.
    /// </summary>
    public static class DaySeriesRules
    {
        public const int CutPercent = 5;
        public const int CutDays = 14;
        public const int HistoryDays = 30;     // days of takings a business loan asks for
        public const int CoverTimes = 20;      // takings must cover the daily payment this many times
        public const int LoansADay = 2;
        public const int ScoreWindow = 30;
        public const int WorthWindow = 30;
        public const int WorthLong = 90;
        public const int CashWindow = 30;
        public const int CustomerWindow = 60;
        public const int DefaultSeed = 0x4D31C0;   // a new book, the same on every machine

        /// <summary>Whole dollars into the series' range. A figure past it is kept at the edge, not wrapped.</summary>
        public static int ClampDollars(long dollars)
        {
            if (dollars >= int.MaxValue)
            {
                return int.MaxValue - 1;
            }
            if (dollars <= int.MinValue + 1)
            {
                return int.MinValue + 1;
            }
            return (int)dollars;
        }

        /// <summary>Worth in the series' units (thousands of dollars), so a fortune still fits.</summary>
        public static int WorthUnits(long dollars) => ClampDollars(dollars / DaySeriesCatalogue.WorthScale);

        /// <summary>The change from <paramref name="back"/> days before <paramref name="endDay"/> to that day; false when either day is missing.</summary>
        public static bool Change(DaySeriesBook book, string key, int endDay, int back, out int change)
        {
            change = 0;
            if (book == null || back < 1 || !book.Get(key, endDay, out int now) || !book.Get(key, endDay - back, out int then))
            {
                return false;
            }
            change = now - then;
            return true;
        }

        /// <summary>Copies a window into floats for a chart, skipping days with no reading (left at the previous, or 0 at the start).</summary>
        public static int Chart(DaySeriesBook book, string key, int endDay, int count, float[] into)
        {
            if (into == null || count < 1)
            {
                return 0;
            }
            count = Math.Min(count, into.Length);
            var raw = new int[count];
            int found = book == null ? 0 : book.Window(key, endDay, count, raw);
            float last = 0f;
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                if (raw[i] != DaySeriesCatalogue.NoValue)
                {
                    last = raw[i];
                    any = true;
                }
                into[i] = any ? last : 0f;
            }
            return found;
        }

        public static bool CutOn(DaySeriesBook book, int day) => book != null && book.CutFrom > 0 && day >= book.CutFrom && day < book.CutUntil;

        /// <summary>Customers a price cut of 5 per cent is taken to bring, a small steady figure, the same everywhere.</summary>
        public static int CutFootfall(DaySeriesBook book, int day) => CutOn(book, day) ? 3 : 0;

        /// <summary>
        /// A campaign's forecast from reach already saved, or from customers when reach is not there yet. It saves nothing:
        /// about a twentieth of the reach, a few at least.
        /// </summary>
        public static int CampaignForecast(int reach, int customers)
        {
            int baseOn = reach > 0 ? reach / 20 : customers / 80;
            if (baseOn < 4)
            {
                return reach <= 0 && customers <= 0 ? 0 : 4;
            }
            return baseOn > 400 ? 400 : baseOn;
        }

        /// <summary>Headroom over the lending threshold, 0 to 100. It is how far the rating sits over what the bank needs, not a chance.</summary>
        public static int Headroom(int score, int threshold, int top)
        {
            if (score < threshold || top <= threshold)
            {
                return 0;
            }
            int room = (score - threshold) * 100 / (top - threshold);
            return room > 100 ? 100 : room;
        }

        /// <summary>True when the business has traded long enough.</summary>
        public static bool HistoryOk(int daysOfTakings) => daysOfTakings >= HistoryDays;

        /// <summary>True when takings cover the daily payment the number of times the rule asks.</summary>
        public static bool CoverOk(long takings, int dailyPayment)
        {
            if (dailyPayment <= 0)
            {
                return takings > 0;
            }
            return takings >= (long)dailyPayment * CoverTimes;
        }

        /// <summary>The daily payment of a loan, whole dollars, from its amount, its yearly rate in hundredths of a per cent, and its term in days.</summary>
        public static int DailyPayment(int amount, int rateBp, int days)
        {
            if (amount <= 0 || days <= 0)
            {
                return 0;
            }
            if (rateBp <= 0)
            {
                return Math.Max(1, amount / days);
            }
            double daily = rateBp / 10000.0 / 365.0;
            double factor = daily / (1.0 - Math.Pow(1.0 + daily, -days));
            int pay = (int)Math.Ceiling(amount * factor - 1e-6);
            return pay < 1 ? 1 : pay;
        }

        /// <summary>Interest over the whole term: every daily payment, less the amount borrowed.</summary>
        public static int Interest(int amount, int daily, int days)
        {
            long all = (long)daily * days;
            long over = all - amount;
            if (over <= 0)
            {
                return 0;
            }
            return over > int.MaxValue ? int.MaxValue : (int)over;
        }

        /// <summary>What is still owed after <paramref name="paid"/> days, never under nothing.</summary>
        public static int StillOwed(int amount, int rateBp, int daily, int paid)
        {
            if (amount <= 0 || paid <= 0)
            {
                return Math.Max(0, amount);
            }
            double bal = amount;
            double rate = rateBp <= 0 ? 0.0 : rateBp / 10000.0 / 365.0;
            for (int i = 0; i < paid && bal > 0; i++)
            {
                double interest = bal * rate;
                bal = bal + interest - daily;
            }
            if (bal <= 0)
            {
                return 0;
            }
            return bal > int.MaxValue ? int.MaxValue : (int)Math.Ceiling(bal - 1e-6);
        }

        /// <summary>
        /// Who borrows today, at most <see cref="LoansADay"/>, in the order of the names once they are sorted by the caller.
        /// <paramref name="banks"/> is how many real banks there are. The amount is the bank's share of a slice of their wealth.
        /// </summary>
        public static int Borrow(DaySeriesBook book, int day, string[] who, int[] wealth, int banks, int[] usualLimit)
        {
            if (book == null || day <= 0 || who == null || wealth == null || banks <= 0)
            {
                return 0;
            }
            int seed = book.Seed != 0 ? book.Seed : DefaultSeed;
            int added = 0;
            int n = who.Length < wealth.Length ? who.Length : wealth.Length;
            for (int i = 0; i < n && added < LoansADay; i++)
            {
                if (string.IsNullOrEmpty(who[i]) || wealth[i] <= 0)
                {
                    continue;
                }
                int id = NameId(who[i]);
                if (Draw(seed, day, id, 1, 11) != 0)
                {
                    continue;
                }
                int rank = Draw(seed, day, id, 2, banks);
                int usual = usualLimit != null && rank < usualLimit.Length ? usualLimit[rank] : wealth[i] / 8;
                int amount = BankRules.Most(usual, rank);
                int cap = wealth[i] / 5;
                if (amount > cap)
                {
                    amount = cap;
                }
                amount = amount / 1000 * 1000;
                if (amount >= 1000 && book.AddLoan(who[i], rank, day, amount))
                {
                    added++;
                }
            }
            return added;
        }

        /// <summary>
        /// Splits <paramref name="total"/> across the names so the parts sum to it. The weights come from the seed, so the
        /// same names and total always split the same way. Returns how many parts were written into <paramref name="into"/>.
        /// </summary>
        public static int Split(int seed, int total, string[] names, int[] into)
        {
            if (into == null || names == null || total <= 0)
            {
                return 0;
            }
            int n = names.Length < into.Length ? names.Length : into.Length;
            int live = 0;
            for (int i = 0; i < n; i++)
            {
                into[i] = 0;
                live += string.IsNullOrEmpty(names[i]) ? 0 : 1;
            }
            if (live == 0)
            {
                return 0;
            }
            int weight = 0;
            var w = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(names[i]))
                {
                    continue;
                }
                w[i] = 1 + Draw(seed, 1, i, 3, 20);
                weight += w[i];
            }
            int left = total;
            int last = -1;
            for (int i = 0; i < n; i++)
            {
                if (w[i] == 0)
                {
                    continue;
                }
                into[i] = (int)((long)total * w[i] / weight);
                left -= into[i];
                last = i;
            }
            if (last >= 0)
            {
                into[last] += left;
            }
            return live;
        }

        // The name, not its place in the caller's list, so two machines with the same firms borrow the same way.
        private static int NameId(string name)
        {
            int hash = 17;
            for (int i = 0; i < name.Length; i++)
            {
                hash = hash * 31 + name[i];
            }
            return hash & int.MaxValue;
        }

        /// <summary>A mix of the seed and the day into a number under <paramref name="mod"/>. The same inputs always match.</summary>
        public static int Draw(int seed, int day, int index, int purpose, int mod)
        {
            if (mod <= 1)
            {
                return 0;
            }
            uint x = (uint)seed * 2246822519u ^ (uint)day * 3266489917u ^ (uint)index * 668265263u ^ (uint)purpose * 374761393u;
            x ^= x >> 16;
            x *= 2246822519u;
            x ^= x >> 13;
            return (int)(x % (uint)mod);
        }

        /// <summary>Writes the city's unemployment (thousandths of its labour force, from the population) for a day. Nothing for a figure below nought: no population runs.</summary>
        public static bool CityJobs(DaySeriesBook book, int day, int jobless)
        {
            return jobless >= 0 && book.Set(DaySeriesCatalogue.Unemployment, day, jobless);
        }
    }
}
