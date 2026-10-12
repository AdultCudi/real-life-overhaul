using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
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
    }

    /// <summary>
    /// The Utilities app, in the game's frame (GameUi.AppShell, copied from MyEmployees) as Services and Finance are,
    /// with the kit's table, on two tabs:
    ///
    ///   Accounts   every account of every place the player rents or owns: where it stands, what this month has come
    ///              to so far, what is owed and when, and whether auto debit pays it; the white box has the picked
    ///              account's provider, use and bills, its deposit, the auto debit switch and "Pay now"
    ///   Internet   every place with its connection (provider, plan, price, contract) and whether the business needs one
    ///              to open; the white box buys one (the providers that reach the place, their plans, a contract or not),
    ///              changes it, cancels it, or reconnects it after a cut
    ///
    /// Power and water are applied for in person at the office; the internet is bought here. Nothing runs while the app
    /// is closed.
    /// </summary>
    public sealed class UtilitiesApp
    {
        private const int TabAccounts = 0, TabInternet = 1;
        private static readonly string[] TabKeys = { "reallife_util_tab_accounts", "reallife_util_tab_internet" };

        public static IModLogger Logger;

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
                TableSpec spec = _tab == TabInternet ? UtilitiesTables.Internet : UtilitiesTables.Accounts;
                UtilitiesState state = _ready ? _director.State : null;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, UtilitiesText.T("reallife_util_app"));
                    box.Title(UtilitiesText.T("reallife_util_off_title"));
                    box.Line(UtilitiesText.T("reallife_util_off_body"));
                }
                else if (_tab == TabInternet)
                {
                    WriteInternet(box, state, ref selected);
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

        // ---------------------------------------------------------------- accounts

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
                box.Line(UtilitiesText.Pair("reallife_util_box_month", UtilitiesText.Money(month)));
                box.Line(UtilitiesText.Pair("reallife_util_box_owed", UtilitiesText.Money(owed)));
                box.Line(UtilitiesText.T("reallife_util_box_office", "office", _director.OfficeName()));
                box.Line(UtilitiesText.T("reallife_util_box_pick"));
                return;
            }
            WriteAccount(box, picked, today);
        }

        private void WriteAccount(InfoBox box, Account a, int today)
        {
            box.Title(_director.NameOf(a));
            box.Line(UtilitiesText.Pair("reallife_util_box_provider", UtilitiesText.ProviderOf(a)));
            box.Line(UtilitiesText.Pair("reallife_util_box_state", UtilitiesText.State(a, today)));
            box.Headline("reallife_util_box_this_month");
            if (a.Service != Service.Internet)
            {
                string units = (a.Units / 1000f).ToString(a.Service == Service.Power ? "0.0" : "0.00", System.Globalization.CultureInfo.InvariantCulture);
                box.Line(UtilitiesText.Pair(a.Service == Service.Power ? "reallife_util_box_kwh" : "reallife_util_box_m3", units));
            }
            box.Line(UtilitiesText.Pair("reallife_util_box_sofar", UtilitiesText.Money(UtilitiesRules.Money(a.Accrued))));
            box.Headline("reallife_util_box_bills");
            if (a.LastBill > 0)
            {
                box.Line(UtilitiesText.Pair("reallife_util_box_last", UtilitiesText.T("reallife_util_box_last_value", "amount", UtilitiesText.Money(a.LastBill), "day", UtilitiesText.Day(a.LastBillDay))));
            }
            box.Line(a.Owed > 0
                ? UtilitiesText.Pair("reallife_util_box_owed_one", UtilitiesText.T("reallife_util_box_owed_value", "amount", UtilitiesText.Money(a.Owed), "day", UtilitiesText.Day(a.DueDay)))
                : UtilitiesText.T("reallife_util_box_nothing_owed"));
            if (a.Deposit > 0)
            {
                box.Line(UtilitiesText.Pair("reallife_util_box_deposit", UtilitiesText.T("reallife_util_box_deposit_value", "amount", UtilitiesText.Money(a.Deposit),
                    "left", Math.Max(0, UtilitiesCatalogue.DepositBackBills - a.OnTime).ToString())));
            }
            if (a.State != SupplyState.Closed)
            {
                box.Switch("reallife_util_box_debit", a.AutoDebit, on => UiClone.Safe(() =>
                {
                    a.AutoDebit = on;
                    Refresh();
                }));
            }
            if (a.Service != Service.Internet && (a.State == SupplyState.Temporary || a.State == SupplyState.Cut))
            {
                box.Line(UtilitiesText.T(a.State == SupplyState.Temporary ? "reallife_util_box_apply_temporary" : "reallife_util_box_apply_cut", "office", _director.OfficeName()));
            }
            Transform buttons = box.ButtonRow();
            if (a.Owed > 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_util_box_pay", () => UiClone.Safe(() =>
                {
                    if (!_director.Pay(a, UtilitiesGame.Today))
                    {
                        UtilitiesText.Toast(UI.Notification.NotificationType.Warning, UtilitiesText.T("reallife_util_desk_no_cash"), 8f);
                    }
                    Refresh();
                }));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_util_box_back", Back);
        }

        // ---------------------------------------------------------------- internet

        private void WriteInternet(InfoBox box, UtilitiesState state, ref string selected)
        {
            int today = UtilitiesGame.Today, hour = today * 24 + (SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0), connected = 0;
            PlaceInfo? picked = null;
            IReadOnlyList<PlaceInfo> places = _director.Places;
            for (int i = 0; i < places.Count; i++)
            {
                PlaceInfo place = places[i];
                string id = "net|" + place.Place;
                TableRow row = Row(id);
                row.Pick = true;
                row.Kind = place.Kind == PlaceKind.Home ? UtilitiesTables.Home : UtilitiesTables.Business;
                if (id == _pick)
                {
                    selected = id;
                    picked = place;
                }
                Account a = state.Find(place.Place, Service.Internet);
                bool live = a != null && a.State != SupplyState.Closed;
                Provider p = live ? InternetRules.Get(a.Provider) : null;
                bool needs = UtilitiesDirector.NeedsNet(place);
                bool down = live && (a.State == SupplyState.Cut || InternetRules.InOutage(a, hour));
                if (live && a.State == SupplyState.Active)
                {
                    connected++;
                }
                row.Set(0, place.Name);
                row.Set(1, p != null ? UtilitiesText.T(p.NameKey) : UtilitiesText.T("reallife_net_none"), needs && !live ? TableRow.Red : TableRow.Plain);
                row.Set(2, p != null ? UtilitiesText.T("reallife_net_mbps", "mbps", p.Mbps[Math.Min(a.Plan, p.Mbps.Length - 1)].ToString()) : "-", p != null ? p.Mbps[Math.Min(a.Plan, p.Mbps.Length - 1)] : double.NaN);
                row.Set(3, live ? UtilitiesText.Money(a.PriceCents / 100f) : "-", live ? a.PriceCents / 100.0 : double.NaN);
                row.Set(4, live ? (InternetRules.InOutage(a, hour) ? UtilitiesText.T("reallife_net_state_outage") : UtilitiesText.State(a, today)) : "-", down ? TableRow.Red : TableRow.Plain);
                row.Set(5, live ? (a.ContractUntil > today ? UtilitiesText.Day(a.ContractUntil) : UtilitiesText.T("reallife_net_month_by_month")) : "-", live && a.ContractUntil > today ? a.ContractUntil : double.NaN);
                row.Set(6, UtilitiesText.T(needs ? "reallife_net_needed" : "reallife_net_not_needed"), needs && !live ? TableRow.Red : TableRow.Plain);
            }
            UiClone.Words(_shell.Header, UtilitiesText.T("reallife_net_headline", "count", connected.ToString(), "places", places.Count.ToString()));
            if (!picked.HasValue)
            {
                _pick = null;
                box.Title(UtilitiesText.T("reallife_util_tab_internet"));
                box.Line(UtilitiesText.T("reallife_net_box_intro"));
                box.Line(UtilitiesText.T("reallife_net_box_needs_note"));
                return;
            }
            WritePlace(box, picked.Value, state, today, hour);
        }

        private void WritePlace(InfoBox box, PlaceInfo place, UtilitiesState state, int today, int hour)
        {
            Account a = state.Find(place.Place, Service.Internet);
            bool live = a != null && a.State != SupplyState.Closed;
            string district = place.Reg != null && !string.IsNullOrEmpty(place.Reg.Neighborhood) ? place.Reg.Neighborhood.GetLocalization() : "-";
            box.Title(place.Name);
            box.Line(UtilitiesText.Pair("reallife_net_box_district", district));
            if (UtilitiesDirector.NeedsNet(place))
            {
                box.Line(UtilitiesText.T("reallife_net_box_needs"));
            }
            if (live && !_choosing)
            {
                Provider p = InternetRules.Get(a.Provider);
                box.Line(UtilitiesText.Pair("reallife_net_box_provider", p != null ? UtilitiesText.T(p.NameKey) + " (" + UtilitiesText.T(p.KindKey) + ")" : "-"));
                if (p != null)
                {
                    box.Line(UtilitiesText.Pair("reallife_net_box_plan", UtilitiesText.T("reallife_net_mbps", "mbps", p.Mbps[Math.Min(a.Plan, p.Mbps.Length - 1)].ToString())));
                }
                box.Line(UtilitiesText.Pair("reallife_util_box_state", InternetRules.InOutage(a, hour) ? UtilitiesText.T("reallife_net_state_outage") : UtilitiesText.State(a, today)));
                box.Line(UtilitiesText.Pair("reallife_net_box_price", UtilitiesText.Money(a.PriceCents / 100f)));
                box.Line(UtilitiesText.Pair("reallife_net_box_contract", a.ContractUntil > today
                    ? UtilitiesText.T("reallife_net_box_contract_until", "day", UtilitiesText.Day(a.ContractUntil), "fee", UtilitiesText.Money(_director.NetExitFee(place)))
                    : UtilitiesText.T("reallife_net_month_by_month")));
                if (InternetRules.InOutage(a, hour))
                {
                    box.Line(UtilitiesText.T("reallife_net_box_outage", "hour", UtilitiesText.Hour(a.OutageUntil % 24)));
                }
                Transform buttons = box.ButtonRow();
                if (a.State == SupplyState.Cut)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_net_box_reconnect", () => UiClone.Safe(() =>
                    {
                        if (!_director.ReconnectNet(place, out string why) && why != null)
                        {
                            UtilitiesText.Toast(UI.Notification.NotificationType.Warning, UtilitiesText.T(why), 8f);
                        }
                        Refresh();
                    }));
                }
                else
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_net_box_change", () => UiClone.Safe(() =>
                    {
                        _choosing = true;
                        _provider = Math.Max(0, ReachIndex(place, a.Provider));
                        _plan = a.Plan;
                        _contract = a.ContractUntil > today || _contract;
                        Refresh();
                    }));
                }
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_net_box_cancel", () => UiClone.Safe(() =>
                {
                    if (!_director.CancelNet(place, out string why) && why != null)
                    {
                        UtilitiesText.Toast(UI.Notification.NotificationType.Warning, UtilitiesText.T(why), 8f);
                    }
                    Refresh();
                }));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_util_box_back", Back);
                return;
            }
            WriteChoice(box, place, a, live, today);
        }

        // The providers that reach the place, a plan of the one chosen, a contract or not: what it costs, and "Buy".
        private void WriteChoice(InfoBox box, PlaceInfo place, Account a, bool live, int today)
        {
            Reach(place);
            if (_reach.Count == 0)
            {
                box.Line(UtilitiesText.T("reallife_net_box_none_here"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_util_box_back", live ? (Action)(() => UiClone.Safe(() => { _choosing = false; Refresh(); })) : Back);
                return;
            }
            _provider = Math.Max(0, Math.Min(_reach.Count - 1, _provider));
            int provider = _reach[_provider];
            Provider p = InternetRules.Get(provider);
            _plan = Math.Max(0, Math.Min(p.Mbps.Length - 1, _plan));
            box.Headline("reallife_net_box_choose");
            box.Selector(_reach.Count, _provider, i => UtilitiesText.T("reallife_net_provider_option", "provider", UtilitiesText.T(InternetRules.Get(_reach[i]).NameKey),
                "kind", UtilitiesText.T(InternetRules.Get(_reach[i]).KindKey)), i => UiClone.Safe(() =>
            {
                _provider = i;
                _plan = 0;
                Refresh();
            }));
            box.Selector(p.Mbps.Length, _plan, i => UtilitiesText.T("reallife_net_plan_option", "mbps", p.Mbps[i].ToString(),
                "price", UtilitiesText.Money(_director.NetPrice(place, provider, i, _contract) / 100f)), i => UiClone.Safe(() =>
            {
                _plan = i;
                Refresh();
            }));
            box.Switch("reallife_net_box_contract_switch", _contract, on => UiClone.Safe(() =>
            {
                _contract = on;
                Refresh();
            }));
            bool same = live && a.Provider == provider && a.State != SupplyState.Cut;
            box.Line(UtilitiesText.Pair("reallife_net_box_price", UtilitiesText.Money(_director.NetPrice(place, provider, _plan, _contract) / 100f)));
            if (!same)
            {
                box.Line(_contract ? UtilitiesText.T("reallife_net_box_install_free") : UtilitiesText.T("reallife_net_box_install_fee", "fee", UtilitiesText.Money(InternetCatalogue.InstallFee)));
                box.Line(UtilitiesText.T("reallife_net_box_install_day", "day", UtilitiesText.Day(today + InternetCatalogue.InstallDays)));
                int exit = live ? _director.NetExitFee(place) : 0;
                if (exit > 0)
                {
                    box.Line(UtilitiesText.T("reallife_net_box_switch_fee", "provider", UtilitiesText.ProviderOf(a), "fee", UtilitiesText.Money(exit)));
                }
            }
            if (_contract)
            {
                box.Line(UtilitiesText.T("reallife_net_box_contract_note", "months", InternetCatalogue.ContractMonths.ToString()));
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, same ? "reallife_net_box_change" : "reallife_net_box_buy", () => UiClone.Safe(() =>
            {
                if (_director.BuyNet(place, provider, _plan, _contract, out string why))
                {
                    _choosing = false;
                }
                else if (why != null)
                {
                    UtilitiesText.Toast(UI.Notification.NotificationType.Warning, UtilitiesText.T(why), 8f);
                }
                Refresh();
            }));
            if (live)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_net_box_keep", () => UiClone.Safe(() =>
                {
                    _choosing = false;
                    Refresh();
                }));
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_util_box_back", Back);
            }
        }

        private void Reach(PlaceInfo place)
        {
            _reach.Clear();
            string district = place.Reg != null ? place.Reg.Neighborhood : null;
            for (int p = 0; p < InternetRules.Count; p++)
            {
                if (InternetRules.Reaches(p, district))
                {
                    _reach.Add(p);
                }
            }
        }

        private int ReachIndex(PlaceInfo place, int provider)
        {
            Reach(place);
            return _reach.IndexOf(provider);
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
