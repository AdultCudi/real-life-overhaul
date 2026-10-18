using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Traffic accidents: stages a collision with the game's AI cars, or takes one the reckless drivers caused (BeginAt), and
    /// runs the scene: the wrecks, police and for the worse ones an ambulance, sometimes a driver who bolts (<see cref="RoadChase"/>),
    /// cleared within three minutes (five for a casualty still seen to). ResponseUnit drives the vehicles, SceneParking and
    /// SceneCordon place them and close the road, AccidentCast and RoadsideStop put the people on it. Cost: idle, one comparison
    /// a frame; a scene, a few timers a frame (the wrecks are frozen).
    /// </summary>
    public sealed partial class TrafficAccidents : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("traffic", "TrafficAccidents", typeof(TrafficAccidents));

        public enum Severity { Minor, Moderate, Severe }

        private const float CheckInterval = 20f;     // seconds between rolls for an accident
        private const float MinDistance = 45f;       // metres from the player a car to start one is looked for...
        private const float MaxDistance = 140f;      // ...and up to this
        private const float MovingSpeed = 10f;      // km/h: slower than this a car is not "on the move"
        private const float HelpDistance = 9f;       // metres from the scene at which the player has helped
        private const float MaxSceneTime = 175f;     // seconds: every scene is planned to clear inside three minutes
        private const float HardSceneTime = 300f;    // ...and runs on to this only while a crew still sees to someone in the road

        private enum Phase { None, Tumbling, Shunting, Active }

        // Ready: crew aboard, lit, about to drive off (the patrol car waits for the scene to clear). Leaving: driven off by the mod.
        private enum Stage { Idle, Waiting, EnRoute, Chasing, OnScene, Ready, Leaving, Done }

        // Planned: one driver will bolt when the patrol car turns up. Running: they have, and RoadChase has the two cars.
        private enum Chase { None, Planned, Running }

        // How the scene is reported once it clears; one is picked at random.
        private static readonly string[] MinorEndings =
        {
            "reallife_accident_cleared_minor_1", "reallife_accident_cleared_minor_2"
        };
        private static readonly string[] ModerateEndings =
        {
            "reallife_accident_cleared_moderate_1", "reallife_accident_cleared_moderate_2", "reallife_accident_cleared_moderate_3"
        };
        private static readonly string[] SingleMinorEndings =
        {
            "reallife_accident_cleared_single_minor_1", "reallife_accident_cleared_single_minor_2"
        };
        private static readonly string[] SingleModerateEndings =
        {
            "reallife_accident_cleared_single_moderate_1", "reallife_accident_cleared_single_moderate_2"
        };
        private static readonly string[] SingleSevereEndings =
        {
            "reallife_accident_cleared_single_severe_1", "reallife_accident_cleared_single_severe_2"
        };
        private static readonly string[] SevereEndings =
        {
            "reallife_accident_cleared_severe_1", "reallife_accident_cleared_severe_2", "reallife_accident_cleared_severe_3"
        };

        public static IModLogger Logger;
        public static TrafficAccidents Instance { get; private set; }

        public bool HasActiveAccident => _phase != Phase.None;

        /// <summary>True while an accident scene is on the street. Only one incident of any kind runs at a time.</summary>
        public static bool IsActive => Instance != null && Instance.HasActiveAccident;

        /// <summary>True while the scene is about someone lying in the road who has not been taken away yet.</summary>
        public bool HasCasualty => _phase != Phase.None && _casualty != null && _casualty.Present;

        /// <summary>True, with where, while an accident the player did not cause is on the street and nobody has helped.</summary>
        public static bool Needing(out Vector3 where)
        {
            TrafficAccidents a = Instance;
            where = a != null ? a._scene : Vector3.zero;
            return a != null && a.HasActiveAccident && a._thanksForStopping && !a._playerHelped;
        }

        private readonly HashSet<Rigidbody> _involved = new HashSet<Rigidbody>();
        private readonly List<Wreck> _wrecks = new List<Wreck>();
        private readonly List<VehicleComponent> _others = new List<VehicleComponent>();
        private readonly List<Waypoint> _routeScratch = new List<Waypoint>();
        private readonly List<Vector3> _fleePath = new List<Vector3>(256);
        private readonly List<Footprint> _footprints = new List<Footprint>(4);
        private readonly List<Renderer> _seatScratch = new List<Renderer>(4);   // a wreck's seat, handed with it to the tow truck

        private Phase _phase = Phase.None;
        private bool _recheck;       // a scene has ended: its road and cars are looked at once more (Recheck)
        private readonly List<VehicleComponent> _released = new List<VehicleComponent>(4);   // the wrecks of the scene that ended
        // Where a car to start a collision with is looked for; a test button widens these for its retries.
        private float _searchFrom = MinDistance;
        private float _searchTo = MaxDistance;
        private float _searchSpeed = MovingSpeed;
        private Severity _severity;
        private float _deadline;
        private float _hardLimit;    // the deadline is never put back beyond this
        private bool _extended;
        private bool _clearLogged;   // a place to stand or park that failed its clear test has been logged for this scene
        private float _lingerUntil;
        private float _nextCheck;
        private Vector3 _scene;
        private bool _playerHelped;
        private bool _attended;      // a unit reached the scene, so the scene may end when the units are done
        private bool _fled;          // a driver ran for it; changes how the scene is reported
        private bool _single;        // one car on its own: worded as that, never as a collision between drivers
        private string _markerLabel;
        private readonly RealLifeOverhaul.GameUi.GuideMark _mark = new RealLifeOverhaul.GameUi.GuideMark("traffic accidents");

        // Staging, kept while a rolling car comes to rest.
        private Wreck _leadWreck;
        private Wreck _worstWreck;   // whose driver the cast calls A (the casualty of a severe crash)...
        private Wreck _otherWreck;   // ...and B
        private bool _seatsEmptied;
        private bool _conesIn;       // a unit has started to leave: the cones were taken in and stay in
        private readonly List<Wreck> _exitWrecks = new List<Wreck>(4);   // wrecks standing in the lane a loaded ambulance leaves by: taken off the road first
        private bool _closing;       // the component is being destroyed: nothing is sent off, everything is released at once
        private Vector3 _origin;
        private Quaternion _heading;
        private float _side;
        private float _tumbleRestFrom;
        private float _tumbleEnd;
        private float _shuntRestFrom;
        private float _shuntEnd;

        private Attendance _ambulance;
        private Attendance _police;

        private ResponseUnit _suspect;
        private Waypoint _fleeAnchor;
        private Chase _chase = Chase.None;
        private readonly RoadChase _pursuit = new RoadChase();

        // A collision that happened on the road (BeginAt): the cars stay where they met and the notice names its cause.
        private bool _inPlace;
        private string _causeKey;

        // The people, told what happens and ticked from here.
        private readonly AccidentCast _people = new AccidentCast();
        private readonly RoadsideStop _arrest = new RoadsideStop();

        private SceneCordon _cordon;

        // A scene about a person knocked down instead of (or as well as) wrecked cars; see StageCasualty.
        private ICasualty _casualty;
        private bool _casualtyScene;
        private bool _fatal;
        private bool _thanksForStopping = true;   // the player is thanked for walking up, unless they caused it

        private void Awake()
        {
            Instance = this;
            _nextCheck = Time.time + CheckInterval;
            _ambulance = new Attendance(new ResponseUnit(gameObject), police: false);
            _police = new Attendance(new ResponseUnit(gameObject), police: true);
            _suspect = new ResponseUnit(gameObject);
            _cordon = new SceneCordon(transform);
            TrafficWatch.Stuck = Unstick;
            _mark.Keep(this);   // the marker goes with this component
        }

        // Stopped by the guard or the feature switched off: a running scene would stand in the road all session, so it ends here.
        private void OnDisable()
        {
            if (_phase == Phase.None)
            {
                return;
            }
            bool closing = _closing;
            try
            {
                _closing = true;
                Clear(resumeTraffic: true, announce: false, why: "its updates stopped");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _mark.Hide();
            }
            finally
            {
                _closing = closing;
            }
        }

        private void OnDestroy()
        {
            _closing = true;
            Clear(resumeTraffic: true, announce: false);
            TrafficEffects.Dispose();
            Shapes.Dispose();
            if (Instance == this)
            {
                Instance = null;
                TrafficWatch.Stuck = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Traffic: TrafficAccidents.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
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
            TrafficWatch.Tick(Time.time);   // one comparison a frame; looks at the traffic five times a second
            if (_lacking)
            {
                return;
            }
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                // Clear leaves no scene, so a fault cannot repeat; a game lacking something named stops the accidents for the session.
                Logger?.Error(e);
                Clear(resumeTraffic: true, announce: false, why: "error");
                _lacking = Guard.StandsDown(e, "traffic accidents");
            }
        }

        private bool _lacking;

        private void Tick()
        {
            if (_phase == Phase.None)
            {
                if (_recheck && TrafficTools.Ready)
                {
                    Recheck();
                }
                if (_ambulance.Unit.InUse || _police.Unit.InUse)
                {
                    TickDepartures(Time.time);   // a unit still driving away from a scene that has ended
                }
                if (Time.time < _nextCheck)
                {
                    return;
                }
                _nextCheck = Time.time + CheckInterval;
                // The roll runs only while the road itself is not the source (Traffic/Reckless), and never indoors.
                if (Settings.TrafficAccidents && !RecklessDrivers.IsSource && ModState.Loaded && TrafficTools.Ready && LifeEvents.IsSafeForEvent()
                    && !Wiring.Scenes.Near(PlayerHelper.GetPosition(), Wiring.Scenes.Beside, Wiring.Scene.Accident)   // no other scene, no building site beside it
                    && Random.value < StreetRules.Due(Settings.AccidentFrequency, Events.Weather.AccidentFactor, TimeHelper.CurrentHour))
                {
                    TryStageAccident();
                }
                return;
            }

            // Entering a building or fast-forwarding shuts traffic down and wipes it.
            if (!TrafficTools.Ready)
            {
                Clear(resumeTraffic: false, announce: false, why: "traffic stopped (indoors, menu or fast-forward)");
                return;
            }
            // Pooled cars can be recycled under us; without its wrecks there is no scene.
            if (IsAnyWreckLost())
            {
                Clear(resumeTraffic: true, announce: false, why: "a wrecked car was taken back by the traffic system");
                return;
            }

            float now = Time.time;
            if (_phase == Phase.Tumbling)
            {
                TickTumble(now);
                return;
            }
            if (_phase == Phase.Shunting)
            {
                TickShunt(now);
                return;
            }

            CheckPlayerHelp();
            UpdateMarker();
            TickAttendance(_ambulance, now);
            TickAttendance(_police, now);
            if (_chase == Chase.Running)
            {
                TickChase(now);
            }
            _people.Tick(now);
            if (!_seatsEmptied && _people.DriversStepped)
            {
                _seatsEmptied = true;
                for (int i = 0; i < _wrecks.Count; i++)
                {
                    // The cast's two once that driver is out. A further car (not in a minor crash) has nobody to play
                    // its driver, so its cab is shown empty too.
                    Wreck wreck = _wrecks[i];
                    bool cast = wreck == _worstWreck || wreck == _otherWreck;
                    if (cast ? _people.DriverOut(wreck == _worstWreck) : _severity != Severity.Minor)
                    {
                        EmptySeat(wreck, cast);
                    }
                }
            }
            _arrest.Tick(now);
            if (now >= _nextSweep)
            {
                _nextSweep = now + 3f;
                SweepAmbulances();
            }
            if (_cordon.Tick(now))
            {
                _people.WaveTrafficOn();
            }

            // Never end on someone still in the road while the ambulance comes or works: the deadline moves on, up to the hard limit.
            if (now >= _deadline && now < _hardLimit && StillAttending())
            {
                _deadline = Mathf.Min(now + 12f, _hardLimit);
                if (!_extended)
                {
                    _extended = true;
                    Logger?.Info("Accident: the casualty is still being seen to; the scene runs on past its time.");
                }
            }

            bool everyoneDone = _attended && _ambulance.Finished && _police.Finished && now >= _lingerUntil;
            if (now >= _deadline || everyoneDone)
            {
                Clear(resumeTraffic: true, announce: true, why: now >= _deadline ? "deadline" : "everyone has left");
            }
        }

        // Someone is lying in the road, and the ambulance is still coming or its crew is still out with them.
        private bool StillAttending()
        {
            Stage stage = _ambulance.Stage;
            bool coming = stage == Stage.Waiting || stage == Stage.EnRoute;
            bool working = stage == Stage.OnScene && !_people.CrewAboard(false);
            // A crew out with its stretcher attends until it is back aboard.
            return ((coming || working) && (_people.PatientDown || (_casualty != null && _casualty.Present))) || (working && _people.CrewOut);
        }

        private bool IsAnyWreckLost()
        {
            for (int i = 0; i < _wrecks.Count; i++)
            {
                if (!TrafficTools.IsHeld(_wrecks[i].Car))
                {
                    return true;
                }
            }
            return false;
        }

        private static readonly int HotRecheck = HotSpots.Slot("Traffic: TrafficAccidents.Recheck");
        // Once after every scene, outdoors with traffic running: any road point still closed, or car still held, is put right.
        private void Recheck()
        {
            using var hotSpot = HotSpots.Time(HotRecheck);
            _recheck = false;
            int points = _cordon.Recheck();
            int cars = 0;
            for (int i = 0; i < _released.Count; i++)
            {
                VehicleComponent car = _released[i];
                if (car != null && car.presetPath == null && TrafficTools.IsHeld(car))
                {
                    TrafficTools.Remove(car, "the accident scene: still held after it ended");
                    cars++;
                }
            }
            _released.Clear();
            if (points > 0 || cars > 0)
            {
                Logger?.Warn("Accident: after the last scene " + points + " road point(s) were still closed and " + cars
                             + " car(s) still held out of traffic; the points are open again and the cars were taken off the road.");
            }
        }

        /// <summary>Ends whatever scene is running, at once and without a message.</summary>
        public static void EndActive()
        {
            if (Instance != null && Instance._phase != Phase.None)
            {
                Instance.Clear(resumeTraffic: true, announce: false, why: "another incident took over");
            }
        }

        private static readonly int HotStage = HotSpots.Slot("Traffic: TrafficAccidents.StageCasualty");
        /// <summary>Starts a scene round someone lying in the road, with an ambulance and a patrol car. False if a scene runs already or traffic does not.</summary>
        /// <param name="scene">Where the marker points and the vehicles head for.</param>
        /// <param name="car">The traffic car that hit them, held with its hazards on until the scene clears; or null.</param>
        /// <param name="byPlayer">The player did it, so walking up earns no thanks.</param>
        /// <param name="asItStands">The car has just stopped there in view: held exactly as it stands, not turned askew.</param>
        public bool StageCasualty(ICasualty casualty, Vector3 scene, VehicleComponent car, bool fatal, bool byPlayer, bool asItStands = false)
        {
            using var hotSpot = HotSpots.Time(HotStage);
            try
            {
                if (_lacking || _phase != Phase.None || casualty == null || !ModState.Loaded || !TrafficTools.Ready)
                {
                    return false;
                }
                if (_recheck)
                {
                    Recheck();
                }
                float now = Time.time;
                _severity = fatal ? Severity.Severe : Severity.Moderate;   // decides how long the crews stay
                _scene = scene;
                _origin = scene;
                _playerHelped = false;
                _attended = false;
                _fled = false;
                _lingerUntil = 0f;
                _chase = Chase.None;
                _casualty = casualty;
                _casualtyScene = true;
                _fatal = fatal;
                _thanksForStopping = !byPlayer;
                _markerLabel = "reallife_pedhit_marker".GetLocalization();

                if (car != null)
                {
                    // The driver stood on the brakes: the car stops where it is, a little askew if there is room.
                    Transform body = car.transform;
                    _heading = body.rotation;
                    _involved.Clear();
                    _involved.Add(car.rb);
                    Quaternion askew = _heading * Quaternion.Euler(0f, Random.Range(8f, 20f) * (Random.value < 0.5f ? -1f : 1f), 0f);
                    _leadWreck = Seize(car);
                    Pin(_leadWreck, body.position, !asItStands && IsPoseFree(car, body.position, askew) ? askew : _heading);
                    AddHazards(_leadWreck);
                    _leadWreck.Towed = false;   // it drives off when the scene clears
                    _worstWreck = _leadWreck;   // its driver gets out and goes to whoever they hit
                }
                else
                {
                    _involved.Clear();
                    _heading = Quaternion.identity;   // no car to take the road's direction from: the cordon asks the road
                }

                _deadline = now + Mathf.Min(Random.Range(120f, 165f), MaxSceneTime);
                _single = false;
                _people.BeginCasualty(casualty, _scene, car, fatal, now);
                if (car != null)
                {
                    _people.AtFault(worst: true);   // the driver who knocked them down and stayed: questioned and ticketed
                }
                PlanResponse(now, true, Settings.Police, now + Random.Range(4f, 10f), now + Random.Range(3f, 14f), casualty, shut: true);
                News.Report(NewsKind.PedestrianHit, _scene, null, fatal ? 1 : 0);
                Logger?.Info($"Accident: pedestrian down ({(fatal ? "fatal" : "injured")}{(car != null ? ", traffic car held" : "")}), clears within {_deadline - now:F0} s.");
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Clear(resumeTraffic: true, announce: false);
                return false;
            }
        }

        private void CollectCasualty()
        {
            ICasualty casualty = _casualty;
            _casualty = null;
            if (casualty == null)
            {
                return;
            }
            try
            {
                casualty.TakeAway();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Called by the 911 contact: sends whoever is still waiting to be dispatched right away.</summary>
        public bool ReportAccident()
        {
            if (_ambulance.Stage == Stage.Waiting)
            {
                _ambulance.At = 0f;
            }
            if (_police.Stage == Stage.Waiting)
            {
                _police.At = 0f;
            }
            return _phase != Phase.None;
        }

        private void CheckPlayerHelp()
        {
            if (_playerHelped || !_thanksForStopping || PlayerHelper.IsUsingVehicle
                || (PlayerHelper.GetPosition() - _scene).sqrMagnitude > HelpDistance * HelpDistance)
            {
                return;
            }
            _playerHelped = true;
            ModState.AddModifier(ModState.Helped);
            if (_casualtyScene)
            {
                Notifications.Show(NotificationType.Success, "reallife_pedhit_helped", null, 8f);
            }
            else
            {
                // Thanked for what is actually there: one driver or several, and nobody is "okay" in a severe crash.
                Notifications.Show(NotificationType.Success, _severity == Severity.Severe ? "reallife_accident_helped_severe"
                    : _single ? "reallife_accident_helped_single" : "reallife_accident_helped", null, 8f);
            }
        }

        /// <summary>A notification about other people's accident, told only while the option for them is on.</summary>
        public static void Notice(NotificationType type, string key, float seconds, string group = "reallife_accident")
        {
            if (Settings.AccidentNotices)
            {
                Notifications.Show(type, key, null, seconds, group);
            }
        }

        private void UpdateMarker()
        {
            if (!Settings.AccidentMarker)
            {
                _mark.Hide();
                return;
            }
            if (_markerLabel == null)
            {
                _markerLabel = "reallife_accident_marker".GetLocalization();
            }
            _mark.Until = _hardLimit + GameUi.GuideMarkRules.SceneGrace;   // the latest the scene can last: the watch takes the marker off past it
            _mark.Show(_scene, _markerLabel);
        }

        private static readonly int HotClear = HotSpots.Slot("Traffic: TrafficAccidents.Clear");
        // AI cars are pooled and reused, so everything added to them has to come off again.
        private void Clear(bool resumeTraffic, bool announce, string why = "other")
        {
            using var hotSpot = HotSpots.Time(HotClear);
            if (_phase != Phase.None)
            {
                Logger?.Info("Accident: scene ended (" + why + ").");
            }
            string ending = null;   // said once the wrecks are seen to: a tow truck coming for one is said instead
            if (announce && _phase == Phase.Active)
            {
                if (_casualtyScene)
                {
                    ending = _fatal ? "reallife_pedhit_cleared_dead" : "reallife_pedhit_cleared_injured";
                }
                else if (_fled)
                {
                    ending = "reallife_accident_cleared_hitrun";
                }
                else
                {
                    string[] endings = _single ? (_severity == Severity.Minor ? SingleMinorEndings : _severity == Severity.Moderate ? SingleModerateEndings : SingleSevereEndings)
                        : _severity == Severity.Minor ? MinorEndings
                        : _severity == Severity.Moderate ? ModerateEndings
                        : SevereEndings;
                    ending = endings[Random.Range(0, endings.Length)];
                }
            }

            if (_phase == Phase.Active && _attended && _ambulance != null)
            {
                // The road is given back only with the ambulance beyond the scene or off the road and the patrol car leaving.
                bool ambulanceAway = _ambulance.Past || !_ambulance.Unit.InUse;
                bool patrolAway = _police.Past || !_police.Unit.InUse;
                DepartureCheck("road reopened", ambulanceAway && !_police.Backing,
                    "the ambulance " + (!_ambulance.Unit.InUse ? "is off the road" : _ambulance.Past ? "is beyond the scene" : "is STILL at the scene (" + _ambulance.Stage + ")")
                    + "; the patrol car " + (patrolAway ? "is beyond the scene or off the road" : _police.Backing ? "is still backing out of the ambulance's way"
                        : _police.Stage == Stage.Ready ? "stands in its lane with its officers aboard and drives off now, with the traffic" : "is still at the scene (" + _police.Stage + ")")
                    + "; ended by: " + why);
            }

            // The marker and the road first, whatever goes wrong below.
            _mark.Hide();
            _cordon.Open();
            SceneObstacles.Clear();

            _people.Clear();
            _arrest.Clear();
            CollectCasualty();   // nobody is left lying in the road once the scene is over
            _casualtyScene = false;
            _single = false;
            _thanksForStopping = true;
            _markerLabel = null;   // the next scene names its own marker

            bool trafficUp = TrafficManager.HasInstance && TrafficManager.IsInitialized;
            int towing = 0, towMinutes = 0;
            foreach (Wreck wreck in _wrecks)
            {
                // A wreck too damaged to drive off goes to the tow truck (Tow/TowService) where it can; otherwise it is removed below.
                TowService tow = TowService.Instance;
                bool forTruck = wreck.Towed && trafficUp && resumeTraffic && !_closing && why != "error" && TrafficTools.Ready
                                && TrafficTools.IsHeld(wreck.Car) && tow != null && tow.Accepts(wreck.Car);
                if (forTruck)
                {
                    _seatScratch.Clear();
                    _seatScratch.AddRange(wreck.Seat);
                }
                Unpin(wreck);
                if (forTruck && tow.Take(wreck.Car, _seatScratch, out int minutes))
                {
                    towing++;
                    if (towMinutes == 0)
                    {
                        towMinutes = minutes;
                    }
                    _released.Add(wreck.Car);
                    continue;
                }
                // A wreck that was already recycled is ordinary traffic again and is left alone.
                if (trafficUp && TrafficTools.IsHeld(wreck.Car))
                {
                    // Traffic shut down round it (indoors, fast-forward): the game cleared every car but ours, skipped for its
                    // scripted route. Left here it would stand ignored in the road, so it is taken off like the rest.
                    if (wreck.Towed || !resumeTraffic || !TrafficTools.Ready)
                    {
                        TrafficTools.Remove(wreck.Car, wreck.Towed ? "the accident scene ended: towed" : "the accident scene ended: traffic has shut down");
                    }
                    else
                    {
                        TrafficTools.ReturnToTraffic(wreck.Car, "the accident scene ended: it drives off");
                    }
                }
                if (wreck.Car != null)
                {
                    _released.Add(wreck.Car);
                }
            }
            _wrecks.Clear();
            _seatScratch.Clear();
            if (towing > 0 && ending != null)
            {
                TowService.TellComing(towMinutes, towing);   // no minutes: it comes when it has finished with the wreck before
            }
            else if (ending != null)
            {
                Notice(NotificationType.Info, ending, 7f);
            }
            _recheck = true;   // looked at again on the next tick out of doors (Recheck)
            _leadWreck = null;
            _worstWreck = null;
            _otherWreck = null;
            _seatsEmptied = false;
            _conesIn = false;
            _exitWrecks.Clear();

            // Units still out are never handed back standing (SendOff). A suspect already stopped is taken away.
            if (_ambulance != null)
            {
                bool drive = why != "error";
                SendOff(_ambulance, resumeTraffic, drive);
                SendOff(_police, resumeTraffic, drive);
                _suspect.Release(resumeTraffic, remove: _pursuit.Now == RoadChase.Step.Held);
            }
            _chase = Chase.None;
            _pursuit.Reset();
            _inPlace = false;
            _causeKey = null;
            _phase = Phase.None;
            _nextCheck = Time.time + CheckInterval * 3f;
        }
    }
}
