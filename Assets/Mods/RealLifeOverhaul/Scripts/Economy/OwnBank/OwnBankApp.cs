using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The bank app's tables (GameUi.TableSpec).</summary>
    internal static class OwnBankTables
    {
        /// <summary>0 what, 1 amount, 2 against what: the assets, then the deposits and the capital.</summary>
        public static readonly TableSpec Books = new TableSpec("OwnBank", "Books")
            .Column("reallife_ob_col_item", 760f, TableSort.None, flexible: true)
            .Column("reallife_ob_col_amount", 360f, TableSort.None)
            .Column("reallife_ob_col_note", 760f, TableSort.None, right: true)
            .Empty("reallife_ob_empty_books");

        /// <summary>0 savers, 1 deposits, 2 of the city's savings, 3 the bank's rate, 4 the rival banks', 5 how they stand.</summary>
        public static readonly TableSpec Savers = new TableSpec("OwnBank", "Savers")
            .Column("reallife_ob_col_savers", 440f, TableSort.Text, flexible: true)
            .Column("reallife_ob_col_deposits", 320f, TableSort.Money, sums: true)
            .Column("reallife_ob_col_share", 220f, TableSort.Number)
            .Column("reallife_ob_col_rate", 220f, TableSort.Number)
            .Column("reallife_ob_col_rivals", 240f, TableSort.Number)
            .Column("reallife_ob_col_state", 360f, TableSort.Text, right: true)
            .Empty("reallife_ob_empty_savers");

        /// <summary>0 borrowers, 1 lent, 2 in arrears, 3 the bank's rate, 4 the rival banks', 5 defaults a year, 6 whether it lends.</summary>
        public static readonly TableSpec Lending = new TableSpec("OwnBank", "Lending")
            .Column("reallife_ob_col_borrowers", 480f, TableSort.Text, flexible: true)
            .Column("reallife_ob_col_lent", 280f, TableSort.Money, sums: true)
            .Column("reallife_ob_col_arrears", 260f, TableSort.Money, sums: true)
            .Column("reallife_ob_col_rate", 200f, TableSort.Number)
            .Column("reallife_ob_col_rivals", 220f, TableSort.Number)
            .Column("reallife_ob_col_defaults", 240f, TableSort.Number)
            .Column("reallife_ob_col_lends", 200f, TableSort.Text, right: true)
            .Empty("reallife_ob_empty_lending");

        /// <summary>0 company, 1 shares, 2 cost, 3 value, 4 gain, 5 dividends a year; the largest holding first.</summary>
        public static readonly TableSpec Fund = new TableSpec("OwnBank", "Fund")
            .Column("reallife_ob_col_company", 520f, TableSort.Text, flexible: true)
            .Column("reallife_ob_col_shares", 220f, TableSort.Number)
            .Column("reallife_ob_col_cost", 280f, TableSort.Money, sums: true)
            .Column("reallife_ob_col_value", 280f, TableSort.Money, sums: true)
            .Column("reallife_ob_col_gain", 260f, TableSort.Money, sums: true)
            .Column("reallife_ob_col_dividends", 280f, TableSort.Money, sums: true, right: true)
            .Order(3, false)
            .Empty("reallife_ob_empty_fund");

        /// <summary>0 branch, 1 visitors this month, 2 seen to, 3 left unseen, 4 accounts opened, 5 loans arranged, 6 fees, 7 rent and wages a day.</summary>
        public static readonly TableSpec Branches = new TableSpec("OwnBank", "Branches")
            .Column("reallife_branch_col_branch", 440f, TableSort.Text, flexible: true)
            .Column("reallife_branch_col_visitors", 200f, TableSort.Number, sums: true)
            .Column("reallife_branch_col_served", 200f, TableSort.Number, sums: true)
            .Column("reallife_branch_col_lost", 200f, TableSort.Number, sums: true)
            .Column("reallife_branch_col_opened", 200f, TableSort.Number, sums: true)
            .Column("reallife_branch_col_loans", 200f, TableSort.Number, sums: true)
            .Column("reallife_branch_col_fees", 240f, TableSort.Money, sums: true)
            .Column("reallife_branch_col_costs", 260f, TableSort.Money, sums: true, right: true)
            .Empty("reallife_branch_empty");
    }

    /// <summary>
    /// The bank's app (GameUi.AppShell) on five tabs: bank, savers, lending, fund, branches (OwnBankApp.Branches). Written on
    /// opening, on a choice, after the day and after a branch's hour.
    /// </summary>
    public sealed partial class OwnBankApp
    {
        private static readonly string[] TabKeys = { "reallife_ob_tab_bank", "reallife_ob_tab_savers", "reallife_ob_tab_lending", "reallife_ob_tab_fund", "reallife_branch_tab" };
        private const int TypedLength = 12;
        private const string GuideTitle = "reallife_meta_title_economy_ownbank";

        private readonly OwnBankDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(16);
        private readonly List<TableRow> _pool = new List<TableRow>(16);
        private AppShell _shell;
        private DataTable _table;
        private string _pick;
        private int _tab;
        private bool _writing;

        public OwnBankApp(OwnBankDirector director)
        {
            _director = director;
            _paySlid = i => _director.SetPayShare(i * BankTiers.PayStep);
        }

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected.</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            _shell = AppShell.Make(menu, dormant, OwnBankDirector.AppId);
            _table = DataTable.On(_shell, "OwnBank");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(OwnBankTables.Books))
            {
                return null;
            }
            _table.Money = amount => FinanceText.Money((float)amount);
            _table.SelectionLost = Back;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        public void Opened()
        {
            Refresh();
        }

        /// <summary>The tab shown when the app next opens.</summary>
        public void Want(int tab)
        {
            _tab = Math.Max(0, Math.Min(TabKeys.Length - 1, tab));
            _pick = null;
        }

        public void Changed()
        {
            if (Showing)
            {
                Refresh();
            }
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab || _pick != null)
            {
                _tab = tab;
                _pick = null;
                Refresh();
            }
        }

        private void RowClicked(TableRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            if (row.Id != _pick)
            {
                _pick = row.Id;
                Refresh();
            }
        }

        private void Back()
        {
            if (_pick == null)
            {
                return;
            }
            _pick = null;
            _table.ClearSelection();
            Refresh();
        }

        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                string selected = null;
                TableSpec spec = _tab == OwnBankDirector.TabSavers ? OwnBankTables.Savers : _tab == OwnBankDirector.TabLending ? OwnBankTables.Lending
                    : _tab == OwnBankDirector.TabFund ? OwnBankTables.Fund : _tab == OwnBankDirector.TabBranches ? OwnBankTables.Branches : OwnBankTables.Books;
                OwnBank bank = _director.Bank;
                if (bank == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, OwnBankText.T("reallife_ob_app"));
                    box.Title(OwnBankText.T("reallife_ob_off_title"));
                    box.Note(OwnBankText.T("reallife_ob_off_body"));
                }
                else
                {
                    BankCity city = OwnBankGame.City();
                    UiClone.Words(_shell.Header, bank.Running
                        ? OwnBankText.Fill("reallife_ob_headline", "bank", bank.Name, "deposits", OwnBankText.Money(bank.Savings), "loans", OwnBankText.Money(bank.Lent))
                        : OwnBankText.T("reallife_ob_app"));
                    if (!bank.Running && _tab != OwnBankDirector.TabBank)
                    {
                        box.Title(OwnBankText.T(TabKeys[_tab]));
                        box.Note(OwnBankText.T("reallife_ob_box_none_yet"));
                    }
                    else if (_tab == OwnBankDirector.TabSavers)
                    {
                        WriteSavers(box, bank, city, ref selected);
                    }
                    else if (_tab == OwnBankDirector.TabLending)
                    {
                        WriteLending(box, bank, city, ref selected);
                    }
                    else if (_tab == OwnBankDirector.TabFund)
                    {
                        WriteFund(box, bank);
                    }
                    else if (_tab == OwnBankDirector.TabBranches)
                    {
                        WriteBranches(box, bank, city, ref selected);
                    }
                    else
                    {
                        WriteBank(box, bank, city, ref selected);
                    }
                }
                box.Done();
                _table.Show(spec, _rows, selected);
            }
            finally
            {
                _writing = false;
            }
        }

        private void WriteBank(InfoBox box, OwnBank b, BankCity c, ref string selected)
        {
            int ratio = OwnBankRules.Ratio(b);
            if (b.Running)
            {
                long assets = b.Reserves + b.Lent + b.InArrears + b.FundValue;
                long need = OwnBankRules.ReserveNeed(b);
                Info("reserves", "reallife_ob_row_reserves", b.Reserves, OwnBankText.T("reallife_ob_note_reserves", "need", OwnBankText.Money(need)),
                    b.Reserves < need ? TableRow.Yellow : TableRow.Plain);
                Info("loans", "reallife_ob_row_loans", b.Lent, OwnBankText.T("reallife_ob_note_share", "share", OwnBankText.Share(b.Lent, assets)), TableRow.Plain);
                Info("arrears", "reallife_ob_row_arrears", b.InArrears, OwnBankText.T("reallife_ob_note_written", "lost", OwnBankText.Money(b.LostAll), "back", OwnBankText.Money(b.RecoveredAll)),
                    b.InArrears > 0 ? TableRow.Yellow : TableRow.Plain);
                Info("fund", "reallife_ob_row_fund", b.FundValue, OwnBankText.T("reallife_ob_note_fund", "gain", OwnBankText.Signed(b.FundValue - b.FundCost)), TableRow.Plain);
                Info("deposits", "reallife_ob_row_deposits", b.Savings,
                    OwnBankText.T("reallife_ob_note_city", "share", OwnBankText.Share(b.Savings, OwnBankRules.SavingsPool(c, 0) + OwnBankRules.SavingsPool(c, 1))), TableRow.Plain);
                Info("capital", "reallife_ob_row_capital", OwnBankRules.CapitalNow(b),
                    OwnBankText.T("reallife_ob_note_ratio", "ratio", OwnBankText.Ratio(ratio), "law", OwnBankText.Ratio(OwnBankCatalogue.MinRatio)),
                    ratio < OwnBankCatalogue.MinRatio ? TableRow.Red : ratio < OwnBankCatalogue.WatchRatio ? TableRow.Yellow : TableRow.Green);
                Info("risk", "reallife_ob_row_risk", OwnBankRules.RiskWeighted(b), OwnBankText.T("reallife_ob_note_risk"), TableRow.Plain);
                if (WriteTierRows(box, b, c, ref selected))
                {
                    return;   // its level or the owner's pay picked: that row's page
                }
                _pick = null;
            }
            BuildingRegistration hq = OwnBankGame.Headquarters();
            int level = Prices.Level, share = _director.ScaleNow(), buy = EndScaleRules.Buying(Settings.EndScale);
            if (!b.Running)
            {
                box.Title(OwnBankText.T("reallife_ob_box_title"));
                if (b.State == OwnBankState.Closed)
                {
                    box.Chip(OwnBankText.T("reallife_ibox_chip_closed"), CellTone.Red);
                    box.Line(OwnBankText.Fill("reallife_ob_box_closed", "bank", b.Name, "day", FinanceText.Day(b.EndDay), "why", OwnBankText.T(OwnBankText.WhyKey(b.Why)),
                        "left", OwnBankText.Money(b.EndCents)));
                    int again = OwnBankRules.AgainDay(b, GameCalendar.DaysPerYear);
                    string wait = OwnBankGame.AgainWhy(b);
                    long next = OwnBankDirector.FoundCapital(level, b.Closures, buy);
                    if (wait != null)
                    {
                        box.Line(OwnBankText.Fill(wait == OwnBankRules.AgainWaitKey ? "reallife_ob_box_closed_after" : "reallife_ob_box_closed_integrity", "day", FinanceText.Day(again),
                            "least", OwnBankCatalogue.AgainIntegrity.ToString(CultureInfo.InvariantCulture), "now", OwnBankGame.Integrity.ToString(CultureInfo.InvariantCulture)), CellTone.Yellow);
                        if (b.Closures > 0)   // a bank a test founded and closed raises nothing
                        {
                            box.Note(OwnBankText.Fill("reallife_ob_box_closed_terms", "capital", OwnBankText.Money(next), "first", OwnBankText.Money(OwnBankDirector.FoundCapital(level, 0, buy))));
                        }
                        if (wait == OwnBankRules.AgainWaitKey && OwnBankGame.StandingOn)
                        {
                            box.Note(OwnBankText.Fill("reallife_ob_box_closed_standing", "least", OwnBankCatalogue.AgainIntegrity.ToString(CultureInfo.InvariantCulture),
                                "now", OwnBankGame.Integrity.ToString(CultureInfo.InvariantCulture)));
                        }
                        Meta.Guide.Point(box, GuideTitle);
                        return;
                    }
                    box.Line(OwnBankText.Fill("reallife_ob_box_closed_open", "day", FinanceText.Day(again)), CellTone.Green);
                }
                if (b.State == OwnBankState.Sold)
                {
                    box.Chip(OwnBankText.T("reallife_ibox_chip_sold"));
                    box.Line(OwnBankText.Fill("reallife_ob_box_sold", "bank", b.Name, "day", FinanceText.Day(b.EndDay), "price", OwnBankText.Money(b.EndCents)));
                }
                long licence = OwnBankDirector.FoundLicence(level, buy), capital = OwnBankDirector.FoundCapital(level, b.Closures, buy);
                box.Note(OwnBankText.Fill("reallife_ob_box_intro", "licence", OwnBankText.Money(licence), "capital", OwnBankText.Money(capital), "ratio",
                    OwnBankText.Ratio(OwnBankCatalogue.MinRatio)));
                box.Note(OwnBankText.Fill("reallife_ob_box_intro_level", "level", OwnBankDirector.TierName(BankTiers.Community),
                    "holds", Holds(BankTiers.Community, OwnBankRules.CapsOf(BankTiers.Community, level, share))));
                box.Section("reallife_ibox_sec_next");
                box.Line(hq != null ? OwnBankText.T("reallife_ob_box_hq", "name", OwnBankGame.Name(hq)) : OwnBankText.T("reallife_ob_box_need_hq"), hq != null ? CellTone.Green : CellTone.Yellow);
                box.RowPair("reallife_ibox_licence", OwnBankText.Money(licence), "reallife_ibox_capital", OwnBankText.Money(capital));
                box.Note(OwnBankText.Fill("reallife_ob_box_rivals", "savers", OwnBankText.Rate(OwnBankRules.RivalSaverBp(c, b, 0)), "loans",
                    OwnBankText.Rate(OwnBankRules.RivalLoanBp(c, b, OwnBankCatalogue.Good))));
                if (hq != null)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_ob_box_found", () => AskName(hq, licence, capital));
                }
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            int conf = OwnBankRules.Confidence(c, b, ratio), trust = OwnBankRules.Trust(c, b, OwnBankGame.Today, conf);
            box.Title(b.Name);
            box.Chip(OwnBankText.Stage(b), b.Stage == Regulator.Breach ? CellTone.Red : b.Stage == Regulator.Watch ? CellTone.Yellow : CellTone.Green);
            if (b.Run)
            {
                box.Line(OwnBankText.T("reallife_ob_box_run"), CellTone.Red);
            }
            if (b.Test)
            {
                box.Note(OwnBankText.T("reallife_ob_box_test"));
            }
            else if (hq == null)
            {
                box.Line(OwnBankText.T("reallife_ob_box_no_hq"), CellTone.Yellow);
            }
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ob_box_level", OwnBankDirector.TierName(OwnBankRules.TierOf(b)));
            box.Row("reallife_ob_box_ratio", OwnBankText.Fill("reallife_ob_box_ratio_value", "ratio", OwnBankText.Ratio(ratio), "law", OwnBankText.Ratio(OwnBankCatalogue.MinRatio),
                "watch", OwnBankText.Ratio(OwnBankCatalogue.WatchRatio)));
            box.Row("reallife_ob_box_trust", OwnBankText.Fill("reallife_ob_box_trust_value", "trust", OwnBankText.Ratio(trust), "confidence", OwnBankText.Ratio(conf)));
            if (b.HistoryCount > 0)
            {
                box.Row("reallife_ob_box_last", OwnBankText.Fill("reallife_ob_box_last_value", "income", OwnBankText.Money(b.LastIncome), "costs", OwnBankText.Money(b.LastCosts),
                    "profit", OwnBankText.Signed(b.LastIncome - b.LastCosts)));
            }
            long yearProfit = OwnBankRules.YearProfit(b);
            box.Row("reallife_ob_box_year", OwnBankText.Signed(yearProfit), yearProfit < 0 ? CellTone.Red : yearProfit > 0 ? CellTone.Green : CellTone.Plain);
            long value = OwnBankRules.SaleValue(b), most = OwnBankRules.MostOut(b, level);
            box.Row("reallife_ob_box_value", OwnBankText.Money(value));
            if (share > 0)
            {
                box.Row("reallife_endscale_ob_crews", OwnBankText.Fill("reallife_endscale_ob_crews_value",
                    "tellers", b.Crew[OwnBankCatalogue.Tellers].ToString(), "tellers_need", OwnBankRules.CrewNeed(b, OwnBankCatalogue.Tellers, share).ToString(),
                    "managers", b.Crew[OwnBankCatalogue.Managers].ToString(), "managers_need", OwnBankRules.CrewNeed(b, OwnBankCatalogue.Managers, share).ToString(),
                    "officers", b.Crew[OwnBankCatalogue.Officers].ToString(), "officers_need", OwnBankRules.CrewNeed(b, OwnBankCatalogue.Officers, share).ToString(),
                    "compliance", b.Crew[OwnBankCatalogue.Compliance].ToString(), "compliance_need", OwnBankRules.CrewNeed(b, OwnBankCatalogue.Compliance, share).ToString(),
                    "wages", OwnBankText.Money(OwnBankRules.Crews(b) * OwnBankCatalogue.CostYearDays / GameCalendar.DaysPerYear * level / PriceRules.Start)));
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_ob_box_put_in", () => AskAmount("reallife_ob_ask_put_in", OwnBankCatalogue.CapitalStepCents * 10, cents =>
            {
                string why;
                return _director.PutIn(cents, out why) ? null : why;
            }));
            if (most > 0)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_ob_box_take_out", () => AskAmount("reallife_ob_ask_take_out", most, cents =>
                {
                    string why;
                    return _director.TakeOut(cents, out why) ? null : why;
                }));
            }
            if (OwnBankRules.Sellable(b))
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_ob_box_sell", () => Ask("reallife_ob_confirm_sell_title".Localize(new { bank = b.Name }),
                    "reallife_ob_confirm_sell_body".Localize(new { price = OwnBankText.Money(value), years = OwnBankCatalogue.SaleYears.ToString() }), "reallife_ob_box_sell", true,
                    () => _director.Sell(value, out string why) ? null : why));
            }
            Meta.Guide.Point(box, GuideTitle);
        }

        private void Info(string id, string key, long cents, string note, CellTone tone)
        {
            TableRow row = Row("b|" + id);
            row.Pick = false;
            row.Set(0, OwnBankText.T(key));
            row.Set(1, OwnBankText.Money(cents), cents / 100.0, tone);
            row.Set(2, note);
        }

        private void WriteSavers(InfoBox box, OwnBank b, BankCity c, ref string selected)
        {
            int picked = -1;
            for (int k = 0; k < OwnBankCatalogue.Savers; k++)
            {
                string id = "s|" + k;
                TableRow row = Row(id);
                row.Pick = true;
                if (id == _pick)
                {
                    selected = id;
                    picked = k;
                }
                int own = OwnBankRules.SaverBp(c, b, k), rival = OwnBankRules.RivalSaverBp(c, b, k);
                long pool = OwnBankRules.SavingsPool(c, k);
                row.Set(0, OwnBankText.Saver(k));
                row.Set(1, OwnBankText.Money(b.Deposits[k]), b.Deposits[k] / 100.0);
                row.Set(2, OwnBankText.Share(b.Deposits[k], pool), pool > 0 ? b.Deposits[k] * 1000.0 / pool : 0.0);
                row.Set(3, OwnBankText.Rate(own), own, own > rival ? TableRow.Green : own < rival ? TableRow.Yellow : TableRow.Plain);
                row.Set(4, OwnBankText.Rate(rival), rival);
                row.Set(5, OwnBankText.T(b.Run ? "reallife_ob_state_running" : "reallife_ob_state_saving"), b.Run ? TableRow.Red : TableRow.Plain);
            }
            int ratio = OwnBankRules.Ratio(b), conf = OwnBankRules.Confidence(c, b, ratio), trust = OwnBankRules.Trust(c, b, OwnBankGame.Today, conf);
            if (picked < 0)
            {
                _pick = null;
                box.Title(OwnBankText.T("reallife_ob_tab_savers"));
                box.Subtitle(b.Name);
                if (b.Run)
                {
                    box.Line(OwnBankText.T("reallife_ob_box_run"), CellTone.Red);
                }
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_ob_box_deposits", OwnBankText.Money(b.Savings));
                box.Row("reallife_ob_box_trust", OwnBankText.Fill("reallife_ob_box_trust_value", "trust", OwnBankText.Ratio(trust), "confidence", OwnBankText.Ratio(conf)));
                if (!b.Run)
                {
                    box.Note(OwnBankText.T("reallife_ob_box_savers_pick"));
                }
                box.Note(OwnBankText.T("reallife_ob_box_savers_intro"));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            int kind = picked;
            box.Title(OwnBankText.Saver(kind));
            box.Subtitle(b.Name);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ob_box_deposits", OwnBankText.Money(b.Deposits[kind]));
            box.Row("reallife_ob_box_wanted", OwnBankText.Money(OwnBankRules.SaversWanted(c, b, kind, trust)));
            box.Row("reallife_ob_box_rival_rate", OwnBankText.Rate(OwnBankRules.RivalSaverBp(c, b, kind)));
            box.Headline("reallife_ob_box_rate_head");
            int least = OwnBankCatalogue.SaverOffsetLeast, step = OwnBankCatalogue.SaverOffsetStep, rivalBp = OwnBankRules.RivalSaverBp(c, b, kind);
            box.Selector((OwnBankCatalogue.SaverOffsetMost - least) / step + 1, (b.SaverOffset[kind] - least) / step,
                i => OwnBankText.Fill("reallife_ob_rate_option", "rate", OwnBankText.Rate(Math.Max(0, rivalBp + least + i * step)), "points", OwnBankText.Points(least + i * step)),
                i => UiClone.Safe(() =>
                {
                    _director.SetSaverRate(kind, least + i * step);
                    Refresh();
                }));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ob_box_back", Back);
            box.Note(OwnBankText.T(kind == OwnBankCatalogue.Firms ? "reallife_ob_box_firms_note" : "reallife_ob_box_people_note"));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteLending(InfoBox box, OwnBank b, BankCity c, ref string selected)
        {
            int picked = -1;
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                string id = "l|" + j;
                TableRow row = Row(id);
                row.Pick = true;
                if (id == _pick)
                {
                    selected = id;
                    picked = j;
                }
                int own = OwnBankRules.LoanBp(c, b, j), rival = OwnBankRules.RivalLoanBp(c, b, j), pd = OwnBankRules.DefaultPerMille(c, b, j);
                row.Set(0, OwnBankText.Segment(j));
                row.Set(1, OwnBankText.Money(b.Loans[j]), b.Loans[j] / 100.0);
                row.Set(2, OwnBankText.Money(b.Arrears[j]), b.Arrears[j] / 100.0, b.Arrears[j] > 0 ? TableRow.Yellow : TableRow.Plain);
                row.Set(3, OwnBankText.Rate(own), own, own < rival ? TableRow.Green : TableRow.Plain);
                row.Set(4, OwnBankText.Rate(rival), rival);
                row.Set(5, OwnBankText.Ratio(pd), pd, pd >= 100 ? TableRow.Red : pd >= 40 ? TableRow.Yellow : TableRow.Plain);
                row.Set(6, OwnBankText.T(b.Lending[j] ? "reallife_ob_lends_yes" : "reallife_ob_lends_no"));
            }
            if (picked < 0)
            {
                _pick = null;
                box.Title(OwnBankText.T("reallife_ob_tab_lending"));
                box.Subtitle(b.Name);
                bool stopped = b.Run || b.Stage == Regulator.Breach;
                if (stopped)
                {
                    box.Chip(OwnBankText.T("reallife_ibox_chip_stopped"), CellTone.Red);
                    box.Line(OwnBankText.T("reallife_ob_box_no_lending"), CellTone.Red);
                }
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_ob_box_lent", OwnBankText.Money(b.Lent));
                box.Row("reallife_ob_box_arrears", OwnBankText.Money(b.InArrears), b.InArrears > 0 ? CellTone.Yellow : CellTone.Plain);
                box.Row("reallife_ob_box_written", OwnBankText.Fill("reallife_ob_box_written_value", "month", OwnBankText.Money(b.LastLost), "all", OwnBankText.Money(b.LostAll),
                    "back", OwnBankText.Money(b.RecoveredAll)));
                if (!stopped)
                {
                    box.Note(OwnBankText.T("reallife_ob_box_lending_pick"));
                }
                box.Note(OwnBankText.T(c.Courts ? "reallife_ob_box_courts_on" : "reallife_ob_box_courts_off"));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            int seg = picked;
            long recovering = b.Recovering[seg];
            box.Title(OwnBankText.Segment(seg));
            box.Subtitle(b.Name);
            box.Chip(OwnBankText.T(b.Lending[seg] ? "reallife_ibox_chip_lending" : "reallife_ibox_chip_stopped"), b.Lending[seg] ? CellTone.Green : CellTone.Yellow);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ob_box_lent", OwnBankText.Money(b.Loans[seg]));
            box.Row("reallife_ob_box_arrears", OwnBankText.Fill("reallife_ob_box_arrears_value", "arrears", OwnBankText.Money(b.Arrears[seg]), "back", OwnBankText.Money(recovering)));
            box.Row("reallife_ob_box_defaults", OwnBankText.Fill("reallife_ob_box_defaults_value", "rate", OwnBankText.Ratio(OwnBankRules.DefaultPerMille(c, b, seg)),
                "usual", OwnBankText.Ratio(OwnBankCatalogue.DefaultBase[seg])));
            box.Row("reallife_ob_box_wanted", OwnBankText.Money(OwnBankRules.LoansWanted(c, b, seg)));
            box.Headline("reallife_ob_box_rate_head");
            int least = OwnBankCatalogue.LoanOffsetLeast, step = OwnBankCatalogue.LoanOffsetStep, rivalBp = OwnBankRules.RivalLoanBp(c, b, seg);
            box.Selector((OwnBankCatalogue.LoanOffsetMost - least) / step + 1, (b.LoanOffset[seg] - least) / step,
                i => OwnBankText.Fill("reallife_ob_rate_option", "rate", OwnBankText.Rate(Math.Max(CreditCatalogue.RateFloor * 100, rivalBp + least + i * step)),
                    "points", OwnBankText.Points(least + i * step)),
                i => UiClone.Safe(() =>
                {
                    _director.SetLoanRate(seg, least + i * step);
                    Refresh();
                }));
            box.Switch("reallife_ob_box_lend_switch", b.Lending[seg], on => UiClone.Safe(() =>
            {
                _director.SetLending(seg, on);
                Refresh();
            }));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ob_box_back", Back);
            box.Note(OwnBankText.T(seg == OwnBankCatalogue.Poor ? "reallife_ob_box_poor_note" : OwnBankCatalogue.IsBusiness(seg) ? "reallife_ob_box_firm_note" : "reallife_ob_box_person_note"));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteFund(InfoBox box, OwnBank b)
        {
            long dividends = 0;
            for (int i = 0; i < b.Holdings.Count; i++)
            {
                FundHolding h = b.Holdings[i];
                TableRow row = Row("f|" + h.StockId);
                row.Pick = false;
                long value = h.Qty * h.LastCents, gain = value - h.CostCents, year = h.Qty * h.DpsCents * 4;
                dividends += year;
                row.Set(0, string.IsNullOrEmpty(h.Ticker) ? h.Name : h.Name + " (" + h.Ticker + ")");
                row.Set(1, h.Qty.ToString("N0", CultureInfo.InvariantCulture), h.Qty);
                row.Set(2, OwnBankText.Money(h.CostCents), h.CostCents / 100.0);
                row.Set(3, OwnBankText.Money(value), value / 100.0);
                row.Set(4, OwnBankText.Signed(gain), gain / 100.0, gain < 0 ? TableRow.Red : gain > 0 ? TableRow.Green : TableRow.Plain);
                row.Set(5, OwnBankText.Money(year), year / 100.0);
            }
            box.Title(OwnBankText.T("reallife_ob_tab_fund"));
            box.Subtitle(b.Name);
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_ob_box_fund_value", OwnBankText.Fill("reallife_ob_box_fund_value_value", "value", OwnBankText.Money(b.FundValue), "gain", OwnBankText.Signed(b.FundValue - b.FundCost)));
            box.Row("reallife_ob_box_fund_room", OwnBankText.Money(OwnBankFund.Room(b)));
            box.Row("reallife_ob_box_fund_dividends", OwnBankText.Money(dividends));
            box.Headline("reallife_ob_box_fund_head");
            box.Selector(OwnBankCatalogue.Strategies, b.Strategy, i => OwnBankText.Strategy(i), i => UiClone.Safe(() =>
            {
                _director.SetStrategy(i);
                Refresh();
            }));
            int step = OwnBankCatalogue.FundLimitStep;
            box.Selector(OwnBankCatalogue.FundLimitMost / step + 1, b.FundLimit / step, i => OwnBankText.T("reallife_ob_fund_limit_option", "share", OwnBankText.Ratio(i * step)),
                i => UiClone.Safe(() =>
                {
                    _director.SetFundLimit(i * step);
                    Refresh();
                }));
            box.Note(OwnBankText.Fill("reallife_ob_box_fund_rules", "companies", OwnBankCatalogue.FundCompanies[Math.Max(1, b.Strategy)].ToString(), "weight",
                OwnBankText.Ratio(OwnBankCatalogue.FundWeight)));
            Meta.Guide.Point(box, GuideTitle);
        }

        // The bank's name typed into the game's text box (empty: the headquarters' name and "Bank"), then the game's question.
        private void AskName(BuildingRegistration hq, long licence, long capital)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            string suggested = OwnBankDirector.Named(null, hq);
            if (uis == null || uis.textInputUi == null)
            {
                AskFound(suggested, licence, capital);
                return;
            }
            GameUi.TextBox.Ask("reallife_ob_ask_name", "common_confirm", text => UiClone.Safe(() => AskFound(OwnBankDirector.Named(text, hq), licence, capital)), null, suggested, true,
                OwnBankDirector.NameMost);
        }

        private void AskFound(string name, long licence, long capital)
        {
            Ask("reallife_ob_confirm_found_title".Localize(new { bank = name }),
                "reallife_ob_confirm_found_body".Localize(new { licence = OwnBankText.Money(licence), capital = OwnBankText.Money(capital) }),
                "reallife_ob_box_found", false, () => _director.Found(name, licence, capital, out string why) ? null : why);
        }

        // An amount in whole money typed into the game's text box; the action it is for checks it.
        private void AskAmount(string placeholderKey, long initialCents, Func<long, string> act)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            GameUi.TextBox.Ask(placeholderKey, "common_confirm", text => UiClone.Safe(() =>
            {
                if (!Whole(text, out long money) || money <= 0)
                {
                    Notifications.ShowError("common_notification_invalid_amount", null, false);
                    return;
                }
                string why = act(money * 100);
                if (why != null)
                {
                    Notifications.Show(NotificationType.Warning, why, null, 8f, null, null, true, false);
                }
                Refresh();
            }), null, (Math.Max(0, initialCents) / 100).ToString(), false, TypedLength);
        }

        // A whole amount from what was typed: digits, with the comma, space and sign of "$5,000" let through. False for anything else.
        internal static bool Whole(string text, out long amount)
        {
            amount = 0;
            bool digits = false;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    amount = Math.Min(amount * 10 + (c - '0'), 99999999999L);
                    digits = true;
                }
                else if (c != ',' && c != ' ' && c != '$')
                {
                    return false;
                }
            }
            return digits;
        }

        // The game's own question before a large sum is spent or the bank sold; on yes, done, and any refusal said.
        private void Ask(LanguageChangeEventDataHolder title, LanguageChangeEventDataHolder body, string confirmKey, bool negative, Func<string> act)
        {
            UiClone.Safe(() => GameConfirm.Show(title, body, () => UiClone.Safe(() =>
            {
                string why = act();
                if (why != null)
                {
                    Notifications.Show(NotificationType.Warning, why, null, 8f, null, null, true, false);
                }
                Refresh();
            }), null, confirmKey, "common_cancel", false, negative));
        }

        private TableRow Row(string id)
        {
            TableRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new TableRow();
            }
            _rows.Add(row.Reset(id));
            return row;
        }
    }
}
