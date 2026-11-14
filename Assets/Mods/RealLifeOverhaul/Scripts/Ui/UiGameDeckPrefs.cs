using System;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Keeps the player's bar and favourites (UiAppDeck) with the game's settings on this computer (PlayerPrefs), never in the
    /// save and never sent: a layout is the person's, as the phone's home screen and the windows' places are, so it holds across
    /// saves and each Going Public player has their own. And the options page's reset. Game side; costs nothing a frame.
    /// </summary>
    internal static class UiGameDeckPrefs
    {
        private const string Pref = "reallife.apps.deck";   // not a locale key: a dot, not an underscore
        private const float NoticeSeconds = 5f;

        public static void Start()
        {
            UiAppDeck.Load = () => UnityEngine.PlayerPrefs.GetString(Pref, string.Empty);
            UiAppDeck.Save = line =>
            {
                try
                {
                    UnityEngine.PlayerPrefs.SetString(Pref, line);
                    UnityEngine.PlayerPrefs.Save();   // on disk now: a crash later keeps the choice
                }
                catch (Exception e)
                {
                    UiDirector.Logger?.Error(e);
                }
            };
            UiAppDeck.Forget();   // read again from this computer, in case something asked for the deck before the store was wired
        }

        /// <summary>The options page's button: the bar the phone's first page again and no favourites.</summary>
        public static void ResetAll()
        {
            try
            {
                UiAppDeck.Current.Restore(string.Empty);
                UiAppDeck.Current.Commit();
                UiShellApps.Rearrange();
                Notifications.Show(NotificationType.Success, "reallife_ui_apps_reset_done", null, NoticeSeconds, null, null, true, false);
            }
            catch (Exception e)
            {
                UiDirector.Logger?.Error(e);
            }
        }
    }
}
