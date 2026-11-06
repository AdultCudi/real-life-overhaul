using System;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.DayNightCycle;
using Buildings;
using Extensions;
using Helpers;
using Streets;
using UI;
using UI.InGameUI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The place card of the live dock: a place's building, its business and what Flare in the city knows of it, read into the card's model when the place changes and again when
    /// the player changes something (a tab, Save, Directions); and the route strip's figures from the roads, the City Cabs quote and the subway. Nothing a frame.
    /// </summary>
    internal sealed partial class MapLive
    {
        private static readonly string[] DayKeys =
        {
            "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday"
        };

        private readonly MapPlaceModel _place = new MapPlaceModel();
        private readonly MapRouteModel _route = new MapRouteModel();
        private readonly MapLayersModel _layers = new MapLayersModel();
        private readonly List<MapRouteMode> _modes = new List<MapRouteMode>(4);
        private bool _routeShown;
        private int _routeChoice;
        private int _placeTab;
        private string _placeKey, _routeKey;
        private MapFlareFacts _flare;
        private bool _busier;   // the place draws more today than it usually does
        private CityBuildingController _nextDoor;

        // ------------------------------------------------------------------------------------------------- the card

        private bool BuildPlace()
        {
            BuildingResume card = Card();
            BuildingRegistration reg = _pickedBuilding != null ? _pickedBuilding.buildingRegistration : null;
            if (reg == null || card == null || _pickedBuilding.building == null)
            {
                return false;
            }
            Building building = _pickedBuilding.building;
            MapPlaceModel m = _place;
            string place = Flare.FlareGame.Place(reg);
            if (place != _placeKey)
            {
                _placeKey = place;
                _routeKey = null;
                _placeTab = 0;
                _routeChoice = 0;
                m.Offset = 0f;
                _flare = MapBridges.Flare != null ? MapBridges.Flare(place) : default;
                _nextDoor = NextDoor(_pickedBuilding);
                _busier = _flare.Found && MapStoreGame.Look(place, _flare.Arrivals);
            }
            m.Title = UiText.T("reallife_map_place_title");
            m.Icon = UiIcon.Store;
            m.Glyph = TypeGlyph(reg);
            m.Tint = UiRole.CatRetail;
            m.Picture = PictureOfPlace(_pickedBuilding, "map:place");
            m.Name = string.IsNullOrEmpty(reg.BusinessName) ? building.Address.ToFormattedString() : reg.BusinessName;
            m.StarHalves = MapPlaceRules.Halves(_flare.Tenths);
            m.Rating = MapPlaceRules.Rating(_flare.Tenths);
            m.Count = _flare.Found && _flare.Tenths > 0 ? "(" + _flare.ReviewCount.ToString("N0", CultureInfo.CurrentCulture) + ")" : null;
            m.Kind = KindWords(card);
            int tier = _flare.Found ? MapPlaceRules.Tier(_flare.Price) : 0;
            m.Price = tier > 0 ? new string('$', tier) : null;
            m.Chips = PlaceChips(card, reg, default);
            m.DirectionsLabel = UiText.T("reallife_map_directions");
            m.SaveLabel = UiText.T("reallife_map_save");
            m.SavedLabel = UiText.T("reallife_map_saved");
            m.DestinationLabel = UiText.T("common_setdestination");
            m.Saved = MapStoreGame.Saved(building.Address.streetName, building.Address.streetNumber);
            m.RouteShown = _routeShown;
            m.CanDestination = UiWidgets.Shows(card.setDestinationButton);
            m.CanBizMan = OffersBizMan(card);
            m.BizManLabel = UiText.T("reallife_co_open_bizman");
            m.Tabs = new[] { UiText.T("reallife_map_tab_overview"), UiText.T("reallife_map_tab_reviews"), UiText.T("reallife_map_tab_business") };
            m.Tab = _placeTab;
            m.Rows = OverviewRows(building);
            BusyHours(reg, m);
            m.PeopleTitle = UiText.T("reallife_map_people");
            m.PeopleAll = _flare.ReviewCount > 2 ? UiText.Fill("reallife_map_all_reviews", "count", _flare.ReviewCount.ToString("N0", CultureInfo.CurrentCulture)) : null;
            m.Reviews = ReviewsOf();
            m.ReviewsEmpty = UiText.T("reallife_map_no_reviews");
            m.BusinessTitle = UiText.T("reallife_map_business");
            m.Business = BusinessRows(card, reg);
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") }, new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_map_back_filters") },
            };
            m.PromptCount = m.Prompts.Length;
            return true;
        }

        // The business's kind in the game's own words, as its card writes it; null where the card has none.
        private static string KindWords(BuildingResume card)
        {
            string words = card.businessTypeLabel != null && card.businessTypeLabel.gameObject.activeSelf ? LabelWords(card.businessTypeLabel) : null;
            return string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        }

        // The address with its district, how far it is, the hours today, and a vacant unit next door with its market rent. The distance is a row of its own because a long
        // address leaves no room for it on the same line.
        private MapPlaceRow[] OverviewRows(Building building)
        {
            BuildingRegistration reg = _pickedBuilding.buildingRegistration;
            var rows = new List<MapPlaceRow>(4)
            {
                new MapPlaceRow { Label = UiText.T("reallife_map_row_address"), Value = building.Address.ToFormattedString() + ", " + Districts.DistrictGame.Name(building.Neighbourhood) },
                new MapPlaceRow { Label = UiText.T("reallife_map_row_distance"), Value = DistanceWords(_pickedAt, _pickedRoad) },
                new MapPlaceRow { Label = UiText.T("reallife_map_row_hours"), Value = HoursOf(Today(reg)) },
            };
            if (_nextDoor != null && _nextDoor.building != null)
            {
                float perSqm = _nextDoor.building.GetBuildingDailyMarketRentPerSqm();
                rows.Add(new MapPlaceRow
                {
                    Label = UiText.T("reallife_map_row_next"), Link = true,
                    Value = perSqm > 0f
                        ? UiText.Fill("reallife_map_row_for_rent", "rent", UiText.Money(Districts.DistrictGame.PerArea(perSqm)), "unit", Districts.DistrictGame.AreaUnit())
                        : UiText.T("common_available_for_rent")
                });
            }
            return rows.ToArray();
        }

        // A vacant unit on the same street within three plots (the numbers step by two), the nearest first.
        private static CityBuildingController NextDoor(CityBuildingController from)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? GameDlc.Open(city) : null;
            Address a = from.building.Address;
            CityBuildingController best = null;
            int bestGap = int.MaxValue;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController c = all[i];
                Building b = c != null ? c.building : null;
                BuildingRegistration reg = b != null ? c.buildingRegistration : null;
                if (reg == null || ReferenceEquals(c, from) || b.Address.streetName != a.streetName)
                {
                    continue;
                }
                int gap = Math.Abs(b.Address.streetNumber - a.streetNumber);
                bool vacant = reg.AvailableForRent && !reg.RentedByPlayer && (string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == "ba:businesstype_empty");
                if (vacant && gap <= 6 && gap < bestGap)
                {
                    best = c;
                    bestGap = gap;
                }
            }
            return best;
        }

        private static ScheduleDay Today(BuildingRegistration reg) => reg.scheduleDays != null ? reg.scheduleDays.Find(x => x.day == TimeHelper.GetDayOfWeek()) : null;

        // The first opening and the last closing of a day's slots, as the game's own hours line reads them.
        private static bool Span(ScheduleDay day, out int open, out int close)
        {
            open = 24;
            close = 0;
            if (day == null || !day.isOpen || day.openingHourSlots == null || day.openingHourSlots.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < day.openingHourSlots.Count; i++)
            {
                open = Math.Min(open, day.openingHourSlots[i].startingHour);
                close = Math.Max(close, day.openingHourSlots[i].endingHour);
            }
            return close > open;
        }

        private static string HoursOf(ScheduleDay day) => Span(day, out int open, out int close)
            ? UiText.Fill("reallife_map_hours_between", "from", open.GetFormattedTime(0f, true), "to", close.GetFormattedTime(0f, true))
            : UiText.T("common_closed");

        // The typical day's shape: the game's own hourly multipliers for the business's kind over the hours it is open today.
        private void BusyHours(BuildingRegistration reg, MapPlaceModel m)
        {
            m.Busy = new int[0];
            m.BusyNow = -1;
            m.BusyNote = null;
            m.BusyUp = false;
            BusinessType type = !string.IsNullOrEmpty(reg.businessTypeName) && reg.businessTypeName != "ba:businesstype_empty" ? BusinessTypeHelper.GetData(reg) : null;
            if (type == null || type.hourlyFactorMultipliers == null || type.hourlyFactorMultipliers.Count == 0 || !Span(Today(reg), out int open, out int close))
            {
                return;
            }
            m.Busy = MapPlaceRules.Busy(hour => HourFactor(type, hour), open, close, out int from);
            m.BusyFrom = from;
            m.BusyNow = MapPlaceRules.Now(TimeHelper.CurrentHour, from, m.Busy.Length);
            m.BusyTitle = UiText.T("reallife_map_busy");
            m.BusyUp = _busier;
            m.BusyNote = _busier ? UiText.T("reallife_map_busier") : UiText.T("reallife_map_typical");
        }

        private static float HourFactor(BusinessType type, int hour)
        {
            for (int i = 0; i < type.hourlyFactorMultipliers.Count; i++)
            {
                HourlyFactorMultiplier f = type.hourlyFactorMultipliers[i];
                if (hour >= f.startingHour && hour < f.endingHour)
                {
                    return f.multiplier;
                }
            }
            return 0f;
        }

        // The newest reviews as the card shows them: the resident's name, their stars, how long ago, and a line the business's weakest point and the stars pick.
        private MapReview[] ReviewsOf()
        {
            if (!_flare.Found || _flare.Newest == null)
            {
                return new MapReview[0];
            }
            int today = TimeHelper.CurrentDay;
            var list = new MapReview[_flare.Newest.Length];
            for (int i = 0; i < list.Length; i++)
            {
                MapFlareReview r = _flare.Newest[i];
                list[i] = new MapReview
                {
                    Name = r.Name, Hue = UiColour.HueOf(r.Name), Halves = r.Stars * 2, When = Ago(MapPlaceRules.DaysAgo(today, r.Day)),
                    Text = UiText.T(MapPlaceRules.ReviewKey(r.Stars, _flare.Why, r.Person, r.Day))
                };
            }
            return list;
        }

        private static string Ago(int days) => days == 0 ? UiText.T("reallife_map_ago_today") : days == 1 ? UiText.T("reallife_map_ago_yesterday")
            : UiText.Fill("reallife_map_ago_days", "days", days.ToString(CultureInfo.InvariantCulture));

        // Whose it is and what it is, then the building: the business's owner with its valuation under it, the building's owner with its market value under it, its floor
        // area, its customers' room and its traffic, and the week's hours. Each of the game's card's lines once.
        private MapPlaceRow[] BusinessRows(BuildingResume card, BuildingRegistration reg)
        {
            var rows = new List<MapPlaceRow>(14);
            BizWorth worth = BizFacts.Read(_pickedBuilding);
            Building building = _pickedBuilding.building;
            string owner = card.rivalBusinessOwner != null && card.rivalBusinessOwner.gameObject.activeSelf ? LabelWords(card.rivalBusinessOwner) : null;
            rows.Add(new MapPlaceRow
            {
                Label = UiText.T("reallife_map_row_owner"),
                Value = reg.RentedByPlayer ? UiText.T("reallife_map_you") : string.IsNullOrWhiteSpace(owner) ? UiText.T("reallife_map_row_none") : owner.Trim()
            });
            if (worth.Valuation != null)
            {
                rows.Add(new MapPlaceRow { Label = BizFacts.ValuationLabel(worth), Value = worth.Valuation });
            }
            string kind = KindWords(card);
            if (kind != null)
            {
                rows.Add(new MapPlaceRow { Label = UiText.T("reallife_map_row_type"), Value = kind });
            }
            string landlord = card.rivalBuildingOwner != null && card.rivalBuildingOwner.gameObject.activeSelf ? LabelWords(card.rivalBuildingOwner) : null;
            if (!string.IsNullOrWhiteSpace(landlord))
            {
                rows.Add(new MapPlaceRow { Label = landlord.Trim() });
            }
            if (worth.Market != null)
            {
                rows.Add(new MapPlaceRow { Label = UiText.T("bizman_market_value"), Value = worth.Market });
            }
            string area = card.buildingAreaLabel != null && card.buildingAreaLabel.gameObject.activeSelf ? LabelWords(card.buildingAreaLabel) : null;
            if (!string.IsNullOrWhiteSpace(area))
            {
                rows.Add(new MapPlaceRow { Label = UiText.T("reallife_ui_world_area"), Value = area.Trim() });
            }
            if (building.GetCustomerCapacity >= 0)
            {
                rows.Add(new MapPlaceRow { Label = UiText.T("reallife_ui_world_capacity"), Value = UiText.Whole(building.GetCustomerCapacity) });
            }
            rows.Add(new MapPlaceRow { Label = UiText.T("reallife_ui_world_traffic"), Value = building.trafficIndex.ToString(CultureInfo.CurrentCulture) });
            for (int i = 0; reg.scheduleDays != null && i < reg.scheduleDays.Count; i++)
            {
                ScheduleDay day = reg.scheduleDays[i];
                int index = Math.Max(0, Math.Min(DayKeys.Length - 1, TimeHelper.GetDayOfWeekIndex(day.day)));
                rows.Add(new MapPlaceRow { Label = UiText.T(DayKeys[index]), Value = HoursOf(day), Ink = Span(day, out _, out _) ? UiRole.None : UiRole.Muted });
            }
            return rows.ToArray();
        }

        // ------------------------------------------------------------------------------------------------- the card's presses

        public bool PlaceTab(int index)
        {
            if (index < 0 || index > 2)
            {
                return false;
            }
            _placeTab = index;
            _place.Offset = 0f;
            return true;
        }

        public bool Directions()
        {
            _routeShown = !_routeShown;
            return true;
        }

        public bool Save()
        {
            if (_pickedBuilding == null || _pickedBuilding.building == null)
            {
                return false;
            }
            Address a = _pickedBuilding.building.Address;
            string name = _pickedBuilding.buildingRegistration != null ? _pickedBuilding.buildingRegistration.BusinessName : null;
            MapStoreGame.Toggle(a.streetName, a.streetNumber, name);
            return true;
        }

        public bool PlaceDestination() => PickedDestination();

        // View the vacant unit next door: the camera goes to it and the game's card turns to it, as a click on it would.
        public bool PlaceRent()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (_nextDoor == null || city == null || city.cityMap == null)
            {
                return false;
            }
            city.cityMap.FocusOnBuilding(_nextDoor);
            Card()?.ShowOptions(_nextDoor);
            return true;
        }

        // The unit next door named over the map where it stands, while the pointer rests on its row: a piece laid out with the page (the camera does not move while the pointer
        // is on the dock, so where the unit is on the screen holds until it leaves), never a frame's work.
        private bool NextDoorLabel(UiFrame frame)
        {
            if (!_hoverNext || _dock != Dock.Place || _nextDoor == null || _nextDoor.building == null)
            {
                return false;
            }
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            Vector3 screen = camera.WorldToScreenPoint(PositionOf(_nextDoor, _nextDoor));
            if (screen.z <= 0f)
            {
                return false;
            }
            UiHudRules.ToLayout(screen.x, screen.y, UnityEngine.Screen.width, UnityEngine.Screen.height, frame.Width, frame.Height, out float x, out float y);
            float perSqm = _nextDoor.building.GetBuildingDailyMarketRentPerSqm();
            _model.Label = new MapLabel
            {
                Icon = UiIcon.Key, Title = UiText.T("reallife_map_next_label"), X = x, Y = y,
                Figure = perSqm > 0f
                    ? UiText.Fill("reallife_map_next_figure", "rent", UiText.Money(Districts.DistrictGame.PerArea(perSqm)), "unit", Districts.DistrictGame.AreaUnit())
                    : null
            };
            return true;
        }

        public bool PlaceAllReviews()
        {
            _placeTab = 1;
            _place.Offset = 0f;
            return true;
        }

        public bool PlaceReview(int index) => index >= 0 && index < _place.Reviews.Length;

        // ------------------------------------------------------------------------------------------------- the route strip

        public bool RouteOption(int index)
        {
            if (index < 0 || index >= _route.Options.Length || _route.Options[index].Off)
            {
                return false;
            }
            _routeChoice = index;
            _route.Chosen = index;
            return true;
        }

        // Go sets the destination as the game's own button does; by taxi it then shuts the map and opens City Cabs' own offer, with its quote and its own Send.
        public bool Go()
        {
            if (_route.Chosen < 0 || _route.Chosen >= _modes.Count || !PickedDestination())
            {
                return false;
            }
            if (_modes[_route.Chosen] == MapRouteMode.Taxi)
            {
                MapKits.AfterMap(TaxiPhone.Open);
                _filters?.CloseCityMap();
            }
            return true;
        }

        // The ways to the picked place with their minutes and cost: on foot and by car from the roads, the cab's quote (the wait and the call-out) and its ride, the subway.
        // Worked out when the place or the hour changes: the cab's quote is a search of its own.
        private void BuildRoute()
        {
            string key = _placeKey + "|" + TimeHelper.CurrentHour;
            if (key == _routeKey && _route.Options.Length > 0)
            {
                _route.Chosen = Mathf.Clamp(_routeChoice, 0, _route.Options.Length - 1);
                _route.CanGo = UiWidgets.Shows(Card() != null ? Card().setDestinationButton : null);
                return;
            }
            _routeKey = key;
            _modes.Clear();
            var options = new List<MapRouteOption>(4);
            Nav.NavDirector director = Nav.NavDirector.Instance;
            float rate = director != null ? Mathf.Max(0.05f, director.Rate) : 1f;
            Vector3 at = _pickedAt;
            Vector3 me = PlayerHelper.PlayerController != null ? PlayerHelper.GetCityPosition() : at;
            float straight = Flat(at, me);
            float walk = director != null && director.Probe(at.x, at.z, true, out float w) ? w : straight * MapRouteRules.StraightDetour;
            float drive = _pickedRoad >= 0f ? _pickedRoad : straight * MapRouteRules.StraightDetour;
            options.Add(new MapRouteOption
            {
                Icon = UiIcon.Walk, Minutes = Nav.NavDirector.MinutesWords(MapRouteRules.Minutes(walk, MapRouteRules.WalkPace, 0f, rate)), Sub = UiText.T("reallife_map_route_walk")
            });
            _modes.Add(MapRouteMode.Walk);
            TaxiDirector cab = TaxiDirector.Instance;
            if (cab != null && cab.Quote(out int wait, out float fee, out _, out _))
            {
                int ride = MapRouteRules.Minutes(drive, MapRouteRules.CarPace, 0f, rate);
                options.Add(new MapRouteOption
                {
                    Icon = UiIcon.Taxi, Minutes = Nav.NavDirector.MinutesWords(wait + ride), Sub = UiText.Fill("reallife_map_route_taxi", "amount", UiText.Money(fee))
                });
                _modes.Add(MapRouteMode.Taxi);
            }
            if (SubwayLeg(me, at, drive, out float seconds))
            {
                options.Add(new MapRouteOption
                {
                    Icon = UiIcon.Train, Minutes = Nav.NavDirector.MinutesWords(Mathf.Max(1, Mathf.CeilToInt(seconds * rate - 0.001f))),
                    Sub = UiText.Fill("reallife_map_route_subway", "amount", UiText.Money(SubwayStation.PricePerRide))
                });
                _modes.Add(MapRouteMode.Subway);
            }
            if (VehicleHelper.AllPlayerVehicles != null && VehicleHelper.AllPlayerVehicles.Count > 0)
            {
                options.Add(new MapRouteOption
                {
                    Icon = UiIcon.Car, Minutes = Nav.NavDirector.MinutesWords(MapRouteRules.Minutes(drive, MapRouteRules.CarPace, 0f, rate)), Sub = UiText.T("reallife_map_route_car")
                });
                _modes.Add(MapRouteMode.Car);
            }
            _route.Options = options.ToArray();
            _route.Chosen = Mathf.Clamp(_routeChoice, 0, options.Count - 1);
            _route.GoLabel = UiText.T("reallife_map_go");
            _route.CanGo = UiWidgets.Shows(Card() != null ? Card().setDestinationButton : null);
        }


        // The subway between the stations nearest the player and the place: the walks to and from them, the wait and the ride; false where it is not worth offering.
        private static bool SubwayLeg(Vector3 me, Vector3 at, float road, out float seconds)
        {
            seconds = 0f;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            IList<SubwayStation> stations = city != null ? city.subwayStations : null;
            if (stations == null || stations.Count < 2)
            {
                return false;
            }
            SubwayStation from = null, to = null;
            float dFrom = float.MaxValue, dTo = float.MaxValue;
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] == null)
                {
                    continue;
                }
                Vector3 p = stations[i].transform.position;
                float a = Flat(p, me), b = Flat(p, at);
                if (a < dFrom)
                {
                    dFrom = a;
                    from = stations[i];
                }
                if (b < dTo)
                {
                    dTo = b;
                    to = stations[i];
                }
            }
            if (from == null || to == null || ReferenceEquals(from, to))
            {
                return false;
            }
            float walkTo = dFrom * MapRouteRules.StraightDetour, walkFrom = dTo * MapRouteRules.StraightDetour;
            if (!MapRouteRules.SubwayOffered(walkTo, walkFrom, road))
            {
                return false;
            }
            seconds = MapRouteRules.SubwaySeconds(walkTo, Flat(from.transform.position, to.transform.position), walkFrom);
            return true;
        }

        private static float Flat(Vector3 a, Vector3 b) => Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));

        // ------------------------------------------------------------------------------------------------- the layers and the buttons

        private void BuildLayers()
        {
            _layers.Title = UiText.T("reallife_map_layers");
            _layers.HideAllLabel = UiText.T("reallife_map_hide_all");
            _layers.HideAllOff = _lit == 0;
            _layerLines.Clear();
            var list = new List<MapLayer>(6);
            AddLayer(list, null, MapChipRules.YoursLine, UiIcon.Key);
            AddLayer(list, "citymap_category_jobs", null, UiIcon.Briefcase);
            AddLayer(list, MapChipRules.RentGroup, null, UiIcon.Building);
            AddLayer(list, "citymap_category_active_businesses", null, UiIcon.Store);
            AddLayer(list, null, "common_subwaystation", UiIcon.Train);
            AddLayer(list, null, "citymap_filter_hide_closed", UiIcon.Clock);
            _layers.Layers = list.ToArray();
        }

        // A layer of the small panel: one of the game's lines by its key, or one of its groups, when the game has it.
        private void AddLayer(List<MapLayer> list, string group, string line, UiIcon icon)
        {
            LineInfo l = null;
            Cat c = null;
            for (int i = 0; line != null && i < _lines.Count && l == null; i++)
            {
                l = _lines[i].Key == line ? _lines[i] : null;
            }
            for (int i = 0; group != null && i < _cats.Count && c == null; i++)
            {
                c = _cats[i].Key == group ? _cats[i] : null;
            }
            if (l == null && c == null)
            {
                return;
            }
            list.Add(new MapLayer { Icon = icon, Name = l != null ? l.Name : c.Name, On = l != null ? l.On : c.Total > 0 && c.On == c.Total, Off = l != null ? !l.Available : c.Total == 0 });
            _layerLines.Add(new LayerRef { Line = l, Cat = c });
        }

        private void BuildButtons()
        {
            CityMapCam cam = CityMapCamera();
            MapButtonsModel b = _model.Buttons;
            b.North = UiText.T("reallife_map_north");
            b.Tilt = Nav.NavMapTilt.Words(Nav.NavTilt.Map, cam != null ? cam.distance : 0f);
            b.Tilted = b.Tilt == "3D";
            b.CanZoomIn = cam != null && cam.distance > cam.minMaxDistance.x + 0.5f;
            b.CanZoomOut = cam != null && cam.distance < cam.minMaxDistance.y - 0.5f;
        }
    }
}
