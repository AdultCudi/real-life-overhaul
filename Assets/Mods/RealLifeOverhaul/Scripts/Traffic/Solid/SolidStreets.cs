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
    /// Traffic sees people in the road. Eight times a second, out of doors with a car near, the people within 90 m go into a
    /// grid (SolidRules.PeopleGrid) and each car the traffic system drives looks along its strip: whoever is in the road
    /// ahead gets a stand-in the cars' own sensors read (<see cref="SolidGame"/>), and the nearest car is braked for them in
    /// good time (<see cref="SolidBrakes"/>); drivers hoot and people answer. Nobody is run down unless one is owed
    /// (RecklessDrivers.PersonOwed) and the car truly cannot stop. Nothing is changed on a car; all is given back at once
    /// (ReleaseAll). Cost: idle, one time comparison a frame; active, about 70 microseconds a look for 40 cars and 60 people.
    /// </summary>
    public sealed partial class SolidStreets : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("streets", "SolidStreets", typeof(SolidStreets));

        private const byte Walker = 0, Figure = 1, Actor = 2, Body = 3;
        private const float ReportEvery = 60f;       // seconds
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
        private const int Phases = 4;   // the cars gathered, the people gathered, the strips and the decisions, the stand-ins and the fades
        private readonly long[] _split = new long[Phases];        // this look's time by phase
        private readonly long[] _splitTotal = new long[Phases];   // ...the minute's
        private readonly long[] _splitWorst = new long[Phases];   // ...the worst look's
        private long _splitAt;
        private bool _timing;   // this look is being measured
        private int _carsMost, _peopleMost, _proxiesMost, _brakedMost;

        private void Awake()
        {
            Instance = this;
            _brakes = gameObject.AddComponent<SolidBrakes>();
            ScenePrewarm.Add("streets: the road map and the city's marked crossings being listed for the traffic", Prewarm);
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

        private static readonly int HotUpdate = HotSpots.Slot("Traffic: SolidStreets.Update");
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
            if (now >= _nextEnsure)
            {
                // Whether the road and crossing grids are still this city's: a flag test, as both are made in Prewarm. Not timed.
                _nextEnsure = now + EnsureEvery;
                RoadMap.Ensure();
                Crossings.Ensure();
            }
            bool timed = Meta.Dev.Verbose;
            _timing = timed;
            if (timed)
            {
                Array.Clear(_split, 0, Phases);
                _splitAt = 0L;
                _clock.Restart();
            }
            bool busy = Tick(now);
            _next = now + (busy ? SolidRules.TickSeconds : SolidRules.IdleSeconds);
            if (timed)
            {
                _clock.Stop();
                _timing = false;
            }
            if (timed && busy)
            {
                long took = _clock.ElapsedTicks;
                _ticks++;
                _tickTotal += took;
                for (int p = 0; p < Phases; p++)
                {
                    _splitTotal[p] += _split[p];
                }
                if (took > _tickWorst)
                {
                    _tickWorst = took;
                    Array.Copy(_split, _splitWorst, Phases);
                }
                if (now >= _nextReport)
                {
                    Report(now);
                }
            }
        }

        // The diagnostics' own lines stop the look's clock (Quiet, Loud), so a look costs what it costs with them off.
        private void Quiet()
        {
            if (_timing)
            {
                _clock.Stop();
            }
        }

        private void Loud()
        {
            if (_timing)
            {
                _clock.Start();
            }
        }

        // The look's time so far goes to one of its phases (with developer diagnostics on): the minute's line says where it went.
        private void Phase(int phase)
        {
            if (_timing)
            {
                long at = _clock.ElapsedTicks;
                _split[phase] += at - _splitAt;
                _splitAt = at;
            }
        }

        /// <summary>Behind the loading screen (ScenePrewarm): the road map and the marked crossings (a 90 ms search). False while there is no road data yet.</summary>
        public static bool Prewarm()
        {
            if (RoadData.Scene == null)
            {
                return false;
            }
            RoadMap.Ensure();
            return Crossings.Prewarm();
        }

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
            Phase(0);
            if (cars == 0)
            {
                if (_active)
                {
                    ReleaseAll("no traffic near");
                }
                return false;
            }
            _active = true;
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
            Phase(1);
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
            Phase(2);
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

            Phase(3);
            _carsMost = Mathf.Max(_carsMost, cars);
            _peopleMost = Mathf.Max(_peopleMost, _grid.Count);
            _proxiesMost = Mathf.Max(_proxiesMost, _pool.InUse);
            _brakedMost = Mathf.Max(_brakedMost, _brakes.Count);
            return true;
        }

        /// <summary>For the next 30 s this one car may reach this one pedestrian, sent out too late for it, if it can no longer stop.</summary>
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

        /// <summary>Everything of this feature off the street at once: stand-ins away (the traffic told), no car braked, fades the game's again.</summary>
        public void ReleaseAll(string why)
        {
            try
            {
                int standIns = _pool.InUse;
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
                if (was && (standIns > 0 || braked > 0 || unfaded > 0))
                {
                    Quiet();
                    Logger?.Info($"Traffic sees people: everything given back at once ({why}): {standIns} stand-in(s) taken away, {braked} car(s) no longer braked, {unfaded} fade(s) put back.");
                    Loud();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _active = false;
            }
        }

        // Once a minute, with developer diagnostics on.

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
                         + (average < 100.0 && worst < 300.0 ? "PASS" : "over budget")
                         + $"; where it went, on average and in the worst look (microseconds): the cars gathered {_splitTotal[0] * micro / _ticks:F0} and {_splitWorst[0] * micro:F0}, "
                         + $"the people {_splitTotal[1] * micro / _ticks:F0} and {_splitWorst[1] * micro:F0}, "
                         + $"the strips and the decisions {_splitTotal[2] * micro / _ticks:F0} and {_splitWorst[2] * micro:F0}, "
                         + $"the stand-ins and the fades {_splitTotal[3] * micro / _ticks:F0} and {_splitWorst[3] * micro:F0}; the diagnostics' own lines are not counted.");
            _ticks = 0;
            _tickTotal = 0;
            _tickWorst = 0;
            Array.Clear(_splitTotal, 0, Phases);
            Array.Clear(_splitWorst, 0, Phases);
            _carsMost = _peopleMost = _proxiesMost = _brakedMost = 0;
        }
    }
}
