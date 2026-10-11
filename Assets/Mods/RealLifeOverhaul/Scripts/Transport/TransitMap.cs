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

        // The bay: the length of the strip between lane and kerb the bus pulls into, so that it stands beside the lane (BusBay).
        public float KerbAside;   // metres from the lane's line to the kerb's edge
        public bool HasBay;       // the strip is wide, straight, level and bare enough for one
        public Vector3 Bay;       // where the middle of the bus stands in it, on the road surface: beside the pole
        public Vector3 BayAlong;  // the way it runs (flat, unit): the lane's
        public float BayOffset;   // metres from the lane's line to the bay's (the middle of the bus)
        public float BayBehind;   // metres of the strip, back from Bay, that must be free for the bus to come in (the body on its own van)
        public float BayAhead;    // ...and on from it, to pull out
        public float BayRoom;     // metres back from Bay for which the strip will do: BayBehind at the least, more where a longer-nosed vehicle can come in too
        public string NoBay;      // why it has none, for the log

        /// <summary>The length of strip the bay takes up.</summary>
        public float BayLength => BayBehind + BayAhead;
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
    /// BAYS. A stop has a bay where the kerb is at least 4.10 m from the lane's line (the lane's
    /// half, 0.15 m clear of its edge, the 2.10 m body, 0.20 m to the kerb: BusBay.KerbNeeded)
    /// along the whole length a bus needs to come in and pull out (some 30 m, more where the
    /// strip is wider), the lane runs straight and plain for that length with no other lane
    /// between it and the kerb, the strip is level road with nothing fixed standing in it, and
    /// the kerb keeps its line. Of the places that will do for a stop, one with a bay is taken
    /// before one without: first in the 140 m, then up to 60 m further on, then up to 50 m
    /// before the place wanted (two stops are then never nearer than 210 m along the line),
    /// and only then the first place without one. Parked cars are not looked at here either.
    ///
    /// The work is done in slices from the director's timer, a search at a time and then stops
    /// one after another for no more than three milliseconds a slice, in the first seconds out
    /// of doors: a few dozen searches of the road graph at most and some thousands of rays and
    /// samples in all (most of them looking for bays), never again.
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
        private const float BayOn = 60f;            // metres beyond a stop's window in which a place with a bay is still taken
        private const float BayBack = 50f;          // ...and before the place wanted: two stops are never nearer than StopSpacing less this
        private const float BayStraight = 0.35f;    // metres the lane's points may lie off its line along a bay
        private const float BayLevel = 0.30f;       // metres the strip may lie above or below the lane along a bay
        private const float BayRoomMost = 23.5f;    // metres back from the pole the strip is looked at for a vehicle that needs more of it than the bus body on its van
        private const float BayAheadSlack = 1f;     // metres more of the strip ahead than the body on its van needs to pull out: a vehicle's own middle is seldom quite its origin
        private const int MaxExpansions = 400000;
        private const int MaxSearches = 48;         // in all: a city where nothing connects is given up on
        private const double StopsSlice = 0.003;    // seconds of looking for stops in one slice

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
        private static bool _lineBegun;         // the line at the cursor has had some of its stops looked for
        private static float _stopWanted;       // metres along it at which its next stop is wanted
        private static int _stopFrom;           // ...and the point of its path the look goes on from
        private static int _widest;
        private static bool _pairing;
        private static bool _limitHit;
        private static int _staticMask;
        private static int _doorMask;
        private static int _parkingMask;

        /// <summary>How many of the stops have a bay.</summary>
        public static int Bays { get; private set; }

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
            _lineBegun = false;
            _widest = 0;
            _pairing = false;
            _limitHit = false;
            Bays = 0;
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
                    // Stop after stop, line after line, until the slice's time is up: at least one each slice.
                    long until = System.Diagnostics.Stopwatch.GetTimestamp() + (long)(StopsSlice * System.Diagnostics.Stopwatch.Frequency);
                    do
                    {
                        if (PlaceStop(Lines[_lineCursor]))
                        {
                            _lineCursor++;
                        }
                    }
                    while (_lineCursor < Lines.Count && System.Diagnostics.Stopwatch.GetTimestamp() < until);
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
            Bays = 0;
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
                    Bays += stop.HasBay ? 1 : 0;
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
            var text = new StringBuilder("Transit check (network): " + Lines.Count + " bus lines, " + Stops.Count + " stops, " + Bays + " of them with a bay (the bus stands out of the lane there; a bay wants the kerb "
                                         + BusBay.KerbNeeded(TransitBus.BodyHalfWidth).ToString("F2") + " m or more from the lane's line), " + bothWays + " of the lines with their other direction, "
                                         + _searches + " searches of " + (_points != null ? _points.Length : 0) + " lane points (" + _laneChanges + " lane changes offered): "
                                         + (bothWays == Lines.Count ? "PASS" : "FAIL (a line runs one way only)") + ".");
            for (int i = 0; i < Lines.Count; i++)
            {
                TransitLine line = Lines[i];
                int sheltered = 0;
                int bays = 0;
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    sheltered += line.Stops[s].Shelter ? 1 : 0;
                    bays += line.Stops[s].HasBay ? 1 : 0;
                }
                text.Append(" ").Append(line.Number).Append(" to ").Append(line.To).Append(' ')
                    .Append((line.Metres[line.Metres.Length - 1] / 1000f).ToString("F1")).Append(" km ")
                    .Append(line.Stops.Count).Append(" stops (").Append(sheltered).Append(" with a shelter, ").Append(bays).Append(" with a bay; the kerb at");
                // Each stop's kerb, and in a word why a stop has no bay: what a look at the street is then held against.
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    TransitStop stop = line.Stops[s];
                    text.Append(' ').Append(stop.KerbAside.ToString("F2"));
                    if (!stop.HasBay)
                    {
                        text.Append(" [").Append(stop.NoBay ?? "no bay").Append(']');
                    }
                }
                text.Append(" m);");
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
            return RoadGraph.Drivable(point);
        }

        // The road graph as the search wants it (Core/RoadGraph, which the GPS builds its own from as well).
        private static void BuildGraph()
        {
            _graph = RoadGraph.Build(_points, LaneChangeCost, out _laneChanges);
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

        // The next stop of a line (or the finding that the next stretch of it has none). True once the line has all its stops.
        private static bool PlaceStop(TransitLine line)
        {
            if (!_lineBegun)
            {
                _lineBegun = true;
                _stopWanted = FirstStopAt;
                _stopFrom = 0;
            }
            float total = line.Metres[line.Metres.Length - 1];
            float wanted = _stopWanted;
            int i = _stopFrom;
            if (i >= line.Path.Length || wanted >= total - 40f)
            {
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    line.Stops[s].Name = NameOf(line.Stops[s].Wait);
                }
                _lineBegun = false;
                return true;
            }
            while (i < line.Path.Length && line.Metres[i] < wanted)
            {
                i++;
            }
            // The first place with a bay; failing that, the first place that will do at all (what was always taken).
            TransitStop plain = null;
            TransitStop bay = null;
            int j = i;
            for (; bay == null && j < line.Path.Length && line.Metres[j] <= wanted + StopWindow; j++)
            {
                TransitStop stop = Try(line, j, plain != null);
                if (stop != null && stop.HasBay)
                {
                    bay = stop;
                }
                else if (stop != null && plain == null)
                {
                    plain = stop;
                }
            }
            int windowEnd = j;
            // None in the window: a little further on, then a little before the place wanted, nearest first.
            for (; bay == null && j < line.Path.Length && line.Metres[j] <= wanted + StopWindow + BayOn; j++)
            {
                bay = Try(line, j, true);
            }
            for (int k = i - 1; bay == null && k >= 0 && line.Metres[k] >= wanted - BayBack; k--)
            {
                bay = Try(line, k, true);
            }
            TransitStop placed = bay ?? plain;
            if (placed != null)
            {
                line.Stops.Add(placed);
                _stopWanted = placed.Metres + StopSpacing;
                _stopFrom = placed.PathIndex;
            }
            else
            {
                _stopWanted = wanted + StopWindow;
                _stopFrom = windowEnd;
            }
            return false;
        }

        // A stop at one lane point of a line, or null if that point will not do (or has no bay, when only one with a bay is wanted).
        private static TransitStop Try(TransitLine line, int index, bool bayOnly)
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
            if (LaneToRight(point, right))
            {
                return null;
            }
            var stop = new TransitStop { PathIndex = index, Lane = lane, Along = along, Kerb = kerb, Metres = line.Metres[index] };
            if (bayOnly)
            {
                LayBay(stop, line, right);
                if (!stop.HasBay)
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
            stop.Sign = sign;
            stop.Wait = wait;
            stop.Parking = _parkingMask != 0 && Physics.CheckSphere(lane + right * (edge * 0.5f + 0.6f) + Vector3.up * 0.5f, 0.6f, _parkingMask, QueryTriggerInteraction.Collide);
            if (!bayOnly)
            {
                LayBay(stop, line, right);
            }
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

        // Another lane of the road to the right of a lane point (the road graph names the lanes that run beside one).
        private static bool LaneToRight(Waypoint point, Vector3 right)
        {
            for (int i = 0; point.otherLanes != null && i < point.otherLanes.Count; i++)
            {
                Waypoint beside = Point(point.otherLanes[i]);
                if (beside != null && Vector3.Dot(beside.position - point.position, right) > 1f)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- a stop's bay

        // Whether the strip beside a stop will do for a bay, and where the bus then stands. Says why not in a word or two.
        // Arithmetic first, then some forty rays and samples and two boxes: only for a place that is otherwise a stop.
        private static void LayBay(TransitStop stop, TransitLine line, Vector3 right)
        {
            Vector3 lane = stop.Lane;
            Vector3 along = stop.Along;
            stop.BayAlong = along;
            stop.KerbAside = KerbAside(stop, right);
            float half = TransitBus.BodyHalfWidth;
            float halfLength = TransitBus.BodyLength * 0.5f;
            float offset = BusBay.Offset(stop.KerbAside, half);
            if (offset <= 0f)
            {
                stop.NoBay = "narrow";
                return;
            }
            BusBay.Curve curveIn = BusBay.In(offset, halfLength);
            BusBay.Curve curveOut = BusBay.Out(offset, halfLength);
            if (curveIn.Cut || curveOut.Cut || curveIn.Length + 1f > JunctionClear || curveOut.Length + 1f > JunctionClear)
            {
                stop.NoBay = "too wide to cross in the plain lane there is";
                return;
            }
            float behind = BusBay.Behind(curveIn, 0f);
            float ahead = BusBay.Ahead(curveOut, 0f) + BayAheadSlack;
            stop.NoBay = LaneBends(stop, line, right, curveIn.Length + 1f, curveOut.Length + 1f) ?? StripBad(stop, right, offset, -behind, ahead);
            if (stop.NoBay != null)
            {
                return;
            }
            Vector3 bay = lane + right * offset;
            CitySpots.Ground(bay, out float ground);
            bay.y = ground;
            stop.HasBay = true;
            stop.Bay = bay;
            stop.BayOffset = offset;
            stop.BayBehind = behind;
            stop.BayAhead = ahead;
            // Further back than the body on its van needs: for a vehicle with a longer nose (it comes in over a longer curve).
            stop.BayRoom = behind < BayRoomMost && LaneBends(stop, line, right, BayRoomMost + 2.5f, 0f) == null && StripBad(stop, right, offset, -BayRoomMost, -behind) == null
                ? BayRoomMost : behind;
        }

        // The lane along a bay: straight for so far back and on from the stop, and nothing of the road between it and the
        // kerb. Null if so, else why not. Arithmetic on the line's own points.
        private static string LaneBends(TransitStop stop, TransitLine line, Vector3 right, float back, float on)
        {
            int[] path = line.Path;
            for (int step = -1; step <= 1; step += 2)
            {
                float run = step < 0 ? back : on;
                for (int k = stop.PathIndex + step; k >= 0 && k < path.Length && Mathf.Abs(line.Metres[k] - stop.Metres) <= run; k += step)
                {
                    Waypoint point = _points[path[k]];
                    Vector3 off = point.position - stop.Lane;
                    if (!Plain(point))
                    {
                        return "a junction";
                    }
                    if (Mathf.Abs(Vector3.Dot(off, right)) > BayStraight || Mathf.Abs(off.y) > 1f)
                    {
                        return "the lane bends";
                    }
                    if (LaneToRight(point, right))
                    {
                        return "a lane beside";
                    }
                }
            }
            return null;
        }

        // The strip from one place along the stop's lane to another (metres from the pole): road at the lane's level right
        // up to where the bus's door side will be, the kerb keeping its line, and nothing fixed standing in it. Null if so,
        // else why not. Two rays and a sample every four metres or so, and one box.
        private static string StripBad(TransitStop stop, Vector3 right, float offset, float from, float to)
        {
            Vector3 lane = stop.Lane;
            Vector3 along = stop.Along;
            float inner = stop.KerbAside - BusBay.KerbGap * 0.5f;
            int steps = Mathf.Max(2, Mathf.CeilToInt((to - from) / 4f));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 at = lane + along * (from + (to - from) * i / steps);
                if (!CitySpots.Ground(at + right * inner, out float edgeGround) || !CitySpots.Ground(at + right * offset, out float middleGround)
                    || Mathf.Abs(edgeGround - lane.y) > BayLevel || Mathf.Abs(middleGround - lane.y) > BayLevel)
                {
                    return "not level road";
                }
                // (Pavement within a step of the bus's side: the kerb comes in, or a crossing runs over the strip.)
                if (CitySpots.Pavement(at + right * (stop.KerbAside - 0.45f), 0.3f, out _))
                {
                    return "the kerb comes in";
                }
            }
            float near = BusBay.LaneHalf + 0.1f;
            float width = stop.KerbAside - 0.05f - near;
            Vector3 centre = lane + along * ((from + to) * 0.5f) + right * (near + width * 0.5f);
            return Physics.CheckBox(centre + Vector3.up * 1.15f, new Vector3(width * 0.5f, 0.8f, (to - from) * 0.5f), Quaternion.LookRotation(along), _staticMask, QueryTriggerInteraction.Ignore)
                ? "something stands in the strip" : null;
        }

        // How far the kerb is from the lane's line: the pavement's edge as the navigation mesh has it, or, where the
        // road surface itself steps up before that, the step. (The mesh is a few centimetres coarse; a bus that stands
        // 0.2 m from the kerb is placed by the kerb that is there.)
        private static float KerbAside(TransitStop stop, Vector3 right)
        {
            Vector3 lane = stop.Lane;
            float meshEdge = Vector3.Dot(Flat(stop.Kerb - lane), right);
            if (meshEdge < BusBay.KerbNeeded(TransitBus.BodyHalfWidth))
            {
                return meshEdge;   // too near for a bay as it is: the step, if there is one, is nearer still
            }
            float from = meshEdge - 0.5f;
            if (!CitySpots.Ground(lane + right * from, out float road) || Mathf.Abs(road - lane.y) > BayLevel)
            {
                return meshEdge;
            }
            for (float d = from + 0.05f; d < meshEdge; d += 0.05f)
            {
                if (CitySpots.Ground(lane + right * d, out float height) && height - road > 0.05f)
                {
                    return d - 0.025f;
                }
            }
            return meshEdge;
        }

        private static bool Plain(Waypoint point)
        {
            return RoadGraph.Plain(point);
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
