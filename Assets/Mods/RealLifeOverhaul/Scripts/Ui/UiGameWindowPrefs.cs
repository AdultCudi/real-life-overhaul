using System;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Keeps the places of the windows the player moved (UiWindowPlaces) with the game's settings on this computer (PlayerPrefs), never
    /// in the save and never sent, and the options page's reset. Game side; costs nothing a frame.
    /// </summary>
    internal static class UiWindowPrefs
    {
        private const string Pref = "reallife.window.places";   // not a locale key: a dot, not an underscore
        private const float NoticeSeconds = 5f;

        public static void Start()
        {
            UiWindowPlaces.Load = () => UnityEngine.PlayerPrefs.GetString(Pref, string.Empty);
            UiWindowPlaces.Save = line =>
            {
                try
                {
                    UnityEngine.PlayerPrefs.SetString(Pref, line);
                    UnityEngine.PlayerPrefs.Save();   // on disk now: a crash later keeps the move
                }
                catch (Exception e)
                {
                    UiDirector.Logger?.Error(e);
                }
            };
        }

        /// <summary>The options page's button: every window back where its screen puts it, as the game's own reset does.</summary>
        public static void ResetAll()
        {
            try
            {
                UiWindowPlaces.ResetAll();
                Notifications.Show(NotificationType.Success, "reallife_windows_reset_done", null, NoticeSeconds, null, null, true, false);
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Error(e);
            }
        }
    }
}
