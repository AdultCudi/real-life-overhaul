using System;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Options, Tests: the gallery of the kit's parts (controls, the big table, charts, a catalogue, a statement, tracks)
    /// over whatever shows, each part answering as on a real screen, Back closing it. It says in its notice why it cannot
    /// open when it cannot (the switch off, the fonts still loading).
    /// </summary>
    public sealed partial class UiDirector
    {
        /// <summary>Options, Tests: the kit's parts over the screen.</summary>
        public void TestParts()
        {
            try
            {
                if (!On)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_off", null, 8f);
                    return;
                }
                if (!UiFonts.Ready)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ui_test_fonts", null, 8f);
                    return;
                }
                var screen = new UiPartsGalleryScreen(UiText.Words, UiText.Money);
                Overlay(screen, UiText.T("reallife_ui_parts_title"), host => screen.Close = () => Destroy(host.gameObject), UiIcon.Grid);
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }
    }
}
