using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The load's check that no ground of the band is left unexplained (the owner, 27 Oct 2026: "There are empty spots in the block with
    /// no grids"): the grid's columns, the held grounds and every block's ground walked at 2 m, as BuildGroundRules says. One line in
    /// Player.log, "Build mode check (bare ground)", once a load with Developer diagnostics on (a FAIL says which column or which spot
    /// and is written at that moment). COST: nothing per frame; once a load, a second after the district stands, a few tens of
    /// milliseconds.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const float BareStep = 2f;   // m between the spots of a block the check looks at

        private bool _bareChecked;           // this load's line is written
        private readonly List<BlockView> _blocks = new List<BlockView>(96);

        // On the second's look: once a load, when the district stands and Developer diagnostics are on.
        private void BareGroundSecond()
        {
            if (_bareChecked || !Verbose || !DistrictLives())
            {
                return;
            }
            _bareChecked = true;
            try
            {
                BareGroundCheck();
            }
            catch (Exception e)
            {
                Fault("the bare ground check", e);
            }
        }

        /// <summary>The bare ground check over what stands now, its report; null when the district has no squares or its blocks are not known.</summary>
        public BareReport BareGroundCheck()
        {
            CellGrid grid = Cells;
            Action<List<BlockView>> blocks = BuildModeBridges.Blocks;
            _blocks.Clear();
            if (grid.Columns.Count == 0 || blocks == null)
            {
                return null;
            }
            blocks(_blocks);
            if (_blocks.Count == 0)
            {
                return null;
            }
            BareReport r = BuildGroundRules.Audit(grid, _blocks, FactsOf, BareStep);
            if (!r.Ok)
            {
                Logger?.Warn(r.Line());
            }
            else
            {
                Logger?.Info(r.Line());
            }
            return r;
        }
    }
}
