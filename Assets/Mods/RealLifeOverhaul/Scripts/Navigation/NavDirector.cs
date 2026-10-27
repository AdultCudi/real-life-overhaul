using System;
using BAModAPI;
using Helpers;
using Parking.UndergroundParking;
using Scenes.MainMenu;
using UI;
using UI.InteriorDesigner;
using UI.Load;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The minimap and the GPS (docs/features/navigation.md): the minimap is a panel of the game's HUD (<see cref="NavHud"/>),
    /// and the GPS routes to whatever sets the game's destination marker, or a job's. With Voogle Route on, the city map is
    /// left to that mod.
    ///
    /// Cost a frame: while the panel shows, the player's place into <see cref="NavHud.Frame"/>; while a search is under way,
    /// one slice of at most 0.4 ms; while the base picture is made, a step of at most 0.8 ms. Everything else runs four
    /// times a second; with both options off the timer returns at once. The graphs and pictures are made behind the loading
    /// screen (<see cref="PrewarmRoads"/>). A fault switches the feature off for the session with one line.
    /// </summary>
    public sealed partial class NavDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("navigation", "NavDirector", typeof(NavDirector));

        private const float Tick = 0.25f;               // seconds
        private const float MarksEvery = 2f;            // seconds between two readings of the places

        public static IModLogger Logger;
        public static NavDirector Instance { get; private set; }

        /// <summary>The city's road graph for driving, for a search of another feature's own; null until it is made.</summary>
        public static RouteSearch Roads => Instance != null && Instance._graph.Ready ? Instance._graph.Drive : null;

        private readonly NavGraph _graph = new NavGraph();
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

        private const int PictureWaitMost = 1500;   // milliseconds the late prewarm waits for a picture's thread (some 100 to 130 ms)

        private void Awake()
        {
            Instance = this;
            NavMarks.Reset();
            _onMap = Guard.Wrap<bool>(OnMap);
            MapParts.Add("route on the map", MapLook);   // asked after the map shut, with Developer diagnostics on (Diagnostics/SessionWatch.Map)
            OtherMods.Look(Logger);   // done once for the city already; a loop over the table
            _voogle = OtherMods.VoogleRoute.Loaded;
            ScenePrewarm.AddLate("navigation: the road graph, the walking graph and the minimap's two pictures being made", PrewarmRoads);
        }

        // Behind the loading screen, once the traffic system has marked the junctions: the graphs and both pictures, drawn
        // side by side on threads and sent here, or an earlier load's pictures of the same city.
        private bool PrewarmRoads()
        {
            bool wantMap = Settings.Minimap;
            if (!wantMap && !(Settings.Gps && !RouteLeft))
            {
                return true;   // switched on later: made on the timer
            }
            if (!_graph.StepsNow())
            {
                return false;
            }
            if (wantMap && Settings.MinimapBase && _base.Idle)
            {
                _base.Begin(_graph, false);   // still idle if the game's palette is not there yet: the timer begins it
            }
            _graph.WaitForPicture(PictureWaitMost);
            if (_base.Drawing && _base.WaitForDrawing(PictureWaitMost))
            {
                _base.Step(false);   // sent to the graphics card now
            }
            return true;
        }

        // The route left to the other mod altogether: it is switched on and the player chose its line.
        private bool RouteLeft => _voogle && Settings.NavOtherRoute;

        private void OnDestroy()
        {
            GlobalEvents.onCityMapToggle -= _onMap;
            Remove();
            _base.Drop(false);   // the picture is kept for the next load of the same city
            _graph.Dispose();
            RoadGraph.Forget();   // the city's one road graph goes with it
            NavMarks.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Navigation: NavDirector.Update");
        private static readonly int HotOnMap = HotSpots.Slot("Navigation: NavDirector.OnMap");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
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
                if (_base.Busy)
                {
                    _base.Step(SaveGameManager.Current != null && !LoadScene.isLoading);
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
            _hud.Frame(_px, _pz, _heading, ViewHeading(_heading), _metres, Settings.MinimapRotate);
        }

        // Degrees clockwise from north of what is ahead on the screen; a camera looking straight down has its screen's up as
        // ahead. With no camera, the player's way.
        private static float ViewHeading(float fallback)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return fallback;
            }
            Transform t = camera.transform;
            Vector3 ahead = t.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude < 0.0001f)
            {
                ahead = t.up;
                ahead.y = 0f;
            }
            return ahead.sqrMagnitude < 0.0001f ? fallback : Mathf.Atan2(ahead.x, ahead.z) * Mathf.Rad2Deg;
        }


        // Four times a second.
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
            BaseWanted(wantMap);
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
                _graph.Step();   // one step a tick, where the prewarm could not make it
            }
            if (SaveGameManager.Current == null || LoadScene.isLoading)
            {
                Show(false);
                return;
            }
            EventHold.Keep(ref GlobalEvents.onCityMapToggle, _onMap, ref _mapSeen);   // the game clears its events on every load
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
                _baseGiven = false;
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
            BaseShown(now);
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

        // The player on foot or at a wheel, where, and whether that is out in the city.
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
                || GameManager.IsAnyMiniGameActive() || GameMembers.FuneralActive || EnergyHelper.goingToHospital || (uis.timeMachine != null && uis.timeMachine.isRunning))
            {
                return false;
            }
            Func<bool> held = NavBridges.HudHeld;
            return held == null || !held();
        }

        // How fast the player walks and the game's clock runs, both against the game's own seconds as the lanes' speeds are,
        // so a time to arrive is a time on the game's clock.
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


        // Everything this feature put into the game's interface goes; the graph and the picture stay for the session.
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
            _baseGiven = false;
            _aloneUntil = 0f;
            _hudSaid = false;
        }

        /// <summary>For the save tools: off the HUD and the map. It comes back with the options on the next tick.</summary>
        public void StopAll()
        {
            Remove();
        }
    }
}
