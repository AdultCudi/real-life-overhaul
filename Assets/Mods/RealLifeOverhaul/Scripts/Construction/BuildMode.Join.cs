using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// What the Place tool says when no design of the city's fits the squares picked (the owner, 28 Oct 2026: a building took part of a
    /// block and each single plot left over read "no design", with no way out): the plot beside it to join with, shown on the map
    /// beside the squares picked, what the joined plot would have, and a button that joins them (the Join tool's own join). Read once
    /// when squares are picked. COST: nothing per frame.
    /// </summary>
    public sealed partial class BuildMode
    {
        private JoinOffer _offer;           // what the squares picked are offered while no design fits them
        private bool _designsRead;          // the city's designs were read for them (not still being looked through)

        private void OfferClear()
        {
            _offer = default(JoinOffer);
            _offer.UseFits = -1;
            _designsRead = false;
        }

        // The squares picked own no design: a join that gives some, offered when one is possible.
        private void OfferRead()
        {
            if (_designsRead && _designs.Count == 0 && !_state.ViewOnly)
            {
                _offer = _d.OfferFor(_site, _use);
            }
        }

        /// <summary>The join offered now (PlotJoinRules.Fits for none), for the tests.</summary>
        public int OfferKind => _offer.Kind;

        /// <summary>The designs the joined plot would have, for the tests.</summary>
        public int OfferDesigns => _offer.Designs;

        // The partner plot's squares drawn as the ghost's cells, so the joined shape reads on the map.
        private void JoinPreview(CellGrid grid)
        {
            if (_offer.Kind != PlotJoinRules.Offer && _offer.Kind != PlotJoinRules.Useless)
            {
                return;
            }
            int at = _offer.PartnerColumn;
            for (int i = 0; i < _offer.PartnerColumns && at >= 0 && at < _cellStart.Length; i++, at = grid.Next(at))
            {
                int rows = System.Math.Min(grid.Columns[at].Rows, System.Math.Max(1, _d.RowsHeld(at, out _)));
                for (int r = 0; r < rows; r++)
                {
                    _fills[_cellStart[at] + r] = (byte)BuildModeRules.Footprint;
                    _lines[_cellStart[at] + r] = BuildModeRules.LinePicked;
                }
            }
        }

        // The panel for squares no design fits: why, the join that would help, the use that already has designs, and the buttons.
        private void WriteNoDesign(BuildModeSheet s, bool parks)
        {
            s.Line(T("reallife_bld_mode_place_none"), BuildModeBar.Red);
            bool join = _offer.Kind == PlotJoinRules.Offer;
            if (join)
            {
                s.Line(ConstructionText.Fill(PlotJoinRules.OfferKey, "columns", ConstructionText.N(_offer.Columns), "designs", ConstructionText.N(_offer.Designs)), BuildModeBar.Good);
            }
            else if (_offer.Kind == PlotJoinRules.Useless)
            {
                s.Line(T(PlotJoinRules.UselessKey), BuildModeBar.Soft);
            }
            else if (_offer.Kind == PlotJoinRules.Alone && _offer.Why != null)
            {
                s.Line(T(_offer.Why), BuildModeBar.Soft);
            }
            if (_offer.UseFits >= 0 && _offer.UseFits < (int)BuildUse.Count)
            {
                s.Line(ConstructionText.Fill(PlotJoinRules.UseKey, "use", T(BuildCatalogue.UseKeys[(int)_use]), "other", T(BuildCatalogue.UseKeys[_offer.UseFits])), BuildModeBar.Soft);
            }
            if (join)
            {
                s.Buttons(RowJoinNext, T(PlotJoinRules.ButtonKey), !_state.ViewOnly, RowClear, T("reallife_bld_sheet_clear"), true);
                if (parks)
                {
                    s.Buttons(RowCarPark, T("reallife_park_button"), true, 0, null, false);
                }
                return;
            }
            s.Buttons(parks ? RowCarPark : 0, parks ? T("reallife_park_button") : null, parks, RowClear, T("reallife_bld_sheet_clear"), true);
        }

        // Join with the next plot pressed: the Join tool's join, then the squares picked again for the joined plot.
        private void ActJoinNext()
        {
            if (_offer.Kind != PlotJoinRules.Offer || string.IsNullOrEmpty(_offer.First))
            {
                return;
            }
            string answer = _d.Join(_offer.First);
            ClearPick();
            Refresh(answer);
        }
    }
}
