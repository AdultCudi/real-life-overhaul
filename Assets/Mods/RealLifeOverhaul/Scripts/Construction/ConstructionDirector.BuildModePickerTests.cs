using System.Collections;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The test of build mode's design window ("Choose a building"; Options > Tests; the owner's screenshot, 27 Oct 2026: the seventh
    /// card drew below the window, over the footer and the toolbar, and the heading named a street by its id). It opens build mode over
    /// the player's nearest zoned land, picks it under Place so the window opens, and measures what is drawn: the heading has no id,
    /// every card lies inside the list's rectangle and is the same size, the list is clipped to its area and reaches its first and its
    /// last card by scrolling, and the window stands on the screen above the toolbar with its foot inside it. Writes one
    /// "Construction check (build mode: the design window)" line ending PASS or FAIL with a notice. Button only: it opens the map.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>Test: Choose a building on the player's nearest zoned site laid out inside its list, the heading in words.</summary>
        public static void TestBuildModePickerNow()
        {
            ConstructionDirector d = ModeReady("the design window");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.PickerTest()));
            }
        }

        private IEnumerator PickerTest()
        {
            const string test = "the design window";
            int own = NearestOwnFootprint(out int count);
            if (own < 0)
            {
                ModeLine(test, "no zoned land of the player's with no project on it, so no building to choose for", false, "reallife_bld_test_nothing");
                yield break;
            }
            if (!OpenOver(test))
            {
                yield break;
            }
            if (_mode.ToolGreyed(BuildTool.Place, out string greyed))
            {
                ModeLine(test, "the Place tool is greyed (" + greyed + ")", false, "reallife_bld_test_nothing");
                yield break;
            }
            _mode.UseTool(BuildTool.Place);
            _mode.PickForTest(own, count, 0);
            yield return null;
            yield return null;   // the layout settles over a frame or two
            if (!_mode.CatalogueShown)
            {
                _mode.ClearForTest();
                _mode.UseTool(BuildTool.Land);
                ModeLine(test, "no design fits the nearest zoned land, so no design window opened", false, "reallife_bld_test_nothing");
                yield break;
            }
            string heading = _mode.CatalogueHeading;
            string wrong = StreetNameRules.HasId(heading) ? "the heading shows an id: '" + heading + "'" : _mode.CatalogueOverlap();
            string scale = _mode.CatalogueScale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string cards = _mode.CatalogueCards.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _mode.ShutCatalogue();
            _mode.ClearForTest();
            _mode.UseTool(BuildTool.Land);
            ModeLine(test, "Choose a building for " + count + " column(s) of the player's land: the heading '" + heading + "', " + cards + " card(s), the window at scale " + scale + ": "
                           + (wrong ?? "every card inside the list and the same size, the list clipped and scrolled to its first and last card, the foot and the toolbar clear, no id in the heading"),
                wrong == null, "reallife_bld_mode_test_fail");
        }

        // The first column of the player's zoned land nearest to where the player stands (a site with no project on it and a use in force),
        // and its columns; -1 for none. Read once, when the test is pressed.
        private int NearestOwnFootprint(out int count)
        {
            count = 0;
            CellGrid grid = Cells;
            Vector3 player = ConstructionGame.Player;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                BuildModeRules.ColumnFacts f = FactsOf(c);
                if (!f.Mine || f.HasProject || f.UnderWay || !BuildModeHintRules.InForce(f) || RowsHeld(c, out LotFile site) == 0 || site == null)
                {
                    continue;
                }
                Middle(c, out float x, out float z);
                float dx = x - player.x, dz = z - player.z;
                float distance = dx * dx + dz * dz;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = c;
                }
            }
            if (best < 0)
            {
                return -1;
            }
            RowsHeld(best, out LotFile mine);
            int at = best;
            while (at >= 0 && RowsHeld(at, out LotFile s) > 0 && s == mine && count < BuildCatalogue.SiteSpacesMost)
            {
                count++;
                at = grid.Next(at);
            }
            return best;
        }
    }
}
