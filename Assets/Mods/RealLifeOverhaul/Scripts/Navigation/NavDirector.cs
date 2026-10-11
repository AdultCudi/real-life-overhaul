using System;
using System.Collections.Generic;
using System.Diagnostics;
using BAModAPI;
using Helpers;
using Parking.UndergroundParking;
using Scenes.MainMenu;
using Streets;
using UI;
using UI.Guiders;
using UI.InteriorDesigner;
using UI.Load;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The minimap and the GPS. See the README ("Minimap and GPS").
    ///
    /// The minimap is a panel of the game's HUD (<see cref="NavHud"/>) that shows a picture of the city's roads made
    /// once per city (<see cref="NavGraph"/>), the player, the places that matter (<see cref="NavMarks"/>) and the
    /// route. The GPS takes its destination from the game: whatever sets the game's destination marker (a building's
    /// card, a task, the phone, this mod's own "set destination" buttons and map markers) or a job's, gets a route
    /// with nothing more to do. The route is searched for over the road graph a slice a frame, followed on a timer,
    /// and drawn on the minimap and on the game's city map (<see cref="NavCityMap"/>).
    ///
    /// With the mod Voogle Route switched on (<see cref="OtherMods"/>) the city map is left to it altogether: it draws
    /// its route there as lines in the world on the map's own layer and has its own click, panels and markers on the
    /// map, so nothing of this mod's is added to the map (no route line, no double click). This mod's route is then
    /// drawn on its own minimap only, where the other mod draws nothing; and with "Use the other mod's route line" on
    /// there is no route of this mod's at all, only the minimap. Either way that mod announces the arrival and ends the
    /// destination: this mod then only clears its own line.
    ///
    /// What runs when:
    ///   every frame      while the panel shows: the player's place and heading into <see cref="NavHud.Frame"/> (which
    ///                    writes only what changed); while a search is under way: one slice of it, 0.4 ms at most
    ///   four times a second   everything else: is the panel to show and where, the destination, where the player is
    ///                    on the route, the two lines (written when a figure in them changes), which markers are in view
    ///   every two seconds     the places are read again
    /// With both options off nothing is built and the timer returns at once.
    ///
    /// A fault anywhere switches the feature off for the session with one line, and takes its objects off the HUD.
    /// </summary>
    public sealed class NavDirector : MonoBehaviour
    {
        private const float Tick = 0.25f;
        private const float MarksEvery = 2f;
        private const double SliceMilliseconds = 0.4;   // of one frame, for a search under way
        private const int SliceSteps = 64;              // the clock is looked at after this many steps
        private const float StartReachOnFoot = 30f;     // lane points this near the player may begin the route
        private const float StartReachDriving = 14f;
        private const float GoalReach = 30f;            // ...and this near the destination may end it
        private const float FarReach = 400f;            // the single nearest lane point is taken up to this far away
        private const float Level = 9f;                 // metres up or down that are still the same road level
        private const float DrivingPace = 0.75f;        // the share of a lane's speed a drive makes good, lights and corners included
        private const float RedrawEvery = 25f;          // metres driven before the line is cut back to the player again
        private const float SameGoal = 5f;              // a destination that has moved less than this is the same one
        private const float RearmBeyond = 150f;         // after arriving, a new route to the same place is made only from this far away
        private const float RetryAfter = 10f;           // seconds before a search that found no way is tried again

        public static IModLogger Logger;
        public static NavDirector Instance { get; private set; }

        /// <summary>The city's road graph for driving once it is made, for whoever searches the same roads with a search of its own; null until then.</summary>
        public static RouteSearch Roads => Instance != null && Instance._graph.Ready ? Instance._graph.Drive : null;

        private enum Phase { Idle, Searching, Routed, NoRoute }

        private readonly NavGraph _graph = new NavGraph();
        private readonly NavRoute _route = new NavRoute();
        private readonly NavTracker _tracker = new NavTracker();
        private readonly List<int> _path = new List<int>(2048);
        private readonly List<int> _starts = new List<int>(64);
        private readonly List<int> _goals = new List<int>(64);
        private NavHud _hud;
        private NavCityMap _map;
        private bool _hudGaveUp, _mapGaveUp;
        private bool _broken;
        private bool _voogle;       // the mod Voogle Route is switched on: the city map is its own
        private bool _otherSaid;
        private float _next;
        private float _nextMarks;
        private bool _showing;
        private bool _pictureGiven;
        private bool _hudSaid;

        // the player
        private Transform _follow;
        private VehicleController _vehicle;
        private Rigidbody _body;
        private bool _driving;
        private float _px, _py, _pz, _heading;
        private float _lastX, _lastZ;
        private float _pace = 3.5f;              // metres a second the player has been making on foot
        private float _rate = 1f;                // minutes on the game's clock per second of the game's time, as it has been running
        private float _lastMinutes = -1f, _lastSeconds;
        private float _metres = NavView.BaseMetres;

        // the destination
        private DirectionGuider _guider;
        private Vector3 _goal;
        private string _goalName;
        private bool _hasGoal;
        private bool _arrived;

        // the search and the route
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

        private Action<bool> _onMap, _mapSeen;
        private bool _mapOpen;

        private void Awake()
        {
            Instance = this;
            NavMarks.Reset();
            _onMap = OnMap;
            OtherMods.Look(Logger);   // (done once for the city already; this costs a loop over the table)
            _voogle = OtherMods.VoogleRoute.Loaded;
        }

        // True while the route is left to the other mod altogether: it is switched on and the player chose its line.
        private bool RouteLeft => _voogle && Settings.NavOtherRoute;

        private void OnDestroy()
        {
            GlobalEvents.onCityMapToggle -= _onMap;
            Remove();
            _graph.Dispose();
            NavMarks.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (_broken)
            {
                return;
            }
            try
            {
                float now = Time.unscaledTime;
                if (now >= _next)
                {
                    _next = now + Tick;
                    Slow(now);
                }
                if (_phase == Phase.Searching)
                {
                    Search();
                }
                if (_showing && Settings.Minimap)
                {
                    View();
                }
            }
            catch (Exception e)
            {
                _broken = true;
                Logger?.Error(e);
                Logger?.Warn("Navigation: switched off for this session after an error.");
                try
                {
                    Remove();
                }
                catch (Exception again)
                {
                    Logger?.Error(again);
                }
            }
        }

        // ---------------------------------------------------------------- every frame

        private void View()
        {
            if (_follow == null || _hud == null)
            {
                return;
            }
            Vector3 at = _follow.position;
            _px = at.x;
            _py = at.y;
            _pz = at.z;
            _heading = _follow.eulerAngles.y;
            float speed = _driving && _body != null ? _body.velocity.magnitude : 0f;
            _metres = NavView.Ease(_metres, NavView.MetresAcross(Settings.MinimapZoom, speed), Time.unscaledDeltaTime, 1.6f);
            _hud.Frame(_px, _pz, _heading, _metres, Settings.MinimapRotate);
        }

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
                // No way with the traffic (the nearest lane leads away and nowhere back, or the place is on a stretch
                // that cannot be driven to): the way along the same streets without the one-way rule is shown instead.
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
                             + _goals.Count + " to end at, " + _searching.Expanded + " search steps in " + _slices + " slices, " + _searchMilliseconds.ToString("F2")
                             + " ms. The marker's direction and distance are shown; tried again in " + RetryAfter.ToString("F0") + " s.");
            }
        }

        // ---------------------------------------------------------------- four times a second

        private void Slow(float now)
        {
            bool wantMap = Settings.Minimap;
            bool wantGps = Settings.Gps && !RouteLeft;
            if (_voogle && !_otherSaid && Meta.Dev.Verbose)
            {
                _otherSaid = true;   // once a city
                Logger?.Info("Navigation check (other mod): " + OtherMods.VoogleRoute.Name + " is switched on: "
                             + (RouteLeft ? "the route is left to it, this mod shows its minimap only" : "this mod's route is drawn on its own minimap only")
                             + ", and nothing of this mod's is on the city map (" + (_map == null ? "no object there: PASS" : "FAIL: its route object is still there") + ").");
            }
            if (!wantMap && !wantGps)
            {
                if (_hud != null || _map != null || _hasGoal)
                {
                    Remove();
                }
                return;
            }
            if (!_graph.Finished)
            {
                _graph.Step();   // one step a tick, and under the loading screen where the city's roads are there in time
            }
            if (SaveGameManager.Current == null || LoadScene.isLoading)
            {
                Show(false);
                return;
            }
            EventHold.Keep(ref GlobalEvents.onCityMapToggle, _onMap, ref _mapSeen);   // (the game clears its events on every load)
            if (CityMap.IsOpen != _mapOpen)
            {
                OnMap(CityMap.IsOpen);
            }
            if (!wantGps && _map != null)
            {
                UnityEngine.Object.Destroy(_map.gameObject);   // the GPS was switched off: nothing of it stays on the city map
                _map = null;
            }
            bool outdoors = Player();
            Clock();
            if (_hud != null && !_hud.Alive)
            {
                _hud = null;   // the game rebuilt its HUD: the copy went with it
                _pictureGiven = false;
            }
            if (_hud == null && !_hudGaveUp && _graph.Ready)
            {
                _hud = NavHud.Build(_graph, out bool later);
                _hudGaveUp = _hud == null && !later;
                if (_hudGaveUp)
                {
                    Logger?.Warn("Navigation: the game's HUD is not built as this version expects, so the minimap and the GPS's lines are left off. The route is still drawn on the city map.");
                }
                if (_hud != null && _hasGoal)
                {
                    GoalOnHud();
                    Draw();
                }
            }
            if (_hud != null && !_pictureGiven && _graph.Picture != null)
            {
                _pictureGiven = true;
                _hud.SetPicture(_graph.Picture);
            }
            if (wantGps && _graph.Ready)
            {
                Guide(now, outdoors);
            }
            else if (_hasGoal)
            {
                Forget();
            }
            bool lines = wantGps && _hasGoal && !_arrived && _phase != Phase.Idle;
            bool show = outdoors && _hud != null && (wantMap || lines) && HudFree();
            if (_hud != null)
            {
                _hud.Place(Settings.MinimapSize, wantMap, lines);
                if (lines)
                {
                    _hud.Lines(_line1, _line2);
                }
            }
            Show(show);
            if (show && wantMap)
            {
                if (now >= _nextMarks)
                {
                    _nextMarks = now + MarksEvery;
                    NavMarks.Gather(_hasGoal ? _guider : null);
                    _hud.MarksChanged();
                }
                _hud.Marks(NavMarks.All, _px, _pz, _metres);
                if (!_hudSaid && Meta.Dev.Verbose)
                {
                    _hudSaid = true;
                    Logger?.Info("Navigation: minimap on the HUD, a copy of the game's task panel, " + _hud.Where + "; view " + Mathf.RoundToInt(_hud.ViewWidth) + " x "
                                 + Mathf.RoundToInt(_hud.ViewHeight) + " units; road picture " + (_graph.Picture != null ? "there" : "not there yet") + "; "
                                 + NavMarks.All.Count + " places known, " + NavHud.Pool + " markers pooled.");
                }
            }
        }

        private void Show(bool show)
        {
            _showing = show;
            if (_hud != null)
            {
                _hud.Show(show);
            }
        }

        // Who the player is just now (on foot or at a wheel), where, and whether that is out in the city.
        private bool Player()
        {
            GameManager game = InstanceBehavior<GameManager>.Instance;
            PlayerController player = game != null ? game.playerController : null;
            if (player == null)
            {
                _follow = null;
                return false;
            }
            VehicleController vehicle = PlayerHelper.IsUsingVehicle ? game.selectedVehicle : null;
            if (vehicle != _vehicle)
            {
                _vehicle = vehicle;
                _driving = vehicle != null && vehicle.vehicleType != null && vehicle.vehicleType.IsMotorVehicle;
                _body = _driving ? vehicle.GetComponent<Rigidbody>() : null;
            }
            _follow = _driving ? vehicle.transform : player.transform;
            bool outdoors = !BuildingManager.IsInsideBuilding && !UndergroundParkingManager.IsInsideParking && !SubwaySystem.IsRiding
                            && !CasinoBoatManager.IsOnCasinoBoat && !player.awaitingRepositioning;
            // Indoors the player's place in the city is the door they went in by (the game's own answer), not where the room is kept.
            Vector3 at = outdoors ? _follow.position : PlayerHelper.GetCityPosition();
            _px = at.x;
            _py = at.y;
            _pz = at.z;
            return outdoors;
        }

        // True while nothing of the game's or the mod's has the screen.
        private static bool HudFree()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.smartphoneUI == null || !uis.smartphoneUI.gameObject.activeInHierarchy)
            {
                return false;   // the game switches the phone off with the rest of its HUD (a menu, the interior designer, a mini game, a scene)
            }
            if (CityMap.IsOpen || FullMenu.IsOpen || UI.MiniMenu.MiniMenu.IsOpen || Options.IsVisible || BuildingPreview.isPreviewing || InteriorDesignerUI.IsOpen
                || GameManager.IsAnyMiniGameActive() || FuneralHelper.IsFuneralActive || EnergyHelper.goingToHospital || (uis.timeMachine != null && uis.timeMachine.isRunning))
            {
                return false;
            }
            Func<bool> held = NavBridges.HudHeld;
            return held == null || !held();
        }

        // How fast the player walks, and how fast the game's clock runs, from what both have been doing. Both are
        // measured against the game's own seconds (which stand still in a pause and run faster at a higher game
        // speed), as the lanes' speeds are, so that a time to arrive is a time on the game's clock.
        private void Clock()
        {
            float minutes = TimeHelper.NowInMinutes();
            float now = Time.time;
            float seconds = now - _lastSeconds;
            if (_lastMinutes >= 0f && seconds > 0.1f && seconds < 2f)
            {
                float passed = minutes - _lastMinutes;
                if (passed > 0.02f && passed < 20f)
                {
                    _rate += (Mathf.Clamp(passed / seconds, 0.1f, 20f) - _rate) * 0.1f;
                }
                if (!_driving)
                {
                    float dx = _px - _lastX;
                    float dz = _pz - _lastZ;
                    float speed = Mathf.Sqrt(dx * dx + dz * dz) / seconds;
                    if (speed > 1f && speed < 12f)
                    {
                        _pace += (speed - _pace) * 0.05f;
                    }
                }
            }
            _lastMinutes = minutes;
            _lastSeconds = now;
            _lastX = _px;
            _lastZ = _pz;
        }

        // ---------------------------------------------------------------- the destination

        // The game's destination marker if it is set; a job's destination otherwise.
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
                // Indoors nothing is followed. With the city map open a route is still wanted on it: from the door
                // of the building the player is in (the game's own "where the player is in the city"), on foot.
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
                _arrived = false;   // gone well away since arriving, and the marker still stands: guided back to it
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

        // Forgets the destination and everything made for it.
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
                own = false;   // that mod says "arrived" and ends the destination by its own rule: one notice and one rule for one arrival
            }
            else
            {
                Notifications.Show(NotificationType.Success, "reallife_nav_arrived", new Dictionary<string, string>(1) { { "place", _goalName ?? string.Empty } },
                                   6f, "reallife_nav_arrived", null, true, false);
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Navigation: arrived at '" + _goalName + "' (" + toPlace.ToString("F0") + " m from it, " + (_driving ? "driving" : "on foot")
                             + "). The route is cleared" + (own ? " and the destination's marker with it." : _voogle ? "; the notice and the marker are the other mod's." : "; a job's marker is the job's to take away."));
            }
            if (own)
            {
                GuidersManager.ResetGuider(DirectionGuiderType.Destination);   // the player's own destination is done with: its marker goes, as a navigator's does
            }
        }

        // ---------------------------------------------------------------- finding the route

        private void Begin(bool onFoot, string why)
        {
            NavIndex places = _graph.Places;
            RouteSearch drive = _graph.Drive;
            _starts.Clear();
            _goals.Clear();
            places.Within(_px, _pz, onFoot ? StartReachOnFoot : StartReachDriving, _starts);
            // At a wheel: only lanes that run the way the car points, so that the route does not begin with the lane beside it going the other way.
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
                Logger?.Info("Navigation: route found to '" + _goalName + "' (" + (_routeOnFoot ? "on foot" : _routeAgainstTraffic ? "driving, but no way with the traffic: one-way rules left out" : "driving")
                             + "; " + _searchWhy + "): " + _route.Count + " lane points, " + _route.Length.ToString("F0") + " m, " + (_route.Turns.Count - 1) + " turns, "
                             + _searching.Expanded + " search steps in " + _slices + " slices, " + _searchMilliseconds.ToString("F2") + " ms in all (longest slice "
                             + _longestSlice.ToString("F2") + " ms).");
            }
        }

        // ---------------------------------------------------------------- following it

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

        // The route as a line, from where the player is to the place itself: for the minimap and for the city map.
        private void Draw()
        {
            _lineCount = 0;
            if (_route.Count > 0 && _phase == Phase.Routed)
            {
                _drawnFrom = _along;
                _lineCount = _route.Thin(_along, ref _lineX, ref _lineY, ref _lineZ);
                // The last steps, from the road to the place.
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

        // The two lines. Each is put together again only when a figure in it has changed.
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

        // No route: the plain distance to the place.
        private void Straight(float toPlace)
        {
            int rounded = NavMath.RoundMetres(toPlace);
            if (rounded != _leftShown)
            {
                _leftShown = rounded;
                _line2 = NavText.T("reallife_nav_straight", "{distance}", rounded.ToFormattedDistance());
            }
        }

        // The game's clock this many of the game's seconds from now, as the game writes a time.
        private string ClockAfter(float seconds)
        {
            int minutes = Mathf.RoundToInt(TimeHelper.CurrentHour * 60f + TimeHelper.CurrentMinute + seconds * _rate);
            minutes = ((minutes % 1440) + 1440) % 1440;
            return (minutes / 60).GetFormattedTime(minutes % 60);
        }

        // ---------------------------------------------------------------- the city map

        // The game's own event (and the slow tick, if the event was missed). Nothing thrown here reaches the game.
        private void OnMap(bool open)
        {
            try
            {
                MapToggled(open);
            }
            catch (Exception e)
            {
                _mapGaveUp = true;
                Logger?.Error(e);
                Logger?.Warn("Navigation: the route on the city map is switched off for this session after an error.");
            }
        }

        private void MapToggled(bool open)
        {
            _mapOpen = open;
            if (_broken || _voogle || !Settings.Gps || _mapGaveUp)
            {
                if (_map != null)
                {
                    _map.Open(false);
                }
                return;
            }
            if (open && _map == null && !_mapGaveUp)
            {
                Colors colours = InstanceBehavior<GlobalReferences>.Instance != null ? InstanceBehavior<GlobalReferences>.Instance.colors : null;
                Color32 colour = colours != null ? colours.blue : new Color32(60, 130, 240, 255);
                colour.a = 235;
                _map = NavCityMap.Make(colour);
                _mapGaveUp = _map == null;
                if (_mapGaveUp)
                {
                    Logger?.Warn("Navigation: the city map's marker layer was not found, so the route is not drawn on the city map and a double click there sets no destination.");
                }
                else
                {
                    _map.Picked = Pick;
                    _map.SetRoute(_lineX, _lineY, _lineZ, _lineCount);
                }
            }
            if (_map != null)
            {
                _map.Clicks = true;
                _map.Open(open);
            }
        }

        // Two clicks on one spot of the open map: that spot is the destination.
        private void Pick(Vector2 onScreen)
        {
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                GuidersManager guiders = InstanceBehavior<GuidersManager>.Instance;
                Camera camera = GameManager.GetMainCamera();
                if (!CityMap.IsOpen || city == null || city.cityMap == null || city.cityMap.IsTaxiMode || city.cityMap.isSubwayMode || BuildingPreview.isPreviewing
                    || guiders == null || guiders.destinationGuider == null || camera == null || SaveGameManager.Current == null)
                {
                    return;
                }
                Ray ray = camera.ScreenPointToRay(onScreen);
                int mask = city.cityMap.cityMapMask.value | city.cityMap.cityMapMaskLowDetail.value;
                Vector3 place;
                CityBuildingController building = null;
                if (Physics.Raycast(ray, out RaycastHit hit, 4000f, mask, QueryTriggerInteraction.Ignore))
                {
                    place = hit.point;
                    building = hit.collider != null ? hit.collider.GetComponentInParent<CityBuildingController>() : null;
                }
                else if (ray.direction.y < -0.01f)
                {
                    place = ray.origin + ray.direction * ((_py - ray.origin.y) / ray.direction.y);   // nothing solid under the pointer: the level the player stands on
                }
                else
                {
                    return;
                }
                string name;
                if (building != null && building.building != null && !building.building.Address.IsUndefined())
                {
                    Address address = building.building.Address;
                    GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination);   // what a building's card does (it also keeps the address in the save)
                    name = address.ToFormattedString();
                }
                else
                {
                    SaveGameManager.Current.customDestination = null;   // a spot is not an address: the save keeps none
                    name = _graph.Ready ? _graph.StreetNear(place.x, place.y, place.z) : null;
                    GuidersManager.SetGuiderTarget(place + Vector3.up * 1.5f, name, guiders.destinationGuider.poiICon, guiders.destinationGuider.poiBackgroundColor, DirectionGuiderType.Destination);
                    name = name ?? NavText.T("poi_custom_location");
                }
                Notifications.Show(NotificationType.Success, "notification_destination_set", new Dictionary<string, string>(1) { { "address", name } });
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Navigation: destination set by a double click on the city map: '" + name + "' at " + place.x.ToString("F0") + ", " + place.z.ToString("F0")
                                 + (building != null ? " (a building)." : " (a spot)."));
                }
                _next = 0f;   // looked at in this frame, so that the route is on the map at once
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- taking it away, tests

        // Everything this feature has put into the game's interface goes; the graph and the picture stay for the session.
        private void Remove()
        {
            Forget();
            _showing = false;
            if (_hud != null)
            {
                _hud.Destroy();
                _hud = null;
            }
            if (_map != null)
            {
                UnityEngine.Object.Destroy(_map.gameObject);
                _map = null;
            }
            _pictureGiven = false;
            _hudSaid = false;
        }

        /// <summary>For the save tools: off the HUD and the map. It comes back with the options on the next tick.</summary>
        public void StopAll()
        {
            Remove();
        }

        private bool Refused(string test)
        {
            if (!ModState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null || !_graph.Ready)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return true;
            }
            if (_broken || !Settings.Gps)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return true;
            }
            if (RouteLeft)
            {
                TestRefusals.TellKey(Logger, test, "reallife_nav_test_other_mod");
                return true;
            }
            if (BuildingManager.IsInsideBuilding || UndergroundParkingManager.IsInsideParking)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.Indoors);
                return true;
            }
            return false;
        }

        /// <summary>"Test: route to the nearest police station".</summary>
        public void TestStation()
        {
            try
            {
                if (Refused("Route to a police station"))
                {
                    return;
                }
                Func<bool> guide = NavBridges.GuideToStation;
                if (guide == null || !guide())
                {
                    TestRefusals.Tell(Logger, "Route to a police station", TestRefusal.NoPrecinct);
                    return;
                }
                _next = 0f;
                Logger?.Info("Navigation test: the nearest police station is the destination.");
                Notifications.Show(NotificationType.Info, "reallife_nav_test_set", null, 8f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>"Test: route to the far end of the city": the lane point furthest from the player, which is the longest search there is from here.</summary>
        public void TestFar()
        {
            try
            {
                Far();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Far()
        {
            if (Refused("Route across the city"))
            {
                return;
            }
            GuidersManager guiders = InstanceBehavior<GuidersManager>.Instance;
            RouteSearch drive = _graph.Drive;
            int furthest = -1;
            float most = 0f;
            for (int i = 0; i < drive.Count; i++)
            {
                if (!_graph.Usable[i] || _graph.Junction[i])
                {
                    continue;
                }
                float dx = drive.X[i] - _px;
                float dz = drive.Z[i] - _pz;
                float sqr = dx * dx + dz * dz;
                if (sqr > most)
                {
                    most = sqr;
                    furthest = i;
                }
            }
            if (furthest < 0 || guiders == null || guiders.destinationGuider == null)
            {
                TestRefusals.Tell(Logger, "Route across the city", TestRefusal.NotLoaded);
                return;
            }
            SaveGameManager.Current.customDestination = null;
            GuidersManager.SetGuiderTarget(new Vector3(drive.X[furthest], drive.Y[furthest] + 1.5f, drive.Z[furthest]), NavText.T("reallife_nav_test_far_name"),
                                           guiders.destinationGuider.poiICon, guiders.destinationGuider.poiBackgroundColor, DirectionGuiderType.Destination);
            _next = 0f;
            Logger?.Info("Navigation test: destination set " + Mathf.Sqrt(most).ToString("F0") + " m away in a straight line, at the lane point furthest from the player.");
            Notifications.Show(NotificationType.Info, "reallife_nav_test_set", null, 8f);
        }

        /// <summary>"Test: clear the destination".</summary>
        public void TestClear()
        {
            try
            {
                if (SaveGameManager.Current == null || InstanceBehavior<GuidersManager>.Instance == null)
                {
                    TestRefusals.Tell(Logger, "Clear the destination", TestRefusal.NotLoaded);
                    return;
                }
                SaveGameManager.Current.customDestination = null;
                GuidersManager.ResetGuider(DirectionGuiderType.Destination);
                _next = 0f;
                Notifications.Show(NotificationType.Info, "reallife_nav_test_cleared", null, 6f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
