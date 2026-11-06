using BAModAPI;
using Streets;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's offices, shops and warehouses counted once a city load, after the features have chosen theirs: how
    /// each stands (<see cref="OfficeTally"/>) and how many are left for the player, written as three log lines. Only
    /// buildings a player may have are counted. Nothing is changed or kept; one pass over the city's buildings.
    /// </summary>
    internal static class OfficeCensus
    {
        private const string Office = "ba:buildingtype_office";
        private const string Retail = "ba:buildingtype_retail";
        private const string Warehouse = "ba:buildingtype_warehouse";

        public static void Log(IModLogger logger)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = GameDlc.Open(city);
            if (buildings == null || logger == null)
            {
                return;
            }
            var offices = new OfficeTally();
            var shops = new OfficeTally();
            var warehouses = new OfficeTally();
            for (int i = 0; i < buildings.Length; i++)
            {
                CityBuildingController b = buildings[i];
                if (b == null || b.building == null || b.building.SpecialService != null || GameMembers.Fenced(b.building) || GameDlc.Gated(b)
                    || b.building.IsHamptonsHouse() || b.entranceDoors == null || b.entranceDoors.Length == 0)
                {
                    continue;
                }
                string type = b.building.BuildingType;
                OfficeTally into = type == Office ? offices : type == Retail ? shops : type == Warehouse ? warehouses : null;
                BuildingRegistration reg = b.buildingRegistration;
                if (into == null || reg == null)
                {
                    continue;
                }
                LetState state = OfficeTally.Classify(reg.RentedByPlayer, reg.BuildingOwnedByPlayer, reg.businessOwnerRivalId, reg.businessTypeName, reg.AvailableForRent,
                    reg.Layout != null, reg.itemInstances != null ? reg.itemInstances.Count : 0);
                string holder = SpecialBuildings.TakenBy(reg) != null ? SpecialBuildings.HolderOf(b) : null;   // held and refused now: its feature is on
                into.Add(b.building.Neighbourhood, state, holder);
            }
            logger.Info(offices.Line("office"));
            logger.Info(shops.Line("retail"));
            logger.Info(warehouses.Line("warehouse"));
            KeptCheck(buildings, logger);
        }

        // The two largest offices are still the two CityReserves keeps for the player, and no feature holds them.
        private static void KeptCheck(CityBuildingController[] buildings, IModLogger logger)
        {
            CityReserves.TopTwo(buildings.Length,
                i => buildings[i] != null && buildings[i].building != null && buildings[i].building.BuildingType == Office && buildings[i].building.SpecialService == null,
                i => buildings[i].building.totalSqm, i => buildings[i].building.StreetName, i => buildings[i].building.StreetNumber, out int first, out int second);
            bool same = first >= 0 && second >= 0
                        && CityReserves.IsPlayerOffice(buildings[first].building.StreetName, buildings[first].building.StreetNumber)
                        && CityReserves.IsPlayerOffice(buildings[second].building.StreetName, buildings[second].building.StreetNumber);
            string held = null;
            for (int k = 0; k < 2; k++)
            {
                int i = k == 0 ? first : second;
                string filed = i >= 0 ? SpecialBuildings.FiledAt(buildings[i]) : null;
                held = filed != null ? filed : held;
            }
            string line = "Office supply: the player's two offices check (the two largest office buildings, "
                          + (first >= 0 ? buildings[first].building.Address.ToFormattedString() : "none") + " and " + (second >= 0 ? buildings[second].building.Address.ToFormattedString() : "none")
                          + ", reserved for the player: no feature of the mod takes them): "
                          + (same && held == null ? "PASS" : !same ? "FAIL (the game's city has changed: they are not the two kept)" : "FAIL (" + held + " is in one)") + ".";
            if (same && held == null)
            {
                logger.Info(line);
            }
            else
            {
                logger.Warn(line);
            }
        }
    }
}
