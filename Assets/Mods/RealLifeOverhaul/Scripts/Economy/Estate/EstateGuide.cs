using System;
using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The figures the Guide's page "Property" prints, from the catalogue and the rules, worked out when the page opens.</summary>
    public static class EstateGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly int[] Seeds = { 977, 1954, 2931, 3908, 4885, 5862, 6839, 7816 };

        public static void AddFacts(Feature f)
        {
            f.Fact("est_run_down", () => EstateText.Share(EstateCatalogue.RunDownShare))
                .Fact("est_boom", () => EstateText.Share(EstateCatalogue.RealTarget[0]))
                .Fact("est_recession", () => EstateText.Share(EstateCatalogue.RealTarget[3]))
                .Fact("est_step", () => EstateText.Share(EstateCatalogue.RealStepMost))
                .Fact("est_yield_low", () => EstateText.Share(EstateCatalogue.YieldLeast))
                .Fact("est_yield_high", () => EstateText.Share(EstateCatalogue.YieldMost))
                .Fact("est_ask_low", () => EstateText.Share(EstateCatalogue.AskLeast))
                .Fact("est_ask_high", () => EstateText.Share(EstateCatalogue.AskMost))
                .Fact("est_rent_scale", () => EstateText.Share(EstateRules.RentShare(Settings.EstateRent * 10)))
                .Fact("est_rent_least", () => EstateText.Share(EstateCatalogue.RentScaleLeast))
                .Fact("est_rent_most", () => EstateText.Share(EstateCatalogue.RentScaleMost))
                .Fact("est_applicants", () => Whole(EstateCatalogue.ApplicantsMost))
                .Fact("est_applicant_days", () => Whole(EstateCatalogue.ApplicantDays))
                .Fact("est_applicants_building", () => Whole(EstateCatalogue.ApplicantsBuildingMost))
                .Fact("est_units", () => Whole(EstateCatalogue.UnitsMost))
                .Fact("est_deposit_days", () => Whole(EstateCatalogue.DepositDays))
                .Fact("est_arrears_days", () => Whole(EstateCatalogue.NoticeArrearsDays))
                .Fact("est_notice_days", () => Whole(EstateCatalogue.NoticeDays[1]))
                .Fact("est_court_days", () => Whole(EstateCatalogue.CourtDays))
                .Fact("est_court_fee", () => Guide.Money(EstateRules.CourtFee(Prices.Level)))
                .Fact("est_poor", () => EstateText.Share(EstateCatalogue.ConditionPoor))
                .Fact("est_unfit", () => EstateText.Share(EstateCatalogue.ConditionUnfit))
                .Fact("est_repair", () => EstateText.Share(EstateCatalogue.RepairShare))
                .Fact("est_cover", () => EstateText.Share(EstateCatalogue.CoverShare))
                .Fact("est_cover_excess", () => EstateText.Share(EstateCatalogue.CoverExcess))
                .Fact("est_levels", () => Whole(EstateCatalogue.LevelMost))
                .Fact("est_level_value", () => EstateText.Share(EstateCatalogue.LevelValue[EstateCatalogue.LevelMost]))
                .Fact("est_level_rent", () => EstateText.Share(EstateCatalogue.LevelRent[EstateCatalogue.LevelMost]))
                .Fact("est_planning_days", () => Whole(EstateCatalogue.PlanningDays))
                .Fact("est_planning_chance", () => EstateText.Share(EstateCatalogue.PlanningChance[1]))
                .Fact("est_offer_days", () => Whole(EstateCatalogue.SaleOfferDays))
                .Fact("est_completion_days", () => Whole(EstateCatalogue.CompletionDays))
                .Fact("est_agent_fee", () => EstateText.Share(EstateCatalogue.AgentFee))
                .Fact("est_gains", () => EstateText.Share(EstateCatalogue.GainsShare))
                .Fact("est_tax_threshold", () => Guide.Money(EstateCatalogue.TaxThreshold))
                .Fact("est_instructions", () => Whole(EstateCatalogue.InstructionsMost))
                .Fact("est_answer_days", () => Whole(EstateCatalogue.InstructionAnswerDays))
                .Fact("est_lapse_days", () => Whole(EstateCatalogue.InstructionLapseDays))
                .Fact("est_commission_sale", () => EstateText.Share(EstateCatalogue.CommissionSale))
                .Fact("est_commission_let", () => EstateText.Share(EstateCatalogue.CommissionLet))
                .Fact("est_hotel_years", () => (EstateCatalogue.HotelMultiple / 1000f).ToString("0.#", Inv))
                .Fact("est_hotel_occupancy", () => EstateText.Share(EstateCatalogue.Occupancy))
                .Fact("est_sale_hours", () => Whole(EstateCatalogue.SaleHours))
                .Fact("est_sale_sqm", () => Whole(EstateCatalogue.SaleSqmPerHour))
                .Fact("est_let_hours", () => Whole(EstateCatalogue.LetHours))
                .Fact("est_let_sqm", () => Whole(EstateCatalogue.LetSqmPerHour))
                .Fact("est_rep_done", () => Whole(EstateCatalogue.RepCompleted))
                .Fact("est_rep_lapsed", () => Whole(-EstateCatalogue.RepLapsed))
                .Fact("est_clean_basic", () => EstateText.Level(EstateCatalogue.CleanTarget[0]))
                .Fact("est_clean_standard", () => EstateText.Level(EstateCatalogue.CleanTarget[1]))
                .Fact("est_clean_excellent", () => EstateText.Level(EstateCatalogue.CleanTarget[2]))
                .Fact("est_rating_staff", () => EstateText.Share(EstateCatalogue.RatingStaff))
                .Fact("est_lease_days", () => Whole(EstateCatalogue.LeaseDepositDays))
                .Fact("est_rate_low", () => EstateText.Share(EstateCatalogue.RateLeast))
                .Fact("est_rate_high", () => EstateText.Share(EstateCatalogue.RateMost))
                .Fact("est_inspection", () => Whole(EstateCatalogue.InspectionEvery))
                .Fact("est_inspection_pass", () => EstateText.Share(EstateCatalogue.InspectionPass))
                .Fact("est_group_discount", () => EstateText.Share(EstateCatalogue.GroupDiscount))
                .Fact("est_campaign_days", () => Whole(EstateCatalogue.CampaignDays))
                .Fact("est_hire_metres", () => Whole(EstateCatalogue.HireLotMetres))
                .Fact("est_hire_spaces", () => Whole(EstateCatalogue.HireSpacesAnyBuilding))
                .Fact("est_hire_cars", () => Whole(EstateCatalogue.HireCarsMost))
                .Fact("est_hire_days", () => Whole(EstateCatalogue.HireDaysMost))
                .Fact("est_hire_deposit", () => Whole(EstateCatalogue.HireDepositDays))
                .Fact("est_hire_damage", () => EstateText.Share(EstateCatalogue.HireDamageChance))
                .Fact("est_hire_late", () => EstateText.Share(EstateCatalogue.HireLateChance))
                .Fact("est_hire_stolen", () => EstateText.Share(EstateCatalogue.HireStolenChance))
                .Fact("est_hire_cover", () => EstateText.Share(EstateCatalogue.HireCoverShare))
                .Fact("est_hire_pays", () => EstateText.Share(EstateCatalogue.HireCoverPays))
                .Fact("est_return_let", () => EstateText.Yield(Let(1000)))
                .Fact("est_return_let_poor", () => EstateText.Yield(Let(EstateCatalogue.IncomeLeast)))
                .Fact("est_return_let_rich", () => EstateText.Yield(Let(EstateCatalogue.IncomeMost)))
                .Fact("est_return_hire", () => EstateText.Yield(Hire()))
                .Fact("est_return_shop", () => EstateText.Yield(EstateBalance.ShopYearlyBp(1)))
                .Fact("est_lease_least", () => Whole(EstateCatalogue.TermLeast))
                .Fact("est_lease_most", () => Whole(EstateCatalogue.TermLeast + EstateCatalogue.TermStep * (EstateCatalogue.TermSteps - 1)))
                .Fact("est_hotel_sell", () => EstateText.Share(EstateCatalogue.HotelSellShare))
                .Fact("est_holdings", () => Whole(EstateCatalogue.HoldingsMost));
        }

        /// <summary>The figures the Guide's page "A landlord's duties" prints.</summary>
        public static void AddDutyFacts(Feature f)
        {
            f.Fact("duty_grace", () => Whole(EstateCatalogue.CallGraceDays))
                .Fact("duty_call_least", () => EstateText.Share(Month(EstateRules.CallChance(1000))))
                .Fact("duty_call_poor", () => EstateText.Share(Month(EstateRules.CallChance(EstateCatalogue.ConditionPoor))))
                .Fact("duty_call_share", () => EstateText.Share(EstateCatalogue.CallShare))
                .Fact("duty_diy", () => EstateText.Share(EstateCatalogue.DiyShare))
                .Fact("duty_diy_days", () => Whole(EstateCatalogue.DiyDays))
                .Fact("duty_lapse", () => Whole(EstateCatalogue.CallLapseDays))
                .Fact("duty_complain", () => EstateText.Level(EstateCatalogue.ComplainBelow))
                .Fact("duty_quit", () => EstateText.Level(EstateCatalogue.QuitBelow))
                .Fact("duty_stay", () => EstateText.Level(EstateCatalogue.QuitBelow + EstateCatalogue.StayAbove))
                .Fact("duty_quit_days", () => Whole(EstateCatalogue.QuitDays))
                .Fact("duty_renew_low", () => EstateText.Share(EstateRules.RenewChance(EstateCatalogue.HappyPoor)))
                .Fact("duty_renew_high", () => EstateText.Share(EstateRules.RenewChance(EstateCatalogue.HappyGood + 150)))
                .Fact("duty_to_city", () => Whole(EstateCatalogue.ComplaintsToCity))
                .Fact("duty_city_days", () => Whole(EstateCatalogue.CityCallDays))
                .Fact("duty_inspect", () => Whole(EstateCatalogue.InspectEvery))
                .Fact("duty_pass", () => EstateText.Level(EstateCatalogue.InspectPass))
                .Fact("duty_old", () => Whole(EstateCatalogue.InspectCallOld))
                .Fact("duty_fine", () => Guide.Money(EstateRules.InspectFine(1, Prices.Level)))
                .Fact("duty_fine_again", () => Guide.Money(EstateRules.InspectFine(EstateCatalogue.StrikesBarred, Prices.Level)))
                .Fact("duty_order", () => Whole(EstateCatalogue.OrderDays))
                .Fact("duty_reinspect", () => Whole(EstateCatalogue.ReinspectDays))
                .Fact("duty_plan_low", () => Whole(EstateCatalogue.PlanDaysLeast))
                .Fact("duty_plan_high", () => Whole(EstateCatalogue.PlanDaysMost))
                .Fact("duty_plan_poor", () => EstateText.Share(EstateRules.PlanAcceptChance(EstateCatalogue.RecordLeast)))
                .Fact("duty_plan_good", () => EstateText.Share(EstateRules.PlanAcceptChance(EstateCatalogue.RecordMost)))
                .Fact("duty_agent_days", () => Whole(EstateCatalogue.AgentLetDays))
                .Fact("duty_manager", () => EstateText.Share(EstateCatalogue.ManagerShare))
                .Fact("duty_more", () => EstateText.Share(EstateCatalogue.ManagedApplicants - 1000))
                .Fact("duty_record", () => Whole(EstateCatalogue.ManagerRecordLeast))
                .Fact("duty_city_come", () => Whole(EstateCatalogue.CityDays));
        }

        // A shop building let in a steady economy, averaged over the seeds.
        private static int Let(int income)
        {
            long sum = 0;
            for (int s = 0; s < Seeds.Length; s++)
            {
                sum += EstateBalance.LetYearlyBp((int)EstateUse.Shops, income, 1, Seeds[s]);
            }
            return (int)(sum / Seeds.Length);
        }

        private static int Hire()
        {
            long sum = 0;
            for (int s = 0; s < Seeds.Length; s++)
            {
                sum += EstateBalance.HireYearlyBp(25000, 1, Seeds[s]);
            }
            return (int)(sum / Seeds.Length);
        }

        // The chance in thousandths of at least one fault in thirty days at a chance a day.
        private static int Month(int perDay)
        {
            return (int)Math.Round(1000.0 * (1.0 - Math.Pow(1.0 - perDay / 1000.0, 30)));
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}
