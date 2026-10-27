using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The land market in the save's line (docs/features/construction.md, "Land market"): "^m:" a head, its own version first, then the
    /// day the rivals' land trading may next move, the money the city has paid for land sold back and the days of the latest moves,
    /// and after it one record for each offer, waiting or lately answered (its fields in order, the rest of the record's text fields
    /// escaped at the end). Fields are appended at the end of a record, a head's after its last; a record of a newer version is read as
    /// far as this one knows it, a part of a version this one does not know is passed over, and a line without the part has no offers.
    /// Written only once there is something in it, so a save that never used the market is as before.
    /// </summary>
    public static partial class BuildStore
    {
        /// <summary>The market part's own version: raised when a field is added to its head.</summary>
        public const int MarketVersion = 1;

        private static void WriteMarket(StringBuilder t, BuildState s)
        {
            bool moved = false;
            for (int i = 0; i < s.MarketMoves.Length; i++)
            {
                moved |= s.MarketMoves[i] > 0;
            }
            if (s.Offers.Count == 0 && s.NextMarketDay <= 0 && s.CityLandBack <= 0L && !moved)
            {
                return;
            }
            t.Append("^m:").Append(MarketVersion.ToString(Inv)).Append('|').Append(s.NextMarketDay.ToString(Inv)).Append('|').Append(s.CityLandBack.ToString(Inv)).Append('|');
            for (int i = 0; i < s.MarketMoves.Length; i++)
            {
                t.Append(i > 0 ? "," : string.Empty).Append(s.MarketMoves[i].ToString(Inv));
            }
            for (int i = Math.Max(0, s.Offers.Count - BuildState.OffersMost); i < s.Offers.Count; i++)
            {
                LandOffer o = s.Offers[i];
                t.Append(';');
                Join(t, o.Id, (int)o.Side, (int)o.State, o.Spaces, o.Grow, o.Rival, (int)o.Use, (int)o.Reason, o.Made, o.Due, o.Raised, o.Answered);
                t.Append('|').Append(o.Price.ToString(Inv)).Append('|').Append(o.Ask.ToString(Inv));
                t.Append('|').Append(Esc(o.Lot)).Append('|').Append(Esc(o.Holding)).Append('|').Append(Esc(o.Design));
            }
        }

        // The land market's part; false for a part of another's.
        private static bool ReadMarket(char part, string body, BuildState s)
        {
            if (part != 'm')
            {
                return false;
            }
            string[] records = body.Split(';');
            var head = new Fields(records[0]);
            int version = head.I(0);
            if (version < 1)
            {
                return true;   // a part this reader cannot place: passed over, the market starts afresh
            }
            s.NextMarketDay = Math.Max(0, head.I(0));
            s.CityLandBack = Math.Max(0L, head.L(0L));
            string[] days = head.S().Split(',');   // the days of the latest moves: plain numbers
            for (int i = 0; i < s.MarketMoves.Length && i < days.Length; i++)
            {
                s.MarketMoves[i] = int.TryParse(days[i], NumberStyles.Integer, Inv, out int d) ? Math.Max(0, d) : 0;
            }
            for (int i = 1; i < records.Length && s.Offers.Count < BuildState.OffersMost; i++)
            {
                if (records[i].Length == 0)
                {
                    continue;
                }
                var r = new Fields(records[i]);
                var o = new LandOffer
                {
                    Id = r.I(0), Side = (LandOfferSide)BuildCatalogue.Clamp(r.I(0), 0, (int)LandOfferSide.Theirs),
                    State = (LandOfferState)BuildCatalogue.Clamp(r.I(0), 0, (int)LandOfferState.Withdrawn), Spaces = BuildCatalogue.Clamp(r.I(1), 1, BuildCatalogue.SiteSpacesMost),
                    Grow = Math.Max(0, r.I(0)), Rival = BuildCatalogue.Clamp(r.I(0), 0, BuildCatalogue.RivalCount - 1), Use = (BuildUse)BuildCatalogue.Clamp(r.I(0), 0, (int)BuildUse.Count - 1),
                    Reason = (LandReason)BuildCatalogue.Clamp(r.I(0), 0, (int)LandReason.Joins), Made = r.I(0), Due = r.I(0), Raised = BuildCatalogue.Clamp(r.I(0), 0, LandMarketRules.RaisesMost),
                    Answered = Math.Max(0, r.I(0))
                };
                o.Price = Math.Max(0L, r.L(0L));
                o.Ask = Math.Max(0L, r.L(0L));
                o.Lot = r.S();
                o.Holding = r.S();
                o.Design = r.S();
                if (o.Id > 0 && o.Lot.Length > 0 && o.Price > 0L && s.Offer(o.Id) == null)
                {
                    s.Offers.Add(o);
                }
            }
            return true;
        }
    }
}
