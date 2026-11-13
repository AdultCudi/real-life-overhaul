using System;
using System.Collections.Generic;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The player's saved places on the city map: a row of its filters, "Saved places", with a marker on each place, as the mod's other places are (MapPlaces). The place
    /// card's Save puts a place in the list; this makes the list something the player sees on the map and can switch off. Looked at once a second,
    /// with the interface on or off (a place saved stays on the game's own map), and only when the list has changed does it do anything:
    /// the markers are made again for the places now saved. A fault is one line in the log and the row is left off.
    /// </summary>
    internal static class MapSavedGame
    {
        private const string Id = "saved";
        private static readonly Color Gold = new Color(0.95f, 0.78f, 0.3f);

        private static MapPlaces.Category _row;
        private static int _seen = -1;
        private static CityManager _city;
        private static bool _broken;

        /// <summary>Once a second: the row follows the list of saved places.</summary>
        public static void Look()
        {
            if (_broken)
            {
                return;
            }
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (city == null)
                {
                    return;
                }
                if (!ReferenceEquals(_city, city))
                {
                    _city = city;   // a new city's map has none of the last one's markers, with the interface on or off
                    _row = null;
                    _seen = -1;
                }
                int version = MapStoreGame.SavedVersion;
                if (version == _seen)
                {
                    return;
                }
                _seen = version;
                var places = new List<MapPlace>(MapStoreGame.Count);
                for (int i = 0; i < MapStoreGame.Count; i++)
                {
                    MapSavedPlace saved = MapStoreGame.SavedAt(i);
                    var address = new Address(saved.Street, saved.Number);
                    CityBuildingController building = city.FindCityBuildingController(address);
                    if (building != null)
                    {
                        places.Add(new MapPlace(Spot(building), string.IsNullOrEmpty(saved.Name) ? address.ToFormattedString() : saved.Name, building) { Address = address });
                    }
                }
                if (_row == null && places.Count == 0)
                {
                    return;   // nothing saved, so no row yet
                }
                if (_row == null)
                {
                    _row = MapPlaces.Register(Id, "reallife_map_row_saved", Gold, MapIcon.Star, true);
                    _row.Available = () => MapStoreGame.Count > 0;
                }
                _row.Set(places);
            }
            catch (Exception e)
            {
                _broken = true;
                UiDirector.Logger?.Error(e);
                UiDirector.Logger?.Warn("UI: the city map's row of saved places could not be kept after the error above; the places are still saved.");
            }
        }

        // A building's door, else its middle.
        private static Vector3 Spot(CityBuildingController building)
        {
            if (building.entranceDoors != null && building.entranceDoors.Length > 0 && building.entranceDoors[0].doorTransform != null)
            {
                return building.entranceDoors[0].doorTransform.position;
            }
            return building.transform.position;
        }

        /// <summary>The city has gone: its map's row went with it (MapPlaces.Shutdown).</summary>
        public static void Forget()
        {
            _row = null;
            _city = null;
            _seen = -1;
            _broken = false;
        }
    }
}
