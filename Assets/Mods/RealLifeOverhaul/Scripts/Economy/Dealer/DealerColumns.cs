using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The Dealership app's tables, declared as data (GameUi.TableSpec): for each tab its columns, how each sorts, which
    /// are summed in the totals line, its filters, the order it opens in and the sentence when a filter leaves nothing.
    /// The numbers beside each are the columns, for whoever writes the rows. Read when a tab is written.
    /// </summary>
    public static class DealerColumns
    {
        private const string App = "Dealership";

        // ---- the Market tab's kinds (ListingKind), and the Offers tab's and the Finance tab's
        public static readonly string[] MarketKinds = { "reallife_dealer_kind_wholesale", "reallife_dealer_kind_used", "reallife_dealer_kind_auction" };
        public const int Waiting = 0, Sold = 1;
        public static readonly string[] OfferKinds = { "reallife_dealer_kind_waiting", "reallife_dealer_kind_sold" };
        public const int Plans = 0, Covers = 1;
        public static readonly string[] FinanceKinds = { "reallife_dealer_kind_plans", "reallife_dealer_kind_covers" };

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
    }
}
