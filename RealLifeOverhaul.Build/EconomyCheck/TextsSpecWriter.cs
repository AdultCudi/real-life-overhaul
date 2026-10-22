using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using RealLifeOverhaul.Street;
using RealLifeOverhaul.Texts;

// RLO Texts' contract of data v6, in the shape the dataset pipeline reads (not-in-repo/textmodel/data/gen/v6/spec/
// CONTRACT.md), written from the mod's own tables so that the training data and the mod cannot drift apart:
//
//   SPEC_v6.json        every card of data v6: an entry for each intent (several when its branches differ in their role,
//                       must line or optional facts), with its card's lines as templates of flat {slots}, the slots' kinds,
//                       value sets and examples, what the speaker may remember (memory kinds as line templates), the
//                       reply's rules and its decoding (grammar, tokens, bans, the name kept out)
//   fixtures_v6.jsonl   one card for each intent, branch and language: every slot the card names, and the card byte for
//                       byte as the mod sends it (TextIntents.Build, then TextsHub.Finish: its lang and memory)
//
// `dotnet EconomyCheck.dll --texts-spec <folder>` writes both into the folder, and the fixtures into this checker's own
// TextsCardsV6.jsonl as well, which TextsSixChecks.cs rebuilds card by card with the mod's builder and renders again from
// the spec alone, as the pipeline does. The values of the cards carried from data 5 and older are the training generator's
// own (TextsCards.jsonl), with the districts as the game shows them in Chinese and Russian; the new cards' are drawn here
// from a fixed seed. The same mod writes the same files.
internal static partial class Program
{
    private const string SpecName = "SPEC_v6.json", FixturesName = "fixtures_v6.jsonl", FixturesCopy = "TextsCardsV6.jsonl";
    private const int SixToday = 100;          // the fixtures' game day, the "today" of their memory lines
    private const int AgeMark = 987654321;     // the age a face is drawn with, to find where its line says {age}

    /// <summary>One fixture: an intent of data v6 in one language, what its card was built from, and the card.</summary>
    private sealed class SixRow
    {
        public TextIntent Intent;
        public SixEntry Entry;
        public string Lang, Card;
        public readonly TextWho Who = new TextWho();
        public readonly TextSlots Slots = new TextSlots();
        public readonly List<TextSaid> History = new List<TextSaid>(3);
        public readonly List<string> Memory = new List<string>(3);
    }

    /// <summary>A card of the training generator's (TextsCards.jsonl): an intent of data 5 or older and its values.</summary>
    private sealed class OldRow
    {
        public string Intent, Branch;
        public int Data;
        public JsonElement Who, Slots, History;
    }

    /// <summary>An entry of the spec: an intent's branches that share their card's lines (the role, the must line) and optional facts.</summary>
    private sealed class SixEntry
    {
        public string Key, Role, MustLine;
        public string[] Optional;
        public bool History;
        public int Face;   // the card's lines of the speaker, at its start
        public readonly List<TextIntent> Branches = new List<TextIntent>(4);
        public readonly List<string> Card = new List<string>(14);
        public readonly List<int> Lengths = new List<int>(4);
        public TextIntent First => Branches[0];
    }

    // The districts as the game shows them in Chinese and Russian (English in the other five): English, Chinese, Russian.
    private static readonly string[][] SixDistricts =
    {
        new[] { "Garment District", "服装区", "Швейный район" },
        new[] { "Hell's Kitchen", "地狱厨房", "Адская кухня" },
        new[] { "Industry City", "产业城", "Индастри-Сити" },
        new[] { "Lower Manhattan", "下曼哈顿", "Нижний Манхэттен" },
        new[] { "Midtown", "中城", "Мидтаун" },
        new[] { "Murray Hill", "默里山", "Мюррей-Хилл" }
    };

    // Values of the new cards that the generator's own have none of.
    private static readonly string[] SixListed = { "Hudson Logistics", "Brightside Foods", "Northgate Media", "Pier Nine Holdings" };
    private static readonly string[] SixDegrees = { "LLB Law", "BSc Computer Science", "BBA Business Administration", "Diploma in Marketing", "MBA" };
    private static readonly string[] SixTitles = { "Store Clerk", "Sales Associate", "Assistant Manager", "Junior Analyst" };
    private static readonly string[] SixSkills =
    {
        "ba:skill_customerservice", "ba:skill_cleaning", "ba:skill_deliverydriver", "ba:skill_securityguard", "ba:skill_negotiation", "ba:skill_programmer",
        "ba:skill_logisticsmanager", "ba:skill_hrmanager", "ba:skill_graphicdesigner", "ba:skill_lawyer"
    };

    // The kinds the pipeline knows (CONTRACT.md: slots).
    private static readonly string[] SixKinds = { "person", "place", "job", "number", "text", "choice" };

    // ---------------------------------------------------------------- the mode

    /// <summary>`--texts-spec folder`: the contract and its fixtures written into the folder, and the fixtures into this checker's own file.</summary>
    private static int WriteTextsSpec(string folder)
    {
        Directory.CreateDirectory(folder);
        List<SixEntry> entries = SixEntries();
        List<SixRow> rows = SixFixtures(entries);
        string lines = SixLines(rows);
        string spec = SixSpec(entries, rows);
        var utf8 = new UTF8Encoding(false);
        File.WriteAllText(System.IO.Path.Combine(folder, FixturesName), lines, utf8);
        File.WriteAllText(System.IO.Path.Combine(TextsBuildFolder(), "EconomyCheck", FixturesCopy), lines, utf8);
        File.WriteAllText(System.IO.Path.Combine(folder, SpecName), spec, utf8);
        Console.WriteLine("RLO Texts data v6: " + SixKeys().Count + " intents in " + entries.Count + " entries, " + SixIntents().Count + " (intent, branch) pairs, "
                          + TextLanguages.All.Length + " languages, " + rows.Count + " fixtures; " + SpecName + " " + Encoding.UTF8.GetByteCount(spec) + " bytes, "
                          + FixturesName + " " + Encoding.UTF8.GetByteCount(lines) + " bytes, written to " + folder);
        return 0;
    }

    /// <summary>Every card of data v6, in the table's order: an intent and branch each.</summary>
    private static List<TextIntent> SixIntents()
    {
        var six = new List<TextIntent>(400);
        foreach (TextIntent t in TextIntents.All)
        {
            if (t.Data == TextIntents.DataV6)
            {
                six.Add(t);
            }
        }
        return six;
    }

    /// <summary>The intents of data v6 by key, in the table's order.</summary>
    private static List<string> SixKeys()
    {
        var keys = new List<string>(300);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (TextIntent t in SixIntents())
        {
            if (seen.Add(t.Key))
            {
                keys.Add(t.Key);
            }
        }
        return keys;
    }

    // ---------------------------------------------------------------- the entries

    /// <summary>The spec's entries: each intent's branches grouped by their role (street talk), must line and optional facts.</summary>
    private static List<SixEntry> SixEntries()
    {
        List<OldRow> old = OldRows();
        var entries = new List<SixEntry>(320);
        var at = new Dictionary<string, SixEntry>(StringComparer.Ordinal);
        foreach (TextIntent t in SixIntents())
        {
            string role = t.Voice == "stranger" ? StreetCards.Role(t.Key, t.Branch) : null;
            string must = t.Must.Length > 0 ? string.Join("; ", t.Must) : null;
            string group = t.Key + "|" + role + "|" + must + "|" + string.Join("|", t.Optional);
            if (!at.TryGetValue(group, out SixEntry e))
            {
                e = new SixEntry { Key = t.Key, Role = role, MustLine = must, Optional = t.Optional };
                at[group] = e;
                entries.Add(e);
            }
            e.Branches.Add(t);
        }
        foreach (SixEntry e in entries)
        {
            foreach (OldRow o in old)
            {
                if (o.Intent == e.Key)
                {
                    int n = o.History.GetArrayLength();
                    if (!e.Lengths.Contains(n))
                    {
                        e.Lengths.Add(n);
                    }
                }
            }
            e.Lengths.Sort();
            e.History = e.Key.StartsWith("street.", StringComparison.Ordinal) || e.Lengths.Exists(n => n > 0);
            if (!e.History)
            {
                e.Lengths.Clear();
            }
            e.Card.AddRange(SixFaceLines(e.First.Face, e.First.Voice, e.Role));
            e.Face = e.Card.Count;
            e.Card.Add("news: {news}");
            e.Card.Add("facts: {facts}");
            if (e.First.MemoryMost > 0)
            {
                e.Card.Add("memory: {memory}");
            }
            if (e.History)
            {
                e.Card.Add("history: {history}");
            }
            e.Card.Add("write: " + e.First.Write);
            if (e.MustLine != null)
            {
                e.Card.Add("must: " + e.MustLine);
            }
            e.Card.Add("lang: {lang}");
        }
        return entries;
    }

    /// <summary>A face's lines as templates of flat slots, drawn through the mod's own TextFaces with every part a placeholder.</summary>
    private static List<string> SixFaceLines(TextFace face, string voice, string role)
    {
        string name;
        switch (face)
        {
            case TextFace.Friend:
            case TextFace.Employee: name = "{first} {surname}"; break;
            case TextFace.InLaws: name = "{name}"; break;
            case TextFace.Boss: name = "{boss}"; break;
            default: name = "{self}"; break;
        }
        var w = new TextWho
        {
            Name = name, First = "{first}", Age = AgeMark, Job = face == TextFace.Boss ? "{employer}" : face == TextFace.Tycoon ? "{company}" : "{job}",
            District = "{home}", Handle = "{handle}", Role = role ?? "{role}", Title = "{title}", Mood = "{mood}", Style = "{style}", Player = "{player}",
            When = "{when}", Works = "{works}"
        };
        w.Traits.Add("{traits}");
        var s = new TextSheet();
        TextFaces.Fill(face, w, s);
        var lines = new List<string>(7);
        void Line(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                lines.Add(key + ": " + value.Replace(AgeMark.ToString(CultureInfo.InvariantCulture), "{age}"));
            }
        }
        Line("who", s.Who);
        Line("role", s.Role);
        Line("traits", s.Traits.Count > 0 ? string.Join("; ", s.Traits) : null);
        Line("mood", s.Mood);
        Line("style", s.Style);
        Line("player", s.Player);
        Line("when", s.When);
        return lines;
    }

    // ---------------------------------------------------------------- the fixtures

    /// <summary>One fixture for each card of data v6 and each of the seven languages.</summary>
    private static List<SixRow> SixFixtures(List<SixEntry> entries)
    {
        List<OldRow> old = OldRows();
        Dictionary<string, List<string>> pools = SixPools(old);
        var rows = new List<SixRow>(3000);
        foreach (SixEntry e in entries)
        {
            foreach (TextIntent t in e.Branches)
            {
                TextIntent before = TextIntents.Find(t.Key, t.Branch, TextIntents.DataV5);
                List<OldRow> source = before != null ? old.FindAll(o => o.Intent == t.Key && o.Branch == t.Branch && o.Data == before.Data) : null;
                if (before != null && source.Count == 0)
                {
                    throw new InvalidOperationException("texts spec: no card of the generator's for " + t.Key + " " + t.Branch + " of data " + before.Data);
                }
                for (int l = 0; l < TextLanguages.All.Length; l++)
                {
                    SixRow row = SixFixture(t, TextLanguages.All[l], l, source, old, pools);
                    row.Entry = e;
                    rows.Add(row);
                }
            }
        }
        // (the table's order, as the pipeline lists the pairs)
        var order = new Dictionary<TextIntent, int>();
        List<TextIntent> six = SixIntents();
        for (int i = 0; i < six.Count; i++)
        {
            order[six[i]] = i;
        }
        var sorted = new List<SixRow>(rows.Count);
        var by = new List<SixRow>[six.Count];
        foreach (SixRow r in rows)
        {
            int i = order[r.Intent];
            (by[i] ?? (by[i] = new List<SixRow>(7))).Add(r);
        }
        foreach (List<SixRow> list in by)
        {
            sorted.AddRange(list);
        }
        return sorted;
    }

    // The generator's cards, each with the data version of the intent it is (a row of data 5 says so; the rest are their intent's first).
    private static List<OldRow> OldRows()
    {
        var rows = new List<OldRow>(1500);
        foreach (string line in File.ReadAllLines(System.IO.Path.Combine(TextsBuildFolder(), "EconomyCheck", "TextsCards.jsonl"), Encoding.UTF8))
        {
            if (line.Length == 0)
            {
                continue;
            }
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement r = doc.RootElement;
            string key = r.GetProperty("intent").GetString();
            if (key == "_prompt")
            {
                continue;
            }
            string branch = r.GetProperty("branch").ValueKind == JsonValueKind.Null ? null : r.GetProperty("branch").GetString();
            int data = r.TryGetProperty("data", out JsonElement d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : 0;
            TextIntent intent = data > 0 ? TextIntents.Find(key, branch, data) : TextIntents.Find(key, branch);
            if (intent == null)
            {
                continue;
            }
            rows.Add(new OldRow
            {
                Intent = key, Branch = branch, Data = intent.Data, Who = r.GetProperty("who").Clone(), Slots = r.GetProperty("slots").Clone(),
                History = r.GetProperty("history").Clone()
            });
        }
        return rows;
    }

    // Every value the generator gave each slot, sorted: the new cards draw their names and places from them.
    private static Dictionary<string, List<string>> SixPools(List<OldRow> old)
    {
        var sets = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (OldRow o in old)
        {
            foreach (JsonProperty p in o.Slots.EnumerateObject())
            {
                if (!sets.TryGetValue(p.Name, out SortedSet<string> set))
                {
                    sets[p.Name] = set = new SortedSet<string>(StringComparer.Ordinal);
                }
                set.Add(p.Value.GetString());
            }
        }
        var pools = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, SortedSet<string>> s in sets)
        {
            pools[s.Key] = new List<string>(s.Value);
        }
        return pools;
    }

    // A seed of a card, a language and a name: the same every time.
    private static int SixSeed(TextIntent t, string lang, string what)
    {
        return (int)(TextsCityRules.Hash(TextIntents.DataV6, t.Key + "#" + t.Branch + "#" + lang + "#" + what, 6) & 0x7fffffff);
    }

    private static string SixPick(IReadOnlyList<string> list, int seed) => list[seed % list.Count];

    private static SixRow SixFixture(TextIntent t, string lang, int l, List<OldRow> source, List<OldRow> old, Dictionary<string, List<string>> pools)
    {
        var row = new SixRow { Intent = t, Lang = lang };
        int s = SixSeed(t, lang, "card");
        if (source != null)
        {
            OldRow o = source[l % source.Count];
            SixReadWho(o.Who, row.Who);
            foreach (JsonProperty p in o.Slots.EnumerateObject())
            {
                row.Slots.Set(p.Name, p.Value.GetString());
            }
            foreach (JsonElement h in o.History.EnumerateArray())
            {
                row.History.Add(new TextSaid(TextIntents.Plain(h[0].GetString()), TextIntents.Plain(h[1].GetString())));
            }
            if (t.UntoldName && row.Slots.Get("known") == null)
            {
                row.Slots.Set("known", SixKnown(t, s));   // (data v6: always said)
            }
        }
        else
        {
            SixNewWho(t, s, old, row.Who);
            SixNewSlots(t, lang, pools, row.Who, row.Slots);
        }
        SixLocalise(lang, row.Who, row.Slots);
        var sheet = new TextSheet();
        if (!TextIntents.Build(t, row.Who, row.Slots, row.History, sheet))
        {
            throw new InvalidOperationException("texts spec: the card " + t.Key + " " + t.Branch + " (" + lang + ") could not be built from its fixture's values");
        }
        List<TextFact> facts = t.MemoryMost > 0 && SixSeed(t, lang, "memory") % 3 != 0 && !SixFirstMeeting(t) ? SixFacts(t, s, row.Slots, pools) : null;
        TextsHub.Finish(t, sheet, lang, facts, SixToday, false);
        row.Memory.AddRange(sheet.Memory);
        row.Card = TextCard.Card(sheet);
        return row;
    }

    // Whether a stranger knows the player's name on a card of street talk: on the branches where they always do, else now and then.
    private static string SixKnown(TextIntent t, int s)
    {
        bool always = (t.Key == "street.greet" && (t.Branch == "famous" || t.Branch == "infamous")) || t.Key == "street.name";
        return always || s % 3 == 0 ? "yes" : "no";
    }

    // The cards of street talk where the stranger has only just met the player: nothing remembered yet.
    private static bool SixFirstMeeting(TextIntent t) => t.Key == "street.greet" && (t.Branch == "stranger" || t.Branch == "famous" || t.Branch == "infamous");

    // The names of the districts as the game shows them in this language (Chinese and Russian have their own).
    private static void SixLocalise(string lang, TextWho who, TextSlots slots)
    {
        int column = lang == "zh-cn" ? 1 : lang == "ru" ? 2 : 0;
        if (column == 0)
        {
            return;
        }
        who.District = SixDistrict(who.District, column);
        foreach (string name in new[] { "district", "home" })
        {
            string v = slots.Get(name);
            if (v != null)
            {
                slots.Set(name, SixDistrict(v, column));
            }
        }
    }

    private static string SixDistrict(string english, int column)
    {
        foreach (string[] d in SixDistricts)
        {
            if (d[0] == english)
            {
                return d[column];
            }
        }
        return english;
    }

    /// <summary>A speaker of a fixture (the generator's own field names: TextsCards.jsonl's "who").</summary>
    private static void SixReadWho(JsonElement w, TextWho who)
    {
        who.Clear();
        who.Name = Str(w, "Name");
        who.First = Str(w, "First");
        who.Age = w.TryGetProperty("Age", out JsonElement age) && age.ValueKind == JsonValueKind.Number ? age.GetInt32() : 0;
        who.Job = Str(w, "Job");
        who.District = Str(w, "District");
        who.Handle = Str(w, "Handle");
        who.Role = Str(w, "Role");
        who.Title = Str(w, "Title");
        who.Mood = Str(w, "Mood");
        who.Style = Str(w, "Style");
        who.Player = Str(w, "Player");
        who.When = Str(w, "When");
        who.Works = Str(w, "Works");
        if (w.TryGetProperty("Traits", out JsonElement traits))
        {
            foreach (JsonElement t in traits.EnumerateArray())
            {
                who.Traits.Add(t.GetString());
            }
        }
    }

    // The speaker of a new card: one of the generator's own people of the same voice, or one of the player's staff drawn here.
    private static void SixNewWho(TextIntent t, int s, List<OldRow> old, TextWho who)
    {
        if (t.Face == TextFace.Employee)
        {
            var people = old.FindAll(o => o.Who.TryGetProperty("Name", out _) && o.Who.TryGetProperty("Job", out _) && TextIntents.Find(o.Intent, o.Branch).Voice == "friend");
            SixReadWho(people[s % people.Count].Who, who);
            string name = who.Name, first = who.First, player = who.Player, when = who.When;
            who.Clear();
            who.Name = name;
            who.First = first;
            who.Job = TextWords.StaffJob(SixSkills[(s / 7) % SixSkills.Length]);
            who.Style = TextWords.StaffStyle(s);
            who.Player = player;
            who.When = when;
            return;   // (where they work: the shop slot's business, SixNewSlots)
        }
        var donors = old.FindAll(o => TextIntents.Find(o.Intent, o.Branch).Voice == t.Voice && TextIntents.Find(o.Intent, o.Branch).Face == t.Face
                                      && (t.Key != "citizen.review" || o.Intent == "citizen.review"));
        if (donors.Count == 0)
        {
            throw new InvalidOperationException("texts spec: nobody of the voice " + t.Voice + " among the generator's cards for " + t.Key);
        }
        SixReadWho(donors[s % donors.Count].Who, who);
    }

    // The values of a new card's slots, as the game gives them.
    private static void SixNewSlots(TextIntent t, string lang, Dictionary<string, List<string>> pools, TextWho who, TextSlots slots)
    {
        int Seed(string what) => SixSeed(t, lang, what);
        bool Has(string what) => Seed("has:" + what) % 2 == 0;
        string Money(long whole) => "$" + whole.ToString("#,0", CultureInfo.InvariantCulture);
        string Wage(int hundredths) => "$" + (hundredths / 100).ToString(CultureInfo.InvariantCulture) + "." + (hundredths % 100).ToString("00", CultureInfo.InvariantCulture);
        if (t.Voice == "employee")
        {
            string shop = SixPick(pools["shop"], Seed("shop"));
            who.Works = shop;
            slots.Set("shop", shop);
            int paid = 1400 + 25 * (Seed("paid") % 40);
            switch (t.Key)
            {
                case "employee.raise_ask":
                    int asked = paid + 100 + 25 * (Seed("asked") % 8);
                    slots.Set("paid", Wage(paid)).Set("asked", Wage(asked)).Set("going", Wage(asked + 25 * (Seed("going") % 6)));
                    break;
                case "employee.raise_thanks":
                    slots.Set("paid", Wage(paid + 150));
                    break;
                case "employee.hours":
                    int most = 30 + Seed("most") % 11;
                    slots.Set("hours", (most + 3 + Seed("hours") % 12).ToString(CultureInfo.InvariantCulture)).Set("most", most.ToString(CultureInfo.InvariantCulture));
                    break;
            }
            return;
        }
        if (t.Key == "citizen.review")
        {
            int stars = 1 + Seed("stars") % 5;
            int service = Seed("service") % 101, pricing = Seed("pricing") % 101, cleanliness = Seed("cleanliness") % 101, interior = Seed("interior") % 101;
            slots.Set("shop", SixPick(pools["shop"], Seed("shop"))).Set("stars", stars.ToString(CultureInfo.InvariantCulture))
                .Set("service", TextWords.Rating(service)).Set("prices", TextWords.Rating(pricing)).Set("cleanliness", TextWords.Rating(cleanliness))
                .Set("interior", TextWords.Rating(interior)).Set("about", TextsCityRules.ReviewAbout(stars, service, pricing, cleanliness, interior));
            if (Has("crowd"))
            {
                slots.Set("crowd", Seed("crowd") % 2 == 0 ? "busy" : "quiet");
            }
            return;
        }
        foreach (string slot in SixSlotNames(t))
        {
            bool optional = SixOptionalOf(t, slot) != null;
            if (slot == "first" || slot == "player" || slot == "self" || (optional && !Has(slot)))
            {
                continue;   // (the speaker's own; an optional fact the game has or has not)
            }
            switch (slot)
            {
                case "street": slots.Set(slot, SixPick(pools["street"], Seed(slot))); break;
                case "shop": slots.Set(slot, SixPick(pools["shop"], Seed(slot))); break;
                case "company": slots.Set(slot, SixPick(SixListed, Seed(slot))); break;
                case "degree": slots.Set(slot, SixPick(SixDegrees, Seed(slot))); break;
                case "name": slots.Set(slot, SixPick(pools["name"], Seed(slot))); break;
                case "child": slots.Set(slot, SixPick(pools["child"], Seed(slot))); break;
                case "amount": slots.Set(slot, Money(5000L * (1 + Seed(slot) % 90))); break;
                case "other":
                    string other = SixPick(pools["first"], Seed(slot));
                    slots.Set(slot, other == who.First ? SixPick(pools["first"], Seed(slot) + 1) : other);
                    break;
                default:
                    throw new InvalidOperationException("texts spec: no value for the slot " + slot + " of " + t.Key);
            }
        }
    }

    // Things the speaker of a card might remember of the player, of the kinds their voice keeps, as the game records them.
    private static List<TextFact> SixFacts(TextIntent t, int s, TextSlots slots, Dictionary<string, List<string>> pools)
    {
        var f = new List<TextFact>(8);
        int d = SixToday;
        string shop = slots.Get("shop") ?? SixPick(pools["shop"], s / 3);
        string place = slots.Get("place") ?? SixPick(pools["place"], s / 5);
        switch (t.Voice)
        {
            case "stranger":
                f.Add(new TextFact(TextMemoryKinds.Met, "street", d));
                f.Add(new TextFact(TextMemoryKinds.Talked, "street", d, SixPick(SixStreetTopics(), s / 7)));
                if (s % 2 == 0)
                {
                    f.Add(new TextFact(TextMemoryKinds.Tone, s % 4 == 0 ? "warm" : "cool", d));
                }
                if (s % 3 == 0)
                {
                    f.Add(new TextFact(TextMemoryKinds.Favour, "directions", d, place));
                }
                if (slots.Get("known") == "yes")
                {
                    f.Add(new TextFact(TextMemoryKinds.Knows, "name", d));
                }
                break;
            case "employee":
                f.Add(new TextFact(TextMemoryKinds.Met, "staff", d - 40 - s % 300, shop));
                f.Add(new TextFact(TextMemoryKinds.Knows, "name", d - 40 - s % 300));
                if (s % 2 == 0 && t.Key != "employee.raise_thanks")
                {
                    f.Add(new TextFact(TextMemoryKinds.Favour, "raise", d - 10 - s % 90, "$" + (15 + s % 6).ToString(CultureInfo.InvariantCulture) + ".50"));
                }
                break;
            case "mom":
                f.Add(new TextFact(TextMemoryKinds.Talked, s % 2 == 0 ? "call" : "text", d - 1 - s % 6, s % 2 == 0 ? null : "keeping in touch"));
                f.Add(new TextFact(TextMemoryKinds.Knows, "business", d - 200, shop));
                if (s % 3 != 0)
                {
                    f.Add(new TextFact(TextMemoryKinds.Event, s % 2 == 0 ? "opened" : "graduated", d - 3 - s % 20, s % 2 == 0 ? shop : null));
                }
                break;
            case "inlaws":
                f.Add(new TextFact(TextMemoryKinds.Talked, "call", d - 5 - s % 20));
                f.Add(new TextFact(TextMemoryKinds.Event, "married", d - 30 - s % 50));
                break;
            case "boss":
                f.Add(new TextFact(TextMemoryKinds.Met, "work", d - 60 - s % 300));
                f.Add(new TextFact(TextMemoryKinds.Talked, "text", d - 1 - s % 9, "work"));
                f.Add(new TextFact(TextMemoryKinds.Knows, "job", d - 60, SixPick(SixTitles, s / 3)));
                break;
            case "lender":
                f.Add(new TextFact(TextMemoryKinds.Talked, "call", d - 2 - s % 9));
                f.Add(new TextFact(TextMemoryKinds.Favour, "owed", d - 9 - s % 20, "$" + (500 * (1 + s % 12)).ToString("#,0", CultureInfo.InvariantCulture)));
                break;
            case "citizen":
                f.Add(new TextFact(TextMemoryKinds.Met, "dm", d - 20 - s % 40));
                f.Add(new TextFact(TextMemoryKinds.Talked, "dm", d - 1 - s % 6));
                f.Add(new TextFact(TextMemoryKinds.Tone, s % 2 == 0 ? "warm" : "flirty", d - 1 - s % 6));
                if (s % 3 == 0)
                {
                    f.Add(new TextFact(TextMemoryKinds.Favour, "invite", d - 2 - s % 5));
                }
                if (s % 4 == 0)
                {
                    f.Add(new TextFact(TextMemoryKinds.Knows, "business", d - 20, shop));
                }
                break;
            default:   // a friend, the partner, the roommate
                string[] met = { "work", "gym", "coffee", "neighbour", "old_friend", "flare", "street", "customer" };
                f.Add(new TextFact(TextMemoryKinds.Met, met[s % met.Length], d - 50 - s % 600));
                f.Add(new TextFact(TextMemoryKinds.Talked, "outing", d - 2 - s % 25, SixPick(TextMemoryTopics.Outings, s / 3)));
                f.Add(new TextFact(TextMemoryKinds.Knows, "name", d - 50 - s % 600));
                if (s % 2 == 0)
                {
                    f.Add(new TextFact(TextMemoryKinds.Knows, "business", d - 50, shop));
                }
                if (s % 3 == 0)
                {
                    string[] tone = { "stood_up", "late", "asked_out", "rude" };
                    f.Add(new TextFact(TextMemoryKinds.Tone, tone[(s / 3) % tone.Length], d - 1 - s % 20));
                }
                if (s % 5 == 0)
                {
                    f.Add(new TextFact(TextMemoryKinds.Favour, s % 2 == 0 ? "lent" : "lift", d - 4 - s % 60));
                }
                if (s % 4 == 1)
                {
                    string[] events = { "crash", "opened", "graduated", "arrest", "win" };
                    string e = events[(s / 4) % events.Length];
                    f.Add(new TextFact(TextMemoryKinds.Event, e, d - 1 - s % 12, e == "opened" ? shop : null, s % 8 == 1 ? SixPick(pools["first"], s / 11) : null));
                }
                break;
        }
        return f;
    }

    // What street talk's lines are remembered as being about, each once, in the lines' order.
    private static List<string> SixStreetTopics()
    {
        var topics = new List<string>();
        for (int i = 0; i < (int)StreetLine.Count; i++)
        {
            string topic = TextMemoryTopics.Street((StreetLine)i);
            if (topic != null && !topics.Contains(topic))
            {
                topics.Add(topic);
            }
        }
        return topics;
    }

    // What a text is remembered as being about, each once, sorted.
    private static List<string> SixTextTopics()
    {
        var texts = new SortedSet<string>(StringComparer.Ordinal);
        foreach (TextIntent t in TextIntents.All)
        {
            string topic = TextMemoryTopics.Text(t.Key);
            if (topic != null)
            {
                texts.Add(topic);
            }
        }
        return new List<string>(texts);
    }

    // Every slot a card's intent lines name (news, facts, optional facts, must), in order of first mention.
    private static List<string> SixSlotNames(TextIntent t)
    {
        var names = new List<string>(8);
        SixScan(t.News, names);
        foreach (string line in t.Facts)
        {
            SixScan(line, names);
        }
        foreach (string line in t.Optional)
        {
            SixScan(line, names);
        }
        foreach (string line in t.Must)
        {
            SixScan(line, names);
        }
        return names;
    }

    private static void SixScan(string line, List<string> into)
    {
        for (int i = 0; line != null && i < line.Length; i++)
        {
            int close = line[i] == '{' ? line.IndexOf('}', i + 1) : -1;
            if (close > i)
            {
                string n = line.Substring(i + 1, close - i - 1);
                if (!into.Contains(n))
                {
                    into.Add(n);
                }
                i = close;
            }
        }
    }

    // The optional fact that names a slot, or null.
    private static string SixOptionalOf(TextIntent t, string slot)
    {
        foreach (string o in t.Optional)
        {
            if (o.IndexOf("{" + slot + "}", StringComparison.Ordinal) >= 0)
            {
                return o;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- a fixture's flat slots

    /// <summary>
    /// Every slot a fixture's card names, as the pipeline reads it: the speaker's parts by the names the card's lines use
    /// (first, surname, age, job, home, handle, role, title, mood, style, player, when, works, traits, name, boss, employer,
    /// self, company), the game's own slots, and the history and memory; "first", "self", "player" and "company" as the
    /// mod's builder puts them in. A name that would carry two values is an error of the contract's naming.
    /// </summary>
    private static List<KeyValuePair<string, object>> SixFlat(SixRow r)
    {
        var flat = new List<KeyValuePair<string, object>>(24);
        var at = new Dictionary<string, int>(StringComparer.Ordinal);
        void Put(string name, object value, bool fromWho)
        {
            if (at.TryGetValue(name, out int i))
            {
                if (!SixSame(flat[i].Value, value))
                {
                    if (fromWho)
                    {
                        throw new InvalidOperationException("texts spec: the slot " + name + " of " + r.Intent.Key + " " + r.Intent.Branch + " (" + r.Lang + ") is both "
                                                            + SixShow(flat[i].Value) + " and " + SixShow(value));
                    }
                    flat[i] = new KeyValuePair<string, object>(name, value);
                }
                return;
            }
            at[name] = flat.Count;
            flat.Add(new KeyValuePair<string, object>(name, value));
        }
        TextWho w = r.Who;
        var used = new List<string>();
        for (int i = 0; i < r.Entry.Face; i++)
        {
            SixScan(r.Entry.Card[i], used);   // (the speaker's lines: the rest name the game's own slots)
        }
        foreach (string name in used)
        {
            switch (name)
            {
                case "first": Put(name, w.First, true); break;
                case "surname":
                    if (w.Name == null || w.First == null || !w.Name.StartsWith(w.First + " ", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("texts spec: " + w.Name + " is not " + w.First + " and a surname (" + r.Intent.Key + ")");
                    }
                    Put(name, w.Name.Substring(w.First.Length + 1), true);
                    break;
                case "age": Put(name, w.Age.ToString(CultureInfo.InvariantCulture), true); break;
                case "job": Put(name, w.Job, true); break;
                case "home": Put(name, w.District, true); break;
                case "handle": Put(name, w.Handle, true); break;
                case "role": Put(name, w.Role, true); break;
                case "title": Put(name, w.Title, true); break;
                case "mood": Put(name, w.Mood, true); break;
                case "style": Put(name, w.Style, true); break;
                case "player": Put(name, w.Player, true); break;
                case "when": Put(name, w.When, true); break;
                case "works": Put(name, w.Works, true); break;
                case "traits": Put(name, new List<string>(w.Traits), true); break;
                case "name":
                case "boss":
                case "self": Put(name, w.Name, true); break;
                case "employer":
                case "company": Put(name, w.Job, true); break;
            }
        }
        var named = SixSlotNames(r.Intent);
        for (int i = 0; i < r.Slots.Count; i++)
        {
            string name = r.Slots.NameAt(i);
            if (!at.ContainsKey(name))
            {
                Put(name, r.Slots.ValueAt(i), false);
            }
            else if (!SixSame(flat[at[name]].Value, r.Slots.ValueAt(i)) && named.Contains(name) && name != "first" && name != "self")
            {
                throw new InvalidOperationException("texts spec: the slot " + name + " of " + r.Intent.Key + " " + r.Intent.Branch + " is the speaker's "
                                                    + SixShow(flat[at[name]].Value) + " and the card's " + r.Slots.ValueAt(i));
            }
        }
        // the four the builder fills from the speaker (TextIntents.Put)
        if (named.Contains("first"))
        {
            Put("first", w.First, true);
        }
        if (named.Contains("self"))
        {
            Put("self", w.Name, true);
        }
        if (named.Contains("player"))
        {
            Put("player", r.Slots.Get("player") ?? w.Player, true);
        }
        if (named.Contains("company"))
        {
            Put("company", r.Slots.Get("company") ?? w.Job, true);
        }
        if (r.Entry.History)
        {
            var pairs = new List<string[]>(r.History.Count);
            foreach (TextSaid h in r.History)
            {
                pairs.Add(new[] { h.Who, h.Said });
            }
            Put("history", pairs, true);
        }
        else if (r.History.Count > 0)
        {
            throw new InvalidOperationException("texts spec: " + r.Intent.Key + " has a history but its entry has no history line");
        }
        if (r.Intent.MemoryMost > 0)
        {
            Put("memory", new List<string>(r.Memory), true);
        }
        return flat;
    }

    private static bool SixSame(object a, object b)
    {
        if (a is string x && b is string y)
        {
            return x == y;
        }
        return SixShow(a) == SixShow(b);
    }

    private static string SixShow(object v)
    {
        switch (v)
        {
            case null: return "(none)";
            case string s: return s;
            case List<string> list: return "[" + string.Join("; ", list) + "]";
            case List<string[]> pairs:
                var b = new StringBuilder();
                foreach (string[] p in pairs)
                {
                    b.Append('[').Append(p[0]).Append(": ").Append(p[1]).Append(']');
                }
                return b.ToString();
            default: return v.ToString();
        }
    }

    // ---------------------------------------------------------------- the fixtures' lines

    private static readonly JsonWriterOptions SixCompact = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonWriterOptions SixIndented = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = true, NewLine = "\n" };

    /// <summary>The fixtures as JSON lines, one a card, each ended by a line feed.</summary>
    private static string SixLines(List<SixRow> rows)
    {
        var b = new StringBuilder(rows.Count * 1400);
        foreach (SixRow r in rows)
        {
            b.Append(SixLine(r)).Append('\n');
        }
        return b.ToString();
    }

    private static string SixLine(SixRow r)
    {
        TextIntent t = r.Intent;
        TextIntent before = TextIntents.Find(t.Key, t.Branch, TextIntents.DataV5);
        using var stream = new MemoryStream();
        using (var j = new Utf8JsonWriter(stream, SixCompact))
        {
            j.WriteStartObject();
            j.WriteString("intent", t.Key);
            SixNullable(j, "branch", t.Branch);
            j.WriteString("lang", r.Lang);
            j.WriteStartObject("slots");
            foreach (KeyValuePair<string, object> p in SixFlat(r))
            {
                j.WritePropertyName(p.Key);
                SixValue(j, p.Value);
            }
            j.WriteEndObject();
            j.WriteString("card", r.Card);
            j.WriteNumber("data", t.Data);
            if (before != null)
            {
                j.WriteNumber("from_data", before.Data);
            }
            j.WriteStartObject("who");
            SixWriteWho(j, r.Who);
            j.WriteEndObject();
            j.WriteNumber("max_chars", TextLanguages.MostUnits(t.MaxLen, r.Lang));
            j.WriteNumber("max_tokens", TextDecoding.Tokens(t, r.Lang));
            j.WriteString("grammar", TextDecoding.GrammarName(r.Lang, TextDecoding.Lowercase(r.Who.Style)));
            j.WriteBoolean("untold_name", t.UntoldName && r.Slots.Get("known") != "yes");
            j.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void SixValue(Utf8JsonWriter j, object v)
    {
        switch (v)
        {
            case null: j.WriteNullValue(); break;
            case string s: j.WriteStringValue(s); break;
            case List<string> list:
                j.WriteStartArray();
                foreach (string s in list)
                {
                    j.WriteStringValue(s);
                }
                j.WriteEndArray();
                break;
            case List<string[]> pairs:
                j.WriteStartArray();
                foreach (string[] p in pairs)
                {
                    j.WriteStartArray();
                    j.WriteStringValue(p[0]);
                    j.WriteStringValue(p[1]);
                    j.WriteEndArray();
                }
                j.WriteEndArray();
                break;
            default: throw new InvalidOperationException("texts spec: a slot of " + v.GetType().Name);
        }
    }

    private static void SixNullable(Utf8JsonWriter j, string name, string value)
    {
        if (value == null)
        {
            j.WriteNull(name);
        }
        else
        {
            j.WriteString(name, value);
        }
    }

    // A speaker in the generator's field names (TextWho's), only the parts they have (Traits always): the mod's builder reads them back.
    private static void SixWriteWho(Utf8JsonWriter j, TextWho w)
    {
        void Part(string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                j.WriteString(name, value);
            }
        }
        Part("Name", w.Name);
        Part("First", w.First);
        if (w.Age > 0)
        {
            j.WriteNumber("Age", w.Age);
        }
        Part("Job", w.Job);
        Part("District", w.District);
        Part("Handle", w.Handle);
        Part("Role", w.Role);
        Part("Title", w.Title);
        Part("Mood", w.Mood);
        Part("Style", w.Style);
        Part("Player", w.Player);
        Part("When", w.When);
        Part("Works", w.Works);
        j.WriteStartArray("Traits");
        foreach (string trait in w.Traits)
        {
            j.WriteStringValue(trait);
        }
        j.WriteEndArray();
    }

    // ---------------------------------------------------------------- the spec

    private static string SixSpec(List<SixEntry> entries, List<SixRow> rows)
    {
        using var stream = new MemoryStream();
        using (var j = new Utf8JsonWriter(stream, SixIndented))
        {
            j.WriteStartObject();
            j.WriteNumber("version", TextIntents.DataV6);
            SixStrings(j, "languages", TextLanguages.All);
            j.WriteStartObject("language_names");
            string[] names = { "English", "Portuguese (Brazil)", "Chinese (Simplified)", "German", "French", "Spanish", "Russian" };
            for (int i = 0; i < TextLanguages.All.Length; i++)
            {
                j.WriteString(TextLanguages.All[i], names[i]);
            }
            j.WriteEndObject();
            j.WriteStartObject("counts");
            j.WriteNumber("intents", SixKeys().Count);
            j.WriteNumber("entries", entries.Count);
            j.WriteNumber("pairs", SixIntents().Count);
            j.WriteNumber("new_pairs", SixIntents().FindAll(t => TextIntents.Find(t.Key, t.Branch, TextIntents.DataV5) == null).Count);
            j.WriteNumber("languages", TextLanguages.All.Length);
            j.WriteNumber("fixtures", rows.Count);
            j.WriteEndObject();
            j.WriteString("system", TextCard.SystemSix);
            j.WriteString("prompt", "<|im_start|>system\n{system}<|im_end|>\n<|im_start|>user\n{card}<|im_end|>\n<|im_start|>assistant\n");
            j.WriteString("stop", TextCard.Stop);
            SixStrings(j, "card_rules", SixCardRules);
            SixNames(j);
            SixLangRules(j);
            SixDecodingTop(j, rows);
            SixMemoryTop(j);
            j.WriteStartArray("intents");
            foreach (SixEntry e in entries)
            {
                SixEntryJson(j, e, rows);
            }
            j.WriteEndArray();
            SixStrings(j, "fixture_fields", SixFixtureFields);
            SixStrings(j, "changes", SixChanges);
            j.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static readonly string[] SixCardRules =
    {
        "Cards render exactly as CONTRACT.md says (spec_tools.render); the mod's own builder agrees with it on every fixture (EconomyCheck, TextsSixChecks.cs).",
        "The lang line is the language the reply is written in, one of 'languages'. The card's own words are English in every language; a name (a person, a business, a street, a district, a company, a degree) is as the game shows it in that language (names_by_lang).",
        "A sum, a figure or a time is written as the game writes it ('$1,250', '$15.50', '19:30') in every language, and a reply copies it exactly.",
        "memory: the speaker's memory lines, joined by '; ' on one line, after the facts (memory.max at most).",
        "A stranger's card always says 'you know their name: yes' or 'no' (a fact of every street branch); with 'no' the request keeps the name out (decoding.untold_name).",
        "The speaker's parts: first and surname make the full name; home is where they live; employer a manager's company; self the speaker's own name (an account, a business, a rival); company a rival's company on their who line."
    };

    private static readonly string[] SixFixtureFields =
    {
        "intent, branch (null for an intent's only card), lang, slots, card: as CONTRACT.md says; slots holds every slot the card names, history and memory among them",
        "data (6) and from_data (the data version whose card this one carries on; absent for a new card)",
        "who: the speaker in the mod's own names (TextWho: Name, First, Age, Job, District, Handle, Role, Title, Mood, Style, Player, When, Works, Traits), which the mod's builder reads back",
        "max_chars: the reply's most units in its language; max_tokens: the request's n_predict; grammar: the request's grammar by name (decoding.grammars); untold_name: true when the request keeps the player's name out"
    };

    private static readonly string[] SixChanges =
    {
        "Every addition is a new key; nothing of CONTRACT.md's shape is renamed, removed or read differently.",
        "Top: language_names, counts, system, prompt, stop, card_rules, names_by_lang, lang_rules, decoding (the grammars), memory (the line templates, rules and lifetimes), fixture_fields, changes.",
        "An entry: data, from_data, face, shape (spoken, text, dm, post, comment, review, paper), use, write, max_chars_by_lang, must_en (must slots checked in an English reply only).",
        "A branch: subject (the event of the player's its news tells; a memory of it is never on its card), beside news, facts and lean.",
        "A slot: lang_rule (as_written, clock or translated: how the reply carries it in another language), beside kind, values and example.",
        "An entry's decoding: max_tokens_by_lang, grammar_by_lang (a grammar's name) and grammar_lowercase, beside max_tokens, grammar (the text of the cased grammar), bans and untold_name.",
        "A fixture: data, from_data, who, max_chars, max_tokens, grammar and untold_name, beside intent, branch, lang, slots and card.",
        "memory.kinds are line templates whose slots are memory_ago, memory_from and a value slot per code (memory_business, memory_place, memory_sum, memory_wage, memory_job, memory_company, memory_street_topic, memory_text_topic, memory_outing); every one is in the entry's slots."
    };

    private static void SixStrings(Utf8JsonWriter j, string name, IEnumerable<string> values)
    {
        j.WriteStartArray(name);
        foreach (string v in values)
        {
            j.WriteStringValue(v);
        }
        j.WriteEndArray();
    }

    /// <summary>The contract's name of a face.</summary>
    private static string SixFaceName(TextFace f)
    {
        switch (f)
        {
            case TextFace.CitizenDm: return "citizen_dm";
            case TextFace.CitizenDmMood: return "citizen_dm_mood";
            case TextFace.InLaws: return "inlaws";
            case TextFace.OfficialAnswer: return "official_answer";
            default: return f.ToString().ToLowerInvariant();
        }
    }

    private static void SixNames(Utf8JsonWriter j)
    {
        j.WriteStartObject("names_by_lang");
        j.WriteStartObject("districts");
        foreach (int column in new[] { 0, 1, 2 })
        {
            var list = new List<string>();
            foreach (string[] d in SixDistricts)
            {
                list.Add(d[column]);
            }
            SixStrings(j, column == 0 ? "en" : column == 1 ? "zh-cn" : "ru", list);
        }
        j.WriteString("others", "Portuguese, German, French and Spanish keep the English names, as the game does.");
        j.WriteEndObject();
        j.WriteString("streets", "A street as the game shows it in the card's language; the fixtures keep the English names.");
        j.WriteString("businesses", "A business's, company's and person's name is the same in every language.");
        j.WriteEndObject();
    }

    private static void SixLangRules(Utf8JsonWriter j)
    {
        j.WriteStartObject("lang_rules");
        j.WriteString("as_written", "A person, place or other name, or a figure or sum: in the reply exactly as the card writes it, in every language; but a place or name of common words, its first letter small ('the park'), is said in the reply's own words in another language.");
        j.WriteString("clock", "A time: in English as the card writes it; in another language its clock ('19:30') exactly and the day in that language.");
        j.WriteString("translated", "English words of the game (a job, a day, a choice, a holiday, a happening): said in the reply's own language; checked as written only in an English reply.");
        j.WriteString("length", "A reply's length is counted in units: one a character, two a wide one (the Chinese, Japanese and Korean characters and the full-width forms). max_chars is English's; Portuguese, German, French, Spanish and Russian have "
                                + TextLanguages.LongerShare + "% of it, rounded down; Chinese the same units, so half the characters (max_chars_by_lang).");
        j.WriteString("capitals", "A sentence starts with a capital in every language but Chinese, unless the style says lowercase.");
        j.WriteString("articles", "'a' and 'an' are checked in English replies only.");
        j.WriteString("script", "A reply is in the script of its language (Latin, Cyrillic for Russian, Chinese characters for Chinese); names may be Latin in any language.");
        j.WriteEndObject();
    }

    private static void SixDecodingTop(Utf8JsonWriter j, List<SixRow> rows)
    {
        j.WriteStartObject("decoding");
        j.WriteNumber("temperature", TextJson.Temperature);
        j.WriteNumber("top_p", TextJson.TopP);
        j.WriteString("max_tokens", "n_predict from the entry's most units in the language: English units / 3, Russian units / 2.2, Chinese units / 2, the rest units / 2.6, rounded down, + "
                                    + TextLanguages.TokenRoom + ". A reply that stops there (stop_type 'limit') is refused as cut, so a reply must end well inside it.");
        j.WriteStartObject("grammars");
        foreach (string name in TextDecoding.Names)
        {
            j.WriteString(name, TextDecoding.Grammar(name));
        }
        j.WriteEndObject();
        j.WriteString("grammar_rule", "'" + TextDecoding.Uncased + "' for Chinese; '" + TextDecoding.Free + "' for a speaker whose style says lowercase; '" + TextDecoding.Cased
                                      + "' for every other card. All three keep out line breaks and control characters, '!' and its full-width form, every dash, a hyphen but one inside a word, and a space at the start; the cased one asks a capital (or no letter) at the start and after every '. ', '? ' and an ellipsis and a space. The bans need no logit bias: the grammar keeps them out.");
        j.WriteString("untold_name", "When an entry's untold_name is true and its card says 'you know their name: no', the player's name (three letters or more) is kept out: the first token of ' <name>' and of '<name>', from the server's /tokenize, go in logit_bias as [[token,false],...]. The checks refuse a reply that holds the name too.");
        SixRow example = rows.Find(r => r.Lang == TextLanguages.English && r.Intent.UntoldName && r.Slots.Get("known") == "no");
        if (example != null)
        {
            string prompt = TextCard.ChatMl(SixSheet(example), TextCard.SystemSix);
            string grammar = TextDecoding.Grammar(TextDecoding.GrammarName(example.Lang, TextDecoding.Lowercase(example.Who.Style)));
            j.WriteString("request_example", TextJson.Request(prompt, 1234, TextDecoding.Tokens(example.Intent, example.Lang), grammar, TextDecoding.Bias(new List<int> { 32001, 32002 })));
            j.WriteString("request_example_notes", "The body of POST /completion for the first English street card whose stranger does not know the name; the two logit_bias ids stand for the name's tokens.");
        }
        j.WriteEndObject();
    }

    // A fixture's card again, as a sheet (for the example request).
    private static TextSheet SixSheet(SixRow r)
    {
        var sheet = new TextSheet();
        TextIntents.Build(r.Intent, r.Who, r.Slots, r.History, sheet);
        sheet.Lang = r.Lang;
        sheet.Memory.AddRange(r.Memory);
        return sheet;
    }

    // The slot a memory code's {value} goes in; null for a code without one.
    private static string SixMemorySlot(string kind, string code)
    {
        switch (kind + "." + code)
        {
            case "met.staff":
            case "knows.business":
            case "event.opened":
            case "event.breakin": return "memory_business";
            case "talked.street": return "memory_street_topic";
            case "talked.text": return "memory_text_topic";
            case "talked.outing": return "memory_outing";
            case "favour.directions": return "memory_place";
            case "favour.owed":
            case "favour.owes": return "memory_sum";
            case "favour.raise": return "memory_wage";
            case "knows.job": return "memory_job";
            case "event.listed": return "memory_company";
            default: return null;
        }
    }

    /// <summary>The memory lines a speaker of these kinds may have, as templates (TextMemoryRules.Line's shapes).</summary>
    private static List<string> SixMemoryKinds(TextMemoryKinds kinds)
    {
        var lines = new List<string>(80);
        foreach (string[] c in TextMemoryRules.Codes)
        {
            if ((kinds & TextMemoryRules.KindOf(c[0])) == 0)
            {
                continue;
            }
            bool standing = c[2].EndsWith("*", StringComparison.Ordinal);
            string text = standing ? c[2].Substring(0, c[2].Length - 1) : c[2];
            if (text.IndexOf("{value}", StringComparison.Ordinal) >= 0)
            {
                string slot = SixMemorySlot(c[0], c[1]) ?? throw new InvalidOperationException("texts spec: no memory slot for " + c[0] + "." + c[1]);
                text = text.Replace("{value}", "{" + slot + "}");
            }
            string ago = standing ? string.Empty : ", {memory_ago}";
            if (c[0] == "event")
            {
                lines.Add("heard " + text + ago);
                lines.Add("heard from {memory_from} that " + text + ago);
            }
            else
            {
                lines.Add(text + ago);
            }
        }
        return lines;
    }

    // How long ago, every way a memory line says it.
    private static List<string> SixAgos()
    {
        var agos = new List<string>();
        for (int days = 0; days < 70; days++)
        {
            string a = TextMemoryRules.Ago(days);
            if (!agos.Contains(a))
            {
                agos.Add(a);
            }
        }
        return agos;
    }

    private static void SixMemoryTop(Utf8JsonWriter j)
    {
        j.WriteStartObject("memory");
        j.WriteNumber("max", TextMemoryRules.CardMost);
        var kinds = new List<string>();
        for (int bit = 1; bit < (int)TextMemoryKinds.All; bit <<= 1)
        {
            kinds.Add(TextMemoryRules.Name((TextMemoryKinds)bit));
        }
        SixStrings(j, "kinds", kinds);
        j.WriteStartObject("codes");
        string kind = null;
        foreach (string[] c in TextMemoryRules.Codes)
        {
            if (c[0] != kind)
            {
                if (kind != null)
                {
                    j.WriteEndObject();
                }
                kind = c[0];
                j.WriteStartObject(kind);
            }
            j.WriteString(c[1], c[2]);
        }
        j.WriteEndObject();
        j.WriteEndObject();
        SixStrings(j, "line_rules", new[]
        {
            "A memory line is its code's words with the value put in (a code whose words name a value is never on a card without one).",
            "An event is heard: 'heard <words>', or 'heard from <first name> that <words>' when somebody told them.",
            "Each line but a standing one (its words end with '*' in codes; the '*' is not written) ends ', <how long ago>': 'today', 'yesterday', '<n> days ago' (2 to 13), 'a few weeks ago' (14 to 59), 'months ago'.",
            "The game chooses a card's lines, the most relevant first, and puts none on what everybody reads (a post, a comment, the paper, a shared card). The reply may use them and never says more than they say."
        });
        j.WriteStartObject("lifetime_days");
        foreach (string k in kinds)
        {
            j.WriteNumber(k, TextMemoryRules.Lifetime(TextMemoryRules.KindOf(k)));
        }
        j.WriteNumber("stranger", TextMemoryRules.StrangerDays);
        j.WriteEndObject();
        j.WriteEndObject();
    }

    // The lean of a street branch's mood (StreetCards): "high", "low" or none.
    private static string SixLean(TextIntent t)
    {
        if (!t.Key.StartsWith("street.", StringComparison.Ordinal))
        {
            return null;
        }
        for (int i = 0; i < (int)StreetLine.Count; i++)
        {
            foreach (bool warm in new[] { true, false })
            {
                string intent = StreetCards.V5((StreetLine)i, warm, out string branch, out int lean);
                if (intent == t.Key && branch == t.Branch)
                {
                    return lean > 0 ? "high" : lean < 0 ? "low" : null;
                }
            }
        }
        return null;
    }

    private static void SixEntryJson(Utf8JsonWriter j, SixEntry e, List<SixRow> rows)
    {
        TextIntent first = e.First;
        TextIntent before = TextIntents.Find(first.Key, first.Branch, TextIntents.DataV5);
        SixRow sample = rows.Find(r => r.Entry == e && r.Lang == TextLanguages.English);
        List<KeyValuePair<string, object>> flat = SixFlat(sample);
        j.WriteStartObject();
        j.WriteString("name", e.Key);
        j.WriteString("voice", first.Voice);
        j.WriteString("channel", first.Channel);
        j.WriteNumber("max_chars", first.MaxLen);
        j.WriteStartObject("branches");
        foreach (TextIntent t in e.Branches)
        {
            j.WriteStartObject(t.Branch ?? string.Empty);
            j.WriteString("news", t.News);
            SixStrings(j, "facts", t.Facts);
            SixNullable(j, "lean", SixLean(t));
            string subject = TextIntents.Subject(t.Key, t.Branch);
            if (subject != null)
            {
                j.WriteString("subject", subject);
            }
            j.WriteEndObject();
        }
        j.WriteEndObject();
        SixStrings(j, "card", e.Card);
        // the slots: every one the card's lines, the branches, the optional facts and the memory kinds name
        List<string> memoryKinds = first.MemoryMost > 0 ? SixMemoryKinds(first.MemoryKinds) : new List<string>();
        var names = new List<string>();
        foreach (string line in e.Card)
        {
            SixScan(line, names);
        }
        foreach (TextIntent t in e.Branches)
        {
            foreach (string n in SixSlotNames(t))
            {
                if (!names.Contains(n))
                {
                    names.Add(n);
                }
            }
        }
        foreach (string line in memoryKinds)
        {
            SixScan(line, names);
        }
        j.WriteStartObject("slots");
        foreach (string n in names)
        {
            if (n == "news" || n == "facts" || n == "memory" || n == "lang")
            {
                continue;
            }
            if (n == "history")
            {
                j.WriteStartObject(n);
                j.WriteString("kind", "text");
                var lengths = new List<string>();
                foreach (int len in e.Lengths)
                {
                    lengths.Add(len.ToString(CultureInfo.InvariantCulture));
                }
                if (lengths.Count == 0)
                {
                    lengths.Add("1");
                }
                SixStrings(j, "values", lengths);
                SixStrings(j, "example", SixHistoryExample(e, rows));
                j.WriteEndObject();
                continue;
            }
            SixSlotJson(j, n, e, flat);
        }
        j.WriteEndObject();
        SixStrings(j, "optional", e.Optional);
        j.WriteStartObject("memory");
        j.WriteNumber("max", first.MemoryMost);
        SixStrings(j, "kinds", memoryKinds);
        j.WriteEndObject();
        SixStrings(j, "must", SixMust(e));
        SixStrings(j, "never", SixNever(first));
        j.WriteStartObject("decoding");
        j.WriteNumber("max_tokens", TextDecoding.Tokens(first, TextLanguages.English));
        j.WriteString("grammar", TextDecoding.Grammar(TextDecoding.Cased));
        SixStrings(j, "bans", TextDecoding.Bans);
        j.WriteBoolean("untold_name", first.UntoldName);
        j.WriteStartObject("max_tokens_by_lang");
        foreach (string lang in TextLanguages.All)
        {
            j.WriteNumber(lang, TextDecoding.Tokens(first, lang));
        }
        j.WriteEndObject();
        j.WriteStartObject("grammar_by_lang");
        foreach (string lang in TextLanguages.All)
        {
            j.WriteString(lang, TextDecoding.GrammarName(lang, false));
        }
        j.WriteEndObject();
        j.WriteString("grammar_lowercase", TextDecoding.Free);
        j.WriteEndObject();
        j.WriteString("notes", SixNotes(e));
        j.WriteNumber("data", TextIntents.DataV6);
        if (before != null)
        {
            j.WriteNumber("from_data", before.Data);
        }
        j.WriteString("face", SixFaceName(first.Face));
        j.WriteString("shape", first.Shape);
        j.WriteString("use", first.Use.ToString().ToLowerInvariant());
        j.WriteString("write", first.Write);
        j.WriteStartObject("max_chars_by_lang");
        foreach (string lang in TextLanguages.All)
        {
            j.WriteNumber(lang, TextLanguages.MostUnits(first.MaxLen, lang));
        }
        j.WriteEndObject();
        SixStrings(j, "must_en", SixMustEnglish(e));
        j.WriteEndObject();
    }

    // A history's example: the first English fixture's longest thread, each line labelled by the slot of its speaker where it is one.
    private static List<string> SixHistoryExample(SixEntry e, List<SixRow> rows)
    {
        SixRow best = null;
        foreach (SixRow r in rows)
        {
            if (r.Entry == e && r.Lang == TextLanguages.English && (best == null || r.History.Count > best.History.Count))
            {
                best = r;
            }
        }
        var lines = new List<string>();
        if (best == null)
        {
            return lines;
        }
        foreach (TextSaid h in best.History)
        {
            string label = h.Who == best.Who.Player ? "{player}" : h.Who == best.Who.First ? "{first}" : h.Who;
            lines.Add(label + ": " + h.Said);
        }
        return lines;
    }

    // The pipeline's kind of one of the mod's slot kinds, and how a reply in another language carries it.
    private static string SixKind(TextSlotKind kind, out string rule)
    {
        switch (kind)
        {
            case TextSlotKind.Person: rule = "as_written"; return "person";
            case TextSlotKind.Place: rule = "as_written"; return "place";
            case TextSlotKind.Name: rule = "as_written"; return "text";
            case TextSlotKind.Number: rule = "as_written"; return "number";
            case TextSlotKind.Time: rule = "clock"; return "text";
            case TextSlotKind.Day: rule = "translated"; return "choice";
            case TextSlotKind.Job: rule = "translated"; return "job";
            case TextSlotKind.Choice: rule = "translated"; return "choice";
            default: rule = "translated"; return "text";
        }
    }

    private static void SixSlotJson(Utf8JsonWriter j, string n, SixEntry e, List<KeyValuePair<string, object>> flat)
    {
        TextIntent first = e.First;
        string kind, rule;
        string[] values;
        switch (n)
        {
            case "surname": kind = "person"; rule = "as_written"; values = null; break;
            case "age": kind = "number"; rule = "as_written"; values = null; break;
            case "home": kind = "place"; rule = "as_written"; values = SixColumn(0); break;
            case "handle": kind = "text"; rule = "as_written"; values = null; break;
            case "role": kind = "choice"; rule = "translated"; values = SixRoles(first.Voice); break;
            case "title": kind = "job"; rule = "translated"; values = null; break;
            case "mood": kind = "choice"; rule = "translated"; values = TextWords.Moods; break;
            case "style": kind = "choice"; rule = "translated"; values = SixStyles(first); break;
            case "when": kind = "text"; rule = "clock"; values = null; break;
            case "works":
            case "employer": kind = "place"; rule = "as_written"; values = null; break;
            case "traits": kind = "choice"; rule = "translated"; values = TextWords.Traits; break;
            case "boss": kind = "person"; rule = "as_written"; values = null; break;
            case "self":
                kind = first.Face == TextFace.Tycoon ? "person" : "text";
                rule = "as_written";
                values = first.Face == TextFace.Tycoon ? SixTycoons(0) : null;
                break;
            case "memory_ago": kind = "choice"; rule = "translated"; values = SixAgos().ToArray(); break;
            case "memory_from": kind = "person"; rule = "as_written"; values = null; break;
            case "memory_business":
            case "memory_place":
            case "memory_company": kind = "place"; rule = "as_written"; values = null; break;
            case "memory_sum":
            case "memory_wage": kind = "number"; rule = "as_written"; values = null; break;
            case "memory_job": kind = "job"; rule = "translated"; values = null; break;
            case "memory_street_topic": kind = "choice"; rule = "translated"; values = SixStreetTopics().ToArray(); break;
            case "memory_text_topic": kind = "choice"; rule = "translated"; values = SixTextTopics().ToArray(); break;
            case "memory_outing": kind = "choice"; rule = "translated"; values = TextMemoryTopics.Outings; break;
            case "job":
                kind = "job";
                rule = "translated";
                values = first.Face == TextFace.Employee ? SixStaffJobs() : TextWords.Jobs;
                break;
            case "company" when first.Face == TextFace.Tycoon:
                kind = "place";
                rule = "as_written";
                values = SixTycoons(1);
                break;
            default:
                kind = SixKind(TextSlotKinds.Of(n), out rule);
                values = SixValues(n, first);
                break;
        }
        j.WriteStartObject(n);
        j.WriteString("kind", kind);
        if (values == null)
        {
            j.WriteNull("values");
        }
        else
        {
            SixStrings(j, "values", values);
        }
        j.WritePropertyName("example");
        object example = null;
        foreach (KeyValuePair<string, object> p in flat)
        {
            if (p.Key == n)
            {
                example = p.Value;
            }
        }
        if (example == null && n.StartsWith("memory_", StringComparison.Ordinal))
        {
            example = SixMemoryExample(n);
        }
        if (example == null && values != null && values.Length > 0)
        {
            example = values[0];
        }
        SixValue(j, example);
        j.WriteString("lang_rule", rule);
        j.WriteEndObject();
    }

    private static string SixMemoryExample(string slot)
    {
        switch (slot)
        {
            case "memory_ago": return "yesterday";
            case "memory_from": return "Dana";
            case "memory_business": return "Sam's Deli";
            case "memory_place": return "Bean There";
            case "memory_company": return "Hudson Logistics";
            case "memory_sum": return "$300";
            case "memory_wage": return "$15.50";
            case "memory_job": return "Sales Associate";
            case "memory_street_topic": return "their work";
            case "memory_text_topic": return "meeting up";
            case "memory_outing": return "coffee";
            default: return null;
        }
    }

    // A column of the districts: English (0), Chinese (1) or Russian (2).
    private static string[] SixColumn(int column)
    {
        var list = new string[SixDistricts.Length];
        for (int i = 0; i < SixDistricts.Length; i++)
        {
            list[i] = SixDistricts[i][column];
        }
        return list;
    }

    // The rival tycoons' names (0) or companies (1), as their cards have them.
    private static string[] SixTycoons(int column)
    {
        return column == 0 ? new[] { "Space Karen", "The Zucc", "Bald Luthor" } : new[] { "Karen Aerospace & Motors", "Zucc Social Holdings", "Luthor Everything Co." };
    }

    private static string[] SixStaffJobs()
    {
        var staff = new List<string>();
        for (int i = 1; i < TextWords.StaffJobs.Length; i += 2)
        {
            staff.Add(TextWords.StaffJobs[i]);
        }
        staff.Add(TextWords.StaffJob(null));
        return staff.ToArray();
    }

    // A speaker's role words by their voice: a friend's stage and how they met, the partner's bond, the roommate; open for the rest.
    private static string[] SixRoles(string voice)
    {
        var roles = new List<string>();
        switch (voice)
        {
            case "friend":
                for (int stage = 1; stage < TextWords.Stages.Length; stage++)
                {
                    foreach (string origin in TextWords.Origins)
                    {
                        string role = TextWords.Stages[stage] + ", " + origin;
                        if (!roles.Contains(role))
                        {
                            roles.Add(role);
                        }
                    }
                }
                return roles.ToArray();
            case "partner":
                foreach (string bond in TextWords.PartnerRoles)
                {
                    roles.Add(bond);
                    roles.Add(bond + TextWords.Expecting);
                }
                return roles.ToArray();
            case "roommate": return new[] { "your roommate" };
            default: return null;
        }
    }

    // Every way of writing a speaker of this kind may have, as the mod's own style functions give them.
    private static string[] SixStyles(TextIntent t)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        switch (t.Face)
        {
            case TextFace.Friend:
                if (t.Voice == "stranger")
                {
                    return StreetRules.Styles;
                }
                for (int seed = 0; seed < 4000; seed++)
                {
                    foreach (int traits in new[] { 0, 1 << 10, 1 << 11 })
                    {
                        set.Add(TextWords.FriendStyle(seed, traits, t.Voice == "partner"));
                    }
                }
                break;
            case TextFace.Citizen:
            case TextFace.CitizenDm:
            case TextFace.CitizenDmMood:
                for (int seed = 0; seed < 4000; seed++)
                {
                    set.Add(TextWords.CitizenStyle(seed));
                }
                break;
            case TextFace.Mom:
                for (int seed = 0; seed < 4000; seed++)
                {
                    set.Add(TextWords.MomStyle(seed));
                }
                break;
            case TextFace.InLaws:
                for (int seed = 0; seed < 4000; seed++)
                {
                    set.Add(TextWords.InLawStyle(seed));
                }
                break;
            case TextFace.Boss:
                for (int seed = 0; seed < 4000; seed++)
                {
                    set.Add(TextWords.BossStyle(seed));
                }
                break;
            case TextFace.Business:
                for (int seed = 0; seed < 4000; seed++)
                {
                    set.Add(TextWords.BusinessStyle(seed));
                }
                break;
            case TextFace.Employee: return TextWords.StaffStyles;
            case TextFace.Tycoon: return TextsMapRules.TycoonStyles;
            default: return null;
        }
        return new List<string>(set).ToArray();
    }

    // The closed list of values a slot of the game takes on this intent's cards, or null for an open one.
    private static string[] SixValues(string slot, TextIntent t)
    {
        bool street = t.Key.StartsWith("street.", StringComparison.Ordinal);
        switch (slot)
        {
            case "known": return new[] { "yes", "no" };
            case "weather": return new[] { "raining" };
            case "moment": return new[] { "rush hour", "night", "the weekend", "morning", "evening" };
            case "way": return street ? TextWords.StreetWays : null;
            case "walk":
                if (!street)
                {
                    return null;
                }
                var walks = new List<string>();
                for (int m = 1; m <= 30; m++)
                {
                    walks.Add(TextWords.Walk(m));
                }
                return walks.ToArray();
            case "what": return street ? TextWords.StreetWhats : null;
            case "event":
                if (!street)
                {
                    return null;
                }
                var events = new List<string>();
                for (int i = 1; i < TextWords.StreetEvents.Length; i += 2)
                {
                    events.Add(TextWords.StreetEvents[i]);
                }
                return events.ToArray();
            case "outing": return new[] { "coffee", "dinner", "drinks", "a film", "a walk", "the gym" };
            case "service":
            case "prices":
            case "cleanliness":
            case "interior": return new[] { "very good", "good", "fair", "poor" };
            case "about": return TextsCityRules.ReviewFigures;
            case "crowd": return new[] { "busy", "quiet" };
            case "day": return TextWords.Weekdays;
            case "topic": return TextIntents.ThreadTopics;
            case "stars": return new[] { "1", "2", "3", "4", "5" };
            case "district": return SixColumn(0);
            case "happening":
                var all = new List<string>(TextWords.HappeningsGood);
                all.AddRange(TextWords.HappeningsBad);
                return all.ToArray();
            default: return null;
        }
    }

    // The rules a reply must keep, in words, and the must slots checked in every language (CONTRACT.md: a slot alone is checked).
    private static List<string> SixMust(SixEntry e)
    {
        TextIntent t = e.First;
        string what = t.Shape == "spoken" ? "one spoken line, said face to face" : t.Shape == "paper" ? "one newspaper brief" : "one " + t.Shape + " message";
        var must = new List<string>
        {
            "write " + what + " in the language of the card's lang line, in character as its who, role, mood and style say",
            "keep within max_chars_by_lang of that language (in units) and end well inside max_tokens_by_lang",
            "use only the card's news, facts, memory and history, every name, figure, sum and time exactly as the card writes it",
            "start each sentence with a capital in a language that has them, unless the style says lowercase"
        };
        if (t.Key.StartsWith("street.", StringComparison.Ordinal) || t.Key.IndexOf(".dm.", StringComparison.Ordinal) >= 0 || t.Key.EndsWith(".said", StringComparison.Ordinal)
            || t.Key.StartsWith("citizen.reply.", StringComparison.Ordinal) || t.Key.StartsWith("citizen.answer.", StringComparison.Ordinal))
        {
            must.Add("answer the history's last line: pick up its words, its question, its idea or its mood");
        }
        if (t.MemoryMost > 0)
        {
            must.Add("let the memory lines shape the words where they fit, never against them and never saying more than they say");
        }
        foreach (string m in t.Must)
        {
            string slot = SixAlone(m);
            if (slot == null)
            {
                must.Add("say " + m);
            }
            else
            {
                SixKind(TextSlotKinds.Of(slot), out string rule);
                if (rule == "as_written")
                {
                    must.Add(m);
                }
                else
                {
                    must.Add("say " + m + (rule == "clock" ? " (in another language its clock exactly)" : " (in another language in its own words)"));
                }
            }
        }
        return must;
    }

    // The must slots checked as written in an English reply only (a day, a job, a time, English words of the game).
    private static List<string> SixMustEnglish(SixEntry e)
    {
        var en = new List<string>();
        foreach (string m in e.First.Must)
        {
            string slot = SixAlone(m);
            if (slot != null)
            {
                SixKind(TextSlotKinds.Of(slot), out string rule);
                if (rule != "as_written")
                {
                    en.Add(m);
                }
            }
        }
        return en;
    }

    // The slot of a template that is one slot alone ("{place}"), or null.
    private static string SixAlone(string template)
    {
        return template != null && template.Length > 2 && template[0] == '{' && template.IndexOf('}') == template.Length - 1 ? template.Substring(1, template.Length - 2) : null;
    }

    // What a reply never does, in words.
    private static List<string> SixNever(TextIntent t)
    {
        var never = new List<string>();
        if (t.UntoldName)
        {
            never.Add("{player}");
            never.Add("the player's name, unless the card says 'you know their name: yes' or the news says you recognise them");
        }
        never.AddRange(new[]
        {
            "an exclamation mark, or its full-width form",
            "a dash of any kind, or a hyphen anywhere but inside a word",
            "a line break, or more than one message",
            "a name, figure, sum, time, event, promise or plan that is not on the card",
            "calling itself an AI, a bot, an assistant or a language model",
            "another language than the card's lang line, or a script that language does not write in (names aside)",
            "swearing or slurs",
            "repeating its own earlier line or the player's words back"
        });
        if (t.Shape == "spoken")
        {
            never.Add("words of chat or phones: messages, posts, apps, followers, texting (this is said face to face)");
        }
        return never;
    }

    // Who speaks and what the game decided, in plain words.
    private static string SixNotes(SixEntry e)
    {
        TextIntent t = e.First;
        string who;
        switch (t.Voice)
        {
            case "stranger": who = "a stranger in the street the player stopped to talk to"; break;
            case "employee": who = "one of the player's staff, in their own thread with the player"; break;
            case "mom": who = "the player's mother"; break;
            case "inlaws": who = "the player's partner's parents"; break;
            case "partner": who = "the player's partner"; break;
            case "friend": who = "somebody of the player's social life"; break;
            case "roommate": who = "the player's roommate"; break;
            case "boss": who = "the player's manager at work"; break;
            case "lender": who = "a back-street moneylender"; break;
            case "citizen": who = "one of the city's people on Flare"; break;
            case "official": who = "one of the city's official Flare accounts"; break;
            case "tycoon": who = "a rival tycoon, a parody: each has their own style (Space Karen boastful, The Zucc smooth, Bald Luthor stiff), which goes with their name and company"; break;
            case "business": who = "a business's own Flare account"; break;
            default: who = "the City Ledger"; break;
        }
        var b = new StringBuilder();
        b.Append("The speaker is ").Append(who).Append(". The game decided ")
            .Append(e.Branches.Count > 1 ? "which branch this is, and every fact of the card" : "what the news says and every fact of the card")
            .Append("; the model only words it (").Append(t.Write).Append(").");
        if (t.Key == "citizen.review")
        {
            b.Append(" The review is of the player's own business: its service, prices, cleanliness and interior are the game's own figures in words, and it is mostly about the best of them for four or five stars, the worst otherwise.");
        }
        if (t.Voice == "employee")
        {
            b.Append(" They write to their employer, politely; what they ask for is the game's own figure.");
        }
        if (e.Branches.Count > 1)
        {
            b.Append(" Branches:");
            foreach (TextIntent br in e.Branches)
            {
                b.Append(' ').Append(br.Branch ?? "(one card)").Append(": ").Append(br.News).Append('.');
            }
        }
        return b.ToString();
    }
}
