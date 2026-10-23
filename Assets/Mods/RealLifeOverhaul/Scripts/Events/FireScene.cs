using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>What burns: a car in traffic, a bin on the pavement, or a shopfront.</summary>
    public enum FireKind { Car, Bin, Building }

    /// <summary>
    /// One fire, from the first smoke to the engine driving away: a car that pulls up smoking, a bin, or a shopfront (nothing of
    /// the building changed). The road beside it is closed and an engine's crew of three knock it down in stages; a patrol car
    /// works the traffic, bystanders film, a 911 call sends the engine at once, and with no engine it burns out. Four times a
    /// second; every way out ends in <see cref="End"/>.
    /// </summary>
    internal sealed partial class FireScene : IEventScene
    {
        private enum Phase { Smoulder, Burning, Attack, Overhaul, PackUp, Leave, BurnOut, Done }

        private const float DispatchAfter = 20f;     // seconds from the first flames to the engine being sent, without a call
        private const float EngineBack = 12f;        // metres the engine stands short of the fire, in its lane
        private const float HardLimit = 250f;        // seconds: whatever state it is in, it is cleared by then
        private const int Bystanders = 3;            // people at most
        private const float CoilOut = 0.8f;          // metres off the engine's side the spare hose lies
        private const float CoilAhead = -2.7f;       // metres ahead of the modelled body's middle: behind the rear axle, clear of the crew and the hose laid
        private const float CoilTurn = 30f;          // degrees off the engine's heading, as the props' fire picture lays it

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
        private Vector3 _panel;         // the pump panel the operator works at; default on a repainted truck
        private Vector3 _door;          // a building's door: where occupants are warned
        private bool _hasDoor;
        private BuildingRegistration _business;   // the player's own, if that is what burns
        private GameObject _hazards;
        private Transform _nozzle;
        private Transform _coil;        // the spare hose lying by the parked engine
        private bool _test, _called, _warned, _engineSent, _engineRetried, _engineParked, _patrolSent, _patrolParked, _closed, _wreckStopped, _waterOn, _told;
        private float _startedAt, _phaseAt, _phaseUntil, _engineAt, _engineDue, _nextSecond, _waterAt, _patrolAt, _leaveAt;
        private int _stage;
        private float _flameSize;
        private string _refusal;

        public FireScene(EventDirector director)
        {
            _director = director;
        }

        public string Label => _kind == FireKind.Car ? "reallife_fire_label_car" : _kind == FireKind.Bin ? "reallife_fire_label_bin" : "reallife_fire_label_building";
        public Vector3 Focus => _seat;
        public float Reach => 240f;
        public string Refusal => _refusal;
        public TestRefusal Why { get; private set; }
        public int WhyMetres { get; private set; }

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
                                       + (kind == FireKind.Building
                                           ? (_hasDoor ? ", a door found to warn the occupants at" : ", no door near: occupants are warned at the frontage")
                                             + (_hasUpper ? ", a window above" : ", no wall found above")
                                           : "")
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
            CityBuildingController[] buildings = GameDlc.Open(city);
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
                _business = registration != null && GoingPublic.Mine(registration) && registration.HasEstablishedBusiness ? registration : null;
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
                        Settle();
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
                            Settle();
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
            if (_hazards != null)
            {
                Object.Destroy(_hazards);   // left by a scene that ended on a fault: a flasher never goes by itself
            }
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
                    _driver.Work("radio_talk", shelf, 0.4f);   // on the phone about their car
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
                        // At the tape at the nearest, facing in: never inside it, where a crossing is pavement too.
                        if (CitySpots.PavementRing(_seat, 13f, 19f, false, out Vector3 line) && !_director.Cordon.Shuts(line) && CitySpots.ClearQuietly(line, 0.45f))
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
            ReturnCoil();
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
}
