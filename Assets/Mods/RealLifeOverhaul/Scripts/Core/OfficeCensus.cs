using System.Diagnostics;
using BAModAPI;
using Streets;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city's offices, shops and warehouses counted once a city load, after the features have chosen theirs: how
    /// each stands (<see cref="OfficeTally"/>) and how many are left for the player, written as three log lines, then the
    /// check on the player's two offices. Only buildings a player may have are counted. Nothing is changed or kept. The count
    /// goes a slice a frame (<see cref="OfficeCount"/>, a building a step), and each line has a frame of its own: whole in
    /// one tick it was 8.5 ms in the owner's log of 2 Nov 2026.
    /// COST: while it runs (once a load), the slice the caller gives a frame, then a line a frame; nothing after.
    /// </summary>
    internal sealed class OfficeCensus
    {
        private const string Office = "ba:buildingtype_office";
        private const string Retail = "ba:buildingtype_retail";
        private const string Warehouse = "ba:buildingtype_warehouse";

        private readonly OfficeCount _count = new OfficeCount();
        private CityBuildingController[] _buildings;
        private OfficeRead _read;   // ReadOne, made once
        private int _line;          // the next line to write once all is counted: the three tallies, then the check

        /// <summary>A count begun over the city's buildings as they stand now; false when the city has none.</summary>
        public bool Begin()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            _buildings = GameDlc.Open(city);
            if (_buildings == null)
            {
                return false;
            }
            _read = _read ?? ReadOne;
            _count.Begin(_buildings.Length, _read);
            _line = 0;
            return true;
        }

        /// <summary>
        /// Some of the count while the slice allows, a building an item (each timed into <paramref name="watch"/>); once all are
        /// counted, a line a call. True once every line is written.
        /// </summary>
        public bool Step(ref WorkSlice slice, SpreadWatch watch, IModLogger logger)
        {
            if (_buildings == null)
            {
                return true;
            }
            if (_count.Running)
            {
                while (slice.More())
                {
                    long item = Stopwatch.GetTimestamp();
                    bool more = _count.Step();
                    watch.Item(0, item);
                    if (!more)
                    {
                        break;
                    }
                }
                return false;   // the lines from the next frame on
            }
            switch (_line++)
            {
                case 0:
                    logger?.Info(_count.Of(0).Line("office"));
                    return false;
                case 1:
                    logger?.Info(_count.Of(1).Line("retail"));
                    return false;
                case 2:
                    logger?.Info(_count.Of(2).Line("warehouse"));
                    return false;
                default:
                    if (logger != null)
                    {
                        KeptCheck(_buildings, logger);
                    }
                    _buildings = null;
                    return true;
            }
        }

        // One building of the list, as the whole pass read it: counted if a player may have it and it is an office, a shop or a warehouse.
        private bool ReadOne(int i, out int kind, out string district, out LetState state, out string holder)
        {
            kind = -1;
            district = null;
            state = LetState.Other;
            holder = null;
            CityBuildingController b = _buildings[i];
            if (b == null || b.building == null || b.building.SpecialService != null || GameMembers.Fenced(b.building) || GameDlc.Gated(b)
                || b.building.IsHamptonsHouse() || b.entranceDoors == null || b.entranceDoors.Length == 0)
            {
                return false;
            }
            string type = b.building.BuildingType;
            kind = type == Office ? 0 : type == Retail ? 1 : type == Warehouse ? 2 : -1;
            BuildingRegistration reg = b.buildingRegistration;
            if (kind < 0 || reg == null)
            {
                return false;
            }
            state = OfficeTally.Classify(reg.RentedByPlayer, reg.BuildingOwnedByPlayer, reg.businessOwnerRivalId, reg.businessTypeName, reg.AvailableForRent,
                reg.Layout != null, reg.itemInstances != null ? reg.itemInstances.Count : 0);
            holder = SpecialBuildings.TakenBy(reg) != null ? SpecialBuildings.HolderOf(b) : null;   // held and refused now: its feature is on
            district = b.building.Neighbourhood;
            return true;
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
