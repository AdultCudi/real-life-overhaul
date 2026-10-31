using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The game studio's entry in the mod's feature list: its option, its save line, its four tests and the Guide page's
    /// figures, each {name} filled from the catalogues when the page opens. Called once when the feature list is built.
    /// </summary>
    public static class StudioGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("studio", "reallife_meta_title_studio")
                .About("reallife_guide_studio_about").Help("reallife_guide_studio")
                .Owns(Settings.LivingStudioId)
                .Data(StudioStore.Key)
                .Test("reallife_studio_test_start", "reallife_studio_test_finish", "reallife_studio_test_launch", "reallife_studio_test_year")
                .ResetData(() => TechDirector.Instance?.ForgetStudio())
                .Fact("studio_genres", () => Whole((int)Genre.Count))
                .Fact("studio_budget_least", () => Guide.Money(Prices.At((int)(StudioCatalogue.Budgets[0] / 100))))
                .Fact("studio_budget_most", () => Guide.Money(Prices.At((int)(StudioCatalogue.Budgets[StudioCatalogue.Budgets.Length - 1] / 100))))
                .Fact("studio_point", () => Guide.Money(Prices.At(StudioCatalogue.PointCents) / 100f))
                .Fact("studio_pre", () => Mille(StudioCatalogue.PhaseShare[0]))
                .Fact("studio_polish", () => Mille(StudioCatalogue.PhaseShare[2]))
                .Fact("studio_grace", () => Whole(StudioCatalogue.MilestoneGraceDays))
                .Fact("studio_cut", () => Mille(1000 - StudioCatalogue.CutWork))
                .Fact("studio_delay", () => Mille(StudioCatalogue.DelayWork - 1000))
                .Fact("studio_crunch_days", () => Whole(StudioCatalogue.CrunchDays))
                .Fact("studio_crunch", () => Mille(StudioCatalogue.CrunchOutput))
                .Fact("studio_boost", () => Mille(StudioCatalogue.BoostOutput))
                .Fact("studio_find_team", () => Mille(StudioCatalogue.PlaytestFinds[0]))
                .Fact("studio_find_open", () => Mille(StudioCatalogue.PlaytestFinds[2]))
                .Fact("studio_quiet", () => Whole(StudioCatalogue.HypeQuietDays))
                .Fact("studio_backlash", () => Whole(StudioCatalogue.BacklashMost))
                .Fact("studio_drm_review", () => Whole(StudioCatalogue.DrmReview))
                .Fact("studio_expect_rise", () => (StudioCatalogue.ExpectRise / 1000f).ToString("0.#", Inv))
                .Fact("studio_expect_most", () => Whole(StudioCatalogue.ExpectMost))
                .Fact("studio_piracy_pc", () => Mille(StudioCatalogue.Piracy[0]))
                .Fact("studio_drm_cut", () => Mille(1000 - StudioCatalogue.DrmEarly))
                .Fact("studio_drm_days", () => Whole(StudioCatalogue.DrmDays))
                .Fact("studio_store_fee", () => Mille(ScaleCatalogue.PlatformFee[0]))
                .Fact("studio_royalty", () => Mille(ScaleCatalogue.EngineRoyalty))
                .Fact("studio_royalty_free", () => Guide.Money(Prices.At((int)(ScaleCatalogue.EngineRoyaltyFree / 100))))
                .Fact("studio_engine", () => Guide.Money(Prices.At((int)(ScaleCatalogue.OwnEngineCost / 100))))
                .Fact("studio_advance", () => Mille(StudioCatalogue.AdvanceShare))
                .Fact("studio_studio_royalty", () => Mille(StudioCatalogue.Royalty))
                .Fact("studio_cert", () => Whole(StudioCatalogue.CertificationDays))
                .Fact("studio_bonus_day", () => Whole(StudioCatalogue.BonusDay))
                .Fact("studio_hit_score", () => Whole(StudioCatalogue.HitScore))
                .Fact("studio_snub_days", () => Whole(StudioCatalogue.SnubDays))
                .Fact("studio_trend_days", () => Whole(StudioCatalogue.TrendDays))
                .Fact("studio_rivals", () => Whole(StudioCatalogue.RivalNames.Length));
        }

        private static string Whole(int value) => value.ToString(Inv);

        private static string Mille(int thousandths) => (thousandths / 10f).ToString("0", Inv) + "%";
    }
}
