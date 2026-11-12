using System;

namespace RealLifeOverhaul.Economy
{
}

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a unit is, from the game's floor area: its size, and for a home its bedrooms. Game-free.</summary>
    public static class UnitRules
    {
        /// <summary>Bedrooms of a home from its floor area in square metres: a studio under 35, then one for about every 20 over 30, to five.</summary>
        public static int Bedrooms(int sqm)
        {
            return sqm < 35 ? 0 : Math.Min(5, Math.Max(1, (sqm - 15) / 20));
        }

        /// <summary>The floor area of one unit of a building: the building's over its units, at least a square metre.</summary>
        public static int UnitSqm(int totalSqm, int units)
        {
            return Math.Max(1, totalSqm / Math.Max(1, units));
        }

        /// <summary>
        /// The market rent of ONE unit, dollars a day: its floor area times what a square metre rents for in its district and use (the rent map's read of
        /// the game's buildings), the whole city's mean unit of that use where the district has none of it, 0 where the map knows neither. A building's rent
        /// is never the answer for a unit: a shop of a building of many floors is one of its units. The estate's offer to the company's lease rule
        /// (CompanyBridges.MarketRent: district, place, the game's own rent) at the merge: the company asks for one rented premises.
        /// </summary>
        public static int MarketRentDay(RentMapData map, string district, int use, int totalSqm, int units)
        {
            if (map == null || map.Day < 0 || use < 0 || use >= RentMapData.Uses)
            {
                return 0;
            }
            int at = map.Districts.IndexOf(district ?? string.Empty);
            int perSqm = at >= 0 ? map.PerSqm(at, use) : -1;   // cents a month for a square metre
            long monthly = perSqm >= 0 ? (long)UnitSqm(totalSqm, units) * perSqm / 100 : Math.Max(0, map.MeanUnit(use));
            return (int)Math.Min(int.MaxValue / 4, monthly / Math.Max(1, map.MonthDays));
        }
    }
}
