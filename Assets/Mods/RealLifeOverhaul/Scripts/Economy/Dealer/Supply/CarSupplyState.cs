using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The new cars of each kind a showroom's manager keeps it stocked with; 0 for none.</summary>
    public sealed class CarSupplyKeep
    {
        public string Place;                                            // the showroom, as the dealership writes a place
        public readonly int[] Keep = new int[CarSupplyCatalogue.Kinds];
    }

    /// <summary>A new car a manager is to order this morning: the market's listing, who supplies it, what it costs and the hour it comes.</summary>
    public struct CarSupplyOrder
    {
        public int Listing;
        public int Supplier;
        public int Kind;
        public int Price;
        public int Due;         // the hour it is on the lot (day x 24 + hour); the hour of the order for a car that comes at once
        public bool Ships;      // it comes later: a delivery holds its place until then
    }

    /// <summary>
    /// What the save keeps of the showroom's suppliers, game-free: the supplier chosen for each kind of car, each showroom's standing
    /// order, the day a manager last ordered, and which of the dealership's deliveries are a supplier's (the rest are the trade auction's).
    /// A save from before has none of it: every kind comes from the wholesaler, as it always did.
    /// </summary>
    public sealed class CarSupplyState
    {
        public readonly int[] Supplier = new int[CarSupplyCatalogue.Kinds];
        public readonly List<CarSupplyKeep> Keeps = new List<CarSupplyKeep>(DealerCatalogue.YardsMost);
        public readonly List<int> Orders = new List<int>(DealerCatalogue.DeliveriesMost);      // the ids of the deliveries a supplier is bringing...
        public readonly List<int> OrderFrom = new List<int>(DealerCatalogue.DeliveriesMost);   // ...and beside each, which supplier
        public int OrderedDay = -1;                                                             // the day the managers last filled the standing orders

        public CarSupplyKeep KeepOf(string place, bool make)
        {
            for (int i = 0; i < Keeps.Count; i++)
            {
                if (Keeps[i].Place == place)
                {
                    return Keeps[i];
                }
            }
            if (!make || string.IsNullOrEmpty(place) || Keeps.Count >= DealerCatalogue.YardsMost)
            {
                return null;
            }
            var k = new CarSupplyKeep { Place = place };
            Keeps.Add(k);
            return k;
        }

        /// <summary>The supplier bringing a delivery; -1 for a car that is not a supplier's (a lot won at the trade auction).</summary>
        public int OrderOf(int delivery)
        {
            int at = Orders.IndexOf(delivery);
            return at >= 0 ? OrderFrom[at] : -1;
        }

        public void Ordered(int delivery, int supplier)
        {
            if (Orders.IndexOf(delivery) < 0)
            {
                Orders.Add(delivery);
                OrderFrom.Add(supplier);
            }
        }

        public void Arrived(int delivery)
        {
            int at = Orders.IndexOf(delivery);
            if (at >= 0)
            {
                Orders.RemoveAt(at);
                OrderFrom.RemoveAt(at);
            }
        }
    }
}
