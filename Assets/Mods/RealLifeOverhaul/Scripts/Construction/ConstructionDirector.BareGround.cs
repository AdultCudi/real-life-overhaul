using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The load's check that no ground of the band is left unexplained (the owner, 27 Oct 2026: "There are empty spots in the block with
    /// no grids"): the grid's columns, the held grounds and every block's ground walked at 2 m, as BuildGroundRules says. One line in
    /// Player.log, "Build mode check (bare ground)", once a load with Developer diagnostics on (a FAIL says which column or which spot),
    /// and one on how it was spread, "Build mode check (bare ground, spread over frames)". Done whole in one frame it was 143 and
    /// 181 ms (the own-spike scan, 28 Oct 2026), so it goes a slice of <see cref="BareSliceMs"/> a frame (BareAudit), the same report
    /// to the last bit. COST: one test a frame while idle; while it runs, a slice a frame for a few seconds once a load, a second
    /// after the district stands.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const float BareStep = 2f;           // m between the spots of a block the check looks at
        private const double BareSliceMs = 1.0;      // ms of the check a frame may take while it runs, one step more at most

        private bool _bareChecked;           // this load's check is begun
        private readonly List<BlockView> _blocks = new List<BlockView>(96);
        private readonly BareAudit _bare = new BareAudit();
        private readonly SpreadWatch _bareSpread = new SpreadWatch();   // the frames the check took, for its self-check line
        private Func<int, BuildModeRules.ColumnFacts> _bareFacts;      // FactsOf, made once

        // On the second's look: once a load, when the district stands and Developer diagnostics are on, the check begun.
        private void BareGroundSecond()
        {
            if (_bareChecked || !Verbose || !DistrictLives())
            {
                return;
            }
            _bareChecked = true;
            try
            {
                BareGroundBegin();
            }
            catch (Exception e)
            {
                _bare.Stop();
                Fault("the bare ground check", e);
            }
        }

        // The check begun over what stands now; false when the district has no squares or its blocks are not known.
        private bool BareGroundBegin()
        {
            CellGrid grid = Cells;
            Action<List<BlockView>> blocks = BuildModeBridges.Blocks;
            _blocks.Clear();
            if (grid.Columns.Count == 0 || blocks == null)
            {
                return false;
            }
            blocks(_blocks);
            if (_blocks.Count == 0)
            {
                return false;
            }
            _bareFacts = _bareFacts ?? FactsOf;
            _bare.Begin(grid, _blocks, _bareFacts, BareStep);
            _bareSpread.Begin();
            return true;
        }

        // Every frame while the check runs: a slice of it. A grid made again under it (the lots changed) begins it again.
        private void BareGroundFrame()
        {
            if (ConstructionGame.Loading)
            {
                return;   // the game loads: the new load's own check comes after it (Load)
            }
            long began = Stopwatch.GetTimestamp();
            try
            {
                if (!ReferenceEquals(Cells, _bare.Grid))
                {
                    _bare.Stop();
                    BareGroundBegin();
                    return;
                }
                WorkSlice slice = WorkSlice.Of(BareSliceMs, 1);
                bool more = true;
                while (more && slice.More())
                {
                    long item = Stopwatch.GetTimestamp();
                    more = _bare.Step();
                    _bareSpread.Item(0, item);
                }
                _bareSpread.Frame(began);
                if (!more)
                {
                    BareGroundSaid(_bare.Report);
                }
            }
            catch (Exception e)
            {
                _bare.Stop();
                Fault("the bare ground check", e);
            }
        }

        private void BareGroundSaid(BareReport r)
        {
            if (r == null)
            {
                return;
            }
            if (!r.Ok)
            {
                Logger?.Warn(r.Line());
            }
            else
            {
                Logger?.Info(r.Line());
            }
            Logger?.Info("Build mode check (bare ground, spread over frames): " + r.Columns + " columns and " + r.Blocks + " blocks, a column's facts or a row of spots a step, "
                         + _bareSpread.Said("a step") + ": " + _bareSpread.Verdict(false) + ".");
        }
    }
}
