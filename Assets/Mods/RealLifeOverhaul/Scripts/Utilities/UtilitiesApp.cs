using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Utilities
{
    /// <summary>The Utilities app's one table, declared as data (GameUi.TableSpec).</summary>
    internal static class UtilitiesTables
    {
        public const int Home = 0, Business = 1;   // the kinds of row (the table's filter)

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
    }

    /// <summary>
    /// The Utilities app, in the game's frame (GameUi.AppShell, copied from MyEmployees) as Services and Finance are,
    /// with the kit's table: every account of every place the player rents or owns, with where it stands, what this
    /// month has come to so far, what is owed and when, and whether auto debit pays it. On the left the white box: with
    /// nothing picked, the month so far and what is owed in all, and where the office is; with an account picked, its
    /// provider, use and bills, its deposit, the auto debit switch and "Pay now". Applying for an account is done in
    /// person at the office. Nothing runs while the app is closed.
    /// </summary>
    public sealed class UtilitiesApp
    {
        public static IModLogger Logger;

        private readonly UtilitiesDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(24);
        private readonly List<TableRow> _pool = new List<TableRow>(24);
        private AppShell _shell;
        private DataTable _table;
        private string _pick;
        private bool _ready, _writing;

        public UtilitiesApp(UtilitiesDirector director)
        {
            _director = director;
        }

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected.</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            _shell = AppShell.Make(menu, dormant, UtilitiesDirector.AppId);
            _table = DataTable.On(_shell, "Utilities");
            if (_table == null || !_shell.SetTabCount(1) || !_shell.SetColumns(UtilitiesTables.Accounts))
            {
                return null;
            }
            _table.Money = amount => UtilitiesText.Money((float)amount);
            _table.SelectionLost = Back;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            UiClone.Key(_shell.TabLabels[0], "reallife_util_tab_accounts");
            UiClone.Rewire(_shell.Tabs[0], Back);
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
            Refresh();
        }

        private void Back()
        {
            if (_pick == null)
            {
                return;
            }
            _pick = null;
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
                _shell.ShowTab(0);
                InfoBox box = _shell.Box;
                box.Clear();
                string selected = null;
                UtilitiesState state = _ready ? _director.State : null;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, UtilitiesText.T("reallife_util_app"));
                    box.Title(UtilitiesText.T("reallife_util_off_title"));
                    box.Line(UtilitiesText.T("reallife_util_off_body"));
                }
                else
                {
                    Write(box, state, ref selected);
                }
                box.Done();
                _table.Show(UtilitiesTables.Accounts, _rows, selected);
            }
            finally
            {
                _writing = false;
            }
        }

        private void Write(InfoBox box, UtilitiesState state, ref string selected)
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
            box.Line(UtilitiesText.Pair("reallife_util_box_provider", UtilitiesText.Provider(a.Service)));
            box.Line(UtilitiesText.Pair("reallife_util_box_state", UtilitiesText.State(a, today)));
            box.Headline("reallife_util_box_this_month");
            string units = (a.Units / 1000f).ToString(a.Service == Service.Power ? "0.0" : "0.00", System.Globalization.CultureInfo.InvariantCulture);
            box.Line(UtilitiesText.Pair(a.Service == Service.Power ? "reallife_util_box_kwh" : "reallife_util_box_m3", units));
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
            if (a.State == SupplyState.Temporary || a.State == SupplyState.Cut)
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
