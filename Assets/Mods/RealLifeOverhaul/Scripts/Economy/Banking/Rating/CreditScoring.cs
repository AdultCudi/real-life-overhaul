using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A rating as worked out at one moment: the score, its grade, each factor from 0 (worst) to 1 (best), and the counts the app words them with.</summary>
    public sealed class CreditScore
    {
        public int Value;
        public CreditGrade Grade;
        public readonly float[] Factor = new float[(int)CreditFactor.Count];

        public int OnTime, Missed;          // instalments inside the fade
        public int HistoryDays;             // days since credit was first held; 0 for none
        public int Applications;            // still counting
        public float OwedShare;             // owed against what could be borrowed, 0 to 1 and over
        public float ServiceShare;          // daily payments against daily income; -1 when there is no income to measure against
        public LegalStanding Legal;
        public int Ceiling = CreditCatalogue.ScoreMax;   // the most it can be today (a bankruptcy, a debt arrangement)

        public bool Lends => Value >= CreditCatalogue.LendingThreshold;

        /// <summary>True while a bankruptcy or a debt arrangement is holding the score down.</summary>
        public bool Held => Ceiling < CreditCatalogue.ScoreMax && Value >= Ceiling;

        /// <summary>Points of the score a factor still has to give.</summary>
        public float Room(CreditFactor factor)
        {
            return CreditCatalogue.PointsOf(factor) * (1f - Factor[(int)factor]);
        }

        public void CopyTo(CreditScore other)
        {
            other.Value = Value;
            other.Grade = Grade;
            Array.Copy(Factor, other.Factor, Factor.Length);
            other.OnTime = OnTime;
            other.Missed = Missed;
            other.HistoryDays = HistoryDays;
            other.Applications = Applications;
            other.OwedShare = OwedShare;
            other.ServiceShare = ServiceShare;
            other.Legal = Legal;
            other.Ceiling = Ceiling;
        }
    }

    /// <summary>The rating's arithmetic: the same file, figures and day always give the same score. Game-free; one pass over at most 60 days and 120 events.</summary>
    public static class CreditScoring
    {
        public static void Compute(CreditFile file, in CreditInputs inputs, int today, CreditScore into)
        {
            into.Factor[(int)CreditFactor.Payments] = Payments(file, today, into);
            into.Factor[(int)CreditFactor.Owed] = Owed(inputs, into);
            into.Factor[(int)CreditFactor.Length] = Length(file, today, into);
            into.Factor[(int)CreditFactor.Legal] = Legal(inputs.Legal);
            into.Factor[(int)CreditFactor.Applications] = Applications(file, today, into);
            into.Legal = inputs.Legal;
            float total = 0f;
            for (int f = 0; f < (int)CreditFactor.Count; f++)
            {
                total += CreditCatalogue.Weights[f] * into.Factor[f];
            }
            into.Ceiling = ReliefRules.Ceiling(file.Relief, today);
            into.Value = CreditCatalogue.Clamp(Math.Min(CreditCatalogue.ScoreFor(total) + file.TestOffset, into.Ceiling));
            into.Grade = CreditCatalogue.GradeOf(into.Value);
        }

        // An instalment weighs by how recent it is, a missed one as several paid; an empty file stands at PaymentNeutral.
        private static float Payments(CreditFile file, int today, CreditScore into)
        {
            float good = 0f, bad = 0f;
            into.OnTime = 0;
            into.Missed = 0;
            for (int i = 0; i < file.Days.Count; i++)
            {
                PayDay d = file.Days[i];
                int age = today - d.Day;
                if (age < 0)
                {
                    age = 0;
                }
                if (age >= CreditCatalogue.PaymentFadeDays)
                {
                    continue;
                }
                float w = 1f - age / (float)CreditCatalogue.PaymentFadeDays;
                int onTime = d.OnTime + d.TestOnTime, missed = d.Missed + d.TestMissed;
                good += w * onTime;
                bad += w * (missed + CreditCatalogue.PartPaidShare * d.Part);
                into.OnTime += onTime;
                into.Missed += missed + d.Part;
            }
            float lasting = Math.Min(file.OldOnTime, CreditCatalogue.LastingPaymentsMost) * CreditCatalogue.LastingPaymentShare;
            float prior = CreditCatalogue.PaymentPriorWeight;
            return Clamp01((prior * CreditCatalogue.PaymentNeutral + good + lasting) / (prior + good + lasting + CreditCatalogue.MissWeight * bad));
        }

        private static float Owed(in CreditInputs inputs, CreditScore into)
        {
            float owed = Math.Max(0f, inputs.Owed);
            into.OwedShare = inputs.Limit > 0f ? owed / inputs.Limit : owed > 0f ? 1f : 0f;
            float againstLimit = 1f - Clamp01(into.OwedShare);
            float againstIncome;
            if (inputs.DailyService <= 0f)
            {
                into.ServiceShare = 0f;
                againstIncome = 1f;
            }
            else if (inputs.DailyIncome <= 0f)
            {
                into.ServiceShare = -1f;
                againstIncome = 0f;
            }
            else
            {
                into.ServiceShare = inputs.DailyService / inputs.DailyIncome;
                againstIncome = 1f - Clamp01(into.ServiceShare / CreditCatalogue.ServiceShareWorst);
            }
            return CreditCatalogue.OwedAgainstLimitShare * againstLimit + (1f - CreditCatalogue.OwedAgainstLimitShare) * againstIncome;
        }

        private static float Length(CreditFile file, int today, CreditScore into)
        {
            into.HistoryDays = file.FirstCreditDay > 0 ? Math.Max(0, today - file.FirstCreditDay) : 0;
            return Clamp01(into.HistoryDays / (float)CreditCatalogue.HistoryFullDays);
        }

        public static float Legal(in LegalStanding legal)
        {
            float cost = Math.Max(0, legal.Convictions) * CreditCatalogue.ConvictionCost
                         + Math.Max(0, legal.Judgments) * CreditCatalogue.JudgmentCost
                         + Math.Min(Math.Max(0, legal.LateDays) * CreditCatalogue.LateDayCost, CreditCatalogue.LateDaysCostMost)
                         + (legal.Outstanding > 0 ? CreditCatalogue.OutstandingCost : 0f)
                         + Math.Min(Math.Max(0, legal.RecentOffences) * CreditCatalogue.OffenceCost, CreditCatalogue.OffencesCostMost);
            return Clamp01(1f - cost);
        }

        private static float Applications(CreditFile file, int today, CreditScore into)
        {
            float dent = 0f;
            into.Applications = 0;
            for (int i = file.Events.Count - 1; i >= 0; i--)
            {
                CreditEvent e = file.Events[i];
                if (e.Kind != CreditKind.Application)
                {
                    continue;
                }
                int age = Math.Max(0, today - e.Day);
                if (age >= CreditCatalogue.ApplicationFadeDays)
                {
                    continue;
                }
                dent += CreditCatalogue.ApplicationDent * (1f - age / (float)CreditCatalogue.ApplicationFadeDays) * Math.Max(1, e.Count);
                into.Applications += Math.Max(1, e.Count);
            }
            return Clamp01(1f - dent);
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        /// <summary>The factors that would raise the score most, best first, each worth at least <paramref name="worthPoints"/>. Returns how many.</summary>
        public static int Tips(CreditScore score, CreditFactor[] into, float worthPoints = 5f)
        {
            int found = 0;
            for (int pass = 0; pass < into.Length; pass++)
            {
                int best = -1;
                float bestRoom = worthPoints;
                for (int f = 0; f < (int)CreditFactor.Count; f++)
                {
                    bool taken = false;
                    for (int k = 0; k < found; k++)
                    {
                        taken |= (int)into[k] == f;
                    }
                    float room = score.Room((CreditFactor)f);
                    if (!taken && room >= bestRoom)
                    {
                        best = f;
                        bestRoom = room;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                into[found++] = (CreditFactor)best;
            }
            return found;
        }

        /// <summary>
        /// Opens the file of a save that never had one: each loan is taken as paid on time for its paid-off part over its
        /// daily payment in days (the game takes exactly that a day), which gives the history's length.
        /// </summary>
        public static void Open(CreditFile file, List<LoanView> loans, int today)
        {
            file.StartDay = today;
            int longest = -1;
            for (int i = 0; i < loans.Count; i++)
            {
                LoanView loan = loans[i];
                int days = loan.Payment > 0 ? (int)Math.Floor(Math.Max(0f, loan.Total - loan.Remaining) / loan.Payment) : 0;
                days = Math.Min(days, CreditCatalogue.LastingPaymentsMost);
                file.OldOnTime += days;
                longest = Math.Max(longest, days);
            }
            if (longest >= 0)
            {
                file.FirstCreditDay = Math.Max(1, today - longest);
            }
        }
    }
}
