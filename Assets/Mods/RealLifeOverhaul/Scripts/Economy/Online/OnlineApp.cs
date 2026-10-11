using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the Web shop app shows, in the game's frame (GameUi.AppShell, copied from MyEmployees) as Finance,
    /// Ventures, the Company app, the Planner and the Guide are. It is an app of its own and not a tab of the Company
    /// app: that app's bar already holds six tabs, and a site has three subjects of its own. Three tabs over the
    /// table on the right, and on the left the white box:
    ///
    ///   Shops       every site with today's orders and its rating, and under them the businesses and warehouses that
    ///               have none; pick a site for its figures, its rating and its settings, or a business to open one
    ///               (OnlineApp.Shops.cs)
    ///   Orders      the queue: every open order with its district, its goods, its promise and where it stands
    ///   Catalogue   one site's products with stock, price and sales, and what else the place stocks; pick one to
    ///               price it or to put it on or off the site (OnlineApp.Lists.cs)
    ///
    /// A list goes in the table, which scrolls; the white box, which does not, shows only the one thing picked. The
    /// table and its rows are the Finance app's (FinanceTable, the game's own BaTable); the columns are declared in
    /// OnlineColumns.
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and figures are made when it
    /// opens, when a tab is picked and when the player changes something in it.
    /// </summary>
    public sealed partial class OnlineApp
    {
        private static readonly string[] TabKeys = { "reallife_online_tab_shops", "reallife_online_tab_orders", "reallife_online_tab_catalogue" };

        public static IModLogger Logger;

        private readonly OnlineDirector _director;
        private readonly List<FinanceRow> _rows = new List<FinanceRow>(32);
        private readonly List<FinanceRow> _pool = new List<FinanceRow>(32);
        private readonly DayBook _week = new DayBook();

        private AppShell _shell;
        private FinanceTable _table;
        private int _tab, _wanted = -1;
        private string _wantPick;
        private bool _ready;        // the director is running for this opening
        private bool _writing;      // a page is being written: a change it causes does not write it again
        private string _picked;     // the row picked on the tab that shows
        private int _page;          // the page of a site in the white box

        public OnlineApp(OnlineDirector director)
        {
            _director = director;
        }

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            SessionWatch.Mark("online shops: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, OnlineDirector.AppId);
            if (_shell == null || !PrepareRow(_shell.RowPrefab) || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(OnlineColumns.Names, OnlineColumns.Shops, OnlineColumns.Widths, OnlineColumns.Flexible))
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
                UiClone.Fallback("the game's employee row is not built as expected, so the Web shop app is not added");
                return false;
            }
            UiClone.KeepOnly(t, "Background", "RaycastTarget", "EmployeeName", "TaskInfo", "BusinessName", "Satisfaction");
            a.name = "A";
            b.name = "B";
            c.name = "C";
            d.name = "D";
            AppShell.RowLayout(layout);
            AppShell.Width(a, OnlineColumns.WidthA, false);
            AppShell.Width(b, OnlineColumns.WidthB, true);
            AppShell.Width(c, OnlineColumns.WidthC, false);
            AppShell.Width(d, OnlineColumns.WidthD, false);
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

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>The tab to show the next time the app opens, and what is picked on it (a notification was clicked).</summary>
        public void Want(int tab, string pick)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            _wantPick = pick;
            if (Showing)
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
                _picked = _wantPick;
                _page = PageFigures;
                _wanted = -1;
                _wantPick = null;
            }
            _ready = _director.AppOpened();
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
        }

        /// <summary>The director says something has changed: the page is written again if it is showing.</summary>
        public void Changed()
        {
            if (Showing && _ready && !_writing)
            {
                Refresh();
            }
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab || _picked != null)
            {
                _tab = tab;
                _picked = null;
                _page = PageFigures;
                Refresh();
            }
        }

        // Everything the open tab shows, made again: asked for when the app opens, a tab is picked or something has changed.
        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                float started = Time.realtimeSinceStartup;
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                OnlineState state = _ready ? _director.State : null;
                string[] columns = OnlineColumns.Shops;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, OnlineText.T("reallife_online_app"));
                    box.Title(OnlineText.T("reallife_online_off_title"));
                    box.Line(OnlineText.T("reallife_online_off_body"));
                    _picked = null;
                }
                else
                {
                    int day = SaveGameManager.Current.Day, now = day * 24 + SaveGameManager.Current.Hour;
                    int open = 0;
                    for (int i = 0; i < state.Shops.Count; i++)
                    {
                        open += state.Shops[i].Orders.Count;
                    }
                    UiClone.Words(_shell.Header, OnlineText.T("reallife_online_head", "sites", state.Shops.Count.ToString(), "orders", open.ToString()));
                    switch (_tab)
                    {
                        case OnlineDirector.TabOrders: columns = WriteOrders(box, state, day, now); break;
                        case OnlineDirector.TabCatalogue: columns = WriteCatalogue(box, state, day); break;
                        default: columns = WriteShops(box, state, day, now); break;
                    }
                }
                _shell.SetColumns(OnlineColumns.Names, columns, OnlineColumns.Widths, OnlineColumns.Flexible);
                box.Done();
                _table.Show(_rows, _picked);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Online shops: tab " + _tab + " written, " + _rows.Count + " row(s); " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        private FinanceRow Row(string id)
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
            row.Id = id;
            row.A = row.B = row.C = row.D = null;
            row.Info = false;
            row.Pick = true;
            row.Index = _rows.Count;
            row.Tone = 0;
            _rows.Add(row);
            return row;
        }

        private void Say(string words)
        {
            FinanceRow row = Row("say:" + _rows.Count);
            row.Info = true;
            row.Pick = false;
            row.A = words;
        }

        // What follows a kind's mark in a row's id ("shop:Main%20St~12"), if the id is of that kind; null otherwise.
        private static string Picked(string id, string kind)
        {
            return id != null && id.StartsWith(kind, System.StringComparison.Ordinal) ? id.Substring(kind.Length) : null;
        }

        private void RowClicked(FinanceRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            _picked = row.Id;
            _page = PageFigures;
            Refresh();
        }

        private void ClosePick()
        {
            _picked = null;
            _page = PageFigures;
            _table.ClearSelection();
            Refresh();
        }

        private void Page(int page)
        {
            _page = page;
            Refresh();
        }
    }
}
