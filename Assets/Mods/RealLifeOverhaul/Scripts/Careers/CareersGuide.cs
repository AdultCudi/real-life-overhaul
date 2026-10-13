using System.Globalization;
using System.Text;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The figures the Guide's pages on education and careers print: every {name} in their text is filled from the
    /// catalogues here, so a page can never say anything the rules do not do. Called once, when the feature list is built
    /// (Meta/FeatureList); each figure is worked out when the page is opened.
    /// </summary>
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

        public static void AddWorkFacts(Feature f)
        {
            f.Fact("work_professions", Professions)
                .Fact("work_self_taught", () => Whole(ProRules.SelfTaughtExtra))
                .Fact("work_live", () => Whole(CareersDirector.ApplicationsLive))
                .Fact("work_screen", () => Whole(ProRules.ScreenPass))
                .Fact("work_offer", () => Whole(ProRules.OfferPass))
                .Fact("work_offer_days", () => Whole(ProRules.OfferDays))
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
                .Fact("work_friends", () => Whole(ProRules.FriendsForReferral))
                .Fact("work_tech_pay", () => Mille(ProRules.KindFactor(EmployerKind.TechFirm) - 1000))
                .Fact("work_rival_pay", () => Mille(ProRules.KindFactor(EmployerKind.Rival) - 1000))
                .Fact("work_public_pay", () => Mille(1000 - ProRules.KindFactor(EmployerKind.Public)))
                .Fact("work_loan_share", () => Bp(StudyCatalogue.LoanShareBp))
                .Fact("work_loan_threshold", () => Guide.Money(Prices.At(StudyCatalogue.LoanThreshold) / 100f));
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

        // ---------------------------------------------------------------- shared

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
