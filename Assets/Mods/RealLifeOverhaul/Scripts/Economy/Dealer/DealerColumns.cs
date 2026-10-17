using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>The app's tables as data (GameUi.TableSpec); each table's summary numbers its columns for whoever writes the rows.</summary>
    public static class DealerColumns
    {
        private const string App = "Dealership";

        // The filters' kinds: the Market tab's (by ListingKind), the Offers, Finance and Growth tabs'.
        public static readonly string[] MarketKinds = { "reallife_dealer_kind_wholesale", "reallife_dealer_kind_used", "reallife_dealer_kind_auction" };
        public const int Waiting = 0, Sold = 1;
        public static readonly string[] OfferKinds = { "reallife_dealer_kind_waiting", "reallife_dealer_kind_sold" };
        public const int Plans = 0, Covers = 1;
        public static readonly string[] FinanceKinds = { "reallife_dealer_kind_plans", "reallife_dealer_kind_covers" };
        public const int GrowthEvents = 0, GrowthMissions = 1, GrowthRep = 2;
        public static readonly string[] GrowthKinds = { "reallife_dealer_kind_events", "reallife_dealer_kind_missions", "reallife_dealer_kind_rep" };

        public static readonly TableSpec Off = new TableSpec(App, "Off").Column("reallife_dealer_col_car", 600f, TableSort.None, flexible: true);

        /// <summary>Stock: 0 car, 1 dealership, 2 km, 3 condition, 4 cost, 5 asking, 6 worth, 7 days on the lot, 8 state.</summary>
        public static readonly TableSpec Stock = new TableSpec(App, "Stock")
            .Column("reallife_dealer_col_car", 420f, TableSort.Text, flexible: true)
            .Column("reallife_dealer_col_dealership", 300f, TableSort.Text)
            .Column("reallife_dealer_col_km", 180f, TableSort.Number)
            .Column("reallife_dealer_col_condition", 170f, TableSort.Number)
            .Column("reallife_dealer_col_cost", 200f, TableSort.Money, sums: true)
            .Column("reallife_dealer_col_asking", 200f, TableSort.Money, sums: true)
            .Column("reallife_dealer_col_worth", 200f, TableSort.Money, sums: true)
            .Column("reallife_dealer_col_days", 140f, TableSort.Number)
            .Column("reallife_dealer_col_state", 260f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_businesses")
            .Order(7, false)
            .Empty("reallife_dealer_empty_stock");

        /// <summary>Market: 0 car, 1 kind, 2 km, 3 condition, 4 price or bid, 5 colour, 6 ends, 7 state.</summary>
        public static readonly TableSpec Market = new TableSpec(App, "Market")
            .Column("reallife_dealer_col_car", 420f, TableSort.Text, flexible: true)
            .Column("reallife_dealer_col_kind", 260f, TableSort.Text)
            .Column("reallife_dealer_col_km", 180f, TableSort.Number)
            .Column("reallife_dealer_col_condition", 200f, TableSort.Number)
            .Column("reallife_dealer_col_price", 220f, TableSort.Money)
            .Column("reallife_dealer_col_colour", 220f, TableSort.Text)
            .Column("reallife_dealer_col_ends", 240f, TableSort.Number)
            .Column("reallife_dealer_col_state", 300f, TableSort.Text, right: true)
            .Filter("reallife_dealer_all_kinds", MarketKinds)
            .Empty("reallife_dealer_empty_market");

        /// <summary>Offers: 0 customer, 1 car, 2 dealership, 3 offer or price, 4 asking or cost, 5 what else, 6 lapses or day, 7 state.</summary>
        public static readonly TableSpec Offers = new TableSpec(App, "Offers")
            .Column("reallife_dealer_col_customer", 300f, TableSort.Text)
            .Column("reallife_dealer_col_car", 360f, TableSort.Text, flexible: true)
            .Column("reallife_dealer_col_dealership", 280f, TableSort.Text)
            .Column("reallife_dealer_col_offer", 200f, TableSort.Money, sums: true)
            .Column("reallife_dealer_col_asking", 200f, TableSort.Money)
            .Column("reallife_dealer_col_extras", 320f, TableSort.Text)
            .Column("reallife_dealer_col_lapses", 200f, TableSort.Number)
            .Column("reallife_dealer_col_state", 240f, TableSort.Text, right: true)
            .Filter("reallife_dealer_all_offers", OfferKinds)
            .Empty("reallife_dealer_empty_offers");

        /// <summary>Finance: 0 customer, 1 car, 2 dealership, 3 owed or paid out, 4 a week or claims, 5 next or until, 6 state.</summary>
        public static readonly TableSpec Finance = new TableSpec(App, "Finance")
            .Column("reallife_dealer_col_customer", 300f, TableSort.Text)
            .Column("reallife_dealer_col_car", 360f, TableSort.Text, flexible: true)
            .Column("reallife_dealer_col_dealership", 280f, TableSort.Text)
            .Column("reallife_dealer_col_owed", 220f, TableSort.Money, sums: true)
            .Column("reallife_dealer_col_weekly", 200f, TableSort.Money, sums: true)
            .Column("reallife_dealer_col_next", 260f, TableSort.Date)
            .Column("reallife_dealer_col_state", 280f, TableSort.Text, right: true)
            .Filter("reallife_dealer_all_finance", FinanceKinds)
            .Empty("reallife_dealer_empty_finance");

        /// <summary>Dealerships: 0 dealership, 1 cars on the lot, 2 salespeople, 3 visitors today, 4 sold today, 5 takings today, 6 margin this week, 7 offers waiting.</summary>
        public static readonly TableSpec Lots = new TableSpec(App, "Dealerships")
            .Column("reallife_dealer_col_dealership", 420f, TableSort.Text, flexible: true)
            .Column("reallife_dealer_col_lot", 200f, TableSort.Number)
            .Column("reallife_dealer_col_staff", 200f, TableSort.Number)
            .Column("reallife_dealer_col_visitors", 200f, TableSort.Number)
            .Column("reallife_dealer_col_sold", 160f, TableSort.Number)
            .Column("reallife_dealer_col_takings", 230f, TableSort.Money, sums: true)
            .Column("reallife_dealer_col_margin", 230f, TableSort.Money, sums: true)
            .Column("reallife_dealer_col_waiting", 200f, TableSort.Number, right: true)
            .Empty("reallife_dealer_empty_lots");

        /// <summary>Growth: 0 event, mission or step, 1 showroom, 2 days, progress or reputation, 3 cost, reward or next step, 4 what it does, 5 state.</summary>
        public static readonly TableSpec Growth = new TableSpec(App, "Growth")
            .Column("reallife_dealer_col_what", 340f, TableSort.Text)
            .Column("reallife_dealer_col_dealership", 280f, TableSort.Text)
            .Column("reallife_dealer_col_figure", 200f, TableSort.Number)
            .Column("reallife_dealer_col_gives", 200f, TableSort.Number)
            .Column("reallife_dealer_col_does", 520f, TableSort.Text, flexible: true)
            .Column("reallife_dealer_col_state", 280f, TableSort.Text, right: true)
            .Filter("reallife_dealer_all_growth", GrowthKinds)
            .Empty("reallife_dealer_empty_growth");

        /// <summary>Reports: the table is hidden behind the report's page; one column for whoever asks for it.</summary>
        public static readonly TableSpec Reports = new TableSpec(App, "Reports").Column("reallife_dealer_col_what", 600f, TableSort.None, flexible: true);
    }
}
