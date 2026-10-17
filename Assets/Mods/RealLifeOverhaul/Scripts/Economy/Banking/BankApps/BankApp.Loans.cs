using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A bank app's Loans and Card tabs: a list in the table, the one picked in the white box, every payment asked of the credit desk.</summary>
    public sealed partial class BankApp
    {
        private readonly List<Debt> _work = new List<Debt>(16);
        private string _picked;     // the row picked on the Loans tab

        private string WriteLoans(InfoBox box)
        {
            CreditDesk desk = Desk;
            int today = SaveGameManager.Current.Day, perYear = Mathf.Max(1, AccountRules.DaysPerYear);
            string selected = null;
            Debt picked = default(Debt);
            bool found = false;
            _spec = _tables.Loans;
            desk.Held(Bank, _debts);
            for (int i = 0; i < _debts.Count; i++)
            {
                Debt d = _debts[i];
                CreditAccount a = d.Account;
                if (a != null && ProductCatalogue.Revolving(a.Kind))
                {
                    continue;   // an overdraft and a card have their own tabs
                }
                TableRow row;
                if (a != null)
                {
                    row = Row("acct:" + a.Id);
                    row.Kind = FinanceTables.Of(a.Kind);
                    row.Set(0, CreditDesk.Name(a));
                    row.Set(2, FinanceText.Money(a.Owed), a.Test ? double.NaN : a.Owed);   // a test tool's account is in no total
                    if (a.RateBp > 0)
                    {
                        row.Set(3, FinanceApp.Rate(a.RateNow), a.RateNow / 100.0, a.Ladder.Penalty ? TableRow.Red : TableRow.Plain);
                    }
                    int next, day;
                    FinanceApp.NextPayment(a, today, out next, out day);
                    Payment(row, next, day, today);
                    if (ProductCatalogue.Instalment(a.Kind) && a.Instalment > 0 && !a.Frozen)
                    {
                        int end = today + Mathf.CeilToInt(a.Owed / (float)a.Instalment);
                        row.Set(6, FinanceText.Day(end), end);
                    }
                    row.Set(7, FinanceText.State(a), FinanceApp.Gravity(a.Ladder, a.Frozen), a.Ladder.InOrder && !a.Ladder.Penalty ? TableRow.Plain : TableRow.Red);
                }
                else
                {
                    LoanMark m = d.Mark;
                    Loan loan = d.Loan;
                    bool frozen = (m.Held & LoanHold.Frozen) != 0;
                    row = Row(FinanceApp.LoanRowId(m));   // by the loan, not its place in the list, which moves when one before it is paid off
                    row.Kind = FinanceTables.OfLoan(m.Purpose);
                    row.Set(0, FinanceText.T(FinanceTables.DebtKinds[row.Kind]));
                    row.Set(2, FinanceText.Money(loan.remainingAmount), loan.remainingAmount);
                    float yearly = FinanceApp.YearlyRate(loan, perYear);
                    if (yearly > 0f)
                    {
                        row.Set(3, FinanceText.T("reallife_econ_row_rate", "rate", yearly.ToString("0.#")), yearly, (m.Held & LoanHold.Penalty) != 0 ? TableRow.Red : TableRow.Plain);
                    }
                    Payment(row, frozen ? 0 : Mathf.RoundToInt(GameLoans.Due(loan)), today + 1, today);
                    if (loan.dailyPayment > 0 && !frozen)
                    {
                        int end = today + Mathf.CeilToInt(loan.remainingAmount / loan.dailyPayment);
                        row.Set(6, FinanceText.Day(end), end);
                    }
                    row.Set(7, FinanceText.State(m.Ladder, frozen, false), FinanceApp.Gravity(m.Ladder, frozen), m.Ladder.InOrder && !m.Ladder.Penalty ? TableRow.Plain : TableRow.Red);
                }
                row.Pick = true;
                row.Set(1, FinanceText.T(FinanceTables.DebtKinds[row.Kind]));
                if (row.Id == _picked)
                {
                    picked = d;
                    found = true;
                    selected = row.Id;
                }
            }
            if (_rows.Count == 0)
            {
                Say("none", FinanceText.T("reallife_bank_row_no_loans", "bank", Bank.Name));
            }
            if (!found)
            {
                _picked = null;
                BankSlice slice = desk.Slice(Bank, _work);
                box.Title(FinanceText.T("reallife_bank_tab_loans"));
                box.Subtitle(Bank.Name);
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_bank_owed_here", FinanceText.Money(slice.Owed));
                box.Row("reallife_econ_over_leaves", FinanceText.Money(slice.Tomorrow));
                box.EndSection();
                box.Note(FinanceText.T("reallife_bank_loans_pick"));
                box.Line(FinanceText.T("reallife_bank_loans_finance"));
                Meta.Guide.Point(box, GuideTitle);
                return null;
            }
            if (picked.Account != null)
            {
                WriteInstalments(box, desk, picked.Account, today);
            }
            else
            {
                WriteLoan(box, desk, picked.Mark, today);
            }
            return selected;
        }

        // The "next payment" and "due" cells of a row; empty where nothing is due.
        private static void Payment(TableRow row, int amount, int day, int today)
        {
            if (amount <= 0 || day <= 0)
            {
                return;
            }
            row.Set(4, FinanceText.Money(amount), amount);
            row.Set(5, FinanceApp.DayWord(day, today), day, day < today ? TableRow.Red : TableRow.Plain);
        }

        private void WriteLoan(InfoBox box, CreditDesk desk, LoanMark m, int today)
        {
            var loan = (Loan)m.Ref;
            bool frozen = (m.Held & LoanHold.Frozen) != 0;
            string kind = FinanceText.T(FinanceTables.DebtKinds[FinanceTables.OfLoan(m.Purpose)]);
            float yearly = FinanceApp.YearlyRate(loan, Mathf.Max(1, AccountRules.DaysPerYear));
            box.Title(kind);
            FinanceApp.StandsLines(box, FinanceText.State(m.Ladder, frozen, false), m.Ladder, m.Purpose == LoanPurpose.Business && Books.Business(m.Security) != null, !frozen);
            box.Row("reallife_econ_over_owed", FinanceText.T("reallife_econ_row_owed_of", "owed", FinanceText.Money(loan.remainingAmount), "total", FinanceText.Money(loan.totalAmount)));
            box.Row("reallife_econ_offer_payment", FinanceText.Money(loan.dailyPayment));
            if (loan.dailyPayment > 0 && !frozen)
            {
                int left = Mathf.CeilToInt(loan.remainingAmount / loan.dailyPayment);
                box.Line(FinanceText.T("reallife_tab_loan_left", "days", left.ToString(), "day", (today + left).ToString()));
            }
            if (yearly > 0f)
            {
                box.Row("reallife_econ_offer_rate", FinanceText.T("reallife_econ_row_rate", "rate", yearly.ToString("0.#")));
            }
            box.Row("reallife_econ_offer_interest", FinanceText.Money(loan.dailyInterest));
            if (m.Purpose == LoanPurpose.Business)
            {
                BuildingRegistration shop = Books.Business(m.Security);
                box.Note(shop != null ? FinanceText.T("reallife_econ_account_secured", "thing", shop.BusinessName) : FinanceText.T("reallife_econ_loan_unsecured"));
            }
            if (frozen)
            {
                FinanceApp.InArrangement(box);
            }
            LoanButtons(box, desk, m, loan, frozen);
            FinanceApp.AboutLines(box, FinanceText.T("reallife_tab_loan_about", "kind", kind, "day", m.FirstDay.ToString()), yearly > 0f ? FinanceText.T("reallife_tab_loan_rate_note") : null,
                                  GuideTitle);
        }

        // A game loan's buttons: the collections call's answers when it is due, else paying it and its daily payment (not in an arrangement).
        private void LoanButtons(InfoBox box, CreditDesk desk, LoanMark m, Loan loan, bool frozen)
        {
            Transform buttons = box.ButtonRow();
            var debt = new Debt(m);
            if (m.Ladder.CallDue && !frozen)
            {
                CallButtons(buttons, desk, debt);
                return;
            }
            int whole = Mathf.CeilToInt(loan.remainingAmount);
            AskAmount(buttons, "reallife_econ_account_pay", "reallife_econ_account_pay_how_much", frozen ? whole : Mathf.Min(whole, loan.dailyPayment), amount => PaidLoan(desk, m, amount));
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_account_pay_off", () =>
                GameConfirm.Show("reallife_bank_confirm_payoff_title".Localize(new { bank = Bank.Name }),
                    "reallife_bank_confirm_payoff".Localize(new { amount = FinanceText.Money(whole), bank = Bank.Name }),
                    () => UiClone.Safe(() => PaidLoan(desk, m, whole)), null, "reallife_econ_account_pay_off", "common_cancel", false, false));
            if (!frozen)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_bank_daily_payment", OpenEconoView);
            }
        }

        // The game's own loans page, where a loan's daily payment is changed.
        private void OpenEconoView()
        {
            if (_menu != null)
            {
                _menu.ShowApp(AppName.EconoView);
            }
        }

        private void PaidLoan(CreditDesk desk, LoanMark m, int amount)
        {
            int used;
            string name = Bank.Name;
            ProductAnswer answer = desk.PayLoan(m, amount, out used);
            if (answer == ProductAnswer.Yes)
            {
                var loan = m.Ref as Loan;
                bool gone = loan == null || loan.remainingAmount <= 0f;
                Tell(NotificationType.Success, gone ? "reallife_bank_note_loan_paid_off" : "reallife_bank_note_loan_paid", new Dictionary<string, string>
                {
                    { "bank", name }, { "amount", FinanceText.Money(used) }, { "left", FinanceText.Money(gone ? 0f : loan.remainingAmount) }
                });
                if (gone)
                {
                    _picked = null;
                }
            }
            Said(answer);
        }

        private void WriteInstalments(InfoBox box, CreditDesk desk, CreditAccount a, int today)
        {
            box.Title(CreditDesk.Name(a));
            FinanceApp.StandsLines(box, FinanceText.State(a), a.Ladder, a.Secured, !a.Frozen && a.Owed > 0);
            box.Row("reallife_econ_over_owed", a.Agreed > 0
                ? FinanceText.T("reallife_econ_row_owed_of", "owed", FinanceText.Money(a.Owed), "total", FinanceText.Money(a.Agreed)) : FinanceText.Money(a.Owed));
            if (a.Kind == ProductKind.Arrears)
            {
                box.Line(FinanceText.T("reallife_econ_account_arrears"), CellTone.Yellow);
            }
            else
            {
                int due = AccountRules.DueTomorrow(a, today), left = a.Instalment > 0 ? Mathf.CeilToInt(a.Owed / (float)a.Instalment) : 0;
                box.Line(FinanceText.T("reallife_econ_account_instalment", "payment", FinanceText.Money(due), "days", left.ToString()));
                if (left > 0 && !a.Frozen)
                {
                    box.Row("reallife_tab_account_paid_off_day", FinanceText.Day(today + left));
                }
                box.Note(FinanceText.T(a.Secured ? "reallife_econ_account_secured" : "reallife_econ_account_unsecured", "thing", CreditDesk.Thing(a)));
            }
            if (a.RateBp > 0)
            {
                FinanceApp.RateLines(box, FinanceApp.Rate(a.RateNow), a.Ladder.Penalty);
            }
            if (a.Ladder.PlanLeft > 0)
            {
                box.Line(FinanceText.T("reallife_econ_account_plan", "left", FinanceText.Money(a.Ladder.PlanLeft), "payment", FinanceText.Money(a.Ladder.PlanPerDay)), CellTone.Yellow);
            }
            if (a.Frozen)
            {
                FinanceApp.InArrangement(box);
            }
            Transform buttons = box.ButtonRow();
            if (a.Ladder.CallDue && !a.Frozen)
            {
                CallButtons(buttons, desk, new Debt(a));
            }
            else if (a.Owed > 0)
            {
                // in a debt arrangement too: the arrangement counts it
                int suggested = a.Frozen ? a.Owed : Mathf.Min(a.Owed, Mathf.Max(a.Ladder.Overdue, AccountRules.DueTomorrow(a, today)));
                AskAmount(buttons, "reallife_econ_account_pay", "reallife_econ_account_pay_how_much", suggested, amount => Paid(desk, a, amount));
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_account_pay_off", () => Paid(desk, a, a.Owed));
            }
            FinanceApp.AboutLines(box, FinanceApp.AboutKind(a.Kind), a.RateBp > 0 && !a.Ladder.Penalty ? FinanceText.T("reallife_tab_account_rate_fixed") : null, GuideTitle);
        }

        private void Paid(CreditDesk desk, CreditAccount a, int amount)
        {
            int used;
            string name = CreditDesk.Name(a);
            ProductAnswer answer = desk.Pay(a, amount, out used);
            if (answer == ProductAnswer.Yes)
            {
                Tell(NotificationType.Success, a.Closed ? "reallife_econ_note_paid_off" : "reallife_econ_note_paid", new Dictionary<string, string>
                {
                    { "account", name }, { "amount", FinanceText.Money(used) }, { "left", FinanceText.Money(a.Owed) }
                });
                if (a.Closed)
                {
                    _picked = null;
                }
            }
            Said(answer);
        }

        // The collections call's three answers, for a debt whose call is due.
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

        // A card held here: its figures, statement and buttons, and the card's own movements over a month in the table.
        private void WriteCard(InfoBox box)
        {
            _spec = _tables.CardMoves;
            CreditDesk desk = Desk;
            CreditAccount card = HeldHere(ProductKind.CreditCard);
            int today = SaveGameManager.Current.Day;
            if (card == null)
            {
                box.Title(FinanceText.T("reallife_econ_kind_card"));
                string other = HeldWith(ProductKind.CreditCard);
                if (other != null)
                {
                    box.Note(FinanceText.T("reallife_bank_card_elsewhere", "other", other, "bank", Bank.Name));
                }
                else
                {
                    ProductOffer offer = desk.Offer(ProductKind.CreditCard, Bank);
                    if (offer.Answer == ProductAnswer.Yes)
                    {
                        box.Line(FinanceText.T("reallife_bank_card_offer", "bank", Bank.Name, "limit", FinanceText.Money(offer.Most), "rate", (offer.RateBp / 100f).ToString("0.#")));
                        InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_bank_apply_go", () => GoApply(BankTables.Card));
                    }
                    else
                    {
                        box.Line(Why(offer.Answer), CellTone.Red);
                    }
                }
                Say("none", FinanceText.T("reallife_bank_row_no_card", "bank", Bank.Name));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            string name = CreditDesk.Name(card);
            CardMovements(name, today);
            box.Title(name);
            FinanceApp.StandsLines(box, FinanceText.State(card), card.Ladder, false, !card.Frozen && card.Owed > 0);
            box.Row("reallife_bank_card_balance", FinanceText.Money(card.Owed));
            box.Row("reallife_econ_offer_limit", FinanceText.Money(card.Agreed));
            if (card.Agreed > 0)
            {
                float used = card.Owed / (float)card.Agreed;
                box.Meter("reallife_ibox_used", used, FinanceText.T("reallife_tab_account_used", "share", Mathf.RoundToInt(used * 100f) + "%", "free", FinanceText.Money(card.Free)),
                    used >= 0.9f ? CellTone.Red : used >= 0.5f ? CellTone.Yellow : CellTone.Green);
            }
            FinanceApp.RateLines(box, FinanceApp.Rate(card.RateNow), card.Ladder.Penalty);
            box.Headline("reallife_bank_card_statement");
            box.Line(card.DueDay > 0
                ? FinanceText.T("reallife_econ_account_card_due", "minimum", FinanceText.Money(card.MinimumLeft), "day", card.DueDay.ToString(), "statement", FinanceText.Money(card.StatementLeft))
                : FinanceText.T("reallife_econ_account_card_next", "day", card.NextDay.ToString()));
            if (card.Frozen)
            {
                FinanceApp.InArrangement(box);
            }
            CardButtons(box, desk, card);
            FinanceApp.AboutLines(box, null, card.Ladder.Penalty ? null : FinanceText.T("reallife_tab_account_rate_moves", "agreed", FinanceApp.Rate(card.RateBp)), GuideTitle);
        }

        // The card's buttons: the collections call's answers when it is due, else paying, drawing and closing; in an arrangement only paying.
        private void CardButtons(InfoBox box, CreditDesk desk, CreditAccount card)
        {
            Transform buttons = box.ButtonRow();
            if (card.Ladder.CallDue && !card.Frozen)
            {
                CallButtons(buttons, desk, new Debt(card));
                return;
            }
            if (card.Owed > 0)
            {
                bool minimum = !card.Frozen && card.DueDay > 0 && card.MinimumLeft > 0;
                if (minimum)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_bank_card_pay_minimum", () => Paid(desk, card, card.MinimumLeft));
                }
                int suggested = card.Frozen ? card.Owed : Mathf.Max(card.MinimumLeft, Mathf.Min(card.Owed, card.StatementLeft));
                AskAmount(buttons, "reallife_econ_account_pay", "reallife_econ_account_pay_how_much", suggested, amount => Paid(desk, card, amount));
                InfoBox.Button(buttons, minimum ? InfoBox.Additional : InfoBox.Positive, "reallife_econ_account_pay_off", () => Paid(desk, card, card.Owed));
            }
            if (card.Frozen)
            {
                return;
            }
            Transform second = card.Owed > 0 ? box.ButtonRow() : buttons;
            AskAmount(second, "reallife_econ_account_draw", "reallife_econ_account_draw_how_much", AccountRules.MostDraw(card), amount => Drew(desk, card, amount));
            if (card.Owed == 0)
            {
                InfoBox.Button(second, InfoBox.Negative, "reallife_econ_account_close", () => Said(desk.CloseAccount(card)));
            }
        }

        private void CardMovements(string name, int today)
        {
            _cardName = name;
            _flow.Read(CashFlow.Month, today, _ofCard);
            for (int i = _flow.Entries.Count - 1; i >= 0; i--)
            {
                CashEntry entry = _flow.Entries[i];
                TableRow row = Row("card:" + i);
                row.Set(0, FinanceText.Day(entry.Day), entry.Day);
                row.Set(1, CashFlow.ItemName(entry.Type, entry.Data));
                if (entry.Amount < 0f)
                {
                    row.Set(2, FinanceText.Money(-entry.Amount), -entry.Amount);   // cash paid into the card
                }
                else
                {
                    row.Set(3, FinanceText.Money(entry.Amount), entry.Amount, TableRow.Yellow);   // cash drawn on it
                }
            }
            if (_rows.Count == 0)
            {
                Say("none", FinanceText.T("reallife_bank_row_card_quiet"));
            }
        }

        private void Drew(CreditDesk desk, CreditAccount card, int amount)
        {
            int fee;
            ProductAnswer answer = desk.Draw(card, amount, out fee);
            if (answer == ProductAnswer.Yes)
            {
                Tell(NotificationType.Success, "reallife_econ_note_drawn", new Dictionary<string, string>
                {
                    { "amount", FinanceText.Money(amount) }, { "fee", FinanceText.Money(fee) }, { "owed", FinanceText.Money(card.Owed) }
                });
            }
            Said(answer);
        }
    }
}
