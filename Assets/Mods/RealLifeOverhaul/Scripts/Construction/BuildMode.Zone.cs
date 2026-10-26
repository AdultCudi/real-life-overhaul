using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The Zone tool square by square (plans/growables.md section 1, as Cities Skylines' zoning tools): four ways of picking the
    /// player's own squares in the tool panel, Fill side (a click fills every square of theirs along that block side, to the depth
    /// they hold), Rectangle (a drag marks the rectangle between the press and the pointer along the press's street), Brush (a
    /// drag marks each square the pointer passes over) and Unzone (a rectangle whose zoning is taken away). With a zone chosen the
    /// marked squares are painted on the release; with none, they stay marked until a zone row is pressed. A use the council does
    /// not allow on a site is asked of City Hall after the game's question box, its fee named, as before. A press on land that is
    /// not the player's picks as before, so the panel says to buy it first. COST: nothing per frame; a pass over a strip's squares
    /// on each look while dragging (twenty times a second), the marks one flag a square, kept between openings.
    /// </summary>
    public sealed partial class BuildMode : ZoneRules.IHeld
    {
        private ZoneShape _shape = ZoneShape.Rect;
        private bool[] _zoneMarks = new bool[0];        // a flag a square (by the layer's cell index): marked by the Zone tool
        private bool[] _zoneBefore = new bool[0];       // ... as they were when the press began
        private int _zoneMarked;
        private bool _zoneDragging;                     // the press began on the player's own land: the shapes pick
        private readonly List<CellAt> _zoneShape = new List<CellAt>(64);
        private readonly List<CellAt> _zoneCells = new List<CellAt>(64);

        /// <summary>The way the Zone tool picks squares now.</summary>
        public ZoneShape Shape => _shape;

        /// <summary>Squares the Zone tool has marked now, for the tests.</summary>
        public int ZoneMarked => _zoneMarked;

        /// <summary>The rows of a column the player holds (ZoneRules.IHeld): the tools pick only their own squares.</summary>
        public int Held(int column) => _d != null ? _d.RowsHeld(column, out _) : 0;

        // The marks made to fit the layer's squares (after a load made the grid again).
        private void ZoneFit()
        {
            int cells = _fills.Length;
            if (_zoneMarks.Length != cells)
            {
                _zoneMarks = new bool[cells];
                _zoneBefore = new bool[cells];
                _zoneMarked = 0;
            }
        }

        private void ZoneClear()
        {
            ZoneFit();
            Array.Clear(_zoneMarks, 0, _zoneMarks.Length);
            _zoneMarked = 0;
            _zoneDragging = false;
        }

        // A press with the Zone tool: on the player's own land the shapes pick (a new pick unless shift adds to it); elsewhere false,
        // and the press picks as the other tools do (so the panel says to buy the land first).
        private bool ZonePressed(CellAt cell)
        {
            ZoneFit();
            _zoneDragging = !cell.None && cell.Column < _facts.Length && Held(cell.Column) > cell.Row;
            if (!_zoneDragging)
            {
                ZoneClear();
                return false;
            }
            if (!BuildModeGame.Shift && _shape != ZoneShape.Brush)
            {
                Array.Clear(_zoneMarks, 0, _zoneMarks.Length);   // a new rectangle or fill; shift adds to what is marked
            }
            Array.Copy(_zoneMarks, _zoneBefore, _zoneMarks.Length);
            ClearDepths();
            return true;
        }

        // The marks as the shape makes them from the press to the pointer; false when the press was not on the player's land.
        private bool ZoneDrag()
        {
            if (!_zoneDragging)
            {
                return false;
            }
            CellGrid grid = _d.Cells;
            if (_shape == ZoneShape.Brush)
            {
                if (ZoneRules.Brush(grid, _to, this))
                {
                    Mark(_to.Column, _to.Row, true);   // the brush adds as it goes
                }
                if (ZoneRules.Brush(grid, _from, this))
                {
                    Mark(_from.Column, _from.Row, true);
                }
            }
            else
            {
                Array.Copy(_zoneBefore, _zoneMarks, _zoneMarks.Length);
                if (_shape == ZoneShape.Fill)
                {
                    ZoneRules.FillSide(grid, _to.Column, this, _zoneShape);
                }
                else
                {
                    ZoneRules.Rect(grid, _from, _to, this, _zoneShape);
                }
                for (int i = 0; i < _zoneShape.Count; i++)
                {
                    Mark(_zoneShape[i].Column, _zoneShape[i].Row, true);
                }
            }
            ZoneGather();
            _took = CellRules.TookAll;
            return true;
        }

        private void Mark(int column, int row, bool on)
        {
            if (column < 0 || column >= _cellStart.Length)
            {
                return;
            }
            int at = _cellStart[column] + row;
            if (at >= 0 && at < _zoneMarks.Length && _zoneMarks[at] != on)
            {
                _zoneMarks[at] = on;
            }
        }

        // The marks read back: their count, the squares as pairs, and the picks the hint bar and the panel read (each column's
        // deepest square marked; the site of the first one is the tool's site).
        private void ZoneGather()
        {
            _zoneCells.Clear();
            for (int c = 0; c < _depth.Length; c++)
            {
                _depth[c] = -1;
                int rows = _d.Cells.Columns[c].Rows;
                for (int r = 0; r < rows; r++)
                {
                    if (_cellStart[c] + r < _zoneMarks.Length && _zoneMarks[_cellStart[c] + r])
                    {
                        _zoneCells.Add(new CellAt(c, r));
                        _depth[c] = r;
                    }
                }
            }
            _zoneMarked = _zoneCells.Count;
            Gather();
        }

        // The squares marked painted with a zone (or none): a use the council does not allow on a site of them asked of City Hall after
        // the game's question box with its fee; then the marks let go, as Cities Skylines' zoning is done on the release.
        private void PaintMarked(int brush)
        {
            if (_zoneMarked == 0 || _state.ViewOnly)
            {
                return;
            }
            int use = brush >= (int)BuildUse.Count || _shape == ZoneShape.Unzone ? -1 : brush;
            var cells = new List<CellAt>(_zoneCells);
            bool hall = false;
            for (int i = 0; i < cells.Count && use >= 0 && !hall; i++)
            {
                _d.RowsHeld(cells[i].Column, out LotFile site);
                hall = site != null && !LandRules.Zoned(_d.ZonesNow(site), (BuildUse)use) && (site.ZoneAsk & BuildCatalogue.Zone((BuildUse)use)) == 0;
            }
            Action paint = () =>
            {
                string answer = _d.PaintCells(cells, use);
                ZoneClear();
                ClearPick();
                Refresh(answer);
            };
            if (hall)
            {
                Ask("reallife_bld_zone_confirm", "reallife_bld_zone_confirm_body", ConstructionDirector.ZoneFee, paint);
                return;
            }
            paint();
        }

        // The marks drawn over the layer, in the picked colour.
        private void ZoneColour()
        {
            for (int i = 0; i < _zoneMarks.Length && i < _fills.Length; i++)
            {
                if (_zoneMarks[i])
                {
                    _fills[i] = (byte)BuildModeRules.Picked;
                    _lines[i] = (byte)BuildModeRules.LinePicked;
                }
            }
        }

        // A way of picking chosen in the panel: the marks kept (Unzone takes them away at once when some are marked).
        private void ShapePicked(int shape)
        {
            _shape = (ZoneShape)Math.Max(0, Math.Min((int)ZoneShape.Count - 1, shape));
            if (_shape == ZoneShape.Unzone && _zoneMarked > 0)
            {
                PaintMarked(BrushNone);
                return;
            }
            Write(false);
        }

        /// <summary>A way of picking chosen as its button does (for the tests).</summary>
        public void UseShape(ZoneShape shape) => ShapePicked((int)shape);

        /// <summary>
        /// The Zone tool's shape worked from a press on a square to a release over another (for the tests), as OnUpdate reads them;
        /// with <paramref name="paint"/> the marks are painted with it (a use, -1 none) at once, without the question box. The squares marked.
        /// </summary>
        public int ZoneForTest(CellAt from, CellAt to, int paint, bool apply, out string answer)
        {
            answer = null;
            if (!_state.Open || _state.Tool != BuildTool.Zone || !ZonePressed(from))
            {
                return 0;
            }
            _from = from;
            _to = to;
            ZoneDrag();
            int marked = _zoneMarked;
            if (apply)
            {
                answer = _d.PaintCells(new List<CellAt>(_zoneCells), paint);
                ZoneClear();
                ClearPick();
                Refresh(null);
            }
            return marked;
        }
    }
}
