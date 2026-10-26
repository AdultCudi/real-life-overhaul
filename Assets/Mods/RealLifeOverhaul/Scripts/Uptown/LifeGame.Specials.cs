using System.Collections.Generic;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Dialogs;
using Helpers;
using Localizor;
using RealLifeOverhaul.NewBuildings;
using Streets;
using UI;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.UI;
using LayoutItem = BusinessLayoutSets.Item;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's special places on the game's side (SpecialsRules): a place's declaration given its service, the place made
    /// whole once the district's buildings are in the game's lists (an importer among the game's importers, a dealership's
    /// spot at the kerb where a car bought on its floor stands), and what its census reads: its service, its registration,
    /// the game's lists, its layout and its staff, its row on the map, a branch's contact. Nothing here runs a frame: once
    /// at a load for eleven buildings, and on a test's press.
    /// </summary>
    internal static partial class LifeGame
    {
        private const string KerbName = "RealLife kerb spot";   // the object a dealership's bought car stands at
        private const float KerbOut = 1.9f;      // m from the door to the street: a car's middle on the pavement, a metre clear of its kerb and of the wall
        private const float KerbAside = 5f;      // m along the front from the door, so the car stands clear of it
        private const string DeskItem = "ba:itemname_specialemployeedesk";   // the game's special employee's desk
        private const string TillItem = "ba:itemname_checkoutcounter";       // the start of the game's checkout counters' names
        private const string ShowcaseEnd = "showcase";                        // the end of the game's showcase vehicles' names

        /// <summary>A place's declaration given the game's service its table copies (the made buildings' engine attaches it).</summary>
        public static void Serve(MadeSpec spec, SpecialPlace place)
        {
            spec.Service = place.Service;
            spec.ServiceName = place.Name;
            spec.ServiceEmployer = place.Employer;
            spec.ServiceGoods = place.Goods;
            spec.PlainType = place.PlainType;
            spec.ServiceStore = place.Staff == SpecialStaff.Showroom;   // a dealership's copy is a vehicle store's, so its desk and its cars' panel have what the game's read
        }

        /// <summary>
        /// A place made whole once the district's buildings are in the game's lists: an importer among the game's importers, a
        /// dealership's kerb spot. Null when it is the place, else why not.
        /// </summary>
        public static string Special(MadeBuilding made, SpecialPlace place)
        {
            Building record = made != null ? made.Building : null;
            if (record == null || made.Registration == null)
            {
                return "its records are not there";
            }
            if (record.SpecialService == null)
            {
                return "it stands as an ordinary building (a player holds it, or the game lacks the service " + place.Service + ": Player.log says which)";
            }
            if (!MadeGame.ImporterListed(record, made.Registration, true))
            {
                return "it is not among the game's importers";
            }
            if (place.Staff == SpecialStaff.Showroom && !Kerb(made))
            {
                return "it has no spot at the kerb for a car bought on its floor";
            }
            return place.Staff == SpecialStaff.Showroom ? Stock(made) : null;
        }

        // A dealership's cars in the game's list of a vehicle store's (the panel of its desk asks that list first), from the showcases of
        // its layout, by its address and its contact's key. Null when listed, else why not.
        private static string Stock(MadeBuilding made)
        {
            try
            {
                Building record = made.Building;
                BuildingRegistration reg = made.Registration;
                BusinessLayoutSet layout = string.IsNullOrEmpty(reg.Layout) ? null
                    : BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(reg.businessTypeName, new BuildingSizeInfo(record), reg.Layout, warnIfNotFound: false);
                var cars = new List<string>(12);
                if (MadeGame.FloorCars(layout, cars) == 0)
                {
                    return "its layout has no showcase of a car the game has, so its purchase panel would list none";
                }
                return MadeGame.Stock(record.Address, reg.BusinessName, cars) ? null : "the game's list of a vehicle store's cars did not take its cars";
            }
            catch (System.Exception e)
            {
                return "the game's list of a vehicle store's cars could not be reached (" + e.GetType().Name + ")";
            }
        }

        // A car bought on a showroom's floor stands at the building's first custom position (ShowcaseVehicleController.Purchase),
        // which a copy of an ordinary building has none of: one on the pavement in front, along the street, clear of the door.
        private static bool Kerb(MadeBuilding made)
        {
            CityBuildingController c = made.Controller;
            if (c == null)
            {
                return false;
            }
            if (c.customPositions == null)
            {
                c.customPositions = new List<Transform>(1);
            }
            if (c.customPositions.Count > 0 && c.customPositions[0] != null && c.customPositions[0].name == KerbName)
            {
                return true;
            }
            float f = made.DoorFacing * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f)), along = new Vector3(Mathf.Cos(f), 0f, -Mathf.Sin(f));
            var spot = new GameObject(KerbName).transform;
            spot.SetParent(c.transform, false);
            spot.position = new Vector3(made.DoorX, made.DoorY, made.DoorZ) + outward * KerbOut + along * KerbAside;
            spot.rotation = Quaternion.Euler(0f, made.DoorFacing + 90f, 0f);
            c.customPositions.Insert(0, spot);
            return true;
        }

        /// <summary>
        /// Null when a standing place is all the game's own special place of its kind is: its record carries the game's service
        /// of its kind; its registration is the service's (its type and name, not to let, nobody's); it is in the game's list
        /// of special buildings (an importer among its importers too); the game finds its layout for its structure; that layout
        /// has what its staff work at; the map's filters have a row for its type; a branch's contact, once met, is the head
        /// office's; a dealership has its kerb spot; a clinic its spot for a player who faints. Else why not.
        /// </summary>
        public static string SpecialWhy(MadeBuilding made, SpecialPlace place)
        {
            Building record = made != null ? made.Building : null;
            BuildingRegistration reg = made != null ? made.Registration : null;
            if (record == null || reg == null)
            {
                return "it does not stand";
            }
            SpecialService service = record.SpecialService;
            if (service == null || service.businessTypeName != place.ServiceType)
            {
                return "its record carries no service of " + place.ServiceType;
            }
            if (reg.businessTypeName != place.ServiceType || reg.BusinessName != service.businessName || reg.AvailableForRent || reg.RentedByPlayer || reg.BuildingOwnedByPlayer)
            {
                return "its registration is " + (reg.businessTypeName ?? "(none)") + " named " + (reg.BusinessName ?? "(none)") + (reg.AvailableForRent ? ", to let" : string.Empty);
            }
            if (!BuildingHelper.SpecialServiceBuildings.TryGetValue(record.Address, out Building listed) || listed != record)
            {
                return "it is not in the game's list of special buildings";
            }
            if (place.ServiceType == SpecialsRules.ImportType && !BuildingHelper.GetImportExportBuildingRegistrations().Contains(reg))
            {
                return "it is not among the game's importers";
            }
            BusinessLayoutSet layout = string.IsNullOrEmpty(reg.Layout) ? null
                : BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(reg.businessTypeName, new BuildingSizeInfo(record), reg.Layout, warnIfNotFound: false);
            if (layout == null)
            {
                return "the game finds no layout " + (reg.Layout ?? "(none)") + " for " + NewBuildingsRules.StructureName(record.BuildingSize, record.BuildingVersion);
            }
            if (!Staffed(layout, place.Staff))
            {
                return "its layout has nothing its staff work at (" + place.Staff + ")";
            }
            UIs uis = InstanceBehavior<UIs>.IsInitialized ? InstanceBehavior<UIs>.Instance : null;
            var toggles = uis != null && uis.mapFilters != null ? GameContract.FilterToggles.Field?.GetValue(uis.mapFilters) as Dictionary<string, Toggle> : null;
            if (toggles != null && toggles.Count > 0 && !toggles.ContainsKey(place.ServiceType.ToLowerInvariant()))
            {
                return "the map's filters have no row for " + place.ServiceType;
            }
            if (place.Branch)
            {
                if (!MadeGame.BranchOf(record.StreetName, record.StreetNumber, out Address head) || head == null)
                {
                    return "it is not known as a branch";
                }
                Address contact = MadeGame.BranchContact(record.StreetName, record.StreetNumber);
                if (contact != null && !contact.Equals(head))
                {
                    return "its contact's address is " + contact.ToFormattedString() + ", not the head office's " + head.ToFormattedString();
                }
            }
            string desks = DesksWhy(layout, service);
            if (desks != null)
            {
                return desks;
            }
            if (place.Staff == SpecialStaff.Showroom && (made.Controller == null || made.Controller.customPositions == null || made.Controller.customPositions.Count == 0))
            {
                return "it has no spot at the kerb for a car bought on its floor";
            }
            if (place.Staff == SpecialStaff.Showroom)
            {
                string store = StoreWhy(made, service);
                if (store != null)
                {
                    return store;
                }
            }
            if (place.ServiceType == "ba:businesstype_clinic" && !service.hasRecoverySpawnPoint)
            {
                return "the game's clinic it copies has no spot for a player who faints";
            }
            return null;
        }

        // Null when every desk of the layout has the dialog its person speaks in the service (a desk on a service with none opens the game's panel
        // on nothing, seats the player and pauses the game), else why not.
        private static string DesksWhy(BusinessLayoutSet layout, SpecialService service)
        {
            for (int i = 0; layout != null && layout.Items != null && i < layout.Items.Count; i++)
            {
                LayoutItem item = layout.Items[i];
                if (item == null || !ServiceDeskRules.IsDesk(item.itemName))
                {
                    continue;
                }
                string kind = ServiceDeskRules.EmployeeOf(item.customValue), dialog = service.dialogType.ToString();
                DeskVerdict verdict = ServiceDeskRules.Verdict(kind, dialog);
                if (verdict != DeskVerdict.Fine)
                {
                    return ServiceDeskRules.Why(kind, dialog, verdict);
                }
            }
            return null;
        }

        // Null when a dealership has all its vehicle store's panel reads: the store's dialog, its settings, its contact under the category
        // the game's delivery looks up, and its cars in the game's list. A small method of its own: a build without one of those game members
        // fails it alone.
        private static string StoreWhy(MadeBuilding made, SpecialService service)
        {
            try
            {
                if (service.dialogType != CallDialogType.VehicleStoreDialog || !(service.settings is VehicleStoreSettings))
                {
                    return "its service has not the vehicle store's dialog and settings its desk's panel reads";
                }
                if (service.contactCategory != ContactCategoryName.FurnitureAndEquipment)
                {
                    return "its contact is not filed as a store's, where the game's delivery of a bought car looks it up";
                }
                int listed = MadeGame.Listed(made.Building.Address);
                return listed > 0 ? null : "the game's list of a vehicle store's cars holds none of its cars";
            }
            catch (System.Exception e)
            {
                return "the game's vehicle store could not be read (" + e.GetType().Name + ")";
            }
        }

        // True when the layout has what the place's staff work at.
        private static bool Staffed(BusinessLayoutSet layout, SpecialStaff staff)
        {
            if (staff == SpecialStaff.None)
            {
                return true;
            }
            for (int i = 0; layout.Items != null && i < layout.Items.Count; i++)
            {
                string item = layout.Items[i] != null ? layout.Items[i].itemName : null;
                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }
                if ((staff == SpecialStaff.Desk && item == DeskItem) || (staff == SpecialStaff.Till && item.StartsWith(TillItem, System.StringComparison.Ordinal))
                    || (staff == SpecialStaff.Showroom && item.EndsWith(ShowcaseEnd, System.StringComparison.Ordinal)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A place's name as the game shows it, and its kind in the player's language (the game names a business type by its id).</summary>
        public static string SpecialName(MadeBuilding made, SpecialPlace place, out string kind)
        {
            string type = place.ServiceType;
            string named = type.GetLocalization();
            kind = string.IsNullOrEmpty(named) ? type : named;
            BuildingRegistration reg = made != null ? made.Registration : null;
            return reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : place.Name ?? place.Service;
        }
    }
}
