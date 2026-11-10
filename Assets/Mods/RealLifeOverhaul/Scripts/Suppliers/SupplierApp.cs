using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>The Suppliers app's tables as data (GameUi.TableSpec).</summary>
    public static class SupplierColumns
    {
        public static readonly TableSpec Off = new TableSpec("Suppliers", "Off").Column("reallife_supply_col_supplier", 600f, TableSort.None, flexible: true);

        /// <summary>Suppliers: 0 name, 1 goods, 2 price against the market, 3 each delivery.</summary>
        public static readonly TableSpec Firms = new TableSpec("Suppliers", "Suppliers")
            .Column("reallife_supply_col_supplier", 380f, TableSort.Text, flexible: true)
            .Column("reallife_supply_col_goods", 360f, TableSort.Text)
            .Column("reallife_supply_col_price", 200f, TableSort.Number)
            .Column("reallife_supply_col_delivery", 220f, TableSort.Money, right: true)
            .Empty("reallife_supply_no_places");

        /// <summary>Contracts: 0 supplier, 1 business, 2 next delivery, 3 each delivery, 4 state.</summary>
        public static readonly TableSpec Contracts = new TableSpec("Suppliers", "Contracts")
            .Column("reallife_supply_col_supplier", 340f, TableSort.Text, flexible: true)
            .Column("reallife_supply_col_business", 300f, TableSort.Text)
            .Column("reallife_supply_col_next", 160f, TableSort.Number)
            .Column("reallife_supply_col_delivery", 220f, TableSort.Money, sums: true)
            .Column("reallife_supply_col_state", 200f, TableSort.Text, right: true)
            .Empty("reallife_supply_contracts_none");

        /// <summary>The market: 0 kind of goods, 1 price against calm, 2 what is out of the ordinary.</summary>
        public static readonly TableSpec Market = new TableSpec("Suppliers", "The market")
            .Column("reallife_supply_col_kind", 460f, TableSort.Text, flexible: true)
            .Column("reallife_supply_col_price", 220f, TableSort.Number)
            .Column("reallife_supply_col_state", 320f, TableSort.Text, right: true);
    }

    /// <summary>
    /// The Suppliers app in the game's frame (GameUi.AppShell), for a player with the RLO interface off: Suppliers, Contracts and The market tabs over a table, and the
    /// white box for what is picked, with its choices and buttons. The same facts and the same calls as the kit's page (Ui/Ventures/SupplyPages, SupplyLive). Nothing runs
    /// while it is closed.
    /// </summary>
    public sealed class SupplierApp
    {
        private static readonly string[] TabKeys = { "reallife_supply_tab_suppliers", "reallife_supply_tab_contracts", "reallife_supply_tab_market" };

        private readonly SupplyFacts _facts = new SupplyFacts();
        private readonly List<TableRow> _rows = new List<TableRow>(12);
        private readonly List<TableRow> _pool = new List<TableRow>(12);
        private readonly List<int> _ids = new List<int>(12);
        private readonly VentWords _words = new VentWords((key, english) => key.GetLocalization(), x => SupplierGame.Money((long)System.Math.Round(x * 100.0)))
            .WithPrice(cents => SupplierGame.Money(cents));
        private AppShell _shell;
        private DataTable _table;
        private int _tab, _place, _term = SupplyPages.TermDefault, _share = SupplierCatalogue.ShareDefault, _pick;
        private bool _writing;

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("suppliers: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, SupplierDirector.AppId);
            _table = DataTable.On(_shell, "Suppliers");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(SupplierColumns.Off))
            {
                return null;
            }
            _table.Money = amount => SupplierGame.Money((long)amount * 100);
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        public void Opened()
        {
            Refresh();
        }

        public void Closed()
        {
        }

        private void ShowTab(int tab)
        {
            _tab = tab;
            _pick = 0;
            Refresh();
        }

        private void RowClicked(TableRow row)
        {
            if (row != null && row.Pick)
            {
                _pick = row.Index;
            }
            Refresh();
        }

        private TableRow Row(string id, int key)
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
            _ids.Add(key);
            return row;
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
                _ids.Clear();
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                SupplierDirector d = SupplierDirector.Instance;
                bool ready = d != null && d.Facts(_facts, _place, _term, _share);
                TableSpec spec = SupplierColumns.Off;
                UiClone.Words(_shell.Header, SupplierText.Fill("reallife_supply_app_head", "owed", SupplierGame.Money(_facts.Owed)));
                if (!ready)
                {
                    box.Title(SupplierText.Fill("reallife_supply_off_title"));
                    box.Note(SupplierText.Fill("reallife_supply_off_body"));
                }
                else
                {
                    switch (_tab)
                    {
                        case 1: spec = Contracts(box); break;
                        case 2: spec = Market(box); break;
                        default: spec = Firms(box); break;
                    }
                }
                box.Done();
                _table.Show(spec, _rows, null);
            }
            finally
            {
                _writing = false;
            }
        }

        private TableSpec Firms(InfoBox box)
        {
            SupplyFacts f = _facts;
            VentWords w = _words;
            if (f.Places.Count == 0)
            {
                box.Title(SupplierText.Fill("reallife_supply_sec_place"));
                box.Note(SupplierText.Fill("reallife_supply_no_places"));
                return SupplierColumns.Firms;
            }
            for (int i = 0; i < f.Firms.Count; i++)
            {
                SupplyFirmFact s = f.Firms[i];
                if (s.Id != SupplierCatalogue.Wholesalers && s.Why == Refusal.Kind)
                {
                    continue;
                }
                bool offers = s.Id != SupplierCatalogue.Wholesalers && s.Why == Refusal.None;
                TableRow row = Row("f" + s.Id, i);
                row.Set(0, SupplyPages.FirmName(w, s.Id), !s.Open || s.TroubleKind != 0 ? TableRow.Yellow : TableRow.Plain);
                row.Set(1, SupplyPages.Kinds(w, s.Kinds));
                row.Set(2, offers ? SupplierText.Percent(s.Against) : string.Empty, s.Against);
                row.Set(3, offers ? SupplierGame.Money(s.Value) : string.Empty, s.Value / 100);
            }
            _pick = Mathf.Clamp(_pick, 0, _ids.Count - 1);
            SupplyFirmFact p = f.Firms[_ids[_pick]];
            SupplyPlaceFact at = f.Places[f.Place];
            box.Title(SupplyPages.FirmName(w, p.Id));
            box.Selector(f.Places.Count, f.Place, i => f.Places[i].Name, i => { _place = i; Refresh(); });
            if (p.Id == SupplierCatalogue.Wholesalers)
            {
                box.Note(SupplierText.Fill("reallife_supply_wholesalers_about"));
                return SupplierColumns.Firms;
            }
            box.Note(SupplierText.Fill(SupplierKeys.About(p.Id)));
            box.RowWords(SupplierText.Fill("reallife_supply_row_owner"), string.IsNullOrEmpty(p.OwnerName) ? SupplierText.Fill("reallife_supply_owner_own")
                : SupplierText.Fill("reallife_supply_owner_rival", "rival", p.OwnerName));
            if (!string.IsNullOrEmpty(p.Depot))
            {
                box.RowWords(SupplierText.Fill("reallife_supply_row_depot"), p.Depot);
            }
            box.RowWords(SupplierText.Fill("reallife_supply_row_ontime"), SupplierText.Fill("reallife_supply_ontime_value", "share", SupplierText.Percent(p.Reliability),
                "missed", p.Missed.ToString(), "all", (p.Brought + p.Missed).ToString()));
            box.RowWords(SupplierText.Fill("reallife_supply_row_lead"), SupplierText.Fill("reallife_supply_lead_value", "n", Mathf.Max(1, p.LeadDays).ToString()));
            box.RowWords(SupplierText.Fill("reallife_supply_row_min"), SupplierGame.Money(p.Min));
            box.RowWords(SupplierText.Fill("reallife_supply_row_late"), SupplyPages.Late(w, p.Late));
            box.RowWords(SupplierText.Fill("reallife_supply_row_health"), SupplierText.Fill(SupplierKeys.HealthKeys[Mathf.Clamp(p.Band, 0, 2)]),
                p.Band == 2 ? CellTone.Yellow : CellTone.Plain);
            box.RowWords(SupplierText.Fill("reallife_supply_row_customers"), p.Customers.ToString());
            if (!p.Open)
            {
                box.Line(SupplierText.Fill("reallife_supply_shut_note", "day", p.ReopenDay.ToString()), CellTone.Yellow);
            }
            else if (p.TroubleKind != 0)
            {
                box.Line(SupplierText.Fill("reallife_supply_trouble_note", "trouble", SupplierText.Fill(SupplierKeys.TroubleKeys[Mathf.Clamp(p.TroubleKind, 0, 2)]),
                    "day", p.TroubleUntil.ToString()), CellTone.Yellow);
            }
            box.Selector(SupplierCatalogue.TermWeeks.Length + 1, f.TermStep, i => SupplyPages.Term(w, i), i => { _term = i; Refresh(); });
            box.Selector(SupplierCatalogue.Shares.Length, f.ShareStep,
                i => SupplierText.Fill("reallife_supply_share_value", "share", SupplierText.Percent(SupplierCatalogue.Shares[i])), i => { _share = i; Refresh(); });
            bool can = p.Why == Refusal.None;
            if (can)
            {
                box.RowWords(SupplierText.Fill("reallife_supply_row_delivery"),
                    SupplierText.Fill("reallife_supply_delivery_value", "amount", SupplierGame.Money(p.Value), "n", p.Lines.ToString()));
                box.RowWords(SupplierText.Fill("reallife_supply_row_price"), SupplyPages.Against(w, p.Against), p.Against <= 1000 ? CellTone.Green : CellTone.Plain);
                box.Note(f.TermStep > 0 ? SupplierText.Fill("reallife_supply_take_or_pay", "share", SupplierText.Percent(SupplierCatalogue.BreakShare))
                    : SupplierText.Fill("reallife_supply_once_note"));
                int supplier = p.Id, weeks = SupplyFacts.Weeks(f.TermStep), share = f.ShareStep;
                string place = at.Key;
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, f.TermStep > 0 ? "reallife_supply_sign" : "reallife_supply_order", () =>
                {
                    SupplyLive.Instance.Sign(supplier, place, weeks, share);
                    Refresh();
                });
            }
            else
            {
                box.Line(SupplierText.Fill(SupplierKeys.RefusalKeys[Mathf.Clamp((int)p.Why, 0, SupplierKeys.RefusalKeys.Length - 1)]), CellTone.Yellow);
            }
            return SupplierColumns.Firms;
        }

        private TableSpec Contracts(InfoBox box)
        {
            SupplyFacts f = _facts;
            VentWords w = _words;
            for (int i = 0; i < f.Contracts.Count; i++)
            {
                SupplyContractFact c = f.Contracts[i];
                string chip = SupplyPages.Chip(w, c, out UiRole tone);
                TableRow row = Row("c" + c.Id, i);
                row.Set(0, SupplyPages.FirmName(w, c.Supplier));
                row.Set(1, c.Place);
                row.Set(2, c.Running ? (c.Due + c.Slip).ToString() : string.Empty, c.Due + c.Slip);
                row.Set(3, SupplierGame.Money(c.Value), c.Value / 100);
                row.Set(4, chip, tone == UiRole.Warn ? TableRow.Yellow : tone == UiRole.Ok ? TableRow.Green : TableRow.Plain);
            }
            if (f.Owed > 0)
            {
                box.Line(SupplierText.Fill("reallife_supply_owed_note", "amount", SupplierGame.Money(f.Owed)), CellTone.Yellow);
            }
            if (f.Contracts.Count == 0)
            {
                box.Title(SupplierText.Fill("reallife_supply_sec_contracts"));
                box.Note(SupplierText.Fill("reallife_supply_contracts_none"));
                if (f.Owed > 0)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_supply_pay_owed", () => { SupplyLive.Instance.PayOwed(); Refresh(); });
                }
                return SupplierColumns.Contracts;
            }
            _pick = Mathf.Clamp(_pick, 0, f.Contracts.Count - 1);
            SupplyContractFact p = f.Contracts[_pick];
            box.Title(SupplierText.Fill("reallife_supply_sec_contract", "supplier", SupplyPages.FirmName(w, p.Supplier), "business", p.Place));
            box.RowWords(SupplierText.Fill("reallife_supply_row_term"), p.Weeks > 0
                ? SupplierText.Fill("reallife_supply_term_done", "made", p.Made.ToString(), "all", p.Deliveries.ToString()) : SupplierText.Fill("reallife_supply_term_once"));
            if (p.Running)
            {
                box.RowWords(SupplierText.Fill("reallife_supply_row_next"), SupplierText.Fill("reallife_supply_next_value", "day", (p.Due + p.Slip).ToString()));
                if (p.Out == Arrival.Late)
                {
                    box.Line(SupplierText.Fill("reallife_supply_next_late", "days", p.Slip.ToString(), "cause", SupplierText.Cause(p.Why)), CellTone.Yellow);
                }
                else if (p.Out == Arrival.Short)
                {
                    box.Line(SupplierText.Fill("reallife_supply_next_short", "share", SupplierText.Percent(1000 - p.OutShare), "cause", SupplierText.Cause(p.Why)), CellTone.Yellow);
                }
            }
            box.RowWords(SupplierText.Fill("reallife_supply_row_delivery"), SupplierGame.Money(p.Value));
            if (p.Running && p.MarketValue > 0)
            {
                int against = (int)(p.Value * 1000 / p.MarketValue);
                box.RowWords(SupplierText.Fill("reallife_supply_row_price"), SupplyPages.Against(w, against), against <= 1000 ? CellTone.Green : CellTone.Yellow);
            }
            box.RowWords(SupplierText.Fill("reallife_supply_row_paid"), SupplierGame.Money(p.Paid));
            if (p.Credit > 0)
            {
                box.RowWords(SupplierText.Fill("reallife_supply_row_credit"), SupplierGame.Money(p.Credit), CellTone.Green);
            }
            if (p.Owed > 0)
            {
                box.RowWords(SupplierText.Fill("reallife_supply_row_owed"), SupplierGame.Money(p.Owed), CellTone.Yellow);
            }
            if (p.Failures > 0)
            {
                box.RowWords(SupplierText.Fill("reallife_supply_row_failures"),
                    SupplierText.Fill("reallife_supply_failures_value", "n", p.Failures.ToString(), "most", SupplierCatalogue.FailuresToLeave.ToString()));
            }
            if (p.NotTaken > 0)
            {
                box.RowWords(SupplierText.Fill("reallife_supply_row_not_taken"), p.NotTaken.ToString(), CellTone.Yellow);
            }
            for (int i = 0; i < p.Lines.Count; i++)
            {
                box.RowWords(p.Lines[i].Name, SupplierText.Fill("reallife_supply_line_value", "units", p.Lines[i].Units.ToString(), "price", SupplierGame.Price(p.Lines[i].Mills)));
            }
            if (!p.Running)
            {
                return SupplierColumns.Contracts;
            }
            int id = p.Id;
            if (p.Weeks > 0)
            {
                box.Line(SupplierText.Fill(p.Buyer ? "reallife_supply_rules_buyer" : "reallife_supply_rules_no_buyer"), p.Buyer ? CellTone.Plain : CellTone.Yellow);
                Transform rules = box.ButtonRow();
                InfoBox.Button(rules, InfoBox.Additional, SupplierKeys.RenewKeys[(int)p.Renew], () => { SupplyLive.Instance.Rule(id, SupplierCatalogue.RuleRenew); Refresh(); });
                InfoBox.Button(rules, InfoBox.Additional, p.Cover ? "reallife_supply_rule_cover_on" : "reallife_supply_rule_cover_off",
                    () => { SupplyLive.Instance.Rule(id, SupplierCatalogue.RuleCover); Refresh(); });
                InfoBox.Button(rules, InfoBox.Additional, p.LeaveRule ? "reallife_supply_rule_leave_on" : "reallife_supply_rule_leave_off",
                    () => { SupplyLive.Instance.Rule(id, SupplierCatalogue.RuleLeave); Refresh(); });
                if (p.Renew != RenewRule.Off)
                {
                    int limit = SupplierCatalogue.RenewLimits[Mathf.Clamp(p.RenewLimit, 0, SupplierCatalogue.RenewLimits.Length - 1)];
                    box.Selector(SupplierCatalogue.RenewLimits.Length, p.RenewLimit, i => LimitWords(SupplierCatalogue.RenewLimits[i]), i =>
                    {
                        SupplierDirector now = SupplierDirector.Instance;
                        Contract c = now != null && now.Running ? now.State.ContractOf(id) : null;
                        for (int n = 0; c != null && c.RenewLimit != i && n < SupplierCatalogue.RenewLimits.Length; n++)
                        {
                            SupplyLive.Instance.Rule(id, SupplierCatalogue.RuleLimit);
                        }
                        Refresh();
                    });
                }
            }
            Transform buttons = box.ButtonRow();
            if (f.Owed > 0)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_supply_pay_owed", () => { SupplyLive.Instance.PayOwed(); Refresh(); });
            }
            if (p.MayLeave)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_supply_leave", () => { SupplyLive.Instance.Leave(id); Refresh(); });
            }
            else
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_supply_end_plain", () => SupplyLive.Instance.End(id));
            }
            return SupplierColumns.Contracts;
        }

        private static string LimitWords(int limit)
        {
            return limit >= 100000 ? SupplierText.Fill("reallife_supply_limit_any") : limit == 0 ? SupplierText.Fill("reallife_supply_limit_market")
                : SupplierText.Fill("reallife_supply_limit_value", "share", SupplierText.Percent(limit));
        }

        private TableSpec Market(InfoBox box)
        {
            SupplyFacts f = _facts;
            for (int k = 0; k < f.Kinds.Length; k++)
            {
                SupplyKindFact kind = f.Kinds[k];
                TableRow row = Row("k" + k, k);
                row.Pick = false;
                row.Set(0, SupplierText.Kind(k));
                row.Set(1, SupplierText.Percent(kind.Factor), kind.Factor);
                row.Set(2, kind.Shock == 1 ? SupplierText.Fill(kind.StartsIn > 0 ? "reallife_supply_chip_shortage_soon" : "reallife_supply_chip_shortage")
                    : kind.Shock == 2 ? SupplierText.Fill("reallife_supply_chip_glut") : SupplierText.Fill("reallife_supply_calm_sub"),
                    kind.Shock == 1 ? TableRow.Yellow : TableRow.Plain);
            }
            box.Title(SupplierText.Fill("reallife_supply_sec_record"));
            box.RowWords(SupplierText.Fill("reallife_supply_row_on_time"), f.OnTime.ToString());
            box.RowWords(SupplierText.Fill("reallife_supply_row_paid_late"), f.Late.ToString(), f.Late > 0 ? CellTone.Yellow : CellTone.Plain);
            box.RowWords(SupplierText.Fill("reallife_supply_row_defaults"), f.Defaults.ToString(), f.Defaults > 0 ? CellTone.Yellow : CellTone.Plain);
            box.Note(SupplierText.Fill("reallife_supply_market_note"));
            for (int i = 0; i < f.News.Count; i++)
            {
                box.Note(f.News[i]);
            }
            if (f.News.Count == 0)
            {
                box.Note(SupplierText.Fill("reallife_supply_news_none"));
            }
            if (f.Session)
            {
                box.Note(SupplierText.Fill("reallife_supply_rivals_session"));
            }
            else if (f.Rivals.Count == 0)
            {
                box.Note(SupplierText.Fill("reallife_supply_rivals_none"));
            }
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                box.Note(f.Rivals[i]);
            }
            return SupplierColumns.Market;
        }
    }
}
