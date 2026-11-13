using System;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    public sealed partial class UiDirector
    {
        /// <summary>
        /// Options, Tests: five faults of the interface's own part, as five of a screen's would be, so that the guard switches it off
        /// as it does in play: its notice, every screen given back and what it held let go of. Press Report a bug after it: the
        /// game's own text box asks, and the screen takes clicks. The part stays off until the game is started again, which the
        /// test says instead when it is off already. Cost: only when pressed.
        /// </summary>
        public void TestGuardOff()
        {
            try
            {
                if (Stopped)
                {
                    Notifications.Show(NotificationType.Info, "reallife_ui_test_guard_stopped", null, 8f);
                    return;
                }
                for (int i = 0; i < FeatureGuard.Most; i++)
                {
                    try
                    {
                        throw new InvalidOperationException("a test fault of the RLO interface's part (options page, test tools), " + (i + 1) + " of " + FeatureGuard.Most);
                    }
                    catch (Exception e)
                    {
                        Guard.Fail(FrameGuard, e, this);
                    }
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
