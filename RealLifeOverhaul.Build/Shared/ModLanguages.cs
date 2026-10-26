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
    /// every key; German, French, Spanish, Russian, Dutch, Turkish, Polish and Korean are checked as soon as their file is there, and may lack
    /// keys, which read in English until they are translated. Kept in step with
    /// Workshop/locales.py's LANGUAGES and FIRST_BATCH (ContractCheck's LanguageChecks holds them, and the game's codes, to each other).
    ///
    /// The per-feature checks write each What's new line and each page's key words by hand in the first nine languages
    /// (<see cref="PerFeature"/>); Polish and Korean are held by the whole-file rules of LaterLanguageChecks instead (every
    /// What's new row, every key, the marks), which need no line written for them. <see cref="FirstBatch"/> lists the
    /// languages whose first full translation is not merged yet: their file holds only what the gate needs, and the rules
    /// that want a complete translation (every key, every What's new row, the languages named in the description, the README,
    /// the Guide's Known limits page and the installer) wait. Empty it when their translation is merged.
    /// </summary>
    internal static class ModLanguages
    {
        public static readonly string[] All = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };
        public const int Required = 3;   // en, pt and zh-cn: the first three of All, which every release carries

        /// <summary>The nine languages the per-feature checks write lines for by hand: the first nine of <see cref="All"/>.</summary>
        public static readonly string[] PerFeature = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };

        /// <summary>The languages whose first full translation is not merged yet (Workshop/locales.py's FIRST_BATCH holds the same list).</summary>
        public static readonly string[] FirstBatch = { "pl", "ko" };

        /// <summary>True for a language in its first batch.</summary>
        public static bool InFirstBatch(string code)
        {
            return System.Array.IndexOf(FirstBatch, code) >= 0;
        }

        /// <summary>The languages the mod names to its players (the Workshop description, the README, the Guide, the installer): all but those in their first batch.</summary>
        public static string[] Listed()
        {
            var codes = new List<string>(All.Length);
            for (int i = 0; i < All.Length; i++)
            {
                if (!InFirstBatch(All[i]))
                {
                    codes.Add(All[i]);
                }
            }
            return codes.ToArray();
        }

        /// <summary>The codes of the per-feature languages to check in a Locales folder: the required ones always (a missing file is then the caller's fault to report), the others of <see cref="PerFeature"/> when their file is there.</summary>
        public static string[] Present(string folder)
        {
            var codes = new List<string>(PerFeature.Length);
            for (int i = 0; i < PerFeature.Length; i++)
            {
                if (i < Required || File.Exists(Path.Combine(folder, PerFeature[i] + ".json")))
                {
                    codes.Add(PerFeature[i]);
                }
            }
            return codes.ToArray();
        }
    }
}
