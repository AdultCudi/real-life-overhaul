using System.Collections.Generic;
using System.Globalization;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The credit desk's day: each payment, each debt's next step, and the takings and rents that go to a lender.</summary>
    public sealed partial class CreditDesk
    {
        /// <summary>The game's day, after its run and the director's reading of it, in a fixed order, so what cash is spent on first never changes.</summary>
        public void Day(int day, List<LoanFinding> findings)
        {
            if (_file == null)
            {
                return;
            }
            int level = Level;
            bool consequences = Settings.EconomyConsequences;
            _claimedToday = false;
            _wallet.BookDay = day - 1;   // on the day that has ended, as the game books its own run
            try
            {
                if (_file.Relief.Arranged)
                {
                    ArrangementDay(day);
                }
                LoansDay(day, findings, level, consequences);
                AccountsDay(day, level, consequences);
                Diversions(day);
                SharkDay(day, level);
                ReliefDay(day);
            }
            finally
            {
                _wallet.BookDay = null;
            }
            _file.Trim();
            CreditLedger.Touch();
        }

        // A line on the credit file about a debt; a test account's is a test entry.
        private static void Post(Debt debt, CreditKind kind, int amount, int day)
        {
            CreditLedger.Post(kind, amount, day, debt.Key, 1, debt.Account != null && debt.Account.Test);
        }

        // The game's loans: a payment made puts a loan back in order (and takes its plan's part); one missed takes the next step.
        private void LoansDay(int day, List<LoanFinding> findings, int level, bool consequences)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                LoanFinding f = findings[i];
                LoanMark m = f.Live;
                var loan = m != null ? m.Ref as Loan : null;
                if (loan == null || (m.Held & LoanHold.Frozen) != 0 || (f.Note != LoanNote.Paid && f.Note != LoanNote.Missed))
                {
                    continue;
                }
                var debt = new Debt(m);
                if (f.Note == LoanNote.Missed)
                {
                    if (consequences)
                    {
                        Step(debt, LadderRules.Missed(m.Ladder, level, SecuredNow(m), m.Ladder.Overdue + f.Amount), f.Amount, 0, day, level);
                    }
                    continue;
                }
                string name = debt.Name;   // now: a loan the plan's last part pays off leaves the game's list
                int part = Mathf.Min(Mathf.Min(m.Ladder.PlanPerDay, m.Ladder.PlanLeft), debt.Owed);
                if (m.Ladder.PlanLeft > 0 && part > 0 && !_wallet.Take(part, Money.Payment, name))
                {
                    if (consequences)
                    {
                        Step(debt, LadderRules.Missed(m.Ladder, level, SecuredNow(m), m.Ladder.Overdue), part, 0, day, level);   // the plan is broken
                    }
                    continue;
                }
                if (m.Ladder.PlanLeft > 0)
                {
                    m.Ladder.PlanLeft = Mathf.Max(0, m.Ladder.PlanLeft - part);
                    m.Ladder.Overdue = Mathf.Max(0, m.Ladder.Overdue - part);
                    PayDown(m, loan, part, day);
                }
                if (LadderRules.Paid(m.Ladder))
                {
                    EndHold(m, LoanHold.Penalty, "three payments in a row");
                    Note(NotificationType.Success, "reallife_econ_note_penalty_over", Data("account", name));
                }
            }
        }

        private static bool SecuredNow(LoanMark m)
        {
            return m.Purpose == LoanPurpose.Business && m.Security.Length > 0 && m.Ladder.RepoDay == 0 && Books.Business(m.Security) != null;
        }

        // Money off one of the game's loans outside its run; the file's reading moves with it, so it is not taken for an early payment.
        private void PayDown(LoanMark m, Loan loan, int amount, int day)
        {
            if (amount <= 0)
            {
                return;
            }
            loan.remainingAmount = Mathf.Max(0f, loan.remainingAmount - amount);
            m.Remaining = loan.remainingAmount;
            if (loan.remainingAmount <= 0.5f)
            {
                RemoveLoan(m, loan);
                CreditLedger.Post(CreditKind.PaidOff, (int)m.Total, day, GameLoans.AccountKey);
            }
        }

        // One of the game's loans leaves the game's list for good; nothing else of the game's refers to a loan.
        private void RemoveLoan(LoanMark m, Loan loan)
        {
            if (Save != null)
            {
                Save.Loans.Remove(loan);
            }
            _file.Loans.Remove(m);
            m.Held = LoanHold.None;
            m.Ref = null;
        }

        private void AccountsDay(int day, int level, bool consequences)
        {
            int payDay = day - 1, paid = 0, paidAmount = 0, missed = 0, missedAmount = 0;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed || a.Test || (a.Owed == 0 && a.Ladder.RepoPending))
                {
                    continue;   // a test account moves only by a test button; a vehicle written off in a bankruptcy only waits to be taken
                }
                int before = a.Owed;
                string name = Name(a);
                DayOutcome o = AccountRules.RunDay(a, day, _wallet, level, consequences, name);
                if (o.WasDue && o.Missed > 0)
                {
                    missed++;
                    missedAmount += o.Missed;
                }
                else if (o.OnTime)
                {
                    paid++;   // a card's minimum paid by hand before its day counts as well, with nothing taken today
                    paidAmount += o.Paid;
                }
                if (Verbose && (o.WasDue || o.Paid > 0 || o.Interest > 0 || o.Statement))
                {
                    Logger?.Info("Economy check (account, day " + day + "): " + a.Kind + " #" + a.Id + ": " + before + " owed, interest " + o.Interest + ", "
                                 + (o.Missed > 0 ? "a payment of " + o.Missed + " missed (" + (consequences ? a.Ladder.Misses + " in a row" : "consequences are off") + ")" : "paid " + o.Paid)
                                 + (o.Fee > 0 ? ", late fee " + o.Fee : string.Empty) + (o.Statement ? ", statement of " + a.StatementLeft + " with a minimum of " + a.MinimumLeft + " due day "
                                    + a.DueDay : string.Empty)
                                 + ", now " + a.Owed + " owed" + (a.Closed ? ", paid off" : string.Empty) + ": " + (a.Owed == Expected(a, before, o) ? "PASS" : "FAIL"));
                }
                if (o.PenaltyEnded)
                {
                    Note(NotificationType.Success, "reallife_econ_note_penalty_over", Data("account", name));
                    Check("rate restored: " + a.Kind + " #" + a.Id + " back at " + (a.RateNow / 100f).ToString("0.##", CultureInfo.InvariantCulture)
                        + "% after three payments in a row", a.RateNow == a.RateAgreedNow);
                }
                if (o.Step != LadderStep.None)
                {
                    Step(new Debt(a), o.Step, o.Missed, o.Fee, day, level);
                }
                if (o.Statement)
                {
                    Note(NotificationType.Info, "reallife_econ_note_statement", Data("amount", FinanceText.Money(a.StatementLeft), "minimum", FinanceText.Money(a.MinimumLeft), "day",
                         a.DueDay.ToString()));
                }
                if (o.Closed)
                {
                    CreditLedger.Unregister(a);
                    CreditLedger.Post(CreditKind.PaidOff, a.Agreed, payDay, a.Key);
                    Note(NotificationType.Success, "reallife_econ_note_paid_off", Data("account", name));
                }
                else if (consequences && !a.Secured && LadderRules.UnsecuredClaimDue(a.Ladder, a.Owed, day, level))
                {
                    Claim(new Debt(a), day);
                }
            }
            if (paid > 0)
            {
                CreditLedger.Post(CreditKind.PaidOnTime, paidAmount, payDay, AccountsSource, paid);
            }
            if (missed > 0)
            {
                int effect = CreditLedger.Post(CreditKind.Missed, missedAmount, payDay, AccountsSource, missed);
                Note(NotificationType.Error, "reallife_econ_note_missed_account", Data("amount", FinanceText.Money(missedAmount), "points", Mathf.Abs(effect).ToString(),
                    "score", CreditLedger.Value.ToString(), "grade", FinanceText.Grade(CreditLedger.Grade)));
            }
        }

        // What an account should owe after its day: an instalment account's interest goes on the balance only when the payment is missed.
        private static int Expected(CreditAccount a, int before, in DayOutcome o)
        {
            bool withPayment = ProductCatalogue.Instalment(a.Kind) && o.Missed == 0;
            return before + (withPayment ? 0 : o.Interest) + o.Fee - (o.Paid - (withPayment ? o.Interest : 0));
        }

        private void Step(Debt debt, LadderStep step, int missed, int feeAdded, int day, int level)
        {
            if (step == LadderStep.None)
            {
                return;
            }
            string name = debt.Name, lender = debt.Lender;
            LadderState s = debt.Ladder;
            string detail = string.Empty;
            switch (step)
            {
                case LadderStep.Reminder:
                    break;   // the notice of the missed payment is the reminder
                case LadderStep.LateFee:
                    {
                        int fee = feeAdded;
                        if (debt.Account == null)
                        {
                            // one of the game's loans: the fee is taken from cash if it is there, else it waits on an account of its own
                            fee = AccountRules.LateFee(missed, level);
                            if (!_wallet.Take(fee, Money.LateFee, name))
                            {
                                Arrears(debt.Mark, fee, day);
                                detail = " (not in cash: added to the late fees owed)";
                            }
                        }
                        Post(debt, CreditKind.LateFee, fee, day);
                        Note(NotificationType.Warning, "reallife_econ_note_late_fee", Data("lender", lender, "account", name, "amount", FinanceText.Money(fee)));
                        detail = " of " + fee + detail;
                    }
                    break;
                case LadderStep.PenaltyRate:
                    if (debt.Account == null && debt.Loan != null && (debt.Mark.Held & LoanHold.Penalty) == 0)
                    {
                        BeginHold(debt.Mark, debt.Loan, LoanHold.Penalty, "the penalty rate");
                    }
                    Post(debt, CreditKind.PenaltyRate, 0, day);
                    Note(NotificationType.Warning, "reallife_econ_note_penalty", Data("lender", lender, "account", name, "points", ProductCatalogue.PenaltyPoints.ToString(), "run",
                         ProductCatalogue.PenaltyClearRun.ToString()));
                    break;
                case LadderStep.Collections:
                    if (debt.Account == null && debt.Loan != null && (debt.Mark.Held & LoanHold.Penalty) == 0 && s.Penalty)
                    {
                        BeginHold(debt.Mark, debt.Loan, LoanHold.Penalty, "the penalty rate");   // a count that passed the third miss by
                    }
                    _file.Relief.CollectorKnown = true;
                    DebtPhone.Setup(this);
                    Post(debt, CreditKind.Collections, s.Overdue, day);
                    Note(NotificationType.Error, "reallife_econ_note_collections", Data("lender", lender, "account", name, "amount", FinanceText.Money(s.Overdue)), _openCollections, 16f);
                    break;
                case LadderStep.Repossession:
                    Repossession(debt, day);
                    break;
                case LadderStep.Claim:
                    Claim(debt, day);
                    break;
            }
            if (Verbose)
            {
                bool secured = debt.Account != null ? debt.Account.Secured : debt.Mark.Security.Length > 0;
                Logger?.Info("Economy check (step, day " + day + "): " + (debt.Account != null ? debt.Account.Kind + " #" + debt.Account.Id : "the loan at " + debt.Mark.Street + " "
                             + debt.Mark.Number)
                             + ": " + s.Misses + " payment(s) missed in a row on level " + level + " (" + (secured ? "secured" : "unsecured") + "), " + s.Overdue + " overdue: " + step + detail + ": "
                             + (LadderRules.Takes(step, level, true) ? "PASS" : "FAIL"));
            }
        }

        // Late fees on one of the game's loans that were not in cash: one account holds them all, with the loan's bank.
        private void Arrears(LoanMark m, int fee, int day)
        {
            CreditAccount a = _file.Account(ProductKind.Arrears);
            if (a == null)
            {
                a = AccountRules.Open(ProductKind.Arrears, 0, 0, day);
                a.Street = m.Street;
                a.Number = m.Number;
                Opened(a);
            }
            a.Owed = WholeMoney.Add(a.Owed, fee);
            a.Agreed = Mathf.Max(a.Agreed, a.Owed);
        }

        // A vehicle is taken once it stands out of sight; a business's takings or a building's rents go to the lender until the arrears are cleared.
        private void Repossession(Debt debt, int day)
        {
            LadderState s = debt.Ladder;
            if (debt.Account != null && debt.Account.Kind == ProductKind.CarFinance)
            {
                s.RepoPending = true;
                _repoWaiting = true;
                Note(NotificationType.Error, "reallife_econ_note_repo_due", Data("lender", debt.Lender, "thing", Thing(debt.Account)), null, 16f);
                return;
            }
            s.RepoDay = day;
            Post(debt, CreditKind.Repossessed, s.Overdue, day);
            if (debt.Account != null)
            {
                Note(NotificationType.Error, "reallife_econ_note_rents_taken", Data("lender", debt.Lender, "thing", Thing(debt.Account), "amount", FinanceText.Money(s.Overdue)), null, 16f);
                return;
            }
            BuildingRegistration shop = Books.Business(debt.Mark.Security);
            Note(NotificationType.Error, "reallife_econ_note_takings_taken", Data("lender", debt.Lender, "business", shop != null ? shop.BusinessName : string.Empty,
                "share", Mathf.RoundToInt(ProductCatalogue.BusinessDivertShare * 100f) + "%", "amount", FinanceText.Money(s.Overdue)), null, 16f);
        }

        // The lender takes the balance and its costs to court, and the debt is the court's from then on.
        private void Claim(Debt debt, int day)
        {
            int owed = debt.Owed;
            if (owed <= 0 || debt.Ladder.ClaimNumber != 0)
            {
                return;
            }
            int costs = LadderRules.ClaimCosts(owed);
            string name = debt.Name, lender = debt.Lender, key = debt.Key;
            if (debt.Account != null && debt.Account.Test)
            {
                Logger?.Info("Economy check (claim, day " + day + "): " + name + ": a claim for " + owed + " and " + costs
                             + " in costs would be filed here; nothing is filed for a test account: PASS");
                return;
            }
            int number = EconomyBridges.FileClaim != null ? EconomyBridges.FileClaim(owed + costs) : 0;
            if (number <= 0)
            {
                if (Verbose)
                {
                    Logger?.Info("Economy check (claim, day " + day + "): " + name + ": " + owed
                                 + " would go to court, but there is no court to file with (its feature is off): the debt stays as it is: PASS");
                }
                return;
            }
            debt.Ladder.ClaimNumber = number;
            if (_file.Relief.Claims.Count < Relief.ClaimsMost)
            {
                _file.Relief.Claims.Add(number);
            }
            _claimedToday = true;
            if (debt.Account != null)
            {
                debt.Account.Owed = 0;
                AccountRules.Unsecure(debt.Account);
                Closed(debt.Account);
            }
            else
            {
                RemoveLoan(debt.Mark, debt.Loan);
            }
            CreditLedger.Post(CreditKind.ClaimFiled, owed + costs, day, key);
            Note(NotificationType.Error, "reallife_econ_note_claim", Data("lender", lender, "account", name, "amount", FinanceText.Money(owed + costs), "costs", FinanceText.Money(costs)), null, 18f);
            if (Verbose)
            {
                Logger?.Info("Economy check (claim, day " + day + "): " + name + ": " + owed + " and " + costs + " in costs filed as civil claim " + number
                             + "; the debt is closed here and is the court's now: PASS");
            }
        }

        // Takings and rents that go to a lender after a repossession step.
        private void Diversions(int day)
        {
            for (int i = _file.Loans.Count - 1; i >= 0; i--)
            {
                LoanMark m = _file.Loans[i];
                var loan = m.Ref as Loan;
                if (m.Ladder.RepoDay <= 0 || loan == null || (m.Held & LoanHold.Frozen) != 0)
                {
                    continue;
                }
                BuildingRegistration shop = Books.Business(m.Security);
                if (shop == null || m.Ladder.Overdue <= 0)
                {
                    m.Ladder.RepoDay = 0;   // the arrears are cleared, or the business has gone
                    continue;
                }
                int want = Mathf.Min(Mathf.Min(Mathf.FloorToInt(Books.SalesYesterday(shop.Address) * ProductCatalogue.BusinessDivertShare), m.Ladder.Overdue), Mathf.CeilToInt(loan.remainingAmount));
                int got = _wallet.TakeUpTo(want, Money.Diverted, shop.BusinessName);
                m.Ladder.Overdue -= got;
                PayDown(m, loan, got, day);
                if (Verbose)
                {
                    Logger?.Info("Economy check (takings, day " + day + "): " + shop.BusinessName + " took " + Books.SalesYesterday(shop.Address).ToString("0") + " yesterday: " + got + " of " + want
                                 + " went to the lender, " + m.Ladder.Overdue + " still overdue: " + (got <= want ? "PASS" : "FAIL"));
                }
                if (m.Ladder.Overdue <= 0)
                {
                    m.Ladder.RepoDay = 0;
                    if (LadderRules.Paid(m.Ladder))
                    {
                        EndHold(m, LoanHold.Penalty, "three payments in a row");
                    }
                    Note(NotificationType.Success, "reallife_econ_note_takings_over", Data("business", shop.BusinessName));
                }
            }
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed || a.Kind != ProductKind.Mortgage || a.Ladder.RepoDay <= 0)
                {
                    continue;
                }
                Entities.RealEstate estate = Books.Estate(a.Security);
                if (estate == null || a.Ladder.Overdue <= 0)
                {
                    a.Ladder.RepoDay = 0;
                    continue;
                }
                int want = Mathf.Min(Mathf.Min(Mathf.FloorToInt(estate.DailyIncome * ProductCatalogue.RentDivertShare), a.Ladder.Overdue), a.Owed);
                int got = _wallet.TakeUpTo(want, Money.Diverted, Thing(a));
                AccountRules.Pay(a, got);
                if (a.Ladder.Overdue <= 0)
                {
                    a.Ladder.RepoDay = 0;
                    Note(NotificationType.Success, "reallife_econ_note_rents_over", Data("thing", Thing(a)));
                }
                if (a.Closed)
                {
                    CreditLedger.Unregister(a);
                }
            }
        }
    }
}
