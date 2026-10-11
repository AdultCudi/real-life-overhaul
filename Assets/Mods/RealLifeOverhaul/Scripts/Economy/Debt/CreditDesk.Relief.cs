using System.Collections.Generic;
using Helpers;
using Tutorial.SideQuests;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    // The credit desk's other half: the collections call, the lender of last resort, debt advice, a debt arrangement
    // and bankruptcy. The conversations themselves are Economy/Debt/DebtPhone.cs; every decision in them is made here.
    public sealed partial class CreditDesk
    {
        private const string GameBankruptcyQuest = "SQBankruptcy";   // the game's own side quest: SideQuestHelper.BankruptcyQuestId

        private static float Income => Mathf.Max(0f, CreditLedger.Income != null ? CreditLedger.Income() : 0f);

        // ---------------------------------------------------------------- the collections call

        /// <summary>The debt the collections call is about: the first one whose call has not been answered. False if there is none.</summary>
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

        /// <summary>"Pay what is overdue now": the overdue money is taken, comes off the debt, and the debt is in order again.</summary>
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
                return ProductAnswer.NoCash;   // (a test account takes no money)
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
            Touch();
            if (Verbose)
            {
                Logger?.Info("Economy check (collections): " + name + ": " + amount + " overdue paid at once; the debt is in order again: " + (s.Misses == 0 && s.Overdue == 0 ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("what was overdue was paid");
            return ProductAnswer.Yes;
        }

        /// <summary>"Agree a payment plan": what is overdue over the plan's days on top of the normal payment. One plan per debt.</summary>
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

        // ---------------------------------------------------------------- the lender of last resort

        public SharkDebt Shark => _file != null ? _file.Shark : null;

        public int SharkMost => SharkRules.Most(Income);

        /// <summary>A bank has just refused the player, or the rating has gone under the threshold: the private lender makes itself known, once.</summary>
        public void Refused()
        {
            if (_file == null || !Settings.EconomyShark || _file.Shark.Known)
            {
                return;
            }
            _file.Shark.Known = true;
            DebtPhone.Setup(this);
            DebtPhone.Text(DebtPhone.Lender, "reallife_text_shark_intro", Data("least", FinanceText.Money(ProductCatalogue.SharkLeast), "most", FinanceText.Money(SharkMost)), _openLender);
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender): made itself known by a text at rating " + CreditLedger.Value + " (lenders want " + CreditCatalogue.LendingThreshold + "): PASS");
            }
        }

        public ProductAnswer SharkBorrow(int amount)
        {
            if (_file == null || !Settings.EconomyShark || !_file.Shark.Known)
            {
                return ProductAnswer.Off;
            }
            SharkDebt debt = _file.Shark;
            ProductAnswer answer = SharkRules.CanLend(debt, amount, Income);
            if (answer != ProductAnswer.Yes)
            {
                return answer;
            }
            int day = Today;
            SharkRules.Lend(debt, amount, day);
            _wallet.Give(amount, Money.SharkLent, string.Empty);
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender): " + amount + " lent on day " + day + ", " + SharkRules.ADay(amount) + " a day on top, " + SharkRules.OwedOnDueDay(amount) + " due on day " + debt.DueDay
                             + ", never more than " + SharkRules.Cap(amount) + "; nothing goes on the credit file: PASS");
            }
            return ProductAnswer.Yes;
        }

        public ProductAnswer SharkPay(int amount, out int used)
        {
            used = 0;
            if (_file == null || !_file.Shark.Active)
            {
                return ProductAnswer.NothingOwed;
            }
            SharkDebt debt = _file.Shark;
            SharkRules.Grow(debt, Today);
            amount = Mathf.Min(amount, debt.Owed);
            if (amount <= 0 || !_wallet.Take(amount, Money.SharkPaid, string.Empty))
            {
                return ProductAnswer.NoCash;
            }
            used = SharkRules.Pay(debt, amount);
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender): " + used + " paid, " + debt.Owed + " left" + (debt.Active ? string.Empty : ": it is over, and nothing went on the credit file") + ": PASS");
            }
            return ProductAnswer.Yes;
        }

        public ProductAnswer SharkExtend(out int fee)
        {
            fee = 0;
            if (_file == null || !_file.Shark.Active)
            {
                return ProductAnswer.NothingOwed;
            }
            SharkDebt debt = _file.Shark;
            int day = Today;
            SharkRules.Grow(debt, day);
            fee = SharkRules.ExtendFee(debt);
            if (!SharkRules.Extend(debt, day))
            {
                return ProductAnswer.AlreadyOpen;
            }
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender): extended once for a fee of " + fee + "; " + debt.Owed + " owed, due on day " + debt.DueDay + ": PASS");
            }
            return ProductAnswer.Yes;
        }

        // Each day: the lender shows itself if nobody else lends; the debt grows; past its day, today's act is decided.
        private void SharkDay(int day, int level)
        {
            SharkDebt debt = _file.Shark;
            if (!debt.Known && !CreditLedger.Score.Lends)
            {
                Refused();
            }
            if (!debt.Active)
            {
                return;
            }
            if (!Settings.EconomyShark)
            {
                debt.GrownDay = day;   // switched off: the debt stands as it is and can be paid; it does not grow and nothing is done about it
                return;
            }
            int added = SharkRules.Grow(debt, day);
            if (day == debt.DueDay)
            {
                DebtPhone.Text(DebtPhone.Lender, "reallife_text_shark_due", Data("amount", FinanceText.Money(debt.Owed)), _openLender);
            }
            SharkAct act = SharkRules.Today(debt, day, level > ProductCatalogue.Relaxed);
            if (act == SharkAct.Warn)
            {
                DebtPhone.Text(DebtPhone.Lender, "reallife_text_shark_warn", Data("amount", FinanceText.Money(debt.Owed), "days", (day - debt.DueDay).ToString()), _openLender);
            }
            else if (act != SharkAct.None)
            {
                debt.Waiting = act;
            }
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender, day " + day + "): " + added + " added, " + debt.Owed + " owed of at most " + SharkRules.Cap(debt.Borrowed) + ", due day " + debt.DueDay
                             + (day > debt.DueDay ? ", " + (day - debt.DueDay) + " day(s) past: " + act + (level > ProductCatalogue.Relaxed ? string.Empty : " (no violence on Relaxed)") : string.Empty)
                             + ": " + (debt.Owed <= SharkRules.Cap(debt.Borrowed) ? "PASS" : "FAIL"));
            }
        }

        // Each hour: an act that is waiting is carried out when nothing has hold of the player.
        private void SharkHour(int day)
        {
            SharkDebt debt = _file.Shark;
            if (debt.Waiting == SharkAct.None)
            {
                return;
            }
            if (!debt.Active || !Settings.EconomyShark || Level <= ProductCatalogue.Relaxed)
            {
                debt.Waiting = SharkAct.None;
                return;
            }
            if (EconomyBridges.Held != null && EconomyBridges.Held())
            {
                return;   // in custody, in hospital, on a trip, in a scene: not now
            }
            if (debt.Waiting == SharkAct.Assault)
            {
                if (PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding)
                {
                    return;   // only in the street, on foot
                }
                int taken = _wallet.TakeUpTo(debt.Owed, Money.SharkTaken, string.Empty);
                bool hurt = EconomyBridges.Assault != null && EconomyBridges.Assault(ProductCatalogue.SharkInjuryHours, ProductCatalogue.SharkInjuryEnergy, "reallife_econ_note_shark_assault");
                SharkRules.Pay(debt, taken);
                debt.Waiting = SharkAct.None;
                Notifications.Show(NotificationType.Error, "reallife_econ_note_shark_taken", Data("amount", FinanceText.Money(taken), "left", FinanceText.Money(debt.Owed)), 14f, null, _openLender, true, false);
                if (Verbose)
                {
                    Logger?.Info("Economy check (private lender, day " + day + "): the borrower was set upon in the street (" + (hurt ? "hurt as a mugging hurts" : "no injury could be given") + "); "
                                 + taken + " in cash taken against the debt, " + debt.Owed + " left: PASS");
                }
                return;
            }
            Books.Businesses(_shops);
            if (_shops.Count == 0)
            {
                debt.Waiting = SharkAct.None;
                DebtPhone.Text(DebtPhone.Lender, "reallife_text_shark_warn", Data("amount", FinanceText.Money(debt.Owed), "days", (day - debt.DueDay).ToString()), _openLender);
                return;
            }
            BuildingRegistration shop = _shops[day % _shops.Count];
            _shops.Clear();
            int bill = SharkRules.DamageBill(debt.Owed);
            _wallet.Force(bill, Money.SharkDamage, shop.BusinessName);
            debt.Waiting = SharkAct.None;
            Notifications.Show(NotificationType.Error, "reallife_econ_note_shark_damage", Data("business", shop.BusinessName, "amount", FinanceText.Money(bill)), 14f, null, _openLender, true, false);
            if (Verbose)
            {
                Logger?.Info("Economy check (private lender, day " + day + "): " + shop.BusinessName + " was damaged: a repair bill of " + bill + " (" + debt.Owed + " still owed): PASS");
            }
        }

        // ---------------------------------------------------------------- debt advice and the ways out

        /// <summary>True while the game's own bankruptcy quest is on: the mod's bankruptcy is not offered, and the player is pointed to the game's.</summary>
        public static bool GameBankruptcy => SideQuestHelper.IsSideQuestActive(GameBankruptcyQuest);

        // Whether a debt is one an arrangement takes: everything that is not secured on something the borrower still has. (Never a test account.)
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

        private readonly List<DebtLine> _lines = new List<DebtLine>(6);

        // Each day: how long something has been overdue, whether a claim is still open, and debt advice when it is due.
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
                Logger?.Info("Economy check (debt advice, day " + day + "): offered; something has been overdue since day " + r.OverdueSince + ", " + r.Claims.Count + " claim(s) before the court: PASS");
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

        // A debt that goes into an arrangement or is written off: nothing is waiting on it any more.
        private static void Quiet(LadderState s)
        {
            s.CallDue = false;
            s.PlanLeft = 0;
            s.PlanPerDay = 0;
            s.Misses = 0;
            s.Overdue = 0;
            s.RepoDay = s.RepoPending ? s.RepoDay : 0;
        }

        // The arrangement's day: the one payment, shared out over the frozen debts oldest first. Three missed and it is over.
        private void ArrangementDay(int day)
        {
            Relief r = _file.Relief;
            int left = 0;
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                left += (m.Held & LoanHold.Frozen) != 0 && m.Ref is Loan ? Mathf.CeilToInt(((Loan)m.Ref).remainingAmount) : 0;
            }
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                left += _file.Accounts[i].Frozen && !_file.Accounts[i].Closed ? _file.Accounts[i].Owed : 0;
            }
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
                    Note(NotificationType.Warning, "reallife_econ_note_arranged_missed", Data("amount", FinanceText.Money(due), "count", r.ArrangedMisses.ToString(), "most", ProductCatalogue.ArrangementMissesMost.ToString()));
                }
                return;
            }
            int share = due;
            for (int i = _file.Loans.Count - 1; i >= 0 && share > 0; i--)
            {
                // (the game keeps its loans oldest first and the file's marks in the same order; counted down so that removing one is safe,
                // which pays the newest first: the order is fixed, and that is what matters to the borrower)
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

        // The arrangement ends: cleared, or broken. What is still owed runs again as it did; the rating stays held for the stated days.
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
        /// What bankruptcy would do: every loan, every account but a mortgage that still has its building, and the
        /// private lender's debt are written off; a vehicle on finance goes back; cash above the stated sum goes to
        /// the creditors; fines and court debts stay.
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

        /// <summary>Does it.</summary>
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
                    // The vehicle goes back; what it fetches is the creditors'. The account stays, with nothing owed, until it has been taken.
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
            Touch();
            CreditLedger.Post(CreditKind.Bankruptcy, plan.WrittenOff, day, "advice");
            Note(NotificationType.Error, "reallife_econ_note_bankrupt", Data("amount", FinanceText.Money(plan.WrittenOff), "cash", FinanceText.Money(plan.CashTaken),
                "days", ProductCatalogue.BankruptNoCreditDays.ToString(), "score", CreditLedger.Value.ToString()), null, 20f);
            if (Verbose)
            {
                Logger?.Info("Economy check (bankruptcy, day " + day + "): before " + plan.WrittenOff + " owed that goes (" + shark + " of it to the private lender), " + plan.Kept + " that stays, " + plan.CashNow
                             + " in cash; after " + plan.Kept + " owed, " + plan.CashLeft + " in cash, " + taken + " vehicle(s) to go back; rating " + before + " to " + CreditLedger.Value + ", nobody lends until day "
                             + plan.NoCreditUntil + ", the next bankruptcy not before day " + plan.NextAllowedDay + ": " + (CreditLedger.Value == ProductCatalogue.BankruptScore && Save.Loans.Count == 0 ? "PASS" : "FAIL"));
            }
            _director.TermsChanged("a bankruptcy");
            return ProductAnswer.Yes;
        }
    }
}
