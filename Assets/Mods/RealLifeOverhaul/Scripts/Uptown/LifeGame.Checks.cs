using System;
using System.Collections.Generic;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using City.CityMap;
using Helpers;
using Localizor;
using RealLifeOverhaul.NewBuildings;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// What Uptown's tests of its offices, its map row and its frame cost read of the game: a leased office's business as the
    /// game's own office businesses are, the row's data and the zone it shows, and which of the district's buildings are in
    /// view. Nothing here runs a frame: each is asked by a test's press.
    /// </summary>
    internal static partial class LifeGame
    {
        /// <summary>
        /// Null when a leased office's business is as a business of the game's of its type: the lessee's type, not to
        /// let; a layout the game finds for its type and structure (its furniture and staff); the game's own valuation of it
        /// read without a fault; staff of the game's; a row for its type in the map's filters; held off the market. Else why not.
        /// </summary>
        public static string OfficeWhy(MadeBuilding made, DistrictLease lease)
        {
            BuildingRegistration reg = made != null ? made.Registration : null;
            Building record = made != null ? made.Building : null;
            if (reg == null || record == null)
            {
                return "its records are not there";
            }
            if (!Leased(made, lease))
            {
                return "its business is " + (reg.businessTypeName ?? "(none)") + " named " + (reg.BusinessName ?? "(none)") + (reg.AvailableForRent ? ", to let" : string.Empty);
            }
            if (string.IsNullOrEmpty(reg.Layout) || BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(reg.businessTypeName, new BuildingSizeInfo(record), reg.Layout, warnIfNotFound: false) == null)
            {
                return "the game finds no layout " + (reg.Layout ?? "(none)") + " for its type and structure";
            }
            float value;
            try
            {
                value = CompetitionHelper.CalculateAiOwnedValuation(reg);
            }
            catch (Exception e)
            {
                return "the game's valuation of it fails (" + e.GetType().Name + ")";
            }
            if (!(value > 0f))
            {
                return "the game values it at nothing";
            }
            if (reg.aiEmployees == null || reg.aiEmployees.Count == 0)
            {
                return "it has no staff of the game's";
            }
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            var toggles = uis != null && uis.mapFilters != null ? GameContract.FilterToggles.Field?.GetValue(uis.mapFilters) as Dictionary<string, Toggle> : null;
            if (toggles != null && toggles.Count > 0 && !toggles.ContainsKey(lease.BusinessType.ToLowerInvariant()))
            {
                return "the map's filters have no row for its type";
            }
            if (SpecialBuildings.TakenBy(reg) == null)
            {
                return "it is not held off the market";
            }
            return null;
        }

        /// <summary>True when Uptown's zone is among the game's district zones, the one its row shows.</summary>
        public static bool ZoneListed()
        {
            CityMapNeighborhoodZones.CityMapNeighborhoodZone[] all = CityMapNeighborhoodZones.Zones;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (all[i] != null && all[i].neighbourhood == DistrictValues.Id && all[i].zoneRenderer != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when Uptown's row's data names the district: ticked, the game's filters show its zone.</summary>
        public static bool RowNamesDistrict()
        {
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            var entries = uis != null && uis.mapFilters != null ? GameContract.FilterEntries.Field?.GetValue(uis.mapFilters) as Dictionary<string, CityMapFilter> : null;
            return entries != null && entries.TryGetValue(DistrictValues.Id, out CityMapFilter row) && row != null && row.Data != null && row.Data.neighbourhood == DistrictValues.Id;
        }

        /// <summary>The name the row shows: the district's id's text in the player's language, as the game names its own district rows.</summary>
        public static string RowName()
        {
            return DistrictValues.Id.GetLocalization();
        }

        /// <summary>True when any part of the building is drawn by a camera now; how many of its parts are.</summary>
        public static bool InView(MadeBuilding made, out int drawn)
        {
            drawn = 0;
            CityBuildingController c = made != null && made.Shown ? made.Controller : null;
            Renderer[] parts = c != null ? c.Renderers : null;
            for (int i = 0; parts != null && i < parts.Length; i++)
            {
                drawn += parts[i] != null && parts[i].isVisible ? 1 : 0;
            }
            return drawn > 0;
        }
    }
}
