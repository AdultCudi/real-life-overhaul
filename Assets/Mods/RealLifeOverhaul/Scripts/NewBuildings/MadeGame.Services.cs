using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using Blueprints;
using Buildings;
using Entities;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// A made building with one of the game's special services: a copy of the game's own SpecialService (found on its own
    /// building at this load by its asset name) on the made record, so every system of the game that finds a special place
    /// (its list of them by address, the registration's type, the contact's address, the city's controllers) finds this one
    /// as one of its own. The copy is never saved: Building records never are, and the registration it fills is the save's,
    /// which the district save keeps with the district's. A branch keeps the game's business name and with it the game's
    /// contact (Contact.GetContact keys a contact by name and type), whose address stays the head office's. Nothing here
    /// runs a frame: steps of the load, of a stand-down, of the city's unloading, and the game's rare contact event.
    /// </summary>
    internal static partial class MadeGame
    {
        private const string ImportType = "ba:businesstype_importexport";   // the game lists importers by this type and its building type's tag

        private static readonly List<UnityEngine.Object> Served = new List<UnityEngine.Object>(16);   // the services and settings copied at this load, destroyed when the city goes
        private static readonly List<Branch> Branches = new List<Branch>(2);
        private static readonly List<string> Goods = new List<string>(24);

        // A branch's address, its head office's, and the contact key they share.
        private sealed class Branch
        {
            public Address Own;
            public Address Head;
            public string Name;
            public string Type;
        }

        /// <summary>
        /// The game's own building of the special service with that asset name, or null: of the game's special buildings, the
        /// one at the first address by street and number should two carry it, so every machine finds the same.
        /// </summary>
        public static Building HeadOf(string asset)
        {
            Building best = null;
            if (string.IsNullOrEmpty(asset))
            {
                return null;
            }
            foreach (KeyValuePair<Address, Building> pair in BuildingHelper.SpecialServiceBuildings)
            {
                Building b = pair.Value;
                if (b == null || b.SpecialService == null || b.SpecialService.name != asset || MadeStreets.IsMine(b.StreetName))
                {
                    continue;
                }
                if (best == null || string.CompareOrdinal(b.StreetName, best.StreetName) < 0 || (b.StreetName == best.StreetName && b.StreetNumber < best.StreetNumber))
                {
                    best = b;
                }
            }
            return best;
        }

        /// <summary>
        /// The service a declaration's record carries: a copy of the game's own, its name the declaration's (or the game's, a
        /// branch), its job's boss its own, an importer's settings its own with its own goods (those the game knows). The
        /// registration filled as BuildingHelper.GetBuildingRegistration fills a special building's when it is new, empty or an
        /// AI business the game's generator put there before; never where a player holds it (<paramref name="held"/>).
        /// Null, with why, when it takes none.
        /// </summary>
        private static SpecialService Serve(MadeSpec spec, BuildingRegistration registration, bool fresh, out string why, out bool held)
        {
            why = null;
            held = false;
            Building head = HeadOf(spec.Service);
            if (head == null)
            {
                why = "the game has no special service named " + spec.Service + " at this load";
                return null;
            }
            bool players = registration != null && (registration.RentedByPlayer || registration.BuildingOwnedByPlayer);
            string name = spec.ServiceName ?? head.SpecialService.businessName;
            ServiceVerdict verdict = NewBuildingsRules.ServiceOf(players, registration != null ? registration.businessTypeName : null, registration != null ? registration.BusinessName : null,
                head.SpecialService.businessTypeName, name);
            if (verdict == ServiceVerdict.Held || registration == null)
            {
                held = registration != null;
                why = registration == null ? "it has no registration" : "a player holds it, so it is not made " + name;
                return null;
            }
            SpecialService copy = UnityEngine.Object.Instantiate(head.SpecialService);
            copy.name = "RealLife " + spec.Id;
            copy.hideFlags = HideFlags.DontUnloadUnusedAsset;   // the engine's sweep of unused assets would take a copy only a record of the mod's holds
            copy.businessName = name;
            if (copy.playerJob != null && !string.IsNullOrEmpty(copy.playerJob.employerName) && !string.IsNullOrEmpty(spec.ServiceEmployer))
            {
                copy.playerJob.employerName = spec.ServiceEmployer;   // the boss's contact is keyed by name and type: the game's own boss runs the game's own place
            }
            if (spec.ServiceGoods != null)
            {
                if (!(copy.settings is ImportExportSettings theirs))
                {
                    UnityEngine.Object.Destroy(copy);
                    why = "the game's " + spec.Service + " imports nothing, so it has no goods to make its own";
                    return null;
                }
                ImportExportSettings own = UnityEngine.Object.Instantiate(theirs);
                own.name = "RealLife " + spec.Id + " goods";
                own.hideFlags = HideFlags.DontUnloadUnusedAsset;
                own.itemsAvailable = KnownGoods(spec.ServiceGoods);
                own.additionalItemsAvailableIfGameSettingEnabled = new List<string>();
                if (own.itemsAvailable.Count == 0)
                {
                    UnityEngine.Object.Destroy(own);
                    UnityEngine.Object.Destroy(copy);
                    why = "none of its goods is an item the game has";
                    return null;
                }
                copy.settings = own;
                Served.Add(own);
            }
            Served.Add(copy);
            Complete(copy, spec);
            if (verdict == ServiceVerdict.Fill)
            {
                if (!fresh)
                {
                    registration.Reset();   // an AI business or an empty unit of the save's: nothing of it carries over, as when the game shuts one down
                    registration.buildingOwnerRivalId = string.Empty;
                }
                FillAsGame(registration, copy, spec);
            }
            else if (!string.IsNullOrEmpty(copy.layout) && (string.Equals(registration.Layout, head.SpecialService.layout, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(registration.Layout, FloorName(spec), StringComparison.OrdinalIgnoreCase)))
            {
                registration.Layout = copy.layout;   // a registration the save kept names the layout it had: the game's, or the dealership's own of an earlier load, whichever this load stands in
            }
            if (spec.ServiceName == null)
            {
                Branches.Add(new Branch { Own = new Address(spec.Street, spec.Number), Head = head.Address, Name = copy.businessName, Type = copy.businessTypeName });
            }
            return copy;
        }

        // The copy completed for its layout's desks and, for a vehicle store, its dialog and settings (MadeGame.Desks) and its floor made walkable (MadeGame.Floor). A build without
        // one of the game members it reads leaves the copy as the game has it and says so; the place stands either way.
        private static void Complete(SpecialService copy, MadeSpec spec)
        {
            try
            {
                string did = CompleteDesks(copy, spec);
                if (did.Length > 0)
                {
                    Logger?.Info("Made buildings: '" + spec.Id + "' copies " + spec.Service + " completed for its desk and its cars: " + did + ".");
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Made buildings: '" + spec.Id + "' copies " + spec.Service + " as the game has it: its desks and cars could not be completed (" + e.GetType().Name + ").");
            }
            if (!spec.ServiceStore)
            {
                return;
            }
            try
            {
                string floor = CompleteFloor(copy, spec);
                if (floor != null)
                {
                    Logger?.Info("Made buildings: '" + spec.Id + "' copies " + spec.Service + " with its showroom floor measured: " + floor + ".");
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Made buildings: '" + spec.Id + "' copies " + spec.Service + " with its floor as the game has it: its seats could not be measured (" + e.GetType().Name + ").");
            }
        }

        // The goods the game knows, in the declaration's order; a name a later build lacks is left out (Player.log says which).
        private static List<string> KnownGoods(string[] goods)
        {
            Goods.Clear();
            var known = new List<string>(goods.Length);
            for (int i = 0; i < goods.Length; i++)
            {
                if (string.IsNullOrEmpty(goods[i]) || known.Contains(goods[i]))
                {
                    continue;
                }
                if (ItemsGetter.GetByName(goods[i], true) != null)
                {
                    known.Add(goods[i]);
                }
                else
                {
                    Goods.Add(goods[i]);
                }
            }
            if (Goods.Count > 0)
            {
                Logger?.Warn("Made buildings: an importer's goods the game does not have at this load are left out: " + string.Join(", ", Goods) + ".");
                Goods.Clear();
            }
            return known;
        }

        // BuildingHelper.GetBuildingRegistration's own fill of a special building's registration, from the copy.
        private static void FillAsGame(BuildingRegistration reg, SpecialService service, MadeSpec spec)
        {
            reg.BusinessName = service.businessName;
            reg.BusinessDescription = service.businessDescription;
            reg.businessTypeName = service.businessTypeName;
            reg.Layout = service.layout;
            reg.scheduleDays = service.scheduleDays;
            reg.signAppearanceSettings = service.signAppearanceSettings;
            reg.logoSettings = service.logoSettings;
            reg.customerCapacity = CompetitionHelper.GetAiBusinessCustomerCapacity(new BuildingSizeInfo(spec.Size, spec.Version), spec.Type);
            reg.RentedByPlayer = false;
            reg.AvailableForRent = false;
            List<BuildingForSale> sale = SaveGameManager.Current != null ? SaveGameManager.Current.buildingsForSale : null;
            for (int i = sale != null ? sale.Count - 1 : -1; i >= 0; i--)
            {
                if (sale[i] != null && sale[i].address != null && sale[i].address.streetName == spec.Street && sale[i].address.streetNumber == spec.Number)
                {
                    sale.RemoveAt(i);   // a special place is never for sale, as the game's own never are
                }
            }
        }

        /// <summary>
        /// A made record with a service in the game's list of special buildings by address, or out of it again (Enter and
        /// Leave; a district standing down). Every system that finds a special place by kind reads that list: the nearest
        /// clinic, the banks, the medical centre, BizMan's sources.
        /// </summary>
        public static void ServiceListed(Building record, bool listed)
        {
            if (record == null || record.SpecialService == null)
            {
                return;
            }
            Address address = record.Address;
            if (listed)
            {
                BuildingHelper.SpecialServiceBuildings[address] = record;
            }
            else if (BuildingHelper.SpecialServiceBuildings.TryGetValue(address, out Building there) && there == record)
            {
                BuildingHelper.SpecialServiceBuildings.Remove(address);
            }
        }

        /// <summary>
        /// An importer among the game's importers, or out of them: the game keeps its list of them once made
        /// (BuildingHelper.GetImportExportBuildingRegistrations, from its list of all buildings, until the next load), so one
        /// made before the district's buildings were in that list lacks it. Asked once the district's buildings are in the
        /// game's lists. True when the importer is in the list (or, <paramref name="listed"/> false, out of it), and for a
        /// building that is no importer, which has nothing to do there.
        /// </summary>
        public static bool ImporterListed(Building record, BuildingRegistration registration, bool listed)
        {
            if (record == null || registration == null || record.SpecialService == null || record.SpecialService.businessTypeName != ImportType)
            {
                return true;
            }
            List<BuildingRegistration> all = BuildingHelper.GetImportExportBuildingRegistrations();
            if (all == null)
            {
                return false;
            }
            bool there = all.Contains(registration);
            if (listed && !there && BuildingTypeHelper.GetData(record).HasTag(BigAmbitions.Tags.TagRef.Buildingtypetag.canimportexport))
            {
                all.Add(registration);
                there = true;
            }
            else if (!listed && there)
            {
                all.Remove(registration);
                there = false;
            }
            return there == listed;
        }

        /// <summary>
        /// Every branch's contact the head office's again: a contact first met at a branch's counter is made with the branch's
        /// address (Contact.CreateContact), and the game sends every call, loan and dialog of a contact to its address, so a
        /// branch's loans name the head office, as a bank's branch does. The game's own contact event calls it, before the
        /// counter's dialog reads the contact. How many were put back.
        /// </summary>
        public static int Rebranch()
        {
            List<Contact> contacts = SaveGameManager.Current != null ? SaveGameManager.Current.Contacts : null;
            int moved = 0;
            for (int b = 0; contacts != null && b < Branches.Count; b++)
            {
                Branch branch = Branches[b];
                for (int i = 0; i < contacts.Count; i++)
                {
                    Contact c = contacts[i];
                    if (c != null && c.id == branch.Name && c.description == branch.Type && c.streetName == branch.Own.streetName && c.streetNumber == branch.Own.streetNumber)
                    {
                        c.streetName = branch.Head.streetName;
                        c.streetNumber = branch.Head.streetNumber;
                        moved++;
                    }
                }
            }
            return moved;
        }

        /// <summary>True while a made building of this load is a branch of one of the game's own businesses.</summary>
        public static bool HasBranches => Branches.Count > 0;

        /// <summary>The head office's address of the branch at that address; false when it is no branch.</summary>
        public static bool BranchOf(string street, int number, out Address head)
        {
            for (int i = 0; i < Branches.Count; i++)
            {
                if (Branches[i].Own.streetName == street && Branches[i].Own.streetNumber == number)
                {
                    head = Branches[i].Head;
                    return true;
                }
            }
            head = null;
            return false;
        }

        /// <summary>The branch's contact's address, or null while the player has not met it: the head office's when all is well.</summary>
        public static Address BranchContact(string street, int number)
        {
            List<Contact> contacts = SaveGameManager.Current != null ? SaveGameManager.Current.Contacts : null;
            for (int b = 0; contacts != null && b < Branches.Count; b++)
            {
                if (Branches[b].Own.streetName != street || Branches[b].Own.streetNumber != number)
                {
                    continue;
                }
                for (int i = 0; i < contacts.Count; i++)
                {
                    if (contacts[i] != null && contacts[i].id == Branches[b].Name && contacts[i].description == Branches[b].Type)
                    {
                        return contacts[i].Address;
                    }
                }
            }
            return null;
        }

        // The city goes: the copies destroyed with the records that carried them.
        private static void DropServices()
        {
            for (int i = 0; i < Served.Count; i++)
            {
                if (Served[i] != null)
                {
                    UnityEngine.Object.Destroy(Served[i]);
                }
            }
            Served.Clear();
            Branches.Clear();
            try
            {
                DropStock();
            }
            catch (Exception)
            {
                // a build without the game's list of a store's cars made none of them
            }
        }
    }
}
