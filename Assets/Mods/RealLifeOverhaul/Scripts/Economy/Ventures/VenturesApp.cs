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
    /// Planner and the Guide are. Two tabs over the table on the right, and on the left the white box:
    ///
    ///   Investors   offers waiting for an answer, money promised, deals; pick one to answer it, counter it, buy the
    ///               investor out or answer a demand; "Raise money" leads through three pages (the business, for a new
    ///               one its kind and budget, the investor) to a meeting (VenturesApp.Investors.cs)
    ///   Property    the buildings the player owns as the game lets them: cost, rent, yield, mortgage
    ///               (VenturesApp.Property.cs)
    ///
    /// (Shares are the Stocks app's: Scripts/Markets.)
    ///
    /// A list goes in the table, which scrolls; the white box, which does not, shows only the one thing picked, and a
    /// tab that needs more than one page has pages with a Back button. The table and its rows are the Finance app's
    /// (DataTable, the game's own BaTable).
    ///
    /// Every table is declared in VenturesApp.Tables.cs and is sorted, filtered and summed by the kit (GameUi.DataTable).
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and figures are made when it
    /// opens, when a tab is picked and when the player changes something in it.
    /// </summary>
    public sealed partial class VenturesApp
    {
        private static readonly string[] TabKeys = { "reallife_ven_tab_investors", "reallife_ven_tab_property" };
        private const string GuideTitle = "reallife_meta_title_ventures";   // the Guide's page every page of the app points to at its foot
        public static IModLogger Logger;

        private readonly VenturesDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(24);
        private readonly List<TableRow> _pool = new List<TableRow>(24);
        private readonly List<BuildingRegistration> _businesses = new List<BuildingRegistration>(16);
        private readonly List<string> _types = new List<string>(40);

        private AppShell _shell;
        private DataTable _table;
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
            using var piece = SessionWatch.Piece("ventures: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, VenturesDirector.AppId);
            _table = DataTable.On(_shell, "Ventures");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(VenturesTables.Off))
            {
                return null;
            }
            _table.Money = amount => FinanceText.Money((float)amount);
            _table.SelectionLost = ClosePick;
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
            TableSpec table = VenturesTables.Off;
            if (file == null || SaveGameManager.Current == null)
            {
                UiClone.Words(_shell.Header, FinanceText.T("reallife_ven_app"));
                box.Title(FinanceText.T("reallife_ven_off_title"));
                box.Note(FinanceText.T("reallife_ven_off_body"));
                _picked = null;
            }
            else
            {
                int day = SaveGameManager.Current.Day;
                switch (_tab)
                {
                    case VenturesPhone.Property: table = WriteProperty(box, file, day); break;
                    default: table = WriteInvestors(box, file, day); break;
                }
            }
            box.Done();
            _table.Show(table, _rows, _picked);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Ventures: tab " + _tab + " written, " + _rows.Count + " row(s); " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
            }
        }

        private TableRow Row(string kind, int index)
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
            row.Reset(kind + index);
            row.Pick = true;
            row.Index = index;
            _rows.Add(row);
            return row;
        }

        private void Say(string words)
        {
            TableRow row = Row("say:", _rows.Count);
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

        private void RowClicked(TableRow row)
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
            _picked = row.Id;
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
