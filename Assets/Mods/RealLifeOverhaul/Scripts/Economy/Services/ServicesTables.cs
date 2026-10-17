using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Services app's tables as data (GameUi.TableSpec): each tab's columns, sorting, sums, filter, opening order and
    /// the sentence shown when a filter leaves nothing. Each table's summary numbers its columns, for whoever writes rows.
    /// </summary>
    internal static class ServicesTables
    {
        // The kinds of row of each table, for its filter.
        public const int Own = 0, Contract = 1, NotOn = 2;                // Cleaning
        public const int OnCrew = 0, OffCrew = 1;                        // Crew
        public const int Offer = 0, Running = 1;                         // Contracts
        public const int Bid = 0, Listed = 1, Unlisted = 2;              // Sell

        private static readonly string[] RoundKinds = { "reallife_svc_kind_own", "reallife_svc_kind_contract", "reallife_svc_kind_not_on" };
        private static readonly string[] CrewKinds = { "reallife_svc_kind_crew_on", "reallife_svc_kind_crew_off" };
        private static readonly string[] ContractKinds = { "reallife_svc_kind_offer", "reallife_svc_kind_running" };
        private static readonly string[] SaleKinds = { "reallife_svc_kind_bid", "reallife_svc_kind_listed", "reallife_svc_kind_unlisted" };

        /// <summary>While the feature is not running, or a part is off: one empty column.</summary>
        public static readonly TableSpec Off = new TableSpec("Services", "Off").Column("reallife_co_col_business", 600f, TableSort.None, flexible: true);

        /// <summary>0 business, 1 district, 2 minutes a day it needs, 3 minutes it got today, 4 cleanliness, 5 next visit, 6 state.</summary>
        public static readonly TableSpec Cleaning = new TableSpec("Services", "Cleaning")
            .Column("reallife_co_col_business", 460f, TableSort.Text, flexible: true)
            .Column("reallife_co_col_district", 280f, TableSort.Text)
            .Column("reallife_svc_col_needs", 210f, TableSort.Number)
            .Column("reallife_svc_col_got", 210f, TableSort.Number)
            .Column("reallife_svc_col_cleanliness", 380f, TableSort.Number)
            .Column("reallife_svc_col_next", 440f, TableSort.Custom)
            .Column("reallife_co_col_state", 245f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", RoundKinds)
            .Order(4, true)
            .Empty("reallife_svc_empty_cleaning");

        /// <summary>0 name, 1 assigned to, 2 skill, 3 satisfaction, 4 wage an hour, 5 hours a week, 6 wages a week, 7 today's shift, 8 on the crew.</summary>
        public static readonly TableSpec Crew = new TableSpec("Services", "Crew")
            .Column("reallife_co_col_name", 360f, TableSort.Text, flexible: true)
            .Column("reallife_svc_col_base", 340f, TableSort.Text)
            .Column("reallife_svc_col_skill", 150f, TableSort.Number)
            .Column("reallife_svc_col_mood", 180f, TableSort.Number)
            .Column("reallife_svc_col_wage", 180f, TableSort.Money)
            .Column("reallife_svc_col_hours", 170f, TableSort.Number)
            .Column("reallife_svc_col_week", 200f, TableSort.Money, sums: true)
            .Column("reallife_svc_col_today", 330f, TableSort.Custom)
            .Column("reallife_svc_col_crew", 275f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", CrewKinds)
            .Order(0, true)
            .Empty("reallife_svc_empty_crew");

        /// <summary>0 business, 1 district, 2 fee a week, 3 standard asked, 4 floor now, 5 the day to answer by or the week ends, 6 state.</summary>
        public static readonly TableSpec Contracts = new TableSpec("Services", "Contracts")
            .Column("reallife_co_col_business", 460f, TableSort.Text, flexible: true)
            .Column("reallife_co_col_district", 320f, TableSort.Text)
            .Column("reallife_svc_col_fee", 240f, TableSort.Money, sums: true)
            .Column("reallife_svc_col_standard", 200f, TableSort.Number)
            .Column("reallife_svc_col_floor", 380f, TableSort.Number)
            .Column("reallife_svc_col_day", 240f, TableSort.Date)
            .Column("reallife_co_col_state", 385f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", ContractKinds)
            .Order(5, true)
            .Empty("reallife_svc_empty_contracts");

        /// <summary>0 business, 1 district or buyer, 2 worth, 3 asking price or offer, 4 offers standing, 5 an offer's last day, 6 state.</summary>
        public static readonly TableSpec Sale = new TableSpec("Services", "Sell")
            .Column("reallife_co_col_business", 460f, TableSort.Text, flexible: true)
            .Column("reallife_svc_col_buyer", 420f, TableSort.Text)
            .Column("reallife_svc_col_worth", 260f, TableSort.Money, sums: true)
            .Column("reallife_svc_col_price", 260f, TableSort.Money)
            .Column("reallife_svc_col_offers", 170f, TableSort.Number)
            .Column("reallife_svc_col_until", 220f, TableSort.Date)
            .Column("reallife_co_col_state", 435f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", SaleKinds)
            .Order(2, false)
            .Empty("reallife_svc_empty_sale");
    }
}
