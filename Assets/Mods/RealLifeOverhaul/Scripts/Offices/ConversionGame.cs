using System;
using System.Collections.Generic;
using Blueprints;
using Buildings;
using Helpers;
using Streets;
using UI;

namespace RealLifeOverhaul.Offices
{
    /// <summary>
    /// Everything the conversion reads from or does to the game. A building's type is a field of the building's definition,
    /// never saved, that everything asks (the businesses that may start there, the map, the rent, the customers, the
    /// competition), while the interior goes by size and version alone; a shop whose size and version the game lists for
    /// offices too stands on an office's structure. The game keeps two copies of each definition: the city scene's (the
    /// CityBuildingController's, which the map card, the filters and the marker read) and the Addressables list's
    /// (BuildingHelper.GetBuilding, which BizMan, the registration and the businesses that may start read). Written in both, and
    /// put back in both on every way out, even after the scene's is gone: the shops' BuildingType. What
    /// keeps the game's own letting off a converted shop is the held addresses' (Core/HeldUnits), as for every building
    /// the mod holds.
    /// </summary>
    internal static class ConversionGame
    {
        public const string Office = "ba:buildingtype_office";
        public const string Retail = "ba:buildingtype_retail";
        private const string Empty = "ba:businesstype_empty";

        /// <summary>A building the player can walk into that is the city's own: no special service, fence or DLC, and a door.</summary>
        public static bool Usable(CityBuildingController c)
        {
            Building b = c != null ? c.building : null;
            return b != null && b.SpecialService == null && !GameMembers.Fenced(b) && !GameDlc.Gated(b) && !b.IsHamptonsHouse()
                   && c.entranceDoors != null && c.entranceDoors.Length > 0 && b.totalSqm > 0 && !string.IsNullOrEmpty(b.StreetName);
        }

        /// <summary>A shop whose size and version the game's own data lets be an office too.</summary>
        public static bool SharedStructure(Building b, List<BuildingSizeInfo> officeSizes)
        {
            for (int i = 0; officeSizes != null && i < officeSizes.Count; i++)
            {
                BuildingSizeInfo s = officeSizes[i];
                if (s != null && s.buildingVersion == b.BuildingVersion && string.Equals(s.buildingSize, b.BuildingSize, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The sizes and versions the game's data lists for offices; the game's own list, not a copy.</summary>
        public static List<BuildingSizeInfo> OfficeSizes() => BuildingSizeHelper.GetBuildingVersionsByBuildingType(Office);

        /// <summary>Standing empty: nobody rents or owns it (the player, another player, a rival) and nothing of the game's is in it.</summary>
        public static bool Vacant(BuildingRegistration reg)
        {
            return reg != null && reg.AvailableForRent && !reg.RentedByPlayer && !reg.BuildingOwnedByPlayer && string.IsNullOrEmpty(reg.businessOwnerRivalId)
                   && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == Empty) && reg.Layout == null
                   && (reg.itemInstances == null || reg.itemInstances.Count == 0);
        }

        /// <summary>A business of a type the game puts in offices (the mod's tech firms' included).</summary>
        public static bool OfficeBusiness(BuildingRegistration reg)
        {
            if (reg == null || string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == Empty)
            {
                return false;
            }
            BusinessType type = BusinessTypeHelper.GetData(reg.businessTypeName);
            return type != null && type.suitableBuildingType == Office;
        }

        /// <summary>The player's to keep through a save: rented by the player (not another player's), owned, or listed for sale.</summary>
        public static bool Keeps(BuildingRegistration reg, HashSet<Address> forSale)
        {
            return reg != null && (GoingPublic.Mine(reg) || reg.BuildingOwnedByPlayer || (forSale != null && reg.Address != null && forSale.Contains(reg.Address)));
        }

        /// <summary>True if the building is listed for sale now (the game's list, GameInstance.buildingsForSale): one pass, no closure made.</summary>
        public static bool OnSale(GameInstance save, Address address)
        {
            List<BuildingForSale> all = save != null ? save.buildingsForSale : null;
            for (int i = 0; address != null && all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].address == address)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The buildings listed for sale now (the game's list, GameInstance.buildingsForSale), into a set.</summary>
        public static void ForSale(GameInstance save, HashSet<Address> into)
        {
            into.Clear();
            List<BuildingForSale> all = save != null ? save.buildingsForSale : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].address != null)
                {
                    into.Add(all[i].address);
                }
            }
        }

        /// <summary>
        /// The building as the game's own list holds it (BuildingHelper.GetBuilding, the Addressables copy every lookup by address
        /// reads: BizMan, the registration's BuildingCached, the businesses that may start, the rent, the customers, the competition);
        /// null when it is the very object the city scene holds, or the list has none. The city scene's CityBuildingController.building
        /// is another copy of the same definition (the scene's own assets, 884 of them, beside the 885 of the Addressables group), so a
        /// type written to one alone makes the map card say one thing and BizMan another.
        /// </summary>
        public static Building GameCopy(Building scene)
        {
            if (scene == null || scene.Address == null)
            {
                return null;
            }
            Building game = BuildingHelper.GetBuilding(scene.Address);
            return game != null && !ReferenceEquals(game, scene) ? game : null;
        }

        /// <summary>
        /// A building's type set in every copy the game keeps (the city scene's and the game's list), and its marker drawn again (not
        /// while the city is going). Either copy may be gone: the scene's with the scene, which is why <paramref name="game"/> is kept
        /// by the caller and put back on its own.
        /// </summary>
        public static void SetType(Building scene, Building game, CityBuildingController c, string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return;
            }
            if (game != null)
            {
                game.BuildingType = type;
            }
            if (scene != null)
            {
                scene.BuildingType = type;
            }
            if (scene != null && !GameManager.isCitySceneBeingUnloaded && c != null && c.buildingRegistration != null)
            {
                c.UpdatePoi();
            }
        }

        /// <summary>
        /// Null when a building's type reads <paramref name="type"/> in each copy the game keeps and in what the registration reads by
        /// address; otherwise what differs (<see cref="ConversionRules.Disagreement"/>).
        /// </summary>
        public static string Disagreement(Building scene, Building game, string type)
        {
            string byAddress = scene != null && scene.Address != null ? BuildingHelper.GetBuilding(scene.Address)?.BuildingType : null;
            return ConversionRules.Disagreement(type, scene != null ? scene.BuildingType : null, game != null ? game.BuildingType : byAddress);
        }

        /// <summary>
        /// Null when every business the game offers to start in a building of this type is one the game made for the type; otherwise
        /// the first that is not, with its own type. The list is the game's (BuildingTypeData.availableBusinessTypes, with the mod's
        /// business types registered by their suitable building type), read once a check, never a frame.
        /// </summary>
        public static string WrongOffer(string buildingType)
        {
            BuildingTypeData data = BuildingTypeHelper.GetData(buildingType);
            return data == null ? "no building type " + buildingType : ConversionRules.WrongOffer(buildingType, data.availableBusinessTypes, SuitableOf);
        }

        /// <summary>
        /// <see cref="WrongOffer"/> for every building type the city's buildings have (the game's list, BuildingHelper.allBuildings), each
        /// type once: null when none offers a business made for another type. <paramref name="types"/> is how many types were looked at.
        /// </summary>
        public static string WrongOfferInCity(out int types)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string wrong = null;
            List<Building> all = BuildingHelper.allBuildings;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                string type = all[i] != null ? all[i].BuildingType : null;
                if (!string.IsNullOrEmpty(type) && seen.Add(type))
                {
                    wrong = wrong ?? WrongOffer(type);
                }
            }
            types = seen.Count;
            return wrong;
        }

        private static string SuitableOf(string businessType)
        {
            BusinessType type = BusinessTypeHelper.GetData(businessType);
            return type != null ? type.suitableBuildingType : null;
        }

        /// <summary>A business of a type made for shops or another kind of building, not for offices: it cannot stay in an office.</summary>
        public static bool OtherBusiness(BuildingRegistration reg)
        {
            if (reg == null || string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == Empty)
            {
                return false;
            }
            BusinessType type = BusinessTypeHelper.GetData(reg.businessTypeName);
            return type != null && type.suitableBuildingType != Office;
        }

        /// <summary>The map's filters applied again, as the game does after a building is rented.</summary>
        public static void Filters()
        {
            if (GameManager.isCitySceneBeingUnloaded)
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null)
            {
                MapPlaces.Refilter(uis.mapFilters);
            }
        }

        /// <summary>One of the two offices kept for the player (Core/CityReserves), as the game writes its address, for the Guide.</summary>
        public static string PlayerOffice(int i)
        {
            try
            {
                string street = CityReserves.PlayerOfficeStreets[i];
                string name = Streets.AddressHelper.GetStreetNameLocalized(street);
                return CityReserves.PlayerOfficeAddress(i, string.IsNullOrEmpty(name) || name == street ? null : name);
            }
            catch (Exception)
            {
                return CityReserves.PlayerOfficeAddress(i, null);   // outside the game: the Guide's page made offline
            }
        }

        /// <summary>City Utilities' building (Core/CityReserves), as the game writes its address, for the Guide.</summary>
        public static string UtilitiesAddress()
        {
            try
            {
                string name = Streets.AddressHelper.GetStreetNameLocalized(CityReserves.UtilitiesStreet);
                return CityReserves.UtilitiesAddress(string.IsNullOrEmpty(name) || name == CityReserves.UtilitiesStreet ? null : name);
            }
            catch (Exception)
            {
                return CityReserves.UtilitiesAddress(null);   // outside the game: the Guide's page made offline
            }
        }

        /// <summary>A business's name and its address, as the game writes them, for the save tools' report.</summary>
        public static string Named(BuildingRegistration reg, Building b)
        {
            string name = reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : null;
            string at = b != null && b.Address != null ? b.Address.ToFormattedString() : null;
            return name != null && at != null ? name + " (" + at + ")" : name ?? at ?? string.Empty;
        }
    }
}
