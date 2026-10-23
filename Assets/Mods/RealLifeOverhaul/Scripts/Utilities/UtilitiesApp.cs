using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The Utilities app's tables, declared as data (GameUi.TableSpec).</summary>
    internal static class UtilitiesTables
    {
        public const int Home = 0, Business = 1;   // the kinds of row (the tables' filter)

        private static readonly string[] Kinds = { "reallife_util_kind_home", "reallife_util_kind_business" };

        /// <summary>0 place, 1 service, 2 state, 3 this month so far, 4 owed, 5 due, 6 auto debit. Opens with what is owed most first.</summary>
        public static readonly TableSpec Accounts = new TableSpec("Utilities", "Accounts")
            .Column("reallife_util_col_place", 440f, TableSort.Text, flexible: true)
            .Column("reallife_util_col_service", 200f, TableSort.Text)
            .Column("reallife_util_col_state", 330f, TableSort.Text)
            .Column("reallife_util_col_month", 230f, TableSort.Money, sums: true)
            .Column("reallife_util_col_owed", 200f, TableSort.Money, sums: true)
            .Column("reallife_util_col_due", 200f, TableSort.Date)
            .Column("reallife_util_col_debit", 200f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", Kinds)
            .Order(4, false)
            .Empty("reallife_util_empty");

        /// <summary>0 place, 1 provider, 2 plan, 3 a month, 4 state, 5 contract, 6 needed to open. Opens by place.</summary>
        public static readonly TableSpec Internet = new TableSpec("Utilities", "Internet")
            .Column("reallife_util_col_place", 420f, TableSort.Text, flexible: true)
            .Column("reallife_net_col_provider", 300f, TableSort.Text)
            .Column("reallife_net_col_plan", 190f, TableSort.Number)
            .Column("reallife_net_col_month", 200f, TableSort.Money, sums: true)
            .Column("reallife_util_col_state", 260f, TableSort.Text)
            .Column("reallife_net_col_contract", 230f, TableSort.Date)
            .Column("reallife_net_col_needed", 200f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", Kinds)
            .Order(0, true)
            .Empty("reallife_net_empty");

        /// <summary>0 district, 1 the company's network there, 2 its connections, 3 its share, 4 homes and businesses, 5 rivals there, 6 its network's cost. Opens by district.</summary>
        public static readonly TableSpec Network = new TableSpec("Utilities", "Network")
            .Column("reallife_bb_col_district", 360f, TableSort.Text, flexible: true)
            .Column("reallife_bb_col_network", 330f, TableSort.Text)
            .Column("reallife_bb_col_connections", 220f, TableSort.Number, sums: true)
            .Column("reallife_bb_col_share", 150f, TableSort.Number)
            .Column("reallife_bb_col_market", 240f, TableSort.Number, sums: true)
            .Column("reallife_bb_col_rivals", 130f, TableSort.Number)
            .Column("reallife_bb_col_cost", 230f, TableSort.Money, sums: true, right: true)
            .Order(0, true)
            .Empty("reallife_bb_empty");
    }

    /// <summary>
    /// The Utilities app in the game's frame (GameUi.AppShell): the Accounts, Internet and Your network tabs, each with its
    /// box. Nothing runs while the app is closed.
    /// </summary>
    public sealed partial class UtilitiesApp
    {
        private const int TabInternet = 1, TabNetwork = 2;
        private const string GuideTitle = "reallife_meta_title_utilities";   // the Guide's page every page points to at its foot
        public const int InternetTab = TabInternet;
        private static readonly string[] TabKeys = { "reallife_util_tab_accounts", "reallife_util_tab_internet", "reallife_util_tab_network" };
        private static Network _cityAsItIs;   // the city's market before any company: a district's rivals before founding

        private readonly UtilitiesDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(24);
        private readonly List<TableRow> _pool = new List<TableRow>(24);
        private readonly List<int> _reach = new List<int>(4);
        private AppShell _shell;
        private DataTable _table;
        private string _pick;
        private int _tab;
        private bool _ready, _writing;
        private bool _choosing;                 // the internet box shows the plans to buy (no connection, or "Change plan")
        private int _provider, _plan;           // what is chosen there: an entry of _reach, and a plan of that provider
        private bool _contract = true;

        public UtilitiesApp(UtilitiesDirector director)
        {
            _director = director;
        }

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected.</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            _shell = AppShell.Make(menu, dormant, UtilitiesDirector.AppId);
            _table = DataTable.On(_shell, "Utilities");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(UtilitiesTables.Accounts))
            {
                return null;
            }
            _table.Money = amount => UtilitiesText.Money((float)amount);
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

        public void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            _ready = _director.AppOpened();
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
        }

        /// <summary>The tab shown when the app next opens (another app sends the player here: Newsstand, for a connection).</summary>
        public void Want(int tab)
        {
            _tab = Math.Max(0, Math.Min(TabKeys.Length - 1, tab));
            _pick = null;
            _choosing = false;
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
                _choosing = false;
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
                return;
            }
            _pick = row.Id;
            _choosing = false;
            _provider = _plan = 0;
            _contract = true;
            Refresh();
        }

        private void Back()
        {
            if (_pick == null)
            {
                return;
            }
            _pick = null;
            _choosing = false;
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
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                string selected = null;
                TableSpec spec = _tab == TabNetwork ? UtilitiesTables.Network : _tab == TabInternet ? UtilitiesTables.Internet : UtilitiesTables.Accounts;
                UtilitiesState state = _ready ? _director.State : null;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, UtilitiesText.T("reallife_util_app"));
                    box.Title(UtilitiesText.T("reallife_util_off_title"));
                    box.Note(UtilitiesText.T("reallife_util_off_body"));
                }
                else if (_tab == TabInternet)
                {
                    WriteInternet(box, state, ref selected);
                }
                else if (_tab == TabNetwork)
                {
                    WriteNetwork(box, state, ref selected);
                }
                else
                {
                    WriteAccounts(box, state, ref selected);
                }
                box.Done();
                _table.Show(spec, _rows, selected);
            }
            finally
            {
                _writing = false;
            }
        }

        private void WriteAccounts(InfoBox box, UtilitiesState state, ref string selected)
        {
            int today = UtilitiesGame.Today, owed = 0, month = 0;
            Account picked = null;
            for (int i = 0; i < state.Accounts.Count; i++)
            {
                Account a = state.Accounts[i];
                string id = a.Place + "|" + (int)a.Service;
                TableRow row = Row(id);
                row.Pick = true;
                row.Kind = a.Kind == PlaceKind.Home ? UtilitiesTables.Home : UtilitiesTables.Business;
                if (id == _pick)
                {
                    selected = id;
                    picked = a;
                }
                int sofar = UtilitiesRules.Money(a.Accrued);
                owed += a.Owed;
                month += sofar;
                bool late = a.Owed > 0 && a.DueDay > 0 && today > a.DueDay;
                bool off = !UtilitiesRules.Supplied(a, today);
                row.Set(0, _director.NameOf(a));
                row.Set(1, UtilitiesText.Service(a.Service));
                row.Set(2, UtilitiesText.State(a, today), off ? TableRow.Red : late ? TableRow.Yellow : TableRow.Plain);
                row.Set(3, UtilitiesText.Money(sofar), sofar);
                row.Set(4, a.Owed > 0 ? UtilitiesText.Money(a.Owed) : "-", a.Owed, late ? TableRow.Red : TableRow.Plain);
                row.Set(5, a.Owed > 0 && a.DueDay > 0 ? UtilitiesText.Day(a.DueDay) : "-", a.Owed > 0 && a.DueDay > 0 ? a.DueDay : double.NaN);
                row.Set(6, UtilitiesText.T(a.AutoDebit ? "reallife_util_debit_on" : "reallife_util_debit_off"));
            }
            UiClone.Words(_shell.Header, UtilitiesText.T("reallife_util_headline", "count", state.Accounts.Count.ToString(), "owed", UtilitiesText.Money(owed)));
            if (picked == null)
            {
                _pick = null;
                box.Title(UtilitiesText.T("reallife_util_app"));
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_util_box_owed", UtilitiesText.Money(owed));
                box.Row("reallife_util_box_month", UtilitiesText.Money(month));
                box.Note(UtilitiesText.T("reallife_util_box_pick"));
                box.Line(UtilitiesText.T("reallife_util_box_office", "office", _director.OfficeName()));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            WriteAccount(box, picked, today);
        }

        private void WriteAccount(InfoBox box, Account a, int today)
        {
            box.Title(_director.NameOf(a));
            box.Chip(UtilitiesText.State(a, today), a.State == SupplyState.Cut ? CellTone.Red : a.State == SupplyState.Temporary ? CellTone.Yellow : CellTone.Green);
            box.Section("reallife_ibox_sec_now");
            if (a.Service != Service.Internet && (a.State == SupplyState.Temporary || a.State == SupplyState.Cut))
            {
                box.Line(UtilitiesText.T(a.State == SupplyState.Temporary ? "reallife_util_box_apply_temporary" : "reallife_util_box_apply_cut", "office", _director.OfficeName()));
            }
            if (a.Owed > 0)
            {
                box.Row("reallife_util_box_owed_one", UtilitiesText.T("reallife_util_box_owed_value", "amount", UtilitiesText.Money(a.Owed), "day", UtilitiesText.Day(a.DueDay)), CellTone.Yellow);
            }
            else
            {
                box.Note(UtilitiesText.T("reallife_util_box_nothing_owed"));
            }
            box.Row("reallife_util_box_provider", UtilitiesText.ProviderOf(a));
            box.Headline("reallife_util_box_this_month");
            if (a.Service != Service.Internet)
            {
                string units = (a.Units / 1000f).ToString(a.Service == Service.Power ? "0.0" : "0.00", System.Globalization.CultureInfo.InvariantCulture);
                box.Row(a.Service == Service.Power ? "reallife_util_box_kwh" : "reallife_util_box_m3", units);
            }
            box.Row("reallife_util_box_sofar", UtilitiesText.Money(UtilitiesRules.Money(a.Accrued)));
            if (a.LastBill > 0 || a.Deposit > 0)
            {
                box.Headline("reallife_util_box_bills");
            }
            if (a.LastBill > 0)
            {
                box.Row("reallife_util_box_last", UtilitiesText.T("reallife_util_box_last_value", "amount", UtilitiesText.Money(a.LastBill), "day", UtilitiesText.Day(a.LastBillDay)));
            }
            if (a.Deposit > 0)
            {
                box.Row("reallife_util_box_deposit", UtilitiesText.T("reallife_util_box_deposit_value", "amount", UtilitiesText.Money(a.Deposit),
                    "left", Math.Max(0, UtilitiesCatalogue.DepositBackBills - a.OnTime).ToString()));
            }
            if (a.State != SupplyState.Closed)
            {
                box.Switch("reallife_util_box_debit", a.AutoDebit, on => UiClone.Safe(() =>
                {
                    a.AutoDebit = on;
                    Refresh();
                }));
            }
            Transform buttons = box.ButtonRow();
            if (a.Owed > 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_util_box_pay", () => UiClone.Safe(() =>
                {
                    if (!_director.Pay(a, UtilitiesGame.Today))
                    {
                        UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T("reallife_util_desk_no_cash"), 8f);
                    }
                    Refresh();
                }));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_util_box_back", Back);
            Meta.Guide.Point(box, GuideTitle);
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
    }
}
