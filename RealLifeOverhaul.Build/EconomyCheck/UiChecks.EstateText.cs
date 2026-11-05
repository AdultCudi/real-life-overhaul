using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// What the Property and Car Showroom pages SAY, in every state of both apps in en, pt and zh-cn: a word never doubled by its article ("Cancel the the heating service"), no
// English left in a translated page (a unit's name, a guest's words), no colon or semicolon in a sentence, a decision card's choices that lift under the pointer, and a pad
// that walks every target of a card in reading order.
internal static partial class Program
{
    // The texts whose own words put an article or a preposition before the fault: they take the bare noun (EstateText.FaultNoun), never the phrase with its article.
    private static readonly string[] FaultNounKeys =
    {
        "reallife_ui_estate_service_name", "reallife_ui_estate_service_line", "reallife_ui_estate_book_kind", "reallife_estate_note_service_done", "reallife_ui_estate_service_title"
    };

    private static readonly Regex DoubledWord = new Regex(@"\b(\p{L}{2,})\s+\1\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    // Portuguese joins a preposition and the article after it ("do aquecimento", "no quarto"): the two apart are a fault.
    private static readonly Regex UnjoinedPt = new Regex(@"\b(de|em|por)\s+(o|a|os|as)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    // English words that stood in the samples as literals (a unit's name, a guest's words, a kind of fault); a proper name of the sample (Park and Lane) is taken out first.
    private static readonly Regex EnglishWord = new Regex(@"\b(flat|floor|unit|house|suite|shop|office|double|single|the|and|with|guest|heating|lock|leak|damp|fittings|electrics|Flat|Floor|Unit|The)\b", RegexOptions.CultureInvariant);
    private static readonly string[] SampleNames = { "Park and Lane", "Bell and Rowe", "Okafor and Sons", "Pier House Hotel" };
    private static readonly Regex Sentence = new Regex(@"(: |;|：|；)", RegexOptions.CultureInvariant);

    private static IEnumerable<KeyValuePair<string, string>> EstateStateLines(string lang)
    {
        UiWords words = lang == "en" ? ((key, english) => LocaleFile("en").TryGetValue(key, out string said) ? said : english) : UiWords(lang);
        foreach (bool dealerApp in new[] { false, true })
        {
            foreach (string state in dealerApp ? UiSamples.DealerStates : UiSamples.EstateStates)
            {
                EstateModel pageModel = dealerApp ? UiSamples.Dealer(words, state) : UiSamples.Estate(words, state);
                var scene = new UiScene();
                EstateFace.Lay(scene, pageModel, UiFrame.FullMenu(16f / 9f, "Property"), true, UiDefaultKeys.Keyboard);
                foreach (string said in EstateTextLines(scene))
                {
                    yield return new KeyValuePair<string, string>((dealerApp ? "dealer " : "estate ") + state, said);
                }
            }
        }
    }

    private static void EstateTextChecks()
    {
        // The fault's words: a bare noun carries no article, a text that takes the phrase with its article puts none before it, and a text that takes the noun is never given the
        // phrase by the code that fills it. "Cancel the the heating service" was the key's "the" and the fault's own "the heating".
        string badNoun = "none", badPhrase = "none", badSource = "none";
        var article = new Dictionary<string, Regex>
        {
            ["en"] = new Regex(@"^(the|a|an)\s", RegexOptions.IgnoreCase), ["pt"] = new Regex(@"^(o|a|os|as|um|uma)\s", RegexOptions.IgnoreCase), ["zh-cn"] = new Regex(@"^$")
        };
        var before = new Dictionary<string, Regex>
        {
            ["en"] = new Regex(@"\b(the|a|an)\s+\{fault\}", RegexOptions.IgnoreCase), ["pt"] = new Regex(@"\b(o|a|os|as|um|uma|de|do|da|dos|das|no|na|nos|nas|em|por)\s+\{fault\}", RegexOptions.IgnoreCase),
            ["zh-cn"] = new Regex(@"^$")
        };
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> d = LocaleFile(lang);
            for (int kind = 0; kind <= 6; kind++)
            {
                if (!d.TryGetValue("reallife_estate_fault_noun_" + kind, out string noun) || noun.Length == 0 || article[lang].IsMatch(noun))
                {
                    badNoun = lang + " " + kind + " " + (noun ?? "missing");
                }
            }
            foreach (KeyValuePair<string, string> kv in d)
            {
                if (kv.Value.Contains("{fault}") && Array.IndexOf(FaultNounKeys, kv.Key) < 0 && before[lang].IsMatch(kv.Value))
                {
                    badPhrase = badPhrase == "none" ? lang + " " + kv.Key + ": " + kv.Value : badPhrase;
                }
            }
        }
        string[] roots = { EstateSourceRoot(), EstateSourceRoot() != null ? System.IO.Path.GetFullPath(System.IO.Path.Combine(EstateSourceRoot(), "..", "..", "Economy", "Estate")) : null };
        foreach (string root in roots)
        {
            if (root != null && Directory.Exists(root))
            {
                foreach (string file in Directory.GetFiles(root, "*.cs"))
                {
                    string text = File.ReadAllText(file);
                    foreach (string key in FaultNounKeys)
                    {
                        for (int at = text.IndexOf("\"" + key + "\"", StringComparison.Ordinal); at >= 0; at = text.IndexOf("\"" + key + "\"", at + 1, StringComparison.Ordinal))
                        {
                            string window = text.Substring(at, Math.Min(text.Length - at, 260));
                            int end = window.IndexOf(';');
                            if ((end >= 0 ? window.Substring(0, end) : window).Contains("EstateText.Fault("))
                            {
                                badSource = System.IO.Path.GetFileName(file) + " " + key;
                            }
                        }
                    }
                }
            }
        }
        Check(badNoun == "none", "estate: the fault nouns of every language carry no article (" + badNoun + ")");
        Check(badPhrase == "none", "estate: no text puts an article or a preposition before the fault phrase that already has its own (" + badPhrase + ")");
        Check(badSource == "none", "estate: the texts that give their own article are filled with the bare noun, never the phrase (" + badSource + ")");

        // Every line of every state: no doubled word, no preposition apart from its article in pt, no English left in pt and zh, no colon or semicolon in a sentence.
        string doubled = "none", unjoined = "none", english = "none", colon = "none";
        var firstSeen = new HashSet<string>();
        int lines = 0;
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            foreach (KeyValuePair<string, string> line in EstateStateLines(lang))
            {
                lines++;
                string said = line.Value;
                string where = lang + " " + line.Key + ": " + said;
                if (DoubledWord.IsMatch(said))
                {
                    doubled = Collect(doubled, where, firstSeen);
                }
                if (lang == "pt" && UnjoinedPt.IsMatch(said))
                {
                    unjoined = Collect(unjoined, where, firstSeen);
                }
                if (lang != "en")
                {
                    string plain = said;
                    foreach (string name in SampleNames)
                    {
                        plain = plain.Replace(name, string.Empty);
                    }
                    if (EnglishWord.IsMatch(plain))
                    {
                        english = Collect(english, where, firstSeen);
                    }
                }
                if (Sentence.IsMatch(said))
                {
                    colon = Collect(colon, where, firstSeen);
                }
            }
        }
        Check(doubled == "none", "estate: no word is said twice in a row on any page in en, pt or zh (" + doubled + ")");
        Check(unjoined == "none", "estate: pt never leaves a preposition apart from its article (" + unjoined + ")");
        Check(english == "none", "estate: no English word of the samples (a unit's name, a guest's words, a kind of fault) reaches the pt and zh pages (" + english + ")");
        // And not in any text the live pages can say, shown on a previewed state or not: every key the pages' sources name, in en, pt and zh-cn.
        string liveColon = "none";
        int liveKeys = 0;
        string uiRoot = EstateSourceRoot();
        if (uiRoot != null)
        {
            var named = new HashSet<string>();
            foreach (string file in Directory.GetFiles(uiRoot, "*.cs"))
            {
                foreach (Match key in Regex.Matches(File.ReadAllText(file), "\"(reallife_[a-z0-9_]+)\""))
                {
                    named.Add(key.Groups[1].Value);
                }
            }
            foreach (string lang in new[] { "en", "pt", "zh-cn" })
            {
                Dictionary<string, string> d = LocaleFile(lang);
                foreach (string key in named)
                {
                    if (d.TryGetValue(key, out string said))
                    {
                        liveKeys++;
                        if (Sentence.IsMatch(said))
                        {
                            liveColon = Collect(liveColon, lang + " " + key + ": " + said, firstSeen);
                        }
                    }
                }
            }
        }
        Check(liveColon == "none" && (uiRoot == null || liveKeys > 600), "estate: no colon or semicolon in any text the live pages name, in en, pt or zh-cn (" + liveColon + "; " + liveKeys + " texts read)");
        Check(colon == "none" && lines > 3000, "estate: no colon or semicolon in any line of any page in en, pt or zh (" + colon + "; " + lines + " lines read)");

        // A decision's choices lift under the pointer: the target's face has the hover fill of a table row, the resting fill lying behind it.
        string flat = "none";
        foreach (string[] one in new[] { new[] { "estate", "call" }, new[] { "estate", "applicant" }, new[] { "dealer", "deal" } })
        {
            UiScene card = LayEstate(NewEstate(one[0] == "dealer", one[1], out EstateStub _));
            int choices = 0;
            for (int i = 0; i < card.NodeCount; i++)
            {
                if (card.Nodes[i].Command == EstateFace.ActionCommand && card.Nodes[i].Face >= 0 && card.Items[card.Nodes[i].Face].Kind == UiKind.Box && card.Nodes[i].H > 40f)
                {
                    choices++;
                    flat = card.Items[card.Nodes[i].Face].Fill == UiRole.None ? flat : one[1] + " choice " + i;
                }
            }
            flat = choices >= 2 ? flat : one[1] + " has " + choices + " choices";
        }
        Check(flat == "none", "estate: every choice of the three decision cards has the hover fill on its face, so the pointer's answer is seen (" + flat + ")");

        // The pad's tour of a card reaches every target in reading order and stays short.
        string lost = "none";
        int tours = 0;
        foreach (bool dealerApp in new[] { false, true })
        {
            foreach (string state in dealerApp ? UiSamples.DealerStates : UiSamples.EstateStates)
            {
                EstateModel m = dealerApp ? UiSamples.Dealer(Words2, state) : UiSamples.Estate(Words2, state);
                if (!m.Card && m.Shape != EstateShape.Decide && m.Shape != EstateShape.Auction)
                {
                    continue;
                }
                var scene = new UiScene();
                EstateFace.Lay(scene, m, UiFrame.FullMenu(16f / 9f, "Property"), true, UiDefaultKeys.XboxPad);
                int start = UiFocusRules.First(scene);
                string walk = EstateTour.Walk(scene, start);
                // A list is one target: a row of it counts as the list's first row. The walk stands on each target once and on none a second time.
                var head = new Dictionary<int, int>();
                for (int i = 0; i < scene.NodeCount; i++)
                {
                    if (scene.Nodes[i].List >= 0 && !head.ContainsKey(scene.Nodes[i].List))
                    {
                        head[scene.Nodes[i].List] = i;
                    }
                }
                Func<int, int> rep = n => scene.Nodes[n].List >= 0 ? head[scene.Nodes[n].List] : n;
                var seen = new HashSet<int> { rep(start) };
                int at = start, repeats = 0;
                foreach (char c in walk)
                {
                    int next = UiFocusRules.Next(scene, at, c == 'R' ? 1 : c == 'L' ? -1 : 0, c == 'D' ? 1 : c == 'U' ? -1 : 0);
                    at = next >= 0 ? next : at;
                    repeats += seen.Add(rep(at)) ? 0 : 1;
                }
                var wanted = new HashSet<int>();
                for (int i = 0; i < scene.NodeCount; i++)
                {
                    if (!scene.Nodes[i].Disabled && scene.Nodes[i].Layer == scene.Nodes[start].Layer)
                    {
                        wanted.Add(rep(i));
                    }
                }
                int missed = 0;
                foreach (int target in wanted)
                {
                    missed += seen.Contains(target) ? 0 : 1;
                }
                tours++;
                if (missed > 0 || repeats > 0)
                {
                    lost = (dealerApp ? "dealer " : "estate ") + state + ": " + missed + " of " + wanted.Count + " targets missed, " + repeats + " stood on twice, " + walk.Length + " presses (" + walk + ")";
                }
            }
        }
        Check(lost == "none" && tours >= 8, "estate: the pad's walk of every card stands on each of its targets once and on none twice (" + tours + " cards; " + lost + ")");
    }

    // A finding added to the ones before it (the first few, each once), so one run names what to mend.
    private static string Collect(string so_far, string finding, HashSet<string> seen)
    {
        if (!seen.Add(finding) || seen.Count > 6)
        {
            return so_far;
        }
        return so_far == "none" ? finding : so_far + " | " + finding;
    }

    private static string Words2(string key, string english) => english;
}
