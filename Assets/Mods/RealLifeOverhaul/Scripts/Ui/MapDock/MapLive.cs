using System;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Buildings;
using City.CityMap;
using Helpers;
using UI;
using UI.InGameUI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The city map's left dock as a screen of the kit over the game's own: it mirrors the game's filter window (its groups, their lines, their switches and the game's own
    /// search), its subway station list and the picked place of its building card, and presses the game's own widgets, so every rule of the game's, Going Public's lines and
    /// the mod's own rows stay the game's. Beside the dock it draws the route strip, the map's buttons and the Layers panel, and swaps the dock for the place card or the
    /// district view when the player asks for them. A look at the game's state four times a second, laid out only when it changed; nothing is allocated or searched per frame.
    /// </summary>
    internal sealed partial class MapLive : IUiScreen, IUiTyping, IUiPointing
    {
        private enum Dock : byte
        {
            Filters = 0,
            Place,
            District
        }

        private const float Every = 0.25f;   // seconds between looks at the game's panel
        private static readonly UiWords Words = (key, english) => UiText.T(key);

        private readonly Component _found;
        private readonly bool _subway;
        private readonly GameUi.MapPicker _picker;   // the mod's choice of a place, when this is the picker's face
        private int _pointedEntry = -1;               // the entry of the choice the pointer rests on
        private CityMapSubwayStationEntry _pointedStation;   // the game's row of the station the pointer rests on, which previews it as the game's own list does
        private bool _hoverNext;                      // the pointer rests on the row of the unit next door
        private readonly CityMapFilters _filters;
        private readonly CityMapSubwayStations _stations;
        private readonly MapScreenModel _model = new MapScreenModel();
        private readonly MapPanelModel _panel = new MapPanelModel();
        private Dock _dock;
        private string _search = string.Empty;
        private float _next;
        private int _seen = int.MinValue;
        private bool _dirty = true;
        private readonly MapLook _look = new MapLook();   // the counts and the groups are read afresh on every open, though nothing looks while the dock is hidden

        public MapLive(Component found, bool subway, Action hideCard = null)
        {
            _hideCard = hideCard;
            _found = found;
            _subway = subway;
            _filters = found as CityMapFilters;
            _stations = found as CityMapSubwayStations;
            _model.Buttons = new MapButtonsModel();
            if (!_picturesOn)
            {
                // The game's own pictures (a line's icon, a building's kind) by the keys the dock gives them; any other key is another part's.
                _picturesOn = true;
                UiPictures.Answer(key => key != null && key.StartsWith("map:", StringComparison.Ordinal) ? PictureOf(key) : null);
            }
        }

        private readonly Action _hideCard;   // hides the game's building box for a picked building (MapFiltersGame.HideCard)
        private static bool _picturesOn;

        /// <summary>The face of one of the mod's choices of a place on the open map (build mode's plots, a bus's stops): the same panel, its rows the choice's entries.</summary>
        public MapLive(GameUi.MapPicker picker) : this(null, false)
        {
            _picker = picker;
        }

        public string Id => _picker != null ? MapKits.PickerId : _subway ? MapKits.SubwayId : MapKits.FiltersId;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    int now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _look.Look(CityMap.IsOpen, Time.unscaledTime);
            Build();
            _model.HasLabel = NextDoorLabel(frame);
            UiMap.Lay(s, _model, frame, dark, keys);
            _dirty = false;
            _seen = Signature();
        }

        public bool Press(int command, int arg)
        {
            bool done = MapPresses.Do(command, arg, this);
            _dirty |= done;
            return done;
        }

        public void Focused(int command, int arg)
        {
            // A key or the pad on a place of a choice: the map glides there after the pause, as it does for the window's own entry.
            Pointed(command, arg);
        }

        /// <summary>
        /// What the pointer (or the keys' focus) rests on: a place of a choice glides the map there, the unit next door shows its label over the map, a district's row makes the
        /// card about that district. Left alone (command -1) they all stop.
        /// </summary>
        public void Pointed(int command, int arg)
        {
            if (_picker != null)
            {
                int entry = command == UiMapFilters.ChoiceCommand && arg >= 0 && arg < _pickerRows.Count ? _pickerRows[arg] : -1;
                if (entry != _pointedEntry)
                {
                    if (_pointedEntry >= 0)
                    {
                        _picker.KitUnpoint(_pointedEntry);
                    }
                    _pointedEntry = entry;
                    if (entry >= 0)
                    {
                        _picker.KitPoint(entry);
                    }
                }
                return;
            }
            if (_subway)
            {
                PointStation(command == UiMapFilters.StationCommand && arg >= 0 && arg < _stationRows.Count ? _stationRows[arg] : null);
                return;
            }
            bool next = _dock == Dock.Place && command == UiMapPlace.RentCommand && _nextDoor != null;
            if (next != _hoverNext)
            {
                _hoverNext = next;
                _dirty = true;
            }
            if (_dock == Dock.District && DistrictOwner != null)
            {
                int stamp = DistrictOwner.KitStamp;
                DistrictOwner.KitPoint(command == UiMapDistrict.RowCommand ? arg : -1);
                if (DistrictOwner.KitStamp != stamp)
                {
                    _dirty = true;   // the chosen district block and the highlighted row follow the district pointed at
                }
            }
        }

        // The game's own row takes the pointer: after its pause the map camera glides to the station and the station is lit, and leaving sends the camera back (the row's own work).
        private void PointStation(CityMapSubwayStationEntry entry)
        {
            if (ReferenceEquals(entry, _pointedStation))
            {
                return;
            }
            CityMapSubwayStationEntry before = _pointedStation;
            _pointedStation = null;
            using (HotSpots.Game())
            {
                if (before != null && before.isActiveAndEnabled)
                {
                    before.OnPointerExit(null);
                }
                if (entry != null && entry.isActiveAndEnabled)
                {
                    entry.OnPointerEnter(null);
                    _pointedStation = entry;
                }
            }
        }

        /// <summary>Back steps out of a card or a view first, then clears a typed search, and last closes the map as the game's own close does.</summary>
        public bool Back()
        {
            if (_dock == Dock.District)
            {
                DistrictOff();
                _dock = Dock.Filters;
                _dirty = true;
                return true;
            }
            if (_dock == Dock.Place)
            {
                _dock = Dock.Filters;
                _routeShown = false;
                _hoverNext = false;
                _dirty = true;
                return true;
            }
            if (_search.Length > 0)
            {
                Typed(string.Empty);
                return true;
            }
            if (_picker != null)
            {
                _picker.KitClose();
            }
            else if (_subway)
            {
                PointStation(null);
                _stations.Cancel();
            }
            else
            {
                _filters.CloseCityMap();
            }
            return true;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
            _panel.Reveal = index;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            float step = UiMapFilters.RowStep(_panel) * 3f;
            if (_dock == Dock.Filters)
            {
                _panel.Offset = Math.Max(0f, _panel.Offset - notches * step);
            }
            else if (_dock == Dock.Place)
            {
                _place.Offset = Math.Max(0f, _place.Offset - notches * step);
            }
            else if (_district != null)
            {
                _district.Offset = Math.Max(0f, _district.Offset - notches * UiTokens.TableRow * 3f);
            }
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- typing

        public int TypingCommand => UiMapFilters.SearchCommand;

        public string TypingText => _search;

        /// <summary>The words as they are typed: the game's own search filters its lines by them, and the places they name are listed under the field.</summary>
        public void Typed(string text)
        {
            _search = text ?? string.Empty;
            if (_picker != null)
            {
                _picker.KitSearch(_search);
                _dirty = true;
                return;
            }
            if (_subway)
            {
                _dirty = true;
                return;
            }
            try
            {
                GameContract.MapFilterSearch.Method?.Invoke(_filters, new object[] { _search });
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the city map's own filtering of its lines by the typed words");
            }
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- what the game says now

        // A few numbers of the game's state: the picked place, the hour, every line's switch and whether it shows, the stations, the tilt, the camera's zoom, the saved places.
        private int Signature()
        {
            unchecked
            {
                if (_picker != null)
                {
                    return ((_picker.Fills * 31 + _picker.Query.GetHashCode()) * 31 + (_picker.IsOpen ? 1 : 0)) * 31 + NavTiltStamp();
                }
                int h = _subway ? 7 : 3;
                UIs uis = InstanceBehavior<UIs>.Instance;
                BuildingResume card = uis != null ? uis.buildingResume : null;
                EntityController target = card != null ? GameMembers.TargetOf(card) : null;
                bool picked = target != null && card.options != null && card.options.gameObject.activeSelf;
                h = h * 31 + (picked ? target.GetInstanceID() : 0);
                h = h * 31 + (!picked && target != null ? target.GetInstanceID() : 0);   // the building under the pointer
                h = h * 31 + TimeHelper.CurrentHour;
                h = h * 31 + (int)_dock;
                bool open = CityMap.IsOpen;
                _look.Look(open, Time.unscaledTime);   // a fresh open counts the places on each line again (the map pauses the game, so they cannot change while it is up)
                h = h * 31 + (_dock == Dock.District && DistrictOwner != null ? MapLook.DistrictStamp(true, DistrictOwner.KitStamp) : 0);
                h = h * 31 + (CityMap.IsOpen &&InstanceBehavior<CityManager>.Instance.cityMap.IsTaxiMode ? 1 : 0);
                h = h * 31 + NavTiltStamp();
                h = h * 31 + (_filters != null ? SignLines(_filters) : SignStations());
                return h * 31 + SignOther();
            }
        }

        private float ZoomStep => Mathf.Max(10f, (CityMapCamera() != null ? CityMapCamera().minMaxDistance.y - CityMapCamera().minMaxDistance.x : 250f) * 0.12f);

        private static CityMapCam CityMapCamera()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            return city != null && city.cityMap != null ? city.cityMap.cityMapCam : null;
        }

        // The tilt button's state and the zoom (to a tenth of the range): what the map's buttons say.
        private static int NavTiltStamp()
        {
            CityMapCam cam = CityMapCamera();
            return Nav.NavTilt.Map * 100 + (cam != null ? Mathf.RoundToInt(cam.distance / 10f) : 0);
        }

        private int SignLines(CityMapFilters filters)
        {
            unchecked
            {
                int h = 17;
                CityMapFilterCategory[] groups = Groups();
                for (int g = 0; groups != null && g < groups.Length; g++)
                {
                    CityMapFilterCategory group = groups[g];
                    if (group == null)
                    {
                        continue;
                    }
                    h = h * 31 + (group.IsCollapsed ? 8 : 0) + (group.gameObject.activeSelf ? 16 : 0);
                    List<CityMapFilter> list = LinesOf(group);
                    for (int i = 0; list != null && i < list.Count; i++)
                    {
                        CityMapFilter f = list[i];
                        if (f != null)
                        {
                            h = h * 31 + (f.Toggle != null && f.Toggle.isOn ? 1 : 0) + (f.gameObject.activeSelf ? 2 : 0) + (f.IsAvailable() ? 4 : 0);
                        }
                    }
                    h = h * 31 + (list != null ? list.Count : 0);
                }
                return h * 31 + _search.GetHashCode();
            }
        }

        private int SignStations()
        {
            unchecked
            {
                int h = 5;
                List<CityMapSubwayStationEntry> entries = StationEntries();
                for (int i = 0; entries != null && i < entries.Count; i++)
                {
                    h = h * 31 + (entries[i] != null && entries[i].gameObject.activeSelf ? 1 : 0);
                }
                return h * 31 + _search.GetHashCode();
            }
        }
    }
}
