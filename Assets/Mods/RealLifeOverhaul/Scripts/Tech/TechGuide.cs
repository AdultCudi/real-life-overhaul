using System;
using System.Globalization;
using System.Text;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The figures the Guide's tech pages print, each {name} filled from the catalogues, worked out when its page opens.</summary>
    public static class TechGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The end game's scale the figures are given at: all of it with its option on, none off.
        private static int Scale => Settings.EndScale ? EndScaleRules.Full : 0;

        public static void AddPeopleFacts(Feature f)
        {
            f.Fact("talent_roles", Roles)
                .Fact("talent_levels", Levels)
                .Fact("talent_mentor", () => Whole(TalentCatalogue.MentorBonus / 10) + "%")
                .Fact("talent_mentees", () => Whole(TalentCatalogue.MentorsEach))
                .Fact("talent_long", () => Whole(TalentCatalogue.BurnoutLong))
                .Fact("talent_reviews", () => Whole(TalentCatalogue.ReviewsAYear))
                .Fact("talent_rating", () => Whole(TalentCatalogue.PromoteRating))
                .Fact("talent_promote_pay", () => Bp(TalentCatalogue.PromotePayBp))
                .Fact("talent_ask", () => Bp(TalentCatalogue.ReviewRiseBp))
                .Fact("talent_ask_top", () => Bp(TalentCatalogue.ReviewRiseBp * 2))
                .Fact("talent_ask_days", () => Whole(TalentCatalogue.AskDays))
                .Fact("talent_promote_days", () => Whole(TalentCatalogue.Scaled(TalentCatalogue.PromoteDaysInLevel, GameCalendar.DaysPerYear)))
                .Fact("talent_probation", () => Whole(TalentCatalogue.Scaled(TalentCatalogue.ProbationDays, GameCalendar.DaysPerYear)))
                .Fact("talent_probation_notice", () => Whole(TalentCatalogue.ProbationNotice))
                .Fact("talent_notice", () => Whole(TalentCatalogue.NoticePerYear))
                .Fact("talent_notice_most", () => Whole(TalentCatalogue.NoticeMost))
                .Fact("talent_severance", () => Whole(TalentCatalogue.SeveranceWeeksPerYear))
                .Fact("talent_severance_least", () => Whole(TalentCatalogue.SeveranceLeastWeeks))
                .Fact("talent_low_runs", () => Whole(TalentCatalogue.LowRatingsToGo))
                .Fact("talent_clause", () => Bp(TalentCatalogue.NonCompeteBp))
                .Fact("talent_clause_poach", () => Whole(100 - TalentCatalogue.NonCompetePoach / 10) + "%")
                .Fact("talent_poach_premium", () => Bp(TalentCatalogue.PoachPremiumBp))
                .Fact("talent_poach_days", () => Whole(TalentCatalogue.PoachAnswerDays))
                .Fact("talent_course_days", () => Whole(TalentCatalogue.CourseDays))
                .Fact("talent_course_gain", () => Whole(TalentCatalogue.CourseGain))
                .Fact("talent_exam_pass", () => Whole(TalentCatalogue.ExamPassBase) + "%")
                .Fact("talent_certs", Certs);
        }

        public static void AddSectorFacts(Feature f)
        {
            f.Fact("tech_segments", Segments)
                .Fact("tech_paying", () => Mille(FirmsCatalogue.ConsumerPaying))
                .Fact("tech_feature_base", () => Whole(FirmsCatalogue.FeatureBase))
                .Fact("tech_feature_step", () => Whole(FirmsCatalogue.FeatureStep))
                .Fact("tech_launch_features", () => Whole(FirmsCatalogue.LaunchFeatures))
                .Fact("tech_features_most", () => Whole(FirmsCatalogue.FeaturesMost))
                .Fact("tech_products_firm", () => Whole(FirmsCatalogue.ProductsPerFirm))
                .Fact("tech_products_most", () => Whole(FirmsCatalogue.ProductsMost))
                .Fact("tech_products_kept", () => Whole(FirmsCatalogue.WithdrawnKept))
                .Fact("tech_points", () => Whole(FirmsCatalogue.PointsPerDay))
                .Fact("tech_net_days", () => Whole(TechBridges.NetDaysNow()))
                .Fact("tech_tiers", () => Mille(FirmsCatalogue.PriceTiers[0]) + ", " + Mille(FirmsCatalogue.PriceTiers[1]) + " " + TechText.T("reallife_guide_tech_or") + " "
                                          + Mille(FirmsCatalogue.PriceTiers[2]))
                .Fact("tech_marketing_most", () => Guide.Money(Prices.At(FirmsCatalogue.MarketingDay[FirmsCatalogue.MarketingDay.Length - 1] / 100)))
                .Fact("tech_tickets", () => Whole(FirmsCatalogue.TicketsPerPerson))
                .Fact("tech_seat", () => Guide.Money(Prices.At(FirmsCatalogue.SoftwarePerSeat) / 100f))
                .Fact("tech_game_sizes", Games)
                .Fact("tech_store_cut", () => Mille(FirmsCatalogue.AppStoreCut))
                .Fact("tech_half_life", () => Whole(FirmsCatalogue.GameHalfLife))
                .Fact("tech_licence", () => Guide.Money(Prices.At(FirmsCatalogue.LicenceDeal / 100)))
                .Fact("tech_upkeep", () => Mille(FirmsCatalogue.LicenceMaintenance))
                .Fact("tech_point_rate", () => Guide.Money(Prices.At(FirmsCatalogue.PointRate / 100)))
                .Fact("tech_security_rate", () => Guide.Money(Prices.At(FirmsCatalogue.SecurityRate / 100)))
                .Fact("tech_managed", () => Guide.Money(Prices.At(FirmsCatalogue.ManagedMonth / 100)))
                .Fact("tech_upfront", () => Mille(FirmsCatalogue.UpfrontShare))
                .Fact("tech_late", () => Mille(FirmsCatalogue.LatePenalty))
                .Fact("tech_tender_every", () => Whole(FirmsCatalogue.TenderEvery))
                .Fact("tech_tender_days", () => Whole(FirmsCatalogue.TenderDays))
                .Fact("tech_council", () => Mille(FirmsCatalogue.CouncilShare - 1000))
                .Fact("tech_outage_churn", () => Mille(FirmsCatalogue.OutageChurn))
                .Fact("tech_sla", () => Mille(FirmsCatalogue.SlaCredit))
                .Fact("tech_fine_customer", () => Guide.Money(Prices.At(FirmsCatalogue.FinePerCustomer) / 100f))
                .Fact("tech_fine_least", () => Guide.Money(Prices.At(FirmsCatalogue.FineLeast / 100)))
                .Fact("tech_fine_year", () => Mille(FirmsCatalogue.FineShareOfYear))
                .Fact("tech_register_fee", () => Guide.Money(Prices.At(FirmsCatalogue.RegisterFee / 100)))
                .Fact("tech_unregistered", () => Mille(FirmsCatalogue.UnregisteredFine - 1000))
                .Fact("tech_relief", () => Mille(FirmsCatalogue.ReliefShare))
                .Fact("tech_scale_monthly", () => Guide.Money(Prices.At((int)(TechPath.ScaleUpMonthly / 100))))
                .Fact("tech_scale_people", () => Whole(TechPath.ScaleUpPeople))
                .Fact("infra_fit", () => Guide.Money(Prices.At((int)(InfraRules.FitCost(1, 0, Scale) / 100))) + " " + TechText.T("reallife_guide_tech_to") + " "
                                         + Guide.Money(Prices.At((int)(InfraRules.FitCost(1, 3, Scale) / 100))))
                .Fact("infra_sqm", () => Whole(InfraCatalogue.SqmPerRack))
                .Fact("infra_rack", () => Guide.Money(Prices.At((int)(InfraRules.RackPrice(false, Scale) / 100))))
                .Fact("infra_gpu", () => Guide.Money(Prices.At((int)(InfraRules.RackPrice(true, Scale) / 100))))
                .Fact("infra_kw", () => Whole(InfraCatalogue.RackKw))
                .Fact("infra_gpu_kw", () => Whole(InfraCatalogue.GpuRackKw))
                .Fact("infra_water", () => (InfraCatalogue.WaterPerMwh / 1000f).ToString("0.#", Inv))
                .Fact("infra_years", () => Whole(InfraCatalogue.WriteDownDays / 365))
                .Fact("infra_technician", () => Whole(InfraCatalogue.RacksPerTechnician))
                .Fact("infra_net", () => Whole(InfraCatalogue.NetNeeded))
                .Fact("infra_colo", () => Guide.Money(Prices.At(InfraCatalogue.ColoMonth / 100)))
                .Fact("infra_unit", () => Guide.Money(Prices.At(InfraCatalogue.UnitDay) / 100f))
                .Fact("infra_gpu_unit", () => Guide.Money(Prices.At(InfraCatalogue.GpuUnitDay) / 100f))
                .Fact("infra_own", () => Mille(InfraCatalogue.OwnHostingShare))
                .Fact("infra_licence", () => Guide.Money(Prices.At((int)(InfraRules.Licence(Scale) / 100))))
                .Fact("infra_cloud_racks", () => Whole(InfraRules.CloudRacksLeast(Scale)))
                .Fact("infra_cloud_engineers", () => Whole(InfraCatalogue.CloudEngineersLeast))
                .Fact("infra_cloud_cash", () => Guide.Money(Prices.At((int)(InfraRules.CloudCashLeast(Scale) / 100))))
                .Fact("infra_war", () => Mille(InfraCatalogue.PriceWarShare))
                .Fact("infra_cut", () => Mille(InfraCatalogue.PriceCut))
                .Fact("infra_floor", () => Mille(InfraCatalogue.PriceFloor))
                .Fact("infra_regions", () => Whole(InfraCatalogue.HyperRegions))
                .Fact("infra_hyper_racks", () => Whole(InfraRules.HyperRacks(Scale)))
                .Fact("infra_hyper_cash", () => Guide.Money(Prices.At(InfraRules.HyperCash(Scale) / 100f)))
                .Fact("infra_train", () => Whole(InfraCatalogue.TrainBase))
                .Fact("infra_rented", () => Mille(InfraCatalogue.RentedGpuShare));
        }

        private static string Segments()
        {
            var text = new StringBuilder();
            for (int s = 0; s < (int)Segment.Count; s++)
            {
                text.Append("- ").Append(TechText.Fill("reallife_guide_tech_segment", "segment", TechText.SegmentName((Segment)s),
                    "market", FirmsCatalogue.Market[s].ToString("N0", Inv), "price", Guide.Money(Prices.At(FirmsCatalogue.Price[s]) / 100f),
                    "churn", Mille(FirmsCatalogue.Churn[s]))).Append('\n');
            }
            return text.ToString();
        }

        private static string Games()
        {
            var text = new StringBuilder();
            for (int s = 0; s < 3; s++)
            {
                text.Append("- ").Append(TechText.Fill("reallife_guide_tech_game", "size", TechText.Scope(s), "points", Whole(FirmsCatalogue.GameScope[s]),
                    "price", Guide.Money(Prices.At(FirmsCatalogue.GamePrice[s]) / 100f))).Append('\n');
            }
            return text.ToString();
        }

        public static void AddVentureFacts(Feature f)
        {
            f.Fact("vc_firms", Firms)
                .Fact("vc_rounds", Rounds)
                .Fact("vc_most", () => Whole(VentureCatalogue.StartupsMost))
                .Fact("vc_raise_when", () => Whole(VentureCatalogue.RaiseWhenMonths))
                .Fact("vc_raised_months", () => Whole(VentureCatalogue.RunwayMonthsRaised))
                .Fact("vc_pool", () => Mille(VentureCatalogue.OptionPool))
                .Fact("vc_down", () => Mille(VentureCatalogue.DownRoundCut))
                .Fact("vc_failed", () => Whole(VentureCatalogue.FailedRaisesToDie))
                .Fact("vc_acquire_least", () => Whole(VentureCatalogue.AcquireMultipleLeast))
                .Fact("vc_acquire_most", () => Whole(VentureCatalogue.AcquireMultipleMost))
                .Fact("vc_listing_arr", () => Guide.Money(Prices.At((int)(VentureCatalogue.ListingArr / 100))))
                .Fact("vc_listing_multiple", () => Whole(VentureCatalogue.ListingMultiple))
                .Fact("vc_acquihire", () => Mille(VentureCatalogue.AcquihireShare))
                .Fact("vc_multiple_least", () => Whole(VentureCatalogue.MultipleLeast))
                .Fact("vc_multiple_most", () => Whole(VentureCatalogue.MultipleMost))
                .Fact("vc_ticket", () => Guide.Money(Prices.At((int)(VentureCatalogue.TicketLeast / 100))))
                .Fact("vc_ask_share", () => Mille(VentureCatalogue.AskPlayerShare))
                .Fact("vc_diligence", () => Guide.Money(Prices.At(VentureCatalogue.DiligenceFee / 100)))
                .Fact("vc_haggle", () => Mille(VentureCatalogue.HaggleCut))
                .Fact("vc_pro_rata_days", () => Whole(VentureCatalogue.ProRataDays))
                .Fact("vc_raise_days", () => Whole(VentureCatalogue.RaiseDays))
                .Fact("vc_meetings", () => Whole(VentureCatalogue.VcMeetingsMost))
                .Fact("vc_sheet_days", () => Whole(VentureCatalogue.TermSheetDays))
                .Fact("vc_target", () => Mille(VentureCatalogue.TargetGrowth))
                .Fact("vc_target_days", () => Whole(VentureCatalogue.TargetDays))
                .Fact("vc_misses", () => Whole(VentureCatalogue.MissesToReplace))
                .Fact("vc_bridge_months", () => Whole(VentureCatalogue.BridgeMonths))
                .Fact("vc_bridge_interest", () => Bp(VentureCatalogue.BridgeInterestBp))
                .Fact("vc_bridge_discount", () => Mille(VentureCatalogue.BridgeDiscount))
                .Fact("vc_grant", () => Mille(VentureCatalogue.OptionGrant))
                .Fact("vc_vest", () => Whole(VentureCatalogue.VestYears))
                .Fact("vc_cliff", () => Whole(VentureCatalogue.CliffYears))
                .Fact("vc_option_poach", () => Whole(100 - VentureCatalogue.OptionPoach / 10) + "%");
        }

        private static string Firms()
        {
            var text = new StringBuilder();
            for (int i = 0; i < VentureCatalogue.Firms.Length; i++)
            {
                VcDef vc = VentureCatalogue.Firms[i];
                text.Append("- ").Append(TechText.Fill(vc.Angels ? "reallife_guide_vc_angels" : "reallife_guide_vc_firm", "name", vc.Name,
                    "least", TechText.RoundName((RoundStage)vc.Least), "most", TechText.RoundName((RoundStage)vc.Most))).Append('\n');
            }
            return text.ToString();
        }

        private static string Rounds()
        {
            var text = new StringBuilder();
            for (int r = 1; r < (int)RoundStage.Count; r++)
            {
                text.Append("- ").Append(TechText.Fill("reallife_guide_vc_round", "round", TechText.RoundName((RoundStage)r),
                    "bar", Guide.Money(Prices.At((int)(VentureCatalogue.RoundBar[r] / 100))),
                    "floor", Guide.Money(Prices.At((int)Math.Min(int.MaxValue, VentureCatalogue.FloorValue[r] / 100))),
                    "burn", Guide.Money(Prices.At(VentureCatalogue.BurnByStage[r] / 100)))).Append('\n');
            }
            return text.ToString();
        }

        public static void AddHiringFacts(Feature f)
        {
            f.Fact("hire_seekers_least", () => Whole(HiringRules.Target(0, false)))
                .Fact("hire_seekers_most", () => Whole(HiringRules.Target(3, false)))
                .Fact("hire_graduates", () => Whole(HiringCatalogue.GraduatesSummer))
                .Fact("hire_unemployment", Unemployment)
                .Fact("hire_ad_day", () => Guide.Money(Prices.At(HiringCatalogue.AdDayCost)))
                .Fact("hire_ad_days", () => Whole(HiringCatalogue.AdDays[0]) + ", " + Whole(HiringCatalogue.AdDays[1]) + " " + TechText.T("reallife_guide_tech_or") + " "
                                            + Whole(HiringCatalogue.AdDays[2]))
                .Fact("hire_boost", () => (HiringCatalogue.BoostCostBp / 10000f).ToString("0.#", Inv))
                .Fact("hire_week", () => Whole(HiringCatalogue.AdWeekMost))
                .Fact("hire_week_boosted", () => Whole(HiringCatalogue.AdWeekMostBoosted))
                .Fact("hire_ads_most", () => Whole(HiringCatalogue.AdsMost))
                .Fact("hire_office_more", () => Whole(HiringCatalogue.OfficeBonusLeast))
                .Fact("hire_office_share", () => Whole(HiringCatalogue.OfficeBonusShare / 10) + "%")
                .Fact("hire_office_floor", () => Whole(HiringCatalogue.OfficeSkillFloor) + "%")
                .Fact("hire_refs", () => Guide.Money(Prices.At(HiringCatalogue.ReferencesCost)))
                .Fact("hire_background", () => Guide.Money(Prices.At(HiringCatalogue.BackgroundCost)))
                .Fact("hire_claim", () => Whole(HiringCatalogue.ClaimMost))
                .Fact("hire_rounds", () => Whole(HiringCatalogue.InterviewRounds))
                .Fact("hire_accept", () => Bp(HiringCatalogue.AcceptBp))
                .Fact("hire_counter", () => Bp(HiringCatalogue.CounterBp))
                .Fact("hire_rival_offer", () => Bp(HiringCatalogue.RivalOfferBp))
                .Fact("hire_offer_days", () => Whole(HiringCatalogue.OfferDays))
                .Fact("hire_lapse", () => Whole(HiringCatalogue.ApplicantDays))
                .Fact("hire_search_fee", () => Bp(HiringCatalogue.SearchFeeBp))
                .Fact("hire_search_retainer", () => Whole(HiringCatalogue.SearchRetainerShare / 10) + "%")
                .Fact("hire_search_count", () => Whole(HiringCatalogue.SearchCandidates))
                .Fact("hire_search_least", () => Whole(HiringCatalogue.SearchDaysLeast))
                .Fact("hire_search_most", () => Whole(HiringCatalogue.SearchDaysMost))
                .Fact("hire_search_level", () => TechText.Level(HiringCatalogue.SearchLeastLevel))
                .Fact("hire_rivals", () => Whole(HiringCatalogue.RivalAdsByCycle[1]));
        }

        private static string Unemployment()
        {
            return TechText.Fill("reallife_guide_tech_unemployment", "boom", Mille(HiringCatalogue.Unemployment[0]), "steady", Mille(HiringCatalogue.Unemployment[1]),
                "slowdown", Mille(HiringCatalogue.Unemployment[2]), "recession", Mille(HiringCatalogue.Unemployment[3]));
        }

        internal static string Mille(int thousandths) => (thousandths / 10f).ToString("0.#", Inv) + "%";

        private static string Roles()
        {
            var text = new StringBuilder();
            for (int i = 0; i < TalentCatalogue.Roles.Length; i++)
            {
                RoleDef r = TalentCatalogue.Roles[i];
                text.Append("- ").Append(TechText.Fill(r.InGame ? "reallife_guide_tech_role_game" : "reallife_guide_tech_role_payroll", "role", TechText.Role(r.Id),
                    "a", TechText.Skill(r.SkillA), "b", TechText.Skill(r.SkillB), "least", TechText.Level(r.LeastLevel), "most", TechText.Level(r.MostLevel),
                    "remote", TechText.T(r.Remote ? "reallife_guide_tech_role_remote" : "reallife_guide_tech_role_onsite"))).Append('\n');
            }
            return text.ToString();
        }

        private static string Levels()
        {
            var text = new StringBuilder();
            for (int l = 0; l < TalentCatalogue.Levels; l++)
            {
                text.Append("- ").Append(TechText.Fill("reallife_guide_tech_level", "level", TechText.Level(l), "least", Whole(TalentCatalogue.LevelSkillLeast[l]),
                    "most", Whole(TalentCatalogue.LevelSkillMost[l]), "years", Whole(TalentCatalogue.LevelYearsLeast[l]))).Append('\n');
            }
            return text.ToString();
        }

        private static string Certs()
        {
            var text = new StringBuilder();
            for (int c = 0; c < TalentCatalogue.CertCount; c++)
            {
                TalentCatalogue.CertDef cert = TalentCatalogue.Certs[c];
                text.Append("- ").Append(TechText.Fill("reallife_guide_tech_cert", "cert", TechText.Cert(c), "skill", TechText.Skill(cert.Skill), "needs", Whole(cert.Needs),
                    "fee", Guide.Money(Prices.At(cert.Fee)), "pay", Bp(cert.PayBp))).Append('\n');
            }
            return text.ToString();
        }

        internal static string Whole(int value) => value.ToString(Inv);

        internal static string Bp(int bp) => (bp / 100f).ToString("0.#", Inv) + "%";
    }
}
