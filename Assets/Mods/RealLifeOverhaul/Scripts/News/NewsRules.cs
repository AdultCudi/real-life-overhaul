namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Economy app's sums: a basket's change, a loan fixed against a variable one, who is paid under the going rate,
    /// and whether the calmer sector is the defensive one. Game-free. Nothing a frame.
    /// </summary>
    public static class NewsRules
    {
        public const int DaysMonth = 30;     // a month of the chart, and the saving's month
        public const int DaysBook = 120;     // the book, and a game year of two 60-day years
        public const int YearDays = 60;      // one game year

        public static int Span(int range) => range <= 0 ? DaysMonth : DaysBook;

        public static int YearOf(int day) => day < 1 ? 1 : (day - 1) / YearDays + 1;

        /// <summary>The edition's number is the day it was printed. Year 1 is days 1 to 60.</summary>
        public static int EditionOf(int day) => day < 1 ? 0 : day;

        /// <summary>The basket's change in basis points. 0 when either reading is missing.</summary>
        public static int BasketBp(int now, int then)
        {
            if (then <= 0 || now <= 0)
            {
                return 0;
            }
            return (int)((long)(now - then) * 10000 / then);
        }

        /// <summary>A day's interest at a fixed rate in basis points, the game's own formula, multiplier 1.</summary>
        public static int FixedDaily(int amount, int fixedRateBp, int daysPerYear)
        {
            if (amount <= 0 || fixedRateBp <= 0)
            {
                return 0;
            }
            return GameLoanMath.DailyInterest(amount, fixedRateBp / 100f, 1f, 1, daysPerYear);
        }

        /// <summary>What a month saves when the fixed day is cheaper. 0 when the fixed rate does not help.</summary>
        public static int MonthlySaving(int variableDaily, int fixedDaily)
        {
            int gap = variableDaily - fixedDaily;
            return gap > 0 ? gap * DaysMonth : 0;
        }

        /// <summary>
        /// The fixed rate from the same terms a consolidation uses, and the month it saves on this loan.
        /// </summary>
        public static int Offer(int amount, int variableDaily, int score, int ownRate, int daysPerYear, out int fixedDaily)
        {
            int bp = ProductTerms.FixedRateBp(score, ownRate, 0, 1f);
            fixedDaily = FixedDaily(amount, bp, daysPerYear);
            return MonthlySaving(variableDaily, fixedDaily);
        }

        /// <summary>The player and a rival both fix when a rise is set and the fixed day is cheaper.</summary>
        public static bool WillFix(bool rateRising, int variableDaily, int fixedDaily)
        {
            return rateRising && variableDaily > fixedDaily;
        }

        /// <summary>1 when this wage is under the going rate.</summary>
        public static int Underpaid(int going, int paid) => going > 0 && paid < going ? 1 : 0;

        /// <summary>True when the calm sector varies less than the wild one, so the line may be said.</summary>
        public static bool Defensive(double calm, double wild) => calm > 0.0 && wild > calm;

        /// <summary>What the Pass saves against taking every title, a week. 0 when it does not.</summary>
        public static int PassSaving(int titlesSum, int pass)
        {
            int gap = titlesSum - pass;
            return gap > 0 ? gap : 0;
        }

        /// <summary>Shares that sum to 1, or zeros when the counts are empty. into is filled up to its length.</summary>
        public static void Shares(int[] counts, float[] into)
        {
            int n = counts == null ? 0 : counts.Length;
            int sum = 0;
            for (int i = 0; i < n; i++)
            {
                sum += counts[i] > 0 ? counts[i] : 0;
            }
            int m = into == null ? 0 : into.Length;
            for (int i = 0; i < m; i++)
            {
                int count = i < n && counts[i] > 0 ? counts[i] : 0;
                into[i] = sum <= 0 ? 0f : (float)count / sum;
            }
        }
    }
}
