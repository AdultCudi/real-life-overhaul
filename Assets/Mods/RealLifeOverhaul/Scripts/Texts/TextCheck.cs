using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

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
        Language,    // letters of another script while the card asks for English (data v6: another script or language than the card's)
        Repeat,      // the same, or nearly the same, as one of the last few things this person said
        Machine,     // the speaker calls itself an AI, a bot, an assistant or a language model
        Capital,     // data v6: a sentence that starts with a small letter, in a language that has capitals
        Article,     // data v6: "a" before a vowel or "an" before a consonant, in English
        Name,        // data v6: the player's name from somebody the card says does not know it
        Cut          // data v6: the reply ran to its most tokens and was cut off
    }

    /// <summary>
    /// The checker every model's words pass before anything is shown; the written line is used when they fail (see
    /// <see cref="TextVerdict"/>). Data v6's replies are judged in the card's language by <see cref="JudgeSix"/>; the
    /// player's own typed words are made fit to show by <see cref="Typed"/>. Game-free.
    /// COST: one pass over a line of 200 characters.
    /// </summary>
    public static partial class TextCheck
    {
        public const int NearShare = 75;     // per cent of words shared: nearly the same
        public const int NearFrom = 4;       // ...counted only for lines of this many words or more

        private static readonly HashSet<string> WordsA = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> WordsB = new HashSet<string>(StringComparer.Ordinal);
        private static readonly StringBuilder Word = new StringBuilder(32);

        /// <summary>The words with the stray marks before the first word taken off (the model sometimes starts with ":" or "-"), trimmed.</summary>
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

        /// <summary>The same for a reply of data v6 in a language, keeping the seven languages' opening marks.</summary>
        public static string Tidy(string raw, string lang)
        {
            if (lang == null || raw == null)
            {
                return Tidy(raw);
            }
            int start = 0;
            while (start < raw.Length && !Starts(raw[start]) && !Opens(raw[start]))
            {
                start++;
            }
            return raw.Substring(start).Trim();
        }

        /// <summary>An opening mark a line of data v6 may start with, or a sentence's first letter come after.</summary>
        public static bool Opens(char c)
        {
            return c == '¿' || c == '«' || c == '»' || c == '„' || c == '‚' || c == '「' || c == '『' || c == '（';
        }

        /// <summary>
        /// What the player typed, fit to show and put on a card: one line, single spaces, the prompt's turn markers out, the
        /// angle brackets of markup made harmless (‹ ›), cut at <paramref name="most"/> characters without splitting a
        /// character in two. Null when nothing is left.
        /// </summary>
        public static string Typed(string text, int most)
        {
            string plain = TextIntents.Plain(text);
            if (string.IsNullOrEmpty(plain) || most <= 0)
            {
                return null;
            }
            var typed = new StringBuilder(Math.Min(plain.Length, most));
            bool space = false;
            for (int i = 0; i < plain.Length && typed.Length < most; i++)
            {
                char c = plain[i];
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    space = typed.Length > 0;
                    continue;
                }
                if (space)
                {
                    if (typed.Length >= most - 1)
                    {
                        break;   // a space at the very end would be trimmed off anyway
                    }
                    typed.Append(' ');
                    space = false;
                }
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= plain.Length || typed.Length >= most - 1)
                    {
                        break;   // both halves, or neither
                    }
                    typed.Append(c).Append(plain[++i]);
                    continue;
                }
                typed.Append(c == '<' ? '‹' : c == '>' ? '›' : c);
            }
            return typed.Length > 0 ? typed.ToString() : null;
        }

        private static bool Starts(char c)
        {
            return char.IsLetterOrDigit(c) || c == '$' || c == '"' || c == '\'' || c == '“' || c == '‘' || char.IsSurrogate(c);
        }

        /// <summary>The verdict on tidied words for this card; <paramref name="recent"/> is what the speaker said lately, or null.</summary>
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
            if (Machine(words, cardText))
            {
                return TextVerdict.Machine;
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

        // The speaker naming itself a machine ("I'm an AI", "as a bot"), first person only and not followed by a person's
        // trade ("an AI researcher", "an assistant at the clinic").
        private static readonly Regex SelfMachine = new Regex(
            @"(?:\b(?:i\s*'?\s*m|i\s+am)\s+(?:(?:just|only|merely|simply|really|actually|literally|basically)\s+)?(?:an?\s+)?|\bas\s+an?\s+)(?:large\s+)?"
            + @"(?:a\.?i\.?|artificial\s+intelligence|ai\s+assistant|ai\s+model|ai\s+language\s+model|bot|chat\s*bot|robot\s+assistant|"
            + @"virtual\s+assistant|digital\s+assistant|assistant|language\s+model|llm)"
            + @"(?![a-z])(?!\s+(?:researcher|research|lab|labs|company|companies|startup|start\s*up|engineer|engineers|developer|developers|expert|experts|fan|fans|enthusiast|"
            + @"investor|investors|firm|firms|sector|stock|stocks|share|shares|job|jobs|team|teams|course|class|degree|student|ethicist|manager|to|at|for|in|on)\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // The words that only a machine would use of itself, unless the card is about them.
        private static readonly Regex MachineWords = new Regex(@"\b(?:chat\s*bots?|language\s+models?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>True if the speaker calls itself a machine, or says "chatbot" or "language model" while the card does not.</summary>
        public static bool Machine(string words, string cardText)
        {
            if (string.IsNullOrEmpty(words))
            {
                return false;
            }
            string plain = words.IndexOf('\u2019') >= 0 ? words.Replace('\u2019', '\'') : words;
            if (SelfMachine.IsMatch(plain))
            {
                return true;
            }
            return MachineWords.IsMatch(plain) && (cardText == null || !MachineWords.IsMatch(cardText));
        }

        /// <summary>A guest's check of the host's words on arrival, without the host's card; an unknown intent is refused.</summary>
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
            if (SelfMachine.IsMatch(words.IndexOf('\u2019') >= 0 ? words.Replace('\u2019', '\'') : words))
            {
                return TextVerdict.Machine;   // without the card, any machine word of the speaker's is refused
            }
            return OtherScript(words) ? TextVerdict.Language : TextVerdict.Ok;
        }

        // Every run of digits, with the commas and points inside it, appears on the card.
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
                int start = i > 0 && words[i - 1] == '$' ? i - 1 : i;   // money with its sign must be on the card with it
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

        // The token on the card as a whole number: "40" is not found in "400" nor "$40" in "$4,000".
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

        // A letter outside the Latin script. Emoji are not letters.
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

        /// <summary>True for the same words once case and marks are set aside, or most of them shared.</summary>
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
