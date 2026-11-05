using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Reads the Construction app's pages from the builder's business and carries every button to the director that already owns it, as the old app did: the same methods, the
    /// same refusals as notices, the same questions in the game's own box (GameConfirm), the same money. The old pages' writers are kept whole, writing into a KitBox, and a page
    /// is composed from what they wrote: a tab's table with its card, or the page of the one thing picked. Written when the director's revision moves or the player pressed
    /// something; nothing a frame.
    /// </summary>
    public sealed partial class ConstructionLive : IConstructionSource
    {
        public static readonly ConstructionLive Instance = new ConstructionLive();

        public const string ScreenId = ConstructionScreen.ScreenId;

        private static readonly string[] TabKeys =
        {
            "reallife_bld_tab_overview", "reallife_bld_tab_land", "reallife_bld_tab_projects", "reallife_bld_tab_tenders", "reallife_bld_tab_crews", "reallife_bld_tab_company",
            "reallife_bld_tab_rivals", "reallife_bld_tab_city"
        };

        private static readonly UiIcon[] TabIcons =
        {
            UiIcon.Home, UiIcon.MapTrifold, UiIcon.Hammer, UiIcon.ClipboardText, UiIcon.People, UiIcon.Briefcase, UiIcon.Flag, UiIcon.Scales
        };

        private readonly List<TableRow> _rows = new List<TableRow>(64);
        private readonly List<TableRow> _pool = new List<TableRow>(64);
        private ConstructionRuns _runs = new ConstructionRuns();
        private KitBox _box;
        private ConstructionView _view;
        private TableSpec _spec = ConstructionTables.Off;
        private int _tab, _wanted = -1, _wantPick, _wantPage, _local;
        private string _pick;      // a row's id: "lot:", "own:", "prj:", "tnd:", "crew:", "offer:", "plant:", "buy:", "staff:", "co:", "rival:"
        private int _page;         // a project's second page (its money) when 1, its materials and hedge when 2
        private bool _opened, _previewing;
        private string _preview, _previewKey;   // the row a table's tab shows beside it, and the one that was written for it
        private UiDataTableModel _detailTable;   // a page that compares things (the builders' quotes) has a table over its cards
        private string[] _detailKeys = new string[0];

        private ConstructionLive()
        {
        }

        private ConstructionDirector Director => ConstructionDirector.Instance;

        public int Generation => _local;

        /// <summary>Something the page shows changed: it is written again.</summary>
        public void Changed()
        {
            _local++;
        }

        /// <summary>The tab to show the next time the screen opens, and the project, tender or crew picked on it (0 for none).</summary>
        public void Want(int tab, int pick)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            _wantPick = pick;
            _local++;
        }

        /// <summary>The project's second page (its money, its hedge, its builders' quotes) is the one to show the next time the screen opens.</summary>
        public void WantPage(int project, int page)
        {
            _wantPage = page;
            Want(ConstructionDirector.TabProjects, project);
        }

        public void Opened()
        {
            _opened = true;
            TakeWant();
        }

        // A notice's tab, row and page are put into the screen once, as the old app did when it opened.
        private void TakeWant()
        {
            if (_wanted < 0)
            {
                return;
            }
            _tab = _wanted;
            _pick = _wantPick <= 0 ? null : _tab == ConstructionDirector.TabTenders ? "tnd:" + _wantPick : _tab == ConstructionDirector.TabProjects ? "prj:" + _wantPick
                : _tab == ConstructionDirector.TabCity ? "bill:" + _wantPick : _tab == ConstructionDirector.TabRivals ? "rival:" + (_wantPick - 1)
                : _tab == ConstructionDirector.TabLand ? (_wantPick >= ConstructionDirector.OfferPickBase ? "cof:" + (_wantPick - ConstructionDirector.OfferPickBase)
                    : _wantPick >= ConstructionDirector.AuctionPickBase ? "auc:" + (_wantPick - ConstructionDirector.AuctionPickBase) : "ofr:" + _wantPick) : null;
            _page = _pick != null ? _wantPage : 0;
            _wanted = -1;
            _wantPage = 0;
        }

        public void Show(int tab)
        {
            ShowTab(tab);
        }

        public bool Pick(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            if (key.StartsWith("q:", StringComparison.Ordinal) && int.TryParse(key.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int quote))
            {
                _quotePick = quote;   // a builder's quote on the project's quotes page: the page stays and shows that builder's
                _local++;
                return true;
            }
            if (_pick == null && _tab != ConstructionDirector.TabOverview && key != _previewKey && HasRow(key))
            {
                _preview = key;   // a first press on a row shows it beside the table; pressing the one shown opens its page
                _local++;
                return true;
            }
            _pick = key;
            _page = 0;
            _local++;
            return true;
        }

        /// <summary>Steps out of a page: a project's second page to the project, the project to its tab. False on a tab's own page.</summary>
        public bool Back()
        {
            if (_pick == null && _page == 0)
            {
                return false;
            }
            if (_page != 0 && _pick != null)
            {
                _page = 0;
            }
            else
            {
                _pick = null;
                _page = 0;
            }
            _local++;
            return true;
        }

        private void ShowTab(int tab)
        {
            tab = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            if (tab != _tab || _pick != null || _page != 0)
            {
                _preview = null;
                _tab = tab;
                _pick = null;
                _page = 0;
                _local++;
            }
        }

        /// <summary>The page opens another section with a row of it picked.</summary>
        private void Jump(int tab, string pick)
        {
            _tab = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            _pick = pick;
            _page = 0;
            _local++;
        }

        private void Refresh()
        {
            _local++;
        }

        // ------------------------------------------------------------------ the page

        public void Fill(ConstructionModel m, ConstructionView v)
        {
            _view = v;
            TakeWant();
            _runs = new ConstructionRuns();
            _detailTable = null;
            _detailKeys = new string[0];
            _box = new KitBox(_runs, ConstructionText.T);
            m.Title = ConstructionText.T("reallife_bld_app");
            m.Icon = UiIcon.Building;
            BuildState state = Director != null && Director.Running ? Director.State : null;
            if (state == null || SaveGameManager.Current == null)
            {
                m.Shape = ConstructionShape.Off;
                m.OffTitle = ConstructionText.T("reallife_bld_off_title");
                m.OffBody = ConstructionText.T("reallife_bld_off_body");
                m.OffAction = ConstructionText.T("reallife_ui_cons_close");
                m.Runs = _runs.ToArray();
                return;
            }
            _tab = Mathf.Clamp(_tab, 0, TabKeys.Length - 1);
            m.Tab = _tab;
            m.Nav = Nav(state);
            string selected = WriteTab(state);
            _previewing = false;
            if (selected == null && _tab != ConstructionDirector.TabOverview)
            {
                // A table's tab shows the thing it points at beside it: the one pressed last, or the first row that can be opened.
                if (_preview != null && !HasRow(_preview))
                {
                    _preview = null;
                }
                string show = _preview ?? FirstPick();
                if (show != null)
                {
                    string kept = _pick;
                    _pick = show;
                    _runs = new ConstructionRuns();
                    _box = new KitBox(_runs, ConstructionText.T);
                    selected = WriteTab(state);
                    _pick = kept;
                    _previewing = selected != null && _box.HasContent;
                    _previewKey = selected;
                }
            }
            Compose(m, state, _previewing ? null : selected);
            m.Runs = _runs.ToArray();
        }

        private string WriteTab(BuildState state)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _pool.Add(_rows[i]);
            }
            _rows.Clear();
            string selected = null;
            _spec = ConstructionTables.For(_tab);
            _box.Clear();
            switch (_tab)
            {
                case ConstructionDirector.TabLand: WriteLand(_box, state, ref selected); break;
                case ConstructionDirector.TabProjects: WriteProjects(_box, state, ref selected); break;
                case ConstructionDirector.TabTenders: WriteTenders(_box, state, ref selected); break;
                case ConstructionDirector.TabCrews: WriteCrews(_box, state, ref selected); break;
                case ConstructionDirector.TabCompany: WriteCompany(_box, state, ref selected); break;
                case ConstructionDirector.TabRivals: WriteRivals(_box, state, ref selected); break;
                case ConstructionDirector.TabCity: WriteCity(_box, state, ref selected); break;
                default: WriteOverview(_box, state, ref selected); break;
            }
            return selected;
        }

        private string FirstPick()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Pick && !_rows[i].Info)
                {
                    return _rows[i].Id;
                }
            }
            return null;
        }

        private bool HasRow(string id)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Id == id && _rows[i].Pick)
                {
                    return true;
                }
            }
            return false;
        }

        private UiNavEntry[] Nav(BuildState state)
        {
            int building = state.Building(), tenders = 0, crews = state.Crews.Count;
            for (int i = 0; i < state.Tenders.Count; i++)
            {
                tenders += state.Tenders[i].State == TenderState.Open ? 1 : 0;
            }
            string[] counts =
            {
                string.Empty, string.Empty, building > 0 ? building.ToString(CultureInfo.InvariantCulture) : string.Empty, tenders > 0
                    ? tenders.ToString(CultureInfo.InvariantCulture) : string.Empty,
                crews > 0 ? crews.ToString(CultureInfo.InvariantCulture) : string.Empty, string.Empty, string.Empty, string.Empty
            };
            string[] groups = { ConstructionText.T("reallife_ui_cons_group_office"), null, null, null, ConstructionText.T("reallife_ui_cons_group_builder"), null, null, null };
            var nav = new UiNavEntry[TabKeys.Length];
            for (int i = 0; i < nav.Length; i++)
            {
                nav[i] = new UiNavEntry { Icon = TabIcons[i], Tint = UiRole.None, Label = ConstructionText.T(TabKeys[i]), Count = counts[i], Group = groups[i] };
            }
            return nav;
        }

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
                // a text with the land's size or the money in it carries them
                Notifications.Show(NotificationType.Info, key, Director.AnswerData(key), 5f, null, null, true, false);
            }
            Refresh();
        }

        // A question of the game's before something that costs or cannot be undone; the answer after.
        private void Confirm(string headerKey, string bodyKey, string name, long amount, Func<string> act, bool negative)
        {
            LanguageChangeEventDataHolder header = headerKey.Localize();
            LanguageChangeEventDataHolder body = bodyKey.Localize(new Dictionary<string, string> { { "name", name ?? string.Empty }, { "amount",
                ConstructionText.Money(amount) } });
            GameConfirm.Show(header, body, () => UiClone.Safe(() => Answer(act())), null, null, null, true, negative);
        }

        // Show on map under a page's title: the place made the game's own destination (the menu closes so its marker shows), and an answer in a notice only when it could not be.
        private void MapRow(KitBox box, Func<string> show)
        {
            KitBox.Button(box.ButtonRow(), KitBox.Additional, "reallife_bld_button_map", () =>
            {
                string why = show();
                if (why != null)
                {
                    Answer(why);
                }
            });
        }

        private void BackButton(KitBox box)
        {
            KitBox.Button(box.ButtonRow(), KitBox.Additional, "reallife_bld_back", () => Back());
        }

        // The guide's link lives in the old app's frame; the kit's own help stands in the app's title strip.
        private static void Guide(KitBox box)
        {
        }

        partial void WriteOverview(KitBox box, BuildState state, ref string selected);
        partial void WriteLand(KitBox box, BuildState state, ref string selected);
        partial void WriteProjects(KitBox box, BuildState state, ref string selected);
        partial void WriteTenders(KitBox box, BuildState state, ref string selected);
        partial void WriteCrews(KitBox box, BuildState state, ref string selected);
        partial void WriteCompany(KitBox box, BuildState state, ref string selected);
        partial void WriteRivals(KitBox box, BuildState state, ref string selected);
        partial void WriteCity(KitBox box, BuildState state, ref string selected);
    }
}
