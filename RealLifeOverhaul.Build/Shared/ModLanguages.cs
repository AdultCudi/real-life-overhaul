using System.Collections.Generic;
using System.IO;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's languages, for the offline checkers (linked into each; nothing of it ships). A language is the file
    /// Locales/&lt;code&gt;.json, named by the code the game gives the language in its own StreamingAssets/locale/locale.json:
    /// the game reads a mod's Locales/&lt;code&gt;.json for the language the player chose and for its fallback, English, and
    /// looks a key up in the first and then the second, so a language without a file, or a key a file lacks, is read in
    /// English (Localizor.LocalizorManager.LoadLocalizationTables and GetLocalization). en, pt and zh-cn always ship with
    /// every key; German, French, Spanish and Russian are checked as soon as their file is there, and may lack keys, which
    /// read in English until they are translated. Kept in step with
    /// Workshop/locales.py's LANGUAGES (ContractCheck's LanguageChecks holds the two, and the game's codes, to each other).
    /// </summary>
    internal static class ModLanguages
    {
        public static readonly string[] All = { "en", "pt", "zh-cn", "de", "fr", "es", "ru" };
        public const int Required = 3;   // en, pt and zh-cn: the first three of All, which every release carries

        /// <summary>The codes of the languages to check in a Locales folder: the required ones always (a missing file is then the caller's fault to report), the others when their file is there.</summary>
        public static string[] Present(string folder)
        {
            var codes = new List<string>(All.Length);
            for (int i = 0; i < All.Length; i++)
            {
                if (i < Required || File.Exists(Path.Combine(folder, All[i] + ".json")))
                {
                    codes.Add(All[i]);
                }
            }
            return codes.ToArray();
        }
    }
}
