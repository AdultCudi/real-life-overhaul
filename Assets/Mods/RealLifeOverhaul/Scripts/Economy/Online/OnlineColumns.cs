namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The columns of the Web shop app's table, all in this one place: the four cells' names in the row (the Finance
    /// app's row, FinanceTable), their widths, and each tab's four titles. A tab's writer answers one of the title
    /// sets and the frame hands it to the table (AppShell.SetColumns), so whatever the table learns to do with a
    /// column later is taken up here and nowhere else. Read when a tab is written; nothing runs otherwise.
    /// </summary>
    public static class OnlineColumns
    {
        public const float WidthA = 620f, WidthB = 500f, WidthC = 300f, WidthD = 300f;   // as the Finance and Ventures apps divide the same row
        public const int Flexible = 1;      // the cell that takes up what is left of the row's width

        public static readonly string[] Names = { "ColumnA", "ColumnB", "ColumnC", "ColumnD" };
        public static readonly float[] Widths = { WidthA, WidthB, WidthC, WidthD };

        public static readonly string[] Shops = { "reallife_online_col_site", "reallife_online_col_business", "reallife_online_col_orders", "reallife_online_col_rating" };
        public static readonly string[] Orders = { "reallife_online_col_order", "reallife_online_col_goods", "reallife_online_col_promised", "reallife_online_col_state" };
        public static readonly string[] Catalogue = { "reallife_online_col_product", "reallife_online_col_stock", "reallife_online_col_price", "reallife_online_col_sold" };
    }
}
