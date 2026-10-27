using System;
using System.Collections;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// Staff and roles as an app on the game's phone: the reference of who runs every business of the mod and how each role is
    /// had, laid out as the game's help screen with its search box (<see cref="StaffView"/>, from Staffing). On and off the phone
    /// like the Guide and the Settings app, and opened by the Guide's page of the same name. Nothing per frame.
    /// </summary>
    public static class StaffApp
    {
        public const string AppId = "RealLifeStaff";

        private const float GiveUpAfter = 600f;   // real seconds of trying to put the app on the phone
        private static readonly string[] AppGlyphs = { "Icon-PersonManage", "Icon-Info" };

        private static PhoneApp _app;
        private static StaffView _view;
        private static int _generation;
        private static bool _failed;
        private static bool _waiting;
        private static string _wanted;

        public static bool Installed => _app != null && _app.Installed;

        public static void Begin(int generation)
        {
            _generation = generation;
            _failed = false;
            _waiting = false;
            MetaUi.Instance.Run(Install(generation));
        }

        public static void End()
        {
            _waiting = false;
            _wanted = null;
            try
            {
                if (_app != null)
                {
                    _app.Remove();
                }
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
                    MetaLog.Warn("the Staff and roles app could not be put on the phone; the Guide still has its page.");
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
            var view = new StaffView();
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_staff_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(120, 200, 150, 255), new Color32(30, 120, 80, 255), 0.42f, "AppIcon-Staff"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _failed = !tryAgain;
                if (_failed)
                {
                    MetaLog.Warn("the game's phone is not built as this version expects, so the Staff and roles app is left off.");
                }
                return;
            }
            _view = view;
            if (Dev.Verbose)
            {
                MetaLog.Info("Meta check (staff app): the Staff and roles app is on the phone: PASS.");
            }
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        /// <summary>Opens the app on an entry ("b:data_centre", "r:data_engineer") or where it was left (null), once a menu of the game's has closed; says so when it cannot.</summary>
        public static void Open(string entry)
        {
            if (!Installed || _view == null)
            {
                Notifications.Show(NotificationType.Info, "reallife_staff_not_here", null, 8f);
                return;
            }
            _wanted = entry;
            if (GuideApp.Blocked)
            {
                Notifications.Show(NotificationType.Info, "reallife_staff_waits", null, 6f);
                if (!_waiting)
                {
                    _waiting = true;
                    MetaUi.Instance.Run(WaitForMenus(_generation));
                }
                return;
            }
            OpenNow();
        }

        private static IEnumerator WaitForMenus(int generation)
        {
            var wait = new WaitForSecondsRealtime(0.25f);
            while (_waiting && generation == _generation && GuideApp.Blocked)
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
                string entry = _wanted;
                _wanted = null;
                _view.Want(entry);
                _app.Open();
                if (Dev.Verbose)
                {
                    MetaLog.Info("Meta check (staff app): opened" + (entry != null ? " on '" + entry + "'" : string.Empty) + ".");
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
            GameSprites.Want(AppGlyphs);
            GameSprites.Want(StaffView.Glyphs);
        }
    }
}
