using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.LowLevel;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the frame before this one went, for the session record's long-frame line: timestamp steps put into the
    /// engine's loop, as <see cref="PerfLoop"/> does, add the time of the steps named in <see cref="GroupOf"/> into their
    /// groups and time every other step by itself (<see cref="LastOther"/>). The sums roll over just after the step that
    /// updates the time, so a frame here is the one Time.unscaledDeltaTime measures and the wait for the last frame to be
    /// shown closes the frame it was part of; the span between roll-overs (<see cref="LastSpanMs"/>) gives the time
    /// outside every step. Per frame, while installed: some two hundred and fifty clock reads and four kernel32 calls
    /// (<see cref="MachineClock"/>), nothing allocated. Installed only with "Developer diagnostics" on and the step timing
    /// option allowing it; stood aside while a probe has its own timestamps in.
    /// </summary>
    public static class FrameSteps
    {
        public const int Update = 0, Coroutines = 1, LateUpdate = 2, Fixed = 3, Physics = 4, Navigation = 5, Animation = 6, Ui = 7, Render = 8, Loading = 9, Wait = 10;
        public const int Count = 11;

        /// <summary>The groups' names as the long-frame line gives them, in the order of the constants.</summary>
        public static readonly string[] Names =
        {
            "scripts' Update", "coroutines and timers", "scripts' LateUpdate", "scripts' FixedUpdate", "physics", "navigation", "animation", "interface", "rendering", "loading",
            "waiting for the last frame to be shown",
        };

        private static readonly long[] Now = new long[Count];    // the frame being run
        private static readonly long[] Last = new long[Count];   // the frame before it, complete
        private static readonly long[] Before = new long[Count]; // ...and the one before that, for a time the engine counts a frame late
        private static readonly PlayerLoopSystem.UpdateFunction BeginStep = Begin;
        private static readonly PlayerLoopSystem.UpdateFunction RollStep = Roll;
        private static readonly PlayerLoopSystem.UpdateFunction ReadStep = Read;
        private static PlayerLoopSystem.UpdateFunction[] _ends;
        private static long[] _otherNow = new long[0];      // every other step, the frame being run
        private static long[] _otherLast = new long[0];     // ...and the frame before it
        private static long[] _otherBefore = new long[0];   // ...and the one before that
        private static string[] _otherNames = new string[0];

        private static long _began;
        private static long _rolledAt;          // the clock at the last roll-over
        private static long _spanLast;          // the frame before this one, roll-over to roll-over
        private static long _stepsLast;         // ...and the time in all its timed steps, the groups and every other step
        private static long _restLast;          // ...of which in the steps outside the groups ("the rest")
        private static long _spanBefore, _stepsBefore, _restBefore;   // the same three for the frame before that
        private static long _readAt;            // the clock at the last pass of the step where the engine reads the time for its scripts
        private static long _readSpan;          // ...and the time between its last two passes
        private static int _reads;
        private static int _rolls;
        private static int _rolledFrame = -10;
        private static int _readFrame = -10;
        private static bool _wanted, _paused, _installed;
        private static string _shape = "not read";

        /// <summary>True while the timestamps are in the engine's loop.</summary>
        public static bool Installed => _installed;

        /// <summary>True when <see cref="LastMs"/> speaks of the whole frame before this one.</summary>
        public static bool Valid => _installed && _rolls >= 2 && Time.frameCount - _rolledFrame <= 1;

        /// <summary>True when the frame before the last one was complete too (<see cref="BeforeSpanMs"/> and the figures of <see cref="BeforeMs"/>).</summary>
        public static bool ValidBefore => Valid && _rolls >= 3;

        /// <summary>True when the engine's own read of the time has a stretch between two passes (<see cref="ReadSpanMs"/>).</summary>
        public static bool ValidRead => Valid && _reads >= 2 && _readFrame == _rolledFrame;

        /// <summary>Milliseconds the frame before this one spent in a group of steps.</summary>
        public static float LastMs(int group) => (float)(Last[group] * 1000.0 / Stopwatch.Frequency);

        /// <summary>Milliseconds between the roll-overs around the frame: its length by the same clock as its steps.</summary>
        public static double LastSpanMs => _spanLast * 1000.0 / Stopwatch.Frequency;

        /// <summary>Milliseconds between the engine's last two reads of the time for its scripts (Initialization.PlayerUpdateTime), or below zero when the loop has none.</summary>
        public static double ReadSpanMs => ValidRead ? _readSpan * 1000.0 / Stopwatch.Frequency : -1.0;

        /// <summary>The frame before the one <see cref="LastSpanMs"/> speaks of, roll-over to roll-over; below zero when not complete.</summary>
        public static double BeforeSpanMs => ValidBefore ? _spanBefore * 1000.0 / Stopwatch.Frequency : -1.0;

        /// <summary>Milliseconds the frame before the last one spent in a group of steps.</summary>
        public static float BeforeMs(int group) => (float)(Before[group] * 1000.0 / Stopwatch.Frequency);

        /// <summary>Milliseconds the frame before the last one spent in all its timed steps.</summary>
        public static double BeforeStepsMs => _stepsBefore * 1000.0 / Stopwatch.Frequency;

        /// <summary>...and in the steps outside the named groups.</summary>
        public static double BeforeRestMs => _restBefore * 1000.0 / Stopwatch.Frequency;

        /// <summary>Milliseconds the frame before this one spent in all its timed steps.</summary>
        public static double LastStepsMs => _stepsLast * 1000.0 / Stopwatch.Frequency;

        /// <summary>Milliseconds it spent in the steps outside the named groups ("the rest" of the steps).</summary>
        public static double LastRestMs => _restLast * 1000.0 / Stopwatch.Frequency;

        /// <summary>For the recording line: where the sums roll over, and the engine's loop as it was found (its phases and their steps).</summary>
        public static string Shape => _shape;

        /// <summary>The step outside the named groups that took the most of the frame before this one, with its milliseconds; false if none.</summary>
        public static bool LastOther(out string name, out float ms) => Other(_otherLast, out name, out ms);

        /// <summary>The same for the frame before the last one.</summary>
        public static bool BeforeOther(out string name, out float ms) => Other(_otherBefore, out name, out ms);

        private static bool Other(long[] sums, out string name, out float ms)
        {
            name = null;
            ms = 0f;
            long best = 0L;
            for (int i = 0; i < sums.Length && i < _otherNames.Length; i++)
            {
                if (sums[i] > best)
                {
                    best = sums[i];
                    name = _otherNames[i];
                }
            }
            if (name == null)
            {
                return false;
            }
            ms = (float)(best * 1000.0 / Stopwatch.Frequency);
            return true;
        }

        /// <summary>Asks for the timestamps to be in the engine's loop, or out of it.</summary>
        public static void Want(bool on)
        {
            _wanted = on;
            Reconcile();
        }

        /// <summary>A probe or a benchmark is about to put its own timestamps in: these stand aside until <see cref="Resume"/>.</summary>
        public static void Pause()
        {
            _paused = true;
            Reconcile();
        }

        public static void Resume()
        {
            _paused = false;
            Reconcile();
        }

        /// <summary>The option that allows the timestamps has changed.</summary>
        public static void Refresh()
        {
            Reconcile();
        }

        private static void Reconcile()
        {
            bool want = _wanted && !_paused && PerfTweaks.StepTiming;
            if (want == _installed)
            {
                return;
            }
            try
            {
                if (want)
                {
                    Install();
                }
                else
                {
                    Remove();
                }
            }
            catch (Exception)
            {
                _installed = false;   // the loop is the engine's: if it cannot be read the long-frame line simply has no split
            }
        }

        private static void Install()
        {
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoopSystem[] phases = loop.subSystemList;
            if (phases == null || phases.Length == 0)
            {
                return;
            }
            if (_ends == null)
            {
                _ends = new PlayerLoopSystem.UpdateFunction[Count];
                for (int g = 0; g < Count; g++)
                {
                    int group = g;
                    _ends[g] = () => Now[group] += Stopwatch.GetTimestamp() - _began;
                }
            }
            var timedPhases = new PlayerLoopSystem[phases.Length];
            // The sums roll over just after the step that updates the time, so a frame's sums and Time.unscaledDeltaTime
            // cover the same stretch; failing that, before the loop's very first step.
            int rollPhase = -1, rollStep = -1;
            for (int p = 0; p < phases.Length && rollPhase < 0; p++)
            {
                PlayerLoopSystem[] steps = phases[p].subSystemList;
                for (int s = 0; steps != null && s < steps.Length; s++)
                {
                    if (UpdatesTime(steps[s].type))
                    {
                        rollPhase = p;
                        rollStep = s;
                        break;
                    }
                }
            }
            // Where the engine reads the time for its scripts, when that is not the step above: a stretch of the frame between the two
            // would lie in the engine's frame time and in no frame of the mod's own clock.
            int readPhase = -1, readStep = -1;
            for (int p = 0; p < phases.Length && readPhase < 0; p++)
            {
                PlayerLoopSystem[] steps = phases[p].subSystemList;
                for (int s = 0; steps != null && s < steps.Length; s++)
                {
                    Type step = steps[s].type;
                    if (step != null && step.Namespace == "UnityEngine.PlayerLoop" && step.Name == "PlayerUpdateTime" && !(p == rollPhase && s == rollStep))
                    {
                        readPhase = p;
                        readStep = s;
                        break;
                    }
                }
            }
            bool rolled = false;
            var shape = new System.Text.StringBuilder(256);
            // Every step outside the named groups gets a slot of its own, with its name, and an end that adds to that slot.
            var otherNames = new List<string>(160);
            var otherEnds = new List<PlayerLoopSystem.UpdateFunction>(160);
            for (int p = 0; p < phases.Length; p++)
            {
                PlayerLoopSystem phase = phases[p];   // a copy: the game's own arrays are never written to
                PlayerLoopSystem[] steps = phase.subSystemList;
                shape.Append(p > 0 ? ", " : string.Empty).Append(phase.type != null ? phase.type.Name : "(unnamed)").Append(' ').Append(steps != null ? steps.Length : 0);
                if (steps == null || steps.Length == 0)
                {
                    timedPhases[p] = phase;
                    continue;
                }
                var with = new PlayerLoopSystem[steps.Length * 3 + (rolled ? 0 : 1) + 1];
                int at = 0;
                if (!rolled && rollPhase < 0)
                {
                    rolled = true;
                    with[at++] = new PlayerLoopSystem { type = typeof(FrameSteps), updateDelegate = RollStep };   // the frame's very first step
                }
                for (int s = 0; s < steps.Length; s++)
                {
                    int group = GroupOf(steps[s].type);
                    if (group >= 0)
                    {
                        with[at++] = new PlayerLoopSystem { type = typeof(FrameSteps), updateDelegate = BeginStep };
                        with[at++] = steps[s];
                        with[at++] = new PlayerLoopSystem { type = typeof(FrameSteps), updateDelegate = _ends[group] };
                    }
                    else
                    {
                        int other = otherNames.Count;
                        otherNames.Add(StepName(phase.type, steps[s].type));
                        otherEnds.Add(() => _otherNow[other] += Stopwatch.GetTimestamp() - _began);
                        with[at++] = new PlayerLoopSystem { type = typeof(FrameSteps), updateDelegate = BeginStep };
                        with[at++] = steps[s];
                        with[at++] = new PlayerLoopSystem { type = typeof(FrameSteps), updateDelegate = otherEnds[other] };
                    }
                    if (p == rollPhase && s == rollStep)
                    {
                        rolled = true;
                        with[at++] = new PlayerLoopSystem { type = typeof(FrameSteps), updateDelegate = RollStep };   // just after the time update: the frame's end
                    }
                    if (p == readPhase && s == readStep)
                    {
                        with[at++] = new PlayerLoopSystem { type = typeof(FrameSteps), updateDelegate = ReadStep };
                    }
                }
                if (at < with.Length)
                {
                    Array.Resize(ref with, at);   // no roll-over in this phase: the slot kept for one is not left empty
                }
                phase.subSystemList = with;
                timedPhases[p] = phase;
            }
            Array.Clear(Now, 0, Count);
            Array.Clear(Last, 0, Count);
            Array.Clear(Before, 0, Count);
            _rolls = 0;
            _reads = 0;
            _spanBefore = _stepsBefore = _restBefore = 0L;
            _shape = (rollPhase >= 0
                         ? "rolled over just after " + StepName(phases[rollPhase].type, phases[rollPhase].subSystemList[rollStep].type) + ", where the engine updates the time"
                         : "rolled over before the loop's first step (no step that updates the time was found)")
                     + "; the engine's loop: " + shape + " steps; a phase without steps, and the time between two passes of the loop, count as outside the engine's steps";
            MachineClock.Start();
            _otherNames = otherNames.ToArray();
            _otherNow = new long[_otherNames.Length];
            _otherLast = new long[_otherNames.Length];
            _otherBefore = new long[_otherNames.Length];
            loop.subSystemList = timedPhases;
            PlayerLoop.SetPlayerLoop(loop);
            _installed = true;
        }

        // Every step of this class's taken out of the loop as it is now, whoever has changed it since.
        private static void Remove()
        {
            _installed = false;
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoopSystem[] phases = loop.subSystemList;
            if (phases == null)
            {
                return;
            }
            var cleaned = new PlayerLoopSystem[phases.Length];
            bool any = false;
            for (int p = 0; p < phases.Length; p++)
            {
                PlayerLoopSystem phase = phases[p];
                PlayerLoopSystem[] steps = phase.subSystemList;
                int mine = 0;
                for (int s = 0; steps != null && s < steps.Length; s++)
                {
                    mine += steps[s].type == typeof(FrameSteps) ? 1 : 0;
                }
                if (mine > 0)
                {
                    any = true;
                    var without = new PlayerLoopSystem[steps.Length - mine];
                    int at = 0;
                    for (int s = 0; s < steps.Length; s++)
                    {
                        if (steps[s].type != typeof(FrameSteps))
                        {
                            without[at++] = steps[s];
                        }
                    }
                    phase.subSystemList = without;
                }
                cleaned[p] = phase;
            }
            if (any)
            {
                loop.subSystemList = cleaned;
                PlayerLoop.SetPlayerLoop(loop);
            }
        }

        private static void Begin()
        {
            _began = Stopwatch.GetTimestamp();
        }

        private static void Roll()
        {
            long at = Stopwatch.GetTimestamp();
            long steps = 0L, rest = 0L;
            for (int g = 0; g < Count; g++)
            {
                steps += Now[g];
                Before[g] = Last[g];
                Last[g] = Now[g];
                Now[g] = 0;
            }
            long[] now = _otherNow, last = _otherLast, before = _otherBefore;
            for (int i = 0; i < now.Length && i < last.Length && i < before.Length; i++)
            {
                rest += now[i];
                before[i] = last[i];
                last[i] = now[i];
                now[i] = 0;
            }
            _spanBefore = _spanLast;
            _stepsBefore = _stepsLast;
            _restBefore = _restLast;
            _stepsLast = steps + rest;
            _restLast = rest;
            _spanLast = at - _rolledAt;
            _rolledAt = at;
            if (_rolls < 3)
            {
                _rolls++;   // the first roll-over after the timestamps go in closes a part of a frame only
            }
            _rolledFrame = Time.frameCount;
            MachineClock.Sample();
        }

        private static void Read()
        {
            long at = Stopwatch.GetTimestamp();
            _readSpan = at - _readAt;
            _readAt = at;
            _readFrame = Time.frameCount;
            if (_reads < 2)
            {
                _reads++;
            }
        }

        // The step in which the engine updates the frame's time (and so where Time.unscaledDeltaTime ends).
        private static bool UpdatesTime(Type step)
        {
            return step != null && step.Namespace == "UnityEngine.PlayerLoop"
                   && (step.Name == "WaitForLastPresentationAndUpdateTime" || step.Name == "PlayerUpdateTime");
        }

        // "PostLateUpdate.UpdateAllSkinnedMeshes" for the engine's steps; another mod's by its own type's full name.
        private static string StepName(Type phase, Type step)
        {
            if (step == null)
            {
                return (phase != null ? phase.Name : "a phase") + ".(unnamed)";
            }
            return step.Namespace != null && step.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal)
                ? (phase != null ? phase.Name + "." : string.Empty) + step.Name
                : step.FullName;
        }

        /// <summary>The group an engine step is timed under, or -1 for one of "the rest". The engine's own names (UnityEngine.PlayerLoop).</summary>
        private static int GroupOf(Type step)
        {
            if (step == null || step.Namespace != "UnityEngine.PlayerLoop")
            {
                return -1;
            }
            switch (step.Name)
            {
                case "ScriptRunBehaviourUpdate": return Update;
                case "ScriptRunDelayedDynamicFrameRate":
                case "ScriptRunDelayedTasks":
                case "ScriptRunDelayedStartupFrame": return Coroutines;
                case "ScriptRunBehaviourLateUpdate": return LateUpdate;
                case "ScriptRunBehaviourFixedUpdate":
                case "ScriptRunDelayedFixedFrameRate": return Fixed;
                case "PhysicsFixedUpdate":
                case "PhysicsUpdate": return Physics;
                case "AIUpdate":
                case "AIUpdatePostScript": return Navigation;
                case "DirectorUpdateAnimationBegin":
                case "DirectorUpdateAnimationEnd":
                case "DirectorFixedUpdate":
                case "DirectorUpdate": return Animation;
                case "PlayerUpdateCanvases":
                case "UpdateCanvasRectTransform": return Ui;
                case "FinishFrameRendering": return Render;
                case "UpdatePreloading": return Loading;
                case "WaitForLastPresentationAndUpdateTime":
                case "PresentAfterDraw": return Wait;
                default: return -1;
            }
        }
    }
}
