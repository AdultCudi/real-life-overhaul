using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.TestRun
{
    /// <summary>
    /// The city expansion's test run (Options > Tests, "City expansion: run every check"): a save that undoes it first,
    /// then every check of <see cref="RunList"/> in its order, each pressed without any input after the player is put
    /// where it needs them, its line waited for, what it changed put back; one load asked for in the middle, the run kept
    /// in PlayerPrefs over it and carried on by itself after; a summary notice and a Player.log block at the end, with a
    /// checklist for the eyes. Pressed again while it runs, it stops and puts back what it can. Refused with Developer
    /// diagnostics or the test tools off, in a Going Public session and in another city. A test that throws is logged and
    /// the run goes on. COST: nothing at all while no run is under way (one PlayerPrefs read at start-up); during a run,
    /// one coroutine on the mod's own persistent object with one time comparison a frame, and a prefix test of each log
    /// line the game writes, on the main thread only.
    /// </summary>
    public static class RunDirector
    {
        private const string Pref = "reallife.testrun";   // not a locale key: a dot; the run carried over its load
        private const float SaveWait = 90f;               // s the run's own save is waited for
        private const float LookSeconds = 20f;            // s the run stays where something is for the eyes, at least
        private const float LoadPoll = 0.5f;              // s between looks at whether the load is over, while one is awaited
        private const int MostHeard = 64;                 // lines kept for the check under way between two looks
        private const float Stale = 120f;                 // s without a beat of the run's: it was ended by a fault, and a press begins a new one

        public static IModLogger Logger;

        /// <summary>The run's put backs that outlive a load ("road", "building", "sites"): written by RunList's steps, carried over.</summary>
        internal static readonly Dictionary<string, string> Duties = new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly List<string> Heard = new List<string>(MostHeard);
        private static readonly List<string> Batch = new List<string>(MostHeard);
        private static readonly Dictionary<string, string> LoadHeard = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly List<RunOutcome> Outcomes = new List<RunOutcome>(96);
        private static readonly List<string> LoadPrefixes = new List<string>(8);
        private static readonly string[] Why = new string[1];
        private static readonly Application.LogCallback Hear = OnLog;   // made once: the same delegate is put on the game's log and taken off it

        private static bool _running, _stopping, _listening, _loadArmed;
        private static volatile RunCheck _current;
        private static int _mainThread = -1;
        private static long _began;
        private static float _beat;             // the last frame the run's coroutine looked (unscaled s)
        private static bool _unpaused;          // the run let go of the player's own pause, and gives it back at the end
        private static int _openUse;            // open-ground checks given a lot so far
        private static bool _besideFound;
        private static Vector3 _besideAt;
        private static float _besideYaw;
        private static string _besideWords;

        /// <summary>True while a run is under way, or waits for its load.</summary>
        public static bool Running => _running;

        // ---------------------------------------------------------------- start, the button, the stop

        /// <summary>Once at start-up: a run that asked for a load carries on after it (when it is young enough); otherwise nothing.</summary>
        public static void Begin(IModLogger logger)
        {
            Logger = logger;
            string kept = UnityEngine.PlayerPrefs.GetString(Pref, string.Empty);
            if (kept.Length == 0)
            {
                return;
            }
            if (!RunRules.ReadCarry(kept, out _began, Duties, Outcomes) || !RunRules.Fresh(_began, DateTime.UtcNow.Ticks))
            {
                Meta.Dev.Always(Logger, "Test run: a run kept from before could not be carried on (too old, or written by another version); it is forgotten.");
                Forget();
                return;
            }
            _running = true;
            _beat = Time.unscaledTime;
            Listen();
            Meta.MetaUi.Instance.Run(Guard.Routine(Run(true)));
        }

        /// <summary>"City expansion: run every check": begins the run, or stops the one under way.</summary>
        public static void Press()
        {
            try
            {
                if (_running && Time.unscaledTime - _beat > Stale)
                {
                    Meta.Dev.Always(Logger, "Test run: the run under way had stopped by a fault; it is ended, and a new one begins.");
                    Finish();
                }
                if (_running)
                {
                    _stopping = true;
                    Logger?.Info("Test run: asked to stop; what it changed is put back where it can be.");
                    return;
                }
                if (!Meta.Dev.Verbose || !Settings.ShowTestTools)
                {
                    RunGame.Warn(RunText.NeedsTools, null, 10f);
                    return;
                }
                if (RunGame.Session)
                {
                    RunGame.Warn(RunText.Session, null, 10f);
                    return;
                }
                if (!RunGame.Loaded)
                {
                    TestRefusals.Tell(Logger, "City expansion run", TestRefusal.NotLoaded);
                    return;
                }
                if (RunGame.OtherCity)
                {
                    RunGame.Warn(RunText.OtherCity, null, 10f);
                    return;
                }
                _running = true;
                _stopping = false;
                _beat = Time.unscaledTime;
                _began = DateTime.UtcNow.Ticks;
                Duties.Clear();
                Outcomes.Clear();
                Listen();
                Meta.MetaUi.Instance.Run(Guard.Routine(Run(false)));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Finish();
            }
        }

        // ---------------------------------------------------------------- the run

        private static IEnumerator Run(bool carried)
        {
            if (carried)
            {
                ArmLoad(RunPlan.All());   // from start-up: the load's own lines are heard from its first
                var wait = new WaitForSecondsRealtime(LoadPoll);
                while (!_stopping && !RunGame.Loaded)
                {
                    _beat = Time.unscaledTime;
                    yield return wait;
                }
            }
            List<RunCheck> order = RunRules.Ordered(RunList.All(out string twice));   // once a city is there, every feature registered
            if (twice != null)
            {
                Logger?.Warn("Test run: checks registered twice (the second of each left out): " + twice + ".");
            }
            _openUse = 0;
            _besideFound = false;
            bool stopped = false;
            if (!carried)
            {
                string file = RunGame.SaveStart(RunText.StartSave, out string why);
                if (file == null)
                {
                    Logger?.Warn("Test run: not begun: the save that undoes it could not be written (" + why + ").");
                    RunGame.Warn(RunText.NoSave, null, 12f);
                    Finish();
                    yield break;
                }
                float until = Time.unscaledTime + SaveWait;
                while (RunGame.Saving && Time.unscaledTime < until)
                {
                    yield return null;
                }
                Logger?.Info("Test run: begun with " + order.Count + " checks; the save '" + RunText.StartSave + "' (" + file + ") undoes everything it does.");
                RunGame.Tell(RunText.Started, new Dictionary<string, string>(1) { { "save", RunText.StartSave } }, 14f);
                yield return GameRuns();
                for (int i = 0; i < order.Count && !_stopping; i++)
                {
                    if (order[i].Phase <= RunPhase.ForTheLoad)
                    {
                        yield return One(order[i]);
                    }
                }
                if (!_stopping)
                {
                    ArmLoad(order);
                    Keep();
                    Logger?.Info("Test run: the checks before the load are done; it waits for a save to be loaded and carries on by itself.");
                    RunGame.Tell(RunText.Load, null, 30f);
                    bool seen = false;
                    var poll = new WaitForSecondsRealtime(LoadPoll);
                    while (!_stopping && !(seen && RunGame.Loaded))
                    {
                        _beat = Time.unscaledTime;
                        seen |= RunGame.Loading;
                        yield return poll;
                    }
                }
            }
            if (!_stopping)
            {
                Logger?.Info("Test run: carries on after the load.");
                RunGame.Tell(RunText.CarriedOn, null, 10f);
                yield return GameRuns();
                for (int i = 0; i < order.Count && !_stopping; i++)
                {
                    if (order[i].Phase >= RunPhase.AfterLoad)
                    {
                        yield return One(order[i]);
                    }
                }
            }
            if (_stopping)
            {
                stopped = true;
                _stopping = false;   // the put backs run
                for (int i = 0; i < order.Count; i++)
                {
                    if (order[i].Phase == RunPhase.End && order[i].Place == RunPlace.Here && RunRules.Find(Outcomes, order[i].Id) == null)
                    {
                        yield return One(order[i]);
                    }
                }
            }
            Summary(order, stopped);
            Finish();
        }

        // Until the game runs: the options closed, the player's own pause let go (given back at the end).
        private static IEnumerator GameRuns()
        {
            while (!_stopping)
            {
                _beat = Time.unscaledTime;
                if (RunGame.Loaded && !RunGame.MenuOpen && RunGame.PlayerPaused)
                {
                    RunGame.SetPause(false);
                    _unpaused = true;
                }
                if (RunGame.Running && !RunGame.MenuOpen)
                {
                    yield break;
                }
                yield return null;
            }
        }

        // One check: the player put where it needs them, its test pressed, its line waited for, what it changed put back.
        private static IEnumerator One(RunCheck c)
        {
            if (RunRules.Find(Outcomes, c.Id) != null)
            {
                yield break;   // decided before the load
            }
            if (_stopping)
            {
                Record(c, RunResult.Stopped, "the run was stopped first");
                yield break;
            }
            if (!c.AtLoad && c.Place != RunPlace.Here)
            {
                yield return Place(c);
                if (Why[0] != null)
                {
                    Record(c, RunResult.Fail, "the run could not take the player there: " + Why[0]);
                    yield break;
                }
            }
            lock (Heard)
            {
                Heard.Clear();
            }
            _current = c;
            RunResult result = RunResult.Waiting;
            string line = null;
            bool ran = false;
            float lookUntil = 0f;
            int tries = 0;
            if (c.AtLoad && LoadHeard.TryGetValue(c.Line, out string heard))
            {
                result = RunRules.Judge(heard, c.Pass);
                line = RunRules.Bare(heard);
            }
            if (c.AtLoad && c.Fails != null && LoadHeard.TryGetValue(c.Fails, out string failed))
            {
                result = RunResult.Fail;
                line = RunRules.Bare(failed);
            }
            if (!c.AtLoad)
            {
                if (c.Look != null)
                {
                    RunGame.Tell(c.Look, null, LookSeconds + 10f);
                    lookUntil = Time.unscaledTime + LookSeconds;
                }
                ran = Press(c, ref result, ref line);
                tries = 1;
            }
            float waited = 0f, last = Time.unscaledTime;
            while (result == RunResult.Waiting)
            {
                yield return null;
                float now = Time.unscaledTime;
                _beat = now;
                if (RunGame.Loading && !c.AtLoad)
                {
                    _stopping = true;   // a save loaded by hand while a check ran: what follows would test another save
                    result = RunResult.Stopped;
                    line = "a save was loaded while it waited";
                    break;
                }
                if (RunGame.Running || !RunGame.Loaded)
                {
                    waited += now - last;   // a menu or the pause in a city stops the clock: a check is never timed out while the game stands still
                }
                last = now;
                if (_stopping)
                {
                    result = RunResult.Stopped;
                    line = "the run was stopped while it waited";
                    break;
                }
                bool retry = Drain(c, ref result, ref line);
                if (retry && tries < RunRules.MostTries)
                {
                    result = RunResult.Waiting;
                    float again = Time.unscaledTime + RunRules.RetrySeconds;
                    while (Time.unscaledTime < again && !_stopping)
                    {
                        _beat = Time.unscaledTime;
                        yield return null;
                    }
                    tries++;
                    ran = Press(c, ref result, ref line) || ran;
                    waited = 0f;
                    continue;
                }
                if (result == RunResult.Waiting && waited > c.Seconds)
                {
                    result = RunResult.TimedOut;
                    line = "no line beginning '" + c.Line + "' within " + c.Seconds.ToString("F0", CultureInfo.InvariantCulture) + " s";
                }
            }
            if (result == RunResult.Pass)
            {
                yield return null;   // what the same call wrote after its deciding line (a refusal, a FAIL of the family)
                RunResult more = RunResult.Pass;
                string moreLine = line;
                Drain(c, ref more, ref moreLine);
                if (RunRules.Worse(result, more) != result)
                {
                    result = more;
                    line = moreLine;
                }
            }
            _current = null;
            bool stood = result == RunResult.Pass || result == RunResult.Fail;
            while (stood && c.Look != null && Time.unscaledTime < lookUntil && !_stopping)
            {
                _beat = Time.unscaledTime;
                yield return null;
            }
            if (ran && c.Undo != null && (!c.UndoOnPass || result == RunResult.Pass))
            {
                try
                {
                    c.Undo();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    Logger?.Warn("Test run: putting back after " + RunRules.NameOf(c) + " threw " + e.GetType().Name + ": " + e.Message + "; the run goes on.");
                }
            }
            Record(c, result, line);
        }

        // The test pressed, its fault caught: a test that throws is a FAULT and the run goes on.
        private static bool Press(RunCheck c, ref RunResult result, ref string line)
        {
            if (c.Run == null)
            {
                result = RunResult.Fault;
                line = "the run has no work for it";
                return false;
            }
            try
            {
                c.Run();
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                result = RunResult.Fault;
                line = "it threw " + e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        // The lines heard for the check since the last look: its own, a stand-down said instead, a refusal, a FAIL of its family.
        // True when the refusal passes by itself (the test is to be pressed again).
        private static bool Drain(RunCheck c, ref RunResult result, ref string line)
        {
            Batch.Clear();
            lock (Heard)
            {
                Batch.AddRange(Heard);
                Heard.Clear();
            }
            bool retry = false;
            string family = c.Family;
            for (int i = 0; i < Batch.Count; i++)
            {
                string text = Batch[i];
                RunResult r;
                if (RunRules.Begins(text, c.Line))
                {
                    r = RunRules.Judge(text, c.Pass);
                }
                else if (c.Fails != null && RunRules.Begins(text, c.Fails))
                {
                    r = RunResult.Fail;
                }
                else if (RunRules.Refusal(text, out string reason))
                {
                    r = RunRules.OfRefusal(reason, out bool again);
                    retry |= again;
                }
                else if (RunRules.Begins(text, family) && text.IndexOf(RunRules.FailWord, StringComparison.Ordinal) >= 0)
                {
                    r = RunResult.Fail;
                }
                else
                {
                    continue;
                }
                if (line == null || RunRules.Worse(result, r) != result)
                {
                    line = RunRules.Bare(text);
                }
                result = RunRules.Worse(result, r);
            }
            Batch.Clear();
            return retry && result != RunResult.Pass && result != RunResult.Fail;
        }

        private static void Record(RunCheck c, RunResult result, string line)
        {
            Outcomes.Add(new RunOutcome { Id = c.Id, Result = result, Line = line ?? string.Empty });
            Logger?.Info("Test run: " + RunRules.Word(result) + " " + RunRules.NameOf(c) + (string.IsNullOrEmpty(line) ? string.Empty : " (" + line + ")") + ".");
        }

        // ---------------------------------------------------------------- the places

        // The player put where the check needs them; Why[0] says why not, or is null.
        private static IEnumerator Place(RunCheck c)
        {
            Why[0] = null;
            yield return RunGame.Outdoors(Why);
            if (Why[0] != null || c.Place == RunPlace.Outdoors)
            {
                yield break;
            }
            Vector3 at = Vector3.zero;
            float yaw = 0f;
            string words = null;
            switch (c.Place)
            {
                case RunPlace.UptownLot:
                    if (!RunGame.UptownLot(out at, out yaw, out string lot))
                    {
                        Why[0] = "Uptown's first block is not laid at this load, or every lot of it is built on";
                        yield break;
                    }
                    words = "the front of Uptown's open lot " + lot;
                    break;
                case RunPlace.BesideBuilding:
                    if (!_besideFound)
                    {
                        _besideFound = RunGame.BesideBuilding(out _besideAt, out _besideYaw, out _besideWords);
                    }
                    if (!_besideFound)
                    {
                        Why[0] = "no building of three to six storeys with a street door within reach";
                        yield break;
                    }
                    at = _besideAt;
                    yaw = _besideYaw;
                    words = "the street door of " + _besideWords;
                    break;
                case RunPlace.OpenGround:
                case RunPlace.PavementEdge:
                    int taken = RunGame.TestBuildingLot();
                    int index = c.Place == RunPlace.OpenGround ? RunRules.OpenLot(_openUse++, taken, RunGame.AirportLots) : RunRules.PavementLot(taken, RunGame.AirportLots);
                    if (index < 0)
                    {
                        Why[0] = "no lot of the airport's grass is free";
                        yield break;
                    }
                    RunGame.AirportLot(index, out at, out yaw);
                    words = (c.Place == RunPlace.OpenGround ? "the front of open lot " : "the pavement at open lot ") + (index + 1) + " of the airport's grass";
                    break;
                case RunPlace.TestBuilding:
                    if (!RunGame.TestBuildingDoor(out at, out yaw))
                    {
                        Why[0] = "the test building does not stand at this load";
                        yield break;
                    }
                    words = "the test building's door";
                    break;
            }
            if (RunGame.There(at))
            {
                yield break;
            }
            Logger?.Info("Test run: the player taken to " + words + " (" + at.x.ToString("F0", CultureInfo.InvariantCulture) + ", " + at.z.ToString("F0", CultureInfo.InvariantCulture)
                         + ") for " + RunRules.NameOf(c) + ".");
            yield return RunGame.MoveTo(at, yaw);
        }

        // ---------------------------------------------------------------- the log

        private static void Listen()
        {
            _mainThread = Thread.CurrentThread.ManagedThreadId;
            if (!_listening)
            {
                _listening = true;
                Application.logMessageReceivedThreaded += Hear;
            }
        }

        private static void Unlisten()
        {
            if (_listening)
            {
                _listening = false;
                Application.logMessageReceivedThreaded -= Hear;
            }
        }

        // The lines the load writes are kept from now on, the first of each.
        private static void ArmLoad(List<RunCheck> order)
        {
            lock (LoadHeard)
            {
                LoadPrefixes.Clear();
                for (int i = 0; i < order.Count; i++)
                {
                    if (order[i].AtLoad)
                    {
                        LoadPrefixes.Add(order[i].Line);
                        if (order[i].Fails != null)
                        {
                            LoadPrefixes.Add(order[i].Fails);
                        }
                    }
                }
                _loadArmed = true;
            }
        }

        // From any thread, for every line logged. Nothing here may log (it would come back here); a fault drops the line.
        private static void OnLog(string text, string stack, LogType type)
        {
            try
            {
                if (Thread.CurrentThread.ManagedThreadId != _mainThread || string.IsNullOrEmpty(text))
                {
                    return;
                }
                RunCheck c = _current;
                if (c != null && (RunRules.Begins(text, c.Line) || (c.Fails != null && RunRules.Begins(text, c.Fails)) || text.IndexOf(RunRules.RefusedMark, StringComparison.Ordinal) >= 0
                                  || (text.IndexOf(RunRules.FailWord, StringComparison.Ordinal) >= 0 && RunRules.Begins(text, c.Family))))
                {
                    lock (Heard)
                    {
                        if (Heard.Count < MostHeard)
                        {
                            Heard.Add(text);
                        }
                    }
                }
                if (_loadArmed)
                {
                    lock (LoadHeard)
                    {
                        for (int i = 0; i < LoadPrefixes.Count; i++)
                        {
                            if (!LoadHeard.ContainsKey(LoadPrefixes[i]) && RunRules.Begins(text, LoadPrefixes[i]))
                            {
                                LoadHeard[LoadPrefixes[i]] = text;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // (see above: a line is lost, the game's log is not touched)
            }
        }

        // ---------------------------------------------------------------- across the load, and the end

        private static void Keep()
        {
            UnityEngine.PlayerPrefs.SetString(Pref, RunRules.Carry(_began, Duties, Outcomes));
            UnityEngine.PlayerPrefs.Save();
        }

        private static void Forget()
        {
            UnityEngine.PlayerPrefs.DeleteKey(Pref);
            UnityEngine.PlayerPrefs.Save();
        }

        private static void Summary(List<RunCheck> order, bool stopped)
        {
            string block = RunRules.Summary(order, Outcomes, RunRules.Eyes(order, Outcomes), stopped, out int passed, out int ran, out List<string> failed);
            Meta.Dev.Always(Logger, block);
            var data = new Dictionary<string, string>(3)
            {
                { "passed", passed.ToString(CultureInfo.InvariantCulture) }, { "ran", ran.ToString(CultureInfo.InvariantCulture) },
                { "failed", RunRules.Short(failed, MostFailedWords) }
            };
            if (stopped)
            {
                RunGame.Warn(RunText.Stopped, new Dictionary<string, string>(1) { { "save", RunText.StartSave } }, 20f);
            }
            else if (failed.Count == 0)
            {
                RunGame.Tell(RunText.DoneAll, data, 30f);
            }
            else
            {
                RunGame.Warn(RunText.Done, data, 30f);
            }
        }

        private const int MostFailedWords = 100;   // characters of failed checks' names in the notice: it stays within the game's 240

        private static void Finish()
        {
            if (_unpaused && RunGame.Loaded)
            {
                RunGame.SetPause(true);   // the player's own pause, as it was before the run let go of it
            }
            _unpaused = false;
            _running = false;
            _stopping = false;
            _current = null;
            lock (LoadHeard)
            {
                _loadArmed = false;
                LoadPrefixes.Clear();
                LoadHeard.Clear();
            }
            lock (Heard)
            {
                Heard.Clear();
            }
            Duties.Clear();
            Unlisten();
            Forget();
        }
    }
}
