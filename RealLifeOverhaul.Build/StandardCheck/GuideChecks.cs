using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// docs/standards/texts.md, RLO-TXT-013 and RLO-TXT-014: the Guide's pages name only what exists, and no feature goes without a page.
internal static partial class Program
{
    private static readonly Regex BoldName = new Regex("<b>(.*?)</b>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex FactName = new Regex(@"\{[a-z0-9_]+\}", RegexOptions.Compiled);
    private static readonly Regex NumberedStep = new Regex(@"^\d+\.\s*", RegexOptions.Compiled);
    private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);

    private const string GuideNamesFile = "RealLifeOverhaul.Build/StandardCheck/guide-game-names.txt";
    private const string GuideCoverageFile = "RealLifeOverhaul.Build/StandardCheck/guide-coverage.txt";

    private static void GuideChecks()
    {
        string path = Path.Combine(_root, LocalesDir, "en.json");
        if (!File.Exists(path))
        {
            return;   // TextChecks has said so
        }
        List<KeyValuePair<string, string>> en = Locale(File.ReadAllText(path, Encoding.UTF8));
        if (en == null)
        {
            return;
        }
        GuideNames(en);
        GuideCoverage(en);
    }

    /// <summary>A name as the Guide and the texts compare it: lower case, one space between words, no {fact}, no closing colon, full stop or ellipsis.</summary>
    private static string Plain(string text)
    {
        string t = Spaces.Replace(FactName.Replace(text, string.Empty), " ").Replace("()", string.Empty).Trim().ToLowerInvariant();
        return t.TrimEnd(':', '.', '…', ' ');
    }

    // RLO-TXT-013: every name in bold in a Guide page (an app, a tab, a button, an option, a page) is a text of the mod, or a name the game owns
    // (guide-game-names.txt: one name a line, lower case, for the game's own apps, buttons and things).
    private static void GuideNames(List<KeyValuePair<string, string>> en)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kv in en)
        {
            if (kv.Key.StartsWith("reallife_guide_", StringComparison.Ordinal) || kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (string line in kv.Value.Split('\n'))
            {
                string name = Plain(line);
                if (name.Length > 0)
                {
                    known.Add(name);
                    int colon = line.IndexOf(':');
                    if (colon > 0)
                    {
                        known.Add(Plain(line.Substring(0, colon)));   // a label that goes on to say what it does: "Wage band: the most anybody is paid"
                    }
                }
            }
        }
        string file = Path.Combine(_root, GuideNamesFile);
        if (!File.Exists(file))
        {
            Hit("RLO-TXT-013", GuideNamesFile, 0, "the list of the game's own names is missing");
            return;
        }
        foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length > 0 && line[0] != '#')
            {
                known.Add(Plain(line));
            }
        }
        foreach (KeyValuePair<string, string> kv in en)
        {
            if (!kv.Key.StartsWith("reallife_guide_", StringComparison.Ordinal))
            {
                continue;
            }
            string subject = "en.json:" + kv.Key;
            Examine("RLO-TXT-013", subject);
            foreach (Match m in BoldName.Matches(kv.Value))
            {
                string name = Plain(NumberedStep.Replace(m.Groups[1].Value, string.Empty));   // "1. An office." heads a step in a list of steps
                if (name.Length > 0 && !known.Contains(name))
                {
                    Hit("RLO-TXT-013", subject, 0, "<b>" + m.Groups[1].Value + "</b> is no text of the mod and not in guide-game-names.txt");
                }
            }
            // A menu path is written with each step in bold, so that the check above reads every step.
            foreach (string line in kv.Value.Split('\n'))
            {
                for (int at = line.IndexOf(" > ", StringComparison.Ordinal); at >= 0; at = line.IndexOf(" > ", at + 3, StringComparison.Ordinal))
                {
                    bool before = at >= 4 && string.CompareOrdinal(line, at - 4, "</b>", 0, 4) == 0;
                    bool after = string.CompareOrdinal(line, at + 3, "<b>", 0, 3) == 0;
                    if (!before || !after)
                    {
                        Hit("RLO-TXT-013", subject, 0, "a path with a step not in bold: " + line);
                        break;
                    }
                }
            }
        }
    }

    // RLO-TXT-014: every page of docs/features is listed in guide-coverage.txt, with the Guide keys that explain it, or with "-" and the reason
    // it is not for players. A key named there holds a text; a page listed there exists.
    private static void GuideCoverage(List<KeyValuePair<string, string>> en)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kv in en)
        {
            texts[kv.Key] = kv.Value;
        }
        string dir = Path.Combine(_root, "docs", "features");
        string file = Path.Combine(_root, GuideCoverageFile);
        Examine("RLO-TXT-014", GuideCoverageFile);
        if (!Directory.Exists(dir) || !File.Exists(file))
        {
            Hit("RLO-TXT-014", GuideCoverageFile, 0, "docs/features or its coverage list is missing");
            return;
        }
        var listed = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (string raw in File.ReadAllLines(file, Encoding.UTF8))
        {
            string line = raw.TrimEnd();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            string[] parts = line.Split('\t');
            if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0 || parts[2].Length == 0)
            {
                Hit("RLO-TXT-014", GuideCoverageFile, 0, "a line is not page, keys and reason separated by tabs: " + line);
                continue;
            }
            listed[parts[0]] = parts;
        }
        var pages = new HashSet<string>(StringComparer.Ordinal);
        foreach (string md in Directory.GetFiles(dir, "*.md"))
        {
            string name = Path.GetFileNameWithoutExtension(md);
            if (name == "README")
            {
                continue;
            }
            pages.Add(name);
            string subject = "docs/features/" + name + ".md";
            Examine("RLO-TXT-014", subject);
            if (!listed.TryGetValue(name, out string[] row))
            {
                Hit("RLO-TXT-014", subject, 0, "has no Guide page and is not listed as not player-facing in guide-coverage.txt");
                continue;
            }
            if (row[1] == "-")
            {
                continue;
            }
            foreach (string key in row[1].Split(','))
            {
                if (!texts.TryGetValue(key.Trim(), out string text) || text.Trim().Length == 0)
                {
                    Hit("RLO-TXT-014", subject, 0, "its Guide text " + key + " is not in en.json");
                }
            }
        }
        foreach (string name in listed.Keys)
        {
            if (!pages.Contains(name))
            {
                Hit("RLO-TXT-014", GuideCoverageFile, 0, "lists " + name + ", which is not a page of docs/features");
            }
        }
    }
}
