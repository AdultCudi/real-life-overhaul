using System.Collections.Generic;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>The seasons' words: the seasons' names, the goods that sell more and less in one, and the snow's hours as the game writes hours.</summary>
    public static class SeasonText
    {
        public const int Named = 3;   // goods named each way in a line

        private static readonly string[] English = { "winter", "spring", "summer", "autumn" };

        private static readonly string[] TitleKeys =
        {
            "reallife_season_name_winter", "reallife_season_name_spring", "reallife_season_name_summer", "reallife_season_name_autumn"
        };

        /// <summary>A season's name in English, for the log.</summary>
        public static string Name(int season)
        {
            return season >= 0 && season < English.Length ? English[season] : "?";
        }

        /// <summary>A season's name in the player's language.</summary>
        public static string Title(int season)
        {
            return season >= 0 && season < TitleKeys.Length ? TitleKeys[season].GetLocalization() : string.Empty;
        }

        public static string T(string key)
        {
            return key.GetLocalization();
        }

        public static string T(string key, string name, string value)
        {
            return key.GetLocalization().Replace("{" + name + "}", value ?? string.Empty);
        }

        /// <summary>The goods that sell most more (or most less) in a season, named as the game names them: "Cup of Tea, Whisky and Gift (Expensive)".</summary>
        public static string Goods(int season, bool rises)
        {
            string a = Ranked(season, 0, rises), b = Ranked(season, 1, rises), c = Ranked(season, 2, rises);
            if (b.Length == 0)
            {
                return a;
            }
            string list = T(c.Length == 0 ? "reallife_season_list_two" : "reallife_season_list_three", "a", a).Replace("{b}", b);
            return list.Replace("{c}", c);
        }

        // The name of the good in a place of the season's ranking, or nothing past the last.
        private static string Ranked(int season, int rank, bool rises)
        {
            int good = rank < Named ? SeasonRules.Ranked(season, rank, rises) : -1;
            return good >= 0 ? SeasonGame.Label(good) : string.Empty;
        }

        /// <summary>"Winter: Cup of Tea, Whisky and Gift (Expensive) sell more, Ice Cream, Slushie and Salad less."</summary>
        public static string Line(int season)
        {
            return T("reallife_season_line", "season", Title(season)).Replace("{more}", Goods(season, true)).Replace("{less}", Goods(season, false));
        }

        /// <summary>The words a season's first morning is told with.</summary>
        public static Dictionary<string, string> TurnData(int season)
        {
            return new Dictionary<string, string>(3) { { "season", Title(season) }, { "more", Goods(season, true) }, { "less", Goods(season, false) } };
        }

        /// <summary>The hours a fall begins and ends, as the game writes hours.</summary>
        public static Dictionary<string, string> Hours(SnowDay snow)
        {
            return new Dictionary<string, string>(2) { { "from", Clock(snow.From) }, { "to", Clock(snow.To) } };
        }

        /// <summary>An hour of the day in the game's own 12 or 24 hour format; hour 24 is midnight.</summary>
        public static string Clock(int hour)
        {
            return (((hour % 24) + 24) % 24).GetFormattedTime(0);
        }
    }
}
