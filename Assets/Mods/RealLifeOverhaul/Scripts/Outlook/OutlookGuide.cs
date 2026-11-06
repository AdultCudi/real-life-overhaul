using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>The Economy app's entry in the feature list; the figures its Guide page names come from the code.</summary>
    public static class OutlookGuide
    {
        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
        private static string Tenths(int tenths) => (tenths / 10.0).ToString("0.#", CultureInfo.InvariantCulture);
        private static string Hundredths(int hundredths) => (hundredths / 100.0).ToString("0.0#", CultureInfo.InvariantCulture);

        public static void Register()
        {
            Features.Add("economy_app", "reallife_meta_title_economy_app")
                .Group("reallife_econapp_header")
                .About("reallife_guide_economy_app_about").Help("reallife_guide_economy_app")
                .Owns(Settings.EconomyAppId, Settings.EconomyAppLookId)
                .Data(OutlookCatalogue.Key)
                .Test("reallife_econapp_test_tabs", () => OutlookDirector.Instance?.TestTabs())
                .Test("reallife_econapp_test_sample", () => OutlookDirector.Instance?.TestSample())
                .Test("reallife_econapp_test_advice", () => OutlookDirector.Instance?.TestAdvice())
                .Fact("econapp_days", () => N(OutlookCatalogue.HistoryDays))
                .Fact("econapp_short", () => N(OutlookCatalogue.ShortDays))
                .Fact("econapp_long", () => N(OutlookCatalogue.LongDays))
                .Fact("econapp_top", () => N(OutlookCatalogue.InsightsOnOverview))
                .Fact("econapp_most", () => N(OutlookCatalogue.InsightsMost))
                .Fact("econapp_target", () => Hundredths(Economy.MacroCatalogue.InflationTarget))
                .Fact("econapp_hot", () => Hundredths(Economy.MacroCatalogue.InflationTarget + OutlookCatalogue.HotGap))
                .Fact("econapp_high", () => Hundredths(OutlookCatalogue.InflationHigh))
                .Fact("econapp_low", () => Hundredths(OutlookCatalogue.InflationLow))
                .Fact("econapp_jobs_tight", () => (OutlookCatalogue.JobsTight / 10.0).ToString("0.#", CultureInfo.InvariantCulture))
                .Fact("econapp_jobs_slack", () => (OutlookCatalogue.JobsSlack / 10.0).ToString("0.#", CultureInfo.InvariantCulture))
                .Fact("econapp_worth", () => (OutlookCatalogue.WorthMoves / 100.0).ToString("0.#", CultureInfo.InvariantCulture))
                .Fact("econapp_income", () => Tenths(OutlookCatalogue.IncomeMoves))
                .Fact("econapp_shock", () => N(OutlookCatalogue.ShockPercent))
                .Fact("econapp_stocks", () => (OutlookCatalogue.StocksMove / 100.0).ToString("0.#", CultureInfo.InvariantCulture))
                .Fact("econapp_demand_high", () => N(OutlookCatalogue.DemandHigh))
                .Fact("econapp_demand_low", () => N(OutlookCatalogue.DemandLow))
                .Fact("econapp_buildings", () => N(OutlookCatalogue.BuildingsMany))
                .Fact("econapp_rate_days", () => N(OutlookCatalogue.RateAheadDays))
                .Fact("econapp_income_falls", () => Tenths(-OutlookCatalogue.IncomeFalls))
                .Fact("econapp_income_rises", () => Tenths(OutlookCatalogue.IncomeRises))
                .Fact("econapp_stocks_falls", () => (-OutlookCatalogue.StocksFall / 100.0).ToString("0.#", CultureInfo.InvariantCulture))
                .Fact("econapp_stocks_rises", () => (OutlookCatalogue.StocksRise / 100.0).ToString("0.#", CultureInfo.InvariantCulture))
                .Cleanup(() => OutlookDirector.Instance?.TestBack());   // a sample year taken off the page
        }
    }

    /// <summary>The Economy app's place on the options page.</summary>
    public static class OutlookOptions
    {
        public static ModOptions AddOutlook(this ModOptions options)
        {
            return options
                .AddHeader("reallife_econapp_header")
                .AddToggle(Settings.EconomyAppId, "reallife_econapp_enabled", Settings.EconomyApp,
                    v => Settings.EconomyApp = v)
                .AddDropdown(Settings.EconomyAppLookId, "reallife_econapp_opt_look",
                    new[] { "reallife_econapp_look_dark", "reallife_econapp_look_light" }, Settings.EconomyAppLook,
                    v => Settings.EconomyAppLook = v)
                .AddButton("reallife_econapp_test_tabs", () => OutlookDirector.Instance?.TestTabs())
                .AddButton("reallife_econapp_test_sample", () => OutlookDirector.Instance?.TestSample())
                .AddButton("reallife_econapp_test_advice", () => OutlookDirector.Instance?.TestAdvice())
                .AddSplitter();
        }
    }
}
