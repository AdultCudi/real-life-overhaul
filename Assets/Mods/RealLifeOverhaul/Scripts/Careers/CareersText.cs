using System.Globalization;
using System.Text;
using Extensions;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The words of education and careers, the same in the apps, notifications, Planner and Guide; called only when something is shown.</summary>
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

        /// <summary>An employer's name as kept: a name of the city's, or "@" and a locale key for one of the mod's (the public sector), which follows the language.</summary>
        public static string Employer(string kept)
        {
            if (string.IsNullOrEmpty(kept))
            {
                return "?";
            }
            return kept[0] == '@' ? T(kept.Substring(1)) : kept;
        }

        /// <summary>A name kept as a locale key (see <see cref="Employer"/>).</summary>
        public static string KeyName(string key) => "@" + key;

        public static string ProName(int prof)
        {
            Profession p = ProCatalogue.Find(prof);
            return p != null ? T(Mod + "_pro_name_" + p.Key) : "?";
        }

        /// <summary>A rank's title: reallife_pro_rank_{profession}_{n}.</summary>
        public static string Title(int prof, int rank)
        {
            Profession p = ProCatalogue.Find(prof);
            if (p == null)
            {
                return "?";
            }
            rank = rank < 0 ? 0 : rank >= p.Ranks.Length ? p.Ranks.Length - 1 : rank;
            return T(Mod + "_pro_rank_" + p.Key + "_" + rank.ToString(Inv));
        }

        private static readonly int[] RunFrom = new int[7], RunTo = new int[7];

        /// <summary>A week's days, with a run of three or more written as one: "Mon to Fri", "Mon, Wed, Fri", "Mon to Thu, Sat".</summary>
        public static string DaySpan(int days)
        {
            int runs = GameUi.LookRules.DayRuns(days, RunFrom, RunTo);
            if (runs == 0)
            {
                return "-";
            }
            var b = new StringBuilder(48);
            for (int i = 0; i < runs; i++)
            {
                if (i > 0)
                {
                    b.Append(", ");
                }
                string from = T(Mod + "_study_day_" + RunFrom[i].ToString(Inv));
                b.Append(RunFrom[i] == RunTo[i] ? from : T("reallife_career_row_hours", "from", from, "to", T(Mod + "_study_day_" + RunTo[i].ToString(Inv))));
            }
            return b.ToString();
        }

        /// <summary>A profession's working week, its days run together: "Mon to Fri, 09:00 to 17:00" (the box's rows).</summary>
        public static string Week(Profession p)
        {
            return Fill("reallife_work_hours", "days", DaySpan(p.Days), "from", Hour(p.Hour), "to", Hour((p.Hour + p.Hours) % 24));
        }

        /// <summary>A profession's working week: "Mon, Tue, Wed, Thu, Fri, 09:00 to 17:00".</summary>
        public static string Hours(Profession p)
        {
            return Fill("reallife_work_hours", "days", DayList(p.Days), "from", Hour(p.Hour), "to", Hour((p.Hour + p.Hours) % 24));
        }

        public static string WorkWay(int mode) => T(Mod + "_work_mode_" + ((WorkMode)(mode < 0 ? 0 : mode > 2 ? 2 : mode)).ToString().ToLowerInvariant());

        public static string Kind(EmployerKind kind) => T(Mod + "_work_kind_" + kind.ToString().ToLowerInvariant());

        public static string ViaName(int via) => T(Mod + "_work_via_" + ((Via)(via < 0 ? 0 : via > 4 ? 4 : via)).ToString().ToLowerInvariant());

        public static string Step(ApplyStep step) => T(Mod + "_work_step_" + step.ToString().ToLowerInvariant());

        /// <summary>A reference: none, poor, fair, good.</summary>
        public static string Reference(int reference) => T(Mod + "_work_reference_" + (reference < 0 ? 0 : reference > 3 ? 3 : reference).ToString(Inv));

        public static string Leaving(int why) => T(Mod + "_work_leaving_" + ((LeaveReason)(why < 0 ? 0 : why > 8 ? 8 : why)).ToString().ToLowerInvariant());

        /// <summary>The key of an office matter's words (reallife_life_matter_{key}), or of one of its answers (with "_a" or "_b").</summary>
        public static string MatterKey(Matter m, string answer = "") => Mod + "_life_matter_" + m.Key + answer;

        public static string ContractState(ContractStep step) => T(Mod + "_own_step_" + step.ToString().ToLowerInvariant());

        public static string Boss(BossKind kind) => T(Mod + "_life_boss_" + kind.ToString().ToLowerInvariant());

        /// <summary>Whole weeks of a number of days, at least one: "8 weeks", "1 week".</summary>
        public static string Weeks(int days)
        {
            int weeks = (days + 6) / 7;
            return weeks <= 1 ? T("reallife_study_weeks_one") : T("reallife_study_weeks", "weeks", weeks.ToString(Inv));
        }
    }
}
