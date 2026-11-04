using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// The Company screen's words in Portuguese and Chinese: every key its screen names (UiScreens.Company.cs) has a value in each language that is not the English one, so that no
// English stands in a sentence of a translated page. A value that is rightly the same in a language (a name, a figure, a word the language borrows) is listed below.
internal static partial class Program
{
    // Words that are the same in Portuguese, or in Chinese, as in English: borrowed words and names.
    private static readonly HashSet<string> SameInPortuguese = new HashSet<string>(StringComparer.Ordinal)
    {
        "Marketing", "marketing", "Rota", "Staff", "Standing", "Open", "Hotel", "Total", "Banco", "Original", "Barista", "Flyers", "Auto", "Regional, Midtown"
    };

    // Keys whose value is rightly the same: the accountants are firms' names, an hour is written alike, and Auto and Regional are the same word in Portuguese.
    private static readonly HashSet<string> SameKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "reallife_co_accountant_0", "reallife_co_accountant_1", "reallife_co_accountant_2", "reallife_co_tree_region", "reallife_rota_hours_n", "reallife_rota_mode_auto_short", "reallife_talent_level_principal"
    };

    private static void CompanyLocaleChecks()
    {
        int before = _run, failedBefore = _failed;
        string root = null;
        for (string dir = AppContext.BaseDirectory; dir != null && root == null; dir = System.IO.Path.GetDirectoryName(dir))
        {
            root = Directory.Exists(System.IO.Path.Combine(dir, "Assets", "Mods", "RealLifeOverhaul", "Locales")) ? dir : null;
        }
        if (root == null)
        {
            Check(false, "company words: the locales are found");
            return;
        }
        string loc = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Locales");
        string screens = File.ReadAllText(System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "UiScreens.Company.cs"));
        int a = screens.IndexOf("// KEYS BEGIN", StringComparison.Ordinal), b = screens.IndexOf("// KEYS END", StringComparison.Ordinal);
        var keys = new List<string>();
        foreach (Match m in Regex.Matches(screens.Substring(a, b - a), "\"(reallife_[a-z0-9_]+)\""))
        {
            keys.Add(m.Groups[1].Value);
        }
        Dictionary<string, string> en = Words(System.IO.Path.Combine(loc, "en.json")), pt = Words(System.IO.Path.Combine(loc, "pt.json")), zh = Words(System.IO.Path.Combine(loc, "zh-cn.json"));
        foreach (string k0 in en.Keys)
        {
            // the company's notices and questions are the screen's words too, whether or not a screen names them
            if ((k0.StartsWith("reallife_co_note_", StringComparison.Ordinal) || k0.StartsWith("reallife_co_q_", StringComparison.Ordinal)) && !keys.Contains(k0))
            {
                keys.Add(k0);
            }
        }
        var missing = new StringBuilder();
        var english = new StringBuilder();
        foreach (string k in keys)
        {
            string e;
            if (!en.TryGetValue(k, out e))
            {
                continue;   // a game key (the face reads some of the game's own words)
            }
            foreach (var lang in new[] { new KeyValuePair<string, Dictionary<string, string>>("pt", pt), new KeyValuePair<string, Dictionary<string, string>>("zh-cn", zh) })
            {
                string v;
                if (!lang.Value.TryGetValue(k, out v) || v.Length == 0)
                {
                    missing.Append(' ').Append(lang.Key).Append(':').Append(k);
                }
                else if (v == e && Regex.IsMatch(e, "[A-Za-z]{4,}") && !SameInPortuguese.Contains(e) && !SameKeys.Contains(k))
                {
                    english.Append(' ').Append(lang.Key).Append(':').Append(k);
                }
            }
        }
        Check(missing.Length == 0, "company words: every key of the Company screen has a Portuguese and a Chinese value" + (missing.Length > 0 ? ", not:" + missing : string.Empty));
        Check(english.Length == 0, "company words: no value of the Company screen in Portuguese or Chinese is the English one (" + keys.Count + " keys)" + (english.Length > 0 ? ", English in:" + english : string.Empty));
        CompanySampleWords();
        Console.WriteLine("  the Company screen's words in pt and zh-cn: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    private static Dictionary<string, string> Words(string path)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(File.ReadAllText(path, Encoding.UTF8), "\"([^\"\\\\]+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
        {
            d[m.Groups[1].Value] = Regex.Unescape(m.Groups[2].Value);
        }
        return d;
    }

    // The names the samples are made of: people, businesses, districts, rivals and insurers. A name reads the same in every language; anything else a page says must come from a key.
    private static readonly string[] SampleNames =
    {
        "Alex Moreno",
        "Ana Costa",
        "Ben Ortiz",
        "Carlos Vega",
        "Dev Kapoor",
        "Eli Novak",
        "Ethan Brooks",
        "Hugo Brandt",
        "Ines Duarte",
        "Ines Park",
        "Jin Park",
        "Kai Brennan",
        "Lena Adler",
        "Leo Martin",
        "Lucia Ferro",
        "Marta Silva",
        "Maya Okafor",
        "Meera Iyer",
        "Mei Lin",
        "Noa Silva",
        "Noah Feldman",
        "Noor Haddad",
        "Omar Haddad",
        "Priya Nair",
        "Ray Walsh",
        "Rui Tanaka",
        "Sana Malik",
        "Tessa Brandt",
        "Tobias Wren",
        "Tomas Reyes",
        "Zara Quinn",
        "Hana Sato",
        "Lena Hoff",
        "Ivo Petrov",
        "Bloom & Co",
        "Corner Cafe",
        "Dockside Garage",
        "Hudson Warehouse",
        "Midtown Deli",
        "Pioneer Game Studio",
        "Pioneer HQ",
        "Pioneer Holdings",
        "Riverside Gym",
        "Speedy Bites",
        "Uptown Books",
        "Harbour Grill",
        "Harbourside Freight HSF",
        "Brooklyn Brew",
        "Midtown Grill",
        "Midtown",
        "Hell's Kitchen",
        "Chelsea",
        "Harlem",
        "Brooklyn",
        "Elm Street",
        "5th Avenue",
        "Safeguard",
        "Prime Assurance",
        "Budget Mutual",
        "Marlow & Finch",
        "Quay Street Bookkeeping",
        "Ashdown Chartered",
        "Quiet week", "Meera", "Noah", "Tessa", "Lena", "Hana", "Grill", "BizMan", "Harbour Grill",
        "Holiday week",
        "Buns",
        "Patties",
        "Fries",
        "Salad",
        "Napkins",
        "Cola",
        "Dev",
        "Ray",
        "Omar"
    };

    // Every sentence, label and skill the layout of a state says in Portuguese or Chinese is a translation: a text that is the English one, or that holds an English word, once the names
    // and the figures are taken out, is a literal the sample wrote instead of a key.
    private static void CompanySampleWords()
    {
        UiWords en = UiWords("en");
        var names = new List<string>(SampleNames);
        names.Sort((x, y) => y.Length.CompareTo(x.Length));
        var found = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            UiWords t = UiWords(lang);
            foreach (string state in UiSamples.CompanyStates)
            {
                HashSet<string> said = SampleTexts(en, state);
                foreach (string text in SampleTexts(t, state))
                {
                    string rest = text;
                    foreach (string n in names)
                    {
                        rest = rest.Replace(n, " ");
                    }
                    rest = Regex.Replace(rest, "\\{[a-z_]+\\}", " ");
                    bool english = lang == "zh-cn" ? Regex.IsMatch(rest, "[A-Za-z]{3,}") : said.Contains(text) && Regex.IsMatch(rest, "[A-Za-z]{3,}") && !SameInPortuguese.Contains(rest.Trim()) && !SameInPortuguese.Contains(text);
                    if (english)
                    {
                        found[lang + ":" + text] = state;
                    }
                }
            }
        }
        var list = new StringBuilder();
        int n2 = 0;
        foreach (KeyValuePair<string, string> kv in found)
        {
            if (n2++ < (Environment.GetEnvironmentVariable("RLO_LOCALE_ALL") != null ? int.MaxValue : 40))
            {
                list.Append("\n      ").Append(kv.Value).Append(' ').Append(kv.Key);
            }
        }
        Check(found.Count == 0, "company words: no sentence, label or skill of a state in Portuguese or Chinese is a sample's English literal (" + found.Count + ")" + list);
    }

    private static HashSet<string> SampleTexts(UiWords t, string state)
    {
        var s = new UiScene();
        CompanyFace.Lay(s, UiSamples.CompanyFactsOf(t, state), UiSamples.CompanyViewOf(state), UiSamples.CompanyWordsOf(t), new CompanyLayout(), UiFrame.FullMenu(16f / 9f, "Company"), true, UiDefaultKeys.Keyboard);
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < s.ItemCount; i++)
        {
            string text = s.Items[i].Text;
            if (!string.IsNullOrEmpty(text) && s.Items[i].Kind == UiKind.Text)
            {
                set.Add(text);
            }
        }
        return set;
    }
}
