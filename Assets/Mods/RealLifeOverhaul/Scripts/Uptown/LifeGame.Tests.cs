using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.Neighborhoods;
using BigAmbitions.Tags;
using Buildings;
using Entities;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using UI.Guiders;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>What Uptown's tests read of the game and ask of it: counts in the save, the game's own daily pass for Uptown alone, a unit to rent. Nothing here runs a frame.</summary>
    internal static partial class LifeGame
    {
        private const string Empty = "ba:businesstype_empty";   // the game's business type of a unit with no business in it

        /// <summary>How many of the buildings' registrations are in the save's list (as the game's own are, while it runs).</summary>
        public static int InSaveList(List<MadeBuilding> buildings)
        {
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            int found = 0;
            for (int i = 0; all != null && i < buildings.Count; i++)
            {
                found += buildings[i] != null && buildings[i].Registration != null && all.Contains(buildings[i].Registration) ? 1 : 0;
            }
            return found;
        }

        /// <summary>The district's units with no business in them and nobody renting them: what the game's daily pass may start one in.</summary>
        public static int EmptyUnits(List<MadeBuilding> buildings)
        {
            int empty = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingRegistration reg = buildings[i] != null ? buildings[i].Registration : null;
                empty += reg != null && !reg.RentedByPlayer && reg.businessTypeName == Empty ? 1 : 0;
            }
            return empty;
        }

        /// <summary>The district's buildings with a business of the game's in them (not the player's, not another player's).</summary>
        public static int Businesses(List<MadeBuilding> buildings)
        {
            int open = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingRegistration reg = buildings[i] != null ? buildings[i].Registration : null;
                open += reg != null && !reg.RentedByPlayer && reg.businessTypeName != Empty && !string.IsNullOrEmpty(reg.businessTypeName) ? 1 : 0;
            }
            return open;
        }

        /// <summary>
        /// The game's own start of a new business in a district (CompetitionHelper.StartNewBusiness, the step of its daily
        /// pass), run for one of the band's districts alone over its buildings the game lists businesses in, as RunDaily calls it.
        /// The game's next day for one; -1 with why when it cannot be asked.
        /// </summary>
        public static int StartBusiness(int district, out string why)
        {
            string id = DistrictSplit.IdOf(district);
            why = null;
            MethodInfo start = GameContract.StartBusiness.Method;
            if (start == null)
            {
                why = "the game's start of a business could not be reached (" + (GameContract.UptownDistrict.Why ?? "a game update?") + ")";
                return -1;
            }
            if (!BuildingHelper.AllNeighbourhoodBuildings.TryGetValue(id, out List<Building> all) || all == null || all.Count == 0)
            {
                why = "the district has no building in the game's lists";
                return -1;
            }
            var listed = new List<Building>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                BuildingTypeData data = BuildingTypeHelper.GetData(all[i]);
                if (data != null && data.HasTag(TagRef.Buildingtypetag.showinbusinesslist))
                {
                    listed.Add(all[i]);   // as RunDaily makes its list of a district's business buildings
                }
            }
            return (int)start.Invoke(null, new object[] { listed, id });
        }

        /// <summary>Each building's business type, by address, into the caller's dictionary (cleared): the business test compares before and after.</summary>
        public static void BusinessesBy(List<MadeBuilding> buildings, Dictionary<string, string> into)
        {
            into.Clear();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingRegistration reg = buildings[i] != null ? buildings[i].Registration : null;
                if (reg != null)
                {
                    into[buildings[i].Address ?? reg.StreetName + " " + reg.StreetNumber] = reg.businessTypeName ?? string.Empty;
                }
            }
        }

        /// <summary>The district's building nearest the player that is to let and empty, or null.</summary>
        public static MadeBuilding ToLet(List<MadeBuilding> buildings)
        {
            UnityEngine.Vector3 at = MadeGame.PlayerAt();
            MadeBuilding best = null;
            float near = float.MaxValue;
            for (int i = 0; i < buildings.Count; i++)
            {
                MadeBuilding made = buildings[i];
                BuildingRegistration reg = made != null ? made.Registration : null;
                if (reg == null || !reg.AvailableForRent || reg.RentedByPlayer || reg.businessTypeName != Empty || GoingPublic.Foreign(reg))
                {
                    continue;
                }
                float dx = made.DoorX - at.x, dz = made.DoorZ - at.z;
                if (dx * dx + dz * dz < near)
                {
                    near = dx * dx + dz * dz;
                    best = made;
                }
            }
            return best;
        }

        /// <summary>The game's own destination set at a building, as the map's "set destination" sets it.</summary>
        public static void Destination(MadeBuilding made)
        {
            GuidersManager.SetGuiderTarget(made.Building.Address, DirectionGuiderType.Destination);
        }

        /// <summary>The player's daily rent of a unit they rent, as the game keeps it.</summary>
        public static float RentOf(MadeBuilding made)
        {
            return made != null && made.Registration != null ? made.Registration.RentPerDay : 0f;
        }

        /// <summary>
        /// What of Uptown a save holds in the game's own part (a save read back from its file, or the one in memory): its
        /// registrations, its stats, its products' demand rows, its row among the map's ticked filters, the player's address
        /// or destination there, its records in the mod's section (that save's own line of the district save).
        /// </summary>
        public static void Holds(GameInstance save, out int registrations, out int stats, out int demand, out int rows, out bool player, out int kept)
        {
            registrations = stats = demand = rows = kept = 0;
            player = false;
            if (save == null)
            {
                return;
            }
            for (int i = 0; save.BuildingRegistrations != null && i < save.BuildingRegistrations.Count; i++)
            {
                registrations += save.BuildingRegistrations[i] != null && DistrictSave.DistrictSaveRules.IsModId(save.BuildingRegistrations[i].StreetName) ? 1 : 0;
            }
            for (int i = 0; save.NeighbourhoodStats != null && i < save.NeighbourhoodStats.Count; i++)
            {
                stats += save.NeighbourhoodStats[i] != null && DistrictSplit.Is(save.NeighbourhoodStats[i].name) ? 1 : 0;
            }
            for (int i = 0; save.productMarketEntries != null && i < save.productMarketEntries.Count; i++)
            {
                List<NeighborhoodDemand> rowsOf = save.productMarketEntries[i] != null ? save.productMarketEntries[i].demandValues : null;
                for (int k = 0; rowsOf != null && k < rowsOf.Count; k++)
                {
                    demand += rowsOf[k] != null && DistrictSplit.Is(rowsOf[k].neighborhood) ? 1 : 0;
                }
            }
            for (int i = 0; save.SelectedCitymapFilters != null && i < save.SelectedCitymapFilters.Count; i++)
            {
                rows += DistrictSplit.Is(save.SelectedCitymapFilters[i]) ? 1 : 0;
            }
            player = DistrictSave.DistrictSaveRules.IsModId(save.CurrentStreetName)
                     || (save.customDestination != null && DistrictSave.DistrictSaveRules.IsModId(save.customDestination.streetName));
            string line = save.modData != null && save.modData.TryGetValue(DistrictSave.DistrictSaveStore.Key, out string text) ? text : null;
            DistrictSave.StoredLine read = DistrictSave.DistrictSaveStore.Read(line, out int _);
            kept = read != null ? read.Records.Count : 0;
        }
    }
}
