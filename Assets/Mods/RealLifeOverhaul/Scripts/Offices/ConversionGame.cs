using System;
using System.Collections.Generic;
using BigAmbitions.Rivals;
using Blueprints;
using Buildings;
using Helpers;
using Streets;
using UI;

namespace RealLifeOverhaul.Offices
{
    /// <summary>
    /// Everything the conversion reads from or does to the game. A building's type (Building.BuildingType) is a field of
    /// the city's building asset, loaded once and never saved, and everything asks it when it needs it: which businesses
    /// can start there, the map's filters and marker, the rent, value and deposit, the customers, the game's competition.
    /// The interior is loaded by size and version alone.
    ///
    /// A shop can be an office when the game's data lists its size and version for both (today size C version 1 and
    /// size D version 2): the game's own offices of that size stand on that structure, so an office business there has the
    /// game's office layouts and designs. Warehouses stay warehouses.
    ///
    /// Written, and put back when the city goes, the option is off or the save is prepared for removal: the chosen shops'
    /// BuildingType (the asset outlives a load), and RivalsHelper.IgnoredAddresses for a converted shop standing empty
    /// with no rival landlord, so the game's competition does not move in. Each change draws the building's marker and
    /// applies the map's filters again.
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
            return b != null && b.SpecialService == null && !GameMembers.Fenced(b) && (int)b.requiredDLC == 0 && !b.IsHamptonsHouse()
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

        /// <summary>No rival is the building's landlord.</summary>
        public static bool NoLandlord(BuildingRegistration reg) => reg != null && string.IsNullOrEmpty(reg.buildingOwnerRivalId);

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

        /// <summary>The building's type set, and its marker drawn again (not while the city is going).</summary>
        public static void SetType(Building b, CityBuildingController c, string type)
        {
            if (b == null || string.IsNullOrEmpty(type))
            {
                return;
            }
            b.BuildingType = type;
            if (!GameManager.isCitySceneBeingUnloaded && c != null && c.buildingRegistration != null)
            {
                c.UpdatePoi();
            }
        }

        /// <summary>On or off the game's list of addresses its rivals leave alone. Returns whether it is on it now.</summary>
        public static bool Ignore(Address address, bool on)
        {
            List<Address> list = RivalsHelper.IgnoredAddresses;
            if (address == null || list == null)
            {
                return false;
            }
            int at = list.IndexOf(address);
            if (on && at < 0)
            {
                list.Add(address);
            }
            else if (!on && at >= 0)
            {
                list.RemoveAt(at);
            }
            return on;
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
