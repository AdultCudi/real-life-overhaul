using System;
using System.Collections.Generic;
using Localizor;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// What the minimap and the GPS ask of the rest of the mod, each with an answer of its own when nobody has
    /// filled it in (Wiring/NavLinks.cs does, once per city load).
    /// </summary>
    public static class NavBridges
    {
        /// <summary>True while a scene of the mod's has the screen (custody, a bus ride, photo mode): the minimap is hidden. Default: never.</summary>
        public static Func<bool> HudHeld;

        /// <summary>Makes the nearest police station the game's destination; false if there is none. Default: none (the test says so).</summary>
        public static Func<bool> GuideToStation;

        /// <summary>
        /// Adds places that move to the minimap's marks, each time they are gathered (every two seconds while the minimap
        /// shows): a vehicle on its way to the player. Whoever fills it in adds to the list and nothing else. Default: none.
        /// </summary>
        public static Action<List<NavMark>> MoreMarks;
    }

    /// <summary>The feature's words: a line of en.json with its {placeholders} filled in. Called when a line changes, never per frame.</summary>
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
