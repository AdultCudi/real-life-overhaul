using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    // The doors through which travel and lodging talk to features that live in other folders
    // (Scripts/Home, Scripts/Social, Scripts/Law, Scripts/Transport, Scripts/Leisure). Each is a
    // small interface with a default that works on its own, so this folder compiles and plays
    // with none of them present. A feature takes a door over with one line when it starts:
    //
    //     TravelBridges.Home = new MyHomeBridge();        // Scripts/Home: hygiene, stress, laundry
    //     TravelBridges.Social = new MySocialBridge();    // Scripts/Social: companions, gifts, dinners, missed dates
    //     TravelBridges.Justice = new MyJusticeBridge();  // Scripts/Law: bail and probation
    //     TravelBridges.Transport = new MyTransportBridge();
    //     TravelBridges.Leisure = new MyLeisureBridge();
    //
    // and hands it back with TravelBridges.Reset...() or by setting null (null means the default).
    // Nothing here is called per frame: only when the player presses a button or a day turns.

    /// <summary>Home routines: hygiene, stress and clean clothes.</summary>
    public interface ITravelHome
    {
        /// <summary>The player showered or freshened up in a room of this quality (0 hostel, 1 budget, 2 mid-range, 3 luxury). Return true if hygiene was handled.</summary>
        bool Freshen(int quality);

        /// <summary>The hotel laundered what the player wears. Return true if handled.</summary>
        bool Laundry(int quality);

        /// <summary>The player rested for this many hours somewhere comfortable (0 to 3). Return true if stress was handled.</summary>
        bool Rest(int hours, int quality);

        /// <summary>A whole holiday of this many days was taken: stress is cleared. Return true if handled.</summary>
        bool Holiday(int days);

        /// <summary>For how many days in a row stress has been high; -1 if stress is not tracked (travel then counts days without a break itself).</summary>
        int HighStressDays { get; }

        /// <summary>True if the player has somewhere of their own to sleep tonight; null if not known (travel then looks at rented apartments).</summary>
        bool? HasBed { get; }
    }

    /// <summary>Somebody who can come along on a trip.</summary>
    public struct TravelCompanion
    {
        public string Id;      // the social feature's own id for them
        public string Name;    // as shown to the player, already localised
    }

    /// <summary>Friends and partner.</summary>
    public interface ITravelSocial
    {
        /// <summary>Fills the list with the people who would come on a trip now (the partner first). Leave it empty for none.</summary>
        void Companions(List<TravelCompanion> into);

        /// <summary>The trip was taken together: a big step for the relationship.</summary>
        void TripTogether(string companionId, int days);

        /// <summary>The player came home with this many souvenirs to give away. Return how many were taken as gifts (the rest are kept for later).</summary>
        int Souvenirs(int count);

        /// <summary>The concierge booked a table for tonight. Return true if the social feature made a date of it.</summary>
        bool TableBooked(string hotelName);

        /// <summary>The player was away from one day to another: return the locale keys of the lines to show for what was missed (dates, calls), or null.</summary>
        List<string> MissedWhileAway(int fromDay, int toDay);
    }

    /// <summary>Bail, probation, court dates.</summary>
    public interface ITravelJustice
    {
        /// <summary>True if the player may leave town for this many days. With false, <paramref name="reasonKey"/> is the locale key of the sentence the travel agent says.</summary>
        bool MayLeaveTown(int days, out string reasonKey);

        /// <summary>The player was away: anything due in that time is the justice feature's to settle. Returns locale keys to show, or null.</summary>
        List<string> MissedWhileAway(int fromDay, int toDay);
    }

    /// <summary>Getting about: coaches and cars on call.</summary>
    public interface ITravelTransport
    {
        /// <summary>A coach seat to the Hamptons, there and back. Return true if the transport feature runs one (the price is then added to the package).</summary>
        bool CoachToHamptons(out int price);

        /// <summary>The coach leaves now with the player on it. Return true if the player was taken there.</summary>
        bool RideCoachToHamptons();

        /// <summary>The concierge calls a car to the hotel door. Return true if one is coming.</summary>
        bool CallCar(UnityEngine.Vector3 door);
    }

    /// <summary>Things to do on a real holiday.</summary>
    public interface ITravelLeisure
    {
        /// <summary>A round of golf in the Hamptons. Return true if the leisure feature took the player to it.</summary>
        bool PlayGolf();

        /// <summary>Dinner out near a place. Return true if the leisure feature staged it.</summary>
        bool DinnerOut(UnityEngine.Vector3 near);

        /// <summary>A multiplier for hotel demand today (1 = an ordinary day; 1.5 = a festival in town). Rooms are scarcer and dearer above 1.</summary>
        float DemandToday { get; }
    }

    public static class TravelBridges
    {
        private static readonly DefaultBridge Fallback = new DefaultBridge();
        private static ITravelHome _home;
        private static ITravelSocial _social;
        private static ITravelJustice _justice;
        private static ITravelTransport _transport;
        private static ITravelLeisure _leisure;

        public static ITravelHome Home { get => _home ?? Fallback; set => _home = value; }
        public static ITravelSocial Social { get => _social ?? Fallback; set => _social = value; }
        public static ITravelJustice Justice { get => _justice ?? Fallback; set => _justice = value; }
        public static ITravelTransport Transport { get => _transport ?? Fallback; set => _transport = value; }
        public static ITravelLeisure Leisure { get => _leisure ?? Fallback; set => _leisure = value; }

        /// <summary>True while the social feature has taken its door over (travel then leaves gifts and companions to it entirely).</summary>
        public static bool HasSocial => _social != null;

        public static bool HasHome => _home != null;

        public static bool HasTransport => _transport != null;

        public static bool HasLeisure => _leisure != null;

        /// <summary>
        /// True once City Utilities has chosen its building (or is off): the hotels choose theirs after it, so the two never
        /// race for a residential building and every machine of a Going Public session has the same. Null: nothing to wait for.
        /// </summary>
        public static Func<bool> UtilitiesPicked;

        /// <summary>Called when the city unloads: whoever took a door over is gone with it.</summary>
        public static void Reset()
        {
            _home = null;
            _social = null;
            _justice = null;
            _transport = null;
            _leisure = null;
            UtilitiesPicked = null;
        }

        /// <summary>
        /// What happens with no other feature present. Everything answers "not handled", and the
        /// caller (HotelStay, Holidays) then applies its own effect through the game's own
        /// hunger, energy and happiness; the one companion is the mod's Friend contact.
        /// </summary>
        private sealed class DefaultBridge : ITravelHome, ITravelSocial, ITravelJustice, ITravelTransport, ITravelLeisure
        {
            public bool Freshen(int quality) => false;
            public bool Laundry(int quality) => false;
            public bool Rest(int hours, int quality) => false;
            public bool Holiday(int days) => false;
            public int HighStressDays => -1;
            public bool? HasBed => null;

            public void Companions(List<TravelCompanion> into)
            {
                if (PhoneContacts.Friend != null)
                {
                    into.Add(new TravelCompanion { Id = "reallife-friend", Name = TravelText.T("reallife-friend") });
                }
            }

            public void TripTogether(string companionId, int days)
            {
                ModState.AddModifier(ModState.Social, 24);
            }

            public int Souvenirs(int count) => 0;
            public bool TableBooked(string hotelName) => false;
            List<string> ITravelSocial.MissedWhileAway(int fromDay, int toDay) => null;

            public bool MayLeaveTown(int days, out string reasonKey)
            {
                reasonKey = null;
                return true;
            }

            List<string> ITravelJustice.MissedWhileAway(int fromDay, int toDay) => null;

            public bool CoachToHamptons(out int price)
            {
                price = 0;
                return false;
            }

            public bool RideCoachToHamptons() => false;
            public bool CallCar(UnityEngine.Vector3 door) => false;
            public bool PlayGolf() => false;
            public bool DinnerOut(UnityEngine.Vector3 near) => false;
            public float DemandToday => 1f;
        }
    }
}
