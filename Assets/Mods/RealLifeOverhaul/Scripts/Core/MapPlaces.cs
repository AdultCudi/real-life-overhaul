using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using City.CityMap;
using Streets;
using UI;
using UI.Guiders;
using UI.InGameUI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>The glyphs a map category can be given without bringing a sprite of its own. Each is drawn in code, once, white on nothing.</summary>
    public enum MapIcon
    {
        Shield, Target, Bus, Bed, Scales, Wheel, Bicycle, Wrench, Briefcase, Ballot, Cone, Cross, Star, Flag, Custom
    }

    /// <summary>One place of the mod's on the city map.</summary>
    public sealed class MapPlace
    {
        /// <summary>Where it is in the city (its door, its stop). The marker floats a little above this.</summary>
        public Vector3 Position;
        /// <summary>Its name as shown: on the marker's name plate, on the destination marker, on the building's card.</summary>
        public string Name;
        /// <summary>Optional: the building it is. The game's own card for that building (under the mouse, when picked on the map) then carries the place's name.</summary>
        public CityBuildingController Building;
        /// <summary>Optional: its address, for anything that wants one (taken from the building if that is given).</summary>
        public Address Address;
        /// <summary>Optional, for a building: locale keys for the two lines of the card that would otherwise say what the unit is and that it is to let.</summary>
        public string CardTypeKey;
        public string CardStateKey;

        internal Transform Anchor;
        internal PointOfInterest Marker;
        internal bool Labelled;
        internal MapPlaces.Category Owner;

        public MapPlace()
        {
        }

        public MapPlace(Vector3 position, string name, CityBuildingController building = null)
        {
            Position = position;
            Name = name;
            Building = building;
        }
    }

    /// <summary>
    /// The mod's places on the game's city map, the way the game's own are there. Every place a
    /// feature adds to the city (a police station, a bus stop, a shop of the mod's) is registered
    /// here; nothing else in the mod touches the map's markers or filters.
    ///
    ///   var stations = MapPlaces.Register("police", "reallife_law_map_stations", blue, MapIcon.Shield, onByDefault: true);
    ///   stations.Set(places);      // or Add / Remove / Clear, whenever the places change
    ///
    /// What a category gets:
    ///   a filter row   in the map's filter list, under "Special buildings" with the subway
    ///                  stations and the hospital: a real row of the game's (it is made by the
    ///                  game's own method from its own template, so it has the icon, the label,
    ///                  the tick box, the search box finds it, its category's switch and "toggle
    ///                  all" work on it, and its arrow button flies the map to the nearest place).
    ///                  Whether it is ticked is remembered in PlayerPrefs, not in the save.
    ///   markers        one of the game's own markers per place (CityMap.AddPoi), in the
    ///                  category's colour with its glyph, shown on the map while the row is
    ///                  ticked; for a category made with inStreet also in the street within
    ///                  100 m, as the game shows the player's own buildings.
    ///   names          the marker's name plate (which the game shows only for directions) is
    ///                  switched on with the place's name.
    ///   a destination  clicking a marker on the open map makes the place the destination (the
    ///                  game's own destination marker, with the name and the distance) and
    ///                  closes the map, as "set destination" does for a building. A place that
    ///                  is a building also has the game's whole card, with its own buttons.
    ///
    /// How it is done, and what happens if the game changes: the filter list is built by
    /// private code (CityMapFilters.CreateFilter), called by reflection; the row is then taken
    /// out of the game's own table of rows so that the save's list of ticked filters never sees
    /// its name. If any of that is not as expected it is logged once and the category has no
    /// row: its markers are then always on the map. Everything runs on the map's own events
    /// (opened, closed, a row ticked) and on one look a second; per frame there is one
    /// reference comparison, and only while some place is a building (the card). All of it is
    /// removed when the city unloads.
    /// </summary>
    public static class MapPlaces
    {
        private const string SpecialBuildings = "citymap_category_special_buildings";
        private const string OwnCategory = "reallife_map_category";
        private const string NextCategory = "common_real_estate";   // the category the game lists after the special buildings
        private const string Pref = "rlo_map_";
        private const float MarkerHeight = 4.5f;
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        public static IModLogger Logger;

        /// <summary>One kind of place: a row in the map's filter list and the markers that go with it.</summary>
        public sealed class Category
        {
            private readonly List<MapPlace> _places = new List<MapPlace>(8);

            internal Category(string id, string titleKey, Color colour, Sprite icon, bool onByDefault, bool inStreet)
            {
                Id = id;
                TitleKey = titleKey;
                Colour = colour;
                Icon = icon;
                InStreet = inStreet;
                On = UnityEngine.PlayerPrefs.GetInt(Pref + id, onByDefault ? 1 : 0) != 0;
            }

            public string Id { get; }
            public string TitleKey { get; }
            public Color Colour { get; }
            public Sprite Icon { get; }
            /// <summary>Also shown in the street within 100 m of the player, like the game's markers for the player's own buildings.</summary>
            public bool InStreet { get; }
            /// <summary>Whether its row is ticked (as remembered; true where the map has no row for it).</summary>
            public bool On { get; internal set; }
            /// <summary>Optional: false hides the row and the markers altogether (the feature's own option is switched off).</summary>
            public Func<bool> Available;
            public IReadOnlyList<MapPlace> Places => _places;

            internal Toggle Toggle;
            internal CityMapFilter Row;

            internal bool Offered => Settings.MapPlacesShown && (Available == null || Available());   // (the one switch over all of them: "The mod's places on the map")

            /// <summary>These places and no others.</summary>
            public void Set(IReadOnlyList<MapPlace> places)
            {
                Clear();
                for (int i = 0; places != null && i < places.Count; i++)
                {
                    Add(places[i]);
                }
            }

            public void Add(MapPlace place)
            {
                if (place == null || _places.Contains(place))
                {
                    return;
                }
                place.Owner = this;
                _places.Add(place);
                Mark(place);
                Apply(this);
            }

            public void Remove(MapPlace place)
            {
                if (place != null && _places.Remove(place))
                {
                    Unmark(place);
                }
            }

            public void Clear()
            {
                for (int i = _places.Count - 1; i >= 0; i--)
                {
                    Unmark(_places[i]);
                }
                _places.Clear();
            }

            /// <summary>The place moved or was renamed: its marker follows.</summary>
            public void Changed(MapPlace place)
            {
                if (place == null || !_places.Contains(place))
                {
                    return;
                }
                Unmark(place);
                Mark(place);
                Apply(this);
            }
        }

        private static readonly List<Category> Categories = new List<Category>(12);
        private static readonly Dictionary<CityBuildingController, MapPlace> ByBuilding = new Dictionary<CityBuildingController, MapPlace>();
        private static readonly Sprite[] Glyphs = new Sprite[(int)MapIcon.Custom];
        private static Driver _driver;
        private static CityMapFilters _filters;       // the list the rows were added to (a new one after every load)
        private static bool _filterFailed;
        private static bool _labelFailed;
        private static bool _markFailed;
        private static FieldInfo _buttonField;
        private static FieldInfo _blobField;
        private static Action<bool> _onMap;
        private static bool _mapOpen;

        /// <summary>True once the map's filter list has taken the mod's rows (false: every category's markers are simply always on the map).</summary>
        public static bool HasFilterRows => _filters != null && !_filterFailed;

        /// <summary>
        /// Adds a kind of place to the map: one row in its filter list and a marker for each place given to the category.
        /// Call once per city load (from a feature's own start-up). Asking again with the same id gives the same category.
        /// </summary>
        /// <param name="titleKey">The locale key of the row's label ("Police stations").</param>
        /// <param name="custom">With <see cref="MapIcon.Custom"/>: the sprite to use (white on nothing reads best: the marker supplies the colour).</param>
        /// <param name="inStreet">True to show the markers in the street as well, within 100 m.</param>
        public static Category Register(string id, string titleKey, Color colour, MapIcon icon, bool onByDefault, Sprite custom = null, bool inStreet = false)
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                if (Categories[i].Id == id)
                {
                    return Categories[i];
                }
            }
            var category = new Category(id, titleKey, colour, icon == MapIcon.Custom && custom != null ? custom : Glyph(icon), onByDefault, inStreet);
            Categories.Add(category);
            if (_driver == null)
            {
                var go = new GameObject("RealLifeOverhaul Map Places");
                _driver = go.AddComponent<Driver>();
            }
            return category;
        }

        /// <summary>Looks at every category again: call when an option that a category's <see cref="Category.Available"/> reads has changed.</summary>
        public static void Refresh()
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                Apply(Categories[i]);
            }
        }

        /// <summary>The place that is this building, or null.</summary>
        public static MapPlace Of(CityBuildingController building)
        {
            return !ReferenceEquals(building, null) && ByBuilding.TryGetValue(building, out MapPlace place) ? place : null;
        }

        /// <summary>Takes everything off the map now (the mod's city features are being unloaded).</summary>
        public static void Shutdown()
        {
            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver.gameObject);   // (its going disposes of the rest)
            }
        }

        // ---------------------------------------------------------------- markers

        private static void Mark(MapPlace place)
        {
            if (!ReferenceEquals(place.Building, null))
            {
                ByBuilding[place.Building] = place;
                if (place.Address == null && place.Building != null && place.Building.building != null)
                {
                    place.Address = place.Building.building.Address;
                }
            }
            if (_markFailed || _driver == null)
            {
                return;
            }
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (city == null || city.cityMap == null || city.cityMap.poiTemplate == null)
                {
                    return;   // (looked at again by the driver: the map may not be there yet)
                }
                var anchor = new GameObject("Place " + place.Name);
                anchor.transform.SetParent(_driver.transform, false);
                anchor.transform.position = place.Position + Vector3.up * MarkerHeight;
                place.Anchor = anchor.transform;
                place.Marker = city.cityMap.AddPoi(place.Anchor, place.Owner.Icon, place.Owner.Colour, place.Name ?? string.Empty);
                place.Labelled = false;
            }
            catch (Exception e)
            {
                _markFailed = true;
                Logger?.Error(e);
                Logger?.Warn("Map places: the game's map would not take a marker; the mod's places are left off the map.");
            }
        }

        private static void Unmark(MapPlace place)
        {
            if (!ReferenceEquals(place.Building, null) && ByBuilding.TryGetValue(place.Building, out MapPlace held) && held == place)
            {
                ByBuilding.Remove(place.Building);
            }
            if (place.Marker != null && !GameManager.isCitySceneBeingUnloaded)
            {
                UnityEngine.Object.Destroy(place.Marker.gameObject);
            }
            if (place.Anchor != null)
            {
                UnityEngine.Object.Destroy(place.Anchor.gameObject);
            }
            place.Marker = null;
            place.Anchor = null;
            place.Labelled = false;
        }

        // Every marker of a category as it should be now: on the open map while its row is ticked; in the street (for a
        // category that is shown there) whenever the feature is on. Called when something changes, never per frame.
        private static void Apply(Category category)
        {
            bool open = CityMap.IsOpen;
            bool offered = category.Offered;
            IReadOnlyList<MapPlace> places = category.Places;
            for (int i = 0; i < places.Count; i++)
            {
                MapPlace place = places[i];
                if (place.Marker == null)
                {
                    if (place.Anchor == null)
                    {
                        Mark(place);
                    }
                    if (place.Marker == null)
                    {
                        continue;
                    }
                }
                PointOfInterest marker = place.Marker;
                if (category.InStreet)
                {
                    // One of the game's permanent markers: it shows and hides it by itself (on the map, and near the player).
                    if (marker.Permanent != offered)
                    {
                        marker.SetPermanent(offered);
                    }
                    bool hide = open && !category.On;
                    if (marker.hidden != hide)
                    {
                        marker.SetHidden(hide);
                    }
                }
                else
                {
                    bool show = open && offered && category.On;
                    if (marker.gameObject.activeSelf != show)
                    {
                        marker.gameObject.SetActive(show);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- the filter rows

        private static void AddRows(CityMapFilters filters)
        {
            try
            {
                Type type = typeof(CityMapFilters);
                FieldInfo togglesField = type.GetField("_filterToggles", Hidden);
                FieldInfo entriesField = type.GetField("_filterEntries", Hidden);
                var toggles = togglesField != null ? togglesField.GetValue(filters) as Dictionary<string, Toggle> : null;
                var entries = entriesField != null ? entriesField.GetValue(filters) as Dictionary<string, CityMapFilter> : null;
                if (toggles == null || entries == null)
                {
                    Fail("its table of rows is not where it was");
                    return;
                }
                if (toggles.Count == 0)
                {
                    return;   // the game has not built its list yet: looked at again in a second
                }
                MethodInfo create = type.GetMethod("CreateFilter", Hidden);
                ParameterInfo[] parameters = create != null ? create.GetParameters() : null;
                if (parameters == null || parameters.Length != 7 || parameters[0].ParameterType != typeof(string) || parameters[1].ParameterType != typeof(Sprite)
                    || parameters[2].ParameterType != typeof(CityMapFilterData) || parameters[3].ParameterType != typeof(CityMapFilterCategory)
                    || !parameters[4].ParameterType.IsValueType || parameters[6].ParameterType != typeof(Func<Vector3?>))
                {
                    Fail("the method that makes a row has changed");
                    return;
                }
                CityMapFilterCategory under = filters.GetCategory(SpecialBuildings);
                bool own = under == null;
                if (own)
                {
                    MethodInfo line = type.GetMethod("CreateLine", Hidden);
                    under = line != null ? line.Invoke(filters, new object[] { OwnCategory }) as CityMapFilterCategory : null;
                }
                if (under == null)
                {
                    Fail("no category to put the rows under");
                    return;
                }
                _filters = filters;
                int added = 0;
                for (int i = 0; i < Categories.Count; i++)
                {
                    Category category = Categories[i];
                    if (category.Toggle != null)
                    {
                        continue;
                    }
                    string name = Pref + category.Id;
                    var data = new CityMapFilterData { isAvailable = () => category.Offered };
                    Func<Vector3?> focus = () => Nearest(category);
                    object languageData = Activator.CreateInstance(parameters[4].ParameterType);
                    var row = create.Invoke(filters, new object[] { name, category.Icon, data, under, languageData, null, focus }) as CityMapFilter;
                    if (row == null || row.Toggle == null)
                    {
                        Fail("a row came back empty");
                        return;
                    }
                    // The row is the mod's from here on: out of the game's own table (so "toggle all" does not write its name
                    // into the save's list of ticked filters), set up again not to write it there itself, and labelled.
                    toggles.Remove(name);
                    entries.Remove(name);
                    row.SetUp(category.TitleKey, on => Ticked(category, on), category.Icon, data, true, default, null, focus);
                    row.name = name;
                    row.Toggle.SetIsOnWithoutNotify(category.On);
                    category.Row = row;
                    category.Toggle = row.Toggle;
                    Transform list = row.transform.parent;
                    Transform next = !own && list != null ? list.Find(NextCategory) : null;
                    if (next != null)
                    {
                        row.transform.SetSiblingIndex(next.GetSiblingIndex());   // up from the very end of the list to the end of its category
                    }
                    added++;
                }
                if (added > 0)
                {
                    // The two switches that tick rows without telling them: the category's own, and "toggle all" at the top.
                    under.ToggleAll.onValueChanged.RemoveListener(Swept);
                    under.ToggleAll.onValueChanged.AddListener(Swept);
                    var all = type.GetField("_toggleAll", Hidden)?.GetValue(filters) as Toggle;
                    if (all != null)
                    {
                        all.onValueChanged.RemoveListener(SweptAll);
                        all.onValueChanged.AddListener(SweptAll);
                    }
                    under.UpdateToggleAllState();
                    Logger?.Info("Map places: " + added + " row(s) added to the city map's filters" + (own ? " (under a category of their own)." : " (under special buildings)."));
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Fail("an error (above)");
            }
        }

        private static void Fail(string why)
        {
            if (_filterFailed)
            {
                return;
            }
            _filterFailed = true;
            for (int i = 0; i < Categories.Count; i++)
            {
                if (Categories[i].Toggle == null)
                {
                    Categories[i].On = true;
                }
            }
            Logger?.Warn("Map places: the city map's filter list could not be given the mod's rows (" + why + "); the mod's places are always shown on the map instead.");
            Refresh();
        }

        // A row was ticked or unticked by the player.
        private static void Ticked(Category category, bool on)
        {
            Remember(category, on);
            Apply(category);
            try
            {
                if (_filters != null)
                {
                    _filters.ApplyFilters();   // (what the game's own rows do when ticked: its buildings are looked at again)
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The category's switch has gone over every row under it, the mod's included, without telling them.
        private static void Swept(bool on)
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                Category category = Categories[i];
                if (category.Toggle != null && category.Toggle.isOn != category.On)
                {
                    Remember(category, category.Toggle.isOn);
                    Apply(category);
                }
            }
        }

        // "Toggle all" goes over the game's table of rows, which the mod's rows are not in: they are ticked to match.
        private static void SweptAll(bool on)
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                Category category = Categories[i];
                if (category.Toggle != null && category.Offered)
                {
                    category.Toggle.SetIsOnWithoutNotify(on);
                    Remember(category, on);
                    Apply(category);
                }
            }
        }

        private static void Remember(Category category, bool on)
        {
            if (category.On != on)
            {
                category.On = on;
                UnityEngine.PlayerPrefs.SetInt(Pref + category.Id, on ? 1 : 0);
            }
        }

        // Where a row's arrow button flies the map to: the category's place nearest the player.
        private static Vector3? Nearest(Category category)
        {
            if (Helpers.PlayerHelper.PlayerController == null)
            {
                return null;
            }
            Vector3 from = Helpers.PlayerHelper.GetCityPosition();
            float best = float.MaxValue;
            Vector3? found = null;
            IReadOnlyList<MapPlace> places = category.Places;
            for (int i = 0; i < places.Count; i++)
            {
                float sqr = (places[i].Position - from).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    found = places[i].Position;
                }
            }
            return found;
        }

        // ---------------------------------------------------------------- names, and a click for a destination

        // A marker's name plate is off for everything but directions: for a place it is switched on, without the "dismiss"
        // button that directions have; and the marker's own blob is made to take a click. Once per marker, after the
        // marker has woken up for the first time (it hides its plate itself when it does).
        private static void Label()
        {
            if (_labelFailed)
            {
                return;
            }
            for (int c = 0; c < Categories.Count; c++)
            {
                IReadOnlyList<MapPlace> places = Categories[c].Places;
                for (int i = 0; i < places.Count; i++)
                {
                    MapPlace place = places[i];
                    PointOfInterest marker = place.Marker;
                    if (marker == null || !marker.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    if (place.Labelled)
                    {
                        marker.SetContainerActive(true);   // (does nothing if it is on)
                        continue;
                    }
                    try
                    {
                        if (_buttonField == null)
                        {
                            _buttonField = typeof(PointOfInterest).GetField("button", Hidden);
                            _blobField = typeof(PointOfInterest).GetField("blobImage", Hidden);
                        }
                        var dismiss = _buttonField != null ? _buttonField.GetValue(marker) as Button : null;
                        if (dismiss == null)
                        {
                            _labelFailed = true;
                            Logger?.Info("Map places: the game's marker has no name plate that can be shown without its button; the mod's markers stay without names.");
                            return;
                        }
                        dismiss.gameObject.SetActive(false);
                        marker.SetText(place.Name ?? string.Empty);
                        marker.SetContainerActive(true);
                        var blob = _blobField != null ? _blobField.GetValue(marker) as Image : null;
                        if (blob != null && blob.GetComponent<Button>() == null)
                        {
                            blob.raycastTarget = true;
                            var click = blob.gameObject.AddComponent<Button>();
                            click.targetGraphic = blob;
                            click.transition = Selectable.Transition.None;
                            click.onClick.AddListener(() => GoTo(place));
                        }
                        place.Labelled = true;
                    }
                    catch (Exception e)
                    {
                        _labelFailed = true;
                        Logger?.Error(e);
                        return;
                    }
                }
            }
        }

        // A marker was clicked on the open map: the place is the destination, and the map closes (as "set destination" does).
        private static void GoTo(MapPlace place)
        {
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (!CityMap.IsOpen || city == null || city.cityMap == null || city.cityMap.IsTaxiMode || city.cityMap.isSubwayMode
                    || place == null || place.Owner == null || InstanceBehavior<GuidersManager>.Instance == null)
                {
                    return;
                }
                GuidersManager.SetGuiderTarget(place.Position + Vector3.up * MarkerHeight, place.Name ?? string.Empty, place.Owner.Icon, place.Owner.Colour, DirectionGuiderType.Destination);
                city.cityMap.Toggle();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the building card

        // The game's card for a building that is one of the mod's places: the place's name where the business name goes, and
        // (if the place gives them) what it is and its state. The game writes all three again whenever it shows the card
        // for anything, so nothing has to be put back.
        private static void Card(BuildingResume card, EntityController target)
        {
            var building = target as CityBuildingController;
            MapPlace place = Of(building);
            if (place == null || place.Owner == null || !place.Owner.Offered)
            {
                return;
            }
            try
            {
                if (card.businessNameLabel != null && !string.IsNullOrEmpty(place.Name))
                {
                    card.businessNameLabel.gameObject.SetActive(true);
                    card.businessNameLabel.text = place.Name;
                }
                if (card.businessTypeLabel != null && !string.IsNullOrEmpty(place.CardTypeKey))
                {
                    card.businessTypeLabel.Key = place.CardTypeKey;
                }
                if (card.openStateLabel != null && !string.IsNullOrEmpty(place.CardStateKey))
                {
                    card.openStateLabel.Key = place.CardStateKey;
                    card.openStateLabel.Prefix = string.Empty;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ByBuilding.Clear();   // (the card is not as expected: left alone from here on)
            }
        }

        // ---------------------------------------------------------------- the driver

        private static void OnMap(bool open)
        {
            _mapOpen = open;
            try
            {
                for (int i = 0; i < Categories.Count; i++)
                {
                    Category category = Categories[i];
                    if (open && category.Toggle != null)
                    {
                        category.Toggle.SetIsOnWithoutNotify(category.On);   // as remembered, whatever was swept last time
                    }
                    Apply(category);
                }
                if (open && _driver != null)
                {
                    _driver.LabelSoon();
                    if (_filters != null && Categories.Count > 0 && Categories[0].Row != null && Categories[0].Row.category != null)
                    {
                        Categories[0].Row.category.UpdateToggleAllState();
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static void Dispose()
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                try
                {
                    Categories[i].Clear();
                }
                catch (Exception)
                {
                    // (the city is going: its markers go with it)
                }
            }
            Categories.Clear();
            ByBuilding.Clear();
            if (_onMap != null)
            {
                GlobalEvents.onCityMapToggle -= _onMap;
            }
            _filters = null;
            _filterFailed = false;
            _labelFailed = false;
            _markFailed = false;
            _mapOpen = false;
            _driver = null;
        }

        /// <summary>
        /// The one object behind the map places: a look a second (in real time: the map pauses the game) that hooks the
        /// map's own event again after a load, adds the rows once the game's filter list is there, and puts names on
        /// markers that have appeared. Its only per-frame work is the building card's reference comparison, and it is
        /// switched off altogether while no place is a building.
        /// </summary>
        private sealed class Driver : MonoBehaviour
        {
            private readonly WaitForSecondsRealtime _second = new WaitForSecondsRealtime(1f);
            private BuildingResume _card;
            private EntityController _carded;
            private int _hookIn;

            private void Start()
            {
                StartCoroutine(Look());
            }

            private void OnDestroy()
            {
                if (_driver == this)
                {
                    Dispose();
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
                        Logger?.Error(e);
                    }
                    yield return _second;
                }
            }

            private void Tick()
            {
                if (SaveGameManager.Current == null)
                {
                    return;
                }
                // The game clears its event hub on every load: the hook is kept attached.
                if (--_hookIn <= 0)
                {
                    _hookIn = 3;
                    if (_onMap == null)
                    {
                        _onMap = OnMap;
                    }
                    GlobalEvents.onCityMapToggle -= _onMap;
                    GlobalEvents.onCityMapToggle += _onMap;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                _card = uis != null ? uis.buildingResume : null;
                enabled = ByBuilding.Count > 0;
                if (!_filterFailed && uis != null && uis.mapFilters != null)
                {
                    bool missing = !ReferenceEquals(_filters, uis.mapFilters);
                    for (int i = 0; i < Categories.Count && !missing; i++)
                    {
                        missing = Categories[i].Toggle == null;
                    }
                    if (missing)
                    {
                        if (!ReferenceEquals(_filters, uis.mapFilters))
                        {
                            for (int i = 0; i < Categories.Count; i++)
                            {
                                Categories[i].Toggle = null;
                                Categories[i].Row = null;
                            }
                        }
                        AddRows(uis.mapFilters);
                    }
                }
                // Markers that could not be made when their places were given (the map was not there yet), and their names.
                if (CityMap.IsOpen != _mapOpen)
                {
                    OnMap(CityMap.IsOpen);   // (the event was missed: the hook had not been put back yet)
                }
                else
                {
                    Refresh();
                }
                Label();
                if (!ReferenceEquals(_card, null))
                {
                    _carded = null;   // written again once a second: the game rewrites the card on the hour
                }
            }

            public void LabelSoon()
            {
                StartCoroutine(Soon());
            }

            private static IEnumerator Soon()
            {
                yield return null;
                yield return null;
                try
                {
                    Label();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }

            // After the game has written the card (it does so the moment a building comes under the mouse or is picked).
            private void LateUpdate()
            {
                if (ReferenceEquals(_card, null))
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
                    Card(_card, target);
                }
            }
        }

        // ---------------------------------------------------------------- the glyphs

        /// <summary>One of the built-in glyphs as a sprite: white on nothing, 64 pixels square, drawn the first time it is asked for.</summary>
        public static Sprite Glyph(MapIcon icon)
        {
            int index = Mathf.Clamp((int)icon, 0, Glyphs.Length - 1);
            if (Glyphs[index] == null)
            {
                Glyphs[index] = Draw((MapIcon)index);
            }
            return Glyphs[index];
        }

        private const int Size = 64;
        private const int Fine = 4;                 // samples a pixel each way: soft edges without a filter
        private const int Grid = Size * Fine;

        private static bool[] _ink;

        private static Sprite Draw(MapIcon icon)
        {
            _ink = new bool[Grid * Grid];
            switch (icon)
            {
                case MapIcon.Shield:
                    Poly(true, 10, 57, 54, 57, 54, 34, 32, 6, 10, 34);
                    Star(false, 32f, 37f, 12f, 5f);
                    break;
                case MapIcon.Target:
                    Ring(32f, 32f, 27f, 5f);
                    Ring(32f, 32f, 15f, 5f);
                    Disc(true, 32f, 32f, 4.5f);
                    Line(true, 32f, 61f, 32f, 50f, 4f);
                    Line(true, 32f, 3f, 32f, 14f, 4f);
                    Line(true, 3f, 32f, 14f, 32f, 4f);
                    Line(true, 61f, 32f, 50f, 32f, 4f);
                    break;
                case MapIcon.Bus:
                    Rect(true, 8f, 18f, 56f, 54f);
                    Rect(false, 13f, 36f, 51f, 48f);
                    Rect(false, 30f, 36f, 34f, 48f);
                    Rect(true, 30.8f, 36f, 33.2f, 48f);
                    Disc(true, 20f, 17f, 7f);
                    Disc(true, 44f, 17f, 7f);
                    Disc(false, 20f, 17f, 3f);
                    Disc(false, 44f, 17f, 3f);
                    Rect(false, 12f, 25f, 19f, 29f);
                    Rect(false, 45f, 25f, 52f, 29f);
                    break;
                case MapIcon.Bed:
                    Rect(true, 6f, 18f, 58f, 31f);
                    Rect(true, 6f, 18f, 12f, 48f);
                    Rect(true, 15f, 31f, 30f, 39f);
                    Rect(true, 6f, 9f, 11f, 18f);
                    Rect(true, 53f, 9f, 58f, 18f);
                    Rect(true, 52f, 18f, 58f, 37f);
                    break;
                case MapIcon.Scales:
                    Line(true, 32f, 10f, 32f, 54f, 4f);
                    Line(true, 10f, 48f, 54f, 48f, 4f);
                    Rect(true, 19f, 6f, 45f, 12f);
                    Line(true, 10f, 48f, 4f, 30f, 2.4f);
                    Line(true, 10f, 48f, 16f, 30f, 2.4f);
                    Poly(true, 2, 30, 18, 30, 14, 23, 6, 23);
                    Line(true, 54f, 48f, 48f, 30f, 2.4f);
                    Line(true, 54f, 48f, 60f, 30f, 2.4f);
                    Poly(true, 46, 30, 62, 30, 58, 23, 50, 23);
                    Disc(true, 32f, 55f, 4f);
                    break;
                case MapIcon.Wheel:
                    Ring(32f, 32f, 27f, 6f);
                    Disc(true, 32f, 32f, 7.5f);
                    Line(true, 32f, 32f, 8f, 34f, 5f);
                    Line(true, 32f, 32f, 56f, 34f, 5f);
                    Line(true, 32f, 32f, 32f, 8f, 5f);
                    break;
                case MapIcon.Bicycle:
                    Ring(15f, 22f, 12f, 4f);
                    Ring(49f, 22f, 12f, 4f);
                    Line(true, 15f, 22f, 26f, 42f, 3.2f);
                    Line(true, 26f, 42f, 44f, 42f, 3.2f);
                    Line(true, 44f, 42f, 49f, 22f, 3.2f);
                    Line(true, 26f, 42f, 33f, 22f, 3.2f);
                    Line(true, 33f, 22f, 44f, 42f, 3.2f);
                    Line(true, 15f, 22f, 33f, 22f, 3.2f);
                    Line(true, 21f, 47f, 31f, 47f, 3.6f);
                    Line(true, 26f, 42f, 26f, 47f, 3f);
                    Line(true, 44f, 42f, 43f, 50f, 3f);
                    Line(true, 40f, 50f, 49f, 51f, 3.6f);
                    break;
                case MapIcon.Wrench:
                    Line(true, 17f, 17f, 45f, 45f, 9f);
                    Disc(true, 47f, 47f, 12.5f);
                    Poly(false, 46, 46, 62, 50, 62, 62, 50, 62);
                    Disc(true, 15f, 15f, 8f);
                    Disc(false, 15f, 15f, 3.2f);
                    break;
                case MapIcon.Briefcase:
                    Rect(true, 7f, 12f, 57f, 44f);
                    Rect(true, 23f, 44f, 41f, 53f);
                    Rect(false, 27f, 44f, 37f, 49f);
                    Rect(false, 7f, 28.5f, 57f, 31f);
                    Rect(true, 28f, 26f, 36f, 34f);
                    break;
                case MapIcon.Ballot:
                    Rect(true, 9f, 6f, 55f, 36f);
                    Rect(false, 20f, 30f, 44f, 33f);
                    Poly(true, 25, 33, 41, 33, 45, 58, 29, 60);
                    Rect(false, 20f, 33f, 44f, 36f);
                    Rect(true, 9f, 34f, 20f, 36f);
                    Rect(true, 44f, 34f, 55f, 36f);
                    break;
                case MapIcon.Cone:
                    Poly(true, 27, 58, 37, 58, 48, 14, 16, 14);
                    Rect(true, 8f, 7f, 56f, 14f);
                    Rect(false, 16f, 30f, 48f, 36f);
                    Rect(false, 16f, 44f, 48f, 48f);
                    break;
                case MapIcon.Star:
                    Star(true, 32f, 30f, 29f, 12f);
                    break;
                case MapIcon.Flag:
                    Line(true, 14f, 6f, 14f, 58f, 5f);
                    Poly(true, 16, 56, 54, 44, 16, 30);
                    Rect(true, 7f, 5f, 23f, 9f);
                    break;
                default:
                    Rect(true, 24f, 7f, 40f, 57f);
                    Rect(true, 7f, 24f, 57f, 40f);
                    break;
            }
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int inked = 0;
                    for (int sy = 0; sy < Fine; sy++)
                    {
                        for (int sx = 0; sx < Fine; sx++)
                        {
                            inked += _ink[(y * Fine + sy) * Grid + x * Fine + sx] ? 1 : 0;
                        }
                    }
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(inked * 255 / (Fine * Fine)));
                }
            }
            _ink = null;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "RealLifeOverhaul Map " + icon };
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            sprite.name = texture.name;
            return sprite;
        }

        // The drawing board is Size units square, y up; each call inks (or wipes) whatever its shape covers.
        private static void Each(bool ink, float x0, float y0, float x1, float y1, Func<float, float, bool> covers)
        {
            int fromX = Mathf.Clamp(Mathf.FloorToInt(x0 * Fine), 0, Grid - 1);
            int toX = Mathf.Clamp(Mathf.CeilToInt(x1 * Fine), 0, Grid - 1);
            int fromY = Mathf.Clamp(Mathf.FloorToInt(y0 * Fine), 0, Grid - 1);
            int toY = Mathf.Clamp(Mathf.CeilToInt(y1 * Fine), 0, Grid - 1);
            for (int y = fromY; y <= toY; y++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    if (covers((x + 0.5f) / Fine, (y + 0.5f) / Fine))
                    {
                        _ink[y * Grid + x] = ink;
                    }
                }
            }
        }

        private static void Rect(bool ink, float x0, float y0, float x1, float y1)
        {
            Each(ink, x0, y0, x1, y1, (x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1);
        }

        private static void Disc(bool ink, float cx, float cy, float radius)
        {
            Each(ink, cx - radius, cy - radius, cx + radius, cy + radius, (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius);
        }

        private static void Ring(float cx, float cy, float radius, float width)
        {
            float inner = (radius - width) * (radius - width);
            Each(true, cx - radius, cy - radius, cx + radius, cy + radius, (x, y) =>
            {
                float sqr = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                return sqr <= radius * radius && sqr >= inner;
            });
        }

        private static void Line(bool ink, float x0, float y0, float x1, float y1, float width)
        {
            float half = width * 0.5f;
            float dx = x1 - x0, dy = y1 - y0;
            float length = Mathf.Max(0.0001f, dx * dx + dy * dy);
            Each(ink, Mathf.Min(x0, x1) - half, Mathf.Min(y0, y1) - half, Mathf.Max(x0, x1) + half, Mathf.Max(y0, y1) + half, (x, y) =>
            {
                float along = Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / length);
                float px = x0 + dx * along - x, py = y0 + dy * along - y;
                return px * px + py * py <= half * half;
            });
        }

        private static void Poly(bool ink, params int[] corners)
        {
            int count = corners.Length / 2;
            Each(ink, 0f, 0f, Size, Size, (x, y) =>
            {
                bool odd = false;
                for (int i = 0, j = count - 1; i < count; j = i++)
                {
                    float xi = corners[i * 2], yi = corners[i * 2 + 1], xj = corners[j * 2], yj = corners[j * 2 + 1];
                    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    {
                        odd = !odd;
                    }
                }
                return odd;
            });
        }

        private static void Star(bool ink, float cx, float cy, float outer, float inner)
        {
            var corners = new float[20];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = (i & 1) == 0 ? outer : inner;
                corners[i * 2] = cx + Mathf.Cos(angle) * radius;
                corners[i * 2 + 1] = cy + Mathf.Sin(angle) * radius;
            }
            Each(ink, cx - outer, cy - outer, cx + outer, cy + outer, (x, y) =>
            {
                bool odd = false;
                for (int i = 0, j = 9; i < 10; j = i++)
                {
                    float xi = corners[i * 2], yi = corners[i * 2 + 1], xj = corners[j * 2], yj = corners[j * 2 + 1];
                    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    {
                        odd = !odd;
                    }
                }
                return odd;
            });
        }
    }
}
