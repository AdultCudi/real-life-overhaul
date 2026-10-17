using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The game's loan interest formula (LoanHelper), for where the mod sets a rate of its own on one of the game's loans: a
    /// business loan, the penalty rate, and a loan priced again for a new interest multiplier. Game-free, so the offline check
    /// holds it against the game's formula.
    /// </summary>
    public static class GameLoanMath
    {
        private const int RateMost = 1000;   // per cent a year: far above any rate a bank or the mod writes; bounds the search for a loan's rate

        /// <summary>LoanHelper.CalculateDailyInterestPayment term for term, in float as the game works it.</summary>
        public static int DailyInterest(float amount, float annualRate, float multiplier, int years, int daysPerYear)
        {
            years = Math.Max(1, years);
            return (int)Math.Floor(amount * annualRate * multiplier / 100f * (float)years / (float)(Math.Max(1, daysPerYear) * years));
        }

        /// <summary>
        /// A loan's daily interest with penalty points on its rate. The game keeps a loan's interest, not its rate, so the points'
        /// interest on the amount borrowed is added to the loan's own: within a unit of the formula at the higher rate.
        /// </summary>
        public static int PenaltyInterest(int ownInterest, float amount, int points, float multiplier, int daysPerYear)
        {
            return Math.Max(0, ownInterest) + DailyInterest(amount, points, multiplier, 1, daysPerYear);
        }

        /// <summary>
        /// A loan's daily interest at a new interest multiplier, at the rate it was made at. The game keeps a loan's interest, not
        /// its rate, so the rate is read back from the interest at the old multiplier: every rate the game or the mod writes is a
        /// whole per cent, and where a small loan's interest fits several, the middle one is taken. An interest no whole rate
        /// gives is moved as the same share of its unit. -1 when no rate can be read (there was no multiplier before).
        /// </summary>
        public static int Repriced(int interest, float amount, float was, float now, int years, int daysPerYear)
        {
            if (was <= 0f || amount <= 0f || interest < 0)
            {
                return -1;
            }
            double point = amount * (double)was / 100.0 / Math.Max(1, daysPerYear);   // a day's interest for each per cent of rate
            double low = Math.Floor(interest / point) - 1.0, high = Math.Ceiling((interest + 1.0) / point) + 1.0;
            int from = low > RateMost ? RateMost + 1 : Math.Max(0, (int)low), to = (int)Math.Min(RateMost, high);
            int first = -1, last = -1;
            for (int rate = from; rate <= to; rate++)
            {
                if (DailyInterest(amount, rate, was, years, daysPerYear) == interest)
                {
                    first = first < 0 ? rate : first;
                    last = rate;
                }
            }
            return first < 0 ? (int)Math.Floor((interest + 0.5) * now / was) : DailyInterest(amount, (first + last) / 2, now, years, daysPerYear);
        }
    }
}
