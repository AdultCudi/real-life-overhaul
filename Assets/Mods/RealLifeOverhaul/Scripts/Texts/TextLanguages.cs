using System;

namespace RealLifeOverhaul.Texts
{
    /// <summary>The letters a language writes in, for the checks that depend on it.</summary>
    public enum TextScript
    {
        Latin,      // English, Portuguese, German, French, Spanish: capitals at a sentence's start
        Cyrillic,   // Russian: capitals at a sentence's start
        Han         // Simplified Chinese: no capitals; a character is as wide as two Latin letters
    }

    /// <summary>
    /// The seven languages RLO Texts writes from data v6: a card's lang code, its script, a line's length in units (a wide
    /// character counts two) and a reply's tokens. Game-free. COST: a pass over a line, or a few comparisons.
    /// </summary>
    public static class TextLanguages
    {
        public const string English = "en";
        public const int LongerShare = 120;    // per cent of English's most for the languages that run about a fifth longer
        public const int TokenRoom = 16;       // tokens past the most units' own, for the end of a sentence and a name of several tokens

        /// <summary>The seven, in the mod's order (RealLifeOverhaul.Build/Shared/ModLanguages).</summary>
        public static readonly string[] All = { "en", "pt", "zh-cn", "de", "fr", "es", "ru" };

        /// <summary>The mod's other languages, which no pack writes, so a game in one keeps every written line.</summary>
        public static readonly string[] Unwritten = { "nl", "tr" };

        /// <summary>The language a card is written in for the game's: a known one as itself, else English, as the mod reads it.</summary>
        public static string Of(string locale)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return English;
            }
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(locale, All[i], StringComparison.OrdinalIgnoreCase))
                {
                    return All[i];
                }
            }
            for (int i = 0; i < Unwritten.Length; i++)
            {
                if (string.Equals(locale, Unwritten[i], StringComparison.OrdinalIgnoreCase))
                {
                    return Unwritten[i];
                }
            }
            return English;
        }

        // Each of the seven as its natives write it, and how they address the player: the teacher's brief of data v6 word for
        // word (prompts6.py), so an AI service writes a language as the packs learnt it.
        private static readonly string[] Natives =
        {
            "British English", "Brazilian Portuguese", "Simplified Chinese (Mandarin as spoken in mainland China)", "German", "French (France)", "Spanish (Spain)", "Russian"
        };

        private static readonly string[] Address =
        {
            "",
            "Use você.",
            "Use 你; 您 only for a formal or polite older speaker.",
            "Use Sie with a stranger; du only for a cheeky or playful young speaker, or between friends.",
            "Use vous with a stranger; tu only for a cheeky or playful young speaker, or between friends.",
            "Use tú with most people in the street; usted only for a formal or older speaker talking to a stranger.",
            "Use вы with a stranger; ты only between friends."
        };

        /// <summary>The language of one of the seven codes as its native speakers write it ("Brazilian Portuguese"); null for any other.</summary>
        public static string Native(string code)
        {
            int at = Array.IndexOf(All, code);
            return at >= 0 ? Natives[at] : null;
        }

        /// <summary>How a speaker of one of the seven addresses the player ("Use você."); empty for English, null for any other code.</summary>
        public static string AddressOf(string code)
        {
            int at = Array.IndexOf(All, code);
            return at >= 0 ? Address[at] : null;
        }

        /// <summary>True for one of the seven codes, written exactly as in <see cref="All"/>.</summary>
        public static bool Known(string code)
        {
            return Array.IndexOf(All, code) >= 0;
        }

        public static TextScript Script(string code)
        {
            switch (code)
            {
                case "zh-cn": return TextScript.Han;
                case "ru": return TextScript.Cyrillic;
                default: return TextScript.Latin;
            }
        }

        /// <summary>True where a sentence starts with a capital letter: every language but Chinese.</summary>
        public static bool Cased(string code) => Script(code) != TextScript.Han;

        /// <summary>The languages a pack's "languages" line names, in the mod's order; English alone with no line or none known.</summary>
        public static string[] Parse(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return new[] { English };
            }
            string[] parts = line.Split(',');
            int count = 0;
            var found = new bool[All.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                int at = Array.IndexOf(All, Of(parts[i].Trim()));
                if (at >= 0 && !found[at] && string.Equals(parts[i].Trim(), All[at], StringComparison.OrdinalIgnoreCase))
                {
                    found[at] = true;
                    count++;
                }
            }
            if (count == 0)
            {
                return new[] { English };
            }
            var codes = new string[count];
            for (int i = 0, k = 0; i < All.Length; i++)
            {
                if (found[i])
                {
                    codes[k++] = All[i];
                }
            }
            return codes;
        }

        public static bool Has(string[] codes, string code)
        {
            return codes != null && code != null && Array.IndexOf(codes, code) >= 0;
        }

        /// <summary>A character as wide as two Latin letters: the Chinese, Japanese and Korean characters and the full-width forms.</summary>
        public static bool Wide(char c)
        {
            return (c >= '\u1100' && c <= '\u115F') || (c >= '\u2E80' && c <= '\u303F') || (c >= '\u3040' && c <= '\u33FF') || (c >= '\u3400' && c <= '\u4DBF')
                   || (c >= '\u4E00' && c <= '\u9FFF') || (c >= '\uA960' && c <= '\uA97F') || (c >= '\uAC00' && c <= '\uD7A3') || (c >= '\uF900' && c <= '\uFAFF')
                   || (c >= '\uFE30' && c <= '\uFE4F') || (c >= '\uFF00' && c <= '\uFF60') || (c >= '\uFFE0' && c <= '\uFFE6');
        }

        /// <summary>A line's length as it is read: one unit a character, two a wide one, two for a pair of surrogates.</summary>
        public static int Units(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            int units = 0;
            for (int i = 0; i < text.Length; i++)
            {
                units += Wide(text[i]) ? 2 : 1;
            }
            return units;
        }

        /// <summary>The most units a line of an intent may have in a language, from the intent's most in English.</summary>
        public static int MostUnits(int english, string code)
        {
            switch (Script(code))
            {
                case TextScript.Han: return english;
                default: return code == English ? english : english * LongerShare / 100;
            }
        }

        /// <summary>The tokens a reply of at most so many units may take in a language: the model's n_predict.</summary>
        public static int Tokens(int units, string code)
        {
            switch (code)
            {
                case English: return units / 3 + TokenRoom;
                case "ru": return units * 10 / 22 + TokenRoom;
                case "zh-cn": return units / 2 + TokenRoom;
                default: return units * 10 / 26 + TokenRoom;
            }
        }

        /// <summary>A letter of this language's script, or of Latin, which names keep in every language (a business, a street).</summary>
        public static bool OwnLetter(char c, TextScript script)
        {
            if (!char.IsLetter(c) || c <= '\u024F' || (c >= '\u1E00' && c <= '\u1EFF'))
            {
                return true;   // not a letter, or a Latin one: names are Latin in every language's line
            }
            switch (script)
            {
                case TextScript.Cyrillic: return c >= '\u0400' && c <= '\u052F';
                case TextScript.Han: return (c >= '\u3400' && c <= '\u4DBF') || (c >= '\u4E00' && c <= '\u9FFF') || (c >= '\uF900' && c <= '\uFAFF') || c == '\u3007';
                default: return false;
            }
        }

        /// <summary>True when a line has a letter of the script itself: a Russian line Cyrillic, a Chinese one a Chinese character.</summary>
        public static bool HasOwnScript(string words, TextScript script)
        {
            if (script == TextScript.Latin || string.IsNullOrEmpty(words))
            {
                return script == TextScript.Latin;
            }
            for (int i = 0; i < words.Length; i++)
            {
                char c = words[i];
                if (c > '\u024F' && char.IsLetter(c) && OwnLetter(c, script))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
