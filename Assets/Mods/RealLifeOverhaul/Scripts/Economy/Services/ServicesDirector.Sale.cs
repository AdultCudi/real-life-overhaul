using System;
using System.Collections.Generic;
using Localizor;
using Streets;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Selling a business: the director's part. What a business is worth and what stands on the market are worked out
    /// when the page asks; offers come, lapse and are paid on the day event; a sale completes when the player
    /// confirms the game's own question, and only then.
    ///
    /// THE DAY OF A SALE, in this order, each step the game's own or another feature's own:
    ///   1  the day's payment is booked (a transaction of the mod's own)
    ///   2  the investors with a share of the business are bought out at Ventures' own price, through Ventures
    ///   3  business loans secured on it are paid off as the game's own pay-off does, through the credit side's link
    ///   4  the staff, the business and the lease end as the game ends them (ServicesGame.Complete)
    ///   5  the mod's own records of the address are closed: the listing and its offers, the cleaning round's entry;
    ///      "running a business" is told, and drops the business's insurance, licences, campaign and footfall
    ///   6  parts of the price still to come, a clause and the capital gain are noted
    /// A sale that would leave an investor or a lender unpaid is refused before the question is asked.
    /// </summary>
    public sealed partial class ServicesDirector
    {
        private static readonly string[] BlockKeys =
        {
            null, "reallife_svc_refuse_not_business", "reallife_svc_refuse_inside", "reallife_svc_refuse_vehicle", "reallife_svc_refuse_gift", "reallife_svc_refuse_busy",
            "reallife_svc_refuse_stock"
        };

        private readonly List<BuildingRegistration> _regs = new List<BuildingRegistration>(16);

        /// <summary>What a business is worth now, line by line.</summary>
        public SaleBreakdown Value(BuildingRegistration reg)
        {
            return SaleRules.Value(ServicesGame.Inputs(reg));
        }

        private void SaleGuard(string what, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                SaleFault(what, e);
            }
        }

        // ---------------------------------------------------------------- the market

        /// <summary>A business goes on the market at a step of the asking price, or its asking price is changed.</summary>
        public void List(string place, int step)
        {
            if (!Ready(false))
            {
                return;
            }
            SaleGuard("listing a business", () =>
            {
                BuildingRegistration reg = ServicesGame.Find(place);
                if (!ServicesGame.IsSellable(reg))
                {
                    Notifications.ShowError("reallife_svc_refuse_not_business");
                    return;
                }
                SaleBreakdown b = Value(reg);
                bool was = _state.Listing(place) != null;
                SaleListing l = SaleRules.List(_state, place, ServicesStore.Clean(ServicesGame.Name(reg)), b.Value, step, Save.Day);
                if (l == null)
                {
                    Notifications.ShowError("reallife_svc_refuse_listings_full");
                    return;
                }
                Logger?.Info("Selling a business: '" + l.Name + "' " + (was ? "now asks" : "is on the market, asking") + " " + l.Asking + " (" + ServicesCatalogue.AskingPct[l.Step] + "% of a value of " + l.Value + ": goodwill "
                             + b.Goodwill + ", stock and furniture " + b.Fixtures + "; investors " + b.Investors + ", loans " + b.Loans + "). An offer every " + ServicesCatalogue.AskingGapDays[l.Step] + " day(s), "
                             + ServicesCatalogue.AskingOffersMost[l.Step] + " at most.");
                Changed();
            });
        }

        public void Withdraw(string place)
        {
            if (!Ready(false))
            {
                return;
            }
            SaleGuard("withdrawing a business", () =>
            {
                SaleListing l = _state.Listing(place);
                if (l != null)
                {
                    Logger?.Info("Selling a business: '" + l.Name + "' is taken off the market; " + _state.OffersFor(place) + " offer(s) go with it.");
                    _state.Unlist(place);
                    Changed();
                }
            });
        }

        public void Refuse(int offerId)
        {
            if (!Ready(false))
            {
                return;
            }
            SaleGuard("refusing an offer", () =>
            {
                SaleOffer o = _state.SaleOffer(offerId);
                if (o != null)
                {
                    _state.SaleOffers.Remove(o);
                    Changed();
                }
            });
        }

        /// <summary>The one counter an offer allows: a tenth more.</summary>
        public void Counter(int offerId)
        {
            if (!Ready(false))
            {
                return;
            }
            SaleGuard("a counter", () =>
            {
                SaleOffer o = _state.SaleOffer(offerId);
                SaleListing l = o != null ? _state.Listing(o.Place) : null;
                if (o == null || o.Countered)
                {
                    return;
                }
                int was = o.Price;
                string name = l != null ? l.Name : "?";
                CounterResult result = SaleRules.Counter(_state, o, l != null ? l.Asking : o.Price);
                string key = result == CounterResult.Accepted ? "reallife_svc_note_counter_met" : result == CounterResult.HalfWay ? "reallife_svc_note_counter_half"
                    : result == CounterResult.Walked ? "reallife_svc_note_counter_walked" : "reallife_svc_note_counter_refused";
                NotificationType type = result == CounterResult.Walked ? NotificationType.Error : result == CounterResult.Refused ? NotificationType.Warning : NotificationType.Success;
                Tell(type, key, TabSale, result == CounterResult.Walked ? "sell:" + o.Place : "bid:" + o.Id, "buyer", ServicesText.Buyer(o), "business", name, "amount", ServicesText.Money(o.Price));
                Logger?.Info("Selling a business: a counter to offer " + o.Id + " for '" + name + "' (" + was + ", a tenth more asked): " + result + (result == CounterResult.Walked ? ", the offer is gone" : ", the price is " + o.Price) + ".");
                Changed();
            });
        }

        /// <summary>An offer is accepted: the game's own question is asked, and the sale completes on its answer.</summary>
        public void Accept(int offerId)
        {
            SaleOffer o = Ready(false) ? _state.SaleOffer(offerId) : null;
            if (o != null)
            {
                Ask(o.Place, o.Price, o.Parts, o.KeepsStaff, o.ClauseDays, o.Buyer, ServicesText.Buyer(o));
            }
        }

        /// <summary>A quick sale: seven tenths of the value, at once, to a dealer in businesses who keeps nobody and asks for no clause.</summary>
        public void QuickSale(string place)
        {
            if (!Ready(false))
            {
                return;
            }
            BuildingRegistration reg = ServicesGame.Find(place);
            Ask(place, SaleRules.Quick(ServicesGame.IsSellable(reg) ? Value(reg).Value : 0), 0, false, 0, (int)BuyerKind.Investor, ServicesText.T("reallife_svc_buyer_quick"));
        }

        // The game's question before the irreversible step. Everything is looked at twice: before the question and again on its answer.
        private void Ask(string place, int price, int parts, bool keepsStaff, int clauseDays, int buyer, string buyerName)
        {
            SaleGuard("asking before a sale", () =>
            {
                BuildingRegistration reg = ServicesGame.Find(place);
                SaleBreakdown b;
                SaleMoney m;
                if (!MaySell(reg, price, parts, out b, out m))
                {
                    return;
                }
                string name = ServicesGame.Name(reg);
                int staff = ServicesGame.StaffAt(reg);
                // The terms that apply, each a sentence of its own, with a space after each.
                string terms = string.Empty;
                if (m.Investors + m.Loans > 0)
                {
                    terms += ServicesText.T("reallife_svc_confirm_owed", "investors", ServicesText.Money(m.Investors), "loans", ServicesText.Money(m.Loans)) + " ";
                }
                if (staff > 0)
                {
                    terms += ServicesText.T(keepsStaff ? "reallife_svc_confirm_staff_go" : "reallife_svc_confirm_staff_stay", "count", staff.ToString()) + " ";
                }
                if (clauseDays > 0)
                {
                    terms += ServicesText.T("reallife_svc_confirm_clause", "days", clauseDays.ToString(), "share", ServicesCatalogue.ClausePenaltyPct.ToString()) + " ";
                }
                string body = ServicesText.Fill("reallife_svc_confirm_sell_body", "business", name, "buyer", buyerName, "price", ServicesText.Money(price),
                    "paid", parts > 1 ? ServicesText.Fill("reallife_svc_confirm_paid_parts", "now", ServicesText.Money(m.Now), "part", ServicesText.Money(m.Part), "count", m.PartsLeft.ToString()) : ServicesText.T("reallife_svc_confirm_paid_once"),
                    "terms", terms);
                HudConfirm.Show("reallife_svc_confirm_sell_title".Localize(new { business = name }), "reallife_svc_confirm_sell_text".Localize(new { text = body }),
                    () => SaleGuard("completing a sale", () => Sell(place, price, parts, keepsStaff, clauseDays, buyer, buyerName)), null, null, null, false, true);
            });
        }

        // Whether a sale at this price can complete now; says why not in a notice of the game's.
        private bool MaySell(BuildingRegistration reg, int price, int parts, out SaleBreakdown b, out SaleMoney m)
        {
            b = default(SaleBreakdown);
            m = default(SaleMoney);
            SaleBlock block = ServicesGame.Blocked(reg);
            if (block != SaleBlock.None)
            {
                Notifications.ShowError(BlockKeys[(int)block]);
                return false;
            }
            b = Value(reg);
            if (price <= 0 && b.Value <= 0)
            {
                Notifications.ShowError("reallife_svc_refuse_worthless");
                return false;
            }
            m = SaleRules.Settle(price, parts, b, ServicesGame.Cash);
            if (m.Short > 0)
            {
                Notifications.Show(NotificationType.Error, "reallife_svc_refuse_short", new Dictionary<string, string>(2) { { "amount", ServicesText.Money(m.Short) }, { "owed", ServicesText.Money(m.Investors + m.Loans) } }, 10f);
                return false;
            }
            return true;
        }

        // The sale itself: after the game's question was answered with yes.
        private void Sell(string place, int price, int parts, bool keepsStaff, int clauseDays, int buyer, string buyerName)
        {
            if (!Ready(false))
            {
                return;
            }
            BuildingRegistration reg = ServicesGame.Find(place);
            SaleBreakdown b;
            SaleMoney m;
            if (!MaySell(reg, price, parts, out b, out m))
            {
                return;
            }
            int day = Save.Day;
            Address address = reg.Address;
            string name = ServicesStore.Clean(ServicesGame.Name(reg)), type, district;
            ServicesGame.Trade(reg, out type, out district);

            // 1 to 3: the money, the investors, the lenders.
            Pay(m.Now, ServicesGame.SaleKey, name, address, null, "the price of " + price + (parts > 1 ? " in " + parts + " parts, the first today" : ", at once") + " from " + buyerName);
            if (m.Investors > 0 && ServicesBridges.PayInvestors != null && !ServicesBridges.PayInvestors(reg))
            {
                Charge(m.Now, ServicesGame.SaleKey, name, address, null, false, true, "the sale is called off: an investor could not be bought out, so the day's payment goes back");
                Notifications.ShowError("reallife_svc_refuse_investors");
                Logger?.Warn("Selling a business: the sale of '" + name + "' is called off. An investor could not be bought out through Ventures; the payment was returned and nothing of the business was changed.");
                return;
            }
            bool loans = m.Loans <= 0 || ServicesBridges.SettleLoans == null || ServicesBridges.SettleLoans(address);
            if (!loans)
            {
                Logger?.Warn("Selling a business: a business loan secured on '" + name + "' could not be paid off; it stays owed, and the credit side will find it unsecured.");
            }

            // 4: the staff, the business and the lease, as the game ends them.
            int staffGone, deposit;
            string log;
            SaleAfter after = ServicesGame.Complete(reg, keepsStaff, out staffGone, out deposit, out log);

            // 5 and 6: the mod's own records.
            _state.Unlist(place);
            CleaningClient client = _state.Client(place);
            if (client != null && !client.Outside)
            {
                _state.Clients.Remove(client);
            }
            SaleDue due = SaleRules.Due(_state, name, buyer, m, day);
            SaleClause clause = SaleRules.Clause(_state, type, district, name, price, clauseDays, day);
            SaleRules.Record(_state, name, price, m.Gain, day);
            try
            {
                ServicesBridges.Sold?.Invoke(place, price);   // (a listed company's business sold: its price is all its holders')
            }
            catch (Exception e)
            {
                Logger?.Warn("Selling a business: the stock market could not be told that '" + name + "' was sold (" + e.Message + ").");
            }
            try
            {
                ServicesBridges.BusinessGone?.Invoke(place);
            }
            catch (Exception e)
            {
                Logger?.Warn("Selling a business: 'running a business' could not be told that '" + name + "' is gone (" + e.Message + "); it finds out by itself in the morning.");
            }

            bool clean = !reg.RentedByPlayer && reg.itemInstances.Count == 0 && ServicesGame.StaffAt(reg) == 0 && !ServicesGame.IsSellable(reg) && _state.Listing(place) == null && _state.OffersFor(place) == 0;
            Logger?.Info("Selling a business: sold " + log + ". Price " + price + " from " + buyerName + ": " + m.Now + " today" + (due != null ? ", " + due.Left + " part(s) of " + due.Part + " from day " + due.NextDay : string.Empty)
                         + "; investors bought out for " + m.Investors + ", loans paid off " + (loans ? m.Loans.ToString() : "0 (could not be paid)") + "; capital gain noted " + m.Gain
                         + (clause != null ? "; no business of type " + type + " in " + district + " until day " + clause.Until : string.Empty) + ".");
            Logger?.Info("Services check (sale, " + name + "): the building is " + (reg.RentedByPlayer ? "STILL" : "no longer") + " rented by the player, " + reg.itemInstances.Count + " item(s) left in it, " + ServicesGame.StaffAt(reg)
                         + " employee(s) still assigned to it, the mod's listing and offers for it " + (_state.Listing(place) == null && _state.OffersFor(place) == 0 ? "closed" : "STILL OPEN") + ": " + (clean ? "PASS" : "FAIL: something of the business is left"));
            Tell(NotificationType.Success, after == SaleAfter.Rival ? "reallife_svc_note_sold_rival" : after == SaleAfter.Owned ? "reallife_svc_note_sold_owned" : "reallife_svc_note_sold_rent", TabSale, null,
                "business", name, "buyer", buyerName, "amount", ServicesText.Money(m.Now), "deposit", ServicesText.Money(deposit));
            if (CleaningOn)
            {
                ReadAll();
                Replan("a business was sold");
            }
            Changed();
        }

        /// <summary>
        /// A business handed over to a buyer with no money, as a sale completes (a takeover of the player's listed company
        /// paid for its shares: Markets/Listing): business loans secured on it are paid off as in any sale, then the staff
        /// (the buyer keeps them), the business and the lease end as the game ends them, and the mod's own records of it
        /// close; an investor's deal on it ends as Ventures ends any deal on a business that is gone. True once it is no
        /// longer the player's (or was not); false while it cannot be done now (the player stands in it, a vehicle or a
        /// special gift is kept at it, a Going Public session is on, or this feature is not running): asked again later.
        /// </summary>
        public bool HandOver(string place, string buyerName)
        {
            if (!_running || _broken || _saleBroken || GoingPublic.InSession)
            {
                return false;
            }
            BuildingRegistration reg = ServicesGame.Find(place);
            if (reg == null || !ServicesGame.IsSellable(reg))
            {
                return true;   // (not the player's business any more)
            }
            if (ServicesGame.Blocked(reg) != SaleBlock.None)
            {
                return false;
            }
            bool done = false;
            SaleGuard("handing a business over in a takeover", () =>
            {
                string name = ServicesStore.Clean(ServicesGame.Name(reg));
                Address address = reg.Address;
                if (ServicesBridges.LoansOwed != null && ServicesBridges.LoansOwed(address) > 0 && (ServicesBridges.SettleLoans == null || !ServicesBridges.SettleLoans(address)))
                {
                    Logger?.Warn("Selling a business: a business loan secured on '" + name + "' could not be paid off in the takeover; it stays owed, and the credit side will find it unsecured.");
                }
                int staffGone, deposit;
                string log;
                ServicesGame.Complete(reg, true, out staffGone, out deposit, out log);
                _state.Unlist(place);
                CleaningClient client = _state.Client(place);
                if (client != null && !client.Outside)
                {
                    _state.Clients.Remove(client);
                }
                try
                {
                    ServicesBridges.BusinessGone?.Invoke(place);
                }
                catch (Exception e)
                {
                    Logger?.Warn("Selling a business: 'running a business' could not be told that '" + name + "' is gone (" + e.Message + "); it finds out by itself in the morning.");
                }
                Logger?.Info("Selling a business: handed over to " + buyerName + " in a takeover: " + log + "; the deposit back " + deposit + ".");
                done = true;
            });
            if (done)
            {
                if (CleaningOn)
                {
                    ReadAll();
                    Replan("a business was handed over");
                }
                Changed();
            }
            return done;
        }

        // ---------------------------------------------------------------- the day

        private void SaleDay(int day)
        {
            // Businesses on the market: one that is no longer the player's, or has stood too long, comes off; the rest may draw an offer.
            for (int i = _state.Listings.Count - 1; i >= 0; i--)
            {
                SaleListing l = _state.Listings[i];
                BuildingRegistration reg = ServicesGame.Find(l.Place);
                if (!ServicesGame.IsSellable(reg))
                {
                    Logger?.Info("Selling a business: '" + l.Name + "' is no longer a business of the player's; it comes off the market with its offers.");
                    _state.Unlist(l.Place);
                    continue;
                }
                if (SaleRules.Expired(l, day))
                {
                    _state.Unlist(l.Place);
                    Tell(NotificationType.Info, "reallife_svc_note_listing_over", TabSale, "sell:" + l.Place, "business", l.Name, "days", ServicesCatalogue.ListingDays.ToString());
                    continue;
                }
            }
            int lapsed = SaleRules.ExpireOffers(_state, day);
            for (int i = 0; i < _state.Listings.Count; i++)
            {
                SaleListing l = _state.Listings[i];
                if (!SaleRules.OfferDue(l, day))
                {
                    continue;
                }
                MakeOffer(l, day);
            }

            // Parts of a price that fall due.
            for (int i = _state.Dues.Count - 1; i >= 0; i--)
            {
                SaleDue d = _state.Dues[i];
                int part = SaleRules.TakeDue(d, day);
                if (part > 0)
                {
                    Pay(part, ServicesGame.PartKey, d.Name, null, null, "a part of the price, " + d.Left + " still to come");
                    Tell(NotificationType.Success, d.Left > 0 ? "reallife_svc_note_part" : "reallife_svc_note_part_last", TabSale, null, "business", d.Name, "amount", ServicesText.Money(part), "count", d.Left.ToString());
                }
                if (d.Left <= 0)
                {
                    _state.Dues.RemoveAt(i);
                }
            }

            // Clauses: one that has run out goes; one that a business of the player's breaks is charged, once.
            SaleRules.ExpireClauses(_state, day);
            if (_state.Clauses.Count > 0)
            {
                ServicesGame.Sellable(_regs);
                for (int i = _state.Clauses.Count - 1; i >= 0; i--)
                {
                    SaleClause c = _state.Clauses[i];
                    for (int r = 0; r < _regs.Count; r++)
                    {
                        string type, district;
                        ServicesGame.Trade(_regs[r], out type, out district);
                        if (!SaleRules.Breaks(c, type, district, ServicesGame.Created(_regs[r])))
                        {
                            continue;
                        }
                        int penalty = SaleRules.ClausePenalty(c.Price);
                        Charge(penalty, ServicesGame.ClauseKey, c.Name, null, null, false, true, "'" + ServicesGame.Name(_regs[r]) + "' is a business of the kind sold, in the same district, taken on day " + ServicesGame.Created(_regs[r])
                                                                                                    + " inside the clause's days (" + c.FromDay + " to " + c.Until + "): " + ServicesCatalogue.ClausePenaltyPct + "% of the price of " + c.Price);
                        Tell(NotificationType.Error, "reallife_svc_note_clause_broken", TabSale, null, "business", c.Name, "other", ServicesGame.Name(_regs[r]), "amount", ServicesText.Money(penalty));
                        _state.Clauses.RemoveAt(i);
                        break;
                    }
                }
                _regs.Clear();
            }
            if (Verbose && (lapsed > 0 || _state.Listings.Count > 0 || _state.Dues.Count > 0 || _state.Clauses.Count > 0))
            {
                Logger?.Info("Services check (the market, day " + day + "): " + _state.Listings.Count + " business(es) on the market, " + _state.SaleOffers.Count + " offer(s) standing, " + lapsed + " lapsed today, "
                             + _state.Dues.Count + " price(s) still being paid, " + _state.Clauses.Count + " clause(s) running: PASS");
            }
        }

        // A listing's turn for an offer: its value is taken afresh from today's figures first. Returns the offer, or null if this turn brings none.
        private SaleOffer MakeOffer(SaleListing l, int day)
        {
            BuildingRegistration reg = ServicesGame.Find(l.Place);
            SaleBreakdown b = Value(reg);
            l.Value = b.Value;
            l.Asking = SaleRules.Asking(l.Value, l.Step);
            SaleOffer offer = SaleRules.Offer(_state, l, day);
            if (Verbose)
            {
                Logger?.Info("Services check (an offer, " + l.Name + ", day " + day + "): value " + l.Value + ", asking " + l.Asking + " (" + ServicesCatalogue.AskingPct[l.Step] + "%), turn " + l.OffersMade + " of "
                             + ServicesCatalogue.AskingOffersMost[l.Step] + ": " + (offer != null
                                 ? ((BuyerKind)offer.Buyer) + " offers " + offer.Price + (offer.Parts > 1 ? " in " + offer.Parts + " parts" : " at once") + ", " + (offer.KeepsStaff ? "keeps" : "does not keep") + " the staff, clause "
                                   + offer.ClauseDays + " day(s), until day " + offer.Deadline + "; never above the asking price: " + (offer.Price <= l.Asking ? "PASS" : "FAIL")
                                 : "nobody comes this time; the next turn is on day " + l.NextOfferDay + ": PASS"));
            }
            if (offer != null)
            {
                Tell(NotificationType.Info, "reallife_svc_note_sale_offer", TabSale, "bid:" + offer.Id, "business", l.Name, "buyer", ServicesText.Buyer(offer), "amount", ServicesText.Money(offer.Price), "day", offer.Deadline.ToString());
            }
            return offer;
        }
    }
}
