using System.Collections;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The test of Build mode's "Choose a building" on the kit (Options > Tests): it opens Build mode over the player's nearest zoned land with the Place tool, picks the land so
    /// the design window opens, and leaves it up for the player to look at (Back, or Leave, closes it). Writes one "Construction check (build mode: choose a building on the
    /// kit)" line with what the panels hold, and says in a notice whether the window is up. Button only: it opens the map.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>Test: Build mode open on the Place tool with Choose a building up, and what its face holds.</summary>
        public static void TestBuildModeKitNow()
        {
            ConstructionDirector d = ModeReady("choose a building on the kit");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.KitTest()));
            }
        }

        private IEnumerator KitTest()
        {
            const string test = "choose a building on the kit";
            int own = NearestOwnFootprint(out int count);
            if (!OpenOver(test))
            {
                yield break;
            }
            _mode.UseTool(BuildTool.Place);
            if (own >= 0)
            {
                _mode.PickForTest(own, count, 0);
            }
            yield return null;
            yield return null;   // the layout settles over a frame or two
            bool up = _mode.CatalogueShown;
            string window = up ? "the design window is up with " + _mode.CatalogueCards + " card(s)" : "no design window opened (no zoned land of the player's with room)";
            ModeLine(test, "Build mode on " + _mode.FaceLine + ": " + window, true, "reallife_bld_mode_test_fail");
            UI.Notification.NotificationType kind = up ? UI.Notification.NotificationType.Info : UI.Notification.NotificationType.Warning;
            UI.Notification.Notifications.Show(kind, up ? "reallife_ui_test_build_shown" : "reallife_ui_test_build_none", null, 9f, null, null, true, false);
        }
    }
}
