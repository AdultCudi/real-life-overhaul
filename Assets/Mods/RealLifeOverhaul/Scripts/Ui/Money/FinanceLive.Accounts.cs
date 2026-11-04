using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;
using UI.Notification;
using UI.Smartphone.Apps.Persona;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class FinanceLive
    {
        private const string WaysId = "ways", SharkId = "lender", ApprovalId = "approval";

        // ------------------------------------------------------------------------------------------------- Accounts

        // Your loans, accounts and cards, each with what you owe on it, its rate, what it takes next and its state; the one chosen with what can be done about it.
        private void ReadAccounts(FinanceFacts f, FinanceView v)
        {
            FinanceAccountsFacts a = f.Accounts;
            CreditFile file = CreditLedger.File;
            GameInstance save = SaveGameManager.Current;
            UtilitiesWords w = W;
            int today = save.Day;
            var rows = new List<FinanceRow>(12);
            var ids = new List<string>(12);
            float owed = 0f;
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                CreditAccount acct = file.Accounts[i];
                if (acct.Closed)
                {
                    continue;
                }
                owed += acct.Test ? 0f : acct.Owed;
                FinanceApp.NextPayment(acct, today, out int next, out int day);
                bool sound = acct.Ladder.InOrder && !acct.Ladder.Penalty;
                rows.Add(new FinanceRow
                {
                    Name = CreditDesk.Name(acct), Sub = CreditDesk.LenderName(acct), Icon = KindIcon(FinanceTables.Of(acct.Kind)),
                    Cells = new[] { Money(acct.Owed), acct.RateBp > 0 ? Rate(acct.RateNow) : string.Empty,
                        next > 0 && day > 0 ? Money(next) + ", " + FinanceApp.DayWord(day, today) : string.Empty, FinanceText.State(acct) },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, sound ? UiRole.Ok : UiRole.Danger }
                });
                ids.Add("acct:" + acct.Id);
            }
            for (int i = 0; i < file.Loans.Count; i++)
            {
                LoanMark m = file.Loans[i];
                var loan = m.Ref as Loan;
                if (loan == null)
                {
                    continue;
                }
                owed += loan.remainingAmount;
                bool frozen = (m.Held & LoanHold.Frozen) != 0, sound = m.Ladder.InOrder && !m.Ladder.Penalty;
                int kind = FinanceTables.OfLoan(m.Purpose);
                float yearly = FinanceApp.YearlyRate(loan, Math.Max(1, AccountRules.DaysPerYear));
                rows.Add(new FinanceRow
                {
                    Name = FinanceText.T(FinanceTables.DebtKinds[kind]), Sub = GameLoans.BankName(loan), Icon = KindIcon(kind),
                    Cells = new[] { Money(loan.remainingAmount), YieldRate(yearly),
                        frozen ? string.Empty : Money(GameLoans.Due(loan)) + ", " + FinanceApp.DayWord(today + 1, today), FinanceText.State(m.Ladder, frozen, false) },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, sound ? UiRole.Ok : UiRole.Danger }
                });
                ids.Add(FinanceApp.LoanRowId(m));
            }
            if (file.Shark.Active)
            {
                owed += file.Shark.Owed;
                int late = today - file.Shark.DueDay;
                rows.Add(new FinanceRow
                {
                    Name = FinanceText.T("reallife_econ_kind_private"), Sub = FinanceText.T("reallife-lender"), Icon = UiIcon.Handshake,
                    Cells = new[] { Money(file.Shark.Owed), string.Empty, Money(file.Shark.Owed) + ", " + FinanceApp.DayWord(file.Shark.DueDay, today),
                        late > 0 ? FinanceText.T("reallife_econ_row_late", "days", late.ToString(Inv)) : FinanceText.T("reallife_econ_state_ok") },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, late > 0 ? UiRole.Danger : UiRole.Ok }
                });
                ids.Add(SharkId);
            }
            if (file.Relief.HasApproval)
            {
                int until = file.Relief.ApprovedDay + ProductCatalogue.ApprovalDays;
                rows.Add(new FinanceRow
                {
                    Name = FinanceText.T("reallife_econ_row_approval", "kind", FinanceText.T(ProductCatalogue.KindKeys[file.Relief.ApprovedKind])),
                        Sub = FinanceText.T(FinanceTables.DebtKinds[FinanceTables.Approval]),
                    Icon = UiIcon.Check, Cells = new[] { Money(file.Relief.ApprovedAmount), file.Relief.ApprovedRateBp > 0 ? Rate(file.Relief.ApprovedRateBp) : string.Empty, string.Empty,
                        FinanceText.T("reallife_econ_row_until_day", "day", until.ToString(Inv)) },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.Warn }
                });
                ids.Add(ApprovalId);
            }
            if (owed > 0f || file.Relief.Arranged || file.Relief.Claims.Count > 0)
            {
                rows.Add(new FinanceRow
                {
                    Name = FinanceText.T("reallife_econ_row_ways"), Sub = w.T("reallife_fin_ways_sub", "Sell, arrange or start again"), Icon = UiIcon.Scales,
                    Cells = new[] { string.Empty, string.Empty, string.Empty, file.Relief.Arranged ? FinanceText.T("reallife_econ_state_arranged") : string.Empty },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.Warn }
                });
                ids.Add(WaysId);
            }
            a.Ids = ids.ToArray();
            int selected = ids.IndexOf(v.Account);
            if (selected < 0)
            {
                selected = ids.Count > 0 ? 0 : -1;
                v.Account = selected >= 0 ? ids[0] : null;
            }
            a.Headline = w.F("reallife_fin_cash_owe", "Cash {cash}, you owe {owed}", "cash", Money(save.Money), "owed", Money(owed));
            a.Table = FinanceKitTables.Make(FinanceKitTables.AccountColumns(w), rows, selected, w.T("reallife_fin_no_accounts", "No accounts"),
                FinanceText.T("reallife_econ_row_no_accounts"), UiIcon.Wallet);
            a.Card = null;
            a.CardPay = false;
            if (selected >= 0)
            {
                CardOf(a, v, file, today, w);
            }
        }

        private static void CardOf(FinanceAccountsFacts a, FinanceView v, CreditFile file, int today, UtilitiesWords w)
        {
            string id = v.Account;
            if (id == SharkId)
            {
                a.Card = new UtilitiesCard
                {
                    Title = FinanceText.T("reallife_econ_kind_private"), Chip = file.Shark.DueDay < today ? FinanceText.T("reallife_econ_row_late",
                        "days", (today - file.Shark.DueDay).ToString(Inv)) : FinanceText.T("reallife_econ_state_ok"),
                    ChipTone = file.Shark.DueDay < today ? UiRole.Danger : UiRole.Ok,
                    Lines = new[]
                    {
                        Pair(Label("reallife_econ_over_owed"), Money(file.Shark.Owed)),
                        Say(FinanceText.T("reallife_econ_private_terms", "day", file.Shark.DueDay.ToString(Inv), "perDay",
                            Money(SharkRules.ADay(file.Shark.Borrowed)), "cap", Money(SharkRules.Cap(file.Shark.Borrowed)))),
                        Say(FinanceText.T("reallife_econ_private_how"))
                    }
                };
                return;
            }
            if (id == ApprovalId && file.Relief.HasApproval)
            {
                Relief r = file.Relief;
                int held = today - r.ApprovedDay + 1;
                int perDay = ProductTerms.AdvanceCharge(r.ApprovedAmount, r.ApprovedRateBp, 1, AccountRules.DaysPerYear);
                int charge = ReliefRules.CancelCharge(r, today, AccountRules.DaysPerYear);
                a.Card = new UtilitiesCard
                {
                    Title = FinanceText.T("reallife_econ_row_approval", "kind", FinanceText.T(ProductCatalogue.KindKeys[r.ApprovedKind])),
                        Chip = FinanceText.T("reallife_econ_row_until_day", "day", (r.ApprovedDay + ProductCatalogue.ApprovalDays).ToString(Inv)),
                    ChipTone = UiRole.Warn,
                    Lines = new[]
                    {
                        Say(FinanceText.T("reallife_econ_approval_stands", "amount", Money(r.ApprovedAmount), "day",
                            (r.ApprovedDay + ProductCatalogue.ApprovalDays).ToString(Inv), "deposit", r.ApprovedDeposit + "%")),
                        Say(FinanceText.T("reallife_econ_approval_charge", "per_day", Money(perDay), "charge", Money(charge)))
                    },
                    Actions = new[] { CardButton(FinanceText.T("reallife_econ_approval_cancel"), UiButtonKind.Destructive, UiIcon.Trash, FinanceAct.CancelApproval, false) }
                };
                return;
            }
            if (id == WaysId)
            {
                WaysCard(a, file);
                return;
            }
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                if (("acct:" + file.Accounts[i].Id) == id && !file.Accounts[i].Closed)
                {
                    AccountCard(a, file.Accounts[i], today, w);
                    return;
                }
            }
            for (int i = 0; i < file.Loans.Count; i++)
            {
                if (file.Loans[i].Ref is Loan && FinanceApp.LoanRowId(file.Loans[i]) == id)
                {
                    LoanCard(a, file.Loans[i], today);
                    return;
                }
            }
            a.Card = new UtilitiesCard { Title = FinanceText.T("reallife_econ_tab_accounts"), Lines = new[] { Say(FinanceText.T("reallife_econ_accounts_pick")) } };
        }

        private static UtilitiesLine Pair(string label, string value, UiRole ink = UiRole.Text) => new UtilitiesLine { Label = label, Value = value, Ink = ink };

        private static UtilitiesLine Say(string text, UiRole ink = UiRole.Text2) => new UtilitiesLine { Value = text, Ink = ink };

        private static UiActionModel CardButton(string label, UiButtonKind kind, UiIcon icon, FinanceAct act, bool main, bool disabled = false)
        {
            return new UiActionModel { Label = label, Kind = kind, Icon = icon, Key = main ? UiAction.Alt : UiAction.Count,
                Command = FinanceFace.CardAct, Arg = (int)act, Disabled = disabled };
        }

        private static void AccountCard(FinanceAccountsFacts facts, CreditAccount acct, int today, UtilitiesWords w)
        {
            LadderState s = acct.Ladder;
            var lines = new List<UtilitiesLine>(8);
            lines.Add(Pair(Label("reallife_econ_over_owed"), acct.Agreed > 0
                ? FinanceText.T("reallife_econ_row_owed_of", "owed", Money(acct.Owed), "total", Money(acct.Agreed)) : Money(acct.Owed)));
            if (ProductCatalogue.Revolving(acct.Kind) && acct.Agreed > 0)
            {
                float used = acct.Owed / (float)acct.Agreed;
                lines.Add(Pair(FinanceText.T("reallife_ibox_used").TrimEnd(' ', ':'), FinanceText.T("reallife_tab_account_used", "share",
                    ((int)Math.Round(used * 100f)).ToString(Inv) + "%", "free", Money(acct.Free)),
                    used >= 0.9f ? UiRole.DangerText : used >= 0.5f ? UiRole.WarnText : UiRole.OkText));
            }
            else if (ProductCatalogue.Instalment(acct.Kind) && acct.Agreed > 0)
            {
                int paid = Math.Max(0, acct.Agreed - acct.Owed);
                lines.Add(Pair(FinanceText.T("reallife_ibox_paid_off").TrimEnd(' ', ':'), FinanceText.T("reallife_tab_account_paid", "paid",
                    Money(paid), "total", Money(acct.Agreed)), UiRole.OkText));
            }
            if (acct.RateBp > 0)
            {
                lines.Add(Pair(Label("reallife_econ_offer_rate"), Rate(acct.RateNow), acct.Ladder.Penalty ? UiRole.DangerText : UiRole.Text));
            }
            int due = AccountRules.DueTomorrow(acct, today);
            switch (acct.Kind)
            {
                case ProductKind.CreditCard:
                    lines.Add(Say(acct.DueDay > 0
                        ? FinanceText.T("reallife_econ_account_card_due", "minimum", Money(acct.MinimumLeft), "day", acct.DueDay.ToString(Inv), "statement", Money(acct.StatementLeft))
                        : FinanceText.T("reallife_econ_account_card_next", "day", acct.NextDay.ToString(Inv))));
                    break;
                case ProductKind.Overdraft:
                    lines.Add(Say(FinanceText.T("reallife_econ_account_overdraft", "free", Money(acct.Free), "float", Money(ProductCatalogue.OverdraftFloat))));
                    break;
                case ProductKind.Arrears:
                    lines.Add(Say(FinanceText.T("reallife_econ_account_arrears"), UiRole.WarnText));
                    break;
                default:
                    int left = acct.Instalment > 0 ? (int)Math.Ceiling(acct.Owed / (float)acct.Instalment) : 0;
                    lines.Add(Say(FinanceText.T("reallife_econ_account_instalment", "payment", Money(due), "days", left.ToString(Inv))));
                    lines.Add(Say(FinanceText.T(acct.Secured ? "reallife_econ_account_secured" : "reallife_econ_account_unsecured", "thing", CreditDesk.Thing(acct))));
                    break;
            }
            if (s.RepoPending)
            {
                lines.Add(Say(FinanceText.T("reallife_econ_account_repo", "thing", CreditDesk.Thing(acct)), UiRole.DangerText));
            }
            if (s.PlanLeft > 0)
            {
                lines.Add(Say(FinanceText.T("reallife_econ_account_plan", "left", Money(s.PlanLeft), "payment", Money(s.PlanPerDay)), UiRole.WarnText));
            }
            if (acct.Frozen)
            {
                lines.Add(Say(FinanceText.T("reallife_econ_account_arranged_pay")));
            }
            string about = FinanceApp.AboutKind(acct.Kind);
            if (about != null)
            {
                lines.Add(Say(about));
            }
            var actions = new List<UiActionModel>(4);
            if (s.CallDue && !acct.Frozen)
            {
                if (!s.PlanUsed)
                {
                    actions.Add(CardButton(FinanceText.T("reallife_collect_plan"), UiButtonKind.Secondary, UiIcon.Calendar, FinanceAct.AgreePlan, false));
                }
                actions.Add(CardButton(FinanceText.T("reallife_collect_cannot"), UiButtonKind.Destructive, UiIcon.Warning, FinanceAct.CannotPay, false));
                actions.Add(CardButton(FinanceText.T("reallife_econ_account_pay_overdue"), UiButtonKind.Primary, UiIcon.Check, FinanceAct.PayOverdue, true));
            }
            else
            {
                if (!acct.Frozen && acct.Kind == ProductKind.CreditCard)
                {
                    actions.Add(CardButton(FinanceText.T("reallife_econ_account_draw"), UiButtonKind.Secondary, UiIcon.Coin, FinanceAct.Draw, false));
                }
                if (!acct.Frozen && acct.Owed == 0 && (acct.Kind == ProductKind.CreditCard || acct.Kind == ProductKind.Overdraft))
                {
                    actions.Add(CardButton(FinanceText.T("reallife_econ_account_close"), UiButtonKind.Destructive, UiIcon.Trash, FinanceAct.Close, false));
                }
                if (acct.Owed > 0)
                {
                    actions.Add(CardButton(FinanceText.T("reallife_econ_account_pay_off"), UiButtonKind.Secondary, UiIcon.Check, FinanceAct.PayOff, false));
                    actions.Add(CardButton(FinanceText.T("reallife_econ_account_pay"), UiButtonKind.Primary, UiIcon.Check, FinanceAct.Pay, true));
                }
            }
            facts.Card = new UtilitiesCard
            {
                Title = CreditDesk.Name(acct), Sub = FinanceText.T("reallife_tab_account_opened", "lender", CreditDesk.LenderName(acct), "day",
                    acct.OpenedDay.ToString(Inv)), Chip = FinanceText.State(acct),
                ChipTone = s.RepoPending || s.Misses >= 2 ? UiRole.Danger : s.Misses > 0 || s.Overdue > 0 ? UiRole.Warn : UiRole.Ok, Lines = lines.ToArray(), Actions = actions.ToArray()
            };
            if (acct.Owed > 0 && !(s.CallDue && !acct.Frozen))
            {
                int suggested = acct.Frozen ? acct.Owed : Math.Min(acct.Owed, Math.Max(s.Overdue, due));
                PayRange(facts, suggested, acct.Owed);
            }
        }

        private static void PayRange(FinanceAccountsFacts facts, int suggested, int most)
        {
            facts.CardPay = most > 0;
            facts.PayLeast = 1;
            facts.PayMost = most;
            facts.PayAmount = Math.Max(1, Math.Min(most, facts.PayAmount > 0 ? facts.PayAmount : suggested));
            facts.PayPrev = FinanceRules.StepAmount(facts.PayAmount, 1, most, false, FinanceApp.AmountSteps);
            facts.PayNext = FinanceRules.StepAmount(facts.PayAmount, 1, most, true, FinanceApp.AmountSteps);
        }

        private static void LoanCard(FinanceAccountsFacts facts, LoanMark m, int today)
        {
            var loan = (Loan)m.Ref;
            bool frozen = (m.Held & LoanHold.Frozen) != 0, penalty = (m.Held & LoanHold.Penalty) != 0;
            var lines = new List<UtilitiesLine>(8)
            {
                Pair(Label("reallife_econ_over_owed"), FinanceText.T("reallife_econ_row_owed_of", "owed", Money(loan.remainingAmount), "total", Money(loan.totalAmount))),
                Pair(Label("reallife_econ_offer_payment"), Money(loan.dailyPayment))
            };
            if (loan.dailyPayment > 0 && !frozen)
            {
                int left = (int)Math.Ceiling(loan.remainingAmount / loan.dailyPayment);
                lines.Add(Say(FinanceText.T("reallife_tab_loan_left", "days", left.ToString(Inv), "day", (today + left).ToString(Inv))));
            }
            float paid = Math.Max(0f, loan.PaidAmount);
            lines.Add(Pair(FinanceText.T("reallife_ibox_paid_off").TrimEnd(' ', ':'), FinanceText.T("reallife_tab_account_paid", "paid", Money(paid),
                "total", Money(loan.totalAmount)), UiRole.OkText));
            float yearly = FinanceApp.YearlyRate(loan, Math.Max(1, AccountRules.DaysPerYear));
            if (yearly > 0f)
            {
                lines.Add(Pair(Label("reallife_econ_offer_rate"), FinanceText.T("reallife_econ_row_rate", "rate", yearly.ToString("0.#", Inv)), penalty ? UiRole.DangerText : UiRole.Text));
            }
            lines.Add(Pair(Label("reallife_econ_offer_interest"), Money(loan.dailyInterest)));
            if (m.Purpose == LoanPurpose.Business)
            {
                BuildingRegistration shop = Books.Business(m.Security);
                lines.Add(Say(shop != null ? FinanceText.T("reallife_econ_account_secured", "thing", shop.BusinessName) : FinanceText.T("reallife_econ_loan_unsecured")));
            }
            if (m.Ladder.PlanLeft > 0)
            {
                lines.Add(Say(FinanceText.T("reallife_econ_account_plan", "left", Money(m.Ladder.PlanLeft), "payment", Money(m.Ladder.PlanPerDay)), UiRole.WarnText));
            }
            if (m.Ladder.RepoDay > 0)
            {
                lines.Add(Say(FinanceText.T("reallife_econ_loan_takings", "share",
                    ((int)Math.Round(ProductCatalogue.BusinessDivertShare * 100f)) + "%", "amount", Money(m.Ladder.Overdue)), UiRole.DangerText));
            }
            if (frozen)
            {
                lines.Add(Say(FinanceText.T("reallife_econ_account_arranged_pay")));
            }
            lines.Add(Say(FinanceText.T("reallife_tab_loan_about", "kind", FinanceText.T(FinanceTables.DebtKinds[FinanceTables.OfLoan(m.Purpose)]), "day", m.FirstDay.ToString(Inv))));
            var actions = new List<UiActionModel>(3);
            if (m.Ladder.CallDue)
            {
                if (!m.Ladder.PlanUsed)
                {
                    actions.Add(CardButton(FinanceText.T("reallife_collect_plan"), UiButtonKind.Secondary, UiIcon.Calendar, FinanceAct.AgreePlan, false));
                }
                actions.Add(CardButton(FinanceText.T("reallife_collect_cannot"), UiButtonKind.Destructive, UiIcon.Warning, FinanceAct.CannotPay, false));
                actions.Add(CardButton(FinanceText.T("reallife_econ_account_pay_overdue"), UiButtonKind.Primary, UiIcon.Check, FinanceAct.PayOverdue, true));
            }
            else
            {
                actions.Add(CardButton(FinanceText.T("reallife_econ_open_econoview"), UiButtonKind.Primary, UiIcon.Check, FinanceAct.EconoView, true));
            }
            facts.Card = new UtilitiesCard
            {
                Title = GameLoans.BankName(loan), Sub = FinanceText.T(FinanceTables.DebtKinds[FinanceTables.OfLoan(m.Purpose)]), Chip = FinanceText.State(m.Ladder, frozen, false),
                ChipTone = m.Ladder.RepoPending || m.Ladder.Misses >= 2 ? UiRole.Danger : m.Ladder.Misses > 0 || m.Ladder.Overdue > 0 ? UiRole.Warn
                    : UiRole.Ok, Lines = lines.ToArray(), Actions = actions.ToArray()
            };
        }

        private static void WaysCard(FinanceAccountsFacts facts, CreditFile file)
        {
            CreditDesk desk = Desk;
            if (desk == null)
            {
                facts.Card = new UtilitiesCard { Title = FinanceText.T("reallife_econ_row_ways") };
                return;
            }
            desk.Owed(out int loans, out int unsecured, out int secured, out int shark, out int other);
            PersonalWealthData wealth = Helpers.PlayerHelper.GetPersonalWealth();
            var lines = new List<UtilitiesLine>(8)
            {
                Pair(Label("reallife_econ_over_owed"), Money(loans + unsecured + secured + shark + other)),
                Say(FinanceText.T("reallife_econ_ways_sell_line", "cash", Money(wealth.cash), "investments", Money(wealth.totalInvestments), "assets", Money(wealth.totalAssets)))
            };
            ProductAnswer arrange = desk.ArrangementOffer(out ArrangementPlan plan);
            if (file.Relief.Arranged)
            {
                lines.Add(Say(FinanceText.Fill("reallife_econ_ways_arranged", "paid", Money(file.Relief.ArrangedPaid), "total",
                    Money(file.Relief.ArrangedTotal), "payment", Money(file.Relief.ArrangedPerDay),
                    "count", file.Relief.ArrangedMisses.ToString(Inv), "most", ProductCatalogue.ArrangementMissesMost.ToString(Inv))));
            }
            else if (arrange == ProductAnswer.Yes)
            {
                lines.Add(Say(FinanceText.T("reallife_econ_ways_arrange_line", "amount", Money(plan.Total), "payment", Money(plan.PerDay), "days", plan.Days.ToString(Inv))));
            }
            else
            {
                lines.Add(Say(FinanceApp.Why(arrange), UiRole.WarnText));
            }
            ProductAnswer bankrupt = desk.BankruptcyOffer(out BankruptcyPlan ruin);
            lines.Add(Say(bankrupt == ProductAnswer.Yes
                ? FinanceText.T("reallife_econ_ways_bankrupt_line", "amount", Money(ruin.WrittenOff), "cash", Money(ruin.CashTaken), "days",
                    ProductCatalogue.BankruptNoCreditDays.ToString(Inv))
                : bankrupt == ProductAnswer.Busy ? FinanceText.T("reallife_advice_game_quest")
                : bankrupt == ProductAnswer.Bankrupt ? FinanceText.T("reallife_advice_bankrupt_soon", "days", ProductCatalogue.BankruptEveryDays.ToString(Inv))
                : FinanceApp.Why(bankrupt)));
            var actions = new List<UiActionModel>(2);
            if (bankrupt == ProductAnswer.Yes)
            {
                actions.Add(CardButton(FinanceText.T("reallife_econ_ways_bankrupt_do"), UiButtonKind.Destructive, UiIcon.Warning, FinanceAct.Bankrupt, false));
            }
            if (arrange == ProductAnswer.Yes && !file.Relief.Arranged)
            {
                actions.Add(CardButton(FinanceText.T("reallife_econ_ways_arrange_do"), UiButtonKind.Primary, UiIcon.Check, FinanceAct.Arrange, true));
            }
            facts.Card = new UtilitiesCard { Title = FinanceText.T("reallife_econ_row_ways"), Lines = lines.ToArray(), Actions = actions.ToArray() };
        }

        // ------------------------------------------------------------------------------------------------- doing

        private bool DoAccount(FinanceAct act, FinanceView v)
        {
            CreditDesk desk = Desk;
            CreditFile file = CreditLedger.File;
            if (desk == null || file == null)
            {
                return false;
            }
            if (act == FinanceAct.Arrange)
            {
                return Arrange(desk);
            }
            if (act == FinanceAct.Bankrupt)
            {
                return Bankrupt(desk);
            }
            if (act == FinanceAct.CancelApproval)
            {
                return CancelApproval(desk, file);
            }
            CreditAccount acct = null;
            LoanMark mark = null;
            for (int i = 0; i < file.Accounts.Count; i++)
            {
                acct = ("acct:" + file.Accounts[i].Id) == v.Account ? file.Accounts[i] : acct;
            }
            for (int i = 0; i < file.Loans.Count; i++)
            {
                mark = file.Loans[i].Ref is Loan && FinanceApp.LoanRowId(file.Loans[i]) == v.Account ? file.Loans[i] : mark;
            }
            if (act == FinanceAct.PayOverdue || act == FinanceAct.AgreePlan || act == FinanceAct.CannotPay)
            {
                if (acct == null && mark == null)
                {
                    return false;
                }
                Debt debt = acct != null ? new Debt(acct) : new Debt(mark);
                if (act == FinanceAct.CannotPay)
                {
                    desk.CannotPay(debt);
                    return true;
                }
                return Said(act == FinanceAct.PayOverdue ? desk.PayOverdue(debt) : desk.AgreePlan(debt));
            }
            if (acct == null)
            {
                return false;
            }
            string name = CreditDesk.Name(acct);
            switch (act)
            {
                case FinanceAct.Pay:
                case FinanceAct.PayOff:
                    {
                        int amount = act == FinanceAct.PayOff ? acct.Owed : Math.Max(1, Math.Min(acct.Owed, v.Pay > 0 ? v.Pay : Math.Min(acct.Owed,
                            Math.Max(acct.Ladder.Overdue, AccountRules.DueTomorrow(acct, RivalGame.Day)))));
                        ProductAnswer answer = desk.Pay(acct, amount, out int used);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, acct.Closed ? "reallife_econ_note_paid_off" : "reallife_econ_note_paid", new Dictionary<string, string>
                            {
                                { "account", name }, { "amount", Money(used) }, { "left", Money(acct.Owed) }
                            });
                            v.Pay = 0;
                        }
                        return Said(answer);
                    }
                case FinanceAct.Draw:
                    {
                        int amount = Math.Max(1, Math.Min(AccountRules.MostDraw(acct), v.Pay > 0 ? v.Pay : AccountRules.MostDraw(acct)));
                        ProductAnswer answer = desk.Draw(acct, amount, out int fee);
                        if (answer == ProductAnswer.Yes)
                        {
                            Tell(NotificationType.Success, "reallife_econ_note_drawn", new Dictionary<string, string> { { "amount", Money(amount) },
                                { "fee", Money(fee) }, { "owed", Money(acct.Owed) } });
                        }
                        return Said(answer);
                    }
                case FinanceAct.Close:
                    return Said(desk.CloseAccount(acct));
                default:
                    return false;
            }
        }

        private bool CancelApproval(CreditDesk desk, CreditFile file)
        {
            Relief relief = file.Relief;
            if (!relief.HasApproval)
            {
                return false;
            }
            int today = RivalGame.Day, held = today - relief.ApprovedDay + 1;
            int charge = ReliefRules.CancelCharge(relief, today, AccountRules.DaysPerYear);
            Lender bank = BankTerms.At(relief.ApprovedStreet, relief.ApprovedNumber);
            GameConfirm.Show("reallife_econ_approval_cancel".Localize(), "reallife_econ_confirm_cancel_approval".Localize(new
                {
                    amount = Money(relief.ApprovedAmount), lender = bank != null ? bank.Name : FinanceText.T("reallife_econ_lender_bank"), charge = Money(charge), days = held.ToString()
                }),
                () => Guarded(() => desk.CancelApproval(false)), null, "reallife_econ_approval_cancel", "common_cancel", false, true);
            return true;
        }

        private bool Arrange(CreditDesk desk)
        {
            ProductAnswer arrange = desk.ArrangementOffer(out ArrangementPlan plan);
            if (arrange != ProductAnswer.Yes)
            {
                return Said(arrange);
            }
            GameConfirm.Show("reallife_econ_ways_arrange".Localize(), "reallife_advice_arrange_page".Localize(new
                {
                    amount = Money(plan.Total), payment = Money(plan.PerDay), days = plan.Days.ToString(), cap = ProductCatalogue.ArrangementScoreCap.ToString(),
                    after = ProductCatalogue.ArrangementCapDaysAfter.ToString(), misses = ProductCatalogue.ArrangementMissesMost.ToString()
                }),
                () => Guarded(() =>
                {
                    ProductAnswer answer = desk.Arrange();
                    if (answer == ProductAnswer.Yes)
                    {
                        Tell(NotificationType.Info, "reallife_econ_note_arranged", new Dictionary<string, string> { { "amount", Money(plan.Total) }, { "payment", Money(plan.PerDay) } });
                    }
                    else
                    {
                        Said(answer);
                    }
                }), null, "reallife_econ_ways_arrange_do", "common_cancel", false, false);
            return true;
        }

        private bool Bankrupt(CreditDesk desk)
        {
            ProductAnswer bankrupt = desk.BankruptcyOffer(out BankruptcyPlan ruin);
            if (bankrupt != ProductAnswer.Yes)
            {
                return Said(bankrupt);
            }
            GameConfirm.Show("reallife_econ_ways_bankrupt".Localize(), "reallife_advice_bankrupt_page".Localize(new
                {
                    amount = Money(ruin.WrittenOff), kept = Money(ruin.Kept), cash = Money(ruin.CashTaken), left = Money(ruin.CashLeft),
                        days = ProductCatalogue.BankruptNoCreditDays.ToString(),
                    from = ProductCatalogue.BankruptRebuildFrom.ToString(), every = ProductCatalogue.BankruptEveryDays.ToString()
                }),
                () => Guarded(() => Said(desk.Bankrupt())), null, "reallife_econ_ways_bankrupt_do", "common_cancel", false, true);
            return true;
        }
    }
}
