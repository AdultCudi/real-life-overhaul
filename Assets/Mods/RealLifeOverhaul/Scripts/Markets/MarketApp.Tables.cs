using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The Stocks app's tables, declared as data (GameUi.TableSpec): for each tab its columns, how each sorts, which are
    /// summed, what its rows can be filtered by, and the sentence shown when a filter leaves nothing. The numbers beside
    /// each are the columns, for whoever writes the rows.
    /// </summary>
    internal static class MarketTables
    {
        // ---- whether an order is still open (the filter of the Orders tab)
        public const int IsOpen = 0, IsDone = 1;
        private static readonly string[] OrderStates = { "reallife_mkt_filter_open", "reallife_mkt_filter_done" };

        /// <summary>While the market is not running: one empty column.</summary>
        public static readonly TableSpec Off = new TableSpec("Stocks", "Off").Column("reallife_mkt_col_company", 600f, TableSort.None, flexible: true);

        /// <summary>Market: 0 index, 1 level, 2 today, 3 five days, 4 a month, 5 a quarter, 6 the last hundred days.</summary>
        public static readonly TableSpec Overview = new TableSpec("Stocks", "Market")
            .Column("reallife_mkt_col_index", 560f, TableSort.Text, flexible: true)
            .Column("reallife_mkt_col_level", 240f, TableSort.Number)
            .Column("reallife_mkt_col_today", 220f, TableSort.Number)
            .Column("reallife_mkt_col_week", 220f, TableSort.Number)
            .Column("reallife_mkt_col_month", 220f, TableSort.Number)
            .Column("reallife_mkt_col_quarter", 220f, TableSort.Number)
            .Column("reallife_mkt_col_hundred", 220f, TableSort.Number, right: true);

        /// <summary>Companies: 0 company, 1 sector, 2 price, 3 today, 4 volume, 5 worth, 6 P/E, 7 yield, 8 held.</summary>
        public static readonly TableSpec Companies = new TableSpec("Stocks", "Companies")
            .Column("reallife_mkt_col_company", 440f, TableSort.Text, flexible: true)
            .Column("reallife_mkt_col_sector", 300f, TableSort.Text)
            .Column("reallife_mkt_col_price", 190f, TableSort.Money)
            .Column("reallife_mkt_col_today", 170f, TableSort.Number)
            .Column("reallife_mkt_col_volume", 190f, TableSort.Number)
            .Column("reallife_mkt_col_worth", 200f, TableSort.Money)
            .Column("reallife_mkt_col_pe", 140f, TableSort.Number)
            .Column("reallife_mkt_col_yield", 140f, TableSort.Number)
            .Column("reallife_mkt_col_held", 170f, TableSort.Number, right: true)
            .Filter("reallife_mkt_filter_sectors", MarketCatalogue.SectorKeys)
            .Filter("reallife_mkt_filter_sizes", MarketCatalogue.SizeKeys)
            .Empty("reallife_mkt_empty_companies");

        /// <summary>Watchlist: the same columns as Companies.</summary>
        public static readonly TableSpec Watch = new TableSpec("Stocks", "Watchlist")
            .Column("reallife_mkt_col_company", 440f, TableSort.Text, flexible: true)
            .Column("reallife_mkt_col_sector", 300f, TableSort.Text)
            .Column("reallife_mkt_col_price", 190f, TableSort.Money)
            .Column("reallife_mkt_col_today", 170f, TableSort.Number)
            .Column("reallife_mkt_col_volume", 190f, TableSort.Number)
            .Column("reallife_mkt_col_worth", 200f, TableSort.Money)
            .Column("reallife_mkt_col_pe", 140f, TableSort.Number)
            .Column("reallife_mkt_col_yield", 140f, TableSort.Number)
            .Column("reallife_mkt_col_held", 170f, TableSort.Number, right: true)
            .Filter("reallife_mkt_filter_sectors", MarketCatalogue.SectorKeys)
            .Empty("reallife_mkt_empty_watch");

        /// <summary>Orders: 0 company, 1 buy or sell, 2 kind, 3 shares, 4 price, 5 lasts, 6 state, 7 placed.</summary>
        public static readonly TableSpec Orders = new TableSpec("Stocks", "Orders")
            .Column("reallife_mkt_col_company", 420f, TableSort.Text, flexible: true)
            .Column("reallife_mkt_col_side", 160f, TableSort.Text)
            .Column("reallife_mkt_col_kind", 200f, TableSort.Text)
            .Column("reallife_mkt_col_shares", 220f, TableSort.Number)
            .Column("reallife_mkt_col_price", 200f, TableSort.Money)
            .Column("reallife_mkt_col_lasts", 240f, TableSort.Text)
            .Column("reallife_mkt_col_state", 260f, TableSort.Text)
            .Column("reallife_mkt_col_placed", 180f, TableSort.Date, right: true)
            .Filter("reallife_mkt_filter_orders", OrderStates)
            .Empty("reallife_mkt_empty_orders");

        /// <summary>Portfolio: 0 company, 1 shares, 2 cost a share, 3 price, 4 worth, 5 gain or loss, 6 the gain in per cent, 7 dividends.</summary>
        public static readonly TableSpec Portfolio = new TableSpec("Stocks", "Portfolio")
            .Column("reallife_mkt_col_company", 440f, TableSort.Text, flexible: true)
            .Column("reallife_mkt_col_shares", 190f, TableSort.Number)
            .Column("reallife_mkt_col_cost_share", 220f, TableSort.Money)
            .Column("reallife_mkt_col_price", 200f, TableSort.Money)
            .Column("reallife_mkt_col_worth", 230f, TableSort.Money, sums: true)
            .Column("reallife_mkt_col_gain", 230f, TableSort.Money, sums: true)
            .Column("reallife_mkt_col_gain_pct", 170f, TableSort.Number)
            .Column("reallife_mkt_col_dividends", 230f, TableSort.Money, sums: true, right: true)
            .Filter("reallife_mkt_filter_sectors", MarketCatalogue.SectorKeys)
            .Empty("reallife_mkt_empty_portfolio");

        /// <summary>History: 0 day, 1 company, 2 what, 3 shares, 4 price, 5 money, 6 commission.</summary>
        public static readonly TableSpec History = new TableSpec("Stocks", "History")
            .Column("reallife_mkt_col_day", 160f, TableSort.Date)
            .Column("reallife_mkt_col_company", 440f, TableSort.Text, flexible: true)
            .Column("reallife_mkt_col_what", 360f, TableSort.Text)
            .Column("reallife_mkt_col_shares", 170f, TableSort.Number)
            .Column("reallife_mkt_col_price", 190f, TableSort.Money)
            .Column("reallife_mkt_col_money", 220f, TableSort.Money, sums: true)
            .Column("reallife_mkt_col_commission", 200f, TableSort.Money, sums: true, right: true)
            .Order(0, false)
            .Empty("reallife_mkt_empty_history");
    }
}
