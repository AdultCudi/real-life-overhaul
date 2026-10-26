using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>The rows of the city map's filters that light a building (CityMapFilters.ApplyFilters).</summary>
    [Flags]
    public enum MapRow
    {
        None = 0,
        ByYou = 1,      // "Rented by you": the player rents it or holds its deed
        ForRent = 2,    // a row of the buildings for rent, by type (Warehouse, Office, Retail, Residential ...): it is to let and of that type
        Business = 4,   // a row of a business type (the special buildings, the active businesses)
        OnSale = 8,     // "Buildings for sale": on the game's list and not the player's
        OwnType = 16    // a type row, for a building the player owns or rents that is not to let (the game lights only one to let); the mod lights it after the game's look
    }

    /// <summary>What the city map's filters read of a building: its record's type and its registration's status.</summary>
    public struct MapBuilding
    {
        public bool Listed;             // in CityManager's list of controllers: the only thing the filters walk
        public string Type;             // the record's building type (Building.BuildingType), read live
        public bool RentedByPlayer;     // the registration's
        public bool Owned;              // the player's deed at its address (SaveGameManager.Current.realEstate)
        public bool AvailableForRent;   // the registration's
        public bool OnSale;             // on the save's list of buildings for sale
        public string Business;         // the registration's business type ("ba:businesstype_empty" for none)
        public bool Open;               // open for business now (BusinessHelper.IsBusinessOpen), asked only when "Hide closed" is on
    }

    /// <summary>The rows the player has switched on.</summary>
    public struct MapOn
    {
        public bool ByYou;
        public bool OnSale;
        public ICollection<string> Types;        // building types whose "for rent" row is on
        public ICollection<string> Businesses;   // business types whose row is on
        public bool HideClosed;                  // the "Hide closed" row is on
    }

    /// <summary>What a registration with the city map does, in the order it does it (<see cref="MapRules.Register"/>, <see cref="MapRules.Leave"/>).</summary>
    [Flags]
    public enum MapStep
    {
        None = 0,
        Marker = 1,     // the building's marker made and set from its registration (CityBuildingController.UpdatePoi): permanent when the player rents or owns it
        Announce = 2,   // the game's own event for a registration that changed (the sign, the lights, the marker, the map's search list)
        Apply = 4,      // the filters applied again, only while the map is open (the game does so after a purchase and a rent)
        Delist = 8,     // out of CityManager's list of controllers and its lookup by address, the closest-building job made again
        Unmark = 16,    // its marker out of the map's lists and gone, its outline put out
        Search = 32     // the map's search list made again without it
    }

    /// <summary>
    /// The city map's filters and the registration of a building with them, game-free so EconomyCheck holds them (docs/features/uptown.md,
    /// "On the map"). The map keeps no list of its own: every time it opens or a row is switched, it walks CityManager's list of controllers once
    /// and lights each building by its record's type and its registration's status (<see cref="Rows"/> is that walk's decision, copied from
    /// CityMapFilters.ApplyFilters, with the owner's addition: a building the player owns or rents shows under its own type row too, which
    /// MapPlaces.Owned lights once the game's look is over). So a building the construction game finishes shows under the right rows the moment it is in that list with
    /// the registration the game reads, and it must leave the list when it is knocked down, or its marker stays on the map. What the game
    /// does besides when a registration changes (its marker made permanent for a building the player rents or owns, which a load does once
    /// for each in CityMap.SetupPois) is <see cref="Register"/>'s. COST: nothing per frame; a few comparisons a call.
    /// </summary>
    public static class MapRules
    {
        /// <summary>
        /// The rows that light a building, as CityMapFilters.ApplyFilters decides: "Rented by you" for a building the player rents or holds the
        /// deed of; the building's type row while it is to let, and (the owner's decision) also for one the player owns or rents, lit by the mod
        /// after the game's look; a business row for the business in it; "for sale" for one on the list that the player does not own.
        /// A building outside the list is lit by nothing: the walk never reaches it.
        /// </summary>
        public static MapRow Rows(MapBuilding b, MapOn on)
        {
            if (!b.Listed)
            {
                return MapRow.None;
            }
            MapRow rows = MapRow.None;
            if (on.ByYou && (b.RentedByPlayer || b.Owned))
            {
                rows |= MapRow.ByYou;
            }
            if (b.AvailableForRent && b.Type != null && on.Types != null && on.Types.Contains(b.Type))
            {
                rows |= MapRow.ForRent;
            }
            else if ((b.Owned || b.RentedByPlayer) && b.Type != null && on.Types != null && on.Types.Contains(b.Type) && !HiddenAsClosed(b, on))
            {
                rows |= MapRow.OwnType;   // the owner's decision: a building the player owns or rents also shows under its own type row, to let or not
            }
            if (!string.IsNullOrEmpty(b.Business) && on.Businesses != null && on.Businesses.Contains(b.Business))
            {
                rows |= MapRow.Business;
            }
            if (on.OnSale && b.OnSale && !b.Owned)
            {
                rows |= MapRow.OnSale;
            }
            return rows;
        }

        // The game's "Hide closed" (ApplyFilters): a building not to let, not residential and not open for business is not lit.
        private static bool HiddenAsClosed(MapBuilding b, MapOn on)
        {
            return on.HideClosed && !b.AvailableForRent && b.Type != ResidentialType && !b.Open;
        }

        private const string ResidentialType = "ba:buildingtype_residential";

        /// <summary>
        /// One line of the map's search list (CityMapFilters.PopulateSearchDropdown) and the word it is found by. A building with a business
        /// name is listed as the game lists it, "name" over its address, found by the name. Any other building is listed too, by its
        /// address over its type, found by the address. Nothing is left out of the search for being empty.
        /// </summary>
        public static string SearchLine(string businessName, string address, string typeName, out string word)
        {
            if (!string.IsNullOrWhiteSpace(businessName))
            {
                word = businessName;
                return "<b><noparse>" + businessName + "</noparse></b><br><noparse>" + address + "</noparse>";
            }
            word = address;
            return "<b><noparse>" + address + "</noparse></b>" + (string.IsNullOrWhiteSpace(typeName) ? "" : "<br><noparse>" + typeName + "</noparse>");
        }

        /// <summary>
        /// What a building that has just stood, or whose use has changed, does with the map: the load's own steps and the game's. A marker is
        /// made for a building that has one or that the player rents or owns (CityMap.SetupPois, which a load runs once for each);
        /// the game's event for a registration that changed always, so the sign, the lights and the search list follow; the filters
        /// applied again only while the map is open, as the game does after a rent or a purchase (a shut map applies them when it opens).
        /// </summary>
        public static MapStep Register(bool hasMarker, bool rented, bool owned, bool mapOpen)
        {
            MapStep steps = MapStep.Announce;
            if (hasMarker || rented || owned)
            {
                steps |= MapStep.Marker;
            }
            if (mapOpen)
            {
                steps |= MapStep.Apply;
            }
            return steps;
        }

        /// <summary>
        /// What a building knocked down does with the map: out of the list the filters walk (else its marker lights again at every opening),
        /// its marker gone with it, and the search list made again. Nothing for a building that was never in the list.
        /// </summary>
        public static MapStep Leave(bool listed, bool hasMarker)
        {
            if (!listed && !hasMarker)
            {
                return MapStep.None;
            }
            MapStep steps = MapStep.Search;
            if (listed)
            {
                steps |= MapStep.Delist;
            }
            if (hasMarker)
            {
                steps |= MapStep.Unmark;
            }
            return steps;
        }

    }

    /// <summary>
    /// CityManager's list of controllers grown and shortened the way the game's own start of a city makes it: one controller for each address,
    /// a new array each time (the game's readers hold the array they were given). Game-free over any reference type so EconomyCheck holds the
    /// rule that GameDlc.Join and GameDlc.Leave apply. COST: one walk over the list, once a building stands or goes.
    /// </summary>
    public static class CityList
    {
        /// <summary>
        /// The list with <paramref name="made"/> at its end, in place of itself and of any entry at the same address (a building knocked down
        /// there): one entry for the address however often it is joined, which is what the next load's list holds too.
        /// </summary>
        public static T[] With<T>(T[] was, T made, Func<T, bool> sameAddress) where T : class
        {
            if (was == null || made == null)
            {
                return was;
            }
            var now = new List<T>(was.Length + 1);
            for (int i = 0; i < was.Length; i++)
            {
                T each = was[i];
                if (ReferenceEquals(each, made) || (each != null && sameAddress != null && sameAddress(each)))
                {
                    continue;
                }
                now.Add(each);
            }
            now.Add(made);
            return now.ToArray();
        }

        /// <summary>The list without <paramref name="gone"/>; the same array when it was not in it (nothing changed, nothing to make again).</summary>
        public static T[] Without<T>(T[] was, T gone, out bool removed) where T : class
        {
            removed = false;
            if (was == null || gone == null)
            {
                return was;
            }
            int at = Array.IndexOf(was, gone);
            if (at < 0)
            {
                return was;
            }
            removed = true;
            var now = new T[was.Length - 1];
            Array.Copy(was, 0, now, 0, at);
            Array.Copy(was, at + 1, now, at, was.Length - at - 1);
            return now;
        }
    }
}
