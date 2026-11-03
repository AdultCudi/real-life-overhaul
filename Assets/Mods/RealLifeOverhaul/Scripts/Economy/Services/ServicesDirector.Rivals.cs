using System;
using System.Collections.Generic;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Rivals of the game's selling their own businesses, what the player can do turned round: now and then, on the day
    /// event, one of them puts up one of its weakest businesses at a little under the game's own takeover price. The player
    /// may buy it first, on the game's own question and BizMan's own takeover; after that another rival who can run it and
    /// put the price up takes it over, or the seller keeps it. Cost: one pass over the rivals' businesses on a day a sale
    /// may come (one in ten days at most); nothing per frame.
    /// </summary>
    public sealed partial class ServicesDirector
    {
        private readonly List<RivalHolding> _holdings = new List<RivalHolding>(64);

        // Sales past the player's days go to their buyer or lapse, those no longer the seller's go, and now and then a new one comes.
        private void RivalSalesDay(int day)
        {
            for (int i = _state.RivalSales.Count - 1; i >= 0; i--)
            {
                RivalSale v = _state.RivalSales[i];
                BuildingRegistration reg = ServicesGame.Find(v.Place);
                if (!ServicesGame.StillTheirs(reg, v))
                {
                    Logger?.Info("Rivals selling: '" + v.Business + "' is no longer " + v.SellerName + "'s as it was put up (closed, bought or changed); it comes off sale.");
                    _state.RivalSales.RemoveAt(i);
                    continue;
                }
                if (!SaleRules.RivalSaleOver(v, day))
                {
                    continue;
                }
                bool passed = v.Buyer.Length > 0 && ServicesGame.PassTo(reg, v);
                Logger?.Info("Rivals selling: '" + v.Business + "' of " + v.SellerName + ", put up on day " + v.Day + " for " + v.Price + ": "
                             + (passed ? "taken over by " + v.BuyerName + "; the business trades on under its new owner"
                                 : v.Buyer.Length > 0 ? v.BuyerName + " could no longer take it, so " + v.SellerName + " keeps it" : "nobody bought it, so " + v.SellerName + " keeps it") + ".");
                if (passed)
                {
                    Tell(NotificationType.Info, "reallife_svc_note_rival_sold", TabSale, null, "seller", v.SellerName, "business", v.Business, "buyer", v.BuyerName, "amount",
                        ServicesText.Money(v.Price));
                }
                _state.RivalSales.RemoveAt(i);
            }
            if (ServicesGame.RivalsOn && SaleRules.RivalSaleDue(_state, day))
            {
                PutUp(day);
            }
        }

        // One rival's business goes up for sale today; null if none could.
        private RivalSale PutUp(int day)
        {
            ServicesGame.RivalHoldings(_holdings);
            int at = SaleRules.PickHolding(_state, _holdings, day), held = _holdings.Count;
            RivalHolding h = at >= 0 ? _holdings[at] : default(RivalHolding);
            _holdings.Clear();
            if (at < 0)
            {
                if (Verbose)
                {
                    Logger?.Info("Services check (rivals selling, day " + day + "): none of " + held + " rivals' business(es) could be put up (a rival never sells its last): PASS");
                }
                return null;
            }
            BuildingRegistration reg = ServicesGame.Find(h.Place);
            float takeover = reg != null ? ServicesGame.Takeover(reg, h.Rival) : 0f;
            int price = SaleRules.RivalAsking(takeover);
            ServicesGame.RivalBuyers(reg, h.Rival, _bidders);
            int b = SaleRules.PickRivalBuyer(_state, _bidders, h.Rival, price, day), could = _bidders.Count;
            string buyer = b >= 0 ? _bidders[b].Id : null, buyerName = b >= 0 ? _bidders[b].Name : null;
            int purse = b >= 0 ? _bidders[b].Purse : 0;
            _bidders.Clear();
            RivalSale sale = SaleRules.OpenRivalSale(_state, h, price, buyer, buyerName, day);
            Logger?.Info("Rivals selling: " + (sale != null
                ? h.RivalName + " puts up '" + h.Business + "' (" + h.Place + ", about " + h.Daily + " a day, of " + h.Count + " businesses) for " + price + " (" + ServicesCatalogue.RivalSaleAskPct
                  + "% of the game's takeover price of " + Math.Round(takeover) + "). The player may buy it until day " + sale.Deadline + "; then "
                  + (buyer != null
                      ? buyerName + " takes it over (can put up " + purse + ", of " + could + " rival(s) who could run it)"
                      : "nobody of " + could + " rival(s) who could run it can pay, and the seller keeps it")
                : "'" + h.Business + "' of " + h.RivalName + " has no price the game can name yet; nothing is put up") + ".");
            if (sale != null)
            {
                Tell(NotificationType.Info, "reallife_svc_note_rival_selling", TabSale, "rsale:" + sale.Id, "seller", sale.SellerName, "business", sale.Business, "amount",
                    ServicesText.Money(sale.Price), "day", sale.Deadline.ToString());
            }
            return sale;
        }

        /// <summary>The player buys a rival's business put up for sale: the game's own checks, then its own question, then BizMan's own takeover.</summary>
        public void BuyRival(int id)
        {
            if (!Ready(false))
            {
                return;
            }
            SaleGuard("buying a rival's business", () =>
            {
                RivalSale v = _state.RivalSale(id);
                BuildingRegistration reg = v != null ? ServicesGame.Find(v.Place) : null;
                if (!Buyable(v, reg))
                {
                    return;
                }
                string body = ServicesText.Fill("reallife_svc_confirm_buy_body", "seller", v.SellerName, "business", v.Business, "amount", ServicesText.Money(v.Price));
                GameConfirm.Show("reallife_svc_confirm_buy_title".Localize(new { business = v.Business }), "reallife_svc_confirm_sell_text".Localize(new { text = body }),
                    () => SaleGuard("completing a purchase", () => Bought(id)), null, null, null, false, true);
            });
        }

        // Whether a rival's sale can still be bought now; says why not with a notice (the game's own where it has one).
        private bool Buyable(RivalSale v, BuildingRegistration reg)
        {
            if (v == null || !ServicesGame.StillTheirs(reg, v))
            {
                if (v != null)
                {
                    _state.RivalSales.Remove(v);
                    Changed();
                }
                Notifications.ShowError("reallife_svc_refuse_rival_sale_gone");
                return false;
            }
            return ServicesGame.MayBuy(reg, v.Price);
        }

        private void Bought(int id)
        {
            if (!Ready(false))
            {
                return;
            }
            RivalSale v = _state.RivalSale(id);
            BuildingRegistration reg = v != null ? ServicesGame.Find(v.Place) : null;
            if (!Buyable(v, reg))
            {
                return;
            }
            if (!ServicesGame.Buy(reg, v.Price))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            _state.RivalSales.Remove(v);
            bool mine = reg.RentedByPlayer && string.IsNullOrEmpty(reg.businessOwnerRivalId);
            Logger?.Info("Rivals selling: the player bought '" + v.Business + "' (" + v.Place + ") from " + v.SellerName + " for " + v.Price + " through the game's own takeover.");
            Logger?.Info("Services check (bought from a rival, " + v.Business + "): the building is " + (reg.RentedByPlayer ? "rented by the player" : "NOT rented by the player")
                         + ", its rival owner " + (string.IsNullOrEmpty(reg.businessOwnerRivalId) ? "cleared" : "STILL SET") + ": " + (mine ? "PASS" : "FAIL: the takeover did not complete"));
            Notifications.Show(NotificationType.Success, "bizman_presentation_notification_offer_accepted", new Dictionary<string, string>(1) { { "name", v.Business } });
            Changed();
        }

        /// <summary>"Test: an offer from a rival": the player's business worth most goes on the market if it is not, and a rival of the game's who could buy it offers now.</summary>
        public void TestRivalOffer()
        {
            if (!TestReady("Rival offer", false))
            {
                return;
            }
            TestGuard("rival offer", false, () =>
            {
                BuildingRegistration reg = BestBusiness(out SaleBreakdown b);
                if (reg == null)
                {
                    TestRefusals.TellKey(Logger, "Rival offer", "reallife_svc_test_no_business");
                    return;
                }
                if (b.Value <= 0)
                {
                    TestRefusals.TellKey(Logger, "Rival offer", "reallife_svc_refuse_worthless");
                    return;
                }
                _investors.Clear();
                ServicesGame.Bidders(reg, _investors, _bidders);
                int could = 0, afford = 0;
                for (int i = _bidders.Count - 1; i >= 0; i--)
                {
                    if (_bidders[i].Kind != (int)BuyerKind.Rival)
                    {
                        _bidders.RemoveAt(i);   // a rival only, for this test
                        continue;
                    }
                    could++;
                    afford += SaleRules.RivalCanBid(_bidders[i].Purse, b.Value) ? 1 : 0;
                }
                string place = ServicesGame.Place(reg);
                SaleListing l = _state.Listing(place) ?? SaleRules.List(_state, place, ServicesStore.Clean(ServicesGame.Name(reg)), b.Value, ServicesCatalogue.AskingDefault, Save.Day);
                if (l == null || _state.SaleOffers.Count >= ServicesCatalogue.OffersKept)
                {
                    _bidders.Clear();
                    TestRefusals.TellKey(Logger, "Rival offer", "reallife_svc_refuse_listings_full");
                    return;
                }
                int made = l.OffersMade, next = l.NextOfferDay;
                SaleOffer offer = afford > 0 ? SaleRules.Offer(_state, l, Save.Day, _bidders) : null;
                _bidders.Clear();
                l.OffersMade = made + (offer != null ? 1 : 0);   // the test uses one of the listing's turns, and does not move the day of the next
                l.NextOfferDay = next;
                if (offer == null)
                {
                    string why = could + " rival(s) of the game's could run it there, " + afford + " could put up " + ServicesCatalogue.RivalLeastPct + "% of its value of " + b.Value
                                 + (reg.BuildingOwnedByPlayer ? "; you own the building, which stays yours, so no rival can take the business over" : string.Empty);
                    TestRefusals.TellKey(Logger, "Rival offer", "reallife_svc_test_no_rival", why);
                    return;
                }
                Tell(NotificationType.Info, "reallife_svc_note_sale_offer", TabSale, "bid:" + offer.Id, "business", l.Name, "buyer", ServicesText.Buyer(offer), "amount",
                    ServicesText.Money(offer.Price), "day", offer.Deadline.ToString());
                TestDone("an offer for '" + l.Name + "' (worth " + b.Value + ") from " + offer.WhoName + ", a rival of the game's with " + offer.WhoCount + " business(es): " + offer.Price
                         + " (ceiling " + offer.Ceiling + "), until day " + offer.Deadline + "; " + afford + " of " + could
                         + " rival(s) who could run it could pay. Accepting it is a real sale: the rival opens its own business there", TabSale, "bid:" + offer.Id);
            });
        }

        /// <summary>"Test: a rival sells a business": one of the rivals' weakest businesses goes up for sale now, offered to the player first.</summary>
        public void TestRivalSale()
        {
            if (!TestReady("Rival sale", false))
            {
                return;
            }
            TestGuard("rival sale", false, () =>
            {
                if (!ServicesGame.RivalsOn || _state.RivalSales.Count >= ServicesCatalogue.RivalSalesMost)
                {
                    TestRefusals.TellKey(Logger, "Rival sale", "reallife_svc_test_no_rival_sale",
                        !ServicesGame.RivalsOn ? "the game's rivals are off in this save" : "two rivals' sales are open already");
                    return;
                }
                int was = _state.RivalSaleDay;
                RivalSale sale = PutUp(Save.Day);
                _state.RivalSaleDay = was;   // the test does not move the day the next real one may come
                if (sale == null)
                {
                    TestRefusals.TellKey(Logger, "Rival sale", "reallife_svc_test_no_rival_sale", "no rival with more than one business has one it could sell, or the game names no price for it");
                    return;
                }
                TestDone(sale.SellerName + " puts up '" + sale.Business + "' for " + sale.Price + " until day " + sale.Deadline + "; "
                         + (sale.Buyer.Length > 0 ? sale.BuyerName + " takes it after that" : "nobody else can pay, so the seller keeps it after that")
                         + ". Buying it is real, on the game's question", TabSale, "rsale:" + sale.Id);
            });
        }
    }
}
