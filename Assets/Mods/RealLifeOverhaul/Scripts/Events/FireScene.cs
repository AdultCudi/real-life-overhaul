using System;
using System.Collections.Generic;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    public enum FireKind { Car, Bin, Building }

    /// <summary>
    /// One fire and the fire service's answer to it, from the first smoke to the engine driving
    /// away. What burns: a car that pulls up with smoke coming from its bonnet (its driver gets
    /// out and away), a bin on the pavement, or a shopfront (the ground-floor frontage and a
    /// window above; nothing of the building is changed).
    ///
    /// The road beside it is closed through the cordon. A fire engine drives up and stops in its
    /// lane short of the fire; three firefighters get out: one takes the nozzle to the fire with
    /// the hose laid behind them, one works the pump at the engine, one keeps people back. The
    /// fire is knocked down in stages (the flames shrink, the smoke turns white, steam), then
    /// damped down; the hose is rolled up, the crew go back to the engine, the tape and cones come
    /// in, and the engine leaves. A patrol car attends if the police option is on and its officer
    /// works the traffic. Bystanders gather at a distance and film. A 911 call sends the engine at
    /// once. If the engine cannot come (no truck free, no road), the fire burns itself out.
    ///
    /// Stepped four times a second. Every way out ends in <see cref="End"/>.
    /// </summary>
    internal sealed class FireScene : IEventScene
    {
        private enum Phase { Smoulder, Burning, Attack, Overhaul, PackUp, Leave, BurnOut, Done }

        private const float DispatchAfter = 20f;     // seconds from the first flames to the engine being sent, without a call
        private const float EngineBack = 12f;        // metres the engine stands short of the fire, in its lane
        private const float HardLimit = 250f;        // seconds: whatever state it is in, it is cleared by then
        private const int Bystanders = 3;

        private readonly EventDirector _director;
        private readonly List<Footprint> _footprints = new List<Footprint>(1);
        private readonly List<Vector3> _wreckPath = new List<Vector3>(48);
        private readonly List<Waypoint> _scratch = new List<Waypoint>(48);
        private readonly CityPerson[] _crew = new CityPerson[3];
        private readonly Vector3[] _crewHome = new Vector3[3];
        private readonly int[] _crewStep = new int[3];
        private readonly CityPerson[] _watchers = new CityPerson[Bystanders];
        private readonly float[] _watcherNext = new float[Bystanders];
        private CityPerson _driver;
        private FireKind _kind;
        private Phase _phase = Phase.Done;
        private Vector3 _seat;          // the seat of the fire, on the ground (or the bonnet)
        private Vector3 _upper;         // a building's window above; else unused
        private bool _hasUpper;
        private Vector3 _lanePoint, _laneDir, _wallAlong;
        private Transform _ownBin;      // a bin of the mod's own, stood where the street had none
        private Vector3 _attack, _outlet, _guard;
        private Vector3 _panel;          // the pump panel the operator works at, on the modelled engine; default on a repainted truck
        private Vector3 _door;          // a building's door: where occupants are warned
        private bool _hasDoor;
        private BuildingRegistration _business;   // the player's own, if that is what burns
        private GameObject _hazards;
        private Transform _nozzle;
        private bool _test, _called, _warned, _engineSent, _engineRetried, _engineParked, _patrolSent, _patrolParked, _closed, _wreckStopped, _waterOn, _told;
        private float _startedAt, _phaseAt, _phaseUntil, _engineAt, _engineDue, _nextSecond, _waterAt, _patrolAt, _leaveAt;
        private int _stage;
        private float _flameSize;
        private string _refusal;

        public FireScene(EventDirector director)
        {
            _director = director;
        }

        public FireKind Kind => _kind;
        public string Label => _kind == FireKind.Car ? "reallife_fire_label_car" : _kind == FireKind.Bin ? "reallife_fire_label_bin" : "reallife_fire_label_building";
        public Vector3 Focus => _seat;
        public float Reach => 240f;
        public string Refusal => _refusal;
        public TestRefusal Why { get; private set; }
        public int WhyMetres { get; private set; }

        // ---------------------------------------------------------------- staging

        public bool Stage(FireKind kind, Vector3 player, Vector3 near, bool test, float now)
        {
            _kind = kind;
            _test = test;
            _refusal = null;
            Why = TestRefusal.NothingFits;
            WhyMetres = 0;
            _called = _warned = _engineSent = _engineRetried = _engineParked = _patrolSent = _patrolParked = _closed = _wreckStopped = _waterOn = _told = false;
            _hasUpper = _hasDoor = false;
            _business = null;
            _stage = 0;
            _driver = null;
            if (!_director.Effects.Ready())
            {
                _refusal = "the game has no smoke effect loaded to make fire from";
                return false;
            }
            bool found = kind == FireKind.Car ? FindCar(player) : kind == FireKind.Bin ? FindBin(near) : FindShopfront(near);
            if (!found)
            {
                return false;
            }
            _startedAt = _phaseAt = now;
            _phase = Phase.Smoulder;
            _phaseUntil = now + (kind == FireKind.Car ? 14f : 7f);
            _nextSecond = now;
            _engineAt = now + (kind == FireKind.Car ? 14f : 7f) + DispatchAfter;
            _flameSize = kind == FireKind.Bin ? 0.55f : kind == FireKind.Car ? 1.0f : 1.4f;
            FireEffects effects = _director.Effects;
            effects.Smoke(0, _seat + Vector3.up * 0.3f, true, 7f);   // the first wisps are pale
            EventDirector.Logger?.Info($"Fire: {kind} fire staged{(test ? " (test)" : "")} {Mathf.Sqrt(CitySpots.Flat(_seat - player)):F0} m from the player"
                                       + (kind == FireKind.Building ? (_hasDoor ? ", a door found to warn the occupants at" : ", no door near: occupants are warned at the frontage") + (_hasUpper ? ", a window above" : ", no wall found above") : "")
                                       + (_business != null ? "; it is one of the player's own businesses." : "."));
            return true;
        }

        // A private car in traffic near the player: it is taken over, pulls in if there is room and stops.
        private bool FindCar(Vector3 player)
        {
            VehicleComponent car = CrimeCars.FindTrafficCar(player, _test ? 10f : 35f, _test ? 70f : 110f, false);
            if (car == null)
            {
                Why = TestRefusal.NoMovingCar;
                WhyMetres = _test ? 70 : 110;
                _refusal = "no private car on the move in range";
                return false;
            }
            ResponseUnit wreck = _director.Wreck;
            if (!CrimeCars.RoadAhead(car, 60f, 18f, _scratch, _wreckPath) || !wreck.Take(car))
            {
                _refusal = "the car had no road ahead to pull up on";
                return false;
            }
            for (int i = 0; i < _wreckPath.Count; i++)
            {
                wreck.Path.Add(_wreckPath[i]);
            }
            wreck.Driver.Begin(car, wreck.Path, wreck.Path[wreck.Path.Count - 1], 4f, 0f);
            wreck.Driver.CruiseSpeed = 7f;
            float over = SceneParking.KerbRoom(car, null);
            if (over > 0f)
            {
                wreck.Driver.PullOver(over);
            }
            _seat = Bonnet(car);
            return true;
        }

        private static Vector3 Bonnet(VehicleComponent car)
        {
            return car.transform.TransformPoint(new Vector3(0f, 1f, car.length * 0.33f));
        }

        // A bin or dumpster among the street furniture near a place.
        private bool FindBin(Vector3 near)
        {
            int mask = LayerMask.GetMask("StreetProps");
            Collider[] hits = mask != 0 ? Physics.OverlapSphere(near, _test ? 40f : 60f, mask, QueryTriggerInteraction.Ignore) : Array.Empty<Collider>();
            Collider best = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null || !IsBin(hit.name) && (hit.transform.parent == null || !IsBin(hit.transform.parent.name)))
                {
                    continue;
                }
                Vector3 at = hit.bounds.center;
                float sqr = CitySpots.Flat(at - near);
                if (sqr < nearest && hit.bounds.size.y < 2.2f && (_test || !CitySpots.InView(at)) && EventRoad.Lane(at, 14f, out _, out _))
                {
                    nearest = sqr;
                    best = hit;
                }
            }
            if (best == null)
            {
                // The street has no bin of its own here: one of the mod's is stood at the kerb, where there is room and a road beside it.
                for (int attempt = 0; attempt < 6 && _ownBin == null; attempt++)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    Vector3 probe = near + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (attempt * 3f);
                    if (CitySpots.Pavement(probe, 6f, out Vector3 pavement) && CitySpots.ClearQuietly(pavement, 0.6f) && (_test || !CitySpots.InView(pavement)) && RoadBeside(pavement))
                    {
                        _ownBin = _director.Props.Stand(EventProps.Bin, pavement, Random.value * 360f);
                        _seat = pavement + Vector3.up * 0.85f;
                    }
                }
                if (_ownBin != null)
                {
                    return true;
                }
                Why = TestRefusal.NoBin;
                WhyMetres = _test ? 40 : 60;
                _refusal = "no bin or dumpster beside a road among " + hits.Length + " pieces of street furniture" + (hits.Length > 0 ? " (first: '" + hits[0].name + "')" : "")
                           + ", and no clear pavement beside a road to stand one on";
                return false;
            }
            Bounds bounds = best.bounds;
            _seat = new Vector3(bounds.center.x, bounds.max.y - 0.1f, bounds.center.z);
            return RoadBeside(_seat);
        }

        private static bool IsBin(string name)
        {
            return name.IndexOf("trash", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("bin", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("dumpster", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("garbage", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("waste", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("litter", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // The nearest shopfront: a flat wall with pavement in front of it and a road beside that.
        private bool FindShopfront(Vector3 near)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 probe = near;
                if (attempt > 0)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    probe += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(6f, 16f);
                }
                if (!CitySpots.Pavement(probe, 8f, out Vector3 pavement) || !CitySpots.Shopfront(pavement, 1.2f, 0.4f, out Vector3 stand, out Vector3 outward))
                {
                    continue;
                }
                if ((!_test && CitySpots.InView(stand)) || !RoadBeside(stand))
                {
                    continue;
                }
                Vector3 wall = stand - outward * 1.2f;
                _wallAlong = Vector3.Cross(Vector3.up, outward);
                _seat = wall + outward * 0.35f;
                _seat.y = stand.y;
                // A window above: only if the wall really is there at that height.
                int walls = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
                _hasUpper = Physics.Raycast(stand + Vector3.up * 4.3f, -outward, out RaycastHit high, 2.6f, walls, QueryTriggerInteraction.Ignore);
                _upper = _hasUpper ? high.point + outward * 0.3f : default;
                FindOwner(stand, outward);
                return true;
            }
            Why = TestRefusal.NoShopfront;
            _refusal = "no flat shopfront with pavement in front and a road beside it within 16 m";
            return false;
        }

        // Whose building it is, and its door. Only the registration of a business the player runs is kept.
        private void FindOwner(Vector3 stand, Vector3 outward)
        {
            _door = stand + outward * 1.5f;
            var city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            float nearest = 14f * 14f;
            CityBuildingController owner = null;
            for (int i = 0; buildings != null && i < buildings.Length; i++)
            {
                CityBuildingController building = buildings[i];
                if (building == null || building.entranceDoors == null)
                {
                    continue;
                }
                for (int d = 0; d < building.entranceDoors.Length; d++)
                {
                    Transform door = building.entranceDoors[d] != null ? building.entranceDoors[d].doorTransform : null;
                    if (door == null)
                    {
                        continue;
                    }
                    float sqr = CitySpots.Flat(door.position - stand);
                    if (sqr < nearest)
                    {
                        nearest = sqr;
                        owner = building;
                        _door = door.position;
                        _hasDoor = true;
                    }
                }
            }
            if (owner != null && owner.building != null)
            {
                BuildingRegistration registration = owner.building.GetRegistration();
                _business = registration != null && registration.RentedByPlayer && registration.HasEstablishedBusiness ? registration : null;
            }
        }

        // The lane beside a fire on the pavement: what the cordon closes and the engine stands in.
        private bool RoadBeside(Vector3 fire)
        {
            if (!EventRoad.Lane(fire, 16f, out Waypoint lane, out _laneDir))
            {
                Why = TestRefusal.NoRoad;
                WhyMetres = 16;
                _refusal = "no plain stretch of road within 16 m of it";
                return false;
            }
            Vector3 offset = fire - lane.position;
            _lanePoint = lane.position + _laneDir * Vector3.Dot(offset, _laneDir);
            if (StreetSpots.Ground(_lanePoint, out float ground))
            {
                _lanePoint.y = ground;
            }
            return true;
        }

        private void CloseRoad()
        {
            if (_closed)
            {
                return;
            }
            _closed = true;
            _footprints.Clear();
            if (_kind == FireKind.Car)
            {
                VehicleComponent car = _director.Wreck.Car;
                _footprints.Add(EventRoad.Of(car));
                _laneDir = _footprints[0].Forward;
                _lanePoint = _footprints[0].Centre;
            }
            else
            {
                _footprints.Add(EventRoad.Stretch(_lanePoint, _laneDir, 2.5f, 1.2f));
            }
            _director.Cordon.Close(_lanePoint, _laneDir, _footprints, true);
        }

        // ---------------------------------------------------------------- running

        public bool Tick(float now, Vector3 player)
        {
            if (_phase == Phase.Done)
            {
                return false;
            }
            if (now - _startedAt > HardLimit)
            {
                EventDirector.Logger?.Warn($"Fire: still in phase {_phase} after {HardLimit:F0} s; cleared.");
                return false;
            }
            SceneCordon cordon = _director.Cordon;
            cordon.Tick(now);
            _director.Officers[0].Tick(now);
            Wreck(now);
            Units(now);
            bool second = now >= _nextSecond;
            if (second)
            {
                _nextSecond = now + 1f;
                _director.People.Tick(_seat, false);
                Watchers(now);
                Crew(now);
            }
            FireEffects effects = _director.Effects;
            switch (_phase)
            {
                case Phase.Smoulder:
                    if (now >= _phaseUntil && (_kind != FireKind.Car || _wreckStopped))
                    {
                        Enter(Phase.Burning, now, 0f);
                        Ignite();
                        if (_kind != FireKind.Car)
                        {
                            CloseRoad();
                        }
                        if (!_told)
                        {
                            _told = true;
                            Notifications.Show(NotificationType.Warning, Started(), null, 9f);
                        }
                    }
                    break;
                case Phase.Burning:
                    if (!_engineSent && now >= _engineAt)
                    {
                        SendEngine(now);
                    }
                    break;
                case Phase.Attack:
                    if (second && _waterOn)
                    {
                        Knockdown(now);
                    }
                    else if (second && now - _phaseAt > 50f)
                    {
                        // Nobody got water on it (the nozzle never reached its place): it is put out from the engine's own tank.
                        _waterOn = true;
                        _waterAt = now;
                        _stage = 0;
                        _phaseUntil = now + 6f;
                    }
                    break;
                case Phase.Overhaul:
                    if (now >= _phaseUntil)
                    {
                        Enter(Phase.PackUp, now, 30f);
                        effects.Steam(_seat, false);
                        effects.Smoke(0, _seat, true, 0f);
                        effects.Smoke(1, _upper, true, 0f);
                        SendCrewHome();
                        SendWatchersOff();
                        Settle(now);
                    }
                    break;
                case Phase.PackUp:
                    if (second)
                    {
                        _director.Hose.RollUp();
                        if ((CrewAboard() && !_director.Hose.Laid) || now >= _phaseUntil)
                        {
                            Depart(now);
                        }
                    }
                    break;
                case Phase.Leave:
                    if (second && Left(now))
                    {
                        _phase = Phase.Done;
                        return false;
                    }
                    break;
                case Phase.BurnOut:
                    if (second)
                    {
                        float left = Mathf.Clamp01((_phaseUntil - now) / 30f);
                        for (int i = 0; i < FireEffects.Flames; i++)
                        {
                            effects.SetFlame(i, left);
                        }
                        effects.Glow(_seat + Vector3.up, left);
                        if (now >= _phaseUntil)
                        {
                            effects.Smoke(0, _seat, true, 0f);
                            effects.Smoke(1, _upper, true, 0f);
                            Notifications.Show(NotificationType.Info, "reallife_fire_burned_out", null, 8f);
                            SendWatchersOff();
                            Settle(now);
                            Depart(now);
                        }
                    }
                    break;
            }
            return true;
        }

        private void Enter(Phase phase, float now, float lasts)
        {
            _phase = phase;
            _phaseAt = now;
            _phaseUntil = now + lasts;
        }

        private void Ignite()
        {
            FireEffects effects = _director.Effects;
            effects.Flame(0, _seat, _flameSize);
            effects.Smoke(0, _seat + Vector3.up * (_kind == FireKind.Building ? 2f : 0.6f), false, _kind == FireKind.Bin ? 9f : 16f);
            if (_kind == FireKind.Building)
            {
                effects.Flame(1, _seat + _wallAlong * 1.3f, _flameSize * 0.8f);
                if (_hasUpper)
                {
                    effects.Flame(2, _upper, 0.9f);
                    effects.Smoke(1, _upper + Vector3.up * 0.8f, false, 12f);
                }
            }
            effects.Glow(_seat + Vector3.up * 1.2f, _kind == FireKind.Bin ? 0.5f : 1f);
        }

        // The burning car: it pulls up, stops and is pinned; its driver gets out and away.
        private void Wreck(float now)
        {
            if (_kind != FireKind.Car || _wreckStopped)
            {
                return;
            }
            ResponseUnit wreck = _director.Wreck;
            if (wreck.IsLost || !wreck.InUse)
            {
                _phase = Phase.Done;   // the pool took the car back: there is nothing to burn
                return;
            }
            VehicleComponent car = wreck.Car;
            _seat = Bonnet(car);
            _director.Effects.Smoke(0, _seat, true, 7f);
            if (wreck.Driver.Active && now - _startedAt > 3.5f)
            {
                wreck.Driver.Brake();
            }
            if (wreck.Driver.Active && now - _startedAt < 12f)
            {
                return;
            }
            wreck.Park();
            _wreckStopped = true;
            SceneObstacles.Add(car, _director.transform);
            _hazards = TrafficEffects.CreateFlasher(car, "RealLifeOverhaul Hazard Lights", TrafficEffects.BeaconHeight(car) - 0.6f, new Color(1f, 0.6f, 0.05f), new Color(1f, 0.6f, 0.05f), 0.45f);
            CloseRoad();
            // The driver: out of the driver's door, and off to the pavement to phone it in.
            if (StreetSpots.BesideCar(car, StreetSpots.Left, 0.7f, out Vector3 door) && StreetSpots.OutOfVehicles(ref door))
            {
                _driver = _director.People.Put(CityLook.Civilian, door, _seat, true);
                if (_driver != null)
                {
                    ActorPool.PlayDoorSound(door);
                    if (CitySpots.PavementRing(_seat, 12f, 20f, false, out Vector3 away))
                    {
                        _driver.WalkTo(away, true);
                    }
                }
            }
        }

        private void SendEngine(float now)
        {
            if (_kind == FireKind.Car && !_wreckStopped)
            {
                return;
            }
            CloseRoad();
            FireEngine engine = _director.Engine;
            if (engine.Dispatch(_director.Cordon, _lanePoint, EngineBack + (_kind == FireKind.Car ? _footprints[0].HalfLength : 0f)))
            {
                _engineSent = true;
                _engineDue = now + 85f;
                _patrolAt = now + 6f;
                Notifications.Show(NotificationType.Info, _called ? "reallife_fire_engine_called" : Random.value < 0.5f ? "reallife_fire_engine_coming_a" : "reallife_fire_engine_coming_b", null, 7f);
                return;
            }
            EventDirector.Logger?.Info("Fire: no engine could be sent (" + engine.Refusal + ")" + (_engineRetried ? "; the fire is left to burn out." : "; trying once more."));
            if (!_engineRetried)
            {
                _engineRetried = true;
                _engineAt = now + 8f;
                return;
            }
            _engineSent = true;   // nothing more is tried
            Enter(Phase.BurnOut, now, _kind == FireKind.Bin ? 35f : 55f);
        }

        // The engine and the patrol car on their way, and standing.
        private void Units(float now)
        {
            FireEngine engine = _director.Engine;
            if (_engineSent && !_engineParked && engine.Out && _phase == Phase.Burning)
            {
                EmergencyDriver driver = engine.Unit.Driver;
                if (engine.Lost)
                {
                    engine.Release(true, false);
                    Enter(Phase.BurnOut, now, 45f);
                    EventDirector.Logger?.Info("Fire: the engine's truck was taken back by the traffic pool on its way; the fire is left to burn out.");
                    return;
                }
                // How far it still has to go is measured along its route, never as the crow flies: a truck on the next street is not near.
                bool waiting = driver.Active && !driver.Arrived && (driver.HeldFor > 9f || now >= _engineDue);
                float away = waiting ? driver.RouteLeft : Mathf.Sqrt(CitySpots.Flat(engine.Car.transform.position - _lanePoint));
                bool held = waiting && driver.HeldFor > 9f && away < 45f;
                if (driver.Arrived || !driver.Active || held || (now >= _engineDue && away < 60f))
                {
                    engine.Park();
                    _engineParked = true;
                    PlanCrew(engine.Car);
                    _director.Cordon.Raise(now);
                    Taper();
                    Enter(Phase.Attack, now, 0f);
                    EventDirector.Logger?.Info($"Fire: the engine is parked {away:F0} m from the fire{(held ? " (held up there by what is in front of it)" : "")}; crew out.");
                }
                else if (now >= _engineDue)
                {
                    engine.Release(true, false);
                    Enter(Phase.BurnOut, now, 40f);
                    EventDirector.Logger?.Info($"Fire: the engine was still {away:F0} m away after 85 s; it is stood down and the fire is left to burn out.");
                }
            }

            ResponseUnit patrol = _director.Patrol;
            if (_engineSent && !_patrolSent && _phase == Phase.Burning && now >= _patrolAt && Settings.Police && Settings.People && engine.Out)
            {
                _patrolSent = true;
                if (_director.Cordon.Stand(false, EngineBack + 13f + (_kind == FireKind.Car ? _footprints[0].HalfLength : 0f), out Waypoint anchor, out Vector3 stand)
                    && patrol.Spawn(UnitKind.Police, anchor, Random.Range(150f, 200f), 40f))
                {
                    Vector3 beyond = stand - anchor.position;
                    beyond.y = 0f;
                    if (beyond.sqrMagnitude > 1f)
                    {
                        patrol.Path.Add(stand);
                    }
                    patrol.Driver.BeginRoute(patrol.Car, patrol.Path, 30f);
                    EventDirector.Logger?.Info("Fire: a patrol car is sent to work the traffic.");
                }
                else
                {
                    EventDirector.Logger?.Info("Fire: no patrol car could be sent (" + (patrol.Refusal ?? "no lane to stand in") + "); the crew see to the road themselves.");
                }
            }
            if (_patrolSent && !_patrolParked && patrol.InUse && _phase != Phase.Leave)
            {
                EmergencyDriver driver = patrol.Driver;
                if (patrol.IsLost)
                {
                    patrol.Release(true, false);
                }
                else if (driver.Arrived || !driver.Active || driver.HeldFor > 8f)
                {
                    patrol.Park();
                    _patrolParked = true;
                    SceneObstacles.Add(patrol.Car, _director.transform);
                    ActorRole officer = _director.Officers[0];
                    SceneCordon cordon = _director.Cordon;
                    if (StreetSpots.BesideCar(patrol.Car, StreetSpots.Left, 0.7f, out Vector3 door) && officer.Enter(door, ActorLook.Officer, 2f, true))
                    {
                        ActorPool.PlayDoorSound(door);
                        cordon.Raise(now);
                        officer.HiVis(true);
                        officer.Go(cordon.DirectorPost, Pace.Walk, cordon.Oncoming, 25f);
                        officer.Work("direct_traffic");
                    }
                    Taper();
                }
            }
        }

        private void Taper()
        {
            SceneCordon cordon = _director.Cordon;
            float near = 0f, far = 0f;
            VehicleComponent engine = _director.Engine.Car;
            if (engine != null && _engineParked)
            {
                cordon.Reach(engine.transform.position, Mathf.Max(1.5f, engine.length * 0.5f), ref near, ref far);
            }
            VehicleComponent patrol = _director.Patrol.Car;
            if (patrol != null && _patrolParked)
            {
                cordon.Reach(patrol.transform.position, Mathf.Max(1.5f, patrol.length * 0.5f), ref near, ref far);
            }
            cordon.LayTaper(_patrolSent && !_patrolParked && _director.Patrol.InUse ? -1f : near, far);
        }

        // ---------------------------------------------------------------- the crew

        // Where each of the three gets out, and where the nozzle and the one who keeps people back go.
        private void PlanCrew(VehicleComponent engine)
        {
            TrafficTools.Footprint(engine, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float side = Vector3.Dot(_seat - centre, right) >= 0f ? 1f : -1f;
            FireEngine unit = _director.Engine;
            bool modelled = unit.Modelled;
            _panel = default;
            for (int i = 0; i < 3; i++)
            {
                Vector3 home;
                bool found;
                if (modelled)
                {
                    // On the modelled engine each gets out at a door that is there (the one with the nozzle at the front door,
                    // the third at the crew door behind it), and the pump operator stands at the pump panel of that side:
                    // 0.50 m off it, where pump_panel's hands are on it. That place is the engine's own, so it is only put on
                    // the walkable ground nearest to it and not moved out from the vehicle.
                    float out_ = i == 1 ? EngineLivery.PanelOut + 0.50f : EngineLivery.BodyHalfWidth + 0.7f;
                    float ahead = i == 0 ? EngineLivery.FrontDoorAhead : i == 1 ? EngineLivery.PanelAhead : EngineLivery.CrewDoorAhead;
                    home = unit.BodyPoint(side * out_, 0f, ahead);
                    found = i == 1 ? StreetSpots.OnRoad(home, 0.6f, out home) : StreetSpots.OnRoad(home, 1.2f, out home) && StreetSpots.OutOfVehicles(ref home);
                    if (i == 1)
                    {
                        _panel = unit.BodyPoint(side * EngineLivery.PanelOut, 0f, EngineLivery.PanelAhead);
                    }
                }
                else
                {
                    home = centre + right * (side * (halfWidth + 0.7f)) + forward * (halfLength * 0.5f - i * 1.15f);
                    found = StreetSpots.OnRoad(home, 1.2f, out home) && StreetSpots.OutOfVehicles(ref home);
                }
                _crewHome[i] = found ? home : default;
                _crewStep[i] = 0;
            }
            // The hose starts on the ground under the outlet of that side (clear of the truck's own colliders, which are
            // wider than the modelled body), or beside a repainted truck a little behind its middle.
            _outlet = modelled
                ? unit.BodyPoint(side * Mathf.Max(EngineLivery.BodyHalfWidth, halfWidth) + side * 0.15f, 0f, EngineLivery.OutletAhead)
                : centre + right * (side * (halfWidth + 0.15f)) - forward * (halfLength * 0.2f);
            if (StreetSpots.Ground(_outlet, out float ground))
            {
                _outlet.y = ground;
            }
            // The nozzle: on the line from the engine to the fire, a few metres short of it, where there is room to stand.
            Vector3 from = centre - _seat;
            from.y = 0f;
            from = from.sqrMagnitude > 0.5f ? from.normalized : -_laneDir;
            float reach = _kind == FireKind.Bin ? 3.6f : _kind == FireKind.Car ? 5f : 5.5f;
            _attack = default;
            for (int attempt = 0; attempt < 5 && _attack == default; attempt++)
            {
                Vector3 want = _seat + Quaternion.Euler(0f, (attempt % 2 == 0 ? 1f : -1f) * (attempt / 2) * 22f, 0f) * from * (reach + (attempt / 3) * 1.5f);
                if (StreetSpots.OnRoad(want, 1.2f, out Vector3 spot) && StreetSpots.OutOfVehicles(ref spot) && CitySpots.ClearQuietly(spot, 0.4f))
                {
                    _attack = spot;
                }
            }
            Vector3 back = _seat + from * (reach + 4.5f) + Vector3.Cross(Vector3.up, from) * 3f;
            _guard = StreetSpots.OnRoad(back, 2f, out Vector3 guard) && StreetSpots.OutOfVehicles(ref guard) && CitySpots.ClearQuietly(guard, 0.4f) ? guard : default;
        }

        // Once a second: one of them gets out, the next second the next; each goes about their job.
        private void Crew(float now)
        {
            if (!_engineParked || (_phase != Phase.Attack && _phase != Phase.Overhaul && _phase != Phase.PackUp))
            {
                return;
            }
            EventPeople people = _director.People;
            CityProps shelf = people.Props.Shelf;
            VehicleComponent engine = _director.Engine.Car;
            for (int i = 0; i < 3; i++)
            {
                CityPerson who = _crew[i];
                if (who != null && who.Lost)
                {
                    if (i == 0)
                    {
                        StopWater();
                    }
                    _crew[i] = null;
                    _crewStep[i] = 9;   // knocked down by a car: the casualty system has them
                    continue;
                }
                switch (_crewStep[i])
                {
                    case 0:   // getting out (one a second, and only while the fire is being fought)
                        if (_phase == Phase.Attack && engine != null && (i == 0 || _crewStep[i - 1] != 0))
                        {
                            if (_crewHome[i] == default)
                            {
                                _crewStep[i] = 9;   // no clear ground beside the engine for this one: they stay aboard
                                break;
                            }
                            who = people.Put(CityLook.Civilian, _crewHome[i], _seat, true, false);
                            if (who == null)
                            {
                                _crewStep[i] = 9;
                                break;
                            }
                            FireOutfit.Wear(who);
                            people.Wear(who, EventProps.Helmet, HumanBodyBones.Head, 0.085f, 0.012f);
                            people.Wear(who, EventProps.AirPack, HumanBodyBones.UpperChest, 0f, 0f);
                            ActorPool.PlayDoorSound(_crewHome[i]);
                            _crew[i] = who;
                            _crewStep[i] = 1;
                            Vector3 goal = i == 0 ? _attack : i == 2 ? _guard : default;
                            if (goal != default && who.WalkTo(goal, i == 0))
                            {
                                _crewStep[i] = 2;
                            }
                            else if (i == 1)
                            {
                                // Square to the pump panel on the modelled engine; on a repainted truck, to the middle of its body
                                // (not its origin, which the game's trucks have under the rear axle).
                                Vector3 panel = _panel;
                                if (panel == default)
                                {
                                    TrafficTools.Footprint(engine, out panel, out _, out _, out _);
                                }
                                who.Turn(panel);
                                who.Work(EventPeople.Clip("pump_panel", "counter_wipe"), shelf, 0.4f);   // hands on the pump panel
                                _crewStep[i] = 3;
                            }
                            else
                            {
                                who.Work("radio_talk", shelf, 0.4f);     // nowhere clear to go: they stay by the engine, on the radio
                                _crewStep[i] = 3;
                            }
                            return;
                        }
                        break;
                    case 2:   // walking to their place
                        Vector3 place = i == 0 ? _attack : _guard;
                        if (who.Arrived && who.Near(place, 0.8f))
                        {
                            who.Stand();
                            _crewStep[i] = 3;
                            if (i == 0)
                            {
                                who.Turn(_seat);
                                StartWater(who, now);
                            }
                            else
                            {
                                who.Face(place + (place - _seat));   // their back to the fire, facing whoever comes too close
                                who.Work(EventPeople.Clip("keep_back", "direct_traffic"), shelf, 0.4f);
                            }
                        }
                        else if (who.Arrived)
                        {
                            who.WalkTo(place, i == 0);
                        }
                        break;
                    case 4:   // damping down: on one knee at what is left of it
                        break;
                    case 5:   // walking back to the engine
                        if ((who.Arrived && who.Near(_crewHome[i], 1.1f)) || now >= _phaseUntil - 4f)
                        {
                            ActorPool.PlayDoorSound(who.Position);
                            people.Release(who);
                            _crew[i] = null;
                            _crewStep[i] = 9;
                        }
                        else if (who.Arrived)
                        {
                            who.WalkTo(_crewHome[i]);
                        }
                        break;
                }
            }
        }

        // The nozzle is in their hands, the hose behind them, water on the fire.
        private void StartWater(CityPerson who, float now)
        {
            EventPeople people = _director.People;
            _nozzle = people.Props.Take(EventProps.Nozzle);
            if (_nozzle == null)
            {
                return;
            }
            Transform body = who.Character.transform;
            Vector3 at = body.position + body.forward * 0.46f + body.right * 0.17f + Vector3.up * 1.02f;
            _nozzle.SetParent(_director.transform, true);
            _nozzle.position = at;
            Aim(_seat);
            if (ClipLibrary.Has("hose_hold") && ClipLibrary.TryGet("hose_hold", out CityClip hold))
            {
                // The clip is made to exactly this nozzle (0.46 m ahead, 0.17 m right, 1.02 m up, 12 degrees up) with the PALMS
                // on its two grips. So the wrists are kept where the clip's own grip has them on the nozzle: nothing to
                // correct at the clip's own aim, and the hands stay on it when it is aimed higher or lower.
                who.Work("hose_hold", people.Props.Shelf, 0.3f, ClipEnd.Return, false);
                _director.Hold.Begin(who.Character.animator, _nozzle, hold.Grip);
            }
            else
            {
                who.WorkOver("carry_box_hold", people.Props.Shelf, 0.3f, ClipEnd.Return, BodyMask.Arms, false);   // both arms forward; the hands are then put on the nozzle
                _director.Hold.Begin(who.Character.animator, _nozzle);
            }
            float run = Mathf.Sqrt(CitySpots.Flat(_outlet - at));
            if (run > 34f || !_director.Hose.Lay(_outlet, at - _nozzle.forward * 0.05f))
            {
                EventDirector.Logger?.Info($"Fire: no hose laid ({run:F0} m from the engine to the nozzle).");
            }
            _waterOn = true;
            _waterAt = now;
            _stage = 0;
            _phaseUntil = now + 8f;
            EventDirector.Logger?.Info($"Fire: water on, {now - _startedAt:F0} s after the first smoke.");
        }

        private void Aim(Vector3 target)
        {
            if (_nozzle == null)
            {
                return;
            }
            Vector3 line = target - _nozzle.position;
            Vector3 flat = new Vector3(line.x, 0f, line.z);
            // Held a little above the line to the fire: the water arcs on to it.
            Vector3 aim = flat.sqrMagnitude > 0.01f ? (flat.normalized + Vector3.up * Mathf.Clamp(line.y / Mathf.Max(1f, flat.magnitude) + 0.22f, 0.05f, 0.9f)).normalized : Vector3.forward;
            _nozzle.rotation = Quaternion.LookRotation(aim);
            _director.Effects.Jet(_nozzle.position + _nozzle.forward * 0.42f, target, true);
        }

        private void StopWater()
        {
            _director.Effects.Jet(default, default, false);
            _director.Hold.End();
            if (_nozzle != null)
            {
                _director.People.Props.Return(_nozzle);
                _nozzle = null;
            }
        }

        // The fire is knocked down in three stages, then it is out.
        private void Knockdown(float now)
        {
            if (now < _phaseUntil)
            {
                return;
            }
            FireEffects effects = _director.Effects;
            _stage++;
            if (_stage == 1)
            {
                effects.SetFlame(0, 0.6f);
                effects.SetFlame(1, 0.5f);
                effects.Glow(_seat + Vector3.up * 1.2f, 0.7f);
                _phaseUntil = now + 8f;
            }
            else if (_stage == 2)
            {
                effects.SetFlame(0, 0.25f);
                effects.SetFlame(1, 0f);
                effects.Smoke(0, _seat + Vector3.up * 0.6f, true, 12f);
                effects.Steam(_seat + Vector3.up * 0.3f, true);
                effects.Glow(_seat + Vector3.up * 1.2f, 0.35f);
                if (_hasUpper)
                {
                    Aim(_upper);   // the ground floor is under control: up to the window
                }
                _phaseUntil = now + 8f;
            }
            else
            {
                for (int i = 0; i < FireEffects.Flames; i++)
                {
                    effects.SetFlame(i, 0f);
                }
                effects.Smoke(0, _seat + Vector3.up * 0.6f, true, 6f);
                if (_hasUpper)
                {
                    effects.Smoke(1, _upper + Vector3.up * 0.8f, true, 6f);
                }
                effects.Glow(default, 0f);
                StopWater();
                Enter(Phase.Overhaul, now, 11f);
                Notifications.Show(NotificationType.Success, Random.value < 0.5f ? "reallife_fire_out_a" : "reallife_fire_out_b", null, 8f);
                // Damping down: the one who had the nozzle goes in close and down on a knee at what is left.
                CityPerson lead = _crew[0];
                if (lead != null && !lead.Lost)
                {
                    lead.StopOver(0.3f);
                    lead.Work("kneel_work", _director.People.Props.Shelf, 0.5f);
                    _crewStep[0] = 4;
                }
                EventDirector.Logger?.Info($"Fire: out {now - _startedAt:F0} s after the first smoke ({now - _waterAt:F0} s of water).");
            }
        }

        private void SendCrewHome()
        {
            StopWater();
            for (int i = 0; i < 3; i++)
            {
                CityPerson who = _crew[i];
                if (who == null || who.Lost)
                {
                    continue;
                }
                if (_crewHome[i] != default && who.WalkTo(_crewHome[i]))
                {
                    _crewStep[i] = 5;
                }
                else
                {
                    _director.People.Release(who);
                    _crew[i] = null;
                    _crewStep[i] = 9;
                }
            }
        }

        // The show is over: the bystanders and the car's driver walk off, out of view, before they are taken back.
        private void SendWatchersOff()
        {
            for (int i = 0; i < Bystanders; i++)
            {
                _director.People.SendOff(_watchers[i]);
                _watchers[i] = null;
            }
            _director.People.SendOff(_driver);
            _driver = null;
        }

        private bool CrewAboard()
        {
            return _crew[0] == null && _crew[1] == null && _crew[2] == null;
        }

        // ---------------------------------------------------------------- bystanders

        private void Watchers(float now)
        {
            EventPeople people = _director.People;
            CityProps shelf = people.Props.Shelf;
            if (_driver != null)
            {
                if (_driver.Lost)
                {
                    _driver = null;
                }
                else if (_driver.Arrived && _driver.Playing == null && _phase != Phase.Smoulder)
                {
                    _driver.Stand();
                    _driver.Face(_seat);
                    _driver.Work("radio_talk", shelf, 0.4f);   // a hand up at the collar: on the phone to somebody about their car
                }
            }
            if (_phase != Phase.Burning && _phase != Phase.Attack && _phase != Phase.BurnOut)
            {
                return;
            }
            for (int i = 0; i < Bystanders; i++)
            {
                CityPerson who = _watchers[i];
                if (who != null && who.Lost)
                {
                    _watchers[i] = who = null;
                }
                if (who == null)
                {
                    // Room is kept in the pool for the crew of three.
                    if (now >= _watcherNext[i] && people.Free > 3 && now - _startedAt > 10f + i * 6f)
                    {
                        _watcherNext[i] = now + 12f;
                        if (CitySpots.PavementRing(_seat, 13f, 19f, false, out Vector3 line) && CitySpots.ClearQuietly(line, 0.45f))
                        {
                            _watchers[i] = people.WalkIn(CityLook.Civilian, line, false);
                        }
                    }
                    continue;
                }
                if (who.Arrived && now >= _watcherNext[i])
                {
                    _watcherNext[i] = now + Random.Range(6f, 10f);
                    who.Stand();
                    who.Face(_seat);
                    who.Work(Random.value < 0.65f ? "take_photo" : "watch_nod", shelf, 0.3f);   // most of them film it
                }
            }
        }

        // ---------------------------------------------------------------- the player

        public string Prompt(Vector3 player)
        {
            bool burning = _phase == Phase.Burning || _phase == Phase.Attack || _phase == Phase.BurnOut;
            return _kind == FireKind.Building && burning && !_warned && !PlayerHelper.IsUsingVehicle && CitySpots.Flat(player - _door) < 4f * 4f
                ? "reallife_fire_prompt_warn" : null;
        }

        public void Interact(float now, Vector3 player)
        {
            if (Prompt(player) == null)
            {
                return;
            }
            _warned = true;
            ModState.AddModifier(ModState.Helped);
            Notifications.Show(NotificationType.Success, Random.value < 0.5f ? "reallife_fire_warned_a" : "reallife_fire_warned_b", null, 8f);
        }

        public bool Report(float now)
        {
            if (_called || _phase == Phase.Done || _phase == Phase.Leave || _phase == Phase.PackUp || _phase == Phase.BurnOut)
            {
                return false;   // already rung in, or already dealt with
            }
            _called = true;
            ModState.AddModifier(ModState.Helped);
            if (_engineSent)
            {
                return true;    // already on its way: the call still counts for the caller
            }
            _engineAt = now;    // sent on the next step, as soon as there is a road for it
            if (_phase == Phase.Smoulder)
            {
                _phaseUntil = Mathf.Min(_phaseUntil, now + 3f);
            }
            EventDirector.Logger?.Info($"Fire: reported by the player {now - _startedAt:F0} s after the first smoke; the engine is sent at once.");
            return true;
        }

        // ---------------------------------------------------------------- the end of it

        // What the fire cost, once it is out or has burned out: only for a business of the player's own.
        private void Settle(float now)
        {
            if (_kind != FireKind.Building || _business == null)
            {
                return;
            }
            BuildingRegistration business = _business;
            _business = null;
            FireDamage.Apply(business, _waterOn ? _waterAt - _startedAt : 120f, _warned, _called);
        }

        // Tape and cones in; the burnt car goes; the vehicles leave.
        private void Depart(float now)
        {
            StopWater();
            _director.Hose.Take();
            for (int i = 0; i < 3; i++)
            {
                if (_crew[i] != null)
                {
                    _director.People.Release(_crew[i]);
                    _crew[i] = null;
                }
            }
            SceneCordon cordon = _director.Cordon;
            cordon.LiftTaper();
            cordon.Lower();
            ActorRole officer = _director.Officers[0];
            VehicleComponent patrol = _director.Patrol.Car;
            if (officer.Present && patrol != null && _patrolParked && StreetSpots.BesideCar(patrol, StreetSpots.Left, 0.7f, out Vector3 door))
            {
                officer.HiVis(false);
                officer.Board(door, Pace.Walk, 14f);
            }
            else
            {
                officer.Exit();
            }
            Enter(Phase.Leave, now, 45f);
            _leaveAt = now + 5f;
            _stage = 0;
        }

        // The leaving, a step a second: the wreck is taken away, the road opened, the engine and then the patrol car drive off.
        private bool Left(float now)
        {
            FireEngine engine = _director.Engine;
            ResponseUnit patrol = _director.Patrol;
            ResponseUnit wreck = _director.Wreck;
            if (_stage == 0)
            {
                bool unseen = !wreck.InUse || wreck.Car == null || !CitySpots.InView(wreck.Car.transform.position);
                if (now < _leaveAt || (!unseen && now < _leaveAt + 14f))
                {
                    return false;
                }
                ReleaseWreck(true);
                _director.Effects.StopAll();
                _director.Cordon.Open();
                if (engine.Out && _engineParked)
                {
                    engine.Leave();
                }
                else if (engine.Out)
                {
                    engine.Release(true, false);
                }
                _stage = 1;
                _leaveAt = now + 5f;
                EventDirector.Logger?.Info("Fire: the road is open again; the engine is leaving.");
                return false;
            }
            if (_stage == 1 && now >= _leaveAt && !_director.Officers[0].Present)   // the officer is aboard (Board has its own time limit)
            {
                _stage = 2;
                if (patrol.InUse && (!_patrolParked || !EventRoad.DriveOff(patrol, true)))
                {
                    patrol.Release(true, false, true);
                }
            }
            // Gone once they have driven their route out, or after the time allowed: then they are ordinary traffic again.
            bool engineGone = !engine.Out || engine.Lost || engine.Unit.Driver.Arrived || !engine.Unit.Driver.Active;
            bool patrolGone = _stage < 2 ? false : !patrol.InUse || patrol.IsLost || patrol.Driver.Arrived || !patrol.Driver.Active;
            return (engineGone && patrolGone) || now >= _phaseUntil;
        }

        private void ReleaseWreck(bool trafficRunning)
        {
            if (_hazards != null)
            {
                Object.Destroy(_hazards);
                _hazards = null;
            }
            ResponseUnit wreck = _director.Wreck;
            if (wreck.InUse)
            {
                if (wreck.Car != null)
                {
                    SceneObstacles.Remove(wreck.Car);
                }
                wreck.Release(trafficRunning, remove: true);   // a burnt-out car does not drive away: it is taken off the road
            }
        }

        public void End(bool trafficRunning)
        {
            _phase = Phase.Done;
            if (_ownBin != null)
            {
                Transform bin = _ownBin;
                _ownBin = null;
                _director.Props.Return(bin);
            }
            _director.Effects.StopAll();
            StopWater();
            _director.Hose.Take();
            for (int i = 0; i < 3; i++)
            {
                _crew[i] = null;
                _crewStep[i] = 0;
            }
            for (int i = 0; i < Bystanders; i++)
            {
                _watchers[i] = null;
                _watcherNext[i] = 0f;
            }
            _driver = null;
            _director.People.ReleaseAll();
            _director.Officers[0].Exit();
            ReleaseWreck(trafficRunning);
            FireEngine engine = _director.Engine;
            if (engine.Out)
            {
                engine.Release(trafficRunning, remove: !trafficRunning);
            }
            ResponseUnit patrol = _director.Patrol;
            if (patrol.InUse)
            {
                if (patrol.Car != null)
                {
                    SceneObstacles.Remove(patrol.Car);
                }
                patrol.Release(trafficRunning, remove: !trafficRunning, quiet: true);
            }
            _director.Cordon.Open();
            _business = null;
            if (_ownBin != null)
            {
                _director.Props.Return(_ownBin);
                _ownBin = null;
            }
        }

        private string Started()
        {
            bool second = Random.value < 0.5f;
            switch (_kind)
            {
                case FireKind.Car: return second ? "reallife_fire_started_car_b" : "reallife_fire_started_car_a";
                case FireKind.Bin: return second ? "reallife_fire_started_bin_b" : "reallife_fire_started_bin_a";
                default: return _business != null ? "reallife_fire_started_own" : second ? "reallife_fire_started_building_b" : "reallife_fire_started_building_a";
            }
        }
    }

    /// <summary>
    /// The firefighter's hands on the nozzle. The nozzle stands in the world in front of them,
    /// aimed at the fire; after the animation each frame both wrists are put where they belong
    /// on it (TwoBoneReach), eased in over a moment. With the clip <c>hose_hold</c> that is
    /// where the clip's own grip has the wrists (the palms are then on the nozzle's two grips,
    /// 0.06 and 0.30 m along it, and at the clip's own aim there is nothing to correct); without
    /// it the wrists go to those two points themselves. Runs only while somebody holds a nozzle
    /// and is being animated; switched off otherwise. About thirty vector operations a frame and
    /// no allocation.
    /// </summary>
    internal sealed class HoseHold : MonoBehaviour
    {
        private static readonly Vector3 RightOnNozzle = new Vector3(0f, 0f, 0.06f);
        private static readonly Vector3 LeftOnNozzle = new Vector3(0f, -0.02f, 0.30f);

        private Animator _animator;
        private Transform _nozzle;
        private Transform _rightUpper, _rightLower, _rightHand, _leftUpper, _leftLower, _leftHand;
        private Vector3 _right = RightOnNozzle;   // where each wrist belongs, in the nozzle's own space
        private Vector3 _left = LeftOnNozzle;
        private float _weight;

        private void Awake()
        {
            enabled = false;
        }

        /// <summary>The hands as a clip holds the nozzle: its grip says where the nozzle is on the right hand bone and where the left wrist is on the nozzle.</summary>
        public void Begin(Animator animator, Transform nozzle, ToolGrip grip)
        {
            Begin(animator, nozzle);
            if (enabled && grip.Hand == HumanBodyBones.RightHand && grip.TwoHanded && grip.OtherHand == HumanBodyBones.LeftHand)
            {
                _right = Quaternion.Inverse(grip.Rotation) * -grip.Position;
                _left = grip.WristOnTool;
            }
        }

        public void Begin(Animator animator, Transform nozzle)
        {
            End();
            _right = RightOnNozzle;
            _left = LeftOnNozzle;
            if (animator == null || nozzle == null || !animator.isHuman)
            {
                return;
            }
            _rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (_rightUpper == null || _rightLower == null || _rightHand == null || _leftUpper == null || _leftLower == null || _leftHand == null)
            {
                return;
            }
            _animator = animator;
            _nozzle = nozzle;
            _weight = 0f;
            enabled = true;
        }

        public void End()
        {
            _animator = null;
            _nozzle = null;
            enabled = false;
        }

        private void LateUpdate()
        {
            try
            {
                if (_animator == null || _nozzle == null)
                {
                    End();
                    return;
                }
                if (!_animator.enabled || !_animator.gameObject.activeInHierarchy)
                {
                    return;   // out of view: nothing to correct
                }
                _weight = Mathf.MoveTowards(_weight, 1f, Time.deltaTime * 3f);
                // The right hand at the back of the nozzle by the hip, the left further along it.
                TwoBoneReach.Solve(_rightUpper, _rightLower, _rightHand, _nozzle.TransformPoint(_right), _weight);
                TwoBoneReach.Solve(_leftUpper, _leftLower, _leftHand, _nozzle.TransformPoint(_left), _weight);
            }
            catch (Exception e)
            {
                EventDirector.Logger?.Error(e);
                End();
            }
        }
    }

    /// <summary>
    /// What a fire costs the player when it is one of their own businesses that burned, through
    /// the game's own mechanisms only: the business is closed for some hours with the game's
    /// "temporarily closed" switch (the one the player has on the business screen) and reopened
    /// by the same switch when the time is up; and a repair bill is charged as an ordinary
    /// transaction. Both are scaled by how quickly water was on the fire, and reduced by the
    /// business's security level, by warning the occupants and by calling 911. Stock is never
    /// touched. Nothing is charged to a save's only business in its first ten days.
    /// </summary>
    internal static class FireDamage
    {
        internal const float RepairLeast = 350f, RepairMost = 1800f;   // a fire's repair bill, at the start's prices
        private const int GraceDays = 10;

        /// <summary>The business closed by a fire, as "street|number", and the game minute it reopens; null when there is none. Kept in modData.</summary>
        internal static string ClosedKey;
        internal static float ReopenAt;

        public static void Apply(BuildingRegistration business, float secondsToWater, bool warned, bool called)
        {
            try
            {
                var save = SaveGameManager.Current;
                if (save == null || business == null)
                {
                    return;
                }
                List<BuildingRegistration> own = BuildingHelper.GetPlayerBuildingRegistrations();
                int businesses = 0;
                for (int i = 0; own != null && i < own.Count; i++)
                {
                    businesses += own[i] != null && own[i].HasEstablishedBusiness ? 1 : 0;
                }
                if (businesses <= 1 && TimeHelper.CurrentDay <= GraceDays)
                {
                    Notifications.Show(NotificationType.Info, "reallife_fire_own_spared", null, 10f);
                    EventDirector.Logger?.Info("Fire: the player's only business, in the save's first days: no closure and no bill.");
                    return;
                }
                // 30 s to water is as good as it gets, two minutes as bad.
                float slow = Mathf.InverseLerp(30f, 120f, secondsToWater);
                float security = business.securityLevelPercentage > 1f ? business.securityLevelPercentage / 100f : business.securityLevelPercentage;
                float relief = 1f - Mathf.Clamp01(security) * 0.4f - (warned ? 0.15f : 0f) - (called ? 0.1f : 0f);
                int hours = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(3f, 10f, slow) * relief), 2, 10);
                int bill = Mathf.RoundToInt(Prices.At(Mathf.Lerp(RepairLeast, RepairMost, slow) * relief) / 10f) * 10;   // (at today's prices)
                GameManager.ChangeMoneySafe(-bill, new TransactionInfo("reallife_transaction_fire_repair"), null, business.Address, force: true);
                FireLosses.Raise(business, bill);   // (for whoever insures it: Events/FireLosses.cs)
                bool closedByUs = false;
                if (!business.temporarilyClosed && ClosedKey == null)
                {
                    business.TemporarilyClose(true);
                    ClosedKey = business.StreetName + "|" + business.StreetNumber;
                    ReopenAt = TimeHelper.NowInMinutes() + hours * 60f;
                    closedByUs = true;
                }
                var data = new Dictionary<string, string>(2) { { "amount", ((float)bill).ToCurrencyFormat() }, { "hours", hours.ToString() } };
                Notifications.Show(NotificationType.Error, closedByUs ? "reallife_fire_own_bill" : "reallife_fire_own_bill_only", data, 14f);
                EventDirector.Logger?.Info($"Fire: the player's business at {business.StreetName} {business.StreetNumber}: water after {secondsToWater:F0} s, security {security:P0}, warned {warned}, "
                                           + $"called {called}: repair bill {bill}, " + (closedByUs ? $"closed for {hours} h (the game's own temporary closure)." : "not closed (it already was, or another closure is running)."));
            }
            catch (Exception e)
            {
                EventDirector.Logger?.Error(e);
            }
        }

        /// <summary>Once a game hour: reopens the business a fire closed when its time is up.</summary>
        public static void Hourly()
        {
            if (ClosedKey == null || TimeHelper.NowInMinutes() < ReopenAt)
            {
                return;
            }
            string key = ClosedKey;
            ClosedKey = null;
            ReopenAt = 0f;
            try
            {
                List<BuildingRegistration> own = BuildingHelper.GetPlayerBuildingRegistrations();
                for (int i = 0; own != null && i < own.Count; i++)
                {
                    BuildingRegistration business = own[i];
                    if (business != null && business.StreetName + "|" + business.StreetNumber == key)
                    {
                        if (business.temporarilyClosed)
                        {
                            business.TemporarilyClose(false);
                            Notifications.Show(NotificationType.Success, "reallife_fire_own_reopened", null, 9f);
                        }
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                EventDirector.Logger?.Error(e);
            }
        }
    }
}
