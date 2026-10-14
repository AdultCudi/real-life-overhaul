using System;
using System.Collections;
using System.Runtime.CompilerServices;
using BAModAPI;
using Localizor;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's part of the build check (Core/GameBuild.cs is the game-free part, which NavCheck compiles):
    ///   at game start   the running game's build is read (GameVersion.GetCurrent().buildNumber, as the game's main menu
    ///                   reads it for "Loaded Big Ambitions (Build N)"), and one line goes to Player.log when it is not
    ///                   the build this version is made for: a warning for an older game, and for a newer one
    ///   the main menu   for an older game, the game's own question box once its start screen is up and nothing of the
    ///                   game's is on it: the mod needs the current game, and how to update it in Steam
    ///   a save loaded   the same, once a session, when the city is ready (from Meta/MetaCity)
    /// The words are the player's language (reallife_build_old_*). Nothing of it runs once the notice has been shown, or
    /// at all on the build the mod is made for; the wait in the main menu is a half-second timer.
    /// </summary>
    public static partial class GameBuild
    {
        public static IModLogger Logger;

        private const string TitleKey = "reallife_build_old_title";
        private const string BodyKey = "reallife_build_old_body";
        private const string OkKey = "reallife_opt_ok";

        private static int _running = -1;
        private static bool _started, _menuShown, _cityShown;

        /// <summary>The running game's build (GameVersion.buildNumber), read once; 0 when it could not be read.</summary>
        public static int Running
        {
            get
            {
                if (_running < 0)
                {
                    try
                    {
                        _running = Math.Max(0, ReadRunning());
                    }
                    catch (Exception)
                    {
                        _running = 0;
                    }
                }
                return _running;
            }
        }

        // The game's own version object, as MainMenuController reads it: named only here, so that a game without it
        // only leaves the build unknown.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadRunning()
        {
            GameVersion current = GameVersion.GetCurrent();
            return current != null ? current.buildNumber : 0;
        }

        /// <summary>
        /// Once at game start (RealLifeOverhaulInit): the line in Player.log, and for an older game the notice in the main
        /// menu as soon as its start screen is free.
        /// </summary>
        public static void Start(IModLogger logger)
        {
            Logger = logger;
            if (_started)
            {
                return;
            }
            _started = true;
            try
            {
                int running = Running;
                string line = Line(running, Meta.Changelog.Version);
                if (line != null)
                {
                    Logger?.Warn(line);
                }
                else
                {
                    Logger?.Info("Game build check: Build " + running + ", the build Real Life Overhaul " + Meta.Changelog.Version + " is made for: PASS.");
                }
                if (Of(running) == BuildKind.Older)
                {
                    Meta.MetaUi.Instance.Run(InMenu());
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>True while the notice is still to be shown in a city this session (an older game, not shown in one yet).</summary>
        public static bool CityDue => !_cityShown && Of(Running) == BuildKind.Older;

        /// <summary>From Meta/MetaCity once the city is ready and quiet: the notice, once a session.</summary>
        public static void ShowInCity()
        {
            _cityShown = true;
            _menuShown = true;   // (back in the main menu it is not shown again)
            Show("a save was loaded");
        }

        // The main menu's notice: once its start screen is up with nothing of the game's over it (its welcome, the
        // question of data, the system's requirements), and before any save is loaded; a save loaded first gets it there.
        private static IEnumerator InMenu()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            float earliest = Time.realtimeSinceStartup + 2f;
            while (!_menuShown)
            {
                yield return wait;
                if (InCity())
                {
                    yield break;
                }
                if (Time.realtimeSinceStartup < earliest || GameConfirm.IsOpen || !GameConfirm.Ready || !StartScreen())
                {
                    continue;
                }
                _menuShown = true;
                Show("the main menu");
            }
        }

        private static bool InCity()
        {
            try
            {
                return Loading();
            }
            catch (Exception e) when (GameMembers.Missing(e))
            {
                GameMembers.DoesWithout(e, "knowing that a save is being loaded while the notice of the game's build waits");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Loading()
        {
            return SaveGameManager.Current != null || LoadScene.isLoading;
        }

        // The main menu's start screen is up: on a game where that cannot be read, true (the wait has been had).
        private static bool StartScreen()
        {
            try
            {
                return StartViewUp();
            }
            catch (Exception e) when (GameMembers.Missing(e))
            {
                GameMembers.DoesWithout(e, "waiting for the main menu's start screen before the notice of the game's build");
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool StartViewUp()
        {
            MainMenuController menu = InstanceBehavior<MainMenuController>.Instance;
            return menu != null && menu.startView != null && menu.startView.activeInHierarchy;
        }

        /// <summary>
        /// Options > Tests: the notice as a game older than the mod shows it (as on Build 3675, the build a player had), on the
        /// game the mod is made for, wherever the player is.
        /// </summary>
        public static void TestNotice()
        {
            try
            {
                int example = Math.Min(3675, MadeFor - 1);
                var body = BodyKey.Localize(new { version = Meta.Changelog.Version, made = MadeFor.ToString(), build = example.ToString() });
                bool shown = GameConfirm.TryShow(TitleKey.Localize(), body, null, null, OkKey, null, false, false);
                Logger?.Info("Game build check (test, notice): the notice an older game gets, as Build " + example + " would show it: "
                             + (shown ? "PASS." : "not shown, this game has no question box the mod can call."));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static void Show(string where)
        {
            try
            {
                var body = BodyKey.Localize(new { version = Meta.Changelog.Version, made = MadeFor.ToString(), build = Running.ToString() });
                if (GameConfirm.TryShow(TitleKey.Localize(), body, null, null, OkKey, null, false, false))
                {
                    Logger?.Info("Game build: the notice that the game needs updating was shown (" + where + ").");
                }
                else
                {
                    Logger?.Warn("Game build: the notice that the game needs updating could not be shown (" + where + "): this game has no question box the mod can call.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
