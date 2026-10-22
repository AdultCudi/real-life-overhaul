namespace RealLifeOverhaul
{
    /// <summary>
    /// City Utilities' building: 13 7th Street when it may serve (<see cref="CityReserves"/>), else the smallest other
    /// residential building of Midtown that may; the same on every machine. COST: one walk over the city's buildings
    /// per choice.
    /// </summary>
    internal static class CivicHost
    {
        /// <summary>The building; null if none in the city may serve.</summary>
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
                (s, n) => SpecialBuildings.Holds(s, n) || CityReserves.IsUtilities(s, n));
            return pick >= 0 ? buildings[pick] : null;
        }

        /// <summary>True if 13 7th Street may serve now.</summary>
        public static bool FixedServes(CityBuildingController[] buildings)
        {
            return buildings != null && Serves(Find(buildings));
        }

        public static bool IsFixed(CityBuildingController b)
        {
            return b != null && b.building != null && CityReserves.IsUtilities(b.building.StreetName, b.building.StreetNumber);
        }

        /// <summary>True for a building with a door the player can walk to that <see cref="CityReserves.HostServes"/> allows.</summary>
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
