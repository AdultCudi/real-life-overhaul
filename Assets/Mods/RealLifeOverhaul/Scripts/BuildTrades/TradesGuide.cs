using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>
    /// The building trades' entry in the feature list, its keys written out whole as the options checks read them. It
    /// registers itself (<see cref="DescribesFeatureAttribute"/>). Its Guide page's figures are facts filled from the
    /// catalogues the trades are paid and judged by.
    /// </summary>
    [DescribesFeature]
    public static class TradesGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static void DescribeFeature()
        {
            Features.Add("buildtrades", "reallife_meta_title_buildtrades")
                .Group("reallife_trades_header")
                .About("reallife_guide_trades_about").Help("reallife_guide_trades")
                .Owns(Settings.TradesId, Settings.TradesOfficeId)
                .Data(DesignBook.Key)
                .ResetData(() => TradesDirector.Instance?.Forget())
                .Fact("trades_mason_pay", () => Guide.Money(JobCatalogue.Find(JobCatalogue.Mason).UnitPay))
                .Fact("trades_labourer_pay", () => Guide.Money(JobCatalogue.Find(JobCatalogue.Labourer).UnitPay))
                .Fact("trades_architect_pay", () => Guide.Money(JobCatalogue.Find(JobCatalogue.Architect).UnitPay))
                .Fact("trades_floor", () => Percent(TradeRules.PayFloor))
                .Fact("trades_mason_points", () => TradeCatalogue.MasonRank.Points.ToString(Inv))
                .Fact("trades_foreman_points", () => TradeCatalogue.ForemanRank.Points.ToString(Inv))
                .Fact("trades_learn", () => (TradeCatalogue.Find(JobCatalogue.Labourer).LearnEach / 1000f).ToString("0.##", Inv))
                .Fact("trades_bribe_out", () => Percent(TradeRules.CaughtShare))
                .Fact("trades_bribe_days", () => DesignBook.BribeComesOutDays.ToString(Inv))
                .Fact("trades_items", () => TradeRules.Items.ToString(Inv))
                .Test("reallife_trades_test_mason", "reallife_trades_test_electrician", "reallife_trades_test_crane", "reallife_trades_test_inspector",
                    "reallife_trades_test_surveyor", "reallife_trades_test_security", "reallife_trades_test_site_work", "reallife_trades_test_office",
                    "reallife_trades_test_design", "reallife_trades_test_tender", "reallife_trades_test_commission", "reallife_trades_test_bribe",
                    "reallife_trades_test_apprentice")
                .Cleanup(() => JobDirector.Instance?.QuitTrade());
        }

        private static string Percent(float share) => (share * 100f).ToString("0", Inv) + "%";
    }

    /// <summary>The building trades' place on the options page: the two options and the test entries (shown with the test tools).</summary>
    public static class TradesOptions
    {
        public static ModOptions AddTrades(this ModOptions options)
        {
            return options
                .AddHeader("reallife_trades_header")
                .AddToggle(Settings.TradesId, "reallife_trades_enabled", Settings.Trades,
                    v => { Settings.Trades = v; if (!v) { JobDirector.Instance?.QuitTrade(); } })
                .AddToggle(Settings.TradesOfficeId, "reallife_trades_office", Settings.TradesOffice,
                    v => { Settings.TradesOffice = v; if (!v) { JobDirector.Instance?.QuitTrade(); } })
                .AddButton("reallife_trades_test_mason", () => TradesDirector.Instance?.TestTrade(JobCatalogue.Mason))
                .AddButton("reallife_trades_test_electrician", () => TradesDirector.Instance?.TestTrade(JobCatalogue.Electrician))
                .AddButton("reallife_trades_test_crane", () => TradesDirector.Instance?.TestTrade(JobCatalogue.Crane))
                .AddButton("reallife_trades_test_inspector", () => TradesDirector.Instance?.TestTrade(JobCatalogue.Inspector))
                .AddButton("reallife_trades_test_surveyor", () => TradesDirector.Instance?.TestTrade(JobCatalogue.Surveyor))
                .AddButton("reallife_trades_test_security", () => TradesDirector.Instance?.TestTrade(JobCatalogue.Security))
                .AddButton("reallife_trades_test_site_work", () => TradesDirector.Instance?.TestSiteWork())
                .AddButton("reallife_trades_test_office", () => TradesDirector.Instance?.TestOffice())
                .AddButton("reallife_trades_test_design", () => TradesDirector.Instance?.TestDesk(JobCatalogue.Architect))
                .AddButton("reallife_trades_test_tender", () => TradesDirector.Instance?.TestDesk(JobCatalogue.Estimator))
                .AddButton("reallife_trades_test_commission", () => TradesDirector.Instance?.TestCommission())
                .AddButton("reallife_trades_test_bribe", () => TradesDirector.Instance?.TestBribe())
                .AddButton("reallife_trades_test_apprentice", () => TradesDirector.Instance?.TestApprentice())
                .AddSplitter();
        }
    }
}
