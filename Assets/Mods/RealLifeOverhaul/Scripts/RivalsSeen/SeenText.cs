using System.Collections.Generic;
using Localizor;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The words of the rivals seen around the city that are built from the state: a sighting's line, a rival's lines for their page. Every other text is a locale key
    /// written whole where it is used. A car and a district are named in the game's own words.
    /// </summary>
    public static class SeenText
    {
        public static string T(string key) => key.GetLocalization();

        /// <summary>A text with its names filled in: pairs of a name and what goes in its place.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        /// <summary>What fills a sighting's line: who, their firm, the place and its district, the car and the one before it.</summary>
        public static Dictionary<string, string> Data(SeenNote n)
        {
            return new Dictionary<string, string>(6)
            {
                { "name", n.Name ?? string.Empty }, { "firm", n.Firm ?? string.Empty }, { "place", n.Place ?? string.Empty }, { "district", SeenGame.DistrictName(n.District) },
                { "car", SeenGame.CarName(n.Car) }, { "old", SeenGame.CarName(n.Old) }
            };
        }

        /// <summary>A sighting as words, for Flare and the pages; empty for one with no line.</summary>
        public static string Line(SeenNote n)
        {
            string key = n != null ? SeenDay.LineKey(n) : string.Empty;
            if (key.Length == 0)
            {
                return string.Empty;
            }
            string text = key.GetLocalization();
            foreach (KeyValuePair<string, string> pair in Data(n))
            {
                text = text.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty);
            }
            return text;
        }

        public static string Form(SeenForm form) => T(SeenCatalogue.FormNames[SeenCatalogue.Clamp((int)form, 0, SeenCatalogue.FormNames.Length - 1)]);

        public static string Spot(SeenSpot spot) => T(SeenCatalogue.SpotNames[SeenCatalogue.Clamp((int)spot, 0, SeenCatalogue.SpotNames.Length - 1)]);

        /// <summary>Where a rival tends to be found, by how their business is doing.</summary>
        public static string Habit(SeenForm form) => T(SeenCatalogue.HabitKeys[SeenCatalogue.Clamp((int)form, 0, SeenCatalogue.HabitKeys.Length - 1)]);
    }
}
