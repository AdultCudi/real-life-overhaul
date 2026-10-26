using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for assembling and splitting land (docs/features/construction.md, "Assembly and splitting"): the
    /// player's sites along one side of a block joined into one (free spaces bought, ground cleared by a demolition, or both),
    /// up to the widest the district's grid takes, so one building of the city's designs stands over all of them; the next free
    /// space bought and joined in one press; and a site split back into its lots, each a site of its own with its own address,
    /// for smaller buildings. Never whole blocks at once (the owner: joining what you own), never in a Going Public session.
    /// On a press; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<LotView> _joinNext = new List<LotView>(8);
        private readonly long[] _weights = new long[BuildCatalogue.SiteSpacesMost];
        private readonly long[] _shares = new long[BuildCatalogue.SiteSpacesMost];

        /// <summary>
        /// Why the player's site cannot be joined with the land after it along its block's side, as a text key; null when it can,
        /// with the next site's first lot in <paramref name="nextId"/>. "reallife_bld_answer_join_buy_first" when the next is a free
        /// space for sale (<see cref="BuyAndJoin"/> takes it).
        /// </summary>
        public string JoinBlock(string firstId, out string nextId)
        {
            nextId = null;
            if (GoingPublic.InSession)
            {
                return "reallife_bld_answer_session_land";
            }
            LotFile a = _state.Lot(firstId);
            if (a == null || !SiteSpaces(firstId, a.Spaces, _strip))
            {
                return "reallife_bld_answer_no_lot";
            }
            if (_state.OnLot(firstId) != null)
            {
                return "reallife_bld_answer_lot_busy";
            }
            if (!After(_strip[_strip.Count - 1], out LotView next))
            {
                return "reallife_bld_answer_join_nothing";
            }
            nextId = next.Id;
            LotFile b = _state.Lot(next.Id);
            if (b == null)
            {
                return FreeSpace(next) ? "reallife_bld_answer_join_buy_first" : "reallife_bld_answer_join_nothing";
            }
            if (_state.OnLot(next.Id) != null)
            {
                return "reallife_bld_answer_lot_busy";
            }
            if (!SiteSpaces(next.Id, b.Spaces, _joinNext))
            {
                return "reallife_bld_answer_no_lot";
            }
            return RedevelopRules.Joins(Joined(_strip).Spaces, Joined(_joinNext).Spaces, a.Spaces + b.Spaces) ? null : "reallife_bld_answer_join_too_wide";
        }

        /// <summary>The player's site joined with their site after it along the block's side: one site, one building over both. Returns a text key.</summary>
        public string Join(string firstId)
        {
            string block = JoinBlock(firstId, out string nextId);
            if (block != null)
            {
                return block;
            }
            LotFile a = _state.Lot(firstId), b = _state.Lot(nextId);
            SiteSpaces(firstId, a.Spaces, _strip);
            SiteSpaces(nextId, b.Spaces, _joinNext);
            _strip.AddRange(_joinNext);
            LotView joined = Joined(_strip);
            int stripRows = CellRules.RowsFor(_strip[0].Depth);
            a.Rows = CellRules.JoinRows(a.Rows, a.Spaces, b.Rows, b.Spaces, stripRows);   // build mode's cells: each lot's rows kept
            a.Paint = a.Paint != 0 ? a.Paint : b.Paint;
            a.Spaces += b.Spaces;
            a.Paid += b.Paid;
            a.Zones |= b.Zones | LandRules.ZonesOf(joined.Class);   // what each was zoned for, and what a site of the joined spaces is
            a.Bought = Math.Min(a.Bought, b.Bought);
            a.EmptySince = a.EmptySince > 0 && b.EmptySince > 0 ? Math.Min(a.EmptySince, b.EmptySince) : Math.Max(a.EmptySince, b.EmptySince);
            a.TaxedTo = Math.Min(a.TaxedTo, b.TaxedTo);
            if (a.ZoneAsk == 0 && b.ZoneAsk != 0)
            {
                a.ZoneAsk = b.ZoneAsk;   // a use asked of the council for either is heard for the whole site
                a.ZoneDay = b.ZoneDay;
            }
            _state.Lots.Remove(b);
            Logger?.Info("Construction: the site at " + firstId + " joined with the one at " + nextId + ": " + a.Spaces + " lot(s), " + joined.Spaces + " space(s), " + joined.Width
                         + " m along the street.");
            Changed();
            return "reallife_bld_answer_joined";
        }

        /// <summary>The free space after the player's site bought at its price and joined to the site in one press. Returns a text key.</summary>
        public string BuyAndJoin(string firstId)
        {
            string block = JoinBlock(firstId, out string nextId);
            if (block != "reallife_bld_answer_join_buy_first")
            {
                return block ?? "reallife_bld_answer_join_owned";
            }
            LotFile a = _state.Lot(firstId);
            if (SiteSpaces(firstId, a.Spaces, _strip) && Space(nextId, out LotView next) && !RedevelopRules.Joins(Joined(_strip).Spaces, Math.Max(1, next.Spaces), a.Spaces + 1))
            {
                return "reallife_bld_answer_join_too_wide";
            }
            string bought = BuySite(nextId, 1);
            if (_state.Lot(nextId) == null)
            {
                return bought;   // not bought: why
            }
            string joined = Join(firstId);
            return joined == "reallife_bld_answer_joined" ? "reallife_bld_answer_bought_joined" : joined;
        }

        /// <summary>The price of the free space after the player's site, or 0 when there is none to buy.</summary>
        public long NextSpacePrice(string firstId)
        {
            return JoinBlock(firstId, out string nextId) == "reallife_bld_answer_join_buy_first" ? SitePrice(nextId, 1) : 0L;
        }

        /// <summary>Why the player's site cannot be split back into its lots, as a text key; null when it can.</summary>
        public string SplitBlock(string firstId)
        {
            if (GoingPublic.InSession)
            {
                return "reallife_bld_answer_session_land";
            }
            LotFile a = _state.Lot(firstId);
            if (a == null || a.Spaces <= 1)
            {
                return "reallife_bld_answer_split_one";
            }
            if (_state.OnLot(firstId) != null)
            {
                return "reallife_bld_answer_lot_busy";
            }
            if (_state.Lots.Count + a.Spaces - 1 > BuildCatalogue.LotsMost)
            {
                return "reallife_bld_answer_too_many_lots";
            }
            return SiteSpaces(firstId, a.Spaces, _strip) ? null : "reallife_bld_answer_no_lot";
        }

        /// <summary>The player's site split back into its lots, each a site of its own, what was paid shared by their land's worth. Returns a text key.</summary>
        public string Split(string firstId)
        {
            string block = SplitBlock(firstId);
            if (block != null)
            {
                return block;
            }
            LotFile a = _state.Lot(firstId);
            int n = Math.Min(_strip.Count, BuildCatalogue.SiteSpacesMost);
            for (int i = 0; i < n; i++)
            {
                _weights[i] = Math.Max(1L, LotPrice(_strip[i]));
            }
            RedevelopRules.Shares(a.Paid, _weights, n, _shares);
            int stripRows = CellRules.RowsFor(_strip[0].Depth);
            for (int i = 1; i < n; i++)
            {
                _state.Lots.Add(new LotFile
                {
                    Id = _strip[i].Id, Bought = a.Bought, Paid = _shares[i], Zones = a.Zones | LandRules.ZonesOf(_strip[i].Class), TaxedTo = a.TaxedTo, EmptySince = a.EmptySince, Spaces = 1,
                    Rows = CellRules.SplitRows(a.Rows, i, stripRows), Paint = a.Paint
                });
            }
            a.Rows = CellRules.SplitRows(a.Rows, 0, stripRows);
            a.Paid = _shares[0];
            a.Spaces = 1;
            a.Zones |= LandRules.ZonesOf(_strip[0].Class);
            Logger?.Info("Construction: the site at " + firstId + " split back into " + n + " site(s) of a lot each.");
            Changed();
            return "reallife_bld_answer_split";
        }
    }
}
