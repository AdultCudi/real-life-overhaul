using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// A made building and the city map's filters. The map keeps no list of its own: each time it opens or a row is switched it walks
    /// CityManager's list of controllers once and lights each building by its record's type and its registration's status
    /// (CityMapFilters.ApplyFilters, copied as <see cref="MapRules.Rows"/>). A building in that list with its registration shows under
    /// the right rows by itself; what a load does besides, once for each building, is its marker (CityMap.SetupPois makes it, and makes it
    /// permanent for one the player rents or owns) and the game's event for a registration that changed (the sign, the lights, the map's
    /// search list). <see cref="Register"/> does those for a building that stands in play, or is set up at its first load, or changes its
    /// type, with the rules' steps (<see cref="MapRules.Register"/>), and <see cref="LeaveCity"/> takes a building knocked down out of the list
    /// and its marker off the map (<see cref="MapRules.Leave"/>). Nothing here runs a frame: one call a building that stands, changes use or
    /// goes. Local to this machine: nothing is sent, and a Going Public session only ever reaches it through the same one-off calls.
    /// </summary>
    internal static partial class MadeGame
    {
        /// <summary>
        /// A building that stands in play (or is set up at its first load, or has changed its type) registered with the city map as the load
        /// registers one: its marker, the game's event for a registration that changed, and the filters applied again while the map is open.
        /// False, with why, when it is not in the city's list (then the next load lists and registers it) or the game's pieces are missing.
        /// </summary>
        public static bool Register(MadeBuilding made, out string why)
        {
            why = null;
            CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
            CityBuildingController c = made != null ? made.Controller : null;
            BuildingRegistration reg = made != null ? made.Registration : null;
            if (city == null || c == null || reg == null || made.Building == null)
            {
                why = "the city's list or its records are not there";
                return false;
            }
            if (!InCityList(made, out _))
            {
                why = "it is not in the city's list yet";
                return false;
            }
            MapStep steps = MapRules.Register(c.poi != null, reg.RentedByPlayer, reg.BuildingOwnedByPlayer, CityMap.IsOpen);
            if ((steps & MapStep.Marker) != 0)
            {
                c.UpdatePoi();   // as CityMap.SetupPois does at a load: permanent, with the owner's and renter's marks, for a building the player rents or owns
            }
            if ((steps & MapStep.Announce) != 0)
            {
                GlobalEvents.onBuildingRegistrationChange?.Invoke(made.Building.Address);   // the game's own event: the sign, the lights, the marker and the map's search list
            }
            if ((steps & MapStep.Apply) != 0)
            {
                Refilter();   // the game does the same after a rent and a purchase; a shut map applies the filters when it opens
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info(RegisterCheck(made, steps));
            }
            return true;
        }

        // The mod's one place that applies the game's filters again: only while the open map is there, a fault kept (MapPlaces.Refilter).
        private static void Refilter()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.mapFilters != null)
            {
                MapPlaces.Refilter(uis.mapFilters);
            }
        }

        // One line a building registered: where the filters would light it with the rows it belongs to switched on, and its marker.
        private static string RegisterCheck(MadeBuilding made, MapStep steps)
        {
            CityBuildingController c = made.Controller;
            BuildingRegistration reg = made.Registration;
            string type = made.Building.BuildingType;
            bool theirs = reg.RentedByPlayer || reg.BuildingOwnedByPlayer;
            var on = new MapOn { ByYou = true, Types = new[] { type }, Businesses = new string[0] };
            MapRow rows = MapRules.Rows(new MapBuilding
            {
                Listed = InCityList(made, out _), Type = type, RentedByPlayer = reg.RentedByPlayer, Owned = reg.BuildingOwnedByPlayer,
                AvailableForRent = reg.AvailableForRent, Business = reg.businessTypeName
            }, on);
            bool marker = !theirs || (c.poi != null && c.poi.Permanent);
            bool lit = !theirs || (rows & MapRow.ByYou) != 0;
            bool toLet = !reg.AvailableForRent || (rows & MapRow.ForRent) != 0;
            string whose = reg.BuildingOwnedByPlayer ? "the player's deed" : reg.RentedByPlayer ? "rented by the player" : "the city's";
            string mark = c.poi != null ? (c.poi.Permanent ? "permanent" : "made") : "none";
            return "Map check (a building registered, " + made.Address + "): type " + type + ", " + whose + (reg.AvailableForRent ? ", to let" : ", not to let")
                   + ", rows that light it " + (rows == MapRow.None ? "none" : rows.ToString()) + ", marker " + mark + ", steps " + steps + ": " + (marker && lit && toLet ? "PASS" : "FAIL");
        }

        /// <summary>
        /// A building knocked down in play taken off the city map: out of CityManager's list of controllers and its lookup by address (the
        /// filters walk that list, so a controller left in it is lit again at every opening of the map), its marker out of the map's
        /// lists and destroyed, its outline put out, the closest-building job made again and the map's search list made again without it.
        /// True when it left (or had never been there); false, with why, when the game's pieces are missing (then none of it is touched).
        /// </summary>
        public static bool LeaveCity(MadeBuilding old, out string why)
        {
            try
            {
                return LeaveCityNow(old, out why);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                why = "a fault (Player.log has it)";
                return false;
            }
        }

        private static bool LeaveCityNow(MadeBuilding old, out string why)
        {
            why = null;
            CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
            CityBuildingController c = old != null ? old.Controller : null;
            if (city == null || c == null || old.Building == null)
            {
                return true;   // nothing of it was in a city
            }
            CityBuildingController[] open = GameDlc.Open(city);
            bool listed = open != null && Array.IndexOf(open, c) >= 0;
            MapStep steps = MapRules.Leave(listed, c.poi != null);
            if (steps == MapStep.None)
            {
                return true;
            }
            var lookup = GameContract.CityLookup.Field?.GetValue(city) as Dictionary<Address, CityBuildingController>;
            if ((steps & MapStep.Delist) != 0 && (lookup == null || GameContract.ClosestReset.Method == null))
            {
                why = "this build of the game keeps its list of buildings otherwise (" + (GameContract.CityLookup.Problem ?? GameContract.ClosestReset.Problem ?? "not found") + ")";
                return false;
            }
            try
            {
                c.SetHighlight(false);   // its outline out, as the game puts one out when no row lights its building
            }
            catch (Exception e)
            {
                Logger?.Warn("Made buildings: the outline of a building knocked down could not be put out (" + e.GetType().Name + ").");
            }
            if ((steps & MapStep.Unmark) != 0)
            {
                Unmark(city, c);
            }
            if ((steps & MapStep.Delist) != 0)
            {
                Address address = old.Building.Address;
                GameDlc.Leave(city, c);
                if (lookup.TryGetValue(address, out CityBuildingController there) && there == c)
                {
                    lookup.Remove(address);
                }
                ResetClosest();
            }
            if ((steps & MapStep.Search) != 0)
            {
                RemakeSearch();
            }
            return true;
        }

        // Its marker out of the map's list of markers and of the filters' list of those they hid, then destroyed: the game never destroys one,
        // so every list that names it is cleaned first (a shut map shows the hidden ones again, and a destroyed marker there would throw).
        private static void Unmark(CityManager city, CityBuildingController c)
        {
            PointOfInterest poi = c.poi;
            if (poi == null)
            {
                return;
            }
            c.poi = null;
            UIs uis = InstanceBehavior<UIs>.Instance;
            var hidden = uis != null && uis.mapFilters != null ? GameContract.MapHidden.Field?.GetValue(uis.mapFilters) as List<PointOfInterest> : null;
            if (hidden != null)
            {
                hidden.RemoveAll(each => each == poi);
                MapPlaces.Drop(poi);   // out of the map's list of markers, its caches made again, then destroyed: the mod's one way to take a marker off the map
            }
            else
            {
                if (city.cityMap != null && city.cityMap.pois != null)
                {
                    city.cityMap.pois.Remove(poi);
                }
                poi.SetPermanent(false);   // left where it is, put out, and named by no list the map walks
                poi.SetHidden(true);
            }
        }

        // The game's own list for the search box made again, from the city's list as it is now.
        private static void RemakeSearch()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            var populate = GameContract.SearchPopulate.Method;
            if (uis == null || uis.mapFilters == null || populate == null)
            {
                return;
            }
            try
            {
                populate.Invoke(uis.mapFilters, new object[] { null });
                SpecialNames.SearchAgain();   // the game's list holds only named businesses: ours (every building) again while the map is open
            }
            catch (Exception e)
            {
                Logger?.Warn("Made buildings: the city map's search list could not be made again (" + e.GetType().Name + "); it is made again at the next load.");
            }
        }
    }
}
