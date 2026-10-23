using System;
using System.Collections.Generic;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>The game part for a franchised shop in the city: opening, converting, keeping and closing it.</summary>
    internal static partial class BrandsGame
    {
        private const string EmptyType = "ba:businesstype_empty";

        private static readonly List<BuildingRegistration> One = new List<BuildingRegistration>(1);

        /// <summary>Opens a franchised shop at a building to let: as a business of the game's own where one of the type fits, else in the books only.</summary>
        public static void Open(Franchise f, BrandSite site, string type)
        {
            f.Place = site.Place;
            f.Hold = Hold.Books;
            f.GameName = string.Empty;
            BuildingRegistration reg = site.Reg;
            try
            {
                AiBusinessDefault chosen = Default(type, reg);
                // A guest of a Going Public session opens it in the books only: the city's businesses are the host's.
                if (chosen != null && Free(reg) && !GoingPublic.IsGuest)
                {
                    CompetitionHelper.StartNewCompetitorBusiness(type, reg, true, chosen, CompetitionHelper.GetRivalIdForBusinessDefault(chosen));
                    f.Hold = Hold.Opened;
                    f.GameName = reg.BusinessName ?? string.Empty;   // never renamed: the game finds its sign and goods by this name and throws on one it does not know
                    SaveGameManager.MarkChange();
                }
            }
            catch (Exception e)
            {
                // Nothing half-opened is left standing: the game's own closure puts the building back to let.
                Logger?.Error(e);
                try
                {
                    if (reg != null && !reg.RentedByPlayer && reg.businessTypeName == type)
                    {
                        reg.ShutDownAIBusiness();
                    }
                }
                catch (Exception second)
                {
                    Logger?.Error(second);
                }
                f.Hold = Hold.Books;
                f.GameName = string.Empty;
            }
            Settle(f, site.Building, type, f.Hold == Hold.Opened ? reg : null);
            Logger?.Info("Brands: " + f.Name + " opens at " + site.Label + (f.Hold == Hold.Opened
                ? " as a business of the game's own, '" + f.GameName + "' (the game's name and sign for it are kept: it finds such a business by its name)"
                : " in the mod's books only (the game has no business of this type that fits the building)") + "; " + f.Base + " customers on an ordinary day by the game's formula.");
        }

        /// <summary>True while a building is to let by the game's own list, empty and not the player's.</summary>
        public static bool Free(BuildingRegistration reg)
        {
            return reg != null && !reg.RentedByPlayer && !reg.BuildingOwnedByPlayer && reg.AvailableForRent && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == EmptyType);
        }

        /// <summary>Fills in the file for a business of the game's of the type, trading in the district and not franchised; changes nothing. False if none.</summary>
        public static bool Convert(Franchise f, BrandsState state, string district, string type)
        {
            try
            {
                List<Building> all;
                if (string.IsNullOrEmpty(district) || !BuildingHelper.AllNeighbourhoodBuildings.TryGetValue(district, out all) || all == null)
                {
                    return false;
                }
                for (int i = 0; i < all.Count; i++)
                {
                    Building building = all[i];
                    BuildingRegistration reg = building != null && building.SpecialService == null && !GameDlc.Gated(building) ? BuildingHelper.GetBuildingRegistration(building.Address) : null;
                    if (reg == null || reg.RentedByPlayer || reg.businessTypeName != type || !reg.HasEstablishedBusiness || Taken(state, Place(reg)))
                    {
                        continue;
                    }
                    f.Place = Place(reg);
                    f.Hold = Hold.Converted;
                    f.GameName = reg.BusinessName ?? string.Empty;
                    f.Born = reg.creationDay;
                    Settle(f, building, type, reg);
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return false;
        }

        private static bool Taken(BrandsState state, string place)
        {
            for (int i = 0; i < state.Franchises.Count; i++)
            {
                if (string.Equals(state.Franchises[i].Place, place, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // The door and the customers of a shop that has just been placed.
        private static void Settle(Franchise f, Building building, string type, BuildingRegistration reg)
        {
            Vector3 spot = Vector3.zero;
            f.HasSpot = building != null && Spot(building.Address, out spot);
            if (f.HasSpot)
            {
                f.X = Mathf.RoundToInt(spot.x);
                f.Z = Mathf.RoundToInt(spot.z);
            }
            f.Base = Customers(building, type, reg);
        }

        // One of the game's ready-made businesses of the type that fits the building, an ordinary trader before a rival's
        // chain (a beaten rival takes its chain's shops with it).
        private static AiBusinessDefault Default(string type, BuildingRegistration reg)
        {
            if (reg == null || reg.BuildingCached == null)
            {
                return null;
            }
            AiBusinessDefault[] all = CompetitionHelper.GetBusinessDefaultsByType(type);
            var size = new BuildingSizeInfo(reg);
            AiBusinessDefault chain = null;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                AiBusinessDefault d = all[i];
                if (d == null || string.IsNullOrEmpty(d.businessName) || d.schedule == null || BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(type, size, d.buildingLayout, false) == null)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(d.corporationRivalId))
                {
                    return d;
                }
                chain = chain ?? d;
            }
            return chain;
        }

        /// <summary>True while a franchised shop that is a business of the game's is still that business: the same type and name, not the player's.</summary>
        public static bool Stands(Franchise f, string type)
        {
            if (f.Hold == Hold.Books)
            {
                return true;
            }
            BuildingRegistration reg = RegAt(f.Place);
            return reg != null && !reg.RentedByPlayer && reg.businessTypeName == type && string.Equals(reg.BusinessName ?? string.Empty, f.GameName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Keeps the city's market from closing a franchised shop: the game never closes a business started in the last
        /// forty days (CompetitionHelper.TryShutdownBusinesses), so its start day is kept at today.
        /// </summary>
        public static void Keep(Franchise f, string type, int day)
        {
            if (f.Hold == Hold.Books)
            {
                return;
            }
            BuildingRegistration reg = RegAt(f.Place);
            if (reg != null && !reg.RentedByPlayer && reg.businessTypeName == type)
            {
                reg.creationDay = day;
            }
        }

        /// <summary>A franchised shop closes: an opened one by the game's own closure, a converted one trades on with its own start day.</summary>
        public static void Close(Franchise f, string type)
        {
            if (f.Hold == Hold.Converted && Stands(f, type))
            {
                BuildingRegistration was = RegAt(f.Place);
                int born = FranchiseRules.BornBack(f);
                if (was != null && born >= 0)
                {
                    was.creationDay = born;   // unknown (an older save): the day the daily keep last wrote stands
                }
                return;
            }
            if (f.Hold != Hold.Opened || !Stands(f, type))
            {
                return;
            }
            if (GoingPublic.IsGuest)
            {
                return;   // the business stands in the host's city, which a guest does not close
            }
            try
            {
                One.Clear();
                One.Add(RegAt(f.Place));
                CompetitionHelper.ShutdownBusinessesImmediate(One);
                SaveGameManager.MarkChange();
                Logger?.Info("Brands: '" + f.GameName + "' at " + AddressWords(f.Place) + " closed by the game's own closure; the building is to let again.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            finally
            {
                One.Clear();
            }
        }

        public static bool PlayerInside(Franchise f)
        {
            try
            {
                BuildingRegistration reg = f.Hold != Hold.Books ? RegAt(f.Place) : null;
                return reg != null && BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance != null && InstanceBehavior<BuildingManager>.Instance.buildingRegistration == reg;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
