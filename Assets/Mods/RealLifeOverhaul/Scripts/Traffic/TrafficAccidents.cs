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
    /// Stages traffic accidents with the game's AI cars and runs the scene. Each accident rolls
    /// a severity, which decides how many cars are involved, how they end up (shunted, spun,
    /// or, for a car or an SUV, rolled onto its side or roof), the smoke, fire and debris, who attends (police always, an ambulance for
    /// the worse ones), how long the scene lasts and how it is cleared. Some are hit-and-runs:
    /// one driver bolts when the patrol car turns up, and is chased (the chase itself is RoadChase). Every scene is planned to be gone
    /// within three minutes, and runs past that (five at most) only for a casualty still being seen to.
    /// Where an accident comes from: with "Reckless drivers and natural accidents" on, from the road itself
    /// (Traffic/Reckless: a car that really met another is handed in at BeginAt, as it stands, and everything
    /// from there on is the same scene); with it off, or if that feature has switched itself off, from the
    /// roll this class has always made every 20 seconds, which places two cars as wrecks out of view.
    /// This class decides what happens and when; ResponseUnit drives the vehicles,
    /// SceneParking says where they may stand, SceneCordon closes and tapes off the road,
    /// TrafficEffects builds what the vehicles and wrecks wear, and AccidentCast and
    /// RoadsideStop put the people on the road (drivers, crews, and an arrest at the roadside for a
    /// driver who ran and was caught: a driver who stays is questioned and ticketed, never arrested). Wrecked and parked
    /// cars are frozen in place (kinematic), so holding a scene costs nothing per frame beyond
    /// a few timers and the on-screen marker.
    /// </summary>
    public class TrafficAccidents : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("traffic", "TrafficAccidents", typeof(TrafficAccidents));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public enum Severity { Minor, Moderate, Severe }

        private const float CheckInterval = 20f;
        private const float MinDistance = 45f;
        private const float MaxDistance = 140f;
        private const float MovingSpeed = 10f;      // km/h: slower than this a car is not "on the move"
        private const float HelpDistance = 9f;
        private const float PartnerDistance = 40f;  // the second car of a collision is looked for this near the first
        private const int OpenRoadPoints = 7;       // road points each way that must be plain lane for a stretch to count as open road
        private const float MaxSceneTime = 175f;   // every scene is planned to clear inside three minutes
        private const float HardSceneTime = 300f;  // ...and may run on to this, but only while a crew is still seeing to someone lying in the road

        private const float AmbulanceRouteLength = 170f;
        private const float AmbulanceStopDistance = 13f;   // used only where the scene has no lane to stand in (see Dispatch)
        private const float AmbulanceBack = 10f;        // metres from the nearest wreck (or whoever lies there) to the middle of the ambulance: its nose 6 to 7 m from them, outside the tape
        private const float PatrolBack = 21f;           // ...and of the patrol car when it stands behind the ambulance
        private const float PatrolBackAlone = 10f;      // ...or has that end of the scene to itself
        private const float CrewWalkLimit = 150f;       // a unit stopped further from its place than this keeps its crew aboard
        private const float BlockedCheck = 0.5f;        // seconds between looks at what is holding a unit up
        private const float HeldBeforeMoving = 1.5f;    // seconds a unit stands behind traffic before that traffic is moved on (out of sight)
        private const float HeldBeforeMovingSeen = 4f;  // ...or in sight
        private const float BoxedIn = 20f;              // seconds a unit may stand with its crew aboard and free to go before it is taken off the road instead
        private const float BoxedInByPeople = 45f;      // ...or behind a person, who will move (it never forces through)
        private const float BoxedSeen = 10f;            // ...and the seconds more it is given to be out of view first
        private const float ClearInView = 5f;           // seconds a wreck in a loaded ambulance's lane is given to be out of view before it is taken off anyway
        private const float MovingOff = 1.5f;           // m/s: faster than this a unit is moving along its lane, and traffic may have it back
        private const float ExitMargin = 0.35f;         // metres each side of its body that the lane it leaves by must be clear
        private const float ExitPace = 7f;              // m/s through where the scene is
        private const float LaneAcross = 3.3f;          // one lane sideways
        private const float StandBackMost = 18f;        // metres back along its own lane a clear place to stand is looked for
        private const float StandStep = 1.5f;
        private const float StandMargin = 0.3f;         // metres all round its body that the place it stands must be clear of everything solid
        private const float RearDoorRoom = 6.5f;        // metres behind the ambulance kept free of cones: its crew turns the stretcher round there
        private const float TapeDownFor = 4f;           // seconds (at least) between the tape coming down and the scene clearing, the patrol car driving off with it
        private const float CrewWalk = 14f;             // held up with no more than this much of its route left, a unit stops there for good; further out it waits while its way is cleared
        private const float ParkLookDistance = 48f;     // metres from the scene at which a unit picks where to pull up
        private const float LeaveDistance = 55f;        // a departing unit is driven this far from the scene before traffic has it back
        private const float LeaveAllowance = 22f;       // ...or for this many seconds
        private const float DriveAllowance = 60f;       // seconds a unit may take to reach the scene

        private const float TumbleChance = 0.4f;        // of severe accidents
        private const float TumbleReach = 14f;          // metres of clear road the roll needs ahead of the car
        private const float TumbleRadius = 16f;         // the car is frozen if it gets this far from where it started
        private const float TumbleMaxTime = 3.2f;
        private const float ShuntLeast = 1f;            // m/s: cars that met slower than this stand where they met
        private const float ShuntMaxTime = 1.6f;        // seconds physics has the cars of a collision on the road before they are frozen
        private const float ShuntRestFrom = 0.25f;      // ...and the least it has them
        private const float ShuntBraking = 9f;          // m/s2 they lose speed with (locked wheels on a dry road)
        private const float ShuntRadius = 12f;          // metres from where they met beyond which a car is frozen at once
        private const float ShuntBounce = 0.2f;         // of the speed they met at that they part with again
        private const float ShuntDamping = 11f;         // per second on the vertical speed (EmergencyDriver's own figure: the traffic system's springs have no damper for a car it ignores)
        private const float TumblePlayerDistance = 35f;
        private const float RollLongest = 5.6f;         // metres: nothing longer than this rolls (vans, trucks, buses, limousines)
        private const float RollTallest = 2.1f;         // ...nor anything taller

        private const float HitAndRunChance = 0.25f;    // of accidents with two or more cars that did not roll
        private const float FleeRouteLength = 650f;
        private const float FleeRouteMinimum = 250f;
        private const float FleeTriggerDistance = 45f;  // the suspect bolts when the patrol car gets this close
        private const float SuspectSpeed = 12.5f;       // m/s
        private const float ChaseSpeed = 15.5f;         // m/s: the patrol car is a little faster
        private const float ChaseGap = 9f;

        private const float CrewGrace = 14f;            // seconds a unit waits past its time for a crew still walking back
        private const float StretcherGrace = 40f;       // ...and for paramedics bringing someone back on the stretcher

        private enum Phase { None, Tumbling, Shunting, Active }

        // Where one attending unit is in its visit to the scene.
        // Ready: its crew is aboard and it stands lit, for seconds only: the patrol car until the scene clears round it
        // (it is the last to go), the ambulance while a wreck in its lane is taken off; then it drives away forwards.
        // Leaving: driving away under the mod, which may go on after the scene has ended: traffic has it back only
        // while it is moving along a lane.
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

        /// <summary>
        /// True while an accident the player did not cause is on the street and nobody has helped yet, and where it is
        /// (Wiring/StandingLinks: walking past it in front of others is noticed). Two field reads; asked a few times a minute.
        /// </summary>
        public static bool Needing(out Vector3 where)
        {
            TrafficAccidents a = Instance;
            where = a != null ? a._scene : Vector3.zero;
            return a != null && a.HasActiveAccident && a._thanksForStopping && !a._playerHelped;
        }

        private sealed class Wreck
        {
            public VehicleComponent Car;
            public AiCarRescueCheck RescueCheck;
            public bool WasKinematic;
            public RigidbodyConstraints Constraints;
            public bool Towed;                      // removed when the scene clears, instead of driving off
            public readonly List<Renderer> Seat = new List<Renderer>(4);   // the driver the vehicle shows in its cab, hidden while that driver is out on the road
            public readonly List<GameObject> Effects = new List<GameObject>();
            public readonly List<int> Preset = new List<int>();   // marks the car as ours; see Seize
        }

        private sealed class Attendance
        {
            public readonly ResponseUnit Unit;
            public readonly bool Police;
            public Stage Stage;
            public float At;                        // when the current stage ends
            public float StopDistance;
            public bool Retried;
            public bool ParkChosen;                 // where to pull up has been looked for
            public bool Recalled;                   // the crew has been called back to the vehicle
            public bool FarEnd;                     // it comes up to the far end of the scene, by the lane running the other way
            public bool AtStand;                    // it is driving to (or standing at) a place the cordon gave it
            public float NextCheck;                 // when what is holding it up is next looked at
            public int MovedOn;                     // traffic cars taken out of its way
            public bool Waits;                      // crew aboard, but it goes only when the scene clears round it (the patrol car)
            public bool Through;                    // it is driving away through where the scene is: at a careful pace until it is beyond
            public float Still;                     // since when it has stood with its crew aboard and free to go; 0 while it is moving
            public Vector3 From;                    // where it set off from when it left
            public bool OwnLane;                    // it leaves through the scene by its own lane, checked for it: it does not pull out to pass there
            public bool Backing;                    // crew aboard, backing along its own lane out of the other unit's way
            public Vector3 BackFrom;                // ...from here
            public float BackUntil;                 // ...and for no longer than this
            public bool WaitedBefore;               // ...and whether it was waiting for the scene to clear before that
            public int GaveWay;                     // times the other unit was moved out of this one's way as it left
            public float SetOffAt;                  // when it set off to leave
            public bool SaidHeld;                   // it has been said that it is held up in view and is not taken off the road there

            public Attendance(ResponseUnit unit, bool police)
            {
                Unit = unit;
                Police = police;
            }

            /// <summary>Nothing more will happen with this unit at the scene: it never came, or it has gone.</summary>
            public bool Finished => Stage == Stage.Idle || Stage == Stage.Done || (Stage == Stage.Ready && Waits);

            /// <summary>It has left the scene's working area, or is on its way out of it.</summary>
            public bool Going => Finished || Stage == Stage.Leaving || Stage == Stage.Ready;

            /// <summary>It has driven beyond the scene (or was never at it): nothing of it is left for the other unit to wait for or drive round.</summary>
            public bool Past => Stage == Stage.Idle || Stage == Stage.Done || (Stage == Stage.Leaving && !Through);
        }

        private readonly HashSet<Rigidbody> _involved = new HashSet<Rigidbody>();
        private readonly List<Wreck> _wrecks = new List<Wreck>();
        private readonly List<VehicleComponent> _others = new List<VehicleComponent>();
        private readonly List<Waypoint> _routeScratch = new List<Waypoint>();
        private readonly List<Vector3> _fleePath = new List<Vector3>(256);
        private readonly List<Footprint> _footprints = new List<Footprint>(4);
        private readonly List<Renderer> _seatScratch = new List<Renderer>(4);   // a wreck's seat, handed with it to the tow truck

        private Phase _phase = Phase.None;
        private bool _recheck;       // a scene has ended: the road and its cars are looked at once more, out of doors (Recheck)
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
        private readonly RealLifeOverhaul.GameUi.GuideMark _mark = new RealLifeOverhaul.GameUi.GuideMark();

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

        // Hit-and-run.
        private ResponseUnit _suspect;
        private Waypoint _fleeAnchor;
        private Chase _chase = Chase.None;
        private readonly RoadChase _pursuit = new RoadChase();

        // A collision that happened on the road (BeginAt): the cars stay exactly where they met, and the
        // scene is announced by what caused it.
        private bool _inPlace;
        private string _causeKey;

        // The people. Both are told what happens and ticked from here; neither runs by itself.
        private readonly AccidentCast _people = new AccidentCast();
        private readonly RoadsideStop _arrest = new RoadsideStop();

        // Scene control: the road closed round the wrecks at once, taped off when the police arrive.
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
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
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
                // Clear leaves no scene behind, so a fault cannot repeat every frame. One on a game that lacks something
                // the accidents name would come back at every look: they stop for the session, the watch on the traffic goes on.
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
                // The roll is the source of accidents only while the road itself is not (Traffic/Reckless). It has never
                // run with the player indoors (traffic is switched off there), so there is nothing else for it to cover.
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
                // Whoever got out is not also still sitting in the cab: a vehicle that shows its driver has the seat emptied.
                _seatsEmptied = true;
                for (int i = 0; i < _wrecks.Count; i++)
                {
                    // The two whose drivers the cast plays: only if that driver really is out. Any further vehicle
                    // of a crash the road was closed for has nobody to play its driver, and is not left with
                    // someone sitting at the wheel of a wreck for the whole scene: its cab is shown empty too.
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

            // A scene never ends on someone still lying in the road with their ambulance on its way or its
            // crew at work: the deadline is put back a little at a time, up to the hard limit.
            if (now >= _deadline && now < _hardLimit && StillAttending())
            {
                _deadline = Mathf.Min(now + 12f, _hardLimit);
                if (!_extended)
                {
                    _extended = true;
                    Logger?.Info("Accident: the casualty is still being seen to; the scene runs on past its time.");
                }
            }

            // The scene ends when everyone who came has left, or at the deadline whatever is going on.
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
            // (A crew that is out with its stretcher is still attending until it is back aboard, whether the casualty is on the road, on the bed or covered.)
            return ((coming || working) && (_people.PatientDown || (_casualty != null && _casualty.Present))) || (working && _people.CrewOut);
        }

        private const float CrewLastCall = 6f;   // seconds before the scene's hard limit at which the ambulance goes whoever is still out (the scene is about to end round them)

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

        /// <summary>Options-menu test buttons: stage an accident right away, of a chosen severity or a random one.</summary>
        public void StageAccidentNow(Severity? severity = null)
        {
            try
            {
                TestRefusal why = _lacking ? TestRefusal.FeatureOff : TestRefusals.Street();
                if (why == TestRefusal.None && _phase != Phase.None)
                {
                    why = TestRefusal.SceneActive;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Accident", why);
                    return;
                }
                if (_recheck)
                {
                    Recheck();
                }
                StreetCrime.EndActive();   // one incident at a time, and the tester asked for this one
                _forcedSeverity = severity;
                // As the mod itself stages one first; then nearer and further; then any car that is moving at all.
                for (int pass = 0; pass < TestSearch.Length && _phase == Phase.None; pass++)
                {
                    _searchFrom = TestSearch[pass].from;
                    _searchTo = TestSearch[pass].to;
                    _searchSpeed = TestSearch[pass].speed;
                    TryStageAccident();
                }
                ResetSearch();
                if (_phase == Phase.None)
                {
                    TestRefusals.Tell(Logger, "Accident", TestRefusal.NoMovingCar, (int)TestSearch[TestSearch.Length - 1].to, TrafficCensus(PlayerHelper.GetPosition()));
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ResetSearch();
                Clear(resumeTraffic: true, announce: false);
            }
        }

        private static readonly (float from, float to, float speed)[] TestSearch =
        {
            (MinDistance, MaxDistance, MovingSpeed), (30f, 200f, MovingSpeed), (18f, 250f, 4f)
        };

        /// <summary>
        /// For the other test buttons that need a traffic car: <see cref="FindMovingCar(Vector3)"/>, then
        /// the same wider searches a test accident makes. Null with how far was looked and what the
        /// traffic pool held (for the log) if there is none.
        /// </summary>
        public VehicleComponent FindMovingCarWide(Vector3 player, out int within, out string census)
        {
            VehicleComponent car = null;
            for (int pass = 0; pass < TestSearch.Length && car == null; pass++)
            {
                _searchFrom = TestSearch[pass].from;
                _searchTo = TestSearch[pass].to;
                _searchSpeed = TestSearch[pass].speed;
                car = FindMovingCar(player, openRoadOnly: false);
            }
            Severity? forced = _forcedSeverity;
            ResetSearch();
            _forcedSeverity = forced;
            within = (int)TestSearch[TestSearch.Length - 1].to;
            census = car == null ? TrafficCensus(player) : null;
            return car;
        }

        private void ResetSearch()
        {
            _forcedSeverity = null;
            _searchFrom = MinDistance;
            _searchTo = MaxDistance;
            _searchSpeed = MovingSpeed;
        }

        // What the traffic pool looked like when no car could be found: for the log, on a failed test only.
        private static string TrafficCensus(Vector3 player)
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            int onRoad = 0, usable = 0, moving = 0;
            float nearest = float.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car == null || !car.gameObject.activeInHierarchy)
                {
                    continue;
                }
                onRoad++;
                if (!IsUsable(car))
                {
                    continue;
                }
                usable++;
                if (car.GetCurrentSpeed() >= 4f)
                {
                    moving++;
                    nearest = Mathf.Min(nearest, Vector3.Distance(car.transform.position, player));
                }
            }
            return "traffic pool " + pool.Count + ", on the road " + onRoad + ", ordinary cars free to use " + usable + ", of those moving " + moving
                   + (moving > 0 ? ", the nearest " + nearest.ToString("F0") + " m away" : string.Empty);
        }

        private static readonly int HotRecheck = HotSpots.Slot("Traffic: TrafficAccidents.Recheck");   // (inside its Update: names the stage that took a long frame)
        // Once after every scene, on the first tick out of doors with traffic running: the road points the
        // cordon switched off must all be open and none of the scene's cars may still be held out of traffic.
        // Whatever is not as it was found is put right, and said in the log.
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

        private static readonly int HotStage = HotSpots.Slot("Traffic: TrafficAccidents.StageCasualty");   // (inside its Update: names the stage that took a long frame)
        /// <summary>
        /// Starts a scene around someone lying in the road: an ambulance and (with police on) a
        /// patrol car are sent, their crews attend, and the casualty is told when the ambulance
        /// leaves with them or the scene ends, whichever comes first. Everything else about the
        /// scene (marker, timetable, clearing within three minutes) is as for a car accident.
        /// </summary>
        /// <param name="scene">Where the marker points and the vehicles head for.</param>
        /// <param name="car">The traffic car that hit them, which stops here with its hazards on until the scene clears; or null.</param>
        /// <param name="byPlayer">The player did it, so walking up to the scene earns no thanks.</param>
        /// <param name="asItStands">The car has really just stopped there, in view (a driver who hit somebody and stayed): it is held exactly as it stands, not turned askew.</param>
        /// <returns>False if a scene is already running or traffic is not.</returns>
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
                    AddEffect(_leadWreck, TrafficEffects.CreateFlasher(car, "RealLifeOverhaul Hazard", TrafficEffects.BeaconHeight(car),
                        new Color(1f, 0.55f, 0.05f), new Color(1f, 0.55f, 0.05f), 0.5f));
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

        // The ambulance is going: whoever was lying there goes with it.
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

        // ---------------------------------------------------------------- staging

        // Set only while a test button is staging an accident of a chosen severity.
        private Severity? _forcedSeverity;

        private static Severity RollSeverity()
        {
            float roll = Random.value;
            return roll < 0.40f ? Severity.Minor : roll < 0.78f ? Severity.Moderate : Severity.Severe;
        }

        private static readonly int HotBegin = HotSpots.Slot("Traffic: TrafficAccidents.BeginAt");   // (inside its Update: names the stage that took a long frame)
        /// <summary>
        /// A collision that has just happened on the road, in front of whoever is looking: the scene starts
        /// round the two cars where they met. Nothing is moved into place by hand (the old staging puts its
        /// second car behind its first, out of view; here both are in view), and the severity is what the
        /// speed they met at makes it: low a bump, middling a crash, high a serious one with the same roll,
        /// smoke and fire rules as any other. From there on it is the scene every accident is: drivers out,
        /// wrecks dressed, the road closed, ambulance and patrol car, a driver who may bolt.
        /// The cars are not pinned dead at the moment of contact. Given the velocities they met with, physics
        /// has them for a moment first (Shunting, as a severe roll has its car): they share their momentum, part
        /// a little, slide to rest on locked wheels and are frozen there, upright. That is bounded in time
        /// (1.6 s) and in distance (12 m), and the scene is measured only after it: the place, the heading, the
        /// road to close, the parking for the units and the cordon all start from where the cars came to rest.
        /// Every way out of a scene (Clear) gives a car in that state back exactly as it gives back a pinned one.
        /// </summary>
        /// <param name="first">The car that caused it. It may be one the caller was driving a moment ago (ResponseUnit.HandOver): any car still on the road will do.</param>
        /// <param name="second">The car it met, if that is an ordinary traffic car; null for a crash on its own (a wall, a parked car, the player's car).</param>
        /// <param name="closingSpeed">How fast the two were closing, in m/s.</param>
        /// <param name="noticeKey">What is said about it, by its cause; null for the usual sentence of its severity.</param>
        /// <param name="firstVelocity">The velocity the first car has just after it struck: what it had, for a car that met another or left its lane; a little of it back the way it came, for one that met something that does not give. Zero: it stands where it is.</param>
        /// <param name="secondVelocity">The velocity the second car had as they met.</param>
        /// <returns>False if no scene could start (another is on, traffic is not running): the caller still has the car to let go.</returns>
        public bool BeginAt(VehicleComponent first, VehicleComponent second, float closingSpeed, string noticeKey, Vector3 firstVelocity = default, Vector3 secondVelocity = default)
        {
            using var hotSpot = HotSpots.Time(HotBegin);
            try
            {
                if (_lacking || _phase != Phase.None || first == null || !first.gameObject.activeInHierarchy || !ModState.Loaded || !TrafficTools.Ready
                    || Wiring.Scenes.AnyBut(Wiring.Scene.Accident))
                {
                    return false;
                }
                if (second != null && (second == first || !IsUsable(second)))
                {
                    second = null;   // a taxi, an ambulance, a car somebody else of the mod holds: it was hit, but it is nobody's wreck
                }
                if (_recheck)
                {
                    Recheck();
                }
                Vector3 player = PlayerHelper.GetPosition();
                _severity = (Severity)StreetRules.Severity(closingSpeed);
                _inPlace = true;
                _causeKey = noticeKey;
                Transform lead = first.transform;
                _origin = lead.position;
                _heading = lead.rotation;
                _scene = _origin;
                _side = Random.value < 0.5f ? -1f : 1f;
                _playerHelped = false;
                _attended = false;
                _fled = false;
                _lingerUntil = 0f;
                _chase = Chase.None;

                _involved.Clear();
                _involved.Add(first.rb);
                if (second != null)
                {
                    _involved.Add(second.rb);
                }
                // The roll of a severe crash, by the rule every accident has: a car or an SUV, clear road ahead, not on top of the player.
                bool tumble = _severity == Severity.Severe && Random.value < TumbleChance && Rolls(first) && CanTumble(first, player);

                _leadWreck = Seize(first);
                Wreck other = second != null ? Seize(second) : null;
                Logger?.Info($"Accident: a collision on the road, closing at {closingSpeed * 3.6f:F0} km/h: {_severity}, {_wrecks.Count} car(s), "
                             + $"{Vector3.Distance(player, _scene):F0} m away{(tumble ? ", lead car rolling" : "")}.");
                if (tumble)
                {
                    if (other != null)
                    {
                        Pin(other, second.transform.position, second.transform.rotation);
                    }
                    BeginTumble(first);
                    return true;
                }
                if (BeginShunt(first, second, firstVelocity, secondVelocity))
                {
                    return true;
                }
                if (other != null)
                {
                    Pin(other, second.transform.position, second.transform.rotation);
                }
                Pin(_leadWreck, _origin, _heading);
                CompleteStaging(tumbled: false);
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Clear(resumeTraffic: true, announce: false);
                return false;
            }
        }

        private static readonly int HotTry = HotSpots.Slot("Traffic: TrafficAccidents.TryStageAccident");   // (inside its Update: names the stage that took a long frame)
        private void TryStageAccident()
        {
            using var hotSpot = HotSpots.Time(HotTry);
            _inPlace = false;
            _causeKey = null;
            Vector3 player = PlayerHelper.GetPosition();
            List<VehicleComponent> pool = Manager.GetVehicleList();
            VehicleComponent first = FindLeadCar(player);
            if (first == null)
            {
                return;
            }

            _severity = _forcedSeverity ?? RollSeverity();
            // A collision takes two cars whenever a second can be found; only a severe one may take three.
            int wanted = _severity == Severity.Severe ? Random.Range(2, 4) : 2;

            // The nearest other cars get drawn into it.
            _others.Clear();
            while (_others.Count < wanted - 1)
            {
                VehicleComponent nearest = null;
                float bestSqr = PartnerDistance * PartnerDistance;
                foreach (VehicleComponent car in pool)
                {
                    if (car == first || _others.Contains(car) || !IsUsable(car))
                    {
                        continue;
                    }
                    float sqr = (car.transform.position - first.transform.position).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        nearest = car;
                    }
                }
                if (nearest == null)
                {
                    break;
                }
                _others.Add(nearest);
            }

            Transform lead = first.transform;
            _origin = lead.position;
            _heading = lead.rotation;
            _scene = _origin;
            _side = Random.value < 0.5f ? -1f : 1f;
            _playerHelped = false;
            _attended = false;
            _fled = false;
            _lingerUntil = 0f;
            _chase = Chase.None;

            // Bodies that belong to this accident; everything else must not be overlapped.
            _involved.Clear();
            _involved.Add(first.rb);
            foreach (VehicleComponent other in _others)
            {
                _involved.Add(other.rb);
            }

            bool tumble = _severity == Severity.Severe && Random.value < TumbleChance && Rolls(first) && CanTumble(first, player);

            // Everyone involved stops being traffic now. The others halt where they are and are
            // moved into the crash once the lead car's final position is known.
            _leadWreck = Seize(first);
            foreach (VehicleComponent other in _others)
            {
                Pin(Seize(other), other.transform.position, other.transform.rotation);
            }
            _others.Clear();

            Logger?.Info($"Accident: {_severity}, {_wrecks.Count} car(s), {Vector3.Distance(player, _scene):F0} m away{(tumble ? ", lead car rolling" : "")}.");

            if (tumble)
            {
                BeginTumble(first);
                return;
            }

            float skew = _severity == Severity.Minor ? Random.Range(6f, 14f)
                : _severity == Severity.Moderate ? Random.Range(18f, 36f)
                : Random.Range(38f, 75f);

            // Spin the lead car as far as there is room for: full, half, the other way, or not at all.
            Quaternion leadRotation = _heading;
            for (int i = 0; i < 4; i++)
            {
                float angle = (i < 2 ? skew : -skew) * (i % 2 == 0 ? 1f : 0.5f) * _side;
                Quaternion candidate = _heading * Quaternion.Euler(0f, angle, 0f);
                if (IsPoseFree(first, _origin, candidate))
                {
                    leadRotation = candidate;
                    break;
                }
            }
            Pin(_leadWreck, _origin, leadRotation);
            CompleteStaging(tumbled: false);
        }

        /// <summary>
        /// An ordinary traffic car on the move, at the distance from the player where incidents
        /// are staged (45 to 140 m), or null. The search starts at a random car so the same
        /// vehicle is not always the one picked.
        /// </summary>
        public VehicleComponent FindMovingCar(Vector3 player)
        {
            return FindMovingCar(player, openRoadOnly: false);
        }

        // The car a collision starts with: one on a plain stretch of road if there is any, so that the
        // scene has lanes to close, room for the response vehicles and no crossing through it.
        private VehicleComponent FindLeadCar(Vector3 player)
        {
            return FindMovingCar(player, openRoadOnly: true) ?? FindMovingCar(player, openRoadOnly: false);
        }

        private VehicleComponent FindMovingCar(Vector3 player, bool openRoadOnly)
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            int start = Random.Range(0, pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[(start + i) % pool.Count];
                if (!IsUsable(car) || car.GetCurrentSpeed() < _searchSpeed)
                {
                    continue;
                }
                float sqr = (car.transform.position - player).sqrMagnitude;
                if (sqr > _searchFrom * _searchFrom && sqr < _searchTo * _searchTo && (!openRoadOnly || IsOnOpenRoad(car)))
                {
                    return car;
                }
            }
            return null;
        }

        // True if the lane the car is in runs plain for a few road points both ways: no junction, no fork, no merge.
        private static bool IsOnOpenRoad(VehicleComponent car)
        {
            TrafficManager traffic = TrafficManager.Instance;
            Waypoint target = traffic.GetTargetWaypoint(car.GetIndex());
            if (target == null)
            {
                return false;
            }
            for (int way = 0; way < 2; way++)
            {
                Waypoint point = target;
                for (int step = 0; step < OpenRoadPoints && point != null; step++)
                {
                    if (point.IsInIntersection() || point.neighbors == null || point.neighbors.Count != 1 || point.prev == null || point.prev.Count != 1)
                    {
                        return false;
                    }
                    point = traffic.GetWaypoint(way == 0 ? point.neighbors[0] : point.prev[0]);
                }
            }
            return true;
        }

        // What can be rolled: a car or an SUV. A van, truck, bus or limousine thrown about only rocks on its
        // wheels and ends up leaning, which reads as a fault; those crash without leaving the ground.
        private static bool Rolls(VehicleComponent car)
        {
            if (car.vehicleType != VehicleTypes.Car || car.length > RollLongest || car.coliderHeight > RollTallest)
            {
                return false;
            }
            string name = car.prefab != null ? car.prefab.name : string.Empty;
            return name.IndexOf("Truck", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Van", StringComparison.OrdinalIgnoreCase) < 0
                   && name.IndexOf("Bus", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Limo", StringComparison.OrdinalIgnoreCase) < 0;
        }

        // A roll needs clear road ahead, and must not happen on top of the player.
        private bool CanTumble(VehicleComponent car, Vector3 player)
        {
            Vector3 forward = _heading * Vector3.forward;
            if ((_origin + forward * TumbleReach - player).sqrMagnitude < TumblePlayerDistance * TumblePlayerDistance)
            {
                return false;
            }
            for (float ahead = TumbleReach / 3f; ahead <= TumbleReach + 0.1f; ahead += TumbleReach / 3f)
            {
                if (!IsPoseFree(car, _origin + forward * ahead, _heading))
                {
                    return false;
                }
            }
            return true;
        }

        // The lead car really rolls: physics has it for a couple of seconds, then TickTumble freezes it.
        private void BeginTumble(VehicleComponent car)
        {
            Vector3 forward = _heading * Vector3.forward;
            Rigidbody body = car.rb;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.None;
            body.velocity = forward * Random.Range(9f, 13f) + Vector3.up * Random.Range(2.5f, 3.5f);
            body.angularVelocity = forward * (_side * Random.Range(4.5f, 6.5f)) + Vector3.up * Random.Range(-1f, 1f);

            _tumbleRestFrom = Time.time + 1f;
            _tumbleEnd = Time.time + TumbleMaxTime;
            _phase = Phase.Tumbling;
        }

        private void TickTumble(float now)
        {
            VehicleComponent car = _leadWreck != null ? _leadWreck.Car : null;
            if (car == null)
            {
                Clear(resumeTraffic: true, announce: false, why: "the rolling car was lost");
                return;
            }

            Rigidbody body = car.rb;
            Vector3 offset = body.position - _origin;
            bool strayed = offset.x * offset.x + offset.z * offset.z > TumbleRadius * TumbleRadius || offset.y < -3f;
            bool atRest = now >= _tumbleRestFrom && body.velocity.sqrMagnitude < 0.5f && body.angularVelocity.sqrMagnitude < 0.5f;
            if (!strayed && !atRest && now < _tumbleEnd)
            {
                return;
            }

            // Wherever it ended up is where the crash is: squarely on its side, its roof or its wheels, and on the road.
            string how = RestRolled(_leadWreck);
            Vector3 rest = car.transform.position;
            _scene = new Vector3(rest.x, _origin.y, rest.z);
            Logger?.Info($"Accident: the rolling car came to rest {how}, {Vector3.Distance(rest, _origin):F0} m on.");
            CompleteStaging(tumbled: true);
        }

        // The cars of a collision on the road keep moving for a moment: they share their momentum (two of them), part
        // with a fifth of the speed they met at, and turn a little. False (nothing changed) if they met too slowly
        // for any of that to show, or a body cannot be moved by physics.
        private bool BeginShunt(VehicleComponent first, VehicleComponent second, Vector3 firstVelocity, Vector3 secondVelocity)
        {
            firstVelocity.y = 0f;
            secondVelocity.y = 0f;
            Rigidbody lead = first.rb;
            Rigidbody struck = second != null ? second.rb : null;
            if (lead == null || lead.isKinematic || (struck != null && struck.isKinematic)
                || (firstVelocity.sqrMagnitude < ShuntLeast * ShuntLeast && secondVelocity.sqrMagnitude < ShuntLeast * ShuntLeast))
            {
                return false;
            }
            float turn = Mathf.Clamp((firstVelocity - secondVelocity).magnitude * 0.06f, 0.15f, 1.1f) * _side;
            if (struck != null)
            {
                float total = Mathf.Max(1f, lead.mass + struck.mass);
                Vector3 together = (firstVelocity * lead.mass + secondVelocity * struck.mass) / total;
                Vector3 closing = firstVelocity - secondVelocity;
                lead.velocity = together - closing * (ShuntBounce * struck.mass / total);
                struck.velocity = together + closing * (ShuntBounce * lead.mass / total);
                struck.angularVelocity = Vector3.up * (-turn * 0.6f);
            }
            else
            {
                lead.velocity = firstVelocity;
            }
            lead.angularVelocity = Vector3.up * turn;
            _shuntRestFrom = Time.time + ShuntRestFrom;
            _shuntEnd = Time.time + ShuntMaxTime;
            _phase = Phase.Shunting;
            return true;
        }

        // Every frame while the cars of a collision are still moving (1.6 s at the most): each loses speed as on locked
        // wheels, its vertical speed is bled off (nothing else damps the springs of a car the traffic system ignores,
        // and it must not hop), and once all are at rest, or out of time or room, each is frozen upright where it is
        // and the scene is built round where they came to rest.
        private void TickShunt(float now)
        {
            float step = Time.deltaTime;
            bool moving = false;
            bool strayed = false;
            for (int i = 0; i < _wrecks.Count; i++)
            {
                VehicleComponent car = _wrecks[i].Car;
                Rigidbody body = car != null ? car.rb : null;
                if (body == null || body.isKinematic)
                {
                    continue;
                }
                Vector3 velocity = body.velocity;
                float fall = velocity.y * Mathf.Exp(-ShuntDamping * step);
                velocity.y = 0f;
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, ShuntBraking * step);
                body.velocity = new Vector3(velocity.x, fall, velocity.z);
                float spin = Mathf.MoveTowards(body.angularVelocity.y, 0f, 1.2f * step);
                body.angularVelocity = Vector3.up * spin;
                moving |= velocity.sqrMagnitude > 0.09f || Mathf.Abs(spin) > 0.1f;
                Vector3 offset = body.position - _origin;
                strayed |= offset.x * offset.x + offset.z * offset.z > ShuntRadius * ShuntRadius || offset.y < -3f;
            }
            if (!strayed && now < _shuntEnd && (moving || now < _shuntRestFrom))
            {
                return;
            }

            // Where each came to rest is where it stands for the scene: on its wheels, turned as it turned.
            for (int i = 0; i < _wrecks.Count; i++)
            {
                VehicleComponent car = _wrecks[i].Car;
                if (car != null)
                {
                    Transform at = car.transform;
                    Pin(_wrecks[i], at.position, Quaternion.Euler(0f, at.rotation.eulerAngles.y, 0f));
                }
            }
            VehicleComponent first = _leadWreck != null ? _leadWreck.Car : null;
            if (first == null)
            {
                Clear(resumeTraffic: true, announce: false, why: "the car that crashed was lost");
                return;
            }
            Vector3 rest = first.transform.position;
            float moved = Vector3.Distance(rest, _origin);
            _origin = rest;
            _heading = first.transform.rotation;
            _scene = _origin;
            Logger?.Info($"Accident: the cars came to rest {moved:F1} m on from where they met, after {ShuntMaxTime - Mathf.Max(0f, _shuntEnd - now):F1} s"
                         + $"{(strayed ? " (frozen early: one had gone too far)" : string.Empty)}; the scene is measured from there.");
            CompleteStaging(tumbled: false);
        }

        private static readonly List<Collider> RestScratch = new List<Collider>(16);

        // Freezes a car that physics has finished throwing about in the nearest pose a wreck comes to rest in:
        // flat on its side, on its roof or on its wheels (never leaning on an edge), pointing the way it
        // points, with its lowest part on the road. Says which.
        private string RestRolled(Wreck wreck)
        {
            VehicleComponent car = wreck.Car;
            Transform body = car.transform;
            Vector3 forward = body.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.05f)
            {
                forward = _heading * Vector3.forward;   // standing on its nose: laid down the way it was going
                forward.y = 0f;
            }
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 up = body.up;
            string how = up.y > 0.7f ? "on its wheels" : up.y < -0.7f ? "on its roof" : "on its side";
            Vector3 roof = up.y > 0.7f ? Vector3.up : up.y < -0.7f ? Vector3.down : Vector3.Dot(up, right) >= 0f ? right : -right;
            Vector3 at = body.position;
            Pin(wreck, at, Quaternion.LookRotation(forward, roof));

            // Down (or up) until the lowest of its body is on the road.
            Physics.SyncTransforms();
            float lowest = float.MaxValue;
            car.GetComponentsInChildren(false, RestScratch);
            for (int i = 0; i < RestScratch.Count; i++)
            {
                Collider collider = RestScratch[i];
                if (collider != null && collider.enabled && !collider.isTrigger && !(collider is WheelCollider))
                {
                    lowest = Mathf.Min(lowest, collider.bounds.min.y);
                }
            }
            RestScratch.Clear();
            if (lowest < float.MaxValue && StreetSpots.Ground(at, out float ground) && Mathf.Abs(ground - lowest) < 3f)
            {
                at.y += ground - lowest + 0.02f;
                body.position = at;
            }
            return how;
        }

        // Places the other cars around the lead car's final position, dresses the scene and sets its timetable.
        private void CompleteStaging(bool tumbled)
        {
            Vector3 forward = _heading * Vector3.forward;
            Vector3 right = _heading * Vector3.right;
            VehicleComponent first = _leadWreck.Car;

            // (A collision that happened on the road: the cars are where they met, and none of the placing below applies.)
            // The third car first (it is taken off the end of the list if there is no room for it): it
            // swerved and ended up alongside, further out if the lead car is lying across the road.
            if (_wrecks.Count > 2 && !_inPlace)
            {
                float gap = tumbled ? Random.Range(3.6f, 4.4f) : Random.Range(2.3f, 2.9f);
                Vector3 position = _scene - right * _side * gap - forward * Random.Range(0.5f, 3f);
                Quaternion rotation = _heading * Quaternion.Euler(0f, Random.Range(15f, 40f) * -_side, 0f);
                if (!MoveIfFree(_wrecks[2], position, rotation))
                {
                    LetGo(_wrecks[2]);
                }
            }
            if (_wrecks.Count > 1 && !_inPlace)
            {
                // Rear-end shunt: the second car is jammed into the back of the first; if the road there is
                // taken, the first ran into the back of it instead. A car with no room either way was not in it.
                VehicleComponent second = _wrecks[1].Car;
                float reach = first.length * 0.5f + second.length * 0.5f - 0.3f;
                Vector3 behind = _scene - forward * reach;
                if (!MoveIfFree(_wrecks[1], behind + right * Random.Range(-0.7f, 0.7f), _heading * Quaternion.Euler(0f, Random.Range(-18f, 18f), 0f))
                    && !MoveIfFree(_wrecks[1], behind, _heading)
                    && !MoveIfFree(_wrecks[1], _scene + forward * reach, _heading))
                {
                    LetGo(_wrecks[1]);
                }
            }
            bool alone = _wrecks.Count == 1;
            if (alone && !tumbled && !_inPlace)
            {
                // One car on its own left the lane: over towards the side it is skewed to, if there is room.
                Transform body = first.transform;
                Vector3 aside = body.position + right * (_side * Random.Range(1f, 1.5f));
                if (IsPoseFree(first, aside, body.rotation))
                {
                    Pin(_leadWreck, aside, body.rotation);
                    _scene = new Vector3(aside.x, _scene.y, aside.z);
                }
            }
            _single = alone;
            _seatsEmptied = false;

            // Said only now, when it is known how many cars are really in it.
            string key = _causeKey != null ? _causeKey : alone
                ? (_severity == Severity.Minor ? "reallife_accident_single_minor"
                    : _severity == Severity.Moderate ? "reallife_accident_single_moderate"
                    : "reallife_accident_single_severe")
                : (_severity == Severity.Minor ? "reallife_accident_minor"
                    : _severity == Severity.Moderate ? "reallife_accident_moderate"
                    : "reallife_accident_severe");
            Notice(_severity == Severity.Severe ? NotificationType.Error : NotificationType.Warning, key, 8f);

            // Some accidents are hit-and-runs: the lead driver will bolt when the police turn up. (Where the cars
            // stand as they met, only if the other one is not in front of it: it cannot drive off through a wreck.)
            if (Settings.Police && !tumbled && _wrecks.Count > 1 && Random.value < HitAndRunChance
                && (!_inPlace || Vector3.Dot(_wrecks[1].Car.transform.position - first.transform.position, forward) < 0f))
            {
                PlanChase(first);
            }
            bool planned = _chase == Chase.Planned;

            // The damage goes on the lead car, unless that one is about to drive off.
            Wreck worst = planned ? _wrecks[1] : _leadWreck;
            for (int i = 0; i < _wrecks.Count; i++)
            {
                AddEffect(_wrecks[i], TrafficEffects.CreateFlasher(_wrecks[i].Car, "RealLifeOverhaul Hazard", TrafficEffects.BeaconHeight(_wrecks[i].Car),
                    new Color(1f, 0.55f, 0.05f), new Color(1f, 0.55f, 0.05f), 0.5f));
            }
            Dress(worst, alone);
            if (_severity == Severity.Severe && !planned && _wrecks.Count > 1)
            {
                AddEffect(_wrecks[1], TrafficEffects.CreateSmoke(_wrecks[1].Car, heavy: false));
            }

            // Who drives away and who needs a tow, once it is over.
            for (int i = 0; i < _wrecks.Count; i++)
            {
                _wrecks[i].Towed = _wrecks[i] == worst ? _severity != Severity.Minor : _severity == Severity.Severe;
            }
            if (planned)
            {
                _leadWreck.Towed = false;
            }

            // The timetable. With police attending, the smaller accidents run a little longer so the visit fits.
            float now = Time.time;
            bool police = Settings.Police;
            float total = _severity == Severity.Minor ? (police ? Random.Range(70f, 100f) : Random.Range(35f, 65f))
                : _severity == Severity.Moderate ? (police ? Random.Range(90f, 130f) : Random.Range(80f, 125f))
                : Random.Range(125f, 170f);
            if (planned)
            {
                total = Mathf.Max(total, Random.Range(150f, 170f));
            }
            _deadline = now + Mathf.Min(total, MaxSceneTime);

            // The drivers: the one whose car took the damage, and the other (who may be about to run).
            _worstWreck = worst;
            _otherWreck = planned ? _leadWreck : _wrecks.Count > 1 ? _wrecks[1] : null;
            VehicleComponent otherCar = _otherWreck != null ? _otherWreck.Car : null;
            _people.Begin(_severity, _scene, worst.Car, otherCar, planned, now);
            if (!planned)
            {
                // The car that caused it is the lead car, and here the worst-hit one (a reckless driver's own crash, the staged
                // crash's first car): its driver stayed, so is questioned and ticketed at the scene, not arrested. Only a
                // hit-and-run's runner is arrested, and that is the chase's (RoadChase, if the patrol car catches them).
                _people.AtFault(worst: true);
            }

            // Each unit is sent after its own delay, so who gets there first varies. A minor bump closes nothing.
            PlanResponse(now, _severity != Severity.Minor, police, now + Random.Range(5f, 13f),
                now + (planned ? Random.Range(3f, 10f) : Random.Range(3f, 22f)), null, shut: _severity != Severity.Minor);

            // Glass and plastic where the cars met (or where one met whatever stopped it), for the officers to mark.
            if (_severity == Severity.Severe)
            {
                _cordon.Litter(Impact(), forward);
            }

            News.Report(NewsKind.Accident, _scene, null, (int)_severity);
            using var piece = SessionWatch.Piece("traffic: an accident being staged");
            Logger?.Info($"Accident {(_inPlace ? "scene begun where it happened" : "staged")}: {_severity}, {_wrecks.Count} car(s){(alone ? " (single vehicle)" : "")}, clears within {total:F0} s{(planned ? ", hit-and-run planned" : "")}.");
        }

        // Where the damage was done: between the first two cars, or just ahead of the nose of one on its own.
        private Vector3 Impact()
        {
            TrafficTools.Footprint(_wrecks[0].Car, out Vector3 centre, out Vector3 forward, out float halfLength, out _);
            Vector3 impact = centre + forward * (halfLength + 0.5f);
            if (_wrecks.Count > 1)
            {
                TrafficTools.Footprint(_wrecks[1].Car, out Vector3 second, out _, out _, out _);
                impact = (centre + second) * 0.5f;
            }
            impact.y = _scene.y;
            return impact;
        }

        // A vehicle that shows its driver sitting in the cab (the delivery truck does) has that figure hidden
        // while the driver is out on the road as one of the scene's people. Only what was showing is hidden,
        // and Unpin shows exactly that again: the figure belongs to a pooled car.
        private static void EmptySeat(Wreck wreck, bool onRoad)
        {
            if (wreck == null || wreck.Car == null || wreck.Seat.Count > 0)
            {
                return;
            }
            var cab = wreck.Car.GetComponentInChildren<DeliveryTruckDriverController>(true);
            if (cab == null || cab.driverAppearanceSetter == null)
            {
                return;
            }
            cab.driverAppearanceSetter.GetComponentsInChildren(true, SeatScratch);
            // The figure that is drawn is the merged mesh, which the game keeps on an object of its own (and lists
            // among the vehicle's body meshes): taken by both names it is known under, wherever it hangs.
            Renderer merged = cab.driverAppearanceSetter.lastMergedMeshRenderer;
            if (merged != null && !SeatScratch.Contains(merged))
            {
                SeatScratch.Add(merged);
            }
            var combiner = cab.driverAppearanceSetter.meshCombiner;
            Renderer combined = combiner != null && combiner.resultMergeGameObject != null ? combiner.resultMergeGameObject.GetComponent<Renderer>() : null;
            if (combined != null && !SeatScratch.Contains(combined))
            {
                SeatScratch.Add(combined);
            }
            for (int i = 0; i < SeatScratch.Count; i++)
            {
                Renderer renderer = SeatScratch[i];
                if (renderer != null && !renderer.forceRenderingOff)
                {
                    renderer.forceRenderingOff = true;
                    wreck.Seat.Add(renderer);
                }
            }
            SeatScratch.Clear();
            if (wreck.Seat.Count > 0)
            {
                Logger?.Info($"Accident: the driver of '{TrafficTools.Name(wreck.Car)}' "
                             + (onRoad ? "is out on the road" : "is played by nobody at this scene, and does not sit at the wheel of a wreck through it")
                             + $"; the figure in its cab ({wreck.Seat.Count} renderer(s)) is hidden until the car is let go.");
            }
        }

        private static readonly List<Renderer> SeatScratch = new List<Renderer>(8);

        private static void FillSeat(Wreck wreck)
        {
            for (int i = 0; i < wreck.Seat.Count; i++)
            {
                if (wreck.Seat[i] != null)
                {
                    wreck.Seat[i].forceRenderingOff = false;
                }
            }
            wreck.Seat.Clear();
        }

        // What every scene does once whatever lies in the road is where it will stay: the road is measured
        // and (unless it is a minor bump) closed, the vehicles standing in it become solid for people on
        // foot, and the units are given their times and their ends of the scene.
        private void PlanResponse(float now, bool ambulance, bool police, float ambulanceAt, float policeAt, ICasualty casualty, bool shut)
        {
            _hardLimit = now + HardSceneTime;
            _extended = false;
            _clearLogged = false;
            _phase = Phase.Active;
            EndDepartures();   // a unit still driving away from the last scene is wanted for this one
            CloseRoad(casualty, shut);
            _people.OnRoadClosed(_cordon);
            for (int i = 0; i < _wrecks.Count; i++)
            {
                SceneObstacles.Add(_wrecks[i].Car, transform);
            }

            Schedule(_ambulance, ambulance, ambulanceAt, AmbulanceStopDistance);
            Schedule(_police, police, policeAt, Random.Range(19f, 25f));
            // The patrol car takes the far end where a lane runs up to it. Where both units must come up
            // the same lane, the ambulance is sent first so that it ends up nearest whoever is hurt.
            _police.FarEnd = police && _cordon.HasFarEnd;
            if (police && ambulance && !_police.FarEnd && _chase != Chase.Planned)
            {
                _police.At = Mathf.Max(_police.At, _ambulance.At + 5f);
            }
            PlanDeparture(ambulance, police);   // where the patrol car stands is settled against the ambulance's way out, now
        }

        private static void Schedule(Attendance attendance, bool attends, float at, float stopDistance)
        {
            attendance.Stage = attends ? Stage.Waiting : Stage.Idle;
            attendance.At = at;
            attendance.StopDistance = stopDistance;
            attendance.Retried = false;
            attendance.ParkChosen = false;
            attendance.Recalled = false;
            attendance.FarEnd = false;
            attendance.AtStand = false;
            attendance.NextCheck = 0f;
            attendance.MovedOn = 0;
            attendance.Waits = false;
            attendance.Through = false;
            attendance.Still = 0f;
            attendance.OwnLane = false;
            attendance.Backing = false;
            attendance.GaveWay = 0;
            attendance.SetOffAt = 0f;
            attendance.SaidHeld = false;
        }

        // ---------------------------------------------------------------- the way out, planned

        private const float AmbulanceHalf = 1.25f;      // half the width an ambulance is taken to have, before one is on the road
        private const float PatrolHalf = 1.05f;         // ...and a patrol car
        private const float PassingRoom = 0.4f;         // metres left between the two when the ambulance drives out past the patrol car
        private const float BackOffMost = 16f;          // metres a patrol car backs along its own lane to give the ambulance the road
        private const float BackOffPace = 3f;           // ...at this many m/s
        private const float NearPlayer = 45f;           // a vehicle this near the camera is never taken off the road, whichever way the camera looks

        // The self-check of a departure, one line a step, each with what was measured and PASS or FAIL: planned, patrol car
        // parked, ambulance set off, ambulance past the scene, patrol car set off, road reopened. A run can be judged from these.
        private static void DepartureCheck(string step, bool pass, string measured)
        {
            string line = "Accident: departure check (" + step + "): " + measured + ": " + (pass ? "PASS." : "FAIL.");
            if (pass)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
        }

        // True if somebody could be looking at a place: in the camera's view, or simply near it (the camera turns faster than a car goes).
        private static bool Seen(Vector3 place)
        {
            if (StreetSpots.IsInView(place))
            {
                return true;
            }
            Camera camera = GameManager.GetMainCamera();
            return camera != null && (camera.transform.position - place).sqrMagnitude < NearPlayer * NearPlayer;
        }

        // Decided when the units are scheduled, before either is on the road: the loaded ambulance will leave forwards
        // along its own lane, through the scene and out past its far end. A patrol car sent to the far end stands
        // there in a lane running the other way, facing the scene, and must stand clear of that line by both their
        // widths and room to pass. Where the road does not give that (a narrow street, lanes that run together), it is
        // not sent to the far end at all: it comes up the ambulance's own lane behind it, where it is in nobody's way.
        private void PlanDeparture(bool ambulance, bool police)
        {
            if (!ambulance || !police)
            {
                DepartureCheck("planned", true, ambulance ? "only the ambulance attends: it leaves forwards along its own lane, nothing of the mod's ahead of it"
                    : police ? "only the patrol car attends: it leaves forwards along its own lane when the scene clears" : "no unit attends");
                return;
            }
            if (!_police.FarEnd)
            {
                DepartureCheck("planned", true, "both units come up the same lane, the ambulance first and nearest the scene, the patrol car behind it: "
                    + "the ambulance leaves forwards through the scene, the patrol car follows when the scene clears");
                return;
            }
            float need = AmbulanceHalf + PatrolHalf + PassingRoom;
            float clear = ExitClearance(out float along);
            if (clear >= need)
            {
                DepartureCheck("planned", true, $"the ambulance leaves forwards along its own lane through the scene; the patrol car stands at the far end in the lane running the other way, "
                    + $"{clear:F1} m from the middle of the ambulance's way out, {along:F0} m along it ({need:F1} m needed: both half widths and {PassingRoom:F1} m); "
                    + "order: the ambulance first, the patrol car when it is past");
                return;
            }
            _police.FarEnd = false;
            if (_chase != Chase.Planned)
            {
                _police.At = Mathf.Max(_police.At, _ambulance.At + 5f);
            }
            DepartureCheck("planned", true, (clear < 0f ? "no place for the patrol car at the far end could be measured against the ambulance's way out"
                    : $"a patrol car at the far end would stand {clear:F1} m from the middle of the ambulance's way out ({need:F1} m needed)")
                + "; it is sent up the ambulance's own lane behind it instead, after it: nothing of the mod's stands ahead of the ambulance");
        }

        // Metres from where a patrol car would stand at the far end to the middle line of the ambulance's way out (its own
        // lane on from where it stands, as the road graph has it), and how far along that way; -1 if either cannot be had.
        private float ExitClearance(out float along)
        {
            along = 0f;
            if (!_cordon.Stand(false, AmbulanceBack, out Waypoint anchor, out _) || !_cordon.Stand(true, PatrolBackAlone, out _, out Vector3 patrol))
            {
                return -1f;
            }
            float least = float.MaxValue;
            // (Every way the lane goes on: at a junction the way out may take any of them, so all are measured.)
            for (int attempt = 0; attempt < 4; attempt++)
            {
                TrafficTools.WalkForward(anchor, 110f, 70, _routeScratch);
                float walked = 0f;
                for (int i = 1; i < _routeScratch.Count; i++)
                {
                    Vector3 from = _routeScratch[i - 1].position;
                    Vector3 leg = _routeScratch[i].position - from;
                    leg.y = 0f;
                    float length = leg.magnitude;
                    if (length < 0.05f)
                    {
                        continue;
                    }
                    Vector3 offset = patrol - from;
                    offset.y = 0f;
                    float t = Mathf.Clamp(Vector3.Dot(offset, leg) / length, 0f, length);
                    float distance = (offset - leg * (t / length)).magnitude;
                    if (distance < least)
                    {
                        least = distance;
                        along = walked + t;
                    }
                    walked += length;
                }
            }
            _routeScratch.Clear();
            return least == float.MaxValue ? -1f : least;
        }

        // Both units are standing at the scene: where the patrol car really is against the lane the ambulance will leave by,
        // measured body to body. (If it does stand in that lane after all, it is moved when the ambulance goes: GiveWay.)
        private void ParkedCheck()
        {
            if (_ambulance.Stage != Stage.OnScene || _police.Stage != Stage.OnScene || _ambulance.Unit.Car == null || _police.Unit.Car == null)
            {
                return;
            }
            TrafficTools.Footprint(_ambulance.Unit.Car, out Vector3 centre, out Vector3 forward, out _, out float halfWidth);
            TrafficTools.Footprint(_police.Unit.Car, out Vector3 theirs, out Vector3 theirForward, out _, out float theirHalf);
            Vector3 offset = theirs - centre;
            offset.y = 0f;
            float along = Vector3.Dot(offset, forward);
            float aside = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward))) - halfWidth - theirHalf;
            bool ahead = along > 0f;
            bool facing = Vector3.Dot(theirForward, forward) < 0f;
            DepartureCheck("units parked", !ahead || aside >= ExitMargin,
                ahead ? $"the patrol car stands {along:F0} m ahead of the ambulance, {(facing ? "facing it" : "facing the same way")}, {aside:F1} m clear of the side of the lane it will leave by ({ExitMargin:F2} m needed)"
                      + (aside >= ExitMargin ? "" : ": it is moved out of that lane when the ambulance goes")
                    : $"the patrol car stands {-along:F0} m behind the ambulance: nothing of the mod's is ahead of it");
        }

        private float _nextSweep;
        private bool _sweepSaid;
        private const float SweepWithin = 90f;   // metres round a scene in which no ordinary ambulance is left in traffic

        private static readonly int HotSweep = HotSpots.Slot("Traffic: TrafficAccidents.SweepAmbulances");   // (inside its Update: names the stage that took a long frame)
        // One ambulance at a scene: the scene's own. An ordinary one from the traffic pool that comes near (it ends up in
        // the queue held at the tape, a second ambulance standing beside the first) is taken off the road, but only where
        // nobody can see it go. Every three seconds while a scene is on; a walk of the traffic list, nothing else.
        private void SweepAmbulances()
        {
            using var hotSpot = HotSpots.Time(HotSweep);
            if (!TrafficTools.Ready)
            {
                return;
            }
            List<VehicleComponent> pool = Manager.GetVehicleList();
            VehicleComponent ours = _ambulance.Unit.InUse ? _ambulance.Unit.Car : null;
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car == null || car == ours || !car.gameObject.activeInHierarchy || !TrafficTools.IsAmbulance(car) || TrafficTools.IsHeld(car) || car.presetPath != null)
                {
                    continue;
                }
                Vector3 at = car.transform.position;
                if ((at - _scene).sqrMagnitude > SweepWithin * SweepWithin || Seen(at))
                {
                    continue;
                }
                TrafficTools.Remove(car, "an ordinary ambulance near an accident scene, out of view: the scene has its own");
                if (!_sweepSaid)
                {
                    _sweepSaid = true;
                    Logger?.Info("Accident: an ordinary ambulance from traffic came near the scene and was taken off the road out of view, so that only the scene's own stands there. (Said once a session.)");
                }
            }
        }

        // A unit that is leaving has stopped behind the other unit of this scene. That is never left to time: the unit
        // in the way is moved, properly, at once. The ambulance has the road: a patrol car in its way drives off ahead
        // of it if it points the same way, and backs off along its own lane if the two are nose to nose (its officers
        // called aboard first if they are still out). A patrol car held up behind the ambulance simply waits for it.
        // True if the other unit is what is in the way and something was done (or is under way) about it.
        private bool GiveWay(Attendance attendance, float now)
        {
            Attendance other = attendance.Police ? _ambulance : _police;
            ResponseUnit unit = attendance.Unit;
            Rigidbody blocker = unit.Driver.Blocker;
            if (blocker == null || !other.Unit.InUse || other.Unit.Car == null || other.Unit.Car.rb != blocker || unit.Driver.HeldFor < 1f)
            {
                return false;
            }
            if (attendance.Police)
            {
                // The patrol car behind the ambulance: if the ambulance is itself standing behind the patrol car (nose to nose), the patrol car is the one that backs.
                if (other.Stage == Stage.Leaving && other.Unit.Driver.Blocker == unit.Car.rb)
                {
                    BackOff(attendance, now, wasWaiting: false);
                }
                return true;
            }
            attendance.GaveWay++;
            switch (other.Stage)
            {
                case Stage.OnScene:
                    // Its officers are still out: called aboard now (the ambulance is loaded, nothing is left to guard).
                    if (!other.Recalled)
                    {
                        other.At = Mathf.Min(other.At, now);
                        Logger?.Info("Accident: the patrol car stands in the loaded ambulance's way with its officers out; they are called aboard at once.");
                    }
                    return true;

                case Stage.Ready:
                    if (other.Backing)
                    {
                        return true;
                    }
                    bool noseToNose = Vector3.Dot(other.Unit.Car.transform.forward, unit.Car.transform.forward) < 0.2f;
                    if (noseToNose)
                    {
                        BackOff(other, now, other.Waits);
                    }
                    else if (!Leave(other, now))
                    {
                        BackOff(other, now, other.Waits);
                    }
                    return true;

                case Stage.Leaving:
                    return true;   // on its way already: the ambulance follows it out
            }
            return false;
        }

        // The patrol car backs along its own lane, tail first, out of the ambulance's way: a route of road points behind
        // it, driven as any other (it looks behind it, closes up on whatever is there and stops short of it).
        private void BackOff(Attendance attendance, float now, bool wasWaiting)
        {
            ResponseUnit unit = attendance.Unit;
            VehicleComponent car = unit.Car;
            Vector3 at = car.transform.position;
            Vector3 facing = car.transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f || !TrafficTools.Ready)
            {
                return;
            }
            facing.Normalize();
            float tail = Mathf.Max(1.5f, car.length * 0.5f);
            Waypoint behind = TrafficTools.ClosestWaypointAlong(at - facing * (tail + 2f), facing * 2f, 12f);
            bool found = behind != null && TrafficTools.WalkBack(behind, BackOffMost + 8f, 24, _routeScratch) > 6f;
            unit.Path.Clear();
            for (int i = _routeScratch.Count - 1; i >= 0 && found; i--)
            {
                // (Nearest first, and only road that is behind its tail.)
                Vector3 point = _routeScratch[i].position;
                if (Vector3.Dot(point - at, facing) < -tail)
                {
                    unit.Path.Add(point);
                }
            }
            _routeScratch.Clear();
            if (unit.Path.Count == 0)
            {
                // No road point behind it to back along: straight back along the way it stands, which is the lane it drove up.
                unit.Path.Add(at - facing * (tail + BackOffMost * 0.5f));
                unit.Path.Add(at - facing * (tail + BackOffMost));
            }
            SceneObstacles.Remove(car);
            unit.Unpark(quiet: true);
            unit.Driver.BeginReverse(car, unit.Path, BackOffPace);
            attendance.Stage = Stage.Ready;
            attendance.Backing = true;
            attendance.BackFrom = at;
            attendance.BackUntil = now + 12f;
            attendance.WaitedBefore = wasWaiting;
            attendance.Waits = false;   // (not "finished" while it is moving: the scene does not clear round a unit that is backing)
            attendance.Still = 0f;
            TrafficWatch.Guard(car, Name(attendance));
            Logger?.Info($"Accident: the {Name(attendance)} stands nose to nose with the ambulance that is leaving; it backs along its own lane, up to {BackOffMost:F0} m, to give it the road.");
        }

        // Backing: until it has gone far enough, has come up to something behind it, or the ambulance is past. Then it stands
        // again (waiting for the scene to clear, as before) or, with the ambulance gone, leaves forwards itself.
        private void TickBacking(Attendance attendance, float now)
        {
            ResponseUnit unit = attendance.Unit;
            EmergencyDriver driver = unit.Driver;
            float backed = Vector3.Distance(unit.Car.transform.position, attendance.BackFrom);
            Attendance other = attendance.Police ? _ambulance : _police;
            bool done = !driver.Active || driver.Arrived || backed >= BackOffMost || driver.HeldFor > 1.5f || now >= attendance.BackUntil || other.Past;
            if (!done)
            {
                return;
            }
            attendance.Backing = false;
            // (Waiting for the scene to clear again if that is what it was doing and there still is a scene; else it tries to leave, forwards, every second.)
            Stand(attendance, now, waits: attendance.WaitedBefore && _phase != Phase.None);
            SceneObstacles.Add(unit.Car, transform);
            Logger?.Info($"Accident: the {Name(attendance)} has backed {backed:F0} m along its lane and stands there until the ambulance is past.");
        }

        // Measures the road round everything lying in it (the wrecks, and anyone knocked down or lying
        // beside their car) and shuts it: traffic stops short of the scene and new traffic turns off; the
        // tape goes up when the police get there.
        private void CloseRoad(ICasualty casualty, bool shut)
        {
            _footprints.Clear();
            for (int i = 0; i < _wrecks.Count; i++)
            {
                // Where the wreck really lies, skew and all (its colliders), so that no end of it crosses the tape.
                TrafficTools.Footprint(_wrecks[i].Car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
                _footprints.Add(new Footprint { Centre = centre, Forward = forward, HalfLength = halfLength + 0.15f, HalfWidth = halfWidth + 0.15f, Vehicle = true });
            }
            if (casualty != null)
            {
                _footprints.Add(new Footprint { Centre = casualty.Position, Forward = Vector3.forward, HalfLength = 1.9f, HalfWidth = 1.9f });   // room for the crew and the stretcher beside them
            }
            else if (_people.WillLie(out Vector3 where, out Vector3 along))
            {
                _footprints.Add(new Footprint { Centre = where, Forward = along, HalfLength = 1.6f, HalfWidth = 1.9f });
            }
            _cordon.Close(_scene, _heading * Vector3.forward, _footprints, shut);
        }

        // True if the car would not overlap anything outside this accident in that pose.
        private bool IsPoseFree(VehicleComponent car, Vector3 position, Quaternion rotation)
        {
            return SceneParking.IsFree(car, position, rotation, _involved);
        }

        // Taxis, private drivers and anything on a scripted route are left alone, and an ambulance
        // is never the car that crashed or the one that runs.
        private static bool IsUsable(VehicleComponent car)
        {
            return car != null && car.gameObject.activeInHierarchy && car.presetPath == null
                   && !TrafficTools.IsAmbulance(car) && !TrafficTools.SpokenFor(car)
                   && !TrafficManager.Instance.IsVehicleIgnored(car.GetIndex());
        }

        // Takes the car away from the traffic system. It is not pinned yet.
        private Wreck Seize(VehicleComponent car)
        {
            Manager.SetVehicleIgnored(car.GetIndex(), true);
            Rigidbody body = car.rb;
            var wreck = new Wreck
            {
                Car = car,
                WasKinematic = body.isKinematic,
                Constraints = body.constraints,
                // The game deletes AI cars that sit still for 20-40 seconds.
                RescueCheck = TrafficTools.SuspendRescue(car)
            };
            // A scripted route (even an empty one) stops the game taking the car off the road for being far
            // from the player or sweeping it away with ordinary traffic: without it a wreck staged at the
            // edge of the traffic range could be gone the moment after it was announced.
            car.presetPath = wreck.Preset;
            TrafficTools.Touch(car, "taken out of traffic as a wreck");
            _wrecks.Add(wreck);
            return wreck;
        }

        // A car that was drawn in but has no room in the crash: it was never in it, and drives on.
        private void LetGo(Wreck wreck)
        {
            _wrecks.Remove(wreck);
            Unpin(wreck);
            if (wreck.Car != null)
            {
                _involved.Remove(wreck.Car.rb);
                TrafficTools.ReturnToTraffic(wreck.Car, "the accident scene: no room for it in the crash");
            }
        }

        // Pins a wreck where the crash left it.
        private static void Pin(Wreck wreck, Vector3 position, Quaternion rotation)
        {
            Rigidbody body = wreck.Car.rb;
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            wreck.Car.transform.SetPositionAndRotation(position, rotation);
        }

        // Moves a pinned car into the crash if there is room. False if there is not; it has not moved then.
        private bool MoveIfFree(Wreck wreck, Vector3 position, Quaternion rotation)
        {
            if (!IsPoseFree(wreck.Car, position, rotation))
            {
                return false;
            }
            wreck.Car.transform.SetPositionAndRotation(position, rotation);
            return true;
        }

        // Takes everything off a wreck and gives the car its own physics back. It stays out of traffic until released.
        private static void Unpin(Wreck wreck)
        {
            FillSeat(wreck);
            foreach (GameObject effect in wreck.Effects)
            {
                if (effect != null)
                {
                    Destroy(effect);
                }
            }
            wreck.Effects.Clear();
            if (wreck.RescueCheck != null)
            {
                wreck.RescueCheck.enabled = true;
            }
            if (wreck.Car != null)
            {
                SceneObstacles.Remove(wreck.Car);
                if (wreck.Car.presetPath == wreck.Preset)
                {
                    wreck.Car.presetPath = null;
                }
                wreck.Car.rb.constraints = wreck.Constraints;
                wreck.Car.rb.isKinematic = wreck.WasKinematic;
                TrafficTools.Touch(wreck.Car, "unpinned by the accident scene");
            }
        }

        private static void AddEffect(Wreck wreck, GameObject effect)
        {
            if (effect != null)
            {
                wreck.Effects.Add(effect);
            }
        }

        // Smoke, fire and debris for the worst-hit car, scaled to the severity.
        private void Dress(Wreck wreck, bool alone)
        {
            VehicleComponent car = wreck.Car;
            if (_severity == Severity.Minor)
            {
                // A car on its own must show why it has stopped: bits on the road and a wisp from the bonnet.
                if (alone || Random.value < 0.5f)
                {
                    AddEffect(wreck, TrafficEffects.CreateDebris(car, _scene, Random.Range(3, 6)));
                }
                if (alone)
                {
                    AddEffect(wreck, TrafficEffects.CreateSmoke(car, heavy: false));
                }
                var horn = car.GetComponentInChildren<AiCarHorn>();
                if (horn != null)
                {
                    TrafficTools.Hoot(horn);
                }
                return;
            }

            AddEffect(wreck, TrafficEffects.CreateDebris(car, _scene, _severity == Severity.Severe ? Random.Range(11, 18) : Random.Range(6, 10)));
            AddEffect(wreck, TrafficEffects.CreateSmoke(car, heavy: _severity == Severity.Severe));
            if (_severity == Severity.Severe && Random.value < 0.6f)
            {
                AddEffect(wreck, TrafficEffects.CreateFire(car));
            }
        }

        // ---------------------------------------------------------------- the scene

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

        /// <summary>
        /// One of the notifications about an accident of other people's (its start, the road closed, a unit on its way, a
        /// chase, the road open again): told only while the option for them is on. What the player did themselves (helped,
        /// hit somebody) is told whatever it is.
        /// </summary>
        public static void Notice(NotificationType type, string key, float seconds, string group = "reallife_accident")
        {
            if (Settings.AccidentNotices)
            {
                Notifications.Show(type, key, null, seconds, group);
            }
        }

        // Pins the accident marker over the scene, or to the screen edge when the scene is out of view (and so the route
        // to it); with the option for it off, there is none.
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
            _mark.Show(_scene, _markerLabel);   // the game's destination marker
        }

        private static readonly int HotAttend = HotSpots.Slot("Traffic: TrafficAccidents.TickAttendance");   // (inside its Update: names the stage that took a long frame)
        // One unit's visit: wait to be sent, drive there, pull up, stay while its crew works, drive away.
        // Every stage ends on a clock as well as on what it is waiting for, and says so in the log once.
        private void TickAttendance(Attendance attendance, float now)
        {
            using var hotSpot = HotSpots.Time(HotAttend);
            ResponseUnit unit = attendance.Unit;
            switch (attendance.Stage)
            {
                case Stage.Waiting:
                    if (now >= attendance.At)
                    {
                        Dispatch(attendance, now);
                    }
                    break;

                case Stage.EnRoute:
                    if (unit.IsLost)
                    {
                        Logger?.Info($"Accident: the {Name(attendance)} was taken off the road on its way; the scene runs without it.");
                        Withdraw(attendance);
                        break;
                    }
                    float sceneSqr = (unit.Car.transform.position - _scene).sqrMagnitude;
                    if (attendance.Police && _chase == Chase.Planned && sceneSqr < FleeTriggerDistance * FleeTriggerDistance)
                    {
                        BeginChase(now);
                        break;
                    }
                    EmergencyDriver driver = unit.Driver;
                    if (!attendance.AtStand && !attendance.ParkChosen && sceneSqr < ParkLookDistance * ParkLookDistance)
                    {
                        // No lane to stand in was to be had: near enough to see what is there, it picks a place on its route.
                        attendance.ParkChosen = true;
                        int place = SceneParking.Find(unit.Car, unit.Path, driver.NextPoint, _scene, attendance.StopDistance);
                        if (place >= 0)
                        {
                            driver.PullUpAt(place);
                        }
                    }
                    if (now >= attendance.NextCheck)
                    {
                        attendance.NextCheck = now + BlockedCheck;
                        ClearTheWay(attendance, driver);
                    }
                    if (driver.Arrived || !driver.Active || now >= attendance.At)
                    {
                        Pulled(attendance, driver, Mathf.Sqrt(sceneSqr), now);
                    }
                    break;

                case Stage.OnScene:
                    if (unit.IsLost)
                    {
                        Logger?.Info($"Accident: the {Name(attendance)} was taken off the road while parked.");
                        Withdraw(attendance);
                        break;
                    }
                    if (!attendance.Recalled && now >= attendance.At && MayLeave(attendance, now))
                    {
                        // Time to go: whoever is still out is called back, and given a moment to get there.
                        attendance.Recalled = true;
                        attendance.At = now + (attendance.Police ? CrewGrace : StretcherGrace);
                        _people.Recall(attendance.Police);
                    }
                    bool aboard = _people.CrewAboard(attendance.Police);
                    bool finished = !attendance.Police && _people.CrewWorked;   // the ambulance goes as soon as its patient is aboard
                    // The ambulance never drives off from under its own crew: with the stretcher out it waits until they are
                    // aboard, however long the way back is (the scene's own hard limit is the only thing that ends that).
                    bool waitsForCrew = !attendance.Police && _people.CrewOut && now < _hardLimit - CrewLastCall;
                    if ((aboard && (attendance.Recalled || finished)) || (attendance.Recalled && now >= attendance.At && !waitsForCrew))
                    {
                        Depart(attendance, now);
                    }
                    break;

                case Stage.Ready:
                    if (unit.IsLost)
                    {
                        Withdraw(attendance);
                    }
                    else if (attendance.Backing)
                    {
                        TickBacking(attendance, now);
                    }
                    else if (attendance.Waits && attendance.Police && _phase == Phase.Active && _ambulance.Past && now >= _lingerUntil && now >= attendance.NextCheck)
                    {
                        // The ambulance is past and the tape has been down its few seconds: if the patrol car's own lane through
                        // the scene is clear, it goes now, while the road is still closed, and the road is given back only when
                        // both are away. With a wreck still in that lane it waits for the scene to clear, as before.
                        attendance.NextCheck = now + 1f;
                        TrafficTools.Footprint(unit.Car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
                        float reach = Mathf.Clamp(_cordon.Through(centre, forward) + 6f, halfLength + 8f, 70f);
                        if (SceneParking.InStrip(centre + forward * (halfLength + 0.3f), centre + forward * reach, halfWidth + ExitMargin, unit.Car.rb, SceneParking.RoadsideMask) == null)
                        {
                            attendance.OwnLane = true;
                            attendance.Still = 0f;
                            if (!Leave(attendance, now))
                            {
                                Stand(attendance, now, waits: true);
                            }
                        }
                    }
                    else if (!attendance.Waits && now >= attendance.NextCheck)
                    {
                        attendance.NextCheck = now + 1f;
                        TryToGo(attendance, now);
                    }
                    break;

                case Stage.Leaving:
                    if (unit.IsLost)
                    {
                        ReleaseExit(attendance);
                        Withdraw(attendance);
                        break;
                    }
                    Vector3 where = unit.Car.transform.position;
                    if (attendance.Through && (!_cordon.Surveyed || _cordon.Through(where, unit.Car.transform.forward) < -PassedBy))
                    {
                        // Past where the scene is: one comparison a tick until then. It picks up speed, and the lane held for it is given back.
                        attendance.Through = false;
                        unit.Driver.CruiseSpeed = EmergencyDriver.DefaultCruiseSpeed;
                        unit.Driver.MayPass = true;   // beyond the scene it drives as on any road
                        ReleaseExit(attendance);
                        DepartureCheck(Name(attendance) + " past the scene", !unit.Driver.OutOfLane,
                            $"beyond the far side of the taped area {now - attendance.SetOffAt:F0} s after it set off"
                            + (unit.Driver.OutOfLane ? ", out of its own lane" : ", in its own lane")
                            + (attendance.GaveWay > 0 ? $"; the other unit was moved out of its way {attendance.GaveWay} time(s)" : ""));
                    }
                    bool moving = unit.Driver.Active && unit.Driver.Speed > MovingOff;
                    if (moving)
                    {
                        attendance.Still = 0f;
                    }
                    else if (attendance.Still <= 0f)
                    {
                        attendance.Still = now;
                    }
                    bool away = (where - attendance.From).sqrMagnitude > LeaveDistance * LeaveDistance;
                    if (moving && (away || (now >= attendance.At && !attendance.Through)))
                    {
                        // Clear of the scene and moving along a lane: only now does the traffic system have it back.
                        float speed = unit.Driver.Speed;
                        ReleaseExit(attendance);
                        unit.Release(resumeTraffic: true, remove: false, quiet: attendance.Police);
                        attendance.Stage = Stage.Done;
                        Logger?.Info($"Accident: the {Name(attendance)} has left: handed back to traffic {Vector3.Distance(where, attendance.From):F0} m on, moving at {speed * 3.6f:F0} km/h.");
                    }
                    else if (!unit.Driver.Active || unit.Driver.Arrived)
                    {
                        // The end of the road it was given, and standing: it is not handed back like that. More road is looked for.
                        ReleaseExit(attendance);
                        Stand(attendance, now, waits: false);
                    }
                    else if (!moving)
                    {
                        if (now >= attendance.NextCheck)
                        {
                            attendance.NextCheck = now + BlockedCheck;
                            if (GiveWay(attendance, now))
                            {
                                attendance.Still = now;   // the other unit is getting out of its way: that is not "standing boxed in"
                                break;
                            }
                            ClearExit(attendance, unit.Driver);
                        }
                        // Behind a person it waits (it never forces through), but not for ever; behind anything else, not for long.
                        bool person = unit.Driver.HeldFor > 0f && unit.Driver.Blocker == null;
                        if (now - attendance.Still > (person ? BoxedInByPeople : BoxedIn))
                        {
                            BoxedOut(attendance, now, person ? "someone stood in front of it" : unit.Driver.Blocker != null
                                ? "'" + unit.Driver.Blocker.name + "' stood in its way and could not be moved" : "it could not get moving");
                        }
                    }
                    break;
            }
        }

        private static string Name(Attendance attendance)
        {
            return attendance.Police ? "patrol car" : "ambulance";
        }

        // A unit standing behind ordinary traffic that has stopped in its way: that traffic is what moves, not
        // where the unit is going. The car is taken off the road (as the game does with any car that
        // stands still for long): at once if nobody is looking that way, after a few seconds otherwise.
        private void ClearTheWay(Attendance attendance, EmergencyDriver driver)
        {
            float held = driver.HeldFor;
            Rigidbody body = driver.Blocker;
            if (held < HeldBeforeMoving || body == null || attendance.MovedOn >= 8)
            {
                return;
            }
            VehicleComponent car = body.GetComponentInParent<VehicleComponent>();
            if (!IsUsable(car) || car.GetCurrentSpeed() > 1f
                || (held < HeldBeforeMovingSeen && StreetSpots.IsInView(car.transform.position)))
            {
                return;
            }
            if (attendance.MovedOn == 0)
            {
                Logger?.Info($"Accident: the {Name(attendance)} is held up by standing traffic with {driver.RouteLeft:F0} m to go; what is in its way is moved on.");
            }
            attendance.MovedOn++;
            TrafficTools.Remove(car, "standing in a response unit's way");
        }

        // The unit has stopped: where it meant to, behind something close to it, or out of time. It
        // parks there, becomes solid for people on foot, and its crew gets out and walks whatever is left.
        private void Pulled(Attendance attendance, EmergencyDriver driver, float fromScene, float now)
        {
            ResponseUnit unit = attendance.Unit;
            float left = attendance.AtStand ? driver.RouteLeft : Mathf.Max(0f, fromScene - attendance.StopDistance);
            string how = driver.Arrived ? (left < 3f ? "at its place" : $"held up {left:F0} m short of its place")
                : now >= attendance.At ? $"out of time, {left:F0} m short of its place"
                : $"stopped {left:F0} m short of its place";
            bool crew = left < CrewWalkLimit;

            unit.Park();
            SceneObstacles.Add(unit.Car, transform);
            attendance.Stage = Stage.OnScene;
            attendance.At = Mathf.Min(_deadline, now + StayTime(attendance.Police));
            _attended = true;
            if (attendance.Police)
            {
                _chase = Chase.None;   // too late for anyone to run
            }
            Logger?.Info($"Accident: the {Name(attendance)} parked {fromScene:F0} m from the scene ({how}"
                         + (attendance.MovedOn > 0 ? $", {attendance.MovedOn} car(s) moved out of its way" : "")
                         + (crew ? ")." : "); too far to walk, its crew stays aboard."));

            // A self-check for the log: a unit of ours must never stand in anything.
            // (Its whole body against every solid layer: a sign overhanging the road counts as much as a post.)
            Collider touching = SceneParking.Standing(unit.Car, unit.Car.transform.position, unit.Car.transform.forward, 0f);
            if (touching != null && !_clearLogged)
            {
                _clearLogged = true;
                Logger?.Warn($"Accident: the {Name(attendance)} is parked touching '{touching.name}' (layer {LayerMask.LayerToName(touching.gameObject.layer)}).");
            }
            OnUnitParked(attendance, crew, now);
        }

        // The patrol car stays while the road is taped off and the ambulance is still working: someone has to hold the traffic.
        private bool MayLeave(Attendance attendance, float now)
        {
            return !attendance.Police || !_cordon.Up || _ambulance.Going || now >= _deadline - 10f;
        }

        // The crew is aboard (or out of time), and from here a unit never stands about: it leaves forwards,
        // within seconds, and is given back to traffic only once it is moving along a lane beyond the scene.
        // The ambulance goes first and at once, siren on: the tape is taken in, bystanders are sent to the
        // kerb, the officers are called back, and it drives on along its own lane: straight through where the
        // scene is if that lane is clear, round the wrecks by the lane beside them if that is (held empty for
        // it), and otherwise the wrecks standing in its lane are taken off the road first. It never reverses.
        // The patrol car is the last to go: the tape comes down, and it drives off the same way when the scene
        // clears round it a few seconds later.
        private void Depart(Attendance attendance, float now)
        {
            _cordon.LiftTaper();   // the cones are the first thing taken in
            _conesIn = true;
            _people.OnUnitLeft(attendance.Police);
            attendance.Still = 0f;
            bool sceneAhead = attendance.AtStand && (_cordon.Up || _wrecks.Count > 0);
            if (attendance.Police)
            {
                if (sceneAhead)
                {
                    Stand(attendance, now, waits: true);
                    if (_ambulance.Going)
                    {
                        // The officers are aboard and the ambulance is done: the tape comes down now, and the scene clears a few
                        // seconds later with the patrol car driving off through it, instead of everything going in the same instant.
                        _cordon.Lower();
                        _lingerUntil = Mathf.Max(_lingerUntil, now + Random.Range(TapeDownFor, TapeDownFor + 3f));
                    }
                    Logger?.Info("Accident: the officers are aboard; the patrol car drives off through the scene as soon as it is cleared.");
                }
                else if (!Leave(attendance, now))
                {
                    Stand(attendance, now, waits: false);
                }
                return;
            }

            CollectCasualty();
            _people.StandAside(now);
            if (_police.Stage == Stage.OnScene && !_police.Recalled)
            {
                _police.At = Mathf.Min(_police.At, now);   // nothing is left to guard: the officers pack up as it goes
            }
            _exitWrecks.Clear();
            if (sceneAhead)
            {
                _cordon.Lower();
                PlanExit(attendance);
            }
            Stand(attendance, now, waits: false);
            attendance.At = now + ClearInView;
            TryToGo(attendance, now);
        }

        // Which way a loaded ambulance gets past the scene in front of it, said once in the log.
        private void PlanExit(Attendance attendance)
        {
            VehicleComponent car = attendance.Unit.Car;
            TrafficTools.Footprint(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float reach = Mathf.Clamp(_cordon.Through(centre, forward) + 6f, halfLength + 8f, 70f);
            Collider own = SceneParking.InStrip(centre + forward * (halfLength + 0.3f), centre + forward * reach, halfWidth + ExitMargin, car.rb);
            attendance.OwnLane = true;   // (unless it is planned round the scene by the lane beside, below)
            string way;
            if (own == null)
            {
                way = "straight on through the scene by its own lane, which is clear";
            }
            else
            {
                float side = 0f;
                for (int i = 0; i < 2 && side == 0f && TrafficManager.HasInstance; i++)
                {
                    float tried = i == 0 ? -1f : 1f;   // the left first: in most streets the lane running the other way
                    Vector3 over = right * (tried * LaneAcross);
                    if (TrafficTools.ClosestWaypoint(centre + forward * (halfLength + 6f) + over, 1.7f) != null
                        && SceneParking.InStrip(centre - forward * halfLength + over, centre + forward * reach + over, halfWidth + ExitMargin, car.rb) == null)
                    {
                        side = tried;
                    }
                }
                if (side != 0f)
                {
                    attendance.OwnLane = false;   // it pulls out into that lane to get by: the one case where it may
                    _cordon.HoldGateShut();   // nothing more is let past the scene into that lane until it is through
                    way = "round the scene by the lane on its " + (side < 0f ? "left" : "right") + ", which is clear and held empty for it";
                }
                else
                {
                    for (int i = 0; i < _wrecks.Count; i++)
                    {
                        if (InLane(_wrecks[i], centre, forward, right, halfLength, halfWidth + ExitMargin, reach))
                        {
                            _exitWrecks.Add(_wrecks[i]);
                        }
                    }
                    way = _exitWrecks.Count > 0
                        ? _exitWrecks.Count + " vehicle(s) held at the scene stand in its lane and no lane beside it is clear: those are taken off the road first"
                          + " (out of view at once, in view after " + ClearInView.ToString("F0") + " s at most)"
                        : "'" + own.name + "' (layer " + LayerMask.LayerToName(own.gameObject.layer) + ") is in its lane: it goes as far as it can and passes where a lane allows";
                }
            }
            Logger?.Info("Accident: the ambulance is loaded; tape taken in, and it leaves forwards, siren on: " + way + ".");
        }

        // True if a held vehicle lies across the lane ahead of a unit: its real footprint against the strip the unit will drive.
        private static bool InLane(Wreck wreck, Vector3 from, Vector3 forward, Vector3 right, float halfLength, float halfWidth, float reach)
        {
            if (wreck == null || wreck.Car == null)
            {
                return false;
            }
            TrafficTools.Footprint(wreck.Car, out Vector3 centre, out Vector3 along, out float length, out float width);
            Vector3 across = Vector3.Cross(Vector3.up, along);
            Vector3 offset = centre - from;
            float ahead = Vector3.Dot(offset, forward);
            float aside = Mathf.Abs(Vector3.Dot(offset, right));
            float deep = Mathf.Abs(Vector3.Dot(along, forward)) * length + Mathf.Abs(Vector3.Dot(across, forward)) * width;
            float wide = Mathf.Abs(Vector3.Dot(along, right)) * length + Mathf.Abs(Vector3.Dot(across, right)) * width;
            return ahead + deep > halfLength && ahead - deep < reach && aside < halfWidth + wide;
        }

        // Standing with its crew aboard: parked (kinematic, so nothing bounces), lit, and watched. With 'waits' it goes
        // when the scene clears round it; without, it tries to go every second and is never left there (BoxedOut).
        private void Stand(Attendance attendance, float now, bool waits)
        {
            ResponseUnit unit = attendance.Unit;
            unit.Driver.Stop();
            unit.Park(keepSiren: !waits && !attendance.Police);
            attendance.Stage = Stage.Ready;
            attendance.Waits = waits;
            attendance.Through = false;
            attendance.NextCheck = 0f;
            if (attendance.Still <= 0f)
            {
                attendance.Still = now;
            }
            if (!waits)
            {
                TrafficWatch.Guard(unit.Car, Name(attendance));
            }
        }

        // A unit that is ready and free to go: the wrecks in its lane first (each as soon as nobody is looking that
        // way, and in any case by the time it was given), then away. One that still cannot go is not left standing.
        private void TryToGo(Attendance attendance, float now)
        {
            bool pending = false;
            for (int i = _exitWrecks.Count - 1; i >= 0 && !attendance.Police; i--)
            {
                Wreck wreck = _exitWrecks[i];
                if (wreck.Car == null || !_wrecks.Contains(wreck))
                {
                    _exitWrecks.RemoveAt(i);
                }
                else if (now >= attendance.At || !StreetSpots.IsInView(wreck.Car.transform.position))
                {
                    Tow(wreck, "towed out of the loaded ambulance's lane");
                }
                else
                {
                    pending = true;
                }
            }
            if (pending || Leave(attendance, now))
            {
                return;
            }
            if (now - attendance.Still > BoxedIn)
            {
                BoxedOut(attendance, now, "there is no lane of road ahead of it to leave by");
            }
        }

        // Drives a standing unit away, forwards, along the lane ahead of it. False (nothing changed) if there is no
        // such lane. It is still the mod's to drive: the Leaving stage hands it back once it is clear and moving.
        private bool Leave(Attendance attendance, float now)
        {
            ResponseUnit unit = attendance.Unit;
            VehicleComponent car = unit.Car;
            Vector3 at = car.transform.position;
            Vector3 facing = car.transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f || !TrafficTools.Ready)
            {
                return false;
            }
            facing.Normalize();
            float nose = Mathf.Max(1.5f, car.length * 0.5f) + 1f;
            // (The direction is given long: the search only takes a lane's direction into account for one longer than a metre.)
            Waypoint ahead = TrafficTools.ClosestWaypointAlong(at + facing * (nose + 3f), facing * 2f, 30f);
            bool found = ahead != null && TrafficTools.WalkForward(ahead, LeaveDistance + 40f, 60, _routeScratch) > 20f;
            if (found)
            {
                // Only road that is in front of its nose: it is never sent back to a point it has passed.
                unit.Path.Clear();
                for (int i = 0; i < _routeScratch.Count; i++)
                {
                    Vector3 point = _routeScratch[i].position;
                    if (unit.Path.Count > 0 || Vector3.Dot(point - at, facing) > nose)
                    {
                        unit.Path.Add(point);
                    }
                }
                found = unit.Path.Count >= 2;
            }
            _routeScratch.Clear();
            if (!found)
            {
                return false;
            }
            SceneObstacles.Remove(car);
            unit.Unpark(quiet: attendance.Police);
            unit.Driver.Begin(car, unit.Path, unit.Path[unit.Path.Count - 1], 6f);
            float through = _cordon.Surveyed ? _cordon.Through(at, facing) : 0f;
            attendance.Through = through > 0f && through < 90f;
            unit.Driver.CruiseSpeed = attendance.Through ? ExitPace : EmergencyDriver.DefaultCruiseSpeed;
            // Through the scene it keeps to the lane that was checked for it: it closes up and waits behind whatever turns
            // up there, and never pulls out into the lane beside, where the other unit stands. (The patrol car likewise;
            // only an ambulance planned round the wrecks by the lane beside them may pull out.)
            unit.Driver.MayPass = !attendance.Through || (!attendance.Police && !attendance.OwnLane);
            attendance.Stage = Stage.Leaving;
            attendance.Waits = false;
            attendance.Backing = false;
            attendance.From = at;
            attendance.NextCheck = 0f;
            attendance.SetOffAt = now;
            attendance.At = now + LeaveAllowance + (attendance.Through ? 10f : 0f);
            TrafficWatch.Guard(car, Name(attendance));
            Logger?.Info($"Accident: the {Name(attendance)} is leaving, forwards along its lane"
                         + (attendance.Through ? $" ({through:F0} m to the far side of the scene, at {ExitPace * 3.6f:F0} km/h until it is past)." : "."));
            SetOffCheck(attendance, at, facing, through);
            return true;
        }

        // The departure check as a unit sets off: what stands in the lane it leaves by (as far as the far side of the scene),
        // where the other unit is against that lane, and whether the order is being kept.
        private void SetOffCheck(Attendance attendance, Vector3 at, Vector3 facing, float through)
        {
            if (_phase == Phase.None && !attendance.Through)
            {
                return;   // driving off an open road after the scene: nothing to check
            }
            VehicleComponent car = attendance.Unit.Car;
            Attendance other = attendance.Police ? _ambulance : _police;
            TrafficTools.Footprint(car, out Vector3 centre, out _, out float halfLength, out float halfWidth);
            float reach = Mathf.Clamp(through + 6f, halfLength + 8f, 70f);
            Collider ahead = SceneParking.InStrip(centre + facing * (halfLength + 0.3f), centre + facing * reach, halfWidth + ExitMargin, car.rb, SceneParking.RoadsideMask);
            bool otherThere = other.Unit.InUse && other.Unit.Car != null && !other.Past;
            string where = "the other unit is not at the scene";
            bool otherAhead = false;
            if (otherThere)
            {
                TrafficTools.Footprint(other.Unit.Car, out Vector3 theirs, out _, out _, out float theirHalf);
                Vector3 offset = theirs - centre;
                offset.y = 0f;
                float along = Vector3.Dot(offset, facing);
                float aside = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, facing))) - halfWidth - theirHalf;
                otherAhead = along > 0f && along < reach + 6f && aside < ExitMargin;
                where = $"the {Name(other)} is {along:F0} m {(along >= 0f ? "ahead" : "behind")}, {aside:F1} m clear of its side ({ExitMargin:F2} m needed)";
            }
            bool order = attendance.Police ? _ambulance.Past : true;
            string step = Name(attendance) + " set off";
            DepartureCheck(step, ahead == null && !otherAhead && order,
                (attendance.OwnLane || attendance.Police ? "by its own lane, which it keeps to" : "round the wrecks by the lane beside them")
                + (ahead == null ? $"; nothing stands in it for the {reach:F0} m to beyond the scene"
                    : $"; '{ahead.name}' (layer {LayerMask.LayerToName(ahead.gameObject.layer)}) stands in it: it closes up, and what is in the way is moved")
                + "; " + where
                + (attendance.Police ? (order ? "; the ambulance is past the scene" : "; the ambulance is NOT yet past the scene") : "; the ambulance goes first")
                + (_cordon.Closed ? "; the road is still closed to traffic" : "; the road is open"));
        }

        // A unit that is driving away and has stopped behind something: a vehicle held at the scene is towed out of its
        // way, ordinary traffic that is standing there is moved on (as on its way in): at once out of view, after a few
        // seconds in view. Nothing is done about a person (it waits) or about anything that is not traffic.
        private void ClearExit(Attendance attendance, EmergencyDriver driver)
        {
            float held = driver.HeldFor;
            Rigidbody body = driver.Blocker;
            if (held < HeldBeforeMoving || body == null)
            {
                return;
            }
            VehicleComponent car = body.GetComponentInParent<VehicleComponent>();
            if (car == null || (held < HeldBeforeMovingSeen && StreetSpots.IsInView(car.transform.position)))
            {
                return;
            }
            for (int i = 0; i < _wrecks.Count; i++)
            {
                if (_wrecks[i].Car == car)
                {
                    Tow(_wrecks[i], "towed out of the way of the " + Name(attendance) + " as it leaves");
                    return;
                }
            }
            if (IsUsable(car) && car.GetCurrentSpeed() <= 1f && attendance.MovedOn < 12)
            {
                attendance.MovedOn++;
                TrafficTools.Remove(car, "standing in the way of a response unit that is leaving");
            }
        }

        // Takes one held vehicle off the road while the scene goes on (TrafficTools.Remove says so in the log).
        private void Tow(Wreck wreck, string why)
        {
            _wrecks.Remove(wreck);
            _exitWrecks.Remove(wreck);
            VehicleComponent car = wreck.Car;
            Unpin(wreck);
            if (car != null)
            {
                _involved.Remove(car.rb);
                if (TrafficTools.IsHeld(car))
                {
                    TrafficTools.Remove(car, "the accident scene: " + why);
                }
                _released.Add(car);
            }
            _leadWreck = _leadWreck == wreck ? null : _leadWreck;
            _worstWreck = _worstWreck == wreck ? null : _worstWreck;
            _otherWreck = _otherWreck == wreck ? null : _otherWreck;
        }

        // A unit with its crew aboard that has stood too long and cannot get away: it is taken off the road rather
        // than left there. Out of view at once; in view it is given a few seconds more to be out of view, then goes
        // anyway. Said once, with why.
        private void BoxedOut(Attendance attendance, float now, string why)
        {
            ResponseUnit unit = attendance.Unit;
            bool seen = unit.Car != null && Seen(unit.Car.transform.position);
            if (seen)
            {
                // Never in front of the player: a response vehicle is not made to vanish while somebody may be looking. It
                // stays, lit, and goes on trying to drive off (the unit in its way is moved: GiveWay; traffic in its way is
                // moved on: ClearExit); it is taken off the road only once nobody can see it. Said once.
                if (!attendance.SaidHeld)
                {
                    attendance.SaidHeld = true;
                    DepartureCheck(Name(attendance) + " held up", false, $"it has stood {now - attendance.Still:F0} s with its crew aboard and cannot get away ({why}); "
                        + "it is in view, so it is not taken off the road: it keeps trying to drive off, and goes only when nobody can see it");
                }
                return;
            }
            Logger?.Warn($"Accident: the {Name(attendance)} stood {now - attendance.Still:F0} s with its crew aboard and could not get away ({why}); "
                         + "it is taken off the road out of view.");
            ReleaseExit(attendance);
            SceneObstacles.Remove(unit.Car);
            unit.Release(resumeTraffic: true, remove: true);
            attendance.Stage = Stage.Done;
        }

        /// <summary>
        /// For TrafficWatch: a response vehicle of this scene has not moved for too long with its crew aboard. It
        /// is taken off the road as a boxed-in unit is (out of view at once, in view after a few seconds more).
        /// </summary>
        public void Unstick(VehicleComponent car)
        {
            Attendance attendance = _ambulance.Unit.Car == car ? _ambulance : _police.Unit.Car == car ? _police : null;
            if (attendance == null || !attendance.Unit.InUse || (attendance.Stage == Stage.Ready && attendance.Waits && _phase != Phase.None)
                || (attendance.Stage != Stage.Ready && attendance.Stage != Stage.Leaving))
            {
                return;   // not ours, at work, or the patrol car waiting its few seconds for the scene to clear
            }
            if (attendance.Still <= 0f)
            {
                attendance.Still = Time.time - BoxedIn;
            }
            BoxedOut(attendance, Time.time, "the traffic watch found it standing");
        }

        private static readonly int HotDepart = HotSpots.Slot("Traffic: TrafficAccidents.TickDepartures");   // (inside its Update: names the stage that took a long frame)
        // After a scene has ended: a unit that is still driving away from it (or looking for a way to) is carried on
        // until traffic has it back, moving, or it is taken off the road. Nothing else of the scene is left by then.
        private void TickDepartures(float now)
        {
            using var hotSpot = HotSpots.Time(HotDepart);
            try
            {
                if (!TrafficTools.Ready)
                {
                    EndDepartures();
                    return;
                }
                for (int i = 0; i < 2; i++)
                {
                    Attendance attendance = i == 0 ? _ambulance : _police;
                    if (!attendance.Unit.InUse)
                    {
                        continue;
                    }
                    if (attendance.Stage == Stage.Ready || attendance.Stage == Stage.Leaving)
                    {
                        TickAttendance(attendance, now);
                    }
                    else
                    {
                        attendance.Unit.Release(resumeTraffic: true, remove: true);
                        attendance.Stage = Stage.Idle;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                EndDepartures();
            }
        }

        // Whatever unit is still out from a scene that is over is let go now: back to traffic if it is moving along its
        // lane, off the road if it is standing.
        private void EndDepartures()
        {
            for (int i = 0; i < 2; i++)
            {
                Attendance attendance = i == 0 ? _ambulance : _police;
                ResponseUnit unit = attendance.Unit;
                if (!unit.InUse)
                {
                    continue;
                }
                bool moving = attendance.Stage == Stage.Leaving && unit.Driver.Active && unit.Driver.Speed > MovingOff;
                _cordon.ReleaseExit();
                unit.Release(TrafficTools.Ready, remove: !moving, quiet: true);
                attendance.Stage = Stage.Idle;
            }
        }

        // The scene is over (Clear): what happens to a unit that is still out. One that is moving goes back to traffic
        // as it is, or carries on leaving. One that is standing is never handed back like that: in view it drives off
        // along its lane (the road is open and the wrecks are gone by now) and is handed back once it is moving; out
        // of view, or with no traffic to drive in, it is taken off the road.
        private void SendOff(Attendance attendance, bool resumeTraffic, bool drive)
        {
            ResponseUnit unit = attendance.Unit;
            if (!unit.InUse)
            {
                attendance.Stage = Stage.Idle;
                return;
            }
            drive &= resumeTraffic && !_closing && TrafficTools.Ready && !unit.IsLost;
            if (attendance.Backing)
            {
                // Backing out of the ambulance's way when the scene ended: it stops, and goes on from standing like any other.
                attendance.Backing = false;
                unit.Driver.Stop();
            }
            if (drive && attendance.Stage == Stage.Leaving)
            {
                return;   // on its way already: TickDepartures sees it off
            }
            if (drive && unit.Driver.Active && unit.Driver.Speed > MovingOff)
            {
                unit.Release(resumeTraffic: true, remove: false, quiet: true);   // still on its way in, and moving
                attendance.Stage = Stage.Idle;
                return;
            }
            if (drive && Seen(unit.Car.transform.position))
            {
                try
                {
                    float now = Time.time;
                    attendance.Still = 0f;
                    attendance.OwnLane = true;
                    if (!Leave(attendance, now))
                    {
                        Stand(attendance, now, waits: false);
                    }
                    return;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            unit.Release(resumeTraffic, remove: true, quiet: true);
            attendance.Stage = Stage.Idle;
        }

        // The lane held for a leaving ambulance is given back to traffic.
        private void ReleaseExit(Attendance attendance)
        {
            if (!attendance.Police)
            {
                _cordon.ReleaseExit();
            }
        }

        // Where a unit is going to stand, tested before it sets off: its whole body and a margin all round against
        // everything solid (vehicles, sign posts, hydrants, poles, bins, buildings), and no crossing under it. A place
        // that fails is moved back along the unit's own lane, a step at a time: never sideways, never to a kerb. The
        // route is cut to end there. Said once if anything was in the way.
        private void SettleStand(Attendance attendance)
        {
            ResponseUnit unit = attendance.Unit;
            List<Vector3> path = unit.Path;
            VehicleComponent car = unit.Car;
            float halfLength = Mathf.Max(1.5f, car.length * 0.5f);
            string inWay = null;
            for (float back = 0f; back <= StandBackMost && path.Count >= 2; back += StandStep)
            {
                // The point so far back along the route from its end, and the way the lane runs there.
                int index = path.Count - 1;
                float left = back;
                Vector3 along = default;
                float leg = 0f;
                while (index > 0)
                {
                    along = path[index] - path[index - 1];
                    along.y = 0f;
                    leg = along.magnitude;
                    if (leg > 0.05f && leg >= left)
                    {
                        break;
                    }
                    left -= leg;
                    index--;
                }
                if (index <= 0)
                {
                    break;   // the route is no longer than that
                }
                along /= leg;
                Vector3 point = path[index] - along * left;
                Collider hit = SceneParking.Standing(car, point, along, StandMargin);
                bool crossing = hit == null && SceneParking.OnCrossing(point, along, halfLength);
                if (hit == null && !crossing)
                {
                    if (back > 0f)
                    {
                        path.RemoveRange(index, path.Count - index);
                        path.Add(point);
                        Logger?.Info($"Accident: the place planned for the {Name(attendance)} failed its clear test ({inWay}); it stands {back:F1} m further back along its own lane instead.");
                    }
                    return;
                }
                if (inWay == null)
                {
                    inWay = crossing ? "it lies on a crossing"
                        : "'" + hit.name + "' on layer " + LayerMask.LayerToName(hit.gameObject.layer) + " within " + StandMargin.ToString("F1") + " m of its body";
                }
            }
            if (inWay != null)
            {
                Logger?.Warn($"Accident: no place for the {Name(attendance)} within {StandBackMost:F0} m back along its lane passed the clear test ({inWay}); it stands as planned.");
            }
        }

        private const float PassedBy = 7f;          // metres beyond the far end of the tape at which it has passed the scene

        // The cones go out once the units are in: upstream of each end, beyond whatever stands there. An end
        // that a unit is still on its way to is left without until it has parked (it would drive through them).
        private void LayTaper()
        {
            if (!_cordon.Up || _conesIn)
            {
                return;
            }
            float near = 0f;
            float far = 0f;
            bool nearDue = false;
            bool farDue = false;
            for (int i = 0; i < 2; i++)
            {
                Attendance attendance = i == 0 ? _ambulance : _police;
                if (attendance.Stage == Stage.Waiting || attendance.Stage == Stage.EnRoute)
                {
                    nearDue |= !attendance.FarEnd;
                    farDue |= attendance.FarEnd;
                }
                else if ((attendance.Stage == Stage.OnScene || attendance.Stage == Stage.Ready) && attendance.Unit.Car != null)
                {
                    // (Behind the ambulance the road is kept free of cones for longer: its crew brings the stretcher round to the rear doors there.)
                    TrafficTools.Footprint(attendance.Unit.Car, out Vector3 centre, out _, out float halfLength, out _);
                    _cordon.Reach(centre, halfLength + (attendance.Police ? 0f : RearDoorRoom), ref near, ref far);
                }
            }
            _cordon.LayTaper(nearDue ? -1f : near, farDue ? -1f : far);
        }

        // Stay as long as the severity calls for; the two units roll separately, so either may leave first.
        private float StayTime(bool police)
        {
            if (police)
            {
                return _severity == Severity.Severe ? Random.Range(40f, 70f)
                    : _severity == Severity.Moderate ? Random.Range(25f, 50f)
                    : Random.Range(20f, 35f);
            }
            return _severity == Severity.Severe ? Random.Range(45f, 70f) : Random.Range(25f, 45f);
        }

        private static readonly int HotParked = HotSpots.Slot("Traffic: TrafficAccidents.OnUnitParked");   // (inside its Update: names the stage that took a long frame)
        /// <summary>
        /// A unit has stopped and parked. This is the one place that knows it, so this is where
        /// its crew gets out (and walks in, however far short of the scene it stopped), and
        /// where the police put the tape up.
        /// </summary>
        /// <param name="crew">False only if it stopped too far away for anyone to walk in.</param>
        private void OnUnitParked(Attendance attendance, bool crew, float now)
        {
            using var hotSpot = HotSpots.Time(HotParked);
            // (No tape once the ambulance has loaded and gone, or is going: it would be put up across the lane it is driving out by.)
            bool ambulanceGone = _ambulance.Stage == Stage.Ready || _ambulance.Stage == Stage.Leaving || _ambulance.Stage == Stage.Done;
            if (attendance.Police && _cordon.Closed && !_cordon.Up && !ambulanceGone)
            {
                _cordon.Raise(now);
                _people.OnCordonRaised(_cordon);
                Notice(NotificationType.Info, "reallife_accident_closed", 6f);
            }
            else if (attendance.Police)
            {
                Notice(NotificationType.Info, "reallife_accident_police", 6f);
            }
            else
            {
                Notice(NotificationType.Info, "reallife_accident_paramedics", 6f);
            }
            LayTaper();
            ParkedCheck();
            if (!crew)
            {
                return;
            }

            // The crew says how long its work will take; the unit waits at least that long. An ambulance
            // crew with someone on the ground may take the scene past its planned time (see Tick).
            float limit = !attendance.Police && (_people.PatientDown || (_casualty != null && _casualty.Present)) ? _hardLimit - 20f : _deadline;
            float busyUntil = _people.OnUnitParked(attendance.Police, attendance.Unit.Car, limit - 8f, now);
            attendance.At = Mathf.Min(limit - 6f, Mathf.Max(attendance.At, busyUntil));
        }

        private static readonly int HotDispatch = HotSpots.Slot("Traffic: TrafficAccidents.Dispatch");   // (inside its Update: names the stage that took a long frame)
        private void Dispatch(Attendance attendance, float now)
        {
            using var hotSpot = HotSpots.Time(HotDispatch);
            ResponseUnit unit = attendance.Unit;
            TrafficManager traffic = TrafficManager.Instance;

            // Where it is going. A patrol car that will end up chasing comes up the suspect's own lane, so
            // its route runs on into the chase. Any other unit is given a place to stand in a lane the
            // wrecks block, so that its route ends on the scene's own carriageway, in sight of it. Only
            // a scene with no such lane (not on a road) falls back on the nearest road point.
            bool chasing = attendance.Police && _chase == Chase.Planned;
            bool ambulanceThere = _ambulance.Stage != Stage.Idle && _ambulance.Stage != Stage.Done;
            float back = !attendance.Police ? AmbulanceBack : attendance.FarEnd || !ambulanceThere ? PatrolBackAlone : PatrolBack;
            Waypoint anchor = null;
            Vector3 stand = default;
            bool hasStand = !chasing && _cordon.Stand(attendance.FarEnd, back, out anchor, out stand);
            if (!hasStand)
            {
                anchor = chasing ? _fleeAnchor : TrafficTools.ClosestWaypoint(_scene, 40f);
            }
            bool spawned = attendance.Police
                ? unit.Spawn(UnitKind.Police, anchor, Random.Range(140f, 200f), attendance.Retried ? 25f : 50f)
                : unit.Spawn(UnitKind.Ambulance, anchor, AmbulanceRouteLength, attendance.Retried ? 30f : 60f);   // a second try settles for a shorter run-in

            if (spawned)
            {
                attendance.AtStand = hasStand;
                if (hasStand)
                {
                    Vector3 beyond = stand - anchor.position;
                    beyond.y = 0f;
                    if (beyond.sqrMagnitude > 1f)
                    {
                        unit.Path.Add(stand);
                    }
                    SettleStand(attendance);   // in its lane, and clear of everything solid by its whole body: else further back along that lane
                    unit.Driver.BeginRoute(unit.Car, unit.Path, CrewWalk);
                }
                else
                {
                    unit.Driver.Begin(unit.Car, unit.Path, _scene, attendance.StopDistance, CrewWalk);
                }
                attendance.Stage = Stage.EnRoute;
                attendance.At = Mathf.Min(now + DriveAllowance, _hardLimit - 60f);
                Logger?.Info($"Accident: {unit.Car.prefab.name} dispatched as {unit.Kind}, {unit.Path.Count} road points, "
                             + (hasStand ? $"to stand {back:F0} m from the wrecks at the {(attendance.FarEnd ? "far" : "near")} end."
                                 : chasing ? "up the lane of the driver who will run."
                                 : "no lane to stand in here: it pulls up where it can."));
            }
            else if (!attendance.Retried)
            {
                // The spawn point may just have been occupied (often by the other unit): try once more on another route.
                attendance.Retried = true;
                attendance.At = now + 6f;
                Logger?.Info($"Accident: no {Name(attendance)} could be put on the road ({unit.Refusal}); trying once more, with a shorter run-in.");
            }
            else
            {
                // No such vehicle to be had, or no route: nothing else is sent in its place, and the scene runs its time.
                Logger?.Info($"Accident: no {Name(attendance)} available ({unit.Refusal}); the scene runs without one.");
                Withdraw(attendance);
            }
        }

        // The unit is not coming, or is gone. Without a patrol car there is nobody to run from.
        private void Withdraw(Attendance attendance)
        {
            ReleaseExit(attendance);
            _people.OnUnitLeft(attendance.Police);
            SceneObstacles.Remove(attendance.Unit.Car);
            attendance.Unit.Release(resumeTraffic: true, remove: false);
            attendance.Stage = Stage.Done;
            if (attendance.Police && _chase == Chase.Planned)
            {
                _chase = Chase.None;
            }
            LayTaper();   // an end it was due at need not be kept clear for it any more
        }

        // ---------------------------------------------------------------- hit-and-run

        // Works out where the lead car would run to. No road to run on means no hit-and-run.
        private void PlanChase(VehicleComponent car)
        {
            try
            {
                // The waypoint the car was driving towards: ahead of it, in its own lane. (A car that came here in the mod's
                // hands has not been the traffic system's for a while, and what that remembers of it is behind it: the
                // lane point ahead of its nose is asked for instead. One walk of the road graph, on this event only.)
                Vector3 nose = car.transform.forward;
                nose.y = 0f;
                Waypoint anchor = _inPlace && nose.sqrMagnitude > 0.01f
                    ? TrafficTools.ClosestWaypointAlong(car.transform.position + nose.normalized * (Mathf.Max(1.5f, car.length * 0.5f) + 3f), nose.normalized * 2f, 12f)
                    : TrafficManager.Instance.GetTargetWaypoint(car.GetIndex());
                if (anchor == null || TrafficTools.WalkForward(anchor, FleeRouteLength, 500, _routeScratch) < FleeRouteMinimum)
                {
                    return;
                }
                _fleePath.Clear();
                for (int i = 0; i < _routeScratch.Count; i++)
                {
                    _fleePath.Add(_routeScratch[i].position);
                }
                _routeScratch.Clear();
                _fleeAnchor = anchor;
                _chase = Chase.Planned;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _chase = Chase.None;
            }
        }

        // The patrol car is nearly at the scene: the lead driver takes off and the patrol car goes after it.
        private void BeginChase(float now)
        {
            if (_leadWreck == null || _leadWreck.Car == null)
            {
                _chase = Chase.None;
                return;
            }
            VehicleComponent car = _leadWreck.Car;
            _people.OnDriverFled();   // back behind the wheel
            Unpin(_leadWreck);
            _wrecks.Remove(_leadWreck);
            _leadWreck = null;
            if (_fleePath.Count == 0 || !_suspect.Take(car))
            {
                // Cannot happen in practice; if it does, the car simply rejoins traffic.
                TrafficTools.ReturnToTraffic(car, "the accident scene: the hit-and-run could not start");
                _chase = Chase.None;
                return;
            }

            _suspect.Path.AddRange(_fleePath);
            EmergencyDriver flee = _suspect.Driver;
            flee.Begin(car, _suspect.Path, _fleePath[_fleePath.Count - 1], 6f);
            flee.CruiseSpeed = SuspectSpeed;

            // Same road points, a little faster, and never past the car it is following.
            ResponseUnit patrol = _police.Unit;
            patrol.Path.AddRange(_fleePath);
            patrol.Driver.Follow(car.rb, ChaseGap);
            patrol.Driver.CruiseSpeed = ChaseSpeed;

            _police.Stage = Stage.Chasing;
            _chase = Chase.Running;
            _fled = true;
            bool caught = Random.value < 0.55f;
            _pursuit.Begin(_suspect, patrol, _arrest, caught, Mathf.Min(now + Random.Range(20f, 45f), _deadline - 22f), arrest: true, "reallife_chase_caught");
            News.Report(NewsKind.HitAndRun, _scene);
            Logger?.Info($"Accident: hit-and-run, {car.prefab.name} is running; will be {(caught ? "caught" : "lost")}.");
            Notice(NotificationType.Warning, "reallife_chase_start", 8f);
        }

        // The chase itself (when the suspect brakes, when the two are held, the arrest at the roadside, when it is
        // over) is RoadChase, which a reckless driver the police have seen is chased by as well.
        private void TickChase(float now)
        {
            if (_pursuit.Tick(now, out bool caught))
            {
                EndChase(caught, now);
            }
        }

        // Caught: the suspect's car is taken away and the patrol car leaves. Escaped: the suspect
        // melts back into traffic and the patrol car gives up.
        private void EndChase(bool caught, float now)
        {
            if (!caught)
            {
                Notice(NotificationType.Info, "reallife_chase_escaped", 8f);
            }
            _arrest.Clear();
            _suspect.Release(resumeTraffic: true, remove: caught);
            // The patrol car: still moving, traffic has it back as it goes; standing behind the car it stopped, it
            // drives off along its lane first (or is taken off the road out of view): never handed back standing.
            _police.AtStand = false;
            SendOff(_police, resumeTraffic: true, drive: true);
            if (_police.Stage == Stage.Idle)
            {
                _police.Stage = Stage.Done;
            }
            _chase = Chase.None;
            _pursuit.Reset();

            // The scene the suspect left behind stays a little longer, then clears if nobody else is working there.
            _attended = true;
            _lingerUntil = now + Random.Range(6f, 14f);
        }

        // ---------------------------------------------------------------- clearing up

        private static readonly int HotClear = HotSpots.Slot("Traffic: TrafficAccidents.Clear");   // (inside its Update: names the stage that took a long frame)
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
                // The last line of the departure check: the road is given back only with the ambulance beyond the scene (or off the
                // road), and the patrol car either gone or standing in its own lane ready to drive off with the traffic.
                bool ambulanceAway = _ambulance.Past || !_ambulance.Unit.InUse;
                bool patrolAway = _police.Past || !_police.Unit.InUse;
                DepartureCheck("road reopened", ambulanceAway && !_police.Backing,
                    "the ambulance " + (!_ambulance.Unit.InUse ? "is off the road" : _ambulance.Past ? "is beyond the scene" : "is STILL at the scene (" + _ambulance.Stage + ")")
                    + "; the patrol car " + (patrolAway ? "is beyond the scene or off the road" : _police.Backing ? "is still backing out of the ambulance's way"
                        : _police.Stage == Stage.Ready ? "stands in its lane with its officers aboard and drives off now, with the traffic" : "is still at the scene (" + _police.Stage + ")")
                    + "; ended by: " + why);
            }

            // The road first: whatever else goes wrong below, it is open again, for traffic and for people on foot.
            _cordon.Open();
            SceneObstacles.Clear();

            // Then the people: they go in every case, whatever state the cars are in.
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
                // A wreck too damaged to drive off is handed to the tow truck (Traffic/Tow/TowService), which takes it away
                // where it can; where it cannot, it is taken off the road below, as it always was.
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
                    // Traffic shut down around it (indoors, fast-forward): the game cleared every car but ours,
                    // which it skips while they have a scripted route. Left here it would stand in the road,
                    // ignored by the traffic system, for as long as the player stays near: taken off like the rest.
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
            _mark.Hide();

            // Units still out: one that is moving goes back to traffic or carries on leaving; one that is standing drives
            // off along its lane now that the road is open (or is taken off the road): never handed back standing
            // (SendOff). A suspect already stopped is taken away.
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
