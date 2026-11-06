using System;
using System.Globalization;
using System.IO;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the interior designer's face works out from plain numbers and words, so EconomyCheck can run it: the search's own test, what a requirement's
    /// count says, the customers an hour and the score against where they began, what a piece of floor costs the game's way, the time of day's slider,
    /// and whether a blueprint's name may be saved. The game's own figures are read in DesignerGame; none is worked out again here. Game-free.
    /// </summary>
    public static class DesignerRules
    {
        public const int ClockMin = 0, ClockMax = 1440;   // minutes: a day

        // ------------------------------------------------------------------ the search

        /// <summary>
        /// The game's own test of a furniture search (FurnitureItemSearch): the item's name in lower case with its spaces taken out holds the typed words,
        /// lower case with their spaces taken out. An empty search holds everything.
        /// </summary>
        public static bool Matches(string name, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            string have = name.ToLowerInvariant().Replace(" ", string.Empty), want = query.ToLowerInvariant().Replace(" ", string.Empty);
            return have.IndexOf(want, StringComparison.Ordinal) >= 0;
        }

        /// <summary>True when the typed words ask for a tag (they start with a hash), as the game's search takes them.</summary>
        public static bool IsTagSearch(string query) => !string.IsNullOrEmpty(query) && query.TrimStart().StartsWith("#", StringComparison.Ordinal);

        // ------------------------------------------------------------------ requirements

        /// <summary>
        /// What a requirement's count says: "6" for a count the business has met, "0 of 1" for one still short, null for a requirement with no count (a
        /// kind of thing, not a number of them).
        /// </summary>
        public static string CountWords(bool counted, bool met, int have, int need)
        {
            if (!counted || need <= 0)
            {
                return null;
            }
            have = Math.Max(0, have);
            return met ? have.ToString(CultureInfo.InvariantCulture) : have.ToString(CultureInfo.InvariantCulture) + " of " + need.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The figure at the head of the requirements: how many are met of how many.</summary>
        public static string MetWords(int met, int of) => met.ToString(CultureInfo.InvariantCulture) + " of " + of.ToString(CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ the readings

        /// <summary>A meter's fill, 0 to 100: a share of its limit, a figure with no limit (9999 in the game) full.</summary>
        public static int Fill(int now, int limit)
        {
            if (limit <= 0 || limit >= 9999)
            {
                return now > 0 ? 100 : 0;
            }
            return Math.Max(0, Math.Min(100, (int)Math.Round(now * 100.0 / limit)));
        }

        /// <summary>A change against the opening, signed: "+8", "-3", or null for none.</summary>
        public static string Change(int now, int then, string unit)
        {
            int d = now - then;
            return d == 0 ? null : (d > 0 ? "+" : "-") + Math.Abs(d).ToString(CultureInfo.InvariantCulture) + (unit ?? string.Empty);
        }

        // ------------------------------------------------------------------ what a piece of floor costs

        /// <summary>
        /// The price of changing one piece of a floor or a wall to a material, as the game's own tool works it out (InteriorElementTool): nothing if it
        /// is that material already; going back to what it was when the designer opened takes back what the piece cost this time; otherwise the new
        /// price, less what a change made this time paid for the material it now has. The material the room began with is never paid back.
        /// </summary>
        public static float PieceCost(string newId, int newColour, string startId, int startColour, string nowId, int nowColour, float nowPrice, float newPrice)
        {
            bool same = nowId == newId && nowColour == newColour;
            if (same)
            {
                return 0f;
            }
            if (startId == newId && startColour == newColour)
            {
                return -nowPrice;
            }
            float cost = 0f;
            if (nowId != startId || nowColour != startColour)
            {
                cost -= nowPrice;
            }
            return cost + newPrice;
        }

        // ------------------------------------------------------------------ the time of day

        /// <summary>A slider's share, 0 to 1, for a value between its ends.</summary>
        public static float Share(float value, float min, float max) => max <= min ? 0f : Math.Max(0f, Math.Min(1f, (value - min) / (max - min)));

        /// <summary>The value a share of the slider stands for, in whole tens of minutes as the game's own steps are, kept between its ends.</summary>
        public static float Value(float share, float min, float max)
        {
            float v = min + Math.Max(0f, Math.Min(1f, share)) * (max - min);
            v = (float)Math.Round(v / 10f) * 10f;
            return Math.Max(min, Math.Min(max, v));
        }

        /// <summary>The value one ten-minute step on from a value, kept between the slider's ends, as the game's own plus and minus buttons step it.</summary>
        public static float Step(float value, int minutes, float min, float max)
        {
            float v = (float)Math.Round((value + minutes) / 10f) * 10f;
            return Math.Max(min, Math.Min(max, v));
        }

        /// <summary>A minute of the day as a clock reads it: "14:30", the day's end "24:00".</summary>
        public static string Clock(float minutes)
        {
            int m = Math.Max(0, (int)Math.Round(minutes));
            return (m / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (m % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>The hours marked under the slider: every third hour that stands between its ends, as minutes from midnight.</summary>
        public static int Marks(float min, float max, int[] into)
        {
            int n = 0;
            for (int h = (int)Math.Ceiling(min / 60f); h * 60 <= max + 0.5f && n < into.Length; h++)
            {
                if (h % 3 == 0)
                {
                    into[n++] = h * 60;
                }
            }
            return n;
        }

        // ------------------------------------------------------------------ the blueprint's name

        /// <summary>The problem with a blueprint's name, as the game refuses it: 0 none, 1 empty, 2 a character a file name cannot hold or a dot.</summary>
        public static int NameProblem(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 1;
            }
            char[] bad = Path.GetInvalidFileNameChars();
            for (int i = 0; i < name.Length; i++)
            {
                if (name[i] == '.' || Array.IndexOf(bad, name[i]) >= 0)
                {
                    return 2;
                }
            }
            return 0;
        }

        // ------------------------------------------------------------------ a business's initials

        /// <summary>A business's initials for its tile: the first letters of its first two words, in capitals, or its first two letters.</summary>
        public static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }
            string[] words = name.Split(new[] { ' ', '-', '&' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
            {
                return (char.ToUpperInvariant(words[0][0]).ToString() + char.ToUpperInvariant(words[1][0])).Trim();
            }
            string one = words.Length == 1 ? words[0] : name.Trim();
            return one.Length >= 2 ? one.Substring(0, 2).ToUpperInvariant() : one.ToUpperInvariant();
        }

        // ------------------------------------------------------------------ the cost lines

        /// <summary>The kinds of change that cost or pay: a piece of furniture bought, a floor or wall laid, a thing sold back.</summary>
        public enum Cause { Furniture = 0, Floor, Wall, Sold, Count }

        /// <summary>Money by cause for this edit: what each costs (positive) or pays back (negative), and how many things it was.</summary>
        public sealed class Tally
        {
            public readonly float[] Money = new float[(int)Cause.Count];
            public readonly int[] Count = new int[(int)Cause.Count];
            public float Area;   // square metres of floor laid

            public void Clear()
            {
                for (int i = 0; i < Money.Length; i++)
                {
                    Money[i] = 0f;
                    Count[i] = 0;
                }
                Area = 0f;
            }

            /// <summary>One change of a cause: its price (a refund negative) and how many things it was.</summary>
            public void Add(Cause cause, float price, int things)
            {
                Money[(int)cause] += price;
                Count[(int)cause] += things;
            }

            /// <summary>What this edit comes to on apply, as a positive amount of money spent (negative where it pays).</summary>
            public float Total()
            {
                float t = 0f;
                for (int i = 0; i < Money.Length; i++)
                {
                    t += Money[i];
                }
                return t;
            }
        }
    }
}
