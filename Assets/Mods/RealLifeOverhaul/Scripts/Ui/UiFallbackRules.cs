using System;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What draws a character the interface's face lacks, and what that asks of the kit, game-free. Overpass holds Latin
    /// and Cyrillic letters. Every other character (Hangul, Chinese, kana, Greek, a symbol) is drawn by one of the
    /// game's own fonts, TextMeshPro's general fallbacks, and TextMeshPro lays a line out by the font of each of its
    /// characters: the game's Noto fonts stand 1.448 em from ascender to descender against the face's 1.266, and a text
    /// that may be cut is ended at its first character when a line stands taller than its box, so a whole language
    /// would draw as nothing. Three rules keep that from every text. The fallbacks the faces are given are the mod's own
    /// copies of the game's fonts with their line cut to the face's (<see cref="Cut"/>), in an order by language
    /// (<see cref="Rank"/>). A text's box has room no line needs for itself (<see cref="UiTextRules.Room"/>). And a
    /// character is measured as the font that draws it draws it (<see cref="AdvanceEm"/>). The check's one line a
    /// language is <see cref="Line"/>. Held offline against the census of the game's fonts
    /// (RealLifeOverhaul.Build/Census/fonts-NNNN.json; EconomyCheck, FontFallbackChecks).
    /// Cost: a binary search a character measured that the face lacks; nothing allocated but the check's line.
    /// </summary>
    public static partial class UiFallbackRules
    {
        public const float HangulEm = 0.92f;      // em: a Hangul syllable or jamo, as the game's Korean font advances every one of them
        public const int CopyPoint = 40;          // px: the em of a copy's atlas, sharp up to the interface's largest text at 4K
        public const int CopyPadding = 4;         // px of distance field round a glyph, a tenth of the em as the faces' own
        public const int WidePage = 4096;         // px: a page of a Chinese, Japanese or Korean copy's atlas is this wide...
        public const int WidePageHeight = 2048;   // ...and this tall: a language's characters on one page, so a text draws them in one piece
        public const int SmallPage = 1024;        // px a side: a page of any other copy's atlas, which draws a few letters and signs
        public const int TickBatch = 16;          // characters put into an atlas in one tick of play, where the loading screen did not hide them all
        public const int MostNamed = 20;          // characters the check's line names when some are left without a font

        // The regions a family's name can end in ("Noto Sans KR"): each writes that region's script.
        private static readonly string[] Regions = { "KR", "SC", "TC", "HK", "JP" };

        /// <summary>The region a fallback family writes for, by the last word of its name ("Noto Sans KR": KR); null for a family of Latin, Greek and Cyrillic letters.</summary>
        public static string Region(string family)
        {
            if (family == null || family.Length < 2 || (family.Length > 2 && family[family.Length - 3] != ' '))
            {
                return null;
            }
            for (int i = 0; i < Regions.Length; i++)
            {
                if (string.Compare(family, family.Length - 2, Regions[i], 0, 2, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Regions[i];
                }
            }
            return null;
        }

        /// <summary>
        /// How much a language wants a region's font before the others, 0 the most; -1 for a region it does not ask for.
        /// Korean reads its own font, Chinese the simplified one (the game has no traditional one, and its Japanese font
        /// draws many Chinese characters in Japanese shapes), Japanese its own. Another language asks for none.
        /// </summary>
        public static int Wants(string language, string region)
        {
            if (language == null || region == null)
            {
                return -1;
            }
            switch (language)
            {
                case "ko": return region == "KR" ? 0 : -1;
                case "zh-cn": return region == "SC" ? 0 : -1;
                case "zh-tw": return region == "TC" ? 0 : region == "HK" ? 1 : region == "SC" ? 2 : -1;
                case "ja": return region == "JP" ? 0 : -1;
                default: return -1;
            }
        }

        /// <summary>
        /// Where a fallback stands in a language's chain, lower first: the Western family, whose advances the kit measures
        /// by, so a Greek letter is drawn by the font it was measured in; then the font of the language's own script, so
        /// one font draws a text; then the other regions' in the game's own order; then every other font, in that order.
        /// </summary>
        public static int Rank(string family, string language, int listed)
        {
            if (string.Equals(family, WesternFamily, StringComparison.Ordinal))
            {
                return 0;
            }
            string region = Region(family);
            if (region == null)
            {
                return 3000 + listed;
            }
            int wanted = Wants(language, region);
            return wanted >= 0 ? 1000 + wanted : 2000 + listed;
        }

        /// <summary>A copy's atlas page for a family, px wide: a region's font draws a language's thousands of characters, any other a few.</summary>
        public static int PageWidth(string family) => Region(family) != null ? WidePage : SmallPage;

        /// <summary>A copy's atlas page for a family, px tall.</summary>
        public static int PageHeight(string family) => Region(family) != null ? WidePageHeight : SmallPage;

        /// <summary>The characters a page holds at the most: its room over a glyph's cell, the em with its distance field on both sides.</summary>
        public static int PageHolds(string family)
        {
            int cell = CopyPoint + CopyPadding * 2;
            return (PageWidth(family) / cell) * (PageHeight(family) / cell);
        }

        /// <summary>A fallback's line measure cut to the face's: the face's own figure, at the fallback's point size.</summary>
        public static float Cut(float faceValue, int facePoint, int fallbackPoint)
        {
            return facePoint > 0 ? faceValue * fallbackPoint / facePoint : faceValue;
        }

        /// <summary>
        /// True when TextMeshPro ends a text that may be cut before a line this tall in a box this tall (one unit for
        /// both): a line over its box by more than the engine's own tolerance. The text then draws nothing at all when
        /// the line is its first.
        /// </summary>
        public static bool Ends(float line, float box) => line > box + 0.0001f;

        /// <summary>
        /// A character the face lacks, in em, as the game's fallback that draws it advances it: the Western font by its
        /// own table, a Hangul letter by the Korean font's one advance, the two long dashes by theirs, and anything else
        /// as a square, which no character of the game's Chinese, Japanese and Korean fonts is wider than.
        /// </summary>
        public static float AdvanceEm(char c)
        {
            int western = WesternAdvance(c);
            return western >= 0 ? western / (float)WesternUnitsPerEm : SquareEm(c);
        }

        /// <summary>A character's advance in the Western fallback's own units, or -1 when that font does not draw it.</summary>
        public static int WesternAdvance(char c)
        {
            int lo = 0, hi = WesternCodes.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int code = WesternCodes[mid];
                if (code == c)
                {
                    return WesternAdvances[mid];
                }
                if (code < c)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return -1;
        }

        /// <summary>How many characters the Western fallback draws, for the checks that walk its table.</summary>
        public static int WesternCount => WesternCodes.Length;

        /// <summary>The Western fallback's own units in an em, which its advances are in.</summary>
        public static int WesternUnits => WesternUnitsPerEm;

        /// <summary>The Western fallback's character at an index of its table.</summary>
        public static char WesternCodeAt(int index) => (char)WesternCodes[index];

        /// <summary>A character drawn by one of the game's Chinese, Japanese and Korean fonts, in em.</summary>
        public static float SquareEm(char c)
        {
            if (Hangul(c))
            {
                return HangulEm;
            }
            return c == '⸺' ? 2f : c == '⸻' ? 3f : 1f;   // the two-em and three-em dashes are the only characters of those fonts over a square
        }

        /// <summary>True for a Hangul syllable or jamo, whole or half width.</summary>
        public static bool Hangul(char c)
        {
            return (c >= 'ᄀ' && c <= 'ᇿ') || (c >= '㄰' && c <= '㆏') || (c >= 'ꥠ' && c <= '꥿')
                   || (c >= '가' && c <= '힣') || (c >= 'ힰ' && c <= '퟿') || (c >= 'ﾠ' && c <= 'ￜ');
        }

        /// <summary>
        /// Marks every character of a locale file's texts: the values of its one object, their escapes read, never its
        /// keys. Line breaks, tabs and the halves of a character beyond the basic plane are no glyph and are left out.
        /// <paramref name="seen"/> has a place for every character (65,536). Cost: one pass over the file.
        /// </summary>
        public static void Mark(string json, bool[] seen)
        {
            if (json == null || seen == null || seen.Length < 65536)
            {
                return;
            }
            bool inText = false, value = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (!inText)
                {
                    if (c == '"')
                    {
                        inText = true;
                    }
                    else if (c == ':')
                    {
                        value = true;
                    }
                    else if (c == ',' || c == '{' || c == '}')
                    {
                        value = false;
                    }
                    continue;
                }
                if (c == '"')
                {
                    inText = false;
                    value = false;   // a key is followed by its colon, a value by a comma or the end
                    continue;
                }
                if (c == '\\' && i + 1 < json.Length)
                {
                    char e = json[++i];
                    switch (e)
                    {
                        case 'n':
                        case 'r':
                        case 't':
                        case 'b':
                        case 'f':
                            continue;
                        case 'u':
                            if (i + 4 < json.Length && Hex(json, i + 1, out int code))
                            {
                                i += 4;
                                c = (char)code;
                                break;
                            }
                            continue;
                        default:
                            c = e;
                            break;
                    }
                }
                if (value && c >= ' ' && !char.IsSurrogate(c))
                {
                    seen[c] = true;
                }
            }
        }

        private static bool Hex(string text, int at, out int code)
        {
            code = 0;
            for (int i = 0; i < 4; i++)
            {
                char h = text[at + i];
                int d = h >= '0' && h <= '9' ? h - '0' : h >= 'a' && h <= 'f' ? h - 'a' + 10 : h >= 'A' && h <= 'F' ? h - 'A' + 10 : -1;
                if (d < 0)
                {
                    return false;
                }
                code = code * 16 + d;
            }
            return true;
        }

        /// <summary>The marked characters the face does not draw, in order, and how many were marked in all. Allocates the answer.</summary>
        public static char[] Outside(bool[] seen, out int marked)
        {
            marked = 0;
            UiFace face = UiFontMetrics.Face(UiFamily.Overpass, 400);   // every weight of the face draws the same characters
            int outside = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                char[] into = pass == 1 ? new char[outside] : null;
                int at = 0;
                for (int c = 0; seen != null && c < seen.Length && c < 65536; c++)
                {
                    if (!seen[c])
                    {
                        continue;
                    }
                    if (pass == 0)
                    {
                        marked++;
                    }
                    if (face.Advance((char)c) >= 0)
                    {
                        continue;
                    }
                    if (pass == 0)
                    {
                        outside++;
                    }
                    else
                    {
                        into[at++] = (char)c;
                    }
                }
                if (pass == 1)
                {
                    return into;
                }
            }
            return new char[0];
        }

        /// <summary>
        /// The check's one line for a language: how many distinct characters the mod's texts hold, how many the face
        /// lacks, how many of those the fallbacks draw (<paramref name="names"/>: each font with its count) and how many
        /// are left without a font (<paramref name="firstLeft"/> names the first of them). A pass is none left, and the
        /// line of them drawn through the kit's own text drawn whole (<paramref name="given"/> characters given,
        /// <paramref name="drawn"/> drawn; none given where the face draws the whole language).
        /// </summary>
        public static string Line(string language, int characters, int missing, int covered, string names, int left, string firstLeft, int given, int drawn, string more)
        {
            bool whole = drawn >= given;
            string verdict = left == 0 && whole ? "PASS"
                : left > 0 ? "FAIL (the first without a font: " + (firstLeft ?? "?") + ": the game's font stands in for this language)"
                : "FAIL (a line of " + given + " of them that may be cut drew " + drawn + " through the kit's own text)";
            return "Fonts check (" + (language ?? "?") + "): " + characters + " of the language's distinct characters in the mod's texts, " + missing + " missing from Overpass, "
                   + covered + " covered by fallbacks (" + (string.IsNullOrEmpty(names) ? "none" : names) + "), " + left + " left: " + verdict
                   + (string.IsNullOrEmpty(more) ? "." : ". " + more);
        }

        /// <summary>Up to <see cref="MostNamed"/> characters as the line names them: each with its code point, so a log that cannot show the letter still says which it is.</summary>
        public static string Named(char[] characters, int count)
        {
            if (characters == null || count <= 0)
            {
                return null;
            }
            var said = new StringBuilder(count < MostNamed ? count * 10 : MostNamed * 10);
            for (int i = 0; i < count && i < characters.Length && i < MostNamed; i++)
            {
                said.Append(i > 0 ? ", " : string.Empty).Append(characters[i]).Append(" U+").Append(((int)characters[i]).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
            }
            return said.ToString();
        }

        /// <summary>True when the kit writes a whole language with the game's font: characters of its texts are left without one in the mod's own chain.</summary>
        public static bool StandsIn(int left) => left > 0;
    }
}
