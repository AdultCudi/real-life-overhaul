using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using Blueprints;
using Buildings;
using BusinessLayoutSets;
using Dialogs;
using Services;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using Vehicles.VehicleTypes;
using LayoutItem = BusinessLayoutSets.Item;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// A made building's special service completed for the desks and the cars its layout has (ServiceDeskRules). The game answers a
    /// click on a special employee's desk with the dialog of the building's SERVICE; a copy of a service that carries none
    /// (Manhattan Luxury Cars, which the dealership of Uptown copies, has no dialog and no vehicle store's settings, as the
    /// game sells its cars off the floor) would leave the game's panel up on nothing with the player seated and the game
    /// paused. So the copy takes the dialog each desk of its layout needs, and a dealership's copy the vehicle store's dialog,
    /// settings and contact category, and its cars go to the game's own list of a store's cars by address and by contact
    /// (Services.ContractItemsForSaleService), the first thing the store's panel asks. Every call is behind a try at its
    /// caller: a build without one of these game members stands the completion down, never the place. Nothing here runs a
    /// frame: a load, and a stand-down.
    /// </summary>
    internal static partial class MadeGame
    {
        private static readonly List<Address> Stocked = new List<Address>(4);       // the addresses whose cars are in the game's list at this load
        private static readonly List<string> StockedContacts = new List<string>(4);  // and the contact keys they were listed under

        /// <summary>
        /// The copy completed for its layout's desks (each desk's service has the dialog its person speaks) and, for a vehicle
        /// store's declaration, the store's own dialog, settings and contact category. Returns what it did, for Player.log; empty
        /// for nothing to do.
        /// </summary>
        private static string CompleteDesks(SpecialService copy, MadeSpec spec)
        {
            string did = string.Empty;
            string employee = null;
            BusinessLayoutSet layout = LayoutOf(copy, spec);
            for (int i = 0; layout != null && layout.Items != null && i < layout.Items.Count; i++)
            {
                LayoutItem item = layout.Items[i];
                if (item == null || !ServiceDeskRules.IsDesk(item.itemName))
                {
                    continue;
                }
                string kind = ServiceDeskRules.EmployeeOf(item.customValue);
                employee = employee ?? (string.IsNullOrEmpty(kind) ? null : kind);
                string want = ServiceDeskRules.Complete(kind, copy.dialogType.ToString());
                if (want != null && Enum.TryParse(want, out CallDialogType type) && type != CallDialogType.NotImplemented)
                {
                    copy.dialogType = type;
                    did += (did.Length > 0 ? ", " : string.Empty) + "its " + kind + " desk answers with " + want;
                }
            }
            if (spec.ServiceStore || ServiceDeskRules.NeedsStore(employee))
            {
                did += Store(copy, spec);
            }
            return did;
        }

        // The layout of the service for the building's structure, or null.
        private static BusinessLayoutSet LayoutOf(SpecialService service, MadeSpec spec)
        {
            return string.IsNullOrEmpty(service.layout) ? null
                : BusinessLayoutSetHelper.GetOrLoadBusinessLayoutSet(service.businessTypeName, new BuildingSizeInfo(spec.Size, spec.Version), service.layout, false);
        }

        // A vehicle store's dialog and settings on the copy; what it changed, for the log.
        private static string Store(SpecialService copy, MadeSpec spec)
        {
            string did = string.Empty;
            if (copy.dialogType != CallDialogType.VehicleStoreDialog)
            {
                did += (copy.dialogType == CallDialogType.NotImplemented ? "no dialog" : "the dialog " + copy.dialogType) + " made the vehicle store's";
                copy.dialogType = CallDialogType.VehicleStoreDialog;
            }
            if (!(copy.settings is VehicleStoreSettings))
            {
                var own = ScriptableObject.CreateInstance<VehicleStoreSettings>();
                own.name = "RealLife " + spec.Id + " store";
                own.hideFlags = HideFlags.DontUnloadUnusedAsset;
                own.npcSpawners = copy.settings != null ? copy.settings.npcSpawners : new NpcSpawnSettings[0];   // the staff and customers it had
                own.canDeliver = false;   // the floor's panel stays as it was (no delivery offered by a car on the floor); the desk's panel delivers for its fee
                Served.Add(own);
                copy.settings = own;
                did += (did.Length > 0 ? ", " : string.Empty) + "the settings made a vehicle store's";
            }
            if (copy.contactCategory != ContactCategoryName.FurnitureAndEquipment)
            {
                copy.contactCategory = ContactCategoryName.FurnitureAndEquipment;   // the game's delivery of a bought car looks the store's contact up under it (VehicleDeliveryHelper.DeliverVehicle)
                did += (did.Length > 0 ? ", " : string.Empty) + "its contact filed as a store's";
            }
            copy.isBusinessContact = true;
            return did.Length > 0 ? "it " + did : string.Empty;
        }

        /// <summary>
        /// The vehicle types a layout's showcases hold, in the layout's order, each once, that the game has: the cars of a
        /// dealership's floor. Into <paramref name="into"/> (not cleared). How many.
        /// </summary>
        public static int FloorCars(BusinessLayoutSet layout, List<string> into)
        {
            var raw = new List<string>(16);
            for (int i = 0; layout != null && layout.Items != null && i < layout.Items.Count; i++)
            {
                LayoutItem item = layout.Items[i];
                PlayerItemPurchaserSettings sold = item != null ? item.playerItemPurchaserSettings : null;
                if (sold == null || !sold.enabled || string.IsNullOrEmpty(sold.itemName))
                {
                    continue;
                }
                BigAmbitions.Items.Item game = ItemsGetter.GetByName(sold.itemName, true);
                if (game != null && !string.IsNullOrEmpty(game.vehicleType))
                {
                    raw.Add(game.vehicleType);
                }
            }
            return StockRules.Distinct(raw, name => VehicleTypeHelper.GetVehicleType(name) != null, into);
        }

        /// <summary>
        /// A dealership's cars into the game's own list of a vehicle store's, by its address and by its contact's key, so the
        /// store's panel finds them first. Taken out again with <see cref="Unstock"/>. True when the list holds them.
        /// </summary>
        public static bool Stock(Address address, string contact, List<string> cars)
        {
            if (address == null || cars == null || cars.Count == 0)
            {
                return false;
            }
            ContractItemsForSaleService.SetVehiclesForAddress(address, cars);
            if (!string.IsNullOrEmpty(contact))
            {
                ContractItemsForSaleService.SetVehiclesForContact(contact, cars);
            }
            if (!Stocked.Contains(address))
            {
                Stocked.Add(address);
                StockedContacts.Add(contact ?? string.Empty);
            }
            return ContractItemsForSaleService.TryGetVehiclesForAddress(address, out List<string> back) && back.Count == cars.Count;
        }

        /// <summary>The cars the game's list holds for an address (the panel's own first look), 0 for none.</summary>
        public static int Listed(Address address)
        {
            return address != null && ContractItemsForSaleService.TryGetVehiclesForAddress(address, out List<string> names) ? names.Count : 0;
        }

        /// <summary>A dealership's cars out of the game's list again (it stands down, or the city goes).</summary>
        public static void Unstock(Address address)
        {
            int at = address != null ? Stocked.IndexOf(address) : -1;
            if (at < 0)
            {
                return;
            }
            string contact = StockedContacts[at];
            Stocked.RemoveAt(at);
            StockedContacts.RemoveAt(at);
            ContractItemsForSaleService.RemoveAddress(address);
            if (!string.IsNullOrEmpty(contact))
            {
                ContractItemsForSaleService.RemoveContact(contact);
            }
        }

        // The city goes: every list of cars this load made taken out of the game's, which keeps them for the run.
        private static void DropStock()
        {
            while (Stocked.Count > 0)
            {
                Unstock(Stocked[Stocked.Count - 1]);
            }
        }
    }
}
