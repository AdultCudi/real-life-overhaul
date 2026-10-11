using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The hotel's and the hire business's days and what the player can do in them (HotelRules, HireRules), with the
    /// money that goes with each. A hotel is one night's sums, once a game day; a hire car is a few comparisons a
    /// day, and on the hour only while it is out or due back. Nothing here runs between those.
    /// </summary>
    public sealed partial class EstateDirector
    {
        // ---------------------------------------------------------------- the hotels

        private void ReadHotels(int day)
        {
            for (int i = 0; i < _state.Hotels.Count; i++)
            {
                EstateGame.ReadHotel(_state.Hotels[i], day);
            }
        }

        /// <summary>True if the player holds the hotel with this id of Travel's: its reception then has the owner's entry and the room is free.</summary>
        public static bool OwnsHotel(string slot)
        {
            EstateDirector d = Instance;
            return d != null && d.On(EstatePart.Hotel) && d._state != null && d._state.Hotel(slot) != null;
        }

        /// <summary>The Property app at a hotel's books (the reception's owner's entry).</summary>
        public static void OpenBooks(string slot)
        {
            Instance?.Open(TabHotel, "hot:" + slot);
        }

        // The night that has just ended at every hotel.
        private void HotelDay(int day)
        {
            int night = day - 1;
            EstateClimate c = EstateGame.Climate(night);
            for (int i = 0; i < _state.Hotels.Count; i++)
            {
                HotelFile f = _state.Hotels[i];
                if (!EstateGame.ReadHotel(f, night))
                {
                    continue;   // Travel no longer has it: it is kept and stands still
                }
                Night(f, night, c);
            }
            ReadHotels(day);
        }

        private void Night(HotelFile f, int night, EstateClimate c)
        {
            string pick = "hot:" + f.Slot;
            if (f.InspectDay == 0)
            {
                f.InspectDay = night + EstateCatalogue.InspectionEvery;
            }
            HotelNight n = HotelRules.Night(f, _state.Seed, night, c);
            Pay(n.Takings, EstateGame.HotelTakingsKey, f.Name, null, n.Sold0 + " and " + n.Sold1 + " room(s) sold");
            Cost(n.Costs + n.Lease, EstateGame.HotelCostsKey, f.Name, null, "the night's staff, laundry, breakfast and utilities" + (n.Lease > 0 ? ", and the day's lease of " + n.Lease : string.Empty), true);
            if (n.Pipe)
            {
                Cost(n.PipeCost, EstateGame.HotelCostsKey, f.Name, null, "a burst pipe", true);
                Tell(NotificationType.Error, "reallife_estate_note_pipe", Data("hotel", f.Name, "amount", EstateText.Money(n.PipeCost), "rooms", f.OutRooms.ToString()), TabHotel, pick);
            }
            if (n.Inspection == 2)
            {
                Cost(n.Fine, EstateGame.HotelCostsKey, f.Name, null, "the inspector's fine", true, false);
                Tell(NotificationType.Error, "reallife_estate_note_inspection_failed", Data("hotel", f.Name, "amount", EstateText.Money(n.Fine)), TabHotel, pick);
            }
            else if (n.Inspection == 1)
            {
                Tell(NotificationType.Success, "reallife_estate_note_inspection_passed", Data("hotel", f.Name), TabHotel, pick);
            }
            if (n.GroupAsks)
            {
                Tell(NotificationType.Info, "reallife_estate_note_group", Data("hotel", f.Name, "rooms", f.AskRooms.ToString(), "nights", f.AskNights.ToString()), TabHotel, pick);
            }
            if (n.BadReview)
            {
                Tell(NotificationType.Warning, "reallife_estate_note_bad_review", Data("hotel", f.Name), TabHotel, pick);
            }
            else if (n.Full && c.Event != 0)
            {
                Tell(NotificationType.Success, "reallife_estate_note_full_house", Data("hotel", f.Name, "amount", EstateText.Money(n.Takings)), TabHotel, pick);
            }
            if (Verbose)
            {
                int rooms = HotelRules.Rooms(f.Tier);
                Logger?.Info("Estate check (hotel night " + night + ", " + f.Name + "): " + (n.Sold0 + n.Sold1) + " of " + rooms + " room(s) sold at rates of " + f.Rate[0] + " and " + f.Rate[1] + " against going rates of "
                             + f.Going[0] + " and " + f.Going[1] + " (season " + c.Season + ", weekday " + c.Weekday + ", event " + c.Event + ", cycle " + c.Cycle + "), takings " + n.Takings + ", costs " + n.Costs + ", lease "
                             + n.Lease + ", clean " + f.Clean + ", rating " + f.Rating + ": " + (n.Sold0 + n.Sold1 <= rooms && n.Takings >= 0 ? "PASS" : "FAIL"));
            }
        }

        /// <summary>A hotel is bought as a going concern, or leased for a deposit and a daily lease.</summary>
        public void TakeHotel(string slotId, bool leased)
        {
            HotelSlot slot = On(EstatePart.Hotel) && Save != null ? EstateGame.Slot(slotId) : null;
            if (slot == null || _state.Hotel(slotId) != null || _state.Hotels.Count >= EstateCatalogue.HotelsMost)
            {
                return;
            }
            int day = Save.Day, tier = (int)slot.Tier;
            int price = EstateGame.HotelPrice(slot, _state.Real, _climate.DaysPerYear);
            int pay = leased ? HotelRules.LeaseDeposit(tier, price, _climate.DaysPerYear) : price;
            string name = EstateGame.HotelName(slot);
            if (!Cost(pay, EstateGame.HotelBuyKey, name, null, leased ? "the deposit on a lease of a hotel asked " + price + " for" : "a hotel bought as a going concern", false, false))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            var f = new HotelFile { Slot = slotId, Leased = leased, Paid = pay, Day = day, LeaseDaily = leased ? HotelRules.LeaseDaily(tier, price, _climate.DaysPerYear) : 0, InspectDay = day + EstateCatalogue.InspectionEvery };
            _state.Hotels.Add(f);
            EstateGame.ReadHotel(f, day);
            Tell(NotificationType.Success, leased ? "reallife_estate_note_hotel_leased" : "reallife_estate_note_hotel_bought", Data("hotel", name, "amount", EstateText.Money(pay)), TabHotel, "hot:" + slotId);
            Changed();
        }

        /// <summary>A hotel is sold back for a little under what it is asked for now, or a lease is ended and its deposit comes back.</summary>
        public void GiveUpHotel(string slotId)
        {
            HotelFile f = On(EstatePart.Hotel) && Save != null ? _state.Hotel(slotId) : null;
            HotelSlot slot = f != null ? EstateGame.Slot(slotId) : null;
            if (f == null)
            {
                return;
            }
            int back = f.Leased || slot == null ? f.Paid : EstateCatalogue.Share(EstateGame.HotelPrice(slot, _state.Real, _climate.DaysPerYear), EstateCatalogue.HotelSellShare);
            Pay(back, EstateGame.HotelSoldKey, f.Name, null, f.Leased ? "a lease ended: its deposit" : "a hotel sold back");
            _state.Hotels.Remove(f);
            Tell(NotificationType.Info, "reallife_estate_note_hotel_gone", Data("hotel", f.Name, "amount", EstateText.Money(back)), TabHotel, null);
            Changed();
        }

        public void SetRoomRate(string slotId, int type, int rate)
        {
            HotelFile f = On(EstatePart.Hotel) ? _state.Hotel(slotId) : null;
            if (f != null && type >= 0 && type < f.Rate.Length)
            {
                f.Rate[type] = EstateCatalogue.Clamp(rate, EstateCatalogue.RateLeast, EstateCatalogue.RateMost);
                Changed();
            }
        }

        public void SetHotelStaff(string slotId, int level)
        {
            HotelFile f = On(EstatePart.Hotel) ? _state.Hotel(slotId) : null;
            if (f != null)
            {
                f.Staff = EstateCatalogue.Clamp(level, 0, EstateCatalogue.StaffLevels - 1);
                Changed();
            }
        }

        /// <summary>A week's campaign for a hotel, paid for now.</summary>
        public void HotelCampaign(string slotId)
        {
            HotelFile f = On(EstatePart.Hotel) && Save != null ? _state.Hotel(slotId) : null;
            if (f == null || f.PushEnd >= Save.Day)
            {
                return;
            }
            if (!Cost(HotelRules.CampaignCost(f.Tier, _climate.Index), EstateGame.HotelCostsKey, f.Name, null, "a week's campaign"))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            f.PushEnd = Save.Day + EstateCatalogue.CampaignDays - 1;
            Changed();
        }

        public void AnswerGroup(string slotId, bool accept)
        {
            HotelFile f = On(EstatePart.Hotel) ? _state.Hotel(slotId) : null;
            if (f == null || f.AskRooms <= 0)
            {
                return;
            }
            if (accept)
            {
                HotelRules.AcceptGroup(f);
            }
            else
            {
                HotelRules.RefuseGroup(f);
            }
            Changed();
        }

        partial void HotelAlerts(int day)
        {
            for (int i = 0; i < _state.Hotels.Count; i++)
            {
                HotelFile f = _state.Hotels[i];
                int id = EstateRules.Hash(f.Slot);
                if (f.AskRooms > 0)
                {
                    Alert(10, id, Math.Max(day, f.AskLapse), EstateText.T("reallife_estate_plan_group_title", "hotel", f.Name),
                        EstateText.T("reallife_estate_plan_group_what", "rooms", f.AskRooms.ToString(), "nights", f.AskNights.ToString()),
                        EstateText.T("reallife_planner_pays", "amount", EstateText.Money(f.AskRooms * f.AskNights * f.AskRate)), EstateText.T("reallife_estate_plan_group_missed"));
                }
                if (f.InspectDay > 0 && f.InspectDay - day <= 3 && f.Clean < EstateCatalogue.InspectionPass)
                {
                    Alert(11, id, Math.Max(day, f.InspectDay), EstateText.T("reallife_estate_plan_inspection_title", "hotel", f.Name), EstateText.T("reallife_estate_plan_inspection_what"), null, null);
                }
            }
        }

        // ---------------------------------------------------------------- car hire

        private string _damageNext;   // a test tool has asked that this car comes back damaged

        // Once when the hire part is on at a start: a car that is out and has an object in the city again (a save was
        // loaded: the game makes every vehicle on its list) is taken out again; one the save no longer has is dropped.
        private void HireReconcile()
        {
            HireFile hire = _state.Hire;
            int hidden = 0, dropped = 0;
            hire.Spaces = hire.Running ? EstateGame.Spaces(hire.Lot) : 0;
            for (int i = hire.Cars.Count - 1; i >= 0; i--)
            {
                HireCar car = hire.Cars[i];
                if (EstateGame.Vehicle(car.Id) == null)
                {
                    hire.Cars.RemoveAt(i);
                    dropped++;
                }
                else if (car.State == (int)HireState.Out && !EstateGame.IsOut(car))
                {
                    hidden += EstateGame.HideAgain(car) ? 1 : 0;
                }
            }
            if (Verbose || hidden > 0 || dropped > 0)
            {
                Logger?.Info("Estate check (hire cars at a start): " + hire.Cars.Count + " on the books, " + hire.Count(HireState.Out) + " out, " + hidden + " taken out of the city again after a load, " + dropped
                             + " no longer the player's and dropped: PASS");
            }
        }

        // The hire part stops: every car that is out is called back. It is in the city again at once where its space is
        // clear, and on the hour when it is not. Nothing is charged for a hire cut short, and its deposit goes back.
        private void RecallCars(string why)
        {
            HireFile hire = _state != null ? _state.Hire : null;
            int back = 0, waiting = 0;
            for (int i = 0; hire != null && Save != null && i < hire.Cars.Count; i++)
            {
                HireCar car = hire.Cars[i];
                if (car.State != (int)HireState.Out && car.State != (int)HireState.Waiting)
                {
                    continue;
                }
                if (car.State == (int)HireState.Out)
                {
                    Cost(car.Deposit, EstateGame.HireDepositKey, EstateGame.CarName(car.Type), null, "a hire cut short: the deposit goes back", true, false);
                    car.HireDays = car.HireRate = car.Deposit = car.HireSeed = 0;
                    car.State = (int)HireState.Waiting;
                    car.Day = Save.Day;
                }
                if (car.HireDays == 0 && EstateGame.BringBack(car, default(HireReturn), Save.Day, true))
                {
                    car.State = (int)HireState.Idle;
                    back++;
                }
                else
                {
                    waiting++;
                }
            }
            if (back > 0 || waiting > 0)
            {
                Logger?.Info("Estate check (hire cars called back, " + why + "): " + back + " in the city again, " + waiting + " waiting for a clear space (each is on the save's list of vehicles all the while): PASS");
            }
        }

        private void HireDay(int day)
        {
            HireFile hire = _state.Hire;
            bool on = On(EstatePart.Hire) && hire.Running;
            if (hire.Running)
            {
                hire.Spaces = EstateGame.Spaces(hire.Lot);
                if (hire.Spaces == 0)
                {
                    hire.Lot = string.Empty;   // the lot is no longer held: nothing more goes out, what is out comes back when due
                    on = false;
                    Tell(NotificationType.Warning, "reallife_estate_note_lot_gone", null, TabHire, null);
                }
            }
            if (on && hire.Cover && hire.Cars.Count > 0)
            {
                int premium = 0;
                for (int i = 0; i < hire.Cars.Count; i++)
                {
                    premium += HireRules.Premium(hire.Cars[i].Price, _climate);
                }
                if (Cost(premium, EstateGame.HireCostsKey, EstateText.T("reallife_estate_hire_cover"), null, "a day of fleet cover for " + hire.Cars.Count + " car(s)"))
                {
                    hire.CostsYear += premium;
                }
                else
                {
                    hire.Cover = false;
                    Tell(NotificationType.Warning, "reallife_estate_note_fleet_cover_lapsed", null, TabHire, null);
                }
            }
            int out0 = hire.Count(HireState.Out);
            for (int i = hire.Cars.Count - 1; i >= 0; i--)
            {
                HireCar car = hire.Cars[i];
                if (EstateGame.Vehicle(car.Id) == null)
                {
                    hire.Cars.RemoveAt(i);   // sold, seized or taken back by a lender: it is off the books
                    continue;
                }
                switch ((HireState)car.State)
                {
                    case HireState.Valet:
                        car.State = (int)HireState.Idle;
                        break;
                    case HireState.Out:
                        if (day >= car.Day)
                        {
                            Due(car, day, false);
                        }
                        break;
                    case HireState.Waiting:
                        Due(car, day, false);
                        break;
                    default:
                        if (on && i < hire.Spaces && HireRules.TryHire(car, _state.Seed, day, _climate, hire.Rating))
                        {
                            GoOut(car, day, false);
                        }
                        break;
                }
            }
            if (Verbose && hire.Cars.Count > 0)
            {
                int outNow = hire.Count(HireState.Out);
                Logger?.Info("Estate check (hire, day " + day + "): " + outNow + " of " + hire.Cars.Count + " car(s) out (" + out0 + " this morning), " + hire.Spaces + " space(s) at the lot, rating " + hire.Rating + ": "
                             + (outNow <= hire.Cars.Count ? "PASS" : "FAIL"));
            }
        }

        // A hire has been agreed by the rules: the car leaves if it can. If it cannot (somebody is standing by it, it has
        // been moved or driven), the customer goes elsewhere and nothing is charged.
        private bool GoOut(HireCar car, int day, bool force)
        {
            HireFile hire = _state.Hire;
            if (!EstateGame.TakeOut(car, hire.Lot, force))
            {
                car.State = (int)HireState.Idle;
                car.HireDays = car.HireRate = car.Deposit = car.HireSeed = car.Day = 0;
                return false;
            }
            string name = EstateGame.CarName(car.Type);
            Pay(car.Deposit, EstateGame.HireDepositKey, name, null, "a customer's deposit, held until the car is back");
            hire.Rating = HireRules.Rated(hire.Rating, EstateGame.GoodOrder(car.Id, day));
            if (Verbose)
            {
                Logger?.Info("Estate check (a hire begins, " + name + "): out for " + car.HireDays + " day(s) at " + car.HireRate + " a day against a going rate of " + HireRules.Going(car.Price, _climate.Index)
                             + ", deposit " + car.Deposit + "; its object is out of the city and its entry is on the save's list: " + (EstateGame.IsOut(car) ? "PASS" : "FAIL"));
            }
            return true;
        }

        // A car is due back: the hire is settled and the car is in the city again, or (its space taken, the player standing
        // there) it waits for the next hour. Rarely it does not come back at all.
        private void Due(HireCar car, int day, bool force)
        {
            HireFile hire = _state.Hire;
            string name = EstateGame.CarName(car.Type);
            if (car.HireDays == 0)
            {
                // called back: nothing to settle
                if (EstateGame.BringBack(car, default(HireReturn), day, force))
                {
                    car.State = (int)HireState.Idle;
                }
                return;
            }
            HireReturn r = HireRules.End(car, hire.Rating, hire.Cover, _climate, _damageNext == car.Id);
            if (r.Stolen)
            {
                Pay(r.Rent, EstateGame.HireRentKey, name, null, "the hire of a car that did not come back");
                Pay(r.Payout, EstateGame.HireClaimKey, name, null, "fleet cover for a car that did not come back");
                hire.TakingsYear += r.Rent;
                EstateGame.Lose(car);
                hire.Cars.Remove(car);
                Tell(NotificationType.Error, r.Payout > 0 ? "reallife_estate_note_car_stolen_covered" : "reallife_estate_note_car_stolen", Data("car", name, "amount", EstateText.Money(r.Payout), "kept", EstateText.Money(r.Kept)),
                    TabHire, null);
                Logger?.Info("Estate: a hire car (" + name + ", id " + car.Id + ") was not brought back; it is off the save's list of vehicles by the game's own removal, cover paid " + r.Payout + ".");
                return;
            }
            if (!EstateGame.BringBack(car, r, day, force))
            {
                car.State = (int)HireState.Waiting;
                return;
            }
            _damageNext = _damageNext == car.Id ? null : _damageNext;
            Pay(r.Rent, EstateGame.HireRentKey, name, null, r.Days + " day(s) of hire" + (r.Late ? ", the last one late" : string.Empty));
            Cost(car.Deposit - r.Kept, EstateGame.HireDepositKey, name, null, "the deposit goes back" + (r.Kept > 0 ? ", less " + r.Kept + " kept against damage" : string.Empty), true, false);
            Cost(r.Cleaning, EstateGame.HireCostsKey, name, null, "cleaning after a hire", true);
            Pay(r.Payout, EstateGame.HireClaimKey, name, null, "fleet cover for damage of " + r.Repair + " over the deposit");
            hire.TakingsYear += r.Rent;
            hire.CostsYear += r.Cleaning;
            HireRules.Settle(car, r);
            if (r.Damage > 0)
            {
                Tell(NotificationType.Warning, "reallife_estate_note_car_damaged", Data("car", name, "amount", EstateText.Money(r.Repair), "kept", EstateText.Money(r.Kept + r.Payout)), TabHire, "car:" + car.Id);
            }
            else if (r.Late)
            {
                Tell(NotificationType.Info, "reallife_estate_note_car_late", Data("car", name, "amount", EstateText.Money(r.Rent)), TabHire, "car:" + car.Id);
            }
            if (Verbose)
            {
                Logger?.Info("Estate check (a hire ends, " + name + "): " + r.Days + " day(s), " + r.Km + " km, fuel at " + r.Fuel / 10 + "% of a tank at most, damage " + r.Damage / 10 + "% (repair reckoned at " + r.Repair
                             + ", " + r.Kept + " of the deposit kept, cover paid " + r.Payout + "), rent " + r.Rent + ", cleaning " + r.Cleaning + "; it stands where it stood: " + (!EstateGame.IsOut(car) ? "PASS" : "FAIL"));
            }
        }

        // On the hour: a car that is out is kept legally parked on paper, and one that waits for its space tries again.
        private void HireHour(int day)
        {
            HireFile hire = _state.Hire;
            bool changed = false;
            for (int i = hire.Cars.Count - 1; i >= 0; i--)
            {
                HireCar car = hire.Cars[i];
                if (car.State == (int)HireState.Out)
                {
                    EstateGame.KeepLegal(car);
                }
                else if (car.State == (int)HireState.Waiting)
                {
                    Due(car, day, false);
                    changed |= i >= hire.Cars.Count || hire.Cars[i].State != (int)HireState.Waiting;
                }
            }
            if (changed)
            {
                Changed();
            }
        }

        /// <summary>The hire business is run from a building the player holds. It can move only while no car is out.</summary>
        public void SetLot(string place)
        {
            HireFile hire = _state != null ? _state.Hire : null;
            if (!On(EstatePart.Hire) || hire == null || hire.Count(HireState.Out) + hire.Count(HireState.Waiting) > 0)
            {
                return;
            }
            hire.Lot = place ?? string.Empty;
            hire.Spaces = hire.Running ? EstateGame.Spaces(hire.Lot) : 0;
            Changed();
        }

        /// <summary>A vehicle of the player's goes on the books. False, with the reason told, if it cannot.</summary>
        public bool AddCar(string id)
        {
            HireFile hire = _state != null ? _state.Hire : null;
            VehicleInstance v = On(EstatePart.Hire) && hire != null && hire.Running ? EstateGame.Vehicle(id) : null;
            if (v == null || hire.Car(id) != null)
            {
                return false;
            }
            string why;
            if (hire.Cars.Count >= Math.Min(hire.Spaces, EstateCatalogue.HireCarsMost))
            {
                Notifications.ShowError("reallife_estate_hire_no_space");
                return false;
            }
            if (!EstateGame.MayHire(v, hire.Lot, out why))
            {
                Notifications.ShowError(why);
                return false;
            }
            hire.Cars.Add(new HireCar { Id = id, Type = v.vehicleTypeName ?? string.Empty, Price = EstateGame.CarPrice(v) });
            Changed();
            return true;
        }

        /// <summary>A car that stands on the lot comes off the books.</summary>
        public void RemoveCar(string id)
        {
            HireFile hire = _state != null ? _state.Hire : null;
            HireCar car = hire != null ? hire.Car(id) : null;
            if (car != null && (car.State == (int)HireState.Idle || car.State == (int)HireState.Valet))
            {
                hire.Cars.Remove(car);
                Changed();
            }
        }

        public void SetCarRate(string id, int rate)
        {
            HireCar car = On(EstatePart.Hire) ? _state.Hire.Car(id) : null;
            if (car != null)
            {
                car.Rate = EstateCatalogue.Clamp(rate, EstateCatalogue.RateLeast, EstateCatalogue.RateMost);
                Changed();
            }
        }

        public void SetFleetCover(bool on)
        {
            if (On(EstatePart.Hire))
            {
                _state.Hire.Cover = on;
                Changed();
            }
        }

        partial void HireAlerts(int day)
        {
            HireFile hire = _state.Hire;
            for (int i = 0; i < hire.Cars.Count; i++)
            {
                HireCar car = hire.Cars[i];
                if (car.State == (int)HireState.Waiting)
                {
                    Alert(12, EstateRules.Hash(car.Id), day, EstateText.T("reallife_estate_plan_car_title", "car", EstateGame.CarName(car.Type)), EstateText.T("reallife_estate_plan_car_what"), null, null);
                }
            }
        }
    }
}
