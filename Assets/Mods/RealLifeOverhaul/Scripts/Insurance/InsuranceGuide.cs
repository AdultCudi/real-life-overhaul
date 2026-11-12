using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// Insurance's entry in the feature list, its keys written out whole as the options checks read them. Its Guide page's numbers are facts filled from
    /// <see cref="InsuranceCatalogue"/> when the page opens.
    /// </summary>
    public static class InsuranceGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("insurance", "reallife_meta_title_insurance")
                .Group("reallife_ins_header")
                .About("reallife_guide_insurance_about").Help("reallife_guide_insurance")
                .Owns(Settings.InsuranceId)
                .Data(InsuranceStore.Key, InsuranceDirector.TestKey)
                .Test("reallife_ins_test_btn_open", "reallife_ins_test_btn_home", "reallife_ins_test_btn_crash", "reallife_ins_test_btn_missed", "reallife_ins_test_btn_back")
                .Fact("ins_grace", () => InsuranceCatalogue.GraceDays.ToString(Inv))
                .Fact("ins_car_wait", () => InsuranceCatalogue.CarWaitDays.ToString(Inv))
                .Fact("ins_home_wait", () => InsuranceCatalogue.HomeWaitDays.ToString(Inv))
                .Fact("ins_noclaim_step", () => Guide.Percent(InsuranceCatalogue.NoClaimStep / 1000f))
                .Fact("ins_noclaim_most", () => Guide.Percent(InsuranceCatalogue.NoClaimMost / 1000f))
                .Fact("ins_point", () => Guide.Percent(InsuranceCatalogue.PointShare / 1000f))
                .Fact("ins_crash", () => Guide.Percent(InsuranceCatalogue.CrashShare / 1000f))
                .Fact("ins_crash_days", () => InsuranceCatalogue.CrashDays.ToString(Inv))
                .Fact("ins_home_claim", () => Guide.Percent(InsuranceCatalogue.HomeClaimLoading / 1000f))
                .Fact("ins_car_claims", () => InsuranceCatalogue.CarYearClaims[0].ToString(Inv) + ", " + InsuranceCatalogue.CarYearClaims[1].ToString(Inv) + ", "
                    + InsuranceCatalogue.CarYearClaims[2].ToString(Inv))
                .Fact("ins_home_claims", () => InsuranceCatalogue.HomeYearClaims[0].ToString(Inv) + ", " + InsuranceCatalogue.HomeYearClaims[1].ToString(Inv) + ", "
                    + InsuranceCatalogue.HomeYearClaims[2].ToString(Inv))
                .ResetData(() => InsuranceDirector.Instance?.Forget());
        }
    }

    /// <summary>Insurance's place on the options page.</summary>
    public static class InsuranceOptions
    {
        public static ModOptions AddInsurance(this ModOptions options)
        {
            return options
                .AddHeader("reallife_ins_header")
                .AddToggle(Settings.InsuranceId, "reallife_ins_enabled", Settings.Insurance, v => { Settings.Insurance = v; })
                .AddButton("reallife_ins_test_btn_open", () => InsuranceDirector.Instance?.TestOpen())
                .AddButton("reallife_ins_test_btn_home", () => InsuranceDirector.Instance?.TestHomeClaim())
                .AddButton("reallife_ins_test_btn_crash", () => InsuranceDirector.Instance?.TestCrashClaim())
                .AddButton("reallife_ins_test_btn_missed", () => InsuranceDirector.Instance?.TestMissed())
                .AddButton("reallife_ins_test_btn_back", () => InsuranceDirector.Instance?.TestBack())
                .AddSplitter();
        }
    }
}
