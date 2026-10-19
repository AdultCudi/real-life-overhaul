using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>What a typed answer on a call says, as <see cref="SocialComposerRules.Read"/> reads it.</summary>
    public enum SocialRead
    {
        Unclear,   // nothing clear: no word it knows, only a question, as much good as bad, or "meh"
        Good,      // good news, or going well: "very good", "not bad", "holding up", a smile
        Bad,       // bad news, or going badly: "not great", "could be better", "kinda rough", a frown
        Yes,       // only a yes ("yes", "yeah", "of course"): what it means is the topic's (SocialTopic.YesIs)
        No         // only a no ("nah", "not really", "nope"): what it means is the topic's (SocialTopic.NoIs)
    }

    /// <summary>
    /// A call's composer, the reading: what the player's typed answer says, by fixed rules (as street talk reads its words,
    /// Street/StreetSay, and Flare an invitation, Flare/FlareInviteRules.Read), over the words of every language the mod has
    /// at once (the Words part), so an answer reads the same whichever of them it is typed in. The words are made plain
    /// first (<see cref="Plain"/>): lower case, accents off, apostrophes out ("don't" is "dont", "I'll" is "i"), a smiley or
    /// an emoji as the sign it is, each Chinese character a word of its own, and a comma, full stop, exclamation mark or
    /// line break between two clauses; a word stretched by a letter typed three times or more is read without the stretch
    /// ("goood", "noooo"). Then the longest phrase of the table at each word says what it is: good or bad news; a turn
    /// ("not", "pas", "nicht", 不) that makes the next good or bad word within <see cref="TurnReach"/> the other ("not too
    /// bad" is good), or Turkish "değil" and "yok" the one before it; a strengthener ("very", "kinda"), which changes
    /// nothing and is passed over by a turn; "but", after which every word counts double; not clear ("meh", "not sure");
    /// or only a yes or a no. A clause that asks ("are you ok?") counts for nothing but what it says before a question back
    /// ("fine thanks you?"). More good than bad, and than unclear, is good; more bad is bad; an answer that is only a yes
    /// or only a no is that; anything else is not clear, never bad for want of a word. The game decides what the words
    /// say; RLO Texts only words the answer to them. Game-free (EconomyCheck, TypedReadingChecks.cs).
    /// COST: the table is made once, at the first reading; a reading is a pass over at most <see cref="ReadMost"/>
    /// characters and a few lookups a word, when typed words are sent and when the model's answer to them comes.
    /// </summary>
    public static partial class SocialComposerRules
    {
        public const int ReadMost = 640;      // characters read at most: a typed answer has TypedMost, the model's about as many
        public const int PhraseMost = 4;      // words in the table's longest phrases ("comme ci comme ca", 马马虎虎)
        public const int TurnReach = 3;       // words a turn reaches to the good or bad word it turns: "not doing that well"
        internal const string Break = "|";    // a word of its own between two clauses
        internal const string Asked = "?";    // ...in place of it, the end of a clause that asks
        internal const string GoodSign = "(good)", BadSign = "(bad)", PlainSign = "(plain)";   // a smiley or emoji, as a word

        private static Dictionary<string, Sense> _table;
        private static List<string> _tableFaults;

        /// <summary>What the typed words say; the same words always read the same.</summary>
        public static SocialRead Read(string text)
        {
            List<string> words = Plain(text);
            if (words.Count == 0)
            {
                return SocialRead.Unclear;
            }
            Dictionary<string, Sense> table = Table;
            for (int i = 0; i < words.Count; i++)
            {
                words[i] = Known(words[i], table);
            }
            return Weigh(Senses(words, table));
        }

        /// <summary>The table's phrases that cannot be one (empty, too long, across a clause) or say two things; empty when it is sound.</summary>
        internal static IReadOnlyList<string> TableFaults
        {
            get
            {
                _ = Table;
                return _tableFaults;
            }
        }

        /// <summary>How many phrases the table holds.</summary>
        internal static int TableSize => Table.Count;

        // Every language's phrases, made plain as the words are, at the first reading (the Words part).
        private static Dictionary<string, Sense> Table
        {
            get
            {
                if (_table == null)
                {
                    var faults = new List<string>();
                    _table = Build(faults);
                    _tableFaults = faults;
                }
                return _table;
            }
        }

        /// <summary>
        /// The words made plain for the table, as the type's summary says, with <see cref="Break"/> or <see cref="Asked"/>
        /// between two clauses; the table's own phrases are made plain the same way.
        /// </summary>
        internal static List<string> Plain(string text)
        {
            var words = new List<string>(24);
            if (string.IsNullOrEmpty(text))
            {
                return words;
            }
            var word = new StringBuilder(16);
            int end = Math.Min(text.Length, ReadMost);
            for (int i = 0; i < end; i++)
            {
                char c = text[i];
                string sign = Smiley(text, i, end, out int used);
                if (sign == null && char.IsHighSurrogate(c))
                {
                    used = i + 1 < end && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                    sign = used == 2 ? Emoji(char.ConvertToUtf32(c, text[i + 1])) : null;
                    if (sign == null)
                    {
                        Flush(word, words);
                        i += used - 1;
                        continue;   // (an emoji that is no sign, a skin tone: it only parts two words)
                    }
                }
                if (sign == null)
                {
                    sign = Symbol(c);
                    used = 1;
                }
                if (sign != null)
                {
                    Flush(word, words);
                    words.Add(sign);
                    i += used - 1;
                    continue;
                }
                if (c == '\'' || c == '’' || c == '‘' || c == '`' || c == '´')
                {
                    i += Ending(text, i + 1, end);   // "I'll" is "i", never "ill"; "don't" is "dont", "c'est" is "cest"
                    continue;
                }
                if (Han(c))
                {
                    Flush(word, words);
                    if (c == '吗')
                    {
                        Mark(words, Asked);   // 吗: the clause asks
                    }
                    else
                    {
                        words.Add(c.ToString());
                    }
                    continue;
                }
                if (char.IsLetterOrDigit(c))
                {
                    Fold(word, c);
                    continue;
                }
                Flush(word, words);
                if (c == '?' || c == '？')
                {
                    Mark(words, Asked);
                }
                else if (Stops(c))
                {
                    Mark(words, Break);
                }
            }
            Flush(word, words);
            return words;
        }

        // The letters of an English ending after an apostrophe ("ll", "re", "ve", "d", "m", "s"), which are left out so that
        // "I'll", "he'll" and "we'll" are not "ill", "hell" and "well"; 0 for anything else, which joins the word ("dont").
        private static int Ending(string t, int from, int end)
        {
            int to = from;
            while (to < end && char.IsLetter(t[to]))
            {
                to++;
            }
            int n = to - from;
            if (n == 0 || n > 2)
            {
                return 0;
            }
            char a = char.ToLowerInvariant(t[from]), b = n == 2 ? char.ToLowerInvariant(t[from + 1]) : ' ';
            bool ending = n == 1 ? a == 'd' || a == 'm' || a == 's' : (a == 'l' && b == 'l') || (a == 'r' && b == 'e') || (a == 'v' && b == 'e');
            return ending ? n : 0;
        }

        // A word as the table knows it: itself, or, for a word stretched by a letter typed three times or more, with those
        // letters cut to two ("goood") and then to one ("greaaat", "noooo"). A word of ordinary double letters is only itself,
        // so "seen", "pass" and Dutch "ook" are never "sen", "pas" and "ok".
        private static string Known(string w, Dictionary<string, Sense> table)
        {
            if (table.ContainsKey(w) || !Stretched(w))
            {
                return w;
            }
            string two = Runs(w, 2);
            if (table.ContainsKey(two))
            {
                return two;
            }
            string one = Runs(w, 1);
            return table.ContainsKey(one) ? one : w;
        }

        // True when a letter stands three times or more in a row.
        private static bool Stretched(string w)
        {
            for (int i = 2; i < w.Length; i++)
            {
                if (w[i] == w[i - 1] && w[i] == w[i - 2] && char.IsLetter(w[i]))
                {
                    return true;
                }
            }
            return false;
        }

        // The word with each run of one letter cut to at most so many.
        private static string Runs(string w, int most)
        {
            var cut = new StringBuilder(w.Length);
            int run = 0;
            for (int i = 0; i < w.Length; i++)
            {
                run = i > 0 && w[i] == w[i - 1] && char.IsLetter(w[i]) ? run + 1 : 1;
                if (run <= most)
                {
                    cut.Append(w[i]);
                }
            }
            return cut.ToString();
        }

        // What each phrase says, the longest phrase of the table first at each word, never across two clauses: one sense for a
        // whole phrase, Word for a word the table does not know, Stop and Question for the ends of clauses.
        private static List<Sense> Senses(List<string> words, Dictionary<string, Sense> table)
        {
            var said = new List<Sense>(words.Count);
            var key = new StringBuilder(32);
            int i = 0;
            while (i < words.Count)
            {
                if (words[i] == Break || words[i] == Asked)
                {
                    said.Add(words[i] == Asked ? Sense.Question : Sense.Stop);
                    i++;
                    continue;
                }
                int longest = 1;
                Sense sense = Sense.Word;
                key.Length = 0;
                for (int n = 1; n <= PhraseMost && i + n <= words.Count; n++)
                {
                    string next = words[i + n - 1];
                    if (next == Break || next == Asked)
                    {
                        break;
                    }
                    if (n > 1)
                    {
                        key.Append(' ');
                    }
                    key.Append(next);
                    if (table.TryGetValue(key.ToString(), out Sense found))
                    {
                        longest = n;
                        sense = found;
                    }
                }
                said.Add(sense);
                i += longest;
            }
            return said;
        }

        // What an answer or a clause says: its good, bad and unclear phrases (by weight), its bare yeses and noes.
        private struct Tally
        {
            public float Good, Bad, Plain;
            public int Yes, No;

            public void Add(Tally other)
            {
                Good += other.Good;
                Bad += other.Bad;
                Plain += other.Plain;
                Yes += other.Yes;
                No += other.No;
            }
        }

        // The answer weighed, clause by clause, by the rules in the type's summary.
        private static SocialRead Weigh(List<Sense> said)
        {
            var answer = new Tally();
            var clause = new Tally();
            var before = new Tally();   // what the clause had said before its last question back ("fine thanks you?")
            bool back = false;
            float weight = 1f;          // doubled after each "but"
            int reach = 0;              // words a turn still reaches
            Sense turn = Sense.Word;    // ...and that turn, for what it is if it turns nothing
            int last = -1;              // the clause's last good or bad phrase, for a turn after it ("iyi değil")
            bool lastGood = false;
            float lastWeight = 0f;
            bool opened = false;        // an opening "well" or "bom", good only if nothing else is said
            for (int k = 0; k <= said.Count; k++)
            {
                Sense s = k < said.Count ? said[k] : Sense.Stop;
                if (s == Sense.Stop || s == Sense.Question)
                {
                    Spent(ref reach, turn, weight, ref clause);
                    if (s == Sense.Stop)
                    {
                        answer.Add(clause);
                    }
                    else if (back)
                    {
                        answer.Add(before);   // (a question back: only the question is not counted)
                    }
                    clause = new Tally();
                    before = new Tally();
                    back = false;
                    last = -1;
                    continue;
                }
                if (s == Sense.VeryOrGood)
                {
                    s = k + 1 < said.Count && Strengthened(said[k + 1]) ? Sense.Very : Sense.Good;
                }
                if (s == Sense.Opening)
                {
                    if (k == 0 && SaysMore(said))
                    {
                        opened = true;
                        continue;
                    }
                    s = Sense.Good;
                }
                switch (s)
                {
                    case Sense.Very:
                        continue;   // (passed over: the turn's reach and the last phrase are kept)
                    case Sense.Good:
                    case Sense.Bad:
                        bool isGood = (s == Sense.Good) != (reach > 0);
                        reach = 0;
                        if (isGood)
                        {
                            clause.Good += weight;
                        }
                        else
                        {
                            clause.Bad += weight;
                        }
                        last = k;
                        lastGood = isGood;
                        lastWeight = weight;
                        continue;
                    case Sense.TurnBefore:
                        if (last == k - 1 && last >= 0)
                        {
                            Turned(lastGood, lastWeight, ref clause);
                        }
                        else
                        {
                            clause.No++;
                        }
                        last = -1;
                        continue;
                    case Sense.Turn:
                    case Sense.TurnOrNo:
                    case Sense.TurnFarOrNo:
                    case Sense.TurnOrBad:
                        reach = s == Sense.TurnOrNo ? 1 : TurnReach;   // (a turn after a turn starts again: "ne ... pas")
                        turn = s;
                        last = -1;
                        continue;
                    case Sense.But:
                        Spent(ref reach, turn, weight, ref clause);
                        weight *= 2f;
                        last = -1;
                        continue;
                    case Sense.Back:
                        before = clause;
                        back = true;
                        break;
                    case Sense.Yes:
                        clause.Yes++;
                        break;
                    case Sense.No:
                        clause.No++;
                        break;
                    case Sense.Plain:
                        clause.Plain += weight;
                        break;
                }
                // A word, a question back, a yes, a no or something unclear: one word of a turn's reach spent.
                last = -1;
                if (reach == 1)
                {
                    Spent(ref reach, turn, weight, ref clause);
                }
                else if (reach > 1)
                {
                    reach--;
                }
            }
            if (opened && answer.Good == 0f && answer.Bad == 0f && answer.Plain == 0f && answer.Yes == 0 && answer.No == 0)
            {
                answer.Good = 1f;   // ("well!", "bom!": the word was the whole answer)
            }
            if (answer.Good > answer.Bad && answer.Good > answer.Plain)
            {
                return SocialRead.Good;
            }
            if (answer.Bad > answer.Good && answer.Bad > answer.Plain)
            {
                return SocialRead.Bad;
            }
            if (answer.Good == 0f && answer.Bad == 0f && answer.Plain == 0f)
            {
                if (answer.Yes > 0 && answer.No == 0)
                {
                    return SocialRead.Yes;
                }
                if (answer.No > 0 && answer.Yes == 0)
                {
                    return SocialRead.No;
                }
            }
            return SocialRead.Unclear;
        }

        // A turn's reach over, with nothing turned: a "no" for the turns that are one alone, bad for "not very" and its kind.
        private static void Spent(ref int reach, Sense turn, float weight, ref Tally clause)
        {
            if (reach <= 0)
            {
                return;
            }
            reach = 0;
            if (turn == Sense.TurnOrNo || turn == Sense.TurnFarOrNo)
            {
                clause.No++;
            }
            else if (turn == Sense.TurnOrBad)
            {
                clause.Bad += weight;
            }
        }

        // The phrase just counted made the other: "iyi değil", "sorun yok".
        private static void Turned(bool wasGood, float weight, ref Tally clause)
        {
            if (wasGood)
            {
                clause.Good -= weight;
                clause.Bad += weight;
            }
            else
            {
                clause.Bad -= weight;
                clause.Good += weight;
            }
        }

        // A "super" or "bem" before these strengthens them rather than being good itself.
        private static bool Strengthened(Sense next) =>
            next == Sense.Good || next == Sense.Bad || next == Sense.VeryOrGood || next == Sense.Opening || next == Sense.Very;

        // True when the answer goes on after its first phrase.
        private static bool SaysMore(List<Sense> said)
        {
            for (int k = 1; k < said.Count; k++)
            {
                if (said[k] != Sense.Stop && said[k] != Sense.Question)
                {
                    return true;
                }
            }
            return false;
        }

        // The word so far, ended: added to the words.
        private static void Flush(StringBuilder word, List<string> words)
        {
            if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Length = 0;
            }
        }

        // The end of a clause: one mark between two clauses, the question's if either is one; none before the first word.
        private static void Mark(List<string> words, string mark)
        {
            if (words.Count == 0)
            {
                return;
            }
            string before = words[words.Count - 1];
            if (before == Break || before == Asked)
            {
                if (mark == Asked)
                {
                    words[words.Count - 1] = Asked;
                }
                return;
            }
            words.Add(mark);
        }

        // A letter in lower case without its accent (ß as ss, the Turkish dotless i as i).
        private static void Fold(StringBuilder word, char c)
        {
            if (c == 'İ' || c == 'ı')
            {
                c = 'i';   // Turkish İ and ı
            }
            c = char.ToLowerInvariant(c);
            switch (c)
            {
                case 'à': case 'á': case 'â': case 'ã': case 'ä': case 'å': case 'ā': c = 'a'; break;
                case 'ç': case 'ć': case 'č': c = 'c'; break;
                case 'è': case 'é': case 'ê': case 'ë': case 'ē': case 'ę': case 'ė': c = 'e'; break;
                case 'ì': case 'í': case 'î': case 'ï': case 'ī': c = 'i'; break;
                case 'ñ': case 'ń': c = 'n'; break;
                case 'ò': case 'ó': case 'ô': case 'õ': case 'ö': case 'ø': case 'ō': c = 'o'; break;
                case 'ù': case 'ú': case 'û': case 'ü': case 'ū': c = 'u'; break;
                case 'ý': case 'ÿ': c = 'y'; break;
                case 'ğ': c = 'g'; break;
                case 'ş': case 'ś': case 'š': c = 's'; break;
                case 'ž': case 'ź': case 'ż': c = 'z'; break;
                case 'ł': c = 'l'; break;
                case 'ё': c = 'е'; break;
                case 'ß': Letter(word, 's'); Letter(word, 's'); return;
                case 'œ': Letter(word, 'o'); Letter(word, 'e'); return;
                case 'æ': Letter(word, 'a'); Letter(word, 'e'); return;
            }
            Letter(word, c);
        }

        private static void Letter(StringBuilder word, char c) => word.Append(c);

        // A Chinese character (the unified ideographs, extension A and the compatibility ones).
        private static bool Han(char c) => (c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿') || (c >= '豈' && c <= '﫿');

        // The signs that end a clause; anything else that is not a letter only parts two words.
        private static bool Stops(char c)
        {
            switch (c)
            {
                case '.': case ',': case '!': case ';': case ':': case '(': case ')': case '[': case ']': case '\n': case '\r':
                case '…': case '—': case '–': case '。': case '，': case '！': case '；': case '、': case '：':
                    return true;
                default:
                    return false;
            }
        }

        // A smiley typed with signs (":)", ":-(", ";)", "=D", ":'(", ":/", "<3", "^^", "^_^"), as its sign; null for anything else.
        private static string Smiley(string t, int i, int end, out int used)
        {
            used = 0;
            char c = t[i];
            if (c == '<' && i + 1 < end && t[i + 1] == '3')
            {
                used = 2;
                return GoodSign;
            }
            if (c == '^' && i + 1 < end && t[i + 1] == '^')
            {
                used = 2;
                return GoodSign;
            }
            if (c == '^' && i + 2 < end && t[i + 1] == '_' && t[i + 2] == '^')
            {
                used = 3;
                return GoodSign;
            }
            if (c != ':' && c != ';' && c != '=')
            {
                return null;
            }
            int k = i + 1;
            if (k < end && (t[k] == '-' || t[k] == '\''))
            {
                k++;   // a nose, or a tear
            }
            if (k >= end)
            {
                return null;
            }
            bool tear = t[k - 1] == '\'';
            string sign;
            switch (t[k])
            {
                case ')': case ']': case 'D': sign = GoodSign; break;
                case '(': case '[': sign = BadSign; break;
                case '|': sign = PlainSign; break;
                case '/': sign = k + 1 < end && t[k + 1] == '/' ? null : PlainSign; break;   // (not a web address's "://")
                default: sign = null; break;
            }
            if (sign == null || (tear && sign == PlainSign))
            {
                return null;
            }
            used = k - i + 1;
            return sign;
        }

        // An emoji beyond the first plane as its sign: smiles, thumbs and hearts good; tears, frowns and the sick bad; a shrug unclear.
        private static string Emoji(int code)
        {
            switch (code)
            {
                case 0x1F600: case 0x1F601: case 0x1F603: case 0x1F604: case 0x1F606: case 0x1F607: case 0x1F609: case 0x1F60A: case 0x1F60B:
                case 0x1F60C: case 0x1F60D: case 0x1F60E: case 0x1F618: case 0x1F642: case 0x1F917: case 0x1F929: case 0x1F970: case 0x1F973:
                case 0x1F911: case 0x1F44D: case 0x1F44C: case 0x1F64C: case 0x1F44F: case 0x1F389: case 0x1F38A: case 0x1F4AA: case 0x1F525:
                case 0x1F496: case 0x1F497: case 0x1F499: case 0x1F49A: case 0x1F49B: case 0x1F49C: case 0x1F495: case 0x1F4AF: case 0x1F4B0:
                case 0x1F4C8: case 0x1F3C6: case 0x1F947: case 0x1F680: case 0x1F31F:
                    return GoodSign;
                case 0x1F61E: case 0x1F61F: case 0x1F620: case 0x1F621: case 0x1F622: case 0x1F623: case 0x1F624: case 0x1F625: case 0x1F626:
                case 0x1F627: case 0x1F628: case 0x1F629: case 0x1F62B: case 0x1F62C: case 0x1F62D: case 0x1F630: case 0x1F631: case 0x1F635:
                case 0x1F637: case 0x1F641: case 0x1F612: case 0x1F613: case 0x1F614: case 0x1F615: case 0x1F616: case 0x1F44E: case 0x1F494:
                case 0x1F912: case 0x1F915: case 0x1F922: case 0x1F92E: case 0x1F927: case 0x1F4A9: case 0x1F4B8: case 0x1F4C9: case 0x1F198:
                    return BadSign;
                case 0x1F610: case 0x1F611: case 0x1F937: case 0x1F914: case 0x1F636:
                    return PlainSign;
                default:
                    return null;
            }
        }

        // A sign of the first plane that stands for good or bad: a smile, a heart, a tick, a star; a frown, a cross.
        private static string Symbol(char c)
        {
            switch (c)
            {
                case '☺': case '❤': case '♥': case '✨': case '✅': case '✔': case '⭐':
                    return GoodSign;
                case '☹': case '❌':
                    return BadSign;
                default:
                    return null;
            }
        }
    }
}
