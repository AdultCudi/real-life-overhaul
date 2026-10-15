using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The bank's entry in the mod's feature list, and the figures the Guide's page "Your own bank" prints: every {name} in
    /// its text is filled from <see cref="OwnBankCatalogue"/> and <see cref="OwnBankRules"/> here, so the page can never
    /// say anything the rules do not do. Called once, when the feature list is built (Meta/FeatureList); each figure is
    /// worked out when the page is opened.
    /// </summary>
    public static class OwnBankGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("economy_ownbank", "reallife_meta_title_economy_ownbank")
                .About("reallife_guide_economy_ownbank_about").Help("reallife_guide_economy_ownbank")
                .Owns(Settings.EconomyBankId)
                .Data(OwnBankStore.Key, OwnBankDirector.TestKey)
                .Test("reallife_ob_test_found", "reallife_ob_test_month", "reallife_ob_test_hard", "reallife_ob_test_run", "reallife_ob_test_deadline", "reallife_ob_test_fund", "reallife_ob_test_back")
                .Fact("ob_licence", () => Money(OwnBankRules.Licence(Prices.Level, EndScaleGuide.Scale)))
                .Fact("ob_capital", () => Money(OwnBankRules.CapitalLeast(Prices.Level, 0, EndScaleGuide.Scale)))
                .Fact("ob_again_capital", () => Money(OwnBankRules.CapitalLeast(Prices.Level, 1, EndScaleGuide.Scale)))
                .Fact("ob_again_integrity", () => Whole(OwnBankCatalogue.AgainIntegrity))
                .Fact("ob_step", () => Money(OwnBankCatalogue.CapitalStepCents))
                .Fact("ob_min", () => OwnBankText.Ratio(OwnBankCatalogue.MinRatio))
                .Fact("ob_watch", () => OwnBankText.Ratio(OwnBankCatalogue.WatchRatio))
                .Fact("ob_lend", () => OwnBankText.Ratio(OwnBankCatalogue.LendRatio))
                .Fact("ob_half", () => OwnBankText.Ratio(OwnBankCatalogue.MinRatio / 2))
                .Fact("ob_reserve", () => OwnBankText.Ratio(OwnBankCatalogue.ReserveShare))
                .Fact("ob_cure_months", () => Whole(OwnBankCatalogue.CureMonths))
                .Fact("ob_fine_share", () => OwnBankText.Ratio(OwnBankCatalogue.FineShare))
                .Fact("ob_fine_least", () => Money(OwnBankRules.At(OwnBankCatalogue.FineLeastCents, Prices.Level)))
                .Fact("ob_weight_poor", () => OwnBankText.Ratio(OwnBankCatalogue.LoanWeight[OwnBankCatalogue.Poor]))
                .Fact("ob_weight_good", () => OwnBankText.Ratio(OwnBankCatalogue.LoanWeight[OwnBankCatalogue.Good]))
                .Fact("ob_weight_fund", () => OwnBankText.Ratio(OwnBankCatalogue.FundWeight))
                .Fact("ob_wind_fee", () => OwnBankText.Ratio(OwnBankCatalogue.WindFee))
                .Fact("ob_rival_margin", () => Points(OwnBankCatalogue.RivalSaverMargin))
                .Fact("ob_answer", () => Points(OwnBankCatalogue.AnswerMost))
                .Fact("ob_poor_premium", () => Points(OwnBankCatalogue.PoorPremium))
                .Fact("ob_trust_months", () => Whole(OwnBankCatalogue.TrustMonths))
                .Fact("ob_run_people", () => OwnBankText.Ratio(OwnBankCatalogue.RunShare[OwnBankCatalogue.People]))
                .Fact("ob_run_firms", () => OwnBankText.Ratio(OwnBankCatalogue.RunShare[OwnBankCatalogue.Firms]))
                .Fact("ob_default_poor", () => OwnBankText.Ratio(OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Poor]))
                .Fact("ob_default_fair", () => OwnBankText.Ratio(OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Fair]))
                .Fact("ob_default_good", () => OwnBankText.Ratio(OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Good]))
                .Fact("ob_default_shops", () => OwnBankText.Ratio(OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Shops]))
                .Fact("ob_default_firms", () => OwnBankText.Ratio(OwnBankCatalogue.DefaultBase[OwnBankCatalogue.Companies]))
                .Fact("ob_adverse", () => OwnBankText.Ratio(OwnBankCatalogue.AdversePerPoint))
                .Fact("ob_court_extra", () => OwnBankText.Ratio(OwnBankCatalogue.CourtExtra))
                .Fact("ob_court_cost", () => OwnBankText.Ratio(OwnBankCatalogue.CourtCost))
                .Fact("ob_recover_months", () => Whole(OwnBankCatalogue.RecoverMonths))
                .Fact("ob_fund_most", () => OwnBankText.Ratio(OwnBankCatalogue.FundLimitMost))
                .Fact("ob_fund_cautious", () => Whole(OwnBankCatalogue.FundCompanies[1]))
                .Fact("ob_fund_balanced", () => Whole(OwnBankCatalogue.FundCompanies[2]))
                .Fact("ob_fund_bold", () => Whole(OwnBankCatalogue.FundCompanies[3]))
                .Fact("ob_sale_years", () => Whole(OwnBankCatalogue.SaleYears))
                .Fact("ob_staff", () => Money(OwnBankRules.At(OwnBankCatalogue.StaffDayCents, Prices.Level)))
                .Cleanup(() => OwnBankDirector.Instance?.StopAll(), -3)
                .ResetData(() => OwnBankDirector.Instance?.Forget());   // the director opens the feature afresh from the (now empty) save on its next second
        }

        private static string Money(long cents)
        {
            return Guide.Money(cents / 100f);
        }

        private static string Points(int bp)
        {
            return (bp / 100.0).ToString("0.##", Inv);
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}
