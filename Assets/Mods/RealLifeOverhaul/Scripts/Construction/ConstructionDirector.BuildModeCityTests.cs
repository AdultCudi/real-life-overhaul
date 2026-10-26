using System.Collections;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode over the whole city's tests (Options > Tests; the owner, 26 Oct 2026: "make the cursor a '+' like how it is in CS"
    /// and "make it grids in all district"): the plot layer drawn over a district of the game's (its plots counted and timed, each
    /// coloured as the rules say, the map's camera seeing its squares), the tools outside Uptown, the pointer a crosshair over the
    /// map and the game's own over the interface, and a known building's card and tool panel. Each opens build mode, glides the map
    /// over the game's district, writes one "Construction check (build mode: ...)" line ending PASS or FAIL with a notice, and leaves
    /// build mode open to look at. Buttons only: they open the map.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const float GlideWait = 2.5f;   // seconds (real time) for the map to glide over the plot and settle

        /// <summary>Test: the plot layer over a district of the game's: the plots counted and timed, coloured as the rules say, seen by the map's camera; the tools outside Uptown.</summary>
        public static void TestBuildModeCityNow()
        {
            ConstructionDirector d = ModeReady("the city's plots");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.CityTest()));
            }
        }

        private IEnumerator CityTest()
        {
            const string test = "the city's plots";
            if (!OpenOver(test))
            {
                yield break;
            }
            int plot = NearPlot();
            if (plot < 0 || !_mode.PlotFactsOf(plot, out PlotFacts f, out string street, out int number, out Vector3 at))
            {
                ModeLine(test, "no plot over the city's districts (" + _mode.PlotCount + " read)", false, "reallife_bld_mode_test_fail");
                yield break;
            }
            _mode.FlyForTest(at);
            yield return new WaitForSecondsRealtime(GlideWait);
            bool uptown = _mode.WhereForTest();
            yield return null;
            string wrong = null;
            if (uptown)
            {
                wrong = "the map over " + ConstructionGame.AddressText(street, number) + " still reads as Uptown";
            }
            BuildModeCityRules.Look(f, _mode.View, out int fill, out int line);
            if (wrong == null && (!_mode.PlotDrawn(plot, out int drawnFill, out int drawnLine) || drawnFill != fill || drawnLine != line))
            {
                wrong = "the plot at " + ConstructionGame.AddressText(street, number) + " is not drawn in the colours the rules give it";
            }
            int seen = _mode.PlotTilesSeen(out int shown);
            if (wrong == null && (shown != _mode.PlotTiles || seen <= 0))
            {
                wrong = shown + " of " + _mode.PlotTiles + " square(s) of the layer drawn, " + seen + " seen by the map's camera";
            }
            for (int t = 0; t < (int)BuildTool.Count && wrong == null; t++)
            {
                string why = BuildModeCityRules.ToolWhyAt((BuildTool)t, _mode.Holdings, _mode.State.ViewOnly, false);
                bool greyed = _mode.ToolGreyed((BuildTool)t, out string said);
                if (greyed != (why != null) || (greyed && said != ConstructionText.T(why)))
                {
                    wrong = (BuildTool)t + (greyed ? " greyed with '" + said + "'" : " open") + ", the rules want " + (why != null ? "'" + ConstructionText.T(why) + "'" : "it open");
                }
            }
            if (wrong == null && _mode.HintShown != _mode.HintWanted)
            {
                wrong = "the hint bar says '" + _mode.HintShown + "', the rules want '" + _mode.HintWanted + "'";
            }
            bool fast = _mode.PlotMilliseconds >= 0.0 && _mode.PlotMilliseconds < 1000.0;
            ModeLine(test, _mode.PlotCount + " plot(s) in " + _mode.PlotTiles + " square(s), read and made in " + _mode.PlotMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)
                           + " ms (wanted under 1000); the map over " + ConstructionGame.AddressText(street, number) + ", Uptown " + Yes(uptown) + ", " + seen + " square(s) seen; Zone "
                           + Greyed(BuildTool.Zone) + ", Place " + Greyed(BuildTool.Place) + ", Join " + Greyed(BuildTool.Join) + (wrong != null ? " WRONG " + wrong : string.Empty),
                wrong == null && fast && _mode.PlotCount > 0, "reallife_bld_mode_test_fail");
        }

        private string Greyed(BuildTool tool) => _mode.ToolGreyed(tool, out string why) ? "greyed ('" + why + "')" : "open";

        // The plot nearest the player (one of the game's buildings outside Uptown), or -1.
        private int NearPlot()
        {
            Vector3 player = ConstructionGame.Player;
            int best = -1;
            float near = float.MaxValue;
            for (int i = 0; i < _mode.PlotCount; i++)
            {
                if (!_mode.PlotFactsOf(i, out _, out _, out _, out Vector3 at))
                {
                    continue;
                }
                float dd = (at.x - player.x) * (at.x - player.x) + (at.z - player.z) * (at.z - player.z);
                if (dd < near)
                {
                    near = dd;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Test: the pointer a crosshair over the map and the game's own over build mode's panels, put back on leaving.</summary>
        public static void TestBuildModeCursorNow()
        {
            ConstructionDirector d = ModeReady("the pointer");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.CursorTest()));
            }
        }

        private IEnumerator CursorTest()
        {
            const string test = "the pointer";
            if (!OpenOver(test))
            {
                yield break;
            }
            yield return null;
            BuildCursor map = _mode.PointerFor(false);
            BuildCursor panel = _mode.PointerFor(true);
            BuildCursor back = _mode.PointerFor(false);
            _mode.Close();
            yield return null;
            BuildCursor left = _mode.PointerNow;
            string again = OpenBuildMode();
            bool pass = map == BuildCursor.Cross && panel == BuildCursor.Normal && back == BuildCursor.Cross && left == BuildCursor.Normal && again == null;
            ModeLine(test, "over the map " + map + ", over a panel " + panel + ", over the map again " + back + ", after leaving " + left + " (wanted Cross, Normal, Cross, Normal)"
                           + (again != null ? "; build mode would not open again (" + again + ")" : string.Empty), pass, "reallife_bld_mode_test_fail");
        }

        /// <summary>Test: a known building of the city's: its card gives its address, type, size, owner, price and state; clicked, its tool panel's actions each open or greyed as the rules and the construction game say.</summary>
        public static void TestBuildModePlotNow()
        {
            ConstructionDirector d = ModeReady("a building's plot");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.PlotTest()));
            }
        }

        private IEnumerator PlotTest()
        {
            const string test = "a building's plot";
            if (!OpenOver(test))
            {
                yield break;
            }
            int plot = _mode.FindPlot(x => x.ForSale);
            plot = plot >= 0 ? plot : NearPlot();
            if (plot < 0 || !_mode.PlotFactsOf(plot, out PlotFacts f, out string street, out int number, out Vector3 at))
            {
                ModeLine(test, "no plot over the city's districts", false, "reallife_bld_mode_test_fail");
                yield break;
            }
            _mode.FlyForTest(at);
            yield return new WaitForSecondsRealtime(GlideWait);
            _mode.WhereForTest();
            string words = _mode.PlotWords(plot) ?? string.Empty;
            string address = ConstructionGame.AddressText(street, number);
            string wrong = null;
            if (!words.Contains(address))
            {
                wrong = "the card '" + words + "' does not give the address " + address;
            }
            int parts = words.Split(';').Length;
            if (wrong == null && parts < 5)
            {
                wrong = "the card '" + words + "' has " + parts + " part(s), not address, type and size, owner, price and state";
            }
            bool picked = _mode.PickPlotForTest(plot);
            yield return null;
            if (wrong == null && (!picked || !_mode.PlotDrawn(plot, out int fill, out _) || fill != BuildModeRules.Picked))
            {
                wrong = "a click did not pick it (picked " + Yes(picked) + ")";
            }
            bool session = _mode.State.ViewOnly;
            string buy = _mode.PlotWhy(PlotAction.Buy), rent = _mode.PlotWhy(PlotAction.Rent), bulldoze = _mode.PlotWhy(PlotAction.Bulldoze);
            if (wrong == null && buy != BuildModeCityRules.BuyWhy(f, session))
            {
                wrong = "Buy says '" + buy + "', the rules want '" + BuildModeCityRules.BuyWhy(f, session) + "'";
            }
            if (wrong == null && rent != BuildModeCityRules.RentWhy(f, session))
            {
                wrong = "Rent says '" + rent + "', the rules want '" + BuildModeCityRules.RentWhy(f, session) + "'";
            }
            if (wrong == null && bulldoze == null)
            {
                wrong = "Bulldoze is open on one of the game's own buildings";
            }
            if (wrong == null && _mode.HintShown != _mode.HintWanted)
            {
                wrong = "the hint bar says '" + _mode.HintShown + "', the rules want '" + _mode.HintWanted + "'";
            }
            ModeLine(test, address + ": '" + words + "'; Buy " + Why(buy) + ", Rent " + Why(rent) + ", Refurbish " + Why(_mode.PlotWhy(PlotAction.Refurbish)) + ", Redevelop "
                           + Why(_mode.PlotWhy(PlotAction.Redevelop)) + ", Bulldoze " + Why(bulldoze) + (wrong != null ? " WRONG " + wrong : string.Empty), wrong == null,
                "reallife_bld_mode_test_fail");
        }

        private static string Why(string key) => key == null ? "open" : "greyed ('" + ConstructionText.T(key) + "')";
    }
}
