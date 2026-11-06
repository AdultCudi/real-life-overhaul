using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using Streets;
using UI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A building the player owns or rents, lit under its own type row. The game's filters light a type row (Warehouse, Office, Retail,
    /// Residential) only for a building to let (CityMapFilters.ApplyFilters), so an owned empty warehouse showed under Rented by you and
    /// nowhere else. The owner's decision is that it shows under its type row too. The game does its look at every building spread over
    /// frames; once that look is over (the same moment the mod's own places are lit again, <see cref="Light"/>) this walks the city's list
    /// once and lights each building <see cref="MapRules.Rows"/> says the type row lights and no game row has lit, in the game's own colour
    /// and icon. Display only: the registration's AvailableForRent and every rent rule are untouched. COST: one walk of the city's list
    /// when the map opens, a row is switched or a registration changes (each of which is one look of the game's, which walks it too); nothing
    /// per frame. Local to this machine: nothing is sent.
    /// </summary>
    public static partial class MapPlaces
    {
        private static bool _ownedDue;      // the map just opened or the game's look has run: light again once it is over
        private static bool _ownedFailed;   // a fault or a game without the rows' fields: the game's own lighting stands

        // The map opened or a look ran: the owner's buildings are to be lit once the game's look is over.
        private static void OwnedDue()
        {
            if (!_ownedFailed)
            {
                _ownedDue = true;
                if (_driver != null)
                {
                    _driver.enabled = true;   // Light runs while the map is open
                }
            }
        }

        // The game's look is over: the owner's buildings lit under their type rows.
        private static void LightOwned()
        {
            _ownedDue = false;
            try
            {
                LightOwnedNow();
            }
            catch (Exception e)
            {
                _ownedFailed = true;
                Logger?.Error(e);
                Logger?.Warn("Map places: a building of the player's could not be lit under its type row; the game's own rows light it.");
            }
        }

        private static void LightOwnedNow()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityMapFilters filters = uis != null ? uis.mapFilters : null;
            var typesField = GameContract.VisibleTypes.Field;
            var closedField = GameContract.HideClosedRow.Field;
            if (typesField == null || closedField == null)
            {
                _ownedFailed = true;
                string problem = GameContract.VisibleTypes.Problem ?? GameContract.HideClosedRow.Problem;
                Logger?.Info("Map places: this build of the game keeps its map rows otherwise (" + problem + "); the game's own rows light the player's buildings.");
                return;
            }
            if (filters == null || city == null || SaveGameManager.Current == null)
            {
                return;
            }
            var types = typesField.GetValue(filters) as HashSet<string>;
            if (types == null || types.Count == 0)
            {
                return;   // no type row is on: nothing to add
            }
            var on = new MapOn { Types = types, Businesses = new string[0], HideClosed = closedField.GetValue(filters) is bool hide && hide };
            List<RealEstate> estate = SaveGameManager.Current.realEstate;
            var deeds = new HashSet<Address>();
            for (int i = 0; estate != null && i < estate.Count; i++)
            {
                if (estate[i] != null)
                {
                    deeds.Add(estate[i].address);
                }
            }
            CityBuildingController[] all = GameDlc.Open(city);
            int lit = 0;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                if (c == null || c.building == null || c.buildingRegistration == null)
                {
                    continue;
                }
                BuildingRegistration reg = c.buildingRegistration;
                bool owned = deeds.Contains(c.building.Address);
                if (!owned && !reg.RentedByPlayer)
                {
                    continue;
                }
                var building = new MapBuilding
                {
                    Listed = true, Type = c.building.BuildingType, Owned = owned, RentedByPlayer = reg.RentedByPlayer, AvailableForRent = reg.AvailableForRent,
                    Business = reg.businessTypeName, Open = !on.HideClosed || BusinessHelper.IsBusinessOpen(reg)
                };
                // Only a building the type row alone lights: another row (a business) has lit it already, in its own colour.
                if ((MapRules.Rows(building, on) & MapRow.OwnType) == 0)
                {
                    continue;
                }
                LightByType(c, reg);
                lit++;
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Map places check (the player's buildings under their type rows): " + lit + " lit among " + types.Count + " ticked type row(s), "
                    + deeds.Count + " deed(s): PASS");
            }
        }

        // As the game lights a building (ApplyFilters): the type's colour and icon, the business's when it has one.
        private static void LightByType(CityBuildingController c, BuildingRegistration reg)
        {
            BuildingTypeData data = BuildingTypeHelper.GetData(c.building);
            Color color = data.mapFilterColor;
            Sprite icon = data.poiIcon;
            if (reg.businessTypeName != "ba:businesstype_empty")
            {
                BusinessType business = BusinessTypeHelper.GetData(reg);
                color = business.cityMapFilterColor;
                icon = c.building.SpecialService != null && c.building.SpecialService.overridePoiIcon != null ? c.building.SpecialService.overridePoiIcon : business.icon;
            }
            if (c.poi == null)
            {
                c.CreatePOI();
            }
            else if (c.poi.Permanent)
            {
                c.poi.SetHidden(false);
            }
            c.SetHighlight(true, (Color32)color);
            c.poi.SetIcon(icon, color);
        }
    }
}
