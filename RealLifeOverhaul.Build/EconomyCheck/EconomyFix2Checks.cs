using System;
using System.Collections.Generic;
using System.Linq;

// Faults fixed in five more Economy folders (OwnBank, City, Online, Ventures, Finance), each reproduced in a file of its
// own: OwnBankFixChecks.cs, CityFixChecks.cs, OnlineFixChecks.cs, VenturesFixChecks.cs and FinanceFixChecks.cs; their
// texts here.
internal static partial class Program
{
    private static void EconomyFixes2()
    {
        OwnBankFixes();
        CityFixes();
        OnlineFixes();
        VenturesFixes();
        FinanceFixes();
        EconomyFix2Texts();
    }

    private static void EconomyFix2Texts()
    {
        string folder = LocalesFolder();
        var languages = new List<(string code, Dictionary<string, string> words)>();
        foreach (string code in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru" })
        {
            languages.Add((code, LoadWords(System.IO.Path.Combine(folder, code + ".json"))));
        }
        if (languages[0].words == null || languages[1].words == null || languages[2].words == null)
        {
            Check(false, "economy fixes: the texts are at " + folder);
            return;
        }
        var missing = new List<string>();
        foreach ((string key, string[] names) in new[]
                 {
                     ("reallife_ob_why_changed", new string[0]), ("reallife_ven_note_no_deal_room", new[] { "{most}", "{share}" }),
                     ("reallife_ven_counter_at_most", new[] { "{name}" }), ("reallife_ven_note_call_missed_no_court", new[] { "{name}", "{amount}", "{count}" }),
                     ("reallife_ven_deal_call_no_court", new[] { "{count}" }), ("reallife_econ_approval_charge", new[] { "{per_day}", "{charge}" }),
                     ("reallife_econ_confirm_cancel_approval", new[] { "{amount}", "{lender}", "{charge}", "{days}" })
                 })
        {
            for (int l = 0; l < 3; l++)
            {
                if (!languages[l].words.TryGetValue(key, out string text) || Array.Exists(names, n => text.IndexOf(n, StringComparison.Ordinal) < 0))
                {
                    missing.Add(languages[l].code + ":" + key);
                }
            }
        }
        Check(missing.Count == 0, "economy fixes: the new texts are in en, pt and zh-cn with their names" + (missing.Count > 0 ? ": " + string.Join(", ", missing) : string.Empty));
        var stale = new List<string>();
        foreach ((string code, Dictionary<string, string> words) in languages)
        {
            foreach (string key in new[] { "reallife_econ_confirm_car", "reallife_econ_confirm_home" })
            {
                if (words != null && words.TryGetValue(key, out string text) && text.IndexOf("{per_day}", StringComparison.Ordinal) < 0)
                {
                    stale.Add(code + ":" + key);
                }
            }
        }
        Check(stale.Count == 0, "economy fixes: the approval's question names the interest a day in every language" + (stale.Count > 0 ? ": " + string.Join(", ", stale) : string.Empty));
        Check(languages[0].words.TryGetValue("reallife_online_days_note", out string note) && note.Contains("six days"), "economy fixes: the day-by-day table's note says it holds today and six days");
        // The fixes stay in the next row until a version numbers them, so every What's new row is read.
        string next = string.Join(" ", languages[0].words
            .Where(kv => kv.Key.StartsWith("reallife_changes_", StringComparison.Ordinal)).Select(kv => kv.Value));
        Check(next.Contains("Fixed: your own bank") && next.Contains("Fixed: the Web shop")
              && next.Contains("Fixed: investors") && next.Contains("Fixed: the Finance app") && next.Contains("Fixed: the city's economy"),
            "economy fixes: What's new names each folder's fixes");
    }
}
