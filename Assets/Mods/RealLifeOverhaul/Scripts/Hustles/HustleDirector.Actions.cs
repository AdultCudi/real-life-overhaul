using System.Globalization;
using RealLifeOverhaul.Economy.Dealership;
using UI.Notification;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>The director's part the app calls: buying and haggling, plans for a thing, selling and answering buyers, visits.</summary>
    public sealed partial class HustleDirector
    {
        /// <summary>The player's negotiation skill, which brings a seller's least down a little.</summary>
        internal static int Negotiation => HustleGame.Skill(HustleCatalogue.NegotiationSkill);

        /// <summary>An offer to a seller. The answer, and their counter when they make one.</summary>
        internal HaggleAnswer Haggle(int listing, int price, bool deliver, out int counter)
        {
            counter = 0;
            Listing l = _state != null ? _state.ListingOf(listing) : null;
            if (l == null || l.Gone)
            {
                return HaggleAnswer.WalkedAway;
            }
            HaggleAnswer a = HustleRules.Haggle(l, price, Negotiation, out counter);
            if (a == HaggleAnswer.Taken && !Buy(listing, price, deliver))
            {
                return HaggleAnswer.Countered;   // they would have, but the money was not there: the listing stands
            }
            if (a == HaggleAnswer.WalkedAway)
            {
                HustleText.Note(NotificationType.Info, "reallife_hustle_note_seller_gone", "who", l.Seller);
            }
            Changed();
            return a;
        }

        /// <summary>A listing bought at a price: paid now, then collected at the seller's door or brought home the next morning.</summary>
        internal bool Buy(int listing, int price, bool deliver)
        {
            Listing l = _state != null ? _state.ListingOf(listing) : null;
            if (l == null || l.Gone || _state.Items.Count >= HustleCatalogue.OwnedMost)
            {
                HustleText.Note(NotificationType.Warning, l == null || l.Gone ? "reallife_hustle_note_listing_gone" : "reallife_hustle_note_too_many");
                return false;
            }
            int fee = deliver && !l.Car ? HustleRules.DeliveryFee(price, HustleGame.Level) : 0;
            bool paid = l.Car ? HustleGame.PayForCar(price, l.Thing) : HustleGame.Pay(price + fee, "reallife_transaction_hustle_bought");
            if (!paid)
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_short", "amount", HustleText.Money(price + fee));
                return false;
            }
            Owned o = HustleRules.Buy(_state, l, price, deliver && !l.Car, fee, HustleGame.Day);
            string thing = HustleText.Thing(o.Thing, o.Car);
            if (o.State == OwnedState.Delivering)
            {
                HustleText.Note(NotificationType.Success, "reallife_hustle_note_bought_deliver", "thing", thing, "fee", HustleText.Money(fee));
            }
            else
            {
                HustleGame.Guide(o.Street, o.Number);
                HustleText.Note(NotificationType.Success, "reallife_hustle_note_bought_collect", "thing", thing, "place", HustleText.Place(o.Street, o.Number), 10f);
            }
            Logger?.Info("Hustles: bought a " + o.Thing + " from " + o.Seller + " for " + price + (fee > 0 ? " and " + fee + " to deliver" : string.Empty) + "; it is "
                         + (o.Condition < l.Shown ? o.Condition + "%, not the " + l.Shown + "% they said" : o.Condition + "%, as they said") + ".");
            Changed();
            return true;
        }

        /// <summary>The game's destination marker on a thing's door, a visit's next address or a repair shop.</summary>
        internal void ShowWay(string street, int number)
        {
            HustleGame.Guide(street, number);
            HustleText.Note(NotificationType.Info, "reallife_hustle_note_way", "place", HustleText.Place(street, number), 6f);
        }

        /// <summary>A repair or a clean by hand, at home or at another place of the player's: the panel there offers it.</summary>
        internal void PlanAtHome(int id, bool fix)
        {
            Owned o = _state != null ? _state.Item(id) : null;
            if (o == null || o.State != OwnedState.Kept || (fix ? !HustleRules.CanFix(o) : !HustleRules.CanClean(o)))
            {
                return;
            }
            o.Plan = fix ? OwnedPlan.FixAtHome : OwnedPlan.CleanAtHome;
            HustleText.Note(NotificationType.Info, fix ? "reallife_hustle_note_plan_fix" : "reallife_hustle_note_plan_clean", "thing", HustleText.Thing(o.Thing, false));
            Changed();
        }

        /// <summary>Electronics taken to the nearest repair shop open now: the way is shown, and the shop's door offers the repair.</summary>
        internal void PlanShop(int id)
        {
            Owned o = _state != null ? _state.Item(id) : null;
            if (o == null || o.State != OwnedState.Kept || !HustleRules.CanShop(o))
            {
                return;
            }
            if (!HustleGame.RepairShop(out HustlePlace shop, out string name))
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_no_shop");
                return;
            }
            o.Plan = OwnedPlan.Shop;
            o.Street = shop.Street;
            o.Number = shop.Number;
            HustleGame.Guide(shop.Street, shop.Number);
            HustleText.Note(NotificationType.Info, "reallife_hustle_note_plan_shop", "thing", HustleText.Thing(o.Thing, false), "shop", name, 10f);
            Changed();
        }

        internal void CancelPlan(int id)
        {
            Owned o = _state != null ? _state.Item(id) : null;
            if (o != null && o.State == OwnedState.Kept)
            {
                o.Plan = OwnedPlan.None;
                Changed();
            }
        }

        /// <summary>A thing put up for sale at the player's price; a car stays where it stands until a buyer takes it.</summary>
        internal void List(int id, int asking)
        {
            Owned o = _state != null ? _state.Item(id) : null;
            if (o == null || (o.State != OwnedState.Kept && o.State != OwnedState.Listed) || asking <= 0)
            {
                return;
            }
            bool was = o.State == OwnedState.Listed;
            HustleRules.List(o, asking, HustleGame.Day);
            if (!was)
            {
                HustleText.Note(NotificationType.Info, "reallife_hustle_note_listed", "thing", HustleText.Thing(o.Thing, o.Car), "amount", HustleText.Money(asking));
            }
            Changed();
        }

        internal void Unlist(int id)
        {
            Owned o = _state != null ? _state.Item(id) : null;
            if (o == null || o.State != OwnedState.Listed)
            {
                return;
            }
            o.State = OwnedState.Kept;
            for (int i = _state.Offers.Count - 1; i >= 0; i--)
            {
                if (_state.Offers[i].Item == id)
                {
                    _state.Offers.RemoveAt(i);
                }
            }
            Changed();
        }

        /// <summary>A buyer's offer taken: the money comes in and the thing goes, a car the game's own way. False (said why) if it cannot.</summary>
        internal bool Accept(int offer)
        {
            BuyerOffer d = _state != null ? _state.OfferOf(offer) : null;
            Owned o = d != null ? _state.Item(d.Item) : null;
            if (d == null || o == null)
            {
                return false;
            }
            if (o.Car)
            {
                if (HustleGame.Driving(o.VehicleId))
                {
                    HustleText.Note(NotificationType.Warning, "reallife_hustle_note_in_car");
                    return false;
                }
                if (o.VehicleId == null || HustleGame.Vehicle(o.VehicleId) == null)
                {
                    Reconcile();
                    Changed();
                    return false;
                }
                HustleGame.RemoveCar(o.VehicleId);
            }
            string thing = HustleText.Thing(o.Thing, o.Car);
            if (!o.Test)
            {
                HustleGame.Sold(d.Offer, thing);
            }
            int margin = HustleRules.Sell(_state, o, d.Buyer, d.Offer, HustleGame.Day);
            HustleText.Note(NotificationType.Success, o.Test ? "reallife_hustle_note_sold_test" : margin >= 0 ? "reallife_hustle_note_sold" : "reallife_hustle_note_sold_loss",
                "thing", thing, "amount", HustleText.Money(d.Offer), "margin", HustleText.Money(margin < 0 ? -margin : margin), 10f);
            Logger?.Info("Hustles check (sold): the " + o.Thing + " went to " + d.Buyer + " for " + d.Offer + " (cost " + (o.Paid + o.Spent) + ", margin " + margin + ")"
                         + (o.Car ? ", the car taken off the game's vehicles" : string.Empty) + (o.Test ? "; a test: no money moved" : string.Empty) + ": PASS");
            Changed();
            return true;
        }

        /// <summary>The player counters a buyer: taken, refused (they come up a little) or gone.</summary>
        internal bool Counter(int offer, int price, out bool gone)
        {
            gone = false;
            BuyerOffer d = _state != null ? _state.OfferOf(offer) : null;
            if (d == null)
            {
                return false;
            }
            bool taken = HustleRules.Counter(d, price, _state.Seed, out gone);
            if (taken)
            {
                return Accept(offer);
            }
            if (gone)
            {
                _state.Offers.Remove(d);
                HustleText.Note(NotificationType.Info, "reallife_hustle_note_buyer_gone", "who", d.Buyer);
            }
            else
            {
                HustleText.Note(NotificationType.Info, "reallife_hustle_note_buyer_up", "who", d.Buyer, "amount", HustleText.Money(d.Offer));
            }
            Changed();
            return false;
        }

        internal void Decline(int offer)
        {
            BuyerOffer d = _state != null ? _state.OfferOf(offer) : null;
            if (d != null)
            {
                _state.Offers.Remove(d);
                Changed();
            }
        }

        /// <summary>A request taken on: the client expects the player in the hours they gave.</summary>
        internal void Book(int id)
        {
            Gig g = _state != null ? _state.GigOf(id) : null;
            if (g == null || g.Stage != GigStage.Offered)
            {
                return;
            }
            HustleRules.Book(g);
            HustleGame.Guide(g.Street, g.Number);
            HustleText.Note(NotificationType.Success, "reallife_hustle_note_booked", "who", g.Client, "time", HustleText.Clock(g.From), "place", HustleText.Place(g.Street, g.Number), 10f);
            Changed();
        }

        internal void Cancel(int id)
        {
            Gig g = _state != null ? _state.GigOf(id) : null;
            if (g == null)
            {
                return;
            }
            HustleRules.Cancel(_state, g);
            HustleText.Note(NotificationType.Info, "reallife_hustle_note_cancelled", "who", g.Client, "stars", HustleCatalogue.CancelRating.ToString(CultureInfo.InvariantCulture));
            Changed();
        }

        /// <summary>A helper sent in the player's place: paid their share from what the client pays when they are done.</summary>
        internal void Send(int id)
        {
            Gig g = _state != null ? _state.GigOf(id) : null;
            if (g == null || (g.Stage != GigStage.Offered && g.Stage != GigStage.Booked))
            {
                return;
            }
            HustleRules.SendHelper(_state, g, HustleGame.Stamp);
            HustleText.Note(NotificationType.Info, "reallife_hustle_note_helper_sent", "who", g.Client, "fee", HustleText.Money(g.HelperFee), "time", HustleText.Clock(g.HelperDone), 10f);
            Logger?.Info("Hustles: a helper was sent to " + g.Client + " for " + g.HelperFee + " of " + g.Pay + "; done at hour stamp " + g.HelperDone + ".");
            Changed();
        }
    }
}
