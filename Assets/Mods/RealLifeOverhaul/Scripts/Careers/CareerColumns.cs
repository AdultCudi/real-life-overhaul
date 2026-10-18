using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The Career app's tables as data (GameUi.TableSpec): each tab's columns, sorts, filters and empty sentence.</summary>
    public static class CareerColumns
    {
        public const int Open = 0, Short = 1, Applied = 2;
        private static readonly string[] States = { "reallife_career_kind_open", "reallife_career_kind_short", "reallife_career_kind_applied" };

        public static readonly TableSpec Off = new TableSpec("Career", "Off").Column("reallife_career_col_post", 600f, TableSort.None, flexible: true);

        public const int PathApply = 0, PathWait = 1, PathCourse = 2, PathSkills = 3;
        private static readonly string[] Paths = { "reallife_career_kind_apply", "reallife_career_kind_wait", "reallife_career_kind_course", "reallife_career_kind_skills" };

        /// <summary>Professions: 0 profession, 1 first post, 2 pay from, 3 fit, 4 vacancies (open to you of all), 5 next step.</summary>
        public static readonly TableSpec Professions = new TableSpec("Career", "Professions")
            .Column("reallife_career_col_profession", 300f, TableSort.Text, flexible: true)
            .Column("reallife_career_col_first", 300f, TableSort.Text)
            .Column("reallife_career_col_pay_from", 170f, TableSort.Number)
            .Column("reallife_career_col_fit", 170f, TableSort.Number)
            .Column("reallife_career_col_vacancies", 170f, TableSort.Number)
            .Column("reallife_career_col_next", 420f, TableSort.Text, right: true)
            .Filter("reallife_career_all_paths", Paths)
            .Empty("reallife_career_empty_paths");

        /// <summary>Jobs: 0 post, 1 profession, 2 employer, 3 how it came, 4 pay, 5 days worked, 6 closes, 7 fit, 8 state.</summary>
        public static readonly TableSpec Jobs = new TableSpec("Career", "Jobs")
            .Column("reallife_career_col_post", 330f, TableSort.Text, flexible: true)
            .Column("reallife_career_col_profession", 230f, TableSort.Text)
            .Column("reallife_career_col_employer", 330f, TableSort.Text)
            .Column("reallife_career_col_via", 220f, TableSort.Text)
            .Column("reallife_career_col_pay", 180f, TableSort.Number)
            .Column("reallife_career_col_mode", 170f, TableSort.Text)
            .Column("reallife_career_col_closes", 160f, TableSort.Number)
            .Column("reallife_career_col_fit", 170f, TableSort.Number)
            .Column("reallife_career_col_state", 220f, TableSort.Text, right: true)
            .Filter("reallife_career_all_professions")
            .Filter("reallife_career_all_states", States)
            .Empty("reallife_career_empty_jobs");

        /// <summary>Applications: 0 post, 1 employer, 2 sent, 3 how it came, 4 interview, 5 offer, 6 answer by, 7 step.</summary>
        public static readonly TableSpec Applications = new TableSpec("Career", "Applications")
            .Column("reallife_career_col_post", 330f, TableSort.Text, flexible: true)
            .Column("reallife_career_col_employer", 330f, TableSort.Text)
            .Column("reallife_career_col_sent", 160f, TableSort.Number)
            .Column("reallife_career_col_via", 220f, TableSort.Text)
            .Column("reallife_career_col_interview", 260f, TableSort.Number)
            .Column("reallife_career_col_offer", 180f, TableSort.Number)
            .Column("reallife_career_col_by", 160f, TableSort.Number)
            .Column("reallife_career_col_state", 260f, TableSort.Text, right: true)
            .Empty("reallife_career_empty_applications");

        /// <summary>My job: 0 day, 1 hours, 2 where, 3 state.</summary>
        public static readonly TableSpec Days = new TableSpec("Career", "Job")
            .Column("reallife_career_col_day", 300f, TableSort.None, flexible: true)
            .Column("reallife_career_col_hours", 300f, TableSort.None)
            .Column("reallife_career_col_where", 360f, TableSort.None)
            .Column("reallife_career_col_state", 300f, TableSort.None, right: true)
            .Empty("reallife_career_empty_job");

        public const int ContractOpen = 0, ContractDone = 1;
        private static readonly string[] ContractKinds = { "reallife_own_kind_open", "reallife_own_kind_done" };

        /// <summary>Own work: 0 client, 1 profession, 2 hours, 3 fee, 4 due, 5 state.</summary>
        public static readonly TableSpec Own = new TableSpec("Career", "Own")
            .Column("reallife_own_col_client", 360f, TableSort.Text, flexible: true)
            .Column("reallife_career_col_profession", 260f, TableSort.Text)
            .Column("reallife_own_col_hours", 220f, TableSort.Number)
            .Column("reallife_own_col_fee", 200f, TableSort.Money)
            .Column("reallife_own_col_due", 180f, TableSort.Number)
            .Column("reallife_career_col_state", 240f, TableSort.Text, right: true)
            .Filter("reallife_own_all_contracts", ContractKinds)
            .Empty("reallife_own_empty_contracts");

        public const int CvJobs = 0, CvQualifications = 1;
        private static readonly string[] CvKinds = { "reallife_career_kind_jobs", "reallife_career_kind_qualifications" };

        /// <summary>CV: 0 what, 1 where, 2 from, 3 to, 4 detail, 5 earned.</summary>
        public static readonly TableSpec Cv = new TableSpec("Career", "CV")
            .Column("reallife_career_col_what", 360f, TableSort.Text, flexible: true)
            .Column("reallife_career_col_where", 330f, TableSort.Text)
            .Column("reallife_career_col_from", 160f, TableSort.Number)
            .Column("reallife_career_col_to", 160f, TableSort.Number)
            .Column("reallife_career_col_detail", 330f, TableSort.Text)
            .Column("reallife_career_col_earned", 200f, TableSort.Money, sums: true, right: true)
            .Filter("reallife_career_all_cv", CvKinds)
            .Empty("reallife_career_empty_cv");
    }
}
