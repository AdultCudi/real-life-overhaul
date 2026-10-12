using System;
using System.Collections;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Dialogs;
using Entities;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A hotel stay from the desk to checkout. The game has no hotel interior, so the room is
    /// the travel panel: a picture of the room, what the tier offers, and a button for each
    /// thing one does in a hotel room. The player never moves: they stand at the door (held
    /// there by one of the game's own navigation blocks) behind the panel, and "Go out" takes
    /// the panel away. The room stays theirs until checkout.
    ///
    /// Sleep is the game's own: the same energy regeneration the bed, car seat or bench gives
    /// (by the room), with the bed-quality multiplier, run by the game's time skip. Every other
    /// effect goes through TravelEffects or, when another feature has taken it over, a bridge.
    /// </summary>
    public sealed class HotelStay
    {
        // Panel buttons.
        private const int Sleep = 1, Nap = 2, Wake = 3, Freshen = 4, RoomService = 5, Minibar = 6, Laundry = 7, Rest = 8, Messages = 9,
            GoOut = 10, Extend = 11, Late = 12, CheckOut = 13, Table = 14, Car = 15, Walk = 16, Dinner = 17, Golf = 18, WakeNow = 19,
            ConfirmOut = 20, Back = 21;

        private readonly TravelDirector _director;
        private readonly HotelReception _reception;

        /// <summary>True while the reception's list is open at a hotel's door (the player is in the street, in no room).</summary>
        public bool AtReception => _reception != null && _reception.IsOpen;
        private bool _plain;               // the game's list could not be made: the night is slept at once and the player is back outside
        private bool _sleeping;
        private bool _confirming;

        public HotelStay(TravelDirector director)
        {
            _director = director;
            _reception = new HotelReception(this, director);
        }

        public bool Busy => _sleeping;

        // ---------------------------------------------------------------- figures

        /// <summary>What a stay beginning tonight costs in all, and its average night.</summary>
        public static int Quote(HotelSlot hotel, RoomType room, int nights, out int perNight)
        {
            int first = TravelEffects.StayDay();
            float demand = TravelBridges.Leisure.DemandToday;
            float total = 0f;
            for (int night = 0; night < nights; night++)
            {
                total += TravelCatalogue.NightRate(hotel, room, first + night, TravelEffects.Weekday(first + night), demand);
            }
            total *= TravelCatalogue.LongStayFactor(nights);
            perNight = Mathf.Max(1, Mathf.RoundToInt(total / nights));
            return perNight * nights;
        }

        public static int Tonight(HotelSlot hotel, RoomType room)
        {
            int day = TravelEffects.StayDay();
            return TravelCatalogue.NightRate(hotel, room, day, TravelEffects.Weekday(day), TravelBridges.Leisure.DemandToday);
        }

        public static int FreeTonight(HotelSlot hotel, RoomType room)
        {
            int day = TravelEffects.StayDay();
            return TravelCatalogue.RoomsFree(hotel, room, day, TravelEffects.Weekday(day), TravelBridges.Leisure.DemandToday);
        }

        /// <summary>Nights of the booking that have begun, the one in progress included.</summary>
        public static int NightsBegun()
        {
            return Mathf.Clamp(TravelEffects.StayDay() - TravelState.CheckInDay + 1, 1, Mathf.Max(TravelState.Nights, 1));
        }

        public static int NightsLeft()
        {
            return Mathf.Max(TravelState.Nights - NightsBegun(), 0);
        }

        public static int CheckoutHour => TravelState.LateCheckout ? TravelCatalogue.LateCheckoutHour : TravelCatalogue.CheckoutHour;

        public static bool PastCheckout()
        {
            return TravelState.HasRoom && TimeHelper.IsInThePast(TravelState.CheckoutDay, CheckoutHour);
        }

        // ---------------------------------------------------------------- booking

        /// <summary>
        /// Takes a room from tonight. Up front the whole stay is paid now; on deposit the first
        /// night is, and the rest (5 % dearer) at checkout. A loyalty night comes off the first
        /// night. False with the reason's locale key if it cannot be done.
        /// </summary>
        public bool Book(HotelSite site, RoomType room, int nights, bool deposit, out string failKey)
        {
            failKey = null;
            if (TravelState.HasRoom)
            {
                failKey = "reallife_travel_desk_has_room";
                return false;
            }
            if (FreeTonight(site.Slot, room) <= 0)
            {
                failKey = "reallife_travel_desk_full";
                return false;
            }
            int total = Quote(site.Slot, room, nights, out int perNight);
            int voucher = TravelState.FreeNights > 0 ? Mathf.Min(perNight, TravelCatalogue.FreeNightCap) : 0;
            int now = deposit ? Mathf.Max(perNight - voucher, 0) : Mathf.Max(total - voucher, 0);
            int later = deposit ? Mathf.RoundToInt((total - perNight) * (1f + TravelCatalogue.DepositSurcharge)) : 0;
            if (HotelReception.Owned(site.Slot.Id))
            {
                now = later = perNight = voucher = 0;   // the owner of the hotel stays for nothing (Economy/Estate)
            }
            if (!TravelEffects.Pay(now, "reallife_transaction_hotel"))
            {
                failKey = "reallife_travel_desk_no_money";
                return false;
            }
            if (voucher > 0)
            {
                TravelState.FreeNights--;
            }
            TravelState.ClearRoom();
            TravelState.HotelId = site.Slot.Id;
            TravelState.RoomId = room.Id;
            TravelState.CheckInDay = TravelEffects.StayDay();
            TravelState.Nights = nights;
            TravelState.Paid = now;
            TravelState.Owed = later;
            TravelState.NightRate = perNight;
            _director.Log("Travel: booked " + room.Id + " at " + site.Slot.Id + " for " + nights + " night(s): " + now + " paid, " + later + " owed at checkout.");
            return true;
        }

        /// <summary>The Hamptons weekend's room: paid for with the package.</summary>
        public void BookPackage(HotelSite site, RoomType room, int nights)
        {
            TravelState.ClearRoom();
            TravelState.HotelId = site.Slot.Id;
            TravelState.RoomId = room.Id;
            TravelState.CheckInDay = TravelEffects.StayDay();
            TravelState.Nights = nights;
            TravelState.NightRate = Tonight(site.Slot, room);
            TravelState.Package = true;
        }

        /// <summary>
        /// Gives the room up. Leaving early, prepaid nights not begun come back at the room's
        /// refund share; on deposit only the nights begun are owed. Stamps are earned for nights
        /// begun. Returns the line to show.
        /// </summary>
        public string CheckOutNow(bool atDesk)
        {
            if (!TravelState.HasRoom)
            {
                return null;
            }
            HotelSlot hotel = TravelCatalogue.Hotel(TravelState.HotelId);
            RoomType room = TravelCatalogue.Room(TravelState.RoomId);
            int begun = NightsBegun();
            int unused = PastCheckout() ? 0 : Mathf.Max(TravelState.Nights - begun, 0);
            int refund = 0;
            int owed = TravelState.Owed;
            if (unused > 0 && !TravelState.Package)
            {
                if (owed > 0)
                {
                    owed = Mathf.RoundToInt(Mathf.Max(begun - 1, 0) * TravelState.NightRate * (1f + TravelCatalogue.DepositSurcharge));
                }
                else
                {
                    refund = Mathf.RoundToInt(unused * TravelState.NightRate * room.Refund);
                }
            }
            if (owed > 0)
            {
                TravelEffects.Settle(-owed, "reallife_transaction_hotel");
            }
            if (refund > 0)
            {
                TravelEffects.Settle(refund, "reallife_transaction_hotel_refund");
            }
            bool package = TravelState.Package;
            int slept = TravelState.NightsSlept;
            if (!package)
            {
                TravelState.Stamps += begun;
                while (TravelState.Stamps >= TravelCatalogue.StampsForFreeNight)
                {
                    TravelState.Stamps -= TravelCatalogue.StampsForFreeNight;
                    TravelState.FreeNights++;
                }
            }
            TravelState.NightsEver += begun;
            string name = TravelText.T(hotel.NameKey);
            _director.Log("Travel: checked out of " + hotel.Id + " after " + begun + " night(s): " + owed + " settled, " + refund + " refunded, " + TravelState.Stamps + " stamp(s), " + TravelState.FreeNights + " free night(s).");
            TravelState.ClearRoom();
            _confirming = false;
            if (package)
            {
                _director.Holidays.WeekendOver(slept);
            }
            string line = refund > 0
                ? TravelText.T("reallife_travel_checkout_refund", "hotel", name, "amount", TravelText.Money(refund))
                : owed > 0
                    ? TravelText.T("reallife_travel_checkout_settled", "hotel", name, "amount", TravelText.Money(owed))
                    : TravelText.T(atDesk ? "reallife_travel_checkout_done" : "reallife_travel_checkout_auto", "hotel", name);
            return line;
        }

        /// <summary>Once an hour: a room past its checkout time is given up, wherever the player is.</summary>
        public void HourTick()
        {
            if (!TravelState.HasRoom || _sleeping || !PastCheckout())
            {
                return;
            }
            bool wasIn = TravelState.InRoom;
            if (wasIn)
            {
                LeaveRoom();
            }
            string line = CheckOutNow(false);
            if (line != null)
            {
                TravelText.Toast(NotificationType.Info, line, 10f);
            }
        }

        // ---------------------------------------------------------------- in the room

        /// <summary>Up to the room: the panel comes up over the street. False with a reason if the room cannot be gone up to now.</summary>
        public bool EnterRoom(out TestRefusal why)
        {
            why = TestRefusal.None;
            if (!TravelState.HasRoom)
            {
                why = TestRefusal.NothingFits;
                return false;
            }
            if (PastCheckout())
            {
                HourTick();
                why = TestRefusal.NothingFits;
                return false;
            }
            if (!_director.CanSeclude(out why))
            {
                return false;
            }
            TravelState.InRoom = true;
            _confirming = false;
            _plain = false;
            _director.Seclude(true);
            Morning();
            Show();
            return true;
        }

        /// <summary>Back to the pavement outside. The room is kept.</summary>
        public void LeaveRoom()
        {
            if (_sleeping)
            {
                StopSleep();
            }
            TravelState.InRoom = false;
            _confirming = false;
            _plain = false;
            _director.Seclude(false);
            _director.Window.Close();   // (the list's closing finds the room already left)
        }

        /// <summary>A save made up in the room was loaded: the panel comes back if it can, else the player is simply outside.</summary>
        public void Resume()
        {
            if (TravelState.InRoom && !EnterRoom(out _))
            {
                TravelState.InRoom = false;
            }
        }

        public void Press(int id)
        {
            if (!TravelState.HasRoom || !TravelState.InRoom)
            {
                return;
            }
            HotelSlot hotel = TravelCatalogue.Hotel(TravelState.HotelId);
            RoomType room = TravelCatalogue.Room(TravelState.RoomId);
            if (_sleeping)
            {
                if (id == WakeNow)
                {
                    StopSleep();
                }
                return;
            }
            GameInstance save = SaveGameManager.Current;
            int today = TimeHelper.CurrentDay;
            switch (id)
            {
                case Sleep:
                    _director.Run(SleepRoutine(room, hotel, SleepGoal(), true));
                    return;
                case Nap:
                    _director.Run(SleepRoutine(room, hotel, Capped(TimeHelper.GetTimeInHours(2)), false));
                    return;
                case Wake:
                    TravelState.WakeHour = TravelState.WakeHour >= 10 ? 5 : TravelState.WakeHour + 1;
                    break;
                case Freshen:
                    if (TravelBridges.Home.Freshen(room.Quality))
                    {
                        Say(TravelText.T("reallife_travel_room_fresh"));
                    }
                    else if (TravelState.FreshDay != today)
                    {
                        TravelState.FreshDay = today;
                        TravelEffects.Mood(1);
                        Say(TravelText.T(room.Quality == 0 ? "reallife_travel_room_fresh_shared" : "reallife_travel_room_fresh"));
                    }
                    else
                    {
                        Say(TravelText.T("reallife_travel_room_fresh_again"));
                    }
                    break;
                case RoomService:
                    int meal = MealPrice(hotel.Tier);
                    if (save.Hunger > 92f)
                    {
                        Say(TravelText.T("reallife_travel_room_not_hungry"));
                    }
                    else if (TravelEffects.Pay(meal, "reallife_transaction_hotel_extras"))
                    {
                        TravelEffects.Feed(hotel.Tier == HotelTier.Budget ? 25f : hotel.Tier == HotelTier.Luxury ? 100f : 60f);
                        if (hotel.Tier == HotelTier.Luxury)
                        {
                            TravelEffects.Mood(1);
                        }
                        Say(TravelText.T(hotel.Tier == HotelTier.Budget ? "reallife_travel_room_vending" : hotel.Tier == HotelTier.Luxury ? "reallife_travel_room_service_lux" : "reallife_travel_room_service",
                            "amount", TravelText.Money(meal)));
                    }
                    else
                    {
                        Say(TravelText.T("reallife_travel_room_no_money"));
                    }
                    break;
                case Minibar:
                    if (TravelState.MinibarDay != today)
                    {
                        TravelState.MinibarDay = today;
                        TravelState.MinibarCount = 0;
                    }
                    int snack = hotel.Tier == HotelTier.Luxury ? 18 : 12;
                    if (TravelState.MinibarCount >= 4)
                    {
                        Say(TravelText.T("reallife_travel_room_minibar_empty"));
                    }
                    else if (TravelEffects.Pay(snack, "reallife_transaction_hotel_extras"))
                    {
                        TravelState.MinibarCount++;
                        TravelEffects.Feed(12f);
                        TravelEffects.Energy(3f);
                        Say(TravelText.T("reallife_travel_room_minibar", "amount", TravelText.Money(snack), "left", TravelText.Number(4 - TravelState.MinibarCount)));
                    }
                    else
                    {
                        Say(TravelText.T("reallife_travel_room_no_money"));
                    }
                    break;
                case Laundry:
                    int wash = hotel.Tier == HotelTier.Luxury ? 0 : hotel.Tier == HotelTier.Budget ? 8 : 25;
                    if (TravelState.LaundryDay == today)
                    {
                        Say(TravelText.T("reallife_travel_room_laundry_again"));
                    }
                    else if (TravelEffects.Pay(wash, "reallife_transaction_hotel_extras"))
                    {
                        TravelState.LaundryDay = today;
                        if (!TravelBridges.Home.Laundry(room.Quality))
                        {
                            TravelEffects.Mood(1);
                        }
                        Say(wash > 0 ? TravelText.T("reallife_travel_room_laundry", "amount", TravelText.Money(wash)) : TravelText.T("reallife_travel_room_laundry_free"));
                    }
                    else
                    {
                        Say(TravelText.T("reallife_travel_room_no_money"));
                    }
                    break;
                case Rest:
                    _director.Run(RestRoutine(room));
                    return;
                case Messages:
                    Say(MessagesLine());
                    break;
                case GoOut:
                    LeaveRoom();
                    return;
                case Extend:
                    ExtendStay(hotel, room);
                    break;
                case Late:
                    int fee = hotel.Tier == HotelTier.Luxury ? 0 : Mathf.RoundToInt(TravelState.NightRate * TravelCatalogue.LateCheckoutShare);
                    if (TravelState.LateCheckout)
                    {
                        Say(TravelText.T("reallife_travel_room_late_already"));
                    }
                    else if (TravelEffects.Pay(fee, "reallife_transaction_hotel_extras"))
                    {
                        TravelState.LateCheckout = true;
                        Say(fee > 0
                            ? TravelText.T("reallife_travel_room_late", "amount", TravelText.Money(fee), "time", TravelText.Hour(TravelCatalogue.LateCheckoutHour))
                            : TravelText.T("reallife_travel_room_late_free", "time", TravelText.Hour(TravelCatalogue.LateCheckoutHour)));
                    }
                    else
                    {
                        Say(TravelText.T("reallife_travel_room_no_money"));
                    }
                    break;
                case CheckOut:
                    _confirming = true;
                    break;
                case Back:
                    _confirming = false;
                    break;
                case ConfirmOut:
                    LeaveRoom();
                    string line = CheckOutNow(true);
                    if (line != null)
                    {
                        TravelText.Toast(NotificationType.Success, line, 10f);
                    }
                    return;
                case Table:
                    if (TravelState.DinnerDay == today)
                    {
                        Say(TravelText.T("reallife_travel_room_table_again"));
                    }
                    else if (TravelBridges.Social.TableBooked(TravelText.T(hotel.NameKey)))
                    {
                        TravelState.DinnerDay = today;
                        Say(TravelText.T("reallife_travel_room_table_booked"));
                    }
                    else if (TravelEffects.Pay(Prices.At(85), "reallife_transaction_hotel_extras"))
                    {
                        TravelState.DinnerDay = today;
                        TravelEffects.Feed(100f);
                        TravelEffects.Mood(2);
                        Say(TravelText.T("reallife_travel_room_table", "amount", TravelText.Money(85)));
                    }
                    else
                    {
                        Say(TravelText.T("reallife_travel_room_no_money"));
                    }
                    break;
                case Car:
                    HotelSite site = _director.Hotels.ById(hotel.Id);
                    Say(TravelText.T(site != null && TravelBridges.Transport.CallCar(site.Front) ? "reallife_travel_room_car" : "reallife_travel_room_car_none"));
                    break;
                case Walk:
                    _director.Run(WalkRoutine());
                    return;
                case Dinner:
                    CottageDinner(today);
                    break;
                case Golf:
                    if (TravelBridges.Leisure.PlayGolf())
                    {
                        LeaveRoom();
                        return;
                    }
                    Say(TravelText.T("reallife_travel_room_golf_none"));
                    break;
            }
            Show();
        }

        private void CottageDinner(int today)
        {
            HotelSite site = _director.Hotels.ById(TravelState.HotelId);
            if (TravelState.DinnerDay == today)
            {
                Say(TravelText.T("reallife_travel_room_table_again"));
            }
            else if (site != null && TravelBridges.Leisure.DinnerOut(site.Front))
            {
                TravelState.DinnerDay = today;
                LeaveRoom();
            }
            else if (TravelEffects.Pay(Prices.At(60), "reallife_transaction_hotel_extras"))
            {
                TravelState.DinnerDay = today;
                TravelEffects.Feed(100f);
                TravelEffects.Mood(2);
                Say(TravelText.T("reallife_travel_room_dinner", "amount", TravelText.Money(60)));
            }
            else
            {
                Say(TravelText.T("reallife_travel_room_no_money"));
            }
        }

        private void ExtendStay(HotelSlot hotel, RoomType room)
        {
            int day = TravelState.CheckoutDay;
            if (TravelState.Package)
            {
                Say(TravelText.T("reallife_travel_room_extend_package"));
                return;
            }
            if (TravelCatalogue.RoomsFree(hotel, room, day, TravelEffects.Weekday(day), TravelBridges.Leisure.DemandToday) <= 0)
            {
                Say(TravelText.T("reallife_travel_room_extend_full"));
                return;
            }
            int rate = Mathf.RoundToInt(TravelCatalogue.NightRate(hotel, room, day, TravelEffects.Weekday(day), TravelBridges.Leisure.DemandToday)
                                        * TravelCatalogue.LongStayFactor(TravelState.Nights + 1));
            if (TravelState.Owed > 0)
            {
                TravelState.Owed += Mathf.RoundToInt(rate * (1f + TravelCatalogue.DepositSurcharge));
            }
            else if (!TravelEffects.Pay(rate, "reallife_transaction_hotel"))
            {
                Say(TravelText.T("reallife_travel_room_no_money"));
                return;
            }
            else
            {
                TravelState.Paid += rate;
            }
            TravelState.Nights++;
            TravelState.LateCheckout = false;   // a late checkout bought for the old last day does not move with it
            Say(TravelText.T("reallife_travel_room_extended", "amount", TravelText.Money(rate), "day", TravelEffects.DayName(TravelState.CheckoutDay)));
        }

        private static string CheckoutTerms(RoomType room)
        {
            int begun = NightsBegun();
            int unused = Mathf.Max(TravelState.Nights - begun, 0);
            if (TravelState.Package)
            {
                return TravelText.T("reallife_travel_room_out_package");
            }
            if (TravelState.Owed > 0)
            {
                int owed = unused > 0 ? Mathf.RoundToInt(Mathf.Max(begun - 1, 0) * TravelState.NightRate * (1f + TravelCatalogue.DepositSurcharge)) : TravelState.Owed;
                return TravelText.T("reallife_travel_room_out_owed", "amount", TravelText.Money(owed));
            }
            if (unused == 0)
            {
                return TravelText.T("reallife_travel_room_out_plain");
            }
            int refund = Mathf.RoundToInt(unused * TravelState.NightRate * room.Refund);
            return refund > 0
                ? TravelText.T("reallife_travel_room_out_refund", "nights", TravelText.Number(unused), "amount", TravelText.Money(refund))
                : TravelText.T("reallife_travel_room_out_norefund", "nights", TravelText.Number(unused));
        }

        private static string MessagesLine()
        {
            int unread = 0;
            var contacts = SaveGameManager.Current.Contacts;
            if (contacts != null)
            {
                for (int i = 0; i < contacts.Count; i++)
                {
                    if (contacts[i] != null && contacts[i].messagesQueue != null)
                    {
                        unread += contacts[i].NumberOfUnreadMessages;
                    }
                }
            }
            string stamps = TravelText.T("reallife_travel_room_messages_stamps", "stamps", TravelText.Number(TravelState.Stamps),
                "need", TravelText.Number(TravelCatalogue.StampsForFreeNight), "free", TravelText.Number(TravelState.FreeNights));
            return (unread > 0 ? TravelText.T("reallife_travel_room_messages", "count", TravelText.Number(unread)) : TravelText.T("reallife_travel_room_messages_none")) + " " + stamps;
        }

        private static int MealPrice(HotelTier tier)
        {
            return tier == HotelTier.Budget ? 8 : tier == HotelTier.Luxury ? 65 : tier == HotelTier.Cottage ? 28 : 32;
        }

        // The first time each day the room is come up to in the morning, a good hotel leaves something.
        private void Morning()
        {
            HotelSlot hotel = TravelCatalogue.Hotel(TravelState.HotelId);
            int today = TimeHelper.CurrentDay;
            if (hotel.Tier != HotelTier.Luxury || TravelState.MorningDay == today || TravelState.NightsSlept == 0 || TimeHelper.CurrentHour >= 12)
            {
                return;
            }
            TravelState.MorningDay = today;
            TravelEvent treat = TravelState.Deal(TravelCatalogue.FineMornings, false, () => Random.value);
            TravelEffects.Apply(treat);
            if (treat != null)
            {
                Say(TravelText.T(treat.Key));
            }
        }

        // ---------------------------------------------------------------- the room's list

        private const string ButtonDo = "reallife_travel_list_do";
        private const string ButtonChange = "reallife_travel_list_change";
        private const string ButtonChoose = "reallife_travel_list_choose";

        private Action<int> _onPick;
        private Action _onClosed;

        /// <summary>True while the night is being slept with no list (the game's window could not be copied).</summary>
        public bool PlainSleep => _plain;

        /// <summary>The room's choices in the game's list window, one entry per thing to do with the cost or the effect under its name. Filled again whenever what it says may have changed.</summary>
        private void Show()
        {
            if (!TravelState.HasRoom || !TravelState.InRoom)
            {
                return;
            }
            HotelSlot hotel = TravelCatalogue.Hotel(TravelState.HotelId);
            RoomType room = TravelCatalogue.Room(TravelState.RoomId);
            TravelList list = _director.Window.Take(this);
            if (list == null)
            {
                Plain(room, hotel);
                return;
            }
            if (_sleeping)
            {
                // The game's time skip switches the HUD off, and its own Cancel button is the way to get up: the list is put away until it ends.
                _director.Window.Close();
                return;
            }
            if (_onPick == null)
            {
                _onPick = _director.Pressed;
                _onClosed = RoomClosed;
            }
            float began = Time.realtimeSinceStartup;
            bool opening = !list.IsOpen;
            string subtitle = TravelText.T("reallife_travel_room_subtitle", "room", TravelText.T(room.NameKey), "nights", TravelText.Number(NightsLeft()),
                "day", TravelEffects.DayName(TravelState.CheckoutDay), "time", TravelText.Hour(CheckoutHour));
            string info = subtitle + "\n<size=80%>" + (_confirming ? CheckoutTerms(room) : TravelText.T(BodyKey(hotel.Tier, room))) + "</size>";
            list.Begin(TravelText.T(hotel.NameKey), info);
            int entries;
            if (_confirming)
            {
                list.Add(Back, TravelText.T("reallife_travel_btn_back"), TravelText.T("reallife_travel_list_stay"), ButtonChoose, true);
                list.Add(ConfirmOut, TravelText.T("reallife_travel_btn_checkout_confirm"), TravelText.T("reallife_travel_list_checkout_detail"), ButtonDo, true);
                entries = 2;
            }
            else
            {
                entries = Choices(list, hotel, room);
            }
            list.Open(_onPick, _onClosed);
            if (opening && Meta.Dev.Verbose)
            {
                _director.Log("Travel check (room list): the game's list is open: " + entries + " entries for the " + room.Id + " at " + hotel.Id + ", " + list.Made + " entries made so far; "
                              + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: PASS.");
            }
        }

        // The main page. Each entry says what it costs or does; a button that cannot be used now is off, and its line says why.
        private int Choices(TravelList list, HotelSlot hotel, RoomType room)
        {
            GameInstance save = SaveGameManager.Current;
            int today = TimeHelper.CurrentDay;
            bool budget = hotel.Tier == HotelTier.Budget;
            int count = 0;
            string wake = TravelText.Hour(TravelState.WakeHour);
            list.Add(Sleep, TravelText.T("reallife_travel_btn_sleep", "time", wake), TravelText.T("reallife_travel_list_sleep"), ButtonDo, true); count++;
            list.Add(Nap, TravelText.T("reallife_travel_btn_nap"), TravelText.T("reallife_travel_list_nap"), ButtonDo, true); count++;
            list.Add(Wake, TravelText.T("reallife_travel_btn_wake", "time", wake), TravelText.T("reallife_travel_list_wake"), ButtonChange, true); count++;
            list.Add(Freshen, TravelText.T(room.Quality == 0 ? "reallife_travel_btn_freshen_shared" : "reallife_travel_btn_freshen"), TravelText.T("reallife_travel_list_freshen"), ButtonDo, true); count++;
            bool hungry = save.Hunger <= 92f;
            list.Add(RoomService, TravelText.T(budget ? "reallife_travel_btn_vending" : "reallife_travel_btn_room_service", "amount", TravelText.Money(MealPrice(hotel.Tier))),
                TravelText.T(hungry ? "reallife_travel_list_meal" : "reallife_travel_list_not_hungry"), ButtonDo, hungry); count++;
            if (!budget)
            {
                bool left = TravelState.MinibarDay != today || TravelState.MinibarCount < 4;
                list.Add(Minibar, TravelText.T("reallife_travel_btn_minibar", "amount", TravelText.Money(hotel.Tier == HotelTier.Luxury ? 18 : 12)),
                    TravelText.T(left ? "reallife_travel_list_minibar" : "reallife_travel_list_minibar_empty"), ButtonDo, left); count++;
            }
            bool washed = TravelState.LaundryDay == today;
            list.Add(Laundry, TravelText.T(budget ? "reallife_travel_btn_laundry_coin" : "reallife_travel_btn_laundry"),
                TravelText.T(washed ? "reallife_travel_list_laundry_done" : hotel.Tier == HotelTier.Luxury ? "reallife_travel_list_laundry_free" : "reallife_travel_list_laundry", "amount", TravelText.Money(hotel.Tier == HotelTier.Luxury ? 0 : budget ? 8 : 25)), ButtonDo, !washed); count++;
            list.Add(Rest, TravelText.T("reallife_travel_btn_rest"), TravelText.T("reallife_travel_list_rest"), ButtonDo, true); count++;
            list.Add(Messages, TravelText.T("reallife_travel_btn_messages"), TravelText.T("reallife_travel_list_messages"), ButtonDo, true); count++;
            bool dined = TravelState.DinnerDay == today;
            if (hotel.Tier == HotelTier.Luxury)
            {
                list.Add(Table, TravelText.T("reallife_travel_btn_table"), TravelText.T(dined ? "reallife_travel_list_dined" : "reallife_travel_list_table", "amount", TravelText.Money(85)), ButtonDo, !dined); count++;
                if (TravelBridges.HasTransport)
                {
                    list.Add(Car, TravelText.T("reallife_travel_btn_car"), TravelText.T("reallife_travel_list_car"), ButtonDo, true); count++;
                }
            }
            if (hotel.Tier == HotelTier.Cottage)
            {
                bool walked = TravelState.WalkDay == today;
                list.Add(Walk, TravelText.T("reallife_travel_btn_walk"), TravelText.T(walked ? "reallife_travel_list_walked" : "reallife_travel_list_walk"), ButtonDo, !walked); count++;
                list.Add(Dinner, TravelText.T("reallife_travel_btn_dinner"), TravelText.T(dined ? "reallife_travel_list_dined" : "reallife_travel_list_dinner", "amount", TravelText.Money(60)), ButtonDo, !dined); count++;
                if (TravelBridges.HasLeisure)
                {
                    list.Add(Golf, TravelText.T("reallife_travel_btn_golf"), TravelText.T("reallife_travel_list_golf"), ButtonDo, true); count++;
                }
            }
            bool extend = !TravelState.Package;
            list.Add(Extend, TravelText.T("reallife_travel_btn_extend"),
                extend ? TravelText.T("reallife_travel_list_extend", "amount", TravelText.Money(ExtensionRate(hotel, room))) : TravelText.T("reallife_travel_list_extend_package"), ButtonDo, extend); count++;
            int fee = hotel.Tier == HotelTier.Luxury ? 0 : Mathf.RoundToInt(TravelState.NightRate * TravelCatalogue.LateCheckoutShare);
            list.Add(Late, TravelText.T("reallife_travel_btn_late", "time", TravelText.Hour(TravelCatalogue.LateCheckoutHour)),
                TravelText.T(TravelState.LateCheckout ? "reallife_travel_list_late_done" : "reallife_travel_list_late", "amount", TravelText.Money(fee)), ButtonDo, !TravelState.LateCheckout); count++;
            list.Add(CheckOut, TravelText.T("reallife_travel_btn_checkout"), TravelText.T("reallife_travel_list_checkout"), ButtonChoose, true); count++;
            list.Add(GoOut, TravelText.T("reallife_travel_btn_go_out"), TravelText.T("reallife_travel_list_go_out"), ButtonDo, true); count++;
            return count;
        }

        // What one more night costs at the rate of its day (the figure ExtendStay charges).
        private static int ExtensionRate(HotelSlot hotel, RoomType room)
        {
            int day = TravelState.CheckoutDay;
            return Mathf.RoundToInt(TravelCatalogue.NightRate(hotel, room, day, TravelEffects.Weekday(day), TravelBridges.Leisure.DemandToday)
                                    * TravelCatalogue.LongStayFactor(TravelState.Nights + 1));
        }

        // The list was closed by the player (Cancel, or a menu opened over it): the room is kept, the player is outside.
        private void RoomClosed()
        {
            if (Meta.Dev.Verbose)
            {
                _director.Log("Travel check (room list): closed" + (TravelState.InRoom ? " by the player." : "."));
            }
            if (TravelState.InRoom && !_sleeping)
            {
                LeaveRoom();
            }
        }

        // No window to show: the night is slept, then the player is outside again, so that a stay can never trap anyone.
        private void Plain(RoomType room, HotelSlot hotel)
        {
            if (_sleeping)
            {
                return;
            }
            if (!_plain)
            {
                _plain = true;
                _director.Log("Travel check (room list): the game's list could not be made; the night is slept at once and the other choices are not offered: FAIL.");
                _director.Run(SleepRoutine(room, hotel, SleepGoal(), true));
                return;
            }
            LeaveRoom();
        }

        private static string BodyKey(HotelTier tier, RoomType room)
        {
            switch (room.Id)
            {
                case "dorm": return "reallife_travel_room_body_dorm";
                case "single": return "reallife_travel_room_body_single";
                case "standard": return "reallife_travel_room_body_standard";
                case "deluxe": return "reallife_travel_room_body_deluxe";
                case "king": return "reallife_travel_room_body_king";
                case "suite": return "reallife_travel_room_body_suite";
                case "cottage_whole": return "reallife_travel_room_body_cottage_whole";
                default: return "reallife_travel_room_body_cottage_room";
            }
        }

        /// <summary>A result or a word to the player: the game's own notification.</summary>
        private static void Say(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                TravelText.Toast(NotificationType.Info, text, 8f);
            }
        }

        // ---------------------------------------------------------------- sleep and rest

        // The next time the clock shows the wake-up hour, twelve hours at most, and never past checkout.
        private static Timestamp SleepGoal()
        {
            int day = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;
            int wakeDay = hour >= TravelState.WakeHour ? day + 1 : day;
            var goal = new Timestamp(wakeDay, TravelState.WakeHour, 0f);
            if (goal.GetMinutesFromNow() > 12f * 60f)
            {
                goal = TimeHelper.GetTimeInHours(12);
            }
            return Capped(goal);
        }

        private static Timestamp Capped(Timestamp goal)
        {
            var checkout = new Timestamp(TravelState.CheckoutDay, CheckoutHour, 0f);
            return goal.CompareTo(checkout) > 0 ? checkout : goal;
        }

        private IEnumerator SleepRoutine(RoomType room, HotelSlot hotel, Timestamp goal, bool night)
        {
            float minutes = goal.GetMinutesFromNow();
            if (minutes < 10f)
            {
                Say(TravelText.T("reallife_travel_room_sleep_no_time"));
                Show();
                yield break;
            }
            float before = TimeHelper.NowInMinutes();
            _sleeping = true;
            Show();
            // What the game's own bed does (PlayerActivity.SleepActivity): stop the walking drain, switch the regeneration on, skip time.
            EnergyHelper.RemoveEnergySpender("move");
            EnergyHelper.SetCurrentEnergyRegen(room.Bed == 0 ? EnergyRegen.Bench : room.Bed == 1 ? EnergyRegen.Car : EnergyRegen.Bed);
            EnergyHelper.energyRegenMultiplier = Mathf.Max(0.5f, EnergyHelper.DefaultEnergyRegenMultiplier + room.BedBonus);
            yield return _director.Skip(goal, night ? "reallife_travel_tm_sleep" : "reallife_travel_tm_nap");
            EndSleepEffects();
            _sleeping = false;
            if (!TravelState.HasRoom)
            {
                yield break;   // the watchdog took the stay away meanwhile
            }
            float slept = TimeHelper.NowInMinutes() - before;
            Say(TravelText.T(TravelText.Pick(TravelCatalogue.SleptWell)));
            if (night && slept >= 240f)
            {
                TravelState.NightsSlept++;
                if (room.RoughNight > 0f && Random.value < room.RoughNight)
                {
                    TravelEvent rough = TravelState.Deal(TravelCatalogue.RoughNights, false, () => Random.value);
                    TravelEffects.Apply(rough);
                    if (rough != null && rough.Mood < 0)
                    {
                        ModState.AddModifier(TravelEffects.RoughNight);
                    }
                    if (rough != null)
                    {
                        Say(TravelText.T(rough.Key));
                    }
                }
                else if (room.Quality == 3)
                {
                    ModState.AddModifier(TravelEffects.Comfort);   // lasts into the next day
                    Say(TravelText.T("reallife_travel_room_slept_lux"));
                }
            }
            if (TravelState.InRoom)
            {
                Show();
            }
        }

        private IEnumerator RestRoutine(RoomType room)
        {
            Timestamp goal = Capped(TimeHelper.GetTimeInHours(2));
            if (goal.GetMinutesFromNow() < 10f)
            {
                Say(TravelText.T("reallife_travel_room_sleep_no_time"));
                Show();
                yield break;
            }
            _sleeping = true;
            Show();
            EnergyHelper.RemoveEnergySpender("move");
            EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Bench);
            EnergyHelper.energyRegenMultiplier = EnergyHelper.DefaultEnergyRegenMultiplier;
            yield return _director.Skip(goal, "reallife_travel_tm_rest");
            EndSleepEffects();
            _sleeping = false;
            if (!TravelState.HasRoom)
            {
                yield break;
            }
            int today = TimeHelper.CurrentDay;
            if (!TravelBridges.Home.Rest(2, room.Quality))
            {
                ModState.AddModifier(TravelEffects.Relaxed);
                if (TravelState.RestDay != today)
                {
                    TravelState.Streak = Mathf.Max(TravelState.Streak - 3, 0);   // an afternoon off undoes three days of the grind
                }
            }
            TravelState.RestDay = today;
            Say(TravelText.T("reallife_travel_room_rested"));
            if (TravelState.InRoom)
            {
                Show();
            }
        }

        // The cottages: two hours on the beach, with whatever the tide brings.
        private IEnumerator WalkRoutine()
        {
            int today = TimeHelper.CurrentDay;
            if (TravelState.WalkDay == today)
            {
                Say(TravelText.T("reallife_travel_room_walk_again"));
                Show();
                yield break;
            }
            Timestamp goal = Capped(TimeHelper.GetTimeInHours(2));
            if (goal.GetMinutesFromNow() < 10f)
            {
                Say(TravelText.T("reallife_travel_room_sleep_no_time"));
                Show();
                yield break;
            }
            _sleeping = true;
            Show();
            yield return _director.Skip(goal, "reallife_travel_tm_walk");
            _sleeping = false;
            if (!TravelState.HasRoom)
            {
                yield break;
            }
            TravelState.WalkDay = today;
            TravelEvent found = TravelState.Deal(TravelCatalogue.BeachWalks, false, () => Random.value);
            TravelEffects.Apply(found);
            if (found != null && found.Souvenir)
            {
                TravelState.Souvenirs++;
            }
            Say(found != null ? TravelText.T(found.Key) : null);
            if (TravelState.InRoom)
            {
                Show();
            }
        }

        private void StopSleep()
        {
            var uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
            {
                uis.timeMachine.StopTimeMachine(0f);
            }
        }

        /// <summary>Puts the game's energy regeneration back as it was. Safe to call at any time; the watchdog does on every forced exit.</summary>
        public static void EndSleepEffects()
        {
            try
            {
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                EnergyHelper.energyRegenMultiplier = EnergyHelper.DefaultEnergyRegenMultiplier;
            }
            catch (Exception)
            {
                // the player is gone with the scene: there is nothing to put back
            }
        }

        /// <summary>The watchdog found the stay in a state it cannot be in: the player is simply outside again.</summary>
        public void ForceOut()
        {
            _sleeping = false;
            _confirming = false;
            _plain = false;
            TravelState.InRoom = false;
            EndSleepEffects();
        }

        // ---------------------------------------------------------------- the desk

        /// <summary>Reception: the game's list of rooms; if that window cannot be made, the game's own dialogue panel asks the same things one at a time.</summary>
        public void OpenDesk(HotelSite site)
        {
            if (_reception.Open(site))
            {
                return;
            }
            HotelDeskDialog.Site = site;
            HotelDeskDialog.Stay = this;
            HotelDeskDialog.Booked = false;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash("reallife_hoteldesk_calldialogtype");
            CallDialogFactory.RegisterDialog(dialogType, () => new HotelDeskDialog());
            InstanceBehavior<UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog, null, AfterDesk);
        }

        // The conversation is over: with a room just taken, straight up to it.
        private void AfterDesk()
        {
            if (HotelDeskDialog.Booked && TravelState.HasRoom && TravelDirector.Instance != null)
            {
                HotelDeskDialog.Booked = false;
                _director.Run(EnterSoon());
            }
        }

        /// <summary>A room was just booked at the list: up to it once the list has closed.</summary>
        public void GoUpSoon()
        {
            _director.Run(EnterSoon());
        }

        private IEnumerator EnterSoon()
        {
            yield return new WaitForSecondsRealtime(0.4f);   // the dialogue panel or the list closes first
            EnterRoom(out _);
        }
    }

    /// <summary>
    /// Reception: the rooms one at a time with tonight's rate and whether any are left, then
    /// how many nights, then how to pay. Three buttons at most, as the game's dialogues have.
    /// </summary>
    public class HotelDeskDialog : Dialog
    {
        internal static HotelSite Site;
        internal static HotelStay Stay;
        internal static bool Booked;

        public HotelDeskDialog()
        {
            npcNameKey = Site != null ? Site.Slot.NameKey : "reallife_travel_desk_name";
            if (Site == null || Stay == null || !Settings.Hotels)
            {
                DialogController.current.ShowEntry(Say(TravelText.T("reallife_travel_desk_closed")));
                return;
            }
            if (TravelState.HasRoom)
            {
                HotelSlot other = TravelCatalogue.Hotel(TravelState.HotelId);
                DialogController.current.ShowEntry(Say(TravelText.T("reallife_travel_desk_has_room", "hotel", TravelText.T(other.NameKey))));
                return;
            }
            string greeting = TravelText.T(TravelText.Pick(Site.Slot.Tier == HotelTier.Budget ? TravelCatalogue.HostelGreetings : TravelCatalogue.DeskGreetings), "hotel", Site.Name);
            DialogController.current.ShowEntry(Room(0, greeting + " "));
        }

        private static DialogEntry Say(string text)
        {
            return PhoneContacts.Closing("reallife_travel_say", new { text });
        }

        private static DialogEntry Room(int index, string lead)
        {
            RoomType[] rooms = TravelCatalogue.Rooms(Site.Slot.Tier);
            RoomType room = rooms[index % rooms.Length];
            int next = (index + 1) % rooms.Length;
            int free = HotelStay.FreeTonight(Site.Slot, room);
            int tonight = HotelStay.Tonight(Site.Slot, room);
            int week = HotelStay.Quote(Site.Slot, room, 7, out _);
            int today = TravelEffects.StayDay();
            string line = free > 0
                ? TravelText.T("reallife_travel_desk_room", "room", TravelText.T(room.NameKey), "rate", TravelText.Money(tonight), "week", TravelText.Money(week), "free", TravelText.Number(free))
                : TravelText.T("reallife_travel_desk_room_full", "room", TravelText.T(room.NameKey));
            if (free > 0 && TravelCatalogue.WeekendNight(TravelEffects.Weekday(today)))
            {
                line += " " + TravelText.T("reallife_travel_desk_weekend");
            }
            if (free > 0 && TravelState.FreeNights > 0)
            {
                line += " " + TravelText.T("reallife_travel_desk_voucher");
            }
            var entry = new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new { text = lead + line }),
                OnCancel = DialogController.current.CancelDialog
            };
            if (free > 0)
            {
                entry.ConfirmTextOverride = "reallife_travel_desk_take".Localize();
                entry.OnConfirm = () => Nights(room, 0);
                entry.SecondOptionTextOverride = "reallife_travel_desk_other";
                entry.OnSecondOption = () => Room(next, string.Empty);
            }
            else
            {
                entry.ConfirmTextOverride = "reallife_travel_desk_other".Localize();
                entry.OnConfirm = () => Room(next, string.Empty);
            }
            return entry;
        }

        private static DialogEntry Nights(RoomType room, int choice)
        {
            int nights = TravelCatalogue.NightChoices[choice % TravelCatalogue.NightChoices.Length];
            int total = HotelStay.Quote(Site.Slot, room, nights, out int perNight);
            int voucher = TravelState.FreeNights > 0 ? Mathf.Min(perNight, TravelCatalogue.FreeNightCap) : 0;
            string line = TravelText.T(nights >= 7 ? "reallife_travel_desk_nights_long" : "reallife_travel_desk_nights",
                "nights", TravelText.Number(nights), "total", TravelText.Money(Mathf.Max(total - voucher, 0)),
                "day", TravelEffects.DayName(TravelEffects.StayDay() + nights), "night", TravelText.Money(perNight));
            return new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new { text = line }),
                ConfirmTextOverride = "reallife_travel_desk_book".Localize(new { nights = TravelText.Number(nights) }),
                OnConfirm = () => Pay(room, nights),
                SecondOptionTextOverride = "reallife_travel_desk_more_nights",
                OnSecondOption = () => Nights(room, choice + 1),
                OnCancel = DialogController.current.CancelDialog
            };
        }

        private static DialogEntry Pay(RoomType room, int nights)
        {
            int total = HotelStay.Quote(Site.Slot, room, nights, out int perNight);
            int voucher = TravelState.FreeNights > 0 ? Mathf.Min(perNight, TravelCatalogue.FreeNightCap) : 0;
            if (nights == 1)
            {
                return Finish(room, nights, false);
            }
            int later = Mathf.RoundToInt((total - perNight) * (1f + TravelCatalogue.DepositSurcharge));
            string line = TravelText.T("reallife_travel_desk_pay", "total", TravelText.Money(Mathf.Max(total - voucher, 0)),
                "deposit", TravelText.Money(Mathf.Max(perNight - voucher, 0)), "later", TravelText.Money(later));
            return new DialogEntry
            {
                messageData = "reallife_travel_say".Localize(new { text = line }),
                ConfirmTextOverride = "reallife_travel_desk_pay_now".Localize(),
                OnConfirm = () => Finish(room, nights, false),
                SecondOptionTextOverride = "reallife_travel_desk_pay_deposit",
                OnSecondOption = () => Finish(room, nights, true),
                OnCancel = DialogController.current.CancelDialog
            };
        }

        private static DialogEntry Finish(RoomType room, int nights, bool deposit)
        {
            if (!Stay.Book(Site, room, nights, deposit, out string failKey))
            {
                return Say(TravelText.T(failKey ?? "reallife_travel_desk_closed", "hotel", Site.Name));
            }
            Booked = true;
            return Say(TravelText.T("reallife_travel_desk_booked", "room", TravelText.T(room.NameKey),
                "day", TravelEffects.DayName(TravelState.CheckoutDay), "time", TravelText.Hour(TravelCatalogue.CheckoutHour)));
        }
    }
}
