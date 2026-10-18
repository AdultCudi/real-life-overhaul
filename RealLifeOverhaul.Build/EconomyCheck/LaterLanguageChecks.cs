using System;
using System.Collections.Generic;
using System.IO;
using IoPath = System.IO.Path;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Dutch and Turkish (nl, tr), the two languages added after German, French, Spanish and Russian. First, every list of the
// mod's languages names them where it names the others: ModLanguages, Workshop/locales.py's languages, What's new marks and
// empty rows (held to the checkers' own FixMarks and NothingYet), the installer's languages and each of its messages, the
// Workshop description's and the README's languages line and the Guide's Known limits page. Then their files are as complete
// as the other translations: every key German, French, Spanish and Russian all have, and no text left as the English unless
// its key is on locales.py's ENGLISH_OK. Then their What's new rows: every numbered row the others have, under the same
// version title, with the language's own fix mark as often as the English has "Fixed: ", and the next row as locales.py
// wants it. Last, What's new tells of the two languages in every language.
internal static partial class Program
{
    // The languages added on 18 Oct 2026, and the ones whose keys they are held to.
    private static readonly string[] AddedLanguages = { "nl", "tr" };
    private static readonly string[] EarlierLaterLanguages = { "de", "fr", "es", "ru" };

    // What's new's line for the two languages, by a piece of it in each language (in the order of ModLanguages.All).
    private static readonly string[] AddedLanguagesLine =
    {
        "The mod can now be read in Dutch and Turkish too", "O mod agora pode ser lido em holandês e turco", "本模组现在也可以用荷兰语和土耳其语阅读",
        "Die Mod lässt sich jetzt auch auf Niederländisch und Türkisch lesen", "Le mod peut désormais être lu en néerlandais et en turc",
        "El mod ya se puede leer también en neerlandés y turco", "Мод теперь можно читать на нидерландском и турецком",
        "De mod kan nu ook in het Nederlands en Turks worden gelezen", "Mod artık Felemenkçe ve Türkçe dillerinde de okunabiliyor"
    };

    private static void LaterLanguages()
    {
        string folder = LocalesFolder();
        string root = IoPath.GetFullPath(IoPath.Combine(folder, @"..\..\..\.."));
        LaterLanguagesListed(root, folder);
        Dictionary<string, string> en = LoadWords(IoPath.Combine(folder, "en.json"));
        if (en == null)
        {
            Check(false, "later languages: en.json is at " + folder);
            return;
        }
        var earlier = new List<Dictionary<string, string>>();
        foreach (string code in EarlierLaterLanguages)
        {
            Dictionary<string, string> words = LoadWords(IoPath.Combine(folder, code + ".json"));
            Check(words != null, "later languages: " + code + ".json is there to hold the added languages to");
            if (words != null)
            {
                earlier.Add(words);
            }
        }
        string locales = ReadOr(IoPath.Combine(root, @"RealLifeOverhaul.Build\Workshop\locales.py"));
        var englishOk = new HashSet<string>(StringComparer.Ordinal);
        Match ok = Regex.Match(locales, @"ENGLISH_OK = \{(.*?)\n\}", RegexOptions.Singleline);
        foreach (Match m in Regex.Matches(ok.Groups[1].Value, @"'([^']+)'"))
        {
            englishOk.Add(m.Groups[1].Value);
        }
        Check(englishOk.Count > 200, "later languages: locales.py's ENGLISH_OK was read (" + englishOk.Count + " keys)");
        foreach (string code in AddedLanguages)
        {
            Dictionary<string, string> words = LoadWords(IoPath.Combine(folder, code + ".json"));
            Check(words != null, "later languages: Locales/" + code + ".json is there");
            if (words != null)
            {
                LaterLanguageComplete(code, en, words, earlier, englishOk);
                LaterLanguageWhatsNew(code, en, words, earlier.Count > 0 ? earlier[0] : null);
            }
        }
        Check(LineEverywhere(AddedLanguagesLine), "later languages: What's new tells of Dutch and Turkish in some row of every language");
    }

    // A feature's line (no fix mark), by a piece of it in each language in the order of ModLanguages.All, in some What's new
    // row of each: the next row until a release numbers it into its own.
    private static bool LineEverywhere(string[] pieces)
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        bool all = codes.Length == ModLanguages.All.Length && pieces.Length == ModLanguages.All.Length;
        for (int l = 0; all && l < codes.Length; l++)
        {
            all &= WhatsNewHas(langs[l], pieces[Array.IndexOf(ModLanguages.All, codes[l])]);
        }
        return all;
    }

    private static string ReadOr(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    // ---------------------------------------------------------------- listed wherever the others are

    private static void LaterLanguagesListed(string root, string folder)
    {
        var all = new List<string>(ModLanguages.All);
        Check(all.Count == 9 && all.IndexOf("nl") == 7 && all.IndexOf("tr") == 8, "later languages: ModLanguages.All ends with nl and tr (" + string.Join(", ", all) + ")");
        Check(FixMarks.Count == all.Count && NothingYet.Count == all.Count && all.TrueForAll(c => FixMarks.ContainsKey(c) && NothingYet.ContainsKey(c)),
            "later languages: the checkers' What's new marks and empty rows have every language");

        // locales.py: its languages with their names, its NOTHING and FIXED, the same as the checkers'.
        string locales = ReadOr(IoPath.Combine(root, @"RealLifeOverhaul.Build\Workshop\locales.py"));
        var names = new List<string>();
        var codes = new List<string>();
        foreach (Match m in Regex.Matches(Regex.Match(locales, @"LANGUAGES = \((.*?)\)\r?\n", RegexOptions.Singleline).Groups[1].Value, @"\('([a-z-]+)', '([^']+)'\)"))
        {
            codes.Add(m.Groups[1].Value);
            names.Add(m.Groups[2].Value);
        }
        Check(string.Join(",", codes) == string.Join(",", all), "later languages: locales.py's LANGUAGES are ModLanguages.All (" + string.Join(", ", codes) + ")");
        foreach ((string dict, Dictionary<string, string> want) in new[] { ("NOTHING", NothingYet), ("FIXED", FixMarks) })
        {
            Dictionary<string, string> got = PythonDict(locales, dict);
            var wrong = new List<string>();
            foreach (string code in all)
            {
                if (!got.TryGetValue(code, out string v) || v != want[code])
                {
                    wrong.Add(code);
                }
            }
            Check(wrong.Count == 0 && got.Count == all.Count, "later languages: locales.py's " + dict + " has every language as the checkers have it" + (wrong.Count > 0 ? " (not: " + string.Join(", ", wrong) + ")" : string.Empty));
        }

        // The installer: a language for each of the mod's that Inno Setup has (all but Simplified Chinese), and each of its
        // messages in every one of them.
        string iss = ReadOr(IoPath.Combine(root, @"RealLifeOverhaul.Build\Installer\RealLifeOverhaul.iss"));
        var setup = new List<string>();
        foreach (Match m in Regex.Matches(iss, "^Name: \"([a-z-]+)\"; MessagesFile: \"compiler:[^\"]+\\.isl\"", RegexOptions.Multiline))
        {
            setup.Add(m.Groups[1].Value);
        }
        var wantSetup = all.FindAll(c => c != "zh-cn");
        Check(string.Join(",", setup) == string.Join(",", wantSetup), "later languages: the installer has a language for each of the mod's but Simplified Chinese (" + string.Join(", ", setup) + ")");
        var lacking = new List<string>();
        foreach (Match m in Regex.Matches(iss, @"^en\.([A-Za-z]+)=", RegexOptions.Multiline))
        {
            foreach (string code in wantSetup)
            {
                if (!Regex.IsMatch(iss, "^" + Regex.Escape(code) + @"\." + m.Groups[1].Value + "=.+", RegexOptions.Multiline))
                {
                    lacking.Add(code + "." + m.Groups[1].Value);
                }
            }
        }
        Check(lacking.Count == 0 && Regex.Matches(iss, @"^en\.[A-Za-z]+=", RegexOptions.Multiline).Count >= 5,
            "later languages: every installer message is there in each of its languages" + (lacking.Count > 0 ? " (lacking: " + string.Join(", ", lacking) + ")" : string.Empty));

        // The languages line, as one list: the Workshop description, the README and the Guide's Known limits page.
        string list = names.Count > 1 ? string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1] : string.Empty;
        Check(names.Count == all.Count && ReadOr(IoPath.Combine(root, @"RealLifeOverhaul.Build\Workshop\description.txt")).Contains("[b]Languages: " + list + "[/b]"),
            "later languages: the Workshop description's languages line names every language (" + list + ")");
        Check(names.Count == all.Count && ReadOr(IoPath.Combine(root, "README.md")).Contains(list + ";"), "later languages: the README's languages line names every language");
        Dictionary<string, string> en = LoadWords(IoPath.Combine(folder, "en.json"));
        Check(en != null && names.Count == all.Count && en.TryGetValue("reallife_guide_limits", out string limits) && limits.Contains("- " + list + ". "),
            "later languages: the Guide's Known limits page names every language");
    }

    // A Python dict of code and text, as locales.py writes NOTHING and FIXED (single or double quotes).
    private static Dictionary<string, string> PythonDict(string source, string name)
    {
        var got = new Dictionary<string, string>(StringComparer.Ordinal);
        Match body = Regex.Match(source, "^" + name + @" = \{(.*?)\}", RegexOptions.Singleline | RegexOptions.Multiline);
        foreach (Match m in Regex.Matches(body.Groups[1].Value, "'([a-z-]+)': (?:'([^']*)'|\"([^\"]*)\")"))
        {
            got[m.Groups[1].Value] = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
        }
        return got;
    }

    // ---------------------------------------------------------------- as complete as the other translations

    private static readonly Regex LatinWord = new Regex("[A-Za-z]{3,}");
    private static readonly Regex Hole = new Regex(@"\{[^{}]*\}|</?[a-zA-Z][^<>]*>");

    private static void LaterLanguageComplete(string code, Dictionary<string, string> en, Dictionary<string, string> words, List<Dictionary<string, string>> earlier, HashSet<string> englishOk)
    {
        var missing = new List<string>();
        int held = 0;
        foreach (string key in en.Keys)
        {
            bool everyEarlier = earlier.Count == EarlierLaterLanguages.Length && earlier.TrueForAll(w => w.ContainsKey(key));
            if (!everyEarlier)
            {
                continue;
            }
            held++;
            if (!words.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text))
            {
                missing.Add(key);
            }
        }
        Check(held > 19000 && missing.Count == 0, "later languages: " + code + ".json has every one of the " + held + " keys German, French, Spanish and Russian all have"
              + (missing.Count > 0 ? " (lacking " + missing.Count + ": " + string.Join(", ", missing.GetRange(0, Math.Min(6, missing.Count))) + ")" : string.Empty));
        var english = new List<string>();
        foreach (KeyValuePair<string, string> text in words)
        {
            if (en.TryGetValue(text.Key, out string source) && text.Value == source && LatinWord.Matches(Hole.Replace(source, string.Empty)).Count >= 4 && !englishOk.Contains(text.Key))
            {
                english.Add(text.Key);
            }
        }
        Check(english.Count == 0, "later languages: no " + code + " text of four words or more is the English unchanged unless locales.py's ENGLISH_OK lists it"
              + (english.Count > 0 ? " (" + english.Count + ": " + string.Join(", ", english.GetRange(0, Math.Min(6, english.Count))) + ")" : string.Empty));
    }

    // ---------------------------------------------------------------- What's new: numbered and marked

    private static readonly Regex NumberedRow = new Regex(@"^reallife_changes_\d{3}_\d+$");

    private static void LaterLanguageWhatsNew(string code, Dictionary<string, string> en, Dictionary<string, string> words, Dictionary<string, string> german)
    {
        var lost = new List<string>();
        var marked = new List<string>();
        int rows = 0;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (!NumberedRow.IsMatch(row.Key) || german == null || !german.ContainsKey(row.Key))
            {
                continue;
            }
            rows++;
            if (!words.TryGetValue(row.Key, out string text) || !words.TryGetValue(row.Key + "_title", out string title)
                || !en.TryGetValue(row.Key + "_title", out string enTitle) || title != enTitle)
            {
                lost.Add(row.Key);
                continue;
            }
            if (Count(text, FixMarks[code]) != Count(row.Value, FixMarks["en"]))
            {
                marked.Add(row.Key);
            }
        }
        Check(rows > 100 && lost.Count == 0, "later languages: " + code + " has every numbered What's new row German has (" + rows + "), under the version's own title"
              + (lost.Count > 0 ? " (lacking: " + string.Join(", ", lost.GetRange(0, Math.Min(6, lost.Count))) + ")" : string.Empty));
        Check(marked.Count == 0, "later languages: each of " + code + "'s numbered rows marks its fixes " + FixMarks[code].Trim() + " as often as the English marks them Fixed:"
              + (marked.Count > 0 ? " (not: " + string.Join(", ", marked.GetRange(0, Math.Min(6, marked.Count))) + ")" : string.Empty));
        string next = en.TryGetValue("reallife_changes_next", out string n) ? n : null;
        string own = words.TryGetValue("reallife_changes_next", out string o) ? o : null;
        Check(next != null && own != null && (next == NothingYet["en"] ? own == NothingYet[code] : own != NothingYet[code] && Count(own, FixMarks[code]) == Count(next, FixMarks["en"])),
            "later languages: " + code + "'s next row is " + NothingYet[code] + " when the English is " + NothingYet["en"] + ", and otherwise marks its fixes as the English does");
    }
}
