using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The Open Roles app's tables, declared as data (GameUi.TableSpec): for each tab its columns, how each sorts, what
    /// its rows can be filtered by and the sentence shown when a filter leaves nothing. The numbers beside each are the
    /// columns, for whoever writes the rows.
    /// </summary>
    public static class RolesColumns
    {
        // ---- where a professional stands (the second filter of the Team tab)
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
    }
}
