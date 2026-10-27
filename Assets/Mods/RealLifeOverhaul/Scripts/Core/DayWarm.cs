using System;
using System.Diagnostics;
using System.Globalization;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's code for the game's midnight compiled ahead, a slice a frame, a little while after a city loads, so the
    /// first midnight of a session does not pay Mono's compiler for every feature's day in the frame the game turns the
    /// day (Core/CodeWarm has the why and the figures). The roots are the handlers on the game's hour and day events, which
    /// the game's NewDay calls in that one frame, and the mod's own day events the economy shares out (<see cref="FeatureDays"/>,
    /// from Wiring). Compiled code lasts the whole run, so the walk is kept across loads and a later load only compiles a
    /// handler not met before. Nothing the day does changes, nor its order or its numbers: only when its code is compiled.
    /// COST: per frame while it runs (some seconds after each load), a slice of <see cref="SliceMs"/> ms of reading and
    /// compiling; before that, one time comparison; disabled once done.
    /// </summary>
    public sealed class DayWarm : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "DayWarm", typeof(DayWarm));   // its Update (Core/Guard)

        // Seconds of play after the loading screen: every feature has put its handlers on the game's events by then
        // (the economy waits a few seconds for the legal record).
        internal const float StartAfter = 20f;
        public const double SliceMs = 1.0;       // milliseconds of reading and compiling a frame may take
        private const double WorstMs = 20.0;     // milliseconds a frame may reach with one long method (the longest offline: 13 ms)
        private const int MostMethods = 30000;   // methods taken in at most
        // Calls from a handler that are read further: the day's own work lies within a few calls of its handler, and the
        // reach grows by some 1,500 methods a call past it (7,900 at six, 21,000 unbounded, October 2026).
        private const int Deepest = 6;

        public static IModLogger Logger;

        /// <summary>The mod's own day and hour events shared out to features (the economy's AfterDay and AfterHour), from Wiring.</summary>
        public static Func<Delegate> FeatureDays;

        private static readonly int HotUpdate = HotSpots.Slot("Core: DayWarm.Update");
        private static CodeWarm _warm;          // kept across loads: compiled code is not lost with a city
        private static DayWarm _instance;

        private float _startAt = -1f;           // unscaled time the walk starts at; set at the first frame after the loading screen
        private bool _rooted;
        private double _spentMs, _worstMs;
        private int _frames;

        /// <summary>True once everything met so far is compiled.</summary>
        public static bool Ready => _warm != null && _warm.Done && _instance != null && _instance._rooted;

        private void Awake()
        {
            _instance = this;
        }

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
            if (!_rooted)
            {
                if (UI.Load.LoadScene.isLoading)
                {
                    _startAt = -1f;
                    return;
                }
                if (_startAt < 0f)
                {
                    _startAt = Time.unscaledTime + StartAfter;
                }
                if (Time.unscaledTime < _startAt)
                {
                    return;
                }
                Root();
            }
            long began = Stopwatch.GetTimestamp();
            WorkSlice slice = WorkSlice.Of(SliceMs, 1);
            bool done = _warm.Run(ref slice);
            double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            _spentMs += ms;
            _worstMs = Math.Max(_worstMs, ms);
            _frames++;
            if (done)
            {
                Report("in play, a slice a frame");
                enabled = false;
            }
        }

        // The handlers as they stand now on the game's events and the mod's own; a handler already met is not read again.
        private void Root()
        {
            _rooted = true;
            _warm ??= new CodeWarm(typeof(DayWarm).Module, MostMethods, Deepest);
            _warm.Root(GlobalEvents.onNewHour);   // called first in the game's NewDay, before its daily run
            _warm.Root(GlobalEvents.onNewDay);
            Delegate shared = null;
            try
            {
                shared = FeatureDays?.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _warm.Root(shared);
            BizPace.BizPaceGame.Warm(_warm);   // and the handlers of BizMan's open, switch and tab change: the first of a session finds them compiled
        }

        private void Report(string how)
        {
            if (Meta.Dev.Verbose && _warm != null)
            {
                CultureInfo c = CultureInfo.InvariantCulture;
                Logger?.Info("Day warm check (" + how + "): the mod's code for the game's midnight compiled ahead: "
                             + _warm.Read + " of the mod's method(s) read, " + _warm.Compiled + " compiled, " + _warm.Refused
                             + " refused (members this build of the game lacks: compiled when first called, as before), "
                             + _warm.Skipped + " with no code of their own; " + _spentMs.ToString("F0", c) + " ms over " + _frames
                             + " frame(s), the most in a frame " + _worstMs.ToString("F1", c) + " ms (wanted: a slice of "
                             + SliceMs.ToString("F0", c) + " ms, a long method alone in its frame): "
                             + (_worstMs < WorstMs ? "PASS" : "FAIL, a frame over " + WorstMs.ToString("F0", c) + " ms") + ".");
            }
        }

        /// <summary>Options > Tests: the rest of the walk done at once, and its line written; says why not when there is no city.</summary>
        public static void TestNow()
        {
            DayWarm warm = _instance;
            if (warm == null || !ModState.Loaded)
            {
                TestRefusals.TellKey(Logger, "Day warm", "reallife_perf_test_daywarm_none");
                return;
            }
            if (!warm._rooted)
            {
                warm.Root();
            }
            long began = Stopwatch.GetTimestamp();
            WorkSlice whole = WorkSlice.Whole;
            while (!_warm.Run(ref whole))
            {
                whole = WorkSlice.Whole;   // a whole slice still stops before a long method that is not its first
            }
            double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            warm._spentMs += ms;
            warm._frames++;
            warm.Report("Options > Tests, the rest at once in " + ms.ToString("F0", CultureInfo.InvariantCulture) + " ms");
            warm.enabled = false;
            PerfDirector.Notify("reallife_perf_test_daywarm_done", 8f);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
