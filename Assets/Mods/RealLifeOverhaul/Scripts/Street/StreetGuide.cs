using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// The feature's entry in the feature list, and its Guide page's figures, each filled from <see cref="StreetRules"/>
    /// when the page is opened.
    /// </summary>
    public static class StreetGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("street", "reallife_meta_title_street")
                .Group("reallife_street_header")
                .About("reallife_guide_street_about").Help("reallife_guide_street")
                .Owns(Settings.StreetTalkId, Settings.StreetBrushId)
                .Preset(Settings.StreetBrushId, 0, 1, 2)
                .Data(StreetStore.Key)
                .Test("reallife_street_test_talk", "reallife_street_test_forget")
                .Fact("street_police", () => ((int)StreetRules.PoliceMetres).ToString(Inv))
                .Fact("street_find", () => ((int)StreetRules.FindMetres).ToString(Inv))
                .Fact("street_mouth", () => StreetRules.MouthPercent.ToString(Inv))
                .Fact("street_mouth_days", () => StreetRules.MouthDays.ToString(Inv))
                .Fact("street_mouth_most", () => StreetRules.MouthMost.ToString(Inv))
                .Fact("street_mouth_day", () => StreetRules.MouthPerDay.ToString(Inv))
                .Fact("street_hold", () => StreetRules.Hold.ToString("0.#", Inv))
                .Fact("street_turns", () => StreetRules.PatienceLeast.ToString(Inv))
                .Fact("street_turns_most", () => StreetRules.PatienceMost.ToString(Inv))
                .Cleanup(() => StreetDirector.Instance?.StopAll())
                .ResetData(() => StreetDirector.Instance?.Forget());
        }
    }

    /// <summary>Street talk's block on the options page, as an extension so it is one line there.</summary>
    public static class StreetOptions
    {
        public static ModOptions AddStreetTalk(this ModOptions options)
        {
            return options
                .AddHeader("reallife_street_header")
                .AddToggle(Settings.StreetTalkId, "reallife_street_enabled", Settings.StreetTalk, v => Settings.StreetTalk = v)
                .AddDropdown(Settings.StreetBrushId, "reallife_street_opt_brush",
                    new[] { "reallife_street_brush_0", "reallife_street_brush_1", "reallife_street_brush_2" }, Settings.StreetBrush, v => Settings.StreetBrush = v)
                .AddButton("reallife_street_test_talk", () => StreetDirector.Instance?.TestTalk())
                .AddButton("reallife_street_test_forget", () => StreetDirector.Instance?.TestForget())
                .AddSplitter();
        }
    }
}
