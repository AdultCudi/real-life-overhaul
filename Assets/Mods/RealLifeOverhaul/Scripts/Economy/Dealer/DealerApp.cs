using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// What the Dealership app shows, in the game's frame (GameUi.AppShell, copied from MyEmployees) as the mod's other
    /// apps are: five tabs over the table on the right, and on the left the white box with the one thing picked.
    ///
    ///   Stock          every car on the dealerships' lots: what it cost, what it is asked at and worth, how long it has
    ///                  stood; pick one to set its price, prepare it or sell it to the trade (DealerApp.Stock.cs)
    ///   Market         the day's new cars from the wholesaler, used cars offered by their owners, and the lots of the
    ///                  trade auction; pick one to buy it, look it over, haggle or bid (DealerApp.Market.cs)
    ///   Offers         customers' offers waiting for an answer, and the cars sold lately (DealerApp.Offers.cs)
    ///   Finance        the cars sold on the dealership's own finance, and the warranties sold (DealerApp.Offers.cs)
    ///   Dealerships    each dealership's lot, staff and figures, and its settings (DealerApp.Lots.cs)
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and figures are made when it opens,
    /// when a tab is picked and when the player or the dealership changes something.
    /// </summary>
    public sealed partial class DealerApp
    {
        private static readonly string[] TabKeys = { "reallife_dealer_tab_stock", "reallife_dealer_tab_market", "reallife_dealer_tab_offers", "reallife_dealer_tab_finance", "reallife_dealer_tab_lots" };
        private const string GuideTitle = "reallife_meta_title_economy_dealer";   // the Guide's page every page of the app points to at its foot

        public static IModLogger Logger;

        private readonly DealerDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(32);
        private readonly List<TableRow> _pool = new List<TableRow>(32);

        private AppShell _shell;
        private DataTable _table;
        private int _tab, _wanted = -1;
        private string _wantPick;
        private bool _ready, _writing;
        private string _picked;
        private int _choice = -1;           // the step a selector of the picked thing stands at; -1 for its own default
        private string _choiceFor;          // ...and what it is for
        private string _yardChoice;         // the dealership a car bought goes to

        public DealerApp(DealerDirector director)
        {
            _director = director;
        }

        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("dealership: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, DealerDirector.AppId);
            _table = DataTable.On(_shell, "Dealership");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(DealerColumns.Off))
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
                DealerState state = _ready ? _director.State : null;
                TableSpec table = DealerColumns.Off;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, DealerText.T("reallife_dealer_app"));
                    box.Title(DealerText.T("reallife_dealer_off_title"));
                    box.Line(DealerText.T("reallife_dealer_off_body"));
                    _picked = null;
                }
                else
                {
                    UiClone.Words(_shell.Header, DealerText.T("reallife_dealer_head", "lots", state.Yards.Count.ToString(), "cars", _director.CarCount().ToString()));
                    int now = SaveGameManager.Current.Day * 24 + SaveGameManager.Current.Hour;
                    switch (_tab)
                    {
                        case DealerDirector.TabMarket: table = WriteMarket(box, state, now); break;
                        case DealerDirector.TabOffers: table = WriteOffers(box, state, now); break;
                        case DealerDirector.TabFinance: table = WriteFinance(box, state); break;
                        case DealerDirector.TabLots: table = WriteLots(box, state); break;
                        default: table = WriteStock(box, state, now); break;
                    }
                }
                box.Done();
                _table.Show(table, _rows, _picked);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Dealership: tab " + _tab + " written, " + _rows.Count + " row(s); " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
                }
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

        private static void Money(TableRow row, int column, int amount, CellTone tone = TableRow.Plain)
        {
            row.Set(column, DealerText.Money(amount), amount, tone);
        }

        private static void Count(TableRow row, int column, int count, CellTone tone = TableRow.Plain)
        {
            row.Set(column, count.ToString(), count, tone);
        }

        private static int Picked(string id, string kind)
        {
            return id != null && id.StartsWith(kind, StringComparison.Ordinal) && int.TryParse(id.Substring(kind.Length), out int n) ? n : -1;
        }

        private static string PickedText(string id, string kind)
        {
            return id != null && id.StartsWith(kind, StringComparison.Ordinal) ? id.Substring(kind.Length) : null;
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
            int at = DealerCatalogue.Clamp(index, 0, count - 1);
            if (!box.HasSelector || !box.Selector(count, at, words, i =>
                {
                    changed(i);
                    Refresh();
                }))
            {
                box.Line(words(at));
            }
        }

        // The step the picked thing's selector stands at, or its default.
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

        // The dealership a car bought goes to: the one chosen, if it has room; a selector among those with room if there are several.
        private string ChooseYard(InfoBox box, DealerState state)
        {
            var room = new List<DealerYard>(state.Yards.Count);
            for (int i = 0; i < state.Yards.Count; i++)
            {
                if (state.Yards[i].Laid && state.Yards[i].FreeSpot() >= 0)
                {
                    room.Add(state.Yards[i]);
                }
            }
            if (room.Count == 0)
            {
                return null;
            }
            int at = 0;
            for (int i = 0; i < room.Count; i++)
            {
                at = room[i].Place == _yardChoice ? i : at;
            }
            _yardChoice = room[at].Place;
            if (room.Count > 1)
            {
                Choose(box, room.Count, at, i => DealerText.T("reallife_dealer_goes_to", "name", room[i].Name, "free", room[i].FreeSpots().ToString()), i => _yardChoice = room[i].Place);
            }
            else
            {
                box.Line(DealerText.T("reallife_dealer_goes_to", "name", room[0].Name, "free", room[0].FreeSpots().ToString()));
            }
            return _yardChoice;
        }
    }
}
