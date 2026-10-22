using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Company app's tables as data (GameUi.TableSpec); each summary numbers the columns for the rows' writer.</summary>
    internal static class CompanyTables
    {
        // How a member of staff stands: the Staff tab's second filter.
        public const int StaffWaiting = 0, StaffLeaving = 1, StaffUnderpaid = 2, StaffFine = 3;
        private static readonly string[] StaffStates = { "reallife_tab_co_staff_waiting", "reallife_tab_co_staff_leaving", "reallife_tab_co_staff_underpaid", "reallife_tab_co_staff_fine" };

        // A policy that runs, a cover with none, a claim: the Insurance tab's second filter.
        public const int Insured = 0, Uninsured = 1, IsClaim = 2;
        private static readonly string[] CoverStates = { "reallife_tab_co_insured", "reallife_tab_co_uninsured", "reallife_tab_co_claim" };

        // A campaign running, booked, none at a business, a report on one that has ended.
        public const int PushRunning = 0, PushBooked = 1, PushNone = 2, PushEnded = 3;
        private static readonly string[] PushStates = { "reallife_tab_co_push_running", "reallife_tab_co_push_booked", "reallife_tab_co_push_none", "reallife_tab_co_push_ended" };

        // What a row of the tax tab is about, and how a licence stands.
        public const int AboutTax = 0, AboutHidden = 1, AboutLicence = 2;
        private static readonly string[] TaxSubjects = { "reallife_tab_co_about_tax", "reallife_tab_co_about_hidden", "reallife_tab_co_about_licence" };
        public const int LicenceValid = 0, LicenceSoon = 1, LicenceMissing = 2;
        private static readonly string[] LicenceStates = { "reallife_tab_co_lic_valid", "reallife_tab_co_lic_soon", "reallife_tab_co_lic_missing" };

        /// <summary>With no state, and for a tab whose part is off: one empty column.</summary>
        public static readonly TableSpec Off = new TableSpec("Company", "Off").Column("reallife_co_col_business", 600f, TableSort.None, flexible: true);

        /// <summary>Overview: 0 business, 1 district, 2 profit yesterday, 3 state.</summary>
        public static readonly TableSpec Overview = new TableSpec("Company", "Overview")
            .Column("reallife_co_col_business", 620f, TableSort.Text, flexible: true)
            .Column("reallife_co_col_district", 500f, TableSort.Text)
            .Column("reallife_co_col_profit", 360f, TableSort.Money, sums: true)
            .Column("reallife_co_col_state", 380f, TableSort.Custom, right: true)
            .Filter("reallife_tab_all_districts")
            .Empty("reallife_tab_empty_businesses");

        /// <summary>Staff: 0 name, 1 business, 2 paid and the going rate, 3 standing, 4 pay a day, 5 skill.</summary>
        public static readonly TableSpec Staff = new TableSpec("Company", "Staff")
            .Column("reallife_co_col_name", 440f, TableSort.Text, flexible: true)
            .Column("reallife_co_col_business", 420f, TableSort.Text)
            .Column("reallife_co_col_pay", 420f, TableSort.Money)
            .Column("reallife_co_col_standing", 300f, TableSort.Custom)
            .Column("reallife_tab_co_col_day_pay", 260f, TableSort.Money, sums: true)
            .Column("reallife_tab_co_col_skill", 180f, TableSort.Number, right: true)
            .Filter("reallife_tab_all_businesses")
            .Filter("reallife_tab_co_all_staff", StaffStates)
            .Empty("reallife_tab_empty_staff");

        /// <summary>Insurance: 0 business, 1 cover, 2 insurer, 3 premium, 4 until.</summary>
        public static readonly TableSpec Cover = new TableSpec("Company", "Insurance")
            .Column("reallife_co_col_business", 560f, TableSort.Text, flexible: true)
            .Column("reallife_co_col_cover", 420f, TableSort.Text)
            .Column("reallife_co_col_insurer", 420f, TableSort.Text)
            .Column("reallife_co_col_premium", 400f, TableSort.Money, sums: true)
            .Column("reallife_co_col_until", 280f, TableSort.Date, right: true)
            .Filter("reallife_tab_co_all_cover", CompanyText.CoverKeys)
            .Filter("reallife_tab_co_all_policies", CoverStates)
            .Empty("reallife_tab_empty_cover");

        /// <summary>Marketing: 0 business, 1 campaign, 2 until, 3 customers, 4 cost, 5 extra profit.</summary>
        public static readonly TableSpec Marketing = new TableSpec("Company", "Marketing")
            .Column("reallife_co_col_business", 520f, TableSort.Text, flexible: true)
            .Column("reallife_co_col_campaign", 420f, TableSort.Text)
            .Column("reallife_co_col_until", 280f, TableSort.Date)
            .Column("reallife_co_col_effect", 260f, TableSort.Number)
            .Column("reallife_tab_co_col_cost", 260f, TableSort.Money, sums: true)
            .Column("reallife_tab_co_col_return", 320f, TableSort.Money, sums: true, right: true)
            .Filter("reallife_tab_co_all_campaigns", PushStates)
            .Empty("reallife_tab_empty_campaigns");

        /// <summary>Tax and licences: 0 what, 1 detail, 2 until, 3 state.</summary>
        public static readonly TableSpec Tax = new TableSpec("Company", "Tax and licences")
            .Column("reallife_co_col_item", 620f, TableSort.Text, flexible: true)
            .Column("reallife_co_col_what", 520f, TableSort.Text)
            .Column("reallife_co_col_until", 300f, TableSort.Date)
            .Column("reallife_co_col_state", 420f, TableSort.Text, right: true)
            .Filter("reallife_tab_co_all_subjects", TaxSubjects)
            .Filter("reallife_tab_co_all_licences", LicenceStates)
            .Empty("reallife_tab_empty_tax");

        /// <summary>Rota, the list of businesses: 0 business, 1 hours, 2 staff, 3 rota.</summary>
        public static readonly TableSpec Rota = new TableSpec("Company", "Rota")
            .Column("reallife_co_col_business", 620f, TableSort.Text)
            .Column("reallife_rota_col_hours", 500f, TableSort.Text, flexible: true)
            .Column("reallife_rota_col_staff", 300f, TableSort.Number)
            .Column("reallife_rota_col_rota", 300f, TableSort.Text, right: true);

        // A business's week and one of its days are laid out by hand, with headings between rows: nothing sorts.
        public static readonly TableSpec RotaWeek = new TableSpec("Company", "Rota: a week")
            .Column("reallife_rota_col_day", 620f, TableSort.None)
            .Column("reallife_rota_col_now", 500f, TableSort.None, flexible: true)
            .Column("reallife_rota_col_advised", 300f, TableSort.None)
            .Column("reallife_rota_col_customers", 300f, TableSort.None, right: true);

        /// <summary>Rota, the plans: 0 plan, 1 hours a week, 2 staff, 3 profit a week, 4 gain, 5 needs; then the people.</summary>
        public static readonly TableSpec RotaPlans = new TableSpec("Company", "Rota: the plans")
            .Column("reallife_rota_col_plan", 520f, TableSort.None)
            .Column("reallife_rota_col_week_hours", 380f, TableSort.None)
            .Column("reallife_rota_col_staff", 240f, TableSort.None)
            .Column("reallife_rota_col_profit", 300f, TableSort.None)
            .Column("reallife_rota_col_gain", 300f, TableSort.None)
            .Column("reallife_rota_col_needs", 600f, TableSort.None, flexible: true);

        public static readonly TableSpec RotaDay = new TableSpec("Company", "Rota: a day")
            .Column("reallife_rota_col_hour", 620f, TableSort.None)
            .Column("reallife_rota_col_customers", 500f, TableSort.None, flexible: true)
            .Column("reallife_rota_col_worth", 300f, TableSort.None)
            .Column("reallife_rota_col_staff", 300f, TableSort.None, right: true);

        /// <summary>Managers, the list: 0 place, 1 manager, 2 skill, 3 decisions this month, 4 state.</summary>
        public static readonly TableSpec Managers = new TableSpec("Company", "Managers")
            .Column("reallife_mgr_col_place", 600f, TableSort.Text, flexible: true)
            .Column("reallife_mgr_col_manager", 440f, TableSort.Text)
            .Column("reallife_tab_co_col_skill", 200f, TableSort.Number)
            .Column("reallife_mgr_col_month", 300f, TableSort.Number)
            .Column("reallife_co_col_state", 300f, TableSort.Text, right: true)
            .Empty("reallife_mgr_row_no_places");

        // A place's kinds of decision, then its decisions, newest first: laid out by hand, nothing sorts.
        public static readonly TableSpec ManagerPlace = new TableSpec("Company", "Managers: a place")
            .Column("reallife_mgr_col_what", 420f, TableSort.None)
            .Column("reallife_mgr_col_detail", 640f, TableSort.None, flexible: true)
            .Column("reallife_mgr_col_day", 220f, TableSort.None)
            .Column("reallife_co_col_state", 320f, TableSort.None, right: true);

        /// <summary>Managers, who could be appointed: 0 name, 1 skill or place, 2 skill, 3 pay, 4 now.</summary>
        public static readonly TableSpec ManagerHands = new TableSpec("Company", "Managers: who")
            .Column("reallife_co_col_name", 520f, TableSort.Text, flexible: true)
            .Column("reallife_mgr_col_where", 440f, TableSort.Text)
            .Column("reallife_tab_co_col_skill", 200f, TableSort.Number)
            .Column("reallife_co_col_pay", 300f, TableSort.Money)
            .Column("reallife_co_col_state", 260f, TableSort.Text, right: true);

        public static TableSpec For(int tab)
        {
            switch (tab)
            {
                case CompanyDirector.TabManagers: return Managers;
                case CompanyDirector.TabStaff: return Staff;
                case CompanyDirector.TabCover: return Cover;
                case CompanyDirector.TabMarketing: return Marketing;
                case CompanyDirector.TabTax: return Tax;
                case CompanyDirector.TabRota: return Rota;
                default: return Overview;
            }
        }

        /// <summary>The number of a name in a filter whose kinds are the app's own (districts, businesses), added if new.</summary>
        public static int KindOf(TableFilter filter, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return -1;
            }
            int kind = filter.Names.IndexOf(name);
            if (kind < 0)
            {
                kind = filter.Names.Count;
                filter.Names.Add(name);
            }
            return kind;
        }
    }
}
