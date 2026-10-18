using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The Tech app in the game's frame (GameUi.AppShell), a table and the picked row's page on each tab. Nothing runs while it is closed.</summary>
    public sealed partial class TechApp
    {
        public const string FirmRow = "firm:", ProductRow = "prod:", ContractRow = "work:", StartupRow = "start:", CapRow = "cap:", CentreRow = "dc:",
            CloudRow = "cloud", FitRow = "fit:";
        private const string NewProductRow = "newprod:";
        private static readonly string[] TabKeys =
        {
            "reallife_tech_tab_firms", "reallife_tech_tab_products", "reallife_tech_tab_contracts", "reallife_tech_tab_startups", "reallife_tech_tab_funding",
            "reallife_tech_tab_infra"
        };

        public static IModLogger Logger;

        private readonly TechDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(32);
        private readonly List<TableRow> _pool = new List<TableRow>(32);

        private AppShell _shell;
        private DataTable _table;
        private int _tab, _wanted = -1, _page;
        private string _wantPick, _picked;
        private bool _ready, _writing;

        public TechApp(TechDirector director)
        {
            _director = director;
        }

        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("tech: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, TechDirector.TechAppId);
            _table = DataTable.On(_shell, "Tech");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(TechColumns.Off))
            {
                return null;
            }
            _table.Money = amount => TechText.Money((long)amount);
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

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

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
                _wanted = -1;
                _wantPick = null;
            }
            _ready = _director.TechOpened();
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
        }

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
                Refresh();
            }
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
                TableSpec table = TechColumns.Off;
                FirmsFile f = _ready ? _director.Firms : null;
                if (f == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, TechText.T("reallife_tech_app"));
                    box.Title(TechText.T("reallife_roles_off_title"));
                    box.Note(TechText.T("reallife_tech_off_body"));
                    _picked = null;
                }
                else
                {
                    int day = SaveGameManager.Current.Day;
                    UiClone.Words(_shell.Header, TechText.T("reallife_tech_head", "firms", _director.FirmPlaces.Count.ToString(), "products", f.Products.Count.ToString()));
                    switch (_tab)
                    {
                        case TechDirector.TabProducts: table = WriteProducts(box, f, day); break;
                        case TechDirector.TabContracts: table = WriteContracts(box, f, day); break;
                        case TechDirector.TabStartups: table = WriteStartups(box, day); break;
                        case TechDirector.TabFunding: table = WriteFunding(box, day); break;
                        case TechDirector.TabInfra: table = WriteInfra(box, day); break;
                        default: table = WriteFirms(box, f, day); break;
                    }
                }
                box.Done();
                _table.Show(table, _rows, _picked);
            }
            finally
            {
                _writing = false;
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
            row.Reset(id);
            row.Pick = true;
            row.Index = _rows.Count;
            _rows.Add(row);
            return row;
        }

        private void Say(string words)
        {
            TableRow row = Row("say:" + _rows.Count);
            row.Info = true;
            row.Pick = false;
            row.Set(0, words);
        }

        private static string Picked(string id, string kind)
        {
            return id != null && id.StartsWith(kind, System.StringComparison.Ordinal) ? id.Substring(kind.Length) : null;
        }

        private void RowClicked(TableRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            _picked = row.Id;
            _page = 0;
            Refresh();
        }

        private void ClosePick()
        {
            _picked = RivalsBack(_picked);   // a competitor's page goes back to its firm's competitors; anything else closes
            _page = 0;
            _table.ClearSelection();
            Refresh();
        }

        private void Do(System.Action act)
        {
            UiClone.Safe(act);
            Refresh();
        }

        private void Set(System.Action change)
        {
            change();
            Refresh();
        }

        private static void Cents(TableRow row, int column, long cents, CellTone tone = TableRow.Plain)
        {
            row.Set(column, TechText.Cents(cents), cents / 100.0, tone);
        }
    }
}
