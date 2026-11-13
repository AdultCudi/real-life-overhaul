using System;
using System.Collections.Generic;
using Extensions;
using RealLifeOverhaul.Economy;
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
                var locate = new List<int>(4);
                var rates = new List<EstateRow>(8);
                int soldAll = 0, roomsAll = 0;
                long takingsAll = 0, costsAll = 0, keptYear = 0;
                var said = new List<EstateRow>(8);
                int saidAll = 0;
                int saidMost = _s.Hotels.Count * EstateCatalogue.RoomTypes;   // as many reviews as the rates beside them have rows, so the two lists end together
                for (int i = 0; i < _s.Hotels.Count; i++)
                {
                    HotelFile f = _s.Hotels[i];
                    int rooms = HotelRules.Rooms(f.Tier), sold = f.Sold[0] + f.Sold[1], soldShare = rooms > 0 ? sold * 100 / rooms : 0;
                    soldAll += sold;
                    roomsAll += rooms;
                    takingsAll += f.Takings;
                    costsAll += f.Costs;
                    keptYear += (long)f.TakingsYear - f.CostsYear;
                    for (int r = 0; r < f.Reviews.Length; r++)
                    {
                        if (f.Reviews[r] != 0)
                        {
                            saidAll++;
                        }
                        if (f.Reviews[r] != 0 && said.Count < saidMost)
                        {
                            // The stars are the guest's own rating of the stay, high for praise and low for a complaint.
                            said.Add(new EstateRow { Icon = UiIcon.Message, Title = EstateText.Review(f.Reviews[r]), Sub = f.Name, Stars = EstateFigures.ReviewHalves(f.Reviews[r]), Run = -1 });
                        }
                    }
                    // The hotel stands in a building of the city's: the game's own guide leads to it, once the city has placed it.
                    string place = EstateGame.HotelPlace(f.Slot);
                    locate.Add(place != null ? _runs.Do(() => EstateGame.Locate(place)) : -1);
                    int left = f.PushEnd >= _day ? f.PushEnd - _day + 1 : 0;
                    int staffShare = (f.Staff + 1) * 100 / EstateCatalogue.StaffLevels;
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { f.Name, string.Empty, (f.Clean / 10).ToString(Inv), staffShare.ToString(Inv), soldShare.ToString(Inv), Money(f.Takings), Money(f.Costs),
                            left > 0 ? EstateText.T("reallife_ui_estate_campaign_on", "days", left.ToString(Inv)) : EstateText.T("reallife_ui_estate_campaign_off"), string.Empty },
                        Values = new[] { (int)UiIcon.Building, HotelRules.Stars(f.Rating) * 2, f.Clean / 10, staffShare, soldShare, 0, 0, 0, place != null ? (int)UiIcon.Locate : 0 },
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, f.Takings < f.Costs ? UiRole.DangerText : UiRole.None,
                            left > 0 ? UiRole.Ok : UiRole.Muted, UiRole.None },
                        Subs = new[] { EstateText.District(HotelDistrict(f.Slot)), null, null, null, null, null, null, null, null },
                        SortKeys = new[] { N, HotelRules.Stars(f.Rating) * 2, f.Clean / 10, staffShare, soldShare, f.Takings, f.Costs, left, N },
                        // The year's takings and costs the old table had a column each for: the table has no room for two more beside its meters, so they stand on the card the
                        // takings cell raises, with what the hotel kept.
                        Card = new UiDataCard
                        {
                            Column = 5, Title = f.Name,
                            Labels = new[] { EstateText.T("reallife_ui_estate_col_takings_year"), EstateText.T("reallife_ui_estate_col_costs_year"),
                                EstateText.T("reallife_ui_estate_fig_kept_year"), EstateText.T("reallife_ui_estate_card_kept_night") },
                            Values = new[] { Money(f.TakingsYear), Money(f.CostsYear), Money(f.TakingsYear - f.CostsYear), Money(f.Takings - f.Costs) }
                        }
                    });
                    keys.Add("hot:" + f.Slot);
                    // Your rate for each kind of room against the going rate, and how much of it sold.
                    for (int type = 0; type < EstateCatalogue.RoomTypes; type++)
                    {
                        int count = HotelRules.Rooms(f.Tier, type), kindSold = f.Sold[type], rate = f.Rate[type];
                        rates.Add(new EstateRow
                        {
                            Icon = UiIcon.Building, Title = EstateText.Pair(f.Name, EstateGame.RoomName(f.Tier, type)),
                            Sub = EstateText.T("reallife_ui_estate_rate_going", "going", Money(f.Going[type]), "share", EstateText.Share(count > 0 ? kindSold * 1000 / count : 0)),
                            Chip = EstateText.T("reallife_ui_estate_of_going", "share", EstateText.Share(rate)), ChipTone = UiRole.Notice,
                            Amount = Money(HotelRules.Rate(f.Going[type], rate)), Run = _runs.Do(() => Open("hot:" + f.Slot, null))
                        });
                    }
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_hotel"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_rating"), Kind = UiCellKind.Stars, Width = 120f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_clean"), Kind = UiCellKind.Meter, Width = 112f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_staff"), Kind = UiCellKind.Meter, Width = 112f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_rooms"), Kind = UiCellKind.Meter, Width = 128f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_takings"), Kind = UiCellKind.Money, Width = 96f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_costs"), Kind = UiCellKind.Money, Width = 96f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_campaign"), Kind = UiCellKind.Chip, Width = 176f },
                    new UiDataColumn { Title = string.Empty, Kind = UiCellKind.Action, Width = 56f }
                };
                TableShape(UiIcon.Building, EstateText.T("reallife_estate_tab_hotel"),
                    EstateText.T("reallife_ui_estate_hotels_sub", "count", _s.Hotels.Count.ToString(Inv)), null, null, cols, rows, keys, null,
                    EstateText.T("reallife_estate_row_no_hotel"), EstateText.T("reallife_ui_estate_empty_hotel_body"), null, new[] { 5, 6 });
                _m.RowRuns = Ordered(locate);
                _m.Notes = new[]
                {
                    EstateText.T("reallife_estate_hotel_what"), EstateText.T("reallife_estate_hotel_night_what", "share", EstateText.Share(EstateCatalogue.Occupancy)),
                    EstateText.T("reallife_ui_estate_note_hotel_staff"), EstateText.T("reallife_estate_hotel_held_what")
                };
                if (_s.Hotels.Count > 0)
                {
                    _m.Strip = new[]
                    {
                        new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_rooms_sold"), Value = soldAll.ToString(Inv) + " / " + roomsAll.ToString(Inv) },
                        new UiFigureModel { Label = EstateText.T("reallife_ui_estate_col_takings"), Value = Cash(takingsAll) },
                        new UiFigureModel { Label = EstateText.T("reallife_ui_estate_col_costs"), Value = Cash(costsAll) },
                        new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_kept_year"), Value = Cash(keptYear) }
                    };
                    _m.RelatedTitle = EstateText.T("reallife_ui_estate_rates_title");
                    _m.RelatedCount = rates.Count.ToString(Inv);
                    _m.RelatedRows = 8;
                    _m.Related = rates.ToArray();
                    if (saidAll > said.Count)
                    {
                        // The list ends with the rates' last line: a line that only says how many reviews it leaves out.
                        said[said.Count - 1] = new EstateRow { Title = EstateText.T("reallife_ui_estate_more_reviews", "count", (saidAll - said.Count + 1).ToString(Inv)), Run = -1 };
                    }
                    _m.Related2Title = EstateText.T("reallife_ui_estate_guests_say");
                    _m.Related2Count = saidAll > 0 ? EstateText.T("reallife_ui_estate_reviews_n", "count", saidAll.ToString(Inv)) : null;
                    _m.Related2 = said.ToArray();
                }
            }

            // The district a hotel stands in, by Travel's own id of it.
            private static string HotelDistrict(string slot)
            {
                HotelSlot hs = TravelCatalogue.Hotel(slot);
                return hs != null ? hs.Neighbourhood : string.Empty;
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
                actions.Add(_runs.Button(EstateText.T(f.Leased ? "reallife_estate_btn_end_lease" : "reallife_estate_btn_sell_hotel"),
                    UiButtonKind.Destructive, UiIcon.None, UiAction.Count, () => Show(() => _d.GiveUpHotel(slot))));
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
                        EstateText.Fill("reallife_estate_hotel_rate", "room", EstateGame.RoomName(f.Tier, type), "count",
                            HotelRules.Rooms(f.Tier, type).ToString(Inv), "amount", Money(HotelRules.Rate(f.Going[type], rate)),
                            "going", Money(f.Going[type]), "share", EstateText.Share(HotelRules.RateDemand(rate))), rate > EstateCatalogue.RateLeast, rate < EstateCatalogue.RateMost,
                        () => Show(() => _d.SetRoomRate(slot, t, rate - EstateCatalogue.RateStep)), () => Show(() => _d.SetRoomRate(slot, t, rate + EstateCatalogue.RateStep))) );
                    steps = steps + 0;
                }
                int staff = f.Staff;
                rooms2.Add(_runs.Step(EstateText.T("reallife_estate_hotel_staff"), EstateText.Staff(staff),
                    EstateText.T("reallife_estate_hotel_staff_cost", "amount", Money(HotelRules.Costs(f.Tier, staff, sold, _c.Index)), "clean",
                        EstateText.Level(EstateCatalogue.CleanTarget[staff]), "day", f.InspectDay.ToString(Inv)),
                    staff > 0, staff < EstateCatalogue.StaffLevels - 1, () => Show(() => _d.SetHotelStaff(slot, staff - 1)), () => Show(() => _d.SetHotelStaff(slot, staff + 1))));
                rooms2.Add(UiFactLine.Section(EstateText.T("reallife_ibox_sec_campaign")));
                rooms2.Add(UiFactLine.Say(f.PushEnd >= _day
                    ? EstateText.T("reallife_estate_hotel_campaign_on", "day", f.PushEnd.ToString(Inv), "share", EstateText.Share(EstateCatalogue.CampaignDemand))
                    : EstateText.T("reallife_estate_hotel_campaign", "amount", Money(HotelRules.CampaignCost(f.Tier, _c.Index)), "days",
                        EstateCatalogue.CampaignDays.ToString(Inv), "share", EstateText.Share(EstateCatalogue.CampaignDemand))));
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
                    CarKind kind = EstateGame.KindOf(car.Type);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { EstateGame.CarName(car.Type), EstateText.CarState(car.State), Money(rate), car.Hires.ToString(Inv),
                            Money(car.Takings), car.State == (int)HireState.Out ? EstateText.T("reallife_estate_row_until", "day", car.Day.ToString(Inv)) : "-" },
                        Values = new[] { (int)CarClass.Glyph(kind), 0, 0, 0, 0, 0 },
                            Tones = new[] { CarClass.Tint(kind, car.Price), car.State == (int)HireState.Waiting ? UiRole.Warn : car.State == (int)HireState.Out
                            ? UiRole.Ok : UiRole.Notice, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.CarClass(carClass), null, null, null, null, null },
                        SortKeys = new[] { N, N, rate, car.Hires, car.Takings, car.State == (int)HireState.Out ? car.Day : N },
                        Kinds = new[] { carClass, HireRules.Away(car.State) ? EstateTables.CarOut : EstateTables.CarIn }
                    });
                    keys.Add("car:" + car.Id);
                }
                // The cars of yours within reach of the lot that could join the fleet, each with the rate it would hire at: a list of their own with a button to add.
                var could = new List<EstateRow>(vehicles.Count);
                for (int i = 0; i < vehicles.Count; i++)
                {
                    VehicleInstance v = vehicles[i];
                    string vid = v.id;
                    int price = EstateGame.CarPrice(v), going = HireRules.Going(price, _c.Index);
                    CarKind kind = EstateGame.KindOf(v.vehicleTypeName);
                    // The cars of yours that are not on hire are rows of the table as the old table had them (its third place to filter by), each opening the page that adds it to the fleet.
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { EstateGame.CarName(v.vehicleTypeName), EstateText.T("reallife_estate_row_on_lot"), Money(going), string.Empty, string.Empty, string.Empty },
                        Values = new[] { (int)CarClass.Glyph(kind), 0, 0, 0, 0, 0 },
                        Tones = new[] { CarClass.Tint(kind, price), UiRole.Muted, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.CarClass(HireRules.Class(price)), null, null, null, null, null },
                        SortKeys = new[] { N, N, going, N, N, N },
                        Kinds = new[] { HireRules.Class(price), EstateTables.CarNotOnHire }
                    });
                    keys.Add("add:" + vid);
                    could.Add(new EstateRow
                    {
                        Icon = CarClass.Glyph(kind), Tint = CarClass.Tint(kind, price), Title = EstateGame.CarName(v.vehicleTypeName), Sub = EstateText.CarClass(HireRules.Class(price)),
                        Chip = EstateText.T("reallife_ui_estate_going_rate_day", "amount", Money(going)), ChipTone = UiRole.Notice,
                        Button = EstateText.T("reallife_ui_estate_add_to_fleet"), ButtonRun = _runs.Do(() => Show(() =>
                        {
                            if (_d.AddCar(vid))
                            {
                                _v.Pick = "car:" + vid;
                            }
                        })),
                        Run = _runs.Do(() => Show(() => _v.Pick = "add:" + vid))
                    });
                }
                var cols = new[]
                {
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_car"), Kind = UiCellKind.Tile, Width = 0f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_state"), Kind = UiCellKind.Chip, Width = 184f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_rate_day"), Kind = UiCellKind.Money, Width = 120f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_hires"), Kind = UiCellKind.Figure, Width = 80f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_takings"), Kind = UiCellKind.Money, Width = 112f },
                    new UiDataColumn { Title = EstateText.T("reallife_ui_estate_col_day"), Kind = UiCellKind.Date, Width = 104f }
                };
                TableShape(UiIcon.Car, EstateText.T("reallife_estate_tab_hire"), hire.Running ? EstateGame.NameOf(hire.Lot)
                    : EstateText.T("reallife_estate_row_no_lot"), null, null, cols, rows, keys, HireSide(hire),
                    EstateText.T(hire.Running ? "reallife_estate_row_no_cars" : "reallife_estate_row_no_lot"), EstateText.T("reallife_estate_hire_what"),
                    new[]
                    {
                        Choice("reallife_estate_filter_all_classes", EstateText.ClassKeys),
                        Choice("reallife_estate_filter_all_places", "reallife_estate_filter_out", "reallife_estate_filter_in", "reallife_estate_filter_not_on_hire")
                    },
                    new[] { 4 }, EstateText.T("reallife_estate_empty_hire"));
                _m.Notes = new[] { EstateText.T("reallife_estate_hire_what") };
                if (hire.Running)
                {
                    // The fleet's rating as the kit's stars before the line that says what it is.
                    _m.HeadStars = Math.Max(0, Math.Min(10, hire.Rating / 100));
                    _m.HeadSub = EstateText.T("reallife_ui_estate_fleet_rating");
                    int hiresAll = 0, outNow = hire.Count(HireState.Out);
                    long takingsAll = 0, rateSum = 0;
                    for (int i = 0; i < hire.Cars.Count; i++)
                    {
                        hiresAll += hire.Cars[i].Hires;
                        takingsAll += hire.Cars[i].Takings;
                        rateSum += HireRules.Rate(hire.Cars[i].Price, hire.Cars[i].Rate, _c.Index);
                    }
                    _m.Strip = new[]
                    {
                        new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_cars_out"), Value = outNow.ToString(Inv) },
                        new UiFigureModel { Label = EstateText.T("reallife_ui_estate_col_hires"), Value = hiresAll.ToString(Inv) },
                        new UiFigureModel { Label = EstateText.T("reallife_ui_estate_col_takings"), Value = Cash(takingsAll) },
                        new UiFigureModel { Label = EstateText.T("reallife_ui_estate_fig_mean_rate"), Value = hire.Cars.Count > 0 ? Cash(rateSum / hire.Cars.Count) : Cash(0) }
                    };
                    _m.RelatedTitle = EstateText.T("reallife_ui_estate_add_cars");
                    _m.RelatedCount = could.Count > 0 ? could.Count.ToString(Inv) : null;
                    _m.RelatedRows = 8;   // the cars within reach run in two columns of four
                    if (could.Count > _m.RelatedRows)
                    {
                        // The list holds eight lines: seven cars and a last line that says how many there are and shows them in the table, filtered to the cars not on hire.
                        int all = could.Count;
                        could.RemoveRange(_m.RelatedRows - 1, all - (_m.RelatedRows - 1));
                        could.Add(MoreRow("reallife_ui_estate_more_cars", all, _runs.Do(() => Show(() =>
                        {
                            _v.Pick = null;
                            _v.Offset = 0f;
                            _v.ResetTable();
                            _v.Chosen[1] = EstateTables.CarNotOnHire;
                        }))));
                    }
                    _m.Related = could.ToArray();
                }
            }

            private UiFactCardModel HireSide(HireFile hire)
            {
                var lines = new List<UiFactLine>(8);
                var actions = new List<UiActionModel>(1);
                if (hire.Running)
                {
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_fig_year_takings"), Money(hire.TakingsYear)));
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_fig_year_costs"), Money(hire.CostsYear)));
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_hire_kept_year"), Money(hire.TakingsYear - hire.CostsYear),
                        hire.TakingsYear >= hire.CostsYear ? UiRole.OkText : UiRole.DangerText));
                    HireCar best = null, most = null;
                    for (int i = 0; i < hire.Cars.Count; i++)
                    {
                        best = best == null || hire.Cars[i].Takings > best.Takings ? hire.Cars[i] : best;
                        most = most == null || hire.Cars[i].Hires > most.Hires ? hire.Cars[i] : most;
                    }
                    if (best != null && best.Takings > 0)
                    {
                        lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_hire_best"), EstateText.Pair(EstateGame.CarName(best.Type), Money(best.Takings))));
                        lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_hire_most"), EstateText.Pair(EstateGame.CarName(most.Type), most.Hires.ToString(Inv))));
                    }
                    lines.Add(UiFactLine.Section(EstateText.T("reallife_ui_estate_hire_sec_cover")));
                    int premium = 0;
                    for (int i = 0; i < hire.Cars.Count; i++)
                    {
                        premium += HireRules.Premium(hire.Cars[i].Price, _c);
                    }
                    bool cover = hire.Cover;
                    lines.Add(_runs.Switch(EstateText.T("reallife_estate_hire_cover"), cover, on => Show(() => _d.SetFleetCover(on))));
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_fig_cover_cost"), EstateText.T("reallife_ui_estate_amount_a_day", "amount", Money(premium))));
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_cover_pays"), EstateText.Share(EstateCatalogue.HireCoverPays)));
                    lines.Add(UiFactLine.Row(EstateText.T("reallife_ui_estate_cover_excess"), Money(EstateCatalogue.HireCoverExcess)));
                    lines.Add(UiFactLine.Section(EstateText.T("reallife_ui_estate_hire_sec_lot")));
                    // The lot's own meters stand in the cover's card: the spaces its cars take and the cars out on hire.
                    lines.Add(UiFactLine.Bar(EstateText.T("reallife_ibox_spaces"), hire.Spaces > 0 ? hire.Cars.Count * 100 / hire.Spaces : 0));
                    lines.Add(UiFactLine.Bar(EstateText.T("reallife_ui_estate_sh_in_hire"), hire.Cars.Count > 0 ? hire.Count(HireState.Out) * 100 / hire.Cars.Count : 0));
                    lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_hire_how", "metres", EstateCatalogue.HireLotMetres.ToString(Inv)), UiRole.Muted));
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
                        lines.Add(_runs.Step(EstateText.T("reallife_estate_hire_lot"), places[at].Address.ToFormattedString(), null, at > 0,
                            at < places.Count - 1, () => Show(() => _v.Lot = at - 1), () => Show(() => _v.Lot = at + 1)));
                        lines.Add(UiFactLine.Say(EstateText.T("reallife_estate_hire_lot_what", "spaces",
                            EstateCatalogue.HireSpacesAnyBuilding.ToString(Inv), "metres", EstateCatalogue.HireLotMetres.ToString(Inv))));
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
                _m.DetailChip = EstateText.T(car.State == (int)HireState.Out ? "reallife_ibox_chip_out" : car.State == (int)HireState.Waiting
                    ? "reallife_ibox_chip_waiting_one" : car.State == (int)HireState.Valet ? "reallife_ibox_chip_valet" : "reallife_ibox_chip_idle");
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
                        EstateText.T("reallife_estate_car_rate", "amount", Money(HireRules.Rate(car.Price, rate, _c.Index)), "chance",
                            EstateText.Share(HireRules.Demand(carClass, _c.Season, _c.Event, _c.Cycle, rate, hire.Rating)),
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
                        Lines = new[] { UiFactLine.Say(EstateText.T("reallife_estate_car_risk", "damage",
                            EstateText.Share(EstateCatalogue.HireDamageChance), "late", EstateText.Share(EstateCatalogue.HireLateChance), "cleaning", Money(HireRules.Cleaning(_c.Index)))) }
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
                        Label = EstateText.T("reallife_ui_estate_rv_month"), Value = change == int.MinValue
                            ? EstateText.T("reallife_ui_estate_rv_month_wait") : (change >= 0 ? "+" : string.Empty) + EstateText.Share(change),
                        Ink = change > 0 ? UiRole.OkText : change < 0 ? UiRole.WarnText : UiRole.None
                    },
                    new UiInfoRow { Label = EstateText.T("reallife_ui_estate_rv_your_let"), Value = EstateText.T("reallife_ui_estate_of", "value", let.ToString(Inv), "most", units.ToString(Inv)) },
                    new UiInfoRow { Label = EstateText.T("reallife_ui_estate_rv_your_mean"), Value = let > 0 ? Cash(rentSum / let) : notYet }
                };
                if (!read)
                {
                    rows.RemoveRange(1, 5);   // nothing is read yet: one "Not yet" for the city's rents, not a row of them; the player's own two figures stand
                }
                var v = new UiInfoViewModel
                {
                    Title = EstateText.T("reallife_ui_estate_rv_title"), Icon = UiIcon.Key, HeadLabel = EstateText.T("reallife_ui_estate_rv_rent"),
                        HeadValue = EstateText.T("reallife_ui_estate_rv_per_month"), Rows = rows.ToArray(),
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
                // The icons down the view's side choose the kind of rent the districts are ranked by and the legend runs over: flats, shops or offices.
                int view = Math.Max(0, Math.Min(2, _v.RentUse)), use = view == 1 ? shops : view == 2 ? offices : homes;
                v.Views = new[] { UiIcon.Home, UiIcon.Store, UiIcon.Briefcase };
                v.View = view;
                int count = read ? map.Ranked(use, order) : 0;
                int low = int.MaxValue, high = 0;
                for (int i = 0; i < count; i++)
                {
                    low = Math.Min(low, map.PerSqm(order[i], use));
                    high = Math.Max(high, map.PerSqm(order[i], use));
                }
                v.ScaleLow = count > 0 ? Cents(low) : string.Empty;
                v.ScaleHigh = count > 0 ? Cents(high) : string.Empty;
                int shown = Math.Min(5, count);
                v.RankTitle = EstateText.T("reallife_ui_estate_rv_districts");
                v.RankNote = count > shown ? EstateText.T("reallife_ui_estate_rv_rank_of", "shown", shown.ToString(Inv), "count", count.ToString(Inv))
                    : EstateText.T("reallife_ui_estate_rv_rank_note");
                v.RankLabels = new string[shown];
                v.RankValues = new string[shown];
                v.RankFills = new int[shown];
                v.RankSteps = new int[shown];
                for (int i = 0; i < shown; i++)
                {
                    int per = map.PerSqm(order[i], use);
                    v.RankLabels[i] = EstateText.District(map.Districts[order[i]]);
                    v.RankValues[i] = Cents(per);
                    v.RankFills[i] = high > 0 ? per * 100 / high : 0;
                    v.RankSteps[i] = Math.Min(4, (int)((long)(per - low) * 5 / Math.Max(1, high - low + 1)));   // the legend's step the map shows the district in
                }
                _m.Rents = v;
                _m.Back = EstateText.T("reallife_co_back");
            }
        }
    }
}
