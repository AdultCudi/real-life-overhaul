using System.Collections;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode's tests of what is drawn (Options > Tests; the owner's first test, 26 Oct 2026: "It shows grid squares on existing
    /// buildings but not on empty spaces", "How do I see the zoning?", the window's text and overlap): each opens build mode over
    /// Uptown, measures, writes one "Construction check (build mode: ...)" line ending PASS or FAIL with a notice, and leaves it
    /// open for the player to look at. Buttons only: they open the map.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>Test: every cell of a block's free spaces drawn for sale with its bright edge, and the map's camera drawing the layer.</summary>
        public static void TestBuildModeLayerNow()
        {
            ConstructionDirector d = ModeReady("the layer");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.LayerTest()));
            }
        }

        private IEnumerator LayerTest()
        {
            const string test = "the layer";
            int column = FreeColumn(1);
            if (column < 0)
            {
                ModeLine(test, "no free land at this load", false, "reallife_bld_test_nothing");
                yield break;
            }
            if (!OpenOver(test))
            {
                yield break;
            }
            _mode.UseTool(BuildTool.Land);
            _mode.UseView(BuildView.Status);
            LotOfColumn(column, out LotView lot);
            Middle(column, out float x, out float z);
            BuildModeGame.FlyTo(new Vector3(x, 0f, z));
            yield return new WaitForSecondsRealtime(1.5f);
            CellGrid grid = Cells;
            int free = 0, drawn = 0, cells = 0;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                if (!LotOfColumn(c, out LotView l) || l.Block != lot.Block || !FactsOf(c).Free)
                {
                    continue;
                }
                free++;
                cells += grid.Columns[c].Rows;
                if (_mode.Drawn(c, out int fill, out int line) && fill == BuildModeRules.ForSale && line == BuildModeRules.LineForSale)
                {
                    drawn += grid.Columns[c].Rows;
                }
            }
            bool seen = _mode.LayerSeen(out string why);
            bool pass = free > 0 && drawn == cells && seen;
            ModeLine(test, "block " + lot.Block + " (" + LotWords(lot.Id) + "): " + drawn + " of " + cells + " cells of its " + free + " free space(s) drawn for sale with their edge; "
                           + "the map's camera " + BuildModeGame.CameraHeight.ToString("0", CultureInfo.InvariantCulture) + " m up draws the layer " + (seen ? "YES" : "NO (" + why + ")"),
                pass, "reallife_bld_mode_test_fail");
        }

        /// <summary>Test: the zoning view: every free cell in its council zoning's colour with its for-sale edge; Buy land opens on it.</summary>
        public static void TestBuildModeZoningNow()
        {
            ConstructionDirector d = ModeReady("zoning view");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.ZoningTest()));
            }
        }

        private IEnumerator ZoningTest()
        {
            const string test = "zoning view";
            if (!OpenOver(test))
            {
                yield break;
            }
            _mode.UseTool(BuildTool.Land);
            bool opensOn = _mode.View == BuildView.Zoning;
            yield return null;
            CellGrid grid = Cells;
            int free = 0, right = 0;
            var uses = new int[5];
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                BuildModeRules.ColumnFacts f = FactsOf(c);
                if (!f.Free)
                {
                    continue;
                }
                free++;
                int wanted = BuildModeRules.ZoneFill(f.Zones, 0, f.MainUse);
                if (_mode.Drawn(c, out int fill, out int line) && fill == wanted && line == BuildModeRules.LineForSale)
                {
                    right++;
                }
                int u = wanted - BuildModeRules.Homes;
                uses[u >= 0 && u < 4 ? u : 4]++;
            }
            string hover = CellWords(FreeColumn(1), 0);
            bool pass = opensOn && free > 0 && right == free && !string.IsNullOrEmpty(hover);
            ModeLine(test, "Buy land opens on the zoning " + Yes(opensOn) + "; " + right + " of " + free + " free column(s) drawn in their zoning with the for-sale edge (homes "
                           + uses[0] + ", shops " + uses[1] + ", offices " + uses[2] + ", warehouses " + uses[3] + ", mixed or none " + uses[4] + "); a cell in words: " + (hover ?? "none"),
                pass, "reallife_bld_mode_test_fail");
        }

        /// <summary>Test: the window in the middle with each tool and each view: every part below the one before it, no text cut short, the list's links apart.</summary>
        public static void TestBuildModeWindowNow()
        {
            ConstructionDirector d = ModeReady("the window");
            if (d != null)
            {
                d.StartCoroutine(Guard.Routine(d.WindowTest()));
            }
        }

        private IEnumerator WindowTest()
        {
            const string test = "the window";
            if (!OpenOver(test))
            {
                yield break;
            }
            string wrong = null;
            int looked = 0;
            for (int t = 0; t < (int)BuildTool.Count && wrong == null; t++)
            {
                _mode.UseTool((BuildTool)t);
                _mode.Unfold();
                yield return null;
                string w = _mode.WindowOverlap();
                looked++;
                wrong = w != null ? ConstructionText.T(BuildMode.ToolKey((BuildTool)t)) + ": " + w : null;
            }
            for (int v = 0; v < (int)BuildView.Count && wrong == null; v++)
            {
                _mode.UseView((BuildView)v);
                yield return null;
                string w = _mode.WindowOverlap();
                looked++;
                wrong = w != null ? ConstructionText.T(BuildModeRules.ViewKeys[v]) + ": " + w : null;
            }
            _mode.UseTool(BuildTool.Land);
            ModeLine(test, looked + " page(s) looked at, each tool and each view: " + (wrong ?? "nothing overlaps and no text is cut short"), wrong == null, "reallife_bld_mode_test_fail");
        }

        // Build mode open over Uptown for a test; false with its line when it would not open.
        private bool OpenOver(string test)
        {
            string why = OpenBuildMode();
            if (why != null || _mode == null || !_mode.IsOpen)
            {
                ModeLine(test, "build mode would not open (" + (why ?? "no window") + ")", false, why ?? "reallife_bld_mode_test_fail");
                return false;
            }
            return true;
        }

        private void Middle(int column, out float x, out float z)
        {
            Cells.Rect(column, 0, 0f, out float x1, out float z1, out float x2, out float z2);
            x = (x1 + x2) * 0.5f;
            z = (z1 + z2) * 0.5f;
        }
    }
}
