using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Redevelopment's rules (docs/features/construction.md, "Redevelopment"), one for each thing and the same for the player and
    /// the city's developers: a demolition's works, its permit, the notice to a building's tenants and its business and what
    /// buying them out costs, the rubble hauled away and a contractor's quote. Money is at the start's prices and charged at
    /// today's (the caller's price level, in thousandths). Game-free: EconomyCheck's RedevelopChecks compiles it.
    /// </summary>
    public static class RedevelopRules
    {
        // ------------------------------------------------------------------ demolition

        public const int DemolitionWorks = 6;                   // the programme's demolition works (Civic's SiteKind.Demolition): the works tables' row, the site's kind
        public const int DemolishTenthsPerHundred = 30;         // tenths of a working hour for each 100 m2 of floor: knocking down is quicker than putting up
        public const int DemolishHoursLeast = 40, DemolishHoursMost = 240;
        public const int RubbleArea = 100;                      // m2 of floor: a lorry load of rubble each
        public const int RubbleLoadPrice = 2400;                // money a load hauled away and tipped
        public const int PermitFee = 900;                       // money, and the building's value's thousandths below
        public const int PermitFeePermille = 1;
        public const int PermitDaysLeast = 3, PermitDaysMost = 6;   // working days to the permit's decision
        public const int PermitArea = 2500;                     // m2 of floor: a working day more for each, up to the most
        public const int NoticeDays = 7;                        // days' notice to a building's tenants and business before its works
        public const int TenantDays = 14;                       // days of what the tenants pay, paid to buy them out
        public const int BusinessDays = 30;                     // days of the unit's rent, paid to buy its business out
        public const float LookDownShare = 0.6f;                // of a demolition's works: its fourth of Civic's five phases, the building coming down behind the wrap

        /// <summary>The working hours knocking down a building of so many square metres of floor takes at the usual pace.</summary>
        public static int DemolitionHours(int area)
        {
            long hours = (long)Math.Max(0, area) * DemolishTenthsPerHundred / 1000L;
            return (int)Math.Max(DemolishHoursLeast, Math.Min(DemolishHoursMost, hours));
        }

        /// <summary>Lorry loads of rubble a building of so many square metres of floor leaves: one for each hundred, at least one.</summary>
        public static int RubbleLoads(int area)
        {
            return Math.Max(1, (Math.Max(0, area) + RubbleArea - 1) / RubbleArea);
        }

        /// <summary>What hauling a building's rubble away costs, at today's prices.</summary>
        public static long RubbleCost(int area, int prices)
        {
            return (long)RubbleLoads(area) * RubbleLoadPrice * Math.Max(1, prices) / 1000L;
        }

        /// <summary>The demolition permit's fee at City Hall: a fee at today's prices and a share of the building's value.</summary>
        public static long PermitFeeOf(long value, int prices)
        {
            return (long)PermitFee * Math.Max(1, prices) / 1000L + Math.Max(0L, value) * PermitFeePermille / 1000L;
        }

        /// <summary>Working days to the demolition permit's decision: a few, a day more for a large building.</summary>
        public static int PermitDays(int area)
        {
            return Math.Max(PermitDaysLeast, Math.Min(PermitDaysMost, PermitDaysLeast + Math.Max(0, area) / PermitArea));
        }

        /// <summary>
        /// What buying a building's tenants and its business out costs: <see cref="TenantDays"/> of what its tenants pay a day
        /// (the deed's occupancy of its floor area, its rent a square metre), and <see cref="BusinessDays"/> of the unit's rent
        /// when a business of the city's trades in it. Nothing for an empty building.
        /// </summary>
        public static long BuyOut(double tenantsDaily, double unitRentDaily, bool business)
        {
            double cost = Math.Max(0.0, tenantsDaily) * TenantDays + (business ? Math.Max(0.0, unitRentDaily) * BusinessDays : 0.0);
            return cost <= 0.0 || double.IsNaN(cost) || double.IsInfinity(cost) ? 0L : (long)Math.Round(cost);
        }

        /// <summary>The day a notice given on <paramref name="day"/> runs out: the works may start the day after.</summary>
        public static int NoticeEnds(int day)
        {
            return day + NoticeDays;
        }

        /// <summary>
        /// The city's best demolition contractor's quote: every one of the city's builders bids on the works (their margin,
        /// speciality and luck; Dunmore and Pike the demolition house), the best score wins. The same seed and day give the same
        /// quote. Its price covers the rubble hauled away too.
        /// </summary>
        public static RefitQuote Quote(int seed, int day, int area, int prices)
        {
            int hours = DemolitionHours(area);
            long value = TenderRules.WorksValue(DemolitionWorks, hours, prices) + RubbleCost(area, prices);
            int days = TenderRules.StandardDays(hours);
            var bids = new TenderBid[BuildCatalogue.RivalCount];
            int count = CommissionRules.Bids(seed, day * 37 + 11, value, days, Speciality.Demolition, null, bids);   // the one way the city's builders quote a client
            TenderBid won = bids[Math.Max(0, TenderRules.Winner(bids, count))];
            return new RefitQuote { Rival = won.Bidder, Price = won.Price / 10L * 10L, Days = Math.Max(1, won.Days), Hours = hours };
        }
    }
}
