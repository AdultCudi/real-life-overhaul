using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// The texts of every language the mod has, for the checks that hold a feature's keys to each language: en first, then pt
// and zh-cn, then German, French, Spanish and Russian when their file is there (ModLanguages). A language the checks read
// is held to the same keys and the same names in curly brackets as the English; a key German, French, Spanish or Russian
// lacks is read as the English, as the game reads it.
internal static partial class Program
{
    private static readonly Regex Names = new Regex(@"\{[a-z_]+\}");

    private static string LocalesFolder()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
    }

    /// <summary>
    /// Each language's words as the game shows them, en first, in the order of <paramref name="codes"/>: a key a later
    /// language lacks reads in English, as the game falls back to it. A required language whose file is missing is null.
    /// </summary>
    private static Dictionary<string, string>[] LanguageWords(out string[] codes)
    {
        string folder = LocalesFolder();
        codes = ModLanguages.Present(folder);
        var words = new Dictionary<string, string>[codes.Length];
        for (int l = 0; l < codes.Length; l++)
        {
            words[l] = LoadWords(System.IO.Path.Combine(folder, codes[l] + ".json"));
            if (l >= ModLanguages.Required && words[l] != null && words[0] != null)
            {
                var shown = new Dictionary<string, string>(words[0], StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> own in words[l])
                {
                    shown[own.Key] = own.Value;
                }
                words[l] = shown;
            }
        }
        return words;
    }

    /// <summary>True when every language's file was read; otherwise one failed check, named for <paramref name="what"/>.</summary>
    private static bool LanguagesRead(string what, Dictionary<string, string>[] words, string[] codes)
    {
        var lost = new List<string>();
        for (int l = 0; l < words.Length; l++)
        {
            if (words[l] == null)
            {
                lost.Add(codes[l] + ".json");
            }
        }
        Check(lost.Count == 0, what + ": every language's file is where the checks look for it (" + LocalesFolder() + "; not read: " + string.Join(", ", lost) + ")");
        return lost.Count == 0;
    }

    /// <summary>The languages a key is missing from, empty in, or written with other names in curly brackets than the English; empty when it is in every one.</summary>
    private static string LanguagesLacking(Dictionary<string, string>[] words, string[] codes, string key)
    {
        string want = words[0].TryGetValue(key, out string e) ? NamesOf(e) : null;
        string lacking = string.Empty;
        for (int l = 0; l < words.Length; l++)
        {
            if (!words[l].TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text) || NamesOf(text) != want)
            {
                lacking += (lacking.Length > 0 ? ", " : string.Empty) + codes[l];
            }
        }
        return lacking;
    }

    private static string NamesOf(string text)
    {
        var found = new List<string>();
        foreach (Match m in Names.Matches(text))
        {
            found.Add(m.Value);
        }
        found.Sort(StringComparer.Ordinal);
        return string.Join(",", found);
    }

    // A text's {names}, sorted (the checks of features written before the seven languages compare them by this).
    private static string Holes(Regex holes, string text)
    {
        var found = new List<string>();
        foreach (Match m in holes.Matches(text))
        {
            found.Add(m.Value);
        }
        found.Sort(StringComparer.Ordinal);
        return string.Join(",", found);
    }
}
