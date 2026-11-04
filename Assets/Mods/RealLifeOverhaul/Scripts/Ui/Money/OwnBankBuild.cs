using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Writes the Your bank page from the facts of the bank: its level and how far it is from the next, its books, the owner's pay and
    /// dividends, the savers and borrowers it serves, the fund and the branches, each tab its own page. Every button is an act of the
    /// director the old app used, or is drawn disabled with its reason. Game-free: the live screen and the preview's samples both come
    /// through it. Cost: a pass over a few dozen rows when the facts or a choice change.
    /// </summary>
    public static partial class OwnBankBuild
    {
        public const int BankTab = 0, SaversTab = 1, LendingTab = 2, FundTab = 3, BranchesTab = 4, Tabs = 5;
        public const string LevelRow = "b|level", PayRow = "b|pay", DivRow = "b|div";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Fill(MoneyModel m, OwnBankFacts f, UtilitiesWords w)
        {
            m.App = MoneyApp.OwnBank;
            m.Icon = UiIcon.PiggyBank;
            m.Across = false;
            m.Shape = MoneyShape.Counter;
            m.Title = w.T("reallife_ob_app", "Your bank");
            m.Nav = new[]
            {
                Tab(w, "reallife_ob_tab_bank", "Bank", UiIcon.Bank), Tab(w, "reallife_ob_tab_savers", "Savers", UiIcon.PiggyBank), Tab(w, "reallife_ob_tab_lending", "Lending", UiIcon.Coin),
                Tab(w, "reallife_ob_tab_fund", "Fund", UiIcon.ChartPie), Tab(w, "reallife_branch_tab", "Branches", UiIcon.Building)
            };
            m.Tab = Math.Max(0, Math.Min(Tabs - 1, m.Tab));
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = w.T("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Confirm, Label = w.T("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = w.T("reallife_ui_prompt_back", "Back") }
            };
            m.EmptyIcon = UiIcon.PiggyBank;
            if (!f.Ready)
            {
                m.Shape = MoneyShape.Table;
                m.EmptyTitle = w.T("reallife_ob_off_title", "Your bank is not running");
                m.EmptyBody = w.T("reallife_ob_off_body", "Switch on Your own bank and the credit rating on the options page, and load a save.");
                return;
            }
            var cards = new BankCards();
            if (f.State != 1)
            {
                if (m.Tab == BankTab)
                {
                    NotRunning(m, f, w, cards);
                }
                else
                {
                    m.Shape = MoneyShape.Table;
                    m.EmptyTitle = m.Tab == SaversTab ? w.T("reallife_ob_tab_savers", "Savers") : m.Tab == LendingTab ? w.T("reallife_ob_tab_lending", "Lending")
                        : m.Tab == FundTab ? w.T("reallife_ob_tab_fund", "Fund") : w.T("reallife_branch_tab", "Branches");
                    m.EmptyBody = w.T("reallife_ob_box_none_yet", "Found your bank on the Bank tab first.");
                }
                cards.Into(m);
                return;
            }
            switch (m.Tab)
            {
                case SaversTab: SaversPage(m, f, w, cards); break;
                case LendingTab: LendingPage(m, f, w, cards); break;
                case FundTab: FundPage(m, f, w, cards); break;
                case BranchesTab: BranchesPage(m, f, w, cards); break;
                default: BankPage(m, f, w, cards); break;
            }
            cards.Into(m);
            BankParts.Tidy(m);
        }

        private static UiNavEntry Tab(UtilitiesWords w, string key, string english, UiIcon icon) => new UiNavEntry { Icon = icon, Label = w.T(key, english) };

        // ------------------------------------------------------------------------------------------------- figures

        internal static string C(UtilitiesWords w, long cents) => w.Money(cents / 100f);

        internal static string Signed(UtilitiesWords w, long cents) => cents < 0 ? "-" + w.Money(-cents / 100f) : w.Money(cents / 100f);

        internal static string Pct(int thousandths) => (thousandths / 10f).ToString("0.#", Inv) + "%";

        internal static string Rate(int bp) => (bp / 100f).ToString("0.##", Inv) + "%";

        internal static string Share(long part, long whole) => whole > 0 ? (part * 1000.0 / whole / 10.0).ToString("0.#", Inv) + "%" : "-";

        internal static string Points(UtilitiesWords w, int bp)
        {
            return bp == 0 ? w.T("reallife_ob_points_same", "the same")
                : w.F("reallife_ob_points", "{points} points", "points", (bp > 0 ? "+" : "-") + (Math.Abs(bp) / 100f).ToString("0.##", Inv));
        }

        // ------------------------------------------------------------------------------------------------- not running

        // Before the first bank, or after one was sold or closed: what a licence costs and what it needs, and the button that founds it.
        private static void NotRunning(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            string kicker = f.State == 3 ? w.T("reallife_ibox_chip_closed", "Closed") : f.State == 2 ? w.T("reallife_ibox_chip_sold", "Sold") : w.T("reallife_ui_ob_kicker_new", "A bank of your own");
            string line = f.State == 3
                ? w.F("reallife_ob_box_closed", "Closed by the regulator on {day}: {why}. Winding up {bank} left you {left}.", "bank", f.Name, "day", f.EndDay.ToString(Inv), "why",
                    f.Why ?? string.Empty,
                    "left", C(w, f.EndCents))
                : f.State == 2 ? w.F("reallife_ob_box_sold", "Sold {bank} on {day} for {price}.", "bank", f.Name, "day", f.EndDay.ToString(Inv), "price", C(w, f.EndCents))
                : w.F("reallife_ob_box_intro", "Licence: {licence}. Capital paid in before the first saver: {capital}, and kept at {ratio} or more of your loans "
                    + "and investments, weighted by their risk.",
                    "licence", C(w, f.Licence), "capital", C(w, f.FoundCapital), "ratio", Pct(f.LawRatio));
            m.Hero = new UiHeroModel { Icon = UiIcon.PiggyBank, Kicker = kicker, Title = f.State == 0 ? w.T("reallife_ob_box_found", "Found a bank") : f.Name, Line = line, Progress = -1 };
            var lines = new List<UtilitiesLine>(8)
            {
                BankParts.Pair(w.T("reallife_ibox_licence", "Licence"), C(w, f.Licence)),
                BankParts.Pair(w.T("reallife_ui_ob_found_capital", "Capital paid in"), C(w, f.FoundCapital)),
                BankParts.Pair(w.T("reallife_ui_ob_law", "The law's least capital"), Pct(f.LawRatio)),
                BankParts.Check(f.HasHq ? w.F("reallife_ob_box_hq", "Run from your headquarters: {name}", "name", f.HqName) : w.T("reallife_ob_box_need_hq",
                    "A bank is run from a headquarters. Rent one first."), f.HasHq),
                BankParts.Say(w.F("reallife_ob_box_intro_level", "It starts as a {level}. {holds} It moves up a level as it grows.", "level", f.Level, "holds", f.CommunityHolds)),
                BankParts.Say(w.F("reallife_ob_box_rivals", "Rival banks: savers get {savers} a year, good borrowers pay {loans}.", "savers", Rate(f.RivalSaverBp), "loans", Rate(f.RivalLoanBp)))
            };
            string reason = null;
            if (f.AgainWait != null)
            {
                reason = f.AgainWait;
                lines.Add(BankParts.Say(f.AgainWait, UiRole.WarnText));
            }
            else if (!f.HasHq)
            {
                reason = w.T("reallife_ob_box_need_hq", "A bank is run from a headquarters. Rent one first.");
            }
            if (f.State == 3 && f.Closures > 0)
            {
                lines.Add(BankParts.Say(w.F("reallife_ob_box_closed_terms", "Your next bank pays in {capital} of capital, more than the {first} a first bank needs.", "capital", C(w,
                    f.NextCapital), "first", C(w, f.FoundCapital))));
            }
            cards.Add(new UtilitiesCard
            {
                Title = w.T("reallife_ob_box_found", "Found a bank"), Sub = f.State == 0 ? null : f.Name, Lines = lines.ToArray(),
                Actions = new[] { BankParts.Button(w.T("reallife_ob_box_found", "Found a bank"), UiButtonKind.Primary, UiIcon.Check, MoneyAct.Found, true, reason != null) }
            });
        }

        // ------------------------------------------------------------------------------------------------- Bank

        // The bank's hero (its level, how far from the next), its figures, its books as rows, and beside them what the row picked is about.
        private static void BankPage(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            int progress = LevelProgress(f, out int met);
            string next = f.NextLevel != null ? w.F("reallife_ui_ob_next", "Next: {level}, {met} of 3 conditions met", "level", f.NextLevel, "met", met.ToString(Inv))
                : w.T("reallife_ob_box_level_top", "Your bank is at the top level: its loans and savings have no limit but the regulator's ratio.");
            m.Hero = new UiHeroModel
            {
                Icon = UiIcon.PiggyBank, Kicker = f.Level, Title = f.Name, Line = f.StageWords + (f.Run ? ". " + w.T("reallife_ob_box_run",
                    "A run on the bank: no lending until the savers stop.") : string.Empty),
                Progress = f.NextLevel != null ? progress : -1, Note = next, NoteIcon = UiIcon.Flag
            };
            m.Figures = new[]
            {
                new UiFigureModel { Label = w.T("reallife_ob_row_deposits", "Savers' deposits"), Value = C(w, f.Deposits) },
                new UiFigureModel { Label = w.T("reallife_ob_row_loans", "Loans"), Value = C(w, f.Lent) },
                new UiFigureModel { Label = w.T("reallife_ob_row_capital", "Capital"), Value = C(w, f.Capital) },
                new UiFigureModel { Label = w.T("reallife_ob_box_ratio", "Capital ratio:"), Value = Pct(f.Ratio) }
            };
            var rows = new List<UiDataRow>(12);
            var ids = new List<string>(12);
            long assets = f.Reserves + f.Lent + f.Arrears + f.Fund;
            AddBook(rows, ids, w, "b|reserves", "reallife_ob_row_reserves", "Reserves at the central bank", f.Reserves, w.F("reallife_ob_note_reserves", "at least {need} kept for savers",
                "need", C(w, f.ReserveNeed)), f.Reserves < f.ReserveNeed ? UiRole.WarnText : UiRole.None);
            AddBook(rows, ids, w, "b|loans", "reallife_ob_row_loans", "Loans", f.Lent, w.F("reallife_ob_note_share", "{share} of the assets", "share", Share(f.Lent, assets)), UiRole.None);
            AddBook(rows, ids, w, "b|arrears", "reallife_ob_row_arrears", "Loans in arrears", f.Arrears, w.F("reallife_ob_note_written", "written off in all {lost}, come back {back}",
                "lost", C(w, f.LostAll), "back", C(w, f.RecoveredAll)),
                f.Arrears > 0 ? UiRole.WarnText : UiRole.None);
            AddBook(rows, ids, w, "b|fund", "reallife_ob_row_fund", "The fund at market prices", f.Fund, w.F("reallife_ob_note_fund", "{gain} against what it cost", "gain", Signed(w,
                f.Fund - f.FundCost)), UiRole.None);
            AddBook(rows, ids, w, "b|deposits", "reallife_ob_row_deposits", "Savers' deposits", f.Deposits, w.F("reallife_ob_note_city", "{share} of the city's savings", "share",
                Share(f.Deposits, f.CitySavings)), UiRole.None);
            AddBook(rows, ids, w, "b|capital", "reallife_ob_row_capital", "Capital", f.Capital, w.F("reallife_ob_note_ratio", "capital ratio {ratio}, the law's least {law}", "ratio",
                Pct(f.Ratio), "law", Pct(f.LawRatio)),
                f.Ratio < f.LawRatio ? UiRole.DangerText : f.Ratio < f.WatchRatio ? UiRole.WarnText : UiRole.OkText);
            AddBook(rows, ids, w, "b|risk", "reallife_ob_row_risk", "Assets weighted by risk", f.Risk, w.T("reallife_ob_note_risk", "what the regulator measures the capital against"), UiRole.None);
            // The three rows that open a page of their own.
            rows.Add(BankParts.Row(new[] { w.T("reallife_ob_row_level", "Level"), f.Level, LevelHolds(f, w) }));
            ids.Add(LevelRow);
            rows.Add(BankParts.Row(new[] { w.T("reallife_ob_row_pay", "Your pay this month"), C(w, f.PaidMonth), PayNote(f, w) }));
            ids.Add(PayRow);
            rows.Add(BankParts.Row(new[] { w.T("reallife_ob_row_div", "Automatic dividends"), C(w, f.DivAll), f.DivEvery == 0 ? w.T("reallife_ob_note_div_off",
                "Off: a dividend only when you take one.")
                : w.F("reallife_ob_note_div_on", "{every}: {share} of the profit kept.", "every", EveryName(f), "share", Pct(f.DivShare)) }));
            ids.Add(DivRow);
            int selected = ids.IndexOf(m.View.Row);
            m.Table = BankParts.Table(new[] { w.T("reallife_ob_col_item", "Item"), w.T("reallife_ob_col_amount", "Amount"), w.T("reallife_ob_col_note", "Note") },
                new[] { UiCellKind.Text, UiCellKind.Figure, UiCellKind.Text }, rows, selected, w.T("reallife_ui_ob_books_empty", "Nothing yet"), string.Empty, UiIcon.PiggyBank);
            m.RowIds = ids.ToArray();
            if (selected < 0)
            {
                m.View.Row = null;
                CapitalCard(m, f, w, cards);
            }
            else if (m.View.Row == LevelRow)
            {
                LevelCard(m, f, w, cards);
            }
            else if (m.View.Row == PayRow)
            {
                PayCard(m, f, w, cards);
            }
            else if (m.View.Row == DivRow)
            {
                DividendCard(m, f, w, cards);
            }
            else
            {
                CapitalCard(m, f, w, cards);
            }
            MonthCard(f, w, cards);
        }

        private static void AddBook(List<UiDataRow> rows, List<string> ids, UtilitiesWords w, string id, string key, string english, long cents, string note, UiRole tone)
        {
            rows.Add(BankParts.Row(new[] { w.T(key, english), C(w, cents), note }, BankParts.Tones(3, 1, tone)));
            ids.Add(id);
        }

        // How far the bank is from the next level, 0 to 100: the three conditions, each counted as far as it goes.
        public static int LevelProgress(OwnBankFacts f, out int met)
        {
            met = 0;
            if (f.NextLevel == null)
            {
                return 100;
            }
            double record = f.RecordMonths > 0 ? Math.Min(1.0, f.HeldMonths / (double)f.RecordMonths) : 1.0;
            double capital = f.CapitalLeast > 0 ? Math.Min(1.0, Math.Max(0L, f.Capital) / (double)f.CapitalLeast) : 1.0;
            double regulator = f.RegulatorOk ? 1.0 : 0.0;
            met = (record >= 1.0 ? 1 : 0) + (capital >= 1.0 ? 1 : 0) + (f.RegulatorOk ? 1 : 0);
            return (int)Math.Round((record + capital + regulator) / 3.0 * 100.0);
        }

        private static string EveryName(OwnBankFacts f) => f.DivEvery >= 0 && f.DivEvery < f.DivEveryNames.Length ? f.DivEveryNames[f.DivEvery] : string.Empty;

        private static string LevelHolds(OwnBankFacts f, UtilitiesWords w)
        {
            return f.Top ? w.F("reallife_ob_note_level_top", "Up to {branches} branches; no limit on loans or savings but the regulator's.", "branches", f.BranchesMost.ToString(Inv))
                : w.F("reallife_ob_note_level", "Up to {branches} branches, {loans} of loans and {deposits} of savings.", "branches", f.BranchesMost.ToString(Inv),
                    "loans", f.LoansMost < 0 ? "-" : C(w, f.LoansMost), "deposits", f.DepositsMost < 0 ? "-" : C(w, f.DepositsMost));
        }

        private static string PayNote(OwnBankFacts f, UtilitiesWords w)
        {
            if (f.PayMode == 1 && f.PayFixed > 0)
            {
                return w.F("reallife_ob_note_pay_fixed", "{amount} every day, whatever the day's profit.", "amount", C(w, f.PayFixed));
            }
            if (f.PayMode == 0 && f.PayShare > 0)
            {
                return w.F("reallife_ob_note_pay_share", "{share} of each day's profit; the rest stays in the bank.", "share", Pct(f.PayShare));
            }
            return w.T("reallife_ob_note_pay_none", "All of each day's profit stays in the bank.");
        }

        // ------------------------------------------------------------------------------------------------- the cards of the Bank tab

        // The default card: the capital in the bank, put in and taken out, and the bank's sale.
        private static void CapitalCard(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var lines = new List<UtilitiesLine>(8)
            {
                BankParts.Pair(w.T("reallife_ob_row_capital", "Capital"), C(w, f.Capital)),
                BankParts.Pair(w.T("reallife_ob_box_ratio", "Capital ratio:"), w.F("reallife_ob_box_ratio_value", "{ratio} (law {law}, watched under {watch})", "ratio", Pct(f.Ratio), "law",
                    Pct(f.LawRatio), "watch", Pct(f.WatchRatio)),
                    f.Ratio < f.LawRatio ? UiRole.DangerText : f.Ratio < f.WatchRatio ? UiRole.WarnText : UiRole.Text),
                BankParts.Pair(w.T("reallife_ob_box_year", "Last year's profit:"), Signed(w, f.YearProfit), f.YearProfit < 0 ? UiRole.DangerText : f.YearProfit > 0 ? UiRole.OkText : UiRole.Text),
                BankParts.Pair(w.T("reallife_ob_box_value", "Sale value:"), C(w, f.SaleValue))
            };
            if (!string.IsNullOrEmpty(f.Crews))
            {
                lines.Add(BankParts.Say(f.Crews));
            }
            if (f.Test)
            {
                lines.Add(BankParts.Say(w.T("reallife_ob_box_test", "Founded by a test: it pays nothing out.")));
            }
            long step = 100000L;
            int put = BankParts.Chosen(m.View.F[MoneyView.FPut], 1000000, 100000, 999999999);
            int card = cards.Next;
            var steps = new[]
            {
                BankParts.AmountStep(w, w.T("reallife_ui_ob_amount", "Amount"), put, 100000, 999999999, MoneyView.FPut, card, 0,
                    w.F("reallife_ui_ob_in_steps", "In whole money, in steps of {step}", "step", w.Money(step)), out MoneyStep move)
            };
            var actions = new List<UiActionModel>(3)
            {
                BankParts.Button(w.T("reallife_ob_box_put_in", "Put in capital"), UiButtonKind.Primary, UiIcon.Plus, MoneyAct.PutIn, true),
                BankParts.Button(w.T("reallife_ob_box_take_out", "Take a dividend"), UiButtonKind.Secondary, UiIcon.Minus, MoneyAct.TakeOut, false, f.MostOut <= 0)
            };
            if (f.MostOut <= 0)
            {
                lines.Add(BankParts.Say(w.T("reallife_ui_ob_take_none", "None can be taken out now: it would leave the bank short of capital."), UiRole.WarnText));
            }
            else
            {
                lines.Add(BankParts.Say(w.F("reallife_ui_ob_take_most", "Up to {most} can be taken out now.", "most", C(w, f.MostOut))));
            }
            if (f.Sellable)
            {
                actions.Add(BankParts.Button(w.T("reallife_ob_box_sell", "Sell the bank"), UiButtonKind.Destructive, UiIcon.Trash, MoneyAct.Sell, false));
            }
            else
            {
                lines.Add(BankParts.Say(w.T("reallife_ui_ob_sell_not", "It cannot be sold while it is in breach or a run is on."), UiRole.WarnText));
                actions.Add(BankParts.Button(w.T("reallife_ob_box_sell", "Sell the bank"), UiButtonKind.Destructive, UiIcon.Trash, MoneyAct.Sell, false, true));
            }
            cards.Add(new UtilitiesCard { Title = w.T("reallife_ui_ob_capital_title", "Capital and sale"), Sub = f.Name, Lines = lines.ToArray(), Steps = steps,
                Actions = actions.ToArray() }, new[] { move });
        }

        // The month so far: what came in, what went out, what was written off and what you were paid.
        private static void MonthCard(OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var lines = new List<UtilitiesLine>(6)
            {
                BankParts.Pair(w.T("reallife_ui_ob_income", "Income"), C(w, f.MonthIncome)),
                BankParts.Pair(w.T("reallife_ui_ob_costs", "Costs"), C(w, f.MonthCosts)),
                BankParts.Pair(w.T("reallife_ui_ob_written", "Written off"), C(w, f.MonthLost)),
                BankParts.Pair(w.T("reallife_ui_ob_paid", "Paid to you"), C(w, f.PaidMonth))
            };
            if (f.HasLast)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ob_box_last", "Last month:"), w.F("reallife_ob_box_last_value", "income {income}, costs {costs}, profit {profit}", "income", C(w,
                    f.LastIncome), "costs", C(w, f.LastCosts),
                    "profit", Signed(w, f.LastIncome - f.LastCosts))));
            }
            cards.Add(new UtilitiesCard { Title = w.T("reallife_ui_ob_month_title", "This month"), Heading = true, Lines = lines.ToArray() });
        }

        // The level's page: what the bank holds against the level's limits, then the next level's conditions ticked as they are met, and the move up.
        private static void LevelCard(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var lines = new List<UtilitiesLine>(10)
            {
                BankParts.Pair(w.T("reallife_ob_meter_deposits", "Savings held"), f.DepositsMost < 0 ? w.F("reallife_ob_figure_no_limit", "{now}, no limit", "now", C(w, f.Deposits))
                    : w.F("reallife_ob_figure_of", "{now} of {most}", "now", C(w, f.Deposits), "most", C(w, f.DepositsMost)),
                        f.DepositsMost >= 0 && f.Deposits >= f.DepositsMost ? UiRole.WarnText : UiRole.Text),
                BankParts.Pair(w.T("reallife_ob_meter_loans", "Loans, with arrears"), f.LoansMost < 0 ? w.F("reallife_ob_figure_no_limit", "{now}, no limit", "now", C(w, f.Lent + f.Arrears))
                    : w.F("reallife_ob_figure_of", "{now} of {most}", "now", C(w, f.Lent + f.Arrears), "most", C(w, f.LoansMost)),
                        f.LoansMost >= 0 && f.Lent + f.Arrears >= f.LoansMost ? UiRole.WarnText : UiRole.Text),
                BankParts.Pair(w.T("reallife_ob_box_branches", "Branches"), w.F("reallife_ob_figure_of", "{now} of {most}", "now", f.BranchesNow.ToString(Inv), "most", f.BranchesMost.ToString(Inv)),
                    f.BranchesNow > f.BranchesMost ? UiRole.WarnText : UiRole.Text)
            };
            if (f.Over)
            {
                lines.Add(BankParts.Say(w.F("reallife_ob_box_level_over",
                    "Your bank holds more than a {level} may. It keeps all of it, and takes no more until it is under the limit or moves up.", "level", f.Level), UiRole.WarnText));
            }
            var actions = new List<UiActionModel>(1);
            if (f.NextLevel == null)
            {
                lines.Add(BankParts.Say(w.T("reallife_ob_box_level_top", "Your bank is at the top level: its loans and savings have no limit but the regulator's ratio.")));
            }
            else
            {
                lines.Add(BankParts.Say(w.F("reallife_ob_sec_level_next", "Next: {level}", "level", f.NextLevel), UiRole.Text));
                lines.Add(BankParts.Check(w.F("reallife_ob_check_record", "{months} months at this level without a breach ({now} so far)", "months", f.RecordMonths.ToString(Inv),
                    "now", Math.Min(f.HeldMonths, f.RecordMonths).ToString(Inv)), f.HeldMonths >= f.RecordMonths));
                lines.Add(BankParts.Check(w.F("reallife_ob_check_capital", "Capital of at least {least} (it has {now})", "least", C(w, f.CapitalLeast), "now", C(w, f.Capital)),
                    f.Capital >= f.CapitalLeast));
                lines.Add(BankParts.Check(w.T("reallife_ob_check_regulator", "The regulator satisfied, and no run on the bank"), f.RegulatorOk));
                lines.Add(BankParts.Pair(w.T("reallife_ob_box_up_fee", "Licence to move up"), C(w, f.MoveFee)));
                lines.Add(BankParts.Pair(w.T("reallife_ob_box_up_holds", "It may then hold"), f.NextTop
                    ? w.F("reallife_ob_note_level_top", "Up to {branches} branches; no limit on loans or savings but the regulator's.", "branches", f.AfterBranches.ToString(Inv))
                    : w.F("reallife_ob_note_level", "Up to {branches} branches, {loans} of loans and {deposits} of savings.", "branches", f.AfterBranches.ToString(Inv), "loans", C(w, f.AfterLoans),
                        "deposits", C(w, f.AfterDeposits))));
                if (f.MoveWhy != null)
                {
                    lines.Add(BankParts.Say(f.MoveWhy, UiRole.WarnText));
                }
                actions.Add(BankParts.Button(w.T("reallife_ob_box_up_button", "Move up a level"), UiButtonKind.Primary, UiIcon.Up, MoneyAct.MoveUp, true, f.MoveWhy != null));
            }
            lines.Add(BankParts.Say(w.T("reallife_ob_box_level_note",
                "Moving up costs the licence of the level above less the one your bank holds. Capital put in on the Bank tab counts, and so does profit left in the bank.")));
            cards.Add(new UtilitiesCard { Title = w.T("reallife_ob_level_title", "Your bank's level"), Sub = f.Name, Chip = f.Level, ChipTone = f.Top ? UiRole.Ok : UiRole.None,
                Lines = lines.ToArray(), Actions = actions.ToArray() });
        }

        // The pay's page: how you are paid (a share on the steps, or a fixed sum you type), and what was paid.
        private static void PayCard(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var steps = new List<UtilitiesStep>(2);
            var moves = new List<MoneyStep>(2);
            var lines = new List<UtilitiesLine>(8);
            var actions = new List<UiActionModel>(1);
            steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_pay_head", "How you are paid"), f.PayMode == 1 ? w.T("reallife_ob_pay_mode_fixed",
                "A fixed sum each day") : w.T("reallife_ob_pay_mode_share", "A share of each day's profit"),
                null, f.PayMode, 2, -1, MoneyAct.PayMode, 0, new[] { 0, 1 }, out MoneyStep mode));
            moves.Add(mode);
            if (f.PayMode == 1)
            {
                int fixedNow = (int)Math.Min(int.MaxValue, f.PayFixed / 100);
                int typed = BankParts.Chosen(m.View.F[MoneyView.FPay], fixedNow, 0, 999999999);
                steps.Add(BankParts.AmountStep(w, w.T("reallife_ob_pay_fixed_row", "Each day"), typed, 0, 999999999, MoneyView.FPay, cards.Next, steps.Count,
                    w.F("reallife_ui_ob_now", "Now {amount} a day", "amount", C(w, f.PayFixed)), out MoneyStep fixedMove));
                moves.Add(fixedMove);
                actions.Add(BankParts.Button(w.T("reallife_ob_pay_set", "Set the sum"), UiButtonKind.Primary, UiIcon.Check, MoneyAct.PayFixed, true));
            }
            else
            {
                int at = f.PayShare / 50, count = 1000 / 50 + 1;
                var values = new int[count];
                for (int i = 0; i < count; i++)
                {
                    values[i] = i * 50;
                }
                steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_pay_share", "Your share"), Pct(f.PayShare), null, Math.Max(0, Math.Min(count - 1, at)), count, -1, MoneyAct.PayShare, 0,
                    values, out MoneyStep share));
                moves.Add(share);
            }
            if (f.PayWanted > 0)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ob_pay_day", "The last day"), C(w, f.PayGiven), f.PayGiven < f.PayWanted ? UiRole.WarnText : UiRole.Text));
                if (f.PayHeld != null)
                {
                    lines.Add(BankParts.Say(f.PayHeld, UiRole.WarnText));
                }
            }
            lines.Add(BankParts.Pair(w.T("reallife_ob_pay_month", "This month"), C(w, f.PaidMonth)));
            lines.Add(BankParts.Pair(w.T("reallife_ob_pay_last", "Last month"), C(w, f.PaidLast)));
            lines.Add(BankParts.Pair(w.T("reallife_ob_pay_all", "In all"), C(w, f.PaidAll)));
            lines.Add(BankParts.Say(w.T("reallife_ui_ob_pay_note", "Your pay comes out of the bank's capital, and only while the bank can spare it.")));
            cards.Add(new UtilitiesCard { Title = w.T("reallife_ob_pay_title", "Your pay"), Sub = f.Name, Lines = lines.ToArray(), Steps = steps.ToArray(), Actions = actions.ToArray() },
                moves.ToArray());
        }

        // The dividends' page: how often and what share, the next one, the last one and what held it back.
        private static void DividendCard(MoneyModel m, OwnBankFacts f, UtilitiesWords w, BankCards cards)
        {
            var steps = new List<UtilitiesStep>(2);
            var moves = new List<MoneyStep>(2);
            var lines = new List<UtilitiesLine>(8);
            int n = Math.Max(1, f.DivEveryNames.Length);
            var every = new int[n];
            for (int i = 0; i < n; i++)
            {
                every[i] = i;
            }
            steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_div_head", "How often"), EveryName(f), null, Math.Max(0, Math.Min(n - 1, f.DivEvery)), n, -1, MoneyAct.DividendEvery, 0, every,
                out MoneyStep how));
            moves.Add(how);
            if (f.DivEvery != 0)
            {
                int count = 1000 / 50 + 1;
                var values = new int[count];
                for (int i = 0; i < count; i++)
                {
                    values[i] = i * 50;
                }
                steps.Add(BankParts.ChoiceStep(w.T("reallife_ob_div_share", "Share of the profit kept"), Pct(f.DivShare), null, Math.Max(0, Math.Min(count - 1, f.DivShare / 50)), count, -1,
                    MoneyAct.DividendShare, 0,
                    values, out MoneyStep share));
                moves.Add(share);
                lines.Add(BankParts.Pair(w.T("reallife_ob_div_next", "Next one at the end of"), w.Day(f.DivNextDay)));
                lines.Add(BankParts.Pair(w.T("reallife_ob_div_kept", "Profit kept since the last one"), Signed(w, f.DivKept), f.DivKept < 0 ? UiRole.DangerText : UiRole.Text));
            }
            if (f.DivDay > 0)
            {
                lines.Add(BankParts.Pair(w.T("reallife_ob_div_last", "The last one"), w.F("reallife_ob_div_last_value", "{amount} on {day}", "amount", C(w, f.DivPaid), "day", f.DivDay.ToString(Inv)),
                    f.DivShort ? UiRole.WarnText : UiRole.Text));
                if (f.DivShort && f.DivWhy != null)
                {
                    lines.Add(BankParts.Say(f.DivWhy, UiRole.WarnText));
                }
            }
            lines.Add(BankParts.Pair(w.T("reallife_ob_div_all", "In all"), C(w, f.DivAll)));
            lines.Add(BankParts.Say(w.T("reallife_ui_ob_div_note", "A dividend is paid to you out of the capital the bank keeps over its least, and only while it can spare it.")));
            cards.Add(new UtilitiesCard { Title = w.T("reallife_ob_div_title", "Automatic dividends"), Sub = f.Name, Lines = lines.ToArray(), Steps = steps.ToArray() }, moves.ToArray());
        }
    }
}
