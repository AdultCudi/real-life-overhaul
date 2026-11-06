using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using BigAmbitions.Items;
using Buildings;
using Entities;
using Helpers;
using Localizor;
using Player.FoodDeliveryJob;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The made buildings' side of the game, the only place they touch it. A made building is a Building record made with
    /// ScriptableObject.CreateInstance on one of the game's own sizes, versions, types and districts, at an address on a
    /// street of the mod's own; it goes into the game's private lookup of buildings by address (what BuildingHelper.GetBuilding
    /// and everything after it reads) with a registration kept only in the private lookup of registrations, never in the
    /// save's list and never in allBuildings, so the city's generator, the rivals, the contacts and the taxes never see it.
    /// Both lookups are emptied by the game at every load, before the scenes; the mod's are put in again behind the loading
    /// screen. The street names go into the game's list of streets the same way. Nothing here runs a frame: these are
    /// steps of the load, of a save, of the tests and of the city's unloading.
    /// </summary>
    internal static partial class MadeGame
    {
        private const string Warehouse = "ba:buildingtype_warehouse";   // the game makes a Warehouse registration for these (BuildingHelper.GetBuildingRegistration)

        public static IModLogger Logger;

        private static readonly Dictionary<string, StreetData> Streets = new Dictionary<string, StreetData>(4, StringComparer.Ordinal);
        private static readonly List<string> Claimed = new List<string>(8);
        private static readonly List<Building> Records = new List<Building>(8);   // the records made at this load, destroyed when the city goes
        private static Address[] _deliveryWas;   // the food deliveries' own list of addresses left out, put back when the city goes
        private static FoodDeliveryJobConfig _deliveryConfig;

        // ---------------------------------------------------------------- the game's own limits, read now

        /// <summary>
        /// True when the game has a structure of that size and version for that type, not one kept for its special buildings.
        /// The special type takes any version the game has of a size: the game stands its own special buildings on ordinary
        /// structures too (its importers on the warehouses' H1), and the type is only made with a service attached (NewBuildingsRules.ServiceRefusal).
        /// </summary>
        public static bool StructureTakes(string size, int version, string type)
        {
            if (string.IsNullOrEmpty(size) || string.IsNullOrEmpty(type))
            {
                return false;
            }
            List<BuildingSizeData> sizes = BuildingSizeHelper.GetAllBuildingSizes();
            for (int i = 0; sizes != null && i < sizes.Count; i++)
            {
                BuildingSizeData data = sizes[i];
                if (data == null || data.buildingSize != size || data.buildingVersions == null)
                {
                    continue;
                }
                for (int v = 0; v < data.buildingVersions.Length; v++)
                {
                    BuildingVersion each = data.buildingVersions[v];
                    if (each != null && each.number == version
                        && (type == NewBuildingsRules.SpecialType || (!each.specialBuildingOnly && each.supportedBuildingTypes != null && each.supportedBuildingTypes.Contains(type))))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>True when the game has that district and it is in the loaded city (Core/CityCheck).</summary>
        public static bool DistrictKnown(string district)
        {
            List<string> all = NeighborhoodHelper.Neighborhoods;
            return !string.IsNullOrEmpty(district) && all != null && all.Contains(district) && CityCheck.Here(district);
        }

        /// <summary>True when a made home can be put on the food deliveries' list of addresses they leave out (the contract's field is there).</summary>
        public static bool HomesLeftOut()
        {
            return GameContract.DeliveryLeftOut.Field != null && DeliveryConfig() != null;
        }

        private static FoodDeliveryJobConfig DeliveryConfig()
        {
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            return references != null ? references.foodDeliveryJobConfig : null;
        }

        /// <summary>True when the game (or another mod) already has a building at that address: the made one then stands down.</summary>
        public static bool GameHas(string street, int number)
        {
            return BuildingHelper.GetBuilding(new Address(street, number)) != null;
        }

        /// <summary>The size's floor area as the game has it, or 0.</summary>
        public static int SquareMetres(string size)
        {
            BuildingSizeData data = BuildingSizeHelper.GetData(size);
            return data != null ? data.squareMeters : 0;
        }

        // ---------------------------------------------------------------- the streets

        /// <summary>The mod's streets among the game's own, so it shows their names (its list is emptied at every load); how many were put in.</summary>
        public static int AddStreets()
        {
            if (!(GameContract.StreetLookup.Field?.GetValue(null) is Dictionary<string, StreetData> lookup))
            {
                return 0;
            }
            int added = 0;
            for (int i = 0; i < MadeStreets.All.Length; i++)
            {
                MadeStreet street = MadeStreets.All[i];
                if (lookup.ContainsKey(street.Id))
                {
                    continue;
                }
                if (!Streets.TryGetValue(street.Id, out StreetData data) || data == null)
                {
                    data = ScriptableObject.CreateInstance<StreetData>();
                    data.name = "RealLife Street " + street.Abbreviation;
                    data.hideFlags = HideFlags.DontUnloadUnusedAsset;   // the engine's sweep of unused assets would take a record only the mod holds
                    Streets[street.Id] = data;
                }
                data.streetName = street.Id;
                data.helpSystemAbbreviation = street.Abbreviation;
                data.isAvenue = street.Avenue;
                data.streetLocalization = Named(street);
                lookup[street.Id] = data;
                added++;
            }
            return added;
        }

        // A street's name in the language of the load (the game's own street names are fixed words in every language too);
        // its id where the mod's texts are not there.
        private static string Named(MadeStreet street)
        {
            string name = street.NameKey.GetLocalization();
            return string.IsNullOrEmpty(name) || name == street.NameKey ? street.Id : name;
        }

        /// <summary>A street's name as the map and the addresses write it: a street of the mod's own by its own name, any other by the game's street text. An id where no text is there.</summary>
        public static string StreetName(string street)
        {
            MadeStreet mine = MadeStreets.Find(street);
            return mine != null ? Named(mine) : AddressHelper.GetStreetNameLocalized(street);
        }

        /// <summary>An address as the game writes one ("1 Proving Lane"); a street of the mod's own by its own name, whether or not it is among the game's yet.</summary>
        public static string Written(string street, int number)
        {
            MadeStreet mine = MadeStreets.Find(street);
            return mine != null ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Named(mine) : new Address(street, number).ToFormattedString();
        }

        // ---------------------------------------------------------------- a made building in the game's lookups

        /// <summary>
        /// The game's record of a made building, made now and never saved (its fields are all the game reads). Its floor area
        /// is its size's times its storeys, or with <see cref="MadeSpec.DesignArea"/> <paramref name="designArea"/>, that of the
        /// game's building its design copies (a district's building is as large as it looks, as the game's are). A declaration
        /// with a service carries a copy of the game's own, its registration filled as the game fills a special building's
        /// (<see cref="Serve"/>), or none where a player holds the lot. Null, with why, when it cannot stand at all: a service
        /// of the special type it may not take and no ordinary type to stand as.
        /// </summary>
        public static Building MakeRecord(MadeSpec spec, int designArea, BuildingRegistration registration, bool fresh, out string why)
        {
            why = null;
            SpecialService service = null;
            string type = spec.Type;
            if (!string.IsNullOrEmpty(spec.Service))
            {
                service = Serve(spec, registration, fresh, out why, out bool held);
                if (service == null)
                {
                    // Without its service a special-type place stands only where a player holds it, as the ordinary building
                    // it was, its registration the save's of that type; without the game's service at all it stands down.
                    if (spec.Type == NewBuildingsRules.SpecialType && (!held || string.IsNullOrEmpty(spec.PlainType)))
                    {
                        return null;
                    }
                    type = spec.Type == NewBuildingsRules.SpecialType ? spec.PlainType : spec.Type;
                    Logger?.Info("Made buildings: '" + spec.Id + "' stands as an ordinary building of " + type + ": " + why + ".");
                    why = null;
                }
            }
            Building b = ScriptableObject.CreateInstance<Building>();
            b.name = "RealLife Made " + spec.Id;
            b.StreetName = spec.Street;
            b.StreetNumber = spec.Number;
            b.BuildingSize = spec.Size;
            b.BuildingVersion = spec.Version;
            b.BuildingType = type;
            b.SpecialService = service;
            b.Neighbourhood = spec.District;
            b.trafficIndex = spec.Traffic;
            b.totalSqm = spec.DesignArea && designArea > 0 ? designArea : SquareMetres(spec.Size) * Math.Max(1, spec.Floors);   // the floor area the floors core divides by one storey's
            // Its DLC stays the default, none: every player may enter it, and Core/GameDlc never takes it for gated.
            Records.Add(b);
            return b;
        }

        /// <summary>
        /// Its registration as the game would make one (BuildingHelper.GetBuildingRegistration): empty, not to let, not for
        /// sale, closed, no business, so a click on its door gives the game's own "business closed" and no delivery picks it.
        /// A district's building (<see cref="MadeSpec.Life"/>) takes the save's own instead: the one in the save's list, else the
        /// one the district save keeps for its address (that very object goes back into the list at its city step), else a new
        /// one put in the save's list as the game's own lookup would; <paramref name="made"/> says it is new at this load.
        /// </summary>
        public static BuildingRegistration MakeRegistration(MadeSpec spec, out bool made)
        {
            made = false;
            if (spec.Life)
            {
                BuildingRegistration own = InSave(spec.Street, spec.Number) ?? DistrictSave.DistrictSaveDirector.KeptRegistration(spec.Street, spec.Number);
                if (own != null)
                {
                    return own;
                }
            }
            BuildingRegistration reg = spec.Type == Warehouse ? new Warehouse() : new BuildingRegistration();
            reg.StreetName = spec.Street;
            reg.StreetNumber = spec.Number;
            if (!spec.Life)
            {
                reg.AvailableForRent = false;
                reg.RentedByPlayer = false;
                return reg;
            }
            made = true;
            SaveGameManager.Current?.BuildingRegistrations?.Add(reg);   // as BuildingHelper.GetBuildingRegistration adds one it makes
            return reg;
        }

        /// <summary>A district's building that could not stand after all: the registration made for it at this load out of the save's list again.</summary>
        public static void Forsake(BuildingRegistration registration, bool made)
        {
            if (made && registration != null)
            {
                SaveGameManager.Current?.BuildingRegistrations?.Remove(registration);
            }
        }

        /// <summary>The registration of the save's list at that address, or null.</summary>
        private static BuildingRegistration InSave(string street, int number)
        {
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].StreetNumber == number && all[i].StreetName == street)
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Into the game's private lookups: the registration first, then the record, so the game's lookup of a registration
        /// finds it before anything can ask (one that found the record alone would add a registration to the save).
        /// </summary>
        public static bool Enter(Building record, BuildingRegistration registration, out string why)
        {
            why = null;
            var registrations = GameContract.RegistrationLookup.Field?.GetValue(null) as Dictionary<Address, BuildingRegistration>;
            var buildings = GameContract.BuildingLookup.Field?.GetValue(null) as Dictionary<Address, Building>;
            if (registrations == null || buildings == null)
            {
                why = "the game's lookups of buildings could not be reached (" + (GameContract.OwnBuildings.Why ?? "not found") + ")";
                return false;
            }
            Address address = record.Address;
            registrations[address] = registration;
            buildings[address] = record;
            ServiceListed(record, true);
            return true;
        }

        /// <summary>Out of the game's private lookups again: a made building whose copy could not be made stands down whole.</summary>
        public static void Leave(Building record)
        {
            var registrations = GameContract.RegistrationLookup.Field?.GetValue(null) as Dictionary<Address, BuildingRegistration>;
            var buildings = GameContract.BuildingLookup.Field?.GetValue(null) as Dictionary<Address, Building>;
            Address address = record.Address;
            if (buildings != null && buildings.TryGetValue(address, out Building there) && there == record)
            {
                buildings.Remove(address);
                registrations?.Remove(address);
            }
            ServiceListed(record, false);
        }

        /// <summary>True when the game's lookup finds the made record at its address, and nothing in the save's list stands for it.</summary>
        public static bool InLookup(MadeBuilding made, out bool inSave)
        {
            Address address = new Address(made.Spec.Street, made.Spec.Number);
            inSave = InSaveList(made.Spec.Street, made.Spec.Number) > 0;
            return BuildingHelper.GetBuilding(address) == made.Building;
        }

        /// <summary>How many registrations of the save's list are at that address (none, for a made building).</summary>
        public static int InSaveList(string street, int number)
        {
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            int found = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                found += all[i] != null && all[i].StreetNumber == number && all[i].StreetName == street ? 1 : 0;
            }
            return found;
        }

        /// <summary>True when the city's list of its buildings has the made copy and its own lookup finds it by its address.</summary>
        public static bool InCityList(MadeBuilding made, out int listed)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = GameDlc.Open(city);   // a made building is never gated, so it is in here if the game listed it
            listed = all != null ? all.Length + GameDlc.GatedInCity : 0;   // the game's own count, as its closest-building job is made from
            bool inList = false;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                inList |= all[i] == made.Controller;
            }
            return inList && city.FindCityBuildingController(new Address(made.Spec.Street, made.Spec.Number)) == made.Controller;
        }

        /// <summary>The length of the game's closest-building job, or -1 while it is not made (or cannot be read: an optional piece of the contract).</summary>
        public static int ClosestJobLength()
        {
            FieldInfo field = GameContract.ClosestJob.Field;
            if (field == null)
            {
                return -1;
            }
            try
            {
                object job = field.GetValue(null);
                return job is ClosestPositionJob read && read.distances.IsCreated ? read.distances.Length : -1;   // a distance for each building of the list it was made from
            }
            catch (Exception)
            {
                return -1;   // read through a boxed copy; a job being run is read again at the next look
            }
        }

        /// <summary>Claims the address in the book of special buildings (which also keeps the game's ignore list), while the option is on.</summary>
        public static bool Claim(MadeSpec spec, Func<bool> on)
        {
            bool done = SpecialBuildings.Reserve(spec.Id, spec.Street, spec.Number, on, SpecialKind.Other);
            if (done)
            {
                Claimed.Add(spec.Id);
            }
            return done;
        }

        /// <summary>
        /// The made homes on the food deliveries' list of addresses they leave out (the game would send them orders, and an
        /// order names its address in the save); the list as the game had it is put back when the city goes.
        /// </summary>
        public static bool LeaveOutHomes(List<Address> homes)
        {
            FieldInfo field = GameContract.DeliveryLeftOut.Field;
            FoodDeliveryJobConfig config = DeliveryConfig();
            if (homes.Count == 0)
            {
                return true;
            }
            if (field == null || config == null)
            {
                return false;
            }
            var was = field.GetValue(config) as Address[];
            if (_deliveryConfig != config)
            {
                _deliveryConfig = config;
                _deliveryWas = was ?? new Address[0];
            }
            var now = new Address[_deliveryWas.Length + homes.Count];
            Array.Copy(_deliveryWas, now, _deliveryWas.Length);
            for (int i = 0; i < homes.Count; i++)
            {
                now[_deliveryWas.Length + i] = homes[i];
            }
            field.SetValue(config, now);
            return true;
        }

        /// <summary>
        /// The claims let go and the food deliveries' list put back as the game had it; when the city goes, the records and
        /// the streets destroyed too. The save tools' removal stops the mod in a city that stays: the made buildings'
        /// controllers stand on, reading their records, until the city goes, and the engine's sweep takes the records then.
        /// </summary>
        public static void Unload(bool cityGoes)
        {
            for (int i = 0; i < Claimed.Count; i++)
            {
                SpecialBuildings.Unreserve(Claimed[i]);
            }
            Claimed.Clear();
            if (_deliveryConfig != null && GameContract.DeliveryLeftOut.Field != null)
            {
                try
                {
                    GameContract.DeliveryLeftOut.Field.SetValue(_deliveryConfig, _deliveryWas);
                }
                catch (Exception e)
                {
                    Logger?.Warn("Made buildings: the food deliveries' list of addresses they leave out could not be put back (" + e.Message + "); the game reads its own again at the next start.");
                }
            }
            _deliveryConfig = null;
            _deliveryWas = null;
            if (cityGoes)
            {
                Drop();
            }
        }

        // Out of the game's lookups and destroyed: what the mod made by code goes with the city.
        private static void Drop()
        {
            for (int i = 0; i < Records.Count; i++)
            {
                Building b = Records[i];
                if (b != null)
                {
                    Leave(b);
                    UnityEngine.Object.Destroy(b);
                }
            }
            Records.Clear();
            DropServices();
            var lookup = GameContract.StreetLookup.Field?.GetValue(null) as Dictionary<string, StreetData>;
            foreach (KeyValuePair<string, StreetData> pair in Streets)
            {
                StreetData data = pair.Value;
                if (lookup != null && lookup.TryGetValue(pair.Key, out StreetData there) && there == data)
                {
                    lookup.Remove(pair.Key);
                }
                if (data != null)
                {
                    UnityEngine.Object.Destroy(data);
                }
            }
            Streets.Clear();
        }

        // ---------------------------------------------------------------- Going Public

        /// <summary>
        /// True while every machine is known to run this mod in this version, so every machine has the same made buildings:
        /// no session; a guest following its host's city; a host followed by its guests. A guest without the mod at all is
        /// not seen by the host: such a player simply does not see the buildings (Going Public ignores what names them).
        /// </summary>
        public static bool SessionAgrees()
        {
            if (!GoingPublic.InSession)
            {
                return true;
            }
            return GoingPublic.IsGuest ? SharedCity.Following : SharedCity.Leading;
        }
    }
}
