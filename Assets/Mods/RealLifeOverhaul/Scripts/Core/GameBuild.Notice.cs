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
    /// The game-touching part of the build check, and the one notice of what this game lacks, in the game's question
    /// box: a Player.log line at game start; for an older game, the notice in the main menu once its start screen is
    /// free; else once a session in a city when a part of the mod is off.
    /// COST: nothing once the notice has shown, or while the build is right and nothing is off; a half-second timer
    /// while it waits in the main menu.
    /// </summary>
    public static partial class GameBuild
    {
        public static IModLogger Logger;

        private const string TitleKey = "reallife_build_old_title";
        private const string BodyKey = "reallife_build_old_body";
        private const string PartsKey = "reallife_build_old_parts";
        private const string ModTitleKey = "reallife_guard_title_mod";
        private const string NewerKey = "reallife_guard_notice_update";
        private const string OffKey = "reallife_guard_notice_parts";
        private const string OkKey = "reallife_opt_ok";
        private const string TextKey = "reallife_opt_text";   // "{text}": the notice put together here

        private static int _running = -1;
        private static bool _started, _menuShown, _cityShown;

        /// <summary>The running game's build, read once; 0 when it could not be read.</summary>
        public static int Running
        {
            get
            {
                if (_running < 0)
                {
                    try
                    {
                        _running = GameContract.VersionCurrent.Missing || GameContract.VersionBuild.Missing ? 0 : Math.Max(0, ReadRunning());
                    }
                    catch (Exception)
                    {
                        _running = 0;
                    }
                }
                return _running;
            }
        }

        // Named only here, so a game without it only leaves the build unknown.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ReadRunning()
        {
            GameVersion current = GameVersion.GetCurrent();
            return current != null ? current.buildNumber : 0;
        }

        /// <summary>Once at game start, from Core/Guard after the contract: the log line, and for an older game the menu's notice.</summary>
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

        /// <summary>True while the notice is still due in a city this session: an older game, or a part of the mod off.</summary>
        public static bool CityDue => !_cityShown && (Of(Running) == BuildKind.Older || Guard.PartsOff);

        /// <summary>For a bug report: whether the notice has shown this session.</summary>
        public static bool NoticeShown => _menuShown || _cityShown;

        /// <summary>From Meta/MetaCity once the city is ready and quiet: the notice, once a session.</summary>
        public static void ShowInCity()
        {
            _cityShown = true;
            _menuShown = true;   // not shown again back in the main menu
            Show("a save was loaded");
        }

        // Once the start screen is up with nothing of the game's over it; a save loaded first gets it in the city.
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
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "knowing that a save is being loaded while the notice of the game's build waits");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Loading()
        {
            return SaveGameManager.Current != null || LoadScene.isLoading;
        }

        // True also where it cannot be read: the wait has been had.
        private static bool StartScreen()
        {
            if (GameContract.MenuStartView.Missing)
            {
                return true;
            }
            try
            {
                return StartViewUp();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "waiting for the main menu's start screen before the notice of the game's build");
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool StartViewUp()
        {
            MainMenuController menu = InstanceBehavior<MainMenuController>.Instance;
            return menu != null && menu.startView != null && menu.startView.activeInHierarchy;
        }

        /// <summary>Options > Tests: the notice as Build 3675 would show it, with the parts the contract would name there.</summary>
        public static void TestNotice()
        {
            try
            {
                int example = Math.Min(3675, MadeFor - 1);
                string body = Older(example) + "\n\n" + PartsKey.GetLocalization().Replace("{parts}", Guard.PartNames(GameContract.LackedBy3675));
                bool shown = GameConfirm.TryShow(TitleKey.Localize(), TextKey.Localize(new { text = body }), null, null, OkKey, null, false, false);
                Logger?.Info("Game build check (test, notice): the notice an older game gets, as Build " + example + " would show it: "
                             + (shown ? "PASS." : "not shown, this game has no question box the mod can call."));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static string Older(int build)
        {
            return BodyKey.GetLocalization().Replace("{version}", Meta.Changelog.Version).Replace("{made}", MadeFor.ToString()).Replace("{build}", build.ToString());
        }

        private static void Show(string where)
        {
            try
            {
                bool older = Of(Running) == BuildKind.Older;
                string body;
                if (older)
                {
                    body = Older(Running);
                    string parts = Guard.PartsNamed(true);
                    if (parts.Length > 0)
                    {
                        body += "\n\n" + PartsKey.GetLocalization().Replace("{parts}", parts);
                    }
                }
                else
                {
                    body = (Of(Running) == BuildKind.Newer ? NewerKey : OffKey).GetLocalization().Replace("{parts}", Guard.PartsNamed(false));
                }
                if (GameConfirm.TryShow((older ? TitleKey : ModTitleKey).Localize(), TextKey.Localize(new { text = body }), null, null, OkKey, null, false, false))
                {
                    Logger?.Info("Game build: the notice " + (older ? "that the game needs updating" : "of the parts of the mod that are off") + " was shown (" + where + ").");
                }
                else
                {
                    Logger?.Warn("Game build: the notice " + (older ? "that the game needs updating" : "of the parts of the mod that are off") + " could not be shown (" + where
                                 + "): this game has no question box the mod can call.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
