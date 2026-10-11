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
        PartPaid = 2,       // an instalment paid in part (no lender of this phase does that; kept for the ones to come)
        NewCredit = 3,      // money borrowed
        PaidOff = 4,        // the last instalment taken: the account is closed
        PaidOffEarly = 5,   // paid off by the borrower ahead of its instalments
        PaymentChanged = 6, // the borrower changed the daily payment
        ExtraPayment = 7,   // part of the principal paid ahead of its instalments
        Application = 8,    // an application for credit
        Refused = 9,        // an application turned down
        FineLate = 10,      // a fine or court debt that could not be taken on its day
        Judgment = 11,      // a civil judgment against the borrower
        Conviction = 12,    // a criminal conviction
        DebtsCleared = 13,  // every overdue fine and court debt paid
        Started = 14,       // the file was opened, with the estimate it began at
        TestAdjust = 15,    // a test tool moved the rating (test only, taken out by "clear test entries")
        Consolidated = 16,  // debts paid off by one new loan that replaces them: not an early pay-off
        LateFee = 17,       // a late fee was added to a debt
        PenaltyRate = 18,   // a debt went onto the penalty rate
        Collections = 19,   // a debt was passed to collections
        Repossessed = 20,   // the security of a debt was taken
        ClaimFiled = 21,    // a lender filed a civil claim for a debt
        PlanAgreed = 22,    // a payment plan was agreed with a lender
        Arrangement = 23,   // a debt arrangement began
        Bankruptcy = 24,    // the borrower was made bankrupt
        Count = 25
    }

    /// <summary>Why nobody lends, for <see cref="CreditLedger.CanBorrow"/>.</summary>
    public enum CreditRefusal
    {
        None = 0,
        NotReady = 1,        // no save is loaded, or the economy is switched off
        BelowThreshold = 2,  // the rating is under the lending threshold
        Bankrupt = 3         // a bankruptcy in the last days: nobody lends, whatever the score
    }

    /// <summary>
    /// Every number of the credit rating and of lending, in one place. The scoring, the banks' terms, the Finance app,
    /// the Guide's page and the offline check all read these; nothing else in the mod writes a figure of its own.
    /// Game-free: this file is compiled as it is by RealLifeOverhaul.Build/EconomyCheck.
    /// </summary>
    public static class CreditCatalogue
    {
        // ---------------------------------------------------------------- the scale

        public const int ScoreMin = 300;
        public const int ScoreMax = 850;

        /// <summary>Below this no lender lends, on any preset: no exception and no dearer offer instead.</summary>
        public const int LendingThreshold = 580;

        /// <summary>Where a save with no debts and no record begins.</summary>
        public const int CleanStart = 640;

        /// <summary>The lowest score of each grade, in the order of <see cref="CreditGrade"/>.</summary>
        public static readonly int[] GradeFrom = { 300, 500, 580, 670, 740, 800 };

        public static readonly string[] GradeKeys =
        {
            "reallife_econ_grade_very_poor", "reallife_econ_grade_poor", "reallife_econ_grade_fair",
            "reallife_econ_grade_good", "reallife_econ_grade_very_good", "reallife_econ_grade_excellent"
        };

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

        // ---------------------------------------------------------------- the factors

        /// <summary>Per cent of the rating each factor decides, in the order of <see cref="CreditFactor"/>. They add up to 100.</summary>
        public static readonly int[] Weights = { 40, 25, 10, 15, 10 };

        public static readonly string[] FactorKeys =
        {
            "reallife_econ_factor_payments", "reallife_econ_factor_owed", "reallife_econ_factor_length",
            "reallife_econ_factor_legal", "reallife_econ_factor_applications"
        };

        /// <summary>A payment, made or missed, counts in full on its day and fades to nothing over this many game days.</summary>
        public const int PaymentFadeDays = 60;

        /// <summary>
        /// Where the payment factor stands with nothing on file, as a share of its full value. It is what makes a save
        /// with no debts and no record come out at <see cref="CleanStart"/>: 40 x 13/44 + 25 + 0 + 15 + 10 = 61.82 of
        /// 100, and 300 + 5.5 x 61.82 = 640.
        /// </summary>
        public const float PaymentNeutral = 13f / 44f;

        /// <summary>How many payments the empty file's standing weighs as: the more, the slower a new history moves the factor.</summary>
        public const float PaymentPriorWeight = 4f;

        /// <summary>One missed instalment weighs as this many paid ones.</summary>
        public const float MissWeight = 8f;

        /// <summary>An instalment paid in part weighs as this share of a missed one.</summary>
        public const float PartPaidShare = 0.5f;

        /// <summary>Payments on time older than the fade still count a little, for good: each as this much of a fresh one, up to <see cref="LastingPaymentsMost"/> of them.</summary>
        public const float LastingPaymentShare = 0.05f;
        public const int LastingPaymentsMost = 200;

        /// <summary>Of the "owed" factor, the share decided by debt against what could be borrowed; the rest is the daily payments against daily income.</summary>
        public const float OwedAgainstLimitShare = 0.8f;

        /// <summary>Daily payments at this share of daily income (or more) are the worst the income half can be.</summary>
        public const float ServiceShareWorst = 0.5f;

        /// <summary>A credit history of this many game days counts in full.</summary>
        public const int HistoryFullDays = 120;

        // The legal and public record: what each thing takes off that factor (1 is spotless, 0 the worst).
        public const float ConvictionCost = 0.35f;
        public const float JudgmentCost = 0.25f;
        public const float LateDayCost = 0.08f;        // per day a fine or court debt could not be taken...
        public const float LateDaysCostMost = 0.40f;   // ...up to this
        public const float OutstandingCost = 0.05f;    // a court debt or fine still being paid off
        public const float OffenceCost = 0.05f;        // per line on the police record in the last OffenceDays...
        public const float OffencesCostMost = 0.20f;   // ...up to this
        public const int OffenceDays = 30;

        /// <summary>An application counts against the borrower for this many game days, less each day.</summary>
        public const int ApplicationFadeDays = 10;

        /// <summary>What a fresh application takes off the applications factor.</summary>
        public const float ApplicationDent = 0.15f;

        // ---------------------------------------------------------------- the file

        /// <summary>Events kept on the file; older ones are folded into running totals.</summary>
        public const int LedgerMost = 120;

        /// <summary>Events the Finance app lists.</summary>
        public const int LedgerShown = 30;

        // ---------------------------------------------------------------- lending

        /// <summary>The rating from which a lender offers its best terms.</summary>
        public const int BestTermsFrom = 800;

        /// <summary>The share of a bank's own cap it will lend at the threshold, and from <see cref="BestTermsFrom"/> up.</summary>
        public const float CapShareAtThreshold = 0.40f;
        public const float CapShareAtBest = 1.30f;

        /// <summary>Points on a bank's own annual rate at the threshold, and from <see cref="BestTermsFrom"/> up. Never a rate under <see cref="RateFloor"/>.</summary>
        public const int RatePointsAtThreshold = 6;
        public const int RatePointsAtBest = -2;
        public const int RateFloor = 1;

        /// <summary>The fee for arranging a loan by phone: a share of the amount, and never less than the minimum. A bank in person charges none.</summary>
        public const float PhoneFeeShare = 0.015f;
        public const int PhoneFeeLeast = 25;

        /// <summary>The smallest loan the game's banks make (BankDialog.MinimumLoanAmount).</summary>
        public const int LoanLeast = 500;

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
