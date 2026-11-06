using System;
using System.Collections.Generic;
using Buildings;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The game's side of a grown building's tenant (LifeDirector.Grow): its unit let as the mix pass lets one, by the same
    /// picks (the kind its street holds fewest of, the game's own start of a business, its chance seeded by the lot and the save
    /// so every machine of a save picks alike), a home let to residents as the game's own swap of homes sets one; and its
    /// traffic index lowered for its condition. COST: nothing per frame; once on a grown building's day.
    /// </summary>
    internal static partial class LifeGame
    {
        /// <summary>A grown building's unit let to its tenant: null when it was, else why not (English, for the log).</summary>
        public static string LetOne(MadeBuilding made, List<MadeBuilding> all, Func<MadeBuilding, bool> skip, GameInstance save)
        {
            BuildingRegistration reg = made != null ? made.Registration : null;
            Building record = made != null ? made.Building : null;
            if (reg == null || record == null)
            {
                return "its records are not there";
            }
            if (reg.RentedByPlayer || reg.BuildingOwnedByPlayer || GoingPublic.Foreign(reg))
            {
                return "it is the player's, or another player's in this session";
            }
            if (skip != null && skip(made))
            {
                return "it is a lease's or a special place's";
            }
            if (record.BuildingType == MixRules.HomeType)
            {
                if (!reg.AvailableForRent)
                {
                    return "its homes are let already";
                }
                reg.AvailableForRent = false;   // let to residents, as the game's own swap of homes sets one
                GlobalEvents.onBuildingRegistrationChange?.Invoke(reg.Address);
                return null;
            }
            if (!Unoccupied(reg))
            {
                return "a business trades there already";
            }
            if (Array.IndexOf(BusinessTypes, record.BuildingType) < 0)
            {
                return "a warehouse is let to whoever rents it, as the game lets one";
            }
            string seed = save != null ? save.seed ?? string.Empty : string.Empty;
            CountStreets(all, skip);
            try
            {
                return Open(made, seed, save) ? null : "no business of the game's has a layout for its structure";
            }
            finally
            {
                OnStreet.Clear();
            }
        }

        /// <summary>A district building's traffic index set (its rent, value and passers-by follow it); false for one with no record.</summary>
        public static bool Traffic(MadeBuilding made, int index)
        {
            if (made == null || made.Building == null)
            {
                return false;
            }
            made.Building.trafficIndex = Mathf.Clamp(index, 0, 100);
            return true;
        }
    }
}
