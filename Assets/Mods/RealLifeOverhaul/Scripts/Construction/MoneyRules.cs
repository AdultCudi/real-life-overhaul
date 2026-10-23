using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A project's money: the construction loan drawn in stages (its most, rate and fee, a day's interest with the fraction
    /// carried, the repayments after completion), an investor's money in and what they are owed, and how a sale's money is
    /// shared between the loan, the investor and the player. Whole money; interest in hundredths, never lost to rounding. Game-free.
    /// </summary>
    public static class MoneyRules
    {
        /// <summary>The most a bank lends on a project: a share of its land and its estimate, in thousands.</summary>
        public static long LoanMost(long land, long estimate)
        {
            long most = (Math.Max(0L, land) + Math.Max(0L, estimate)) * BuildCatalogue.LoanToCostPermille / 1000L;
            return most / 1000L * 1000L;
        }

        /// <summary>The loan's rate in hundredths of a per cent a year: the business loan's rate at the player's rating and some points more.</summary>
        public static int RateBp(double businessRate)
        {
            double rate = double.IsNaN(businessRate) || businessRate <= 0.0 ? BuildCatalogue.LoanRateDefault : businessRate + BuildCatalogue.LoanPointsOver;
            return (int)Math.Round(Math.Min(60.0, rate) * 100.0);
        }

        /// <summary>The bank's fee for arranging a loan, taken with its first draw.</summary>
        public static long Fee(long agreed)
        {
            return Math.Max(0L, agreed) * BuildCatalogue.LoanFeePermille / 1000L;
        }

        /// <summary>One draw: a share of the loan at the start (0) and after each inspection (1 to 3), the last taking what is left.</summary>
        public static long Draw(long agreed, long drawn, int index)
        {
            if (index < 0 || index >= BuildCatalogue.LoanDrawPermille.Length || agreed <= 0)
            {
                return 0L;
            }
            if (index == BuildCatalogue.LoanDrawPermille.Length - 1)
            {
                return Math.Max(0L, agreed - drawn);
            }
            return Math.Min(Math.Max(0L, agreed - drawn), agreed * BuildCatalogue.LoanDrawPermille[index] / 1000L);
        }

        /// <summary>
        /// A day's interest on what is owed, at a rate in hundredths of a per cent a year over a year of the save's days; the
        /// fraction of a unit is kept in <paramref name="carry"/> (hundredths) for the next day.
        /// </summary>
        public static long DayInterest(long balance, int rateBp, int daysPerYear, ref long carry)
        {
            if (balance <= 0 || rateBp <= 0 || daysPerYear <= 0)
            {
                return 0L;
            }
            long hundredths = balance * rateBp / daysPerYear / 100L + carry;   // balance x (bp / 10,000) / days, in hundredths
            carry = hundredths % 100L;
            return hundredths / 100L;
        }

        /// <summary>A day's repayment after completion: what is owed spread over the days left of the term, rounded up.</summary>
        public static long Instalment(long balance, int daysLeft)
        {
            if (balance <= 0)
            {
                return 0L;
            }
            return daysLeft <= 1 ? balance : (balance + daysLeft - 1) / daysLeft;
        }

        /// <summary>The days a loan is repaid over from completion: a few game years of the save's length.</summary>
        public static int TermDays(int daysPerYear)
        {
            return Math.Max(8, daysPerYear) * BuildCatalogue.LoanYears;
        }

        /// <summary>What an investor puts in: a share of the estimate, in thousands.</summary>
        public static long InvestorPut(int investor, long estimate)
        {
            int i = BuildCatalogue.Clamp(investor, 0, BuildCatalogue.InvestorCount - 1);
            long put = Math.Max(0L, estimate) * BuildCatalogue.InvestorSharePermille[i] / 1000L;
            return put / 1000L * 1000L;
        }

        /// <summary>What an investor is owed at completion for what they put in.</summary>
        public static long InvestorOwed(int investor, long put)
        {
            int i = BuildCatalogue.Clamp(investor, 0, BuildCatalogue.InvestorCount - 1);
            return Math.Max(0L, put) * (1000L + BuildCatalogue.InvestorReturnPermille[i]) / 1000L;
        }

        /// <summary>
        /// A flat's price shared out: a share to the loan while it is owed, then the investor while they are owed, the rest the
        /// player's. Returns the player's; the loan's and the investor's parts come back through the outs.
        /// </summary>
        public static long Share(long price, long loanOwed, long investorOwed, out long toLoan, out long toInvestor)
        {
            long p = Math.Max(0L, price);
            toLoan = Math.Min(Math.Max(0L, loanOwed), p * BuildCatalogue.LoanFromSalesPermille / 1000L);
            long left = p - toLoan;
            toInvestor = Math.Min(Math.Max(0L, investorOwed), left);
            return left - toInvestor;
        }
    }
}
