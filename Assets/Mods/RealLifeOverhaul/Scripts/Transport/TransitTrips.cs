using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One stop a rider can be taken to from where they are, and what the ride is.</summary>
    public struct TransitTrip
    {
        public TransitStop To;        // the stop got off at, of the last bus's line
        public float Minutes;         // game minutes of the ride, from boarding the first bus, the change included where there is one
        public float Metres;          // of road: along the lines where planned, else estimated from the straight line
        public bool Direct;           // one bus all the way
        public TransitStop Board;     // the stop boarded at, whose line is the bus to take; null for an estimate without a plan
        public TransitStop ChangeOff; // a change: where they get off the first bus
        public TransitStop ChangeOn;  // and the second line's stop they board at (the same place, or a short walk)
        public float Wait;            // game minutes until the first bus calls

        /// <summary>True for a journey the planner found.</summary>
        public bool Planned => Board != null;

        public bool Changes => ChangeOff != null && ChangeOn != null;
    }

    /// <summary>
    /// Where the buses take a rider from a place: any stop of the network, by the planner (TransitPlan.From) where one bus or
    /// two reach it, else estimated from the straight line. Reads <see cref="TransitMap.Net"/> once per opening of the list.
    /// </summary>
    public static class TransitTrips
    {
        public const float WalkedTo = 150f;          // metres, as the crow flies: a stop this near is not a ride
        public const float RoadFactor = 1.35f;       // a way along the streets is about this much longer than the straight line
        public const float ChangeMinutes = 6f;       // game minutes for getting off one bus and onto the next, where the change is not planned
        public const float StopEvery = 330f;         // metres between stops on a line, on average (TransitMap: every 260 m, in the 140 m after)
        public const float MinutesPerMetre = 0.06f;  // game minutes; the game's taxi takes 0.04
        public const float MinutesPerStop = 1f;      // game minutes

        private static readonly IComparer<TransitTrip> Comparer = Comparer<TransitTrip>.Create(Compare);   // made once, so a sort allocates nothing
        private static TransitPlan.Journey[] _best = new TransitPlan.Journey[0];

        // The game's speed setting, which scales a journey's length as it does the game's taxi's.
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

        /// <summary>Game minutes of a ride of so many metres and stops along a line.</summary>
        public static float RideMinutes(float metres, int stops)
        {
            return Mathf.Max(0f, metres) * MinutesPerMetre + Mathf.Max(0, stops) * MinutesPerStop;
        }

        /// <summary>True when <paramref name="to"/> lies further along <paramref name="from"/>'s line.</summary>
        public static bool IsDirect(TransitStop from, TransitStop to)
        {
            return from != null && to != null && to.Line == from.Line && to.Order > from.Order;
        }

        /// <summary>The ride from one stop to another. For a list or a boarding, not a frame.</summary>
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
                trip.Board = from;
            }
            else
            {
                trip.Metres = Mathf.Sqrt(CitySpots.Flat(to.Sign - from.Sign)) * RoadFactor;
                int stops = Mathf.Max(1, Mathf.RoundToInt(trip.Metres / StopEvery));
                trip.Minutes = (trip.Metres * MinutesPerMetre + (stops - 1) * MinutesPerStop + 1f + ChangeMinutes) * speed;
            }
            return trip;
        }

        /// <summary>Game minutes left of a journey from a stop of its first bus, or from its change with <paramref name="from"/> null.</summary>
        public static float RestMinutes(TransitTrip trip, TransitStop from)
        {
            if (!trip.Changes)
            {
                TransitStop start = from ?? trip.Board;
                return start != null ? Between(start, trip.To).Minutes : trip.Minutes;
            }
            float first = from != null && IsDirect(from, trip.ChangeOff) ? Between(from, trip.ChangeOff).Minutes : 0f;
            return first + ChangeMinutes * Speed() + Between(trip.ChangeOn, trip.To).Minutes;
        }

        /// <summary>
        /// Every place offered from <paramref name="place"/>: one bus by line, then a change soonest first, then the rest nearest
        /// first. Returns how many are direct.
        /// </summary>
        public static int From(TransitStop place, List<TransitTrip> into, double now, Func<int, int, double, double> call)
        {
            into.Clear();
            TransitPlan.Net net = TransitMap.Net;
            if (place == null || net == null || place.At.Place < 0 || place.At.Place >= net.Places)
            {
                return 0;
            }
            TransitStop from = place.At;
            float speed = Speed();
            if (_best.Length < net.Places)
            {
                _best = new TransitPlan.Journey[net.Places];
            }
            TransitPlan.From(net, from.Place, now, call, _best);
            List<TransitStop> places = TransitMap.Places;
            int direct = 0;
            for (int pass = 0; pass < 3; pass++)
            {
                int began = into.Count;
                for (int i = 0; i < places.Count && i < net.Places; i++)
                {
                    TransitStop to = places[i];
                    if (to == from || CitySpots.Flat(to.Sign - from.Sign) < WalkedTo * WalkedTo)
                    {
                        continue;
                    }
                    TransitPlan.Journey best = _best[i];
                    if (pass == 0 && best.Found && best.Direct)
                    {
                        into.Add(Planned(best, now, speed));
                    }
                    else if (pass == 1 && best.Found && !best.Direct)
                    {
                        into.Add(Planned(best, now, speed));
                    }
                    else if (pass == 2 && !best.Found)
                    {
                        into.Add(Between(from, to, speed));
                    }
                }
                if (pass == 0)
                {
                    direct = into.Count;
                    if (direct > 1)
                    {
                        into.Sort(0, direct, ByLine);
                    }
                }
                else if (into.Count - began > 1)
                {
                    into.Sort(began, into.Count - began, pass == 1 ? BySoonest : Comparer);
                }
            }
            return direct;
        }

        private static TransitTrip Planned(TransitPlan.Journey journey, double now, float speed)
        {
            TransitLine first = TransitMap.Lines[journey.Line];
            var trip = new TransitTrip
            {
                Board = first.Stops[journey.From],
                Direct = journey.Direct,
                Wait = (float)Math.Max(0.0, journey.Board - now),
                Minutes = (float)Math.Max(1.0, journey.Arrive - journey.Board) * speed
            };
            if (journey.Direct)
            {
                trip.To = first.Stops[journey.Off];
                trip.Metres = trip.To.Metres - trip.Board.Metres;
            }
            else
            {
                TransitLine second = TransitMap.Lines[journey.Line2];
                trip.ChangeOff = first.Stops[journey.Off];
                trip.ChangeOn = second.Stops[journey.On2];
                trip.To = second.Stops[journey.Off2];
                trip.Metres = trip.ChangeOff.Metres - trip.Board.Metres + trip.To.Metres - trip.ChangeOn.Metres;
            }
            return trip;
        }

        private static readonly IComparer<TransitTrip> ByLine = Comparer<TransitTrip>.Create((a, b) =>
        {
            int line = a.Board.Line.CompareTo(b.Board.Line);
            return line != 0 ? line : a.To.Order.CompareTo(b.To.Order);
        });

        private static readonly IComparer<TransitTrip> BySoonest = Comparer<TransitTrip>.Create((a, b) =>
        {
            int soonest = (a.Wait + a.Minutes).CompareTo(b.Wait + b.Minutes);
            return soonest != 0 ? soonest : a.To.Id.CompareTo(b.To.Id);
        });

        private static int Compare(TransitTrip a, TransitTrip b)
        {
            int byLength = a.Metres.CompareTo(b.Metres);
            return byLength != 0 ? byLength : a.To.Id.CompareTo(b.To.Id);
        }
    }
}
