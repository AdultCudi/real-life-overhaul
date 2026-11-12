using System;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class UiDirector
    {
        /// <summary>Options, Tests: Mod settings on the kit, over the screen.</summary>
        public void TestSettings()
        {
            OpenSettings(null);
        }

        /// <summary>Options, Tests: Mod settings with a search already typed.</summary>
        public void TestSettingsSearch()
        {
            OpenSettings("police");
        }

        /// <summary>Options, Tests: the Guide on the kit.</summary>
        public void TestGuide()
        {
            OpenKit(GuideScreen.Make(), "reallife_guide_app", UiIcon.Info);
        }

        /// <summary>Options, Tests: Staff and roles on the kit.</summary>
        public void TestStaff()
        {
            OpenKit(StaffScreen.Make(), "reallife_staff_app", UiIcon.People);
        }

        /// <summary>Options, Tests: the bug report card, which saves a file and sends nothing.</summary>
        public void TestBugReport()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                BugReportBox.TryShow(UiText.T("reallife_bug_where_city"), 2400000L);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the save tools' question, without running the tool.</summary>
        public void TestSaveTools()
        {
            OpenKit(new SaveToolsScreen(), "reallife_meta_remove_title", UiIcon.Warning);
        }

        /// <summary>Options, Tests: each of these screens' switches off and back on, ending on the kit.</summary>
        public void TestSettingsSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                string[] ids = { SettingsScreen.ScreenId, GuideScreen.ScreenId, StaffScreen.ScreenId, BugReportScreen.ScreenId, SaveToolsScreen.ScreenId };
                for (int i = 0; i < ids.Length; i++)
                {
                    string option = UiScreenSwitches.OptionOf(ids[i]);
                    bool was = UiScreenSwitches.On(ids[i]);
                    UiScreenSwitches.Set(option, false);
                    UiScreenSwitches.Set(option, was);
                }
                Notifications.Show(NotificationType.Info, "reallife_ui_test_settings_back", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OpenSettings(string query)
        {
            SettingsScreen screen = (SettingsScreen)SettingsScreen.Make();
            if (!string.IsNullOrEmpty(query))
            {
                screen.Typed(query);
            }
            OpenKit(screen, "reallife_set_app", UiIcon.Gear);
        }

        private void OpenKit(IUiScreen screen, string titleKey, UiIcon icon)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                Overlay(screen, UiText.T(titleKey), host =>
                {
                    host.Hide();
                    host.gameObject.SetActive(false);
                    Destroy(host.gameObject);
                }, icon);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
