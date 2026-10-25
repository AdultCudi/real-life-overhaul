using System.Globalization;
using System.Text;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The figures the Guide's careers pages print: every {name} filled from the catalogues, worked out as the page opens (Meta/FeatureList).</summary>
    public static class CareersGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void AddStudyFacts(Feature f)
        {
            f.Fact("study_school_courses", () => Courses(CourseKind.Short, CourseKind.Vocational))
                .Fact("study_early", () => Whole(StudyCatalogue.ArriveEarlyMinutes))
                .Fact("study_late", () => Whole(StudyCatalogue.LateMinutes))
                .Fact("study_counted", () => Mille(StudyCatalogue.CountedShare))
                .Fact("study_barred", () => Mille(StudyCatalogue.Barred))
                .Fact("study_homework_hours", () => Whole(StudyCatalogue.HomeworkHours))
                .Fact("study_exam_hours", () => Whole(StudyCatalogue.ExamHours))
                .Fact("study_exam_window", () => Whole(StudyCatalogue.ExamWindowDays))
                .Fact("study_pass", () => Whole(StudyCatalogue.Pass))
                .Fact("study_merit", () => Whole(StudyCatalogue.Merit))
                .Fact("study_distinction", () => Whole(StudyCatalogue.Distinction))
                .Fact("study_retakes", () => Whole(StudyCatalogue.RetakesMost))
                .Fact("study_retake_days", () => Whole(StudyCatalogue.RetakeDays))
                .Fact("study_retake_fee", () => Mille(StudyCatalogue.RetakeFeeShare))
                .Fact("study_fade_days", () => Whole(StudyCatalogue.FadeAfterDays))
                .Fact("study_fade_floor", () => Mille(StudyCatalogue.FadeFloor))
                .Fact("study_stressed", () => Whole(StudyCatalogue.StressedWeekHours))
                .Fact("study_refresher", () => Whole(StudyCatalogue.RefresherPoints))
                .Fact("study_at_once", () => Whole(StudyRules.LiveMost))
                .Fact("study_tired", () => Whole(StudyCatalogue.TiredEnergy))
                .Fact("study_skills", () => Whole(Skills.Count))
                .Fact("study_exam_open", () => CareersText.Hour(StudyCatalogue.ExamOpens))
                .Fact("study_exam_close", () => CareersText.Hour(StudyCatalogue.ExamCloses));
        }

        public static void AddDegreeFacts(Feature f)
        {
            f.Fact("degrees_address", CareersGame.HomeAddress)
                .Fact("degrees_list", () => Courses(CourseKind.Degree))
                .Fact("degrees_after", () => Courses(CourseKind.Postgrad))
                .Fact("degrees_certs", () => Courses(CourseKind.Certificate))
                .Fact("degrees_pass", () => Whole(StudyCatalogue.DegreePass))
                .Fact("degrees_lower", () => Whole(StudyCatalogue.LowerSecond))
                .Fact("degrees_upper", () => Whole(StudyCatalogue.UpperSecond))
                .Fact("degrees_first", () => Whole(StudyCatalogue.First))
                .Fact("degrees_parts", () => Whole(StudyCatalogue.InstalmentsHigher))
                .Fact("degrees_merit", () => Mille(StudyCatalogue.MeritGrantShare))
                .Fact("degrees_need", () => Mille(StudyCatalogue.NeedGrantShare))
                .Fact("degrees_need_cash", () => Guide.Money(Prices.At(StudyCatalogue.NeedGrantCash)))
                .Fact("degrees_council", () => Mille(StudyCatalogue.CouncilShare))
                .Fact("degrees_grant_most", () => Mille(StudyCatalogue.GrantMost))
                .Fact("degrees_loan_rate", () => Bp(StudyCatalogue.LoanRateBp))
                .Fact("degrees_loan_share", () => Bp(StudyCatalogue.LoanShareBp))
                .Fact("degrees_loan_threshold", () => Guide.Money(Prices.At(StudyCatalogue.LoanThreshold) / 100f))
                .Fact("degrees_loan_most", () => Guide.Money(Prices.At(StudyCatalogue.LoanMost)))
                .Fact("degrees_part_time", () => CareersText.Timetable(StudyCatalogue.PartTimeDaysUniversity, StudyCatalogue.PartTimeHourUniversity))
                .Fact("degrees_ceremony", () => CareersText.Hour(CareersDirector.CeremonyHour))
                .Fact("degrees_window", () => Whole(StudyCatalogue.ExamWindowDays * 2));
        }

        public static void AddFamilyFacts(Feature f)
        {
            f.Fact("family_tutor_least", () => Whole(StudyCatalogue.TutorLeast))
                .Fact("family_tutor_rate", () => Guide.Money(Prices.At(StudyRules.TutorRate(StudyCatalogue.TutorLeast)) / 100f))
                .Fact("family_tutor_top", () => Guide.Money(Prices.At(StudyRules.TutorRate(100)) / 100f))
                .Fact("family_tutor_hours", () => Whole(StudyCatalogue.TutorMinutes / 60))
                .Fact("family_tutor_per_day", () => Whole(StudyCatalogue.TutorPerDay))
                .Fact("family_tutor_from", () => CareersText.Hour(StudyCatalogue.TutorFrom))
                .Fact("family_tutor_to", () => CareersText.Hour(StudyCatalogue.TutorTo))
                .Fact("family_academy_staff", () => Whole(StudyCatalogue.AcademyPerStaff))
                .Fact("family_academy_demand", () => Whole(StudyCatalogue.AcademyDemand))
                .Fact("family_academy_fee", () => Guide.Money(Prices.At(StudyCatalogue.AcademyFeeCents) / 100f))
                .Fact("family_academy_courses", () => Bp(StudyCatalogue.AcademyStaffCourseBp))
                .Fact("family_degree_days", () => Whole(TalentCatalogue.Scaled(TalentCatalogue.DegreeDays, GameCalendar.DaysPerYear)))
                .Fact("family_degree_gain", () => Whole(TalentCatalogue.DegreeGain));
        }

        public static void AddWorkFacts(Feature f)
        {
            f.Fact("work_professions", Professions)
                .Fact("work_self_taught", () => Whole(ProRules.SelfTaughtExtra))
                .Fact("work_live", () => Whole(CareersDirector.ApplicationsLive))
                .Fact("work_screen", () => Whole(ProRules.ScreenPass))
                .Fact("work_offer", () => Whole(ProRules.OfferPass))
                .Fact("work_offer_days", () => Whole(ProRules.OfferDays))
                .Fact("work_answer_least", () => Whole(ProRules.AnswerDaysLeast))
                .Fact("work_answer_most", () => Whole(ProRules.AnswerDaysMost))
                .Fact("work_interview_after", () => Whole(ProRules.InterviewAfterDays))
                .Fact("work_start_days", () => Whole(ProRules.StartDays))
                .Fact("work_ask_small", () => Bp(ProRules.AskSmallBp))
                .Fact("work_ask_large", () => Bp(ProRules.AskLargeBp))
                .Fact("work_asks", () => Whole(ProRules.AsksMost))
                .Fact("work_early", () => Whole(ProRules.ClockEarlyMinutes))
                .Fact("work_late", () => Whole(ProRules.ClockLateMinutes))
                .Fact("work_late_after", () => Whole(ProRules.LateAfterMinutes))
                .Fact("work_counted", () => Mille(ProRules.CountedShare))
                .Fact("work_warnings", () => Whole(CareersDirector.WarningsToGo))
                .Fact("work_agency_days", () => Whole(ProRules.AgencyDays))
                .Fact("work_agency_more", () => Whole(ProRules.OpeningsAgency))
                .Fact("work_reach", () => Whole(ProRules.OpeningsWithinReach))
                .Fact("work_friends", () => Whole(ProRules.FriendsForReferral))
                .Fact("work_tech_pay", () => Mille(ProRules.KindFactor(EmployerKind.TechFirm) - 1000))
                .Fact("work_rival_pay", () => Mille(ProRules.KindFactor(EmployerKind.Rival) - 1000))
                .Fact("work_public_pay", () => Mille(1000 - ProRules.KindFactor(EmployerKind.Public)))
                .Fact("work_loan_share", () => Bp(StudyCatalogue.LoanShareBp))
                .Fact("work_loan_threshold", () => Guide.Money(Prices.At(StudyCatalogue.LoanThreshold) / 100f));
        }

        public static void AddLifeFacts(Feature f)
        {
            f.Fact("life_review_days", () => Whole(LifeRules.ReviewEvery(GameCalendar.DaysPerYear)))
                .Fact("life_rise", () => Bp(LifeRules.RiseBp))
                .Fact("life_rise_top", () => Bp(LifeRules.RiseBp * 2))
                .Fact("life_rise_half", () => Bp(LifeRules.RiseBp / 2))
                .Fact("life_rises", () => Whole(LifeRules.RaisesMost))
                .Fact("life_bonus_4", () => Whole(LifeRules.BonusDays4))
                .Fact("life_bonus_5", () => Whole(LifeRules.BonusDays5))
                .Fact("life_promote", () => Bp(LifeRules.PromoteBp))
                .Fact("life_low_runs", () => Whole(LifeRules.LowRunsToGo))
                .Fact("life_ask", () => Bp(LifeRules.AskRiseBp))
                .Fact("life_ask_days", () => Whole(LifeRules.AskGapDays))
                .Fact("life_overtime", () => Whole(LifeRules.OvertimeMinutes / 60))
                .Fact("life_long_week", () => Whole(LifeRules.LongWeekHours))
                .Fact("life_matter_days", () => Whole(LifeRules.MatterDays))
                .Fact("life_poach_premium", () => Bp(LifeRules.PoachPremiumBp))
                .Fact("life_severance", () => Whole(LifeRules.SeveranceWeeksPerYear))
                .Fact("life_severance_least", () => Whole(LifeRules.SeveranceLeastWeeks))
                .Fact("life_benefit_weeks", () => Whole(LifeRules.BenefitWeeks))
                .Fact("life_benefit_share", () => Mille(LifeRules.BenefitShare))
                .Fact("life_benefit_most", () => Guide.Money(Prices.At(LifeRules.BenefitMost)))
                .Fact("life_options", () => Whole(LifeRules.OptionsPerRank))
                .Fact("life_vest_days", () => Whole(TalentCatalogue.Scaled(LifeRules.VestQuarterDays, GameCalendar.DaysPerYear)))
                .Fact("life_pension_own", () => Bp(LifeRules.PensionOwnBp))
                .Fact("life_pension_employer", () => Bp(LifeRules.PensionEmployerBp))
                .Fact("life_pension_public", () => Bp(LifeRules.PensionPublicBp))
                .Fact("life_pension_growth", () => Bp(LifeRules.PensionGrowthBp))
                .Fact("life_retire", () => Whole(LifeRules.RetireAge))
                .Fact("life_retire_early", () => Whole(LifeRules.RetireEarlyAge))
                .Fact("life_retire_cut", () => Bp(LifeRules.EarlyCutBp))
                .Fact("life_pension_years", () => Whole(LifeRules.PensionYears))
                .Fact("life_share_chief", () => Bp(LifeRules.ProfitShareChiefBp))
                .Fact("life_share_exec", () => Bp(LifeRules.ProfitShareExecBp));
        }

        public static void AddLadderFacts(Feature f)
        {
            f.Fact("ladder_days_1", () => Whole(LifeRules.Paced(LadderRanks.Of((int)Rung.Supervisor).Days, GameCalendar.DaysPerYear)))
                .Fact("ladder_days_2", () => Whole(LifeRules.Paced(LadderRanks.Of((int)Rung.StoreManager).Days, GameCalendar.DaysPerYear)))
                .Fact("ladder_days_3", () => Whole(LifeRules.Paced(LadderRanks.Of((int)Rung.Regional).Days, GameCalendar.DaysPerYear)))
                .Fact("ladder_days_4", () => Whole(LifeRules.Paced(LadderRanks.Of((int)Rung.Executive).Days, GameCalendar.DaysPerYear)))
                .Fact("ladder_score_1", () => Whole(LadderRanks.Of((int)Rung.Supervisor).Score))
                .Fact("ladder_score_2", () => Whole(LadderRanks.Of((int)Rung.StoreManager).Score))
                .Fact("ladder_score_3", () => Whole(LadderRanks.Of((int)Rung.Regional).Score))
                .Fact("ladder_score_4", () => Whole(LadderRanks.Of((int)Rung.Executive).Score))
                .Fact("ladder_need_1", () => Whole(LadderRanks.Of((int)Rung.Supervisor).Need))
                .Fact("ladder_need_2", () => Whole(LadderRanks.Of((int)Rung.StoreManager).Need))
                .Fact("ladder_need_3", () => Whole(LadderRanks.Of((int)Rung.Regional).Need))
                .Fact("ladder_need_4", () => Whole(LadderRanks.Of((int)Rung.Executive).Need))
                .Fact("ladder_pay_1", () => Guide.Money(Prices.At(ManagerPay((int)Rung.Supervisor)) / 100f))
                .Fact("ladder_pay_2", () => Guide.Money(Prices.At(ManagerPay((int)Rung.StoreManager)) / 100f))
                .Fact("ladder_pay_3", () => Guide.Money(Prices.At(ManagerPay((int)Rung.Regional)) / 100f))
                .Fact("ladder_pay_4", () => Guide.Money(Prices.At(ManagerPay((int)Rung.Executive)) / 100f))
                .Fact("ladder_task_days", () => Whole(CareerLadderRules.TaskDays))
                .Fact("ladder_bonus", () => Whole(CareerLadderRules.ReviewBonusMost))
                .Fact("ladder_stores", () => Whole(CareerLadderRules.StoresMost))
                .Fact("ladder_raise_days", () => Whole(CareerLadderRules.RaiseGapDays));
        }

        // A rung's pay from the management profession at one of the city's own businesses, cents an hour at the start's prices.
        private static int ManagerPay(int rung) => ProRules.PostPay(ProCatalogue.Find(ProCatalogue.Manager), LadderRanks.Of(rung).PayRank, EmployerKind.City, 1000);

        public static void AddOwnFacts(Feature f)
        {
            f.Fact("own_colleagues", () => Whole(OwnRules.ColleaguesMost))
                .Fact("own_colleagues_days", () => Whole(OwnRules.ColleaguesDays))
                .Fact("own_founder_most", () => Mille(OwnRules.FounderMost))
                .Fact("own_restraint_days", () => Whole(TalentCatalogue.Scaled(OwnRules.RestraintDays, GameCalendar.DaysPerYear)))
                .Fact("own_claim_weeks", () => Whole(OwnRules.ClaimWeeks))
                .Fact("own_claim_colleague", () => Whole(OwnRules.ClaimWeeksPerColleague))
                .Fact("own_claim_colleagues", () => Mille(OwnRules.ClaimColleagues))
                .Fact("own_claim_field", () => Mille(OwnRules.ClaimSameField))
                .Fact("own_freelance", () => Bp(OwnRules.FreelanceBp))
                .Fact("own_practice", () => Bp(OwnRules.PracticeBp))
                .Fact("own_office", () => Bp(OwnRules.OfficeBp))
                .Fact("own_late", () => Bp(OwnRules.LateCutBp))
                .Fact("own_side", () => Mille(OwnRules.SideWorkFound))
                .Fact("own_contracts", () => Whole(OwnRules.ContractsMost))
                .Fact("own_offer_days", () => Whole(OwnRules.OfferDays))
                .Fact("own_register_law", () => Guide.Money(Prices.At(OwnRules.RegisterFee(ProCatalogue.Find(ProCatalogue.Lawyer)))))
                .Fact("own_register_least", () => Guide.Money(Prices.At(OwnRules.RegisterFee(ProCatalogue.Find(ProCatalogue.Designer)))));
        }

        /// <summary>Every profession with its ladder and what its first and top rungs pay now, one bullet each.</summary>
        private static string Professions()
        {
            var b = new StringBuilder(2048);
            for (int i = 0; i < ProCatalogue.All.Length; i++)
            {
                Profession p = ProCatalogue.All[i];
                b.Append(b.Length > 0 ? "\n- " : "- ").Append(CareersText.ProName(p.Id)).Append(": ").Append(CareersText.Title(p.Id, 0)).Append(" (")
                    .Append(Guide.Money(Prices.At(p.Ranks[0].Pay) / 100f)).Append(") ").Append(CareersText.T("reallife_guide_work_to")).Append(' ')
                    .Append(CareersText.Title(p.Id, p.Ranks.Length - 1)).Append(" (").Append(Guide.Money(Prices.At(p.Ranks[p.Ranks.Length - 1].Pay) / 100f)).Append(')');
            }
            return b.ToString();
        }

        private static string Bp(int bp) => (bp / 100f).ToString("0.#", Inv) + "%";

        /// <summary>The courses of the kinds given, each with its fee at today's prices: "First aid (120), Barista (150)".</summary>
        private static string Courses(params CourseKind[] kinds)
        {
            var b = new StringBuilder(512);
            for (int i = 0; i < StudyCatalogue.All.Length; i++)
            {
                CourseDef c = StudyCatalogue.All[i];
                if (System.Array.IndexOf(kinds, c.Kind) < 0)
                {
                    continue;
                }
                if (b.Length > 0)
                {
                    b.Append(", ");
                }
                b.Append(CareersText.Course(c.Id)).Append(" (").Append(Guide.Money(Prices.At(c.Fee))).Append(')');
            }
            return b.ToString();
        }

        private static string Whole(long n) => n.ToString("N0", Inv);

        private static string Mille(int thousandths) => (thousandths / 10f).ToString("0.#", Inv) + "%";
    }
}
