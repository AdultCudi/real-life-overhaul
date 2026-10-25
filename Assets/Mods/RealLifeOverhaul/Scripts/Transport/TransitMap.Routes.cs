using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The bus network's part for the routes of a district's own (TransitPlan.Route): laid once the stations are paired,
    /// each direction a search of the road graph from its first point through every point of its way, behind the loading
    /// screen with the rest of the routes (one direction a step where it is laid in play); then its stops at the places
    /// wanted, each at the nearest plain stretch of lane that will take a stop, or shared with a line stopping there
    /// already. They run as local lines, so the main lines stay the paired routes they were.
    /// </summary>
    public static partial class TransitMap
    {
        public const int MostOwnRoutes = 4;             // routes of a district's own, beyond the paired ones
        private const float WantedNear = 12f;           // metres either side of a wanted place a stop is looked for first
        private const float WantedFar = 24f;            // and then, with the shorter clear stretch, this far
        private const float WantedClear = 18f;          // metres of plain lane either side of such a stop: an avenue's 57.6 m between its crossings holds one
        private const int ViaSteps = 60000;             // steps of the search for one stretch between two points of a way

        private struct OwnRoute
        {
            public TransitPlan.Route There;
            public TransitPlan.Route Back;
            public string Number;
        }

        private static readonly List<OwnRoute> OwnRoutes = new List<OwnRoute>(MostOwnRoutes);
        private static readonly List<int> ViaStarts = new List<int>(64);
        private static readonly List<int> ViaGoals = new List<int>(64);
        private static readonly List<int> LegScratch = new List<int>(1024);
        private static readonly int[] TryOrder = new int[32];
        private static int _ownCursor;                  // the next direction of a district's route to lay (two a route)
        private static int _ownWanted, _ownLaid, _ownMissed, _ownRefused;
        private static int _wantedAt;                   // the wanted stop of the line being laid out
        private static string _ownNotes;

        /// <summary>
        /// A route of a district's own both ways, laid at this load after the paired ones (one a name; kept until the city
        /// goes): its first point should stand a stretch before its first stop, its last a stretch after its last stop.
        /// </summary>
        public static void AddRoute(TransitPlan.Route there, TransitPlan.Route back)
        {
            if (there == null || back == null || OwnRoutes.Count >= MostOwnRoutes)
            {
                return;
            }
            for (int i = 0; i < OwnRoutes.Count; i++)
            {
                if (OwnRoutes[i].There.Name == there.Name)
                {
                    return;
                }
            }
            OwnRoutes.Add(new OwnRoute { There = there, Back = back });
        }

        /// <summary>True when every direction of the district's routes has been laid or refused.</summary>
        private static bool OwnLaid => _ownCursor >= OwnRoutes.Count * 2;

        /// <summary>How the district's own routes went at this load, for the census: wanted stops, laid, missed.</summary>
        public static void OwnCounts(out int routes, out int wanted, out int laid, out int missed)
        {
            routes = OwnRoutes.Count;
            wanted = _ownWanted;
            laid = _ownLaid;
            missed = _ownMissed;
        }

        /// <summary>True for a line of a district's own route.</summary>
        public static bool IsOwn(TransitLine line)
        {
            return line != null && line.Wanted != null;
        }

        private static void ResetOwn()
        {
            _ownCursor = 0;
            _ownWanted = 0;
            _ownLaid = 0;
            _ownMissed = 0;
            _ownRefused = 0;
            _wantedAt = 0;
            _ownNotes = null;
            for (int i = 0; i < OwnRoutes.Count; i++)
            {
                OwnRoute own = OwnRoutes[i];
                own.Number = null;
                OwnRoutes[i] = own;
            }
        }

        // One direction of the next district's route: its way searched stretch by stretch, then its line with its stops wanted.
        private static void LayOwn()
        {
            int index = _ownCursor / 2;
            bool back = _ownCursor % 2 == 1;
            _ownCursor++;
            OwnRoute own = OwnRoutes[index];
            TransitPlan.Route route = back ? own.Back : own.There;
            string fault = route.Fault() ?? Way(route);
            if (fault != null)
            {
                _ownRefused++;
                _ownNotes = (_ownNotes ?? string.Empty) + " " + route.Name + " (" + fault + ");";
                return;
            }
            if (own.Number == null)
            {
                own.Number = "M" + (_routes + 1);   // numbered on after the paired routes, so theirs stay as they were
                OwnRoutes[index] = own;
                _routes++;
            }
            TransitLine line = MakeLine(0, 0);
            line.Number = own.Number;
            int to = NearestEnd(route.ViaX[route.Points - 1], route.ViaZ[route.Points - 1]);
            line.ToKey = to >= 0 ? Ends[to].NameKey : line.ToKey;
            XScratch.Clear();
            ZScratch.Clear();
            for (int i = 0; i < line.Path.Length; i++)
            {
                Vector3 at = _points[line.Path[i]].position;
                XScratch.Add(at.x);
                ZScratch.Add(at.z);
            }
            line.Wanted = new float[route.Stops];
            int found = TransitPlan.WantedAlong(XScratch, ZScratch, line.Metres, line.Path.Length, route.StopX, route.StopZ, line.Wanted);
            _ownWanted += route.Stops;
            _ownMissed += route.Stops - found;
            Lines.Add(line);
        }

        // The way of a route through its points into PathScratch, or why there is none. Each stretch is the shortest from
        // where the last ended to the lane points round the next point, so the way keeps to the lanes that run its way.
        private static string Way(TransitPlan.Route route)
        {
            PathScratch.Clear();
            LanesNear(route.ViaX[0], route.ViaZ[0], ViaStarts);
            if (ViaStarts.Count == 0)
            {
                return "no lane near its first point";
            }
            for (int v = 1; v < route.Points; v++)
            {
                LanesNear(route.ViaX[v], route.ViaZ[v], ViaGoals);
                if (ViaGoals.Count == 0)
                {
                    return "no lane near its point " + (v + 1);
                }
                Vector3 goal = _points[ViaGoals[0]].position;
                if (!_graph.Find(ViaStarts, ViaGoals, route.ViaX[v], goal.y, route.ViaZ[v], TransitPlan.ViaReach + 6f, ViaSteps, LegScratch))
                {
                    return "no road from its point " + v + " to its point " + (v + 1);
                }
                for (int i = PathScratch.Count > 0 ? 1 : 0; i < LegScratch.Count; i++)
                {
                    PathScratch.Add(LegScratch[i]);
                }
                ViaStarts.Clear();
                ViaStarts.Add(LegScratch[LegScratch.Count - 1]);
            }
            return PathScratch.Count > 8 ? null : "it has no length";
        }

        // The drivable lane points of a road proper round a point of a way, of a kerb's lane alone (no lane to its right), so
        // the way keeps to the lane a stop is laid from. A pass over the lane points, behind the loading screen.
        private static void LanesNear(float x, float z, List<int> into)
        {
            into.Clear();
            float reach = TransitPlan.ViaReach * TransitPlan.ViaReach;
            for (int i = 0; i < _points.Length; i++)
            {
                Waypoint point = _points[i];
                if (point == null || point.neighbors == null || point.neighbors.Count == 0)
                {
                    continue;
                }
                float dx = point.position.x - x, dz = point.position.z - z;
                if (dx * dx + dz * dz > reach || !RoadGraph.Drivable(point) || !Plain(point))
                {
                    continue;
                }
                Waypoint next = Point(point.neighbors[0]);
                Vector3 ahead = next != null ? Flat(next.position - point.position) : Vector3.zero;
                if (ahead.sqrMagnitude > 0.01f && !LaneToRight(point, Vector3.Cross(Vector3.up, ahead.normalized)))
                {
                    into.Add(i);
                }
            }
        }

        // The station or terminus a route ends by, the one nearest its last point: what its line says it goes to.
        private static int NearestEnd(float x, float z)
        {
            int best = -1;
            float nearest = float.MaxValue;
            for (int i = 0; i < Ends.Count; i++)
            {
                float dx = Ends[i].At.x - x, dz = Ends[i].At.z - z, d = dx * dx + dz * dz;
                if (d < nearest)
                {
                    nearest = d;
                    best = i;
                }
            }
            return best;
        }

        // The next wanted stop of a district's line: shared with a place laid there already, else the nearest lane point
        // round it that takes a stop, else one a little further with the shorter clear stretch. True once all are done.
        private static bool PlaceWanted(TransitLine line)
        {
            if (!_lineBegun)
            {
                _lineBegun = true;
                _wantedAt = 0;
                _stopFrom = 0;
            }
            if (_wantedAt >= line.Wanted.Length)
            {
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    line.Stops[s].Name = line.Stops[s].IsHost ? NameOf(line.Stops[s].Wait) : line.Stops[s].At.Name;
                }
                _lineBegun = false;
                return true;
            }
            float wanted = line.Wanted[_wantedAt++];
            if (wanted < 0f)
            {
                return false;   // the way does not pass it: counted as missed when the way was laid
            }
            int hostId = TransitPlan.HostAlong(line.Path, line.Metres, wanted - WantedNear, wanted + WantedNear, wanted, 3, _hostAt ?? (_hostAt = HostAt), out int hostIndex);
            if (hostId >= 0 && hostIndex > _stopFrom)
            {
                TransitStop shared = Share(line, hostIndex, Hosts[hostId], WantedClear);
                if (shared != null)
                {
                    line.Stops.Add(shared);
                    _shared++;
                    _ownLaid++;
                    CallsAt(Hosts[hostId]);
                    _stopFrom = shared.PathIndex;
                    return false;
                }
            }
            TransitStop placed = TryRound(line, wanted, WantedNear, JunctionClear) ?? TryRound(line, wanted, WantedFar, WantedClear);
            if (placed == null)
            {
                _ownMissed++;
                return false;
            }
            placed.LanePoint = line.Path[placed.PathIndex];
            placed.Host = placed;
            if (!HostIndex.ContainsKey(placed.LanePoint))
            {
                HostIndex[placed.LanePoint] = Hosts.Count;
                Hosts.Add(placed);
            }
            line.Stops.Add(placed);
            _ownLaid++;
            _stopFrom = placed.PathIndex;
            return false;
        }

        private static TransitStop TryRound(TransitLine line, float wanted, float within, float clear)
        {
            int count = TransitPlan.Nearest(line.Metres, line.Path.Length, wanted, within, _stopFrom, TryOrder);
            for (int k = 0; k < count; k++)
            {
                TransitStop stop = Try(line, TryOrder[k], false, clear);
                if (stop != null)
                {
                    return stop;
                }
            }
            return null;
        }

        // The district's routes in the log, after the network's line.
        private static string OwnWords()
        {
            if (OwnRoutes.Count == 0)
            {
                return null;
            }
            return "Transit check (district routes): " + OwnRoutes.Count + " route(s) of a district's own both ways, " + _ownWanted + " stops wanted, " + _ownLaid + " laid, "
                   + _ownMissed + " missed" + (_ownRefused > 0 ? ", " + _ownRefused + " direction(s) not laid:" + _ownNotes : string.Empty) + ": "
                   + (_ownMissed == 0 && _ownRefused == 0 ? "PASS" : "FAIL") + ".";
        }
    }
}
