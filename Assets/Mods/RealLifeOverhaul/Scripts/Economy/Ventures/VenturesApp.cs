using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// What the Ventures app shows, in the game's frame (GameUi.AppShell, copied from MyEmployees) as Finance, the
    /// Planner and the Guide are. Four tabs over the table on the right, and on the left the white box:
    ///
    ///   Investors   offers waiting for an answer, money promised, deals; pick one to answer it, counter it, buy the
    ///               investor out or answer a demand; "Raise money" leads through three pages (the business, for a new
    ///               one its kind and budget, the investor) to a meeting (VenturesApp.Investors.cs)
    ///   Market      the listed companies with price, today's move and the thirty-day trend; pick one to buy or sell
    ///   Portfolio   the shares held with their worth and gain or loss; pick one for its company's page
    ///               (VenturesApp.Market.cs)
    ///   Property    the buildings the player owns as the game lets them: cost, rent, yield, mortgage
    ///               (VenturesApp.Property.cs)
    ///
    /// A list goes in the table, which scrolls; the white box, which does not, shows only the one thing picked, and a
    /// tab that needs more than one page has pages with a Back button. The table and its rows are the Finance app's
    /// (FinanceTable, the game's own BaTable).
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and figures are made when it
    /// opens, when a tab is picked and when the player changes something in it.
    /// </summary>
    public sealed partial class VenturesApp
    {
        private const float WidthA = 620f, WidthB = 500f, WidthC = 300f, WidthD = 300f;

        private static readonly string[] TabKeys = { "reallife_ven_tab_investors", "reallife_ven_tab_market", "reallife_ven_tab_portfolio", "reallife_ven_tab_property" };
        private static readonly string[] ColumnNames = { "ColumnA", "ColumnB", "ColumnC", "ColumnD" };
        private static readonly float[] ColumnWidths = { WidthA, WidthB, WidthC, WidthD };
        private static readonly string[] InvestorColumns = { "reallife_ven_col_investor", "reallife_ven_col_business", "reallife_ven_col_terms", "reallife_ven_col_state" };
        private static readonly string[] BusinessColumns = { "reallife_ven_col_business", "reallife_ven_col_profit", "reallife_ven_col_age", "reallife_ven_col_promised" };
        private static readonly string[] TypeColumns = { "reallife_ven_col_kind", "reallife_ven_col_sector", "reallife_ven_col_liked", "reallife_ven_col_run" };
        private static readonly string[] WhoColumns = { "reallife_ven_col_investor", "reallife_ven_col_looks", "reallife_ven_col_sums", "reallife_ven_col_open" };
        private static readonly string[] MarketColumns = { "reallife_ven_col_company", "reallife_ven_col_price", "reallife_ven_col_today", "reallife_ven_col_trend" };
        private static readonly string[] PortfolioColumns = { "reallife_ven_col_company", "reallife_ven_col_shares", "reallife_ven_col_worth", "reallife_ven_col_gain" };
        private static readonly string[] PropertyColumns = { "reallife_ven_col_building", "reallife_ven_col_cost", "reallife_ven_col_rent", "reallife_ven_col_yield" };

        public static IModLogger Logger;

        private readonly VenturesDirector _director;
        private readonly List<FinanceRow> _rows = new List<FinanceRow>(24);
        private readonly List<FinanceRow> _pool = new List<FinanceRow>(24);
        private readonly List<BuildingRegistration> _businesses = new List<BuildingRegistration>(16);
        private readonly List<string> _types = new List<string>(40);

        private AppShell _shell;
        private FinanceTable _table;
        private FullMenu _menu;
        private int _tab, _wanted = -1;
        private bool _ready;       // the director has the file open for this opening
        private string _picked;    // the row picked on the tab that shows

        public VenturesApp(VenturesDirector director)
        {
            _director = director;
        }

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            _menu = menu;
            SessionWatch.Mark("ventures: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, VenturesDirector.AppId);
            if (_shell == null || !PrepareRow(_shell.RowPrefab) || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(ColumnNames, InvestorColumns, ColumnWidths, 1))
            {
                return null;
            }
            _table = _shell.List.gameObject.AddComponent<FinanceTable>();
            _table.scroller = _shell.Scroller;
            _table.prefab = _shell.RowPrefab.AddComponent<FinanceCellView>();
            _table.allowRowSelection = true;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        // The game's employee row has a tick box and ten cells. Four are kept and renamed, as Finance and the Planner keep
        // them: the name, the task (its warning sign hidden), the business and the satisfaction, which the game sets to the right.
        private static bool PrepareRow(GameObject row)
        {
            Transform t = row.transform;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            Transform a = t.Find("EmployeeName"), b = t.Find("TaskInfo"), c = t.Find("BusinessName"), d = t.Find("Satisfaction");
            if (layout == null || a == null || b == null || c == null || d == null || b.Find("Task") == null)
            {
                UiClone.Fallback("the game's employee row is not built as expected, so the Ventures app is not added");
                return false;
            }
            UiClone.KeepOnly(t, "Background", "RaycastTarget", "EmployeeName", "TaskInfo", "BusinessName", "Satisfaction");
            a.name = "A";
            b.name = "B";
            c.name = "C";
            d.name = "D";
            AppShell.RowLayout(layout);
            AppShell.Width(a, WidthA, false);
            AppShell.Width(b, WidthB, true);
            AppShell.Width(c, WidthC, false);
            AppShell.Width(d, WidthD, false);
            AppShell.OneLine(a.GetComponent<TMP_Text>());
            AppShell.OneLine(c.GetComponent<TMP_Text>());
            AppShell.OneLine(d.GetComponent<TMP_Text>());
            AppShell.OneLine(b.Find("Task").GetComponent<TMP_Text>());
            Transform sign = b.Find("WarningIcon");
            if (sign != null)
            {
                sign.gameObject.SetActive(false);
            }
            return true;
        }

        // ---------------------------------------------------------------- opening and the tabs

        /// <summary>The tab to show the next time the app opens (a notification was clicked).</summary>
        public void Want(int tab)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            if (Showing)
            {
                Opened();
            }
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

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
                _picked = null;
                _page = PageList;
            }
            _ready = _director.AppOpened();
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab)
            {
                _tab = tab;
                _picked = null;
                _page = PageList;
                Refresh();
            }
        }

        // Everything the open tab shows, made again: asked for when the app opens, a tab is picked or something has changed.
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
            _shell.ShowTab(_tab);
            InfoBox box = _shell.Box;
            box.Clear();
            VenturesFile file = _ready ? _director.File : null;
            string[] columns = InvestorColumns;
            if (file == null || SaveGameManager.Current == null)
            {
                UiClone.Words(_shell.Header, FinanceText.T("reallife_ven_app"));
                box.Title(FinanceText.T("reallife_ven_off_title"));
                box.Line(FinanceText.T("reallife_ven_off_body"));
                _picked = null;
            }
            else
            {
                int day = SaveGameManager.Current.Day;
                switch (_tab)
                {
                    case VenturesPhone.Market: columns = WriteMarket(box, file); break;
                    case VenturesPhone.Portfolio: columns = WritePortfolio(box, file); break;
                    case VenturesPhone.Property: columns = WriteProperty(box, file, day); break;
                    default: columns = WriteInvestors(box, file, day); break;
                }
            }
            _shell.SetColumns(ColumnNames, columns, ColumnWidths, 1);
            box.Done();
            _table.Show(_rows, _picked);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Ventures: tab " + _tab + " written, " + _rows.Count + " row(s); " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
            }
        }

        private FinanceRow Row(string kind, int index)
        {
            FinanceRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new FinanceRow();
            }
            row.Id = kind + index;
            row.A = row.B = row.C = row.D = null;
            row.Info = false;
            row.Pick = true;
            row.Index = index;
            row.Tone = 0;
            _rows.Add(row);
            return row;
        }

        private void Say(string words)
        {
            FinanceRow row = Row("say:", _rows.Count);
            row.Info = true;
            row.Pick = false;
            row.A = words;
        }

        // The number at the end of a row's id ("offer:12"), if the id is of that kind; -1 otherwise.
        private static int Picked(string id, string kind)
        {
            int number;
            return id != null && id.StartsWith(kind, System.StringComparison.Ordinal) && int.TryParse(id.Substring(kind.Length), out number) ? number : -1;
        }

        private void RowClicked(FinanceRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            if (_tab == VenturesPhone.Investors && InvestorRowClicked(row))
            {
                return;
            }
            // (a holding's row leads to its company's page)
            _picked = row.Id.StartsWith(HoldRow, System.StringComparison.Ordinal) ? CompanyRow + row.Index : row.Id;
            _shareStep = 0;
            Refresh();
        }

        private void ClosePick()
        {
            _picked = null;
            _table.ClearSelection();
            Refresh();
        }

        private static string Pair(string labelKey, string value)
        {
            return FinanceText.Pair(labelKey, value);
        }

        private static string Money(float amount)
        {
            return FinanceText.Money(amount);
        }
    }
}
