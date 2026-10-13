using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// What the Brands app shows, in the game's frame (GameUi.AppShell, copied from MyEmployees) as Finance, Ventures
    /// and the Company app are, with the kit's table (GameUi.DataTable, the game's own BaTable). An app of its own and
    /// not a page of Ventures or the Company app: it has four lists of its own, each with a page for the row that is
    /// picked, which is what one app's tabs are for, and the other two apps' tab bars are full. Four tabs over the
    /// table on the right, and on the left the white box with the one thing picked:
    ///
    ///   Brands        each brand and each district it is known in; pick one for its figures explained
    ///   Shops         every shop of the player's and every franchised shop, in one table: who runs it, its takings,
    ///                 its standards; pick one of the player's to make a brand of it, join one or take a city chain's
    ///                 franchise
    ///   Applicants    who wants to open a shop under a brand; pick one for their record, a building and the terms
    ///   Agreements    every franchise agreement, what its franchisee asks, and the player's own with a city chain
    ///
    /// A list goes in the table, which scrolls; the white box, which does not, shows only the one thing picked, three
    /// selectors at most and a Back button. Each tab's page is in its own file (BrandsApp.Brands.cs and its
    /// neighbours). Every table is declared once, in BrandsApp.Tables.cs, and is sorted, filtered and summed by the
    /// kit; what is picked stays picked through a sort or a filter (the kit keeps the row by its id).
    ///
    /// Everything on it is the game's. PER FRAME: NOTHING, open or closed. The rows and the page are made when the app
    /// opens, when a tab or a row is picked, and when the director says something has changed: a few dozen strings
    /// and the box's objects, on a click.
    /// </summary>
    public sealed partial class BrandsApp
    {
        private static readonly string[] TabKeys = { "reallife_brand_tab_brands", "reallife_brand_tab_shops", "reallife_brand_tab_applicants", "reallife_brand_tab_agreements" };

        public static IModLogger Logger;

        private readonly BrandsDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(48);
        private readonly List<TableRow> _pool = new List<TableRow>(48);

        private AppShell _shell;
        private DataTable _table;
        private int _tab, _wanted = -1;
        private string _pick, _wantPick;   // what is picked: a row's id ("brand:3", "dist:3:...", "shop:...", "app:7", "fr:9")
        private bool _ready;               // the director has read the player's businesses for this opening
        private bool _writing;             // a page is being written: a change it causes does not write it again

        public BrandsApp(BrandsDirector director)
        {
            _director = director;
        }

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("brands: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, BrandsDirector.AppId);
            _table = DataTable.On(_shell, "Brands");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(BrandsTables.Off))
            {
                return null;
            }
            _table.Money = amount => BrandsText.Money((float)amount);
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

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>The tab to show the next time the app opens, and what is picked on it (a notification was clicked, or something has just been made).</summary>
        public void Want(int tab, string pick)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            _wantPick = pick;
            if (Showing)
            {
                TakeWanted();
                Refresh();
            }
        }

        private void TakeWanted()
        {
            if (_wanted >= 0)
            {
                _tab = _wanted;
                _pick = _wantPick;
                _wanted = -1;
                _wantPick = null;
                ResetPages();
            }
        }

        public void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            TakeWanted();
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
                TakeWanted();
                Refresh();
            }
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab || _pick != null)
            {
                _tab = tab;
                _pick = null;
                ResetPages();
                Refresh();
            }
        }

        // A row was clicked, or the kit selected the first row left after a filter took the picked one out.
        private void RowClicked(TableRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            if (row.Id != _pick)
            {
                ResetPages();
            }
            _pick = row.Id;
            Refresh();
        }

        private void Back()
        {
            ClosePick();
        }

        // Nothing is picked any more: Back was pressed, or a filter left no row to pick.
        private void ClosePick()
        {
            _pick = null;
            ResetPages();
            _table.ClearSelection();
            Refresh();
        }

        // What a page inside the box was showing (its own selectors) starts again for another row.
        private void ResetPages()
        {
            _brandPage = 0;
            _pushKind = 0;
            _pushDistrict = 0;
            _joinBrand = 0;
            _chain = 0;
            _dealPage = 0;
            _site = 0;
            _fee = _royalty = _levy = 1;
            _length = 0;
            _exclusive = false;
            _franchisePage = -1;
        }

        // Everything the open tab shows, made again: asked for when the app opens, a tab or a row is picked, or something has changed.
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
                TableSpec table = BrandsTables.Off;
                BrandsState state = _ready ? _director.State : null;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, BrandsText.T("reallife_brand_app"));
                    box.Title(BrandsText.T("reallife_brand_off_title"));
                    box.Line(BrandsText.T("reallife_brand_off_body"));
                }
                else
                {
                    int day = SaveGameManager.Current.Day;
                    UiClone.Words(_shell.Header, BrandsText.Fill("reallife_brand_headline", "brands", state.Brands.Count.ToString(), "shops", (state.Members.Count + state.Franchises.Count).ToString(),
                        "waiting", _director.Waiting().ToString()));
                    switch (_tab)
                    {
                        case BrandsDirector.TabShops:
                            table = WriteShops(box, state, day, ref selected);
                            break;
                        case BrandsDirector.TabApplicants:
                            table = WriteApplicants(box, state, day, ref selected);
                            break;
                        case BrandsDirector.TabAgreements:
                            table = WriteAgreements(box, state, day, ref selected);
                            break;
                        default:
                            table = WriteBrands(box, state, day, ref selected);
                            break;
                    }
                    Meta.Guide.Point(box, "reallife_meta_title_economy_brands");   // (every page of the app ends with the Guide's page, after its buttons)
                }
                box.Done();
                _table.Show(table, _rows, selected);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Brands: tab " + _tab + " written, " + _rows.Count + " row(s)" + (_pick != null ? ", picked " + _pick : string.Empty) + "; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // ---------------------------------------------------------------- rows

        // A row that can be picked, from the pool. Its cells are set by whoever asked (row.Set: the words, and the figure a column is ordered and summed by).
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
            _rows.Add(row);
            return row;
        }

        // True if a row is the one picked now; it is then the table's selected row.
        private bool Is(TableRow row, ref string selected)
        {
            if (row.Id != _pick)
            {
                return false;
            }
            selected = row.Id;
            return true;
        }

        // A line of words where a table has nothing to list at all.
        private void Say(string id, string words)
        {
            TableRow row = Row(id);
            row.Info = true;
            row.Pick = false;
            row.A = words;
        }

        // The number of a name in a filter's own list of kinds (districts), added if it is new.
        private static int KindOf(List<string> names, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return -1;
            }
            int at = names.IndexOf(name);
            if (at < 0)
            {
                at = names.Count;
                names.Add(name);
            }
            return at;
        }

        // What follows a prefix in what is picked ("app:" and the applicant's number); null if something else is picked.
        private string Picked(string prefix)
        {
            return _pick != null && _pick.StartsWith(prefix, StringComparison.Ordinal) ? _pick.Substring(prefix.Length) : null;
        }

        private void BackButton(InfoBox box)
        {
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_brand_back", Back);
        }

        // A choice among a few: the game's previous/next selector, or the choice as a line where the game's selector could not be had.
        private void Choose(InfoBox box, int count, int index, Func<int, string> words, Action<int> changed)
        {
            if (count < 1)
            {
                return;
            }
            int at = BrandCatalogue.Clamp(index, 0, count - 1);
            if (!box.HasSelector || !box.Selector(count, at, words, i =>
                {
                    changed(i);
                    Refresh();
                }))
            {
                box.Line(words(at));
            }
        }

        // Anything that costs or cannot be undone is asked first, with the game's own question.
        private static void Confirm(string titleKey, string bodyKey, object data, Action yes, bool negative)
        {
            HudConfirm.Show(titleKey.Localize(), bodyKey.Localize(data), () => UiClone.Safe(yes), null, null, null, false, negative);
        }

        private static string Standards(int points)
        {
            return points.ToString();
        }
    }
}
