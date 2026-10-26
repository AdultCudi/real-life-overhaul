using System;
using System.Collections.Generic;
using BigAmbitions.Tags;
using Buildings;
using Entities;
using Helpers;
using Localizor;
using RealLifeOverhaul.NewBuildings;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The game's side of the whole-building check (LifeDirector.Complete): what the game holds of one district building, read
    /// into CompleteFacts, and the owner given to a city's building BizMan would call "Undefined". Nothing here runs a frame:
    /// asked on a stand and once a load.
    /// </summary>
    internal static partial class LifeGame
    {
        private const string Government = "isfromgovernment";   // the business type's tag (TagRef.Businesstag.isfromgovernment)
        private const float DoorReach = 1.5f;                     // m from the door to the walking mesh: a person reaches it

        /// <summary>What the game holds of a made building, for the check.</summary>
        public static CompleteFacts CompleteFacts(MadeBuilding made, bool playerRecord, string recordType, string recordSize, int planNumber)
        {
            BuildingRegistration reg = made.Registration;
            Building record = made.Building;
            GameInstance save = SaveGameManager.Current;
            var f = new CompleteFacts
            {
                Street = made.Spec.Street, Number = made.Spec.Number, PlanNumber = planNumber, RecordType = recordType, RecordSize = recordSize, PlayerRecord = playerRecord,
                DistrictStreet = made.Spec.Street != null && made.Spec.Street.StartsWith(CompleteRules.StreetPrefix, StringComparison.Ordinal)
            };
            if (reg != null)
            {
                f.Street = reg.StreetName;
                f.Number = reg.StreetNumber;
                f.BusinessName = reg.BusinessName;
                f.HasBusiness = !string.IsNullOrEmpty(reg.BusinessName) && reg.businessTypeName != "ba:businesstype_empty";
                f.DisplayName = reg.GetDisplayName();
                f.RivalOwner = reg.buildingOwnerRivalId;
                f.ForRent = reg.AvailableForRent;
                f.RentedByPlayer = reg.RentedByPlayer;
                f.ScheduleDays = reg.scheduleDays != null ? reg.scheduleDays.Count : 0;
                f.Government = GovernmentType(reg);
            }
            if (record != null)
            {
                f.Type = record.BuildingType;
                f.Size = record.BuildingSize;
                f.TypeWords = !string.IsNullOrEmpty(record.BuildingType) && LocalizorManager.IsLocalizedKey(record.BuildingType);
            }
            f.AtAddress = 0;
            for (int i = 0; save != null && save.BuildingRegistrations != null && i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration other = save.BuildingRegistrations[i];
                if (other != null && other.StreetNumber == f.Number && other.StreetName == f.Street)
                {
                    f.AtAddress++;
                    f.InRegistrations |= ReferenceEquals(other, reg);
                }
            }
            f.InLookup = MadeGame.InLookup(made, out _);
            if (!f.InRegistrations && reg != null && f.InLookup)
            {
                f.AtAddress = Math.Max(1, f.AtAddress);   // a building kept in the game's lookup alone counts once
                f.InRegistrations = f.AtAddress == 1;
            }
            RealEstate deed = Deed(made);
            f.PlayerDeed = deed != null;
            f.Occupancy = deed != null && deed.occupancy > 0f ? 1 : 0;
            f.OnSale = OnSale(save, record != null ? record.Address : null);
            f.InCityList = InCityList(made.Controller);
            f.DoorOnMesh = NavMesh.SamplePosition(new Vector3(made.DoorX, made.DoorY, made.DoorZ), out NavMeshHit _, DoorReach, NavMesh.AllAreas);
            return f;
        }

        // The game's government business types (City Hall, the school's): BizMan writes the government as their owner.
        private static bool GovernmentType(BuildingRegistration reg)
        {
            try
            {
                return BusinessTypeHelper.GetData(reg).HasTag(TagRef.Businesstag.isfromgovernment);
            }
            catch (Exception)
            {
                return false;   // a type the game does not know: not the government's
            }
        }

        private static bool OnSale(GameInstance save, Address address)
        {
            for (int i = 0; address != null && save != null && save.buildingsForSale != null && i < save.buildingsForSale.Count; i++)
            {
                if (save.buildingsForSale[i] != null && save.buildingsForSale[i].address == address)
                {
                    return true;
                }
            }
            return false;
        }

        // Among the city's list of buildings: the array the map's filters and its search read (CityMapFilters).
        private static bool InCityList(CityBuildingController controller)
        {
            CityBuildingController[] all = GameDlc.Open(InstanceBehavior<CityManager>.Instance);   // a district building is never a DLC's
            for (int i = 0; controller != null && all != null && i < all.Length; i++)
            {
                if (ReferenceEquals(all[i], controller))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A city's building that is not the player's, not the government's, has no rival owner and is not for sale is given a rival
        /// owner, as the game's own generator gives every building of its districts one: of the rivals owning buildings in this save,
        /// the one its address picks (CompleteRules.PickOwner). None when no rival owns anything.
        /// </summary>
        public static void MendOwner(MadeBuilding made, List<string> rivals)
        {
            BuildingRegistration reg = made.Registration;
            GameInstance save = SaveGameManager.Current;
            if (reg == null || save == null || !string.IsNullOrEmpty(reg.buildingOwnerRivalId) || Deed(made) != null || GovernmentType(reg)
                || OnSale(save, made.Building != null ? made.Building.Address : null) || GoingPublic.Foreign(reg))
            {
                return;
            }
            CompleteRules.Rivals(Owners(save), rivals);
            string pick = CompleteRules.PickOwner(rivals, reg.StreetName, reg.StreetNumber);
            if (pick != null)
            {
                reg.buildingOwnerRivalId = pick;
                LifeDirector.Logger?.Info("Uptown: the city's building at " + made.Address + " had no owner the game could name; its owner is now rival " + pick
                    + ", as the game's generator gives its buildings.");
            }
        }

        private static IEnumerable<string> Owners(GameInstance save)
        {
            for (int i = 0; save.BuildingRegistrations != null && i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration r = save.BuildingRegistrations[i];
                if (r != null && !string.IsNullOrEmpty(r.buildingOwnerRivalId))
                {
                    yield return r.buildingOwnerRivalId;
                }
            }
        }

        /// <summary>The notice that a building is not whole yet: how many, and that Player.log names the items.</summary>
        public static void SayIncomplete(int count)
        {
            Notifications.Show(NotificationType.Warning, LifeText.Incomplete, new Dictionary<string, string> { { "count", count.ToString(System.Globalization.CultureInfo.InvariantCulture) } }, 10f);
        }
    }
}
