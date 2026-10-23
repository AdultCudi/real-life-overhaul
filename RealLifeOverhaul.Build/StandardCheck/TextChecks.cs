using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// docs/standards/texts.md: the English texts' dashes and exclamation marks, and new keys only ever added at the end of
// en.json under the reallife_ prefix (against the en.json of the commit this branch left main at).
internal static partial class Program
{
    private static readonly Regex Dash = new Regex("[–—]| - ", RegexOptions.Compiled);

    private static void TextChecks()
    {
        string rel = LocalesDir + "/en.json";
        string path = Path.Combine(_root, rel);
        Examine("RLO-TXT-002", rel);
        if (!File.Exists(path))
        {
            Hit("RLO-TXT-002", rel, 0, "en.json is missing");
            return;
        }
        List<KeyValuePair<string, string>> en = Locale(File.ReadAllText(path, Encoding.UTF8));
        if (en == null)
        {
            Hit("RLO-TXT-002", rel, 0, "en.json is not a JSON object of texts");
            return;
        }
        foreach (KeyValuePair<string, string> kv in en)
        {
            string subject = "en.json:" + kv.Key;
            Examine("RLO-TXT-006", subject);
            Examine("RLO-TXT-007", subject);
            if (Dash.IsMatch(kv.Value))
            {
                Hit("RLO-TXT-006", subject, 0, "a dash: " + kv.Value);
            }
            // RLO-TXT-007: one exclamation mark at most; whether the text is speech is for review.
            if (kv.Value.IndexOf('!') != kv.Value.LastIndexOf('!'))
            {
                Hit("RLO-TXT-007", subject, 0, "more than one exclamation mark: " + kv.Value);
            }
        }
        Appended(rel, en);
    }

    // RLO-TXT-002: the keys the branch started from keep their order, and every new key comes after all of them.
    private static void Appended(string rel, List<KeyValuePair<string, string>> en)
    {
        string from = MergeBase();
        string old = from == null ? null : Git("show " + from + ":" + rel);
        List<KeyValuePair<string, string>> before = old == null ? null : Locale(old);
        if (before == null)
        {
            Notes.Add("RLO-TXT-002 not checked: no git history to compare en.json with");
            return;
        }
        var was = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kv in before)
        {
            was.Add(kv.Key);
        }
        var now = new HashSet<string>(StringComparer.Ordinal);
        int lastOld = -1;
        for (int i = 0; i < en.Count; i++)
        {
            now.Add(en[i].Key);
            if (was.Contains(en[i].Key))
            {
                lastOld = i;
            }
        }
        var keptBefore = new List<string>();
        foreach (KeyValuePair<string, string> kv in before)
        {
            if (now.Contains(kv.Key))
            {
                keptBefore.Add(kv.Key);
            }
        }
        int k = 0;
        int added = 0;
        for (int i = 0; i < en.Count; i++)
        {
            string key = en[i].Key;
            if (was.Contains(key))
            {
                if (keptBefore[k] != key)
                {
                    Hit("RLO-TXT-002", rel, 0, "the keys are out of their order at " + key + ": " + keptBefore[k] + " came here before");
                    break;
                }
                k++;
                continue;
            }
            added++;
            if (i < lastOld)
            {
                Hit("RLO-TXT-002", rel, 0, "new key " + key + " is not at the end: it stands before " + en[lastOld].Key);
            }
            if (!key.StartsWith("reallife_", StringComparison.Ordinal) && !key.StartsWith("reallife:businesstype_", StringComparison.Ordinal))
            {
                Hit("RLO-TXT-002", rel, 0, "new key " + key + " does not begin reallife_ (nor names a business type of the mod's, which the game reads by its name)");
            }
        }
        int removed = was.Count - keptBefore.Count;
        if (removed > 0)
        {
            Notes.Add("en.json: " + removed + " key(s) taken out since " + from.Substring(0, 8) + " (allowed only when nothing reads them, RLO-TXT-002)");
        }
        Console.WriteLine("  en.json: " + en.Count + " keys, " + added + " new since " + from.Substring(0, 8));
    }

    /// <summary>The texts of a locale file in their order; null if it is not an object of strings.</summary>
    private static List<KeyValuePair<string, string>> Locale(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json.TrimStart('﻿'));
            var list = new List<KeyValuePair<string, string>>(20000);
            foreach (JsonProperty p in doc.RootElement.EnumerateObject())
            {
                list.Add(new KeyValuePair<string, string>(p.Name, p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : ""));
            }
            return list;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
