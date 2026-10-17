using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The test entries on the mod's options page.</summary>
    public sealed partial class TravelDirector
    {
        private bool TestReady(string test, bool featureOn)
        {
            TestRefusal why = !_loaded || _failed ? TestRefusal.NotLoaded : !featureOn ? TestRefusal.FeatureOff : TestRefusal.None;
            if (why == TestRefusal.None)
            {
                CanSeclude(out why);
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            return true;
        }

        private void Refuse(string test, string key, string detail)
        {
            Logger?.Info(test + " test: not started (" + detail + ").");
            Notifications.Show(NotificationType.Warning, key, null, 8f);
        }

        /// <summary>Takes the cheapest free room of the nearest hotel of a tier for one night, at its real price, and goes up to it.</summary>
        public void TestCheckIn(HotelTier tier)
        {
            const string test = "Hotel check-in";
            if (!TestReady(test, Settings.Hotels))
            {
                return;
            }
            if (TravelState.HasRoom)
            {
                Refuse(test, "reallife_travel_test_has_room", "a room is already booked at " + TravelState.HotelId);
                return;
            }
            HotelSite site = Hotels.Nearest(PlayerHelper.PlayerController.transform.position, tier, out float distance);
            if (site == null)
            {
                Refuse(test, "reallife_travel_test_no_hotel", "no hotel of tier " + tier + " was designated in this city");
                return;
            }
            RoomType[] rooms = TravelCatalogue.Rooms(site.Slot.Tier);
            RoomType room = HotelStay.FreeTonight(site.Slot, rooms[0]) > 0 ? rooms[0] : rooms[1];
            if (!Stay.Book(site, room, 1, false, out string failKey))
            {
                Refuse(test, failKey == "reallife_travel_desk_full" ? "reallife_travel_test_full" : "reallife_travel_test_no_money",
                    site.Slot.Id + ", " + room.Id + ", " + HotelStay.Tonight(site.Slot, room) + " a night, " + failKey);
                return;
            }
            Logger?.Info(test + " test: " + room.Id + " at " + site.Slot.Id + ", " + Mathf.RoundToInt(distance) + " m away.");
            Stay.EnterRoom(out _);
        }

        public void TestOpenRoom()
        {
            const string test = "Stay panel";
            if (!TestReady(test, Settings.Hotels))
            {
                return;
            }
            if (!TravelState.HasRoom)
            {
                Refuse(test, "reallife_travel_test_no_room", "no room is booked");
                return;
            }
            if (!Stay.EnterRoom(out TestRefusal why) && why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
            }
        }

        /// <summary>Gives a free economy ticket for a trip, alone, and sets off at once; a ticket already held is replaced.</summary>
        public void TestTrip(string tripId)
        {
            const string test = "Trip";
            if (!TestReady(test, Settings.Holidays))
            {
                return;
            }
            Trip trip = TravelCatalogue.TripById(tripId);
            if (trip == null || TravelState.Away || TravelState.Package)
            {
                Refuse(test, "reallife_travel_test_away", "a trip is already under way");
                return;
            }
            if (trip.Kind == TripKind.Real && TravelState.HasRoom && Hotels.ById(TravelCatalogue.HamptonsSlot) != null)
            {
                Refuse(test, "reallife_travel_test_has_room", "the Hamptons weekend needs the room slot, and a room is booked at " + TravelState.HotelId);
                return;
            }
            TravelState.ClearTicket();
            TravelState.TicketId = trip.Id;
            TravelState.TicketDay = TimeHelper.CurrentDay;
            if (!Holidays.Depart(out string why, true))
            {
                TravelState.ClearTicket();
                Logger?.Info(test + " test: not started (" + why + ").");
                TravelText.Toast(NotificationType.Warning, why ?? TravelText.T("reallife_travel_depart_busy"), 8f);
            }
        }

        public void TestReturn()
        {
            const string test = "Return from trip";
            if (!_loaded || _failed)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
            }
            else if (!Holidays.ReturnNow())
            {
                Refuse(test, "reallife_travel_test_not_away", "no card trip is under way (the Hamptons weekend ends at checkout)");
            }
        }

        /// <summary>Ends everything: out of the room or the trip, no room, no ticket, no marker. Stamps and past trips are kept.</summary>
        public void TestClear()
        {
            if (!_loaded)
            {
                TestRefusals.Tell(Logger, "Travel clear", TestRefusal.NotLoaded);
                return;
            }
            ToStreet();
            TravelState.ClearRoom();
            TravelState.ClearTicket();
            TravelState.ClearAway();
            TravelState.BurntOut = false;
            ModState.RemoveModifier(TravelEffects.Burnout);
            Unmark();
            Logger?.Info("Travel clear test: no room, no ticket, nobody away.");
            Notifications.Show(NotificationType.Info, "reallife_travel_test_cleared", null, 6f);
        }
    }
}
