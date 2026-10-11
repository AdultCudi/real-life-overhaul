using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using Streets;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the Property app shows, in the game's frame (GameUi.AppShell, copied from MyEmployees) as Finance,
    /// Ventures and Company are, with Finance's table (FinanceTable, the game's own BaTable). Seven tabs over the
    /// table on the right, and on the left the white box with the one thing picked:
    ///
    ///   Market      the game's own listings at the mod's prices, and the city's hotels; pick one for what makes
    ///               its price and the button that opens the game's purchase screen
    ///   Mine        every building owned, the hotels held and the hire lot; pick a building to let it yourself,
    ///               insure it, put it up for sale or answer an offer
    ///   Tenants     who is in each building the player lets, and who has applied; pick one to answer, set the
    ///               asking rent, serve notice or go to court
    ///   Works       each building's condition and level; pick one for repairs, a refurbishment or a change of use
    ///   Agency      the office, the negotiators and the instructions; pick one to take it on or decline
    ///   Hotel       each hotel's night; pick one for its rates, its staff, a campaign, a group that asks
    ///   Car hire    the lot and its cars; pick a car for its rate, or a vehicle on the lot to put it on the books
    ///
    /// A list goes in the table, which scrolls; the white box, which does not, shows only the one thing picked and
    /// says in sentences where each figure comes from. Each tab's page is in its own file.
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and the page are made when it
    /// opens, when a tab or a row is picked, and when the director says something has changed.
    /// </summary>
    public sealed partial class EstateApp
    {
        public static IModLogger Logger;

        private readonly EstateDirector _director;
        private readonly List<FinanceRow> _rows = new List<FinanceRow>(48);
        private readonly List<FinanceRow> _pool = new List<FinanceRow>(48);

        private AppShell _shell;
        private FinanceTable _table;
        private int _tab, _wanted = -1;
        private string _pick, _wantPick;   // what is picked: a row's id ("own:...", "app:...", "ins:...", "hot:...", "car:...")
        private bool _ready;               // the director has read the game for this opening
        private bool _writing;             // a page is being written: a change it causes does not write it again

        public EstateApp(EstateDirector director)
        {
            _director = director;
        }

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            SessionWatch.Mark("estate: the app being built from the game's MyEmployees");   // once per city: a few hundred objects copied in one frame
            _shell = AppShell.Make(menu, dormant, EstateDirector.AppId);
            if (_shell == null || !PrepareRow(_shell.RowPrefab) || !_shell.SetTabCount(EstateColumns.Tabs.Length))
            {
                return null;
            }
            if (!_shell.SetColumns(EstateColumns.Names, EstateColumns.Titles[0], EstateColumns.Widths, EstateColumns.Flexible))
            {
                return null;
            }
            FinanceCellView.SelectedColour = _shell.RowSelected;
            _table = _shell.List.gameObject.AddComponent<FinanceTable>();
            _table.scroller = _shell.Scroller;
            _table.prefab = _shell.RowPrefab.AddComponent<FinanceCellView>();
            _table.allowRowSelection = true;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < EstateColumns.Tabs.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], EstateColumns.Tabs[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        // The game's employee row has a tick box and ten cells. Four are kept and renamed, as Finance and the Planner
        // keep them: the name, the task (its warning sign hidden), the business and the satisfaction.
        private static bool PrepareRow(GameObject row)
        {
            Transform t = row.transform;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            Transform a = t.Find("EmployeeName"), b = t.Find("TaskInfo"), c = t.Find("BusinessName"), d = t.Find("Satisfaction");
            if (layout == null || a == null || b == null || c == null || d == null || b.Find("Task") == null)
            {
                UiClone.Fallback("the game's employee row is not built as expected, so the Property app is not added");
                return false;
            }
            UiClone.KeepOnly(t, "Background", "RaycastTarget", "EmployeeName", "TaskInfo", "BusinessName", "Satisfaction");
            a.name = "A";
            b.name = "B";
            c.name = "C";
            d.name = "D";
            AppShell.RowLayout(layout);
            AppShell.Width(a, EstateColumns.WidthA, false);
            AppShell.Width(b, EstateColumns.WidthB, true);
            AppShell.Width(c, EstateColumns.WidthC, false);
            AppShell.Width(d, EstateColumns.WidthD, false);
            AppShell.OneLine(a.GetComponent<TMP_Text>());
            AppShell.OneLine(c.GetComponent<TMP_Text>());
            AppShell.OneLine(d.GetComponent<TMP_Text>());
            AppShell.OneLine(b.Find("Task").GetComponent<TMP_Text>());
            Transform sign = b.Find("WarningIcon");
            if (sign != null)
            {
                sign.gameObject.SetActive(false);
            }
            return true;
        }

        // ---------------------------------------------------------------- opening and the tabs

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>The tab to show the next time the app opens, and what is picked on it (a notification was clicked).</summary>
        public void Want(int tab, string pick)
        {
            _wanted = Mathf.Clamp(tab, 0, EstateColumns.Tabs.Length - 1);
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

        private void RowClicked(FinanceRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            _pick = row.Id;
            // A hotel and the lot are looked after on their own tabs: a click on one in the list of what is owned goes there.
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
                _shell.SetColumns(EstateColumns.Names, EstateColumns.Titles[_tab], EstateColumns.Widths, EstateColumns.Flexible);
                InfoBox box = _shell.Box;
                box.Clear();
                string selected = null;
                EstateState state = _ready ? _director.State : null;
                if (state == null || SaveGameManager.Current == null)
                {
                    UiClone.Words(_shell.Header, EstateText.T("reallife_estate_app"));
                    box.Title(EstateText.T("reallife_estate_off_title"));
                    box.Line(EstateText.T("reallife_estate_off_body"));
                }
                else
                {
                    int day = SaveGameManager.Current.Day;
                    UiClone.Words(_shell.Header, EstateText.T("reallife_estate_headline", "buildings", state.Holdings.Count.ToString(), "waiting", _director.Waiting().ToString()));
                    switch (_tab)
                    {
                        case EstateDirector.TabMine:
                            WriteMine(box, state, day, ref selected);
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
                                WriteWorks(box, state, day, ref selected);
                            }
                            break;
                        case EstateDirector.TabAgency:
                            if (PartPage(EstatePart.Property, box))
                            {
                                WriteAgency(box, state, day, ref selected);
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
                                WriteHire(box, state, day, ref selected);
                            }
                            break;
                        default:
                            WriteMarket(box, state, day, ref selected);
                            break;
                    }
                }
                box.Done();
                _table.Show(_rows, selected);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Estate: tab " + _tab + " written, " + _rows.Count + " row(s)" + (_pick != null ? ", picked " + _pick : string.Empty) + "; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
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
            box.Title(EstateText.T(EstateColumns.Tabs[_tab]));
            box.Line(EstateText.T("reallife_estate_part_off"));
            Say("off", EstateText.T("reallife_estate_part_off_row"));
            return false;
        }

        // ---------------------------------------------------------------- rows

        private FinanceRow Row(string id)
        {
            FinanceRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new FinanceRow();
            }
            row.Id = id;
            row.A = row.B = row.C = row.D = null;
            row.Info = row.Pick = false;
            row.Index = 0;
            row.Tone = 0;
            _rows.Add(row);
            return row;
        }

        // A row that can be picked. True if it is the one picked now.
        private bool Pick(string id, string a, string b, string c, string d, int tone, ref string selected)
        {
            FinanceRow row = Row(id);
            row.Pick = true;
            row.A = a;
            row.B = b;
            row.C = c;
            row.D = d;
            row.Tone = tone;
            if (id == _pick)
            {
                selected = id;
                return true;
            }
            return false;
        }

        private void Say(string id, string words)
        {
            FinanceRow row = Row(id);
            row.Info = true;
            row.A = words;
        }

        // What follows a prefix in what is picked ("own:" and the place); null if something else is picked.
        private string Picked(string prefix)
        {
            return _pick != null && _pick.StartsWith(prefix, StringComparison.Ordinal) ? _pick.Substring(prefix.Length) : null;
        }

        private void BackButton(Transform buttons)
        {
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
        }

        // A previous/next selector over a rate against the going one, in steps: 60% to 160%, or the asking rent's 80% to 130%.
        private static void Steps(InfoBox box, int least, int most, int step, int value, Action<int> changed)
        {
            int count = (most - least) / step + 1;
            if (!box.HasSelector || !box.Selector(count, (EstateCatalogue.Clamp(value, least, most) - least) / step, i => EstateText.Share(least + i * step), i => changed(least + i * step)))
            {
                box.Line(EstateText.Share(value));
            }
        }

        // ---------------------------------------------------------------- Market

        private readonly List<BuildingForSale> _listed = new List<BuildingForSale>(32);

        private void WriteMarket(InfoBox box, EstateState state, int day, ref string selected)
        {
            EstateClimate c = _director.Climate;
            GameInstance save = SaveGameManager.Current;
            CityBuilding picked = null;
            HotelSlot pickedHotel = null;
            bool property = _director.On(EstatePart.Property);
            _listed.Clear();
            for (int i = 0; property && save.buildingsForSale != null && i < save.buildingsForSale.Count; i++)
            {
                BuildingForSale f = save.buildingsForSale[i];
                CityBuilding b = f != null && f.address != null ? EstateGame.Find(EstateGame.Place(f.address.streetName, f.address.streetNumber)) : null;
                if (b == null || b.Holding != null || b.B == null || b.B.IsHamptonsHouse())
                {
                    continue;
                }
                int cond = EstateRules.StreetCondition(state.Seed, b.Key);
                if (Pick("mkt:" + b.Place, f.address.ToFormattedString(), EstateText.District(b.District) + ", " + EstateText.Use(EstateGame.UseOf(b.B.BuildingType)), EstateText.Money(f.buildingPrice),
                        EstateText.Level(cond), cond < EstateCatalogue.RunDownMost ? 1 : 0, ref selected))
                {
                    picked = b;
                }
            }
            for (int i = 0; _director.On(EstatePart.Hotel) && i < TravelCatalogue.Hotels.Length; i++)
            {
                HotelSlot slot = TravelCatalogue.Hotels[i];
                if (state.Hotel(slot.Id) != null)
                {
                    continue;
                }
                if (Pick("htl:" + slot.Id, EstateGame.HotelName(slot), EstateText.District(slot.Neighbourhood) + ", " + EstateText.T("reallife_estate_row_hotel"),
                        EstateText.Money(EstateGame.HotelPrice(slot, state.Real, c.DaysPerYear)), EstateText.T("reallife_estate_row_going_concern"), 0, ref selected))
                {
                    pickedHotel = slot;
                }
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
                box.Line(EstateText.T("reallife_estate_market_what"));
                box.Line(EstateText.Pair("reallife_estate_market_level", EstateText.Share(state.Real)));
                box.Line(EstateText.T("reallife_estate_market_heading", "cycle", EstateText.Cycle(c.Cycle), "target", EstateText.Share(EstateRules.RealTarget(c.Cycle, c.Quarters))));
                box.Line(EstateText.Pair("reallife_estate_market_prices", EstateText.Share(c.Index)));
                box.Line(EstateText.T("reallife_estate_market_rents"));
                box.Line(EstateText.T("reallife_estate_market_mortgage"));
            }
        }

        // One of the game's listings: what makes its price, in sentences, and the way to the game's own purchase screen.
        private void WriteListing(InfoBox box, EstateState state, CityBuilding b, EstateClimate c)
        {
            int cond = EstateRules.StreetCondition(state.Seed, b.Key), income = Mathf.RoundToInt(CityEconomy.DistrictIncome(b.District) * 10f), use = EstateGame.UseOf(b.B.BuildingType);
            int value = EstateGame.Value(b, state, c.Index);
            int rent = EstateRules.MarketRent(b.Base, use, income, cond, 0, c);
            string place = b.Place;
            box.Title(EstateGame.NameOf(place));
            box.Line(EstateText.Pair("reallife_estate_listing_price", EstateText.Money(value)));
            box.Line(EstateText.T("reallife_estate_listing_base", "amount", EstateText.Money(b.Base)));
            box.Line(EstateText.T("reallife_estate_listing_market", "market", EstateText.Share(state.Real), "prices", EstateText.Share(c.Index)));
            box.Line(EstateText.T("reallife_estate_listing_district", "district", EstateText.District(b.District), "share", EstateText.Share(EstateRules.DistrictValue(income))));
            box.Line(EstateText.T(cond < EstateCatalogue.RunDownMost ? "reallife_estate_listing_run_down" : "reallife_estate_listing_kept", "condition", EstateText.Level(cond), "share", EstateText.Share(EstateRules.ConditionValue(cond)),
                "amount", EstateText.Money(EstateRules.RepairCost(b.Base, cond, c.Index))));
            box.Line(rent > 0
                ? EstateText.T("reallife_estate_listing_rent", "use", EstateText.Use(use), "amount", EstateText.Money(rent), "yield", EstateText.Yield((int)((long)rent * c.DaysPerYear * 10000 / Math.Max(1, value))))
                : EstateText.T("reallife_estate_listing_unfit"));
            box.Line(EstateText.T("reallife_estate_listing_how"));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_offer", () => _director.Buy(place));
            BackButton(buttons);
        }

        private void WriteHotelForSale(InfoBox box, EstateState state, HotelSlot slot, EstateClimate c)
        {
            int tier = (int)slot.Tier;
            int price = EstateGame.HotelPrice(slot, state.Real, c.DaysPerYear);
            string id = slot.Id;
            box.Title(EstateGame.HotelName(slot));
            box.Line(EstateText.Fill("reallife_estate_hotel_rooms", "count0", HotelRules.Rooms(tier, 0).ToString(), "room0", EstateGame.RoomName(tier, 0), "count1", HotelRules.Rooms(tier, 1).ToString(),
                "room1", EstateGame.RoomName(tier, 1)));
            box.Line(EstateText.Pair("reallife_estate_listing_price", EstateText.Money(price)));
            box.Line(EstateText.T("reallife_estate_hotel_price_what", "years", (EstateCatalogue.HotelMultiple / 1000f).ToString("0.#"), "share", EstateText.Share(EstateCatalogue.Occupancy), "market", EstateText.Share(state.Real)));
            box.Line(EstateText.T("reallife_estate_hotel_lease_what", "deposit", EstateText.Money(HotelRules.LeaseDeposit(tier, price, c.DaysPerYear)), "amount", EstateText.Money(HotelRules.LeaseDaily(tier, price, c.DaysPerYear))));
            box.Line(EstateText.T("reallife_estate_hotel_held_what"));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_buy_hotel", () => _director.TakeHotel(id, false));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_estate_btn_lease_hotel", () => _director.TakeHotel(id, true));
            BackButton(box.ButtonRow());
        }
    }
}
