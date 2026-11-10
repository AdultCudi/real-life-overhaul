using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>
    /// The entry of the dynasty in the feature list, its keys written out whole as the options checks read them. The numbers of its Guide page are facts filled from
    /// <see cref="DynastyCatalogue"/> when the page opens.
    /// </summary>
    public static class DynastyGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("dynasty", "reallife_meta_title_dynasty")
                .Group("reallife_dynasty_header")
                .About("reallife_guide_dynasty_about").Help("reallife_guide_dynasty")
                .Owns(Settings.DynastyId)
                .Data(DynastyStore.Key)
                .Test("reallife_dynasty_test_btn_statement", "reallife_dynasty_test_btn_age", "reallife_dynasty_test_btn_last", "reallife_dynasty_test_btn_rival")
                .Fact("dynasty_threshold", () => Guide.Money((int)DynastyCatalogue.Threshold))
                .Fact("dynasty_rate_low", () => (DynastyCatalogue.BandRate[0] / 10).ToString(Inv) + "%")
                .Fact("dynasty_rate_mid", () => (DynastyCatalogue.BandRate[1] / 10).ToString(Inv) + "%")
                .Fact("dynasty_rate_top", () => (DynastyCatalogue.BandRate[2] / 10).ToString(Inv) + "%")
                .Fact("dynasty_band_low", () => Guide.Money((int)DynastyCatalogue.BandWidth[0]))
                .Fact("dynasty_band_mid", () => Guide.Money((int)(DynastyCatalogue.BandWidth[0] + DynastyCatalogue.BandWidth[1])))
                .Fact("dynasty_window", () => DynastyCatalogue.WindowYears.ToString(Inv))
                .Fact("dynasty_spouse", () => (DynastyCatalogue.SpouseShare / 10).ToString(Inv) + "%")
                .Fact("dynasty_others", () => (DynastyCatalogue.OthersMost / 10).ToString(Inv) + "%")
                .Fact("dynasty_fee", () => Guide.Money(DynastyCatalogue.WillFee))
                .Fact("dynasty_change_fee", () => Guide.Money(DynastyCatalogue.ChangeFee))
                .Fact("dynasty_retire_least", () => DynastyCatalogue.RetireLeast.ToString(Inv))
                .Fact("dynasty_keep_most", () => (DynastyCatalogue.KeepSteps[DynastyCatalogue.KeepSteps.Length - 1] / 10).ToString(Inv) + "%")
                .Fact("dynasty_last_age", () => DynastyCatalogue.LastAge.ToString(Inv))
                .Fact("dynasty_loan_rate", () => (DynastyCatalogue.LoanRate / 10).ToString(Inv) + "%")
                .Fact("dynasty_loan_years", () => DynastyCatalogue.LoanYears.ToString(Inv))
                .Fact("dynasty_fame", () => (DynastyCatalogue.FameShare / 10).ToString(Inv) + "%")
                .ResetData(() => DynastyDirector.Instance?.Forget());
        }
    }

    /// <summary>The place of the dynasty on the options page.</summary>
    public static class DynastyOptions
    {
        public static ModOptions AddDynasty(this ModOptions options)
        {
            return options
                .AddHeader("reallife_dynasty_header")
                .AddToggle(Settings.DynastyId, "reallife_dynasty_enabled", Settings.Dynasty, v => { Settings.Dynasty = v; })
                .AddButton("reallife_dynasty_test_btn_statement", () => DynastyDirector.Instance?.TestStatement())
                .AddButton("reallife_dynasty_test_btn_age", () => DynastyDirector.Instance?.TestRetireAge())
                .AddButton("reallife_dynasty_test_btn_last", () => DynastyDirector.Instance?.TestLastDays())
                .AddButton("reallife_dynasty_test_btn_rival", () => DynastyDirector.Instance?.TestRival())
                .AddSplitter();
        }
    }
}
