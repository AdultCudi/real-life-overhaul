using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Finance app in the game's frame (GameUi.AppShell): five tabs over a table that scrolls, and the white box with
    /// the tab's figures or the row picked. Nothing runs while it is closed; it is written on opening and on each click.
    /// </summary>
    public sealed partial class FinanceApp
    {
        /// <summary>The glyph on the app's icon: the game's coin, or a picture the phone's own scene uses.</summary>
        public static readonly string[] AppGlyphs = { "Icon-Money", "Icon-Coin" };

        private const int TypedAmountLength = 9;   // characters the game's text box takes for an amount
        private const string GuideTitle = "reallife_meta_title_economy";   // the Guide's page each page points to at its foot

        private static readonly string[] TabKeys = { "reallife_econ_tab_overview", "reallife_econ_tab_credit", "reallife_econ_tab_borrow", "reallife_econ_tab_cash", "reallife_econ_tab_accounts" };
        private static readonly int[] AmountSteps = { 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000, 2500000, 5000000 };

        public static IModLogger Logger;

        private readonly EconomyDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(64);
        private readonly List<TableRow> _pool = new List<TableRow>(64);
        private readonly List<int> _amounts = new List<int>(16);
        private readonly CreditFactor[] _tips = new CreditFactor[2];
        private readonly CashFlow _flow = new CashFlow();
        private readonly CashTable _cash;   // the Cash flow tab's table, over _flow.Entries
        private bool _cashRows;             // the table showing is _cash, not _rows

        private AppShell _shell;
        private DataTable _table;
        private TableSpec _spec = FinanceTables.Off;   // the table the tab showing is written as
        private FullMenu _menu;
        private int _tab, _wanted = -1;
        private int _amount;   // the amount on the Borrow page showing
        private bool _ready;   // the director has brought the file up to date for this opening

        public FinanceApp(EconomyDirector director)
        {
            _director = director;
            _cash = new CashTable(_flow.Entries, CashWords.Game);
        }

        private CreditDesk Desk => _director.Desk;

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
                    Refresh(false);   // the table has filtered its rows itself; the box says what the category comes to
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

        /// <summary>The tab to show the next time the app opens (a notification was clicked).</summary>
        public void Want(int tab)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            if (_shell != null && _shell.Root != null && _shell.Root.activeInHierarchy)
            {
                Opened();
            }
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
            _standing = _wantStanding && _tab == Finance.Overview && StandingOn() != null;
            _wantStanding = false;
            _standingPick = _standing ? null : _standingPick;   // a notification opens the page afresh, as the rivals' does
            OpenedRivals();
            _ready = _director.AppOpened();
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab || _standing || _rivals)
            {
                _tab = tab;
                _standing = false;
                _standingPick = null;
                _rivals = false;
                _rivalsPick = null;
                _factor = -1;
                Refresh();
            }
        }

        private void Refresh()
        {
            Refresh(true);
        }

        // Everything the tab showing has, made again. With 'table' false only the box is, from what was read for the table.
        private void Refresh(bool table)
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            if (table)
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _cashRows = false;
            }
            StandingPage standing = _standing && _tab == Finance.Overview ? StandingOn() : null;
            StandingPage rivals = _rivals && _tab == Finance.Overview ? RivalsOn() : null;
            _shell.ShowTab(standing != null || rivals != null ? -1 : _tab);   // no tab lit on these pages, so Overview's leads back
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
                    case Finance.CashFlow: WriteCashFlow(box, table); break;
                    case Finance.Accounts: selected = WriteAccounts(box); break;
                    default:
                        if (rivals != null)
                        {
                            WriteRivals(box, rivals);
                            selected = _rivalsPick;
                        }
                        else if (standing != null)
                        {
                            WriteStanding(box, standing);
                            selected = _standingPick;
                        }
                        else
                        {
                            WriteOverview(box, score);
                        }
                        break;
                }
            }
            box.Done();
            if (table && _cashRows)
            {
                _table.Show(_spec, _cash, selected);   // written as it scrolls: some fifteen rows of words, not one for every movement
            }
            else if (table)
            {
                _table.Show(_spec, _rows, selected);
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Finance: tab " + _tab + (table ? " written, " + (_cashRows ? _cash.Count + " row(s) as they scroll" : _rows.Count + " row(s)") : ": its box written again")
                             + "; " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms.");
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

        private void RowClicked(TableRow row)
        {
            if (_rivals && _tab == Finance.Overview)
            {
                PickOnPage(row, ref _rivalsPick);
                return;
            }
            if (_standing && _tab == Finance.Overview)
            {
                PickOnPage(row, ref _standingPick);
                return;
            }
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            if (_tab == Finance.Credit)
            {
                _factor = row.Index;
                Refresh();
                return;
            }
            if (_tab == Finance.Accounts)
            {
                _picked = row.Id;
                Refresh();
                return;
            }
            if (_tab != Finance.Borrow)
            {
                _table.ClearSelection();
                return;
            }
            if (row.Id != null && row.Id.StartsWith("product:", StringComparison.Ordinal))
            {
                PickProduct(row.Index);
                Refresh();
                return;
            }
            if (row.Index < 0 || row.Index >= BankTerms.Lenders.Count)
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

        // On a page another feature writes: a row that can be picked is, anything else clears the selection.
        private void PickOnPage(TableRow row, ref string pick)
        {
            if (row != null && row.Pick)
            {
                pick = row.Id;
                Refresh();
            }
            else
            {
                _table.ClearSelection();
            }
        }

        private void ClosePick()
        {
            _lender = null;
            _product = -1;
            _picked = null;
            _factor = -1;
            _standingPick = null;
            _rivalsPick = null;
            _table.ClearSelection();
            Refresh();
        }

        /// <summary>A credit grade's tone: green from good up, plain for fair, orange for poor, red for very poor.</summary>
        internal static CellTone Tone(CreditGrade grade)
        {
            return grade >= CreditGrade.Good ? CellTone.Green : grade == CreditGrade.Fair ? CellTone.Plain : grade == CreditGrade.Poor ? CellTone.Yellow : CellTone.Red;
        }

        internal static string Rate(int rateBp)
        {
            return FinanceText.T("reallife_econ_row_rate", "rate", (rateBp / 100f).ToString("0.#"));
        }

        internal static string Why(ProductAnswer answer)
        {
            switch (answer)
            {
                case ProductAnswer.BelowThreshold: return FinanceText.T("reallife_econ_why_threshold", "threshold", CreditCatalogue.LendingThreshold.ToString());
                case ProductAnswer.Bankrupt: return FinanceText.T("reallife_econ_why_bankrupt", "days", ProductCatalogue.BankruptNoCreditDays.ToString());
                case ProductAnswer.Arrangement: return FinanceText.T("reallife_econ_why_arrangement");
                case ProductAnswer.NoBank: return FinanceText.T("reallife_econ_row_no_banks");
                case ProductAnswer.TooLittle: return FinanceText.T("reallife_econ_why_too_little");
                case ProductAnswer.TooMuch: return FinanceText.T("reallife_econ_why_too_much");
                case ProductAnswer.NoSecurity: return FinanceText.T("reallife_econ_why_no_security", "days", ProductCatalogue.BusinessDaysOnRecord.ToString());
                case ProductAnswer.NoCash: return FinanceText.T("reallife_econ_why_no_cash");
                case ProductAnswer.NothingOwed: return FinanceText.T("reallife_econ_why_nothing");
                case ProductAnswer.Busy: return FinanceText.T("reallife_econ_why_busy");
                case ProductAnswer.AlreadyOpen: return FinanceText.T("reallife_econ_why_open");
                default: return FinanceText.T("reallife_econ_why_off");
            }
        }

        /// <summary>A whole amount from what was typed ("$5,000" too); false for anything else.</summary>
        internal static bool Whole(string text, out int amount)
        {
            amount = 0;
            bool digits = false;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    amount = Mathf.Min(amount * 10 + (c - '0'), 99999999);
                    digits = true;
                }
                else if (c != ',' && c != ' ' && c != '$')
                {
                    return false;
                }
            }
            return digits;
        }

        private static void Tell(NotificationType type, string key, Dictionary<string, string> data = null)
        {
            Notifications.Show(type, key, data, 9f, null, null, true, false);
        }

        private void Said(ProductAnswer answer)
        {
            if (answer != ProductAnswer.Yes)
            {
                Tell(NotificationType.Error, "reallife_econ_note_not_done", new Dictionary<string, string> { { "why", Why(answer) } });
            }
            After();
        }

        // Something was done: the app is written again if it is still showing.
        private void After()
        {
            if (_shell != null && _shell.Root != null && _shell.Root.activeInHierarchy)
            {
                _ready = _director.Running && CreditLedger.Ready;
                Refresh();
            }
        }
    }
}
