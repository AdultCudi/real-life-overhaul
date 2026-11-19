using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The rivals' head offices on the city map: one row in the map's filters ("Rivals' head offices") and a marker on each office with the rival's name, which the game's own map
    /// and minimap show whichever interface is on (Core/MapPlaces), and whose click sets the destination. Made again on the day's pass and when the language changes; taken off when
    /// the feature stops. COST: a few markers once a day; nothing per frame of its own.
    /// </summary>
    internal static class SeenMap
    {
        private const string Id = "rival_offices";
        private static readonly Color Colour = new Color(0.36f, 0.46f, 0.66f);
        private static readonly List<MapPlace> Places = new List<MapPlace>(32);
        private static MapPlaces.Category _map;

        /// <summary>The head offices of the day's rivals as places on the map; how many were put there. Those whose building the city's table did not find are left off.</summary>
        public static int Set(IList<SeenSubject> subjects, SeenTable table)
        {
            try
            {
                Places.Clear();
                for (int i = 0; subjects != null && i < subjects.Count; i++)
                {
                    SeenSubject s = subjects[i];
                    if (s == null || s.Gone || s.Head.Length == 0 || !table.Office(s.Head, out object building, out float _, out float _) || !(building is CityBuildingController c))
                    {
                        continue;
                    }
                    Places.Add(new MapPlace(c.transform.position, SeenText.Fill("reallife_seen_map_name", "name", s.Name), c) { CardTypeKey = "reallife_seen_map_card" });
                }
                if (_map == null)
                {
                    _map = MapPlaces.Register(Id, "reallife_seen_map_row", Colour, MapIcon.Tower, true, inStreet: true);
                    _map.Available = () => SeenDirector.Instance != null && SeenDirector.Instance.Running;
                }
                _map.Set(Places);
                return Places.Count;
            }
            catch (Exception e)
            {
                SeenDirector.Logger?.Error(e);
                return 0;
            }
        }

        /// <summary>Every marker off the map: the feature stops, the city unloads.</summary>
        public static void Clear()
        {
            try
            {
                Places.Clear();
                _map?.Set(Places);
            }
            catch (Exception e)
            {
                SeenDirector.Logger?.Error(e);
            }
        }
    }
}
