using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Texts;

// Checks for RLO Texts' contract of data v6 (TextsSpecWriter.cs): the fixtures in this folder (TextsCardsV6.jsonl) are
// the cards the mod builds now, card for card; the mod's own builder makes every fixture's card from its values; the
// spec alone renders every one exactly, as the dataset pipeline does (CONTRACT.md's rules, spec_tools.render in C#); the
// spec has the contract's shape; every card of data v6 has a fixture in each of the seven languages; every slot of every
// intent has a kind; a pack of data 5 or older is never asked a card of data v6; and the copies in the data folder, when
// it is on this PC, are the same.
internal static partial class Program
{
    private static readonly Regex SixSlot = new Regex(@"\{([A-Za-z_][A-Za-z0-9_.]*)\}", RegexOptions.CultureInvariant);
    private static readonly string[] SixEntryKeys = { "name", "voice", "channel", "max_chars", "branches", "card", "slots", "optional", "memory", "must", "never", "decoding", "notes" };
    private static readonly string[] SixDecodingKeys = { "max_tokens", "grammar", "bans", "untold_name" };
    private static readonly string[] SixReserved = { "news", "facts", "memory", "history", "lang" };

    private static void TextsSix()
    {
        SixOlder();   // (the mod's tables alone)
        string path = System.IO.Path.Combine(TextsBuildFolder(), "EconomyCheck", FixturesCopy);
        bool there = File.Exists(path);
        Check(there, "texts v6: the contract's fixtures are where the checks look for them (" + path + "; EconomyCheck --texts-spec writes them)");
        string stored = there ? File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n") : string.Empty;   // (a checkout may give the file the machine's line ends)
        var fixtures = new List<JsonElement>(2600);
        foreach (string line in stored.Split('\n'))
        {
            if (line.Length > 0)
            {
                using JsonDocument doc = JsonDocument.Parse(line);
                fixtures.Add(doc.RootElement.Clone());
            }
        }
        SixCoverage(fixtures);
        SixRebuild(fixtures);
        string spec = null, lines = null, fault = null;
        try
        {
            List<SixEntry> entries = SixEntries();
            List<SixRow> rows = SixFixtures(entries);
            lines = SixLines(rows);
            spec = SixSpec(entries, rows);
        }
        catch (InvalidOperationException e)
        {
            fault = e.Message;
        }
        Check(fault == null, "texts v6: the contract is written from the mod's tables without a fault (" + (fault ?? "none") + ")");
        if (fault != null)
        {
            return;
        }
        Check(stored == lines, "texts v6: the contract's fixtures are the cards the mod builds now (" + SixFirstDifference(stored, lines)
                               + "; rerun EconomyCheck --texts-spec into the data folder and tell the data side)");
        using (JsonDocument specDoc = JsonDocument.Parse(spec))
        {
            JsonElement root = specDoc.RootElement;
            SixRenderAll(root, fixtures);
            SixShape(root);
        }
        SixFolder(spec, lines);
    }

    // Where two texts first differ, by line, for a failing check's line.
    private static string SixFirstDifference(string a, string b)
    {
        string[] la = a.Split('\n'), lb = b.Split('\n');
        for (int i = 0; i < Math.Max(la.Length, lb.Length); i++)
        {
            string x = i < la.Length ? la[i] : "(none)", y = i < lb.Length ? lb[i] : "(none)";
            if (x != y)
            {
                return "line " + (i + 1) + " differs: stored " + (x.Length > 160 ? x.Substring(0, 160) + "..." : x) + " | now " + (y.Length > 160 ? y.Substring(0, 160) + "..." : y);
            }
        }
        return "the same";
    }

    // ---------------------------------------------------------------- the mod's builder

    // Every fixture's card made again by the mod's own builder from the fixture's speaker, slots, history and memory.
    private static void SixRebuild(List<JsonElement> fixtures)
    {
        int same = 0;
        var wrong = new List<string>();
        foreach (JsonElement f in fixtures)
        {
            string key = f.GetProperty("intent").GetString();
            string branch = f.GetProperty("branch").ValueKind == JsonValueKind.Null ? null : f.GetProperty("branch").GetString();
            TextIntent intent = TextIntents.Find(key, branch, TextIntents.DataV6);
            if (intent == null || intent.Data != TextIntents.DataV6)
            {
                wrong.Add(key + " " + branch + ": no card of data v6 in the mod");
                continue;
            }
            var who = new TextWho();
            SixReadWho(f.GetProperty("who"), who);
            JsonElement slots = f.GetProperty("slots");
            var values = new TextSlots();
            foreach (string name in SixSlotNames(intent))
            {
                if (slots.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String)
                {
                    values.Set(name, v.GetString());
                }
            }
            var history = new List<TextSaid>();
            if (slots.TryGetProperty("history", out JsonElement h))
            {
                foreach (JsonElement pair in h.EnumerateArray())
                {
                    history.Add(new TextSaid(pair[0].GetString(), pair[1].GetString()));
                }
            }
            var sheet = new TextSheet();
            string card = "(not built)";
            if (TextIntents.Build(intent, who, values, history, sheet))
            {
                sheet.Lang = f.GetProperty("lang").GetString();
                if (slots.TryGetProperty("memory", out JsonElement memory))
                {
                    foreach (JsonElement m in memory.EnumerateArray())
                    {
                        sheet.Memory.Add(m.GetString());
                    }
                }
                card = TextCard.Card(sheet);
            }
            if (card == f.GetProperty("card").GetString())
            {
                same++;
            }
            else if (wrong.Count < 4)
            {
                wrong.Add(key + " " + branch + " (" + f.GetProperty("lang").GetString() + "):\n    mod:     " + card.Replace("\n", "\\n") + "\n    fixture: "
                          + f.GetProperty("card").GetString().Replace("\n", "\\n"));
            }
        }
        Check(fixtures.Count > 2000 && same == fixtures.Count, "texts v6: the mod's builder makes every fixture's card byte for byte from its values (" + same + " of " + fixtures.Count + ")"
                                                               + (wrong.Count > 0 ? "\n  " + string.Join("\n  ", wrong) : string.Empty));
    }

    // ---------------------------------------------------------------- the pipeline's rendering

    // Every fixture's card rendered from the spec alone, by the contract's rules (CONTRACT.md; spec_tools.render).
    private static void SixRenderAll(JsonElement spec, List<JsonElement> fixtures)
    {
        var by = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonElement e in spec.GetProperty("intents").EnumerateArray())
        {
            foreach (JsonProperty b in e.GetProperty("branches").EnumerateObject())
            {
                by[e.GetProperty("name").GetString() + "#" + b.Name] = e;
            }
        }
        int same = 0;
        var wrong = new List<string>();
        foreach (JsonElement f in fixtures)
        {
            string key = f.GetProperty("intent").GetString();
            string branch = f.GetProperty("branch").ValueKind == JsonValueKind.Null ? string.Empty : f.GetProperty("branch").GetString();
            if (!by.TryGetValue(key + "#" + branch, out JsonElement entry))
            {
                wrong.Add(key + " " + branch + ": not in the spec");
                continue;
            }
            string card = SixRender(entry, branch, f.GetProperty("lang").GetString(), f.GetProperty("slots"), out string why) ?? "(" + why + ")";
            if (card == f.GetProperty("card").GetString())
            {
                same++;
            }
            else if (wrong.Count < 4)
            {
                wrong.Add(key + " " + branch + " (" + f.GetProperty("lang").GetString() + "):\n    spec:    " + card.Replace("\n", "\\n") + "\n    fixture: "
                          + f.GetProperty("card").GetString().Replace("\n", "\\n"));
            }
        }
        Check(fixtures.Count > 2000 && same == fixtures.Count, "texts v6: the spec alone renders every fixture's card exactly, as the dataset pipeline does (" + same + " of " + fixtures.Count + ")"
                                                               + (wrong.Count > 0 ? "\n  " + string.Join("\n  ", wrong) : string.Empty));
    }

    /// <summary>A card rendered from a spec entry, a branch, a language and a fixture's slots by CONTRACT.md's rules; null (and why) when it is not built.</summary>
    private static string SixRender(JsonElement entry, string branch, string lang, JsonElement slots, out string why)
    {
        why = null;
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (JsonProperty p in slots.EnumerateObject())
        {
            values[p.Name] = SixRead(p.Value);
        }
        JsonElement b = entry.GetProperty("branches").GetProperty(branch);
        string news = SixFill(b.GetProperty("news").GetString(), values);
        if (news == null)
        {
            why = "the news needs a slot that has no value";
            return null;
        }
        var facts = new List<string>();
        foreach (JsonElement fact in b.GetProperty("facts").EnumerateArray())
        {
            string t = SixFill(fact.GetString(), values);
            if (t == null)
            {
                why = "a fact needs a slot that has no value";
                return null;
            }
            facts.Add(t);
        }
        foreach (JsonElement fact in entry.GetProperty("optional").EnumerateArray())
        {
            string t = SixFill(fact.GetString(), values);
            if (t != null)
            {
                facts.Add(t);
            }
        }
        values["news"] = news;
        values["facts"] = facts;
        values["lang"] = lang;
        if (!values.TryGetValue("memory", out object memory) || !(memory is List<string>))
        {
            values["memory"] = memory is string one && one.Length > 0 ? new List<string> { one } : new List<string>();
        }
        var history = values.TryGetValue("history", out object h) && h is List<string[]> pairs ? pairs : new List<string[]>();
        var lines = new List<string>();
        foreach (JsonElement template in entry.GetProperty("card").EnumerateArray())
        {
            string line = template.GetString();
            int sep = line.IndexOf(": ", StringComparison.Ordinal);
            if (sep < 0)
            {
                why = "a card line without 'field: '";
                return null;
            }
            string field = line.Substring(0, sep), body = line.Substring(sep + 2);
            if (body.Trim() == "{history}")
            {
                if (history.Count > 0)
                {
                    var t = new StringBuilder("history:");
                    foreach (string[] pair in history)
                    {
                        t.Append("\n  ").Append(pair[0]).Append(": ").Append(pair[1]);
                    }
                    lines.Add(t.ToString());
                }
                continue;
            }
            var parts = new List<string>();
            foreach (string part in body.Split("; "))
            {
                string t = SixFill(part, values);
                if (!string.IsNullOrEmpty(t))
                {
                    parts.Add(t);
                }
            }
            if (parts.Count > 0)
            {
                lines.Add(field + ": " + string.Join("; ", parts));
            }
        }
        return string.Join("\n", lines);
    }

    // A slot's value as the contract reads it: a string, a list of strings, or the history's pairs.
    private static object SixRead(JsonElement v)
    {
        switch (v.ValueKind)
        {
            case JsonValueKind.String: return v.GetString();
            case JsonValueKind.Array:
                if (v.GetArrayLength() > 0 && v[0].ValueKind == JsonValueKind.Array)
                {
                    var pairs = new List<string[]>();
                    foreach (JsonElement p in v.EnumerateArray())
                    {
                        pairs.Add(new[] { p[0].GetString(), p[1].GetString() });
                    }
                    return pairs;
                }
                var list = new List<string>();
                foreach (JsonElement s in v.EnumerateArray())
                {
                    list.Add(s.ValueKind == JsonValueKind.String ? s.GetString() : s.ToString());
                }
                return list;
            case JsonValueKind.Null: return null;
            default: return v.ToString();
        }
    }

    // A template with every slot put in (a list joined by "; "), or null when one has no value (null, "" or an empty list).
    private static string SixFill(string template, Dictionary<string, object> values)
    {
        bool missing = false;
        string filled = SixSlot.Replace(template, m =>
        {
            values.TryGetValue(m.Groups[1].Value, out object v);
            switch (v)
            {
                case string s when s.Length > 0: return s;
                case List<string> list when list.Count > 0: return string.Join("; ", list);
                default:
                    missing = true;
                    return string.Empty;
            }
        });
        return missing ? null : filled;
    }

    // ---------------------------------------------------------------- the shape

    // The spec has the contract's shape (spec_tools.shape_problems): the keys, unique pairs, lean words, kinds, declared slots, decoding, memory.
    private static void SixShape(JsonElement spec)
    {
        var problems = new List<string>();
        if (!spec.TryGetProperty("version", out JsonElement version) || version.GetInt32() != 6)
        {
            problems.Add("the version is not 6");
        }
        if (!spec.TryGetProperty("languages", out JsonElement langs) || langs.GetArrayLength() != TextLanguages.All.Length)
        {
            problems.Add("the languages are not the seven");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement e in spec.GetProperty("intents").EnumerateArray())
        {
            string where = "entry " + index++ + " (" + (e.TryGetProperty("name", out JsonElement n) ? n.GetString() : "?") + ")";
            foreach (string k in SixEntryKeys)
            {
                if (!e.TryGetProperty(k, out _))
                {
                    problems.Add(where + ": no " + k);
                }
            }
            if (problems.Count > 0)
            {
                break;
            }
            foreach (JsonProperty b in e.GetProperty("branches").EnumerateObject())
            {
                if (!seen.Add(n.GetString() + "#" + b.Name))
                {
                    problems.Add(where + ": the branch '" + b.Name + "' twice");
                }
                if (!b.Value.TryGetProperty("news", out _) || !b.Value.TryGetProperty("facts", out _))
                {
                    problems.Add(where + ": the branch '" + b.Name + "' lacks news or facts");
                }
                if (b.Value.TryGetProperty("lean", out JsonElement lean) && lean.ValueKind != JsonValueKind.Null && lean.GetString() != "high" && lean.GetString() != "low")
                {
                    problems.Add(where + ": the branch '" + b.Name + "' leans " + lean);
                }
            }
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement line in e.GetProperty("card").EnumerateArray())
            {
                if (line.GetString().IndexOf(": ", StringComparison.Ordinal) < 0)
                {
                    problems.Add(where + ": a card line without 'field: '");
                }
                SixUsed(line.GetString(), used);
            }
            foreach (JsonProperty b in e.GetProperty("branches").EnumerateObject())
            {
                SixUsed(b.Value.GetProperty("news").GetString(), used);
                foreach (JsonElement fact in b.Value.GetProperty("facts").EnumerateArray())
                {
                    SixUsed(fact.GetString(), used);
                }
            }
            foreach (JsonElement fact in e.GetProperty("optional").EnumerateArray())
            {
                SixUsed(fact.GetString(), used);
            }
            JsonElement memory = e.GetProperty("memory");
            if (!memory.TryGetProperty("max", out _) || !memory.TryGetProperty("kinds", out JsonElement kinds))
            {
                problems.Add(where + ": memory without max or kinds");
                continue;
            }
            foreach (JsonElement k in kinds.EnumerateArray())
            {
                SixUsed(k.GetString(), used);
            }
            JsonElement slots = e.GetProperty("slots");
            foreach (JsonProperty s in slots.EnumerateObject())
            {
                if (s.Name == "history" || s.Name == "lang" || s.Name == "memory")
                {
                    continue;
                }
                if (Array.IndexOf(SixKinds, s.Value.GetProperty("kind").GetString()) < 0)
                {
                    problems.Add(where + ": the slot " + s.Name + " of the kind " + s.Value.GetProperty("kind").GetString());
                }
                JsonElement values = s.Value.GetProperty("values");
                if (values.ValueKind != JsonValueKind.Null && values.ValueKind != JsonValueKind.Array)
                {
                    problems.Add(where + ": the slot " + s.Name + "'s values are neither a list nor null");
                }
            }
            foreach (string s in used)
            {
                if (Array.IndexOf(SixReserved, s) < 0 && !slots.TryGetProperty(s, out _))
                {
                    problems.Add(where + ": the slot " + s + " is used but not in slots");
                }
            }
            JsonElement decoding = e.GetProperty("decoding");
            foreach (string k in SixDecodingKeys)
            {
                if (!decoding.TryGetProperty(k, out _))
                {
                    problems.Add(where + ": no decoding." + k);
                }
            }
            if (problems.Count > 8)
            {
                break;
            }
        }
        Check(problems.Count == 0, "texts v6: the spec has the contract's shape: every key, each (intent, branch) once, lean high, low or none, the pipeline's kinds, every slot used declared, decoding and memory ("
                                   + (problems.Count == 0 ? seen.Count + " pairs" : string.Join("; ", problems)) + ")");
    }

    private static void SixUsed(string template, HashSet<string> into)
    {
        foreach (Match m in SixSlot.Matches(template ?? string.Empty))
        {
            into.Add(m.Groups[1].Value);
        }
    }

    // ---------------------------------------------------------------- coverage

    // Every card of data v6 has a fixture in each of the seven languages; every slot of every intent has a kind.
    private static void SixCoverage(List<JsonElement> fixtures)
    {
        var count = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (JsonElement f in fixtures)
        {
            string key = f.GetProperty("intent").GetString() + "#" + (f.GetProperty("branch").ValueKind == JsonValueKind.Null ? string.Empty : f.GetProperty("branch").GetString());
            if (!count.TryGetValue(key, out HashSet<string> langs))
            {
                count[key] = langs = new HashSet<string>(StringComparer.Ordinal);
            }
            langs.Add(f.GetProperty("lang").GetString());
        }
        var missing = new List<string>();
        foreach (TextIntent t in SixIntents())
        {
            if (!count.TryGetValue(t.Key + "#" + (t.Branch ?? string.Empty), out HashSet<string> langs) || langs.Count != TextLanguages.All.Length)
            {
                missing.Add(t.Key + " " + t.Branch);
            }
        }
        Check(missing.Count == 0 && SixIntents().Count > 300, "texts v6: every card of data v6 has a fixture in each of the seven languages (" + SixIntents().Count + " cards"
                                                             + (missing.Count > 0 ? "; without: " + string.Join(", ", missing.GetRange(0, Math.Min(8, missing.Count))) : string.Empty) + ")");
        var unlisted = new SortedSet<string>(StringComparer.Ordinal);
        foreach (TextIntent t in TextIntents.All)
        {
            foreach (string slot in SixSlotNames(t))
            {
                if (!TextSlotKinds.Listed(slot))
                {
                    unlisted.Add(slot);
                }
            }
        }
        Check(unlisted.Count == 0, "texts v6: every slot of every intent has its kind in TextSlotKinds (without: " + (unlisted.Count == 0 ? "none" : string.Join(", ", unlisted)) + ")");
    }

    // A pack of data 5 or older keeps its exact cards: never asked one of data v6; and each card of data v6 carried on is its older card's, with the street's additions.
    private static void SixOlder()
    {
        int older = 0, wrong = 0;
        var bad = new List<string>();
        foreach (TextIntent t in TextIntents.All)
        {
            if (t.Data > TextIntents.DataV5)
            {
                continue;
            }
            older++;
            TextIntent five = TextIntents.Find(t.Key, t.Branch, TextIntents.DataV5);
            if (five == null || five.Data > TextIntents.DataV5 || TextIntents.Find(t.Key, t.Branch, t.Data).Data > t.Data)
            {
                wrong++;
                bad.Add(t.Key + " " + t.Branch);
            }
        }
        Check(older > 350 && wrong == 0, "texts v6: a pack of data 5 or older is never asked a card of data v6 (" + older + " older cards" + (bad.Count > 0 ? "; wrong: " + string.Join(", ", bad) : string.Empty) + ")");
        var changed = new List<string>();
        foreach (TextIntent t in SixIntents())
        {
            TextIntent before = TextIntents.Find(t.Key, t.Branch, TextIntents.DataV5);
            if (before == null)
            {
                continue;
            }
            bool street = t.Key.StartsWith("street.", StringComparison.Ordinal);
            int facts = before.Facts.Length;
            bool same = t.News == before.News && t.Write == before.Write && t.Voice == before.Voice && t.Face == before.Face && t.MaxLen == before.MaxLen
                        && string.Join("|", t.Must) == string.Join("|", before.Must) && t.Facts.Length == facts + (street ? 1 : 0);
            for (int i = 0; same && i < facts; i++)
            {
                same = t.Facts[i] == before.Facts[i];
            }
            if (same && street)
            {
                same = t.Facts[facts] == "you know their name: {known}" && t.UntoldName;
            }
            else if (same)
            {
                same = string.Join("|", t.Optional) == string.Join("|", before.Optional) && !t.UntoldName;
            }
            if (!same)
            {
                changed.Add(t.Key + " " + t.Branch);
            }
        }
        Check(changed.Count == 0, "texts v6: every card of data v6 carried on is its older card's, the street's always saying whether the name is known (changed: "
                                  + (changed.Count == 0 ? "none" : string.Join(", ", changed)) + ")");
        Check(TextCard.SystemOf(TextIntents.DataV5) == TextCard.System && TextCard.SystemOf(TextIntents.DataV6) == TextCard.SystemSix && TextCard.SystemSix.IndexOf("lang line", StringComparison.Ordinal) > 0,
            "texts v6: a pack of data 5 keeps the trained system turn; data 6's names the lang line");
    }

    // The copies in the data folder, when it is on this PC, are the mod's contract as it is now.
    private static void SixFolder(string spec, string lines)
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(TextsBuildFolder(), "..", "..", "not-in-repo", "textmodel", "data", "gen", "v6", "spec"));
        string specPath = System.IO.Path.Combine(folder, SpecName), fixturesPath = System.IO.Path.Combine(folder, FixturesName);
        if (!File.Exists(specPath) || !File.Exists(fixturesPath))
        {
            Console.WriteLine("  (texts v6: no contract in the data folder on this PC, " + folder + ": its copies not compared)");
            return;
        }
        Check(File.ReadAllText(specPath, Encoding.UTF8).Replace("\r\n", "\n") == spec && File.ReadAllText(fixturesPath, Encoding.UTF8).Replace("\r\n", "\n") == lines,
            "texts v6: the data folder's " + SpecName + " and " + FixturesName + " are the mod's contract as it is now (rerun EconomyCheck --texts-spec " + folder + ")");
    }
}
