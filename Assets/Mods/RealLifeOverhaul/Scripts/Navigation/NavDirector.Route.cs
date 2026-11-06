using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Helpers;
using Streets;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>The director's part for the GPS: the destination, the search, following the route and its two lines.</summary>
    public sealed partial class NavDirector
    {
        private const double SliceMilliseconds = 0.4;   // of one frame, for a search under way
        private const int SliceSteps = 64;              // the clock is looked at after this many steps
        private const float StartReachOnFoot = 30f;     // metres from the player of the lane points that may begin the route
        private const float StartReachDriving = 14f;    // metres
        private const float GoalReach = 30f;            // metres from the destination of those that may end it
        private const float FarReach = 400f;            // metres up to which the single nearest lane point is taken
        private const float Level = 9f;                 // metres up or down that are still the same road level
        private const float DrivingPace = 0.75f;        // the share of a lane's speed a drive makes good, lights and corners included
        private const float RedrawEvery = 25f;          // metres driven before the line is cut back to the player again
        private const float SameGoal = 5f;              // metres a destination may move and be the same one
        private const float RearmBeyond = 150f;         // metres away from where, after arriving, a new route to it is made
        private const float RetryAfter = 10f;           // seconds before a search that found no way is tried again

        private enum Phase { Idle, Searching, Routed, NoRoute }

        private readonly NavRoute _route = new NavRoute();
        private readonly NavTracker _tracker = new NavTracker();
        private readonly List<int> _path = new List<int>(2048);
        private readonly List<int> _starts = new List<int>(64);
        private readonly List<int> _goals = new List<int>(64);

        private DirectionGuider _guider;
        private Vector3 _goal;
        private string _goalName;
        private bool _hasGoal;
        private bool _arrived;

        private Phase _phase;
        private RouteSearch _searching;
        private bool _searchOnFoot, _searchAgainstTraffic, _routeOnFoot, _routeAgainstTraffic;
        private int _slices;
        private float _searchReach;
        private double _searchMilliseconds, _longestSlice;
        private string _searchWhy;
        private float _retryAt;
        private int _segment;
        private float _along, _lateral, _drawnFrom;
        private float[] _lineX, _lineY, _lineZ;
        private int _lineCount;
        private int _turnShown = -1, _metresShown = -1, _leftShown = -1;
        private bool _nowShown;
        private string _line1, _line2;

        // One slice of the search: steps until it is decided or the slice's time is up.
        private void Search()
        {
            long began = Stopwatch.GetTimestamp();
            RouteSearch.Outcome outcome;
            double took;
            do
            {
                outcome = _searching.Advance(_searching.Expanded + SliceSteps, _path);
                took = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            }
            while (outcome == RouteSearch.Outcome.Searching && took < SliceMilliseconds);
            _slices++;
            _searchMilliseconds += took;
            _longestSlice = Math.Max(_longestSlice, took);
            if (outcome == RouteSearch.Outcome.Searching)
            {
                return;
            }
            if (outcome == RouteSearch.Outcome.Found)
            {
                Routed();
                return;
            }
            if (!_searchOnFoot && !_searchAgainstTraffic && _graph.Walk.Begin(_starts, _goals, _goal.x, _graph.Drive.Y[_goals[0]], _goal.z, _searchReach))
            {
                // No way with the traffic: the way along the same streets without the one-way rule is shown instead.
                _searchAgainstTraffic = true;
                _searching = _graph.Walk;
                return;
            }
            _phase = Phase.NoRoute;
            _retryAt = Time.unscaledTime + RetryAfter;
            _route.Clear();
            Draw();
            _line1 = NavText.T("reallife_nav_no_route");
            _leftShown = -1;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Navigation: no way by road to '" + _goalName + "' (" + (_searchOnFoot ? "on foot" : "driving") + "): " + _starts.Count + " lane points to start from, "
                             + _goals.Count + " to end at, " + _searching.Expanded + " search steps in " + _slices + " slices, " + _searchMilliseconds.ToString("F2", CultureInfo.InvariantCulture)
                             + " ms. The marker's direction and distance are shown; tried again in " + RetryAfter.ToString("F0") + " s.");
            }
        }


        // The game's destination marker if it is set, else a job's.
        private static DirectionGuider Wanted()
        {
            GuidersManager guiders = InstanceBehavior<GuidersManager>.Instance;
            if (guiders == null)
            {
                return null;
            }
            if (guiders.destinationGuider != null && guiders.destinationGuider.target != null)
            {
                return guiders.destinationGuider;
            }
            return guiders.jobDestinationGuider != null && guiders.jobDestinationGuider.target != null ? guiders.jobDestinationGuider : null;
        }

        private void Guide(float now, bool outdoors)
        {
            DirectionGuider wanted = Wanted();
            if (wanted == null)
            {
                if (_hasGoal)
                {
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Navigation: the destination '" + _goalName + "' was taken away; the route is cleared.");
                    }
                    Forget();
                }
                return;
            }
            Vector3 at = wanted.target.position;
            if (!_hasGoal || wanted != _guider || (at - _goal).sqrMagnitude > SameGoal * SameGoal)
            {
                NewGoal(wanted, at);
            }
            if (!outdoors)
            {
                // Indoors nothing is followed, but the open city map still wants a route, on foot from the building's door.
                if (CityMap.IsOpen && _phase == Phase.Idle && !_arrived)
                {
                    Begin(true, "looked at on the city map from indoors");
                }
                return;
            }
            float gx = _goal.x - _px;
            float gz = _goal.z - _pz;
            float toPlace = Mathf.Sqrt(gx * gx + gz * gz);
            if (_arrived)
            {
                if (toPlace < RearmBeyond)
                {
                    return;
                }
                _arrived = false;   // gone well away since arriving with the marker still there: guided back to it
                _phase = Phase.Idle;
                GoalOnHud();
            }
            bool onFoot = !_driving;
            if (toPlace <= (onFoot ? NavTracker.ArriveOnFoot : NavTracker.ArriveDriving))
            {
                Arrive(toPlace);
                return;
            }
            switch (_phase)
            {
                case Phase.Idle:
                    Begin(onFoot, "a new destination");
                    break;
                case Phase.NoRoute:
                    Straight(toPlace);
                    if (now >= _retryAt)
                    {
                        Begin(onFoot, "trying again");
                    }
                    break;
                case Phase.Searching:
                    if (onFoot != _searchOnFoot)
                    {
                        Begin(onFoot, onFoot ? "on foot now" : "driving now");
                    }
                    break;
                case Phase.Routed:
                    if (onFoot != _routeOnFoot)
                    {
                        Begin(onFoot, onFoot ? "on foot now" : "driving now");
                        break;
                    }
                    Follow(onFoot, toPlace);
                    break;
            }
        }

        private void NewGoal(DirectionGuider guider, Vector3 at)
        {
            _guider = guider;
            _goal = at;
            _goalName = NameOf(guider);
            _hasGoal = true;
            _arrived = false;
            _phase = Phase.Idle;
            _route.Clear();
            _line1 = null;
            _line2 = null;
            _nextMarks = 0f;   // the marker that was the destination's is another place's now
            GoalOnHud();
            Draw();
        }

        private void GoalOnHud()
        {
            if (_hud == null)
            {
                return;
            }
            NavMarks.Prepare();
            Color32 colour = _guider.poiBackgroundColor;
            Sprite icon = _guider.poiICon;
            if (NavMarks.Read(_guider.poi, out Sprite shown, out Color32 shownColour))
            {
                colour = shownColour;
                icon = shown != null ? shown : icon;
            }
            _hud.Goal(true, _goal.x, _goal.z, icon, colour);
            _hud.Title(_goalName);
        }

        // What the game calls the place: the business or the address for a building, the marker's own words otherwise.
        private static string NameOf(DirectionGuider guider)
        {
            try
            {
                Address address = guider.CurrentAddress;
                if (!address.IsUndefined())
                {
                    string special = SpecialBuildings.NameFor(address);
                    if (special != null)
                    {
                        return special;
                    }
                    string business = BuildingHelper.GetBuildingRegistration(address).BusinessName;
                    return string.IsNullOrEmpty(business) ? address.ToFormattedString() : business;
                }
                string words = NavMarks.Words(guider.poi);
                if (!string.IsNullOrEmpty(words))
                {
                    return words;
                }
            }
            catch (Exception)
            {
                // a marker with nothing readable on it: the game's own word for such a place
            }
            return NavText.T("poi_custom_location");
        }

        private void Forget()
        {
            _hasGoal = false;
            _guider = null;
            _arrived = false;
            _phase = Phase.Idle;
            _route.Clear();
            _line1 = null;
            _line2 = null;
            _lineCount = 0;
            _nextMarks = 0f;
            if (_hud != null)
            {
                _hud.Goal(false, 0f, 0f, null, default);
                _hud.Title(null);
                _hud.Route(0);
                _hud.Lines(null, null);
            }
            if (_map != null)
            {
                _map.SetRoute(null, null, null, 0);
            }
        }

        private void Arrive(float toPlace)
        {
            GuidersManager guiders = InstanceBehavior<GuidersManager>.Instance;
            bool own = guiders != null && _guider != null && ReferenceEquals(_guider, guiders.destinationGuider);
            _arrived = true;
            _phase = Phase.Idle;
            _route.Clear();
            Draw();
            _line1 = null;
            _line2 = null;
            if (_hud != null)
            {
                _hud.Title(null);
            }
            if (_voogle)
            {
                own = false;   // that mod says "arrived" and ends the destination by its own rule
            }
            else
            {
                Notifications.Show(NotificationType.Success, "reallife_nav_arrived", new Dictionary<string, string>(1) { { "place", _goalName ?? string.Empty } },
                                   6f, "reallife_nav_arrived", null, true, false);
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Navigation: arrived at '" + _goalName + "' (" + toPlace.ToString("F0") + " m from it, " + (_driving ? "driving" : "on foot")
                             + "). The route is cleared" + (own ? " and the destination's marker with it."
                                 : _voogle ? "; the notice and the marker are the other mod's." : "; a job's marker is the job's to take away."));
            }
            if (own)
            {
                GuidersManager.ResetGuider(DirectionGuiderType.Destination);   // the player's own destination: its marker goes
            }
        }


        private void Begin(bool onFoot, string why)
        {
            NavIndex places = _graph.Places;
            RouteSearch drive = _graph.Drive;
            _starts.Clear();
            _goals.Clear();
            places.Within(_px, _pz, onFoot ? StartReachOnFoot : StartReachDriving, _starts);
            // At a wheel, only lanes running the way the car points, so the route does not begin on the lane going the other way.
            float fx = 0f, fz = 0f;
            if (!onFoot && _follow != null)
            {
                Vector3 forward = _follow.forward;
                fx = forward.x;
                fz = forward.z;
            }
            int kept = 0;
            for (int i = 0; i < _starts.Count; i++)
            {
                int point = _starts[i];
                if (Mathf.Abs(drive.Y[point] - _py) > Level)
                {
                    continue;
                }
                if (!onFoot)
                {
                    int edge = drive.EdgeStart[point];
                    if (edge < drive.EdgeStart[point + 1])
                    {
                        int next = drive.EdgeTo[edge];
                        if ((drive.X[next] - drive.X[point]) * fx + (drive.Z[next] - drive.Z[point]) * fz < 0f)
                        {
                            continue;
                        }
                    }
                }
                _starts[kept++] = point;
            }
            _starts.RemoveRange(kept, _starts.Count - kept);
            if (_starts.Count == 0)
            {
                int nearest = places.Nearest(_px, _pz, FarReach);
                if (nearest >= 0)
                {
                    _starts.Add(nearest);
                }
            }
            places.Within(_goal.x, _goal.z, GoalReach, _goals);
            float reach = GoalReach;
            if (_goals.Count == 0)
            {
                int nearest = places.Nearest(_goal.x, _goal.z, FarReach);
                if (nearest >= 0)
                {
                    _goals.Add(nearest);
                    float dx = drive.X[nearest] - _goal.x;
                    float dz = drive.Z[nearest] - _goal.z;
                    reach = Mathf.Sqrt(dx * dx + dz * dz) + 1f;
                }
            }
            _searchOnFoot = onFoot;
            _searchAgainstTraffic = false;
            _searchReach = reach;
            _searchWhy = why;
            _slices = 0;
            _searchMilliseconds = 0.0;
            _longestSlice = 0.0;
            _searching = onFoot ? _graph.Walk : drive;
            bool wasRouted = _phase == Phase.Routed;
            if (_starts.Count == 0 || _goals.Count == 0 || !_searching.Begin(_starts, _goals, _goal.x, drive.Y[_goals[0]], _goal.z, reach))
            {
                _phase = Phase.NoRoute;
                _retryAt = Time.unscaledTime + RetryAfter;
                _route.Clear();
                Draw();
                _line1 = NavText.T("reallife_nav_no_route");
                _leftShown = -1;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Navigation: no road within " + FarReach.ToString("F0") + " m of " + (_starts.Count == 0 ? "the player" : "'" + _goalName + "'")
                                 + ", so there is no route; the marker's direction and distance are shown.");
                }
                return;
            }
            _phase = Phase.Searching;
            if (!wasRouted)
            {
                _line1 = NavText.T("reallife_nav_searching");
                _line2 = null;
            }
        }

        private void Routed()
        {
            _route.Build(_path, _graph.Drive, _graph.Road, _graph.Junction, _graph.Speed);
            _phase = Phase.Routed;
            _routeOnFoot = _searchOnFoot;
            _routeAgainstTraffic = _searchAgainstTraffic;
            _segment = 0;
            _tracker.Reset();
            _route.Locate(_px, _py, _pz, ref _segment, out _along, out _lateral);
            _turnShown = -1;
            _leftShown = -1;
            Draw();
            Words(_routeOnFoot);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Navigation: route found to '" + _goalName + "' ("
                             + (_routeOnFoot ? "on foot" : _routeAgainstTraffic ? "driving, but no way with the traffic: one-way rules left out" : "driving")
                             + "; " + _searchWhy + "): " + _route.Count + " lane points, " + _route.Length.ToString("F0") + " m, " + (_route.Turns.Count - 1) + " turns, "
                             + _searching.Expanded + " search steps in " + _slices + " slices, " + _searchMilliseconds.ToString("F2", CultureInfo.InvariantCulture) + " ms in all (longest slice "
                             + _longestSlice.ToString("F2", CultureInfo.InvariantCulture) + " ms).");
            }
        }


        private void Follow(bool onFoot, float toPlace)
        {
            _route.Locate(_px, _py, _pz, ref _segment, out _along, out _lateral);
            switch (_tracker.Tick(Tick, onFoot, _lateral, _route.Length - _along, toPlace))
            {
                case NavEvent.Arrived:
                    Arrive(toPlace);
                    return;
                case NavEvent.Reroute:
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Navigation: " + _lateral.ToString("F0") + " m off the route for " + NavTracker.OffFor.ToString("F0") + " s (" + (onFoot ? "on foot" : "driving")
                                     + ", " + _along.ToString("F0") + " m along it): looking for a new route.");
                    }
                    Begin(onFoot, "off the route");
                    return;
            }
            if (_along - _drawnFrom > RedrawEvery)
            {
                Draw();
            }
            Words(onFoot);
        }

        // The route as a line from the player to the place itself, for the minimap and the city map.
        private void Draw()
        {
            _lineCount = 0;
            if (_route.Count > 0 && _phase == Phase.Routed)
            {
                _drawnFrom = _along;
                _lineCount = _route.Thin(_along, ref _lineX, ref _lineY, ref _lineZ);
                // the last steps, from the road to the place
                float dx = _goal.x - _lineX[_lineCount - 1];
                float dz = _goal.z - _lineZ[_lineCount - 1];
                if (dx * dx + dz * dz > 9f && _lineCount < _lineX.Length)
                {
                    _lineX[_lineCount] = _goal.x;
                    _lineY[_lineCount] = _lineY[_lineCount - 1];
                    _lineZ[_lineCount] = _goal.z;
                    _lineCount++;
                }
            }
            if (_hud != null)
            {
                _hud.RouteX = _lineX;
                _hud.RouteZ = _lineZ;
                _hud.Route(_lineCount);
            }
            if (_map != null)
            {
                _map.SetRoute(_lineX, _lineY, _lineZ, _lineCount);
            }
        }

        // The two lines, each put together again only when a figure in it has changed.
        private void Words(bool onFoot)
        {
            int next = _route.NextTurn(_along);
            if (next < 0)
            {
                return;
            }
            NavTurn turn = _route.Turns[next];
            float ahead = Mathf.Max(0f, turn.At - _along);
            float lastSteps = 0f;
            if (_route.Count > 0)
            {
                float dx = _goal.x - _route.X[_route.Count - 1];
                float dz = _goal.z - _route.Z[_route.Count - 1];
                lastSteps = Mathf.Sqrt(dx * dx + dz * dz);
            }
            if (turn.Kind == NavTurnKind.Arrive)
            {
                ahead += lastSteps;
            }
            bool close = ahead < 25f;
            int metres = NavMath.RoundMetres(ahead);
            if (next != _turnShown || metres != _metresShown || close != _nowShown)
            {
                _turnShown = next;
                _metresShown = metres;
                _nowShown = close;
                string street = turn.Kind == NavTurnKind.UTurn || turn.Kind == NavTurnKind.Arrive ? null : _graph.StreetOf(turn.Road);
                _line1 = NavText.T(NavText.TurnKey(turn.Kind, close, street != null), "{distance}", metres.ToFormattedDistance(), "{street}", street);
            }
            float left = Mathf.Max(0f, _route.Length - _along) + lastSteps;
            int leftRounded = NavMath.RoundMetres(left);
            if (leftRounded != _leftShown)
            {
                _leftShown = leftRounded;
                float seconds = onFoot ? left / Mathf.Max(1f, _pace) : _route.SecondsFrom(_along) / DrivingPace + lastSteps / 3f;
                _line2 = NavText.T("reallife_nav_remaining", "{distance}", leftRounded.ToFormattedDistance(), "{time}", ClockAfter(seconds));
            }
        }

        private void Straight(float toPlace)
        {
            int rounded = NavMath.RoundMetres(toPlace);
            if (rounded != _leftShown)
            {
                _leftShown = rounded;
                _line2 = NavText.T("reallife_nav_straight", "{distance}", rounded.ToFormattedDistance());
            }
        }

        // The game's clock this many of its seconds from now.
        private string ClockAfter(float seconds)
        {
            int minutes = Mathf.RoundToInt(TimeHelper.CurrentHour * 60f + TimeHelper.CurrentMinute + seconds * _rate);
            minutes = ((minutes % 1440) + 1440) % 1440;
            return (minutes / 60).GetFormattedTime(minutes % 60);
        }
    }
}
