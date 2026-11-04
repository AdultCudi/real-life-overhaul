using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// The Company screen's words in Portuguese and Chinese: every key its screen names (UiScreens.Company.cs) has a value in each language that is not the English one, so that no
// English stands in a sentence of a translated page. A value that is rightly the same in a language (a name, a figure, a word the language borrows) is listed below.
internal static partial class Program
{
    // Words that are the same in Portuguese, or in Chinese, as in English: borrowed words and names.
    private static readonly HashSet<string> SameInPortuguese = new HashSet<string>(StringComparer.Ordinal)
    {
        "Marketing", "marketing", "Rota", "Staff", "Standing", "Open", "Hotel", "Total", "Banco", "Original"
    };

    // Keys whose value is rightly the same: the accountants are firms' names, an hour is written alike, and Auto and Regional are the same word in Portuguese.
    private static readonly HashSet<string> SameKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "reallife_co_accountant_0", "reallife_co_accountant_1", "reallife_co_accountant_2", "reallife_co_tree_region", "reallife_rota_hours_n", "reallife_rota_mode_auto_short"
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
}
