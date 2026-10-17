using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The game's loan interest formula (LoanHelper), for where the mod sets a rate of its own on one of the game's loans: a
    /// business loan, and the penalty rate. Game-free, so the offline check holds it against the game's formula.
    /// </summary>
    public static class GameLoanMath
    {
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
    }
}
