using System;
using System.Diagnostics;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's drivers drive with care. The traffic system (Gley) notices what is ahead only inside each car's 3 m sensor
    /// and then stops within 1.25 m, from any speed; it sees a red light one lane point ahead; it gives nobody way. Eight
    /// times a second, out of doors with traffic running and this machine's, each car it drives within 90 m of the player
    /// reads its road ahead (its lane points, the way its indicator says it turns) and what is on it: cars, the player's car
    /// and the game's parking room behind it, the player on foot, the mod's bus and tow truck, red lights, give-way lines,
    /// closed lanes, lower limits, sharp bends. A car that must slow is slowed in good time (<see cref="DriverBrakes"/>) to
    /// reach the game's own sensor or stop line at walking pace, so the game's stop is gentle. A car keeps its distance
    /// behind the player and room to park; a hold line lets the player out of a space or reverse into one. Nothing on a car
    /// or a lane point is changed; all is given back at once (<see cref="ReleaseAll"/>). Cost: idle, one comparison a frame;
    /// a look, its cars read 12 a frame over a few frames (DriverCheck times the rules: about 7 microseconds for 40 cars).
    /// </summary>
    public sealed partial class CityDrivers : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("streets", "CityDrivers", typeof(CityDrivers));

        private const float ReportEvery = 60f;       // seconds between two cost lines (developer diagnostics)
        private const float LookWanted = 150f;       // microseconds a look may take on average (the self-check's figure)...
        private const float LookWorst = 600f;        // ...and at worst

        public static IModLogger Logger;
        public static CityDrivers Instance { get; private set; }

        /// <summary>True while the city's cars are driven with care: the option is on and this has not switched itself off.</summary>
        public static bool Working => Instance != null && Instance._working && Settings.CityDrivers;

        private readonly DriverGame _game = new DriverGame();
        private readonly DriverRules.Road _road = new DriverRules.Road(DriverRules.PathPoints + 2);
        private readonly DriverRules.Obstacles _obstacles = new DriverRules.Obstacles(DriverRules.MostObstacles);
        private readonly Stopwatch _clock = new Stopwatch();
        private DriverBrakes _brakes;

        private bool _working = true;
        private bool _active;        // something of this feature may be on the street (a car named, a line up)
        private int _faults;
        private float _next;
        private bool _evaluating;    // a look is under way: its cars are being read, a slice a frame
        private int _cursor;         // the next of the look's cars to read
        private float _lookAt;       // when the look began

        // The minute's figures, with developer diagnostics on.
        private int _looks;
        private long _lookTicks;     // this look's time so far
        private long _lookTotal;
        private long _lookWorst;
        private float _nextReport;
        private int _carsMost, _obstaclesMost, _brakedMost, _linesMost;

        private void Awake()
        {
            Instance = this;
            _brakes = gameObject.AddComponent<DriverBrakes>();
        }

        private void OnDisable()
        {
            ReleaseAll("its updates stopped");
        }

        private void OnDestroy()
        {
            ReleaseAll("the mod is closing");
            _game.DestroyLines();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Traffic: CityDrivers.Update");
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
            if (!_evaluating && now < _next)
            {
                return;
            }
            try
            {
                if (_evaluating)
                {
                    Slice(now);
                }
                else
                {
                    Step(now);
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // Everything is given back at once, so a fault cannot repeat on the same car; after three the cars are the game's alone this session.
        private void Fault(Exception e)
        {
            Logger?.Error(e);
            ReleaseAll("an error");
            _next = Time.time + DriverRules.IdleSeconds;
            if (++_faults >= 3 && _working)
            {
                _working = false;
                Logger?.Warn("City drivers: switched off for this session after repeated errors (see above); the city's cars drive as the game has them.");
            }
        }

        // A look begins: the street read once, then its cars a slice a frame.
        private void Step(float now)
        {
            if (!_working || !Settings.CityDrivers || !ModState.Loaded || !TrafficTools.Ready)
            {
                // Switched off, indoors, a menu, fast travel, sleep or a Going Public guest: the cars are the game's alone.
                if (_active)
                {
                    ReleaseAll(Settings.CityDrivers ? "the street is paused or not this machine's" : "the option is off");
                }
                _next = now + DriverRules.IdleSeconds;
                return;
            }
            bool timed = Meta.Dev.Verbose;
            if (timed)
            {
                _lookTicks = 0L;
                _clock.Restart();
            }
            int cars = BeginLook(now);
            if (timed)
            {
                _clock.Stop();
                _lookTicks += _clock.ElapsedTicks;
            }
            if (cars == 0)
            {
                EndLook(now, cars);
                _next = now + (_active ? DriverRules.TickSeconds : DriverRules.IdleSeconds);
                return;
            }
            _active = true;
            _evaluating = true;
            _cursor = 0;
            _lookAt = now;
            Slice(now);
        }

        // Some of the look's cars read; at the last, the look's decisions about the hold lines.
        private void Slice(float now)
        {
            bool timed = Meta.Dev.Verbose;
            if (timed)
            {
                _clock.Restart();
            }
            int end = Mathf.Min(_cars, _cursor + DriverRules.SliceCars);
            for (; _cursor < end; _cursor++)
            {
                Evaluate(_cursor, now);
            }
            bool done = _cursor >= _cars;
            if (done)
            {
                EndLook(now, _cars);
            }
            if (timed)
            {
                _clock.Stop();
                _lookTicks += _clock.ElapsedTicks;
                if (done)
                {
                    Count(now);
                }
            }
            if (done)
            {
                _evaluating = false;
                _next = _lookAt + DriverRules.TickSeconds;
            }
        }

        private void Count(float now)
        {
            _looks++;
            _lookTotal += _lookTicks;
            if (_lookTicks > _lookWorst)
            {
                _lookWorst = _lookTicks;
            }
            _carsMost = Mathf.Max(_carsMost, _cars);
            _obstaclesMost = Mathf.Max(_obstaclesMost, _obstacles.Count);
            _brakedMost = Mathf.Max(_brakedMost, _brakes != null ? _brakes.Count : 0);
            _linesMost = Mathf.Max(_linesMost, LinesUp);
            if (now >= _nextReport)
            {
                Report(now);
            }
        }

        /// <summary>
        /// Everything of this feature off the street at once: no car braked, every hold line taken away (the traffic told, so
        /// the cars held for it drive on), what was remembered of the cars forgotten. Safe at any time.
        /// </summary>
        public void ReleaseAll(string why)
        {
            try
            {
                int braked = _brakes != null ? _brakes.Count : 0;
                int lines = LinesUp;
                if (_brakes != null)
                {
                    _brakes.Clear();
                }
                DropLines(why);
                ForgetCars();
                _evaluating = false;
                _cursor = 0;
                bool was = _active;
                _active = false;
                if (was && (braked > 0 || lines > 0))
                {
                    Logger?.Info($"City drivers: everything given back at once ({why}): {braked} car(s) no longer slowed, {lines} hold line(s) taken away.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _active = false;
                _evaluating = false;
            }
        }

        // Once a minute, with developer diagnostics on.
        private void Report(float now)
        {
            _nextReport = now + ReportEvery;
            if (_looks == 0)
            {
                return;
            }
            double micro = 1000000.0 / Stopwatch.Frequency;
            double average = _lookTotal * micro / _looks;
            double worst = _lookWorst * micro;
            bool pass = average < LookWanted && worst < LookWorst;
            Logger?.Info($"Traffic check (city drivers' cost): {_looks} looks in the last minute, {average:F0} microseconds a look on average and {worst:F0} at worst, "
                         + $"over {DriverRules.SliceCars} cars a frame (wanted: under {LookWanted:F0} and {LookWorst:F0}); at most {_carsMost} cars read, "
                         + $"{_obstaclesMost} things on the roads, {_brakedMost} cars slowed at once, {_linesMost} hold line(s): "
                         + (pass ? "PASS" : "FAIL, over budget"));
            _looks = 0;
            _lookTotal = 0L;
            _lookWorst = 0L;
            _carsMost = _obstaclesMost = _brakedMost = _linesMost = 0;
        }
    }
}
