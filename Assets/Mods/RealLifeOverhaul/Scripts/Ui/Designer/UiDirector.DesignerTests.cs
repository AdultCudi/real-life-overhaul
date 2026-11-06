using System.Collections.Generic;
using BigAmbitions.InteriorDesigner;
using UI.InteriorDesigner;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: what the interior designer's face holds now said in one notice (the tool in hand, what the business needs and what is still short, what the edit
    /// comes to), and the designer's switch flipped to the game's own panels and back. A notice says why one cannot run. The switch test changes only the running value,
    /// and waits until the designer is shut, since a face is chosen at each opening.
    /// </summary>
    public sealed partial class UiDirector
    {
        /// <summary>Options, Tests: what the designer's face shows, while the designer is open.</summary>
        public void TestDesignerState()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                string said = DesignerKits.Describe(out string tool, out string met, out string short_, out string apply);
                if (said == null)
                {
                    Notifications.Show(NotificationType.Info, "reallife_ui_test_designer_shut", null, 8f);
                    return;
                }
                Notifications.Show(NotificationType.Info, "reallife_ui_test_designer_state_is",
                    new Dictionary<string, string> { { "tool", tool }, { "met", met }, { "short", short_ }, { "apply", apply } }, 12f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        /// <summary>Options, Tests: the designer's switch off for the next opening, then on again.</summary>
        public void TestDesignerSwitch()
        {
            try
            {
                if (!Ready())
                {
                    return;
                }
                if (InteriorDesignerUI.IsOpen)
                {
                    Notifications.Show(NotificationType.Info, "reallife_ui_test_designer_close_first", null, 8f);
                    return;
                }
                string option = UiScreenSwitches.OptionOf(DesignerFace.ScreenId);
                UiScreenSwitches.Set(option, !UiScreenSwitches.On(DesignerFace.ScreenId));
                UiScreenSwitches.Set(option, true);
                Notifications.Show(NotificationType.Info, "reallife_ui_test_designer_flipped", null, 8f);
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
