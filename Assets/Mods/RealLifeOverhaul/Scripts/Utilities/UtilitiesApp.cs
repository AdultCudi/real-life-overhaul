using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using Streets;
using UI;
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
    /// The Utilities app, in the game's frame (GameUi.AppShell, copied from MyEmployees) as Services and Finance are,
    /// with the kit's table, on two tabs:
    ///
    ///   Accounts   every account of every place the player rents or owns: where it stands, what this month has come
    ///              to so far, what is owed and when, and whether auto debit pays it; the white box has the picked
    ///              account's provider, use and bills, its deposit, the auto debit switch and "Pay now"
    ///   Internet   every place with its connection (provider, plan, price, contract) and whether the business needs one
    ///              to open; the white box buys one (the providers that reach the place, their plans, a contract or not),
    ///              changes it, cancels it, or reconnects it after a cut
    ///   Your network  the player's own broadband company (BroadbandCompany): each district with the network there, its
    ///              connections and share, the homes and businesses and the rivals there; the white box founds the
    ///              company, builds a district's network, sets its prices, maintenance and marketing, or sells it
    ///
    /// Power and water are applied for in person at the office; the internet is bought here. Nothing runs while the app
    /// is closed.
    /// </summary>
    public sealed class UtilitiesApp
    {
        private const int TabAccounts = 0, TabInternet = 1, TabNetwork = 2;
        private const string GuideTitle = "reallife_meta_title_utilities";   // the Guide's page every page of the app points to at its foot
        public const int InternetTab = TabInternet;
        private static readonly string[] TabKeys = { "reallife_util_tab_accounts", "reallife_util_tab_internet", "reallife_util_tab_network" };
        private static Network _cityAsItIs;   // the city's market before any company, for a district's rivals before founding

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
                        UtilitiesText.Toast(UI.Notification.NotificationType.Warning, UtilitiesText.T("reallife_util_desk_no_cash"), 8f);
                    }
                    Refresh();
                }));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_util_box_back", Back);
            Meta.Guide.Point(box, GuideTitle);
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
                int left = InternetRules.GraceLeft(state, today);
                if (left > 0)
                {
                    box.Line(UtilitiesText.T("reallife_net_box_grace", "left", left.ToString(), "days", InternetCatalogue.GraceDays.ToString(),
                        "day", UtilitiesText.Day(state.GraceUntil - 1), "count", state.Grace.Count.ToString()));
                }
                box.Note(UtilitiesText.T("reallife_net_box_intro"));
                box.Note(UtilitiesText.T("reallife_net_box_needs_note"));
                Meta.Guide.Point(box, GuideTitle);
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
            if (UtilitiesDirector.NeedsNet(place))
            {
                box.Line(UtilitiesText.T("reallife_net_box_needs"));
                if (InternetRules.InGrace(state, place.Place, today))
                {
                    box.Line(UtilitiesText.T("reallife_net_box_grace_place", "left", InternetRules.GraceLeft(state, today).ToString(), "days", InternetCatalogue.GraceDays.ToString(),
                        "day", UtilitiesText.Day(state.GraceUntil - 1)));
                }
            }
            if (live && !_choosing)
            {
                Provider p = InternetRules.Get(a.Provider);
                box.Row("reallife_util_box_state", InternetRules.InOutage(a, hour) ? UtilitiesText.T("reallife_net_state_outage") : UtilitiesText.State(a, today));
                if (InternetRules.InOutage(a, hour))
                {
                    box.Line(UtilitiesText.T("reallife_net_box_outage", "hour", UtilitiesText.Hour(a.OutageUntil % 24)));
                }
                box.Row("reallife_net_box_price", UtilitiesText.Money(a.PriceCents / 100f));
                box.Row("reallife_net_box_provider", p != null ? UtilitiesText.T(p.NameKey) + " (" + UtilitiesText.T(p.KindKey) + ")" : "-");
                if (p != null)
                {
                    box.Row("reallife_net_box_plan", UtilitiesText.T("reallife_net_mbps", "mbps", p.Mbps[Math.Min(a.Plan, p.Mbps.Length - 1)].ToString()));
                }
                box.Row("reallife_net_box_contract", a.ContractUntil > today
                    ? UtilitiesText.T("reallife_net_box_contract_until", "day", UtilitiesText.Day(a.ContractUntil), "fee", UtilitiesText.Money(_director.NetExitFee(place)))
                    : UtilitiesText.T("reallife_net_month_by_month"));
                box.Row("reallife_net_box_district", district);
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
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            box.Row("reallife_net_box_district", district);
            WriteChoice(box, place, a, live, today);
            Meta.Guide.Point(box, GuideTitle);
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
            bool ours = provider == InternetCatalogue.OwnIndex && _director.State.Network.Running;
            box.Selector(p.Mbps.Length, _plan, i => ours ? UtilitiesText.T("reallife_net_plan_option_own", "mbps", p.Mbps[i].ToString())
                : UtilitiesText.T("reallife_net_plan_option", "mbps", p.Mbps[i].ToString(), "price", UtilitiesText.Money(_director.NetPrice(place, provider, i, _contract) / 100f)), i => UiClone.Safe(() =>
            {
                _plan = i;
                Refresh();
            }));
            if (!ours)
            {
                box.Switch("reallife_net_box_contract_switch", _contract, on => UiClone.Safe(() =>
                {
                    _contract = on;
                    Refresh();
                }));
            }
            bool same = live && a.Provider == provider && a.State != SupplyState.Cut;
            box.Row("reallife_net_box_price", UtilitiesText.Money(_director.NetPrice(place, provider, _plan, _contract && !ours) / 100f));
            if (!same)
            {
                box.Line(ours ? UtilitiesText.T("reallife_net_box_install_own") : _contract ? UtilitiesText.T("reallife_net_box_install_free")
                    : UtilitiesText.T("reallife_net_box_install_fee", "fee", UtilitiesText.Money(InternetCatalogue.InstallFee)));
                box.Line(UtilitiesText.T("reallife_net_box_install_day", "day", UtilitiesText.Day(today + InternetCatalogue.InstallDays)));
                int exit = live ? _director.NetExitFee(place) : 0;
                if (exit > 0)
                {
                    box.Line(UtilitiesText.T("reallife_net_box_switch_fee", "provider", UtilitiesText.ProviderOf(a), "fee", UtilitiesText.Money(exit)));
                }
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
            if (_contract && !ours)
            {
                box.Note(UtilitiesText.T("reallife_net_box_contract_note", "months", InternetCatalogue.ContractMonths.ToString()));
            }
        }

        // ---------------------------------------------------------------- your network

        private void WriteNetwork(InfoBox box, UtilitiesState state, ref string selected)
        {
            Network n = state.Network;
            int today = UtilitiesGame.Today, level = Prices.Level, picked = -1, open = 0, scale = _director.Company != null ? _director.Company.ScaleNow() : 0, buy = EndScaleRules.Buying(Settings.EndScale);
            for (int d = 0; d < Network.Districts; d++)
            {
                string id = "bb|" + d;
                TableRow row = Row(id);
                row.Pick = true;
                if (id == _pick)
                {
                    selected = id;
                    picked = d;
                }
                bool isOpen = n.Open(d, today), digging = n.Digging(d);
                open += isOpen ? 1 : 0;
                int connections = BroadbandRules.Connections(n, d), share = n.Exists ? n.Share[d, Network.Own] : 0, rivals = Rivals(d);
                int cost = isOpen || digging ? n.Spent[d] : BroadbandRules.BuildCost(d, level, buy);
                int market = MarketShown(n, d, buy, isOpen || digging);
                row.Set(0, BroadbandCompany.District(d));
                row.Set(1, isOpen ? UtilitiesText.T("reallife_bb_state_open", "day", UtilitiesText.Day(n.Built[d])) : digging ? UtilitiesText.T("reallife_bb_state_digging", "day", UtilitiesText.Day(n.ReadyDay(d)))
                    : UtilitiesText.T("reallife_bb_state_none"), isOpen ? TableRow.Green : digging ? TableRow.Yellow : TableRow.Plain);
                row.Set(2, n.Exists ? connections.ToString("N0", UtilitiesText.Culture) : "-", connections);
                row.Set(3, n.Exists ? UtilitiesText.T("reallife_bb_percent", "share", UtilitiesText.Percent(share)) : "-", share);
                row.Set(4, market.ToString("N0", UtilitiesText.Culture), market);
                row.Set(5, rivals.ToString(UtilitiesText.Culture), rivals);
                row.Set(6, UtilitiesText.Money(cost), cost);
            }
            UiClone.Words(_shell.Header, n.Running ? UtilitiesText.T("reallife_bb_headline", "count", BroadbandRules.Connections(n).ToString("N0", UtilitiesText.Culture), "districts", open.ToString())
                : UtilitiesText.T(n.State == CompanyState.Sold ? "reallife_bb_headline_sold" : "reallife_bb_headline_none"));
            if (picked >= 0)
            {
                WriteDistrict(box, n, picked, today, level, scale, buy);
                return;
            }
            _pick = null;
            WriteCompany(box, n, open, level, scale, today, buy);
        }

        // A district's homes and businesses as the company can reach them: what its network reaches once dug, or would reach if dug now (at the scale, the whole district).
        private static int MarketShown(Network n, int d, int scale, bool built)
        {
            return built ? BroadbandRules.MarketOf(n, d) : EndScaleRules.Mix(BroadbandCatalogue.Market[d], BroadbandRules.MarketAtScale(d), scale);
        }

        private void WriteCompany(InfoBox box, Network n, int open, int level, int scale, int today, int buy)
        {
            BroadbandCompany company = _director.Company;
            BuildingRegistration hq = UtilitiesGame.Headquarters();
            box.Title(UtilitiesText.T("reallife_bb_box_title"));
            if (n.State == CompanyState.Sold)
            {
                box.Line(UtilitiesText.T("reallife_bb_box_sold", "day", UtilitiesText.Day(n.SoldDay), "price", UtilitiesText.Money(n.SalePrice)));
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            if (!n.Running)
            {
                int fee = BroadbandRules.LicenceFee(level, buy);
                box.Line(hq != null ? UtilitiesText.T("reallife_bb_box_hq", "name", HqName(hq)) : UtilitiesText.T("reallife_bb_box_need_hq"));
                box.Note(UtilitiesText.T("reallife_bb_box_intro", "fee", UtilitiesText.Money(fee)));
                if (hq != null)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_bb_box_found", () => Ask("reallife_bb_confirm_found_title".Localize(),
                        "reallife_bb_confirm_found_body".Localize(new { fee = UtilitiesText.Money(fee) }), "reallife_bb_box_found", false,
                        () => company.Found(out string why) ? null : why));
                }
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            if (hq == null)
            {
                box.Line(UtilitiesText.T("reallife_bb_box_no_hq"));
            }
            int connections = BroadbandRules.Connections(n);
            box.Row("reallife_bb_box_connections", UtilitiesText.T("reallife_bb_box_connections_value", "count", connections.ToString("N0", UtilitiesText.Culture), "districts", open.ToString()));
            if (scale > 0)
            {
                box.Row("reallife_endscale_bb_crews", UtilitiesText.T("reallife_endscale_bb_crews_value",
                    "field", n.Crew[BroadbandCatalogue.Field].ToString(), "field_need", BroadbandRules.CrewNeed(n, BroadbandCatalogue.Field, today).ToString(),
                    "calls", n.Crew[BroadbandCatalogue.Calls].ToString(), "calls_need", BroadbandRules.CrewNeed(n, BroadbandCatalogue.Calls, today).ToString(),
                    "ops", n.Crew[BroadbandCatalogue.Operations].ToString(), "ops_need", BroadbandRules.CrewNeed(n, BroadbandCatalogue.Operations, today).ToString(),
                    "wages", UtilitiesText.Money(BroadbandRules.CrewCents(n) * BroadbandCatalogue.WageMonthDays / GameCalendar.MonthDays * level / PriceRules.Start / 100f)));
            }
            if (n.Owed > 0)
            {
                box.Line(UtilitiesText.T("reallife_endscale_owed_line", "owed", UtilitiesText.Money(n.Owed)));
            }
            if (n.HistoryCount > 0)
            {
                box.Row("reallife_bb_box_last", UtilitiesText.T("reallife_bb_box_last_value", "takings", UtilitiesText.Money(n.LastTakings), "costs", UtilitiesText.Money(n.LastCosts),
                    "profit", UtilitiesText.Money(n.LastTakings - n.LastCosts)));
            }
            box.Row("reallife_bb_box_month", UtilitiesText.T("reallife_bb_box_month_value", "takings", UtilitiesText.Money(n.MonthTakings), "costs", UtilitiesText.Money(n.MonthCosts)));
            box.Row("reallife_bb_box_year", UtilitiesText.Money(BroadbandRules.YearProfit(n)));
            long value = BroadbandRules.SaleValue(n);
            box.Row("reallife_bb_box_value", UtilitiesText.Money(value));
            box.Headline("reallife_bb_box_settings");
            int least = BroadbandCatalogue.PriceLeast, step = BroadbandCatalogue.PriceStep;
            box.Selector((BroadbandCatalogue.PriceMost - least) / step + 1, (n.PriceShare - least) / step, i => PricesText(least + i * step, level), i => UiClone.Safe(() =>
            {
                company.SetPrices(least + i * step);
                Refresh();
            }));
            int careLeast = BroadbandCatalogue.MaintenanceLeast, careStep = BroadbandCatalogue.MaintenanceStep;
            box.Selector((BroadbandCatalogue.MaintenanceMost - careLeast) / careStep + 1, (n.Maintenance - careLeast) / careStep, i => UtilitiesText.T("reallife_bb_maintenance_option",
                "share", ((careLeast + i * careStep) / 10).ToString(), "outages", BroadbandRules.OutagePerMille(careLeast + i * careStep).ToString()), i => UiClone.Safe(() =>
            {
                company.SetMaintenance(careLeast + i * careStep);
                Refresh();
            }));
            box.Selector(BroadbandCatalogue.Marketing.Length, n.Marketing, i => i == 0 ? UtilitiesText.T("reallife_bb_marketing_none")
                : UtilitiesText.T("reallife_bb_marketing_option", "amount", UtilitiesText.Money(BroadbandRules.MarketingMoney(i, level))), i => UiClone.Safe(() =>
            {
                company.SetMarketing(i);
                Refresh();
            }));
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_bb_box_sell", () => Ask("reallife_bb_confirm_sell_title".Localize(),
                "reallife_bb_confirm_sell_body".Localize(new { price = UtilitiesText.Money(value), buyer = UtilitiesText.T(BroadbandCatalogue.SoldNameKey) }), "reallife_bb_box_sell", true,
                () => company.Sell(out string why) ? null : why));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteDistrict(InfoBox box, Network n, int d, int today, int level, int scale, int buy)
        {
            BroadbandCompany company = _director.Company;
            box.Title(BroadbandCompany.District(d));
            int cost = BroadbandRules.BuildCost(d, level, buy), extend = BroadbandRules.ExtendCost(n, d, level, today, buy);
            if (n.Open(d, today))
            {
                box.Line(UtilitiesText.T("reallife_bb_box_district_open", "day", UtilitiesText.Day(n.Built[d]), "count", BroadbandRules.Connections(n, d).ToString("N0", UtilitiesText.Culture),
                    "share", UtilitiesText.Percent(n.Share[d, Network.Own]), "trust", UtilitiesText.Percent(BroadbandRules.Trust(n, d, today, GameCalendar.MonthDays))));
            }
            else if (n.Digging(d))
            {
                box.Line(UtilitiesText.T("reallife_bb_box_district_digging", "day", UtilitiesText.Day(n.ReadyDay(d))));
            }
            else
            {
                box.Line(UtilitiesText.T("reallife_bb_box_district_cost", "cost", UtilitiesText.Money(cost), "months", BroadbandCatalogue.BuildMonths.ToString()));
            }
            box.Row("reallife_bb_box_district_market", MarketShown(n, d, buy, n.Open(d, today) || n.Digging(d)).ToString("N0", UtilitiesText.Culture));
            if (extend > 0)
            {
                box.Line(UtilitiesText.T("reallife_endscale_bb_district_old", "reach", BroadbandCatalogue.Market[d].ToString("N0", UtilitiesText.Culture),
                    "all", BroadbandRules.MarketAtScale(d).ToString("N0", UtilitiesText.Culture), "cost", UtilitiesText.Money(extend)));
            }
            box.Row("reallife_bb_box_district_shares", Shares(n.Exists ? n : CityAsItIs(), d));
            Transform buttons = box.ButtonRow();
            if (extend > 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_endscale_bb_extend", () => Ask("reallife_endscale_bb_confirm_extend_title".Localize(new { district = BroadbandCompany.District(d) }),
                    "reallife_endscale_bb_confirm_extend_body".Localize(new { cost = UtilitiesText.Money(extend) }), "reallife_endscale_bb_extend", false,
                    () => company.Extend(d, out string why) ? null : why));
            }
            if (n.Running && !n.Open(d, today) && !n.Digging(d))
            {
                int upkeep = (int)EndScaleRules.Mix((long)BroadbandRules.BuildCost(d, level) * BroadbandCatalogue.UpkeepPerMille / 1000,
                    (long)BroadbandRules.BuildCost(d, level, EndScaleRules.Full) * BroadbandCatalogue.UpkeepPerMilleScale / 1000, buy);
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_bb_box_build", () => Ask("reallife_bb_confirm_build_title".Localize(new { district = BroadbandCompany.District(d) }),
                    "reallife_bb_confirm_build_body".Localize(new { cost = UtilitiesText.Money(cost), months = BroadbandCatalogue.BuildMonths.ToString(), upkeep = UtilitiesText.Money(upkeep) }),
                    "reallife_bb_box_build", false, () => company.Build(d, out string why) ? null : why));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_util_box_back", Back);
            Meta.Guide.Point(box, GuideTitle);
        }

        // The game's own question before a large sum is spent (or a company sold): on yes, done, and any refusal said.
        private void Ask(LanguageChangeEventDataHolder title, LanguageChangeEventDataHolder body, string confirmKey, bool negative, Func<string> act)
        {
            UiClone.Safe(() => GameConfirm.Show(title, body, () => UiClone.Safe(() =>
            {
                string why = act();
                if (why != null)
                {
                    UtilitiesText.Toast(NotificationType.Warning, UtilitiesText.T(why), 8f);
                }
                Refresh();
            }), null, confirmKey, "common_cancel", false, negative));
        }

        private static string PricesText(int share, int level)
        {
            return UtilitiesText.T("reallife_bb_price_option", "share", (share / 10).ToString(), "p0", UtilitiesText.Money(BroadbandRules.PlanCents(share, 0, level) / 100f),
                "p1", UtilitiesText.Money(BroadbandRules.PlanCents(share, 1, level) / 100f), "p2", UtilitiesText.Money(BroadbandRules.PlanCents(share, 2, level) / 100f));
        }

        // The providers of a district by their share, the largest first: "Kestrel Cable 45%, Brightvein Fibre 30%, ...".
        private static string Shares(Network n, int d)
        {
            var order = new List<int>(Network.Providers);
            for (int p = 0; p < Network.Providers; p++)
            {
                if (n.Share[d, p] > 0)
                {
                    order.Add(p);
                }
            }
            order.Sort((x, y) => n.Share[d, y].CompareTo(n.Share[d, x]));
            var words = new System.Text.StringBuilder(96);
            for (int i = 0; i < order.Count; i++)
            {
                int p = order[i];
                string name = p == Network.Own ? UtilitiesText.T(n.State == CompanyState.Sold ? BroadbandCatalogue.SoldNameKey : BroadbandCatalogue.OwnNameKey) : UtilitiesText.T(InternetCatalogue.Providers[p].NameKey);
                words.Append(i == 0 ? string.Empty : ", ").Append(UtilitiesText.T("reallife_bb_share_item", "provider", name, "share", UtilitiesText.Percent(n.Share[d, p])));
            }
            return words.ToString();
        }

        private static Network CityAsItIs()
        {
            if (_cityAsItIs == null)
            {
                _cityAsItIs = new Network();
                BroadbandRules.Found(_cityAsItIs, 0);
                _cityAsItIs.State = CompanyState.None;
            }
            return _cityAsItIs;
        }

        private static int Rivals(int d)
        {
            int count = 0;
            for (int p = 0; p < InternetCatalogue.Providers.Length; p++)
            {
                count += InternetRules.Reaches(p, BroadbandCatalogue.Districts[d]) ? 1 : 0;
            }
            return count;
        }

        private static string HqName(BuildingRegistration hq)
        {
            return string.IsNullOrEmpty(hq.BusinessName) ? hq.Address.ToFormattedString() : hq.BusinessName;
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
