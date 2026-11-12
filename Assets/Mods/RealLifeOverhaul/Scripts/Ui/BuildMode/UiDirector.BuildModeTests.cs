using RealLifeOverhaul.Construction;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: "Choose a building" in Build mode on the kit, and Build mode's switch flipped to the old panels and back. A notice says why one cannot run. The switch test
    /// changes only the running value, and closes Build mode first if it is open, since a face is chosen at each opening.
    /// </summary>
    public sealed partial class UiDirector
    {
        /// <summary>Options, Tests: Build mode on the Place tool with Choose a building up, on the kit.</summary>
        public void TestBuildCatalogue()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                ConstructionDirector.TestBuildModeKitNow();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: Build mode's switch off for the next opening, then on again.</summary>
        public void TestBuildSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                ConstructionDirector d = ConstructionDirector.Instance;
                if (d != null && d.Mode != null && d.Mode.IsOpen)
                {
                    d.Mode.Close();
                }
                string option = UiScreenSwitches.OptionOf(BuildFace.ScreenId);
                UiScreenSwitches.Set(option, !UiScreenSwitches.On(BuildFace.ScreenId));
                UiScreenSwitches.Set(option, true);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_build_switch", null, 8f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
