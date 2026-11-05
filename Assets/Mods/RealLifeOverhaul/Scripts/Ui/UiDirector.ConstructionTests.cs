using RealLifeOverhaul.Construction;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: the Construction app on the kit, and its switch flipped back to the page as it was. A notice says why one cannot open.
    /// The switch test changes only the running value.
    /// </summary>
    public sealed partial class UiDirector
    {
        /// <summary>Options, Tests: the Construction app on the kit, on its overview.</summary>
        public void TestConstruction()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                ConstructionLive.Instance.Want(ConstructionDirector.TabOverview, 0);
                if (!PhoneApp.OpenById(ConstructionDirector.AppId))
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_phone", null, 8f);
                }
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the Construction switch flipped to the old page and back on.</summary>
        public void TestConstructionSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                string id = ConstructionScreen.ScreenId;
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(id), !UiScreenSwitches.On(id));
                UiScreenSwitches.Set(UiScreenSwitches.OptionOf(id), true);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_construction_switch", null, 8f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
