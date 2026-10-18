using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The Open Roles app's tables, declared as data (GameUi.TableSpec). The numbers beside each are the columns.</summary>
    public static class RolesColumns
    {
        // row kinds: where a professional stands, the Team tab's second filter
        public const int Waiting = 0, Leaving = 1, Learning = 2, Settled = 3;
        private static readonly string[] StateKinds = { "reallife_roles_kind_waiting", "reallife_roles_kind_leaving", "reallife_roles_kind_learning", "reallife_roles_kind_settled" };

        public static readonly TableSpec Off = new TableSpec("Open Roles", "Off").Column("reallife_roles_col_name", 600f, TableSort.None, flexible: true);

        /// <summary>Team: 0 name, 1 role, 2 level, 3 main skill, 4 specialisms, 5 morale, 6 burnout, 7 business, 8 state.</summary>
        public static readonly TableSpec Team = new TableSpec("Open Roles", "Team")
            .Column("reallife_roles_col_name", 300f, TableSort.Text, flexible: true)
            .Column("reallife_roles_col_role", 330f, TableSort.Text)
            .Column("reallife_roles_col_level", 170f, TableSort.Number)
            .Column("reallife_roles_col_skill", 150f, TableSort.Number)
            .Column("reallife_roles_col_specialisms", 330f, TableSort.Number)
            .Column("reallife_roles_col_morale", 150f, TableSort.Number)
            .Column("reallife_roles_col_burnout", 150f, TableSort.Number)
            .Column("reallife_roles_col_business", 280f, TableSort.Text)
            .Column("reallife_roles_col_state", 260f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_businesses")
            .Filter("reallife_roles_all_states", StateKinds)
            .Empty("reallife_roles_empty_team");

        // row kinds: the jobs board
        public const int AdOpen = 0, AdClosed = 1;
        private static readonly string[] AdKinds = { "reallife_hire_kind_ad_open", "reallife_hire_kind_ad_closed" };

        /// <summary>Jobs: 0 role, 1 level, 2 business, 3 pay, 4 arrangement, 5 applicants, 6 ends, 7 cost.</summary>
        public static readonly TableSpec Jobs = new TableSpec("Open Roles", "Jobs")
            .Column("reallife_roles_col_role", 360f, TableSort.Text, flexible: true)
            .Column("reallife_roles_col_level", 170f, TableSort.Number)
            .Column("reallife_roles_col_business", 300f, TableSort.Text)
            .Column("reallife_hire_col_pay", 320f, TableSort.Number)
            .Column("reallife_hire_col_work", 200f, TableSort.Text)
            .Column("reallife_hire_col_applicants", 180f, TableSort.Number)
            .Column("reallife_hire_col_ends", 200f, TableSort.Number)
            .Column("reallife_roles_col_state", 220f, TableSort.Text, right: true)
            .Filter("reallife_hire_all_ads", AdKinds)
            .Empty("reallife_hire_empty_jobs");

        // row kinds: the applicants
        public const int StageNew = 0, StageScreened = 1, StageOffer = 2, StageHired = 3, StageGone = 4;
        private static readonly string[] StageKinds = { "reallife_hire_kind_new", "reallife_hire_kind_screened", "reallife_hire_kind_offer", "reallife_hire_kind_hired", "reallife_hire_kind_gone" };

        /// <summary>Applicants: 0 name, 1 role, 2 level, 3 skill (as the CV says until it is tested), 4 asks, 5 for, 6 applied, 7 keen, 8 stage.</summary>
        public static readonly TableSpec Applicants = new TableSpec("Open Roles", "Applicants")
            .Column("reallife_roles_col_name", 290f, TableSort.Text, flexible: true)
            .Column("reallife_roles_col_role", 320f, TableSort.Text)
            .Column("reallife_roles_col_level", 160f, TableSort.Number)
            .Column("reallife_roles_col_skill", 150f, TableSort.Number)
            .Column("reallife_hire_col_asks", 190f, TableSort.Number)
            .Column("reallife_hire_col_for", 320f, TableSort.Text)
            .Column("reallife_hire_col_applied", 170f, TableSort.Number)
            .Column("reallife_hire_col_keen", 150f, TableSort.Number)
            .Column("reallife_roles_col_state", 260f, TableSort.Text, right: true)
            .Filter("reallife_hire_all_stages", StageKinds)
            .Filter("reallife_hire_all_jobs")
            .Empty("reallife_hire_empty_applicants");

        /// <summary>Market: 0 family, 1 seekers, 2 juniors, 3 mids, 4 seniors up, 5 rivals' openings, 6 your ads, 7 shortage, 8 first role's going rate at mid.</summary>
        public static readonly TableSpec Market = new TableSpec("Open Roles", "Market")
            .Column("reallife_hire_col_family", 330f, TableSort.Text, flexible: true)
            .Column("reallife_hire_col_seekers", 170f, TableSort.Number)
            .Column("reallife_hire_col_juniors", 160f, TableSort.Number)
            .Column("reallife_hire_col_mids", 160f, TableSort.Number)
            .Column("reallife_hire_col_seniors", 190f, TableSort.Number)
            .Column("reallife_hire_col_openings", 190f, TableSort.Number)
            .Column("reallife_hire_col_yours", 150f, TableSort.Number)
            .Column("reallife_hire_col_shortage", 170f, TableSort.Number)
            .Column("reallife_hire_col_going", 220f, TableSort.Number, right: true)
            .Empty("reallife_hire_empty_market");

        /// <summary>Search: 0 role, 1 level, 2 business, 3 fee, 4 paid, 5 delivers, 6 candidates, 7 state.</summary>
        public static readonly TableSpec Search = new TableSpec("Open Roles", "Search")
            .Column("reallife_roles_col_role", 360f, TableSort.Text, flexible: true)
            .Column("reallife_roles_col_level", 170f, TableSort.Number)
            .Column("reallife_roles_col_business", 300f, TableSort.Text)
            .Column("reallife_hire_col_fee", 220f, TableSort.Money, sums: true)
            .Column("reallife_hire_col_paid", 220f, TableSort.Money, sums: true)
            .Column("reallife_hire_col_due", 200f, TableSort.Number)
            .Column("reallife_hire_col_applicants", 180f, TableSort.Number)
            .Column("reallife_roles_col_state", 240f, TableSort.Text, right: true)
            .Empty("reallife_hire_empty_search");
    }
}
