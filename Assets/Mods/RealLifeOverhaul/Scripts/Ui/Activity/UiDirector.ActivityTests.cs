using System;
using PlayerActivity;
using UI;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The activity kit's tests (Options, Tests): a working day, a class at a desk and a desk session's time in the chair on
    /// the game's own activity panel with the kit over it; a skip of an hour on the game's own time machine under the
    /// kit's veil; and the funeral's card for the player now, whose every way on only closes it. With a screen's switch
    /// off the same test shows the game's own screen, so the switch flipped back is seen too. Each says why it cannot run
    /// when it cannot (the interface off, the fonts loading, no city, something under way, a Going Public session).
    /// </summary>
    public sealed partial class UiDirector
    {
        private const int TestSkipMinutes = 60;

        /// <summary>Options, Tests: a working day on the kit before it starts (its slider, the time machine, Start).</summary>
        public void TestActivityWork() => TestPanel(TestActivity.Kind.Work);

        /// <summary>Options, Tests: a class at a desk on the kit.</summary>
        public void TestActivityClass() => TestPanel(TestActivity.Kind.Class);

        /// <summary>Options, Tests: a desk session's time in the chair: the strip above the status bar at once.</summary>
        public void TestActivityChair() => TestPanel(TestActivity.Kind.Chair);

        private void TestPanel(TestActivity.Kind kind)
        {
            try
            {
                if (!TestCity())
                {
                    return;
                }
                if (PlayerActivityUI.IsPanelOpen || InstanceBehavior<UIs>.Instance.timeMachine.isRunning)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_activity_busy", null, 8f);
                    return;
                }
                var activity = new TestActivity(kind);
                PlayerActivityUI.Show(activity);
                if (!PlayerActivityUI.IsPanelOpen)
                {
                    return;   // the game said why it would not (the player is in a car, has no energy)
                }
                if (kind == TestActivity.Kind.Chair)
                {
                    activity.SetOff();
                }
                Notifications.Show(NotificationType.Info, UiScreenSwitches.On("activity_panel") ? "reallife_ui_test_activity_shown" : "reallife_ui_test_activity_own", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: an hour skipped on the game's own time machine, under the kit's veil, with Cancel.</summary>
        public void TestActivitySkip()
        {
            try
            {
                if (!TestCity())
                {
                    return;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (PlayerActivityUI.IsPanelOpen || uis.timeMachine.isRunning)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_activity_busy", null, 8f);
                    return;
                }
                uis.timeMachine.StartTimeMachine(TimeHelper.Now().AddMinutes(TestSkipMinutes), disableCancel: false);
                Notifications.Show(NotificationType.Info, UiScreenSwitches.On("time_skip") ? "reallife_ui_test_activity_skip_shown" : "reallife_ui_test_activity_own", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the funeral's card for the player now; every way on, and Back, only close it.</summary>
        public void TestActivityFuneral()
        {
            try
            {
                if (!TestCity())
                {
                    return;
                }
                var screen = new FuneralScreen(InstanceBehavior<UIs>.Instance.funeralUI, true);
                Overlay(screen, null, host => screen.Close = () => Destroy(host.gameObject), UiIcon.Flag);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_activity_funeral_shown", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The city's tests may run: the interface on, its fonts in, a city loaded, and no Going Public session (whose clock
        // and panels are shared); a notice says which is wanting.
        private static bool TestCity()
        {
            if (!On)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_off", null, 8f);
                return false;
            }
            if (!UiFonts.Ready)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_fonts", null, 8f);
                return false;
            }
            if (SaveGameManager.Current == null || InstanceBehavior<UIs>.Instance == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_city", null, 8f);
                return false;
            }
            if (GoingPublic.InSession)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_activity_together", null, 8f);
                return false;
            }
            return true;
        }
    }
}
