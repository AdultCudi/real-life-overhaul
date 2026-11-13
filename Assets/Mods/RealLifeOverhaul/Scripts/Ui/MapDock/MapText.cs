using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's own words as the kit draws them. The game writes its labels for TextMeshPro, which reads a few tags in them (the open state of a place is "Open" and
    /// "Closing in 7 hours (17:00)" with a line break tag between), and the kit draws plain text: the tags are taken out and a break becomes a line break
    /// (<see cref="Plain"/>) or, where the kit has one line to give (a chip, a row), a middle dot (<see cref="OneLine"/>). Game-free. Cost: a pass over a short
    /// string when a place is picked or the list is read, never a frame.
    /// </summary>
    public static class MapText
    {
        public const string Join = " · ";

        private static readonly HashSet<string> Tags = new HashSet<string>
        {
            "b", "i", "u", "s", "br", "nobr", "color", "size", "sprite", "sup", "sub", "font", "mark", "align", "line-height", "link", "pos", "space", "cspace", "mspace", "page",
            "indent", "margin", "width", "voffset", "rotate", "gradient", "alpha", "lowercase", "uppercase", "allcaps", "smallcaps", "style", "material", "noparse", "line-indent"
        };

        /// <summary>The words with their text tags taken out; a break tag and a line feed are one line feed, and nothing is left at the ends. Null for null.</summary>
        public static string Plain(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }
            if (raw.IndexOf('<') < 0 && raw.IndexOf('\r') < 0)
            {
                return raw.Trim();
            }
            var b = new StringBuilder(raw.Length);
            int i = 0;
            while (i < raw.Length)
            {
                char c = raw[i];
                if (c == '<' && TagAt(raw, i, out int end, out bool lineBreak))
                {
                    if (lineBreak)
                    {
                        b.Append('\n');
                    }
                    i = end + 1;
                    continue;
                }
                if (c != '\r')
                {
                    b.Append(c);
                }
                i++;
            }
            return b.ToString().Trim();
        }

        /// <summary>The words on one line: the breaks of <see cref="Plain"/> as a middle dot, empty lines left out.</summary>
        public static string OneLine(string raw) => Lines(Plain(raw), Join);

        /// <summary>
        /// Whether words have the shape of a text key ("bizman_send_offer_button") and not of words: one run of letters,
        /// digits, underscores and colons with an underscore in it. The game leaves a label as its key until the label first shows.
        /// </summary>
        public static bool KeyShaped(string words)
        {
            if (string.IsNullOrEmpty(words) || words.Length < 3)
            {
                return false;
            }
            bool under = false;
            for (int i = 0; i < words.Length; i++)
            {
                char c = words[i];
                under |= c == '_';
                if (!(c == '_' || c == ':' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                {
                    return false;
                }
            }
            return under && words[0] != '_' && words[words.Length - 1] != '_';
        }

        /// <summary>
        /// The taxi button's words as the game writes them on its own button (BuildingResume.SetClickLabels): "Travel here" over the minutes and the fare in brackets,
        /// on the game's two lines, each kept whole; <paramref name="none"/> when the game wrote none. A button draws the lines one over the other.
        /// </summary>
        public static string TaxiLabel(string raw, string none)
        {
            string words = Plain(raw);
            return string.IsNullOrWhiteSpace(words) ? none : Lines(words, "\n");
        }

        // A plain text's lines, each trimmed, the empty ones left out, joined by a mark: the text itself when it has one line.
        private static string Lines(string plain, string join)
        {
            if (string.IsNullOrEmpty(plain) || plain.IndexOf('\n') < 0)
            {
                return plain;
            }
            string[] lines = plain.Split('\n');
            var b = new StringBuilder(plain.Length + 4);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (b.Length > 0)
                {
                    b.Append(join);
                }
                b.Append(line);
            }
            return b.ToString();
        }

        // Whether a tag of TextMeshPro's stands at i: its end, and whether it is a break. A "<" in a price or a comparison is not one.
        private static bool TagAt(string s, int i, out int end, out bool lineBreak)
        {
            end = -1;
            lineBreak = false;
            int close = s.IndexOf('>', i + 1);
            if (close < 0 || close - i > 80)
            {
                return false;
            }
            int from = i + 1;
            if (from < close && s[from] == '/')
            {
                from++;
            }
            int at = from;
            while (at < close && (char.IsLetter(s[at]) || s[at] == '-'))
            {
                at++;
            }
            string name = s.Substring(from, at - from).ToLowerInvariant();
            bool colourCode = from < close && s[from] == '#';
            if (!colourCode && !Tags.Contains(name))
            {
                return false;
            }
            if (at < close && !colourCode && s[at] != '=' && s[at] != ' ' && s[at] != '/')
            {
                return false;
            }
            end = close;
            lineBreak = name == "br";
            return true;
        }
    }
}
