using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>What a rival developer asks for a site it holds: the ask, the range the player is shown, and what it rests on.</summary>
    public struct LandAsk
    {
        public long Ask, Low, High;   // money; the ask lies in the range, not in its middle
        public int Premium;           // thousandths over what it paid
        public bool OnlySite;         // the only site the developer holds
        public bool Short;            // the developer is short of cash
    }

    /// <summary>
    /// The land market, the player's side (docs/features/construction.md, "Land market"; the rules in <see cref="LandMarketRules"/>): land sold
    /// back to the city at its fair price, an offer for land a rival developer holds (answered by its rules the next working day), and an
    /// offer from a rival for land of the player's, accepted or refused in the Land tab. Never in a Going Public session: no land is sold
    /// there. Everything is on a press or the day's pass; nothing runs per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<LotView> _marketStrip = new List<LotView>(8);

        // ------------------------------------------------------------------ selling back to the city

        /// <summary>
        /// The market value of a site of the player's: each of its spaces at its own traffic and the rows of it the player holds, as the Land
        /// tab prices free spaces; 0 when one of them is not known.
        /// </summary>
        public long SiteValue(string firstId)
        {
            LotFile own = _state.Lot(firstId);
            return own == null ? 0L : ValueOf(firstId, own.Spaces, own.Rows);
        }

        // The market value of so many spaces from a first, with the rows of each held (empty for every row); 0 when one is not known.
        private long ValueOf(string firstId, int spaces, string rows)
        {
            if (!SiteSpaces(firstId, spaces, _marketStrip))
            {
                return 0L;
            }
            int stripRows = CellRules.RowsFor(_marketStrip[0].Depth);
            long value = 0L;
            for (int i = 0; i < _marketStrip.Count; i++)
            {
                long each = LotPrice(_marketStrip[i]);
                if (each <= 0L)
                {
                    return 0L;
                }
                value += CellRules.ColumnPrice(each, CellRules.RowsAt(rows, i, stripRows), stripRows);
            }
            return value;
        }

        /// <summary>What the city pays for a site of the player's now.</summary>
        public long SellPrice(string firstId)
        {
            LotFile own = _state.Lot(firstId);
            return own == null ? 0L : LandMarketRules.CityPrice(own.Paid, SiteValue(firstId));
        }

        // True when something is planned, permitted or under way on the land: a project, a car park, a grown building.
        private bool SitePlanned(string firstId)
        {
            return _state.OnLot(firstId) != null || ParkOf(firstId) != null || GrowOf(firstId) != null || ParkUnderWayOn(firstId);
        }

        // True when any of a site's spaces is ground the district's plan stood a building on: the city does not buy that back, for it would not be for sale again.
        private bool SiteBuiltOn(string firstId, int spaces)
        {
            if (!SiteSpaces(firstId, spaces, _marketStrip))
            {
                return true;
            }
            for (int i = 0; i < _marketStrip.Count; i++)
            {
                if (OnSpaces(_marketStrip[i]) && _marketStrip[i].Size != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Why the city will not buy a site of the player's now (a text key), or null.</summary>
        public string SellBlock(string firstId)
        {
            LotFile own = _state.Lot(firstId);
            bool found = own != null;
            return LandMarketRules.SellWhy(GoingPublic.InSession, found, found && SitePlanned(firstId), found && SiteBuiltOn(firstId, own.Spaces), found ? SellPrice(firstId) : 0L);
        }

        /// <summary>A site of the player's sold back to the city at its fair price, as the spaces for sale again. Returns a text key.</summary>
        public string Sell(string firstId)
        {
            string block = SellBlock(firstId);
            if (block != null)
            {
                return block;
            }
            LotFile own = _state.Lot(firstId);
            long price = SellPrice(firstId);
            string where = LotWords(firstId);
            int spaces = own.Spaces;
            _state.Lots.Remove(own);
            ConstructionGame.Give(price, "reallife_transaction_bld_land_sold", where);   // its own line in the game's books
            _state.CityLandBack += price;
            GrowsChanged();
            Logger?.Info("Construction: the site at " + firstId + " (" + spaces + " space(s), paid " + own.Paid + ") sold back to the city for " + price + "; the spaces are for sale again.");
            Changed();
            return "reallife_land_answer_sold";
        }

        // ------------------------------------------------------------------ a rival developer's land

        /// <summary>The grown sites a rival developer holds whose works have not begun (its plans may be at either step), into <paramref name="into"/> (cleared).</summary>
        public void RivalLand(List<GrowFile> into)
        {
            into.Clear();
            for (int i = 0; i < _state.Grows.Count; i++)
            {
                GrowFile g = _state.Grows[i];
                if (g.Owner != GrowOwner.Player && (g.Stage == GrowStage.Plans || g.Stage == GrowStage.Planning))
                {
                    into.Add(g);
                }
            }
        }

        /// <summary>The grown site with a number, or null.</summary>
        public GrowFile RivalSite(int id) => _state.GrowById(id);

        /// <summary>The rival developer's grown site along a player's site's block side, before or after it, or null for none.</summary>
        public GrowFile RivalBeside(string firstId)
        {
            LotFile own = _state?.Lot(firstId);
            if (own == null || !SiteSpaces(firstId, own.Spaces, _marketStrip))
            {
                return null;
            }
            GrowFile found = null;
            if (Before(_marketStrip[0], out LotView before))
            {
                found = BesideOf(before.Id);
            }
            if (found == null && After(_marketStrip[_marketStrip.Count - 1], out LotView after))
            {
                found = BesideOf(after.Id);
            }
            return found;
        }

        private GrowFile BesideOf(string lot)
        {
            GrowFile g = GrowOf(lot);
            return g != null && g.Owner != GrowOwner.Player ? g : null;
        }

        // The land a developer holds at what it paid: every site of its own under way.
        private long LandHeldBy(int rival, string holding, out int sites)
        {
            long held = 0L;
            sites = 0;
            for (int i = 0; i < _state.Grows.Count; i++)
            {
                GrowFile g = _state.Grows[i];
                if (g.UnderWay && g.Owner != GrowOwner.Player && g.Rival == rival && string.Equals(g.Holding, holding ?? string.Empty, StringComparison.Ordinal))
                {
                    held += g.Price;
                    sites++;
                }
            }
            return held;
        }

        // True when a developer's land, at what it paid, is more than its means: the builder's record is its purse.
        private bool ShortOfCash(int rival, string holding)
        {
            RivalFile r = _state.Rivals[BuildCatalogue.Clamp(rival, 0, BuildCatalogue.RivalCount - 1)];
            return LandMarketRules.Short(LandHeldBy(rival, holding, out _), LandMarketRules.Means(r.Reputation, r.Built, r.Won, ConstructionGame.Prices));
        }

        /// <summary>True when the developer who holds a site is short of cash.</summary>
        public bool ShortOfCash(GrowFile g) => ShortOfCash(g.Rival, g.Holding);

        /// <summary>A developer's ask for a site it holds today, and the range the player is shown.</summary>
        public LandAsk AskOf(GrowFile g, int day)
        {
            LandHeldBy(g.Rival, g.Holding, out int sites);
            bool shortCash = ShortOfCash(g);
            var a = new LandAsk { OnlySite = sites <= 1, Short = shortCash };
            a.Premium = LandMarketRules.Premium(LandMarketRules.Progress(g.Stage == GrowStage.Planning, day, g.Until), a.OnlySite, shortCash);
            a.Ask = LandMarketRules.Ask(g.Price, a.Premium);
            LandMarketRules.Range(a.Ask, _state.Seed, g.Id, out a.Low, out a.High);
            return a;
        }

        /// <summary>The newest offer of the player's for a rival's site, answered or waiting, or null.</summary>
        public LandOffer LastOffer(string lot) => _state.LastOn(LandOfferSide.Mine, lot);

        /// <summary>Why the player cannot offer a price for a rival's site now (a text key), or null.</summary>
        public string OfferBlock(GrowFile g, long price)
        {
            if (g == null || g.Owner == GrowOwner.Player)
            {
                return "reallife_land_answer_gone";
            }
            LandOffer last = _state.LastOn(LandOfferSide.Mine, g.Lot);
            bool started = g.Stage == GrowStage.Building || g.Stage == GrowStage.Letting || g.Stage == GrowStage.Let;
            return LandMarketRules.OfferWhy(GoingPublic.InSession, started, g.Stage == GrowStage.Dropped, _state.WaitingOn(g.Lot) != null, ConstructionGame.Day, last != null ? last.Made : 0,
                last != null && last.State == LandOfferState.Refused, last != null ? last.Price : 0L, last != null ? last.Raised : 0, price, (long)ConstructionGame.Cash, _state.Lots.Count);
        }

        /// <summary>An offer for a rival's site: it is answered by the developer's rules the next working day. Returns a text key.</summary>
        public string Offer(int growId, long price)
        {
            GrowFile g = _state.GrowById(growId);
            string block = OfferBlock(g, price);
            if (block != null)
            {
                return block;
            }
            LandOffer last = _state.LastOn(LandOfferSide.Mine, g.Lot);
            int day = ConstructionGame.Day;
            bool raise = last != null && last.State == LandOfferState.Refused && price > last.Price && last.Raised < LandMarketRules.RaisesMost;
            var o = new LandOffer
            {
                Id = _state.NewId(), Side = LandOfferSide.Mine, State = LandOfferState.Waiting, Lot = g.Lot, Spaces = g.Spaces, Grow = g.Id, Rival = g.Rival, Holding = g.Holding, Use = g.Use,
                Price = price, Made = day, Due = LandMarketRules.AnswerDay(day), Raised = raise ? last.Raised + 1 : 0
            };
            _state.Offers.Add(o);
            _state.TidyOffers();
            Logger?.Info("Construction: the player offered " + price + " for the land at " + g.Lot + " held by " + GrowDeveloper(g) + (raise ? " (raised " + o.Raised + " time(s))" : string.Empty)
                         + "; answered on day " + o.Due + ".");
            Changed();
            return "reallife_land_answer_offered";
        }

        // ------------------------------------------------------------------ the day: offers answered

        // The day's answers: the player's offers whose day has come, and a rival's offers left unanswered past their last day.
        private void LandMarketDay(int day)
        {
            for (int i = 0; i < _state.Offers.Count; i++)
            {
                LandOffer o = _state.Offers[i];
                if (!o.Waiting)
                {
                    continue;
                }
                if (o.Side == LandOfferSide.Mine && day >= o.Due)
                {
                    AnswerOffer(o, day);
                }
                else if (o.Side == LandOfferSide.Theirs && day > o.Due)
                {
                    o.State = LandOfferState.Lapsed;
                    o.Answered = day;
                    Logger?.Info("Construction: the offer for the land at " + o.Lot + " from " + OfferDeveloper(o) + " lapsed unanswered.");
                    Changed();
                }
            }
            if (Settings.ConstructionDevelopers && DistrictLives())
            {
                LandMarketMoves(day);   // the rivals' own trading (ConstructionDirector.LandMarketNpc.cs)
            }
        }

        // A rival's answer to the player's offer, by its rules: accept at or over its ask, refuse under it, lapse if its works began or its plans were dropped.
        private void AnswerOffer(LandOffer o, int day)
        {
            GrowFile g = _state.GrowById(o.Grow);
            bool started = g != null && (g.Stage == GrowStage.Building || g.Stage == GrowStage.Letting || g.Stage == GrowStage.Let);
            bool gone = g == null || g.Stage == GrowStage.Dropped;
            long ask = g != null ? AskOf(g, day).Ask : 0L;
            LandAnswer answer = LandMarketRules.Decide(o.Price, ask, started, gone);
            o.Answered = day;
            o.Ask = ask;
            string where = LotWords(o.Lot), who = OfferDeveloper(o);
            if (answer == LandAnswer.Accept && (ConstructionGame.Cash < o.Price || _state.Lots.Count >= BuildCatalogue.LotsMost || GoingPublic.InSession))
            {
                o.State = LandOfferState.Lapsed;
                MarketTell(NotificationType.Warning, "reallife_land_notice_nomoney", where, who, o.Price, 0);
                Logger?.Info("Construction: " + who + " would take " + o.Price + " for the land at " + o.Lot + " but the player could not pay or hold it; the offer lapsed.");
                Changed();
                return;
            }
            switch (answer)
            {
                case LandAnswer.Lapse:
                    o.State = LandOfferState.Lapsed;
                    MarketTell(NotificationType.Info, "reallife_land_notice_lapsed", where, who, o.Price, 0);
                    Logger?.Info("Construction: the player's offer for the land at " + o.Lot + " lapsed (" + (started ? "the works began" : "the plans were dropped") + ").");
                    break;
                case LandAnswer.Refuse:
                    o.State = LandOfferState.Refused;
                    MarketTell(NotificationType.Warning, "reallife_land_notice_refused", where, who, o.Price, 0);
                    Logger?.Info("Construction: " + who + " refused " + o.Price + " for the land at " + o.Lot + " (it asked " + ask + ").");
                    break;
                default:
                    TakeRivalLand(o, g, day);
                    break;
            }
            Changed();
        }

        // The deal done: the money, the land the player's as a site bought outright, the developer's plans dropped on its side with whatever it had paid.
        private void TakeRivalLand(LandOffer o, GrowFile g, int day)
        {
            string where = LotWords(o.Lot);
            if (!ConstructionGame.Take(o.Price, "reallife_transaction_bld_land_rival", where, false))
            {
                o.State = LandOfferState.Lapsed;
                MarketTell(NotificationType.Warning, "reallife_land_notice_nomoney", where, OfferDeveloper(o), o.Price, 0);
                return;
            }
            LotView site = default(LotView);
            bool known = SiteSpaces(g.Lot, g.Spaces, _marketStrip);
            if (known)
            {
                site = Joined(_marketStrip);
            }
            _state.Lots.Add(new LotFile { Id = g.Lot, Bought = day, Paid = o.Price, Zones = LandRules.ZonesOf(site.Class), TaxedTo = day, EmptySince = day, Spaces = g.Spaces });
            g.Stage = GrowStage.Dropped;   // its plans and what it paid for them are dropped on its side; nothing is refunded to anyone
            g.Until = day;
            GrowsChanged();
            o.State = LandOfferState.Accepted;
            MarketTell(NotificationType.Success, "reallife_land_notice_accepted", where, OfferDeveloper(o), o.Price, 0);
            Logger?.Info("Construction: " + OfferDeveloper(o) + " sold the land at " + g.Lot + " (" + g.Spaces
                + " space(s)) to the player for " + o.Price + "; its plans for " + g.Use + " are dropped.");
        }

        // ------------------------------------------------------------------ a rival's offer for the player's land

        /// <summary>The rivals' offers for the player's land that wait for an answer, newest last, into <paramref name="into"/> (cleared).</summary>
        public void OffersIn(List<LandOffer> into)
        {
            into.Clear();
            for (int i = 0; i < _state.Offers.Count; i++)
            {
                if (_state.Offers[i].Waiting && _state.Offers[i].Side == LandOfferSide.Theirs)
                {
                    into.Add(_state.Offers[i]);
                }
            }
        }

        /// <summary>The offer with a number, or null.</summary>
        public LandOffer OfferNumber(int id) => _state.Offer(id);

        /// <summary>Why a rival's offer cannot be accepted now (a text key), or null: the land must still be the player's and have nothing on it.</summary>
        public string AcceptBlock(LandOffer o)
        {
            if (o == null || !o.Waiting || o.Side != LandOfferSide.Theirs)
            {
                return "reallife_land_answer_offer_gone";
            }
            return SellBlock(o.Lot);
        }

        /// <summary>A rival's offer accepted: the land is the developer's at the price, and its plans start on it. Returns a text key.</summary>
        public string Accept(int offerId)
        {
            LandOffer o = _state.Offer(offerId);
            string block = AcceptBlock(o);
            if (block != null)
            {
                if (o != null && o.Waiting && block != "reallife_bld_answer_session_land")
                {
                    o.State = LandOfferState.Lapsed;   // the land is used now: the offer is over
                    o.Answered = ConstructionGame.Day;
                    Changed();
                }
                return block;
            }
            LotFile own = _state.Lot(o.Lot);
            int day = ConstructionGame.Day, id = _state.NewId();
            string where = LotWords(o.Lot);
            _state.Lots.Remove(own);
            ConstructionGame.Give(o.Price, "reallife_transaction_bld_land_rival_sold", where);
            _state.Grows.Add(new GrowFile
            {
                Id = id, Stage = GrowStage.Plans, Owner = GrowOwner.Builder, Lot = o.Lot, Spaces = o.Spaces, Use = o.Use, Rival = o.Rival, Tier = GrowTier.Standard, Day = day, Design = o.Design,
                Price = o.Price, Until = day + GrowRules.PlansDays(_state.Seed, id)
            });
            TidyGrows();
            GrowsChanged();
            o.State = LandOfferState.Accepted;
            o.Answered = day;
            Paper("reallife_land_paper_bought", OfferDeveloper(o) + " (" + where + ")", o.Spaces);
            Logger?.Info("Construction: the player sold the land at " + o.Lot + " (" + o.Spaces + " space(s), paid " + own.Paid
                + ") to " + OfferDeveloper(o) + " for " + o.Price + "; it plans " + o.Use + " there.");
            Changed();
            return "reallife_land_answer_accepted_in";
        }

        /// <summary>A rival's offer refused: the developer leaves that land alone for a while. Returns a text key.</summary>
        public string Refuse(int offerId)
        {
            LandOffer o = _state.Offer(offerId);
            if (o == null || !o.Waiting || o.Side != LandOfferSide.Theirs)
            {
                return "reallife_land_answer_offer_gone";
            }
            o.State = LandOfferState.Refused;
            o.Answered = ConstructionGame.Day;
            Logger?.Info("Construction: the player refused " + o.Price + " from " + OfferDeveloper(o) + " for the land at " + o.Lot + ".");
            Changed();
            return "reallife_land_answer_refused_in";
        }

        // ------------------------------------------------------------------ in words

        /// <summary>Who an offer is with: a rival tycoon's holding, or the builder whose arm it is.</summary>
        public static string OfferDeveloper(LandOffer o)
        {
            return !string.IsNullOrEmpty(o.Holding) ? o.Holding : ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(o.Rival, 0, BuildCatalogue.RivalCount - 1)]);
        }

        /// <summary>Why a rival wants the player's land, in words.</summary>
        public static string ReasonWords(LandOffer o) => ConstructionText.T(o.Reason == LandReason.Joins ? "reallife_land_reason_joins" : "reallife_land_reason_blocks");

        // A notice of the market's: where the land is, who with, the money, opening the Land tab on the offer.
        private void MarketTell(NotificationType type, string key, string where, string who, long amount, int offer)
        {
            var data = new Dictionary<string, string>(4)
            {
                { "name", where ?? string.Empty }, { "developer", who ?? string.Empty }, { "amount", ConstructionText.Money(amount) }
            };
            ConstructionGame.Tell(type, key, data, () => Open(TabLand, offer));
        }
    }
}
