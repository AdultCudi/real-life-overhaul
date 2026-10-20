using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using RealLifeOverhaul.Texts;

// Checks for how a request of data v6 is made so that a reply cannot break the rules (Texts/TextDecoding.cs): the three
// GBNF grammars read by a small matcher of their own here and tried on lines that keep and break each rule (one line, no
// "!" in any form, no dash, a hyphen only inside a word, a capital at each sentence's start where asked, the full-width
// digits kept out of Chinese); which grammar a card takes; the tokens; the name's forms and the logit_bias; the request's
// and /tokenize's bodies (a pack before data v6 sends what it always sent); the hub filling a data 6 job; and, where the
// Lite pack is on this PC, the pinned llama.cpp taking each grammar and the name's ban (TextsRunnerSix, from TextsRunner).
internal static partial class Program
{
    /// <summary>A GBNF grammar as llama.cpp reads the subset TextDecoding writes: rules, alternatives, groups, literals, classes and * + ?.</summary>
    private sealed class Gbnf
    {
        private abstract class Node
        {
            public int Id;
        }

        private sealed class Alt : Node
        {
            public readonly List<List<Item>> Seqs = new List<List<Item>>();
        }

        private sealed class Item
        {
            public Node Atom;
            public char Quantity;   // '\0', '*', '+' or '?'
        }

        private sealed class Ref : Node
        {
            public string Name;
        }

        private sealed class Lit : Node
        {
            public string Text;
        }

        private sealed class Cls : Node
        {
            public bool Negated;
            public readonly List<int[]> Ranges = new List<int[]>();
        }

        private readonly Dictionary<string, Alt> _rules = new Dictionary<string, Alt>(StringComparer.Ordinal);
        private readonly Dictionary<long, HashSet<int>> _memo = new Dictionary<long, HashSet<int>>();
        private string _text, _s;
        private int _at, _ids;
        public string Error;

        public static Gbnf Parse(string grammar)
        {
            var g = new Gbnf();
            try
            {
                foreach (string raw in grammar.Split('\n'))
                {
                    if (raw.Trim().Length == 0)
                    {
                        continue;
                    }
                    int def = raw.IndexOf("::=", StringComparison.Ordinal);
                    if (def < 0)
                    {
                        throw new FormatException("no ::= in " + raw);
                    }
                    string name = raw.Substring(0, def).Trim();
                    g._text = raw;
                    g._at = def + 3;
                    Alt body = g.ParseAlt();
                    g.Spaces();
                    if (g._at < raw.Length)
                    {
                        throw new FormatException("left over at " + g._at + " in " + raw);
                    }
                    if (g._rules.ContainsKey(name))
                    {
                        throw new FormatException("the rule " + name + " twice");
                    }
                    g._rules[name] = body;
                }
                if (!g._rules.ContainsKey("root"))
                {
                    throw new FormatException("no root");
                }
            }
            catch (FormatException e)
            {
                g.Error = e.Message;
            }
            return g;
        }

        /// <summary>The names of every rule referred to but not defined (none in a grammar llama.cpp accepts).</summary>
        public List<string> Undefined()
        {
            var missing = new List<string>();
            foreach (Alt a in _rules.Values)
            {
                Walk(a, missing);
            }
            return missing;
        }

        private void Walk(Node n, List<string> missing)
        {
            switch (n)
            {
                case Ref r:
                    if (!_rules.ContainsKey(r.Name) && !missing.Contains(r.Name))
                    {
                        missing.Add(r.Name);
                    }
                    break;
                case Alt a:
                    foreach (List<Item> seq in a.Seqs)
                    {
                        foreach (Item i in seq)
                        {
                            Walk(i.Atom, missing);
                        }
                    }
                    break;
            }
        }

        private void Spaces()
        {
            while (_at < _text.Length && (_text[_at] == ' ' || _text[_at] == '\t'))
            {
                _at++;
            }
        }

        private Alt ParseAlt()
        {
            var alt = new Alt { Id = _ids++ };
            alt.Seqs.Add(ParseSeq());
            Spaces();
            while (_at < _text.Length && _text[_at] == '|')
            {
                _at++;
                alt.Seqs.Add(ParseSeq());
                Spaces();
            }
            return alt;
        }

        private List<Item> ParseSeq()
        {
            var seq = new List<Item>();
            while (true)
            {
                Spaces();
                if (_at >= _text.Length || _text[_at] == '|' || _text[_at] == ')')
                {
                    return seq;
                }
                Node atom = ParseAtom();
                char q = '\0';
                if (_at < _text.Length && (_text[_at] == '*' || _text[_at] == '+' || _text[_at] == '?'))
                {
                    q = _text[_at++];
                }
                seq.Add(new Item { Atom = atom, Quantity = q });
            }
        }

        private Node ParseAtom()
        {
            char c = _text[_at];
            if (c == '(')
            {
                _at++;
                Alt inner = ParseAlt();
                Spaces();
                if (_at >= _text.Length || _text[_at] != ')')
                {
                    throw new FormatException("no ) in " + _text);
                }
                _at++;
                return inner;
            }
            if (c == '"')
            {
                _at++;
                var lit = new System.Text.StringBuilder();
                while (_at < _text.Length && _text[_at] != '"')
                {
                    lit.Append((char)Char());
                }
                if (_at >= _text.Length)
                {
                    throw new FormatException("an open string in " + _text);
                }
                _at++;
                return new Lit { Id = _ids++, Text = lit.ToString() };
            }
            if (c == '[')
            {
                _at++;
                var cls = new Cls { Id = _ids++ };
                if (_at < _text.Length && _text[_at] == '^')
                {
                    cls.Negated = true;
                    _at++;
                }
                while (_at < _text.Length && _text[_at] != ']')
                {
                    int lo = Char(), hi = lo;
                    if (_at + 1 < _text.Length && _text[_at] == '-' && _text[_at + 1] != ']')
                    {
                        _at++;
                        hi = Char();
                    }
                    if (hi < lo)
                    {
                        throw new FormatException("a range backwards in " + _text);
                    }
                    cls.Ranges.Add(new[] { lo, hi });
                }
                if (_at >= _text.Length)
                {
                    throw new FormatException("an open class in " + _text);
                }
                _at++;
                return cls;
            }
            int start = _at;
            while (_at < _text.Length && (char.IsLetterOrDigit(_text[_at]) || _text[_at] == '-'))
            {
                _at++;
            }
            if (_at == start)
            {
                throw new FormatException("'" + c + "' at " + _at + " in " + _text);
            }
            return new Ref { Id = _ids++, Name = _text.Substring(start, _at - start) };
        }

        // One character of a literal or a class, with GBNF's escapes (\xHH, \uHHHH, \t \n \r, and a character escaped as itself).
        private int Char()
        {
            char c = _text[_at++];
            if (c != '\\')
            {
                return c;
            }
            char e = _text[_at++];
            switch (e)
            {
                case 'x': return Hex(2);
                case 'u': return Hex(4);
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                default: return e;
            }
        }

        private int Hex(int digits)
        {
            int v = int.Parse(_text.Substring(_at, digits), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            _at += digits;
            return v;
        }

        /// <summary>True when the whole line is of the grammar's root.</summary>
        public bool Matches(string s)
        {
            if (Error != null)
            {
                return false;
            }
            _s = s;
            _memo.Clear();
            return Match(_rules["root"], 0).Contains(s.Length);
        }

        private HashSet<int> Match(Node n, int pos)
        {
            long key = (long)n.Id << 32 | (uint)pos;
            if (_memo.TryGetValue(key, out HashSet<int> known))
            {
                return known;
            }
            var ends = new HashSet<int>();
            _memo[key] = ends;   // (no rule here refers to itself before reading a character)
            switch (n)
            {
                case Ref r: ends.UnionWith(Match(_rules[r.Name], pos)); break;
                case Alt a:
                    foreach (List<Item> seq in a.Seqs)
                    {
                        ends.UnionWith(MatchSeq(seq, 0, pos));
                    }
                    break;
                case Lit l:
                    if (string.CompareOrdinal(_s, pos, l.Text, 0, l.Text.Length) == 0 && pos + l.Text.Length <= _s.Length)
                    {
                        ends.Add(pos + l.Text.Length);
                    }
                    break;
                case Cls c:
                    if (pos < _s.Length)
                    {
                        bool inside = false;
                        foreach (int[] range in c.Ranges)
                        {
                            inside |= _s[pos] >= range[0] && _s[pos] <= range[1];
                        }
                        if (inside != c.Negated)
                        {
                            ends.Add(pos + 1);
                        }
                    }
                    break;
            }
            return ends;
        }

        private HashSet<int> MatchSeq(List<Item> seq, int i, int pos)
        {
            if (i == seq.Count)
            {
                return new HashSet<int> { pos };
            }
            var ends = new HashSet<int>();
            foreach (int p in MatchItem(seq[i], pos))
            {
                ends.UnionWith(MatchSeq(seq, i + 1, p));
            }
            return ends;
        }

        private HashSet<int> MatchItem(Item item, int pos)
        {
            switch (item.Quantity)
            {
                case '?':
                    var maybe = new HashSet<int>(Match(item.Atom, pos)) { pos };
                    return maybe;
                case '*': return Closure(item.Atom, new HashSet<int> { pos });
                case '+': return Closure(item.Atom, new HashSet<int>(Match(item.Atom, pos)));
                default: return Match(item.Atom, pos);
            }
        }

        private HashSet<int> Closure(Node atom, HashSet<int> start)
        {
            var all = new HashSet<int>(start);
            var frontier = new List<int>(start);
            while (frontier.Count > 0)
            {
                var next = new List<int>();
                foreach (int p in frontier)
                {
                    foreach (int e in Match(atom, p))
                    {
                        if (all.Add(e))
                        {
                            next.Add(e);
                        }
                    }
                }
                frontier = next;
            }
            return all;
        }
    }

    private static void TextsDecodingSix()
    {
        // the grammars: read as llama.cpp reads them, every character an ASCII escape, every rule defined
        var grammars = new Dictionary<string, Gbnf>(StringComparer.Ordinal);
        var faults = new List<string>();
        foreach (string name in TextDecoding.Names)
        {
            string text = TextDecoding.Grammar(name);
            Gbnf g = Gbnf.Parse(text);
            grammars[name] = g;
            bool ascii = true;
            foreach (char c in text)
            {
                ascii &= c < 128;
            }
            if (g.Error != null || g.Undefined().Count > 0 || !ascii)
            {
                faults.Add(name + ": " + (g.Error ?? (g.Undefined().Count > 0 ? "undefined " + string.Join(", ", g.Undefined()) : "a character not ASCII")));
            }
        }
        Check(faults.Count == 0 && TextDecoding.Grammar("none") == null, "texts v6, grammars: the three read as GBNF, every rule defined, written in ASCII escapes (" + (faults.Count == 0 ? "cased, free, uncased" : string.Join("; ", faults)) + ")");
        if (faults.Count > 0)
        {
            return;
        }
        Gbnf cased = grammars[TextDecoding.Cased], free = grammars[TextDecoding.Free], uncased = grammars[TextDecoding.Uncased];
        string Bad(Gbnf g, params string[] lines)
        {
            var wrong = new List<string>();
            foreach (string l in lines)
            {
                if (g.Matches(l))
                {
                    wrong.Add(l);
                }
            }
            return string.Join(" | ", wrong);
        }
        string Good(Gbnf g, params string[] lines)
        {
            var wrong = new List<string>();
            foreach (string l in lines)
            {
                if (!g.Matches(l))
                {
                    wrong.Add(l);
                }
            }
            return string.Join(" | ", wrong);
        }
        string nl = "\n", ls = ((char)0x2028).ToString(), em = ((char)0x2014).ToString(), en = ((char)0x2013).ToString(), fw = ((char)0xFF01).ToString(), ix = ((char)0xA1).ToString();
        string casedGood = Good(cased, "Hello there. How are you?", "Me enteré del accidente en 5th Ave. ¿Estás bien?", "It's $1,250.50 now.", "A well-known place.", "Fine… Thanks.",
            "OK", "\"Quoted\" words.", "3 days ago.", "Слышала про аварию. Ты в порядке?", "Ça va? Oui.", "Was it 19:30? Yes.");
        string casedBad = Bad(cased, "hello there.", "Hello. how are you?", "Hello!", "Hello" + fw, ix + "Hola, bueno.", "Hello " + em + " there", "Hello " + en + "there", "Hello - there",
            "Hello" + nl + "there", " Hello", "Hello" + ls + "there", "Hello -", "Fine? sure.", "Привет. как ты?", "Hi--there");
        Check(casedGood.Length == 0 && casedBad.Length == 0, "texts v6, the cased grammar: a capital (or no letter) at each sentence's start, one line, no '!' in any form, no dash, a hyphen only inside a word (passed wrongly: "
                                                             + (casedBad.Length > 0 ? casedBad : "none") + "; refused wrongly: " + (casedGood.Length > 0 ? casedGood : "none") + ")");
        string freeGood = Good(free, "hello there. how are you?", "lol ok", "Hi. fine", "a well-known place");
        string freeBad = Bad(free, "hello!", "hi - there", " hi", "hi" + nl + "there", "hi " + em + " there", "hi" + fw);
        Check(freeGood.Length == 0 && freeBad.Length == 0, "texts v6, the free grammar (a speaker in lower case): no capitals asked, the rest kept (passed wrongly: " + (freeBad.Length > 0 ? freeBad : "none")
                                                           + "; refused wrongly: " + (freeGood.Length > 0 ? freeGood : "none") + ")");
        string zhGood = Good(uncased, "听说你在5th Ave出了车祸。你还好吗？", "好的", "“你好”，他说。");
        string zhBad = Bad(uncased, "你好" + fw, "价格是１２３", "你好" + nl, "。你好", " 你好", "你好" + em + "再见");
        Check(zhGood.Length == 0 && zhBad.Length == 0, "texts v6, the uncased grammar (Chinese): no capitals, no full-width digits (a figure as the card writes it), the rest kept (passed wrongly: "
                                                       + (zhBad.Length > 0 ? zhBad : "none") + "; refused wrongly: " + (zhGood.Length > 0 ? zhGood : "none") + ")");
        Check(TextDecoding.GrammarName("zh-cn", true) == TextDecoding.Uncased && TextDecoding.GrammarName("pt", true) == TextDecoding.Free && TextDecoding.GrammarName("pt", false) == TextDecoding.Cased
              && TextDecoding.GrammarName("en", false) == TextDecoding.Cased && TextDecoding.Lowercase("warm, lowercase") && !TextDecoding.Lowercase("warm") && !TextDecoding.Lowercase(null),
            "texts v6, grammars: uncased for Chinese, free for a speaker who writes in lower case, cased for the rest");
        Check(Array.IndexOf(TextDecoding.Bans, "!") >= 0 && Array.IndexOf(TextDecoding.Bans, fw) >= 0 && Array.IndexOf(TextDecoding.Bans, ix) >= 0 && !cased.Matches("Hi" + ix + " there."),
            "texts v6, bans: '!', its full-width and opening forms, each kept out by the grammar");

        // the tokens, the name, the bodies
        TextIntent crashSix = TextIntents.Find("friend.crash", null, TextIntents.DataV6);
        Check(TextDecoding.Tokens(crashSix, "en") == TextLanguages.Tokens(200, "en") && TextDecoding.Tokens(crashSix, "pt") == TextLanguages.Tokens(240, "pt") && TextDecoding.Tokens(crashSix, "zh-cn") == 116,
            "texts v6, tokens: a request's n_predict from its intent's most units in its language");
        string[] forms = TextDecoding.NameForms("Alex");
        Check(forms != null && forms.Length == 2 && forms[0] == " Alex" && forms[1] == "Alex" && TextDecoding.NameForms("Al") == null && TextDecoding.NameForms("you") == null
              && TextDecoding.NameForms(null) == null && TextDecoding.NameForms("<b>x") == null,
            "texts v6, the name: its two forms (in a line, at its start); none for a name too short to ban alone, or the stand-in 'you'");
        Check(TextDecoding.Bias(new List<int> { 5, 7, 5, -1 }) == "[[5,false],[7,false]]" && TextDecoding.Bias(new List<int>()) == string.Empty && TextDecoding.Bias(null) == string.Empty,
            "texts v6, the name: each token once in logit_bias, never written; none without tokens");
        string grammar = TextDecoding.Grammar(TextDecoding.Cased);
        string body = TextJson.Request("<|im_start|>user\nhi<|im_end|>", 3, 82, grammar, "[[5,false]]");
        bool parsed = false, fields = false;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement r = doc.RootElement;
            parsed = true;
            fields = r.GetProperty("n_predict").GetInt32() == 82 && r.GetProperty("grammar").GetString() == grammar && r.GetProperty("logit_bias")[0][0].GetInt32() == 5
                     && !r.GetProperty("logit_bias")[0][1].GetBoolean() && r.GetProperty("stop")[0].GetString() == TextCard.Stop && r.GetProperty("cache_prompt").GetBoolean();
        }
        catch (Exception)
        {
        }
        string old = TextJson.Request("<|im_start|>user\nhi<|im_end|>", 3, TextJson.Tokens);
        Check(parsed && fields && TextJson.Request("<|im_start|>user\nhi<|im_end|>", 3, TextJson.Tokens, null, null) == old && !old.Contains("grammar") && !old.Contains("logit_bias"),
            "texts v6, the request: its grammar and the name's ban after the fields of every request, valid JSON; a pack before data v6 sends what it always sent");
        string tokenize = TextJson.Tokenize("Alex");
        bool tokenizeOk = false;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(tokenize);
            tokenizeOk = doc.RootElement.GetProperty("content").GetString() == "Alex" && !doc.RootElement.GetProperty("add_special").GetBoolean() && !doc.RootElement.GetProperty("parse_special").GetBoolean();
        }
        catch (Exception)
        {
        }
        Check(tokenizeOk && TextJson.FirstToken("{\"tokens\":[12,34]}") == 12 && TextJson.FirstToken("{\"tokens\": [ 7 ]}") == 7 && TextJson.FirstToken("{\"tokens\":[]}") == -1
              && TextJson.FirstToken(null) == -1 && TextJson.FirstToken("{\"error\":1}") == -1,
            "texts v6, /tokenize: the body asks no special tokens; its answer's first token read, none from an empty or a wrong answer");
        Check(TextJson.Cut("{\"content\":\"Hi\",\"stop_type\":\"limit\"}") && !TextJson.Cut("{\"content\":\"Hi\",\"stop_type\":\"eos\"}") && !TextJson.Cut("{\"content\":\"Hi\",\"stop_type\":\"word\"}") && !TextJson.Cut(null),
            "texts v6, the answer: a reply that stopped at its most tokens is known as cut");

        // the hub's job of data v6
        var q = new TextQueue();
        var hub = new TextsHub(q) { Runner = new PretendRunner(), Data = TextIntents.DataV6, Language = "en", Languages = new[] { "en", "zh-cn" } };
        TextJob Job(string key, string intent, string branch, string known, string style, string lang)
        {
            hub.Language = lang;
            TextRequest r = TextsAskOf(key, intent, null, w => { }, TextPriority.Soon, 30);
            r.Branch = branch;
            r.Who.Role = "a stranger in the street";
            r.Who.Style = style;
            r.Slots.Set("place", "The Night Owl").Set("way", "off to your left").Set("walk", "6 minutes");
            if (known != null)
            {
                r.Slots.Set("known", known);
            }
            r.History.Add(new TextSaid("Alex", "Is there a bar near here?"));
            return hub.Ask(r, 0) ? q.Take(0.1, true) : null;
        }
        TextJob untold = Job("j1", "street.directions", "found", "no", "warm", "en");
        TextJob told = Job("j2", "street.directions", "found", "yes", "warm", "en");
        TextJob lower = Job("j3", "street.directions", "found", "no", "cheeky, lowercase", "en");
        TextJob zh = Job("j4", "street.directions", "found", "no", "warm", "zh-cn");
        TextJob friend = Job("j5", "friend.crash", null, null, "warm", "en");
        TextIntent street = TextIntents.Find("street.directions", "found", TextIntents.DataV6);
        Check(untold != null && untold.BanName == "Alex" && untold.Grammar == TextDecoding.CasedText && untold.Tokens == TextDecoding.Tokens(street, "en") && untold.Prompt.Contains("you know their name: no")
              && told != null && told.BanName == null && lower != null && lower.Grammar == TextDecoding.FreeText && zh != null && zh.Grammar == TextDecoding.UncasedText
              && zh.Tokens == TextDecoding.Tokens(street, "zh-cn") && friend != null && friend.BanName == null && friend.Grammar == TextDecoding.CasedText,
            "texts v6, the hub: a data 6 job carries its grammar by the language and the style, its tokens, and the player's name to keep out only where the card says it is not known");
        Check(Job("j6", "street.directions", "found", null, "warm", "en") == null, "texts v6, the hub: a stranger's card without whether they know the name is not built (the written line)");
    }

    // Data v6's requests through the pinned llama.cpp with the Lite pack (called by TextsRunner once its server is ready):
    // each grammar is taken and kept by the server, the name's tokens come from its /tokenize, and the reply ends inside its tokens.
    private static void TextsRunnerSix(TextQueue q, TextServer server)
    {
        TextIntent intent = TextIntents.Find("street.directions", "found", TextIntents.DataV6);
        var cases = new[] { ("cased", "en", "warm"), ("free", "en", "cheeky, lowercase"), ("uncased", "zh-cn", "warm") };
        int kept = 0;
        var lines = new List<string>();
        q.SetBudget(120, TextQueue.BackgroundPerMinute);
        foreach (var c in cases)
        {
            var who = new TextWho { Name = "Ruth Nakamura", First = "Ruth", Age = 40, Job = "bartender", District = "Murray Hill", Role = "a stranger in the street", Mood = "fine", Style = c.Item3,
                                    Player = "Alex", When = "Sunday 21:00" };
            var slots = new TextSlots().Set("place", "The Night Owl").Set("street", "8th Ave").Set("way", "off to your left").Set("walk", "6 minutes").Set("known", "no");
            var sheet = new TextSheet();
            if (intent == null || !TextIntents.Build(intent, who, slots, new[] { new TextSaid("Alex", "Is there a bar near here?") }, sheet))
            {
                lines.Add(c.Item1 + ": no card");
                continue;
            }
            sheet.Lang = c.Item2;
            string grammar = TextDecoding.Grammar(TextDecoding.GrammarName(c.Item2, TextDecoding.Lowercase(c.Item3)));
            var job = new TextJob { Key = "six:" + c.Item1, Prompt = TextCard.ChatMl(sheet, TextCard.SystemSix), Priority = TextPriority.Now, Deadline = TextClock.Now + 25,
                                    Tokens = TextDecoding.Tokens(intent, c.Item2), Grammar = grammar, BanName = "Alex" };
            if (!q.Add(job, TextClock.Now))
            {
                lines.Add(c.Item1 + ": not taken by the queue (" + job.Why + ")");
                continue;
            }
            var sw = Stopwatch.StartNew();
            while ((job.State == TextJobState.Waiting || job.State == TextJobState.Running) && sw.Elapsed.TotalSeconds < 30)
            {
                System.Threading.Thread.Sleep(20);
            }
            bool ok = job.State == TextJobState.Written && job.Raw != null && Gbnf.Parse(grammar).Matches(job.Raw);
            kept += ok ? 1 : 0;
            lines.Add(c.Item1 + ": " + job.State + (job.Raw != null ? " \"" + job.Raw + "\"" : string.Empty) + (job.Why != null ? " (" + job.Why + ")" : string.Empty) + (job.Cut ? " cut" : string.Empty));
        }
        Console.WriteLine("  texts v6, the runner: " + string.Join("; ", lines));
        Check(kept == cases.Length, "texts v6, the runner: the pinned llama.cpp takes each grammar and the name's ban, and every reply keeps its grammar (" + kept + " of " + cases.Length + ")");
    }
}
