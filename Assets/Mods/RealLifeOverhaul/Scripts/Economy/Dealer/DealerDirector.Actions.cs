using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>Why something the player asked of the dealership could not be done; the text key says so.</summary>
    public enum DealerRefusal
    {
        None, Off, NoLot, LotFull, Cash, Gone, Busy, NoBids, Leading, NotInside, Staff, InWorkshop, Bays, Nothing, EventOn, Rested
    }

    /// <summary>What the player and the staff do; each refuses with the game's notice, or moves the money, the car and the state together.</summary>
    public sealed partial class DealerDirector
    {
        private static readonly string[] RefusalKeys =
        {
            null, "reallife_dealer_refuse_off", "reallife_dealer_refuse_no_lot", "reallife_dealer_refuse_full", "reallife_dealer_refuse_cash", "reallife_dealer_refuse_gone",
            "reallife_dealer_refuse_busy", "reallife_dealer_refuse_no_bids", "reallife_dealer_refuse_leading", "reallife_dealer_refuse_inside", "reallife_dealer_refuse_staff",
            "reallife_dealer_refuse_workshop", "reallife_dealer_refuse_bays", "reallife_dealer_refuse_nothing", "reallife_dealer_refuse_event_on", "reallife_dealer_refuse_rested"
        };

        private bool Refuse(DealerRefusal why)
        {
            if (why != DealerRefusal.None)
            {
                Notifications.ShowError(RefusalKeys[(int)why]);
            }
            return why == DealerRefusal.None;
        }

        /// <summary>The first showroom with room on its lot, the given one first (a place held for a delivery counts as taken); null with the reason.</summary>
        internal DealerYard Room(string want, out DealerRefusal why)
        {
            why = DealerRefusal.None;
            if (!_running || _state == null)
            {
                why = DealerRefusal.Off;
                return null;
            }
            DealerYard first = want != null ? _state.Yard(want) : null;
            if (first != null && _state.Room(first) > 0)
            {
                return first;
            }
            bool anyLaid = false;
            for (int i = 0; i < _state.Yards.Count; i++)
            {
                DealerYard y = _state.Yards[i];
                anyLaid |= y.Laid;
                if (_state.Room(y) > 0 && RegOf(y) != null)
                {
                    return y;
                }
            }
            why = anyLaid ? DealerRefusal.LotFull : DealerRefusal.NoLot;
            return null;
        }

        // A car onto a free place of a lot as a game vehicle; null if it cannot be made. Places held for deliveries stay free unless
        // it is one (held).
        private StockCar Arrive(DealerYard y, string type, string colour, int km, int condition, int faults, int known, int cost, CarSource source, bool held = false)
        {
            BuildingRegistration reg = RegOf(y);
            int spot = y.FreeSpot();
            if (reg == null || spot < 0 || (!held && _state.Room(y) <= 0))
            {
                return null;
            }
            string id = DealerGame.MakeCar(reg, y, spot, type, colour, condition);
            if (id == null)
            {
                return null;
            }
            var car = new StockCar
            {
                Id = _state.Next(), VehicleId = id, Type = type, Colour = colour, Km = km, Condition = condition, Faults = faults, Known = known, Cost = cost,
                Bought = Save.Day, Source = source, Spot = spot
            };
            car.Asking = DealerRules.Asking(Worth(car, Prices.Level));
            y.Cars.Add(car);
            _ids.Add(id);
            DealerRules.Most(y, DealerCatalogue.TallyLot, y.Cars.Count);
            Advance(y);
            return car;
        }

        /// <summary>The player buys a new car, or a used one at the price agreed (asking, or the owner's counter).</summary>
        public bool Buy(int listing, string yardPlace, int price)
        {
            DealerListing l = _state != null ? _state.Listing(listing) : null;
            if (l == null || l.Gone || l.Kind == ListingKind.Auction)
            {
                return Refuse(_running ? DealerRefusal.Gone : DealerRefusal.Off);
            }
            DealerYard y = Room(yardPlace, out DealerRefusal why);
            if (y == null)
            {
                return Refuse(why);
            }
            BuildingRegistration reg = RegOf(y);
            if (l.Kind == ListingKind.Wholesale)
            {
                price = DealerRules.WholesalePrice(l.Asking, y.Rep);   // a respected showroom's perk; the app shows the same
            }
            if (!DealerGame.Take(price, DealerLines.BoughtType, DealerGame.Bought(l.Type), reg, false))
            {
                return Refuse(DealerRefusal.Cash);
            }
            StockCar car = Arrive(y, l.Type, l.Colour, l.Km, l.Condition, l.Faults, l.Inspected ? l.Faults : 0, price, l.Kind == ListingKind.Wholesale ? CarSource.Wholesale : CarSource.Used);
            if (car == null)
            {
                DealerGame.Give(price, DealerLines.BoughtType, DealerGame.Bought(l.Type), reg);   // the money back: the car could not be made
                Logger?.Warn("Dealership: the game could not make a '" + l.Type + "' on the lot of '" + y.Name + "'; the money was given back.");
                return Refuse(DealerRefusal.Busy);
            }
            l.Gone = true;
            Book(y, 0, price, 0, 0);
            Logger?.Info("Dealership: a " + l.Type + " bought " + (l.Kind == ListingKind.Wholesale ? "new" : "used") + " for " + price + " onto the lot of '" + y.Name
                         + "', asked at " + car.Asking + ".");
            Tell(NotificationType.Info, "reallife_dealer_note_bought",
                new Dictionary<string, string>(3) { { "car", DealerGame.CarName(l.Type) }, { "name", y.Name }, { "price", DealerText.Money(price) } }, TabStock, CarRow(car));
            Changed();
            return true;
        }

        internal static string CarRow(StockCar car) => "car:" + car.Id;

        /// <summary>An inspection of a used car or a lot, paid at the showroom: its faults and true condition are known.</summary>
        public bool Inspect(int listing, string yardPlace)
        {
            DealerListing l = _state != null ? _state.Listing(listing) : null;
            if (l == null || l.Gone || l.Inspected)
            {
                return Refuse(_running ? DealerRefusal.Gone : DealerRefusal.Off);
            }
            int fee = InspectionFee(l);
            DealerYard y = yardPlace != null ? _state.Yard(yardPlace) : null;
            y = y ?? (_state.Yards.Count > 0 ? _state.Yards[0] : null);
            BuildingRegistration reg = y != null ? RegOf(y) : null;
            if (reg == null)
            {
                return Refuse(DealerRefusal.NoLot);
            }
            if (!DealerGame.Take(fee, DealerLines.InspectionKey, DealerGame.Named(reg), reg, false))
            {
                return Refuse(DealerRefusal.Cash);
            }
            DealerRules.Inspect(l);
            if (l.Kind == ListingKind.Auction)
            {
                l.Shown = l.Condition;
            }
            Book(y, 0, fee, 0, 0);
            Changed();
            return true;
        }

        internal int InspectionFee(DealerListing l)
        {
            return l.Kind == ListingKind.Auction ? Prices.At(DealerCatalogue.AuctionInspection)
                : DealerRules.InspectionFee(Worth(l), Prices.At(DealerCatalogue.InspectionLeast), Prices.At(DealerCatalogue.InspectionMost));
        }

        /// <summary>The player offers an owner a price: taken (and bought), countered, or the owner sells elsewhere.</summary>
        public HaggleAnswer Haggle(int listing, string yardPlace, int price, out int counter)
        {
            counter = 0;
            DealerListing l = _state != null ? _state.Listing(listing) : null;
            if (l == null || l.Gone || l.Kind != ListingKind.Used)
            {
                Refuse(_running ? DealerRefusal.Gone : DealerRefusal.Off);
                return HaggleAnswer.WalkedAway;
            }
            if (Room(yardPlace, out DealerRefusal why) == null)
            {
                Refuse(why);
                return HaggleAnswer.Countered;
            }
            HaggleAnswer answer = DealerRules.Haggle(l, price, out counter);
            if (answer == HaggleAnswer.Taken)
            {
                if (!Buy(listing, yardPlace, price))
                {
                    return HaggleAnswer.Countered;
                }
            }
            Changed();
            return answer;
        }

        /// <summary>The player bids the next step on a lot, if the cash and a place on a lot are there for it.</summary>
        public bool Bid(int listing)
        {
            DealerListing l = _state != null ? _state.Listing(listing) : null;
            if (l == null || l.Gone || l.Kind != ListingKind.Auction || Now >= l.Ends)
            {
                return Refuse(_running ? DealerRefusal.Gone : DealerRefusal.Off);
            }
            if (_state.NoBidsUntil >= Save.Day)
            {
                return Refuse(DealerRefusal.NoBids);
            }
            if (l.Leader == 0)
            {
                return Refuse(DealerRefusal.Leading);
            }
            if (Room(null, out DealerRefusal why) == null)
            {
                return Refuse(why);
            }
            int worth = Worth(l), bid = DealerRules.NextBid(l, worth);
            if (DealerGame.Cash < bid + DealerRules.BuyersFee(bid, Prices.At(DealerCatalogue.BuyersFeeLeast)))
            {
                return Refuse(DealerRefusal.Cash);
            }
            DealerRules.Bid(l, worth, Now);
            Changed();
            return true;
        }

        // An hour of the trade auction: the room bids, the lots of the hour are knocked down, and a new sale opens when it is time.
        private void Auction(int now, int level)
        {
            int outbid = DealerRules.RoomHour(_state, now, _models, level);
            if (outbid > 0)
            {
                Tell(NotificationType.Info, "reallife_dealer_note_outbid", new Dictionary<string, string>(1) { { "count", outbid.ToString() } }, TabMarket, null);
            }
            DealerRules.KnockedDown(_state, now, _down);
            for (int i = 0; i < _down.Count; i++)
            {
                DealerListing l = _down[i];
                l.Gone = true;
                if (l.Leader != 0)
                {
                    continue;   // another dealer's, or nobody's
                }
                int fee = DealerRules.BuyersFee(l.Asking, Prices.At(DealerCatalogue.BuyersFeeLeast));
                DealerYard y = Room(null, out DealerRefusal why);
                BuildingRegistration reg = y != null ? RegOf(y) : null;
                bool paid = reg != null && _state.Deliveries.Count < DealerCatalogue.DeliveriesMost && DealerGame.Cash >= l.Asking + fee
                            && DealerGame.Take(l.Asking, DealerLines.BoughtType, DealerGame.Bought(l.Type), reg, false);
                DealerDelivery car = null;
                if (paid)
                {
                    DealerGame.Take(fee, DealerLines.FeeKey, DealerGame.Named(reg), reg, true);
                    car = Ship(l, y, l.Asking + fee, now);   // its place on the lot is held until it comes
                }
                if (car == null)
                {
                    _state.NoBidsUntil = Save.Day + DealerCatalogue.NoPayDays;
                    Tell(NotificationType.Warning, "reallife_dealer_note_unpaid",
                        new Dictionary<string, string>(2) { { "car", DealerGame.CarName(l.Type) }, { "days", DealerCatalogue.NoPayDays.ToString() } }, TabMarket, null);
                    Logger?.Info("Dealership: a lot won at " + l.Asking + " could not be paid for or put on a lot (" + why + "); no bids for " + DealerCatalogue.NoPayDays + " days.");
                    continue;
                }
                Book(y, 0, l.Asking + fee, 0, 0);
                Logger?.Info("Dealership: a " + l.Type + " won at the trade auction for " + l.Asking + " and a fee of " + fee + "; it arrives at '" + y.Name + "' at hour " + car.Due + ".");
                Tell(NotificationType.Info, "reallife_dealer_note_won", new Dictionary<string, string>(4)
                    { { "car", DealerGame.CarName(l.Type) }, { "price", DealerText.Money(l.Asking) }, { "name", y.Name }, { "when", DealerText.When(car.Due) } }, TabStock, DeliveryRow(car));
            }
            if (DealerRules.SaleDue(_state, now % 24))
            {
                DealerRules.OpenSale(_state, now, _models, _colours, level);
            }
        }

        /// <summary>
        /// A deal closed at a price: the money of the sale (cash, or a deposit with the rest on the showroom's finance, any
        /// part exchange counted in the price), a warranty and extras if the buyer takes them, the car off the lot and their
        /// own car onto its place. A buyer refused finance pays cash or walks away. True if sold.
        /// </summary>
        internal bool Close(DealerYard y, BuildingRegistration reg, DealerOffer d, int price, bool byStaff)
        {
            StockCar car = y.Car(d.Car);
            if (car == null || reg == null)
            {
                y.Deals.Remove(d);
                return false;
            }
            bool finance = d.WantsFinance && y.Finance && DealerRules.Lends(d.Score);
            if (d.WantsFinance && !finance)
            {
                var dice = new DealerDice(_state.Seed, d.Id, 83);
                if (!dice.Chance(DealerCatalogue.FinanceCash))
                {
                    y.Deals.Remove(d);
                    Tell(NotificationType.Info, "reallife_dealer_note_no_finance",
                        new Dictionary<string, string>(2) { { "customer", d.Customer }, { "car", DealerGame.CarName(car.Type) } }, TabOffers, null);
                    Changed();
                    return false;
                }
            }
            int tier = y.Warranty ? d.Tier : -1;
            int premium = DealerRules.Premium(price, tier, Prices.At(DealerCatalogue.PremiumLeast));
            int allowance = d.TradeType != null ? d.TradeAllowance : 0;
            DealMoney m = DealerRules.Settle(price, allowance, finance, d.Deposit, d.Term, d.Score, premium, DealerGame.DaysPerYear);
            int worth = Worth(car, Prices.Level);

            // Booked as the game books a vehicle sold: what came in today, the part exchange in it at its allowance.
            DealerGame.Give(m.Deposit + m.Allowance, DealerLines.SoldType, DealerGame.Sold(car.Type, reg), reg);
            if (m.Allowance > 0)
            {
                DealerGame.Take(m.Allowance, DealerLines.BoughtType, DealerGame.Bought(d.TradeType), reg, true);   // their car, bought at the allowance
            }
            if (m.Premium > 0)
            {
                DealerGame.Give(m.Premium, DealerLines.PremiumKey, DealerGame.Named(reg), reg);
                if (_state.Covers.Count < DealerCatalogue.CoversMost)
                {
                    _state.Covers.Add(new DealerCover
                    {
                        Id = _state.Next(), Place = y.Place, Customer = d.Customer, Type = car.Type, Tier = tier, Worth = worth, Condition = car.Condition,
                        Until = Save.Day + DealerCatalogue.TierDays[tier]
                    });
                }
            }
            if (m.OnFinance && _state.Plans.Count < DealerCatalogue.PlansMost)
            {
                _state.Plans.Add(new DealerPlan
                {
                    Id = _state.Next(), Place = y.Place, Customer = d.Customer, Type = car.Type, Worth = worth, Owed = m.Financed + m.Interest, Weekly = m.Weekly,
                    Due = Save.Day + DealerCatalogue.WeekDays, Score = d.Score
                });
            }
            else if (m.OnFinance)
            {
                DealerGame.Give(m.Financed, DealerLines.FinanceKey, DealerGame.Named(reg), reg);   // no room for one more plan: the finance is sold on at once
            }

            // Each extra on its own line, bought in by the showroom as it sells it.
            int extras = y.Extras ? d.Extras : 0, extrasIn = 0, extrasCost = 0;
            for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
            {
                if ((extras & (1 << e)) == 0)
                {
                    continue;
                }
                int sell = DealerRules.ExtraPrice(price, e, Prices.At(DealerCatalogue.ExtraLeast[e]));
                int buy = DealerRules.ExtraCost(sell, e);
                DealerGame.Give(sell, DealerLines.ExtraKeys[e], DealerGame.Named(reg), reg);
                DealerGame.Take(buy, DealerLines.ExtrasCostKey, DealerGame.Named(reg), reg, true);
                extrasIn += sell;
                extrasCost += buy;
            }

            DealerGame.RemoveCar(car.VehicleId);
            _ids.Remove(car.VehicleId);
            y.Cars.Remove(car);
            RemoveDeals(y, car.Id);
            StockCar swapped = null;
            if (d.TradeType != null)
            {
                var faultDice = new DealerDice(_state.Seed, d.Id, 89);
                int faults = DealerRules.DrawFaults(d.TradeKm, d.TradeCondition, ref faultDice);
                swapped = Arrive(y, d.TradeType, d.TradeColour, d.TradeKm, d.TradeCondition, faults, 0, m.Allowance, CarSource.TradeIn);
                if (swapped == null)
                {
                    // No place for it after all: it goes straight to the trade.
                    int trade = DealerRules.TradePrice(DealerRules.Worth(DealerGame.ListPrice(d.TradeType), Prices.Level, d.TradeKm, d.TradeCondition));
                    DealerGame.Give(trade, DealerLines.SoldType, DealerGame.Sold(d.TradeType, reg), reg);
                    Book(y, trade, 0, 0, trade - m.Allowance);
                }
            }
            if (_state.Sales.Count >= DealerCatalogue.SalesKept)
            {
                _state.Sales.RemoveAt(_state.Sales.Count - 1);
            }
            _state.Sales.Insert(0, new DealerSale
            {
                Day = Save.Day, Place = y.Place, Customer = d.Customer, Type = car.Type, Price = price, Cost = car.Cost, Financed = m.OnFinance, Tier = m.Premium > 0 ? tier : -1, Extras = extras
            });
            Book(y, m.Deposit + m.Allowance + m.Premium + extrasIn, m.Allowance + extrasCost, 0, price - car.Cost + extrasIn - extrasCost);
            y.Today.Sold++;
            y.Today.Revenue += price;
            y.Today.Extras += extrasIn;

            // The showroom's reputation and its missions.
            int margin = price - car.Cost;
            DealerRules.Count(y, DealerCatalogue.TallySold, 1);
            DealerRules.Count(y, DealerCatalogue.TallyMargin, margin >= DealerCatalogue.Share(price, DealerCatalogue.GoodMargin) ? 1 : 0);
            DealerRules.Count(y, DealerCatalogue.TallyTradeIn, m.Allowance > 0 ? 1 : 0);
            DealerRules.Count(y, DealerCatalogue.TallyFinance, m.OnFinance ? 1 : 0);
            DealerRules.Count(y, DealerCatalogue.TallyExtras, DealerRules.ExtraCount(extras));
            DealerRules.Count(y, DealerCatalogue.TallyQuick, Save.Day - car.Bought <= DealerCatalogue.QuickDays ? 1 : 0);
            Repute(y, DealerRules.SaleRep(car.Condition, car.Faults));
            Advance(y);
            Logger?.Info("Dealership: " + d.Customer + " bought a " + car.Type + " at '" + y.Name + "' for " + price + (byStaff ? " (closed by the staff)" : string.Empty)
                         + ": " + m.Deposit + " today" + (m.Allowance > 0 ? ", a part exchange at " + m.Allowance : string.Empty)
                         + (m.OnFinance ? ", " + m.Financed + " on finance at " + m.Weekly + " a week for " + m.Weeks + " weeks" : string.Empty)
                         + (m.Premium > 0 ? ", a warranty for " + m.Premium : string.Empty) + (extrasIn > 0 ? ", extras for " + extrasIn + " bought in at " + extrasCost : string.Empty)
                         + "; the car had cost " + car.Cost + "; reputation now " + y.Rep + ".");
            Tell(NotificationType.Info, byStaff ? "reallife_dealer_note_sold_staff" : "reallife_dealer_note_sold", new Dictionary<string, string>(4)
                { { "customer", d.Customer }, { "car", DealerGame.CarName(car.Type) }, { "price", DealerText.Money(price) }, { "margin", DealerText.Money(price - car.Cost) } },
                swapped != null ? TabStock : TabOffers, swapped != null ? CarRow(swapped) : null);
            Changed();
            return true;
        }

        private DealerOffer FindDeal(int deal, out DealerYard yard)
        {
            yard = null;
            for (int y = 0; _state != null && y < _state.Yards.Count; y++)
            {
                DealerOffer d = _state.Yards[y].Deal(deal);
                if (d != null)
                {
                    yard = _state.Yards[y];
                    return d;
                }
            }
            return null;
        }

        public bool Accept(int deal)
        {
            DealerOffer d = FindDeal(deal, out DealerYard y);
            if (d == null || d.Stage != OfferStage.Open)
            {
                return Refuse(d == null ? DealerRefusal.Gone : DealerRefusal.Busy);
            }
            return Close(y, RegOf(y), d, d.Offer, false);
        }

        /// <summary>The player counters a customer's offer: taken (and sold), or not; a second refused counter and they walk away.</summary>
        public bool Counter(int deal, int price, out bool gone)
        {
            gone = false;
            DealerOffer d = FindDeal(deal, out DealerYard y);
            if (d == null || d.Stage != OfferStage.Open)
            {
                return Refuse(d == null ? DealerRefusal.Gone : DealerRefusal.Busy);
            }
            bool taken = DealerRules.Counter(d, price, _state.Seed, out gone);
            if (taken)
            {
                return Close(y, RegOf(y), d, d.Offer, false);
            }
            if (gone)
            {
                y.Deals.Remove(d);
                Repute(y, -DealerCatalogue.RepWalked);
                Tell(NotificationType.Info, "reallife_dealer_note_walked", new Dictionary<string, string>(1) { { "customer", d.Customer } }, TabOffers, null);
            }
            Changed();
            return false;
        }

        public void Decline(int deal)
        {
            DealerOffer d = FindDeal(deal, out DealerYard y);
            if (d != null)
            {
                y.Deals.Remove(d);
                Changed();
            }
        }

        /// <summary>The player sends a customer out on the test drive they asked for, with nobody on shift to do it.</summary>
        public bool Drive(int deal)
        {
            DealerOffer d = FindDeal(deal, out DealerYard y);
            StockCar car = d != null ? y.Car(d.Car) : null;
            if (d == null || car == null || d.Stage != OfferStage.Open || d.Drove)
            {
                return Refuse(d == null ? DealerRefusal.Gone : DealerRefusal.Busy);
            }
            DealerRules.TestDrive(d, car, Now, _state.Seed);
            DealerGame.Wear(car);
            Changed();
            return true;
        }

        public void Ask(int car, int asking)
        {
            StockCar c = _state != null ? _state.Car(car, out DealerYard _) : null;
            if (c != null && asking > 0)
            {
                c.Asking = asking;
                Changed();
            }
        }

        internal int PrepareCost(StockCar c)
        {
            return DealerRules.PrepareCost(DealerRules.Worth(DealerGame.ListPrice(c.Type), Prices.Level, 0, 1000), c.Condition, c.Known, Prices.At(DealerCatalogue.PrepareBase));
        }

        /// <summary>A car prepared: a valet, a check and the known faults put right; an asking price at the default follows its new worth.</summary>
        public bool Prepare(int car)
        {
            DealerYard yard = null;
            StockCar c = _state != null ? _state.Car(car, out yard) : null;
            if (c == null)
            {
                return Refuse(_running ? DealerRefusal.Gone : DealerRefusal.Off);
            }
            if (c.ShopUntil > Now)
            {
                return Refuse(DealerRefusal.InWorkshop);
            }
            BuildingRegistration reg = RegOf(yard);
            int cost = PrepareCost(c);
            if (!DealerGame.Take(cost, DealerLines.PrepareKey, DealerGame.Named(reg), reg, false))
            {
                return Refuse(DealerRefusal.Cash);
            }
            int worthBefore = Worth(c, Prices.Level);
            bool followed = c.Asking == DealerRules.Asking(worthBefore);
            c.Condition = DealerRules.Prepared(c.Condition, c.Faults, c.Known);
            c.Faults &= ~c.Known;
            c.Known = 0;
            c.Cost += cost;
            if (followed)
            {
                c.Asking = DealerRules.Asking(Worth(c, Prices.Level));
            }
            DealerGame.Wear(c);
            Book(yard, 0, cost, 0, 0);
            Changed();
            return true;
        }

        /// <summary>A car sold to the trade at once: another dealer pays its share of the car's worth.</summary>
        public bool SellToTrade(int car)
        {
            DealerYard yard = null;
            StockCar c = _state != null ? _state.Car(car, out yard) : null;
            if (c == null)
            {
                return Refuse(_running ? DealerRefusal.Gone : DealerRefusal.Off);
            }
            if (c.ShopUntil > Now)
            {
                return Refuse(DealerRefusal.InWorkshop);
            }
            BuildingRegistration reg = RegOf(yard);
            if (reg == null)
            {
                return Refuse(DealerRefusal.NoLot);
            }
            int price = DealerRules.TradePrice(Worth(c, Prices.Level));
            DealerGame.Give(price, DealerLines.SoldType, DealerGame.Sold(c.Type, reg), reg);
            DealerGame.RemoveCar(c.VehicleId);
            _ids.Remove(c.VehicleId);
            yard.Cars.Remove(c);
            RemoveDeals(yard, c.Id);
            if (_state.Sales.Count >= DealerCatalogue.SalesKept)
            {
                _state.Sales.RemoveAt(_state.Sales.Count - 1);
            }
            _state.Sales.Insert(0, new DealerSale { Day = Save.Day, Place = yard.Place, Customer = string.Empty, Type = c.Type, Price = price, Cost = c.Cost, Trade = true });
            Book(yard, price, 0, 0, price - c.Cost);
            Logger?.Info("Dealership: a " + c.Type + " of '" + yard.Name + "' sold to the trade for " + price + "; it had cost " + c.Cost + ".");
            Changed();
            return true;
        }

        public void Floor(string place, int share)
        {
            DealerYard y = _state != null ? _state.Yard(place) : null;
            if (y != null)
            {
                y.StaffFloor = DealerCatalogue.Clamp(share, DealerCatalogue.StaffFloorLeast, DealerCatalogue.StaffFloorMost);
                Changed();
            }
        }

        public void OfferFinance(string place, bool on)
        {
            DealerYard y = _state != null ? _state.Yard(place) : null;
            if (y != null)
            {
                y.Finance = on;
                Changed();
            }
        }

        public void OfferWarranty(string place, bool on)
        {
            DealerYard y = _state != null ? _state.Yard(place) : null;
            if (y != null)
            {
                y.Warranty = on;
                Changed();
            }
        }

        /// <summary>The lot of the showroom the player stands in laid out again from its floor as it is now, its cars moved to the new places.</summary>
        public bool Relay(string place)
        {
            DealerYard y = _state != null ? _state.Yard(place) : null;
            BuildingRegistration reg = y != null ? RegOf(y) : null;
            if (reg == null || !DealerGame.Inside(reg))
            {
                return Refuse(DealerRefusal.NotInside);
            }
            int had = y.Spots.Count;
            if (!DealerGame.Lay(y))
            {
                return Refuse(DealerRefusal.Busy);
            }
            for (int c = 0; c < y.Cars.Count; c++)
            {
                y.Cars[c].Spot = c < y.Spots.Count ? c : -1;
            }
            DealerGame.Park(y, _lost, _bays);
            Logger?.Info("Dealership: the lot of '" + y.Name + "' laid out again: " + had + " place(s) before, " + y.Spots.Count + " now.");
            Changed();
            return true;
        }
    }
}
