using System;
using System.Collections;
using RealLifeOverhaul.GameUi;
using UI;
using UI.MiniMenu;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Guide as an app on the game's phone (<see cref="PhoneApp"/>, built as the Planner is): a button on the home
    /// screen and in the full menu's top bar, and a panel in the game's frame (<see cref="GuideView"/>). It is put on the
    /// phone once the city's phone is there (asked again each second until it is, for a few minutes), and taken off when
    /// the city unloads.
    ///
    /// Asked for from the options page, the contact's call, or the welcome notice's click: if the phone is on the home
    /// screen the app opens at once, as the game opens its own; if a menu of the game's is up (the options, the map, a
    /// question) a notification says the Guide opens when the menu is closed, and it does. In the main menu there is no
    /// phone: a notification says where the Guide is. Nothing here runs per frame; the waiting is a quarter-second
    /// timer that lasts only while a request waits.
    /// </summary>
    public static class GuideApp
    {
        public const string AppId = "RealLifeGuide";

        private const float GiveUpAfter = 600f;

        private static PhoneApp _app;
        private static GuideView _view;
        private static int _generation;
        private static bool _failed;
        private static bool _waiting;
        private static string _wantedTopic;

        public static bool Installed => _app != null && _app.Installed;

        /// <summary>The city has loaded: put the app on the phone when the phone is there.</summary>
        public static void Begin(int generation)
        {
            _generation = generation;
            _failed = false;
            _waiting = false;
            PhoneApp.Logger = PhoneApp.Logger ?? MetaLog.Sink;
            AppShell.Logger = AppShell.Logger ?? MetaLog.Sink;
            InfoBox.Logger = InfoBox.Logger ?? MetaLog.Sink;
            GameSprites.Logger = GameSprites.Logger ?? MetaLog.Sink;
            AppIconPainter.Logger = AppIconPainter.Logger ?? MetaLog.Sink;
            MetaUi.Instance.Run(Install(generation));
        }

        /// <summary>The city is unloading: the app comes off the phone.</summary>
        public static void End()
        {
            _waiting = false;
            _wantedTopic = null;
            try
            {
                if (_app != null)
                {
                    _app.Remove();
                }
                GameSprites.Forget();
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
            _app = null;
            _view = null;
        }

        private static IEnumerator Install(int generation)
        {
            var wait = new WaitForSecondsRealtime(1f);
            float giveUp = Time.realtimeSinceStartup + GiveUpAfter;
            while (generation == _generation && _app == null && !_failed && Time.realtimeSinceStartup < giveUp)
            {
                try
                {
                    TryInstall();
                }
                catch (Exception e)
                {
                    _failed = true;
                    MetaLog.Error(e);
                    MetaLog.Warn("the Guide app could not be put on the phone; the Guide is left off.");
                }
                if (_app == null && !_failed)
                {
                    yield return wait;
                }
            }
        }

        private static void TryInstall()
        {
            GameSprites.Want("Icon-Info", "Icon-Edit", "Icon-Notifications");
            var view = new GuideView();
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_guide_app",
                Icon = like => AppIconPainter.Paint(like, GameSprites.Get("Icon-Info") ?? GameSprites.Get("Icon-Edit"), new Color32(255, 170, 60, 255), new Color32(224, 104, 10, 255), 0.42f, "AppIcon-Guide"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _failed = !tryAgain;
                if (_failed)
                {
                    MetaLog.Warn("the game's phone is not built as this version expects, so the Guide app is left off (the Guide's text is still in the mod's files).");
                }
                return;
            }
            _view = view;
            if (Dev.Verbose)
            {
                MetaLog.Info("Meta check (guide app): the Guide is on the phone: PASS.");
            }
        }

        // ---------------------------------------------------------------- asking for it

        /// <summary>One of the game's own screens that the phone's menu must not be opened over, or that hides the HUD.</summary>
        private static bool Blocked => CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen || Scenes.MainMenu.Options.IsVisible;

        /// <summary>Opens the Guide (on a page, or the list for null). Waits for a menu of the game's to close; says so when it cannot.</summary>
        public static void Open(string topicId)
        {
            if (!Installed || _view == null)
            {
                Notifications.Show(NotificationType.Info, "reallife_guide_not_here", null, 8f);
                return;
            }
            _wantedTopic = topicId;
            if (Blocked)
            {
                Notifications.Show(NotificationType.Info, "reallife_guide_waits", null, 6f);
                if (!_waiting)
                {
                    _waiting = true;
                    MetaUi.Instance.Run(WaitForMenus(_generation));
                }
                return;
            }
            OpenNow();
        }

        /// <summary>The welcome notice's click, and the options page's welcome button: the Guide on its "What changed" page.</summary>
        public static void OpenWhatChanged()
        {
            Open("whatsnew");
        }

        private static IEnumerator WaitForMenus(int generation)
        {
            var wait = new WaitForSecondsRealtime(0.25f);
            while (_waiting && generation == _generation && Blocked)
            {
                yield return wait;
            }
            if (_waiting && generation == _generation)
            {
                _waiting = false;
                OpenNow();
            }
            _waiting = false;
        }

        private static void OpenNow()
        {
            try
            {
                string topic = _wantedTopic;
                _wantedTopic = null;
                _view.Want(topic);
                _app.Open();
                if (Dev.Verbose)
                {
                    MetaLog.Info("Meta check (guide app): opened" + (topic != null ? " on '" + topic + "'" : string.Empty) + ".");
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }
    }
}
