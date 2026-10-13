using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Helpers;
using Localizor;
using Streets;
using TMPro;
using UI;
using UI.CurrentBuilding;
using UI.Guiders;
using UI.InGameUI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The names of the mod's special buildings, wherever the game names a building. A feature that gives one of the
    /// city's buildings a role puts it in with one line once it has chosen the building, under an id of its own:
    ///
    ///   SpecialBuildings.Set("utilities", office.Building, SpecialKind.Civic, "reallife_util_desk_title", "reallife_util_card_type", () => Settings.Utilities);
    ///   SpecialBuildings.Set("campus", campus.Street, campus.Number, SpecialKind.Campus, campusNameKey);   // by address
    ///   SpecialBuildings.Remove("campus");                                                                           // let go of it
    ///
    /// and everything else reads <see cref="NameFor(CityBuildingController)"/> (or the address form), which is null for
    /// any building that keeps the game's own name: one not in the book, one whose feature is off, one the player rents
    /// or owns, and every building while the option "Special buildings carry their own names" is off. The book
    /// itself (<see cref="SpecialNameBook"/>) has no game types and is checked offline.
    ///
    /// Where the name is shown (<see cref="SpecialNames"/>, the component that does it): the game's card over a building
    /// (under the pointer in the street and on the map), the destination marker of the game's guiders, the headline at
    /// the top of the screen inside the building, the city map's search; the GPS's title and its arrival line
    /// (Navigation/NavDirector) and the mod's own map markers (World/MapLinks, Core/MapPlaces) read it too. Only what is
    /// shown is changed, never what the game keeps in the save, so switching the option off or removing the mod shows
    /// the game's own names again. The book is filled again on every city load.
    /// </summary>
    public static class SpecialBuildings
    {
        private static readonly SpecialNameBook Book = new SpecialNameBook();
        private static readonly Func<string, string> Localise = key => key.GetLocalization();

        public static IModLogger Logger;

        /// <summary>Buildings in the book (whether or not their names are shown just now).</summary>
        public static int Count => Book.Count;

        /// <summary>Changes whenever the book or its names do.</summary>
        public static int Stamp => Book.Stamp * 31 + FindableTypes.Count;

        /// <summary>True while names are given at all: the option is on and some building is in the book.</summary>
        public static bool Active => Settings.SpecialNames && Book.Count > 0;

        /// <summary>True while the city map's search is to be made by the mod: names are given, or some kind is to be found by its own word.</summary>
        internal static bool Searching => Active || FindableTypes.Count > 0;

        /// <summary>Puts a building in the book under the feature's id, in place of what that id held. False if it was refused (and said once in the log).</summary>
        public static bool Set(string id, CityBuildingController building, SpecialKind kind, string nameKey, string typeKey = null, Func<bool> shown = null)
        {
            if (building == null || building.building == null || building.building.Address == null)
            {
                return Remove(id) && false;
            }
            return Set(id, building.building.Address.streetName, building.building.Address.streetNumber, kind, nameKey, typeKey, shown);
        }

        /// <summary>The same, by address (for a feature that keeps the street and number rather than the building).</summary>
        public static bool Set(string id, string street, int number, SpecialKind kind, string nameKey, string typeKey = null, Func<bool> shown = null)
        {
            bool done = Book.Set(id, street, number, kind, nameKey, typeKey, shown);
            if (!done)
            {
                SpecialEntry there = Book.At(street, number);
                Logger?.Info("Special buildings: '" + id + "' at " + street + " " + number + " keeps the game's name ("
                             + (!SpecialNameBook.Accepts(kind) ? kind + " keeps its own name" : there != null ? "the building is already '" + there.Id + "'" : "nothing to name it by") + ").");
            }
            SpecialNames.Changed();
            return done;
        }

        /// <summary>
        /// The same with a proper name that is never translated (a company's), by address. The name is shown as it is
        /// given; the line under it is still a locale key.
        /// </summary>
        public static bool SetNamed(string id, string street, int number, SpecialKind kind, string name, string typeKey = null, Func<bool> shown = null)
        {
            bool done = Book.Set(id, street, number, kind, name, typeKey, shown, literal: true);
            if (!done)
            {
                SpecialEntry there = Book.At(street, number);
                Logger?.Info("Special buildings: '" + id + "' at " + street + " " + number + " keeps the game's name ("
                             + (there != null ? "the building is already '" + there.Id + "'" : "nothing to name it by") + ").");
            }
            SpecialNames.Changed();
            return done;
        }

        /// <summary>The id of the feature that holds a building, shown or not; null for none (a feature choosing a building passes over the others').</summary>
        public static string HolderOf(CityBuildingController building)
        {
            Address address = building != null && building.building != null ? building.building.Address : null;
            return address != null ? Book.HolderOf(address.streetName, address.streetNumber) : null;
        }

        /// <summary>Takes the id's building out of the book: it has the game's own name again.</summary>
        public static bool Remove(string id)
        {
            bool done = Book.Remove(id);
            if (done)
            {
                SpecialNames.Changed();
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

        /// <summary>The special name of the building at an address, or null where the game's own name stands.</summary>
        public static string NameFor(Address address)
        {
            if (!Active || address == null || string.IsNullOrEmpty(address.streetName))
            {
                return null;
            }
            if (Book.At(address.streetName, address.streetNumber) == null)
            {
                return null;   // (most addresses: no look at the registration)
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

        /// <summary>True if a feature holds the building at an address (a later feature that picks a building of its own leaves it alone).</summary>
        public static bool Holds(string street, int number)
        {
            return Book.Holds(street, number);
        }

        /// <summary>True if a feature holds this building.</summary>
        public static bool Holds(CityBuildingController building)
        {
            Address address = building != null && building.building != null ? building.building.Address : null;
            return address != null && Book.Holds(address.streetName, address.streetNumber);
        }

        private static readonly HashSet<string> FindableTypes = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// A kind of the game's special service (its business type, "ba:businesstype_school") that keeps the game's own name
        /// but is to be found in the city map's search even where the game gives it no business name: it is listed under
        /// the game's own word for the kind. A feature that sends the player there says so once per city load.
        /// </summary>
        public static void Findable(string businessType)
        {
            if (!string.IsNullOrEmpty(businessType) && FindableTypes.Add(businessType))
            {
                SpecialNames.Changed();
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
        }

        /// <summary>
        /// True for a building the player rents or owns (in a Going Public session another player's too: Going Public puts
        /// it on this machine as rented by the player). No feature makes such a building its own: it chooses another, and
        /// one whose building becomes the player's moves off it.
        /// </summary>
        public static bool Players(BuildingRegistration registration)
        {
            return registration != null && !SpecialNameBook.MaySit(registration.RentedByPlayer, registration.BuildingOwnedByPlayer);
        }

        /// <summary>The same for a building in the street.</summary>
        public static bool Players(CityBuildingController building)
        {
            return building != null && Players(building.buildingRegistration);
        }

        // ---------------------------------------------------------------- off the market (Core/ClaimedOffers)

        /// <summary>
        /// The name of the feature a building is taken by, for the line that says so, while it is off the market here: a
        /// feature holds it and is on, and the player neither rents nor owns it. Null for every other building (most of
        /// them after one dictionary look). The same with the option for names off: the building is taken all the same.
        /// </summary>
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

        /// <summary>The same by address.</summary>
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

        /// <summary>
        /// When the game's look at the buildings on the open map is over (MapPlaces): a building taken by a feature that
        /// the game lit because it is to let or for sale (its rows of units to let by kind, and "for sale") is put out, as
        /// the game puts out a building it does not list. One that a row of the mod's lights is lit again by MapPlaces in
        /// the same frame. A business in a taken building stays as the game's rows of businesses show it. Thirty-odd
        /// dictionary looks, on the event only.
        /// </summary>
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

    /// <summary>
    /// Shows the special buildings' names (<see cref="SpecialBuildings"/>) in the game's own interface. Each place is
    /// written after the game has written it, on the game's own events or the look once a second, never per frame
    /// except for one reference comparison while some building is in the book (the card, as Core/MapPlaces does it):
    ///   the card       the business name on the game's card over a building (UI.InGameUI.BuildingResume, which is
    ///                  also the card of a building picked on the city map), and what it is on the line under it;
    ///                  written the frame the card changes building, and again each second (the game rewrites it on
    ///                  the hour and when a registration changes);
    ///   the guiders    the name plate of the destination marker and the other guiders set to a special building's
    ///                  address (UI.Guiders.DirectionGuider): once a second, and as soon as "set destination" is pressed;
    ///   inside         the headline at the top of the screen in the building (UI.CurrentBuilding.CurrentBuildingUI):
    ///                  the frame after the player goes in, and once a second;
    ///   the search     the city map's search list (CityMapFilters): the game makes it from the business names, so a
    ///                  special building is listed under its own name and the empty ones (a police station) are
    ///                  listed at all; made again only when the map opens after the game has made its own anew.
    /// Switching the option off puts the game's own words back at once (it is asked to write them again). Everything
    /// goes with the city: the book is emptied when this component is destroyed.
    /// </summary>
    public sealed class SpecialNames : MonoBehaviour
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string ToLet = "common_available_for_rent";   // the game's state line on the card for a unit to let
        private const string NotToLet = "reallife_claimed_card";

        private static SpecialNames _instance;

        private readonly WaitForSecondsRealtime _second = new WaitForSecondsRealtime(1f);
        private readonly Dictionary<DirectionGuider, Address> _named = new Dictionary<DirectionGuider, Address>(4);
        private BuildingResume _card;
        private bool _cardFailed;
        private EntityController _carded;
        private UnityEngine.UI.Button _destination;   // the card's "set destination", which has our listener
        private CurrentBuildingUI _inside;
        private TextMeshProUGUI _headline;
        private bool _headlineNamed;
        private bool _lookedForInside;
        private List<string> _searchOptions;          // the search list last given to the map (null: the game's own)
        private int _searchStamp = -1;               // the book's stamp when that list was made
        private bool _searchFailed;
        private bool _on;
        private int _stamp = -1;
        private Action<Address> _onEnter;
        private Action<Address> _enterSeen;
        private Action<bool> _onMap;
        private Action<bool> _mapSeen;
        private Action _onLanguage;

        private static FieldInfo _headlineField, _searchListField, _searchDropField, _optionsField;
        private static MethodInfo _populate;

        /// <summary>The book or an option changed: everything is written again at the next look (and the card at once).</summary>
        internal static void Changed()
        {
            if (_instance != null)
            {
                _instance._carded = null;
                _instance._stamp = -1;
                _instance.enabled = SpecialBuildings.Active;
            }
        }

        private void Awake()
        {
            _instance = this;
            _onEnter = _ => StartCoroutine(Soon());
            _onMap = open =>
            {
                if (open)
                {
                    Search();
                }
            };
            _onLanguage = () =>
            {
                SpecialBuildings.Forget();
                _searchOptions = null;
                Changed();
            };
            try
            {
                LocalizorManager.OnLanguageChanged = (Action)Delegate.Combine(LocalizorManager.OnLanguageChanged, _onLanguage);
            }
            catch (Exception e)
            {
                SpecialBuildings.Logger?.Error(e);
            }
        }

        private void Start()
        {
            StartCoroutine(Look());
        }

        private void OnDestroy()
        {
            try
            {
                LocalizorManager.OnLanguageChanged = (Action)Delegate.Remove(LocalizorManager.OnLanguageChanged, _onLanguage);
                if (_destination != null)
                {
                    _destination.onClick.RemoveListener(Guiders);
                }
            }
            catch (Exception)
            {
                // the game's interface is going too
            }
            SpecialBuildings.Clear();
            ClaimedOffers.Forget();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private IEnumerator Look()
        {
            while (true)
            {
                try
                {
                    Tick();
                }
                catch (Exception e)
                {
                    SpecialBuildings.Logger?.Error(e);
                }
                yield return _second;
            }
        }

        private IEnumerator Soon()
        {
            yield return null;   // after the game's own handlers of the same event have written their words
            if (_inside == null)
            {
                _lookedForInside = false;   // (looked for again on going in, not every second)
            }
            try
            {
                Inside();
            }
            catch (Exception e)
            {
                SpecialBuildings.Logger?.Error(e);
            }
        }

        // Once a second.
        private void Tick()
        {
            if (SaveGameManager.Current == null)
            {
                return;
            }
            EventHold.Keep(ref GlobalEvents.onEnterBuilding, _onEnter, ref _enterSeen);
            EventHold.Keep(ref GlobalEvents.onCityMapToggle, _onMap, ref _mapSeen);
            ClaimedOffers.Keep();   // the mod's buildings off the market (Core/ClaimedOffers): two null tests once its pages are watched
            UIs uis = InstanceBehavior<UIs>.Instance;
            BuildingResume card = uis != null ? uis.buildingResume : null;
            if (!ReferenceEquals(card, _card))
            {
                if (_destination != null)
                {
                    _destination.onClick.RemoveListener(Guiders);
                }
                _card = card;
                _destination = card != null ? card.setDestinationButton : null;
                if (_destination != null)
                {
                    _destination.onClick.AddListener(Guiders);   // (after the game's own: its listener is the button's own)
                }
            }
            bool on = SpecialBuildings.Active;
            bool changed = on != _on || _stamp != SpecialBuildings.Stamp;
            bool searching = SpecialBuildings.Searching;
            _on = on;
            _stamp = SpecialBuildings.Stamp;
            enabled = on;
            _carded = null;   // the card written again: the game rewrites it on the hour
            Guiders();
            Inside();
            if (changed && CityMap.IsOpen)
            {
                Search();
            }
            else if (changed && !searching && _searchOptions != null)
            {
                Search();   // the game's own list back
            }
        }

        // ---------------------------------------------------------------- the card

        private static readonly int HotLateUpdate = HotSpots.Slot("Core: SpecialNames.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (ReferenceEquals(_card, null) || _cardFailed)
            {
                return;
            }
            EntityController target = _card.TargetController;
            if (ReferenceEquals(target, _carded))
            {
                return;
            }
            _carded = target;
            if (!ReferenceEquals(target, null))
            {
                Card(target as CityBuildingController);
            }
        }

        private void Card(CityBuildingController building)
        {
            string name = SpecialBuildings.NameFor(building);
            if (name == null)
            {
                return;
            }
            try
            {
                // A building off the market (Core/ClaimedOffers) is not said to be to let.
                if (_card.openStateLabel != null && string.Equals(_card.openStateLabel.Key, ToLet, StringComparison.Ordinal)
                    && SpecialBuildings.TakenBy(building.buildingRegistration) != null)
                {
                    _card.openStateLabel.Key = NotToLet;
                    _card.openStateLabel.Prefix = string.Empty;
                }
                if (_card.businessNameLabel != null)
                {
                    _card.businessNameLabel.gameObject.SetActive(true);
                    _card.businessNameLabel.text = name;
                }
                string type = SpecialBuildings.TypeKeyFor(building);
                if (_card.businessTypeLabel != null && type != null)
                {
                    _card.businessTypeLabel.Key = type;
                }
            }
            catch (Exception e)
            {
                SpecialBuildings.Logger?.Error(e);
                _cardFailed = true;   // (the card is not as expected: left alone from here on)
            }
        }

        // ---------------------------------------------------------------- the guiders

        // Each guider set to an address: the special name on its marker, or the game's own words back where a name was
        // put and is no longer wanted. A few comparisons a second.
        private void Guiders()
        {
            List<DirectionGuider> all = GuidersManager.AllGuiders;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                DirectionGuider guider = all[i];
                if (guider == null || guider.poi == null)
                {
                    continue;
                }
                Address address = guider.CurrentAddress;
                string name = address != null && !address.IsUndefined() ? SpecialBuildings.NameFor(address) : null;
                if (name != null)
                {
                    if (!string.Equals(Nav.NavMarks.Words(guider.poi), name, StringComparison.Ordinal))
                    {
                        guider.poi.SetText(name);
                    }
                    _named[guider] = address;
                }
                else if (_named.TryGetValue(guider, out Address was))
                {
                    _named.Remove(guider);
                    if (address != null && !address.IsUndefined() && address == was)
                    {
                        guider.UpdateAddressName(address);   // the game's own words for it again
                    }
                }
            }
        }

        // ---------------------------------------------------------------- inside

        private static readonly int HotInside = HotSpots.Slot("Core: SpecialNames.Inside");
        private void Inside()
        {
            using var hotSpot = HotSpots.Time(HotInside);
            if (!SpecialBuildings.Active && !_headlineNamed)
            {
                return;
            }
            if (!_lookedForInside)
            {
                _lookedForInside = true;
                // The game's own reference to its panel, not a search of the scene: FindObjectOfType with switched-off objects
                // included walked the whole city, a frame of some 100 ms the first time the player went into a building.
                UIs uis = InstanceBehavior<UIs>.Instance;
                UI.PlayerHUD.PlayerHUD hud = uis != null ? uis.playerHUD : null;
                _inside = hud != null ? hud.currentBuildingUI : null;
                if (_headlineField == null)
                {
                    _headlineField = typeof(CurrentBuildingUI).GetField("headlineLabel", Hidden);
                }
                _headline = _inside != null && _headlineField != null ? _headlineField.GetValue(_inside) as TextMeshProUGUI : null;
            }
            if (_inside == null || _headline == null)
            {
                return;
            }
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            BuildingRegistration registration = BuildingManager.IsInsideBuilding && manager != null && !Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking
                ? manager.buildingRegistration : null;
            string name = registration != null ? SpecialBuildings.NameFor(registration.Address) : null;
            if (name != null)
            {
                if (!string.Equals(_headline.text, name, StringComparison.Ordinal))
                {
                    _headline.text = name;
                }
                _headlineNamed = true;
            }
            else if (_headlineNamed)
            {
                _headlineNamed = false;
                if (registration != null)
                {
                    _inside.UpdateData();   // the game's own headline again
                }
            }
        }

        // ---------------------------------------------------------------- the city map's search

        private static readonly int HotSearch = HotSpots.Slot("Core: SpecialNames.Search");
        // The game lists every building with a business name, as "name, address", and finds them by the name. The list is
        // made again the way the game makes it, with the special names, only when the game has made its own anew since.
        private void Search()
        {
            using var hotSpot = HotSpots.Time(HotSearch);
            if (_searchFailed)
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            CityMapFilters filters = uis != null ? uis.mapFilters : null;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (filters == null || city == null || city.cityBuildingControllers == null)
            {
                return;
            }
            try
            {
                if (_searchListField == null)
                {
                    _searchListField = typeof(CityMapFilters).GetField("_searchableBuildings", Hidden);
                    _searchDropField = typeof(CityMapFilters).GetField("searchDropdown", Hidden);
                    _optionsField = typeof(UI.Elements.Dropdown).GetField("_options", Hidden);
                    _populate = typeof(CityMapFilters).GetMethod("PopulateSearchDropdown", Hidden);
                }
                var listed = _searchListField != null ? _searchListField.GetValue(filters) as List<CityBuildingController> : null;
                var drop = _searchDropField != null ? _searchDropField.GetValue(filters) as UI.Elements.Dropdown : null;
                if (listed == null || drop == null || _optionsField == null || _populate == null)
                {
                    _searchFailed = true;
                    SpecialBuildings.Logger?.Info("Special buildings: the city map's search is not as expected; it lists the game's own names.");
                    return;
                }
                bool ours = _searchOptions != null && ReferenceEquals(_optionsField.GetValue(drop), _searchOptions);
                if (!SpecialBuildings.Searching)
                {
                    if (ours)
                    {
                        _populate.Invoke(filters, new object[] { null });   // the game's own list again
                    }
                    _searchOptions = null;
                    return;
                }
                if (ours && _searchStamp == SpecialBuildings.Stamp)
                {
                    return;
                }
                var options = new List<string>(listed.Count + 8);
                var words = new List<string>(listed.Count + 8);
                listed.Clear();
                CityBuildingController[] all = city.cityBuildingControllers;
                for (int i = 0; i < all.Length; i++)
                {
                    CityBuildingController building = all[i];
                    if (building == null || building.building == null || building.buildingRegistration == null)
                    {
                        continue;
                    }
                    string name = SpecialBuildings.NameFor(building) ?? building.buildingRegistration.BusinessName;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = SpecialBuildings.FoundAs(building);   // (a school or a hospital with no business name: the game's word for it)
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }
                    }
                    options.Add("<b><noparse>" + name + "</noparse></b><br><noparse>" + building.building.Address.ToFormattedString() + "</noparse>");
                    words.Add(name);
                    listed.Add(building);
                }
                drop.SetOptions(options, false, -1, words);
                _searchOptions = options;
                _searchStamp = SpecialBuildings.Stamp;
            }
            catch (Exception e)
            {
                _searchFailed = true;
                SpecialBuildings.Logger?.Error(e);
                SpecialBuildings.Logger?.Info("Special buildings: the city map's search could not be given the names (the error above); it lists the game's own.");
            }
        }
    }
}
