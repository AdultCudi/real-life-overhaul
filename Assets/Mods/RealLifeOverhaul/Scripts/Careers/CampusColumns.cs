using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Campus app's tables, declared as data (GameUi.TableSpec): for each tab its columns, how each sorts, what its rows
    /// can be filtered by and the sentence shown when a filter leaves nothing. The numbers beside each are the columns, for
    /// whoever writes the rows.
    /// </summary>
    public static class CampusColumns
    {
        private static readonly string[] Kinds =
        {
            "reallife_study_kind_short", "reallife_study_kind_vocational", "reallife_study_kind_degree", "reallife_study_kind_postgrad", "reallife_study_kind_certificate"
        };

        public const int Open = 0, Taken = 1, Locked = 2;
        private static readonly string[] States = { "reallife_campus_kind_open", "reallife_campus_kind_taken", "reallife_campus_kind_locked" };

        public static readonly TableSpec Off = new TableSpec("Campus", "Off").Column("reallife_campus_col_course", 600f, TableSort.None, flexible: true);

        /// <summary>Courses: 0 course, 1 kind, 2 where, 3 classes, 4 timetable, 5 length, 6 fee, 7 teaches, 8 state.</summary>
        public static readonly TableSpec Courses = new TableSpec("Campus", "Courses")
            .Column("reallife_campus_col_course", 330f, TableSort.Text, flexible: true)
            .Column("reallife_campus_col_kind", 220f, TableSort.Text)
            .Column("reallife_campus_col_where", 300f, TableSort.Text)
            .Column("reallife_campus_col_classes", 150f, TableSort.Number)
            .Column("reallife_campus_col_timetable", 330f, TableSort.Text)
            .Column("reallife_campus_col_length", 170f, TableSort.Number)
            .Column("reallife_campus_col_fee", 190f, TableSort.Money)
            .Column("reallife_campus_col_teaches", 330f, TableSort.Text)
            .Column("reallife_campus_col_state", 220f, TableSort.Text, right: true)
            .Filter("reallife_campus_all_kinds", Kinds)
            .Filter("reallife_campus_all_states", States)
            .Empty("reallife_campus_empty_courses");

        /// <summary>Studying: 0 course, 1 mode, 2 next class, 3 attended, 4 homework, 5 paid, 6 exam, 7 stage.</summary>
        public static readonly TableSpec Studying = new TableSpec("Campus", "Studying")
            .Column("reallife_campus_col_course", 330f, TableSort.Text, flexible: true)
            .Column("reallife_campus_col_mode", 200f, TableSort.Text)
            .Column("reallife_campus_col_next", 300f, TableSort.Number)
            .Column("reallife_campus_col_attended", 200f, TableSort.Number)
            .Column("reallife_campus_col_homework", 200f, TableSort.Number)
            .Column("reallife_campus_col_paid", 190f, TableSort.Money, sums: true)
            .Column("reallife_campus_col_exam", 260f, TableSort.Number)
            .Column("reallife_campus_col_state", 240f, TableSort.Text, right: true)
            .Empty("reallife_campus_empty_studying");

        public const int ResultPassed = 0, ResultFailed = 1, ResultLeft = 2;
        private static readonly string[] ResultKinds = { "reallife_campus_kind_passed", "reallife_campus_kind_failed", "reallife_campus_kind_left" };

        /// <summary>Results: 0 course, 1 kind, 2 grade, 3 mark, 4 attended, 5 retakes, 6 ended, 7 paid, 8 outcome.</summary>
        public static readonly TableSpec Results = new TableSpec("Campus", "Results")
            .Column("reallife_campus_col_course", 330f, TableSort.Text, flexible: true)
            .Column("reallife_campus_col_kind", 220f, TableSort.Text)
            .Column("reallife_campus_col_grade", 260f, TableSort.Number)
            .Column("reallife_campus_col_mark", 140f, TableSort.Number)
            .Column("reallife_campus_col_attended", 200f, TableSort.Number)
            .Column("reallife_campus_col_retakes", 150f, TableSort.Number)
            .Column("reallife_campus_col_ended", 200f, TableSort.Number)
            .Column("reallife_campus_col_paid", 190f, TableSort.Money, sums: true)
            .Column("reallife_campus_col_state", 220f, TableSort.Text, right: true)
            .Filter("reallife_campus_all_results", ResultKinds)
            .Empty("reallife_campus_empty_results");

        /// <summary>Family: 0 child, 1 age, 2 schooling, 3 college fund, 4 university, 5 state.</summary>
        public static readonly TableSpec Family = new TableSpec("Campus", "Family")
            .Column("reallife_campus_col_child", 300f, TableSort.Text, flexible: true)
            .Column("reallife_campus_col_age", 140f, TableSort.Number)
            .Column("reallife_campus_col_schooling", 240f, TableSort.Text)
            .Column("reallife_campus_col_fund", 200f, TableSort.Money)
            .Column("reallife_campus_col_university", 380f, TableSort.Text)
            .Column("reallife_campus_col_state", 240f, TableSort.Text, right: true)
            .Empty("reallife_campus_empty_family");

        public const int SkillProfession = 0, SkillTrade = 1;
        private static readonly string[] SkillKinds = { "reallife_campus_kind_professional", "reallife_campus_kind_trade" };

        /// <summary>Skills: 0 skill, 1 points, 2 best, 3 last used, 4 state.</summary>
        public static readonly TableSpec Skills = new TableSpec("Campus", "Skills")
            .Column("reallife_campus_col_skill", 420f, TableSort.Text, flexible: true)
            .Column("reallife_campus_col_points", 180f, TableSort.Number)
            .Column("reallife_campus_col_best", 180f, TableSort.Number)
            .Column("reallife_campus_col_used", 220f, TableSort.Number)
            .Column("reallife_campus_col_state", 260f, TableSort.Text, right: true)
            .Filter("reallife_campus_all_skills", SkillKinds)
            .Order(1, false)
            .Empty("reallife_campus_empty_skills");
    }
}
