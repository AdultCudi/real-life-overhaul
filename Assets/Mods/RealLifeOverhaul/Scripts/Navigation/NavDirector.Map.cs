using System;
using System.Collections.Generic;
using System.Text;
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
        private NavRouteLabel _labelSent;

        /// <summary>
        /// Where the interface draws the label on the route's line over the open city map: called with the label whenever it changes (a route found, the map opened) and with
        /// <see cref="NavRouteLabel.On"/> false once there is none or the map has shut. Set by the interface while its map panels are on, else null.
        /// </summary>
        public static Action<NavRouteLabel> RouteLabelTo;

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
            RouteLabelKeep();
            OpenCamera(open && !_voogle);
            if (_broken || _voogle || !Settings.Gps || _mapGaveUp)
            {
                if (_map != null)
                {
                    _map.Open(false);
                }
                return;
            }
            if (open && _map == null)
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

        // The label for the line on the map: the middle of the line as drawn, with the minutes and the way to go the minimap's block says. Handed over only when one of them changed
        // (the map pauses the game, so that is a route found or the map opened), and taken back when the route is gone or the map has shut. Four times a second and on the map's event.
        private void RouteLabelKeep()
        {
            Action<NavRouteLabel> to = RouteLabelTo;
            if (to == null)
            {
                _labelSent.On = false;
                return;
            }
            var now = new NavRouteLabel { Minutes = _minutesWords, Distance = _distanceWords, OnFoot = _routeOnFoot };
            now.On = _mapOpen && _map != null && _phase == Phase.Routed && !string.IsNullOrEmpty(_minutesWords) && LineMiddle(out now.At);
            bool same = now.On == _labelSent.On
                        && (!now.On || (now.Minutes == _labelSent.Minutes && now.Distance == _labelSent.Distance && now.OnFoot == _labelSent.OnFoot
                                        && (now.At - _labelSent.At).sqrMagnitude < 1f));
            if (same)
            {
                return;
            }
            _labelSent = now;
            to(now);
        }

        // The point halfway along the line drawn on the map: on the line itself, so the label never floats off it.
        private bool LineMiddle(out Vector3 middle)
        {
            bool found = NavMath.Halfway(_lineX, _lineY, _lineZ, _lineCount, out float x, out float y, out float z);
            middle = new Vector3(x, y, z);
            return found;
        }

        // The map check after the map shut (Diagnostics/SessionWatch.Map): the route's object off with the map and the game's camera's tilt given back.
        private int MapLook(StringBuilder line)
        {
            bool shown = _map != null && _map.gameObject.activeSelf;
            line?.Append("route on the map: ").Append(_mapOpen ? "still as for the open map" : shown ? "its object still shown" : "off with the map").Append("; ");
            return (_mapOpen || shown ? 1 : 0) + CameraLook(line);
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
