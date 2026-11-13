using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// free; else in a city when a part of the mod is off. Once a start, wherever it was shown.
    /// WHAT IT SAYS is short whatever is off (Core/BuildNoticeRules): the box grows with its text without limit, and the
    /// list of every part that was off on Build 3670 put its heading and buttons off the screen and locked a player out
    /// of the main menu. In the main menu the game reads no key for its question box, so Esc and Enter close the notice
    /// from here.
    /// COST: nothing once the notice has shown, or while the build is right and nothing is off; a half-second timer
    /// while it waits in the main menu, and three key reads a frame while it is up there (none while a text field has the
    /// keyboard, Core/TypingNow).
    /// </summary>
    public static partial class GameBuild
    {
        public static IModLogger Logger;

        private const string TitleKey = "reallife_build_old_title";
        private const string BodyKey = "reallife_build_old_body";
        private const string OffKey = "reallife_build_old_off";
        private const string ShortKey = "reallife_build_old_short";
        private const string OldPartsKey = "reallife_build_old_parts";
        private const string ModTitleKey = "reallife_guard_title_mod";
        private const string NewerKey = "reallife_guard_notice_update";
        private const string PartsKey = "reallife_guard_notice_parts";
        private const string PartsShortKey = "reallife_guard_notice_short";
        private const string MoreKey = "reallife_guard_list_more";
        private const string LogKey = "reallife_guard_list_log";
        private const string OkKey = "reallife_opt_ok";
        private const string TextKey = "reallife_opt_text";   // "{text}": the notice put together here

        private static int _running = -1;
        private static bool _started, _shown;
        private static bool _up;   // the notice is up in the main menu: its keys are read here
        private static readonly Action Closed = () => _up = false;

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

        /// <summary>True while the notice is still due this start: an older game, or a part of the mod off, and not shown yet in the main menu or a city.</summary>
        public static bool CityDue => !_shown && (Of(Running) == BuildKind.Older || Guard.PartsOff);

        /// <summary>For a bug report: whether the notice has shown this start.</summary>
        public static bool NoticeShown => _shown;

        /// <summary>From Meta/MetaCity once the city is ready and quiet: the notice, unless the main menu showed it.</summary>
        public static void ShowInCity()
        {
            if (_shown)
            {
                return;
            }
            _shown = true;
            Show("a save was loaded", true);
        }

        // Once the start screen is up with nothing of the game's over it; a save loaded first gets it in the city.
        private static IEnumerator InMenu()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            float earliest = Time.realtimeSinceStartup + 2f;
            while (!_shown)
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
                _shown = true;
                _up = Show("the main menu", false);
            }
            if (!_up)
            {
                yield break;
            }
            // The start screen reads no key for the question box (only the load screen does, Scenes.MainMenu/LoadGame.Update), and
            // the game clears the selection as the box opens: so Esc and Enter close the notice here, and its button is selected.
            yield return null;
            GameConfirm.SelectConfirm();
            while (_up && !GameConfirm.KnownClosed && !InCity())
            {
                if (!TypingNow.Now && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                {
                    bool closed = GameConfirm.Confirm();
                    Logger?.Info("Game build: the notice in the main menu was " + (closed ? "closed with a key." : "not closed by its key: this game gives the mod no way to press its button."));
                    break;
                }
                yield return null;
            }
            _up = false;
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

        private static bool _testAll;   // the test shows its two notices in turn

        /// <summary>
        /// Options > Tests, two notices in turn: as Build 3675 would show it, with the parts the contract would name there;
        /// then with every feature and every part off, the most there is to count, as Build 3670 had nearly all of them.
        /// </summary>
        public static void TestNotice()
        {
            try
            {
                bool all = _testAll;
                _testAll = !_testAll;
                int example = Math.Min(all ? 3670 : 3675, MadeFor - 1);
                List<string> names = all ? Guard.EveryName() : Guard.PartNames(GameContract.LackedBy3675);
                int features = Guard.FeaturesAll;
                BuildNotice notice = BuildNoticeRules.Compose(BuildKind.Older, example, Meta.Changelog.Version, names, all ? features : 0, features, Words(), GameConfirm.Zoom,
                    GameConfirm.MenuBoxToo);
                bool shown = GameConfirm.TryShow(TitleKey.Localize(), TextKey.Localize(new { text = notice.Body }), null, null, OkKey, null, false, false);
                Logger?.Info("Game build check (test, notice): the notice an older game gets, "
                             + (all ? "with every feature and every part off (" + names.Count + " counted)" : "as Build " + example + " would show it") + " (form " + notice.Form + ", "
                             + notice.Named + " part(s) named, " + notice.Body.Length + " characters): " + (shown ? "PASS." : "not shown, this game has no question box the mod can call.")
                             + " Press again for " + (all ? "the notice as Build 3675 would show it." : "the notice with everything off."));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static BuildNoticeWords Words()
        {
            return new BuildNoticeWords
            {
                OldBody = BodyKey.GetLocalization(),
                OldOff = OffKey.GetLocalization(),
                OldShort = ShortKey.GetLocalization(),
                OldParts = OldPartsKey.GetLocalization(),
                Update = NewerKey.GetLocalization(),
                Parts = PartsKey.GetLocalization(),
                Short = PartsShortKey.GetLocalization(),
                More = MoreKey.GetLocalization(),
                Log = LogKey.GetLocalization()
            };
        }

        // True when the box went up. In the main menu its two buttons say so when they close it, for the keys' wait.
        private static bool Show(string where, bool inCity)
        {
            try
            {
                BuildKind kind = Of(Running);
                bool older = kind == BuildKind.Older;
                List<string> names = Guard.OffNames(older);
                float zoom = GameConfirm.Zoom;
                BuildNotice notice = BuildNoticeRules.Compose(kind, Running, Meta.Changelog.Version, names, Guard.FeaturesOff, Guard.FeaturesAll, Words(), zoom,
                    inCity || GameConfirm.MenuBoxToo);
                string what = older ? "that the game needs updating" : "of the parts of the mod that are off";
                Action closed = inCity ? null : Closed;
                if (GameConfirm.TryShow((older ? TitleKey : ModTitleKey).Localize(), TextKey.Localize(new { text = notice.Body }), closed, closed, OkKey, null, false, false))
                {
                    Logger?.Info("Game build: the notice " + what + " was shown (" + where + "): " + names.Count + " part(s) counted, " + notice.Named + " named, form " + notice.Form
                                 + ", " + notice.Body.Length + " characters at a UI zoom of " + zoom.ToString("0.##", CultureInfo.InvariantCulture) + "; " + Guard.FeaturesOff
                                 + " of " + Guard.FeaturesAll
                                 + " features are off whole. The whole list is in the line 'Game contract: the whole list for this game' above.");
                    return true;
                }
                Logger?.Warn("Game build: the notice " + what + " could not be shown (" + where + "): this game has no question box the mod can call.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return false;
        }
    }
}
