using System;
using System.Globalization;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Car parks for build mode (docs/features/construction.md, "Parking lots"): the words a car park's squares say when the pointer is
    /// over them, the car park under a column, and the figures its panel shows. Reads only; every action is the same call the pay machine's
    /// dialogue makes (ConstructionDirector.ParkRun.cs).
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        /// <summary>The car park (under way or standing) that takes the squares of a column, or null.</summary>
        public CarParkFile ParkOfColumn(int column)
        {
            return LotOfColumn(column, out LotView lot) ? ParkOf(lot.Id) : null;
        }

        /// <summary>A line for the hover card of a lot with a car park on it: what it is, its bays and how it is let; null for none.</summary>
        internal string ParkHover(string lot)
        {
            CarParkFile c = ParkOf(lot);
            if (c == null)
            {
                return null;
            }
            if (c.UnderWay)
            {
                return ConstructionText.Fill("reallife_park_hover_building", "bays", ConstructionText.N(c.Bays), "owner", ParkOwnerName(c));
            }
            return ConstructionText.Fill("reallife_park_hover", "bays", ConstructionText.N(c.Bays), "owner", ParkOwnerName(c), "how", ParkModeWords(c));
        }

        /// <summary>A car park's state today in words, for its panel: the cars in its bays, and for the player's own, what it took today.</summary>
        internal string ParkNow(CarParkFile c)
        {
            if (c.UnderWay)
            {
                return ConstructionText.Fill("reallife_park_now_building", "day", ConstructionText.Day(c.Until));
            }
            string cars = ConstructionText.Fill("reallife_park_now_cars", "cars", ConstructionText.N(c.Cars), "bays", ConstructionText.N(c.Bays));
            return c.Owner == CarParkOwner.Player && c.Mode == CarParkMode.Public
                ? cars + ", " + ConstructionText.Fill("reallife_park_now_today", "amount", ConstructionText.Money(c.Taken / 100L))
                : cars;
        }

        /// <summary>The money a car park of the player's made over its last week, in the game's whole units (what it took less what it cost to run).</summary>
        internal long ParkWeek(CarParkFile c) => c.Net(7);

        /// <summary>The offer waiting longest at a car park of the player's, in words; null when none waits.</summary>
        internal string ParkOfferWords(CarParkFile c)
        {
            if (c.Offers.Count == 0)
            {
                return null;
            }
            CarParkOffer o = c.Offers[0];
            return ConstructionText.Fill("reallife_park_desk_offer", "who", o.Name, "bays", ConstructionText.N(o.Bays), "each", ConstructionText.Money(o.Monthly / 100L),
                "total", ConstructionText.Money(o.Monthly * o.Bays / 100L), "day", ConstructionText.Day(o.Expires), "more", ConstructionText.N(c.Offers.Count - 1));
        }

        /// <summary>What going public would pay back to the lessees of a car park of the player's today: the unused days of each month, in whole money.</summary>
        internal long ParkLeaseRefund(CarParkFile c)
        {
            long back = 0L;
            int day = ConstructionGame.Day;
            for (int i = 0; c != null && i < c.Leases.Count; i++)
            {
                CarParkLease l = c.Leases[i];
                if (l.Who != CarParkLease.Player)
                {
                    back += CarParkRules.Refund(l.Monthly, l.Bays, Math.Max(0, l.PaidTo - day + 1)) / 100L;
                }
            }
            return back;
        }

        /// <summary>The id of the first offer waiting at a car park, or 0.</summary>
        internal static int ParkFirstOffer(CarParkFile c) => c.Offers.Count > 0 ? c.Offers[0].Id : 0;

        /// <summary>The price an hour of a car park in cents, with the band it moves in as "least to most", for its panel.</summary>
        internal string ParkBandWords(CarParkFile c)
        {
            int fair = ParkFair(c.Lot);
            return ConstructionText.Fill("reallife_park_band", "least", ConstructionText.Cents(CarParkRules.PriceLeast(fair)), "most", ConstructionText.Cents(CarParkRules.PriceMost(fair)));
        }

        /// <summary>The percentage of a car park's bays holding a car, for its panel.</summary>
        internal static string ParkFillWords(CarParkFile c)
        {
            return CarParkRules.Occupied(c.Cars, c.Bays).ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
