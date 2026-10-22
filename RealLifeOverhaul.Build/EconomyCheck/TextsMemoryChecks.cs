using System;
using System.Collections.Generic;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

// Checks for what people remember of the player (data v6: Texts/TextMemoryRules.cs, TextMemoryStore.cs, TextMemoryTopics.cs):
// a fact's line in the card's words with how long ago, the lines a card carries chosen the same way whatever order the
// facts were recorded in (only the kinds its speaker may remember, fresh, not its own news, the most relevant first, one
// of each kind and code, at most three), the keys a memory may be kept under (never a Going Public session's own numbers),
// the store's save line read back as written, its caps, its pruning by age and a stranger's day, a stranger who swaps
// numbers taking what they remember along, a later version's line kept, and the words of every topic.
internal static partial class Program
{
    private static void TextsMemorySix()
    {
        // the lines
        Check(TextMemoryRules.Ago(0) == "today" && TextMemoryRules.Ago(1) == "yesterday" && TextMemoryRules.Ago(5) == "5 days ago" && TextMemoryRules.Ago(13) == "13 days ago"
              && TextMemoryRules.Ago(14) == "a few weeks ago" && TextMemoryRules.Ago(59) == "a few weeks ago" && TextMemoryRules.Ago(60) == "months ago" && TextMemoryRules.Ago(-2) == "today",
            "texts v6, memory: how long ago, in the card's words");
        Check(TextMemoryRules.Line(new TextFact(TextMemoryKinds.Met, "street", 98), 100) == "met you in the street, 2 days ago"
              && TextMemoryRules.Line(new TextFact(TextMemoryKinds.Favour, "directions", 100, "Bean There"), 100) == "gave you directions to Bean There, today"
              && TextMemoryRules.Line(new TextFact(TextMemoryKinds.Knows, "name", 3), 100) == "knows your name"
              && TextMemoryRules.Line(new TextFact(TextMemoryKinds.Event, "crash", 99), 100) == "heard you crashed your car, yesterday"
              && TextMemoryRules.Line(new TextFact(TextMemoryKinds.Event, "opened", 90, "Sam's Deli", "Dana"), 100) == "heard from Dana that you opened Sam's Deli, 10 days ago"
              && TextMemoryRules.Line(new TextFact(TextMemoryKinds.Favour, "owed", 50, "$300"), 100) == "you still owe them $300"
              && TextMemoryRules.Line(new TextFact(TextMemoryKinds.Favour, "directions", 100), 100) == null && TextMemoryRules.Line(new TextFact(TextMemoryKinds.Met, "nowhere", 100), 100) == null
              && TextMemoryRules.Line(new TextFact(TextMemoryKinds.Talked, "street", 100, "a\nb<|x|>"), 100) == "talked to you in the street about a bx, today",
            "texts v6, memory: a fact's line: its code's words and value, an event as heard (from whom), how long ago unless it stands; none without its value or for an unknown code");
        bool names = true;
        foreach (string[] c in TextMemoryRules.Codes)
        {
            TextMemoryKinds k = TextMemoryRules.KindOf(c[0]);
            names &= k != TextMemoryKinds.None && TextMemoryRules.Name(k) == c[0] && TextMemoryRules.Template(k, c[1]) == c[2] && TextMemoryRules.Lifetime(k) > 0;
        }
        Check(names && TextMemoryRules.KindOf("nothing") == TextMemoryKinds.None && TextMemoryRules.Codes.Length > 50, "texts v6, memory: every code of the six kinds has its words and its kind's lifetime");

        // a card's lines
        TextIntent friend = TextIntents.Find("friend.invite.coffee", null, TextIntents.DataV6);
        TextIntent mom = TextIntents.Find("mom.worry", null, TextIntents.DataV6);
        TextIntent gossip = TextIntents.Find("friend.gossip", "crash", TextIntents.DataV6);
        TextIntent post = TextIntents.Find("citizen.post.weather", null, TextIntents.DataV6);
        var facts = new List<TextFact>
        {
            new TextFact(TextMemoryKinds.Met, "work", 10),
            new TextFact(TextMemoryKinds.Talked, "outing", 95, "coffee"),
            new TextFact(TextMemoryKinds.Talked, "outing", 90, "dinner"),
            new TextFact(TextMemoryKinds.Tone, "stood_up", 97),
            new TextFact(TextMemoryKinds.Knows, "business", 10, "Sam's Deli"),
            new TextFact(TextMemoryKinds.Event, "crash", 99),
            new TextFact(TextMemoryKinds.Tone, "rude", 20),
            new TextFact(TextMemoryKinds.Favour, "lent", 98)
        };
        var lines = new List<string>();
        TextMemoryRules.Select(facts, friend, 100, null, lines);
        string first = string.Join(" | ", lines);
        var reversed = new List<TextFact>(facts);
        reversed.Reverse();
        TextMemoryRules.Select(reversed, friend, 100, null, lines);
        string second = string.Join(" | ", lines);
        var shuffled = new List<TextFact> { facts[4], facts[1], facts[7], facts[0], facts[6], facts[3], facts[5], facts[2] };
        TextMemoryRules.Select(shuffled, friend, 100, null, lines);
        Check(first == second && first == string.Join(" | ", lines) && lines.Count == TextMemoryRules.CardMost,
            "texts v6, memory: a card's lines the same whatever order the facts were recorded in, at most " + TextMemoryRules.CardMost + " (" + first + ")");
        Check(first == "met up with you for coffee, 5 days ago | you stood them up, 3 days ago | heard you crashed your car, yesterday",
            "texts v6, memory: the most relevant first: on an invitation's card the last outing and being stood up, then what they heard; the newer of each kind and code (" + first + ")");
        Check(!first.Contains("rude") && !first.Contains("dinner"), "texts v6, memory: a tone past its lifetime left out; one line of each kind and code (the newest)");
        TextMemoryRules.Select(facts, gossip, 100, TextIntents.Subject(gossip.Key, gossip.Branch), lines);
        Check(!string.Join(" | ", lines).Contains("crashed"), "texts v6, memory: an event the card's own news tells is not said again in its memory (" + string.Join(" | ", lines) + ")");
        TextMemoryRules.Select(new List<TextFact> { new TextFact(TextMemoryKinds.Met, "work", 90) }, mom, 100, null, lines);
        bool momNoMet = lines.Count == 0;
        TextMemoryRules.Select(facts, post, 100, null, lines);
        Check(momNoMet && lines.Count == 0 && post.MemoryMost == 0 && TextMemoryRules.Select(null, friend, 100, null, lines) == 0,
            "texts v6, memory: only the kinds a speaker may remember (Mom never 'met'), and none on what everybody reads");
        var stranger = TextIntents.Find("street.directions", "found", TextIntents.DataV6);
        var met = new List<TextFact>
        {
            new TextFact(TextMemoryKinds.Knows, "name", 100), new TextFact(TextMemoryKinds.Favour, "directions", 100, "Bean There"), new TextFact(TextMemoryKinds.Met, "street", 100),
            new TextFact(TextMemoryKinds.Event, "crash", 99)
        };
        TextMemoryRules.Select(met, stranger, 100, null, lines);
        Check(lines.Count == 3 && lines[0] == "gave you directions to Bean There, today" && lines[1] == "heard you crashed your car, yesterday",
            "texts v6, memory: the way given comes first on a card about the way, before even what they heard (" + string.Join(" | ", lines) + ")");

        // whose memory
        string[] ok = { "s:3", "sp:12345", "e:emp-7", "c:42", "boss:Hudson", "r:0", "mom", "inlaws", "lender", "f12", "p3" };
        string[] no = { "fn:45", "lp:3:1", "f", "p", "fx", "s:1|2", "s:1^2", null, string.Empty, "fp:9", new string('s', 90) };
        bool keys = true;
        foreach (string k in ok)
        {
            keys &= TextMemoryRules.KeyOk(k);
        }
        foreach (string k in no)
        {
            keys &= !TextMemoryRules.KeyOk(k);
        }
        Check(keys, "texts v6, memory: kept only under the player's own people's keys, the same alone or in a Going Public session; never a session's numbers");
        Check(TextMemoryRules.Kept("sp:1", new TextFact(TextMemoryKinds.Met, "street", 99), 100) && !TextMemoryRules.Kept("sp:1", new TextFact(TextMemoryKinds.Met, "street", 98), 100)
              && TextMemoryRules.Kept("s:1", new TextFact(TextMemoryKinds.Met, "street", 98), 100) && !TextMemoryRules.Kept("s:1", new TextFact(TextMemoryKinds.Tone, "warm", 60), 100)
              && TextMemoryRules.Stranger("sp:1") && !TextMemoryRules.Stranger("s:1"),
            "texts v6, memory: a stranger remembered for the day; the rest for their kind's lifetime");

        // the store
        var store = new TextMemoryStore();
        bool remembered = store.Remember("s:1", new TextFact(TextMemoryKinds.Met, "work", 5)) && store.Remember("s:1", new TextFact(TextMemoryKinds.Talked, "outing", 90, "coffee"))
                          && !store.Remember("fn:1", new TextFact(TextMemoryKinds.Met, "work", 5)) && !store.Remember("s:1", new TextFact(TextMemoryKinds.Met, "nowhere", 5));
        store.Remember("s:1", new TextFact(TextMemoryKinds.Talked, "outing", 95, "coffee"));
        store.Remember("sp:9", new TextFact(TextMemoryKinds.Met, "street", 99, "a|b^c%d", "Dé"));
        Check(remembered && store.People == 2 && store.Of("s:1").Count == 2 && store.Of("s:1")[1].Day == 95, "texts v6, memory store: a fact remembered again moves its day on; a session's key and an unknown code are not kept");
        string line = store.Write();
        var back = new TextMemoryStore();
        bool read = back.Read(line);
        Check(read && back.Write() == line && back.Of("sp:9")[0].Value == "a|b^c%d" && back.Of("sp:9")[0].From == "Dé" && line.StartsWith("v=1^m=s:1|met|work|5||", StringComparison.Ordinal),
            "texts v6, memory store: the save's line read back as written, its texts escaped (" + line + ")");
        Check(!back.Read("v=2^m=s:1|met|work|5||") && back.People == 0 && !back.Read("v=1^m=s:1|met") && back.People == 0 && back.Read("v=1^m=s:1|met|work|5|||later") && back.People == 1
              && back.Read("v=1^q=x^m=s:1|feel|warm|5||") && back.People == 0 && back.Read(null) && back.Read(string.Empty),
            "texts v6, memory store: a later version's line, or a damaged one, gives none (kept in the save as read); a later field or record passed over");
        int moved = store.Move("sp:9", "s:2");
        Check(moved == 1 && store.Of("sp:9") == null && store.Of("s:2") != null && store.Move("s:2", "fn:1") == 0, "texts v6, memory store: a stranger who swaps numbers takes what they remember to their new key");
        var full = new TextMemoryStore();
        for (int i = 0; i < TextMemoryRules.PeopleMost + 10; i++)
        {
            full.Remember("s:" + i, new TextFact(TextMemoryKinds.Met, "work", i));
        }
        for (int i = 0; i < 12; i++)
        {
            full.Remember("s:500", new TextFact(TextMemoryKinds.Talked, "street", 600, "topic " + i));
        }
        Check(full.People == TextMemoryRules.PeopleMost && full.Of("s:0") == null && full.Of("s:500").Count == TextMemoryRules.FactsMost,
            "texts v6, memory store: at most " + TextMemoryRules.PeopleMost + " people (the one remembered longest ago goes) and " + TextMemoryRules.FactsMost + " facts each");
        var prune = new TextMemoryStore();
        prune.Remember("sp:1", new TextFact(TextMemoryKinds.Met, "street", 90));
        prune.Remember("s:1", new TextFact(TextMemoryKinds.Tone, "warm", 50));
        prune.Remember("s:1", new TextFact(TextMemoryKinds.Met, "work", 50));
        int gone = prune.Prune(100);
        Check(gone == 2 && prune.People == 1 && prune.Of("s:1").Count == 1 && prune.Changed, "texts v6, memory store: pruned once a day: a stranger past their day, a fact past its lifetime");
        Check(TextMemoryStore.SaveKey == "reallife:texts_memory" && TextMemoryStore.Version == 1, "texts v6, memory store: its own key in the save");

        // the topics
        var streetTopics = new HashSet<string>();
        for (int i = 0; i < (int)StreetLine.Count; i++)
        {
            string topic = TextMemoryTopics.Street((StreetLine)i);
            if (topic != null)
            {
                streetTopics.Add(topic);
            }
        }
        Check(streetTopics.Count >= 9 && TextMemoryTopics.Tone(StreetLine.SmallWarm) == "warm" && TextMemoryTopics.Tone(StreetLine.ThreatLeave) == "threat" && TextMemoryTopics.Tone(StreetLine.Waiting) == null
              && TextMemoryTopics.Text("friend.invite.coffee") == "meeting up" && TextMemoryTopics.Text("partner.crash") == "your crash" && TextMemoryTopics.Text("boss.noshow") == "work"
              && TextMemoryTopics.Text(null) == null && TextMemoryTopics.Outings.Length == 8 && TextMemoryRules.Template(TextMemoryKinds.Tone, TextMemoryTopics.Tone(StreetLine.FlirtYes)) != null,
            "texts v6, memory topics: what each line of street talk and each text was about, and how a talk went, in the memory's words");
    }
}
