using System;
using System.Collections;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The mod's settings as an app on the game's phone: the game's help screen in dark colours (<see cref="SettingsView"/>),
    /// written through the Mods tab's own rows; on and off the phone like the Guide.
    /// </summary>
    public static class SettingsApp
    {
        public const string AppId = "RealLifeSettings";

        private const float GiveUpAfter = 600f;   // real seconds of trying to put the app on the phone
        private static readonly string[] AppGlyphs = { "Icon-Settings", "Icon-Edit" };

        private static PhoneApp _app;
        private static SettingsView _view;
        private static int _generation;
        private static bool _failed;
        private static bool _waiting;
        private static string _wanted;

        public static bool Installed => _app != null && _app.Installed;

        /// <summary>For the Mods tab's button, which is on the page only while the app is on the phone.</summary>
        public static readonly Func<bool> IsInstalled = () => Installed;

        public static void Begin(int generation)
        {
            _generation = generation;
            _failed = false;
            _waiting = false;
            Settings.Changed -= ValuesChanged;
            Settings.Changed += ValuesChanged;
            MetaUi.Instance.Run(Install(generation));
        }

        public static void End()
        {
            _waiting = false;
            _wanted = null;
            Settings.Changed -= ValuesChanged;
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

        // A preset, an import or a reset read every option again: the page shows the new values if it is open.
        private static void ValuesChanged()
        {
            try
            {
                _view?.Changed();
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
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
                    MetaLog.Warn("the Settings app could not be put on the phone; the options are on the Mods tab as ever.");
                }
                if (_app == null && !_failed)
                {
                    yield return wait;
                }
            }
        }

        private static void TryInstall()
        {
            if (!OptionsLayout.AppReady)
            {
                _failed = true;
                MetaLog.Warn("the options page could not be put in order, so the Settings app is left off; the options are on the Mods tab.");
                return;
            }
            GameSprites.Want(AppGlyphs);
            GameSprites.Want(SettingsView.Glyphs);
            var view = new SettingsView();
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_set_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(170, 180, 196, 255), new Color32(58, 70, 92, 255), 0.42f, "AppIcon-Settings"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _failed = !tryAgain;
                if (_failed)
                {
                    MetaLog.Warn("the game's phone is not built as this version expects, so the Settings app is left off; the options are on the Mods tab.");
                }
                return;
            }
            _view = view;
            if (Dev.Verbose)
            {
                MetaLog.Info("Meta check (settings app): the Settings app is on the phone: PASS.");
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

        /// <summary>Opens the app on a section ("s:interiors"), an option (its id) or where it was left (null), once a menu of the game's has closed; says so when it cannot.</summary>
        public static void Open(string target)
        {
            if (!Installed || _view == null)
            {
                Notifications.Show(NotificationType.Info, "reallife_set_not_here", null, 8f);
                return;
            }
            _wanted = target;
            if (GuideApp.Blocked)
            {
                Notifications.Show(NotificationType.Info, "reallife_set_waits", null, 6f);
                if (!_waiting)
                {
                    _waiting = true;
                    MetaUi.Instance.Run(WaitForMenus(_generation));
                }
                return;
            }
            OpenNow();
        }

        /// <summary>A button at the foot of another app's page that opens this app on that page's section (BizMan's Interior tab); nothing while the app is off the phone.</summary>
        public static void Link(InfoBox box, string sectionId)
        {
            if (box == null || !Installed)
            {
                return;
            }
            string target = "s:" + sectionId;
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_set_link", () => UiClone.Safe(() => Open(target)));
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
                string target = _wanted;
                _wanted = null;
                _view.Want(target);
                _app.Open();
                if (Dev.Verbose)
                {
                    MetaLog.Info("Meta check (settings app): opened" + (target != null ? " on '" + target + "'" : string.Empty) + ".");
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }
    }
}
