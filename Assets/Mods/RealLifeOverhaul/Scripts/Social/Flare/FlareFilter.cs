using System;
using System.Text;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The mod's own word list for what another player of a Going Public session typed, used when Steam's own text filter
    /// is not there (FlareGame.SteamFilter) and, at Strict, after it. A word on the list is shown as its first letter and
    /// stars. Letters written as figures or signs ("sh1t", "$hit") and doubled letters count as the letters. At Standard
    /// only whole words are matched; at Strict the longer words are found inside other words too, and any web address is
    /// starred. The list is kept in ROT13 so that the source reads clean. Game-free (EconomyCheck, FlareLiveChecks.cs).
    ///
    /// COST: one pass over a text of at most 300 characters, when a post or a message of another player's comes in; never
    /// per frame. What the player types is never filtered: it is shown to them as typed.
    /// </summary>
    public static class FlareFilter
    {
        public const int Standard = 0, Strict = 1;

        private static readonly string[] Rot =
        {
            "shpx", "fuvg", "phag", "ovgpu", "onfgneq", "nffubyr", "nefrubyr", "qvpx", "pbpx", "chffl", "juber", "fyhg", "avttre", "avttn",
            "snttbg", "snt", "ergneq", "gjng", "jnaxre", "fcnfgvp", "xvxr", "puvax", "fcvp", "genaal", "qlxr", "zbgureshpxre", "obyybpxf",
            "cevpx", "cbeen", "pnenyub", "chgn", "zreqn", "ivnqb", "ohprgn"
        };

        private const int InsideFrom = 5;   // at Strict, a word this long or longer is found inside other words too

        private static readonly string[] Words = Unrot();

        private static string[] Unrot()
        {
            var words = new string[Rot.Length];
            for (int i = 0; i < Rot.Length; i++)
            {
                var b = new StringBuilder(Rot[i].Length);
                foreach (char c in Rot[i])
                {
                    b.Append(c >= 'a' && c <= 'z' ? (char)('a' + (c - 'a' + 13) % 26) : c);
                }
                words[i] = b.ToString();
            }
            return words;
        }

        /// <summary>How many words the list has (for the checks).</summary>
        public static int Count => Words.Length;

        /// <summary>A text with the list's words starred; the same text when there is none. Null stays null.</summary>
        public static string Clean(string text, int strength)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            // The letters as they read, and for each character of the text the letter of them it stands at: a space or a
            // sign between words is a space; a dot, a dash, a star or an underscore inside a word is nothing ("s.h.i.t");
            // a doubled letter stands at the letter before it ("shiiit").
            var plain = new StringBuilder(text.Length);
            var at = new int[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '*' || c == '.' || c == '-' || c == '_')
                {
                    at[i] = plain.Length - 1;
                    continue;
                }
                char l = Letter(c);
                if (l == '\0')
                {
                    l = ' ';
                }
                if (plain.Length > 0 && plain[plain.Length - 1] == l)
                {
                    at[i] = plain.Length - 1;
                    continue;
                }
                plain.Append(l);
                at[i] = plain.Length - 1;
            }
            char[] shown = text.ToCharArray();
            bool any = false;
            string p = plain.ToString();
            for (int w = 0; w < Words.Length; w++)
            {
                string word = Collapse(Words[w]);
                int from = 0;
                while ((from = p.IndexOf(word, from, StringComparison.Ordinal)) >= 0)
                {
                    int end = from + word.Length;
                    bool whole = (from == 0 || p[from - 1] == ' ') && (end == p.Length || p[end] == ' ');
                    bool inside = strength >= Strict && word.Length >= InsideFrom;
                    if (whole || inside)
                    {
                        Star(shown, at, from, end);
                        any = true;
                    }
                    from = end;
                }
            }
            if (strength >= Strict)
            {
                any |= Addresses(shown);
            }
            return any ? new string(shown) : text;
        }

        // Every character that stands in plain[from..end) starred but the first.
        private static void Star(char[] shown, int[] at, int from, int end)
        {
            bool first = true;
            for (int i = 0; i < shown.Length; i++)
            {
                if (at[i] < from || at[i] >= end || char.IsWhiteSpace(shown[i]))
                {
                    continue;
                }
                if (first)
                {
                    first = false;
                    continue;
                }
                shown[i] = '*';
            }
        }

        // A web address (a word with "://", "www." or a dot before two to four letters at its end): starred whole.
        private static bool Addresses(char[] shown)
        {
            bool any = false;
            int start = 0;
            for (int i = 0; i <= shown.Length; i++)
            {
                if (i == shown.Length || char.IsWhiteSpace(shown[i]))
                {
                    if (i > start && IsAddress(new string(shown, start, i - start)))
                    {
                        for (int k = start; k < i; k++)
                        {
                            shown[k] = '*';
                        }
                        any = true;
                    }
                    start = i + 1;
                }
            }
            return any;
        }

        private static bool IsAddress(string word)
        {
            string w = word.ToLowerInvariant().TrimEnd('.', ',', '!', '?', ')');
            if (w.IndexOf("://", StringComparison.Ordinal) >= 0 || w.StartsWith("www.", StringComparison.Ordinal))
            {
                return true;
            }
            int dot = w.LastIndexOf('.');
            if (dot <= 0 || w.Length - dot - 1 < 2 || w.Length - dot - 1 > 4)
            {
                return false;
            }
            for (int i = dot + 1; i < w.Length; i++)
            {
                if (w[i] < 'a' || w[i] > 'z')
                {
                    return false;
                }
            }
            return char.IsLetter(w[dot - 1]);
        }

        // A character as the letter it reads as, lower case; '\0' for one that is no letter (a separator).
        private static char Letter(char c)
        {
            switch (c)
            {
                case '0': return 'o';
                case '1': return 'i';
                case '3': return 'e';
                case '4': case '@': return 'a';
                case '5': case '$': return 's';
                case '7': case '+': return 't';
                case '8': return 'b';
            }
            if (c == '*' || c == '.' || c == '-' || c == '_')
            {
                return '\0';
            }
            char l = char.ToLowerInvariant(c);
            return char.IsLetter(l) ? l : '\0';
        }

        private static string Collapse(string word)
        {
            var b = new StringBuilder(word.Length);
            for (int i = 0; i < word.Length; i++)
            {
                if (b.Length == 0 || b[b.Length - 1] != word[i])
                {
                    b.Append(word[i]);
                }
            }
            return b.ToString();
        }
    }
}
