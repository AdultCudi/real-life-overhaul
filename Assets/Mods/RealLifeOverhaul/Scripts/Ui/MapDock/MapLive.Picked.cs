using System;
using BigAmbitions.DayNightCycle;
using Buildings;
using Helpers;
using Streets;
using TMPro;
using UI;
using UI.InGameUI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The picked place of the live dock: the game's building card read into the dock's short block, and its buttons pressed.</summary>
    internal sealed partial class MapLive
    {
        private const string PickedPicture = "map:picked";

        private CityBuildingController _pickedBuilding;   // the building picked, or null for a station or a taxi stop
        private EntityController _pickedTarget;
        private Vector3 _pickedAt;
        private float _pickedRoad = -1f;                  // metres by road from the player, or -1 where the roads are not known

        private static BuildingResume Card()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.buildingResume : null;
        }

        // The place the player clicked on the map (the game's building card shows its options once a building is clicked, and only then): false when none.
        private bool ReadPicked(ref MapPicked p)
        {
            BuildingResume card = Card();
            EntityController target = card != null ? GameMembers.TargetOf(card) : null;
            if (target == null || card.options == null || !card.options.gameObject.activeSelf)
            {
                _pickedTarget = null;
                _pickedBuilding = null;
                return false;
            }
            if (!ReferenceEquals(target, _pickedTarget))
            {
                _pickedTarget = target;
                _pickedBuilding = card.CityBuildingController;
                _pickedAt = PositionOf(target, _pickedBuilding);
                _pickedRoad = RoadTo(_pickedAt);
            }
            bool taxi = InstanceBehavior<CityManager>.Instance.cityMap.IsTaxiMode;
            BuildingRegistration reg = _pickedBuilding != null ? _pickedBuilding.buildingRegistration : null;
            string address = _pickedBuilding != null ? _pickedBuilding.building.Address.ToFormattedString() : LabelWords(card.addressLabel);
            string district = _pickedBuilding != null ? Districts.DistrictGame.Name(_pickedBuilding.building.Neighbourhood) : null;
            string name = reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : address;
            p.Name = name;
            p.Address = string.IsNullOrEmpty(district) || name == address ? address : address + ", " + district;
            if (name == address)
            {
                p.Address = district;
            }
            p.Distance = DistanceWords(_pickedAt, _pickedRoad);
            p.Icon = reg != null ? TypeGlyph(reg) : UiIcon.MapPin;
            p.Tint = reg != null ? UiRole.CatRetail : UiRole.None;
            p.Picture = PictureOfPlace(_pickedBuilding, PickedPicture);
            p.Chips = PlaceChips(card, reg);
            p.BlockLabel = UiText.T("reallife_map_picked");
            p.ClearLabel = UiText.T("reallife_map_clear");
            p.LocateLabel = UiText.T("reallife_map_locate");
            p.DestinationLabel = taxi ? TaxiWords(card) : UiText.T("common_setdestination");
            p.CanDestination = taxi ? UiWidgets.Shows(card.taxiTravelButton) : UiWidgets.Shows(card.setDestinationButton);
            p.CanDetails = _pickedBuilding != null && reg != null;
            p.CanBizMan = OffersBizMan(card);
            p.BizManLabel = UiText.T("reallife_co_open_bizman");
            return true;
        }

        // A game label's words on one line, its text tags taken out (the open state's break between "Open" and "Closing in 7 hours" was drawn as the tag itself).
        private static string LabelWords(Localizor.LanguageChangeEvent.TextLocalizationComponent label) => MapText.OneLine(LabelLines(label));

        // The same with its breaks kept as line feeds, for the one label that wants its lines apart.
        private static string LabelLines(Localizor.LanguageChangeEvent.TextLocalizationComponent label)
        {
            TMP_Text text = label != null ? label.TextContainer : null;
            return text != null ? MapText.Plain(text.text) : null;
        }

        // The taxi button's own words ("Travel here" over the fare and the minutes), as the game writes them.
        private static string TaxiWords(BuildingResume card)
        {
            string words = LabelLines(card.taxiTravelButtonText);
            return string.IsNullOrWhiteSpace(words) ? UiText.T("common_setdestination") : words.Replace("\n", ", ").Trim();
        }

        private static Vector3 PositionOf(EntityController target, CityBuildingController building)
        {
            if (building != null && building.entranceDoors != null && building.entranceDoors.Length > 0 && building.entranceDoors[0].doorTransform != null)
            {
                return building.entranceDoors[0].doorTransform.position;
            }
            return target.transform.position;
        }

        // How far the place is: the road from the player where the roads are known, else the straight line.
        private static string DistanceWords(Vector3 at, float road)
        {
            if (road >= 0f)
            {
                return UiText.T("reallife_map_distance_road").Replace("{distance}", Mathf.RoundToInt(road).ToFormattedDistance());
            }
            var uis = InstanceBehavior<GameManager>.Instance;
            Vector3 me = uis != null && uis.playerController != null ? uis.playerController.transform.position : at;
            float flat = Mathf.Sqrt((at.x - me.x) * (at.x - me.x) + (at.z - me.z) * (at.z - me.z));
            return UiText.T("reallife_map_distance_line").Replace("{distance}", Mathf.RoundToInt(flat).ToFormattedDistance());
        }

        // Metres by road to a place as the minimap's graph gives them, or -1 (the graph is not made, or there is no way).
        private static float RoadTo(Vector3 at)
        {
            Nav.NavDirector director = Nav.NavDirector.Instance;
            return director != null && director.Probe(at.x, at.z, false, out float metres) ? metres : -1f;
        }

        // The state chips of a place: open, closed, to let; whose it is (yours, a rival's); the card's own words and tones.
        private static MapStateChip[] PlaceChips(BuildingResume card, BuildingRegistration reg)
        {
            var chips = new System.Collections.Generic.List<MapStateChip>(2);
            if (reg != null)
            {
                string state = card.openStateLabel != null && card.openStateLabel.gameObject.activeSelf ? LabelWords(card.openStateLabel) : null;
                if (!string.IsNullOrWhiteSpace(state))
                {
                    UiRole tone = reg.AvailableForRent ? UiRole.Accent : BusinessHelper.IsBusinessOpen(reg) ? UiRole.Ok : UiRole.Danger;
                    chips.Add(new MapStateChip { Label = state.Trim(), Tone = tone });
                }
                string owner = card.rivalBusinessOwner != null && card.rivalBusinessOwner.gameObject.activeSelf ? LabelWords(card.rivalBusinessOwner) : null;
                if (!string.IsNullOrWhiteSpace(owner))
                {
                    chips.Add(new MapStateChip { Label = owner.Trim(), Tone = reg.RentedByPlayer ? UiRole.Accent : UiRole.Warn });
                }
            }
            return chips.ToArray();
        }

        // A building's kind as a glyph of the kit's: the type of its business, else its building's.
        private static UiIcon TypeGlyph(BuildingRegistration reg)
        {
            string type = reg.businessTypeName != null ? reg.businessTypeName.ToLowerInvariant() : string.Empty;
            if (MapChipRules.Matches(MapChipKind.Food, type, null))
            {
                return UiIcon.ForkKnife;
            }
            if (MapChipRules.Matches(MapChipKind.Groceries, type, null))
            {
                return UiIcon.ShoppingBag;
            }
            if (MapChipRules.Matches(MapChipKind.Fuel, type, null))
            {
                return UiIcon.Lightning;
            }
            return reg.businessTypeName == "ba:businesstype_empty" ? UiIcon.Key : UiIcon.Store;
        }

        // The game's own picture of a building's kind (its map icon), kept under a key for the kit; null when the game has none.
        private string PictureOfPlace(CityBuildingController building, string key)
        {
            if (building == null || building.building == null)
            {
                return null;
            }
            BuildingRegistration reg = building.buildingRegistration;
            Sprite sprite = null;
            if (reg != null && reg.businessTypeName != "ba:businesstype_empty")
            {
                sprite = BusinessTypeHelper.GetData(reg).icon;
            }
            if (sprite == null)
            {
                sprite = BuildingTypeHelper.GetData(building.building).poiIcon;
            }
            if (sprite == null)
            {
                return null;
            }
            Pictures[key] = sprite;
            return key;
        }

        // ------------------------------------------------------------------------------------------------- the picked block's buttons

        public bool PickedLocate()
        {
            CityMapCam cam = CityMapCamera();
            if (_pickedTarget == null || cam == null)
            {
                return false;
            }
            cam.MoveCameraToTarget(_pickedAt);
            return true;
        }

        public bool PickedDestination()
        {
            BuildingResume card = Card();
            if (card == null)
            {
                return false;
            }
            return InstanceBehavior<CityManager>.Instance.cityMap.IsTaxiMode ? UiWidgets.Press(card.taxiTravelButton) : UiWidgets.Press(card.setDestinationButton);
        }

        public bool PickedClear()
        {
            BuildingResume card = Card();
            if (card == null)
            {
                return false;
            }
            card.Close();
            return true;
        }

        // The game's card shows its BizMan button for a building it has a registration for, and the kit offers it exactly then, through that button's own click.
        private static bool OffersBizMan(BuildingResume card) => card != null && UiWidgets.Shows(card.bizManButton);

        public bool PickedBizMan()
        {
            BuildingResume card = Card();
            return card != null && UiWidgets.Press(card.bizManButton);
        }

        public bool PickedMore()
        {
            if (_pickedBuilding == null || _pickedBuilding.buildingRegistration == null)
            {
                return false;
            }
            _dock = Dock.Place;
            _routeShown = false;
            return true;
        }
    }
}
