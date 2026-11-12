using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: Property, its map of the city's rents and the Car Showroom on the kit, and each switch flipped back to the page as it was.
    /// A notice says why one cannot open. The switch test changes only the running value.
    /// </summary>
    public sealed partial class UiDirector
    {
        private static readonly string[] EstateSwitchIds = { "estate", "dealer" };

        /// <summary>Options, Tests: Property on the kit.</summary>
        public void TestEstate() => OpenEstate(EstateDirector.AppId, EstateDirector.TabMine, null);

        /// <summary>Options, Tests: the map of the city's rents (the Market tab's view of it).</summary>
        public void TestEstateRents() => OpenEstate(EstateDirector.AppId, EstateDirector.TabMarket, "rents");

        /// <summary>Options, Tests: the Car Showroom on the kit.</summary>
        public void TestDealer() => OpenEstate(DealerDirector.AppId, DealerDirector.TabStock, null);

        /// <summary>Options, Tests: each estate screen's switch flipped, then Property's put back on.</summary>
        public void TestEstateSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                for (int i = 0; i < EstateSwitchIds.Length; i++)
                {
                    string id = EstateSwitchIds[i];
                    UiScreenSwitches.Set(UiScreenSwitches.OptionOf(id), !UiScreenSwitches.On(id));
                }
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf("estate"), true);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_estate_switch", null, 8f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OpenEstate(string id, int tab, string pick)
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                if (id == EstateDirector.AppId)
                {
                    EstateLive.Want(tab, pick);
                }
                else
                {
                    DealerLive.Want(tab, pick);
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
