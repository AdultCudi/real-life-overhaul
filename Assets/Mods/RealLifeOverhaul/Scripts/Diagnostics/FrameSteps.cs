using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.LowLevel;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Where the frame before this one went, for the long-frame line of the session record: how long the engine
    /// spent in every script's Update, in coroutines and timers, in LateUpdate, in FixedUpdate, in physics, in
    /// navigation (the step in which the engine re-cuts the walkable mesh round a carving obstacle), in animation,
    /// in the interface's rebuilds, in rendering and in bringing loaded assets in. A long frame whose time is in
    /// "navigation" is a cut of the mesh; one in "coroutines" while the game's screen is black is the game's own
    /// walk into or out of a building; one in "Update" is a script's, the game's or the mod's.
    ///
    /// It works as <see cref="PerfLoop"/> does (a timestamp step put into the engine's list of frame steps), and it
    /// keeps one frame, not a window. The dozen steps named in <see cref="GroupOf"/> are added up into their groups;
    /// every other step of the frame (the engine's own, and any other mod's) is timed by itself, so that a long frame
    /// whose time is in "the rest" can name the one step it was in (<see cref="LastOther"/>).
    ///
    /// The frame is the one Time.unscaledDeltaTime measures: from one time update to the next. Since Unity 2022.2 the
    /// time is updated in the loop's first step, TimeUpdate.WaitForLastPresentationAndUpdateTime, after the wait for the
    /// frame before to be shown; so the sums roll over just after that step, and the wait (the graphics card, the render
    /// thread or the display) closes the frame it was part of. Until October 2026 they rolled over just before it, and a long
    /// wait was counted in the next frame's sums while the delta already held it: it showed as "the rest" with no step
    /// named. The span between two roll-overs is kept too (<see cref="LastSpanMs"/>), with the time in all the timed
    /// steps, so that what lay outside every step (between two passes of the loop, or in a phase without steps) is known
    /// as well; and the operating system's figures for the same frame are read at the roll-over (<see cref="MachineClock"/>).
    ///
    /// Per frame some two hundred and fifty timestamp reads and as many additions, and four calls into kernel32 (a few
    /// microseconds in all), nothing allocated. Installed only while "Developer diagnostics" is on and the option
    /// "Measurements time every step of the frame" allows it; taken out of the loop again when either goes off, and
    /// stood aside while a probe or a benchmark has its own timestamps in the loop.
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
        private static readonly PlayerLoopSystem.UpdateFunction BeginStep = Begin;
        private static readonly PlayerLoopSystem.UpdateFunction RollStep = Roll;
        private static PlayerLoopSystem.UpdateFunction[] _ends;
        private static long[] _otherNow = new long[0];      // every other step, the frame being run
        private static long[] _otherLast = new long[0];     // ...and the frame before it
        private static string[] _otherNames = new string[0];

        private static long _began;
        private static long _rolledAt;          // the clock at the last roll-over
        private static long _spanLast;          // the frame before this one, roll-over to roll-over
        private static long _stepsLast;         // ...and the time in all its timed steps, the groups and every other step
        private static long _restLast;          // ...of which in the steps outside the groups ("the rest")
        private static int _rolls;
        private static int _rolledFrame = -10;
        private static bool _wanted, _paused, _installed;
        private static string _shape = "not read";

        /// <summary>True while the timestamps are in the engine's loop.</summary>
        public static bool Installed => _installed;

        /// <summary>True when <see cref="LastMs"/> speaks of the frame before this one (the timestamps are in the loop and ran over a whole frame).</summary>
        public static bool Valid => _installed && _rolls >= 2 && Time.frameCount - _rolledFrame <= 1;

        /// <summary>Milliseconds the frame before this one spent in a group of steps.</summary>
        public static float LastMs(int group) => (float)(Last[group] * 1000.0 / Stopwatch.Frequency);

        /// <summary>Milliseconds from the roll-over before the frame to the one after it: the frame's length by the same clock as its steps.</summary>
        public static double LastSpanMs => _spanLast * 1000.0 / Stopwatch.Frequency;

        /// <summary>Milliseconds the frame before this one spent in all its timed steps.</summary>
        public static double LastStepsMs => _stepsLast * 1000.0 / Stopwatch.Frequency;

        /// <summary>Milliseconds it spent in the steps outside the named groups ("the rest" of the steps).</summary>
        public static double LastRestMs => _restLast * 1000.0 / Stopwatch.Frequency;

        /// <summary>For the recording line: where the sums roll over, and the engine's loop as it was found (its phases and their steps).</summary>
        public static string Shape => _shape;

        /// <summary>
        /// The step outside the named groups that took the most of the frame before this one ("PostLateUpdate.UpdateAllSkinnedMeshes",
        /// or another mod's step by its type), with its milliseconds. False when none was timed.
        /// </summary>
        public static bool LastOther(out string name, out float ms)
        {
            name = null;
            ms = 0f;
            long best = 0L;
            for (int i = 0; i < _otherLast.Length; i++)
            {
                if (_otherLast[i] > best)
                {
                    best = _otherLast[i];
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
            // Where the sums roll over: just after the step that updates the time (TimeUpdate.WaitForLastPresentationAndUpdateTime
            // since 2022.2, Initialization.PlayerUpdateTime before it), so that a frame's sums and Time.unscaledDeltaTime cover
            // the same stretch. Failing both, before the loop's very first step, as before.
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
                var with = new PlayerLoopSystem[steps.Length * 3 + (rolled ? 0 : 1)];
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
            _rolls = 0;
            _shape = (rollPhase >= 0
                         ? "rolled over just after " + StepName(phases[rollPhase].type, phases[rollPhase].subSystemList[rollStep].type) + ", where the engine updates the time"
                         : "rolled over before the loop's first step (no step that updates the time was found)")
                     + "; the engine's loop: " + shape + " steps; a phase without steps, and the time between two passes of the loop, count as outside the engine's steps";
            MachineClock.Start();
            _otherNames = otherNames.ToArray();
            _otherNow = new long[_otherNames.Length];
            _otherLast = new long[_otherNames.Length];
            loop.subSystemList = timedPhases;
            PlayerLoop.SetPlayerLoop(loop);
            _installed = true;
        }

        // Out of the loop as it is now, whoever has changed it since: every step of this class's is left out, the rest is untouched.
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
                Last[g] = Now[g];
                Now[g] = 0;
            }
            long[] now = _otherNow, last = _otherLast;
            for (int i = 0; i < now.Length && i < last.Length; i++)
            {
                rest += now[i];
                last[i] = now[i];
                now[i] = 0;
            }
            _stepsLast = steps + rest;
            _restLast = rest;
            _spanLast = at - _rolledAt;
            _rolledAt = at;
            if (_rolls < 2)
            {
                _rolls++;   // the first roll-over after the timestamps go in closes a part of a frame only
            }
            _rolledFrame = Time.frameCount;
            MachineClock.Sample();
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

        /// <summary>
        /// Which group an engine step is timed under, or -1 for a step that is not timed (it is part of "the rest").
        /// The names are the engine's own (UnityEngine.PlayerLoop, the same in every 2022.3 player).
        /// </summary>
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
