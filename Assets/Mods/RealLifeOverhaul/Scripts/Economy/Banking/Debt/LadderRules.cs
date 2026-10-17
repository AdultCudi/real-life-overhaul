using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What a lender does when payments are not made: one state machine for every debt, the mod's accounts and the
    /// game's loans alike. It counts due payments missed in a row; a payment made in full puts the count back to
    /// nothing (what is on the credit file stays there).
    ///
    ///   misses   Standard    Hardcore    what happens
    ///   1        1           1           a reminder
    ///   2        2           2           a late fee
    ///   3        3           3           a penalty rate, until three payments in a row are made
    ///   4        5           4           the collections call: pay what is overdue, agree a plan, or neither
    ///   5        8           6           secured accounts only: the security is taken
    ///   6        12          9           a civil claim through the courts
    ///
    /// Relaxed stops after the call, Standard after the security is taken, Realistic and Hardcore go to the claim
    /// (<see cref="ProductCatalogue.LastStep"/>). A plan that is broken goes straight to the step after the call.
    /// Game-free: the caller acts on the step it is given (a notice, a fee, a rate, a call).
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

        /// <summary>
        /// A due payment was missed; <paramref name="overdue"/> is the money now overdue in all. Returns the step this
        /// miss takes, or None.
        /// </summary>
        public static LadderStep Missed(LadderState s, int level, bool secured, int overdue)
        {
            level = ProductCatalogue.Level(level);
            int[] counts = ProductCatalogue.Counts(level);
            s.Overdue = Math.Max(0, overdue);
            s.PaidRun = 0;
            LadderStep step = LadderStep.None;
            if (s.PlanLeft > 0)
            {
                // The plan is broken: straight to the next step this level and this debt have, and the count goes with it.
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
                s.Penalty = true;   // (a broken plan can pass the third miss by: the dearer rate goes with any later step)
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

        /// <summary>The plan's daily amount for what is overdue.</summary>
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

        /// <summary>The lender's costs on a claim for this balance.</summary>
        public static int ClaimCosts(int balance)
        {
            return balance <= 0 ? 0 : Math.Max(ProductCatalogue.ClaimCostsLeast, ProductCatalogue.ShareUp(balance, ProductCatalogue.ClaimCostsShare));
        }

        /// <summary>What a repossessed vehicle bought for this price fetches.</summary>
        public static int RepossessionValue(int price)
        {
            return price <= 0 ? 0 : (int)Math.Floor(price * (double)ProductCatalogue.RepossessionValueShare + 1e-4);
        }

        /// <summary>
        /// A balance left unsecured by a repossession is due for a claim: the days have passed, it still stands, the
        /// level goes that far and no claim is out.
        /// </summary>
        public static bool UnsecuredClaimDue(LadderState s, int owed, int today, int level)
        {
            return s.RepoDay > 0 && !s.RepoPending && s.ClaimNumber == 0 && owed > 0 && today - s.RepoDay >= ProductCatalogue.UnsecuredClaimDays
                   && Takes(LadderStep.Claim, level, false);
        }
    }
}
