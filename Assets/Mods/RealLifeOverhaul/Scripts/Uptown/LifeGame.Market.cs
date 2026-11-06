using System;
using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using Buildings;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The game's side of a district's market (LifeDirector.Market): its standing buildings counted by use from their
    /// registrations, and the market level written into the district's own record of the game's, which the game's rent and price
    /// arithmetic reads (Building.GetMarketPricePerSqm, its rent a square metre). Each of the band's two districts has a market of
    /// its own. Nothing here runs a frame: once a game day.
    /// </summary>
    internal static partial class LifeGame
    {
        private const string EmptyType = "ba:businesstype_empty";

        /// <summary>The game's day now, or -1 with no save.</summary>
        public static int Today => SaveGameManager.Current != null ? SaveGameManager.Current.Day : -1;

        /// <summary>
        /// A district's units of each use, its vacant ones (for rent, nobody in), those of them in a building finished within
        /// DemandRules.FillingDays (still being let), its average traffic, and the share of each use's units that reads as normal:
        /// the larger of the game's own ideal share kept free to let there (its third for homes, shops and offices, the mix's)
        /// and the share the city's own districts stand empty at now (the city's warehouses stand nearly half empty).
        /// </summary>
        public static DistrictMarket Market(IList<MadeBuilding> standing, LifeState state, int today, int district)
        {
            var m = new DistrictMarket { Units = new int[DemandRules.Uses], Vacant = new int[DemandRules.Uses], Filling = new int[DemandRules.Uses], Normal = new int[DemandRules.Uses] };
            long traffic = 0;
            int counted = 0;
            for (int i = 0; standing != null && i < standing.Count; i++)
            {
                MadeBuilding made = standing[i];
                if (made == null || made.Building == null || DistrictSplit.Index(DistrictOfRecord(made)) != district)
                {
                    continue;
                }
                int use = DemandRules.UseOfType(made.Building.BuildingType);
                if (use < 0)
                {
                    continue;
                }
                m.Units[use]++;
                if (ToLet(made.Registration))
                {
                    m.Vacant[use]++;
                    FinishedBuilding f = state != null && made.Spec != null ? state.On(made.Spec.Id) : null;
                    if (f != null && today >= 0 && today - f.Day < DemandRules.FillingDays)
                    {
                        m.Filling[use]++;
                    }
                }
                traffic += (long)made.Building.trafficIndex;
                counted++;
            }
            m.Traffic = counted > 0 ? (int)(traffic / counted) : 50;
            Normal(m.Normal, district);
            return m;
        }

        // For rent, nobody in: as the game lets a unit (AvailableForRent, the empty business).
        private static bool ToLet(BuildingRegistration reg)
        {
            return reg != null && reg.AvailableForRent && !reg.RentedByPlayer && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == EmptyType);
        }

        // Each use's normal share free to let, thousandths: the game's ideal for the district (else its city-wide one) or the city's own districts' now, the larger.
        private static void Normal(int[] into, int district)
        {
            var units = new int[DemandRules.Uses];
            var vacant = new int[DemandRules.Uses];
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                Building b = c != null ? c.building : null;
                if (b == null || b.SpecialService != null || DistrictSplit.Is(b.Neighbourhood) || b.IsHamptonsHouse())
                {
                    continue;
                }
                int use = DemandRules.UseOfType(b.BuildingType);
                if (use < 0)
                {
                    continue;
                }
                units[use]++;
                if (ToLet(c.buildingRegistration))
                {
                    vacant[use]++;
                }
            }
            for (int use = 0; use < DemandRules.Uses; use++)
            {
                int city = units[use] > 0 ? (int)(1000L * vacant[use] / units[use]) : -1;
                into[use] = Math.Max(Ideal(DemandRules.TypeOfUse(use), district), city);
            }
        }

        // The game's ideal share of a type kept free to let in a district (else its city-wide one), thousandths; -1 for none.
        private static int Ideal(string type, int district)
        {
            BuildingTypeData data = string.IsNullOrEmpty(type) ? null : BuildingTypeHelper.GetData(type);
            IdealAvailableBuildingsInNeighborhood[] ideal = data != null ? data.idealAvailableBuildingsInNeighborhood : null;
            int global = -1;
            for (int i = 0; ideal != null && i < ideal.Length; i++)
            {
                if (ideal[i] == null)
                {
                    continue;
                }
                if (ideal[i].neighbourhood == DistrictSplit.IdOf(district))
                {
                    return ideal[i].idealAvailableBuildingsPercentage * 10;
                }
                if (ideal[i].neighbourhood == GlobalNeighbourhood)
                {
                    global = ideal[i].idealAvailableBuildingsPercentage * 10;
                }
            }
            return global;
        }

        private const string GlobalNeighbourhood = "ba:neighborhood_global";   // the game's entry for every district without its own (CompetitionHelper)

        /// <summary>
        /// The market level (thousandths) written into a district's record: its rent a square metre for a dollar of price, and its
        /// buildings' value, from the district's own figures.
        /// </summary>
        public static void ApplyLevel(int district, int level)
        {
            NeighborhoodData record = Record(district);
            if (record == null)
            {
                return;
            }
            DistrictFigures f = Figures(district);
            record.rentMultiplierPerSqmPrice = f.RentPerPrice * level / 1000f;
            record.realEstateMultiplier = f.RealEstate * level / 1000f;
        }
    }
}
