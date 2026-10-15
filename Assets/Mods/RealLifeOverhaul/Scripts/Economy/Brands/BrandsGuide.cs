using System.Globalization;
using System.Text;
using Localizor;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The figures the Guide's page "Brands and franchises" prints: every {name} in its text is filled from
    /// <see cref="BrandCatalogue"/> here, so the page can never say anything the rules do not do. Called once, when
    /// the feature list is built (Meta/FeatureList); each figure is worked out when the page is opened.
    /// </summary>
    public static class BrandsGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const int GuideShops = 3, GuideCustomers = 100, GuideStandards = 75, GuideDays = 20;   // the example the page works through: three busy shops with good standards, a new brand, twenty days
        private const int SpreadDistricts = 3, SpreadDays = 40;   // the second example: the same three shops, one in each of three districts, forty days

        public static void AddFacts(Feature f)
        {
            f.Fact("br_brands_most", () => Whole(BrandCatalogue.BrandsMost))
                .Fact("br_local", () => Tenths(BrandCatalogue.LocalBand))
                .Fact("br_keep_margin", () => Tenths(BrandCatalogue.ChainPriceMargin))
                .Fact("br_keep_round", () => Whole(BrandCatalogue.KeepPerRound))
                .Fact("br_w_sat", () => Whole(BrandCatalogue.WeightSatisfaction))
                .Fact("br_w_clean", () => Whole(BrandCatalogue.WeightClean))
                .Fact("br_w_stock", () => Whole(BrandCatalogue.WeightStock))
                .Fact("br_w_skill", () => Whole(BrandCatalogue.WeightSkill))
                .Fact("br_mean", () => Tenths(BrandCatalogue.BrandMeanShare))
                .Fact("br_worst", () => Tenths(BrandCatalogue.BrandWorstShare))
                .Fact("br_decay", () => Hundredths(BrandCatalogue.DecayBp))
                .Fact("br_decay_empty", () => Hundredths(BrandCatalogue.DecayEmptyBp))
                .Fact("br_three_gain", () => Hundredths(BrandRules.Gain(GuideShops, GuideCustomers, GuideStandards, 0)))
                .Fact("br_three_days", () => Whole(GuideDays))
                .Fact("br_three_after", () => Whole(BrandRules.After(GuideShops, GuideCustomers, GuideStandards, GuideDays) / 100))
                .Fact("br_pull_first", () => Hundredths(BrandCatalogue.PullFirst))
                .Fact("br_pull_less", () => Hundredths(BrandCatalogue.PullLess))
                .Fact("br_pull_shops", () => Whole(BrandCatalogue.PullShopsMost))
                .Fact("br_spread_days", () => Whole(SpreadDays))
                .Fact("br_spread_after", () => Whole(BrandRules.AfterSpread(SpreadDistricts, GuideCustomers, GuideStandards, SpreadDays, out int _) / 100))
                .Fact("br_spread_city", () =>
                {
                    BrandRules.AfterSpread(SpreadDistricts, GuideCustomers, GuideStandards, SpreadDays, out int city);
                    return Whole(city / 100);
                })
                .Fact("br_city_districts", () => Whole(BrandCatalogue.CityDistricts))
                .Fact("br_city_step", () => Hundredths(BrandCatalogue.CityStepBp))
                .Fact("br_city_one", () => Hundredths(BrandCatalogue.RecMost / BrandCatalogue.CityDistricts))
                .Fact("br_push_local", () => Hundredths(BrandCatalogue.CampaignLocalGain))
                .Fact("br_push_city", () => Hundredths(BrandCatalogue.CampaignCityGain))
                .Fact("br_scandals", Scandals)
                .Fact("br_rec_most", () => Whole(BrandCatalogue.RecFootfallMost))
                .Fact("br_low", () => Whole(BrandCatalogue.LowStandards))
                .Fact("br_tolerance", () => Whole(BrandCatalogue.ToleranceMost))
                .Fact("br_cap", () => Whole(BrandCatalogue.FootfallMost))
                .Fact("br_cannibal", () => Whole(BrandCatalogue.CannibalEach))
                .Fact("br_cannibal_most", () => Whole(BrandCatalogue.CannibalMost))
                .Fact("br_radius", () => Whole(BrandCatalogue.CannibalRadius))
                .Fact("br_offer_shops", () => Whole(BrandCatalogue.OfferShops))
                .Fact("br_offer_standards", () => Whole(BrandCatalogue.OfferStandards))
                .Fact("br_offer_city", () => Whole(BrandCatalogue.OfferCityRec / 100))
                .Fact("br_offer_district", () => Whole(BrandCatalogue.OfferDistrictRec / 100))
                .Fact("br_app_days", () => Whole(BrandCatalogue.ApplicantDays))
                .Fact("br_app_most", () => Whole(BrandCatalogue.ApplicantsMost))
                .Fact("br_fees", () => Choices(BrandCatalogue.EntryFee, v => Guide.Money(v)))
                .Fact("br_royalties", () => Choices(BrandCatalogue.RoyaltyBp, v => Hundredths(v) + "%"))
                .Fact("br_levies", () => Choices(BrandCatalogue.LevyBp, v => Hundredths(v) + "%"))
                .Fact("br_lengths", () => Choices(BrandCatalogue.LengthYears, Whole))
                .Fact("br_support_base", () => Guide.Money(CompanyCatalogue.AtPrices(BrandCatalogue.SupportBase, Prices.Level)))
                .Fact("br_support_share", () => Hundredths(BrandCatalogue.SupportBp))
                .Fact("br_fund_idle", () => Guide.Money(CompanyCatalogue.AtPrices(BrandCatalogue.FundIdleMost, Prices.Level)))
                .Fact("br_inspector", () => Guide.Money(FranchiseRules.InspectorFee(Prices.Level)))
                .Fact("br_inspect_due", () => Whole(BrandCatalogue.InspectDueDays))
                .Fact("br_fresh", () => Whole(BrandCatalogue.FreshDays))
                .Fact("br_warn_days", () => Whole(BrandCatalogue.WarnDays))
                .Fact("br_retrain", () => Guide.Money(FranchiseRules.RetrainFee(Prices.Level)))
                .Fact("br_cause", () => Whole(BrandCatalogue.CauseWarnings))
                .Fact("br_comp_share", () => Tenths(BrandCatalogue.CompensationLeastShare))
                .Fact("br_unfair", () => Hundredths(BrandCatalogue.UnfairLoss))
                .Fact("br_failure", () => Hundredths(BrandCatalogue.FailureLoss))
                .Fact("br_help_days", () => Whole(BrandCatalogue.HelpDays))
                .Fact("br_waive", () => Whole(BrandCatalogue.WaiveDays))
                .Fact("br_second_share", () => Tenths(BrandCatalogue.SecondShopShare))
                .Fact("br_renewal_notice", () => Whole(BrandCatalogue.RenewalNotice))
                .Fact("br_renewal_share", () => Tenths(BrandCatalogue.RenewalShare))
                .Fact("br_ask_days", () => Whole(BrandCatalogue.AskDays))
                .Fact("br_chains", Chains)
                .Fact("br_chain_warn", () => Whole(BrandCatalogue.ChainWarnDays))
                .Fact("br_chain_fine", () => Whole(BrandCatalogue.ChainFineDays))
                .Fact("br_chain_end", () => Whole(BrandCatalogue.ChainEndDays))
                .Fact("br_chain_exit", () => Whole(BrandCatalogue.ChainExitDays));
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }

        // Thousandths as a per cent without its sign: "60", "2.5".
        private static string Tenths(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Inv);
        }

        // Hundredths as they stand: recognition in points ("1.5"), ten-thousandths of a share in per cent ("0.5").
        private static string Hundredths(int hundredths)
        {
            return (hundredths / 100f).ToString("0.##", Inv);
        }

        // "a, b or c", from the table the rules themselves read.
        private static string Choices(int[] values, System.Func<int, string> words)
        {
            var text = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                text.Append(i == 0 ? string.Empty : i == values.Length - 1 ? " " + "reallife_guide_economy_brands_or".GetLocalization() + " " : ", ").Append(words(values[i]));
            }
            return text.ToString();
        }

        private static string Scandals()
        {
            return "reallife_guide_economy_brands_scandals".GetLocalization()
                .Replace("{inspection}", Hundredths(BrandCatalogue.ScandalLoss[(int)Scandal.Inspection]))
                .Replace("{strike}", Hundredths(BrandCatalogue.ScandalLoss[(int)Scandal.Strike]))
                .Replace("{fire}", Hundredths(BrandCatalogue.ScandalLoss[(int)Scandal.Fire]));
        }

        // One line per city chain, from the tables the rules themselves read.
        private static string Chains()
        {
            var text = new StringBuilder();
            for (int c = 0; c < BrandCatalogue.Chains; c++)
            {
                text.Append("- ").Append("reallife_guide_economy_brands_chain".GetLocalization()
                    .Replace("{name}", BrandsText.Chain(c))
                    .Replace("{fee}", Guide.Money(BrandCatalogue.ChainFee[c]))
                    .Replace("{royalty}", Hundredths(BrandCatalogue.ChainRoyaltyBp[c]))
                    .Replace("{percent}", Whole(FranchiseRules.ChainFootfall(c)))
                    .Replace("{standards}", Whole(BrandCatalogue.ChainStandards[c]))
                    .Replace("{least}", Tenths(BrandCatalogue.ChainPriceLeast[c]))
                    .Replace("{most}", Tenths(BrandCatalogue.ChainPriceMost[c]))).Append('\n');
            }
            return text.ToString();
        }
    }
}
