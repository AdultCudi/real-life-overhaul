using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>Why a model's words were not shown (Ok: shown).</summary>
    public enum TextVerdict
    {
        Ok,
        Empty,       // nothing left once the stray marks before the first word were taken off
        Lines,       // more than one line
        Long,        // longer than the intent's most
        Must,        // a fact the card says must appear does not
        Number,      // a figure or money that is not on the card
        Dash,        // an en or em dash, or a hyphen used as one
        Exclaim,     // an exclamation mark from a voice that never uses one
        Banned,      // a word on Flare's list
        Language,    // letters of another script while the card asks for English
        Repeat       // the same, or nearly the same, as one of the last few things this person said
    }

    /// <summary>
    /// The checker every model's words pass before anything is shown (the written line is used when they fail):
    ///
    ///   tidied     everything before the first letter, digit, $ or quote taken off (the 2B model through llama.cpp
    ///              sometimes starts with a stray ":", "." or "-"), and the ends trimmed
    ///   one line   no line break
    ///   length     at most the intent's most (200 for a text, 140 to 180 for Flare)
    ///   must       every must fact of the card, case aside
    ///   numbers    no figure that is not on the card (a run of digits, with its commas and points, must appear in the card)
    ///   dashes     no en or em dash and no hyphen standing alone between words
    ///   "!"        only from Mom and the partner (the only voices whose training lines have one)
    ///   words      none of Flare's list (Social/Flare/FlareFilter, the same list other players' text goes through)
    ///   English    no letters of another script while the card says lang: en
    ///   repeats    not the same, or nearly the same, as the last few things the same person said (words compared:
    ///              three quarters of them shared, of four or more, counts as nearly)
    ///
    /// Game-free (EconomyCheck, TextsChecks.cs: each rule made to fail). COST: one pass over a line of 200 characters.
    /// </summary>
    public static class TextCheck
    {
        public const int NearShare = 75;     // per cent of words shared: nearly the same
        public const int NearFrom = 4;       // ...counted only for lines of this many words or more

        private static readonly HashSet<string> WordsA = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> WordsB = new HashSet<string>(StringComparer.Ordinal);
        private static readonly StringBuilder Word = new StringBuilder(32);

        /// <summary>The words with the stray marks before the first word taken off and the ends trimmed.</summary>
        public static string Tidy(string raw)
        {
            if (raw == null)
            {
                return string.Empty;
            }
            int start = 0;
            while (start < raw.Length && !Starts(raw[start]))
            {
                start++;
            }
            return raw.Substring(start).Trim();
        }

        private static bool Starts(char c)
        {
            return char.IsLetterOrDigit(c) || c == '$' || c == '"' || c == '\'' || c == '“' || c == '‘' || char.IsSurrogate(c);
        }

        /// <summary>The verdict on tidied words for this card. <paramref name="recent"/>: the last few things the same speaker said (may be null).</summary>
        public static TextVerdict Judge(string words, TextIntent intent, TextSheet card, string cardText, IReadOnlyList<string> recent, int filterStrength = 1)
        {
            if (string.IsNullOrEmpty(words))
            {
                return TextVerdict.Empty;
            }
            if (words.IndexOf('\n') >= 0 || words.IndexOf('\r') >= 0)
            {
                return TextVerdict.Lines;
            }
            if (words.Length > intent.MaxLen)
            {
                return TextVerdict.Long;
            }
            for (int i = 0; i < card.Must.Count; i++)
            {
                if (words.IndexOf(card.Must[i], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return TextVerdict.Must;
                }
            }
            if (!NumbersOnCard(words, cardText))
            {
                return TextVerdict.Number;
            }
            if (HasDash(words))
            {
                return TextVerdict.Dash;
            }
            if (!intent.Exclaims && words.IndexOf('!') >= 0)
            {
                return TextVerdict.Exclaim;
            }
            if (Flare.FlareFilter.Clean(words, filterStrength) != words)
            {
                return TextVerdict.Banned;
            }
            if ((card.Lang == null || card.Lang == "en") && OtherScript(words))
            {
                return TextVerdict.Language;
            }
            if (recent != null)
            {
                for (int i = 0; i < recent.Count; i++)
                {
                    if (Near(words, recent[i]))
                    {
                        return TextVerdict.Repeat;
                    }
                }
            }
            return TextVerdict.Ok;
        }

        /// <summary>
        /// A guest's check of the host's words on arrival (the card is the host's): tidy, one line, the intent's length, no
        /// dash, "!" only for the voices that use it, none of Flare's list, no other script. Unknown intent: refused.
        /// </summary>
        public static TextVerdict JudgeShared(string words, TextIntent intent, int filterStrength = 1)
        {
            if (intent == null || string.IsNullOrEmpty(words) || Tidy(words) != words)
            {
                return TextVerdict.Empty;
            }
            if (words.IndexOf('\n') >= 0 || words.IndexOf('\r') >= 0)
            {
                return TextVerdict.Lines;
            }
            if (words.Length > intent.MaxLen)
            {
                return TextVerdict.Long;
            }
            if (HasDash(words))
            {
                return TextVerdict.Dash;
            }
            if (!intent.Exclaims && words.IndexOf('!') >= 0)
            {
                return TextVerdict.Exclaim;
            }
            if (Flare.FlareFilter.Clean(words, filterStrength) != words)
            {
                return TextVerdict.Banned;
            }
            return OtherScript(words) ? TextVerdict.Language : TextVerdict.Ok;
        }

        // Every run of digits (with the commas and points inside it) appears on the card.
        private static bool NumbersOnCard(string words, string card)
        {
            int i = 0;
            while (i < words.Length)
            {
                if (!char.IsDigit(words[i]))
                {
                    i++;
                    continue;
                }
                int start = i > 0 && words[i - 1] == '$' ? i - 1 : i;   // (money with its sign: "$40" must be on the card as "$40")
                while (i < words.Length && (char.IsDigit(words[i]) || (Joins(words[i]) && i + 1 < words.Length && char.IsDigit(words[i + 1]))))
                {
                    i++;
                }
                if (card == null || !Whole(card, words.Substring(start, i - start)))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Joins(char c) => c == ',' || c == '.' || c == ':';

        // The token on the card as a whole number: no digit (or a digit's comma or point) right before or after it, so that
        // "40" is not found in "400" nor "$40" in "$4,000".
        private static bool Whole(string card, string token)
        {
            int at = card.IndexOf(token, StringComparison.Ordinal);
            while (at >= 0)
            {
                int end = at + token.Length;
                bool before = at > 0 && token[0] != '$' && (char.IsDigit(card[at - 1]) || (Joins(card[at - 1]) && at > 1 && char.IsDigit(card[at - 2])));
                bool after = end < card.Length && (char.IsDigit(card[end]) || (Joins(card[end]) && end + 1 < card.Length && char.IsDigit(card[end + 1])));
                if (!before && !after)
                {
                    return true;
                }
                at = card.IndexOf(token, at + 1, StringComparison.Ordinal);
            }
            return false;
        }

        private static bool HasDash(string words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                char c = words[i];
                if (c == '–' || c == '—' || c == '‒' || c == '―' || c == '‐' || c == '‑' || c == '−')
                {
                    return true;   // en, em, figure and other dashes, the Unicode hyphens and the minus sign
                }
                if (c == '-' && i + 1 < words.Length && words[i + 1] == '-')
                {
                    return true;   // two hyphens: a dash typed
                }
                if (c == '-' && (i == 0 || words[i - 1] == ' ' || i == words.Length - 1 || words[i + 1] == ' '))
                {
                    return true;   // a hyphen standing alone: used as a dash
                }
            }
            return false;
        }

        // A letter outside the Latin script (Cyrillic, Greek, Chinese, Arabic and the rest). Emoji are not letters.
        private static bool OtherScript(string words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                char c = words[i];
                if (c > 'ɏ' && char.IsLetter(c))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The same, or nearly: the same words once case and marks are set aside, or most of them shared.</summary>
        public static bool Near(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }
            int na = Split(a, WordsA), nb = Split(b, WordsB);
            if (na == 0 || nb == 0)
            {
                return false;
            }
            if (WordsA.SetEquals(WordsB))
            {
                return true;
            }
            if (WordsA.Count < NearFrom || WordsB.Count < NearFrom)
            {
                return false;
            }
            int shared = 0;
            foreach (string w in WordsA)
            {
                if (WordsB.Contains(w))
                {
                    shared++;
                }
            }
            int most = Math.Max(WordsA.Count, WordsB.Count);
            return shared * 100 >= most * NearShare;
        }

        private static int Split(string text, HashSet<string> into)
        {
            into.Clear();
            Word.Length = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                char c = i < text.Length ? text[i] : ' ';
                if (char.IsLetterOrDigit(c) || c == '\'')
                {
                    Word.Append(char.ToLowerInvariant(c));
                }
                else if (Word.Length > 0)
                {
                    into.Add(Word.ToString());
                    Word.Length = 0;
                }
            }
            return into.Count;
        }
    }
}
