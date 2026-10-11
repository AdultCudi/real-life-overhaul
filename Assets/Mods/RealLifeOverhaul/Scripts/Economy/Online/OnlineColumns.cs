using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The Web shop app's tables, all declared in this one place as data (GameUi.TableSpec): for each tab and page its
    /// columns, how each sorts, which are summed in the totals line, what its rows can be filtered by, the order it
    /// opens in and the sentence shown when a filter leaves nothing. The numbers beside each are the columns, for
    /// whoever writes the rows. Only money is summed: the kit writes every total as money, so counts are added up in the
    /// white box instead. Read when a tab is written; nothing runs otherwise.
    /// </summary>
    public static class OnlineColumns
    {
        // ---- whether a business has an online shop (the filter of the Shops tab)
        public const int HasShop = 0, NoShop = 1;
        private static readonly string[] ShopKinds = { "reallife_online_kind_has", "reallife_online_kind_none" };

        // ---- where an order stands (the first filter of the Orders tab); an order past its promise is late whatever its stage
        public const int Waiting = 0, Packed = 1, Out = 2, Late = 3, ComingBack = 4;
        private static readonly string[] OrderKinds =
        {
            "reallife_online_stage_waiting", "reallife_online_stage_picked", "reallife_online_stage_out", "reallife_online_kind_late", "reallife_online_kind_back"
        };

        // ---- the two filters of the Catalogue tab
        public const int InStock = 0, OutOfStock = 1;
        private static readonly string[] StockKinds = { "reallife_online_kind_in_stock", "reallife_online_kind_out_stock" };
        public const int Offered = 0, NotOffered = 1;
        private static readonly string[] OfferKinds = { "reallife_online_kind_on", "reallife_online_kind_not_on" };

        /// <summary>While the feature is not running: one empty column.</summary>
        public static readonly TableSpec Off = new TableSpec("Web shop", "Off").Column("reallife_online_col_site", 600f, TableSort.None, flexible: true);

        /// <summary>Shops: 0 online shop, 1 business, 2 orders today, 3 open orders, 4 takings today, 5 profit today, 6 takings this week, 7 rating.</summary>
        public static readonly TableSpec Shops = new TableSpec("Web shop", "Shops")
            .Column("reallife_online_col_site", 380f, TableSort.Text, flexible: true)
            .Column("reallife_online_col_business", 340f, TableSort.Text)
            .Column("reallife_online_col_today", 190f, TableSort.Number)
            .Column("reallife_online_col_open", 160f, TableSort.Number)
            .Column("reallife_online_col_takings", 240f, TableSort.Money, sums: true)
            .Column("reallife_online_col_profit", 240f, TableSort.Money, sums: true)
            .Column("reallife_online_col_week", 260f, TableSort.Money, sums: true)
            .Column("reallife_online_col_rating", 200f, TableSort.Number, right: true)
            .Filter("reallife_tab_all_businesses", ShopKinds)
            .Empty("reallife_online_empty_shops");

        /// <summary>A site's last seven days and today: 0 day, 1 orders, 2 picked, 3 takings, 4 cost of goods, 5 delivery, 6 refunds, 7 profit, 8 late, cancelled or lost.</summary>
        public static readonly TableSpec Days = new TableSpec("Web shop", "Shops: day by day")
            .Column("reallife_online_col_day", 240f, TableSort.Date, flexible: true)
            .Column("reallife_online_col_today", 170f, TableSort.Number)
            .Column("reallife_online_col_picked", 170f, TableSort.Number)
            .Column("reallife_online_col_takings", 230f, TableSort.Money, sums: true)
            .Column("reallife_online_col_goods_cost", 230f, TableSort.Money, sums: true)
            .Column("reallife_online_col_delivery", 220f, TableSort.Money, sums: true)
            .Column("reallife_online_col_refunds", 220f, TableSort.Money, sums: true)
            .Column("reallife_online_col_profit", 230f, TableSort.Money, sums: true)
            .Column("reallife_online_col_troubles", 200f, TableSort.Number, right: true)
            .Order(0, false);

        /// <summary>Orders: 0 number, 1 online shop, 2 district, 3 goods, 4 value, 5 promised, 6 state (ordered by the hours left to its promise).</summary>
        public static readonly TableSpec Orders = new TableSpec("Web shop", "Orders")
            .Column("reallife_online_col_order", 170f, TableSort.Number)
            .Column("reallife_online_col_site", 360f, TableSort.Text)
            .Column("reallife_online_col_district", 320f, TableSort.Text)
            .Column("reallife_online_col_goods", 520f, TableSort.Text, flexible: true)
            .Column("reallife_online_col_value", 220f, TableSort.Money, sums: true)
            .Column("reallife_online_col_promised", 280f, TableSort.Date)
            .Column("reallife_online_col_state", 330f, TableSort.Custom, right: true)
            .Filter("reallife_online_all_states", OrderKinds)
            .Filter("reallife_online_all_shops")
            .Order(5, true)
            .Empty("reallife_online_empty_orders");

        /// <summary>Catalogue: 0 product, 1 in stock, 2 price in the shop, 3 online price, 4 going price, 5 sold this week, 6 asked for and not in stock this week, 7 state.</summary>
        public static readonly TableSpec Catalogue = new TableSpec("Web shop", "Catalogue")
            .Column("reallife_online_col_product", 440f, TableSort.Text, flexible: true)
            .Column("reallife_online_col_stock", 180f, TableSort.Number)
            .Column("reallife_online_col_shop_price", 210f, TableSort.Money)
            .Column("reallife_online_col_price", 210f, TableSort.Money)
            .Column("reallife_online_col_going", 210f, TableSort.Money)
            .Column("reallife_online_col_sold_week", 200f, TableSort.Number)
            .Column("reallife_online_col_missed", 220f, TableSort.Number)
            .Column("reallife_online_col_state", 280f, TableSort.Text, right: true)
            .Filter("reallife_online_all_stock", StockKinds)
            .Filter("reallife_online_all_offer", OfferKinds)
            .Empty("reallife_online_empty_catalogue");
    }
}
