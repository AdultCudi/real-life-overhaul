using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using GleyTrafficSystem;
using GleyUrbanAssets;
using Streets;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The bus network, laid over the city by rule once a city load and kept in memory only: the same city always gives the
    /// same network. Its routes join the game's subway stations along the road graph, both ways, the streets one route takes
    /// counting less to the next so the lines gather on the avenues and share their stops; a stop is laid every 260 m, one
    /// with a bay first (TransitMap.Bays). The routes are laid behind the loading screen (<see cref="Prewarm"/>, 150 to 250
    /// ms of searches); the stops need the street, so they are placed on the director's timer out of doors, a slice of about
    /// a millisecond a frame, as are the routes where they could not be laid behind the screen.
    /// </summary>
    public static partial class TransitMap
    {
        public const int MaxRoutes = 16;
        public const float TransferWalk = 160f;     // metres between two places a change of bus is made between on foot
        private const float TerminalReach = 150f;   // metres from its station a line may begin and end on any plain lane point
        private const float LaneChangeCost = 30f;   // metres a change of lane counts for
        private const float StopSpacing = 260f;     // metres between stops wanted
        private const int MaxExpansions = 400000;   // steps of one search
        private const int MaxSearches = 100;        // searches in all: a city where nothing connects is given up on
        private const double StopsSliceMs = 1.0;    // milliseconds of looking for stops a slice; it ends after the first stop past it
        private const double SeekSliceMs = 1.0;     // milliseconds of a search of the road graph a slice, where it is made in play
        private const int SeekSteps = 64;           // steps of a search between two looks at the clock

        public static IModLogger Logger;

        public static readonly List<TransitLine> Lines = new List<TransitLine>(MaxRoutes * 2);
        public static readonly List<TransitStop> Stops = new List<TransitStop>(320);

        /// <summary>The places the buses stop at: one host stop each, every line's stop there in its <see cref="TransitStop.Calls"/>.</summary>
        public static readonly List<TransitStop> Places = new List<TransitStop>(200);

        /// <summary>The network as the journey planner reads it.</summary>
        public static TransitPlan.Net Net { get; private set; }

        // The lane points a planned closure (Civic/RoadClosure) may not take, on each line's way into and out of its stops.
        private const float GuardBefore = 120f;     // metres
        private const float GuardAfter = 40f;       // metres
        private static readonly HashSet<int> Guarded = new HashSet<int>();

        /// <summary>True if a lane point is on a bus line's way into or out of one of its stops: planned works do not shut it.</summary>
        public static bool Guards(int lanePoint)
        {
            return Guarded.Contains(lanePoint);
        }

        /// <summary>True once the network has been laid out; it may be empty (see the log).</summary>
        public static bool Ready { get; private set; }

        /// <summary>True while the network is being laid out: the director then comes back every frame.</summary>
        public static bool PlacingStops => !Ready;

        /// <summary>True once the routes are laid (behind the loading screen, as a rule), before or after their stops are.</summary>
        public static bool RoutesLaid => Ready || _phase >= 2;

        private static Waypoint[] _points;
        private static readonly List<SubwayStation> Stations = new List<SubwayStation>(24);
        private static readonly List<List<int>> Terminals = new List<List<int>>(24);
        private static readonly List<int> PathScratch = new List<int>(2048);
        private static readonly List<float> XScratch = new List<float>(2048);
        private static readonly List<float> ZScratch = new List<float>(2048);
        private static readonly StringBuilder Notes = new StringBuilder(512);
        private static readonly Dictionary<int, int> HostIndex = new Dictionary<int, int>(256);   // lane point: the index in Hosts of the stop laid there
        private static readonly List<TransitStop> Hosts = new List<TransitStop>(256);
        private static CityBuildingController[] _buildings;
        private static RouteSearch _graph;
        private static TransitPlan.Pairing _pairs;
        private static TransitLine _there;      // the way out to the partner being tried, waiting for the way back
        private static int _phase;              // 0 not begun, 1 pairing, 2 stops
        private static int _pa;                 // the pair being tried
        private static int _pb;
        private static bool _trying;
        private static int _routes;
        private static int _searches;
        private static bool _seeking;           // a search of the road graph is under way, a slice at a time
        private static int _refused;
        private static int _laneChanges;
        private static int _lineCursor;
        private static bool _lineBegun;         // the line at the cursor has had some of its stops looked for
        private static int _shared;             // stops laid at a place another line laid first
        private static int _staticMask;
        private static int _doorMask;
        private static int _parkingMask;
        private static int _propsLayer = -1;

        public static int Bays { get; private set; }   // how many of the places have a bay

        public static void Reset()
        {
            BusZones.Restore();   // before the stops go: it marks their zones as not made
            Lines.Clear();
            Stops.Clear();
            Places.Clear();
            Net = null;
            Guarded.Clear();
            Stations.Clear();
            Terminals.Clear();
            PathScratch.Clear();
            HostIndex.Clear();
            Hosts.Clear();
            SharedBy.Clear();
            Notes.Length = 0;
            _points = null;
            _buildings = null;
            _graph = null;
            _pairs = null;
            _there = null;
            _phase = 0;
            _pa = -1;
            _pb = -1;
            _trying = false;
            _seeking = false;
            _routes = 0;
            _searches = 0;
            _refused = 0;
            _laneChanges = 0;
            _lineCursor = 0;
            _lineBegun = false;
            _shared = 0;
            Bays = 0;
            Ready = false;
        }

        public static Waypoint Point(int index)
        {
            return _points != null && index >= 0 && index < _points.Length ? _points[index] : null;
        }

        /// <summary>Behind the loading screen, once the traffic system has started: the routes laid whole. False while the city is not there yet.</summary>
        public static bool Prewarm()
        {
            if (!Settings.Buses || Ready || _phase >= 2)
            {
                return true;
            }
            CurrentSceneData data = RoadData.Scene;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (data == null || data.allWaypoints == null || city == null || city.subwayStations == null || !TrafficManager.IsInitialized)
            {
                return false;
            }
            using (SessionWatch.Piece("transit: the bus routes laid between the subway stations (behind the loading screen)"))
            {
                for (int guard = 0; guard < MaxSearches * 4 + 64 && !Ready && _phase < 2; guard++)
                {
                    Next(false);
                }
            }
            return true;
        }

        /// <summary>Lays out what is left of the network now, for a test pressed before it was done.</summary>
        public static void Complete()
        {
            for (int guard = 0; guard < MaxSearches * 4 + 64 + 4096 && !Ready; guard++)
            {
                Next(false);
            }
        }

        /// <summary>The next slice of the layout, from a timer until <see cref="Ready"/>; a fault leaves an empty network.</summary>
        public static void Step()
        {
            using var hotSpot = HotSpots.Time(HotStep);
            Next(true);
        }

        private static readonly int HotStep = HotSpots.Slot("Transport: TransitMap.Step");

        // In play (sliced) each search and the stops a slice of about a millisecond; behind the loading screen whole.
        private static void Next(bool sliced)
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
                    if (!PairStep(sliced ? WorkSlice.Of(SeekSliceMs, 1) : WorkSlice.Whole))
                    {
                        _phase = 2;
                        _graph = null;   // the search's memory is the size of the road graph: let go of now
                        _pairs = null;
                    }
                    return;
                }
                if (_lineCursor < Lines.Count)
                {
                    // Stop after stop until the slice's time is up, at least one each slice.
                    WorkSlice slice = sliced ? WorkSlice.Of(StopsSliceMs, 1) : WorkSlice.Whole;
                    while (_lineCursor < Lines.Count && slice.More())
                    {
                        if (PlaceStop(Lines[_lineCursor]))
                        {
                            _lineCursor++;
                        }
                    }
                    return;
                }
                Finish();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Lines.Clear();
                Stops.Clear();
                Places.Clear();
                _seeking = false;
                Finish();
            }
        }

        private static void Finish()
        {
            using var piece = SessionWatch.Piece("transit: the bus network finished (lines, places, timetables)");
            // A line with fewer than two stops carries nobody; a place it laid out goes to the next line that stops there.
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (Lines[i].Stops.Count < 2)
                {
                    TransitLine gone = Lines[i];
                    Lines.RemoveAt(i);
                    for (int s = 0; s < gone.Stops.Count; s++)
                    {
                        Rehost(gone.Stops[s]);
                    }
                }
            }
            Stops.Clear();
            Places.Clear();
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
                    stop.Calls.Clear();
                    stop.Near.Clear();
                    Stops.Add(stop);
                    for (int k = 0; k < line.Path.Length; k++)
                    {
                        float from = line.Metres[k] - stop.Metres;
                        if (from >= -GuardBefore && from <= GuardAfter)
                        {
                            Guarded.Add(line.Path[k]);
                        }
                    }
                }
            }
            for (int i = 0; i < Stops.Count; i++)
            {
                TransitStop stop = Stops[i];
                if (stop.IsHost)
                {
                    stop.Host = stop;
                    stop.Place = Places.Count;
                    Places.Add(stop);
                    Bays += stop.HasBay ? 1 : 0;
                }
            }
            for (int i = 0; i < Stops.Count; i++)
            {
                TransitStop stop = Stops[i];
                TransitStop host = stop.At;
                stop.Place = host.Place;
                if (host == stop)
                {
                    host.Calls.Insert(0, stop);
                }
                else
                {
                    host.Calls.Add(stop);
                }
            }
            Timetables();
            Ready = true;
            _graph = null;
            _pairs = null;
            _buildings = null;
            _there = null;
            PathScratch.Clear();
            PathScratch.TrimExcess();
            XScratch.Clear();
            XScratch.TrimExcess();
            ZScratch.Clear();
            ZScratch.TrimExcess();
            HostIndex.Clear();
            Hosts.Clear();
            SharedBy.Clear();
            for (int i = 0; i < Terminals.Count; i++)
            {
                Terminals[i].Clear();
            }
            if (Lines.Count == 0)
            {
                Logger?.Warn("Transit: no bus line could be laid out in this city (" + Stations.Count + " subway stations, "
                             + (_points != null ? _points.Length : 0) + " lane points, " + _searches + " searches); there are no buses.");
                Terminals.Clear();
                return;
            }
            Report();
            Terminals.Clear();
        }

        private static void Rehost(TransitStop gone)
        {
            if (!gone.IsHost)
            {
                return;
            }
            TransitStop next = null;
            for (int i = 0; i < Lines.Count; i++)
            {
                List<TransitStop> stops = Lines[i].Stops;
                for (int s = 0; s < stops.Count; s++)
                {
                    if (stops[s].Host != gone)
                    {
                        continue;
                    }
                    if (next == null)
                    {
                        next = stops[s];
                        next.Host = next;
                        next.Zone = gone.Zone;
                        next.Boarding.Clear();
                        next.Boarding.AddRange(gone.Boarding);
                        next.BoardingMade = gone.BoardingMade;
                        next.ZoneLook = gone.ZoneLook;
                        if (!next.HasBay)
                        {
                            next.Zone = null;   // its own way in is not straight: no zone for a bay nobody uses
                        }
                    }
                    else
                    {
                        stops[s].Host = next;
                    }
                }
            }
        }

        // The main lines (the longest routes), each line's place in its timetable, and the planner's network.
        private static void Timetables()
        {
            var lengths = new List<KeyValuePair<string, float>>(Lines.Count);
            for (int i = 0; i < Lines.Count; i++)
            {
                TransitLine line = Lines[i];
                float metres = line.Metres[line.Metres.Length - 1];
                int at = lengths.FindIndex(p => p.Key == line.Number);
                if (at < 0)
                {
                    lengths.Add(new KeyValuePair<string, float>(line.Number, metres));
                }
                else if (metres > lengths[at].Value)
                {
                    lengths[at] = new KeyValuePair<string, float>(line.Number, metres);
                }
            }
            lengths.Sort((x, y) => y.Value.CompareTo(x.Value));
            var net = new TransitPlan.Net();
            for (int i = 0; i < Places.Count; i++)
            {
                net.AddPlace(Places[i].Sign.x, Places[i].Sign.z);
            }
            for (int i = 0; i < Lines.Count; i++)
            {
                TransitLine line = Lines[i];
                int rank = lengths.FindIndex(p => p.Key == line.Number);
                line.Main = rank >= 0 && rank < TransitPlan.MainRoutes;
                line.Seed = i * 7 + 3;
                line.Minutes = new float[line.Stops.Count];
                var places = new int[line.Stops.Count];
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    line.Minutes[s] = TransitTrips.RideMinutes(line.Stops[s].Metres - line.Stops[0].Metres, s);
                    places[s] = line.Stops[s].Place;
                }
                net.AddLine(places, line.Minutes);
            }
            net.Link(TransferWalk);
            var sx = new float[Stations.Count];
            var sz = new float[Stations.Count];
            for (int s = 0; s < Stations.Count; s++)
            {
                sx[s] = Stations[s].transform.position.x;
                sz[s] = Stations[s].transform.position.z;
            }
            net.Interchanges(sx, sz, TransitPlan.InterchangeWithin);
            // The places' own lists are the planner's, so a change on foot is offered where it plans one.
            for (int i = 0; i < Places.Count; i++)
            {
                Places[i].Near.Clear();
                List<int> near = net.Near[i];
                for (int k = 0; k < near.Count; k++)
                {
                    Places[i].Near.Add(Places[near[k]]);
                }
            }
            Net = net;
        }

        private static bool Begin()
        {
            CurrentSceneData data = RoadData.Scene;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (data == null || data.allWaypoints == null || data.allWaypoints.Length < 100 || city == null || city.subwayStations == null)
            {
                return false;
            }
            _points = data.allWaypoints;
            _buildings = GameDlc.Open(city);
            _staticMask = LayerMask.GetMask("StreetProps", "Buildings", "BuildingsOutlined", "BuildingWalls");
            _doorMask = LayerMask.GetMask("InteractiveItems", "InteractiveItemsOutlined");
            _parkingMask = LayerMask.GetMask("ParkingArea");
            _propsLayer = LayerMask.NameToLayer("StreetProps");

            Stations.Clear();
            for (int i = 0; i < city.subwayStations.Count; i++)
            {
                if (city.subwayStations[i] != null)
                {
                    Stations.Add(city.subwayStations[i]);
                }
            }
            Stations.Sort((a, b) => ((int)a.stationName).CompareTo((int)b.stationName));

            // A line may start and end on any plain lane point of a road proper near its station.
            Terminals.Clear();
            int withEnds = 0;
            var x = new float[Stations.Count];
            var z = new float[Stations.Count];
            var ends = new bool[Stations.Count];
            for (int s = 0; s < Stations.Count; s++)
            {
                Vector3 at = Stations[s].transform.position;
                x[s] = at.x;
                z[s] = at.z;
                var near = new List<int>(64);
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
                    if (RoadGraph.Drivable(point) && TrafficManager.IsNotIntersection(point))
                    {
                        near.Add(i);
                    }
                }
                Terminals.Add(near);
                ends[s] = near.Count > 0;
                withEnds += near.Count > 0 ? 1 : 0;
            }
            if (withEnds < 2)
            {
                return false;
            }
            BuildGraph();
            _pairs = new TransitPlan.Pairing(x, z, ends);
            _trying = false;
            return true;
        }

        // The city's one road graph (Core/RoadGraph.Shared, the GPS's too), searched with edge costs of its own for the corridors.
        private static void BuildGraph()
        {
            RouteSearch roads = RoadGraph.Shared(_points);
            if (roads != null)
            {
                _graph = RouteSearch.Over(roads, true);
                _laneChanges = RoadGraph.SharedLaneChanges;
            }
            else
            {
                _graph = RoadGraph.Build(_points, LaneChangeCost, out _laneChanges);   // the traffic system not started: never so where the street is ready
            }
        }

        // One search of the road graph (or a slice of it), or the choice of the next pair. False when nothing is left to pair.
        private static bool PairStep(WorkSlice slice)
        {
            if (!_seeking && (_routes >= MaxRoutes || _searches >= MaxSearches))
            {
                return false;
            }
            if (!_trying)
            {
                if (!_pairs.Next(out _pa, out _pb))
                {
                    return false;
                }
                _trying = true;
                _there = null;
                return true;
            }
            if (!_seeking)
            {
                _searches++;
            }
            if (_there == null)
            {
                if (!Seek(_pa, _pb, ref slice, out string why))
                {
                    return true;   // more of the search on the next slice
                }
                if (why == null)
                {
                    _there = MakeLine(_pa, _pb);
                    return true;
                }
                Refuse(why);
                return true;
            }
            if (!Seek(_pb, _pa, ref slice, out string back))
            {
                return true;
            }
            if (back != null)
            {
                Refuse("a way there but " + back + " back");
                return true;
            }
            TransitLine line = MakeLine(_pb, _pa);
            string number = "M" + (_routes + 1);
            _there.Number = number;
            line.Number = number;
            Lines.Add(_there);
            Lines.Add(line);
            _routes++;
            Corridor(_there.Path);
            Corridor(line.Path);
            _pairs.Ends(_pa, _pb);
            _pairs.Done(_pa, _pb);
            for (int s = 0; s < Stations.Count; s++)
            {
                if (Passes(_there, Stations[s].transform.position) || Passes(line, Stations[s].transform.position))
                {
                    _pairs.Serve(s);
                }
            }
            _there = null;
            _trying = false;
            return true;
        }

        private static void Refuse(string why)
        {
            _refused++;
            Notes.Append(' ').Append(Stations[_pa].stationName).Append(" and ").Append(Stations[_pb].stationName).Append(" (").Append(why).Append(");");
            _pairs.Done(_pa, _pb);
            _there = null;
            _trying = false;
        }

        private static bool Passes(TransitLine line, Vector3 at)
        {
            float within = TransitPlan.ServedWithin * TransitPlan.ServedWithin;
            for (int i = 0; i < line.Path.Length; i += 2)
            {
                if (CitySpots.Flat(_points[line.Path[i]].position - at) < within)
                {
                    return true;
                }
            }
            return false;
        }

        // The streets a line runs along count less to the lines laid after it.
        private static void Corridor(int[] path)
        {
            int[] starts = _graph.EdgeStart;
            int[] to = _graph.EdgeTo;
            float[] extra = _graph.EdgeExtra;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                int a = path[i];
                int b = path[i + 1];
                for (int e = starts[a]; e < starts[a + 1]; e++)
                {
                    if (to[e] == b && extra[e] == 0f)
                    {
                        extra[e] = -TransitPlan.CorridorShare * Vector3.Distance(_points[a].position, _points[b].position);
                    }
                }
            }
        }

        // The way from one station to another, searched for as long as the slice allows: true once decided, with why there is
        // none, or null for a way in PathScratch. Step for step what RouteSearch.Find does in one call.
        private static bool Seek(int from, int to, ref WorkSlice slice, out string why)
        {
            why = null;
            RouteSearch.Outcome outcome = RouteSearch.Outcome.NoWay;
            if (!_seeking)
            {
                Vector3 goal = Stations[to].transform.position;
                PathScratch.Clear();
                if (!_graph.Begin(Terminals[from], Terminals[to], goal.x, goal.y, goal.z, TerminalReach + 6f))
                {
                    why = Judge(from, to, false);
                    return true;
                }
                _seeking = true;
            }
            do
            {
                outcome = _graph.Advance(Math.Min(_graph.Expanded + SeekSteps, MaxExpansions), PathScratch);
            }
            while (outcome == RouteSearch.Outcome.Searching && _graph.Expanded < MaxExpansions && slice.More());
            if (outcome == RouteSearch.Outcome.Searching)
            {
                if (_graph.Expanded < MaxExpansions)
                {
                    return false;
                }
                _graph.GiveUp();   // at its limit, as a search made whole stops there
            }
            _seeking = false;
            why = Judge(from, to, outcome == RouteSearch.Outcome.Found);
            return true;
        }

        private static string Judge(int from, int to, bool found)
        {
            found = found && PathScratch.Count > 8;
            if (!found)
            {
                if (!_graph.HitLimit)
                {
                    // What the search never got near cannot be driven to from here either: never asked for.
                    for (int s = 0; s < Stations.Count; s++)
                    {
                        List<int> ends = Terminals[s];
                        bool near = false;
                        for (int e = 0; e < ends.Count && !near; e++)
                        {
                            near = _graph.WasReached(ends[e]);
                        }
                        if (!near && s != from)
                        {
                            _pairs.NoRoad(from, s);
                        }
                    }
                }
                return _graph.HitLimit ? "the search stopped at its limit of " + MaxExpansions + " steps" : "no road";
            }
            XScratch.Clear();
            ZScratch.Clear();
            for (int i = 0; i < PathScratch.Count; i++)
            {
                Vector3 at = _points[PathScratch[i]].position;
                XScratch.Add(at.x);
                ZScratch.Add(at.z);
            }
            return TransitPlan.Unbelievable(XScratch, ZScratch, XScratch.Count, _pairs.Span(from, to));
        }

        private static TransitLine MakeLine(int from, int to)
        {
            var line = new TransitLine
            {
                ToKey = GameMembers.NameKey(Stations[to]),
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

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        /// <summary>The place nearest a place within a distance, or null. A pass over the places, for a timer.</summary>
        public static TransitStop Nearest(Vector3 place, float within, TransitStop not = null)
        {
            TransitStop nearest = null;
            float best = within * within;
            for (int i = 0; i < Places.Count; i++)
            {
                TransitStop stop = Places[i];
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

        public static int Onward(TransitStop stop)
        {
            return stop == null ? 0 : Lines[stop.Line].Stops.Count - 1 - stop.Order;
        }

        /// <summary>Every line's number in one text ("M1 M2 M3"): each letter and figure a stop's sign can show. Made anew each call.</summary>
        public static string LineNumbers()
        {
            var text = new StringBuilder(Lines.Count * 4);
            for (int i = 0; i < Lines.Count; i++)
            {
                text.Append(i > 0 ? " " : string.Empty).Append(Lines[i].Number);
            }
            return text.ToString();
        }

        /// <summary>The numbers of the lines that stop at a place, in the order they were laid ("M2 M5"), each once.</summary>
        public static string Numbers(TransitStop place, string between = " ")
        {
            if (place == null)
            {
                return string.Empty;
            }
            List<TransitStop> calls = place.At.Calls;
            if (calls.Count == 0)
            {
                return place.Line >= 0 && place.Line < Lines.Count ? Lines[place.Line].Number : string.Empty;
            }
            var text = new StringBuilder(16);
            for (int i = 0; i < calls.Count; i++)
            {
                string number = Lines[calls[i].Line].Number;
                bool again = false;
                for (int k = 0; k < i && !again; k++)
                {
                    again = Lines[calls[k].Line].Number == number;
                }
                if (!again)
                {
                    text.Append(text.Length > 0 ? between : string.Empty).Append(number);
                }
            }
            return text.ToString();
        }
    }
}
