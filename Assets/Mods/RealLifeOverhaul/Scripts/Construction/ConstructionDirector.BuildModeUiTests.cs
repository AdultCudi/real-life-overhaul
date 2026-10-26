using System.Collections;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The interface test's part for the owner's test of build mode (26 Oct 2026, "Fix those"): the game's labels at the foot
    /// hidden, a drag picking forgivingly along its street (far along it, over the block's corner, after a pick on another street),
    /// land for sale picked under Zone saying to buy it first and Buy land taking the pick on, and squares that make no site with
    /// their facts, how to fix them and a Clear button; each look measured (the HUD, the boxes' pictures, the zone rows'
    /// squares, the legend, the hint on one line). Run inside TestBuildModeWindowNow; its words go into that test's one line.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private string _uiWrong;

        // The owner's test's looks, after the interface test's own; _uiWrong set to what is wrong, else null.
        private IEnumerator OwnerLooks(StringBuilder said)
        {
            _uiWrong = null;
            said.Append("; the game's labels at the foot hidden ").Append(Yes(BuildModeGame.FootLabelsHeld));
            if (!BuildModeGame.FootLabelsHeld)
            {
                _uiWrong = "the game's FPS, address and neighbourhood lines are not hidden while build mode is open";
                yield break;
            }
            int free = FreeColumn(1);
            if (free < 0)
            {
                said.Append("; no land for sale to drag over");
                yield break;
            }
            CellGrid grid = Cells;
            _mode.UseTool(BuildTool.Land);
            yield return null;

            // Far along the street: at most a site's width taken, as a site.
            int far = free;
            for (int k = 0; k < 11; k++)
            {
                int next = grid.Next(far);
                if (next < 0)
                {
                    break;
                }
                far = next;
            }
            grid.Rect(far, grid.Columns[far].Rows - 1, 0f, out float x1, out float z1, out float x2, out float z2);
            _mode.ClearForTest();
            _mode.DragForTest(free, 0, (x1 + x2) * 0.5f, (z1 + z2) * 0.5f);
            yield return null;
            _uiWrong = Took("a drag far along the street", said);
            if (_uiWrong != null)
            {
                yield break;
            }

            // Over the block's corner, onto another street's squares: still along the first street.
            int other = OtherStrip(free, false);
            if (other >= 0)
            {
                grid.Rect(other, 0, 0f, out x1, out z1, out x2, out z2);
                _mode.ClearForTest();
                _mode.DragForTest(free, 0, (x1 + x2) * 0.5f, (z1 + z2) * 0.5f);
                yield return null;
                _uiWrong = Took("a drag onto another street's squares", said);
                if (_uiWrong == null && grid.Columns[_mode.Site.FirstColumn].Strip != grid.Columns[free].Strip)
                {
                    _uiWrong = "a drag onto another street's squares picked along that street, not the first";
                }
                if (_uiWrong != null)
                {
                    yield break;
                }
            }

            // The owner's screenshot: squares picked on one street, then a drag on another; the second street's alone, as a site.
            int second = OtherStrip(free, true);
            if (second >= 0)
            {
                _mode.PickForTest(free, 1, 0);
                grid.Rect(second, grid.Columns[second].Rows - 1, 0f, out x1, out z1, out x2, out z2);
                _mode.DragForTest(second, 0, (x1 + x2) * 0.5f, (z1 + z2) * 0.5f);
                yield return null;
                _uiWrong = Took("a pick on one street, then a drag on another", said);
                if (_uiWrong == null && grid.Columns[_mode.Site.FirstColumn].Strip != grid.Columns[second].Strip)
                {
                    _uiWrong = "a drag on another street kept the first street's squares";
                }
                if (_uiWrong != null)
                {
                    yield break;
                }
            }

            // Squares that make no site: the reason, how to fix it, what is known, Clear as a button.
            _mode.ClearForTest();
            _mode.PickForTest(free, 1, 1);
            yield return null;
            _uiWrong = Measured("one square picked (no site)");
            said.Append("; one square alone: '").Append(_mode.HintShown).Append('\'');
            if (_uiWrong != null)
            {
                yield break;
            }

            // Land for sale under Zone: buy it first; Buy land takes the pick on.
            if (!_mode.ToolGreyed(BuildTool.Zone, out _))
            {
                _mode.UseTool(BuildTool.Zone);
                _mode.PickForTest(free, 2, 0);
                yield return null;
                string buyFirst = ConstructionText.T(BuildModeHintRules.BuyFirst);
                said.Append("; land for sale under Zone: '").Append(_mode.HintShown).Append('\'');
                if (_mode.HintShown != buyFirst)
                {
                    _uiWrong = "land for sale under Zone: the hint says '" + _mode.HintShown + "', not '" + buyFirst + "'";
                    yield break;
                }
                _uiWrong = Measured("land for sale under Zone");
                if (_uiWrong != null)
                {
                    yield break;
                }
                int cells = _mode.PickedCells;
                _mode.GoBuyForTest();
                yield return null;
                if (_mode.State.Tool != BuildTool.Land || _mode.PickedCells != cells)
                {
                    _uiWrong = "Buy land from the Zone panel did not take the " + cells + " squares picked on (" + _mode.State.Tool + ", " + _mode.PickedCells + " squares)";
                    yield break;
                }
                said.Append(", then Buy land with them: '").Append(_mode.HintShown).Append('\'');
            }
            _mode.ClearForTest();
            _mode.UseTool(BuildTool.Land);
        }

        // The pick a drag made: a site no wider than a site may be, measured; its words added.
        private string Took(string what, StringBuilder said)
        {
            CellSite site = _mode.Site;
            said.Append("; ").Append(what).Append(": ").Append(site.Count).Append(" space(s), ").Append(_mode.PickedCells).Append(" squares");
            string note = BuildModeHintRules.TookKey(BuildTool.Land, _mode.Took);
            if (note != null)
            {
                said.Append(" ('").Append(ConstructionText.T(note)).Append("')");
            }
            if (site.Count <= 0 || site.Count > BuildCatalogue.SiteSpacesMost)
            {
                return what + ": the squares make no site (" + _mode.HintShown + ")";
            }
            return Measured(what);
        }

        // A column on another strip of the same block: any (forSale false), or one for sale.
        private int OtherStrip(int column, bool forSale)
        {
            CellGrid grid = Cells;
            CellColumn c = grid.Columns[column];
            for (int i = 0; i < grid.Columns.Count; i++)
            {
                CellColumn o = grid.Columns[i];
                if (o.Block != c.Block || o.Strip == c.Strip)
                {
                    continue;
                }
                if (!forSale || LotOfColumn(i, out LotView lot) && FreeSpace(lot))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
