using System.Collections.Generic;
using Tutorial.SideQuests;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The credit desk's ways out of debt: debt advice, a debt arrangement and bankruptcy. The advice call itself is <see cref="DebtPhone"/>'s.</summary>
    public sealed partial class CreditDesk
    {
        private const string GameBankruptcyQuest = "SQBankruptcy";   // the game's own side quest: SideQuestHelper.BankruptcyQuestId

        private readonly List<DebtLine> _lines = new List<DebtLine>(6);

        /// <summary>True while the game's own bankruptcy quest is on: the mod's bankruptcy is not offered, and the player is pointed to the game's.</summary>
        public static bool GameBankruptcy => SideQuestHelper.IsSideQuestActive(GameBankruptcyQuest);

        // A debt an arrangement takes: anything not secured on something the borrower still has, never a test account.
        private static bool Unsecured(CreditAccount a)
        {
            return !a.Closed && !a.Test && a.Owed > 0 && !(ProductCatalogue.Instalment(a.Kind) && a.Secured);
        }

        // One of the game's quest loans is open (the tutorial's first-business loan): the ways out leave the game's own story alone.
        private static bool QuestLoan => BankTerms.House != null && BankTerms.QuestHold(BankTerms.House.Settings) && !GameBankruptcy;

        /// <summary>Everything owed, by what could become of it: the game's loans, the mod's unsecured accounts, what is secured, the private lender, fines and court debts.</summary>
        public void Owed(out int loans, out int unsecured, out int secured, out int shark, out int other)
        {
            loans = unsecured = secured = shark = other = 0;
            if (_file == null || Save == null)
            {
                return;
            }
            for (int i = 0; i < Save.Loans.Count; i++)
            {
                loans += Mathf.CeilToInt(Mathf.Max(0f, Save.Loans[i].remainingAmount));
            }
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (!a.Closed && !a.Test)
                {
                    unsecured += Unsecured(a) ? a.Owed : 0;
                    secured += Unsecured(a) ? 0 : a.Owed;
                }
            }
            shark = _file.Shark.Active ? _file.Shark.Owed : 0;
            _lines.Clear();
            EconomyBridges.OtherDebts?.Invoke(_lines);
            for (int i = 0; i < _lines.Count; i++)
            {
                other += _lines[i].Owed;
            }
        }

        // How long something has been overdue, whether a claim is still open, and debt advice when it is due.
        private void ReliefDay(int day)
        {
            Relief r = _file.Relief;
            for (int i = r.Claims.Count - 1; i >= 0; i--)
            {
                if (EconomyBridges.ClaimOpen == null || !EconomyBridges.ClaimOpen(r.Claims[i]))
                {
                    r.Claims.RemoveAt(i);   // settled, judged or dismissed: the court's business from here on
                }
            }
            bool overdue = false;
            for (int i = 0; i < _file.Loans.Count && !overdue; i++)
            {
                overdue = _file.Loans[i].Ladder.Misses > 0;
            }
            for (int i = 0; i < _file.Accounts.Count && !overdue; i++)
            {
                overdue = !_file.Accounts[i].Closed && !_file.Accounts[i].Test && _file.Accounts[i].Ladder.Misses > 0;
            }
            overdue |= _file.Shark.Active && day > _file.Shark.DueDay;
            if (!ReliefRules.AdviceDue(r, overdue, _claimedToday || r.Claims.Count > 0, day))
            {
                return;
            }
            r.AdviceKnown = true;
            DebtPhone.Setup(this);
            Note(NotificationType.Info, "reallife_econ_note_advice", null, _openAdvice, 16f);
            if (Verbose)
            {
                Logger?.Info("Economy check (debt advice, day " + day + "): offered; something has been overdue since day " + r.OverdueSince + ", " + r.Claims.Count
                             + " claim(s) before the court: PASS");
            }
        }

        /// <summary>What a debt arrangement would come to: every loan and every unsecured account, paid by one daily payment out of income.</summary>
        public ProductAnswer ArrangementOffer(out ArrangementPlan plan)
        {
            plan = default(ArrangementPlan);
            if (_file == null)
            {
                return ProductAnswer.Off;
            }
            if (_file.Relief.Arranged)
            {
                return ProductAnswer.AlreadyOpen;
            }
            if (QuestLoan || GameBankruptcy)
            {
                return ProductAnswer.Off;
            }
            int loans, unsecured, secured, shark, other;
            Owed(out loans, out unsecured, out secured, out shark, out other);
            if (loans + unsecured <= 0)
            {
                return ProductAnswer.NothingOwed;
            }
            plan = ReliefRules.Arrangement(loans + unsecured, Income);
            return ProductAnswer.Yes;
        }

        /// <summary>Begins it: the debts stand still (no interest, no fees, no further steps) and are paid by the one payment.</summary>
        public ProductAnswer Arrange()
        {
            ArrangementPlan plan;
            ProductAnswer answer = ArrangementOffer(out plan);
            if (answer != ProductAnswer.Yes)
            {
                return answer;
            }
            int day = Today;
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                var loan = m.Ref as Loan;
                if (loan == null)
                {
                    continue;
                }
                Quiet(m.Ladder);
                BeginHold(m, loan, LoanHold.Frozen, "a debt arrangement");
            }
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (Unsecured(a))
                {
                    a.Frozen = true;
                    Quiet(a.Ladder);
                    a.MinimumLeft = 0;
                    a.DueDay = 0;
                }
            }
            ReliefRules.BeginArrangement(_file.Relief, plan, day);
            CreditLedger.Post(CreditKind.Arrangement, plan.Total, day, "advice");
            if (Verbose)
            {
                Logger?.Info("Economy check (debt arrangement, day " + day + "): " + plan.Total + " frozen (no interest, no fees, no further steps), " + plan.PerDay + " a day for about " + plan.Days
                             + " days out of an average income of " + Income.ToString("0") + "; rating held at " + ProductCatalogue.ArrangementScoreCap + " or under, now " + CreditLedger.Value + ": "
                             + (CreditLedger.Value <= ProductCatalogue.ArrangementScoreCap ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a debt arrangement");
            return ProductAnswer.Yes;
        }

        // A debt that goes into an arrangement: nothing is waiting on it any more.
        private static void Quiet(LadderState s)
        {
            s.CallDue = false;
            s.PlanLeft = 0;
            s.PlanPerDay = 0;
            s.Misses = 0;
            s.Overdue = 0;
            s.RepoDay = s.RepoPending ? s.RepoDay : 0;
        }

        // The arrangement's day: the one payment, shared out over its debts. Missing it too often ends the arrangement.
        private void ArrangementDay(int day)
        {
            Relief r = _file.Relief;
            int left = ArrangedLeft();
            int due = Mathf.Min(r.ArrangedPerDay, left);
            if (due > 0 && !_wallet.Take(due, Money.Arrangement, string.Empty))
            {
                bool over = ReliefRules.ArrangementMissed(r);
                if (Verbose)
                {
                    Logger?.Info("Economy check (debt arrangement, day " + day + "): the payment of " + due + " was missed (" + r.ArrangedMisses + " of " + ProductCatalogue.ArrangementMissesMost + ")"
                                 + (over ? ": the arrangement is over" : string.Empty) + ": PASS");
                }
                if (over)
                {
                    EndArrangement(day, false, left);
                }
                else
                {
                    Note(NotificationType.Warning, "reallife_econ_note_arranged_missed", Data("amount", FinanceText.Money(due), "count", r.ArrangedMisses.ToString(), "most",
                         ProductCatalogue.ArrangementMissesMost.ToString()));
                }
                return;
            }
            int share = due;
            // the loans newest first (counted down, so a paid-off one can leave the list): a fixed order is what matters
            for (int i = _file.Loans.Count - 1; i >= 0 && share > 0; i--)
            {
                LoanMark m = _file.Loans[i];
                var loan = m.Ref as Loan;
                if ((m.Held & LoanHold.Frozen) == 0 || loan == null)
                {
                    continue;
                }
                int part = Mathf.Min(share, Mathf.CeilToInt(loan.remainingAmount));
                share -= part;
                PayDown(m, loan, part, day);
            }
            for (int i = 0; i < _file.Accounts.Count && share > 0; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (!a.Frozen || a.Closed)
                {
                    continue;
                }
                int part = Mathf.Min(share, a.Owed);
                share -= part;
                AccountRules.Pay(a, part);
                if (a.Closed)
                {
                    CreditLedger.Unregister(a);
                }
            }
            r.ArrangedPaid += due;
            if (Verbose)
            {
                Logger?.Info("Economy check (debt arrangement, day " + day + "): " + due + " paid, " + (left - due) + " left of " + r.ArrangedTotal + ": " + (share == 0 ? "PASS" : "FAIL"));
            }
            if (left - due <= 0)
            {
                EndArrangement(day, true, 0);
            }
        }

        // What everything an arrangement holds still owes: its frozen loans and its frozen accounts.
        private int ArrangedLeft()
        {
            int left = 0;
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                left += (m.Held & LoanHold.Frozen) != 0 && m.Ref is Loan ? Mathf.CeilToInt(Mathf.Max(0f, ((Loan)m.Ref).remainingAmount)) : 0;
            }
            return left + ReliefRules.FrozenOwed(_file.Accounts);
        }

        /// <summary>
        /// Money paid by hand into a debt the arrangement holds, in an app or in EconoView: it counts under the arrangement,
        /// which ends cleared the moment nothing is left in it, whichever way the last of it was paid.
        /// </summary>
        public void PaidByHand(int amount, string how)
        {
            if (_file == null || !_file.Relief.Arranged)
            {
                return;
            }
            int left = ArrangedLeft(), day = Today;
            bool ends = ReliefRules.PaidByHand(_file.Relief, amount, left);
            if (Verbose)
            {
                Logger?.Info("Economy check (debt arrangement, day " + day + "): " + amount + " paid by hand (" + how + "), " + left + " left of " + _file.Relief.ArrangedTotal + ", "
                             + _file.Relief.ArrangedPaid + " paid under it" + (ends ? ": everything in it is paid, it ends now" : string.Empty) + ": "
                             + ((long)_file.Relief.ArrangedPaid + left <= _file.Relief.ArrangedTotal ? "PASS" : "FAIL"));   // nothing in it grows: paid and left never pass what it began with
            }
            if (ends)
            {
                EndArrangement(day, true, 0);
            }
        }

        // The arrangement ends, cleared or broken: what is still owed runs again as before, and the rating stays held for the stated days.
        private void EndArrangement(int day, bool cleared, int left)
        {
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                EndHold(_file.Loans[i], LoanHold.Frozen, "the arrangement ended");
            }
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                _file.Accounts[i].Frozen = false;
            }
            ReliefRules.EndArrangement(_file.Relief, day);
            Note(cleared ? NotificationType.Success : NotificationType.Error, cleared ? "reallife_econ_note_arranged_done" : "reallife_econ_note_arranged_broken",
                Data("amount", FinanceText.Money(left), "days", ProductCatalogue.ArrangementCapDaysAfter.ToString(), "cap", ProductCatalogue.ArrangementScoreCap.ToString()), null, 16f);
            if (Verbose)
            {
                Logger?.Info("Economy check (debt arrangement, day " + day + "): ended, " + (cleared ? "everything in it paid" : left + " still owed and running again as before") + "; "
                             + _file.Relief.ArrangedPaid + " was paid under it; the rating stays held for " + ProductCatalogue.ArrangementCapDaysAfter + " days: PASS");
            }
            _director.TermsChanged("the arrangement ended");
        }

        /// <summary>
        /// What bankruptcy would do: every debt but a mortgage with its building, fines and court debts is written off, a vehicle
        /// on finance goes back, and cash above the sum kept goes to the creditors.
        /// </summary>
        public ProductAnswer BankruptcyOffer(out BankruptcyPlan plan)
        {
            plan = default(BankruptcyPlan);
            if (_file == null)
            {
                return ProductAnswer.Off;
            }
            if (GameBankruptcy)
            {
                return ProductAnswer.Busy;
            }
            if (QuestLoan)
            {
                return ProductAnswer.Off;
            }
            int day = Today;
            if (!ReliefRules.CanBankrupt(_file.Relief, day))
            {
                return ProductAnswer.Bankrupt;
            }
            int loans, unsecured, secured, shark, other, mortgages = 0;
            Owed(out loans, out unsecured, out secured, out shark, out other);
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                mortgages += !a.Closed && !a.Test && a.Kind == ProductKind.Mortgage && a.Secured ? a.Owed : 0;
            }
            plan = ReliefRules.Bankruptcy(loans + unsecured + secured - mortgages + shark, mortgages + other, _wallet.Cash, day);
            return plan.WrittenOff <= 0 ? ProductAnswer.NothingOwed : ProductAnswer.Yes;
        }

        public ProductAnswer Bankrupt()
        {
            if (_file != null && _file.Relief.HasApproval && !GameBankruptcy && ReliefRules.CanBankrupt(_file.Relief, Today))
            {
                CancelApproval(false);   // money that is the lender's goes back before anything is counted
            }
            BankruptcyPlan plan;
            ProductAnswer answer = BankruptcyOffer(out plan);
            if (answer != ProductAnswer.Yes)
            {
                return answer;
            }
            int day = Today, before = CreditLedger.Value;
            Relief r = _file.Relief;
            if (r.Arranged)
            {
                ReliefRules.EndArrangement(r, day);
            }
            for (int i = _file.Loans.Count - 1; i >= 0; i--)
            {
                var loan = _file.Loans[i].Ref as Loan;
                if (loan != null)
                {
                    RemoveLoan(_file.Loans[i], loan);
                }
            }
            Save.Loans.Clear();
            _file.Loans.Clear();
            int taken = 0;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed || a.Test || (a.Kind == ProductKind.Mortgage && a.Secured))
                {
                    a.Frozen = false;
                    continue;   // a mortgage stays with its building, on its payments
                }
                a.Frozen = false;
                a.Owed = 0;
                a.Ladder.Clear();
                if (a.Kind == ProductKind.CarFinance && a.Secured && Books.Vehicle(a.Security) != null)
                {
                    // the vehicle goes back and what it fetches is the creditors'; the account waits, with nothing owed, until it is taken
                    a.SecurityValue = 0;
                    a.Ladder.RepoPending = true;
                    _repoWaiting = true;
                    taken++;
                    continue;
                }
                Closed(a);
            }
            for (int i = 0; i < r.Claims.Count; i++)
            {
                EconomyBridges.WithdrawClaim?.Invoke(r.Claims[i]);
            }
            r.Claims.Clear();
            int shark = _file.Shark.Active ? _file.Shark.Owed : 0;
            bool known = _file.Shark.Known;
            _file.Shark.Clear();
            _file.Shark.Known = known;
            _wallet.Force(plan.CashTaken, Money.Bankruptcy, string.Empty);
            r.BankruptDay = day;
            r.OverdueSince = 0;
            r.AdviceTold = false;
            CreditLedger.Touch();
            CreditLedger.Post(CreditKind.Bankruptcy, plan.WrittenOff, day, "advice");
            Note(NotificationType.Error, "reallife_econ_note_bankrupt", Data("amount", FinanceText.Money(plan.WrittenOff), "cash", FinanceText.Money(plan.CashTaken),
                "days", ProductCatalogue.BankruptNoCreditDays.ToString(), "score", CreditLedger.Value.ToString()), null, 20f);
            if (Verbose)
            {
                Logger?.Info("Economy check (bankruptcy, day " + day + "): before " + plan.WrittenOff + " owed that goes (" + shark + " of it to the private lender), " + plan.Kept + " that stays, "
                             + plan.CashNow
                             + " in cash; after " + plan.Kept + " owed, " + plan.CashLeft + " in cash, " + taken + " vehicle(s) to go back; rating " + before + " to " + CreditLedger.Value
                                 + ", nobody lends until day "
                             + plan.NoCreditUntil + ", the next bankruptcy not before day " + plan.NextAllowedDay + ": "
                                 + (CreditLedger.Value == ProductCatalogue.BankruptScore && Save.Loans.Count == 0 ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a bankruptcy");
            return ProductAnswer.Yes;
        }
    }
}
