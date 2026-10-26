using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using City.CityMap;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>The glyphs a map category can have without a sprite of its own, each drawn in code once, white on nothing.</summary>
    public enum MapIcon
    {
        Shield, Target, Bus, Bed, Scales, Wheel, Bicycle, Wrench, Briefcase, Ballot, Cone, Cross, Star, Flag, Paper, Tower, Cloud, Coin, House, Heart, Mortarboard, Custom
    }

    /// <summary>One place of the mod's on the city map.</summary>
    public sealed class MapPlace
    {
        /// <summary>Where it is in the city (its door, its stop); the marker floats a little above.</summary>
        public Vector3 Position;
        /// <summary>Its name on the marker's plate, the destination marker and the building's card.</summary>
        public string Name;
        /// <summary>Optional: the building it is, whose card then carries the place's name.</summary>
        public CityBuildingController Building;
        /// <summary>
        /// Optional: the building lit on the open map while the place's row is ticked, as the game's filters light theirs,
        /// standing for the place instead of the mod's marker. <see cref="Building"/> if that is given.
        /// </summary>
        public CityBuildingController Lit;
        /// <summary>Optional: its address; taken from the building if that is given.</summary>
        public Address Address;
        /// <summary>Optional, for a building: locale keys for the card's two lines on what the unit is and that it is to let.</summary>
        public string CardTypeKey;
        public string CardStateKey;
        /// <summary>
        /// Optional: a name the place's building (<see cref="Lit"/> or <see cref="Building"/>) is also found by in the city map's search, as an
        /// employer is by its name when its office is a board outside a shop; <see cref="AliasUnder"/> is the line shown under it.
        /// </summary>
        public string Alias;
        public string AliasUnder;

        internal Transform Anchor;
        internal PointOfInterest Marker;
        internal bool Labelled;
        internal MapPlaces.Category Owner;
        internal int LitWhen;   // the look of the map it was last lit for

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
    /// The mod's places on the game's city map, the way the game's own are there; nothing else in the mod touches the
    /// map's markers or filters. <c>MapPlaces.Register("police", "reallife_law_map_stations", blue, MapIcon.Shield,
    /// onByDefault: true).Set(places);</c>
    /// A category gets a real row of the game's filter list under "Special buildings" (made by the game's own private
    /// CreateFilter, then taken out of its table so the save never sees it; ticked or not in PlayerPrefs), one of the
    /// game's markers per place with its name, shown while the row is ticked (inStreet: also within 100 m in the
    /// street), and a click on a marker that sets the destination. If the game's list is not as expected, that is logged
    /// once and the markers are always shown. Everything is removed when the city unloads.
    /// COST: the map's own events and one look a second; per frame a reference comparison while a place is a building.
    /// </summary>
    public static partial class MapPlaces
    {
        private const string SpecialBuildings = "citymap_category_special_buildings";
        private const string OwnCategory = "reallife_map_category";
        private const string NextCategory = "common_real_estate";   // the category the game lists after the special buildings
        private const string Neighbourhoods = "common_neighborhoods";   // the game's districts: a choice's line stands before it
        private const string Pref = "rlo_map_";
        private const float MarkerHeight = 4.5f;   // metres above the place

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
            /// <summary>Also shown in the street within 100 m of the player, as the game shows the player's own buildings.</summary>
            public bool InStreet { get; }
            /// <summary>Whether its row is ticked, as remembered; true where the map has no row for it.</summary>
            public bool On { get; internal set; }
            /// <summary>Optional: false hides the row and the markers, when the feature's own option is off.</summary>
            public Func<bool> Available;
            public IReadOnlyList<MapPlace> Places => _places;

            internal Toggle Toggle;
            internal CityMapFilter Row;

            internal bool Offered => Settings.MapPlacesShown && (Available == null || Available());

            /// <summary>True while the row is ticked and offered: its places are on the map and the minimap.</summary>
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

        private static readonly List<Choice> Choices = new List<Choice>(2);
        private static readonly List<Category> Categories = new List<Category>(12);
        private static readonly Dictionary<CityBuildingController, MapPlace> ByBuilding = new Dictionary<CityBuildingController, MapPlace>();
        private static readonly List<MapPlace> Lights = new List<MapPlace>(64);   // the places that light up a building
        private static bool _lightFailed;
        private static bool _gameLooking;   // the game's look at every building was under way last frame
        private static int _lightLook = 1;  // counts the map's looks: a place lit for an older one is lit again
        private static readonly Sprite[] Glyphs = new Sprite[(int)MapIcon.Custom];
        private static readonly HashSet<string> Said = new HashSet<string>(StringComparer.Ordinal);
        private static Driver _driver;
        private static CityMapFilters _filters;       // the list the rows were added to, a new one after every load
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
        /// True while something is picked on the open map (GameUi.MapPicker): the mod's places are off it, as the game
        /// takes its filters off for its subway stations.
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

        /// <summary>Every kind of place registered; the minimap shows those <see cref="Category.Shown"/>.</summary>
        public static IReadOnlyList<Category> All => Categories;

        /// <summary>
        /// Adds a kind of place: a filter row and a marker per place. Call once per city load; the same id gives the same
        /// category. <paramref name="custom"/>: the sprite for <see cref="MapIcon.Custom"/>, best white on nothing.
        /// </summary>
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

        /// <summary>Looks at every category again: call when an option a <see cref="Category.Available"/> reads has changed.</summary>
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

        /// <summary>The place that is this building, or null.</summary>
        public static MapPlace Of(CityBuildingController building)
        {
            return !ReferenceEquals(building, null) && ByBuilding.TryGetValue(building, out MapPlace place) ? place : null;
        }

        /// <summary>Takes everything off the map now: the mod's city features are being unloaded.</summary>
        public static void Shutdown()
        {
            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver.gameObject);   // its going disposes of the rest
            }
        }

        private static int _dropped;   // markers taken off since the driver's last look, for its self-check

        /// <summary>
        /// True while the map's object is switched on, so a marker made now is woken by the engine. The game switches the
        /// map off during its time machine; a marker made then and destroyed before it came back was never woken, so it
        /// stayed in CityMap.pois dead and the game threw on it every frame. A place given meanwhile waits a second.
        /// </summary>
        internal static bool CanMark(CityManager city)
        {
            return city != null && city.cityMap != null && city.cityMap.poiTemplate != null && city.cityMap.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Takes one of the mod's markers off the game's map: out of its list first (the marker's own OnDestroy does that
        /// only if it was ever woken), the list's caches rebuilt, then destroyed. Every marker of the mod's goes this way.
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

        /// <summary>What a place lacks to be put on the map, a position or an icon; null for nothing.</summary>
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

        /// <summary>With Developer diagnostics on, after markers were taken off: the game's list looked through once for dead ones.</summary>
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
                    return;   // looked at again by the driver
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
            if (Lights.Remove(place))
            {
                Refilter(_filters);   // the game looks at its buildings again, and puts the light out
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

        // Every marker of a category as it should be now: on the open map while its row is ticked, in the street (InStreet)
        // while the feature is on. On a change, never per frame.
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
                    // A permanent marker: the game shows and hides it by itself, on the map and near the player.
                    bool hide = !category.On;
                    if (marker.Permanent != offered)
                    {
                        marker.SetPermanent(offered);   // switches the marker on, hidden or not
                        if (offered)
                        {
                            marker.SetHidden(hide);
                            PermanentPointsOfInterest.UpdatePermanentPointsOfInterest();   // in the game's list from the next frame
                        }
                    }
                    // An unticked row hides its places in the street too. On the open map a lit building's own marker
                    // stands in.
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

        /// <summary>
        /// The game's map filters applied again, as it does after a change to its buildings, only while the open map is
        /// there: ApplyFilters reads CityManager, gone while the city unloads. A fault goes no further. Every call of the
        /// mod's comes here.
        /// </summary>
        internal static void Refilter(CityMapFilters filters)
        {
            try
            {
                if (GameManager.isCitySceneBeingUnloaded || filters == null || !CityMap.IsOpen)
                {
                    return;
                }
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (city != null && city.cityMap != null)
                {
                    filters.ApplyFilters();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // True if a place's building stands for it on the open map.
        private static bool Lighting(MapPlace place)
        {
            return !_lightFailed && place.LitBuilding != null;
        }

        // The ticked rows' buildings, lit as the game's filters light theirs: outline and marker in the row's colour and
        // icon. The game looks at every building again after a filter change and puts out lights not its own; one it put
        // out has its marker off, so it is lit again that frame (one whose marker always shows, after each look). Nothing
        // is lit where the game puts out its own (subway pick, the full menu, a building photo) nor while Quiet.
        // Once a frame while the map is open.
        private static void Light()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            var game = InstanceBehavior<GameManager>.Instance;
            bool idle = Lights.Count == 0 && !_ownedDue && !_gameLooking && !CityMapFilters.Work.HasPendingWork;   // nothing to light, nothing to wait for
            if (_quiet || _lightFailed || idle || city == null || city.cityMap == null || city.cityMap.isSubwayMode || UI.Smartphone.FullMenu.IsOpen
                || (game != null && game.buildingOutdoorsCameraCaptureController != null && game.buildingOutdoorsCameraCaptureController.gameObject.activeSelf))
            {
                _gameLooking = false;
                return;
            }
            bool looking = CityMapFilters.Work.HasPendingWork;
            if (_gameLooking && !looking)
            {
                _lightLook++;   // the game's look at every building is over
                RealLifeOverhaul.SpecialBuildings.Unlist();   // the mod's buildings out of the to-let rows, lit again below where ours want
                OwnedDue();
            }
            _gameLooking = looking;
            if (_ownedDue && !looking)
            {
                LightOwned();   // the player's buildings under their type rows, once, after the game's look
            }
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
                    Refresh();   // their markers come back
                    return;
                }
            }
        }

        private static readonly int HotOnMap = HotSpots.Slot("Core: MapPlaces.OnMap");
        private static void OnMap(bool open)
        {
            using var hotSpot = HotSpots.Time(HotOnMap);
            _mapOpen = open;
            _lightLook++;   // every place lit afresh on the map just opened
            _gameLooking = false;
            if (open)
            {
                OwnedDue();   // and the player's buildings under their type rows, once the game's look is over
            }
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
                    // the city is unloading: its markers go with it
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
            _ownedFailed = false;
            _ownedDue = false;
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

    }
}
