using BAModAPI;
using Streets;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's office buildings counted once a city load, when the mod's features have chosen theirs (Tech/Sites, after
    /// the towers): standing empty and to let, a business of the game's, a rival's, the player's (or another player's in a
    /// Going Public session), and held by the mod, by feature; how many stood empty before the mod held any, and how many
    /// are left for the player, in all and by district (Core/OfficeSupply). The shops and the warehouses are counted the
    /// same way, the other buildings an office business may be started in. Only the city's own buildings that may be had
    /// are counted: no special service, none behind a private fence, none that needs a DLC, no villa of the Hamptons.
    ///
    /// Nothing is changed and nothing is kept: one look at the city's buildings (a few hundred registrations read), and
    /// three lines in the log.
    /// </summary>
    internal static class OfficeCensus
    {
        private const string Office = "ba:buildingtype_office";
        private const string Retail = "ba:buildingtype_retail";
        private const string Warehouse = "ba:buildingtype_warehouse";

        public static void Log(IModLogger logger)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
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
                if (b == null || b.building == null || b.building.SpecialService != null || GameMembers.Fenced(b.building) || (int)b.building.requiredDLC != 0
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
                string holder = SpecialBuildings.TakenBy(reg) != null ? SpecialBuildings.HolderOf(b) : null;   // (held and refused now: its feature is on)
                into.Add(b.building.Neighbourhood, state, holder);
            }
            logger.Info(offices.Line("office"));
            logger.Info(shops.Line("retail"));
            logger.Info(warehouses.Line("warehouse"));
            KeptCheck(buildings, logger);
        }

        // The two largest office buildings a player can rent or buy are still the two the mod keeps for the player
        // (Core/CityReserves), and none of the mod's features holds them: one line, a warning if the game's city changed.
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
