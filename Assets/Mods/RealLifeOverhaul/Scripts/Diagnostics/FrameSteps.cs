using System;
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
    /// It works as <see cref="PerfLoop"/> does (a timestamp step put into the engine's list of frame steps), but
    /// only in front of and behind the dozen steps named in <see cref="GroupOf"/>, and it keeps one frame, not a
    /// window: per frame about thirty timestamp reads and as many additions (under two microseconds), nothing
    /// allocated. Installed only while "Developer diagnostics" is on and the option "Measurements time every step
    /// of the frame" allows it; taken out of the loop again when either goes off, and stood aside while a probe or
    /// a benchmark has its own timestamps in the loop.
    /// </summary>
    public static class FrameSteps
    {
        public const int Update = 0, Coroutines = 1, LateUpdate = 2, Fixed = 3, Physics = 4, Navigation = 5, Animation = 6, Ui = 7, Render = 8, Loading = 9;
        public const int Count = 10;

        /// <summary>The groups' names as the long-frame line gives them, in the order of the constants.</summary>
        public static readonly string[] Names =
        {
            "scripts' Update", "coroutines and timers", "scripts' LateUpdate", "scripts' FixedUpdate", "physics", "navigation", "animation", "interface", "rendering", "loading",
        };

        private static readonly long[] Now = new long[Count];    // the frame being run
        private static readonly long[] Last = new long[Count];   // the frame before it, complete
        private static readonly PlayerLoopSystem.UpdateFunction BeginStep = Begin;
        private static readonly PlayerLoopSystem.UpdateFunction RollStep = Roll;
        private static PlayerLoopSystem.UpdateFunction[] _ends;

        private static long _began;
        private static int _rolledFrame = -10;
        private static bool _wanted, _paused, _installed;

        /// <summary>True while the timestamps are in the engine's loop.</summary>
        public static bool Installed => _installed;

        /// <summary>True when <see cref="LastMs"/> speaks of the frame before this one (the timestamps are in the loop and ran).</summary>
        public static bool Valid => _installed && Time.frameCount - _rolledFrame <= 1;

        /// <summary>Milliseconds the frame before this one spent in a group of steps.</summary>
        public static float LastMs(int group) => (float)(Last[group] * 1000.0 / Stopwatch.Frequency);

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
            bool rolled = false;
            for (int p = 0; p < phases.Length; p++)
            {
                PlayerLoopSystem phase = phases[p];   // a copy: the game's own arrays are never written to
                PlayerLoopSystem[] steps = phase.subSystemList;
                if (steps == null || steps.Length == 0)
                {
                    timedPhases[p] = phase;
                    continue;
                }
                int timed = 0;
                for (int s = 0; s < steps.Length; s++)
                {
                    timed += GroupOf(steps[s].type) >= 0 ? 1 : 0;
                }
                var with = new PlayerLoopSystem[steps.Length + timed * 2 + (rolled ? 0 : 1)];
                int at = 0;
                if (!rolled)
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
                        with[at++] = steps[s];
                    }
                }
                phase.subSystemList = with;
                timedPhases[p] = phase;
            }
            Array.Clear(Now, 0, Count);
            Array.Clear(Last, 0, Count);
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
            for (int g = 0; g < Count; g++)
            {
                Last[g] = Now[g];
                Now[g] = 0;
            }
            _rolledFrame = Time.frameCount;
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
                default: return -1;
            }
        }
    }
}
