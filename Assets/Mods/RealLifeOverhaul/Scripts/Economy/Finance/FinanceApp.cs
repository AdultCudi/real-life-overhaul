using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the Finance app shows, in the game's frame (GameUi.AppShell, copied from MyEmployees) as the Planner and
    /// the Guide are. Five tabs over the table on the right, and on the left the white box with the tab's figures:
    ///
    ///   Overview    cash, what is owed, what leaves tomorrow, net worth, the rating; the table lists every debt
    ///   Credit      the score and its grade and what would raise it; the table lists what the rating is made of and
    ///               the file's last events; pick a factor to see how it stands and what moves it
    ///   Borrow      every offer: the city's banks and the other kinds of credit; pick one, see what it costs, apply
    ///   Cash flow   every movement of money for yesterday, 7 days or 30, with what it was for
    ///   Accounts    every debt with its lender, what is owed, the next payment and how it stands; pick one to pay it,
    ///               draw on it or close it, and the ways out of debt (FinanceApp.Accounts.cs)
    ///
    /// A list goes in the table, which scrolls; the white box, which does not, shows only the one thing picked.
    /// Every table is declared in FinanceApp.Tables.cs and is sorted, filtered and summed by the kit (GameUi.DataTable).
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and figures are made when it
    /// opens, when a tab is picked and when the player changes something in it.
    /// </summary>
    public sealed partial class FinanceApp
    {
        /// <summary>The glyph on the app's icon: the game's coin, or a picture the phone's own scene uses.</summary>
        public static readonly string[] AppGlyphs = { "Icon-Money", "Icon-Coin" };

        private const int TypedAmountLength = 9;
        internal const string GuideTitle = "reallife_meta_title_economy";   // the Guide's page the app's pages point to at their foot

        private static readonly string[] TabKeys = { "reallife_econ_tab_overview", "reallife_econ_tab_credit", "reallife_econ_tab_borrow", "reallife_econ_tab_cash", "reallife_econ_tab_accounts" };
        private static readonly int[] AmountSteps = { 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000, 2500000, 5000000 };

        public static IModLogger Logger;

        private readonly EconomyDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(64);
        private readonly List<TableRow> _pool = new List<TableRow>(64);
        private readonly List<DebtLine> _debts = new List<DebtLine>(6);
        private readonly List<int> _amounts = new List<int>(16);
        private readonly CreditFactor[] _tips = new CreditFactor[2];
        private readonly CashFlow _flow = new CashFlow();

        private AppShell _shell;
        private DataTable _table;
        private TableSpec _spec = FinanceTables.Off;   // the table the tab that shows is written as
        private FullMenu _menu;
        private int _tab, _wanted = -1;
        private int _factor = -1;  // the part of the rating picked on the Credit tab
        private int _period = CashFlow.Week;
        private Lender _lender;
        private int _amount;
        private bool _ready;       // the director has brought the file up to date for this opening

        public FinanceApp(EconomyDirector director)
        {
            _director = director;
        }

        public static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            _menu = menu;
            using var piece = SessionWatch.Piece("finance: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, EconomyDirector.AppId);
            _table = DataTable.On(_shell, "Finance");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(FinanceTables.Off))
            {
                return null;
            }
            _table.Money = amount => FinanceText.Money((float)amount);
            _table.SelectionLost = ClosePick;
            _table.Filtered = () =>
            {
                if (_tab == Finance.CashFlow)
                {
                    Refresh();   // the box says what the category chosen comes to
                }
            };
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        // ---------------------------------------------------------------- opening and the tabs

        /// <summary>The tab to show the next time the app opens (a notification was clicked).</summary>
        public void Want(int tab)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            if (_shell != null && _shell.Root != null && _shell.Root.activeInHierarchy)
            {
                Opened();
            }
        }

        /// <summary>The city's page is what shows the next time the app opens (a notification of the city's economy was clicked).</summary>
        public void WantCity()
        {
            _wantCity = true;
            Want(Finance.Overview);
        }

        public void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            if (_wanted >= 0)
            {
                _tab = _wanted;
                _wanted = -1;
            }
            _city = _wantCity && _tab == Finance.Overview;
            _wantCity = false;
            _standing = _wantStanding && _tab == Finance.Overview && StandingOn() != null;
            _wantStanding = false;
            _city &= !_standing;
            _ready = _director.AppOpened();
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab || _city || _standing)
            {
                _tab = tab;
                _city = false;
                _standing = false;
                _standingPick = null;
                _factor = -1;
                Refresh();
            }
        }

        // Everything the open tab shows, made again: asked for when the app opens, a tab is picked or a figure has changed.
        private void Refresh()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            for (int i = 0; i < _rows.Count; i++)
            {
                _pool.Add(_rows[i]);
            }
            _rows.Clear();
            bool city = _city && _tab == Finance.Overview && CityState() != null;
            StandingPage standing = _standing && _tab == Finance.Overview ? StandingOn() : null;
            _shell.ShowTab(city || standing != null ? -1 : _tab);   // (on the city's page and the Standing page no tab is the one showing: Overview's can be clicked to go back)
            InfoBox box = _shell.Box;
            box.Clear();
            string selected = null;
            _spec = FinanceTables.Off;
            if (!_ready || !CreditLedger.Ready)
            {
                UiClone.Words(_shell.Header, FinanceText.T("reallife_econ_app"));
                box.Title(FinanceText.T("reallife_econ_off_title"));
                box.Note(FinanceText.T("reallife_econ_off_body"));
            }
            else
            {
                CreditScore score = CreditLedger.Score;
                UiClone.Words(_shell.Header, FinanceText.T("reallife_econ_headline", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade)));
                switch (_tab)
                {
                    case Finance.Credit: selected = WriteCredit(box, score); break;
                    case Finance.Borrow: selected = WriteBorrow(box, score); break;
                    case Finance.CashFlow: WriteCashFlow(box); break;
                    case Finance.Accounts: selected = WriteAccounts(box); break;
                    default:
                        if (standing != null)
                        {
                            WriteStanding(box, standing);
                            selected = _standingPick;
                        }
                        else if (city)
                        {
                            WriteCity(box);
                        }
                        else
                        {
                            WriteOverview(box, score);
                        }
                        break;
                }
            }
            box.Done();
            _table.Show(_spec, _rows, selected);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Finance: tab " + _tab + " written, " + _rows.Count + " row(s); " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
            }
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

        private void Say(string id, string words)
        {
            TableRow row = Row(id);
            row.Info = true;
            row.A = words;
        }

        // ---------------------------------------------------------------- Overview

        private void WriteOverview(InfoBox box, CreditScore score)
        {
            GameInstance save = SaveGameManager.Current;
            List<Loan> loans = save.Loans;
            CreditFile file = CreditLedger.File;
            Obligations due = Books.Tomorrow(file);   // the figure the overdraft covers a short day against
            float owedLoans = 0f, payments = due.Loans, interest = due.LoanInterest;
            _spec = FinanceTables.Overview;
            for (int i = 0; i < loans.Count; i++)
            {
                Loan loan = loans[i];
                owedLoans += loan.remainingAmount;
                TableRow row = Row("loan:" + i);
                DebtCells(row, GameLoans.BankName(loan), FinanceTables.OfLoan(Purpose(file, loan)), loan.remainingAmount, GameLoans.Due(loan));
                if (loan.dailyPayment > 0)
                {
                    int days = Mathf.CeilToInt(loan.remainingAmount / loan.dailyPayment);
                    row.Set(4, FinanceText.T("reallife_econ_row_days", "days", days.ToString()), days);
                }
            }
            float owedAccounts = OverviewAccounts(file, save.Day);
            _debts.Clear();
            EconomyBridges.OtherDebts?.Invoke(_debts);
            float owedOther = 0f, otherDaily = due.Other;
            for (int i = 0; i < _debts.Count; i++)
            {
                DebtLine debt = _debts[i];
                owedOther += debt.Owed;
                TableRow row = Row("debt:" + i);
                DebtCells(row, FinanceText.T(debt.NameKey), FinanceTables.Other, debt.Owed, debt.PerDay > 0 ? Mathf.Min(debt.PerDay, debt.Owed) : 0f);
                if (debt.LateDays > 0)
                {
                    row.Set(4, FinanceText.T("reallife_econ_row_late", "days", debt.LateDays.ToString()), TableRow.Red);
                }
            }
            if (_rows.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_debts"));
            }

            // Rent and wages as the game's daily run takes them (Books.Tomorrow).
            float rent = due.Rent, wages = due.Wages;
            var wealth = PlayerHelper.GetPersonalWealth();   // the game's own figure for what the player is worth (Persona shows it)

            box.Title(FinanceText.T("reallife_econ_tab_overview"));
            box.Chip(FinanceText.Grade(score.Grade), Tone(score.Grade));
            box.Section("reallife_look_sec_now");
            box.Row("reallife_econ_over_cash", FinanceText.Money(save.Money), save.Money < 0f ? CellTone.Red : CellTone.Plain);
            box.Row("reallife_econ_over_owed", FinanceText.Money(owedLoans + owedAccounts + owedOther));
            if (owedOther > 0f || owedAccounts > 0f)
            {
                box.Note(FinanceText.T("reallife_econ_over_owed_three", "loans", FinanceText.Money(owedLoans), "accounts", FinanceText.Money(owedAccounts), "other", FinanceText.Money(owedOther)));
            }
            box.Row("reallife_econ_over_rating", FinanceText.T("reallife_econ_rating_value", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade)), Tone(score.Grade));
            bool cityOn = CityLines(box);   // the bank rate, prices and the economy, while the city's economy runs
            box.Headline("reallife_econ_over_costs");
            box.Row("reallife_econ_over_leaves", FinanceText.Money(due.Total));
            box.Row("reallife_econ_over_loan_payments", FinanceText.Money(payments));
            if (interest > 0f)
            {
                box.Note(FinanceText.T("reallife_econ_over_interest", "amount", FinanceText.Money(interest)));
            }
            if (due.Accounts > 0f)
            {
                box.Row("reallife_econ_over_accounts", FinanceText.Money(due.Accounts));
            }
            if (otherDaily > 0f)
            {
                box.Row("reallife_econ_over_fines", FinanceText.Money(otherDaily));
            }
            box.Row("reallife_econ_over_rent", FinanceText.Money(rent));
            box.Row("reallife_econ_over_wages", FinanceText.Money(wages));
            box.Headline("reallife_econ_over_worth_head");
            box.Row("reallife_econ_over_worth", FinanceText.Money(wealth.CurrentWealth - owedOther - owedAccounts));
            box.Note(FinanceText.T("reallife_econ_over_worth_parts", "investments", FinanceText.Money(wealth.totalInvestments), "assets", FinanceText.Money(wealth.totalAssets)));
            Transform buttons = box.ButtonRow();
            if (cityOn)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_macro_open", ShowCity);
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_econ_open_econoview", OpenEconoView);
            if (StandingOn() != null)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_fame_open", ShowStanding);   // the player's standing, its own page (Scripts/Fame through EconomyBridges.Standing)
            }
            Meta.Guide.Point(box, GuideTitle);
        }

        // The Overview's first four cells of a debt: who it is owed to, its kind, what is owed and what it takes tomorrow.
        private static void DebtCells(TableRow row, string name, int kind, float owed, float daily)
        {
            row.Kind = kind;
            row.Set(0, name);
            row.Set(1, FinanceText.T(FinanceTables.DebtKinds[kind]));
            row.Set(2, FinanceText.Money(owed), owed);
            if (daily > 0f)
            {
                row.Set(3, FinanceText.Money(daily), daily);
            }
        }

        // What the mod lent one of the game's loans for (a plain loan, a business loan, a consolidation), from its mark on the file.
        private static int Purpose(CreditFile file, Loan loan)
        {
            for (int i = 0; i < file.Loans.Count; i++)
            {
                if (ReferenceEquals(file.Loans[i].Ref, loan))
                {
                    return file.Loans[i].Purpose;
                }
            }
            return LoanPurpose.Plain;
        }

        // Repaying and changing a daily payment stay where the game has them: its own loans page.
        private void OpenEconoView()
        {
            if (_menu != null)
            {
                _menu.ShowApp(AppName.EconoView);
            }
        }

        // ---------------------------------------------------------------- Credit

        // What the rating is made of, then the file's last events, in one table. A factor can be picked: the box
        // then says how it stands and what moves it; with none picked it says what would raise the rating most.
        private string WriteCredit(InfoBox box, CreditScore score)
        {
            _spec = FinanceTables.Credit;
            string kindFactor = FinanceText.T(FinanceTables.CreditKinds[FinanceTables.Factor]), kindEvent = FinanceText.T(FinanceTables.CreditKinds[FinanceTables.Event]);
            for (int f = 0; f < (int)CreditFactor.Count; f++)
            {
                float most = CreditCatalogue.PointsOf((CreditFactor)f);
                int now = Mathf.RoundToInt(score.Factor[f] * most), weight = CreditCatalogue.Weights[f];
                TableRow row = Row("factor:" + f);
                row.Pick = true;
                row.Index = f;
                row.Kind = FinanceTables.Factor;
                row.Set(0, FinanceText.T(CreditCatalogue.FactorKeys[f]));
                row.Set(1, kindFactor);
                row.Set(2, weight + "%", weight);
                // (ordered by the points it costs: the factor that holds the rating back most comes first)
                row.Set(5, FinanceText.T("reallife_tab_points_of", "now", now.ToString(), "most", Mathf.RoundToInt(most).ToString()), now - most,
                    score.Factor[f] < 0.4f ? TableRow.Red : score.Factor[f] < 0.65f ? TableRow.Yellow : TableRow.Plain);
            }
            List<CreditEvent> events = CreditLedger.File.Events;
            int shown = 0;
            for (int i = events.Count - 1; i >= 0 && shown < CreditCatalogue.LedgerShown; i--, shown++)
            {
                CreditEvent e = events[i];
                TableRow row = Row("event:" + i);
                row.Kind = FinanceTables.Event;
                row.Set(0, FinanceText.Event(e));
                row.Set(1, kindEvent);
                row.Set(3, FinanceText.Day(e.Day), e.Day);
                string amount = FinanceText.EventAmount(e);
                if (!string.IsNullOrEmpty(amount))
                {
                    row.Set(4, amount, e.Amount);
                }
                row.Set(5, FinanceText.Effect(e.Effect), e.Effect, e.Effect < 0 ? TableRow.Red : e.Effect > 0 ? TableRow.Green : TableRow.Plain);
            }

            if (_factor >= 0 && _factor < (int)CreditFactor.Count)
            {
                WriteFactor(box, score, (CreditFactor)_factor);
                return "factor:" + _factor;
            }
            box.Title(FinanceText.T("reallife_econ_rating_value", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade)));
            box.Chip(FinanceText.Grade(score.Grade), Tone(score.Grade));
            box.Line(FinanceText.T(score.Lends ? "reallife_econ_credit_threshold_ok" : "reallife_econ_credit_threshold_no", "threshold", CreditCatalogue.LendingThreshold.ToString()),
                score.Lends ? CellTone.Green : CellTone.Red);
            int tips = CreditScoring.Tips(score, _tips);
            if (tips > 0)
            {
                box.Headline("reallife_econ_credit_raise");
                for (int i = 0; i < tips; i++)
                {
                    box.Line(FinanceText.Tip(score, _tips[i]));
                }
            }
            box.Headline("reallife_econ_credit_factors");
            for (int f = 0; f < (int)CreditFactor.Count; f++)
            {
                float part = score.Factor[f];
                box.RowWords(FinanceText.FactorName((CreditFactor)f), FinanceText.Stands(score, (CreditFactor)f),
                    part >= 0.85f ? CellTone.Green : part >= 0.4f ? CellTone.Plain : CellTone.Yellow);
            }
            if (shown == 0)
            {
                box.Note(FinanceText.T("reallife_econ_row_no_events"));
            }
            WorstLine(box, score);
            Meta.Guide.Point(box, GuideTitle);
            return null;
        }

        private static readonly string[] FactorAbout =
        {
            "reallife_tab_factor_about_payments", "reallife_tab_factor_about_owed", "reallife_tab_factor_about_length", "reallife_tab_factor_about_legal", "reallife_tab_factor_about_applications"
        };

        // One part of the rating: what it is, its weight, the points it gives of those it could, how it stands and what moves it.
        private void WriteFactor(InfoBox box, CreditScore score, CreditFactor factor)
        {
            float most = CreditCatalogue.PointsOf(factor);
            int now = Mathf.RoundToInt(score.Factor[(int)factor] * most);
            float part = score.Factor[(int)factor];
            CellTone tone = part >= 0.85f ? CellTone.Green : part >= 0.4f ? CellTone.Plain : CellTone.Yellow;
            box.Title(FinanceText.T(CreditCatalogue.FactorKeys[(int)factor]));
            box.Chip(FinanceText.Word(part), tone);
            box.Note(FinanceText.T(FactorAbout[(int)factor]));
            box.Section("reallife_look_sec_now");
            box.Row("reallife_tab_factor_stands", FinanceText.Stands(score, factor), tone);
            box.Meter("reallife_tab_factor_points", part, FinanceText.T("reallife_tab_points_of", "now", now.ToString(), "most", Mathf.RoundToInt(most).ToString()), tone);
            box.Row("reallife_tab_factor_weight", CreditCatalogue.Weights[(int)factor] + "%");
            box.Headline("reallife_tab_factor_moves");
            box.Line(FinanceText.Tip(score, factor));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }

        // ---------------------------------------------------------------- Borrow

        private string WriteBorrow(InfoBox box, CreditScore score)
        {
            _spec = FinanceTables.Borrow;
            if (!score.Lends)
            {
                // Under the threshold: the reason, what would raise the rating, and no way to apply.
                _lender = null;
                box.Title(FinanceText.T("reallife_econ_borrow_refused_title"));
                box.Chip(FinanceText.Grade(score.Grade), Tone(score.Grade));
                box.Line(FinanceText.T("reallife_econ_borrow_refused", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade), "threshold", CreditCatalogue.LendingThreshold.ToString()), CellTone.Red);
                int tips = CreditScoring.Tips(score, _tips);
                if (tips > 0)
                {
                    box.Headline("reallife_econ_credit_raise");
                    for (int i = 0; i < tips; i++)
                    {
                        box.Line(FinanceText.Tip(score, _tips[i]));
                    }
                }
                Say("refused", FinanceText.T("reallife_econ_row_no_lender", "threshold", CreditCatalogue.LendingThreshold.ToString()));
                Meta.Guide.Point(box, GuideTitle);
                return null;
            }
            IReadOnlyList<Lender> lenders = BankTerms.Lenders;
            string selected = ProductRows();   // the other kinds of credit, in the same list as the banks
            bool found = false;
            string loanKind = FinanceText.T(FinanceTables.OfferKinds[0]);
            for (int i = 0; i < lenders.Count; i++)
            {
                Lender lender = lenders[i];
                TableRow row = Row("lender:" + i);
                row.Pick = true;
                row.Index = i;
                row.Kind = 0;
                float most = PhoneLending.MostNow(lender, score.Value), rate = PhoneLending.RateOffered(lender, score.Value);
                bool lends = most >= CreditCatalogue.LoanLeast;
                row.Set(0, lender.Name);
                row.Set(1, loanKind);
                row.Set(2, FinanceText.Money(most), most, lends ? TableRow.Plain : TableRow.Red);
                row.Set(3, FinanceText.T("reallife_econ_row_rate", "rate", rate.ToString("0.#")), rate);
                if (lends)
                {
                    // What borrowing the most on offer would take each day, its fee and its term: one figure to compare lenders by.
                    LoanQuote quote = PhoneLending.Quote(lender, Mathf.FloorToInt(most), score.Value);
                    row.Set(4, FinanceText.Money(quote.DailyPayment + quote.DailyInterest), quote.DailyPayment + quote.DailyInterest);
                    row.Set(5, FinanceText.Money(quote.Fee), quote.Fee);
                    row.Set(6, FinanceText.T("reallife_econ_row_days", "days", quote.Days.ToString()), quote.Days);
                }
                row.Set(7, FinanceText.T(lends ? "reallife_econ_row_on_offer" : "reallife_econ_row_not_offered"), lends ? TableRow.Plain : TableRow.Red);
                if (lender == _lender)
                {
                    selected = row.Id;
                    found = true;
                }
            }
            if (!found)
            {
                _lender = null;
            }
            if (lenders.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_banks"));
            }
            if (_lender == null && _product >= 0)
            {
                WriteProduct(box);
                return selected;
            }
            if (_lender == null)
            {
                box.Title(FinanceText.T("reallife_econ_tab_borrow"));
                box.Chip(FinanceText.Grade(score.Grade), Tone(score.Grade));
                box.Line(FinanceText.T("reallife_econ_borrow_pick"));
                box.Section("reallife_look_sec_how");
                box.Note(FinanceText.T("reallife_econ_borrow_fee_note", "share", FeeShare(), "least", FinanceText.Money(CreditCatalogue.PhoneFeeLeast)));
                box.Note(FinanceText.T("reallife_tab_borrow_columns_note"));
                box.Note(FinanceText.T("reallife_econ_borrow_repay_note"));
                Meta.Guide.Point(box, GuideTitle);
                return null;
            }
            WriteOffer(box, score);
            return selected;
        }

        private static string FeeShare()
        {
            return (CreditCatalogue.PhoneFeeShare * 100f).ToString("0.#") + "%";
        }

        private void WriteOffer(InfoBox box, CreditScore score)
        {
            Lender lender = _lender;
            int most = Mathf.FloorToInt(PhoneLending.MostNow(lender, score.Value));
            box.Title(lender.Name);
            box.Subtitle(FinanceText.T(FinanceTables.OfferKinds[0]));
            box.Section("reallife_look_sec_terms");
            box.Row("reallife_econ_offer_most", FinanceText.Money(most));
            if (most < CreditCatalogue.LoanLeast)
            {
                box.Line(FinanceText.T("reallife_econ_offer_nothing", "least", FinanceText.Money(CreditCatalogue.LoanLeast)), CellTone.Red);
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            _amount = Mathf.Clamp(_amount, CreditCatalogue.LoanLeast, most);
            Amounts(most);
            LoanQuote quote = PhoneLending.Quote(lender, _amount, score.Value);
            box.Row("reallife_econ_offer_rate", FinanceText.T("reallife_econ_row_rate", "rate", quote.AnnualRate.ToString("0.#")));
            box.Headline("reallife_econ_offer_amount");
            if (!box.HasSelector || !box.Selector(_amounts.Count, _amounts.IndexOf(_amount), i => FinanceText.Money(_amounts[i]), i =>
                {
                    _amount = _amounts[i];
                    Refresh();
                }))
            {
                box.Line(FinanceText.Money(_amount));
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform entry = box.ButtonRow();
            if (uis != null && uis.textInputUi != null)
            {
                // The game's own text box. It takes any text, so what is typed is read as a whole amount (digits; the
                // comma the game writes in "$5,000" is let through) and checked against the least and the most.
                InfoBox.Button(entry, InfoBox.Additional, "reallife_econ_offer_type", () =>
                    uis.textInputUi.Show("reallife_econ_offer_placeholder", "common_confirm", text => UiClone.Safe(() => Typed(text)), null, _amount.ToString(), false, TypedAmountLength));
            }
            box.Headline("reallife_econ_offer_cost");
            box.Row("reallife_econ_offer_payment", FinanceText.Money(quote.DailyPayment));
            box.Row("reallife_econ_offer_interest", FinanceText.Money(quote.DailyInterest));
            box.Row("reallife_econ_offer_days", quote.Days.ToString());
            box.Row("reallife_econ_offer_fee", FinanceText.Money(quote.Fee));
            box.Row("reallife_econ_offer_total", FinanceText.Money(quote.Cost));
            box.Row("reallife_tab_offer_repay", FinanceText.Money(_amount + quote.Cost));
            float owedThem = GameLoans.OwedTo(lender.Address);
            if (owedThem > 0f)
            {
                box.Row("reallife_tab_offer_owed_them", FinanceText.Money(owedThem), CellTone.Yellow);
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_offer_apply", Apply);
            InfoBox.Button(buttons, InfoBox.Additional, "common_cancel", ClosePick);
            // What the rating did to the rate: this bank's rate at the best rating there is, against the one offered.
            float best = PhoneLending.RateOffered(lender, CreditCatalogue.ScoreMax);
            box.Note(quote.AnnualRate > best + 0.05f
                ? FinanceText.T("reallife_tab_offer_rating_adds", "points", (quote.AnnualRate - best).ToString("0.#"), "best", best.ToString("0.#"))
                : FinanceText.T("reallife_tab_offer_rating_best"));
            Meta.Guide.Point(box, GuideTitle);
        }

        // The amounts the selector steps through: round figures up to the most on offer, the most itself, and what was typed.
        private void Amounts(int most)
        {
            _amounts.Clear();
            for (int i = 0; i < AmountSteps.Length && AmountSteps[i] < most; i++)
            {
                _amounts.Add(AmountSteps[i]);
            }
            _amounts.Add(most);
            if (!_amounts.Contains(_amount))
            {
                int at = 0;
                while (at < _amounts.Count && _amounts[at] < _amount)
                {
                    at++;
                }
                _amounts.Insert(at, _amount);
            }
        }

        private void Typed(string text)
        {
            if (_lender == null || !CreditLedger.Ready)
            {
                return;
            }
            int typed = 0;
            bool digits = false;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    typed = Mathf.Min(typed * 10 + (c - '0'), 99999999);
                    digits = true;
                }
                else if (c != ',' && c != ' ' && c != '$')
                {
                    digits = false;   // anything else (a decimal point, a letter) is no whole amount: the game's own "invalid amount" says so
                    break;
                }
            }
            int most = Mathf.FloorToInt(PhoneLending.MostNow(_lender, CreditLedger.Score.Value));
            if (!digits)
            {
                Notifications.ShowError("common_notification_invalid_amount", null, false);
                return;
            }
            if (typed < CreditCatalogue.LoanLeast)
            {
                Notifications.Show(NotificationType.Error, "reallife_econ_note_too_little", new Dictionary<string, string> { { "least", FinanceText.Money(CreditCatalogue.LoanLeast) } }, 6f, null, null, true, false);
                typed = CreditCatalogue.LoanLeast;
            }
            else if (typed > most)
            {
                Notifications.Show(NotificationType.Error, "reallife_econ_note_too_much", new Dictionary<string, string> { { "lender", _lender.Name }, { "most", FinanceText.Money(most) } }, 6f, null, null, true, false);
                typed = most;
            }
            _amount = typed;
            Refresh();
        }

        private void RowClicked(TableRow row)
        {
            if (_standing && _tab == Finance.Overview)
            {
                if (row != null && row.Pick)
                {
                    _standingPick = row.Id;
                    Refresh();
                }
                else
                {
                    _table.ClearSelection();
                }
                return;
            }
            if (row != null && row.Pick && _tab == Finance.Credit && !_city)
            {
                _factor = row.Index;
                Refresh();
                return;
            }
            if (row != null && row.Pick && PickedOther(row))
            {
                Refresh();
                return;
            }
            if (row == null || !row.Pick || _tab != Finance.Borrow || row.Index < 0 || row.Index >= BankTerms.Lenders.Count)
            {
                _table.ClearSelection();
                return;
            }
            Lender lender = BankTerms.Lenders[row.Index];
            if (lender != _lender)
            {
                _lender = lender;
                _amount = 0;
            }
            _product = -1;
            Refresh();
        }

        private void ClosePick()
        {
            _lender = null;
            _product = -1;
            _picked = null;
            _factor = -1;
            _standingPick = null;
            _table.ClearSelection();
            Refresh();
        }

        // The game's own question box: everything the loan costs is said before it is taken.
        private void Apply()
        {
            Lender lender = _lender;
            if (lender == null || !CreditLedger.Ready)
            {
                return;
            }
            CreditRefusal refusal;
            if (!CreditLedger.CanBorrow(out refusal))
            {
                _director.TellRefused();
                Refresh();
                return;
            }
            int amount = _amount;
            LoanQuote quote = PhoneLending.Quote(lender, amount, CreditLedger.Score.Value);
            GameConfirm.Show("reallife_econ_confirm_title".Localize(new { lender = lender.Name }),
                "reallife_econ_confirm_body".Localize(new
                {
                    amount = FinanceText.Money(amount),
                    lender = lender.Name,
                    rate = quote.AnnualRate.ToString("0.#"),
                    payment = FinanceText.Money(quote.DailyPayment),
                    interest = FinanceText.Money(quote.DailyInterest),
                    days = quote.Days.ToString(),
                    fee = FinanceText.Money(quote.Fee),
                    cost = FinanceText.Money(quote.Cost)
                }),
                () => UiClone.Safe(() => Confirmed(lender, amount)), null, "reallife_econ_offer_apply", "common_cancel", false, false);
        }

        private void Confirmed(Lender lender, int amount)
        {
            LoanQuote quote;
            LoanAnswer answer = _director.Borrow(lender, amount, out quote);
            switch (answer)
            {
                case LoanAnswer.Approved:
                    Notifications.Show(NotificationType.Success, "reallife_econ_note_lent", new Dictionary<string, string>
                    {
                        { "lender", lender.Name }, { "amount", FinanceText.Money(amount) }, { "fee", FinanceText.Money(quote.Fee) },
                        { "payment", FinanceText.Money(quote.DailyPayment + quote.DailyInterest) }
                    }, 10f, null, null, true, false);
                    _lender = null;
                    _tab = Finance.Overview;
                    break;
                case LoanAnswer.TooMuch:
                    Notifications.Show(NotificationType.Error, "reallife_econ_note_too_much", new Dictionary<string, string>
                    {
                        { "lender", lender.Name }, { "most", FinanceText.Money(PhoneLending.MostNow(lender, CreditLedger.Value)) }
                    }, 8f, null, null, true, false);
                    break;
                case LoanAnswer.TooLittle:
                    Notifications.Show(NotificationType.Error, "reallife_econ_note_too_little", new Dictionary<string, string> { { "least", FinanceText.Money(CreditCatalogue.LoanLeast) } }, 6f, null, null, true, false);
                    break;
                default:
                    if (CreditLedger.Ready)
                    {
                        _director.TellRefused();
                    }
                    break;
            }
            if (_shell != null && _shell.Root != null && _shell.Root.activeInHierarchy)
            {
                _ready = _director.Running && CreditLedger.Ready;
                Refresh();
            }
        }

        // ---------------------------------------------------------------- Cash flow

        private void WriteCashFlow(InfoBox box)
        {
            int today = SaveGameManager.Current.Day;
            _flow.Read(_period, today);
            _spec = FinanceTables.Cash;
            box.Title(FinanceText.T("reallife_econ_tab_cash"));
            if (!box.HasSelector || !box.Selector(CashFlow.PeriodDays.Length, _period, CashFlow.PeriodName, i =>
                {
                    _period = i;
                    Refresh();
                }))
            {
                box.Line(CashFlow.PeriodName(_period));
            }
            box.Section("reallife_look_sec_totals");
            box.Row("reallife_econ_cash_in", FinanceText.Money(_flow.In), CellTone.Green);
            box.Row("reallife_econ_cash_out", FinanceText.Money(-_flow.Out), CellTone.Red);
            float net = _flow.In + _flow.Out;
            box.Row("reallife_econ_cash_net", FinanceText.Money(net), net >= 0f ? CellTone.Green : CellTone.Red);
            if (_flow.OldestDay > _flow.FromDay && _flow.FromDay >= 1)
            {
                box.Note(FinanceText.T("reallife_econ_cash_reach", "day", _flow.OldestDay.ToString()));
            }
            CategoryLines(box);
            if (_flow.TopCount > 0)
            {
                box.Headline("reallife_econ_cash_largest");
                for (int i = 0; i < _flow.TopCount; i++)
                {
                    CashFlow.Item item = _flow.Top[i];
                    box.RowWords(CashFlow.ItemName(item), FinanceText.Money(item.Amount), item.Amount >= 0f ? CellTone.Green : CellTone.Red);
                }
            }
            // Every movement of money, newest first; the game's own words for it, and what it was for.
            for (int i = _flow.Entries.Count - 1; i >= 0; i--)
            {
                CashFlow.Entry entry = _flow.Entries[i];
                TableRow row = Row("cash:" + i);
                row.Kind = entry.Category;
                row.Set(0, FinanceText.Day(entry.Day), entry.Day);
                row.Set(1, CashFlow.ItemName(entry.Type, entry.Data));
                row.Set(2, FinanceText.T(CashCategory.Keys[entry.Category]));
                if (entry.Amount > 0f)
                {
                    row.Set(3, FinanceText.Money(entry.Amount), entry.Amount, TableRow.Green);
                }
                else
                {
                    row.Set(4, FinanceText.Money(-entry.Amount), -entry.Amount, TableRow.Red);
                }
            }
            if (_rows.Count == 0)
            {
                Say("none", FinanceText.T("reallife_econ_row_no_cash"));
            }
            Meta.Guide.Point(box, GuideTitle);
        }

        // The category chosen in the table's filter: what it came to in the period, and its share of all that came in and went out.
        private void CategoryLines(InfoBox box)
        {
            int category = FinanceTables.Cash.Filters[0].Chosen;
            if (category < 0 || category >= CashCategory.Count)
            {
                return;
            }
            float came = _flow.CategoryIn[category], went = -_flow.CategoryOut[category];
            if (came <= 0f && went <= 0f)
            {
                return;   // (no row of it in this period: the table goes back to all of them)
            }
            box.Headline(CashCategory.Keys[category]);
            if (came > 0f)
            {
                box.Line(FinanceText.T("reallife_tab_cash_share_in", "amount", FinanceText.Money(came), "share", Share(came, _flow.In)));
            }
            if (went > 0f)
            {
                box.Line(FinanceText.T("reallife_tab_cash_share_out", "amount", FinanceText.Money(went), "share", Share(went, -_flow.Out)));
            }
            box.Row("reallife_econ_cash_net", FinanceText.Money(came - went), came >= went ? CellTone.Green : CellTone.Red);
        }

        /// <summary>A credit grade's tone: green from good up, plain for fair, orange for poor, red for very poor.</summary>
        internal static CellTone Tone(CreditGrade grade)
        {
            return grade >= CreditGrade.Good ? CellTone.Green : grade == CreditGrade.Fair ? CellTone.Plain : grade == CreditGrade.Poor ? CellTone.Yellow : CellTone.Red;
        }

        private static string Share(float part, float whole)
        {
            return whole > 0f ? Mathf.RoundToInt(part / whole * 100f) + "%" : "0%";
        }
    }
}
