using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// Meta's part of a city load: one call from RealLifeOverhaulCity when the features have been started, one
    /// when they are unloaded. After the save's mod data has been read it runs the check for leftovers, adds the
    /// guide's phone contact and, the first time on this computer or after a change of version, shows the
    /// welcome panel. It waits for these on a half-second timer and then stops: nothing of it runs afterwards.
    /// </summary>
    public static class MetaCity
    {
        private const float GiveUpAfter = 900f;

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
                MetaUi.Instance.Run(Watch(_generation));
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
    /// The "Developer diagnostics" switch. Off (as the mod ships), the keys the features registered as development
    /// aids (the performance measurements and the F8 and F9 dumps) do nothing, and Info lines do not go to the log;
    /// warnings and errors always do. No feature is edited for this: a key is switched off in <see cref="Keys"/>,
    /// and the one logger every class is handed (<see cref="Gated"/>) drops Info lines while the switch is off.
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

            public void Info(string message)
            {
                if (_verbose)
                {
                    _inner.Info(message);
                }
            }

            public void Warn(string message)
            {
                _inner.Warn(message);
            }

            public void Error(string message)
            {
                _inner.Error(message);
            }

            public void Error(Exception exception)
            {
                _inner.Error(exception);
            }
        }

        private static bool _verbose;

        /// <summary>
        /// True while "Developer diagnostics" is on. For the few places where a diagnostic line costs something to
        /// work out (a self-check that asks the navigation mesh, a text put together on a timer): they test this
        /// first, since the text handed to a logger is built before the logger can drop it.
        /// </summary>
        public static bool Verbose => _verbose;

        /// <summary>
        /// The context the game hands a mod entry, with its logger behind the switch. Both entries call this first
        /// and pass the result on, so every logger in the mod, whoever keeps it and wherever, is this one.
        /// </summary>
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
                SessionWatch.Apply(on);   // the once-a-minute line and the long-frame line (Diagnostics/SessionWatch): a disabled component while off
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
