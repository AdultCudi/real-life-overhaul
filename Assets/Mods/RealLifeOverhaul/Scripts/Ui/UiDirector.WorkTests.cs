using RealLifeOverhaul.Careers;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Hustles;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: Career, Campus, Open Roles and the Marketplace on the kit, and each switch flipped back to the page as it was. A
    /// notice says why one cannot open. The switch test changes only the running value.
    /// </summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] WorkSwitchIds = { "career", "campus", "open_roles", "marketplace" };

        /// <summary>Options, Tests: Career on the kit.</summary>
        public void TestCareer() => OpenWork(CareersDirector.CareerAppId);

        /// <summary>Options, Tests: Campus on the kit.</summary>
        public void TestCampus() => OpenWork(CareersDirector.CampusAppId);

        /// <summary>Options, Tests: Open Roles on the kit.</summary>
        public void TestRoles() => OpenWork(TechDirector.RolesAppId);

        /// <summary>Options, Tests: the Marketplace on the kit.</summary>
        public void TestMarketplace() => OpenWork(HustleDirector.AppId);

        /// <summary>Options, Tests: each work app's switch flipped, then Career's put back on.</summary>
        public void TestWorkSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                for (int i = 0; i < WorkSwitchIds.Length; i++)
                {
                    string id = WorkSwitchIds[i];
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(id), !UiScreenSwitches.On(id));
                }
                for (int i = 0; i < WorkSwitchIds.Length; i++)
                {
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(WorkSwitchIds[i]), true);
                }
                Notifications.Show(NotificationType.Info, "reallife_ui_test_work_switch", null, 8f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OpenWork(string id)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                if (!PhoneApp.OpenById(id))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                }
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
