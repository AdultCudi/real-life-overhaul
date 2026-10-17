using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The credit desk's collections call: the debt it is about and its three answers. The conversation itself is <see cref="DebtPhone"/>'s.</summary>
    public sealed partial class CreditDesk
    {
        /// <summary>The debt the collections call is about: the first whose call has not been answered. False if there is none.</summary>
        public bool CallAbout(out Debt debt)
        {
            debt = default(Debt);
            for (int i = 0; _file != null && i < _file.Loans.Count; i++)
            {
                if (_file.Loans[i].Ladder.CallDue && _file.Loans[i].Ref is Loan)
                {
                    debt = new Debt(_file.Loans[i]);
                    return true;
                }
            }
            for (int i = 0; _file != null && i < _file.Accounts.Count; i++)
            {
                if (_file.Accounts[i].Ladder.CallDue && !_file.Accounts[i].Closed)
                {
                    debt = new Debt(_file.Accounts[i]);
                    return true;
                }
            }
            return false;
        }

        /// <summary>"Pay what is overdue now": the money is taken, comes off the debt, and the debt is in order again.</summary>
        public ProductAnswer PayOverdue(Debt debt)
        {
            if (_file == null || !debt.Valid)
            {
                return ProductAnswer.Off;
            }
            LadderState s = debt.Ladder;
            int amount = Mathf.Min(s.Overdue, debt.Owed);
            string name = debt.Name;
            if (amount <= 0)
            {
                s.CallDue = false;
                return ProductAnswer.NothingOwed;
            }
            if (!(debt.Account != null && debt.Account.Test) && !_wallet.Take(amount, Money.Payment, name))
            {
                return ProductAnswer.NoCash;   // a test account takes no money
            }
            int day = Today;
            if (debt.Account != null)
            {
                AccountRules.Pay(debt.Account, amount);
                if (debt.Account.Closed)
                {
                    CreditLedger.Unregister(debt.Account);
                }
            }
            else
            {
                LoanMark m = debt.Mark;
                m.Ladder.PlanLeft = 0;
                if (LadderRules.Paid(m.Ladder))
                {
                    EndHold(m, LoanHold.Penalty, "what was overdue is paid");
                }
                PayDown(m, debt.Loan, amount, day);
            }
            s.CallDue = false;
            CreditLedger.Touch();
            if (Verbose)
            {
                Logger?.Info("Economy check (collections): " + name + ": " + amount + " overdue paid at once; the debt is in order again: " + (s.Misses == 0 && s.Overdue == 0 ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("what was overdue was paid");
            return ProductAnswer.Yes;
        }

        /// <summary>"Agree a payment plan": what is overdue over the plan's days, on top of the normal payment. One plan per debt.</summary>
        public ProductAnswer AgreePlan(Debt debt)
        {
            if (_file == null || !debt.Valid)
            {
                return ProductAnswer.Off;
            }
            LadderState s = debt.Ladder;
            if (s.PlanUsed)
            {
                return ProductAnswer.AlreadyOpen;
            }
            bool agreed = debt.Account != null ? AccountRules.AgreePlan(debt.Account) : LadderRules.AgreePlan(s);
            if (!agreed)
            {
                return ProductAnswer.NothingOwed;
            }
            Post(debt, CreditKind.PlanAgreed, s.PlanLeft, Today);
            if (Verbose)
            {
                Logger?.Info("Economy check (collections): " + debt.Name + ": a plan for " + s.PlanLeft + " at " + s.PlanPerDay + " a day on top of the normal payment: "
                             + (s.PlanPerDay * ProductCatalogue.PlanDays >= s.PlanLeft ? "PASS" : "FAIL"));
            }
            return ProductAnswer.Yes;
        }

        /// <summary>"I can't pay": the call is over, and the count goes on.</summary>
        public void CannotPay(Debt debt)
        {
            if (_file != null && (debt.Account != null || debt.Mark != null))
            {
                debt.Ladder.CallDue = false;
                if (Verbose)
                {
                    Logger?.Info("Economy check (collections): " + debt.Name + ": the borrower cannot pay; nothing is agreed and the count goes on at " + debt.Ladder.Misses + ": PASS");
                }
            }
        }
    }
}
