using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Services app in the game's frame (GameUi.AppShell): four tabs over a table, and a box with the one thing picked.
    /// Nothing runs while it is closed: a page is written when it opens, on a pick, or when the director says so.
    /// </summary>
    public sealed partial class ServicesApp
    {
        private static readonly string[] TabKeys = { "reallife_svc_tab_cleaning", "reallife_svc_tab_crew", "reallife_svc_tab_contracts", "reallife_svc_tab_sale" };

        public static IModLogger Logger;

        private readonly ServicesDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(48);
        private readonly List<TableRow> _pool = new List<TableRow>(48);

        private AppShell _shell;
        private DataTable _table;
        private TableSpec _spec = ServicesTables.Off;   // the table of the page being written
        private int _tab, _wanted = -1;
        private string _pick, _wantPick;   // what is picked: a row's id ("cli:...", "add:...", "emp:...", "offer:...", "con:...", "sell:...", "bid:...")
        private bool _ready;               // the director has read the game for this opening
        private bool _writing;             // a page is being written: a change it causes does not write it again

        public ServicesApp(ServicesDirector director)
        {
            _director = director;
        }

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("services: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, ServicesDirector.AppId);
            _table = DataTable.On(_shell, "Services");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(ServicesTables.Off))
            {
                return null;
            }
            _table.Money = amount => ServicesText.Money((float)amount);
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

        /// <summary>The tab to show the next time the app opens, and what is picked on it (a notification, or another app's button).</summary>
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
                _pick = _wantPick;
                _wanted = -1;
                _wantPick = null;
                _step = -1;
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
            if (tab != _tab || _pick != null)
            {
                _tab = tab;
                _pick = null;
                _step = -1;
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
            if (row.Id == _pick)
            {
                return;   // the kit selects the same row again after a sort or a filter
            }
            _pick = row.Id;
            _step = -1;
            Refresh();
        }

        private void Back()
        {
            if (_pick == null)
            {
                return;
            }
            _pick = null;
            _step = -1;
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
                float started = Time.realtimeSinceStartup;
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                _spec = ServicesTables.Off;
                InfoBox box = _shell.Box;
                box.Clear();
                string selected = null;
                ServicesState state = _ready ? _director.State : null;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, ServicesText.T("reallife_svc_app"));
                    box.Title(ServicesText.T("reallife_co_off_title"));
                    box.Note(ServicesText.T("reallife_svc_off_body"));
                }
                else
                {
                    int day = SaveGameManager.Current.Day;
                    UiClone.Words(_shell.Header,
                        ServicesText.T("reallife_svc_headline", "crew", state.Crew.Count.ToString(), "businesses", state.Clients.Count.ToString(), "waiting", _director.Waiting().ToString()));
                    if (_tab == ServicesDirector.TabSale)
                    {
                        if (PartPage(_director.SaleOn, box))
                        {
                            WriteSale(box, state, day, ref selected);
                        }
                    }
                    else if (PartPage(_director.CleaningOn, box))
                    {
                        switch (_tab)
                        {
                            case ServicesDirector.TabCrew: WriteCrew(box, state, ref selected); break;
                            case ServicesDirector.TabContracts: WriteContracts(box, state, day, ref selected); break;
                            default: WriteCleaning(box, state, day, ref selected); break;
                        }
                    }
                }
                box.Done();
                _table.Show(_spec, _rows, selected);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Services: tab " + _tab + " written, " + _rows.Count + " row(s)" + (_pick != null ? ", picked " + _pick : string.Empty) + "; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms.");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // A tab whose part is switched off, or stopped by a fault, says so and shows nothing else.
        private bool PartPage(bool on, InfoBox box)
        {
            if (on)
            {
                return true;
            }
            _spec = ServicesTables.Off;
            box.Title(ServicesText.T(TabKeys[_tab]));
            box.Line(ServicesText.T("reallife_co_part_off"), CellTone.Yellow);
            Say("off", ServicesText.T("reallife_co_part_off_row"));
            return false;
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

        // A row that can be picked, of a kind for the table's filter; 'picked' says whether it is the one picked now.
        private TableRow Pick(string id, int kind, ref string selected, out bool picked)
        {
            TableRow row = Row(id);
            row.Pick = true;
            row.Kind = kind;
            picked = id == _pick;
            if (picked)
            {
                selected = id;
            }
            return row;
        }

        private void Say(string id, string words)
        {
            TableRow row = Row(id);
            row.Info = true;
            row.A = words;
        }

        // The foot of a picked thing's page: Back, then the Guide's page.
        private void BackButton(InfoBox box)
        {
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", Back);
            Guide(box);
        }

        // The Guide's page for the tab that shows: selling a business, or the cleaning service for the rest.
        private void Guide(InfoBox box)
        {
            Meta.Guide.Point(box, _tab == ServicesDirector.TabSale ? "reallife_meta_title_economy_sale" : "reallife_meta_title_economy_cleaning");
        }
    }
}
