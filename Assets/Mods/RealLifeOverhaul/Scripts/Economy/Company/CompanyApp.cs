using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Company app in the game's own frame (GameUi.AppShell): seven tabs, a list in the table and the one thing picked
    /// in the white box. Nothing runs while it is closed; a page is written on opening, a pick or a change.
    /// </summary>
    public sealed partial class CompanyApp
    {
        private static readonly string[] TabKeys =
        {
            "reallife_co_tab_overview", "reallife_co_tab_staff", "reallife_co_tab_cover", "reallife_co_tab_marketing", "reallife_co_tab_tax", "reallife_rota_tab",
            "reallife_mgr_tab"
        };
        public static IModLogger Logger;

        private readonly CompanyDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(48);
        private readonly List<TableRow> _pool = new List<TableRow>(48);

        private AppShell _shell;
        private DataTable _table;
        private TableSpec _spec = CompanyTables.Off;   // the table the showing page is written as
        private int _tab, _wanted = -1;
        private string _pick, _wantPick;   // a row's id: "shop:...", "emp:...", "pol:..."
        private bool _ready;               // the director has read the staff for this opening
        private bool _writing;             // a change a page causes while it is written does not write it again

        public CompanyApp(CompanyDirector director)
        {
            _director = director;
        }

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("company: the app being built from the game's MyEmployees");   // once per city
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

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>The tab to show the next time the app opens, and what is picked on it: a notification was clicked.</summary>
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
            int mark = row.Id.IndexOf('#');   // a row that shares its page with another of the list (the managers' Needs you) carries a mark after it
            _pick = mark > 0 ? row.Id.Substring(0, mark) : row.Id;
            Refresh();
        }

        private void Back()
        {
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
                    box.Note(CompanyText.T("reallife_co_off_body"));
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
                        case CompanyDirector.TabManagers:
                            WriteManagers(box, ref selected);
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
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms; " + box.Fit() + ".");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        partial void WriteStaff(InfoBox box, CompanyState state, int day, ref string selected);
        partial void WriteCover(InfoBox box, CompanyState state, int day, ref string selected);
        partial void WriteMarketing(InfoBox box, CompanyState state, int day, ref string selected);
        partial void WriteTax(InfoBox box, CompanyState state, int day, ref string selected);
        partial void WriteLicences(InfoBox box, CompanyState state, int day, ref string selected);
        partial void WriteRota(InfoBox box, ref string selected);
        partial void WriteManagers(InfoBox box, ref string selected);

        // A tab whose part is off, or stopped by a fault, says so and shows nothing else.
        private bool PartPage(CompanyPart part, InfoBox box)
        {
            if (_director.On(part))
            {
                return true;
            }
            _spec = CompanyTables.Off;
            box.Title(CompanyText.T(TabKeys[_tab]));
            box.Line(CompanyText.T("reallife_co_part_off"), CellTone.Yellow);
            Guide(box);
            Say("off", CompanyText.T("reallife_co_part_off_row"));
            return false;
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

        // A row that can be picked; its id, which becomes 'selected' if it is the one picked now.
        private string Pick(string id, string a, string b, string c, string d, int tone, ref string selected)
        {
            TableRow row = Row(id);
            row.Pick = true;
            row.A = a;
            row.B = b;
            row.C = c;
            row.D = d;
            row.Tone = (CellTone)tone;   // 0 plain, 1 yellow, 2 red
            if (id == _pick)
            {
                selected = id;
            }
            return id;
        }

        /// <summary>The row written last, for its sort keys, its kind and its cells past the fourth.</summary>
        private TableRow Last => _rows[_rows.Count - 1];

        private void Say(string id, string words)
        {
            TableRow row = Row(id);
            row.Info = true;
            row.A = words;
        }

        // What follows a prefix in the pick ("emp:" and the employee's id); null if something else is picked.
        private string Picked(string prefix)
        {
            return _pick != null && _pick.StartsWith(prefix, StringComparison.Ordinal) ? _pick.Substring(prefix.Length) : null;
        }

        // The same for a place and a kind ("pol:" and "place:2"); false if something else is picked.
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
            Guide(box);
        }

        private void Guide(InfoBox box)
        {
            Meta.Guide.Point(box, _tab == CompanyDirector.TabRota ? "reallife_meta_title_economy_rota"
                : _tab == CompanyDirector.TabManagers ? "reallife_meta_title_economy_managers" : "reallife_meta_title_economy_company");
        }

        private string WriteOverview(InfoBox box, CompanyState state, int day)
        {
            string selected = null;
            IReadOnlyList<CompanyShop> shops = _director.Shops;
            CompanyShop picked = null;
            TableFilter districts = CompanyTables.Overview.Filters[0];   // its kinds are the districts the businesses are in
            districts.Names.Clear();
            for (int i = 0; i < shops.Count; i++)
            {
                CompanyShop shop = shops[i];
                int waiting = ShopWaiting(state, shop, day);
                string district = CompanyText.District(shop.District);
                string id = Pick("shop:" + shop.File.Place, shop.Name, district, shop.HasBooks ? CompanyText.Money(shop.Profit) : CompanyText.T("reallife_co_no_books"),
                    waiting > 0 ? CompanyText.T("reallife_co_state_waiting", "count", waiting.ToString()) : CompanyText.T("reallife_co_state_in_order"),
                    waiting > 0 ? 2 : 0, ref selected);
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
            IReadOnlyList<CompanyAlert> alerts = _director.Alerts;
            if (alerts.Count > 0)
            {
                box.Chip(CompanyText.T("reallife_ibox_chip_waiting", "count", alerts.Count.ToString()), CellTone.Yellow);
            }
            box.Headline("reallife_co_over_alerts");
            for (int i = 0; i < alerts.Count && i < AlertsShown; i++)
            {
                box.Line("<b>" + alerts[i].Title + "</b> " + alerts[i].What, CellTone.Yellow);
            }
            if (alerts.Count == 0)
            {
                box.Note(CompanyText.T("reallife_co_over_no_alerts"));
            }
            else if (alerts.Count > AlertsShown)
            {
                box.Note(CompanyText.T("reallife_co_over_more", "count", (alerts.Count - AlertsShown).ToString()));
            }
            box.Headline("reallife_co_tab_overview");
            box.Row("reallife_co_over_businesses", shops.Count.ToString());
            box.Row("reallife_co_over_staff", state.Staff.Count.ToString());
            box.Row("reallife_co_over_quarter", CompanyText.T("reallife_co_over_quarter_value", "quarter", state.Tax.Quarter.ToString(), "day", state.Tax.QuarterFrom.ToString()));
            Transform others = ServicesDirector.CanClean || OwnBankDirector.CanOpen ? box.ButtonRow() : null;
            if (ServicesDirector.CanClean)
            {
                InfoBox.Button(others, InfoBox.Additional, "reallife_svc_open_cleaning", ServicesDirector.OpenCleaning);
            }
            if (OwnBankDirector.CanOpen)
            {
                InfoBox.Button(others, InfoBox.Positive, "reallife_ob_open", OwnBankDirector.OpenApp);
            }
            WriteListingLine(box);
            Guide(box);
            return null;
        }

        private const int AlertsShown = 4;   // alerts at the head of the Overview; the rest are counted in one line

        // The company and the city's market (Markets/Listing): where it stands, and a button to its page in the Stocks app.
        private static void WriteListingLine(InfoBox box)
        {
            string line = null;
            try
            {
                line = CompanyBridges.ListingLine != null ? CompanyBridges.ListingLine() : null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (string.IsNullOrEmpty(line) || CompanyBridges.OpenListing == null)
            {
                return;
            }
            box.Headline("reallife_lst_headline");
            box.Line(line);
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_lst_open_stocks", () => UiClone.Safe(CompanyBridges.OpenListing));
        }

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

        private void WriteShop(InfoBox box, CompanyState state, CompanyShop shop, int day)
        {
            ShopFile file = shop.File;
            box.Title(shop.Name);
            box.Subtitle(CompanyText.District(shop.District));
            int waiting = ShopWaiting(state, shop, day);
            box.Chip(waiting > 0 ? CompanyText.T("reallife_co_state_waiting", "count", waiting.ToString()) : CompanyText.T("reallife_co_state_in_order"),
                waiting > 0 ? CellTone.Yellow : CellTone.Green);
            if (_director.On(CompanyPart.Staff) && file.StrikeFrom > 0)
            {
                box.Line(CompanyText.T(file.Striking(day) ? "reallife_co_shop_strike_on" : "reallife_co_shop_strike_called", "day",
                    StaffRules.StrikeShown(file, day).ToString()), CellTone.Red);
            }
            box.Section("reallife_ibox_yesterday");
            if (shop.HasBooks)
            {
                box.RowPair("reallife_ibox_takings", CompanyText.Money(shop.Takings), "reallife_ibox_profit", CompanyText.Money(shop.Profit));
            }
            else
            {
                box.Note(CompanyText.T("reallife_co_shop_no_books"));
            }
            box.Section("reallife_ibox_sec_business");
            box.Row("reallife_co_shop_district", CompanyText.T("reallife_co_shop_district_value", "district", CompanyText.District(shop.District), "income", shop.Income.ToString()));
            if (_director.On(CompanyPart.Staff))
            {
                box.Line(shop.Staff > 0
                    ? CompanyText.T("reallife_co_shop_staff", "count", shop.Staff.ToString(), "standing", (shop.Standing / shop.Staff).ToString(), "low", shop.Low.ToString())
                    : CompanyText.T("reallife_co_shop_no_staff"), shop.Staff > 0 && shop.Low > 0 ? CellTone.Yellow : CellTone.Plain);
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
                box.Row("reallife_co_shop_cover", cover.Length > 0 ? cover : CompanyText.T("reallife_co_none"));
            }
            if (_director.On(CompanyPart.Marketing))
            {
                box.Row("reallife_co_shop_campaign", file.Push >= 0
                    ? CompanyText.T(file.Pushing(day) ? "reallife_co_shop_campaign_on" : "reallife_co_shop_campaign_booked", "campaign", CompanyText.Campaign(file.Push), "day",
                        (file.Pushing(day) ? file.PushEnd - 1 : file.PushFrom).ToString())
                    : CompanyText.T("reallife_co_none"));
            }
            if (_director.On(CompanyPart.Licences))
            {
                int missing = LicenceRules.Missing(file, shop.Needs, day);
                box.Row("reallife_co_shop_licences",
                    missing == 0 ? CompanyText.T("reallife_co_shop_licences_ok") : CompanyText.T("reallife_co_shop_licences_missing", "list", CompanyText.Licences(missing)),
                    missing == 0 ? CellTone.Green : CellTone.Red);
            }
            if (_director.On(CompanyPart.Tax) && file.Hide > 0)
            {
                box.Row("reallife_co_shop_hidden", file.Hide + "%", CellTone.Yellow);
            }
            BuildingRegistration reg = shop.Reg;   // the shop is the director's and is used again; the click keeps its own
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_co_open_bizman", () => OpenBizMan(reg));
            if (ServicesDirector.CanSell)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_svc_open_sale", () => ServicesDirector.OpenSale(file.Place));
            }
            BackButton(box);
        }

        // The business's own page in the game's BizMan (CompanyGame.OpenBizMan); the game's notice if it cannot be opened.
        private static void OpenBizMan(BuildingRegistration reg)
        {
            if (!CompanyGame.OpenBizMan(reg))
            {
                Notifications.ShowError("reallife_co_bizman_cannot", "reallife_co_bizman_cannot", false);
            }
        }
    }
}
