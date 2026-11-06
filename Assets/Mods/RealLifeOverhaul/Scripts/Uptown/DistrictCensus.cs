using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>One building of a district for the census: its game building type and the customers its size has room for as that type (BuildingSizeData.customerCapacities).</summary>
    public struct CensusUnit
    {
        public string Type;      // "ba:buildingtype_retail" and so on
        public int Capacity;     // customers for that type: 15, 30, 40 or 75 for a shop, 4 to 50 for an office, 0 for a home or a warehouse
    }

    /// <summary>
    /// One district's census (plans/uptown-split.md): the figures that say how much it weighs against the others. Customer
    /// volume is the customers all its shops have room for, times the district's weight on them (NeighborhoodData's
    /// customerDemandsWeight, which CustomerEntriesHelper multiplies a shop's customers by), as one number.
    /// </summary>
    public sealed class CensusRow
    {
        public string Name;
        public double Area;              // m2 of the district's ground on the city map
        public int Buildings, Homes, Shops, Offices, Warehouses, Venues;
        public int Large;                // shops with room for 75 customers, the game's largest (M1)
        public int ShopCapacity;         // customers all its shops have room for
        public readonly int[] ByTier = new int[DistrictCensus.Tiers.Length];   // shops by customers: 15, 30, 40, 75
        public int Working, Middle, Upper;   // % of its people by class
        public float Demand;             // customerDemandsWeight

        /// <summary>The customer volume: its shops' room for customers times the district's weight.</summary>
        public double Volume => ShopCapacity * (double)Demand;

        /// <summary>Homes of upper-class people: its homes times the upper-class share, the weight of the rich in the district.</summary>
        public double UpperHomes => Homes * Upper / 100.0;

        /// <summary>The row as one line for the log, numbers in the invariant culture.</summary>
        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            var t = new StringBuilder(256);
            t.Append(Name).Append(": ").Append((Area / 1e6).ToString("0.00", c)).Append(" km2, ").Append(Buildings).Append(" buildings (").Append(Homes).Append(" homes, ")
             .Append(Shops).Append(" shops, ").Append(Offices).Append(" offices, ").Append(Warehouses).Append(" warehouses, ").Append(Venues).Append(" venues), shops by customers ");
            for (int i = 0; i < ByTier.Length; i++)
            {
                t.Append(i > 0 ? ", " : string.Empty).Append(DistrictCensus.Tiers[i]).Append('x').Append(ByTier[i]);
            }
            t.Append(", ").Append(ShopCapacity).Append(" customers in all at x").Append(Demand.ToString("0.##", c)).Append(" = volume ").Append(Volume.ToString("0", c))
             .Append("; people ").Append(Working).Append('/').Append(Middle).Append('/').Append(Upper).Append(" % working, middle and upper class, ")
             .Append(UpperHomes.ToString("0.0", c)).Append(" upper-class homes");
            return t.ToString();
        }
    }

    /// <summary>
    /// The census of a district against the game's own, game-free (EconomyCheck's UptownSplitChecks; the in-game test reads the same
    /// rows from the standing buildings): the rows are counted from a district's units, and a district passes when its customer volume
    /// and its large stores are within the most any of the game's old districts has (<see cref="Old"/>, Build 3682's, which the test
    /// reads again from the game's own data). Every answer comes from the arguments alone. COST: nothing per frame; one pass over the
    /// district's buildings when asked.
    /// </summary>
    public static class DistrictCensus
    {
        public static readonly int[] Tiers = { 15, 30, 40, 75 };   // the customers a shop has room for in the game's sizes (A1, C1, D2, M1)
        public const int LargeCapacity = 75;                       // M1's: the game's largest shop
        public const string RetailType = "ba:buildingtype_retail";
        public const string HomeType = "ba:buildingtype_residential";
        public const string OfficeType = "ba:buildingtype_office";
        public const string WarehouseType = "ba:buildingtype_warehouse";

        /// <summary>One of the game's old districts as Build 3682 has it (its addressables' buildings and sizes, NeighborhoodData and the zones on the city map).</summary>
        public sealed class OldDistrict
        {
            public readonly string Id;
            public readonly double Area;
            public readonly int Buildings, Homes, Shops, Offices, Warehouses;
            public readonly int[] ByTier;   // shops with room for 15, 30, 40 and 75 customers
            public readonly float Demand;
            public readonly int Working, Middle, Upper;

            public OldDistrict(string id, double area, int buildings, int homes, int shops, int offices, int warehouses, int[] byTier, float demand, int working, int middle, int upper)
            {
                Id = id;
                Area = area;
                Buildings = buildings;
                Homes = homes;
                Shops = shops;
                Offices = offices;
                Warehouses = warehouses;
                ByTier = byTier;
                Demand = demand;
                Working = working;
                Middle = middle;
                Upper = upper;
            }

            public int Capacity
            {
                get
                {
                    int sum = 0;
                    for (int i = 0; i < ByTier.Length; i++)
                    {
                        sum += ByTier[i] * Tiers[i];
                    }
                    return sum;
                }
            }

            public int Large => ByTier[ByTier.Length - 1];

            public double Volume => Capacity * (double)Demand;

            public double UpperHomes => Homes * Upper / 100.0;
        }

        /// <summary>The game's seven districts as Build 3682 has them (the census of the game's own buildings: not-in-repo scratch-feasibility/land, the zones' extents).</summary>
        public static readonly OldDistrict[] Old =
        {
            new OldDistrict("ba:neighborhood_garmentdistrict", 310.0 * 540.0, 166, 79, 54, 18, 6, new[] { 31, 21, 0, 2 }, 0.20f, 70, 22, 8),
            new OldDistrict("ba:neighborhood_hellskitchen", 310.0 * 540.0, 146, 84, 49, 9, 0, new[] { 18, 29, 2, 0 }, 0.70f, 6, 82, 12),
            new OldDistrict("ba:neighborhood_industrycity", 560.0 * 860.0, 200, 71, 81, 0, 41, new[] { 35, 42, 4, 0 }, 0.10f, 80, 18, 2),
            new OldDistrict("ba:neighborhood_lowermanhattan", 390.0 * 530.0, 117, 45, 29, 15, 19, new[] { 6, 17, 2, 4 }, 0.15f, 77, 19, 4),
            new OldDistrict("ba:neighborhood_midtown", 480.0 * 540.0, 94, 23, 32, 29, 0, new[] { 5, 9, 6, 12 }, 0.90f, 0, 6, 94),
            new OldDistrict("ba:neighborhood_murrayhill", 350.0 * 530.0, 103, 33, 39, 15, 8, new[] { 11, 11, 6, 11 }, 0.40f, 65, 11, 24),
            new OldDistrict("ba:neighborhood_thehamptons", 1622.0 * 955.0, 59, 28, 22, 0, 1, new[] { 5, 16, 1, 0 }, 0.10f, 2, 18, 80)
        };

        /// <summary>The most customer volume any of the old districts has.</summary>
        public static double OldVolumeMost(IList<OldDistrict> old)
        {
            double most = 0.0;
            for (int i = 0; old != null && i < old.Count; i++)
            {
                most = Math.Max(most, old[i].Volume);
            }
            return most;
        }

        /// <summary>The most large stores any old district has.</summary>
        public static int OldLargeMost(IList<OldDistrict> old)
        {
            int most = 0;
            for (int i = 0; old != null && i < old.Count; i++)
            {
                most = Math.Max(most, old[i].Large);
            }
            return most;
        }

        /// <summary>The most shops any old district has.</summary>
        public static int OldShopsMost(IList<OldDistrict> old)
        {
            int most = 0;
            for (int i = 0; old != null && i < old.Count; i++)
            {
                most = Math.Max(most, old[i].Shops);
            }
            return most;
        }

        /// <summary>The most upper-class homes any old district has.</summary>
        public static double OldUpperHomesMost(IList<OldDistrict> old)
        {
            double most = 0.0;
            for (int i = 0; old != null && i < old.Count; i++)
            {
                most = Math.Max(most, old[i].UpperHomes);
            }
            return most;
        }

        /// <summary>A district's row counted from its units.</summary>
        public static CensusRow Count(string name, double area, DistrictFigures figures, IEnumerable<CensusUnit> units)
        {
            var row = new CensusRow { Name = name, Area = area, Working = figures.Working, Middle = figures.Middle, Upper = figures.Upper, Demand = figures.Demand };
            if (units == null)
            {
                return row;
            }
            foreach (CensusUnit u in units)
            {
                row.Buildings++;
                switch (u.Type)
                {
                    case HomeType:
                        row.Homes++;
                        break;
                    case RetailType:
                        row.Shops++;
                        row.ShopCapacity += u.Capacity;
                        row.Large += u.Capacity >= LargeCapacity ? 1 : 0;
                        int tier = TierOf(u.Capacity);
                        if (tier >= 0)
                        {
                            row.ByTier[tier]++;
                        }
                        break;
                    case OfficeType:
                        row.Offices++;
                        break;
                    case WarehouseType:
                        row.Warehouses++;
                        break;
                    default:
                        row.Venues++;
                        break;
                }
            }
            return row;
        }

        // The tier a shop's room for customers falls in: the largest tier not over it; -1 for a shop with none.
        private static int TierOf(int capacity)
        {
            int at = -1;
            for (int i = 0; i < Tiers.Length; i++)
            {
                at = capacity >= Tiers[i] ? i : at;
            }
            return at;
        }

        /// <summary>
        /// Null when a district's customer volume and large stores are within the most of the old districts (the owner, 27 Oct 2026:
        /// neither district may beat the old ones); else why not, in English for the log.
        /// </summary>
        public static string Verdict(CensusRow row, double volumeMost, int largeMost)
        {
            if (row == null)
            {
                return "no row";
            }
            var c = CultureInfo.InvariantCulture;
            if (row.Volume > volumeMost + 1e-9)
            {
                return "its customer volume " + row.Volume.ToString("0", c) + " is over the old districts' most, " + volumeMost.ToString("0", c);
            }
            if (row.Large > largeMost)
            {
                return "its " + row.Large + " large stores are over the old districts' most, " + largeMost;
            }
            return null;
        }
    }
}
