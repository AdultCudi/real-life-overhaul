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
