using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>Hygiene, stress and clean clothes, for a room of quality 0 (hostel) to 3 (luxury).</summary>
    public interface ITravelHome
    {
        bool Freshen(int quality);

        bool Laundry(int quality);

        bool Rest(int hours, int quality);

        bool Holiday(int days);

        /// <summary>Days in a row of high stress; -1 if not tracked (travel then counts its own days without a break).</summary>
        int HighStressDays { get; }

        /// <summary>Whether the player has a bed of their own tonight; null if not known (travel then looks for a rented apartment).</summary>
        bool? HasBed { get; }
    }

    /// <summary>Somebody who can come along on a trip.</summary>
    public struct TravelCompanion
    {
        public string Id;      // the social feature's own id for them
        public string Name;    // already localised
    }

    /// <summary>Friends and partner.</summary>
    public interface ITravelSocial
    {
        /// <summary>Fills the list with who would come on a trip now, the partner first; empty for nobody.</summary>
        void Companions(List<TravelCompanion> into);

        void TripTogether(string companionId, int days);

        /// <summary>Souvenirs brought home to give away. Returns how many were taken as gifts; the rest are kept.</summary>
        int Souvenirs(int count);

        bool TableBooked(string hotelName);

        /// <summary>Locale keys of what was missed while away (dates, calls), or null.</summary>
        List<string> MissedWhileAway(int fromDay, int toDay);
    }

    /// <summary>Bail, probation, court dates.</summary>
    public interface ITravelJustice
    {
        /// <summary>Whether the player may leave town for this many days; if not, the locale key of the agent's reason.</summary>
        bool MayLeaveTown(int days, out string reasonKey);

        /// <summary>Settles anything due while the player was away. Returns locale keys to show, or null.</summary>
        List<string> MissedWhileAway(int fromDay, int toDay);
    }

    /// <summary>Getting about: coaches and cars on call.</summary>
    public interface ITravelTransport
    {
        /// <summary>A coach seat to the Hamptons and back, if the transport feature runs one; its price joins the package.</summary>
        bool CoachToHamptons(out int price);

        bool RideCoachToHamptons();

        bool CallCar(UnityEngine.Vector3 door);
    }

    /// <summary>Things to do on a real holiday.</summary>
    public interface ITravelLeisure
    {
        bool PlayGolf();

        bool DinnerOut(UnityEngine.Vector3 near);

        /// <summary>Hotel demand today: 1 on an ordinary day, 1.5 with a festival in town. Above 1 rooms are scarcer and dearer.</summary>
        float DemandToday { get; }
    }

    /// <summary>
    /// Travel's doors to other features, set by Wiring; null means the default. A door's bool method returns true when its
    /// feature handled the matter, and false lets travel apply its own effect.
    /// </summary>
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

        /// <summary>True while the social feature holds its door: travel then leaves gifts and companions to it.</summary>
        public static bool HasSocial => _social != null;

        public static bool HasTransport => _transport != null;

        public static bool HasLeisure => _leisure != null;

        /// <summary>True once City Utilities has chosen or is off; hotels choose after it, so every machine agrees.</summary>
        public static Func<bool> UtilitiesPicked;

        /// <summary>Called as the city unloads: whoever held a door is gone with it.</summary>
        public static void Reset()
        {
            _home = null;
            _social = null;
            _justice = null;
            _transport = null;
            _leisure = null;
            UtilitiesPicked = null;
        }

        // Nothing handled, so the caller applies its own effect; the one companion is the mod's Friend contact.
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
