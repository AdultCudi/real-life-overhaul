using System;
using System.Collections.Generic;
using Streets;
using UI;
using UI.Guiders;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>The director's part for the game's city map: the route drawn on it and a destination set by a double click.</summary>
    public sealed partial class NavDirector
    {
        private Action<bool> _onMap, _mapSeen;
        private bool _mapOpen;

        // The game's own event, and the slow tick if the event was missed. Nothing thrown here reaches the game.
        private void OnMap(bool open)
        {
            using var hotSpot = HotSpots.Time(HotOnMap);
            try
            {
                MapToggled(open);
            }
            catch (Exception e)
            {
                _mapGaveUp = true;
                Logger?.Error(e);
                Logger?.Warn("Navigation: the route on the city map is switched off for this session after an error.");
            }
        }

        private void MapToggled(bool open)
        {
            _mapOpen = open;
            if (_broken || _voogle || !Settings.Gps || _mapGaveUp)
            {
                if (_map != null)
                {
                    _map.Open(false);
                }
                return;
            }
            if (open && _map == null && !_mapGaveUp)
            {
                Colors colours = InstanceBehavior<GlobalReferences>.Instance != null ? InstanceBehavior<GlobalReferences>.Instance.colors : null;
                Color32 colour = colours != null ? colours.blue : new Color32(60, 130, 240, 255);
                colour.a = 235;
                _map = NavCityMap.Make(colour);
                _mapGaveUp = _map == null;
                if (_mapGaveUp)
                {
                    Logger?.Warn("Navigation: the city map's marker layer was not found, so the route is not drawn on the city map and a double click there sets no destination.");
                }
                else
                {
                    _map.Picked = Pick;
                    _map.SetRoute(_lineX, _lineY, _lineZ, _lineCount);
                }
            }
            if (_map != null)
            {
                _map.Clicks = true;
                _map.Open(open);
            }
        }

        // Two clicks on one spot of the open map: that spot is the destination.
        private void Pick(Vector2 onScreen)
        {
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                GuidersManager guiders = InstanceBehavior<GuidersManager>.Instance;
                Camera camera = GameManager.GetMainCamera();
                if (!CityMap.IsOpen || city == null || city.cityMap == null || city.cityMap.IsTaxiMode || city.cityMap.isSubwayMode || BuildingPreview.isPreviewing
                    || guiders == null || guiders.destinationGuider == null || camera == null || SaveGameManager.Current == null)
                {
                    return;
                }
                Ray ray = camera.ScreenPointToRay(onScreen);
                int mask = city.cityMap.cityMapMask.value | city.cityMap.cityMapMaskLowDetail.value;
                Vector3 place;
                CityBuildingController building = null;
                if (Physics.Raycast(ray, out RaycastHit hit, 4000f, mask, QueryTriggerInteraction.Ignore))
                {
                    place = hit.point;
                    building = hit.collider != null ? hit.collider.GetComponentInParent<CityBuildingController>() : null;
                }
                else if (ray.direction.y < -0.01f)
                {
                    place = ray.origin + ray.direction * ((_py - ray.origin.y) / ray.direction.y);   // nothing solid there: the player's level
                }
                else
                {
                    return;
                }
                string name;
                if (building != null && building.building != null && !building.building.Address.IsUndefined())
                {
                    Address address = building.building.Address;
                    GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination);   // as a building's card does, the address kept in the save
                    name = address.ToFormattedString();
                    string special = SpecialBuildings.NameFor(address);
                    if (special != null)
                    {
                        guiders.destinationGuider.poi.SetText(special);   // the special building's own name on the marker at once
                        name = NavText.T("reallife_map_name_at").Replace("{what}", special).Replace("{place}", name);   // "Courthouse: 12 Third Street"
                    }
                }
                else
                {
                    SaveGameManager.Current.customDestination = null;   // a spot is not an address: the save keeps none
                    name = _graph.Ready ? _graph.StreetNear(place.x, place.y, place.z) : null;
                    GuidersManager.SetGuiderTarget(place + Vector3.up * 1.5f, name, guiders.destinationGuider.poiICon, guiders.destinationGuider.poiBackgroundColor, DirectionGuiderType.Destination);
                    name = name ?? NavText.T("poi_custom_location");
                }
                Notifications.Show(NotificationType.Success, "notification_destination_set", new Dictionary<string, string>(1) { { "address", name } });
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Navigation: destination set by a double click on the city map: '" + name + "' at " + place.x.ToString("F0") + ", " + place.z.ToString("F0")
                                 + (building != null ? " (a building)." : " (a spot)."));
                }
                _next = 0f;   // looked at this frame, so the route is on the map at once
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
