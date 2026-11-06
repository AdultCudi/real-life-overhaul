using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Offline checks of the names the city map's filters and BizMan show for business types. The map makes a row per
    /// business type a player can found and labels it with the type's name, in lower case, as a text key
    /// (CityMapFilters.CreateActiveBusinessesCategory), so:
    ///   - no business type of the mod's is named in en as one of the game's own is (the mod's car dealership was
    ///     "Car Dealership", the game's car dealer's name, until it became the Car Showroom);
    ///   - the type names saves hold are as they were (only their texts change);
    ///   - the Car Showroom has its name in all three languages;
    ///   - Empire Motors' dealership type, whose own locale files the game cannot read, has a text under the exact key
    ///     the map asks for, in all three languages, and it is the only key of another mod's that the locales hold.
    /// The type names are read from the sources as text, the game's names from its own en.json.
    /// </summary>
    internal static class TypeNameCheck
    {
        private const string GameLocale = @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\StreamingAssets\locale\en.json";
        private const string ShowroomKey = "reallife_businesstype_cardealership";   // the type's name: saves hold it, so it never changes
        private const string EmpireKey = "empiremotors:businesstype_cardealership"; // Empire Motors' type name in lower case, as the map asks for it

        /// <summary>The keys of other mods' that the mod's locales may hold, each with its reason: everything else starts "reallife".</summary>
        public static readonly HashSet<string> OtherModsKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            EmpireKey,   // the city map's row for Empire Motors' dealership: its own files are not in the game's format, so the game has no text for it
            // The Contacts app's groups: the game asks for a category's name by its number (ContactCategoryName.GetLocalizeKey), the mod's seven are 101 to 107
            ContactGroups.NameKey(ContactGroup.Family), ContactGroups.NameKey(ContactGroup.Friends), ContactGroups.NameKey(ContactGroup.Work), ContactGroups.NameKey(ContactGroup.Services),
            ContactGroups.NameKey(ContactGroup.Money), ContactGroups.NameKey(ContactGroup.City), ContactGroups.NameKey(ContactGroup.Other)
        };

        public static void Run(Action<string> fail, Dictionary<string, string> en, string src)
        {
            Console.WriteLine("\n== Business type names: the map's rows and BizMan ==");
            int checks = 0;
            void Check(bool ok, string message)
            {
                checks++;
                if (!ok) fail("type names: " + message);
            }

            Dictionary<string, string> pt = Read(Path.Combine(src, "Locales", "pt.json"));
            Dictionary<string, string> zh = Read(Path.Combine(src, "Locales", "zh-cn.json"));

            // The saved type names, as the sources declare them.
            string scripts = Path.Combine(src, "Scripts");
            string catalogue = File.ReadAllText(Path.Combine(scripts, "Economy", "Dealer", "DealerCatalogue.cs"));
            Check(catalogue.Contains("TypeName = \"" + ShowroomKey + "\""), "the Car Showroom's type name is no longer " + ShowroomKey + ": saves hold it");
            string empire = File.ReadAllText(Path.Combine(scripts, "Core", "EmpireMotors.cs"));
            Match em = Regex.Match(empire, "DealershipType = \"([^\"]+)\"");
            Check(em.Success && em.Groups[1].Value.ToLowerInvariant() == EmpireKey, "Core/EmpireMotors.DealershipType is not " + EmpireKey + " in lower case");

            // The Car Showroom's name.
            Check(Text(en, ShowroomKey) == "Car Showroom", "en " + ShowroomKey + " is '" + Text(en, ShowroomKey) + "', wanted 'Car Showroom'");
            Check(Text(pt, ShowroomKey) == "Showroom de Carros", "pt " + ShowroomKey + " is '" + Text(pt, ShowroomKey) + "'");
            Check(Text(zh, ShowroomKey) == "汽车展厅", "zh-cn " + ShowroomKey + " is '" + Text(zh, ShowroomKey) + "'");
            Check(Text(en, "reallife_dealer_app") == "Car Showroom", "the app's name in en is '" + Text(en, "reallife_dealer_app") + "'");

            // No business type of the mod's named as one of the game's.
            int types = 0;
            if (File.Exists(GameLocale))
            {
                var game = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> p in Read(GameLocale))
                {
                    if (p.Key.StartsWith("ba:businesstype_", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.Value))
                        game[p.Value.Trim()] = p.Key;
                }
                foreach (KeyValuePair<string, string> p in en.Where(x => x.Key.StartsWith("reallife_businesstype_", StringComparison.Ordinal) || x.Key.StartsWith("reallife:businesstype_", StringComparison.Ordinal)))
                {
                    types++;
                    Check(!game.TryGetValue(p.Value.Trim(), out string same), p.Key + " is named '" + p.Value + "', as the game's " + same + " is");
                }
                Check(types > 0, "no business type of the mod's has a text in en.json");
            }
            else
            {
                Console.WriteLine("  (the game's en.json is not at " + GameLocale + ": the names are not compared with the game's)");
            }

            // Empire Motors' row: a readable text under the exact key, in all three; no other key of another mod's.
            Check(Text(en, EmpireKey) == "Empire Motors dealership", "en " + EmpireKey + " is '" + Text(en, EmpireKey) + "'");
            Check(Text(pt, EmpireKey).Length > 0 && Text(pt, EmpireKey) != Text(en, EmpireKey), "pt has no text of its own for " + EmpireKey);
            Check(Regex.IsMatch(Text(zh, EmpireKey), @"\p{IsCJKUnifiedIdeographs}"), "zh-cn has no Chinese text for " + EmpireKey);
            string[] languages = ModLanguages.Present(Path.Combine(src, "Locales"));
            for (int i = ModLanguages.Required; i < languages.Length; i++)
            {
                // A later language may lack a key (it reads in English); a text it has is its own.
                Dictionary<string, string> other = Read(Path.Combine(src, "Locales", languages[i] + ".json"));
                foreach (string key in new[] { ShowroomKey, EmpireKey })
                    Check(!other.ContainsKey(key) || (Text(other, key).Length > 0 && Text(other, key) != Text(en, key)), languages[i] + " has the English for " + key + ", not a text of its own");
            }
            foreach (string key in en.Keys.Where(k => !k.StartsWith("reallife", StringComparison.Ordinal)))
                Check(OtherModsKeys.Contains(key), key + " is a key of another mod's that has no reason here");

            Console.WriteLine($"  {types} business type(s) of the mod's compared with the game's names; {checks} checks");
        }

        private static string Text(Dictionary<string, string> texts, string key)
        {
            return texts.TryGetValue(key, out string v) ? v : string.Empty;
        }

        private static Dictionary<string, string> Read(string path)
        {
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String)
                        texts[p.Name] = p.Value.GetString();
                }
            }
            return texts;
        }
    }
}
