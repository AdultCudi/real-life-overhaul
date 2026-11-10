using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The rules of the showroom's suppliers, game-free: what a supplier asks for a listed car, when it comes, which supplier a rival
    /// dealer buys a kind from, and what a manager orders in the morning to keep a showroom's standing order. Nothing here draws a
    /// number at the moment of ordering: a price and a delivery follow from the listing, the supplier and the hour alone, so a reload
    /// changes nothing. COST: a pass over the market's few listings for each kind a manager orders, once a morning.
    /// </summary>
    public static class CarSupplyRules
    {
        /// <summary>A kind's place in the tables from the car catalogue's kind (CarKind as a number); -1 for a kind no showroom deals in.</summary>
        public static int KindIndex(int carKind)
        {
            int k = carKind - CarSupplyCatalogue.KindFirst;
            return k >= 0 && k < CarSupplyCatalogue.Kinds ? k : -1;
        }

        /// <summary>The kind of a type of car, by the market's models; -1 for one the market does not know.</summary>
        public static int KindOf(IReadOnlyList<MarketModel> models, string type)
        {
            for (int i = 0; models != null && i < models.Count; i++)
            {
                if (models[i].Type == type)
                {
                    return KindIndex(models[i].Kind);
                }
            }
            return -1;
        }

        /// <summary>The supplier a kind of car is bought from: the one chosen while the feature is on, the wholesaler otherwise and for a kind unknown.</summary>
        public static int SupplierFor(CarSupplyState s, int kind, bool on)
        {
            if (!on || s == null || kind < 0 || kind >= CarSupplyCatalogue.Kinds)
            {
                return CarSupplyCatalogue.Wholesaler;
            }
            return DealerCatalogue.Clamp(s.Supplier[kind], 0, CarSupplyCatalogue.Suppliers - 1);
        }

        /// <summary>A supplier's price for a car the wholesaler lists at <paramref name="listed"/>: its share for the kind, named in fifties.</summary>
        public static int Price(int listed, int supplier, int kind)
        {
            if (supplier <= CarSupplyCatalogue.Wholesaler || supplier >= CarSupplyCatalogue.Suppliers || kind < 0 || kind >= CarSupplyCatalogue.Kinds)
            {
                return listed;
            }
            int share = DealerCatalogue.Clamp(CarSupplyCatalogue.Price[supplier][kind], CarSupplyCatalogue.PriceLeast, CarSupplyCatalogue.PriceMost);
            return DealerCatalogue.Named(DealerCatalogue.Share(listed, share));
        }

        /// <summary>What a showroom pays: the supplier's price, less the perk of a respected showroom's reputation as the wholesaler gives it.</summary>
        public static int ShowroomPrice(int listed, int supplier, int kind, int rep)
        {
            return DealerRules.WholesalePrice(Price(listed, supplier, kind), rep);
        }

        public static int Days(int supplier)
        {
            return supplier >= 0 && supplier < CarSupplyCatalogue.Suppliers ? Math.Max(0, CarSupplyCatalogue.Days[supplier]) : 0;
        }

        /// <summary>True when a supplier's car comes later, on a delivery that holds its place on the lot.</summary>
        public static bool Ships(int supplier) => Days(supplier) > 0;

        /// <summary>The hour a car ordered at <paramref name="now"/> is on the lot: now, or the supplier's days on at the delivery hour.</summary>
        public static int Due(int now, int supplier)
        {
            int days = Days(supplier);
            return days <= 0 ? now : (now / 24 + days) * 24 + DealerCatalogue.DeliveryHour;
        }

        /// <summary>The supplier that asks least for a kind; the wholesaler where none is cheaper.</summary>
        public static int Cheapest(int kind)
        {
            int best = CarSupplyCatalogue.Wholesaler;
            for (int s = 1; kind >= 0 && kind < CarSupplyCatalogue.Kinds && s < CarSupplyCatalogue.Suppliers; s++)
            {
                best = CarSupplyCatalogue.Price[s][kind] < CarSupplyCatalogue.Price[best][kind] ? s : best;
            }
            return best;
        }

        /// <summary>
        /// The supplier a rival dealer buys a kind of car from, the same all game: a dealer that prices keenly takes the cheapest for the
        /// kind and waits for it, one that prices high wants the car at once from the wholesaler, and one between draws for each kind.
        /// </summary>
        public static int RivalSupplier(int seed, int rival, int kind)
        {
            if (kind < 0 || kind >= CarSupplyCatalogue.Kinds)
            {
                return CarSupplyCatalogue.Wholesaler;
            }
            int bias = RivalRules.BiasOf(seed, rival);
            if (bias < CarSupplyCatalogue.RivalKeen)
            {
                return Cheapest(kind);
            }
            if (bias >= CarSupplyCatalogue.RivalQuick)
            {
                return CarSupplyCatalogue.Wholesaler;
            }
            var dice = new DealerDice(seed, rival, kind, 101);
            return dice.Chance(CarSupplyCatalogue.RivalDraw) ? Cheapest(kind) : CarSupplyCatalogue.Wholesaler;
        }

        /// <summary>A rival dealer's car as its supplier makes it: the price it asks follows what it paid, and it is on the lot when it has come.</summary>
        public static void RivalBuys(ref int ask, ref int day, int seed, int rival, int carKind, bool on)
        {
            int kind = KindIndex(carKind);
            if (!on || kind < 0)
            {
                return;
            }
            int supplier = RivalSupplier(seed, rival, kind);
            ask = Price(ask, supplier, kind);
            day += Days(supplier);
        }

        /// <summary>The standing order of a showroom for a kind, within what a lot holds.</summary>
        public static int Keep(CarSupplyState s, string place, int kind)
        {
            CarSupplyKeep k = s != null && kind >= 0 && kind < CarSupplyCatalogue.Kinds ? s.KeepOf(place, false) : null;
            return k != null ? DealerCatalogue.Clamp(k.Keep[kind], 0, CarSupplyCatalogue.KeepMost) : 0;
        }

        /// <summary>The cars of a kind a showroom has: on its lot, and on their way to it.</summary>
        public static int Have(DealerState s, DealerYard y, IReadOnlyList<MarketModel> models, int kind)
        {
            int n = 0;
            for (int c = 0; c < y.Cars.Count; c++)
            {
                n += KindOf(models, y.Cars[c].Type) == kind ? 1 : 0;
            }
            for (int i = 0; i < s.Deliveries.Count; i++)
            {
                n += s.Deliveries[i].Place == y.Place && KindOf(models, s.Deliveries[i].Type) == kind ? 1 : 0;
            }
            return n;
        }

        private static bool Planned(List<CarSupplyOrder> orders, int listing)
        {
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].Listing == listing)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// What a showroom's manager orders this morning, added to <paramref name="into"/> (which already holds what the managers of the
        /// other showrooms took, so no listing is ordered twice): for each kind short of its standing order, the cheapest new cars of
        /// the kind the market lists today, from the kind's supplier at its price, while the lot has a place, the deliveries have
        /// room and the cash covers the car. Returns the cash left; nothing is ever ordered that the cash at hand does not pay for.
        /// </summary>
        public static long Plan(DealerState s, CarSupplyState sup, DealerYard y, IReadOnlyList<MarketModel> models, long cash, int now, List<CarSupplyOrder> into)
        {
            if (s == null || sup == null || y == null || !y.Laid)
            {
                return cash;
            }
            int room = s.Room(y), ways = DealerCatalogue.DeliveriesMost - s.Deliveries.Count;
            for (int i = 0; i < into.Count; i++)
            {
                ways -= into[i].Ships ? 1 : 0;   // what the other showrooms' managers have already sent for
            }
            for (int kind = 0; kind < CarSupplyCatalogue.Kinds && room > 0; kind++)
            {
                int want = Keep(sup, y.Place, kind) - Have(s, y, models, kind);
                int supplier = SupplierFor(sup, kind, true);
                bool ships = Ships(supplier);
                while (want > 0 && room > 0 && (!ships || ways > 0))
                {
                    DealerListing best = null;
                    for (int i = 0; i < s.Market.Count; i++)
                    {
                        DealerListing l = s.Market[i];
                        if (l.Gone || l.Kind != ListingKind.Wholesale || KindOf(models, l.Type) != kind || Planned(into, l.Id))
                        {
                            continue;
                        }
                        best = best == null || l.Asking < best.Asking || (l.Asking == best.Asking && l.Id < best.Id) ? l : best;
                    }
                    if (best == null)
                    {
                        break;   // the market lists no more of the kind today
                    }
                    int price = ShowroomPrice(best.Asking, supplier, kind, y.Rep);
                    if (price <= 0 || price > cash)
                    {
                        break;   // the cheapest of the kind is more than the cash at hand: nothing of the kind today
                    }
                    into.Add(new CarSupplyOrder { Listing = best.Id, Supplier = supplier, Kind = kind, Price = price, Due = Due(now, supplier), Ships = ships });
                    cash -= price;
                    room--;
                    want--;
                    ways -= ships ? 1 : 0;
                }
            }
            return cash;
        }

        /// <summary>The supplier of each kind put inside the table, and every standing order inside what a lot holds: what a line read from a save is made to.</summary>
        public static void Bound(CarSupplyState s)
        {
            for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
            {
                s.Supplier[k] = DealerCatalogue.Clamp(s.Supplier[k], 0, CarSupplyCatalogue.Suppliers - 1);
            }
            for (int i = 0; i < s.Keeps.Count; i++)
            {
                for (int k = 0; k < CarSupplyCatalogue.Kinds; k++)
                {
                    s.Keeps[i].Keep[k] = DealerCatalogue.Clamp(s.Keeps[i].Keep[k], 0, CarSupplyCatalogue.KeepMost);
                }
            }
        }

        /// <summary>The marks of deliveries that are no longer on their way are let go, so the list never outgrows the deliveries.</summary>
        public static void Prune(CarSupplyState sup, DealerState s)
        {
            for (int i = sup.Orders.Count - 1; i >= 0; i--)
            {
                bool there = false;
                for (int d = 0; s != null && d < s.Deliveries.Count && !there; d++)
                {
                    there = s.Deliveries[d].Id == sup.Orders[i];
                }
                if (!there)
                {
                    sup.Orders.RemoveAt(i);
                    sup.OrderFrom.RemoveAt(i);
                }
            }
        }
    }
}
