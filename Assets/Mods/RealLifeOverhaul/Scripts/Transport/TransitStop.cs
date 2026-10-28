using System.Collections.Generic;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One line's stop at the kerb by a plain stretch of lane; lines on one lane share the place, its <see cref="Host"/>'s.</summary>
    public sealed class TransitStop
    {
        public int Id;            // its index in TransitMap.Stops
        public int Line;          // the directed line it is on
        public int Order;         // its place along that line, 0 first
        public int PathIndex;     // the index in the line's path of the lane point the bus stops at
        public Vector3 Lane;      // that lane point
        public Vector3 Along;     // the way the lane runs there (flat, unit)
        public Vector3 Kerb;      // the edge of the pavement beside it
        public Vector3 Sign;      // where the pole stands
        public Vector3 Wait;      // where people wait, and where a rider gets off
        public Vector3 ShelterAt; // the middle of the shelter's floor
        public bool Shelter;      // there is room for one
        public bool Parking;      // a parking lane lies between the lane and the kerb
        public float Metres;      // along the line from its start
        public string Name;       // the address it is called by

        // The bay: the strip between lane and kerb the bus pulls into, to stand beside the lane (BusBay).
        public float KerbAside;   // metres from the lane's line to the kerb's edge
        public bool HasBay;       // the strip is wide, straight, level and bare enough for one
        public Vector3 Bay;       // where the middle of the bus stands in it, on the road surface: beside the pole
        public Vector3 BayAlong;  // the way it runs (flat, unit): the lane's
        public float BayOffset;   // metres from the lane's line to the bay's (the middle of the bus)
        public float BayBehind;   // metres of the strip back from Bay that must be free for the bus to come in
        public float BayAhead;    // and on from it, to pull out
        public float BayRoom;     // metres back from Bay for which the strip will do, BayBehind at the least
        public string NoBay;      // why it has none, for the log
        public BusZone Zone;      // what its zone needs done to the street (BusZones): the host's only

        // The game's street furniture on the kerb by the doors, switched off while the buses run (BusZones).
        public readonly List<GameObject> Boarding = new List<GameObject>(4);
        public bool BoardingMade; // switched off now

        public float Stand;       // metres on from the pole the bus's middle stands, so neither door lines up with what is on the kerb
        public int KerbThings;    // what stands on the kerb by the doors and stays (a tree, a lamp, the pole), for the log
        public float ZoneLook;    // when BusZones next looks at it while something there waits to be out of view
        // The place, shared by the lines that stop here.
        public int LanePoint = -1;   // the lane point's index in the road graph
        public TransitStop Host;     // the stop that laid the place out (the first line to stop here); null or itself for that one
        public int Place = -1;       // the place's index in TransitMap.Places
        public readonly List<TransitStop> Calls = new List<TransitStop>(3);   // a host's: every line's stop here, its own first
        public readonly List<TransitStop> Near = new List<TransitStop>(3);    // a host's: other places a short walk away, for a change on foot

        public float BayLength => BayBehind + BayAhead;

        /// <summary>True for the stop that laid the place out, whose pole, shelter, bay and zone are the place's.</summary>
        public bool IsHost => Host == null || Host == this;

        public TransitStop At => Host ?? this;   // the place: its host stop
    }

    /// <summary>One direction of a bus route: a path along the road graph and the stops on it.</summary>
    public sealed class TransitLine
    {
        public string Number;     // "M1"
        public string ToKey;      // the game's own name key of the far end
        public int[] Path;        // lane points, in order
        public float[] Metres;    // along the path to each of them
        public readonly List<TransitStop> Stops = new List<TransitStop>(12);
        public bool Main;         // one of the main lines: runs more often, and through the night
        public int Seed;          // its place in its timetable (TransitPlan.Offset)
        public float[] Minutes;   // game minutes of a ride from its first stop to each of its stops (TransitTrips)
        public float[] Wanted;    // a district's own route: metres along its path each of its stops is wanted at (-1 where it does not pass); null for a paired line
        public TransitPlan.Route Route;   // ... and the route it was laid from, for the log's word on a stop not laid; null for a paired line

        public string To => ToKey.GetLocalization();

        /// <summary>The line's offset in its timetable at one of its stops.</summary>
        public double OffsetAt(int order)
        {
            return TransitPlan.Offset(Seed, Minutes != null && order >= 0 && order < Minutes.Length ? Minutes[order] : 0f);
        }
    }
}
