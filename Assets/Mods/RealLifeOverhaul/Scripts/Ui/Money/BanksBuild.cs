using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Writes the Banks page from the facts of the save and what the player has chosen: the bank picked, the tab and its row, the amounts.
    /// Every tab builds its own parts from nothing, so no tab can show the page of another, and every button it draws is an act the
    /// screen carries to the desk the old app used, or is drawn disabled with its reason. Game-free: the live screen and the preview's
    /// samples both come through it, so what a shot shows is what ships. Cost: a pass over a few dozen rows when the facts or a choice change.
    /// </summary>
    public static partial class BanksBuild
    {
        public const int Accounts = 0, Loans = 1, Overdraft = 2, Card = 3, Apply = 4, Rating = 5, Tabs = 6;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Fill(MoneyModel m, BanksFacts f, UtilitiesWords w)
        {
            m.App = MoneyApp.Banks;
            m.Icon = UiIcon.Bank;
            m.Across = true;
            m.Shape = MoneyShape.Counter;
            m.Title = w.T("reallife_banks_app", "Banks");
            m.Nav = new[]
            {
                Tab(w, "reallife_bank_tab_accounts", "Accounts", UiIcon.Wallet), Tab(w, "reallife_bank_tab_loans", "Loans", UiIcon.ListChecks),
                Tab(w, "reallife_ui_bank_tab_overdraft", "Overdraft", UiIcon.Percent), Tab(w, "reallife_bank_tab_card", "Card", UiIcon.IdCard),
                Tab(w, "reallife_bank_tab_apply", "Apply", UiIcon.Plus), Tab(w, "reallife_bank_tab_rating", "Rating", UiIcon.Star)
            };
            m.Tab = Math.Max(0, Math.Min(Tabs - 1, m.Tab));
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = w.T("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Confirm, Label = w.T("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = w.T("reallife_ui_prompt_back", "Back") }
            };
            m.EmptyIcon = UiIcon.Bank;
            if (!f.Ready || f.Banks.Length == 0)
            {
                m.Shape = MoneyShape.Table;
                m.EmptyTitle = BankParts.Either(w, f.Ready, "reallife_ui_bank_none_title", "No banks in the city", "reallife_bank_off_title", "Not ready");
                m.EmptyBody = BankParts.Either(w, f.Ready, "reallife_ui_bank_none_body", "The city has no bank to show yet.", "reallife_bank_off_body", "The credit rating is not running. Close the phone and open the app again in a moment.");
                return;
            }
            int at = Math.Max(0, Math.Min(f.Banks.Length - 1, f.Bank));
            BankFact bank = f.Banks[at];
            m.PickNames = new string[f.Banks.Length];
            for (int i = 0; i < f.Banks.Length; i++)
            {
                m.PickNames[i] = f.Banks[i].Name;
            }
            m.PickAt = at;
            m.Profile = Profile(w, f, bank);
            var cards = new BankCards();
            switch (m.Tab)
            {
                case Loans: LoansTab(m, f, bank, w, cards); break;
                case Overdraft: LimitTab(m, f, bank, w, cards, f.Overdraft, true); break;
                case Card: LimitTab(m, f, bank, w, cards, f.Card, false); break;
                case Apply: ApplyTab(m, f, bank, w, cards); break;
                case Rating: RatingTab(m, f, bank, w, cards); break;
                default: AccountsTab(m, f, bank, w, cards); break;
            }
            cards.Into(m);
            BankParts.Tidy(m);
        }

        private static UiNavEntry Tab(UtilitiesWords w, string key, string english, UiIcon icon) => new UiNavEntry { Icon = icon, Label = w.T(key, english) };

        // The bank at the head: its mark, its name, the rate it gives you, the most it lends you, since when you bank with it, and your standing.
        private static UiProfileModel Profile(UtilitiesWords w, BanksFacts f, BankFact b)
        {
            bool held = b.Since > 0 || b.Debts > 0;
            return new UiProfileModel
            {
                Name = b.Name, Hue = 200f + b.Rank * 67f, Ring = UiRole.None, Role = w.F("reallife_ui_bank_rank", "Rank {rank} of {count}", "rank", (b.Rank + 1).ToString(Inv), "count", f.Banks.Length.ToString(Inv)),
                RoleIcon = UiIcon.Bank, Status = held ? w.T("reallife_ui_bank_customer", "You bank here") : w.T("reallife_ui_bank_new", "No account yet"), StatusTone = held ? UiRole.Ok : UiRole.None,
                FactLabels = new[] { w.T("reallife_ui_bank_f_rate", "Loan rate"), w.T("reallife_ui_bank_f_most", "Lends you up to"), w.T("reallife_ui_bank_f_since", "Account since"),
                    w.T("reallife_ui_bank_f_owed", "You owe here") },
                FactValues = new[]
                {
                    b.Lends ? w.F("reallife_fin_rate_year", "{rate}% a year", "rate", b.Rate.ToString("0.#", Inv)) : w.T("reallife_ui_bank_not_lending", "Not lending"),
                    b.Lends ? w.Money(b.Most) : w.T("reallife_ui_bank_nothing", "Nothing now"),
                    b.Since > 0 ? w.Day(b.Since) : w.T("reallife_ui_bank_no_history", "No history yet"),
                    w.Money(b.Owed)
                },
                RatingLabel = w.T("reallife_ui_bank_standing", "Standing"), Rating = f.Halves
            };
        }

        // ------------------------------------------------------------------------------------------------- Accounts

        // Your current account here and what you hold with the bank, beside this bank's own movements.
        private static void AccountsTab(MoneyModel m, BanksFacts f, BankFact bank, UtilitiesWords w, BankCards cards)
        {
            int period = f.Periods.Length > 0 ? Math.Max(0, Math.Min(f.Periods.Length - 1, f.Period)) : 0;
            m.PeriodNames = f.Periods;
            m.Statement = BankParts.Statement(w, bank.Name, f.IsHouse ? w.T("reallife_ui_bank_balance", "Your cash") : w.T("reallife_ui_bank_owed_here", "You owe here"),
                w.Money(f.IsHouse ? f.Cash : f.Owed), f.Periods.Length > 0 ? f.Periods[period] : string.Empty, f.Statement, f.StatementIn, f.StatementOut,
                w.T("reallife_ui_bank_moves_group", "Movements in the period"), w.F("reallife_ui_bank_no_moves", "No movements at {bank}", "bank", bank.Name),
                w.T("reallife_ui_bank_moves_body", "They are listed here as money moves."), UiIcon.Wallet);
            var lines = new List<UtilitiesLine>(6);
            if (f.IsHouse)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ui_bank_balance", "Your cash"), w.Money(f.Cash), f.Cash < 0f ? UiRole.DangerText : UiRole.Text));
                lines.Add(BankParts.Say(w.T("reallife_bank_current_here", "Your cash is held here.")));
            }
            else
            {
                lines.Add(BankParts.Say(w.F("reallife_bank_current_elsewhere", "Current account: with {house}. Shown here: what you hold with {bank}.",
                    "house", f.HouseName, "bank", bank.Name)));
            }
            lines.Add(BankParts.Pair(w.T("reallife_ui_bank_owed_here", "You owe here"), w.Money(f.Owed)));
            lines.Add(BankParts.Pair(w.T("reallife_econ_over_leaves", "Leaves tomorrow in all:"), w.Money(f.Tomorrow)));
            lines.Add(BankParts.Pair(w.T("reallife_ui_bank_debts", "Debts here"), f.Debts.ToString(Inv)));
            cards.Add(new UtilitiesCard
            {
                Title = w.T("reallife_bank_current", "Current account"), Sub = bank.Name, Chip = f.IsHouse ? w.T("reallife_ui_bank_house", "Holds your cash") : null, ChipTone = UiRole.Ok,
                Lines = lines.ToArray(),
                Actions = new[] { BankParts.Link(w.T("reallife_ui_bank_tab_overdraft", "Overdraft"), UiIcon.Percent, Overdraft), BankParts.Link(w.T("reallife_bank_tab_loans", "Loans"), UiIcon.ListChecks, Loans) }
            });
        }

        // ------------------------------------------------------------------------------------------------- Loans

        // What you owe here, a row each; the one picked with what can be done about it.
        private static void LoansTab(MoneyModel m, BanksFacts f, BankFact bank, UtilitiesWords w, BankCards cards)
        {
            var rows = new List<UiDataRow>(8);
            var ids = new string[f.Debt.Length];
            int selected = -1;
            for (int i = 0; i < f.Debt.Length; i++)
            {
                BankDebt d = f.Debt[i];
                ids[i] = d.Id;
                selected = m.View.Row == d.Id ? i : selected;
                rows.Add(BankParts.Row(new[]
                {
                    d.Name, w.Money(d.Owed), d.Rate ?? string.Empty, d.NextAmount > 0 ? w.Money(d.NextAmount) : string.Empty, d.NextWord ?? string.Empty,
                    d.EndDay > 0 ? w.Day(d.EndDay) : string.Empty, d.State
                }, BankParts.Tones(7, 6, d.Sound ? UiRole.Ok : UiRole.Danger)));
            }
            m.Table = BankParts.Table(new[]
            {
                w.T("reallife_ui_bank_col_debt", "Debt"), w.T("reallife_fin_col_owed", "Owed"), w.T("reallife_fin_col_rate", "Rate"), w.T("reallife_ui_bank_col_next", "Next payment"),
                w.T("reallife_fin_col_day", "A day"), w.T("reallife_ui_bank_col_ends", "Paid off"), w.T("reallife_fin_col_state", "State")
            }, new[] { UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Text, UiCellKind.Text, UiCellKind.Chip }, rows, selected,
                w.F("reallife_bank_row_no_loans", "You owe {bank} nothing.", "bank", bank.Name), w.T("reallife_ui_bank_loans_empty_body", "A loan taken at this bank is listed here."), UiIcon.ListChecks);
            m.RowIds = ids;
            if (selected < 0)
            {
                m.View.Row = null;
                cards.Add(new UtilitiesCard
                {
                    Title = w.F("reallife_ui_bank_loans_at", "Loans at {bank}", "bank", bank.Name), Sub = null,
                    Lines = new[]
                    {
                        BankParts.Pair(w.T("reallife_bank_owed_here", "Owed to this bank:"), w.Money(f.Owed)), BankParts.Pair(w.T("reallife_econ_over_leaves", "Leaves tomorrow in all:"), w.Money(f.Tomorrow)),
                        BankParts.Pair(w.T("reallife_ui_bank_debts", "Debts here"), f.Debts.ToString(Inv)),
                        BankParts.Say(f.Debt.Length > 0 ? w.T("reallife_ui_bank_pick_debt", "Pick a debt in the list to pay it.") : w.T("reallife_ui_bank_no_debt", "You owe this bank nothing."))
                    },
                    Actions = new[] { BankParts.Link(w.T("reallife_bank_tab_apply", "Apply"), UiIcon.Plus, Apply) }
                });
                return;
            }
            DebtCard(m, f.Debt[selected], w, cards, true);
        }

        /// <summary>One debt as a card: its facts, an amount to pay, and what can be done about it now.</summary>
        internal static void DebtCard(MoneyModel m, BankDebt d, UtilitiesWords w, BankCards cards, bool loans)
        {
            var lines = new List<UtilitiesLine>(10);
            lines.Add(BankParts.Pair(w.T("reallife_econ_over_owed", "Owed in all:"), d.Total > 0f && d.Total != d.Owed
                ? w.F("reallife_econ_row_owed_of", "{owed} of {total}", "owed", w.Money(d.Owed), "total", w.Money(d.Total)) : w.Money(d.Owed)));
            if (d.Rate != null)
            {
                lines.Add(BankParts.Pair(w.T("reallife_econ_offer_rate", "Rate a year:"), d.Rate, d.RateRed ? UiRole.DangerText : UiRole.Text));
            }
            if (d.Payment > 0 && !d.Frozen)
            {
                lines.Add(BankParts.Pair(w.T("reallife_econ_offer_payment", "Daily payment:"), w.Money(d.Payment)));
            }
            if (d.GameLoan)
            {
                lines.Add(BankParts.Pair(w.T("reallife_econ_offer_interest", "Daily interest:"), w.Money(d.Interest)));
            }
            if (d.Left > 0 && !d.Frozen)
            {
                lines.Add(BankParts.Say(w.F("reallife_tab_loan_left", "About {days} payments left, paid off on day {day}", "days", d.Left.ToString(Inv), "day", d.EndDay.ToString(Inv))));
            }
            if (d.Kind == 3 && d.Total > 0f)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ui_bank_free", "Free to draw"), w.Money(d.Free), BankParts.Used(d.Owed / d.Total)));
            }
            if (d.Kind == 3 && d.DueDay > 0)
            {
                lines.Add(BankParts.Say(w.F("reallife_econ_account_card_due", "Minimum {minimum} taken on day {day}. Unpaid of the last statement: {statement}", "minimum", w.Money(d.Minimum), "day", d.DueDay.ToString(Inv),
                    "statement", w.Money(d.StatementLeft))));
            }
            else if (d.Kind == 3)
            {
                lines.Add(BankParts.Say(w.F("reallife_econ_account_card_next", "Next statement: day {day}", "day", d.NextStatement.ToString(Inv))));
            }
            if (d.Thing != null)
            {
                lines.Add(BankParts.Say(d.Thing));
            }
            if (d.PlanLeft > 0f)
            {
                lines.Add(BankParts.Say(w.F("reallife_econ_account_plan", "Plan: {left} still to pay, {payment} a day on top of the normal payment", "left", w.Money(d.PlanLeft), "payment", w.Money(d.PlanPerDay)), UiRole.WarnText));
            }
            if (d.Frozen)
            {
                lines.Add(BankParts.Say(w.T("reallife_econ_account_arranged_pay", "In your debt arrangement: its daily payment pays this. You can pay it sooner here, and the arrangement ends once everything in it is paid.")));
            }
            if (!string.IsNullOrEmpty(d.About))
            {
                lines.Add(BankParts.Say(d.About));
            }
            var steps = new List<UtilitiesStep>(2);
            var moves = new List<MoneyStep>(2);
            var actions = new List<UiActionModel>(4);
            int card = cards.Next;
            bool call = d.CallDue && !d.Frozen;
            if (call)
            {
                if (!d.PlanUsed)
                {
                    actions.Add(BankParts.Button(w.T("reallife_collect_plan", "Agree a payment plan"), UiButtonKind.Secondary, UiIcon.Calendar, MoneyAct.Plan, false, false, d.Number));
                }
                actions.Add(BankParts.Button(w.T("reallife_collect_cannot", "I can't pay"), UiButtonKind.Destructive, UiIcon.Warning, MoneyAct.CannotPay, false, false, d.Number));
                actions.Add(BankParts.Button(w.T("reallife_econ_account_pay_overdue", "Pay what is overdue"), UiButtonKind.Primary, UiIcon.Check, MoneyAct.PayOverdue, true, false, d.Number));
            }
            else
            {
                if (d.Owed > 0f && d.PayMost > 0)
                {
                    int pay = BankParts.Chosen(m.View.F[MoneyView.FPay], d.PaySuggested, 1, d.PayMost);
                    steps.Add(BankParts.AmountStep(w, w.T("reallife_econ_account_pay", "Pay an amount"), pay, 1, d.PayMost, MoneyView.FPay, card, steps.Count,
                        w.F("reallife_fin_range", "{least} to {most}", "least", w.Money(1), "most", w.Money(d.PayMost)), out MoneyStep move));
                    moves.Add(move);
                    if (d.Kind == 3 && d.Minimum > 0 && !d.Frozen && d.DueDay > 0)
                    {
                        actions.Add(BankParts.Button(w.T("reallife_bank_card_pay_minimum", "Pay the minimum"), UiButtonKind.Secondary, UiIcon.Check, MoneyAct.PayMinimum, false, false, d.Number));
                    }
                    actions.Add(BankParts.Button(w.T("reallife_econ_account_pay_off", "Pay it all off"), UiButtonKind.Secondary, UiIcon.Check, MoneyAct.PayOff, false, false, d.Number));
                    actions.Add(BankParts.Button(w.T("reallife_econ_account_pay", "Pay an amount"), UiButtonKind.Primary, UiIcon.Check, MoneyAct.Pay, true, false, d.Number));
                }
                if (d.Kind == 3 && !d.Frozen && d.DrawMost > 0)
                {
                    int draw = BankParts.Chosen(m.View.F[MoneyView.FDraw], d.DrawMost, 1, d.DrawMost);
                    steps.Add(BankParts.AmountStep(w, w.T("reallife_econ_account_draw", "Draw cash"), draw, 1, d.DrawMost, MoneyView.FDraw, card, steps.Count,
                        w.F("reallife_fin_range", "{least} to {most}", "least", w.Money(1), "most", w.Money(d.DrawMost)), out MoneyStep drawMove));
                    moves.Add(drawMove);
                    actions.Add(BankParts.Button(w.T("reallife_econ_account_draw", "Draw cash"), UiButtonKind.Secondary, UiIcon.Coin, MoneyAct.Draw, false, false, d.Number));
                }
                if ((d.Kind == 2 || d.Kind == 3) && d.Owed <= 0f && !d.Frozen)
                {
                    actions.Add(BankParts.Button(w.T("reallife_econ_account_close", "Close it"), UiButtonKind.Destructive, UiIcon.Trash, MoneyAct.Close, false, false, d.Number));
                }
                if (d.GameLoan && !d.Frozen)
                {
                    actions.Add(BankParts.Button(w.T("reallife_bank_daily_payment", "Change the daily payment"), UiButtonKind.Secondary, UiIcon.Gear, MoneyAct.DailyPayment, false, false, d.Number));
                }
            }
            cards.Add(new UtilitiesCard
            {
                Title = d.Name, Sub = d.Opened > 0 ? w.F("reallife_ui_bank_opened", "Opened on day {day}", "day", d.Opened.ToString(Inv)) : null, Chip = d.State, ChipTone = d.Sound ? UiRole.Ok : UiRole.Danger,
                Lines = lines.ToArray(), Steps = steps.ToArray(), Actions = actions.ToArray()
            }, moves.ToArray());
        }
    }
}
