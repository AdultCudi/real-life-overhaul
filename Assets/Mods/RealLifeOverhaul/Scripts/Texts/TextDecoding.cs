using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// How every request of data v6 is made so that a reply cannot break the rules, rather than only be refused after
    /// (TextCheck still has the last word): llama.cpp's server is sent, with the prompt,
    ///
    ///   a grammar   one of three GBNF grammars (<see cref="Grammar"/>, by the card's language and the speaker's style,
    ///               <see cref="GrammarName"/>) that allows no line break or control character, no "!" nor its full-width
    ///               form, no dash and no hyphen but one inside a word, nothing before the first word, and, in a language
    ///               with capitals for a speaker who does not write in lower case, a capital (or no letter) at the line's
    ///               start and after every ". ", "? " or "… "
    ///   the length  n_predict from the intent's most units in the language (TextLanguages.Tokens): a reply cannot run on,
    ///               and one cut at the limit is refused (TextVerdict.Cut)
    ///   the name    when the card says the speaker does not know the player's name, the first token of the name as it is
    ///               written in a line (" Sam", and "Sam" at the start) banned through logit_bias; the tokens come from the
    ///               server's own /tokenize and are kept per name for as long as that server runs (TextServer)
    ///
    /// The exact bodies are in the contract (SPEC_v6.json: decoding). Game-free (EconomyCheck, TextsDecodingChecks.cs).
    /// COST: a string chosen and a few appended a request.
    /// </summary>
    public static class TextDecoding
    {
        public const string Cased = "cased";       // a capital at every sentence's start: English, Portuguese, German, French, Spanish, Russian
        public const string Free = "free";         // ...the same without the capitals, for a speaker who writes in lower case
        public const string Uncased = "uncased";   // Chinese: no capitals, and the full-width digits kept out (a figure is written as the card writes it)

        /// <summary>What no reply of data v6 may hold, in the contract's words: "!", its full-width form and Spanish's opening one; the grammars keep them out.</summary>
        public static readonly string[] Bans = { "!", "\uFF01", "\u00A1" };

        // What no character of a reply may be: control characters and line breaks of every kind, "!", its full-width and opening forms, the dashes.
        private const string Never = @"\x00-\x1F\x7F\u0085\u00A1\u200B-\u200F\u2028\u2029!\uFF01\u2010-\u2015\u2212";

        /// <summary>The cased grammar: a capital (or no letter) to start each sentence; no line break, "!", dash or loose hyphen.</summary>
        public static readonly string CasedText =
            "root ::= up rest\n"
            + "rest ::= ( plain | hyphen | stop next )* stop?\n"
            + "plain ::= [^" + Never + @"\-.?\u2026]" + "\n"
            + "hyphen ::= \"-\" [^ " + Never + @"\-.?\u2026]" + "\n"
            + @"stop ::= [.?\u2026]+" + "\n"
            + "next ::= [^ " + Never + @"\-.?\u2026] | "" ""+ up" + "\n"
            + @"up ::= [^ a-z\u00DF-\u00F6\u00F8-\u00FF\u0153\u0430-\u045F" + Never + @"\-.?\u2026]" + "\n";

        /// <summary>The free grammar: as the cased one without the capitals.</summary>
        public static readonly string FreeText =
            "root ::= first rest\n"
            + "rest ::= ( plain | hyphen )*\n"
            + "first ::= [^ " + Never + @"\-.?\u2026]" + "\n"
            + "plain ::= [^" + Never + @"\-]" + "\n"
            + "hyphen ::= \"-\" [^ " + Never + @"\-]" + "\n";

        /// <summary>The uncased grammar (Chinese): as the free one, with the full-width digits kept out.</summary>
        public static readonly string UncasedText =
            "root ::= first rest\n"
            + "rest ::= ( plain | hyphen )*\n"
            + "first ::= [^ " + Never + @"\uFF10-\uFF19\-.?\u2026\u3002\uFF1F]" + "\n"
            + "plain ::= [^" + Never + @"\uFF10-\uFF19\-]" + "\n"
            + "hyphen ::= \"-\" [^ " + Never + @"\uFF10-\uFF19\-]" + "\n";

        /// <summary>The names of the grammars, in the contract's order.</summary>
        public static readonly string[] Names = { Cased, Free, Uncased };

        /// <summary>The grammar a request of this language and style takes: uncased for Chinese, free for a speaker who writes in lower case, cased otherwise.</summary>
        public static string GrammarName(string lang, bool lowercase)
        {
            if (!TextLanguages.Cased(lang))
            {
                return Uncased;
            }
            return lowercase ? Free : Cased;
        }

        /// <summary>A grammar's GBNF by its name; null for an unknown one.</summary>
        public static string Grammar(string name)
        {
            switch (name)
            {
                case Cased: return CasedText;
                case Free: return FreeText;
                case Uncased: return UncasedText;
                default: return null;
            }
        }

        /// <summary>True when the speaker's style words say they write in lower case (TextWords.FriendStyle, CitizenStyle).</summary>
        public static bool Lowercase(string style) => style != null && style.IndexOf("lowercase", System.StringComparison.Ordinal) >= 0;

        /// <summary>The n_predict of a request of an intent in a language.</summary>
        public static int Tokens(TextIntent intent, string lang) => TextLanguages.Tokens(TextLanguages.MostUnits(intent.MaxLen, lang), lang);

        /// <summary>
        /// The forms of a name whose first token is banned: " Sam" (in a line) and "Sam" (its start). None for a name too short
        /// to ban without taking a common token with it (two letters or fewer), or the card's stand-in when the game has no
        /// name ("you").
        /// </summary>
        public static string[] NameForms(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 3 || name == "you" || name.IndexOf('<') >= 0)
            {
                return null;
            }
            return new[] { " " + name, name };
        }

        /// <summary>The logit_bias of a request: each token never written, in order, once each ("[[id,false],...]"); empty for none.</summary>
        public static string Bias(List<int> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return string.Empty;
            }
            var b = new StringBuilder(16 + tokens.Count * 12);
            b.Append('[');
            int written = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] < 0 || tokens.IndexOf(tokens[i]) != i)
                {
                    continue;   // (a token twice: once)
                }
                b.Append(written > 0 ? "," : string.Empty).Append('[').Append(tokens[i].ToString(CultureInfo.InvariantCulture)).Append(",false]");
                written++;
            }
            return written > 0 ? b.Append(']').ToString() : string.Empty;
        }
    }
}
