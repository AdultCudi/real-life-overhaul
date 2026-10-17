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
    /// <summary>
    /// One line's stop: a fixed place at the kerb beside a plain stretch of lane. Where several lines run along the same
    /// lane they share the place (<see cref="Host"/>): each has a stop of its own (its own place along its own line), with
    /// the host's pole, shelter, bay and zone.
    /// </summary>
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
        public BusZone Zone;      // where it has one, what its zone needs done to the street (BusZones): the host's only

        // The boarding strip (BusInside.BoardingStrips): the game's street furniture standing on the kerb by the doors or on the
        // way from the waiting place to them, switched off while the buses run (BusZones), so that nobody walks round it to board.
        public readonly List<GameObject> Boarding = new List<GameObject>(4);
        public bool BoardingMade; // switched off now

        public float Stand;       // metres on from the pole that the bus's middle stands (BusInside.StandShift): neither door lines up with what stands on the kerb
        public int KerbThings;    // what stands on the kerb by the doors and stays (a tree, a lamp, the pole), for the log
        public float ZoneLook;    // the director's time at which BusZones next looks at it while something there waits to be out of view
        // The place, shared by the lines that stop here.
        public int LanePoint = -1;   // the lane point's index in the road graph
        public TransitStop Host;     // the stop that laid the place out (the first line to stop here); null or itself for that one
        public int Place = -1;       // the place's index in TransitMap.Places
        public readonly List<TransitStop> Calls = new List<TransitStop>(3);   // (a host's) every line's stop here, its own first
        public readonly List<TransitStop> Near = new List<TransitStop>(3);    // (a host's) other places a short walk away: a change of bus on foot

        /// <summary>The length of strip the bay takes up.</summary>
        public float BayLength => BayBehind + BayAhead;

        /// <summary>True for the stop that laid the place out: its pole, shelter, bay and zone are the place's.</summary>
        public bool IsHost => Host == null || Host == this;

        /// <summary>The place: the host stop.</summary>
        public TransitStop At => Host ?? this;
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
        public bool Main;         // one of the main lines: runs more often, and through the night
        public int Seed;          // its place in its timetable (TransitPlan.Offset)
        public float[] Minutes;   // game minutes of a ride from its first stop to each of its stops (TransitTrips)

        public string To => ToKey.GetLocalization();

        /// <summary>The line's offset in its timetable at one of its stops (TransitPlan.NextCall).</summary>
        public double OffsetAt(int order)
        {
            return TransitPlan.Offset(Seed, Minutes != null && order >= 0 && order < Minutes.Length ? Minutes[order] : 0f);
        }
    }

    /// <summary>
    /// The bus network, laid over the city by rule once per city load and kept in memory only
    /// (nothing of it is saved, and the same city always gives the same network).
    ///
    /// THE ROUTES. The ends of the routes are the game's own subway stations. Every station should be served by
    /// TransitPlan.StationTarget lines (ending there, or passing within TransitPlan.ServedWithin): the pair asked for
    /// next is the one that serves the stations furthest short of that, 600 m to 3.2 km apart (best near 1.9 km: across
    /// town, up an avenue), in a direction neither end has a route in yet (TransitPlan.Pairing), up to 16 routes. A
    /// station's end of a line is any plain lane point within 150 m of it, not one chosen point (<see cref="RouteSearch"/>
    /// says why), and the way is the shortest along the road graph following lanes, with a change of lane allowed where
    /// the graph offers one at the price of 30 m of road. A route is laid only with both its directions, usually on the
    /// two sides of the same streets, and only if neither doubles back or is much longer than the straight line
    /// (TransitPlan.Unbelievable). The streets a route runs along count a fifth less to the routes laid after it
    /// (TransitPlan.CorridorShare): the lines gather on the avenues, as a city's do, and there share their stops. A
    /// search that never got near a station says that no road leads there from here: that pair is never tried.
    ///
    /// THE STOPS. Along each path a stop is looked for every 260 m. A stop another line has already laid on the stretch
    /// looked along is shared: this line stops there too (TransitPlan.HostAlong), with a stop of its own at the same lane
    /// point, the same pole and the same bay (where its own way into the bay is plain and straight; else it stops in the
    /// lane there). Else, in the 140 m after, the first lane point that is: on a road proper with no junction within
    /// 26 m either way, straight, at street level, with pavement within 5.6 m to its right that is at least 1.5 m deep
    /// and has no door, wall or street furniture where the pole and the waiting people stand. Vehicles are not looked
    /// at here (they come and go): they are tested when a stop is dressed and when a bus pulls up. Two places within
    /// TransferWalk of each other are joined for a change of bus on foot (the other side of the street, the next corner).
    ///
    /// BAYS. A stop has a bay where the kerb is at least 4.20 m from the lane's line (the lane's
    /// half, the bus's side on its edge, the 2.40 m bus, 0.15 m to the kerb: BusBay.KerbNeeded)
    /// along the whole length the bus needs to come in and pull out (some 48 m, from 28 m behind
    /// the pole to 20 m beyond it, more where the strip is wider: BusBay.Lay, swept as the bus is
    /// driven), the lane runs straight and plain for that length with no other lane
    /// between it and the kerb, the strip is level road, and the kerb keeps its line. The kerb is
    /// taken where the bus can come up to it: where something on the pavement reaches out over its
    /// edge, that far out (KerbLine). The length of strip is then the buses' own, a bus zone
    /// (BusZones): the game's parking there is cut back, and street furniture standing in the
    /// strip itself is switched off. So a bay is refused only for what a zone cannot change: a
    /// building, something that is not plain furniture, parking that is not a plain run along the
    /// kerb, or a run that would lose more than BusZones.LossMost metres beyond the zone. Of the
    /// places that will do for a stop, one with a bay is taken before one without: first in the
    /// 140 m, then up to 60 m further on, then up to 50 m before the place wanted (two stops are
    /// then never nearer than 210 m along the line), and only then the first place without one.
    /// Parked cars are not looked at here either: the zone takes them in when it is made.
    ///
    /// WHEN. The routes (the ends at the stations and some dozens of searches of the road graph, 150 to
    /// 250 ms in all) are laid behind the loading screen, in the game's late "game loaded" call once its
    /// traffic system has marked the junctions (<see cref="Prewarm"/>, a late piece of Core/SceneFinds'
    /// ScenePrewarm), over the city's one road graph (Core/RoadGraph.Shared, the GPS's too, with costs of
    /// its own for the corridors). They used to be laid in play: the ends and a graph of its own in one
    /// frame (1188 KB and 20 to 50 ms), then a search a frame of 3 to 6 ms
    /// for some 40 frames. The stops need the street (rays and samples against what stands on it, some
    /// thousands, most of them looking for bays), so they are placed on the director's timer in the first
    /// seconds out of doors, a slice a frame of about a millisecond (<see cref="WorkSlice"/>), never again.
    /// Where the routes could not be laid behind the loading screen (buses switched on later, a city whose
    /// traffic had not started), they are laid on the timer as before, each search a slice a frame of about
    /// a millisecond (<see cref="RouteSearch.Advance"/>), with exactly the outcome of a search made whole.
    /// </summary>
    public static class TransitMap
    {
        public const int MaxRoutes = 16;
        public const float TransferWalk = 160f;     // metres between two places a change of bus is made between on foot
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
        private const float KerbLookBehind = 33f;   // metres behind the pole and ahead of it the kerb's edge is looked along: all a bay may take up (BusBay.Lay)
        private const float KerbLookAhead = 26f;
        private const float BayAheadSlack = 1f;     // metres more of the strip ahead than the body on its van needs to pull out: a vehicle's own middle is seldom quite its origin
        private const int MaxExpansions = 400000;
        private const int MaxSearches = 100;        // in all: a city where nothing connects is given up on
        private const double StopsSliceMs = 1.0;    // milliseconds of looking for stops in one slice (a frame): the slice ends after the first stop past it
        private const double SeekSliceMs = 1.0;     // milliseconds of a search of the road graph in one slice, where it is made in play
        private const int SeekSteps = 64;           // steps of a search between two looks at the clock

        public static IModLogger Logger;

        public static readonly List<TransitLine> Lines = new List<TransitLine>(MaxRoutes * 2);
        public static readonly List<TransitStop> Stops = new List<TransitStop>(320);

        /// <summary>The places the buses stop at: one host stop each, with every line's stop there in its <see cref="TransitStop.Calls"/>.</summary>
        public static readonly List<TransitStop> Places = new List<TransitStop>(200);

        /// <summary>The network as the journey planner reads it (TransitPlan.Net): its places are <see cref="Places"/>, its lines <see cref="Lines"/>.</summary>
        public static TransitPlan.Net Net { get; private set; }

        // The lane points a planned closure may not take (Civic/RoadClosure): each line's own from GuardBefore metres before
        // each of its stops to GuardAfter past it. Shut there, a stop is cut off: no bus can be put on the road before it
        // out of sight, and the panel would say DELAYED for as long as the works stand.
        private const float GuardBefore = 120f;
        private const float GuardAfter = 40f;
        private static readonly HashSet<int> Guarded = new HashSet<int>();

        /// <summary>True if a lane point is on a bus line's way into or out of one of its stops: planned works do not shut it.</summary>
        public static bool Guards(int lanePoint)
        {
            return Guarded.Contains(lanePoint);
        }

        /// <summary>True once the network has been laid out (it may be empty: see the log).</summary>
        public static bool Ready { get; private set; }

        /// <summary>True while the stops are being placed, a slice at a time: the director then comes back every frame, so that the stops are there in seconds and not in minutes.</summary>
        public static bool PlacingStops => !Ready;   // (the pairing too where it was not done behind the loading screen: a slice of a search a frame, about a millisecond)
        public static bool Failed { get; private set; }

        private static Waypoint[] _points;
        private static readonly List<SubwayStation> Stations = new List<SubwayStation>(24);
        private static readonly List<List<int>> Terminals = new List<List<int>>(24);
        private static readonly List<int> PathScratch = new List<int>(2048);
        private static readonly List<float> XScratch = new List<float>(2048);
        private static readonly List<float> ZScratch = new List<float>(2048);
        private static readonly StringBuilder Notes = new StringBuilder(512);
        private static readonly Dictionary<int, int> HostIndex = new Dictionary<int, int>(256);   // lane point: the host stop laid there (an index into Hosts)
        private static readonly List<TransitStop> Hosts = new List<TransitStop>(256);
        private static Func<int, int> _hostAt;
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
        private static float _stopWanted;       // metres along it at which its next stop is wanted
        private static int _stopFrom;           // ...and the point of its path the look goes on from
        private static int _shared;             // stops laid at a place another line laid first
        private static int _staticMask;
        private static int _doorMask;
        private static int _parkingMask;
        private static int _propsLayer = -1;
        private static readonly BusZone Zoning = new BusZone();   // what the bay being laid out would need done: kept for the stop when it has one

        /// <summary>How many of the stops have a bay.</summary>
        public static int Bays { get; private set; }

        public static void Reset()
        {
            BusZones.Restore();   // (before the stops go: it marks their zones as not made)
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
            Failed = false;
        }

        public static Waypoint Point(int index)
        {
            return _points != null && index >= 0 && index < _points.Length ? _points[index] : null;
        }

        /// <summary>
        /// Behind the loading screen, once the game has started its traffic system (a late piece of ScenePrewarm): the routes
        /// laid, every search made whole. The stops are left to the timer (they need the street). True when that is done, or
        /// there is nothing to do (buses off: laid on the timer if they are switched on); false while the city's roads, its
        /// stations or its traffic system are not there yet (the timer then lays them, as before).
        /// </summary>
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

        /// <summary>For a test pressed before the layout has finished: all of what is left of it now, in one frame.</summary>
        public static void Complete()
        {
            for (int guard = 0; guard < MaxSearches * 4 + 64 + 4096 && !Ready; guard++)
            {
                Next(false);
            }
        }

        /// <summary>
        /// Does the next slice of the layout. Call from a timer while the traffic system is running
        /// until <see cref="Ready"/>; a fault ends it with an empty network and one line in the log.
        /// </summary>
        public static void Step()
        {
            Next(true);
        }

        // One step of the layout; in play (sliced) each search and the stops a slice of about a millisecond, behind the loading screen whole.
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
                    // Stop after stop, line after line, until the slice's time is up: at least one each slice.
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
                Failed = true;
                _seeking = false;
                Finish();
            }
        }

        private static void Finish()
        {
            // Lines that got fewer than two stops carry nobody anywhere; a place one of them laid out goes to the next line that stops there.
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

        // A stop of a line that is let go: if it laid out a place other lines stop at, the first of them has the place now.
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
                            next.Zone = null;   // (its own way in is not straight: no zone is made for a bay nobody uses)
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
            // The places' own lists are the planner's: a change on foot is offered where it plans one.
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

        // The network in the log: every line, how many lines each place and each station has, and the changes of bus.
        private static void Report()
        {
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
            int[] byLines = new int[6];
            int transfers = 0;
            for (int i = 0; i < Places.Count; i++)
            {
                byLines[Mathf.Min(5, Places[i].Calls.Count)]++;
                transfers += Places[i].Calls.Count >= 2 || Places[i].Near.Count > 0 ? 1 : 0;
            }
            var station = new StringBuilder(256);
            int stationsServed = 0;
            int stationsThree = 0;
            for (int s = 0; s < Stations.Count; s++)
            {
                int serving = StationLines(Stations[s].transform.position);
                stationsServed += serving > 0 ? 1 : 0;
                stationsThree += serving >= 3 ? 1 : 0;
                station.Append(' ').Append(Stations[s].stationName).Append(' ').Append(serving).Append(';');
            }
            var text = new StringBuilder("Transit check (network): " + Lines.Count + " bus lines (" + _routes + " routes, " + bothWays + " of the lines with their other direction), "
                                         + Stops.Count + " stops at " + Places.Count + " places, " + Bays + " of the places with a bay (the bus stands out of the lane there; a bay wants the kerb "
                                         + BusBay.KerbNeeded(TransitBus.BodyHalfWidth).ToString("F2") + " m or more from the lane's line), " + _shared + " stops shared with a line laid before; places served by 1 line "
                                         + byLines[1] + ", by 2 " + byLines[2] + ", by 3 " + byLines[3] + ", by 4 or more " + (byLines[4] + byLines[5]) + "; " + transfers + " places where a change of bus can be made (another line there or within "
                                         + TransferWalk.ToString("F0") + " m); subway stations with a line within " + TransitPlan.ServedWithin.ToString("F0") + " m " + stationsServed + " of " + Stations.Count + ", with 3 or more "
                                         + stationsThree + " (" + station + "); " + _searches + " searches of " + (_points != null ? _points.Length : 0) + " lane points (" + _laneChanges + " lane changes offered), "
                                         + _refused + " pairs refused: " + (bothWays == Lines.Count ? "PASS" : "FAIL (a line runs one way only)") + ".");
            for (int i = 0; i < Lines.Count; i++)
            {
                TransitLine line = Lines[i];
                int sheltered = 0;
                int bays = 0;
                int shared = 0;
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    sheltered += line.Stops[s].Shelter ? 1 : 0;
                    bays += line.Stops[s].HasBay ? 1 : 0;
                    shared += line.Stops[s].At.Calls.Count > 1 ? 1 : 0;
                }
                text.Append(" ").Append(line.Number).Append(line.Main ? " (main)" : string.Empty).Append(" to ").Append(line.To).Append(' ')
                    .Append((line.Metres[line.Metres.Length - 1] / 1000f).ToString("F1")).Append(" km ")
                    .Append(line.Stops.Count).Append(" stops (").Append(shared).Append(" shared with another line, ").Append(sheltered).Append(" with a shelter, ").Append(bays).Append(" with a bay; the kerb at");
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
            if (Notes.Length > 0)
            {
                Logger?.Info("Transit: the pairs of stations not joined:" + Notes);
                Notes.Length = 0;
            }
            LogPoles();
        }

        // How many routes have a stop within reach of a station.
        private static int StationLines(Vector3 at)
        {
            int count = 0;
            for (int i = 0; i < Lines.Count; i++)
            {
                bool earlier = false;
                for (int k = 0; k < i && !earlier; k++)
                {
                    earlier = Lines[k].Number == Lines[i].Number;
                }
                if (earlier)
                {
                    continue;
                }
                bool near = false;
                for (int k = i; k < Lines.Count && !near; k++)
                {
                    if (Lines[k].Number != Lines[i].Number)
                    {
                        continue;
                    }
                    for (int s = 0; s < Lines[k].Stops.Count && !near; s++)
                    {
                        near = CitySpots.Flat(Lines[k].Stops[s].Sign - at) < (TransitPlan.ServedWithin + 30f) * (TransitPlan.ServedWithin + 30f);
                    }
                }
                count += near ? 1 : 0;
            }
            return count;
        }

        // Where the poles stand and the buses stop by them: one line.
        private static void LogPoles()
        {
            int clear = 0, shifted = 0, things = 0;
            for (int i = 0; i < Stops.Count; i++)
            {
                TransitStop stop = Stops[i];
                Vector3 right = Vector3.Cross(Vector3.up, stop.Along);
                Vector3 toWait = Flat(stop.Wait - stop.Lane), toPole = Flat(stop.Sign - stop.Lane);
                float kerb = Vector3.Dot(Flat(stop.Kerb - stop.Lane), right);
                clear += BusInside.SignClear(Vector3.Dot(toPole, stop.Along), Vector3.Dot(toPole, right), kerb, Vector3.Dot(toWait, stop.Along), Vector3.Dot(toWait, right), stop.Stand) ? 1 : 0;
                shifted += Mathf.Abs(stop.Stand) > 0.01f ? 1 : 0;
                things += stop.KerbThings;
            }
            Logger?.Info("Transit check (poles): " + clear + " of " + Stops.Count + " stops' poles stand clear of both doors and the ways to them; the bus stands on from its pole or short of it at "
                         + shifted + " stop(s) so that neither door lines up with what stays on the kerb (" + things + " thing(s) in all, the poles counted): "
                         + (clear == Stops.Count ? "PASS" : "FAIL") + ".");
        }

        // ---------------------------------------------------------------- the ends of the routes

        private static bool Begin()
        {
            CurrentSceneData data = RoadData.Scene;
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

            // Where a station's buses may start from and end at: every plain lane point of a road proper near it.
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
                    if (Drivable(point) && TrafficManager.IsNotIntersection(point))
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

        private static bool Drivable(Waypoint point)
        {
            return RoadGraph.Drivable(point);
        }

        // The city's one road graph (Core/RoadGraph.Shared, the GPS's too, built with the same price for a change of lane),
        // searched with a search of its own whose edge costs are its own: the corridors are counted for less on them alone
        // (Corridor). The graph is the one a build of its own gave, so every route is the one it always was.
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
                _graph = RoadGraph.Build(_points, LaneChangeCost, out _laneChanges);   // (the traffic system not started: never so where the street is ready)
            }
        }

        // ---------------------------------------------------------------- the pairs, and the way between them

        // One search of the road graph (or a slice of it), or the choice of the next pair. False when there is nothing left to pair.
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

        // A line's path passes within reach of a place.
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

        // The streets a line runs along count less to the lines laid after it: the lines gather on them and share their stops.
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

        // The search for the way from one station to another, begun or gone on with, for as long as the slice allows: true once it
        // is decided, with why there is none (no road, or one that will not do for a bus line), or null for a way found into
        // PathScratch. Step for step what RouteSearch.Find does in one call, held to MaxExpansions steps in all.
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

        // A search decided: the way found, or why there is none (no road, or one that will not do for a bus line).
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
                FromKey = GameMembers.NameKey(Stations[from]),
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
                Terminus(line);
                for (int s = 0; s < line.Stops.Count; s++)
                {
                    line.Stops[s].Name = line.Stops[s].IsHost ? NameOf(line.Stops[s].Wait) : line.Stops[s].At.Name;
                }
                _lineBegun = false;
                return true;
            }
            // A stop another line has already laid on this stretch: this line stops there too.
            int hostId = TransitPlan.HostAlong(line.Path, line.Metres, Mathf.Max(wanted - BayBack, FirstStopAt), wanted + StopWindow + BayOn, wanted, 3,
                _hostAt ?? (_hostAt = HostAt), out int hostIndex);
            if (hostId >= 0)
            {
                TransitStop shared = Share(line, hostIndex, Hosts[hostId]);
                if (shared != null)
                {
                    line.Stops.Add(shared);
                    _shared++;
                    CallsAt(Hosts[hostId]);
                    _stopWanted = shared.Metres + StopSpacing;
                    _stopFrom = shared.PathIndex;
                    return false;
                }
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
            // (Never nearer the start of the line than a first stop may be: a bus has to be put on the road before its stop.)
            for (int k = i - 1; bay == null && k >= 0 && line.Metres[k] >= Mathf.Max(wanted - BayBack, FirstStopAt); k--)
            {
                bay = Try(line, k, true);
            }
            TransitStop placed = bay ?? plain;
            if (placed != null)
            {
                placed.LanePoint = line.Path[placed.PathIndex];
                placed.Host = placed;
                if (!HostIndex.ContainsKey(placed.LanePoint))
                {
                    HostIndex[placed.LanePoint] = Hosts.Count;
                    Hosts.Add(placed);
                }
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

        // The line's last stop as near its station as the street allows: a line ends at a station, and the station is where
        // its riders change onto the other lines and the subway. Looked for from the end of the path back, where the last stop
        // laid is more than TerminusGap before it: a stop another line has there first, else the first place that will do.
        private const float TerminusGap = 120f;

        private static void Terminus(TransitLine line)
        {
            float total = line.Metres[line.Metres.Length - 1];
            float last = line.Stops.Count > 0 ? line.Stops[line.Stops.Count - 1].Metres : FirstStopAt - TerminusGap;
            if (total - last <= TerminusGap)
            {
                return;
            }
            int hostId = TransitPlan.HostAlong(line.Path, line.Metres, last + TerminusGap, total, total, 3, _hostAt ?? (_hostAt = HostAt), out int hostIndex);
            if (hostId >= 0)
            {
                TransitStop shared = Share(line, hostIndex, Hosts[hostId]);
                if (shared != null)
                {
                    line.Stops.Add(shared);
                    _shared++;
                    CallsAt(Hosts[hostId]);
                    return;
                }
            }
            for (int j = line.Path.Length - 4; j >= 3 && line.Metres[j] >= last + TerminusGap; j--)
            {
                TransitStop stop = Try(line, j, false);
                if (stop == null)
                {
                    continue;
                }
                stop.LanePoint = line.Path[stop.PathIndex];
                stop.Host = stop;
                if (!HostIndex.ContainsKey(stop.LanePoint))
                {
                    HostIndex[stop.LanePoint] = Hosts.Count;
                    Hosts.Add(stop);
                }
                line.Stops.Add(stop);
                return;
            }
        }

        // Lines stopping at each place so far, the host's own counted: no more than MostLinesAt share one.
        public const int MostLinesAt = 8;
        private static readonly Dictionary<TransitStop, int> SharedBy = new Dictionary<TransitStop, int>(256);

        private static void CallsAt(TransitStop host)
        {
            SharedBy[host] = (SharedBy.TryGetValue(host, out int lines) ? lines : 1) + 1;
        }

        private static int HostAt(int lanePoint)
        {
            return HostIndex.TryGetValue(lanePoint, out int index) ? index : -1;
        }

        // This line's own stop at a place another line laid out: the same pole, shelter and bay, at its own place along its
        // own line. Null if the lane is not plain either side of it on this line's way (it comes in at a junction just
        // before). The bay is the place's only if this line's own way into it and out of it is as straight and plain as
        // the bay needs; else its bus stops in the lane there.
        private static TransitStop Share(TransitLine line, int index, TransitStop host)
        {
            if (SharedBy.TryGetValue(host, out int lines) && lines >= MostLinesAt)
            {
                return null;   // as many lines as one place is timetabled for (TransitDirector.MostCalls)
            }
            int[] path = line.Path;
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
            var stop = new TransitStop
            {
                PathIndex = index, Lane = host.Lane, Along = host.Along, Kerb = host.Kerb, Sign = host.Sign, Wait = host.Wait, ShelterAt = host.ShelterAt,
                Shelter = host.Shelter, Parking = host.Parking, Metres = line.Metres[index], KerbAside = host.KerbAside, HasBay = host.HasBay, Bay = host.Bay,
                BayAlong = host.BayAlong, BayOffset = host.BayOffset, BayBehind = host.BayBehind, BayAhead = host.BayAhead, BayRoom = host.BayRoom, NoBay = host.NoBay,
                LanePoint = host.LanePoint, Host = host, Stand = host.Stand, KerbThings = host.KerbThings
            };
            if (stop.HasBay && BusBay.Lay(stop.KerbAside, out _, out BusBay.Curve curveIn, out BusBay.Curve curveOut, out _, out _))
            {
                float leaves = curveIn.Length + BusBay.Settle + BusBay.BusMiddleAhead + 1f;
                float rejoins = curveOut.Length - BusBay.BusMiddleAhead + 1f;
                string bends = LaneBends(stop, line, Vector3.Cross(Vector3.up, stop.Along), leaves, rejoins);
                if (bends != null)
                {
                    stop.HasBay = false;
                    stop.NoBay = "on the " + line.Number + "'s way into the bay " + bends;
                }
            }
            return stop;
        }

        // A stop at one lane point of a line, or null if that point will not do (or has no bay, when only one with a bay is wanted).
        private static TransitStop Try(TransitLine line, int index, bool bayOnly)
        {
            int[] path = line.Path;
            if (index < 3 || index >= path.Length - 3 || HostIndex.ContainsKey(path[index]))
            {
                return null;   // (a lane point another line has laid a place at is that place's: shared, never laid twice)
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
            FindBoarding(stop, right, edge);
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
            // The pole stands clear of the doors and the ways to them (it used to stand at the kerb beside the bus's middle,
            // on the way to the middle door and at the edge of its opening); then the bus stands where neither door lines up
            // with what stays on the kerb, and the boarding strip is looked at again for where its doors then are.
            if (!PlaceSign(stop, right, edge))
            {
                return null;
            }
            Stand(stop, right, edge);
            if (Mathf.Abs(stop.Stand) > 0.01f)
            {
                FindBoarding(stop, right, edge);
            }
            return stop;
        }

        // ---------------------------------------------------------------- the boarding strip

        private static readonly Collider[] BoardHits = new Collider[24];
        private const float BoardingPieceMost = 2.6f;   // metres across a piece of furniture may be, at the most, to be switched off

        // The street furniture standing on the boarding strip at a stop (BusInside.BoardingStrips): the kerb from beyond the
        // middle door to beyond the front door and a walkway 1.5 m wide from the waiting place to each door. Only plain
        // pieces of the game's street furniture (BusZones.PropOf), not too big; never a tree, a building or anything of the mod's
        // own. Three boxes, once per stop as the network is laid out.
        private static void FindBoarding(TransitStop stop, Vector3 right, float kerb)
        {
            stop.Boarding.Clear();
            stop.BoardingMade = false;
            if (_propsLayer < 0)
            {
                return;
            }
            Vector3 toWait = stop.Wait - stop.Lane;
            BusInside.Strip[] strips = BusInside.BoardingStrips(kerb, Vector3.Dot(Flat(toWait), stop.Along), Vector3.Dot(Flat(toWait), right), stop.Stand);
            for (int s = 0; s < strips.Length; s++)
            {
                BusInside.Strip strip = strips[s];
                Vector3 a = stop.Lane + stop.Along * strip.Along0 + right * strip.Aside0;
                Vector3 b = stop.Lane + stop.Along * strip.Along1 + right * strip.Aside1;
                Vector3 way = Flat(b - a);
                if (way.sqrMagnitude < 0.01f)
                {
                    continue;
                }
                Vector3 centre = (a + b) * 0.5f;
                centre.y = stop.Wait.y + 1.2f;
                int found = Physics.OverlapBoxNonAlloc(centre, new Vector3(strip.Half, 1.1f, strip.Length * 0.5f), BoardHits, Quaternion.LookRotation(way.normalized), 1 << _propsLayer,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < found; i++)
                {
                    Collider hit = BoardHits[i];
                    BoardHits[i] = null;
                    GameObject prop = hit != null ? BoardingPiece(hit) : null;
                    if (prop != null && !stop.Boarding.Contains(prop) && (stop.Zone == null || !stop.Zone.Clear.Contains(prop)))
                    {
                        stop.Boarding.Add(prop);
                    }
                }
            }
        }

        // A piece of the game's street furniture that may be switched off for the boarding strip, or null.
        private static GameObject BoardingPiece(Collider hit)
        {
            if (hit.attachedRigidbody != null || hit.gameObject.layer != _propsLayer || hit.GetComponentInParent<TreeController>() != null)
            {
                return null;   // a thing that moves, not street furniture, or a street tree
            }
            Transform root = hit.transform.root;
            if (root != null && root.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal))
            {
                return null;   // the mod's own: the shelter, the stop's sign
            }
            Vector3 size = hit.bounds.size;
            if (size.x > BoardingPieceMost || size.z > BoardingPieceMost)
            {
                return null;   // a kiosk, a wall: not a piece of furniture to take away
            }
            GameObject piece = BusZones.PropOf(hit);
            return piece != null && !BusZones.MustStay(piece) ? piece : null;
        }

        // ---------------------------------------------------------------- the pole, and where the bus stands

        // The pole at the first of BusInside.SignPlaces that is pavement with nothing standing there and is clear of the doors
        // and the ways to them however the bus stands (BusInside.SignClearAlways). False if none is (no stop here, then).
        private static bool PlaceSign(TransitStop stop, Vector3 right, float kerb)
        {
            Vector3 toWait = Flat(stop.Wait - stop.Lane);
            float waitAlong = Vector3.Dot(toWait, stop.Along), waitAside = Vector3.Dot(toWait, right);
            for (int i = 0; i < BusInside.SignPlaces.Length; i++)
            {
                if (i == BusInside.SignByShelter && !stop.Shelter)
                {
                    continue;
                }
                BusInside.Spot place = BusInside.SignPlaces[i];
                float aside = kerb + place.X;
                if (!BusInside.SignClearAlways(place.Z, aside, kerb, waitAlong, waitAside))
                {
                    continue;
                }
                if (IsPavement(stop.Lane + right * aside + stop.Along * place.Z, out Vector3 at) && Open(at, 0.3f))
                {
                    stop.Sign = at;
                    return true;
                }
            }
            return false;
        }

        private static readonly Collider[] KerbHits = new Collider[24];
        private static readonly float[] KerbAt = new float[26];
        private static readonly float[] KerbHalfs = new float[26];

        // Where the bus stands by the pole (BusInside.StandShift): what stays on the kerb by its doors is found once, in one box
        // along all the places the doors may come to, from the kerb's edge to beyond the doors' steps: a street tree (by its trunk),
        // anything else that is not switched off for the boarding strip, and the pole. In a bay it may only stand a little on.
        private static void Stand(TransitStop stop, Vector3 right, float kerb)
        {
            stop.Stand = 0f;
            stop.KerbThings = 0;
            int count = 0;
            float from = BusInside.MiddleDoor - BusInside.DoorHalf - 2.5f;
            float to = BusInside.FrontDoor + BusInside.DoorHalf + 2.5f;
            float inner = kerb - 0.3f;
            float outer = kerb + BusInside.DoorStep;
            Vector3 centre = stop.Lane + stop.Along * ((from + to) * 0.5f) + right * ((inner + outer) * 0.5f);
            centre.y = stop.Wait.y + 1.2f;
            int found = _propsLayer < 0 ? 0 : Physics.OverlapBoxNonAlloc(centre, new Vector3((outer - inner) * 0.5f, 1.0f, (to - from) * 0.5f), KerbHits, Quaternion.LookRotation(stop.Along),
                1 << _propsLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found; i++)
            {
                Collider hit = KerbHits[i];
                KerbHits[i] = null;
                if (hit == null || hit.attachedRigidbody != null || count >= KerbAt.Length - 1)
                {
                    continue;
                }
                Transform root = hit.transform.root;
                if (root != null && root.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal))
                {
                    continue;   // the mod's own: the pole is counted below
                }
                GameObject piece = BoardingPiece(hit);
                if (piece != null && stop.Boarding.Contains(piece))
                {
                    continue;   // switched off while the buses run
                }
                Bounds bounds = hit.bounds;
                bool tree = hit.GetComponentInParent<TreeController>() != null;
                KerbAt[count] = Vector3.Dot(Flat(bounds.center - stop.Lane), stop.Along);
                KerbHalfs[count] = tree ? TrunkHalf : Mathf.Abs(bounds.extents.x * stop.Along.x) + Mathf.Abs(bounds.extents.z * stop.Along.z);
                count++;
            }
            Vector3 pole = Flat(stop.Sign - stop.Lane);
            if (Vector3.Dot(pole, right) <= outer + BusInside.PoleReach)
            {
                KerbAt[count] = Vector3.Dot(pole, stop.Along);
                KerbHalfs[count] = BusInside.PoleReach;
                count++;
            }
            stop.KerbThings = count;
            stop.Stand = stop.HasBay ? BusInside.StandShift(KerbAt, KerbHalfs, count, 0f, BusInside.BayStandMost)
                                     : BusInside.StandShift(KerbAt, KerbHalfs, count, -1f, 1f);
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
            float kerb = KerbAside(stop, right);
            stop.KerbAside = KerbLine(stop, right, kerb);
            if (!BusBay.Lay(stop.KerbAside, out float offset, out BusBay.Curve curveIn, out BusBay.Curve curveOut, out float behind, out float ahead))
            {
                stop.NoBay = offset > 0f ? "too wide to cross in the plain lane there is"
                    : stop.KerbAside < kerb - 0.01f ? "narrow with " + (_edgeWhat ?? "what stands") + " on the kerb's edge (the kerb " + kerb.ToString("F2") + " m out, the bus may come to " + stop.KerbAside.ToString("F2") + ")"
                    : "narrow";
                return;
            }
            ahead += BayAheadSlack;
            // The lane straight and plain from where the bus's front axle leaves it to where it is back on it.
            float leaves = curveIn.Length + BusBay.Settle + BusBay.BusMiddleAhead + 1f;
            float rejoins = curveOut.Length - BusBay.BusMiddleAhead + 1f;
            Zoning.Reset();
            stop.NoBay = LaneBends(stop, line, right, leaves, rejoins) ?? StripBad(stop, right, offset, -behind, ahead, Zoning)
                         ?? BusZones.Plan(stop, right, -behind, ahead, Zoning);
            if (stop.NoBay != null)
            {
                return;
            }
            Vector3 bay = lane + right * offset;
            CitySpots.Ground(bay, out float ground);
            bay.y = ground;
            FindMeters(stop, right, -behind, ahead, Zoning);
            stop.HasBay = true;
            stop.Bay = bay;
            stop.BayOffset = offset;
            stop.BayBehind = behind;
            stop.BayAhead = ahead;
            stop.BayRoom = behind;   // (one bus, the mod's own: what it needs is what is laid out)
            stop.Zone = Zoning.Copy(-behind, ahead, stop.KerbAside);
            stop.Zone.Lanes = Zoning.Lanes;
            stop.Zone.Cut = Zoning.Cut;
            stop.Zone.Lost = Zoning.Lost;
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
        // up to where the bus's door side will be, the kerb keeping its line, and nothing fixed standing in it that the
        // zone cannot take away. Null if so, else why not; what the zone is to switch off is put in it. Two rays and a
        // sample every four metres or so, and one box.
        private static readonly Collider[] StripHits = new Collider[24];

        private static string StripBad(TransitStop stop, Vector3 right, float offset, float from, float to, BusZone zone)
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
            // Up to where the bus's side will be (BusBay.KerbGap from the kerb), not to the kerb itself: a lamp post or a
            // sign on the kerb's edge is not in the bus's way. What is found is named, for the log.
            float width = stop.KerbAside - BusBay.KerbGap - near;
            Vector3 centre = lane + along * ((from + to) * 0.5f) + right * (near + width * 0.5f);
            int found = Physics.OverlapBoxNonAlloc(centre + Vector3.up * 1.15f, new Vector3(width * 0.5f, 0.8f, (to - from) * 0.5f), StripHits, Quaternion.LookRotation(along), _staticMask, QueryTriggerInteraction.Ignore);
            string bad = found >= StripHits.Length ? "more stands in the strip than can be counted" : null;
            for (int i = 0; i < found; i++)
            {
                Collider hit = StripHits[i];
                StripHits[i] = null;
                if (bad == null && hit != null)
                {
                    bad = Standing(stop, right, hit, zone);
                }
            }
            return bad;
        }

        // The game's parking meters along a bay's kerb, on the pavement within a step of its edge: with the parking gone from the
        // bay they stand for nothing, and are switched off with the zone (BusZones) as plain furniture is. One box.
        private static void FindMeters(TransitStop stop, Vector3 right, float from, float to, BusZone zone)
        {
            if (_propsLayer < 0)
            {
                return;
            }
            float inner = stop.KerbAside - 0.3f;
            float outer = stop.KerbAside + 1.2f;
            Vector3 centre = stop.Lane + stop.Along * ((from + to) * 0.5f) + right * ((inner + outer) * 0.5f) + Vector3.up * 1.1f;
            int found = Physics.OverlapBoxNonAlloc(centre, new Vector3((outer - inner) * 0.5f, 1.0f, (to - from) * 0.5f), StripHits, Quaternion.LookRotation(stop.Along),
                1 << _propsLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found; i++)
            {
                Collider hit = StripHits[i];
                StripHits[i] = null;
                if (hit == null || !IsMeter(hit.transform))
                {
                    continue;
                }
                GameObject piece = BoardingPiece(hit);
                if (piece != null && !zone.Meters.Contains(piece) && !zone.Clear.Contains(piece))
                {
                    zone.Meters.Add(piece);
                }
            }
        }

        // The game's parking meter (Prefab_ParkingMeter, its models SM_ParkingMeter_LOD*): by its name or a parent's.
        private static bool IsMeter(Transform part)
        {
            for (int up = 0; up < 4 && part != null; up++, part = part.parent)
            {
                if (part.name.IndexOf("ParkingMeter", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Something fixed found in a bay's strip. Null if the zone deals with it: street furniture standing in the road is
        // switched off with the zone; a street tree on the pavement only seems to reach out (its collider is a capsule
        // twice as broad as its trunk) and is left. Else why not, naming it.
        private static string Standing(TransitStop stop, Vector3 right, Collider hit, BusZone zone)
        {
            if (hit.attachedRigidbody != null)
            {
                return null;   // a thing that moves: looked at when the bay is used (TransitBays.Free)
            }
            string what = "'" + hit.name + "' on " + LayerMask.LayerToName(hit.gameObject.layer);
            if (hit.gameObject.layer != _propsLayer)
            {
                return "a building stands in the strip: " + what;
            }
            float middle = Vector3.Dot(Flat(hit.bounds.center - stop.Lane), right);
            if (middle >= stop.KerbAside - 0.05f)
            {
                if (hit.GetComponentInParent<TreeController>() == null)
                {
                    return "something on the kerb's edge reaches into the strip: " + what;
                }
                if (!zone.Known.Contains(hit))
                {
                    zone.Known.Add(hit);
                }
                return null;
            }
            GameObject prop = BusZones.PropOf(hit);
            if (prop == null)
            {
                return "something stands in the strip that is not plain street furniture: " + what;
            }
            if (!zone.Clear.Contains(prop))
            {
                zone.Clear.Add(prop);
            }
            return null;
        }

        // How near the lane's line a bus may come at the kerb: the kerb's edge, or nearer where street furniture on the
        // pavement reaches out over it (a sign or a bin on the very edge); the bus's side then keeps SideClear from it.
        // Looked at along all the length a bay may take up, in one box. A street tree is taken at its trunk. Not the
        // buildings: a building's box is no guide to its walls where the streets do not run square to the world, and a
        // wall that does reach into the strip is found there (StripBad).
        private const float SideClear = 0.10f;
        private const float TrunkHalf = 0.15f;   // a street tree's trunk, halved: some 0.3 m across, in a collider four times as broad
        private static string _edgeWhat;         // what brought the kerb's line in, for the log
        private static readonly Collider[] EdgeHits = new Collider[24];

        private static float KerbLine(TransitStop stop, Vector3 right, float kerb)
        {
            float inner = kerb - 0.6f;
            float outer = kerb + 0.4f;
            Vector3 centre = stop.Lane + stop.Along * ((KerbLookAhead - KerbLookBehind) * 0.5f) + right * ((inner + outer) * 0.5f) + Vector3.up * 1.15f;
            int found = _propsLayer < 0 ? 0 : Physics.OverlapBoxNonAlloc(centre, new Vector3((outer - inner) * 0.5f, 0.8f, (KerbLookAhead + KerbLookBehind) * 0.5f), EdgeHits, Quaternion.LookRotation(stop.Along),
                1 << _propsLayer, QueryTriggerInteraction.Ignore);
            float line = kerb;
            _edgeWhat = null;
            for (int i = 0; i < found; i++)
            {
                Collider hit = EdgeHits[i];
                EdgeHits[i] = null;
                if (hit == null || hit.attachedRigidbody != null)
                {
                    continue;
                }
                float middle = Vector3.Dot(Flat(hit.bounds.center - stop.Lane), right);
                if (middle < kerb - 0.05f)
                {
                    continue;   // it stands in the road: the strip's own look deals with it (StripBad)
                }
                float reach = Reach(hit, stop.Lane, right, middle) + BusBay.KerbGap - SideClear;
                if (reach < line)
                {
                    line = reach;
                    _edgeWhat = "'" + hit.name + "'";
                }
            }
            return line;
        }

        // The nearest a thing on the pavement comes to the lane's line, in metres aside of it.
        private static float Reach(Collider hit, Vector3 lane, Vector3 right, float middle)
        {
            if (hit.GetComponentInParent<TreeController>() != null)
            {
                return middle - TrunkHalf;
            }
            if (hit is BoxCollider || hit is SphereCollider || hit is CapsuleCollider || (hit is MeshCollider mesh && mesh.convex))
            {
                return Vector3.Dot(Flat(hit.ClosestPoint(hit.bounds.center - right * 8f) - lane), right);
            }
            Bounds box = hit.bounds;   // (a mesh that is not convex: its box, which is never smaller than it)
            float least = float.MaxValue;
            for (int corner = 0; corner < 4; corner++)
            {
                var at = new Vector3((corner & 1) == 0 ? box.min.x : box.max.x, box.center.y, (corner & 2) == 0 ? box.min.z : box.max.z);
                least = Mathf.Min(least, Vector3.Dot(Flat(at - lane), right));
            }
            return least;
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
            float best = 150f * 150f;
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

        /// <summary>The place nearest a place, within a distance; null if there is none. A pass over the places: for a timer, not a frame.</summary>
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

        /// <summary>How many stops lie after this one on its line.</summary>
        public static int Onward(TransitStop stop)
        {
            return stop == null ? 0 : Lines[stop.Line].Stops.Count - 1 - stop.Order;
        }

        /// <summary>The numbers of the lines that stop at a place, in the order they were laid ("M2 M5"); the same number once.</summary>
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

        /// <summary>A line's stop at a place, or null if the line does not call there.</summary>
        public static TransitStop CallOf(TransitStop place, int line)
        {
            List<TransitStop> calls = place != null ? place.At.Calls : null;
            for (int i = 0; calls != null && i < calls.Count; i++)
            {
                if (calls[i].Line == line)
                {
                    return calls[i];
                }
            }
            return null;
        }
    }
}
