using System.Collections;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode's tests of its guidance (Options > Tests; the owner, 26 Oct 2026: "when a new user sees it they must not feel where
    /// to click"): the hint bar's line in each state, the tools greyed until they can be used with the reason in their tooltip,
    /// and the first opening's tour. Each opens build mode over Uptown, walks it, writes one "Construction check (build mode: ...)"
    /// line ending PASS or FAIL with a notice, and leaves build mode open to look at. Buttons only: they open the map.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>Test: the hint bar says the line build mode's rules want in each state walked: each tool idle, squares picked, a zone chosen, Esc's line.</summary>
        public static void TestBuildModeHintsNow()
        {
            ConstructionDirector d = ModeReady("the hint bar");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.HintsTest()));
            }
        }

        private IEnumerator HintsTest()
        {
            const string test = "the hint bar";
            if (!OpenOver(test))
            {
                yield break;
            }
            var said = new StringBuilder(512);
            string wrong = null;
            int states = 0;
            for (int t = 0; t < (int)BuildTool.Count && wrong == null; t++)
            {
                if (_mode.ToolGreyed((BuildTool)t, out _))
                {
                    continue;
                }
                _mode.UseTool((BuildTool)t);
                yield return null;
                wrong = HintRight(ConstructionText.T(BuildMode.ToolKey((BuildTool)t)), said, ref states);
                string expected = ConstructionText.T(IdleHint((BuildTool)t));
                if (wrong == null && _mode.HintShown != expected)
                {
                    wrong = ConstructionText.T(BuildMode.ToolKey((BuildTool)t)) + ": '" + _mode.HintShown + "', not '" + expected + "'";
                }
            }
            int free = FreeColumn(2);
            if (wrong == null && free >= 0)
            {
                _mode.UseTool(BuildTool.Land);
                _mode.PickForTest(free, 2, 0);
                yield return null;
                wrong = HintRight("squares picked", said, ref states);
                _mode.ClearForTest();
                yield return null;
                if (wrong == null)
                {
                    wrong = HintRight("cleared", said, ref states);
                }
                if (wrong == null && _mode.HintShown != ConstructionText.T("reallife_bld_hint_land_idle"))
                {
                    wrong = "cleared: '" + _mode.HintShown + "', not the first step's line";
                }
            }
            _mode.UseTool(BuildTool.Land);
            ModeLine(test, states + " state(s): " + said + (wrong != null ? " WRONG " + wrong : string.Empty), wrong == null && states > 0, "reallife_bld_mode_test_fail");
        }

        private static string IdleHint(BuildTool tool)
        {
            switch (tool)
            {
                case BuildTool.Zone: return "reallife_bld_hint_zone_pick";
                case BuildTool.Place: return "reallife_bld_hint_place_idle";
                case BuildTool.Bulldoze: return "reallife_bld_hint_bulldoze_idle";
                case BuildTool.Join: return "reallife_bld_hint_join_idle";
                case BuildTool.Refurbish: return "reallife_bld_hint_refurbish_idle";
                default: return "reallife_bld_hint_land_idle";
            }
        }

        // The hint bar against what the rules want now; its words added to the line.
        private string HintRight(string state, StringBuilder said, ref int states)
        {
            states++;
            string shown = _mode.HintShown, wanted = _mode.HintWanted;
            said.Append(states > 1 ? "; " : string.Empty).Append(state).Append(" '").Append(shown).Append('\'');
            if (string.IsNullOrEmpty(shown))
            {
                return state + ": the hint bar is empty";
            }
            return shown == wanted ? null : state + ": '" + shown + "', the rules want '" + wanted + "'";
        }

        /// <summary>
        /// Test: the tools greyed as the rules say for what the player holds now, each greyed one's tooltip giving its reason, and a
        /// greyed one not chosen when pressed; then the toolbar as it stands with nothing owned (Zone, Place, Join, Bulldoze and
        /// Refurbish greyed with their reasons, Buy land open).
        /// </summary>
        public static void TestBuildModeToolsNow()
        {
            ConstructionDirector d = ModeReady("the tools");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.ToolsTest()));
            }
        }

        private IEnumerator ToolsTest()
        {
            const string test = "the tools";
            if (!OpenOver(test))
            {
                yield break;
            }
            yield return null;
            BuildHoldings held = _mode.Holdings;
            string wrong = Greyed(held, "now");
            BuildTool before = _mode.State.Tool;
            for (int t = 0; t < (int)BuildTool.Count && wrong == null; t++)
            {
                if (_mode.ToolGreyed((BuildTool)t, out _))
                {
                    _mode.UseTool((BuildTool)t);
                    wrong = _mode.State.Tool != before ? "a greyed tool was chosen when pressed: " + (BuildTool)t : null;
                }
            }
            _mode.ShowToolsFor(new BuildHoldings());
            yield return null;
            string nothing = wrong == null ? Greyed(new BuildHoldings(), "with nothing owned") : null;
            wrong = wrong ?? nothing;
            int open = 0;
            for (int t = 0; t < (int)BuildTool.Count; t++)
            {
                open += _mode.ToolGreyed((BuildTool)t, out _) ? 0 : 1;
            }
            if (wrong == null && (open != 1 || _mode.ToolGreyed(BuildTool.Land, out _)))
            {
                wrong = "with nothing owned " + open + " tool(s) open, not Buy land alone";
            }
            _mode.Rewrite();
            ModeLine(test, "held now: land " + held.Land + " column(s), " + held.Placeable + " free for a building, " + held.Unzoned + " to zone, buildings " + held.Buildings
                           + "; with nothing owned only Buy land open: " + (wrong ?? "each tool greyed as the rules say, its tooltip giving the reason"), wrong == null, "reallife_bld_mode_test_fail");
        }

        // Each tool greyed exactly when the rules give a reason for these holdings, its tooltip's line that reason; null when so.
        private string Greyed(BuildHoldings held, string when)
        {
            for (int t = 0; t < (int)BuildTool.Count; t++)
            {
                string why = BuildModeHintRules.ToolWhy((BuildTool)t, held, _mode.State.ViewOnly);
                bool greyed = _mode.ToolGreyed((BuildTool)t, out string line);
                if (greyed != (why != null))
                {
                    return when + ": " + (BuildTool)t + (greyed ? " greyed with no reason" : " open though the rules say '" + ConstructionText.T(why) + "'");
                }
                if (greyed && line != ConstructionText.T(why))
                {
                    return when + ": " + (BuildTool)t + "'s tooltip says '" + line + "', not '" + ConstructionText.T(why) + "'";
                }
            }
            return null;
        }

        /// <summary>Test: the first opening's tour walked: four tips, each on the screen and clear of the part it points at, then remembered as seen.</summary>
        public static void TestBuildModeTourNow()
        {
            ConstructionDirector d = ModeReady("the tour");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.TourTest()));
            }
        }

        private IEnumerator TourTest()
        {
            const string test = "the tour";
            if (!OpenOver(test))
            {
                yield break;
            }
            bool zone = !_mode.ToolGreyed(BuildTool.Zone, out _);
            if (zone)
            {
                _mode.UseTool(BuildTool.Zone);   // the tallest tool panel: the owner's test found the first tip over it
            }
            yield return null;
            bool first = _mode.TourForTest(out string wrong);
            int tips = first ? 1 : 0;
            yield return new WaitForSecondsRealtime(0.5f);
            TourStep step = TourStep.HintBar;
            for (int k = 0; k < 4 && wrong == null; k++)
            {
                step = _mode.TourNextForTest(out wrong);
                if (step != TourStep.Done)
                {
                    tips++;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            bool seen = BuildModeTour.Seen;
            bool pass = wrong == null && tips == 4 && step == TourStep.Done && seen;
            _mode.UseTool(BuildTool.Land);
            ModeLine(test, tips + " of 4 tip(s) shown" + (zone ? " over the Zone tool's panel" : string.Empty)
                           + ", each on the screen, its words inside the card's padding, clear of its part, the tool panel, the legend and the game's HUD, "
                           + (step == TourStep.Done ? "then done" : "not done") + ", remembered " + Yes(seen)
                           + (wrong != null ? " (" + wrong + ")" : string.Empty), pass, "reallife_bld_mode_test_fail");
        }
    }
}
