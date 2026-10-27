using System;
using System.Collections;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>Meta's part of a city load: once the save's data is read, the leftovers check, the Guide's contact, the welcome and the old-build notice; short waits that stop.</summary>
    public static class MetaCity
    {
        private const float GiveUpAfter = 900f;   // real seconds a wait lasts at most

        private static int _generation;

        /// <summary>The object every city feature is a component of, so that the save tools can stop them all.</summary>
        public static GameObject Root { get; private set; }

        public static void Begin(ModContext context, GameObject root)
        {
            try
            {
                Root = root;
                MetaLog.Sink = context.Logger;
                SaveTools.CityLoaded();
                Dev.Apply();
                _generation++;
                GuideApp.Begin(_generation);
                SettingsApp.Begin(_generation);
                MetaUi.Instance.Run(Watch(_generation));
                if (GameBuild.CityDue)
                {
                    MetaUi.Instance.Run(BuildWatch(_generation));
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        public static void End()
        {
            _generation++;
            Root = null;
            GuideApp.End();
            SettingsApp.End();
            try
            {
                if (MetaUi.Exists)
                {
                    MetaUi.Instance.CloseAll();
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        private static bool Ready()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && gm.playerController != null && SaveGameManager.Current != null && !LoadScene.isLoading && ModState.Loaded;
        }

        private static IEnumerator Watch(int generation)
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            float giveUp = Time.realtimeSinceStartup + GiveUpAfter;
            while (generation == _generation && !Ready())
            {
                if (Time.realtimeSinceStartup > giveUp)
                {
                    yield break;
                }
                yield return wait;
            }
            if (generation != _generation)
            {
                yield break;
            }
            SaveTools.CheckAfterLoad();
            SaveTools.EnsureHook();
            GuideContact.Setup();

            if (!Welcome.Due)
            {
                yield break;
            }
            // Not on top of the first seconds of a load, a menu, a dialogue or the game being paused.
            float earliest = Time.realtimeSinceStartup + 4f;
            while (generation == _generation && (Time.realtimeSinceStartup < earliest || !Quiet()))
            {
                if (Time.realtimeSinceStartup > giveUp)
                {
                    yield break;
                }
                yield return wait;
            }
            if (generation == _generation && Welcome.Due)
            {
                Welcome.Open(false);
            }
        }

        // Once a session, in a wait of its own, so that nothing of the load waits for it and nothing of the load keeps it away.
        private static IEnumerator BuildWatch(int generation)
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            float giveUp = Time.realtimeSinceStartup + GiveUpAfter;
            while (generation == _generation && !Ready())
            {
                if (Time.realtimeSinceStartup > giveUp)
                {
                    yield break;
                }
                yield return wait;
            }
            // A menu or a pause is waited out for half a minute at most: on an older game what says the city is quiet may be missing.
            float earliest = Time.realtimeSinceStartup + 4f;
            float latest = earliest + 30f;
            while (generation == _generation && (Time.realtimeSinceStartup < earliest || GameConfirm.IsOpen || (Time.realtimeSinceStartup < latest && !Quiet())))
            {
                if (Time.realtimeSinceStartup > giveUp)
                {
                    yield break;
                }
                yield return wait;
            }
            if (generation == _generation && GameBuild.CityDue)
            {
                GameBuild.ShowInCity();
            }
        }

        private static bool Quiet()
        {
            try
            {
                return Ready() && !ThirdPersonCamera.IsUiOpen() && LifeEvents.IsSafeForEvent() && !MetaUi.AnyOpen;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The "Developer diagnostics" switch: off, the development keys do nothing and the one logger every class is handed
    /// (<see cref="Gated"/>) drops Info lines but those written through <see cref="Always"/>.
    /// </summary>
    public static class Dev
    {
        private sealed class GatedLogger : IModLogger
        {
            private readonly IModLogger _inner;

            public GatedLogger(IModLogger inner)
            {
                _inner = inner;
            }

            /// <summary>The game's own logger behind the switch (<see cref="Always"/>).</summary>
            public IModLogger Inner => _inner;

            public void Info(string message)
            {
                if (_verbose)
                {
                    Counted();
                    if (!DayLines.Hold(_inner, message))   // the lines of the frame the day turns in are written in the frames after it
                    {
                        _inner.Info(message);
                    }
                }
            }

            public void Warn(string message)
            {
                Counted();
                DayLines.Flush();   // what was held comes first: the log stays in order
                _inner.Warn(message);
            }

            public void Error(string message)
            {
                Counted();
                DayLines.Flush();
                _inner.Error(message);
            }

            public void Error(Exception exception)
            {
                Counted();
                DayLines.Flush();
                _inner.Error(exception);
            }
        }

        private static bool _verbose;
        private static int _lines;

        /// <summary>Lines the mod has written to the log this run, for the session record: the game keeps every one in memory (DebugLogCollector).</summary>
        public static int Lines => _lines;

        private static void Counted()
        {
            System.Threading.Interlocked.Increment(ref _lines);   // features log from worker threads too
        }

        /// <summary>An Info line that goes to the log whatever the switch says, for the rare line a player's log must show to answer a report. Once per event.</summary>
        public static void Always(IModLogger logger, string message)
        {
            if (logger is GatedLogger gated)
            {
                Counted();
                DayLines.Flush();
                gated.Inner.Info(message);
            }
            else
            {
                logger?.Info(message);
            }
        }

        /// <summary>True while "Developer diagnostics" is on: tested before building a diagnostic line that costs something, since a logger drops it only once it is built.</summary>
        public static bool Verbose => _verbose;

        /// <summary>The game's context with its logger behind the switch: both entries call this first, so every logger in the mod is this one.</summary>
        public static ModContext Gated(ModContext context)
        {
            _verbose = Settings.DevDiagnostics;
            return context.Logger is GatedLogger ? context : new ModContext(context.ModRootPath, context.ModId, new GatedLogger(context.Logger));
        }

        /// <summary>Brings keys and logging in line with the option. Called when it changes and after each city load.</summary>
        public static void Apply()
        {
            try
            {
                bool on = Settings.DevDiagnostics;
                _verbose = on;
                SessionWatch.Apply(on);   // the session record's lines: a disabled component while off
                foreach (Feature feature in Features.All)
                {
                    foreach (KeyAction action in feature.DiagnosticKeys)
                    {
                        Keys.SetOff(action, !on);
                    }
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }
    }
}
