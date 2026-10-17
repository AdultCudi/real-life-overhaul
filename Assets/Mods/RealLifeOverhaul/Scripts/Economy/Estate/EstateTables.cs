using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The Property app's tables as data (GameUi.TableSpec); each summary numbers the columns for whoever writes the rows.</summary>
    internal static class EstateTables
    {
        /// <summary>The tabs, in the order of EstateDirector.TabMarket and its neighbours.</summary>
        public static readonly string[] Tabs =
        {
            "reallife_estate_tab_market", "reallife_estate_tab_mine", "reallife_estate_tab_tenants", "reallife_estate_tab_works", "reallife_estate_tab_agency",
            "reallife_estate_tab_hotel", "reallife_estate_tab_hire"
        };

        // The Market tab's second filter: the four uses, then a hotel.
        public const int IsHotel = EstateCatalogue.Uses;
        private static readonly string[] MarketKinds =
        {
            "reallife_estate_use_homes", "reallife_estate_use_shops", "reallife_estate_use_offices", "reallife_estate_use_storage", "reallife_estate_row_hotel"
        };

        public const int OwnBuilding = 0, OwnHotel = 1, OwnLot = 2;
        private static readonly string[] OwnKinds = { "reallife_estate_kind_building", "reallife_estate_kind_hotel", "reallife_estate_kind_lot" };

        public const int TenantPaying = 0, TenantBehind = 1, TenantNotice = 2, TenantNone = 3, TenantApplies = 4;
        private static readonly string[] TenantStates =
        {
            "reallife_estate_filter_paying", "reallife_estate_filter_behind", "reallife_estate_filter_notice", "reallife_estate_filter_empty", "reallife_estate_filter_applicants"
        };

        public const int WorksOn = 0, WorksNeeded = 1, WorksNone = 2;
        private static readonly string[] WorkStates = { "reallife_estate_filter_works_on", "reallife_estate_filter_works_needed", "reallife_estate_filter_works_none" };

        public const int IsLetting = 0, IsSale = 1;
        private static readonly string[] InstructionKinds = { "reallife_estate_filter_lettings", "reallife_estate_filter_sales" };

        public const int CarOut = 0, CarIn = 1, CarNotOnHire = 2;
        private static readonly string[] CarPlaces = { "reallife_estate_filter_out", "reallife_estate_filter_in", "reallife_estate_filter_not_on_hire" };

        /// <summary>While the feature is not running: one empty column.</summary>
        public static readonly TableSpec Off = new TableSpec("Property", "Off").Column("reallife_estate_col_building", 600f, TableSort.None, flexible: true);

        /// <summary>Market: 0 building or hotel, 1 district, 2 use, 3 asking price, 4 condition, 5 rent a day if let, 6 yield a year on the price.</summary>
        public static readonly TableSpec Market = new TableSpec("Property", "Market")
            .Column("reallife_estate_col_building", 460f, TableSort.Text, flexible: true)
            .Column("reallife_co_col_district", 340f, TableSort.Text)
            .Column("reallife_estate_col_use", 220f, TableSort.Text)
            .Column("reallife_estate_col_price", 280f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_condition", 220f, TableSort.Number)
            .Column("reallife_estate_col_rent", 260f, TableSort.Money)
            .Column("reallife_estate_col_yield", 220f, TableSort.Number, right: true)
            .Filter("reallife_tab_all_districts")
            .Filter("reallife_tab_all_kinds", MarketKinds)
            .Order(3, true)
            .Empty("reallife_estate_empty_market");

        /// <summary>Mine: 0 what, 1 kind, 2 state, 3 worth, 4 paid, 5 coming in a day, 6 in this year, 7 costs this year.</summary>
        public static readonly TableSpec Mine = new TableSpec("Property", "Mine")
            .Column("reallife_estate_col_building", 420f, TableSort.Text, flexible: true)
            .Column("reallife_estate_col_kind", 200f, TableSort.Text)
            .Column("reallife_estate_col_state", 380f, TableSort.Text)
            .Column("reallife_estate_col_worth", 250f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_paid", 250f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_rent", 230f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_year_in", 240f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_year_out", 240f, TableSort.Money, sums: true, right: true)
            .Filter("reallife_tab_all_kinds", OwnKinds)
            .Order(3, false)
            .Empty("reallife_estate_empty_mine");

        /// <summary>Tenants: 0 building, 1 tenant or applicant, 2 rent a day, 3 owed, 4 record, 5 lease or application until, 6 state.</summary>
        public static readonly TableSpec Tenants = new TableSpec("Property", "Tenants")
            .Column("reallife_estate_col_building", 440f, TableSort.Text, flexible: true)
            .Column("reallife_estate_col_who", 420f, TableSort.Text)
            .Column("reallife_estate_col_rent", 240f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_owed", 220f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_record", 180f, TableSort.Number)
            .Column("reallife_estate_col_day", 180f, TableSort.Date)
            .Column("reallife_estate_col_state", 340f, TableSort.Text, right: true)
            .Filter("reallife_estate_filter_all_states", TenantStates)
            .Order(3, false)
            .Empty("reallife_estate_empty_tenants");

        /// <summary>Works: 0 building, 1 condition, 2 level, 3 use, 4 what a repair to new costs, 5 works under way, 6 until.</summary>
        public static readonly TableSpec Works = new TableSpec("Property", "Works")
            .Column("reallife_estate_col_building", 460f, TableSort.Text, flexible: true)
            .Column("reallife_estate_col_condition", 220f, TableSort.Number)
            .Column("reallife_estate_col_level", 160f, TableSort.Number)
            .Column("reallife_estate_col_use", 220f, TableSort.Text)
            .Column("reallife_estate_col_repair", 260f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_works", 340f, TableSort.Text)
            .Column("reallife_estate_col_day", 180f, TableSort.Date, right: true)
            .Filter("reallife_estate_filter_all_states", WorkStates)
            .Order(1, true)
            .Empty("reallife_estate_empty_works");

        /// <summary>Agency: 0 instruction, 1 kind, 2 worth, 3 commission if it completes, 4 hours still to do, 5 stage, 6 until.</summary>
        public static readonly TableSpec Agency = new TableSpec("Property", "Agency")
            .Column("reallife_estate_col_instruction", 460f, TableSort.Text, flexible: true)
            .Column("reallife_estate_col_kind", 200f, TableSort.Text)
            .Column("reallife_estate_col_worth", 270f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_commission", 270f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_hours", 200f, TableSort.Number)
            .Column("reallife_estate_col_stage", 280f, TableSort.Text)
            .Column("reallife_estate_col_day", 180f, TableSort.Date, right: true)
            .Filter("reallife_tab_all_kinds", InstructionKinds)
            .Filter("reallife_tab_all_stages", EstateText.StageKeys)
            .Order(6, true)
            .Empty("reallife_estate_empty_agency");

        /// <summary>Hotel: 0 hotel, 1 rooms sold last night, 2 takings, 3 costs, 4 taken this year, 5 spent this year, 6 rating.</summary>
        public static readonly TableSpec Hotel = new TableSpec("Property", "Hotel")
            .Column("reallife_estate_col_hotel", 460f, TableSort.Text, flexible: true)
            .Column("reallife_estate_col_sold", 220f, TableSort.Number)
            .Column("reallife_estate_col_takings", 260f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_costs", 260f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_year_in", 270f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_year_out", 270f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_rating", 200f, TableSort.Number, right: true)
            .Order(2, false)
            .Empty("reallife_estate_empty_hotel");

        /// <summary>Car hire: 0 car, 1 class, 2 state, 3 rate a day, 4 hires, 5 taken, 6 due back.</summary>
        public static readonly TableSpec Hire = new TableSpec("Property", "Car hire")
            .Column("reallife_estate_col_car", 460f, TableSort.Text, flexible: true)
            .Column("reallife_estate_col_class", 220f, TableSort.Text)
            .Column("reallife_estate_col_state", 360f, TableSort.Text)
            .Column("reallife_estate_col_rate", 240f, TableSort.Money)
            .Column("reallife_estate_col_hires", 180f, TableSort.Number)
            .Column("reallife_estate_col_takings", 260f, TableSort.Money, sums: true)
            .Column("reallife_estate_col_day", 180f, TableSort.Date, right: true)
            .Filter("reallife_estate_filter_all_classes", EstateText.ClassKeys)
            .Filter("reallife_estate_filter_all_places", CarPlaces)
            .Order(5, false)
            .Empty("reallife_estate_empty_hire");
    }
}
