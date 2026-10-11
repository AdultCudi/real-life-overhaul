using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a lender offers a borrower with a given rating.</summary>
    public struct LenderTerms
    {
        public bool Lends;
        public float Cap;       // the most the borrower may owe this lender in all
        public int AnnualRate;  // per cent a year, before the game's own interest multiplier
    }

    /// <summary>Why an amount cannot be borrowed.</summary>
    public enum LoanAnswer
    {
        Approved = 0,
        BelowThreshold = 1,
        TooLittle = 2,      // under the smallest loan a bank makes
        TooMuch = 3         // over what this lender will lend now
    }

    /// <summary>
    /// The terms table: a lender's own cap and rate, moved by the borrower's rating. Game-free.
    ///
    ///   under the threshold   nothing is lent: the cap is 0
    ///   at the threshold      40% of the lender's own cap, its own rate plus 6 points
    ///   from 800              130% of its own cap, its own rate less 2 points
    ///   between               a straight line from the one to the other; the rate is rounded to a whole point
    ///
    /// A rate is never under 1 per cent. Both figures move one way only as the score rises: the cap never falls and
    /// the rate never rises.
    /// </summary>
    public static class LendingTerms
    {
        public static LenderTerms For(int score, float ownCap, int ownRate)
        {
            var terms = new LenderTerms();
            float t = Position(score);
            float points = CreditCatalogue.RatePointsAtThreshold + (CreditCatalogue.RatePointsAtBest - CreditCatalogue.RatePointsAtThreshold) * t;
            terms.AnnualRate = Math.Max(CreditCatalogue.RateFloor, ownRate + (int)Math.Round(points, MidpointRounding.AwayFromZero));
            terms.Lends = score >= CreditCatalogue.LendingThreshold;
            if (terms.Lends)
            {
                float share = CreditCatalogue.CapShareAtThreshold + (CreditCatalogue.CapShareAtBest - CreditCatalogue.CapShareAtThreshold) * t;
                terms.Cap = (float)Math.Round(Math.Max(0f, ownCap) * (double)share);   // to the whole unit: a share that is 1.3 but for float noise must not lose one
            }
            return terms;
        }

        /// <summary>Where a score stands between the threshold (0) and the best terms (1).</summary>
        public static float Position(int score)
        {
            float t = (score - CreditCatalogue.LendingThreshold) / (float)(CreditCatalogue.BestTermsFrom - CreditCatalogue.LendingThreshold);
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        /// <summary>The fee for arranging a loan of this amount by phone: the share, rounded up to a whole unit of money, and never under the least.</summary>
        public static int PhoneFee(float amount)
        {
            // (the small step back keeps a product that is whole but for float noise, 1,000 x 1.5%, from being rounded up one more)
            return amount <= 0f ? 0 : Math.Max(CreditCatalogue.PhoneFeeLeast, (int)Math.Ceiling(amount * (double)CreditCatalogue.PhoneFeeShare - 1e-4));
        }

        /// <summary>Whether an amount can be borrowed, given the rating and the most the lender will lend now.</summary>
        public static LoanAnswer Decide(int score, float amount, float mostNow)
        {
            if (score < CreditCatalogue.LendingThreshold)
            {
                return LoanAnswer.BelowThreshold;
            }
            if (amount < CreditCatalogue.LoanLeast)
            {
                return LoanAnswer.TooLittle;
            }
            return amount > mostNow ? LoanAnswer.TooMuch : LoanAnswer.Approved;
        }

        /// <summary>
        /// The most a lender will lend now, by the two rules the game's bank applies (BankDialog.LoanRequest): its cap
        /// less what is already owed to it, and never more than the borrower's means allow
        /// (<paramref name="meansLimit"/>: the game's own figure from wealth and income, less every loan).
        /// </summary>
        public static float MostNow(float cap, float owedToLender, float meansLimit)
        {
            float room = Math.Max(0f, cap - Math.Max(0f, owedToLender));
            return (float)Math.Floor(Math.Max(0f, Math.Min(room, meansLimit)));
        }
    }
}
