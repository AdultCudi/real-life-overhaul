using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>What the showroom's suppliers ask of the rest of the mod, with a default; Wiring fills it in.</summary>
    public static class CarSupplyBridges
    {
        /// <summary>
        /// True where a manager of the company decides the stock of the business at a place today (Economy/Company, the Stock orders task,
        /// through the door every decision of a manager passes). Default: nobody does.
        /// </summary>
        public static Func<string, bool> Manages;

        public static void Reset()
        {
            Manages = null;
        }
    }

    /// <summary>
    /// The showroom's suppliers (docs/features/car-suppliers.md): the supplier chosen for each kind of car, what a new car costs
    /// from it and when it comes, the order that holds a place on the lot until then, and the standing orders a manager fills each
    /// morning. Every purchase goes through <see cref="Buy"/>, the one path a new car is paid for by. Its own line of the save;
    /// with the option off every kind comes from the wholesaler as before, and cars already ordered still arrive. COST: nothing per
    /// frame; a pass over the market's listings once a morning for each showroom with a standing order.
    /// </summary>
    public sealed partial class DealerDirector
    {
        private CarSupplyState _supply = new CarSupplyState();
        private readonly List<CarSupplyOrder> _orders = new List<CarSupplyOrder>(DealerCatalogue.SpotsMost);
        private Func<DealerYard, bool> _supplyDecides;   // made once: the hour asks every hour

        /// <summary>True while the choice of suppliers is in force.</summary>
        internal bool SupplyOn => _running && Settings.EconomyDealerSupply;

        /// <summary>The suppliers' state. Read it only while the dealership runs.</summary>
        internal CarSupplyState Supply => _supply;

        private void SupplyLoad(GameInstance save)
        {
            _supply = CarSupplyStore.Load(save.modData, out int version) ?? new CarSupplyState();
            CarSupplyRules.Prune(_supply, _state);
            if (version > 0)
            {
                Logger?.Info("Dealership: the suppliers' line read (version " + version + "): " + _supply.Keeps.Count + " standing order(s), " + _supply.Orders.Count + " car(s) on order.");
            }
        }

        private void SupplyWrite(GameInstance save)
        {
            CarSupplyRules.Prune(_supply, _state);
            save.modData[CarSupplyStore.Key] = CarSupplyStore.Write(_supply);
        }

        private void SupplyForget()
        {
            _supply = new CarSupplyState();
        }

        /// <summary>The kind of a listing's car as the suppliers' tables count it; -1 for one no supplier brings.</summary>
        internal int SupplyKind(DealerListing l)
        {
            return l != null ? CarSupplyRules.KindOf(_models, l.Type) : -1;
        }

        /// <summary>The supplier a new car on the market would come from: the kind's, or the wholesaler with the option off.</summary>
        internal int SupplierOf(DealerListing l)
        {
            return l != null && l.Kind == ListingKind.Wholesale ? CarSupplyRules.SupplierFor(_supply, SupplyKind(l), SupplyOn) : CarSupplyCatalogue.Wholesaler;
        }

        /// <summary>What a new car costs a showroom from its kind's supplier; the pages show it and <see cref="Buy"/> charges it.</summary>
        internal int NewPrice(DealerListing l, DealerYard y)
        {
            return CarSupplyRules.ShowroomPrice(l.Asking, SupplierOf(l), SupplyKind(l), y != null ? y.Rep : 0);
        }

        /// <summary>The hour a new car bought now would be on the lot.</summary>
        internal int NewDue(DealerListing l)
        {
            return CarSupplyRules.Due(Now, SupplierOf(l));
        }

        // A new car from a supplier that delivers later: refused before any money if no delivery can be taken, then paid for and sent,
        // its place on the lot held until it comes. True if ordered.
        private bool BuyShipped(DealerListing l, DealerYard y, BuildingRegistration reg, int price, int supplier)
        {
            if (CarSupplyRules.WaysFull(_state))
            {
                Notifications.ShowError("reallife_carsup_refuse_ways");
                return false;
            }
            if (!DealerGame.Take(price, DealerLines.BoughtType, DealerGame.Bought(l.Type), reg, false))
            {
                return Refuse(DealerRefusal.Cash);
            }
            var q = new DealerDelivery
            {
                Id = _state.Next(), Place = y.Place, Type = l.Type, Colour = l.Colour, Km = l.Km, Condition = l.Condition, Faults = l.Faults, Known = l.Faults,
                Cost = price, Due = CarSupplyRules.Due(Now, supplier)
            };
            _state.Deliveries.Add(q);
            _supply.Ordered(q.Id, supplier);
            l.Gone = true;
            Book(y, 0, price, 0, 0);
            Logger?.Info("Dealership: a " + l.Type + " ordered new from supplier " + supplier + " for " + price + "; it arrives at '" + y.Name + "' at hour " + q.Due + ".");
            Tell(NotificationType.Info, "reallife_carsup_note_ordered", new Dictionary<string, string>(5)
            {
                { "car", DealerGame.CarName(l.Type) }, { "supplier", CarSupplyText.Name(supplier) }, { "price", DealerText.Money(price) }, { "name", y.Name },
                { "when", DealerText.When(q.Due) }
            }, TabStock, DeliveryRow(q));
            Changed();
            return true;
        }

        /// <summary>What the game's question box asks before a new car is bought: for one that comes later, from whom and when.</summary>
        internal string NewQuestion(DealerListing l, int price)
        {
            int supplier = SupplierOf(l);
            return CarSupplyRules.Ships(supplier)
                ? DealerText.Fill("reallife_carsup_q_order", "car", DealerGame.CarName(l.Type), "supplier", CarSupplyText.Name(supplier), "price", DealerText.Money(price),
                    "when", DealerText.When(NewDue(l)))
                : DealerText.T("reallife_dealer_q_buy", "car", DealerGame.CarName(l.Type), "price", DealerText.Money(price));
        }

        /// <summary>For the save tools: the suppliers' line is removed and every kind comes from the wholesaler again; cars on order still arrive.</summary>
        public void ForgetSupply()
        {
            GameInstance save = Save;
            if (save != null && save.modData != null)
            {
                save.modData.Remove(CarSupplyStore.Key);
            }
            SupplyForget();
            Revision++;
        }

        // A delivery as its car goes onto the lot: a supplier's is a new car, another is a lot won at the trade auction.
        private CarSource DeliverySource(DealerDelivery q)
        {
            return CarSupplyRules.SourceOf(_supply, q.Id);
        }

        // The notice of a delivery: the supplier's own words for a car it brought, the auction's otherwise.
        private string DeliveryNote(DealerDelivery q, string auction, string supplier)
        {
            return _supply.OrderOf(q.Id) >= 0 ? supplier : auction;
        }

        /// <summary>The player chooses the supplier of a kind of car. Cars already ordered keep the terms they were ordered on.</summary>
        public bool SetSupplier(int kind, int supplier)
        {
            if (!SupplyOn || kind < 0 || kind >= CarSupplyCatalogue.Kinds || supplier < 0 || supplier >= CarSupplyCatalogue.Suppliers)
            {
                return Refuse(DealerRefusal.Off);
            }
            if (_supply.Supplier[kind] != supplier)
            {
                _supply.Supplier[kind] = supplier;
                Logger?.Info("Dealership: kind " + kind + " of car is now bought from supplier " + supplier + ".");
                Changed();
            }
            return true;
        }

        /// <summary>The player sets how many new cars of a kind a showroom's manager keeps it stocked with; 0 ends the standing order.</summary>
        public bool SetKeep(string place, int kind, int count)
        {
            DealerYard y = SupplyOn && place != null ? _state.Yard(place) : null;
            if (y == null || kind < 0 || kind >= CarSupplyCatalogue.Kinds)
            {
                return Refuse(DealerRefusal.Off);
            }
            CarSupplyKeep keep = _supply.KeepOf(place, true);
            if (keep == null)
            {
                return Refuse(DealerRefusal.Off);
            }
            keep.Keep[kind] = DealerCatalogue.Clamp(count, 0, Math.Min(CarSupplyCatalogue.KeepMost, Math.Max(y.Spots.Count, DealerCatalogue.SpotsLeast)));
            Changed();
            return true;
        }

        /// <summary>True where a manager orders this showroom's stock, so its standing order is filled each morning.</summary>
        internal bool SupplyManaged(DealerYard y)
        {
            try
            {
                return y != null && CarSupplyBridges.Manages != null && CarSupplyBridges.Manages(y.Place);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        // The morning's standing orders (CarSupplyRules.Morning), once a game day for each showroom however often the hour is worked or the save is
        // loaded, and only where somebody decides today: every car through Buy.
        private void SupplyMorning(int day, int now)
        {
            if (!SupplyOn || _state.MarketDay != day)
            {
                return;
            }
            _supplyDecides = _supplyDecides ?? SupplyDecides;
            SupplyOrders(day, now, _supplyDecides);
        }

        // A showroom the player owns whose manager decides today.
        private bool SupplyDecides(DealerYard y) => RegOf(y) != null && SupplyManaged(y);

        private int SupplyOrders(int day, int now, Func<DealerYard, bool> decides)
        {
            _orders.Clear();
            CarSupplyRules.Morning(_state, _supply, _models, day, now, DealerGame.Cash, SupplyOn, decides, _orders);
            int bought = 0, spent = 0;
            for (int i = 0; i < _orders.Count; i++)
            {
                CarSupplyOrder o = _orders[i];
                if (Buy(o.Listing, o.Place, o.Price))
                {
                    bought++;
                    spent += o.Price;
                }
            }
            if (Verbose || bought > 0)
            {
                Logger?.Info("Dealership check (suppliers, the standing orders of hour " + now + "): " + _orders.Count + " planned, " + bought + " ordered for " + spent
                             + ", cash " + DealerGame.Cash + ": " + (bought == _orders.Count ? "PASS" : "some could not be bought") + ".");
            }
            _orders.Clear();
            return bought;
        }

        // ------------------------------------------------------------------ Options, Tests

        /// <summary>"A supplier's order now": the first new car on the market bought from the cheapest supplier for its kind, which is chosen for the kind first.</summary>
        public void TestSupplyOrder()
        {
            const string test = "Dealership supplier's order";
            if (!SupplyOn || _state == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            DealerListing pick = null;
            for (int i = 0; i < _state.Market.Count && pick == null; i++)
            {
                DealerListing l = _state.Market[i];
                pick = !l.Gone && l.Kind == ListingKind.Wholesale && SupplyKind(l) >= 0 ? l : null;
            }
            DealerYard y = pick != null ? Room(null, out DealerRefusal _) : null;
            if (pick == null || y == null)
            {
                Notifications.ShowError(pick == null ? "reallife_carsup_test_none" : "reallife_dealer_no_room");
                Logger?.Info("Dealership check (test, a supplier's order): " + (pick == null ? "no new car is listed today" : "no showroom has a free place") + ".");
                return;
            }
            int kind = SupplyKind(pick), supplier = CarSupplyRules.Cheapest(kind);
            SetSupplier(kind, supplier);
            int price = NewPrice(pick, y), ways = _state.Deliveries.Count, cars = y.Cars.Count, cash = DealerGame.Cash;
            bool done = Buy(pick.Id, y.Place, price);
            bool right = done && cash - DealerGame.Cash == price && (CarSupplyRules.Ships(supplier) ? _state.Deliveries.Count == ways + 1 : y.Cars.Count == cars + 1);
            Logger?.Info("Dealership check (test, a supplier's order): kind " + kind + " from supplier " + supplier + " at " + price + " against the wholesaler's "
                         + DealerRules.WholesalePrice(pick.Asking, y.Rep) + ", due hour " + CarSupplyRules.Due(Now, supplier) + ": " + (right ? "PASS" : done ? "FAIL" : "not bought") + ".");
        }

        /// <summary>"A manager's morning order now": a standing order of one more car of a kind listed today at the first showroom, filled at once as its manager would.</summary>
        public void TestSupplyManager()
        {
            const string test = "Dealership manager's order";
            if (!SupplyOn || _state == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            DealerYard y = Room(null, out DealerRefusal _);
            DealerListing pick = null;
            for (int i = 0; i < _state.Market.Count && pick == null; i++)
            {
                DealerListing l = _state.Market[i];
                pick = !l.Gone && l.Kind == ListingKind.Wholesale && SupplyKind(l) >= 0 ? l : null;
            }
            if (pick == null || y == null)
            {
                Notifications.ShowError(pick == null ? "reallife_carsup_test_none" : "reallife_dealer_no_room");
                Logger?.Info("Dealership check (test, a manager's order): " + (pick == null ? "no new car is listed today" : "no showroom has a free place") + ".");
                return;
            }
            int kind = SupplyKind(pick), had = CarSupplyRules.Have(_state, y, _models, kind);
            SetKeep(y.Place, kind, had + 1);
            _supply.KeepOf(y.Place, true).Day = -1;   // the test fills it at once, whatever the day's mark
            int bought = SupplyOrders(Now / 24, Now, p => p == y && RegOf(p) != null);
            int has = CarSupplyRules.Have(_state, y, _models, kind);
            Notifications.Show(NotificationType.Info, "reallife_carsup_test_manager", new Dictionary<string, string>(2) { { "count", bought.ToString() }, { "name", y.Name } }, 8f);
            Logger?.Info("Dealership check (test, a manager's order): the standing order of kind " + kind + " at '" + y.Name + "' is " + CarSupplyRules.Keep(_supply, y.Place, kind)
                         + ", it had " + had + " and has " + has + ", " + bought + " ordered: " + (has <= CarSupplyRules.Keep(_supply, y.Place, kind) && bought >= 0 ? "PASS" : "FAIL") + ".");
        }
    }
}
