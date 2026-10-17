using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What the save keeps of the ways out of debt and of an approval for finance (part "r" of the credit file's line).</summary>
    public sealed class Relief
    {
        public const int ClaimsMost = 12;

        public bool Arranged;           // a debt arrangement is running
        public int ArrangedDay;
        public int ArrangedPerDay;
        public int ArrangedTotal;       // what was owed when it began
        public int ArrangedPaid;        // paid under it: the daily payments, and what was paid by hand into its debts
        public int ArrangedMisses;      // of its payments, missed in all
        public int ArrangedEndDay;      // the day the last one ended; 0 for never

        public int BankruptDay;         // the day of the last bankruptcy; 0 for never

        public int OverdueSince;        // the first day of the present run with something overdue; 0 for none
        public bool AdviceTold;         // debt advice has been offered for this run
        public bool AdviceKnown;        // its contact is on the phone
        public bool CollectorKnown;     // and the collections contact

        // an approval for car finance or a mortgage, waiting for the purchase
        public int ApprovedKind = -1;   // ProductKind; -1 for none
        public int ApprovedAmount;      // advanced into cash
        public int ApprovedDeposit;     // per cent
        public int ApprovedTerm;
        public int ApprovedRateBp;
        public int ApprovedDay;
        public string ApprovedStreet = string.Empty;   // the lending bank
        public int ApprovedNumber;

        public readonly List<int> Claims = new List<int>(2);   // the court's numbers of the lenders' claims still open

        public bool HasApproval => ApprovedKind >= 0 && ApprovedAmount > 0;

        public void ClearApproval()
        {
            ApprovedKind = -1;
            ApprovedAmount = ApprovedDeposit = ApprovedTerm = ApprovedRateBp = ApprovedDay = ApprovedNumber = 0;
            ApprovedStreet = string.Empty;
        }
    }

    /// <summary>What a bankruptcy does, worked out before it is asked for.</summary>
    public struct BankruptcyPlan
    {
        public int WrittenOff;      // unsecured debt that goes
        public int Kept;            // debt that stays: a mortgage, fines and court debts
        public int CashNow;
        public int CashTaken;       // to the creditors
        public int CashLeft;
        public int NoCreditUntil;   // the last day on which nobody lends
        public int NextAllowedDay;  // the first day another bankruptcy could be asked for
    }

    /// <summary>What a debt arrangement comes to, worked out before it is asked for.</summary>
    public struct ArrangementPlan
    {
        public int Total;
        public int PerDay;
        public int Days;
    }

    /// <summary>The arithmetic of the ways out of debt, and the ceilings they put on the rating. Game-free.</summary>
    public static class ReliefRules
    {
        public static int ArrangementPayment(float dailyIncome)
        {
            return Math.Max(ProductCatalogue.ArrangementLeast, (int)Math.Round(Math.Max(0f, dailyIncome) * (double)ProductCatalogue.ArrangementShare));
        }

        public static ArrangementPlan Arrangement(int total, float dailyIncome)
        {
            int perDay = Math.Min(Math.Max(0, total), ArrangementPayment(dailyIncome));
            return new ArrangementPlan { Total = Math.Max(0, total), PerDay = perDay, Days = perDay > 0 ? (total + perDay - 1) / perDay : 0 };
        }

        public static void BeginArrangement(Relief r, in ArrangementPlan plan, int today)
        {
            r.Arranged = true;
            r.ArrangedDay = today;
            r.ArrangedPerDay = plan.PerDay;
            r.ArrangedTotal = plan.Total;
            r.ArrangedPaid = 0;
            r.ArrangedMisses = 0;
        }

        /// <summary>A day's payment was missed. True if that ends the arrangement.</summary>
        public static bool ArrangementMissed(Relief r)
        {
            return ++r.ArrangedMisses >= ProductCatalogue.ArrangementMissesMost;
        }

        public static void EndArrangement(Relief r, int today)
        {
            r.Arranged = false;
            r.ArrangedEndDay = today;
        }

        /// <summary>What is still owed on the accounts an arrangement holds (the frozen ones).</summary>
        public static int FrozenOwed(List<CreditAccount> accounts)
        {
            long owed = 0;
            for (int i = 0; accounts != null && i < accounts.Count; i++)
            {
                CreditAccount a = accounts[i];
                owed += a.Frozen && !a.Closed ? Math.Max(0, a.Owed) : 0;
            }
            return (int)Math.Min(int.MaxValue, owed);
        }

        /// <summary>
        /// Money paid by hand into a debt an arrangement holds counts under it; <paramref name="left"/> is what it still holds
        /// after. True when it is to end now, cleared: once ended it is not running, so it never ends twice.
        /// </summary>
        public static bool PaidByHand(Relief r, int amount, int left)
        {
            if (r == null || !r.Arranged)
            {
                return false;
            }
            r.ArrangedPaid = (int)Math.Min(int.MaxValue, (long)r.ArrangedPaid + Math.Max(0, amount));
            return left <= 0;
        }

        public static bool CanBankrupt(Relief r, int today)
        {
            return r.BankruptDay <= 0 || today - r.BankruptDay >= ProductCatalogue.BankruptEveryDays;
        }

        public static BankruptcyPlan Bankruptcy(int unsecured, int kept, int cash, int today)
        {
            cash = Math.Max(0, cash);
            int taken = Math.Max(0, cash - ProductCatalogue.BankruptKeepCash);
            return new BankruptcyPlan
            {
                WrittenOff = Math.Max(0, unsecured), Kept = Math.Max(0, kept), CashNow = cash, CashTaken = taken, CashLeft = cash - taken,
                NoCreditUntil = today + ProductCatalogue.BankruptNoCreditDays - 1, NextAllowedDay = today + ProductCatalogue.BankruptEveryDays
            };
        }

        /// <summary>Nobody lends for a while after a bankruptcy, whatever the score (<see cref="CreditLedger.CanBorrow"/>).</summary>
        public static bool NoCredit(Relief r, int today)
        {
            return r.BankruptDay > 0 && today - r.BankruptDay < ProductCatalogue.BankruptNoCreditDays;
        }

        /// <summary>The most the rating can be today, after a bankruptcy or during an arrangement and for some days after it.</summary>
        public static int Ceiling(Relief r, int today)
        {
            int ceiling = CreditCatalogue.ScoreMax;
            if (r.BankruptDay > 0)
            {
                int since = today - r.BankruptDay;
                long rebuilt = since < ProductCatalogue.BankruptNoCreditDays ? ProductCatalogue.BankruptScore
                    : ProductCatalogue.BankruptRebuildFrom + (long)(since - ProductCatalogue.BankruptNoCreditDays) * ProductCatalogue.BankruptRebuildPerDay;
                ceiling = (int)Math.Min(ceiling, Math.Max(CreditCatalogue.ScoreMin, rebuilt));
            }
            if (r.Arranged || (r.ArrangedEndDay > 0 && today - r.ArrangedEndDay < ProductCatalogue.ArrangementCapDaysAfter))
            {
                ceiling = Math.Min(ceiling, ProductCatalogue.ArrangementScoreCap);
            }
            return ceiling;
        }

        /// <summary>Keeps count of how long something has been overdue. True on the day debt advice is to be offered, once a run.</summary>
        public static bool AdviceDue(Relief r, bool anyOverdue, bool claimFiled, int today)
        {
            if (!anyOverdue && !claimFiled)
            {
                r.OverdueSince = 0;
                r.AdviceTold = false;
                return false;
            }
            if (r.OverdueSince <= 0)
            {
                r.OverdueSince = today;
            }
            if (r.AdviceTold || (!claimFiled && today - r.OverdueSince < ProductCatalogue.AdviceOverdueDays))
            {
                return false;
            }
            r.AdviceTold = true;
            return true;
        }

        /// <summary>An approval for finance has run out: nothing was bought by the end of the day after it was given.</summary>
        public static bool ApprovalOver(Relief r, int today)
        {
            return r.HasApproval && today > r.ApprovedDay + ProductCatalogue.ApprovalDays;
        }
    }
}
