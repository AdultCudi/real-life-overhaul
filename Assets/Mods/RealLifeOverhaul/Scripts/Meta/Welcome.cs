using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Meta
{
    /// <summary>The welcome: the game's notification on the first city load and after each new version (per install); a click opens the Guide on "What changed".</summary>
    public static class Welcome
    {
        private const string SeenId = "meta_seen_release";
        private const string DevelopmentSeenId = "meta_seen_version";   // written by the development builds: deleted, never read

        /// <summary>True when this version's notes have not been opened yet, so the Guide marks the newest one.</summary>
        internal static bool Unread => SeenVersion != Changelog.Shown;

        /// <summary>The version whose welcome the player has seen, or null.</summary>
        private static string SeenVersion
        {
            get
            {
                string key = Settings.PrefsKey(SeenId);
                string seen = key == null ? null : UnityEngine.PlayerPrefs.GetString(key, string.Empty);
                return string.IsNullOrEmpty(seen) ? null : seen;
            }
        }

        private static bool _twiceShown;

        /// <summary>The beta runs while a public copy of the mod is switched on too: its welcome says so, at every start until that copy is off.</summary>
        private static bool Twice => Changelog.IsBeta && RealLifeOverhaulInit.TwoCopies && !_twiceShown;

        private const string PropsNote = "reallife_props_note_failed";
        private static bool _propsShown;

        /// <summary>The props bundle failed or is missing a part (a second copy, an unfinished download, another game build): said once a session, with what to check.</summary>
        private static bool PropsFailed => PropLibrary.Trouble != null && !_propsShown;

        /// <summary>True when this version's welcome has not been shown yet (a beta's own tag counts as a version), a beta must say two copies are on, or the props failed to load.</summary>
        public static bool Due => Settings.ModId != null && (SeenVersion != Changelog.Shown || Twice || PropsFailed);

        /// <summary>Shows the welcome notice and notes the version as seen; <paramref name="asked"/> (from the options page) opens the Guide on "What changed" instead.</summary>
        public static void Open(bool asked)
        {
            try
            {
                string seen = SeenVersion;
                string key = Settings.PrefsKey(SeenId);
                if (key != null)
                {
                    UnityEngine.PlayerPrefs.DeleteKey(Settings.PrefsKey(DevelopmentSeenId));
                    UnityEngine.PlayerPrefs.SetString(key, Changelog.Shown);
                    UnityEngine.PlayerPrefs.Save();
                }
                if (asked)
                {
                    GuideApp.OpenWhatChanged();
                    return;
                }
                bool update = seen != null && Changelog.Compare(seen, Changelog.Version) < 0;
                var data = new Dictionary<string, string>(1) { { "version", Changelog.Shown } };
                string notice = update ? "reallife_meta_welcome_note_update" : "reallife_meta_welcome_note_first";
                bool welcome = seen != Changelog.Shown;   // the props notice alone brings no welcome again
                if (Changelog.IsBeta)
                {
                    bool twice = Twice;
                    notice = twice ? "reallife_beta_note_twice" : "reallife_beta_note";
                    _twiceShown = _twiceShown || twice;
                    welcome = welcome || twice;
                }
                if (welcome)
                {
                    Notifications.Show(NotificationType.Info, notice, data, 14f, notice, Guard.Wrap(GuideApp.OpenWhatChanged), true, false);
                    MetaLog.Info("welcome shown (" + (update ? "after " + seen : "first run") + ").");
                }
                if (PropsFailed)
                {
                    // Bus stops, shelters and the other modelled things are drawn as plain shapes: said once, and the cause is in Player.log.
                    _propsShown = true;
                    Notifications.Show(NotificationType.Warning, PropsNote, null, 20f, PropsNote, null, true, false);
                    MetaLog.Info("props notice shown: " + PropLibrary.Trouble + ".");
                }
                if (Dev.Verbose)
                {
                    MetaLog.Info("Meta check (welcome notice): shown as the game's own notification with a click that opens the Guide on 'What changed': PASS.");
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }
    }
}
