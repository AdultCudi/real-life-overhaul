using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Land auctions and offers on city lots (<see cref="AuctionRules"/>): the free spaces of the grid are put under the hammer on a calendar, the city's eight builders bid on them
    /// by their licence level, standing and need from the cash their record and their work on land give them, and the player bids from the Land tab or makes an offer on a space that is
    /// not up. A space the player wins is theirs at the price; one a builder wins becomes that builder's site (its plans and the land market's asks follow, as for any site of
    /// theirs), and what a builder holds counts against its cash. The auctioneer is a person of the shared people pool. Notices open the Land tab on the auction. The day's pass runs the
    /// calendar, the rooms and the answers; nothing per frame. Never in a Going Public session: no land is sold there.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>A pick number at or over this on the Land tab is an auction (its number after the base); over <see cref="OfferPickBase"/> the player's offer for a space.</summary>
        public const int AuctionPickBase = 1000000, OfferPickBase = 2000000;

        private readonly List<AuctionLot> _auctionLots = new List<AuctionLot>(32);
        private readonly List<AuctionEvent> _auctionEvents = new List<AuctionEvent>(16);
        private readonly Bidder[] _bidders = new Bidder[BuildCatalogue.RivalCount];

        private bool AuctionsOn() => Settings.ConstructionDevelopers && DistrictLives() && !GoingPublic.InSession;

        /// <summary>True while a space is up for auction: nobody buys it outright, and the developers leave it alone.</summary>
        public bool AuctionOn(string lot) => _state != null && _state.AuctionOn(lot) != null;

        // A free space the city may put up: free land nobody holds, builds on or develops, which the player does not own and which has a price.
        private bool Auctionable(LotView lot, out long list)
        {
            list = 0L;
            if (!FreeSpace(lot) || _state.Lot(lot.Id) != null)
            {
                return false;
            }
            list = LotPrice(lot);
            return list > 0L;
        }

        // The city's builders as bidders: their level, standing, the cash they have and how much they need land.
        private void FillBidders()
        {
            for (int r = 0; r < _bidders.Length; r++)
            {
                RivalFile f = _state.Rivals[r];
                long held = LandHeldBy(r, string.Empty, out _);
                long worth = AuctionRules.Worth(f.Reputation, f.Built, f.Won, ConstructionGame.Prices, f.Gained);
                _bidders[r] = new Bidder { Level = LevelRules.Rival(f.Built, f.Reputation), Standing = f.Reputation, Cash = AuctionRules.Cash(worth, held), Need = AuctionRules.Need(held, worth) };
            }
        }

        /// <summary>A rival developer's worth now: its record's means and what it has made on land and works.</summary>
        public long RivalWorth(int rival)
        {
            if (_state == null || rival < 0 || rival >= _state.Rivals.Length)
            {
                return 0L;
            }
            RivalFile f = _state.Rivals[rival];
            return Math.Max(0L, AuctionRules.Worth(f.Reputation, f.Built, f.Won, ConstructionGame.Prices, f.Gained));
        }

        /// <summary>The cash a rival developer can put into land now.</summary>
        public long RivalCash(int rival)
        {
            return _state == null ? 0L : AuctionRules.Cash(RivalWorth(rival), LandHeldBy(BuildBounds(rival), string.Empty, out _));
        }

        private static int BuildBounds(int rival) => BuildCatalogue.Clamp(rival, 0, BuildCatalogue.RivalCount - 1);

        // The day: the calendar opens lots when its day has come, the builders bid, the hammer falls, and the city answers the player's offers.
        private void AuctionDay(int day)
        {
            if (!AuctionsOn())
            {
                return;
            }
            bool opening = _state.NextAuctionDay <= 0 || day >= _state.NextAuctionDay;
            _auctionLots.Clear();
            if (opening)
            {
                IReadOnlyList<LotView> lots = Lots;
                for (int i = 0; i < lots.Count; i++)
                {
                    if (Auctionable(lots[i], out long list))
                    {
                        _auctionLots.Add(new AuctionLot { Lot = lots[i].Id, List = list, Area = lots[i].Width * lots[i].Depth });
                    }
                }
            }
            bool live = false;
            for (int i = 0; i < _state.Auctions.Count && !live; i++)
            {
                live = _state.Auctions[i].Live;
            }
            if (!opening && !live && _state.CityOffersWaiting() == 0)
            {
                return;
            }
            FillBidders();
            _auctionEvents.Clear();
            AuctionRules.Day(_state, day, _auctionLots, _bidders, opening, _auctionEvents);
            int opened = 0;
            AuctionFile first = null;
            for (int i = 0; i < _auctionEvents.Count; i++)
            {
                AuctionEvent e = _auctionEvents[i];
                switch (e.Kind)
                {
                    case AuctionEventKind.Opened:
                        opened++;
                        first = first ?? e.Auction;
                        Logger?.Info("Construction: " + LotWords(e.Auction.Lot) + " is up for auction (list " + e.Auction.List + ", opening " +
                            e.Auction.Opening + ", reserve " + e.Auction.Reserve + ") until day "
                                     + e.Auction.Ends + ", the auctioneer " + e.Auction.Hall + ".");
                        break;
                    case AuctionEventKind.PlayerOutbid:
                        AuctionTell(NotificationType.Warning, "reallife_auc_notice_outbid", e.Auction, e.Rival);
                        Logger?.Info("Construction: " + AuctionWho(e.Rival) + " outbid the player on " + e.Auction.Lot + " at " + e.Auction.Bid + ".");
                        break;
                    case AuctionEventKind.Sold:
                        SettleAuction(e.Auction, day);
                        break;
                    case AuctionEventKind.Passed:
                        Logger?.Info("Construction: the auction of " + e.Auction.Lot + " passed in: the best bid " + e.Auction.Bid + " did not reach the reserve " + e.Auction.Reserve + ".");
                        if (e.Auction.Mine > 0L)
                        {
                            AuctionTell(NotificationType.Info, "reallife_auc_notice_passed", e.Auction, -2);
                        }
                        break;
                }
            }
            if (opened > 0)
            {
                Tell(NotificationType.Info, "reallife_auc_notice_opened", LotWords(first.Lot), null, TabLand, AuctionPickBase + first.Id, opened);
            }
            CityOffersDay(day);
            if (_auctionEvents.Count > 0)
            {
                Changed();
            }
        }

        // The hammer: the player's lot bought at the price (or lapsed if the money is not there), a builder's lot made its site.
        private void SettleAuction(AuctionFile a, int day)
        {
            if (a.Leader == -1)
            {
                if (!Lot(a.Lot, out LotView lot) || _state.Lots.Count >= BuildCatalogue.LotsMost || !ConstructionGame.Take(a.Bid, "reallife_transaction_bld_land_auction", LotWords(a.Lot), false))
                {
                    a.State = AuctionState.Lapsed;
                    AuctionTell(NotificationType.Warning, "reallife_auc_notice_lapsed", a, -1);
                    Logger?.Info("Construction: the player led the auction of " + a.Lot + " at " + a.Bid + " but could not pay or hold it; the lot is for sale again.");
                    return;
                }
                _state.Lots.Add(new LotFile { Id = a.Lot, Bought = day, Paid = a.Bid, Zones = LandRules.ZonesOf(lot.Class), TaxedTo = day, EmptySince = day });
                AuctionTell(NotificationType.Success, "reallife_auc_notice_won", a, -1);
                Logger?.Info("Construction: the player won the auction of " + a.Lot + " for " + a.Bid + " (list " + a.List + ").");
                return;
            }
            RivalTakesLot(a, day);
            if (a.Mine > 0L)
            {
                AuctionTell(NotificationType.Warning, "reallife_auc_notice_lost", a, a.Leader);
            }
        }

        // A builder's win: the space its site, its plans for the use its district wants drawn at once, as for every site the city's growth gives a builder.
        private void RivalTakesLot(AuctionFile a, int day)
        {
            if (!Lot(a.Lot, out LotView lot))
            {
                a.State = AuctionState.Passed;   // the lot is not in the district at this load: the sale falls through
                return;
            }
            BuildUse use = DevelopmentRules.Use(lot.Id, lot.Class);
            int best = -1;
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                int demand = DemandFor((BuildUse)u, lot.District);
                if (LandRules.Zoned(LandRules.ZonesOf(lot.Class), (BuildUse)u) && demand > best)
                {
                    best = demand;
                    use = (BuildUse)u;
                }
            }
            if (!GrowDesign(lot, use, GrowTier.Standard, false, out string design))
            {
                design = NoDesign;   // the designs are being looked through: the plans find none, and the land goes back to the city
            }
            int id = _state.NewId();
            var g = new GrowFile
            {
                Id = id, Stage = GrowStage.Plans, Owner = GrowOwner.Builder, Lot = a.Lot, Spaces = 1, Use = use, Rival = a.Leader, Day = day, Design = design, Price = a.Bid,
                Until = day + GrowRules.PlansDays(_state.Seed, id)
            };
            g.Tier = BuilderFinish(g.Rival, g.Price, DemandFor(use, lot.District));
            _state.Grows.Add(g);
            _state.CityLand += g.Price;
            TidyGrows();
            GrowsChanged();
            Paper("reallife_auc_paper_won", GrowName(g), 1);
            Logger?.Info("Construction: " + AuctionWho(a.Leader) + " won the auction of " + a.Lot + " for " + a.Bid + " (list " + a.List + "), to build " + use + " in " + design + " (" + g.Tier
                         + " finish); its plans run until day " + g.Until + ".");
        }

        /// <summary>Who a bid is by: the player, or the builder's name.</summary>
        public static string AuctionWho(int who)
        {
            return who == -1 ? ConstructionText.T("reallife_auc_you") : who >= 0 ?
                ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(who, 0, BuildCatalogue.RivalCount - 1)]) : string.Empty;
        }

        // A notice of the auction's: where the lot is, who with, the money, opening the Land tab on the auction.
        private void AuctionTell(NotificationType type, string key, AuctionFile a, int who)
        {
            var data = new Dictionary<string, string>(4)
            {
                { "name", LotWords(a.Lot) }, { "developer", AuctionWho(who) }, { "amount", ConstructionText.Money(a.Bid) }
            };
            int pick = AuctionPickBase + a.Id;
            ConstructionGame.Tell(type, key, data, () => Open(TabLand, pick));
        }

        // ------------------------------------------------------------------ the player's bid

        /// <summary>The auctions to show on the Land tab, newest first: those running, then those lately over.</summary>
        public void AuctionsIn(List<AuctionFile> into)
        {
            into.Clear();
            for (int i = _state.Auctions.Count - 1; i >= 0; i--)
            {
                into.Add(_state.Auctions[i]);
            }
        }

        /// <summary>Why the player cannot bid this much now (a text key), or null.</summary>
        public string BidBlock(AuctionFile a, long amount)
        {
            if (GoingPublic.InSession)
            {
                return "reallife_bld_answer_session_land";
            }
            return AuctionRules.BidWhy(a, amount, (long)ConstructionGame.Cash, ConstructionGame.Day, _state.Lots.Count);
        }

        /// <summary>The player bids an amount: they lead at it, and are outbid by a builder with the means and the licence. Returns a text key.</summary>
        public string Bid(int auctionId, long amount)
        {
            AuctionFile a = _state.Auction(auctionId);
            string block = BidBlock(a, amount);
            if (block != null)
            {
                return block;
            }
            AuctionRules.Bid(a, amount, ConstructionGame.Day);
            Logger?.Info("Construction: the player bid " + amount + " on the auction of " + a.Lot + " (list " + a.List + ").");
            Changed();
            return "reallife_auc_answer_bid";
        }

        // ------------------------------------------------------------------ offers on city lots

        /// <summary>The list price of a free space, or 0 when it is not one the city would sell.</summary>
        public long ListPrice(string lot)
        {
            return Lot(lot, out LotView l) && Auctionable(l, out long list) ? list : 0L;
        }

        /// <summary>Why the player cannot offer a price for a space now (a text key), or null.</summary>
        public string CityOfferBlock(string lot, long price)
        {
            LotView l = default(LotView);
            bool known = !string.IsNullOrEmpty(lot) && Lot(lot, out l);
            bool atAuction = known && _state.AuctionOn(lot) != null;
            bool found = known && (atAuction || FreeSpace(l));
            long list = found ? LotPrice(l) : 0L;
            CityOfferFile last = _state.LastCityOffer(lot);
            bool waiting = last != null && last.Waiting;
            return AuctionRules.OfferWhy(GoingPublic.InSession, found, atAuction, waiting, _state.CityOffersWaiting(), ConstructionGame.Day, last != null ? last.Made : 0, price, list,
                (long)ConstructionGame.Cash, _state.Lots.Count);
        }

        /// <summary>The player's offer for a space under the list price: the city answers the next working day by a seeded chance that rises with the price. Returns a text key.</summary>
        public string OfferCity(string lot, long price)
        {
            string block = CityOfferBlock(lot, price);
            if (block != null)
            {
                return block;
            }
            Lot(lot, out LotView l);
            int day = ConstructionGame.Day;
            var o = new CityOfferFile { Id = _state.NewId(), Lot = lot, Price = price, List = LotPrice(l), Made = day, Due = AuctionRules.AnswerDay(day), State = CityOfferState.Waiting };
            _state.CityOffers.Add(o);
            _state.TidyAuctions();
            Logger?.Info("Construction: the player offered " + price + " for " + lot + " (list " + o.List + "); the city answers on day " + o.Due + ".");
            Changed();
            return "reallife_auc_answer_offered";
        }

        /// <summary>The player's newest offer for a space, answered or waiting, or null.</summary>
        public CityOfferFile LastCityOffer(string lot) => _state.LastCityOffer(lot);

        // The city's answers to the offers whose day has come: taken at the price by the chance the price earned, or refused; the offer lapses if the space is no longer for sale.
        private void CityOffersDay(int day)
        {
            for (int i = 0; i < _state.CityOffers.Count; i++)
            {
                CityOfferFile o = _state.CityOffers[i];
                if (!o.Waiting || day < o.Due)
                {
                    continue;
                }
                o.Answered = day;
                string where = LotWords(o.Lot);
                bool found = Lot(o.Lot, out LotView lot);
                bool free = found && FreeSpace(lot) && _state.Lot(o.Lot) == null && _state.AuctionOn(o.Lot) == null;
                if (!free || _state.Lots.Count >= BuildCatalogue.LotsMost)
                {
                    o.State = CityOfferState.Lapsed;
                    CityOfferTell(NotificationType.Info, "reallife_auc_notice_offer_lapsed", o, where);
                }
                else if (!AuctionRules.Takes(_state.Seed, o.Id, o.Price, o.List))
                {
                    o.State = CityOfferState.Refused;
                    CityOfferTell(NotificationType.Warning, "reallife_auc_notice_offer_no", o, where);
                    Logger?.Info("Construction: the city refused " + o.Price + " for " + o.Lot + " (list " + o.List + ").");
                }
                else if (!ConstructionGame.Take(o.Price, "reallife_transaction_bld_land", where, false))
                {
                    o.State = CityOfferState.Lapsed;
                    CityOfferTell(NotificationType.Warning, "reallife_auc_notice_offer_money", o, where);
                }
                else
                {
                    o.State = CityOfferState.Accepted;
                    _state.Lots.Add(new LotFile { Id = o.Lot, Bought = day, Paid = o.Price, Zones = LandRules.ZonesOf(lot.Class), TaxedTo = day, EmptySince = day });
                    CityOfferTell(NotificationType.Success, "reallife_auc_notice_offer_yes", o, where);
                    Logger?.Info("Construction: the city took " + o.Price + " for " + o.Lot + " (list " + o.List + ").");
                }
                Changed();
            }
        }

        private void CityOfferTell(NotificationType type, string key, CityOfferFile o, string where)
        {
            var data = new Dictionary<string, string>(3) { { "name", where }, { "amount", ConstructionText.Money(o.Price) }, { "list", ConstructionText.Money(o.List) } };
            int pick = OfferPickBase + o.Id;
            ConstructionGame.Tell(type, key, data, () => Open(TabLand, pick));
        }
    }
}
