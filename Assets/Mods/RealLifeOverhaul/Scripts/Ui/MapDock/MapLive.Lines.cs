using System;
using System.Collections.Generic;
using Buildings;
using City.CityMap;
using Helpers;
using Localizor;
using Streets;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The filters' side of the live dock: the game's groups and lines read into the kit's rows, the rail, the quick chips, the counts and the station list.</summary>
    internal sealed partial class MapLive
    {
        private const string Mod = "reallife";   // the start of every key; a chip's key is made of parts put after it, so the text check reads only whole keys

        private sealed class Cat
        {
            public CityMapFilterCategory Obj;
            public string Key, Name;
            public bool Collapsed, Foreign;
            public int On, Total, Sibling;
            public readonly List<LineInfo> Lines = new List<LineInfo>(16);
        }

        private sealed class LineInfo
        {
            public CityMapFilter Obj;
            public string Key, Name;
            public bool On, Available, Shown, Locate, Foreign;
            public int Sibling, Count;
            public Sprite Sprite;
            public MapLineSpec Spec;
            public Cat Cat;
        }

        private static readonly string[] GameGroups =
        {
            "bizman_status", "citymap_category_jobs", MapChipRules.RentGroup, "citymap_category_special_buildings", "common_real_estate", "citymap_category_activities", "common_neighborhoods",
            "citymap_category_active_businesses"
        };

        private readonly List<Cat> _cats = new List<Cat>(16);
        private readonly List<LineInfo> _lines = new List<LineInfo>(160);
        private readonly Dictionary<CityMapFilter, string> _keyOf = new Dictionary<CityMapFilter, string>(160);
        private readonly List<MapRow> _rows = new List<MapRow>(180);
        private readonly List<Cat> _rowCats = new List<Cat>(180);      // each row's group, in step with the rows
        private readonly List<LineInfo> _rowLines = new List<LineInfo>(180);    // each row's line (null for a group), in step with the rows
        private readonly List<Cat> _railCats = new List<Cat>(16);
        private readonly List<MapRailItem> _rail = new List<MapRailItem>(16);
        private readonly List<MapChip> _chips = new List<MapChip>(8);
        private readonly List<int> _chipKinds = new List<int>(8);
        private readonly List<string> _keys = new List<string>(160), _groupKeys = new List<string>(160);
        private readonly List<bool> _on = new List<bool>(160), _avail = new List<bool>(160);
        private readonly List<int> _chipLines = new List<int>(32);
        private readonly List<MapResult> _results = new List<MapResult>(8);
        private readonly List<CityBuildingController> _resultBuildings = new List<CityBuildingController>(8);
        private static readonly Dictionary<string, Sprite> Pictures = new Dictionary<string, Sprite>(64, StringComparer.Ordinal);
        private Dictionary<string, CityMapFilter> _entries;
        private string _revealGroup;
        private int _lit, _litTotal;

        private Dictionary<string, CityMapFilter> Entries()
        {
            if (_entries == null && _filters != null)
            {
                _entries = GameContract.MapFilterEntries.Field?.GetValue(_filters) as Dictionary<string, CityMapFilter>;
            }
            return _entries;
        }

        private List<CityMapSubwayStationEntry> StationEntries() => _stations != null ? GameContract.MapStationEntries.Field?.GetValue(_stations) as List<CityMapSubwayStationEntry> : null;

        private static Toggle MasterSwitch(CityMapFilters filters) => GameContract.MapFilterAll.Field?.GetValue(filters) as Toggle;

        // What else the signature holds: the saved places and the map's own events the counts follow.
        private int SignOther() => MapStoreGame.Count * 31 + (_look.CountsStale ? 1 : 0);

        private CityMapFilterCategory[] _groups;

        // The game's groups in the window, found when the map opens: the lines of a group are in its own list, which holds the mod's rows too (they are taken out of the
        // game's table by name, so the table alone would leave them off the kit's list).
        private CityMapFilterCategory[] Groups()
        {
            if ((_groups == null || _groups.Length == 0 || _look.GroupsStale) && _filters != null)
            {
                _look.GroupsStale = false;
                _groups = _filters.GetComponentsInChildren<CityMapFilterCategory>(true);
            }
            return _groups;
        }


        private static List<CityMapFilter> LinesOf(CityMapFilterCategory group) => group != null ? GameContract.MapGroupLines.Field?.GetValue(group) as List<CityMapFilter> : null;

        // The game's lines and groups, in the order the game lists them (a group's place in its list, a line's in its own), read now.
        private void ReadLines()
        {
            _cats.Clear();
            _lines.Clear();
            CityMapFilterCategory[] groups = Groups();
            if (groups == null)
            {
                return;
            }
            Dictionary<string, CityMapFilter> table = Entries();
            _keyOf.Clear();
            if (table != null)
            {
                foreach (KeyValuePair<string, CityMapFilter> pair in table)
                {
                    if (pair.Value != null)
                    {
                        _keyOf[pair.Value] = pair.Key;
                    }
                }
            }
            for (int g = 0; g < groups.Length; g++)
            {
                CityMapFilterCategory group = groups[g];
                List<CityMapFilter> list = LinesOf(group);
                if (group == null || list == null)
                {
                    continue;
                }
                var cat = new Cat { Obj = group, Key = group.name, Collapsed = group.IsCollapsed, Sibling = group.transform.GetSiblingIndex() };
                cat.Name = GroupName(group, cat.Key);
                cat.Foreign = Array.IndexOf(GameGroups, cat.Key) < 0 && !cat.Key.StartsWith("reallife", StringComparison.Ordinal);
                for (int i = 0; i < list.Count; i++)
                {
                    CityMapFilter f = list[i];
                    if (f == null)
                    {
                        continue;
                    }
                    string key = _keyOf.TryGetValue(f, out string known) ? known : f.name;
                    var line = new LineInfo
                    {
                        Obj = f, Key = key, Cat = cat, On = f.Toggle != null && f.Toggle.isOn, Available = f.IsAvailable(), Shown = f.gameObject.activeSelf, Sibling = f.transform.GetSiblingIndex(),
                        Name = LineName(f, key), Locate = Locates(f), Foreign = cat.Foreign
                    };
                    Image picture = GameContract.MapLineIcon.Field?.GetValue(f) as Image;
                    line.Sprite = picture != null && picture.enabled ? picture.sprite : null;
                    line.Spec = SpecOf(f, key);
                    cat.Lines.Add(line);
                    _lines.Add(line);
                }
                _cats.Add(cat);
            }
            _cats.Sort((a, b) => a.Sibling.CompareTo(b.Sibling));
            for (int i = 0; i < _cats.Count; i++)
            {
                _cats[i].Lines.Sort((a, b) => a.Sibling.CompareTo(b.Sibling));
                for (int k = 0; k < _cats[i].Lines.Count; k++)
                {
                    LineInfo line = _cats[i].Lines[k];
                    _cats[i].Total += line.Available ? 1 : 0;
                    _cats[i].On += line.Available && line.On ? 1 : 0;
                }
            }
            _keys.Clear();
            _groupKeys.Clear();
            _on.Clear();
            _avail.Clear();
            for (int i = 0; i < _lines.Count; i++)
            {
                _keys.Add(_lines[i].Key);
                _groupKeys.Add(_lines[i].Cat.Key);
                _on.Add(_lines[i].On);
                _avail.Add(_lines[i].Available);
            }
            MapCountRules.Tally(_on, _avail, out _lit, out _litTotal);
            if (_look.CountsStale)
            {
                CountPlaces();
            }
            for (int i = 0; i < _lines.Count; i++)
            {
                _lines[i].Count = _counts != null && i < _counts.Length ? _counts[i] : -1;
            }
        }

        private static string GroupName(CityMapFilterCategory cat, string key)
        {
            Component label = GameContract.MapGroupLabel.Field?.GetValue(cat) as Component;
            TMP_Text text = label != null ? label.GetComponentInChildren<TMP_Text>(true) : null;
            string words = text != null ? text.text : null;
            return string.IsNullOrWhiteSpace(words) ? DistrictNameOf(key) : words.Trim();
        }

        private static string LineName(CityMapFilter f, string key)
        {
            TMP_Text text = GameContract.MapLineLabel.Field?.GetValue(f) as TMP_Text;
            string words = text != null ? text.text : null;
            return string.IsNullOrWhiteSpace(words) ? DistrictNameOf(key) : words.Trim();
        }

        // A name that has no words yet is its key given a plain reading: never the raw key on the screen.
        private static string DistrictNameOf(string key)
        {
            string localized = key != null && LocalizorManager.IsLocalizedKey(key) ? key.GetLocalization() : null;
            return string.IsNullOrEmpty(localized) ? UiText.T("reallife_map_unnamed_line") : localized;
        }

        private static bool Locates(CityMapFilter f)
        {
            Button b = GameContract.MapLineFocus.Field?.GetValue(f) as Button;
            return b != null && b.gameObject.activeSelf;
        }

        // What a line shows, from the data the game gave it: the same fields its filter work reads.
        private static MapLineSpec SpecOf(CityMapFilter f, string key)
        {
            CityMapFilterData d = f.Data;
            if (key == MapChipRules.YoursLine)
            {
                return new MapLineSpec { Kind = MapLineKind.Rented };
            }
            if (key == "common_buildings_for_sale")
            {
                return new MapLineSpec { Kind = MapLineKind.ForSale };
            }
            if (d == null)
            {
                return default;
            }
            if (d.rivalId != null)
            {
                return new MapLineSpec { Kind = MapLineKind.Rival, Key = d.rivalId };
            }
            if (d.buildingType != null)
            {
                return new MapLineSpec { Kind = MapLineKind.BuildingType, Key = d.buildingType };
            }
            if (d.businessTypeName != null)
            {
                return new MapLineSpec { Kind = MapLineKind.BusinessType, Key = d.businessTypeName };
            }
            if (d.neighbourhood != null)
            {
                return new MapLineSpec { Kind = MapLineKind.Neighbourhood, Key = d.neighbourhood };
            }
            if (d.address != null)
            {
                return new MapLineSpec { Kind = MapLineKind.Address, Key = AddressKey(d.address) };
            }
            return default;
        }

        private static string AddressKey(Address a) => a != null ? a.streetName + "|" + a.streetNumber : null;

        // ------------------------------------------------------------------------------------------------- the counts

        private int[] _counts;
        private readonly List<MapBuildingFact> _facts = new List<MapBuildingFact>(900);
        private readonly List<MapLineSpec> _specs = new List<MapLineSpec>(160);

        // One pass over the city's buildings, once when the map opens and when a business changes: the same tests the game's filter work puts each through.
        private void CountPlaces()
        {
            _look.CountsStale = false;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? GameDlc.Open(city) : null;
            if (all == null)
            {
                return;
            }
            var forSale = new HashSet<string>(StringComparer.Ordinal);
            List<BuildingForSale> sales = SaveGameManager.Current != null ? SaveGameManager.Current.buildingsForSale : null;
            for (int i = 0; sales != null && i < sales.Count; i++)
            {
                forSale.Add(AddressKey(sales[i].address));
            }
            _facts.Clear();
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                BuildingRegistration reg = c != null && c.building != null ? c.buildingRegistration : null;
                if (reg == null)
                {
                    continue;
                }
                string address = AddressKey(c.building.Address);
                _facts.Add(new MapBuildingFact
                {
                    BuildingType = c.building.BuildingType, BusinessType = reg.businessTypeName, Neighbourhood = c.building.Neighbourhood, Address = address, RivalId = reg.businessOwnerRivalId,
                    ForRent = reg.AvailableForRent, Rented = reg.RentedByPlayer, Owned = reg.BuildingOwnedByPlayer, ForSale = forSale.Contains(address)
                });
            }
            _specs.Clear();
            for (int i = 0; i < _lines.Count; i++)
            {
                _specs.Add(_lines[i].Spec);
            }
            if (_counts == null || _counts.Length < _specs.Count)
            {
                _counts = new int[_specs.Count + 16];
            }
            MapCountRules.Count(_specs, _facts, _counts);
            // The mod's own rows count their own places, as the markers it puts on the map.
            IReadOnlyList<MapPlaces.Category> own = MapPlaces.All;
            for (int c = 0; c < own.Count; c++)
            {
                for (int i = 0; i < _lines.Count; i++)
                {
                    if (ReferenceEquals(own[c].Row, _lines[i].Obj))
                    {
                        _counts[i] = own[c].Places.Count;
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- the rows, the rail, the chips

        private static UiIcon GroupIcon(string key, bool foreign)
        {
            switch (key)
            {
                case "bizman_status": return UiIcon.Check;
                case "citymap_category_jobs": return UiIcon.Briefcase;
                case "citymap_category_rentable": return UiIcon.Building;
                case "citymap_category_special_buildings": return UiIcon.Star;
                case "common_real_estate": return UiIcon.Tag;
                case "citymap_category_activities": return UiIcon.Trophy;
                case "common_neighborhoods": return UiIcon.MapTrifold;
                case "citymap_category_active_businesses": return UiIcon.Store;
                case "reallife_dv_category": return UiIcon.Chart;
            }
            return foreign ? UiIcon.People : UiIcon.Cube;
        }

        // The glyph of a line that has no picture of its own: its group's.
        private static UiIcon LineIcon(LineInfo line) => GroupIcon(line.Cat.Key, line.Cat.Foreign);

        private void BuildRows()
        {
            _rows.Clear();
            _rowCats.Clear();
            _rowLines.Clear();
            _rail.Clear();
            _railCats.Clear();
            string other = UiText.T("reallife_map_other_mod");
            for (int c = 0; c < _cats.Count; c++)
            {
                Cat cat = _cats[c];
                if (!cat.Obj.gameObject.activeSelf)
                {
                    continue;   // the game's own search left it nothing to show
                }
                _rows.Add(new MapRow
                {
                    Kind = MapRowKind.Group, Name = cat.Name, Icon = GroupIcon(cat.Key, cat.Foreign), Total = cat.Total, OnCount = cat.On, On = cat.Total > 0 && cat.On == cat.Total,
                    Collapsed = cat.Collapsed, Off = cat.Total == 0, Foreign = cat.Foreign, Note = cat.Foreign ? other : null
                });
                _rowCats.Add(cat);
                _rowLines.Add(null);
                _rail.Add(new MapRailItem { Icon = GroupIcon(cat.Key, cat.Foreign), Name = cat.Name, Count = cat.On });
                _railCats.Add(cat);
                if (cat.Collapsed)
                {
                    continue;
                }
                for (int i = 0; i < cat.Lines.Count; i++)
                {
                    LineInfo line = cat.Lines[i];
                    if (!line.Shown || !line.Available)
                    {
                        continue;
                    }
                    string picture = null;
                    if (line.Sprite != null)
                    {
                        picture = "map:" + line.Key;
                        Pictures[picture] = line.Sprite;
                    }
                    _rows.Add(new MapRow
                    {
                        Kind = MapRowKind.Line, Name = line.Name, Icon = LineIcon(line), Picture = picture, Count = line.Count, On = line.On, Locate = line.Locate, Foreign = line.Foreign,
                        Note = line.Foreign ? other : null
                    });
                    _rowCats.Add(cat);
                    _rowLines.Add(line);
                }
            }
        }

        // The quick chips whose lines exist.
        private void BuildChips()
        {
            _chips.Clear();
            _chipKinds.Clear();
            for (int k = 0; k < (int)MapChipKind.Count; k++)
            {
                var kind = (MapChipKind)k;
                if (MapChipRules.Lines(kind, _keys, _groupKeys, _avail, _chipLines) == 0)
                {
                    continue;
                }
                _chips.Add(new MapChip { Label = UiText.T(Mod + "_map_chip_" + ChipId(kind)), Icon = ChipIcon(kind), On = MapChipRules.Lit(_chipLines, _on) });
                _chipKinds.Add(k);
            }
        }

        private static string ChipId(MapChipKind kind)
        {
            switch (kind)
            {
                case MapChipKind.Food: return "food";
                case MapChipKind.Groceries: return "groceries";
                case MapChipKind.Fuel: return "fuel";
                case MapChipKind.Parking: return "parking";
                case MapChipKind.Jobs: return "jobs";
                case MapChipKind.ForRent: return "rent";
                default: return "yours";
            }
        }

        private static UiIcon ChipIcon(MapChipKind kind)
        {
            switch (kind)
            {
                case MapChipKind.Food: return UiIcon.ForkKnife;
                case MapChipKind.Groceries: return UiIcon.ShoppingBag;
                case MapChipKind.Fuel: return UiIcon.Lightning;
                case MapChipKind.Parking: return UiIcon.Car;
                case MapChipKind.Jobs: return UiIcon.Briefcase;
                case MapChipKind.ForRent: return UiIcon.Key;
                default: return UiIcon.Star;
            }
        }

        // The places the typed words name, by business name or address, in the game's own list of places the search looks through.
        private void BuildResults()
        {
            _results.Clear();
            _resultBuildings.Clear();
            if (_filters == null || _search.Trim().Length < 2)
            {
                return;
            }
            var list = GameContract.MapFilterBuildings.Field?.GetValue(_filters) as List<CityBuildingController>;
            string text = _search.Trim();
            for (int i = 0; list != null && i < list.Count && _results.Count < 5; i++)
            {
                CityBuildingController c = list[i];
                string name = c != null && c.buildingRegistration != null ? c.buildingRegistration.BusinessName : null;
                string address = c != null && c.building != null ? c.building.Address.ToFormattedString() : null;
                if (!string.IsNullOrEmpty(name) && (name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || (address != null && address.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    _results.Add(new MapResult { Name = name, Address = address });
                    _resultBuildings.Add(c);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------- the dock's page

        private void BuildFilters()
        {
            ReadLines();
            BuildRows();
            BuildChips();
            BuildResults();
            MapPanelModel m = _panel;
            bool taxi = CityMap.IsOpen && InstanceBehavior<CityManager>.Instance.cityMap.IsTaxiMode;
            m.Page = MapPage.Filters;
            m.Title = UiText.T("reallife_map_filters_title");
            m.Icon = UiIcon.MapTrifold;
            m.Note = taxi ? UiText.T("reallife_map_taxi_note") : null;
            m.Rail = _rail.ToArray();
            m.SearchPlaceholder = UiText.T("citymap_map_search_placeholder");
            m.Search = _search;
            m.Results = _results.ToArray();
            m.ResultsNone = _search.Trim().Length >= 2 && _results.Count == 0 && _rows.Count == 0 ? UiText.T("reallife_map_no_place") : null;
            m.Chips = _search.Length > 0 ? new MapChip[0] : _chips.ToArray();
            m.ShowAll = true;
            m.ShowAllLabel = UiText.T("reallife_map_show_all");
            m.ShowAllCount = UiText.Fill("reallife_map_on_of", "on", UiText.Whole(_lit), "total", UiText.Whole(_litTotal));
            Toggle master = MasterSwitch(_filters);
            m.ShowAllOn = master != null && master.isOn;
            m.Rows = _rows.ToArray();
            m.Empty = _rows.Count == 0 ? UiText.T("reallife_map_no_line") : null;
            m.FooterLabel = null;
            m.Prompts = new MapPrompts(Words).Filters;
            m.PromptCount = m.Prompts.Length;
            m.Picked = ReadPicked(ref m.Place);
            m.RailShown = RailFor(m);
            if (_revealGroup != null)
            {
                for (int i = 0; i < _rowCats.Count; i++)
                {
                    if (_rowLines[i] == null && _rowCats[i].Key == _revealGroup)
                    {
                        m.Reveal = i;
                    }
                }
                _revealGroup = null;
            }
        }

        // The rail's group under the top of the list: the group of the first row in view.
        private int RailFor(MapPanelModel m)
        {
            int first = Mathf.Clamp(Mathf.RoundToInt(m.Offset / UiMapFilters.RowStep(m)), 0, Math.Max(0, _rowCats.Count - 1));
            return first < _rowCats.Count ? _railCats.IndexOf(_rowCats[first]) : -1;
        }

        // ------------------------------------------------------------------------------------------------- the subway's stations

        private void BuildSubway()
        {
            MapPanelModel m = _panel;
            m.Page = MapPage.Subway;
            m.Title = UiText.T("common_subwaystation");
            m.Icon = UiIcon.Train;
            m.Note = UiText.T("reallife_map_subway_note");
            m.Rail = new MapRailItem[0];
            m.RailShown = -1;
            m.SearchPlaceholder = UiText.T("citymap_map_search_placeholder");
            m.Search = _search;
            m.Results = new MapResult[0];
            m.ResultsNone = null;
            m.Chips = new MapChip[0];
            m.ShowAll = false;
            m.Picked = false;
            m.FooterLabel = null;
            _rows.Clear();
            _stationRows.Clear();
            List<CityMapSubwayStationEntry> entries = StationEntries();
            SubwayStation here = InstanceBehavior<CityManager>.Instance.subwaySystem != null ? InstanceBehavior<CityManager>.Instance.subwaySystem.lastSubwayStation : null;
            string text = _search.Trim();
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                SubwayStation station = entries[i] != null ? entries[i].Station : null;
                if (station == null)
                {
                    continue;
                }
                string name = GameMembers.NameKey(station).GetLocalization();
                if (text.Length > 0 && name.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    continue;
                }
                bool current = ReferenceEquals(station, here);
                _rows.Add(new MapRow
                {
                    Kind = MapRowKind.Station, Name = name, Note = DistrictNameOf(station.neighbourhood), Icon = UiIcon.Train, Off = current || SubwaySystem.IsRiding,
                    Chip = current ? UiText.T("reallife_map_here") : null
                });
                _stationRows.Add(entries[i]);
            }
            m.Rows = _rows.ToArray();
            m.Empty = _rows.Count == 0 ? UiText.T("reallife_map_no_station") : null;
            m.Prompts = new MapPrompts(Words).Filters;
            m.PromptCount = m.Prompts.Length;
        }

        private readonly List<CityMapSubwayStationEntry> _stationRows = new List<CityMapSubwayStationEntry>(32);

        /// <summary>The pictures the dock's lines carry, by key, for the kit's picture lookup.</summary>
        internal static Sprite PictureOf(string key) => key != null && Pictures.TryGetValue(key, out Sprite s) ? s : null;
    }

    /// <summary>The key prompts the dock shows while the keys lead (it is pointer driven in the game, so they are only what the preview and a screen opened with a pad show).</summary>
    internal readonly struct MapPrompts
    {
        public readonly UiPrompt[] Filters;

        public MapPrompts(UiWords t)
        {
            Filters = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("common_close", "Close") },
            };
        }
    }
}
