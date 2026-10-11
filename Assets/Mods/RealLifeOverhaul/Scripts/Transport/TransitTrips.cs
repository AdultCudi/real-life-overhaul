using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One stop a rider can be taken to from where they are, and what the ride is.</summary>
    public struct TransitTrip
    {
        public TransitStop To;
        public float Minutes;     // game minutes, the change of bus included where there is one
        public float Metres;      // of road: along the line when direct, else estimated from the straight line
        public bool Direct;       // further along the same line: the same bus all the way
    }

    /// <summary>
    /// Where the buses take a rider from a stop: any stop of the network, as the game's subway takes a rider from any
    /// station to any other. A stop further along the same line is reached on the one bus, by the line's own metres.
    /// Any other stop is reached with a change of bus, which is not played out (the subway's changes are not either):
    /// its time is worked out from the straight line between the two stops, lengthened by what streets add to it,
    /// plus the stops on the way and the wait for the second bus. Nothing here searches the road graph: it reads
    /// <see cref="TransitMap.Lines"/> and <see cref="TransitMap.Stops"/> once per opening of the list.
    ///
    /// Not offered: the stop itself, and a stop of another line within 150 m, which is the other side of the street
    /// or the next corner and is walked to.
    /// </summary>
    public static class TransitTrips
    {
        public const float WalkedTo = 150f;          // metres, as the crow flies: a stop of another line this near is not a ride
        public const float RoadFactor = 1.35f;       // a way along streets is about this much longer than the straight line
        public const float ChangeMinutes = 6f;       // game minutes for getting off one bus and onto the next
        public const float StopEvery = 330f;         // metres between stops on a line, on average (TransitMap: every 260 m, in the 140 m after)
        public const float MinutesPerMetre = 0.06f;  // the game's taxi takes 0.04
        public const float MinutesPerStop = 1f;

        private static readonly IComparer<TransitTrip> Comparer = Comparer<TransitTrip>.Create(Compare);   // made once: sorting makes nothing

        /// <summary>The game's speed setting scales a journey's length, as it does the game's taxi's.</summary>
        private static float Speed()
        {
            try
            {
                return Mathf.Clamp(PlayerPrefSettings.GameSpeed, 0.25f, 4f);
            }
            catch (Exception)
            {
                return 1f;
            }
        }

        /// <summary>True when <paramref name="to"/> lies further along the line <paramref name="from"/> is on.</summary>
        public static bool IsDirect(TransitStop from, TransitStop to)
        {
            return from != null && to != null && to.Line == from.Line && to.Order > from.Order;
        }

        /// <summary>The ride from one stop to another: its time in game minutes and its length. For a list or a boarding, not a frame.</summary>
        public static TransitTrip Between(TransitStop from, TransitStop to)
        {
            return Between(from, to, Speed());
        }

        private static TransitTrip Between(TransitStop from, TransitStop to, float speed)
        {
            var trip = new TransitTrip { To = to, Direct = IsDirect(from, to) };
            if (trip.Direct)
            {
                trip.Metres = to.Metres - from.Metres;
                trip.Minutes = (trip.Metres * MinutesPerMetre + (to.Order - from.Order - 1) * MinutesPerStop + 1f) * speed;
            }
            else
            {
                trip.Metres = Mathf.Sqrt(CitySpots.Flat(to.Sign - from.Sign)) * RoadFactor;
                int stops = Mathf.Max(1, Mathf.RoundToInt(trip.Metres / StopEvery));
                trip.Minutes = (trip.Metres * MinutesPerMetre + (stops - 1) * MinutesPerStop + 1f + ChangeMinutes) * speed;
            }
            return trip;
        }

        /// <summary>
        /// Every stop offered from <paramref name="from"/>: first the stops further along its own line, in the order
        /// the bus calls at them, then the others, nearest first. Returns how many are direct.
        /// </summary>
        public static int From(TransitStop from, List<TransitTrip> into)
        {
            into.Clear();
            if (from == null || from.Line < 0 || from.Line >= TransitMap.Lines.Count)
            {
                return 0;
            }
            float speed = Speed();
            List<TransitStop> own = TransitMap.Lines[from.Line].Stops;
            for (int i = from.Order + 1; i < own.Count; i++)
            {
                into.Add(Between(from, own[i], speed));
            }
            int direct = into.Count;
            List<TransitStop> all = TransitMap.Stops;
            for (int i = 0; i < all.Count; i++)
            {
                TransitStop stop = all[i];
                if (stop == from || IsDirect(from, stop) || CitySpots.Flat(stop.Sign - from.Sign) < WalkedTo * WalkedTo)
                {
                    continue;
                }
                into.Add(Between(from, stop, speed));
            }
            if (into.Count - direct > 1)
            {
                into.Sort(direct, into.Count - direct, Comparer);
            }
            return direct;
        }

        private static int Compare(TransitTrip a, TransitTrip b)
        {
            int byLength = a.Metres.CompareTo(b.Metres);
            return byLength != 0 ? byLength : a.To.Id.CompareTo(b.To.Id);
        }
    }
}
