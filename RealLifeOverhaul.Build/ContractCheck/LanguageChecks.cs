using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// The mod's languages (Shared/ModLanguages.cs) against the game on disk. The game lists its languages in
// StreamingAssets/locale/locale.json (code, then name) and reads, for the language the player chose and for its fallback
// language, its own <code>.json and then each mod's Locales/<code>.json (Localizor.LocalizorManager.LoadLocalizationTables,
// in HGPlugins.dll; the mods' folders come from BigAmbitions.ModsInternal.ModDiscoveryRegistry). A key is looked up in the
// chosen language and then in the fallback (GetLocalization). So each of the mod's codes must be one of the game's, the
// fallback must be English, and none of the mod's languages may be one the game ignores: then a language without a file of
// the mod's, or a key its file lacks, is read in English and never shown as a raw key.
internal static partial class Program
{
    // The name the game's index gives each of the mod's languages, as it begins.
    private static readonly Dictionary<string, string> GameLanguageNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "en", "English" }, { "pt", "Portuguese (BR)" }, { "zh-cn", "Chinese (Simplified)" }, { "de", "German" }, { "fr", "French" },
        { "es", "Spanish" }, { "ru", "Russian" }, { "nl", "Dutch" }, { "tr", "Turkish" }
    };

    private static void Languages(string managed, string root)
    {
        Section("the mod's languages against the game's: " + string.Join(", ", ModLanguages.All));
        string data = Path.GetFullPath(Path.Combine(managed, ".."));
        string gameLocales = Path.Combine(data, "StreamingAssets", "locale");
        string indexPath = Path.Combine(gameLocales, "locale.json");
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        if (File.Exists(indexPath))
        {
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(indexPath)))
            {
                foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    index[p.Name.ToLowerInvariant()] = p.Value.GetString();   // the game lowers the codes as it reads them
                }
            }
        }
        Check(index.Count >= ModLanguages.All.Length, "languages: the game's index of languages was not read (" + indexPath + ")");
        Check(GameLanguageNames.Count == ModLanguages.All.Length, "languages: a name to hold each of the mod's languages to");
        foreach (string code in ModLanguages.All)
        {
            Check(index.TryGetValue(code, out string name) && GameLanguageNames.TryGetValue(code, out string want) && name.StartsWith(want, StringComparison.Ordinal),
                "languages: " + code + " is not the game's code for " + (GameLanguageNames.TryGetValue(code, out string w) ? w : "?") + " (the game's index has " + (name ?? "nothing") + ")");
            Check(File.Exists(Path.Combine(gameLocales, code + ".json")), "languages: the game has no texts of its own in " + code + ".json");
        }

        // The game's settings: what it starts in, what it falls back to, what it leaves out.
        string defaultLanguage, fallback;
        List<string> ignored;
        string how = ReadLocalizorSettings(data, out defaultLanguage, out fallback, out ignored);
        Check(how == null, "languages: " + how);
        if (how == null)
        {
            Check(fallback == "en", "languages: the game falls back to " + fallback + ", not English: a key the player's language lacks would not be read in English");
            foreach (string code in ModLanguages.All)
            {
                Check(!ignored.Contains(code), "languages: the game ignores " + code);
            }
            Console.WriteLine("   the game starts in " + defaultLanguage + " and falls back to " + fallback + "; it ignores " + (ignored.Count == 0 ? "no language" : string.Join(", ", ignored)) + ".");
        }

        // The mod's own files: only its languages, the ones every release carries there.
        string modLocales = Path.Combine(root, @"Assets\Mods\RealLifeOverhaul\Locales");
        var known = new HashSet<string>(ModLanguages.All, StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(modLocales, "*.json"))
        {
            string code = Path.GetFileNameWithoutExtension(file);
            Check(known.Contains(code), "languages: Locales/" + Path.GetFileName(file) + " is not one of the mod's languages (Shared/ModLanguages.cs)");
        }
        for (int i = 0; i < ModLanguages.Required; i++)
        {
            Check(File.Exists(Path.Combine(modLocales, ModLanguages.All[i] + ".json")), "languages: Locales/" + ModLanguages.All[i] + ".json is missing");
        }
        Check(ModLanguages.All[0] == "en" && ModLanguages.Present(modLocales)[0] == "en", "languages: English comes first, as the source of every other");

        // Workshop/locales.py, which checks every language's texts, knows the same languages in the same order.
        string script = Path.Combine(root, @"RealLifeOverhaul.Build\Workshop\locales.py");
        string text = File.Exists(script) ? File.ReadAllText(script) : string.Empty;
        Match list = Regex.Match(text, @"LANGUAGES = \((.*?)\)\r?\n", RegexOptions.Singleline);
        var codes = new List<string>();
        foreach (Match m in Regex.Matches(list.Groups[1].Value, @"\('([a-z-]+)', '"))
        {
            codes.Add(m.Groups[1].Value);
        }
        Check(string.Join(",", codes) == string.Join(",", ModLanguages.All), "languages: locales.py's LANGUAGES (" + string.Join(", ", codes) + ") are not ModLanguages.All");
        Match required = Regex.Match(text, @"REQUIRED = \(([^)]*)\)");
        Check(required.Success && required.Groups[1].Value.Replace(" ", string.Empty) == "'" + string.Join("','", ModLanguages.All, 0, ModLanguages.Required) + "'",
            "languages: locales.py's REQUIRED are not the first " + ModLanguages.Required + " of ModLanguages.All");
    }

    // Localizor's settings as the game loads them (Resources/LocalizorSettings, in resources.assets): after the object's
    // name come gameID (an int), storageLocation (an int), ignoredLanguages (a count, then strings), defaultLanguage and
    // fallbackLanguage (strings), each string its length then its bytes, padded to four. Null when read, or what went wrong.
    private static string ReadLocalizorSettings(string data, out string defaultLanguage, out string fallback, out List<string> ignored)
    {
        defaultLanguage = fallback = null;
        ignored = new List<string>();
        string path = Path.Combine(data, "resources.assets");
        if (!File.Exists(path))
        {
            return "the game's resources.assets is not at " + path;
        }
        byte[] bytes = File.ReadAllBytes(path);
        byte[] name = new byte[4 + 17];
        name[0] = 17;
        Encoding.ASCII.GetBytes("LocalizorSettings", 0, 17, name, 4);
        int found = 0;
        for (int at = IndexOf(bytes, name, 0); at >= 0; at = IndexOf(bytes, name, at + 1))
        {
            int read = (at + name.Length + 3) & ~3;
            if (read + 12 > bytes.Length || BitConverter.ToInt32(bytes, read) <= 0 || (uint)BitConverter.ToInt32(bytes, read + 4) > 1)
            {
                continue;
            }
            int count = BitConverter.ToInt32(bytes, read + 8);
            read += 12;
            var list = new List<string>();
            string first = null, second = null;
            bool fits = count >= 0 && count <= 40;
            for (int i = 0; fits && i < count + 2; i++)
            {
                fits = SettingsString(bytes, ref read, out string code);
                if (i < count) list.Add(code);
                else if (i == count) first = code;
                else second = code;
            }
            if (!fits)
            {
                continue;
            }
            found++;
            defaultLanguage = first;
            fallback = second;
            ignored = list;
        }
        return found == 1 ? null : found == 0 ? "the game's language settings (LocalizorSettings) were not found in " + path : found + " language settings were found in " + path;
    }

    private static bool SettingsString(byte[] bytes, ref int read, out string value)
    {
        value = null;
        if (read + 4 > bytes.Length)
        {
            return false;
        }
        int length = BitConverter.ToInt32(bytes, read);
        if (length < 2 || length > 8 || read + 4 + length > bytes.Length)
        {
            return false;
        }
        value = Encoding.ASCII.GetString(bytes, read + 4, length);
        read = (read + 4 + length + 3) & ~3;
        return Regex.IsMatch(value, "^[a-z]{2}(-[a-z]{2})?$");
    }
}
