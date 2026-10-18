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

        /// <summary>True when this version's welcome has not been shown yet.</summary>
        public static bool Due => Settings.ModId != null && SeenVersion != Changelog.Version;

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
                    UnityEngine.PlayerPrefs.SetString(key, Changelog.Version);
                    UnityEngine.PlayerPrefs.Save();
                }
                if (asked)
                {
                    GuideApp.OpenWhatChanged();
                    return;
                }
                bool update = seen != null && Changelog.Compare(seen, Changelog.Version) < 0;
                var data = new Dictionary<string, string>(1) { { "version", Changelog.Version } };
                string notice = update ? "reallife_meta_welcome_note_update" : "reallife_meta_welcome_note_first";
                Notifications.Show(NotificationType.Info, notice, data, 14f, notice, Guard.Wrap(GuideApp.OpenWhatChanged), true, false);
                MetaLog.Info("welcome shown (" + (update ? "after " + seen : "first run") + ").");
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
