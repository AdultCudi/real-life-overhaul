using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The bank risk, credit cards and trading desk's feature entry, and their Guide page's figures, filled from the catalogues and the rules when the page opens.</summary>
    public static class BankGrowthGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("economy_bank_risk", "reallife_meta_title_economy_bank_risk")
                .About("reallife_guide_economy_bank_risk_about").Help("reallife_guide_economy_bank_risk")
                .Owns(Settings.EconomyBankRiskId)
                .Test("reallife_ob_test_stress", "reallife_ob_test_cards", "reallife_ob_test_desk")
                .Fact("rk_div_week", () => Whole(BankDividends.WeekDays))
                .Fact("rk_div_tax", () => OwnBankText.Ratio(Markets.ListingCatalogue.DividendTaxBp / 10))
                .Fact("rk_min", () => OwnBankText.Ratio(OwnBankCatalogue.MinRatio))
                .Fact("rk_watch", () => OwnBankText.Ratio(OwnBankCatalogue.WatchRatio))
                .Fact("rk_wind_fee", () => OwnBankText.Ratio(OwnBankCatalogue.WindFee))
                .Fact("rk_weight_fund", () => OwnBankText.Ratio(OwnBankCatalogue.FundWeight))
                .Fact("rk_firm_recession", () => Ratio(BankRisk.FirmCycle[BankRisk.Standard][3]))
                .Fact("rk_firm_recession_hard", () => Ratio(BankRisk.FirmCycle[BankRisk.Hard][3]))
                .Fact("rk_people_recession", () => Ratio(BankRisk.PeopleStress[BankRisk.Standard][3]))
                .Fact("rk_people_recession_hard", () => Ratio(BankRisk.PeopleStress[BankRisk.Hard][3]))
                .Fact("rk_recover_recession", () => Ratio(BankRisk.RecoveryCycle[BankRisk.Standard][3]))
                .Fact("rk_recover_recession_hard", () => Ratio(BankRisk.RecoveryCycle[BankRisk.Hard][3]))
                .Fact("rk_shock_chance", () => Ratio(BankRisk.ShockChance[BankRisk.Standard]))
                .Fact("rk_shock_chance_hard", () => Ratio(BankRisk.ShockChance[BankRisk.Hard]))
                .Fact("rk_shock_default", () => Ratio(BankRisk.ShockDefault[BankRisk.Standard]))
                .Fact("rk_shock_default_hard", () => Ratio(BankRisk.ShockDefault[BankRisk.Hard]))
                .Fact("rk_conc_limit", () => Ratio(BankRisk.ConcLimit[BankRisk.Standard]))
                .Fact("rk_conc_limit_hard", () => Ratio(BankRisk.ConcLimit[BankRisk.Hard]))
                .Fact("rk_conc_weight", () => Ratio(BankRisk.ConcWeight[BankRisk.Standard]))
                .Fact("rk_conc_weight_hard", () => Ratio(BankRisk.ConcWeight[BankRisk.Hard]))
                .Fact("rk_reserve", () => Ratio(BankRisk.ReserveShare[BankRisk.Standard]))
                .Fact("rk_reserve_hard", () => Ratio(BankRisk.ReserveShare[BankRisk.Hard]))
                .Fact("rk_flight", () => Times(BankRisk.FlightSpeed[BankRisk.Standard]))
                .Fact("rk_flight_hard", () => Times(BankRisk.FlightSpeed[BankRisk.Hard]))
                .Fact("rk_flight_conf", () => Ratio(BankRisk.FlightConfidence))
                .Fact("rk_cover_warn", () => Tenths(BankRisk.CoverWarnDays))
                .Fact("rk_run_start", () => Ratio(BankRisk.RunStart[BankRisk.Standard]))
                .Fact("rk_run_start_hard", () => Ratio(BankRisk.RunStart[BankRisk.Hard]))
                .Fact("rk_warn_ratio", () => Ratio(BankRisk.LendRatio[BankRisk.Standard]))
                .Fact("rk_warn_ratio_hard", () => Ratio(BankRisk.LendRatio[BankRisk.Hard]))
                .Fact("rk_watch_lend", () => Ratio(BankRisk.WatchLend[BankRisk.Standard]))
                .Fact("rk_watch_lend_hard", () => Ratio(BankRisk.WatchLend[BankRisk.Hard]))
                .Fact("rk_run_hold", () => Ratio(BankRisk.RunRatePer))
                .Fact("rk_run_hold_most", () => Ratio(BankRisk.RunRateMost))
                .Fact("rk_sale_most", () => Ratio(BankRisk.SaleMost))
                .Fact("rk_sale_low", () => Ratio(BankRisk.SaleDiscount[BankRisk.Standard][0]))
                .Fact("rk_sale_high", () => Ratio(BankRisk.SaleDiscount[BankRisk.Standard][3]))
                .Fact("rk_sale_low_hard", () => Ratio(BankRisk.SaleDiscount[BankRisk.Hard][0]))
                .Fact("rk_sale_high_hard", () => Ratio(BankRisk.SaleDiscount[BankRisk.Hard][3]))
                .Fact("rk_interbank_most", () => Ratio(BankRisk.InterbankMost[BankRisk.Standard]))
                .Fact("rk_interbank_most_hard", () => Ratio(BankRisk.InterbankMost[BankRisk.Hard]))
                .Fact("rk_interbank_spread", () => Points(BankRisk.InterbankSpread[BankRisk.Standard]))
                .Fact("rk_interbank_spread_hard", () => Points(BankRisk.InterbankSpread[BankRisk.Hard]))
                .Fact("rk_interbank_run", () => Points(BankRisk.InterbankRun))
                .Fact("rk_interbank_fee", () => OwnBankText.Rate(BankRisk.InterbankFeeBp))
                .Fact("rk_interbank_roll", () => Points(BankRisk.InterbankRoll))
                .Fact("rk_card_fixed", () => Money(OwnBankRules.At(CardTerms.FixedDayCents, Prices.Level)))
                .Fact("rk_card_revolve", () => Ratio(CardTerms.Revolve[OwnBankCatalogue.Good]))
                .Fact("rk_card_interchange", () => OwnBankText.Rate(CardTerms.InterchangeBp))
                .Fact("rk_card_fraud", () => OwnBankText.Rate(CardTerms.FraudBp))
                .Fact("rk_card_default", () => Ratio(CardTerms.DefaultMult[OwnBankCatalogue.Good]))
                .Fact("rk_card_weight", () => Ratio(CardTerms.CardWeight))
                .Fact("rk_black_most", () => Ratio(CardTerms.BlackMostShare))
                .Fact("rk_black_discount", () => Points(CardTerms.BlackDiscountBp))
                .Fact("rk_desk_day", () => Money(OwnBankRules.At(BankDesk.DayCents, Prices.Level)))
                .Fact("rk_desk_custody_people", () => Ratio(BankDesk.Custody[OwnBankCatalogue.People]))
                .Fact("rk_desk_custody_firms", () => Ratio(BankDesk.Custody[OwnBankCatalogue.Firms]))
                .Fact("rk_desk_turnover", () => (BankDesk.Turnover[OwnBankCatalogue.People] / 1000.0).ToString("0.#", Inv))
                .Fact("rk_desk_commission", () => OwnBankText.Rate(BankDesk.CommissionBp))
                .Fact("rk_desk_deriv_commission", () => OwnBankText.Rate(BankDesk.DerivCommissionBp))
                .Fact("rk_desk_share_most", () => Ratio(BankDesk.ShareMost))
                .Fact("rk_desk_cut_keep", () => Ratio(BankDesk.CutKeep));
        }

        private static string Ratio(int thousandths) => OwnBankText.Ratio(thousandths);

        private static string Times(int thousandths) => (thousandths / 1000.0).ToString("0.#", Inv);

        private static string Tenths(int tenths) => (tenths / 10.0).ToString("0.#", Inv);

        private static string Points(int bp) => (bp / 100.0).ToString("0.##", Inv);

        private static string Money(long cents) => Guide.Money(cents / 100f);

        private static string Whole(int value) => value.ToString(Inv);
    }
}
