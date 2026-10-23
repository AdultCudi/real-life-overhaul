using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The Construction app's tables as data (GameUi.TableSpec); each summary numbers the columns for the rows' writer.</summary>
    internal static class ConstructionTables
    {
        // What a row of the Land tab is: a lot for sale, a lot of the player's, a building of the player's, a landmark's site.
        public const int LandForSale = 0, LandOwned = 1, LandBuilding = 2, LandLandmark = 3;
        private static readonly string[] LandKinds = { "reallife_bld_kind_for_sale", "reallife_bld_kind_owned", "reallife_bld_kind_building", "reallife_lmk_kind_landmark" };

        // What a row of the Crews tab is: a crew, an offer, plant, the office's staff.
        public const int CrewsCrew = 0, CrewsOffer = 1, CrewsPlant = 2, CrewsStaff = 3;
        private static readonly string[] CrewsKinds = { "reallife_bld_kind_crew", "reallife_bld_kind_offer", "reallife_bld_kind_plant", "reallife_bld_kind_staff" };

        public static readonly TableSpec Off = new TableSpec("Construction", "Off").Column("reallife_bld_col_what", 600f, TableSort.None, flexible: true);

        /// <summary>Overview: 0 project, 1 stage, 2 next step.</summary>
        public static readonly TableSpec Overview = new TableSpec("Construction", "Overview")
            .Column("reallife_bld_col_project", 760f, TableSort.Text, flexible: true)
            .Column("reallife_bld_col_stage", 420f, TableSort.Text)
            .Column("reallife_bld_col_next", 560f, TableSort.Text, right: true)
            .Empty("reallife_bld_empty_projects");

        /// <summary>Land: 0 place, 1 size, 2 zoned for, 3 price, 4 state.</summary>
        public static readonly TableSpec Land = new TableSpec("Construction", "Land")
            .Column("reallife_bld_col_place", 560f, TableSort.Text, flexible: true)
            .Column("reallife_bld_col_size", 260f, TableSort.Number)
            .Column("reallife_bld_col_zoned", 420f, TableSort.Text)
            .Column("reallife_bld_col_price", 340f, TableSort.Money)
            .Column("reallife_bld_col_state", 300f, TableSort.Text, right: true)
            .Filter("reallife_bld_all_land", LandKinds)
            .Order(3, true)
            .Empty("reallife_bld_empty_land");

        /// <summary>Projects: 0 project, 1 stage, 2 progress, 3 spent, 4 next step.</summary>
        public static readonly TableSpec Projects = new TableSpec("Construction", "Projects")
            .Column("reallife_bld_col_project", 620f, TableSort.Text, flexible: true)
            .Column("reallife_bld_col_stage", 380f, TableSort.Text)
            .Column("reallife_bld_col_progress", 220f, TableSort.Number)
            .Column("reallife_bld_col_spent", 320f, TableSort.Money, sums: true)
            .Column("reallife_bld_col_next", 420f, TableSort.Text, right: true)
            .Empty("reallife_bld_empty_projects");

        /// <summary>Tenders: 0 works, 1 value, 2 closes, 3 the player's bid, 4 state.</summary>
        public static readonly TableSpec Tenders = new TableSpec("Construction", "Tenders")
            .Column("reallife_bld_col_works", 640f, TableSort.Text, flexible: true)
            .Column("reallife_bld_col_value", 320f, TableSort.Money)
            .Column("reallife_bld_col_closes", 240f, TableSort.Date)
            .Column("reallife_bld_col_bid", 240f, TableSort.Text)
            .Column("reallife_bld_col_state", 320f, TableSort.Text, right: true)
            .Order(2, false)
            .Empty("reallife_bld_empty_tenders");

        /// <summary>Crews: 0 name, 1 what, 2 skill, 3 morale, 4 a day, 5 where.</summary>
        public static readonly TableSpec Crews = new TableSpec("Construction", "Crews")
            .Column("reallife_bld_col_name", 460f, TableSort.Text, flexible: true)
            .Column("reallife_bld_col_kind", 420f, TableSort.Text)
            .Column("reallife_bld_col_skill", 180f, TableSort.Number)
            .Column("reallife_bld_col_morale", 180f, TableSort.Number)
            .Column("reallife_bld_col_day", 260f, TableSort.Money, sums: true)
            .Column("reallife_bld_col_where", 380f, TableSort.Text, right: true)
            .Filter("reallife_bld_all_people", CrewsKinds)
            .Empty("reallife_bld_empty_crews");

        /// <summary>Company: 0 what, 1 how it stands.</summary>
        public static readonly TableSpec Company = new TableSpec("Construction", "Company")
            .Column("reallife_bld_col_what", 700f, TableSort.None, flexible: true)
            .Column("reallife_bld_col_state", 700f, TableSort.None, right: true);

        /// <summary>Rivals: 0 builder, 1 speciality, 2 reputation, 3 works won, 4 built.</summary>
        public static readonly TableSpec Rivals = new TableSpec("Construction", "Rivals")
            .Column("reallife_bld_col_builder", 620f, TableSort.Text, flexible: true)
            .Column("reallife_bld_col_speciality", 380f, TableSort.Text)
            .Column("reallife_bld_col_reputation", 260f, TableSort.Number)
            .Column("reallife_bld_col_won", 220f, TableSort.Number)
            .Column("reallife_bld_col_built", 220f, TableSort.Number, right: true)
            .Order(2, false);

        public static TableSpec For(int tab)
        {
            switch (tab)
            {
                case ConstructionDirector.TabLand: return Land;
                case ConstructionDirector.TabProjects: return Projects;
                case ConstructionDirector.TabTenders: return Tenders;
                case ConstructionDirector.TabCrews: return Crews;
                case ConstructionDirector.TabCompany: return Company;
                case ConstructionDirector.TabRivals: return Rivals;
                default: return Overview;
            }
        }
    }
}
