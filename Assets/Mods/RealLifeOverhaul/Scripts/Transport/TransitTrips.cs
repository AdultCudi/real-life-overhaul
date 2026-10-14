using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One stop a rider can be taken to from where they are, and what the ride is.</summary>
    public struct TransitTrip
    {
        public TransitStop To;        // the stop got off at: of the last bus's line
        public float Minutes;         // game minutes of the ride, from boarding the first bus, the change included where there is one
        public float Metres;          // of road: along the lines where planned, else estimated from the straight line
        public bool Direct;           // one bus all the way
        public TransitStop Board;     // the stop boarded at, at the place the rider is at: its line is the bus to take (null: the old estimate, a change not planned)
        public TransitStop ChangeOff; // a change: where they get off the first bus...
        public TransitStop ChangeOn;  // ...and the stop of the second line they board at (the same place, or a short walk)
        public float Wait;            // game minutes until the first bus calls

        /// <summary>True for a journey the planner found (one bus, or two with the change at a transfer point).</summary>
        public bool Planned => Board != null;

        /// <summary>True for a journey with its change planned.</summary>
        public bool Changes => ChangeOff != null && ChangeOn != null;
    }

    /// <summary>
    /// Where the buses take a rider from a place: any stop of the network, as the game's subway takes a rider from any
    /// station to any other. The journey planner (TransitPlan.From) finds, for each place, the soonest arrival on the
    /// timetables: on one bus of any line that calls where the rider is, or on two, with the change at a place both lines
    /// stop at or a short walk between two places (a transfer point). A place no journey of one change reaches is offered
    /// as before, its time worked out from the straight line between the two stops, lengthened by what streets add to it,
    /// plus the stops on the way and the wait for a second bus, and its ride passes under the fade. Nothing here searches
    /// the road graph: it reads <see cref="TransitMap.Net"/> once per opening of the list.
    ///
    /// Not offered: the place itself, and a place within 150 m, which is the other side of the street or the next corner
    /// and is walked to.
    /// </summary>
    public static class TransitTrips
    {
        public const float WalkedTo = 150f;          // metres, as the crow flies: a stop this near is not a ride
        public const float RoadFactor = 1.35f;       // a way along streets is about this much longer than the straight line
        public const float ChangeMinutes = 6f;       // game minutes for getting off one bus and onto the next, where the change is not planned
        public const float StopEvery = 330f;         // metres between stops on a line, on average (TransitMap: every 260 m, in the 140 m after)
        public const float MinutesPerMetre = 0.06f;  // the game's taxi takes 0.04
        public const float MinutesPerStop = 1f;

        private static readonly IComparer<TransitTrip> Comparer = Comparer<TransitTrip>.Create(Compare);   // made once: sorting makes nothing
        private static TransitPlan.Journey[] _best = new TransitPlan.Journey[0];

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

        /// <summary>Game minutes of a ride along a line: so many metres, with so many stops called at on the way (each a minute).</summary>
        public static float RideMinutes(float metres, int stops)
        {
            return Mathf.Max(0f, metres) * MinutesPerMetre + Mathf.Max(0, stops) * MinutesPerStop;
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

        /// <summary>
        /// The ride of a journey from a stop of its first bus (or, with <paramref name="from"/> null, from its change) to its
        /// end: what is left of the first bus, the change and the second bus. For a skip of the rest of a journey.
        /// </summary>
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
        /// Every place offered from <paramref name="place"/>: first those one bus reaches, line by line in the order the bus calls
        /// at them, then those with a change, soonest first, then the rest, nearest first. <paramref name="call"/> gives when a
        /// line next calls at a stop of its (TransitDirector: the timetable, as the panel shows it). Returns how many are direct.
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

        // A journey the planner found, as the list offers it.
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
