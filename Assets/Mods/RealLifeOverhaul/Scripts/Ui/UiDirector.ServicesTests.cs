using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: Services on the kit, its Sell page, and the screen's switch flipped back to the page as it was. A notice says why one cannot
    /// open. The switch test changes only the running value.
    /// </summary>
    public sealed partial class UiDirector
    {
        /// <summary>Options, Tests: Services on the kit, on its Cleaning tab.</summary>
        public void TestServices() => OpenServices(ServicesDirector.TabCleaning);

        /// <summary>Options, Tests: the Sell tab of Services, on the first business that can be sold.</summary>
        public void TestServicesSell() => OpenServices(ServicesDirector.TabSale);

        /// <summary>Options, Tests: the Services screen's switch flipped, then put back on.</summary>
        public void TestServicesSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(ServicesScreen.ScreenId), !UiScreenSwitches.On(ServicesScreen.ScreenId));
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(ServicesScreen.ScreenId), true);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_services_switch", null, 8f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OpenServices(int tab)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                ServicesLive.Want(tab, null);
                if (!PhoneApp.OpenById(ServicesDirector.AppId))
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
