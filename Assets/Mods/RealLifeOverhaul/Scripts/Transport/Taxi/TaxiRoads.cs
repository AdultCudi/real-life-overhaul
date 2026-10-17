using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using GleyTrafficSystem;
using GleyUrbanAssets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the taxi feature asks of the rest of the mod, each with an answer of its own when nobody has filled
    /// it in (Wiring/TaxiLinks.cs does, once per city load).
    /// </summary>
    public static class TaxiBridges
    {
        /// <summary>True while a scene of the mod's holds the player and it is not their doing (an arrest, custody, a test). Default: never.</summary>
        public static Func<bool> Held;

        /// <summary>True while the player is being carried by something of the mod's they chose (a bus ride). Default: never.</summary>
        public static Func<bool> Riding;

        /// <summary>True in rain or a storm of the mod's weather. Default: never.</summary>
        public static Func<bool> Wet;

        /// <summary>The road graph somebody else has already made for this city, to search over without making it again; null if nobody has. Default: nobody.</summary>
        public static Func<RouteSearch> Roads;
    }

    /// <summary>A way for a taxi to the caller: the lane point it starts on, the places it drives through and where it stops.</summary>
    public sealed class TaxiPlan
    {
        public readonly List<Vector3> Route = new List<Vector3>(160);   // every place after the start, the stopping place last
        public readonly List<int> Points = new List<int>(160);          // the lane point each is, -1 for a place of the taxi's own
        public int Start = -1;           // the lane point it is put on
        public Vector3 StartAt;
        public Vector3 Stop;             // the lane point it stops at
        public Vector3 Along;            // the way the lane runs there
        public Vector3 Caller;           // where the caller stood when it was planned
        public float Metres;             // of road, from the start to the stop
        public float FromCaller;         // metres between the stop and the caller
        public float StartAway;          // metres between the start and the caller, as the crow flies
        public int Starts, Goals, Steps;
        public double Milliseconds;
        public float At;                 // Time.time it was planned

        public bool Valid => Start >= 0 && Route.Count > 1;

        public void Clear()
        {
            Route.Clear();
            Points.Clear();
            Start = -1;
            Metres = 0f;
            Starts = Goals = Steps = 0;
            Milliseconds = 0.0;
        }
    }

    /// <summary>
    /// The roads as a called taxi needs them: which lane is the caller's, where out of view a taxi can be put,
    /// and the way between the two. It searches the mod's shared road graph (Core/RoadGraph, Core/RouteSearch):
    /// the one the GPS has made if there is one (the places and edges are shared, only this search's own four
    /// working arrays are new), else one made here the first time somebody calls, once per city.
    ///
    /// One search answers both questions at once: it starts from every place a taxi may be put (plain lane points
    /// in a band of distances round the caller, out of view) and ends at the caller's lane, so what comes back is
    /// the nearest of those places by road and the way from it. The cost is one pass over the lane points and one
    /// search, when a call is made and never otherwise; nothing here runs on a timer.
    /// </summary>
    public sealed class TaxiRoads
    {
        private const float LaneChangeCost = 30f;    // as the bus network and the GPS have it
        private const int MostStarts = 4096;         // one lane point in four of the band: more than any band holds
        private const int MostGoals = 12;
        private const float GoalSpread = 5f;         // lane points this much further from the caller than the nearest also end a route
        private const int SearchLimit = 60000;
        private const float NeverNearer = 60f;       // nothing is put on the road nearer the caller than this, in view or not
        private const float SeenWithin = 115f;       // metres from the camera beyond which nothing counts as in view (CitySpots.InView looks to 110)
        private const float FinalShort = 1.2f;       // the driver stops this far short of the last place of a route (EmergencyDriver.FinalReach)

        private readonly List<int> _starts = new List<int>(1024);
        private readonly List<int> _goals = new List<int>(MostGoals);
        private readonly List<int> _path = new List<int>(256);
        private readonly HashSet<int> _taken = new HashSet<int>();
        private Waypoint[] _points;
        private RouteSearch _search;
        private bool _shared;
        private float _goalReach;
        private Vector3 _eye;
        private bool _hasEye;

        /// <summary>The lane point with this number, or null.</summary>
        public Waypoint Point(int index)
        {
            return _points != null && index >= 0 && index < _points.Length ? _points[index] : null;
        }

        /// <summary>The lane points, for whoever follows a route over them.</summary>
        public Waypoint[] Points => _points;

        /// <summary>Forgets the lane points a taxi could not be put on (traffic stood there): a new call starts with none.</summary>
        public void NewCall()
        {
            _taken.Clear();
        }

        /// <summary>A lane point traffic stood on when a taxi was to be put there: the next plan leaves it out.</summary>
        public void Refuse(int point)
        {
            _taken.Add(point);
        }

        public void Dispose()
        {
            _points = null;
            _search = null;
            _taken.Clear();
        }

        // The graph, the first time it is wanted in this city.
        private bool Ready()
        {
            if (_search != null && _points != null)
            {
                return true;
            }
            CurrentSceneData data = RoadData.Scene;
            if (data == null || data.allWaypoints == null || data.allWaypoints.Length < 100)
            {
                return false;
            }
            _points = data.allWaypoints;
            Stopwatch watch = Stopwatch.StartNew();
            RouteSearch made = TaxiBridges.Roads != null ? TaxiBridges.Roads() : null;
            _shared = made != null && made.Count == _points.Length && made.EdgeStart != null;
            if (_shared)
            {
                _search = new RouteSearch(made);
                _search.SetEdges(made.EdgeStart, made.EdgeTo, made.EdgeExtra);
            }
            else
            {
                using var piece = SessionWatch.Piece("the taxi's road graph");
                _search = RoadGraph.Build(_points, LaneChangeCost, out _);
            }
            if (Meta.Dev.Verbose)
            {
                TaxiDirector.Logger?.Info("Taxi: road graph " + (_shared ? "shared with the GPS" : "made") + ": " + _points.Length + " lane points, "
                                          + watch.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms.");
            }
            return true;
        }

        /// <summary>
        /// Plans a taxi's way to the caller. False with why: no lane a car can use within reach of them, or no way
        /// to it from any place a taxi may start.
        /// </summary>
        /// <param name="type">The traffic system's kind of the vehicle that will come: only lanes it may use.</param>
        /// <param name="least">The nearest to the caller a taxi may start, in metres as the crow flies.</param>
        /// <param name="most">...and the furthest.</param>
        /// <param name="mayBeSeen">A start in view is allowed (the test that brings one at once), though never close.</param>
        public bool Find(Vector3 caller, VehicleTypes type, float least, float most, bool mayBeSeen, TaxiPlan plan, out TaxiRules.Refusal refusal)
        {
            plan.Clear();
            refusal = TaxiRules.Refusal.NoRoad;
            if (!Ready())
            {
                return false;
            }
            using var piece = SessionWatch.Piece("a taxi's route");
            Stopwatch watch = Stopwatch.StartNew();
            Collect(caller, type, Mathf.Max(least, NeverNearer), most, mayBeSeen);
            plan.Starts = _starts.Count;
            plan.Goals = _goals.Count;
            if (_goals.Count == 0)
            {
                return false;
            }
            refusal = TaxiRules.Refusal.NoTaxi;
            if (_starts.Count == 0)
            {
                return false;
            }
            Waypoint end = _points[_goals[0]];
            bool found = _search.Find(_starts, _goals, caller.x, end.position.y, caller.z, _goalReach, SearchLimit, _path);
            plan.Steps = _search.Expanded;
            plan.Milliseconds = watch.Elapsed.TotalMilliseconds;
            if (!found || _path.Count < 3)
            {
                return false;
            }
            float metres = 0f;
            for (int i = 1; i < _path.Count; i++)
            {
                metres += Vector3.Distance(_points[_path[i]].position, _points[_path[i - 1]].position);
            }
            if (!TaxiRules.RouteAccepted(metres, most))
            {
                return false;
            }
            Fill(plan, caller, metres);
            refusal = TaxiRules.Refusal.None;
            return true;
        }

        // One pass over the lane points: the caller's lane (the nearest plain lane points on their level) and the places
        // a taxi may start from.
        private void Collect(Vector3 caller, VehicleTypes type, float least, float most, bool mayBeSeen)
        {
            _starts.Clear();
            _goals.Clear();
            float within = TaxiRules.RoadWithin * TaxiRules.RoadWithin;
            float leastSqr = least * least;
            float mostSqr = most * most;
            float nearest = float.MaxValue;
            Camera camera = GameManager.GetMainCamera();
            _hasEye = camera != null;
            _eye = _hasEye ? camera.transform.position : caller;
            for (int pass = 0; pass < 2; pass++)
            {
                // The first pass finds how near the caller's lane is; the second takes its points and the starts.
                float reach = pass == 0 ? 0f : Mathf.Sqrt(nearest) + GoalSpread;
                for (int i = 0; i < _points.Length; i++)
                {
                    Waypoint point = _points[i];
                    if (point == null)
                    {
                        continue;
                    }
                    float dx = point.position.x - caller.x;
                    float dz = point.position.z - caller.z;
                    float sqr = dx * dx + dz * dz;
                    if (sqr < within)
                    {
                        if (Mathf.Abs(point.position.y - caller.y) > TaxiRules.RoadLevel || !Usable(point, type))
                        {
                            continue;
                        }
                        if (pass == 0)
                        {
                            nearest = Mathf.Min(nearest, sqr);
                        }
                        else if (sqr <= reach * reach && _goals.Count < MostGoals)
                        {
                            _goals.Add(i);
                        }
                    }
                    else if (pass == 1 && sqr >= leastSqr && sqr <= mostSqr && (i & 3) == 0 && _starts.Count < MostStarts
                             && !_taken.Contains(i) && Usable(point, type) && !point.enter && !point.exit && (mayBeSeen || Unseen(point.position)))
                    {
                        _starts.Add(i);
                    }
                }
                if (nearest == float.MaxValue)
                {
                    return;   // no lane within reach: nothing to start for
                }
                // (No end of the route is further from the caller than this: across, and the two levels a lane may differ by.)
                _goalReach = Mathf.Sqrt(nearest) + GoalSpread + TaxiRules.RoadLevel * 2f + 1f;
            }
        }

        // A plain lane point of a road proper that this kind of vehicle may use, open, with road ahead of it.
        private static bool Usable(Waypoint point, VehicleTypes type)
        {
            return !point.temporaryDisabled && point.neighbors != null && point.neighbors.Count > 0
                   && point.allowedCars != null && point.allowedCars.Contains(type) && RoadGraph.Drivable(point) && RoadGraph.Plain(point);
        }

        // Nothing appears where it would be seen to: not where the camera looks, by both of the mod's tests of that.
        // (Further from the camera than either test looks, neither is asked: a band's places mostly are.)
        private bool Unseen(Vector3 place)
        {
            return (_hasEye && (place - _eye).sqrMagnitude > SeenWithin * SeenWithin) || (!CitySpots.InView(place) && !StreetSpots.IsInView(place));
        }

        private void Fill(TaxiPlan plan, Vector3 caller, float metres)
        {
            Waypoint start = _points[_path[0]];
            Waypoint last = _points[_path[_path.Count - 1]];
            for (int i = 1; i < _path.Count - 1; i++)
            {
                plan.Route.Add(_points[_path[i]].position);
                plan.Points.Add(_path[i]);
            }
            Vector3 along = last.position - _points[_path[_path.Count - 2]].position;
            along.y = 0f;
            along = along.sqrMagnitude > 0.01f ? along.normalized : Vector3.forward;
            // The driver stops short of the last place of a route by FinalShort: the last place is that far beyond the
            // lane point, so the taxi comes to rest on it.
            plan.Route.Add(last.position + along * FinalShort);
            plan.Points.Add(-1);
            plan.Start = _path[0];
            plan.StartAt = start.position;
            plan.Stop = last.position;
            plan.Along = along;
            plan.Caller = caller;
            plan.Metres = metres;
            plan.FromCaller = Mathf.Sqrt(CitySpots.Flat(last.position - caller));
            plan.StartAway = Mathf.Sqrt(CitySpots.Flat(start.position - caller));
            plan.At = Time.time;
        }
    }
}
