using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>
    /// The Marketplace app in the game's frame (GameUi.AppShell, copied from MyEmployees), a part file a tab: the day's
    /// listings, the player's things, buyers' offers and freelance visits. Nothing runs while it is closed: the rows are made
    /// when it opens, when a tab is picked and when something changes.
    /// </summary>
    public sealed partial class HustleApp
    {
        private static readonly string[] TabKeys = { "reallife_hustle_tab_listings", "reallife_hustle_tab_yours", "reallife_hustle_tab_offers", "reallife_hustle_tab_freelance" };
        private const string GuideTitle = "reallife_meta_title_hustles";   // the Guide's page every page of the app points to at its foot

        public static IModLogger Logger;

        private readonly HustleDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(32);
        private readonly List<TableRow> _pool = new List<TableRow>(32);

        private AppShell _shell;
        private DataTable _table;
        private int _tab, _wanted = -1;
        private string _wantPick;
        private bool _ready, _writing;
        private string _picked;
        private int _choice = -1;           // the step a selector of the picked thing stands at; -1 for its own default
        private string _choiceFor;
        private bool _deliver;              // goods bought are to be brought home

        public HustleApp(HustleDirector director)
        {
            _director = director;
        }

        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("hustles: the Marketplace app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, HustleDirector.AppId);
            _table = DataTable.On(_shell, "Marketplace");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(HustleColumns.Off))
            {
                return null;
            }
            _table.Money = amount => HustleText.Money((int)Math.Round(amount));
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
                Pick(_wantPick);
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
                Pick(null);
                Refresh();
            }
        }

        private void Pick(string id)
        {
            _picked = id;
            _choice = -1;
            _choiceFor = id;
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
                HustleState state = _ready ? _director.State : null;
                TableSpec table = HustleColumns.Off;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, HustleText.T("reallife_hustle_app"));
                    box.Title(HustleText.T("reallife_hustle_off_title"));
                    box.Note(HustleText.T("reallife_hustle_off_body"));
                    _picked = null;
                }
                else
                {
                    UiClone.Words(_shell.Header, Header(state));
                    switch (_tab)
                    {
                        case HustleDirector.TabYours: table = WriteYours(box, state); break;
                        case HustleDirector.TabOffers: table = WriteOffers(box, state); break;
                        case HustleDirector.TabFreelance: table = WriteFreelance(box, state); break;
                        default: table = WriteListings(box, state); break;
                    }
                }
                box.Done();
                _table.Show(table, _rows, _picked);
                if (Meta.Dev.Verbose)
                {
                    float ms = (Time.realtimeSinceStartup - started) * 1000f;
                    Logger?.Info("Hustles: tab " + _tab + " written, " + _rows.Count + " row(s); " + ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms.");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        private static string Header(HustleState state)
        {
            int listed = 0;
            for (int i = 0; i < state.Items.Count; i++)
            {
                listed += state.Items[i].State == OwnedState.Listed ? 1 : 0;
            }
            return state.Ratings.Count > 0
                ? HustleText.T("reallife_hustle_head_rated", "listings", state.Listings.Count.ToString(CultureInfo.InvariantCulture), "things",
                    state.Items.Count.ToString(CultureInfo.InvariantCulture), "listed", listed.ToString(CultureInfo.InvariantCulture), "stars", HustleText.Stars(state.Rating))
                : HustleText.T("reallife_hustle_head", "listings", state.Listings.Count.ToString(CultureInfo.InvariantCulture), "things",
                    state.Items.Count.ToString(CultureInfo.InvariantCulture), "listed", listed.ToString(CultureInfo.InvariantCulture));
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

        private static void Money(TableRow row, int column, int amount, CellTone tone = TableRow.Plain)
        {
            row.Set(column, HustleText.Money(amount), amount, tone);
        }

        private static int Picked(string id, string kind)
        {
            return id != null && id.StartsWith(kind, StringComparison.Ordinal) && int.TryParse(id.Substring(kind.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : -1;
        }

        private void RowClicked(TableRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            Pick(row.Id);
            Refresh();
        }

        private void ClosePick()
        {
            Pick(null);
            _table.ClearSelection();
            Refresh();
        }

        // A selector of the game's in the box, or its words as a line where the game's selector could not be had.
        private void Choose(InfoBox box, int count, int index, Func<int, string> words, Action<int> changed)
        {
            if (count < 1)
            {
                return;
            }
            int at = HustleCatalogue.Clamp(index, 0, count - 1);
            if (!box.HasSelector || !box.Selector(count, at, words, i =>
                {
                    changed(i);
                    Refresh();
                }))
            {
                box.Line(words(at));
            }
        }

        private int Choice(int fallback)
        {
            return _choiceFor == _picked && _choice >= 0 ? _choice : fallback;
        }

        private void SetChoice(int step)
        {
            _choiceFor = _picked;
            _choice = step;
        }

        // Anything that costs or cannot be undone is asked first, with the game's own question.
        private static void Confirm(string titleKey, string body, Action yes, bool negative)
        {
            GameConfirm.Show(titleKey.GetLocalization(), body, () => UiClone.Safe(yes), null, null, null, false, negative);
        }

        private static string Where(string street, int number)
        {
            return HustleText.Place(street, number);
        }
    }
}
