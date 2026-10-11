using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Hotel and Car hire tabs. Each writes its rows and, for what is picked, a page in the white box that says
    /// in sentences what each figure is made of. Written when the tab is shown; nothing is kept.
    /// </summary>
    public sealed partial class EstateApp
    {
        private readonly List<VehicleInstance> _vehicles = new List<VehicleInstance>(8);
        private int _lot;   // the building showing for the lot

        // ---------------------------------------------------------------- Hotel

        private void WriteHotel(InfoBox box, EstateState state, int day, ref string selected)
        {
            EstateClimate c = _director.Climate;
            HotelFile picked = null;
            for (int i = 0; i < state.Hotels.Count; i++)
            {
                HotelFile f = state.Hotels[i];
                int rooms = HotelRules.Rooms(f.Tier);
                TableRow row;
                if (Pick("hot:" + f.Slot, ref selected, out row))
                {
                    picked = f;
                }
                row.Set(0, f.Name, f.AskRooms > 0 ? TableRow.Yellow : TableRow.Plain);
                row.Set(1, EstateText.T("reallife_estate_row_sold", "sold", (f.Sold[0] + f.Sold[1]).ToString(), "rooms", rooms.ToString()), f.Sold[0] + f.Sold[1]);
                row.Set(2, EstateText.Money(f.Takings), f.Takings);
                row.Set(3, EstateText.Money(f.Costs), f.Costs, f.Takings < f.Costs ? TableRow.Red : TableRow.Plain);
                row.Set(4, EstateText.Money(f.TakingsYear), f.TakingsYear);
                row.Set(5, EstateText.Money(f.CostsYear), f.CostsYear);
                row.Set(6, EstateText.Stars(f.Rating), f.Rating);
            }
            if (picked == null && state.Hotels.Count == 1 && _pick == null)
            {
                picked = state.Hotels[0];   // (one hotel: its page is the tab's page)
            }
            if (picked != null)
            {
                for (int r = 0; r < picked.Reviews.Length; r++)
                {
                    if (picked.Reviews[r] != 0)
                    {
                        Say("rev" + r, EstateText.Review(picked.Reviews[r]));
                    }
                }
                WriteHotelOf(box, state, picked, day, c);
                return;
            }
            if (state.Hotels.Count == 0)
            {
                Say("none", EstateText.T("reallife_estate_row_no_hotel"));
            }
            box.Title(EstateText.T("reallife_estate_tab_hotel"));
            box.Line(EstateText.T("reallife_estate_hotel_what"));
            box.Line(EstateText.T("reallife_estate_hotel_night_what", "share", EstateText.Share(EstateCatalogue.Occupancy)));
            box.Line(EstateText.T("reallife_estate_hotel_staff_what"));
            box.Line(EstateText.T("reallife_estate_hotel_held_what"));
        }

        private void WriteHotelOf(InfoBox box, EstateState state, HotelFile f, int day, EstateClimate c)
        {
            string slot = f.Slot;
            int rooms = HotelRules.Rooms(f.Tier);
            box.Title(f.Name);
            box.Line(EstateText.T("reallife_estate_hotel_last", "sold", (f.Sold[0] + f.Sold[1]).ToString(), "rooms", rooms.ToString(), "takings", EstateText.Money(f.Takings)) + " "
                     + EstateText.T(f.Leased ? "reallife_estate_hotel_last_leased" : "reallife_estate_hotel_last_owned", "costs", EstateText.Money(f.Costs), "lease", EstateText.Money(f.LeaseDaily)));
            box.Line(EstateText.T("reallife_estate_hotel_year", "takings", EstateText.Money(f.TakingsYear), "costs", EstateText.Money(f.CostsYear)));
            box.Line(EstateText.T("reallife_estate_hotel_rating", "stars", HotelRules.Stars(f.Rating).ToString(), "clean", EstateText.Level(f.Clean), "target", EstateText.Level(HotelRules.RatingTarget(f.Clean, f.Staff, (f.Rate[0] + f.Rate[1]) / 2)))
                     + " " + EstateText.T("reallife_estate_hotel_rating_what", "share", EstateText.Share(HotelRules.RatingDemand(f.Rating))));
            for (int type = 0; type < EstateCatalogue.RoomTypes; type++)
            {
                int t = type;
                box.Line(EstateText.Fill("reallife_estate_hotel_rate", "room", EstateGame.RoomName(f.Tier, type), "count", HotelRules.Rooms(f.Tier, type).ToString(), "amount", EstateText.Money(HotelRules.Rate(f.Going[type], f.Rate[type])),
                    "going", EstateText.Money(f.Going[type]), "share", EstateText.Share(HotelRules.RateDemand(f.Rate[type]))));
                Steps(box, EstateCatalogue.RateLeast, EstateCatalogue.RateMost, EstateCatalogue.RateStep, f.Rate[type], rate => _director.SetRoomRate(slot, t, rate));
            }
            box.Headline("reallife_estate_hotel_staff");
            if (!box.HasSelector || !box.Selector(EstateCatalogue.StaffLevels, f.Staff, EstateText.Staff, level => _director.SetHotelStaff(slot, level)))
            {
                box.Line(EstateText.Staff(f.Staff));
            }
            box.Line(EstateText.T("reallife_estate_hotel_staff_cost", "amount", EstateText.Money(HotelRules.Costs(f.Tier, f.Staff, f.Sold[0] + f.Sold[1], c.Index)), "clean", EstateText.Level(EstateCatalogue.CleanTarget[f.Staff]),
                "day", f.InspectDay.ToString()));
            Transform buttons = box.ButtonRow();
            if (f.AskRooms > 0)
            {
                box.Line(EstateText.Fill("reallife_estate_hotel_group", "rooms", f.AskRooms.ToString(), "nights", f.AskNights.ToString(), "rate", EstateText.Money(f.AskRate), "amount", EstateText.Money(f.AskRooms * f.AskNights * f.AskRate),
                    "day", f.AskLapse.ToString()));
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_accept", () => _director.AnswerGroup(slot, true));
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_refuse", () => _director.AnswerGroup(slot, false));
            }
            else if (f.GroupNights > 0)
            {
                box.Line(EstateText.T("reallife_estate_hotel_group_in", "rooms", f.GroupRooms.ToString(), "nights", f.GroupNights.ToString(), "rate", EstateText.Money(f.GroupRate)));
            }
            if (f.OutEnd >= day && f.OutRooms > 0)
            {
                box.Line(EstateText.T("reallife_estate_hotel_out", "rooms", f.OutRooms.ToString(), "day", f.OutEnd.ToString()));
            }
            if (f.PushEnd >= day)
            {
                box.Line(EstateText.T("reallife_estate_hotel_campaign_on", "day", f.PushEnd.ToString(), "share", EstateText.Share(EstateCatalogue.CampaignDemand)));
            }
            else
            {
                box.Line(EstateText.T("reallife_estate_hotel_campaign", "amount", EstateText.Money(HotelRules.CampaignCost(f.Tier, c.Index)), "days", EstateCatalogue.CampaignDays.ToString(), "share", EstateText.Share(EstateCatalogue.CampaignDemand)));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_estate_btn_campaign", () => _director.HotelCampaign(slot));
            }
            Transform more = box.ButtonRow();
            InfoBox.Button(more, InfoBox.Negative, f.Leased ? "reallife_estate_btn_end_lease" : "reallife_estate_btn_sell_hotel", () => _director.GiveUpHotel(slot));
            if (state.Hotels.Count > 1)
            {
                BackButton(more);
            }
        }

        // ---------------------------------------------------------------- Car hire

        private void WriteHire(InfoBox box, EstateState state, int day, ref string selected)
        {
            EstateClimate c = _director.Climate;
            HireFile hire = state.Hire;
            HireCar picked = null;
            VehicleInstance add = null;
            TableRow row;
            for (int i = 0; i < hire.Cars.Count; i++)
            {
                HireCar car = hire.Cars[i];
                int rate = HireRules.Rate(car.Price, car.Rate, c.Index), carClass = HireRules.Class(car.Price);
                if (Pick("car:" + car.Id, ref selected, out row))
                {
                    picked = car;
                }
                row.Kind = carClass;
                row.Kind2 = car.State == (int)HireState.Out ? EstateTables.CarOut : EstateTables.CarIn;
                row.Set(0, EstateGame.CarName(car.Type));
                row.Set(1, EstateText.CarClass(carClass));
                row.Set(2, EstateText.CarState(car.State), car.State == (int)HireState.Waiting ? TableRow.Yellow : TableRow.Plain);
                row.Set(3, EstateText.Money(rate), rate);
                row.Set(4, car.Hires.ToString(), car.Hires);
                row.Set(5, EstateText.Money(car.Takings), car.Takings);
                if (car.State == (int)HireState.Out)
                {
                    row.Set(6, EstateText.T("reallife_estate_row_until", "day", car.Day.ToString()), car.Day);
                }
            }
            if (hire.Running)
            {
                EstateGame.Hireable(hire, _vehicles);
                for (int i = 0; i < _vehicles.Count; i++)
                {
                    VehicleInstance v = _vehicles[i];
                    int price = EstateGame.CarPrice(v), going = HireRules.Going(price, c.Index);
                    if (Pick("add:" + v.id, ref selected, out row))
                    {
                        add = v;
                    }
                    row.Kind = HireRules.Class(price);
                    row.Kind2 = EstateTables.CarNotOnHire;
                    row.NoTotal = true;
                    row.Set(0, EstateGame.CarName(v.vehicleTypeName));
                    row.Set(1, EstateText.CarClass(HireRules.Class(price)));
                    row.Set(2, EstateText.T("reallife_estate_row_on_lot"));
                    row.Set(3, EstateText.Money(going), going);
                }
            }
            if (_rows.Count == 0)
            {
                Say("none", EstateText.T(hire.Running ? "reallife_estate_row_no_cars" : "reallife_estate_row_no_lot"));
            }
            if (picked != null)
            {
                WriteCar(box, hire, picked, day, c);
                return;
            }
            if (add != null)
            {
                string id = add.id;
                int price = EstateGame.CarPrice(add), carClass = HireRules.Class(price);
                box.Title(EstateGame.CarName(add.vehicleTypeName));
                box.Line(EstateText.T("reallife_estate_hire_add_what", "class", EstateText.CarClass(carClass), "amount", EstateText.Money(HireRules.Going(price, c.Index)),
                    "chance", EstateText.Share(HireRules.Demand(carClass, c.Season, c.Event, c.Cycle, EstateCatalogue.Thousand, hire.Rating))));
                box.Line(EstateText.T("reallife_estate_hire_gone_what"));
                box.Line(EstateText.T("reallife_estate_hire_spaces", "count", hire.Cars.Count.ToString(), "spaces", hire.Spaces.ToString()));
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_estate_btn_add_car", () =>
                {
                    if (_director.AddCar(id))
                    {
                        _pick = "car:" + id;
                        Refresh();
                    }
                });
                BackButton(buttons);
                return;
            }
            box.Title(EstateText.T("reallife_estate_tab_hire"));
            box.Line(EstateText.T("reallife_estate_hire_what"));
            if (hire.Running)
            {
                box.Line(EstateText.Pair("reallife_estate_hire_lot", EstateGame.NameOf(hire.Lot)));
                box.Line(EstateText.T("reallife_estate_hire_spaces", "count", hire.Cars.Count.ToString(), "spaces", hire.Spaces.ToString()));
                box.Line(EstateText.T("reallife_estate_hire_rating", "rating", EstateText.Level(hire.Rating)));
                box.Line(EstateText.T("reallife_estate_hire_year", "takings", EstateText.Money(hire.TakingsYear), "costs", EstateText.Money(hire.CostsYear)));
                int premium = 0;
                for (int i = 0; i < hire.Cars.Count; i++)
                {
                    premium += HireRules.Premium(hire.Cars[i].Price, c);
                }
                if (!box.HasSwitch || !box.Switch("reallife_estate_hire_cover", hire.Cover, _director.SetFleetCover))
                {
                    box.Line(EstateText.T(hire.Cover ? "reallife_estate_own_cover_on" : "reallife_estate_own_cover_off"));
                }
                box.Line(EstateText.T("reallife_estate_hire_cover_what", "amount", EstateText.Money(premium), "share", EstateText.Share(EstateCatalogue.HireCoverPays), "excess", EstateText.Money(EstateCatalogue.HireCoverExcess)));
                box.Line(EstateText.T("reallife_estate_hire_how", "metres", EstateCatalogue.HireLotMetres.ToString()));
                if (hire.Count(HireState.Out) + hire.Count(HireState.Waiting) == 0)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_estate_btn_close_lot", () => _director.SetLot(null));
                }
                return;
            }
            EstateGame.Lots(_places);
            if (_places.Count == 0)
            {
                box.Line(EstateText.T("reallife_estate_hire_no_building"));
                return;
            }
            _lot = EstateCatalogue.Clamp(_lot, 0, _places.Count - 1);
            if (!box.HasSelector || !box.Selector(_places.Count, _lot, i => _places[i].Address.ToFormattedString(), i => _lot = i))
            {
                box.Line(_places[_lot].Address.ToFormattedString());
            }
            box.Line(EstateText.T("reallife_estate_hire_lot_what", "spaces", EstateCatalogue.HireSpacesAnyBuilding.ToString(), "metres", EstateCatalogue.HireLotMetres.ToString()));
            InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_estate_btn_open_lot", () =>
            {
                EstateGame.Lots(_places);
                if (_lot >= 0 && _lot < _places.Count)
                {
                    _director.SetLot(EstateGame.Place(_places[_lot].StreetName, _places[_lot].StreetNumber));
                }
            });
        }

        private void WriteCar(InfoBox box, HireFile hire, HireCar car, int day, EstateClimate c)
        {
            string id = car.Id;
            int carClass = HireRules.Class(car.Price);
            box.Title(EstateGame.CarName(car.Type));
            box.Line(EstateText.T("reallife_estate_car_what", "class", EstateText.CarClass(carClass), "price", EstateText.Money(car.Price), "going", EstateText.Money(HireRules.Going(car.Price, c.Index))));
            Steps(box, EstateCatalogue.RateLeast, EstateCatalogue.RateMost, EstateCatalogue.RateStep, car.Rate, rate => _director.SetCarRate(id, rate));
            box.Line(EstateText.T("reallife_estate_car_rate", "amount", EstateText.Money(HireRules.Rate(car.Price, car.Rate, c.Index)),
                "chance", EstateText.Share(HireRules.Demand(carClass, c.Season, c.Event, c.Cycle, car.Rate, hire.Rating)), "rating", EstateText.Level(hire.Rating)));
            switch ((HireState)car.State)
            {
                case HireState.Out:
                    box.Line(EstateText.T("reallife_estate_car_out_what", "day", car.Day.ToString(), "amount", EstateText.Money(car.HireRate), "deposit", EstateText.Money(car.Deposit)));
                    break;
                case HireState.Waiting:
                    box.Line(EstateText.T("reallife_estate_car_waiting_what"));
                    break;
                case HireState.Valet:
                    box.Line(EstateText.T("reallife_estate_car_valet_what"));
                    break;
                default:
                    box.Line(EstateText.T("reallife_estate_car_idle_what", "metres", EstateCatalogue.HireLotMetres.ToString()));
                    break;
            }
            box.Line(EstateText.T("reallife_estate_car_totals", "hires", car.Hires.ToString(), "amount", EstateText.Money(car.Takings)));
            box.Line(EstateText.T("reallife_estate_car_risk", "damage", EstateText.Share(EstateCatalogue.HireDamageChance), "late", EstateText.Share(EstateCatalogue.HireLateChance), "cleaning", EstateText.Money(HireRules.Cleaning(c.Index))));
            Transform buttons = box.ButtonRow();
            if (car.State == (int)HireState.Idle || car.State == (int)HireState.Valet)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_estate_btn_remove_car", () =>
                {
                    _pick = null;
                    _director.RemoveCar(id);
                });
            }
            BackButton(buttons);
        }
    }
}
