using System.Globalization;
using System.Text;
using Extensions;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The words of education and careers: every figure they show is written here, the same way in the apps, the
    /// notifications, the Planner and the Guide. Every text is a key of en.json; money as the game writes it. Called when
    /// something is shown, never otherwise.
    /// </summary>
    public static class CareersText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string Mod = "reallife";   // (keys made of parts are put together from this, so the text check reads only whole keys)
        private static readonly StringBuilder B = new StringBuilder(64);

        public static string T(string key) => FinanceText.T(key);

        public static string T(string key, string name, string value) => FinanceText.T(key, name, value);

        public static string T(string key, string name, string value, string name2, string value2) => FinanceText.T(key, name, value, name2, value2);

        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs);

        public static string Pair(string labelKey, string value) => FinanceText.Pair(labelKey, value);

        /// <summary>Whole money, as the Finance app writes it.</summary>
        public static string Money(long whole) => FinanceText.Money(whole);

        /// <summary>A sum kept in cents, in whole units.</summary>
        public static string Cents(long cents) => FinanceText.Money(cents / 100f);

        /// <summary>A rate kept in cents, with its cents: "$34.50".</summary>
        public static string Rate(int cents) => (cents / 100f).ToCurrencyFormat();

        public static string Percent(int whole) => whole.ToString(Inv) + "%";

        /// <summary>Thousandths as a per cent: "65%".</summary>
        public static string Mille(int thousandths) => (thousandths / 10f).ToString("0", Inv) + "%";

        public static string Whole(long n) => n.ToString("N0", Inv);

        public static string Day(int day) => FinanceText.Day(day);

        // ---------------------------------------------------------------- studying

        public static string Course(int id)
        {
            CourseDef c = StudyCatalogue.Find(id);
            return c != null ? T(Mod + "_study_course_" + c.Key) : "?";
        }

        public static string About(CourseDef c) => c != null ? T(Mod + "_study_about_" + c.Key) : string.Empty;

        public static string Kind(CourseKind kind) => T(Mod + "_study_kind_" + kind.ToString().ToLowerInvariant());

        public static string Campus(Campus campus) => T(Mod + "_study_campus_" + campus.ToString().ToLowerInvariant());

        public static string Mode(StudyMode mode) => T(Mod + "_study_mode_" + mode.ToString().ToLowerInvariant());

        public static string Mode(int mode) => Mode((StudyMode)TalentCatalogue.Clamp(mode, 0, 2));

        public static string Skill(int skill) => T(StudyCatalogue.SkillKey(skill));

        /// <summary>A grade in words: a course's fail, pass, merit, distinction; a degree's fail, third, lower second, upper second, first.</summary>
        public static string Grade(CourseDef c, int grade)
        {
            grade = TalentCatalogue.Clamp(grade, 0, c != null && c.Higher ? 4 : 3);
            return T((c != null && c.Higher ? Mod + "_study_class_" : Mod + "_study_grade_") + grade.ToString(Inv));
        }

        public static string Stage(EnrolStage stage) => T(Mod + "_study_stage_" + stage.ToString().ToLowerInvariant());

        /// <summary>A timetable: its days, short ("Mon, Wed, Fri"), and the hour: "Mon, Wed, Fri at 10:00".</summary>
        public static string Timetable(int days, int hour)
        {
            if (days == 0)
            {
                return T("reallife_study_timetable_none");
            }
            return T("reallife_study_timetable", "days", DayList(days), "hour", Hour(hour));
        }

        public static string DayList(int days)
        {
            B.Length = 0;
            for (int i = 0; i < 7; i++)
            {
                if ((days & (1 << i)) != 0)
                {
                    if (B.Length > 0)
                    {
                        B.Append(", ");
                    }
                    B.Append(T(Mod + "_study_day_" + i.ToString(Inv)));
                }
            }
            return B.ToString();
        }

        /// <summary>An hour as the game writes the time of day.</summary>
        public static string Hour(int hour) => (hour % 24).GetFormattedTime(0f);

        /// <summary>A length of game time in hours and minutes.</summary>
        public static string Minutes(int minutes) => T("reallife_study_hours_minutes", "hours", (minutes / 60).ToString(Inv), "minutes", (minutes % 60).ToString("00", Inv));

        /// <summary>Points of a skill kept in thousandths, whole: "42".</summary>
        public static string Points(int thousandths) => (thousandths / 1000).ToString(Inv);

        /// <summary>Whole weeks of a number of days, at least one: "8 weeks", "1 week".</summary>
        public static string Weeks(int days)
        {
            int weeks = (days + 6) / 7;
            return weeks <= 1 ? T("reallife_study_weeks_one") : T("reallife_study_weeks", "weeks", weeks.ToString(Inv));
        }
    }
}
