using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A route's departures as the player set them on its page: the first run's hour, the runs a day and the hours between.</summary>
    public sealed class RouteTimes
    {
        public int First = TripRules.GameRunHour;
        public int Runs = 1;
        public int Gap = 12;
        public int Last = int.MinValue;   // the absolute game minute of the last departure begun; MinValue before any
        public bool Set;                  // the player chose these; otherwise the defaults are used and may change with the goods
    }

    /// <summary>What a trip carries to one stop: pieces of each product.</summary>
    public sealed class TripStop
    {
        public string Place;     // the stop's address key (CompanyGame's form: street~number)
        public int Arrive;       // the absolute game minute it arrives
        public bool Done;        // delivered
        public readonly List<string> Items = new List<string>(4);
        public readonly List<int> Pieces = new List<int>(4);

        public int Total()
        {
            int n = 0;
            for (int i = 0; i < Pieces.Count; i++)
            {
                n += Pieces[i];
            }
            return n;
        }
    }

    /// <summary>A trip waiting to leave or on the road: one vehicle and its driver, from a route's warehouse, round its stops and back.</summary>
    public sealed class Trip
    {
        public string Plan;
        public string From;           // the warehouse's address key
        public int Slot;              // the warehouse's vehicle slot
        public string Vehicle;        // the vehicle's id, and its driver's, as they were when it left
        public string Driver;
        public int Depart;            // absolute game minutes
        public int Back;              // 0 while it waits to leave
        public bool Out;              // it has left (its load and timeline are set)
        public int Metres;
        public int Boxes, Room;       // boxes loaded, the vehicle's room
        public int FuelCents, WageCents;
        public TripVehicle Kind;
        public readonly List<TripStop> Stops = new List<TripStop>(4);

        public int Pieces()
        {
            int n = 0;
            for (int i = 0; i < Stops.Count; i++)
            {
                n += Stops[i].Total();
            }
            return n;
        }
    }

    /// <summary>One run of the log: what it moved, how far and for how long, and its fuel and wages.</summary>
    public struct TripLog
    {
        public int Day, Minute;      // when it left: the game's day and the minute of that day
        public string Plan;
        public int Slot, Stops, Pieces, Boxes, Room, Metres, Minutes, FuelCents, WageCents;
    }

    /// <summary>
    /// The delivery trips' state in a save: each route's departures, the trips waiting or on the road, the log of the last seven days'
    /// runs and the day the rival shops' books were last charged. Game-free; written by <see cref="TripStore"/>.
    /// </summary>
    public sealed class TripState
    {
        public readonly Dictionary<string, RouteTimes> Routes = new Dictionary<string, RouteTimes>(StringComparer.Ordinal);
        public readonly List<Trip> Trips = new List<Trip>(8);
        public readonly List<TripLog> Log = new List<TripLog>(64);
        public int RivalDay = -1;

        public RouteTimes Route(string plan)
        {
            if (!Routes.TryGetValue(plan, out RouteTimes r))
            {
                r = new RouteTimes();
                Routes[plan] = r;
            }
            return r;
        }

        /// <summary>The trip of a warehouse's vehicle slot waiting or on the road (every route from it shares its vehicles); null when free.</summary>
        public Trip Busy(string from, int slot)
        {
            for (int i = 0; i < Trips.Count; i++)
            {
                if (Trips[i].From == from && Trips[i].Slot == slot)
                {
                    return Trips[i];
                }
            }
            return null;
        }
    }
}
