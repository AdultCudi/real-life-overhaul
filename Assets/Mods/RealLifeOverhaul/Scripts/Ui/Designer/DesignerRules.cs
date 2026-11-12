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
        /// What a requirement's count says: "6" for a count the business has met, "0 of 1" for one still short (in the words the language gives
        /// <paramref name="pair"/>), null for a requirement with no count (a kind of thing, not a number of them).
        /// </summary>
        public static string CountWords(bool counted, bool met, int have, int need, string pair)
        {
            if (!counted || need <= 0)
            {
                return null;
            }
            have = Math.Max(0, have);
            return met ? have.ToString(CultureInfo.InvariantCulture) : Pair(pair, have, need);
        }

        /// <summary>The figure at the head of the requirements: how many are met of how many.</summary>
        public static string MetWords(int met, int of, string pair) => Pair(pair, met, of);

        /// <summary>Two numbers in the language's own words for "this of that", from its text with {have} and {need} in it.</summary>
        public static string Pair(string pair, int have, int need) =>
            (pair ?? "{have}/{need}").Replace("{have}", have.ToString(CultureInfo.InvariantCulture)).Replace("{need}", need.ToString(CultureInfo.InvariantCulture));

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
        /// What changing one piece of a floor or a wall to a material comes to, as the game's own tool works it out (InteriorElementTool): the charge for the new material
        /// and, apart from it, the refund of the one it had. Nothing at all if it is that material already. Going back to what it was when the designer opened takes back
        /// what the piece cost this time and charges nothing; otherwise the new price is charged and the material a change made this time put there is paid back. The
        /// material the room began with is never paid back: the game refunds only what this edit laid.
        /// </summary>
        public static float PieceMoney(string newId, int newColour, string startId, int startColour, string nowId, int nowColour, float nowPrice, float newPrice, out float refund)
        {
            refund = 0f;
            if (nowId == newId && nowColour == newColour)
            {
                return 0f;
            }
            bool home = startId == newId && startColour == newColour;
            if (home || nowId != startId || nowColour != startColour)
            {
                refund = nowPrice;
            }
            return home ? 0f : newPrice;
        }

        /// <summary>The price of changing one piece, the charge less the refund: what the game's tool adds to the edit.</summary>
        public static float PieceCost(string newId, int newColour, string startId, int startColour, string nowId, int nowColour, float nowPrice, float newPrice)
        {
            float charge = PieceMoney(newId, newColour, startId, startColour, nowId, nowColour, nowPrice, newPrice, out float refund);
            return charge - refund;
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

        // ------------------------------------------------------------------ what a piece of furniture is for the room

        /// <summary>Where a piece goes besides the floor.</summary>
        public enum Place : byte { None = 0, Wall, Ceiling }

        /// <summary>Where a piece hangs, by the item's own placement flags; a piece that snaps to the ceiling is a ceiling piece even if the wall flag is set too.</summary>
        public static Place PlaceOf(bool wallMounted, bool snapToCeiling) => snapToCeiling ? Place.Ceiling : wallMounted ? Place.Wall : Place.None;

        public const float DefaultCell = 0.5f;   // metres: the item's own placement step when the data holds none
        public const string SizeShort = "{w} x {d}";   // a size on a tile, where the unit would not fit in every language; the detail says it in words

        /// <summary>
        /// A piece's footprint in the game's grid cells, as its grid filler counts them (GridFiller.TryPlaceItem): each side rounded to the centimetre, divided by the
        /// cell and rounded up. False for a piece with no footprint, which has no size to show.
        /// </summary>
        public static bool Tiles(float width, float depth, float cell, out int wide, out int deep)
        {
            wide = deep = 0;
            if (!(width > 0.005f) || !(depth > 0.005f))
            {
                return false;
            }
            float step = cell > 0f ? cell : DefaultCell;
            wide = Math.Max(1, (int)Math.Ceiling((float)Math.Round(width, 2) / step));
            deep = Math.Max(1, (int)Math.Ceiling((float)Math.Round(depth, 2) / step));
            return true;
        }

        /// <summary>The floor extent of a piece's colliders, merged as the game merges them (MovingServiceHelper.MergeColliders): the box all of them lie in.</summary>
        public struct Extent
        {
            public float MinX, MaxX, MinZ, MaxZ;
            public int Count;

            /// <summary>One collider: its size and its centre, on the ground's two axes.</summary>
            public void Add(float sizeX, float sizeZ, float centreX, float centreZ)
            {
                float x0 = centreX - sizeX * 0.5f, x1 = centreX + sizeX * 0.5f, z0 = centreZ - sizeZ * 0.5f, z1 = centreZ + sizeZ * 0.5f;
                if (Count == 0)
                {
                    MinX = x0;
                    MaxX = x1;
                    MinZ = z0;
                    MaxZ = z1;
                }
                else
                {
                    MinX = Math.Min(MinX, x0);
                    MaxX = Math.Max(MaxX, x1);
                    MinZ = Math.Min(MinZ, z0);
                    MaxZ = Math.Max(MaxZ, z1);
                }
                Count++;
            }

            public float Width => Count > 0 ? MaxX - MinX : 0f;
            public float Depth => Count > 0 ? MaxZ - MinZ : 0f;
        }

        /// <summary>
        /// A piece's facts for its tile, in the player's words: where it goes ("ceiling", "wall"), what it holds ("holds 24") joined in one line, and its size in
        /// cells ("2 x 1 tiles"). A fact a piece does not have is null.
        /// </summary>
        public static void FactWords(Place place, int holds, int wide, int deep, string holdsPattern, string ceilingWord, string wallWord, string sizePattern, out string kind, out string size)
        {
            string where = place == Place.Ceiling ? ceilingWord : place == Place.Wall ? wallWord : null;
            string holding = holds > 0 ? (holdsPattern ?? "holds {n}").Replace("{n}", holds.ToString(CultureInfo.InvariantCulture)) : null;
            kind = where != null && holding != null ? where + ", " + holding : where ?? holding;
            size = wide > 0 && deep > 0
                ? (sizePattern ?? "{w} x {d}").Replace("{w}", wide.ToString(CultureInfo.InvariantCulture)).Replace("{d}", deep.ToString(CultureInfo.InvariantCulture))
                : null;
        }

        // ------------------------------------------------------------------ the cost lines

        /// <summary>The kinds of change that cost or pay: a piece of furniture bought, a floor or wall laid, a thing sold back, the floor a new one laid over paid back.</summary>
        public enum Cause { Furniture = 0, Floor, Wall, Sold, FloorBack, Count }

        /// <summary>Money by cause for this edit: what each costs (positive) or pays back (negative), and how many things it was.</summary>
        public sealed class Tally
        {
            public readonly float[] Money = new float[(int)Cause.Count];
            public readonly int[] Count = new int[(int)Cause.Count];
            public float Area;       // square metres of floor laid
            public float AreaBack;   // square metres of floor paid back

            public void Clear()
            {
                for (int i = 0; i < Money.Length; i++)
                {
                    Money[i] = 0f;
                    Count[i] = 0;
                }
                Area = 0f;
                AreaBack = 0f;
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
