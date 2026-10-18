using System;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Accidents that happen for a reason, in view: now and then an ordinary traffic car near the player is driven recklessly
    /// (<see cref="RecklessCar"/>) or somebody steps out in front of one (<see cref="Jaywalker"/>), and whatever happens on
    /// the road follows: mostly nothing, a near miss, a crash for TrafficAccidents.BeginAt, somebody run down, or a patrol
    /// car that pulls the driver over or gives chase (<see cref="RoadChase"/>). How often is StreetRules'. One car and one
    /// person at most, never with another scene on, never changed in view; all given back at once (EndNow). Cost: idle,
    /// three flag tests and a time comparison a frame and a roll every 20 s; while out, a few comparisons a tick.
    /// </summary>
    public sealed partial class RecklessDrivers : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("streets", "RecklessDrivers", typeof(RecklessDrivers));

        private const float TickInterval = 0.25f;    // seconds
        private const float SettleMost = 14f;        // seconds a settled car is given to reach a moment at which handing it back will not show
        private const float EaseMost = 4f;           // ...and then to pull up, if none came
        private const float WaitForScene = 14f;      // seconds a driver who stopped for somebody they hit waits to be taken into the scene
        private const float FleeMost = 40f;          // seconds a driver who ran keeps running if no patrol car comes
        private const float ScareEvery = 1f;         // seconds between looks for people in its way
        private const float HornEvery = 3f;          // seconds
        private const float HornWithin = 16f;        // metres: traffic this near sounds its horn at what it has just done
        private const float PullUpWithin = 22f;      // metres behind a driver who is stopping at which the patrol car has closed up
        private const float NearCamera = 45f;        // metres: nothing is put on the road this near the camera, whichever way it looks
        private const float TestWithin = 200f;       // metres a test button looks for a car
        private const int CrowdMost = 10;            // people a test sends across the road at once

        private enum Phase { Idle, Driving, Settling, Stopping, Waiting, Fleeing, Chased }

        public static IModLogger Logger;
        public static RecklessDrivers Instance { get; private set; }

        /// <summary>True while the road is where accidents come from; TrafficAccidents and PedestrianIncidents roll their own only while it is false.</summary>
        public static bool IsSource => Instance != null && Instance._working && Settings.NaturalAccidents && Settings.TrafficAccidents;

        private readonly RoadsideStop _stop = new RoadsideStop();
        private readonly RoadChase _chase = new RoadChase();
        private readonly Jaywalker _jay = new Jaywalker();
        private readonly Jaywalker[] _crowd = new Jaywalker[CrowdMost];   // a test's ten people crossing at once
        private int _crowdOut;
        private readonly GameUi.GuideMark _mark = new GameUi.GuideMark("the reckless drivers' tests");
        private RecklessCar _car;
        private ResponseUnit _patrol;

        private Phase _phase = Phase.Idle;
        private bool _working = true;
        private int _faults;
        private float _nextCheck;
        private float _nextTick;
        private float _until;
        private float _nextScare;
        private float _nextHorn;
        private float _carsDue;          // car accidents owing (StreetRules.Accrue)
        private float _peopleDue;        // ...and people due to be hit
        private int _hurtSeen;           // PedestrianIncidents.TrafficHurt when last looked at
        private int _lightsSeen;
        private float _patrolAt;         // when a patrol car that has seen it sets off after it; 0 for never
        private bool _presenceLooked;
        private bool _complies;          // the driver stops for the patrol car
        private bool _chaseWanted;       // a driver who ran: a patrol car is put behind it once it is well clear of the scene
        private Vector3 _scene;          // where somebody it hit lies
        private float _waitUntil;
        private float _markUntil;
        private bool _marked;
        private string _markLabel;
        private VehicleComponent _blameless;
        private float _blamelessUntil;
        private VehicleComponent _striker;   // an ordinary car that hit somebody and was not taken over
        private VehicleComponent _struckBy;  // the car CarStrikes has just named (Struck), until the next frame
        private float _struckSpeed;
        private bool _struck;
        private bool _test;              // a test button started this one: it is told when a part of it cannot be done
        private bool _easing;            // settled, and now pulling up to be handed back standing
        private float _hurtAt;           // when it last ran somebody down who did not get up; 0 if it has not
        private float _jayAt;

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < _crowd.Length; i++)
            {
                _crowd[i] = new Jaywalker();
            }
            _car = gameObject.AddComponent<RecklessCar>();
            _mark.Keep(this);   // the marker goes with this component
            _patrol = new ResponseUnit(gameObject);
            _nextCheck = Time.time + StreetRules.CheckSeconds * 2f;
        }

        private void OnDestroy()
        {
            EndNow("the mod is closing");
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Traffic: RecklessDrivers.Update");
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
            float now = Time.time;
            if (_struck)
            {
                _struck = false;
                try
                {
                    OnHurt(now);
                }
                catch (Exception e)
                {
                    Fault(e);
                }
            }
            if (_phase == Phase.Idle && !_jay.Active && _crowdOut == 0)
            {
                if (now < _nextCheck)
                {
                    return;
                }
                _nextCheck = now + StreetRules.CheckSeconds;
                try
                {
                    Check(now);
                }
                catch (Exception e)
                {
                    Fault(e);
                }
                return;
            }
            try
            {
                if (_phase != Phase.Idle && _car.Struck != RecklessCar.Hit.None)
                {
                    OnStruck(now);
                }
                if (_jay.Pending)
                {
                    _jay.Tick(now);   // at the kerb, waiting for the moment to step out: looked at every frame, for seconds
                }
                if (now >= _nextTick)
                {
                    _nextTick = now + TickInterval;
                    Tick(now);
                }
                if (_marked)
                {
                    Mark(now);
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // Everything given back at once, so a fault cannot repeat; after three, TrafficAccidents' own roll takes over (IsSource).
        private void Fault(Exception e)
        {
            Logger?.Error(e);
            EndNow("an error");
            if (_working && Guard.StandsDown(e, "reckless drivers"))
            {
                _working = false;   // (a game that lacks something they name would fail at every look)
            }
            else if (++_faults >= 3 && _working)
            {
                _working = false;
                Logger?.Warn("Reckless drivers: switched off for this session after repeated errors (see above); accidents are staged out of view as before.");
            }
        }

        private void Check(float now)
        {
            if (!_working || !Settings.NaturalAccidents || !Settings.TrafficAccidents || Free() != TestRefusal.None)
            {
                return;
            }
            // The old roll's chance at this look falls due instead.
            int frequency = Settings.AccidentFrequency;
            float weather = Events.Weather.AccidentFactor;
            int hour = TimeHelper.CurrentHour;
            _carsDue = StreetRules.Accrue(_carsDue, frequency, weather, hour);
            bool people = PeopleCanBeHit();
            if (people)
            {
                _peopleDue = StreetRules.Accrue(_peopleDue, frequency, weather, hour, StreetRules.PeopleShare);
            }
            bool night = Crime.IsNight;
            if (Random.value < StreetRules.RecklessChance(frequency, weather, hour, night))
            {
                StreetRules.Kind kind = StreetRules.Pick(Random.Range(0, 100), night);
                bool blind = people && _peopleDue >= 1f;
                if (blind)
                {
                    kind = StreetRules.PickForPeople(kind, Random.Range(0, 100));
                }
                Begin(null, kind, _carsDue >= 1f, blind, false, false, now);
                return;
            }
            if (people && Random.value < StreetRules.JaywalkChance(frequency, weather, hour))
            {
                StepOut(_peopleDue >= 1f, 0, now, out _);
            }
        }

        /// <summary>True if somebody on foot is owed and nothing else has the road: Traffic/Solid asks before a car that cannot stop reaches them.</summary>
        public bool PersonOwed => _working && Settings.NaturalAccidents && Settings.TrafficAccidents && _peopleDue >= 1f
                                  && PeopleCanBeHit() && Free() == TestRefusal.None;

        private static bool PeopleCanBeHit()
        {
            return Settings.PedestrianIncidents && Settings.Ragdolls && RagdollSystem.Instance != null && RagdollSystem.Instance.Working
                   && PedestrianIncidents.Instance != null && CarStrikes.Instance != null && CarStrikes.Instance.enabled;
        }

        // Why nothing may start now, or None: the old roll's conditions, then whatever else has the road.
        private static TestRefusal Free()
        {
            if (!ModState.Loaded || PlayerHelper.PlayerController == null)
            {
                return TestRefusal.NotLoaded;
            }
            if (!TrafficTools.Ready || !LifeEvents.IsSafeForEvent())
            {
                return TestRefusal.TrafficOff;
            }
            if (Wiring.Scenes.Near(PlayerHelper.GetPosition(), Wiring.Scenes.Beside))
            {
                return TestRefusal.SceneActive;   // an accident, a crime, a fire or street event, a rally, a building site beside it
            }
            if ((PolicePursuit.Instance != null && PolicePursuit.Instance.Busy) || PoliceAttention.IsBusy || Law.Custody.Active)
            {
                return TestRefusal.PoliceBusy;
            }
            if (LicenceDirector.IsActive || JobDirector.OnShift || TransitDirector.Riding || TaxiDirector.Out)
            {
                return TestRefusal.OnJob;         // a driving test, a shift, a bus ride, a taxi called: the road is theirs
            }
            return TestRefusal.None;
        }

        private void Tick(float now)
        {
            if (!TrafficTools.Ready)
            {
                // Indoors, a menu, fast travel or sleep: the game has wiped the traffic and taken its pedestrians back.
                EndNow("traffic stopped (indoors, menu or fast-forward)");
                return;
            }
            if (_jay.Active)
            {
                _jay.Tick(now);
            }
            if (_crowdOut > 0)
            {
                int still = 0;
                for (int i = 0; i < _crowd.Length; i++)
                {
                    if (_crowd[i].Active)
                    {
                        _crowd[i].Tick(now);
                        still += _crowd[i].Active ? 1 : 0;
                    }
                }
                if (still == 0)
                {
                    Logger?.Info("Jaywalker: the crowd a test sent across the road is all across or put back.");
                }
                _crowdOut = still;
            }
            if (_phase == Phase.Idle)
            {
                return;
            }
            if (!_car.InUse || _car.IsLost)
            {
                Logger?.Info("Reckless driver: the car was taken back by the traffic system; nothing more comes of it.");
                CallOff();
                _car.Release(resumeTraffic: true, remove: false);
                Idle(now);
                return;
            }

            switch (_phase)
            {
                case Phase.Driving:
                    _car.Tick(now);
                    Around(now);
                    if (Seen(now))
                    {
                        break;
                    }
                    if (now >= _until || !_car.Driver.Active || !Settings.NaturalAccidents || Wiring.Scenes.AnyLargeSceneActive
                        || (PolicePursuit.Instance != null && PolicePursuit.Instance.Busy))
                    {
                        Settle(now, now >= _until ? "its time is up" : !_car.Driver.Active ? "it ran out of road" : "something else has the street now");
                    }
                    break;

                case Phase.Settling:
                    _car.Tick(now);
                    bool unseen = !InSight(_car.Car.transform.position);
                    bool standing = _car.Driver.Holding || !_car.Driver.Active;
                    // At the traffic's pace in its lane, clear road, no light: handed back as it drives (TrafficTools.ReturnToTraffic).
                    bool rolling = !standing && !_easing && now >= _rollFrom && _car.CanRoll;
                    if (unseen || standing || rolling || (_easing && now >= _until))
                    {
                        Finish(now, false, rolling ? $"handed back on the move at {_car.Driver.Speed * 3.6f:F0} km/h" : unseen ? "out of view"
                            : standing ? "standing: at a light, pulled up, or at the end of its road" : "it would not stop", rolling || (unseen && _car.CanRoll));
                    }
                    else if (!_easing && now >= _until)
                    {
                        // Never with clear road ahead: it pulls up first, as a car handed back behind another must start from rest.
                        _easing = true;
                        _car.Driver.Brake();
                        _until = now + EaseMost;
                    }
                    break;

                case Phase.Stopping:
                    if (!_car.Driver.Active || now >= _waitUntil)
                    {
                        _car.Unit.Park();   // held still from here: not left standing on its springs (TrafficWatch)
                        _phase = Phase.Waiting;
                        _waitUntil = now + WaitForScene;
                    }
                    break;

                case Phase.Waiting:
                    if (now >= _waitUntil)
                    {
                        // Nobody came for it (the casualty got up, or no scene could start): the driver goes on their way.
                        Finish(now, false, "the driver who stopped drives on: no scene was started round them");
                    }
                    break;

                case Phase.Fleeing:
                    _car.Tick(now);
                    Around(now);
                    if (_chaseWanted && (_car.Car.transform.position - _scene).sqrMagnitude > StreetRules.ChaseAfter * StreetRules.ChaseAfter)
                    {
                        _chaseWanted = false;
                        if (Patrol(now, true))
                        {
                            break;
                        }
                    }
                    if (now >= _until || !_car.Driver.Active)
                    {
                        TrafficAccidents.Notice(NotificationType.Info, "reallife_streets_got_away", 8f);
                        Settle(now, "the driver who ran got away");
                    }
                    break;

                case Phase.Chased:
                    _car.Tick(now);
                    Around(now);
                    _stop.Tick(now);
                    if (_complies && _chase.Now == RoadChase.Step.Fleeing && _patrol.InUse && !_patrol.IsLost
                        && (_patrol.Car.transform.position - _car.Car.transform.position).sqrMagnitude < PullUpWithin * PullUpWithin)
                    {
                        _chase.EndRun(now);   // the patrol car is on its tail: the driver gives up and brakes
                    }
                    if (_chase.Tick(now, out bool caught))
                    {
                        EndChase(caught, now);
                    }
                    break;
            }
        }

        private void Around(float now)
        {
            VehicleComponent car = _car.Car;
            EmergencyDriver driver = _car.Driver;
            float speed = driver.Speed;
            if (_car.LightsRun != _lightsSeen)
            {
                _lightsSeen = _car.LightsRun;
                Logger?.Info($"Reckless driver: ran a red light at {speed * 3.6f:F0} km/h ({_lightsSeen} so far).");
                Horns(car, 2);
                _nextScare = 0f;
            }
            if (speed < StreetRules.NearMissSpeed || _car.Settled)
            {
                return;
            }
            if (now >= _nextScare)
            {
                _nextScare = now + ScareEvery;
                HornReactions reactions = HornReactions.Instance;
                if (reactions != null)
                {
                    reactions.Scare(car.transform.position, car.transform.forward, _car.HalfWidth);
                }
            }
            // Somebody ahead that the driver has seen, close: its own horn.
            if (driver.AheadIsPerson && now >= _nextHorn && StreetRules.NearMiss(driver.Room, 0f, _car.HalfWidth, speed))
            {
                _nextHorn = now + HornEvery;
                var horn = car.GetComponentInChildren<AiCarHorn>();
                if (horn != null)
                {
                    TrafficTools.Hoot(horn);
                }
                Logger?.Info($"Reckless driver: somebody {driver.Room:F0} m ahead of it at {speed * 3.6f:F0} km/h: a near miss, if it stops.");
            }
        }

        private void Horns(VehicleComponent car, int most)
        {
            Vector3 at = car.transform.position;
            for (int i = 0; i < _car.NearCount && most > 0; i++)
            {
                VehicleComponent other = _car.Near(i);
                if (other == null || !other.gameObject.activeInHierarchy || (other.transform.position - at).sqrMagnitude > HornWithin * HornWithin)
                {
                    continue;
                }
                var horn = other.GetComponentInChildren<AiCarHorn>();
                if (horn != null)
                {
                    TrafficTools.Hoot(horn);
                    most--;
                }
            }
        }

        private static bool InSight(Vector3 place)
        {
            if (StreetSpots.IsInView(place))
            {
                return true;
            }
            Camera camera = GameManager.GetMainCamera();
            return camera != null && (camera.transform.position - place).sqrMagnitude < NearCamera * NearCamera;
        }

        private void Settle(float now, string why)
        {
            _car.Settle();
            _phase = Phase.Settling;
            _easing = false;
            _until = now + SettleMost;
            _rollFrom = now + RollAfter;
            _patrolAt = 0f;
            Logger?.Info("Reckless driver: settles down (" + why + "); it is an ordinary car again as soon as that will not show.");
        }

        private void Finish(float now, bool remove, string how, bool rolling = false)
        {
            _car.Release(resumeTraffic: true, remove: remove, rolling: rolling && !remove);
            Idle(now);
            Logger?.Info("Reckless driver: over (" + how + ").");
        }

        private const float RollAfter = 2.5f;        // seconds a settled car drives like anybody before it may be handed back on the move
        private float _rollFrom;

        private void Idle(float now)
        {
            _phase = Phase.Idle;
            _nextCheck = now + StreetRules.CheckSeconds;
            _patrolAt = 0f;
            _hurtAt = 0f;
            if (_marked)
            {
                _marked = false;
                _mark.Hide();
            }
        }

        /// <summary>Everything of this feature off the street at once and as the game had it. Safe at any time.</summary>
        public void EndNow(string why)
        {
            try
            {
                bool anything = _phase != Phase.Idle || _jay.Active || _crowdOut > 0 || (_patrol != null && _patrol.InUse);
                _stop.Clear();
                _chase.Reset();
                _jay.End(why);
                for (int i = 0; i < _crowd.Length; i++)
                {
                    if (_crowd[i] != null)
                    {
                        _crowd[i].End(why);
                    }
                }
                _crowdOut = 0;
                _striker = null;
                bool traffic = TrafficTools.Ready;
                if (_patrol != null && _patrol.InUse)
                {
                    _patrol.Release(traffic, remove: true, quiet: true);
                }
                if (_car != null)
                {
                    _car.Release(traffic, remove: false);
                }
                Idle(Time.time);
                if (anything)
                {
                    Logger?.Info("Reckless drivers: everything given back at once (" + why + ").");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _phase = Phase.Idle;
            }
        }
    }
}
