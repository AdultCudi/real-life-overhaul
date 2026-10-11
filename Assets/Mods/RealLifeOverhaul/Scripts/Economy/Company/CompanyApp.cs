using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the Company app shows, in the game's frame (GameUi.AppShell, copied from MyEmployees) as Finance, the
    /// Planner and the Guide are, with Finance's table (DataTable, the game's own BaTable). Six tabs over the
    /// table on the right, and on the left the white box with the one thing picked:
    ///
    ///   Overview            each business: its district, yesterday's profit by the game's books, what needs an answer
    ///   Staff               requests, offers, a strike and overtime that wait, then every employee against the going rate
    ///   Cover               claims that wait, then each business's three kinds of cover; pick one for quotes
    ///   Marketing           each business's campaign, then the last eight reports
    ///   Tax and licences    the return, the accountant, cash off the books, and every licence a business needs
    ///   Rota                every place with staff; pick one for its forecast, the hours worth opening, the people it
    ///                       takes and the button that fills its rota; pick a day of it for that day's hours
    ///
    /// A list goes in the table, which scrolls; the white box, which does not, shows only the one thing picked, with a
    /// Back button to the tab's own page. Each tab's page is in its own file (CompanyApp.Staff.cs and its neighbours).
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and the page are made when it
    /// opens, when a tab or a row is picked, and when the director says something has changed.
    /// </summary>
    public sealed partial class CompanyApp
    {
        // (Every table is declared in CompanyApp.Tables.cs and is sorted, filtered and summed by the kit: GameUi.DataTable.)
        private static readonly string[] TabKeys = { "reallife_co_tab_overview", "reallife_co_tab_staff", "reallife_co_tab_cover", "reallife_co_tab_marketing", "reallife_co_tab_tax", "reallife_rota_tab" };
        public static IModLogger Logger;

        private readonly CompanyDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(48);
        private readonly List<TableRow> _pool = new List<TableRow>(48);

        private AppShell _shell;
        private DataTable _table;
        private TableSpec _spec = CompanyTables.Off;   // the table the page that shows is written as
        private int _tab, _wanted = -1;
        private string _pick, _wantPick;   // what is picked: a row's id ("shop:...", "emp:...", "pol:...")
        private bool _ready;               // the director has read the staff for this opening
        private bool _writing;             // a page is being written: a change it causes does not write it again

        public CompanyApp(CompanyDirector director)
        {
            _director = director;
        }

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            SessionWatch.Mark("company: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, CompanyDirector.AppId);
            _table = DataTable.On(_shell, "Company");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(CompanyTables.Off))
            {
                return null;
            }
            _table.Money = amount => CompanyText.Money((float)amount);
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

        // ---------------------------------------------------------------- opening and the tabs

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>The tab to show the next time the app opens, and what is picked on it (a notification was clicked).</summary>
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
                _pick = _wantPick;
                _wanted = -1;
                _wantPick = null;
            }
            _ready = _director.AppOpened();
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
            _director.RotaLeft();
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
            Refresh();
        }

        private void Back()
        {
            _pick = null;
            _table.ClearSelection();
            Refresh();
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
                CompanyState state = _ready ? _director.State : null;
                _spec = state != null ? CompanyTables.For(_tab) : CompanyTables.Off;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, CompanyText.T("reallife_co_app"));
                    box.Title(CompanyText.T("reallife_co_off_title"));
                    box.Line(CompanyText.T("reallife_co_off_body"));
                }
                else
                {
                    int day = SaveGameManager.Current.Day;
                    UiClone.Words(_shell.Header, CompanyText.T("reallife_co_headline", "businesses", _director.Shops.Count.ToString(), "waiting", _director.Waiting().ToString()));
                    switch (_tab)
                    {
                        case CompanyDirector.TabStaff:
                            if (PartPage(CompanyPart.Staff, box))
                            {
                                WriteStaff(box, state, day, ref selected);
                            }
                            break;
                        case CompanyDirector.TabCover:
                            if (PartPage(CompanyPart.Cover, box))
                            {
                                WriteCover(box, state, day, ref selected);
                            }
                            break;
                        case CompanyDirector.TabMarketing:
                            if (PartPage(CompanyPart.Marketing, box))
                            {
                                WriteMarketing(box, state, day, ref selected);
                            }
                            break;
                        case CompanyDirector.TabRota:
                            WriteRota(box, ref selected);
                            break;
                        case CompanyDirector.TabTax:
                            if (!_director.On(CompanyPart.Tax) && !_director.On(CompanyPart.Licences))
                            {
                                PartPage(CompanyPart.Tax, box);
                            }
                            else
                            {
                                WriteTax(box, state, day, ref selected);
                                WriteLicences(box, state, day, ref selected);
                            }
                            break;
                        default:
                            selected = WriteOverview(box, state, day);
                            break;
                    }
                }
                box.Done();
                _table.Show(_spec, _rows, selected);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Company: tab " + _tab + " written, " + _rows.Count + " row(s)" + (_pick != null ? ", picked " + _pick : string.Empty) + "; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms; " + box.Fit() + ".");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // Each tab's page is in its own file; a tab whose part is not built yet shows its name and nothing else.
        partial void WriteStaff(InfoBox box, CompanyState state, int day, ref string selected);
        partial void WriteCover(InfoBox box, CompanyState state, int day, ref string selected);
        partial void WriteMarketing(InfoBox box, CompanyState state, int day, ref string selected);
        partial void WriteTax(InfoBox box, CompanyState state, int day, ref string selected);
        partial void WriteLicences(InfoBox box, CompanyState state, int day, ref string selected);
        partial void WriteRota(InfoBox box, ref string selected);

        // A tab whose part is switched off, or stopped by a fault, says so and shows nothing else.
        private bool PartPage(CompanyPart part, InfoBox box)
        {
            if (_director.On(part))
            {
                return true;
            }
            _spec = CompanyTables.Off;
            box.Title(CompanyText.T(TabKeys[_tab]));
            box.Line(CompanyText.T("reallife_co_part_off"));
            Say("off", CompanyText.T("reallife_co_part_off_row"));
            return false;
        }

        // ---------------------------------------------------------------- rows

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

        // A row that can be picked. Returns its id if it is the one picked now.
        private string Pick(string id, string a, string b, string c, string d, int tone, ref string selected)
        {
            TableRow row = Row(id);
            row.Pick = true;
            row.A = a;
            row.B = b;
            row.C = c;
            row.D = d;
            row.Tone = tone;
            if (id == _pick)
            {
                selected = id;
            }
            return id;
        }

        /// <summary>The row written last: for the figures its columns are ordered by, its kind and the cells past the fourth.</summary>
        private TableRow Last => _rows[_rows.Count - 1];

        private void Say(string id, string words)
        {
            TableRow row = Row(id);
            row.Info = true;
            row.A = words;
        }

        // What follows a prefix in what is picked ("emp:" and the employee's id); null if something else is picked.
        private string Picked(string prefix)
        {
            return _pick != null && _pick.StartsWith(prefix, StringComparison.Ordinal) ? _pick.Substring(prefix.Length) : null;
        }

        // The same for a place and a kind ("pol:" and "place:2"): false if something else is picked.
        private bool Picked(string prefix, out string place, out int kind)
        {
            place = null;
            kind = -1;
            string rest = Picked(prefix);
            int at = rest != null ? rest.LastIndexOf(':') : -1;
            if (at <= 0 || !int.TryParse(rest.Substring(at + 1), out kind))
            {
                return false;
            }
            place = rest.Substring(0, at);
            return true;
        }

        private void BackButton(InfoBox box)
        {
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_co_back", Back);
        }

        // ---------------------------------------------------------------- Overview

        private string WriteOverview(InfoBox box, CompanyState state, int day)
        {
            string selected = null;
            IReadOnlyList<CompanyShop> shops = _director.Shops;
            CompanyShop picked = null;
            TableFilter districts = CompanyTables.Overview.Filters[0];   // the filter's kinds: the districts the businesses are in
            districts.Names.Clear();
            for (int i = 0; i < shops.Count; i++)
            {
                CompanyShop shop = shops[i];
                int waiting = ShopWaiting(state, shop, day);
                string district = CompanyText.District(shop.District);
                string id = Pick("shop:" + shop.File.Place, shop.Name, district, shop.HasBooks ? CompanyText.Money(shop.Profit) : CompanyText.T("reallife_co_no_books"),
                    waiting > 0 ? CompanyText.T("reallife_co_state_waiting", "count", waiting.ToString()) : CompanyText.T("reallife_co_state_in_order"), waiting > 0 ? 2 : 0, ref selected);
                TableRow row = Last;
                row.Kind = CompanyTables.KindOf(districts, district);
                row.Key[2] = shop.HasBooks ? shop.Profit : double.NaN;
                row.Key[3] = waiting;
                picked = id == selected ? shop : picked;
            }
            if (shops.Count == 0)
            {
                Say("none", CompanyText.T("reallife_co_row_no_business"));
            }
            if (picked != null)
            {
                WriteShop(box, state, picked, day);
                return selected;
            }
            box.Title(CompanyText.T("reallife_co_app"));
            box.Line(FinanceText.Pair("reallife_co_over_businesses", shops.Count.ToString()));
            box.Line(FinanceText.Pair("reallife_co_over_staff", state.Staff.Count.ToString()));
            box.Line(FinanceText.Pair("reallife_co_over_quarter", CompanyText.T("reallife_co_over_quarter_value", "quarter", state.Tax.Quarter.ToString(), "day", state.Tax.QuarterFrom.ToString())));
            IReadOnlyList<CompanyAlert> alerts = _director.Alerts;
            box.Headline("reallife_co_over_alerts");
            for (int i = 0; i < alerts.Count && i < 6; i++)
            {
                box.Line("<b>" + alerts[i].Title + "</b> " + alerts[i].What);
            }
            if (alerts.Count == 0)
            {
                box.Line(CompanyText.T("reallife_co_over_no_alerts"));
            }
            else if (alerts.Count > 6)
            {
                box.Line(CompanyText.T("reallife_co_over_more", "count", (alerts.Count - 6).ToString()));
            }
            if (ServicesDirector.CanClean) InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_svc_open_cleaning", ServicesDirector.OpenCleaning);   // (the cleaning service: Economy/Services)
            return null;
        }

        // What waits for an answer at one business.
        private int ShopWaiting(CompanyState state, CompanyShop shop, int day)
        {
            int waiting = 0;
            ShopFile file = shop.File;
            if (_director.On(CompanyPart.Staff))
            {
                waiting += (file.StrikeFrom > 0 ? 1 : 0) + (file.OvertimeAsk > 0 ? 1 : 0);
                for (int i = 0; i < state.Staff.Count; i++)
                {
                    StaffFile f = state.Staff[i];
                    waiting += f.Place == file.Place ? (f.AskDay > 0 ? 1 : 0) + (f.PoachDay > 0 ? 1 : 0) : 0;
                }
            }
            for (int i = 0; _director.On(CompanyPart.Cover) && i < state.Claims.Count; i++)
            {
                waiting += state.Claims[i].Stage == ClaimStage.Open && state.Claims[i].Place == file.Place ? 1 : 0;
            }
            waiting += _director.On(CompanyPart.Licences) && LicenceRules.Missing(file, shop.Needs, day) != 0 ? 1 : 0;
            return waiting;
        }

        // One business: everything the company knows of it, in a few lines.
        private void WriteShop(InfoBox box, CompanyState state, CompanyShop shop, int day)
        {
            ShopFile file = shop.File;
            box.Title(shop.Name);
            box.Line(FinanceText.Pair("reallife_co_shop_district", CompanyText.T("reallife_co_shop_district_value", "district", CompanyText.District(shop.District), "income", shop.Income.ToString())));
            box.Line(shop.HasBooks
                ? CompanyText.T("reallife_co_shop_yesterday", "takings", CompanyText.Money(shop.Takings), "profit", CompanyText.Money(shop.Profit))
                : CompanyText.T("reallife_co_shop_no_books"));
            if (_director.On(CompanyPart.Staff))
            {
                box.Line(shop.Staff > 0
                    ? CompanyText.T("reallife_co_shop_staff", "count", shop.Staff.ToString(), "standing", (shop.Standing / shop.Staff).ToString(), "low", shop.Low.ToString())
                    : CompanyText.T("reallife_co_shop_no_staff"));
                if (file.StrikeFrom > 0)
                {
                    box.Line(CompanyText.T(file.Striking(day) ? "reallife_co_shop_strike_on" : "reallife_co_shop_strike_called", "day", (file.Striking(day) ? file.StrikeEnd : file.StrikeFrom).ToString()));
                }
            }
            if (_director.On(CompanyPart.Cover))
            {
                string cover = string.Empty;
                for (int k = 0; k < file.Cover.Length; k++)
                {
                    if (file.Cover[k].Active)
                    {
                        cover += (cover.Length > 0 ? ", " : string.Empty) + CompanyText.Cover(k);
                    }
                }
                box.Line(FinanceText.Pair("reallife_co_shop_cover", cover.Length > 0 ? cover : CompanyText.T("reallife_co_none")));
            }
            if (_director.On(CompanyPart.Marketing))
            {
                box.Line(FinanceText.Pair("reallife_co_shop_campaign", file.Push >= 0
                    ? CompanyText.T(file.Pushing(day) ? "reallife_co_shop_campaign_on" : "reallife_co_shop_campaign_booked", "campaign", CompanyText.Campaign(file.Push), "day",
                        (file.Pushing(day) ? file.PushEnd - 1 : file.PushFrom).ToString())
                    : CompanyText.T("reallife_co_none")));
            }
            if (_director.On(CompanyPart.Licences))
            {
                int missing = LicenceRules.Missing(file, shop.Needs, day);
                box.Line(FinanceText.Pair("reallife_co_shop_licences", missing == 0 ? CompanyText.T("reallife_co_shop_licences_ok") : CompanyText.T("reallife_co_shop_licences_missing", "list", CompanyText.Licences(missing))));
            }
            if (_director.On(CompanyPart.Tax) && file.Hide > 0)
            {
                box.Line(FinanceText.Pair("reallife_co_shop_hidden", file.Hide + "%"));
            }
            if (ServicesDirector.CanSell) InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_svc_open_sale", () => ServicesDirector.OpenSale(file.Place));   // (selling a business: Economy/Services)
            BackButton(box);
        }
    }
}
