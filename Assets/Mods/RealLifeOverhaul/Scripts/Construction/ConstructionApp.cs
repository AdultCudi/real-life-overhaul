using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The Construction app in the game's own frame (GameUi.AppShell, copied from MyEmployees): eight tabs (the overview,
    /// land, projects, tenders, crews, the company, the rival builders, the city), a list in the table and the one thing picked in the
    /// white box. Nothing runs while it is closed; a page is written on opening, a pick, a press or a change.
    /// </summary>
    public sealed partial class ConstructionApp
    {
        private static readonly string[] TabKeys =
        {
            "reallife_bld_tab_overview", "reallife_bld_tab_land", "reallife_bld_tab_projects", "reallife_bld_tab_tenders", "reallife_bld_tab_crews", "reallife_bld_tab_company",
            "reallife_bld_tab_rivals", "reallife_bld_tab_city"
        };

        public static IModLogger Logger;

        private readonly ConstructionDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(64);
        private readonly List<TableRow> _pool = new List<TableRow>(64);

        private AppShell _shell;
        private DataTable _table;
        private TableSpec _spec = ConstructionTables.Off;
        private int _tab, _wanted = -1, _wantPick, _wantPage;
        private string _pick;      // a row's id: "lot:", "own:", "prj:", "tnd:", "crew:", "offer:", "plant:", "buy:", "staff:", "co:", "rival:"
        private int _page;         // a project's second page (its money) when 1, its materials and hedge when 2
        private bool _writing;

        public ConstructionApp(ConstructionDirector director)
        {
            _director = director;
        }

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("construction: the app being built from the game's MyEmployees");   // once per city
            _shell = AppShell.Make(menu, dormant, ConstructionDirector.AppId);
            _table = DataTable.On(_shell, "Construction");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(ConstructionTables.Off))
            {
                return null;
            }
            _table.Money = amount => ConstructionText.Money((long)amount);
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

        /// <summary>The tab to show the next time the app opens, and the project, tender or crew picked on it (0 for none).</summary>
        public void Want(int tab, int pick)
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
                _pick = _wantPick <= 0 ? null : _tab == ConstructionDirector.TabTenders ? "tnd:" + _wantPick : _tab == ConstructionDirector.TabProjects ? "prj:" + _wantPick
                    : _tab == ConstructionDirector.TabCity ? "bill:" + _wantPick : _tab == ConstructionDirector.TabRivals ? "rival:" + (_wantPick - 1)
                    : _tab == ConstructionDirector.TabLand ? "ofr:" + _wantPick : null;
                _page = _pick != null ? _wantPage : 0;
                _wanted = -1;
                _wantPage = 0;
            }
            Refresh();
        }

        public void Closed()
        {
            _page = 0;
        }

        public void Changed()
        {
            if (Showing && !_writing)
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
                _page = 0;
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
            _pick = row.Id;
            _page = 0;
            Refresh();
        }

        private void Back()
        {
            _pick = null;
            _page = 0;
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
                InfoBox box = _shell.Box;
                box.Clear();
                string selected = null;
                BuildState state = _director.Running ? _director.State : null;
                _spec = state != null ? ConstructionTables.For(_tab) : ConstructionTables.Off;
                _shell.SetColumns(_spec);
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, ConstructionText.T("reallife_bld_app"));
                    box.Title(ConstructionText.T("reallife_bld_off_title"));
                    box.Note(ConstructionText.T("reallife_bld_off_body"));
                    Say("off", ConstructionText.T("reallife_bld_off_row"));
                }
                else
                {
                    UiClone.Words(_shell.Header, Headline(state));
                    switch (_tab)
                    {
                        case ConstructionDirector.TabLand: WriteLand(box, state, ref selected); break;
                        case ConstructionDirector.TabProjects: WriteProjects(box, state, ref selected); break;
                        case ConstructionDirector.TabTenders: WriteTenders(box, state, ref selected); break;
                        case ConstructionDirector.TabCrews: WriteCrews(box, state, ref selected); break;
                        case ConstructionDirector.TabCompany: WriteCompany(box, state, ref selected); break;
                        case ConstructionDirector.TabRivals: WriteRivals(box, state, ref selected); break;
                        case ConstructionDirector.TabCity: WriteCity(box, state, ref selected); break;
                        default: WriteOverview(box, state, ref selected); break;
                    }
                }
                box.Done();
                _table.Show(_spec, _rows, selected);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Construction: tab " + _tab + " written, " + _rows.Count + " row(s)" + (_pick != null ? ", picked " + _pick : string.Empty) + "; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms; " + box.Fit() + ".");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        private string Headline(BuildState state)
        {
            string name = _director.Office(out PlaceView office) && state.Firm.Founded > 0 ? office.Name : ConstructionText.T("reallife_bld_app");
            return ConstructionText.T("reallife_bld_headline", "name", name, "reputation", state.Firm.Reputation.ToString(), "sites", state.Building().ToString());
        }

        partial void WriteOverview(InfoBox box, BuildState state, ref string selected);
        partial void WriteLand(InfoBox box, BuildState state, ref string selected);
        partial void WriteProjects(InfoBox box, BuildState state, ref string selected);
        partial void WriteTenders(InfoBox box, BuildState state, ref string selected);
        partial void WriteCrews(InfoBox box, BuildState state, ref string selected);
        partial void WriteCompany(InfoBox box, BuildState state, ref string selected);
        partial void WriteRivals(InfoBox box, BuildState state, ref string selected);
        partial void WriteCity(InfoBox box, BuildState state, ref string selected);

        // ------------------------------------------------------------------ rows

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

        // A row that can be picked; its id becomes 'selected' when it is the one picked now.
        private TableRow Pick(string id, ref string selected)
        {
            TableRow row = Row(id);
            row.Pick = true;
            if (id == _pick)
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

        // What follows a prefix in the pick ("prj:" and the project's number); null when something else is picked.
        private string Picked(string prefix)
        {
            return _pick != null && _pick.StartsWith(prefix, StringComparison.Ordinal) ? _pick.Substring(prefix.Length) : null;
        }

        private int PickedNumber(string prefix)
        {
            string rest = Picked(prefix);
            return rest != null && int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
        }

        // ------------------------------------------------------------------ answers

        // A press's answer: a short notice of the game's, and the page written again with what changed.
        private void Answer(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                Notifications.Show(NotificationType.Info, key, _director.AnswerData(key), 5f, null, null, true, false);   // a text with the land's size or the money in it carries them
            }
            Refresh();
        }

        // A question of the game's before something that costs or cannot be undone; the answer after.
        private void Confirm(string headerKey, string bodyKey, string name, long amount, Func<string> act, bool negative)
        {
            LanguageChangeEventDataHolder header = headerKey.Localize();
            LanguageChangeEventDataHolder body = bodyKey.Localize(new Dictionary<string, string> { { "name", name ?? string.Empty }, { "amount", ConstructionText.Money(amount) } });
            GameConfirm.Show(header, body, () => UiClone.Safe(() => Answer(act())), null, null, null, true, negative);
        }

        // Show on map under a page's title, a row of its own: the place made the game's own destination (the menu closes so its
        // marker shows), and an answer in a notice only when it could not be.
        private void MapRow(InfoBox box, Func<string> show)
        {
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_bld_button_map", () =>
            {
                string why = show();
                if (why != null)
                {
                    Answer(why);
                }
            });
        }

        private void BackButton(InfoBox box)
        {
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_bld_back", Back);
            Guide(box);
        }

        private static void Guide(InfoBox box)
        {
            Meta.Guide.Point(box, "reallife_meta_title_construction");
        }
    }
}
