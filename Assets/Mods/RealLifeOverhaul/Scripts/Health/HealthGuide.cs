using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Health
{
    /// <summary>
    /// Health's entry in the feature list, its keys written out whole as the options checks read them. Its Guide page's numbers are facts filled from <see cref="HealthCatalogue"/>
    /// when the page opens.
    /// </summary>
    public static class HealthGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("health", "reallife_meta_title_health")
                .Group("reallife_hc_header")
                .About("reallife_guide_health_about").Help("reallife_guide_health")
                .Owns(Settings.HealthId)
                .Data(HealthStore.Key, HealthDirector.TestKey)
                .Test("reallife_hc_test_btn_ill", "reallife_hc_test_btn_serious", "reallife_hc_test_btn_admit", "reallife_hc_test_btn_people", "reallife_hc_test_btn_staff",
                    "reallife_hc_test_btn_back")
                .Fact("hc_neglect", () => HealthCatalogue.NeglectDays.ToString(Inv))
                .Fact("hc_admission", () => Guide.Money(HealthCatalogue.Admission))
                .Fact("hc_stay_day", () => Guide.Money(HealthCatalogue.StayDay))
                .Fact("hc_stay_most", () => HealthCatalogue.StayMost.ToString(Inv))
                .Fact("hc_missed", () => Guide.Money(HealthCatalogue.MissedFee))
                .Fact("hc_basic", () => Guide.Money(HealthCatalogue.PremiumMonth[1]))
                .Fact("hc_standard", () => Guide.Money(HealthCatalogue.PremiumMonth[2]))
                .Fact("hc_full", () => Guide.Money(HealthCatalogue.PremiumMonth[3]))
                .Fact("hc_wait", () => HealthCatalogue.WaitDays.ToString(Inv))
                .Fact("hc_debt_months", () => HealthCatalogue.DebtMonths.ToString(Inv))
                .Fact("hc_debt_court", () => HealthCatalogue.DebtClaimAfter.ToString(Inv))
                .Fact("hc_self_cert", () => HealthCatalogue.SelfCertDays.ToString(Inv))
                .Fact("hc_note_after", () => HealthCatalogue.NoteAfterStay.ToString(Inv))
                .ResetData(() => HealthDirector.Instance?.Forget());
        }
    }

    /// <summary>Health's place on the options page.</summary>
    public static class HealthOptions
    {
        public static ModOptions AddHealth(this ModOptions options)
        {
            return options
                .AddHeader("reallife_hc_header")
                .AddToggle(Settings.HealthId, "reallife_hc_enabled", Settings.Health, v => { Settings.Health = v; })
                .AddButton("reallife_hc_test_btn_ill", () => HealthDirector.Instance?.TestIll())
                .AddButton("reallife_hc_test_btn_serious", () => HealthDirector.Instance?.TestSerious())
                .AddButton("reallife_hc_test_btn_admit", () => HealthDirector.Instance?.TestAdmit())
                .AddButton("reallife_hc_test_btn_people", () => HealthDirector.Instance?.TestPeople())
                .AddButton("reallife_hc_test_btn_staff", () => HealthDirector.Instance?.TestStaff())
                .AddButton("reallife_hc_test_btn_back", () => HealthDirector.Instance?.TestBack())
                .AddSplitter();
        }
    }
}
