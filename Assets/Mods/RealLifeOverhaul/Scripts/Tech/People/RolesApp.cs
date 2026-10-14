using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The Open Roles app, in the game's frame (GameUi.AppShell, copied from MyEmployees) as the Web shop and Ventures
    /// apps are. Its tabs over the table on the right, and on the left the white box with the one thing picked:
    ///
    ///   Team         every professional of the player's with their role, level, skills, morale and burnout; pick one for
    ///                their file, their career (role, promotion, a rise, a course, an exam) and their contract (where they
    ///                work, a restraint clause, notice and severance) (RolesApp.Team.cs)
    ///   Jobs         the player's ads on the jobs board, and the page that posts one (RolesApp.Hiring.cs)
    ///   Applicants   everyone who has applied, with what is known of them; pick one to check them, interview them and
    ///                make an offer
    ///   Market       the city's labour market by family: seekers by level, the rivals' openings, the shortage, the
    ///                going rate; pick a family for its rivals and districts
    ///   Search       the search firms briefed for senior roles, and the page that briefs one
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and figures are made when it
    /// opens, when a tab is picked and when the player changes something in it.
    /// </summary>
    public sealed partial class RolesApp
    {
        private static readonly string[] TabKeys = { "reallife_roles_tab_team", "reallife_roles_tab_jobs", "reallife_roles_tab_applicants", "reallife_roles_tab_market", "reallife_roles_tab_search" };
        // The Guide's pages the tabs point to at their foot: professionals (the Team tab) and the jobs board (the rest).
        private const string PeopleGuide = "reallife_meta_title_tech_people", JobsGuide = "reallife_meta_title_tech_jobs";

        public static IModLogger Logger;

        private readonly TechDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(64);
        private readonly List<TableRow> _pool = new List<TableRow>(64);

        private AppShell _shell;
        private DataTable _table;
        private int _tab, _wanted = -1, _page;
        private string _wantPick, _picked;
        private bool _ready, _writing;

        public RolesApp(TechDirector director)
        {
            _director = director;
        }

        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("open roles: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, TechDirector.RolesAppId);
            _table = DataTable.On(_shell, "Open Roles");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(RolesColumns.Off))
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
                _page = 0;
                _wanted = -1;
                _wantPick = null;
            }
            _ready = _director.RolesOpened();
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
                _page = 0;
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
                TableSpec table = RolesColumns.Off;
                if (!_ready || SaveGameManager.Current == null || _director.Talent == null)
                {
                    UiClone.Words(_shell.Header, TechText.T("reallife_roles_app"));
                    box.Title(TechText.T("reallife_roles_off_title"));
                    box.Line(TechText.T("reallife_roles_off_body"));
                    _picked = null;
                }
                else
                {
                    UiClone.Words(_shell.Header, TechText.T("reallife_roles_head", "count", _director.People.Count.ToString()));
                    int day = SaveGameManager.Current.Day;
                    if (_tab != TechDirector.TabTeam && _director.Hiring == null)
                    {
                        box.Title(TechText.T("reallife_roles_off_title"));
                        box.Line(TechText.T("reallife_hire_off_body"));
                        _picked = null;
                    }
                    else
                    {
                        switch (_tab)
                        {
                            case TechDirector.TabJobs: table = WriteJobs(box, day); break;
                            case TechDirector.TabApplicants: table = WriteApplicants(box, day); break;
                            case TechDirector.TabMarket: table = WriteMarket(box, day); break;
                            case TechDirector.TabSearch: table = WriteSearch(box, day); break;
                            default: table = WriteTeam(box, day); break;
                        }
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
            _picked = null;
            _page = 0;
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
