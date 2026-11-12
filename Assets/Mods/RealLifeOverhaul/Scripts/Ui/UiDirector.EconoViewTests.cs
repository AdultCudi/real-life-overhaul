using System;
using System.Collections;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of EconoView (Options, Tests): the app on the kit, the app on its loans tab (as its tab
    /// shows it, once the full menu has opened it), and the app switched back to the game's own and on again. Each says in
    /// a notice exactly why it cannot run when it cannot. Nothing is saved: the switch test changes the running value only,
    /// and the next start reads the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        private const string EconoApp = "EconoView";        // the game's name for the app (AppName.EconoView), as its phone opens it
        private const float EconoSettles = 0.4f;            // seconds of real time for the full menu to open the app before its tab is chosen

        /// <summary>Options, Tests: EconoView, which the kit draws while its switch is on.</summary>
        public void TestEconoView()
        {
            try
            {
                if (OpenEcono())
                {
                    Slow();
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: EconoView open on its loans, as its tab opens them.</summary>
        public void TestEconoViewLoans()
        {
            try
            {
                if (OpenEcono())
                {
                    Slow();
                    StartCoroutine(Guard.Routine(EconoLoansSoon()));
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: EconoView switched to the game's own for this run, or back onto the kit.</summary>
        public void TestEconoViewSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(EconoViewKit.Id);
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(EconoViewKit.Id), on);
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_econoview_kit" : "reallife_ui_test_econoview_game", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The phone's EconoView opened as its button opens it, or a notice that says why it cannot be.
        private bool OpenEcono()
        {
            if (!Ready())
            {
                return false;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.smartphoneUI == null || uis.fullMenu == null || uis.fullMenu.econoView == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                return false;
            }
            EconoViewKit.Install();
            uis.smartphoneUI.OpenApp(EconoApp);
            return true;
        }

        // Once the full menu has opened the app and the kit has made its screen, its loans tab.
        private IEnumerator EconoLoansSoon()
        {
            yield return new WaitForSecondsRealtime(EconoSettles);
            EconoViewScreen screen = EconoViewKit.Screen;
            if (screen == null || !EconoViewKit.Showing)
            {
                Notifications.Show(NotificationType.Warning, UiScreenSwitches.On(EconoViewKit.Id) ? "reallife_ui_test_phone" : "reallife_ui_test_econoview_game", null, 8f);
                yield break;
            }
            screen.ShowLoans();
        }
    }
}
