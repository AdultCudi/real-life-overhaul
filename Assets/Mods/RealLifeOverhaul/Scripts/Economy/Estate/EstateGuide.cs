using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The figures the Guide's page "Property" prints: every {name} in its text is filled from
    /// <see cref="EstateCatalogue"/> or worked out by <see cref="EstateBalance"/> here, so the page can never say
    /// anything the rules do not do. Called once, when the feature list is built (Meta/FeatureList); each figure is
    /// worked out when the page is opened (the yearly returns run the rules over a few years of days: a few
    /// milliseconds, once, on that page).
    /// </summary>
    public static class EstateGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly int[] Seeds = { 977, 1954, 2931, 3908, 4885, 5862, 6839, 7816 };

        public static void AddFacts(Feature f)
        {
            f.Fact("est_run_down", () => Share(EstateCatalogue.RunDownShare))
                .Fact("est_boom", () => Share(EstateCatalogue.RealTarget[0]))
                .Fact("est_recession", () => Share(EstateCatalogue.RealTarget[3]))
                .Fact("est_step", () => Share(EstateCatalogue.RealStepMost))
                .Fact("est_yield_low", () => Share(EstateCatalogue.YieldLeast))
                .Fact("est_yield_high", () => Share(EstateCatalogue.YieldMost))
                .Fact("est_ask_low", () => Share(EstateCatalogue.AskLeast))
                .Fact("est_ask_high", () => Share(EstateCatalogue.AskMost))
                .Fact("est_applicants", () => Whole(EstateCatalogue.ApplicantsMost))
                .Fact("est_applicant_days", () => Whole(EstateCatalogue.ApplicantDays))
                .Fact("est_deposit_days", () => Whole(EstateCatalogue.DepositDays))
                .Fact("est_arrears_days", () => Whole(EstateCatalogue.NoticeArrearsDays))
                .Fact("est_notice_days", () => Whole(EstateCatalogue.NoticeDays[1]))
                .Fact("est_court_days", () => Whole(EstateCatalogue.CourtDays))
                .Fact("est_court_fee", () => Guide.Money(EstateRules.CourtFee(Prices.Level)))
                .Fact("est_poor", () => Share(EstateCatalogue.ConditionPoor))
                .Fact("est_unfit", () => Share(EstateCatalogue.ConditionUnfit))
                .Fact("est_repair", () => Share(EstateCatalogue.RepairShare))
                .Fact("est_cover", () => Share(EstateCatalogue.CoverShare))
                .Fact("est_cover_excess", () => Share(EstateCatalogue.CoverExcess))
                .Fact("est_levels", () => Whole(EstateCatalogue.LevelMost))
                .Fact("est_level_value", () => Share(EstateCatalogue.LevelValue[EstateCatalogue.LevelMost]))
                .Fact("est_level_rent", () => Share(EstateCatalogue.LevelRent[EstateCatalogue.LevelMost]))
                .Fact("est_planning_days", () => Whole(EstateCatalogue.PlanningDays))
                .Fact("est_planning_chance", () => Share(EstateCatalogue.PlanningChance[1]))
                .Fact("est_offer_days", () => Whole(EstateCatalogue.SaleOfferDays))
                .Fact("est_completion_days", () => Whole(EstateCatalogue.CompletionDays))
                .Fact("est_agent_fee", () => Share(EstateCatalogue.AgentFee))
                .Fact("est_gains", () => Share(EstateCatalogue.GainsShare))
                .Fact("est_tax_threshold", () => Guide.Money(EstateCatalogue.TaxThreshold))
                .Fact("est_instructions", () => Whole(EstateCatalogue.InstructionsMost))
                .Fact("est_answer_days", () => Whole(EstateCatalogue.InstructionAnswerDays))
                .Fact("est_lapse_days", () => Whole(EstateCatalogue.InstructionLapseDays))
                .Fact("est_commission_sale", () => Share(EstateCatalogue.CommissionSale))
                .Fact("est_commission_let", () => Share(EstateCatalogue.CommissionLet))
                .Fact("est_hotel_years", () => (EstateCatalogue.HotelMultiple / 1000f).ToString("0.#", Inv))
                .Fact("est_hotel_occupancy", () => Share(EstateCatalogue.Occupancy))
                .Fact("est_lease_days", () => Whole(EstateCatalogue.LeaseDepositDays))
                .Fact("est_rate_low", () => Share(EstateCatalogue.RateLeast))
                .Fact("est_rate_high", () => Share(EstateCatalogue.RateMost))
                .Fact("est_inspection", () => Whole(EstateCatalogue.InspectionEvery))
                .Fact("est_inspection_pass", () => Share(EstateCatalogue.InspectionPass))
                .Fact("est_group_discount", () => Share(EstateCatalogue.GroupDiscount))
                .Fact("est_campaign_days", () => Whole(EstateCatalogue.CampaignDays))
                .Fact("est_hire_metres", () => Whole(EstateCatalogue.HireLotMetres))
                .Fact("est_hire_spaces", () => Whole(EstateCatalogue.HireSpacesAnyBuilding))
                .Fact("est_hire_cars", () => Whole(EstateCatalogue.HireCarsMost))
                .Fact("est_hire_days", () => Whole(EstateCatalogue.HireDaysMost))
                .Fact("est_hire_deposit", () => Whole(EstateCatalogue.HireDepositDays))
                .Fact("est_hire_damage", () => Share(EstateCatalogue.HireDamageChance))
                .Fact("est_hire_late", () => Share(EstateCatalogue.HireLateChance))
                .Fact("est_hire_stolen", () => Share(EstateCatalogue.HireStolenChance))
                .Fact("est_hire_cover", () => Share(EstateCatalogue.HireCoverShare))
                .Fact("est_hire_pays", () => Share(EstateCatalogue.HireCoverPays))
                .Fact("est_return_let", () => Bp(Let(1000)))
                .Fact("est_return_let_poor", () => Bp(Let(EstateCatalogue.IncomeLeast)))
                .Fact("est_return_let_rich", () => Bp(Let(EstateCatalogue.IncomeMost)))
                .Fact("est_return_hire", () => Bp(Hire()))
                .Fact("est_return_shop", () => Bp(EstateBalance.ShopYearlyBp(1)));
        }

        // A shop building let in a steady economy, over the seeds.
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

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }

        private static string Share(int thousandths)
        {
            return (thousandths / 10f).ToString("0.#", Inv) + "%";
        }

        private static string Bp(int hundredths)
        {
            return (hundredths / 100f).ToString("0.#", Inv) + "%";
        }
    }
}
