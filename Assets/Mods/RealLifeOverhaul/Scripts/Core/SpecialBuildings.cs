using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using Localizor;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The names of the mod's special buildings, wherever the game names a building. A feature puts its chosen building
    /// in under an id of its own:
    ///
    ///   SpecialBuildings.Set("utilities", office.Building, SpecialKind.Civic, "reallife_util_desk_title", "reallife_util_card_type", () => Settings.Utilities);
    ///   SpecialBuildings.Remove("utilities");
    ///
    /// and the rest read <see cref="NameFor(CityBuildingController)"/>, null where the game's own name stands (not in the
    /// book, its feature off, the player's, or the option off). <see cref="SpecialNames"/> shows the names; only what is
    /// shown changes, never the save. The book (<see cref="SpecialNameBook"/>) is game-free and filled on every city load.
    /// </summary>
    public static class SpecialBuildings
    {
        private static readonly SpecialNameBook Book = new SpecialNameBook();
        private static readonly Func<string, string> Localise = key => key.GetLocalization();

        public static IModLogger Logger;

        public static int Count => Book.Count;

        /// <summary>Changes whenever the book or its names do.</summary>
        public static int Stamp => Book.Stamp * 31 + FindableTypes.Count;

        /// <summary>True while names are given at all: the option is on and some building is in the book.</summary>
        public static bool Active => Settings.SpecialNames && Book.Count > 0;

        /// <summary>
        /// True: the mod always makes the city map's search, so any building is found by its address, empty or not (the game lists only a
        /// building with a business name), with the special names and kinds found by their word besides.
        /// </summary>
        internal static bool Searching => true;

        /// <summary>Puts a building in the book under the feature's id, in place of what it held; false if refused (logged).</summary>
        public static bool Set(string id, CityBuildingController building, SpecialKind kind, string nameKey, string typeKey = null, Func<bool> shown = null)
        {
            if (building == null || building.building == null || building.building.Address == null)
            {
                return Remove(id) && false;
            }
            return Set(id, building.building.Address.streetName, building.building.Address.streetNumber, kind, nameKey, typeKey, shown);
        }

        public static bool Set(string id, string street, int number, SpecialKind kind, string nameKey, string typeKey = null, Func<bool> shown = null)
        {
            bool done = Book.Set(id, street, number, kind, nameKey, typeKey, shown);
            if (!done)
            {
                SpecialEntry there = Book.At(street, number);
                string reserved = Book.ReservedFor(street, number);
                Logger?.Info("Special buildings: '" + id + "' at " + street + " " + number + " keeps the game's name ("
                             + (!SpecialNameBook.Accepts(kind) ? kind + " keeps its own name" : there != null ? "the building is already '" + there.Id + "'"
                                 : reserved != null ? "the building is reserved for '" + reserved + "'" : "nothing to name it by") + ").");
            }
            Changed();
            return done;
        }

        /// <summary>Reserves a fixed address for a feature's id while <paramref name="on"/> holds (<see cref="SpecialNameBook.Reserve"/>).</summary>
        public static bool Reserve(string id, string street, int number, Func<bool> on, SpecialKind kind = SpecialKind.Other)
        {
            bool done = Book.Reserve(id, street, number, on, kind);
            if (!done)
            {
                Logger?.Warn("Special buildings: '" + id + "' could not reserve " + street + " " + number + " (another feature reserves it).");
            }
            Changed();
            return done;
        }

        public static void Unreserve(string id)
        {
            if (Book.Unreserve(id))
            {
                Changed();
            }
        }

        public static string ReservedFor(string street, int number)
        {
            return Book.ReservedFor(street, number);
        }

        /// <summary>
        /// Gives the building filed under the id a say on the game's card beyond its name and kind: open or closed by the feature's own hours and who runs
        /// it (<see cref="SpecialFacts"/>, filled by <paramref name="facts"/> each time the card is written, once a second at most). False when the id
        /// holds nothing. Only what the card shows changes, never the save.
        /// </summary>
        public static bool Facts(string id, Action<SpecialFacts> facts)
        {
            SpecialEntry entry = Book.Of(id);
            if (entry == null)
            {
                return false;
            }
            entry.Facts = facts;
            SpecialNames.Changed();
            return true;
        }

        /// <summary>The book's entry for a building while its feature shows it, or null: what the game's card writes from.</summary>
        internal static SpecialEntry EntryFor(CityBuildingController building)
        {
            Address address = Active && building != null && building.building != null ? building.building.Address : null;
            return address == null || Players(building) ? null : Book.At(address.streetName, address.streetNumber);
        }

        private static readonly SpecialFacts PageFacts = new SpecialFacts();
        private static bool _pageFactsFailed;

        /// <summary>
        /// What BizMan's page of a building the mod has taken (<see cref="TakenBy(BuildingRegistration)"/>) says beyond "Taken by": the feature's facts (who owns
        /// it, its own line) and the words of what it is (the card's line under the name). Null when the building is not taken. Asked when the page is
        /// written, never per frame; the feature's callback runs behind its own guard, so a fault there is one log line and the page keeps its "Taken by".
        /// The returned object is the book's own, valid until the next call.
        /// </summary>
        internal static SpecialFacts PageOf(BuildingRegistration registration, out string typeWords)
        {
            typeWords = null;
            if (TakenBy(registration) == null)
            {
                return null;
            }
            SpecialEntry entry = Book.At(registration.StreetName, registration.StreetNumber);
            PageFacts.Clear();
            if (entry == null)
            {
                return PageFacts;
            }
            typeWords = string.IsNullOrEmpty(entry.TypeKey) ? null : Localise(entry.TypeKey);
            if (entry.Facts != null && !_pageFactsFailed)
            {
                try
                {
                    entry.Facts(PageFacts);
                }
                catch (Exception e)
                {
                    _pageFactsFailed = true;   // one line, then the page's own "Taken by" for every building
                    PageFacts.Clear();
                    Logger?.Error(e);
                    Logger?.Warn("Special buildings: a feature's say on BizMan's page of its building failed (the error above); the page says 'Taken by' alone.");
                }
            }
            return PageFacts;
        }

        /// <summary>The id of the feature put in the book at a building, leaving reservations aside; null for none.</summary>
        public static string FiledAt(CityBuildingController building)
        {
            Address address = building != null && building.building != null ? building.building.Address : null;
            return address != null ? Book.FiledId(address.streetName, address.streetNumber) : null;
        }

        /// <summary>As Set, with a proper name never translated; a tower gives its companies by floor as <paramref name="tenants"/>.</summary>
        public static bool SetNamed(string id, string street, int number, SpecialKind kind, string name, string typeKey = null, Func<bool> shown = null, string[] tenants = null)
        {
            bool done = Book.Set(id, street, number, kind, name, typeKey, shown, literal: true, tenants: tenants);
            if (!done)
            {
                SpecialEntry there = Book.At(street, number);
                Logger?.Info("Special buildings: '" + id + "' at " + street + " " + number + " keeps the game's name ("
                             + (there != null ? "the building is already '" + there.Id + "'" : "nothing to name it by") + ").");
            }
            Changed();
            return done;
        }

        /// <summary>The id's feature stood a board of its own at the door: the game's signs are then only switched off.</summary>
        public static void Boarded(string id, bool board)
        {
            Book.Boarded(id, board);
        }

        /// <summary>Every building in the book, shown or not, into the caller's list.</summary>
        internal static void Entries(List<SpecialEntry> into)
        {
            Book.All(into);
        }

        /// <summary>An entry's name in the current language while its feature shows it (kept until the language changes).</summary>
        internal static string NameOf(SpecialEntry entry)
        {
            return entry != null ? Book.NameAt(entry.Street, entry.Number, false, Localise) : null;
        }

        /// <summary>The id of the feature that holds a building, shown or not; null for none.</summary>
        public static string HolderOf(CityBuildingController building)
        {
            Address address = building != null && building.building != null ? building.building.Address : null;
            return address != null ? Book.HolderOf(address.streetName, address.streetNumber) : null;
        }

        /// <summary>Who is in the building at an address by floor, while shown; null for any other. The book's own array.</summary>
        public static string[] TenantsOf(string street, int number)
        {
            SpecialEntry entry = Book.Count > 0 ? Book.At(street, number) : null;
            return entry != null ? entry.Tenants : null;
        }

        /// <summary>The same for a building in the street; null for the player's.</summary>
        public static string[] TenantsOf(CityBuildingController building)
        {
            Address address = building != null && building.building != null ? building.building.Address : null;
            return address == null || Players(building) ? null : TenantsOf(address.streetName, address.streetNumber);
        }

        /// <summary>Once a second: an option switched changes what is shown and refused, and all made from the book is made again.</summary>
        internal static void LookShown()
        {
            if (Book.Count > 0 && Book.LookShown())
            {
                Changed();
            }
        }

        public static bool Remove(string id)
        {
            bool done = Book.Remove(id);
            if (done)
            {
                Changed();
            }
            return done;
        }

        /// <summary>The special name of a building, or null where the game's own name stands.</summary>
        public static string NameFor(CityBuildingController building)
        {
            if (!Active || building == null || building.building == null)
            {
                return null;
            }
            Address address = building.building.Address;
            return address == null ? null : Book.NameAt(address.streetName, address.streetNumber, Players(building.buildingRegistration), Localise);
        }

        public static string NameFor(Address address)
        {
            if (!Active || address == null || string.IsNullOrEmpty(address.streetName))
            {
                return null;
            }
            if (Book.At(address.streetName, address.streetNumber) == null)
            {
                return null;   // most addresses: no look at the registration
            }
            BuildingRegistration registration = null;
            try
            {
                registration = BuildingHelper.GetBuildingRegistration(address);
            }
            catch (Exception)
            {
                // a place the game has no registration for: named as the book says
            }
            return Book.NameAt(address.streetName, address.streetNumber, Players(registration), Localise);
        }

        /// <summary>The locale key of what a special building is (the line under its name on the game's card), or null.</summary>
        public static string TypeKeyFor(CityBuildingController building)
        {
            if (NameFor(building) == null)
            {
                return null;
            }
            Address address = building.building.Address;
            SpecialEntry entry = Book.At(address.streetName, address.streetNumber);
            return entry != null ? entry.TypeKey : null;
        }

        public static bool Holds(string street, int number)
        {
            return Book.Holds(street, number);
        }

        /// <summary>True if a feature is in the building now, reservations aside: for what the mod has made of it.</summary>
        public static bool Filed(string street, int number)
        {
            return Book.FiledId(street, number) != null;
        }

        public static bool Filed(CityBuildingController building)
        {
            return FiledAt(building) != null;
        }

        public static bool Holds(CityBuildingController building)
        {
            Address address = building != null && building.building != null ? building.building.Address : null;
            return address != null && Book.Holds(address.streetName, address.streetNumber);
        }

        private static readonly HashSet<string> FindableTypes = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>A special service's business type to be found in the map's search by the game's word for it.</summary>
        public static void Findable(string businessType)
        {
            if (!string.IsNullOrEmpty(businessType) && FindableTypes.Add(businessType))
            {
                Changed();
            }
        }

        /// <summary>The name a building is found by in the city map's search where it has no business name of its own, or null.</summary>
        internal static string FoundAs(CityBuildingController building)
        {
            if (FindableTypes.Count == 0 || building == null || building.building == null || building.building.SpecialService == null)
            {
                return null;
            }
            string type = building.building.SpecialService.businessTypeName;
            return type != null && FindableTypes.Contains(type) ? type.GetLocalization() : null;
        }

        internal static void Forget()
        {
            Book.Forget();
        }

        internal static void Clear()
        {
            Book.Clear();
            FindableTypes.Clear();
            HeldUnits.BookChanged();
        }

        // Every change of the book: what is shown is written again, and what the book holds is held from the game's letting at once.
        private static void Changed()
        {
            SpecialNames.Changed();
            HeldUnits.BookChanged();
        }

        /// <summary>What the book holds now, as claims (<see cref="SpecialNameBook.Holding"/>), into the caller's list.</summary>
        internal static void Holding(List<HeldClaim> into)
        {
            Book.Holding(into);
        }

        /// <summary>True for a building the player rents or owns (in Going Public another player's too).</summary>
        public static bool Players(BuildingRegistration registration)
        {
            return registration != null && !SpecialNameBook.MaySit(registration.RentedByPlayer, registration.BuildingOwnedByPlayer);
        }

        public static bool Players(CityBuildingController building)
        {
            return building != null && Players(building.buildingRegistration);
        }

        /// <summary>The name of the feature a building is taken by while it is off the market here; null for any other.</summary>
        public static string TakenBy(BuildingRegistration registration)
        {
            if (registration == null || Book.Count == 0 || string.IsNullOrEmpty(registration.StreetName)
                || Book.At(registration.StreetName, registration.StreetNumber) == null
                || !Book.Refuses(registration.StreetName, registration.StreetNumber, registration.RentedByPlayer, registration.BuildingOwnedByPlayer))
            {
                return null;
            }
            return Book.NameAt(registration.StreetName, registration.StreetNumber, false, Localise);
        }

        public static string TakenBy(Address address)
        {
            if (address == null || Book.Count == 0 || Book.At(address.streetName, address.streetNumber) == null)
            {
                return null;
            }
            BuildingRegistration registration = null;
            try
            {
                registration = BuildingHelper.GetBuildingRegistration(address);
            }
            catch (Exception)
            {
                // a place the game has no registration for: nothing to rent there
            }
            return TakenBy(registration);
        }

        private static readonly List<SpecialEntry> ShownNow = new List<SpecialEntry>(32);
        private const string NoBusiness = "ba:businesstype_empty";

        /// <summary>After the game lights the open map: a taken building lit only as to let or for sale is put out.</summary>
        internal static void Unlist()
        {
            CityManager city = Book.Count > 0 ? InstanceBehavior<CityManager>.Instance : null;
            if (city == null)
            {
                return;
            }
            Book.Shown(ShownNow);
            for (int i = 0; i < ShownNow.Count; i++)
            {
                SpecialEntry entry = ShownNow[i];
                CityBuildingController building = city.FindCityBuildingController(new Address(entry.Street, entry.Number));
                BuildingRegistration registration = building != null ? building.buildingRegistration : null;
                if (registration == null || Players(registration) || (!string.IsNullOrEmpty(registration.businessTypeName) && registration.businessTypeName != NoBusiness)
                    || (!registration.AvailableForRent && !registration.IsOnSale()))
                {
                    continue;
                }
                building.SetHighlight(false);
            }
            ShownNow.Clear();
        }
    }
}
