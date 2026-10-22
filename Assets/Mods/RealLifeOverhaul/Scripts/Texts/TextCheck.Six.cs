using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul.Texts
{
    /// <summary>The checker of data v6, in the card's language, which has the last word over the grammar. Game-free. COST: a pass over a line.</summary>
    public static partial class TextCheck
    {
        public const int StrangeLeast = 2;     // words of another language a line must have, with none of its own, to be refused as written in it
        public const int StrangeFrom = 4;      // ...counted only on lines of this many words or more

        // Common little words of each Latin-script language, the language's code first.
        private static readonly string[][] Little =
        {
            new[] { "en", "the", "and", "you", "is", "are", "that", "this", "with", "for", "was", "have", "what", "your", "but", "just", "it", "of", "to", "i", "my",
                "we", "they", "be", "will", "can", "not", "so", "there", "about", "it's", "i'm", "don't" },
            new[] { "pt", "não", "você", "é", "uma", "com", "isso", "eu", "está", "tá", "obrigado", "obrigada", "muito", "também", "sim", "aqui", "já", "seu", "sua",
                "meu", "minha", "foi", "tem", "vai", "bem", "ele", "ela", "nós", "hoje", "agora", "então", "quando", "onde", "mas" },
            new[] { "de", "der", "die", "das", "und", "ist", "nicht", "ich", "sie", "ein", "eine", "mit", "auf", "für", "zu", "wir", "aber", "auch", "noch", "danke",
                "sehr", "dich", "mich", "schon", "heute", "hier", "bin", "bist", "mein", "dein", "gut", "ja", "nein", "jetzt", "wie", "wo" },
            new[] { "fr", "le", "les", "et", "est", "je", "tu", "vous", "une", "pas", "pour", "avec", "ce", "c'est", "mais", "merci", "très", "oui", "ça", "j'ai", "nous",
                "sur", "au", "aux", "du", "des", "il", "elle", "mon", "ma", "ton", "ta", "suis", "qui", "dans", "aussi", "maintenant" },
            new[] { "es", "el", "los", "las", "y", "es", "yo", "tú", "una", "del", "pero", "gracias", "muy", "sí", "está", "también", "aquí", "hoy", "qué", "lo",
                "ahora", "cuando", "donde", "tengo", "tiene", "eso", "esto", "mi", "su", "nos", "más", "hay", "ya", "soy" }
        };

        private static readonly Dictionary<string, HashSet<string>> LittleOf = LittleSets();
        private static readonly List<string> LineWords = new List<string>(48);
        private static readonly System.Text.StringBuilder Piece = new System.Text.StringBuilder(32);

        // The speaker naming itself a machine in each language but English (SelfMachine), in SelfMachineLangs' order.
        private static readonly Regex[] SelfMachineIn =
        {
            new Regex(@"\b(?:(?:eu\s+)?sou|como)\s+(?:uma?\s+)?(?:i\.?a\.?|intelig[eê]ncia\s+artificial|rob[oô]|bot|chatbot|assistente(?:\s+virtual)?|modelo\s+de\s+linguagem)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"\b(?:(?:yo\s+)?soy|como)\s+(?:una?\s+)?(?:i\.?a\.?|inteligencia\s+artificial|robot|bot|chatbot|asistente(?:\s+virtual)?|modelo\s+de\s+lenguaje)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"\b(?:je\s+suis|suis|en\s+tant\s+qu['’]?)\s*(?:une?\s+)?(?:i\.?a\.?|intelligence\s+artificielle|robot|bot|chatbot|assistante?(?:\s+virtuel(?:le)?)?|mod[eè]le\s+de\s+langage)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"\b(?:ich\s+bin|als)\s+(?:eine?n?\s+)?(?:k\.?i\.?|künstliche\s+intelligenz|roboter|bot|chatbot|assistent(?:in)?|sprachmodell)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"(?:^|[^\p{L}])(?:я|как)\s+(?:[—-]\s+)?(?:ии|искусственный\s+интеллект|бот|чат-?бот|робот|ассистент|помощник|языковая\s+модель|нейросеть)(?![\p{L}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"我(?:只)?是(?:一个|一款|个)?(?:人工智能|AI|ai|机器人|聊天机器人|语言模型|助手|AI助手|虚拟助手)", RegexOptions.CultureInvariant)
        };

        private static readonly string[] SelfMachineLangs = { "pt", "es", "fr", "de", "ru", "zh-cn" };

        private static Dictionary<string, HashSet<string>> LittleSets()
        {
            var sets = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            for (int i = 0; i < Little.Length; i++)
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                for (int k = 1; k < Little[i].Length; k++)
                {
                    set.Add(Little[i][k]);
                }
                sets[Little[i][0]] = set;
            }
            return sets;
        }

        /// <summary>The verdict on tidied words of data v6; <paramref name="untold"/> is the player's name when the speaker does not know it.</summary>
        public static TextVerdict JudgeSix(string words, TextIntent intent, TextSheet card, string cardText, IReadOnlyList<string> recent, string lang, bool cut,
            string untold, bool lowercase, int filterStrength = 1)
        {
            if (string.IsNullOrEmpty(words))
            {
                return TextVerdict.Empty;
            }
            if (cut)
            {
                return TextVerdict.Cut;
            }
            if (Breaks(words))
            {
                return TextVerdict.Lines;
            }
            if (TextLanguages.Units(words) > TextLanguages.MostUnits(intent.MaxLen, lang))
            {
                return TextVerdict.Long;
            }
            for (int i = 0; i < card.Must.Count; i++)
            {
                string needed = TextSlotKinds.Needed(card.Must[i], i < card.MustKinds.Count ? card.MustKinds[i] : TextSlotKind.Text, lang);
                if (needed != null && words.IndexOf(needed, StringComparison.OrdinalIgnoreCase) < 0)
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
            if (words.IndexOf('!') >= 0 || words.IndexOf('！') >= 0 || words.IndexOf('¡') >= 0)
            {
                return TextVerdict.Exclaim;
            }
            if (Flare.FlareFilter.Clean(words, filterStrength) != words)
            {
                return TextVerdict.Banned;
            }
            if (!InLanguage(words, lang))
            {
                return TextVerdict.Language;
            }
            if (!lowercase && TextLanguages.Cased(lang) && !Capitals(words))
            {
                return TextVerdict.Capital;
            }
            if (lang == TextLanguages.English && !Articles(words))
            {
                return TextVerdict.Article;
            }
            if (Names(words, untold))
            {
                return TextVerdict.Name;
            }
            if (MachineIn(words, cardText, lang))
            {
                return TextVerdict.Machine;
            }
            if (recent != null)
            {
                for (int i = 0; i < recent.Count; i++)
                {
                    if (Near(words, recent[i], lang))
                    {
                        return TextVerdict.Repeat;
                    }
                }
            }
            return TextVerdict.Ok;
        }

        /// <summary>A guest's check of the host's words of data v6, without the card; an unknown intent is refused.</summary>
        public static TextVerdict JudgeSharedSix(string words, TextIntent intent, string lang, int filterStrength = 1)
        {
            if (intent == null || string.IsNullOrEmpty(words) || Tidy(words, lang) != words)
            {
                return TextVerdict.Empty;
            }
            if (Breaks(words))
            {
                return TextVerdict.Lines;
            }
            if (TextLanguages.Units(words) > TextLanguages.MostUnits(intent.MaxLen, lang))
            {
                return TextVerdict.Long;
            }
            if (HasDash(words))
            {
                return TextVerdict.Dash;
            }
            if (words.IndexOf('!') >= 0 || words.IndexOf('！') >= 0 || words.IndexOf('¡') >= 0)
            {
                return TextVerdict.Exclaim;
            }
            if (Flare.FlareFilter.Clean(words, filterStrength) != words)
            {
                return TextVerdict.Banned;
            }
            if (!InLanguage(words, lang))
            {
                return TextVerdict.Language;
            }
            return MachineIn(words, null, lang) ? TextVerdict.Machine : TextVerdict.Ok;
        }

        // A line break of any kind, Unicode's own included.
        private static bool Breaks(string words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                char c = words[i];
                if (c == '\n' || c == '\r' || c == '\u0085' || c == (char)0x2028 || c == (char)0x2029 || c == '\v' || c == '\f')
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether a line is in its language: by its script, and for a Latin script by its little words.</summary>
        public static bool InLanguage(string words, string lang)
        {
            TextScript script = TextLanguages.Script(lang);
            for (int i = 0; i < words.Length; i++)
            {
                if (!TextLanguages.OwnLetter(words[i], script))
                {
                    return false;
                }
            }
            if (script != TextScript.Latin)
            {
                return TextLanguages.HasOwnScript(words, script);
            }
            LittleWords(words);
            if (LineWords.Count < StrangeFrom || !LittleOf.TryGetValue(lang, out HashSet<string> own))
            {
                return true;
            }
            int mine = 0;
            for (int i = 0; i < LineWords.Count; i++)
            {
                mine += own.Contains(LineWords[i]) ? 1 : 0;
            }
            if (mine > 0)
            {
                return true;
            }
            foreach (KeyValuePair<string, HashSet<string>> other in LittleOf)
            {
                if (other.Key == lang)
                {
                    continue;
                }
                int theirs = 0;
                for (int i = 0; i < LineWords.Count; i++)
                {
                    theirs += other.Value.Contains(LineWords[i]) ? 1 : 0;
                }
                if (theirs >= StrangeLeast)
                {
                    return false;
                }
            }
            return true;
        }

        // The line's words in lower case into LineWords.
        private static void LittleWords(string words)
        {
            LineWords.Clear();
            Piece.Length = 0;
            for (int i = 0; i <= words.Length; i++)
            {
                char c = i < words.Length ? words[i] : ' ';
                if (char.IsLetter(c) || ((c == '\'' || c == '’') && Piece.Length > 0))
                {
                    Piece.Append(c == '’' ? '\'' : char.ToLowerInvariant(c));
                }
                else if (Piece.Length > 0)
                {
                    LineWords.Add(Piece.ToString().TrimEnd('\''));
                    Piece.Length = 0;
                }
            }
        }

        /// <summary>True when every sentence starts with a capital, opening quotes and marks passed over.</summary>
        public static bool Capitals(string words)
        {
            bool start = true;
            for (int i = 0; i < words.Length; i++)
            {
                char c = words[i];
                if (start)
                {
                    if (char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '“' || c == '‘' || c == '(' || c == '[' || c == '¡' || Opens(c))
                    {
                        continue;
                    }
                    if (char.IsLetter(c) && char.IsLower(c))
                    {
                        return false;
                    }
                    start = false;
                }
                if ((c == '.' || c == '?' || c == '!' || c == '…') && i + 1 < words.Length && words[i + 1] == ' ')
                {
                    start = true;
                }
            }
            return true;
        }

        /// <summary>True when English's "a" and "an" fit the word after them, by its sound for the words known ("a euro", "an hour").</summary>
        public static bool Articles(string words)
        {
            int start = 0;
            string prev = null;
            for (int i = 0; i <= words.Length; i++)
            {
                if (i < words.Length && words[i] != ' ')
                {
                    continue;
                }
                string token = words.Substring(start, i - start).Trim('"', '\'', '“', '”', '‘', '’', '(', ')', ',', '.', '?', ':', ';');
                start = i + 1;
                if (token.Length == 0)
                {
                    continue;
                }
                if (prev != null && !ArticleFits(prev, token))
                {
                    return false;
                }
                prev = token == "a" || token == "an" || token == "A" || token == "An" ? token : null;
            }
            return true;
        }

        private static bool ArticleFits(string article, string word)
        {
            char first = word[0];
            if (!char.IsLetter(first) || (word.Length >= 2 && char.IsUpper(word[0]) && char.IsUpper(word[1])))
            {
                return true;   // a figure, a symbol, a short form in capitals: let be
            }
            string w = word.ToLowerInvariant();
            bool vowel = w[0] == 'a' || w[0] == 'e' || w[0] == 'i' || w[0] == 'o';
            if (article == "a" || article == "A")
            {
                return !vowel || w.StartsWith("one", StringComparison.Ordinal) || w.StartsWith("once", StringComparison.Ordinal) || w.StartsWith("eu", StringComparison.Ordinal)
                       || w.StartsWith("ewe", StringComparison.Ordinal);
            }
            return vowel || w[0] == 'u' || w.StartsWith("hour", StringComparison.Ordinal) || w.StartsWith("honest", StringComparison.Ordinal)
                   || w.StartsWith("honour", StringComparison.Ordinal) || w.StartsWith("honor", StringComparison.Ordinal) || w.StartsWith("heir", StringComparison.Ordinal);
        }

        /// <summary>True when the words name the player as a whole word, any case; false with no name to keep out.</summary>
        public static bool Names(string words, string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 2 || string.IsNullOrEmpty(words))
            {
                return false;
            }
            int at = words.IndexOf(name, StringComparison.OrdinalIgnoreCase);
            while (at >= 0)
            {
                int end = at + name.Length;
                bool before = at > 0 && char.IsLetterOrDigit(words[at - 1]);
                bool after = end < words.Length && char.IsLetterOrDigit(words[end]);
                if (!before && !after)
                {
                    return true;
                }
                at = words.IndexOf(name, at + 1, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>True if the speaker calls itself a machine in the card's language, or in English, which a slip may write in any.</summary>
        public static bool MachineIn(string words, string cardText, string lang)
        {
            if (Machine(words, cardText))
            {
                return true;
            }
            for (int i = 0; i < SelfMachineLangs.Length; i++)
            {
                if (SelfMachineLangs[i] == lang && SelfMachineIn[i].IsMatch(words.IndexOf('’') >= 0 ? words.Replace('’', '\'') : words))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The same, or nearly, in a language; Chinese, which has no spaces, by its pairs of characters.</summary>
        public static bool Near(string a, string b, string lang)
        {
            if (TextLanguages.Script(lang) != TextScript.Han)
            {
                return Near(a, b);
            }
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }
            int na = Pairs(a, WordsA), nb = Pairs(b, WordsB);
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
            return shared * 100 >= Math.Max(WordsA.Count, WordsB.Count) * NearShare;
        }

        // A Chinese line's pairs of characters, and its Latin words whole, into a set.
        private static int Pairs(string text, HashSet<string> into)
        {
            into.Clear();
            Word.Length = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                char c = i < text.Length ? text[i] : ' ';
                if (TextLanguages.Wide(c) && char.IsLetter(c))
                {
                    if (Word.Length > 0)
                    {
                        into.Add(Word.ToString());
                        Word.Length = 0;
                    }
                    if (i + 1 < text.Length && TextLanguages.Wide(text[i + 1]) && char.IsLetter(text[i + 1]))
                    {
                        into.Add(text.Substring(i, 2));
                    }
                    else if (i == 0 || !TextLanguages.Wide(text[i - 1]) || !char.IsLetter(text[i - 1]))
                    {
                        into.Add(text.Substring(i, 1));   // a character alone
                    }
                    continue;
                }
                if (char.IsLetterOrDigit(c))
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
