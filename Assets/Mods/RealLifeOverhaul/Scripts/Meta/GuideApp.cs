using System;
using System.Collections;
using RealLifeOverhaul.GameUi;
using UI.MiniMenu;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Guide as an app on the game's phone (<see cref="PhoneApp"/>), drawn by <see cref="GuideView"/>: on the phone while a
    /// city is loaded; asked for over a menu of the game's, it opens when the menu closes. Nothing per frame.
    /// </summary>
    public static class GuideApp
    {
        public const string AppId = "RealLifeGuide";

        private const float GiveUpAfter = 600f;   // real seconds of trying to put the app on the phone

        private static PhoneApp _app;
        private static GuideView _view;
        private static int _generation;
        private static bool _failed;
        private static bool _waiting;
        private static string _wantedTopic;

        public static bool Installed => _app != null && _app.Installed;

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
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new GuideView();
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_guide_app",
                Icon = like => AppIconPainter.Paint(like, GameSprites.Get("Icon-Info") ?? GameSprites.Get("Icon-Edit"),
                    new Color32(255, 170, 60, 255), new Color32(224, 104, 10, 255), 0.42f, "AppIcon-Guide"),
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

        /// <summary>One of the game's own screens that the phone's menu must not be opened over, or that hides the HUD.</summary>
        internal static bool Blocked => CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen || Scenes.MainMenu.Options.IsVisible;

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

        /// <summary>The game's pictures this asks for, named before the one look behind the loading screen (GameSprites.Prewarm).</summary>
        internal static void WantPictures()
        {
            GameSprites.Want("Icon-Info", "Icon-Edit", "Icon-Notifications");
        }
    }
}
