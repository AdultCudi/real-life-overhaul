using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// City Utilities' building (Core/CityReserves says which and why: 13 7th Street): that address when it may serve,
    /// else, meanwhile, the smallest other residential building of Midtown that may. Never one of the game's own buildings
    /// (no special service, never an office or a shop), never one a player rents or owns, never one another feature holds
    /// or reserves. The same city gives the same building on every machine. One walk over the city's list at a choice (once
    /// a city load, and when a move is due); a few reads every five seconds while it stands meanwhile.
    /// </summary>
    internal static class CivicHost
    {
        /// <summary>The building, at 13 7th Street or meanwhile; null if none in the city may serve.</summary>
        public static CityBuildingController Pick(CityBuildingController[] buildings, out bool fixedOne)
        {
            fixedOne = false;
            if (buildings == null)
            {
                return null;
            }
            CityBuildingController home = Find(buildings);
            if (Serves(home))
            {
                fixedOne = true;
                return home;
            }
            int pick = CityReserves.PickSmallest(buildings.Length,
                i => buildings[i] != null && buildings[i].building != null && buildings[i].building.Neighbourhood == CityReserves.UtilitiesDistrict && Serves(buildings[i]),
                i => buildings[i].building.totalSqm, i => buildings[i].building.StreetName, i => buildings[i].building.StreetNumber,
                (s, n) => SpecialBuildings.Holds(s, n) || CityReserves.IsUtilities(s, n));   // (every held or reserved one)
            return pick >= 0 ? buildings[pick] : null;
        }

        /// <summary>True if 13 7th Street may serve now (an office standing meanwhile moves back once it does).</summary>
        public static bool FixedServes(CityBuildingController[] buildings)
        {
            return buildings != null && Serves(Find(buildings));
        }

        /// <summary>True for 13 7th Street.</summary>
        public static bool IsFixed(CityBuildingController b)
        {
            return b != null && b.building != null && CityReserves.IsUtilities(b.building.StreetName, b.building.StreetNumber);
        }

        /// <summary>
        /// An ordinary residential building with a door the player can walk to, no special service of the game's, nobody's,
        /// and nothing of another feature's in it or reserved for another (CityReserves.HostServes).
        /// </summary>
        public static bool Serves(CityBuildingController b)
        {
            if (b == null || b.building == null || b.entranceDoors == null || b.entranceDoors.Length == 0 || b.entranceDoors[0] == null || GameMembers.Fenced(b.building)
                || (int)b.building.requiredDLC != 0 || b.building.IsHamptonsHouse())
            {
                return false;
            }
            string filed = SpecialBuildings.FiledAt(b);
            string reserved = SpecialBuildings.ReservedFor(b.building.StreetName, b.building.StreetNumber);
            bool other = (filed != null && filed != CityReserves.UtilitiesId) || (reserved != null && reserved != CityReserves.UtilitiesId);
            return CityReserves.HostServes(b.building.BuildingType, b.building.SpecialService != null, SpecialBuildings.Players(b), other);
        }

        private static CityBuildingController Find(CityBuildingController[] buildings)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i] != null && buildings[i].building != null && CityReserves.IsUtilities(buildings[i].building.StreetName, buildings[i].building.StreetNumber))
                {
                    return buildings[i];
                }
            }
            return null;
        }
    }
}
