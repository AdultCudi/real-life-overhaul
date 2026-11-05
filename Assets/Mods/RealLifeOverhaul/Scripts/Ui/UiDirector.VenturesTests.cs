using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Options, Tests: Brands, Ventures and the Web shop on the kit, and each switch flipped back to the page as it was. A notice says why one cannot open.</summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] VenturesSwitchIds = { "ventures", "brands" };

        /// <summary>Options, Tests: Ventures on the kit.</summary>
        public void TestVentures()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                VenturesLive.Want(VenPages.TabInvestors, null);
                if (!PhoneApp.OpenById(VenturesDirector.AppId))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                }
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: Brands on the kit.</summary>
        public void TestBrands()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                BrandsLive.Want(BraPages.TabBrands, null);
                if (!PhoneApp.OpenById(RealLifeOverhaul.Economy.Brands.BrandsDirector.AppId))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                }
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: each screen's switch flipped, then put back on.</summary>
        public void TestVenturesSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                for (int i = 0; i < VenturesSwitchIds.Length; i++)
                {
                    string id = VenturesSwitchIds[i];
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(id), !UiScreenSwitches.On(id));
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(id), true);
                }
                Notifications.Show(NotificationType.Info, "reallife_ui_test_ventures_switch", null, 8f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
