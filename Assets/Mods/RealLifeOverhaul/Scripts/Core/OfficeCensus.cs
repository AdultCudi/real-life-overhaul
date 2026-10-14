using BAModAPI;

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
        }
    }
}
