using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The construction game's reads and writes of a building's occupants for redevelopment: whether anything of the player's own
    /// is still in it (their business, their home, their staff: they move out first, by the game's own ways), what its tenants
    /// and a business of the city's in it pay, and the notice's end: the business closed by the game's own call
    /// (BuildingRegistration.ShutDownAIBusiness, as the game closes one that fails), the unit kept off the market and the
    /// deed's tenants gone. Nothing here keeps state. COST: nothing per frame; on a page, a press or a day's pass.
    /// </summary>
    internal static partial class ConstructionGame
    {
        private const string Residential = "ba:buildingtype_residential";

        /// <summary>
        /// Why a building is not empty of the player's own things, as a text key (their business trading in it, their home, a
        /// member of their staff placed there), or null when nothing of theirs is in it.
        /// </summary>
        internal static string Occupied(string street, int number)
        {
            BuildingRegistration reg = string.IsNullOrEmpty(street) ? null : BuildingHelper.GetBuildingRegistration(new Address(street, number));
            if (reg == null)
            {
                return null;
            }
            Building b = GameRegistrations.BuildingOf(reg);
            if (reg.RentedByPlayer && reg.HasEstablishedBusiness && reg.businessTypeName != EmptyBusiness)
            {
                return "reallife_bld_answer_vacant_business";
            }
            if (reg.RentedByPlayer && b != null && b.BuildingType == Residential)
            {
                return "reallife_bld_answer_vacant_home";
            }
            List<EmployeeInstance> staff = SaveGameManager.Current != null ? SaveGameManager.Current.EmployeeInstances : null;
            for (int i = 0; staff != null && i < staff.Count; i++)
            {
                Address a = staff[i] != null ? staff[i].assignedAddress : null;
                if (a != null && a.streetNumber == number && a.streetName == street)
                {
                    return "reallife_bld_answer_vacant_staff";
                }
            }
            return null;
        }

        /// <summary>
        /// What a building's occupants pay a day: its tenants (the deed's occupancy of its floor area at its rent a square
        /// metre, the owner's income from them), the unit's market rent, and whether a business of the city's trades in it.
        /// </summary>
        internal static void Tenants(string street, int number, out double tenantsDaily, out double unitRentDaily, out bool business)
        {
            tenantsDaily = unitRentDaily = 0.0;
            business = false;
            var at = new Address(street, number);
            RealEstate deed = DeedOf(street, number);
            if (deed != null)
            {
                tenantsDaily = deed.pricePerSqm * deed.totalSqm * (deed.occupancy / 100.0);
            }
            BuildingRegistration reg = string.IsNullOrEmpty(street) ? null : BuildingHelper.GetBuildingRegistration(at);
            Building b = reg != null ? GameRegistrations.BuildingOf(reg) : null;
            if (b != null)
            {
                unitRentDaily = b.GetBuildingDailyMarketRent();
                business = !reg.RentedByPlayer && reg.HasEstablishedBusiness && reg.businessTypeName != EmptyBusiness && !GoingPublic.Foreign(reg);
            }
        }

        /// <summary>
        /// The notice has run: a business of the city's in the unit closed by the game's own call, the unit kept off the market so
        /// no other moves in while the works go on, and the deed's tenants gone. Never another player's business. True when
        /// something was done.
        /// </summary>
        internal static bool Evict(string street, int number)
        {
            bool done = false;
            BuildingRegistration reg = string.IsNullOrEmpty(street) ? null : BuildingHelper.GetBuildingRegistration(new Address(street, number));
            if (reg != null && !reg.RentedByPlayer && !GoingPublic.Foreign(reg))
            {
                if (reg.HasEstablishedBusiness && reg.businessTypeName != EmptyBusiness)
                {
                    reg.ShutDownAIBusiness();   // the game's own close of a business of the city's, its landlord kept
                    done = true;
                }
                if (reg.AvailableForRent)
                {
                    reg.AvailableForRent = false;
                    done = true;
                }
            }
            return HoldEmpty(street, number) || done;
        }

        /// <summary>The deed's tenants kept out while a building's works go on (the game would let its floor area again each day). True when they were not already.</summary>
        internal static bool HoldEmpty(string street, int number)
        {
            RealEstate deed = DeedOf(street, number);
            if (deed == null || deed.occupancy <= 0f)
            {
                return false;
            }
            deed.occupancy = 0f;
            return true;
        }

        /// <summary>The game's building types its structure of a size and version takes (BuildingVersion.supportedBuildingTypes), into the caller's list.</summary>
        internal static void Takes(string size, int version, List<string> into)
        {
            into.Clear();
            BuildingSizeData data = string.IsNullOrEmpty(size) ? null : BuildingSizeHelper.GetData(size);
            BuildingVersion[] versions = data != null ? data.buildingVersions : null;
            for (int i = 0; versions != null && i < versions.Length; i++)
            {
                if (versions[i] != null && versions[i].number == version && versions[i].supportedBuildingTypes != null)
                {
                    into.AddRange(versions[i].supportedBuildingTypes);
                }
            }
        }

        // The player's deed of a building (the game's real estate), or null.
        private static RealEstate DeedOf(string street, int number)
        {
            List<RealEstate> estate = SaveGameManager.Current != null ? SaveGameManager.Current.realEstate : null;
            for (int i = 0; estate != null && i < estate.Count; i++)
            {
                Address a = estate[i] != null ? estate[i].address : null;
                if (a != null && a.streetNumber == number && a.streetName == street)
                {
                    return estate[i];
                }
            }
            return null;
        }
    }
}
