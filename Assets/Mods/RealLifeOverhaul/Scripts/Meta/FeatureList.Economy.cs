using System.Globalization;
using System.Text;
using Localizor;

namespace RealLifeOverhaul.Meta
{
    /// <summary>The feature list's part for the economy: credit, the city's economy, investors, markets, banks, companies, property and tech.</summary>
    internal static partial class FeatureList
    {
        private static void RegisterEconomy()
        {
            // The game's loans are the game's own records: nothing of them is this feature's to remove.
            Features.Add("economy", "reallife_meta_title_economy")
                .Group("reallife_econ_header")
                .About("reallife_guide_economy_about").Help("reallife_guide_economy")
                .Owns(Settings.EconomyId, Settings.EconomyProductsId, Settings.EconomySharkId, Settings.EconomyConsequencesId, Settings.EconomyLevelId, Settings.EconomyGradeUpNotesId)
                .Preset(Settings.EconomyLevelId, Economy.ProductCatalogue.Relaxed, Economy.ProductCatalogue.Realistic, Economy.ProductCatalogue.Hardcore)
                .Data(Economy.CreditStore.Key)
                .Contact(Economy.DebtPhone.ContactIds)
                .Fact("econ_min", () => Whole(Economy.CreditCatalogue.ScoreMin))
                .Fact("econ_max", () => Whole(Economy.CreditCatalogue.ScoreMax))
                .Fact("econ_threshold", () => Whole(Economy.CreditCatalogue.LendingThreshold))
                .Fact("econ_start", () => Whole(Economy.CreditCatalogue.CleanStart))
                .Fact("econ_bands", EconomyBands)
                .Fact("econ_w_payments", () => Whole(Economy.CreditCatalogue.Weights[(int)Economy.CreditFactor.Payments]) + "%")
                .Fact("econ_w_owed", () => Whole(Economy.CreditCatalogue.Weights[(int)Economy.CreditFactor.Owed]) + "%")
                .Fact("econ_w_length", () => Whole(Economy.CreditCatalogue.Weights[(int)Economy.CreditFactor.Length]) + "%")
                .Fact("econ_w_legal", () => Whole(Economy.CreditCatalogue.Weights[(int)Economy.CreditFactor.Legal]) + "%")
                .Fact("econ_w_applications", () => Whole(Economy.CreditCatalogue.Weights[(int)Economy.CreditFactor.Applications]) + "%")
                .Fact("econ_fade_days", () => Whole(Economy.CreditCatalogue.PaymentFadeDays))
                .Fact("econ_miss_weight", () => Guide.Number(Economy.CreditCatalogue.MissWeight))
                .Fact("econ_history_days", () => Whole(Economy.CreditCatalogue.HistoryFullDays))
                .Fact("econ_application_days", () => Whole(Economy.CreditCatalogue.ApplicationFadeDays))
                .Fact("econ_best_from", () => Whole(Economy.CreditCatalogue.BestTermsFrom))
                .Fact("econ_cap_low", () => Guide.Percent(Economy.CreditCatalogue.CapShareAtThreshold))
                .Fact("econ_cap_high", () => Guide.Percent(Economy.CreditCatalogue.CapShareAtBest))
                .Fact("econ_rate_low", () => Whole(Economy.CreditCatalogue.RatePointsAtThreshold))
                .Fact("econ_rate_high", () => Whole(-Economy.CreditCatalogue.RatePointsAtBest))
                .Fact("econ_rate_floor", () => Whole(Economy.CreditCatalogue.RateFloor) + "%")
                .Fact("econ_fee", () => Guide.Percent(Economy.CreditCatalogue.PhoneFeeShare))
                .Fact("econ_fee_least", () => Guide.Money(Economy.CreditCatalogue.PhoneFeeLeast))
                .Fact("econ_loan_least", () => Guide.Money(Economy.CreditCatalogue.LoanLeast))
                .Fact("econ_events_shown", () => Whole(Economy.CreditCatalogue.LedgerShown))
                .Fact("econ_week", () => Whole(Economy.CashFlow.PeriodDays[Economy.CashFlow.Week]))
                .Fact("econ_month", () => Whole(Economy.CashFlow.PeriodDays[Economy.CashFlow.Month]))
                .Fact("econ_top", () => Whole(Economy.CashFlow.TopMost))
                .Fact("econ_od_low", () => Guide.Percent(Economy.ProductCatalogue.OverdraftShareAtThreshold))
                .Fact("econ_od_high", () => Guide.Percent(Economy.ProductCatalogue.OverdraftShareAtBest))
                .Fact("econ_od_least", () => Guide.Money(Economy.ProductCatalogue.OverdraftLeast))
                .Fact("econ_od_income", () => Whole(Economy.ProductCatalogue.OverdraftIncomeDays))
                .Fact("econ_od_points", () => Whole(Economy.ProductCatalogue.OverdraftRatePoints))
                .Fact("econ_od_float", () => Guide.Money(Economy.ProductCatalogue.OverdraftFloat))
                .Fact("econ_card_low", () => Guide.Percent(Economy.ProductCatalogue.CardShareAtThreshold))
                .Fact("econ_card_high", () => Guide.Percent(Economy.ProductCatalogue.CardShareAtBest))
                .Fact("econ_card_least", () => Guide.Money(Economy.ProductCatalogue.CardLeast))
                .Fact("econ_card_income", () => Whole(Economy.ProductCatalogue.CardIncomeDays))
                .Fact("econ_card_points", () => Whole(Economy.ProductCatalogue.CardRatePoints))
                .Fact("econ_card_fee", () => Guide.Percent(Economy.ProductCatalogue.CardCashFeeShare))
                .Fact("econ_card_fee_least", () => Guide.Money(Economy.ProductCatalogue.CardCashFeeLeast))
                .Fact("econ_card_days", () => Whole(Economy.ProductCatalogue.CardStatementDays))
                .Fact("econ_card_grace", () => Whole(Economy.ProductCatalogue.CardGraceDays))
                .Fact("econ_card_min", () => Guide.Percent(Economy.ProductCatalogue.CardMinimumShare))
                .Fact("econ_card_min_least", () => Guide.Money(Economy.ProductCatalogue.CardMinimumLeast))
                .Fact("econ_biz_days", () => Whole(Economy.ProductCatalogue.BusinessDaysOnRecord))
                .Fact("econ_biz_multiple", () => Whole(Economy.ProductCatalogue.BusinessProfitMultiple))
                .Fact("econ_biz_points", () => Whole(-Economy.ProductCatalogue.BusinessRatePoints))
                .Fact("econ_car_dep_low", () => Whole(Economy.ProductCatalogue.CarDepositLeast) + "%")
                .Fact("econ_car_dep_high", () => Whole(Economy.ProductCatalogue.CarDepositMost) + "%")
                .Fact("econ_car_term_low", () => Whole(Economy.ProductCatalogue.CarTermLeast))
                .Fact("econ_car_term_high", () => Whole(Economy.ProductCatalogue.CarTermMost))
                .Fact("econ_car_points", () => Whole(Economy.ProductCatalogue.CarRatePoints))
                .Fact("econ_home_dep_low", () => Whole(Economy.ProductCatalogue.HomeDepositLeast) + "%")
                .Fact("econ_home_dep_high", () => Whole(Economy.ProductCatalogue.HomeDepositMost) + "%")
                .Fact("econ_home_term_low", () => Whole(Economy.ProductCatalogue.HomeTermLeast))
                .Fact("econ_home_term_high", () => Whole(Economy.ProductCatalogue.HomeTermMost))
                .Fact("econ_home_points", () => Whole(-Economy.ProductCatalogue.HomeRatePoints))
                .Fact("econ_home_multiple", () => Whole(Economy.ProductCatalogue.HomeCapMultiple))
                .Fact("econ_approval_days", () => Whole(Economy.ProductCatalogue.ApprovalDays))
                .Fact("econ_cons_fee", () => Guide.Percent(Economy.ProductCatalogue.ConsolidateFeeShare))
                .Fact("econ_cons_fee_least", () => Guide.Money(Economy.ProductCatalogue.ConsolidateFeeLeast))
                .Fact("econ_ladder", EconomyLadder)
                .Fact("econ_late_share", () => Guide.Percent(Economy.ProductCatalogue.LateFeeShare))
                .Fact("econ_late_least", () => Guide.Money(Economy.ProductCatalogue.LateFeeLeast))
                .Fact("econ_penalty_points", () => Whole(Economy.ProductCatalogue.PenaltyPoints))
                .Fact("econ_penalty_run", () => Whole(Economy.ProductCatalogue.PenaltyClearRun))
                .Fact("econ_plan_days", () => Whole(Economy.ProductCatalogue.PlanDays))
                .Fact("econ_repo_share", () => Guide.Percent(Economy.ProductCatalogue.RepossessionValueShare))
                .Fact("econ_divert", () => Guide.Percent(Economy.ProductCatalogue.BusinessDivertShare))
                .Fact("econ_unsecured_days", () => Whole(Economy.ProductCatalogue.UnsecuredClaimDays))
                .Fact("econ_claim_costs", () => Guide.Percent(Economy.ProductCatalogue.ClaimCostsShare))
                .Fact("econ_claim_costs_least", () => Guide.Money(Economy.ProductCatalogue.ClaimCostsLeast))
                .Fact("econ_shark_least", () => Guide.Money(Economy.ProductCatalogue.SharkLeast))
                .Fact("econ_shark_most", () => Guide.Money(Economy.ProductCatalogue.SharkMost))
                .Fact("econ_shark_days", () => Whole(Economy.ProductCatalogue.SharkIncomeDays))
                .Fact("econ_shark_rate", () => Guide.Percent(Economy.ProductCatalogue.SharkDailyShare))
                .Fact("econ_shark_due", () => Whole(Economy.ProductCatalogue.SharkDueDays))
                .Fact("econ_shark_extend", () => Guide.Percent(Economy.ProductCatalogue.SharkExtendFeeShare))
                .Fact("econ_shark_cap", () => Whole(Economy.ProductCatalogue.SharkCapMultiple))
                .Fact("econ_shark_warn", () => Whole(Economy.ProductCatalogue.SharkWarnDay))
                .Fact("econ_shark_assault", () => Whole(Economy.ProductCatalogue.SharkAssaultDay))
                .Fact("econ_shark_damage", () => Whole(Economy.ProductCatalogue.SharkDamageDay))
                .Fact("econ_shark_repeat", () => Whole(Economy.ProductCatalogue.SharkRepeatDays))
                .Fact("econ_shark_bill_low", () => Guide.Money(Economy.ProductCatalogue.SharkDamageLeast))
                .Fact("econ_shark_bill_high", () => Guide.Money(Economy.ProductCatalogue.SharkDamageMost))
                .Fact("econ_advice_days", () => Whole(Economy.ProductCatalogue.AdviceOverdueDays))
                .Fact("econ_arr_share", () => Guide.Percent(Economy.ProductCatalogue.ArrangementShare))
                .Fact("econ_arr_least", () => Guide.Money(Economy.ProductCatalogue.ArrangementLeast))
                .Fact("econ_arr_cap", () => Whole(Economy.ProductCatalogue.ArrangementScoreCap))
                .Fact("econ_arr_after", () => Whole(Economy.ProductCatalogue.ArrangementCapDaysAfter))
                .Fact("econ_arr_misses", () => Whole(Economy.ProductCatalogue.ArrangementMissesMost))
                .Fact("econ_bank_keep", () => Guide.Money(Economy.ProductCatalogue.BankruptKeepCash))
                .Fact("econ_bank_score", () => Whole(Economy.ProductCatalogue.BankruptScore))
                .Fact("econ_bank_days", () => Whole(Economy.ProductCatalogue.BankruptNoCreditDays))
                .Fact("econ_bank_from", () => Whole(Economy.ProductCatalogue.BankruptRebuildFrom))
                .Fact("econ_bank_per_day", () => Whole(Economy.ProductCatalogue.BankruptRebuildPerDay))
                .Fact("econ_bank_every", () => Whole(Economy.ProductCatalogue.BankruptEveryDays))
                .Fact("econ_car_least", () => Guide.Money(Economy.ProductTerms.FinanceLeast(Economy.ProductKind.CarFinance)))
                .Fact("econ_home_least", () => Guide.Money(Economy.ProductTerms.FinanceLeast(Economy.ProductKind.Mortgage)))
                .Fact("econ_shark_bill_share", () => Guide.Percent(Economy.ProductCatalogue.SharkDamageShare))
                .Cleanup(() => Economy.EconomyDirector.Instance?.StopAll())
                .ResetData(() => Economy.EconomyDirector.Instance?.Forget());   // the director opens a new file from the (now empty) save on its next second

            Features.Add("economy_city", "reallife_meta_title_economy_city")
                .Group("reallife_macro_header")
                .About("reallife_guide_economy_city_about").Help("reallife_guide_economy_city")
                .Owns(Settings.EconomyCityId, Settings.EconomyRateAheadId)
                .Preset(Settings.EconomyCityLevelId, Economy.MacroCatalogue.Relaxed, Economy.MacroCatalogue.Realistic, Economy.MacroCatalogue.Hardcore)
                .Data(Economy.CityStore.Key, Economy.CityStore.TestKey, Economy.CityStore.EveKey)
                .Fact("macro_month_least", () => Whole(Economy.MacroCatalogue.MonthDaysLeast))
                .Fact("macro_base_low", () => CityPoints(-Economy.MacroCatalogue.BaseLeast))
                .Fact("macro_base_high", () => CityPoints(Economy.MacroCatalogue.BaseMost))
                .Fact("macro_move_most", () => CityPoints(Economy.MacroCatalogue.MoveMost))
                .Fact("macro_target", () => CityHundredths(Economy.MacroCatalogue.InflationTarget))
                .Fact("macro_inflation_low", () => CityHundredths(-Economy.MacroCatalogue.InflationLeast))
                .Fact("macro_inflation_high", () => CityHundredths(Economy.MacroCatalogue.InflationMost))
                .Fact("macro_state_least", () => Whole(Economy.MacroCatalogue.StateMonthsLeast))
                .Fact("macro_state_most", () => Whole(Economy.MacroCatalogue.StateMonthsMost))
                .Fact("macro_opening", () => Whole(Economy.MacroCatalogue.OpeningSteadyMonths))
                .Fact("macro_states", CityStates)
                .Fact("macro_income_low", () => Whole(Economy.MacroCatalogue.IncomeLeast / 10))
                .Fact("macro_income_high", () => Whole(Economy.MacroCatalogue.IncomeMost / 10))
                .Fact("macro_income_band", () => Whole(Economy.MacroCatalogue.IncomeBand / 10))
                .Fact("macro_over_band", () => Whole(Economy.MacroCatalogue.OverBand / 10))
                .Fact("macro_over_loss", () => Guide.Number(Economy.MacroCatalogue.OverLossPerPercent / 1000f))
                .Fact("macro_over_most", () => Whole(Economy.MacroCatalogue.OverLossMost))
                .Fact("macro_under_gain", () => Whole(Economy.MacroCatalogue.UnderGain))
                .Fact("macro_footfall_low", () => Whole(-Economy.MacroCatalogue.FootfallLeast))
                .Fact("macro_footfall_high", () => Whole(Economy.MacroCatalogue.FootfallMost))
                .Fact("macro_factor_low", () => Whole(100 - Economy.MacroCatalogue.FactorLeast / 10))
                .Fact("macro_factor_high", () => Whole(Economy.MacroCatalogue.FactorMost / 10 - 100))
                .Fact("macro_shock_days_low", () => Whole(Economy.MacroCatalogue.ShockDaysLeast))
                .Fact("macro_shock_days_high", () => Whole(Economy.MacroCatalogue.ShockDaysMost))
                .Fact("macro_adjust_band", () => Whole(Economy.MacroCatalogue.AdjustQuiet / 10))
                .Fact("macro_copycat_days", () => Whole(Economy.MacroCatalogue.CopycatDays))
                .Fact("macro_copycat_rest", () => Whole(Economy.MacroCatalogue.CopycatRestDays))
                .Fact("macro_swing_relaxed", () => Whole(Economy.MacroCatalogue.SwingPercent[Economy.MacroCatalogue.Relaxed]) + "%")
                .Fact("macro_swing_realistic", () => Whole(Economy.MacroCatalogue.SwingPercent[Economy.MacroCatalogue.Realistic]) + "%")
                .Fact("macro_swing_hardcore", () => Whole(Economy.MacroCatalogue.SwingPercent[Economy.MacroCatalogue.Hardcore]) + "%")
                .Fact("macro_eve_hour", () => Economy.CityModel.DecisionEveHour.ToString(CultureInfo.InvariantCulture) + ":00")
                .Fact("macro_shocks_most", () => Whole(Economy.MacroCatalogue.ShocksAtOnce))
                .Cleanup(() => Economy.CityEconomyDirector.Instance?.StopAll(), -1)
                .ResetData(() => Economy.CityEconomyDirector.Instance?.Forget());   // the director opens a new economy from the (now empty) save on its next second
            // Its rows stand under the economy's header.
            Features.Add("ventures", "reallife_meta_title_ventures")
                .About("reallife_guide_ventures_about").Help("reallife_guide_ventures")
                .Owns(Settings.VenturesInvestorsId, Settings.VenturesLevelId)
                .Preset(Settings.VenturesLevelId, Economy.Ventures.VenturesCatalogue.Relaxed, Economy.Ventures.VenturesCatalogue.Realistic, Economy.Ventures.VenturesCatalogue.Hardcore)
                .Data(Economy.Ventures.VenturesStore.Key)
                .Test("reallife_ven_test_offer", "reallife_ven_test_pitch_angel", "reallife_ven_test_pitch_tycoon", "reallife_ven_test_pitch_firm", "reallife_ven_test_list",
                    "reallife_ven_test_call", "reallife_ven_test_back")
                .Fact("ven_profit_days", () => Whole(Economy.Ventures.VenturesCatalogue.ProfitDaysNeeded))
                .Fact("ven_window", () => Whole(Economy.Ventures.VenturesCatalogue.ProfitWindowDays))
                .Fact("ven_average_days", () => Whole(Economy.Ventures.VenturesCatalogue.AverageDays))
                .Fact("ven_profit_floor", () => Guide.Money(Economy.Ventures.VenturesCatalogue.OfferProfitFloor))
                .Fact("ven_offer_every", () => Whole(Economy.Ventures.VenturesCatalogue.OfferEveryDays))
                .Fact("ven_offer_chance", () => Whole(Economy.Ventures.VenturesCatalogue.OfferChancePct) + "%")
                .Fact("ven_offer_liked", () => Whole(Economy.Ventures.VenturesCatalogue.OfferChancePct + Economy.Ventures.VenturesCatalogue.LikedBonusPct) + "%")
                .Fact("ven_value_days", () => Whole(Economy.Ventures.VenturesCatalogue.OfferValueDays))
                .Fact("ven_lapse_days", () => Whole(Economy.Ventures.VenturesCatalogue.OfferLapseDays))
                .Fact("ven_counter_money", () => Whole(Economy.Ventures.VenturesCatalogue.CounterMoneyPct) + "%")
                .Fact("ven_counter_share", () => Whole(Economy.Ventures.VenturesCatalogue.CounterSharePct) + "%")
                .Fact("ven_questions_least", () => Whole(Economy.Ventures.VenturesCatalogue.Questions[0]))
                .Fact("ven_questions_most", () => Whole(Economy.Ventures.VenturesCatalogue.Questions[2]))
                .Fact("ven_bank", () => Whole(Economy.Ventures.Pitch.Bank.Length))
                .Fact("ven_truth_base", () => Whole(Economy.Ventures.VenturesCatalogue.TruthBase))
                .Fact("ven_points", () => Whole(Economy.Ventures.VenturesCatalogue.PointsMost))
                .Fact("ven_catch_little", () => Whole(Economy.Ventures.VenturesCatalogue.StretchCatchPct) + "%")
                .Fact("ven_catch_much", () => Whole(Economy.Ventures.VenturesCatalogue.BoldCatchPct) + "%")
                .Fact("ven_caught_days", () => Whole(Economy.Ventures.VenturesCatalogue.CaughtCloseDays))
                .Fact("ven_caught_name", () => Whole(-Economy.Ventures.VenturesCatalogue.ReputationCaught))
                .Fact("ven_cold_name", () => Whole(-Economy.Ventures.VenturesCatalogue.ReputationCold))
                .Fact("ven_cooldown", () => Whole(Economy.Ventures.VenturesCatalogue.PitchCooldownDays))
                .Fact("ven_need_angel", () => Whole(Economy.Ventures.VenturesCatalogue.ScoreNeeded[0]))
                .Fact("ven_need_tycoon", () => Whole(Economy.Ventures.VenturesCatalogue.ScoreNeeded[1]))
                .Fact("ven_need_firm", () => Whole(Economy.Ventures.VenturesCatalogue.ScoreNeeded[2]))
                .Fact("ven_pitch_days_least", () => Whole(Economy.Ventures.VenturesCatalogue.ValueDaysLeast))
                .Fact("ven_pitch_days_most", () => Whole(Economy.Ventures.VenturesCatalogue.ValueDaysMost))
                .Fact("ven_budget_least", () => Whole(Economy.Ventures.VenturesCatalogue.BudgetShareLeastPct) + "%")
                .Fact("ven_budget_most", () => Whole(Economy.Ventures.VenturesCatalogue.BudgetShareMostPct) + "%")
                .Fact("ven_pledge_days", () => Whole(Economy.Ventures.VenturesCatalogue.PledgeDays))
                .Fact("ven_young_days", () => Whole(Economy.Ventures.VenturesCatalogue.YoungDays))
                .Fact("ven_share_cap", () => Guide.Percent(Economy.Ventures.VenturesCatalogue.ShareCapBp / 10000f))
                .Fact("ven_demand_days", () => Whole(Economy.Ventures.VenturesCatalogue.DemandDays))
                .Fact("ven_board_extra", () => Guide.Percent(Economy.Ventures.VenturesCatalogue.BoardExtraBp / 10000f))
                .Fact("ven_board_days", () => Whole(Economy.Ventures.VenturesCatalogue.BoardDays))
                .Fact("ven_cut", () => Whole(Economy.Ventures.VenturesCatalogue.CostCutPct) + "%")
                .Fact("ven_cut_days", () => Whole(Economy.Ventures.VenturesCatalogue.CostCutDays))
                .Fact("ven_call_kept", () => Whole(Economy.Ventures.VenturesCatalogue.CallKeptPct) + "%")
                .Fact("ven_call_least", () => Whole(Economy.Ventures.VenturesCatalogue.CallLeastPct) + "%")
                .Fact("ven_call_misses", () => Whole(Economy.Ventures.VenturesCatalogue.CallMissesMost))
                .Fact("ven_levels", VenturesLevels)
                .Fact("ven_buy_out_days", () => Whole(Economy.Ventures.VenturesCatalogue.BuyOutDays))
                .Fact("ven_buy_out_floor", () => Whole(Economy.Ventures.VenturesCatalogue.BuyOutFloorPct) + "%")
                .Fact("ven_offer_slow", () => Whole(Economy.Ventures.VenturesCatalogue.SlowdownChancePct) + "%")
                .Fact("ven_offer_boom", () => Whole(Economy.Ventures.VenturesCatalogue.BoomChancePct) + "%")
                .Fact("ven_offers_most", () => Whole(Economy.Ventures.VenturesCatalogue.OffersMost))
                .Fact("ven_name_start", () => Whole(Economy.Ventures.VenturesCatalogue.ReputationStart))
                .Fact("ven_name_claim", () => Whole(-Economy.Ventures.VenturesCatalogue.ReputationClaim))
                .Fact("ven_pledges_most", () => Whole(Economy.Ventures.VenturesCatalogue.PledgesMost))
                .Fact("ven_deals_most", () => Whole(Economy.Ventures.VenturesCatalogue.DealsMost))
                .Fact("ven_deals_business", () => Whole(Economy.Ventures.VenturesCatalogue.DealsPerBusiness))
                .Fact("ven_follow_days", () => Whole(Economy.Ventures.VenturesCatalogue.FollowOnValueDays))
                .Cleanup(() => Economy.Ventures.VenturesDirector.Instance?.StopAll())
                .ResetData(() => Economy.Ventures.VenturesDirector.Instance?.Forget());   // the director opens a new file from the (now empty) save on its next second

            Features.Add("markets", "reallife_meta_title_markets")
                .Group("reallife_mkt_header")
                .About("reallife_guide_markets_about").Help("reallife_guide_markets")
                .Owns(Settings.MarketsId, Settings.MarketsMarginId, Settings.MarketsDividendNotesId, Settings.MarketsBondsId, Settings.MarketsBondSoFarId, Settings.MarketsHoldingId)
                .Test("reallife_mkt_bond_test_open", "reallife_mkt_bond_test_pay", "reallife_mkt_test_holding")
                .Data(Markets.MarketStore.Key)
                .Fact("mkt_companies", () => Whole(MarketsListed()))
                .Fact("mkt_sectors", () => Whole((int)Markets.MarketSector.Count))
                .Fact("mkt_open", () => MarketsTime(Markets.MarketCatalogue.OpenMinute))
                .Fact("mkt_close", () => MarketsTime(Markets.MarketCatalogue.CloseMinute))
                .Fact("mkt_step", () => Whole(Markets.MarketCatalogue.StepMinutes))
                .Fact("mkt_record_days", () => Whole(Markets.MarketCatalogue.DividendRecordDays))
                .Fact("mkt_pay_days", () => Whole(Markets.MarketCatalogue.DividendPayDays))
                .Fact("mkt_split", () => Guide.Money(Markets.MarketCatalogue.SplitAboveCents / 100f))
                .Fact("mkt_premium_least", () => Guide.Percent((float)Markets.MarketCatalogue.TakeoverPremiumLeast))
                .Fact("mkt_premium_most", () => Guide.Percent((float)Markets.MarketCatalogue.TakeoverPremiumMost))
                .Fact("mkt_fail", () => Guide.Percent((float)Markets.MarketCatalogue.TakeoverFailChance))
                .Fact("mkt_penny", () => Guide.Money(Prices.At(Markets.MarketCatalogue.PennyCents / 100f)))
                .Fact("mkt_penny_days", () => Whole(Markets.MarketCatalogue.PennyDays))
                .Fact("mkt_good_till", () => Whole(Markets.MarketCatalogue.GoodTillDays))
                .Fact("mkt_commission", () => Guide.Percent(Markets.MarketCatalogue.CommissionBp / 10000f))
                .Fact("mkt_commission_least", () => Guide.Money(Prices.At(Markets.MarketCatalogue.CommissionLeastAtStart)))
                .Fact("mkt_order_share", () => Whole(Markets.MarketCatalogue.OrderShareOfVolumePct) + "%")
                .Fact("mkt_holding", () => Guide.Percent(Markets.MarketTrading.HoldingBp / 10000f))
                .Fact("mkt_margin_open", () => Guide.Money(Prices.At(Markets.MarketCatalogue.MarginOpenLeastAtStart)))
                .Fact("mkt_initial", () => Whole(Markets.MarketCatalogue.InitialMarginPct) + "%")
                .Fact("mkt_maintenance_long", () => Whole(Markets.MarketCatalogue.MaintenanceLongPct) + "%")
                .Fact("mkt_maintenance_short", () => Whole(Markets.MarketCatalogue.MaintenanceShortPct) + "%")
                .Fact("mkt_risky_extra", () => Whole(Markets.MarketCatalogue.MaintenanceRiskyExtraPct) + "%")
                .Fact("mkt_forced", () => Whole(Markets.MarketCatalogue.ForcedBelowPct) + "%")
                .Fact("mkt_margin_rate", () => Guide.Percent(Markets.MarketCatalogue.MarginRateOverBp / 10000f))
                .Fact("mkt_borrow_least", () => Guide.Percent(Markets.MarketCatalogue.BorrowFeeLeastBp / 10000f))
                .Fact("mkt_borrow_most", () => Guide.Percent(Markets.MarketCatalogue.BorrowFeeMostBp / 10000f))
                .Fact("mkt_trusts", () => Whole(MarketsTrusts()))
                .Fact("mkt_trust_payout", () => Guide.Percent((float)Markets.MarketCatalogue.TrustPayout))
                .Fact("mkt_trust_property", () => Guide.Percent((float)Markets.MarketCatalogue.TrustPropertyShare))
                .Fact("bond_least", () => Guide.Money(Prices.At(Markets.BondCatalogue.LeastAtStart)))
                .Fact("bond_most", () => Guide.Money(Prices.At(Markets.BondCatalogue.MostAtStart)))
                .Fact("bond_holdings", () => Whole(Markets.BondCatalogue.HoldingsMost))
                .Fact("bond_term_premium", () => Guide.Percent(Markets.BondCatalogue.TermPremiumBpYear / 10000f))
                .Fact("bond_term_premium_most", () => Guide.Percent(Markets.BondCatalogue.TermPremiumMostBp / 10000f))
                .Fact("bond_city_spread", () => Guide.Percent(Markets.BondCatalogue.CitySpreadBp / 10000f))
                .Fact("bond_deposit_under", () => Guide.Percent(Markets.BondCatalogue.DepositUnderBp / 10000f))
                .Fact("bond_deposit_point", () => Guide.Percent(Markets.BondCatalogue.DepositPerPointBp / 10000f))
                .Fact("bond_sell_treasury", () => Guide.Percent(Markets.BondCatalogue.SellSpreadTreasuryBp / 10000f))
                .Fact("bond_sell_city", () => Guide.Percent(Markets.BondCatalogue.SellSpreadCityBp / 10000f))
                .Fact("bond_penalty_months", () => Whole(Markets.BondCatalogue.DepositPenaltyMonths))
                .Fact("bond_short_years", () => Whole((int)Markets.BondCatalogue.ShortYears))
                .Fact("mkt_holding_own", () => Guide.Percent(Markets.MarketCatalogue.HoldingCapBp[0] / 10000f))
                .Fact("mkt_holding_float", () => Guide.Percent(Markets.MarketCatalogue.HoldingFloatBp / 10000f))
                .Fact("mkt_orders_most", () => Whole(Markets.MarketCatalogue.OrdersMost))
                .Fact("mkt_watch_most", () => Whole(Markets.MarketCatalogue.WatchMost))
                .Cleanup(() => Markets.MarketDirector.Instance?.StopAll())
                .ResetData(() => Markets.MarketDirector.Instance?.Forget());   // the director opens a new market from the (now empty) save on its next second
            Markets.ListingGuide.Register();
            Markets.TradingGuide.Register();
            Markets.DerivGuide.Register();

            Features.Add("economy_banks", "reallife_meta_title_economy_banks")
                .About("reallife_guide_economy_banks_about").Help("reallife_guide_economy_banks")
                .Owns(Settings.EconomyBanksId)
                .Fact("bank_threshold", () => Whole(Economy.CreditCatalogue.LendingThreshold))
                .Test("reallife_bank_test_check", "reallife_bank_test_open");

            Economy.CompanyGuide.AddFacts(Features.Add("economy_company", "reallife_meta_title_economy_company")
                .Group("reallife_co_header")
                .About("reallife_guide_economy_company_about").Help("reallife_guide_economy_company")
                .Owns(Settings.EconomyCompanyId, Settings.EconomyCompanyStaffId, Settings.EconomyCompanyCoverId, Settings.EconomyCompanyMarketingId,
                    Settings.EconomyCompanyTaxId, Settings.EconomyCompanyHideId, Settings.EconomyCompanyLicencesId)
                .Preset(Settings.EconomyCompanyLevelId, Economy.CompanyCatalogue.Relaxed, Economy.CompanyCatalogue.Realistic, Economy.CompanyCatalogue.Hardcore)
                .Data(Economy.CompanyStore.Key, Economy.CompanyStore.TestKey)
                .Cleanup(() => Economy.CompanyDirector.Instance?.StopAll(), -2)
                .ResetData(() => Economy.CompanyDirector.Instance?.Forget()));   // the director opens the feature afresh from the (now empty) save on its next second

            // Its rows stand under the company's header.
            Economy.RotaGuide.AddFacts(Features.Add("economy_rota", "reallife_meta_title_economy_rota")
                .About("reallife_guide_economy_rota_about").Help("reallife_guide_economy_rota")
                .Owns(Settings.EconomyRotaId)
                .Data(Economy.RotaStore.Key)
                .Test("reallife_rota_test_open", "reallife_rota_test_plans", "reallife_rota_test_fill", "reallife_rota_test_back"));

            // Its rows stand under the company's header too.
            Economy.ManagerGuide.AddFacts(Features.Add("economy_managers", "reallife_meta_title_economy_managers")
                .About("reallife_guide_economy_managers_about").Help("reallife_guide_economy_managers")
                .Owns(Settings.EconomyManagersId)
                .Data(Economy.ManagerStore.Key)
                .Test("reallife_mgr_test_appoint", "reallife_mgr_test_fill", "reallife_mgr_test_decide", "reallife_mgr_test_undo"));

            // Over the managers, under the company's header too.
            Economy.HierarchyGuide.AddFacts(Features.Add("economy_hierarchy", "reallife_meta_title_economy_hierarchy")
                .About("reallife_guide_economy_hierarchy_about").Help("reallife_guide_economy_hierarchy")
                .Owns(Settings.EconomyHierarchyId)
                .Data(Economy.HierarchyStore.Key)
                .Test("reallife_hier_test_store", "reallife_hier_test_region", "reallife_hier_test_executives", "reallife_hier_test_owner",
                    "reallife_hier_test_gap", "reallife_hier_test_budget", "reallife_hier_test_demand",
                    "reallife_hier_test_mood", "reallife_hier_test_wave", "reallife_hier_test_kpi", "reallife_hier_test_exception"));

            Economy.EstateGuide.AddFacts(Features.Add("economy_estate", "reallife_meta_title_economy_estate")
                .Group("reallife_estate_header")
                .About("reallife_guide_economy_estate_about").Help("reallife_guide_economy_estate")
                .Owns(Settings.EstateId, Settings.EstateHotelId, Settings.EstateHireId)
                .Preset(Settings.EstateRentId, 150, 100, 80)   // rental income, per cent
                .StandsAside(GoingPublic.OwnershipSentence, Settings.EstateHireId)   // a hired car leaves the city and comes back as another
                .Data(Economy.EstateStore.Key, Economy.EstateStore.TestKey)
                .Cleanup(() => Economy.EstateDirector.Instance?.StopAll(), -2)
                .ResetData(() => Economy.EstateDirector.Instance?.Forget()));   // the director opens the feature afresh from the (now empty) save on its next second
            // A landlord's duties: a part of the property business, with its own switch and Guide page; its state is the property's line.
            Economy.EstateGuide.AddDutyFacts(Features.Add("economy_estate_duties", "reallife_meta_title_economy_estate_duties")
                .Group("reallife_estate_header")
                .About("reallife_guide_economy_estate_duties_about").Help("reallife_guide_economy_estate_duties")
                .Owns(Settings.EstateDutiesId)
                .Test("reallife_estate_test_call", "reallife_estate_test_complaint", "reallife_estate_test_inspection", "reallife_estate_test_manager"));
            // Its rows stand under the company's header.
            Economy.OnlineGuide.AddFacts(Features.Add("economy_online", "reallife_meta_title_economy_online")
                .About("reallife_guide_economy_online_about").Help("reallife_guide_economy_online")
                .Owns(Settings.EconomyOnlineId, Settings.EconomyOnlineWaitNotesId)
                .Test("reallife_online_test_open", "reallife_online_test_orders", "reallife_online_test_late", "reallife_online_test_return", "reallife_online_test_day", "reallife_online_test_back",
                    "reallife_online_test_net")
                .Data(Economy.OnlineStore.Key, Economy.OnlineStore.TestKey)
                .Cleanup(() => Economy.OnlineDirector.Instance?.StopAll(), -2)
                .ResetData(() => Economy.OnlineDirector.Instance?.Forget()));   // the director opens the feature afresh from the (now empty) save on its next second
            Tech.TechGuide.AddPeopleFacts(Features.Add("tech_people", "reallife_meta_title_tech_people")
                .Group("reallife_talent_header")
                .About("reallife_guide_tech_people_about").Help("reallife_guide_tech_people")
                .Owns(Settings.TechPeopleId)
                .Test("reallife_talent_test_review", "reallife_talent_test_offer", "reallife_talent_test_course", "reallife_names_test")
                .Data(Tech.TalentStore.Key, Tech.TechNews.Key)
                .Cleanup(() => Tech.TechDirector.Instance?.StopAll(), -2)
                .ResetData(() => Tech.TechDirector.Instance?.Forget()));
            Tech.TechGuide.AddHiringFacts(Features.Add("tech_jobs", "reallife_meta_title_tech_jobs")
                .About("reallife_guide_tech_jobs_about").Help("reallife_guide_tech_jobs")
                .Owns(Settings.TechJobsId)
                .Test("reallife_hire_test_applicants", "reallife_hire_test_market", "reallife_hire_test_search", "reallife_hire_test_orphan", "reallife_crew_test")
                .Data(Tech.HiringStore.Key)
                .Cleanup(() => Tech.TechDirector.Instance?.StopAll(), -2)
                .ResetData(() => Tech.TechDirector.Instance?.Forget()));
            Tech.TechGuide.AddSectorFacts(Features.Add("tech_sector", "reallife_meta_title_tech_sector")
                .About("reallife_guide_tech_sector_about").Help("reallife_guide_tech_sector")
                .Owns(Settings.TechSectorId, Settings.TechOfferNotesId, Settings.TechInfraOfferId)
                .Test("reallife_tech_test_offer", "reallife_tech_test_tender", "reallife_tech_test_outage", "reallife_tech_test_breach", "reallife_infra_test_down",
                    "reallife_infra_test_price_war", "reallife_infra_test_offer", "reallife_tech_test_net")
                .Data(Tech.FirmsStore.Key, Tech.InfraStore.Key)
                .Cleanup(() => Tech.TechDirector.Instance?.StopAll(), -2)
                .ResetData(() => Tech.TechDirector.Instance?.Forget()));
            Tech.TechGuide.AddVentureFacts(Features.Add("tech_startups", "reallife_meta_title_tech_startups")
                .About("reallife_guide_tech_startups_about").Help("reallife_guide_tech_startups")
                .Owns(Settings.TechStartupsId)
                .Test("reallife_vc_test_pitch", "reallife_vc_test_sale", "reallife_vc_test_board")
                .Data(Tech.VentureStore.Key)
                .Cleanup(() => Tech.TechDirector.Instance?.StopAll(), -2)
                .ResetData(() => Tech.TechDirector.Instance?.Forget()));
            Features.Add("tech_sites", "reallife_meta_title_tech_sites")
                .About("reallife_guide_tech_sites_about").Help("reallife_guide_tech_sites")
                .Fact("sites_most", () => Guide.Number(Tech.ServerRoomPlan.MostShown))
                .Fact("sites_companies", () => Guide.Number(Tech.HiringCatalogue.RivalFirms.Length))
                .Fact("sites_towers", () => Guide.Number(Tech.HqTowers.Towers(Tech.HiringCatalogue.RivalFirms.Length)))
                .Fact("sites_floors", () => Guide.Number(Tech.HqTowers.PerTower))
                .Owns(Settings.TechSiteRoomsId, Settings.TechSiteHqId, Settings.TechSiteCloudId, Settings.TechSiteOfficesId)
                .Test("reallife_sites_test_room", "reallife_sites_test_hq", "reallife_sites_test_floor")
                .Cleanup(() => Tech.TechSites.Instance?.StopAll(), -1);
            // Its row stands under the company's header.
            Economy.Dealership.DealerGuide.AddFacts(Features.Add("economy_dealer", "reallife_meta_title_economy_dealer")
                .About("reallife_guide_economy_dealer_about").Help("reallife_guide_economy_dealer")
                .Fact("showroom_shown", () => Guide.Number(Economy.Dealership.FloorCatalogue.ShownMost))
                .Fact("showroom_at_once", () => Guide.Number(Economy.Dealership.FloorCatalogue.AtOnce))
                .Owns(Settings.EconomyDealerId, Settings.EconomyDealerFloorId)
                .Test("reallife_dealer_test_stock", "reallife_dealer_test_customers", "reallife_dealer_test_auction", "reallife_dealer_test_week",
                    "reallife_dealer_test_event", "reallife_dealer_test_arrive",
                    "reallife_dealer_test_mission", "reallife_dealer_test_back", "reallife_showroom_test_floor")
                .Data(Economy.Dealership.DealerStore.Key, Economy.Dealership.DealerStore.TestKey, Economy.Dealership.ShowroomBayStore.Key)
                .Cleanup(() => Economy.Dealership.DealerDirector.Instance?.StopAll(), -2)
                .ResetData(() => Economy.Dealership.DealerDirector.Instance?.Forget()));   // the director opens the feature afresh from the (now empty) save on its next second
            // Their rows stand under the company's header; they share one line of the save.
            Economy.ServicesGuide.AddCleaningFacts(Features.Add("economy_cleaning", "reallife_meta_title_economy_cleaning")
                .About("reallife_guide_economy_cleaning_about").Help("reallife_guide_economy_cleaning")
                .Owns(Settings.EconomyCleaningId)
                .Test("reallife_svc_test_round", "reallife_svc_test_contract", "reallife_svc_test_week")
                .Data(Economy.ServicesStore.Key)
                .Cleanup(() => Economy.ServicesDirector.Instance?.StopAll(), -3)
                .ResetData(() => Economy.ServicesDirector.Instance?.Forget()));   // the director opens the feature afresh from the (now empty) save on its next second
            Economy.ServicesGuide.AddSaleFacts(Features.Add("economy_sale", "reallife_meta_title_economy_sale")
                .About("reallife_guide_economy_sale_about").Help("reallife_guide_economy_sale")
                .Owns(Settings.EconomySaleId)
                .StandsAside(GoingPublic.OwnershipSentence, Settings.EconomySaleId)   // who rents what is the host's record in a session
                .Test("reallife_svc_test_offer", "reallife_svc_test_sale_paper", "reallife_svc_test_part"));
            Features.Add("office_space", "reallife_meta_title_office_space")
                .About("reallife_guide_office_space_about").Help("reallife_guide_office_space")
                .Fact("offices_district", () => Guide.Number(Offices.ConversionRules.DistrictFloor))
                .Fact("offices_city", () => Guide.Number(Offices.ConversionRules.CityFloor))
                .Fact("offices_kept_1", () => Offices.ConversionGame.PlayerOffice(0))
                .Fact("offices_kept_2", () => Offices.ConversionGame.PlayerOffice(1))
                .Fact("util_address", Offices.ConversionGame.UtilitiesAddress)
                .Owns(Settings.OfficeConversionId)
                .Test("reallife_offices_test_list")
                .Data(Offices.ConversionStore.Key)
                .Cleanup(() => Offices.Conversions.Instance?.StopAll(), -1)
                .ResetData(() => Offices.Conversions.Instance?.Forget());
            Economy.Brands.BrandsGuide.AddFacts(Features.Add("economy_brands", "reallife_meta_title_economy_brands")
                .Group("reallife_brand_header")
                .About("reallife_guide_economy_brands_about").Help("reallife_guide_economy_brands")
                .Owns(Settings.EconomyBrandsId)
                .Data(Economy.Brands.BrandStore.Key, Economy.Brands.BrandStore.TestKey)
                .Cleanup(() => Economy.Brands.BrandsDirector.Instance?.StopAll(), -3)
                .ResetData(() => Economy.Brands.BrandsDirector.Instance?.Forget()));   // the director opens the feature afresh from the (now empty) save on its next second
        }

        // The companies listed when a market opens.
        private static int MarketsListed()
        {
            int listed = 0;
            for (int i = 0; i < Markets.MarketCatalogue.All.Length; i++)
            {
                listed += Markets.MarketCatalogue.All[i].Pipeline ? 0 : 1;
            }
            return listed;
        }

        private static int MarketsTrusts()
        {
            int trusts = 0;
            for (int i = 0; i < Markets.MarketCatalogue.All.Length; i++)
            {
                trusts += Markets.MarketCatalogue.All[i].PayDays > 0 ? 1 : 0;
            }
            return trusts;
        }

        // Quarter points as points, and hundredths of a per cent as per cent, for the city's economy's page.
        private static string CityPoints(int quarters)
        {
            return (quarters / (float)Economy.MacroCatalogue.QuartersPerPoint).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string CityHundredths(int hundredths)
        {
            return (hundredths / 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        // One line per state of the cycle, from the table the model itself reads.
        private static string CityStates()
        {
            var text = new StringBuilder();
            for (int s = 0; s < Economy.MacroCatalogue.StateCount; s++)
            {
                if (s == (int)Economy.CycleState.Steady)
                {
                    continue;   // the state everything is measured against
                }
                text.Append("- ").Append("reallife_guide_economy_city_state".GetLocalization()
                    .Replace("{state}", Economy.CityText.StateKeys[s].GetLocalization())
                    .Replace("{footfall}", Economy.CityText.Percent(Economy.MacroCatalogue.StateFootfall[s]))
                    .Replace("{spend}", Economy.CityText.Change(Economy.MacroCatalogue.StateSpend[s]))
                    .Replace("{property}", Economy.CityText.Change(Economy.MacroCatalogue.StateProperty[s]))).Append('\n');
            }
            return text.ToString();
        }

        // One line per level of "How demanding investors are", from the tables the deals themselves count by.
        private static string VenturesLevels()
        {
            var text = new StringBuilder();
            string[] names = { "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore" };
            for (int level = 0; level < names.Length; level++)
            {
                int misses = Economy.Ventures.VenturesCatalogue.MissesToCall[level];
                text.Append("- ").Append((misses > 0 ? "reallife_guide_ventures_level" : "reallife_guide_ventures_level_never").GetLocalization()
                    .Replace("{level}", names[level].GetLocalization())
                    .Replace("{growth}", Whole(Economy.Ventures.VenturesCatalogue.GrowthScalePct[level]) + "%")
                    .Replace("{misses}", Whole(misses))
                    .Replace("{days}", Whole(Economy.Ventures.VenturesCatalogue.CallDays[level]))).Append('\n');
            }
            return text.ToString();
        }

        // One line per level of "How hard not paying hits", from the table the lenders themselves count by.
        private static string EconomyLadder()
        {
            var text = new StringBuilder();
            string[] names = { "reallife_meta_preset_relaxed", "reallife_meta_preset_standard", "reallife_meta_preset_realistic", "reallife_meta_preset_hardcore" };
            for (int level = 0; level < names.Length; level++)
            {
                int[] counts = Economy.ProductCatalogue.Counts(level);
                var last = Economy.ProductCatalogue.LastStep[level];
                text.Append("- ").Append("reallife_guide_economy_ladder".GetLocalization()
                    .Replace("{level}", names[level].GetLocalization())
                    .Replace("{reminder}", Whole(counts[0])).Replace("{fee}", Whole(counts[1])).Replace("{rate}", Whole(counts[2])).Replace("{call}", Whole(counts[3])));
                if (last >= Economy.LadderStep.Repossession)
                {
                    text.Append("reallife_guide_economy_ladder_security".GetLocalization().Replace("{count}", Whole(counts[4])));
                }
                text.Append((last >= Economy.LadderStep.Claim ? "reallife_guide_economy_ladder_claim" : "reallife_guide_economy_ladder_stop").GetLocalization().Replace("{count}", Whole(counts[5])));
                if (level == Economy.ProductCatalogue.Relaxed)
                {
                    text.Append(' ').Append("reallife_guide_economy_ladder_half".GetLocalization());
                }
                text.Append('\n');
            }
            return text.ToString();
        }

        // One line per grade, from the catalogue the rating itself is graded by.
        private static string EconomyBands()
        {
            var text = new StringBuilder();
            for (int g = 0; g < Economy.CreditCatalogue.GradeFrom.Length; g++)
            {
                text.Append("- ").Append("reallife_guide_economy_band".GetLocalization()
                    .Replace("{from}", Whole(Economy.CreditCatalogue.GradeFrom[g]))
                    .Replace("{to}", Whole(Economy.CreditCatalogue.GradeTo((Economy.CreditGrade)g)))
                    .Replace("{grade}", Economy.CreditCatalogue.GradeKeys[g].GetLocalization())).Append('\n');
            }
            return text.ToString();
        }
    }
}
