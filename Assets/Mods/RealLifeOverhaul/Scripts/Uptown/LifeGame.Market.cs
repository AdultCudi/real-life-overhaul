using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The game's side of the district's market (LifeDirector.Market): its standing buildings counted by use from their
    /// registrations, and the market level written into Uptown's own record of the game's, which the game's rent and price
    /// arithmetic reads (Building.GetMarketPricePerSqm, its rent a square metre). Nothing here runs a frame: once a game day.
    /// </summary>
    internal static partial class LifeGame
    {
        private const string EmptyType = "ba:businesstype_empty";

        /// <summary>The game's day now, or -1 with no save.</summary>
        public static int Today => SaveGameManager.Current != null ? SaveGameManager.Current.Day : -1;

        /// <summary>The district's units of each use, its vacant ones (for rent, nobody in) and its average traffic.</summary>
        public static DistrictMarket Market(IList<MadeBuilding> standing)
        {
            var m = new DistrictMarket { Units = new int[DemandRules.Uses], Vacant = new int[DemandRules.Uses] };
            long traffic = 0;
            int counted = 0;
            for (int i = 0; standing != null && i < standing.Count; i++)
            {
                MadeBuilding made = standing[i];
                if (made == null || made.Building == null)
                {
                    continue;
                }
                int use = DemandRules.UseOfType(made.Building.BuildingType);
                if (use < 0)
                {
                    continue;
                }
                m.Units[use]++;
                BuildingRegistration reg = made.Registration;
                if (reg != null && reg.AvailableForRent && !reg.RentedByPlayer && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == EmptyType))
                {
                    m.Vacant[use]++;
                }
                traffic += (long)made.Building.trafficIndex;
                counted++;
            }
            m.Traffic = counted > 0 ? (int)(traffic / counted) : 50;
            return m;
        }

        /// <summary>The market level (thousandths) written into Uptown's record: its rent a square metre for a dollar of price, and its buildings' value, from the district's own figures.</summary>
        public static void ApplyLevel(int level)
        {
            if (_record == null)
            {
                return;
            }
            DistrictFigures f = Figures;
            _record.rentMultiplierPerSqmPrice = f.RentPerPrice * level / 1000f;
            _record.realEstateMultiplier = f.RealEstate * level / 1000f;
        }
    }
}
