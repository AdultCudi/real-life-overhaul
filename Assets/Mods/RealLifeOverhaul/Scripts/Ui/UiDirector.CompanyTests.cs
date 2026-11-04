using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: the Company app on the kit, a manager's page, a week's rota, and the app's switch flipped back to the old pages and on again. A notice says why one cannot
    /// open (the economy is not running, or the kit's switch is off). The switch test changes only the running value.
    /// </summary>
    public sealed partial class UiDirector
    {
        /// <summary>Options, Tests: the Company app on the kit, on its overview.</summary>
        public void TestCompany() => OpenCompany(CompanyDirector.TabOverview, null);

        /// <summary>Options, Tests: the Company app on the first manager's page, or the Managers tab where there is none.</summary>
        public void TestCompanyManager()
        {
            CompanyDirector d = CompanyDirector.Instance;
            string pick = null;
            for (int i = 0; d != null && d.Running && i < d.Managed.Count && pick == null; i++)
            {
                pick = d.Managed[i].Own != null ? "mp:" + d.Managed[i].Place.Place : null;
            }
            OpenCompany(CompanyDirector.TabManagers, pick);
        }

        /// <summary>Options, Tests: the Company app on the first place's rota.</summary>
        public void TestCompanyRota()
        {
            CompanyDirector d = CompanyDirector.Instance;
            string pick = d != null && d.Running && d.RotaPlaces.Count > 0 ? "rota:" + d.RotaPlaces[0].Place : null;
            OpenCompany(CompanyDirector.TabRota, pick);
        }

        /// <summary>Options, Tests: the Company app's switch flipped between the kit and the old pages (a notice says which stands now).</summary>
        public void TestCompanySwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf("company"), !UiScreenSwitches.On("company"));   // each press flips it; the next open shows the page it stands for
                Notifications.Show(NotificationType.Info, UiScreenSwitches.On("company") ? "reallife_ui_test_company_on" : "reallife_ui_test_company_off_page", null, 8f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OpenCompany(int tab, string pick)
        {
            try
            {
                CompanyDirector d = CompanyDirector.Instance;
                if (!Ready())
                {
                    return;
                }
                if (d == null || !d.Running)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_company_off", null, 8f);
                    return;
                }
                d.Open(tab, pick);   // the app opens on the tab and the pick; the kit takes the same request in its own pages
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
