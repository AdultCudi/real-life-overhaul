using System;
using System.Collections;
using System.Collections.Generic;
using UI;
using UI.Notification;
using UI.Smartphone.Apps.MyEmployees;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface's tests of My Employees (Options, Tests): the app on the kit, a person's page opened through the
    /// game's own row (the first employee, or the first candidate when nobody is hired yet), and the app switched back to
    /// the game's own and on again. Each says in a notice exactly why it cannot run when it cannot. Nothing is saved: the
    /// switch test changes the running value only, and the next start reads the page's.
    /// </summary>
    public sealed partial class UiDirector
    {
        private const string StaffApp = "MyEmployees";   // the game's name for the app (AppName.MyEmployees), as its phone opens it

        /// <summary>Options, Tests: My Employees, which the kit draws while its switch is on.</summary>
        public void TestEmployees()
        {
            try
            {
                if (OpenStaff() != null)
                {
                    Slow();
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: My Employees open on a person's page, through the game's own row as a click opens it.</summary>
        public void TestEmployeePage()
        {
            try
            {
                MyEmployees app = OpenStaff();
                if (app != null)
                {
                    Slow();
                    StartCoroutine(Guard.Routine(PersonSoon(app)));
                }
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: My Employees switched to the game's own for this run, or back onto the kit.</summary>
        public void TestEmployeesSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                bool on = !UiScreenSwitches.On(EmployeesKit.StaffId);
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(EmployeesKit.StaffId), on);
                Notifications.Show(NotificationType.Info, on ? "reallife_ui_test_employees_kit" : "reallife_ui_test_employees_game", null, 8f);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        // The phone's My Employees opened as its button opens it, or a notice that says why it cannot be.
        private MyEmployees OpenStaff()
        {
            if (!Ready())
            {
                return null;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.smartphoneUI == null || uis.fullMenu == null || uis.fullMenu.myEmployees == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                return null;
            }
            EmployeesKit.Install();
            uis.smartphoneUI.OpenApp(StaffApp);
            return uis.fullMenu.myEmployees;
        }

        // Once the game has loaded its table (a frame after the tab opens), its first person through the game's own row event.
        private IEnumerator PersonSoon(MyEmployees app)
        {
            yield return new WaitForSecondsRealtime(MenuSettles);
            List<EmployeeModel> staff = app.employeeScrollerController.data;
            if (staff != null && staff.Count > 0 && staff[0] != null)
            {
                app.employeeScrollerController.onRowSelected.Invoke(staff[0]);
                yield break;
            }
            app.ChangeTab("Candidates");
            yield return new WaitForSecondsRealtime(MenuSettles);
            List<CandidateModel> candidates = app.candidateScrollerController.data;
            if (candidates != null && candidates.Count > 0 && candidates[0] != null)
            {
                app.candidateScrollerController.onRowSelected.Invoke(candidates[0]);
                yield break;
            }
            Notifications.Show(NotificationType.Warning, "reallife_ui_test_employees_none", null, 8f);
        }
    }
}
