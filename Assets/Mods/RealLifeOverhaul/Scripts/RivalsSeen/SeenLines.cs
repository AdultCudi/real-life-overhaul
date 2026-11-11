using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.RivalsSeen
{
    /// <summary>
    /// The feature's lines in words: a sighting's line for Flare, the paper and the notices, and a rival's few words for their pages, in the language an
    /// <see cref="ISeenWords"/> gives (the game's through SeenText, a locale's in the checks). A text's {names} are filled in one walk of it, with no table of
    /// names made and no name filled twice. Game-free. COST: one walk of the text and the one string returned; the builder is kept.
    /// </summary>
    public static class SeenLines
    {
        public const string CarKey = "reallife_seen_page_car", TodayKey = "reallife_seen_today";

        private static readonly StringBuilder Text = new StringBuilder(256);

        /// <summary>A sighting as words; empty for one with no line.</summary>
        public static string Line(SeenNote n, ISeenWords w)
        {
            string key = n != null && w != null ? SeenDay.LineKey(n) : string.Empty;
            if (key.Length == 0)
            {
                return string.Empty;
            }
            string text = w.T(key) ?? string.Empty;
            Text.Clear();
            for (int i = 0; i < text.Length; i++)
            {
                int close = text[i] == '{' ? text.IndexOf('}', i + 1) : -1;
                string value = close > i + 1 ? Value(n, text, i + 1, close - i - 1, w) : null;
                if (value != null)
                {
                    Text.Append(value);
                    i = close;
                }
                else
                {
                    Text.Append(text[i]);
                }
            }
            return Text.ToString();
        }

        // What fills a name of a sighting's line: who, their firm, the place and its district, the car and the one before it; null for any other name.
        private static string Value(SeenNote n, string text, int at, int length, ISeenWords w)
        {
            if (Is(text, at, length, "name"))
            {
                return n.Name ?? string.Empty;
            }
            if (Is(text, at, length, "firm"))
            {
                return n.Firm ?? string.Empty;
            }
            if (Is(text, at, length, "place"))
            {
                return n.Place ?? string.Empty;
            }
            if (Is(text, at, length, "district"))
            {
                return string.IsNullOrEmpty(n.District) ? string.Empty : w.District(n.District) ?? string.Empty;
            }
            if (Is(text, at, length, "car"))
            {
                return string.IsNullOrEmpty(n.Car) ? string.Empty : w.Car(n.Car) ?? string.Empty;
            }
            if (Is(text, at, length, "old"))
            {
                return string.IsNullOrEmpty(n.Old) ? string.Empty : w.Car(n.Old) ?? string.Empty;
            }
            return null;
        }

        private static bool Is(string text, int at, int length, string name) => length == name.Length && string.CompareOrdinal(text, at, name, 0, length) == 0;

        /// <summary>A text with up to two {names} filled in; any other name is left as it is.</summary>
        public static string Fill(string text, string name, string value, string name2 = null, string value2 = null)
        {
            text = text ?? string.Empty;
            Text.Clear();
            for (int i = 0; i < text.Length; i++)
            {
                int close = text[i] == '{' ? text.IndexOf('}', i + 1) : -1;
                if (close > i + 1 && name != null && Is(text, i + 1, close - i - 1, name))
                {
                    Text.Append(value ?? string.Empty);
                    i = close;
                }
                else if (close > i + 1 && name2 != null && Is(text, i + 1, close - i - 1, name2))
                {
                    Text.Append(value2 ?? string.Empty);
                    i = close;
                }
                else
                {
                    Text.Append(text[i]);
                }
            }
            return Text.ToString();
        }

        /// <summary>A place's word, for a page and the panel.</summary>
        public static string Spot(SeenSpot spot, ISeenWords w) => w.T(SeenCatalogue.SpotNames[SeenCatalogue.Clamp((int)spot, 0, SeenCatalogue.SpotNames.Length - 1)]);

        /// <summary>Where a rival tends to be found, by how their business is doing.</summary>
        public static string Habit(SeenForm form, ISeenWords w) => w.T(SeenCatalogue.HabitKeys[SeenCatalogue.Clamp((int)form, 0, SeenCatalogue.HabitKeys.Length - 1)]);

        /// <summary>The same in a few words, for a narrow place on a page.</summary>
        public static string Found(SeenForm form, ISeenWords w) => w.T(SeenCatalogue.FoundKeys[SeenCatalogue.Clamp((int)form, 0, SeenCatalogue.FoundKeys.Length - 1)]);

        /// <summary>
        /// A tycoon's line for their panel and the rich list: the car they drive and where they tend to be found, and for somebody who knows the player well enough,
        /// where they will be next today from this hour. Empty for somebody not on record, never marked or gone.
        /// </summary>
        public static string Tycoon(SeenFile f, SeenRecord r, SeenSubject s, int day, int hour, ISeenWords w)
        {
            if (f == null || r == null || s == null || s.Gone || r.MarkDay < 0 || w == null)
            {
                return string.Empty;
            }
            string car = r.Car.Length > 0 ? Fill(w.T(CarKey), "car", w.Car(r.Car)) + " " : string.Empty;
            string found = Habit(r.Form, w);
            if (r.Known >= SeenRules.KnowsFrom)
            {
                SeenSpot next = SeenRules.Next(f, r, s, day, hour, out int at);
                if (next != SeenSpot.Nowhere)
                {
                    found += " " + Fill(w.T(TodayKey), "spot", Spot(next, w), "hour", at.ToString(CultureInfo.InvariantCulture));
                }
            }
            return car + found;
        }

        /// <summary>The keys of every line a sighting can have, for the checks of the texts.</summary>
        public static void Keys(List<string> into)
        {
            into.Clear();
            for (int s = 0; s < SeenCatalogue.PlaceLines.Length; s++)
            {
                for (int f = 0; f < SeenCatalogue.PlaceLines[s].Length; f++)
                {
                    if (SeenCatalogue.PlaceLines[s][f].Length > 0)
                    {
                        into.Add(SeenCatalogue.PlaceLines[s][f]);
                    }
                }
            }
            into.Add(SeenCatalogue.NewCarLine);
            into.Add(SeenCatalogue.CarDownLine);
            into.Add(SeenCatalogue.PlayerCarLine);
            into.Add(SeenCatalogue.PlayerPlaceLine);
        }
    }
}
