using System;
using System.Collections.Generic;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UI.Smartphone.Apps.Persona;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Accounts tab, and the pieces of a debt's page that BankApp shares.</summary>
    public sealed partial class FinanceApp
    {
        private const string WaysRow = "ways", LenderRow = "lender", ApprovalRow = "approval";

        private string _picked;   // the id of the row picked

        private string WriteAccounts(InfoBox box)
        {
            CreditDesk desk = Desk;
            CreditFile file = CreditLedger.File;
            int today = SaveGameManager.Current.Day;
            string selected = null;
            CreditAccount picked = null;
            LoanMark pickedLoan = null;
            float owed = 0f;
            _spec = FinanceTables.Accounts;
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                CreditAccount a = file.Accounts[i];
                if (a.Closed)
                {
                    continue;
                }
                owed += a.Test ? 0 : a.Owed;
                TableRow row = Row("acct:" + a.Id);
                row.Pick = true;
                row.Kind = FinanceTables.Of(a.Kind);
                bool sound = a.Ladder.InOrder && !a.Ladder.Penalty;
                row.Set(0, FinanceText.T("reallife_tab_account_with", "account", CreditDesk.Name(a), "lender", CreditDesk.LenderName(a)));
                row.Set(1, FinanceText.T(FinanceTables.DebtKinds[row.Kind]));
                row.Set(2, FinanceText.Money(a.Owed), a.Test ? double.NaN : a.Owed);   // a test tool's account is in no total
                if (a.RateBp > 0)
                {
                    row.Set(3, Rate(a.RateNow), a.RateNow / 100.0, a.Ladder.Penalty ? TableRow.Red : TableRow.Plain);
                }
                NextPayment(a, today, out int next, out int day);
                PaymentCells(row, next, day, today, !a.Test);
                row.Set(6, FinanceText.State(a), Gravity(a.Ladder, a.Frozen), sound ? TableRow.Plain : TableRow.Red);
                if (row.Id == _picked)
                {
                    picked = a;
                    selected = row.Id;
                }
            }
            int perYear = Mathf.Max(1, AccountRules.DaysPerYear);
            for (int i = 0; i < file.Loans.Count; i++)
            {
                LoanMark m = file.Loans[i];
                var loan = m.Ref as Loan;
                if (loan == null)
                {
                    continue;
                }
                owed += loan.remainingAmount;
                TableRow row = Row(LoanRowId(m));
                row.Pick = true;
                row.Kind = FinanceTables.OfLoan(m.Purpose);
                bool frozen = (m.Held & LoanHold.Frozen) != 0, sound = m.Ladder.InOrder && !m.Ladder.Penalty;
                string kind = FinanceText.T(FinanceTables.DebtKinds[row.Kind]);
                row.Set(0, FinanceText.T("reallife_tab_account_with", "account", kind, "lender", GameLoans.BankName(loan)));
                row.Set(1, kind);
                row.Set(2, FinanceText.Money(loan.remainingAmount), loan.remainingAmount);
                float yearly = YearlyRate(loan, perYear);
                if (yearly > 0f)
                {
                    row.Set(3, FinanceText.T("reallife_econ_row_rate", "rate", yearly.ToString("0.#")), yearly, (m.Held & LoanHold.Penalty) != 0 ? TableRow.Red : TableRow.Plain);
                }
                PaymentCells(row, frozen ? 0 : Mathf.RoundToInt(GameLoans.Due(loan)), today + 1, today);
                row.Set(6, FinanceText.State(m.Ladder, frozen, false), Gravity(m.Ladder, frozen), sound ? TableRow.Plain : TableRow.Red);
                if (row.Id == _picked)
                {
                    pickedLoan = m;
                    selected = row.Id;
                }
            }
            if (file.Shark.Active)
            {
                owed += file.Shark.Owed;
                TableRow row = Row(LenderRow);
                row.Pick = true;
                row.Kind = FinanceTables.Private;
                int late = today - file.Shark.DueDay;
                string kind = FinanceText.T("reallife_econ_kind_private");
                row.Set(0, FinanceText.T("reallife_tab_account_with", "account", kind, "lender", FinanceText.T("reallife-lender")));
                row.Set(1, kind);
                row.Set(2, FinanceText.Money(file.Shark.Owed), file.Shark.Owed);
                PaymentCells(row, file.Shark.Owed, file.Shark.DueDay, today);   // the whole sum falls due on one day
                row.Set(6, late > 0 ? FinanceText.T("reallife_econ_row_late", "days", late.ToString()) : FinanceText.T("reallife_econ_state_ok"), Mathf.Max(0, late),
                    late > 0 ? TableRow.Red : TableRow.Plain);
                selected = row.Id == _picked ? row.Id : selected;
            }
            if (file.Relief.HasApproval)
            {
                // Money promised, not owed: listed with its last day, and in no total.
                TableRow row = Row(ApprovalRow);
                row.Pick = true;
                row.Kind = FinanceTables.Approval;
                int until = file.Relief.ApprovedDay + ProductCatalogue.ApprovalDays;
                string approval = FinanceText.T("reallife_econ_row_approval", "kind", FinanceText.T(ProductCatalogue.KindKeys[file.Relief.ApprovedKind]));
                row.Set(0, FinanceText.T("reallife_tab_account_with", "account", approval, "lender", BankName(file.Relief.ApprovedStreet, file.Relief.ApprovedNumber)));
                row.Set(1, FinanceText.T(FinanceTables.DebtKinds[FinanceTables.Approval]));
                row.Set(2, FinanceText.Money(file.Relief.ApprovedAmount));
                if (file.Relief.ApprovedRateBp > 0)
                {
                    row.Set(3, Rate(file.Relief.ApprovedRateBp), file.Relief.ApprovedRateBp / 100.0);
                }
                row.Set(5, DayWord(until, today), until);
                row.Set(6, FinanceText.T("reallife_econ_row_until_day", "day", until.ToString()));
                selected = row.Id == _picked ? row.Id : selected;
            }
            int claims = file.Relief.Claims.Count;
            if (owed > 0f || file.Relief.Arranged || claims > 0)
            {
                // The way to the ways out of debt, pinned at the head whatever the order and the filter.
                TableRow row = Row(WaysRow);
                row.Pick = true;
                row.Pinned = true;
                row.Set(0, FinanceText.T("reallife_econ_row_ways"));
                row.Set(6, file.Relief.Arranged ? FinanceText.T("reallife_econ_state_arranged")
                    : claims > 0 ? FinanceText.T("reallife_econ_state_claims", "count", claims.ToString()) : string.Empty);
                selected = row.Id == _picked ? row.Id : selected;
            }
            if (_rows.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_accounts"));
            }

            if (picked != null)
            {
                WriteAccount(box, desk, picked, today);
            }
            else if (pickedLoan != null)
            {
                WriteLoan(box, desk, pickedLoan);
            }
            else if (selected == LenderRow)
            {
                box.Title(FinanceText.T("reallife_econ_kind_private"));
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_econ_over_owed", FinanceText.Money(file.Shark.Owed), file.Shark.Owed > 0 ? CellTone.Red : CellTone.Plain);
                box.Line(FinanceText.T("reallife_econ_private_terms", "day", file.Shark.DueDay.ToString(), "perDay", FinanceText.Money(SharkRules.ADay(file.Shark.Borrowed)),
                    "cap", FinanceText.Money(SharkRules.Cap(file.Shark.Borrowed))), CellTone.Yellow);
                box.Note(FinanceText.T("reallife_econ_private_how"));
                Meta.Guide.Point(box, GuideTitle);
            }
            else if (selected == ApprovalRow)
            {
                // The approval's own page, as Borrow shows it; the product is set only while it is written.
                _product = file.Relief.ApprovedKind == (int)ProductKind.Mortgage ? HomeLoan : CarFinance;
                box.Title(FinanceText.T(ProductKeys[_product]));
                WriteFinance(box, desk, (ProductKind)file.Relief.ApprovedKind);
                _product = -1;
                Meta.Guide.Point(box, GuideTitle);
            }
            else if (selected == WaysRow)
            {
                WriteWays(box, desk);
                Meta.Guide.Point(box, GuideTitle);
            }
            else
            {
                Obligations due = Books.Tomorrow(file);
                box.Title(FinanceText.T("reallife_econ_tab_accounts"));
                box.Note(FinanceText.T("reallife_econ_accounts_pick"));
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_econ_over_owed", FinanceText.Money(owed));
                box.Row("reallife_econ_over_leaves", FinanceText.Money(due.Loans + due.Accounts));
                box.Section("reallife_ibox_sec_how");
                box.Note(FinanceText.T("reallife_econ_accounts_order"));
                Meta.Guide.Point(box, GuideTitle);
            }
            return selected;
        }

        /// <summary>A game loan's row in Finance and a bank app, by its mark (the bank, the first day and the sum): a pick stays on the loan when one before it is paid off.</summary>
        internal static string LoanRowId(LoanMark m)
        {
            return "loan:" + m.Street + "#" + m.Number + "@" + m.FirstDay + "/" + Mathf.RoundToInt(m.Total);
        }

        private static string BankName(string street, int number)
        {
            Lender lender = BankTerms.At(street, number);
            return lender != null ? lender.Name : FinanceText.T("reallife_econ_lender_bank");
        }

        /// <summary>
        /// What an account takes next and on which day: an instalment tomorrow, a card's minimum on its due day, an overdraft
        /// what is over its limit. Nothing while a debt arrangement holds it.
        /// </summary>
        internal static void NextPayment(CreditAccount a, int today, out int amount, out int day)
        {
            amount = day = 0;
            if (a.Closed || a.Frozen || a.Owed <= 0)
            {
                return;
            }
            if (a.Kind == ProductKind.CreditCard)
            {
                if (a.DueDay > 0 && a.MinimumLeft > 0)
                {
                    amount = a.MinimumLeft;
                    day = a.DueDay;
                }
                return;
            }
            amount = AccountRules.DueTomorrow(a, today);
            day = amount > 0 ? today + 1 : 0;
        }

        private static void PaymentCells(TableRow row, int amount, int day, int today, bool inTotal = true)
        {
            if (amount <= 0 || day <= 0)
            {
                return;
            }
            row.Set(4, FinanceText.Money(amount), inTotal ? amount : double.NaN);   // a test tool's account is in no total
            row.Set(5, DayWord(day, today), day, day < today ? TableRow.Red : TableRow.Plain);
        }

        /// <summary>A day as the game says it: "Today", "Tomorrow", else "Day 14".</summary>
        internal static string DayWord(int day, int today)
        {
            return day == today ? FinanceText.T("common_today") : day == today + 1 ? FinanceText.T("common_tomorrow") : FinanceText.Day(day);
        }

        /// <summary>How grave a debt's standing is, to order by: misses, then the lender's steps, an arrangement last.</summary>
        internal static double Gravity(LadderState s, bool frozen)
        {
            return s.Misses + (s.Penalty ? 0.25 : 0) + (s.PlanLeft > 0 ? 0.5 : 0) + (s.RepoDay > 0 ? 20 : 0) + (s.CallDue ? 50 : 0) + (s.RepoPending ? 100 : 0) + (frozen ? 200 : 0);
        }

        /// <summary>A game loan's yearly rate in per cent, from its daily interest: the game keeps no rate.</summary>
        internal static float YearlyRate(Loan loan, int daysPerYear)
        {
            return loan.totalAmount > 0f ? loan.dailyInterest * (float)daysPerYear / loan.totalAmount * 100f : 0f;
        }

        /// <summary>The head of a debt's page: its state, and what further misses would bring.</summary>
        internal static void StandsLines(InfoBox box, string state, LadderState s, bool secured, bool nextStep)
        {
            box.Chip(state, s.RepoPending || s.Misses >= 2 ? CellTone.Red : s.Misses > 0 || s.Overdue > 0 ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_ibox_sec_now");
            if (nextStep && FinanceTables.NextStep(s, CreditDesk.Level, secured, out string step, out int away))
            {
                box.Line(away <= 1
                    ? FinanceText.T("reallife_tab_account_next_miss", "step", FinanceText.T(step))
                    : FinanceText.T("reallife_tab_account_later_miss", "step", FinanceText.T(step), "count", away.ToString()), away <= 1 ? CellTone.Red : CellTone.Yellow);
            }
        }

        /// <summary>The note on a debt a debt arrangement holds: the arrangement pays it, and it can be paid sooner.</summary>
        internal static void InArrangement(InfoBox box)
        {
            box.Note(FinanceText.T("reallife_econ_account_arranged_pay"));
        }

        /// <summary>The rate a year, and under it the penalty in it while there is one.</summary>
        internal static void RateLines(InfoBox box, string rate, bool penalty)
        {
            box.Row("reallife_econ_offer_rate", rate, penalty ? CellTone.Red : CellTone.Plain);
            if (penalty)
            {
                box.Line(FinanceText.T("reallife_econ_account_penalty", "points", ProductCatalogue.PenaltyPoints.ToString(), "run", ProductCatalogue.PenaltyClearRun.ToString()), CellTone.Red);
            }
        }

        /// <summary>A debt page's foot: what the credit is, how its rate is set (either may be null), the Guide.</summary>
        internal static void AboutLines(InfoBox box, string about, string rateNote, string guideTitle)
        {
            if (about != null)
            {
                box.Note(about);
            }
            if (rateNote != null)
            {
                box.Note(rateNote);
            }
            Meta.Guide.Point(box, guideTitle);
        }

        private void WriteAccount(InfoBox box, CreditDesk desk, CreditAccount a, int today)
        {
            bool revolving = ProductCatalogue.Revolving(a.Kind), instalment = ProductCatalogue.Instalment(a.Kind);
            box.Title(CreditDesk.Name(a));
            LadderState s = a.Ladder;
            StandsLines(box, FinanceText.State(a), s, a.Secured, !a.Frozen && a.Owed > 0);
            if (s.RepoPending)
            {
                box.Line(FinanceText.T("reallife_econ_account_repo", "thing", CreditDesk.Thing(a)), CellTone.Red);
            }
            box.Row("reallife_econ_over_owed", a.Agreed > 0
                ? FinanceText.T("reallife_econ_row_owed_of", "owed", FinanceText.Money(a.Owed), "total", FinanceText.Money(a.Agreed)) : FinanceText.Money(a.Owed));
            if (revolving && a.Agreed > 0)
            {
                float used = a.Owed / (float)a.Agreed;
                box.Meter("reallife_ibox_used", used, FinanceText.T("reallife_tab_account_used", "share", Mathf.RoundToInt(used * 100f) + "%", "free", FinanceText.Money(a.Free)),
                    used >= 0.9f ? CellTone.Red : used >= 0.5f ? CellTone.Yellow : CellTone.Green);
            }
            else if (instalment && a.Agreed > 0)
            {
                int paid = Mathf.Max(0, a.Agreed - a.Owed);
                box.Meter("reallife_ibox_paid_off", LookRules.Fill(paid, a.Agreed),
                    FinanceText.T("reallife_tab_account_paid", "paid", FinanceText.Money(paid), "total", FinanceText.Money(a.Agreed)), CellTone.Green);
            }
            if (a.RateBp > 0)
            {
                RateLines(box, Rate(a.RateNow), a.Ladder.Penalty);
            }
            int due = AccountRules.DueTomorrow(a, today);
            switch (a.Kind)
            {
                case ProductKind.CreditCard:
                    box.Line(a.DueDay > 0
                        ? FinanceText.T("reallife_econ_account_card_due", "minimum", FinanceText.Money(a.MinimumLeft), "day", a.DueDay.ToString(), "statement", FinanceText.Money(a.StatementLeft))
                        : FinanceText.T("reallife_econ_account_card_next", "day", a.NextDay.ToString()));
                    break;
                case ProductKind.Overdraft:
                    box.Line(FinanceText.T("reallife_econ_account_overdraft", "free", FinanceText.Money(a.Free), "float", FinanceText.Money(ProductCatalogue.OverdraftFloat)));
                    break;
                case ProductKind.Arrears:
                    box.Line(FinanceText.T("reallife_econ_account_arrears"), CellTone.Yellow);
                    break;
                default:
                    {
                        int left = a.Instalment > 0 ? Mathf.CeilToInt(a.Owed / (float)a.Instalment) : 0;
                        box.Line(FinanceText.T("reallife_econ_account_instalment", "payment", FinanceText.Money(due), "days", left.ToString()));
                        if (left > 0 && !a.Frozen)
                        {
                            box.Row("reallife_tab_account_paid_off_day", FinanceText.Day(today + left));
                        }
                        box.Note(FinanceText.T(a.Secured ? "reallife_econ_account_secured" : "reallife_econ_account_unsecured", "thing", CreditDesk.Thing(a)));
                        break;
                    }
            }
            if (s.PlanLeft > 0)
            {
                box.Line(FinanceText.T("reallife_econ_account_plan", "left", FinanceText.Money(s.PlanLeft), "payment", FinanceText.Money(s.PlanPerDay)), CellTone.Yellow);
            }
            box.Note(FinanceText.T("reallife_tab_account_opened", "lender", CreditDesk.LenderName(a), "day", a.OpenedDay.ToString()));
            if (a.Frozen)
            {
                InArrangement(box);
            }
            AccountButtons(box, desk, a, s, due);
            string rateNote = a.RateBp > 0 && !a.Ladder.Penalty
                ? (revolving ? FinanceText.T("reallife_tab_account_rate_moves", "agreed", Rate(a.RateBp)) : FinanceText.T("reallife_tab_account_rate_fixed"))
                : null;
            AboutLines(box, AboutKind(a.Kind), rateNote, GuideTitle);
        }

        // In a debt arrangement only paying, in part or in full: the arrangement counts it and ends once all is paid.
        private void AccountButtons(InfoBox box, CreditDesk desk, CreditAccount a, LadderState s, int due)
        {
            Transform buttons = box.ButtonRow();
            if (s.CallDue && !a.Frozen)
            {
                CallButtons(buttons, desk, new Debt(a));
                return;
            }
            if (a.Owed > 0)
            {
                int suggested = a.Frozen ? a.Owed : Mathf.Min(a.Owed, Mathf.Max(s.Overdue, due));
                AskAmount(buttons, "reallife_econ_account_pay", "reallife_econ_account_pay_how_much", suggested, amount => Paid(desk, a, amount));
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_account_pay_off", () => Paid(desk, a, a.Owed));
            }
            if (a.Frozen)
            {
                return;   // nothing is drawn on it and it is not closed while the arrangement holds it
            }
            if (a.Kind == ProductKind.CreditCard)
            {
                Transform second = a.Owed > 0 ? box.ButtonRow() : buttons;
                AskAmount(second, "reallife_econ_account_draw", "reallife_econ_account_draw_how_much", AccountRules.MostDraw(a), amount => Drew(desk, a, amount));
                if (a.Owed == 0)
                {
                    InfoBox.Button(second, InfoBox.Negative, "reallife_econ_account_close", () => Said(desk.CloseAccount(a)));
                }
            }
            else if (a.Kind == ProductKind.Overdraft && a.Owed == 0)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_econ_account_close", () => Said(desk.CloseAccount(a)));
            }
        }

        // A button asking for an amount in the game's own text box; the action checks what was typed.
        private void AskAmount(Transform row, string buttonKey, string placeholderKey, int initial, Action<int> then)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            InfoBox.Button(row, InfoBox.Additional, buttonKey, () => uis.textInputUi.Show(placeholderKey, "common_confirm", text => UiClone.Safe(() =>
            {
                if (!Whole(text, out int amount) || amount <= 0)
                {
                    Notifications.ShowError("common_notification_invalid_amount", null, false);
                    return;
                }
                then(amount);
            }), null, Mathf.Max(0, initial).ToString(), false, TypedAmountLength));
        }

        private void Paid(CreditDesk desk, CreditAccount a, int amount)
        {
            string name = CreditDesk.Name(a);
            ProductAnswer answer = desk.Pay(a, amount, out int used);
            if (answer == ProductAnswer.Yes)
            {
                Tell(NotificationType.Success, a.Closed ? "reallife_econ_note_paid_off" : "reallife_econ_note_paid", new Dictionary<string, string>
                {
                    { "account", name }, { "amount", FinanceText.Money(used) }, { "left", FinanceText.Money(a.Owed) }
                });
            }
            Said(answer);
        }

        private void Drew(CreditDesk desk, CreditAccount card, int amount)
        {
            ProductAnswer answer = desk.Draw(card, amount, out int fee);
            if (answer == ProductAnswer.Yes)
            {
                Tell(NotificationType.Success, "reallife_econ_note_drawn", new Dictionary<string, string>
                {
                    { "amount", FinanceText.Money(amount) }, { "fee", FinanceText.Money(fee) }, { "owed", FinanceText.Money(card.Owed) }
                });
            }
            Said(answer);
        }

        private void CallButtons(Transform buttons, CreditDesk desk, Debt debt)
        {
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_account_pay_overdue", () => Said(desk.PayOverdue(debt)));
            if (!debt.Ladder.PlanUsed)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_collect_plan", () => Said(desk.AgreePlan(debt)));
            }
            InfoBox.Button(buttons, InfoBox.Negative, "reallife_collect_cannot", () =>
            {
                desk.CannotPay(debt);
                After();
            });
        }

        private void WriteLoan(InfoBox box, CreditDesk desk, LoanMark m)
        {
            var loan = (Loan)m.Ref;
            int today = SaveGameManager.Current.Day;
            bool frozen = (m.Held & LoanHold.Frozen) != 0, penalty = (m.Held & LoanHold.Penalty) != 0;
            box.Title(GameLoans.BankName(loan));
            StandsLines(box, FinanceText.State(m.Ladder, frozen, false), m.Ladder, m.Purpose == LoanPurpose.Business && Books.Business(m.Security) != null, !frozen);
            box.Row("reallife_econ_over_owed", FinanceText.T("reallife_econ_row_owed_of", "owed", FinanceText.Money(loan.remainingAmount), "total", FinanceText.Money(loan.totalAmount)));
            box.Row("reallife_econ_offer_payment", FinanceText.Money(loan.dailyPayment));
            if (loan.dailyPayment > 0 && !frozen)
            {
                int left = Mathf.CeilToInt(loan.remainingAmount / loan.dailyPayment);
                box.Line(FinanceText.T("reallife_tab_loan_left", "days", left.ToString(), "day", (today + left).ToString()));
            }
            float paid = Mathf.Max(0f, loan.PaidAmount);
            box.Meter("reallife_ibox_paid_off", loan.totalAmount > 0f ? paid / loan.totalAmount : 1f,
                FinanceText.T("reallife_tab_account_paid", "paid", FinanceText.Money(paid), "total", FinanceText.Money(loan.totalAmount)), CellTone.Green);
            float yearly = YearlyRate(loan, Mathf.Max(1, AccountRules.DaysPerYear));
            if (yearly > 0f)
            {
                RateLines(box, FinanceText.T("reallife_econ_row_rate", "rate", yearly.ToString("0.#")), penalty);
            }
            box.Row("reallife_econ_offer_interest", FinanceText.Money(loan.dailyInterest));
            if (m.Purpose == LoanPurpose.Business)
            {
                BuildingRegistration shop = Books.Business(m.Security);
                box.Note(shop != null ? FinanceText.T("reallife_econ_account_secured", "thing", shop.BusinessName) : FinanceText.T("reallife_econ_loan_unsecured"));
            }
            if (m.Ladder.PlanLeft > 0)
            {
                box.Line(FinanceText.T("reallife_econ_account_plan", "left", FinanceText.Money(m.Ladder.PlanLeft), "payment", FinanceText.Money(m.Ladder.PlanPerDay)), CellTone.Yellow);
            }
            if (m.Ladder.RepoDay > 0)
            {
                string share = Mathf.RoundToInt(ProductCatalogue.BusinessDivertShare * 100f) + "%";
                box.Line(FinanceText.T("reallife_econ_loan_takings", "share", share, "amount", FinanceText.Money(m.Ladder.Overdue)), CellTone.Red);
            }
            if (frozen)
            {
                InArrangement(box);   // EconoView pays it sooner, as a bank's app does
            }
            Transform buttons = box.ButtonRow();
            if (m.Ladder.CallDue)
            {
                CallButtons(buttons, desk, new Debt(m));
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_econ_open_econoview", OpenEconoView);
            }
            string about = FinanceText.T("reallife_tab_loan_about", "kind", FinanceText.T(FinanceTables.DebtKinds[FinanceTables.OfLoan(m.Purpose)]), "day", m.FirstDay.ToString());
            AboutLines(box, about, yearly > 0f && !penalty ? FinanceText.T("reallife_tab_loan_rate_note") : null, GuideTitle);
        }

        private void WriteWays(InfoBox box, CreditDesk desk)
        {
            CreditFile file = CreditLedger.File;
            desk.Owed(out int loans, out int unsecured, out int secured, out int shark, out int other);
            PersonalWealthData wealth = PlayerHelper.GetPersonalWealth();
            box.Title(FinanceText.T("reallife_econ_row_ways"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_econ_over_owed", FinanceText.Money(loans + unsecured + secured + shark + other));
            box.Headline("reallife_econ_ways_sell");
            box.Line(FinanceText.T("reallife_econ_ways_sell_line", "cash", FinanceText.Money(wealth.cash), "investments", FinanceText.Money(wealth.totalInvestments),
                "assets", FinanceText.Money(wealth.totalAssets)));
            ProductAnswer arrange = desk.ArrangementOffer(out ArrangementPlan plan);
            box.Headline("reallife_econ_ways_arrange");
            if (file.Relief.Arranged)
            {
                box.Line(FinanceText.Fill("reallife_econ_ways_arranged", "paid", FinanceText.Money(file.Relief.ArrangedPaid), "total", FinanceText.Money(file.Relief.ArrangedTotal),
                    "payment", FinanceText.Money(file.Relief.ArrangedPerDay), "count", file.Relief.ArrangedMisses.ToString(), "most", ProductCatalogue.ArrangementMissesMost.ToString()));
            }
            else if (arrange == ProductAnswer.Yes)
            {
                box.Line(FinanceText.T("reallife_econ_ways_arrange_line", "amount", FinanceText.Money(plan.Total), "payment", FinanceText.Money(plan.PerDay), "days", plan.Days.ToString()));
            }
            else
            {
                box.Line(Why(arrange), CellTone.Yellow);
            }
            ProductAnswer bankrupt = desk.BankruptcyOffer(out BankruptcyPlan ruin);
            box.Headline("reallife_econ_ways_bankrupt");
            box.Line(bankrupt == ProductAnswer.Yes
                ? FinanceText.T("reallife_econ_ways_bankrupt_line", "amount", FinanceText.Money(ruin.WrittenOff), "cash", FinanceText.Money(ruin.CashTaken),
                    "days", ProductCatalogue.BankruptNoCreditDays.ToString())
                : bankrupt == ProductAnswer.Busy ? FinanceText.T("reallife_advice_game_quest")
                : bankrupt == ProductAnswer.Bankrupt ? FinanceText.T("reallife_advice_bankrupt_soon", "days", ProductCatalogue.BankruptEveryDays.ToString())
                : Why(bankrupt));
            Transform buttons = box.ButtonRow();
            if (arrange == ProductAnswer.Yes && !file.Relief.Arranged)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_econ_ways_arrange_do", () =>
                    GameConfirm.Show("reallife_econ_ways_arrange".Localize(),
                        "reallife_advice_arrange_page".Localize(new
                        {
                            amount = FinanceText.Money(plan.Total), payment = FinanceText.Money(plan.PerDay), days = plan.Days.ToString(), cap = ProductCatalogue.ArrangementScoreCap.ToString(),
                            after = ProductCatalogue.ArrangementCapDaysAfter.ToString(), misses = ProductCatalogue.ArrangementMissesMost.ToString()
                        }),
                        () => UiClone.Safe(() =>
                        {
                            ProductAnswer answer = desk.Arrange();
                            if (answer == ProductAnswer.Yes)
                            {
                                Tell(NotificationType.Info, "reallife_econ_note_arranged", new Dictionary<string, string>
                                {
                                    { "amount", FinanceText.Money(plan.Total) }, { "payment", FinanceText.Money(plan.PerDay) }
                                });
                            }
                            Said(answer);
                        }), null, "reallife_econ_ways_arrange_do", "common_cancel", false, false));
            }
            if (bankrupt == ProductAnswer.Yes)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_econ_ways_bankrupt_do", () =>
                    GameConfirm.Show("reallife_econ_ways_bankrupt".Localize(),
                        "reallife_advice_bankrupt_page".Localize(new
                        {
                            amount = FinanceText.Money(ruin.WrittenOff), kept = FinanceText.Money(ruin.Kept), cash = FinanceText.Money(ruin.CashTaken), left = FinanceText.Money(ruin.CashLeft),
                            days = ProductCatalogue.BankruptNoCreditDays.ToString(), from = ProductCatalogue.BankruptRebuildFrom.ToString(), every = ProductCatalogue.BankruptEveryDays.ToString()
                        }),
                        () => UiClone.Safe(() => Said(desk.Bankrupt())), null, "reallife_econ_ways_bankrupt_do", "common_cancel", false, true));   // the game's red question box
            }
        }
    }
}
