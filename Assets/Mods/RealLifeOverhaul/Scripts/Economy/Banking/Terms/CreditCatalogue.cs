using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The six grades a credit rating is shown with, lowest first. Saved as a number: never reorder.</summary>
    public enum CreditGrade
    {
        VeryPoor = 0,
        Poor = 1,
        Fair = 2,
        Good = 3,
        VeryGood = 4,
        Excellent = 5
    }

    /// <summary>The five things the rating is worked out from, in the order of <see cref="CreditCatalogue.Weights"/>.</summary>
    public enum CreditFactor
    {
        Payments = 0,       // instalments paid in full and on time against those missed
        Owed = 1,           // what is owed against what could be borrowed and against recent income
        Length = 2,         // how long there has been credit at all
        Legal = 3,          // fines paid late, court debts, civil judgments, convictions
        Applications = 4,   // applications made in the last few days
        Count = 5
    }

    /// <summary>What can be written on the credit file. Saved as a number: add at the end, never renumber.</summary>
    public enum CreditKind
    {
        PaidOnTime = 0,     // the day's instalments, paid in full
        Missed = 1,         // an instalment that could not be taken
        PartPaid = 2,       // an instalment paid in part (no lender does that yet)
        NewCredit = 3,
        PaidOff = 4,        // the last instalment taken: the account is closed
        PaidOffEarly = 5,   // paid off by the borrower ahead of its instalments
        PaymentChanged = 6, // the borrower changed the daily payment
        ExtraPayment = 7,   // part of the principal paid ahead of its instalments
        Application = 8,
        Refused = 9,
        FineLate = 10,      // a fine or court debt that could not be taken on its day
        Judgment = 11,      // a civil judgment against the borrower
        Conviction = 12,
        DebtsCleared = 13,  // every overdue fine and court debt paid
        Started = 14,       // the file was opened, with the estimate it began at
        TestAdjust = 15,    // a test tool moved the rating
        Consolidated = 16,  // debts paid off by one new loan that replaces them: not an early pay-off
        LateFee = 17,
        PenaltyRate = 18,
        Collections = 19,
        Repossessed = 20,   // the security of a debt was taken
        ClaimFiled = 21,    // a lender filed a civil claim for a debt
        PlanAgreed = 22,
        Arrangement = 23,   // a debt arrangement began
        Bankruptcy = 24,
        Count = 25
    }

    /// <summary>Why nobody lends, for <see cref="CreditLedger.CanBorrow"/>.</summary>
    public enum CreditRefusal
    {
        None = 0,
        NotReady = 1,        // no save is loaded, or the economy is switched off
        BelowThreshold = 2,
        Bankrupt = 3         // a bankruptcy in the last days: nobody lends, whatever the score
    }

    /// <summary>
    /// Every number of the credit rating and of a bank's loan terms, read by the scoring, the banks' terms, the Finance app,
    /// the Guide and the offline check. Game-free: compiled as it is by EconomyCheck.
    /// </summary>
    public static class CreditCatalogue
    {
        public const int ScoreMin = 300;
        public const int ScoreMax = 850;
        public const int LendingThreshold = 580;   // below this no lender lends, on any preset: no exception and no dearer offer
        public const int CleanStart = 640;         // where a save with no debts and no record begins

        public static readonly int[] GradeFrom = { 300, 500, 580, 670, 740, 800 };   // the lowest score of each grade

        public static readonly string[] GradeKeys =
        {
            "reallife_econ_grade_very_poor", "reallife_econ_grade_poor", "reallife_econ_grade_fair",
            "reallife_econ_grade_good", "reallife_econ_grade_very_good", "reallife_econ_grade_excellent"
        };

        public static readonly int[] Weights = { 40, 25, 10, 15, 10 };   // per cent of the rating each factor decides; they add up to 100

        public static readonly string[] FactorKeys =
        {
            "reallife_econ_factor_payments", "reallife_econ_factor_owed", "reallife_econ_factor_length",
            "reallife_econ_factor_legal", "reallife_econ_factor_applications"
        };

        public const int PaymentFadeDays = 60;   // game days over which a payment, made or missed, fades from full to nothing

        /// <summary>
        /// The payment factor of an empty file, as a share of its full value: what makes a clean save come out at
        /// <see cref="CleanStart"/> (40 x 13/44 + 25 + 0 + 15 + 10 = 61.82 of 100, and 300 + 5.5 x 61.82 = 640).
        /// </summary>
        public const float PaymentNeutral = 13f / 44f;

        public const float PaymentPriorWeight = 4f;      // payments the empty file's standing weighs as: the more, the slower a new history moves it
        public const float MissWeight = 8f;              // paid instalments one missed one weighs as
        public const float PartPaidShare = 0.5f;         // of a missed one, an instalment paid in part
        public const float LastingPaymentShare = 0.05f;  // of a fresh one, a payment on time past the fade, for good...
        public const int LastingPaymentsMost = 200;      // ...up to this many of them
        public const float OwedAgainstLimitShare = 0.8f; // of the "owed" factor, debt against what could be borrowed; the rest is payments against income
        public const float ServiceShareWorst = 0.5f;     // daily payments at this share of daily income are the worst the income half can be
        public const int HistoryFullDays = 120;          // game days of credit history that count in full

        // the legal and public record: what each thing takes off that factor (1 is spotless, 0 the worst)
        public const float ConvictionCost = 0.35f;
        public const float JudgmentCost = 0.25f;
        public const float LateDayCost = 0.08f;        // per day a fine or court debt could not be taken...
        public const float LateDaysCostMost = 0.40f;   // ...up to this
        public const float OutstandingCost = 0.05f;    // a court debt or fine still being paid off
        public const float OffenceCost = 0.05f;        // per line on the police record in the last OffenceDays...
        public const float OffencesCostMost = 0.20f;   // ...up to this
        public const int OffenceDays = 30;             // game days

        public const int ApplicationFadeDays = 10;     // game days an application counts against the borrower, less each day
        public const float ApplicationDent = 0.15f;    // what a fresh application takes off the applications factor

        public const int LedgerMost = 120;             // events kept on the file; older ones are folded into running totals
        public const int LedgerShown = 30;             // events the Finance app lists

        public const int BestTermsFrom = 800;          // the rating from which a lender offers its best terms
        public const float CapShareAtThreshold = 0.40f;   // of a bank's own cap, lent at the threshold...
        public const float CapShareAtBest = 1.30f;        // ...and from BestTermsFrom up
        public const int RatePointsAtThreshold = 6;       // points on a bank's own annual rate at the threshold...
        public const int RatePointsAtBest = -2;           // ...and from BestTermsFrom up
        public const int RateFloor = 1;                   // per cent a year: no rate goes under it

        /// <summary>The fee for arranging a loan by phone: a share of the amount, never less than the least. A bank in person charges none.</summary>
        public const float PhoneFeeShare = 0.015f;
        public static int PhoneFeeLeast => Prices.At(25);

        /// <summary>The smallest loan the game's banks make (BankDialog.MinimumLoanAmount).</summary>
        public static int LoanLeast => Prices.At(500);

        public static CreditGrade GradeOf(int score)
        {
            for (int g = GradeFrom.Length - 1; g > 0; g--)
            {
                if (score >= GradeFrom[g])
                {
                    return (CreditGrade)g;
                }
            }
            return CreditGrade.VeryPoor;
        }

        /// <summary>The highest score of a grade.</summary>
        public static int GradeTo(CreditGrade grade)
        {
            int g = (int)grade;
            return g >= GradeFrom.Length - 1 ? ScoreMax : GradeFrom[g + 1] - 1;
        }

        /// <summary>The score for a weighted total of the factors between 0 and 100.</summary>
        public static int ScoreFor(float total)
        {
            float t = total < 0f ? 0f : total > 100f ? 100f : total;
            return Clamp((int)Math.Round(ScoreMin + (ScoreMax - ScoreMin) * t / 100f));
        }

        public static int Clamp(int score)
        {
            return score < ScoreMin ? ScoreMin : score > ScoreMax ? ScoreMax : score;
        }

        /// <summary>How many points of the score one whole factor is worth.</summary>
        public static float PointsOf(CreditFactor factor)
        {
            return Weights[(int)factor] * (ScoreMax - ScoreMin) / 100f;
        }
    }
}
