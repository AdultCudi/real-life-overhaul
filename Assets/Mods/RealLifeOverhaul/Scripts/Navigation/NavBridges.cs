using System;
using System.Collections.Generic;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul.Nav
{
    /// <summary>What the minimap and the GPS ask of the rest of the mod, filled in by Wiring/NavLinks.cs; each has a default.</summary>
    public static class NavBridges
    {
        /// <summary>True while a scene of the mod's has the screen, such as custody or photo mode: the minimap hides. Default: never.</summary>
        public static Func<bool> HudHeld;

        /// <summary>Makes the nearest police station the game's destination; false if there is none. Default: none.</summary>
        public static Func<bool> GuideToStation;

        /// <summary>Adds places that move, such as a vehicle on its way, each time the marks are gathered. Default: none.</summary>
        public static Action<List<NavMark>> MoreMarks;

        /// <summary>Adds the box (the game's own shadow and map look) of each building the mod stands in the city, drawn on the base beside the game's. Default: none.</summary>
        public static Action<List<MeshRenderer>> MoreBuildings;

        /// <summary>Changes whenever the buildings <see cref="MoreBuildings"/> gives do: the base is drawn again. Default: never.</summary>
        public static Func<int> MoreBuildingsStamp;

        /// <summary>True for a building of the game's list that the mod made: left out when the city is measured, so the kept file is the game's city alone. Default: none.</summary>
        public static Func<CityBuildingController, bool> NotMeasured;
    }

    /// <summary>The feature's words with their {placeholders} filled in; called when a line changes, never per frame.</summary>
    public static class NavText
    {
        public static string T(string key)
        {
            return key.GetLocalization();
        }

        public static string T(string key, string name, string value)
        {
            return key.GetLocalization().Replace(name, value ?? string.Empty);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return key.GetLocalization().Replace(name, value ?? string.Empty).Replace(name2, value2 ?? string.Empty);
        }

        /// <summary>The key of a turn's sentence: its kind, "_now" when it is close, "_onto" when the street's name is known.</summary>
        public static string TurnKey(NavTurnKind kind, bool now, bool named)
        {
            string stem;
            switch (kind)
            {
                case NavTurnKind.Left: stem = "reallife_nav_left"; break;
                case NavTurnKind.Right: stem = "reallife_nav_right"; break;
                case NavTurnKind.KeepLeft: stem = "reallife_nav_keep_left"; break;
                case NavTurnKind.KeepRight: stem = "reallife_nav_keep_right"; break;
                case NavTurnKind.UTurn: stem = "reallife_nav_uturn"; break;
                default: return now ? "reallife_nav_arrive_now" : "reallife_nav_arrive";
            }
            return stem + (now ? "_now" : string.Empty) + (named ? "_onto" : string.Empty);
        }
    }
}
