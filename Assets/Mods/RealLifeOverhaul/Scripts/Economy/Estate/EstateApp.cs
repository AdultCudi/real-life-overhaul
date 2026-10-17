using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using Streets;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Property app in the game's frame: seven tabs over the game's table, and a box for the one thing picked.
    /// Nothing runs while it is closed: a page is written when it opens, on a pick, or when the director says so.
    /// </summary>
    public sealed partial class EstateApp
    {
        public static IModLogger Logger;

        private readonly EstateDirector _director;
        private static readonly TableSpec[] Specs =
        {
            EstateTables.Market, EstateTables.Mine, EstateTables.Tenants, EstateTables.Works, EstateTables.Agency, EstateTables.Hotel, EstateTables.Hire
        };

        private readonly List<TableRow> _rows = new List<TableRow>(48);
        private readonly List<TableRow> _pool = new List<TableRow>(48);
        private readonly List<BuildingRegistration> _places = new List<BuildingRegistration>(16);   // the offices or lots to choose from

        private AppShell _shell;
        private DataTable _table;
        private int _tab, _wanted = -1;
        private string _pick, _wantPick;   // a row's id, its kind before the colon: "own:", "unt:", "app:", "ins:", "hot:", "car:" and so on
        private bool _ready;               // the director has read the game for this opening
        private bool _writing;             // a page is being written: a change it causes does not write it again

        public EstateApp(EstateDirector director)
        {
            _director = director;
        }

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each says so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("estate: the app being built from the game's MyEmployees");   // once per city: a few hundred objects in one frame
            _shell = AppShell.Make(menu, dormant, EstateDirector.AppId);
            _table = DataTable.On(_shell, "Property");
            if (_table == null || !_shell.SetTabCount(EstateTables.Tabs.Length) || !_shell.SetColumns(EstateTables.Off))
            {
                return null;
            }
            _table.Money = amount => EstateText.Money((float)amount);
            _table.SelectionLost = Back;   // a filter took the picked row out: the tab's own page
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < EstateTables.Tabs.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], EstateTables.Tabs[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>The tab and the row a clicked notification asks for: shown now if the app is open, else when it opens.</summary>
        public void Want(int tab, string pick)
        {
            _wanted = Mathf.Clamp(tab, 0, EstateTables.Tabs.Length - 1);
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
        }

        /// <summary>The director says something has changed: the page is written again if it is showing.</summary>
        public void Changed()
        {
            if (Showing && _ready)
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
            // A hotel and the lot are looked after on their own tabs.
            if (_tab == EstateDirector.TabMine && _pick.StartsWith("hot:", StringComparison.Ordinal))
            {
                _tab = EstateDirector.TabHotel;
            }
            else if (_tab == EstateDirector.TabMine && _pick == "lot:")
            {
                _tab = EstateDirector.TabHire;
                _pick = null;
            }
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
                EstateState state = _ready ? _director.State : null;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, EstateText.T("reallife_estate_app"));
                    box.Title(EstateText.T("reallife_estate_off_title"));
                    box.Note(EstateText.T("reallife_estate_off_body"));
                }
                else
                {
                    int day = SaveGameManager.Current.Day;
                    UiClone.Words(_shell.Header, EstateText.T("reallife_estate_headline", "buildings", state.Holdings.Count.ToString(), "waiting", _director.Waiting().ToString()));
                    switch (_tab)
                    {
                        case EstateDirector.TabMine:
                            WriteMine(box, state, ref selected);
                            break;
                        case EstateDirector.TabTenants:
                            if (PartPage(EstatePart.Property, box))
                            {
                                WriteTenants(box, state, day, ref selected);
                            }
                            break;
                        case EstateDirector.TabWorks:
                            if (PartPage(EstatePart.Property, box))
                            {
                                WriteWorks(box, state, ref selected);
                            }
                            break;
                        case EstateDirector.TabAgency:
                            if (PartPage(EstatePart.Property, box))
                            {
                                WriteAgency(box, state, ref selected);
                            }
                            break;
                        case EstateDirector.TabHotel:
                            if (PartPage(EstatePart.Hotel, box))
                            {
                                WriteHotel(box, state, day, ref selected);
                            }
                            break;
                        case EstateDirector.TabHire:
                            if (PartPage(EstatePart.Hire, box))
                            {
                                WriteHire(box, state, ref selected);
                            }
                            break;
                        default:
                            WriteMarket(box, state, ref selected);
                            break;
                    }
                }
                box.Done();
                _table.Show(state == null || SaveGameManager.Current == null ? EstateTables.Off : Specs[_tab], _rows, selected);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Estate: tab " + _tab + " written, " + _rows.Count + " row(s)" + (_pick != null ? ", picked " + _pick : string.Empty) + "; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms.");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // A tab whose part is switched off, or stopped by a fault, says so and shows nothing else.
        private bool PartPage(EstatePart part, InfoBox box)
        {
            if (_director.On(part))
            {
                return true;
            }
            box.Title(EstateText.T(EstateTables.Tabs[_tab]));
            box.Line(EstateText.T("reallife_estate_part_off"), CellTone.Yellow);
            Meta.Guide.Point(box, GuideTitle);
            Say("off", EstateText.T("reallife_estate_part_off_row"));
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
            row.Reset(id);
            _rows.Add(row);
            return row;
        }

        // A row that can be picked. True if it is the one picked now.
        private bool Pick(string id, ref string selected, out TableRow row)
        {
            row = Row(id);
            row.Pick = true;
            if (id == _pick)
            {
                selected = id;
                return true;
            }
            return false;
        }

        private void Say(string id, string words)
        {
            TableRow row = Row(id);
            row.Info = true;
            row.A = words;
        }

        // The place of a name among a filter's kinds (the districts of the rows shown), added if it is new.
        private static int KindOf(List<string> names, string name)
        {
            int at = names.IndexOf(name);
            if (at < 0)
            {
                at = names.Count;
                names.Add(name);
            }
            return at;
        }

        // What follows a prefix in what is picked; null if something else is picked.
        private string Picked(string prefix)
        {
            return _pick != null && _pick.StartsWith(prefix, StringComparison.Ordinal) ? _pick.Substring(prefix.Length) : null;
        }

        private const string GuideTitle = "reallife_meta_title_economy_estate";   // the Guide's page every page points to at its foot

        private void BackButton(Transform buttons)
        {
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
        }

        // A previous/next selector over a share, in steps.
        private static void Steps(InfoBox box, int least, int most, int step, int value, Action<int> changed)
        {
            int count = (most - least) / step + 1;
            if (!box.HasSelector || !box.Selector(count, (EstateCatalogue.Clamp(value, least, most) - least) / step, i => EstateText.Share(least + i * step), i => changed(least + i * step)))
            {
                box.Line(EstateText.Share(value));
            }
        }

        private void WriteMarket(InfoBox box, EstateState state, ref string selected)
        {
            EstateClimate c = _director.Climate;
            GameInstance save = SaveGameManager.Current;
            CityBuilding picked = null;
            HotelSlot pickedHotel = null;
            bool property = _director.On(EstatePart.Property);
            List<string> districts = EstateTables.Market.Filters[0].Names;   // the districts of what is for sale
            districts.Clear();
            TableRow row;
            for (int i = 0; property && save.buildingsForSale != null && i < save.buildingsForSale.Count; i++)
            {
                BuildingForSale f = save.buildingsForSale[i];
                CityBuilding b = f != null && f.address != null ? EstateGame.Find(EstateGame.Place(f.address.streetName, f.address.streetNumber)) : null;
                if (b == null || b.Holding != null || b.B == null || b.B.IsHamptonsHouse())
                {
                    continue;
                }
                int cond = EstateRules.StreetCondition(state.Seed, b.Key), use = EstateGame.UseOf(b.B.BuildingType);
                int rent = EstateRules.MarketRent(b.Base, use, Mathf.RoundToInt(CityEconomy.DistrictIncome(b.District) * 10f), cond, 0, c);
                string district = EstateText.District(b.District);
                if (Pick("mkt:" + b.Place, ref selected, out row))
                {
                    picked = b;
                }
                row.Kind = KindOf(districts, district);
                row.Kind2 = use;
                row.Set(0, f.address.ToFormattedString());
                row.Set(1, district);
                row.Set(2, EstateText.Use(use));
                row.Set(3, EstateText.Money(f.buildingPrice), f.buildingPrice);
                row.Set(4, EstateText.Level(cond), cond, EstateRules.RunDown(cond) ? TableRow.Yellow : TableRow.Plain);
                if (rent > 0 && f.buildingPrice > 0f)
                {
                    row.Set(5, EstateText.Money(rent), rent);
                    row.Set(6, EstateText.Yield((int)((long)rent * c.DaysPerYear * 10000 / (long)f.buildingPrice)),
                        (double)rent * c.DaysPerYear * 100.0 / f.buildingPrice);
                }
            }
            for (int i = 0; _director.On(EstatePart.Hotel) && i < TravelCatalogue.Hotels.Length; i++)
            {
                HotelSlot slot = TravelCatalogue.Hotels[i];
                if (state.Hotel(slot.Id) != null)
                {
                    continue;
                }
                int price = EstateGame.HotelPrice(slot, state.Real, c.DaysPerYear);
                string district = EstateText.District(slot.Neighbourhood);
                if (Pick("htl:" + slot.Id, ref selected, out row))
                {
                    pickedHotel = slot;
                }
                row.Kind = KindOf(districts, district);
                row.Kind2 = EstateTables.IsHotel;
                row.Set(0, EstateGame.HotelName(slot));
                row.Set(1, district);
                row.Set(2, EstateText.T("reallife_estate_row_hotel"));
                row.Set(3, EstateText.Money(price), price);
                row.Set(4, EstateText.T("reallife_estate_row_going_concern"));
            }
            if (_rows.Count == 0)
            {
                Say("none", EstateText.T("reallife_estate_row_no_market"));
            }
            if (picked != null)
            {
                WriteListing(box, state, picked, c);
            }
            else if (pickedHotel != null)
            {
                WriteHotelForSale(box, state, pickedHotel, c);
            }
            else
            {
                box.Title(EstateText.T("reallife_estate_tab_market"));
                box.Chip(EstateText.Cycle(c.Cycle));
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_estate_market_level", EstateText.Share(state.Real));
                box.Line(EstateText.T("reallife_estate_market_heading", "cycle", EstateText.Cycle(c.Cycle), "target", EstateText.Share(EstateRules.RealTarget(c.Cycle, c.Quarters))));
                box.Row("reallife_estate_market_prices", EstateText.Share(c.Index));
                box.Section("reallife_ibox_sec_how");
                box.Note(EstateText.T("reallife_estate_market_what"));
                box.Note(EstateText.T("reallife_estate_market_rents"));
                box.Note(EstateText.T("reallife_estate_market_mortgage"));
                Meta.Guide.Point(box, GuideTitle);
            }
        }

        // One of the game's listings: what makes its price, and the way to the game's own purchase screen.
        private void WriteListing(InfoBox box, EstateState state, CityBuilding b, EstateClimate c)
        {
            int cond = EstateRules.StreetCondition(state.Seed, b.Key), income = Mathf.RoundToInt(CityEconomy.DistrictIncome(b.District) * 10f);
            int use = EstateGame.UseOf(b.B.BuildingType);
            int value = EstateGame.Value(b, state, c.Index);
            int rent = EstateRules.MarketRent(b.Base, use, income, cond, 0, c);
            string place = b.Place;
            box.Title(EstateGame.NameOf(place));
            box.Subtitle(EstateText.T("reallife_ibox_type_in", "type", EstateText.Use(use), "district", EstateText.District(b.District)));
            box.Chip(EstateText.Level(cond), EstateRules.RunDown(cond) ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_estate_listing_price", EstateText.Money(value));
            box.Section("reallife_ibox_sec_price_made");
            box.Line(EstateText.T("reallife_estate_listing_base", "amount", EstateText.Money(b.Base)));
            box.Line(EstateText.T("reallife_estate_listing_market", "market", EstateText.Share(state.Real), "prices", EstateText.Share(c.Index)));
            box.Line(EstateText.T("reallife_estate_listing_district", "district", EstateText.District(b.District), "share", EstateText.Share(EstateRules.DistrictValue(income))));
            box.Line(EstateText.T(EstateRules.RunDown(cond) ? "reallife_estate_listing_run_down" : "reallife_estate_listing_kept", "condition", EstateText.Level(cond),
                "share", EstateText.Share(EstateRules.ConditionValue(cond)), "amount", EstateText.Money(EstateRules.RepairCost(b.Base, cond, c.Index))),
                EstateRules.RunDown(cond) ? CellTone.Yellow : CellTone.Plain);
            box.Section("reallife_ibox_sec_letting");
            box.Line(rent > 0
                ? EstateText.T("reallife_estate_listing_rent", "use", EstateText.Use(use), "amount", EstateText.Money(rent),
                    "yield", EstateText.Yield((int)((long)rent * c.DaysPerYear * 10000 / Math.Max(1, value))))
                : EstateText.T("reallife_estate_listing_unfit"));
            if (b.Units > 1 && rent > 0)
            {
                box.Line(EstateText.T("reallife_estate_listing_units", "units", b.Units.ToString(), "amount", EstateText.Money(Math.Max(1, rent / b.Units))));
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_offer", () => _director.Buy(place));
            BackButton(buttons);
            box.Note(EstateText.T("reallife_estate_listing_how"));
            Meta.Guide.Point(box, GuideTitle);
        }

        private void WriteHotelForSale(InfoBox box, EstateState state, HotelSlot slot, EstateClimate c)
        {
            int tier = (int)slot.Tier;
            int price = EstateGame.HotelPrice(slot, state.Real, c.DaysPerYear);
            string id = slot.Id;
            box.Title(EstateGame.HotelName(slot));
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_estate_listing_price", EstateText.Money(price));
            box.Line(EstateText.Fill("reallife_estate_hotel_rooms", "count0", HotelRules.Rooms(tier, 0).ToString(), "room0", EstateGame.RoomName(tier, 0),
                "count1", HotelRules.Rooms(tier, 1).ToString(), "room1", EstateGame.RoomName(tier, 1)));
            box.Note(EstateText.T("reallife_estate_hotel_price_what", "years", (EstateCatalogue.HotelMultiple / 1000f).ToString("0.#"), "share", EstateText.Share(EstateCatalogue.Occupancy),
                "market", EstateText.Share(state.Real)));
            box.Note(EstateText.T("reallife_estate_hotel_lease_what", "deposit", EstateText.Money(HotelRules.LeaseDeposit(tier, price, c.DaysPerYear)),
                "amount", EstateText.Money(HotelRules.LeaseDaily(tier, price, c.DaysPerYear))));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_buy_hotel", () => _director.TakeHotel(id, false));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_estate_btn_lease_hotel", () => _director.TakeHotel(id, true));
            BackButton(box.ButtonRow());
            box.Note(EstateText.T("reallife_estate_hotel_held_what"));
            Meta.Guide.Point(box, GuideTitle);
        }
    }
}
