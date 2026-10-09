using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using GleyTrafficSystem;
using GleyUrbanAssets;
using Localizor;
using Streets;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One bus stop: a fixed place at the kerb beside a plain stretch of lane.</summary>
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
    }

    /// <summary>One direction of a bus route: a path along the road graph and the stops on it.</summary>
    public sealed class TransitLine
    {
        public string Number;     // "M1"
        public string FromKey;    // the game's own name keys of the two ends
        public string ToKey;
        public int[] Path;        // lane points, in order
        public float[] Metres;    // along the path to each of them
        public readonly List<TransitStop> Stops = new List<TransitStop>(12);

        public string To => ToKey.GetLocalization();
    }

    /// <summary>
    /// The bus network, laid over the city by rule once per city load and kept in memory only
    /// (nothing of it is saved, and the same city always gives the same network).
    ///
    /// THE RULE. The ends of the routes are the game's own subway stations, taken in the order
    /// of the game's station list (SubwayStationName). Up to six times over: the first station
    /// not yet used is one end, and the other is the station furthest from it (not yet used,
    /// 500 m to 2.8 km away) that can be driven to AND back from. A station's end of a line is
    /// any plain lane point within 150 m of it, not one chosen point (<see cref="RouteSearch"/>
    /// says why), and the way is the shortest along the road graph following lanes, with a
    /// change of lane allowed where the graph offers one at the price of 30 m of road. So each
    /// route has its two directions, usually on the two sides of the same streets. Only when a
    /// station has no partner it can be driven both ways with does it get a line one way, and
    /// the log says so. Along each path a stop is looked for every 260 m, in the 140 m after
    /// that, at the first lane point that is: on a road proper with no junction within 26 m
    /// either way, straight, at street level, with pavement within 5.6 m to its right that is
    /// at least 1.5 m deep and has no door, wall or street furniture where the pole and the
    /// waiting people stand. Vehicles are not looked at here (they come and go): they are
    /// tested when a stop is dressed and when a bus pulls up.
    ///
    /// The work is done in slices from the director's timer, a search or a line's stops at a
    /// time, in the first seconds out of doors: a few dozen searches of the road graph at most
    /// and a few hundred physics queries in all, never again.
    /// </summary>
    public static class TransitMap
    {
        public const int MaxRoutes = 6;
        private const float MaxSpan = 2800f;        // metres between the two ends of a route, as the crow flies
        private const float MinSpan = 500f;
        private const float TerminalReach = 150f;   // a line may begin and end on any plain lane point this near its station
        private const float LaneChangeCost = 30f;   // metres a change of lane counts for
        private const float FirstStopAt = 90f;      // metres from the start of a path
        private const float StopSpacing = 260f;
        private const float StopWindow = 140f;
        private const float JunctionClear = 26f;    // metres of plain lane either side of a stop
        private const float KerbMost = 5.6f;        // lane middle to pavement edge
        private const float KerbLeast = 1.3f;
        private const int MaxExpansions = 400000;
        private const int MaxSearches = 48;         // in all: a city where nothing connects is given up on

        public static IModLogger Logger;

        public static readonly List<TransitLine> Lines = new List<TransitLine>(MaxRoutes * 2);
        public static readonly List<TransitStop> Stops = new List<TransitStop>(160);

        /// <summary>True once the network has been laid out (it may be empty: see the log).</summary>
        public static bool Ready { get; private set; }
        public static bool Failed { get; private set; }

        private static Waypoint[] _points;
        private static readonly List<SubwayStation> Stations = new List<SubwayStation>(24);
        private static readonly List<List<int>> Terminals = new List<List<int>>(24);
        private static readonly List<int> Partners = new List<int>(24);
        private static readonly List<int> PathScratch = new List<int>(2048);
        private static readonly StringBuilder Notes = new StringBuilder(256);
        private static CityBuildingController[] _buildings;
        private static RouteSearch _graph;
        private static bool[] _used;
        private static TransitLine _there;      // the way out to the partner being tried, waiting for the way back
        private static TransitLine _oneWay;     // the first way out that had no way back: used only if nothing better turns up
        private static int _oneWayTo;
        private static int _phase;              // 0 not begun, 1 pairing, 2 stops
        private static int _a;
        private static int _partner;
        private static int _routes;
        private static int _searches;
        private static int _laneChanges;
        private static int _lineCursor;
        private static int _widest;
        private static bool _pairing;
        private static bool _limitHit;
        private static int _staticMask;
        private static int _doorMask;
        private static int _parkingMask;

        public static void Reset()
        {
            Lines.Clear();
            Stops.Clear();
            Stations.Clear();
            Terminals.Clear();
            Partners.Clear();
            PathScratch.Clear();
            Notes.Length = 0;
            _points = null;
            _buildings = null;
            _graph = null;
            _used = null;
            _there = null;
            _oneWay = null;
            _phase = 0;
            _a = 0;
            _partner = 0;
            _routes = 0;
            _searches = 0;
            _laneChanges = 0;
            _lineCursor = 0;
            _widest = 0;
            _pairing = false;
            _limitHit = false;
            Ready = false;
            Failed = false;
        }

        public static Waypoint Point(int index)
        {
            return _points != null && index >= 0 && index < _points.Length ? _points[index] : null;
        }

        /// <summary>
        /// Does the next slice of the layout. Call from a timer while the traffic system is running
        /// until <see cref="Ready"/>; a fault ends it with an empty network and one line in the log.
        /// </summary>
        public static void Step()
        {
            if (Ready)
            {
                return;
            }
            try
            {
                if (_phase == 0)
                {
                    if (!Begin())
                    {
                        Finish();
                        return;
                    }
                    _phase = 1;
                    return;
                }
                if (_phase == 1)
                {
                    if (!PairStep())
                    {
                        _phase = 2;
                        _graph = null;   // the search's memory is the size of the road graph: let go of now
                        _used = null;
                    }
                    return;
                }
                if (_lineCursor < Lines.Count)
                {
                    PlaceStops(Lines[_lineCursor], _lineCursor);
                    _lineCursor++;
                    return;
                }
                Finish();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Lines.Clear();
                Stops.Clear();
                Failed = true;
                Finish();
            }
        }

        private static void Finish()
        {
            // Lines that got fewer than two stops carry nobody anywhere.
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (Lines[i].Stops.Count < 2)
                {
                    Lines.RemoveAt(i);
                }
            }
            Stops.Clear();
            for (int i = 0; i < Lines.Count; i++)
            {
                TransitLine line = Lines[i];
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    TransitStop stop = line.Stops[s];
                    stop.Line = i;
                    stop.Order = s;
                    stop.Id = Stops.Count;
                    Stops.Add(stop);
                }
            }
            Ready = true;
            _graph = null;
            _used = null;
            _buildings = null;
            _there = null;
            _oneWay = null;
            Partners.Clear();
            PathScratch.Clear();
            PathScratch.TrimExcess();
            for (int i = 0; i < Terminals.Count; i++)
            {
                Terminals[i].Clear();
            }
            Terminals.Clear();
            if (Lines.Count == 0)
            {
                Logger?.Warn("Transit: no bus line could be laid out in this city (" + Stations.Count + " subway stations, "
                             + (_points != null ? _points.Length : 0) + " lane points, " + _searches + " searches); there are no buses.");
                return;
            }
            int bothWays = 0;
            for (int i = 0; i < Lines.Count; i++)
            {
                for (int k = 0; k < Lines.Count; k++)
                {
                    if (k != i && Lines[k].Number == Lines[i].Number)
                    {
                        bothWays++;
                        break;
                    }
                }
            }
            var text = new StringBuilder("Transit check (network): " + Lines.Count + " bus lines, " + Stops.Count + " stops, " + bothWays + " of the lines with their other direction, "
                                         + _searches + " searches of " + (_points != null ? _points.Length : 0) + " lane points (" + _laneChanges + " lane changes offered): "
                                         + (bothWays == Lines.Count ? "PASS" : "FAIL (a line runs one way only)") + ".");
            for (int i = 0; i < Lines.Count; i++)
            {
                TransitLine line = Lines[i];
                int sheltered = 0;
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    sheltered += line.Stops[s].Shelter ? 1 : 0;
                }
                text.Append(" ").Append(line.Number).Append(" to ").Append(line.To).Append(' ')
                    .Append((line.Metres[line.Metres.Length - 1] / 1000f).ToString("F1")).Append(" km ")
                    .Append(line.Stops.Count).Append(" stops (").Append(sheltered).Append(" with a shelter);");
            }
            Logger?.Info(text.ToString());
        }

        // ---------------------------------------------------------------- the ends of the routes

        private static bool Begin()
        {
            CurrentSceneData data = CurrentSceneData.GetSceneInstance();
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (data == null || data.allWaypoints == null || data.allWaypoints.Length < 100 || city == null || city.subwayStations == null)
            {
                return false;
            }
            _points = data.allWaypoints;
            _buildings = city.cityBuildingControllers;
            _staticMask = LayerMask.GetMask("StreetProps", "Buildings", "BuildingsOutlined", "BuildingWalls");
            _doorMask = LayerMask.GetMask("InteractiveItems", "InteractiveItemsOutlined");
            _parkingMask = LayerMask.GetMask("ParkingArea");

            Stations.Clear();
            for (int i = 0; i < city.subwayStations.Count; i++)
            {
                if (city.subwayStations[i] != null)
                {
                    Stations.Add(city.subwayStations[i]);
                }
            }
            Stations.Sort((a, b) => ((int)a.stationName).CompareTo((int)b.stationName));

            // Where a station's buses may start from and end at: every plain lane point of a road proper near it.
            Terminals.Clear();
            int withEnds = 0;
            for (int s = 0; s < Stations.Count; s++)
            {
                Vector3 at = Stations[s].transform.position;
                var ends = new List<int>(64);
                for (int i = 0; i < _points.Length; i++)
                {
                    Waypoint point = _points[i];
                    if (point == null || point.neighbors == null || point.neighbors.Count == 0 || point.prev == null || point.prev.Count == 0)
                    {
                        continue;
                    }
                    Vector3 offset = point.position - at;
                    if (offset.y > 5f || offset.y < -5f || offset.x * offset.x + offset.z * offset.z > TerminalReach * TerminalReach)
                    {
                        continue;
                    }
                    if (Drivable(point) && TrafficManager.IsNotIntersection(point))
                    {
                        ends.Add(i);
                    }
                }
                Terminals.Add(ends);
                withEnds += ends.Count > 0 ? 1 : 0;
            }
            if (withEnds < 2)
            {
                return false;
            }
            BuildGraph();
            _used = new bool[Stations.Count];
            _a = 0;
            _pairing = false;
            return true;
        }

        private static bool Drivable(Waypoint point)
        {
            return point.allowedCars != null && point.allowedCars.Contains(VehicleTypes.Car);
        }

        // The road graph as the search wants it: a lane's own next points, and the changes of lane the graph offers
        // between two lanes that run the same way side by side.
        private static void BuildGraph()
        {
            int count = _points.Length;
            _graph = new RouteSearch(count);
            var start = new int[count + 1];
            int edges = 0;
            int[] to = null;
            float[] extra = null;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1)
                {
                    to = new int[edges];
                    extra = new float[edges];
                }
                edges = 0;
                for (int i = 0; i < count; i++)
                {
                    Waypoint point = _points[i];
                    if (pass == 1)
                    {
                        start[i] = edges;
                    }
                    if (point == null)
                    {
                        continue;
                    }
                    if (pass == 0)
                    {
                        _graph.X[i] = point.position.x;
                        _graph.Y[i] = point.position.y;
                        _graph.Z[i] = point.position.z;
                    }
                    for (int k = 0; point.neighbors != null && k < point.neighbors.Count; k++)
                    {
                        int next = point.neighbors[k];
                        if (next < 0 || next >= count || _points[next] == null || !Drivable(_points[next]))
                        {
                            continue;
                        }
                        if (pass == 1)
                        {
                            to[edges] = next;
                            extra[edges] = 0f;
                        }
                        edges++;
                    }
                    for (int k = 0; point.otherLanes != null && k < point.otherLanes.Count; k++)
                    {
                        int beside = point.otherLanes[k];
                        if (!LaneChange(point, beside))
                        {
                            continue;
                        }
                        if (pass == 1)
                        {
                            to[edges] = beside;
                            extra[edges] = LaneChangeCost;
                            _laneChanges++;
                        }
                        edges++;
                    }
                }
            }
            start[count] = edges;
            _graph.SetEdges(start, to, extra);
        }

        // A change of lane a bus may make: between two plain lane points a few metres apart whose lanes run the same way.
        private static bool LaneChange(Waypoint point, int beside)
        {
            if (beside < 0 || beside >= _points.Length)
            {
                return false;
            }
            Waypoint other = _points[beside];
            if (other == null || !Drivable(other) || !Drivable(point) || !Plain(point) || !Plain(other)
                || point.neighbors == null || point.neighbors.Count == 0 || other.neighbors == null || other.neighbors.Count == 0)
            {
                return false;
            }
            Waypoint ahead = Point(point.neighbors[0]);
            Waypoint otherAhead = Point(other.neighbors[0]);
            if (ahead == null || otherAhead == null)
            {
                return false;
            }
            Vector3 across = other.position - point.position;
            if (Mathf.Abs(across.y) > 2f)
            {
                return false;
            }
            across.y = 0f;
            Vector3 lane = Flat(ahead.position - point.position);
            Vector3 otherLane = Flat(otherAhead.position - other.position);
            if (across.sqrMagnitude < 1f || across.sqrMagnitude > 14f * 14f || lane.sqrMagnitude < 0.01f || otherLane.sqrMagnitude < 0.01f)
            {
                return false;
            }
            lane.Normalize();
            return Vector3.Dot(lane, otherLane.normalized) > 0.7f && Vector3.Dot(lane, across) > -1f;
        }

        // ---------------------------------------------------------------- the pairs, and the way between them

        // One search of the road graph, or the bookkeeping between two. False when there is nothing left to pair.
        private static bool PairStep()
        {
            if (_routes >= MaxRoutes || _searches >= MaxSearches)
            {
                EndStation();
                return false;
            }
            if (!_pairing)
            {
                while (_a < Stations.Count && (_used[_a] || Terminals[_a].Count == 0))
                {
                    _a++;
                }
                if (_a >= Stations.Count)
                {
                    return false;
                }
                // The stations it could be paired with, furthest first.
                Partners.Clear();
                Vector3 from = Stations[_a].transform.position;
                for (int b = 0; b < Stations.Count; b++)
                {
                    if (b == _a || _used[b] || Terminals[b].Count == 0)
                    {
                        continue;
                    }
                    float sqr = CitySpots.Flat(Stations[b].transform.position - from);
                    if (sqr > MinSpan * MinSpan && sqr <= MaxSpan * MaxSpan)
                    {
                        Partners.Add(b);
                    }
                }
                Partners.Sort((x, y) => CitySpots.Flat(Stations[y].transform.position - from).CompareTo(CitySpots.Flat(Stations[x].transform.position - from)));
                _partner = 0;
                _there = null;
                _oneWay = null;
                _widest = 0;
                _limitHit = false;
                Notes.Length = 0;
                _pairing = true;
                return true;
            }
            if (_partner >= Partners.Count)
            {
                EndStation();
                _a++;
                return true;
            }
            int to = Partners[_partner];
            _searches++;
            if (_there == null)
            {
                if (Find(_a, to))
                {
                    _there = MakeLine(_a, to);
                    return true;
                }
                Notes.Append(" no way to ").Append(Stations[to].stationName).Append(';');
                // What that search never got near cannot be driven to from here either: not searched for.
                if (!_graph.HitLimit)
                {
                    for (int k = Partners.Count - 1; k > _partner; k--)
                    {
                        List<int> ends = Terminals[Partners[k]];
                        bool near = false;
                        for (int e = 0; e < ends.Count && !near; e++)
                        {
                            near = _graph.WasReached(ends[e]);
                        }
                        if (!near)
                        {
                            Notes.Append(" no way to ").Append(Stations[Partners[k]].stationName).Append(';');
                            Partners.RemoveAt(k);
                        }
                    }
                }
                _partner++;
                return true;
            }
            if (Find(to, _a))
            {
                TransitLine back = MakeLine(to, _a);
                string number = "M" + (_routes + 1);
                _there.Number = number;
                back.Number = number;
                Lines.Add(_there);
                Lines.Add(back);
                _used[_a] = true;
                _used[to] = true;
                _routes++;
                _there = null;
                _oneWay = null;
                EndStation();
                _a++;
                return true;
            }
            Notes.Append(" a way to ").Append(Stations[to].stationName).Append(" but none back;");
            if (_oneWay == null)
            {
                _oneWay = _there;
                _oneWayTo = to;
            }
            _there = null;
            _partner++;
            return true;
        }

        // The station being paired is done with: a line one way if that is all it could have, and what was found out, in one line.
        private static void EndStation()
        {
            if (!_pairing)
            {
                return;
            }
            _pairing = false;
            if (_oneWay != null && _routes < MaxRoutes)
            {
                _oneWay.Number = "M" + (_routes + 1);
                Lines.Add(_oneWay);
                _used[_a] = true;
                _used[_oneWayTo] = true;
                _routes++;
                Notes.Append(" so ").Append(_oneWay.Number).Append(" runs one way only, to ").Append(Stations[_oneWayTo].stationName).Append(';');
            }
            _oneWay = null;
            _there = null;
            if (Notes.Length > 0)
            {
                Logger?.Info("Transit: pairing " + Stations[_a].stationName + " (" + Terminals[_a].Count + " lane points to start from):" + Notes
                             + " the widest search that failed got to " + _widest + " of " + _points.Length + " lane points"
                             + (_limitHit ? " and stopped at its limit of " + MaxExpansions + " steps." : " and ran out of road: no lane leads from the one end to the other."));
            }
            Notes.Length = 0;
            Partners.Clear();
        }

        private static bool Find(int from, int to)
        {
            Vector3 goal = Stations[to].transform.position;
            bool found = _graph.Find(Terminals[from], Terminals[to], goal.x, goal.y, goal.z, TerminalReach + 6f, MaxExpansions, PathScratch) && PathScratch.Count > 8;
            if (!found)
            {
                _widest = Mathf.Max(_widest, _graph.Reached);
                _limitHit |= _graph.HitLimit;
            }
            return found;
        }

        private static TransitLine MakeLine(int from, int to)
        {
            var line = new TransitLine
            {
                FromKey = Stations[from].GetNameKey(),
                ToKey = Stations[to].GetNameKey(),
                Path = PathScratch.ToArray(),
                Metres = new float[PathScratch.Count]
            };
            float metres = 0f;
            for (int i = 1; i < line.Path.Length; i++)
            {
                metres += Vector3.Distance(_points[line.Path[i - 1]].position, _points[line.Path[i]].position);
                line.Metres[i] = metres;
            }
            return line;
        }

        // ---------------------------------------------------------------- the stops of a line

        private static void PlaceStops(TransitLine line, int lineIndex)
        {
            float total = line.Metres[line.Metres.Length - 1];
            float wanted = FirstStopAt;
            int i = 0;
            while (i < line.Path.Length && wanted < total - 40f)
            {
                while (i < line.Path.Length && line.Metres[i] < wanted)
                {
                    i++;
                }
                bool placed = false;
                int j = i;
                for (; j < line.Path.Length && line.Metres[j] <= wanted + StopWindow; j++)
                {
                    TransitStop stop = Try(line, j);
                    if (stop != null)
                    {
                        line.Stops.Add(stop);
                        wanted = line.Metres[j] + StopSpacing;
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    wanted += StopWindow;
                }
                i = j;
            }
            for (int s = 0; s < line.Stops.Count; s++)
            {
                line.Stops[s].Name = NameOf(line.Stops[s].Wait);
            }
        }

        // A stop at one lane point of a line, or null if that point will not do.
        private static TransitStop Try(TransitLine line, int index)
        {
            int[] path = line.Path;
            if (index < 3 || index >= path.Length - 3)
            {
                return null;
            }
            Waypoint point = _points[path[index]];
            if (!Plain(point))
            {
                return null;
            }
            // Plain lane for a bus length and more either side.
            for (int k = index - 1; k >= 0 && line.Metres[index] - line.Metres[k] < JunctionClear; k--)
            {
                if (!Plain(_points[path[k]]))
                {
                    return null;
                }
            }
            for (int k = index + 1; k < path.Length && line.Metres[k] - line.Metres[index] < JunctionClear; k++)
            {
                if (!Plain(_points[path[k]]))
                {
                    return null;
                }
            }
            Vector3 before = Flat(point.position - _points[path[index - 3]].position);
            Vector3 after = Flat(_points[path[index + 3]].position - point.position);
            if (before.sqrMagnitude < 1f || after.sqrMagnitude < 1f || Vector3.Angle(before, after) > 9f)
            {
                return null;
            }
            Vector3 along = (before + after).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, along);
            Vector3 lane = point.position;
            if (!CitySpots.Ground(lane, out float ground) || Mathf.Abs(ground - lane.y) > 1f)
            {
                return null;   // a bridge, a ramp, a tunnel
            }
            lane.y = ground;

            // The edge of the pavement to the right: the first point that is pavement, with road before it.
            float edge = -1f;
            Vector3 kerb = default;
            for (float d = KerbLeast; d <= KerbMost; d += 0.3f)
            {
                if (IsPavement(lane + right * d, out Vector3 found))
                {
                    edge = d;
                    kerb = found;
                    break;
                }
            }
            if (edge < 0f || edge <= KerbLeast + 0.01f)
            {
                return null;   // none, or the lane point itself is on something walkable: not a kerb we understand
            }
            // Another lane of the road between this one and the kerb: its traffic would run between the bus and the people.
            // (The road graph names the lanes that run beside a lane point; one of them to the right is that lane.)
            for (int i = 0; point.otherLanes != null && i < point.otherLanes.Count; i++)
            {
                Waypoint beside = Point(point.otherLanes[i]);
                if (beside != null && Vector3.Dot(beside.position - point.position, right) > 1f)
                {
                    return null;
                }
            }
            Vector3 sign = lane + right * (edge + 0.5f);
            Vector3 wait = lane + right * (edge + 1.25f) - along * 1.6f;
            if (!IsPavement(sign, out sign) || !IsPavement(wait, out wait) || !IsPavement(lane + right * (edge + 1.5f) + along * 1.5f, out _)
                || !Open(sign, 0.3f) || !Open(wait, 0.45f) || !Open(wait - along * 1.1f, 0.4f))
            {
                return null;
            }
            var stop = new TransitStop
            {
                PathIndex = index, Lane = lane, Along = along, Kerb = kerb, Sign = sign, Wait = wait, Metres = line.Metres[index],
                Parking = _parkingMask != 0 && Physics.CheckSphere(lane + right * (edge * 0.5f + 0.6f) + Vector3.up * 0.5f, 0.6f, _parkingMask, QueryTriggerInteraction.Collide)
            };
            // A shelter stands back from the kerb, open to the road, ahead of the pole: only where the pavement is deep and bare enough.
            Vector3 shelter = lane + right * (edge + 2.35f) + along * 3.2f;
            if (IsPavement(shelter, out Vector3 floor) && IsPavement(shelter + right * 0.85f + along * 1.7f, out _) && IsPavement(shelter + right * 0.85f - along * 1.7f, out _)
                && IsPavement(shelter - right * 1.6f + along * 1.7f, out _) && IsPavement(shelter - right * 1.6f - along * 1.7f, out _)
                && !Physics.CheckBox(floor + Vector3.up * 1.3f, new Vector3(1.05f, 1.1f, 1.95f), Quaternion.LookRotation(along), _staticMask | _doorMask, QueryTriggerInteraction.Collide)
                && !Physics.CheckSphere(floor + Vector3.up, 3.2f, _doorMask, QueryTriggerInteraction.Collide))
            {
                stop.Shelter = true;
                stop.ShelterAt = floor;
            }
            return stop;
        }

        private static bool Plain(Waypoint point)
        {
            return point != null && !point.enter && !point.exit && !point.giveWay && !point.IsInIntersection() && TrafficManager.IsNotIntersection(point);
        }

        private static bool IsPavement(Vector3 place, out Vector3 spot)
        {
            return CitySpots.Pavement(place, 0.3f, out spot) && CitySpots.Flat(spot - place) < 0.3f * 0.3f;
        }

        // Nothing that never moves stands there, and nothing the player can use is close by.
        private static bool Open(Vector3 spot, float radius)
        {
            return !Physics.CheckCapsule(spot + Vector3.up * (0.35f + radius), spot + Vector3.up * 2.4f, radius, _staticMask, QueryTriggerInteraction.Ignore)
                   && !Physics.CheckSphere(spot + Vector3.up, 2.4f, _doorMask, QueryTriggerInteraction.Collide);
        }

        // What a stop is called: the address of the nearest building.
        private static string NameOf(Vector3 place)
        {
            CityBuildingController nearest = null;
            float best = 70f * 70f;
            for (int i = 0; _buildings != null && i < _buildings.Length; i++)
            {
                CityBuildingController building = _buildings[i];
                if (building == null || building.building == null)
                {
                    continue;
                }
                Vector3 offset = building.transform.position - place;
                float sqr = offset.x * offset.x + offset.z * offset.z;
                if (sqr < best)
                {
                    best = sqr;
                    nearest = building;
                }
            }
            try
            {
                if (nearest != null && nearest.building.Address != null && !nearest.building.Address.IsUndefined())
                {
                    return nearest.building.Address.ToFormattedString();
                }
            }
            catch (Exception)
            {
                // an address that cannot be written out: the stop goes by its number
            }
            return null;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        // ---------------------------------------------------------------- questions

        /// <summary>The stop nearest a place, within a distance; null if there is none. A pass over the stops: for a timer, not a frame.</summary>
        public static TransitStop Nearest(Vector3 place, float within, TransitStop not = null)
        {
            TransitStop nearest = null;
            float best = within * within;
            for (int i = 0; i < Stops.Count; i++)
            {
                TransitStop stop = Stops[i];
                Vector3 offset = stop.Sign - place;
                float sqr = offset.x * offset.x + offset.z * offset.z;
                if (sqr < best && stop != not && Mathf.Abs(offset.y) < 8f)
                {
                    best = sqr;
                    nearest = stop;
                }
            }
            return nearest;
        }

        /// <summary>How many stops lie after this one on its line.</summary>
        public static int Onward(TransitStop stop)
        {
            return stop == null ? 0 : Lines[stop.Line].Stops.Count - 1 - stop.Order;
        }
    }
}
