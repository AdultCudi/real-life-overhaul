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

        /// <summary>
        /// Opens a franchised shop at a building to let: a business of the game's own where the game has one of the type
        /// that fits the building, in the books only where it has none. The file gets its place, hold, the game's name
        /// for the business, its door and its customers.
        /// </summary>
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

        /// <summary>True while a building is to let by the game's own list, empty and not the player's: what a franchised shop may open in.</summary>
        public static bool Free(BuildingRegistration reg)
        {
            return reg != null && !reg.RentedByPlayer && !reg.BuildingOwnedByPlayer && reg.AvailableForRent && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == EmptyType);
        }

        /// <summary>
        /// Finds a business of the game's own of the brand's type trading in the district and not yet franchised, and
        /// fills in the file given for it to join as it stands. Nothing of the game's changes. False if there is none.
        /// </summary>
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
                    BuildingRegistration reg = building != null && building.SpecialService == null ? BuildingHelper.GetBuildingRegistration(building.Address) : null;
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

        // One of the game's ready-made businesses of the type with a layout for this building's size, an ordinary trader
        // before one of a rival's chain (a rival that is beaten takes its chain's shops with it).
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
        /// Keeps the city's own market from closing a franchised shop while its agreement runs: the game's daily run
        /// never closes a business that started within the last forty days (CompetitionHelper.TryShutdownBusinesses),
        /// so the shop's start day is kept at today. A converted shop's own day is put back by <see cref="Close"/>.
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

        /// <summary>
        /// A franchised shop closes. One the game started for the brand is closed by the game's own closure, which puts
        /// the building back to let; a converted one trades on with the start day it had; one in the books was never there.
        /// </summary>
        public static void Close(Franchise f, string type)
        {
            if (f.Hold == Hold.Converted && Stands(f, type))
            {
                BuildingRegistration was = RegAt(f.Place);
                if (was != null)
                {
                    was.creationDay = f.Born;
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

        /// <summary>True while the player stands inside the building a franchised shop is in.</summary>
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
