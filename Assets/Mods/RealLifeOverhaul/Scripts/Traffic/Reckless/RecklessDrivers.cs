using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Accidents that happen for a reason, where the player can see them. Now and then one ordinary
    /// traffic car near the player is driven recklessly for a while (RecklessCar: a speeder, a driver who
    /// runs a red light, one who brakes late, a drunk at night), and now and then somebody steps into the
    /// road in front of a car (Jaywalker). What comes of it is whatever happens on the road: mostly
    /// nothing, and the car is an ordinary car again after 20 to 60 seconds; a near miss, with horns and
    /// people jumping clear (HornReactions.Scare); a collision, which is handed to TrafficAccidents.BeginAt
    /// as it stands; somebody run down, which is PedestrianIncidents' and ends with the driver staying or
    /// running; or a patrol car that saw it and pulls the driver over, or chases them (RoadChase).
    ///
    /// HOW OFTEN. The option "traffic accident frequency" keeps its meaning. The old roll was a chance of
    /// frequency x 0.02 x the weather at each 20 second look; here the same quantity falls due at each
    /// look (StreetRules.Due), so the same number of accidents is owed per hour. A reckless driver is
    /// about three times as likely as that at a look, and only one who appears while an accident is owed
    /// drives with no margin (and uses the debt up if a collision comes of it). So most of them get away
    /// with it, and accidents cannot come faster than they used to.
    ///
    /// EVERY CAR THAT HITS SOMEBODY ENDS HERE. CarStrikes tells this director whenever a traffic car it watches
    /// has thrown somebody down (Struck): the reckless car, the car somebody stepped out in front of, or any
    /// ordinary car that could not stop (Traffic/Solid). One path from there: the debt is paid, and the
    /// driver stops and waits for the scene or runs, with a patrol car after them if there are police. An
    /// ordinary car is taken over at that moment to do it, exactly as the reckless one was taken over
    /// earlier; if a reckless car is already out, the ordinary one is only remembered, and is given to the
    /// scene if its driver has pulled up beside the person (GiveUpStriker).
    ///
    /// ONE AT A TIME, AND NOT IN THE WAY. One reckless car at most, one person stepping out at most, and
    /// neither while an accident scene or any other large scene is on, the police have business with the
    /// player, or one of the mod's own uses of the road is under way (a driving test, a shift, a bus
    /// ride, a taxi called). The car is never put on the road or changed in view: it is a car that was
    /// driving there already, whose driver changes.
    ///
    /// GIVING EVERYTHING BACK. The car: handed back to traffic as an ordinary car once it has settled down
    /// (standing at a light, or out of view, or pulled up after a few seconds at the traffic's own pace), passed to
    /// the accident scene on a collision, or taken away after an arrest. The patrol car: handed back to
    /// traffic when its stop is done. The person in the road: Jaywalker. All of it at once, in the state
    /// every unit is given back in (EndNow), when traffic stops (the player goes indoors, a menu, fast
    /// travel, sleep), the option is switched off through the options page's cleanup, a fault, or the
    /// city is unloaded with the mod's root object (OnDestroy). Saving changes nothing: traffic is not
    /// saved, and after a load there is no car of ours.
    ///
    /// COST. Nothing going on: three flag tests and one time comparison a frame, and every 20 seconds a
    /// roll. Picking a car or a person to step out: one pass over the traffic list (and the pedestrians),
    /// on that look only. While a car is out: RecklessCar's own (four looks a second, a box test every
    /// other physics step), one pass over the pedestrians a second for the people in its way, and a
    /// handful of comparisons a tick here. No physics query of its own and nothing allocated while it runs.
    /// </summary>
    public class RecklessDrivers : MonoBehaviour
    {
        private const float TickInterval = 0.25f;
        private const float SettleMost = 14f;        // seconds a settled car is given to reach a moment at which handing it back will not show
        private const float EaseMost = 4f;           // ...and then to pull up, if none came
        private const float WaitForScene = 14f;      // seconds a driver who stopped for somebody they hit waits to be taken into the scene
        private const float FleeMost = 40f;          // seconds a driver who ran keeps running if no patrol car comes
        private const float ScareEvery = 1f;         // seconds between looks for people in its way
        private const float HornEvery = 3f;
        private const float HornWithin = 16f;        // metres: traffic this near sounds its horn at what it has just done
        private const float PatrolSees = 0.12f;      // of the speeders and light runners: a passing patrol car happens to see it
        private const float PresenceSees = 60f;      // metres from the parked patrol car inside which its officers see one go by
        private const float ChaseGap = 9f;           // metres the patrol car keeps behind it (the accident scene's own figure)
        private const float PullUpWithin = 22f;      // metres behind a driver who is stopping at which the patrol car has closed up
        private const float PullOverMost = 40f;      // seconds the patrol car is given to close up
        private const float SceneClear = 30f;        // metres a patrol car's way must keep from where somebody lies in the road
        private const float NearCamera = 45f;        // nothing is put on the road this near the camera, whichever way it looks
        private const float TestWithin = 200f;       // metres a test button looks for a car
        private const float MarkFor = 15f;           // seconds a test's car is marked
        private const float BlamelessFor = 5f;       // seconds after it hit the player's car in which that crash is still its fault
        private const float HurtScene = 20f;         // seconds after it ran somebody down in which the scene round them has not begun or has only just
        private const int CrowdMost = 10;            // people a test sends across the road at once
        private const float StrikerFor = 20f;        // seconds an ordinary car that hit somebody is remembered for the scene
        private const float StrikerSlow = 6f;        // km/h: slower than this its driver has pulled up

        private enum Phase { Idle, Driving, Settling, Stopping, Waiting, Fleeing, Chased }

        public static IModLogger Logger;
        public static RecklessDrivers Instance { get; private set; }

        /// <summary>
        /// True while the road is where accidents come from: both options are on ("Traffic accidents" and this one)
        /// and this has not switched itself off. TrafficAccidents and PedestrianIncidents make their own rolls only
        /// while it is false, so with this option off, or after a fault here, everything is as it was before.
        /// </summary>
        public static bool IsSource => Instance != null && Instance._working && Settings.NaturalAccidents && Settings.TrafficAccidents;

        private readonly RoadsideStop _stop = new RoadsideStop();
        private readonly RoadChase _chase = new RoadChase();
        private readonly Jaywalker _jay = new Jaywalker();
        private readonly Jaywalker[] _crowd = new Jaywalker[CrowdMost];   // a test's ten people crossing at once
        private int _crowdOut;
        private readonly GameUi.GuideMark _mark = new GameUi.GuideMark();
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

        private void Update()
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

        // Everything is given back at once, so a fault cannot repeat; after three the road is no longer the source of
        // accidents this session, and the roll TrafficAccidents has always made takes over (IsSource).
        private void Fault(Exception e)
        {
            Logger?.Error(e);
            EndNow("an error");
            if (++_faults >= 3 && _working)
            {
                _working = false;
                Logger?.Warn("Reckless drivers: switched off for this session after repeated errors (see above); accidents are staged out of view as before.");
            }
        }

        // ---------------------------------------------------------------- the look every 20 seconds

        private void Check(float now)
        {
            if (!_working || !Settings.NaturalAccidents || !Settings.TrafficAccidents || Free() != TestRefusal.None)
            {
                return;
            }
            // What the old roll's chance was at this look falls due instead (same conditions, same quantity).
            int frequency = Settings.AccidentFrequency;
            float weather = Events.Weather.AccidentFactor;
            _carsDue = StreetRules.Accrue(_carsDue, frequency, weather);
            bool people = PeopleCanBeHit();
            if (people)
            {
                _peopleDue = StreetRules.Accrue(_peopleDue, frequency, weather, StreetRules.PeopleShare);
            }
            bool night = Crime.IsNight;
            if (Random.value < StreetRules.RecklessChance(frequency, weather, night))
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
            if (people && Random.value < StreetRules.JaywalkChance(frequency, weather))
            {
                StepOut(_peopleDue >= 1f, 0, now, out _);
            }
        }

        /// <summary>
        /// True if somebody on foot is owed to the street: a whole one is in the bank, people can be hit at all,
        /// and nothing else has the road. Traffic/Solid asks (only when a car it looks at can no longer stop for
        /// somebody) before it lets that car reach them; with this false every ordinary car stops in time.
        /// </summary>
        public bool PersonOwed => _working && Settings.NaturalAccidents && Settings.TrafficAccidents && _peopleDue >= 1f
                                  && PeopleCanBeHit() && Free() == TestRefusal.None;

        /// <summary>People owing, for the log.</summary>
        public float PeopleDue => _peopleDue;

        private static bool PeopleCanBeHit()
        {
            return Settings.PedestrianIncidents && Settings.Ragdolls && RagdollSystem.Instance != null && RagdollSystem.Instance.Working
                   && PedestrianIncidents.Instance != null && CarStrikes.Instance != null && CarStrikes.Instance.enabled;
        }

        // Why nothing may start now, or None. The first three lines are what the old roll asked; the rest keep this
        // out of the way of whatever else has the road.
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

        // ---------------------------------------------------------------- picking the car

        // Makes one traffic car the reckless one. car: that one, or null to pick. False if none could be had.
        // struck: it is an ordinary car that has this moment hit somebody, taken over so that its driver can stop or run.
        private bool Begin(VehicleComponent car, StreetRules.Kind kind, bool accidentDue, bool blind, bool never, bool test, float now, bool struck = false)
        {
            if (_phase != Phase.Idle)
            {
                return false;
            }
            Vector3 player = PlayerHelper.GetPosition();
            SessionWatch.Mark("streets: a reckless driver being picked");
            // (A car whose road ahead is too short is passed over: the next one is tried, a few times.)
            for (int attempt = 0; attempt < 4; attempt++)
            {
                VehicleComponent picked = car != null ? car : Pick(player, kind, test);
                if (picked == null)
                {
                    return false;
                }
                if (_car.Take(picked, kind, accidentDue, blind, never))
                {
                    break;
                }
                if (car != null || attempt == 3)
                {
                    return false;
                }
            }
            VehicleComponent taken = _car.Car;
            PedestrianIncidents incidents = PedestrianIncidents.Instance;
            _phase = Phase.Driving;
            _test = test;
            _until = now + (never ? 90f : StreetRules.Lasts(Random.value));
            _nextTick = now;
            _nextScare = now + ScareEvery;
            _nextHorn = now;
            _hurtSeen = incidents != null ? incidents.TrafficHurt : 0;
            _lightsSeen = 0;
            _hurtAt = 0f;
            _presenceLooked = false;
            _chaseWanted = false;
            _complies = false;
            _patrolAt = !test && !struck && Settings.Police && StreetRules.PatrolNotices(kind) && Random.value < PatrolSees ? now + Random.Range(4f, 10f) : 0f;
            if (struck)
            {
                Logger?.Info($"Reckless driver: '{TrafficTools.Name(taken)}', an ordinary car that has just hit somebody {Vector3.Distance(player, taken.transform.position):F0} m from the player, "
                             + "is taken over where it is: its driver stops for them or runs.");
                return true;
            }
            Logger?.Info($"Reckless driver: '{TrafficTools.Name(taken)}' {Vector3.Distance(player, taken.transform.position):F0} m from the player is now a {kind}"
                         + $" ({(never ? "test: brakes for no vehicle" : accidentDue ? "an accident is due: no margin" : "with a margin: gets away with it unless somebody else errs")}"
                         + $"{(blind ? ", does not see people on foot" : string.Empty)}), cruising at {_car.Cruise * 3.6f:F0} km/h for up to {_until - now:F0} s."
                         + $" Owing: {_carsDue:F2} car accident(s), {_peopleDue:F2} pedestrian(s).");
            return true;
        }

        // An ordinary private car on the move, far enough for the change not to show and near enough to be seen: in view
        // now, or coming the player's way. A test looks wider and takes any that is moving. Starts at a random car.
        private static VehicleComponent Pick(Vector3 player, StreetRules.Kind kind, bool test)
        {
            List<VehicleComponent> pool = Manager.GetVehicleList();
            if (pool.Count == 0)
            {
                return null;
            }
            const float least = 12f;   // a test's own range: the rule's (StreetRules.InPickRange) is for the cars nobody asked for
            const float most = TestWithin;
            float speed = test ? 6f : StreetRules.PickSpeedKmh;
            int start = Random.Range(0, pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[(start + i) % pool.Count];
                if (!CrimeCars.IsOrdinary(car) || car.GetCurrentSpeed() < speed)
                {
                    continue;
                }
                Vector3 at = car.transform.position;
                Vector3 toPlayer = player - at;
                float sqr = toPlayer.sqrMagnitude;
                if (test ? sqr < least * least || sqr > most * most : !StreetRules.InPickRange(Mathf.Sqrt(sqr)))
                {
                    continue;
                }
                if (!test && !StreetSpots.IsInView(at) && Vector3.Dot(car.transform.forward, toPlayer) <= 0f)
                {
                    continue;
                }
                if (kind == StreetRules.Kind.RedLight && !LightOnItsLane(car, test ? 220f : 130f))
                {
                    continue;   // nothing for it to run
                }
                return car;
            }
            return null;
        }

        // True if the lane the car is in comes to a traffic light within so many metres: its own lane is followed
        // (a lane does not fork before its stop line), a few dozen points at the most.
        private static bool LightOnItsLane(VehicleComponent car, float within)
        {
            TrafficManager traffic = TrafficManager.Instance;
            Waypoint point = traffic.GetTargetWaypoint(car.GetIndex());
            float walked = 0f;
            for (int step = 0; step < 60 && point != null && walked < within; step++)
            {
                if (point.enter)
                {
                    return !point.giveWay;
                }
                if (point.neighbors == null || point.neighbors.Count == 0)
                {
                    return false;
                }
                Waypoint next = traffic.GetWaypoint(point.neighbors[0]);
                if (next == null)
                {
                    return false;
                }
                walked += Vector3.Distance(point.position, next.position);
                point = next;
            }
            return false;
        }

        // ---------------------------------------------------------------- four times a second

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
                    // On the move at the traffic's pace, in its lane, with clear road and no light holding it: it is handed
                    // back as it drives, keeping its speed (TrafficTools.ReturnToTraffic says why the traffic system takes a
                    // moving car), and nothing shows at all.
                    bool rolling = !standing && !_easing && now >= _rollFrom && _car.CanRoll;
                    if (unseen || standing || rolling || (_easing && now >= _until))
                    {
                        Finish(now, false, rolling ? $"handed back on the move at {_car.Driver.Speed * 3.6f:F0} km/h" : unseen ? "out of view"
                            : standing ? "standing: at a light, pulled up, or at the end of its road" : "it would not stop", rolling || (unseen && _car.CanRoll));
                    }
                    else if (!_easing && now >= _until)
                    {
                        // Still in view, still moving, and never once with clear road ahead of it in all that time (traffic in
                        // front, out of its lane): it pulls up first, since a car handed back behind another must start from
                        // rest (the traffic system begins with no knowledge of what is ahead of it).
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
                        Notifications.Show(NotificationType.Info, "reallife_streets_got_away", null, 8f, "reallife_accident");
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

        // What it does to the people and the traffic round it: a red light run, somebody in its way.
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
                    horn.Hail(false);
                }
                Logger?.Info($"Reckless driver: somebody {driver.Room:F0} m ahead of it at {speed * 3.6f:F0} km/h: a near miss, if it stops.");
            }
        }

        // The traffic nearest it sounds its horn (the game's own AiCarHorn).
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
                    horn.Hail(false);
                    most--;
                }
            }
        }

        // ---------------------------------------------------------------- somebody run down

        /// <summary>
        /// Called by CarStrikes (from its physics step) when a traffic car it watches has thrown somebody down: the
        /// reckless car, the car somebody stepped out in front of, or an ordinary car that could not stop. Only
        /// noted here; what comes of it is decided in this director's next frame (OnHurt), on the one path every
        /// such car takes.
        /// </summary>
        public void Struck(VehicleComponent car, float speed)
        {
            _struckBy = car;
            _struckSpeed = speed;
            _struck = true;
        }

        // A traffic car has thrown somebody down. If they are hurt the debt is paid and the driver stops or runs: the
        // reckless car as it is; an ordinary car taken over at this moment to do the same, or (a reckless car being out
        // already, or its road too short to drive on) remembered, so that the scene takes it if it has pulled up.
        private void OnHurt(float now)
        {
            VehicleComponent car = _struckBy;
            float speed = _struckSpeed;
            _struckBy = null;
            PedestrianIncidents incidents = PedestrianIncidents.Instance;
            if (incidents == null || car == null)
            {
                return;
            }
            bool hurt = incidents.TrafficHurt != _hurtSeen;
            _hurtSeen = incidents.TrafficHurt;
            bool ours = _phase != Phase.Idle && _car.InUse && _car.Car == car;
            if (!hurt)
            {
                if (ours && _phase == Phase.Driving)
                {
                    Logger?.Info("Reckless driver: knocked somebody over at walking pace; they are getting up, and the driver has had a fright.");
                    Settle(now, "it knocked somebody over");
                }
                else if (!ours)
                {
                    Logger?.Info($"Traffic check (struck): '{TrafficTools.Name(car)}' knocked somebody over at {speed * 3.6f:F0} km/h; they are getting up, and it drives on.");
                }
                return;
            }
            _peopleDue = Mathf.Max(0f, _peopleDue - 1f);
            bool fatal = incidents.TrafficKilled;
            if (!ours)
            {
                Logger?.Info($"Traffic check (struck): '{TrafficTools.Name(car)}' ran somebody down at {speed * 3.6f:F0} km/h ({(fatal ? "fatal" : "injured")}). Still owing: {_peopleDue:F2} pedestrian(s).");
                _striker = car;
                _jayAt = now;
                if (_phase != Phase.Idle || !_working || !car.gameObject.activeInHierarchy || !CrimeCars.IsOrdinary(car)
                    || !Begin(car, StreetRules.Kind.Tailgater, false, false, false, false, now, struck: true))
                {
                    Logger?.Info("Reckless driver: the car that hit them could not be taken over (" + (_phase != Phase.Idle ? "a reckless car is out already" : "no road ahead of it to drive on, or not an ordinary car")
                                 + "); it is given to the scene if its driver has pulled up beside them.");
                    return;
                }
                _striker = null;
            }
            _hurtAt = now;
            if (_phase != Phase.Driving)
            {
                Logger?.Info("Reckless driver: somebody else was run down as it ran.");
                return;
            }
            _scene = _car.Car.transform.position;
            if (StreetRules.Flees(_car.Kind, fatal, Random.Range(0, 100)))
            {
                _phase = Phase.Fleeing;
                _car.Flee();
                _until = now + FleeMost;
                _chaseWanted = Settings.Police;
                _patrolAt = 0f;
                News.Report(NewsKind.HitAndRun, _scene);
                Notifications.Show(NotificationType.Warning, "reallife_streets_hit_run", null, 8f, "reallife_accident");
                Logger?.Info($"Reckless driver: ran somebody down ({(fatal ? "fatal" : "injured")}) and is not stopping"
                             + (_chaseWanted ? $"; a patrol car goes after it once it is {StreetRules.ChaseAfter:F0} m clear of the scene." : "; no police to go after it."));
            }
            else
            {
                _phase = Phase.Stopping;
                _car.Driver.Brake();
                _waitUntil = now + 8f;
                _patrolAt = 0f;
                Logger?.Info($"Reckless driver: ran somebody down ({(fatal ? "fatal" : "injured")}) and is stopping; the car stays for the scene.");
            }
        }

        /// <summary>
        /// Called by PedestrianIncidents when somebody a traffic car hit has come to rest and a scene is about to
        /// start round them: the car that did it, if its driver stopped (it is the scene's from here: a car that
        /// has been ours is passed on as it stands), or null if they drove off or there is no such car.
        /// </summary>
        public VehicleComponent GiveUpStriker()
        {
            try
            {
                float now = Time.time;
                if (_phase == Phase.Stopping || _phase == Phase.Waiting)
                {
                    CallOff();
                    VehicleComponent car = _car.HandOver();
                    Idle(now);
                    Logger?.Info("Reckless driver: the driver who stopped is part of the scene now" + (car == null ? " (the car was no longer ours to give)." : "."));
                    return car;
                }
                // An ordinary car that hit them and was not taken over, if its driver has pulled up beside them.
                VehicleComponent last = _striker;
                if (last != null && (_phase == Phase.Idle || _car.Car != last) && now - _jayAt < StrikerFor && last.gameObject.activeInHierarchy
                    && CrimeCars.IsOrdinary(last) && last.GetCurrentSpeed() < StrikerSlow)
                {
                    _striker = null;
                    return last;
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
            return null;
        }

        // ---------------------------------------------------------------- a patrol car that saw it

        // True if a patrol car has just gone after it (the phase has changed).
        private bool Seen(float now)
        {
            if (!Settings.Police || !StreetRules.PatrolNotices(_car.Kind))
            {
                return false;
            }
            if (_patrolAt > 0f && now >= _patrolAt)
            {
                _patrolAt = 0f;
                return Patrol(now, false);
            }
            // The officers at a parked patrol car see one go by (asked once for each car, as it comes near).
            PolicePresence presence = PolicePresence.Instance;
            if (!_presenceLooked && presence != null && presence.CarWithin(_car.Car.transform.position, PresenceSees))
            {
                _presenceLooked = true;
                Logger?.Info("Reckless driver: it passed the parked patrol car; a patrol car is sent after it.");
                return Patrol(now, false);
            }
            return false;
        }

        // Puts a patrol car on the road well behind it (as an accident scene's is: 140 to 200 m back along the road that
        // leads to it, lights and siren on) and sends it after the car. Never where it can be seen appearing, and never
        // by a way that runs through where somebody lies. True if the chase is on.
        private bool Patrol(float now, bool hitAndRun)
        {
            if (!Settings.Police || _patrol.InUse || !_car.InUse)
            {
                return false;
            }
            Waypoint anchor = _car.Behind();
            if (anchor == null || !_patrol.Spawn(UnitKind.Police, anchor, Random.Range(140f, 200f), 60f))
            {
                Logger?.Info("Reckless driver: no patrol car could be put on the road behind it (" + (anchor == null ? "no road point" : _patrol.Refusal) + "); it is not chased.");
                NoPatrol();
                return false;
            }
            VehicleComponent unit = _patrol.Car;
            bool inSight = InSight(unit.transform.position);
            bool through = hitAndRun && PassesNear(_patrol.Path, _scene, SceneClear);
            if (inSight || through)
            {
                _patrol.Release(resumeTraffic: true, remove: true);
                Logger?.Info("Reckless driver: the patrol car would have " + (inSight ? "appeared in view" : "had to drive through where somebody lies") + "; none is sent, and it is not chased.");
                NoPatrol();
                return false;
            }
            // Its road: up to the point behind the car, then the car's own road from there on (the list is read live).
            List<Vector3> road = _car.Unit.Path;
            for (int i = _car.BehindIndex + 1; i < road.Count; i++)
            {
                _patrol.Path.Add(road[i]);
            }
            EmergencyDriver driver = _patrol.Driver;
            driver.Begin(unit, _patrol.Path, _patrol.Path[_patrol.Path.Count - 1], 6f);
            driver.Follow(_car.Car.rb, ChaseGap);

            _complies = !hitAndRun && StreetRules.PullsOver(Random.Range(0, 100));
            if (_complies)
            {
                // The driver sees the lights and eases off; the patrol car closes up and they stop.
                _car.Settle();
                driver.CruiseSpeed = 21f;
                _chase.Begin(_car.Unit, _patrol, _stop, true, now + PullOverMost, arrest: false, "reallife_streets_pulled_over");
                Notifications.Show(NotificationType.Info, "reallife_streets_patrol_stop", null, 8f, "reallife_accident");
            }
            else
            {
                _car.Flee();
                driver.CruiseSpeed = _car.Cruise + 3.5f;
                bool caught = Random.value < 0.55f;
                _chase.Begin(_car.Unit, _patrol, _stop, caught, now + Random.Range(20f, 45f), arrest: true, hitAndRun ? "reallife_chase_caught" : "reallife_streets_chase_caught");
                Notifications.Show(NotificationType.Warning, hitAndRun ? "reallife_streets_chase_hit_run" : "reallife_streets_patrol_chase", null, 8f, "reallife_accident");
            }
            _fledFromPeople = hitAndRun;
            _phase = Phase.Chased;
            Logger?.Info($"Reckless driver: a patrol car is {Vector3.Distance(unit.transform.position, _car.Car.transform.position):F0} m behind it, lights on; "
                         + (_complies ? "the driver will stop for it." : $"the driver runs and will be {(_chase.WillBeCaught ? "caught" : "lost")}."));
            return true;
        }

        private bool _fledFromPeople;

        // A test that asked for a patrol car is told when none could be sent (anything else simply goes unchased).
        private void NoPatrol()
        {
            if (_test)
            {
                Notifications.Show(NotificationType.Warning, "reallife_streets_test_no_patrol", null, 8f);
            }
        }

        private void EndChase(bool caught, float now)
        {
            _stop.Clear();
            SendPatrolOff();
            _chase.Reset();
            if (caught && _complies)
            {
                Notifications.Show(NotificationType.Info, "reallife_streets_ticketed", null, 8f, "reallife_accident");
                Finish(now, false, "pulled over, written up and sent on its way");
            }
            else if (caught)
            {
                // (As the accident scene's own chase ends: the car of a driver who was arrested is taken away.)
                Finish(now, true, "stopped, the driver arrested, the car taken away");
            }
            else
            {
                Notifications.Show(NotificationType.Info, _fledFromPeople ? "reallife_chase_escaped" : "reallife_streets_chase_lost", null, 8f, "reallife_accident");
                Settle(now, "it lost the patrol car");
            }
        }

        // The patrol car goes back to traffic as it is (lights on for a few seconds, siren off), and is watched: one
        // that then stands for long is taken off the road by TrafficWatch, as an accident scene's would be.
        private void SendPatrolOff()
        {
            if (!_patrol.InUse)
            {
                return;
            }
            VehicleComponent unit = _patrol.Car;
            if (unit != null && TrafficTools.IsHeld(unit))
            {
                TrafficWatch.Guard(unit, "patrol car after a roadside stop");
            }
            _patrol.Release(resumeTraffic: TrafficTools.Ready, remove: false, quiet: true);
        }

        // Whatever was behind the car is called off: the people at the roadside leave, the patrol car goes its way.
        private void CallOff()
        {
            _stop.Clear();
            _chase.Reset();
            SendPatrolOff();
        }

        private static bool PassesNear(List<Vector3> path, Vector3 place, float within)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 offset = path[i] - place;
                if (offset.x * offset.x + offset.z * offset.z < within * within)
                {
                    return true;
                }
            }
            return false;
        }

        // True if somebody could be looking at a place: in the camera's view, or simply near it.
        private static bool InSight(Vector3 place)
        {
            if (StreetSpots.IsInView(place))
            {
                return true;
            }
            Camera camera = GameManager.GetMainCamera();
            return camera != null && (camera.transform.position - place).sqrMagnitude < NearCamera * NearCamera;
        }

        // ---------------------------------------------------------------- a collision

        // RecklessCar has met something (its box another vehicle's, a bend too fast, something fixed): the car is passed to
        // the accident scene as it stands, with the other car if there is one and the speed they met at.
        private void OnStruck(float now)
        {
            RecklessCar.Hit hit = _car.Struck;
            VehicleComponent other = hit == RecklessCar.Hit.Car ? _car.Other : null;
            float closing = _car.Closing;
            StreetRules.Kind kind = _car.Kind;
            if (_hurtAt > 0f && now - _hurtAt < HurtScene && hit != RecklessCar.Hit.Player)
            {
                // It had just run somebody down, and now it has hit something as well. One scene comes of the two, and it is
                // the one round the person: a crash scene started here would be ended again the moment they come to rest
                // (PedestrianIncidents lets nothing stand in the way of help). So the car stays where it stopped, and is
                // taken into that scene as the car that hit them, whether its driver had meant to stop or not.
                CallOff();
                _car.Stay();
                _phase = Phase.Waiting;
                _waitUntil = now + WaitForScene;
                Logger?.Info($"Reckless driver: having just run somebody down it hit something at {closing * 3.6f:F0} km/h and goes no further; it stays for the scene round them.");
                return;
            }
            CallOff();
            // What the cars keep for a moment after they met: what each had, or, for one that met something that
            // does not give, a little of it back the way it came. Read before the car is handed over.
            Vector3 own = hit == RecklessCar.Hit.Solid || hit == RecklessCar.Hit.Player ? _car.Velocity * -0.15f : _car.Velocity;
            Vector3 theirs = _car.OtherVelocity;
            VehicleComponent mine = _car.HandOver();
            Idle(now);
            if (mine == null)
            {
                Logger?.Info("Reckless driver: it hit something, but the car was no longer ours to hand to a scene.");
                return;
            }
            if (hit == RecklessCar.Hit.Player)
            {
                _blameless = mine;
                _blamelessUntil = now + BlamelessFor;
            }
            string notice = hit == RecklessCar.Hit.Player ? "reallife_streets_crash_player"
                : hit == RecklessCar.Hit.Bend ? "reallife_streets_crash_bend"
                : kind == StreetRules.Kind.Speeder ? "reallife_streets_crash_speeder"
                : kind == StreetRules.Kind.RedLight ? "reallife_streets_crash_redlight"
                : kind == StreetRules.Kind.Drunk ? "reallife_streets_crash_drunk"
                : "reallife_streets_crash_tailgater";
            TrafficAccidents accidents = TrafficAccidents.Instance;
            string what = hit == RecklessCar.Hit.Car ? "met '" + TrafficTools.Name(other) + "'"
                : hit == RecklessCar.Hit.Player ? "ran into the player's car"
                : hit == RecklessCar.Hit.Bend ? "took a bend faster than the road holds"
                : "ran into something fixed";
            if (accidents != null && accidents.BeginAt(mine, other, closing, notice, own, theirs))
            {
                _carsDue = Mathf.Max(0f, _carsDue - 1f);
                Logger?.Info($"Reckless driver: the {kind} {what} at {closing * 3.6f:F0} km/h; the accident scene has both from here. Still owing: {_carsDue:F2} car accident(s).");
            }
            else
            {
                TrafficTools.ReturnToTraffic(mine, "a reckless car that hit something while no scene could start");
                Logger?.Info($"Reckless driver: the {kind} {what} at {closing * 3.6f:F0} km/h, but no accident scene could start (another scene is on); it drives on.");
            }
        }

        /// <summary>
        /// True if that traffic car is the reckless one, or was until it ran into the player a moment ago: a
        /// collision with it is its driver's fault, and the player owes nothing for it (CrashConsequences asks).
        /// </summary>
        public static bool AtFault(VehicleComponent car)
        {
            RecklessDrivers drivers = Instance;
            if (drivers == null || car == null)
            {
                return false;
            }
            return (drivers._car != null && drivers._car.InUse && drivers._car.Car == car)
                   || (drivers._blameless == car && Time.time < drivers._blamelessUntil);
        }

        /// <summary>
        /// The game's own collision between the player's car and the reckless car arrived before this had seen the two
        /// boxes meet: it is the same crash, taken from here as it would have been a step later.
        /// </summary>
        public void HitPlayer(VehicleComponent car, float closing)
        {
            if (_phase != Phase.Idle && _car != null && _car.InUse && _car.Car == car)
            {
                _car.Report(RecklessCar.Hit.Player, null, closing);
            }
        }

        // ---------------------------------------------------------------- settling down and letting go

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

        /// <summary>
        /// Everything of this feature off the street at once and as the game had it: the car back to traffic, the
        /// patrol car gone, the people at the roadside and whoever was crossing the road back where they belong.
        /// Safe at any time, and when nothing is going on.
        /// </summary>
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
                _phase = Phase.Idle;
                _nextCheck = Time.time + StreetRules.CheckSeconds;
                _patrolAt = 0f;
                _hurtAt = 0f;
                if (_marked)
                {
                    _marked = false;
                    _mark.Hide();
                }
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

        // ---------------------------------------------------------------- stepping out

        // Sends somebody across the road in front of a car. mode 0: as it comes (a near miss, unless one is due);
        // 1: a test, any car that is coming; 2: a test that ends with them hit: they stop in the lane and the car is
        // made the reckless one, blind to them; 3: a test, they wait at the kerb and step out close (a hard brake and
        // the horn); 4: a test, they stand in the lane until the car behind them sounds its horn; 5: a test, they wait
        // at the kerb and step out too late for the driver (an ordinary car: the hit is its own).
        // False if no pedestrian and car stand right for it.
        private bool StepOut(bool late, int mode, float now, out float metres)
        {
            metres = 0f;
            if (_jay.Active || _phase != Phase.Idle)
            {
                return false;
            }
            Vector3 player = PlayerHelper.GetPosition();
            SessionWatch.Mark("streets: somebody to step into the road being picked");
            if (!Jaywalker.Find(player, mode == 0 ? 10f : 6f, mode == 0 ? 60f : 90f, late, mode, out Pedestrian walker, out VehicleComponent car, out Vector3 across, out Vector3 lane))
            {
                return false;
            }
            Jaywalker.Step step = mode == 2 ? Jaywalker.Step.Stay : mode == 3 ? Jaywalker.Step.Close : mode == 4 ? Jaywalker.Step.Dawdle
                : mode == 5 ? Jaywalker.Step.Late : Jaywalker.Step.Across;
            if (!_jay.Begin(walker, car, across, lane, step, now))
            {
                return false;
            }
            metres = Vector3.Distance(player, walker.transform.position);
            _jayAt = now;
            _nextTick = now;
            PedestrianIncidents incidents = PedestrianIncidents.Instance;
            _hurtSeen = incidents != null ? incidents.TrafficHurt : 0;
            CarStrikes strikes = CarStrikes.Instance;
            if (strikes != null && Settings.Ragdolls)
            {
                strikes.Watch(car);
            }
            if ((late && mode == 0) || mode == 5)
            {
                // Somebody is to be hit by an ordinary car: Traffic/Solid lets this one car reach this one person
                // (every other car still stops for them). Without that feature the car is not held back by anything.
                SolidStreets.Owe(car, walker);
            }
            if (mode == 2 && !Begin(car, StreetRules.Kind.Tailgater, true, true, false, true, now))
            {
                _jay.End("the car could not be made the reckless one");
                return false;
            }
            return true;
        }

        // Sends up to ten of the people walking beside the road here straight across it, all at once. How many went.
        private int Crowd(float now, out float metres)
        {
            metres = 0f;
            Vector3 player = PlayerHelper.GetPosition();
            SessionWatch.Mark("streets: a crowd to cross the road being picked");
            int sent = 0;
            for (int i = 0; i < _crowd.Length; i++)
            {
                if (_crowd[i].Active)
                {
                    continue;
                }
                // (Whoever is in the road for us already is passed over by the search itself: their own script is off.)
                if (!Jaywalker.FindCrosser(player, 5f, 70f, out Pedestrian walker, out Vector3 across, out Vector3 lane))
                {
                    break;
                }
                if (_crowd[i].Begin(walker, null, across, lane, Jaywalker.Step.Across, now))
                {
                    metres = sent == 0 ? Vector3.Distance(player, walker.transform.position) : metres;
                    sent++;
                }
            }
            _crowdOut = 0;
            for (int i = 0; i < _crowd.Length; i++)
            {
                _crowdOut += _crowd[i].Active ? 1 : 0;
            }
            _nextTick = now;
            return sent;
        }

        // ---------------------------------------------------------------- the test buttons

        public enum TestKind { Speeder, RedLight, Drunk, Jaywalker, HitSomeone, Crash, Patrol, StepRoom, StepClose, Dawdle, StepLate, Crowd }

        /// <summary>Options-menu test buttons: one of the things this feature does, started here and now, or a notice saying why not.</summary>
        public void Test(TestKind kind)
        {
            try
            {
                bool solid = kind == TestKind.StepRoom || kind == TestKind.StepClose || kind == TestKind.Dawdle || kind == TestKind.StepLate || kind == TestKind.Crowd;
                bool people = kind == TestKind.Jaywalker || kind == TestKind.HitSomeone || kind == TestKind.StepLate;
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && solid && !SolidStreets.Working)
                {
                    why = TestRefusal.FeatureOff;   // these four show what "Traffic sees people in the road" does
                }
                if (why == TestRefusal.None && (!_working || (!solid && !Settings.NaturalAccidents) || (people && !PeopleCanBeHit()) || (kind == TestKind.Patrol && !Settings.Police)))
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why == TestRefusal.None && ((PolicePursuit.Instance != null && PolicePursuit.Instance.Busy) || PoliceAttention.IsBusy))
                {
                    why = TestRefusal.PoliceBusy;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Reckless driver", why);
                    return;
                }
                StreetCrime.EndActive();    // one incident at a time, and the tester asked for this one
                TrafficAccidents.EndActive();
                EndNow("a test button");

                float now = Time.time;
                float metres = 0f;
                bool started;
                string none = null;
                switch (kind)
                {
                    case TestKind.RedLight:
                        started = Begin(null, StreetRules.Kind.RedLight, false, false, false, true, now);
                        none = "reallife_streets_test_no_light";
                        break;
                    case TestKind.Drunk:
                        started = Begin(null, StreetRules.Kind.Drunk, false, false, false, true, now);
                        break;
                    case TestKind.Jaywalker:
                    case TestKind.StepRoom:
                        started = StepOut(false, 1, now, out metres);
                        none = "reallife_streets_test_no_pair";
                        break;
                    case TestKind.HitSomeone:
                        started = StepOut(true, 2, now, out metres);
                        none = "reallife_streets_test_no_pair";
                        break;
                    case TestKind.Crash:
                        started = Begin(null, StreetRules.Kind.Tailgater, true, false, true, true, now);
                        break;
                    case TestKind.StepClose:
                        started = StepOut(false, 3, now, out metres);
                        none = "reallife_streets_test_no_pair";
                        break;
                    case TestKind.Dawdle:
                        started = StepOut(false, 4, now, out metres);
                        none = "reallife_streets_test_no_pair";
                        break;
                    case TestKind.StepLate:
                        started = StepOut(true, 5, now, out metres);
                        none = "reallife_streets_test_no_pair";
                        break;
                    case TestKind.Crowd:
                        int sent = Crowd(now, out metres);
                        started = sent > 0;
                        none = "reallife_solid_test_no_crowd";
                        if (started)
                        {
                            Logger?.Info($"Jaywalker: a test sent {sent} people across the road at once, the nearest {metres:F0} m from the player.");
                            var crowd = new Dictionary<string, string>(1) { { "count", sent.ToString() } };   // the game keeps the toast's data
                            Notifications.Show(NotificationType.Info, "reallife_solid_test_crowd", crowd, 8f);
                            return;
                        }
                        break;
                    case TestKind.Patrol:
                        started = Begin(null, StreetRules.Kind.Speeder, false, false, false, true, now);
                        if (started)
                        {
                            _patrolAt = now + 2f;
                        }
                        break;
                    default:
                        started = Begin(null, StreetRules.Kind.Speeder, false, false, false, true, now);
                        break;
                }
                if (!started)
                {
                    if (none != null)
                    {
                        TestRefusals.TellKey(Logger, "Reckless driver", none, kind.ToString());
                    }
                    else
                    {
                        TestRefusals.Tell(Logger, "Reckless driver", TestRefusal.NoMovingCar, (int)TestWithin, kind.ToString());
                    }
                    return;
                }
                if (_car.InUse && _car.Car != null)
                {
                    metres = Vector3.Distance(PlayerHelper.GetPosition(), _car.Car.transform.position);
                    _marked = true;
                    _markUntil = now + MarkFor;
                    _markLabel = "reallife_streets_test_marker".GetLocalization();
                }
                var data = new Dictionary<string, string>(1) { { "metres", ((int)metres).ToString() } };   // the game keeps the toast's data
                Notifications.Show(NotificationType.Info, "reallife_streets_test_started", data, 8f);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // The game's own destination marker over a test's car for a few seconds, so that the tester knows which one it is.
        private void Mark(float now)
        {
            if (now < _markUntil && _car.InUse && _car.Car != null)
            {
                _mark.Show(_car.Car.transform.position, _markLabel);
                return;
            }
            _marked = false;
            _mark.Hide();
        }
    }
}
