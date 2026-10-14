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
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>The glyphs a map category can be given without bringing a sprite of its own. Each is drawn in code, once, white on nothing.</summary>
    public enum MapIcon
    {
        Shield, Target, Bus, Bed, Scales, Wheel, Bicycle, Wrench, Briefcase, Ballot, Cone, Cross, Star, Flag, Paper, Tower, Cloud, Coin, House, Heart, Custom
    }

    /// <summary>One place of the mod's on the city map.</summary>
    public sealed class MapPlace
    {
        /// <summary>Where it is in the city (its door, its stop). The marker floats a little above this.</summary>
        public Vector3 Position;
        /// <summary>Its name as shown: on the marker's name plate, on the destination marker, on the building's card.</summary>
        public string Name;
        /// <summary>Optional: the building it is. The game's own card for that building (under the mouse, when picked on the map) then carries the place's name, and the building lights up as <see cref="Lit"/> says.</summary>
        public CityBuildingController Building;
        /// <summary>
        /// Optional: the building that lights up on the open map while the place's row is ticked, as the game's own
        /// filters light theirs: its outline in the row's colour and its own marker with the row's icon, which stands
        /// for the place there instead of the mod's marker. <see cref="Building"/> if that is given; given alone for a
        /// place at a building that keeps its own card (a sign on a school's wall, a board at an employer's).
        /// </summary>
        public CityBuildingController Lit;
        /// <summary>Optional: its address, for anything that wants one (taken from the building if that is given).</summary>
        public Address Address;
        /// <summary>Optional, for a building: locale keys for the two lines of the card that would otherwise say what the unit is and that it is to let.</summary>
        public string CardTypeKey;
        public string CardStateKey;

        internal Transform Anchor;
        internal PointOfInterest Marker;
        internal bool Labelled;
        internal MapPlaces.Category Owner;
        internal int LitWhen;   // the look of the map it was last lit for (MapPlaces.Light)

        /// <summary>The building that lights up for it, if any.</summary>
        internal CityBuildingController LitBuilding => Lit != null ? Lit : Building;

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
        private const string Neighbourhoods = "common_neighborhoods";   // the game's own category of districts: a choice's line stands before it
        private const string Pref = "rlo_map_";
        private const float MarkerHeight = 4.5f;

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

            /// <summary>True while the row is ticked and offered: its places are on the map (and on the minimap).</summary>
            public bool Shown => On && Offered;

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
                string missing = Incomplete(place, this);
                if (missing != null)
                {
                    if (Said.Add(Id + ":" + missing))
                    {
                        Logger?.Warn("Map places: a place of '" + Id + "' (" + (place.Name ?? "no name") + ") is not put on the map: " + missing + ".");
                    }
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

        /// <summary>One row of a <see cref="Choice"/>: its label, its icon, and where its arrow button flies the map (none: no arrow).</summary>
        public sealed class ChoiceRow
        {
            public string Id;
            public string TitleKey;
            public Sprite Icon;
            public Func<Vector3?> Focus;

            internal CityMapFilter Row;
        }

        /// <summary>
        /// A set of rows of the mod's in the map's filter list that mark no place: switches under a category line of their
        /// own (with no "toggle all" and no folding), of which at most one is ticked at a time. Ticking one unticks the
        /// others; the one ticked is remembered in PlayerPrefs, not in the save, and <see cref="Changed"/> is told.
        /// </summary>
        public sealed class Choice
        {
            private readonly ChoiceRow[] _rows;

            internal Choice(string id, string categoryKey, ChoiceRow[] rows)
            {
                Id = id;
                CategoryKey = categoryKey;
                _rows = rows;
                string remembered = UnityEngine.PlayerPrefs.GetString(Pref + id, string.Empty);
                Picked = -1;
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i] != null && rows[i].Id == remembered)
                    {
                        Picked = i;
                    }
                }
            }

            public string Id { get; }
            public string CategoryKey { get; }
            public IReadOnlyList<ChoiceRow> Rows => _rows;
            /// <summary>The row ticked, or -1 (as remembered, whether or not the choice is offered now).</summary>
            public int Picked { get; private set; }
            /// <summary>Optional: false hides the line and its rows (the feature's own option is switched off).</summary>
            public Func<bool> Available;
            /// <summary>Told when the row ticked changes, with the new one (or -1).</summary>
            public Action<int> Changed;

            internal CityMapFilterCategory Line;
            internal bool Offered => Available == null || Available();
            internal bool HasRows => Line != null;

            /// <summary>Ticks a row (or none, with -1) as the player would.</summary>
            public void Pick(int index)
            {
                if (index < -1 || index >= _rows.Length)
                {
                    index = -1;
                }
                ShowTicks(index);
                if (Picked == index)
                {
                    return;
                }
                Picked = index;
                UnityEngine.PlayerPrefs.SetString(Pref + Id, index >= 0 ? _rows[index].Id : string.Empty);
                try
                {
                    Changed?.Invoke(index);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }

            internal void ShowTicks(int index)
            {
                for (int i = 0; i < _rows.Length; i++)
                {
                    if (_rows[i].Row != null)
                    {
                        _rows[i].Row.SetIsOnWithoutNotify(i == index);
                    }
                }
            }

            internal void Ticked(int index, bool on)
            {
                Pick(on ? index : Picked == index ? -1 : Picked);
            }

            internal void Forget()
            {
                Line = null;
                for (int i = 0; i < _rows.Length; i++)
                {
                    _rows[i].Row = null;
                }
            }
        }

        private static readonly List<Choice> Choices = new List<Choice>(2);
        private static readonly List<Category> Categories = new List<Category>(12);
        private static readonly Dictionary<CityBuildingController, MapPlace> ByBuilding = new Dictionary<CityBuildingController, MapPlace>();
        private static readonly List<MapPlace> Lights = new List<MapPlace>(64);   // the places that light up a building (MapPlace.Lit)
        private static bool _lightFailed;
        private static bool _gameLooking;   // the game's own look at every building was under way at the last frame
        private static int _lightLook = 1;  // counts the looks of the map: a place lit for an older one is lit again
        private static readonly Sprite[] Glyphs = new Sprite[(int)MapIcon.Custom];
        private static readonly HashSet<string> Said = new HashSet<string>(StringComparer.Ordinal);
        private static Driver _driver;
        private static CityMapFilters _filters;       // the list the rows were added to (a new one after every load)
        private static CityMapFilters _choiceFilters; // the list the choices' lines were added to
        private static bool _filterFailed;
        private static bool _choiceFailed;
        private static bool _labelFailed;
        private static bool _markFailed;
        private static FieldInfo _buttonField;
        private static FieldInfo _blobField;
        private static Action<bool> _onMap;
        private static Action<bool> _mapSeen;
        private static bool _mapOpen;
        private static bool _quiet;

        /// <summary>
        /// True while something is being picked on the open map (GameUi.MapPicker): the mod's own places are off it, as
        /// the game takes its filters off the map for its subway stations. Put back to false when the picking ends.
        /// </summary>
        public static bool Quiet
        {
            get { return _quiet; }
            set
            {
                if (_quiet != value)
                {
                    _quiet = value;
                    Refresh();
                }
            }
        }

        /// <summary>Every kind of place registered (read only: the minimap shows the ones that are <see cref="Category.Shown"/>).</summary>
        public static IReadOnlyList<Category> All => Categories;

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

        /// <summary>
        /// Adds a set of switches to the map's filter list (a <see cref="Choice"/>): a category line with the given title
        /// and one row per entry, placed just before the game's own neighbourhoods. Call once per city load. Asking again
        /// with the same id gives the same choice.
        /// </summary>
        public static Choice RegisterChoice(string id, string categoryKey, ChoiceRow[] rows)
        {
            for (int i = 0; i < Choices.Count; i++)
            {
                if (Choices[i].Id == id)
                {
                    return Choices[i];
                }
            }
            var choice = new Choice(id, categoryKey, rows ?? new ChoiceRow[0]);
            Choices.Add(choice);
            if (_driver == null)
            {
                var go = new GameObject("RealLifeOverhaul Map Places");
                _driver = go.AddComponent<Driver>();
            }
            return choice;
        }

        /// <summary>Looks at every category again: call when an option that a category's <see cref="Category.Available"/> reads has changed.</summary>
        public static void Refresh()
        {
            for (int i = 0; i < Categories.Count; i++)
            {
                Apply(Categories[i]);
            }
            for (int i = 0; i < Choices.Count; i++)
            {
                Offer(Choices[i]);
            }
        }

        // A choice that is not offered keeps its line in the list, folded to nothing: the game itself switches every
        // category line on whenever it applies a search (CityMapFilterCategory.UpdateFilterVisibility), and its rows are
        // hidden by their own "is available" test, which reads the choice's.
        private static void Offer(Choice choice)
        {
            CityMapFilterCategory line = choice.Line;
            if (line == null)
            {
                return;
            }
            bool offered = choice.Offered;
            var layout = line.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = line.gameObject.AddComponent<LayoutElement>();
            }
            if (layout.ignoreLayout == offered)
            {
                layout.ignoreLayout = !offered;
                line.transform.localScale = offered ? Vector3.one : Vector3.zero;
                line.ApplySearch(null);   // its rows shown or hidden by their test, as the category does after a search
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

        private static int _dropped;   // markers taken off the game's map since the driver's last look (for its self-check)

        /// <summary>
        /// True while the game's map can take a marker: the map's object is switched on, so a marker made now is woken by
        /// the engine. The game switches the whole map off during its time machine (sleeping, skipping time:
        /// TimeMachine.ToggleUiVisibility). A marker made then and destroyed before the map came back was never woken, so
        /// its own OnDestroy (which takes it out of CityMap.pois) never ran: it stayed in the game's list dead, and the
        /// game's PermanentPointsOfInterest.HandlePermanentPOIs, called from CityMap.LateUpdate, threw a
        /// NullReferenceException on it every frame from then on (0.16.0). A place given meanwhile waits for the driver's
        /// next look (once a second).
        /// </summary>
        internal static bool CanMark(CityManager city)
        {
            return city != null && city.cityMap != null && city.cityMap.poiTemplate != null && city.cityMap.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// One of the mod's markers off the game's map: taken out of the game's list of markers first (the marker's own
        /// OnDestroy does that only if the engine ever woke it), the game's two caches of that list rebuilt on the next frame
        /// (CityManager.UpdatePointOfInterests), then destroyed. Every marker the mod gives the game goes this way.
        /// </summary>
        internal static void Drop(PointOfInterest marker)
        {
            if (ReferenceEquals(marker, null))
            {
                return;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city != null && city.cityMap != null && city.cityMap.pois.Remove(marker))
            {
                city.UpdatePointOfInterests();
            }
            _dropped++;
            if (marker != null)
            {
                UnityEngine.Object.Destroy(marker.gameObject);
            }
        }

        /// <summary>What a place lacks to be put on the map (null: nothing): a place where it is, and a category with an icon.</summary>
        private static string Incomplete(MapPlace place, Category owner)
        {
            Vector3 p = place.Position;
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z))
            {
                return "it has no position";
            }
            if (owner.Icon == null)
            {
                return "its kind has no icon";
            }
            return null;
        }

        /// <summary>
        /// With Developer diagnostics on, after markers were taken off the map: the game's list of markers looked through
        /// once for any left dead (one look over a few hundred references, only after a change, never per frame).
        /// </summary>
        private static void CheckDropped(CityManager city)
        {
            if (_dropped == 0)
            {
                return;
            }
            int dropped = _dropped;
            _dropped = 0;
            if (!Meta.Dev.Verbose || city == null || city.cityMap == null)
            {
                return;
            }
            List<PointOfInterest> pois = city.cityMap.pois;
            int dead = 0;
            for (int i = 0; i < pois.Count; i++)
            {
                if (pois[i] == null)
                {
                    dead++;
                }
            }
            Logger?.Info("Map places check (markers taken off): " + dropped + " taken out of the game's list of " + pois.Count + " marker(s) before they went; "
                + dead + " dead marker(s) left in it" + (dead == 0 ? ": PASS" : " (not the mod's: its own are taken out first)"));
        }

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
            if (place.LitBuilding != null && !Lights.Contains(place))
            {
                Lights.Add(place);
                place.LitWhen = 0;
                if (_driver != null)
                {
                    _driver.enabled = true;
                }
            }
            if (_markFailed || _driver == null)
            {
                return;
            }
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (!CanMark(city))
                {
                    return;   // (looked at again by the driver: the map may not be there yet, or be switched off)
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
            if (Lights.Remove(place) && CityMap.IsOpen && _filters != null)
            {
                _filters.ApplyFilters();   // the game looks at its buildings again, and puts the light out
            }
            if (!ReferenceEquals(place.Marker, null) && !GameManager.isCitySceneBeingUnloaded)
            {
                Drop(place.Marker);
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
            Follow(category, open);
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
                    bool hide = !category.On;
                    if (marker.Permanent != offered)
                    {
                        marker.SetPermanent(offered);   // (switches the marker on, hidden or not)
                        if (offered)
                        {
                            marker.SetHidden(hide);
                            PermanentPointsOfInterest.UpdatePermanentPointsOfInterest();   // in the game's own list of permanent markers from the next frame
                        }
                    }
                    // A row that is not ticked hides its places in the street as on the open map: "off" means off. On the
                    // open map a place whose building is lit has the building's own marker instead.
                    hide |= open && (_quiet || Lighting(place));
                    if (marker.hidden != hide || hide && marker.gameObject.activeSelf)
                    {
                        marker.SetHidden(hide);
                    }
                }
                else
                {
                    bool show = open && offered && category.On && !_quiet && !Lighting(place);
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
                // The filters' private fields and methods, resolved once (Core/GameContract): null when a game update took one away.
                FieldInfo togglesField = GameContract.FilterToggles.Field;
                FieldInfo entriesField = GameContract.FilterEntries.Field;
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
                MethodInfo create = GameContract.FilterCreate.Method;
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
                    MethodInfo line = GameContract.FilterCreateLine.Method;
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
                    var data = new CityMapFilterData { isAvailable = Guard.Asked(() => category.Offered, false) };   // (asked by the game's map: Core/Guard)
                    Func<Vector3?> focus = Guard.Asked<Vector3?>(() => Nearest(category), null);
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
                    row.SetUp(category.TitleKey, Guard.Wrap<bool>(on => Ticked(category, on)), category.Icon, data, true, default, null, focus);
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
                    _sweptHeard = _sweptHeard ?? Guard.Listener<bool>(Swept);   // (made once, through the guard: a fault never reaches the game's own listeners: Core/Guard)
                    _sweptAllHeard = _sweptAllHeard ?? Guard.Listener<bool>(SweptAll);
                    under.ToggleAll.onValueChanged.RemoveListener(_sweptHeard);
                    under.ToggleAll.onValueChanged.AddListener(_sweptHeard);
                    var all = GameContract.FilterToggleAll.Field?.GetValue(filters) as Toggle;
                    if (all != null)
                    {
                        all.onValueChanged.RemoveListener(_sweptAllHeard);
                        all.onValueChanged.AddListener(_sweptAllHeard);
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

        // The lines and rows of the choices: made by the game's own two methods, as the categories' rows are, and taken out
        // of the game's table of rows at once. The line stands before the game's neighbourhoods (or at the end of the list
        // if that is not there), its rows under it; its "toggle all" and its fold are put away, for at most one of its rows
        // is ever ticked. If the game's list is not as expected the choice has no rows, with one line in the log.
        private static void AddChoiceRows(CityMapFilters filters)
        {
            if (_choiceFailed)
            {
                return;
            }
            try
            {
                var toggles = GameContract.FilterToggles.Field?.GetValue(filters) as Dictionary<string, Toggle>;   // (resolved once: Core/GameContract)
                var entries = GameContract.FilterEntries.Field?.GetValue(filters) as Dictionary<string, CityMapFilter>;
                MethodInfo create = GameContract.FilterCreate.Method;
                MethodInfo makeLine = GameContract.FilterCreateLine.Method;
                ParameterInfo[] parameters = create != null ? create.GetParameters() : null;
                if (toggles == null || entries == null || makeLine == null || parameters == null || parameters.Length != 7
                    || parameters[3].ParameterType != typeof(CityMapFilterCategory) || !parameters[4].ParameterType.IsValueType
                    || parameters[6].ParameterType != typeof(Func<Vector3?>))
                {
                    ChoiceFail("the methods that make a line and a row have changed");
                    return;
                }
                if (toggles.Count == 0)
                {
                    return;   // the game has not built its list yet: looked at again in a second
                }
                for (int c = 0; c < Choices.Count; c++)
                {
                    Choice choice = Choices[c];
                    if (choice.HasRows)
                    {
                        continue;
                    }
                    var line = makeLine.Invoke(filters, new object[] { choice.CategoryKey }) as CityMapFilterCategory;
                    if (line == null || line.ToggleAll == null)
                    {
                        ChoiceFail("a line came back empty");
                        return;
                    }
                    line.ToggleAll.gameObject.SetActive(false);
                    Transform fold = line.transform.Find("Collapse");
                    if (fold != null)
                    {
                        fold.gameObject.SetActive(false);   // (folding would write the line's name into the save's list of folded lines)
                    }
                    Transform list = line.transform.parent;
                    Transform before = list != null ? list.Find(Neighbourhoods) : null;
                    int at = before != null ? before.GetSiblingIndex() : -1;
                    if (at >= 0)
                    {
                        line.transform.SetSiblingIndex(at);
                    }
                    choice.Line = line;
                    var data = new CityMapFilterData { isAvailable = Guard.Asked(() => choice.Offered, false) };   // (asked by the game's map: Core/Guard)
                    for (int i = 0; i < choice.Rows.Count; i++)
                    {
                        ChoiceRow entry = choice.Rows[i];
                        int index = i;
                        string name = Pref + choice.Id + "_" + entry.Id;
                        object languageData = Activator.CreateInstance(parameters[4].ParameterType);
                        Func<Vector3?> focus = Guard.Asked(entry.Focus, null);
                        var row = create.Invoke(filters, new object[] { name, entry.Icon, data, line, languageData, null, focus }) as CityMapFilter;
                        if (row == null || row.Toggle == null)
                        {
                            ChoiceFail("a row came back empty");
                            return;
                        }
                        toggles.Remove(name);
                        entries.Remove(name);
                        row.SetUp(entry.TitleKey, Guard.Wrap<bool>(on => choice.Ticked(index, on)), entry.Icon, data, true, default, null, focus);
                        row.name = name;
                        entry.Row = row;
                        if (at >= 0)
                        {
                            row.transform.SetSiblingIndex(line.transform.GetSiblingIndex() + 1 + i);
                        }
                    }
                    choice.ShowTicks(choice.Picked);
                    line.UpdateToggleAllState();
                    Offer(choice);
                    Logger?.Info("Map places: the choice '" + choice.Id + "' added to the city map's filters with " + choice.Rows.Count + " row(s).");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ChoiceFail("an error (above)");
            }
        }

        private static void ChoiceFail(string why)
        {
            _choiceFailed = true;
            Logger?.Warn("Map places: the city map's filter list could not be given the mod's switches (" + why + "); they are left off the map.");
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
            // Said to the player once, with the one switch that still works: without rows nothing on the map turns them off.
            try
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Warning, "reallife_map_rows_failed", null, 14f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Refresh();
        }

        // A row was ticked or unticked by the player.
        private static void Ticked(Category category, bool on)
        {
            if (Said.Add("clicked " + category.Id))
            {
                Logger?.Info("Map places: the row '" + category.Id + "' " + (on ? "ticked" : "unticked") + " by the player (the first time this session); "
                             + category.Places.Count + " place(s), " + (category.Offered ? "offered" : "not offered") + ".");
            }
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
        private static UnityAction<bool> _sweptHeard, _sweptAllHeard;   // (the two switches' listeners, through the guard)

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

        // While the map is open the tick the player sees is what counts. A row's tick can be changed by a path that does
        // not tell it (a switch over many rows, another mod's), and the markers then went on following the tick as it
        // was remembered: unticked rows whose places stayed on the map. Said once a session per row, with what was seen.
        private static void Follow(Category category, bool open)
        {
            Toggle toggle = category.Toggle;
            if (!open || toggle == null || toggle.isOn == category.On)
            {
                return;
            }
            if (Said.Add(category.Id))
            {
                Logger?.Info("Map places: the row '" + category.Id + "' was " + (toggle.isOn ? "ticked" : "unticked")
                             + " without telling it; its places follow the tick from here.");
            }
            Remember(category, toggle.isOn);
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
                            _buttonField = GameContract.PoiButton.Field;   // (resolved once: Core/GameContract)
                            _blobField = GameContract.PoiBlob.Field;
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
                            click.onClick.AddListener(Guard.Listener(() => GoTo(place)));
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

        // The game's card for a building that is one of the mod's places: the place's name where the business name goes (the
        // building's own name instead, for a special building: Core/SpecialBuildings, which writes the same name), and (if
        // the place gives them) what it is and its state. The game writes all three again whenever it shows the card for
        // anything, so nothing has to be put back.
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
                string name = RealLifeOverhaul.SpecialBuildings.NameFor(building) ?? place.Name;
                if (card.businessNameLabel != null && !string.IsNullOrEmpty(name))
                {
                    card.businessNameLabel.gameObject.SetActive(true);
                    card.businessNameLabel.text = name;
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

        // ---------------------------------------------------------------- the buildings lit on the map

        // True if a place's building stands for it on the open map (lit as the game lights its own).
        private static bool Lighting(MapPlace place)
        {
            return !_lightFailed && place.LitBuilding != null;
        }

        // The buildings of the ticked rows, lit on the open map as the game's own filters light theirs (CityMapFilters
        // .ApplyFilters): the outline in the row's colour and the building's own marker with the row's icon. The game
        // looks at every building again whenever a filter changes, a few a frame, and puts out every light that is not
        // its own; a building of ours that it put out has its marker switched off, so it is lit again in the same frame,
        // before anything is drawn. A building whose marker the game always shows (one of the player's own) cannot be
        // told apart that way: it is lit again when each look is over. Nothing is lit where the game puts out all its
        // own lights: its choice of a subway station, the phone's full menu over the map, a picture of a building being
        // taken; nor while something is being picked on the map (Quiet). Once a frame while the map is open.
        private static void Light()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            var game = InstanceBehavior<GameManager>.Instance;
            if (_quiet || _lightFailed || Lights.Count == 0 || city == null || city.cityMap == null || city.cityMap.isSubwayMode || UI.Smartphone.FullMenu.IsOpen
                || (game != null && game.buildingOutdoorsCameraCaptureController != null && game.buildingOutdoorsCameraCaptureController.gameObject.activeSelf))
            {
                _gameLooking = false;
                return;
            }
            bool looking = CityMapFilters.Work.HasPendingWork;
            if (_gameLooking && !looking)
            {
                _lightLook++;   // the game's look at every building is over
                RealLifeOverhaul.SpecialBuildings.Unlist();   // the mod's buildings are not to let nor for sale: out of those rows (lit again below where a row of ours wants them)
            }
            _gameLooking = looking;
            for (int i = 0; i < Lights.Count; i++)
            {
                MapPlace place = Lights[i];
                Category category = place.Owner;
                CityBuildingController building = place.LitBuilding;
                if (category == null || building == null || !category.Shown)
                {
                    continue;
                }
                PointOfInterest poi = building.poi;
                if (place.LitWhen == _lightLook && poi != null && (poi.Permanent || poi.gameObject.activeSelf))
                {
                    continue;   // lit, as far as can be seen
                }
                try
                {
                    if (poi == null)
                    {
                        building.CreatePOI();
                    }
                    else if (poi.Permanent)
                    {
                        poi.SetHidden(false);
                    }
                    building.SetHighlight(true, (Color32)category.Colour);
                    building.poi.SetIcon(category.Icon, category.Colour);
                    place.LitWhen = _lightLook;
                }
                catch (Exception e)
                {
                    _lightFailed = true;
                    Logger?.Error(e);
                    Logger?.Warn("Map places: a building of the mod's could not be lit on the map; the mod's places keep their own markers there instead.");
                    Refresh();   // (their markers come back)
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- the driver

        private static readonly int HotOnMap = HotSpots.Slot("Core: MapPlaces.OnMap");
        private static void OnMap(bool open)
        {
            using var hotSpot = HotSpots.Time(HotOnMap);
            _mapOpen = open;
            _lightLook++;   // (every place is lit afresh on the map that has just opened)
            _gameLooking = false;
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
                for (int i = 0; i < Choices.Count; i++)
                {
                    if (open)
                    {
                        Choices[i].ShowTicks(Choices[i].Picked);   // as remembered, whatever was swept last time
                    }
                    Offer(Choices[i]);
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
            for (int i = 0; i < Choices.Count; i++)
            {
                Choices[i].Forget();
            }
            Choices.Clear();
            _choiceFailed = false;
            ByBuilding.Clear();
            Lights.Clear();
            _lightFailed = false;
            _gameLooking = false;
            if (_onMap != null)
            {
                GlobalEvents.onCityMapToggle -= _onMap;
            }
            _filters = null;
            _choiceFilters = null;
            _filterFailed = false;
            _labelFailed = false;
            _markFailed = false;
            _mapOpen = false;
            _driver = null;
        }

        /// <summary>
        /// The one object behind the map places: a look a second (in real time: the map pauses the game) that hooks the
        /// map's own event again after a load, adds the rows once the game's filter list is there, and puts names on
        /// markers that have appeared. Its only per-frame work is the building card's reference comparison and, while
        /// the map is open, a look at the buildings lit for the ticked rows (Light); it is switched off altogether while
        /// no place is a building.
        /// </summary>
        private sealed class Driver : MonoBehaviour
        {
            private static readonly FeatureGuard FrameGuard = Guard.For("navigation", "MapPlaces.Driver", typeof(MapPlaces.Driver));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

            private readonly WaitForSecondsRealtime _second = new WaitForSecondsRealtime(1f);
            private BuildingResume _card;
            private EntityController _carded;
            private bool _cardOff;   // the card's lines stood down: this game lacks something they name (Core/GameMembers)
            private int _hookIn;

            private void Start()
            {
                StartCoroutine(Guard.Routine(Look()));
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
                    bool lacking = false;
                    try
                    {
                        Tick();
                    }
                    catch (Exception e)
                    {
                        // A game that lacks something the look names would fail at every one: said once, and the look stops.
                        lacking = Guard.StandsDown(e, "the mod's places on the city map");
                        if (!lacking)
                        {
                            Logger?.Error(e);
                        }
                    }
                    if (lacking)
                    {
                        yield break;
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
                        _onMap = Guard.Wrap<bool>(OnMap);
                    }
                    EventHold.Keep(ref GlobalEvents.onCityMapToggle, _onMap, ref _mapSeen);   // put back only when the game's event is not the one last seen
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                _card = uis != null ? uis.buildingResume : null;
                enabled = ByBuilding.Count > 0 || Lights.Count > 0;
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
                if (!_choiceFailed && uis != null && uis.mapFilters != null && Choices.Count > 0)
                {
                    bool stale = !ReferenceEquals(_choiceFilters, uis.mapFilters);
                    if (stale)
                    {
                        for (int i = 0; i < Choices.Count; i++)
                        {
                            Choices[i].Forget();   // (a new list after a load: its lines and rows are made again)
                        }
                        _choiceFilters = uis.mapFilters;
                    }
                    bool missing = false;
                    for (int i = 0; i < Choices.Count && !missing; i++)
                    {
                        missing = !Choices[i].HasRows;
                    }
                    if (missing)
                    {
                        AddChoiceRows(uis.mapFilters);
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
                CheckDropped(InstanceBehavior<CityManager>.Instance);
                if (!ReferenceEquals(_card, null))
                {
                    _carded = null;   // written again once a second: the game rewrites the card on the hour
                }
            }

            public void LabelSoon()
            {
                StartCoroutine(Guard.Routine(Soon()));
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

            private static readonly int HotLateUpdate = HotSpots.Slot("Core: Driver.LateUpdate");
            // After the game's own work of the frame: its look at the buildings (GameManager.Update), and the card it writes
            // the moment a building comes under the mouse or is picked.
            private void LateUpdate()
            {
                using var hotSpot = HotSpots.Time(HotLateUpdate);
                if (FrameGuard.Off)
                {
                    enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
                if (_mapOpen)
                {
                    try
                    {
                        Light();
                    }
                    catch (Exception e)
                    {
                        _lightFailed = true;
                        Logger?.Error(e);
                    }
                }
                if (ReferenceEquals(_card, null) || _cardOff)
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
                        Card(_card, target);
                    }
                    catch (Exception e) when (Guard.StandsDown(e, "the mod's lines on the game's building card"))
                    {
                        _cardOff = true;
                    }
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
                case MapIcon.Paper:
                    // A newspaper: the sheet's edge, the masthead across the top, two columns of print and a picture.
                    Rect(true, 9f, 8f, 55f, 56f);
                    Rect(false, 13.5f, 12.5f, 50.5f, 51.5f);
                    Rect(true, 17f, 41f, 47f, 47.5f);
                    Rect(true, 17f, 33.5f, 30f, 36.5f);
                    Rect(true, 17f, 27f, 30f, 30f);
                    Rect(true, 17f, 20.5f, 30f, 23.5f);
                    Rect(true, 17f, 15f, 27f, 18f);
                    Rect(true, 34f, 22f, 47f, 36.5f);
                    Rect(true, 34f, 15f, 47f, 18f);
                    break;
                case MapIcon.Tower:
                    // An office tower: its block, three columns of windows, the door at its foot (y runs up).
                    Rect(true, 14f, 4f, 50f, 60f);
                    for (int floor = 0; floor < 5; floor++)
                    {
                        for (int column = 0; column < 3; column++)
                        {
                            Rect(false, 19f + column * 10f, 18f + floor * 8.4f, 25f + column * 10f, 23.5f + floor * 8.4f);
                        }
                    }
                    Rect(false, 28f, 4f, 36f, 13f);
                    break;
                case MapIcon.Cloud:
                    // A cloud over a rack's two lit servers.
                    Disc(true, 22f, 40f, 11f);
                    Disc(true, 36f, 45f, 13f);
                    Disc(true, 47f, 37f, 9f);
                    Rect(true, 12f, 30f, 54f, 40f);
                    Rect(true, 18f, 6f, 46f, 15f);
                    Rect(true, 18f, 18f, 46f, 27f);
                    Rect(false, 21f, 9f, 25f, 12f);
                    Rect(false, 21f, 21f, 25f, 24f);
                    break;
                case MapIcon.Coin:
                    // A coin: its rim, and a dollar sign struck on its face in six strokes.
                    Ring(32f, 32f, 27f, 5f);
                    Line(true, 23f, 43f, 41f, 43f, 4.5f);
                    Line(true, 23f, 43f, 23f, 32f, 4.5f);
                    Line(true, 23f, 32f, 41f, 32f, 4.5f);
                    Line(true, 41f, 32f, 41f, 21f, 4.5f);
                    Line(true, 41f, 21f, 23f, 21f, 4.5f);
                    Line(true, 32f, 13f, 32f, 51f, 4.5f);
                    break;
                case MapIcon.House:
                    // A house: its roof, its walls, the door (y runs up).
                    Poly(true, 4, 32, 32, 58, 60, 32);
                    Rect(true, 12f, 6f, 52f, 34f);
                    Rect(false, 27f, 6f, 37f, 22f);
                    Rect(true, 42f, 44f, 48f, 56f);
                    break;
                case MapIcon.Heart:
                    // A heart: two lobes and the point below them.
                    Disc(true, 21f, 40f, 13f);
                    Disc(true, 43f, 40f, 13f);
                    Poly(true, 9, 36, 55, 36, 32, 8);
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
