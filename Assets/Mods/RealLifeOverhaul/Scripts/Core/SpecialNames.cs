using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Helpers;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.BizPace;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.NewBuildings;
using Streets;
using TMPro;
using UI;
using UI.CurrentBuilding;
using UI.Guiders;
using UI.InGameUI;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Shows the special buildings' names in the game's own interface, each written after the game has written it: the
    /// building card (BuildingResume) the frame it changes building and each second; the guiders' name plates once a
    /// second and on "set destination"; the headline inside a building on going in and each second; the city map's
    /// search list when the map opens after the game has made its own. The option off puts the game's words back at
    /// once. Per frame: one reference comparison while a building is in the book.
    /// </summary>
    public sealed class SpecialNames : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "SpecialNames", typeof(SpecialNames));

        private const string ToLet = "common_available_for_rent";   // the game's state line on the card for a unit to let
        private const string NotToLet = "reallife_claimed_card";

        private static SpecialNames _instance;

        private readonly WaitForSecondsRealtime _second = new WaitForSecondsRealtime(1f);
        private readonly Dictionary<DirectionGuider, Address> _named = new Dictionary<DirectionGuider, Address>(4);
        private BuildingResume _card;
        private bool _cardFailed;
        private EntityController _carded;
        private UnityEngine.UI.Button _destination;   // the card's "set destination", with our listener
        private CurrentBuildingUI _inside;
        private TextMeshProUGUI _headline;
        private bool _headlineNamed;
        private bool _lookedForInside;
        private List<string> _searchOptions;          // the search list last given to the map (null: the game's own)
        private int _searchStamp = -1;               // the book's stamp when that list was made
        private bool _searchFailed;
        private readonly List<SpecialBuildings.SearchAlias> _aliases = new List<SpecialBuildings.SearchAlias>(16);   // the words besides the names, for the list being made
        private bool _on;
        private int _stamp = -1;
        private Action<Address> _onEnter;
        private Action<Address> _enterSeen;
        private Action<bool> _onMap;
        private Action<bool> _mapSeen;
        private Action<Address> _onRegistration;
        private Action<Address> _registrationSeen;
        private Action _onLanguage;

        private static readonly int BizRegistration = HotSpots.Slot("Core: SpecialNames.OnRegistration");

        private static FieldInfo _headlineField, _searchListField, _searchDropField, _optionsField;
        private static MethodInfo _populate;

        /// <summary>The book or an option changed: everything is written again at the next look, the card at once.</summary>
        internal static void Changed()
        {
            if (_instance != null)
            {
                _instance._carded = null;
                _instance._stamp = -1;
                _instance.enabled = SpecialBuildings.Active || SpecialBuildings.Count > 0;   // the card's "not to let" stands with the names off too
            }
        }

        private void Awake()
        {
            _instance = this;
            _onEnter = Guard.Wrap<Address>(_ => StartCoroutine(Guard.Routine(Soon())));
            _onMap = Guard.Wrap<bool>(open =>
            {
                if (open)
                {
                    Search();
                }
            });
            _onRegistration = Guard.Wrap<Address>(OnRegistration);
            _onLanguage = Guard.Wrap(() =>
            {
                SpecialBuildings.Forget();
                _searchOptions = null;
                Changed();
            });
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
            StartCoroutine(Guard.Routine(Look()));
        }

        private void OnDestroy()
        {
            try
            {
                LocalizorManager.OnLanguageChanged = (Action)Delegate.Remove(LocalizorManager.OnLanguageChanged, _onLanguage);
                GlobalEvents.onEnterBuilding -= _onEnter;   // the wrapped handlers EventHold put on: not left on the game's events for a second copy to double
                GlobalEvents.onCityMapToggle -= _onMap;
                GlobalEvents.onBuildingRegistrationChange -= _onRegistration;
                if (_destination != null)
                {
                    _destination.onClick.RemoveListener(_guiders);
                }
            }
            catch (Exception)
            {
                // the game's interface is going too
            }
            SpecialBuildings.Clear();
            ClaimedOffers.Forget();
            FrontSigns.Forget();
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

        // The game's registration change event, which BizMan raises each time a business view or the Schedule tab is left: only while the city map is
        // open does it ask for anything.
        private void OnRegistration(Address address)
        {
            using var timed = BizPaceWatch.Time(BizRegistration);   // a step of BizMan closing a business or leaving a tab (BizPace)
            if (CityMap.IsOpen)
            {
                StartCoroutine(Guard.Routine(SearchSoon()));   // the game made its own list anew: ours again, after its handler
            }
        }

        // A registration changed while the map is open: the game made its search list anew (its handler may run after ours), so a frame on ours is made again.
        private IEnumerator SearchSoon()
        {
            yield return null;
            Search();
        }

        /// <summary>The game's search list was made anew by the mod (a building left): ours again, only while the map is open (a shut map makes it when it opens).</summary>
        internal static void SearchAgain()
        {
            if (_instance != null && CityMap.IsOpen)
            {
                _instance.Search();
            }
        }

        private IEnumerator Soon()
        {
            yield return null;   // after the game's own handlers of the same event have written their words
            if (_inside == null)
            {
                _lookedForInside = false;   // looked for again on going in, not every second
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
            EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _onRegistration, ref _registrationSeen);
            ClaimedOffers.Keep();   // the mod's buildings off the market: two null tests once its pages are watched
            ClaimedMap.Keep();      // ...and out of the city map's rows to let and for sale: set up once
            FrontSigns.Keep();      // their fronts: no "For rent", their own sign: a few flag reads a building
            SpecialBuildings.LookShown();
            UIs uis = InstanceBehavior<UIs>.Instance;
            BuildingResume card = uis != null ? uis.buildingResume : null;
            if (!ReferenceEquals(card, _card))
            {
                if (_destination != null)
                {
                    _destination.onClick.RemoveListener(_guiders);
                }
                _card = card;
                _destination = card != null ? card.setDestinationButton : null;
                if (_destination != null)
                {
                    _guiders = _guiders ?? Guard.Listener(Guiders);
                    _destination.onClick.AddListener(_guiders);   // after the game's own, which is the button's own
                }
            }
            bool on = SpecialBuildings.Active;
            bool changed = on != _on || _stamp != SpecialBuildings.Stamp;
            bool searching = SpecialBuildings.Searching;
            _on = on;
            _stamp = SpecialBuildings.Stamp;
            enabled = on || SpecialBuildings.Count > 0;   // the card's "not to let" stands with the names off too
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

        private static readonly int HotLateUpdate = HotSpots.Slot("Core: SpecialNames.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (ReferenceEquals(_card, null) || _cardFailed)
            {
                return;
            }
            EntityController target = GameMembers.TargetOf(_card);
            if (ReferenceEquals(target, _carded))
            {
                return;
            }
            _carded = target;
            if (!ReferenceEquals(target, null))
            {
                try
                {
                    Card(target as CityBuildingController);
                }
                catch (Exception e) when (Guard.StandsDown(e, "the special buildings' names on the game's building card"))
                {
                    _cardFailed = true;   // a game lacking something the card names would fail at every building
                }
            }
        }

        private void Card(CityBuildingController building)
        {
            string name = SpecialBuildings.NameFor(building);
            if (name == null && (building == null || SpecialBuildings.TakenBy(building.buildingRegistration) == null))
            {
                return;
            }
            try
            {
                // A building off the market is not said to be to let, even with the names off.
                if (_card.openStateLabel != null && string.Equals(_card.openStateLabel.Key, ToLet, StringComparison.Ordinal)
                    && SpecialBuildings.TakenBy(building.buildingRegistration) != null)
                {
                    _card.openStateLabel.Key = NotToLet;
                    _card.openStateLabel.Prefix = string.Empty;
                }
                if (name == null)
                {
                    return;
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
                SpecialEntry entry = SpecialBuildings.EntryFor(building);
                if (entry != null && entry.Facts != null)
                {
                    Facts(entry);
                }
            }
            catch (Exception e)
            {
                SpecialBuildings.Logger?.Error(e);
                _cardFailed = true;   // the card is not as expected: left alone from here on
            }
        }

        private static readonly SpecialFacts Said = new SpecialFacts();

        // What a feature says on the card besides the name: open or closed by its own hours (the game's own lines for them, so the card reads as any
        // business's does), who runs it in the card's owner line, and the card's "click to enter" where the door may be walked in at. Once a second.
        private void Facts(SpecialEntry entry)
        {
            Said.Clear();
            entry.Facts(Said);
            if (Said.State != CardState.None && _card.openStateLabel != null)
            {
                bool open = Said.State == CardState.Open;
                string key = SpecialFacts.StateKey(Said.State, Said.Hours);
                _card.openStateLabel.gameObject.SetActive(true);
                _card.openStateLabel.Prefix = string.Empty;
                _card.openStateLabel.SetData(LanguageChangeEventDataHolder.Create(key, new { hours = Said.Hours, time = Said.AtHour.GetFormattedTime() }, open ? "common_open" : "common_closed"));
                _card.openStateLabel.TextContainer.color = open ? WorldPanel.Green() : WorldPanel.Red();
            }
            if (_card.rivalBusinessOwner != null)
            {
                bool run = !string.IsNullOrEmpty(Said.RunLine);
                _card.rivalBusinessOwner.gameObject.SetActive(run);
                if (run)
                {
                    _card.rivalBusinessOwner.SetData(LanguageChangeEventDataHolder.Create("reallife_campus_card_state", new { state = Said.RunLine }, "common_unknown"));
                }
            }
            _card.rivalBuildingOwner?.gameObject.SetActive(false);
            if (_card.clickToEnterLabel != null && Said.State != CardState.None)
            {
                _card.clickToEnterLabel.gameObject.SetActive(Said.CanEnter && !CityMap.IsOpen && !PlayerHelper.IsUsingVehicle);
            }
        }

        // Each guider set to an address: the special name on its marker, or the game's own words back where one is no
        // longer wanted. A few comparisons a second.
        private UnityAction _guiders;

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
                // The game's own reference to its panel, not a search of the scene (some 100 ms).
                UIs uis = InstanceBehavior<UIs>.Instance;
                UI.PlayerHUD.PlayerHUD hud = uis != null ? uis.playerHUD : null;
                _inside = hud != null ? hud.currentBuildingUI : null;
                if (_headlineField == null)
                {
                    _headlineField = GameContract.BuildingHeadline.Field;
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

        private static readonly int HotSearch = HotSpots.Slot("Core: SpecialNames.Search");

        // The game lists every building with a business name as "name, address" and finds it by the name; the list is made
        // again the game's way, with the special names, only when the game has made its own anew since.
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
                    _searchListField = GameContract.SearchBuildings.Field;
                    _searchDropField = GameContract.SearchDropdown.Field;
                    _optionsField = GameContract.DropdownOptions.Field;
                    _populate = GameContract.SearchPopulate.Method;
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
                CityBuildingController[] all = GameDlc.Open(city);
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
                        name = SpecialBuildings.FoundAs(building);   // a school or a hospital with no business name
                    }
                    string address = building.building.Address.ToFormattedString();
                    string type = building.building.BuildingType;
                    // Any other building too, by its address and type: MapRules.SearchLine is the one rule for a line.
                    options.Add(MapRules.SearchLine(name, address, string.IsNullOrEmpty(type) ? null : type.ToLower().GetLocalization(), out string word));
                    words.Add(word);
                    listed.Add(building);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }
                    string[] tenants = SpecialBuildings.TenantsOf(building);
                    for (int t = 0; tenants != null && t < tenants.Length; t++)
                    {
                        // Each company of a tower found by its own name too, with the tower's under it.
                        options.Add("<b><noparse>" + tenants[t] + "</noparse></b><br><noparse>" + name + ", " + building.building.Address.ToFormattedString() + "</noparse>");
                        words.Add(tenants[t]);
                        listed.Add(building);
                    }
                }
                // Words a building is found by besides its own name: an employer whose office is a board outside a shop, by the employer's name.
                _aliases.Clear();
                SpecialBuildings.AllAliases(_aliases);
                for (int a = 0; a < _aliases.Count; a++)
                {
                    SpecialBuildings.SearchAlias alias = _aliases[a];
                    options.Add("<b><noparse>" + alias.Word + "</noparse></b>" + (string.IsNullOrWhiteSpace(alias.Under) ? string.Empty : "<br><noparse>" + alias.Under + "</noparse>"));
                    words.Add(alias.Word);
                    listed.Add(alias.Building);
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
