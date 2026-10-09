using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Mods;
using Extensions;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The justice system's options (group "Justice" on the options page), read from PlayerPrefs at
    /// game start like every other option of the mod, and its test buttons.
    /// </summary>
    public static class JusticeSettings
    {
        public const string EnabledId = "justice";
        public const string LawyersId = "justice_lawyers";
        public const string CourthouseId = "justice_courthouse";
        public const string NoPrisonId = "justice_no_prison";
        public const string PrisonCapId = "justice_prison_cap";
        public const string CommunityId = "justice_community";
        public const string CivilId = "justice_civil";
        public const string AftermathId = "justice_aftermath";
        public const string PhoneId = "justice_phone";

        public static bool Enabled = true;        // cases, bail, court dates, verdicts and sentences
        public static bool Lawyers = true;        // the city's law firms can be retained (off: the public defender or yourself)
        public static bool Courthouse = true;     // hearings are attended in person at the courthouse (off: by video link from the Legal file)
        public static bool NoPrison = false;      // "No prison: fines and custody only"
        public static int PrisonCap = JusticeLaw.PrisonCapDefault;   // the longest prison term, in-game days (3 to 14)
        public static bool Community = true;      // community service is a played shift (off: it becomes a fine)
        public static bool Civil = true;          // victims of the player's crashes may sue
        public static bool Aftermath = true;      // appeals, record sealing, compensation for a wrongful arrest
        public static bool Phone = true;          // the Legal contact and file on the phone

        public static void Load(string modId)
        {
            Enabled = Bool(modId, EnabledId, Enabled);
            Lawyers = Bool(modId, LawyersId, Lawyers);
            Courthouse = Bool(modId, CourthouseId, Courthouse);
            NoPrison = Bool(modId, NoPrisonId, NoPrison);
            PrisonCap = JusticeLaw.ClampCap(UnityEngine.PlayerPrefs.GetInt(Key(modId, PrisonCapId), PrisonCap));
            Community = Bool(modId, CommunityId, Community);
            Civil = Bool(modId, CivilId, Civil);
            Aftermath = Bool(modId, AftermathId, Aftermath);
            Phone = Bool(modId, PhoneId, Phone);
        }

        /// <summary>The "Justice" block of the options page: toggles, the prison cap, and the test buttons.</summary>
        public static ModOptions AddJustice(this ModOptions options)
        {
            return options
                .AddHeader("reallife_justice_header")
                .AddToggle(EnabledId, "reallife_justice_enabled", Enabled, v => Enabled = v)
                .AddToggle(LawyersId, "reallife_justice_lawyers", Lawyers, v => Lawyers = v)
                .AddToggle(CourthouseId, "reallife_justice_courthouse", Courthouse, v => Courthouse = v)
                .AddToggle(NoPrisonId, "reallife_justice_no_prison", NoPrison, v => NoPrison = v)
                .AddSlider(PrisonCapId, "reallife_justice_prison_cap", JusticeLaw.PrisonCapMin, JusticeLaw.PrisonCapMax, PrisonCap,
                    v => PrisonCap = JusticeLaw.ClampCap(v))
                .AddToggle(CommunityId, "reallife_justice_community", Community, v => Community = v)
                .AddToggle(CivilId, "reallife_justice_civil", Civil, v => Civil = v)
                .AddToggle(AftermathId, "reallife_justice_aftermath", Aftermath, v => Aftermath = v)
                .AddToggle(PhoneId, "reallife_justice_phone", Phone, v => Phone = v)
                .AddButton("reallife_justice_test_case_minor", () => JusticeDirector.Instance?.TestCase(0))
                .AddButton("reallife_justice_test_case_serious", () => JusticeDirector.Instance?.TestCase(1))
                .AddButton("reallife_justice_test_case_grave", () => JusticeDirector.Instance?.TestCase(2))
                .AddButton("reallife_justice_test_case_weak", () => JusticeDirector.Instance?.TestCase(3))
                .AddButton("reallife_justice_test_court_date", () => JusticeDirector.Instance?.TestCourtDate())
                .AddButton("reallife_justice_test_hearing", () => JusticeDirector.Instance?.TestHearing())
                .AddButton("reallife_justice_test_prison_short", () => JusticeDirector.Instance?.TestPrison(JusticeLaw.PrisonCapMin))
                .AddButton("reallife_justice_test_prison_cap", () => JusticeDirector.Instance?.TestPrison(PrisonCap))
                .AddButton("reallife_justice_test_parole", () => JusticeDirector.Instance?.TestParole())
                .AddButton("reallife_justice_test_civil", () => JusticeDirector.Instance?.TestCivil())
                .AddButton("reallife_justice_test_community", () => JusticeDirector.Instance?.TestCommunity())
                .AddButton("reallife_justice_test_file", () => JusticeDirector.Instance?.TestFile())
                .AddButton("reallife_justice_test_clear", () => JusticeDirector.Instance?.TestClear());
        }

        // Same key format the game uses when it saves mod options (as in Core/Settings).
        private static string Key(string modId, string id) => "m:" + modId + ":" + id;

        private static bool Bool(string modId, string id, bool fallback) => UnityEngine.PlayerPrefs.GetInt(Key(modId, id), fallback ? 1 : 0) != 0;
    }

    /// <summary>
    /// Why a justice test button (or a shift of community service) did not start. The causes every test of the mod
    /// shares are told through Core/TestRefusal (not loaded, option off, the police have the player); the justice
    /// system's own each have one sentence in en.json, the same contract.
    /// </summary>
    public enum JusticeRefusal
    {
        None,
        NotLoaded,      // no save, or the mod's part of it is not ready                  (Core: TestRefusal.NotLoaded)
        FeatureOff,     // the justice system is switched off                              (Core: TestRefusal.FeatureOff)
        PoliceBusy,     // the police already have business with the player                (Core: TestRefusal.PoliceBusy)
        PartOff,        // the part the button tests is switched off, or "No prison" is on
        Broken,         // it switched itself off after an error this session
        Busy,           // a hearing, a custody, a shift or the limit of open cases is in the way
        NoCase,         // there is no open case
        InPrison,       // not while serving a term
        Indoors,        // the shift is worked on foot in the street
        NoPavement,     // no clear pavement near enough to put litter on
        Night           // the litter crews do not work at night
    }

    public static class JusticeRefusals
    {
        public static void Tell(IModLogger logger, string test, JusticeRefusal why, string detail = null)
        {
            switch (why)
            {
                case JusticeRefusal.NotLoaded:
                    TestRefusals.Tell(logger, "Justice: " + test, TestRefusal.NotLoaded, 0, detail);
                    return;
                case JusticeRefusal.FeatureOff:
                    TestRefusals.Tell(logger, "Justice: " + test, TestRefusal.FeatureOff, 0, detail);
                    return;
                case JusticeRefusal.PoliceBusy:
                    TestRefusals.Tell(logger, "Justice: " + test, TestRefusal.PoliceBusy, 0, detail);
                    return;
            }
            logger?.Info("Justice: " + test + " test: not started (" + why + (detail != null ? "; " + detail : string.Empty) + ").");
            Notifications.Show(NotificationType.Warning, KeyOf(why), null, 8f);
        }

        private static string KeyOf(JusticeRefusal why)
        {
            switch (why)
            {
                case JusticeRefusal.PartOff: return "reallife_justice_refused_part_off";
                case JusticeRefusal.Broken: return "reallife_justice_refused_broken";
                case JusticeRefusal.NoCase: return "reallife_justice_refused_no_case";
                case JusticeRefusal.InPrison: return "reallife_justice_refused_in_prison";
                case JusticeRefusal.Indoors: return "reallife_justice_refused_indoors";
                case JusticeRefusal.NoPavement: return "reallife_justice_refused_no_pavement";
                case JusticeRefusal.Night: return "reallife_justice_refused_night";
                default: return "reallife_justice_refused_busy";
            }
        }
    }

    /// <summary>
    /// Text for the justice system's own screens. Every call builds a string, so these are for
    /// when a page or a prompt changes, never per frame. {key} in any text is the interaction key
    /// as bound now (Core/Keys).
    /// </summary>
    public static class JusticeText
    {
        public static string T(string key)
        {
            string text = key.GetLocalization();
            return text != null && text.IndexOf("{key}", System.StringComparison.Ordinal) >= 0 ? text.Replace("{key}", Keys.Label(KeyAction.Interact)) : text;
        }

        public static string T(string key, string name, string value)
        {
            return T(key).Replace("{" + name + "}", value);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return T(key).Replace("{" + name + "}", value).Replace("{" + name2 + "}", value2);
        }

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3)
        {
            return T(key).Replace("{" + name + "}", value).Replace("{" + name2 + "}", value2).Replace("{" + name3 + "}", value3);
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }

        public static string N(int number)
        {
            return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>A share as a whole percentage: 0.734 is "73".</summary>
        public static string Percent(float share)
        {
            return N(Mathf.RoundToInt(share * 100f));
        }

        /// <summary>The name of a charge as shown.</summary>
        public static string Charge(string id)
        {
            ChargeDef def = JusticeLaw.Find(id);
            return def != null ? T(def.NameKey) : id;
        }

        public static string Stars(int stars)
        {
            return N(Mathf.Clamp(stars, 1, 5)) + "/5";   // (the game's font is not sure to have a star)
        }

        public static void Toast(NotificationType type, string key, float seconds = 9f)
        {
            Notifications.Show(type, key, null, seconds);
        }

        public static void Toast(NotificationType type, string key, string name, string value, float seconds = 9f)
        {
            // A new dictionary each time: the game keeps hold of it for as long as the toast is listed.
            Notifications.Show(type, key, new Dictionary<string, string>(1) { { name, value } }, seconds);
        }

        public static void Toast(NotificationType type, string key, string name, string value, string name2, string value2, float seconds = 9f)
        {
            Notifications.Show(type, key, new Dictionary<string, string>(2) { { name, value }, { name2, value2 } }, seconds);
        }
    }
}
