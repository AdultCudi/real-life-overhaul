using System.Collections.Generic;
using System.Text;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's places on the city map, part: as the map shuts (docs/standards/performance.md, RLO-PERF-022). The game puts
    /// out the light of every building it lit, but leaves the outline layer on a building that is not active as the map
    /// shuts (OnIoExit), so a building of the mod's lit then would come back outlined; it is put out here the game's way.
    /// The look for the map check: the mod's markers as in the street, no building of the mod's left lit.
    /// </summary>
    public static partial class MapPlaces
    {
        private static readonly MapParts.Look MapLookCall = MapLook;   // made once

        // After the categories were put as in the street (OnMap): a lit building the game left outlined, put out.
        private static void Shut()
        {
            int outlined = LayerHelper.BuildingOutlinedLayerIndex;
            for (int i = 0; i < Lights.Count; i++)
            {
                CityBuildingController building = Lights[i].LitBuilding;
                if (building == null || building.gameObject.activeInHierarchy || !Outlined(building, outlined))
                {
                    continue;   // the game's own close put it out
                }
                building.ClearHighlight();   // its parts' layers and its outline colour
                int plain = LayerHelper.BuildingLayerIndex;
                if (building.lodVersion != null && building.lodVersion.gameObject.layer == outlined)
                {
                    building.lodVersion.gameObject.layer = plain;
                }
                if (building.gameObject.layer == outlined)
                {
                    building.gameObject.layer = plain;
                }
            }
        }

        // True if anything of the building's the game outlines is on the outline layer.
        private static bool Outlined(CityBuildingController building, int outlined)
        {
            if (building.gameObject.layer == outlined || (building.lodVersion != null && building.lodVersion.gameObject.layer == outlined))
            {
                return true;
            }
            List<ViewBlockingEntityPart> parts = building.cityBuildingParts;
            for (int i = 0; parts != null && i < parts.Count; i++)
            {
                if (parts[i] != null && parts[i].gameObject.layer == outlined)
                {
                    return true;
                }
            }
            return false;
        }

        // The map check after the map shut (Diagnostics/SessionWatch.Map): the mod's markers as in the street (a street
        // marker hidden as its row says, the map's own off) and no building of the mod's still lit, but the one the pointer
        // is on. Counts only; words only with a line.
        private static int MapLook(StringBuilder line)
        {
            int markers = 0, lit = 0;
            for (int c = 0; c < Categories.Count; c++)
            {
                Category category = Categories[c];
                IReadOnlyList<MapPlace> places = category.Places;
                for (int i = 0; i < places.Count; i++)
                {
                    PointOfInterest marker = places[i].Marker;
                    if (marker == null)
                    {
                        continue;
                    }
                    bool wrong = category.InStreet ? marker.Permanent && marker.hidden == category.On : marker.gameObject.activeSelf;
                    markers += wrong ? 1 : 0;
                }
            }
            int outlined = LayerHelper.BuildingOutlinedLayerIndex;
            EntityController pointed = MouseController.currentTargetEntity;   // the pointer outlines a building as it should
            if (pointed is ViewBlockingEntityPart part)
            {
                pointed = part.cityBuildingController;
            }
            for (int i = 0; i < Lights.Count; i++)
            {
                CityBuildingController building = Lights[i].LitBuilding;
                if (building != null && !ReferenceEquals(building, pointed) && Outlined(building, outlined))
                {
                    lit++;
                }
            }
            bool holding = _mapOpen || markers > 0 || lit > 0;
            if (line != null)
            {
                line.Append("map places: ");
                if (_mapOpen)
                {
                    line.Append("still as for the open map, ");
                }
                line.Append(markers).Append(" marker(s) not as in the street, ").Append(lit).Append(" of ").Append(Lights.Count).Append(" building(s) of the mod's still lit");
            }
            return holding ? 1 : 0;
        }
    }
}
