using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>
    /// The schemes' entry in the mod's feature list: its option under Living population, its save line, its tests and the
    /// Guide page's figures, each {name} filled from the catalogue when the page opens. Called once when the list is built.
    /// </summary>
    public static class SchemeGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("schemes", "reallife_meta_title_schemes")
                .About("reallife_guide_schemes_about").Help("reallife_guide_schemes")
                .Owns(Settings.LivingSchemesId)
                .Data(SchemeStore.Key)
                .Test("reallife_scheme_test_inquiry", "reallife_scheme_test_step", "reallife_scheme_test_fund", "reallife_scheme_test_rival", "reallife_scheme_test_standing")
                .Cleanup(() => SchemeDirector.Instance?.Release(), 0)
                .ResetData(() => SchemeDirector.Instance?.Forget())
                .Fact("scheme_surcharge", () => Percent(SchemeCatalogue.SurchargePermille))
                .Fact("scheme_letter", () => Whole(SchemeCatalogue.LetterAfter))
                .Fact("scheme_visit", () => Whole(SchemeCatalogue.VisitAfter))
                .Fact("scheme_findings", () => Whole(SchemeCatalogue.ChargeAfter))
                .Fact("scheme_drop", () => Whole(SchemeCatalogue.GiveUpAfter))
                .Fact("scheme_file_days", () => Whole(SchemeCatalogue.TrailDays))
                .Fact("scheme_repeat_days", () => Whole(SchemeCatalogue.RepeatDays))
                .Fact("scheme_felony", () => Guide.Money(Prices.At(SchemeCatalogue.FelonyFrom)))
                .Fact("scheme_wash_cut", () => Percent(SchemeCatalogue.WashCut))
                .Fact("scheme_wash_most", () => Percent(SchemeCatalogue.WashSteps[SchemeCatalogue.WashSteps.Length - 1]))
                .Fact("scheme_report", () => Guide.Money(Prices.At(SchemeCatalogue.ReportFrom)))
                .Fact("scheme_pattern", () => Guide.Money(Prices.At(SchemeCatalogue.PatternFrom)))
                .Fact("scheme_pattern_days", () => Whole(SchemeCatalogue.PatternDays))
                .Fact("scheme_club_stake", () => Guide.Money(Prices.At(SchemeCatalogue.ClubStakeMost)))
                .Fact("scheme_lift_most", () => Percent(SchemeCatalogue.LiftMost))
                .Fact("scheme_hold_days", () => Whole(SchemeCatalogue.HoldDays))
                .Fact("scheme_fund_return", () => Percent(SchemeCatalogue.FundReturn))
                .Fact("scheme_fund_days", () => Whole(SchemeCatalogue.FundDays));
        }

        private static string Whole(int value) => value.ToString(Inv);

        private static string Percent(int thousandths) => (thousandths / 10).ToString(Inv) + "%";
    }
}
