using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Mods;
using Extensions;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The justice system's options and test buttons (group "Justice"), read from PlayerPrefs at game start.</summary>
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
        public static bool Lawyers = true;        // off: the public defender or yourself
        public static bool Courthouse = true;     // off: hearings by video link from the Legal file
        public static bool NoPrison = false;      // fines and custody only
        public static int PrisonCap = JusticeLaw.PrisonCapDefault;   // in-game days, 3 to 14
        public static bool Community = true;      // off: the hours become a fine
        public static bool Civil = true;          // victims of the player's crashes may sue
        public static bool Aftermath = true;      // appeals, record sealing, compensation for a wrongful arrest
        public static bool Phone = true;          // the Legal contact and file on the phone

        public static void Load()
        {
            Enabled = Bool(EnabledId, Enabled);
            Lawyers = Bool(LawyersId, Lawyers);
            Courthouse = Bool(CourthouseId, Courthouse);
            NoPrison = Bool(NoPrisonId, NoPrison);
            PrisonCap = JusticeLaw.ClampCap(UnityEngine.PlayerPrefs.GetInt(OptionKeysRules.Key(PrisonCapId), PrisonCap));
            Community = Bool(CommunityId, Community);
            Civil = Bool(CivilId, Civil);
            Aftermath = Bool(AftermathId, Aftermath);
            Phone = Bool(PhoneId, Phone);
        }

        public static ModOptions AddJustice(this ModOptions options)
        {
            return options
                .AddHeader("reallife_justice_header")
                .AddToggle(EnabledId, "reallife_justice_enabled", Enabled, v => Enabled = v)
                .AddToggle(LawyersId, "reallife_justice_lawyers", Lawyers, v => Lawyers = v)
                .AddToggle(CourthouseId, "reallife_justice_courthouse", Courthouse, v => Courthouse = v)
                .AddToggle(NoPrisonId, "reallife_justice_no_prison", NoPrison, v => NoPrison = v)
                .AddSlider(PrisonCapId, "reallife_justice_prison_cap", JusticeLaw.PrisonCapMin, JusticeLaw.PrisonCapMax, PrisonCap,
                    v => PrisonCap = JusticeLaw.ClampCap(v), Meta.SettingsTable.SliderValue)
                .AddToggle(CommunityId, "reallife_justice_community", Community, v => Community = v)
                .AddToggle(CivilId, "reallife_justice_civil", Civil, v => Civil = v)
                .AddToggle(AftermathId, "reallife_justice_aftermath", Aftermath, v => Aftermath = v)
                .AddToggle(PhoneId, "reallife_justice_phone", Phone, v => Phone = v)
                .AddButton("reallife_justice_test_case_minor", () => JusticeDirector.Instance?.TestCase(0))
                .AddButton("reallife_justice_test_case_serious", () => JusticeDirector.Instance?.TestCase(1))
                .AddButton("reallife_justice_test_case_grave", () => JusticeDirector.Instance?.TestCase(2))
                .AddButton("reallife_justice_test_case_weak", () => JusticeDirector.Instance?.TestCase(3))
                .AddButton("reallife_justice_test_camera", () => JusticeDirector.Instance?.TestCameraDismissed())
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

        private static bool Bool(string id, bool fallback) => UnityEngine.PlayerPrefs.GetInt(OptionKeysRules.Key(id), fallback ? 1 : 0) != 0;
    }

    /// <summary>Why a justice test button, or a shift of community service, did not start.</summary>
    public enum JusticeRefusal
    {
        None,
        NotLoaded,      // told as Core's TestRefusal.NotLoaded
        FeatureOff,     // told as Core's TestRefusal.FeatureOff
        PoliceBusy,     // told as Core's TestRefusal.PoliceBusy
        PartOff,        // the part tested is switched off, or "No prison" is on
        Broken,         // switched itself off after an error this session
        Busy,           // a hearing, a custody, a shift or the limit of open cases is in the way
        NoCase,
        InPrison,
        Indoors,        // a shift is worked on foot in the street
        NoPavement,     // nowhere clear near enough to put litter
        Night
    }

    /// <summary>Tells a <see cref="JusticeRefusal"/>: the causes all tests share through Core/TestRefusal, the rest as a notice of its own.</summary>
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

    /// <summary>Texts for the justice system's screens, built when a page changes, never per frame. {key} is the interaction key.</summary>
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

        /// <summary>A charge's name as shown; the id itself for one this build does not know.</summary>
        public static string Charge(string id)
        {
            ChargeDef def = JusticeLaw.Find(id);
            return def != null ? T(def.NameKey) : id;
        }

        public static string Stars(int stars)
        {
            return N(Mathf.Clamp(stars, 1, 5)) + "/5";   // the game's font may lack a star
        }

        public static void Toast(NotificationType type, string key, float seconds = 9f)
        {
            Notifications.Show(type, key, null, seconds);
        }

        public static void Toast(NotificationType type, string key, string name, string value, float seconds = 9f)
        {
            // A new dictionary each time: the game keeps it for as long as the toast is listed.
            Notifications.Show(type, key, new Dictionary<string, string>(1) { { name, value } }, seconds);
        }

        public static void Toast(NotificationType type, string key, string name, string value, string name2, string value2, float seconds = 9f)
        {
            Notifications.Show(type, key, new Dictionary<string, string>(2) { { name, value }, { name2, value2 } }, seconds);
        }
    }
}
