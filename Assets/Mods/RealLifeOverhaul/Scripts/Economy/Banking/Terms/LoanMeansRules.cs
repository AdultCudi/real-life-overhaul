using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The game's means test for a loan (BankDialog.GetEconomyMaxLoanAmount, private there), game-free, with the one thing the game leaves to the year's length put right. The test lets a
    /// borrower have the larger of their wealth and a quarter of the daily income over the days a loan is paid back in, less what is owed. The days are the bank's years times the game's
    /// days a year (LoanHelper.CalculatePayBackDays), so at 365 days a year the same income is lent six times what it is at the 60 the game is balanced on: a new player with 2,000 in the
    /// bank and gig work was offered 48,000. A bank's own cap does not scale with the year either, so it never held that back. With the year scaled, a long year lends what a 60 day year
    /// lends for the same daily income; a shorter year keeps the game's smaller limit. Costs arithmetic, on a press or at a game hour.
    /// </summary>
    public static class LoanMeansRules
    {
        /// <summary>Days: the year the game's means test is balanced on (its default game year).</summary>
        public const int ReferenceYear = 60;

        /// <summary>The share of the daily income that counts for each day a loan is paid back in: the game's own.</summary>
        public const float IncomeShare = 0.25f;

        /// <summary>The first loan the game's tutorial lends before its first business objective is done: the game's own (TutorialHelper.MaxLoanAmountBeforeCompletingObjectiveGetFirstLoan).</summary>
        public const float TutorialLoan = 15500f;

        /// <summary>The days a loan is paid back in, as the means test counts them: the game's (years times the year's days), or with the year scaled never more than the reference year's.</summary>
        public static int PayBackDays(int daysPerYear, int years, bool scaled)
        {
            int days = Math.Max(1, daysPerYear);
            return (scaled ? Math.Min(days, ReferenceYear) : days) * Math.Max(1, years);
        }

        /// <summary>
        /// The most a borrower's means allow in new loans, in whole money: the game's test term for term. <paramref name="tutorial"/> is true while the game's first loan is still
        /// to be taken in its tutorial. <paramref name="owed"/> is every loan, at every bank.
        /// </summary>
        public static float Limit(float wealth, float dailyIncome, int payBackDays, float owed, bool tutorial)
        {
            float most = tutorial ? TutorialLoan : 0f;
            most = Math.Max(most, wealth);
            most = Math.Max(most, dailyIncome * payBackDays * IncomeShare);
            return (float)Math.Floor(Math.Max(0f, most - owed));
        }
    }
}
