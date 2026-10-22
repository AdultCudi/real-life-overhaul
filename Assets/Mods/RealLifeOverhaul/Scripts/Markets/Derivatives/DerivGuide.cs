using System.Globalization;
using System.Text;
using Localizor;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Futures and options in the mod's feature list, and the figures their Guide page prints, each filled from the rules
    /// when the page is opened so the page cannot say what the rules do not do.
    /// </summary>
    public static class DerivGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("markets_derivatives", "reallife_meta_title_markets_derivatives")
                .Group("reallife_mkt_header")
                .About("reallife_guide_markets_derivatives_about").Help("reallife_guide_markets_derivatives")
                .Owns(Settings.MarketsDerivativesId)
                .Test("reallife_mkt_drv_test_open", "reallife_mkt_drv_test_settle", "reallife_mkt_drv_test_call")
                .Fact("drv_materials", () => Whole(Count()))
                .Fact("drv_farm_day", () => Bp(DerivCatalogue.GroupSigmaBp[(int)MaterialGroup.Farm]))
                .Fact("drv_paper_day", () => Bp(DerivCatalogue.GroupSigmaBp[(int)MaterialGroup.Paper]))
                .Fact("drv_farm_week", () => Guide.Percent((float)MaterialRules.Spread(MaterialGroup.Farm, 5)))
                .Fact("drv_paper_week", () => Guide.Percent((float)MaterialRules.Spread(MaterialGroup.Paper, 5)))
                .Fact("drv_farm_half", () => Whole(DerivCatalogue.GroupHalfLife[(int)MaterialGroup.Farm]))
                .Fact("drv_metals_half", () => Whole(DerivCatalogue.GroupHalfLife[(int)MaterialGroup.Metals]))
                .Fact("drv_weeks", () => Whole(DerivCatalogue.WeeksAhead))
                .Fact("drv_closes_least", () => Whole(DerivRules.ClosesLeast))
                .Fact("drv_spread", () => Bp(DerivCatalogue.FutureHalfSpreadBp))
                .Fact("drv_markup", () => Bp(DerivCatalogue.OptionMarkupBp))
                .Fact("drv_option_spread", () => Bp(DerivCatalogue.OptionHalfSpreadBp))
                .Fact("drv_commission", () => Bp(MarketCatalogue.CommissionBp))
                .Fact("drv_margin_times", () => Whole(DerivCatalogue.MarginSdTimes))
                .Fact("drv_margin_least", () => Whole(DerivCatalogue.MarginLeastPct) + "%")
                .Fact("drv_margin_most", () => Whole(DerivCatalogue.MarginMostPct) + "%")
                .Fact("drv_margin_farm", () => Whole(DerivRules.MarginPct(MaterialGroup.Farm)) + "%")
                .Fact("drv_margin_paper", () => Whole(DerivRules.MarginPct(MaterialGroup.Paper)) + "%")
                .Fact("drv_maintenance", () => Whole(DerivCatalogue.MaintenancePct) + "%")
                .Fact("drv_call_days", () => Whole(MarketCatalogue.CallDays))
                .Fact("drv_forced", () => Whole(MarketCatalogue.ForcedBelowPct) + "%")
                .Fact("drv_contracts", () => Whole(DerivCatalogue.ContractsMost))
                .Fact("drv_units", () => DerivCatalogue.MaterialUnitsMost.ToString("N0"))
                .Fact("drv_quantities", () => List(DerivCatalogue.MaterialQuantities))
                .Fact("drv_shares", () => List(DerivCatalogue.ShareQuantities))
                .Fact("drv_strikes", () => Steps())
                .Fact("drv_settle_steps", () => Whole(DerivCatalogue.SettleSteps * MarketCatalogue.StepMinutes))
                .Fact("drv_listed_days", () => Whole(DerivCatalogue.ShareListedDaysLeast))
                .Fact("drv_share_adv", () => Whole(DerivCatalogue.ShareOptionAdvPct) + "%")
                .Fact("drv_kept_days", () => Whole(DerivCatalogue.EndedKeptDays));
        }

        private static int Count()
        {
            int n = 0;
            for (int g = 0; g < DerivCatalogue.GroupNames.Length; g++)
            {
                n += DerivCatalogue.GroupNames[g].Length;
            }
            return n;
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }

        private static string Bp(int bp)
        {
            return Guide.Percent(bp / 10000f);
        }

        // "500, 1,000, 2,500 or 5,000" in the language's words.
        private static string List(long[] values)
        {
            var text = new StringBuilder(48);
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(i == values.Length - 1 ? " " + "reallife_mkt_bot_list_or".GetLocalization() + " " : ", ");
                }
                text.Append(values[i].ToString("N0"));
            }
            return text.ToString();
        }

        // The strikes on offer: "10% and 5% under, at the price, 5% and 10% over" as the steps say.
        private static string Steps()
        {
            int most = 0;
            for (int i = 0; i < DerivCatalogue.StrikeStepsBp.Length; i++)
            {
                most = System.Math.Max(most, System.Math.Abs(DerivCatalogue.StrikeStepsBp[i]));
            }
            return Guide.Percent(most / 10000f);
        }
    }
}
