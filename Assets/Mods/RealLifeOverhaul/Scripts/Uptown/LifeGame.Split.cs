using System;
using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using Buildings;
using Entities;
using Helpers;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The game's side of the split (SplitRules): what a save made when the band was one district holds under Uptown's id that
    /// the second district needs a copy of, so each of the two has its own from the first load: the product market's demand
    /// rows (the game makes a missing one itself at its daily pass, but logs an error for every ask before it), the city map's
    /// ticked rows. The stats are copied by <see cref="Stats"/> and the market levels by the market pass. Nothing here runs a
    /// frame: once, at the first load after the split, behind the loading screen.
    /// </summary>
    internal static partial class LifeGame
    {
        /// <summary>
        /// The save's demand rows and ticked map rows for Uptown copied for Cedar Heights where it has none: the same values at the
        /// start, so the old demand for every product there is kept. How many rows were made.
        /// </summary>
        public static int MoveRows(GameInstance save, out int demand, out int filters)
        {
            demand = filters = 0;
            if (save == null)
            {
                return 0;
            }
            string from = DistrictSplit.UptownId, to = DistrictSplit.CedarId;
            for (int i = 0; save.productMarketEntries != null && i < save.productMarketEntries.Count; i++)
            {
                List<NeighborhoodDemand> rows = save.productMarketEntries[i] != null ? save.productMarketEntries[i].demandValues : null;
                if (rows == null)
                {
                    continue;
                }
                NeighborhoodDemand source = null;
                bool has = false;
                for (int k = 0; k < rows.Count; k++)
                {
                    if (rows[k] == null)
                    {
                        continue;
                    }
                    source = rows[k].neighborhood == from ? rows[k] : source;
                    has |= rows[k].neighborhood == to;
                }
                if (source != null && !has)
                {
                    rows.Add(new NeighborhoodDemand
                    {
                        neighborhood = to, demand = source.demand, providers = source.providers, lastDaySold = source.lastDaySold,
                        lastDayProvidersExceeded = source.lastDayProvidersExceeded, hasPlayerMonopoly = source.hasPlayerMonopoly
                    });
                    demand++;
                }
            }
            if (save.SelectedCitymapFilters != null && save.SelectedCitymapFilters.Contains(from) && !save.SelectedCitymapFilters.Contains(to))
            {
                save.SelectedCitymapFilters.Add(to);
                filters++;
            }
            return demand + filters;
        }

        /// <summary>The customers a building has room for as the type it is (BuildingSizeData's customerCapacities), 0 for a type with none or when the size cannot be read.</summary>
        public static int CapacityOf(Building b)
        {
            try
            {
                return b != null ? b.GetCustomerCapacity : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// The game's old districts counted now from its own buildings (the city's list through GameDlc.Open, so nothing a DLC keeps),
        /// each with the game's weight and classes for it, into the caller's list (cleared); their area is Build 3682's zones. False
        /// when the city's buildings cannot be read, and the list holds the districts as Build 3682 has them (DistrictCensus.Old).
        /// </summary>
        public static bool OldCensus(List<CensusRow> into)
        {
            into.Clear();
            CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
            CityBuildingController[] all = city != null ? GameDlc.Open(city) : null;
            var units = new List<CensusUnit>[DistrictCensus.Old.Length];
            for (int i = 0; i < units.Length; i++)
            {
                units[i] = new List<CensusUnit>(128);
            }
            for (int i = 0; all != null && i < all.Length; i++)
            {
                Building b = all[i] != null ? all[i].building : null;
                if (b == null)
                {
                    continue;
                }
                for (int k = 0; k < units.Length; k++)
                {
                    if (DistrictCensus.Old[k].Id == b.Neighbourhood)
                    {
                        units[k].Add(new CensusUnit { Type = b.SpecialService != null ? "special" : b.BuildingType, Capacity = CapacityOf(b) });
                    }
                }
            }
            bool live = all != null && all.Length > 0;
            for (int k = 0; k < units.Length; k++)
            {
                DistrictCensus.OldDistrict o = DistrictCensus.Old[k];
                if (!live || units[k].Count == 0)
                {
                    into.Add(Written(o));
                    live = false;
                    continue;
                }
                NeighborhoodData data = null;
                try
                {
                    data = NeighborhoodHelper.GetData(o.Id);
                }
                catch (Exception)
                {
                    data = null;
                }
                var figures = new DistrictFigures
                {
                    Working = data != null ? data.workingClassPercentage : o.Working, Middle = data != null ? data.middleClassPercentage : o.Middle,
                    Upper = data != null ? data.upperClassClassPercentage : o.Upper, Demand = data != null ? data.customerDemandsWeight : o.Demand
                };
                into.Add(DistrictCensus.Count(o.Id.Replace("ba:neighborhood_", string.Empty), o.Area, figures, units[k]));
            }
            if (!live)
            {
                into.Clear();
                for (int k = 0; k < DistrictCensus.Old.Length; k++)
                {
                    into.Add(Written(DistrictCensus.Old[k]));
                }
            }
            return live;
        }

        // An old district as Build 3682 has it, as a row.
        private static CensusRow Written(DistrictCensus.OldDistrict o)
        {
            var row = new CensusRow
            {
                Name = o.Id.Replace("ba:neighborhood_", string.Empty), Area = o.Area, Buildings = o.Buildings, Homes = o.Homes, Shops = o.Shops, Offices = o.Offices,
                Warehouses = o.Warehouses, Large = o.Large, ShopCapacity = o.Capacity, Working = o.Working, Middle = o.Middle, Upper = o.Upper, Demand = o.Demand
            };
            for (int i = 0; i < o.ByTier.Length; i++)
            {
                row.ByTier[i] = o.ByTier[i];
            }
            return row;
        }
    }
}
