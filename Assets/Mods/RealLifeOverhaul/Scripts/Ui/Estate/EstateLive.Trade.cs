using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using Extensions;
using Streets;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class EstateLive
    {
        private sealed partial class Page
        {
            // ------------------------------------------------------------------------------------------------- hotels

            public void Hotel()
            {
                string slot = Picked(_v.Pick, "hot:");
                HotelFile picked = slot != null ? _s.Hotel(slot) : null;
                if (picked == null && _s.Hotels.Count == 1 && _v.Pick == null)
                {
                    picked = _s.Hotels[0];   // one hotel: its page is the tab's page
                }
                if (picked != null)
                {
                    HotelPage(picked);
                    return;
                }
                _v.Pick = null;
                var rows = new List<UiDataRow>(4);
                var keys = new List<string>(4);
                for (int i = 0; i < _s.Hotels.Count; i++)
                {
                    HotelFile f = _s.Hotels[i];
                    int rooms = HotelRules.Rooms(f.Tier), sold = f.Sold[0] + f.Sold[1];
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { f.Name, EstateText.T("reallife_estate_row_sold", "sold", sold.ToString(Inv), "rooms", rooms.ToString(Inv)), Money(f.Takings), Money(f.Costs), Money(f.TakingsYear), Money(f.CostsYear) },
                        Values = new[] { (int)UiIcon.Building, 0, 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, f.Takings < f.Costs ? UiRole.Danger : UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.T("reallife_ibox_stars", "stars", HotelRules.Stars(f.Rating).ToString(Inv)), null, null, null, null, null }
                    });
                    keys.Add("hot:" + f.Slot);
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_hotel"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_rooms"), Kind = UiCellKind.Text, Width = 144f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_takings"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_costs"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_takings_year"), Kind = UiCellKind.Money, Width = 128f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_costs_year"), Kind = UiCellKind.Money, Width = 128f }
                };
                var side = new UiFactCardModel
                {
                    Title = EstateText.T("reallife_estate_tab_hotel"), Heading = true,
                    Lines = new[]
                    {
                        UiFactLine.Say(EstateText.T("reallife_estate_hotel_what")), UiFactLine.Say(EstateText.T("reallife_estate_hotel_night_what", "share", EstateText.Share(EstateCatalogue.Occupancy))),
                        UiFactLine.Say(EstateText.T("reallife_estate_hotel_staff_what")), UiFactLine.Say(EstateText.T("reallife_estate_hotel_held_what"))
                    }
                };
                TableShape(UiIcon.Building, EstateText.T("reallife_estate_tab_hotel"), EstateText.T("reallife_ui_estate_hotels_sub", "count", _s.Hotels.Count.ToString(Inv)), null, null, cols, rows, keys, side,
                    EstateText.T("reallife_estate_row_no_hotel"), EstateText.T("reallife_ui_estate_empty_hotel_body"));
            }

            private void HotelPage(HotelFile f)
            {
                string slot = f.Slot;
                int rooms = HotelRules.Rooms(f.Tier), sold = f.Sold[0] + f.Sold[1];
                _m.Shape = EstateShape.Detail;
                _m.Back = _s.Hotels.Count > 1 ? EstateText.T("reallife_co_back") : null;
                _m.DetailIcon = UiIcon.Building;
                _m.DetailTitle = f.Name;
                _m.DetailSub = EstateText.T(f.Leased ? "reallife_ibox_chip_leased" : "reallife_estate_row_hotel_owned");
                _m.DetailChip = EstateText.T("reallife_ibox_stars", "stars", HotelRules.Stars(f.Rating).ToString(Inv));
                _m.DetailChipTone = UiRole.Notice;
                var money = new List<UiFactLine>(8);
                if (f.AskRooms > 0)
                {
                    money.Add(UiFactLine.Say(EstateText.Fill("reallife_estate_hotel_group", "rooms", f.AskRooms.ToString(Inv), "nights", f.AskNights.ToString(Inv), "rate", Money(f.AskRate),
                        "amount", Money(f.AskRooms * f.AskNights * f.AskRate), "day", f.AskLapse.ToString(Inv)), UiRole.OkText));
                }
                else if (f.GroupNights > 0)
                {
                    money.Add(UiFactLine.Say(EstateText.T("reallife_estate_hotel_group_in", "rooms", f.GroupRooms.ToString(Inv), "nights", f.GroupNights.ToString(Inv), "rate", Money(f.GroupRate))));
                }
                if (f.OutEnd >= _day && f.OutRooms > 0)
                {
                    money.Add(UiFactLine.Say(EstateText.T("reallife_estate_hotel_out", "rooms", f.OutRooms.ToString(Inv), "day", f.OutEnd.ToString(Inv)), UiRole.WarnText));
                }
                money.Add(UiFactLine.Bar(EstateText.T("reallife_ibox_rooms_sold"), rooms > 0 ? sold * 100 / rooms : 0));
                money.Add(UiFactLine.Say(EstateText.T("reallife_estate_hotel_last", "sold", sold.ToString(Inv), "rooms", rooms.ToString(Inv), "takings", Money(f.Takings)) + " "
                    + EstateText.T(f.Leased ? "reallife_estate_hotel_last_leased" : "reallife_estate_hotel_last_owned", "costs", Money(f.Costs), "lease", Money(f.LeaseDaily))));
                money.Add(UiFactLine.Say(EstateText.T("reallife_estate_hotel_year", "takings", Money(f.TakingsYear), "costs", Money(f.CostsYear))));
                for (int r = 0; r < f.Reviews.Length; r++)
                {
                    if (f.Reviews[r] != 0)
                    {
                        money.Add(UiFactLine.Say(EstateText.Review(f.Reviews[r])));
                    }
                }
                var actions = new List<UiActionModel>(3);
                actions.Add(_runs.Button(EstateText.T(f.Leased ? "reallife_estate_btn_end_lease" : "reallife_estate_btn_sell_hotel"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count, () => Show(() => _d.GiveUpHotel(slot))));
                if (f.AskRooms > 0)
                {
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_refuse"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => _d.AnswerGroup(slot, false))));
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_accept"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => _d.AnswerGroup(slot, true))));
                }
                var rooms2 = new List<UiFactLine>(8);
                rooms2.Add(UiFactLine.Say(EstateText.T("reallife_estate_hotel_rating", "stars", HotelRules.Stars(f.Rating).ToString(Inv), "clean", EstateText.Level(f.Clean),
                    "target", EstateText.Level(HotelRules.RatingTarget(f.Clean, f.Staff, (f.Rate[0] + f.Rate[1]) / 2)))));
                for (int type = 0; type < EstateCatalogue.RoomTypes; type++)
                {
                    int t = type, rate = f.Rate[type];
                    int steps = (EstateCatalogue.RateMost - EstateCatalogue.RateLeast) / EstateCatalogue.RateStep;
                    rooms2.Add(_runs.Step(EstateGame.RoomName(f.Tier, type), EstateText.Money(HotelRules.Rate(f.Going[type], rate)),
                        EstateText.Fill("reallife_estate_hotel_rate", "room", EstateGame.RoomName(f.Tier, type), "count", HotelRules.Rooms(f.Tier, type).ToString(Inv), "amount", Money(HotelRules.Rate(f.Going[type], rate)),
                            "going", Money(f.Going[type]), "share", EstateText.Share(HotelRules.RateDemand(rate))), rate > EstateCatalogue.RateLeast, rate < EstateCatalogue.RateMost,
                        () => Show(() => _d.SetRoomRate(slot, t, rate - EstateCatalogue.RateStep)), () => Show(() => _d.SetRoomRate(slot, t, rate + EstateCatalogue.RateStep))) );
                    steps = steps + 0;
                }
                int staff = f.Staff;
                rooms2.Add(_runs.Step(EstateText.T("reallife_estate_hotel_staff"), EstateText.Staff(staff),
                    EstateText.T("reallife_estate_hotel_staff_cost", "amount", Money(HotelRules.Costs(f.Tier, staff, sold, _c.Index)), "clean", EstateText.Level(EstateCatalogue.CleanTarget[staff]), "day", f.InspectDay.ToString(Inv)),
                    staff > 0, staff < EstateCatalogue.StaffLevels - 1, () => Show(() => _d.SetHotelStaff(slot, staff - 1)), () => Show(() => _d.SetHotelStaff(slot, staff + 1))));
                rooms2.Add(UiFactLine.Section(EstateText.T("reallife_ibox_sec_campaign")));
                rooms2.Add(UiFactLine.Say(f.PushEnd >= _day
                    ? EstateText.T("reallife_estate_hotel_campaign_on", "day", f.PushEnd.ToString(Inv), "share", EstateText.Share(EstateCatalogue.CampaignDemand))
                    : EstateText.T("reallife_estate_hotel_campaign", "amount", Money(HotelRules.CampaignCost(f.Tier, _c.Index)), "days", EstateCatalogue.CampaignDays.ToString(Inv), "share", EstateText.Share(EstateCatalogue.CampaignDemand))));
                var campaign = new List<UiActionModel>(1);
                if (f.PushEnd < _day)
                {
                    campaign.Add(_runs.Button(EstateText.T("reallife_estate_btn_campaign"), UiButtonKind.Secondary, UiIcon.Megaphone, UiAction.Count, () => Show(() => _d.HotelCampaign(slot))));
                }
                rooms2.Add(UiFactLine.Say(EstateText.T("reallife_estate_hotel_rating_what", "share", EstateText.Share(HotelRules.RatingDemand(f.Rating)))));
                _m.Left = new[] { new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_money"), Heading = true, Lines = money.ToArray(), Actions = actions.ToArray() } };
                _m.Right = new[] { new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_rooms"), Heading = true, Lines = rooms2.ToArray(), Actions = campaign.ToArray() } };
            }

            // ------------------------------------------------------------------------------------------------- car hire

            public void Hire()
            {
                HireFile hire = _s.Hire;
                string carPick = Picked(_v.Pick, "car:");
                HireCar picked = carPick != null ? hire.Car(carPick) : null;
                if (picked != null)
                {
                    CarPage(hire, picked);
                    return;
                }
                string addPick = Picked(_v.Pick, "add:");
                var vehicles = new List<VehicleInstance>(8);
                if (hire.Running)
                {
                    EstateGame.Hireable(hire, vehicles);
                }
                VehicleInstance add = null;
                for (int i = 0; addPick != null && i < vehicles.Count; i++)
                {
                    add = vehicles[i].id == addPick ? vehicles[i] : add;
                }
                if (add != null)
                {
                    AddCarPage(hire, add);
                    return;
                }
                _v.Pick = null;
                var rows = new List<UiDataRow>(12);
                var keys = new List<string>(12);
                for (int i = 0; i < hire.Cars.Count; i++)
                {
                    HireCar car = hire.Cars[i];
                    int rate = HireRules.Rate(car.Price, car.Rate, _c.Index), carClass = HireRules.Class(car.Price);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { EstateGame.CarName(car.Type), EstateText.CarState(car.State), Money(rate), car.Hires.ToString(Inv), Money(car.Takings), car.State == (int)HireState.Out ? EstateText.T("reallife_estate_row_until", "day", car.Day.ToString(Inv)) : "-" },
                        Values = new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 }, Tones = new[] { UiRole.None, car.State == (int)HireState.Waiting ? UiRole.Warn : car.State == (int)HireState.Out ? UiRole.Ok : UiRole.Notice, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.CarClass(carClass), null, null, null, null, null }
                    });
                    keys.Add("car:" + car.Id);
                }
                for (int i = 0; i < vehicles.Count; i++)
                {
                    VehicleInstance v = vehicles[i];
                    int price = EstateGame.CarPrice(v), going = HireRules.Going(price, _c.Index);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { EstateGame.CarName(v.vehicleTypeName), EstateText.T("reallife_estate_row_on_lot"), Money(going), "-", "-", "-" },
                        Values = new[] { (int)UiIcon.Car, 0, 0, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.CarClass(HireRules.Class(price)), null, null, null, null, null }
                    });
                    keys.Add("add:" + v.id);
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_car"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_state"), Kind = UiCellKind.Chip, Width = 136f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_rate_day"), Kind = UiCellKind.Money, Width = 120f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_hires"), Kind = UiCellKind.Figure, Width = 80f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_takings"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_day"), Kind = UiCellKind.Date, Width = 104f }
                };
                TableShape(UiIcon.Car, EstateText.T("reallife_estate_tab_hire"), hire.Running ? EstateGame.NameOf(hire.Lot) : EstateText.T("reallife_estate_row_no_lot"), null, null, cols, rows, keys, HireSide(hire),
                    EstateText.T(hire.Running ? "reallife_estate_row_no_cars" : "reallife_estate_row_no_lot"), EstateText.T("reallife_estate_hire_what"));
            }

            private UiFactCardModel HireSide(HireFile hire)
            {
                var lines = new List<UiFactLine>(8);
                var actions = new List<UiActionModel>(1);
                if (hire.Running)
                {
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_estate_hire_lot"), EstateGame.NameOf(hire.Lot)));
                    lines.Add(UiFactLine.Bar(EstateText.T("reallife_ibox_spaces"), hire.Spaces > 0 ? hire.Cars.Count * 100 / hire.Spaces : 0));
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_hire_rating", "rating", EstateText.Level(hire.Rating))));
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_hire_year", "takings", Money(hire.TakingsYear), "costs", Money(hire.CostsYear))));
                    int premium = 0;
                    for (int i = 0; i < hire.Cars.Count; i++)
                    {
                        premium += HireRules.Premium(hire.Cars[i].Price, _c);
                    }
                    bool cover = hire.Cover;
                    lines.Add(_runs.Switch(EstateText.T("reallife_estate_hire_cover"), cover, on => Show(() => _d.SetFleetCover(on))));
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_hire_cover_what", "amount", Money(premium), "share", EstateText.Share(EstateCatalogue.HireCoverPays), "excess", Money(EstateCatalogue.HireCoverExcess))));
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_hire_how", "metres", EstateCatalogue.HireLotMetres.ToString(Inv))));
                    if (hire.Count(HireState.Out) + hire.Count(HireState.Waiting) == 0)
                    {
                        actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_close_lot"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count, () => Show(() => _d.SetLot(null))));
                    }
                }
                else
                {
                    var places = new List<BuildingRegistration>(8);
                    EstateGame.Lots(places);
                    if (places.Count == 0)
                    {
                        lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_hire_no_building"), UiRole.WarnText));
                    }
                    else
                    {
                        int at = Math.Max(0, Math.Min(places.Count - 1, _v.Lot));
                        string lot = EstateGame.Place(places[at].StreetName, places[at].StreetNumber);
                        lines.Add(_runs.Step(EstateText.T("reallife_estate_hire_lot"), places[at].Address.ToFormattedString(), null, at > 0, at < places.Count - 1, () => Show(() => _v.Lot = at - 1), () => Show(() => _v.Lot = at + 1)));
                        lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_hire_lot_what", "spaces", EstateCatalogue.HireSpacesAnyBuilding.ToString(Inv), "metres", EstateCatalogue.HireLotMetres.ToString(Inv))));
                        actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_open_lot"), UiButtonKind.Primary, UiIcon.Car, UiAction.Alt, () => Show(() => _d.SetLot(lot))));
                    }
                }
                return new UiFactCardModel { Title = EstateText.T("reallife_estate_tab_hire"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() };
            }

            private void CarPage(HireFile hire, HireCar car)
            {
                string id = car.Id;
                int carClass = HireRules.Class(car.Price);
                _m.Shape = EstateShape.Detail;
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = UiIcon.Car;
                _m.DetailTitle = EstateGame.CarName(car.Type);
                _m.DetailSub = EstateText.CarClass(carClass);
                _m.DetailChip = EstateText.T(car.State == (int)HireState.Out ? "reallife_ibox_chip_out" : car.State == (int)HireState.Waiting ? "reallife_ibox_chip_waiting_one" : car.State == (int)HireState.Valet ? "reallife_ibox_chip_valet" : "reallife_ibox_chip_idle");
                _m.DetailChipTone = car.State == (int)HireState.Out ? UiRole.Ok : UiRole.Notice;
                string say;
                switch ((HireState)car.State)
                {
                    case HireState.Out: say = EstateText.T("reallife_estate_car_out_what", "day", car.Day.ToString(Inv), "amount", Money(car.HireRate), "deposit", Money(car.Deposit)); break;
                    case HireState.Waiting: say = EstateText.T("reallife_estate_car_waiting_what"); break;
                    case HireState.Valet: say = EstateText.T("reallife_estate_car_valet_what"); break;
                    default: say = EstateText.T("reallife_estate_car_idle_what", "metres", EstateCatalogue.HireLotMetres.ToString(Inv)); break;
                }
                int rate = car.Rate;
                var lines = new List<UiFactLine>
                {
                    UiFactLine.Say(say),
                    UiFactLine.Row(EstateText.T("reallife_ibox_hires"), car.Hires.ToString(Inv)),
                    UiFactLine.Row(EstateText.T("reallife_ibox_takings"), Money(car.Takings)),
                    UiFactLine.Say(EstateText.T("reallife_estate_car_what", "class", EstateText.CarClass(carClass), "price", Money(car.Price), "going", Money(HireRules.Going(car.Price, _c.Index)))),
                    _runs.Step(EstateText.T("reallife_ui_estate_rate"), EstateText.Share(rate),
                        EstateText.T("reallife_estate_car_rate", "amount", Money(HireRules.Rate(car.Price, rate, _c.Index)), "chance", EstateText.Share(HireRules.Demand(carClass, _c.Season, _c.Event, _c.Cycle, rate, hire.Rating)),
                            "rating", EstateText.Level(hire.Rating)), rate > EstateCatalogue.RateLeast, rate < EstateCatalogue.RateMost,
                        () => Show(() => _d.SetCarRate(id, rate - EstateCatalogue.RateStep)), () => Show(() => _d.SetCarRate(id, rate + EstateCatalogue.RateStep)))
                };
                var actions = new List<UiActionModel>(1);
                if (car.State == (int)HireState.Idle || car.State == (int)HireState.Valet)
                {
                    actions.Add(_runs.Button(EstateText.T("reallife_estate_btn_remove_car"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count, () => Show(() =>
                    {
                        _v.Pick = null;
                        _d.RemoveCar(id);
                    })));
                }
                _m.Left = new[] { new UiFactCardModel { Title = EstateText.T("reallife_ibox_sec_money"), Heading = true, Lines = lines.ToArray(), Actions = actions.ToArray() } };
                _m.Right = new[]
                {
                    new UiFactCardModel
                    {
                        Lines = new[] { UiFactLine.Say(EstateText.T("reallife_estate_car_risk", "damage", EstateText.Share(EstateCatalogue.HireDamageChance), "late", EstateText.Share(EstateCatalogue.HireLateChance), "cleaning", Money(HireRules.Cleaning(_c.Index)))) }
                    }
                };
            }

            private void AddCarPage(HireFile hire, VehicleInstance add)
            {
                string id = add.id;
                int price = EstateGame.CarPrice(add), carClass = HireRules.Class(price);
                _m.Shape = EstateShape.Detail;
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = UiIcon.Car;
                _m.DetailTitle = EstateGame.CarName(add.vehicleTypeName);
                _m.DetailSub = EstateText.CarClass(carClass);
                var lines = new List<UiFactLine>
                {
                    UiFactLine.Say(EstateText.T("reallife_estate_hire_add_what", "class", EstateText.CarClass(carClass), "amount", Money(HireRules.Going(price, _c.Index)),
                        "chance", EstateText.Share(HireRules.Demand(carClass, _c.Season, _c.Event, _c.Cycle, EstateCatalogue.Thousand, hire.Rating)))),
                    UiFactLine.Bar(EstateText.T("reallife_ibox_spaces"), hire.Spaces > 0 ? hire.Cars.Count * 100 / hire.Spaces : 0),
                    UiFactLine.Say(EstateText.T("reallife_estate_hire_gone_what"))
                };
                _m.Left = new[]
                {
                    new UiFactCardModel
                    {
                        Title = EstateText.T("reallife_estate_btn_add_car"), Heading = true, Lines = lines.ToArray(),
                        Actions = new[]
                        {
                            _runs.Button(EstateText.T("reallife_estate_btn_add_car"), UiButtonKind.Primary, UiIcon.Plus, UiAction.Alt, () => Show(() =>
                            {
                                if (_d.AddCar(id))
                                {
                                    _v.Pick = "car:" + id;
                                }
                            }))
                        }
                    }
                };
            }

            // ------------------------------------------------------------------------------------------------- the city's rents

            private string Cents(int cents) => cents < 0 ? EstateText.T("reallife_ui_estate_not_yet") : ((float)cents / 100f).ToCurrencyFormat();

            // The info view of the city's rents: the means, the units standing empty, the month's change, what is for sale by kind, the map's ramp and the districts ranked.
            private void RentsView()
            {
                RentMapData map = _d.Rents;
                _m.Shape = EstateShape.Rents;
                bool read = map.Day >= 0;
                int homes = (int)EstateUse.Homes, shops = (int)EstateUse.Shops, offices = (int)EstateUse.Offices;
                int change = RentRingRules.Change(_s);
                int let = 0, units = 0;
                long rentSum = 0;
                for (int i = 0; i < _s.Holdings.Count; i++)
                {
                    EstateHolding h = _s.Holdings[i];
                    if (!h.Managed)
                    {
                        continue;
                    }
                    for (int n = 0; n < h.Units.Count; n++)
                    {
                        units++;
                        if (h.Units[n].Let)
                        {
                            let++;
                            rentSum += (long)h.Units[n].Rent * _monthDays;
                        }
                    }
                }
                int empty = read ? map.EmptyShare() : -1;
                string notYet = EstateText.T("reallife_ui_estate_not_yet");
                var rows = new List<UiInfoRow>(8)
                {
                    new UiInfoRow { Label = EstateText.T("reallife_ui_estate_rv_mean_flat"), Value = read && map.MeanUnit(homes) >= 0 ? Money(map.MeanUnit(homes)) : notYet },
                    new UiInfoRow { Label = EstateText.T("reallife_ui_estate_rv_flat_sqm"), Value = read ? Cents(map.PerSqm(homes)) : notYet },
                    new UiInfoRow { Label = EstateText.T("reallife_ui_estate_rv_shop_sqm"), Value = read ? Cents(map.PerSqm(shops)) : notYet },
                    new UiInfoRow { Label = EstateText.T("reallife_ui_estate_rv_office_sqm"), Value = read ? Cents(map.PerSqm(offices)) : notYet },
                    new UiInfoRow { Label = EstateText.T("reallife_ui_estate_rv_empty"), Value = empty < 0 ? notYet : EstateText.Share(empty), Ink = empty > 100 ? UiRole.WarnText : UiRole.None },
                    new UiInfoRow
                    {
                        Label = EstateText.T("reallife_ui_estate_rv_month"), Value = change == int.MinValue ? EstateText.T("reallife_ui_estate_rv_month_wait") : (change >= 0 ? "+" : string.Empty) + EstateText.Share(change),
                        Ink = change > 0 ? UiRole.WarnText : change < 0 ? UiRole.OkText : UiRole.None
                    },
                    new UiInfoRow { Label = EstateText.T("reallife_ui_estate_rv_your_let"), Value = EstateText.T("reallife_ui_estate_of", "value", let.ToString(Inv), "most", units.ToString(Inv)) },
                    new UiInfoRow { Label = EstateText.T("reallife_ui_estate_rv_your_mean"), Value = let > 0 ? Cash(rentSum / let) : notYet }
                };
                var v = new UiInfoViewModel
                {
                    Title = EstateText.T("reallife_ui_estate_rv_title"), Icon = UiIcon.Key, HeadLabel = EstateText.T("reallife_ui_estate_rv_rent"), HeadValue = EstateText.T("reallife_ui_estate_rv_per_month"), Rows = rows.ToArray(),
                    ChartTitle = EstateText.T("reallife_ui_estate_rv_for_sale"), LegendTitle = EstateText.T("reallife_ui_estate_rv_legend"), ScaleLabel = EstateText.T("reallife_ui_estate_rv_scale")
                };
                int total = map.ForSaleAll();
                var labels = new List<string>(4);
                var values = new List<string>(4);
                var shares = new List<float>(4);
                for (int k = 0; k < RentMapData.Uses; k++)
                {
                    if (map.ForSale[k] > 0)
                    {
                        labels.Add(EstateText.Use(k));
                        values.Add(EstateText.Share(map.ForSale[k] * 1000 / Math.Max(1, total)));
                        shares.Add(map.ForSale[k] / (float)Math.Max(1, total));
                    }
                }
                v.ChartLabels = labels.ToArray();
                v.ChartValues = values.ToArray();
                v.ChartShares = shares.ToArray();
                v.ChartNote = total > 0 ? EstateText.T("reallife_ui_estate_rv_listings", "count", total.ToString(Inv)) : null;
                var order = new int[Math.Max(1, map.DistrictCount)];
                int count = read ? map.Ranked(homes, order) : 0;
                int low = int.MaxValue, high = 0;
                for (int i = 0; i < count; i++)
                {
                    low = Math.Min(low, map.PerSqm(order[i], homes));
                    high = Math.Max(high, map.PerSqm(order[i], homes));
                }
                v.ScaleLow = count > 0 ? Cents(low) : string.Empty;
                v.ScaleHigh = count > 0 ? Cents(high) : string.Empty;
                int shown = Math.Min(5, count);
                v.RankTitle = EstateText.T("reallife_ui_estate_rv_districts");
                v.RankNote = EstateText.T("reallife_ui_estate_rv_rank_note");
                v.RankLabels = new string[shown];
                v.RankValues = new string[shown];
                v.RankFills = new int[shown];
                for (int i = 0; i < shown; i++)
                {
                    int per = map.PerSqm(order[i], homes);
                    v.RankLabels[i] = EstateText.District(map.Districts[order[i]]);
                    v.RankValues[i] = Cents(per);
                    v.RankFills[i] = high > 0 ? per * 100 / high : 0;
                }
                _m.Rents = v;
                _m.Back = EstateText.T("reallife_co_back");
            }
        }
    }
}
