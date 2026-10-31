using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// What the Place tool offers when no design of the city's fits the squares picked (<see cref="PlotJoinRules"/>): the site beside the
    /// player's to join it with, what joining would give, and the pair to join. Read once when squares are picked.
    /// </summary>
    public struct JoinOffer
    {
        public int Kind;              // PlotJoinRules.Fits (nothing to offer), Offer, Useless or Alone
        public string Why;            // why no join is possible, as a text key (null while one is, or when the squares are only part of a site)
        public string First;          // the first site of the pair to join: Join(First) makes them one site
        public int PartnerColumn;     // the site it would be joined with: its first column and how many it takes, for the map's preview
        public int PartnerColumns;
        public int Columns;           // the columns the joined shape takes
        public int Designs;           // the designs the joined shape has for the use
        public int UseFits;           // a zoned use other than the site's that has designs on the squares as they are (BuildUse), -1 for none
    }

    /// <summary>
    /// The director's part for leftover land (the owner, 28 Oct 2026): a building placed on part of a site leaves single spaces, and a
    /// single space has designs for shops alone, so each read "no design". The Place tool asks here what could be done: join the leftover
    /// with the site of the player's beside it (the site after it first, then the one before it), with the designs the joined shape has;
    /// or, for a single space, the use that does have designs. On a pick; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<DesignChoice> _offerDesigns = new List<DesignChoice>(32);
        private readonly int[] _offerByUse = new int[(int)BuildUse.Count];

        /// <summary>
        /// The offer for squares picked on the player's land when no design fits them: <paramref name="site"/> the squares as a site,
        /// <paramref name="use"/> the use they are placed for. Only squares that make the player's whole site (a part of one is picked
        /// wider instead) are offered a join.
        /// </summary>
        public JoinOffer OfferFor(CellSite site, BuildUse use)
        {
            var offer = new JoinOffer { Kind = PlotJoinRules.Alone, UseFits = -1 };
            CellGrid grid = Cells;
            RowsHeld(site.FirstColumn, out LotFile own);
            if (own == null || !Space(own.Id, out LotView ownFirst))
            {
                offer.Kind = PlotJoinRules.Fits;
                return offer;
            }
            int ownColumn = grid.ColumnOf(ownFirst.Strip, ownFirst.Index);
            if (site.FirstColumn != ownColumn || site.Count != own.Spaces)
            {
                offer.Kind = PlotJoinRules.Fits;
                return offer;   // part of a site: more squares of it are picked, not a join
            }
            int stripRows = CellRules.RowsFor(ownFirst.Depth);
            int ownRows = CellRules.MinRows(own.Rows, own.Spaces, stripRows);
            string afterWhy = JoinBlock(own.Id, out string nextId);
            LotFile after = afterWhy == null ? _state.Lot(nextId) : null;
            string beforeWhy = null;
            LotFile before = null;
            if (Before(ownFirst, out LotView lastBefore) && (before = Holding(lastBefore.Id)) != null)
            {
                beforeWhy = JoinBlock(before.Id, out string beforeNext);
                before = beforeWhy == null && beforeNext == own.Id ? before : null;
            }
            int afterDesigns = 0, beforeDesigns = 0, afterColumn = -1, beforeColumn = -1;
            if (after != null && Space(after.Id, out LotView afterFirst))
            {
                afterColumn = grid.ColumnOf(afterFirst.Strip, afterFirst.Index);
                int rows = PlotJoinRules.Rows(site.MinRows, ownRows, CellRules.MinRows(after.Rows, after.Spaces, stripRows));
                afterDesigns = CountDesigns(ownColumn, own.Spaces + after.Spaces, rows, use);
            }
            if (before != null && Space(before.Id, out LotView beforeFirst))
            {
                beforeColumn = grid.ColumnOf(beforeFirst.Strip, beforeFirst.Index);
                int rows = PlotJoinRules.Rows(site.MinRows, ownRows, CellRules.MinRows(before.Rows, before.Spaces, stripRows));
                beforeDesigns = CountDesigns(beforeColumn, before.Spaces + own.Spaces, rows, use);
            }
            int side = PlotJoinRules.Side(after != null && afterColumn >= 0, afterDesigns, before != null && beforeColumn >= 0, beforeDesigns);
            offer.Kind = PlotJoinRules.Kind(0, side, side == PlotJoinRules.Before ? beforeDesigns : afterDesigns);
            if (side == PlotJoinRules.Neither)
            {
                offer.Why = afterWhy == "reallife_bld_answer_join_nothing" && beforeWhy != null ? beforeWhy : afterWhy;
            }
            else
            {
                LotFile partner = side == PlotJoinRules.Before ? before : after;
                int partnerColumn = side == PlotJoinRules.Before ? beforeColumn : afterColumn;
                PlotJoinRules.Shape(side, ownColumn, own.Spaces, partnerColumn, partner.Spaces, out _, out int columns);
                offer.First = side == PlotJoinRules.Before ? before.Id : own.Id;
                offer.PartnerColumn = partnerColumn;
                offer.PartnerColumns = partner.Spaces;
                offer.Columns = columns;
                offer.Designs = side == PlotJoinRules.Before ? beforeDesigns : afterDesigns;
            }
            bool busy = _state.OnLot(own.Id) != null || ParkOf(own.Id) != null;
            if (!busy)
            {
                int zones = ZonesNow(own);
                for (int u = 0; u < _offerByUse.Length; u++)
                {
                    _offerByUse[u] = u != (int)use && LandRules.Zoned(zones, (BuildUse)u) ? CountDesigns(site.FirstColumn, site.Count, site.MinRows, (BuildUse)u) : 0;
                }
                offer.UseFits = PlotJoinRules.OtherUse((int)use, zones, _offerByUse);
            }
            return offer;
        }

        // The designs a footprint of columns and rows has for a use, as build mode lists them (at most its window holds), without a log line.
        private int CountDesigns(int firstColumn, int columns, int rows, BuildUse use)
        {
            FootprintDesigns(firstColumn, columns, rows, use, _offerDesigns, true);
            return Math.Min(_offerDesigns.Count, BuildModeCatalogue.Most);
        }
    }
}
