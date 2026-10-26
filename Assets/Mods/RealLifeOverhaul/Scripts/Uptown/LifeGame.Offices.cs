using System;
using System.Collections.Generic;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Helpers;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// What the offices' tests read of the game: one of the district's buildings as an office (its business, staff, layout,
    /// market and owner), and the rest of the city's offices with businesses, to set the district's share against. Nothing
    /// here runs a frame: each is asked by a test's press.
    /// </summary>
    internal static partial class LifeGame
    {
        /// <summary>
        /// A district building as the offices' census reads it, when the game built it as an office; false for any other
        /// type, or one whose records are not there. <paramref name="at"/> is where the player stands.
        /// </summary>
        public static bool Office(MadeBuilding made, Vector3 at, out OfficeView view)
        {
            view = default(OfficeView);
            BuildingRegistration reg = made != null ? made.Registration : null;
            Building record = made != null ? made.Building : null;
            if (reg == null || record == null || record.BuildingType != BuildingsRules.OfficeType)
            {
                return false;
            }
            string type = reg.businessTypeName ?? string.Empty;
            float dx = made.DoorX - at.x, dz = made.DoorZ - at.z;
            view = new OfficeView
            {
                Address = made.Address ?? reg.StreetName + " " + reg.StreetNumber, Street = reg.StreetName, Number = reg.StreetNumber, Type = type,
                Staffed = reg.aiEmployees != null && reg.aiEmployees.Count > 0,
                LaidOut = !string.IsNullOrEmpty(reg.Layout) && BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(type, new BuildingSizeInfo(record), reg.Layout, warnIfNotFound: false) != null,
                ToLet = reg.AvailableForRent && (type.Length == 0 || type == BuildingsRules.EmptyBusinessType),
                Players = reg.RentedByPlayer || reg.BuildingOwnedByPlayer || GoingPublic.Foreign(reg),
                Employs = WorkEmploys(type), Distance2 = dx * dx + dz * dz
            };
            return true;
        }

        // The working life's word on a type; no word (it is off, or a fault) is no.
        private static bool WorkEmploys(string type)
        {
            Func<string, bool> employs = DistrictWork.Employs;
            if (employs == null || string.IsNullOrEmpty(type))
            {
                return false;
            }
            try
            {
                return employs(type);
            }
            catch (Exception e)
            {
                LifeDirector.Logger?.Error(e);
                return false;
            }
        }

        /// <summary>The district's units of a building type that are the city's (no special building, not the player's) and how many of them stand empty to let.</summary>
        public static void UnitsOf(List<MadeBuilding> standing, string type, out int units, out int emptyToLet)
        {
            units = emptyToLet = 0;
            for (int i = 0; i < standing.Count; i++)
            {
                BuildingRegistration reg = standing[i] != null ? standing[i].Registration : null;
                Building record = standing[i] != null ? standing[i].Building : null;
                if (reg == null || record == null || record.BuildingType != type || record.SpecialService != null || reg.RentedByPlayer || reg.BuildingOwnedByPlayer)
                {
                    continue;
                }
                units++;
                string business = reg.businessTypeName;
                emptyToLet += reg.AvailableForRent && (string.IsNullOrEmpty(business) || business == BuildingsRules.EmptyBusinessType) ? 1 : 0;
            }
        }

        /// <summary>
        /// The rest of the city's office buildings (the registrations in the save's list on the game's own streets; the
        /// game's special buildings, which it never lets, left out) and how many have a business in them: the game's own
        /// share, which the census sets Uptown's against. One pass over the save's registrations.
        /// </summary>
        public static void CityOffices(out int offices, out int businesses)
        {
            offices = businesses = 0;
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || !reg.HasValidAddress || DistrictSave.DistrictSaveRules.IsModId(reg.StreetName) || reg.RentedByPlayer || reg.BuildingOwnedByPlayer)
                {
                    continue;   // Uptown's own (the mod's streets), and the player's, neither let nor the game's
                }
                Building b = GameRegistrations.BuildingOf(reg);
                if (b == null || b.BuildingType != BuildingsRules.OfficeType || b.SpecialService != null || GameDlc.Gated(b))
                {
                    continue;
                }
                offices++;
                string type = reg.businessTypeName;
                businesses += !string.IsNullOrEmpty(type) && type != BuildingsRules.EmptyBusinessType ? 1 : 0;
            }
        }
    }
}
