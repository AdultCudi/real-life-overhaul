using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The 2.5D pictures' tilt as the player keeps it: the minimap tilted or flat (the card's 2D and 3D button), and the city map flat, tilted or by its distance
    /// (the map's tilt button). A player's choice of the screen, not of the save, so it is kept in the game's player preferences as the map's ticked rows are; the
    /// first look is the default. Cost: one preference read when the city loads and one write a press.
    /// </summary>
    public static class NavTilt
    {
        private const string MinimapKey = "rlo_minimap_tilt";
        private const string MapKey = "rlo_map_tilt";

        private static int _minimap = -1, _map = -1;

        /// <summary>The minimap shows its picture tilted (the default) or flat.</summary>
        public static bool Minimap
        {
            get
            {
                if (_minimap < 0)
                {
                    _minimap = NavDestRules.TiltedFrom(UnityEngine.PlayerPrefs.GetInt(MinimapKey, 1)) ? 1 : 0;
                }
                return _minimap != 0;
            }
        }

        /// <summary>Turns the minimap's picture flat or tilted and remembers it; returns the new state.</summary>
        public static bool ToggleMinimap()
        {
            _minimap = Minimap ? 0 : 1;
            UnityEngine.PlayerPrefs.SetInt(MinimapKey, _minimap);
            return _minimap != 0;
        }

        /// <summary>The city map's tilt: 0 by the camera's distance (the default), 1 flat, 2 tilted.</summary>
        public static int Map
        {
            get
            {
                if (_map < 0)
                {
                    _map = Mathf.Clamp(UnityEngine.PlayerPrefs.GetInt(MapKey, 0), 0, 2);
                }
                return _map;
            }
        }

        /// <summary>The map's button: by distance, then flat, then tilted, then by distance again; returns the new state.</summary>
        public static int StepMap()
        {
            _map = NavMapTilt.Next(Map);
            UnityEngine.PlayerPrefs.SetInt(MapKey, _map);
            return _map;
        }
    }
}
