using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Where one debt, an account or a game loan, stands with its lender: missed payments in a row and what was done about them.</summary>
    public sealed class LadderState
    {
        public int Misses;          // due payments missed in a row
        public int Overdue;         // the money of those payments
        public int PaidRun;         // payments made in a row since the penalty rate began
        public int Step;            // the furthest step this run of misses has reached (LadderStep)
        public bool Penalty;        // the penalty rate is on
        public int PlanLeft;        // a payment plan: money still to pay under it
        public int PlanPerDay;
        public bool PlanUsed;       // one plan per account
        public bool CallDue;        // the collections call has not been answered yet
        public int RepoDay;         // the day the security was taken or the takings began to be diverted; 0 for not
        public bool RepoPending;    // the vehicle is to be taken as soon as it stands out of sight
        public int ClaimNumber;     // the court's number of the claim; 0 for none

        public bool InOrder => Misses == 0 && PlanLeft == 0 && RepoDay == 0 && !RepoPending && ClaimNumber == 0;

        public void Clear()
        {
            Misses = Overdue = PaidRun = Step = PlanLeft = PlanPerDay = RepoDay = ClaimNumber = 0;
            Penalty = PlanUsed = CallDue = RepoPending = false;
        }
    }

    /// <summary>
    /// What a lender does as due payments go on being missed, for accounts and the game's loans alike: steps by misses in a row
    /// (<see cref="ProductCatalogue.StepCounts"/>), back to nothing on a payment in full. Game-free: the caller acts on the step.
    /// </summary>
    public static class LadderRules
    {
        /// <summary>Whether a level takes a step at all, for a debt that is or is not secured.</summary>
        public static bool Takes(LadderStep step, int level, bool secured)
        {
            if (step == LadderStep.None || step > ProductCatalogue.LastStep[ProductCatalogue.Level(level)])
            {
                return false;
            }
            return step != LadderStep.Repossession || secured;
        }

        /// <summary>A due payment was missed; <paramref name="overdue"/> is the money now overdue in all. Returns the step this miss takes, or None.</summary>
        public static LadderStep Missed(LadderState s, int level, bool secured, int overdue)
        {
            level = ProductCatalogue.Level(level);
            int[] counts = ProductCatalogue.Counts(level);
            s.Overdue = Math.Max(0, overdue);
            s.PaidRun = 0;
            LadderStep step = LadderStep.None;
            if (s.PlanLeft > 0)
            {
                // the plan is broken: straight to the next step this level and this debt have, and the count goes with it
                s.PlanLeft = 0;
                s.PlanPerDay = 0;
                step = Takes(LadderStep.Repossession, level, secured) && s.RepoDay == 0 && !s.RepoPending ? LadderStep.Repossession
                    : Takes(LadderStep.Claim, level, secured) ? LadderStep.Claim : LadderStep.None;
                s.Misses = step == LadderStep.None ? s.Misses + 1 : Math.Max(s.Misses + 1, counts[(int)step - 1]);
            }
            else
            {
                s.Misses++;
                for (int i = counts.Length - 1; i >= 0; i--)
                {
                    if (s.Misses == counts[i])
                    {
                        step = (LadderStep)(i + 1);
                        break;
                    }
                }
                if (!Takes(step, level, secured))
                {
                    step = LadderStep.None;
                }
            }
            if (step == LadderStep.None)
            {
                return step;
            }
            s.Step = Math.Max(s.Step, (int)step);
            if (step >= LadderStep.PenaltyRate)
            {
                s.Penalty = true;   // a broken plan can pass the third miss by: the dearer rate goes with any later step
            }
            if (step == LadderStep.Collections)
            {
                s.CallDue = true;
            }
            return step;
        }

        /// <summary>A due payment was made in full. True if the penalty rate ends with it.</summary>
        public static bool Paid(LadderState s)
        {
            s.Misses = 0;
            if (s.PlanLeft <= 0)
            {
                s.PlanLeft = 0;
                s.PlanPerDay = 0;
                s.Overdue = 0;
                s.Step = (int)LadderStep.None;
                s.CallDue = false;
            }
            if (!s.Penalty || ++s.PaidRun < ProductCatalogue.PenaltyClearRun)
            {
                return false;
            }
            s.Penalty = false;
            s.PaidRun = 0;
            return true;
        }

        public static int PlanPerDay(int overdue)
        {
            return overdue <= 0 ? 0 : (overdue + ProductCatalogue.PlanDays - 1) / ProductCatalogue.PlanDays;
        }

        /// <summary>The collections call's plan. False if there is nothing overdue or the one plan has been had.</summary>
        public static bool AgreePlan(LadderState s)
        {
            if (s.PlanUsed || s.Overdue <= 0)
            {
                return false;
            }
            s.PlanUsed = true;
            s.PlanLeft = s.Overdue;
            s.PlanPerDay = PlanPerDay(s.Overdue);
            s.Misses = 0;
            s.CallDue = false;
            return true;
        }

        public static int ClaimCosts(int balance)
        {
            return balance <= 0 ? 0 : Math.Max(ProductCatalogue.ClaimCostsLeast, ProductCatalogue.ShareUp(balance, ProductCatalogue.ClaimCostsShare));
        }

        public static int RepossessionValue(int price)
        {
            return price <= 0 ? 0 : (int)Math.Floor(price * (double)ProductCatalogue.RepossessionValueShare + 1e-4);
        }

        /// <summary>A balance left unsecured by a repossession is due for a claim.</summary>
        public static bool UnsecuredClaimDue(LadderState s, int owed, int today, int level)
        {
            return s.RepoDay > 0 && !s.RepoPending && s.ClaimNumber == 0 && owed > 0 && today - s.RepoDay >= ProductCatalogue.UnsecuredClaimDays
                   && Takes(LadderStep.Claim, level, false);
        }
    }
}
