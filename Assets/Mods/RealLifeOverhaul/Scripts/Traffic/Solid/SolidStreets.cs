using System;
using System.Collections.Generic;
using System.Diagnostics;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Traffic sees people in the road. The game's cars drive through its pedestrians: this director
    /// makes somebody in the road a real obstacle to every traffic car near the player, all the time.
    ///
    /// EIGHT TIMES A SECOND (Tick), and only out of doors with the option on and a car near:
    ///   1. the people within 90 m of the player (the game's walkers, its standing figures, the mod's own
    ///      people, bodies in the road) go into one grid (SolidRules.PeopleGrid);
    ///   2. each traffic car within 90 m that the traffic system is driving looks at the grid squares its
    ///      strip covers: its own width and half a metre each side, as far ahead as it takes to stop;
    ///   3. whoever is in a strip, and in the road (a pavement a strip happens to sweep does not count),
    ///      gets one of sixteen stand-ins the cars' own sensors read (SolidGame), so that the game itself
    ///      holds the car short of them, queues the traffic behind it and lets it go when they have gone;
    ///   4. the car nearest to reaching somebody is braked for them in good time (SolidBrakes), so that
    ///      it slows like a driver who has seen them and does not stop dead from speed at the last metre;
    ///   5. the driver sounds the horn if they stepped out close (a hard brake), and again every four
    ///      seconds if they stand in front of the stopped car for more than three; the people answer it
    ///      as they answer the player's (HornReactions.Sound), which is what clears the road;
    ///   6. somebody about to step out where the car could no longer stop waits at the kerb instead
    ///      (HornReactions.Wait), so a careful driver is never asked for the impossible;
    ///   7. a car that is within a second of somebody is watched for real contact (CarStrikes.Glance).
    ///
    /// NOBODY IS RUN DOWN UNLESS ONE IS OWED. A hit takes three things at once: the bank the reckless
    /// drivers draw on owes a whole pedestrian (RecklessDrivers.PersonOwed), a car finds somebody in its
    /// way whom it cannot stop for with all its tyres give, and no other car is already about to hit
    /// somebody. Then that one car gets no stand-in for that one person and only its tyres' braking, and
    /// reaches them at whatever speed is left: a real hit, PedestrianIncidents' from there, and the driver
    /// stops or runs (RecklessDrivers.Struck). In every other case the car stops: braked with whatever it
    /// takes, and held by the game's own sensor.
    ///
    /// NOTHING IS LEFT STANDING. Nothing is changed on a car: a stand-in that is taken away is reported
    /// to the traffic system as the game reports a pedestrian put away, and braking is a force that
    /// simply stops being applied. A car held for 12 seconds has the person moved on (they jog out of its
    /// way); at 16, somebody nobody can see is put back in the game's pool, which sends them out
    /// somewhere else as it does at the end of any walk. A car at a red light, or one the traffic system
    /// holds at a stop line, is not counted as held by the people crossing in front of it. Everything is
    /// given back at once (ReleaseAll) when the player goes indoors, traffic stops, the option is switched
    /// off, a fault, or the mod closes.
    ///
    /// COST. Idle (indoors, the option off, no car within 90 m): one time comparison a frame, and twice
    /// a second a look for whether that has changed. Active: nothing per frame but that comparison; eight
    /// times a second a look that for 40 cars and 60 people comes to about 400 engine calls (four for a car
    /// that is out and near, up to eight for a walker), 1,200 array operations for the grid, and a few
    /// dozen multiplications for each person a car finds in its squares: some 70 microseconds in the
    /// game's runtime by that count. No physics query, nothing allocated, no search of the scene. At most
    /// three cars are measured for the first time in one look (about 40 microseconds each, once a session
    /// for each pooled car). Its average and worst are measured and logged once a minute with developer
    /// diagnostics on.
    /// </summary>
    public partial class SolidStreets : MonoBehaviour
    {
        private const byte Walker = 0, Figure = 1, Actor = 2, Body = 3;
        private const float ReportEvery = 60f;
        private const float GlanceFor = 0.6f;        // seconds a car stays watched after it was last within a second of somebody
        private const float StrikerFor = 3f;         // seconds a car that is about to hit somebody stays the one, unless it is named again
        private const float Level = 3f;              // metres up or down within which a person and a car are on the same road
        private const float EnsureEvery = 5f;        // seconds between two looks at whether the road and crossing grids are still this city's

        public static IModLogger Logger;
        public static SolidStreets Instance { get; private set; }

        /// <summary>True while traffic is being made to see people: the option is on and this has not switched itself off.</summary>
        public static bool Working => Instance != null && Instance._working && Settings.SolidStreets;

        private readonly SolidGame _game = new SolidGame();
        private readonly SolidRules.PeopleGrid _grid = new SolidRules.PeopleGrid(SolidRules.MostPeople);
        private readonly SolidRules.ProxyPool _pool = new SolidRules.ProxyPool(SolidRules.Proxies);
        private readonly Stopwatch _clock = new Stopwatch();
        private SolidBrakes _brakes;
        private PedestrianSpawner _spawner;

        // Everybody in the grid, by their number in it.
        private readonly byte[] _kind = new byte[SolidRules.MostPeople];
        private readonly int[] _source = new int[SolidRules.MostPeople];       // their place in the list they came from
        private readonly float[] _height = new float[SolidRules.MostPeople];
        private readonly float[] _vx = new float[SolidRules.MostPeople];
        private readonly float[] _vz = new float[SolidRules.MostPeople];
        private readonly byte[] _place = new byte[SolidRules.MostPeople];       // 0 not asked yet, 1 pavement, 2 in the road
        private readonly float[] _reachedIn = new float[SolidRules.MostPeople]; // seconds before the first car reaches them; MaxValue: in nobody's way
        private readonly int[] _nearestCar = new int[SolidRules.MostPeople];

        // The cars looked at this tick.
        private readonly VehicleComponent[] _car = new VehicleComponent[SolidRules.MostCars];
        private readonly int[] _index = new int[SolidRules.MostCars];           // its place in the traffic system's list
        private readonly float[] _cx = new float[SolidRules.MostCars];
        private readonly float[] _cy = new float[SolidRules.MostCars];
        private readonly float[] _cz = new float[SolidRules.MostCars];
        private readonly float[] _fx = new float[SolidRules.MostCars];
        private readonly float[] _fz = new float[SolidRules.MostCars];
        private readonly float[] _speed = new float[SolidRules.MostCars];
        private readonly int[] _ahead = new int[SolidRules.MostCars];           // the person it reaches first, or -1
        private readonly float[] _aheadBy = new float[SolidRules.MostCars];     // metres from its nose to them

        // What is remembered of each pooled car from tick to tick, by its place in the traffic system's list.
        private float[] _heldSince = new float[0];
        private float[] _lastHorn = new float[0];
        private byte[] _judged = new byte[0];
        private bool[] _movedOn = new bool[0];

        // One strip's finds.
        private readonly int[] _found = new int[48];
        private readonly float[] _foundAhead = new float[48];
        private readonly float[] _foundSide = new float[48];

        // The stand-ins: who has each, and what the sharing changed.
        private readonly bool[] _freed = new bool[SolidRules.Proxies];
        private readonly bool[] _given = new bool[SolidRules.Proxies];
        private readonly int[] _tags = new int[SolidRules.Proxies];

        private bool _working = true;
        private bool _active;
        private int _faults;
        private float _next;
        private float _nextSenses;
        private float _nextEnsure;
        private float _nextReport;
        private float _nextProxyLine;
        private int _proxiesSaid = -1;
        private bool _owedAsked;
        private bool _owed;

        // The one car that is about to hit somebody, if any.
        private VehicleComponent _striker;
        private int _strikerId;          // the person's number from look to look (Human)
        private float _strikerUntil;
        private Pedestrian _putAway;     // somebody to go back to the game's pool when this look is over (its list is not changed under the look)

        // A hit that somebody else of the mod has arranged (somebody stepping out too late): this car may reach this pedestrian.
        private VehicleComponent _oweCar;
        private Pedestrian _oweWalker;
        private float _oweUntil;

        // The minute's figures.
        private int _ticks;
        private long _tickTotal;
        private long _tickWorst;
        private int _carsMost, _peopleMost, _proxiesMost, _brakedMost;

        private void Awake()
        {
            Instance = this;
            _brakes = gameObject.AddComponent<SolidBrakes>();
        }

        private void OnDestroy()
        {
            ReleaseAll("the mod is closing");
            _game.DestroyStandIns();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            float now = Time.time;
            if (now < _next)
            {
                return;
            }
            try
            {
                Step(now);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // Everything is given back at once, so a fault cannot repeat on the same car or person; after three the cars are the game's alone this session.
        private void Fault(Exception e)
        {
            Logger?.Error(e);
            ReleaseAll("an error");
            _next = Time.time + SolidRules.IdleSeconds;
            if (++_faults >= 3 && _working)
            {
                _working = false;
                Logger?.Warn("Traffic sees people: switched off for this session after repeated errors (see above); traffic behaves as the game has it.");
            }
        }

        private void Step(float now)
        {
            if (!_working || !Settings.SolidStreets || !ModState.Loaded || !TrafficTools.Ready)
            {
                // Switched off, indoors, a menu, fast travel or sleep: the game has taken its traffic and its people back.
                if (_active)
                {
                    ReleaseAll("the option is off or the street is paused");
                }
                _next = now + SolidRules.IdleSeconds;
                return;
            }
            bool timed = Meta.Dev.Verbose;
            if (timed)
            {
                _clock.Restart();
            }
            bool busy = Tick(now);
            _next = now + (busy ? SolidRules.TickSeconds : SolidRules.IdleSeconds);
            if (timed && busy)
            {
                long took = _clock.ElapsedTicks;
                _ticks++;
                _tickTotal += took;
                _tickWorst = took > _tickWorst ? took : _tickWorst;
                if (now >= _nextReport)
                {
                    Report(now);
                }
            }
        }

        // ---------------------------------------------------------------- one look at the street

        // False if there was nothing to look at (no car near): the next look is half a second off.
        private bool Tick(float now)
        {
            TrafficManager traffic = TrafficManager.Instance;
            List<VehicleComponent> pooled = Manager.GetVehicleList();
            if (_game.Pool(traffic, pooled.Count) || _heldSince.Length < pooled.Count)
            {
                // Another city's traffic: what was remembered of each car is forgotten, and the senses are read again.
                _heldSince = new float[pooled.Count];
                _lastHorn = new float[pooled.Count];
                _judged = new byte[pooled.Count];
                _movedOn = new bool[pooled.Count];
                _nextSenses = 0f;
            }
            Vector3 player = PlayerHelper.GetPosition();
            int cars = GatherCars(player, traffic, pooled);
            if (cars == 0)
            {
                if (_active)
                {
                    ReleaseAll("no traffic near");
                }
                return false;
            }
            _active = true;
            if (now >= _nextEnsure)
            {
                _nextEnsure = now + EnsureEvery;
                RoadMap.Ensure();     // the grids "in the road or on the pavement" is asked of: built once for a city, a flag test after that
                Crossings.Ensure();
            }
            if (_spawner == null)
            {
                var city = InstanceBehavior<CityManager>.Instance;
                _spawner = city != null ? city.pedestrianSpawner : null;
            }
            List<Pedestrian> walkers = _spawner != null ? _spawner.activePedestrians : null;
            if (_game.How == SolidGame.Mode.Unknown && now >= _nextSenses)
            {
                _nextSenses = now + 1f;
                _game.Read(pooled, walkers);
            }
            GatherPeople(player, walkers);
            _owedAsked = false;
            if ((object)_striker != null && (now > _strikerUntil || _striker == null || !_striker.gameObject.activeInHierarchy))
            {
                _striker = null;
            }
            if ((object)_oweCar != null && (now > _oweUntil || _oweCar == null || _oweWalker == null))
            {
                _oweCar = null;
                _oweWalker = null;
            }

            Look(cars, walkers, now);
            Decide(cars, walkers, now);
            Share(walkers, now);
            _game.TickFade(now);
            if ((object)_putAway != null)
            {
                Pedestrian away = _putAway;
                _putAway = null;
                if (away != null && away.isActiveAndEnabled && InstanceBehavior<CityManager>.IsInitialized)
                {
                    HornReactions.LetGo(away);
                    away.Release();
                }
            }

            _carsMost = Mathf.Max(_carsMost, cars);
            _peopleMost = Mathf.Max(_peopleMost, _grid.Count);
            _proxiesMost = Mathf.Max(_proxiesMost, _pool.InUse);
            _brakedMost = Mathf.Max(_brakedMost, _brakes.Count);
            return true;
        }

        // The traffic cars within 90 m that the traffic system itself is driving. Four engine calls for each car out.
        private int GatherCars(Vector3 player, TrafficManager traffic, List<VehicleComponent> pooled)
        {
            int count = 0;
            _game.BeginLook();
            for (int i = 0; i < pooled.Count && count < SolidRules.MostCars; i++)
            {
                VehicleComponent car = pooled[i];
                if (car == null || !car.isActiveAndEnabled || car.rb == null)
                {
                    continue;
                }
                int index = car.GetIndex();
                // (A car the mod drives itself, a response unit or the reckless one, has a driver with eyes of its own.)
                if (index < 0 || index >= _heldSince.Length || traffic.IsVehicleIgnored(index))
                {
                    continue;
                }
                Transform body = _game.At(index, car);
                if (body == null)
                {
                    continue;   // not measured yet, and enough have been at this look
                }
                Vector3 at = body.position;
                float dx = at.x - player.x;
                float dz = at.z - player.z;
                if (dx * dx + dz * dz > SolidRules.Near * SolidRules.Near)
                {
                    continue;
                }
                Vector3 forward = body.forward;
                float flat = Mathf.Sqrt(forward.x * forward.x + forward.z * forward.z);
                if (flat < 0.5f)
                {
                    continue;
                }
                Vector3 velocity = car.rb.velocity;
                _car[count] = car;
                _index[count] = index;
                _cx[count] = at.x;
                _cy[count] = at.y;
                _cz[count] = at.z;
                _fx[count] = forward.x / flat;
                _fz[count] = forward.z / flat;
                _speed[count] = Mathf.Max(0f, (velocity.x * forward.x + velocity.z * forward.z) / flat);
                count++;
            }
            return count;
        }

        // Everybody within 90 m of the player into the grid: positions and, for those who walk, how they are moving.
        private void GatherPeople(Vector3 player, List<Pedestrian> walkers)
        {
            _grid.Begin(player.x, player.z);
            const float near = SolidRules.Near * SolidRules.Near;
            for (int i = walkers != null ? walkers.Count - 1 : -1; i >= 0; i--)
            {
                Pedestrian walker = walkers[i];
                // Switched off: gone, or in somebody's hands (a casualty). The exception is somebody the mod has sent into the road.
                if (walker == null || walker.tpc == null || (!walker.enabled && !Jaywalker.Holds(walker)))
                {
                    continue;
                }
                Vector3 at = walker.transform.position;
                float dx = at.x - player.x;
                float dz = at.z - player.z;
                if (dx * dx + dz * dz > near)
                {
                    continue;
                }
                int person = _grid.Add(at.x, at.z);
                if (person < 0)
                {
                    continue;
                }
                Note(person, Walker, i, at.y);
                var agent = walker.tpc.navmeshAgent;
                if (agent != null && agent.isActiveAndEnabled)
                {
                    Vector3 going = agent.velocity;
                    _vx[person] = going.x;
                    _vz[person] = going.z;
                }
            }
            List<BaseHuman> standing = StreetPeople.Standing(Logger);
            for (int i = standing.Count - 1; i >= 0; i--)
            {
                BaseHuman figure = standing[i];
                if (figure == null || !figure.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Vector3 at = figure.transform.position;
                float dx = at.x - player.x;
                float dz = at.z - player.z;
                if (dx * dx + dz * dz <= near)
                {
                    int person = _grid.Add(at.x, at.z);
                    if (person >= 0)
                    {
                        Note(person, Figure, i, at.y);
                    }
                }
            }
            ActorPool actors = ActorPool.Instance;
            for (int i = actors != null ? actors.ActorCount - 1 : -1; i >= 0; i--)
            {
                StreetActor actor = actors.ActorAt(i);
                if (!actor.InUse || !actor.IsValid || actor.Limp)
                {
                    continue;   // (somebody lying in the road is in the list of bodies below)
                }
                Vector3 at = actor.Position;
                float dx = at.x - player.x;
                float dz = at.z - player.z;
                if (dx * dx + dz * dz <= near)
                {
                    int person = _grid.Add(at.x, at.z);
                    if (person >= 0)
                    {
                        Note(person, Actor, i, at.y);
                    }
                }
            }
            RagdollSystem ragdolls = RagdollSystem.Instance;
            for (int i = 0; ragdolls != null && ragdolls.enabled && i < RagdollSystem.MaxBodies; i++)
            {
                if (ragdolls.TryGetBody(i, out Vector3 at))
                {
                    float dx = at.x - player.x;
                    float dz = at.z - player.z;
                    if (dx * dx + dz * dz <= near)
                    {
                        int person = _grid.Add(at.x, at.z);
                        if (person >= 0)
                        {
                            Note(person, Body, i, at.y);
                            _place[person] = 2;   // wherever a body lies, a car must not drive over it
                        }
                    }
                }
            }
            _grid.Build();
        }

        private void Note(int person, byte kind, int source, float height)
        {
            _kind[person] = kind;
            _source[person] = source;
            _height[person] = height;
            _vx[person] = 0f;
            _vz[person] = 0f;
            _place[person] = 0;
            _reachedIn[person] = float.MaxValue;
            _nearestCar[person] = -1;
        }

        // True if somebody is in the road (the carriageway or a marked crossing) and not on a pavement: a grid lookup, asked
        // once a tick for each person some strip has found.
        private bool InRoad(int person)
        {
            if (_place[person] == 0)
            {
                var at = new Vector3(_grid.X(person), _height[person], _grid.Z(person));
                _place[person] = HornReactions.PlaceOf(at, out _) == StreetRules.Place.Pavement ? (byte)1 : (byte)2;
            }
            return _place[person] == 2;
        }

        // Each car's strip against the grid: whom it reaches first, and for everybody in a strip, which car reaches them first.
        private void Look(int cars, List<Pedestrian> walkers, float now)
        {
            for (int c = 0; c < cars; c++)
            {
                _ahead[c] = -1;
                _aheadBy[c] = float.MaxValue;
                int index = _index[c];
                float speed = _speed[c];
                float halfWidth = _game.HalfWidth(index);
                float halfStrip = SolidRules.HalfStrip(halfWidth);
                float axle = _game.Axle(index);
                float nose = _game.Nose(index);
                int found = _grid.Strip(_cx[c], _cz[c], _fx[c], _fz[c], axle - 0.5f, nose + SolidRules.PersonRadius + SolidRules.SightFor(speed, _game.Gap(index)),
                    halfStrip + SolidRules.StepBand, _found, _foundAhead, _foundSide);
                if (found == 0)
                {
                    continue;
                }
                for (int k = 0; k < found; k++)
                {
                    int person = _found[k];
                    float level = _height[person] - _cy[c];
                    if (level > Level || level < -Level)
                    {
                        continue;   // on a bridge over the road, or under it
                    }
                    float distance = _foundAhead[k] - nose - SolidRules.PersonRadius;
                    float side = _foundSide[k];
                    if (side > halfStrip || side < -halfStrip)
                    {
                        Stepper(c, person, distance, side, halfStrip, walkers, now);
                        continue;
                    }
                    // (Somebody lying in the road takes up more of it than their middle: the whole strip counts for them.)
                    bool moving = _kind[person] == Body || _vx[person] * _vx[person] + _vz[person] * _vz[person] > SolidRules.MovingFrom * SolidRules.MovingFrom;
                    if (distance < -1.5f || !SolidRules.InWay(side, halfWidth, moving) || !InRoad(person))
                    {
                        continue;
                    }
                    if (distance < _aheadBy[c])
                    {
                        _aheadBy[c] = distance;
                        _ahead[c] = person;
                    }
                    float reachedIn = (distance < 0f ? 0f : distance) / (speed < 1f ? 1f : speed);
                    if (reachedIn < _reachedIn[person])
                    {
                        _reachedIn[person] = reachedIn;
                        _nearestCar[person] = c;
                    }
                }
            }
        }

        // Somebody beside a strip, walking into it. If the car could no longer stop for them they wait at the kerb, unless
        // an accident is owed (then they step out, and what comes of it is decided when they are in the car's way).
        private void Stepper(int c, int person, float distance, float side, float halfStrip, List<Pedestrian> walkers, float now)
        {
            float speed = _speed[c];
            if (_kind[person] != Walker || distance < 0f || !SolidRules.MustWait(speed, distance))
            {
                return;
            }
            float sideSpeed = _vx[person] * _fz[c] - _vz[person] * _fx[c];
            if (!SolidRules.StepsIn(side, sideSpeed, halfStrip))
            {
                return;
            }
            Pedestrian walker = walkers[_source[person]];
            if (Jaywalker.Holds(walker) || StopsAtItsLine(c) || Owed())
            {
                return;
            }
            // (Where they would be in a second: on the carriageway, or only further along the pavement the strip sweeps?)
            var then = new Vector3(_grid.X(person) + _vx[person] * SolidRules.StepWithin, _height[person], _grid.Z(person) + _vz[person] * SolidRules.StepWithin);
            if (HornReactions.PlaceOf(then, out _) == StreetRules.Place.Pavement)
            {
                return;
            }
            HornReactions reactions = HornReactions.Instance;
            var car = new Vector3(_cx[c], _cy[c], _cz[c]);
            if (reactions != null && reactions.Wait(walker, car, false, SolidRules.WaitFor(speed, distance)) && Meta.Dev.Verbose)
            {
                Logger?.Info($"Traffic check (person ahead): '{TrafficTools.Name(_car[c])}' at {speed * 3.6f:F0} km/h, somebody about to step out {distance:F0} m ahead, "
                             + $"needs {SolidRules.SafeToStep(speed):F0} m: they wait at the kerb: PASS");
            }
        }

        // True if the car is coming up to a stop line it will stop at: the traffic system is bringing it to rest there, or
        // its own light is red. Nobody waits at the kerb for such a car, and it is not about to run anybody down.
        private bool StopsAtItsLine(int c)
        {
            return _car[c].GetCurrentAction() == SpecialDriveActionTypes.StopInPoint
                   || HornReactions.LightOf(new Vector3(_cx[c], _cy[c], _cz[c]), new Vector3(_fx[c], 0f, _fz[c])) == StreetRules.Light.Red;
        }

        // True if a pedestrian is owed to the street (asked of the reckless drivers' director once a tick at the most, and
        // only when some car has found somebody it cannot stop for).
        private bool Owed()
        {
            if (!_owedAsked)
            {
                _owedAsked = true;
                RecklessDrivers drivers = RecklessDrivers.Instance;
                _owed = (object)_striker == null && drivers != null && drivers.PersonOwed;
            }
            return _owed;
        }

        // What each car that has somebody ahead does about them: brake, sound the horn, wait, be watched.
        private void Decide(int cars, List<Pedestrian> walkers, float now)
        {
            _brakes.Begin();
            CarStrikes strikes = CarStrikes.Instance;
            HornReactions reactions = HornReactions.Instance;
            bool verbose = Meta.Dev.Verbose;
            for (int c = 0; c < cars; c++)
            {
                int index = _index[c];
                int person = _ahead[c];
                if (person < 0)
                {
                    _heldSince[index] = 0f;
                    _movedOn[index] = false;
                    _judged[index] = (byte)SolidRules.Driver.Clear;
                    continue;
                }
                VehicleComponent car = _car[c];
                float speed = _speed[c];
                float distance = _aheadBy[c];
                float gap = _game.Gap(index);
                float nose = _game.Nose(index);
                Pedestrian walker = _kind[person] == Walker ? walkers[_source[person]] : null;
                bool hears = _kind[person] == Walker || _kind[person] == Figure;   // nobody sounds a horn at a body in the road, or at a crew at work
                var judged = SolidRules.Judge(speed, distance, gap);
                var before = (SolidRules.Driver)_judged[index];
                _judged[index] = (byte)judged;
                var carAt = new Vector3(_cx[c], _cy[c], _cz[c]);
                var facing = new Vector3(_fx[c], 0f, _fz[c]);
                bool first = _nearestCar[person] == c;   // of the cars that have this person ahead, the one that reaches them first

                // Is this the one car that may reach somebody? Arranged (somebody sent out too late), or owed and too late to stop.
                bool strike = false;
                if ((object)_striker == car)
                {
                    Human(person, walkers, out _, out int who);
                    strike = who == _strikerId;
                }
                if (!strike && (object)_striker == null && judged == SolidRules.Driver.Late && first && speed >= SolidRules.StrikeFrom && !StopsAtItsLine(c))
                {
                    bool arranged = (object)_oweCar == car && walker != null && walker == _oweWalker;
                    if (arranged || (walker != null && walker.enabled && Owed()))
                    {
                        strike = true;
                        _owed = false;   // one at a time: nobody else is owed at this look
                        Logger?.Info($"Traffic check (struck): '{TrafficTools.Name(car)}' at {speed * 3.6f:F0} km/h could not stop: needed "
                                     + $"{SolidRules.StoppingDistance(speed, SolidRules.HardMost) + gap:F0} m, had {distance:F0}"
                                     + $" ({(arranged ? "somebody stepped out too late" : "a pedestrian is owed")}): it brakes with all its tyres give and reaches them at what is left.");
                    }
                }
                if (strike)
                {
                    BaseHuman human = Human(person, walkers, out _, out int who);
                    _striker = car;
                    _strikerId = who;
                    _strikerUntil = now + StrikerFor;
                    _reachedIn[person] = float.MaxValue;   // no stand-in for them while this car comes
                    _game.Unsee(car, walker);
                    _game.Unfade(human, now);
                }

                // Somebody a step into its way whom it can no longer stop for comfortably steps back, when nothing is owed.
                if (!strike && walker != null && walker.enabled && reactions != null && before == SolidRules.Driver.Clear && SolidRules.MustWait(speed, distance)
                    && !StopsAtItsLine(c))
                {
                    float sideSpeed = _vx[person] * _fz[c] - _vz[person] * _fx[c];
                    SolidRules.Local(_grid.X(person), _grid.Z(person), _cx[c], _cz[c], _fx[c], _fz[c], out _, out float side);
                    if (StreetRules.StepsBack(side, sideSpeed, _game.HalfWidth(index)))
                    {
                        reactions.Wait(walker, carAt, true, SolidRules.WaitFor(speed, distance));
                    }
                }

                // The brake: for whoever has begun, and whoever must.
                if (judged != SolidRules.Driver.Clear || _brakes.Was(car))
                {
                    _brakes.Add(car, _grid.X(person), _grid.Z(person), nose, gap, strike);
                }
                if (verbose && before == SolidRules.Driver.Clear && judged != SolidRules.Driver.Clear && !strike)
                {
                    float needs = SolidRules.StoppingDistance(speed, judged == SolidRules.Driver.Ease ? SolidRules.HardFrom : SolidRules.HardMost) + gap;
                    Logger?.Info($"Traffic check (person ahead): '{TrafficTools.Name(car)}' at {speed * 3.6f:F0} km/h, the person {distance:F0} m ahead, needs {needs:F0} m to stop: "
                                 + (judged == SolidRules.Driver.Late ? "braking with whatever it takes (nothing is owed: it must stop): PASS"
                                     : judged == SolidRules.Driver.Hard ? "braking hard: PASS" : "braking: PASS"));
                }

                // The horn at somebody who stepped out close. One car for one person: the one that reaches them first.
                if (first && hears && SolidRules.HornDue(judged, before, now - _lastHorn[index]))
                {
                    Horn(c, index, car, walker, carAt, facing, reactions, now,
                        verbose ? $"somebody stepped out {distance:F0} m ahead at {speed * 3.6f:F0} km/h: a hard brake" : null);
                }

                // Standing behind somebody. Not at a red light or a stop line the traffic system holds it at: there the people have the road.
                if (SolidRules.Held(speed, distance, gap) && car.GetCurrentAction() != SpecialDriveActionTypes.StopInPoint
                    && HornReactions.LightOf(carAt, facing) != StreetRules.Light.Red)
                {
                    if (_heldSince[index] <= 0f)
                    {
                        _heldSince[index] = now;
                    }
                    float heldFor = now - _heldSince[index];
                    if (first && hears && SolidRules.DawdleHorn(heldFor, now - _lastHorn[index]) && Dawdles(person))
                    {
                        Horn(c, index, car, walker, carAt, facing, reactions, now, verbose ? $"it has stood behind somebody for {heldFor:F0} s" : null);
                    }
                    if (SolidRules.MoveOn(heldFor) && !_movedOn[index] && walker != null && walker.enabled)
                    {
                        _movedOn[index] = true;
                        bool moved = reactions != null && reactions.MoveOn(walker, carAt);
                        Logger?.Info($"Traffic check (held too long): '{TrafficTools.Name(car)}' has stood behind somebody for {heldFor:F0} s (the most is {SolidRules.HoldMost:F0}): "
                                     + (moved ? "moved the person on: PASS" : "they could not be moved on; put elsewhere by the game once nobody can see them"));
                    }
                    else if (walker != null && walker.enabled && walker.isActiveAndEnabled
                             && SolidRules.Remove(heldFor, StreetSpots.IsInView(new Vector3(_grid.X(person), _height[person], _grid.Z(person)))))
                    {
                        // Out of view, and still there: back to the game's pool, which sends them out somewhere else as at the end
                        // of any walk. (Done when this look is over: the pool takes them out of the list the look is reading.)
                        _heldSince[index] = 0f;
                        _movedOn[index] = false;
                        _reachedIn[person] = float.MaxValue;
                        _putAway = walker;
                        Logger?.Info($"Traffic check (held too long): '{TrafficTools.Name(car)}' was still held after {heldFor:F0} s by somebody nobody can see: the game puts them elsewhere: PASS");
                    }
                }
                else
                {
                    _heldSince[index] = 0f;
                    _movedOn[index] = false;
                }

                if (strikes != null && Settings.Ragdolls && SolidRules.Watch(speed, distance))
                {
                    strikes.Glance(car, now + GlanceFor);
                }
            }
            _brakes.End();
        }

        // True if the person a car stands behind is one to sound a horn at: standing or hardly moving, or walking where
        // they have no business to (somebody crossing at a walk on a crossing whose light is theirs is simply waited for).
        private bool Dawdles(int person)
        {
            if (_vx[person] * _vx[person] + _vz[person] * _vz[person] < SolidRules.MovingFrom * SolidRules.MovingFrom)
            {
                return true;
            }
            var at = new Vector3(_grid.X(person), _height[person], _grid.Z(person));
            return !(HornReactions.PlaceOf(at, out bool theirs) == StreetRules.Place.Crossing && theirs);
        }

        // The car's own horn, and the people's answer to it.
        private void Horn(int c, int index, VehicleComponent car, Pedestrian walker, Vector3 carAt, Vector3 facing, HornReactions reactions, float now, string why)
        {
            _lastHorn[index] = now;
            bool sounded = _game.Hoot(index, car);
            if (walker != null && Jaywalker.Hooted(walker))
            {
                // (Somebody the mod stood in the lane: they walk on at the horn. Nobody else needs to answer it.)
            }
            else if (reactions != null)
            {
                reactions.Sound(carAt, facing, _game.HalfWidth(index));
            }
            if (why != null)
            {
                Logger?.Info($"Traffic check (horn): '{TrafficTools.Name(car)}': {why}: " + (sounded ? "its horn sounds, and the people answer it: PASS" : "it has no horn of the game's to sound"));
            }
        }

        // The stand-ins: everybody in some car's way who is not sensed as they are, the most pressing first.
        private void Share(List<Pedestrian> walkers, float now)
        {
            _pool.Begin();
            bool usable = _game.StandInsUsable;
            int people = _grid.Count;
            for (int person = 0; person < people; person++)
            {
                if (_reachedIn[person] == float.MaxValue)
                {
                    continue;
                }
                BaseHuman human = Human(person, walkers, out Pedestrian walker, out int id);
                _game.Unfade(human, now);
                if (usable && id != 0 && !_game.SensedAsIs(walker))
                {
                    _pool.Want(id, _reachedIn[person], person);
                }
            }
            _pool.Share(_freed, _given, _tags);
            for (int slot = 0; slot < _pool.Size; slot++)
            {
                if (_freed[slot])
                {
                    _game.Park(slot);   // (taken from one person: the cars are told before it stands on another)
                }
                if (_pool.Holder(slot) != 0)
                {
                    int person = _tags[slot];
                    if (!_game.Place(slot, new Vector3(_grid.X(person), _height[person], _grid.Z(person)), transform))
                    {
                        _pool.Clear(_freed);
                        break;
                    }
                }
            }
            if (Meta.Dev.Verbose && _pool.InUse != _proxiesSaid && now >= _nextProxyLine)
            {
                _proxiesSaid = _pool.InUse;
                _nextProxyLine = now + 1f;
                Logger?.Info($"Traffic check (proxy): {_pool.InUse} of {_pool.Size} in use: {(_pool.InUse <= _pool.Size ? "PASS" : "more than there are")}");
            }
        }

        // Who somebody in the grid is: the character whose fade is held off, the walker if they are one, and a number that
        // is theirs from look to look (never 0; a body's is its place among the ragdolls).
        private BaseHuman Human(int person, List<Pedestrian> walkers, out Pedestrian walker, out int id)
        {
            walker = null;
            id = 0;
            int source = _source[person];
            switch (_kind[person])
            {
                case Walker:
                    walker = source < walkers.Count ? walkers[source] : null;
                    if (walker == null || walker.tpc == null)
                    {
                        walker = null;
                        return null;
                    }
                    id = walker.tpc.GetInstanceID();
                    return walker.tpc;
                case Figure:
                    List<BaseHuman> standing = StreetPeople.Standing(Logger);
                    BaseHuman figure = source < standing.Count ? standing[source] : null;
                    id = figure != null ? figure.GetInstanceID() : 0;
                    return figure;
                case Actor:
                    ActorPool actors = ActorPool.Instance;
                    StreetActor actor = actors != null && source < actors.ActorCount ? actors.ActorAt(source) : null;
                    if (actor == null || !actor.IsValid)
                    {
                        return null;
                    }
                    id = actor.Character.GetInstanceID();
                    return actor.Character;
                default:
                    id = int.MinValue + 1 + source;
                    return null;
            }
        }

        // ---------------------------------------------------------------- arranged by somebody else of the mod

        /// <summary>
        /// Somebody has been sent into the road too late for this car (Traffic/Reckless, a pedestrian who is due
        /// to be hit, or a test): for the next half minute this one car may reach this one pedestrian if it finds
        /// it can no longer stop for them. Every other car still stops for them, and this one for everybody else.
        /// </summary>
        public static void Owe(VehicleComponent car, Pedestrian walker)
        {
            SolidStreets streets = Instance;
            if (streets != null && car != null && walker != null)
            {
                streets._oweCar = car;
                streets._oweWalker = walker;
                streets._oweUntil = Time.time + 30f;
            }
        }

        // ---------------------------------------------------------------- giving everything back

        /// <summary>
        /// Everything of this feature off the street at once: every stand-in away (and the traffic system told so),
        /// no car braked, everybody's fade the game's again, nothing remembered of any car. Safe at any time, and
        /// when nothing is out.
        /// </summary>
        public void ReleaseAll(string why)
        {
            try
            {
                int out_ = _pool.InUse;
                int braked = _brakes != null ? _brakes.Count : 0;
                int unfaded = _game.Unfaded;
                _pool.Clear(_freed);
                for (int slot = 0; slot < _freed.Length; slot++)
                {
                    if (_freed[slot])
                    {
                        _game.Park(slot);
                    }
                }
                if (_brakes != null)
                {
                    _brakes.Clear();
                }
                _game.RestoreFades();
                for (int i = 0; i < _heldSince.Length; i++)
                {
                    _heldSince[i] = 0f;
                    _movedOn[i] = false;
                    _judged[i] = 0;
                }
                _striker = null;
                _oweCar = null;
                _oweWalker = null;
                _putAway = null;
                _spawner = null;
                _proxiesSaid = -1;
                bool was = _active;
                _active = false;
                if (was && (out_ > 0 || braked > 0 || unfaded > 0))
                {
                    Logger?.Info($"Traffic sees people: everything given back at once ({why}): {out_} stand-in(s) taken away, {braked} car(s) no longer braked, {unfaded} fade(s) put back.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _active = false;
            }
        }

        // ---------------------------------------------------------------- once a minute, with developer diagnostics on

        private void Report(float now)
        {
            _nextReport = now + ReportEvery;
            if (_ticks == 0)
            {
                return;
            }
            double micro = 1000000.0 / Stopwatch.Frequency;
            double average = _tickTotal * micro / _ticks;
            double worst = _tickWorst * micro;
            Logger?.Info($"Traffic check (cost): {_ticks} looks in the last minute, {average:F0} microseconds on average and {worst:F0} at worst (wanted: under 100 and 300); "
                         + $"at most {_carsMost} cars looked at, {_peopleMost} people in the grid, {_proxiesMost} of {_pool.Size} stand-ins in use, {_brakedMost} cars braked, "
                         + $"{(CarStrikes.Instance != null ? CarStrikes.Instance.WatchedCount : 0)} watched for contact now; mode {_game.How}: "
                         + (average < 100.0 && worst < 300.0 ? "PASS" : "over budget"));
            _ticks = 0;
            _tickTotal = 0;
            _tickWorst = 0;
            _carsMost = _peopleMost = _proxiesMost = _brakedMost = 0;
        }
    }
}
