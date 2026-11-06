using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The land market, the rivals' side (the owner: whatever the player can do, rivals and NPCs must be able to do too). A developer
    /// short of cash sells a site it cannot afford before its works begin, back to the city at the same fair price the player gets, or to
    /// another developer that has the means, at what it paid and a tenth; a developer whose site the city's rules then drop sells the
    /// land back to the city too. A developer offers for the player's land that has stood unused when it stands in the way of a site it
    /// wants or lies beside one it holds, at the land's market value and a premium, and the player accepts or refuses in the Land tab.
    /// Slow and the same on every machine: one move every eight to twelve days and three in thirty at the most, drawn from the save's seed
    /// and the day, none in a Going Public session (the day's pass does not call this there). COST: nothing per frame; the move's day
    /// reads the grown sites and the player's few lots once.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private struct LandCandidate
        {
            public string Lot;
            public LandReason Reason;
            public BuildUse Use;
            public int Rival;
        }

        private readonly List<LandCandidate> _landCandidates = new List<LandCandidate>(8);
        private readonly List<int> _landBuyers = new List<int>(BuildCatalogue.RivalCount);

        // ------------------------------------------------------------------ the move

        private void LandMarketMoves(int day)
        {
            if (_state.NextMarketDay <= 0)
            {
                _state.NextMarketDay = day + LandMarketRules.FirstDay;   // the first look: the market is counted first
                return;
            }
            if (day < _state.NextMarketDay)
            {
                return;
            }
            if (LandMarketRules.MonthFull(_state.MarketMoves, _state.MarketMoves.Length, day))
            {
                _state.NextMarketDay = day + LandMarketRules.GapLeast;
                return;
            }
            bool offerFirst = LandMarketRules.MoveKind(_state.Seed, day) == 1;
            bool moved = offerFirst ? LandOfferOnPlayers(day, null, true) != null || LandSellOff(day) : LandSellOff(day) || LandOfferOnPlayers(day, null, true) != null;
            if (moved)
            {
                _state.NoteMove(day);
                _state.NextMarketDay = LandMarketRules.NextMove(_state.Seed, day);
            }
            else
            {
                _state.NextMarketDay = day + 2;   // nothing to do today, or the city's designs are being read: looked at again soon
            }
        }

        // ------------------------------------------------------------------ rivals sell what they cannot afford

        /// <summary>
        /// A developer short of cash sells one site whose works have not begun: to another developer that has the means (at what it paid
        /// and a tenth), else back to the city at the fair price. False when none is short. A test calls it for any site through
        /// <paramref name="only"/>.
        /// </summary>
        internal bool LandSellOff(int day, GrowFile only = null)
        {
            GrowFile seller = null;
            for (int i = 0; i < _state.Grows.Count && seller == null; i++)
            {
                GrowFile g = _state.Grows[i];
                bool mine = only == null ? (g.Stage == GrowStage.Plans || g.Stage == GrowStage.Planning) && g.Owner == GrowOwner.Builder && ShortOfCash(g) : ReferenceEquals(g, only);
                seller = mine ? g : null;
            }
            if (seller == null)
            {
                return false;
            }
            _landBuyers.Clear();
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                LandHeldBy(r, string.Empty, out int sites);
                if (r != seller.Rival && sites < 2 && !ShortOfCash(r, string.Empty))
                {
                    _landBuyers.Add(r);
                }
            }
            bool toRival = _landBuyers.Count > 0 && BuildRandom.Of(_state.Seed, seller.Id, 0x3A55).Chance(600);
            if (toRival)
            {
                int buyer = _landBuyers[RedevelopRules.PickOf(_state.Seed, day, 0x3B, _landBuyers.Count)];
                int from = seller.Rival;
                seller.Rival = buyer;
                seller.Holding = string.Empty;
                seller.Price = LandMarketRules.TradePrice(seller.Price);   // what the buyer paid is what the next ask rests on
                GrowsChanged();
                Paper("reallife_land_paper_traded", GrowName(seller), seller.Spaces);
                Logger?.Info("Construction: " + ConstructionText.T(BuildCatalogue.RivalKeys[from]) + " is short of cash and sold the site at " + seller.Lot + " to "
                             + ConstructionText.T(BuildCatalogue.RivalKeys[buyer]) + " for " + seller.Price + "; its plans go on.");
            }
            else
            {
                seller.Stage = GrowStage.Dropped;
                seller.Until = day;
                GrowsChanged();
                LandSoldBack(seller);
            }
            Changed();
            return true;
        }

        /// <summary>The land of a site a developer gives up bought back by the city at the fair price: the ledger and the City Ledger's story. Nothing for the player's own.</summary>
        private void LandSoldBack(GrowFile g)
        {
            if (g.Owner == GrowOwner.Player || _state == null)
            {
                return;
            }
            long price = LandMarketRules.CityPrice(g.Price, ValueOf(g.Lot, g.Spaces, string.Empty));
            _state.CityLandBack += price;
            Paper("reallife_land_paper_back", GrowName(g), g.Spaces);
            Logger?.Info("Construction: the city bought the land at " + g.Lot + " (" + g.Spaces + " space(s)) back from " + GrowDeveloper(g) + " for " + price + " (it paid " + g.Price + ").");
        }

        // ------------------------------------------------------------------ rivals offer for the player's land

        /// <summary>
        /// A developer's offer for a site of the player's that has stood unused, when it stands in the way of a site the developer wants
        /// (free land lies beside it and the use is wanted in its district) or lies beside a site a developer holds. Null when no land
        /// qualifies, two offers already wait, or the city's designs are being read (then it is asked again). <paramref name="only"/>
        /// names the one site a test offers on, whether or not it has stood unused; <paramref name="notice"/> tells the player.
        /// </summary>
        internal LandOffer LandOfferOnPlayers(int day, string only, bool notice)
        {
            if (_state.TheirsWaiting() >= 2 && only == null)
            {
                return null;
            }
            _landCandidates.Clear();
            for (int i = 0; i < _state.Lots.Count; i++)
            {
                LotFile own = _state.Lots[i];
                if (only != null ? !string.Equals(own.Id, only, StringComparison.Ordinal) : !LandMarketRules.Unused(own.EmptySince, day) || own.ZoneAsk != 0)
                {
                    continue;
                }
                LandOffer last = _state.LastOn(LandOfferSide.Theirs, own.Id);
                if (only == null && last != null && (last.Waiting || (last.State != LandOfferState.Accepted && LandMarketRules.Resting(last.Answered > 0 ? last.Answered : last.Made, day))))
                {
                    continue;
                }
                if (SellBlock(own.Id) != null)
                {
                    continue;   // something is planned or built there, or the city would not buy it: no developer wants it either
                }
                LandCandidate c = WantedBy(own);
                if (c.Lot == null && only != null)
                {
                    c = ForcedWant(own);   // a test's site: the first use it is zoned for, by the builder of that use
                }
                if (c.Lot != null)
                {
                    _landCandidates.Add(c);
                }
            }
            for (int k = 0; k < _landCandidates.Count; k++)
            {
                LandCandidate c = _landCandidates[(RedevelopRules.PickOf(_state.Seed, day, 0x3C, _landCandidates.Count) + k) % _landCandidates.Count];
                if (!Lot(c.Lot, out LotView site))
                {
                    continue;
                }
                if (!GrowDesign(site, c.Use, GrowTier.Standard, false, out string design))
                {
                    return null;   // the city's designs are being looked through: the next pass
                }
                if (design == NoDesign)
                {
                    continue;   // nothing of the city's fills the player's ground: no developer would want it for that
                }
                int id = _state.NewId();
                LotFile own = _state.Lot(c.Lot);
                long price = LandMarketRules.OfferPrice(SiteValue(c.Lot), c.Reason, _state.Seed, id);
                var o = new LandOffer
                {
                    Id = id, Side = LandOfferSide.Theirs, State = LandOfferState.Waiting, Lot = c.Lot, Spaces = own.Spaces, Rival = c.Rival, Use = c.Use, Reason = c.Reason, Design = design,
                    Price = price, Made = day, Due = LandMarketRules.Due(day)
                };
                _state.Offers.Add(o);
                _state.TidyOffers();
                Logger?.Info("Construction: " + OfferDeveloper(o) + " offers " + price + " for the player's land at " + c.Lot + " (" + c.Reason + ", to build " + c.Use + " in " + design
                             + "); the player has until day " + o.Due + ".");
                if (notice)
                {
                    MarketTell(NotificationType.Info, "reallife_land_notice_offer_in", LotWords(c.Lot), OfferDeveloper(o), price, o.Id);
                }
                Changed();
                return o;
            }
            return null;
        }

        // A test's site wanted for the first use it is zoned for; nothing when it is zoned for none.
        private LandCandidate ForcedWant(LotFile own)
        {
            int zones = ZonesNow(own);
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if (LandRules.Zoned(zones, (BuildUse)u))
                {
                    return new LandCandidate { Lot = own.Id, Reason = LandReason.Blocks, Use = (BuildUse)u, Rival = DevelopmentRules.Rival(own.Id, (BuildUse)u) };
                }
            }
            return new LandCandidate();
        }

        // What a developer wants a site of the player's for, if anything: a use its district asks for on land next to free land, or beside a site a developer holds.
        private LandCandidate WantedBy(LotFile own)
        {
            var none = new LandCandidate();
            if (!SiteSpaces(own.Id, own.Spaces, _marketStrip))
            {
                return none;
            }
            LotView first = _marketStrip[0], last = _marketStrip[_marketStrip.Count - 1];
            GrowFile beside = null;
            bool free = false;
            if (Before(first, out LotView before))
            {
                beside = BesideOf(before.Id);
                free |= FreeSpace(before);
            }
            if (After(last, out LotView after))
            {
                beside = beside ?? BesideOf(after.Id);
                free |= FreeSpace(after);
            }
            int zones = ZonesNow(own);
            if (beside != null && LandRules.Zoned(zones, beside.Use))
            {
                return new LandCandidate { Lot = own.Id, Reason = LandReason.Joins, Use = beside.Use, Rival = beside.Rival };
            }
            if (!free)
            {
                return none;
            }
            int best = -1, margin = int.MinValue;
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                int demand = DemandFor((BuildUse)u, first.District);
                if (LandRules.Zoned(zones, (BuildUse)u) && demand >= 0 && demand >= GrowRules.Threshold[u] && demand - GrowRules.Threshold[u] > margin)
                {
                    best = u;
                    margin = demand - GrowRules.Threshold[u];
                }
            }
            return best < 0 ? none : new LandCandidate { Lot = own.Id, Reason = LandReason.Blocks, Use = (BuildUse)best, Rival = DevelopmentRules.Rival(own.Id, (BuildUse)best) };
        }
    }
}
